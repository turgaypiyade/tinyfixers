using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GeneralTophud.png'yi Team ve Market panellerinin üst bandına (TopBar) uygular —
/// paneli yeniden KURMAZ, başka hiçbir görsele dokunmaz; tekrar çalıştırılabilir.
/// (Leaderboard bilerek dışarıda: kullanıcı orayı elle tamamladı.)
/// </summary>
public static class TopHudApplyTool
{
    private const string SpritePath = "Assets/_Project/Art/UI/RanksTeamUI/GeneralTophud.png";

    // "Biraz offset": bant varsayılan 150px'ten daha uzun ve hafif aşağı taşar,
    // görselin alt kavisi içerikle buluşsun diye. Beğenmezsen buradan ayarla.
    private const float BandHeight = 210f;
    private const float BandYOffset = 0f;      // üstten aşağı itme (px; 0 = tam üst)
    private const float BandSideOverflow = 6f; // yanlardan taşma → kenar boşluğu görünmesin

    // İçerik gövdesi bandın ALTINDAN başlar (bant 150→210 uzayınca içerik altında
    // kalmasın diye Body/ScrollView üst kenarı da buraya itilir).
    private const float ContentTop = BandHeight + BandYOffset + 10f;

    [MenuItem("TinyFixers/Mockup/Ekle - Genel TopHud (Team + Market)")]
    public static void Apply()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Genel TopHud",
                $"Sprite bulunamadı:\n{SpritePath}\nTexture Type = Sprite (2D and UI) olmalı.", "Tamam");
            return;
        }

        int applied = 0;
        applied += ApplyTo(FindPanel<TeamScreenController>(), sprite, "TeamPanel");
        applied += ApplyTo(FindPanel<ShopScreenController>(), sprite, "ShopPanel");

        if (applied == 0)
        {
            EditorUtility.DisplayDialog("Genel TopHud",
                "Sahnede Team/Shop paneli bulunamadı. MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        EditorUtility.DisplayDialog("Genel TopHud",
            $"TopHud {applied} panele uygulandı (Team/Market). Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static Transform FindPanel<T>() where T : MonoBehaviour
    {
        var ctrl = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        return ctrl != null ? ctrl.transform : null;
    }

    private static int ApplyTo(Transform panel, Sprite sprite, string label)
    {
        if (panel == null) return 0;

        var topBar = panel.Find("TopBar") as RectTransform;
        var img = topBar != null ? topBar.GetComponent<Image>() : null;
        if (img == null)
        {
            Debug.LogWarning($"[TopHud] {label}: TopBar/Image bulunamadı, atlandı.");
            return 0;
        }

        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;   // bant tam genişliğe esner
        img.color = Color.white;

        topBar.anchorMin = new Vector2(0, 1);
        topBar.anchorMax = new Vector2(1, 1);
        topBar.pivot = new Vector2(0.5f, 1);
        topBar.anchoredPosition = new Vector2(0, -BandYOffset);
        topBar.sizeDelta = new Vector2(BandSideOverflow * 2f, BandHeight);

        // İçerik gövdesi bandın altına insin: Team'de "Body", Shop'ta "ScrollView".
        var content = (panel.Find("Body") ?? panel.Find("ScrollView")) as RectTransform;
        if (content != null)
            content.offsetMax = new Vector2(content.offsetMax.x, -ContentTop);
        else
            Debug.LogWarning($"[TopHud] {label}: Body/ScrollView bulunamadı — içerik el ile kaydırılmalı.");

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        return 1;
    }
}
