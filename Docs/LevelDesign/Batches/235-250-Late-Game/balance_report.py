"""Completed-run evidence and explicit limitations, separate from design approval."""
from author import *


def report():
    ds=json.loads((HERE/'designs.json').read_text())
    assert (HERE/'finalized.json').exists()
    all_games=[];runs=[];errors=[]
    for folder in sorted(HERE.glob('Sim-*')):
        if not folder.is_dir():continue
        games=[]
        for p in sorted(folder.glob('job*/report.json')):
            r=json.loads(p.read_text(encoding='utf-8-sig'))
            assert r['completed'] and len(r['games'])==len(r['config']['levelNumbers'])*r['config']['gamesPerLevel'],p
            games+=r['games']
        if not games:continue
        all_games+=games;runs.append(dict(folder=folder.name,games=len(games)))
        errors += [dict(folder=folder.name,level=g['levelPath'],sample=g['sample'],seed=g['seed'],outcome=g['outcome'],detail=g['detail'])for g in games if g['outcome']not in('Won','Lost')]
    (HERE/'sim-runs.json').write_text(json.dumps(dict(runs=runs,totalGames=len(all_games),errors=errors),ensure_ascii=False,indent=2)+'\n')
    lines=['# 235–250 · Denge incelemesi','',
        '16 seviye aktif V1 kataloğuna eklendi. 225–234 yalnız hamle ayarıyla ayrı raporda tamamlandı. Yeni seviyelerde grid ve engel fikri önce seçildi; denemelerin ardından bütçeler ayarlandı.',
        '', '**Aşağıdakiler Usta bot sonuçlarıdır; insan geçiş oranı değildir.** Oyuncu hedefleri sonu1–4 için%50–60,5/9 için yaklaşık%30;6–8 için önceki%40–50 yönüdür. Boss ayrıca değerlendirilir. 20 oyunluk örnek çok dar bir yüzde bandını doğrulayamaz. Bazı seviyelerde gerçek kullanıcı planlaması botun tek hamlelik puanlamasından belirgin biçimde farklıdır.',
        '', '|Level|Son hamle|Bot W/L|Hata|Bot geçişi|', '|---|---:|---:|---:|---:|']
    for d in ds:
        e=d['botEvidence'];lines.append(f'|{d["level"]}|{d["moves"]}|{e["won"]}/{e["lost"]}|{e["errors"]}|%{e["winPercent"]}|')
    lines+=['','## Seçim gerekçeleri','',
        '- **235/249:** Geniş ilk bütçelerin kolaylığı azaltıldı. 249’un22 hamlesi ayrı seed örneğinde6/20=%30 verdi. 235 hem renk/heykel işi hem oyma→çamur ayakları taşır; yalnız ilk açılışla bitmez.',
        '- **236/242/248:** Seçilen örneklerde hedef bandının biraz üstünde kalabilen rahatlık korundu. Dar örnekte tam yüzdeye uydurmak için sürekli bir hamle azaltmak yerine manuel teste pay bırakıldı.',
        '- **237/241/243/246/247:** Seçilen bütçelerin ayrı gerçek oyun sonuçları tablodadır. KeyGenerator hedefi botun kendi mevcut üretici çevirisini kullanır; EnergyContainer/Bird/Dimerdal hedeflerinde bu destek aynı değildir.',
        '- **239/250:** Düşük bot oranını imkânsız seviye diye yorumlamadık. Özellikle elmas üretimi için doğru kepengi, rengi ve üretim yüzünü planlamak botun puanlamasında doğrudan temsil edilmez.239’da kasa arkasından üretim açılır;250’de sinek kutusu/elma açılışı da vardır. Bu iki seviye insan dengesinin doğrulandığı iddiası taşımıyor.',
        '- **240:** İlk620 HP ve8+8 oyma/çamur çok kolaydı. Son sürüm900 HP,16+16 kenar yükü, iki su deposu ve dinamik su hedefidir. Üç dalga,24 hamle,320 oyuncu HP. Kombo odaklı daha rahat bir boss olarak kaldı;5/9 için verilen%30 hedefi boss’a otomatik uygulanmadı.',
        '- **244:** 22/24/25/27 hamle örnekleri belirgin değişkenlik gösterdi.24 orta bütçe seçildi; bot unbreakableCargo çevresindeki eşleşmeleri de hedef yakınlığıyla puanlar, oyuncunun kaideyi açıp düz düşürme planını tam temsil etmez. İnsan%50–60 doğrulaması yok; sabah manuel testte öncelikli örneklerden biridir.',
        '- **245:**238 ile benzer iki kanatlı ilk taslak bırakıldı. L kaideli v2’nin24 hamlesi ayrı koşuyla ölçüldü. İlk245 simülasyonları önceki geometriye aittir; güncel yüzde olarak kullanılmaz.',
        '', '## Veri sürümü ve koruma','',
        'Her seçilen bütçenin klasörü `designs.json → botEvidence.folder` alanında kayıtlıdır. `SimLevels` ilk denemeler için sabittir; revizyonlar `Retune-Inputs`, `Final-Trial-Inputs` ve son doğrulama klasörlerine ayrıldı. Bir koşunun ortasında kaynak JSON değiştirilmedi. Güncel dosyalardan `SimLevels-Current` üretilir.',
        '', 'Gerçek YAML/packed array/custom origin/footprint/goal/ikon ve katalog kontrolleri geçti. Önceki470 level/meta dosyası hash olarak korundu; eski katalog ve isim dizileri yalnız append edildi. 176’ya dokunulmadı. Yeni16 seviyede çim42/58/69 veColorCrate71–74 yok; pinler yalnız doğal dolum ulaşmayan normal hücrelerdedir. Her aktif normal yuva açılışta doğal dolum veya destekli sıradan pin alır. Kalıcı kaynaklar altta kalırken sonraki normal refill erişimi doğrulandı.',
        '', f'Bu batch içinde toplam **{len(all_games)} gerçek headless oyun** çalıştı. Hata kayıtları `sim-runs.json` içindedir; kazanma oranının paydasına eklenmez. Kısa32 oyunluk açılış koşusu hatasızdı. Daha uzun koşularda nadir görünürlük/efekt teardown hataları çıktı; bu yüzden bütün runtime’ın hatasız olduğu iddia edilmez.',
        '', '## Üç başlangıç booster’ı','',
        'LineH/PulseCore/SystemOverride teslimatı ve raporu sim koduna eklendi.21 Python testinin,56 izole sim C# kontrolünün ve8 injector iptal/idle/timeout temizliği kontrolünün sonucu PASS. Kaydedilmiş streak’in ek booster eklememesi için sim koruması vardır. Bekleyen board-idle aboneliği disable/destroy/iptalde temizlenir; envanter tüketilmez.',
        '', '**Yeni Mac sim build’i için açık onay bekleniyor.** Kullanıcının AGENTS.md kuralı açık istek olmadan build’i yasaklıyor; async izin sorusu yanıtsız kaldı. Bu nedenle üç booster’lı koşu yapılmış gibi gösterilmedi. Mevcut bütün ölçümler boostersız hazır Mac binary’siyle yapıldı. Yeni build alındığında önce1 oyunla tam3 teslimat raporu doğrulanmalı, sonra225–234 aynı hamlelerle karşılaştırılmalı.',
        '', 'Kullanıcı manuel oyun/editör onayı bekleniyor. Yeni Unity build’i, UI kontrolü, yayın, release baseline veya commit yok.']
    (HERE/'Balance-Review.md').write_text('\n'.join(lines)+'\n')
    print('Balance report:',len(all_games),'games;',len(errors),'non-Won/Lost outcomes.')


if __name__=='__main__':report()
