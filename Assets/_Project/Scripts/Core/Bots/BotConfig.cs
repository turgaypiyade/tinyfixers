using UnityEngine;

/// <summary>
/// Bot havuzu ve takım sistemi için tüm parametreler.
/// Inspector'dan değiştirilebilir.
/// </summary>
[CreateAssetMenu(fileName = "BotConfig", menuName = "TinyFixers/Bot Config")]
public class BotConfig : ScriptableObject
{
    [Header("Bot Havuzu")]
    [Tooltip("Oyunda simüle edilecek toplam bot sayısı")]
    [Min(1)] public int targetBotPoolSize = 100_000;

    [Tooltip("Bu kadar gerçek kullanıcı gelince botlar tamamen sıfırlanır")]
    [Min(1)] public int realUserThreshold = 100_000;

    [Header("Bot Davranışı")]
    [Range(0f, 1f)]
    [Tooltip("Botların bir seviyeyi kazanma olasılığı")]
    public float botWinRate = 0.65f;

    [Tooltip("Bir bot'un level tamamlama süresi (saniye) — minimum")]
    [Min(5f)] public float botPlayIntervalMinSeconds = 30f;

    [Tooltip("Bir bot'un level tamamlama süresi (saniye) — maksimum")]
    [Min(10f)] public float botPlayIntervalMaxSeconds = 300f;

    [Header("Takım")]
    [Tooltip("İlk açılışta otomatik oluşturulacak takım sayısı")]
    [Min(1)] public int initialTeamCount = 100;

    [Tooltip("Her takımdaki üye sayısı")]
    [Min(1)] public int teamMemberCount = 40;

    [Header("Dil")]
    [Tooltip("True: sistem dilini otomatik algıla. False: aşağıdaki dili kullan")]
    public bool autoDetectLanguage = true;
    public BotNameLanguage fallbackLanguage = BotNameLanguage.Turkish;
}

public enum BotNameLanguage { Turkish, English }
