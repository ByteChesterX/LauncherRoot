using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class InstanceService : IInstanceService
{
    private readonly string _instancesDir;
    private readonly string _instancesFilePath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public InstanceService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".LauncherRoot");
        _instancesDir = Path.Combine(root, "instances");
        _instancesFilePath = Path.Combine(root, "instances.json");
        Directory.CreateDirectory(_instancesDir);
    }

    public async Task<List<Instance>> LoadInstancesAsync()
    {
        return await TryReadJsonAsync<List<Instance>>(_instancesFilePath) ?? [];
    }

    public async Task SaveInstancesAsync(List<Instance> instances)
    {
        await WriteJsonAtomicAsync(_instancesFilePath, instances);
    }

    public async Task<Instance?> GetInstanceAsync(string id)
    {
        var instances = await LoadInstancesAsync();
        return instances.Find(i => i.Id == id);
    }

    public async Task AddInstanceAsync(Instance instance)
    {
        var instances = await LoadInstancesAsync();
        instances.Add(instance);
        await SaveInstancesAsync(instances);

        var instanceDir = Path.Combine(_instancesDir, instance.InstanceDir);
        Directory.CreateDirectory(Path.Combine(instanceDir, "minecraft", "versions"));
        Directory.CreateDirectory(Path.Combine(instanceDir, "minecraft", "libraries"));
        Directory.CreateDirectory(Path.Combine(instanceDir, "minecraft", "resourcepacks"));
        Directory.CreateDirectory(Path.Combine(instanceDir, "minecraft", "shaderpacks"));
        Directory.CreateDirectory(Path.Combine(instanceDir, "mods"));
    }

    public async Task DeleteInstanceAsync(string id)
    {
        var instances = await LoadInstancesAsync();
        var instance = instances.Find(i => i.Id == id);
        if (instance != null)
        {
            instances.Remove(instance);
            await SaveInstancesAsync(instances);

            var instanceDir = Path.Combine(_instancesDir, instance.InstanceDir);
            if (Directory.Exists(instanceDir))
                Directory.Delete(instanceDir, true);
        }
    }

    public async Task UpdateInstanceAsync(Instance instance)
    {
        var instances = await LoadInstancesAsync();
        var index = instances.FindIndex(i => i.Id == instance.Id);
        if (index < 0) return;

        instances[index] = instance;
        await SaveInstancesAsync(instances);
    }

    public async Task<Instance> DuplicateInstanceAsync(Instance source, string newName)
    {
        var instances = await LoadInstancesAsync();

        var clone = new Instance
        {
            Name = newName,
            Version = source.Version,
            Loader = source.Loader,
            LoaderVersion = source.LoaderVersion,
        };

        // Copy instance directory if it exists
        var srcDir = Path.Combine(_instancesDir, source.InstanceDir);
        var dstDir = Path.Combine(_instancesDir, clone.InstanceDir);
        if (Directory.Exists(srcDir))
            CopyDirectory(srcDir, dstDir);

        instances.Add(clone);
        await SaveInstancesAsync(instances);
        return clone;
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var dest = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }

    public string GetInstanceMinecraftPath(Instance instance)
    {
        return Path.Combine(_instancesDir, instance.InstanceDir, "minecraft");
    }

    public string GetInstanceModsPath(Instance instance)
    {
        return Path.Combine(_instancesDir, instance.InstanceDir, "mods");
    }

    public string GetInstanceResourcepackPath(Instance instance)
    {
        return Path.Combine(_instancesDir, instance.InstanceDir, "minecraft", "resourcepacks");
    }

    public string GetInstanceShaderpackPath(Instance instance)
    {
        return Path.Combine(_instancesDir, instance.InstanceDir, "minecraft", "shaderpacks");
    }

    public string GetInstanceGamePath(Instance instance)
    {
        return Path.Combine(_instancesDir, instance.InstanceDir);
    }

    public async Task<ModState> LoadModStateAsync(Instance instance)
    {
        var path = Path.Combine(_instancesDir, instance.InstanceDir, "modstate.json");
        return await TryReadJsonAsync<ModState>(path) ?? new ModState();
    }

    public async Task SaveModStateAsync(Instance instance, ModState state)
    {
        var dir = Path.Combine(_instancesDir, instance.InstanceDir);
        Directory.CreateDirectory(dir);
        await WriteJsonAtomicAsync(Path.Combine(dir, "modstate.json"), state);
    }

    public async Task<byte[]> ExportInstanceAsync(Instance instance)
    {
        var instanceDir = Path.Combine(_instancesDir, instance.InstanceDir);
        using var ms = new MemoryStream();
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true);

        var metaEntry = archive.CreateEntry("instance.json");
        await using (var writer = new StreamWriter(metaEntry.Open()))
            await writer.WriteAsync(JsonSerializer.Serialize(instance, JsonOpts));

        var modsDir = Path.Combine(instanceDir, "mods");
        if (Directory.Exists(modsDir))
        {
            foreach (var file in Directory.GetFiles(modsDir))
            {
                var name = Path.GetFileName(file);
                var entry = archive.CreateEntry($"mods/{name}");
                await using (var entryStream = entry.Open())
                await using (var fileStream = File.OpenRead(file))
                    await fileStream.CopyToAsync(entryStream);
            }
        }

        return ms.ToArray();
    }

    public async Task<Instance?> ImportInstanceAsync(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);

            var metaEntry = archive.GetEntry("instance.json");
            if (metaEntry == null) return null;

            Instance? instance;
            using (var reader = new StreamReader(metaEntry.Open()))
                instance = JsonSerializer.Deserialize<Instance>(await reader.ReadToEndAsync(), JsonOpts);

            if (instance == null) return null;

            instance.Id = Guid.NewGuid().ToString("N")[..8];
            instance.Name = $"{instance.Name} (imported)";
            instance.CreatedAt = DateTime.Now;

            var instanceDir = Path.Combine(_instancesDir, instance.InstanceDir);
            Directory.CreateDirectory(Path.Combine(instanceDir, "mods"));

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("mods/", StringComparison.Ordinal))
                    continue;
                if (entry.FullName.EndsWith("/") || entry.Name.Length == 0)
                    continue;

                var target = Path.GetFullPath(Path.Combine(instanceDir, entry.FullName));
                var root = Path.GetFullPath(instanceDir);
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using (var entryStream = entry.Open())
                await using (var fs = File.Create(target))
                    await entryStream.CopyToAsync(fs);
            }

            var instances = await LoadInstancesAsync();
            instances.Add(instance);
            await SaveInstancesAsync(instances);

            return instance;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private async Task<T?> TryReadJsonAsync<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;

            await using var fs = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(fs, JsonOpts);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task WriteJsonAtomicAsync<T>(string path, T value)
    {
        await _writeLock.WaitAsync();
        try
        {
            var tmp = path + ".tmp";
            await using (var fs = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(fs, value, JsonOpts);
            }
            File.Move(tmp, path, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AddPlayTimeAsync(string instanceId, long seconds)
    {
        var instances = await LoadInstancesAsync();
        var instance = instances.Find(i => i.Id == instanceId);
        if (instance == null) return;
        instance.PlayTimeSeconds += seconds;
        await SaveInstancesAsync(instances);
    }
}
