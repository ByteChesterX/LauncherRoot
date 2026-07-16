using System;

namespace LauncherRoot.Models;

public class PlayerConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AuthType { get; set; } = "offline";
    public string DisplayName => $"{Username} ({AuthType})";
}
