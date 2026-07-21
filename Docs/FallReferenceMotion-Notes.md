# Fall Reference Motion Notes

Current status: the reference-frame fall/spawn experiment is kept in the codebase, but it is disabled at runtime.

- `BoardController.UseReferenceFallMotion` currently returns `false`, so the old stable fall path is active.
- The reference motion attempt must not be mixed with the existing segmented/diagonal collapse runner as a partial hybrid.
- The risky cases to validate before re-enabling are diagonal slides, segmented board paths, special creation/trigger cascades, boss-duel levels, and blocked/overlay cells.
- `LevelP_0060` is a useful regression level because it has diagonal slide pressure. Add temporary specials in memory and verify that pieces never overlap, input never opens early, and `RunAfterIdle` does not stay busy.
- If revisiting, start from the current collapse path semantics first, then rebuild only the visual timing layer around that complete path.

Reason for leaving it off: the partial reference implementation caused stuck/busy states and overlap around diagonal movement. The stable production behavior is the previous fall/collapse flow.
