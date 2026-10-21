# 181 tekrar incelemesi — 2026-10-20

**Sonraki açıklama:** Kullanıcı “şekle çok itiraz etmiyorum ama ... bu level için çok kolay” dedi. Bu rapor v1'in benzerlik kanıtıdır; yalnız şekil yüzünden levelin bırakılması gerektiği şeklinde okunmaz. v1 saklandı; basamak korunup hedef/katman yükü artırılan [v2](../../Level181-Basamakli-Arsiv.md) güncel tasarımdır. Aşağıdaki ilk ret değerlendirmesi tarihsel kayıt olarak tutulmuştur.

Kullanıcı: **“bunu yapmıştık benzerleri var.. lütfen sistemdekileri bir kontrol et”**. Basamaklı Arşiv bu haliyle reddedildi; önceki “yeni kompozisyon” değerlendirmesi yeterli tarihsel karşılaştırmaya dayanmıyordu.

[Gerçek sprite'larla yan yana karşılaştırma](Comparison.png).

Katalogdaki **1–180'in tamamı**, gerçek katalog numarası/GUID ile assetlere çözülerek tarandı. 181 karşılaştırma adayı olarak ayrıca okundu. Maskeler, başlangıç engelleri, origin'ler, katman kayıtları, Safe/AncientSeal, geçerli Tube yolları, Magnet yolları, hedefler ve engel aileleri envantere alındı. Beş toplu yerleşim haritası incelendi; seçili altı board gerçek sprite'larla yeniden render edilip kontrol edildi. Bu, bütün levelların oyun içi tek tek oynanması veya bütün özel görünümlerin birebir runtime render'ı değildir.

| Mevcut level | Asset | Somut ortak fikir | 181'deki farkın sınırı |
| --- | --- | --- | --- |
| **7 — Sandık Koridoru** | `LevelP_00080.asset` | Sol kenardan aşağı doğru genişleyen çapraz basamak; sağ/üst taraftan açılır | 181'de basamaklar 2×2 büyüklüğünde ve altta yoğunlaşıyor; basamak fikri yeni değil |
| **23 — Enerji Şöleni** | `LevelP_00260.asset` | Sol ve alt kenarı dolduran tek kütle; üst sağda açık çalışma alanı | Kaynaklar farklı olsa da açık alan/engel kütlesi ilişkisi tekrar ediyor |
| **48 — Çamurlu Fenerler** | `LevelP_00540.asset` | Açık üst köşe, alt ve karşı kenarı kaplayan L kütlesi; örtü arkasında çamur | Katman ve kenara bağlı kütle birlikteliği zaten var |
| **68 — Fenerli Rıhtım** | `LevelP_00770.asset` | Solda basamaklı engel sınırı, sağda dikey çalışma/boru bölgesi | 181'de borular yok; basamaklı kenar düzeni tekrar ediyor |
| **121 — Jel Kartuşları** | `LevelP_001260.asset` | **ColorChest + Wardrobe + SculptingStone** aynı levelde | 121'de ayrı bantlar ve jel kartuşları var; 181'de yığınlar var. Aynı aileleri seçmek tek başına yeni oynanış fikri değil |
| **66 — Saklı Kasa** | `LevelP_00740.asset` | Büyük alt bölge ve dolaplarla üstteki çalışma alanının ayrılması; kasa arkasında içerik | Geometrik tarama bunu da aday çıkardı; basamak benzerliğinden çok bölge/açılma ailesi ortak |

**Sonuç:** Kullanıcının tekrar eleştirisi destekleniyor. 181, mevcut motiflerin farklı boyut ve engellerle birleştirilmesi; bunu yeterince yeni bir tasarım gibi sunmamak gerekir. ColorChest'in son kullanımının 154 olması bu değerlendirmeyi değiştirmiyor. 121'de ortak üçlü zaten mevcut; 176'da da yakın zamanda dolap kullanılmış.

Bir sonraki 181 denemesi için çıkarım: yalnız yönü çevirmek, basamağı büyütmek, başka dolap/sandık koymak veya tekrar çamur saklamak yeterli değildir. Oyuncunun ilk kararı ve açılan bölgenin sağladığı fayda değişmeli. ColorCrate 71–74 şimdilik kullanılmaz. Bu inceleme sırasında yeni bir level alternatifi yazılmadı; 182'ye geçilmedi. Reddedilen 181 asseti karşı örnek ve revizyon tabanı olarak korundu; katalogdan silinmedi. 1–180 değiştirilmedi.

## Tekrarlanabilir tarama

```sh
python3 Tools/review_level_patterns.py --candidate 181 --output Docs/LevelDesign/Reviews/181-Repetition
python3 Docs/LevelDesign/Reviews/181-Repetition/render_comparison.py
```

`Catalog-Inventory.json` tüm kaynak yerleşim verisini; `Similarity.json` adayları ve sınırları tutar. `Catalog-001-036.png` ile başlayan beş harita 1–180'i kapsar. `Comparison.png` gerçek sprite'lı seçili karşılaştırmadır; normal taş renkleri temsilidir.

Geometrik aday araması iki ayrı karşılaştırma kullanır: board üzerindeki konumlar ve engel kütlesinin kendi sınırlarına kırpılmış silueti. Boyutlar normalize edilir, aynı yön ve aynalanmış yönler incelenir. Sayısal IoU bir **aday bulma ölçütüdür**; algılanan benzerlik, özgünlük veya geçiş oranı yüzdesi değildir. Örneğin sadece board koordinatlarına bakmak, 181'in daha küçük basamağının 7'deki büyük basamakla ilişkisini geri sıralara atıyordu; kırpılmış şekil ve görsel inceleme bunu yakaladı.

Eski kayıtlarda sınır dışında kalmış Tube/overlay girdileri de var; bunlar `geometryWarnings` içinde tutuldu, bu tasarım incelemesinde düzeltilmedi. Runtime'daki Tube `GetCellIndices` sınır kontrolü izlendi; geçersiz yol şekil hesabına katılmadı. Başlangıçtaki grass/movable görsellerinin ayrıntısı, dinamik yayılma, düşüş, hasar ve oyun dengesi yalnız bu haritalardan çıkarılamaz. Unity Play/build veya oyun/bot/kaskat simülasyonu çalıştırılmadı.
