# Level 173 — Kargo Rıhtımı

Katalog 173 → `LevelP_001770.asset`, chapter 7. **9×11, 92 aktif / 7 hole, 28 hamle, dört renk, Normal.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level173-Kargo-Rihtimi.png). [173–180 toplu inceleme](Levels173-180.md).

```text
oooo.oooo
oooo.oooo
oooo.oooo
oCoooooCo
oCoooooCo
PPoooooPP
PPoooooPP
PPoo.ooPP
PPoo.ooPP
PPoo.ooPP
PPoo.ooPP
```

`o` otomatik normal taş; `.` hole. C Cargo; P PlasticTwoStage, son üç sıradaki fenerlerin altında Mud. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

İki dört-sütunluk rıhtımı birleştiren geniş köprüde special kur; iki kargo kolonunun altını sırayla aç.

- 4 kargo iki sütunda ikişerli bekler.
- 24 fener iki kesintisiz kenar bandıdır.
- Alt 12 fener kırılınca çamur açılır.

| Hedef | Adet |
| --- | ---: |
| Cargo | 4 |
| PlasticTwoStage | 24 |
| Mud | 12 |

Dört kargo indeksleri 28/34/37/43; çıkış sütunları x=1/7. Her iki sütunda da son satıra kadar bütün hücreler aktiftir. Kargo yana kaymaz ve hasarla kırılmaz; altındaki fenerlerin açılması gerekir. Açığa çıkan Mud Cargo çıkışını kalıcı olarak bloke etmez.

## Katmanlar

- 12 hücre: taban ID 25 → üst ID 40, `stackOrder=1`.

## Tasarım notları

- Kargo yalnız x=1/7 sütunlarından düz aşağı iner; ikisi de son satırda aktiftir.
- Hole: merkez sütunun üst üç ve alt dört hücresi; 135/156 örneklerindeki bölme ve çıkış düşüncesi.

Başlangıçta **64 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. Başlangıç taşı/special pinleri sıfırdır; normal renkler rastgeledir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**28 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
