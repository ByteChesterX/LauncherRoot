using System;

namespace LauncherRoot.Models;

public class BackupInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string InstanceId { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long SizeBytes { get; set; }
}
