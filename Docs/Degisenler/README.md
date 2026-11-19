# Değişenler (görsel yedekleri)

Oyundaki bir görsel yenisiyle değiştirildiğinde ESKİ hâli buraya, tarih klasörüne yedeklenir
(kullanıcı kuralı 2026-11-11). Dosyanın oyundaki yolu ve GUID'i değişmez; yalnız içerik değişir.

| Tarih | Oyundaki dosya | Yedek | Not |
|---|---|---|---|
| 2026-11-11 | Assets/_Project/Art/UI/BackGroundImgs/loading.png | 2026-11-11/loading_eski.png | Aynı sahne, yeni parlak öğle ışığı ve temiz çizim (941×1672) |
| 2026-11-11 | Assets/_Project/Art/UI/Missions/ND_M001/1/MIS_DEFAULT.png | 2026-11-11/MIS_DEFAULT_eski.png | Tac Mahal → Özgürlük Heykeli; karakterler arkadan, turist gibi (856×1836 → alt 2 px zemin uzatıldı, 856×1838) |
| 2026-11-11 | (aynı dosya, revize) MIS_DEFAULT.png | 2026-11-11/MIS_DEFAULT_eski.png (Tac Mahal) | Kullanıcı karakterleri biraz sağa kaydırdı: Characters/NewDesign11102026/Ozgurluk.png |
| 2026-11-11 | Settings/ChapterThemeSet/ChapterTheme_1.asset (gameBackground, lightBoardCells) | Eski değer: gameBackground GBG10.png (guid bb7f6e6989e334e268ff2aa168a5897b), lightBoardCells 1 | Bölüm 1 oyun arka planı → GBG13_HarborPier (açık, reklamdaki gibi), hücreler koyu (lightBoardCells 0), tahta çerçevesi borderColorId 0 → 5 (bölüm 8 ile aynı eşleşme) |
| 2026-11-11 | (revize) ChapterTheme_1.asset gameBackground | Önceki: GBG13_HarborPier (geçici) | Bölüm 1 → yeni GBG16_MonumentValley.png (Amerika/çöl teması; sahne 150 px aşağı kaydırıldı, üst gökyüzü uzatıldı). Liman iskelesi bölüm 8'de kalır |
| 2026-11-11 | Settings/ChapterThemeSet/ChapterTheme_2.asset | Eski: gameBackground GBG8 (guid 501dea3808c7c499c91228de827c630c), borderColorId 6, lightBoardCells 1 | Bölüm 2 (level 26–50) → yeni GBG17_GoldenGate.png (sahne 110 px aşağı), borderColorId 5, koyu hücre |
| 2026-11-11 | Settings/ChapterThemeSet/ChapterTheme_3.asset | Eski: gameBackground GBG9 (guid 43f0f0b65cbf245388c331a4a655773a), borderColorId 5, lightBoardCells 1 | Bölüm 3 (level 51–75) → yeni GBG18_FixersCamp.png (ekibin tamir kampı, sahne 110 px aşağı), koyu hücre |
| 2026-11-11 | Settings/ChapterThemeSet/ChapterTheme_4.asset | Eski: gameBackground GBG2 (guid de628a5c919f1440faa83c2a6b3c42cf), borderColorId 0, lightBoardCells 1 | Bölüm 4 (level 76–100) → yeni GBG19_TropicalBeach.png (kaydırmasız), borderColorId 5, koyu hücre |
| 2026-11-11 | Settings/ChapterThemeSet/ChapterTheme_5.asset | Eski: gameBackground GBG3V1 (guid 1ffcbeb613bd8411cab3c93c2d6e0373), borderColorId 0, lightBoardCells 1 | Bölüm 5 (level 101–125) → yeni GBG20_SnowCabin.png (981×1602 → 941 genişlik, üst 60 px gökyüzü + alt 75 px kar zemini eklendi), borderColorId 5, koyu hücre |

| 2026-11-11 | ChapterTheme_2, _3, _5 borderColorId | 5 (Petrol) | 6 (Indigo): lila hücrelerle aynı renk ailesi; 1 ve 4 Petrol kaldı (çöl turuncusuna zıt, plajda deniz turkuazı) |
| 2026-11-15 | Art/Icons/Gems/AnimalStyle/V3/GreenStoneV9.png | Eski: Docs/Degisenler/2026-11-15/GreenStoneV9.png (doygunluk 0.72) | Yeşil taş canlandırıldı: doygunluk ×1.28, parlaklık ×1.10, ton -3.6° (≈0.91, diğer üç taşla eşit). Kullanıcı: "yeşil biraz ölü duruyor" |
| 2026-11-15 | Art/Icons/Gems/AnimalStyle/V3/GreenStoneV9.png | Eski: Docs/Degisenler/2026-11-15/GreenStoneV9_doygunlukB.png (ve ilk orijinal GreenStoneV9.png) | Yeni silüet: ters trapez tıpa + düz boğum + armut gövde (kullanıcı çizimi, ChatGPT render, "2" + canlandırma). Match Villains şişesinden ayrışma. Kullanıcı onayı |
| 2026-11-15 | Art/UI/BackGroundImgs/LevelLoadingImg/Hint_3Match_YellowBreak_NoText_NoPanels.png | Eski: Docs/Degisenler/2026-11-15/Hint_3Match_YellowBreak_NoText_NoPanels.png | Yükleme ipucu görseli kullanıcının yeni sürümüyle değiştirildi (güncel taş çizimleri) |
| 2026-11-15 | Art/UI/BackGroundImgs/LevelLoadingImg/LİneHLoading.png | Eski: Docs/Degisenler/2026-11-15/LİneHLoading.png | Yükleme ipucu görseli kullanıcının yeni sürümüyle değiştirildi (güncel taş çizimleri) |
| 2026-11-17 | Art/UI/DailySlotMachine/spinBG2, innerWheelV4, OuterWheelV4, Pointer, SpinHeader (.png) | Eski: Docs/Degisenler/2026-11-17/DailyWheel/ (mor kumarhane sahnesi, ampullü çerçeve, "Daily Spin" yazılı başlık) | Günlük çark atölye temasına geçti (kullanıcı ChatGPT görselleri): zeminli atölye arka planı, 8 dilimli dişli çark (ovalliği düzeltildi), OuterWheel = altın göbek, Pointer = anahtar ok, SpinHeader = yazısız tabela (başlık koddan wheel_title). Yeni: BearLever_Up/Mid/Down.png (kol çeken ayı, taban hizalı). Aynı GUID'ler korundu |
- 2026-11-19 WorldTour: City_mis1_Site/City_mis7_Site (eski 21:9 iskeleli panoramalar) + City_mis1_eski_panorama — yeni 4:3 dikey uyumlu şehir resimlerine geçildi; iskeleli yeniler bekleniyor.
- 2026-11-19 WorldTour: City_mis7 eski eşyasız İstanbul → yıkık halle aynı meydanlı yeni resim (City_mis7_eski_esyasiz.png).
- 2026-11-19 AppIcon: RamLogo.png (uygulama ikonu, 14 slot) koç → baretli ayı; aynı dosya/GUID. Eski: RamLogo_eski_kocikon.png
