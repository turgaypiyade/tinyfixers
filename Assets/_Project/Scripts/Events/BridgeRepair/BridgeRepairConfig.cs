using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Yarıştaki bir karakterin kareleri (sağa bakar). Walk = yürüme döngüsü, Bend = ayakta → eğil → diz çök.</summary>
[Serializable]
public sealed class BridgeCharacterDef
{
    public string id = "bear";
    public Sprite[] walk = Array.Empty<Sprite>();
    [Tooltip("0 = ayakta (idle), sonrakiler eğilme sırası (son kare = köprüye uzanmış).")]
    public Sprite[] bend = Array.Empty<Sprite>();

    public Sprite Idle => bend != null && bend.Length > 0 ? bend[0] : (walk != null && walk.Length > 0 ? walk[0] : null);
}

/// <summary>
/// Bridge Repair (Köprü Tamiri) yarış eventinin tüm parametreleri. Asset: Resources/Events/BridgeRepairConfig.asset.
///
/// Kurallar (kullanıcı spec'i, 2026-09-27):
///  - 30. seviye bitince açılır (<see cref="minLevelGate"/> = oynanacak level ≥ 31).
///  - Haftada <see cref="eventsPerWeek"/> kez RASTGELE günde (tüm cihazlarda aynı gün) <see cref="windowHours"/> saat sürer.
///  - <see cref="contestantCount"/> yarışmacı (oyuncu + botlar). Her KAZANILAN level = köprüye 1 parça;
///    kaybetmenin cezası YOK. <see cref="levelsToFinish"/> levelde oyuncu için yarış biter.
///  - İlk <see cref="prizeRanks"/> sıraya ödül. Oyuncu bitirmeden ilk 3 dolarsa oyuncu için yarış biter (elenir).
///  - Botların bitiş süresi tamamen rastgele (<see cref="botMinFinishHours"/>..<see cref="botMaxFinishHours"/>).
/// </summary>
[CreateAssetMenu(fileName = "BridgeRepairConfig", menuName = "TinyFixers/Events/Bridge Repair Config")]
public sealed class BridgeRepairConfig : ScriptableObject
{
    public const string ResourcePath = "Events/BridgeRepairConfig";

    [Header("Kimlik")]
    public string eventId = "bridge_repair";

    [Header("Kapı & Zamanlama")]
    [Tooltip("Oynanacak level (CurrentLevel.Global) bu değere ulaşınca açılır. 31 = 30. seviye bitince.")]
    [Min(1)] public int minLevelGate = 31;

    [Tooltip("Haftada kaç kez (farklı günlerde) çıkar. Günler hafta numarasından türetilir → tüm cihazlarda aynı.")]
    [Range(1, 7)] public int eventsPerWeek = 2;

    [Tooltip("Bir pencere kaç saat açık kalır.")]
    [Min(1)] public int windowHours = 24;

    [Tooltip("Rastgele gün seçiminde hiç kullanılmayacak günler (UTC). Boş = tüm günler aday.")]
    public List<DayOfWeek> excludedDays = new();

    [Tooltip("Gün seçim tohumu. Değiştirmek tüm oyuncular için takvimi karıştırır (canlıda dokunma).")]
    public int scheduleSalt = 7129;

    [Tooltip("Yarış oyuncu için bitince (bitirdi / ilk 3 doldu) yeni yarışa girmek için beklenecek süre (dk). " +
             "Pencere açık kaldıkça bu aralıkla tekrar katılınabilir.")]
    [Min(0)] public int rejoinCooldownMinutes = 20;

    [Tooltip("Açıksa event ilk aktif olunca (bu pencerede bir kez) otomatik katılım popup'ı çıkar.")]
    public bool autoShowJoinPopup = true;

    [Header("Yarış")]
    [Tooltip("Oyuncu dahil yarışmacı sayısı (arka planda 5 köprü var).")]
    [Range(2, 5)] public int contestantCount = 5;

    [Tooltip("Köprüyü bitirmek için kazanılacak level sayısı.")]
    [Min(1)] public int levelsToFinish = 15;

    [Tooltip("Ödül alan ilk N sıra. N bot oyuncudan önce bitirirse oyuncu için yarış biter.")]
    [Min(1)] public int prizeRanks = 3;

    [Tooltip("Bir botun katılımdan sonra köprüyü bitirmesi için en kısa süre (saat). Bitiş süresi bu aralıkta tamamen rastgele.")]
    [Min(0.1f)] public float botMinFinishHours = 3f;

    [Tooltip("Bir botun köprüyü bitirmesi için en uzun süre (saat). Pencereden uzunsa bazı botlar hiç bitiremez.")]
    [Min(0.1f)] public float botMaxFinishHours = 30f;

    [Header("Ödüller (sıraya göre)")]
    public List<DailySlotReward> rank1Rewards = new()
    {
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = 1000, fallbackName = "1000 Altın" },
        new DailySlotReward { type = DailySlotRewardType.Booster_Hammer, amount = 1, fallbackName = "Çekiç" },
        new DailySlotReward { type = DailySlotRewardType.Booster_Shuffle, amount = 1, fallbackName = "Karıştır" },
    };
    public List<DailySlotReward> rank2Rewards = new()
    {
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = 500, fallbackName = "500 Altın" },
        new DailySlotReward { type = DailySlotRewardType.Booster_Hammer, amount = 1, fallbackName = "Çekiç" },
    };
    public List<DailySlotReward> rank3Rewards = new()
    {
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = 250, fallbackName = "250 Altın" },
    };

    [Header("Karakterler (sağa bakan kareler)")]
    public List<BridgeCharacterDef> characters = new();

    [Header("Debug (yalnız Editor — PRODUCTION'DA KAPAT)")]
    [Tooltip("Açıkken takvim ve level kapısı yok sayılır (yalnız UNITY_EDITOR). Canlıya çıkmadan kapat.")]
    public bool debugForceAvailable = false;

    public IReadOnlyList<DailySlotReward> RewardsForRank(int rank)
    {
        return rank switch
        {
            1 => rank1Rewards,
            2 => rank2Rewards,
            3 => rank3Rewards,
            _ => Array.Empty<DailySlotReward>()
        };
    }

    private static BridgeRepairConfig s_shared;
    public static BridgeRepairConfig Shared =>
        s_shared != null ? s_shared : (s_shared = Resources.Load<BridgeRepairConfig>(ResourcePath));
}
