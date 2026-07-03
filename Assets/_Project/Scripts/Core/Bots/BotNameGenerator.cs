using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistem diline göre gerçekçi bot isimleri üretir.
/// Takım, event ve gelecekteki tüm bot sistemlerinde ortak kullanılır.
/// </summary>
public static class BotNameGenerator
{
    private static readonly string[] TurkishFirstNames =
    {
        "Ahmet", "Mehmet", "Ali", "Mustafa", "Hüseyin", "İbrahim", "Hasan",
        "Kemal", "Emre", "Burak", "Furkan", "Serkan", "Murat", "Oğuz",
        "Zeynep", "Ayşe", "Fatma", "Elif", "Emine", "Hatice", "Merve",
        "Selin", "Büşra", "Derya", "Gizem", "Tuğçe", "Neslihan", "Yasemin"
    };

    private static readonly string[] TurkishSuffixes =
    {
        "Usta", "Tamirci", "Kahraman", "Çelik", "Demir", "Yıldız",
        "Avcı", "Keskin", "Pala", "Kaya", "Dağ", "Bulut"
    };

    private static readonly string[] EnglishFirstNames =
    {
        "Alex", "Sam", "Jordan", "Taylor", "Morgan", "Casey", "Riley",
        "Jamie", "Quinn", "Drew", "Blake", "Skyler", "Avery", "Logan",
        "Parker", "Hunter", "Mason", "Tyler", "Dylan", "Cameron"
    };

    private static readonly string[] EnglishSuffixes =
    {
        "Fixer", "Master", "Pro", "Swift", "Sharp", "Bolt",
        "Gear", "Core", "Ace", "Prime", "Spark", "Blaze"
    };

    private static readonly HashSet<string> _usedNames = new();

    public static string Generate(BotNameLanguage language)
    {
        // Büyük isim havuzu (Resources/SeedData/player_names) varsa oradan benzersiz isim ver.
        if (NamePool.HasPlayers)
            return NamePool.NextPlayerName();

        string[] firstNames = language == BotNameLanguage.Turkish
            ? TurkishFirstNames : EnglishFirstNames;
        string[] suffixes = language == BotNameLanguage.Turkish
            ? TurkishSuffixes : EnglishSuffixes;

        // En fazla 20 denemede benzersiz isim bul
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string first  = firstNames[Random.Range(0, firstNames.Length)];
            string suffix = suffixes[Random.Range(0, suffixes.Length)];
            int    number = Random.Range(10, 9999);
            string name   = $"{first}_{suffix}{number}";

            if (_usedNames.Add(name))
                return name;
        }

        // Fallback: timestamp bazlı benzersiz isim
        return $"Bot_{System.DateTime.UtcNow.Ticks % 1_000_000}";
    }

    public static BotNameLanguage DetectLanguage(BotConfig config)
    {
        if (!config.autoDetectLanguage)
            return config.fallbackLanguage;

        return Application.systemLanguage == SystemLanguage.Turkish
            ? BotNameLanguage.Turkish
            : BotNameLanguage.English;
    }

    public static void ClearUsedNames() => _usedNames.Clear();
}
