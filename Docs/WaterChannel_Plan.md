# Su Kanalı — tasarım planı

Durum: **tasarım kilitli, görsel bekliyor** (kural: görsel iyi değilse kodlanmaz). Fikir: kullanıcı, 2026-10-20.

## Oyuncu ne görür

Tahtanın üst kenarında bir **su deposu** var. Depodan tahtanın **alt kenarına** kadar inen, taşların altına
gömülü kapalı bir **kanal yolu** döşenmiş. Kanal hücrelerinin üstündeki taşları eşleştirdikçe o hücreler açılır.
Depoya kesintisiz bağlanan açık hücrelere su akar. Kanalın tamamı açılınca su alt kenardan çıkıp gider: hedef +1.

## Kurallar (kullanıcı kararları)

| Konu | Karar |
|---|---|
| Yol | Seviye editöründe mıknatıs/duvar gibi hücre hücre serbest çizilir (yatay, dikey, köşeli). |
| Kapalı hücre | Taşın ALTINDA durur (çamur/jel gibi alt katman). Üstünde taş olur, oynanır. |
| Açılma | Üstündeki taş eşleşince ya da special vurunca **tek vuruşta** açılır. Üstünde engel varsa (ör. sandık) o kırılınca açılır. Yanındaki eşleşme açmaz. |
| Açık hücre ve taşlar | Taş açık kanalda **durmaz, içinden geçip aşağı düşer** (tahta deliği gibi geçirgen). Altındaki sütunlar beslenmeye devam eder; seviye kilitlenmez. |
| Su akışı | Su yalnız depodan kesintisiz açılmış hücrelere akar. Ortadan açılan parça bağlanana kadar kuru bekler, bağlanınca dolar. |
| Bitiş | Kanalın tamamı açılıp su alt kenara ulaşınca su aşağıdan akıp gider, kanal hedefi tamamlanır (hedef = kanal sayısı). |
| Depo ve çıkış | Depo üst kenarda sabit; kanal ondan başlar, alt kenarda biter. |
| Special | Bir etki = bir vuruş kuralı: special'ın geçtiği her kanal hücresi bir kez açılır. |

## Teknik notlar (kodlamadan önce)

- **Veri:** Mıknatıs gibi yol listesi (`pathCellIndices`) + depo hücresi; çizim aracı mıknatısınkinden uyarlanır.
- **Hücre durumu:** kapalı → açık-kuru → açık-sulu. Açılma, alt katman hasarıyla aynı yoldan (üstündeki taş temizlenince).
- **En riskli kısım — çalışma anında geçirgen hücre:** Tahta delikleri şu an sabit (seviye başında belli). Açılan
  kanal hücresinin oyun sırasında "geçirgen delik" olması; yerçekimi/çapraz kayma (`CascadeLogic`, besleme maskesi),
  eşleşme bulma (`MatchFinder` delikleri eşleşmeye katmaz), spawn kenarı ve özel efektlerin hücre hedeflemesini etkiler.
  Bu kısım ayrı ve dikkatli bir iş; önce tek kanallı test seviyesiyle doğrulanmalı.
- **Su animasyonu:** tek bir akan su dokusu kanal parçalarının üstüne maskeli kaydırılır (parça başına ayrı animasyon yok,
  ortak güncelleme döngüsü — performans kuralı). Akış ön cephesi hücreden hücreye ilerler.
- **Simülasyon:** sim şu an güvenilir değil (kullanıcı); bu engel sim'e sonra eklenecek.

## Gereken görseller

Tutarlılık sorunu yaşamamak için **az sayıda parça** üretilsin, yönleri ben döndürerek çoğaltırım:

1. **Kapalı kanal hücresi** (taşın altında görünen kapak): taş kapak / ızgara, 1 görsel.
2. **Açık kanal, düz parça** (kuru): 1 görsel (yatay). Dikey = 90° döndürülmüş hali.
3. **Açık kanal, köşe parçası** (kuru): 1 görsel. Diğer 3 köşe döndürülerek.
4. **Su**: ayrı görsel gerekmez; akan su dokusunu ben üretebilirim (ya da tek bir dikişsiz su dokusu).
5. **Depo**: mevcut Su Deposu bidon görseli kullanılabilir ya da yeni bir üst kenar deposu/çeşme.
6. **Çıkış**: alt kenarda ızgaralı ağız ya da küçük şelale, 1 görsel.

Hepsi aynı istemle, **tek seferde tek bir sayfada** üretilmeli (ayrı ayrı üretilirse stil tutmuyor).

## Görsel yöntemi (2026-10-20 güncelleme)

İlk görsel sayfası olmadı: yukarıdan bakışlı/kabartmalıydı ve parçaların kanal ağzı ölçüleri tutmuyordu.
Karar: **tamamen düz (2D flat), yukarıdan dik bakış**. Görsel araçları piksel ölçüsüne sadık kalmadığı için
**geometriyi kod çizer** (tam ölçü, parçalar dikişsiz birleşir); araçtan yalnız **dikişsiz dokular** istenir
(düz taş yüzeyi, oyuk tabanı, su). **Kullanıcı kararı: oyuk kenarları hücrenin %10 ve %90'ında** (oyuk genişliği hücrenin %80'i; kısa süre %5-%95 denendi, görünce %10-%90'a dönüldü).
Köşe parçası aynı çizgilerden kırılır (soldan giren oyuk %10-%90 bandında %90'a kadar gider, aşağı döner).
Işık sol üstten: oyuğun üst/sol iç duvarına ince gölge. Prototip: Masaüstü/Wondi Paylaşım/Kanal Prototip.png.

Net ölçü (100 px hücre, yatay parça): 0-10 taş kenar, **10-90 oyuk ve su (su tam bu aralığı doldurur, içeri çekilmez)**, 90-100 taş kenar. Dikeyde aynısı soldan sağa. Derinlik yalnız üst/sol iç duvar gölgesiyle verilir.

## İlerleme

- **Görseller (2026-10-21, hazır):** `Art/UI/Obstacles/WaterChannel/` — `Channel_{H,V,LB,LT,RB,RT}` (kuru),
  aynı şekillerin `_Closed` hali (pirinç ızgaralı kapak), `Water_*` (saydam su katmanı), `ChannelSource.png` (depo/çeşme).
  Kullanıcı dokuları (taş 21, kapak 22, oyuk 23, su 24) dikişsiz yapılıp kodla 0-10/10-90/90-100 geometriye giydirildi.
  Köşe adı = bağladığı iki kenar (L sol, R sağ, T üst, B alt). Işık sol üstten; şekiller döndürülmez, ayrı çizilir.
  **Köşeler (kullanıcı, 2 tur):** önce çeyrek halka denendi (fazla geniş); son hali L şeklinde bant, dış köşe yarıçapı %15,
  iç köşe dolgusu %6 (neredeyse dik, kenarları yumuşak). Kenarlara yine tam 10/90'da değer, düz parçayla dikişsiz.
- **Adım 1 — veri + editör (2026-10-21, kod yazıldı, Unity testi bekliyor):** `ObstacleId.WaterChannel = 76`;
  `LevelData.waterChannels` (`WaterChannelEntry.pathCellIndices`) + `WaterChannelEntry.Validate` (≥2 hücre, oynanır,
  tekrar yok, dik komşu, ilk = sütununun en üst oynanır hücresi, son = en alt). Seviye editörü **Kanal** modu: sırayla tıkla
  (komşu zorunlu, son hücreye tekrar tıklama geri alır), Kaydet doğrular, ızgarada turkuaz yol + ▼ depo / ↓ çıkış, Erase siler.
- **Adım 2-5 — görünüm, açılma, geçirgenlik, su akışı, hedef (2026-10-21, kod yazıldı, Unity testi bekliyor):**
  - `GridSpawner.StampWaterChannelCellsIntoLevel`: yol hücreleri `WaterChannel` (her hücre kendi origin'i) — v1: kanal
    hücresinde başka engel yok. Genel çizim + örtü açılınca yeniden çizim kanalı atlar.
  - `ObstacleLibrary` id 76 = WaterPuddle (53) kopyası: UnderTileLayered, hits 1, goalIcon = ChannelSource.
    `ObstacleStateService.IsInteractiveUnderTileOverlayAt` kanalı içerir → üstündeki taş normal oynanır.
    Hasar: normal eşleşmede vuruş döngüsü, special'da taş temizliği (çamurla aynı yol).
  - `WaterChannelService` (Resources/WaterChannelSkin): hücre başına zemin + su Image (underTilesObstaclesRoot),
    depo ilk hücrenin üstünde (overTilesObstaclesRoot). Temizlenince kapak → açık kanal + `BoardController.MarkRuntimePassThroughHole`
    (maskHoles + holes) → taş içinden geçer. Su akışı: depodan kesintisiz açık hücrelere, hücre başına 0.14 sn (düz: yönlü
    Filled, köşe: alfa); `BeginGoalOrbFlight` ile level-end bekler. Sonunda `NotifyWaterChannelCompleted`.
  - `TopHudController`: WaterChannel için OnObstacleDestroyed yok sayılır, `OnWaterChannelCompleted` hedefi 1 düşürür.
  - İpucu id 76 + loc hint_obstacle_waterchannel(_name).
- Açık noktalar: depo görseli tahta maskesinde kırpılabilir (üst kenar dışı); çıkışta görsel şelale yok; sim kanalı bilmiyor;
  kanal hücresine başka engel (yığın) desteği yok.

### Kural değişikliği (2026-10-21, kullanıcı)
- **Depo herhangi bir hücrede:** yolun İLK hücresi depo. Oyunda geçirgen delik (taş içinden geçer, üstüne taş konmaz),
  altında suyla dolu kanal ağzı, üstünde (taşların üstünde) çeşme başlığı. İlk sürümde depo tahtanın dışında
  çiziliyordu → tahta maskesinde kırpılıp görünmüyordu; artık tahtanın içinde.
- **Yol yukarı çıkamaz:** her adım aşağı ya da yana (su aşağı akar); yatayda aynı satırda ilerleyebilir.
- **Çıkış sol/sağ/alt kenar:** son hücre tahta kenarına ya da bir boşluğa değmeli; `WaterChannelEntry.ExitSide` çıkış
  kenarını seçer (önce son adımın yönü, sonra alt, sonra yan). Editörde depo "D", çıkış yön oku.
- GridSpawner hücre zemini döngüsü oyun sırasında geçirgen yapılmış hücreyi (depo) delik sayar (taş konmaz).
- **Special'ı kanala atma (2026-10-21, kod yazıldı, test bekliyor):** açık kanal hücresi (kuru ya da sulu; depo hariç)
  `MarkRuntimePassThroughHole(..., specialDropTarget: true)` ile işaretlenir. `RequestSwapFromDrag`: hedef böyle bir hücreyse
  ve sürüklenen special ise `ProcessSpecialIntoChannel` → special kanala kayar (0.12 sn) ve orada patlar (ProcessSpecialTap
  sonlandırmasıyla aynı, 1 hamle). Normal taş açık kanala sürüklenemez (hareket olmaz). v1: yalnız tahta boştayken (dinamik
  girdi yok).

### 2026-10-21 (devam)
- **Normal taş açık kanala:** orada eşleşme tamamlanıyorsa girer, eşleşme orada temizlenir; tamamlanmıyorsa geri döner,
  hamle harcanmaz (`ProcessTileIntoChannel`). Eşleşme bulucu `board.Holes`'a, yerçekimi `maskHoles`'a bakar → taş
  kanaldayken yalnız `Holes` işareti kalkar, hücre boşalınca (`RestoreChannelHoleWhenVacated`) geri konur. 4'lü eşleşmede
  special kanal hücresinde doğarsa yerçekimiyle aşağı geçer. Hareketli engel / yağ / kilitli taş kanala gidemez.
- **Su akışı yenilendi (kullanıcı: "progress bar gibi"):** sabit hız, hücreler arası duraksama yok (ilk hücre yumuşak
  kalkış); akış ucunda dalgalı köpük (`WaterFoam.png`, düzde giriş→çıkış, köşede çeyrek yay boyunca döner, hafif
  yalpalar); bitişte çıkış kenarından damla sıçraması (`WaterDroplet.png`, havuzlu 7 damla, yerçekimli).
- **Hata (2026-10-21):** açık kanal hücresinde kalan taş/special/movable havada kalıyordu (yerçekimi yalnız taş tutabilen hücrelerden topluyordu). CascadeLogic artık geçirgen hücredeki taşı da toplar, sütundaki boş yuva kadar.
- **Havada kalan special (2026-10-21):** resimdeki TNT = PulseCore görünümü. Kanala atılan taşla L/T veya 4lü eşleşmede special kanal hücresinde doğuyordu; taş yan sütundan geldiği için o sütunda yer yoktu. Special türü kanal hücresine göre belirlenir (PulseCore korunur), temizlemeden hemen sonra special oyuncunun taşı çektiği boşalan hücreye kayar (ProcessTileIntoChannelCore). Yerçekimi düzeltmesi (geçirgen hücredeki taşı toplama) ek güvence olarak kalır.

## MODEL DEĞİŞTİ (2026-10-21, kullanıcı: "hol gibi davranmayacağız, suyun gidişine karar vereceğiz")
Açılan kanal hücresi artık **geçirgen delik DEĞİL**. Hücre "açık" olarak hafızada tutulur ve **normal hücre** kalır:
taşlar üstüne gelir, oynanır, eşleşir. Su, depoya kesintisiz bağlı açık hücreler boyunca **taşların altındaki katmanda**
akar; tamamı açılınca kenardan çıkar → hedef. Yukarıdaki "taş içinden geçer / taş-special kanala atma / havada kalan
special" maddeleri GEÇERSİZ; o kodlar silindi (ProcessSpecialIntoChannel, ProcessTileIntoChannel, specialDropChannelCells,
CascadeLogic geçirgen hücre toplama — motor commit'teki haline döndü). Yalnız **depo** geçirgen kalır (çeşme taşların
üstünde, taş içinden geçer).
Kapalı görünüm (kullanıcı): ızgaralı kapak yerine açık kanal görselinin KOYU hali (Channel_*_Closed = açık × (0.74,0.72,0.76); ilk deneme 0.52 fazla koyuydu); vuruş alınca açık renge döner. Oyuncu nerede oynayacağını kanal şeklinden görür.
- **Senkron düzeltmeleri (2026-10-21):** (1) Açılma birincil olarak `OnObstacleDestroyed` ile dinlenir (her yok edilmede
  tetiklenir; `SpecialCellUtils` gibi görsel olay tetiklemeyen hasar yolları kaçmasın), görsel olay yedek; `Open()` idempotent.
  (2) Su kapalı hücrede durup köpük sönerken o hücre açılırsa akış kaldığı yerden sürer (önce sonsuza dek takılıyordu).
  Performans: hücre başına 2 Image (su başta kapalı), kare başına iş yalnız akış sırasında; damla Image'ları havuzlu.
- **Şelale (2026-10-21):** çıkış alt kenardaysa kanal tamamlanınca tahtanın altından level bitene kadar sürekli dökülür. Shader Shaders/UI_WaterfallFlow.shader (iki katman aşağı akan doku + dikey çizgiler + üstte köpük + altta sis; tamamen _Time, CPU işi yok), malzeme Resources/WaterChannelWaterfall.mat (build için Resources ta), doku WaterFlowTex.png (Repeat). RawImage grassOverlayRoot ta (maske dışı, kenar üstü), 0.8x1.5 hücre, 0.5 sn belirir.

### 2026-10-21 — Şelale kırpılıyordu (düzeltildi)
- Kök sebep: şelale `grassOverlayRoot`'a bağlıydı. `LiftGrassRootAboveBorder` onu `BoardBorderRoot`'un ebeveynine taşıyor, ama sahnede o ebeveyn **BoardMask (RectMask2D)** — yani kök hâlâ maskenin içinde, tahtanın altına sarkan şelale tamamen kırpılıyordu.
- Çözüm: çıkış efektleri (şelale + damlalar) `BoardController.UnclippedFxRoot` altında `WaterChannelExitFx` kökünde. Kök her kullanımda groundRoot'un dünya konum/ölçeğine hizalanır → hücre koordinatları aynı. Dispose'ta yok edilir (GridSpawner o kökü temizlemez).
- Şelale artık tam tahta kenarından başlar (kök tahtanın önünde; içeri taşarsa taşın üstüne binerdi).

### 2026-10-21 — Kanal üstünde çim (ve diğer 1x1 engeller)
- Eskiden `StampWaterChannelCellsIntoLevel` kanal hücresindeki boyanmış engeli sessizce siliyordu.
- Artık 1x1 engel (çim vb.) kanalın ÜSTÜNE yığın katmanı olur (`StampChannelCoversIntoLevel` → `StampStackedEntry`). Sıra alttan üste: kanal → boyanmış engel → yazarın stackedObstacles/safes katmanları.
- Çim temizlenince altındaki kanal açığa çıkar; kanal yine yalnız üstündeki taş eşleşince/special vurunca açılır (servis yalnız WaterChannel kırılmasında açar). Kanal görseli baştan çizilir, çimin altından görünür.
- Çok hücreli engel kanal üstünde desteklenmez: uyarı loglanır, silinir. Depo hücresi (path[0]) engel taşımaz.
- Unity testi bekliyor.
