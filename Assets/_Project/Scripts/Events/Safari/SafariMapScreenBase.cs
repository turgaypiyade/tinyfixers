using UnityEngine;

/// <summary>
/// Event harita-overlay ekranlarının ortak tabanı. <see cref="SafariEventController"/> yalnız bu tip
/// üzerinden konuşur; böylece aynı backend (state/schedule/pool) farklı sunumlarla sürülebilir:
///  - <see cref="SafariMapScreen"/> — yatay yarış (pitstop + uçurum).
///  - <c>RisingMapScreen</c>     — dikey asansör (kule katları + scissor kaldıraç).
/// </summary>
public abstract class SafariMapScreenBase : MonoBehaviour
{
    /// <summary>Haritayı aç ve dönüş sonucunu (varsa) anime et.</summary>
    public abstract void Open(SafariEventController owner, SafariRoundOutcome outcome);

    /// <summary>Haritayı kapat.</summary>
    public abstract void Hide();
}
