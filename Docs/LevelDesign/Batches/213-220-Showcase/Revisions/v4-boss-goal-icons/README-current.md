# Boss goal icons — 2026-11-04

The user reported missing BossDamage icons in goals and the PreLevel/editor view. Active V1 catalogue inspection found exactly two missing assignments:210 and220. All20 earlier bosses used BadgerIdle.png (GUID bd8a2a2aabdf46fcabea5eb683bc76d9). That existing sprite is now assigned to the BossDamage goal of210 and220.

Both TopHUD.ResolveGoalIcon and PreLevelSpecialPopupController.ResolvePreviewGoalIcon return goal.iconOverride when assigned. LevelDataEditor exposes the same Sprite field. No runtime UI change is necessary. The two authoring scripts now assign the existing icon for boss goals, and static goal preview rendering honours explicit icon overrides.

`verify_icons.py` compares each current asset with the saved snapshot plus exactly one icon replacement. It checks all22 boss goals, the sprite GUID/image, unchanged other V1 level/meta/catalog/name files, existing UI override routing and authoring Python syntax. Moves, HP, goals amounts, arrays, masks, layers and all other fields stay byte-identical.

This is an additive icon correction after v3 move calibration; current213–220 moves remain21/21/22/20/21/22/24/28. Snapshots and manifest are local change records, not an app release baseline. No Unity build/Play, device test, upload or publishing occurred.
