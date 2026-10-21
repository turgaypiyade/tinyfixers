# Level 178 — Çiçekli Pusula

Katalog 178 → `LevelP_001820.asset`, chapter 7. **9×11, 59 aktif / 40 hole, 29 hamle, dört renk, Normal.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level178-Cicekli-Pusula.png). [173–180 toplu inceleme](Levels173-180.md).

```text
....o....
...ooo...
..ooooo..
.ooooooo.
ooooooooo
ooooooooo
ooooooooo
.ooooooo.
..ooooo..
...ooo...
....o....
```

`o` otomatik normal taş; `.` hole. Dört uçta nilüfer; dış kontur porselen; sekiz porselenin altında Oil. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Porselen konturu açınca çıkan yağı kontrol et; pusulanın dört ucundaki nilüferlerin special yutma zamanını yönet.

- 4 nilüfer: yapraklar ve alttaki çim.
- 8 yağ kaynağı porselen altında gizli.
- Yağın yayılmasını erken kontrol etmek değerli.

| Hedef | Adet |
| --- | ---: |
| GrassWaterLily | 4 |
| Oil (dinamik) | 8 |
| HelmetPorcelain | 16 |

Nilüferler 4/45/53/94 indekslerinde, şeklin dört ucundadır. 16 porselenin sekizinin altında yağ vardır. 43 tile barındırabilen hücrenin dördü nilüfer altıdır; 39'u nilüfersiz normal çalışma alanıdır. Dört yaprak ve alttaki çim temizlenmeden nilüfer goal'ü ilerlemez. İçinde special tutan çiçeği normal match açmaz; bir başka special ile geri salınır.

## Katmanlar

- 8 hücre: taban ID 14 → üst ID 30, `stackOrder=1`.

## Tasarım notları

- Yağ tabandadır; üst katman tek hareketli porselendir.
- Yağ tavanı 8: aynı anda açık yağı sınırlar, toplam üretilen yağ adedi için sabit üst sınır değildir.

Başlangıçta **43 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**29 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
