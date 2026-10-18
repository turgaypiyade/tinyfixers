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

    /// <summary>Haritayı kapat (animasyonsuz — level geçişi vb.).</summary>
    public abstract void Hide();

    // ── Daire geçişi (ikondan açılır, ikona kapanır) ──
    private EventScreenIris iris;
    private RectTransform irisOrigin;

    /// Open'ın sonunda, ekranın çalışma anında eklediği çocuklar (kalabalık, lift) kurulduktan SONRA çağır:
    /// iris ilk çağrıda kökün çocuklarını kendi kabına taşır.
    protected void PlayIrisOpen(Transform host, SafariEventController owner)
    {
        if (iris == null) iris = EventScreenIris.For((RectTransform)host);
        irisOrigin = owner != null ? owner.IconRect : null;
        iris.PlayOpen(irisOrigin);
        EventSfx.Play(x => x.arrowWhoosh);
    }

    /// Kapat düğmesi: daire ikona küçülür, sonra Hide().
    protected void HideAnimated()
    {
        if (iris == null || !isActiveAndEnabled) { Hide(); return; }
        iris.PlayClose(irisOrigin, Hide);
    }
}
