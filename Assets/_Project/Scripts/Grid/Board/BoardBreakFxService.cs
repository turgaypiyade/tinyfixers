using System.Collections.Generic;
using UnityEngine;

public class BoardBreakFxService
{
    private const float MinSafeFxLifetime = 0.75f;
    private const int TileBreakFxParticleCount = 2;
    private const float TileBreakFxScale = 1f;
    private const float TileBreakFxParticleMinSize = 28f;
    private const float TileBreakFxParticleMaxSize = 40f;
    private const float TileBreakFxMaxParticleScreenSize = 0.14f;
    private const float TileBreakFxLightGravityMin = 0.35f;
    private const float TileBreakFxLightGravityMax = 0.65f;
    private const float TileBreakFxUpwardVelocity = 95f;

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
        bool isNormalTile = IsNormalTileBreak(tile);
        float scale = isNormalTile ? TileBreakFxScale : 1f;
        Vector3 worldCenter = board.GetTileWorldCenter(tile);

        SpawnAtWorld(
            board.TileBreakFxPrefab,
            board.TileBreakFxLifetime,
            worldCenter,
            color,
            null,
            scale,
            isNormalTile,
            isNormalTile ? TileBreakFxParticleCount : 0);
    }

    private static bool IsNormalTileBreak(TileView tile)
    {
        if (tile == null || tile.GetSpecial() != TileSpecial.None)
            return false;

        return tile.GetTileType() switch
        {
            TileType.Gear => true,
            TileType.Core => true,
            TileType.Bolt => true,
            TileType.Plate => true,
            TileType.Normal => true,
            _ => false
        };
    }

    public void PlayObstacleBreak(ObstacleVisualChange change)
    {
        if (board.BoardFlowTraceEnabled)
            Debug.Log($"[ObstacleFX] id={change.obstacleId} cleared={change.cleared} remaining={change.remainingHits} hitPrefab={(board.ObstacleHitFxPrefab != null ? board.ObstacleHitFxPrefab.name : "NULL")}");

        // Sound is position-independent: play before origin validation so Tube (originIndex=-1) still gets audio.
        PlayObstacleSound(change);

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
                if (board.BoardFlowTraceEnabled)
                    Debug.Log($"[ObstacleFX] Suppressed for id={change.obstacleId} remaining={change.remainingHits}");
                return;
            }
        }

        IReadOnlyList<Sprite> particleSprites = ResolveObstacleParticleSprites(change);
        if (board.BoardFlowTraceEnabled)
        {
            string spriteNames = particleSprites != null ? string.Join(",", System.Linq.Enumerable.Select(particleSprites, s => s != null ? s.name : "null")) : "null";
            Debug.Log($"[ObstacleFX] sprites={particleSprites?.Count ?? 0} id={change.obstacleId} names=[{spriteNames}]");
        }

        FxLog(
            $"[ObstacleFX] Spawn request. prefab={prefab.name}, cell=({x},{y}), " +
            $"lifetime={lifetime}, sprites={(particleSprites != null ? particleSprites.Count : 0)}"
        );

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

        AudioClip clip;
        float vol;
        if (change.isRepeatHit)
        {
            // remaining değişmedi (Wardrobe item gibi): stage ses mantığını atla, genel sesi çal.
            clip = def.hitSound;
            vol  = def.hitSoundVolume;
        }
        else if (change.cleared)
        {
            clip = def.breakSound;
            vol  = def.breakSoundVolume;
            // breakSound tanımlı değilse son hit stage'inin sesine bak, o da yoksa genel sese düş.
            if (clip == null)
                (clip, vol) = def.GetHitSoundForRemainingHits(change.remainingHits);
        }
        else
        {
            (clip, vol) = def.GetHitSoundForRemainingHits(change.remainingHits);
        }

        if (clip == null)
            return;

        if (!GameSettings.SoundEnabled)
            return;


        board.SfxSource?.PlayOneShot(clip, vol);
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

    /// <summary>
    /// Barrel oil-splash gibi ad-hoc particle burst'leri için: kanıtlanmış SpawnAtWorld hattını
    /// (BreakFxParent + world→anchored konumlama + sprite sheet) dışarıya açar.
    /// </summary>
    public void PlaySplashFx(GameObject prefab, float lifetime, Vector3 worldPos, IReadOnlyList<Sprite> sprites = null)
    {
        if (prefab == null)
            return;
        SpawnAtWorld(prefab, lifetime, worldPos, Color.white, sprites);
    }

    private void SpawnAtWorld(
        GameObject prefab,
        float lifetime,
        Vector3 worldPos,
        Color color,
        IReadOnlyList<Sprite> particleSprites,
        float scale = 1f,
        bool useLightTileMotion = false,
        int overrideParticleCount = 0)
    {
        if (prefab == null)
            return;

        RectTransform parent = board.BreakFxParent;
        float resolvedScale = Mathf.Max(0.01f, scale);

        // Havuz anahtarı kullanım imzasını içerir: aynı prefab farklı modlarla (light motion,
        // burst sayısı, sprite'lı/sprite'sız) kullanılıyor; instance'lar mod karıştırmadan dönsün.
        var poolKey = (prefab, useLightTileMotion, overrideParticleCount,
                       particleSprites != null && particleSprites.Count > 0);

        GameObject go = TakeFromPool(poolKey);
        bool fromPool = go != null;

        if (parent != null)
        {
            if (!fromPool)
                go = Object.Instantiate(prefab, parent);
            else if (go.transform.parent != parent)
                go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = board.WorldToAnchoredIn(parent, worldPos);
                rt.localScale = Vector3.one * resolvedScale;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                go.transform.position = worldPos;
                go.transform.localScale = prefab.transform.localScale * resolvedScale;
            }
        }
        else
        {
            if (!fromPool)
                go = Object.Instantiate(prefab, worldPos, Quaternion.identity);
            else
            {
                go.transform.SetParent(null, false);
                go.transform.position = worldPos;
            }

            go.transform.localScale = prefab.transform.localScale * resolvedScale;
        }

        go.SetActive(true);

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);

        ApplyColor(systems, color);
        if (useLightTileMotion)
            ApplyLightTileBreakMotion(systems);
        if (overrideParticleCount > 0)
            ApplyBurstParticleCount(systems, overrideParticleCount);

        if (particleSprites != null && particleSprites.Count > 0)
            ApplyParticleSprites(go, systems, particleSprites);
        else
            PlayWithFanBurst(systems);

        float safeLifetime = CalculateSafeLifetime(lifetime, systems);

        if (board != null && board.isActiveAndEnabled)
            board.StartCoroutine(CoReturnToPool(go, poolKey, safeLifetime));
        else
            Object.Destroy(go, safeLifetime);
    }

    // Kırılma FX havuzu: yoğun temizliklerde (Override dalgası vb.) aynı frame'de onlarca
    // Instantiate/Destroy çifti hitch yaratıyordu; instance'lar anahtar başına yeniden kullanılır.
    private const int MaxPooledPerKey = 24;
    private readonly Dictionary<(GameObject prefab, bool lightMotion, int burstCount, bool hasSprites), Stack<GameObject>> fxPools = new();

    private GameObject TakeFromPool((GameObject, bool, int, bool) key)
    {
        if (!fxPools.TryGetValue(key, out var stack))
            return null;

        while (stack.Count > 0)
        {
            var go = stack.Pop();
            if (go != null)
                return go;
        }

        return null;
    }

    private System.Collections.IEnumerator CoReturnToPool(GameObject go, (GameObject, bool, int, bool) key, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (go == null)
            yield break;

        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        go.SetActive(false);

        if (!fxPools.TryGetValue(key, out var stack))
            fxPools[key] = stack = new Stack<GameObject>();

        if (stack.Count >= MaxPooledPerKey)
        {
            Object.Destroy(go);
            yield break;
        }

        stack.Push(go);
    }

    private static void ApplyBurstParticleCount(ParticleSystem[] systems, int particleCount)
    {
        if (systems == null)
            return;

        short count = (short)Mathf.Clamp(particleCount, 1, short.MaxValue);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            var emission = ps.emission;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, count)
            });
        }
    }

    private static void ApplyLightTileBreakMotion(ParticleSystem[] systems)
    {
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(
                TileBreakFxParticleMinSize,
                TileBreakFxParticleMaxSize);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(
                TileBreakFxLightGravityMin,
                TileBreakFxLightGravityMax);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
                renderer.maxParticleSize = Mathf.Max(renderer.maxParticleSize, TileBreakFxMaxParticleScreenSize);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(
                0f,
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 0f)));
            velocity.y = new ParticleSystem.MinMaxCurve(
                TileBreakFxUpwardVelocity,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.35f, 0.55f),
                    new Keyframe(1f, -0.25f)));
            velocity.z = new ParticleSystem.MinMaxCurve(
                0f,
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 0f)));
        }
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

            if (board.BoardFlowTraceEnabled)
                Debug.Log($"[ObstacleFX] PS={ps.gameObject.name} validSprites={validCount} sprite={(firstValidSprite != null ? firstValidSprite.name : "null")} tex={(firstValidSprite?.texture != null ? firstValidSprite.texture.name : "null")}");

            ps.Clear(true);
            ps.Play(true);

            if (board.BoardFlowTraceEnabled)
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

        // psr.material ilk erişimde tek instance yaratır, sonraki erişimler aynı instance'ı
        // döner — her kırılmada new Material yaratıp sızdırmaz (pooled FX'te de güvenli).
        var mat = psr.material;
        mat.SetTexture(MainTexId, sprite.texture);

        float texW = sprite.texture.width;
        float texH = sprite.texture.height;
        if (texW > 0f && texH > 0f)
        {
            Rect rect = sprite.textureRect;
            mat.SetTextureOffset(MainTexId, new Vector2(rect.x / texW, rect.y / texH));
            mat.SetTextureScale(MainTexId, new Vector2(rect.width / texW, rect.height / texH));
        }
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
