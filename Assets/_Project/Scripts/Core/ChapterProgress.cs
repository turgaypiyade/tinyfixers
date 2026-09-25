using UnityEngine;

/// <summary>
/// "Oyuncu kaçıncı bölümde (chapter)?" sorusunun TEK merkezi kaynağı.
/// Level numarası [[CurrentLevel]]'den, bölüm uzunluğu ChapterThemeLibrary asset'inden gelir
/// (levelsPerChapter) — sayı hiçbir yerde ikinci kez yazılmaz.
///
/// Kütüphane Resources'tan otomatik yüklenir; isteyen sahne tarafı elindeki referansı
/// <see cref="Library"/>'ye set ederek (örn. Awake'te) bunu ezebilir.
/// Kütüphane hiç bulunamazsa bölüm 1 kabul edilir ve chapter'a bağlı kapılar AÇIK kalır —
/// yanlış konfigürasyon oyuncuyu asla ilerlemeden kilitlemez.
/// </summary>
public static class ChapterProgress
{
    public const string ResourcesPath = "ChapterThemeLibrary";

    private static ChapterThemeLibrary _library;
    private static bool _resolveAttempted;
    private static bool _missingWarned;

    /// <summary>Bölüm uzunluğunu veren kütüphane. Set edilmezse Resources'tan yüklenir.</summary>
    public static ChapterThemeLibrary Library
    {
        get
        {
            if (_library == null && !_resolveAttempted)
            {
                _resolveAttempted = true;
                _library = Resources.Load<ChapterThemeLibrary>(ResourcesPath);
            }
            return _library;
        }
        set
        {
            if (value == null) return;
            _library = value;
            _resolveAttempted = true;
        }
    }

    /// <summary>Kütüphane bulunabildi mi? false ise chapter bilgisi güvenilir değildir.</summary>
    public static bool HasLibrary
    {
        get
        {
            var lib = Library;
            if (lib == null && !_missingWarned)
            {
                _missingWarned = true;
                Debug.LogWarning(
                    $"[ChapterProgress] ChapterThemeLibrary bulunamadı (Resources/{ResourcesPath}). " +
                    "Bölüm bilgisi gerektiren kapılar AÇIK kabul edilecek.");
            }
            return lib != null;
        }
    }

    /// <summary>Bölüm başına level sayısı (kütüphane yoksa 1 → her level bir bölüm sayılmaz, bkz. Current).</summary>
    public static int LevelsPerChapter
    {
        get
        {
            var lib = Library;
            return lib != null ? Mathf.Max(1, lib.levelsPerChapter) : 1;
        }
    }

    /// <summary>Oyuncunun şu anki 1-tabanlı bölümü. Kütüphane yoksa 1.</summary>
    public static int Current => ChapterOfLevel(CurrentLevel.Global);

    /// <summary>Verilen global level hangi bölüme düşer (1-tabanlı). Kütüphane yoksa 1.</summary>
    public static int ChapterOfLevel(int globalLevel)
    {
        var lib = Library;
        if (lib == null) return 1;
        return lib.GetChapterForLevel(globalLevel);
    }

    /// <summary>Bir bölümün İLK level numarası (1-tabanlı).</summary>
    public static int FirstLevelOfChapter(int chapter)
        => (Mathf.Max(1, chapter) - 1) * LevelsPerChapter + 1;

    /// <summary>Bir bölümün SON level numarası (1-tabanlı).</summary>
    public static int LastLevelOfChapter(int chapter)
        => Mathf.Max(1, chapter) * LevelsPerChapter;
}
