# Level 168 — Zincir Atölyesi

Katalog 168 → `LevelP_001720.asset`, chapter 7. **9×11 tam dikdörtgen, 99 aktif hücre, 32 hamle, dört renk.** Orta-zor ilk ayar. İstek: eski engelleri geri kullanarak daha çok kırılma, patlama ve hareket. [Görsel önizleme](Level168-Zincir-Atolyesi.png).

```text
ooooooooo
WWPoooPWW
WWPoooPWW
CCoooooCC
CCoooooCC
CoooooooC
ooooooooo
CCoooooCC
CCoooooCC
CCPBBEECC
CCPBBKKCC
```

`o` otomatik normal taş; `W` iki 2×2 Wardrobe (27); `P` PlasticTwoStage/fener (40); `C` Mud (25) → chest2 (5); `B` tek 2×2 OverrideBatteryBox (43); `E` yardımcı EggBird (45); `K` yardımcı RocketBasket (38). İndeksler sıfır tabanlı, y üstten aşağıdır. Hole yoktur.

## Hareket akışı

Üstte iki dolabın kapakları düşer; içerideki eşyalar sırayla takla atıp dökülür. Mevcut kütüphanede **dolap başına 10 eşya**, toplam 20 vardır. İlk vuruş kapağı açar; açık dolapta normal vuruş bir, special iki eşya çıkarır. Goal 20 eşya değil iki dolaptır. Yanındaki turuncu fenerler iki aşamada kırılır ve dolap yanına eşleşme alanı açar.

Yanlardaki **26 iki katlı sandık** kırıldıkça **26 çamur** ortaya çıkar: her kırılma yeni bir temizleme aşaması açar. Çamur tabandadır, sandık `stackOrder=1` örtüdür. Beş hücre genişliğinde üretim alanları ve tam genişlikte orta sıra, büyük eşleşme/special kurmaya alan bırakır. Bu tasarım normal eşleşmeyle de ilerler; yalnız special bekleyen Rodos/metal/tüp zinciri kullanılmaz.

Altta tek, bitişik yardım bölgesi vardır:

- **Batarya origin 84**, (3,9), 2×2. Mevcut ayarda dört rengin her biri üç şarj ister; tamamlanınca tahtaya bir vuruşluk dalga verir ve specials zincirlenebilir. Dolapları veya çok aşamalı bütün engelleri tek dalgada bitireceği varsayılmaz.
- **İki yumurta 86/87**, bataryanın sağında. İki isabetle çatlayıp açılır; kuşlar uygun hedeflere dalar. Belirli hedef veya eksiksiz aynı zincir garanti edilmez.
- **İki roket sepeti 95/96**, yumurtaların hemen altında. Toplam altı roket. Bu levelde bilinçli olarak `rocketBasketFireAllOnHit=1`: bir tetikleme kalan yüklü roketleri birlikte salar. Normal eşleşme yine yüklü renklerden biriyle eşleşmelidir. İki üçlü salvo hareket ve kırılma ödülüdür.

Yardımcıların sırası oyuncunun eşleşmelerine bağlıdır; sabit senaryo değildir. Yumurtaları açmak sepetlerin üstünde çalışma alanı bırakır; roketler ve batarya başka hedefleri/special'ları etkileyebilir. Yardımcılar ayrı goal değildir. Güçlü yardım, 26 katmanlı sandık/çamur ve dolap içerikleriyle dengelenir; zorluk yalnız düşük hamleye yüklenmez.

## Hedefler ve dolum

| Hedef | Adet |
| --- | ---: |
| Wardrobe | 2 |
| chest2 | 26 |
| Mud | 26 |
| PlasticTwoStage | 6 |

Dolap origin'leri **9/16**, batarya origin'i **84**. Fenerler **11/15/20/24/83/92** indekslerindedir. **51 normal taş hücresi** başlangıç engelleri dururken olası spawn/köşe yoluna sahiptir; 48 fiziksel hücre başlangıçta engellidir. `pinnedTileTypes` ve `pinnedSpecialTypes` sıfırdır; taşlar tamamen otomatik, hazır special yoktur. Refill dört renk kullanır; bataryanın renklerinden biri havuzdan çıkarılmaz.

Merkez sıra y=6 tamamen açıktır. y=3/4/7/8'de x=2…6 beşli alanlar vardır. Üst sandık bölgeleri y=5'te dış kenara daralarak alt boşluklara köşe dolumu bırakır; engel arkasında başlangıçta doldurulamayacak normal cep yoktur. Batarya ve sepetler alt kenara oturur, altında bekleyen normal hücre bulunmaz. Hareketli fener/yumurtalar oyun içinde yer değiştirebilir.

## Doğrulama ve performans sınırı

`python3 Tools/check_level168.py`: yedi 99 elemanlı dizi, footprint/origin'ler, 26 sıvı-taban/katı-örtü, hedef semantiği, yardımcı bölgesi, salvo ayarı, sıfır pinler, 51 taş hücresinin dolum erişimi, tanıtım sırası ve katalog/GUID kontrol edildi. Bütün engeller 168'den önce tanıtılmıştır. Önceki **344 asset/ayar dosyası hash ile aynı**, katalog öneki korunur; 167 değişmez. Yeni asset/katalog Unity YAML olarak ayrıştırıldı. 167 kontrolü de geçti; tarihsel katalog hash kontrolü sonraki level eklemelerine izin verirken 167'ye kadar olan öneki birebir doğrular.

Yeni runtime/VFX kodu, update döngüsü veya kare başı tarama eklenmedi. Sık kırılan sandık ve fenerler mevcut taş/kırılma havuzlarını kullanır. İki dolap mevcut kapak/eşya Image'larını kurup düşüş sonunda yok eder; kapak yarıları mevcut Sprite.Create yolundadır ve bu task'ta havuza taşınmadı. Bu mevcut maliyet iki dolapla sınırlıdır.

İki yumurta mevcut hatch başına aday List/HashSet taraması ve uçuş görseli kurar; action finally uçuşu yok edip job'u bırakır, view OnDisable uçuş sesini durdurur. Roketler mevcut PatchBot uçuş yolunu ve finally uçuş sayacı temizliğini kullanır. Tek batarya mevcut tek detonasyon dalgası, origin deduplication, yeniden kullanılan servis ve gecikmeli darbe yollarını kullanır; dalga içindeki List/HashSet/efekt maliyetleri korunur. Bunlar yeni havuzlama veya ölçülmüş performans iyileştirmesi olarak sunulmaz. Cihaz maliyeti ve canlı iptal/sahne çıkışı ölçülmedi.

**32 hamle ilk denge ayarıdır; gerçek kazanma oranı ölçülmedi.** Unity Play/build, bot ve oyun/kaskat simülasyonu çalıştırılmadı. Önizleme mevcut sprite'larla yerleşim şemasıdır, normal renkler temsili; alttaki mini tahta sandıkların altındaki çamuru gösterir.
