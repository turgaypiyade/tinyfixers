# ProLevels V1 — 11–15 grubu

2026-10-28. Kullanıcının "sadece11–15 yapmanı istiyorum" devam isteğiyle hazırlandı.
**11 / Yağlı Rampa mevcut haliyle korundu; boş12–15 tasarlandı ve V1 kataloğuna bağlandı.**
Yeni bir11 tasarımı veya11'in dengesi için kullanıcı onayı varsayılmadı. 16 ve sonrası yapılmadı.

| Level | TR / EN isim | Hamle | Hedefler | Açık başlangıç taşı |
|---|---|---:|---|---:|
| 11 | Yağlı Rampa / Slippery Ramp | 30 (korundu) | 23 sandık1 +36 yağ (korundu) | 21 örtüsüz hücre |
| 12 | Açık Avlu / Open Courtyard | 29 | 17 sandık1 +21 çamur | 64 |
| 13 | Katmanlı Geçit / Layered Passage | 30 | 5 sandık1 +7 sandık2 +21 çamur | 69 |
| 14 | Yağlı Köşe / Oily Corner | 28 | 7 sandık1 +9 sandık2 +8 başlangıç yağı | 57 |
| 15 | Üç Katlı Bent / Three-Tier Dam | 29 | 9'ar sandık1/2/3 +27 çamur | 54 |

12–14 Normal için hedef geçiş %55–85;15 Zor için %35–55. Bunlar **tasarım hedefidir**;
hamleler ilk denge ayarıdır, gerçek geçiş oranları ölçülmedi.

## Kompozisyon ve karar

- **12 / Açık Avlu:** Tam9×9, üstte4 sıra tamamen açık. AltU konturu17 tek-hit sandıktır;
  içindeki7×3 çamura doğrudan erişilir. Yağ tanıtımından sonra yayılma baskısı olmadan
  bildik eşleşme/komşu hasarı pekiştirilir. Çamur sandık altında değildir.
- **13 / Katmanlı Geçit:** Tam9×9, üstten başlayan iki geniş çalışma alanı. Orta-sol7 hücrelik
  dikey iki-hit bölme ve sağ alt5 tek-hit sandık tekL kütlesi oluşturur.12 sandığın altında
  çamur; sağda9 açık çamur vardır. Bölme açılınca hedef işi bitmez, alt zemine devam edilir.
  Normal çalışma alanlarının tamamı baştan dolum alır; kapalı boş cep ve pin çözümü yok.
- **14 / Yağlı Köşe:** Tam9×9,57 serbest başlangıç hücresi. Sağ alttaki2×4 yağ cebi tek-hit
  kapak/sol dirsek ile iki-hit alt sıranın oluşturduğu tek bağlı konturun içindedir.
  Başlangıç8 yağ, `oilSpreadMaxCells=14`. Önce ince kapağı aç, sonra yağa düzenli isabetle
  yayılmayı bastır. Mevcut serviste bir hamlede **herhangi bir yağ isabeti bütün yayılmayı
  durdurur**; vurulmazsa stabilizasyon sonrası yalnız1 hücre yayılır. Yağ hedefi HUD'da
  yayılan hücrelerle dinamik büyüyebilir; statik8 sayısı bütün oyun boyunca sabit sanılmaz.
- **15 / Üç Katlı Bent:** Tam9×9, üstte6 sıra/54 taş kesintisiz geniş alan. Alt3 sıra
  önden arkaya1→2→3 vuruşlu sandık ve hepsinin altında27 çamur.27 sandığın ilk toplam
  vuruş yükü54, çamurun54; toplam108 hücre-vuruşu. Bu **hamle sayısı hesabı değildir**:
  tek eşleşme/special birden fazla hedefi vurabilir. Zorluk27 hücrelik örtü sonrası kalan
  işten gelir; yeni karmaşık mekanik, dar boğaz veya düşük hamle şoku eklenmedi.

Yalnız4/5/6/14/25 kullanıldı. Yeni engel tanıtımı, hazır special, sabit taş rengi veya
refill kotası yok; `randomPool` dört temel renk, `refillSpecialGuard=-1` mevcut otomatik
kurala bırakıldı. Zorluk koşullarını değiştirmek için global yardım/special kodu düzenlenmedi.
13 ve15'te **iki authored katman** vardır: altta çamur, üstte sandık. Üç-hit sandık üç
ayrı authored katman anlamına gelmez. Hepsi1×1 engeldir.

## Dosyalar ve koruma

Yeni assetler `Assets/_Project/Settings/ProLevels/LevelV1_00000120.asset` … `00000150.asset`.
`LevelCatalogProV1.asset` içinde yalnız12–15'in boş referansları dolduruldu; sıra/anahtarlar ve
diğer kayıtlar aynı. `LevelNames.json`'a dört TR/EN kayıt eklendi; önceki kayıtlar birebir aynı.
`Docs/LevelDesign/Level-Names.md` ile isimler eşit.

`Before/` mevcut11, katalog, isimler ve çalışma notunu içerir.
`protected-before.json`: önceden var olan3.007 Settings/Scripts/Resources/Scenes/Prefabs/Shader
dosyasının hash'i. Yeni assetler dışında bu dosyalar değişmedi. Katalog ve isimler ayrı
semantik kontrollerle doğrulandı;11'in asseti ayrıca birebir karşılaştırıldı.
Genel `git diff` daha önceki kullanıcı değişikliklerini de içerir; bu grubun farkı bu snapshot'a göredir.

## Doğrulama

- `python3 -B .../verify.py`: gerçek asset dizileri,81 hücrelik maskeler,origin'ler, katmanlar,
  hedef adetleri, dört renk, pin/special yokluğu, yağ tavanı, GUID/katalog ve TR/EN geçti.
- Normal açık hücreler için mevcut `CascadeLogic.ComputeGravityReachableMask` kuralına
  uygun statik aşağı/diyagonal erişim grafiği kontrol edildi:64/69/57/54 hücrenin hepsi
  başlangıç dolumuna bağlı. Bu, taş/kaskat/oynanış simülasyonu değildir.
- Her yeni levelin sandık kütlesi dört-yönlü tek bağlı kompozisyondur.1–11'e ait inceleme
  görseliyle karşılaştırıldı.12altU,13ortaL,14kapalıyağköşesi,15yataykademelibent;
  eskiV/kase ve basamakların doğrudan kopyası yapılmadı. Tarihte ilk kez kullanılan
  şekiller oldukları veya bütün eski katalogdan farklı oldukları iddia edilmez.
- `render_preview.py` gerçek assetlerden/proje sprite'larından dört detay ve bir grup
  görseli üretir. Bütün PNG'ler görsel olarak incelendi. Taş renkleri temsili;
  bunlar Unity ekran görüntüsü veya gerçek açılış RNG sonucu değildir.
- Unity arayüzü, Play/build, bot veya kaskat simülasyonu çalıştırılmadı. Gerçek cihaz FPS'i
  ve geçiş oranları ölçülmedi. Runtime/VFX/köşe/yuvarlatma/library/sahne/prefab kodu değişmedi.

## Performans kapsamı

Normal asset tasarımıdır; yeni per-frame kod, board taraması, materyal veya efekt sistemi
eklenmedi. En büyük yeni açık board81 hücre; çamur en fazla27 (mevcut6'da49), yeni yağ
başlangıcı8 ve tavan14 (korunan11'de başlangıç36). Mevcut mud/oil renderer'larının kurulma
maliyeti ve Oil yayılmasının mevcut liste/HashSet oluşturması devam eder; bu tasarımlarla
ortadan kalkmış sayılmaz. Mevcut efekt havuzları/iptal/scene teardown yolları değiştirilmedi.
Kod eklenmemesi ölçülmüş performans garantisi değildir.

İnceleme taslağıdır; oynanış sonrası kullanıcı geri bildirimi bekleniyor.16 tasarlanmadı.
