# Obstacle scatter — 2026-10-23

User authorized all four candidates discussed after the Wardrobe spread adjustment.

- PaintCanBox: side launch 1.6–2.2 → 2.8–3.5 cells/s; upward launch 2.6–3.2 → 3.4–4.1 cells/s. Existing 0.12 s wobble, 0.75 s flight, spin and gravity remain. Analytical horizontal travel is 2.1–2.625 cells instead of 1.2–1.65. Motion units are converted to the FX root.
- Old 2×2 ColorChest bulbs: copied whole before the source hides, outward ballistic flight for 0.42 s, then existing sprite-based break particles at the landing position. A final bulb survives destruction of the chest. The original immediate burst is a fallback only if flight cannot start. Door animation is unchanged.
- Safe: fragment radial displacement multiplied by 1.65, including existing serialized prefab tuning. Same fragment count, lifetime and ease-out shape. Distinct case-part selection now reuses a shared scratch buffer. Removed the unused per-fragment Update component.
- TarpStatue: loose tag and child counter jump sideways together and spin, then fade. Original duration 0.6 s. Cached graphics and explicit completion/disable resets restore position, rotation and alpha while preserving tint. Statue-to-HUD, chips and final shatter remain unchanged.

## Lifetime and performance

`ObstacleScatterFx` rents existing `UiVfxPool` images and advances every active throw in one board-owned LateUpdate. There is no new coroutine/component per flying piece, no board scan and no per-frame collection allocation. Its list retains capacity; pool/list growth can allocate on first use or at a new concurrency peak. Pool capacity/overflow policy remains the existing one.

Flights are independent of the source obstacle. Board Init and SetLevelData explicitly cancel pending flights, including restarting the same LevelData instance. Owner disable, level replacement, inactive FX parent and driver teardown also return images without triggering delayed bursts. Returns clear sprite references; the next rental resets visual state. Cached single-sprite burst storage is safe because BoardBreakFxService consumes it synchronously.

No new audio paths, level assets, obstacle hit counts or goal behavior were added. Previous Wardrobe work and other existing dirty changes were preserved; `Before/` contains the six touched existing source files as they stood at the start of this task.

## Verification

Command: `csi Tools/check_obstacle_scatter.csx`

- Full driver, UiVfxPool and four obstacle views bind to installed Unity 6000.3.9f1/UI/TMP APIs, with project collaborators doubled; no assembly emitted. GridSpawner and BoardController syntax checked, and scoped caller/reset diffs reviewed.
- 45 isolated checks run the production scatter driver against UI/pool/owner doubles: trajectory/delay, landing-position burst, source destruction, pooling/reuse, concurrent flights, board ownership, level/disable/teardown cancellation and duplicate-return protection.
- 8 isolated checks run production TarpStatue tag methods: outward/upward motion, spin, completion, tint-preserving reset and interrupted cleanup.
- Scoped whitespace check passed.

Unity Play, project builds and device profiling were not run. The travel changes and reduced object churn follow from code and isolated tests; visual quality, clipping at device aspect ratios and measured frame-time improvement still require in-game review.
