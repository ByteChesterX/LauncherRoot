using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LauncherRoot.Services;

namespace LauncherRoot.ViewModels;

public partial class SplashViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private double _opacity;

    public ILocalizationService Localization => _localization;

    public SplashViewModel(MainWindowViewModel main, ILocalizationService localization)
    {
        _main = main;
        _localization = localization;
        _ = AnimateAsync();
    }

    private async Task AnimateAsync()
    {
        for (double i = 0; i <= 1; i += 0.05)
        {
            Opacity = i;
            await Task.Delay(20);
        }
        Opacity = 1;

        await Task.Delay(3000);

        for (double i = 1; i >= 0; i -= 0.05)
        {
            Opacity = i;
            await Task.Delay(20);
        }
        Opacity = 0;

        _main.NavigateTo(PageType.MainMenu);
    }
}
