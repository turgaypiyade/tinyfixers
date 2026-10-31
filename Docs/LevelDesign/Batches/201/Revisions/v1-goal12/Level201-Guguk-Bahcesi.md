# 201 · Guguk Bahçesi / Cuckoo Garden

[Yerleşim önizlemesi](Batches/201/Level201-Guguk-Bahcesi.png) ·
[Level asseti](../../Assets/_Project/Settings/ProLevels/LevelV1_00002010.asset) ·
[Statik doğrulama](Batches/201/validation.json)

Yeni BirdHouse78 ilk kez aktif **ProLevels V1** sırasındaki201'de tanıtılır.
9×10 board,26 hamle,4 renk,88 aktif hücre. Tek hedef **12 kuş**
(`targetType=Collectible`, `collectibleId=Bird/3`); ev kırma hedefi yok.
Katalog anahtarı `LevelCL_0201`, chapter9. TR/EN adları isim JSON ve MD'de kayıtlıdır.

Kullanıcının istediği toplu yerleşim: sağ üstte ve sol altta birer **2 sütun×3 satır**
grup, toplam12 ev. Üst grup üstten1–3. satır/7–8. sütun; alt grup8–10. satır/2–3.
sütun. İki grubun da dış tarafında bir normal taş sütunu bırakıldı. Böylece her evin
bir normal eşleşme yüzü var; üst grubun altında boş kalan kalıcı gölge oluşmuyor.
Sol üst ve sağ alt uçlardaki iki dış köşe hole çapraz yerleşimi tamamlar.
76 normal yuvanın tamamı statik dolum grafına bağlı ve tek eşleşme alanındadır.

İlk karar, hangi grupta kuşları çıkarıp aynı gruba ikinci vuruşu hazırlayacağını
seçmek. Kapak açıldıktan sonraki hamlede isabet gelmezse kuş geri girer. Aynı
zincirde veya bir sonraki hamlede isabet kuşu toplar; ev kalır ve ortak hedef
bitene kadar tekrar üretir. Tek ev birden çok kuş verebilir.3×3 iç evin special
zorunluluğunu ilk tanıtıma taşımamak için2×3 seçildi. Başka engel, örtü, hazır
special veya başlangıç renk pini eklenmedi; geniş alan doğal special üretimine
izin verir. Mevcut otomatik78 hint kaydı ve TR/EN açıklaması korunur.

Önceki200 aktif V1 katalog yeri ve isimler değişmedi. Güncel çalışma alanındaki
3.184 mevcut dosyanın hash'i koruma kaydına göre aynı; yeni BirdHouse library/
servis/art ayarları ve önceki Gift Festival/can/envanter çalışmaları korunuyor.
Bir önceki katalog ve isimler `Batches/201/Before/` altında; çalışma betikleri,
manifest ve koruma listesi aynı klasörde. Bu bir release veya remote yayın değildir.

200 eski V1 girişinin geometry/katman envanteri tarandı. Yerel20/86/150/200
sprite karşılaştırması incelendi: dört köşe/dört üretici/yüksek katmanlı kanat
yerine kullanıcının istediği iki küçük çapraz kuş üretici grubu seçildi. Renkler
temsili; PNG oyun ekran görüntüsü veya gerçek bir hamle sonucu değildir.

Mevcut BirdHouse sunumu tek Update ve kendi flyer havuzunu kullanır;12 ortak kuş
kotası, uçuş rezervasyonlarıyla sınırlanır. Ev görselleri ilk kullanımda kurulur,
flyer havuzu ilk ihtiyaçta büyür; ilk allocation maliyetleri tamamen yok edilmiş
değildir. Kapatmada/teardown'da uçuşların goal bildirimi iptal edilir, bekleyen
board işleri dengelenir ve abonelikler kaldırılır. Runtime/VFX/audio kodu değişmedi.

`python3 Docs/LevelDesign/Batches/201/verify.py --assets` geçti: Unity YAML,
90 elemanlık diziler,1×1 originler,iki altışarlı dikdörtgen grup,collectible goal,
normal komşuluk/refill, GUID,katalog ve isim eşleşmesi,önceki dosyaların korunması.
**Henüz oynanıp onaylanmadı.**26 hamle ilk denge ayarıdır; kazanma oranı/FPS
ölçülmedi. Unity Play/build,bot veya kaskat simülasyonu çalıştırılmadı.
