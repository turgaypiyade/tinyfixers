using UnityEngine;

/// <summary>
/// UI elemanının "ev" anchoredPosition'ını Awake'te bir kez yakalar; başka bir şey
/// pozisyonu değiştirirse LateUpdate'te GERİ ALIR ve faili teşhis için loglar
/// (frame, sapma, aktiflik). Board home-pos drift dersinin UI karşılığı:
/// pozisyonu canlı okuyan/yazan hatalı bir akış varsa hem belirti düzelir hem
/// Console'daki uyarı kaymanın TAM ANINI gösterir → kök sebep oradan bulunur.
///
/// Layout group'un yönettiği çocuklara TAKMA (layout zaten pozisyonu sahiplenir);
/// panelin kendisine tak.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class RectHomePositionGuard : MonoBehaviour
{
    [Tooltip("Bu mesafeden (px) küçük sapmalar yok sayılır.")]
    [SerializeField, Min(0.01f)] private float tolerance = 0.5f;

    [Tooltip("Sapma tespitinde Console'a uyarı yaz (kök sebep avı için).")]
    [SerializeField] private bool logOnDrift = true;

    private RectTransform rt;
    private Vector2 home;
    private bool captured;

    private void Awake()
    {
        rt = (RectTransform)transform;
        home = rt.anchoredPosition;
        captured = true;
    }

    /// <summary>Ev pozisyonunu bilerek değiştirmek istersen (örn. tasarım güncellemesi) çağır.</summary>
    public void RecaptureHome()
    {
        if (rt == null) rt = (RectTransform)transform;
        home = rt.anchoredPosition;
        captured = true;
    }

    private void LateUpdate()
    {
        if (!captured) return;

        Vector2 current = rt.anchoredPosition;
        if ((current - home).sqrMagnitude <= tolerance * tolerance)
            return;

        if (logOnDrift)
            Debug.LogWarning(
                $"[RectHomeGuard] '{name}' pozisyonu kaydı: {current} (ev {home}, frame {Time.frameCount}) — geri alındı. " +
                "Bu uyarının zamanı kaymayı tetikleyen akışı gösterir.", this);

        rt.anchoredPosition = home;
    }
}
