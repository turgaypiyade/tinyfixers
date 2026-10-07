using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jel fırlatıcı (kartuş) görsel efektleri. Çatlak hâlde nabız + buhar: <see cref="GelLauncherIdleFx"/>.
/// Atışta sıkışma/esneme ve buhar patlaması: GelLauncherFireAction bu yardımcıları kullanır.
/// Sprite'lar Resources/GelLauncher altındadır (kapaksız gövde 4 yön, 3 buhar bulutu).
/// </summary>
public static class GelLauncherFx
{
    private const string OpenPath = "GelLauncher/GelLauncherOpen";
    private const string SteamPath = "GelLauncher/GelSteam";
    private static readonly Color SteamTint = new Color(0.97f, 0.9f, 1f, 0.9f);
    private static Sprite[] steamSprites;

    // Grid'de y aşağı artar: Up = (0,-1).
    public static Vector2Int Direction(ObstacleId id)
    {
        switch (id)
        {
            case ObstacleId.GelLauncherDown: return new Vector2Int(0, 1);
            case ObstacleId.GelLauncherLeft: return new Vector2Int(-1, 0);
            case ObstacleId.GelLauncherRight: return new Vector2Int(1, 0);
            default: return new Vector2Int(0, -1);
        }
    }

    public static bool IsVertical(ObstacleId id)
        => id == ObstacleId.GelLauncherUp || id == ObstacleId.GelLauncherDown;

    /// UI uzayında yön (UI y yukarı artar).
    public static Vector2 UiDirection(ObstacleId id)
    {
        var d = Direction(id);
        return new Vector2(d.x, -d.y);
    }

    public static Sprite LoadOpenSprite(ObstacleId id)
    {
        string suffix = id == ObstacleId.GelLauncherDown ? "Down"
            : id == ObstacleId.GelLauncherLeft ? "Left"
            : id == ObstacleId.GelLauncherRight ? "Right" : "Up";
        return Resources.Load<Sprite>(OpenPath + suffix);
    }

    private static Sprite RandomSteamSprite()
    {
        if (steamSprites == null)
        {
            steamSprites = new Sprite[3];
            for (int i = 0; i < steamSprites.Length; i++)
                steamSprites[i] = Resources.Load<Sprite>(SteamPath + (i + 1));
        }
        for (int tries = 0; tries < 4; tries++)
        {
            var s = steamSprites[Random.Range(0, steamSprites.Length)];
            if (s != null) return s;
        }
        return null;
    }

    /// Ağızdan buhar bulutları: ileri + iki yana savrulur, büyüyüp söner.
    /// localPos/cell, parent'ın yerel uzayındadır.
    public static void SpawnSteam(MonoBehaviour host, RectTransform parent, Vector2 localPos, Vector2 uiDir,
        float cell, int count, float sizeScale = 0.55f)
    {
        if (host == null || parent == null || !host.isActiveAndEnabled) return;
        Vector2 perp = new Vector2(-uiDir.y, uiDir.x);
        for (int i = 0; i < count; i++)
        {
            var sprite = RandomSteamSprite();
            if (sprite == null) return;

            var go = new GameObject("GelSteam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localPosition = localPos;
            float size = cell * sizeScale * Random.Range(0.85f, 1.15f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = SteamTint;

            // Sırayla sola / sağa (iki buluttan fazlası ileri doğru dağılır).
            float side = count == 1 ? Random.Range(-0.4f, 0.4f) : (i % 2 == 0 ? -1f : 1f) * Random.Range(0.7f, 1.1f);
            Vector2 drift = (uiDir * Random.Range(0.25f, 0.55f) + perp * side * 0.6f) * cell;
            host.StartCoroutine(CoPuff(rt, img, drift, Random.Range(0.75f, 1f)));
        }
    }

    private static IEnumerator CoPuff(RectTransform rt, Image img, Vector2 drift, float seconds)
    {
        Vector3 start = rt.localPosition;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / seconds;
            float ease = 1f - (1f - k) * (1f - k);
            rt.localPosition = start + (Vector3)(drift * ease);
            rt.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.15f, ease);
            var c = SteamTint;
            c.a = SteamTint.a * (1f - k);
            img.color = c;
            yield return null;
        }
        if (rt != null) Object.Destroy(rt.gameObject);
    }
}

/// <summary>
/// Çatlak kartuş beklerken: ara ara hafifçe şişip eski hâline döner ve ağzından buhar çıkar.
/// GridSpawner, kartuş ilk vuruşu alıp çatlak sprite'a geçince engel görseline ekler; görsel
/// yıkılınca bileşen de gider.
/// </summary>
[DisallowMultipleComponent]
public sealed class GelLauncherIdleFx : MonoBehaviour
{
    private const float PulseSeconds = 0.45f;
    private const float PulseAmount = 0.07f;

    private ObstacleId launcherId;
    private float cellSize;
    private RectTransform rt;

    public static void Ensure(Image image, ObstacleId id, float cellSize)
    {
        if (image == null) return;
        var fx = image.GetComponent<GelLauncherIdleFx>();
        if (fx == null) fx = image.gameObject.AddComponent<GelLauncherIdleFx>();
        fx.launcherId = id;
        fx.cellSize = cellSize;
        fx.rt = image.rectTransform;
    }

    private void Start() => StartCoroutine(Loop());

    private IEnumerator Loop()
    {
        // Vuruş sarsıntısı bitsin, sonra nabız başlasın.
        yield return new WaitForSeconds(Random.Range(0.45f, 0.9f));
        while (true)
        {
            yield return Pulse();
            yield return new WaitForSeconds(Random.Range(1.4f, 2.4f));
        }
    }

    private IEnumerator Pulse()
    {
        if (rt == null) yield break;
        Vector2 basePos = rt.anchoredPosition;
        Vector3 baseScale = rt.localScale;
        Vector2 size = rt.rect.size;
        // Pivot ne olursa olsun görsel merkez etrafında ölçekle.
        Vector2 pivotToCenter = new Vector2((0.5f - rt.pivot.x) * size.x, (0.5f - rt.pivot.y) * size.y);
        bool steamed = false;

        for (float t = 0f; t < PulseSeconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / PulseSeconds;
            float s = 1f + PulseAmount * Mathf.Sin(k * Mathf.PI);
            rt.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
            rt.anchoredPosition = basePos - pivotToCenter * (s - 1f);

            if (!steamed && k >= 0.45f)
            {
                steamed = true;
                Vector2 uiDir = GelLauncherFx.UiDirection(launcherId);
                float halfLen = (GelLauncherFx.IsVertical(launcherId) ? size.y : size.x) * 0.5f;
                Vector2 center = (Vector2)rt.localPosition + pivotToCenter;
                GelLauncherFx.SpawnSteam(this, rt.parent as RectTransform, center + uiDir * halfLen * 0.85f,
                    uiDir, cellSize, 2, 0.5f);
            }
            yield return null;
        }

        if (rt != null)
        {
            rt.localScale = baseScale;
            rt.anchoredPosition = basePos;
        }
    }
}
