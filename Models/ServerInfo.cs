using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LauncherRoot.Models;

public partial class ServerInfo : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int PlayCount { get; set; }
    public DateTime LastPlayed { get; set; }

    [ObservableProperty]
    private Bitmap? _icon;
}
