using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One update loop for pooled steam; independent of the launchers that emit it.</summary>
public sealed class GelLauncherSteamFx : MonoBehaviour
{
    internal const string PoolKey = "GelLauncher.Steam";
    private const int MaxRetained = 96;
    private static GelLauncherSteamFx instance;

    private struct Puff
    {
        public Image image;
        public RectTransform rect;
        public Vector3 start;
        public Vector2 drift;
        public Color tint;
        public float elapsed, lifetime;
    }

    private readonly List<Puff> active = new(96);

    public static void Track(Image image, RectTransform rect, Vector2 drift, float lifetime)
    {
        if (instance == null)
        {
            var go = new GameObject("[GelLauncherSteamFx]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GelLauncherSteamFx>();
        }

        rect.localScale = Vector3.one * 0.35f;
        instance.active.Add(new Puff
        {
            image = image, rect = rect, start = rect.localPosition, drift = drift,
            tint = image.color, lifetime = Mathf.Max(0.01f, lifetime)
        });
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var puff = active[i];
            puff.elapsed += Time.deltaTime;
            if (puff.image == null || puff.rect == null || !puff.image.gameObject.activeInHierarchy
                || puff.elapsed >= puff.lifetime)
            {
                Release(puff);
                int last = active.Count - 1;
                active[i] = active[last];
                active.RemoveAt(last);
                continue;
            }

            float k = puff.elapsed / puff.lifetime;
            float ease = 1f - (1f - k) * (1f - k);
            puff.rect.localPosition = puff.start + (Vector3)(puff.drift * ease);
            puff.rect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.15f, ease);
            var color = puff.tint;
            color.a *= 1f - k * k;
            puff.image.color = color;
            active[i] = puff;
        }
    }

    private static void Release(Puff puff)
    {
        if (puff.image == null) return;
        puff.image.sprite = null;
        UiVfxPool.Return(PoolKey, puff.image.gameObject, MaxRetained);
    }

    private void OnDisable()
    {
        foreach (var puff in active) Release(puff);
        active.Clear();
        if (instance == this) instance = null;
    }
}
