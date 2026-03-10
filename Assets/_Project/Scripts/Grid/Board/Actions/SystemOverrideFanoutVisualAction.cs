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

            // 1. Tek bir hedefe ışın gönder
            board.PlayLightningStrikeForTiles(
                new List<TileView> { t },
                originTile: origin,
                visualTargets: new List<TileView> { t },
                allowCondense: false,
                onTargetBeamSpawned: null);

            // 2. Işının hedefe "gitme" süresi (görsel his için kısa bir bekleme)
            yield return new WaitForSeconds(0.08f);

            // 3. Işın hedefe vardı, taşı özel taşa dönüştür (Line/Pulse/Patchbot)
            t.RefreshIcon();

            // Override + special partner: Beam hedefe vardığında hedefteki special kısa bir vurgu alsın.
            // PulseCore için ekstra pulse verme; kendi aktivasyon patlama animasyonunu kullanır.
            var targetSpecial = t.GetSpecial();
            bool shouldPulse = doSelectionPulse
                               || targetSpecial == TileSpecial.PatchBot;

            if (shouldPulse)
            {
                sequencer.Animator.PlaySelectionPulse(t, delay: 0f, peakScale: 1.30f, upTime: 0.10f, downTime: 0.10f);
            }

            // 4. Sonraki ışına geçmeden önce ufak bir es (böylece sırayla "tık-tık-tık" çalışır)
            yield return new WaitForSeconds(0.04f);
        }

        // Tüm taşlar dönüştükten sonra patlamadan önce çok kısa bir nefes payı
        yield return new WaitForSeconds(0.15f);
    }
}
