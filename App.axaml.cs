using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using LauncherRoot.Views;

namespace LauncherRoot;

public partial class App : Application
{
    public static void SetTheme(bool isDark)
    {
        if (Current is App app)
        {
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

            var resources = app.Resources;
            if (isDark)
            {
                resources["BgPrimaryBrush"] = new SolidColorBrush(Color.Parse("#05080A"));
                resources["BgSecondaryBrush"] = new SolidColorBrush(Color.Parse("#0A0E14"));
                resources["BgCardBrush"] = new SolidColorBrush(Color.Parse("#0D1219"));
                resources["BgHoverBrush"] = new SolidColorBrush(Color.Parse("#131B26"));
                resources["BgSidebarBrush"] = new SolidColorBrush(Color.Parse("#030508"));
                resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#3B82F6"));
                resources["AccentHoverBrush"] = new SolidColorBrush(Color.Parse("#60A5FA"));
                resources["SuccessBrush"] = new SolidColorBrush(Color.Parse("#166534"));
                resources["WarningBrush"] = new SolidColorBrush(Color.Parse("#A16207"));
                resources["ErrorBrush"] = new SolidColorBrush(Color.Parse("#B91C1C"));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.Parse("#E2E8F0"));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.Parse("#7E8EA0"));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.Parse("#3B4A5C"));
                resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#162032"));
            }
            else
            {
                resources["BgPrimaryBrush"] = new SolidColorBrush(Color.Parse("#F8FAFC"));
                resources["BgSecondaryBrush"] = new SolidColorBrush(Color.Parse("#F1F5F9"));
                resources["BgCardBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                resources["BgHoverBrush"] = new SolidColorBrush(Color.Parse("#E2E8F0"));
                resources["BgSidebarBrush"] = new SolidColorBrush(Color.Parse("#F8FAFC"));
                resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#2563EB"));
                resources["AccentHoverBrush"] = new SolidColorBrush(Color.Parse("#1D4ED8"));
                resources["SuccessBrush"] = new SolidColorBrush(Color.Parse("#16A34A"));
                resources["WarningBrush"] = new SolidColorBrush(Color.Parse("#D97706"));
                resources["ErrorBrush"] = new SolidColorBrush(Color.Parse("#DC2626"));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.Parse("#0F172A"));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.Parse("#475569"));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.Parse("#94A3B8"));
                resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#CBD5E1"));
            }
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
