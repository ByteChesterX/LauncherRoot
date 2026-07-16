using System.Collections.Generic;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public class ThemeService : IThemeService
{
    private readonly ConfigService _config;

    public ThemeService(ConfigService config)
    {
        _config = config;
    }

    public List<Theme> GetThemes()
    {
        return
        [
            new Theme
            {
                Id = "performance",
                Name = "Performans Modlar\u0131",
                NameEn = "Performance Mods",
                Description = "Sodium, Lithium, Phosphor ve FerriteCore ile oyununuzu h\u0131zland\u0131r\u0131n",
                DescriptionEn = "Boost your game with Sodium, Lithium, Phosphor and FerriteCore",
                Icon = "\u26A1",
                Mods =
                [
                    new ThemeMod { Slug = "sodium", Name = "Sodium" },
                    new ThemeMod { Slug = "lithium", Name = "Lithium" },
                    new ThemeMod { Slug = "phosphor", Name = "Phosphor" },
                    new ThemeMod { Slug = "ferrite-core", Name = "FerriteCore" },
                    new ThemeMod { Slug = "krypton", Name = "Krypton" },
                    new ThemeMod { Slug = "smoothboot-fabric", Name = "Smooth Boot" },
                    new ThemeMod { Slug = "lazy-dfu", Name = "Lazy DFU" },
                    new ThemeMod { Slug = "entityculling", Name = "Entity Culling" },
                    new ThemeMod { Slug = "immediatelyfast", Name = "ImmediatelyFast" },
                    new ThemeMod { Slug = "modernfix", Name = "ModernFix" },
                ]
            },
            new Theme
            {
                Id = "visual",
                Name = "G\u00F6rsel Modlar",
                NameEn = "Visual Mods",
                Description = "Iris Shaders, Sodium + Shaders ve Better Clouds ile g\u00F6rselleri zenginle\u015Ftirin",
                DescriptionEn = "Enhance visuals with Iris Shaders, Sodium + Shaders and Better Clouds",
                Icon = "\u2728",
                Mods =
                [
                    new ThemeMod { Slug = "iris", Name = "Iris" },
                    new ThemeMod { Slug = "sodium", Name = "Sodium" },
                    new ThemeMod { Slug = "better-clouds", Name = "Better Clouds" },
                    new ThemeMod { Slug = "fabulously-optimized", Name = "Fabulously Optimized" },
                    new ThemeMod { Slug = "complementary-shaders", Name = "Complementary Shaders" },
                    new ThemeMod { Slug = "bsl-shaders", Name = "BSL Shaders" },
                    new ThemeMod { Slug = "make-up-ultra-fast-shaders", Name = "MakeUp Ultra Fast Shaders" },
                    new ThemeMod { Slug = "continuity", Name = "Continuity" },
                    new ThemeMod { Slug = "lambda-better-grass", Name = "Lambda Better Grass" },
                    new ThemeMod { Slug = "dynamic-fps", Name = "Dynamic FPS" },
                ]
            },
            new Theme
            {
                Id = "hybrid",
                Name = "Karma Modlar",
                NameEn = "Hybrid Mods",
                Description = "Performans ve g\u00F6rsel modlar\u0131n en iyi kar\u0131\u015F\u0131m\u0131",
                DescriptionEn = "Best mix of performance and visual mods",
                Icon = "\u2699\uFE0F",
                Mods =
                [
                    new ThemeMod { Slug = "sodium", Name = "Sodium" },
                    new ThemeMod { Slug = "lithium", Name = "Lithium" },
                    new ThemeMod { Slug = "iris", Name = "Iris" },
                    new ThemeMod { Slug = "phosphor", Name = "Phosphor" },
                    new ThemeMod { Slug = "ferrite-core", Name = "FerriteCore" },
                    new ThemeMod { Slug = "continuity", Name = "Continuity" },
                    new ThemeMod { Slug = "entityculling", Name = "Entity Culling" },
                    new ThemeMod { Slug = "dynamic-fps", Name = "Dynamic FPS" },
                    new ThemeMod { Slug = "immediatelyfast", Name = "ImmediatelyFast" },
                    new ThemeMod { Slug = "modernfix", Name = "ModernFix" },
                ]
            },
            new Theme
            {
                Id = "vanillaplus",
                Name = "Vanilla+",
                NameEn = "Vanilla+",
                Description = "Vanilla hissini koruyan Quality of Life iyile\u015Ftirmeleri",
                DescriptionEn = "Quality of Life improvements that preserve the vanilla feel",
                Icon = "\uD83C\uDF1F",
                Mods =
                [
                    new ThemeMod { Slug = "appleskin", Name = "AppleSkin" },
                    new ThemeMod { Slug = "jade", Name = "Jade" },
                    new ThemeMod { Slug = "minihud", Name = "MiniHUD" },
                    new ThemeMod { Slug = "litematica", Name = "Litematica" },
                    new ThemeMod { Slug = "xaeros-minimap", Name = "Xaero's Minimap" },
                    new ThemeMod { Slug = "xaeros-world-map", Name = "Xaero's World Map" },
                    new ThemeMod { Slug = "inventory-profiles-next", Name = "Inventory Profiles Next" },
                    new ThemeMod { Slug = "mouse-tweaks", Name = "Mouse Tweaks" },
                    new ThemeMod { Slug = "roughly-enough-items", Name = "Roughly Enough Items" },
                    new ThemeMod { Slug = "modmenu", Name = "Mod Menu" },
                ]
            }
        ];
    }
}
