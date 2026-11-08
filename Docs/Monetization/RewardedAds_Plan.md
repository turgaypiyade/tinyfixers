# Ödüllü Reklam Genişletme Planı

Durum: **UYGULANDI (2026-11-08, yayından önce — kullanıcı kararı)**, Unity/cihaz testi bekliyor.
Kapsam kararı: fail ekranı + ayrı can reklamı + çark (günde 1). **×2 altın YOK**, **PreLevel bedava özel taş KALDIRILDI**
(kullanıcı ekranda gördü: yuva satırına sığmadı, "bunu kaldırabiliriz")
(kazanma ödülü sabit 10 altın; ×2 = +10 değmez — kullanıcı: "x2'yi ekleme").
Kod: `Core/Backend/RewardedAdCaps.cs` (tek kapı + günlük sınır + ad_offer/ad_reward analitiği),
`UI/AdRewardButton.cs` (çerçeve + ▶ disk + kurdele, Resources/UIButtons/Ad_*), entegrasyon:
`LevelEndSimplePopupController` (fail), `LivesRefillOffer` + `MainMenuLivesDisplay` (can), `FortuneWheelController`
(çark). Analitik `placement`: 0 Continue, 1 Life, 2 WheelSpin.

## Neden

Bugün reklam hakkı **seviye başına tek**: fail'de bedava devam ya da can bitince +1 can, hangisi önce
(`LevelAdRight`, `UI/LevelEndSimplePopupController.cs` `WatchAdThenContinue`, `UI/LivesRefillOffer.cs`,
`UI/MainMenuLivesDisplay.cs` `StartAd`). Sonuç:

- Oyuncu ömür boyu en fazla seviye sayısı kadar (~250) reklam izleyebilir; günde ~1.
- Bir seviyede takılıp canı biten oyuncu, o seviyenin hakkını harcadıysa reklamla can alamaz → bekler ya da bırakır.

Tahmin (ölçüm değil): TR/PH ödüllü eCPM ~1–5 $; 1.000 DAU × ~1 reklam/gün ≈ 1–5 $/gün.

## İlkeler

- **Yalnız ödüllü, oyuncunun seçtiği reklam.** Geçiş (interstitial) ve banner YOK — "adil oyun" kuralı, elde tutma.
- Her yerin **günlük sınırı** var; altın/can satışını öldürmez.
- Hak yalnız reklam **gerçekten izlenince** harcanır (bugünkü `TryConsume` sonrası ödül kalıbı korunur).
- Reklam hazır değilse buton gizlenmez, mevcut "Reklam hazır değil" penceresi gösterilir.
- Tüm gösterim `AdService.ShowRewarded` üzerinden; ses ayarı `AdService.ApplySoundSetting` ile zaten bağlı.

## Yerler

| # | Yer | Ödül | Günlük sınır | Not |
|---|---|---|---|---|
| 1 | Seviye kazanınca (level-end success) | Kazanılan altını ×2 | 5 | Türde en çok izlenen. Yalnız altın; yıldız/puan ikiye katlanmaz. |
| 2 | Can bitince (ana menü + PreLevel + fail→can yok) | +1 can | 3 | `LevelAdRight`'tan **ayrılır**; kendi günlük sayacı. |
| 3 | Günlük çark (`FortuneWheelController`) | +1 ek çevirme (bedava havuzdan) | 1 | Bedava çevirme kullanıldıktan sonra görünür. |
| 4 | Seviye öncesi (PreLevel) | 1 bedava joker (rastgele: çekiç/roket/karıştırıcı) | 1 | Seviye başlamadan; zor seviyelerde değer. |
| 5 | Fail'de devam | Bugünkü gibi +hamle | Seviye başına 1 | `LevelAdRight` yalnız buna kalır. |

Beklenen: günde ~3–5 reklam/oyuncu (tahmin).

## Ekran / UX

### Ortak "reklam butonu" dili (her yerde aynı)
- Tek bileşen (karar 2026-11-08): **yuvarlak ▶ ikon butonu + köşede "ÜCRETSİZ" kurdelesi**, ilgili ana butonun
  yanında. Renk altınlı butondan farklı (oyuncu "bu altın harcatmaz" diye bir bakışta ayırsın). İkon sprite'ı
  projede yok → hazırlanacak.
- **Reklam hazır değilse ya da günlük sınır dolduysa buton GİZLENİR** (pasif/gri bırakılmaz; "Reklam hazır değil"
  çıkmazına düşürmez). İstisna: fail ekranı aşağıda.
- Kalan hak sayısı gösterilmez (sade); yalnız can reklamında "Bugün 2 hak" küçük alt yazı olabilir.

### 1) Fail ekranı — ÖNCE BU (bugünkü sorun)
Bugün: tek buton "900 altınla devam". Reklam seçeneği yalnız altın YETMEZSE çıkan ikinci popup'ta
(`HandleBuyMovesClicked`) → altını olan oyuncu reklamla bedava devam edebileceğini hiç görmez.

**KARAR (kullanıcı, 2026-11-08): B — ana butonun yanında yuvarlak ▶ ikon butonu.** İki tam boy buton çirkin
duruyordu; link fazla web gibi/gözden kaçar.
```
          +5 HAMLE
 ┌─────────────────────┐ ┌────┐
 │   🪙 900  DEVAM ET   │ │ ▶  │   ← köşede küçük "ÜCRETSİZ" kurdelesi
 └─────────────────────┘ └────┘
               Vazgeç
```
- **Ana buton tek, iki anlam:** altın yetiyorsa "🪙 900 Devam et" (bugünkü), yetmiyorsa aynı buton
  "Altın al" → oyun içi market. Bugünkü ikinci "Yetersiz altın" popup'ı (`HandleBuyMovesClicked` →
  `RuntimeChoicePopup.ShowOffer`) KALKAR → tek ekran, tek karar.
- **▶ butonu hep görünür** (altın olsun olmasın) — seviye başına tek hak; izlenince kaybolur.
  Reklam o an hazır değilse yine görünür; basınca mevcut "Reklam hazır değil" penceresi, hak harcanmaz.
- Yalnız ikon anlaşılmayabilir → "ÜCRETSİZ" kurdelesi şart; ikon oyun tarzı ▶ / TV (AdMob logosu KULLANILMAZ:
  oyuncu tanımaz + Google marka kuralı). Sprite projede yok → hazırlanacak.
- Aynı yuvarlak ▶ + kurdele bileşeni diğer yerlerde de kullanılır (tek görsel dil).
- Ekonomi notu: ilk fail'de çoğu oyuncu reklamı seçer → ilk devamda altın harcaması düşer; ikinci fail'den
  itibaren yalnız altın (artan bedel) kalır. Bu adil ve anlaşılır; IAP etkisi ilk hafta ölçülür.

### 2) Seviye kazanınca — Altın ×2
```
   ⭐ 3      🪙 +40
 [ ▶ 2 katına çıkar  ÜCRETSİZ ]   ← küçük, ikincil
 [        DEVAM               ]   ← ana buton, değişmez
```
- Altın sayımı bitince belirir; izlenince sayaç +40 daha sayar (aynı uçuş animasyonu). Bir kez basılır.
- Ana akışı yavaşlatmaz: DEVAM her zaman aynı yerde, reklam butonu onun üstünde ve daha küçük.

### 3) Can bitince
Bugünkü can popup'ı (`LivesRefillOffer`) zaten "Reklam izle" seçeneği taşıyor; değişen yalnız kural
(seviye hakkından ayrı, günde 3). Görsel değişiklik: ortak reklam butonu stili + "Bugün 2 hak" alt yazı.

### 4) Günlük çark
Bedava çevirme kullanılınca çevir butonunun yanında: `[▶ Tekrar çevir ÜCRETSİZ]` ve bugünkü `[🪙 100 çevir]`.
Günde 1 kez; kullanınca reklam butonu kaybolur.

### 5) Seviye öncesi (PreLevel) — bedava joker
Joker seçim satırının sonunda tek bir kart: `▶ Bedava joker`. İzlenince joker kutudan çıkar, envantere eklenir
ve otomatik seçili gelir. Günde 1; kullanınca kart kaybolur. Satır kalabalıklaşırsa kart yerine küçük rozet.

### Akış kuralları
- Reklam kapanınca oyuncu aynı ekrana döner, ödül animasyonla gösterilir (sessiz arka plan değişimi yok).
- Reklam yarıda kapatılırsa: ödül yok, hak harcanmaz, buton yerinde kalır.
- Hiçbir ekranda reklam butonu ana butonun yerini almaz (yanlışlıkla reklam açılmasın).

## Teknik tasarım

- **`RewardedAdCaps`** (yeni, `Core/Backend/` ya da `Core/`): tek merkez. `enum AdPlacement { DoubleCoins, Life, WheelSpin, PreLevelJoker, Continue }`.
  - `bool CanOffer(AdPlacement)`, `bool TryConsume(AdPlacement)`; sayaçlar PlayerPrefs, gün anahtarı yerel tarih (`yyyyMMdd`).
  - Sınırlar tek ScriptableObject/asset'te; ileride Firestore `config/ads`'ten yalnız düşürülebilir (sunucu yükseltemez → hile/hata koruması).
  - `Continue` → mevcut `LevelAdRight` sarmalanır (kayıt anahtarı `ad_continue_used_level` değişmez).
- **Can reklamı**: `LivesRefillOffer.WatchAd` ve `MainMenuLivesDisplay.StartAd` `LevelAdRight` yerine `RewardedAdCaps.Life` kullanır.
- **×2 altın**: level-end success panelinde ödül sayımı bitmeden buton; izlenince `PlayerWallet.AddCoinsWithCheckpoint` ile
  ek altın (aynı checkpoint kalıbı → çift ödeme yok). Buton bir kez basılır.
- **Çark**: `FortuneWheelController` bedava hakkı bitince "Reklam izle, tekrar çevir" butonu; ödül havuzu `freeSpinConfig`.
- **PreLevel joker**: `PreLevelSpecialPopupController` yanına küçük reklam rozeti; ödül oturum değil envanter jokeri.
- **Bulut kaydı**: günlük sayaçlar cihaz yerelinde kalır (`CloudSaveManifest`'e girmez); verilen ödüller zaten
  cüzdan/envanter yoluyla kaydedilir.

## Analitik

- Yeni GA4 olayı `ad_reward`: `placement`, `level`, `granted` (bool). `GameAnalytics.Log` üzerinden.
- `ad_offer_shown` (placement) — buton gösterimi; izlenme oranı = ad_reward / ad_offer_shown.
- GA4 özel boyut: `placement`.
- İlk hafta bakılacak: yer başına izlenme oranı, günlük sınıra dayanan oyuncu yüzdesi, IAP'ye etkisi.

## Test

- Development build (Google test reklamları) ile her yer: izle → ödül; yarıda kapat → ödül yok, hak kalır.
- Günlük sınır dolunca buton pasif + metin ("Yarın yine gel").
- Gün değişimi (cihaz saati) → sayaçlar sıfırlanır.
- Ses kapalıyken reklam sessiz (`MobileAds.SetApplicationMuted`).
- Uçak modunda: "Reklam hazır değil" penceresi, hak harcanmaz.

## Açık sorular (uygulamadan önce kullanıcıya)

1. ×2 altın sınırı 5 mi, yoksa her kazanç mı?
2. PreLevel jokeri hangi jokerler arasından?
3. Can reklamı günde 3 yeterli mi?
4. Lokalizasyon: buton metinleri TR/EN (`ad_double_coins`, `ad_extra_life`, `ad_extra_spin`, `ad_free_joker`, `ad_daily_limit`).
