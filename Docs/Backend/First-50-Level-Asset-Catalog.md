# LevelV1 visual catalog

This inventory starts from the authored `LevelV1_*` entries in `LevelCatalogProV1`. It does not treat every definition in `ObstacleLibrary` as used by the first 50 levels.

## What levels 1–50 use

These 23 obstacle IDs appear on the board or as obstacle goals in the first 50 levels:

| Obstacle | Levels |
|---|---|
| Stone | 2, 3, 10, 20, 30, 40, 50 |
| Chest 1 | 1, 3, 5, 8, 9, 11, 12, 14, 15, 17, 18, 19, 20, 27, 30, 32, 33, 37, 40, 42, 45, 46 |
| Chest 2 | 2, 5, 7, 8, 14, 15, 24, 43 |
| Chest 3 | 8, 15, 17, 25, 37, 42, 44 |
| Plastic | 18, 19, 30 |
| Orange plastic | 17, 18, 19, 22, 30, 40 |
| Blue plastic | 17, 40, 44 |
| Red plastic | 24, 40 |
| Oil | 11, 14, 28, 31, 35 |
| Color chest | 26, 27, 28, 29, 31, 32, 33, 34, 37, 39, 47, 48, 50 |
| Mud | 6, 8, 9, 12, 15, 25, 28, 32, 33, 37, 46 |
| Tube (goal) | 41–46 |
| Wardrobe | 36, 38, 45 |
| Helmet porcelain | 46–49 |
| Hat launcher | 31, 33, 34, 35, 36, 40, 45 |
| Cargo | 21, 22, 23, 25, 34, 43, 44, 49 |
| Player shield pickup | 30, 50 |
| Enemy shield pickup | 30 |
| Water channel (goal) | 4 |
| Gold coin | 28 |
| Bird house | 13 |
| Fly egg box | 16 |
| Fly fruit | 16 |

The images are the sprite fields and auxiliary image references belonging to just these IDs, plus any per-level sprite overrides. The Unity menu **TinyFixers → Yayın → LevelV1 Görsel Kataloğunu Üret** walks the actual `LevelV1` assets and writes their exact source paths here, grouped by level ranges. Later ranges are 51–100, 101–150, 151–200, and so on.

## Why the installed app stayed large

Every authored `LevelV1` asset references the same full `ObstacleLibrary.asset`. The content utility now generates filtered `LevelData` and `ObstacleLibrary` copies per range, uses the first-50 copies only in the activated player catalog, and keeps authored source assets intact. `ObstacleHintManager` now reads the active level's library instead of holding a scene reference to the full library.

Assets loaded only by string through `Resources.Load` remain in Unity's Resources payload until their runtime callers are migrated to the progressive resource resolver. Event artwork and city panoramas already have their own gates; this level filter does not claim to move every remaining Resources folder.

The 23-ID inventory is based on the current authored levels. Exact sprite paths and ranges are generated from Unity's serialized assets by the menu command; no player build or size measurement is performed by that command.

## Current first-50 image source paths

Static asset-reference walk found 76 image files in definitions used by levels 1–50, including level-specific Sprite references. Three serialized GUID references do not resolve to a `.meta` file in this checkout, so Unity must confirm whether those are stale/missing references.

- `Assets/_Project/Art/Icons/Gems/AnimalStyle/V3/BlueStoneV8.png`
- `Assets/_Project/Art/Icons/Gems/AnimalStyle/V3/RedStoneV5.png`
- `Assets/_Project/Art/Icons/Gems/AnimalStyle/V3/YellowStoneV7.png`
- `Assets/_Project/Art/Icons/Gems/AnimalStyle/V3/key_v7.png`
- `Assets/_Project/Art/UI/BattleField/EnemyShield.png`
- `Assets/_Project/Art/UI/BattleField/PlayerShield.png`
- `Assets/_Project/Art/UI/GoldMoney.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdBody.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdGoalIcon.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdHouseClosed.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdHouseOpened.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdHousePerch.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingL1.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingL2.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingL3.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingR1.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingR2.png`
- `Assets/_Project/Art/UI/Obstacles/BirdHouse/BirdWingR3.png`
- `Assets/_Project/Art/UI/Obstacles/ColorCrate/ColorCrate_Red_1.png`
- `Assets/_Project/Art/UI/Obstacles/ColorCrate/ColorCrate_Yellow_1.png`
- `Assets/_Project/Art/UI/Obstacles/Dimerdal/DimerdalDiamondGoal.png`
- `Assets/_Project/Art/UI/Obstacles/LampStrip/LampStrip_GoalIcon.png`
- `Assets/_Project/Art/UI/Obstacles/Lantern/Lantern_2_Lit.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Barrell/DropMud.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cabinet/CabinetClosedV1.1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cabinet/CabinetClosedV1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cabinet/CabinetLeftDoor.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cabinet/CabinetOpenedV1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cabinet/CabinetRightDoor.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Cargo/CargoV3.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Case/Case_GoalIcon.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Chests/ChestParticulesV3.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Chests/ChestStage23ParticulesV3.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Chests/ChestV1.1/ChestV1_1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Chests/ChestV1.1/ChestV1_2.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Chests/ChestV1.1/ChestV1_3.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/EggBird/BirdEgg.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/EnergyCircle/ernergycircleV1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Grass/GrassFlowerPreview.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Grass/GrassG1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HatContainer/HatHummer.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HatContainer/HotContainerV1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HelmetLocker/HelmetLockerClosed.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HelmetLocker/HelmetLockerEmpty.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HelmetLocker/Nut.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/HelmetPorcelain/HelmetOwl.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Magnet/MagnetObstacle1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlaySTG1V1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlayStage1_Sp 1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlayStage2V2.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/OilParts/OilDropV4.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/OilParts/OilV4.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/OrangeCapObs.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/PaintCanBox/PaintCanBox_9.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Plastic_Blue.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Plastic_Red.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/PurpleCapObs.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/SculptV1/SculptAllV2.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/SculptV1/SculptMetalV3 1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/SealSafe/SealSafeIcon.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Tubes/TubeBaseV1.png`
- `Assets/_Project/Art/UI/Obstacles/RobotStyle/Wall/kiremit_bej.png`
- `Assets/_Project/Art/UI/Obstacles/WaterChannel/ChannelSource.png`
- `Assets/_Project/Art/UI/Obstacles/WaterChannel/Channel_V_Closed.png`
- `Assets/_Project/Art/UI/Obstacles/Windmill/FlourGoalIcon.png`
- `Assets/_Project/Art/UI/WonderCharacters/Badger/BadgerIdle.png`
- `Assets/_Project/Resources/FlyFruit/Apple.png`
- `Assets/_Project/Resources/FlyFruit/AppleHalf.png`
- `Assets/_Project/Resources/FlyFruit/BoxFrame.png`
- `Assets/_Project/Resources/FlyFruit/BoxReference.png`
- `Assets/_Project/Resources/FlyFruit/Egg.png`
- `Assets/_Project/Resources/GrassWaterLily/CF1.png`
- `Assets/_Project/Resources/Hamster/Hamster_Idle.png`
- `Assets/_Project/Resources/HotAirBalloon/Balloon_S4.png`
- `Assets/_Project/Resources/RhodesStatue/S1_Base.png`
- `Assets/_Project/Resources/TarpStatue/Lion_Icon.png`
