using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class MainMenuViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly IModrinthService _modrinth;
    private readonly IMinecraftService _minecraft;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly IInstanceService _instances;
    private readonly ICurseForgeService _curseforge;
    private readonly IBackupService _backups;

    private static readonly HttpClient ServerIconClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3),
        DefaultRequestHeaders = { { "User-Agent", "LauncherRoot/1.0" } }
    };

    private static readonly HttpClient ServerIconFallbackClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3),
        DefaultRequestHeaders = { { "User-Agent", "LauncherRoot/1.0" } }
    };

    [ObservableProperty]
    private string _playerUsername = "";

    [ObservableProperty]
    private string _playerInitial = "";

    [ObservableProperty]
    private bool _showStatusBar;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private double _statusProgress;

    [ObservableProperty]
    private ObservableCollection<Instance> _instanceList = [];

    [ObservableProperty]
    private ObservableCollection<InstanceGroupItem> _groupedInstanceList = [];

    [ObservableProperty]
    private Instance? _selectedInstance;

    [ObservableProperty]
    private bool _hasInstances;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private ObservableCollection<BackupInfo> _availableBackups = [];

    [ObservableProperty]
    private bool _showBackupPanel;

    [ObservableProperty]
    private ObservableCollection<ServerInfo> _servers = [];

    [ObservableProperty]
    private bool _showAddServerDialog;

    [ObservableProperty]
    private string _newServerName = "";

    [ObservableProperty]
    private string _newServerAddress = "";

    public ILocalizationService Localization => _localization;

    public MainMenuViewModel(
        ConfigService config,
        IModrinthService modrinth,
        IMinecraftService minecraft,
        MainWindowViewModel main,
        ILocalizationService localization,
        IInstanceService instances,
        ICurseForgeService curseforge,
        IBackupService backups)
    {
        _config = config;
        _modrinth = modrinth;
        _minecraft = minecraft;
        _main = main;
        _localization = localization;
        _instances = instances;
        _curseforge = curseforge;
        _backups = backups;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var player = await _config.LoadPlayerAsync();
        PlayerUsername = player.Username;
        PlayerInitial = !string.IsNullOrEmpty(player.Username) ? player.Username[..1].ToUpperInvariant() : "?";

        var allInstances = await _instances.LoadInstancesAsync();
        InstanceList = new ObservableCollection<Instance>(allInstances);
        HasInstances = InstanceList.Count > 0;
        RebuildGroupedList();

        var cfg = await _config.LoadConfigAsync();
        if (cfg.SelectedInstanceId != null)
            SelectedInstance = InstanceList.FirstOrDefault(i => i.Id == cfg.SelectedInstanceId);
        if (SelectedInstance == null && InstanceList.Count > 0)
            SelectedInstance = InstanceList[0];

        LoadServers(cfg);
    }

    private void LoadServers(LauncherConfig cfg)
    {
        if (cfg.Servers.Count == 0)
        {
            cfg.Servers = new List<ServerInfo>
            {
                new() { Name = "Hypixel", Address = "mc.hypixel.net" },
                new() { Name = "Mineplex", Address = "us.mineplex.com" },
                new() { Name = "Cubecraft", Address = "cubecraft.net" },
                new() { Name = "The Hive", Address = "play.hivemc.com" },
                new() { Name = "2b2t", Address = "2b2t.org" },
                new() { Name = "Wynncraft", Address = "play.wynncraft.com" },
                new() { Name = "Purple Prison", Address = "purpleprison.net" },
                new() { Name = "ManaCube", Address = "play.manacube.com" },
                new() { Name = "Plexus", Address = "plexus.gg" },
                new() { Name = "Jartex", Address = "jartexnetwork.com" },
            };
            _ = _config.SaveConfigNowAsync(cfg);
        }

        var top = cfg.Servers
            .OrderByDescending(s => s.PlayCount)
            .Take(10)
            .ToList();
        Servers = new ObservableCollection<ServerInfo>(top);
        _ = LoadServerIconsAsync(top);
    }

    private static async Task LoadServerIconsAsync(List<ServerInfo> servers)
    {
        var semaphore = new SemaphoreSlim(2, 2);
        var tasks = servers.Select(async server =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (!await TryLoadIconMcsrvstat(server))
                    await TryLoadIconMcApi(server);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    private static async Task<bool> TryLoadIconMcsrvstat(ServerInfo server)
    {
        try
        {
            var json = await ServerIconClient.GetStringAsync(
                $"https://api.mcsrvstat.us/2/{server.Address}").ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("icon", out var iconProp))
            {
                var dataUri = iconProp.GetString();
                if (dataUri != null && dataUri.StartsWith("data:image/png;base64,"))
                {
                    var base64 = dataUri["data:image/png;base64,".Length..];
                    var bytes = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(bytes);
                    var bitmap = new Bitmap(ms);
                    await Dispatcher.UIThread.InvokeAsync(() => server.Icon = bitmap);
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static async Task<bool> TryLoadIconMcApi(ServerInfo server)
    {
        try
        {
            var base64 = await ServerIconFallbackClient.GetStringAsync(
                $"https://eu.mc-api.net/v3/server/favicon/{server.Address}").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(base64))
            {
                var bytes = Convert.FromBase64String(base64.Trim('"'));
                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                await Dispatcher.UIThread.InvokeAsync(() => server.Icon = bitmap);
                return true;
            }
        }
        catch { }
        return false;
    }

    private void RebuildGroupedList()
    {
        var groups = InstanceList
            .GroupBy(i => string.IsNullOrEmpty(i.Group) ? _localization["group.ungrouped"] : i.Group)
            .OrderBy(g => g.Key == _localization["group.ungrouped"] ? 1 : 0)
            .ThenBy(g => g.Key);

        var items = new ObservableCollection<InstanceGroupItem>();
        foreach (var group in groups)
        {
            items.Add(new InstanceGroupItem { IsGroup = true, GroupName = group.Key });
            foreach (var instance in group.OrderBy(i => i.Name))
                items.Add(new InstanceGroupItem { IsGroup = false, Instance = instance });
        }
        GroupedInstanceList = items;
    }

    partial void OnSelectedInstanceChanged(Instance? value)
    {
        if (value != null)
            _ = SelectInstanceAsync(value);
    }

    private async Task SelectInstanceAsync(Instance instance)
    {
        var cfg = await _config.LoadConfigAsync();
        cfg.SelectedInstanceId = instance.Id;
        await _config.SaveConfigAsync(cfg);
    }

    [RelayCommand]
    private void SelectInstance(Instance? instance)
    {
        if (instance == null) return;
        SelectedInstance = instance;
    }

    [RelayCommand]
    private void AddInstance()
    {
        _main.NavigateTo(PageType.InstanceCreate);
    }

    [RelayCommand]
    private void OpenModManagement()
    {
        if (SelectedInstance == null)
        {
            _main.NavigateTo(PageType.InstanceCreate);
            return;
        }

        _main.NavigateTo(PageType.ModManagement);
    }

    [RelayCommand]
    private void OpenPlayerSetup()
    {
        _main.NavigateTo(PageType.PlayerSetup);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _main.NavigateTo(PageType.Settings);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task EditInstance(Instance? instance)
    {
        if (instance == null) return;

        // Auto-backup before editing
        StatusText = _localization["status.backingup"];
        ShowStatusBar = true;
        try
        {
            await _backups.BackupAsync(instance);
        }
        catch (Exception ex)
        {
            _config.Log($"Otomatik yedekleme hatası: {ex.Message}");
        }
        ShowStatusBar = false;

        _main.EditingInstance = instance;
        _main.NavigateTo(PageType.InstanceEdit);
    }

    [RelayCommand]
    private async Task ShowBackups(Instance? instance)
    {
        if (instance == null) return;
        var backups = await _backups.GetBackupsAsync(instance);
        AvailableBackups = new ObservableCollection<BackupInfo>(backups);
        ShowBackupPanel = true;
    }

    [RelayCommand]
    private void HideBackups()
    {
        ShowBackupPanel = false;
        AvailableBackups.Clear();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RestoreBackup(BackupInfo? backup)
    {
        if (backup == null || SelectedInstance == null) return;

        var instance = SelectedInstance;
        StatusText = _localization["status.restoring"];
        ShowStatusBar = true;
        HasError = false;

        var ok = await _backups.RestoreAsync(instance, backup);
        if (ok)
            ShowBackupPanel = false;
        else
        {
            HasError = true;
            StatusText = _localization["error.restore"];
            await Task.Delay(3000);
        }

        ShowStatusBar = false;
    }

    [RelayCommand]
    private async Task DeleteBackup(BackupInfo? backup)
    {
        if (backup == null) return;
        await _backups.DeleteBackupAsync(backup);
        AvailableBackups.Remove(backup);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task BackupNow(Instance? instance)
    {
        if (instance == null) return;
        StatusText = _localization["status.backingup"];
        ShowStatusBar = true;
        await _backups.BackupAsync(instance);
        ShowStatusBar = false;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DeleteInstance(Instance? instance)
    {
        if (instance == null) return;

        await _instances.DeleteInstanceAsync(instance.Id);
        InstanceList.Remove(instance);
        RebuildGroupedList();

        HasInstances = InstanceList.Count > 0;

        if (SelectedInstance == instance)
        {
            SelectedInstance = InstanceList.FirstOrDefault();
            if (SelectedInstance == null)
            {
                var cfg = await _config.LoadConfigAsync();
                cfg.SelectedInstanceId = null;
                await _config.SaveConfigAsync(cfg);
            }
        }
    }

    [RelayCommand]
    private async Task SetInstanceGroup(Instance? instance)
    {
        if (instance == null) return;

        var groups = await GetOrLoadGroupsAsync();
        var cfg = await _config.LoadConfigAsync();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return;

            var dialog = new Avalonia.Controls.Window
            {
                Title = _localization["group.select"],
                SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                Padding = new Avalonia.Thickness(20),
                MinWidth = 300,
            };

            var stack = new Avalonia.Controls.StackPanel { Spacing = 12 };

            var combo = new Avalonia.Controls.ComboBox
            {
                ItemsSource = new ObservableCollection<string>(["", .. groups]),
                SelectedItem = instance.Group,
                Width = 250,
                Height = 34,
            };

            var newGroupBox = new Avalonia.Controls.TextBox
            {
                Watermark = _localization["group.new"],
                Width = 250,
                Height = 34,
            };

            var btnPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
            };

            var cancelBtn = new Avalonia.Controls.Button
            {
                Content = _localization["microsoft.cancel"],
                MinWidth = 80,
                Height = 32,
                Classes = { "secondary" },
            };
            cancelBtn.Click += (_, _) => dialog.Close(false);

            var okBtn = new Avalonia.Controls.Button
            {
                Content = "OK",
                MinWidth = 80,
                Height = 32,
                Classes = { "primary" },
            };
            okBtn.Click += (_, _) => dialog.Close(true);

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            stack.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = _localization["group.select"],
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
            });
            stack.Children.Add(combo);
            stack.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = _localization["group.or"],
                FontSize = 11,
                Foreground = Avalonia.Media.Brushes.Gray,
            });
            stack.Children.Add(newGroupBox);
            stack.Children.Add(btnPanel);
            dialog.Content = stack;

            var result = await dialog.ShowDialog<bool>(window);
            if (!result) return;

            var selectedGroup = !string.IsNullOrWhiteSpace(newGroupBox.Text)
                ? newGroupBox.Text.Trim()
                : (combo.SelectedItem as string ?? "");

            instance.Group = selectedGroup;
            await _instances.UpdateInstanceAsync(instance);
            RebuildGroupedList();

            if (!string.IsNullOrEmpty(selectedGroup) && !cfg.Groups.Contains(selectedGroup))
            {
                cfg.Groups.Add(selectedGroup);
                await _config.SaveConfigAsync(cfg);
            }
        }
    }

    private async Task<List<string>> GetOrLoadGroupsAsync()
    {
        var cfg = await _config.LoadConfigAsync();
        var fromInstances = InstanceList
            .Select(i => i.Group)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct()
            .ToList();
        foreach (var g in fromInstances)
        {
            if (!cfg.Groups.Contains(g))
                cfg.Groups.Add(g);
        }
        await _config.SaveConfigAsync(cfg);
        return cfg.Groups;
    }

    [RelayCommand]
    private void OpenLogsFolder(Instance? instance)
    {
        if (instance == null) return;
        var logsPath = Path.Combine(_instances.GetInstanceGamePath(instance), "minecraft", "logs");
        if (!Directory.Exists(logsPath))
            logsPath = _config.GetLogsPath();
        if (Directory.Exists(logsPath))
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsPath,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    [RelayCommand]
    private async Task DuplicateInstance(Instance? instance)
    {
        if (instance == null) return;
        var clone = await _instances.DuplicateInstanceAsync(instance, $"{instance.Name} (kopya)");
        InstanceList.Add(clone);
        RebuildGroupedList();
    }

    [RelayCommand]
    private async Task ExportInstance(Instance? instance)
    {
        if (instance == null) return;

        try
        {
            var data = await _instances.ExportInstanceAsync(instance);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow;
                if (window == null) return;

                var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    SuggestedFileName = $"{instance.Name}.mrpack",
                    DefaultExtension = "mrpack",
                });
                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await stream.WriteAsync(data);
                }
            }
        }
        catch (Exception ex)
        {
            _config.Log($"Dışa aktarma hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportInstance()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return;

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("MRPack") { Patterns = ["*.mrpack"] }],
            });
            if (files.Count == 0) return;

            try
            {
                var data = await File.ReadAllBytesAsync(files[0].Path.LocalPath);
                var instance = await _instances.ImportInstanceAsync(data);
                if (instance != null)
                {
                    InstanceList.Add(instance);
                    RebuildGroupedList();
                    HasInstances = true;
                }
            }
            catch (Exception ex)
            {
                _config.Log($"İçe aktarma hatası: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void OpenInstanceFolder(Instance? instance)
    {
        if (instance == null) return;
        var path = _instances.GetInstanceGamePath(instance);
        if (Directory.Exists(path))
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    [RelayCommand]
    private void OpenLogViewer()
    {
        var window = new Views.LogViewerWindow(_localization);
        window.Show();
    }

    [RelayCommand]
    private void OpenScreenshotsFolder(Instance? instance)
    {
        if (instance == null) return;
        var path = Path.Combine(_instances.GetInstanceGamePath(instance), "minecraft", "screenshots");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(psi);
    }

    [RelayCommand]
    private async Task OpenScreenshotGallery(Instance? instance)
    {
        if (instance == null) return;
        var path = Path.Combine(_instances.GetInstanceGamePath(instance), "minecraft", "screenshots");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            return;
        }

        var files = Directory.GetFiles(path, "*.png").OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
        if (files.Length == 0) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return;

            var gallery = new Views.ScreenshotGalleryWindow(files, _localization);
            await gallery.ShowDialog(window);
        }
    }

    [RelayCommand]
    private async Task CopyError()
    {
        if (string.IsNullOrEmpty(StatusText)) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(StatusText);
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ShowStatusBar = false;
        HasError = false;
    }

    [RelayCommand]
    private void ShowAddServer()
    {
        NewServerName = "";
        NewServerAddress = "";
        ShowAddServerDialog = true;
    }

    [RelayCommand]
    private void HideAddServer()
    {
        ShowAddServerDialog = false;
    }

    [RelayCommand]
    private async Task AddServer()
    {
        if (string.IsNullOrWhiteSpace(NewServerName) || string.IsNullOrWhiteSpace(NewServerAddress))
            return;

        var cfg = await _config.LoadConfigAsync();
        var server = new ServerInfo
        {
            Name = NewServerName.Trim(),
            Address = NewServerAddress.Trim(),
        };
        cfg.Servers.Add(server);
        await _config.SaveConfigNowAsync(cfg);
        ShowAddServerDialog = false;
        LoadServers(cfg);
    }

    [RelayCommand]
    private async Task RemoveServer(ServerInfo? server)
    {
        if (server == null) return;
        var cfg = await _config.LoadConfigAsync();
        cfg.Servers.RemoveAll(s => s.Id == server.Id);
        await _config.SaveConfigNowAsync(cfg);
        LoadServers(cfg);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConnectToServer(ServerInfo? server)
    {
        if (server == null) return;
        var launcherConfig = await _config.LoadConfigAsync();
        var player = await _config.LoadPlayerAsync();

        if (string.IsNullOrWhiteSpace(player.Username))
        {
            _main.NavigateTo(PageType.PlayerSetup);
            return;
        }

        var instance = SelectedInstance;
        if (instance == null)
        {
            _main.NavigateTo(PageType.InstanceCreate);
            return;
        }

        ShowStatusBar = true;
        HasError = false;
        StatusText = _localization["status.preparing"];

        var progress = new Progress<double>(p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusProgress = p;
                StatusText = p switch
                {
                    < 0.3 => _localization["status.preparing"],
                    < 0.6 => _localization["status.downloading.minecraft"],
                    < 0.9 => _localization["status.downloading.libraries"],
                    _ => _localization["status.preparing"],
                };
            });
        });

        _minecraft.Progress = progress;
        StatusProgress = 0;
        StatusText = _localization["status.preparing"];

        var assetsDir = _config.GetAssetsPath();
        var ready = await _minecraft.EnsureInstanceReadyAsync(instance, assetsDir);
        _minecraft.Progress = null;

        if (!ready)
        {
            StatusText = _minecraft.LastError ?? _localization["error.download"];
            HasError = true;
            await Task.Delay(5000);
            ShowStatusBar = false;
            return;
        }

        StatusProgress = 0;
        StatusText = _localization["status.launching"];

        var startTime = DateTime.UtcNow;
        var launched = await _minecraft.LaunchInstanceAsync(
            instance, player.Username, launcherConfig.RamGB, _config.GetAssetsPath(),
            player.AccessToken, player.Uuid, server.Address);

        if (launched)
        {
            server.PlayCount++;
            server.LastPlayed = DateTime.UtcNow;
            var cfg = await _config.LoadConfigAsync();
            var existing = cfg.Servers.Find(s => s.Id == server.Id);
            if (existing != null)
            {
                existing.PlayCount = server.PlayCount;
                existing.LastPlayed = server.LastPlayed;
            }
            await _config.SaveConfigNowAsync(cfg);
            LoadServers(cfg);

            var elapsed = (long)(DateTime.UtcNow - startTime).TotalSeconds;
            _ = _instances.AddPlayTimeAsync(instance.Id, elapsed);
            instance.PlayTimeSeconds += elapsed;
            Environment.Exit(0);
        }
        else
        {
            StatusText = _minecraft.LastError ?? _localization["error.java"];
            HasError = true;
            await Task.Delay(5000);
            ShowStatusBar = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Play()
    {
        var launcherConfig = await _config.LoadConfigAsync();
        var player = await _config.LoadPlayerAsync();

        if (string.IsNullOrWhiteSpace(player.Username))
        {
            _main.NavigateTo(PageType.PlayerSetup);
            return;
        }

        var instance = SelectedInstance;
        if (instance == null)
        {
            _main.NavigateTo(PageType.InstanceCreate);
            return;
        }

        ShowStatusBar = true;
        HasError = false;
        StatusText = _localization["status.preparing"];

        var progress = new Progress<double>(p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusProgress = p;
                StatusText = p switch
                {
                    < 0.3 => _localization["status.preparing"],
                    < 0.6 => _localization["status.downloading.minecraft"],
                    < 0.9 => _localization["status.downloading.libraries"],
                    _ => _localization["status.preparing"],
                };
            });
        });

        _minecraft.Progress = progress;
        StatusProgress = 0;
        StatusText = _localization["status.preparing"];

        var assetsDir = _config.GetAssetsPath();
        var ready = await _minecraft.EnsureInstanceReadyAsync(instance, assetsDir);
        _minecraft.Progress = null;

        if (!ready)
        {
            StatusText = _minecraft.LastError ?? _localization["error.download"];
            HasError = true;
            await Task.Delay(5000);
            ShowStatusBar = false;
            return;
        }

        StatusProgress = 0;
        StatusText = _localization["status.launching"];

        var startTime = DateTime.UtcNow;
        var launched = await _minecraft.LaunchInstanceAsync(
            instance, player.Username, launcherConfig.RamGB, _config.GetAssetsPath(),
            player.AccessToken, player.Uuid);

        if (launched)
        {
            var elapsed = (long)(DateTime.UtcNow - startTime).TotalSeconds;
            _ = _instances.AddPlayTimeAsync(instance.Id, elapsed);
            instance.PlayTimeSeconds += elapsed;
            Environment.Exit(0);
        }
        else
        {
            StatusText = _minecraft.LastError ?? _localization["error.java"];
            HasError = true;
            await Task.Delay(5000);
            ShowStatusBar = false;
        }
    }
}
