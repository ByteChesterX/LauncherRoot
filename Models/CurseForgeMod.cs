using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LauncherRoot.Models;

public class CurseForgeSearchResult
{
    [JsonPropertyName("data")]
    public List<CurseForgeMod> Data { get; set; } = [];

    [JsonPropertyName("pagination")]
    public CurseForgePagination? Pagination { get; set; }
}

public class CurseForgePagination
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

public class CurseForgeMod
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("logo")]
    public CurseForgeLogo? Logo { get; set; }

    [JsonPropertyName("latestFiles")]
    public List<CurseForgeFile> LatestFiles { get; set; } = [];

    [JsonPropertyName("classId")]
    public int ClassId { get; set; }
}

public class CurseForgeLogo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }
}

public class CurseForgeFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("gameVersions")]
    public List<string> GameVersions { get; set; } = [];

    [JsonPropertyName("releaseType")]
    public int ReleaseType { get; set; } = 1;
}
