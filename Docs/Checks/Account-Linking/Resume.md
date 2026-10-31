# Hesap bağlama — güncel devam kaydı

Son durum: 2026-10-29. **Kod ve servis yapılandırması hazırlandı; yeni cihaz build’i, gerçek giriş ve cihazlar arası kayıt testi henüz yapılmadı. Tümü canlı çalışıyor sayma.**

## 2026-10-29 — deploy sonrası açılışta kapanma: Facebook framework embedding

- Xcode15:27 launch session kaydı: UnityFramework `dlopen` sırasında
  `Library not loaded: @rpath/FBAEMKit.framework/FBAEMKit`. Unity başlamadan kapanıyor.
  Uygulama paketindeki Frameworks dizininde yalnız UnityFramework vardı; native Mach-O,
  altı Facebook framework'üne dinamik bağlantı taşıyordu. Pods'taki FBAEMKit binary'si
  gerçek dynamic library; `use_frameworks! :linkage => :static` bunu statik yapmıyor.
- Yeni `IosFacebookFrameworkEmbedPostProcess`1006 sırasında altı SDK kütüphanesini
  ana uygulamanın Embed Frameworks fazına CodeSignOnCopy/RemoveHeadersOnCopy ile ekler.
  Kaynak `$(PODS_XCFRAMEWORKS_BUILD_DIR)/<SDK>/<SDK>.framework`: doğru cihaz/simülatör
  slice'ını CocoaPods seçer; raw XCFramework veya yanlış mimari kopyalanmaz.
- UnityFramework'teki mevcut SDK linkage'ı korunur; ana uygulamaya ikinci link eklenmez.
  Tekrar export'ta aynı altı embed girdisi yenilenir, birikmez. Unity embed API'sinin
  runpath değiştirmesine karşı config bazında inherited/custom yollar korunur; Swift
  runtime embedding ana uygulamada açılır. iOS Resolver main app Pod target'ı açık tutulur.
- `csi Tools/check_ios_facebook_embedding.csx`:580 kontrol; üretim Configure metodu
  kurulu Unity'nin gerçek managed PBX API'siyle sentetik projede çalıştı. Eksik Pods'ta
  erken hata/no-op,6 framework,imzalama,doğru source path,25 tekrarlı export,config'ler
  ve mevcut UnityFramework fazlarının korunması kapsandı. Üretilen fixture plutil lint geçti.
- Mevcut Xcode export'u, uygulama paketi ve cihaz bu revizyonda değiştirilmedi.
  Unity/native build veya yeni deploy çalıştırılmadı. Kullanıcının yeniden export/deploy
  alması ve telefonda açılışı doğrulaması gerekiyor; crash düzeldiği henüz cihazda doğrulanmadı.

## 2026-10-29 — iOS duplicate symbols için kalıcı bağımlılık düzeltmesi

- Kullanıcı mevcut Xcode export'unu düzeltmememizi, yalnız Unity kaynaklarını düzeltmemizi istedi.
  Bu revizyonda Xcode/Pods/DerivedData dosyaları değiştirilmedi, build/resolve/deploy yapılmadı.
- Hata kaydında GoogleUtilities, FBLPromises ve GTMSessionFetcher; Firebase'in SPM grafiği ile
  Google Sign-In'in CocoaPods grafiğinden aynı link'e geliyordu. Incremental export ayrıca
  Firebase'in dört ürününü27'şer kez biriktirmişti.
- Projeye ait `GoogleSignInDependencies.xml` artık10.1.0 GoogleSignIn SPM ürününü bildirir;
  `replacesPod="GoogleSignIn"` eşleşmesi sayesinde aynı Pod eklenmez. Firebase12.15.0 ile
  ortak Google bağımlılıkları tek SPM grafiğinde çözülür. SDK sürümü değiştirilmedi.
- Facebook'un bağımsız19.0 CocoaPods kurulumu ve Android'in üç mevcut Google bağımlılığı
  korundu. Apple/Google/Facebook/e-posta auth runtime'ı, public client config, URL şeması ve
  Apple capability/provisioning ayarları değiştirilmedi. Apple Developer/capability önkoşulu sürer.
- `IosAuthDependencyPostProcess` iOS build öncesi proje seviyesinde SPM'i açar; resolver'ın35
  sırasından sonra39'da aynı package requirement/product/build link'lerini tekilleştirir.
  Farklı sürümler, farklı embed ayarları ve diğer target'ın ihtiyaç duyduğu bağlantılar korunur.
 46'da GoogleSignIn Pod'u yeniden eklenmişse pod install'dan önce açık hata verir.
- `csi Tools/check_ios_auth_dependencies.csx`:92 gerçek normalizer kontrolü;1/2/27/50 export,
  idempotency, farklı requirement, hedefler arası ürün paylaşımı, embed ayarı ve referans listesi
  tekilleştirmesi geçti. Kurulu Unity Xcode + EDM4U API metadata binding geçti; assembly üretilmedi.
  Normalize edilen sentetik PBX fixture'ı `plutil -lint` geçti.
- Sonraki adım: kullanıcı Unity'den yeniden export alır; Facebook Pods bulunduğu için
  `.xcworkspace` açılır. Gerçek linker ve cihaz giriş testi bu revizyonda yapılmadı.

## Kapsam ve kullanıcı kısıtları

- Settings / Save Progress: mevcut anonim kaydı Google / Facebook / Apple kimliğine bağlama; çakışmada kullanıcı onayıyla diğer hesabın kaydına geçme.
- Kullanıcı tarayıcıyla kurulum için açık izin verdi. Son onayı: Play sertifikasını Firebase’e ve aynı Android/iOS uygulamasını Meta’ya bağlamak için “evet”.
- **Ücretli işlem yok.** Firebase Spark / $0. Workspace, Blaze, ücretli hizmet, üyelik veya ödeme açılmadı.
- Destek ve developer iletişim adresi yalnız **hello@wondigames.com**. Posta kişisel Gmail’e yönleniyor; Google hesabı mevcut adresle ücretsiz oluşturulmuş durumda.
- **Apple Developer onayı hâlâ bekliyor.** TF_APPLE_SIGNIN_CAPABILITY kapalı kalmalı; kişisel provisioning’i bozma.
- Play organizasyonu JULVERA TEKNOLOJİ ANONİM ŞİRKETİ, developer ID 6067741772505387104. Kullanıcının turgayp@gmail.com hesabında developer / sürüm çıkma yetkisi var. Organizasyon hesabı daha önce julveradigital@gmail.com olarak belirtilmişti.
- Özel anahtar / App Secret / admin credential oyun assetlerine, loglara veya GitHub’a girmemeli. Instagram’daki Wondi Social uygulaması ve ~/.wondi-instagram.env değiştirilmedi.
- Kullanıcı bu tur build istemedi. Unity build, dotnet build, native derleme veya deploy yapılmadı. Level varlıklarına dokunulmadı.

## Servislerde tamamlananlar

### Firebase / Google

- Proje tinyfixers-bc1cd. hello@wondigames.com Editor; eski kişisel Owner korunuyor. Firebase koşulları kullanıcı tarafından önceki tur kabul edildi.
- Google / Facebook / Apple / Anonymous sağlayıcıları Enabled. Public name Wonder Fixers, support hello@wondigames.com.
- Play Console’dan Wonder Fixers’ın **Closed testing** durumu ve paket **com.yigefep.tinyfixers** doğrulandı. App ID 4972775521056614645; herhangi bir release yayımlanmadı.
- Play App signing açık sertifikası indirildi: /Users/turgayp/Downloads/certificates.zip. İmzalama özel anahtarına erişilmedi, değiştirilmedi.
- Aktif deployment sertifikasının SHA-1 ve SHA-256 fingerprint’leri Firebase Android uygulamasına kaydedildi ve tabloda doğrulandı:
  - SHA-1: 7A:EC:70:AC:71:8B:13:1F:6D:A0:8F:37:A2:AB:6B:5E:80:AF:16:DC
  - SHA-256: DF:16:ED:DA:33:A7:EC:85:53:17:B7:24:2A:C6:C7:B4:45:19:49:71:41:09:BE:00:89:81:2B:8A:9E:5D:6A:8F
- Güncel Android google-services.json indirildi; paket, Play SHA-1, Android + Web OAuth client varlığı ve Web client’ın runtime config ile eşleşmesi doğrulandı. Yerel Assets/StreamingAssets/google-services.json güncellendi. Resmi Firebase Python dönüştürücüsüyle yerel google-services.xml güncellendi (proje build’i değil).
- Gerçek iOS GoogleService-Info.plist önceki tur indirilip Assets’e kopyalanmıştı; CLIENT_ID ve reverse scheme mevcut.
- Google Auth Platform Audience **External / In production** olarak zaten ayarlıydı; değiştirilmedi.
- Google Branding: Wonder Fixers ve hello@wondigames.com doğrulandı. Home / privacy / terms sayfaları www.wondigames.com altına ve authorized domain wondigames.com olarak kaydedildi. Developer contact hello adresi yapıldı.
- Otomatik Google marka doğrulaması başarılı oldu; **Publish branding** yapıldı. Son açık UI: “Your branding has been verified and is being shown to users.” Logo yüklenmedi.

### Meta

- Ayrı oyun uygulaması Wonder Fixers, App ID **1494776035829187**, Wondi Games business **1474945814501093**. Eski Instagram uygulaması Wondi Social (1113770644469714) korunuyor.
- Meta App Secret önceki tur kullanıcı onayıyla yalnız Firebase Facebook sağlayıcısına aktarıldı; yerel dosyaya yazılmadı. Tekrar ortaya çıkarmak gerekmiyor.
- Valid OAuth redirect URI kaydedildi: https://tinyfixers-bc1cd.firebaseapp.com/__/auth/handler
- Basic alanları: wondigames.com; https://www.wondigames.com/privacy/ ; /terms/ ; /delete-account/ ; Games / Match 3.
- iOS Bundle ID **com.yigefep.tinyfixers** eklendi; Store ID henüz yok. Otomatik in-app event logging kapalı.
- Android Google Play platformu: paket **com.yigefep.tinyfixers**, Class name **com.unity3d.player.UnityPlayerGameActivity** (PlayerSettings androidApplicationEntry=2 ile uyumlu).
- Play deployment sertifikası Meta key hash: **euxwrHGLEx9toI83oqtrXoCvFtw=**.
- Android otomatik purchase/subscription logging kapalı. Site URL **https://www.wondigames.com/** eklenerek domain validation hatası giderildi. Reload sonrası tüm mobil alanlar ve logging kapalı değerleri doğrulandı.
- Meta kapalı test paketini public Store’dan doğrulayamama uyarısı gösterdi; platform alanları yine de kaydedildi ve reload’da korundu.
- **Unpublished. 1024×1024 gerçek oyun ikonu, business verification / App Review ve gerçek test talimatları hâlâ gerekiyor.** Kullanıcıya ikonun dosya yolu soruldu; henüz yanıt yok. Örnek ikon / yeni uydurma görsel kullanma. Şimdilik Meta app-role/test kullanıcılarıyla cihaz testi yapılabilir; tüm oyunculara hazır deneme.
- Advanced “Native or desktop app?” switch’i değiştirilmedi. Başka izin, yeni kişi veya API access grant eklenmedi.

## Yerel uygulama değişiklikleri

- Resmi Meta Unity SDK **19.0.0**, Assets/FacebookSDK altında. Tools/install_facebook_sdk.py ve facebook-sdk-install.json kaynak/digest kaydı içeriyor. Gereksiz native modüller körlemesine çıkarılmadı.
- SocialSignInConfig + AuthNonce; Google iOS resmi GIDSignIn ve Android Credential Manager köprüleri; Facebook Android normal ve iOS Limited Login (hash nonce / raw nonce Firebase) köprüsü eklendi.
- Google bağımlılıkları: GoogleSignIn iOS 10.1.0, AndroidX credentials + credentials-play-services-auth 1.5.0, googleid 1.1.1. Native dependency resolve ve cihaz derlemesi henüz yapılmadı.
- Google managed host yalnız giriş düğmesine basılınca oluşturulur, Update döngüsü yok. Facebook managed init yalnız giriş istendiğinde; analytics/advertiser toplama kapalı. Apple plugin Update yalnız bekleyen giriş süresince açık.
- iOS Google köprüsünün Unity extern fonksiyonları C linkage kullanır; URL geri dönüş listener’ı ekli. Native kod henüz derlenmedi.
- SocialSignInBuildSetup: yerel public client config’ten Meta settings yazar; Android SDK manifest generator/check kullanır, iOS Google reverse URL scheme ve Meta telemetry flags ayarlar. Config yalnız dört izinli public alan içerebilir; ekstra alan varsa build’i durdurur. Hook API binding doğrulandı; hook çalıştırılmadı.
- Apple bridge nonce / iptal / hata / lifecycle düzeltildi; capability hâlâ kapalı.
- EditorAuthLinkDebug yalnız UNITY_EDITOR && TF_AUTH_LINK_DEBUG ile opt-in. Normal editor’de sahte Google Email/Password girişi yok.
- SaveProgressPopup her açılışta/availability/OnReady/restore’da güncellenir; linked/unavailable ayrımı, busy/duplicate tap/iptal, idempotent event aboneliği ve kapanış temizliği var. TR/EN metinler eklendi.
- AuthLinkService: UID’ye bağlı tek işlem, typed collision errors, Apple UpdatedCredential, kaynak kaydının server ack’i, yerel recovery snapshot, yeni kimlikte exact restore tamamlanınca menü yenileme. Farklı UID’ler arası ilerleme / cüzdan / arkadaş verisi birleştirilmez.
- LocalAccountData: local owner + pending switch marker, device-local account-recovery snapshot. Manifest account verisini temizleyip hedef kaydı uygular; ayarlar korunur, IAP işlem receipt’leri cihaz genelinde union edilerek tekrar teslim engellenir.
- Cloud save eski UID/version callback’lerini reddeder; farklı UID restore’u Source.Server ile alır, başarısızsa kapılar kapalı kalır ve retry yapar. BootLoader pending switch sırasında timeout ile kaynak verisini yeni UID’ye açmaz.
- CurrencyLedger, FriendState, LivesManager, Team cache, telemetry owner/stream resetleri var. Stats reporter restore kapısı ve UID/stream ack guard kullanır; hesap değişiminde birikmiş eski oturum süresi temizlenir. Hesap silme, login işlemi sürerken başlatılmaz.
- Performans telefon üzerinde ölçülmedi. Giriş düğmesi dışında yeni gameplay taraması / VFX / board update / ses kaynağı eklenmedi.

## Anahtarlar / Git

- Assets/_Project/Resources/AuthProviderConfig.local.json yerel ve ignored, chmod0600. Yalnız Google iOS/Web client ID, Meta App ID ve **public SDK Client Token** içerir. Client Token, özel App Secret değildir; native SDK tarafından uygulamayla dağıtılır.
- Client Token UI’dan aktarılırken kullanılan .utmp/social-login.env.txt artık silindi; geçici CUA token değişkenleri temizlendi. Özel Meta App Secret assetlerde yok.
- .gitignore env, signing material, service account/admin dosyaları, local config, FacebookSettings.asset ve üretilen AndroidManifest.xml/meta’yı kapsar.
- Firebase’in dört client config dosyası **yalnız Git takibinden çıkarıldı**; yerel dosyalar korundu. Silmeler staged: GoogleService-Info.plist, google-services.json, google-services-desktop.json, google-services.xml.
- Commit/push yapılmadı. Eski Git geçmişi temizlenmedi; eski public Firebase API client key’leri geçmişte hâlâ olabilir. Admin secret olduğu iddia edilmemeli.
- Güncel nonignored kaynak dosyalarında gerçek SDK client-token / Firebase API-key değerleri için tarama eşleşme bulmadı. Runtime config alan allowlist’i ve Google Web ID eşleşmesi doğrulandı.
- Kullanıcının diğer çok sayıdaki değişikliğini ve staged FingerTutorial taşımasını koru; reset/revert yapma.

## Doğrulama

- csi Tools/check_account_linking.csx: **25 üretim dosyası** installed Unity/Firebase/Meta API metadata binding PASS (default/iOS/Android ve Apple define senaryosu). Android+iOS Editor hook binding PASS. Assembly üretilmedi.
- csi Tools/test_account_linking.csx: **32** üretim-metodu account linking/backup/restore regression PASS. SDK transport stubs; gerçek hesap veya cloud write yok. Fixture source recovery temp dizinini kaldırır.
- csi Tools/check_social_profile_sync.csx: metadata PASS ve **35** production-method social sync regression PASS. SDK stub; remote değişiklik yok.
- İlgili değişen dosyalarda git diff --check PASS.
- **Unity/native derleme, telefon giriş/restore, gerçek performans testi yapılmadı.** Mevcut Play binary bu yeni kodları içermiyor; kullanıcı build istediğinde yeni kapalı test sürümü gerekir.

## Sonraki somut işler

1. Kullanıcının ikon dosya yolu yanıtını al; Meta için gerçek 1024×1024 ikonu kullan. Gerekirse dosyayı önce görsel olarak incele; yeni ikon icat etme.
2. Kullanıcı yeni build’i o tur açıkça istediğinde Android/iOS native dependency çözümleme ve cihaz build’ini gerçekleştir. İlk test Google link→UID korunması, iptal/yeniden giriş, başka cihaz restore; sonra farklı mevcut hesaba geçişte düşük ilerlemenin exact restore’u ve offline hata.
3. Play’den yüklenen sürümle test et. Debug/sideload imzası Play’den farklıdır; o kanalda test istenirse o gerçek sertifikayı ayrıca kaydet, imza anahtarı oluşturma/değiştirme.
4. Meta review/business verification şartlarını gerçek bilgiler ve gerçek cihaz test kaydıyla tamamla; onaylanmadan tüm oyunculara açık deneme.
5. Apple Developer onayı gelince kullanıcı bilgisiyle capability/provisioning ve Apple giriş cihaz testine geç. Yeni ücretli üyelik/ödeme yapma.

## Tarayıcıdan devam

Chrome browser ID 1, session 🔐 Oyun hesapları. Context özetiyle dönülürse önce cua.rewriteDocumentation(). UI indeksleri yeniden alınmalı.
- Meta 1527356183 Basic, unfinished app review / ikon için handoff işaretlendi; secret maskeli.
- Firebase 1527356195 /u/1 hello hesabı Android General; provider’lar Enabled, Spark $0.
- Play 1527356202, app keymanagement; salt okunur sertifika bilgisi alındı.
- Google Auth 1527356209 /auth/branding?authuser=0&project=tinyfixers-bc1cd; marka verified ve shown to users olarak doğrulandı.

Birincil teknik kaynaklar:
- https://developers.google.com/identity/sign-in/ios/start-integrating
- https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation
- https://firebase.google.com/docs/auth/ios/facebook-login
- https://firebase.google.com/docs/auth/unity/apple
- https://github.com/facebook/facebook-sdk-for-unity/releases/tag/sdk-version-19.0.0
- https://github.com/facebook/facebook-sdk-for-unity/blob/sdk-version-19.0.0/Facebook.Unity.Editor/android/ManifestMod.cs
