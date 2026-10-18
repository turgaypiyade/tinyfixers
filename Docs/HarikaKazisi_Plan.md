# Bostan Hasadı (kazı event'i) — Event Tasarım Taslağı

2026-10-10. Kod YOK, yalnız tasarım.

**KİLİTLENEN KARARLAR (2026-10-10, güncel — tema SON hali):**
- **Tema: Bostan Hasadı** — topraktan sebze/meyve çıkar ("daha renkli ve keyifli", kullanıcı). Hikâye bağı:
  tamirci hayvanlar harikayı onarırken yanına bostan kurmuş, hasat ekibin ödülü. Aşağıdaki "harika parçası"
  ifadeleri "ürün" olarak okunmalı; mekanik aynı. (Arkeoloji ve alet temaları denendi, bırakıldı.)
- Koleksiyon: hasat tezgâhı (kasalar + sepet). İsteğe bağlı nadir "altın ürün" booster/joker verebilir.
- **Arka plan:** oyuncunun en son tamamladığı harikanın `backgroundSprite`'ı (yoksa aktif harika) — chapter
  değişince kendiliğinden değişir, yeni çizim yok. Büyük sandık o harikanın sandık görselleri.
- Kat ödülleri her chapter'da aynı (tek config). Event parası: bahçe küreği.
- Kürek level kazanınca (1, ilk deneme 2, boss 3) · kazıcı **Ayı** (hasır şapka) · **25. seviye**, **5 gün** · sabit yerleşim · ipucu ışıltısı açık.
- Görsel prompt'ları: `Docs/HarikaKazisi_GorselPromptlari.md`.

---

## 1. Özet

Gezgin tamirci hayvanlar, harikaların toprağa gömülü parçalarını kazıp çıkarıyor.
Oyuncu **level kazandıkça kürek** kazanır; ana menüdeki **kazı alanında** her kürekle bir toprak
karesini kazar. Altta gizli **harika parçaları** vardır; bir parçanın tüm kareleri açılınca parça
toplanır. Kattaki bütün parçalar bulununca **kat ödülü** alınır ve bir alt kata inilir. Son katta
harika tamamlanır ve **büyük sandık** açılır.

Neden bu oyunda iyi çalışır:
- Her kürek küçük bir sürpriz ("altında ne var?") → level oynamak için ek sebep.
- Oyunun hikâyesine oturur (harika tamiri); mevcut harika açılış sistemi sonda yeniden kullanılır.
- Kurallar tek cümle; asıl match-3 oyunuyla yarışmaz (kazı ekranında oturum kısa: birkaç dokunuş).

---

## 2. Temel döngü

1. Level kazan → kürek kazan (§3).
2. Ana menüye dönüşte kürekler event ikonuna uçar (progress event'teki uçuş deseni).
3. İkona dokun → kazı ekranı → küreği olan kare(ler)i kazar.
4. Parça tamamlanınca parlayarak üstteki "harika silüeti"ne uçar ve yerine oturur.
5. Katın tüm parçaları bulununca kat sandığı → bir alt kata inme animasyonu.
6. Son kat → harika tamamlanır → büyük sandık + harika açılış efekti.

---

## 3. Kürek kazanma

| Durum | Kürek |
|---|---|
| Level kazanıldı | 1 |
| İlk denemede kazanıldı | +1 (toplam 2) |
| Boss düellosu kazanıldı | 3 **[KARAR]** |

- Kürek **level kazanılınca** verilir; level bırakılırsa verilmez.
- **Kayıp kuralı (proje kuralı):** fail ekranındaki kahraman balonunda görünür →
  "Vazgeçersen Altın ve **2 Kürek** elinden gidecek!" (`LevelLossRegistry` provider'ı; UI'a dokunulmaz).
- **[KARAR] Alternatif — board'dan kürek toplamak:** Kürekler level içinde taş gibi düşer, eşleşince
  toplanır (progress event'teki staging altyapısı hazır). Daha heyecanlı ama level'ın dengesine karışır;
  ilk sürüm için önerim "kazanınca kürek" (basit, dengeyi bozmaz).

---

## 4. Kazı alanı kuralları

- Her katta bir ızgara; her kare **1 kürek** ile kazılır.
- Parçalar farklı boyutlarda: 1x1, 1x2, 2x1, 2x2, 1x3 (dönebilir).
- Parçanın **tüm kareleri** açılınca parça toplanır (yarım açık parça kenarı görünür → merak).
- Kattaki bütün parçalar bulununca kat biter; **açılmamış kareleri kazmak gerekmez.**
- Yerleşim **sabit tasarlanmış** (her oyuncu için aynı; rastgele değil) → ekonomi öngörülebilir,
  kötü şans riski yok. **[KARAR]** İstenirse kat başına 2–3 hazır düzen arasından rastgele seçim.

### Önerilen katlar (ilk taslak — simülasyonla netleşir)

| Kat | Izgara | Parça kareleri | Ortalama kürek* | Kat ödülü |
|---|---|---|---|---|
| 1 | 4x4 (16) | 6 (3 parça) | ~11 | 100 altın |
| 2 | 4x5 (20) | 8 (3 parça) | ~15 | 1 joker + 150 altın |
| 3 | 5x5 (25) | 10 (4 parça) | ~20 | 2 joker + 30 dk sonsuz can |
| 4 | 5x6 (30) | 12 (4 parça) | ~25 | Büyük sandık (altın + 3 joker + çerçeve) |

\* Rastgele kazan oyuncu için; parça kenarlarını takip eden oyuncu daha az harcar.
Toplam ~70 kürek. **[KARAR]** Event süresi 5 gün → günde ~7 galibiyet + ilk deneme bonusları ile
aktif oyuncu bitirir, ara sıra oynayan 2–3 kat görür. Simülasyon motoruyla (galibiyet/gün dağılımı)
ayarlanacak.

### Oyuncuyu yormamak için
- **İpucu ışıltısı:** Bir parçanın bir karesi açıldıysa komşu karelerde hafif ışıltı (parçayı takip
  ettirir, rastgele kazmayı azaltır). **[KARAR]**
- **Toplu kazı:** Elinde çok kürek varsa "Hepsini kullan" düğmesi yok — her dokunuş tek kare (keyif
  dokunuşta). Ama kazma animasyonu kısa (≈0.25 sn) tutulur.

---

## 5. Takvim ve giriş kapısı

- Açılış seviyesi: **[KARAR]** 25. seviye bitince (Bridge 31, Safari 50 → çakışmasın diye erken event).
- Süre: **[KARAR]** 5 gün; bitince 1–2 gün ara, sonra yeni harika ile yeni sezon.
- Takvim deseni Bridge Repair'deki gibi (hafta/pencere anahtarı + tüm cihazlarda aynı).
- Süre bitince bulunamayan parçalar kaybolur; o ana kadar alınan kat ödülleri kalır.
- Katılım: ilk açılışta tanıtım popup'ı + "Nasıl oynanır" (3 kart). Level öncesi tanıtım şeridine
  (`PreLevelEventPromoRegistry`) "Kazı başladı — katıl!" kartı.

---

## 6. Ekran akışı

1. **Sol event paneli ikonu:** kürek sayısı rozeti + kalan süre.
2. **Kazı ekranı:**
   - Üstte: harika silüeti (bulunan parçalar renkli oturur) + kat göstergesi (Kat 2/4).
   - Ortada: toprak ızgarası.
   - Altta: kürek sayacı + karakter (kazı yapan hayvan) + kat ödülü önizlemesi.
   - Kürek yoksa: "Level kazan, kürek kazan!" + Oyna düğmesi (doğrudan sıradaki level).
3. **Kazma anı:** dokun → kürek iner → toz + toprak parçaları → kare açılır (boş toprak / parça kenarı).
4. **Parça tamamlanınca:** parça yerinden kalkar, parlar, silüetteki yerine uçar ("tık" sesi).
5. **Kat bitince:** sandık açılır (mevcut `RewardChestRevealOverlay`), zemin çöker → alt kat.
6. **Son kat:** harika tamamlanır → harika açılış efekti (dissolve) → büyük sandık.

---

## 7. Teknik bağlantı noktaları (mevcut sistemler)

- **Durum:** `DigEventState` (PlayerPrefs, anahtarlar `CloudSaveManifest`'e) — Bridge/Safari deseni.
- **Kürek verme:** `PlayerStats.OnLevelCleared` dinlenir (Bridge'deki gibi anında, menü dönüşünü beklemez).
- **Fail balonu:** `LevelLossRegistry.Register("dig_event", …)` → bu level kazanılırsa verilecek kürek.
- **Tanıtım şeridi:** `PreLevelEventPromoRegistry.Register("dig_event", …)`.
- **Ödüller:** `DailySlotRewardService.Grant` + `RewardChestRevealOverlay`.
- **Analytics:** `dig_join`, `dig_tile` (kat, kalan kürek), `dig_floor_complete` (kat, harcanan kürek,
  süre), `dig_event_end` (ulaşılan kat). Ekonomiyi canlı veriyle ayarlamak için.
- **Config:** `DigEventConfig` (ScriptableObject, Resources/Events): katlar, ızgara, parça yerleşimi,
  ödüller, takvim, kürek oranları — kodsuz ayar.
- **Performans:** Kazı ekranı UI'dır; ızgara en fazla 30 kare → havuz gerekmez, toz efekti mevcut
  FX havuzlarından.

---

## 8. Çizilmesi gerekenler

| # | Görsel | Not |
|---|---|---|
| 1 | Kazı ekranı arka planı | Kazı alanı: ahşap iskele, çadır, harika kalıntısı ufukta |
| 2 | Toprak karesi (2–3 varyant) | Kazılmamış; üstte çim/taş kırıntısı |
| 3 | Kazılmış kare | Koyu çukur, parça yoksa boş |
| 4 | Harika parçaları | Seçilen harikanın 3–4 parçası (sütun, kubbe, heykel başı…) |
| 5 | Harika silüeti | Parçaların oturduğu gölge çizim |
| 6 | Kürek ikonu | Event parası; ikon + küçük rozet hali |
| 7 | Kazı yapan karakter | **Hayvan** (robot DEĞİL) — ör. ayı veya maymun, baret + kürek |
| 8 | Event logosu / sol panel ikonu | Kürek + harika parçası |
| 9 | Kat sandığı / büyük sandık | Mevcut sandıklar kullanılabilir |

Görsel prompt'ları (İngilizce, mevcut doküman stilinde) karar netleşince
`Docs/HarikaKazisi_GorselPromptlari.md`'ye yazılacak.

---

## 9. Karar listesi (kullanıcı)

1. Kürek kaynağı: **kazanınca** (önerilen) mi, board'dan **toplanan** mı?
2. Boss galibiyeti kaç kürek? (öneri 3)
3. Parça yerleşimi sabit mi, kat başına 2–3 düzenden rastgele mi?
4. İpucu ışıltısı olsun mu? (öneri: evet)
5. Açılış seviyesi (öneri 25) ve süre (öneri 5 gün).
6. Hangi harika ile başlıyoruz, kazı yapan karakter hangi hayvan?
7. Kat ödülleri tablosu uygun mu?
