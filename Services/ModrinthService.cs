using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class ModrinthService : IModrinthService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.modrinth.com/v2";
    private readonly ConfigService _config;

    public IProgress<double>? Progress { get; set; }

    public ModrinthService(ConfigService config)
    {
        _config = config;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
    }

    public async Task<List<ModInfo>> DownloadThemeModsAsync(
        List<ThemeMod> mods, string modsPath, string gameVersion, string loader)
    {
        var downloaded = new List<ModInfo>();
        var total = mods.Count;
        var completed = 0;

        Directory.CreateDirectory(modsPath);

        var throttle = new SemaphoreSlim(3);
        var tasks = mods.Select(async themeMod =>
        {
            await throttle.WaitAsync();
            try
            {
                var modInfo = await GetModInfoAsync(themeMod.Slug, gameVersion, loader);
                if (modInfo?.DownloadUrl == null || modInfo.FileName == null)
                {
                    _config.Log($"Mod {themeMod.Slug}: {gameVersion}/{loader} sürümü bulunamadı");
                    return (ModInfo?)null;
                }

                var filePath = Path.Combine(modsPath, modInfo.FileName);
                if (!File.Exists(filePath))
                {
                    var response = await _http.GetAsync(modInfo.DownloadUrl);
                    response.EnsureSuccessStatusCode();
                    await using var fs = File.Create(filePath);
                    await response.Content.CopyToAsync(fs);
                }

                _config.Log($"Mod indirildi: {themeMod.Slug} -> {modInfo.FileName}");

                return new ModInfo
                {
                    Slug = themeMod.Slug,
                    Name = themeMod.Name,
                    FileName = modInfo.FileName,
                    Downloaded = true,
                    Enabled = true
                };
            }
            catch (HttpRequestException ex)
            {
                _config.Log($"Mod indirme hatası ({themeMod.Slug}): {ex.Message}");
                return (ModInfo?)null;
            }
            catch (Exception ex)
            {
                _config.Log($"Beklenmeyen hata ({themeMod.Slug}): {ex.Message}");
                return (ModInfo?)null;
            }
            finally
            {
                throttle.Release();
                var c = Interlocked.Increment(ref completed);
                Progress?.Report((double)c / total);
            }
        });

        var results = await Task.WhenAll(tasks);
        downloaded.AddRange(results.Where(r => r != null)!);
        return downloaded;
    }

    public async Task<List<ModrinthHit>> SearchModsAsync(string query, string loader = "fabric", string projectType = "mod", int limit = 20)
    {
        try
        {
            var facetParts = new List<string> { $"project_type:{projectType}" };

            if (projectType == "mod")
            {
                var loaderFacet = loader switch
                {
                    "forge" => "forge",
                    "neoforge" => "neoforge",
                    "fabric" => "fabric",
                    "quilt" => "quilt",
                    _ => "minecraft",
                };
                facetParts.Add($"categories:{loaderFacet}");
            }

            var facetsJson = System.Text.Json.JsonSerializer.Serialize(new[] { facetParts.ToArray() });
            var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}" +
                      $"&facets={Uri.EscapeDataString(facetsJson)}&limit={limit}";
            var result = await _http.GetFromJsonAsync<ModrinthSearchResult>(url);
            return result?.Hits ?? [];
        }
        catch (Exception ex)
        {
            _config.Log($"Mod arama hatası: {ex.Message}");
            return [];
        }
    }

    public async Task<ModInfo?> GetModInfoAsync(string slug, string gameVersion, string loader, string projectType = "mod")
    {
        try
        {
            var project = await _http.GetFromJsonAsync<ModrinthProject>($"{BaseUrl}/project/{slug}");
            if (project == null) return null;

            var versions = await _http.GetFromJsonAsync<List<ModrinthVersion>>($"{BaseUrl}/project/{slug}/version");
            var best = PickBestVersion(versions, gameVersion, loader, projectType);
            var primaryFile = best?.Files.FirstOrDefault(f => f.Primary) ?? best?.Files.FirstOrDefault();

            return new ModInfo
            {
                Slug = project.Slug,
                Name = project.Title,
                Description = project.Description,
                IconUrl = project.IconUrl,
                DownloadUrl = primaryFile?.Url,
                FileName = primaryFile?.Filename,
                Downloaded = false,
                Enabled = true
            };
        }
        catch (Exception ex)
        {
            _config.Log($"Mod bilgisi alınamadı ({slug}): {ex.Message}");
            return null;
        }
    }

    private static ModrinthVersion? PickBestVersion(List<ModrinthVersion>? versions, string gameVersion, string loader, string projectType = "mod")
    {
        if (versions == null || versions.Count == 0) return null;

        if (projectType != "mod")
        {
            return versions
                .Where(v => v.GameVersions.Contains(gameVersion))
                .OrderByDescending(v => v.DatePublished)
                .FirstOrDefault();
        }

        var loaderName = loader switch
        {
            "forge" => "forge",
            "neoforge" => "neoforge",
            "fabric" => "fabric",
            "quilt" => "quilt",
            _ => "fabric",
        };

        return versions
            .Where(v => v.GameVersions.Contains(gameVersion) && v.Loaders.Contains(loaderName))
            .OrderByDescending(v => v.DatePublished)
            .FirstOrDefault()
            ?? versions
                .Where(v => v.GameVersions.Contains(gameVersion))
                .OrderByDescending(v => v.DatePublished)
                .FirstOrDefault();
    }

    public async Task<ModInfo?> CheckUpdateAsync(string slug, string gameVersion, string loader, string currentFile)
    {
        try
        {
            var project = await _http.GetFromJsonAsync<ModrinthProject>($"{BaseUrl}/project/{slug}");
            if (project == null) return null;

            var versions = await _http.GetFromJsonAsync<List<ModrinthVersion>>($"{BaseUrl}/project/{slug}/version");
            if (versions == null || versions.Count == 0) return null;

            var best = PickBestVersion(versions, gameVersion, loader);
            if (best == null) return null;

            var primaryFile = best.Files.FirstOrDefault(f => f.Primary) ?? best.Files.FirstOrDefault();
            if (primaryFile == null || primaryFile.Filename == currentFile) return null;

            return new ModInfo
            {
                Slug = project.Slug,
                Name = project.Title,
                DownloadUrl = primaryFile.Url,
                FileName = primaryFile.Filename,
                LatestVersion = best.VersionNumber,
                HasUpdate = true,
            };
        }
        catch
        {
            return null;
        }
    }
}
