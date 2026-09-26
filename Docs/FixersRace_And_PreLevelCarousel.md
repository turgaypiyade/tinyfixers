# Fixers Yarışı + Level Öncesi Tanıtım Şeridi — Tasarım Taslağı

2026-09-26. Kod YOK, yalnız tasarım. Kullanıcı görselleri çizecek; kararlar §3'teki sorulara göre kilitlenecek.

---

## A. Level öncesi tanıtım şeridi (carousel)

**Ne:** Level öncesi popup'ın (PreLevelSpecialPopup) panelinin ALTINDA, kendiliğinden kayan küçük kartlar
(~4 sn'de bir, parmakla da kaydırılır, altta nokta göstergesi). Diğer oyunlardaki "Safari başladı, katıl!"
şeridi.

**Kural:** Oyna butonundan dikkat çalmaz — kısa (≈180 px), panelin altında; gösterilecek bir şey yoksa
hiç görünmez. Karta dokununca ilgili ekran açılır, kapanınca level öncesi popup'a geri dönülür.

**Kart kaynakları** (az event olsa da şerit hep 2–4 kartla dolu olur):

| Kart | Ne zaman | Dokununca |
|---|---|---|
| Safari / Yükseliş | etkin + katılmadıysan "Katıl!", katıldıysan "Kat 3/7 — ilk denemede geç!" | Safari haritası |
| Fixers Yarışı | etkin + katılmadıysan "Katıl!", katıldıysan "Sen 4. sıradasın — 9/15 taş" | Yarış ekranı |
| Günlük çark | bedava çevirme hazırsa "Çark seni bekliyor!" | Çark |
| Süreli jokerler | aktif süreli joker/sonsuz can varsa "Jokerlerin bedava: 2s 14dk" | — (bilgi) |
| Market fırsatı | Gezginci Çantası hiç alınmadıysa "Başlangıç paketi" | Market |
| Boss yaklaşıyor | sonraki boss 1–2 level uzaktaysa "2 level sonra düello!" | — (bilgi) |

**Mimari:** `LevelLossRegistry` ile aynı desen — her özellik kendi kart sağlayıcısını kaydeder, şerit
hiçbir event'i tanımaz (yeni event = yeni sağlayıcı, şerit koduna dokunulmaz). Kart görünümü tek prefab
(çerçeve + ikon + başlık + alt yazı), içerik sağlayıcıdan gelir. Mevcut popup giriş animasyonu ve
RewardTextStyle yeniden kullanılır.

**Çizilmesi gerekenler:** 1 kart çerçevesi (≈860×170, 9-slice uygun), nokta göstergesi (aktif/pasif).
İkonlar mevcut (Safari logosu, çark, GoldSheets, joker ikonları) — yalnız Yarış ikonu yeni.

---

## B. Fixers Yarışı

**Özet:** 10 tamirci (sen + 9 rakip) aynı anda başlar. Herkesin önünde kendi yolu var; her **level
kazanınca** yoluna **1 kaldırım taşı** döşenir. **15 taşı ilk bitiren** 1.'lik ödülünü, sonra bitirenler
2. ve 3.'lük ödüllerini alır. Safari'den farkı: kaybetmek geri götürmez — yarış hızla ilgili, ceza yok.

### Akış
1. **Başlangıç:** Etkinlik açılınca "Yarışa katıl" popup'ı (Safari katılım popup'ı gibi). Katılınca o an
   "katıl" diyen oyuncular arasından 9 rakip seçilir (şimdilik bot; backend gelince gerçek oyuncu —
   Safari'deki hibrit bot kuralıyla aynı).
2. **Oynarken:** Normal leveller — ayrı level yok. Ana menüye dönünce kazandıysan taş uçup yoluna
   oturur (animasyon), rakiplerin ilerlemesi de güncellenir.
3. **Bitiş:** İlk 3 yer dolunca ya da süre (öneri 24 sa) dolunca yarış kapanır; ödüller kazanma
   ekranıyla verilir (Safari kazanma ekranı deseni: kurdele + RewardTextStyle yazılar).

### Ekran
- 10 yol yan yana, **aşağıdan yukarı** uzanır; en üstte bitiş kemeri/bayrağı.
- Her yolun başında oyuncunun avatarı (mevcut `PlayerAvatarProvider`), avatar döşediği son taşın
  üstünde durur → kim öndeyse yukarıda. Sen hep vurgulu (çerçeve/parıltı), lider tacı.
- Taş döşeme anı: taş yukarıdan düşüp "tık" sesiyle oturur, küçük toz efekti.
- Üstte: kalan süre + "Sen 4. sıradasın".

### Rakiplerin hızı (bot simülasyonu)
- Her rakibin gizli bir "hızı" var (saatte kaç level kazandığı); koşu seed'inden belirlenir →
  Safari'deki gibi koşu boyunca tutarlı, her yarışta farklı (`SafariCrowdSimulation` deseni).
- Rakip taşları = hız × geçen süre (+ küçük rastgelelik), 15'te durur → kayıt gerekmez, her an
  hesaplanabilir; oyuncu çevrimdışıyken de rakipler ilerler.
- Hafif **denge**: hızların çoğu, normal oynayan bir oyuncunun yarışabileceği aralıkta; 1–2 hızlı
  rakip hep gerilim yaratır. Oyuncu çok gerideyse en hızlılar biraz yavaşlar (fark edilmeyecek kadar).

### Ödüller (öneri — senin kararın)
| Sıra | Ödül |
|---|---|
| 1. | 1000 altın + her booster'dan 1 |
| 2. | 500 altın + 1 süreli joker (1 saat) |
| 3. | 250 altın |

### Takvim
Safari Pzt/Çrş/Cts/Paz açık → Yarış **Salı/Perşembe/Cuma**. Level kapısı öneri ≥ 30 (Safari ≥ 50).

### Yeniden kullanılacaklar
`SafariParticipantPool` (isim+avatar+level'a yakın botlar), `SafariState` deseni (cycle, katılım,
seed), `SafariSchedule` (çoklu gün), level kazanma tespiti (snapshot), `SafariRewardView` deseni,
`RewardTextStyle`, `PopupEntranceAnimator`, ana menü event butonu (`SafariEventButton` deseni).

### Çizilmesi gerekenler
- Yol zemini (dikey, tekrar edebilen) + yol kenarı
- Kaldırım taşı (2–3 varyasyon; boş yuva ve döşenmiş hali)
- Bitiş kemeri / bayrak
- 1–2–3 kupa/rozet ikonları
- Ana menü etkinlik ikonu + katılım popup'ı görseli
- (Şerit kartı için) yarış ikonu

---

## 3. Senin karar vermen gerekenler

1. **Taş kuralı:** Her kazanç 1 taş mı, yoksa ilk denemede kazanç 2 taş mı (ustalığı ödüllendirir)?
2. **Süre:** 24 saat mi, yoksa ilk 3 bitirene kadar süresiz mi?
3. **Hedef:** 15 taş ve 10 kişi kalsın mı?
4. **Ödüller:** Yukarıdaki tablo uygun mu?
5. **Şerit:** Bilgi kartları (süreli joker, boss yaklaşıyor) da olsun mu, yoksa yalnız tıklanıp bir
   yere götüren kartlar mı?
