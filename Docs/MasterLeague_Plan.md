# Usta Ligi (çalışma adı) — seviyeler bitince yarış modu

Durum: Aşama 1 KODLANDI (2026-11-03), Unity testi bekliyor. Ad: **Usta Ligi** (kullanıcı onayı).

## Neden
Oyuncu son seviyeyi bitirince yeni paket (hedef: 2 haftada 50 seviye) gelene kadar oyunu bırakmasın. Aynı noktadaki
oyuncular birbiriyle yarışsın; geçilmiş seviyeler zorlaştırılmış halde yeniden oynanır.

## Kullanıcı kararları (2026-11-03)
- Mod, ana yoldaki son seviyeyi bitirince açılır. Ana ekranda "Yeni seviyeler X gün sonra" + Usta Ligi girişi.
- **Havuz:** katalogda **31 ve sonrası**, **Boss Duel (levelKind=1) ve Super Hard (numarası 9 ile biten) hariç**. Herkes için
  aynı havuz. Yeni seviyeler kataloğa eklendikçe havuz kendiliğinden büyür.
- **Seviyeler sürekli açık:** oyuncu dilediği kadar oynar; kilit/günlük açılım yok.
- **Tur:** her tur başında havuz **mutlak karıştırılır ve yeni liste oluşturulur** (oyuncu sırayı ezberlemesin, farklı seviye
  gibi gelsin). **Herkese aynı sıra** (kullanıcı): seed = tur no + havuz sürümü → aynı turdaki herkes aynı listeyi oynar
  (adil, takımda konuşulabilir); tur değişince liste tamamen yeniden karışır. Listedeki her seviye bir kez geçilince tur biter.
- **Zorlaşma:** 1. tur hamle −%10, sonraki her tur ek −%10. **Tavan −%30** (kullanıcı); sonrasında zorluk sabit, yalnız puan
  çarpanı artar.
  Hamle en az 5 (yuvarlama aşağı değil, en yakına).
- **Can harcanır** (normal seviye gibi; kayıpta can gider).
- **Puan:** geçilen seviye = taban puan + kalan hamle × hamle değeri, × tur çarpanı (1. tur ×1.0, sonra her tur +0.2).
  Seviye o turda tekrar oynanabilir; sayılan o turdaki EN İYİ puan (toplanmaz).
- **Yarış:** haftalık sıralama (mevcut haftalık takvim, Pazartesi). Tur ilerlemesi/kesinti haftadan bağımsız sürer.
- **Botlar:** sıralama bot ile doldurulur; puanları mantıklı: bot başına sabit beceri + günde oynadığı seviye sayısı,
  haftanın geçen süresiyle büyür (Pazartesi sabahı kimse dolu puanla görünmez). Altın bahsi yok → bot kuralı ihlali yok.

## Açık / sonraki
- Ödül tablosu (haftalık ilk 3 / ilk 10 / katılım): altın + joker + rozet — değerler kullanıcıdan.
- İsteğe bağlı 2. aşama: seviyeyi **yatay aynalı** sunmak (asimetrik seviyeler yeni gibi görünür).
- Ad ve görsel: giriş butonu, ekran başlığı, rozet.
- Seviye başına kesinti ayarı (sınırdaki seviyeler için) — sim verisi gelince.

## Teknik plan
### Aşama 1 — çekirdek (oyun içi, sunucusuz)
- `MasterLeagueState` (PlayerPrefs + cloud save manifest): tur no, karışık liste (katalog level numaraları), listedeki konum,
  bu turda geçilenler + en iyi puan, haftalık puan (hafta anahtarıyla, WeeklyScore kalıbı).
- `MasterLeaguePool`: katalogdan havuz (31+, levelKind != Boss, level % 10 != 9), tur+havuz-sürümü seed'li Fisher–Yates
  (herkeste aynı; havuz büyüyünce yeni turdan itibaren yeni seviyeler girer).
- Seviye yükleme: normal LevelData + `moves = max(5, round(moves × (1 − kesinti)))`; ana yol `current_level` ilerlemez,
  yıldız/görev/etkinlik ilerlemesi mod içinde YOK (yalnız lig puanı). Level-end popup'ı lig sonucu gösterir.
- Ana menü: son seviye bitince seviye butonu yerine "Usta Ligi — Tur N, seviye k/M" + yeni seviye sayacı.
### Aşama 2 — yarış
- Firestore `leaderboards/master_{week}/scores` (mevcut şema: name, score, updatedAt → kural değişmez).
- Liderlik ekranına Usta Ligi sekmesi; bot doldurma (BotProgression kalıbı, haftalık büyüme).
### Aşama 3 — ödül + cila
- Hafta sonu ödül talebi (Gear Cup claim kalıbı ya da yerel), rozet, giriş animasyonu, aynalama (ops.).

## Performans
Mod mevcut seviye akışını kullanır; ek runtime maliyeti yok. Havuz/karıştırma tur başında bir kez (≤ birkaç yüz int).

## Aşama 1 — uygulandı (2026-11-03)
Kullanıcı ek kararları: ligde **+5 hamle ile devam AÇIK** (normal kural); **altın normal verilir**; **yıldız verilmez**.
- `Core/MasterLeague.cs`: havuz/karışım/kesinti/puan/tur/kayıt (cloud save manifest'te). Mevcut katalog: havuz 144 seviye.
- `CurrentLevel.Played` (lig oturumunda lig seviyesi) → `LevelRuntimeSelector`, `GridSpawner` (uzak hamle ayarı + lig kesintisi),
  PreLevel hedef önizlemesi + başlık "USTA LİGİ · TUR N".
- Ana menü seviye butonu: ana yol bittiyse "USTA LİGİ"; tıklayınca sıradaki lig seviyesi oturuma alınır.
- Level sonu (`ShowMasterLeagueSuccess`): current_level ilerlemez, yıldız yok, altın normal, "+N lig puanı";
  `PlayerStats.RecordLevelCleared` → etkinlikler (Hediye Şenliği/Hasat/Köprü) kazancı görür.
- Editör menüsü TinyFixers ▸ Debug ▸ Usta Ligi (aç / ana yola dön / sıfırla / durum).
- Bilinen: analitik level_start/level_end lig seviyesini Global (max+1) numarasıyla kaydeder; LevelAssist fail sayacı
  lig seviyeleri arasında ortak (Global anahtarı) → gizli yardım ligde de devreye girebilir. Aşama 2'de ele alınacak.

## Lig teması — uygulandı (2026-11-03), Unity testi bekliyor
Kullanıcı kararı: ligde **kraliyet mavisi** alt/üst barlar, popup'lar **mavi + altın kenar**, buton çerçeveleri **metal gri**.
- Görseller mevcutların renk çevirisi (gölge/kontur korunur): `Art/UI/ScreenImages/MasterLeague/League_*.png`; meta'lar
  orijinalden kopya (9-slice aynı). Üretim betiği: ton kaydırma bordo→220°, altın→çelik (üstten ışık geçişli).
- `ChapterTheme_MasterLeague` (chapterIndex 99, ChapterTheme_9 kopyası): barlar + PreLevel/LevelEnd zeminleri mavi;
  oyun arka planı `League_GameBG_Rooftop.png` (kule çatısı, ay ışıklı gece, bej taş zemin; ham hali), lightBoardCells=1 (tahta bölgesi parlaklığı 0.54). Intro görseli `League_IntroGate.png` (kumtaşı atölye kapısı, doygunluk −%40, parlaklık ×0.85). Gri taş ("oyunu gerçekçi çerçeveye soktu") ve mavi-mor reddedildi. Intro akışı: kapı (kepenk açılır) → oyun içi asansör geçişi (kat sayacı = tur) → çatı; yalnız ilk girişte.
- Seçim: `ChapterThemeLibrary.GetCurrentTheme` → current_level > katalog son seviyesi ise lig teması (yeni paket gelince
  kendiliğinden bölüm temasına döner). `ChapterTheme.Active` statik: runtime popup'lar buradan okur.
- `ChapterTheme.spriteSwaps/materialSwaps`: sahnede sabit görseller (alt bar butonları, ButtonFrame'ler, popup zeminleri)
  ve yazı konturları (TinyFixersPop OnMaroon/OnRed → OnBlue, Inter OnRed → yeni Inter_28pt-ExtraBold_OnNavy).
  `ChapterThemeApplier` sahne başına bir kez tarar (yalnız değişim listesi doluysa; bölüm temalarında maliyet yok).
  `CommonPopupView` zemin + başlık materyali, `ButtonFrameBacking.Attach` çerçeve → `ChapterTheme.SwapActive`.
- Test: TinyFixers ▸ Debug ▸ Usta Ligi ▸ Ligi Aç → ana menüyü yeniden aç.

## Intro + düzeltmeler — uygulandı ve kullanıcı onayladı (2026-11-03)
- `UI/MasterLeagueIntro.cs`: ana yol bitip ana menüye İLK dönüşte bir kez (`master_league_intro_seen`, cloud save).
  Kapı (`ChapterTheme.introBackground`) + "USTA LİGİ" → kapıya yaklaşma + amber parlama → çatı aşağıdan kayar, kat sayacı
  1 → katalog son seviyesi → "LİG N" + açıklama + BAŞLA (klasik yeşil + metal çerçeve). Kendi üst katman Canvas'ı (sorting 900),
  dokunuş aşamayı ×4 hızlandırır. Tetik: `MainMenuLevelButtonController.Start`. Ses yok (merkezi ses kararı sonra).
- Başlık kısaldı: "LİG {0}" / "LEAGUE {0}" (PreLevel, LevelEnd ve ana menü seviye butonu).
- Lig teması değişim listesine: seçili alt sekme (`SelectedHighlight` → `League_TabSelected`), fail popup
  (`LevelEndFailPopupV1` → `League_LevelEndFailPopup`).
- Find Friends alt butonu: üretilmiş turuncu `Btn_Orange` yerine popup ailesinin klasik lacivert butonu (`secondaryButton`).
- Debug: "Introyu Tekrar Göster"; "Lig İlerlemesini Sıfırla" introyu da sıfırlar.

## Aşama 2 — yarış: uygulandı (2026-11-03), Unity/cihaz testi bekliyor
- Firestore `leaderboards/master_{yyyy-Www}/scores/{uid}` {name, score, updatedAt} — mevcut izinli şema, kural değişmez.
  Yalnız bu hafta lig puanı > 0 olan oyuncu yazılır (`PublishOwnScoreIfChanged`, imzaya lig puanı eklendi; lig kazancında
  hemen çağrılır).
- Liderlik ekranı: sahneye dokunmadan **Haftalık sekmesine alt seçenek** "Haftalık | Usta Ligi" (yalnız `MasterLeague.Reached`
  — lige bir kez ulaşan; yeni paket gelse de görünür kalır). Ayrı 5. sekme yerine bu seçildi (sahne/sekme düzeni değişmez).
- Lig botları `BotProgression.MasterWeeklyScore`: günde 2–14 lig seviyesi, kazanma 0.55–0.85, ortalama kalan hamle 2–8,
  %35'i 2–3. turda; %40'ı haftaya 0–2.5 gün geç katılır (Pazartesi sabahı boş/az). Puan gerçek kuralla aynı ölçek.
  Bot sayısı = clamp(BotPopulation.ActiveCount / 30, 120, 500); isim aralığı 60000+.
- Deneme kaydı ve analitik `CurrentLevel.Played` (lig seviyesinin kendi numarası): deneme sayaçları tek seviyelik, ligdeyken
  ana yolda aktif deneme yok → çakışmaz. GA4 `level` boyutu ligde **"L57"** (kullanıcı kararı; ana yol "000057"), kullanıcı
  özelliği `current_level` ligde güncellenmez (ana yol ilerlemesi kalır). **Gizli yardım ligde KAPALI** (yarış adil kalsın).

## "LİG N" numarası (2026-11-03, kullanıcı)
Görünen numara tur değil, **ligde kazanılan seviye sayısı + 1** (`MasterLeague.LevelNumber`, `master_league_cleared`, cloud):
ana yoldaki seviye numarası gibi her yeni kazançta artar, turlar arasında sıfırlanmaz. Tur (hamle kesintisi, puan çarpanı) iç
kural olarak kalır. Ana menü butonu, PreLevel, LevelEnd ve intro aynı numarayı gösterir.

## Lig özeti — uygulandı (2026-11-03), Unity testi bekliyor
- `UI/MasterLeagueSummary.cs` (CommonPopupView): lige ulaşmış + ana yol yeniden açık (yeni paket) + dönemde ≥1 lig kazancı →
  ana menüye ilk dönüşte bir kez. Satırlar: kazanılan lig seviyesi, toplam lig puanı, en iyi haftalık puan, en iyi haftalık
  sıra (liderlik ekranında lig tablosu görüldüyse), ligde kazanılan altın, ulaşılan tur. "Yeni Seviyeler" → kapanır ve dönem
  istatistikleri (`master_league_stint_*`, cloud) sıfırlanır.
- Ana menü açılışında intro ya da özet açıldıysa o açılışta bildirim izni sorulmaz.
- Debug: "Özeti Dene (yeni paket geldi say)" — dönem verisi yoksa örnek değerler yazar, current_level'ı son seviyeye çeker.
- Ana menü butonları (kullanıcı): LİG butonu ligde de ORİJİNAL KIRMIZI kalır (lacivert denendi, 2026-11-04 "mavi çirkin durdu"); Görevler butonu
  sarı kalır, yalnız kenarı metal gri (`League_MissionsButton`). Tema alanları menuLevelSelectorBtn/menuRepairBtn + değişim
  listesi. Kenar ayrımı: saydam kenara uzaklık + sıcak ton maskesi (gövde sarısı korunur).

## Aşama 3 — haftalık ödül + rozet: uygulandı (2026-11-03), Unity/cihaz testi bekliyor
Kullanıcı kararı: **joker yok**, yalnız altın + rozet. 1. → 1.500 + altın rozet · 2–3. → 1.000 + gümüş · 4–10. → 500 + bronz
· 11–50. → 250 · katılım → 100. Rozet görselleri kullanıcıdan (kule + defne, mavi kurdele): `Resources/MasterLeague/MasterBadge_{Gold,Silver,Bronze}.png`.
- `Core/MasterLeagueRewards.cs`: hafta devri (`RollWeek`: geçen haftanın puanı bekleyen ödüle taşınır; lig kazancından önce
  ve ana menüde), `Claim` (altın + defter + "alındı haftası" checkpoint tek kayıtta → iki kez alınamaz), rozet sayıları.
- Kesin sıra `FirebaseLeaderboardService.FetchMasterFinalRank`: 1 + hafta sonu puanı yüksek bot (`BotProgression.MasterFinalScore`,
  aynı tohum, 7 gün) + Firestore'da puanı yüksek gerçek oyuncu (`WhereGreaterThan`, ≤60). Çevrimdışıysa ödül bekler.
- `UI/MasterLeagueRewardPopup.cs`: ana menüde (intro → ödül → özet sırası), "Geçen hafta N. oldun!" + rozet + altın + Topla.
- `UI/MasterBadgeView.cs`: profil avatarının sağ alt köşesinde en değerli rozet + ×N.
- Debug: "Haftalık Ödülü Dene", "Rozetleri Sıfırla".
### Sonra (kullanıcı: takım/sıralama ekranlarında da görünsün)
Başka oyuncunun rozeti için Firestore'da `badge` alanı gerekir: `leaderboards/{board}/scores` kuralı `hasOnly([...,'badge'])`
+ `badge is int && 0..3`; takım üyeleri/sohbet için de aynı alan. Kural yayını kullanıcıda — yayından ÖNCE alan yazılmaz.

## Lig sezonu (kullanıcı kuralı 2026-11-04)
Ana yol her bittiğinde lig SIFIRDAN başlar: yeni intro, "LİG 1", 1. tur, yeni karışık liste (havuz yeni seviyelerle büyür →
sıra da yeni). Önceki sezondan devam YOK. Sezon kimliği `master_league_season` = lig açıldığındaki katalog son seviyesi;
değişince (`MasterLeague.IsUnlocked` içinde) tur, imza, bu turda geçilenler, lig numarası ve intro bayrağı silinir. Haftalık
puan, bekleyen ödül, rozetler ve özet istatistikleri korunur.
Açılış zinciri düzeltmesi: haftalık ödül girişi (auth) hazır olunca sırayı hesaplar; hesaplanamazsa sıra lig özetine /
bildirim iznine geçer (önceden ödül kaydı özeti engelliyordu). Editör Play sıfırlaması lig kayıtlarını korur; debug menüsü
seviyeyi EditorTestSettings.testLevel'a da yazar.
