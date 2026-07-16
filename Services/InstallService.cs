using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class InstallService : IInstallService
{
    private static readonly string SentinelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".LauncherRoot");

    private static readonly string SentinelFile = Path.Combine(SentinelsDir, ".installed");

    private static string? _installDir;
    private static string InstallDir =>
        _installDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "LauncherRoot");

    private static string DesktopFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications", "LauncherRoot.desktop");

    private static string IconDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "icons", "hicolor", "256x256", "apps");

    private static string IconPath => Path.Combine(IconDir, "LauncherRoot.png");

    public bool IsInstalled => File.Exists(SentinelFile);

    public async Task InstallAsync(bool force = false)
    {
        if (!force && IsInstalled)
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        try
        {
            var exeDir = AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
                return;

            // Copy app files to install directory
            Directory.CreateDirectory(InstallDir);
            CopyDirectory(exeDir, InstallDir);
            SetAllExecutable(InstallDir);

            // Create icon
            Directory.CreateDirectory(IconDir);
            CreatePngIcon(IconPath);

            // Create .desktop file
            var desktopDir = Path.GetDirectoryName(DesktopFilePath);
            if (desktopDir != null)
                Directory.CreateDirectory(desktopDir);

            var execPath = FindExecutable(InstallDir);
            var iconPath = IconPath;

            var desktopContent = GenerateDesktopEntry(execPath, iconPath);
            await File.WriteAllTextAsync(DesktopFilePath, desktopContent);
            SetExecutable(DesktopFilePath);

            // Mark installed
            Directory.CreateDirectory(SentinelsDir);
            await File.WriteAllTextAsync(SentinelFile, DateTime.UtcNow.ToString("O"));

            // Signal icon cache update
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var iconThemeDir = Path.Combine(home, ".local", "share", "icons", "hicolor");
            if (Directory.Exists(iconThemeDir))
            {
                try
                {
                    File.SetLastWriteTimeUtc(iconThemeDir, DateTime.UtcNow);
                }
                catch
                {
                    // Best effort
                }
            }
        }
        catch
        {
            // Install failure should not crash the app
        }
    }

    private static string FindExecutable(string directory)
    {
        var nativeExe = Path.Combine(directory, "LauncherRoot");
        if (File.Exists(nativeExe))
            return nativeExe;

        var dllPath = Path.Combine(directory, "LauncherRoot.dll");
        if (File.Exists(dllPath))
            return $"dotnet {dllPath}";

        return nativeExe;
    }

    private static string GenerateDesktopEntry(string execPath, string iconPath)
    {
        return $"[Desktop Entry]\n" +
               $"Type=Application\n" +
               $"Name=LauncherRoot\n" +
               $"Comment=Minecraft Mod Launcher\n" +
               $"Exec={execPath}\n" +
               $"Icon={iconPath}\n" +
               $"Terminal=false\n" +
               $"Categories=Game;\n" +
               $"StartupNotify=true\n";
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var dest = Path.Combine(destDir, fileName);
            try
            {
                File.Copy(file, dest, true);
            }
            catch
            {
                // Skip files that can't be copied (e.g. in use)
            }
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSub = Path.Combine(destDir, dirName);
            Directory.CreateDirectory(destSub);
            CopyDirectory(subDir, destSub);
        }

        // Restore execute permission on the native binary
        var nativeExe = Path.Combine(destDir, "LauncherRoot");
        if (File.Exists(nativeExe))
            SetExecutable(nativeExe);
    }

    private static void SetExecutable(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort
        }
    }

    private static void SetAllExecutable(string rootDir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (ext == ".so" || ext == "" && Path.GetFileName(file) == "LauncherRoot")
                {
                    SetExecutable(file);
                }
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static void CreatePngIcon(string path)
    {
        var greenR = (byte)0x4A;
        var greenG = (byte)0xDE;
        var greenB = (byte)0x80;

        var size = 256;
        var rawPixels = new byte[size * size * 4];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var cx = x - size / 2;
                var cy = y - size / 2;
                var dist = Math.Sqrt(cx * cx + cy * cy);
                var radius = size / 2 - 8;

                var i = (y * size + x) * 4;
                if (dist <= radius)
                {
                    // Inside circle - solid green
                    rawPixels[i] = greenB;
                    rawPixels[i + 1] = greenG;
                    rawPixels[i + 2] = greenR;
                    rawPixels[i + 3] = (byte)255;

                    if (x > size / 4 && x < size * 3 / 5 &&
                        y > size / 3 && y < size * 3 / 4)
                    {
                        var lx = x > size / 4 && x < size * 3 / 8;
                        var ly = y > size / 2 && y < size * 3 / 4;
                        if (lx || ly)
                        {
                            rawPixels[i] = (byte)0x05;
                            rawPixels[i + 1] = (byte)0x2E;
                            rawPixels[i + 2] = (byte)0x16;
                            rawPixels[i + 3] = (byte)255;
                        }
                    }

                    if (dist > radius - 4 && dist <= radius)
                    {
                        rawPixels[i] = (byte)0x05;
                        rawPixels[i + 1] = (byte)0x2E;
                        rawPixels[i + 2] = (byte)0x16;
                        rawPixels[i + 3] = (byte)255;
                    }
                }
                else
                {
                    rawPixels[i] = 0;
                    rawPixels[i + 1] = 0;
                    rawPixels[i + 2] = 0;
                    rawPixels[i + 3] = 0;
                }
            }
        }

        WritePng(path, rawPixels, size, size);
    }

    private static void WritePng(string path, byte[] pixels, int width, int height)
    {
        using var stream = File.Create(path);
        var writer = new BinaryWriter(stream);

        // PNG signature
        writer.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR chunk
        WriteChunk(writer, "IHDR", GetIhdrData(width, height));

        // IDAT chunk - pixel data
        var rawWithFilter = AddFilterBytes(pixels, width, height);
        var compressed = Compress(rawWithFilter);
        WriteChunk(writer, "IDAT", compressed);

        // IEND chunk
        WriteChunk(writer, "IEND", Array.Empty<byte>());
    }

    private static byte[] GetIhdrData(int width, int height)
    {
        var data = new byte[13];
        BigEndian(data, 0, width);
        BigEndian(data, 4, height);
        data[8] = 8; // bit depth
        data[9] = 6; // color type: RGBA
        data[10] = 0; // compression
        data[11] = 0; // filter
        data[12] = 0; // interlace
        return data;
    }

    private static byte[] AddFilterBytes(byte[] pixels, int width, int height)
    {
        var rowSize = width * 4;
        var result = new byte[(rowSize + 1) * height];
        for (var y = 0; y < height; y++)
        {
            result[y * (rowSize + 1)] = 0; // filter: None
            Buffer.BlockCopy(pixels, y * rowSize, result, y * (rowSize + 1) + 1, rowSize);
        }
        return result;
    }

    private static byte[] Compress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var deflate = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            input.CopyTo(deflate);
        }

        var compressed = output.ToArray();

        // Prepend zlib header (2 bytes)
        var zlib = new byte[compressed.Length + 6];
        zlib[0] = 0x78; // CMF: deflate, window bits 15
        zlib[1] = 0x9C; // FLG: default compression
        Array.Copy(compressed, 0, zlib, 2, compressed.Length);

        // Adler-32 checksum (raw)
        var adler = Adler32(data);
        BigEndian(zlib, zlib.Length - 4, (int)adler);

        return zlib;
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        var a = 1U;
        var b = 0U;
        foreach (var byteVal in data)
        {
            a = (a + byteVal) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static void WriteChunk(BinaryWriter writer, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var crcData = new byte[typeBytes.Length + data.Length];
        Array.Copy(typeBytes, 0, crcData, 0, typeBytes.Length);
        Array.Copy(data, 0, crcData, typeBytes.Length, data.Length);

        BigEndian(writer, (uint)data.Length);
        writer.Write(typeBytes);
        writer.Write(data);
        BigEndian(writer, Crc32(crcData));
    }

    private static uint Crc32(byte[] data)
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }

        var crc = 0xFFFFFFFFU;
        foreach (var b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFU;
    }

    private static void BigEndian(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private static void BigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
