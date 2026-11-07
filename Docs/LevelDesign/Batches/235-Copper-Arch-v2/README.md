# Yalnız 235 — tek kütleli katmanlı kemer · 2026-11-07

Kullanıcı “şimdi 235 üzerinde çok düşünerek değerlendir. katmanlı yapı kullanabilirsin” dedi. Yalnız aktif V1 235 revize edildi; diğer levellar korunur. Yeni sürüm kullanıcı tarafından henüz onaylanmadı.

**Son tasarım:** Bakır Kemer / Copper Arch, 9×11/85 aktif, 28 hamle ilk ayar, Hard. Alt kenardan yükselen tek taş kütlesi ve iki 2×2 heykel ayağı. 19 oyma→19 çamur; heykeller altında sekiz çamur daha. Sol 14 mavi/sağ 14 yeşil, branda sonrası kişi başına üç special isabeti. Hedefler: 2 heykel /19 oyma /27 çamur. 58 doğal açılış yuvası, sıfır pin. BatteryBox/Omega/çim/ColorCrate yok.

**Agent değerlendirmesi:** Eski iki ince sütun görsel odağı zayıf bırakıyordu. İlk kalın U taslağı da 176/182 benzerliği nedeniyle uygulanmadı. Son alt kütle, geniş üst alanı açılacak kemere yöneltir. 234’ün üçlü oyma/plastik/çamur yığını tekrarlanmadı. Heykel bitince altındaki çamur ayrı iştir; aynı combo bunu da ilerletebilir. Fazladan oyuncu hamlesi kesinmiş gibi sayılmaz.

- `revise.py --apply`: bir defalık yazıcı; tekrar yazmayı reddeder. `--verify`: gerçek assetin statik kontrolü, oyun çalıştırmaz.
- `Before/`: eski asset/meta/model/not/PNG/Contact. `protected-assets.json`: tur öncesi 6.986 Assets dosyasının hashleri.
- `design.json`: güncel 235. Master batch modelinde yalnız 235 güncellendi; diğer 15 model eşitlik kontrolüyle korundu. Eski bot kanıtı yeni modele taşınmadı.
- `Preview.png`: açılış ve iki katman kesiti; gerçek sprite’lar, temsili renkler. Oynanmış an veya zorunlu açılma sırası değildir.
- `Comparison.png` / `Similarity.json`: eski/yeni ve katalog karşılaştırması. Puanlar özgünlük/onay yüzdesi değildir. Tam envanter `Reviews/235-Copper-Arch-v2/` içindedir.
- `validation.json`: yalnız 235’te yedi veri alanı değişti; diğer 6.985 Assets dosyası uygulama sonrası aynı. Ayrı/tam katman kaldırma ve tek tek heykel açılmasında besleme; origin/footprint/hedef/ikon/katalog/isim kontrolü. Tüm dinamik düşüşlerin garantisi değildir.

Mevcut LevelEditor heykel paleti taban/yığın girişlerini listeler; renk/sayı/boyut `tarpStatues` verisindedir. Obstacle/Overlay ve mevcut library sprite/goal ikonları kullanılır. Yeni obstacle/editor/runtime uygulaması yoktur; manuel UI/oyun testi kullanıcıdadır.

İki heykel ve mevcut servisleri korunur. Artan renk kotası mevcut managed uçuş/efekt nesnesi maliyetini artırabilir; performans kazancı iddia edilmez. Tekrar kullanılan tamponlar ve Clear/OnDestroy uçuş iptali incelendi. Yeni update döngüsü, kaynak veya ses yok. CPU/GC/FPS ve teardown runtime’da ölçülmedi.

Eski SimLevels/rapor/final hashleri tarihsel kanıttır, yeni 235’in girdisi/denge ölçümü değildir. 28 hamle ilk ayardır. Build/Play/sim/UI/yayın/baseline/commit yapılmadı. Sonraki revizyon mevcut kullanıcı assetinden yeni snapshot almalıdır.
