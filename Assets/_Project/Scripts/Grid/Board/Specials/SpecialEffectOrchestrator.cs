using System.Collections;
using UnityEngine;

/// <summary>
/// Centralized effect trigger hub for special/combo visual side-effects.
/// Gameplay systems should call this instead of directly invoking BoardController VFX methods
/// so visuals remain decoupled from fanout/dispatch logic.
/// </summary>
public class SpecialEffectOrchestrator
{
    private readonly BoardController board;

    public SpecialEffectOrchestrator(BoardController board)
    {
        this.board = board;
    }

    public void EmitComboTriggered(TileSpecial a, TileSpecial b, Vector2Int originCell)
    {
        ComboBehaviorEvents.EmitComboTriggered(a, b, originCell);
    }

    public void EmitComboVisualQueued(TileSpecial a, TileSpecial b, Vector2Int originCell, float duration)
    {
        ComboBehaviorEvents.EmitComboVisualQueued(a, b, originCell, duration);
    }

    public void EmitOverrideFanoutStarted(Vector2Int originCell, TileSpecial targetSpecial)
    {
        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(originCell, targetSpecial);
    }

    public void EmitPulseEmitterComboTriggered(Vector2Int centerCell)
    {
        PulseBehaviorEvents.EmitPulseEmitterComboTriggered(centerCell);
    }

    public void PlayPulseExplosionAt(int x, int y)
    {
        board.PlayPulsePulseExplosionVfxAtCell(x, y);
    }

    public void PlayPulseExplosionDelayed(int x, int y, float delay)
    {
        board.StartCoroutine(CoPlayPulseExplosionDelayed(x, y, delay));
    }

    public float PlayOverrideComboVfxAndQueue(TileSpecial a, TileSpecial b, Vector2Int originCell)
    {
        float duration = board.PlaySystemOverrideComboVfxAndGetDuration(originCell);
        EmitComboVisualQueued(a, b, originCell, duration);
        return duration;
    }

    private IEnumerator CoPlayPulseExplosionDelayed(int x, int y, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlayPulseExplosionAt(x, y);
    }
}
