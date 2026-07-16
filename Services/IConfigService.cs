using System.Collections.Generic;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface IConfigService
{
    string RootPath { get; }
    Task<PlayerConfig> LoadPlayerAsync();
    Task SavePlayerAsync(PlayerConfig config);
    Task DeleteAccountAsync(string accountId);
    Task<List<PlayerConfig>> LoadAccountsAsync();
    Task SwitchAccountAsync(string accountId);
    Task<LauncherConfig> LoadConfigAsync();
    Task SaveConfigAsync(LauncherConfig config);
    Task<ModState> LoadModStateAsync();
    Task SaveModStateAsync(ModState state);
    string GetModsPath();
    string GetMinecraftPath();
    string GetLogsPath();
    string GetAssetsPath();
    void Log(string message);
    void ResetAll();
}
