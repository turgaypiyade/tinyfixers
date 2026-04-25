# Board Border Theme Setup

This folder contains the runtime theme selector for `DynamicBoardBorder`.

## Intended setup

1. Create one `BorderSpriteSet` asset per color:
   - Orange
   - Purple
   - MetallicGray
   - DarkNavy
   - Black
2. Assign the 12 border prefabs/sprites for each set.
3. Create one `BorderSpriteLibrary` asset and add all `BorderSpriteSet` assets to its `sets` list.
4. Add `BoardBorderThemeApplier` to the same GameObject as `BoardController`.
5. In `BoardBorderThemeApplier`, select only:
   - `Border Color`
   - `Border Sprite Library`
   - optionally `Border Drawer` if auto-resolve does not find it.

`BoardBorderThemeApplier` applies the selected sprite group to `DynamicBoardBorder` before the grid is built, so `GridSpawner` can continue using the existing `borderDrawer.Draw(...)` flow.

## Why this is separate from BoardController

`BoardController` already has many gameplay responsibilities. Keeping border theme selection in a small companion component prevents board logic from depending on visual sprite-pack details while still letting the theme be selected from the BoardController GameObject in the Inspector.
