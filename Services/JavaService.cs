using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class JavaService
{
    private readonly HttpClient _http = new();
    private const string AdoptiumApi = "https://api.adoptium.net/v3";

    public static string? FindSystemJava()
    {
        try
        {
            var psi = new ProcessStartInfo("java", "-version")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardError.ReadToEnd();
            proc.WaitForExit(3000);
            return proc.ExitCode != 0 ? null : "java";
        }
        catch { return null; }
    }

    public async Task<string?> DownloadJavaAsync(string installDir)
    {
        try
        {
            var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var os = OperatingSystem.IsWindows() ? "windows"
                : OperatingSystem.IsMacOS() ? "mac"
                : "linux";

            var infoUrl = $"{AdoptiumApi}/assets/feature_releases/21/ga?architecture={arch}&image_type=jre&os={os}&page_size=1&sort_method=DEFAULT";
            var info = await _http.GetFromJsonAsync<JsonElement>(infoUrl);
            if (!info.TryGetProperty("releases", out var releases) || releases.GetArrayLength() == 0)
                return null;

            var release = releases[0];
            var pkgInfo = release.GetProperty("binaries")[0].GetProperty("package");
            var link = pkgInfo.GetProperty("link").GetString()!;
            var fileName = Path.GetFileName(link);

            Directory.CreateDirectory(installDir);
            var zipPath = Path.Combine(installDir, fileName);

            using (var resp = await _http.GetAsync(link))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await resp.Content.CopyToAsync(fs);
            }

            var javaDir = Path.Combine(installDir, "java21");
            if (Directory.Exists(javaDir)) Directory.Delete(javaDir, true);

            if (OperatingSystem.IsWindows())
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, javaDir);
            else
            {
                var psi = new ProcessStartInfo("tar")
                {
                    Arguments = $"-xzf \"{zipPath}\" -C \"{javaDir}\" --strip-components=1",
                    UseShellExecute = false,
                };
                Directory.CreateDirectory(javaDir);
                Process.Start(psi)?.WaitForExit(60000);
            }

            File.Delete(zipPath);

            var javaFiles = Directory.GetFiles(javaDir, "java", SearchOption.AllDirectories);
            if (javaFiles.Length > 0) return javaFiles[0];

            javaFiles = Directory.GetFiles(javaDir, "java.exe", SearchOption.AllDirectories);
            return javaFiles.Length > 0 ? javaFiles[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
