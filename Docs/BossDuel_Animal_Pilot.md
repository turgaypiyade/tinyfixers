# Boss Duel — animal pilot

`01_Game` references `Assets/_Project/Settings/RamDuelCharacter.asset`,
`BadgerDuelCharacter.asset` and `HyenaDuelCharacter.asset`. These rules are the only ones the
duel runs; see the last section for what the robot duel left behind and what replaced it.

## Approved design — implemented 2026-09-20 (Play-mode verification pending)

These decisions supersede `BossDuel_Plan.md`, which now describes only the removed robot duel.
Production boss HP, power and counter damage were retuned on 2026-09-21; see
[Boss progression tuning](BossDuel_Progression_Tuning.md). Move limits are unchanged.

### Move damage and encounter flow

- Treat 20–40 cleared tiles per move as normal. Power remains cleared tile count multiplied
  by damage per tile, including cascades and special clears, with no hidden damage reduction.
- An encounter has 1–3 opponents, each with its own HP, attack damage, and appearance.
- The level's existing `moves` limit applies across the entire encounter, without refunds or
  wave resets. The last move finishes its board resolution, attack, surviving enemy's counter,
  and any wave transition before the normal result evaluation. Killing the final opponent on
  the last move wins; leaving any opponent alive loses even if player HP remains.
- After the player's move and attack resolve, a surviving opponent counterattacks once.
  A defeated opponent never counterattacks. Its replacement waits for the next player move.
- Player HP and unused protection carry between opponents. There is no automatic wave heal.
  Excess damage never transfers to the next opponent, which starts at its own full HP.
- Opponents use fixed per-wave attack damage. Shields come from the level board; optional
  obstacle volleys run after a surviving enemy's counterattack. The first two production bosses
  have no volleys. Later bosses use authored pools of previously introduced debris/overlays.
- Balance the entire encounter around the number of counterattacks, considering obstacles,
  special creation, and observed move powers. Strong moves earn fewer counterattacks.
- Initial example only: player HP 100, power per tile 1; opponents have HP/attack pairs
  60/8, 90/10, and 120/12. Repeated 30-tile moves require 2/3/4 attacks and incur
  64 total damage, leaving 36 HP. Repeated 20-tile moves would require 116 damage worth
  of counterattacks to finish without protection, exceeding the player's HP.
- Author opponents using `bossWaves`: `hpWeight` partitions the total BossDamage goal,
  `attackDamageBase` sets attack power, and `characterProfile` selects each opponent's poses.
  Goal 270 with weights 60/90/120 produces those exact HP values. Only 1–3 positive-HP waves
  are used; their HP sum stays equal to the goal, including very small totals and skewed weights.

### Shield protection (approved)

- Every collected shield adds 2 protection points to the corresponding character's pool.
  Both player and enemy use the same rule; protection has no timer and does not expire idle.
- At impact: absorbed damage = min(incoming damage, protection); subtract absorbed damage
  from protection, then apply the remaining damage to HP. HP damage cannot be negative.
- Only the protection actually used is consumed. Against a 10-damage attack, 1/2/3/5
  pickups yield 8/6/4/0 HP damage. Six pickups yield 12 protection: the attack removes
  10 protection, deals no HP damage, and leaves 2 protection for a later attack.
- If the enemy dies before counterattacking, player protection is not consumed and carries
  to the next opponent. The new opponent does not inherit the defeated opponent's protection.
- Show available protection as a small shield icon with the remaining protection value.
  Show the full shield pose as soon as protection is collected and keep it while protection remains.
  Attacks temporarily use attack poses; a protected hit adds the brief block recoil.
  Make absorbed damage and HP damage readable: a 40-power hit against 4 protection
  consumes 4 protection and deals 36 HP damage, subject to remaining HP.

### Defeat presentation

- Use a slight stagger, tilted head, and 2–3 orbiting stars with a brief dazed motion;
  avoid blood and a hard collapse. Existing art can serve initially; a dedicated dazed
  pose with partly closed eyes and a tilted head is a later visual improvement.
- Player defeat leads to the loss result. Enemy defeat leads to the next opponent or,
  after the last opponent, victory.

## Current behavior

- The player's three supplied idle images become idle, occasional blink, and focused poses.
- The previously supplied R3/R4 images provide hammer windup and impact.
- Badger idle alternates between p1 (resting) and p3 (ready). p2 is focused/recovery.
- Badger attack poses follow p4 → p3 → pA3: windup, dash approach, impact.
- Both characters dash horizontally to the opponent in 0.18s and return in 0.2s, leaving
  fading colored silhouettes. Ram: cyan/gold; badger: orange/red. There is no jump arc.
  Each actor reuses at most 12 non-interactive afterimages; disable/reset clears the effects.
  The UI silhouette material preserves sprite alpha while replacing RGB with the trail tint.
- Yellow/orange/red tapered impact streaks spawn at the strike sprite's weapon contact point,
  in the same frame as damage. Power controls size, thickness, count (4–10), and redness.
  `powerForFullImpact` (default 40) caps only visual intensity, never damage.
  `weaponImpactPoint` is editable per character for differently cropped strike artwork.
- Attacks are serialized so the two characters do not dash through each other.
  A winning attacker returns home before its victory hop and level-end hold are released.
- Original PNGs are copied unchanged under `Art/UI/RoboCharacters/Ram/Duel` and `Badger`.
- Each pose has a height multiplier and ground pivot. These align differently cropped images
  without modifying their pixels. Final pose alignment still needs visual tuning in Play mode.
- Cleared tiles, including cascades and special clears, accumulate
  `tile count × LevelData.damagePerClearedTile`. One melee impact applies the total after
  board activity settles. Power is displayed in a vertical meter left of the player.
  For example, `damagePerClearedTile = 1` and 20 clears in the move produce one 20-damage hit
  against an unshielded enemy with at least 20 HP. Existing level values are not overwritten.
- Cleared tiles are the only source of player damage. The former special-activation bonus,
  PatchBot projectile, colour weakness multiplier, stun multiplier and super laser are gone.
- Shield pickups add persistent protection, displayed as a shield badge and remaining points
  below each HP bar. The supplied full-body shield pose stays visible while protection remains, except during an
  attack. Protected impacts briefly recoil; spending the last point still shows the final block. A toast separates
  absorbed damage from HP damage. An optional `shieldHit` pose can replace the block pose.
- Melee turns do not block input. An obstacle volley waits for board activity to finish, then
  locks input for its 0.38-second flight so targets cannot move underneath it. A strike, counterattack, daze or wave entrance plays while
  the player keeps swapping; those clears are not dropped, they accumulate into the next strike,
  which starts as soon as the running turn finishes. Thrown debris converts a normal tile in place without awarding clear credit or power. Result evaluation still waits for the whole turn.
- The level-end hold starts synchronously when the move starts. Settling explicitly excludes
  this hold, avoiding a wait on itself. Disabling the controller releases the gate and hold.

## Level-end watchdog during continuous play (2026-09-21)

Editor.log showed the 30-second force-drain firing with 17/18 moves remaining and goals
incomplete, while swaps and opponent transitions were still progressing. A settle wait
started by an ordinary move was timing the whole busy encounter, then clearing its live
BossStrikeDrain counter. Level-end evaluation now starts only when moves are exhausted
or goals are complete. An existing wait cancels if extra moves resume unfinished play.
The real terminal-result leak recovery and final-strike wait remain intact. The boss
hold diagnostic resets on actual progress (moves/clears, damage, shield absorption and
opponent changes) instead of treating consecutive turns as one stalled strike.

`csi Tools/check_level_end_wait.csx` exercises the production evaluation methods with
board doubles: continuing play, final strikes, late goal completion, extra moves and real
terminal leaks. These checks and `Tools/check_boss_duel.csx` pass without a project build;
Play-mode reproduction remains to be verified.

## Finishing strike — 2026-09-22

Only the player's predicted lethal hit against the final opponent gets a finishing
animation. The threshold includes current enemy protection, uses a wide integer sum,
and is checked again immediately before contact. An enemy shield collected during the
preparation can remove the finishing impact emphasis; it does not cancel the actual hit.

The character profile's **Final opponent — finishing strike** controls enable/disable,
windup multiplier (default 1.4), slow airborne approach (0.6s to 60% of the path), hop
height (18% of standing height), fast descending contact (0.035s), and brief strike-pose
hold (0.065s). These values are explicitly serialized on `RamDuelCharacter`. The motion
follows one continuous parabola, with its apex halfway and zero height at impact.
The normal return and existing victory
sequence follow. Finishing impact streaks use full visual strength and a 1.15 size/duration
multiplier, regardless of how little HP the enemy had left. Gameplay damage is unchanged.
The slow motion affects the actor's choreography only: no global `Time.timeScale` change,
new board lock, or additional end-evaluation hold is introduced. Intermediate opponents
and enemy counterattacks retain normal timing.

`csi Tools/check_boss_finisher.csx` executes the production Attack coroutine with doubles
and covers ordinary timing, actual vertical lift, arc endpoints/crest in both directions,
ground-level impact, slower preparation, faster contact, single impact, victory
return, profile disable, late protection, cancellation and disposal. Finisher eligibility
is covered by `Tools/check_boss_duel.csx`. Rendering still needs Play-mode review.

## Remaining art

The badger profile is assigned to `enemyCharacter` in `01_Game`, and both legacy enemy
cannon objects are hidden. Badger source art already faces left, so mirroring is disabled.
Use the same profile format for a bear; a weapon sprite and Rigidbody are not required.

## Opponent order and the ghost exit (2026-09-20)

- `BossDuelController.opponentRotation` lists opponents by position: index 0 is the first,
  index 1 the second, and so on. A wave that carries its own `characterProfile` still wins;
  the rotation only fills the gap the automatic wave formula leaves, so a multi-opponent
  encounter no longer repeats one animal. A shorter list holds its last valid entry.
  `01_Game` sets it to badger, then hyena.
- `HyenaDuelCharacter.asset` maps the five supplied images: S3 rests, S1 stands alert
  (idle alternate and victory), S2 crouches low (focused, dash pose and defeat), SA1 raises
  the crowbar (windup) and SA2 swings it (strike, weapon impact at 0.13/0.35). The hyena has
  no shield pose yet. Protection remains visible through the HP-bar badge; animal profiles
  no longer fall back to the old circular robot shield when shield artwork is missing.
- A defeated opponent no longer disappears between frames. After its daze it drifts upward,
  shrinks to `ghostExitScale`, and fades out, and only then does the next opponent slide in
  from the right. The rise and shrink run on the robot root, so they never fight the pose
  animation on the body child; the fade uses a CanvasGroup so the daze stars and shield
  bubble vanish with it. `ghostExitDuration = 0` restores the instant swap.

Victory reuses the standing pose with a hop. Defeat reuses a standing pose with a slight tilt,
stagger, and three orbiting procedural stars. An out-of-moves daze resets if extra moves are
added; an HP defeat remains terminal.
R4 contains its own ground impact effect. Dedicated victory, defeat, and clean impact art
can replace these profile entries later.

## Play-mode checks (not run in this change)

1. Clear 3 tiles with damage-per-tile 10: power reaches 30, enemy loses 30 once at impact.
2. Cause a cascade/special chain: all clears join the same attack; no intermediate laser or hit.
3. Try rapid taps/drags and boosters during that move: no second move is consumed until recovery.
4. On the last move, kill the final enemy: success, with no premature out-of-moves popup.
5. On the last move, leave enemy HP remaining: fail evaluation resumes after the attack.
6. Set damage-per-tile to 0: no hit and no stuck input/level-end hold.
7. Strike an enemy with 4 protection at power 40: protection becomes 0, HP drops by 36.
8. Finish a wave: death/entrance visuals complete, the next wave and its HP initialize correctly.
9. Lose during windup: the pending player impact is cancelled and defeat pose is retained.
10. Enter a normal level: duel UI is hidden and dynamic board input works as before.
11. Disable/leave the duel during a chain or swing: no retained boss drain count or input gate.
12. Watch the badger: p4 → p3 → pA3, one hit on arrival, then p2 on the return dash.
13. Both characters leave and return to their own ground positions without moving the HP bars.
14. Wait idle, then resolve a move: no timed/charged attack; one counter after the player returns.
15. Land a winning hit: return dash completes before victory hop/result flow, without teleporting.
16. Collect either shield: its badge gains 2; the normal idle pose remains until impact.
17. Receive 10 damage with 2/4/12 protection: lose 8/6/0 HP and retain 0/0/2 protection.
18. After a protected hit, the shield reaction ends even if protection remains; the badge persists.
19. Wait idle: protection does not expire. Advance a wave: player protection persists, enemy starts clean.
20. Set tile power to 1 and clear 20 tiles (including cascades): exactly one 20-damage hit.
21. Compare power 3, 20, and 40+: strike streaks increase in intensity; HP damage stays exact.
22. Both dashes leave colored sprite silhouettes which fade in place and never intercept input.
23. Finish/reset/leave during a dash: no leftover effect objects; victory still waits for return.
24. Defeat an intermediate opponent on the last move: next opponent does not counterattack;
    after the transition, the normal out-of-moves result appears.
25. Receive lethal counter damage: enemy finishes its return, player dazes, then fail appears.
26. Run out of moves with HP remaining, then accept extra moves: stars disappear and play resumes.
27. Use three different wave profiles: each opponent uses its own poses and trail material.
28. Repeat an encounter with 30-tile moves and the 60/90/120 HP, 8/10/12 damage setup:
    nine moves are needed, six counters deal 64 damage, and player HP ends at 36.

No Unity, dotnet, or project build was run. `Tools/check_boss_duel.csx` parses the changed C#
files and executes production protection/turn/hold methods and the wave builder with isolated
service doubles. The checks cover shield conservation, ordering, zero-power turns, last-move
win/loss, extra-move pose recovery, and exact wave HP totals. Run from the repository root:

```sh
/Library/Frameworks/Mono.framework/Versions/Current/Commands/csi Tools/check_boss_duel.csx
```

Unity animation timing, UI placement, and integration with live board cascades still need the
Play-mode checks above. No scene references are required for the new procedural badges/stars.

## Legacy duel removed, power meter and energy orbs (2026-09-20)

The robot duel's rules and their UI are deleted rather than disabled. Gone: the `animalDuel`
switch and every branch behind it, the rapid-fire bolt queue with its arms/muzzles/prefabs,
the interruptible charge attack and stun, the colour-weakness badge and its damage multiplier,
the super laser badge and beam, the runtime shield-pickup spawner, the special-activation bonus
strikes, timed shields, the per-wave heal, and the generated sprites and localization keys that
only those features used. The controller went from 3103 to about 1800 lines, `BossDifficulty`
now resolves only HP, counter damage, oil and appearance, and `BossWaveDef` keeps only the
fields an animal opponent uses. The four legacy cannon arms were already inactive in `01_Game`,
so removing their references changes nothing on screen. `BossDuelIntroController` is untouched
but is still unassigned in the scene, so it does not run.

Two readability changes replace what was removed:

- **Bar contrast without new art.** The HP bar track and fill sprites were two shades of the
  same colour. The track Images are now tinted dark (a multiply tint can only darken, so the
  fill keeps its full colour), which separates them by value instead of by hue.
- **Power meter.** The plain "GÜÇ n" label became a vertical bar standing to the player's left,
  filling from the bottom, with the number beneath it. The fill is the share of the opponent's
  *remaining* HP this hit will remove, so a full bar means the hit is lethal. It uses the HP
  bar's own artwork: `PowerBar.png` and `PowerBarFill.png` are the existing frame and fill
  rotated upright, with their saturated greens hue-shifted to gold and the grey rim left alone.
  A recoloured copy is needed because a canvas tint multiplies and therefore cannot turn green
  art yellow; the track is then darkened by tint, the same trick that separated the HP bars.
- **Energy orbs.** Every cleared tile releases an orb tinted with that tile's own colour, from
  `BoardBreakFxService.ColorForTileType`, now shared with the break FX so both read one table.
  Each orb arcs from its cell to a rally point over the board centre and circles there while the
  cascade keeps feeding the ring; once clearing stops, the whole ring streams into the meter one
  after another, each orb dragging a trail sampled from its own position history. The orbs are
  presentation only: power is still counted synchronously when the tile clears, because the
  level-end hold depends on it; an orb's arrival only raises the displayed number and pops the
  meter, and the displayed value snaps to the true one at the moment of the strike. A move's
  attack waits for the last orb to land. Orbs are pooled and capped at 18; past that the credit
  is applied instantly without a visual, so the meter never lags behind a large cascade. Every
  duration, the ring radius, the orb size and the trail length are editable in the Inspector.

## Play continues through the strike (2026-09-20)

The duel no longer takes input away. `BoardController.BossDuelTurnPending` and the three input
checks that read it are gone, so swaps, taps and boosters stay live while a strike, counter,
daze or wave entrance plays.

What made the lock necessary was that `HandleTilesCleared` dropped every clear arriving during a
turn, so anything the player did mid-animation was lost. That drop is now narrowed to the window
in which an enemy oil effect is placing tiles, which is the only clearing the player should not
be paid for. Everything else accumulates: power keeps rising during the animation and becomes the
next strike, which begins the moment the running turn ends. Turns never overlap — a second one
waits for the first — but the player never waits for either.

A turn used to end by waiting for the board to settle so the out-of-moves daze could be judged.
With free input that wait could be held open indefinitely by a player who keeps playing, stalling
the next strike, so it now runs only when the move counter has actually reached zero, and the
daze waits until no queued power or open move remains. The level-end hold is unchanged and still
covers the whole turn, so no result popup can appear mid-strike.

## Obstacle throw sheets (2026-09-22)

Automatic waves now use an opaque white body tint. The old second-wave orange and third-wave
red multipliers discolored Hyena's artwork; these have been removed. An explicitly authored
`BossWaveDef.bodyTint` remains available for intentional visual variants.

Badger and Hyena now use the twelve sliced sprites from `BadgerThrow/BadgerThrow.png` and
`HyenaThrows/HyenaThrows.png`. Their character profiles own `throwFrames`, each containing a
ground-aligned pose and a normalized `handPoint`. Heights compensate for the different trimmed
frame sizes; hand points are authored per frame. The existing melee recovery finishes first,
then the enemy stands at its idle ground position for 0.08 seconds before throwing in place.

Default playback is 16 frames/second, with release at zero-based frame 6 (the seventh frame).
The actual obstacle preview sprite follows the hands until release. A volley initially shows
one held prop; all requested projectiles leave the same hand point together and grow from
character-relative size to board-cell size in flight. Flight overlaps the remaining character
frames. The character returns to its rest/shield pose afterward. These timings, frame poses,
hand points and held size are editable in `BadgerDuelCharacter` / `HyenaDuelCharacter`.

The short pressure input lock now covers preparation and flight, so selected target cells
remain stable. Cancellation disposes the nested animation and projectiles; aborted throws
do not place obstacles. Profiles without a complete throw sheet retain the previous flight.
Throw count, frequency, eligible obstacle pools, pressure caps and the first two bosses'
disabled pressure settings are unchanged.

`csi Tools/check_boss_throw.csx` checks sprite references and executes the production throw
and volley iterators with test doubles: release timing, hand transforms, simultaneous flight
and recovery, cancellation, disposal, fallback and placement. No Unity build or Play-mode
visual review was performed; final hand alignment should be checked in the running scene.

## Boss duel sounds (2026-09-22)

The controller loads unassigned clips from `Resources/Audio/BossDuelsSound` when a boss duel
initializes and preloads their audio data before combat. Inspector clip overrides are supported.
`PlayerAttack` / `EnemyAttack` play when that actor's fast swing begins; for the finishing strike
this occurs after the slow airborne approach. `PlayerHit` is the player's hit on the enemy;
`EnemyHit` is the enemy's hit on the player. Both hit sounds fire at contact, including shield
contact. `EnemyObstacleThrow` plays once per volley, exactly when the obstacle leaves the hand,
while cancelled preparations stay silent. The existing sound-enabled setting gates playback.
Attack, impact and obstacle-throw volumes are adjustable on the controller. Missing side-specific
hit clips fall back to the old `hitSfx`. No in-game listening pass has been performed.
