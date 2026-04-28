using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverrideRadialEffectPlayer : IClearEffectPlayer
{
    // ═════════════════════════════════════════════════════════════════════════
    //  IMPACT VFX TUNING
    //  Yıldırım çizimi burada değil — LightningSpawner yapıyor.
    //  Bu dosya sadece hedef taşta "beyaz parlak kare + dönme + fade" oynatır.
    // ═════════════════════════════════════════════════════════════════════════

    // Yıldırım çarpma anı (LightningBeam.lifeTime = 0.20s ile senkron)
    private const float LIGHTNING_HIT_MOMENT = 0.06f;

    // Outline square (beyaz kare çerçeve)
    private const float SQUARE_SIZE_RATIO = 1.1f;   // tile boyutunun çarpanı
    private const float SQUARE_BORDER_WIDTH = 4f;      // kenar kalınlığı (px)
    private const float SQUARE_DURATION = 0.35f;   // total ömür
    private const float SQUARE_ROTATIONS = 1.0f;    // kaç tur dönsün
    private const float SQUARE_SCALE_START = 1.3f;    // başlangıçta biraz büyük (içe doğru kapanır)
    private const float SQUARE_SCALE_END = 0.0f;    // sonda merkeze kapanır (yok olma hissi)

    // Renk
    private static readonly Color SQUARE_COLOR = new Color(1f, 1f, 1f, 1f);
    private static readonly Color FLASH_COLOR = new Color(0.95f, 0.98f, 1f, 0.9f);

    private const float FLASH_DURATION = 0.14f;
    private const float FLASH_SIZE_RATIO = 0.9f;

    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is OverrideRadialEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        OverrideRadialEffectDescriptor radial = effect as OverrideRadialEffectDescriptor;
        if (radial == null || radial.TargetTiles == null || radial.TargetTiles.Count == 0)
            yield break;

        PlayOverrideCenterVfx(board, radial);

        RectTransform vfxRoot = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;

        float maxDelay = 0f;

        for (int i = 0; i < radial.TargetTiles.Count; i++)
        {
            TileView tile = radial.TargetTiles[i];
            if (tile == null)
                continue;

            float delay = 0f;
            if (radial.DelayMap != null && radial.DelayMap.TryGetValue(tile, out delay))
            {
                if (delay > maxDelay)
                    maxDelay = delay;
            }

            board.StartCoroutine(PlayTileImpactWithVfx(board, vfxRoot, tile, delay, context));
        }

        yield return new WaitForSeconds(maxDelay + SQUARE_DURATION + radial.Timing.TailHoldSeconds);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  TILE IMPACT — rotating square + flash + clear
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator PlayTileImpactWithVfx(
        BoardController board,
        RectTransform vfxRoot,
        TileView tile,
        float delay,
        ClearEffectPlaybackContext context)
    {
        if (tile == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null)
            yield break;

        // Yıldırımın hedefe varma anıyla senkron bekle
        if (LIGHTNING_HIT_MOMENT > 0f)
            yield return new WaitForSeconds(LIGHTNING_HIT_MOMENT);

        if (tile == null)
            yield break;

        // VFX spawn
        if (vfxRoot != null)
        {
            Vector2 targetPos = GetTileAnchoredPos(board, tile);
            float tileSize = board.TileSize;

            SpawnRotatingSquare(vfxRoot, targetPos, tileSize);
            SpawnFlash(vfxRoot, targetPos, tileSize);
        }

        // Taşı temizle
        if (context != null && context.NotifyCellImpactNow != null)
            context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ROTATING OUTLINE SQUARE
    //  Taş etrafında beyaz parlak kare çerçeve, döner ve küçülerek kapanır.
    //  4 Image kenar (top/bottom/left/right) — sprite gerektirmez.
    // ═════════════════════════════════════════════════════════════════════════

    private void SpawnRotatingSquare(RectTransform parent, Vector2 pos, float tileSize)
    {
        float squareSize = tileSize * SQUARE_SIZE_RATIO;

        // Root — döndürülecek ve ölçeklenecek
        var rootGo = new GameObject("OverrideImpactSquare", typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.SetParent(parent, worldPositionStays: false);
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = pos;
        rootRt.sizeDelta = new Vector2(squareSize, squareSize);
        rootRt.SetAsLastSibling();

        // 4 kenar çiz — her biri ince dikdörtgen
        CreateSquareEdge(rootRt, "Top", new Vector2(0f, 0.5f * squareSize), new Vector2(squareSize, SQUARE_BORDER_WIDTH));
        CreateSquareEdge(rootRt, "Bottom", new Vector2(0f, -0.5f * squareSize), new Vector2(squareSize, SQUARE_BORDER_WIDTH));
        CreateSquareEdge(rootRt, "Left", new Vector2(-0.5f * squareSize, 0f), new Vector2(SQUARE_BORDER_WIDTH, squareSize));
        CreateSquareEdge(rootRt, "Right", new Vector2(0.5f * squareSize, 0f), new Vector2(SQUARE_BORDER_WIDTH, squareSize));

        var runner = rootGo.AddComponent<VfxCoroutineRunner>();
        runner.Run(AnimateSquare(rootGo, rootRt));
    }

    private void CreateSquareEdge(RectTransform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = SQUARE_COLOR;
        img.raycastTarget = false;
    }

    private IEnumerator AnimateSquare(GameObject go, RectTransform rt)
    {
        if (go == null || rt == null) yield break;

        // Tüm kenarları al — alpha için
        Image[] edges = go.GetComponentsInChildren<Image>();

        float t = 0f;
        while (t < SQUARE_DURATION)
        {
            if (go == null || rt == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / SQUARE_DURATION);

            // Rotation — lineer dönüş (daha önce yavaşlamasın, "sıkı" his)
            float angle = SQUARE_ROTATIONS * 360f * k;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Scale — büyükten sıfıra kapanma (EaseInCubic — sonda hızlanır)
            float scale = Mathf.Lerp(SQUARE_SCALE_START, SQUARE_SCALE_END, EaseInCubic(k));
            rt.localScale = new Vector3(scale, scale, 1f);

            // Alpha — son %30'da hızla söner
            float alpha = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] == null) continue;
                Color c = SQUARE_COLOR;
                c.a = alpha;
                edges[i].color = c;
            }

            yield return null;
        }

        if (go != null)
            Object.Destroy(go);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  FLASH — beyaz parlama, kareyle birlikte
    // ═════════════════════════════════════════════════════════════════════════

    private void SpawnFlash(RectTransform parent, Vector2 pos, float tileSize)
    {
        float size = tileSize * FLASH_SIZE_RATIO;

        var go = new GameObject("OverrideImpactFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.color = FLASH_COLOR;
        img.raycastTarget = false;

        var runner = go.AddComponent<VfxCoroutineRunner>();
        runner.Run(AnimateFlash(go, rt, img));
    }

    private IEnumerator AnimateFlash(GameObject go, RectTransform rt, Image img)
    {
        if (go == null || rt == null || img == null) yield break;

        Color startColor = img.color;
        Vector2 startSize = rt.sizeDelta;

        float t = 0f;
        while (t < FLASH_DURATION)
        {
            if (go == null || rt == null || img == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / FLASH_DURATION);

            Color c = startColor;
            c.a = startColor.a * (1f - k * k);
            img.color = c;

            float s = Mathf.Lerp(1f, 1.25f, k);
            rt.sizeDelta = startSize * s;

            yield return null;
        }

        if (go != null)
            Object.Destroy(go);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ORIGINAL METHODS (aynen korundu)
    // ═════════════════════════════════════════════════════════════════════════

    private void PlayOverrideCenterVfx(BoardController board, OverrideRadialEffectDescriptor radial)
    {
       /* if (radial.OriginCell.HasValue)
            ComboBehaviorEvents.EmitComboTriggered(TileSpecial.SystemOverride, TileSpecial.None, radial.OriginCell.Value);

        if (radial.OriginTile == null)
            return;

        if (board.BoardVfxPlayer != null)
        {
            Vector2 pos = GetTileAnchoredPos(board, radial.OriginTile);
            board.BoardVfxPlayer.PlayPulseVfx(pos, 2, board.TileSize);
        }

        if (board.SfxSource != null && board.SfxPulseCoreBoom != null)
            board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);*/
    }

    private Vector2 GetTileAnchoredPos(BoardController board, TileView tile)
    {
        if (tile == null)
            return Vector2.zero;

        RectTransform tileRect = null;

        try
        {
            tileRect = tile.GetComponent<RectTransform>();
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }

        if (tileRect == null)
            return Vector2.zero;

        if (tileRect.gameObject == null)
            return Vector2.zero;

        RectTransform vfxRoot = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;
        if (vfxRoot != null)
        {
            Vector3 worldPos;
            try
            {
                worldPos = tileRect.TransformPoint(tileRect.rect.center);
            }
            catch (MissingReferenceException)
            {
                return Vector2.zero;
            }

            Vector3 localPos;
            try
            {
                localPos = vfxRoot.InverseTransformPoint(worldPos);
            }
            catch (MissingReferenceException)
            {
                return Vector2.zero;
            }

            return (Vector2)localPos;
        }

        RectTransform tilesRoot = board.Parent;
        Vector2 rootOffset = tilesRoot != null ? tilesRoot.anchoredPosition : Vector2.zero;

        try
        {
            return rootOffset + tileRect.anchoredPosition;
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }
    }

    private static float EaseInCubic(float t) => t * t * t;
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}

// ═════════════════════════════════════════════════════════════════════════════
//  Helper
// ═════════════════════════════════════════════════════════════════════════════
public sealed class VfxCoroutineRunner : MonoBehaviour
{
    public void Run(System.Collections.IEnumerator routine)
    {
        if (routine != null && isActiveAndEnabled)
            StartCoroutine(routine);
    }
}