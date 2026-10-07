# Engel tasarım rehberi

108 hazırlanırken mevcut kod, `ObstacleLibrary.asset` ve `LevelCatalogPro.asset` üzerinden çıkarıldı. Tasarım amaçlı yaşayan nottur; eski dokümanlarla çelişirse güncel servis kodu ve asset ayarları esas alınır. Yeni engel veya davranış değiştiğinde güncellenir.

## Seçim ilkesi

O ana kadar açılmış bütün engeller tasarım havuzudur. Son tanıtılan engeller her levelde zorunlu değildir. Bölgesel renk uyumu, toplu geometrik yerleşim, oynanış çeşitliliği ve yardım/zorluk dengesi birlikte değerlendirilir. Aşağıdaki ilk seviye değerleri katalogdaki ilk authored/goal kullanımını gösterir; ayrı bir tutorial takvimi değildir. Dinamik doğan engeller ayrıca değerlendirilmelidir.

## Jel — SpreadingGel, ID 46

- Temizlenecek bir engel değildir; kalıcı bir kaplama hedefidir. Altındaki taş oynanabilir. Tüm board hedefleniyorsa `goal.amount = Normal hücre sayısı`; hole'lar sayılmaz, sonradan açılacak engel hücreleri sayılır.
- HUD kalanını `amount - RevealedCount` olarak hesaplar. Başlangıçtaki açık jel sayılır; örtü altındaki mühürlü jel, örtü kırılmadan hedefe sayılmaz ve yayılma kaynağı olmaz.
- Güncel temel kural: olayın kaynağı jele bulaşmışsa olayın temizlediği taş hücrelerine jel bırakılır. Normal eşleşmenin bir taşı jel üzerindeyse bütün eşleşme grubu bulaşır. Oluşan special da bulaşı taşır.
- Swap ile jel kaynağından çıkan taş, o swap'ın eşleşme kararında kaynak kabul edilir. Geçersiz swap tek başına kalıcı yayılma yaratmaz. Salt gravity de yayılma nedeni değildir.
- Jel üzerinde/bulaşmış kaynaktan tetiklenen special ve combo, zincirlerde de yayılma sağlayabilir. Jelsiz bağımsız cascade eşleşmeleri otomatik bulaşmaz. Bir etkinin jel üzerinden geçmesi veya jele komşu olması tek başına yeterli değildir.
- SculptingStone/chest gibi katmanların altında seed saklanabilir: base `obstacles[] = SpreadingGel`, üst engel `stackedObstacles[]`. Örtü kırılınca seed açılır.
- Tasarım akışı: açık jel çevresinde eşleşme/special kur → engelli bölgeleri aç → geriye kalan bütün hücreleri kapla. Yayılma, bütün engeller bitene kadar kodla ertelenmez; iki süreç birlikte ilerleyebilir. Zorunlu temizlenecek engeller ayrıca hedeflenir.
- Yardımcı patlamalar bütün board'u otomatik jelleyecek varsayımıyla tasarım yapılmaz; jel kaynağı ve ilgili etki yolu önemlidir.

Kaynaklar: [BoardController](../../Assets/_Project/Scripts/Grid/Board/BoardController.cs) içindeki `NoteGelSpreadParticipants`, `NoteGelCarriedBySwap`, `ShouldPaintGelOnClear`, `NoteGelSpreadOrigin`; [SpreadingGelOverlayService](../../Assets/_Project/Scripts/Grid/SpreadingGel/SpreadingGelOverlayService.cs); [TopHudController](../../Assets/_Project/Scripts/UI/TopHUD/TopHudController.cs) içindeki jel hedef hesabı; [GridSpawner](../../Assets/_Project/Scripts/Grid/GridSpawner.cs) içindeki `DrawStampedBeneathVisuals`.

`LevelData.cs` jel açıklamasındaki eski swap özeti güncel eşleşme/zincir kurallarını bütünüyle açıklamıyor. Rehber yukarıdaki çalışan metotlara dayanır.

## Yardımcı engeller

Kullanıcının hedef politikası: **RocketBasket ve OverrideBatteryBox doğrudan goal yapılmaz.** EggBird yardımcı rolündeyse hedef zorunluluğu yoktur; tüm yumurtalar kırılmadan level tamamlanabilir. EggBird ancak özel olarak temizleme amacı seçildiğinde goal olur. Yardımcılar mümkün olduğunda bitişik bir grup halinde düzenlenir. Tam-board jel gibi ayrı hedefler, yardımcıların kapladığı hücrelere erişimi yine gerektirebilir; doğrudan yardımcı goal'ü olmaması bu dolaylı gereksinimi kaldırmaz.

| Engel | Güncel davranış | Tasarımda kullanım |
|---|---|---|
| RocketBasket · 38 | Varsayılan kırmızı/sarı/mavi olmak üzere 3 roket. Komşu normal eşleşme yalnız yüklü aynı renk roketi ateşler; special/booster kalan roketlerden birini ateşleyebilir. Boşalınca kalkar; normalde ayrı hedef yapılmaz. | Zor bölgelere yardım sağlayan erişilebilir bir odak. Yakınında yeterli eşleşme alanı ve gerekli renk havuzu bırak. |
| OverrideBatteryBox · 43 | 2×2, dört rengin şarjı ayrı. Güncel library `hits=3`: renk başına 3, toplam 12 şarj vuruşu. Dolunca board genelinde bir vuruşluk dalga; normal taşları temizler, specials zincirlenebilir. Çok katmanlı her engeli tek seferde yok etmez. | Daha çok hazırlık isteyen güçlü yardım. Dört renk havuzunu ve çevresinde şarj edilebilir alanı koru. |
| EggBird · 45 | Hareketli, iki vuruş: çatla → kuşlar çık. Üç uygun hedef seçilir; mümkünse farklı, eşit rastgele, hedef önceliği yok. Hedef başına tek vuruş; ilave komşu splash yok. | Küçük bir engeli temizlemekle yardım ödülü bir arada. Belirli bir engeli kesin vuracağı varsayılmaz. |

RocketBasket için `rocketBasketFireAllOnHit=1` bütün yüklü roketleri tek tetiklemede boşaltır; normal match yine yüklü renklerden biriyle eşleşmelidir. Bu ayar bilinçli bir kolaylaştırmadır. 108'de kapalıdır.

Kaynaklar: [RocketBasketService](../../Assets/_Project/Scripts/Grid/Board/Obstacles/RocketBasketService.cs), [ObstacleStateService](../../Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs), [OverrideBatteryBoxDetonationAction](../../Assets/_Project/Scripts/Grid/Board/Actions/OverrideBatteryBoxDetonationAction.cs), [EggBirdHatchAction](../../Assets/_Project/Scripts/Grid/Board/Actions/EggBirdHatchAction.cs), [EggBird QA](../EggBird-QA.md).

## Açılmış engel aileleri için kısa başvuru

Vuruş sayıları mevcut library'den okunmuştur; özel servislerin renk, öğe, şarj ve kilit sayaçları ayrıca geçerlidir. Tablo ayrıntılı servis dokümanlarının yerine geçmez.

| Engel / ID | İlk seviye | Özellik / yerleşim notu |
|---|---:|---|
| chest1/2/3 · 4/5/6 | 1 / 3 / 14 | Tek hücre, 1/2/3 vuruş. Ahşap renk blokları, şeritler ve örtüler için. |
| Stone · 1 | 2 | Tek vuruşlu taş; mevcut stage bayrakları ayrıca library'den kontrol edilir. |
| Mud · 25 | 4 | İki katmanlı alt zemin; jel gibi kalıcı kaplama hedefi değildir. |
| Oil · 14 | 6 | Yayılan baskı engeli; yayılma/temizleme kuralları Oil servisi üzerinden işler. |
| plastic_orange / plastic · 8/7 | 8 / 12 | Tek vuruşlu hareketli engeller. Gravity açılış desenini zamanla değiştirir. |
| Cargo · 34 | 9 | Kırılmaz; aşağı çıkıştan toplanır. Çıkış ve düz düşüş yolu tasarlanmalı. |
| Plastic_Blue / Plastic_Red · 11/12 | 14 / 15 | Komşu normal eşleşmede kendi rengini ister; renk havuzu ve erişim önemli. |
| ColorChest · 21 | 18 | 2×2 renk mekanikli sandık; tek hücreli chest gibi sayılmamalı. |
| HatLauncher · 31 | 22 | EnergyOrb üreten kaynak; collectible hedefiyle birlikte düşünülür. |
| Wardrobe · 27 | 29 | 2×2; kapı açma ve içerideki öğeleri çıkarma aşamaları. |
| Player/EnemyShieldPickup · 35/36 | 30 | Boss bağlamında kalkan; normal levelde sıradan kırılabilir hareketli öğe. |
| Tube · 26 | 31 | Özel `tubes[]` verisiyle çok hücreli, küçülen engel. |
| HelmetPorcelain · 30 | 37 | Tek vuruş, hareketli; açık tonlu gruplar için. |
| SculptingStone · 28 | 38 | Güncel ayarda üç vuruş, tek hücre. Krem bloklar; aşamalarla oyma şekil açığa çıkar. |
| SculptingSpecial · 39 | 45 | Güncel ayarda tek vuruş fakat yalnız special hasarı. Normal match ile açılacağı varsayılmaz. |
| PlasticTwoStage · 40 | 47 | İki vuruş, hareketli; plastic/porcelain ailesiyle uyumlu gruplar. |
| Grass · 42 | 58 | Görünen komşu eşleşmelerle aşınan örtü; kendi hücresindeki match aynı hasar kuralı değildir. Güncel library `locksInteraction=1`; eski enum yorumundaki serbest swap özeti esas alınmaz. |
| GoldMoney · 32 | 63 | Tek vuruşlu hareketli bonus; ana level hedefi değildir. |
| Safe · 33 | 63 | `safes[]` footprint'i altında içerik saklar; sıralı renk kilitleri. |
| RocketBasket · 38 | 71 | Yukarıdaki yardım bölümü. |
| Barrell_v2 · 41 | 72 | Güncel footprint 2×2, dört vuruş; sonunda Mud saçar. Dinamik Mud hedefi dikkate alınır. |
| Magnet · 29 | 74 | `magnets[]` ile eşleşmiş uçlar; vuruşlarla yaklaşır, birleşince kalkar. |
| KeyGenerator · 44 | 76 | Key taşı üretir; Tile/Key hedefiyle birlikte kullanılır. |
| EnergyContainer · 22 | 79 | EnergyOrb üretimi servis yönetimindedir; salt `hits` değeri hedef toplamı değildir. |
| SpreadingGel · 46 | 86 | Yukarıdaki kaplama bölümü. |
| OverrideBatteryBox / EggBird · 43/45 | 87 | Yukarıdaki yardım bölümü. |
| WaterTank · 52 | 93 | 2×2, iki vuruş ve güncel stage'lerde SpecialOnly; kırılınca WaterPuddle üretir. |
| Wall · 55 | 101 | Bağlı parça ortak origin taşır. Tek hücreye beş vuruş bütün parçayı yıkar; hedef hücre değil parça sayısıdır. |
| GrassFlower · 58 | 105 | Çimin üzerinde ilave çiçek katmanı. GrassFlower hedefi çiçek dökümünü; Grass hedefi alttaki çimin temizlenmesini ister. |
| GelLauncher · 59–62 | — | Jel fırlatıcı (kartuş). Up/Down 1×2, Left/Right 2×1; ağız yönü id'den. Yalnız special, 2 vuruş (kapalı → çatlak). Kırılınca kapak ağızdan tahta kenarına kadar uçar, yolundaki her hücrede engel kalmayana kadar vurur (kırılmazlarda durur), taşı kırar/special'ı tetikler; jel 2 hücre geriden yolu ve kartuşun kendi hücrelerini boyar. Hedef değil, yardımcı. Köşelere/kenarlara, ağız tahtanın içine bakacak şekilde konur; dik olanı üst köşeye koymak o sütunun üst girişini kapatır. |
| PaintCanBox · 63 | Boya Piramidi | 2×2 sabit kasa, içinde 4-3-2 dizili 9 boya kutusu (sayılabilir içerik). Bitişik her eşleşme 1, her special 2 kutu düşürür; vuruşta tüm kutular sallanır, düşen takla atarak uçar. Son kutuyla kasa kalkar. Hedef olarak kullanılabilir (obstacleId 63). Wardrobe'un kapısız, hemen başlayan hâli: erken seviyelerde orta zorluk, yan yana 2 kasa güzel durur. |

WaterPuddle (53), WaterTank'tan doğan tek vuruşlu alt zemindir; kendi hücresindeki taş temizlenince kurur. Dinamik olarak görülmesi, yalnız doğrudan yerleştirilmiş obstacle dizilerinin taranmasıyla bulunmayabilir.

Listede bulunmayan bir ID yalnız kodda mevcut diye yeni levele eklenmez. Katalog, üretici/katman verileri ve tanıtım sırası ayrıca incelenir.

Temel kaynaklar: [LevelData](../../Assets/_Project/Scripts/Core/LevelData.cs), [ObstacleLibrary](../../Assets/_Project/Settings/ObstacleLibrary.asset), [LevelCatalogPro](../../Assets/_Project/Settings/LevelCatalogPro.asset). [Eski akış envanteri](../ObstacleFlow_Inventory.md) zamanlama geçmişi için faydalıdır; güncel gameplay ayarı olarak tek başına kullanılmaz.
