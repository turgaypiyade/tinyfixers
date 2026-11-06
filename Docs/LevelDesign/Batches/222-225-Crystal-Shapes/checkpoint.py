"""Persist explicit user direction separately from unplayed agent design assessments."""
import json,re
from design import HERE,ROOT
ds=json.loads((HERE/'designs.json').read_text())
summary='\n'.join(f'- **{d["level"]} {d["name"]}:** {d["width"]}×{d["height"]}, {sum(d["data"]["cells"])} aktif, {d["moves"]} hamle'+(' Hard' if d['level']==225 else ' Normal')+f'; `LevelV1_{d["level"]*10:08d}.asset` / `{d["levelKey"]}`.' for d in ds)
runtime='Elmaslar artık engel değil normal `TileType.Diamond*` taşıdır: takas/düşüş/yana kayma/special oluşturma; yalnız aynı renk elmaslarla eşleşme veya special ile temizlenme. Komşu normal match elması kırmaz. Üretim yalnız hareketsiz, iniş görseli tamamlanmış normal taşa yerleşir; hareketlenirse hedef yeniden seçilir. Sayaç TarpStatue uçuşu varışında bir kez iner. Eski havada dönüştürme/komşu match ile toplama notları tarihseldir. Son davranış cihazda tekrar oynanmadı; `Docs/Checks/Dimerdal/README.md` günceldir.'
checkpoint=f'''## Güncel devam — 222–225 · 2026-11-05

- **Son açık yetki:** “222–225 level tasarlamamız lazım”; “bazen board gridleri bir şekle benzer bazen içindeki engelleri de bir şekle benzetebiliriz”. Bu istek eski “222 istenmedi” kayıtlarının önündedir. 226+ istenmedi.
{summary}
- 222 dış yaprak maskesi, çapraz 16 çim ve tek yeşil Dimerdal ile 10 elmas; 223 yatay anahtar maskesi, 14 Key / 8 porselen / 6 oyma / 6 çamur; 224 iç engellerden kesintisiz çapa, 12 elmas / 34 tek-hit ahşap / 1 heykel / 4 üst çim; 225 4×4 mühür (9/9/9) üstünde kapalı çamur avlusu, iki 2×4 special-only pusula suru ve tabanda kristal kapıları, 16 elmas / 1 mühür / 16 pusula taşı / 16 çamur.
- Asset/meta, aktif V1 katalog, TR/EN adlar ve dört not/gerçek sprite PNG'si hazır. Kayıt `Batches/222-225-Crystal-Shapes/`; tekrar envanterleri `Reviews/222-Crystal-Shapes` … `225-Crystal-Shapes`. `design.py` sahip olunan hash'leri kontrol eder; kullanıcı değişikliğini sessizce ezmez.
- Statik YAML/dizi/origin/katman/hedef/ikon/dolum/katalog/isim kontrolü geçti; 3.409 kapsam dışı dosya ve 1–221 aynı. Kalıcı kaynak grupları tabana dayanır. Açılabilir kapak gölgesindeki boş başlangıç yuvaları PNG'de gösterilir; renk pini/hazır special yok. Açılabilir katmanlar kalkınca kaynak altında beslemesiz cep kalmaz.
- PNG'ler açılıp incelendi. **Bu agent görsel değerlendirmesidir; kullanıcı dört taslağı henüz oynamadı/onaylamadı.** Hamleler ilk ayar. Build/Play/cihaz/bot/gravity-cascade simülasyonu, yayın ve release baseline yok. Gameplay/akış kodu veya library değiştirilmedi.
- 211=24, onaylı 212 yelkeni, 213–220 son hamleleri ve 221'in onaylı 24 hamle/8 elmas/6 pin düzeni korundu. 221–230'daki Dimerdal renkleri tek mekanik ailesidir; ikinci mekanik 226+ isteğinde ayrıca seçilir.
- **Güncel mekanik:** {runtime}
'''
for name in ('Memory.md','Design-State.md'):
    p=ROOT/'Docs/LevelDesign'/name;t=p.read_text();assert '## Güncel devam — 222–225' not in t
    head,body=t.split('\n',1);p.write_text(head+'\n\n'+checkpoint+'\n'+body.lstrip('\n'))
p=ROOT/'Docs/LevelDesign/Level-Names.md';t=p.read_text();assert '| 222 |' not in t
p.write_text(t+''.join(f'| {d["level"]} | {d["name"]} |\n' for d in ds))
p=ROOT/'Docs/LevelDesign/Design-Feedback.md';t=p.read_text();head,body=t.split('\n',1)
feedback='''## 222–225 kapsamı ve iki türlü şekil dili — kullanıcı · 2026-11-05

**Kaynak / açık söz:** “şimdi 222 iyi tasarlarmısın kurallarımıza göre 225 dahil 222–225 level tasarlamamız lazım. bazen board gridleri birşekle benzer bazen içindeki engelleride birşekle benzetebiliriz”.

**Kabul edilen kural ve kapsam:** yalnız 222–225 yetkilidir; bazen maskenin kendisi, bazen düz maskede engeller tanınabilir şekli oluşturur. Her boardu aynı dış siluete veya aynı iki kanada çevirmek gerekmez. Bu mesaj dört yeni taslağın görsel veya oynanmış denge onayı değildir; 1–221 ve 226+ için revizyon yetkisi vermez.

**Agent'ın uygulaması / ayrı değerlendirme:** 222 yaprak ve 223 yatay anahtar maskesi; 224 iç çapa; 225 mühürlü kale avlusu. Görsel inceleme ve statik sonuçlar batch README'dedir. Kullanıcı henüz bu dört leveli denemedi.
'''
p.write_text(head+'\n\n'+feedback+'\n'+body.lstrip('\n'))
p=ROOT/'Docs/LevelDesign/SESSION-LEVEL-DESIGN-NOTES.md';t=p.read_text()
anchor='## 1. Başlamadan önce gerçek durumu çöz'
t=t.replace(anchor,'**Son devam noktası (sonraki açık kullanıcı isteği):** 222–225 taslakları hazır; Zümrüt Yaprak24, Uçan Anahtar23, Kristal Çapa24, Mühürlü Kristal Kalesi25Hard. 1–221/runtime korundu; dört gerçek sprite PNG açılıp incelendi. Oynanmış/onaylanmış denge yok. Kayıt `Batches/222-225-Crystal-Shapes/`; Memory/Design-State en üst kayıt günceldir. 226+ istenmedi.\n\n'+anchor,1)
t,n=re.subn(r'^- Dimerdal açıldıktan sonra.*$', '- Güncel Dimerdal: '+runtime,t,flags=re.M);assert n==1
t=t.replace('**222 ve sonrası için bu not tek başına tasarım yetkisi vermez.**','**226 ve sonrası için bu not tek başına tasarım yetkisi vermez.**')
p.write_text(t)
p=ROOT/'Docs/LevelDesign/Obstacle-Design-Guide.md';t=p.read_text().replace('## Dimerdal — V1 ilk tanıtım221 · 2026-11-04','## Dimerdal — V1 ilk tanıtım221 · güncel2026-11-05')
t,n=re.subn(r'^- ÜrünID96–99.*$', '- Ürün normal `TileType.DiamondBlue/Green/Yellow/Red` taşıdır. ID96–99 authoring/ikon uyumu için korunur; authored elmas spawn/reveal sırasında normal taşa çevrilir. Normal takas/düşüş/yana kayma/special oluşturma vardır; yalnız aynı renk elmaslarla eşleşir veya special ile temizlenir. Komşu normal match kırmaz. **Hedef Collectible Diamond4**, bütün renkler ortak kotayı paylaşır; kutu kırılması veya her renkten ayrı zorunlu adet hedeflenmez.',t,flags=re.M);assert n==1
t,n=re.subn(r'^\*\*Son kullanıcı yönü \(2026-11-04\):\*\*.*$', '**Son kullanıcı yönü (2026-11-05):** '+runtime,t,flags=re.M);assert n==1
p.write_text(t)
print('Updated state/memory, explicit feedback, name index and current Dimerdal mechanics. No level/runtime mutations.')
