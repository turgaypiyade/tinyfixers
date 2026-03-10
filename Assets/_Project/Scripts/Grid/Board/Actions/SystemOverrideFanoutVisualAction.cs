using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SystemOverrideFanoutVisualAction : BoardAction
{
    private BoardController board;
    private TileView origin;
    private List<TileView> targets;

    private bool doSelectionPulse;

    public SystemOverrideFanoutVisualAction(BoardController board, TileView origin, List<TileView> targets, bool doPulse)
    {
        this.board = board;
        this.origin = origin;
        this.targets = targets;
        this.doSelectionPulse = doPulse;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (origin == null || targets == null || targets.Count == 0) yield break;

        foreach (var t in targets)
        {
            if (t == null) continue;

            bool beamReachedTarget = false;

            // 1. Tek bir hedefe ışın gönder
            float beamDuration = board.PlayLightningStrikeForTiles(
                new List<TileView> { t },
                originTile: origin,
                visualTargets: new List<TileView> { t },
                allowCondense: false,
                onTargetBeamSpawned: _ => beamReachedTarget = true);

            // 2. Işın hedefe vardığı anda taşı özel taşa dönüştür (Line/Pulse/Patchbot)
            float timeout = Mathf.Max(beamDuration, 0.08f) + 0.02f;
            float elapsed = 0f;
            while (!beamReachedTarget && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            t.RefreshIcon();

            // Override + special partner: Beam hedefe vardığında hedefteki special kısa bir vurgu alsın.
            var targetSpecial = t.GetSpecial();
            bool shouldPulse = doSelectionPulse
                               || targetSpecial == TileSpecial.PatchBot
                               || targetSpecial == TileSpecial.PulseCore;

            if (shouldPulse)
            {
                sequencer.Animator.PlaySelectionPulse(t, delay: 0f, peakScale: 1.30f, upTime: 0.10f, downTime: 0.10f);
            }

            // 3. Sonraki ışına geçmeden önce ufak bir es (böylece sırayla "tık-tık-tık" çalışır)
            yield return new WaitForSeconds(0.04f);
        }

        // Tüm taşlar dönüştükten sonra özel tetiklere geçmeden önce kısa bir nefes payı
        yield return new WaitForSeconds(0.15f);
    }
}
