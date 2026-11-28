# Progressive event art plan

Status (2026-11-26): event packaging, runtime gates, reversible config activation and player-scene reference stripping are implemented in source. The latest iOS build report shows 288.3 MiB of user assets and the iPhone reports 534.5 MB installed app size; this does not meet the roughly 300 MB target. Report review found four legacy Safari map textures under `Resources/RisingEvent` were omitted from the event inventory and remained local. The editor pipeline now includes those files in Safari's bundle and journals reversible moves out of Resources during activation. Rebuild the content manifest/bundles and reactivate before the next player build. This fix accounts for only about 2 MiB in the current build report, so it is not the explanation for the remaining size gap. No size reduction is claimed until a fresh player build is measured.

## Image review: candidates for discussion

I matched asset GUIDs in event Resources configs and `MainMenu.unity`. The main menu scene has serialized references to 19 event-folder files, including several large scene backgrounds. This means some full-screen artwork is currently pulled in with the main menu before its event is open. These are strong candidates for lazy loading or remote event bundles, subject to checking the owning UI and keeping a small local event badge.

| Candidate | Why it may be loaded early | Review decision |
| --- | --- | --- |
| `BridgeRepair/BridgeRepairBG.png`, `BridgeLeftV3.png`, `BridgeRightV3.png`, `BridgeSegmentV3.png` | Direct `MainMenu.unity` references; full bridge scene art should only be needed by Bridge Repair map. | Move into Bridge Repair pack if no non-event use remains; keep `BridgeEventLogo.png` local if it is the menu badge. |
| `Harvest/Harvest_SceneBG.jpg`, `Harvest_Bear_Idle.png`, crop art | Direct `MainMenu.unity` refs and also `HarvestConfig` refs. Likely serialized on prebuilt Harvest UI plus config. | Remove duplicate early UI/config references after establishing one runtime source; keep only small Harvest logo/badge local. |
| `TinySafari/RisingDesignV4.png`, `SafariH.png`, `SafariTitle.png`, `SafriPopupBG.png` | Direct `MainMenu.unity` references; they belong to Safari intro/map/popup. | Lazy-load in `SafariEventController` before popup/intro/map; keep icon art local. |
| `BridgeRepair/BridgeRepairPopupImg.png`, `BridgeTitleBG.png` | Main menu serialized refs and Bridge Repair popup art. | Bridge Repair pack, after confirming these are not reused by a general popup. |
| `SafariEventBTNV2.png`, `Harvest_Logo.png`, `BridgeEventLogo.png` | Main menu icons need a visible event affordance while locked/active. | Keep local initially, or make a small optimized set of badge-only textures. Do not gate menu icons on large event packs. |
| `GiftFestivalLogo.png` | Historical early-load path: the controller used to read `cfg.Art.menuLogo` for the menu badge. | Keep a reduced local menu badge or generate one local icon; move the large logo/background/animation art into the event pack. Build icon without touching `cfg.Art`. |
| `DailyBonusButton.png`, `AllianceHelp.png`, `AllianceHelp/UFO.png` | These are not clearly owned by the five scheduled events; Alliance and Daily Bonus have independent flows. | Keep local until their separate unlock/lazy-load lifecycle is reviewed. |

User confirmed these full-screen/popup images are needed only when their event popup appears; they should not load with the initial menu. Main-menu references were mapped to concrete objects:

User clarified that event images are generally event-specific; only images shared across events need special handling. Therefore prefer one exclusive bundle per event. During dependency analysis, any sprite/material referenced by more than one event is explicitly reported and moved into a small shared event-art bundle (or retained locally if used by general gameplay/menu). Do not duplicate a shared asset into every event bundle.

| MainMenu object | Current image | Action |
| --- | --- | --- |
| `Background` under Bridge Repair `Board` | `BridgeRepair/BridgeRepairBG.png` | Remove the prebuilt full-screen image from the menu scene; load in the Bridge Repair event view. |
| `BridgeRepairMapScreen` | `BridgeLeftV3.png`, `BridgeRightV3.png`, `BridgeSegmentV3.png` | Instantiate/build the map only after Bridge Repair art is ready. |
| Two `Frame` objects and `OverlayImage` | `BridgeTitleBG.png`, `BridgeRepairPopupImg.png` | Create popup art at event time after the bundle gate. |
| `RisingDesignV4` `Background`, `Crowd`, `Frame`, `OverlayImage` | Safari background, crowd, title and join popup | Build/open the existing Safari view after the event bundle gate. |
| Harvest `Board`, `Bear`, `TrowelIcon`, `Icon` | Harvest scene, character, tool and crop | Keep only a small event badge in the menu. Load the board and gameplay art on `HarvestScreen.Open`. |

Keep `RisingEventIcon` (`SafariEventBTNV2.png`), `HarvestEventIcon` (`Harvest_Logo.png`) and `BridgeRepairEventIcon` (`BridgeEventLogo.png`) local as small menu affordances. Also keep the independent Alliance Help and Daily Bonus images local. Do not remove these menu icons when moving popup art.

The scene currently serializes these event panel/popup references directly, so deactivating their GameObjects alone does not prove that Unity excludes the sprites from the player. The implementation must remove those references from `MainMenu.unity`, construct or populate the event-specific views after a verified bundle load, and then verify the player dependency graph. Until the runtime load gate and lazy view construction are connected, keep source files and serialized UI intact. Do not leave null `Image` components that silently turn the menu popup into an empty panel.

Gift Festival originally read `cfg.Art.menuLogo` while building its menu icon. The intermediate live-window guard reduced early access but did not remove player dependencies. The current implementation instead uses lightweight `GiftFestivalConfig.menuLogo/menuFont/menuFontMaterial` fields; `cfg.Art` is first needed behind the event gate. The original Resources art assets remain intact until user-run activation creates stripped runtime copies.

## Implementation status

Implemented in source:

- Manifest schema 3 adds independent `events`, source config GUIDs, unlock field names and stable GUID/local-file-ID addresses for multi-sprite textures. Schema 2 remains readable for the existing exports. Threshold numbers are not duplicated into the manifest.
- `ProgressiveEventBuild` inventories direct config/scene art references, assigns shared source files to `event-shared`, rejects ungated scene consumers and local atlas membership, and records source/importer hashes. `plan.json` reports shared event assets and local dependency duplication. The existing content-build button includes all five events.
- `ProgressiveContent.Events` uses the existing single worker, SHA-256/size verification, CRC bundle load, atomic cache and shared cancel/retry overlay. Prefetch caches bytes only. Scene/config sprite references are populated before opening the event UI. Multi-sprite sheets are decoded once per pack load.
- Dynamic prefetch reads current configs after cloud restore and on resume. Safari, Bridge and Harvest prefetch during live calendar windows within the configured lookahead. Gift can prefetch before its first player-specific cycle; after unlock it follows that cycle. Gear respects `eventEnabled` and its configured season. Gift's actual existing rule is `CurrentLevel.Global > unlockAfterLevel` (100 completed means playable level 101); its lookahead uses the same strict comparison.
- Controller and public view entry gates cover Safari join/promo/intro/map, Bridge join/promo/how-to/map/victory, Harvest open, Gift invitation/join/screen and Gear invitation/join/preview/pending result. Callbacks re-enter availability/state checks, and an event-window change during download cancels the stale entry. Safari pending outcomes are cleared only after map presentation starts. Cancelling an optional download suppresses automatic re-prompts/prefetch for that event for the current app session; manual entry can retry.
- Gift menu logo/font/material now have lightweight fields on `GiftFestivalConfig`; its menu no longer reads `cfg.Art`. General gameplay loss/reward affordances (Safari loss icon, Harvest trowel, Gear gain icon), global fonts/UI and the five menu logos remain local. Large event-only art is packaged.
- Bridge's river material/mask are also packaged; runtime string loading goes through `EventResource`.

Source-preserving activation (runs only when the user clicks Activate after remote verification):

1. Move original art-bearing config assets out of Resources **with their GUIDs preserved**; generate lightweight config copies at the original Resources paths, plus `EventArtBindings.asset`. A recovery journal records each generated copy and source move. Bridge's Resources river material is moved without a local duplicate.
2. Original saved scenes continue to reference the original authoring configs and sprites. `IProcessSceneWithReport` remaps config references and strips remote art **in the player build's scene copy only**, installing a scene binding host. It rejects residual remote dependencies, including indirect/prefab dependencies it cannot safely rewrite. Editor Play/preview scenes are not processed.
3. `link.xml` preserves the bound project types for IL2CPP. Player build guards require the verified manifest, unchanged event source/import settings, matching runtime config copies, and no remaining remote dependencies reachable from Resources.
4. Restore Local removes generated copies and restores original GUID-preserved configs/material. Editor Shared-config access resolves original sources while remote mode is active. After activation, edit the originals under `Assets/_Project/RemoteContent/EventSources`, then rebuild/reverify/reactivate; do not hand-edit the generated Resources copies.

Lifetime/performance: pending view requests cancel on disable/destroy. Authored inactive UI and dynamically cached screens remain consumers while the menu scene exists, so closing/reopening a popup does not repeatedly decode the same textures. Scene teardown releases these cache consumers; shared bundles stay loaded while another event uses them. Cleanup runs between worker operations, never during an async asset load. `Unload(false)` preserves outstanding overlay references; this is not a claim of immediate GPU-memory reclamation. No measured performance or size reduction is available yet.

Checks performed without running Unity: source/scene reference inspection, classification of remote scene consumers, new Gift menu reference migration, Python syntax, C# lexical delimiters and whitespace checks. These are **not** a C# compilation, Unity dependency build, Play-mode test or device test.

### User-run next steps

1. Let Unity 6000.3.9f1 import/compile the code and inspect Console. No player/Xcode build is needed for this first step.
2. On this Mac with iOS selected, use `TinyFixers > Yayın > Parçalı İçerik (R2)` and build content again. The prior 16-file / 92.4 MiB output does not contain these event packs. The operation does not upload or mark a release. Inspect the new `plan.json`, including `sharedEventAssets` and `eventLocalDependencies`.
3. Run `python3 Tools/r2_content.py plan --directory Exports/ProgressiveContent/iOS` and inspect the new package list/size.
4. When you choose to upload: `python3 Tools/r2_content.py publish --directory Exports/ProgressiveContent/iOS --apply`, then `python3 Tools/r2_content.py verify --directory Exports/ProgressiveContent/iOS`. These commands are instructions only; the agent has not run them.
5. Only after verification, click Activate for iOS. A later user-run player build applies the scene-copy stripping and guards. Verify device behavior using the checklist below; actual app-size savings require that build's dependency/size report.
6. Prepare Android independently on the Windows machine. No release baseline is created by any of these content steps.

## Goal

Keep event-only artwork out of the initial app download. Download each event's artwork when its configured level gate and UTC schedule make the event available, then retain the verified files locally for later visits. Before opening any invitation, promo, intro, map, screen, or pending-result presentation, ensure that event's pack is ready. If it is not ready, show the shared download/retry UI and let the player cancel optional event entry. Never spend event currency, consume a life, mark a join, claim a reward, or advance event state before the art gate succeeds.

## Event gates are configuration, not constants

The effective gate is read from the existing config assets at runtime. Do not copy current values into a second list, manifest, or source-code constant. Changing an event's minimum level must automatically change its prefetch point.

| Event | Runtime gate / timing source | Current configured gate | Menu and entry points |
| --- | --- | ---: | --- |
| Tiny Safari / Rising | `SafariConfig.minLevelGate`, `SafariSchedule.IsActiveNow` | 45 | Main menu controller, join popup, pre-level promo, intro and `RisingMapScreen` |
| Bridge Repair | `BridgeRepairConfig.minLevelGate`, `BridgeRepairSchedule.IsActiveNow` | 60 | Main menu controller, join/how-to popup, map, victory and pre-level promo |
| Harvest | `HarvestConfig.minLevelGate`, `HarvestSchedule`; active season through `HarvestState.IsLive` | 50 | Left event button, `HarvestScreen`, how-to and rewards |
| Gift Festival | `GiftFestivalConfig.unlockAfterLevel`, player UTC cycle / `GiftFestivalState.IsLive` | 100 | Dynamically installed main-menu icon, invitation and `GiftFestivalScreen` |
| Gear Cup | `GearCupPreviewConfig.minLevelGate`, `seasonHours`, `eventEnabled` and `GearCupState` | 110 | Main-menu preview screen, invitation, result and rewards |

Current values above are a snapshot from the checked-in configs. The runtime pack planner must resolve config assets and schedule state, not branch on these numbers. If a threshold changes, update the corresponding config and regenerate the content build so its source stamps and package manifest match.

Prefetch using the same 15-level lookahead as level content: start when `CurrentLevel.Global + prefetchLevels >= configuredGate`; the event still cannot be entered until its schedule says it is live. Only fetch if it has not been cached. For scheduled events, if the gate is already passed, fetch as the event window approaches/opens, not continuously during long inactive periods. On app resume and cloud restore, recalculate from current config and restored progress. Do not prefetch an event that is disabled by its live config.

## Current artwork inventory

`Assets/_Project/Art/UI/MainScreenEvents` contains 91 files (84 PNG, plus other source files), **62.16 MiB source size**. This is not the final bundle size. Existing level/city output does not include these files.

| Pack | Candidate source art | Important config/runtime references |
| --- | --- | --- |
| `event-safari` | `SafariEventBTNV2.png`, `TinySafari/**`, `RisingEvent/**` | `SafariConfig.lossIcon`; controller/button refs; `SafariAvatarStackView` authored helmet sprites; `RisingMapScreen` serialized reward art; map-specific `Resources.Load` assets such as the lift sound are not event art and stay local. |
| `event-bridge-repair` | `BridgeRepair/**` including all `Characters/**` | `BridgeRepairConfig.characters`; controller's continue/check/coin/chest/victory art; `BridgeRepairRiver.mat` and mask; map screen serialized refs and generated UI. Shared reward chest, coin and global UI belong in local shared art. |
| `event-harvest` | `Harvest/**` | `HarvestConfig.sceneBackground`, soil/dug/hint tiles, crops, bear frames, trowel. The config is in Resources and currently owns direct Sprite refs. |
| `event-gift-festival` | `GiftFestival/**` excluding source-only `AssetSources.json`; timing-bar SVGs must be checked for player runtime use | `GiftFestivalArt.asset` referenced by `GiftFestivalConfig.Art`; frames/materials loaded through this config. The menu now uses the lightweight config fields; large art is accessed behind the event gate. |
| `event-gear-cup` | No dedicated folder was found under MainScreenEvents; inventory its config dependencies before assigning a pack | `GearCupPreviewConfig` sprite arrays/materials (`Resources/Events/GearCupConfig`) and any directly serialized UI art. Keep shared team emblems and reward icons local if used outside this event. |
| local shared | `AllianceHelp*`, `DailyBonusButton.png`, shared event plate/lock art | These are global/help/daily UI, not assigned to a scheduled event without confirming usage. Do not move them wholesale. |

Inventory scope must follow actual dependency and usage, not folder names alone. For every candidate, inspect config references, serialized scene/prefab references, SpriteAtlas membership, runtime string paths, and any references outside that event. A file used by a general-purpose screen stays local or goes into an explicit shared pack.

## Runtime integration requirements

1. Add `events` pack entries to the progressive manifest with stable event IDs, configured unlock key/source config GUID, addressable asset GUID/path pairs, immutable bundle hash, size and CRC. Event packs must not be coupled to level-number ranges.
2. Build one independent bundle per event after dependency analysis. Reject cross-event or local-scene dependencies unless explicitly split into local shared art. This permits unloading one event without unloading another event's textures.
3. Keep a small local event icon so a locked/live badge can render without downloading the large screen art. Do not build an event menu using an art-dependent config before the pack is loaded.
4. Provide one event-art lease/gate API. Acquire it before opening invitations, pre-level promos, intro/map/screen, resume/pending-result presentation. Populate runtime config sprite references from the verified bundle, then call existing UI construction. Release on all close, cancel, disable and scene-teardown paths; unload the exclusive bundle only after the final consumer releases it.
5. Check gate completion after the download and before changing event state. Recheck live schedule, event-enabled flag, level gate and ownership/join state because a window can expire during a download.
6. Cache verified files by content hash. Reinstall behavior: restore player progress first, then only prefetch events whose configured gate is reached/within lookahead and whose schedule is live or near opening. Past event packs download on demand when the event is available again.
7. Activation moves only proven event-only assets out of Resources/local serialized references. Preserve `.meta` GUIDs, source configs, editor previews and local rollback. Build guard rejects moved event assets if the matching verified manifest is missing or config/art stamps changed.

## Per-event audit before enabling removal

- Safari: controller currently opens join popup, intro, map and pending outcomes from multiple paths. Gate each path once centrally; preserve pre-level promo behavior and do not block normal level start on an optional event download.
- Bridge Repair: gate auto-popup, manual icon, pre-level promo, pending map/victory/reward; preserve awarded rewards if the player declines to view the event art.
- Harvest: gate the live event button before `HarvestScreen.Open`; keep locked/next-window countdown and season-end conversion functional without art. Keep win-to-trowel logic available independently of the screen.
- Gift Festival: controller creates its main-menu logo from `cfg.Art.menuLogo` and can auto-open an invitation. Separate the small local icon from large art, gate invitation and screen, and keep state/schedule sync independent of art.
- Gear Cup: gate invite, preview, pending result and reward presentation; avoid losing pending result if the player cancels download.
- All: keep audio under existing central APIs/settings. Artwork packaging must not add per-frame polling or texture allocations. Cancel requests when no consumers remain and avoid unloading during Unity async loads.

## Release workflow

1. Audit candidate GUIDs/dependencies and generate an event-art report.
2. Implement runtime gates and source-preserving editor activation/rollback.
3. Prepare Android/iOS event bundles independently from each platform's level/city bundles; compare hashes and verify all dependencies.
4. Inspect package sizes and local duplicate dependencies before publishing.
5. User runs the prepared plan/publish/verify commands. Only after a verified receipt may the user activate the platform manifest in Unity.
6. On device, check each event at just-before-gate, at gate while inactive, at live-window start, during download cancellation, offline revisit, schedule expiry during download, cloud restore, and pending reward/result. Confirm event state/rewards are unchanged by art download failure.

No event bundle was added to the existing iOS output by this code change. Existing R2 objects, the iOS manifest and the user's terminal process were not changed.
