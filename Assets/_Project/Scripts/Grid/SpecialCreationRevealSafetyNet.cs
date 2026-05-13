using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Safety net for rare cases where TileView's special creation reveal coroutine exits
/// before its cleanup path runs. When that happens, the generated halo can stay on
/// top of the tile and the icon can remain scaled up. This watcher only touches
/// reveal halos that have survived longer than the reveal animation should last.
/// </summary>
public sealed class SpecialCreationRevealSafetyNet : MonoBehaviour
{
    private const string HaloName = "__SpecialCreationHalo";
    private const float ScanIntervalSeconds = 0.25f;
    private const float StaleHaloSeconds = 1.0f;

    private static SpecialCreationRevealSafetyNet instance;

    private readonly Dictionary<int, float> haloFirstSeenByTileId = new Dictionary<int, float>();
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(SpecialCreationRevealSafetyNet));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SpecialCreationRevealSafetyNet>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
        CleanupStaleRevealVisuals();
    }

    private void CleanupStaleRevealVisuals()
    {
        var tiles = Resources.FindObjectsOfTypeAll<TileView>();
        HashSet<int> seenThisScan = null;

        for (int i = 0; i < tiles.Length; i++)
        {
            TileView tile = tiles[i];
            if (tile == null || tile.gameObject == null)
                continue;

            if (!tile.gameObject.scene.IsValid() || !tile.gameObject.activeInHierarchy)
                continue;

            Transform halo = tile.transform.Find(HaloName);
            if (halo == null)
                continue;

            int tileId = tile.GetInstanceID();
            seenThisScan ??= new HashSet<int>();
            seenThisScan.Add(tileId);

            if (!haloFirstSeenByTileId.TryGetValue(tileId, out float firstSeen))
            {
                haloFirstSeenByTileId[tileId] = Time.unscaledTime;
                continue;
            }

            if (Time.unscaledTime - firstSeen < StaleHaloSeconds)
                continue;

            Destroy(halo.gameObject);
            haloFirstSeenByTileId.Remove(tileId);
            ResetIconRevealScale(tile);
        }

        if (haloFirstSeenByTileId.Count == 0)
            return;

        if (seenThisScan == null || seenThisScan.Count == 0)
        {
            haloFirstSeenByTileId.Clear();
            return;
        }

        s_keysToRemove.Clear();
        foreach (int tileId in haloFirstSeenByTileId.Keys)
        {
            if (!seenThisScan.Contains(tileId))
                s_keysToRemove.Add(tileId);
        }

        for (int i = 0; i < s_keysToRemove.Count; i++)
            haloFirstSeenByTileId.Remove(s_keysToRemove[i]);

        s_keysToRemove.Clear();
    }

    private static readonly List<int> s_keysToRemove = new List<int>();

    private static void ResetIconRevealScale(TileView tile)
    {
        if (tile == null || tile.IconImage == null)
            return;

        RectTransform iconRt = tile.IconImage.rectTransform;
        if (iconRt == null)
            return;

        iconRt.localScale = Vector3.one;
        iconRt.localRotation = Quaternion.identity;

        Color c = tile.IconImage.color;
        c.a = 1f;
        tile.IconImage.color = c;
    }
}
