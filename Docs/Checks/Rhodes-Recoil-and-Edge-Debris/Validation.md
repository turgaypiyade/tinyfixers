# Rhodes recoil, larger shield, board-edge debris and stronger torch — 2026-10-23

Explicit feedback: prior shield pieces still looked too small; first hit should shake shield/sword much more; adjacent normal matches should react even without dealing damage. At horizontal board edges, obstacle pieces should fly inward rather than leave the board. Additional request: slightly stronger RodosDevi torch.

## Changes

- Shield half-piece render scale 1.3 → 2.8, a 2.15× linear increase over the previous implementation. Two pieces still use the outgoing S2_Shield art. Shape/order and special-only damage rules unchanged.
- First frame immediately kicks sword to 72° and shield to −56°, followed by 0.90 seconds of decaying recoil (previously sinusoidal startup at 0°, amplitudes 34°/22°, 0.65 seconds).
- BoardController reacts only to rejected NormalMatch requests against Rhodes, using the existing per-origin image lookup. Existing adjacent-match damage collection supplies these requests. No HP, stage or goal changes and no additional board scan. Same-frame requests are coalesced and the initial 0.16 seconds of the recoil are protected from repeated restart. OnDisable still cancels/reset the animation; no new event subscriptions.
- Shared ObstacleDebrisBounds converts board horizontal edges into the effect parent's coordinates, includes a conservative spinning-piece radius, redirects escaping horizontal throws inward and caps travel to available room. Extremely narrow boards can scale oversized pieces to fit. Both eased and ballistic motion supported; anchor offsets are included.
- Applied to shared scatter (Rhodes, Safe, PaintCanBox, ColorChest bulbs, Wardrobe contents/finale), generic obstacle body shatter including Rhodes final stage, cabinet doors, TarpStatue tag/chips/frame and LaserCannon shards. Decorative glows and HUD collection destinations are unchanged. Generic Rhodes dust is suppressed for the final hit too; its existing head/scaffold body shatter and central sound still run.
- Bounds work is constant-time per emitted piece; no new per-frame buffers/scans, and shared image pools/cache cancellation remain intact. Legacy batch coroutines retain their existing lifetimes. Frame-time improvement has not been measured.
- RodosDevi torch retains the same image/region/reveal guards. UV motion increased about 1.6×, flicker/glow increased and two embers slightly enlarged. No new audio, particle objects or CPU animation loops. Original background PNG unchanged. The earlier inline preview represents the previous, softer intensity.

## Verification

`csi Tools/check_obstacle_scatter.csx`: full shared driver/pool/bounds/generic shatter, six obstacle views and WonderReveal sources bind against installed Unity/UI/TMP APIs with project doubles; no emitted assembly. Changed callers/services syntax checked. 1,982 combined isolated behavior checks, including 1,860 numerical trajectory samples covering left/right/center launches, both directions, ballistic/eased motion, full spinning-piece envelopes, narrow boards and anchor offsets. Production rejected-match hook verified without HP loss, with other contexts/successful hits excluded. Actual recoil methods verify immediate full amplitude, later swing, cancellation and neighbor coalescing. Pool cleanup and prior effect behavior checks remain passing.

`csi Tools/check_rhodes_statue_rules.csx`: existing 34 special-only/context/footprint checks. `ruby Tools/check_rhodes_statue_assets.rb`: existing authored-stage/registration checks.

No Unity Play, build, shader compilation or device visual/profile checks were run. These are implementation and isolated math/lifetime results; in-game visual scale remains for review.
