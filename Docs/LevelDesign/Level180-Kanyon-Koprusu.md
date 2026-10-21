# Level 180 — Kanyon Köprüsü

Katalog 180 → `LevelP_001840.asset`, chapter 7. **9×10, 84 aktif / 6 hole, 30 hamle, dört renk, Boss Duel.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level180-Kanyon-Koprusu.png). [173–180 toplu inceleme](Levels173-180.md).

```text
oooo.oooo
oooo.oooo
Booo.oooB
BBoooooBB
BBBoooBBB
BBoooooBB
BoooooooB
oooo.oooo
oooo.oooo
oooo.oooo
```

`o` otomatik normal taş; `.` hole. B mavi plastik; alt kenarda dört oyuncu kalkanı ve iki yardımcı yumurta. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

İki kanyon kanadını birleştiren orta köprüde büyük eşleşmeler kur; alt kalkanları ve yumurta yardımını baskıdan önce aç.

- 580 boss hasarı · 270 oyuncu canı.
- 3 dalga · 30 hamle · 9×10 board.
- 4 sayaçta 2 engel; baskı/yağ tavanı 8.

| Hedef | Adet |
| --- | ---: |
| Collectible / BossDamage | 580 |

Yükseklik 10: boss karakter alanı korunur. Hedef 580 BossDamage; playerMaxHp 270, base enemy damage 7, üç otomatik dalga, damagePerClearedTile 1. Dört sayaçta iki yağ/kırmızı/yeşil/porselen tehdidi atılabilir; baskı ve yağ tavanı 8. Başlangıçtaki 18 mavi plastik fırlatma havuzunda yoktur. Araç tehdidi 170'teki açık ayarı kullanır. Dört oyuncu kalkanı 83/84/86/87, iki yardımcı yumurta 81/82. Başlangıç kalkanlarının toplam pickup koruması 8; hedef boss hasarıdır.

## Tasarım notları

- Başlangıç mavi plastikleri, boss fırlatma havuzunda yoktur: yağ/kırmızı/yeşil/porselen havuzu 170 seviyesinden korunur.
- Araç tehdidi 170 ayarıyla açık; dört kalkanın toplam koruması mevcut pickup başına 2 ayarından gelir.
- İki yumurta yalnız yardımcıdır; boss hasarı dışında authored hedef yoktur.

Başlangıçta **60 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**30 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
