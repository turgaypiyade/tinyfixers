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
