using System.Threading.Tasks;

namespace LauncherRoot.Services;

public interface IInstallService
{
    bool IsInstalled { get; }
    Task InstallAsync(bool force = false);
}
