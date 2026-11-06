# FlyFruit sesleri ve sinek boyutu · 2026-11-06

Kullanıcının beş WAV dosyası Sources altında birebir yedeklendi; Downloads dosyaları değiştirilmedi. İşlenmiş kopyalar Assets/_Project/Audio/SFX/FlyFruit altında. Baş/son sessizlikleri temizlendi, mono48kHz PCM16 dönüştürüldü, kısa giriş/çıkış yumuşatıldı. Mevcut nilüfer, porselen, sandık ve fırlatma efektlerinin RMS/tepe değerleri referans alındı; bu bir işitsel oyun testi değildir.

| Olay | Dosya | Süre | Library volume |
|---|---|---:|---:|
| Elma ısırma | AppleBite |0.367s|0.82|
| Yumurta kırılma | EggBreak |0.367s|0.85|
| Yumurta fırlatma | EggFlight |0.372s|1.00|
| Sinek uçuş döngüsü | FlyBuzzLoop |0.8665s|0.90|
| Kutu tamamen kırılma | BoxBreak |0.395s|0.90|

Fırlatma/ısırma/kutu bitişi BoardAudioDirector.PlayOneShotClip üzerinden; yumurta kırılma mevcut BoardBreakFxService üzerinden çalar. Sinek sürüleri servis başına tek vızıltı lease paylaşır. BoardAudioDirector mevcut EventSfx havuzundan ses ödünç alır; engeller AudioSource oluşturmaz. Token sahipliği yeniden kullanılan sesi eski lease’in kapatmasını önler. SoundEnabled OFF aktif sesleri durdurur; ON yalnız devam eden uçuşun döngüsünü yeniden başlatır. Son sinek, Clear/OnDisable ve board teardown lease’i bırakır. Ses havuzu doluysa yeni kaynak oluşturulmaz.

Son kullanıcı düzeltmesi: sinekler küçük kalıyordu. Yükseliş EggBird gibi0.70s, ölçek0.85→1.75; ayrılmada1.20’ye toparlanır; hedef yolunda1.00 normal boyuta döner. Önceki0.48hedef ölçeği kaldırıldı. Mevcut ortak Update ve VFX havuzu korunur; yeni coroutine/efekt nesnesi eklenmedi. Motor FPS/GC ölçümü yapılmadı.

Doğrulama: check_flyfruit.csx354izole üretim-yöntemi kontrolü; check_flyfruit_audio.csx12merkezi havuz/ayar/sahiplik kontrolü; check_flyfruit_audio_assets.py WAV,GUID,orijinal ve level koruma denetimi. Bunlar Unity veya oynanış testi değildir. Kullanıcı manuel testi kendisi yapar. Bu devam bölümünde UI kontrolü, Unity build/Play, seviye değişikliği, yayın ve commit yapılmadı. processing.json ölçümleri ve validation.json koruma sonuçlarını içerir.

## Son uçuş düzeltmesi

Kullanıcı tepe ölçeğini2.1istedi:0.85→2.10→1.20→1.00. Her sürü başlangıcında yükseliş noktası;her sinek için ayrılma konumu,kavis,zamanlama,kanat fazı ve salınım bir kez rastgele seçilir. Üç sinek elmanın ayrı iç bölgelerine,footprint ile ölçeklenen küçük sapmalarla iner. Rastgelelik yalnız görseldir;aynı sürünün hedef meyvesi,lokma rezervasyonu ve hedef sayımı korunur. Parametreler mevcut sekiz sürünün tekrar kullanılan üçlü dizilerinde saklanır;frame başına rastgele örnekleme veya yeni nesne oluşturma eklenmedi.354izole yöntem kontrolü geçti;Unity/Play/UI testi yapılmadı.

## Kullanıcı ses ve yumurta uçuş geri bildirimi

Sinek vızıltısı düşük bulundu: dosya RMS-29→-22dB,volume0.75→0.90; toplam yaklaşık+8.6dB. Yumurta uçuşu dosya RMS-25.08→-20.5dB,volume0.80→1.00; toplam yaklaşık+6.5dB. Diğer üç sesin dosya ve seviye ayarı korunur. Yumurta sesi artık HitBox içinde special darbesiyle aynı anda değil, FlyEggProductionFx tarafından yükselişin0.12s anında merkezi PlayOneShotClip ile bir kez istenir. Clear/timeout/teardown kalan uçuşu iptal ettiğinden bekleyen ses ayrıca coroutine bırakmaz. Duyulmama sebebi cihazda ölçülmedi; special darbesinin altında kalma ihtimaline göre zamanlama ve seviye düzeltildi.

Yumurta0.65ölçekten0.42s yükselişte2.0ölçeğe büyür; hafif esneme/yatma,0.04s tepe ve0.62s kavisli inişte1.0normal boyuta dönüş. Hedef düşmeye başlayıp yeniden seçilince mevcut konum/boyut/açıdan devam eder. Havuzdan alınan görselin açısı sıfırlanır. Mevcut havuz ve tek Tick korunur; ek nesne/kaynak/coroutine eklenmedi.357izole üretim-yöntemi kontrolü ve güncel WAV/bağlantı/koruma denetimi geçti. Manuel Unity/oyun testi kullanıcıdadır.

## Son formasyon ve ses ayarı

Üçlü uçuş korunarak yatay formasyon aralığı0.65→1.05hücreye açıldı;ortadaki sinek biraz yukarıda,yanlar aşağıda. Her sineğin kendi küçük rastgele sapması var. Yükselişin ilk kısmından sonra formasyon kademeli açılır;tepedeki üç görsel aynı noktada üst üste binmez. Kavisler yanlara ayrılır ve elmadaki üç iniş bölgesi birbirinden biraz uzaklaştırıldı. Ekran kenarındaki sürünün merkezi için yatay pay artırıldı. Mevcut tek Update/havuz ve başlangıçta örneklenen parametreler korunur.

Kullanıcı sinek sesini beğenip biraz daha yüksek istedi:son sürüme göre+3dB,RMS-22→-19dB,library volume0.90aynı. Klip tepesi yaklaşık-11.4dB;orijinal kaynak ve diğer dört ses değişmedi.357izole yöntem kontrolü ve WAV/asset denetimi geçti. Unity/Play/UI kullanılmadı;görsel ve işitsel manuel test kullanıcıdadır.

Son kontrolde eski snapshot’a göre231vefont assetleri ayrıca değişmiş bulundu;bu dosyalara müdahale edilmedi. Tam koruma audit bu farkları raporladı. Güncel WAV/bağlantı denetimi açık --audio-only seçeneğiyle yürütülür ve audio-validation.json içine koruma auditinin bu sonuçtan hariç tutulduğunu yazar;eski snapshot/hashi benimsenmedi.
