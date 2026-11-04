# Covered procedural contents — 2026-11-04

User report: a Grass-covered GelLauncher appears empty and should show its own full contents when Grass opens. The user asked for the same audit on PaintCanBox, ColorChest, Wardrobe and other procedurally assembled obstacles.

## Findings and changes

`DrawStampedBeneathVisuals` temporarily stamps the beneath obstacle ID/origin into the level arrays. The origin's live HP still belongs to the cover. The previous `GetObstacleIdAt(...) == desiredId` check therefore passed, even though `GetRemainingHitsAt` returned Grass HP1. A fresh GelLauncher was configured as damaged. The actual production press pose drew38.5% chamber liquid instead of its unpressed63% pose. LaserCannon, RhodesStatue and TarpStatue had the same identity/HP assumption.

`ResolveObstacleViewRemainingHits` now centralises the existing saved-layer query keyed by ID **and** origin. First construction and true beneath-preview promotion call `ConfigureObstacleContentView`; Ensure reuses existing children and reapplies actual layer HP. A saved damaged layer remains damaged. This is presentation-only: these methods do not mutate gameplay HP or make a fresh copy of a depleted layer.

| View | Result of audit / change |
|---|---|
|GelLauncher Up/Down/Left/Right|Own layer HP drives actual Configure/ApplyPose; full/damaged press pose is retained on preview reuse. Generic root Image remains disabled; assembled children render.|
|PaintCanBox|Existing saved-layer fix uses the shared resolver; promotion updates the existing pyramid. Real9→8→6→0 and partial-state tests still pass without another build of the pyramid.|
|ColorChest|Prior typed-registry guard remains. Promotion keeps the existing cabinet and bulbs; it does not call OpenCabinet/ShowAll again and resurrect consumed bulbs.|
|Wardrobe|Construction starts closed; item creation happens through HandleWardrobeOpened → actual OpenDoor. Grass destruction does not unregister it. The production opening route with eight supplied test sprites installs all eight; promotion does not rerun OpenDoor or duplicate items. Actual owner retirement now drops the typed registry.|
|BatteryBox|A second cover-cleanup bug was found: Grass also removed its `_batteryBoxViews` entry. Only actual BatteryBox destruction now removes it, preserving subsequent per-colour battery callbacks.|
|OverrideBatteryBox|Typed registry is removed only for actual owner retirement. Its existing special reveal/recreation path and detonation ownership remain.|
|LaserCannon / RhodesStatue|Construction/reveal pass actual own HP to existing Ensure APIs, rather than cover HP; service registration remains on the existing board.|
|TarpStatue|Service Register also uses the saved-layer query; underlying statue hit stage is no longer read from the cover. Its separate colour/product count remains service-owned.|
|Safe / AncientSeal / Wall / LampStrip / Windmill|These use separate service renderers or are excluded from generic beneath-preview drawing; the audited temporary-identity HP path is not used. They were inspected, not all exercised in a Unity scene.|

Typed cabinet/battery/wardrobe and pending override maps are cleared when rebuilding the board alongside the root-view maps, avoiding stale references after child teardown.

## Verification

- `csi Tools/check_layered_obstacle_contents.csx --before`: reproduces fresh GelLauncher using Grass HP1 and the actual half-pressed pose.
- `csi Tools/check_layered_obstacle_contents.csx`:103 production-method assertions; four launcher directions, real Configure/ApplyPose/Motion, own full/partial HP, fallback values, exhausted-layer lookup, other secondary-view routing and actual Wardrobe.OpenDoor.
- `csi Tools/check_layered_content_registry.csx --before`: reproduces BatteryBox losing its registry on Grass retirement.
- `csi Tools/check_layered_content_registry.csx`:30 ownership checks for ColorChest/BatteryBox/Wardrobe in origin-first and non-origin-first orders, late cover events, image reuse and repeated actual-owner cleanup.
- Existing covered-origin routing:437 assertions /24orders, including one content refresh only on real promotion.
- Existing covered ColorChest:384 assertions /48orders.
- Existing actual PaintCanBox pyramid:26 assertions.
- Existing Rhodes final-event routing:6 assertions.
- Modified production C# was parsed in these isolated harnesses. No Unity/project build, Play Mode, device session, gravity/cascade or bot simulation was run.

The new harness uses real production methods with presentation doubles; it does not reproduce every Unity rendering/lifetime detail. Restart the level to obtain fresh view state when checking the device report.

## Scope and performance

The source snapshot is in `Before/`. Production changes are GridSpawner.cs and TarpStatueService.cs. There are no new per-frame callbacks, board scans, textures, audio sources, per-cell effects or view pools. Saved-layer lookup walks the small stack at the specific origin; it runs on construction/promotion. Existing Ensure methods reuse built children; the real paint test verifies no repeated pyramid build. Register callbacks use their existing services. Actual hits keep their existing animation paths. Press cancellation/driver ownership and effect teardown stay in the existing view; only reveal synchronisation invokes Configure on the existing view. Scene rebuild clears typed maps after the existing child teardown. No measured FPS improvement is claimed.

The same turn also calibrates213–220 moves only; see `Docs/LevelDesign/Batches/213-220-Showcase/Revisions/v3-moves-2026-11-04/`. Exact non-move asset equality, goals/paths/layers and preserved catalogue/name/meta data were checked. Independent concurrent MasterLeague/menu/backend/test-setting changes were observed in the workspace and recorded in that revision's concurrent-changes.json; this task did not edit those files and does not claim they remained unchanged.
