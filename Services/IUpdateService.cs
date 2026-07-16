using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Changelog { get; set; } = "";
    public long Size { get; set; }
}

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task<string?> DownloadUpdateAsync(UpdateInfo info);
    void ScheduleRestart(string newFilePath);
}
