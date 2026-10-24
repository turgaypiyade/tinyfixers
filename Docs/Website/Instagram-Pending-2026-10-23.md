# Instagram Reels — tamamlanan yayın

Kullanıcının açık yayın izniyle bağlantı tekrar denendi ve erişim açıldı. `wondigames` BUSINESS hesabı doğrulandı; son beş paylaşım kontrol edildi, son caption’sız videonun küçük resmi indirildi ve Seviye 114 ile farklı olduğu görüldü. 23 saniyelik Reels yayımlandı; API üzerinden permalink ve Türkçe/İngilizce açıklaması doğrulandı.

Yayın: https://www.instagram.com/reel/De2RrspGIU-/

Kayıtlı `~/.wondi-instagram.env` mevcut. Değeri kaydedilmedi/yazdırılmadı. Mevcut `Website/social/ig.py` aracının hesap doğrulama isteği (`graph.instagram.com/v23.0/me`) Instagram'dan HTTP 400, OAuthException code 200, `API access blocked.` döndürdü. Bu yanıttan tokenın süresinin dolduğu veya uygulamanın tam olarak neden engellendiği çıkarılamaz. Bu ilk hata, kullanıcının sonraki yeniden deneme isteğinde artık oluşmadı; hesap erişimi ve mükerrer kontrolü yayın öncesinde tamamlandı.

Hazır aday: `Website/social/reels/reel_L114.mp4` (23 saniye, 1080×1920, H.264/AAC, 60 fps). Türkçe/İngilizce açıklaması `Website/social/reels/captions/L114.txt` içinde güncellendi. Videodan 3 saniye aralıklarla sekiz kare incelendi; açılış, oynanış, patlamalar ve sonuç ekranı görüldü. Dosya mevcut web yayınının `/social/reels/reel_L114.mp4` yolunda bulunuyor.

Yayın kaydı: `Instagram-Publication-2026-10-23.json`. Container FINISHED olduktan sonra bir kez media_publish çağrıldı. Sonraki çalışmalarda tekrar paylaşma; önce bu kayıt ve hesap medyası kontrol edilmeli.

Tawk.to notu: yayınlanan İngilizce/Türkçe HTML'de mevcut embed URL ve hesap kimliği korundu. Yalnız localhost gibi önizleme alanlarında yüklenmiyor; gerçek wondigames.com alanlarında yüklenmesi için kod mevcut. Canlı sohbet/ticket işlemi bu çalışmada oluşturulmadı.
