# Covered multicell origin: orphaned Grass image

The user reported Grass remaining visibly on the board after a LineV hit, although it no longer existed in gameplay state. Reproduced with the level 212 PaintCanBox origin 74 and footprint cells 74, 75, 83, 84, each covered by an individual Grass obstacle.

## Cause and change

Revealing cell 83 requested a view for the underlying PaintCanBox origin 74 while origin 74 still contained Grass. `ReinitRestoredBeneathOrigin` guarded HP initialization by identity, but emitted `RequestObstacleViewCreate` outside that guard. `GridSpawner.HandleObstacleCreatedDynamic` consequently promoted the pre-rendered paint image as Grass and overwrote the original Grass image's tracking entry. A later hit destroyed the paint image under that entry and left the original Grass image orphaned.

The state service now returns before both initialization and view creation unless the requested obstacle ID and origin are exposed at the origin cell. The existing destruction-before-reveal ordering stays intact. No board scans, allocations, new images, audio, level edits, or renderer cleanup heuristics were added. The skipped renderer work is an expected saving; device performance was not measured.

## Verification

- `csi Tools/check_covered_origin_view_reveal.csx --before`: reproduces paint being deleted as Grass and the original Grass image surviving untracked, using the saved previous gate and actual current GridSpawner creation/destruction methods.
- `csi Tools/check_covered_origin_view_reveal.csx`: passes 413 assertions across all 24 orders of the four cover-clear callbacks, including both vertical orders. Checks covered-origin suppression, live cover ownership, preview reuse, final cleanup, invalid origins, and remaining-hit forwarding.
- `csi Tools/check_paintcan_cover_reveal.csx`: passes 26 checks for the previous full paint pyramid fix, including 9 → 8 → 6 → 0 and preserved partial counts.
- `csi Tools/check_rhodes_final_event_order.csx`: passes 6 existing event routing checks.
- `git diff --check`: passes.
- All Settings files match the hashes captured before this change; see the adjacent validation JSON.

These are isolated production-method checks with Unity doubles and deterministic callbacks. They do not run a board simulation, Unity build, Play Mode, or device gameplay. In-game visual confirmation requires restarting the level and testing LineV against the covered paint boxes. An already orphaned image in an existing running board is not retroactively removed by this change.
