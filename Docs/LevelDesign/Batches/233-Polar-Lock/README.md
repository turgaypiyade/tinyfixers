# 233 — Polar Lock

Only233 was requested. User then said **“çim istemiyorum ama artık”**. All grass variants42/58/69 are excluded; earlier levels are preserved. No new mechanic or runtime code was introduced.

- Production:`Assets/_Project/Settings/ProLevels/LevelV1_00002330.asset`,catalog233/`LevelCL_0233`,chapter10.
-8×11/82active,27moves Normal. Goals8Cargo/2Safe/1Magnet pair/8SculptingSpecial/12Porcelain.
- Two2×2safes hide four cargos each. Opposite lock orders;4hits per safe. Bottom U magnet has12cells,11endpoint advances.8special-only floor cells lie in the cargo's four straight exit lanes.
-42/42starting normal slots have natural feed;no pins or starting specials. Approved-route topology phases feed42/50/62/74/82slots. An alternative early floor clear leaves8temporarily roofed cells until the safes open;the validator records this explicitly.

`author.py` writes only233,the appended catalog row and TR/EN names. `review.py` parses actual Unity YAML,checks arrays/masks/layers/goals/refs and compares preservation hashes. It performs geometric reachability only,no game/cascade simulation. `render.py` uses current project sprites;opening and cutaway were visually inspected. Normal tile colors are illustrative.

`protected-before.json` retains original hashes.6103unrelated original asset hashes match;catalog/names have only233appended.108changed externally during work and its current content was preserved in `External/` and `external-edits.json`,rather than overwriting it or replacing the original baseline.232’s user edits,32moves and5+5+5production,are explicitly verified. New Dimerdal audio and the covered-hit fix are preserved.

Whole active V1 inventory:`../../Reviews/233-Polar-Lock/`. The closest geometry candidate142and magnet example198were compared with233using current sprites in `Comparison.png`. Bottom-heavy masses and two safes are a real similarity with142;mechanical distinction is the independent colored safe releases into one endpoint-driven freight gate. Similarity scores are shortlist aids,not novelty or approval percentages.

Rejected alternatives:another Dimerdal/fruit island would repeat231–232;gel+launcher would revive229's rejected early-cleanup relationship;a full perimeter magnet would require too many endpoint advances;a4×2roof near the top created two unfilled initial cells. The final safes sit above the occupied exit floor,with no initial empty pocket,and retain square readable fronts.

Existing editor palette/name/sprite,size/path placement,erase and Goals/PreLevel routes were inspected;no editor/runtime code needed. Current costs include temporary magnet cage/FX objects and lists;no performance measurements or new runtime allocations are claimed.

Validation:`validation.json`. Audio playback,UnityUI,Play/build,bot/cascade simulation,device FPS/win-rate measurement,publishing,releasebaseline and commits were not performed.27moves is an initial setting;the user performs manual tests.234is not authorized.
