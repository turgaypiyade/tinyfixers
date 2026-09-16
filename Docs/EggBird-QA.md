# EggBird

`ObstacleId.EggBird = 45` replaces WolfEgg in existing levels and goals. It keeps movable obstacle gravity/swap behavior. The shared ObstacleLibrary supplies two stages, all 21 BirdSheet sub-sprites, and the RocketBasket flame at auxiliary slot 21; no scene component or prefab setup is required.

## Animation

- Initial appearance: BirdEgg. First valid hit: BreakEgg. Second valid hit: hatch.
- Shell fragments: EggParts_0..3 on the first hit (cream upper shell), EggParts_4..6 when hatching (purple lower shell). The sheet is readable for the existing particle sprite-atlas path.
- Audio: breakingegg.wav on both cracking stages; birdflying.wav loops from takeoff through the dive (volume 0.75, 60 ms fade-in), then stops at impact. Birdbomb.wav plays once for the simultaneous three-bird impact on the board audio source so its tail survives the flight visual. Flight mute follows Sound settings; level exit/disable also stops the loop. The three short clips preload for immediate playback.
- Rise: a 0.12-second crouch/recoil followed by a 0.98-second climb, 0.68 to 1.75 scale, stretching, banking along an S curve, a full front/side/back turn, feather trails and wings flapping at 12 Hz.
- Split: a 0.18-second compression/wobble, violet and gold rings with scattered feathers, then three birds springing outward along staggered arcs over 0.52 seconds. A 0.26-second bob/tilt holds the formation before diving.
- Dive: three randomly chosen occupied board cells, without replacement when possible. Gold target rings, curved accelerating descent over 0.62 seconds, wings at 15 Hz.
- Impact: RocketBasket's authored flame sprite/animation plays immediately at each distinct destination. Scenes without its component use the same shared animation and the obstacle's fallback sprite (2.55-cell sprite box, 0.34 seconds). One hit per distinct target cell goes through the existing PatchBot impact/MatchClear pipeline; specials use normal chain behavior. No adjacent obstacle splash damage is added.
- Rise, split, dive and flame playback run outside the main action queue. `EggBirdFlight` contributes to level-end accounting but not the PatchBot/special visual gate, so normal gravity, match/fall overlap and dynamic input are not held by the birds. Only impact damage enters the sequencer, reading current cell contents when it executes.
- With only one or two eligible cells, all three birds remain visible and share the available destinations; shared cells receive one grouped hit. With no targets, the flight cleans up after splitting.

## Play Mode checks still required

1. Place EggBird in a test level. Confirm the first adjacent match changes only the stage; make it fall or swap, then confirm the second hit hatches exactly once.
2. Repeat using specials and boosters. Confirm the cracked stage is not skipped by a single hit and each destroyed egg starts one flight.
3. Check front/side/back sprite changes, wing hinges, three distinct birds and board alignment, including eggs on the top row and side edges.
4. Hatch several eggs in one clear. Check all flights finish, targets explode, specials chain and the board settles without a retained flight job.
5. Hatch the last goal egg on the last move. The win/fail popup must wait for queued dives, impacts and their resulting resolve.
6. Test holes, Cargo, moving tiles and multi-cell obstacles. Holes without obstacles and Cargo are excluded; impacts read cell contents when executed, and normal obstacle damage rules remain in effect.
7. Exit/restart the level during rise, split and dive. No bird visuals or queued damage should carry into the new level.
8. Break a cracked egg while a long cascade is forming. Confirm tiles continue falling and matching throughout the rise, split and dive, including when several eggs hatch together. Flame visuals must appear on arrival even if the impact damage is queued behind a fall.
9. Check RocketBasket itself still uses its existing authored impact scale, duration and sprite after sharing the flame player.
10. Check shell pieces and breakingegg on both stages, continuous birdflying through the split/dive, and Birdbomb exactly with the impact flames. Toggle Sound off/on during flight and exit the level mid-flight: no loop should remain audible after teardown.

## Verification performed

The user confirmed the initial EggBird implementation works in-game. This refinement was checked statically for stage/sprite references, shared flame settings, the dedicated nonblocking flight registration and removal of flight animation from the main queue. No build was run; the refined animation still needs Play Mode visual verification.
