using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class InstanceCreateViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly IMinecraftService _minecraft;
    private readonly IInstanceService _instances;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly HttpClient _http;
    private readonly Instance? _editingInstance;

    [ObservableProperty]
    private ObservableCollection<string> _availableVersions = [];

    [ObservableProperty]
    private string _selectedVersion = "1.21.4";

    [ObservableProperty]
    private string _selectedLoader = "vanilla";

    [ObservableProperty]
    private string _instanceName = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private ObservableCollection<string> _availableGroups = [];

    [ObservableProperty]
    private string _selectedGroup = "";

    public ILocalizationService Localization => _localization;

    public bool IsEditing => _editingInstance != null;
    public bool IsFabric => SelectedLoader == "fabric";
    public bool IsForge => SelectedLoader == "forge";
    public bool IsNeoforge => SelectedLoader == "neoforge";
    public bool IsQuilt => SelectedLoader == "quilt";
    public bool IsVanilla => SelectedLoader == "vanilla";

    public InstanceCreateViewModel(
        ConfigService config,
        IMinecraftService minecraft,
        IInstanceService instances,
        MainWindowViewModel main,
        ILocalizationService localization,
        Instance? existingInstance = null)
    {
        _config = config;
        _minecraft = minecraft;
        _instances = instances;
        _main = main;
        _localization = localization;
        _editingInstance = existingInstance;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");

        _ = LoadGroupsAsync();

        if (_editingInstance != null)
        {
            InstanceName = _editingInstance.Name;
            SelectedVersion = _editingInstance.Version;
            SelectedLoader = _editingInstance.Loader;
            SelectedGroup = _editingInstance.Group;
            _ = LoadVersionsAsync();
        }
        else
        {
            _ = LoadVersionsAsync();
        }
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            var groups = new ObservableCollection<string> { "" };
            foreach (var g in cfg.Groups)
                groups.Add(g);
            AvailableGroups = groups;
        }
        catch
        {
            AvailableGroups = new ObservableCollection<string> { "" };
        }
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            var manifest = await _http.GetFromJsonAsync<JsonElement>(
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");

            var versions = manifest.GetProperty("versions").EnumerateArray()
                .Select(v => v.GetProperty("id").GetString()!)
                .Distinct()
                .OrderByDescending(v => System.Version.TryParse(v, out var ver) ? ver : new System.Version(0, 0))
                .ToList();

            AvailableVersions = new ObservableCollection<string>(versions);
            if (AvailableVersions.Count > 0)
                SelectedVersion = AvailableVersions[0];
        }
        catch
        {
            AvailableVersions = new ObservableCollection<string>
            {
                "1.21.4", "1.21.3", "1.21.1", "1.21",
                "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.20",
                "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19",
                "1.18.2", "1.18.1", "1.18",
                "1.17.1", "1.17",
                "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1", "1.16",
                "1.15.2", "1.15.1", "1.15",
                "1.14.4", "1.14.3", "1.14.2", "1.14.1", "1.14",
                "1.13.2", "1.13.1", "1.13",
                "1.12.2", "1.12.1", "1.12",
                "1.11.2", "1.11.1", "1.11",
                "1.10.2", "1.10.1", "1.10",
                "1.9.4", "1.9.3", "1.9.2", "1.9.1", "1.9",
                "1.8.9", "1.8.8", "1.8.7", "1.8.6", "1.8.5", "1.8.4", "1.8.3", "1.8.2", "1.8.1", "1.8",
                "1.7.10", "1.7.9", "1.7.8", "1.7.7", "1.7.6", "1.7.5", "1.7.4", "1.7.3", "1.7.2"
            };
            SelectedVersion = "1.21.4";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedVersionChanged(string value)
    {
        UpdateInstanceName();
    }

    partial void OnSelectedLoaderChanged(string value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsFabric));
        OnPropertyChanged(nameof(IsForge));
        OnPropertyChanged(nameof(IsNeoforge));
        OnPropertyChanged(nameof(IsQuilt));
        OnPropertyChanged(nameof(IsVanilla));
        UpdateInstanceName();
    }

    private void UpdateInstanceName()
    {
        var loaderLabel = SelectedLoader switch
        {
            "fabric" => "Fabric",
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "quilt" => "Quilt",
            _ => "Vanilla",
        };
        InstanceName = $"{SelectedVersion} - {loaderLabel}";
    }

    [RelayCommand]
    private void SelectLoader(string loader)
    {
        SelectedLoader = loader;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Create()
    {
        if (IsCreating) return;
        IsCreating = true;
        ErrorMessage = "";

        try
        {
            var allInstances = await _instances.LoadInstancesAsync();

            var duplicate = allInstances.Any(i =>
                i.Name == InstanceName &&
                (_editingInstance == null || i.Id != _editingInstance.Id));
            if (duplicate)
            {
                ErrorMessage = "Bu isimde bir profil zaten var.";
                return;
            }

            if (_editingInstance != null)
            {
                _editingInstance.Name = InstanceName;
                _editingInstance.Version = SelectedVersion;
                _editingInstance.Loader = SelectedLoader;
                _editingInstance.Group = SelectedGroup;
                await _instances.UpdateInstanceAsync(_editingInstance);

                _config.Log($"Instance güncellendi: {_editingInstance.Name} ({_editingInstance.Loader} {_editingInstance.Version})");
            }
            else
            {
                var instance = new Instance
                {
                    Name = InstanceName,
                    Version = SelectedVersion,
                    Loader = SelectedLoader,
                    Group = SelectedGroup,
                };

                await _instances.AddInstanceAsync(instance);

                var cfg = await _config.LoadConfigAsync();
                cfg.SelectedInstanceId = instance.Id;
                await _config.SaveConfigAsync(cfg);

                SaveNewGroup(SelectedGroup);

                _config.Log($"Instance oluşturuldu: {instance.Name} ({instance.Loader} {instance.Version})");
            }

            _main.NavigateTo(PageType.MainMenu);
        }
        catch (System.Exception ex)
        {
            ErrorMessage = $"Hata: {ex.Message}";
            _config.Log($"Instance kaydetme hatası: {ex.Message}");
        }
        finally
        {
            IsCreating = false;
        }
    }

    private async void SaveNewGroup(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        try
        {
            var cfg = await _config.LoadConfigAsync();
            if (!cfg.Groups.Contains(group))
            {
                cfg.Groups.Add(group);
                await _config.SaveConfigAsync(cfg);
            }
        }
        catch { }
    }

    [RelayCommand]
    private void Back()
    {
        _main.NavigateTo(PageType.MainMenu);
    }
}
