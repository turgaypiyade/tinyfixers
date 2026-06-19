using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Harita üzerinde bir bölge. Toplam yıldız >= unlockStars olunca sisi (fog) açılır.
/// Şimdilik sadece bölge açma (level girişi yok). Bina/dekor ayrı; bu sadece sisi yönetir.
/// </summary>
public sealed class WorldMapRegion : MonoBehaviour
{
    [Tooltip("Bu bölge kaç TOPLAM yıldızda açılır.")]
    [SerializeField, Min(0)] private int unlockStars;
    [Tooltip("Bölgeyi örten sis/bulut. CanvasGroup'lu bir obje; açılınca fade-out olur.")]
    [SerializeField] private CanvasGroup fog;
    [Tooltip("Kilitliyken sis opaklığı. 1 = tam kapalı, 0.6-0.8 = altındaki siluet hafif görünür.")]
    [SerializeField, Range(0f, 1f)] private float lockedFogAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.6f;

    [Header("Pin (opsiyonel)")]
    [Tooltip("Bölge pini. Sisin DIŞINDA olmalı (fade olmasın).")]
    [SerializeField] private Image pin;
    [SerializeField] private Sprite lockedPinSprite;
    [SerializeField] private Sprite unlockedPinSprite;

    public int UnlockStars => unlockStars;

    private Coroutine fadeCo;

    public void Apply(int totalStars, bool animate)
    {
        bool revealed = totalStars >= unlockStars;

        // Pin görseli (sisten bağımsız, hep görünür).
        if (pin != null)
        {
            var s = revealed ? unlockedPinSprite : lockedPinSprite;
            if (s != null) pin.sprite = s;
        }

        if (fog == null) return;

        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        if (!revealed)
        {
            // Kilitli: sis kısmen saydam (siluet görünür).
            fog.gameObject.SetActive(true);
            fog.alpha = lockedFogAlpha;
            return;
        }

        // Zaten gizliyse (alpha 0 / kapalı) animasyona gerek yok — direkt kapalı bırak.
        bool alreadyHidden = !fog.gameObject.activeSelf || fog.alpha <= 0.001f;

        if (!animate || alreadyHidden)
        {
            fog.alpha = 0f;
            fog.gameObject.SetActive(false);
            return;
        }

        // Görünürken açıldı → fade-out (sis objesi şu an aktif, coroutine güvenli).
        fadeCo = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        fog.gameObject.SetActive(true);
        float start = fog.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fog.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }
        fog.alpha = 0f;
        fog.gameObject.SetActive(false);
        fadeCo = null;
    }
}
