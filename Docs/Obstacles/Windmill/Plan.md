# Yel Değirmeni + Un Çuvalı + Un — tasarım kararları (2026-11-01, kullanıcı)

## Görseller (onaylı)
- Değirmen: `Windmill_Concept_Approved.png` (taş örme gövde, kahve kiremit çatı, çelik göbek, 4 parlak kanat).
  Oyun parçaları `parts/` — Body (ayrı ürettirilen kanatsız gövde, `Windmill_Body_Source.png`), Sail_B/Y/R/G, Hub (en üst).
  Ayırma betiği `split_parts.py` (renk tonu + çeyrek kısıtı + çerçeve = panel genişletmesi ∩ kahve/altın olmayan).
  Not: kanat uçları taban çelik halkasından küçük parça alıyor — oyuna alırken temizle.
- Un çuvalı 1×2: yatay görsel onaylı (`FlourSack_Horizontal.png`); dikey = yatayın 90° çevrilmişi
  (kullanıcı: ayrı dikey üretim 2:3 oranıyla küçük kaldı, çevrilmiş daha iyi).
- Un örtüsü: çamur örtüsü yapısında (kenar/köşe) beyaz-krem un dokusu — Claude üretecek.

## Kurallar
- Değirmen 2×2. 4 kanat: mavi, sarı, kırmızı, yeşil; kanatlar 2×2'nin dört köşesine bakar (X dizilim).
- **Her zaman döner** (dönmeyen versiyon yok): her oyuncu hamlesinden sonra kanatlar saat yönünde bir köşe ilerler.
- Kanat yalnız **baktığı köşe hücresinin dış komşularında kendi renginde eşleşme** ile kırılır (ColorChest renk kuralı +
  köşe kısıtı). Special değirmenin hangi hücresine çarparsa o köşedeki kanadı kırar.
- Kırılan kanat kendi görselinden 2–3 büyük parçaya ayrılıp savrulur (ObstacleShatterFx kuralı).
- 4 kanat bitince değirmen **1 un çuvalı** fırlatır (editörde "değirmen başına çuval sayısı", varsayılan 1).
- Çuval 1×2, yatay ya da dikey; **rastgele, normal taş olan** uygun 1×2 alana düşer.
- Çuval 1 vuruşla patlar → **5×5** alana un yayar (çuvalın 1×2'sini kapsayan 5×5).
- Un **her zaman en alt katman** (çamur gibi), taşın altında; üstünde eşleşmeyle temizlenir.
  Un yalnız alt katmanı boş ve sabit engel olmayan hücrelere yayılır (çamur/jel/yağ/su olan hücreye yayılmaz).
- Değirmenin **üstüne hiçbir şey konmaz**; ileride **altına** çamur vb. konabilir (diğer 2×2 katman sistemiyle).

## Açık
- Hedef: kaynak+ürün kuralı (jel atıcı + jel gibi) → değirmen mi, un mu, ikisi mi hedef?
- Sesler: gıcırtı/dönme, kanat kırılması, çuval düşüş, un püskürmesi (kullanıcı dosya verecek).
- 206 katalog girişi henüz yok.

## Uygulama (2026-11-01, kod — Unity'de oynanmadı)
- `LevelData`: Windmill 80, FlourSack 81 (2×1), FlourSackV 82 (1×2), Flour 83. Library girdileri + sprite'lar
  `Art/UI/Obstacles/Windmill/` (gövde/4 kanat/göbek/önizleme, çuval yatay+dikey, un karosu — un karosu ilk sürüm, sade).
- `Grid/Windmill/WindmillService.cs`: kural (köşe+renk, LampStrip sözleşmesi, duvar ailesi hücre başı vuruş), hamle
  sonu saat yönü dönüş (0.42 sn, hafif aşma), kırılan kanat savrulur, yıkılınca gövde ObstacleShatterFx; çuval verisi
  yıkıldığı anda `ObstacleStateService.TrySpawnFlourSack` ile yazılır, 0.55 sn yay uçuşundan sonra görünüm çizilir
  (ObstacleSpread job). Uygun 1×2 yoksa un doğrudan değirmen alanına. Çuval patlayınca 5×5 (çuvalın ilk hücresi merkez)
  engelsiz hücrelere un.
- Hedef: `TopHudController` Flour'u çamur/su birikintisi gibi leke hedefi sayar + ayakta değirmen ve çuval başına 25
  beklenen un (değirmen yıkılmadan hedef 0'a düşüp erken kazanma olmasın).
- Entegrasyon: ObstacleStateService (IsWallFamily, WindmillHitInterceptor, IsFlourAt, pasif alt katman), GridSpawner
  (DrawWindmills, generic çizim/geri yükleme atlaması, ipucu kökleri), PatchBot temsilci köşesi, BoardBreakFx (değirmen
  genel parçacık yok), Sim (NotSimulated), editör (un üst katman olamaz), Hint 80/83 + TR/EN.
- Sesler (kullanıcı dosyaları, mono + seviye dengeli, `Audio/SFX/ObstacleSounds/Windmill/`): kanat kırılma=Windmill_SailBreak,
  yıkılma=Windmill_Collapse, dönme=Windmill_SailRotate (releaseSound, 0.8), fırlatma=FlourSack_Throw (flightLoopSound, tek
  sefer), iniş=FlourSack_Land (impactSound), patlama=FlourSack_Burst (breakSound). Eski geçici not:
  çuval inişi (FlourSack.impactSound) boş. Kullanıcı dosyaları gelince kütüphanede değişecek.
- Bilinen sınırlar: kanat kırılması tek parça savrulma (2–3 parça kuralı henüz değil); un karosu sade; çuval uçarken
  altındaki taşlar kilitli görünür.
- 206 Değirmen Tepesi: 8×10 tepe silüeti, tek değirmen (3,2), 25 hamle, hedef Un 25. `Batches/206-210-Windmill/`.

## Revizyon (2026-11-01, kullanıcı oyunu sonrası)
"değirmen beklerken dönebilir mi? kanatlar sadece special ile kırılsın.. bu durumda sıralama önemli değil.. goals'te un
çuvalını ya da değirmeni gösterelim, un belirgin değil."
- Renk/köşe kuralı ve hamle sonu çeyrek dönüş KALDIRILDI. Kanatlar yalnız special/joker ile kırılır; special'ın çarptığı
  köşeye o an en yakın duran kanat kırılır.
- Bekleme dönüşü: 28°/sn saat yönü, değirmenler rastgele fazda. Maliyet: her değirmen kendi alt Canvas'ında (yalnız 6
  görsel yeniden kurulur, tahta Canvas'ı değil); servis değirmen ayakta olduğu sürece açık.
- Un hedef ikonu: dikey çuvaldan kare `FlourGoalIcon.png` (hedef + ipucu 83).
- 2026-11-01 cihaz/editör testi: değirmen GÖRÜNMEDİ. Kök: değirmen köküne AddComponent<Canvas>() ile eklenen alt Canvas
  (tahta Canvas'ı Screen Space-Camera, Pixel Perfect açık, TilesRoot'ta stencil maske). Alt Canvas kapatılınca (UseSubCanvas
  = false) göründü; [Windmill] sprite hatası yoktu. Ders: tahta içinde alt Canvas eklemek görünürlüğü bozabiliyor →
  Canvas katman ayrımı çalışmasında her katman tek tek doğrulanmalı.
- 2026-11-01 kullanıcı: "çuvalın uçup yere düşüşü çok görünür değil; parçalanmada da animasyon olsun". Uçuş artık en üst efekt
  katmanında (VfxRoot), 0,85 sn, 3,2 hücre yay, değirmenden 0→1,3 büyüyerek çıkar, tam tur takla; iniş yerinde koyulaşan
  gölge; inişte yassılaş-toparlan esnemesi (0,3 sn) + 8 toz bulutçuğu + güçlü mikro sarsıntı. Patlama: çuval kendi görselinden
  2–3 parça (ObstacleShatterFx, görünüm iniş anında saklanır) + 5×5'e yayılan un bulutu (13 bulutçuk); çuvalda genel kırılma
  parçacığı kapalı. Bulutçuklar tek seferlik oluştur/yok et (değirmen başına birkaç kez; havuz gerekmedi).
- 2026-11-01 kullanıcı: "kanatların dönüşünde ses yok". Her oyuncu hamlesi sonunda rüzgâr esintisi: kanatlar 0,7 sn hızlanır (9× → 1×, yumuşak sönüm) ve Windmill_SailRotate bir kez çalar (sürekli döngü sesi bilerek yok).
- 2026-11-01 kullanıcı: "yel değirmenine göre toplam unu göstermek en iyisi; hiç düşmemişse bile kullanıcı düşeceğini bilmeli"
  (eski barrel→mud örneğinde önce kaynak gösterilip sonra ürün çıkıyordu, hedef alanından yiyordu). Yeni kural: çuval başına
  un = hedefteki Un ÷ başlangıç değirmen sayısı (`WindmillService.FlourPerSack`); çuval TAM o kadar döker (merkeze en yakın
  boş hücreler: önce 5×5, gerekirse dışa halka). Sayaç = canlı un + (ayakta değirmen + çuval) × FlourPerSack → baştan toplam,
  yalnız temizledikçe azalır; tahtada yer yoksa dökülebildiği kadar.
- 2026-11-01 kullanıcı oyunu (206): un kanalın üstüne sıçradı → önce un temizlendi, sonra kanal açıldı; kullanıcı: "tam istediğim gibi oldu". KURAL: un, açılmamış su kanalının ÜSTÜNE gelebilir (kanal alt katmanda kalır); bu hata değil, korunacak.
