using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class BackupService : IBackupService
{
    private readonly IInstanceService _instances;
    private readonly ConfigService _config;

    public string BackupRoot { get; }

    public BackupService(ConfigService config, IInstanceService instances)
    {
        _config = config;
        _instances = instances;
        BackupRoot = Path.Combine(config.RootPath, "backups");
        Directory.CreateDirectory(BackupRoot);
    }

    public async Task<BackupInfo> BackupAsync(Instance instance)
    {
        var instanceDir = _instances.GetInstanceGamePath(instance);
        var instanceBackupDir = Path.Combine(BackupRoot, instance.Id);
        Directory.CreateDirectory(instanceBackupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{SanitizeFileName(instance.Name)}_{timestamp}.zip";
        var zipPath = Path.Combine(instanceBackupDir, fileName);

        if (Directory.Exists(instanceDir))
        {
            ZipFile.CreateFromDirectory(instanceDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        else
        {
            // Create an empty zip if no game directory exists yet
            using var emptyZip = System.IO.Compression.ZipFile.Open(zipPath, ZipArchiveMode.Create);
        }

        var fileInfo = new FileInfo(zipPath);

        var backup = new BackupInfo
        {
            InstanceId = instance.Id,
            FileName = fileName,
            CreatedAt = DateTime.Now,
            SizeBytes = fileInfo.Length,
        };

        await SaveBackupMetaAsync(instance.Id, backup);
        _config.Log($"Yedek oluşturuldu: {fileName} ({backup.SizeBytes} bytes)");
        return backup;
    }

    public Task<bool> RestoreAsync(Instance instance, BackupInfo backup)
    {
        var zipPath = Path.Combine(BackupRoot, instance.Id, backup.FileName);
        if (!File.Exists(zipPath)) return Task.FromResult(false);

        try
        {
            var instanceDir = _instances.GetInstanceGamePath(instance);
            if (Directory.Exists(instanceDir))
                Directory.Delete(instanceDir, recursive: true);

            ZipFile.ExtractToDirectory(zipPath, instanceDir);
            _config.Log($"Yedek geri yüklendi: {backup.FileName}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _config.Log($"Yedek geri yükleme hatası: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<List<BackupInfo>> GetBackupsAsync(Instance instance)
    {
        var metaPath = GetMetaPath(instance.Id);
        if (!File.Exists(metaPath))
            return Task.FromResult(new List<BackupInfo>());

        try
        {
            var json = File.ReadAllText(metaPath);
            var backups = JsonSerializer.Deserialize<List<BackupInfo>>(json) ?? [];
            return Task.FromResult(backups.OrderByDescending(b => b.CreatedAt).ToList());
        }
        catch
        {
            return Task.FromResult(new List<BackupInfo>());
        }
    }

    public Task<bool> DeleteBackupAsync(BackupInfo backup)
    {
        try
        {
            var zipPath = Path.Combine(BackupRoot, backup.InstanceId, backup.FileName);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var backups = GetBackupsAsync(new Instance { Id = backup.InstanceId }).GetAwaiter().GetResult();
            backups.RemoveAll(b => b.Id == backup.Id);
            var json = JsonSerializer.Serialize(backups);
            File.WriteAllText(GetMetaPath(backup.InstanceId), json);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private string GetMetaPath(string instanceId) =>
        Path.Combine(BackupRoot, instanceId, "meta.json");

    private async Task SaveBackupMetaAsync(string instanceId, BackupInfo backup)
    {
        var backups = await GetBackupsAsync(new Instance { Id = instanceId });
        backups.Add(backup);
        var json = JsonSerializer.Serialize(backups);
        await File.WriteAllTextAsync(GetMetaPath(instanceId), json);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
