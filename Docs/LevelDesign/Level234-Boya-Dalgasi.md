# 234 — Boya Dalgası / Paint Wave

**2026-11-07 güncel hamle: 46.** Bu tur yalnız hamle ayarı yapıldı (24→46); aşağıdaki önceki tasarım notu ve eski görseller tarihsel değerler içerebilir. [Güncel denge incelemesi](Reviews/225-234-Balance-2026-11-07/Review.md).

**Aktif revizyon: v2-layered,2026-11-07.** Kullanıcı ilk taslağı oynayıp kolay buldu; grid için “güzel” dedi. Dolayısıyla8×11maske ve74aktif hücre aynen korundu.24hamle de değişmedi; zorluk katmanlar veikinci çalışma yüzüyle artırıldı. Yeni sürüm henüz oynanmadı.

Önce seçilip paylaşılan fikir: dalga biçimli gövde,kenarda açılıp yeni işe dönüşen atölye. V2’de yalnız sol sütuna yoğunlaşmak yerine sağ omuz da katmanlı iş taşır.176’nın V’si ve233’ün altU’su kopyalanmadı.

| Bölge | Katmanlar | Hedef |
| --- | --- | --- |
| Sol üst iki2×2kapak | ColorChest21→PaintCanBox63 |2renkli sandık+2boya kasası |
| Alt sol2×6kenar ve sağ omuz2×2 | SculptingStone28→PlasticTwoStage40→Mud25 |16oyma+16plastik+16çamur |

Renkli sandıklar ilk hitte açılır;sonra kalan dört renk ayrı işlerdir. Her boya kasasında9kova vardır,normal isabet1,special isabeti2kova düşürür. Oyma hücresi3hit,orta plastik2hit,alt çamur2hit ister. Hedef sayıları hücre/engel sayısıdır,hit sayısı değildir.5goal sınırı korunur. Tek-hit mavi/kırmızı plastikler bu revizyonda kaldırıldı;çim ailesi veColorCrate71–74yok.

Orta plastik hareketlidir:oyma örtüsü kalkınca düşerek kendi hücresindeki çamuru bırakabilir. Bu durumda çamur görünürken plastik başka yerde hâlâ temizlenmeyi bekler. Kesitler yerinde duran katman kimliklerini anlatır;plastiklerin aynı hücrede kalacağı garanti edilmez.

İlk karar iki farklı yüze dağılır: üst solda renk/kapak ilerletmek veya sağ omuz vealt kenarın3katmanını aşındırmak. Güçlü bir combo birden fazla işi ortak ilerletebilir;bu fayda katmanlardan bağımsız zorluk gibi sayılmadı. Örneğin iki sıradan hasar geçişi üç-hit oyma taşlarını henüz bitirmez;üst katman kalksa bile alttaiki-hit plastik veiki-hit çamur vardır. Kapağın açılması da boya işini tamamlamaz. Yeni toplam ham hücre-hit yükü140’tır fakat aynı isabet birçok hücreyi vurabilir;bu140oyuncu hamlesi veya ölçülmüş zorluk değildir.

Başlangıçta50normal yuva:49’u otomatik beslenir,yalnız(0,4)/index32Gear başlangıç taşıdır. Altındaki oyma kapağı destek olur. Kullanıcının istediği istisna budur;geri kalan çalışma alanı elle pinlenmedi,hazır special yok. Üst sol boya kasaları bitince bu tek gölge yuvası da doğal beslemeye katılır. Farklı açılış kesitlerinde50/58/66/74normal yuva erişimleri statik kontrol edildi. İlk taş doluluğu ile sonradan sürekli refill aynı iddia değildir.

Gerçek sprite açılış ve3katman kesiti incelendi. Normal renkler temsilidir;kesitler düşüş/oynanmış an göstermez. Güncel tekrar envanteri `Reviews/234-Paint-Wave-v2/`;v1’in iki sütun benzerliği veoynanış reddi `Batches/234-Paint-Wave/Revisions/v2-layered/Before/` içinde saklıdır. Şeklin benzersiz veya yeni sürümün dengeli olduğu iddia edilmez.

Mevcut LevelEditor isim/sprite,1×1/2×2footprint,katman sırası,erase/resize veGoals/PreLevel ikonları kullanılır;atlas alt-sprite’ı olan oyma ikonunun negatif fileID’si geçerlidir. İlave runtime/VFX/audio kodu yok;var olan kurulum/hasar efektleri veortak havuz/ses/teardown yolları sürer. Yeni performans kazancı ölçülmedi.

Yalnız234assetinin obstacle/layer/goal verileri değişti;maskenin tamamı,isim/GUID/meta,katalog veTR/EN isim eşleşmesi aynı.6109diğerAssets dosyası korunur;176kullanıcı tarafından tamamlanmıştır vebu çalışmanın dışındadır. Aynı oturumdaki kullanıcı kaynaklı yarım elma görseli de korunur. UnityUI/Play/build/bot-kaskat simülasyonu/yayın/commit yapılmadı;manuel test kullanıcıda.

[Güncel önizleme](Level234-Boya-Dalgasi.png) · [Üç katman kesiti](Batches/234-Paint-Wave/Layers.png) · [Doğrulama](Batches/234-Paint-Wave/Revisions/v2-layered/validation.json)
