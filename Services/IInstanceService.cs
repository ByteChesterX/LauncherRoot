using System.Collections.Generic;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface IInstanceService
{
    Task<List<Instance>> LoadInstancesAsync();
    Task SaveInstancesAsync(List<Instance> instances);
    Task<Instance?> GetInstanceAsync(string id);
    Task AddInstanceAsync(Instance instance);
    Task DeleteInstanceAsync(string id);
    Task UpdateInstanceAsync(Instance instance);
    Task<Instance> DuplicateInstanceAsync(Instance instance, string newName);
    string GetInstanceMinecraftPath(Instance instance);
    string GetInstanceModsPath(Instance instance);
    string GetInstanceResourcepackPath(Instance instance);
    string GetInstanceShaderpackPath(Instance instance);
    string GetInstanceGamePath(Instance instance);
    Task<ModState> LoadModStateAsync(Instance instance);
    Task SaveModStateAsync(Instance instance, ModState state);
    Task<byte[]> ExportInstanceAsync(Instance instance);
    Task<Instance?> ImportInstanceAsync(byte[] data);
    Task AddPlayTimeAsync(string instanceId, long seconds);
}
