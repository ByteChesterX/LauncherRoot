using System.Collections.Generic;

namespace LauncherRoot.Models;

public class Theme
{
    public static string CurrentLanguage { get; set; } = "tr";

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<ThemeMod> Mods { get; set; } = [];

    public string LocalizedName => CurrentLanguage == "tr" ? Name : NameEn;
    public string LocalizedDescription => CurrentLanguage == "tr" ? Description : DescriptionEn;
}

public class ThemeMod
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
}
