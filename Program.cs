using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using LauncherRoot.Services;

namespace LauncherRoot;

public static class Program
{
    public static string? LaunchInstanceId;
    public static string? LaunchPlayerName;

    public static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--launch" && i + 1 < args.Length)
                LaunchInstanceId = args[++i];
            if (args[i] == "--player" && i + 1 < args.Length)
                LaunchPlayerName = args[++i];
            if (args[i] == "--updated")
            {
                Console.WriteLine("Update applied successfully.");
            }

            if (args[i] == "--help" || args[i] == "-h")
            {
                Console.WriteLine("Usage: LauncherRoot [--launch <instanceId>] [--player <name>] [--updated] [--install]");
                return;
            }

            if (args[i] == "--install")
            {
                new InstallService().InstallAsync(force: true).GetAwaiter().GetResult();
                Console.WriteLine("Desktop entry created.");
                return;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
