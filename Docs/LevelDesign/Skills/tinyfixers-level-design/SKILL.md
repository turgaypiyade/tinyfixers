---
name: tinyfixers-level-design
description: Design and revise numbered TinyFixers production levels, review board compositions, and capture the user's level-design feedback in this Unity repository. Exclude unrelated gameplay coding and generic image generation.
---

# TinyFixers level design

Resolve the repository root by `Assets/_Project/Settings/LevelCatalogPro.asset`. All paths below are relative to that root. This workflow preserves feedback across sessions; it does not train model weights.

## Resume and choose

Read `Docs/LevelDesign/Design-State.md`, `Design-Principles.md`, and the relevant entries in `Design-Feedback.md`. Use the existing levels through 170 as the primary local visual reference pool; the user considers their appearance successful. Read the image index at `Docs/LevelDesign/References/Index.md` and open only relevant examples. Preserve user-approved levels. Do not redirect the active design every time a new reference arrives. Feedback-only references teach visual grammar; they do not authorize redesigning an approved level or copying another game's mechanics.

Resolve the requested level by catalog number, not asset filename. Inspect current `ObstacleLibrary.asset`, `LevelData.cs`, `Obstacle-Design-Guide.md`, and nearby levels before choosing obstacles. Old summaries can miss newly added families. Distinguish new mechanics from color variants. Consider the whole unlocked pool, not just the newest obstacle or ordinary crates.

Keep every level's Turkish name in `Assets/_Project/Resources/LevelNames.json`, matched by the real catalog `level` and `levelKey`. Preserve existing names unless the user requests a rename. Add a name for each new level; keep its design-note title, preview and `Docs/LevelDesign/Level-Names.md` consistent. Names are persistent product data for later UI use, separate from a draft batch's rollback files.

Form a clear visual composition and player decision before authoring: what shape is visible, which region opens first, and what that unlocks. Consider a few distinct concepts internally and choose without requiring approval for routine design choices. Check repetition across the existing catalog, then inspect the closest historical boards as well as nearby levels. A long gap since an obstacle's last use does not establish novelty.

For a repetition review, use `Tools/review_level_patterns.py --candidate <catalog-number> --output Docs/LevelDesign/Reviews/<review-name>` to inventory authored geometry, layers and families without changing assets. Inspect the shortlist visually; compare obstacle silhouettes both at board position and cropped to their own bounds, including reflections. Small versus large versions of the same staircase can evade a board-position-only comparison. Check the open work area, first player decision, and what each cleared layer unlocks. Scores are shortlist aids, never a percentage of novelty or user approval. See `Docs/LevelDesign/Reviews/181-Repetition/Review.md` for the rejected staircase and its historical counterparts.

## Visual grammar

Crates and blockers should complete columns, bands, contours, tiled regions, or recognizable shapes. Detached rectangular crate islands surrounded by normal tiles were rejected. A coherent checkerboard using two obstacle families is valid; scattering unrelated blockers is different. Holes and solid areas should explain the composition. Frames, steps, broad bands, and full columns are options, not mandatory templates for every level.

Preserve wide useful match areas where compatible with the concept. Mechanical variety must have a readable hierarchy and progression; adding many types is not itself creativity. Avoid recurring chest/mud/lantern/statue wings. A reference with a solid lower checkerboard or functional perimeter can inspire a different composition using our supported obstacles. Do not infer the behavior of glass-like cells, colored balls, or decorative objects from appearance alone.

Theme: repair animals restoring World Wonders. New obstacles are considered alongside older ones. Do not convert every level into another wooden-crate level.

## Author and verify

Follow the project design principles for liquid bases, layers, helper goals, introductions, and Boss Duel dimensions. After 170 normal levels should also challenge; endings 5 are Hard and endings 9 Super Hard. A 40–50% pass-rate direction is a target until measured, not an established result.

Check arrays, masks, origins/footprints, goals/layers, colors, refill and special access, catalog references, and preservation of earlier levels. Starting pins are a deliberate design tool; do not disguise an unreachable refill route. Check the actual special implementation if it must cross holes. Do not run Unity builds, bot/cascade simulation, or unrequested project builds.

Use existing tile/effect pools. Review churn, allocations, repeated scans, cancellation and teardown for chosen mechanics. State existing costs and unmeasured limits without claiming measured performance gains. Normal level authoring should not require new runtime/VFX code.

Render using current project sprites, inspect the actual PNG, and correct visual fragmentation before delivery. Static correctness is separate from visual quality and measured balance. Label illustrative random colors honestly. Deliver the level asset/catalog update, a concise design note, and preview.

## Learn and leave a checkpoint

Record feedback with source/user wording, accepted/rejected status, concrete reason, reusable lesson, and scope/exception. Do not label references or drafts as user-approved, or unplayed levels as balanced. Retain rejected cases as counterexamples.

Update only the appropriate record: reusable workflow here; evidence/preferences in `Design-Feedback.md`; mechanics in `Obstacle-Design-Guide.md`; current approval, asset, validation limits and next work in `Design-State.md`. Copy supplied reference images before temporary attachments disappear. Consolidate repeated lessons rather than adding conflicting rules.

Resume from the short state file next time. Do not silently redesign approved work or start the next level without a request.
