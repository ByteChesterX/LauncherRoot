# LauncherRoot — Sürüm Notları

## 🆕 Eklenen Özellikler

### 1. Konsol Penceresi (LogViewer)
- Ayrı bir pencere olarak açılır (`Views/LogViewerWindow`)
- `~/.LauncherRoot/logs/` dizinindeki en güncel `.log` dosyasını okur
- Konsol tipi font (monospace) ile çıktıyı gösterir
- "🔄 Yenile" butonu ile log içeriği tazelenebilir
- MainMenu'de "📟 Konsol" butonu ile erişilir

### 2. Ekran Görüntüleri Klasörü
- Instance'ın `minecraft/screenshots/` klasörüne tek tıkla erişim
- Klasör yoksa otomatik oluşturulur
- Sidebar'da "📸 Ekran görüntüleri" butonu

### 3. Instance İşlem Butonları Sidebar'a Taşındı
- 7 buton sidebar'ın alt bölümünde sıralanır:
  - ✏️ Düzenle — instance'ı düzenleme modunda açar
  - 🗑️ Sil — instance'ı siler
  - 📋 Log klasörü — instance'ın `logs/` klasörünü açar
  - 📂 Oyun klasörünü aç — instance'ın oyun dizinini açar
  - 📸 Ekran görüntüleri — `screenshots/` klasörünü açar
  - 📟 Konsol — LogViewer penceresini açar
  - 📋 Çoğalt — instance'ı kopyalar
- Butonlar her zaman görünür (hover gerektirmez)
- Instance seçilmediğinde gizlenir

### 4. Yeni Instance Butonu Üst Konum
- "➕ Yeni" butonu instance listesinin üstüne alındı
- Liste çok uzun olsa bile her zaman görünür

### 5. Ayarlar Sayfası Yatay Düzen
- 820px genişliğinde 2 kolonlu grid düzeni
- RAM + FPS yan yana
- Tema + Dil yan yana
- Java Yolu + Çözünürlük yan yana
- JVM Argümanları tam genişlik
- Pre-launch + Wrapper yan yana
- Post-exit tek başına

### 6. Sıfırla Kartı (Ayarlar Sayfası En Üstte)
- Kırmızı çerçeveli bağımsız kart
- ⚠️ uyarı ikonu ve "Tüm ayarları, hesapları ve profilleri siler" açıklaması
- Diğer ayarlardan ayrı konumlandırıldı

### 7. Sidebar Tasarımı Sadeleştirme
- Badge'den tüm butonlar kaldırıldı, sadece seçili instance ismi + versiyon kaldı
- "Seçili:" etiketi eklendi
- "Hoş geldin!" mesajı kullanıcı adının altında gösterilir

### 8. Masaüstü Ortamı Desteği
- Instance butonları `UseShellExecute = true` ile açılır
- Log klasörü, oyun klasörü, ekran görüntüleri işletim sisteminin varsayılan dosya yöneticisinde açılır

### 9. Ayarlar Sayfası

**RAM** (💾)
- 1–16 GB arası slider
- Anlık seçilen değer gösterilir ("4 GB")

**FPS Limiti** (🖥️)
- ComboBox ile hazır seçenekler (30, 60, 120, 144, unlimited)

**Tema** (🎨)
- Light/Dark toggle
- Anlık geçiş, DynamicResource altyapısı

**Dil** (🌐)
- EN / TR toggle
- Tüm arayüz anlık değişir

**Java Yolu** (☕)
- TextBox + "Gözat" butonu ile dosya seçici
- Boş bırakılırsa auto-detect (`PATH`, `JAVA_HOME`, `/usr/lib/jvm`, JetBrains JBR taranır)

**Çözünürlük** (🖥️)
- Genişlik × Yükseklik (px) girişi
- Oyun başlatılırken `--width` / `--height` olarak kullanılır

**JVM Argümanları** (☕)
- Varsayılan: `-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions -XX:MaxGCPauseMillis=50 -XX:+DisableExplicitGC`
- Serbest metin girişi

**Ön/Komut Ayarları**
- 🚀 **Pre-launch** — Java başlatılmadan önce çalıştırılır
- 📦 **Wrapper** — Java komutunu sarar (ör. `prismlauncher`)
- 🔚 **Post-exit** — Oyun kapandıktan sonra çalıştırılır
- Tümü başarısız olsa bile oyun başlatmayı engellemez
- Instance'a özel değil, genel ayarlardır

**🗑️ Sıfırla**
- Tüm ayarları, hesapları ve profilleri siler
- Kırmızı çerçeveli kart ile diğer ayarlardan ayrılmıştır
- Sildikten sonra PlayerSetup sayfasına yönlendirir

## 🔨 Build (Geliştiriciler İçin)

### Klonlama ve Çalıştırma

```bash
git clone https://github.com/ddddfffffsgggsgs3232e/LauncherRoot.git
cd LauncherRoot
dotnet run
```

### Derleme

#### Linux (x64)

```bash
dotnet publish -c Release --self-contained true -r linux-x64 -p:PublishSingleFile=true -o publish-linux
```

#### Windows (x64)

```bash
dotnet publish -c Release --self-contained true -r win-x64 -p:PublishSingleFile=true -o publish-win
```

#### macOS (Intel)

```bash
dotnet publish -c Release --self-contained true -r osx-x64 -p:PublishSingleFile=true -o publish-mac
```

#### macOS (Apple Silicon)

```bash
dotnet publish -c Release --self-contained true -r osx-arm64 -p:PublishSingleFile=true -o publish-mac-arm
```

#### LİSANS
Proje GPL-V3 ile lisanslanmıştır.

## 📦 Gereksinimler

- **JDK 21** (Minecraft için)
  - **Linux:** `sudo apt install openjdk-21-jdk`
  - **Windows:** [Oracle JDK 21](https://www.oracle.com/java/technologies/downloads/)
- **.NET Runtime:** Gerekmez — self-contained derlenmiştir
