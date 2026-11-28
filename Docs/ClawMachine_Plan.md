# Pençe Makinesi (Claw Machine) — Tasarım Notu

Durum: TASLAK, kullanıcı onayı bekliyor (2026-11-26). Görseller gelmeden kodlama yok.
Sıra: parçalı indirme + sürüm işinden SONRA.

## 1. Özet

Lunapark oyuncak yakalama makinesi. Oyuncu seviye kazanarak **jeton** toplar; her jetonla pençeyi
sağa-sola kaydırıp bırakır. Pençe bir oyuncağı yakalarsa ödül verir. Makinenin içinde 6 **peluş
karakter** (Pemi, Brumo, Tinko, Fiko, Pilo, Bram) var; event boyunca 6'sını toplayan büyük sandığı açar.

Neden: (1) farklı bir mekanik (Hediye Şenliği = zamanlamalı top; bu = nişan + şans göstergesi),
(2) altın için **harcama yeri** (ek jeton altınla), (3) koleksiyon hedefi oyuncuyu event boyunca geri getirir.
Kumar DEĞİL: bahis yok, "kaybettiren" makine yok, kaybın sebebi görünür (nişan) ve şans gizli değil (gösterge).

## 2. Takvim ve açılış

| Ayar | Değer (öneri) |
|---|---|
| Açılış seviyesi | 70 (Köprü 60 ile Gear Cup 110 arası) |
| Süre | 4 gün açık, 3 gün ara (Hasat 5+2 ile çakışmayacak şekilde kaydırılır) |
| Ana menü | Sol/sağ event paneli, ortak şablon ikon + EventIconPlate (kalan süre) |
| Kilitliyken | EventLockOverlay ("SEVİYE 70") |

## 3. Jeton

| Kaynak | Miktar |
|---|---|
| Seviye kazanma | +1 jeton |
| Zor / çok zor seviye | +2 jeton |
| Event'e katılım hediyesi | +3 jeton |
| Altınla ek jeton | 300 altın, günde en çok 3 (altın harcama yeri) |
| Jeton tavanı | 10 (fazlası boşa gitmez: tavanda seviye kazancı jeton yazmaz, oyuncuya "dolu" gösterilir) |

Event bitince kalan jeton silinir (geri ödeme yok). Sonuç ekranında bu açıkça yazılır.

## 4. Oyun akışı (bir deneme)

1. Oyuncu pençeyi yatay sürükler. Bıraktığı konum iniş hedefini belirler.
2. Oyuncu sürüklemeyi bırakır → pençe o x konumunda iner.
3. **Sonuç dokunuşta belirlenir** (aşağıdaki kural), sonra animasyon gösterir. Kapatma/çökme ödülü
   çiftlemez, jetonu yakmaz (Hediye Şenliği GiftFestivalState kalıbı: önce kalıcı kayıt, sonra sunum).
4. Yakalandıysa: pençe oyuncağı kaldırır → çıkış haznesine bırakır → ödül uçar (cüzdan/joker/koleksiyon yuvası).
5. Kaçtıysa: pençe boş kapanır ya da oyuncak kenardan kayar; **Pençe Gücü** göstergesi dolar.

## 5. Kazanma kuralı (adil, görünür)

```
ilk sezon denemesi → küçük kapsül
büyük ödülden sonra beşinci deneme → büyük kapsül
diğer denemeler → nişana ve ayarlı ağırlıklara göre boş / kayma / küçük / büyük
```

İlk büyük ödül en geç sezonun beşinci denemesinde gelir. Küçük kapsül ve kayma büyük ödül sayacını sıfırlamaz. Büyük kapsülde sayaç yeniden başlar. Garanti denemesinde hedefe uygun kapsül pençenin erişimine yerleştirilir. Gösterge bir sonraki garantiye kalan denemeyi gösterir.

Jeton, sayaç, sonuç ve ödül kaydı bırakma anında birlikte saklanır. Sunum kaydedilmiş sonucu oynatır. Boş sonuçta pençe boş yükselir, kaymada kapsül yükselişte düşer, kazançta hazneye gider. Hareket süre ve konum eğrileriyle yönetilir.

## 6. Makinenin içi (ödül masası)

Makinede her an ~8 oyuncak görünür; yakalanan oyuncağın yerine yenisi düşer (ağırlıklı çekiliş).

| Oyuncak | Ödül | Ağırlık (öneri) |
|---|---|---|
| Küçük altın kesesi | 25 altın | 30 |
| Büyük altın kesesi | 60 altın | 10 |
| Booster kutusu | 1 booster (çekiç/satır/sütun/karıştır, açılmış olanlardan) | 20 |
| Joker kutusu | 1 joker (Line/PulseCore/Override) | 15 |
| Kalp | 30 dk sınırsız can | 8 |
| Peluş karakter | Koleksiyon yuvası | 17 (6 karaktere eşit bölünür) |

Kural: makine altın VERMEKTEN çok altın HARCATMALI → altın keseleri küçük, değerli ödüller joker/can/koleksiyon.

## 7. Koleksiyon (event içi, albüm yok)

- Makine ekranının üstünde 6 boş yuva; yakalanan peluş yuvasına oturur.
- 6'sı dolunca **büyük sandık**: 3 joker + 2 booster + 1 saat sınırsız can (+ 200 altın).
- Tekrar gelen peluş → +1 jeton (oyunu uzatır, altın şişirmez).
- Event bitince yuvalar sıfırlanır; sonraki event aynı 6 peluşla yeniden başlar. **Kalıcı albüm/sekme YOK**,
  sezonluk yeni görsel YOK (6 peluş bir kez çizilir). Çeşitlilik istenirse yalnız makine rengi/arka plan değişir.

## 8. Denge hedefi

**Monte Carlo (2026-11-28, 12-20 bin oyun):** ilk taslak (peluş ağırlığı 17, peluş da %60) → 22 jetonla tamamlama
yalnız ~%1 (peluş seyrek + çoğu tekrar). "Eksik peluş öncelikli" → %94-100 (fazla kolay). SEÇİLEN: makinede her an
en az 1 peluş (minPlushVisible), peluş kimliği rastgele, peluş tutma 0.40 / kapsül 0.60 → 15 jeton %8, 22 jeton **%37**,
30 jeton %68. Varsayım: oyuncu eksik peluşu hedefler, isabet 0.45-1.0 arası.

- Aktif oyuncu (günde ~5-6 seviye) bir event'te ~20-25 jeton kazanır.
- Hedef: aktif oyuncuların %30-40'ı koleksiyonu tamamlasın. Daha az → küser; daha fazla → değeri kalmaz.
- Tüm sayılar tek config'te (ClawMachineConfig). Yayından önce masaüstünde **Monte Carlo** (Python) ile
  tamamlama oranı ve ortalama ödül hesaplanır; sonra oyuncu verisiyle ayarlanır.

## 9. Ekranlar

1. **Giriş/katılım popup'ı** (ilk kez): makine görseli, "Seviye kazan → jeton → oyuncak yakala", BAŞLA.
2. **Makine ekranı** (EventScreenIris ile ikondan açılır): cam kutu, pençe, oyuncaklar, çıkış haznesi,
   üstte koleksiyon şeridi, altta jeton sayısı + "BIRAK" butonu + "+" (altınla jeton), Pençe Gücü göstergesi.
3. **Sonuç ekranı** (event bitişi): toplanan peluşlar, kalan jetonların silindiği bilgisi.
4. **İpucu/öğretici:** ilk deneme tek adım (parmak "BIRAK"ı gösterir).

## 10. Teknik notlar

- Fizik motoru YOK: pençe ve oyuncak hareketleri tween/koroutin (cihaz kasmasın, sonuç deterministik).
- Durum: ClawMachineState (JSON, CloudSaveManifest'e eklenir), jeton + yuvalar + gösterge + sezon kimliği.
- Ödül teslimi: DailySlotRewardService.Grant; altın kalemi checkpoint'li (Haftalık Yarış kalıbı).
- Ses: merkezi EventSfx (pençe motoru döngüsü, kapanma, yakalama, kayma); GameSettings.SoundEnabled'a uyar.
- Performans: oyuncak görselleri havuzlu, tek Update yalnız ekran açıkken, kapalıyken bileşen pasif.
- Analitik (GA4): claw_attempt (isabet, sonuç, gösterge), claw_token_buy, claw_collection_complete.
- Uzaktan ayar: config/clawMachine (Firestore) yalnız sezon tarihleri + ağırlıklar (Progress Event kalıbı).

## 11. Görsel listesi (ChatGPT, tek sayfada tutarlı üretim)

**Durum 2026-11-26:** makine, pençe (açık/kapalı), kapsül ve 6 peluş HAZIR → `Docs/ClawMachine/source/`
(Plush_{Pilo,Tinko,Pemi,Bram,Brumo,Fiko}.png 512², alt hizalı). Ödüller ayrı çizilmez: şeffaf kapsül
(alt yarı koddan renklenir) + içinde oyunun mevcut ikonu (TileIconLibrary). Kalan: ana menü ikonu, arka plan.
Deneme montajı: ~/Desktop/Wondi Paylaşım/pence_makinesi_deneme.png
Peluşlar SADE (kıyafetsiz, yalnız fular) seçildi: PlushSimple_{ad}.png. Kapsül 3 katman: arka (soluk tam kapsül,
alt yarı renkli) → obje (bantta oturur) → ön (üst cam %28 saydam + parlamalar tam, bant + alt yarı opak).

1. Makine gövdesi (önden, cam kutu boş, üst pençe rayı, altta hazne) — ana parça
2. Pençe (açık + kapalı, iki kare) + pençe kablosu (dikey, döşenebilir)
3. 6 peluş karakter (Characters/ referanslarıyla; KARAKTER GÖRSELİ REFERANS ŞART)
4. Ödül oyuncakları: küçük/büyük altın kesesi, booster kutusu, joker kutusu, kalp peluşu
5. Ana menü ikonu (event şablonu: altın halka + düz zemin + taşan pençe/peluş)
6. Arka plan (sade lunapark/atölye, 01_Game sade dili)

## 12. Açık sorular (kullanıcı)

- Açılış seviyesi 70 uygun mu?
- Ek jeton fiyatı 300 altın / günde 3 uygun mu?
- Büyük sandık içeriği?
- Event takvimi: Hasat ile dönüşümlü mü, aynı anda mı?
- Yığın rolü / engel değil (mini oyun) — tahtaya etkisi yok.

## 10. Beceri tabanlı 2.5D pençe (2026-11-28, kullanıcı kararı — eski §5 garantilerinin yerine)

Kullanıcı testi: "ilkinde yakalıyorum, sonra mutlaka kaçıyor" → sonuç beceriden değil sistemden geliyordu. Yeni kural:

- **Kontrol:** joystick; pençe sol-önden başlar, yalnız **sağa** (u) ve **geriye** (w, derinlik) gider — geri dönüş yok.
  Joystick ortasına dokunmak = **İNDİR** (durdurulamaz). Bırakınca pençe hızla orantılı sallanır; sallanırken
  indirmek isabeti kaydırır. Tabanda nişan gölgesi derinliği gösterir. Her denemeden sonra pençe başlangıca döner.
- **Süre:** sezonun ilk `untimedAttempts` (3) denemesi süresiz; sonra `moveSeconds` (12 sn), ilk hareketle başlar.
- **Oyuncaklar:** 3 derinlik sırası (w 0.12 / 0.5 / 0.88), sıralar yatayda kaydırılmış; arka küçük ve yukarıda.
- **Sonuç (ClawMachineRules.Resolve):** isabet = 1 − elips mesafe (`itemHalfWidth` yatay, `itemHalfDepth` derinlik).
  ≥ çekirdek (`coreAccuracy` 0.55, peluş `plushCoreAccuracy` 0.68) → kesin yakalar; `slipAccuracy` (0.25)..çekirdek →
  kenar: tutma şansı 0 → `edgeGrip` (0.35 / peluş 0.2), tutmazsa kayar; altı → boş.
- **Gizli garanti yok.** Pençe Gücü (3 kaçırış) = **güçlü pençe**: kenar da kesin tutar, nişan yine şart.
  Ömür boyu ilk deneme = öğretici: tolerans ×`tutorialTolerance` (1.8) + güçlü + yönlendirme yazısı.
  `bigGuaranteeEvery` yalnız kapsül **içeriğini** belirler (yakalamayı değil).
- **Python sınaması (40 bin deneme, nişan hatası σ):** usta (±2%) kapsül %92 / peluş %74; orta (±4%) %56 / %35;
  acemi (±7%) %27 / %15; güçlü pençe orta peluş %86. İnsan becerisi simüle edilemez → GA4 `claw_attempt`
  (accuracy_pct, row, strong, plush, attempt) ile ilk hafta ölçülüp config'ten ayarlanır.
