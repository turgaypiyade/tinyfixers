# Level 174 — Mor Mercek

Katalog 174 → `LevelP_001780.asset`, chapter 7. **9×11, 87 aktif / 12 hole, 28 hamle, dört renk, Normal.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level174-Mor-Mercek.png). [173–180 toplu inceleme](Levels173-180.md).

```text
..ooooo..
.ooooooo.
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
.SoooooS.
..SSSSS..
```

`o` otomatik normal taş; `.` hole. S SculptingSpecial; iki alt kenarda yukarı bakan GelLauncher; ortada üç açık jel kaynağı. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Oval merceğin ortasında jel taşıyan special üret; çevreyi açıp jeli bütün aktif hücrelere taşı.

- Pusulalar normal eşleşmeyle kırılmaz.
- İki kartuş ikişer special isabeti ister.
- Kartuş kapağı aynı sütundaki çevreyi açar.

| Hedef | Adet |
| --- | ---: |
| SpreadingGel | 87 |
| SculptingSpecial | 31 |

Üç açık jel kaynağı (4,3)/(4,4)/(4,5); kartuş origin'leri 73/79 = (1,8)/(7,8), her biri 1×2 Up. Hedef 87 aktif hücrenin tamamıdır; 12 hole sayılmaz. Başlangıçtaki açık kaynaklar mevcut HUD tarafından kaplamadan düşülür. Kartuşların tüm board'u otomatik jelleyeceği varsayılmaz.

## Tasarım notları

- Jel goal miktarı hole hariç bütün board: engellerin sonradan açılan hücreleri de dahil.
- Kartuşlar yardımcıdır; doğrudan goal değil. Hazır special yok.

Başlangıçta **52 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**28 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
