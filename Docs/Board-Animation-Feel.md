# Board animation feel

## Selected tile lift

The first selected/dragged tile grows to 1.20 scale while crossing its swap partner. The other tile stays at its resting scale. The existing foreground ordering and swap duration remain in use, including invalid-swap return travel. `BoardController.swapLiftAmount` controls the amount; the game scene explicitly uses 0.2.

The movement coroutine applies a smooth rise during the first 18% of travel, holds through 64%, then settles over the remainder. Top-left root pivot compensation keeps growth centred across the icon, propeller and Override child views. Arrival callbacks see the normal movement position before the cosmetic offset is applied.

Horizontal travel previously used only the Y distance to detect movement, which made ordinary horizontal swaps snap immediately. It now uses the full two-dimensional distance. New movement, cancellation, grid snapping and pool reset clear the lift. Lifetime/token guards prevent an old swap finishing against a reused tile; `SwapAction` also avoids restoring Idle over a new owner or a falling tile.

## Special creation review

Creation already includes gather, scale overshoot and halo effects. This change retains their timing and the approved Override turn animation. The generic reveal now retains one disabled halo Image/Canvas child per TileView after first use instead of creating and destroying it on every reveal. The active-reveal reference is cleared separately, so an inactive cache does not keep the tile flagged as revealing. Cleanup disables the child without reparenting during OnDisable; destroying the owner also destroys the cache.

The gather and generic reveal paths can both write icon scale during formation. Coordinating those paths is a separate candidate for visual tuning; this change does not replace their choreography. Gather contribution ghosts also remain an allocation source. The halo reuse is an expected reduction in repeated object churn, not a claim of zero cascade allocations.

## Verification

- `Tools/check_swap_lift.csx`: 1,617 assertions execute production movement and SwapAction code with Unity doubles at 30/60/120 fps. Checks cover four directions, centred growth, selected-only peak scale, unchanged duration, exact arrival, reverse travel, cancellation and stale pooled lifetimes.
- Dynamic input harness: 21 checks passed.
- Source/API diagnostics: 618 C# files, zero errors, no assembly emit.
- No Unity build, visual playtest, device profiling or Frame Debugger measurement was performed. The final motion feel should be assessed in Play Mode/on device.
