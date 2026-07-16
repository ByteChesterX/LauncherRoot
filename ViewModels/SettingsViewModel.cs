using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly JavaService _java;
    private readonly IUpdateService _updater;
    private readonly IInstallService _installer;

    [ObservableProperty]
    private int _ramGB = 4;

    [ObservableProperty]
    private int _selectedFpsIndex;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isTurkish = true;

    [ObservableProperty]
    private string _jvmArgs = "";

    [ObservableProperty]
    private string _preLaunchCommand = "";

    [ObservableProperty]
    private string _wrapperCommand = "";

    [ObservableProperty]
    private string _postExitCommand = "";

    [ObservableProperty]
    private string _javaPath = "";

    [ObservableProperty]
    private int _windowWidth = 854;

    [ObservableProperty]
    private int _windowHeight = 480;

    [ObservableProperty]
    private bool _isJavaInstalled;

    [ObservableProperty]
    private bool _isDownloadingJava;

    [ObservableProperty]
    private string _javaStatusText = "";

    [ObservableProperty]
    private string _currentVersion = "";

    [ObservableProperty]
    private string _updateStatus = "";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    public ILocalizationService Localization => _localization;

    public ObservableCollection<string> FpsOptions { get; } =
    [
        "30",
        "60",
        "120",
        "144",
        "240",
        "",
    ];

    public SettingsViewModel(ConfigService config, MainWindowViewModel main, ILocalizationService localization, IUpdateService updater, IInstallService installer)
    {
        _config = config;
        _main = main;
        _localization = localization;
        _java = new JavaService();
        _updater = updater;
        _installer = installer;

        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            RamGB = cfg.RamGB;
            SelectedFpsIndex = cfg.FpsLimit switch
            {
                30 => 0,
                60 => 1,
                120 => 2,
                144 => 3,
                240 => 4,
                _ => 5,
            };
            IsDarkTheme = cfg.DarkTheme;
            IsTurkish = cfg.Language != "en";
            JvmArgs = cfg.JvmArgs;
            PreLaunchCommand = cfg.PreLaunchCommand;
            WrapperCommand = cfg.WrapperCommand;
            PostExitCommand = cfg.PostExitCommand;
            JavaPath = cfg.JavaPath;
            WindowWidth = cfg.WindowWidth;
            WindowHeight = cfg.WindowHeight;
        }
        catch { }

        IsJavaInstalled = !string.IsNullOrEmpty(JavaPath) || JavaService.FindSystemJava() != null;
        JavaStatusText = IsJavaInstalled
            ? string.Format(_localization["settings.java.installed"], JavaPath ?? "system")
            : _localization["settings.java.notfound"];

        CurrentVersion = string.Format(_localization["settings.update.current"], _updater.CurrentVersion);
    }

    public Task<string?> ResolveJavaPathAsync()
    {
        if (!string.IsNullOrEmpty(JavaPath))
            return Task.FromResult<string?>(JavaPath);
        return Task.FromResult(JavaService.FindSystemJava());
    }

    partial void OnRamGBChanged(int value) => _ = SaveConfigPropertyAsync(cfg => cfg.RamGB = value);
    partial void OnSelectedFpsIndexChanged(int value) => _ = SaveFpsAsync(value);
    partial void OnIsDarkThemeChanged(bool value) => _ = SaveThemeAsync(value);
    partial void OnIsTurkishChanged(bool value) => _ = SaveLanguageAsync(value ? "tr" : "en");
    partial void OnJvmArgsChanged(string value) => _ = SaveConfigPropertyAsync(cfg => cfg.JvmArgs = value);
    partial void OnPreLaunchCommandChanged(string value) => _ = SaveConfigPropertyAsync(cfg => cfg.PreLaunchCommand = value);
    partial void OnWrapperCommandChanged(string value) => _ = SaveConfigPropertyAsync(cfg => cfg.WrapperCommand = value);
    partial void OnPostExitCommandChanged(string value) => _ = SaveConfigPropertyAsync(cfg => cfg.PostExitCommand = value);
    partial void OnJavaPathChanged(string value) => _ = SaveConfigPropertyAsync(cfg => cfg.JavaPath = value);
    partial void OnWindowWidthChanged(int value) => _ = SaveConfigPropertyAsync(cfg => cfg.WindowWidth = value);
    partial void OnWindowHeightChanged(int value) => _ = SaveConfigPropertyAsync(cfg => cfg.WindowHeight = value);

    private async Task SaveConfigPropertyAsync(Action<LauncherConfig> setter)
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            setter(cfg);
            await _config.SaveConfigAsync(cfg);
        }
        catch (Exception ex)
        {
            _config.Log($"Config kaydetme hatası: {ex.Message}");
        }
    }

    private async Task SaveFpsAsync(int index)
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            cfg.FpsLimit = index switch
            {
                0 => 30,
                1 => 60,
                2 => 120,
                3 => 144,
                4 => 240,
                _ => 0,
            };
            await _config.SaveConfigAsync(cfg);
        }
        catch (Exception ex)
        {
            _config.Log($"FPS kaydetme hatası: {ex.Message}");
        }
    }

    private async Task SaveThemeAsync(bool dark)
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            cfg.DarkTheme = dark;
            await _config.SaveConfigAsync(cfg);
            App.SetTheme(dark);
        }
        catch (Exception ex)
        {
            _config.Log($"Tema kaydetme hatası: {ex.Message}");
        }
    }

    private async Task SaveLanguageAsync(string lang)
    {
        try
        {
            var cfg = await _config.LoadConfigAsync();
            cfg.Language = lang;
            await _config.SaveConfigAsync(cfg);
            await _localization.SetLanguageAsync(lang);
        }
        catch (Exception ex)
        {
            _config.Log($"Dil kaydetme hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Back()
    {
        _main.NavigateTo(PageType.MainMenu);
    }

    [RelayCommand]
    private async Task InstallDesktop()
    {
        await _installer.InstallAsync(force: true);
    }

    [RelayCommand]
    private async Task DownloadJava()
    {
        if (IsDownloadingJava) return;
        IsDownloadingJava = true;
        JavaStatusText = _localization["settings.java.detecting"];

        try
        {
            var installDir = System.IO.Path.Combine(_config.RootPath, "java");
            var path = await _java.DownloadJavaAsync(installDir);
            if (path != null)
            {
                JavaPath = path;
                IsJavaInstalled = true;
                JavaStatusText = string.Format(_localization["settings.java.installed"], path);
            }
        }
        finally
        {
            IsDownloadingJava = false;
        }
    }

    [RelayCommand]
    private async Task ResetLauncher()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return;

            var dialog = new Avalonia.Controls.Window
            {
                Title = _localization["settings.reset"],
                SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                MinWidth = 340,
                MaxWidth = 480,
                Padding = new Avalonia.Thickness(24),
            };

            var stack = new Avalonia.Controls.StackPanel { Spacing = 16 };

            stack.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = _localization["settings.reset.confirm"],
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });

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

            var confirmBtn = new Avalonia.Controls.Button
            {
                Content = _localization["settings.resetbtn"],
                MinWidth = 80,
                Height = 32,
                Classes = { "danger" },
            };
            confirmBtn.Click += (_, _) => dialog.Close(true);

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(confirmBtn);
            stack.Children.Add(btnPanel);
            dialog.Content = stack;

            var result = await dialog.ShowDialog<bool>(window);
            if (!result) return;
        }

        try
        {
            _config.ResetAll();

            App.SetTheme(true);
            await _localization.SetLanguageAsync("tr");

            _main.NavigateTo(PageType.PlayerSetup);
        }
        catch (Exception ex)
        {
            _config.Log($"Sıfırlama hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        IsUpdateAvailable = false;
        UpdateStatus = _localization["settings.update.checking"];

        var info = await _updater.CheckForUpdatesAsync();
        if (info == null)
        {
            UpdateStatus = _localization["settings.update.nonew"];
            IsCheckingUpdate = false;
            return;
        }

        IsUpdateAvailable = true;
        UpdateStatus = string.Format(_localization["settings.update.available"], info.Version);
        IsCheckingUpdate = false;

        UpdateStatus = _localization["settings.update.downloading"];
        var path = await _updater.DownloadUpdateAsync(info);
        if (path == null)
        {
            UpdateStatus = _localization["settings.update.error"];
            return;
        }

        UpdateStatus = _localization["settings.update.downloaded"];
        _updater.ScheduleRestart(path);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Close();
    }

    [RelayCommand]
    private async Task BrowseJava()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Java yürütülebilir dosyasını seçin",
                AllowMultiple = false,
            });
            if (files.Count > 0)
                JavaPath = files[0].Path.LocalPath;
        }
    }
}
