using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jel fırlatıcı (pres) yardımcıları: yön, gövde devri ve buhar bulutları. Görsel <see cref="GelLauncherPressView"/>,
/// atış GelLauncherFireAction'dadır. Buhar sprite'ları Resources/GelLauncher/GelSteam1-3.
/// </summary>
public static class GelLauncherFx
{
    private const string SteamPath = "GelLauncher/GelSteam";
    private static readonly Color SteamTint = new Color(0.60f, 0.38f, 0.78f, 1f);
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

    // Kırılan kartuşun GERÇEK görseli: GridSpawner yok etmek yerine buraya bırakır, GelLauncherFireAction
    // devralıp sıkışma → ileri atılma → kapak animasyonunu onun üstünde oynatır (geçici kopya VFX katmanında
    // düşen taşların/efektlerin arasında kayboluyordu). Devralınmazsa bırakan taraf kısa süre sonra siler.
    private static readonly System.Collections.Generic.Dictionary<int, Image> pendingBodies = new();

    public static void HandOverBody(int originIndex, Image view)
    {
        if (view == null) return;
        if (pendingBodies.TryGetValue(originIndex, out var old) && old != null && old != view)
            Object.Destroy(old.gameObject);
        pendingBodies[originIndex] = view;
    }

    public static Image TakeBody(int originIndex)
    {
        if (!pendingBodies.TryGetValue(originIndex, out var view)) return null;
        pendingBodies.Remove(originIndex);
        return view;
    }

    public static bool IsBodyPending(int originIndex, Image view)
        => pendingBodies.TryGetValue(originIndex, out var v) && v == view;

    public static bool IsVertical(ObstacleId id)
        => id == ObstacleId.GelLauncherUp || id == ObstacleId.GelLauncherDown;

    /// UI uzayında yön (UI y yukarı artar).
    public static Vector2 UiDirection(ObstacleId id)
    {
        var d = Direction(id);
        return new Vector2(d.x, -d.y);
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

            var img = UiVfxPool.RentImage(GelLauncherSteamFx.PoolKey, parent, "GelSteam");
            var go = img.gameObject;
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localPosition = localPos;
            float size = cell * sizeScale * Random.Range(0.85f, 1.15f);
            rt.sizeDelta = new Vector2(size, size);

            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = SteamTint;

            // Sırayla sola / sağa (iki buluttan fazlası ileri doğru dağılır).
            float side = count == 1 ? Random.Range(-0.4f, 0.4f) : (i % 2 == 0 ? -1f : 1f) * Random.Range(0.7f, 1.1f);
            Vector2 drift = (uiDir * Random.Range(0.25f, 0.55f) + perp * side * 0.6f) * cell;
            // A shared driver outlives the emitter; no per-puff component or coroutine allocation.
            float lifetime = Random.Range(0.75f, 1f);
            GelLauncherSteamFx.Track(img, rt, drift, lifetime);
        }
    }
}
