using UnityEngine;

public enum EventDurationType { Daily, Weekly, Custom }

/// <summary>
/// Tüm event parametrelerini tutan ScriptableObject.
/// Inspector'dan değiştirilebilir, kod değişikliği gerekmez.
/// </summary>
[CreateAssetMenu(fileName = "EventConfig", menuName = "TinyFixers/Event Config")]
public class EventConfig : ScriptableObject
{
    [Header("Kimlik")]
    public string eventId   = "streak_event";
    public string eventName = "Usta Tamirciler";

    [Header("Süre")]
    public EventDurationType durationType = EventDurationType.Daily;
    [Tooltip("Sadece Custom seçildiğinde geçerli")]
    [Min(1)] public int customDurationHours = 24;

    public int DurationHours => durationType switch
    {
        EventDurationType.Daily   => 24,
        EventDurationType.Weekly  => 168,
        _                         => customDurationHours
    };

    [Header("Kazanma Koşulu")]
    [Tooltip("Ödül için üst üste kazanılması gereken seviye sayısı")]
    [Min(1)] public int consecutiveWinsRequired = 5;

    [Header("Cooldown")]
    [Tooltip("Kaybedince tekrar katılmak için bekleme süresi (dakika)")]
    [Min(1)] public int failCooldownMinutes = 60;

    [Header("Ödül")]
    [Tooltip("Event'i kazananlar arasında paylaşılacak toplam coin")]
    [Min(1)] public int prizePool = 1000;

    [Header("Katılımcı")]
    [Tooltip("0 = sınırsız")]
    public int maxParticipants = 0;
}
