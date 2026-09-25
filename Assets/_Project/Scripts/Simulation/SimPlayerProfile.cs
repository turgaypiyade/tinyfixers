/// <summary>
/// Oyuncu modeli. Eski sim'de tek bir "mistakeChance" vardı — ya kusursuz greedy ya da zar.
/// Gerçek oyuncu ikisi de değil: iyi hamleleri ÇOĞUNLUKLA bulur, bazılarını gözden kaçırır,
/// arada saçmalar ve nadiren iki hamle ileri bakar. Bu profil o üç ekseni ayrı ayrı modeller.
/// </summary>
public sealed class SimPlayerProfile
{
    public string Name = "Average";

    /// <summary>Kaç hamle ileri bakar. 1 = bu hamlenin gerçek sonucu, 2 = devamını da dener.</summary>
    public int Depth = 1;

    /// <summary>Depth 2'de kaç aday derinleştirilir (ışın genişliği). Maliyeti bu belirler.</summary>
    public int BeamWidth = 5;

    /// <summary>Hiç düşünmeden rastgele oynama olasılığı (dikkat dağınıklığı).</summary>
    public float BlunderChance = 0.10f;

    /// <summary>Her adayı gözden kaçırma olasılığı — "o hamleyi görmedim".</summary>
    public float MissChance = 0.18f;

    /// <summary>Softmax sıcaklığı: 0 = hep en iyi, büyüdükçe iyi-ama-en-iyi-değil hamleler de seçilir.</summary>
    public float Temperature = 0.25f;

    /// <summary>Tahtada special biriktirme eğilimi (1 = normal). Uzman oyuncu combo için bekler.</summary>
    public float SpecialPatience = 1f;

    /// <summary>
    /// Hedefe NİŞAN ALMA becerisi (0..1). Bot, hedefe kısmi hasar veren hamleyi hesapla bulabilir;
    /// gerçek oyuncu bunu ancak kısmen görür. Özellikle hedefi tahtanın KÖŞESİNDE / dar kanalda olan
    /// levellarda fark büyüktür: bot her hamlede o dar match'i kurar, insan kuramaz. 1 = hedef hasarına tam ağırlık, düşük değer = "hedefi gözetir ama çoğunlukla genel iyi hamleyi oynar".
    /// Hedefi TAMAMLAYAN hamlenin değeri bundan etkilenmez — onu herkes görür.
    /// </summary>
    public float GoalFocus = 1f;

    public SimPlayerProfile Snapshot() => (SimPlayerProfile)MemberwiseClone();

    public static SimPlayerProfile Novice => new()
    {
        Name = "Novice", Depth = 1, BlunderChance = 0.34f, MissChance = 0.40f,
        Temperature = 0.75f, SpecialPatience = 0.3f, GoalFocus = 0.20f
    };

    public static SimPlayerProfile Average => new()
    {
        Name = "Average", Depth = 1, BlunderChance = 0.10f, MissChance = 0.18f,
        Temperature = 0.25f, SpecialPatience = 1f, GoalFocus = 0.50f
    };

    public static SimPlayerProfile Expert => new()
    {
        Name = "Expert", Depth = 2, BeamWidth = 5, BlunderChance = 0.02f, MissChance = 0.05f,
        Temperature = 0.08f, SpecialPatience = 1.6f, GoalFocus = 0.85f
    };

    /// <summary>Hatasız seçimli, sınırlı derinlikli bot; çözülebilirlik kanıtı değildir.</summary>
    public static SimPlayerProfile Perfect => new()
    {
        Name = "Perfect", Depth = 2, BeamWidth = 8, BlunderChance = 0f, MissChance = 0f,
        Temperature = 0f, SpecialPatience = 1.6f, GoalFocus = 1f
    };

    public static SimPlayerProfile ByName(string name) => (name ?? "").ToLowerInvariant() switch
    {
        "novice"  => Novice,
        "expert"  => Expert,
        "perfect" => Perfect,
        _         => Average
    };
}
