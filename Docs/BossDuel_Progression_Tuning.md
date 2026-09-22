# Boss progression — 2026-09-21

Scope: the first 50 entries in the active `LevelCatalogPro`, containing bosses at
5/10/15/20/25/30/35/40/45. Catalog level 50 is the normal `LevelP_00510` asset.
`Levelp_00355` occupies slot 36, so file numbers are not a reliable progression index.
Normal-level tuning is unchanged. All boss goals retain the BadgerIdle override.

## Encounter budget

The first boss remains an accessible introduction, with its length revised on 2026-09-22.
The first boss uses five power per cleared tile; later bosses use one, making two-point
shield pickups meaningful against the later damage scale. HP still
comes exclusively from the BossDamage goal; wave totals equal that goal exactly.
Wave counts are explicit so opening an asset in the editor does not depend on saved
`current_level`. Moves are unchanged.

| Catalog level | Total enemy HP | Player HP | Power/tile | Opponents | Counter damage by opponent | Volley |
|---|---:|---:|---:|---:|---|---|
| 5 | 500 | 100 | 5 | 1 | 4 | Off |
| 10 | 220 | 120 | 1 | 1 | 5 | Off |
| 15 | 260 | 120 | 1 | 2 | 6 / 8 | 1 per 3 counters |
| 20 | 280 | 125 | 1 | 2 | 6 / 8 | 1 per 3 counters |
| 25 | 300 | 125 | 1 | 2 | 7 / 9 | 1 per 3 counters |
| 30 | 330 | 135 | 1 | 3 | 6 / 8 / 9 | 1 per 3 counters |
| 35 | 360 | 140 | 1 | 3 | 7 / 9 / 10 | 2 per 4 counters |
| 40 | 390 | 155 | 1 | 3 | 7 / 9 / 10 | 2 per 4 counters |
| 45 | 420 | 175 | 1 | 3 | 8 / 10 / 12 | 2 per 4 counters |

At a constant 30 clears/move, the simplified damage budgets require 4/8/9/11/11/13/13/14/16
moves respectively, leaving 88/85/70/61/52/57/52/58/43 player HP before shield benefits.
At 20 clears/move, the last three bosses are marginal without shields (0/-4/-17 HP),
making their twelve authored player shields useful. These are sensitivity calculations,
**not measured win rates**: actual clears depend on board topology, cascades, special use,
pressure and shield collection timing. The intended result is an accessible first 50 levels
with gradually increasing incentive to make strong moves.

## First-boss pacing revision — 2026-09-22

The reported roughly five-move completion of `LevelP_00050` is the early-player anchor.
The original 200 HP / 5 power required only 40 cleared tiles. The final revision keeps
five power per tile and raises boss HP to 500, requiring 100 clears. It also exceeds
the intervening 80 HP / 1 power setting's 80-clear budget. Counter damage is 4;
player HP stays 100, moves stay 25, the board remains obstacle-free and volleys stay off.

An illustrative sequence `[6,9,3,12,10,6,9,3,12,10,6,9,6]` wins in five moves with the
original settings and thirteen now, leaving 52 player HP. At constant 6/8/10/12 clears
per move, the new encounter takes 17/13/10/9 moves and leaves 36/52/64/68 HP. A strong
20-clear average still wins in five moves; there is no damage cap or artificial minimum
number of moves. These calculations do not substitute for observed Play-mode results.

## Obstacles and boards

Verified first appearances in the active catalog: Oil 7, orange plastic 9, plastic 13,
blue plastic 16, red plastic 17, HelmetPorcelain 42. Every authored projectile appears
strictly after its introduction. No chest, wardrobe, sculpting stone, generator, cargo,
shield pickup, egg or coin can be thrown, even if accidentally selected in the editor.

| Boss | Projectile rotation | Maximum present |
|---|---|---:|
| 15 | Oil, orange plastic, plastic | 6 |
| 20 | Oil, orange plastic, blue plastic | 6 |
| 25 | Oil, plastic, red plastic | 6 |
| 30 | Oil, orange plastic, blue plastic, red plastic | 6 |
| 35 | Oil, plastic, blue plastic, red plastic | 7 |
| 40 | Oil, orange plastic, blue plastic, red plastic | 8 |
| 45 | Oil, HelmetPorcelain, blue plastic, red plastic | 8 |

One type is used per volley; the rotation continues across opponents. A defeated opponent
never throws. Counters toward the next volley reset for the new opponent.

The pressure cap counts existing obstacles from the pool, including authored ones. There
is also a hard ceiling of one quarter of usable cells. A dense board delays volleys until
the player opens room. Targets exclude holes, obstacles, locked/reserved/moving cells,
specials and absent tiles. Each target is checked cumulatively to preserve a playable swap.
The check treats movable debris as locked, conservatively.

Projectiles visibly travel from the enemy to the chosen cells. Selection waits for board
jobs to settle; input pauses only for the 0.38-second volley. Movables replace the normal
tile in place without generating clear events, player power or goal progress. Overlays
use the existing dynamic view event. Cleanup releases the lock and projectile objects on
completion or controller disable.

Level 20 retains its 14-hole funnel but removes 15 alternating orange blockers from the
top six rows, allowing horizontal matches. Level 45 opens 25 cells among its helmet and
sculpting-stone walls; initial occupied cells decrease from 63 to 38. Its shield positions
and board mask are unchanged. Other boss layouts are unchanged.

Mud and SpreadingGel remain selectable supported overlays for future authoring. Gel is
deliberately absent from the difficulty pools: the existing gel is an indestructible,
nonblocking coverage objective that benefits the player. It must not be relabeled as a
blocking attack without a separate mechanic. Oil supplies the tile-covering pressure here.

Old explicit wave arrays in bosses 35/40/45 had transparent body tints and superseded
the level's pressure settings. They now use the standard wave formula with explicit
three-opponent counts and visible tints.

## Shield display

Pickup immediately selects the full-body shield sprite when available. Protection stays
visible while points remain; attacks temporarily use their attack poses. Absorbed hits
still recoil, including the final hit that consumes the last point. Animal characters without
shield artwork show protection through the HP-bar badge only; the old circular bubble
is reserved for legacy actors without a character profile. The player carries protection between
opponents; enemy protection resets. No shield arithmetic was changed.

## Verification and remaining runtime checks

- `python3 Tools/audit_boss_progression.py`: catalog order, prior introductions, allowed
  projectile types, sprite availability, board arrays/origins, no obstacles in holes,
  exact HP sums, fixed wave counts, first-two-boss exclusion, damage-budget scenarios.
- `csi Tools/check_boss_duel.csx`: production C# syntax plus isolated extracted-method
  checks for protection arithmetic, encounter turns, moves and wave HP allocation.
- `csi Tools/check_boss_pressure.csx`: extracted production methods with board/Unity doubles;
  projectile exclusions, caps, occupied/special/moving-cell avoidance, playable-swap
  preservation and shield visibility during collection, depletion and attacks.

No Unity/project build or Play-mode session was run. The existing SimRunner explicitly
does not simulate collectible goals/BossDamage, so `_SimStats.csv` is not evidence of boss
win rates. Play-mode verification remains necessary for projectile alignment, movable
fall/clear presentation, oil overlay rendering, both characters' shield poses, interrupted
volleys, late-wave transitions and real player completion rates. Start with bosses
15/20/35/45 and a low-power run using shields.
