# ColorChest under Grass: missing bulbs

The user reported level219's covered ColorChest opening empty despite still taking damage. The prefab and all four bulb children already existed in the beneath preview. `HandleObstacleDestroyed(Grass)` correctly retired the cover image but unconditionally removed `_chestViews[originIndex]`, which belonged to the chest beneath that same origin. `HandleChestOpened` and colour callbacks could no longer find the view; the bulbs stayed hidden.

`GridSpawner.HandleObstacleDestroyed` now removes that typed registry entry only for an actual ColorChest destruction. Preview promotion reuses the existing prefab and child images. No extra scene searches, components, textures, view rebuilding, allocations, audio paths or update loops were added by this fix. Normal chest destruction still releases the registry; scene teardown still clears it. Existing cabinet door/VFX costs remain unchanged and were not device-profiled.

Verification uses actual production destruction, preview promotion, chest-open routing, `ChestObstacleView.OpenCabinet`, `ShowAll` and bulb visibility/removal methods, with Unity doubles for prefab children and door effects:

- `csi Tools/check_colorchest_cover_reveal.csx --before`: reproduces all four bulbs remaining hidden after Grass unregisters the chest.
- `csi Tools/check_colorchest_cover_reveal.csx`:384 assertions across48 cover-clear orders (two219 origins,24 orders each). Checks four visible bulbs on opening, partial visibility after a colour clears, late cover events, preview reuse, actual chest retirement and repeated cleanup.
- Existing covered-origin view test:413 assertions /24 orders pass.
- Existing PaintCanBox pyramid test:26 checks pass.
- Existing Rhodes final-event routing test:6 checks pass. Its double enum and the covered-origin double enum include ColorChest for the new production branch.

This is a runtime presentation fix, not an edit to level219's mask, goals or obstacles. The concurrent authorised level revisions are documented in `Docs/LevelDesign/Batches/213-220-Showcase/Revisions/v2-lamps-launchers/`. Tests run isolated methods, not a Unity build, Play Mode, device session, gravity/cascade or bot simulation. Restart the level to obtain fresh prefab registry state; an already-unregistered view in an active board is not retroactively repaired.
