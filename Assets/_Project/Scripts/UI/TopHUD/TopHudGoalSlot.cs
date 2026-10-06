using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopHudGoalSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject completedCheck;
    [SerializeField] private Vector2 defaultIconSize = new Vector2(70f, 70f);
    [SerializeField] private Vector2 largeIconSize = new Vector2(95f, 95f);

    private static readonly Color CountOutline = new Color(0.20f, 0.08f, 0.10f, 1f);

    /// <summary>
    /// YALNIZ oyun içi HUD: büyük ikon + net, konturlu sayı. Prefab (GoalSlot) Level öncesi pencerede de
    /// kullanıldığı için prefab değerleri değişmez; HUD bunu Setup'tan sonra çağırır.
    /// </summary>
    public void ApplyHudStyle(Vector2 iconSize, float countFontSize)
    {
        ApplyIconSize(iconSize);
        if (countText == null) return;
        countText.fontSize = countFontSize;
        if (countText.enableAutoSizing) countText.fontSizeMax = countFontSize;
        // Fontun yumuşak konturlu (buğulu) varsayılan materyali yerine net materyal + keskin koyu kontur.
        CrispTextMaterial.Apply(countText);
        TmpOutline.Apply(countText, 0.28f, CountOutline);
    }

    public void Setup(Sprite sprite, int remaining, bool useLargeIcon = false)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            ApplyIconSize(useLargeIcon ? largeIconSize : defaultIconSize);
        }

        SetRemaining(remaining);
    }

    private void ApplyIconSize(Vector2 size)
    {
        if (icon == null)
            return;

        RectTransform iconRt = icon.rectTransform;
        if (iconRt == null)
            return;

        iconRt.sizeDelta = new Vector2(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y));
    }

    public void SetRemaining(int remaining)
    {
        bool completed = remaining <= 0;

        if (countText != null)
        {
            countText.gameObject.SetActive(!completed);
            countText.text = Mathf.Max(0, remaining).ToString();
        }

        if (completedCheck != null)
            completedCheck.SetActive(completed);
    }

    public RectTransform IconRectTransform
    {
        get
        {
            if (icon != null) return icon.rectTransform;
            return transform as RectTransform;
        }
    }

    public Sprite IconSprite => icon != null ? icon.sprite : null;
}
