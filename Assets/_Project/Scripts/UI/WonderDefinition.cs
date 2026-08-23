using System;
using UnityEngine;

/// <summary>
/// Tek bir "dünya harikası"nın tüm verisi: arka plan imajı, kaynak kademeleri,
/// kaynakçı kareleri ve üzerinde gezen karakterler (her biri kendi yolu + kareleriyle).
/// WonderScene bu veriden sahneyi runtime kurar. [[project_wonder_reveal_background]]
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Wonder Definition", fileName = "Wonder_")]
public class WonderDefinition : ScriptableObject
{
    [Header("Kimlik / Görev Görünümü")]
    public string wonderId = "wonder";
    [Tooltip("Mission satırında + Journey'de görünen isim (ör. Mahalron)")]
    public string displayName = "";
    [Tooltip("Mission satırındaki ikon. Boşsa backgroundSprite kullanılır.")]
    public Sprite taskIcon;

    [Header("Arka Plan")]
    public Sprite backgroundSprite;
    [Tooltip("Görev sayısı = kaynak kademesi sayısı. Reveal % = yapılan görev / bu.")]
    [Min(1)] public int totalStages = 5;

    [Header("Görevler (parça parça inşa)")]
    [Tooltip("Her görev = harikanın bir parçası: ad + ikon + yıldız. Doluysa görev sayısı BUNDAN gelir.")]
    public WonderTask[] tasks;

    [Header("Görev Maliyeti — tasks BOŞSA yedek")]
    [Tooltip("tasks boşsa: görev başı yıldız. Boşsa base+step formülü.")]
    public int[] starCosts;
    public int starCostBase = 5;
    public int starCostStep = 5;

    /// <summary>Görev sayısı — tasks doluysa ondan, değilse totalStages.</summary>
    public int TaskCount => (tasks != null && tasks.Length > 0) ? tasks.Length : totalStages;

    /// <summary>Görev adı — tasks'ta varsa, yoksa harika adı.</summary>
    public string GetTaskName(int stage)
    {
        if (tasks != null && stage >= 0 && stage < tasks.Length && !string.IsNullOrEmpty(tasks[stage].name))
            return tasks[stage].name;
        return string.IsNullOrEmpty(displayName) ? wonderId : displayName;
    }

    /// <summary>Görev ikonu — tasks'ta varsa, yoksa taskIcon, yoksa backgroundSprite.</summary>
    public Sprite GetTaskIcon(int stage)
    {
        if (tasks != null && stage >= 0 && stage < tasks.Length && tasks[stage].icon != null)
            return tasks[stage].icon;
        return taskIcon != null ? taskIcon : backgroundSprite;
    }

    /// <summary>Bu görevin ELLE atanmış özel ikonu var mı? Yoksa reveal-önizleme kullanılır.</summary>
    public bool HasExplicitTaskIcon(int stage)
        => (tasks != null && stage >= 0 && stage < tasks.Length && tasks[stage].icon != null) || taskIcon != null;

    [Header("Tamamlama Ödülü (sandık)")]
    public DailySlotReward[] chestRewards;
    public Sprite chestClosedSprite;
    public Sprite chestOpenedSprite;

    /// <summary>Belirli bir görevin (0-index) yıldız maliyeti.</summary>
    public int GetStarCost(int stage)
    {
        if (tasks != null && stage >= 0 && stage < tasks.Length && tasks[stage].starCost > 0)
            return tasks[stage].starCost;
        if (starCosts != null && stage >= 0 && stage < starCosts.Length)
            return starCosts[stage];
        return starCostBase + starCostStep * Mathf.Max(0, stage);
    }

    [Header("Kaynakçı")]
    public Sprite[] welderFrames;
    public float welderFps = 10f;
    public Vector2 welderSize = new Vector2(240, 240);
    [Tooltip("Kaynakçının başlangıç/park konumu (anchored)")]
    public Vector2 welderHome = Vector2.zero;

    [Header("Karakterler")]
    public WonderCharacter[] characters;
}

/// <summary>Bir harika görevi = inşa parçası: ad + ikon + yıldız maliyeti.</summary>
[Serializable]
public class WonderTask
{
    public string name;      // ör. "Ana Kubbe" (boşsa harika adı)
    public Sprite icon;      // görev satırı ikonu (boşsa harika ikonu)
    public int starCost = 5; // bu görevin yıldız maliyeti
}

/// <summary>Harika üzerinde gezen bir karakter (robot/dron) — yolu + kareleri + ayarları.</summary>
[Serializable]
public class WonderCharacter
{
    public string name = "robot";
    public WonderAmbientAgent.FacingMode facingMode = WonderAmbientAgent.FacingMode.DirectionalFrontBack;

    [Header("Kareler")]
    public Sprite[] frontFrames;   // ileri giderken (bize dönük)
    public Sprite[] backFrames;    // dönüşte (arkası dönük)
    public Sprite[] walkFrames;    // SideMirror/dron
    public float walkFps = 5f;
    public bool mirrorBySide = false;

    [Header("Hareket")]
    public float speed = 90f;
    public float bobAmplitude = 8f;
    public float bobFrequency = 6f;
    public float visualSize = 200f;
    public bool pingPong = true;
    public float pauseAtPoint = 0.4f;

    [Header("Yol (anchored noktalar)")]
    public Vector2[] path;
}
