using System.Collections.Generic;
using UnityEngine;

public enum WorkshopMode
{
    SpriteSwap,  // Mevcut davranış: tüm sahne tek resim, crossfade ile değişir
    LayerFlyIn   // Yeni: temiz background sabit, parçalar uçup ekrana konumlanır
}

public enum FlyInFrom { Left, Right, Bottom, Top }

[System.Serializable]
public class WorkshopStage
{
    [Tooltip("Task list'te gösterilecek metnin lokalizasyon anahtarı. " +
             "Örn: \"workshop_task_left_leg\" → \"Robotun sol bacağını tamir et\".")]
    public string nameLocalizationKey;

    [Tooltip("Lokalizasyon yoksa fallback olarak kullanılacak Türkçe ad.")]
    public string stageName;

    [Tooltip("Bu aşamayı tamamlamak için harcanacak yıldız sayısı.")]
    [Min(0)] public int starCost = 1;

    [Tooltip("SpriteSwap modunda: bu aşamaya geçilince gösterilecek tam sahne görseli.\n" +
             "LayerFlyIn modunda: Stage 0 = temiz background, Stage 1+ = kullanılmaz.")]
    public Sprite stageImage;

    [Tooltip("Task list item'da gösterilecek ikon.")]
    public Sprite taskIcon;

    [Tooltip("Sparkle/VFX'in patlayacağı bölge — normalize koordinat (0-1).")]
    public Vector2 vfxFocusNormalized = new Vector2(0.5f, 0.5f);

    [Tooltip("Tamamlandığında çalacak ses. Opsiyonel.")]
    public AudioClip sfxOnComplete;

    [Header("Layer Fly-In (mode=LayerFlyIn, Stage 1+ için)")]
    [Tooltip("Uçup gelecek parça sprite'ı. Background ile aynı canvas boyutunda olmalı.")]
    public Sprite layerSprite;

    [Tooltip("Parçanın geleceği ekran kenarı.")]
    public FlyInFrom flyInFrom = FlyInFrom.Left;
}

[CreateAssetMenu(fileName = "WorkshopStageData", menuName = "CoreCollapse/Workshop/Stage Data", order = 1)]
public class WorkshopStageData : ScriptableObject
{
    [Tooltip("SpriteSwap: tüm sahne tek resim, crossfade.\n" +
             "LayerFlyIn: Stage 0 = temiz background, Stage 1+ = uçan parçalar.")]
    public WorkshopMode mode = WorkshopMode.SpriteSwap;

    [Tooltip("İlk eleman = Stage 0 = başlangıç görseli veya temiz background.\n" +
             "Sonraki her eleman bir aşamayı temsil eder.")]
    public List<WorkshopStage> stages = new List<WorkshopStage>();

    [Header("Final Reward (Sandık)")]
    [Tooltip("Tüm aşamalar tamamlanınca verilecek ödül paketi.")]
    public WorkshopRewardBundle finalReward;
}
