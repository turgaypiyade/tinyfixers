using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Full-screen artwork layers. Character PNGs retain their authored transparent margins.
public sealed class BossDuelIntroArtwork : MonoBehaviour
{
    private RectTransform viewport, playerRect, enemyRect, versus;
    private RectTransform leftMotion, rightMotion, playerLayer, enemyLayer;
    private Image playerImage, enemyImage;
    private Sprite[] players, enemies;
    private Vector2 lastSize;

    public static BossDuelIntroController Create(Transform owner = null, int sortingOrder = 950)
    {
        const string path = "BossDuelIntro/";
        var blue = Resources.Load<Sprite>(path + "BGL");
        var red = Resources.Load<Sprite>(path + "BGB");
        var bear = Resources.Load<Sprite>(path + "BT1");
        var ram = Resources.Load<Sprite>(path + "RT");
        var hyena = Resources.Load<Sprite>(path + "HB1");
        var badger = Resources.Load<Sprite>(path + "PB1");
        if (!blue || !red || !bear || !ram || !hyena || !badger)
        {
            Debug.LogWarning("[BossDuelIntro] Missing artwork in Resources/BossDuelIntro.");
            return null;
        }

        var root = new GameObject("BossDuelIntro", typeof(RectTransform));
        root.SetActive(false);
        // A scene-root canvas is essential: nesting under Battlefield's HUD canvas
        // would inherit its safe-area rectangle and leave gaps at the bottom/right.
        if (owner != null)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var group = root.AddComponent<CanvasGroup>();
        var art = root.AddComponent<BossDuelIntroArtwork>();
        art.viewport = (RectTransform)root.transform;
        art.players = new[] { bear, ram };
        art.enemies = new[] { hyena, badger };

        var cover = AddImage("Cover", root.transform, null);
        cover.color = new Color(0.025f, 0.035f, 0.09f, 1f);
        cover.raycastTarget = true;
        var left = AddRect("PlayerSide", root.transform);
        var right = AddRect("BossSide", root.transform);
        AddImage("BlueBackground", left, blue);
        AddImage("RedBackground", right, red);
        art.leftMotion = left;
        art.rightMotion = right;
        // Keep both backgrounds before the character layers on the SAME canvas.
        // Characters follow their side's animation without inheriting its draw order.
        art.enemyLayer = AddRect("BossCharacterLayer", root.transform);
        art.playerLayer = AddRect("PlayerCharacterLayer", root.transform);
        art.enemyImage = AddImage("Boss", art.enemyLayer, hyena);
        art.playerImage = AddImage("Player", art.playerLayer, bear);
        art.playerRect = art.playerImage.rectTransform;
        art.enemyRect = art.enemyImage.rectTransform;
        Pin(art.playerRect, new Vector2(0f, 1f));
        Pin(art.enemyRect, new Vector2(1f, 0f));

        art.versus = AddRect("VS", root.transform);
        Pin(art.versus, new Vector2(0.5f, 0.5f));
        art.versus.sizeDelta = new Vector2(440f, 300f);
        art.versus.localRotation = Quaternion.Euler(0f, 0f, -8f);
        var vsGroup = art.versus.gameObject.AddComponent<CanvasGroup>();
        var shadow = AddVersusText("Shadow", art.versus, new Color(0.03f, 0.015f, 0.08f));
        shadow.rectTransform.anchoredPosition = new Vector2(8f, -14f);
        shadow.outlineColor = new Color(0.03f, 0.015f, 0.08f);
        shadow.outlineWidth = 0.32f;
        var rim = AddVersusText("GoldRim", art.versus, new Color(1f, 0.58f, 0.04f));
        rim.outlineColor = new Color(1f, 0.58f, 0.04f);
        rim.outlineWidth = 0.22f;
        var face = AddVersusText("Face", art.versus, Color.white);
        face.enableVertexGradient = true;
        face.colorGradient = new VertexGradient(
            Color.white, Color.white,
            new Color(1f, 0.78f, 0.16f), new Color(1f, 0.78f, 0.16f));
        face.outlineColor = new Color(0.25f, 0.075f, 0.025f);
        face.outlineWidth = 0.1f;
        art.playerLayer.SetAsLastSibling(); // Upper-left hero is the topmost artwork.

        var controller = root.AddComponent<BossDuelIntroController>();
        controller.ConfigureArtwork(group, left, right, vsGroup, art);
        root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        art.RefreshLayout();
        art.SyncCharacterMotion();
        return controller;
    }

    public void RandomizeCharacters()
    {
        playerImage.sprite = players[Random.Range(0, players.Length)];
        enemyImage.sprite = enemies[Random.Range(0, enemies.Length)];
        RefreshLayout();
    }

    private void LateUpdate()
    {
        if (viewport.rect.size != lastSize) RefreshLayout();
        SyncCharacterMotion();
    }

    private void SyncCharacterMotion()
    {
        playerLayer.anchoredPosition = leftMotion.anchoredPosition;
        playerLayer.localScale = leftMotion.localScale;
        enemyLayer.anchoredPosition = rightMotion.anchoredPosition;
        enemyLayer.localScale = rightMotion.localScale;
    }

    private void RefreshLayout()
    {
        lastSize = viewport.rect.size;
        if (lastSize.x <= 0f || lastSize.y <= 0f) return;
        // Fit complete character canvases without distortion or cropping. Boss cut edges
        // stay on the actual screen corner, independently of safe-area padding/aspect ratio.
        FitCharacter(playerRect, playerImage.sprite);
        FitCharacter(enemyRect, enemyImage.sprite);
        playerRect.anchoredPosition = new Vector2(0f, -lastSize.y * 0.025f);
        float size = Mathf.Min(lastSize.x, lastSize.y);
        versus.sizeDelta = new Vector2(size * 0.43f, size * 0.3f);
        // All three layers need identical glyph sizing; separate auto-sizing would
        // produce mismatched outlines because each material has different padding.
        foreach (var label in versus.GetComponentsInChildren<TextMeshProUGUI>())
            label.fontSize = size * 0.235f;
    }

    private void FitCharacter(RectTransform rect, Sprite sprite)
    {
        Vector2 source = sprite.rect.size;
        float scale = Mathf.Min(lastSize.x / source.x, lastSize.y / source.y);
        rect.sizeDelta = source * scale;
    }

    private static RectTransform AddRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Pin(RectTransform rect, Vector2 corner)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = corner;
        rect.anchoredPosition = Vector2.zero;
    }

    private static Image AddImage(string name, Transform parent, Sprite sprite)
    {
        var image = AddRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI AddVersusText(string name, Transform parent, Color color)
    {
        var text = AddRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "VS";
        text.font = TMP_Settings.defaultFontAsset;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        text.fontSize = 240f;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
