using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana (yeşil) butonları canlandırır: birkaç saniyede bir üstünden soldan sağa geçen yumuşak ışık şeridi
/// + isteğe bağlı hafif "nefes alma" (büyü-küçül). Şerit butonun KENDİ görseliyle maskelenir (şekilden
/// taşmaz) ve ilk çocuk olarak eklenir → buton yazısının ALTINDA kalır.
///
/// Görsel: Resources/UI/ButtonShine.png (hazır sprite). Buton pasifken (interactable=false) ikisi de durur.
/// Buton görseli çalışma anında değişirse (tema) maske onu takip eder.
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonShine : MonoBehaviour
{
    private const string ShineSpritePath = "UI/ButtonShine";

    [Header("Parlama")]
    [SerializeField, Min(0.5f)] private float interval = 2.8f;         // iki geçiş arası (sn)
    [SerializeField, Min(0.1f)] private float sweepDuration = 0.6f;
    [SerializeField, Range(0.05f, 1f)] private float shineWidth = 0.28f; // buton genişliğine oran
    [SerializeField, Range(-45f, 45f)] private float tilt = -18f;
    [SerializeField, Range(0f, 1f)] private float shineAlpha = 0.75f;

    [Header("Nefes alma")]
    [SerializeField] private bool breathe = true;
    [SerializeField, Range(0f, 0.15f)] private float breatheAmount = 0.035f;
    [SerializeField, Min(0.3f)] private float breathePeriod = 1.6f;

    private Image target;
    private Button button;
    private Image maskImage;
    private RectTransform maskRect;
    private RectTransform shineRect;
    private Image shineImage;
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleCaptured;
    private float timer;
    private float sweepT = -1f;

    /// <summary>Kodla kurulan butonlar için: nefes almayı aç/kapa (yalnız birincil buton nefes alsın).</summary>
    public void Configure(bool breathing) => breathe = breathing;

    private void Awake()
    {
        target = GetComponent<Image>();
        button = GetComponent<Button>();
        Build();
    }

    private void OnEnable()
    {
        if (!baseScaleCaptured)
        {
            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }
        timer = Random.Range(0.4f, interval);   // yan yana butonlar aynı anda parlamasın
        sweepT = -1f;
        if (shineImage != null) shineImage.enabled = false;
    }

    private void OnDisable()
    {
        if (baseScaleCaptured) transform.localScale = baseScale;
    }

    private void Build()
    {
        if (target == null || maskRect != null) return;
        var sprite = Resources.Load<Sprite>(ShineSpritePath);
        if (sprite == null) return;

        var maskGo = new GameObject("ShineMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskGo.layer = gameObject.layer;   // Screen Space Camera culling guard
        maskRect = (RectTransform)maskGo.transform;
        maskRect.SetParent(transform, false);
        maskRect.SetAsFirstSibling();       // yazı (sonraki çocuklar) üstte kalsın
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
        maskImage = maskGo.GetComponent<Image>();
        maskImage.raycastTarget = false;
        SyncMaskShape();
        maskGo.GetComponent<Mask>().showMaskGraphic = false;

        var shineGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shineGo.layer = gameObject.layer;
        shineRect = (RectTransform)shineGo.transform;
        shineRect.SetParent(maskRect, false);
        shineRect.anchorMin = shineRect.anchorMax = new Vector2(0.5f, 0.5f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.localEulerAngles = new Vector3(0f, 0f, tilt);
        shineImage = shineGo.GetComponent<Image>();
        shineImage.sprite = sprite;
        shineImage.raycastTarget = false;
        shineImage.enabled = false;
    }

    // Maske = butonun kendi görseli (aynı sprite + aynı çizim tipi/oran) → şerit şekilden taşmaz.
    private void SyncMaskShape()
    {
        if (maskImage == null || target == null) return;
        maskImage.sprite = target.sprite;
        maskImage.type = target.type;
        maskImage.preserveAspect = target.preserveAspect;
        maskImage.color = Color.white;
    }

    private void Update()
    {
        if (target == null) return;
        if (maskImage != null && maskImage.sprite != target.sprite) SyncMaskShape();

        bool active = button == null || button.interactable;
        float dt = Time.unscaledDeltaTime;

        // Nefes alma.
        if (breathe && active && baseScaleCaptured)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / breathePeriod);
            transform.localScale = baseScale * (1f + breatheAmount * k);
        }
        else if (baseScaleCaptured)
        {
            transform.localScale = baseScale;
        }

        // Parlama şeridi.
        if (shineRect == null) return;
        if (!active)
        {
            shineImage.enabled = false;
            sweepT = -1f;
            return;
        }

        if (sweepT < 0f)
        {
            timer -= dt;
            if (timer > 0f) return;
            sweepT = 0f;
            shineImage.enabled = true;
        }

        sweepT += dt / sweepDuration;
        Rect r = maskRect.rect;
        float w = r.width * shineWidth;
        float h = r.height * 1.8f;   // eğikken köşeleri de kapsasın
        shineRect.sizeDelta = new Vector2(w, h);
        float t = Mathf.Clamp01(sweepT);
        float e = t * t * (3f - 2f * t);
        float x = Mathf.Lerp(-r.width * 0.5f - w, r.width * 0.5f + w, e);
        shineRect.anchoredPosition = new Vector2(x, 0f);
        shineImage.color = new Color(1f, 1f, 1f, shineAlpha * Mathf.Sin(t * Mathf.PI));

        if (sweepT >= 1f)
        {
            sweepT = -1f;
            timer = interval;
            shineImage.enabled = false;
        }
    }
}
