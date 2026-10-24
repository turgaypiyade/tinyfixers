# Rhodes statue: order reviewed before effects changed — 2026-10-23

Existing authored order verified in RhodesStatueView and ObstacleLibrary:
1. Remaining 4→3, stage 1→2: intact statue becomes cracked; shield and sword remain.
2. Remaining 3→2, stage 2→3: shield disappears, sword remains.
3. Remaining 2→1, stage 3→4: sword disappears; head/scaffold remain.
4. Remaining 1→0: existing final destruction.

This order and all damage/footprint rules were preserved. Shield/sword were already separate visible child layers, with rotation around authored hand pivots. Previously generic stone-chip particles accompanied all intermediate stages; the outgoing attachment was hidden during stage change without producing its own large fragments.

Changes: first hit emits four spaced, larger existing stone-chip images (0.40/0.46 of statue width, a visual tuning choice, not a measured 3–4× device-size claim). Second hit splits the outgoing S2_Shield art into two large pieces, rendered 1.3× their natural half sizes. Third hit splits S3_Sword into four horizontal pieces at 1.65× natural sizes. Outgoing art is copied BEFORE SetStage replaces/hides it. Duplicate and backward stage notifications emit no debris. Existing final head/scaffold destruction remains unchanged. Surviving sword/shield recoil amplitudes increased 15→34 and 6→22 degrees, duration 0.50→0.65 seconds, with interruption reset.

Only intermediate generic dust is suppressed; central sound playback remains before suppression. Pool/cached slices shared with other obstacle throws; no per-fragment GameObject creation after warm-up. Board teardown returns images and destroys generated slices.

Verification: 94 combined isolated checks in `Tools/check_obstacle_scatter.csx` include production OnHit ordering, capture-before-hide, duplicates, real swing/reset methods and pooled slice ownership. 34 existing rules checks in `Tools/check_rhodes_statue_rules.csx` passed; `ruby Tools/check_rhodes_statue_assets.rb` passed. Full affected views bind to installed Unity/UI APIs with project doubles; no emitted assembly. Source S2_Shield and S3_Sword visually inspected. No Unity Play/build/device profiling.
