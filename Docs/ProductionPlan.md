# Production Planı (canlıya çıkış)

Karar tarihi: 2026-07-18. Kural: **bu andan itibaren her iş production-hazır** yapılır;
mock/sim ancak açıkça işaretlenip production'a geçiş adımıyla yazılır.

## Kilit kararlar

1. **Bot evreni** (eski karar teyit): canlıya SIFIR arkadaş/takımla ÇIKILMAZ.
   10-20k simüle kullanıcı ile başlanır; gerçek kullanıcı geldikçe bot oranı azaltılır.
   - Botlar Firestore'a YAZILMAZ — deterministik üretilir (NamePool 10K isim +
     BotProgression zaman-bazlı skor). Her cihazda aynı isim aynı skoru görür,
     sunucu maliyeti sıfır. Azaltma tek yerden: `BotPopulation.ActiveCount`
     (gerçek oyuncu sayısına göre eğri, Remote Config'e bağlanabilir).
   - GERÇEK olmak zorunda olanlar: ID ile oyuncu arama, arkadaş ekleme (karşılıklı
     görünürlük), takım üyeliği + sohbeti (aynı takımdaki gerçek oyuncular birbirini
     görür). Bot takımlar dizinde görünür; gerçek oyuncu bot takıma katılırsa o takım
     o anda Firestore'da gerçekleşir (lazy materialization).
2. **Sosyal login**: Google + **Sign in with Apple (iOS'ta üçüncü parti login sunuyorsan
   Apple ZORUNLU kılar)** + Facebook. NOT: Instagram oyunlara login SDK'sı sunmaz;
   Meta tarafı = Facebook Login (Instagram hesabıyla bağlantılı çalışır). Akış: mevcut
   anonim Firebase Auth hesabı **link** edilir (ilerleme kaybolmaz), çakışmada
   "hangi kayıt?" seçimi.
3. **Backend**: Firebase (proje tinyfixers-bc1cd). Firestore ana veri; Cloud Functions
   yalnız gerekirse (v1'de kaçınıyoruz — rules ile çözülebilenler rules'ta).

4. **Monetizasyon/analitik yığını (2026-07-19, KİLİTLİ — kullanıcı onayı):**
   - Reklam: **Google AdMob** (rewarded ana format; ölçek büyüyünce AdMod mediation
     altına Unity Ads eklenebilir).
   - Analytics: **Firebase Analytics** (+ level_start/complete/fail, ekonomi, sosyal
     event'leri kod tarafında eklenecek).
   - IAP: **Unity IAP** → Apple App Store / Google Play Billing (zorunlu kanal);
     ürün tanımları App Store Connect / Play Console'da (KULLANICI adımı).
   - Kullanıcı import listesi: FirebaseCrashlytics + FirebaseAnalytics + AdMob SDK.

## Fazlar

### P1 — Cloud save (en kritik: veri kaybı)
- PlayerPrefs kritik anahtar envanteri → `CloudSaveManifest` (tek liste, tek gerçek).
- `FirebaseCloudSaveService`: boot'ta restore (yerel boşsa/eskiyse), oyun içinde
  debounced yazma (ör. 10 sn + önemli olaylarda anında), `users/{uid}/save/main`.
- Çakışma politikası v1: **en yüksek ilerleme kazanır** (current_level, sonra updatedAt).
- Reinstall/cihaz değişimi: login sonrası restore.

### P2 — Güvenlik
- `firestore.rules` repo'da tutulur, Firebase CLI/console ile deploy edilir:
  - users/{uid}: yalnız sahibi okur/yazar.
  - leaderboards/*/scores/{uid}: herkes okur; yalnız sahibi yazar; alan tipleri +
    makul aralık doğrulaması.
  - players/{uid}: herkes okur (arama için gerekli alanlar); yalnız sahibi yazar.
  - teams/*: okuma herkes; üyelik/sohbet yazımı yalnız üye; kapasite kontrolü.
- App Check (Play Integrity / DeviceCheck) — console'dan aktive, SDK bayrağı.

### P3 — Gerçek sosyal çekirdek
- `players/{uid}`: name, nameLower, friendCode (benzersiz), chapter, region, avatar,
  teamId, updatedAt. Boot'ta upsert.
- Arkadaş: `users/{uid}/friends/{friendUid}` (+ karşı tarafta görünürlük için
  friendCode araması gerçek). Öneriler = bot havuzu + (varsa) gerçek oyuncular.
- Takım: `teams/{teamId}` (name, nameLower, emblem, desc, minChapter, memberCount,
  capacity), `teams/{id}/members/{uid}`, `teams/{id}/chat/{msgId}` (son N mesaj).
  Ara = Firestore prefix sorgusu + bot takımlarla harmanlama; Oluştur = gerçek doc.
- Liderlik Türkiye: `players.region` yazılır (device locale), gerçek oyuncular
  bölge filtresiyle gelir; bot dolgu bölge havuzundan.

### P4 — Sosyal login
- Firebase console: Google/Apple/Facebook provider'ları aç (KULLANICI adımı).
- SDK'lar: Google Sign-In (iOS URL scheme), Apple (Unity SIWA plugin), Facebook SDK
  (KULLANICI import adımı — app id gerekir).
- `AuthLinkService`: anonim → credential link; "already-in-use" çakışmasında
  cloud-save karşılaştırıp kullanıcıya seçim sun.
- Profil ekranına "Hesabını bağla" bölümü (kayıp ilerleme koruması mesajıyla).

### P5 — Launch hijyeni
- Crashlytics + Analytics doğrulama (SDK var mı, DebugSymbols upload).
- Log kapısı: Debug.Log'lar release build'de kapalı (Logger.filterLogType /
  koşullu derleme); EnableSpecialChainTrace default off.
- Unity IAP: TL teklifleri gerçek ürünlere bağlanır (App Store Connect ürünleri —
  KULLANICI adımı), receipt doğrulama v1 client-side.
- ATT (iOS) + consent akışı; gizlilik politikası linki.
- Test kalıntıları denetimi (EditorTestSettings, debug menüler, LevelCatalog CSV).

## Sıra ve durum

P1 → P2 → P3 → P4 → P5. Her faz bitiminde kullanıcı testi.
- P1: KOD TAMAM (2026-07-19) — `CloudSaveManifest` (anahtar envanteri) +
  `FirebaseCloudSaveService` (boot restore "yüksek level kazanır", dirty-push 15sn,
  pause/quit push, restore çözülmeden push yok). TEST: cihazda oyna → sil → yeniden kur
  → ilerleme geri gelmeli. NOT: yeni kalıcı anahtar ekleyen CloudSaveManifest'e de eklemeli.
- P2: RULES DOSYASI HAZIR (`firestore.rules`, repo kökü) — KULLANICI ADIMI:
  Firebase Console → Firestore → Rules → yapıştır → Publish. App Check ayrıca ele alınacak.
- P3a: KOD TAMAM (2026-07-19) — `players/{uid}` dizini (`PlayerDirectoryService`:
  boot upsert + cloud-save ritminde UpsertIfChanged + friendCode sorgusu + region tespiti);
  ID araması GERÇEK (bot fallback kaldırıldı, `FriendDirectory.SearchByCode` async,
  popup kilitli-buton arama durumu); `FriendState.RealFriends` (uid|isim|bölüm, kalıcı +
  cloud-save'e dahil); `BotPopulation` (15k evren, görülen gerçek oyuncu başına ~25 bot
  emekli, taban 1500; liderlik sıraları artık evrene göre — pinli self #6543 gibi);
  NamePool sarım son-eki (10k isim → 15k benzersiz bot); takım evreni ~ActiveCount/40.
- P3b: KOD TAMAM (2026-07-19) — `FirebaseTeamCloud` (teams/{id}+members+chat modeli;
  oluştur auto-id, gerçek katıl Increment, bot takım lazy-materialize "bot_{seed}" —
  aynı bot takımı seçen iki gerçek oyuncu aynı dokümanda buluşur; görünen üye =
  botMembers+realMembers; yazımlar optimistik, offline'da Firestore cache senkronlar);
  `TeamDirectory.Browse` async gerçek+bot harman (gerçek önce, isim/id dedupe;
  Firestore yoksa bot fallback → ekran boş kalmaz); `FirebaseTeamService` (ITeamService,
  canlı chat Listen son-30, kendi mesajın local-cache'ten anında, bot takımlarda
  deterministik bot sohbet harmanı, IDisposable → ResetTeam dinleyiciyi kapatır);
  `PlayerTeamState.TeamId` (+cloud-save manifest); ITeamService.OnChanged eklendi,
  TeamScreenController canlı akışa abone.
- P4-P5: bekliyor

Ek (2026-07-19): `GeneralTophud.png` yalnız Team + Market üst bandına uygulanır —
menü: TinyFixers > Mockup > Ekle - Genel TopHud (Team + Market). Leaderboard bilerek
dışarıda (kullanıcı elle tamamladı).

Kullanıcıya düşen console/SDK adımları her fazda açıkça listelenecek (Firebase console
provider açma, Facebook app id, App Store Connect ürünleri, rules deploy).
