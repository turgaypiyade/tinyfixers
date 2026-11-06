# Dimerdal elmas görselleri · 2026-11-05

Kullanıcının verdiği `bluedimerdal.png`, `greendimerdal.png`, `reddimerdal.png` ve `yellowdimerdal.png` dosyaları mevcut dört `DimerdalDiamond*.png` assetinin yerine birebir kopyalandı. PNG pikselleri değiştirilmedi. Saydam kenar boşlukları için Unity sprite dikdörtgenleri yeni çizime göre güncellendi; GUID, spriteID, fileID, pivot ve import/kompresyon ayarları korundu. Yeni texture veya material eklenmedi.

Tahtadaki elmas, kutu içindeki elmas ve üretim uçuşu aynı sprite referansını kullanmaya devam eder. On iki kutu boyut/renk varyantının `auxiliarySprites[1]` ve dört board elmasının sprite referansı kontrol edildi. Gerçek sprite'larla hazırlanan `Yeni-Elmaslar-Onizleme.png` açılıp incelendi; Unity ekran görüntüsü değildir.

Önceki dört PNG ve dört `.meta`, önceki `assets.json` ve SHA-256 manifesti proje dışında saklanır:

`/Users/turgayp/Desktop/Wondi Paylaşım/codex/Dimerdal-Elmas-Yedek-2026-11-05`

Yedek, assetler değiştirilmeden önce hash ile doğrulandı. `replacement.json` yeni kaynakların yollarını/hash'lerini, sprite dikdörtgenlerini ve korunan kimlikleri kaydeder. Çerçeveler, kepenkler, runtime kodu ve level dosyaları aynı kaldı. YAML ve referans kontrolü geçti. Unity build/Play veya cihaz testi yapılmadı.
