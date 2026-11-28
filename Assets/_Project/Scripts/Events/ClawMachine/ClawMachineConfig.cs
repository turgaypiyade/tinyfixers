using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Pençe Makinesi ayarları (Docs/ClawMachine_Plan.md). Tüm denge sayıları burada; kod sabit sayı taşımaz.
/// Resources/Events/ClawMachineConfig.
/// </summary>
[CreateAssetMenu(fileName = "ClawMachineConfig", menuName = "TinyFixers/Events/Claw Machine Config")]
public sealed class ClawMachineConfig : ScriptableObject
{
    public const string ResourcePath = "Events/ClawMachineConfig";
    private static ClawMachineConfig shared;
    // Parçalı içerik: etkin yayında oyuncudaki kopya görselsiz gelir; görseller event paketinden bağlanır (GateEvent).
    public static ClawMachineConfig Shared => shared != null ? shared
        : (shared = ProgressiveContent.EventSourceConfig<ClawMachineConfig>(ResourcePath));

    [Header("Açılış ve oyuncuya özel UTC döngüsü")]
    [Tooltip("Bu seviyeye gelen (oynanacak level ≥ değer) oyuncuda açılır.")]
    [Min(1)] public int unlockLevel = 16;   // 2026-11-28: 70 → 16 (16-19 açılış listesinde boş)
    [Tooltip("Sezon (koleksiyon + jeton) süresi. Haftalık: 156 sa + 12 sa ara.")]
    [Min(1)] public int activeHours = 156;
    [Min(1)] public int restHours = 12;
    [Tooltip("Sezon içinde gün aşırı açık (kapalı günde jeton birikir, makine kapalı).")]
    public bool everyOtherDay = true;
    [Tooltip("Açık günde oyun oturumu (dk): ilk denemeyle başlar.")]
    [Min(1)] public int sessionMinutes = 30;
    [Tooltip("Oturum bitince mola (dk).")]
    [Min(0)] public int sessionRestMinutes = 120;
    [Tooltip("Yalnız editör. Oyuncu build'inde kapıyı asla açmaz.")]
    public bool debugForceAvailable;

    [Header("Jeton")]
    [Min(0)] public int joinTokens = 3;
    [Tooltip("Kullanılmıyor (2026-11-28): normal seviyede winsPerToken kazanışta 1 jeton.")]
    [Min(0)] public int tokensPerWin = 1;
    [Tooltip("Normal seviyede kaç kazanışta 1 jeton (ekonomi 2026-11-28: 2).")]
    [Min(1)] public int winsPerToken = 2;
    [Tooltip("Zor/çok zor seviye kazanışı: hemen bu kadar jeton.")]
    [Min(0)] public int tokensPerHardWin = 1;
    [Min(1)] public int tokenCap = 10;
    [Tooltip("Altınla 1 jeton fiyatı (altın harcama yeri).")]
    [Min(1)] public int coinTokenPrice = 300;
    [Min(0)] public int coinTokensPerDay = 3;
    [Tooltip("Koleksiyonda zaten olan peluş tekrar yakalanınca verilen jeton.")]
    [Min(0)] public int duplicatePlushTokens = 1;

    [Header("Yakalama kuralı — beceri tabanlı (2026-11-28, gizli garanti yok)")]
    [Tooltip("Yatay yarıçap (oyun alanı genişliğine oran). İsabet = 1 − elips mesafe.")]
    [Range(0.02f, 0.3f)] public float itemHalfWidth = 0.093f;
    [Tooltip("Derinlik yarıçapı (0..1 derinlik ekseninde). Sıralar 0.12 / 0.5 / 0.88'de.")]
    [Range(0.05f, 0.5f)] public float itemHalfDepth = 0.22f;
    [Tooltip("Kapsül: bu isabetin üstü kesin yakalar (çekirdek).")]
    [Range(0.1f, 1f)] public float coreAccuracy = 0.5f;
    [Tooltip("Peluş ağırdır: çekirdek daha dar.")]
    [Range(0.1f, 1f)] public float plushCoreAccuracy = 0.62f;
    [Tooltip("Bu isabetin altı boş pençe; üstü (çekirdeğe kadar) kenar: çoğunlukla kayar.")]
    [Range(0f, 1f)] public float slipAccuracy = 0.25f;
    [Tooltip("Kenar bölgesinde çekirdeğe en yakın noktadaki tutma şansı (kenarda 0'a iner). Kapsül.")]
    [Range(0f, 1f)] public float edgeGrip = 0.35f;
    [Range(0f, 1f)] public float plushEdgeGrip = 0.2f;
    [Tooltip("Pençe Gücü: bu kadar art arda kaçırıştan sonra sıradaki denemede pençe GÜÇLÜ (kenar da tutar; nişan yine şart).")]
    [Range(1, 10)] public int missesForPower = 3;
    // Büyük ödül: makinede her an en az bir büyük ödüllü kapsül görünür (ClawMachineState.EnsureBigVisible).
    [Tooltip("Ömür boyu ilk deneme (öğretici): yarıçaplar bu katla genişler.")]
    [Range(1f, 3f)] public float tutorialTolerance = 1.8f;

    [Header("Kontrol (joystick: yalnız sağa ve geriye; ortasına dokunmak İNDİR)")]
    [Tooltip("Tam itişte saniyede alınan yol (0..1 eksende).")]
    [Range(0.1f, 2f)] public float moveSpeed = 0.45f;
    [Range(0.1f, 2f)] public float depthSpeed = 0.55f;
    [Tooltip("Hareket bırakılınca sallanma genliği (hızla orantılı, yatay eksende).")]
    [Range(0f, 0.15f)] public float swayAmplitude = 0.025f;
    [Tooltip("Sallanmanın sönme süresi (sn).")]
    [Range(0.2f, 4f)] public float swaySettle = 0.8f;
    [Tooltip("Bu kadar deneme (sezon) süresiz; sonrasında her denemede süre sınırı (hareketle başlar).")]
    [Min(0)] public int untimedAttempts = 3;
    [Tooltip("Süre sınırı (sn). Bitince pençe kendiliğinden iner.")]
    [Range(3f, 30f)] public float moveSeconds = 12f;

    [Header("Makinenin içi")]
    [Range(4, 20)] public int itemCount = 20;
    [Tooltip("Makinede her an görünen en az peluş sayısı (yakalanınca yerine kapsül düşse de tamamlanır).")]
    [Range(0, 6)] public int minPlushVisible = 4;
    [Tooltip("Yeni oyuncak düşerken peluş olma ağırlığı (kapsül ağırlıklarının toplamına göre).")]
    [Min(0)] public int plushWeight = 30;
    [Tooltip("Kapsül ödülleri. big = büyük ödül (merhamet sayacını sıfırlar). Kapsül içeriği yakalanınca açılır.")]
    public ClawPrize[] prizes = ClawPrize.Defaults();
    [Tooltip("Kapsül alt yarısı renkleri (görsel).")]
    public Color[] capsuleColors =
    {
        new Color(0.98f, 0.78f, 0.16f), new Color(0.90f, 0.24f, 0.20f), new Color(0.24f, 0.55f, 0.95f),
        new Color(0.30f, 0.78f, 0.25f), new Color(0.94f, 0.36f, 0.66f), new Color(0.60f, 0.38f, 0.90f),
    };

    [Header("Koleksiyon (6 peluş → büyük sandık)")]
    public DailySlotReward[] collectionRewards =
    {
        new DailySlotReward { type = DailySlotRewardType.Joker_Line, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Joker_PulseCore, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Joker_SystemOverride, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Booster_Hammer, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Booster_Shuffle, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Lives, amount = 1, durationMinutes = 60 },
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = 200 },
    };

    [Header("Görseller (Art/UI/MainScreenEvents/ClawMachine)")]
    public Sprite menuIcon;
    public Sprite machineTop, machineGlass, machineBottom;
    public Sprite clawOpen, clawClosed;
    public Sprite capsuleBack, capsuleBottom, capsuleFront;
    [Tooltip("Sıra: Pilo, Tinko, Pemi, Bram, Brumo, Fiko (koleksiyon yuvası sırası).")]
    public Sprite[] plushes = Array.Empty<Sprite>();
    public Sprite closeIcon;
    [Tooltip("Jeton ikonu (bakır, siyah delikli). Event paketinde; sayaçta yazının solunda.")]
    public Sprite tokenIcon;
    [Tooltip("Ekran arka planı (sade çizgili mor, event paketi). Boşsa gradyan. Atari salonu görseli reddedildi (2026-11-28).")]
    public Sprite background;
    [Tooltip("Başlık/buton/sayı fontu (Inter ExtraBold — yerelde zaten kullanılıyor).")]
    public TMP_FontAsset font;
    [Tooltip("Gövde metni fontu (Inter SemiBold, ortak popup okunaklı fontu).")]
    public TMP_FontAsset bodyFont;
    [Tooltip("Giriş overlay'i: engel tanıtım ekranıyla aynı font (DynaPuff Bold) ve başlık materyali (kullanıcı 2026-11-28).")]
    public TMP_FontAsset introFont;
    public Material introTitleMaterial;
    [Tooltip("Kullanılmıyor (2026-11-28): koleksiyon makinenin tabelasında. Boş bırak.")]
    public Sprite shelf;
    [Tooltip("Kontrol paneli: solda jeton yuvası, sağ üstte sayaç penceresi, altta 3 lamba.")]
    public Sprite controlPanel;
    [Tooltip("Ödül bacası iki katman (aynı tuval): arka = kutu, ön = ön çubuk + cam. Oyuncak arasına düşer.")]
    public Sprite chuteBack, chuteFront;
    [Tooltip("Pençeyi taşıyan çapraz kiriş (Machine_Top'tan kesildi). 2.5D'de pençeyle geriye gider: ekranda iner, daralır.")]
    public Sprite gantry;
    [Tooltip("Kontrol okları (sağa; geriye için döndürülür). Yarı saydam tuşların içinde.")]
    public Sprite arrowIcon;
    [Tooltip("Üst HUD barı: solda altın yuvası, ortada başlık plakası, sağda süre (pati amblemi silindi — tabelayla tekrar).")]
    public Sprite topHud;
    [Tooltip("Makinenin içi (arka duvar + perspektif zemin + WONDI neon). Varsa cam şeffaf, derinlik buna göre.")]
    public Sprite interior;
    public Color backgroundTop = new Color(0.30f, 0.12f, 0.36f);
    public Color backgroundBottom = new Color(0.10f, 0.04f, 0.14f);

    public const int PlushCount = 6;
    public int ItemCount => Mathf.Clamp(itemCount, 4, 20);
    public Sprite PlushSprite(int id) => plushes != null && id >= 0 && id < plushes.Length ? plushes[id] : null;
    public ClawPrize Prize(int index) => prizes != null && index >= 0 && index < prizes.Length ? prizes[index] : null;
}

[Serializable]
public sealed class ClawPrize
{
    public DailySlotReward reward = new DailySlotReward();
    public bool big;
    [Min(0)] public int weight = 10;

    private static ClawPrize P(DailySlotRewardType type, int amount, int weight, bool big = false, int minutes = 0)
        => new ClawPrize { reward = new DailySlotReward { type = type, amount = amount, durationMinutes = minutes }, weight = weight, big = big };

    // Plan §6: altın keseleri küçük; değerli ödüller joker / can (makine altın vermekten çok harcatır).
    public static ClawPrize[] Defaults() => new[]
    {
        P(DailySlotRewardType.Coins, 5, 45),   // dolgu: küçük ama gerçek ödül (boş kapsül yok)
        P(DailySlotRewardType.Coins, 25, 15),
        P(DailySlotRewardType.Booster_Hammer, 1, 7),
        P(DailySlotRewardType.Booster_Shuffle, 1, 7),
        P(DailySlotRewardType.Booster_Row, 1, 6),
        P(DailySlotRewardType.Booster_Hammer, 1, 8, true),   // 60 altın kesesi yerine (ekonomi 2026-11-28)
        P(DailySlotRewardType.Joker_Line, 1, 6, true),
        P(DailySlotRewardType.Joker_PulseCore, 1, 5, true),
        P(DailySlotRewardType.Joker_SystemOverride, 1, 4, true),
        P(DailySlotRewardType.Lives, 1, 8, true, 30),
    };
}
