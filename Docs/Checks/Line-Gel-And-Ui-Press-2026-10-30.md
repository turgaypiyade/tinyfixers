# Line gel propagation and press sounds

## Confirmed gel defect

The native single-line sweep already explicitly contaminated its targets when its own source carried gel. Fused crosses, simultaneous implant lines and virtual carried lines relied on the mutable move-wide gel scope instead. A target previously classified as a clean cascade could suppress that scope, and a delayed clear could outlive it. The source's gel then failed to appear on some hit tiles or reach an arrival special.

The fix snapshots the source pair before the normal swap partner can clear/reenter the tile pool. The resolver passes that boolean into line/cross/payload runners. Each relevant sweep explicitly marks its actual clear targets and arrival specials. Real simultaneous lines snapshot each source before any target marking; wet/dry intersections retain gel without retroactively contaminating a dry emitter based on collection order.

Source rules remain unchanged: an exposed source gel cell or contaminated source carries gel; merely crossing gel does not contaminate a dry emission. Mask holes, caged tiles, obstacle-owned movable tiles and protected cells keep their existing exclusions. Sweep geometry, timings, damage and activation order were not changed.

`csi Tools/check_line_gel.csx` reproduces the omission using fixed before-change sweep excerpts, then checks current production methods: **560 before / 564 after checks** across H/V, single/cross/real/virtual paths, both source sides, hidden seeds, clear-scope reset, previous clean-match exemptions, source-view loss, arrival inheritance and wet/dry intersection order. `check_pulsepulse_gel.csx` remains **68 before / 72 after**, and combo source-visibility regressions also passed. These are isolated board doubles, not a Unity/device run.

## Reusable press sound component

`UiClickSound` now supports `playOnPress` and an optional `clipOverride`. It handles the left pointer-down event directly and plays through `GameEventSfx.PlayUiClip` / `PlayUi`. The central release classifier skips press-owned buttons, preventing a second sound on release. `Mode.None`, non-interactable buttons and disabled components remain silent; central sound settings and mixer routing still apply. Existing release-based buttons retain their behavior.

One component is serialized on each target:

- MainMenu `LevelButton` (level selector).
- MainMenu `RepairButton`, displayed as **MISSIONS**.
- `PreLevelSpecialPopup` prefab `ContinueButton`, also used by retry instances.

All three use the current central UI confirmation clip. Prelevel's optional custom `continueSfx` overrides the component clip when assigned, and its old click-time playback is skipped for a press-owned button.

To reuse: add `UiClickSound` to a button, enable **Play On Press**, select a mode, and optionally assign **Clip Override**. No local audio source is needed.

`csi Tools/check_ui_press_sound.csx`: **10 production component/classification checks** cover immediate dispatch, release deduplication, central clip routing, sound settings, disabled/None/inactive controls and legacy release fallback. Serialized target/component IDs and shared clip references were also checked. No Unity/player/project build was run.
