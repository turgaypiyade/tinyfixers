public enum TutorialId
{
    BasicMatch        = 0,
    LineHCreated      = 1,
    PatchBotCreated   = 2,
    LineHActivated    = 3,
    PulseCoreCreated  = 4,
    OverrideCreated   = 5,
    SpecialCombo      = 6,   // iki special yan yana → birleştir (level'da pinli hazır special'larla)
    PulseCoreActivated = 7,
    PatchBotActivated  = 8,
    OverrideActivated  = 9,
    // Joker tanıtımı (kullanıcı 2026-11-13): joker açılış level'ında bedava joker zorunlu kullandırılır.
    // fromCell = hedef hücre (satır/sütun jokerinde o hücrenin satırı/sütunu; karıştırmada yok sayılır).
    JokerHammer  = 10,
    JokerRow     = 11,
    JokerColumn  = 12,
    JokerShuffle = 13,
    JokerPaint   = 14,
}

// Sadece level-controlled combo tutorial'ları için (Group 2)
public enum ComboTutorialId
{
    None              = 0,
    LineVLineH        = 1,
    LinePulseCore     = 2,
    LinePatchBot      = 3,
    PulseCorePatchBot = 4,
    OverrideLine      = 5,
    OverridePulseCore = 6,
    OverrideOverride  = 7,
    PulsePulse        = 8,
    PatchBotPatchBot  = 9,
}
