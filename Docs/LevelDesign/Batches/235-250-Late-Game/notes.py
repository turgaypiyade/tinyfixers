"""Write reviewable design notes from the current authored model and evidence."""
from render import *


def notes():
    ds=json.loads((HERE/'designs.json').read_text());sim=json.loads((HERE/'similarity.json').read_text())
    rank={d['level']:d for d in sim}
    lines=['# 235–250 · Geç dönem tasarımları','',
        'Kullanıcı 225–234 denge işinden sonra 250’ye kadar yeni seviyeler istedi. Bu klasör yalnız 235–250’yi yazar. 1–234’ün level/meta dosyaları append öncesi hashleriyle korunur; 225–234’ün yetkili hamle ayarları ayrı inceleme kaydındadır.',
        '', 'Fikir önce seçildi: farklı açılış kararları, kenarla birleşen geçici engeller, geniş eşleşme yüzleri ve açıldığında işi devam eden katmanlar. Çim42/58/69 ve renkli sandık71–74 kullanılmadı. Normal renkler otomatik dolar; yalnız doğal dolumun erişmediği normal açılış yuvalarına sıradan taş pini kondu. Hazır special yok.',
        '', '|Level|Ad|Tür|Hamle|Oyuncu hedefi|Hedef sayısı|', '|---|---|---|---:|---|---:|']
    for d in ds:
        lines.append(f'|{d["level"]}|[{d["name"]}](../../Level{d["level"]}-{d["slug"]}.md)|{d["difficulty"]}|{d["moves"]}|{d["target"]}|{len(d["goals"])}|')
        g=d['openingGeometry'];near=rank[d['level']]['closest'][0];crop=rank[d['level']]['cropped'][0]
        note=[f'# {d["level"]} · {d["name"]} / {d["nameEn"]}','',
            f'{d["width"]}×{d["height"]}, {g["activeCells"]} aktif hücre; **{d["moves"]} hamle · {d["difficulty"]}**. 4 renk. Oyuncu hedefi: {d["target"]}; kalibre insan geçiş oranı değildir.',
            '', f'[Asset](../../{d["asset"]}) · [Önizleme](Level{d["level"]}-{d["slug"]}.png) · [Batch](Batches/235-250-Late-Game/README.md)',
            '', d['idea'], '', '**İlk karar:** '+d['firstDecision'], '', '**Açılınca:** '+d['change'], '', '**Kalan iş:** '+d['remaining'],
            '', 'Hedefler: '+', '.join(f'{x["amount"]} {label(x)}'for x in d['goals'])+'.',
            '', f'Açılışta {g["normalSlots"]} normal yuva: {g["naturallyFed"]} doğal dolum, {len(g["shadowPins"])} gerçek gölge pini. Pin indexleri: {g["shadowPins"]}. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.',
            '', f'Karşılaştırma kısa listesinde konumsal en yakın {near["level"]}, kırpılmış siluette {crop["level"]}. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.',
            '', 'Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.',
            '', 'Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.']
        if d['level']==242:note.insert(-2,'Pil hedefi kutu sayısı değildir: bir kutudaki dört renk × üç kabul edilen vuruş = 12. Goals/PreLevel için mevcut dolu pil sprite’ı kullanılır; boş kasa resmi hedef ikonu yapılmadı.')
        if d['level'] in [240,247]:note.insert(-2,'Su birikintisi hedefi dinamik olarak yaşayan su hücrelerinden yeniden sayılır. İki depo × mevcut 8 damla başlangıç gösterimi16’dır; kaynaklar açıldıktan sonraki gerçek yerleşim/çakışmalar sayaçta takip edilir. En fazla beş hedef sınırına bu slot baştan dahildir.')
        (ROOT/f'Docs/LevelDesign/Level{d["level"]}-{d["slug"]}.md').write_text('\n'.join(note)+'\n')
    lines+=['','## Tanıtım ve editör','',
        '231–240’ta kullanıcının 231’de tanıttığı sinek kutusu/yumurta/elma akışı devam eder;235–240 yeni renk varyantlarını yeni mekanik diye saymaz.241’de EnergyContainer22,242’de BatteryBox23 V1’e girer: biri ortak enerji kotası, diğeri dört ayrı renk pilinin vuruşlarıdır.246’daki Omega24, mevcut iki vuruşlu hareketli engelin görsel varyantıdır; üçüncü bağımsız mekanik olarak sayılmaz. Böylece241–250’de iki yeni mekanik vardır.',
        '', 'Bunlar mevcut runtime/library engelleridir. LevelDataEditor paleti library.obstacles listesinden okunur;22/23/24 mevcut ad/sprite/footprint ile seçilip yerleştirilir. EnergyContainer için level bazlı enerji ayarı, BatteryBox için4×hits hedef önerisi, Omega için mevcut movable yerleştirme/erase/resize akışı kullanılır. Yeni hedef ikonları nonzero Sprite referanslarıyla doğrulandı. Fruit/Safe/Tube/Magnet/Channel boyut ve yol verileri mevcut editor modlarıyla eşleşir; runtime-only bir teslim değildir.',
        '', '## Tekrar ve performans','',
        '`similarity.json` bütün önceki aktif V1 kataloğunu tarar. `Contact.png` ve16 gerçek sprite kartı görsel inceleme içindir. 241’in sade rıhtımı eski kenar kolonlarıyla ortak bir dikdörtgen kullanır; bu bilinçli tanıtım sadeleşmesidir.239 kasa altında Cargo yerine üretim açar;233’ün teslimat kilidini kopyalamaz.249’un C yolu233’ün kısa U’sundan daha uzundur ve dolap köprüsüyle farklı bir son iş taşır; aynı mıknatıs ailesi olduğu saklanmaz.',
        '', 'Tile/engel sayıları veriyle belirlenir; ek per-frame tarama kodu eklenmedi. Enerji uçuşları, kuşlar ve su saçılımı mevcut coroutine/efekt maliyetlerini taşır. Özellikle iki su deposunun saçılımı mevcut geçici liste/HashSet ve ekran damlası maliyetini kullanır; bu koşular cihaz CPU/GC/FPS ölçümü değildir. Sık efektler mevcut havuzları kullanır. Simülasyonun yeni booster hostu oyun başına tek dedicated injector kullanır, iptal/timeoutta temizlenir; booster görselleri için ayrı kaynak/havuz yaratılmadı.',
        '', '## Kanıt ve sınır','',
        '`Sim-Smoke`:16×2 oyun. `Sim-Balance`:16×20 başlangıç bütçesi örneği. `Sim-Retune`:ayar adayları. İlk sim girdileri `SimLevels` altında sabit tutuldu;245v2 ve sonraki değişiklikler ayrı klasörlerden çalışır. Güncel level girdileri `SimLevels-Current` içindedir. Oranlar bot içindir; hata/zaman aşımı kayıp değildir. Üç başlangıç booster’ı için yeniden sim build izni bekleniyor; booster’lı yüzde henüz yok.',
        '', 'Projeye yalnız yeni16 level/meta, katalog append’i ve16 TR/EN ad eklendi. Önceki kullanıcı levelları korunur. Yayın/release baseline/commit yok.']
    (HERE/'README.md').write_text('\n'.join(lines)+'\n')
    print('Wrote16 design notes and batch README.')


if __name__=='__main__':notes()
