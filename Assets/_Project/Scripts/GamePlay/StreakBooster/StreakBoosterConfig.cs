using UnityEngine;

/// <summary>
/// Streak booster event ayarlarını Inspector'dan değiştirilebilir kılar. Bir bootstrap/persistent
/// objeye koy (ör. MainMenu veya Game sahnesinde). Awake'te statik ThresholdLevel'ı set eder.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class StreakBoosterConfig : MonoBehaviour
{
    [Tooltip("Bu level ve SONRASINDA streak booster event'i aktif olur (üst üste geçişte special hakkı).")]
    [SerializeField, Min(1)] private int thresholdLevel = 25;

    private void Awake()
    {
        StreakBoosterEvent.ThresholdLevel = Mathf.Max(1, thresholdLevel);
    }
}
