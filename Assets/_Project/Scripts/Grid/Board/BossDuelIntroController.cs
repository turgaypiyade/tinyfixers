using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Boss-duel açılış animasyonu: oyuncunun atadığı iki parça (sol + sağ) ekran dışından
/// gelip ortada — sahnede yerleştirildikleri pozisyonda — birleşir, kısa süre bekler,
/// sonra overlay fade-out olur ve oyun açılır.
///
/// Parçalar pozunu/oryantasyonunu DEĞİŞTİRMEZ; yalnızca yatay kayar. Parçaları sahnede
/// BİRLEŞECEKLERİ konuma yerleştir — o anchoredPosition hedef olarak kullanılır.
public sealed class BossDuelIntroController : MonoBehaviour
{
    [Header("Overlay")]
    [Tooltip("Intro panelinin CanvasGroup'u — başta gizli, animasyon başında açılır, sonunda fade-out olur. Opak bir background içeriyorsa board intro boyunca gizlenir.")]
    [SerializeField] private CanvasGroup introRoot;

    [Header("Parçalar (sahnede BİRLEŞECEKLERİ yere yerleştir)")]
    [Tooltip("Soldan gelen parça. Sahnedeki anchoredPosition'ı = ortada birleşeceği hedef.")]
    [SerializeField] private RectTransform leftPiece;
    [Tooltip("Sağdan gelen parça. Sahnedeki anchoredPosition'ı = ortada birleşeceği hedef.")]
    [SerializeField] private RectTransform rightPiece;

    [Header("Zamanlama")]
    [SerializeField, Min(0.05f)] private float slideInDuration = 0.5f;
    [SerializeField, Min(0f)]    private float holdDuration    = 0.7f;
    [SerializeField, Min(0f)]    private float exitDuration    = 0.35f;

    [Header("Giriş mesafesi")]
    [Tooltip("Parçaların ekran dışından başlama ek payı (px). Canvas yarı-genişliğine + parça genişliğine eklenir, böylece her hedef için tam ekran dışından başlar.")]
    [SerializeField, Min(0f)] private float offscreenMargin = 150f;

    [Header("Birleşme vurgusu (opsiyonel)")]
    [Tooltip("Birleşme anında kısa scale punch. 1 = kapalı (parçalar hiç poz değiştirmez).")]
    [SerializeField, Min(1f)] private float meetPunchScale    = 1f;
    [SerializeField, Min(0f)] private float meetPunchDuration = 0.12f;

    [Header("Akış")]
    [Tooltip("Açık ise overlay sahne açılır açılmaz OPAK durur (board'u örter). LoadingScreen (sortingOrder 999) bunu transition boyunca gizler; boss değilse BossDuelController hemen kapatır. Loading screen kalkarken board 'flash' ediyorsa aç. NOT: açıkken intro mutlaka BossDuelController'a bağlı olmalı, yoksa boss-dışı levelda overlay takılı kalır.")]
    [SerializeField] private bool coverFromStart = false;

    public bool HasIntro => introRoot != null && leftPiece != null && rightPiece != null;

    private Vector2 _leftMeet, _rightMeet;
    private bool    _captured;

    private void Awake()
    {
        if (introRoot == null) return;

        if (coverFromStart && HasIntro)
        {
            // Board'u baştan ört: parçaları ekran dışında beklet, panel opak.
            introRoot.gameObject.SetActive(true);
            introRoot.alpha = 1f;
            introRoot.blocksRaycasts = true;
            ParkPiecesOffscreen();
        }
        else
        {
            introRoot.alpha = 0f;
            introRoot.blocksRaycasts = false;
            introRoot.gameObject.SetActive(false);
        }
    }

    /// Intro olmayan (boss-dışı) levellarda overlay'i anında kaldırır.
    public void HideImmediate()
    {
        if (introRoot == null) return;
        introRoot.alpha = 0f;
        introRoot.blocksRaycasts = false;
        introRoot.gameObject.SetActive(false);
    }

    /// Sahnede yerleştirilen (birleşme) pozisyonlarını bir kez yakalar — parçalar ekran
    /// dışına alınmadan ÖNCE çağrılmalı.
    private void CaptureMeetPositions()
    {
        if (_captured || leftPiece == null || rightPiece == null) return;
        _leftMeet  = leftPiece.anchoredPosition;
        _rightMeet = rightPiece.anchoredPosition;
        _captured  = true;
    }

    private void ParkPiecesOffscreen()
    {
        CaptureMeetPositions();
        float travel = GetCanvasWidth() * 0.5f + offscreenMargin;
        leftPiece.anchoredPosition  = _leftMeet  + new Vector2(-(travel + leftPiece.rect.width),  0f);
        rightPiece.anchoredPosition = _rightMeet + new Vector2( (travel + rightPiece.rect.width), 0f);
    }

    /// Sol/sağ parçaları ekran dışından getirip ortada birleştirir, bekler, overlay'i temizler.
    public IEnumerator Play()
    {
        if (!HasIntro) yield break;

        ParkPiecesOffscreen();
        Vector2 leftStart  = leftPiece.anchoredPosition;
        Vector2 rightStart = rightPiece.anchoredPosition;

        introRoot.gameObject.SetActive(true);
        introRoot.alpha = 1f;
        introRoot.blocksRaycasts = true;

        // Kayarak ortada birleş (ease-out quad — controller ile aynı his).
        float t = 0f;
        while (t < slideInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / slideInDuration);
            float e = 1f - (1f - k) * (1f - k);
            leftPiece.anchoredPosition  = Vector2.LerpUnclamped(leftStart,  _leftMeet,  e);
            rightPiece.anchoredPosition = Vector2.LerpUnclamped(rightStart, _rightMeet, e);
            yield return null;
        }
        leftPiece.anchoredPosition  = _leftMeet;
        rightPiece.anchoredPosition = _rightMeet;

        if (meetPunchScale > 1f && meetPunchDuration > 0f)
            yield return MeetPunch();

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // Çık (fade-out) → oyun açılır.
        t = 0f;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            introRoot.alpha = 1f - Mathf.Clamp01(t / exitDuration);
            yield return null;
        }

        introRoot.alpha = 0f;
        introRoot.blocksRaycasts = false;
        introRoot.gameObject.SetActive(false);
    }

    private IEnumerator MeetPunch()
    {
        Vector3 baseL = leftPiece.localScale,  peakL = baseL * meetPunchScale;
        Vector3 baseR = rightPiece.localScale, peakR = baseR * meetPunchScale;

        float half = meetPunchDuration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            leftPiece.localScale  = Vector3.LerpUnclamped(baseL, peakL, k);
            rightPiece.localScale = Vector3.LerpUnclamped(baseR, peakR, k);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            leftPiece.localScale  = Vector3.LerpUnclamped(peakL, baseL, k);
            rightPiece.localScale = Vector3.LerpUnclamped(peakR, baseR, k);
            yield return null;
        }
        leftPiece.localScale  = baseL;
        rightPiece.localScale = baseR;
    }

    private float GetCanvasWidth()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.transform is RectTransform crt && crt.rect.width > 0f)
            return crt.rect.width;
        return Screen.width;
    }
}
