# Rhodes last-hit disappearance — follow-up, 2026-10-31

The player reported that the final statue still vanished instantly after the earlier two-half VFX change.

The VFX method itself passed isolated motion/pool tests, but it was called only from `GridSpawner.HandleObstacleVisualChanged`. Actual damage emits `ObstacleStateService.OnObstacleDestroyed` first. The spawner's destruction handler removed the tracked Image, so the later visual-change callback returned before starting the halves. The earlier test did not cover this event order.

`GridSpawner.TryPlayRhodesFinalBreak` now captures and launches the detached halves from either event path, before the source is hidden/destroyed. The winning handler removes tracking; later/repeated notifications cannot emit duplicate halves or remove a replacement obstacle. It uses the same shared scatter/pool driver; there are no new animation objects, per-frame scans or per-piece coroutines in this follow-up.

Verification:

- `csi Tools/check_rhodes_final_event_order.csx --before`: the saved old destruction method reproduces the missing halves.
- `csi Tools/check_rhodes_final_event_order.csx`: six production routing checks pass (destruction first, repeat event, stale obstacle ID, unavailable VFX cleanup, visual first).
- `csi Tools/check_obstacle_scatter.csx`: Unity API metadata check without emit and 1,992 combined behavior checks pass, including two full-size halves, ballistic motion, source destruction, pool return and cancellation.

No Unity build/Play or device rendering was run. The exact missed event path is corrected; the phone visual remains unverified.
