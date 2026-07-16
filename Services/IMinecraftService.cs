using System;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface IMinecraftService
{
    IProgress<double>? Progress { get; set; }
    string? LastError { get; }
    Task<bool> EnsureInstanceReadyAsync(Instance instance, string assetsDir);
    Task<bool> LaunchInstanceAsync(Instance instance, string username, int ramGB, string assetsDir, string accessToken = "0", string uuid = "", string? serverAddress = null);
}
