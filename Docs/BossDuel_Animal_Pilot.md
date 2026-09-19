# Boss Duel — animal pilot

`01_Game` now enables `BossDuelController.animalDuel` and references
`Assets/_Project/Settings/RamDuelCharacter.asset`.

## Current behavior

- The player's three supplied idle images become idle, occasional blink, and focused poses.
- The previously supplied R3/R4 images provide hammer windup and impact.
- Original PNGs are copied unchanged under `Art/UI/RoboCharacters/Ram/Duel`.
- Each pose has a height multiplier and ground pivot. These align differently cropped images
  without modifying their pixels. Final pose alignment still needs visual tuning in Play mode.
- Cleared tiles, including cascades and special clears, accumulate
  `tile count × LevelData.damagePerClearedTile`. One melee impact applies the total after
  board activity settles. Power is displayed below the player's HP bar.
- No separate special activation bonus, PatchBot projectile, color multiplier, stun damage
  multiplier, or super laser contributes to player damage in animal mode.
- Enemy shields still absorb the hit. Damage remains capped at the current wave's remaining HP;
  overkill does not transfer to another wave. Existing wave balance and enemy attack cadence remain.
- New input waits through the move and its attack/recovery, so successive moves cannot merge
  into a single power total. This input gate does not suspend resolve work or alter popup locks.
- The level-end hold starts synchronously when the move starts. Settling explicitly excludes
  this hold, avoiding a wait on itself. Disabling the controller releases the gate and hold.

## Remaining art

The enemy still uses the existing robot artwork as a placeholder, with melee timing instead
of laser projectiles. Assign a badger `BossDuelCharacterProfile` to `enemyCharacter` when ready.
Use the same profile format for a bear; a weapon sprite and Rigidbody are not required.
`mirrorHorizontally` supports right-facing source art on the enemy side.

Victory/defeat currently reuse standing poses with the existing hop/collapse animation.
R4 contains its own ground impact effect. Dedicated victory, defeat, and clean impact art
can replace these profile entries later.

## Play-mode checks (not run in this change)

1. Clear 3 tiles with damage-per-tile 10: power reaches 30, enemy loses 30 once at impact.
2. Cause a cascade/special chain: all clears join the same attack; no intermediate laser or hit.
3. Try rapid taps/drags and boosters during that move: no second move is consumed until recovery.
4. On the last move, kill the final enemy: success, with no premature out-of-moves popup.
5. On the last move, leave enemy HP remaining: fail evaluation resumes after the attack.
6. Set damage-per-tile to 0: no hit and no stuck input/level-end hold.
7. Strike an active enemy shield: one absorbed hit, then power resets.
8. Finish a wave: death/entrance visuals complete, the next wave and its HP initialize correctly.
9. Lose during windup: the pending player impact is cancelled and defeat pose is retained.
10. Enter a normal level: duel UI is hidden and dynamic board input works as before.
11. Disable/leave the duel during a chain or swing: no retained boss drain count or input gate.

No Unity, dotnet, or project build was run. Static checks cover asset references, unchanged
source-image hashes, scene edit scope, and C# delimiter balance; runtime behavior remains to
be verified in the Unity Editor.
