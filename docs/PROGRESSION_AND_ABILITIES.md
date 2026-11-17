# Progression and Ordinary Abilities

## Recoverable Death And Forcefield Return

Missile hits use the same replaceable
player-death cue as fatal contact. Each run starts with the active player plus
two spare lives. Two miniature slimes replace the numeric Lives readout. Their
color follows the player and their art copies the actual player sprite pieces
and proportions, while their movement is independent. They
pulse together, occasionally swaying left and right for a complete four-beat
measure. Routine changes only occur at measure boundaries. After a recoverable
hit, the rightmost spare jumps down from the life row toward the player rather
than draining, slashing or splitting. The third death ends the run unless a
life has been regained. Full reward bar sets replenish one spare, capped at two.
An earned slime emerges from behind the nearest border, takes a short step onto
the final bar, then flips upward from its spike. Its arc clears the life-row
ledge before descending into its slot. A full set at the existing cap makes all spare
slimes flip together once; this one-off reaction need not wait for a measure.

The replacement sometimes flips on takeoff (one chance in three). At the jump's
apex, 0.33 seconds after the hit, the real player takes over and can move and
fire during the descent while invincible and fully visible. Landing at 1.5 seconds
begins the 1.5-second flashing protection timer; airborne time does not consume it.
Replacement death cues do not delay control.
If a landing gap is temporarily unsafe, the slime stays steerable above the row
until it can land safely, retaining untimed protection until touchdown. The fleet keeps moving; ordinary pause
also pauses recovery and the spare-life animation. A full enemy row at the player
still forces game over regardless of spare lives.

Special Blue forcefields slow the stunned tongue's return using the archived
`legacy-v1:Candy Cruisers/Assets/Scripts/Tongue.cs` rule: `maxSpeed = 20` (also
verified in the archived Player prefab), with `speedFactor = -0.25 + 0.01 * level`.
The positive return rate is `20 * (0.25 - 0.01 * level)`: 4.8 at level 1,
3 at level 10 and 1.2 at level 19. The rebuild floors this at 0.2 units per second
to avoid the archived zero/reversed return at level 25 and above. The rate is
captured at impact and applies immediately, including leftover time in that
frame. Matching Blue and magic shots still bypass the forcefield. Normal shots
retain their existing return speed, and stun ends when the tongue returns or is canceled.

## Progression

## Orange: Singleton Swap

Orange unlocks at level 9 and never receives a special tier. Each eligible new
wave guarantees exactly one Orange. Later row slots and Purple summons each
have a 1-in-75 chance while no Orange is present, subject to color-clear locks.
Only one actual Orange may spawn on screen. All mimics retain Yellow identity
until a regular mimic permanently converts; copied Orange appearance alone does not consume an Orange slot. An Orange-only override starts with one
enemy and leaves subsequent slots empty unless their rare Orange roll succeeds.
The older retreat-explosion behavior remains as a defensive fallback for custom
fleets, but normal spawning can no longer produce two Oranges.

On its randomized
cooldown (6-12 seconds initially, with the existing level-based maximum reduction),
it flashes, casts for .35 seconds, then swaps positions with an eligible singleton.
The target must have no same-color or active imitation-chain connections, and
its color must match a different enemy next to the Orange's original position.
Eligibility uses true colors; mimics remain Yellow until permanent conversion. A missing eligible
target simply consumes that attempt and waits for the next cooldown.

Spawn and swap destinations cannot put two Oranges in orthogonally adjacent
cells. Batch/new-row planners keep Orange runs to one enemy and extend an
existing neighboring color when a vertical conflict would occur. Orange-only
developer overrides deliberately leave empty cells. Summons filter illegal
Orange destinations before choosing a color. Swaps update both identities and
cells atomically, then show orange portal effects at both endpoints.

Retreat is the exception: if it brings Oranges together, their entire connected
Orange group immediately explodes. Each awards five times the current level's
base enemy points. Defeat progression, last-of-color rewards and fleet bonuses
still apply; shot combos do not multiply these environmental points or receive
hit credit. Orange explosions never create a special tier. Both ordinary and special Yellow can attach to Orange and gain its ability.

### Quiet Player Color Assistance

`EnemyGrid.PlayerColorWeights` supplies both eligibility and selection weights
when the player needs a new color. `SelectablePlayerColors` exposes the same
eligible set. Formation changes do not replace an already-ready color.

- A barrier is a connected line of special Blues spanning the formation's
  leftmost through rightmost occupied columns. Connections may be horizontal,
  vertical, diagonal, or mixed. Unused outside columns do not matter; a gap
  inside the span breaks the wall. Disconnected Blues do not form a barrier
  merely because every column has a special Blue at its front.
- With a barrier, select only Blue or colors represented beneath it. Each
  column uses its lowest barrier cell, so targets below a higher section of a
  staggered wall remain eligible. If multiple walls span the formation, the
  nearest one in each column controls eligibility. This rule does not depend
  on the temporary shield cooldown or activation state.
- Non-Blue enemies immediately above a special Blue remain blocked even when
  there is no full barrier. One exposed enemy can keep its color eligible.
  Ordinary Blues do not create barriers. True logical colors determine
  eligibility. A Yellow remains eligible during copying windup and while mimicking.
- For each eligible color, every on-screen enemy of that color adds `row + 1`
  to its weight, where the top row is zero. More enemies increase a color's
  odds, and lower rows contribute more. Eligibility and population are separate:
  a color with no eligible target has zero weight, but once it has an eligible
  target, its enemies above the barrier count too. A complete barrier doubles
  Blue's combined weight as an additional preference.
- Random selection uses the combined weights, not equal odds per color.
  Apply it on a new selection or reroll, never to a tongue in flight. Planned
  next-wave colors also use population and row weights, while preserving the
  preference for a different color during refill. Recheck the reservation
  after the fleet arrives and reroll with live weights if it is blocked.

An empty board does not invent a Blue enemy or interrupt the existing next-wave
color reservation. Diagonal connectivity is only for barrier detection; matching
and special promotion still use orthogonal neighbors. Cooldowns, scoring, and
spawn rules are unchanged.

### Score And Levels

`RunProgress` owns score, defeated count, and level. A real matching clear emits
one `EnemyGrid.MatchCleared` event with its removed count, fleet-clear flag and
the sum of the defeated enemies' death-cascade digits.
`GameSession` awards score before handling the separate fleet refill event.
Empty/repeated hits, shield absorption, and disabling enemies award nothing.

Each enemy awards `(100 + 10 * (level - 1)) * multiplier` points. Ordinary shots
use shortest connection depth: the hit enemy is 1, direct neighbors 2, and each
further branch adds one. A host-colored hit on a regular mimic enters its host
group; Yellow hits stay in the Yellow group. Rainbow hits on regular mimics enter both.
Only chains of three or more earn multipliers. Chains of one or two award base
points per enemy and use staggered unnumbered singleton pops for either shot type.
Magic shots add the preceding peak multiplier to each enemy's local connection
depth. Equally distant branches share a multiplier; the new peak carries into
the next qualifying chain. For example, 2, 1, 2, 3, 4, 5 followed by a chain
with local depths 2, 1, 2, 3 produces 7, 6, 7, 8. Small chains neither advance
nor reset the peak. Every new accepted shot resets it; rejected fire does not.
Both shot types hide the first multiplier's label while still awarding its
base points. Defeat progression still counts actual enemies.

The combo streak is separate from death digits and magic-chain peaks. Each
contact scores its entire connected chain using the current combo, then awards
one streak point per distinct true enemy color destroyed by this shot. This
updates the displayed and scoring multipliers immediately, before a magic shot
resolves its next contact, even when several contacts happen in one frame.
Starting at x1, a Red hit scores at x1 and raises the combo to x2; a Green hit
then scores at x2 and raises it to x3. Another disconnected Red group scores at
x3 but cannot award Red again in that shot. A new shot can earn Red again.

A host-colored hit on a regular mimic scores that mimic and the real host group.
A Yellow hit scores only the Yellow group. Rainbow hits on regular mimics score
both groups; rainbow hits on special mimics score only their Yellow group.
All Yellows, including special mimics, count as Yellow for destroyed-color streaks
until a regular mimic converts permanently. A host-colored shot reveals a special
Yellow and passes through it, continuing to search for a matching enemy. Revealing
alone adds no score, kills or streak point and does not preserve the combo. If the
shot returns without destroying a matching enemy, it breaks the combo even if it
revealed one or more Yellows or subsequently hit a regular shield. A real matching
hit beyond the revealed Yellow scores normally and preserves the combo.
Special-shield deflections retain their combo penalty.

Returning or canceling a successful shot adds no extra point. A shot that
defeats no enemies resets the streak when retraction begins or it is canceled.
Rejected fire commands do not reset it, and a player hit resets it immediately.
Shot feedback adds the already-awarded subtotal to the current calculation,
such as `300 + 100 x4 = +700`, without multiplying earlier hits again.

Clearing the fleet adds `10000 * level`, without a digit or combo multiplier.
Both use the level before that clear.

Each level in `Assets/Resources/LevelMap.json` supplies an ordered `fleets`
list. Clear every enemy from each fleet, including incoming lines and summons,
to move to the next entry. The final full clear advances exactly one level.
The progress bar counts completed fleets; defeat counts and score multipliers
cannot skip entries. See [Level Progression](LEVEL_PROGRESSION.md).

Procedural development fixtures without a catalog retain the legacy cumulative
threshold `10 * level * (level + 1)` and the following default sizes and unlocks.
Authored fleets use their own candidate pools and row layouts. Incoming lines
and summons stay within each fleet's resolved opening palette, and each new fleet
starts its own incoming-line sequence at zero.

| Level | Eligible Types | Refill Size |
| --- | --- | --- |
| 1 | Red, Blue | 10 |
| 2 | Red, Blue, Green | 10 |
| 3 | Red, Blue, Green | 15 |
| 4-5 | Red, Blue, Green, Yellow | 25 |
| 6 | Red, Blue, Green, Purple, Yellow | 25 |
| 7-8 | Red, Blue, Green, Purple, Yellow | 36 |
| 9+ | All six | 36 |

The scene starts empty. Ten Red/Blue enemies grow in on beat four and reach
full size on the following beat one (or override-selected types when testing). Unlocks affect subsequent
rows, refill batches, and Purple summons. Subsequent rows are five enemies wide
through level 6 and six enemies wide from level 7 onward. Existing enemies do
not change type when a level changes. Restart reloads a fresh score and
level-one state.
See `docs/LEVEL_PROGRESSION.md` for the dedicated level-by-level design reference.
The heads-up display shows score, level, progress toward the next level, and
brief score/level/unlock feedback. Game over also shows final score.

## Purple: Preserve the Original Rule

Ordinary Purple summons one random eligible type into an empty orthogonally
adjacent cell anywhere in the fleet, not necessarily beside the caller.
Selection follows the original two-stage choice: random eligible anchor, then
random empty neighboring cell. The original farthest-candidate-row exclusions
are preserved: no downward extension from that row, and no rightward extension
from its last candidate. Candidates outside the visible horizontal boundary
are also excluded to respect the rebuild's occupied-column motion.

Its rebalanced cooldown starts at 6-9 seconds and follows the shared gentle
maximum reduction below. Purple flashes brightly for the beat before its summon triggers.
The summon is registered immediately when the cooldown completes; its rift and
0.8-second phase-in are presentation only. The summoned enemy can be matched
and use its abilities during the phase-in. A full fleet produces no summon.
The original random-neighbor upper-bound bug is not reproduced.

Tier-two Purple removes the ordinary farthest-row restrictions. Its candidate
gaps can extend one row below the lowest occupied row, still beside an occupied
cell and inside the horizontal playfield. Row index 10 (the eleventh, bottom row)
is also eligible. Summons cannot extend beyond the grid. An occupied bottom row
blocks further descent; game over requires its enemy body to touch the player. Color locks,
level unlocks, overrides, cooldowns and rift presentation are unchanged.

## Yellow: Mimics, Hosts and Yellow Groups

A mimic is a Yellow currently copying a color. Its host is the real enemy whose
color it copies. The host group contains real enemies of that color connected
directly or through active mimics copying that color;
the mimic group contains connected Yellows, including mimics copying other colors.
Copying changes presentation and abilities while logical identity stays Yellow.
Only permanent conversion removes that Yellow identity.
Copying never makes Yellow unavailable. Automatic player-color refresh preserves
a held Yellow while any true Yellow remains, including the last Yellow's windup.
Normal explicit shot rerolls still use the ordinary color-selection rules.

Regular Yellow keeps its two-soundtrack-beat windup. It gains the copied color's
regular ability even when its host is special. Special Yellow copies immediately
and gains the color's special ability even when its host is regular.
Adjacent plain Yellows are excluded from both target pools. Adjacent active
mimics are valid targets: the Yellow copies their copied color and refers to the
underlying real host. Copying itself gives no score, clear bar or player damage.

A mimic supplies a promotion member to its host group when that group already
contains at least two real hosts, including hosts joined only through the mimic.
For example, `gb/by` promotes both Blues and colors three quarters of the regular
Yellow Blue, regardless of which Blue it chose. This promotes the hosts while keeping the
Yellow's tier independent. Three connected Yellows promote their own group,
including Yellows that currently display different copied colors. Removing or
detaching a mimic reevaluates surviving hosts and removes unsupported special
status, upgraded shielding and Green tempo boosts. Only mimics with a live path
to their real host through the copied color can supply promotion support.
Revealing a bridge mimic detaches unsupported copying chains before hosts are
reevaluated. Thus `yyyrr` returns to two regular Reds after revealing the third
Yellow, while `yyyrrr` retains its three special real Reds.

| Hit | Regular mimic | Special mimic |
| --- | --- | --- |
| Yellow | Destroy the mimic and its yellow group; preserve hosts | Destroy the mimic and its yellow group; preserve hosts |
| Host color | Destroy this mimic and its real host group; preserve other Yellows | Flash, reveal as Yellow and detach; tongue passes through and preserves both groups |
| Rainbow magic | Destroy both the host group and yellow group | Counts as Yellow: destroy the yellow group and preserve hosts |

A special Blue mimic is the host-color-hit exception: its shield is Yellow.
Yellow and rainbow shots bypass that shield and destroy the yellow group. Blue
and every other ordinary non-Yellow shot receive the normal special-shield shock,
stun and returning-tongue penalty. The mimic stays attached after a shield hit.
Yellow and Blue special shield cells use one shared square perimeter, removing
internal borders across color boundaries while retaining each segment's own
friendly shot color.
Other special mimics can be killed only by Yellow or rainbow shots; unrelated
ordinary colors pass through. Revealing uses the existing flash animation.

### Coverage and Permanent Conversion

A regular mimic's half facing each adjacent enemy showing the copied color is
colored accordingly. Halves are combined as a union: one side covers half,
perpendicular sides cover three quarters, and opposite horizontal or vertical
sides cover the entire sprite. All adjacent enemies showing the copied color
contribute, regardless of which host was selected or whether the real hosts
touch directly. Thus `gb/ry/gb` converts the middle Yellow completely to Blue.
Full coverage permanently converts a regular
mimic into a real enemy of that color. It then follows normal matching, promotion
and ability rules and is no longer a member of its old Yellow group. Special
mimics are fully colored immediately but never convert permanently from coverage.
Coverage is reevaluated after movement and adjacency changes. Losing a host or
its copying connection detaches surviving mimics rather than introducing extra
kills into either group. Death effects keep a stable material independent of the
mimic's temporary split-color material.

## Green: Count-Based Movement

Default average movement is `0.03 * liveRegularGreenCount * levelSpeedMultiplier`
world units per second at a constant tempo. There is no movement with zero Greens, including the Red/Blue-only
opening. Counts are read from the live grid, including Yellow conversions.
Movement is spent on every song beat. Each tick retains the
original single-beat distance, using the configured `0.03 * liveRegularGreenCount *
levelSpeedMultiplier` rate multiplied by the completed beat duration. If a long frame crosses multiple
beats, later movement ticks use any updated Green count.

Tier-two Greens never contribute to count-based fleet movement or dash. Promotion
starts their ability cooldown; promotion itself does not change tempo. When the
one-beat windup completes, each special raises the shared gameplay and music
tempo by 3%. Each special can activate only once, and its boost lasts while it
remains an active special Green. The track keeps its current playback position.

Regular movement pulses and special tempo indicators share a chamfered,
black-outlined dance-pad arrow with green gradient and white insets. Regular
arrows stay centered on their enemies and follow horizontal fleet direction.
They grow from 30% to 175% of the enemy size; opacity stays full through 110%
size, then eases smoothly to zero at 175%. Growth and fade finish together at the
start of the next soundtrack beat, including when a movement frame arrives late. An activated
special's arrow points up and stays centered on that enemy. Specials do not spin or
show fleet-wide speed wakes.

As an arrow appears, its Green body fades out over 12% of a beat. Ordinary
Greens reappear on the next offbeat (the half-beat mark); activated specials
stay cloaked with their upward arrows visible. This changes only the artwork:
cloaked enemies retain their collision bounds, matching, abilities and tempo
contributions. Pause freezes cloaking; recoloring or clearing the effect restores
normal opacity.

When special Greens are cleared, detached down arrows fall and fade over one beat.
Lost tempo boosts ease down together over one beat rather than changing speed
instantly. Pause freezes the decrease and arrows. Transforming or removing an
activated special also removes its boost with the same easing.

Regular Greens have no dash ability. `EnemyGridMovement.UseLegacyMovement` remains
an optional switch, false by default, for continuous fleet travel in mechanics fixtures.
Fleet travel uses `EnemyGridMovement.AdvanceDistance`,
including occupied-column edge detection, contact, reversal, descent and spawning.

## Shared Difficulty Scaling

Starting cooldown ranges are Red 2.25-7.5 seconds, Blue recharge 2.25-7.5,
special Green activation 6-9, Purple 6-9, and Yellow 1.5-4.5. Each enemy independently resamples its
interval after activation. Ordinary Blue starts unshielded, with its initial
cooldown counting from spawn. Only after that cooldown expires does its shield
power up for 0.35 seconds; it cannot protect or render during the spawn rift.
Yellow-to-Blue conversions also begin with a full cooldown. Upgraded Blue group
shields retain their existing arrival/promotion timing.
Its brighter near-white cyan arc expands from a point below the enemy into
its original full shape. Protection begins only when expansion is complete.
A mismatch breaks it permanently. Ordinary Blues power up only once per Blue
color lifetime; replaying spawn visuals does not restore a spent shield.

Every level above one subtracts 0.25 seconds from each range's maximum, never
reducing it below the minimum plus three seconds. Minimums stay fixed. Existing
countdowns finish normally; new rolls use the current level. This replaces the
older, stronger Purple/Yellow cooldown scaling.

Fleet base speed increases by 3% of its initial value per level, capped at twice
the initial speed. With the default average 0.03 units/second per Green, level 6 moves at 0.0345 per Green.
The legacy ordinary Green's 0.1-unit burst remains separate and does not grow with level.
`CombatBalance` centralizes these tuning values. Basic and special abilities
have separate tuning entries, selected from the enemy's actual tier whenever
a cooldown is sampled. Both entries currently retain the same values and share
the same level scaling. Existing countdowns finish normally; special abilities
replace their basic counterparts rather than running a second simultaneous cast.

## Magic Shot

Clearing the last enemy of a color grants one magic charge, capped at two.
Partial clears, repeated hits, conversion, and disabling enemies do not award
charges. A color that later returns can earn another charge when cleared again.
The powered player cycles through all six palette colors, advancing four color
steps per music beat (or ten steps per second outside gameplay or without a music component).
Score, level and combo text, the level-progress fill and miniature life bodies
follow the player's accent color; reserved return colors, celebration colors and
life-reward colors retain their existing priority. The text beneath the
level-progress bar is hidden, including the stored-charge count and score feedback. The frame keeps its white/accent color animation.
The older spatial border pulse was removed in commit `c171e7f` on October 1,
2026, before the progressive magic-text work. It used a one-beat sinusoidal
spawn pulse to swell border thickness to three times normal and compress the
world projection by up to 8.4%. The current spawn-pulse timer still runs, but
its value no longer controls border thickness or camera compression. The
white/accent color flash remains; the return-color preview holds that accent
color steady. The magic-only inner-border size pulse below is independent of
this old spawn timer and color animation.

The original pulse movement, timing and anchors remain unchanged. At pulse peaks,
magic adds size to score, level, combo and combo-break text, the pause shortcut,
world multiplier labels, enemies, the player, extra lives and the level-progress
bar. One through six lit color-clear bars add 10%, 15%, 20%, 25%, 30% and 35%, respectively. This
additional growth returns to zero between peaks and uses the lit-bar count even
after stored charges reach their cap of two. Score, level and combo text keep their existing
108% pulse, so their combined six-bar peak reaches 145.8% of the fitted base
text size. The pause shortcut receives just the additional size. Existing popup growth
and travel also remain intact. Enemies and extra lives retain their on-beat pulse;
the player retains its half-beat offset. Extra growth preserves each element's
existing center and movement. The progress bar retains its full original width;
its normal pulse, pending-level glow and extra magic growth are vertical only.

The inner border edge and four corners animate independently of the outer rim
while magic is active. The edge swells in thickness on displayed beats 1 and 3;
the corners grow around their individual centers on beats 2 and 4. The edge
has a three-times-thickness base peak, and corners keep their 9% base pulse;
both layer on the same progressive bonus. Their phase follows the metronome
and each song's downbeat offset. Return-color previews and celebration colors
do not suppress this size animation. The outermost rim stays fixed. Scenery, particles, bottom color-clear
blocks, tongue, development clock and metronome receive no additional growth.
While powered visuals are active, the player's pupils ease to 140% of their normal size over 0.2 seconds and ease back
when magic ends. Pending absorption can delay the stored-charge presentation;
an active magic shot enables it immediately. Pause freezes the presentation.
Collision bounds, camera framing and touch coordinates retain their original behavior.

The next successful firing consumes all stored charges and creates a rainbow piercing
tongue. It hits any color, bypasses shields, and clears each struck enemy's
ordinary same-color chain. An accepted magic shot fires
immediately, with no wind-up or charge animation. Repeated fire is blocked while
the shot is active, and hit/game-over cancellation stops the shot. The old
`MagicChargePresentation` placeholder remains unused for possible future reuse.
Magic is twice as wide and travels at four times the normal extension and return
speeds (56 and 80 world units/second). It continues
extending rather than retracting at first contact, unless that hit clears the
fleet, in which case it immediately begins its visible return. Hits are swept over the full travel
segment so a slow frame cannot skip intermediate enemies. Retraction does not
deal damage. A missed or subsequently canceled shot still spends all stored magic;
a rejected fire command spends nothing. Only clearing the last enemy of a color
during the magic shot earns magic for the next shot. Killing a chain while that
color survives elsewhere cannot retain magic, even if multiple charges were
stored before firing. New awards do not change the current shot.

Following the original reset rules, a player hit, fleet clear, or restart removes
stored magic. The tongue uses the ordinary length-based whistle, so its faster
travel produces a faster pitch glide rather than a separate magic sound. Genuine
color clears also retain their existing rising tone and colored absorption dust.
The original color-wheel visual direction is no longer part of
the rebuild; the bottom color-clear blocks are the retained color-progress
presentation.

Audit finding: the direct `PrepareFatalDefeat` path cancels the shot and clears
the bar streak but does not explicitly zero `MagicCharges`. Ordinary damage and
starting a new run do reset that counter. The extra text and pupil presentation
is disabled during fatal defeat; this retained counter behavior was left unchanged.

## Numbered Death Cascades

Each match records shortest connection distance from the struck enemy before
removing anything. On ordinary shots the first enemy has no numeral, its
neighbors show `2`, their next neighbors show `3`, and so on. Siblings share a
number and burst together. Mimic links are traversed only for the applicable
shot rule above, and each digit uses the dying enemy's true color. Magic shots display a continuous
per-enemy count across all groups: blank, `2`, `3`, `4`, and so on. Local branch
depth still controls each group's animation timing independently of its score
multiplier, so later hits do not acquire long delays merely from a large count.

`EnemyDeathBurst.RingDelay` is 0.065 seconds between rings; `BurstSeconds` is
0.5 seconds for the shards and digit pop/fade. The effect holds a sprite snapshot
until its ring begins. Combat occupancy, ability shutdown and scoring resolve
immediately, so visual remnants cannot shoot, be hit again or duplicate rewards.
Refills wait for the outgoing effects as well as the returning tongue. The
presentation component can be replaced without changing matching logic.

## Tier Two Groups

Enemy tier is separate from game level. As in the original promotion rule, a
same-color orthogonal group of three or more upgrades its members. All five
colors now upgrade. They retain tier two when the group splits; changing
color resets the tier until the new group qualifies. Diagonal neighbors and
Yellow tendrils do not count toward promotion.

Tier-two ships use the shared squid sprite assigned on `EnemyRowSpawner`'s
optional `specialSprite` field.
Orange always uses that special sprite, including in menu previews, while retaining
its existing single-tier abilities, cooldowns and hitbox; it does not need promotion.
Special Purple adds forward-row summons, including the bottom row. Special Yellow fully copies its host color and ability and reverts on host death, as detailed above.

An active tier-two Blue shields its whole connected Blue group with a rigid
outline along the exposed cell edges, including concave boundaries. Internal
edges and individual shields are hidden; disconnected groups have separate shields.
The shield waits for the existing arrival/power-up sequence. A non-Blue ordinary
tongue hits the perimeter and retracts without damaging the shield. The player
and tongue turn grey, movement and firing stop, and the stun ends exactly when
the tongue finishes or is canceled. Blue shots and magic bypass the shield.
Ordinary Blues retain their existing breakable full-ring shields.

All scheduled abilities use the same warning window: the soundtrack beat immediately
before activation. Active shields and passive regular Green movement have no pending
ability warning. Tier-two Reds aim during that shared one-beat warning.
During the warning, the triangle rotates toward the player's current position
at 540 degrees per second. At launch, it aligns exactly with the shot direction,
fires from its forward tip, then smoothly returns to its resting downward pose.
Only the body artwork rotates; grid placement and collision bounds do not.
Pause freezes the turn, and color changes or disabling the enemy clear the pose.

The aimed shot keeps its launch direction for its entire flight: no tracking,
turning or loops. It keeps the former special missile's 3.5-unit speed and opaque
1.5-second white pulse, and retires at the playfield edges or after five seconds.
Swept collision handles slow frames. If no living player is available or the
replacement is still falling, both the warning pose and launch direction point
downward. Aimed fire resumes on landing, even during flashing protection.
Ordinary Reds retain their steady-colored,
downward 5-unit-per-second missiles. The legacy homing mode remains available in
code but is no longer used by special Reds.

## Color-Clear Cycle

A genuine last-of-color clear locks that color out of new rows and Purple
summons while its color-clear bar remains powered. Level increases and override edits do
not erase locks. Conversion and disabling enemies do not count as clears.
Locks remain active during the refill pause; a successful new batch resets
them after the entire fleet has been cleared. Restart also starts fresh.

The colored segments at the bottom of the playfield record a color-clear streak.
Each shot must destroy at least one entire color to retain the existing segments
and add the newly cleared colors. A miss, partial-group clear or shield-only hit
clears all segments when the tongue finishes returning. Canceling an unsuccessful
shot also ends the streak. Ordinary and magic shots follow the same rule; a magic
shot can clear multiple colors, and one successful clear is enough. Breaking the
streak releases the spawn locks for the disappearing bars, making those colors
eligible for new rows and Purple summons again, subject to level and override
settings. Seen-color history, score and magic rewards are not reset.
The lost bars play the same simultaneous rise, fade and power-down sound as a
partial fleet clear, over one beat at the current tempo. Their spawn locks release
immediately; gameplay continues, pause freezes the fade, and a newly earned bar
replaces its old power-down immediately.
The next genuine color clear starts a fresh streak without restoring old bars.
Uncleared colors draw nothing; clearing a color fills its slot. Slots represent
distinct colors actually encountered in the current fleet, including cleared
colors. Each slot is the full progress-track width divided by that seen count, with a two-pixel
visual gutter. Earning a segment does not change the slots or their denominator.
Spawning or transforming into a newly encountered color adds a slot. Unlocks
and override selections alone do not. Seen history resets with a new batch. The earned segments
remain through the refill pause and reset when the new batch spawns.

If an override selects only locked colors while older enemies remain alive,
the fleet continues turning and descending but adds no new row; Purple summons
also do nothing. This prevents the development control from reviving a cleared
color or stalling descent. Unconfigured prefabs remain excluded from spawning.

## Wave Transition

The opening uses the same planned-batch mechanism as a refill, with two rows
instead of the later level-scaled refill size. Player movement is available
during the empty opening, and its selected color belongs to the planned fleet.
Restart repeats this empty opening rather than loading pre-placed enemies.
With music playing, opening fleets build on beat four and land at full size on
the next beat one, using the same sharp arrival as refills.

Opening fleets use a one-beat grow-in; full-set refills use the latter half of
beat four. Ordinary new rows grow in over
0.8 seconds; Purple summons use the original rift geometry and 0.8-second fade.
Ordinary and special Purple summons always use a purple rift, regardless of the new
enemy's color; its body, logical color and abilities remain unchanged.
The sprite begins fully transparent immediately,
preventing a full-opacity flash before animation. Blue's shield stays hidden
during the rift, then runs its separate 0.35-second power-up.
Effects are cosmetic: grid registration, matching, movement, and ability timers
remain live during phase-in. The centralized spawn path triggers the effect for
opening fleets, ordinary rows, refills, and summons without duplicate triggers.

The last hit no longer cancels the tongue. It retracts visibly while the fleet
is paused. Partial clears spike every earned bar at once and power them down
together across the remaining time until the fleet begins appearing, fading
their glow back to the default unpowered bar color without further beat pulsing. Complete
sets wait until a whole beat leaves room for one bar pulse per beat leading
into the downbeat. With two filled colors, the bars animate on counts three and
four. While waiting, the bars continue their normal music-reactive motion. The
last bar animates in the first half of four; the fleet grows during the latter
half and finishes on the following beat one. Growth accelerates into a sharp full-size landing on one, without an early
overshoot or a settling tail. There is no gap between the countdown and the grow-in, including when a life
is awarded. The arrival follows the soundtrack clock across tempo changes and
long frames. The sequence chooses randomly
between a left-to-right overlapping wave and individually rising bars in shuffled
order; their display slots stay fixed. The field stays empty through the bar
countdown, and the incoming slime always bounces off the
actual final bar while it is rising, whichever order was chosen. With no active
music, full sets use one beat per bar at the current tempo, while partial bars
power down together over one beat. Pause freezes the countdown and arrival. Player movement remains active, but firing
into an empty field is blocked.

Newly unlocked sound-bar slots stay hidden until the activated bars finish their
clear celebration. The existing slots retain their old widths throughout that
animation. Over the following beat, the new slots grow from zero width while the
existing slots narrow into the new equal-width layout. This presentation-only
transition pauses with gameplay and does not delay enemy spawning.

At fleet clear, the spawner prepares the exact next batch using the new level
and current override. The player keeps the outgoing shot color until the tongue
returns, then chooses a color from that prepared batch, preferring a different
color when available. No neutral grey state is introduced. The same batch is
instantiated after the pause, guaranteeing that the selected color appears.
Override edits during the pause apply to later spawning, not the reserved batch.

## Replaceable Presentation

Five enemy prefabs hold their sprites and base dimensions. `EnemyAbilities`
owns gameplay timers; `EnemyPresentation` owns rifts, speed wakes, tendrils,
flashes, and imitation overlays. Its `PhaseIn`, `SpeedShift`, `BeginImitation`,
and `Tick` entry points are the replacement boundary for hand-drawn animation.
Animation callbacks never award score, spawn enemies, or commit imitation.

The current effects are procedural placeholders, not final illustration or
frame-by-frame animation. Full firing/hit/defeat animation coverage remains a
separate checklist item. Authored row patterns and field-aware fairness remain
pending. Generated rows now choose up to three distinct eligible colors without
replacement and allocate adjacent runs, never repeating a color in separate
parts of a new row. Run sizes and color order remain randomized. This applies
to openings, new rows and refills, not individual Purple summons or Yellow
transformations of existing enemies.

## Verification

`ProgressionChecks` extends the full editor suite with thresholds, scoring,
unlock-gated rows and summons, independent Green steps and edge contact,
timed Yellow attachment, host-dependent deaths, special reversion and ability adoption, fleet-wide Purple
selection, and full-grid safety. The Play mode suite checks real-hit scoring,
restart reset, and crossing an unlock threshold through a real clear.

The editor-only `RuntimeGameplayChecks.Preview` builds a temporary cue showcase
and captures `TestResults/ability-feedback.png` in a visible Unity session.
It never saves the showcase over the gameplay scene.

Disconnected enemies retreat one grid space per soundtrack beat. Retreat and Yellow
transformation follow soundtrack position across pauses and tempo changes, with a
default-tempo beat clock when no soundtrack is available.
