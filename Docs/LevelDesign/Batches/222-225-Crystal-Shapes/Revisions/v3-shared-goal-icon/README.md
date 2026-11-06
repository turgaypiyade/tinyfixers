# Bütün elmasların ortak mor hedef ikonu · 2026-11-05

Kullanıcı, yeşil hedef ikonu görünürken sarı elmasın da sayılmasının kafa karıştırdığını bildirdi ve sağladığı `modimerdal.png` dosyasının Goals/PreLevel ortak sembolü olmasını istedi.

PNG pikselleri aynen `DimerdalDiamondGoal.png` olarak alındı; saydam kenarlar Unity sprite rect ile ayarlandı. Dimerdal 84–99 tanımlarında yalnız `goalIconSprite` mor sembole geçti. Aktif V1 katalogdan çözülen 221, 222, 224 ve 225'te renkli Diamond `iconOverride` aynı mor sprite ile değişti. Hedef miktarları 8/10/12/16, bütün diğer hedefler, hamleler, yerleşimler, katalog ve isimler korundu. Varsayılan HUD/PreLevel/sonuç çözümü kütüphane hedef ikonunu, mevcut seviyeler explicit override'u kullanır; her iki yol da aynı mora çıkar. Runtime UI kodu değişmedi.

Tahta, kutu içi ve üretim/toplama uçuşu kendi renkli ürün görselini kullanmaya devam eder. Ortak hedef ikonu ayrı bir sprite'tır. Ek tarama, coroutine, material veya efekt yoktur; mevcut sprite referansı çizim yolu kullanılır. CPU/GC/FPS ölçülmedi.

`Before/` beş değiştirilen assetin ve batch modellerinin önceki halini saklar. `apply.py` yalnız izin verilen ikon satırlarını yazar, başka bir mevcut değişiklik gördüğünde durur. `verify.py` tam dosya karşılaştırmasıyla yalnız bu satırları ve diğer 3035 asset/kod dosyasının korunmasını doğrular. Yeni kaynak PNG hash'i, metadata YAML ve UI ikon çözüm yolları kontrol edildi. Önceki kullanıcı Level11 değişikliği bu turun baseline'ında yer alır ve korunur.

`Ortak-Elmas-Hedefi.png` ve mevcut level önizlemeleri güncel gerçek sprite'larla üretildi; açılıp kontrol edildi. Bunlar statik önizlemedir. Oynanış, Unity Play/build, cihaz, bot/gravity/cascade simülasyonu veya yayın yapılmadı.

Doğrulama: `python3 Docs/LevelDesign/Batches/222-225-Crystal-Shapes/verify.py`.
