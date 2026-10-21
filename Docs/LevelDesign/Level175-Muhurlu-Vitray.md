# Level 175 — Mühürlü Vitray

Katalog 175 → `LevelP_001790.asset`, chapter 7. **9×11, 81 aktif / 18 hole, 28 hamle, dört renk, Hard.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level175-Muhurlu-Vitray.png). [173–180 toplu inceleme](Levels173-180.md).

```text
ooooooooo
RRoooooGG
RRoooooGG
RRoooooGG
YYoooooBB
YYoooooBB
YYoooooBB
.YYoooBB.
..YTTTB..
...TTT...
...TTT...
```

`o` otomatik normal taş; `.` hole. R/G/Y/B Mud üstündeki üç aşamalı renkli sandıklar; T tek 3×3 brandalı heykel. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Aynı renk eşleşmesiyle vitray kanatlarını açarken 18 sarı taşı toplayıp alt heykelin brandasını sök.

- 30 renkli sandık → 30 iki katlı çamur.
- Branda 18 sarı taşla açılır.
- Açılan heykele 3 special isabeti gerekir.

| Hedef | Adet |
| --- | ---: |
| Mud | 30 |
| TarpStatue | 1 |

Tek heykel origin 75 = (3,8), 3×3. `tarpStatues` etiketi Yellow=1 ve count=18. Branda her yerden temizlenen sarı taşları toplar; açılana kadar doğrudan hasarla ilerlemez. Ardından üç special isabeti ister. 30 renkli sandığın dağılımı kırmızı 6 / yeşil 6 / sarı 9 / mavi 9; her biri üç aşamalıdır. Bunlar ayrıca goal değildir: 30 Mud hedefi bu örtülerin tamamının açılmasını zaten gerektirir.

## Katmanlar

- 6 hücre: taban ID 25 → üst ID 72, `stackOrder=1`.
- 6 hücre: taban ID 25 → üst ID 74, `stackOrder=1`.
- 9 hücre: taban ID 25 → üst ID 71, `stackOrder=1`.
- 9 hücre: taban ID 25 → üst ID 73, `stackOrder=1`.

## Tasarım notları

- Sandık hedefi ayrıca yazılmaz: 30 çamuru bitirmek sandıkların tamamını da açmayı gerektirir.
- Hard yükü: renk kısıtı + iki aşamalı sıvı tabanı + renk toplama sonrası special. Yardımcı yok.

Başlangıçta **42 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**28 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
