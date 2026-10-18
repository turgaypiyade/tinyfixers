# Wonder Boya Kovası (5. joker) — kurallar ve uygulama (2026-10-17)

EN: **Wonder Paint Bucket**. `BoardController.BoosterMode.PaintBucket`, booster index **4**, açılış **level 30**
(diğerleri 8/13/18/24; açılınca 3 hak hediye + ilk oyun ücretsiz — ortak BoosterAccessService kuralı).

## Kurallar (kullanıcı onaylı)
1. Joker seçilir → odak ekranı: **"Wonder Boya Kovası!"** / "Bir taşa dokun: o renk 3 hamle boyunca tahtadan çıkar!"
2. Oyuncu **normal** bir taşa dokunur → o TÜRÜN tahtadaki bütün normal taşları kalan türlerden rastgele birine döner.
   Dönmez: special'lar, engel altındaki / kilitli (kafes, mıknatıs vb.) taşlar, hareketli engel taşları.
3. Dönüşüm: dokunulan taştan her hedefe **gökkuşağı kuşağı** akar (uzaklığa göre dalga gecikmesi), kuşak varınca taş
   türünü değiştirir + küçük zıplama + parlama. Oluşan eşleşmeler hemen patlar.
4. Sonraki **3 oyuncu hamlesi** boyunca o tür üretilmez (dolum, mıknatıs, karıştırma). 3. hamlenin tahtası durunca tür
   geri gelir; dönüşmüş taşlar geri dönmez. Joker hamle tüketmez.
5. Slotun üstünde rozet: soluk taş ikonu + kalan hamle (3 → 2 → 1).
6. **Kullanılamaz, hak düşmez, sebep gösterilir:**
   - tür bir level hedefinde (taş toplama) ya da brandalı heykel / mühür o rengi hâlâ istiyorsa,
   - kaldırınca 2'den az tür kalacaksa (2 türlü level),
   - ambargo zaten sürüyorsa,
   - dokunulan şey normal taş değilse.

## Kod
| Parça | Yer |
|---|---|
| Ambargo, doğrulama, dönüşüm, hamle sayımı, rozet | `Grid/Board/PaintBucketJoker.cs` (board'a eklenen bileşen) |
| Gökkuşağı kuşağı (havuzlu, tek döngü) | `VFX/PaintRibbonFx.cs` |
| Süzülmüş havuz | `BoardController.RandomPool` (ambargoda çıkarılmış tür yok) |
| Dokunuş / reddetme olayı | `BoardController.TryUseBoosterAtCell`, `OnBoosterTargetRejected` |
| Heykel / mühür renk ihtiyacı | `TarpStatueService.NeedsColor`, `AncientSealService.NeedsColor` |
| 5. slot | `JokerFocusOverlayController.EnsurePaintBucketSlot` — karıştırıcı slotu çalışma anında kopyalanır; **ikon `TileIconLibrary.boosterPaintBucket` atanmadan slot oluşmaz** |
| Envanter / bulut | `booster_paint_count` (BoosterInventory, CloudSaveManifest) |
| Ödül türü | `DailySlotRewardType.Booster_PaintBucket = 24` (çark, haftalık görev JSON'unda `"Booster_PaintBucket"`) |
| Metinler | `joker_title_paint`, `joker_hint_paint`, `joker_paint_reject_*`, `shop_reward_paint` (TR/EN) |

## Bekleyen
- İkon (kullanıcı ChatGPT'de üretiyor) → `Art/Icons/Boosters/PaintBucket.png` + TileIconLibrary_Main'e bağlanacak.
- Mağaza paketi (ShopCatalog) — ikon gelince.
- Unity'de test: dönüşüm akışı, 3 hamle sayımı (dynamic input), reddetme mesajları, rozet konumu, 5 slotun çubuğa sığması.
