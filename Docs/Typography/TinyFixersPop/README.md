# TinyFixers Pop — v0.5

TinyFixers popup başlıkları ve kısa buton metinleri için özgün font taslağı.
Grobold denemesinde beğenilen kalınlık ve hareket hissi tasarım yönüdür;
Grobold veya başka bir fontun konturları bu dosyaya aktarılmadı.

## v0.5 — okunurluk ve aralık düzeltmesi (27 Ekim 2026)

Kullanıcı, incelemede önerilen küçük `e` açıklığı, sıkışan harf aralıkları ve kerning
aktarımı düzeltmelerinin uygulanmasını istedi. Düzenlemeler:

- `e` gözünün iki ekseni yaklaşık %18 büyütüldü; sağ açıklık hafif açıldı. Dış şekil korunur;
  `é è ê ë` gibi türevler aynı yeni gözü kullanır.
- Büyük harflerin sağ aralığı 20/1000 em artırıldı (48 px yazıda harf başına 0.96 px).
  Küçük harf/rakam aralıkları ve rakam konturları korundu.
- Mevcut kerning çiftleri aksanlı türevlere de yayıldı: toplam 441 çift (`To` / `Tö` gibi).
- TMP üreticisi artık `includeFontFeatures: true` kullanır. Yazı bileşenlerinin güncel
  `m_ActiveFontFeatures` listesinde kerning zaten açıktır; eski `m_enableKerning: 0`
  alanı tek başına kerning'in kapalı olduğu anlamına gelmez.

Doğrulama: 305 karakterin eşlemesi korundu; rakam konturları ve ölçüleri önceki TTF ile
birebir aynı; Türkçe dahil büyük harflerin ilerleme artışı 20 birim; aksanlı kerning eşitliği
kontrol edildi. `Preview-v05.png` ve `Comparison-v04-v05.png` TTF'den çizilmiş örneklerdir;
oyun ekran görüntüsü değildir.

Bu revizyonda Unity arayüzü kullanılmadı ve proje build edilmedi. Kaynak TTF yenilendi;
oyunda v0.5 görünmesi için mevcut TMP atlası Unity'de
`TinyFixers > Fonts > TinyFixers Pop > Build Font Asset + Presets` menüsünden yenilenmelidir.
Mevcut SDF asset'i bu dosya düzenlemesi sırasında değiştirilmedi.

## v0.4 — elle çizilen harfler (27 Ekim 2026)

Kullanıcı: "S, e, 9, O, D, T, H, A, N, a harflerine kanalize ol." Bu 10 harf `hand_drawn()` içinde
SON ölçülerde (büyük harf 815, küçük harf 626, taban -22) sıfırdan çizildi; Grobold'un dili,
kendi konturlarımızla:
- "düz" kenarlar sağa yükselir (`SLOPE` ≈ 2.6°), gövdeler kama (üstte geniş), köşeler az yuvarlak;
- O ve 9 yatık yumurta/elips, iç boşluklar küçük ve yatık (D yarık, a ve O oval);
- S, e, 9 ve a geniş uçlu kalemle orta çizgi üzerinden yazılır (`pen_stroke`): uçlar açılı kesilir;
- A sivri tepe + açık ayaklar; e çapraz yükselen çizgi + yarık göz.
Aksanlı biçimler (Ş Ö Ä é …) yeni harfin üstüne kurulur (`apply_hand_drawn`).
Kalan harfler henüz eski iskeletten — aynı dille sırayla yeniden çizilecek.

## v0.3 — dik ve dolgun (27 Ekim 2026)

Kullanıcı: "eğim vermeden yazarken harfler neden çok yamuluyor, Grobold çok daha iyi."
Ölçüm: Grobold'da düz harflerin taban sapması 5–17 birim (dümdüz), v0.2'de 14–59. Yamukluğun
kaynağı v0.2'nin rastgele ±5° döndürmesiydi. v0.3: döndürme/zıplama yok; Grobold'a göre ölçüldü:
%12 büyük (büyük harf ~818, Grobold 822), yatayda %8 dar, gövde 34 (gövde/yükseklik ≈ 0.37),
aralık 30 sıkı, küçük harf oranı v0.1'deki gibi (0.756 — Grobold 0.74). Grobold'un "el çizimi"
karakteri (kama gövdeler, eğik üst kenarlar) dönüşümle gelmez; harf harf yeniden çizim gerekir.

## v0.2 — "chunky" geçişi (27 Ekim 2026)

Kullanıcı isteği: Grobold'a benzesin, yorum da katılsın; TMP outline/underlay/softness/glow
hepsiyle sorunsuz çalışsın. v0.1 iskeletleri korunup `build_font.py` içinde şu hattan geçer
(ayarlar dosyanın başındaki `STYLE` sözlüğünde):

1. Küçük harfler %9 uzun (x-height 529 → 577).
2. İki katlı `a` (Grobold karakteri); diğer harfler v0.1 çizimi.
3. Her harfe sabit "zıplama": ±5° eğim + ±22 birim satır kayması (rakamlarda ±2.4° / ±10 —
   skor ve seviye numaraları sakin kalsın). Değer karakterin hash'inden gelir, her üretimde aynıdır.
4. Yuvarlak kalınlaştırma: tüm konturlar 26 birim büyür, iç köşeler 14 birim yuvarlanır
   (kalın gövde, küçük iç boşluk, yumuşak köşe). Çok çizgili semboller (`₺ € $ # % @ & *`)
   daha hafif (10/6) — yoksa çizgi aralıkları kapanıyor.
5. Üst üste binen konturlar birleştirilir, hepsi saat yönünde tek temiz şekil olur. Bu, TMP SDF
   outline/underlay/softness'ta leke ve dikiş çıkmaması için şarttır.

Bilinen zayıflar: `&` çizimi zayıf; zıplama sabit (aynı harf her yerde aynı eğimde).
v0.1 TTF'i git'te yoktu ve v0.2 üzerine yazıldı; v0.1 görünümü yalnız `Preview-v01.png`'de.

## Dosyalar

- Unity kaynak fontu: `Assets/_Project/Fonts/TinyFixersPop/TinyFixersPop-Regular.ttf`
- Görsel örnek: `Preview-v05.png`; önce/sonra: `Comparison-v04-v05.png` (oyun ekran görüntüsü değildir).
- Ana düzenlenebilir kaynak: `build_font.py`; harf yolları ve ölçüler burada tanımlıdır.
- `glyphs.json`: üretilen tüm konturların koordinatları ve ilerleme genişlikleri.
- `glyphs/`: İngilizce/Türkçe harflerin ve rakamların ayrı SVG çizimleri.

SVG ve JSON dosyaları dışa aktarımdır; TTF'yi yeniden üretmek için değişiklikler
Python kaynağına uygulanır. PNG yalnızca bir örnektir; fontun kendisi vektördür.
TTF ana dosyadır; Unity/TMP atlası bu kaynaktan üretilir.

## Kapsam ve görünüm

305 kod noktası: temel Latin büyük/küçük harfler, rakamlar, yazdırılabilir ASCII,
Türkçe `Çç Şş Ğğ Iı İi Öö Üü`, seçili Latin aksanları ve `₺ €` dahil semboller.
Tüm dillerin kapsandığı anlamına gelmez. Birleşen aksanlar için gelişmiş
yerleştirme henüz yoktur; hazırlanmış Türkçe karakterler kullanılmalıdır.

Tek stil: Regular 400; harf konturları doğrudan kalın çizildi, sahte bold gerekmez.
Beyaz dolgu, koyu outline/gölge ve başlık yayı fonta gömülü değildir; TMP materyali
ve mevcut TextArcEffect bunları uygular. Böylece aynı font farklı buton
renklerinde kullanılabilir.

## Unity'de kullanım

`TinyFixers > Fonts > TinyFixers Pop > Build Font Asset + Presets` (Play Mode dışında):

- `Assets/_Project/Fonts/TinyFixersPop/TinyFixersPop SDF.asset` — statik atlas, TTF'teki tüm
  karakterler, SDFAA, 90 pt örnekleme, 13 px padding (kalın outline + gölge + softness + glow için).
  Eksik karakterde BakBak One'a düşer. Kerning çiftleri de aktarılır.
  Yeniden çalıştırınca yerinde güncellenir (GUID korunur).
- `Assets/_Project/Fonts/Materials/TinyFixersPop_*.mat` — mevcut BakBak/Inter/DynaPuff
  preset'lerinin görünümü Pop atlasına kopyalanır (Cartoon, PreLevelTitle, TitleOnMaroon,
  OnGreenButton, OnBlueButton, GoldGlow, RoyalTitle). TMP Inspector'daki Material Preset
  listesinde görünürler.

TTF her değiştiğinde menüyü yeniden çalıştır.

## Oyunda deneme

Unity menüsünden:
`TinyFixers > Fonts > PreLevel Font Trial > TinyFixers Pop (original)`

Play Mode sırasında PreLevel başlığı ve devam butonuna uygulanır; mevcut arc ve
materyal görünümü kullanılır. Grobold seçeneği aynı menüden geri seçilebilir.
Seçim editör oturumuna aittir; prefabın kalıcı font ataması değişmez.
Bu çalışma sırasında Unity açılarak veya proje build edilerek test yapılmadı.

## Yeniden üretme

Python 3, FontTools (4.67.0 ile doğrulandı), skia-pathops ve Pillow gerekir
(örn. `python3 -m venv .venv && .venv/bin/pip install fonttools skia-pathops pillow`):

```sh
python3 Docs/Typography/TinyFixersPop/build_font.py
```

Kaynak font okunmaz. Script konturlardan TTF, JSON, SVG ve örnek PNG üretir;
TTF'yi tekrar açarak ASCII/Türkçe karakter eşlemesini ve metrikleri kontrol eder.
Örnek PNG açıklamalarında macOS Helvetica kullanılır; font örneklerinin tamamı
TinyFixers Pop ile çizilir. Helvetica konturları font dosyasına dahil edilmez.

## Kaynak kaydı

Konturlar bu proje için 27 Ekim 2026 tarihinde kodla sıfırdan oluşturuldu.
Üçüncü taraf font dosyası türetilmedi ve üçüncü taraf font lisansı eklenmedi.
Kaynaklar ve TTF proje içinde tutulur; ticari font satışından alınmış bir dosya yoktur.
