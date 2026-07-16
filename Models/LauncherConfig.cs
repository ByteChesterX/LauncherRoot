using System.Collections.Generic;

namespace LauncherRoot.Models;

public class LauncherConfig
{
    public int RamGB { get; set; } = 4;
    public int FpsLimit { get; set; } = 60;
    public bool DarkTheme { get; set; } = true;
    public string Language { get; set; } = "tr";
    public string? SelectedTheme { get; set; }
    public string? SelectedInstanceId { get; set; }
    public string JvmArgs { get; set; } = "--add-opens java.base/java.lang.invoke=ALL-UNNAMED --add-opens java.base/java.lang=ALL-UNNAMED --add-opens java.base/java.util=ALL-UNNAMED --add-opens java.base/java.lang.reflect=ALL-UNNAMED --add-opens java.base/java.text=ALL-UNNAMED --add-opens java.base/java.io=ALL-UNNAMED --add-opens java.base/java.net=ALL-UNNAMED --add-opens java.base/java.nio=ALL-UNNAMED --add-opens java.base/sun.security.ssl=ALL-UNNAMED --add-opens java.base/sun.security.util=ALL-UNNAMED -XX:+UseG1GC -XX:+UnlockExperimentalVMOptions -XX:MaxGCPauseMillis=50 -XX:+DisableExplicitGC";
    public string PreLaunchCommand { get; set; } = "";
    public string WrapperCommand { get; set; } = "";
    public string PostExitCommand { get; set; } = "";
    public string JavaPath { get; set; } = "";
    public int WindowWidth { get; set; } = 854;
    public int WindowHeight { get; set; } = 480;
    public List<PlayerConfig> Accounts { get; set; } = [];
    public string ActiveAccountId { get; set; } = "";
    public string MicrosoftClientId { get; set; } = "";
    public string ElybyClientId { get; set; } = "";
    public string ElybyClientSecret { get; set; } = "";
    public List<string> Groups { get; set; } = [];
    public List<ServerInfo> Servers { get; set; } = [];
    public string CurseForgeApiKey { get; set; } = "";
}
