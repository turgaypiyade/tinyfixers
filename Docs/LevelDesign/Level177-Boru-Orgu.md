# Level 177 — Boru Orgu

Katalog 177 → `LevelP_001810.asset`, chapter 7. **9×11, 99 aktif / 0 hole, 27 hamle, dört renk, Normal.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level177-Boru-Orgu.png). [173–180 toplu inceleme](Levels173-180.md).

```text
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
```

`o` otomatik normal taş; `.` hole. Dört Up tube (6/4/4/6 hücre); orta altta iki KeyGenerator; dipte dört porselen. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Geniş üst alanda çizgi kurup farklı yükseklikteki boruları birlikte kısalt; orta makinelerden anahtar üret.

- 4 boru toplam 20 hücre kaplar.
- Komşu match veya special açık ucu küçültür.
- 16 anahtar üretilip tahtadan temizlenmelidir.

| Hedef | Adet |
| --- | ---: |
| Tube | 4 |
| Tile / Key | 16 |
| HelmetPorcelain | 4 |

Tube base origin'leri 91/93/95/97; yön Up=0, uzunluklar 6/4/4/6. Dört borunun toplam uzunluğu 20; goal dört borudur. Komşu normal eşleşme de boruyu küçültebilir; yalnız special değildir. KeyGenerator hücreleri 85/94: üst üste iki makine aynı ortak 16 anahtar kotasını kullanır. Key randomPool'da yoktur. Hedef kapasite − yere inen anahtar + board'daki gerçek anahtar hesabıyla bütün anahtarlar üretilip temizlenince biter. Alt makineye başlangıçta normal komşu erişimi sınırlı; çizgi etkileri yararlıdır.

## Tasarım notları

- Tube goal hücre değil dört tüptür. Key goal TileType.Key=9 kullanır.
- Makineler kalıcıdır ve altında normal hücre yoktur; rastgele havuza Key eklenmedi.

Başlangıçta **73 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**27 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
