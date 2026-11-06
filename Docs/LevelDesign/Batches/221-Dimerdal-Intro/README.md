# 221 — single-color Dimerdal introduction

User request: “şimdi sadece tek rengi tanıştırdığımız 221.levelı yaparmısın”. Scope is 221 only, using one Dimerdal color. This authoring request supersedes earlier historical notes saying 221 had not been requested. It is not user approval of the resulting layout or balance.

Selected design: **Mavi Vitrin / Blue Showcase**; 8×10 / 68 active cells, two adjacent blue 2×2 Dimerdals form the bottom plinth, 60 naturally fed normal stone cells, one Diamond8 goal, 24 moves, four normal stone colors. Six normal-color pins offer an exact blue three-match above both boxes. No other obstacle, prepared special, existing diamond, cover or refill spawning is added.

Other internally considered compositions were a free central single box and a four-box panel. The first would leave an unexplained island; the second would overemphasize footprint and multiple producers during the introduction. Two adjacent bottom boxes keep a clear top matching face and finish the board silhouette.

The current Dimerdal code and library were read before authoring: four opening hits, subsequent adjacent matching group produces one colored board diamond, collection later advances the shared collectible goal. Same group touching both open boxes still produces one diamond. Permanent frames remain blockers; no passage or box-break goal is assumed. Existing layered-view/state/audio/pool behavior is reused; no runtime or VFX change is part of this level task.

`Before/` saves the catalog, names and design records before the new entry. `protected-before.json` hashes current Settings, Scripts, Resources, Dimerdal art and tutorial data excluding only the catalog and level-name registry. It includes the uncommitted Dimerdal implementation from the preceding task and preserves all existing levels, including the user's211 moves24 and the approved212 sail mask.

Subsequent user-reported compile fix: `authorized-code-fixes.json` permits only the exact old/new hash of `ObstacleStateService.cs` after renaming the conflicting Dimerdal local `remaining` to `dimerdalRemaining` (`CS0136`). The original baseline stays intact. The verifier accepts that recorded code-only correction and still checks every other protected hash; the221asset and other levels remain unchanged.

`design.py` creates 221 once and refuses to overwrite it. `verify.py` checks asset/model agreement, footprint, colors, pins, goals/icons, feed routes, append-only catalog/name changes and every protected hash. `render.py` uses actual imported project sprite rects and mirrors DimerdalView's layer placement. Its free colors are illustrative, with exact authored pins marked.

Repetition inventory scans the 220 prior active V1 levels. Closest board-position blocker score is216; cropped rectangles naturally match many older compact blocks. These are shortlist metrics, not novelty percentages. Visual assessment is in the review note. No previous level was redesigned.

## Validation

```sh
python3 Docs/LevelDesign/Batches/221-Dimerdal-Intro/verify.py
python3 Docs/LevelDesign/Batches/221-Dimerdal-Intro/render.py
python3 Tools/review_level_patterns.py --candidate 221 --catalog Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset --output Docs/LevelDesign/Reviews/221-Dimerdal-Intro
```

The pin inspection checks static authored geometry only; it does not execute a board move. All 60 normal cells have a clear vertical spawn route even before opening, so the visual does not fill imaginary shadow pockets. Current BoardController/BoardInitService source accepts normal pins before random match avoidance; the pinned tiles do not need a gravity simulation to establish their authored positions.

Initial move/goal choice is intentionally approachable after220Boss; no claim of measured balance, user play approval, FPS or win rate. No Unity/project build, PlayMode, device play, bot/gravity/cascade simulation, release baseline or remote publication. 222 is not authorized by this request.
