# User-supplied bitten apple

The user reported that the older bitten-apple variant was still visible and supplied the intended image again. The exact attached PNG bytes replaced `Assets/_Project/Resources/FlyFruit/AppleHalf.png`; the previous PNG and importer are in `Before/`.

Its existing GUID, importer, alpha, centered pivot and512default import limit are preserved. Obstacle102already references this sprite as `auxiliarySprites[0]`; `FlyFruitService.UpdateFruit` selects it at `Remaining<=3`,after the third of six bites. No image-generation or image-editing transform was applied. Full apple, egg, goal/prelevel sprites, audio, mechanics and level files did not change in this task.

`validation.json` checks exact source/output hashes, RGBA alpha, sprite reference and unchanged6109otherAssets files. The old image and supplied image were visually inspected. No UnityUI, Play, build or gameplay simulation. Later234difficulty work is separately authorized and uses a fresh preservation snapshot that already includes this replacement.
