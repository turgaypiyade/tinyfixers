// Değerler kayıtlı veriye (asset + sunucu JSON'u adla) bağlı — sıra/numara DEĞİŞTİRME, yalnız sona ekle.
public enum ProgressGoalType
{
    LineH,              // Yatay çizgi (LineEmitter_H tile aktive edildi)
    LineV,              // Dikey çizgi (LineEmitter_V tile aktive edildi)
    LineAny,            // Yatay veya dikey çizgi

    TileCleared_Gear,   // Sarı taş kır
    TileCleared_Core,   // Kırmızı taş kır
    TileCleared_Bolt,   // Mavi taş kır
    TileCleared_Plate,  // Yeşil taş kır
    TileCleared_Any,    // Herhangi renkli taş kır

    ObstacleCleared,    // Engel kır (tamamen kalkan her engel 1)
    SpecialCreated,     // Eşleşmeden special oluştur (her tür)
    LevelWon,           // Level kazan
    FirstTryWin,        // Level'ı ilk denemede kazan
}
