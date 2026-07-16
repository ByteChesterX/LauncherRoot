using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class LocalizationService : ILocalizationService
{
    private Dictionary<string, string> _strings = [];
    public string CurrentLanguage { get; private set; } = "tr";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _strings.TryGetValue(key, out var val) ? val : $"[{key}]";

    public async Task SetLanguageAsync(string code)
    {
        CurrentLanguage = code;
        await LoadLanguageAsync(code);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentLanguage"));
    }

    private async Task LoadLanguageAsync(string code)
    {
        var culture = code switch
        {
            "tr" => "tr",
            "en" => "en",
            _ => "en"
        };

        try
        {
            var assembly = typeof(LocalizationService).Assembly;
            var resourceName = $"LauncherRoot.Resources.locales.{culture}.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? GetDefaultStrings(culture);
            }
            else
            {
                _strings = GetDefaultStrings(culture);
            }
        }
        catch
        {
            _strings = GetDefaultStrings(culture);
        }
    }

    private static Dictionary<string, string> GetDefaultStrings(string culture)
    {
        if (culture == "tr")
            return new Dictionary<string, string>
            {
                ["app.title"] = "LauncherRoot",
                ["player.welcome"] = "LauncherRoot'a Hoş Geldiniz!",
                ["player.name"] = "Kullanıcı Adı",
                ["player.placeholder"] = "Kullanıcı adınızı girin...",
                ["player.continue"] = "Devam Et",
                ["theme.title"] = "Tema Seçimi",
                ["theme.select"] = "Bir tema seçerek başlayın:",
                ["theme.downloading"] = "Modlar indiriliyor...",
                ["theme.download.complete"] = "İndirme tamamlandı!",
                ["splash.letithappen"] = "Let it happen...",
                ["menu.play"] = "Oyna",
                ["menu.modmanagement"] = "Mod Yönetimi",
                ["menu.settings"] = "Ayarlar",
                ["menu.back"] = "Geri Dön",
                ["modmng.title"] = "Mod Yönetimi",
                ["modmng.installed"] = "Yüklü Modlar",
                ["modmng.search"] = "Mod Ara...",
                ["modmng.searchPlaceholder"] = "Modrinth'te ara...",
                ["modmng.searching"] = "Aranıyor...",
                ["modmng.results"] = "sonuç bulundu",
                ["modmng.noresults"] = "Sonuç bulunamadı",
                ["modmng.install"] = "Yükle",
                ["modmng.enabled"] = "Aktif",
                ["modmng.disabled"] = "Devre Dışı",
                ["settings.title"] = "Ayarlar",
                ["settings.ram"] = "RAM (GB)",
                ["settings.fps"] = "FPS Sınırı",
                ["settings.fps.unlimited"] = "Sınırsız",
                ["settings.theme"] = "Tema",
                ["settings.theme.light"] = "Açık",
                ["settings.theme.dark"] = "Koyu",
                ["settings.language"] = "Dil",
                ["settings.reset"] = "Launcher'ı Sıfırla",
                ["settings.reset.confirm"] = "Tüm ayarlar silinecek. Emin misiniz?",
                ["settings.reset.done"] = "Launcher sıfırlandı. Yeniden başlatılıyor...",
                ["download.complete"] = "İndirme tamamlandı, oyun keyfine başlayabilirsiniz!",
                ["status.downloading.fabric"] = "Fabric yükleniyor...",
                ["status.downloading.minecraft"] = "Minecraft indiriliyor...",
                ["status.downloading.libraries"] = "Kütüphaneler indiriliyor...",
                ["status.preparing"] = "Oyun hazırlanıyor...",
                ["status.launching"] = "Oyun başlatılıyor...",
                ["error.noplayer"] = "Lütfen bir kullanıcı adı girin.",
                ["error.network"] = "İnternet bağlantısı hatası!",
                ["error.api"] = "API hatası oluştu.",
                ["error.java"] = "Java bulunamadı! Lütfen Java'yı kurun.",
                ["error.download"] = "Dosyalar indirilemedi. İnternet bağlantınızı kontrol edin.",
                ["error.version"] = "Sürüm bulunamadı: ",
            };

        return new Dictionary<string, string>
        {
            ["app.title"] = "LauncherRoot",
            ["player.welcome"] = "Welcome to LauncherRoot!",
            ["player.name"] = "Username",
            ["player.placeholder"] = "Enter your username...",
            ["player.continue"] = "Continue",
            ["theme.title"] = "Theme Selection",
            ["theme.select"] = "Start by selecting a theme:",
            ["theme.downloading"] = "Downloading mods...",
            ["theme.download.complete"] = "Download complete!",
            ["splash.letithappen"] = "Let it happen...",
            ["menu.play"] = "Play",
            ["menu.modmanagement"] = "Mod Management",
            ["menu.settings"] = "Settings",
            ["menu.back"] = "Back",
            ["modmng.title"] = "Mod Management",
            ["modmng.installed"] = "Installed Mods",
            ["modmng.search"] = "Search Mods...",
            ["modmng.searchPlaceholder"] = "Search on Modrinth...",
            ["modmng.searching"] = "Searching...",
            ["modmng.results"] = "results found",
            ["modmng.noresults"] = "No results found",
            ["modmng.install"] = "Install",
            ["modmng.enabled"] = "Enabled",
            ["modmng.disabled"] = "Disabled",
            ["settings.title"] = "Settings",
            ["settings.ram"] = "RAM (GB)",
            ["settings.fps"] = "FPS Limit",
            ["settings.fps.unlimited"] = "Unlimited",
            ["settings.theme"] = "Theme",
            ["settings.theme.light"] = "Light",
            ["settings.theme.dark"] = "Dark",
            ["settings.language"] = "Language",
            ["settings.reset"] = "Reset Launcher",
            ["settings.reset.confirm"] = "All settings will be deleted. Are you sure?",
            ["settings.reset.done"] = "Launcher reset. Restarting...",
            ["download.complete"] = "Download complete, enjoy the game!",
            ["status.downloading.fabric"] = "Installing Fabric...",
            ["status.downloading.minecraft"] = "Downloading Minecraft...",
            ["status.downloading.libraries"] = "Downloading libraries...",
            ["status.preparing"] = "Preparing game...",
            ["status.launching"] = "Launching game...",
            ["error.noplayer"] = "Please enter a username.",
            ["error.network"] = "Network connection error!",
            ["error.api"] = "An API error occurred.",
            ["error.java"] = "Java not found! Please install Java.",
            ["error.download"] = "Failed to download files. Check your internet connection.",
            ["error.version"] = "Version not found: ",
        };
    }
}
