using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class MinecraftService : IMinecraftService, IDisposable
{
    private readonly HttpClient _http;
    private readonly ConfigService _config;
    private readonly IInstanceService _instances;

    public IProgress<double>? Progress { get; set; }
    public string? LastError { get; private set; }

    public MinecraftService(ConfigService config, IInstanceService instances)
    {
        _config = config;
        _instances = instances;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<bool> EnsureInstanceReadyAsync(Instance instance, string assetsDir)
    {
        var minecraftDir = _instances.GetInstanceMinecraftPath(instance);

        LastError = null;
        Directory.CreateDirectory(minecraftDir);

        try
        {
            var ok = instance.Loader switch
            {
                "fabric" => await EnsureFabricReadyAsync(instance, minecraftDir, assetsDir),
                "forge" => await EnsureForgeReadyAsync(instance, minecraftDir, assetsDir),
                "neoforge" => await EnsureNeoforgeReadyAsync(instance, minecraftDir, assetsDir),
                "quilt" => await EnsureQuiltReadyAsync(instance, minecraftDir, assetsDir),
                _ => await EnsureVanillaReadyAsync(instance.Version, minecraftDir, assetsDir),
            };
            if (ok && !string.IsNullOrEmpty(instance.LoaderVersion))
                await _instances.UpdateInstanceAsync(instance);
            if (!ok && LastError == null)
                LastError = $"Instance hazırlanamadı ({instance.Loader})";
            return ok;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _config.Log($"Instance hazırlık hatası ({instance.Id}): {ex.Message}");
            return false;
        }
    }

    private async Task<bool> EnsureVanillaReadyAsync(string version, string minecraftDir, string assetsDir)
    {
        Progress?.Report(0.1);

        var versionData = await GetVersionDataAsync(version);
        if (versionData == null) return false;
        var vd = versionData.Value;

        var clientUrl = vd.GetProperty("downloads").GetProperty("client").GetProperty("url").GetString()!;

        var clientDir = Path.Combine(minecraftDir, "versions", version);
        Directory.CreateDirectory(clientDir);
        var clientJar = Path.Combine(clientDir, $"{version}.jar");
        if (!File.Exists(clientJar))
        {
            var response = await _http.GetAsync(clientUrl);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(clientJar);
            await response.Content.CopyToAsync(fs);
        }

        Progress?.Report(0.4);

        if (vd.TryGetProperty("libraries", out var libs))
            await DownloadLibrariesAsync(libs, minecraftDir);

        Progress?.Report(0.7);

        await DownloadAssetsForVersionAsync(vd, assetsDir);

        Progress?.Report(1.0);
        return true;
    }

    private async Task<bool> EnsureFabricReadyAsync(Instance instance, string minecraftDir, string assetsDir)
    {
        Progress?.Report(0.1);

        var version = instance.Version;
        var metaUrl = $"https://meta.fabricmc.net/v2/versions/loader/{version}";
        var loaders = await _http.GetFromJsonAsync<List<JsonElement>>(metaUrl);
        if (loaders == null || loaders.Count == 0)
        {
            LastError = $"Fabric meta alınamadı: {version}";
            _config.Log(LastError);
            return false;
        }

        var loader = loaders[0];
        var loaderVersion = loader.GetProperty("loader").GetProperty("version").GetString() ?? "0.16.9";
        instance.LoaderVersion = loaderVersion;

        var profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{version}/{loaderVersion}/profile/json";
        var profileJson = await _http.GetStringAsync(profileUrl);

        var versionDir = Path.Combine(minecraftDir, "versions", $"fabric-loader-{version}-{loaderVersion}");
        Directory.CreateDirectory(versionDir);

        var profilePath = Path.Combine(versionDir, $"fabric-loader-{version}-{loaderVersion}.json");
        await File.WriteAllTextAsync(profilePath, profileJson);

        Progress?.Report(0.3);

        var versionData = await GetVersionDataAsync(version);
        if (versionData == null) return false;
        var vd = versionData.Value;

        var clientUrl = vd.GetProperty("downloads").GetProperty("client").GetProperty("url").GetString()!;
        var clientDir = Path.Combine(minecraftDir, "versions", version);
        Directory.CreateDirectory(clientDir);
        var clientJar = Path.Combine(clientDir, $"{version}.jar");
        if (!File.Exists(clientJar))
        {
            var response = await _http.GetAsync(clientUrl);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(clientJar);
            await response.Content.CopyToAsync(fs);
        }

        Progress?.Report(0.5);

        if (vd.TryGetProperty("libraries", out var mcLibs))
            await DownloadLibrariesAsync(mcLibs, minecraftDir);

        Progress?.Report(0.7);

        var profile = JsonSerializer.Deserialize<JsonElement>(profileJson);
        if (profile.TryGetProperty("libraries", out var fabricLibs))
            await DownloadFabricLibrariesAsync(fabricLibs, minecraftDir);

        Progress?.Report(0.8);

        await DownloadAssetsForVersionAsync(vd, assetsDir);

        Progress?.Report(1.0);
        return true;
    }

    private async Task<bool> EnsureQuiltReadyAsync(Instance instance, string minecraftDir, string assetsDir)
    {
        Progress?.Report(0.1);

        var version = instance.Version;
        var metaUrl = $"https://meta.quiltmc.org/v3/versions/loader/{version}";
        var loaders = await _http.GetFromJsonAsync<List<JsonElement>>(metaUrl);
        if (loaders == null || loaders.Count == 0)
        {
            LastError = $"Quilt meta alınamadı: {version}";
            _config.Log(LastError);
            return false;
        }

        var loader = loaders[0];
        var loaderVersion = loader.GetProperty("loader").GetProperty("version").GetString() ?? "0.27.1";
        instance.LoaderVersion = loaderVersion;

        var profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{version}/{loaderVersion}/profile/json";
        var profileJson = await _http.GetStringAsync(profileUrl);

        var versionDir = Path.Combine(minecraftDir, "versions", $"quilt-loader-{version}-{loaderVersion}");
        Directory.CreateDirectory(versionDir);

        var profilePath = Path.Combine(versionDir, $"quilt-loader-{version}-{loaderVersion}.json");
        await File.WriteAllTextAsync(profilePath, profileJson);

        Progress?.Report(0.3);

        var versionData = await GetVersionDataAsync(version);
        if (versionData == null) return false;
        var vd = versionData.Value;

        var clientUrl = vd.GetProperty("downloads").GetProperty("client").GetProperty("url").GetString()!;
        var clientDir = Path.Combine(minecraftDir, "versions", version);
        Directory.CreateDirectory(clientDir);
        var clientJar = Path.Combine(clientDir, $"{version}.jar");
        if (!File.Exists(clientJar))
        {
            var response = await _http.GetAsync(clientUrl);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(clientJar);
            await response.Content.CopyToAsync(fs);
        }

        Progress?.Report(0.5);

        if (vd.TryGetProperty("libraries", out var mcLibs))
            await DownloadLibrariesAsync(mcLibs, minecraftDir);

        Progress?.Report(0.7);

        var profile = JsonSerializer.Deserialize<JsonElement>(profileJson);
        if (profile.TryGetProperty("libraries", out var quiltLibs))
            await DownloadQuiltLibrariesAsync(quiltLibs, minecraftDir);

        Progress?.Report(0.8);

        await DownloadAssetsForVersionAsync(vd, assetsDir);

        Progress?.Report(1.0);
        return true;
    }

    private async Task<bool> EnsureForgeReadyAsync(Instance instance, string minecraftDir, string assetsDir)
    {
        Progress?.Report(0.1);

        var version = instance.Version;
        var forgeVersion = await GetLatestForgeVersionAsync(version);
        if (forgeVersion == null)
        {
            LastError = $"Forge sürümü bulunamadı: {version}";
            _config.Log(LastError);
            return false;
        }

        instance.LoaderVersion = forgeVersion;

        var versionId = $"{version}-{forgeVersion}";
        var versionJsonPath = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.json");
        if (File.Exists(versionJsonPath))
        {
            Progress?.Report(0.8);
            var versionData = await GetVersionDataAsync(version);
            if (versionData != null)
                await DownloadAssetsForVersionAsync(versionData.Value, assetsDir);
            Progress?.Report(1.0);
            return true;
        }

        var installerJar = $"forge-{version}-{forgeVersion}-installer.jar";
        var installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}-{forgeVersion}/{installerJar}";
        var installerPath = Path.Combine(minecraftDir, installerJar);

        if (!File.Exists(installerPath))
        {
            var resp = await _http.GetAsync(installerUrl);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"Forge installer indirilemedi: {version}-{forgeVersion}";
                _config.Log(LastError);
                return false;
            }
            await using var fs = File.Create(installerPath);
            await resp.Content.CopyToAsync(fs);
        }

        Progress?.Report(0.3);

        var javaPath = FindJava();
        if (javaPath == null)
        {
            LastError = "Java bulunamadı";
            return false;
        }

        var launcherProfiles = Path.Combine(minecraftDir, "launcher_profiles.json");
        if (!File.Exists(launcherProfiles))
            await File.WriteAllTextAsync(launcherProfiles,
                "{\"profiles\":{},\"selectedProfile\":\"(Default)\",\"clientToken\":\"00000000-0000-0000-0000-000000000000\",\"launcherVersion\":{\"format\":21,\"name\":\"2.1.1234\"}}");

        var psi = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"--add-modules ALL-MODULE-PATH -jar \"{installerPath}\" --installClient \"{minecraftDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = minecraftDir,
        };

        var proc = Process.Start(psi);
        if (proc == null)
        {
            LastError = "Forge installer başlatılamadı";
            _config.Log(LastError);
            return false;
        }

        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            LastError = string.IsNullOrWhiteSpace(error) ? output[..Math.Min(output.Length, 500)] : error[..Math.Min(error.Length, 500)];
            _config.Log($"Forge kurulum hatası: {LastError}");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(output))
            _config.Log($"Forge installer: {output[..Math.Min(output.Length, 300)]}");

        Progress?.Report(0.7);

        var versionData2 = await GetVersionDataAsync(version);
        if (versionData2 != null)
        {
            if (versionData2.Value.TryGetProperty("libraries", out var forgeLibs))
                await DownloadLibrariesAsync(forgeLibs, minecraftDir);

            await DownloadAssetsForVersionAsync(versionData2.Value, assetsDir);
        }

        Progress?.Report(1.0);
        return true;
    }

    private async Task<string?> GetLatestForgeVersionAsync(string mcVersion)
    {
        try
        {
            var metadata = await _http.GetFromJsonAsync<JsonElement>(
                "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.json");
            if (!metadata.TryGetProperty("versions", out var versions)) return null;

            var matching = versions.EnumerateArray()
                .Select(v => v.GetString() ?? "")
                .Where(v => v.StartsWith(mcVersion + "-", StringComparison.Ordinal))
                .Select(v => v[(mcVersion.Length + 1)..])
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();

            return matching.Count > 0 ? matching[^1] : null;
        }
        catch (Exception ex)
        {
            _config.Log($"Forge meta hatası: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> EnsureNeoforgeReadyAsync(Instance instance, string minecraftDir, string assetsDir)
    {
        Progress?.Report(0.1);

        var version = instance.Version;
        var neoVersion = await GetLatestNeoforgeVersionAsync(version);
        if (neoVersion == null)
        {
            LastError = $"NeoForge sürümü bulunamadı: {version}";
            _config.Log(LastError);
            return false;
        }

        instance.LoaderVersion = neoVersion;

        var versionId = neoVersion;
        var versionJsonPath = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.json");
        if (File.Exists(versionJsonPath))
        {
            Progress?.Report(0.8);
            var versionData = await GetVersionDataAsync(version);
            if (versionData != null)
                await DownloadAssetsForVersionAsync(versionData.Value, assetsDir);
            Progress?.Report(1.0);
            return true;
        }

        var installerJar = $"neoforge-{neoVersion}-installer.jar";
        var installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoVersion}/{installerJar}";
        var installerPath = Path.Combine(minecraftDir, installerJar);

        if (!File.Exists(installerPath))
        {
            var resp = await _http.GetAsync(installerUrl);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"NeoForge installer indirilemedi: {neoVersion}";
                _config.Log(LastError);
                return false;
            }
            await using var fs = File.Create(installerPath);
            await resp.Content.CopyToAsync(fs);
        }

        Progress?.Report(0.3);

        var javaPath = FindJava();
        if (javaPath == null)
        {
            LastError = "Java bulunamadı";
            return false;
        }

        var launcherProfiles = Path.Combine(minecraftDir, "launcher_profiles.json");
        if (!File.Exists(launcherProfiles))
            await File.WriteAllTextAsync(launcherProfiles,
                "{\"profiles\":{},\"selectedProfile\":\"(Default)\",\"clientToken\":\"00000000-0000-0000-0000-000000000000\",\"launcherVersion\":{\"format\":21,\"name\":\"2.1.1234\"}}");

        var psi = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"--add-modules ALL-MODULE-PATH -jar \"{installerPath}\" --installClient \"{minecraftDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = minecraftDir,
        };

        var proc = Process.Start(psi);
        if (proc == null)
        {
            LastError = "NeoForge installer başlatılamadı";
            _config.Log(LastError);
            return false;
        }

        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            LastError = string.IsNullOrWhiteSpace(error) ? output[..Math.Min(output.Length, 500)] : error[..Math.Min(error.Length, 500)];
            _config.Log($"NeoForge kurulum hatası: {LastError}");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(output))
            _config.Log($"NeoForge installer: {output[..Math.Min(output.Length, 300)]}");

        Progress?.Report(0.7);

        var versionData2 = await GetVersionDataAsync(version);
        if (versionData2 != null)
        {
            if (versionData2.Value.TryGetProperty("libraries", out var neoLibs))
                await DownloadLibrariesAsync(neoLibs, minecraftDir);

            await DownloadAssetsForVersionAsync(versionData2.Value, assetsDir);
        }

        Progress?.Report(1.0);
        return true;
    }

    private async Task<string?> GetLatestNeoforgeVersionAsync(string mcVersion)
    {
        try
        {
            var xml = await _http.GetStringAsync(
                "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml");

            var doc = XDocument.Parse(xml);
            var versions = doc.Root?.Element("versioning")?.Element("versions")?.Elements("version")
                .Select(v => v.Value)
                .ToList();

            if (versions == null || versions.Count == 0) return null;

            var parts = mcVersion.Split('.');
            if (parts.Length < 2) return null;

            var neoMajor = parts[1];
            var neoMinor = parts.Length >= 3 ? parts[2] : null;

            var prefix = neoMinor != null ? $"{neoMajor}.{neoMinor}." : $"{neoMajor}.";

            var matching = versions
                .Where(v => v.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();

            return matching.Count > 0 ? matching[^1] : null;
        }
        catch (Exception ex)
        {
            _config.Log($"NeoForge meta hatası: {ex.Message}");
            return null;
        }
    }

    private async Task DownloadQuiltLibrariesAsync(JsonElement libraries, string minecraftDir)
    {
        var libDir = Path.Combine(minecraftDir, "libraries");
        var throttle = new SemaphoreSlim(6);
        var tasks = libraries.EnumerateArray().Select(async lib =>
        {
            await throttle.WaitAsync();
            try
            {
                var name = lib.GetProperty("name").GetString()!;
                var parts = name.Split(':');
                if (parts.Length < 3) return;

                var groupId = parts[0].Replace('.', '/');
                var artifactId = parts[1];
                var version = parts[2];
                var classifier = parts.Length >= 4 ? parts[3] : null;
                var jarName = classifier != null
                    ? $"{artifactId}-{version}-{classifier}.jar"
                    : $"{artifactId}-{version}.jar";
                var libFilePath = Path.Combine(libDir, groupId, artifactId, version, jarName);

                if (File.Exists(libFilePath)) return;

                var baseUrl = lib.TryGetProperty("url", out var urlProp)
                    ? urlProp.GetString()!
                    : "https://maven.quiltmc.org/repository/release/";

                var downloadUrl = $"{baseUrl.TrimEnd('/')}/{groupId}/{artifactId}/{version}/{jarName}";

                var resp = await _http.GetAsync(downloadUrl);
                if (resp.IsSuccessStatusCode)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(libFilePath)!);
                    await using var fs = File.Create(libFilePath);
                    await resp.Content.CopyToAsync(fs);
                }
                else
                {
                    _config.Log($"Kütüphane indirilemedi: {downloadUrl}");
                }
            }
            catch (Exception ex)
            {
                _config.Log($"Quilt lib hatası: {ex.Message}");
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task<JsonElement?> GetVersionDataAsync(string version)
    {
        var manifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        var manifest = await _http.GetFromJsonAsync<JsonElement>(manifestUrl);

        foreach (var v in manifest.GetProperty("versions").EnumerateArray())
        {
            if (v.GetProperty("id").GetString() != version) continue;
            return await _http.GetFromJsonAsync<JsonElement>(v.GetProperty("url").GetString()!);
        }
        return null;
    }

    private async Task DownloadAssetsForVersionAsync(JsonElement versionData, string assetsDir)
    {
        var assetIndex = versionData.GetProperty("assetIndex");
        var assetIndexId = assetIndex.GetProperty("id").GetString()!;
        var assetIndexUrl = assetIndex.GetProperty("url").GetString()!;
        var indexDir = Path.Combine(assetsDir, "indexes");
        Directory.CreateDirectory(indexDir);
        var indexPath = Path.Combine(indexDir, $"{assetIndexId}.json");

        if (!File.Exists(indexPath))
        {
            var indexData = await _http.GetStringAsync(assetIndexUrl);
            await File.WriteAllTextAsync(indexPath, indexData);
        }

        await DownloadAssetsAsync(assetsDir, assetIndexId);
    }

    private async Task DownloadLibrariesAsync(JsonElement libraries, string minecraftDir)
    {
        var libDir = Path.Combine(minecraftDir, "libraries");
        var throttle = new SemaphoreSlim(6);
        var tasks = libraries.EnumerateArray()
            .Where(ShouldIncludeLibrary)
            .Select(async lib =>
            {
                await throttle.WaitAsync();
                try
                {
                    if (!lib.TryGetProperty("downloads", out var downloads) ||
                        !downloads.TryGetProperty("artifact", out var artifact)) return;

                    var libPath = artifact.GetProperty("path").GetString()!;
                    var libUrl = artifact.GetProperty("url").GetString()!;
                    var libFilePath = Path.Combine(libDir, libPath);

                    if (!File.Exists(libFilePath))
                    {
                        var resp = await _http.GetAsync(libUrl);
                        if (resp.IsSuccessStatusCode)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(libFilePath)!);
                            await using var fs = File.Create(libFilePath);
                            await resp.Content.CopyToAsync(fs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _config.Log($"Kütüphane indirme hatası: {ex.Message}");
                }
                finally
                {
                    throttle.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private static bool ShouldIncludeLibrary(JsonElement lib)
    {
        if (!lib.TryGetProperty("rules", out var rules))
            return true;

        var osName = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "osx"
            : "linux";

        var allowed = true;
        foreach (var rule in rules.EnumerateArray())
        {
            var action = rule.GetProperty("action").GetString();
            if (rule.TryGetProperty("os", out var osRule))
            {
                if (osRule.TryGetProperty("name", out var nameEl) && nameEl.GetString() == osName)
                {
                    allowed = action == "allow";
                    break;
                }
            }
            else
            {
                allowed = action == "allow";
                break;
            }
        }

        return allowed;
    }

    private async Task DownloadFabricLibrariesAsync(JsonElement libraries, string minecraftDir)
    {
        var libDir = Path.Combine(minecraftDir, "libraries");
        var throttle = new SemaphoreSlim(6);
        var tasks = libraries.EnumerateArray().Select(async lib =>
        {
            await throttle.WaitAsync();
            try
            {
                var name = lib.GetProperty("name").GetString()!;
                var parts = name.Split(':');
                if (parts.Length < 3) return;

                var groupId = parts[0].Replace('.', '/');
                var artifactId = parts[1];
                var version = parts[2];
                var classifier = parts.Length >= 4 ? parts[3] : null;
                var jarName = classifier != null
                    ? $"{artifactId}-{version}-{classifier}.jar"
                    : $"{artifactId}-{version}.jar";
                var libFilePath = Path.Combine(libDir, groupId, artifactId, version, jarName);

                if (File.Exists(libFilePath)) return;

                var baseUrl = lib.TryGetProperty("url", out var urlProp)
                    ? urlProp.GetString()!
                    : "https://maven.fabricmc.net/";

                var downloadUrl = $"{baseUrl.TrimEnd('/')}/{groupId}/{artifactId}/{version}/{jarName}";

                var resp = await _http.GetAsync(downloadUrl);
                if (resp.IsSuccessStatusCode)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(libFilePath)!);
                    await using var fs = File.Create(libFilePath);
                    await resp.Content.CopyToAsync(fs);
                }
                else
                {
                    _config.Log($"Kütüphane indirilemedi: {downloadUrl}");
                }
            }
            catch (Exception ex)
            {
                _config.Log($"Fabric lib hatası: {ex.Message}");
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task DownloadAssetsAsync(string assetsDir, string version)
    {
        var indexPath = Path.Combine(assetsDir, "indexes", $"{version}.json");
        if (!File.Exists(indexPath)) return;

        var objectsDir = Path.Combine(assetsDir, "objects");
        var indexJson = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(indexPath));
        if (!indexJson.TryGetProperty("objects", out var objects)) return;

        var total = objects.EnumerateObject().Count();
        var done = 0;
        var assetBaseUrl = "https://resources.download.minecraft.net";
        var throttle = new SemaphoreSlim(8);
        var tasks = objects.EnumerateObject().Select(async asset =>
        {
            await throttle.WaitAsync();
            try
            {
                var hash = asset.Value.GetProperty("hash").GetString()!;
                var prefix = hash[..2];
                var objDir = Path.Combine(objectsDir, prefix);
                var objPath = Path.Combine(objDir, hash);
                if (!File.Exists(objPath))
                {
                    Directory.CreateDirectory(objDir);
                    var resp = await _http.GetAsync($"{assetBaseUrl}/{prefix}/{hash}");
                    if (resp.IsSuccessStatusCode)
                    {
                        await using var fs = File.Create(objPath);
                        await resp.Content.CopyToAsync(fs);
                    }
                }
            }
            catch { }
            finally
            {
                throttle.Release();
                var c = Interlocked.Increment(ref done);
                if (c % 100 == 0 || c == total)
                    Progress?.Report(0.8 + 0.2 * (double)c / total);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task<bool> LaunchInstanceAsync(Instance instance, string username, int ramGB, string assetsDir, string accessToken = "0", string uuid = "", string? serverAddress = null)
    {
        LastError = null;
        try
        {
            var launcherConfig = await _config.LoadConfigAsync();
            var customJvmArgs = launcherConfig.JvmArgs?.Trim() ?? "";
            var preLaunchCmd = launcherConfig.PreLaunchCommand?.Trim();
            var wrapperCmd = launcherConfig.WrapperCommand?.Trim();
            var postExitCmd = launcherConfig.PostExitCommand?.Trim();
            var customJavaPath = launcherConfig.JavaPath?.Trim();

            // Pre-launch command
            if (!string.IsNullOrEmpty(preLaunchCmd))
            {
                _config.Log($"Ön komut çalıştırılıyor: {preLaunchCmd}");
                var prePsi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{preLaunchCmd.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                };
                var preProcess = Process.Start(prePsi);
                if (preProcess != null)
                    preProcess.WaitForExit();
            }

            var javaPath = FindJava(customJavaPath);
            if (javaPath == null)
            {
                LastError = "Java not found";
                _config.Log("Java bulunamadı");
                return false;
            }

            var minecraftDir = _instances.GetInstanceMinecraftPath(instance);
            var gameDir = _instances.GetInstanceGamePath(instance);
            var libDir = Path.Combine(minecraftDir, "libraries");

            string clientJar;
            if (instance.Loader == "fabric" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var versionId = $"fabric-loader-{instance.Version}-{instance.LoaderVersion}";
                clientJar = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.jar");
                if (!File.Exists(clientJar))
                    clientJar = Path.Combine(minecraftDir, "versions", instance.Version, $"{instance.Version}.jar");
            }
            else if (instance.Loader == "quilt" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var versionId = $"quilt-loader-{instance.Version}-{instance.LoaderVersion}";
                clientJar = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.jar");
                if (!File.Exists(clientJar))
                    clientJar = Path.Combine(minecraftDir, "versions", instance.Version, $"{instance.Version}.jar");
            }
            else if (instance.Loader == "forge" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var versionId = $"{instance.Version}-{instance.LoaderVersion}";
                clientJar = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.jar");
                if (!File.Exists(clientJar))
                    clientJar = Path.Combine(minecraftDir, "versions", instance.Version, $"{instance.Version}.jar");
            }
            else if (instance.Loader == "neoforge" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var versionId = instance.LoaderVersion;
                clientJar = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.jar");
                if (!File.Exists(clientJar))
                    clientJar = Path.Combine(minecraftDir, "versions", instance.Version, $"{instance.Version}.jar");
            }
            else
            {
                clientJar = Path.Combine(minecraftDir, "versions", instance.Version, $"{instance.Version}.jar");
            }

            var allJars = new List<string>();
            if (File.Exists(clientJar))
                allJars.Add(clientJar);
            if (Directory.Exists(libDir))
                allJars.AddRange(Directory.GetFiles(libDir, "*.jar", SearchOption.AllDirectories));

            var byArtifact = new Dictionary<string, (string path, Version? version)>();
            foreach (var jar in allJars)
            {
                if (jar.StartsWith(libDir + Path.DirectorySeparatorChar))
                {
                    var rel = Path.GetRelativePath(libDir, jar);
                    var parts = rel.Split(Path.DirectorySeparatorChar);
                    var fileName = parts[^1];
                    var fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
                    if (parts.Length >= 4)
                    {
                        var groupId = string.Join(".", parts.Take(parts.Length - 3));
                        var artifactId = parts[^3];
                        var versionStr = parts[^2];
                        var classifier = "";
                        var baseName = fileNameNoExt;
                        if (baseName.StartsWith(artifactId + "-"))
                            baseName = baseName[(artifactId.Length + 1)..];
                        if (baseName.StartsWith(versionStr + "-"))
                            classifier = baseName[(versionStr.Length + 1)..];
                        var key = $"{groupId}:{artifactId}:{classifier}";
                        Version? v = null;
                        var clean = versionStr.Split('-')[0];
                        if (Version.TryParse(clean, out var parsed))
                            v = parsed;
                        if (!byArtifact.TryGetValue(key, out var existing) ||
                            (v != null && (existing.version == null || v > existing.version)))
                            byArtifact[key] = (jar, v);
                        continue;
                    }
                }
                byArtifact[jar] = (jar, null);
            }

            var classpath = byArtifact.Values.Select(x => x.path).ToList();
            var cp = string.Join(Path.PathSeparator, classpath);

            if (instance.Loader == "neoforge" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var joptBetter = Path.Combine(libDir, "net", "sf", "jopt-simple", "jopt-simple", "5.0.4", "jopt-simple-5.0.4.jar");
                var joptBad = Path.Combine(libDir, "net", "sf", "jopt-simple", "jopt-simple", "6.0-alpha-3", "jopt-simple-6.0-alpha-3.jar");
                if (File.Exists(joptBetter) && File.Exists(joptBad))
                {
                    var idx = classpath.FindIndex(p => p.EndsWith("jopt-simple-6.0-alpha-3.jar"));
                    if (idx >= 0)
                        classpath[idx] = joptBetter;
                    cp = string.Join(Path.PathSeparator, classpath);
                }
            }

            var (mainClass, extraArgs) = GetLaunchConfig(instance);

            var effectiveUuid = FormatUuid(string.IsNullOrEmpty(uuid) ? Guid.NewGuid().ToString("N") : uuid);
            var token = !string.IsNullOrEmpty(accessToken) ? accessToken : "0";

            var versionData = await GetVersionDataAsync(instance.Version);
            var assetIndex = versionData?.GetProperty("assetIndex").GetProperty("id").GetString() ?? instance.Version;

            string jvmArgs;
            string args;

            if (instance.Loader == "neoforge" && !string.IsNullOrEmpty(instance.LoaderVersion))
            {
                var neoVersionId = $"neoforge-{instance.LoaderVersion}";
                var neoJsonPath = Path.Combine(minecraftDir, "versions", neoVersionId, $"{neoVersionId}.json");

                if (File.Exists(neoJsonPath))
                {
                    var neoDoc = JsonDocument.Parse(await File.ReadAllTextAsync(neoJsonPath));
                    var jvmArray = neoDoc.RootElement.GetProperty("arguments").GetProperty("jvm").EnumerateArray();
                    var libraryDir = Path.Combine(minecraftDir, "libraries");
                    var sep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
                    var neoJvmParts = new List<string>();

                    foreach (var entry in jvmArray)
                    {
                        var val = entry.GetString() ?? "";
                        val = val.Replace("${library_directory}", libraryDir);
                        val = val.Replace("${classpath_separator}", sep);
                        val = val.Replace("${version_name}", neoVersionId);
                        if (val.Contains(' '))
                            val = $"\"{val}\"";
                        neoJvmParts.Add(val);
                    }

                    neoJvmParts.Insert(0, "-Dbsl.debug");
                    neoJvmParts.Add($"-DlegacyClassPath={cp}");
                    neoJvmParts.AddRange(customJvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    jvmArgs = string.Join(" ", neoJvmParts);

                    var neoGameArgs = string.Join(" ",
                        neoDoc.RootElement.GetProperty("arguments").GetProperty("game").EnumerateArray()
                            .Select(e => e.GetString() ?? ""));

                    args = $"{jvmArgs} -Xmx{ramGB}G -Xms{Math.Max(1, ramGB / 2)}G " +
                           $"{mainClass} " +
                           $"--gameDir \"{gameDir}\" " +
                           $"--assetsDir \"{assetsDir}\" " +
                           $"--assetIndex \"{assetIndex}\" " +
                           $"--username \"{username}\" " +
                           $"--uuid \"{effectiveUuid}\" " +
                           $"--accessToken \"{token}\" " +
                           $"--version \"{instance.Version}\" " +
                           $"--width {launcherConfig.WindowWidth} --height {launcherConfig.WindowHeight} " +
                           $"{neoGameArgs}";
                }
                else
                {
                    var addOpens = "--add-opens java.base/java.lang.invoke=ALL-UNNAMED --add-opens java.base/java.lang=ALL-UNNAMED --add-opens java.base/java.util=ALL-UNNAMED --add-opens java.base/java.lang.reflect=ALL-UNNAMED --add-opens java.base/java.text=ALL-UNNAMED --add-opens java.base/java.io=ALL-UNNAMED --add-opens java.base/java.net=ALL-UNNAMED --add-opens java.base/java.nio=ALL-UNNAMED --add-opens java.base/sun.security.ssl=ALL-UNNAMED --add-opens java.base/sun.security.util=ALL-UNNAMED ";
                    jvmArgs = $"{addOpens}{customJvmArgs}";
                    args = $"{jvmArgs} -Xmx{ramGB}G -Xms{Math.Max(1, ramGB / 2)}G -cp \"{cp}\" " +
                           $"{mainClass} " +
                           $"--gameDir \"{gameDir}\" " +
                           $"--assetsDir \"{assetsDir}\" " +
                           $"--assetIndex \"{assetIndex}\" " +
                           $"--username \"{username}\" " +
                           $"--uuid \"{effectiveUuid}\" " +
                           $"--accessToken \"{token}\" " +
                           $"--version \"{instance.Version}\" " +
                           $"--width {launcherConfig.WindowWidth} --height {launcherConfig.WindowHeight} " +
                           $"{extraArgs}";
                }
            }
            else
            {
                var addOpens = instance.Loader is "forge" or "neoforge"
                    ? "--add-opens java.base/java.lang.invoke=ALL-UNNAMED --add-opens java.base/java.lang=ALL-UNNAMED --add-opens java.base/java.util=ALL-UNNAMED --add-opens java.base/java.lang.reflect=ALL-UNNAMED --add-opens java.base/java.text=ALL-UNNAMED --add-opens java.base/java.io=ALL-UNNAMED --add-opens java.base/java.net=ALL-UNNAMED --add-opens java.base/java.nio=ALL-UNNAMED --add-opens java.base/sun.security.ssl=ALL-UNNAMED --add-opens java.base/sun.security.util=ALL-UNNAMED "
                    : "";
                jvmArgs = $"{addOpens}{customJvmArgs}";
                args = $"{jvmArgs} -Xmx{ramGB}G -Xms{Math.Max(1, ramGB / 2)}G -cp \"{cp}\" " +
                       $"{mainClass} " +
                       $"--gameDir \"{gameDir}\" " +
                       $"--assetsDir \"{assetsDir}\" " +
                       $"--assetIndex \"{assetIndex}\" " +
                       $"--username \"{username}\" " +
                       $"--uuid \"{effectiveUuid}\" " +
                       $"--accessToken \"{token}\" " +
                       $"--version \"{instance.Version}\" " +
                       $"--width {launcherConfig.WindowWidth} --height {launcherConfig.WindowHeight} " +
                       $"{extraArgs}";
            }

            if (!string.IsNullOrEmpty(serverAddress))
                args += $" --server \"{serverAddress}\" --port 25565";

            _config.Log($"Başlatılıyor: {javaPath} {args}");

            Process? process;
            if (!string.IsNullOrEmpty(wrapperCmd))
            {
                var wrapperPsi = new ProcessStartInfo
                {
                    FileName = wrapperCmd,
                    Arguments = $"{javaPath} {args}",
                    UseShellExecute = false,
                    WorkingDirectory = gameDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                process = Process.Start(wrapperPsi);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = args,
                    UseShellExecute = false,
                    WorkingDirectory = gameDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                process = Process.Start(psi);
            }
            if (process == null)
            {
                _config.Log("Minecraft işlemi başlatılamadı");
                return false;
            }

            // Read streams immediately to prevent pipe buffer blocking on Unix
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // Run on background thread so UI thread doesn't freeze
            var exited = await Task.Run(() => process.WaitForExit(10000));
            if (exited)
            {
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                _config.Log($"Minecraft erken çıktı (kod: {process.ExitCode})");
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    var errText = stderr[..Math.Min(stderr.Length, 1000)];
                    _config.Log($"Hata: {errText}");
                    LastError = errText;
                }
                if (!string.IsNullOrWhiteSpace(stdout))
                    _config.Log($"Çıktı: {stdout[..Math.Min(stdout.Length, 500)]}");
                return false;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var output = await stdoutTask;
                    var error = await stderrTask;
                    if (!string.IsNullOrWhiteSpace(output))
                        _config.Log($"MC çıktı: {output[..Math.Min(output.Length, 10000)]}");
                    if (!string.IsNullOrWhiteSpace(error))
                        _config.Log($"MC hata: {error[..Math.Min(error.Length, 10000)]}");
                    _config.Log($"Minecraft çıkış kodu: {process.ExitCode}");
                }
                catch { }

                // Post-exit command
                if (!string.IsNullOrEmpty(postExitCmd))
                {
                    try
                    {
                        _config.Log($"Çıkış sonrası komut çalıştırılıyor: {postExitCmd}");
                        var postPsi = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"-c \"{postExitCmd.Replace("\"", "\\\"")}\"",
                            UseShellExecute = false,
                        };
                        var postProcess = Process.Start(postPsi);
                        if (postProcess != null)
                            postProcess.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        _config.Log($"Çıkış sonrası komut hatası: {ex.Message}");
                    }
                }
            });

            _config.Log($"Minecraft başlatıldı: {username}, {instance.Loader} {instance.Version}, RAM: {ramGB}GB");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _config.Log($"Başlatma hatası: {ex.Message}");
            return false;
        }
    }

    private static string FormatUuid(string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return uuid;
        if (uuid.Contains('-')) return uuid;
        if (uuid.Length != 32) return uuid;
        try
        {
            return $"{uuid[..8]}-{uuid[8..12]}-{uuid[12..16]}-{uuid[16..20]}-{uuid[20..]}";
        }
        catch
        {
            return uuid;
        }
    }

    private static (string mainClass, string extraArgs) GetLaunchConfig(Instance instance)
    {
        return instance.Loader switch
        {
            "fabric" => ("net.fabricmc.loader.impl.launch.knot.KnotClient", ""),
            "quilt" => ("org.quiltmc.loader.impl.launch.knot.KnotClient", ""),
            "forge" => ("cpw.mods.bootstraplauncher.BootstrapLauncher",
                $"--launchTarget forgeclient --fml.forgeVersion {instance.LoaderVersion ?? "51.0.0"} --fml.mcVersion {instance.Version} --fml.forgeGroup net.minecraftforge"),
            "neoforge" => ("cpw.mods.bootstraplauncher.BootstrapLauncher",
                $"--launchTarget forgeclient --fml.neoVersion {instance.LoaderVersion ?? "21.0.0"} --fml.mcVersion {instance.Version} --fml.forgeGroup net.neoforged"),
            _ => ("net.minecraft.client.main.Main", ""),
        };
    }

    private static string? FindJava(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            return customPath;

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (javaHome != null)
        {
            var javas = new[] { Path.Combine(javaHome, "bin", "java"),
                                Path.Combine(javaHome, "bin", "java.exe") };
            foreach (var j in javas)
                if (File.Exists(j)) return j;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var javaPath = Path.Combine(dir.Trim(), "java");
                if (File.Exists(javaPath)) return javaPath;
                var javaExe = Path.Combine(dir.Trim(), "java.exe");
                if (File.Exists(javaExe)) return javaExe;
            }
            catch { }
        }

        var extraPaths = new[]
        {
            "/usr/bin/java",
            "/usr/lib/jvm/default-java/bin/java",
            "/usr/lib/jvm/default/bin/java",
            "/usr/local/lib/jvm/java/bin/java",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java", "bin", "java.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java", "bin", "java.exe"),
        };
        foreach (var p in extraPaths)
            if (File.Exists(p)) return p;

        // Search JetBrains Toolbox JBR directories
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var toolboxDir = Path.Combine(localAppData, "JetBrains", "Toolbox", "apps");
            if (Directory.Exists(toolboxDir))
            {
                foreach (var appDir in Directory.GetDirectories(toolboxDir, "IDEA-U*"))
                {
                    var jbrDir = Path.Combine(appDir, "jbr", "bin", "java.exe");
                    if (File.Exists(jbrDir)) return jbrDir;
                    var jbrDirUnix = Path.Combine(appDir, "jbr", "bin", "java");
                    if (File.Exists(jbrDirUnix)) return jbrDirUnix;
                }
            }
        }
        catch { }

        var jvmDirs = new[] { "/usr/lib/jvm", "/usr/local/lib/jvm", "/opt/java" };
        foreach (var jvmDir in jvmDirs)
        {
            if (!Directory.Exists(jvmDir)) continue;
            try
            {
                foreach (var dir in Directory.GetDirectories(jvmDir))
                {
                    var javaPath = Path.Combine(dir, "bin", "java");
                    if (File.Exists(javaPath)) return javaPath;
                }
            }
            catch { }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit(2000);
                return "java";
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        _http?.Dispose();
        GC.SuppressFinalize(this);
    }
}
