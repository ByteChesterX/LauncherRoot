using CommunityToolkit.Mvvm.ComponentModel;

namespace LauncherRoot.Models;

public partial class ModInfo : ObservableObject
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? IconUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? FileName { get; set; }

    [ObservableProperty]
    private bool _enabled = true;

    public bool Downloaded { get; set; }
    public bool HasUpdate { get; set; }
    public string? LatestVersion { get; set; }
    public string? InstalledVersion { get; set; }
}
