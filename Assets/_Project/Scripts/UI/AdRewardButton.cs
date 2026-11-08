using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ödüllü reklam butonu — her reklam yerinde aynı görsel dil (karar 2026-11-08, plan: Docs/Monetization/RewardedAds_Plan.md):
/// sabit altın çerçeve + basılınca küçülen mavi ▶ disk + altta "ÜCRETSİZ" kurdelesi (yazı Unity'de, dile göre).
/// Görseller Resources/UIButtons/Ad_*. Kod içinden kurulur; sahne/prefab değişmez.
/// </summary>
public sealed class AdRewardButton : MonoBehaviour
{
    private const float DiscRatio = 459f / 533f;      // diskin çerçeveye oranı (kaynak PNG'ler aynı merkezli)
    private const float RibbonAspect = 492f / 990f;

    public Button Button { get; private set; }
    public event Action Clicked;

    private TMP_Text ribbonLabel;

    public static AdRewardButton Create(Transform parent, float size, string objectName = "AdRewardButton")
    {
        int layer = parent.gameObject.layer;   // runtime UI: ebeveyn katmanı (kamera culling)
        var root = NewChild(objectName, parent, layer);
        root.sizeDelta = new Vector2(size, size);
        var view = root.gameObject.AddComponent<AdRewardButton>();

        // Disk (basılan) → çerçeve (sabit, üstte) → kurdele (en üstte).
        var disc = NewChild("Disc", root, layer);
        disc.sizeDelta = new Vector2(size * DiscRatio, size * DiscRatio);
        var discImage = disc.gameObject.AddComponent<Image>();
        discImage.sprite = Resources.Load<Sprite>("UIButtons/Ad_PlayButton");
        discImage.preserveAspect = true;
        view.Button = disc.gameObject.AddComponent<Button>();
        view.Button.transition = Selectable.Transition.None;
        view.Button.targetGraphic = discImage;
        // Button eklenirken (kapalı CanvasGroup altında) yazılan "disabled" tint None'da hiç sıfırlanmaz → kalıcı soluk disk.
        discImage.canvasRenderer.SetColor(Color.white);
        disc.gameObject.AddComponent<PressScaleButton>();
        view.Button.onClick.AddListener(() => view.Clicked?.Invoke());

        var frame = NewChild("Frame", root, layer);
        frame.sizeDelta = new Vector2(size, size);
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = Resources.Load<Sprite>("UIButtons/Ad_PlayFrame");
        frameImage.preserveAspect = true;
        frameImage.raycastTarget = false;

        float ribbonWidth = size * 1.18f;
        var ribbon = NewChild("Ribbon", root, layer);
        ribbon.sizeDelta = new Vector2(ribbonWidth, ribbonWidth * RibbonAspect);
        ribbon.anchoredPosition = new Vector2(0f, -size * 0.40f);
        var ribbonImage = ribbon.gameObject.AddComponent<Image>();
        ribbonImage.sprite = Resources.Load<Sprite>("UIButtons/Ad_Ribbon");
        ribbonImage.preserveAspect = true;
        ribbonImage.raycastTarget = false;

        // Yazı: kurdelenin orta bandına oturur (Ad_Ribbon ölçüsü: band merkezi alttan %57, kalınlık %45,
        // sağa ~9.5° yükselir) — Pop + kırmızı zemin konturu.
        var label = NewChild("Label", ribbon, layer);
        label.anchorMin = new Vector2(0.20f, 0.39f);
        label.anchorMax = new Vector2(0.80f, 0.75f);
        label.sizeDelta = Vector2.zero;
        label.localEulerAngles = new Vector3(0f, 0f, 9.5f);
        var text = label.gameObject.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMax = size * 0.27f;   // kısa "FREE" bandın üst kenarına değmesin
        text.fontSizeMin = size * 0.10f;
        TextSurfaceStyle.Apply(text, TextSurface.Red);
        view.ribbonLabel = text;
        view.RefreshLabel();
        return view;
    }

    private static RectTransform NewChild(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += RefreshLabel;
        RefreshLabel();
    }

    private void OnDisable() => GameLocalization.OnLanguageChanged -= RefreshLabel;

    private void RefreshLabel()
    {
        if (ribbonLabel != null) ribbonLabel.text = GameLocalization.GetOr("ad_free_ribbon", "ÜCRETSİZ");
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
