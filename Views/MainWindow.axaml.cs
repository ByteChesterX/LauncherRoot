using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;
using LauncherRoot.Services;
using LauncherRoot.ViewModels;

namespace LauncherRoot.Views;

public partial class MainWindow : Window
{
    private object? _previousContent;
    private bool _isFading;

    public MainWindow()
    {
        InitializeComponent();

        var config = new ConfigService();
        var instances = new InstanceService();
        var modrinth = new ModrinthService(config);
        var minecraft = new MinecraftService(config, instances);
        var theme = new ThemeService(config);
        var localization = new LocalizationService();
        var updater = new UpdateService(config);
        var installer = new InstallService();
        var curseforge = new CurseForgeService(config);
        var backups = new BackupService(config, instances);

        _ = ApplyStartupConfig(config, localization);

        var vm = new MainWindowViewModel(config, modrinth, minecraft, theme, localization, instances, updater, installer, curseforge, backups);
        DataContext = vm;

        vm.Navigating += OnNavigating;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnNavigating()
    {
        _previousContent = NewPageHost.Content;
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentViewModel)) return;
        if (_isFading) return;
        _isFading = true;

        try
        {
            OldPageHost.Content = _previousContent;
            OldPageHost.Opacity = 1;
            NewPageHost.Opacity = 0;

            await System.Threading.Tasks.Task.Delay(30);

            await Task.WhenAll(
                Fade(OldPageHost, 1, 0, 200),
                Fade(NewPageHost, 0, 1, 200)
            );

            OldPageHost.Content = null;
        }
        catch
        {
            OldPageHost.Content = null;
            OldPageHost.Opacity = 0;
            NewPageHost.Opacity = 1;
        }
        finally
        {
            _isFading = false;
        }
    }

    private static async Task Fade(Control target, double from, double to, int durationMs)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.Zero,
                    Setters = { new Setter { Property = Visual.OpacityProperty, Value = from } }
                },
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(durationMs),
                    Setters = { new Setter { Property = Visual.OpacityProperty, Value = to } }
                }
            }
        };

        await animation.RunAsync(target);
        target.Opacity = to;
    }

    private static async Task ApplyStartupConfig(ConfigService config, LocalizationService localization)
    {
        var cfg = await config.LoadConfigAsync();

        App.SetTheme(cfg.DarkTheme);

        await localization.SetLanguageAsync(cfg.Language ?? "tr");
    }
}
