# Level tasarım referansı (128+ için) — 2026-10-11

Kaynak: `LevelCatalogPro` (1–127, `level` alanına göre), `ObstacleLibrary.asset`, sprite renk ölçümü (baskın ton) ve
servis kodu. [Tasarım ilkeleri](Design-Principles.md) ve [engel rehberi](Obstacle-Design-Guide.md) geçerli; bu doküman
onları **renk uyumu, tazelik ve katman (overlay) tarifleri** açısından tamamlar.

Kullanıcı hedefi (2026-10-11): **şık gridler, renkleri uyumlu board'lar, duruma göre overlay ile zorlaşan level'lar.**
Sıradaki slotlar: 128, 129 (normal), **130 (Boss Duel)**.

## 1. Renk aileleri (sprite ölçümü)

Taş renkleri: Gear sarı · Core kırmızı · Bolt mavi · Plate yeşil. Board'u 1–2 aile + bir vurgu rengiyle kur.

| Aile | Engeller (id) | Not |
|---|---|---|
| **Sıcak ahşap / toprak** | chest1/2/3 (4/5/6), PlasticTwoStage fener (40), plOrange (8), Mud (25, kahve zemin), SculptStone (28, kum), Barrel2 (41, kum kasa), Gold (32), Hamster (57), Cargo kırmızı panda (34) | En geniş aile; "sıcak" board için temel. Helmet baykuş (30) krem vurgu olarak uyar. |
| **Bahçe yeşili** | Grass (42), GrassFlower (58, beyaz çiçekli), Oil (14, yeşil jöle), Wardrobe (27, yeşil — robot amblemli), PlayerShield (35), plGreen (13, hiç tanıtılmadı) | Yeşil + sıcak ahşap en doğal ikili. |
| **Mor laboratuvar** | Gel (46), plastic (7, mor), HatLauncher (31), EnergyContainer (22, mor sandık), GelLauncher (59–62, mavi gövde/mor jel), EnemyShield (36) | Jel temalı level'lar; turuncu fenerle tamamlayıcı kontrast. |
| **Taş / gri** | Wall (55, krem tuğla), MetalWall (56, gri perçin), SculptSpecial (39, gri pusula), Stone (1) | Çerçeve, sur, kale; renkli içeriği çerçeveler. |
| **Mavi / su** | plBlue (11), WaterPuddle (53), Tube (26), KeyGenerator (44), ColorChest (21, mavi çerçeve), RocketBasket (38), AncientSeal (64) | Serin vurgu; kırmızı ile güçlü kontrast. |
| **Kırmızı vurgu** | plRed (12), WaterTank (52), PaintCanBox (63), Magnet (29) | Az miktarda vurgu; tek başına ağır. |

## 2. Tazelik — son kullanım (öneri çeşitliliği için)

- **Son dönemde çok kullanıldı (dinlendir):** GrassFlower (105–127, 10+ level), MetalWall (112–120 art arda), Wall, PlasticTwoStage.
  113–116 sonrası kullanıcı: "çiçek/çim çok kullanıldı; çamur, jel, taş katmanları öne çıksın".
- **Uzun süredir yok (yeniden getir):** Oil (son **21**!), Cargo (69), Magnet (83), KeyGenerator (81), OverrideBatteryBox (87),
  Safe (92), chest2 (96), Barrel2 (97), düz Grass (98), Tube (100), plBlue/plRed (113), SculptSpecial (111).
- **Hiç tanıtılmadı:** Plastic_Yellow (9), Plastic_Green (13) — renk uyumu için tanıtılabilir (yeni engel tanıtımı sayılır).
- **En yeni (121–131):** GelLauncher (121), PaintCanBox (122), AncientSeal (126), LaserCannon (131, 2×2, yalnız special; alt kenara koy).

## 3. Katman (overlay) kuralları — kodda kilitli

1. Mud / Gel / WaterPuddle **daima en altta**; başka bir şeyin üstüne konamaz.
2. **Tam örtme:** üstteki katman alttaki engeli aynı ya da daha çok hücreyle kaplar. Grass/Oil saydam örtü: kısmi örtebilir, altı görünür.
3. Yığında **en fazla 1 hareketli** (movable) engel. Örn. Gold + PlasticTwoStage aynı yığında YASAK.
4. Çok hücreli engelin bir hücresi bile örtülüyse **hiç vuruş almaz**. Örn. 2×2 sandık + üstünde grass: grass bitene dek kilitli.
5. Vuruş yalnız **en üst katmana**; alt katman açılana dek "yok hükmünde".
6. Örtülü/kilitli engeller PatchBot, roket sepeti ve EggBird hedef listesinden çıkar.
7. Örtü + altında içerik engellerinde (Safe, AncientSeal) **önce alt açılır, sonra hedef bildirilir** — altın erteleme kuralı buna bağlı.

## 4. Zorluk tarifleri (overlay ile)

| Tarif | Yığın (alttan üste) | Etki |
|---|---|---|
| Gizli zemin | Mud → chest3 | Sandık kırılınca çamur çıkar; iki hedef tek hücrede. 109 ve 111'de kullanıldı. |
| Jel tohumu | Gel → SculptStone | Örtü kırılmadan jel yayılmaz; yayılma sırasını tasarlatır. 108 ve 116'da kullanıldı. |
| Örtülü kale | Wall/MetalWall → GrassFlower | Önce çiçek, sonra duvar; 105, 119 ve 127'de kullanıldı (**dinlendir**). |
| Saydam yağ | chest/Helmet → Oil | Yağ görünür ama alttaki engel kilitli. Oil uzun süredir yok, **taze**. |
| Üç katman (hard) | Mud → chest3 → Grass | Kullanıcı onaylı: "zor level'larda 3 katmanlı yığınlar". |
| Kasa hazinesi | Gold → Safe / AncientSeal | Ödül; altın kalınca kazanma ertelenir (hamle devam). |

Hareketli engeli (plastik, kask, altın) bir yığına yalnız bir kez koy.

## 5. Kompozisyon notları

- Tam 9×11 veya 9×10 çerçeve + hole şeritleri ya da simetrik siluet; bölgesel renk blokları.
- Kalıcı engeller (HatLauncher, KeyGenerator, EnergyContainer, RocketBasket): tam boy sütun yaparsan **yatay eşleşme imkânsızlaşır** (125 dersi). Şerit genişliği ≥3 bırak ya da kalıcıları alta yerleştir.
- Kalıcı engelin **altında** hücre kalırsa oraya dikey dolum olmaz → kalıcıları alt kenara veya tam sütun olarak koy.
- Az renk havuzu (randomPool) kaskadı çok artırır: 125'te 2 renk "çok rahat", 3 renk "geçilemiyor". Havuz da bir zorluk düğmesi.
- Yardımcılar (RocketBasket, OverrideBatteryBox, EggBird) hedef yapılmaz; toplu bölge halinde koy.

## 6. Boss 130 için başlangıç (110/120 çizgisinin devamı)

| | 110 | 120 | **130 önerisi** |
|---|---|---|---|
| Rakip canı | 400 (2 rakip) | 450 (3 rakip) | ~480 (3 rakip) |
| Oyuncu canı | 190 | 210 | ~220 |
| Hasar | 6 / 8 | 6 / 8 / 9 | 6 / 8 / 9 |
| Hamle | 28 | 30 | ~30 |

Fırlatılabilir türler yalnız Oil, Mud, Gel, plastikler, Helmet, PlasticTwoStage. Board'da çok sayıda bulunan türü
havuza koyma (fırlatma tavanı onları da sayar). 121–129 arasında yeni çıkanlar (GelLauncher, PaintCanBox, AncientSeal)
board'da kullanılabilir.

## 7. Hole, ada ve grup kuralları (kullanıcı, 2026-10-11)

- **Hole silüeti zorunlu düşün:** düz dikdörtgen + tek desen "çok basit". Hole hem silüet hem zorluk (darboğaz, ayrık ada) verir.
- **Adacıklar:** grid'i hole ile bağımsız adalara böl — ulaşması zor. Motor: hole'lar spawn için geçirgen
  (`IsSpawnPassThroughCell`), üstü yalnız hole olan ada kendi dolar; üst adadan alt adaya taş hole'dan düşer.
  Adayı yalnız üstündeki **engel** keser (o zaman çapraz dolum gerekir).
- **Hareketli engeller grup halinde:** PlasticTwoStage, plastikler, kask vb. bitişik blok/şerit olarak; tek tek serpme.
- **Tercih edilen overlay:** plastikler, SculptingStone ve HelmetPorcelain → **Gel / Mud / SculptSpecial üstüne örtü**
  (115: Mud üstünde PTS/Helmet; 108/116: Gel üstünde SculptStone). Yığında en fazla 1 hareketli.
- **Hole = oda bölmesi (kullanıcı örneği L135):** birkaç tam satır/sütun hole board'u dikdörtgen odalara böler; her
  oda tek tür; hole ile kopan odalara yalnız special ile kırılan engel; ana odada dolum kenara yapışık L bant, blok
  ortasına kanal açma; iki tür çapraz gradyanla ayrılır. Bkz. `Level135.md`.
- **Her şey şekil olmasın:** 3–4 levelde bir soyut desen (şerit, dama, iç içe çerçeve, ada kümeleri).

## 8. Şekil havuzu (kullanılanları işaretle)

Akış: aşağı **daralan** (taç→gövde, kâse→ayak, kalkan) ve **alta oturan** (ev, gemi) şekiller güvenli; aşağı **genişleyen**
(piramit, çan, Eyfel) kısa tutulmalı — genişleyen kısım yalnız çaprazla dolar.

- Kullanıldı: kelebek 106 · madalyon 107 · elmas 108 · kemer 110 · kale 111/115/117 · uçurtma 113 · petek 114 · kalp 116 ·
  taç 120 · piramit 127 · **elma ağacı 128 · kupa 129 · kalkan 130** · soyut: gözcü adaları 131 · deney tüpü rafı 132 (Tube+Magnet dönüşü) · Roma tapınağı 133 (Tube sütun + Wall basamak + Mud alınlık) · Roma tapınağı II 134 (Wall çatı + Tube sütun + Wall zemin)
- Bahçe/doğa: ev, lale, mantar, kaktüs-saksı, ayçiçeği, yaprak, armut, çilek, havuç
- Tamir/alet (oyun teması): anahtar, çekiç, vida, dişli (halka), ampul, musluk, kova, el feneri
- Harika/yapı: Kolezyum kemerleri, deniz feneri, yel değirmeni, köprü, saat kulesi, kubbe, (kısa) Eyfel
- Nesne/sembol: çan, kum saati, şemsiye, balon (ada + ip), yelkenli, roket, çapa, zarf, hediye kutusu
- Soyut: halka, çapraz, dama, zikzak şeritler, iç içe çerçeveler, ayna simetrik adalar
