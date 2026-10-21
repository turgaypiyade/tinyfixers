# Level 185 — Işık Merdiveni

Katalog185 → **LevelP_001890.asset**, chapter8. **9×11,78aktif/21hole,32hamle,4renk,Hard.** Kullanıcının açık görsel dönüşüm isteğiyle tasarlandı. [Önizleme](Level185-Isik-Merdiveni.png), [kalıcı referans](References/12-Aynali-Serit-Referansi.png).

```text
ooo......
oooLLLLLL
oooPPPPPP
oooLLLLLL
PPPPPPSSS
LLLLLL...
PPPPPP.GG
LLLSSS..G
PPP....GG
LLL.G.G.G
SSS.GGGG.
```

`o` otomatik normal taş; `.` hole; `L` Lantern75; `P` PlasticTwoStage40; `S` SculptingStone28; `G` SculptingSpecial39. Koordinatlar soldan/üstten bir tabanlıdır. Görsel9×11olarak okunup **x→8−x yatay ayna** uygulandı: ana merdiven sola iner, alttaki kıvrımlı şekil sağda kalır. Düşey yön ters çevrilmedi.

| Hedef | Adet | Açılış düzeni |
| --- | ---: | --- |
| Lantern75 | **24** | Yumurta alanının2/4/6/8/10.satırları;2vuruş |
| PlasticTwoStage40 | **21** | Yumurta alanının3/5/7/9.satırları;2vuruş |
| SculptingStone28 | **9** | 5.satır7–9.sütun,8.satır4–6.sütun,11.satır1–3.sütun;3vuruş |
| SculptingSpecial39 | **12** | Sağ alttaki küçük kıvrımlı hedef alanı;yalnız special |

Kullanıcının “yumurtalar yerine bir sıra fener bir sıra plastic two stages” isteği **yatay satır şeritleri** olarak uygulandı. Bu,182'ye özel fener sütunu tercihinin bütün levellara zorunlu olmadığı açık bir örnektir. “Diğer sarı yerler” referansın bej/sarı blok bantları olarak yorumlandı; başlangıç alanındaki normal sarı eşleşme taşları dört renk havuzunda kalır. Hiç EggBird eklenmedi; referanstaki yumurtaların olası yardım davranışı kopyalanmadı.

Alt mor engelin bizdeki karşılığı belirsiz olduğu için opsiyonel seçim soruldu; yanıt gelmeden açıklanan **pusula varsayımı** kullanıldı. Bu bölüm39ID'li special-only hedeflerden oluşur; referanstaki mor öğenin gerçek davranışı hakkında iddia yoktur. Kullanıcı farklı bir aile isterse yalnız bu alan ayrıca revize edilir, mevcut seçim onaylanmış sayılmaz.

Açılış solda3×4=12normaltaş alanıdır. Dar başlangıcı desteklemek için referanstakiTNTfikri korunup mevcut PulseCore, **(3,3)/origin20** ile engel sınırına yaklaştırıldı; normal renk pini yoktur. Referanstaki TNT'nin birebir hücresi kopyalanmadı. Amaç üstte ilk iki-hit şeritlere erişimi kolaylaştırmak; alt hedefleri otomatik bitirmek değil. Hazır special gol değildir. Başka yardımcı veya başlangıç combo'su yoktur.

Hedefler başlangıç engel adetlerine eşittir:24/21/9/12. Ek movable refill kotası tasarlanmadı. ID40hamle başına spawn kilidi ile ID75genericpasssınırı farklıdır, fakat bu tasarım hedefi başlangıcın üstüne çıkarmadığı için ek üretim işi yüklemez. Desen açılış kompozisyonudur;45hareketli engelin gravity/swap sonrası yatay şeritlerde kalması beklenmez.

Sağ alt alanın dört-yön komşulukta6+6iki parçası vardır; ana66hücrelik bölgeden ayrıdır. Bu **referans maskesinin** aynasıdır. Referansta iki köşe-temas(5/10) düğümü bulunur ve korunur; 181'deki kesintisiz çapraz boşluk talebinden farklıdır. Burada ilave köprü/hole ekleyip referans şekli değiştirilmedi. Paylaşılan border/yuvarlatma/theme/prefab/sahne kodu değişmedi. Mevcut hücre sprite'lı PNG gerçek Unity border ekran görüntüsü değildir; son köşe pikselleri Unity'de doğrulanmadı.

12normal başlangıç hücresinin tamamı doğrudan dikey dolum alır. Alt pusula adaları normal match alanı sayılmaz; special erişimi gerekir. LineH/LineV kodu hole/etkilenmeyen hücrede `continue` ederek bütün satır/sütunu dolaşır, beam hole'da bitmez. Her alt pusulanın sütununda üst ana bölgenin açılabilen hareketli hedefi vardır; orada kurulan dikey special alt hedefe ulaşabilir. Bu, garanti edilmiş çözüm hamle dizisi veya oynanış simülasyonu değildir.

**32hamle ilk ayar; Hard(sonu5) sınıfı korunur.** Yumurtalar yerine45iki-hit movable koymak referansın olası yardımcı zincirini sağlamaz; bu yüzden sırf görseli izlemek kazanılabilirlik kanıtı değildir. 32hamle ve tek başlangıçTNTsi bu yük için ilk tasarım kararıdır. %40–50geçiş veya bütün hedeflerin yeterli hamlede bittiği ölçülmedi.

Tekrar kontrolü1–184üzerindedir.39PorselenVitrin yoğun movable alanı/üst açılış,144MorTesisat kırpılmış engel silueti bakımından yakındır. 184'ün boya terası/iki kanalı kullanılmadı. Yeni siluet veya daha önce hiç kullanılmamış desen iddiası yoktur; bu level bilinçli olarak kullanıcının referansını ve belirli ayna/engel değişimini uygular. [İnceleme](Reviews/185-Mirror/Review.md), [karşılaştırma](Reviews/185-Mirror/Comparison.png).

Statik doğrulama geçti: yatay ayna herhücre,7dizi,mask/origin,tek-hücrefootprint,hedefler,pin,dolum,özelhedefgeometrisi,katalog/GUID/isim. UnityYAML Ruby ile ayrıştırıldı. Gerçek proje sprite'lı PNG incelendi. **2.403 önceki ayar/script/Resources dosyası**,179kullanıcıdüzeni ve184dahil hashilekorundu;1–184isimleri aynı. Ekli referans kalıcı kopyalandı.

Performans: yeni runtime/VFX/update/tarama yok.45movable mevcut AcquireTileObject/ReleaseTile havuzları ve TileView init/resetlifetime yolunu kullanır. Hareketli iki-hit hedeflere line isabeti mevcut TryRouteMovableToImpact yolundadır; ayrı normal tile silinmesiyle hayalet state oluşturmayan güncel kod korundu. BoardBreakFxService mevcut efekt havuzunu kullanır; GoalFlyFx'in ghostImage/GameObject/Shadowoluşturma/yoketme maliyeti sürer. Genericmovablehedef sayımı board taramalarını kullanmaya devam eder. Yeni coroutine veya havuz eklenmedi; cihazprofili/canlıiptaltesti yapılmadı, performans kazancı iddia edilmez.

Authoring,snapshot,referansgrid,doğrulama `Batches/185/` içinde. UnityPlay/build,C#build,bot/kaskatsimülasyonu yok.186istenmedi.
