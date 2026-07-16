using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LauncherRoot.Models;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class ThemeSelectionViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly IModrinthService _modrinth;
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;
    private readonly IInstanceService _instances;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private ObservableCollection<Theme> _themes = [];

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _progressText = "";

    public ILocalizationService Localization => _localization;

    public ThemeSelectionViewModel(ConfigService config, IModrinthService modrinth, MainWindowViewModel main, ILocalizationService localization, IInstanceService instances, IThemeService themeService)
    {
        _config = config;
        _modrinth = modrinth;
        _main = main;
        _localization = localization;
        _instances = instances;
        _themeService = themeService;
        LoadThemes();
    }

    private void LoadThemes()
    {
        Theme.CurrentLanguage = _localization.CurrentLanguage;
        Themes = new ObservableCollection<Theme>(_themeService.GetThemes());
    }

    [RelayCommand]
    private async Task SelectTheme(Theme? theme)
    {
        if (theme == null || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;

        var launcherConfig = await _config.LoadConfigAsync();
        launcherConfig.SelectedTheme = theme.Id;
        await _config.SaveConfigAsync(launcherConfig);

        var modsPath = _config.GetModsPath();

        var progress = new Progress<double>(p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                DownloadProgress = p;
                ProgressText = $"%{p * 100:F0}";
            });
        });

        _modrinth.Progress = progress;

        var version = "1.21.4";
        var loader = "fabric";
        if (!string.IsNullOrEmpty(launcherConfig.SelectedInstanceId))
        {
            var instance = await _instances.GetInstanceAsync(launcherConfig.SelectedInstanceId);
            if (instance != null)
            {
                version = instance.Version;
                loader = instance.Loader;
            }
        }

        try
        {
            await _modrinth.DownloadThemeModsAsync(theme.Mods, modsPath, version, loader);
            _config.Log($"Tema indirildi: {theme.Id}");
        }
        catch (Exception ex)
        {
            _config.Log($"Tema indirme hatası: {ex.Message}");
        }

        IsDownloading = false;
        _main.NavigateTo(PageType.Splash);
    }
}
