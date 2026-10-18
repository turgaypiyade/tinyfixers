# Level 161 — Rodos Atölyesi

`LevelCatalogPro` global 161 → `LevelP_001650.asset`, chapter 7. **9×11 tam dikdörtgen, 28 hamle, dört renk.** Rodos Heykeli'nin kolay ilk production tanıtımı. [Yerleşim önizlemesi](Level161-Rodos-Atolyesi.png) · [161–162 birlikte](Levels161-162-Onizleme.png).

```text
ooooooooo
ooooooooo
ooooVoooo
ooooooooo
ooooooooo
ooooooooo
oHooRRooo
ooooRRooo
ooooooooo
oCCCCCCCo
ooCCCCCoo
```

`o` otomatik taş · `V` hazır LineV · `H` hazır LineH · `R` tek 2×2 RhodesStatue (70) · `C` chest1 (4). Bütün noktalar/hücreler aktiftir; hole yoktur.

| Hedef | Adet | Yerleşim |
| --- | ---: | --- |
| RhodesStatue | 1 | (4,6), origin 58; 58/59/67/68 hücreleri aynı origin |
| chest1 | 12 | Alt iki sırada bitişik yedi/beş sandıktan sıcak taban |

Heykel başlangıçtan görünür ve örtüsüzdür. Her special isabetinde bir aşaması parçalanır; **dört special vuruşuyla bir heykel hedefi** tamamlanır. Normal eşleşme ve doğrudan booster hasar vermez. 2×2'de dört hücreye değen tek etki dört ayrı vuruş olarak varsayılmaz. Güncel kılıç/kalkan katmanları ve mevcut vuruş animasyonları kullanılır.

İki hazır çizgi öğretimi kolaylaştırır: LineV (4,2; indeks 22) heykelin sütununda, LineH (1,6; indeks 55) heykelin satırındadır. İkisi ayrı konumdadır; tek zorunlu combo/otomatik gösteri tasarlanmadı. Oyuncu özel taşları kullanıp heykelin aşamalarını görebilir; kalan vuruşlar için geniş tahtada yeni special üretir. Hazır çizgilerin ikisinin de heykeli dört vuruşta tek başına bitireceği varsayılmaz. Sandıklar tek vuruşlu ve yalnız tabandadır; çamur/jel/kapak eklenerek yeni engelin tanıtımı ağırlaştırılmaz.

**Tutorial zaten mevcut:** `ObstacleHintLibrary` ID 70 ve TR/EN `hint_obstacle_rhodes_statue` / `_name` kayıtları doğrulandı. `01_Game` sahnesindeki açık `ObstacleHintManager`, başlangıç `obstacles[]` içindeki 70'i toplar, ilk karşılaşmada oyunu durdurup açıklamayı gösterir ve onaydan sonra heykeli vurgular. Ek tutorial adımı/runtime kodu gerekmez; `tutorialSteps=[]`. İpucunu daha önce testte görmüş oyuncunun `obstacle_hint_seen_70` kaydı silinmez; ilk karşılaşma kuralı korunur.

99 aktif hücre: 83 taş hücresi (iki hazır special dahil), dört heykel hücresi ve 12 sandık. Diğer **81 taşın rengi sabitlenmez**; mevcut otomatik dolum kullanılır. Yalnız hazır special'ların alt renkleri Gear/Core olarak iki `pinnedTileTypes` girdisidir; `pinnedSpecialTypes` aynı iki hücreyi tutar. İlk rastgele açılış için belirli bir normal takas sayısı iddia edilmez.

Yedi hücre dizisi 99 eleman. Footprint/origin, hedef, katalog/GUID, dört special-only stage, tutorial kayıtları ve tüm 83 taş hücresinin başlangıç engelleri yerindeyken olası spawn/izinli köşe erişimi statik kontrol edildi. Bu kontrol gravity/kazanma simülasyonu değildir.

Yeni gameplay/VFX kodu eklenmedi. Bir mevcut RhodesStatueView kökü iki kılıç/kalkan Image'ını kurulumda oluşturup aşamalar arasında kullanır; `OnDisable` vuruş coroutine'ini durdurup parça pozlarını sıfırlar. Standart taş kırılma havuzu/ses yolu kullanılır. Yeni kare başı tarama veya üretim eklenmedi; cihaz performansı ölçülmedi.

**28 hamle kolay tanıtım için ilk ayardır; gerçek kazanma oranı ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizlemedeki normal renkler temsili; iki hazır special ve engel yerleri gerçek assetten gelir. Rodos, mevcut runtime Resources görselleri ve parça konumlarıyla gösterilmiştir.
