# ProLevels V1 — 11–15 / v2

2026-10-28. İlk 12–15 taslakları atraksiyon ve level fikri eksikliği nedeniyle kullanıcı
tarafından reddedildi. Asset/meta/PNG/not ve araçları `Revisions/v1/` içinde saklandı.
Bu dosya ve klasörün kökündeki PNG'ler **yeni v2 taslağını** anlatır; kullanıcı onayı yoktur.
**11 / Yağlı Rampa korundu. Yalnız 12–15 yeniden tasarlandı; 16'ya geçilmedi.**

| Level | İsim | Hamle | Hedefler | Açılış fikri |
|---|---|---:|---|---|
| 11 | Yağlı Rampa | 30, korundu | 23 sandık1 + 36 yağ | Mevcut kullanıcı leveli |
| 12 | Açık Avlu | 31 | 19 sandık1 + 28 çamur | Pencere arkasındaki kapağı aç, alt avlu dolmaya başlasın |
| 13 | Katmanlı Geçit | 30 | 9 sandık1 + 9 sandık2 + 18 çamur | Sol odayı aç, uzun çalışma kanadından sağ odaya vur |
| 14 | Yağlı Köşe | 29 | 12 sandık2 + 6 sandık1 + 12 başlangıç yağı | Z biçimindeki iki alanı yağlı geçişten birleştir |
| 15 | Üç Katlı Bent | 30 | 6'şar sandık1/2/3 + 24 çamur | Combo ile ilk bendi aç, diğer iki hazneyi sırayla çöz |

12–14 Normal hedef geçiş %55–85; 15 Zor hedef %35–55. Bunlar ölçülmüş sonuç değil,
tasarım hedefidir. Hamleler oynanışla ayarlanacak ilk değerlerdir. TR/EN isimler
korundu: Open Courtyard, Layered Passage, Oily Corner, Three-Tier Dam.

## Kompozisyon ve oyuncu kararı

- **12:** 9×10 / 74 aktif. Ortadaki 3×2 pencereyi sandık konturu çevreler.
  Üstündeki dikey roket boşluktan geçerek alt kapağı vurur. Kapak açılınca alt avluya
  dolum rotası oluşur; oyuncu geçidi genişletip çamuru temizler. Alt 28 çamur hücresi
  başlangıçta taşsızdır. İlk roket bütün avluyu doldurmuş veya temizlemiş sayılmaz.
- **13:** 9×9 / 63 aktif, dört odalı H silueti. Alt sol oda tek-hit, alt sağ oda
  iki-hit sandıklıdır; ikisinin altında da çamur vardır. Sol oda açılınca üstüne bağlı
  tam boy 3×9 çalışma kanadı kazanılır. Burada üretilen yatay roket alt orta boşluğu
  geçerek sağ odaya ulaşabilir. Hazır special yoktur.
- **14:** 9×10 / 63 aktif, kaymış iki alanı bağlayan Z silueti. Sol kenarda iki tam
  sütun iki-hit sandık, sağ altta tek-hit sandık grubu, arada 3×4 yağ. Hazır yatay roket
  geçiş sırasındaki yağı temizleyerek normal komşulukta iki alanı birleştirir.
  Yağa isabet o hamlenin yayılmasını bastırır; vurulmazsa mevcut servis bir hücre
  yayabilir. Başlangıç 12 yağ, tavan 18; HUD hedefi yayılmayla büyüyebilir.
- **15:** 9×11 / 75 aktif. Sağa-sola kayan üç haznenin girişinde altışar sandık:
  önce bir, sonra iki, sonra üç-hit. Hazır Pulse + dikey roket birleşimi mevcut
  3 sıra + 3 sütun etkisiyle ilk bendi açar. Alt bentleri zayıflatır ancak bitirmez;
  ikinci ve üçüncü haznede oyun devam eder. Sandıkların altında 18, son zeminde 6
  çamur vardır. Başlangıçta alt haznelerin 30 serbest hücresi boştur.

Yalnız izinli 4/5/6/14/25 engelleri kullanıldı; yeni engel tanıtımı yoktur. Çamur
alt tabanda, katı örtüler `stackedObstacles` üst katmanındadır. Hazır special türleri
önceki V1 levellarında tanıtılmıştır. Sabit normal taş rengi ve ek refill kotası yoktur;
dört temel renk ve mevcut otomatik `refillSpecialGuard=-1` korunur.

## Doğrulama ve sınırlar

`python3 -B Docs/LevelDesign/Batches/ProLevels-V1-011-015/verify.py` gerçek asset
maskelerini, dizilerini, katmanları, hedefleri, pinleri, GUID/isim/katalog bağlantısını
ve koruma kayıtlarını denetler. Sonuç `validation.json` içindedir.

Başlangıç dolumu mevcut gravity kurallarına göre statik erişim grafıyla incelendi.
Hazır special konumları başlangıç yerçekiminde düşmez. Belirtilen kapak/oda açılınca
12'de 27→50, 13'te 45→54, 15'te 27→45 hücre üst dolumuna bağlanır. 14'te serbest
hücrelerin komşuluk bileşeni 2→1 olur. Bütün örtüler kaldırıldığında kalıcı dolum
tuzağı yoktur. Bunlar gerçek vuruş, kaskat veya oynanış simülasyonu değildir.

189 tarihsel katalog board'u benzerlik adayları için tarandı. Yakın örnekler
`Repetition-v2.png` üzerinde incelendi; skorlar benzersizlik veya kalite yüzdesi değildir.
`render_preview.py` gerçek asset ve mevcut proje sprite'larından önizleme üretir.
Taş renkleri temsilidir; boş başlangıç hücreleri dolu gösterilmez. Detaylardaki ikinci
maske belirtilen açılmanın statik bağlantı kontrolüdür; oynanmış ekran değildir.

Unity arayüzü, Play/build, bot veya kaskat simülasyonu çalıştırılmadı. Cihaz FPS'i ve
geçiş oranları ölçülmedi. Yeni runtime/VFX/material/efekt havuzu kodu eklenmedi.
En büyük yeni maske 9×11, 75 aktif hücredir. Mevcut yağ/çamur servislerinin maliyetleri
devam eder; 15'in combo etkisi mevcut efektleri kullanır. Kod eklenmemesi FPS garantisi
değildir; combo anı cihazda ayrıca gözlenmelidir.

## Dosyalar ve koruma

Güncel assetler `Assets/_Project/Settings/ProLevels/LevelV1_00000120.asset` …
`LevelV1_00000150.asset`. V2 yalnız bu dört levelin içeriğini değiştirir.
Katalog referansları, GUID'ler ve isimler ilk taslaktaki gibi kalır.
11'in asseti `Before/` kopyasıyla birebir aynıdır.

`protected-v2-before.json` içindeki 3.007 mevcut ayar/kod/Resources/sahne/prefab/shader
dosyası korunmuştur. İlk taslaktan sonra dışarıdan değişmiş bulunan mevcut
`LevelV1_00000040.asset` geri alınmadı; fark `external-changes-before-v2.json` içinde
kaydedildi ve v2 koruma kaydında güncel hali esas alındı. Eski katalog tarihsel
referanstır; ona level yazılmadı. Yayınlama veya release baseline oluşturma yapılmadı.
