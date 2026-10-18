# Level 153 — İki Avlu

`LevelCatalogPro` global 153 → `LevelP_001570.asset`, chapter 7. Kullanıcının ikinci görselindeki L biçimli 9×9 tahta; 32 hamle ve dört renk. Görsel: [önizleme](Level153-Iki-Avlu.png).

```text
....PPPPP
....PoooP
....PoooP
....PoooP
PPPPPPPPP
PoooPXXXX
PoooPXXXX
PoooPXXXX
PPPPPXXXX
```

`.` hole · `o` sabit başlangıç taşı · `P` PlasticTwoStage (40) · `X` SculptingSpecial (39).

| Hedef | Adet | Yerleşim |
| --- | ---: | --- |
| PlasticTwoStage | 31 | Karpuzların yerine, iki avluyu çevreleyen kesintisiz iki aşamalı bant |
| SculptingSpecial | 16 | Sağ alttaki 4×4 blok; mevcut kütüphanede tek special vuruşuyla kırılır |

Üst sağ ve alt sol 3×3 avlunun 18 başlangıç taşı elle sabitlendi. Refill dört renkten rastgele devam eder. Hazır special yoktur. Her iki avluda da açılışta üçlü veya aynı renk 2×2 yoktur; toplam dokuz geçerli komşu takası vardır. Referanstaki tek renkli iç alanlar doğrudan kopyalanmadı; kendiliğinden temizlenmeyen ve oyuncuya hamle veren dağılım kullanıldı.

Oyuncu normal eşleşmelerle turuncu bandı açıp daha geniş special üretme alanı kazanır. Alt sol avludan yatay, üst sağdan dikey special’larla sağ alttaki hedeflere ulaşılabilir. Normal eşleşme SculptingSpecial’a hasar vermez; bu kural kütüphanedeki `SpecialOnly` stage’den doğrulandı. Yardımcı EggBird/sepet eklenmedi.

65 aktif hücre, sol üstte 16 hole vardır. Yedi hücre dizisi 81 eleman. Tüm engeller 1×1 ve origin’leri kendi indeksidir; yığın katmanı yoktur. Maske bağlıdır. Sabit special taşlarının altında normal hücre kalmaz; iki normal avlunun yukarıdan beslenme yolu hareketli PlasticTwoStage dışında kalıcı engelle kesilmez. Sol avlu üstündeki hole’lar mevcut spawn akışında geçirgendir.

Dizi uzunlukları, maske, origin’ler, hedef adetleri, sabit renkler, açılış eşleşmeleri/takasları, sprite ve katalog bağlantıları statik kontrol edildi. Önceki level assetleri ile ortak ObstacleLibrary korunur. Yeni runtime/VFX kodu veya tarama eklenmedi; mevcut engel hasarı, gravity ve efekt temizliği kullanılır. Cihaz performansı ölçülmedi.

32 hamle ilk deneme ayarıdır; kazanılabilirlik/kazanma oranı ölçülmedi. Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme yerleşim şemasıdır, oyun ekran görüntüsü değildir.
