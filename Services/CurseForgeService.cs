using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class CurseForgeService : ICurseForgeService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.curseforge.com/v1";
    private const int GameId = 432;
    private readonly ConfigService _config;
    private string? _cachedKey;
    private bool _keyLoaded;

    public CurseForgeService(ConfigService config)
    {
        _config = config;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
    }

    private async Task<string?> GetKeyAsync()
    {
        if (!_keyLoaded)
        {
            try
            {
                var cfg = await _config.LoadConfigAsync().ConfigureAwait(false);
                var saved = cfg.CurseForgeApiKey;
                if (!string.IsNullOrWhiteSpace(saved) && saved != CurseForgeKeys.DefaultApiKey)
                    _cachedKey = saved;
                else
                    _cachedKey = CurseForgeKeys.DefaultApiKey;
            }
            catch { _cachedKey = CurseForgeKeys.DefaultApiKey; }
            _keyLoaded = true;
        }
        return _cachedKey;
    }

    public async Task<List<CurseForgeMod>> SearchModsAsync(string query, string gameVersion = "", string loader = "", int classId = 0, int limit = 20)
    {
        var key = await GetKeyAsync();
        if (key == null) return [];

        try
        {
            var url = $"{BaseUrl}/mods/search?gameId={GameId}&searchFilter={Uri.EscapeDataString(query)}&pageSize={limit}";
            if (classId > 0) url += $"&classId={classId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", key);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _config.Log($"CurseForge arama hatası: {response.StatusCode}");
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<CurseForgeSearchResult>();
            var list = result?.Data ?? [];

            var q = query.ToLowerInvariant();
            return [.. list.OrderByDescending(m =>
                m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 2 :
                m.Slug.Contains(q, StringComparison.OrdinalIgnoreCase) ? 1 : 0)];
        }
        catch (Exception ex)
        {
            _config.Log($"CurseForge arama hatası: {ex.Message}");
            return [];
        }
    }

    public async Task<CurseForgeFile?> GetBestFileAsync(int modId, string gameVersion, string loader)
    {
        var key = await GetKeyAsync();
        if (key == null) return null;

        try
        {
            var url = $"{BaseUrl}/mods/{modId}/files";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", key);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<CurseForgeFilesResult>();
            if (result?.Data == null || result.Data.Count == 0) return null;

            var loaderName = loader switch
            {
                "forge" => "Forge",
                "fabric" => "Fabric",
                "quilt" => "Quilt",
                "neoforge" => "NeoForge",
                _ => "",
            };

            return result.Data
                .Where(f => f.GameVersions.Contains(gameVersion))
                .Where(f => string.IsNullOrEmpty(loaderName) || f.GameVersions.Contains(loaderName))
                .OrderByDescending(f => f.ReleaseType == 1)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _config.Log($"CurseForge dosya hatası: {ex.Message}");
            return null;
        }
    }
}

public class CurseForgeFilesResult
{
    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public List<CurseForgeFile> Data { get; set; } = [];
}
