using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WaterTank kırılma animasyonu: depo merkezinden her hedef hücreye bir su damlası "göktaşı" gibi
/// fırlar — önce yavaşlayarak yükselir (tepe noktası hedefin üstünde, board'un yukarısında), sonra
/// hızlanarak, uzayıp iz bırakarak hücreye çakılır. Varışta onLand(hücre) (birikinti görünür) +
/// sıçrama pop'u. Damlalar sırayla ateşlenir. Üst VFX katmanında (BoardVfxPlayer.VfxRoot) çizilir.
///
/// Sahne kurulumu gerekmez: WaterTankSpreadAction bu component'i BoardController GO'suna ilk
/// kullanımda ekler; ayarlar/görseller Resources/WaterTank/WaterTankConfig'ten.
/// </summary>
public sealed class WaterTankSplashAnimator : MonoBehaviour
{
    private BoardController board;
    private WaterTankConfig config;

    public void Init(BoardController owner, WaterTankConfig cfg)
    {
        board = owner;
        config = cfg;
    }

    private RectTransform FlightRoot =>
        (board != null && board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null)
            ? board.BoardVfxPlayer.VfxRoot
            : (board != null ? board.Parent : null);

    private Sprite PickDropletSprite(int index)
    {
        var sprites = config != null ? config.dropletSprites : null;
        if (sprites != null && sprites.Count > 0)
        {
            int n = sprites.Count;
            for (int j = 0; j < n; j++)
            {
                var s = sprites[(index + j) % n];
                if (s != null) return s;
            }
        }
        return WaterDropletSprites.Droplet;
    }

    /// <summary>Tüm damlalar inince döner. onLand her hedef için tam bir kez çağrılır.</summary>
    public IEnumerator PlaySplash(Vector2Int origin, Vector2Int size, IReadOnlyList<Vector2Int> targets, Action<Vector2Int> onLand)
    {
        var root = FlightRoot;
        if (board == null || root == null || targets == null || targets.Count == 0)
        {
            InvokeAll(targets, onLand);
            yield break;
        }

        Vector2 center = WorldToLocal(FootprintWorldCenter(origin, size), root);
        float ts = Vector2.Distance(CellAnchored(origin, root), CellAnchored(origin + Vector2Int.right, root));
        if (ts < 1f) ts = Mathf.Max(1f, board.TileSize);

        int spriteOffset = UnityEngine.Random.Range(0, 8);
        float footprint = ts * Mathf.Max(size.x, size.y);
        PlayBreakBurst(root, center, footprint, spriteOffset);
        PlayBreakShards(root, center, footprint);

        float interval = config != null ? Mathf.Max(0f, config.launchInterval) : 0.05f;
        int remaining = targets.Count;

        for (int i = 0; i < targets.Count; i++)
        {
            float delay = i * interval + UnityEngine.Random.Range(0f, interval * 0.5f);
            StartCoroutine(FlyMeteor(root, center, targets[i], ts, PickDropletSprite(spriteOffset + i), onLand, delay,
                () => remaining--));
        }

        while (remaining > 0)
            yield return null;
    }

    private IEnumerator FlyMeteor(
        RectTransform root,
        Vector2 center,
        Vector2Int cell,
        float ts,
        Sprite sprite,
        Action<Vector2Int> onLand,
        float delay,
        Action onDone)
    {
        // onLand/onDone her koşulda (component/sahne kapanışı hariç) çağrılsın diye try/finally.
        GameObject go = null;
        bool landed = false;
        try
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Vector2 tgt = CellAnchored(cell, root);

            go = new GameObject("WaterMeteor", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float dropSize = ts * (config != null ? config.dropletSizeRatio : 0.7f);
            rt.sizeDelta = new Vector2(dropSize, dropSize);
            rt.anchoredPosition = center;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = DropletTint;

            Vector2 apexRange = config != null ? config.apexHeightTiles : new Vector2(3f, 5f);
            float apexY = Mathf.Max(center.y, tgt.y) + ts * UnityEngine.Random.Range(apexRange.x, apexRange.y);
            // Tepe yatayda hedefe doğru ~%60 ilerler → düşüş hafif açılı, göktaşı gibi iner.
            Vector2 apex = new Vector2(Mathf.Lerp(center.x, tgt.x, 0.6f), apexY);

            float rise = Mathf.Max(0.05f, (config != null ? config.riseDuration : 0.34f) * UnityEngine.Random.Range(0.9f, 1.15f));
            float fall = Mathf.Max(0.05f, (config != null ? config.fallDuration : 0.26f) * UnityEngine.Random.Range(0.9f, 1.15f));
            float stretch = config != null ? config.fallStretch : 1.6f;
            float trailInterval = config != null ? config.trailInterval : 0.025f;

            // 1) Yükseliş: ease-out (fışkırır, tepede yavaşlar), hafif küçülür.
            float t = 0f;
            Vector2 prev = center;
            while (t < rise)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rise);
                float e = 1f - (1f - k) * (1f - k);
                Vector2 pos = new Vector2(Mathf.Lerp(center.x, apex.x, k), Mathf.Lerp(center.y, apex.y, e));
                rt.anchoredPosition = pos;
                OrientAlong(rt, pos - prev, 1f + (stretch - 1f) * 0.4f * (1f - k));
                float sc = Mathf.Lerp(1.15f, 0.8f, k);
                rt.localScale = new Vector3(rt.localScale.x * sc, rt.localScale.y * sc, 1f);
                prev = pos;
                yield return null;
            }

            // 2) Düşüş: ease-in (hızlanarak çakılır), hız yönünde uzar, iz bırakır.
            t = 0f;
            float trailClock = 0f;
            while (t < fall)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                float e = k * k;
                Vector2 pos = Vector2.Lerp(apex, tgt, e);
                rt.anchoredPosition = pos;
                OrientAlong(rt, pos - prev, Mathf.Lerp(1f, stretch, k));
                float sc = Mathf.Lerp(0.8f, 1.05f, k);
                rt.localScale = new Vector3(rt.localScale.x * sc, rt.localScale.y * sc, 1f);

                if (trailInterval > 0f)
                {
                    trailClock += Time.deltaTime;
                    if (trailClock >= trailInterval)
                    {
                        trailClock = 0f;
                        StartCoroutine(TrailGhost(root, sprite, pos, dropSize * sc * 0.6f));
                    }
                }

                prev = pos;
                yield return null;
            }

            Destroy(go);
            go = null;

            landed = true;
            onLand?.Invoke(cell);

            float splat = config != null ? config.splatSizeRatio : 0.95f;
            if (splat > 0f)
            {
                var splatSprite = config != null && config.splatSprite != null ? config.splatSprite : sprite;
                StartCoroutine(SplatPop(root, splatSprite, ts * splat, tgt));
            }
        }
        finally
        {
            if (go != null) Destroy(go);
            if (!landed) onLand?.Invoke(cell);
            onDone?.Invoke();
        }
    }

    // Dağılırken bidon gövdesinden parçalar saçılır (crackFrames'in son karesinden kesilir).
    private void PlayBreakShards(RectTransform root, Vector2 center, float footprint)
    {
        int count = config != null ? config.breakShardCount : 8;
        Sprite source = null;
        var frames = config != null ? config.crackFrames : null;
        if (frames != null)
            for (int i = frames.Count - 1; i >= 0 && source == null; i--)
                source = frames[i];
        WaterTankFx.SpawnShards(this, root, center, footprint, source, count);
    }

    private Color DropletTint => config != null ? config.dropletTint : Color.white;

    // Depo dağılırken merkezden her yöne saçılan damlalar (yalnız görsel; hücreye bağlı değil).
    // footprint: deponun uzun kenarı (piksel) — 2x2 depo daha geniş saçar.
    private void PlayBreakBurst(RectTransform root, Vector2 center, float footprint, int spriteOffset)
    {
        int count = config != null ? config.burstDropletCount : 12;
        if (count <= 0)
            return;

        float radius = footprint * (config != null ? config.burstRadiusTiles : 1.3f);
        float height = footprint * (config != null ? config.burstHeightTiles : 0.9f);
        float duration = Mathf.Max(0.08f, config != null ? config.burstDuration : 0.42f);
        float size = footprint * 0.45f * (config != null ? config.dropletSizeRatio : 0.7f);

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + UnityEngine.Random.Range(-15f, 15f);
            StartCoroutine(BurstDroplet(root, center, PickDropletSprite(spriteOffset + i),
                size * UnityEngine.Random.Range(0.7f, 1.25f), angle,
                radius * UnityEngine.Random.Range(0.5f, 1.05f),
                height * UnityEngine.Random.Range(0.7f, 1.25f),
                duration * UnityEngine.Random.Range(0.85f, 1.2f)));
        }
    }

    private IEnumerator BurstDroplet(RectTransform root, Vector2 center, Sprite sprite, float size,
        float angleDeg, float radius, float height, float duration)
    {
        var go = new GameObject("WaterBurstDroplet", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = center;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = DropletTint;

        Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
        Vector2 end = center + dir * radius;
        Vector2 prev = center;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = 1f - (1f - k) * (1f - k);
            // Yay: önce yukarı sıçrar, sonra yerçekimiyle düşer (sin yerine parabol, düşüş daha uzun).
            float lift = height * 4f * k * (0.85f - k);
            Vector2 pos = Vector2.Lerp(center, end, e) + Vector2.up * lift;
            rt.anchoredPosition = pos;
            OrientAlong(rt, pos - prev, 1.25f);
            float sc = Mathf.Lerp(1f, 0.55f, k);
            rt.localScale = new Vector3(rt.localScale.x * sc, rt.localScale.y * sc, 1f);
            var c = img.color; c.a = DropletTint.a * (1f - Mathf.Clamp01((k - 0.6f) / 0.4f)); img.color = c;
            prev = pos;
            yield return null;
        }

        Destroy(go);
    }

    // Yönü hareket vektörüne çevirir: sprite'ın "altı" (damlanın dolgun ucu) hareket yönüne bakar,
    // sivri ucu arkada kalır (gerçek düşen damla gibi); o eksende uzar.
    private static void OrientAlong(RectTransform rt, Vector2 delta, float stretch)
    {
        if (delta.sqrMagnitude > 0.0001f)
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f);
        float squash = 1f / Mathf.Sqrt(Mathf.Max(1f, stretch));
        rt.localScale = new Vector3(squash, stretch, 1f);
    }

    private IEnumerator TrailGhost(RectTransform root, Sprite sprite, Vector2 pos, float size)
    {
        var go = new GameObject("WaterMeteorTrail", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = DropletTint;

        const float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float sc = Mathf.Lerp(1f, 0.3f, k);
            rt.localScale = new Vector3(sc, sc, 1f);
            var c = img.color; c.a = DropletTint.a * 0.55f * (1f - k); img.color = c;
            yield return null;
        }

        Destroy(go);
    }

    private IEnumerator SplatPop(RectTransform root, Sprite sprite, float peak, Vector2 pos)
    {
        var go = new GameObject("WaterSplat", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(peak, peak);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = DropletTint;

        const float dur = 0.22f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);
            // Yassı sıçrama: yatayda açılır, dikeyde basık.
            rt.localScale = new Vector3(Mathf.Lerp(0.4f, 1.3f, e), Mathf.Lerp(0.4f, 0.75f, e), 1f);
            var c = img.color; c.a = DropletTint.a * (1f - k); img.color = c;
            yield return null;
        }

        Destroy(go);
    }

    private Vector3 FootprintWorldCenter(Vector2Int origin, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        Vector3 c0 = board.GetCellWorldCenterPosition(origin.x, origin.y);
        Vector3 c1 = board.GetCellWorldCenterPosition(origin.x + w - 1, origin.y + h - 1);
        return (c0 + c1) * 0.5f;
    }

    private Vector2 CellAnchored(Vector2Int cell, RectTransform space)
        => WorldToLocal(board.GetCellWorldCenterPosition(cell.x, cell.y), space);

    private static Vector2 WorldToLocal(Vector3 worldPos, RectTransform space)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }

    private static void InvokeAll(IReadOnlyList<Vector2Int> targets, Action<Vector2Int> onLand)
    {
        if (targets == null || onLand == null) return;
        for (int i = 0; i < targets.Count; i++)
            onLand(targets[i]);
    }
}
