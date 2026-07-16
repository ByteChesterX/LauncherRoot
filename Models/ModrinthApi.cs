using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LauncherRoot.Models;

public class ModrinthProject
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }
}

public class ModrinthVersion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = "";

    [JsonPropertyName("game_versions")]
    public List<string> GameVersions { get; set; } = [];

    [JsonPropertyName("loaders")]
    public List<string> Loaders { get; set; } = [];

    [JsonPropertyName("files")]
    public List<ModrinthFile> Files { get; set; } = [];

    [JsonPropertyName("date_published")]
    public DateTime DatePublished { get; set; }
}

public class ModrinthFile
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class ModrinthSearchResult
{
    [JsonPropertyName("hits")]
    public List<ModrinthHit> Hits { get; set; } = [];
}

public partial class ModrinthHit : ObservableObject
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("project_type")]
    public string ProjectType { get; set; } = "";

    [JsonIgnore]
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _icon;
}
