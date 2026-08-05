using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class ModManagementViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly IModrinthService _modrinth;
    private readonly ICurseForgeService _curseforge;
    private readonly IInstanceService _instances;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly Instance _instance;
    private readonly string _modsPath;
    private readonly string _rpPath;
    private readonly string _spPath;

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders =
        {
            { "User-Agent", "LauncherRoot/1.0" }
        }
    };

    private static readonly HttpClient IconHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders =
        {
            { "User-Agent", "LauncherRoot/1.0" }
        }
    };

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedMods = [];

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedResourcePacks = [];

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedShaderPacks = [];

    [ObservableProperty]
    private ObservableCollection<ModSearchResult> _searchResults = [];

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchStatus = "";

    [ObservableProperty]
    private string _instanceName = "";

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _isIrisInstalled;

    [ObservableProperty]
    private bool _isInstallingIris;

    [ObservableProperty]
    private string _searchButtonText = "";

    [ObservableProperty]
    private bool _isCurseForgeSource;

    [ObservableProperty]
    private string _sourceLabel = "";

    public ILocalizationService Localization => _localization;

    public string CurrentProjectType => SelectedTab switch
    {
        1 => "resourcepack",
        2 => "shader",
        _ => "mod",
    };

    public ModManagementViewModel(
        ConfigService config,
        IModrinthService modrinth,
        ICurseForgeService curseforge,
        IInstanceService instances,
        Instance instance,
        MainWindowViewModel main,
        ILocalizationService localization)
    {
        _config = config;
        _modrinth = modrinth;
        _curseforge = curseforge;
        _instances = instances;
        _instance = instance;
        _main = main;
        _localization = localization;
        _modsPath = _instances.GetInstanceModsPath(instance);
        _rpPath = _instances.GetInstanceResourcepackPath(instance);
        _spPath = _instances.GetInstanceShaderpackPath(instance);
        InstanceName = instance.DisplayName;
        Directory.CreateDirectory(_modsPath);
        Directory.CreateDirectory(_rpPath);
        Directory.CreateDirectory(_spPath);
        _ = LoadInstalledModsAsync();
        _ = LoadPacksAsync();
        UpdateSearchButtonText();
        UpdateSourceLabel();
    }

    partial void OnSelectedTabChanged(int value)
    {
        SearchResults.Clear();
        SearchStatus = "";
        SearchQuery = "";
        OnPropertyChanged(nameof(CurrentProjectType));
        UpdateSearchButtonText();
        CheckIrisInstalled();
    }

    partial void OnIsCurseForgeSourceChanged(bool value)
    {
        SearchResults.Clear();
        SearchQuery = "";
        SearchStatus = value ? _localization["modmng.cf.warning"] : "";
        UpdateSourceLabel();
        UpdateSearchButtonText();
    }

    private void UpdateSourceLabel()
    {
        SourceLabel = IsCurseForgeSource ? "CurseForge" : "Modrinth";
    }

    private void UpdateSearchButtonText()
    {
        SearchButtonText = IsCurseForgeSource
            ? _localization["modmng.search.cf"]
            : SelectedTab switch
            {
                1 => _localization["modmng.search.rp"],
                2 => _localization["modmng.search.shader"],
                _ => _localization["modmng.search.mod"],
            };
    }

    private void CheckIrisInstalled()
    {
        if (SelectedTab == 2)
            IsIrisInstalled = InstalledMods.Any(m =>
                m.Slug.Contains("iris", StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadInstalledModsAsync()
    {
        var modState = await _instances.LoadModStateAsync(_instance);
        var mods = new ObservableCollection<ModInfo>();

        if (Directory.Exists(_modsPath))
        {
            foreach (var file in Directory.GetFiles(_modsPath, "*.jar"))
            {
                var fileName = Path.GetFileName(file);
                var slug = Path.GetFileNameWithoutExtension(file);
                mods.Add(new ModInfo
                {
                    Slug = slug,
                    Name = slug,
                    FileName = fileName,
                    Downloaded = true,
                    Enabled = modState.ModEnabled.GetValueOrDefault(slug, true)
                });
            }

            foreach (var file in Directory.GetFiles(_modsPath, "*.jar.disabled"))
            {
                var fileName = Path.GetFileName(file);
                var baseName = fileName[..^".disabled".Length];
                var slug = Path.GetFileNameWithoutExtension(baseName);
                mods.Add(new ModInfo
                {
                    Slug = slug,
                    Name = slug,
                    FileName = baseName,
                    Downloaded = true,
                    Enabled = false
                });
            }
        }

        InstalledMods = mods;
        _ = CheckUpdatesAsync();
        CheckIrisInstalled();
    }

    private Task LoadPacksAsync()
    {
        var rps = new ObservableCollection<ModInfo>();
        foreach (var file in Directory.GetFiles(_rpPath, "*.zip"))
        {
            var fileName = Path.GetFileName(file);
            rps.Add(new ModInfo { Slug = fileName, Name = fileName, FileName = fileName, Downloaded = true, Enabled = true });
        }
        foreach (var file in Directory.GetFiles(_rpPath, "*.zip.disabled"))
        {
            var fileName = Path.GetFileName(file);
            var baseName = fileName[..^".disabled".Length];
            rps.Add(new ModInfo { Slug = baseName, Name = baseName, FileName = baseName, Downloaded = true, Enabled = false });
        }
        InstalledResourcePacks = rps;

        var sps = new ObservableCollection<ModInfo>();
        foreach (var file in Directory.GetFiles(_spPath, "*.zip"))
        {
            var fileName = Path.GetFileName(file);
            sps.Add(new ModInfo { Slug = fileName, Name = fileName, FileName = fileName, Downloaded = true, Enabled = true });
        }
        foreach (var file in Directory.GetFiles(_spPath, "*.zip.disabled"))
        {
            var fileName = Path.GetFileName(file);
            var baseName = fileName[..^".disabled".Length];
            sps.Add(new ModInfo { Slug = baseName, Name = baseName, FileName = baseName, Downloaded = true, Enabled = false });
        }
        InstalledShaderPacks = sps;
        return Task.CompletedTask;
    }

    private async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdates || InstalledMods.Count == 0) return;
        IsCheckingUpdates = true;

        var updateTasks = InstalledMods.Select(mod => CheckModUpdateAsync(mod));
        await Task.WhenAll(updateTasks);

        IsCheckingUpdates = false;
    }

    private async Task CheckModUpdateAsync(ModInfo mod)
    {
        try
        {
            var info = await _modrinth.CheckUpdateAsync(mod.Slug, _instance.Version, _instance.Loader, mod.FileName ?? "");
            if (info?.HasUpdate == true)
            {
                mod.OldFileName ??= mod.FileName;
                mod.HasUpdate = true;
                mod.LatestVersion = info.LatestVersion;
                mod.DownloadUrl = info.DownloadUrl;
                mod.FileName = info.FileName;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task ToggleItem(ModInfo? item)
    {
        if (item == null || string.IsNullOrEmpty(item.FileName)) return;

        var folder = SelectedTab switch
        {
            1 => _rpPath,
            2 => _spPath,
            _ => _modsPath,
        };

        var enabledPath = Path.Combine(folder, item.FileName);
        var disabledPath = enabledPath + ".disabled";

        try
        {
            if (item.Enabled)
            {
                if (File.Exists(disabledPath))
                    File.Move(disabledPath, enabledPath);
            }
            else if (File.Exists(enabledPath))
            {
                File.Move(enabledPath, disabledPath);
            }

            if (SelectedTab == 0)
            {
                var modState = await _instances.LoadModStateAsync(_instance);
                modState.ModEnabled[item.Slug] = item.Enabled;
                await _instances.SaveModStateAsync(_instance, modState);
            }
        }
        catch (Exception ex)
        {
            _config.Log($"Durum değiştirilemedi ({item.Slug}): {ex.Message}");
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        SearchStatus = _localization["modmng.searching"];

        var results = new ObservableCollection<ModSearchResult>();

        if (IsCurseForgeSource)
        {
            var cfClassId = SelectedTab switch
            {
                1 => 12,    // resource packs
                2 => 6552,  // shader packs
                _ => 6,     // mods
            };
            var cfResults = await _curseforge.SearchModsAsync(SearchQuery, _instance.Version, _instance.Loader, cfClassId);
            foreach (var mod in cfResults)
            {
                results.Add(new ModSearchResult
                {
                    Source = "curseforge",
                    Title = mod.Name,
                    Description = mod.Summary,
                    Slug = mod.Slug,
                    CurseForgeId = mod.Id,
                    IconUrl = mod.Logo?.ThumbnailUrl ?? mod.Logo?.Url ?? "",
                });
            }

            _ = LoadIconsAsync(results.ToList());
        }
        else
        {
            var projectType = CurrentProjectType;
            var modrinthResults = await _modrinth.SearchModsAsync(SearchQuery, _instance.Loader, projectType);
            foreach (var hit in modrinthResults)
            {
                results.Add(new ModSearchResult
                {
                    Source = "modrinth",
                    Title = hit.Title,
                    Description = hit.Description,
                    Slug = hit.Slug,
                    IconUrl = hit.IconUrl ?? "",
                });
            }

            _ = LoadIconsAsync(results.ToList());
        }

        SearchResults = results;

        SearchStatus = results.Count > 0
            ? $"{results.Count} {_localization["modmng.results"]}"
            : _localization["modmng.noresults"];
        IsSearching = false;
    }

    private static async Task LoadIconsAsync(List<ModSearchResult> results)
    {
        var tasks = results
            .Where(hit => !string.IsNullOrWhiteSpace(hit.IconUrl))
            .Select(hit => LoadSingleIconAsync(hit));

        await Task.WhenAll(tasks);
    }

    private static async Task LoadSingleIconAsync(ModSearchResult hit)
    {
        try
        {
            var data = await IconHttpClient.GetByteArrayAsync(hit.IconUrl);
            using var ms = new MemoryStream(data);
            var bitmap = new Bitmap(ms);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => hit.Icon = bitmap);
        }
        catch { }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InstallMod(ModSearchResult? hit)
    {
        if (hit == null) return;

        var folder = SelectedTab switch
        {
            1 => _rpPath,
            2 => _spPath,
            _ => _modsPath,
        };

        try
        {
            string? downloadUrl = null;
            string? fileName = null;

            if (hit.Source == "curseforge")
            {
                var bestFile = await _curseforge.GetBestFileAsync(hit.CurseForgeId, _instance.Version, _instance.Loader);
                downloadUrl = bestFile?.DownloadUrl;
                fileName = bestFile?.FileName ?? $"{hit.Slug}.jar";
            }
            else
            {
                var info = await _modrinth.GetModInfoAsync(hit.Slug, _instance.Version, _instance.Loader, CurrentProjectType);
                downloadUrl = info?.DownloadUrl;
                fileName = info?.FileName ?? $"{hit.Slug}{(SelectedTab == 0 ? ".jar" : ".zip")}";
            }

            if (downloadUrl == null) return;

            var filePath = Path.Combine(folder, fileName);

            if (!File.Exists(filePath))
            {
                var response = await SharedHttpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(filePath);
                await response.Content.CopyToAsync(fs);
            }

            if (SelectedTab == 0)
            {
                if (InstalledMods.Any(m => m.Slug == hit.Slug)) return;
                InstalledMods.Add(new ModInfo
                {
                    Slug = hit.Slug,
                    Name = hit.Title,
                    FileName = fileName,
                    Downloaded = true,
                    Enabled = true
                });
                _ = CheckUpdatesAsync();

                var modState = await _instances.LoadModStateAsync(_instance);
                modState.ModEnabled[hit.Slug] = true;
                await _instances.SaveModStateAsync(_instance, modState);
            }
            else
            {
                var list = SelectedTab == 1 ? InstalledResourcePacks : InstalledShaderPacks;
                if (list.Any(m => m.Slug == hit.Slug)) return;
                list.Add(new ModInfo
                {
                    Slug = hit.Slug,
                    Name = hit.Title,
                    FileName = fileName,
                    Downloaded = true,
                    Enabled = true
                });
            }

            CheckIrisInstalled();
        }
        catch (Exception ex)
        {
            _config.Log($"Yükleme hatası ({hit.Slug}): {ex.Message}");
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task UpdateMod(ModInfo? mod)
    {
        if (mod?.DownloadUrl == null || mod.FileName == null) return;

        try
        {
            var suffix = mod.Enabled ? "" : ".disabled";
            var filePath = Path.Combine(_modsPath, mod.FileName + suffix);
            var backup = filePath + ".bak";

            var oldName = mod.OldFileName != null && mod.OldFileName != mod.FileName
                ? mod.OldFileName
                : null;
            var oldPath = oldName != null ? Path.Combine(_modsPath, oldName + suffix) : null;

            if (File.Exists(filePath)) File.Move(filePath, backup);

            try
            {
                var response = await SharedHttpClient.GetAsync(mod.DownloadUrl);
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(filePath);
                await response.Content.CopyToAsync(fs);
                if (File.Exists(backup)) File.Delete(backup);
                if (oldPath != null && File.Exists(oldPath)) File.Delete(oldPath);
                mod.HasUpdate = false;
                mod.OldFileName = null;
                _config.Log($"Mod güncellendi: {mod.Slug}");
            }
            catch
            {
                if (File.Exists(backup)) File.Move(backup, filePath);
                throw;
            }
        }
        catch (Exception ex)
        {
            _config.Log($"Mod güncelleme hatası ({mod.Slug}): {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out var idx))
            SelectedTab = idx;
    }

    [RelayCommand]
    private void ToggleSource()
    {
        IsCurseForgeSource = !IsCurseForgeSource;
    }

    [RelayCommand]
    private async Task InstallIris()
    {
        if (IsInstallingIris) return;
        IsInstallingIris = true;

        try
        {
            var info = await _modrinth.GetModInfoAsync("iris", _instance.Version, _instance.Loader);
            if (info?.DownloadUrl == null) return;

            var fileName = info.FileName ?? "iris.jar";
            var filePath = Path.Combine(_modsPath, fileName);

            if (!File.Exists(filePath))
            {
                var response = await SharedHttpClient.GetAsync(info.DownloadUrl);
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(filePath);
                await response.Content.CopyToAsync(fs);
            }

            if (!InstalledMods.Any(m => m.Slug == "iris"))
            {
                InstalledMods.Add(new ModInfo
                {
                    Slug = "iris",
                    Name = "Iris Shaders",
                    FileName = fileName,
                    Downloaded = true,
                    Enabled = true,
                });
            }

            IsIrisInstalled = true;
        }
        catch (Exception ex)
        {
            _config.Log($"Iris yükleme hatası: {ex.Message}");
        }
        finally
        {
            IsInstallingIris = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        _main.NavigateTo(PageType.MainMenu);
    }
}
