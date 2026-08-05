using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class ConfigService : IConfigService
{
    public string RootPath { get; }

    private LauncherConfig? _cachedConfig;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _saveDebounceCts;

    public ConfigService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".LauncherRoot");
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(GetModsPath());
        Directory.CreateDirectory(GetMinecraftPath());
        Directory.CreateDirectory(GetLogsPath());
    }

    public string GetModsPath() => Path.Combine(RootPath, "mods");
    public string GetMinecraftPath() => Path.Combine(RootPath, "minecraft");
    public string GetLogsPath() => Path.Combine(RootPath, "logs");
    public string GetAssetsPath() => Path.Combine(GetMinecraftPath(), "assets");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public async Task<PlayerConfig> LoadPlayerAsync()
    {
        var path = Path.Combine(RootPath, "player.json");
        if (File.Exists(path))
        {
            var old = await TryReadJsonAsync<PlayerConfig>(path);
            if (old != null && !string.IsNullOrEmpty(old.Username))
            {
                var cfg = await LoadConfigAsync();
                if (cfg.Accounts.Count == 0)
                {
                    old.Id = "active";
                    cfg.Accounts.Add(old);
                    cfg.ActiveAccountId = "active";
                    await SaveConfigAsync(cfg);
                }
                File.Delete(path);
                return old;
            }
        }

        var config = await LoadConfigAsync();
        var active = config.Accounts.Find(a => a.Id == config.ActiveAccountId);
        return active ?? config.Accounts.FirstOrDefault() ?? new PlayerConfig();
    }

    public async Task SavePlayerAsync(PlayerConfig player)
    {
        var cfg = await LoadConfigAsync();
        var idx = cfg.Accounts.FindIndex(a => a.Id == player.Id);
        if (idx >= 0)
            cfg.Accounts[idx] = player;
        else
            cfg.Accounts.Add(player);
        cfg.ActiveAccountId = player.Id;
        await SaveConfigAsync(cfg);
    }

    public async Task DeleteAccountAsync(string accountId)
    {
        var cfg = await LoadConfigAsync();
        cfg.Accounts.RemoveAll(a => a.Id == accountId);
        if (cfg.ActiveAccountId == accountId)
            cfg.ActiveAccountId = cfg.Accounts.FirstOrDefault()?.Id ?? "";
        await SaveConfigAsync(cfg);
    }

    public async Task<List<PlayerConfig>> LoadAccountsAsync()
    {
        var cfg = await LoadConfigAsync();
        return cfg.Accounts;
    }

    public async Task SwitchAccountAsync(string accountId)
    {
        var cfg = await LoadConfigAsync();
        cfg.ActiveAccountId = accountId;
        await SaveConfigAsync(cfg);
    }

    public async Task<LauncherConfig> LoadConfigAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            var path = Path.Combine(RootPath, "config.json");
            _cachedConfig = await TryReadJsonAsync<LauncherConfig>(path) ?? new LauncherConfig();
            return _cachedConfig;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public Task SaveConfigAsync(LauncherConfig config)
    {
        _cachedConfig = config;
        return DebouncedSaveConfigAsync(config);
    }

    public async Task SaveConfigNowAsync(LauncherConfig config)
    {
        _cachedConfig = config;
        _saveDebounceCts?.Cancel();
        await WriteJsonAtomicAsync(Path.Combine(RootPath, "config.json"), config);
    }

    private async Task DebouncedSaveConfigAsync(LauncherConfig config)
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            await WriteJsonAtomicAsync(Path.Combine(RootPath, "config.json"), config);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Log($"Config kaydedilemedi: {ex.Message}");
        }
    }

    public async Task<ModState> LoadModStateAsync()
    {
        var path = Path.Combine(RootPath, "modstate.json");
        return await TryReadJsonAsync<ModState>(path) ?? new ModState();
    }

    public async Task SaveModStateAsync(ModState state)
    {
        await WriteJsonAtomicAsync(Path.Combine(RootPath, "modstate.json"), state);
    }

    private async Task<T?> TryReadJsonAsync<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;

            await using var fs = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(fs, JsonOpts);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log($"JSON okuma hatası ({path}): {ex.Message}");
            return null;
        }
    }

    private async Task WriteJsonAtomicAsync<T>(string path, T value)
    {
        await _writeLock.WaitAsync();
        try
        {
            var tmp = path + ".tmp";
            await using (var fs = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(fs, value, JsonOpts);
            }
            File.Move(tmp, path, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Log(string message)
    {
        try
        {
            var logPath = Path.Combine(GetLogsPath(), $"launcher-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void ResetAll()
    {
        _cachedConfig = null;
        _saveDebounceCts?.Cancel();

        _writeLock.Wait();
        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, true);
        }
        catch (Exception ex)
        {
            Log($"Sıfırlama sırasında hata: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
        }

        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(GetModsPath());
        Directory.CreateDirectory(GetMinecraftPath());
        Directory.CreateDirectory(GetLogsPath());
        Directory.CreateDirectory(Path.Combine(RootPath, "instances"));
    }
}
