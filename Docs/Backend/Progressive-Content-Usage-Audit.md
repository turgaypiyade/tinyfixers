# Progressive content usage audit

Static asset-reference audit from the current checkout. This is a source-file inventory, not a player-build size report. Unity YAML GUID references were followed from `MainMenu.unity`, the active level catalog and event configs; `Resources.Load` calls and runtime-created objects were checked separately in code.

## The three requested groups

| Group | Source of truth | Keep local / defer |
|---|---|---|
| Levels | `Settings/ProLevels/LevelCatalogProV1.asset` → each actual `LevelV1_*` → obstacle IDs on that level → only those definitions in `ObstacleLibrary` | Levels 1–50 and their used images stay local. Starting at 51, images/data are grouped by the level's 50-level pack. Existing 15-level lookahead is calculated at runtime. |
| Events | Event configs plus references from enabled scenes and event-screen consumers | Only the small menu logos/badges stay local. Each event's large art is requested before its screen opens. Art referenced by multiple event packs is shared. Config unlock thresholds remain runtime config values. |
| Mission / world map | MainMenu world-map consumers, `WonderCatalog`/`Wonder_mis*`, mission task art, and city order | Keep mission/task/world-map artwork out of the initial player and request it before opening the relevant view. City panorama pairs remain separate city packs selected by the saved city order. |

Everything outside those ownership lists remains in the initial app. In particular, an image is not considered remote merely because it lives under an `Art` folder; a serialized reference or `Resources.Load` path can still pull it into the player.

## What the current source references say

### Levels 1–50

The real LevelV1 inventory in `First-50-Level-Asset-Catalog.md` contains 23 obstacle IDs and 76 image files, totaling **52.08 MiB of source files**. The full `ObstacleLibrary` is only a lookup table; it must not be used as the level-1–50 keep list. This validates the level-based selection rule, but does not prove the player contains only these 76 images: other scene, prefab, Resources, and code references must also be accounted for.

### Events

The MainMenu serialized reference walk reaches **40.0 MiB** of event-folder images. That set contains large screen art as well as the logos. The intended local menu marks are `SafariEventBTNV2`, `BridgeEventLogo`, `Harvest_Logo`, `GiftFestivalLogo`, and the small Gear Cup badges. The other event art belongs behind its event-screen gate. The currently configured event thresholds are read from config at runtime; the values in the old plan are only a snapshot.

Source folder sizes, including files not all referenced by MainMenu:

- Bridge Repair: 32.85 MiB
- Gift Festival: 17.86 MiB
- Safari: 4.91 MiB
- Harvest: 4.37 MiB
- Gear Cup (`Art/UI/Missions/GearCup`): 27.92 MiB

`HarvestConfig.asset` references `WonderCatalog.asset`. That catalog references all ten `Wonder_mis*` definitions, which in turn reference mission backgrounds, construction images, and task icons. This creates a transitive path from a Resources event config to mission artwork. Moving only the visible MainMenu object references is not enough to remove those images from the player.

### Mission and world map

The MainMenu reference walk reaches **104.0 MiB** under `Art/UI/Missions`:

- `Missions/1` map UI: 3.08 MiB
- `Missions/ND_M001` wonder/mission art: 72.06 MiB
- `Missions/GearCup`: 27.92 MiB (belongs to the Gear Cup event group above)
- `Missions/TaskIcons`: 0.85 MiB
- `Missions/MissionPopup.png`: about 0.12 MiB

The `ND_M001` directory is not a single map image: it has both ten city/wonder backdrops and construction-stage/site/piece-map art. The mission pack must include the art needed by the opened mission view without making the whole directory a local dependency through `WonderCatalog` or `HarvestConfig`.

WorldTour city panoramas are resolved by code from `Resources/WorldTour`, not by a serialized MainMenu reference walk. Therefore their absence from that walk does not mean they are unused. They need separate city-pack gating and their source references must be stripped from the player resource set while the shared globe/map UI remains local.

## Required correction to the current packaging approach

The current mission collector expands every enabled scene's transitive dependencies by folder. That can over-collect an entire candidate folder, and it does not itself remove art references held inside referenced ScriptableObjects such as `WonderCatalog` and `Wonder_mis*`. The `HarvestConfig → WonderCatalog → Wonder_mis* → image` chain is a concrete player-retention path. A mission AssetBundle can exist and still yield little or no install-size reduction while that chain remains reachable.

Before another player build, the utility should:

1. Build ownership from actual LevelV1 entries, event configs/scene consumers, and mission/map definitions; report a path once with all owners when it is shared.
2. Create runtime metadata/catalog copies for remote mission definitions and clear their serialized image references in the player scene/config copies; retain source assets unchanged for the editor.
3. Add a build-time dependency assertion that fails if a remote-owned image is still reachable from any player scene, Resources asset, or local catalog.
4. Keep non-owned assets local. Report the exact source paths and estimated bundle bytes by owner before upload.

The 52.08/40.0/104.0 MiB figures are source-file sizes. They are not a promise of an equal reduction in iOS installed size; Unity texture import, duplicate dependencies, native executable size and compression affect that measurement. The observed 507 MiB install size is still the authoritative result until a new player build is installed and measured.
