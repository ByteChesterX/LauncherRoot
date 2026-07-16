using System.Collections.Generic;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface IBackupService
{
    string BackupRoot { get; }
    Task<BackupInfo> BackupAsync(Instance instance);
    Task<bool> RestoreAsync(Instance instance, BackupInfo backup);
    Task<List<BackupInfo>> GetBackupsAsync(Instance instance);
    Task<bool> DeleteBackupAsync(BackupInfo backup);
}
