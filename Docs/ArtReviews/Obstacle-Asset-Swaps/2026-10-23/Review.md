# Kullanıcının verdiği engel görselleri · 2026-10-23

- `Downloads/OverrideBatteryBox.png` → mevcut `OverrideBatteryBody.png`. Yeni1254×1254 RGBA gövde, aynı sprite GUID/fileID üzerinden library, oyun ve editör önizlemelerine ulaşır. Ayrı şarj barları, gauge ve ibre bağlantıları korundu.
- `Downloads/GelLauncherNavy.png` → mevcut `Resources/GelLauncher/Press/PressFrame.png`. Eski ve yeni çerçeve887×1774 RGBA; oyun ve Level Editor aynı yolu kullandığı için ikisi de navy çerçeveyi alır. Jel/pres parçaları, dört yön ve animasyon kodu aynı.

Görseller kullanıcı dosyalarından bayt-bayt kopyalandı; yeniden çizim, kırpma veya kaynak dosyada yeniden boyutlandırma yapılmadı. Mevcut `.meta`, import ayarları ve GUID'ler aynı. OverrideBattery import512 maksimum/sıkıştırma ayarlarını, PressFrame2048/sıkıştırmasız ayarlarını korur. Yeni kaynak için ek texture veya runtime VFX nesnesi eklenmedi; gerçek cihaz maliyeti ölçülmedi.

`manifest.json` kaynak/hedef hash'lerini ve korunan library/kod/meta hash'lerini içerir; eşitlikler doğrulandı. Eski iki PNG ve meta'ları `Before/` içinde saklı. GelLauncher pres denemesinin telefonda son görsel onayı hâlâ beklenir; bu istek yalnız çerçeve rengini değiştirir.

Unity Play/build çalıştırılmadı; animasyon üst katmanlarının yeni gövde üzerindeki gerçek görünümü cihazda doğrulanmadı.
