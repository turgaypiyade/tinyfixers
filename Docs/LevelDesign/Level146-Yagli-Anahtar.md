# Level 146 — Yağlı Anahtar

Asset: `LevelP_001500.asset` → `LevelCatalogPro` `level: 146` (chapter 6, `LevelCL_0146`).
Normal level, 9×11 zarf, **51 aktif hücre / 48 hole**, **28 hamle**, dört renk.
Hedef zorluk: normal; 145'in yoğun special hedeflerinden sonra farklı bir mekanikle nefes aralığı.
Hamle ve zorluk ilk tahmindir, oynanarak doğrulanmadı.

## Kompozisyon

Tamirci hayvanların aleti: üstte açık çeneli anahtar, ortada üç hücre genişliğinde sap,
altta deliği olan halka. Maske, engeller ve katmanlar sağ-sol simetriktir.
Ahşap/turuncu sandıklar ve fenerler sıcak ana aile; yeşil yağ çene uçlarında tek vurgu bölgesidir.
Çim, çiçek, lazer topu ve heykel kullanılmaz; eski engeller farklı bir kompozisyonda geri gelir.

```text
OO.....OO
OOoooooOO
.CCoooCC.
..CCoCC..
...MMM...
...PPP...
...PPP...
...MMM...
..ooooo..
..oo.oo..
..ooooo..
```

`.` hole · `o` normal taş · `C` chest2 (5) · `O` chest2 üstünde Oil (14)
· `M` Mud (25) · `P` Mud üstünde PlasticTwoStage (40).
Koordinatlar sıfır tabanlı, y üstten aşağıdır.

Önizleme: `Level146-Yagli-Anahtar.png`. Mevcut library ve taş sprite'larıyla yerleşim şemasıdır;
oyun ekran görüntüsü değildir. Yağ saydamlığı ve zemin birleşimleri runtime'da farklı çizilebilir.

## Hedefler ve katmanlar

| Hedef | Adet | Rol |
|---|---:|---|
| chest2 | 16 | Çenenin iki tarafında ikişer vuruşlu ahşap bloklar. |
| Oil | 8 başlangıç hücresi | Çenenin üst iki sırasındaki sandıkların üzerinde örtü. |
| Mud | 12 | Sapın dört satırı; altı hücre başlangıçta fenerlerle örtülü. |
| PlasticTwoStage | 6 | Sap ortasında bitişik 3×2 blok; ikişer vuruş, hareketli. |

Yağ temizlenmeden altındaki sandık hasar almaz; yağ kalkınca sandığın iki vuruşu kalır.
Fenerin altındaki çamur, fener kırıldığında veya hücreden düştüğünde açılır.
Yığınlar en fazla iki katmandır, aynı hücrede en fazla bir hareketli engel vardır.
Yağ için `oilSpreadMaxCells = 10`: aynı anda en fazla 10 canlı yağ hücresi.
Oil servisi, hamlede herhangi bir yağ vurulursa yayılmaz; vurulmazsa uygun tek komşuya yayılabilir.
Yeni yağ hücresi HUD kalan hedefine eklenir; **8 yağ kırıp hâlâ canlı yağ varken bitirme** hedeflenmez.

## Oyun akışı ve dolum

1. Çene içindeki taşlardan başlayıp iki taraftaki sandıklara ulaş veya alt halkada eşleşme/special hazırla.
2. Yağa düzenli vuruş vererek yayılmayı durdur; yağ altındaki sandıkları açıp yan dolum yollarını genişlet.
3. Sapın fenerlerini erit, açığa çıkan çamuru temizle ve çene uçlarında kalan hedeflere special taşı.

Başlangıçta 29 açık normal taş hücresi elle sabitlidir; refill dört renk havuzundan rastgele gelir.
Hazır special ve ilave yardım paketi verilmez. `(4,1) ↔ (4,2)` takası, üstte dört sarı taşlık eşleşme sağlar:
special'ı oyuncu üretir. Üst ve alt bölgede ayrı açılış hamleleri vardır.

Orta sütunun üstündeki mask hole'undan dolum girişi vardır. Sap üç sütun, çene boğazı bir hücredir;
darboğaz üst ve alt bölgelerdeki normal eşleşmeleri ayırır, line/combo kullanımını değerli kılar.
Yan sütunlardaki sandıklar aşağılarının doğrudan dolumunu başlangıçta keser. Bu hücreler pinned taşlarla
dolu başlar; sandıklar açılana kadar yeniden dolum orta sütun ve çapraz akışa bağlıdır.
**Hole tek başına engel değildir:** alttaki halka deliği düşüş için geçirgendir; kolonun yukarısındaki
sandık ise dolumu gerçekten keser. Bütün sabit blocker'lar kırılabilir; kalıcı üretici engel yoktur.
Fenerler hareketlidir, açılışta bitişik olan desen oyun ilerledikçe değişebilir.

## Statik doğrulama ve sınırlar

99 elemanlı diziler, hole'larda boş içerik, bağlı tek maske, origin'ler, 14 katman kaydı,
hedef adetleri, dört renk havuzu ve global katalog/GUID bağlantısı kontrol edilir.
Açılışta hazır üçlü veya aynı renk 2×2 yoktur; yalnız açık normal taşlarla **8 geçerli takas**
vardır (üstte 4, altta 4). Hareketli fenerlerle yapılabilecek takaslar bu sayıya dahil değildir.
Kontrol statik dizilime aittir; runtime gravity/refill, kazanılabilirlik veya kazanma oranı ölçümü değildir.

Kullanılan engeller katalogda 146'dan önce açılmıştır. Yeni engel tanıtımı yapılmaz.
Performans açısından yeni gameplay/VFX kodu veya spawn sistemi eklenmez. Yağ yayılması mevcut servisin
tek hücre/hamle kuralı ve 10 canlı hücre tavanıyla sınırlandırılır; performans ölçümü yapılmadı.
Unity, bot, oynanış simülasyonu ve build çalıştırılmadı. 28 hamleyi kullanıcı oynayarak değerlendirecek;
ilk geri bildirimde üst çene uçlarına erişim ve halka bölgesinin yeniden dolumu özellikle izlenir.
