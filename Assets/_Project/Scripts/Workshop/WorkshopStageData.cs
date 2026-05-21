using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("Bu aşamaya GEÇİLDİĞİNDE atölyenin alacağı tam sahne görseli. " +
             "Stage 0 = başlangıç durumu, Stage N = N kez tamir sonrası durum.")]
    public Sprite stageImage;

    [Tooltip("Task list item'da gösterilecek ikon. Genelde stageImage'in bir kırpıntısı veya " +
             "değişen bölgeye ait küçük bir illüstrasyon.")]
    public Sprite taskIcon;

    [Tooltip("Sparkle/VFX'in patlayacağı bölge — sahnedeki Image'in normalize koordinatı (0-1). " +
             "Örn (0.5, 0.5) = ortası, (0.7, 0.3) = sağ-üst bölge. Robot ayağa kalkıyorsa (0.5, 0.6).")]
    public Vector2 vfxFocusNormalized = new Vector2(0.5f, 0.5f);

    [Tooltip("Tamamlandığında çalacak ses. Opsiyonel.")]
    public AudioClip sfxOnComplete;
}

[CreateAssetMenu(fileName = "WorkshopStageData", menuName = "CoreCollapse/Workshop/Stage Data", order = 1)]
public class WorkshopStageData : ScriptableObject
{
    [Tooltip("İlk eleman = Stage 0 = başlangıç (en kirli/dağınık) görseli. " +
             "Sonraki her eleman bir önceki üzerine eklenecek tamirin sonucu.")]
    public List<WorkshopStage> stages = new List<WorkshopStage>();

    [Header("Final Reward (Sandık)")]
    [Tooltip("Tüm aşamalar tamamlanınca verilecek ödül. Progress bar'ın sonunda chestIcon gösterilir.")]
    public WorkshopReward finalReward;
}
