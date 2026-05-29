using System.Collections.Generic;
using UnityEngine;

public class BoardBreakFxService
{
    private const float MinSafeFxLifetime = 0.75f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly MaterialPropertyBlock ParticlePropertyBlock = new();

    private readonly BoardController board;
    private readonly List<Sprite> fallbackSpriteBuffer = new(1);

    public BoardBreakFxService(BoardController board)
    {
        this.board = board;
    }

    public void PlayTileBreak(TileView tile)
    {
        if (tile == null)
            return;

        Color color = ResolveBreakColor(tile);

        SpawnAtWorld(
            board.TileBreakFxPrefab,
            board.TileBreakFxLifetime,
            board.GetTileWorldCenter(tile),
            color,
            null);
    }

    public void PlayObstacleBreak(ObstacleVisualChange change)
    {
        Debug.Log($"[ObstacleFX] id={change.obstacleId} cleared={change.cleared} remaining={change.remainingHits} hitPrefab={(board.ObstacleHitFxPrefab != null ? board.ObstacleHitFxPrefab.name : "NULL")}");

        if (change.originIndex < 0 || board.Width <= 0 || board.Height <= 0)
        {
            FxWarn(
                $"[ObstacleFX] Abort: invalid origin/board. " +
                $"origin={change.originIndex}, board={board.Width}x{board.Height}"
            );
            return;
        }

        GameObject prefab = change.cleared
            ? board.ObstacleBreakFxPrefab
            : board.ObstacleHitFxPrefab;

        float lifetime = change.cleared
            ? board.ObstacleBreakFxLifetime
            : board.ObstacleHitFxLifetime;

        if (prefab == null)
        {
            Debug.LogWarning($"[ObstacleFX] Abort: prefab NULL. cleared={change.cleared}");
            return;
        }

        int x = change.originIndex % board.Width;
        int y = change.originIndex / board.Width;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
        {
            FxWarn($"[ObstacleFX] Abort: invalid cell. x={x}, y={y}");
            return;
        }

        if (!change.cleared)
        {
            var def = board.LevelData?.obstacleLibrary?.Get(change.obstacleId);
            if (def != null && def.IsHitParticlesSuppressedForRemainingHits(change.remainingHits))
            {
                Debug.LogWarning($"[ObstacleFX] Suppressed for id={change.obstacleId} remaining={change.remainingHits}");
                PlayObstacleSound(change);
                return;
            }
        }

        IReadOnlyList<Sprite> particleSprites = ResolveObstacleParticleSprites(change);
        string spriteNames = particleSprites != null ? string.Join(",", System.Linq.Enumerable.Select(particleSprites, s => s != null ? s.name : "null")) : "null";
        Debug.Log($"[ObstacleFX] sprites={particleSprites?.Count ?? 0} id={change.obstacleId} names=[{spriteNames}]");

        FxLog(
            $"[ObstacleFX] Spawn request. prefab={prefab.name}, cell=({x},{y}), " +
            $"lifetime={lifetime}, sprites={(particleSprites != null ? particleSprites.Count : 0)}"
        );

        PlayObstacleSound(change);

        Color fxColor = ResolveObstacleHitColor(change);
        SpawnAtWorld(
            prefab,
            lifetime,
            board.GetCellWorldCenterPosition(x, y),
            fxColor,
            particleSprites);
    }

    private static Color ResolveObstacleHitColor(ObstacleVisualChange change)
    {
        return change.removedColor switch
        {
            ChestColorMask.Gear  => new Color(1.00f, 0.78f, 0.25f, 1f),
            ChestColorMask.Core  => new Color(0.95f, 0.30f, 0.30f, 1f),
            ChestColorMask.Bolt  => new Color(0.30f, 0.60f, 1.00f, 1f),
            ChestColorMask.Plate => new Color(0.35f, 0.85f, 0.45f, 1f),
            _                    => Color.white
        };
    }

    private void PlayObstacleSound(ObstacleVisualChange change)
    {
        if (board.LevelData?.obstacleLibrary == null)
            return;

        var def = board.LevelData.obstacleLibrary.Get(change.obstacleId);
        if (def == null)
            return;

        AudioClip clip = change.cleared ? def.breakSound : def.hitSound;
        float vol = change.cleared ? def.breakSoundVolume : def.hitSoundVolume;

        if (clip == null)
            return;

        int x = change.originIndex % board.Width;
        int y = change.originIndex / board.Width;

        AudioSource.PlayClipAtPoint(clip, board.GetCellWorldCenterPosition(x, y), vol);
    }

    private IReadOnlyList<Sprite> ResolveObstacleParticleSprites(ObstacleVisualChange change)
    {
        if (board.LevelData == null)
        {
            FxWarn("[ObstacleFX] No LevelData.");
            return null;
        }

        if (board.LevelData.obstacleLibrary == null)
        {
            FxWarn("[ObstacleFX] No ObstacleLibrary on LevelData.");
            return null;
        }

        var def = board.LevelData.obstacleLibrary.Get(change.obstacleId);
        if (def == null)
        {
            FxWarn($"[ObstacleFX] No ObstacleDef found for id={change.obstacleId}");
            return null;
        }

        if (!change.cleared && def.IsHitParticlesSuppressedForRemainingHits(change.remainingHits))
            return null;

        List<Sprite> sprites = change.cleared
            ? def.breakParticleSprites
            : def.GetHitParticleSpritesForRemainingHits(change.remainingHits);

        if (sprites != null && sprites.Count > 0)
        {
#if OBSTACLE_FX_DEBUG
            int nonNullCount = 0;
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null)
                    nonNullCount++;
            }
            FxLog($"[ObstacleFX] Using custom obstacle particle sprites. id={change.obstacleId}, cleared={change.cleared}, count={sprites.Count}, nonNull={nonNullCount}");
#endif
            return sprites;
        }

        Sprite fallback = change.sprite;

        if (fallback == null)
            fallback = def.GetPreviewSprite();

        if (fallback == null)
        {
            FxWarn(
                $"[ObstacleFX] No custom sprites and no fallback sprite. " +
                $"id={change.obstacleId}, cleared={change.cleared}"
            );
            return null;
        }

        FxLog($"[ObstacleFX] Using fallback sprite: {fallback.name}");

        fallbackSpriteBuffer.Clear();
        fallbackSpriteBuffer.Add(fallback);
        return fallbackSpriteBuffer;
    }

    private void SpawnAtWorld(
        GameObject prefab,
        float lifetime,
        Vector3 worldPos,
        Color color,
        IReadOnlyList<Sprite> particleSprites)
    {
        if (prefab == null)
            return;

        RectTransform parent = board.BreakFxParent;
        GameObject go;

        if (parent != null)
        {
            go = Object.Instantiate(prefab, parent);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = board.WorldToAnchoredIn(parent, worldPos);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                go.transform.position = worldPos;
            }
        }
        else
        {
            go = Object.Instantiate(prefab, worldPos, Quaternion.identity);
        }

        go.SetActive(true);

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);

        ApplyColor(systems, color);

        if (particleSprites != null && particleSprites.Count > 0)
            ApplyParticleSprites(go, systems, particleSprites);
        else
            PlayWithFanBurst(systems);

        float safeLifetime = CalculateSafeLifetime(lifetime, systems);
        Object.Destroy(go, safeLifetime);
    }

    private float CalculateSafeLifetime(float requestedLifetime, ParticleSystem[] systems)
    {
        float safeLifetime = Mathf.Max(MinSafeFxLifetime, requestedLifetime);

        if (systems == null)
            return safeLifetime;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            var main = ps.main;

            float duration = main.duration;
            float maxParticleLifetime = main.startLifetime.constantMax;
            float startDelay = main.startDelay.constantMax;

            safeLifetime = Mathf.Max(
                safeLifetime,
                startDelay + duration + maxParticleLifetime + 0.05f);
        }

        return safeLifetime;
    }

    private void ApplyColor(ParticleSystem[] systems, Color color)
    {
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            var main = ps.main;
            main.startColor = color;
        }
    }

    private void ApplyParticleSprites(
        GameObject go,
        ParticleSystem[] systems,
        IReadOnlyList<Sprite> sprites)
    {
        if (go == null)
        {
            FxWarn("[ObstacleFX] ApplyParticleSprites abort: go is null.");
            return;
        }

        FxLog(
            $"[ObstacleFX] ApplyParticleSprites. go={go.name}, " +
            $"particleSystems={(systems != null ? systems.Length : 0)}, sprites={(sprites != null ? sprites.Count : 0)}"
        );

        if (systems == null || systems.Length == 0)
        {
            FxWarn($"[ObstacleFX] No ParticleSystem found under prefab instance: {go.name}");
            return;
        }

        if (sprites == null || sprites.Count == 0)
        {
            FxWarn("[ObstacleFX] No sprites provided for particle texture sheet.");
            return;
        }

        int validCount = 0;
        Sprite firstValidSprite = null;
        for (int s = 0; s < sprites.Count; s++)
        {
            if (sprites[s] == null) continue;
            if (firstValidSprite == null) firstValidSprite = sprites[s];
            validCount++;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (validCount == 1 && firstValidSprite != null)
            {
                // Single sprite: bypass TextureSheet atlas creation and set material directly.
                // TextureSheet Sprites mode creates an internal atlas requiring Read/Write-enabled
                // textures; standalone sprites may fail silently. Direct material approach is robust.
                var textureSheet = ps.textureSheetAnimation;
                textureSheet.enabled = false;
                ApplyDirectSpriteToMaterial(ps, firstValidSprite);
            }
            else if (validCount > 1)
            {
                var textureSheet = ps.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Sprites;

                for (int s = textureSheet.spriteCount - 1; s >= 0; s--)
                    textureSheet.RemoveSprite(s);

                for (int s = 0; s < sprites.Count; s++)
                {
                    if (sprites[s] != null)
                        textureSheet.AddSprite(sprites[s]);
                }

                ApplyParticleMainTexture(ps, firstValidSprite);
            }

            Debug.Log($"[ObstacleFX] PS={ps.gameObject.name} validSprites={validCount} sprite={(firstValidSprite != null ? firstValidSprite.name : "null")} tex={(firstValidSprite?.texture != null ? firstValidSprite.texture.name : "null")}");

            ps.Clear(true);
            ps.Play(true);

            Debug.Log($"[ObstacleFX] PS played={ps.isPlaying} particleCount={ps.particleCount}");
        }
    }

    private void ApplyDirectSpriteToMaterial(ParticleSystem ps, Sprite sprite)
    {
        if (ps == null || sprite == null || sprite.texture == null)
            return;

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        if (psr == null)
            return;

        var mat = new Material(psr.sharedMaterial);
        mat.SetTexture(MainTexId, sprite.texture);

        float texW = sprite.texture.width;
        float texH = sprite.texture.height;
        if (texW > 0f && texH > 0f)
        {
            Rect rect = sprite.textureRect;
            mat.SetTextureOffset(MainTexId, new Vector2(rect.x / texW, rect.y / texH));
            mat.SetTextureScale(MainTexId, new Vector2(rect.width / texW, rect.height / texH));
        }

        psr.material = mat;
    }

    private void ApplyParticleMainTexture(ParticleSystem ps, Sprite firstSprite)
    {
        if (ps == null || firstSprite == null || firstSprite.texture == null)
            return;

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        if (psr == null)
            return;

        var mat = psr.material;
        if (mat != null)
            mat.SetTexture(MainTexId, firstSprite.texture);
    }

    private static void PlayWithFanBurst(ParticleSystem[] systems)
    {
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private Color ResolveBreakColor(TileView tile)
    {
        if (tile == null)
            return Color.white;

        TileType type = tile.GetTileType();

        if (tile.GetSpecial() == TileSpecial.SystemOverride &&
            tile.GetOverrideBaseType(out var baseType))
        {
            type = baseType;
        }

        return type switch
        {
            TileType.Gear => new Color(1.00f, 0.78f, 0.25f, 1f),
            TileType.Core => new Color(0.95f, 0.30f, 0.30f, 1f),
            TileType.Bolt => new Color(0.30f, 0.60f, 1.00f, 1f),
            TileType.Plate => new Color(0.35f, 0.85f, 0.45f, 1f),

            TileType.LineEmitter_H => new Color(0.95f, 0.30f, 0.30f, 1f),
            TileType.LineEmitter_V => new Color(0.30f, 0.60f, 1.00f, 1f),
            TileType.PatchBot => new Color(1.00f, 0.78f, 0.25f, 1f),
            TileType.SystemOverride => Color.white,
            TileType.Normal => Color.white,
            _ => Color.white
        };
    }

    private static void FxLog(string message)
    {
#if OBSTACLE_FX_DEBUG
        Debug.Log(message);
#endif
    }

    private static void FxWarn(string message)
    {
#if OBSTACLE_FX_DEBUG
        Debug.LogWarning(message);
#endif
    }
}