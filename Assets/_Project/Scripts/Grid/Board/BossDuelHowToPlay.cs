using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// First-duel introduction: three short illustrated steps on one page, like Bridge Repair.
public sealed class BossDuelHowToPlay : MonoBehaviour
{
    private const string SeenKey = "boss_duel_howto_seen_v1";
    public static bool ShouldShow => !RuntimeSimulationSession.IsActive && PlayerPrefs.GetInt(SeenKey, 0) == 0;
    public bool Completed { get; private set; }

    private const float RevealDuration = 1.8f;
    private RectTransform viewport, safeArea, design;
    private TMP_Text title, action;
    private readonly RectTransform[] steps = new RectTransform[3];
    private readonly Image[] arrows = new Image[2];
    private readonly Sprite[] ownedArrowSprites = new Sprite[2];
    private float revealTime;
    private bool revealComplete;
    private Rect lastSafeArea;
    private Vector2 lastViewport;

    public static BossDuelHowToPlay Show(Transform owner, Sprite playerSprite, Sprite enemySprite)
    {
        var root = new GameObject("BossDuelHowToPlay", typeof(RectTransform));
        try
        {
            SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            var canvas = root.AddComponent<Canvas>();
            // Keep the hierarchy active so TMP.Awake initializes the font/material before
            // BridgeRepairUI.Label sets outlines. Hide rendering only during construction.
            canvas.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 980;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var view = root.AddComponent<BossDuelHowToPlay>();
            view.viewport = (RectTransform)root.transform;
            view.Build(playerSprite, enemySprite);
            view.RefreshReveal();
            canvas.enabled = true;
            Canvas.ForceUpdateCanvases();
            view.RefreshLayout();
            return view;
        }
        catch
        {
            // Show has not returned yet, so the controller cannot own/clean up this root.
            root.SetActive(false);
            Destroy(root);
            throw;
        }
    }

    private void Build(Sprite playerSprite, Sprite enemySprite)
    {
        var cover = BridgeRepairUI.Solid("Cover", transform, new Color(0.02f, 0.04f, 0.1f, 1f));
        cover.raycastTarget = true;
        var tap = cover.gameObject.AddComponent<Button>();
        tap.targetGraphic = cover;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(OnTap);

        safeArea = BridgeRepairUI.Stretch("SafeArea", transform);
        design = BridgeRepairUI.Rect("Design", safeArea, new Vector2(1000f, 1800f), Vector2.zero);
        title = BridgeRepairUI.Label("Header", design, L("boss_howto_header", "BOSS DÜELLOSU"),
            80f, new Vector2(900f, 120f), new Vector2(0f, 800f));

        // Same left -> right -> left reading order as the Bridge Repair introduction.
        steps[0] = Step("Match", new Vector2(-215f, 465f),
            L("boss_howto_match", "Taşları eşleştir,\nsaldırını güçlendir!"));
        var library = TileIconLibrary.Shared;
        var tileSprite = library != null ? library.Get(TileType.Core) : null;
        for (int i = 0; i < 3; i++)
        {
            var tile = BridgeRepairUI.Picture("Tile" + i, steps[0], tileSprite,
                Vector2.one * 115f, new Vector2((i - 1) * 126f, 10f));
            if (tileSprite == null)
            {
                tile.sprite = BridgeRepairUI.Pill();
                tile.color = new Color(1f, 0.4f, 0.3f);
                tile.enabled = true;
            }
        }
        arrows[0] = Arrow(0, false, new Vector2(215f, 355f));

        steps[1] = Step("Fight", new Vector2(215f, 55f),
            L("boss_howto_exchange", "Hamlen bitince vurursun.\nRakip de karşılık verir!"));
        BridgeRepairUI.Picture("Player", steps[1], playerSprite,
            new Vector2(185f, 235f), new Vector2(-115f, 0f));
        BridgeRepairUI.Picture("Enemy", steps[1], enemySprite,
            new Vector2(185f, 235f), new Vector2(115f, 0f));
        BridgeRepairUI.Label("Versus", steps[1], "VS", 42f,
            new Vector2(85f, 65f), Vector2.zero, BridgeRepairUI.Gold);
        BridgeRepairUI.Label("PlayerTag", steps[1], L("boss_howto_you", "SEN"), 30f,
            new Vector2(205f, 50f), new Vector2(-115f, -135f), new Color(0.4f, 1f, 0.6f));
        BridgeRepairUI.Label("EnemyTag", steps[1], L("boss_howto_enemy", "RAKİP"), 30f,
            new Vector2(205f, 50f), new Vector2(115f, -135f), new Color(0.85f, 0.55f, 1f));
        arrows[1] = Arrow(1, true, new Vector2(-230f, -100f));

        steps[2] = Step("Win", new Vector2(-215f, -405f),
            L("boss_howto_finish", "Canın ve hamlen bitmeden\nrakipleri yen, hedefleri tamamla!"));
        var glow = BridgeRepairUI.Picture("VictoryGlow", steps[2], BridgeRepairUI.Glow(),
            new Vector2(410f, 310f), new Vector2(0f, 5f));
        glow.color = new Color(1f, 0.8f, 0.25f, 0.45f);
        BridgeRepairUI.Picture("Winner", steps[2], playerSprite,
            new Vector2(245f, 265f), new Vector2(0f, 5f));

        action = BridgeRepairUI.Label("Continue", design,
            L("boss_howto_start", "Düelloya başlamak için dokun"),
            52f, new Vector2(900f, 100f), new Vector2(0f, -800f), BridgeRepairUI.Gold);
    }

    private RectTransform Step(string name, Vector2 position, string caption)
    {
        var step = BridgeRepairUI.Rect(name, design, new Vector2(460f, 310f), position);
        var text = BridgeRepairUI.Label("Caption", step, caption, 44f,
            new Vector2(560f, 145f), new Vector2(0f, -220f));
        text.textWrappingMode = TextWrappingModes.Normal;
        return step;
    }

    private Image Arrow(int index, bool flip, Vector2 position)
    {
        // CurvedArrow creates a texture and sprite; this one-shot overlay owns both.
        var sprite = BridgeRepairUI.CurvedArrow(flip);
        ownedArrowSprites[index] = sprite;
        var arrow = BridgeRepairUI.Picture("Arrow" + index, design, sprite,
            new Vector2(170f, 190f), position);
        arrow.type = Image.Type.Filled;
        arrow.fillMethod = Image.FillMethod.Vertical;
        arrow.fillOrigin = (int)Image.OriginVertical.Top;
        return arrow;
    }

    private void OnTap()
    {
        if (Completed) return;
        // As in Bridge Repair, the first early tap reveals everything without dismissing it.
        if (revealTime < RevealDuration)
        {
            revealTime = RevealDuration;
            RefreshReveal();
            return;
        }
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        Completed = true;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        RefreshLayout();
        revealTime += Time.unscaledDeltaTime;
        if (!revealComplete) RefreshReveal();
        if (revealComplete)
            action.alpha = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin((revealTime - RevealDuration) * 2.6f));
    }

    private void RefreshReveal()
    {
        title.alpha = Mathf.Clamp01(revealTime / 0.25f);
        for (int i = 0; i < steps.Length; i++)
        {
            float t = Mathf.Clamp01((revealTime - 0.2f - i * 0.6f) / 0.35f);
            steps[i].localScale = Vector3.one * BridgeRepairUI.EaseOutBack(t);
        }
        for (int i = 0; i < arrows.Length; i++)
            arrows[i].fillAmount = BridgeRepairUI.Smooth01((revealTime - 0.55f - i * 0.6f) / 0.25f);
        revealComplete = revealTime >= RevealDuration;
        action.alpha = revealComplete ? 1f : 0f;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ownedArrowSprites.Length; i++)
        {
            var sprite = ownedArrowSprites[i];
            if (sprite == null) continue;
            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
            ownedArrowSprites[i] = null;
        }
    }

    private void RefreshLayout()
    {
        var area = Screen.safeArea;
        var size = viewport.rect.size;
        if (area == lastSafeArea && size == lastViewport) return;
        lastSafeArea = area;
        lastViewport = size;
        float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        safeArea.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
        safeArea.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        float usableWidth = size.x * area.width / width;
        float usableHeight = size.y * area.height / height;
        design.localScale = Vector3.one * Mathf.Min(1f, Mathf.Min(usableWidth / 1040f, usableHeight / 1840f));
    }

    private static string L(string key, string fallback) => BridgeRepairUI.L(key, fallback);
}
