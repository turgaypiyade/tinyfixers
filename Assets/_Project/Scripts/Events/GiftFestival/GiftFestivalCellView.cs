using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One of sixteen reusable cells. No instantiate/destroy during a shot or board change.</summary>
public sealed class GiftFestivalCellView
{
    public readonly RectTransform Root;
    public readonly Button Button;
    private readonly Image glass, gift, reward;
    private readonly Image[] corners = new Image[4];
    private readonly TMP_Text amount;
    private readonly Color tint;
    private static readonly Color[] Palette =
    {
        new(1f, 0.16f, 0.24f), new(0.06f, 0.46f, 1f), new(0.04f, 0.85f, 0.27f), new(0.63f, 0.13f, 1f),
        new(1f, 0.72f, 0.04f), new(0.04f, 0.82f, 0.94f), new(1f, 0.36f, 0.04f), new(0.97f, 0.08f, 0.65f),
    };

    public GiftFestivalCellView(Transform parent, GiftFestivalConfig cfg, int index, System.Action<int> selected)
    {
        Root = GiftFestivalUI.Rect("Gift" + index, parent, Vector2.one * 100, Vector2.zero);
        tint = Palette[index % Palette.Length];
        glass = GiftFestivalUI.Picture("Glass", Root, cfg.Art.glass, Vector2.one * 100, Vector2.zero);
        glass.color = tint;
        Button = GiftFestivalUI.Button(glass, () => selected(index));
        Button.transition = Selectable.Transition.None;
        gift = GiftFestivalUI.Picture("GiftSymbol", Root, cfg.Art.giftSymbol, new Vector2(72, 76), Vector2.zero);
        gift.color = new Color(tint.r * 0.48f, tint.g * 0.48f, tint.b * 0.48f, 0.9f);
        for (int i = 0; i < 4; i++)
        {
            var sprite = cfg.Art.metalCorners != null && i < cfg.Art.metalCorners.Length ? cfg.Art.metalCorners[i] : null;
            corners[i] = GiftFestivalUI.Picture("MetalCorner" + i, Root, sprite, Vector2.one * 16, Vector2.zero);
        }
        var icon = TileIconLibrary.Shared != null ? TileIconLibrary.Shared.GetRewardIcon(DailySlotRewardType.Coins) : null;
        reward = GiftFestivalUI.Picture("Reward", Root, icon, Vector2.one * 50, new Vector2(0, 10));
        amount = GiftFestivalUI.Label("Amount", Root, cfg, "", 22, new Vector2(90, 29), new Vector2(0, -29));
        SetOpen(false, null);
    }

    public void Place(Vector2 pos, float size)
    {
        Root.anchoredPosition = pos;
        Root.localScale = Vector3.one * (size / 100f);
        for (int i = 0; i < 4; i++)
            corners[i].rectTransform.anchoredPosition = new Vector2(i % 2 == 0 ? -43 : 43, i < 2 ? 43 : -43);
    }

    public void SetOpen(bool opened, DailySlotReward prize)
    {
        glass.color = opened ? new Color(tint.r, tint.g, tint.b, 0.13f) : tint;
        gift.gameObject.SetActive(!opened);
        for (int i = 0; i < corners.Length; i++) corners[i].color = new Color(1, 1, 1, opened ? 0.3f : 1f);
        bool empty = GiftFestivalRewards.IsEmpty(prize);
        reward.sprite = opened && !empty ? prize.ResolveIcon() : null;
        reward.gameObject.SetActive(opened && reward.sprite != null);
        amount.gameObject.SetActive(opened);
        if (opened) amount.text = empty ? "—" : prize.type == DailySlotRewardType.Coins || prize.type == DailySlotRewardType.Stars
            ? "+" + prize.amount : prize.AmountLabel;
        Button.interactable = !opened;
    }
}
