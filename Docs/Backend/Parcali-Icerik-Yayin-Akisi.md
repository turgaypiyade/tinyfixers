# Parçalı İçerik — Yayın Akışı (Mac/iOS ve Windows/Android)

Son güncelleme: 28 Kasım 2026.

## Temel kural

**Repo her zaman YEREL içerik modunda commit edilir.** Uzak (etkin) mod yalnız build anında, o makinede açılır:

- Etkin modda katalog, git'e girmeyen üretilmiş kopyalara (`RemoteContent/GeneratedLevelPacks/`) ve
  TEK platformun manifestine (`Resources/ProgressiveContentManifest.json`) bağlanır.
- Bu haliyle commit edilirse diğer makinede ilk 50 level referansı boş kalır ve iki platform birbirinin
  manifestini ezer.
- Korumalar:
  1. "Tüm akış" butonu player build bittiğinde (başarılı ya da değil) otomatik olarak yerel moda döner.
  2. Git `pre-commit` kontrolü, manifest dosyası varken commit'i engeller (`Tools/git-hooks/pre-commit`).
  3. Unity build koruması, yanlış platform/sürüm manifestiyle player build almayı engeller.

## Her makinede bir kez

1. Git kontrolünü aç: `git config core.hooksPath Tools/git-hooks`
2. Cloudflare kimliği: ev klasöründe `.wondi-cloudflare.env`
   (Mac: `~/.wondi-cloudflare.env`, Windows: `%USERPROFILE%\.wondi-cloudflare.env`):
   ```
   CLOUDFLARE_API_TOKEN=...
   CLOUDFLARE_ACCOUNT_ID=...
   ```
   Bu dosya ASLA repoya, oyuna ya da yayın içeriğine girmez.
3. Python 3 (Windows'ta `py` komutu) ve Node.js (`npx`, wrangler için) kurulu olmalı.
4. Unity sürümü birebir aynı: **6000.3.9f1** (paketler Unity sürümüne bağlı).

## Her sürümde (platform başına)

1. `git pull` → Unity'yi aç.
2. Platformu seç (File ▸ Build Profiles): Mac'te **iOS**, Windows'ta **Android**.
   Android için "Build App Bundle (Google Play)" işaretli olsun.
3. Sürüm numarasını ayarla (Player Settings ▸ Version, Android'de Bundle Version Code).
   **Manifest sürüme birebir bağlıdır; sürüm değişince bu akış yeniden çalıştırılmalıdır.**
4. Açık sahneleri kaydet.
5. `TinyFixers ▸ Yayın ▸ Parçalı İçerik (R2)` ▸ **"Tüm akış: paketle → R2 → doğrula → etkinleştir → player build"**.
   - Paketler `Exports/ProgressiveContent/<Platform>/` altında üretilir, R2'de
     `content.wondigames.com/<Platform>/` altına yüklenir (iOS / Android; büyük-küçük harf önemli).
   - Değişmeyen paketlerin dosya adı (sha256) aynı kalır, yeniden yüklenmez.
   - Player çıktısı: `Exports/PlayerBuild/` (iOS = Xcode projesi, Android = .aab).
   - Bitince durum alanında "Repo yerel moda döndü" yazmalı. Yazmıyorsa "Yerel içerik moduna geri dön"e bas.
6. Çıktıyı **kapalı test** kanalına yükle (TestFlight / Play İç test). Doğrudan üretime çıkma.

## Cihaz test listesi (her platformda)

Editör uzak yolu hiç çalıştırmaz (her zaman yerel dosyaları kullanır); bu testler yalnız cihazda yapılabilir.

- [ ] Temiz kurulum → Level 1-50 internetsiz oynanıyor.
- [ ] Level 50 → 51 geçişi: paket iner, seviye açılır (ilerleme göstergesi görünür).
- [ ] Uçak modunda 51'e girmeye çalış → "Tekrar dene" çıkıyor, internet açılınca devam ediyor.
- [ ] İndirme sırasında uygulamayı arka plana al ve geri dön → indirme kaldığı yerden/yeniden tamamlanıyor.
- [ ] Bir event'i (ör. Bostan Hasadı) ve bir harika şehrini ilk kez aç → içerik iniyor, görseller doğru.
- [ ] Yeni sandık/renkli sandık görselleri (L178), bidon sızıntısı (L116) uzak paketten doğru geliyor.
- [ ] Yeniden kurulum + bulut kaydı (ör. L120'de) → yalnız gereken paket iniyor, aradakiler inmiyor.
- [ ] Kurulum boyutunu ölç (önceki ölçüm 507 MB).

## Doğrulama notları (28 Kasım, iOS paketleri)

- R2'deki 43 paketin 43'ü doğru boyutta erişilebilir.
- 251 seviyenin her birinin kullandığı engel türleri, kendi 50'lik aralığının filtreli kütüphanesinde
  kaynakla aynı görsellerle mevcut (tüm aşamalar, ara görseller, sesler, hedef ikonu dahil).
- İlk 50 seviyenin 23 engel türü yerel kütüphanede eksiksiz.
- Paketlenmiş hiçbir dosya paket build'inden sonra değişmemiş (27 Kas 19:24).
- Bilinen, pakete bağlı OLMAYAN eski sorun: bazı engel tanımlarında (id 27-32, 77) silinmiş dosyalara
  işaret eden 2 referans var (vuruş parçacığı / dolap eşyası listesi). Canlı sürümde de aynı; görsel boş kalır.

## Cloudflare önerileri

- `content.wondigames.com` için bot korumasını atlayan bir WAF kuralı ekle (varsayılan Python istemcisi 403
  aldı; Unity istemcisi geçti — bazı ağlarda sorun çıkmasın).
- Dosyalar şu an kenar önbelleğinden değil R2'den geliyor (`cf-cache-status: DYNAMIC`); dosyalar değişmez
  (sha256 adlı) olduğu için "Cache Everything" kuralı güvenle uygulanabilir.
