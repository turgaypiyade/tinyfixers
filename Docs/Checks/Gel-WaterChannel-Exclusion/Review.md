# Su kanalına jel bulaşmaması · 2026-10-23

Kullanıcı kuralı: su kanalı bir obstacle'dır; jel bulaşması kanalın üstüne eklenmemeli.

Kanal hücresinin mantıksal obstacle ID'si açılışta silinir; açık kanal zemini ve su görünümü board'da kalır. Bu nedenle yalnız `IsWaterChannelAt` kontrolü açılmış/tamamlanmış kanalı korumaz.

`SpreadingGelOverlayService.Init` mevcut levelin geçerli kanal yollarını, depo dahil, bir HashSet'e bir kez kaydeder. Spawner bu level bilgisini ilk seed çiziminden önce verir. Ortak `AddGelInternal` kanal hücresini görsel üretimi, fade, border refresh ve hedef bildirimi öncesinde reddeder. Normal eşleşme, Pulse/diğer combo, joker, yeni special altı, GelLauncher izi ve doğrudan seed/reveal eklemeleri aynı girişten geçer. Kapalı/açılmış/ıslanmış/tamamlanmış durum ayrımı olmaksızın bütün kanal yolu hariçtir. Jelin sıradan engel örtüsü altında bekleyen seed davranışı korunur.

`ClearAll` görselleri temizler, aynı board'un kanal haritasını korur. Yeni levelde `Init` eski üyeliği temizleyip yenisini kurar; önceki levelin yolu sonrakini engellemez. Yeni event aboneliği veya teardown callback'i yoktur.

Performans: bir kalıcı set ve level kurulurken yol başına doğrulama/üyelik eklemesi; jel ekleme başına O(1) sorgu. Engellenen hücre için obje, animasyon veya border işi başlamaz. Yeni per-frame tarama/Update/ses kaynağı yok; mevcut görsel üretimi değişmedi ve cihaz performansı ölçülmedi.

- `csi Tools/check_gel_channel_exclusion.csx`:38 gerçek üretim metodu kontrolü; kanal/depo, mantıksal ID kaldırılması sonrası kalıcı üyelik, tüm board boya girişleri, sayaç/olay/visual üretmeme, normal komşu ve mühürlü seed, ClearAll, çoklu yol, geçersiz yol ve yeni levelde yeniden kullanım.
- `csi Tools/check_pulsepulse_gel.csx`:68 eski kontrol/72 güncel kontrol geçti; önceki Pulse–Pulse jel düzeltmesi korundu.
-1.898 kaynak/Settings dosyasından yalnız gel servisi ve spawner Init çağrısı değişti; diğer1.896 dosya ve level yerleşimleri korundu. Syntax ve ilgili `git diff --check` geçti.

Kontroller doubles ile izole üretim metotlarını çalıştırır; Unity Play/build, gerçek renderer, bot/kaskat simülasyonu veya cihaz doğrulaması yapılmadı.

Tasarım sonucu: kanallı bir levelde tam-board jel hedefi hazırlanırken kanal yolunun/deponun hücreleri kaplanabilir alana dahil edilmemeli. Bu tur level hedefleri/assetleri değiştirilmedi; mevcut katalog taramasında hem doğrudan jel hedefi hem authored waterChannels içeren kayıt bulunmadı.
