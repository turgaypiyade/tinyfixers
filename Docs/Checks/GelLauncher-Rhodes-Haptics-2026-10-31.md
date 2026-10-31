# GelLauncher, Rhodes final split and gameplay haptics — 2026-10-31

Implemented the three requested changes without level/scene/asset edits or a project build.

## GelLauncher

Extracted Lift's existing arrival impact into `BoosterCellImpactPass`; Lift still supplies its own special reservation and subchain launch callbacks. GelLauncher now uses that same pass directly when its cap reaches each cell. Removed its repeated six-hit obstacle loop and per-cell queued actions. Damage context/fallback, multi-cell origin deduplication, per-cell walls/magnet endpoints, unbreakable collectibles and covered special survival follow Lift's rules.

Path cells are held until the shot finishes, like the Lift footprint. Gel is placed on open path cells immediately after their impact; it no longer races ahead through an independent path-paint coroutine. The body footprint's existing two-cell gel delay and press animation remain. Both holds, child routines, projectile and board job are released on cancellation/level changes.

## Rhodes

The final surviving base sprite is split 2×1 into two full-height halves, launched apart/upward and allowed to fall/rotate. The general shatter is bypassed when this starts successfully. The shared scatter driver, pooled Images, board-owned sprite slices and teardown cancellation are reused. Intermediate hit stages, sounds, HP and goals are unchanged.

## Haptics

- LineV/H: a light tick when an actual live tile is cleared by the arrived line, including the fallback clear path.
- LineV/H + Pulse: the custom step/fallback clear callbacks also emit this tick. Empty/protected cells do not emit a tile-break tick.
- Pulse: the existing visible explosion event emits one heavy accent outside combo sessions too; duplicate close events are coalesced.
- Override combos: one opening accent; implant and rocket-launch notifications no longer add taps. Actual spawned special explosions and obstacle hits still use their own feedback.
- All successful obstacle hits through the central board damage API emit a light tick. Rejected damage does not. The hamster's direct scripted departure is not an attack and is unchanged.

The central 60 ms gate combines concurrent/very close requests, so multiple broken cells in one frame do not create multiple device commands. Normal match/fall/menu events do not gain vibration except for successful obstacle hits. Vibration settings are cached and updated through the existing setting event, avoiding PlayerPrefs reads on every hit. Settings-off and board disable stop active patterns and reset combo sessions. There is no delayed haptic queue.

Removed per-impact managed/native success logs. Android reuses three cached effect objects and one JNI argument array; iOS reuses one transient player per strength and rebuilds after an engine reset. Existing continuous feedback API remains available but line combos now use the arrived-cell ticks. Three cached platform effects/players live for the application lifetime; scene-local VFX remains board-owned.

## Verification

| Check | Result |
|---|---|
| `check_lift_gel_impacts.csx` | 13 production shared impact checks pass. |
| `check_gel_launcher_motion.csx` | 984 production coroutine samples pass across four directions and 30/60/120 Hz, including real body/head handoff, arrival gel, path holds, reuse and cancellation. Updated the older doubles/cases that still expected the removed legacy fallback body. |
| `check_obstacle_scatter.csx` | Installed Unity/UI API metadata binding passes without assembly emit. 1,992 combined behavior checks, including exactly two full-height Rhodes halves, upward/falling trajectory, source destruction, return and cancellation. Existing stub updated for BirdHouse. |
| `check_gameplay_haptics.csx` | 15 production policy checks pass, including settings cancellation and duplicate-event gating. Both the managed policy and actual Android cache branch allocate 0 B over 10,000 warm calls **with platform/JNI doubles**. Eight integration sources parse. |
| `check_line_gel.csx` | Existing 564 current-production contamination/arrival checks pass; before-fixture checks still reproduce the old issue. |
| `check_combo_source_visibility.csx` | 304 production checks pass; updated the reset double for the previous visibility fix's flash cancellation dependency. |
| `check_rhodes_statue_rules.csx` | 34 existing stage/context/footprint checks pass. |
| Native iOS syntax/API check | `clang -fsyntax-only`, ARC, arm64, installed iPhoneOS 27 SDK, project's iOS 15 minimum: passes, no output emitted. |

The old `check_rhodes_statue_assets.rb` references the removed historical `RhodesStatue_Stages.png.meta` and cannot run against today's Resources-based artwork. No art files were changed to satisfy that obsolete check.

No Unity build/Play or device profiler was run. Timing, artwork composition, vibration feel, real JNI/native allocation costs and device FPS still need phone validation; the allocation numbers above describe only isolated doubles. No new per-frame board scans or per-piece Update loops were added. GelLauncher eliminates per-cell action allocations; the shared impact collections allocate once per sweep. Rhodes creates/caches two slices on first use per board and reuses pooled Images.
