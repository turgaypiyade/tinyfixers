# Session checkpoint — Dimerdal work completed, device replay pending

Continue from `Docs/Checks/Dimerdal/README.md` for implementation and validation details. The earlier proposal to hold settled landing cells was superseded by the user's explicit instruction: production joins the flowing board, replaces airborne colored stones in place and preserves their motion. Do not reintroduce a landing hold, grid snap, lifetime reset or stationary-only target restriction.

Implemented:

- Open boxes produce from matching-color groups and special/tool strikes of any color. Four opening hits remain separate from production.
- KeyGenerator-inspired rise/arc production follows live TileView identity and can replace airborne stones without changing fall lifetime/token/route/speed/state/bump. Special/clearing/swapping content and foreign reservations remain protected.
- PatchBot may keep a target after it becomes a diamond; actual intent/targetability methods are covered by focused tests.
- Collection uses the actual TarpStatue `AncientSealCollectFx` (diamond override, pooled main visuals), with common gather/orbit and HUD count on arrival. Collection jobs/cancellation cannot leave a stale level waiting or credit a new HUD.
- Square 1x1/2x2/3x3 variants retained with the user's conditional permission to preserve artwork proportions. IDs unchanged.

User approval: “güzel kurdun oyunu beğendim” refers to221's visual layout. Preserve its8x10 mask, two blue2x2 boxes,24moves,8diamonds,6pins, catalog key and names. This does not establish measured balance or approval of the new runtime fixes. No222 request exists.

Reported screenshot is saved as `User-Freeze-Screenshot.png`. Old lifetime renewal was shown to cancel an actual in-progress motor route before seating; the exact device freeze has not been replayed. Focused tests pass, but Unity PlayMode/device visuals and performance still need user play. No build was requested or run.

Validation commands and limits are in README.md. Tests use actual production methods/motor with Unity doubles and must not be described as a full Unity compile. The previous `remaining` CS0136 failure is retained as a `--before` scope-audit regression case.

Shared workspace caution: unrelated UI/font assets were already dirty. During this task separate CascadeLogic and TileFallMotionSystem changes appeared. Preserve them; this task did not author or revert them. Baseline/hash records separate concurrent edits from authorized Dimerdal changes. No reset, publish or release baseline.

Generated art/level previews belong in `/Users/turgayp/Desktop/Wondi Paylaşım/codex/` when requested. Current approved art/source PNGs remain intact. User dislikes unnecessary computer-use automation for image review.
