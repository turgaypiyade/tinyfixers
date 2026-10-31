# Special, combo and match VFX performance review

Gameplay targeting, damage, clear ordering, callback scheduling and configured animation durations are unchanged. The changes reuse presentation data and effect images, and consolidate particle updates. No Unity/player/project build or Android profiling was performed.

| Presentation | Change / review result | Preserved behavior |
| --- | --- | --- |
| Pulse Core | Existing explosion-instance pool retained. Procedural sprite cache now uses an explicit value key, eliminating formatted strings and captured generator delegates on warm plays. Three variants per settings key remain. | Explosion radius callbacks, wave timing, generated texture algorithms and random variant selection. |
| Pulse + Pulse | Pooled fuse sparks advance in `UiFxPool` instead of one coroutine per spark. Owner disable and charge completion synchronously release their sparks. | Emission cadence, random draws, drift, size and fade curves, first-frame advancement, charge duration. |
| Override, including mixed combos | Shared `LightningBeam` caches gradient objects/keys lazily and sends each line's same points in one `SetPositions` call. Existing Override turn/link/charge pools and cached sprite generation were reviewed and retained. | Inner colors, outer/inner point geometry, persistent alpha keys, target callbacks, turn/orbit/tether and wave timing. |
| Line H/V, including combo sweeps | Child afterimages are returned before the rocket hierarchy is destroyed. Externally parented tails retain their existing lifetime. Timed impact particle resets use a reusable list instead of an allocated component array. | Swept cells, asymmetric travel, timing, trail fade/expansion and completion callbacks. |
| PatchBot, including carried-special flights | Flight afterimages rent images and fade in the shared driver. Launch/impact sparks use the shared driver instead of individual coroutines. Owner-scoped cleanup prevents stale updates after return. | Flight path, target resolution, carried special, arrival callback, sprite geometry, spark easing/pop/alpha, audio and flight timing. |
| Normal matches and wave implosions | Ring/star/shard bookkeeping uses value records and bounded, independently leased buffers. Explicit enumerator disposal returns pieces and scratch storage. Center calculation uses `TransformPoint(rect.center)` rather than allocating a corners array. | One ring, five stars, seven shards; size, position, rotation, alpha, duration and the tile pop/clear animation. |

`UiVfxPool` remains the common UI object store; no separate effect object pool was added. Scratch buffers are bounded to 128 concurrent retained burst buffers and cleared before reuse. Shared update lists use swap removal. Teardown entry points check the existing driver instead of creating it while disabling an owner.

## Verification

`csi Tools/check_vfx_hotpaths.csx` runs 6,023 checks against current production methods and fixed before-change excerpts in `Tools/Fixtures/VfxHotpaths`. It compares line points/gradients, per-frame charge and PatchBot spark curves, match particle transforms/alpha/duration, owner cancellation, child versus external trail release, warm reuse and procedural cache keys.

Managed allocation measurements use rendering doubles, after warmup:

| Isolated workload | Before | After |
| --- | ---: | ---: |
| 2,000 line geometry / inner-gradient updates | 160,000 B | 0 B |
| 2,000 charge-spark spawn/cancel operations | Per-spark coroutine in production before | 0 B in the shared-driver probe |
| 1,000 normal match burst enumerations | 1,104,000 B | 176,000 B |
| 2,000 warm Pulse Core sprite-cache plays | Formatted keys / captured delegates | 0 B |

Other production-method regressions passed:

- `check_pulse_visual_lifetime.csx`: 620 checks.
- `check_override_turn_frames.csx`: 1,887 turn/charge checks and 11,138 combo motion/tether/wave/pool checks; sprite import checks also passed.
- `check_combo_source_visibility.csx`: 304 checks.
- `check_patchbot_magnet.csx`: 85 endpoint checks.
- `check_pulsepulse_gel.csx`: 68 before / 72 after checks for existing source-order/gel behavior.

The older `check_special_flow.csx` harness could not execute: it currently splices `LivesManager`'s `System.Security.Cryptography` / `System.Text` using directives after type declarations. A temporary copy correcting only those namespaces then exposed stale board/lives doubles (missing `CellHold`, flow-pump members, attempt/review/cloud services). The original harness and gameplay source were not changed to work around these unrelated test-infrastructure issues. The dedicated VFX, combo, Pulse Core, Override and PatchBot checks above passed.

These measurements do not include Unity native allocation, canvas rebuilding, overdraw, rendering or device frame times. First procedural texture generation and first pool growth still incur warmup costs. Full flight/rocket root creation and gameplay action orchestration were retained; the work targets repeated visual particle/afterimage churn and managed per-frame data.
