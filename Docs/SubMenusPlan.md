# Alt-Menü Ekranları Planı — Journey / Rank / Team / Marketplace

Ana menü alt çubuğundaki (`BottomBar` / `BottomTabController`) dört sekmenin açacağı
içerik ekranları. Butonlar + ikonlar + yazılar sahnede ZATEN var
(`ButtonJourney/ButtonRanks/ButtonTeam/ButtonMarket`); eksik olan açtıkları **içerik
panelleri**. Referans: Match Masters / Royal Match ailesi (kullanıcı 4 ekran görseli verdi).

## Kararlar (kilit)

- **Mimari:** Her ekran mevcut panel konvansiyonuna uyar (`RegionUnlockListPanel` kalıbı):
  bir `Controller` (panelRoot + CanvasGroup + close butonları + `itemPrefab` + `container`)
  + bir satır/kart bileşeni. Prefab'lar container'a basılır, el-yazımı coroutine ile animasyon.
  Kullanıcı sadece Inspector'da prefab + container bağlar.
- **Veri:** Statik içerik `ScriptableObject` katalogdan (Shop) gelir. Dinamik/çok-oyuncu
  içerik (Rank/Team) bir **servis arayüzü** arkasından gelir; v1'de **mock** uygulama,
  backend kararından sonra aynı arayüze gerçek implementasyon takılır.
- **Backend:** Şimdilik YOK. Rank/Team mock ile çalışır. Backend seçimi (UGS / Firebase /
  PlayFab) ertelendi; kod arayüzü değişime hazır.
- **Tema:** Tek `UITheme` SO — palet + spacing + paylaşılan sprite/font. Renkleri biz
  veriyoruz (referanslar mor; bizim oyuna göre amber-trim'li sıcak mor varyantı).
- **Sanat vs Kod ayrımı:** Layout + bileşen + mantık + veri + tema = bizde. İllüstrasyon
  (karakter banner'ları, item ikonları, teklif görselleri) = kullanıcı sağlar, Inspector'da
  bağlanır.

## Palet (UITheme defaultları)

| Rol | Hex |
|---|---|
| Ekran zemini (koyu) | `#2B2350` |
| Panel yüzeyi | `#51458C` |
| Krem kart yüzeyi | `#F3E9D8` |
| Başlık bandı | `#6E54B5` |
| Özel teklif bandı (magenta) | `#8E2150` |
| Accent / coin (amber) | `#FFB23E` |
| Altın kenar | `#F5A623` |
| CTA / onay yeşili | `#4FC95B` |
| Fiyat yeşili | `#5BD15B` |
| Can kırmızı | `#FF4D5E` |
| Bilgi mavi | `#36A6E0` |
| Açık metin | `#FFFFFF` |
| Alt metin | `#C9BEEA` |
| Krem üstü metin | `#5A3E2B` |

## Ekranlar

### 1. Mağaza (Marketplace) — yerel, backend yok
- Üstte coin bakiyesi + başlık.
- Bölümler (`ShopSection`): "Özel Teklifler", "Mega Fırsatlar", "Coin Paketleri" ...
- Teklif kartı (`ShopOffer`): sol büyük görsel (coin yığını/kupa) + miktar, orta/sağ
  içerik ikon gridi (booster + sonsuz/süreli rozet), altta isim + fiyat butonu.
- Fiyat türü: Coin / Yıldız / Gerçek-Para(TL) / Bedava. Coin/Yıldız → `PlayerWallet`
  ile anında satın alma + ödül grant. TL → v1'de "yakında" (IAP entegre değil).
- Ödül grant: `ShopRewardGranter` → PlayerWallet.AddCoins / BoosterInventory.Add / (lives TODO).
- Carousel (özel teklif sayfalama + noktalar): v2.

### 2. Liderlik Panosu (Rank) — mock
- Sekmeler: Haftalık / Arkadaşlar / Oyuncular / Takım.
- Üstte yarışma başlığı + kalan süre.
- Top-3 büyük vurgulu kart (avatar + karakter görseli + madalya + hediye).
- Altında sıralı satırlar (sıra no + avatar + isim + puan). Kendi satırın yeşil vurgulu.
- `ILeaderboardService` → `MockLeaderboardService`. Kendi skoru gerçek (`PlayerWallet`/level).

### 3. Takım (Team) — mock
- Başlık + amblem + takım adı + "Takım Bilgisi".
- Hediye ilerleme çubuğu + sayaç + görev banner'ı.
- Sohbet akışı (avatar + mesaj balonu, zaman).
- Can istekleri (kalp + ilerleme + "Yardım").
- Alt: "Can İste" / "Mesaj".
- `ITeamService` → `MockTeamService`.

### 4. Yolculuk (Journey) — yerel
- Level yolu DEĞİL. Tamir görevleriyle (workshop, ~9-10 görev) oluşan **chapter arka plan
  resmi** cache'lenir; burada büyük kart olarak gösterilir ("İzle" + "Bölüm X").
- Altta sonraki bölümün önizlemesi.
- Kaynak: `ChapterTheme.menuBackground/gameBackground` + `WorkshopController/WorkshopStageData`.

## Build sırası
1. UITheme (temel) ✅ önce
2. Mağaza (en bağımsız, ekonomi hazır)
3. Liderlik Panosu (mock)
4. Takım (mock)
5. Yolculuk (mevcut sistemlere bağlanır)
