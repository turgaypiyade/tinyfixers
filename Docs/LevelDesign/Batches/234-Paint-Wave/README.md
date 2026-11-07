# 234 — Paint Wave

**Active revision:v2-layered.** The user liked the74-cell mask but playedv1andfound it too easy. V2preserves the grid,24moves,name,GUID/meta,catalog andnames. Only234is in scope.176was completed by the user and must not be revised.

Idea was shared before placement:wave silhouette and an edge workshop that reveals another task. User-approved workflow lessons are inMemory/Design-Principles/Design-Feedback and the level-design skill:idea/style first;disappearing edge contours;automatic fill by default,ordinary starting tiles only where natural opening fill cannot reach.

V1had2ColorChests over2PaintCanBoxes plus6blue/6red single-hit plastics. The played-easy version is preserved in `Revisions/v2-layered/Before/`.

V2has2ColorChests over2PaintCanBoxes and16three-layer cells:3-hit SculptingStone→2-hit PlasticTwoStage→2-hit Mud.12cells line the lower left edge;4cells fill the right shoulder. Goals2/2/16/16/16,five slots. The right shoulder adds a second work face so one left-column effect does not represent all required work. No grass ornew mechanic. No move-count reduction.

49of50starting normal slots feed naturally. Index32is the only real ordinary Gear pin,with a covered support cell below. Underlying paint remains a temporary one-cell roof;after it clears all applicable slots feed naturally. Full phase details are in `validation.json`. No prepared special orfully pinned workspace.

`revise_v2.py` writes only234andrefuses external asset drift. `author.py` is the historicalv1writer andnowrefuses to run when anactive revision exists. `review_v2.py` parses actual Unity YAML,compares full mask/unchanged fields,checks all3-layer cell lists,footprints,5goals,real sprite refs,stage reachability and6109othercurrentasset hashes. `review.py` dispatches to the active verifier. `render.py` renders the current model;opening/cutaways were inspected. Free normal colors are illustrative.

Whole-catalog repeat inventory is `../../Reviews/234-Paint-Wave-v2/`. V1resembled earlier two-column edge arrangements;v2still shares the user's accepted mask andedge grammar,anddoes not claim uniqueness ormeasured balance. User's manual test was performed onv1;v2awaits testing. No new runtime,Update,audio source orVFX system;existing creation/teardown/pool costs remain unmeasured.

The requested bitten-apple sprite replacement is a separate completed task at `Docs/Checks/FlyFruit-Bitten-Apple-2026-11-07/`. Its newbytes are already in this revision's preservation snapshot. No UnityUI,Play/build,bot/cascade simulation,publishing,release baseline orcommit.235is not authorized.
