using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopHudGoalSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject completedCheck;

    public void Setup(Sprite sprite, int remaining)
    {
        if (icon != null)
            icon.sprite = sprite;

        SetRemaining(remaining);
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
