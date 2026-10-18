# Chapter theme, Mission ve Journey bağlantıları

`Resources/ChapterThemeLibrary.asset` içindeki `levelsPerChapter = 25` tek chapter uzunluğu kaynağıdır. `ChapterProgress` / `ChapterThemeLibrary.GetChapterForLevel` mevcut global levelden chapter'ı hesaplar.

| Chapter | Level aralığı | Oyun arka planı | Harika kaydı |
|---|---|---|---|
| 1 | 1–25 | GBG10 | mis1 — Eğik Pati Kulesi |
| 2 | 26–50 | GBG8 | mis2 — Altın Kuyruk Köprüsü |
| 3 | 51–75 | GBG9 | mis3 — Eyfil Kulesi |
| 4 | 76–100 | GBG2 | mis4 — Miyav Piramidi |
| 5 | 101–125 | GBG3 | mis5 — Büyük Pati Seddi / Great Paw Wall |
| 6 | 126–150 | GBG3 | mis6 — Kuyruk Kanyonu / Tail Canyon |

Oyun ekranı ve standart menü görselleri `Settings/ChapterThemeSet/ChapterTheme_*.asset` üzerinden atanır. Mission popup'ı, Journey kartları ve tamamlanmış harika arka planı ise **aynı `Settings/Wonders/WonderCatalog.asset`** listesini kullanır. Sadece theme'in `menuBackground` alanını değiştirmek Mission veya Journey'ye kayıt eklemez.

## Beşinci ve altıncı harika

- `Wonder_mis5.asset`: `GreatWall.png`, sekiz onarım görevi.
- `Wonder_mis6.asset`: `GrandKanyonV1.png`, sekiz onarım görevi.
- Harika ve görev adları `tinyfixers_localization.json` içinde `wonder_mis5_name`, `wonder_mis6_name`, `wonder_mis5_task_1…8`, `wonder_mis6_task_1…8` anahtarlarıyla Türkçe/İngilizce tanımlıdır.
- Her yeni harikanın maliyetleri mevcut mis4 dizisiyle aynıdır: 3, 3, 3, 2, 3, 3, 2, 5; toplam **24 yıldız**. Görevin `starCost = 0` olması ücretsiz olduğu anlamına gelmez: `GetStarCost` önce `starCosts[]` dizisini kullanır. Bu diziyi atlayıp yedek 5, 10…40 formülünden 180 yıldız hesaplamak yanlıştır.
- Sandık ödülleri, kaynakçı kareleri ve etkin kuş profili mis4'ten korunur. Yeni runtime/VFX kodu veya efekt havuzu eklenmez. Overlay aynı harikanın sonraki görevlerinde mevcut sahne/karakter/materyal/buffer'larını kullanmaya devam eder.

## Açılma ve gösterim

`WonderProgress.UnlockedEventCount` açık görev seti sayısını mevcut chapter ile sınırlar. mis5, 100. level bitip 101 açılınca; mis6, 125. level bitip 126 açılınca yapılabilir. Önceki harikaların bitmesi şart değildir; görev ilerlemeleri bağımsızdır.

MainMenu'deki Mission popup'ı aynı anda üç satır gösterir. Açık ve bitmemiş harikaların sıradaki görevleri katalog sırasıyla bu alanı doldurur; önceki harikalar bitmemişse mis5/mis6 görevleri henüz üç satıra sığmayabilir. Bu mevcut sıralama ve satır sınırı değiştirilmedi.

Journey bütün katalog kayıtlarını önizleme olarak listeler. Yapılmamış harikalar hologramdır; görevi chapter ile açılır. **Kullan** butonu ancak harika tamamen onarıldığında görünür. Seçilen tamamlanmış harika veya son tamamlanan harika, ana menüde `WonderBackgroundView` üzerinden gösterilir.

İlk dört katalog kaydı aynı sırada tutuldu; yeni ikisi sona eklendi. Mevcut `wonder_stage_0…3` ilerlemelerinin anlamı değişmedi. Sahne referansları değiştirilmedi.

Doğrulama: altı kayıt/meta/görsel bağlantısı, iki dilde isim ve görev anahtarları, sahnedeki Mission/Journey/background katalog eşleşmesi statik kontrol edildi. Üretim chapter ve maliyet metotları izole C# kontrollerinde çalıştırıldı. Unity build'i ve canlı Play Mode testi yapılmadı.
