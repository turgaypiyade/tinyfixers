# 176 contour revision

User explicitly rejected the current V1 `LevelV1_00001760` layout and asked for a redesign at its existing difficulty. This author resolves catalog176 / `LevelCL_0176`; the historical176 Dolaplı Geçit preview belongs to a different catalog and is preserved.

Original:7×7 rectangle, five red and five green ColorCrates in two disconnected floating bars. Revised:8×8 tapered bowl,54active cells, one connected ten-cell V contour (five cells of each color). Each color is also connected. No grass, extra mechanics, helper or prepared special.

This is the first red/green ColorCrate introduction in active V1 and a Normal ending6 level.26moves, goals5+5, original full3HP crates, name, GUID/meta, catalog, music and every other serialized setting are unchanged. The new-family resting preference for later designs is not globally revoked.

`revise.py --apply` writes only176 geometry arrays and dimensions, with preservation guards. It must not be used after manual user edits without a new snapshot. `--verify` renders previews and checks the current asset without writing the level. The default renders the candidate only. No build or game simulation is invoked.

Validation parses real Unity YAML, verifies arrays/origins/footprints, single connected board and obstacle/color contours, exact unchanged goals/settings/identity and44/44→49/49→54/54feed phases. Old feed analysis is recorded separately. All6107other originalAssets files match the snapshot, including user108 and232 changes and the233/audio work.

`Before/` retains the rejected live asset. `Before-After.png` and `Historical-Comparison.png` use current project sprites and illustrative normal colors; both were opened and inspected. Whole-catalog repetition review is in `../../Reviews/176-Contour-Revision/`. A cropped lower-bowl resemblance with2is acknowledged;178's heavier four-color frame is not copied into the introduction. Scores are shortlist tools, not novelty percentages or measured balance.

Existing editor palette/name/sprite, placement/erase/resize and Goals/PreLevel fallback icons were checked. No new runtime/VFX/audio code or recurring allocations. Existing shared damage/audio/pool/teardown behavior remains in place; performance and pass rate are unmeasured. No UI control, UnityPlay/build, bot/cascade simulation, publishing, release baseline or commit. Manual gameplay test belongs to the user; other levels are out of scope.
