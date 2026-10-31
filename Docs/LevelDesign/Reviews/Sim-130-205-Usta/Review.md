# 130–205 Usta bot simi — onluk eğrisi kontrolü · 2026-10-31

Kaynak: `Tools/Sim/run_sim.py --levels 130-205 --games 15 --jobs 7 --policy Auto` (51+ = Usta bot),
build 2026-10-31 21:12 (131–205 genel revizyonundan SONRA). Ham veri: `~/Desktop/Sim/L130_205_Usta/summary.csv`.
Bot gerçek oyuncudan zayıftır: oranlar GÖRELİ okunur. Kuş evi (201–205) simde modellenmez; boss hasarı yaklaşık.
Kullanıcı hedef listelerini bu simden sonra elle düzeltti (kaynak+ürün çiftleri) → bazı oranlar değişebilir.

Eğri (Design-Principles): 1/6 tanıtım=kolay, 2→4 artan, 5 Hard, 7→8 artan, 9 Super Hard, 10'lar Boss.

| Sapma | Seviyeler (bot kazanma %) |
|---|---|
| Zor olması gerekirken kolay | 130 Boss 100 · 135 Hard 73 · 199 Super 67 · 189 Super 40 (sınırda) |
| Tanıtım olması gerekirken zor | 141 · 151 · 166 · 176 (%20) · 196 (%0) |
| Eğri tersine (7/8, 6'dan ya da önceki artandan kolay) | 137 (87, 136=53) · 167 (87, 166=20) · 183 (87, 182=73) |
| Artan sırada kırılma (önceki sonrakinden zor) | 134 (7) > 135 · 143 (13) > 144 (40) · 157 (0) > 158 (20) · 173 (27) > 174 (53) · 196–198 hepsi ≤13 iken 199=67 |

Not: İlk turda "kolay" işaretlenen 131/161/171/181/191/146 tanıtım seviyeleridir → kolay olmaları doğru.
