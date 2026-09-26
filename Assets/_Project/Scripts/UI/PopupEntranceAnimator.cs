using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tüm popup'ların ortak giriş/çıkış animasyonu — KAYARAK:
///   giriş  — karartma 0→hedef alfa; panel ekranın üstünden aşağı kayar, hafif taşıp yerine oturur
///            (ease-out-back) + fade; butonlar sırayla pop.
///   çıkış  — panel aşağı doğru kısa kayıp söner, karartma kalkar; bitince onDone.
/// Ölçek değil POZİSYON animasyonu: prefab popup'ların çoğu stretch/stretch olduğu için ölçek
/// büyüme/küçülme doğru görünmüyordu; anchoredPosition kaydırması her anchor düzeninde çalışır.
///
/// Kullanım: <see cref="Enter"/> / <see cref="Exit"/> (bileşen yoksa eklenir). Alfa'yı kendisi
/// yöneten controller'lar animateAlpha=false geçer.
/// </summary>
public enum PopupSlideFrom { Top, Right }

public sealed class PopupEntranceAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private Graphic dim;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool animateAlpha = true;
    [Tooltip("Top: yukarıdan iner. Right: ekranı yatayda kaplayan popup'lar (fail) sağdan girer.")]
    [SerializeField] private PopupSlideFrom slideFrom = PopupSlideFrom.Top;

    [Header("Giriş")]
    [SerializeField, Min(0f)] private float dimDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.42f;
    [Tooltip("Başlangıç ofseti = üst canvas yüksekliği × bu oran (1 = tam ekran yukarıdan).")]
    [SerializeField, Min(0f)] private float slideDistance = 0.75f;
    [Tooltip("Ease-out-back taşma miktarı (yerine otururken aşağı sarkma).")]
    [SerializeField] private float overshoot = 1.25f;
    [SerializeField] private bool popButtons = true;
    [SerializeField, Min(0f)] private float buttonsDelay = 0.22f;
    [SerializeField, Min(0f)] private float buttonStagger = 0.05f;
    [SerializeField, Min(0.01f)] private float buttonDuration = 0.22f;

    [Header("Çıkış")]
    [SerializeField, Min(0.01f)] private float exitDuration = 0.18f;
    [SerializeField, Min(0f)] private float exitDistance = 0.12f;

    // CommonPopupView.Fit ile uyum için korunur (kaydırma modelinde her zaman 1).
    public float ScaleFactor => 1f;
    public bool ScaleDrivenExternally { get; set; }
    // Sahneye gömülü, SetActive ile yeniden açılan popup'lar her açılışta oynasın.
    public bool PlayOnEnable { get => playOnEnable; set => playOnEnable = value; }

    private CanvasGroup panelGroup;
    private float dimTargetAlpha = -1f;
    private Vector2 panelHome;
    private bool homeCaptured;
    private bool animating;
    private Coroutine routine;
    private readonly List<(RectTransform rt, Vector3 baseScale)> buttons = new();

    public static PopupEntranceAnimator Enter(RectTransform panel, Graphic dim = null, bool animateAlpha = true,
        PopupSlideFrom from = PopupSlideFrom.Top)
    {
        var anim = For(panel);
        if (anim == null) return null;
        anim.animateAlpha = animateAlpha;
        anim.slideFrom = from;
        if (dim != null) anim.dim = dim;
        anim.PlayEnter();
        return anim;
    }

    public static void Exit(RectTransform panel, Action onDone)
    {
        var anim = panel != null ? panel.GetComponent<PopupEntranceAnimator>() : null;
        if (anim == null) { onDone?.Invoke(); return; }
        anim.PlayExit(onDone);
    }

    private static PopupEntranceAnimator For(RectTransform panel)
    {
        if (panel == null) return null;
        if (!panel.TryGetComponent(out PopupEntranceAnimator anim))
        {
            anim = panel.gameObject.AddComponent<PopupEntranceAnimator>();
            anim.panel = panel;
        }
        return anim;
    }

    public void Configure(RectTransform panelRect, Graphic dimGraphic)
    {
        panel = panelRect;
        dim = dimGraphic;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            PlayEnter();
    }

    private void OnDisable()
    {
        // Yarım kalan animasyon bir sonraki açılışta ara değerle başlamasın.
        if (routine != null) StopCoroutine(routine);
        routine = null;
        RestoreFinalState();
    }

    public void PlayEnter()
    {
        if (panel == null) panel = transform as RectTransform;
        if (panel == null || !isActiveAndEnabled) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CoEnter());
    }

    /// Çıkış animasyonu; bitince onDone. Etkileşim hemen kapanır (çift tık yok).
    public void PlayExit(Action onDone)
    {
        if (panel == null || !isActiveAndEnabled)
        {
            onDone?.Invoke();
            return;
        }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CoExit(onDone));
    }

    private void CaptureHome()
    {
        // Ev pozisyonu yalnız animasyon DIŞINDA yakalanır (araya giren Enter ara değeri ev sanmasın).
        if (!animating || !homeCaptured)
        {
            panelHome = panel.anchoredPosition;
            homeCaptured = true;
        }
        if (dim != null && dimTargetAlpha < 0f)
            dimTargetAlpha = dim.color.a;
        if (animateAlpha && panelGroup == null && !panel.TryGetComponent(out panelGroup))
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
    }

    private void CaptureButtons()
    {
        buttons.Clear();
        if (!popButtons) return;
        foreach (var b in panel.GetComponentsInChildren<Button>(false))
        {
            // Başlıktaki kapatma X'i panel ile gelir; yalnız içerik/aksiyon butonları sıralı pop yapar.
            if (b.name == "BtnClose") continue;
            var rt = (RectTransform)b.transform;
            buttons.Add((rt, rt.localScale));
        }
    }

    // Kayma yönü (giriş ofsetinin işareti) ve eksendeki üst canvas boyu.
    private Vector2 SlideDirection => slideFrom == PopupSlideFrom.Right ? Vector2.right : Vector2.up;

    private float TravelLength()
    {
        var parentRt = panel.parent as RectTransform;
        bool horizontal = slideFrom == PopupSlideFrom.Right;
        float len = parentRt != null ? (horizontal ? parentRt.rect.width : parentRt.rect.height) : 0f;
        return len > 1f ? len : (horizontal ? 1080f : 1920f);
    }

    // Yatay giriş ekranı tam kaplayan panel için: tamamen ekran dışından başlar.
    private float EnterDistance() => TravelLength() * (slideFrom == PopupSlideFrom.Right ? 1f : slideDistance);

    private IEnumerator CoEnter()
    {
        CaptureHome();
        animating = true;
        float distance = EnterDistance();

        // Koddan kurulan popup'ta bileşen butonlar eklenmeden başlar: bu kare gizle, butonları bir
        // kare sonra topla. Tam hâl bir kare bile görünmez.
        SetDimAlpha(0f);
        ApplyPanel(distance, 0f);
        yield return null;

        CaptureButtons();
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].rt.localScale = Vector3.zero;
        if (panelGroup != null)
        {
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        float total = Mathf.Max(dimDuration, slideDuration,
            buttonsDelay + Mathf.Max(0, buttons.Count - 1) * buttonStagger + buttonDuration);
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            SetDimAlpha(dimTargetAlpha * Mathf.Clamp01(dimDuration > 0f ? t / dimDuration : 1f));

            float k = Mathf.Clamp01(t / slideDuration);
            ApplyPanel(distance * (1f - EaseOutBack(k, overshoot)), Mathf.Clamp01(k * 3f));

            for (int i = 0; i < buttons.Count; i++)
            {
                float kb = Mathf.Clamp01((t - buttonsDelay - i * buttonStagger) / buttonDuration);
                buttons[i].rt.localScale = buttons[i].baseScale * Mathf.Max(0f, EaseOutBack(kb, 1.7f));
            }
            yield return null;
        }

        RestoreFinalState();
        routine = null;
    }

    private IEnumerator CoExit(Action onDone)
    {
        CaptureHome();
        animating = true;
        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        float distance = TravelLength() * exitDistance;
        float t = 0f;
        while (t < exitDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / exitDuration);
            float e = k * k;
            ApplyPanel(-distance * e, 1f - e);
            SetDimAlpha(dimTargetAlpha * (1f - e));
            yield return null;
        }

        routine = null;
        RestoreFinalState();
        // Kapanan panel bir sonraki açılışa kadar görünmez kalsın (controller SetActive(false) yapar).
        if (panelGroup != null) panelGroup.alpha = 0f;
        onDone?.Invoke();
    }

    private void RestoreFinalState()
    {
        if (panel != null && homeCaptured)
            panel.anchoredPosition = panelHome;
        if (panelGroup != null)
            panelGroup.alpha = 1f;
        SetDimAlpha(dimTargetAlpha);
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].rt != null)
                buttons[i].rt.localScale = buttons[i].baseScale;
        animating = false;
    }

    private void ApplyPanel(float offset, float alpha)
    {
        panel.anchoredPosition = panelHome + SlideDirection * offset;
        if (panelGroup != null)
            panelGroup.alpha = alpha;
    }

    private void SetDimAlpha(float a)
    {
        if (dim == null || a < 0f) return;
        var c = dim.color;
        c.a = a;
        dim.color = c;
    }

    private static float EaseOutBack(float k, float s)
    {
        float p = k - 1f;
        return 1f + p * p * ((s + 1f) * p + s);
    }
}
