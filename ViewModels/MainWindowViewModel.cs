using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public enum PageType
{
    PlayerSetup,
    ThemeSelection,
    Splash,
    MainMenu,
    ModManagement,
    Settings,
    InstanceCreate,
    InstanceEdit,
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly IModrinthService _modrinth;
    private readonly IMinecraftService _minecraft;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly IInstanceService _instances;
    private readonly IUpdateService _updater;
    private readonly IInstallService _installer;
    private readonly ICurseForgeService _curseforge;
    private readonly IBackupService _backups;

    private int _navigationStack;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private string _windowTitle = "LauncherRoot";

    public ILocalizationService Localization => _localization;

    public event Action? Navigating;

    public Instance? EditingInstance { get; set; }

    public MainWindowViewModel(
        ConfigService config,
        IModrinthService modrinth,
        IMinecraftService minecraft,
        IThemeService themeService,
        ILocalizationService localization,
        IInstanceService instances,
        IUpdateService updater,
        IInstallService installer,
        ICurseForgeService curseforge,
        IBackupService backups)
    {
        _config = config;
        _modrinth = modrinth;
        _minecraft = minecraft;
        _themeService = themeService;
        _localization = localization;
        _instances = instances;
        _updater = updater;
        _installer = installer;
        _curseforge = curseforge;
        _backups = backups;

        _localization.PropertyChanged += OnLocalizationChanged;

        _ = InitializeAsync();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item" || e.PropertyName == "CurrentLanguage")
        {
            WindowTitle = _localization["app.title"];
        }
    }

    private async Task InitializeAsync()
    {
        await _installer.InstallAsync();

        var cfg = await _config.LoadConfigAsync();

        if (!string.IsNullOrEmpty(Program.LaunchInstanceId))
        {
            var instance = await _instances.GetInstanceAsync(Program.LaunchInstanceId);
            if (instance != null)
            {
                cfg.SelectedInstanceId = instance.Id;
                if (!string.IsNullOrEmpty(Program.LaunchPlayerName))
                {
                    var accounts = await _config.LoadAccountsAsync();
                    var player = accounts.Find(a => a.Username == Program.LaunchPlayerName);
                    if (player != null)
                        await _config.SwitchAccountAsync(player.Id);
                }
                NavigateTo(PageType.MainMenu);
                return;
            }
        }

        var hasPlayer = !string.IsNullOrEmpty(cfg.ActiveAccountId) || (await _config.LoadAccountsAsync()).Count > 0;
        NavigateTo(hasPlayer ? PageType.MainMenu : PageType.PlayerSetup);
    }

    private void SwitchViewModel(object? vm)
    {
        if (CurrentViewModel is IDisposable old)
            old.Dispose();
        CurrentViewModel = vm;
        _navigationStack++;
    }

    public void NavigateTo(PageType page)
    {
        try
        {
            switch (page)
            {
                case PageType.PlayerSetup:
                    Navigating?.Invoke();
                    SwitchViewModel(new PlayerSetupViewModel(_config, this, _localization));
                    break;
                case PageType.ThemeSelection:
                    Navigating?.Invoke();
                    SwitchViewModel(new ThemeSelectionViewModel(_config, _modrinth, this, _localization, _instances, _themeService));
                    break;
                case PageType.Splash:
                    Navigating?.Invoke();
                    SwitchViewModel(new SplashViewModel(this, _localization));
                    break;
                case PageType.MainMenu:
                    Navigating?.Invoke();
                    SwitchViewModel(new MainMenuViewModel(_config, _modrinth, _minecraft, this, _localization, _instances, _curseforge, _backups));
                    break;
                case PageType.ModManagement:
                    _ = NavigateToModManagementAsync();
                    break;
                case PageType.Settings:
                    Navigating?.Invoke();
                    SwitchViewModel(new SettingsViewModel(_config, this, _localization, _updater, _installer));
                    break;
                case PageType.InstanceCreate:
                    Navigating?.Invoke();
                    SwitchViewModel(new InstanceCreateViewModel(_config, _minecraft, _instances, this, _localization));
                    break;
                case PageType.InstanceEdit:
                    NavigateToInstanceEdit();
                    break;
            }
        }
        catch (Exception ex)
        {
            _config.Log($"NavigateTo hatası ({page}): {ex.Message}");
        }
    }

    private void NavigateToInstanceEdit()
    {
        try
        {
            if (EditingInstance == null) return;
            Navigating?.Invoke();
            SwitchViewModel(new InstanceCreateViewModel(_config, _minecraft, _instances, this, _localization, EditingInstance));
        }
        catch (Exception ex)
        {
            _config.Log($"NavigateToInstanceEdit hatası: {ex.Message}");
        }
    }

    private async Task NavigateToModManagementAsync()
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            if (cfg.SelectedInstanceId == null) return;
            var instance = await _instances.GetInstanceAsync(cfg.SelectedInstanceId);
            if (instance == null) return;
            Navigating?.Invoke();
            SwitchViewModel(new ModManagementViewModel(
                _config, _modrinth, _curseforge, _instances, instance, this, _localization));
        }
        catch (Exception ex)
        {
            _config.Log($"NavigateToModManagement hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_navigationStack > 0)
        {
            NavigateTo(PageType.MainMenu);
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (CurrentViewModel is INotifyPropertyChanged)
        {
            var current = CurrentViewModel;
            var page = current switch
            {
                MainMenuViewModel => PageType.MainMenu,
                ModManagementViewModel => PageType.ModManagement,
                SettingsViewModel => PageType.Settings,
                PlayerSetupViewModel => PageType.PlayerSetup,
                InstanceCreateViewModel => PageType.InstanceCreate,
                ThemeSelectionViewModel => PageType.ThemeSelection,
                _ => PageType.MainMenu,
            };
            NavigateTo(page);
        }
    }
}
