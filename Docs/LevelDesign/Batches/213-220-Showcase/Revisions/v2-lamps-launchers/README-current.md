# 213–220 · Güncel dağılım (v2)

Kullanıcı219’da çim altındaki ColorChest ampullerinin görünmediğini bildirdi; ayrıca LampStrip’i217 ve bir başka seviyede, GelLauncher’ı iki seviyede istedi. Plastik renk varyantlarının kaldırılmasına ve gerektiğinde maskeye müdahaleye izin verdi.

| Seviye | Güncel ana değişiklik | Hamle |
|---|---|---|
|213 Işıklı Balon|Sepetin iki kenarında yukarı ateşleyen2 kartuş; Jel61 hedefi|24 (22→24)|
|215 Vitray Atölyesi · Hard|Renkli plastikler kalktı; iki sütun ve çim kaplı4 sandık; dipte4 kartuş; Jel99|26|
|217 Kanatlı Kum Saati|Yeşil plastik yerine iki kayan L lamba çerçevesi|24|
|218 Kıvrılan Nehir|Mavi/kırmızı plastik yerine iki C çerçeve; sol sabit/sağ kayan; kanal aradan|25|

**214,216,219,220 level assetleri;1–212; bütün meta/GUID’ler, katalog ve TR/EN adlar bu tur korunmuştur.**219’un hata düzeltmesi runtime görünüm kaydındadır; leveli değiştirilmedi. Özgün balon/kum saati/nehir maskeleri korundu.215 aynı9×11 dikdörtgende yeniden düzenlendi.

## ColorChest görünüm düzeltmesi

Grass yok edilmesi aynı origin’deki önceden kurulmuş `_chestViews` kaydını kaldırıyordu. Artık yalnız gerçek ColorChest yok edilmesi kaldırır. Açılma ve ampul silme olayları mevcut prefab’a ulaşır; yeni view/child üretimi veya tarama eklenmez. Bkz. `Docs/Checks/ColorChest-Cover-Reveal.md`.Eski hata izole testte üretildi; yeni384 kontrol/48sıra ve önceki413/26/6 regresyon kontrolleri geçti.

## Veri / mekanik sınırları

- LampStrip yalnız217/218’de var; iki levelde de2 frame hedeflenir.217’nin iki L’si hareketli;218’in C’lerinden sol sabit, sağ hareketli. Yollar dallanmıyor; her şeritte bir mor lamba special gerektirir. Renkler dört taş rengiyle uyumlu.
- GelLauncher yalnız213/215’te var; sırasıyla2 ve4 kartuş. Hedef olmayan yardımcıdır. Gel46 hedefleri tüm aktif alan61/99; bu iki boardda kalıcı üretici veya su kaynağı yok. Yardım ateşi kendi hücrelerini ve yolu jeller; geri kalanı eşleşme/special ile yayılır. Jel gösterim önizlemede ateşlenmiş gibi uydurulmadı.
- 213’te dikey kartuşlar sepetin iki yanına bitişik; yukarı atış aradaki mask hole’larını geçer. `GelLauncherFireAction.BuildPath` koordinatları board sınırına kadar toplar; hole hücreleri darbede atlanır, yol orada bitmez.
- 215’te dört yatay kartuş tek alt bantta, merkez açıklığına bakar. Üzerlerindeki iki sandık sütunu hem iç hem dış besleme yüzüne sahip. Açılışta51 normal yuva doğal dolar; pin gerekmiyor. Kartuşların tek başına veya sandıkların tek başına kaldırıldığı statik uçlar da kontrol edildi.
- 218 kanal19 hücreden oluşur (kaynak dahil). Lamba çerçevesiyle kesişmiyor, son adımı alt sınırda aşağı. Kalıcı kuş kıyısı /24kuş ve iki sandık korundu.
- Her level en fazla5goal. Plastik9/11/12/13 seçilen dört levelden kalktı; PlasticTwo40 ayrı iki-hit aile olarak uygun yerlerde kaldı. 221 veya sonrası için ek seviye/yayın yetkisi çıkarılmadı.
- Authoring snapshot’ları yerel geri alma/veri koruma kaydıdır; **app release baseline değildir**. V1 yazıcısı/denetçisi aktif revizyonu ezmeyecek şekilde yönlendirildi. `revise.py` seçili assetlerin son yazım hash’i farklıysa durur; sonradan kullanıcı değişikliği sessizce geri alınmaz.

## Kontroller ve performans

Bağımsız Ruby/Psych YAML okuması; dizi/mask/footprint/origin/katman/goals; şerit bağlantısı/degree/renk/parça ayarı; jel hedefi ve kartuş yönü; kanal ayrıklığı; gerçek pinler; açılış, kartuş, sandık ve tüm kaldırılabilir katmanların temizlendiği geometrik kontrol noktaları.2989 kapsam dışı Settings/Scripts/Resources dosyası aynı. Hareketli engeller sabitlenmiş statik kesitlerde yeni açılan alt hücrelerin gerçek gravity sonucu olduğu iddia edilmez; modeldeki sınır ayrı raporlanır.

Yeni runtime/VFX eklenmedi. LampStripService mevcut tek Update ve tekrar kullanılan hücre listelerini kullanır; Clear/OnDestroy shift/fall işleri, hold/job ve state aboneliklerini temizler. GelLauncherPressView mevcut ortak driver/moving listesini, cap/gel-görsel havuzlarını kullanır; FireAction finally blokları alt coroutine’leri durdurur, havuz görüntülerini iade eder ve flow hold/job’ları bırakır. Ateş yolunda mevcut List/impact-pass maliyetleri ve başlangıç görünüm kurulumları vardır. ColorChest düzeltmesi mevcut view’ı korur; kapak efektlerinin mevcut allocation maliyetleri değişmedi. Sesler merkezi board audio yolunda kaldı. Cihaz FPS/denge ölçümü veya performans artışı iddiası yok.

Güncel proje sprite’larıyla dört revizyon PNG’si ve toplu sayfa açılıp incelendi. Press çizimi güncel Editor preview’ındaki frame/gel/head UV crop ve yönleriyle, LampStrip çizimi güncel auxiliary sprite’larla yapıldı. Temsili serbest renkler ve statik katman kesitleri işaretli. Tekrar kısa listesi `Reviews/213-220-Showcase-v2/` altındadır; skorlar yenilik/onay yüzdesi değildir.

**Unity build/Play, bot/kaskat/gravity simülasyonu, cihaz oyunu, upload/publish yapılmadı.**24/26/24/25hamle ilk revizyon ayarlarıdır; kullanıcı denemesi beklenir.
