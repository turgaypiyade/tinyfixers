using System.Collections.Generic;
using UnityEngine;

public class BoardBreakFxService
{
    private readonly BoardController board;

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
        Debug.Log(
            $"[ObstacleFX] PlayObstacleBreak called. " +
            $"id={change.obstacleId}, cleared={change.cleared}, origin={change.originIndex}, remaining={change.remainingHits}"
        );

        if (change.originIndex < 0 || board.Width <= 0 || board.Height <= 0)
        {
            Debug.LogWarning(
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
            Debug.LogWarning(
                $"[ObstacleFX] Abort: prefab is NULL. " +
                $"cleared={change.cleared}, expected={(change.cleared ? "ObstacleBreakFxPrefab" : "ObstacleHitFxPrefab")}"
            );
            return;
        }

        int x = change.originIndex % board.Width;
        int y = change.originIndex / board.Width;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
        {
            Debug.LogWarning($"[ObstacleFX] Abort: invalid cell. x={x}, y={y}");
            return;
        }

        IReadOnlyList<Sprite> particleSprites = ResolveObstacleParticleSprites(change);

        Debug.Log(
            $"[ObstacleFX] Spawn request. prefab={prefab.name}, cell=({x},{y}), " +
            $"lifetime={lifetime}, sprites={(particleSprites != null ? particleSprites.Count : 0)}"
        );

        PlayObstacleSound(change);

        SpawnAtWorld(
            prefab,
            lifetime,
            board.GetCellWorldCenterPosition(x, y),
            Color.white,
            particleSprites);
    }

    private void PlayObstacleSound(ObstacleVisualChange change)
    {
        if (board.LevelData?.obstacleLibrary == null) return;

        var def = board.LevelData.obstacleLibrary.Get(change.obstacleId);
        if (def == null) return;

        AudioClip clip  = change.cleared ? def.breakSound   : def.hitSound;
        float     vol   = change.cleared ? def.breakSoundVolume : def.hitSoundVolume;

        if (clip == null) return;

        int x = change.originIndex % board.Width;
        int y = change.originIndex / board.Width;
        AudioSource.PlayClipAtPoint(clip, board.GetCellWorldCenterPosition(x, y), vol);
    }

    private IReadOnlyList<Sprite> ResolveObstacleParticleSprites(ObstacleVisualChange change)
    {
        if (board.LevelData == null)
        {
            Debug.LogWarning("[ObstacleFX] No LevelData.");
            return null;
        }

        if (board.LevelData.obstacleLibrary == null)
        {
            Debug.LogWarning("[ObstacleFX] No ObstacleLibrary on LevelData.");
            return null;
        }

        var def = board.LevelData.obstacleLibrary.Get(change.obstacleId);
        if (def == null)
        {
            Debug.LogWarning($"[ObstacleFX] No ObstacleDef found for id={change.obstacleId}");
            return null;
        }

        List<Sprite> sprites = change.cleared
            ? def.breakParticleSprites
            : def.hitParticleSprites;

        if (sprites != null && sprites.Count > 0)
        {
            int nonNullCount = 0;
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null)
                    nonNullCount++;
            }

            Debug.Log(
                $"[ObstacleFX] Using custom obstacle particle sprites. " +
                $"id={change.obstacleId}, cleared={change.cleared}, count={sprites.Count}, nonNull={nonNullCount}"
            );

            return sprites;
        }

        Sprite fallback = change.sprite;

        if (fallback == null)
            fallback = def.GetPreviewSprite();

        if (fallback == null)
        {
            Debug.LogWarning(
                $"[ObstacleFX] No custom sprites and no fallback sprite. " +
                $"id={change.obstacleId}, cleared={change.cleared}"
            );
            return null;
        }

        Debug.Log($"[ObstacleFX] Using fallback sprite: {fallback.name}");

        return new[] { fallback };
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

        ApplyColor(go, color);

        if (particleSprites != null && particleSprites.Count > 0)
            ApplyParticleSprites(go, particleSprites);
        else
            PlayWithFanBurst(go);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
    }

    private void ApplyColor(GameObject go, Color color)
    {
        if (go == null)
            return;

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startColor = color;
        }
    }

    private void ApplyParticleSprites(GameObject go, IReadOnlyList<Sprite> sprites)
    {
        if (go == null)
        {
            Debug.LogWarning("[ObstacleFX] ApplyParticleSprites abort: go is null.");
            return;
        }

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);

        Debug.Log(
            $"[ObstacleFX] ApplyParticleSprites. go={go.name}, " +
            $"particleSystems={systems.Length}, sprites={(sprites != null ? sprites.Count : 0)}"
        );

        if (systems.Length == 0)
        {
            Debug.LogWarning($"[ObstacleFX] No ParticleSystem found under prefab instance: {go.name}");
            return;
        }

        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogWarning("[ObstacleFX] No sprites provided for particle texture sheet.");
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;

            for (int s = textureSheet.spriteCount - 1; s >= 0; s--)
                textureSheet.RemoveSprite(s);

            int added = 0;

            Sprite firstSprite = null;
            for (int s = 0; s < sprites.Count; s++)
            {
                if (sprites[s] == null)
                    continue;

                textureSheet.AddSprite(sprites[s]);
                if (firstSprite == null) firstSprite = sprites[s];
                added++;
            }

            if (firstSprite != null)
            {
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    var mat = psr.material;
                    if (mat != null)
                        mat.SetTexture("_MainTex", firstSprite.texture);
                }
            }

            Debug.Log(
                $"[ObstacleFX] ParticleSystem configured. " +
                $"system={ps.gameObject.name}, addedSprites={added}, finalSpriteCount={textureSheet.spriteCount}"
            );

            ps.Clear(true);
            ps.Play(true);

            Debug.Log(
                $"[ObstacleFX] ParticleSystem played. " +
                $"system={ps.gameObject.name}, isPlaying={ps.isPlaying}, particleCount={ps.particleCount}"
            );
        }
    }

    private static void PlayWithFanBurst(GameObject go)
    {
        if (go == null) return;
        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
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
            TileType.Gear => new Color(1.00f, 0.78f, 0.25f, 1f), // sari
            TileType.Core => new Color(0.95f, 0.30f, 0.30f, 1f), // kirmizi
            TileType.Bolt => new Color(0.30f, 0.60f, 1.00f, 1f), // mavi
            TileType.Plate => new Color(0.35f, 0.85f, 0.45f, 1f), // yesil

            // fallback'ler
            TileType.LineEmitter_H => new Color(0.95f, 0.30f, 0.30f, 1f),
            TileType.LineEmitter_V => new Color(0.30f, 0.60f, 1.00f, 1f),
            TileType.PatchBot => new Color(1.00f, 0.78f, 0.25f, 1f),
            TileType.SystemOverride => Color.white,
            TileType.Normal => Color.white,
            _ => Color.white
        };
    }
}