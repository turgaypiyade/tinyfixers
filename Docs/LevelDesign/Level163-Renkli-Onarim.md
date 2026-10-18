# Level 163 — Renkli Onarım

Katalog 163 → `LevelP_001670.asset`, chapter 7. **9×11, 99 aktif hücre, 28 hamle, dört renk.** Orta; 162 sonrasında normal eşleşmeyle de ilerleyen, açık ve akıcı bir kırılma seviyesi. [Önizleme](Level163-Renkli-Onarim.png).

```text
ooooooooo
PPoooooPP
PPoooooPP
AAoooooBB
AAoooooBB
PPoooooPP
PPoooooPP
CCoooooDD
CCoooooDD
PPoooooPP
PPoooooPP
```

`o` rastgele taş; `P` Mud (25) → HelmetPorcelain (30); A/B/C/D dört ayrı 2×2 PaintCanBox (63). Hole yoktur. Koordinatlar sıfır tabanlı, y üstten aşağıdır.

| Hedef | Adet |
| --- | ---: |
| PaintCanBox | 4 kasa |
| Mud | 24 hücre |
| HelmetPorcelain | 24 |

Kasa origin'leri **27, 34, 63, 70**; her kasada dokuz kutu, toplam 36 kutu vardır. Goal dört kasadır. Komşu normal eşleşme bir, special iki kutu düşürür. Dört hücreye değen tek etki dört bağımsız isabet olarak varsayılmaz. Krem porselen, mavi/kırmızı kasalar ve sıcak çamur iki simetrik yan şerit kurar.

Kasaların Red/Blue/Yellow/Green seçimleri kayıtlıdır; ayrı Resources renk çizimleri bulunmadığında mevcut servis aynı `PaintCanBox/PaintCan` sprite'ını kullanır. Önizleme mevcut varsayılan kutuyu gösterir; olmayan renk varyantı üretilmez.

Ortadaki beş sütun on bir sıra boyunca açıktır. **59 başlangıç taş hücresi**, tamamı rastgele; hazır special veya yardımcı yoktur. Porselen hareketli tek örtüdür, altında iki vuruşlu çamur vardır. Önce kısa kırılmalarla alan açılır, sonra kasalar ve çamur birlikte ilerler. Porselen gravity ile yer değiştirebilir.

Footprint, hedef, katman, renk seçimleri, dolum ve katalog kontrolü geçti. **28 hamle ilk ayardır; kazanma oranı ölçülmedi.** [Ortak doğrulama ve performans notları](Levels163-166.md). Unity Play/build ve bot simülasyonu yapılmadı; önizlemedeki normal renkler temsili.
