# Level 176 — Dolaplı Geçit

Katalog 176 → `LevelP_001800.asset`, chapter 7. **9×11, 83 aktif / 16 hole, 30 hamle, dört renk, Normal.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level176-Dolapli-Gecit.png). [173–180 toplu inceleme](Levels173-180.md).

```text
ooo...ooo
ooo...ooo
WWoooooWW
WWoooooWW
.PoooooP.
.PoooooP.
.PoooooP.
.PoooooP.
.PoooooP.
WWoHHHoWW
WWoHHHoWW
```

`o` otomatik normal taş; `.` hole. W dört 2×2 Wardrobe; P iki beşli fener şeridi; H bitişik 3×2 HatLauncher bölgesi. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Beş sütunluk geçitten iki atölye kanadını aç; alttaki altı şapkaya erişip 24 enerjiyi tamamla.

- 4 dolap, toplam 40 eşya saklar.
- Kapak sonrası normal hit 1, special 2 eşya.
- 6 şapka ortak 24 enerji hedefini besler.

| Hedef | Adet |
| --- | ---: |
| Wardrobe | 4 |
| PlasticTwoStage | 10 |
| Collectible / EnergyOrb | 24 |

Dolap origin'leri 18/25/81/88; hepsi 2×2. Her dolapta mevcut library ile 10 eşya vardır. Şapka hücreleri 84/85/86/93/94/95; 24 enerji level toplamıdır. Şapkalar mevcut goal kapasitesine göre tükenir; doğrudan engel hedefi değildir.

## Tasarım notları

- Enerji hedefi şapka başına 24 değil, bütün level için toplam 24.
- Üst merkezdeki hole şeridi dolumda geçirgendir; orta alanın üstüne kalıcı engel konmadı.

Başlangıçta **51 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**30 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
