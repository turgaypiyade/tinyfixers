# Hediye Şenliği / Gift Festival

## Entry and tuning

- The MainMenu runtime installer finds the existing `LeftEventPanel` and adds `GiftFestivalLogo.png` there. It does not rebuild the scene or replace existing event icons.
- The production gate opens when level **100 has been completed** (`CurrentLevel.Global > 100`). Winning level 100 starts the player's first UTC cycle, before the level counter advances.
- Each cycle runs **48 hours**, followed by **120 hours off**. Restarting the app or closing the screen preserves the original cycle anchor.
- Each active cycle requires explicit **Join** acceptance. Joining grants no balls. The lifetime-first on-screen tutorial grants **exactly 1 free ball**; subsequent new main-game wins award **exactly 1 ball**. Wins before joining, losses, retries, duplicate win notifications and expired/completed events award none. Existing banked balances are preserved. Banked balls are not lost when a level is lost; only the potential one-ball win reward is listed in the loss popup.
- The boards are **2×2 → 3×3 → 4×4**. Their size, box rewards, completion rewards, timing widths/speed and schedule are editable in [GiftFestivalConfig.asset](../Assets/_Project/Resources/Events/GiftFestivalConfig.asset).
- Prize decks mix coins, jokers, boosters and stars. Fresh boards contain **1/4, 2/9 and 4/16 empty boxes** (nearest whole-box count to 25%). Closed boxes do not reveal their contents. A seed and the complete prize layout are saved once per board, so reopening cannot reroll prizes or empty positions. Old already-paid coin boxes retain their original reward display.
- Board bonuses vary too: the first gives a hammer; the second gives 100 coins and a Pulse Core; the last gives System Override, Paint Bucket and 2 stars. Configure `boxRewards`, `completionRewards` and `emptyBoxFraction` in the event config. Legacy `boxCoins`/`completionCoins` fields remain only for old saves/configs.
- The main-menu logo uses the same **150×150 rect and 1.15 scale** as Gear Cup and Bridge Repair, with its countdown aligned to the ribbon. The scale is set before the shared idle animator captures it.
- Speed increases after each completed event board: indicator traversal takes **0.90 / 0.75 / 0.625 seconds** (about 17% faster after the 2026-11-01 adjustment), while throw/swing/flight playback runs at **1.35× / 1.62× / 1.944×**. The green width is **20%**; its timing window gets shorter through speed. Shot speed is captured for the board being hit, including its final box. The reward/impact presentation keeps its readable 0.74-second duration. Tune `sweepSeconds`, `shotSpeed` and `speedIncreasePerRound` in the config.
- Art references, `stadium.png`, the main-menu logo and TinyFixers Pop font/material are in [GiftFestivalArt.asset](../Assets/_Project/Resources/Events/GiftFestivalArt.asset).
- Closed gift symbols use a 72×76 cell-local box (previously 51×54); opened prize icons use 50×50 (previously 34×34), with larger amount text. Cell spacing, board placement and sprite aspect ratios are preserved on every board size; the cached cell objects are reused.
- The menu logo's ribbon shows only the remaining event time. The invitation reuses `GearCupJoinPopup` with the Gift Festival title, field backdrop and a looping rabbit throw / bear swing demonstration. Its blue **Not now** action is on the left, green **Join** on the right. The popup/hero objects are cached and reused; the silent animation uses a single update and pauses on focus loss or disable.

## Play

1. Each eligible cycle automatically offers the invitation once, after cloud restore/loading completes and the main menu is free of tutorials, other popups and event screens. Safari/Bridge/Gear Cup get their earlier startup slots; Gift Festival waits at least four seconds plus a one-second clear gap. The receipt is saved in the existing cloud-backed event state only once the popup is actually visible. Declining or reopening the app does not repeat it that cycle; the icon remains available for manual entry. Joining also suppresses reminders. `autoShowJoinPopup` controls this behavior.
2. Accept the invitation to join. The stadium intro pans downward and zooms toward the field, then blends into the gameplay background. Tap anywhere to skip it. Input stays blocked until the intro settles.
   On the first introduction only, a blocking spotlight with TR/EN captions and the existing tutorial finger guides one real shot: select the highlighted unopened, non-empty gift, then press VUR!/HIT! when the indicator reaches green. Only this demonstration holds the indicator at green and blocks early presses; all subsequent shots sweep normally. The existing saved prize deck is used unchanged. The grant receipt and completion are lifetime fields in the cloud-backed event save, separate from the per-cycle invitation receipt. Closing before the shot resumes without another grant; spending the shot commits completion with the debit/reward checkpoint. If the event expires before that shot, just the one reserved introduction ball survives into the next joined cycle. Completed tutorials and starting grants never repeat in later cycles. Editor preview uses its own receipts.
3. Select a closed gift box. Selection does not reset or recenter the moving indicator.
4. Press **VUR!** / **HIT!**. The visible indicator position is captured on **pointer down**, including keyboard submit support.
5. Green hits the selected box. Left yellow hits one box below; right yellow hits one above. At the board edge, the direction reflects inward. A yellow hit on an opened box consumes the ball without granting another prize. Red misses the board.
6. The ball debit, opened box and earned rewards are saved before the throw/swing animation. Closing during an animation cannot refund a spent shot or repeat its reward.
   An empty box also opens and consumes a ball, shows **EMPTY BOX! / BOŞ KUTU!**, and pays no box reward. It still counts toward board completion; any completion bonus is awarded separately.
7. Opening the final gift advances to the next board and grants the completion prize once. Earned wallet/inventory rewards survive event expiry. Board progress and unused balls reset at the next active cycle.

The meter uses native `MaskableGraphic` geometry derived from the supplied SVG design, with the **same** 15/25/20/25/15 boundaries used by the hit rules. The SVG originals are retained under `Vectors/`; no runtime rasterization or new vector package is required. Supplied image assets preserve their original aspect ratios; the hit button frame/face use proportional fitting rather than horizontal stretching.

## Editor preview

Open MainMenu and enter Play Mode, then choose:

`TinyFixers > Debug > Gift Festival > Preview (Play, Isolated)`

The preview ignores the level gate and stores its own board/balls under a separate editor-only key. It plays reward presentation without changing the real wallet, inventory or cloud save. `Add 10 Balls (Play)` adds preview balls while preview mode is active. `Reset Event` resets the currently selected preview/production state; use it only deliberately.

`Force Available (Editor Only)` also uses the isolated editor save. Production builds never honor the override. The committed setting is **off**.

Suggested visual checks: stadium opening and skip; selection on all board sizes; all three timing results; pose alignment and ball release/contact; closing/reopening during a shot; no-ball and expired states; Turkish/English labels; short/tall displays and safe areas.

## Persistence and performance

- `giftfestival_state_v1` is registered with `CloudSaveManifest`, including account replacement and progress reset. No raw game data is stored in scene assets.
- Coin/star delivery uses `PlayerWallet.AddCoinsWithCheckpoint` / `AddStarsWithCheckpoint`; non-timed life rewards, including those in older saved prize layouts, use `LivesManager.AddLivesWithCheckpoint`. Life initialization/offline regeneration completes before staging the delivery receipt, then the life balance and receipt are saved together before HUD callbacks. The shared 10-life maximum still applies. The main-menu heart shows at most 5 inside the heart and any extra lives in its badge (5+2 means 7 total). Current default decks award coins/jokers/boosters/stars, without new life prizes; saved layouts retain their actual original prize types. Inventory delivery stages a receipt before calling the existing central reward API. Both line orientations now map to the visible shared line inventory, avoiding the old `prelevel_special_unknown_count` write when a random line reward chose LineV. Opened boxes show their actual prize icon and amount; empty boxes show no reward icon or +0 amount.
- The settings asset does not reference textures. Early-level win hooks do not load the event artwork. The screen is built once, on first open, and reused.
- Sixteen cells, one projectile, one impact glow and eight fragments are reused across rounds/shots. A single unscaled update animates the screen; no per-shot coroutines, repeated scene searches, texture generation or object creation occur in the animation loop.
- Only the meter indicator's transform moves. The frame/zones' geometry is rebuilt for normal UI size changes, not every animation frame.
- Disable/scene teardown removes subscriptions and cancels presentation through component lifetime. Focus loss pauses presentation, while expiry is rechecked against UTC when focus returns.
- Five edited WAVs live under `Assets/_Project/Audio/SFX/GiftFestival/` and are referenced by the central `EventSfxLibrary`. Screen playback uses the existing `EventSfx` eight-voice pool and `GameEventSfxConfig.sfxGroup`; no event-local sources/pools are added. The pool immediately stops/mutes transient sounds when `GameSettings.SoundEnabled` changes and removes its subscription on teardown. Token-owned handles cancel only this screen's voices on close, disable, intro skip and focus loss. Audio milestones are consumed while muted so enabling sound cannot replay stale effects. Red misses omit the bat-contact cue; only newly opened boxes play glass break.
- These are code-level reuse checks, not measured device performance claims.

## Validation

`csi Tools/check_gift_festival.csx`

The isolated harness executes production rules/state/prize-layout code and the real `PlayerStats.RecordLevelCleared` / `MarkCurrentLevelFailed` methods with persistence/wallet stubs: 33,470 checks for timing boundaries (including the 20% green region), every cell/edge on all board sizes, schedule boundaries, 100th-level unlock, explicit joining/rejoining, repeated failures/retries, duplicate win protection, misses, deflections, all 29 positions, empty-box quotas, mixed payouts, stable layouts after reload, interrupted coin/star/item delivery, the potential one-ball loss warning and the one-time tutorial grant/completion across reloads, cycle changes and interrupted reward delivery. It also parses all event C# files. No Unity/player/project build is invoked by this harness. Tutorial visual layout/input should additionally be checked in Unity/on device.

`csi Tools/check_gift_festival_delivery.csx` adds 141 checks executing the real reward service, wallet, lives manager and joker/booster inventories together with event rules/state. It covers two-heart payouts from 0 through 10 starting lives, immediate life-change notifications, restart persistence, interrupted delivery callbacks, initialization saves, both random line orientations, all supported item rewards and no shot refunds or repeat tutorial grants. Dependencies such as Unity, cloud and the ledger transport are stubbed. It does not reproduce or establish the exact cause of a missing-heart display on the older phone build. The older broad `check_mercy_retention.csx` harness currently fails compilation due to unrelated stale stubs (`ComebackBoost`, `PaintBucket`, store availability and exception logging); production code was not changed to accommodate that harness.

`csi Tools/check_loss_paging.csx` executes the production loss popup's render, page-transition and cancel methods with UI stubs: 8,367 checks cover optional bubble/prefab bindings, the three-item cap, 3+1 paging, rapid taps, row reuse and interrupted transitions. The loss presentation's CanvasGroup does not block raycasts; hide/disable cancels its transition and releases the input lock. Device layout/touch verification remains manual.

The open Unity editor imported the assets and completed its normal script reload. Gameplay/visual review is left to the developer.

`AssetSources.json` records the supplied files and hashes. Original pixels are preserved; Unity sprite rectangles describe slicing. Earlier art versions are retained under `References/` and are not referenced by the runtime art asset.

## Audio preparation

`python3 Tools/prepare_giftfestival_audio.py` leaves the Downloads originals unchanged and produces trimmed, filtered, boundary-faded 48 kHz PCM WAVs. Import uses Decompress On Load/preload with additional normalization disabled. Intro remains stereo; the short gameplay effects are mono. The three-second `Giftfestival_intro_v2.wav` replaces the old intro at the same imported asset path/GUID. There is no time stretching or runtime pitch adjustment. The camera settles at 2.6 seconds and the remaining 0.4-second audio tail finishes naturally, with a 120 ms closing fade. Skipping, leaving, focus loss or a new shot still cancels its central audio handle.

Exact edits, source hashes, gains and measured output true peaks are in [AudioProcessing.json](Audio/GiftFestival/AudioProcessing.json). Intro targets -22 LUFS with a -7 dBTP ceiling; Throw -6 dBTP, BatHit -3 dBTP, BallFlight -7 dBTP and GlassBreak -4 dBTP before per-cue mix gains. These are file measurements, not a device speaker loudness measurement.

`csi Tools/check_event_sfx.csx` exercises production central audio playback with source/settings stubs, including mixer routing, source reuse, immediate mute, pending fade cancellation, stale ownership tokens and teardown. New Gift Festival cues use complete, offline-trimmed clips and do not start fade coroutines per shot.

- Invitation: matching blue/green action artwork, description and countdown above the action, batter at the lower-left foreground and pitcher farther back inside the clipped arena. Sprite aspect ratios stay intact.
- The bright green TR/EN gift-selection hint pulses in the shared update while waiting for a target; selecting a box immediately hides it. It is hidden during the intro, shots and closing.
