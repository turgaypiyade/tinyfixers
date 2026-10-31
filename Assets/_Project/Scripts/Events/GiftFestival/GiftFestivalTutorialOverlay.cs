using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One cached spotlight, driven by the screen's shared update. No overlay coroutines.</summary>
public sealed class GiftFestivalTutorialOverlay
{
    private readonly RectTransform root, finger;
    private readonly TutorialSpotlightGraphic dim;
    private readonly TMP_Text caption;
    private readonly List<Rect> panels = new(4);
    private readonly Vector3[] corners = new Vector3[4];
    private Vector2 fingerTip;

    public GiftFestivalTutorialOverlay(RectTransform design, GiftFestivalConfig cfg)
    {
        root = GiftFestivalUI.Stretch("FirstShotTutorial", design);
        dim = GiftFestivalUI.Stretch("Spotlight", root).gameObject.AddComponent<TutorialSpotlightGraphic>();
        dim.color = new Color(0.02f, 0.04f, 0.12f, 0.78f);
        dim.raycastTarget = true;
        dim.UseRaycastHole = true;
        var panel = GiftFestivalUI.Rect("CaptionPanel", root, new Vector2(820, 170), new Vector2(0, -350))
            .gameObject.AddComponent<Image>();
        panel.color = new Color(0.035f, 0.10f, 0.22f, 0.97f);
        panel.raycastTarget = false;
        caption = GiftFestivalUI.Label("Caption", panel.transform, cfg, "", 44, new Vector2(780, 150), Vector2.zero,
            new Color(1f, 0.94f, 0.57f));
        var hand = GiftFestivalUI.Picture("Finger", root, UiIcons.Finger,
            GiftFestivalUI.FitSize(UiIcons.Finger, new Vector2(110, 120)), Vector2.zero);
        finger = hand.rectTransform;
        finger.pivot = new Vector2(0.40f, 0.03f); // Same fingertip anchor as the existing profile tutorial.
        Hide();
    }

    public void Show(RectTransform target, bool timingStep)
    {
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        target.GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]);
        Vector2 max = root.InverseTransformPoint(corners[2]);
        Rect hole = timingStep ? new Rect(-410, -895, 820, 340)
            : Rect.MinMaxRect(min.x - 12, min.y - 12, max.x + 12, max.y + 12);
        dim.RaycastHole = hole;
        Rect full = root.rect;
        panels.Clear();
        panels.Add(Rect.MinMaxRect(full.xMin, full.yMin, full.xMax, hole.yMin));
        panels.Add(Rect.MinMaxRect(full.xMin, hole.yMax, full.xMax, full.yMax));
        panels.Add(Rect.MinMaxRect(full.xMin, hole.yMin, hole.xMin, hole.yMax));
        panels.Add(Rect.MinMaxRect(hole.xMax, hole.yMin, full.xMax, hole.yMax));
        dim.SetRects(panels);
        caption.text = GiftFestivalUI.L(timingStep ? "tutorial_hit" : "tutorial_select");
        fingerTip = (min + max) * 0.5f;
        Tick(0);
    }

    public void Tick(float time)
    {
        finger.anchoredPosition = fingerTip + Vector2.up * (6f + 10f * (0.5f + 0.5f * Mathf.Sin(time * 5f)));
    }

    public void Hide() => root.gameObject.SetActive(false);
}
