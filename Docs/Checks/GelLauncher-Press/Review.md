# GelLauncher press presentation

## User decision — 2026-10-22

The press version is a trial pending the user's phone review, not a final visual approval. Keep it for that test. If the user dislikes the phone result and requests reverting, restore the previous GelLauncher appearance and presentation, including level-editor previews. Preserve their level placements and unrelated gameplay fixes.

Pre-press copies of `GridSpawner.cs`, `GelLauncherFx.cs` and `GelLauncherFireAction.cs` are in `Before/`. Use them as comparison sources for a scoped rollback; do not overwrite whole files because later unrelated changes may exist. The editor integration is the `GelLauncherPressPreview.TryDraw` dispatch in `LevelDataEditor.DrawObstaclePreview`. Original legacy art and obstacle-library sprite references were retained.

2026-10-23: the user supplied `Downloads/GelLauncherNavy.png` and explicitly requested replacing only the purple frame. `PressFrame.png` now contains that navy image unchanged; its dimensions, GUID, import settings and runtime/editor resource path are unchanged. The previous purple PNG is retained in `Docs/ArtReviews/Obstacle-Asset-Swaps/2026-10-23/Before/`. Gel and press motion are unchanged; phone-review trial status remains pending.

Implemented the approved two-hit press using the supplied frame, gel and pressure-head PNGs. The source images are copied unchanged. Sprite/UV cropping selects their useful regions at runtime.

- First hit: rotating stripe phase, pressure head travels halfway and holds.
- Second hit: completes the press, briefly shows cracks, releases the gold head and runs the existing gel launch path. Rapid consecutive hits still pass through the halfway pose.
- All four directions use the same layered presentation. The spawner uses the existing 1×2 or 2×1 obstacle footprint.
- Gel remains behind the pressure head; the purple housing stays in front. The striped rod is a procedural UI quad with stationary cylindrical highlights and animated stripe phase, rather than rotating a flat PNG.

## Test location

Catalog **189 — İkiz Gölet Kalesi**, `Assets/_Project/Settings/ProductionLevels/LevelP_001930.asset` (catalog GUID `f32b4f61e7ad4f8b8e2285f3cacad558`). The user reported no active launcher and added one for testing. The earlier origin-array entries alone were insufficient evidence of active launchers. This fix leaves the user's level placement unchanged.

`Preview.html` is an interactive Canvas2D motion approximation using the actual source PNGs; it is not a Unity screenshot or shader-render verification. It supports first/second hits, rapid consecutive hits, reset and four orientations. Browser automation was unavailable, so this HTML was not visually inspected in a browser.

## Validation

- `csi Tools/check_gel_press.csx`: 398 assertions against the production motion class; six C# files parsed.
- `csi Tools/check_gel_launcher_motion.csx`: 2,219 isolated presentation checks, including four directions, frame rates, press-before-launch ordering, pooling, interruption and cleanup. These use UI/board doubles, not board/cascade simulation.
- `csi Docs/Checks/GelLauncher-Press/check_unity_api.csx`: three new presentation classes bind to the installed Unity 6000.3.9f1 UGUI/API metadata; no assembly emitted.
- Scoped `git diff --check`: passed.
- All 610 protected Settings/level-name snapshot entries match. All three copied PNGs match the supplied source bytes.

Unity Play, builds, shader compilation, device rendering and gameplay simulation were not run. Visual acceptance and device performance remain to be checked in Unity/on device.

## User test: missing CanvasRenderer

The first Unity test reported `MissingComponentException` on `StripedRod`. The custom `MaskableGraphic` was added dynamically without declaring a `CanvasRenderer` requirement; initialization failed before the old root image was disabled and the assembled press was configured.

`GelLauncherPressRodGraphic` now declares `[RequireComponent(typeof(CanvasRenderer))]`, so Unity adds the renderer before enabling the graphic. The API check now also validates that dependency declaration; syntax and API binding checks alone previously missed it. There is no new per-hit component creation.

## Level-editor preview

`LevelDataEditor.DrawObstaclePreview` now dispatches the four GelLauncher directions to `GelLauncherPressPreview`. Palette, board and stacked-obstacle badges assemble the supplied frame, gel and pressure-head textures with the runtime chamber proportions and direction. Static rod art comes from the supplied head PNG; the runtime animated shader is unchanged. Optional one-hit previews use the half-pressed pose.

The helper preserves IMGUI matrix/color state, clips the shaft/head to the chamber, caches imported texture references and creates no scene objects, sprites, materials or textures. There are no imported-image edits or level-data changes. The API metadata check includes the new editor helper and syntax checks LevelDataEditor. The actual inspector has not been visually verified; no Unity Play/build was launched.

## Performance and lifetime review

One shared update driver advances only moving presses. Idle presses have no per-instance Update. Motion uses reusable state and a shared list; the shader uses one shared material without dynamic texture writes or per-instance material allocation. The hierarchy is created once per view and reused for pooled fallback bodies. Existing pooled projectile/shard/steam paths remain in use; the live obstacle body retains its existing destruction ownership.

Disable/reconfigure increments a generation counter and unregisters motion. Driver teardown cancels tracked motion. The runtime head sprite is shared and destroyed when the last view owner is destroyed. Audio continues through existing central APIs.

The three uncompressed source textures are shared across launchers and cost approximately 18 MiB in RGBA32 before platform/runtime overhead. No device memory, frame-time improvement or zero-allocation claim was measured.
