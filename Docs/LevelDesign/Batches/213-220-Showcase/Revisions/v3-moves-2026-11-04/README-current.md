# 213–220 · Hamle kalibrasyonu · 2026-11-04

Kullanıcı213–218’i ilk canında geçtiğini,219 ve220’de birer can kaybettiğini bildirdi. Bitirişte kalan hamle sayısı ve tekrar örneklemi verilmedi; bu kayıt ölçülmüş kazanma oranı değildir. Normal seviyelerde2–3,215Hard’da4,219SuperHard ve220Boss’ta birer hamle azaltıldı.219/220 zaten kayıp ürettiğinden daha küçük değişiklik yapıldı.

| Level | Önce | Yeni |
|---|---:|---:|
|213|24|21|
|214|24|21|
|215|26|22|
|216|22|20|
|217|24|21|
|218|25|22|
|219|25|24|
|220|29|28|

Sekiz assette yalnız `moves` satırı değişti; bütün maskeler, diziler, hedefler, katmanlar, pinler, LampStrip/GelLauncher yerleri, boss HP/atak ayarları, isimler, katalog ve GUID/metalar korundu. `revise_moves.py` tek uygulama korumalıdır. Önceki assetler/notlar/PNG/model/validation bu klasördeki snapshot’lardır; app-release baseline değildir.

`verify_moves.py` eski ve yeni assetleri moves satırı dışında byte karşılaştırır ve v2’nin tam YAML/origin/goal/lamba/kanal/refill denetimini yeni kapsamla tekrar kullanır. Kapsam dışındaki eşzamanlı MasterLeague/menü/backend/test-ayarı düzenlemeleri `concurrent-changes.json` içinde ayrıca kayıtlıdır; bu görev bu dosyaları yazmadı. Eski seviye/katalog/isim assetlerinde eşzamanlı değişiklik bulunmadı.

Güncel proje sprite PNG’leri ve toplu sayfa yeni hamleleri gösterir. Geometri değişmediğinden önceki görsel karşılaştırma geçerlidir. Unity build/Play, bot/kaskat veya gravity simülasyonu yapılmadı; yeni bütçeler henüz kullanıcı tarafından oynanmadı. Bu ilk kalibrasyon, kesin denge sonucu değildir.

Ayrı runtime işi: çim altındaki kodla kurulan görsellerin katman HP’si ve view sahipliği düzeltildi; `Docs/Checks/Layered-Obstacle-Contents-2026-11-04/README.md`.
