# Invisible tiles during fast gameplay — 2026-10-31

Report: level 166, a Lily swallowed/released a special; a cell still contained an Override but its artwork was absent. The player also observed a normal tile disappearing and a similar gap in a different level without Lily.

## Findings and changes

Two visibility races reproduced using the production methods with UI doubles:

1. `OverrideSpecialView` saved `Image.color.a` when hiding the ordinary icon for its rotating halves. Special gather/reveal also writes that alpha. If the turn started while gather had alpha zero, the turn could later restore zero after gather had revealed the tile. The static icon then remained invisible. The turn now hides through its own `CanvasRenderer` multiplier and restores only that multiplier. It leaves the gather/clear fade and input graphic enabled state alone. Stop, disable and external combo cleanup release the multiplier idempotently.
2. `TileView.FlashWhiteSpark`, used by PatchBot impacts, captured and restored the entire RGBA color. A flash sampled during gather could restore alpha zero to a surviving **normal tile** after its reveal. Conversely, a flash started before a clear could reveal a source that should remain hidden. Flash now modifies RGB only and preserves the current alpha on every write, including restarts and completion. Icon replacement and pooled visual reset cancel the old flash so it cannot recolor new content.

Lily landing was also hardened: reject consumed combo sources, pending/airborne falls and unfinished creation visuals. Arrival validates both the saved tile lifetime and the original reserved destination, rather than accepting the same tile at another cell. Invalid arrivals retain the stored special, release the original reservation/hold, and retry through the existing idle/change path. Swallow eligibility uses the same settled-tile check. The existing creation-playing flag now includes background gather sorting ownership.

Reviewed the shared pool acquire/release reset, delayed clear/formation lifetime guards, continuous fall move-token ownership, and detached PulseCore cleanup. No additional unproven movement changes or periodic board-wide repair scans were added. Level assets, move/hit/reward rules and audio are untouched by this change.

## Verification

Commands run directly with `csi`; no Unity build, Play mode or device replay:

| Check | Result |
|---|---|
| `Tools/check_tile_visibility.csx --before` | Reproduces both alpha races from captured pre-change methods; 810 failures among 812 synthetic interleaving checks. |
| `Tools/check_tile_visibility.csx` | 816 checks pass: Override/gather overlap; flash/gather overlap for normal tiles; flash/clear overlap; restarted flashes at 15/30/60/120 FPS; icon replacement cancellation. Also parses all three changed runtime sources. |
| `Tools/check_waterlily_response.csx` | 582 checks pass, including unsafe landing targets, pending motion, moved destination, preserved stored special and teardown. Updated pre-existing test doubles for the current central audio API. |
| `Tools/check_override_turn_frames.csx` | 1,887 animation/pool checks and 11,138 combo checks pass, plus prefab/frame-reference checks. Visibility assertions now measure combined image and renderer alpha. |
| `Tools/check_tile_lifetime.csx` | 30 checks pass for pool lifetimes, stale pop cancellation and clear handoff. |
| `Tools/check_pulse_visual_lifetime.csx` | 620 checks pass for PulseCore visibility, ownership and pooling. |
| `Tools/check_presentation_lifetime.csx` | 266 queued presentation lifetime checks pass. |
| `Tools/check_continuous_fall.csx` | 125 checks pass; eight integration sources parse. |

Before fixtures are under `Tools/Fixtures/TileVisibility/`. Tests exercise real extracted production methods with scheduler/UI doubles; they do not prove the exact reported device sequence or GPU rendering.

Performance review: no additional per-frame board scans, pools, GameObjects, materials or coroutine allocations. The existing flash coroutine remains; it is canceled when its content/lifetime ends. Override reuses the existing Image/CanvasRenderer. Lily adds constant-time checks within the existing bounded target search, never a new per-frame search. No device FPS or allocation profiler measurements were made.

Remaining validation: replay 166 with repeated Lily release, rapid PatchBot/special chains and normal-tile cascades on the reporting device. The proven alpha races are fixed; the report itself was not directly reproduced in Unity.
