# PvP Düello Event'i — Plan Notu

> Durum: **Tasarım kilitli, kod yok.** Oyun canlıya çıktıktan sonra, oyuncu tutma (retention) verisine
> göre eklenecek. Bu not o gün doğrudan uygulamaya geçebilmek için yazıldı.
>
> Karar tarihi: 2026-10-04

## Fikir

Boss Duel'in (hayvan modu) gerçek oyuncuya karşı oynanan hâli. Rakip hayvan yerine diğer oyuncu kendi
karakteriyle çıkar. İki oyuncu altın koyar, kazanan potu alır.

## Kurallar

### Açılma şartı
- Oyunun toplam kayıtlı kullanıcısı **10.000'i geçince** event aktif olur.
- Oyuncu ayrıca **70. level'ı geçmiş** olmalı.

### Giriş ve pot
- Her oyuncu **300 altın** koyar → ortada **600 altın**, kazanan hepsini alır.
- Altın oyun **dışına asla çıkmaz**: paraya çevrilmez, oyuncular arası transfer yok.
- Booster'lar düelloda **kapalı** (ileride belki).

### Eşleşme
- Yalnız **canlı**: iki oyuncu da o an online olmalı.
- Havuz: event'e kayıtlı ve o an **"Rakip Ara"** diyen oyuncular.
- Oyuncu sayısı arttıkça yakın ilerlemedekiler eşleşir: oyuncunun level'ı ±25 aralığı
  (ör. 25'teki oyuncu 0–50 arasıyla).
- Arkadaş daveti: arkadaş online ise davet gönderilir, kabul ederse oynarlar.
- Rakip **4–5 saniyede** bulunamazsa arama iptal; altın alınmaz.
- **Bot yok, kayıtlı (asenkron) rakip yok.** (Altın ortadayken botu gerçek oyuncu gibi göstermek
  yanıltıcı sayılır.)

### Oynanış
- İki oyuncu aynı anda, birbirini beklemeden oynar.
- Her hamlede kırılan taşlar bir vuruş olur; vuruşlar kuyruğa girer ve rakibe sırayla gider.
  (Boss Duel'deki "kırdığın taş kadar darbe" mantığı.)
- İki oyuncunun taş dizilimi farklı olabilir.
- Eşit can ve eşit hamle sayısıyla başlanır.
- Kazanan: rakibin canını önce bitiren. Hamleler biterse canı çok kalan.

### Bağlantı / çıkış
- Bağlantı koparsa **15–20 saniye** geri dönme süresi. Dönemezse kaybeder.
- Oyundan çıkan kaybeder; maç sonuna kadar oynanır.

### Beraberlik
- İlk beraberlikte **+5 hamle uzatma**.
- Uzatmada da berabere → iki oyuncunun 300 altını **iade**.

## Teknik notlar (uygulama günü için)

- **Sunucu otoritesi şart.** Pot maç başında sunucuda emanete alınır, sonucu sunucu belirler ve
  dağıtır (Firebase Cloud Function + mevcut altın defteri / currency ledger). Telefon "kazandım" diyemez.
- Canlı vuruş akışı için düşük gecikmeli kanal (Firebase Realtime Database) Firestore'dan uygun.
- Yeniden kullanılacaklar: Boss Duel düello ekranı, can barları, güç göstergesi, darbe akışı,
  hayvan karakter profilleri.
- Mağaza: model 8 Ball Pool benzeri (beceri tabanlı, cash-out yok, pot sunucuda). Yayın öncesi
  yaş derecelendirme anketinde dikkatli doldurulmalı.

## Açık sorular

- İki oyuncuya aynı başlangıç board'u (aynı seed) verilsin mi?
- Oyun pottan pay alsın mı (ör. 600 yerine 540)?

## İlgili dokümanlar

- `Docs/BossDuel_Plan.md`, `Docs/BossDuel_Animal_Pilot.md`
