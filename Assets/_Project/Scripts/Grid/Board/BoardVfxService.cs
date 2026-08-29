using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Board-level VFX: combo explosions, pulse emitter effects.
/// No coroutines — just play/instantiate calls.
/// BoardController delegates here and forwards serialized references.
/// </summary>
public class BoardVfxService
{
    private readonly BoardController board;

    public BoardVfxService(BoardController board)
    {
        this.board = board;
    }

    // Override combo tek-atış SFX (Resources/Audio/OverrideCombo/*), board.Audio üzerinden.
    private static readonly Dictionary<string, AudioClip> _overrideSfxCache = new Dictionary<string, AudioClip>();

    private void PlayOverrideSfx(string fileName)
    {
        if (board == null || board.Audio == null || string.IsNullOrEmpty(fileName))
            return;

        if (!_overrideSfxCache.TryGetValue(fileName, out AudioClip clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>("Audio/OverrideCombo/" + fileName);
            _overrideSfxCache[fileName] = clip;
        }

        if (clip != null)
            board.Audio.PlayOneShotClip(clip);
    }

    public float PlaySystemOverrideComboVfxAndGetDuration(
        OverrideComboController vfx,
        RectTransform vfxSpace,
        int originX,
        int originY,
        Sprite overrideSpriteA,
        Sprite overrideSpriteB,
        Sprite mergedSprite = null)
    {
        if (vfx == null) return 0f;

        vfx.gameObject.SetActive(true);
        if (vfxSpace != null)
        {
            TileView ta = board.LastSwapA;
            TileView tb = board.LastSwapB;
            vfx.SetWaveMaxRadius(WaveMaxRadiusPx(originX, originY));

            if (ta != null && tb != null)
            {
                Vector3 worldOrigin = ResolveWorldCenterForCell(originX, originY);
                Vector2 localOrigin = board.WorldToAnchoredIn(vfxSpace, worldOrigin);

                Vector2 localA = board.WorldToAnchoredIn(vfxSpace, board.GetTileWorldCenter(ta));
                Vector2 localB = board.WorldToAnchoredIn(vfxSpace, board.GetTileWorldCenter(tb));
                vfx.PlayAtAnchoredPositions(localA, localB, localOrigin, overrideSpriteA, overrideSpriteB, mergedSprite);
            }
            else
            {
                Vector3 worldMid = ResolveWorldCenterForCell(originX, originY);
                Vector2 localMid = board.WorldToAnchoredIn(vfxSpace, worldMid);
                vfx.PlayAtAnchoredPosition(localMid, overrideSpriteA, overrideSpriteB, mergedSprite);
            }
        }
        else
        {
            vfx.Play(overrideSpriteA, overrideSpriteB, mergedSprite);
        }
        
        // ── Üç-fazlı Override+Override sesi ──
        // 1) SPIN-UP: combo başında (küreler dönmeye başlarken).
        PlayOverrideSfx("Override1");

        // 2) IMPACT: küreler çarpıştığında (OnImpact, ~0.70s).
        System.Action onImpactSfx = () => PlayOverrideSfx("Override2");
        vfx.OnImpact += onImpactSfx;

        // 3) WAVE: radyal dalga board'u temizlerken (ilk wave callback'inde bir kez).
        bool waveSfxPlayed = false;

        System.Action<float> onProgress = null;
        onProgress = (r) =>
        {
            board.InvokeSystemOverrideWaveProgress(r);
            if (!waveSfxPlayed)
            {
                waveSfxPlayed = true;
                PlayOverrideSfx("Override3");
            }
        };
        vfx.OnWaveRadiusChanged += onProgress;

        System.Action onFinished = null;
        onFinished = () => {
            vfx.OnWaveRadiusChanged -= onProgress;
            vfx.OnImpact -= onImpactSfx;
            vfx.OnComboFinished -= onFinished;
        };
        vfx.OnComboFinished += onFinished;

        float duration = vfx.GetTotalDuration();
        SystemOverrideBehaviorEvents.EmitOverrideComboVfxPlayed(duration);
        return duration;
    }

    public void PlayPulseEmitterComboVfxAtCell(PulseEmitterComboController vfx, RectTransform vfxSpace, int x, int y)
    {
        if (vfx == null || vfxSpace == null) return;

        vfx.gameObject.SetActive(true);

        Vector3 worldMid = ResolveWorldCenterForCell(x, y);
        Vector2 localMid = board.WorldToAnchoredIn(vfxSpace, worldMid);
        Vector2 boardSize = vfxSpace.rect.size;
        if (boardSize.sqrMagnitude < 1f)
            boardSize = new Vector2(board.Width * board.TileSize, board.Height * board.TileSize);

        vfx.SetTileSize(board.TileSize);
        vfx.PlayAt(localMid, boardSize);
    }

    public GameObject PlayPulsePulseExplosionVfxAtCell(GameObject prefab, RectTransform vfxSpace, float lifetime, int x, int y, float scale = 1f)
    {
        if (prefab == null || vfxSpace == null) return null;

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(x, y));

        Vector3 worldMid = ResolveWorldCenterForCell(x, y);
        Vector2 localMid = board.WorldToAnchoredIn(vfxSpace, worldMid);

        var go = Object.Instantiate(prefab, vfxSpace);
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = localMid;
            rt.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.position = worldMid;
        }

        // DISARI DAN destroy ETME
        // Object.Destroy(go, lifetime);

        // İsteğe bağlı: prefab scripti varsa süreyi buradan senkronlayabilirsin
        var fx = go.GetComponentInChildren<PulseCoreExplosionFX>(true);
        if (fx != null)
        {
            fx.SetLifetime(lifetime);
        }
        
        return go;
    }


    // Dalga cephesinin varış yarıçapı: origin hücresinden en uzak board KÖŞESİNE px mesafe.
    // Saf grid matematiği — RectTransform pivot/anchor geometrisine hiç dayanmaz.
    // BuildWaveFrontClearDelays AYNI normalizasyonu kullanır → VFX ile temizleme birebir senkron.
    public float WaveMaxRadiusPx(int originX, int originY)
    {
        float maxCells = SpecialVisualService.FarthestCornerDistanceCells(
            board.Width, board.Height, originX, originY);
        // +0.5: dalga son hücrenin merkezinden geçtikten sonra hücre SINIRINA kadar sürsün.
        return (maxCells + 0.5f) * board.TileSize;
    }

    private Vector3 ResolveWorldCenterForCell(int x, int y)
    {
        bool inBounds = x >= 0 && x < board.Width && y >= 0 && y < board.Height;
        if (inBounds)
        {
            TileView tile = board.Tiles[x, y];
            if (tile != null)
                return board.GetTileWorldCenter(tile);

            // Obstacle / empty tile fallback: use geometric center of the board cell.
            return board.GetCellWorldCenterPosition(x, y);
        }

        // Last resort: preserve previous behavior and use last swap midpoint.
        TileView ta = board.LastSwapA;
        TileView tb = board.LastSwapB;
        if (ta != null && tb != null)
            return (board.GetTileWorldCenter(ta) + board.GetTileWorldCenter(tb)) * 0.5f;
        if (ta != null)
            return board.GetTileWorldCenter(ta);
        if (tb != null)
            return board.GetTileWorldCenter(tb);

        return Vector3.zero;
    }

    public HashSet<Vector2Int> BuildPulseEmitterTargets(int cx, int cy)
    {
        var set = new HashSet<Vector2Int>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= board.Height) continue;
            for (int x = 0; x < board.Width; x++)
                if (!board.IsMaskHoleCell(x, yy)) set.Add(new Vector2Int(x, yy));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= board.Width) continue;
            for (int y = 0; y < board.Height; y++)
                if (!board.IsMaskHoleCell(xx, y)) set.Add(new Vector2Int(xx, y));
        }

        return set;
    }
}
