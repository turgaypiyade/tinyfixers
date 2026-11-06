# PatchBot special kombosu — zayıf duvar hücresi / un kaynağı

2026-11-06. Kullanıcı tek PatchBot'un duvarı doğru hedeflediğini, PatchBot+special kombosunun en zayıf hücreyi seçmediğini netleştirdi. Ardından değirmenin un hedefinin kaynağı olarak hedef sayılmasını ve mevcut undan önce hedeflenmesini istedi. Level tasarımı kullanıcıdadır; hiçbir level düzeni değişmedi.

## Son davranış

- Wall/MetalWall parçası kombo hedefi olarak seçildiğinde yalnız en yüksek çatlak aşamasındaki **vurulabilir** hücreleri adaydır. Örtülü, pasif veya ortak rezervasyon kapasitesi bitmiş hücreler uygunluk filtresini geçmez. Eşit aşamada payload'ın gerçek etki alanı karşılaştırılır.
- LineH/LineV/Pulse taşıyan botun hedef, görsel iniş ve Pulse patlama merkezi aynı duvar hücresinde kalır. Dikdörtgen duvarın merkezine geri taşınmaz. Tek PatchBot'un mevcut zayıf hücre seçim algoritması değiştirilmedi.
- Duvar veya kullanılabilir un kaynağı varsa sıradan dolgu taşı kombo merkezini kendine çekmez. Gerçek taş hedefleri, cargo açma hücreleri ve jel hedefleri geçerli seçenek olmaya devam eder; diğer engellerin alan faydası karşılaştırması korunur.
- Aktif Flour hedefi, Windmill'i de hedef engel grubuna alır. Tek botta bloklayan hedef değirmen, taş altındaki undan önce değerlendirilir. Alan kombosunda uygun/rezerve edilmemiş değirmen varken doğrudan Flour hücresi hedef adayı yapılmaz. Değirmen kalmayınca veya uygun değirmen kapasitesi uçan botlara ayrılmışsa kalan un yeniden seçilebilir. Başka faydalı bir payload'ın arada unu da temizlemesi engellenmez.
- HUD'a Windmill hedefi eklenmez; mevcut un hedefi ve miktarı aynıdır. Değirmen diğer bütün hedeflerden önce gelecek sabit birincilik kazanmaz; diğer hedef engellerle birlikte değerlendirilir.

## Neden bozulmuştu?

Tek botun koordinatörü zaten duvar aşamalarını okuyordu. Taşınan Line/Pulse için kullanılan ayrı `PatchBotImpactScorer` ise bütün duvar hücrelerini aynı değerle puanlıyor, çatlak aşamasını karşılaştırmıyordu. Ayrıca Pulse değerlendirmesi ve gerçek sanal Pulse, dikdörtgen parçayı merkezliyordu; dash görseli de aynı merkeze kayıyordu. Un hedefi yalnız Flour kimliğini işaretliyor, Windmill'i kaynak hedef olarak ilişkilendirmiyordu.

## Değişiklikler ve performans

Yalnız dört üretim dosyası: `PatchBotImpactScorer.cs`, `PatchBotTargetCoordinator.cs`, `PatchbotDashUI.cs`, `SpecialChainRunner.cs`. `Before/` bu tur öncesi kopyaları içerir.

Mevcut Cell/Footprint snapshot kayıtlarına aşama ve taş hedefi bilgisi eklendi; mevcut diziler ve listeler yeniden kullanılır. Duvar servisi hedef seçimi başına en çok bir kez bulunur; aşama bilgisi mevcut tek snapshot taramasında okunur. Uçuş tick'ine aşama veya tahta taraması eklenmedi. Wall/MetalWall görselinde merkez için yapılan tam tahta taraması erken dönüşle atlanır. Yeni coroutine, update döngüsü, GameObject, material, pool veya AudioSource yoktur. Ses, hasar miktarı, düşüş/akış, efekt ömrü ve temizliği değiştirilmedi. CPU/GC/FPS ölçülmedi.

## Gerçek yöntem kontrolleri

- `csi Tools/check_patchbot_wall_weakpoint.csx --before`: eski üretim puanlayıcısının tek zayıf hücreyi dikkate almaması, eski dash ve sanal Pulse'ın merkez kaydırması tekrar üretildi.
- `csi Tools/check_patchbot_wall_weakpoint.csx`: **260** gerçek seçim/uygunluk/rezervasyon/intent/aim/Pulse merkezi kontrolü. Wall/MetalWall, dikdörtgen ve iki farklı delikli yol, dolgu taşlı/boş alanlar, solo/LineH/LineV/Pulse, örtülü zayıf nokta, değişen çatlak, un kaynağı önceliği, değirmen bittikten sonra un, ortak rezervasyon bırakma, snapshot dizisi ve scorer yeniden kullanımı, uçuşta O(1) canlı uygunluk kontrolü.
- Aynı betiğin yüklediği `check_keygenerator_patchbot.csx`: **147** mevcut payload/cargo/helper/üretici/jel kontrolü geçti. Eski test doubles güncel enum ve servis tanımlarına getirildi; üretim enumları kaynaktan okunur.
- `csi Tools/check_patchbot_magnet.csx`: **85** mıknatıs uçları/tüp tabanı/Safe merkezleme/canlı hedef/iptal kontrolü geçti. Test enum'una Wall/MetalWall eklendi; mıknatıs davranışı değişmedi.
- **6052** diğer proje dosyası hash ile aynı: bütün level asset/meta, katalog, isim, editör, duvar servisi, ses ve düşüş/akış kodları korundu.

Kontroller gerçek üretim yöntemlerini sunum doubles ile çalıştırır. Unity build/Play/cihaz, bot/kaskat/düşüş simülasyonu, yayın, baseline veya commit yapılmadı. Cihazda davranış, denge ve performans henüz ölçülmedi.
