using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yolculuk içeriği. Her bölüm = tamir görevleriyle (≈9-10 görev) oluşan bir arka plan
/// resmi. Resim cache'lenip burada büyük kart olarak gösterilir. Görseller (ChapterTheme'in
/// menuBackground/gameBackground'undan) buraya Inspector'da bağlanır.
///
/// Oluştur: Assets > Create > TinyFixers > Journey Catalog
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Journey Catalog", fileName = "JourneyCatalog")]
public sealed class JourneyCatalog : ScriptableObject
{
    public List<JourneyChapter> chapters = new();

    private const string CurrentKey = "journey_current_index";

    /// <summary>Şu an aktif (henüz tamamlanmakta olan) bölüm index'i — PlayerPrefs.</summary>
    public static int CurrentIndex
    {
        get => PlayerPrefs.GetInt(CurrentKey, 0);
        set { PlayerPrefs.SetInt(CurrentKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }
}

/// <summary>Tek bir yolculuk bölümü.</summary>
[Serializable]
public sealed class JourneyChapter
{
    public string title = "Bölüm";
    public int chapterNumber = 1;

    [Tooltip("Tamir görevleriyle oluşan arka plan resmi (cache'lenmiş).")]
    public Sprite image;

    [Tooltip("Resmin ne kadarı tamir edildi (0-1). 1 = tamamlandı.")]
    [Range(0f, 1f)] public float revealProgress = 1f;
}
