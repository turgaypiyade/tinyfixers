# Bölgesel normal clear ve dinamik special tap

## Uygulanan kapsam

- `useDynamicBoardInputGate` ve `useFlowActivities` açıkken, special üretmeyen normal clear yalnız eşleşen taşların sütunlarını ve sağ/sol komşu sütunlarını input'a kapatır.
- Bu bölgenin dışında normal swap ve special tap kabul edilir. Clear sırasında kabul edilen tek hamle, mevcut action'lar bitince taş kimliği, koordinat ve kilitler yeniden doğrulanarak işlenir. Bekleme sırasında hamle harcanmaz.
- Yalnız düşüş varsa mevcut dinamik swap yolu korunur; aynı hücre uygunluk kontrolüyle special tap de kabul edilir. Aktivasyon mevcut action kuyruğuna katılır.
- Dinamik normal eşleşmenin tüm katılımcıları sabit ve oynanabilir olmalıdır; yalnız swap uçlarının sabit olması yeterli değildir.
- Bir dinamik hamle işlenirken ikinci dinamik hamle başlatılmaz.
- Special üretim sunumları, special efektleri, combo ve obstacle spread için genel kilit korunur. Bu değişiklik Line/Pulse/PatchBot efektleri sırasında bölgesel aktivasyon açmaz.

## Play Mode doğrulaması

Bu senaryolar henüz çalıştırılmadı. İncelemeyi kolaylaştırmak için mevcut dinamik input slow-motion ayarı kullanılabilir.

| Senaryo | Beklenen |
| --- | --- |
| Sol tarafta dikey üçlü temizlenirken en sağda geçerli normal swap | Girdi kabul edilir; clear sonrasında bir kez uygulanır, moves bir azalır. |
| Aynı durumda eşleşmesiz normal swap | Taşlar geri döner, moves değişmez. |
| Temizlenen sütunda veya hemen yanındaki sütunda drag/tap | Girdi kabul edilmez. |
| Uzak sütunlar düşerken sabit special'a tek tık | Bir aktivasyon kuyruğa girer, moves bir azalır; board sonunda idle olur. |
| Special sabit fakat tüm komşuları düşüyor/kilitli | Kendi hücresi uygunsa tek tık kullanılabilir; güvenli partneri olmayan swap yapılamaz. |
| Swap uçları sabit, yatay eşleşmenin üçüncü taşı başka sütunda düşüyor | Swap geri alınır, hareketli taş clear'a katılmaz, moves değişmez. |
| Clear sırasında bir hamle verip art arda tap/drag | Yalnız ilk kabul edilen hamle işlenir; çift moves tüketimi veya çift special aktivasyonu olmaz. |
| Bekleyen girdinin taşı başka hücreye taşınır veya hücre kilitlenir | Bekleyen hamle iptal edilir, moves düşmez. |
| Grass + altında engel bulunan levelda geçersiz swap | Taş ve katmanlar geri alınır; moves değişmez. |
| Oil, kafes veya pending-triggered special hücresine giriş | Kilit aşılmaz. |
| Line/Pulse/PatchBot zinciri veya special üretim animasyonu | Genel kilit devam eder; zincir hedeflerine müdahale edilmez. |
| Son hamlede hızlı tap/drag, ardından level sonu | Moves negatif olmaz; board idle/level sonu akışına ulaşır. |

Scope'lar ref-count ile tutulur: üst üste gelen iki clear'dan biri bittiğinde ortak sütun açılmaz. `DrainAll` eski scope'ları geçersiz kılar; eski handle'ın sonradan bırakılması yeni scope'ları etkilemez.
