using CommunityToolkit.Mvvm.ComponentModel;

namespace LauncherRoot.Models;

public partial class ModSearchResult : ObservableObject
{
    public string Source { get; set; } = "modrinth";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Slug { get; set; } = "";
    public int CurseForgeId { get; set; }
    public string IconUrl { get; set; } = "";

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _icon;
}
