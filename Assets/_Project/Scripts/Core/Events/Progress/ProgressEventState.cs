public enum ProgressEventState
{
    Scheduled,  // Henüz başlamadı (ileride sunucu zamanlama için)
    Active,     // Sayıyor, ödüller kazanılabilir
    Ended,      // Süre doldu
}
