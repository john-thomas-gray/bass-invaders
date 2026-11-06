# Occupied-column movement

## Rule

Only an occupied column can trigger a turn and descent. Movement to the right
uses the rightmost occupied column; movement to the left uses the leftmost.
One enemy anywhere in a column is sufficient. Color, special state, and row do
not affect whether that column is occupied. Gaps between occupied columns do
not matter for the horizontal boundary calculation.

Contact uses the outer edge of the logical cell, not sprite pixels or shields.
This keeps movement stable when art changes or shield/animation visuals grow.
The current scene uses an unrotated grid with positive horizontal scale.

Horizontal average speed is proportional to the live Green count: 0.015 world
units per second per Green, multiplied by level scaling. Zero Greens means no
motion. The numerical timing examples below assume one Green at level-one
speed. `UseGreenDashes` retains the previous nonzero-base-speed mode, disabled
by default.

### Beat-Locked Ticks

The default fleet moves on counts two and four of the current song, holding still
on counts one and three. Each tick keeps the original single-beat distance,
using the configured movement rate multiplied by the completed beat duration.
Skipping the other two counts halves the average speed at a constant tempo.
Green count and level change tick length rather than tick cadence. The fallback editor beat uses the
default soundtrack tempo when no song is playing. The final beat at an edge can
be shortened to preserve exact boundary timing, then any remaining beat distance
is spent after the turn unless the callback stops or resets movement.

Each movement tick sparks one live Green using `EnemyPresentation.MovementPulse`.
The rotation starts in row/column order, then repeats; removed or recolored
Greens are skipped and newly appearing Greens join the queue. Both ordinary
and special Greens participate. The pulse changes only presentation tint and
electrical arcs without arrows. Collision geometry and ability cooldowns stay
the same. Special-Green dashes remain a separate ability. Pause preserves partial
beat progress; a new wave resets it.

## Geometry

The playfield edges are x = -3 and x = +3, shared with player wrapping and the
visible border. Six logical columns are spaced 0.75 world units apart. Their
local centers are:

    localCenter(column) = (column - 2.5) * 0.75
    occupiedLeft  = gridX + localCenter(firstOccupied) - 0.375
    occupiedRight = gridX + localCenter(lastOccupied)  + 0.375

The implementation converts cell edges through the grid transform, rather than
assuming its origin remains fixed. At unit scale, a level 1-6 row occupies
columns 0-4, spans 3.75 units, and reaches the right edge when gridX is +1.5.
At speed 0.015 units per second, that means about 100 seconds to the first right
contact from center and 150 seconds between opposite contacts if both outer columns remain
occupied. A level 7+ row occupies columns 0-5, spans 4.5 units, and reaches the
right edge when gridX is +0.75.

If column 4 is empty, the rightmost possible occupied column becomes 3. It must
travel another 0.75 units before touching the right border. At the default
speed, this adds about 50 seconds. Clearing the last enemy in column 3 adds another
column's worth of travel. These are geometric consequences, not timers.

## Movement update

`GridModel.OccupiedColumns` scans live enemy records to find the first and last
occupied columns. `EnemyGrid.OccupiedHorizontalBounds` converts their outer
edges into world coordinates. Neither operation caches occupancy, so removals
are reflected on the next movement update.

`EnemyGridMovement.Tick` tracks beat position and only spends movement when the
song crosses count two or four. Long frames can process multiple ticks, and each
new tick reads the live Green count. `AdvanceDistance` handles each tick against
the distance to the relevant border:

1. If the grid is empty, do not move, turn, or request descent.
2. If the frame cannot reach the border, move normally in the current direction.
3. Otherwise, move exactly to contact, reverse direction, and emit `SweepEnded`.
4. The spawner handles descent and new rows, stopping descent at occupied capacity.
5. Recompute bounds and spend any leftover travel against the new state.

Recomputing after the callback matters: spawning can expand the occupied range
and relabel columns. A single slow frame can cross multiple boundaries, but each
event corresponds to an actual occupied-edge contact. Disabling movement during
game over stops the loop immediately. Resetting the sweep increments a version
counter, preventing unused travel from leaking into a reset fleet. A stationary
fleet or a formation occupying the entire field width remains still.

This replaces both the old reflected-offset formula and its time-based turn
counter. Direction is now explicit state, and there is no Travel Distance
setting.

## Fitting a new row

A sparse fleet can travel beyond the position where a full five-column row fits.
Simply spawning columns 0-4 at that origin would put new enemies offscreen.
Snapping the origin back alone would make survivors jump sideways.

`EnemyGrid.AlignForNewRow` instead shifts column labels and the origin by equal
and opposite amounts. `GridModel.TryShiftColumns` validates every destination
before rebuilding its cell lookup. All surviving enemies receive the same
column shift, so their adjacency, identity, color, and relative shape survive.

Example: only columns 0-3 remain occupied when a five-wide row reaches the
right wall. The grid origin is x = 2.25. Before spawning:

- Shift all survivor column indices right by one.
- Move the grid origin left by 0.75 to x = 1.5.
- Rebuild each survivor's local position from its new column.

A survivor formerly in column 3 was at 2.25 + 0.375 = 2.625. After relabeling it
is in column 4 at 1.5 + 1.125 = 2.625. Its horizontal screen position is unchanged.
It then descends one row normally. The level 1-6 row fills columns 0-4 entirely
inside the border; level 7+ rows fill columns 0-5. Left-edge contacts use the
mirrored operation.

The spawner checks the bottom boundary before relabeling or descending, so an
occupied bottom row blocks additional descent and rows without ending the run.
Horizontal sweeping continues. The grid has eleven rows: the final row is at
player height, while the original ten retain their positions. Only body contact
between a live player and an enemy in that final row triggers game over.

Horizontal fleet movement and player movement test the whole traveled segment
against stable body bounds, stopping at the earliest contact. Wrapping is split
into movement to the edge and movement from the opposite edge, not a sweep
through the field. Arrival by descent or Purple summon also checks contact.
Shields, tongues and detached death effects are not enemy bodies. Pause defers
contact checks; the absent player during recovery cannot collide, but missile
invulnerability after reappearing does not prevent game over from body contact.
Batch refill still restores the original grid position and starts moving right.

## Verification

The editor suite covers clearing a leading column immediately before contact,
empty columns on both sides, occupancy in a lower row, empty fleets, zero speed,
30/60/120-frame-per-second simulation versus a long frame, callback-driven stop
and reset, and no-room-to-move protection. Spawn tests cover both directions and
verify survivor positions, identities, occupancy, and full-row bounds.

The Play mode suite additionally removes all but one occupied column, verifies
that the previous full-row boundary no longer causes a descent, and continues
to real contact. It checks exactly one new row, preserved survivor positions,
and all spawned cells inside the border. Existing matching, retreat, abilities,
refill, game-over, and restart checks also pass.
