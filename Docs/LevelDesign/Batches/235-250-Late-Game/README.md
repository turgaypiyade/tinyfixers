# 235–250 · Geç dönem tasarımları

**235 güncellemesi · 2026-11-07:** Ayrı kullanıcı talebiyle yalnız **235 v2** revize edildi: 9×11/85 aktif, **28 hamle ilk ayar**, 2 heykel/19 oyma/27 çamur. Güncel model `designs.json` içindeki 235 kaydıdır; [revizyon](../235-Copper-Arch-v2/README.md). Contact ve 235 önizlemesi güncellendi. Eski 235 sim girdileri/18 hamle/%35 sonuçları tarihsel kanıttır. İlk toplu `validation.json`/hash kayıtları yeni 235’i kapsamaz; ayrı doğrulama kullanılır. Yeni sürümün dengesi ölçülmedi.

Kullanıcı 225–234 denge işinden sonra 250’ye kadar yeni seviyeler istedi. Bu klasör yalnız 235–250’yi yazar. 1–234’ün level/meta dosyaları append öncesi hashleriyle korunur; 225–234’ün yetkili hamle ayarları ayrı inceleme kaydındadır.

Fikir önce seçildi: farklı açılış kararları, kenarla birleşen geçici engeller, geniş eşleşme yüzleri ve açıldığında işi devam eden katmanlar. Çim42/58/69 ve renkli sandık71–74 kullanılmadı. Normal renkler otomatik dolar; yalnız doğal dolumun erişmediği normal açılış yuvalarına sıradan taş pini kondu. Hazır special yok.

|Level|Ad|Tür|Hamle|Oyuncu hedefi|Hedef sayısı|
|---|---|---|---:|---|---:|
|235|[Bakır Kemer v2](../../Level235-Bakir-Kemer.md)|Hard|28 ilk ayar|%30 yönü; ölçülmedi|3|
|236|[Cam Ray](../../Level236-Cam-Ray.md)|Normal|16|%40–50|3|
|237|[Dört Vana](../../Level237-Dort-Vana.md)|Normal|26|%40–50|4|
|238|[Kuşlu Tribün](../../Level238-Kuslu-Tribun.md)|Normal|30|%40–50|4|
|239|[Baklava Kilidi](../../Level239-Baklava-Kilidi.md)|Super Hard|33|%30 yönü|4|
|240|[Yağmur Arenası](../../Level240-Yagmur-Arenasi.md)|Boss Duel|24|Boss ayrı değerlendirilir|5|
|241|[Enerji Rıhtımı](../../Level241-Enerji-Rihtimi.md)|Normal|24|%50–60|3|
|242|[Pil Kavşağı](../../Level242-Pil-Kavsagi.md)|Normal|21|%50–60|4|
|243|[Anahtar Yelkeni](../../Level243-Anahtar-Yelkeni.md)|Normal|30|%50–60|4|
|244|[Çifte Çıkış](../../Level244-Cifte-Cikis.md)|Normal|24|%50–60|4|
|245|[Mermer Çarkı](../../Level245-Mermer-Carki.md)|Hard|24|%30 yönü|3|
|246|[Modül Köprüsü](../../Level246-Modul-Koprusu.md)|Normal|25|%40–50|3|
|247|[Su Saati](../../Level247-Su-Saati.md)|Normal|16|%40–50|5|
|248|[Renkli Sahne](../../Level248-Renkli-Sahne.md)|Normal|17|%40–50|4|
|249|[Son Köprü](../../Level249-Son-Kopru.md)|Super Hard|22|%30 yönü|4|
|250|[Ustaların Meydanı](../../Level250-Ustalarin-Meydani.md)|Boss Duel|36|Boss ayrı değerlendirilir|5|

## Tanıtım ve editör

231–240’ta kullanıcının 231’de tanıttığı sinek kutusu/yumurta/elma akışı devam eder;235–240 yeni renk varyantlarını yeni mekanik diye saymaz.241’de EnergyContainer22,242’de BatteryBox23 V1’e girer: biri ortak enerji kotası, diğeri dört ayrı renk pilinin vuruşlarıdır.246’daki Omega24, mevcut iki vuruşlu hareketli engelin görsel varyantıdır; üçüncü bağımsız mekanik olarak sayılmaz. Böylece241–250’de iki yeni mekanik vardır.

Bunlar mevcut runtime/library engelleridir. LevelDataEditor paleti library.obstacles listesinden okunur;22/23/24 mevcut ad/sprite/footprint ile seçilip yerleştirilir. EnergyContainer için level bazlı enerji ayarı, BatteryBox için4×hits hedef önerisi, Omega için mevcut movable yerleştirme/erase/resize akışı kullanılır. Yeni hedef ikonları nonzero Sprite referanslarıyla doğrulandı. Fruit/Safe/Tube/Magnet/Channel boyut ve yol verileri mevcut editor modlarıyla eşleşir; runtime-only bir teslim değildir.

## Tekrar ve performans

`similarity.json` bütün önceki aktif V1 kataloğunu tarar. `Contact.png` ve16 gerçek sprite kartı görsel inceleme içindir. 241’in sade rıhtımı eski kenar kolonlarıyla ortak bir dikdörtgen kullanır; bu bilinçli tanıtım sadeleşmesidir.239 kasa altında Cargo yerine üretim açar;233’ün teslimat kilidini kopyalamaz.249’un C yolu233’ün kısa U’sundan daha uzundur ve dolap köprüsüyle farklı bir son iş taşır; aynı mıknatıs ailesi olduğu saklanmaz.

Tile/engel sayıları veriyle belirlenir; ek per-frame tarama kodu eklenmedi. Enerji uçuşları, kuşlar ve su saçılımı mevcut coroutine/efekt maliyetlerini taşır. Özellikle iki su deposunun saçılımı mevcut geçici liste/HashSet ve ekran damlası maliyetini kullanır; bu koşular cihaz CPU/GC/FPS ölçümü değildir. Sık efektler mevcut havuzları kullanır. Simülasyonun yeni booster hostu oyun başına tek dedicated injector kullanır, iptal/timeoutta temizlenir; booster görselleri için ayrı kaynak/havuz yaratılmadı.

## Kanıt ve sınır

`Sim-Smoke`:16×2 oyun. `Sim-Balance`:16×20 başlangıç bütçesi örneği. `Sim-Retune`:ayar adayları. İlk sim girdileri `SimLevels` altında sabit tutuldu;245v2 ve sonraki değişiklikler ayrı klasörlerden çalışır. Güncel level girdileri `SimLevels-Current` içindedir. Oranlar bot içindir; hata/zaman aşımı kayıp değildir. Üç başlangıç booster’ı için yeniden sim build izni bekleniyor; booster’lı yüzde henüz yok.

Projeye yalnız yeni16 level/meta, katalog append’i ve16 TR/EN ad eklendi. Önceki kullanıcı levelları korunur. Yayın/release baseline/commit yok.
