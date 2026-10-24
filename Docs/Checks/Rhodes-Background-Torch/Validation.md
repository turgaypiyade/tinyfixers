# RodosDevi.png ambient torch — 2026-10-23

Exact source: Assets/_Project/Art/UI/Missions/ND_M001/1/RodosDevi.png (1001×1570). The upper-left flame sits above the torch bowl near normalized x=0.258, y=0.944 in bottom-up UVs.

WonderRevealView enables _RhodesTorch only when its displayed Sprite.name is exactly RodosDevi. The existing UI/WonderReveal shader animates the painted flame's warm pixels, adds slight brightness variation/local orange glow and two sparse drifting embers. UV displacement grows above the bowl, leaving the bowl and statue stationary. The effect requires a fully revealed image, is excluded from falling restoration sheets and follows sprite-relative UVs through atlas/aspect fitting. Other backgrounds, gameplay statue obstacles and plain-image thumbnails are unaffected. Original PNG and import settings are unchanged.

GPU-only animation, no added Update/coroutine, particle objects or audio. Existing material ownership and teardown remain in WonderRevealView. The localized branch avoids extra flame texture sampling outside the upper-left torch area. Expected allocation/object-count benefits follow from code; device timing was not measured.

Preview: Docs/ArtReviews/Rhodes-Torch/rodos-mesale.html. Its GLSL flame function is mechanically translated from the production shader. A JPEG copy is embedded for preview size only; the source asset is not compressed or modified. Flame toggle and close/full view provided. JS syntax checked with node --check, fragment size below 1 MB. Browser/GPU rendering and Unity shader compilation have not been run; this is not a captured Unity result.

Verification: full WonderRevealView/restoration source metadata binding against installed Unity/UI APIs passed, along with exact-sprite/missing-sprite exclusion checks among 94 combined isolated checks. No Unity Play or build.
