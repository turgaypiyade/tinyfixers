# Level 149 — Mühürlü Dört Kule (EggBird revizyonu)

Asset: `LevelP_001530.asset` → `LevelCatalogPro` `level: 149` (chapter 6, `LevelCL_0149`).
Normal level, **9×11**, **69 aktif hücre / 30 hole**, **30 hamle**, dört renk.
Kullanıcı isteği: zor level, jel üstünde taş/duvar üstünde AncientSeal; kırıldıkça alttakiler de iş çıkarsın;
en fazla dört engel türü, dördüncü tür tasarımcı seçimi.

Seçim: **SpreadingGel + SculptingStone + AncientSeal + EggBird**.
Wall/MetalWall yerine SculptingStone seçildi: her açılan hücre ayrı üç vuruş ister, tek duvar parçasına
odaklanıp bütün bölgeyi birden açmak mümkün değildir. İlk SculptingSpecial sürümü kullanıcıya çok zor geldi.
36 special taşı, aynı yerlerde 36 EggBird ile değiştirildi; yumurtalar ayrı hedef değil, yardım kaynağıdır.
Yardım nedeniyle hamle ilk deneme ayarı olarak 32'den 30'a indirildi. Güncel denge oynanarak doğrulanmadı.

## Kompozisyon

Dört simetrik 3×3 mühür kulesi ve ortada üç sütunluk kesintisiz oyun koridoru.
Kuleler arasındaki hole kuşakları, üst/alt çıkıntılar ve merkez köprüsü soyut bir oda kompozisyonu kurar.
Mühürlerin mavi/altın görünümü ana aile; açıldıkça kum rengi taş, krem/mor yumurtalar ve mor jel ortaya çıkar.
Son üç seviyedeki nesne/doğa siluetlerinden sonra soyut ve katmanlı bir desen tercih edildi.

```text
...ooo...
AAAoooBBB
AAAoooBBB
AAAoooBBB
...ooo...
...GGG...
...ooo...
CCCoooDDD
CCCoooDDD
CCCoooDDD
...ooo...
```

`.` hole · `o` normal taş · `G` açık jel üzerindeki normal taş.
`A/B/C/D` üçer katmanı örten farklı AncientSeal'lerdir; her kulede dokuz hücre aynı yığını taşır.
Origin'ler: **A 9**, **B 15**, **C 63**, **D 69**. Koordinatlar sıfır tabanlı, y üstten aşağıdır.

Önizleme: `Level149-Muhurlu-Dort-Kule.png`. Mevcut sprite'larla açılış ve sağda katman sırası çizilir.
Gerçek oyun ekran görüntüsü değildir; mühür çarkı/sayaç yerleşimi ve jel birleşimleri runtime'da farklı olabilir.

## Katmanlar — her kule hücresinde

| Alttan üste | Veri | Üst katman açılınca kalan iş |
|---|---|---|
| SpreadingGel (46) | Base `obstacles[]`; hücre başına seed | Sabit kapak kalkıp hareketli yumurta açığa çıkınca jel görünür, hedefe sayılır ve aktifleşir. |
| EggBird (45) | `stackedObstacles[]`, sıra 1 | İki vuruşta çatlar/kuş çıkarır; normal eşleşmeyle de kırılır, hareketlidir. |
| SculptingStone (28) | `stackedObstacles[]`, sıra 2 | Hücre başına 3 vuruş; normal eşleşme veya special kabul eder. |
| AncientSeal (64) | `ancientSeals[]`, sıra 3; 3×3 | 6 kırmızı + 6 sarı + 6 yeşil taş toplar, sonra altındaki dokuz taş açılır. |

Oyuncunun gördüğü sıra: **mühür → SculptingStone → EggBird ve jel**.
Yalnız en üst katman işler. Bir mührün toplanması alttaki taşlara hasar vermez; taş temizlenince
iki vuruşlu yumurta açılır. Güncel `EnsureGroundBeneath` kuralında hareketli engel altındaki jel açıktır:
SculptingStone kırıldığında yumurta henüz çatlamadan da altındaki seed hedefe sayılır/aktifleşir.
Yumurta düşebilir veya takas edilebilir; taşın altındaki açılış simetrisi oyun ilerledikçe değişir.
Aynı special/combo zinciri birden fazla geçerli vuruş sağlayabilir; her katman için ayrı oyuncu hamlesi zorunlu değildir.
Yığın başına **yalnız bir hareketli engel**, EggBird vardır. Mühürler altlarındaki bütün 3×3 alanı tam örter.

## Üç hedef, dört engel türü

- **4 AncientSeal:** her biri 18 renk taşı, toplam 72 toplama. Başlangıç renkleri A kırmızı, B yeşil,
  C sarı, D kırmızı. Aynı rengi isteyen mühürler tek taşı çoğaltmaz, sırayla paylaşır.
- **36 SculptingStone:** toplam 108 hücre-vuruş yükü; tek etki birden fazla hücreyi vurabilir.
- **69 hücre jel kaplama:** bütün aktif board, sonradan açılan kule hücreleri dahil.
  Başlangıçta yalnız merkezdeki üç jel açıktır: kalan hedef 66. Kule altındaki 36 seed örtülüyken sayılmaz.

**36 EggBird yardımcıdır, goal değildir.** Bütün yumurtaları kırmak kazanma şartı değildir.
Mevcut kodda her yumurta üç rastgele uygun hedefe birer vuruş gönderir; mümkünse hedefler farklıdır.
Komşu hücrelere ayrıca alan hasarı yoktur. Kuşlar başka yumurtaları/taşları vurabilir, special tetikleyebilir;
örtülü veya pasif engeller hedeflenmez, doğrudan AncientSeal toplamasını ilerletmezler.

Jel kırılmaz; hedef yayarak kaplamaktır. Merkezdeki üç açık seed, mühürler toplanırken jel kaynaklı
eşleşme/special üretmeye izin verir. Bütün hedefler bitmeden level tamamlanmaz; mühürleri açmak tek başına yetmez.

## Oynanış ve dolum

Başlangıçtaki 33 normal taş hücresi merkezde, üst girişe doğrudan bağlıdır; hiçbir açık hücre dolum
gölgesinde değildir. Kule altlarına gizli pinned taş veya hazır special konmaz.
33 açılış rengi sabittir; refill dört normal renkten gelir. Dar koridor yatay/2×2 fırsatları ve
dikey eşleşmelerden üretilen yatay etkileri değerli kılar; line/combo aynı satırdaki iki kuleyi vurabilir.

İki görülebilir fırsat: `(3,3) ↔ (4,3)` dört sarı taşlık dikey eşleşme;
`(5,5) ↔ (5,6)` **yalnız 2×2 kare** oluşturur (eşlik eden üçlü yok), jel kaynaklı PatchBot fırsatıdır.
Başlangıçta otomatik üçlü/aynı renk 2×2 yoktur; **18 geçerli açılış takası** vardır.
İlk aşamada renkleri toplayıp hangi mührün daha önce açılacağını yönet; sonra taşları temizleyip yumurta
yardımını açığa çıkar. Kuşlar diğer açık hedeflere yardım eder. Mühürler aynı anda açılmak zorunda değildir;
toplama, kırma ve jel yayma birlikte ilerler.

Dolum kontrolü gerçek köşe kuralını içerir: AncientSeal, SculptingStone ve EggBird `allowDiagonal=1`;
EggBird sabit blocker değildir ve normal gravity/takas kullanır. Temizlenen kule hücreleri yerlerine ve üstlerindeki kalan engellere
göre dikey/çapraz dolum alır. Tüm kapaklar temizlendiğinde 69 hücrenin tamamı spawn erişimine sahiptir.
Hole'lar düşüşte geçirgendir; kalıcı engel veya engel altında doldurulması gereken bağımsız cep yoktur.

## Doğrulama ve sınırlar

99 elemanlı diziler, 69 hücrelik bağlı/simetrik maske, hole'larda boş içerik, dört 3×3 mühür ayak izi,
72 stacked kayıt, 36 hücrede tam dört katman sırası ve en fazla bir movable, origin'ler, üç hedef, geçmiş tanıtımlar,
jel seed/kalan hedef sayımı, açılış fırsatları ve katalog/GUID statik kontrol edildi.
Sabit kapak aşamalarında 33 merkez hücresinin, tüm sabit kapaklar kalkınca 69 hücrenin erişimi
güncel `allowDiagonal` değerleri ve iki köşeden en az birinin geçilebilir olması kuralıyla incelendi.
Bu, coroutine/reveal zamanlamasının veya gerçek oynanış dengesinin testi değildir.
Bu revizyonda maske, mühür ayarları, SculptingStone ve jel kayıtları, açılış taşları, GUID ve katalog korunur.
Yalnız special taşı katmanı EggBird'e çevrildi, special taşı hedefi kaldırıldı ve hamle 30 oldu.

Yeni gameplay/VFX kodu eklenmez. Dört mühür tek mevcut AncientSealService'i kullanır; toplam toplama kotası
72 ile sınırlıdır. Mevcut uçuşların nesne oluşturma/Dispose maliyeti ve mühür kırılma efektleri vardır;
havuzlu oldukları varsayılmaz. Servisin uçuş iptali, uçuş job'larını ve kırılma hücre tutmalarını bırakma
yolları kaynakta incelendi. Yumurta yardımı en fazla 36 hatch ve 108 hedef vuruşu üretir; hedef bulunamazsa
daha az olabilir. Mevcut EggBirdHatchAction/EggBirdFlightView kullanılır: uçuş görselleri nesne oluşturur,
havuzlu olduğu varsayılmaz; hedef taraması yumurta başınadır. Hatch action eski board'a hasarı engelleyen
kontrol ve finally'de flight/job temizliği içerir; uçuş view'ı disable'da uçuş sesini durdurur.
Yeni gameplay/VFX kodu eklenmedi; canlı sahne çıkışı, çoklu hatch, nesne sayısı ve kare süresi ölçülmedi.
Unity, bot, oynanış simülasyonu ve build çalıştırılmadı. **30 hamle ilk tahmindir**; ilk testte renk
toplama süresi, yumurtaların yardım yoğunluğu ve hareketli engel altındaki jel açılması değerlendirilir.
