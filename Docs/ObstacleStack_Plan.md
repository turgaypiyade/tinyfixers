# Engel Yığını (Obstacle Stack) Planı

2026-09-26. Kullanıcı: "Overlay yaptığımız obstacle'larda genel bir sorun var. Tüm obstacle'ların üzerine obstacle
gelebilir; stack mantığı yapıp önce üstteki tüketilip sonra alttakine geçmeli."
Tetikleyen: L61 kargo+grass hatası (hasar kodu kargo'da "kırılmaz" deyip çıktı, grass'a hiç ulaşmadı).

## 1. Bugünkü model — üç ayrı mekanizma

| Mekanizma | Ne tutar | Sorun |
|---|---|---|
| `level.obstacles[]` + `obstacleOrigins[]` | Hücre başına TEK "birincil" engel | Hasar ve sorguların çoğu yalnız buna bakar (servis içinde 54, dışarıda 9 dosya) |
| `_stampedBeneathByCell` | Gerçek bir liste-yığın: editörde üst üste konanlar, Safe altı, mıknatıs | Yalnız bazı sorgular (IsOverTileBlockerAt) içine bakar; açığa çıkarma yolu birden çok (Clear, Safe reveal, magnet retire, tube free) |
| `_underTileBeneathMovable` | Tek yuva: hareketli engel girince alttakini saklar | Grass/Oil ekranda ÜSTTE, veride ALTTA → istisna kodları (TryConsumeCoveringOverlayHit), kargo hatası |

Ek özel durumlar: Safe (NxN kapak, kendi reveal'ı), Magnet (yol hücreleri stamp'lenir, uçlar "açık/kapalı"),
Tube (hücre hücre boşalır), çok-hücreli engeller (origin tekrar vuruş engeli), aşamaya göre davranış değiştiren
engeller (behaviorByStage).

## 2. Hedef model — hücre başına TEK yığın

**Kural 1 — Yığın sırası = çizim sırası = vuruş sırası.** Her hücrenin alttan üste katman listesi var. Ekranda
en üstte görünen, veride de en üsttedir ve vuruşu o alır. Tükenince yığından çıkar, bir alttaki açığa çıkar.
Ters dizilim (görsel üst / veri alt) yasak → istisna kodları silinir.

**Kural 2 — Sıra SERBEST (kullanıcı kararı 2026-09-26).** Yığının sırası editörde nasıl dizildiyse odur:
Safe üstünde Grass da, Grass üstünde Safe de; 4 sandık altında Safe de, Safe altında sandık da olur. Tek kısıt:
**zemin altı engeller (Mud, SpreadingGel) daima en altta** (başka bir şeyin üstünde olamaz).
Oyun içi DİNAMİK girişler: (a) hareketli engel (Plastic/Helmet/Cargo/GoldMoney) bir hücreye düşünce zemin altı
katmanların hemen üstüne, hücreye bağlı örtülerin (Grass/Oil) altına girer — yani taşın yerine; (b) Barrel'ın
saçtığı Mud yığının en altına girer.

**Kural 3 — Vuruş yalnız en üste; alttaki YOK hükmünde (kullanıcı onayı).** Üstteki gidene kadar alttaki ne hasar
alır ne de özelliği işler. En üst katman bu vuruş türünü kabul etmiyorsa (ör. SpecialOnly taşa normal eşleşme)
vuruş BOŞA gider, alta SIZMAZ. Kırılmaz katman (Cargo) vuruşu yutar; üstünde örtü varsa önce örtü gider.

**Kural 4 — Hücre özellikleri tüm yığından türetilir.** "Taşı düşürür mü, taşı tutar mı, dokunulabilir mi, special
etkiler mi" gibi sorular yalnız birincile değil, katman sınıflarının birleşimine bakar (tek fonksiyon).

**Kural 5 — Çok-hücreli engel** her hücresinde aynı origin'li bir katmandır; bir vuruş kaynağı origin başına bir kez
vurur (bugünkü kural korunur).

**Kural 6 — Yığın başına en fazla 1 hareketli engel** (mevcut Movable Stack Kuralı korunur).

## 3. Geçiş fazları (her faz ayrı test; davranış bozulmadan)

- **F1 — Veri modeli:** `CellStack` (katman listesi) tek doğruluk kaynağı olur; `obstacles[]` yalnız "en üst
  katmanın aynası" olarak kalır → mevcut 60+ okuma noktası çalışmaya devam eder. Üç mekanizma bu yığına taşınır;
  tek `Push/Pop/Top/Layers` API'si.
- **F2 — Vuruş:** tek yol: en üst katman → tüket → pop → açığa çıkan katmanı bildir. Özel dallar (Safe/Magnet/
  Tube/Cargo) yığın katmanı olarak ifade edilir.
- **F3 — Hareket:** hareketli engel yığındaki kendi katmanını taşır; sınıfına göre doğru yere yerleşir
  (_underTileBeneathMovable silinir).
- **F4 — Sorgular:** blocks/holds/locked/affectable tek türetme fonksiyonundan (Kural 4).
- **F5 — Görsel:** her katmanın view'ı yığın sırasıyla çizilir (sort order = katman sırası); GridSpawner'daki
  "beneath view" ve örtü istisnaları kalkar.
- **F6 — Temizlik:** TryConsumeCoveringOverlayHit, kargo özel dalı, magnet retire, çift reveal yolları silinir.

## 4. Regresyon seti

Safe + içerik · Chest/Wardrobe altında mud · Grass üstünde/altında magnet · Plastic/Helmet'in mud/grass/oil'e
düşmesi · Kargonun grass/mud'dan geçmesi · Barrel mud'unun blocker altına düşmesi · Grass altında KeyGenerator ·
Tube · Magnet uçları · Jel yayılımı · Yağ yayılımı · Hedef sayımları (mud/grass/oil) · Level editörü Overlay modu.

## 5. Kullanıcı kararları (2026-09-26, KİLİTLİ)

1. Sabit sınıf sırası YOK — editördeki dizilim geçerli; yalnız Mud/Jel en altta.
2. Alttaki, üstteki gidene kadar yok hükmünde (hasar almaz, özelliği işlemez).
3. Editör serbest (Mud/Jel kısıtı hariç).
