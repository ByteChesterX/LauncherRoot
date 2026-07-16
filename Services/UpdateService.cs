using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _http;
    private readonly string _configPath;
    private const string GitHubApi = "https://api.github.com/repos";
    private const string Owner = "ddddfffffsgggsgs3232e";
    private const string Repo = "LauncherRoot";

    public string CurrentVersion { get; }

    public UpdateService(ConfigService config)
    {
        _configPath = Path.Combine(config.RootPath, "updates");
        Directory.CreateDirectory(_configPath);

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "0.0.0";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var url = $"{GitHubApi}/{Owner}/{Repo}/releases/latest";
            var response = await _http.GetFromJsonAsync<JsonElement>(url);

            var tag = response.GetProperty("tag_name").GetString() ?? "";
            var tagVersion = tag.TrimStart('v');
            var body = response.GetProperty("body").GetString() ?? "";

            if (!TryParseVersion(tagVersion, out var remote))
                return null;

            if (!TryParseVersion(CurrentVersion, out var local))
                return null;

            if (remote <= local)
                return null;

            if (!response.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0)
                return null;

            var asset = assets[0];
            var downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
            var size = asset.GetProperty("size").GetInt64();

            if (string.IsNullOrEmpty(downloadUrl))
                return null;

            return new UpdateInfo
            {
                Version = tagVersion,
                DownloadUrl = downloadUrl,
                Changelog = body,
                Size = size,
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo info)
    {
        try
        {
            var fileName = Path.GetFileName(info.DownloadUrl) ?? $"LauncherRoot-{info.Version}";
            var destPath = Path.Combine(_configPath, fileName);

            if (File.Exists(destPath))
                return destPath;

            using var resp = await _http.GetAsync(info.DownloadUrl);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(destPath);
            await resp.Content.CopyToAsync(fs);

            return destPath;
        }
        catch
        {
            return null;
        }
    }

    public void ScheduleRestart(string newFilePath)
    {
        var appPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(appPath)) return;

        var scriptPath = Path.Combine(_configPath, "update.sh");

        if (OperatingSystem.IsWindows())
        {
            scriptPath = Path.Combine(_configPath, "update.cmd");
            var batch = $"""
                @echo off
                timeout /t 2 /nobreak >nul
                copy /y "{newFilePath}" "{appPath}"
                start "" "{appPath}" --updated
                del "%~f0"
                """;
            File.WriteAllText(scriptPath, batch);
        }
        else
        {
            var shell = $""""
                #!/bin/sh
                sleep 2
                cp "{newFilePath}" "{appPath}"
                chmod +x "{appPath}"
                "{appPath}" --updated &
                rm -- "$0"
                """";
            File.WriteAllText(scriptPath, shell);
            Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit(2000);
        }

        var psi = new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
        };
        Process.Start(psi);
    }

    private static bool TryParseVersion(string version, out Version result)
    {
        if (Version.TryParse(version, out var v))
        {
            result = v;
            return true;
        }

        if (System.Version.TryParse(version + ".0", out v))
        {
            result = v;
            return true;
        }

        result = new Version(0, 0, 0);
        return false;
    }

    public void Dispose() => _http.Dispose();
}
