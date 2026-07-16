using System.Collections.Generic;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface ICurseForgeService
{
    Task<List<CurseForgeMod>> SearchModsAsync(string query, string gameVersion = "", string loader = "", int classId = 0, int limit = 20);
    Task<CurseForgeFile?> GetBestFileAsync(int modId, string gameVersion, string loader);
}
