# Level 125 — Şapka Şeritleri (hard)

Asset: `LevelP_001300.asset` → `LevelCatalogPro` `level: 125` (chapter 5, `LevelCL_0125`). 9×9 tam board, 25 hamle. **Kullanıcı tasarımı, birebir kuruldu** (2026-10-11).

```text
PLPoLoPLP   (9 sıranın hepsi aynı)
```

`P` PlasticTwoStage (40) — sütun 1, 3, 7, 9 (36 adet), `L` HatLauncher (31) — sütun 2, 5, 8 (27 adet), `o` normal taş (sütun 4 ve 6, otomatik dolum).

Hedefler: EnergyOrb 290 (HatLauncher), PlasticTwoStage 36.

## Kurulum öncesi uyarılar (Claude, koddan)

- HatLauncher kırılmaz (hedef dolana dek kalıcı engel) → board en fazla 2 hücrelik dikey şeritlere bölünür; yatay eşleşme ve L/T special'ı oluşamaz.
- Normal/kaskad eşleşmede her şapka hamle başına en fazla 1 orb verir (ObstacleStateService emit guard). Tahmini erişilebilir orb 25 hamlede ~100–150; 290'a ulaşmak beklenmiyor.
- Sütun 1 ve 9'daki 18 plastiğin (36 vuruş) yanında şapka var; yalnız special ile kırılabilir.
- Alternatif öneri (kullanıcı reddetti): şapkaları alt 6 sıraya indirip üst 3 sırayı açmak, orb ~120.

Oynanarak doğrulanmalı; Unity, bot veya simülasyon çalıştırılmadı.

## Kullanıcı testi (2026-10-11)

- Renk havuzu **2 renk** (Gear + Core, `randomPool`): 1 hamle kala geçildi — hard için doğru sıkılık. Karar: 2 renk + 25 hamle kalır.
- 3 renkte belirgin zorlaşıyor (kullanıcı). Orb hedefinin tutmasının sebebi 2 renkte hemen her hamle her şapkanın yanında eşleşme olması (şapka başına hamlede 1 orb sınırı doluyor).
- Refill 4+ seriyi engeller, 3'lüye izin verir → kaskadlar uzun ama sonlu; dinamik girdi açık kalır.
