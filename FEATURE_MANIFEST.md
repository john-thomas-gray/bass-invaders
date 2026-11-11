# Candy Cruisers Feature Manifest

This document records what is present in the original project and what has been
discussed for the rebuild. It is an inventory for choosing implementation
order, not a claim that every original feature is complete or balanced.

## Rebuild Progress

- [x] Add a gameplay edit of Contact trimmed at the analyzed transition beats near 0:55 and 4:48 (55.380-288.206 seconds), with beat-map, downbeat, key and loudness metadata.

- [x] Match the title-screen slime body to the subtitle's current palette color while keeping it saturated through the white pulse and switching directly to the next color each beat. Verified with menu color tests.

- [x] Add a small rounded lizard-like bulb beyond the player's tongue endpoint. It follows ordinary, return, magic and shock presentation without changing collision geometry. Verified with focused presentation tests.

- [x] Improve subtitle legibility against its emitted light with a thin white outline and softer local glow, preserving solid letter colors and existing backlight behavior. Verified with menu tests and a rendered portrait preview.

- [x] Title backlighting passes through enclosed letter openings even above solid lower strokes. Outer light boundaries rise 15 degrees from the subtitle's bottom ink corners. Verified with rendered pixel tests and portrait/landscape menu previews.

- [x] The solid-colored, glowing subtitle emits one continuous field of beat-pulsing light. The opaque white main title and unchanged slime artwork block it, casting actual glyph and sprite shadows; light escapes through gaps instead of passing through the artwork. Verified with shadow-render pixel tests, menu regression checks and portrait/landscape previews.

- [x] Keep soot and falling cinders below the eyes and pupils throughout recovery, including blinks; restore original face draw order afterward and exclude soot from extra-life copies.

- [x] Verify the special Blue electrical shock's one-beat travel duration, silent fallback and unchanged archived tongue-return speeds.

- [x] Special Blue shield shocks leave a gray trail along the tongue, flash on player impact, and leave a black body with tiny pupils and smoke. On tongue return, the player blinks and soot falls off as cinders revealing the next color; stun and return mechanics are unchanged.

- [x] Full-set bars reuse the partial-set color/light drain during each bar's falling phase, preserving sequential spikes, removal timing, sounds and rewards.

- [x] Remove wave-spawn inward border compression and rim thickening; keep camera framing, border geometry and existing color flashes unchanged.

- [x] Replacement jumps ease inward toward the playfield center during ascent, by up to 80% of a body width, while preserving their landing destination.

- [x] Replacement lives visibly crouch to half height in their slot for a 0.2-second anticipation phase before springing into their jump; flips wait until takeoff.

- [x] Player landings use a shared grounded squash and rebound after replacement jumps, reward-bar flips and title-screen arrival, without changing movement or collision bounds.

- [x] Replacement lives jump in a continuous sideways arc from their life slot, with subtle takeoff squash, airborne stretch and landing compression; apex control and collision bounds remain unchanged.

- [x] Reward-bar flips trigger at 10% body overlap; side-lift determines rotation up to 50%, and movement determines rotation above 50%.

- [x] Spare slimes copy the player's actual sprite pieces and proportions while keeping independent synchronized movement.
- [x] Replacement slimes sometimes flip on takeoff; control resumes at the apex with untimed airborne invincibility, and flashing protection starts only on landing.
- [x] Earned slimes emerge clipped behind the nearby border, step onto the final bar, flip upward and arc above their life slot before landing.

- [x] Extra lives use independent synchronized slime artwork: normal pulses and occasional left-right routines change only on four-beat measure boundaries; full-life full-set rewards trigger one group flip.
- [x] Earned-life slimes enter from offscreen and trampoline from the final reward-bar spike into the life row; consumed spares jump down from the life row on death instead of slashing or splitting.

- [x] Full reward-bar celebration spikes left to right like a wave; a spike beneath the player launches a small visual-only trampoline flip. Partial-set power-down and refill timing remain unchanged.

This copy is the active rebuild checklist. Check items off as they are implemented
and verified. The original-game inventory below describes the old game, not the
completion status of the rebuild.

### Completed

- [x] Play the replaceable player death burst for missile hits and the last-Yellow
  transformation penalty, retaining normal respawn/invulnerability timing.
- [x] Restore the archived special-Blue tongue return speed:
  `20 * (0.25 - 0.01 * level)` units per second, clamped to 0.2 at high levels
  to prevent the original level-25 stall/reversal. Preserve ordinary returns.

- [x] Limit normal spawning to one Orange on screen. Every eligible new wave
  guarantees one; subsequent row/summon slots use a 1-in-75 chance only while
  no Orange exists. Preserve level/override gates and color-clear locks.

- [x] On fatal fleet contact, freeze gameplay and play a replaceable player death
  cue before showing game over. Placeholder: a white flash and colored fragment
  burst lasting 0.9 seconds. Keep the player hidden until restart; record the
  final score only after the cue finishes. Missile-hit recovery remains unchanged.

- [x] Unlock Orange at level 9 and include it in developer spawn overrides,
  player colors, magic-ready flashing, color-clear rewards and modular visuals.
- [x] Keep Oranges isolated on spawn and after swaps; never promote them.
  Orange-only overrides leave gaps instead of violating isolation.
- [x] Orange swaps with a same-color singleton matching one of its neighbors,
  so that the singleton joins the neighbor. Exclude linked/connected targets,
  impossible destinations and swaps that would place Oranges together.
- [x] If retreat brings Oranges together, explode the touching Orange group for
  five times base points per Orange, with defeat and color-clear credit, but no
  shot-combo multiplier. Resolve immediately before promotion or later retreat.
- [x] Ordinary Yellow cannot transform into Orange; special Yellow can mimic
  Orange visually while remaining logically Yellow.

- [x] Create the Unity 6000.3.24f1 rebuild project.
- [x] Create the standalone gameplay scene and make it the build entry scene.
- [x] Display the original star background (static for this first slice).
- [x] Spawn ten opening enemies in two rows of five after an empty opening pause.
- [x] Move the formation horizontally at adjustable speed; turn and descend
  only when the leading occupied column reaches a playfield edge. Empty edge
  columns extend travel. This replaces the original fixed sweep limits.
- [x] Reindex columns when needed before a new row spawns, preserving survivors'
  horizontal positions, identities, and connectivity while fitting the full row
  inside the playfield.
- [x] Store Red/Blue enemy visuals in replaceable sprite prefabs.
- [x] Keep the formation visible in portrait, tall-phone, and landscape views.
- [x] Add a subtle playfield border aligned with the player's horizontal wrap
  boundaries and visible in portrait and landscape views.
- [x] Keep player movement, wrapping, and ongoing pointer movement active
  between cleared fleets; only game over locks player controls.
- [x] Run the full initial gameplay check suite: movement bounds, frame-rate
  independence, scene references, framing, and rendered previews.

The opening formation uses Red and Blue unless a development override is active. Level-based spawning now gates
all rows, batches, and Purple summons. Five sprite prefabs and a separate
ability-presentation component support replaceable art; full animation coverage
is still pending. The scene now uses a fifty-five-cell
occupancy model, player controls, and a single active extend/retract tongue.
Matching shots now clear orthogonally connected same-color enemies and retract
at the first matching hit. Mismatched enemies allow the tongue to pass through.
Cleared cells, scene objects, and available-color counts update together.

- [x] Disconnected groups back up one row every 0.15 seconds until reconnected
  to the top-anchored fleet. Connections use horizontal/vertical neighbors of
  any color. Groups retain their shape, identities, colors, and enemy counts.
  If no top-row enemies remain, groups rise until they reach the top row.
  This replaces the original game's removal of disconnected groups.
- [x] Verify reconnection in the full editor suite and in Play mode after
  clearing a connecting enemy.

### Recommended Next Steps

This order is a recommendation, pending the user's implementation choices.

1. [x] Implement the five-column, eleven-row logical grid with reliable cell
   occupancy, enemy identity, color counts, and neighbor queries.
2. [x] Add player movement and edge wrapping, with keyboard and touch controls.
   - [x] Mobile taps near or directly beneath the character shoot without repositioning; distant taps move to their target after release, and dragging repositions without firing. Keep desktop controls unchanged.
3. [x] Add the extend/retract tongue, one active shot at a time, and shot colors
   selected from enemies currently present.
4. [x] Implement matching hits and orthogonally connected same-color chains,
   including reliable removal from the grid and color counts.
5. [x] Establish separate visual children and animation event hooks; add
   replaceable placeholder cues for firing, matching, and enemy defeat.
6. [x] Implement ordinary Red missiles and Blue shields, with warning cues,
   player hits, respawning, and temporary invulnerability.
7. [x] Complete the basic wave loop: descent, new rows, game over, fleet refill,
   position reset, and restart.
8. [x] Add score and level progression with visible feedback, plus the exact
   unlock schedule: Red/Blue at level 1, Green at 2, Yellow at 4, Purple at 6.
   Implement the ordinary abilities of newly unlocked types with warning cues.
9. [ ] Author reusable five-enemy line configurations with level eligibility
   and a simple way to preview each formation.
10. [ ] Add field-aware spawn selection using live enemy counts and active
    threats, then playtest and tune limits on overwhelming combinations.

### Step 7 Progress

Prioritized ahead of steps 5 and 6 at the user's request.

- [x] Shift surviving enemies down one cell at each occupied-column edge contact.
- [x] Add five randomly selected Red/Blue enemies in the new top row.
- [x] Preserve enemy identities, gaps, colors, and logical/visual alignment.
- [x] Block descent and spawning when the bottom row is occupied, without
  overwriting enemies; continue horizontal movement until enemy-player contact.
- [x] Verify both turnarounds, long frames, full-grid blocking, and spawning
  with the complete editor and Play mode check suites.
- [x] Turn bottom-row enemy-body contact with the player into game over; stop movement,
  retreat, firing, enemy abilities, and missiles, and show a Restart button.
- [x] Refill cleared fleets after the earned bars run out at one musical beat
  per bar; reset fleet position and sweep timing while fired missiles persist.
- [x] Add restart through the game-over button or R key. Reload the scene to
  restore the opening 10 enemies, player state, abilities, and timers.
- [x] Verify the complete refill, game-over, and restart sequence in Play mode.

Step 8 now scales refills to 15/20/25/30 enemies by level and gates their colors.
Authored lines and field-aware fairness remain pending.

### Step 8 Progress

- [x] Award points once per defeated enemy, plus the level-scaled fleet bonus.
- [x] Advance through cumulative defeat thresholds, including multiple levels
  in one clear; show score, level, progress, score feedback, and unlock notices.
- [x] Gate new rows, refill batches, and Purple summons: Red/Blue at 1, Green
  at 2, Yellow at 4, Purple at 6. Restart resets score and level.
- [x] Give each Green its own warning flash and physical fleet step with green
  speed wakes; bursts obey occupied-column contacts, descent, and game over.
- [x] Rebalance Green: shorten each dash to 0.1 world units (one third of its
  initial length) and independently resample 8-20-second cooldowns at spawn
  and each activation for fewer, more widely staggered bursts.
- [x] Preserve ordinary Purple's fleet-wide gap spawning, with a brilliant
  flash and electric phase-in. Cooldowns are rebalanced below.
- [x] Preserve ordinary Yellow's random neighboring-type adoption.
  Add a growing tendril and gradual visual morph
  without delaying the gameplay conversion or making it reversible.
- [x] Keep presentation separate from gameplay timers and state changes so
  hand-drawn effects can replace the placeholders.
- [x] Verify the full editor and Play mode suites, including scoring, unlocks,
  Green edge contacts, Yellow permanence, and Purple capacity/eligibility.

See `docs/PROGRESSION_AND_ABILITIES.md` for exact rules, visual timing, tests,
and the presentation replacement boundary. Later milestones added combo streaks,
magic-shot multipliers and special enemy variants.

### Development Controls

- [x] Add an editor-only spawn override window with individual enemy-type
  toggles. Override level eligibility for future rows, refill batches, and
  Purple summons; require at least one type. Preserve selections through Play
  mode and scene restarts, without changing existing enemies or shipping the
  control in player builds.

### Combat Balance and Magic

- [x] Keep ordinary Blue shields as full rings encircling the enemy, retaining
  ordinary shield blocking and matching-color bypass.
- [x] Spread out abilities with independent cooldown ranges: Red 6-18 seconds,
  Blue recharge 35-75, Green 8-20, Purple 18-50, Yellow 18-40.
- [x] Increase base fleet speed by 3% of its starting speed per level, capped
  at twice that speed. Reduce maximum cooldowns by 0.25 seconds per level,
  preserving unchanged minimums and at least four seconds of variation.
- [x] Grant a magic charge when the last enemy of a color is cleared; cap at
  two, show readiness, and make the next shot pierce all colors and shields.
  Preserve same-color chain clearing; reset magic on hit, fleet clear, and restart.
- [x] Verify full editor and Play mode suites plus rendered shield/magic previews.

### Color-Cycle Feedback

- [x] Match the level-progress fill to the player's displayed color; flash the
  player through all five colors while magic is ready or a magic shot is active.
- [x] Exclude cleared colors from rows and Purple summons until the entire
  fleet is cleared and a new batch begins. Preserve locks through level changes,
  refill delay, and override edits; reset them for a new batch or restart.
- [x] Show earned colored segments at the bottom of the playfield, each using 1/N of the
  full track where N is the distinct color count actually seen in the current fleet,
  including cleared colors. Unlocks and override selections alone do not add slots.
  Draw a segment only after its color is destroyed, without changing the denominator.
- [x] Make magic twice as wide and twice as fast in both directions.
- [x] Retract the tongue visibly after the last enemy dies; wait for its return
  before refilling. Switch straight to a color guaranteed in the planned batch,
  preferring a different color when available, with no grey state.
- [x] Verify editor and Play mode regressions and a rendered feedback preview.

### Spawn Presentation

- [x] Start and restart with an empty field, then spawn the planned two-row
  opening fleet after a one-second pause; keep player movement available.
- [x] Use the shared electric-rift phase-in for opening fleets, new rows,
  refill batches, and Purple summons, tinted to the arriving enemy's color.
- [x] Begin sprites fully transparent and fade Blue shields with their owners.
- [x] Verify full editor and Play mode suites plus empty-opening and spawn-effect renders.

### Five-Column Fleet And Red Timing

- [x] Reduce Red's level-one cooldown to 6-18 seconds (12 seconds on average,
  down from 16), retaining independent random timing and level scaling.
- [x] Use five centered columns with unchanged cell spacing and playfield width:
  ten opening enemies, five enemies per new row, and fifty-five-cell capacity.
- [x] Update refill sizes, sparse-column boundary checks, summon fixtures, and
  documentation; verify full editor and Play mode suites and rendered layouts.

### V2.0.0 Row Composition And Migration

- [x] Cap each newly generated row at three distinct colors, with each color
  occupying a single contiguous run. Apply to openings, refills, and new top rows.
- [x] Preserve unlocks, development overrides, and cleared-color exclusions.
  Individual Purple summons and Yellow transformations retain existing rules.
- [x] Verify full editor and Play mode suites for the grouped formations.
- [x] Move the Unity project to `/Users/johngray/swe/candy-cruisers`, preserve
  the old checkout and history, and publish the V2.0.0 source release.

### Current Combat And Readability Tuning

These settings supersede earlier timing and Green-dash milestones above.

- [x] After Blue's 0.8-second arrival, power its shield up from a point in front
  of the body over 0.35 seconds. Use a bright near-white cyan arc, preserve its
  final shape, and enable protection only once expansion finishes. Recharge
  repeats the power-up animation.
- [x] Increase the level-progress bar height from three to nine pixels.
- [x] Reduce all ability cooldown ranges by 25%: Red 4.5-13.5 seconds,
  Blue 26.25-56.25, optional Green dash 6-15, Purple 13.5-37.5, Yellow 13.5-30.
  Keep level-based maximum reduction with a three-second minimum range.
- [x] Make normal movement proportional to live Green count (0.3 world units
  per second per Green before level scaling); zero Greens means no horizontal
  movement. Retain the previous dash mode behind `UseGreenDashes`, off by default.
- [x] Add a 0.2-second magic-shot wind-up; double previous magic extension and
  return speeds to 56/80 world units per second. Keep movement available while
  charging and cancel pending shots on hit/game over or an empty fleet.
- [x] Double magic-ready color flashing to one color change every 0.1 seconds.
- [x] Pass full editor and Play mode suites, including retained dash coverage,
  Green-count boundary timing, shield sequencing, and magic cancellation checks.

### Longer Magic Charge And Slower Greens

These values supersede the previous 0.2-second wind-up and Green contribution.

- [x] Increase magic-shot wind-up to 0.75 seconds without changing shot speed.
- [x] Add a replaceable charge placeholder: a pulsing color-cycling ring with
  converging sparks at the mouth, following player movement and hiding on launch
  or cancellation. Normal shots do not use the effect.
- [x] Reduce each Green's passive speed contribution tenfold to 0.03 world units
  per second before level scaling; preserve zero-Green stopping and legacy dashes.
- [x] Pass full editor and Play mode suites and verify portrait/landscape renders.

### Immediate Magic And Arcade Frame

These changes supersede the magic wind-up and charge-animation milestones above.

- [x] Fire magic shots immediately, retaining their width, piercing behavior,
  and 56/80 world-unit extension/return speeds. Stop using the charge placeholder.
- [x] Strengthen the playfield frame with a brighter inner boundary, chamfered
  outer outline and minimalist corner brackets, without changing playable bounds.
- [x] Tint every frame element to the player's displayed color, including magic
  flashing, with all lines visible in portrait and landscape layouts.
- [x] Document `CombatBalance.CooldownRange` as the cooldown tuning point and
  preserve the user's current edits. Check level-scaling invariants without
  requiring the previous exact cooldown values.
- [x] Verify full editor and Play mode suites, frame color checks and renders.

### Yellow Transformation Combat

These rules supersede the earlier immediate-conversion Yellow implementation.

- [x] Keep Yellow logically Yellow during its two-second transformation, linked
  to the non-Yellow neighbor it is copying. Adopt the new type only on completion.
- [x] Clear both sides of an active attachment: a matching hit on Yellow clears
  the target color group, and clearing that group also kills attached Yellows.
  Resolve multiple attachments without duplicate enemy scores or clear events.
- [x] Award the Yellow bar and normal magic reward when a hit kills the last
  Yellow, including linked group clears and after previous Yellows transformed.
- [x] Never award the Yellow bar, score, magic, or spawn exclusion when the last
  Yellow disappears by transformation. Yellow can spawn again afterward.
- [x] Hit a player whose selected color is Yellow when the last Yellow finishes
  transforming, using normal damage/invulnerability rules and canceling their shot.
- [x] Verify full editor and Play mode suites, including both hit directions,
  multiple attachments, suspended transformations, completion penalties and respawns.

### Player Color Selection During Transformation

- [x] Exclude Yellow from new player-color selections while all remaining
  Yellows are transforming, without changing an already-Yellow player.
- [x] Restore Yellow eligibility when an untransformed Yellow is present,
  including new spawns or canceled transformations.
- [x] Verify full editor and Play mode suites, including selection regressions
  and existing earned-bar persistence after misses and partial clears.

### Color-Clear Streak Bars

These rules supersede the earlier indicator persistence after unsuccessful shots.

- [x] Keep and extend the bars only when each successive shot clears an entire
  color. Remove all bars on return or cancellation of an unsuccessful shot.
- [x] Apply the same rule to ordinary and magic shots, including partial clears.
- [x] Keep seen-color sizing, score and magic rewards independent from the
  displayed streak. Later color clears start a fresh displayed streak.
- [x] Release each color's spawn lock when its bar disappears. Allow that color
  in future rows and Purple summons under normal level/override restrictions;
  clearing it again earns its bar and reward and locks its spawns again.
- [x] Pass the full editor and Play mode suites, including successful streaks,
  ordinary/magic misses, partial clears, cancellation and bar-controlled spawn locks.

### Blue Shield Width

- [x] Replace the scale reduction with shorter arc ends. Preserve the original
  radius, thickness and spacing; make the outer endpoint chord one enemy diameter.
- [x] Preserve the bright arc and power-up animation. Pass editor gameplay checks,
  including pixel-level endpoint span, unchanged radii, power-up and recharge.

### Numbered Death Cascades And Tier Two

- [x] Burst enemies into digits of their own color, numbered by shortest
  connection distance: impact is 1, direct neighbors are 2, then 3 and onward.
  Sibling branches share a digit and timing; Yellow tendrils count as links.
- [x] Cascade visual deaths by 0.065 seconds per ring with shards and a digit
  pop/fade. Keep dying placeholders out of combat and wait for them before refill.
- [x] Promote connected Red/Blue groups of three or more to a separate tier two,
  retaining that tier until color change. Use downward equilateral triangles with
  a replaceable sprite slot; leave special Green/Purple/Yellow for later.
- [x] Give upgraded Blues a rigid perimeter around their connected Blue group,
  following concave edges without internal dividers. A mismatched ordinary shot
  turns the player grey and stuns movement/firing until the tongue returns.
  Preserve Blue/magic bypass and ordinary Blue arc shields.
- [x] Give upgraded Reds bounded-turn homing missiles with existing warnings and
  cooldowns, swept collision, suspension and finite lifetime. Keep ordinary Reds straight-firing.
- [x] Pass full editor and Play mode suites, including branch depths, stun
  recovery, shield bypass, promotion, homing timing and collision. Verify renders
  at portrait, landscape and tall-phone sizes.

### Original-Style Homing

Historical implementation, superseded for special Reds by aimed shots below.

- [x] Restore original horizontal pursuit at 3.5 units per second, with gentle
  18-degree-per-second steering capped at 35 degrees either side of downward.
- [x] Retire homing missiles at playfield edges or five seconds; never allow
  upward loops or re-entry. Preserve ordinary missiles and swept collision.
- [x] Pass full editor and Play mode suites, including speed, angular limits,
  target reversal, offscreen retirement and frame-step consistency.

### Homing Flash And Wider Steering

Historical steering implementation; aimed shots retain the slow flash.

- [x] Slowly flash homing missiles toward white on a 1.5-second cycle without
  reducing opacity. Pause the flash with suspension; keep ordinary missiles steady.
- [x] Widen the homing cap to plus/minus 50 degrees while preserving gentle
  steering, downward travel and permanent offscreen removal.
- [x] Pass full editor and Play mode suites, including pulse timing and turn limits.

### Aimed Special Red Shots

- [x] Replace special Red homing attacks with straight shots aimed at the player's
  position at launch. Preserve projectile speed, slow flash and offscreen removal.
- [x] Turn the triangle toward the player during the firing warning, then return
  it to its resting pose after firing. Rotate artwork only; preserve hit bounds.
- [x] Verify pause, color-change cleanup, fixed flight after player movement,
  ordinary Red behavior and missile survival after shooter death.

### Special Purple Summon Tint

- [x] Use purple spawn-in rifts for every enemy summoned by a special Purple,
  regardless of its color. Preserve the arriving enemy's identity and abilities,
  and keep normal batch, row and ordinary Purple summons color-specific.

### All-Color Grouping And Special Green

- [x] Extend connected-group tier-two promotion and triangle placeholders to
  Green, Purple and Yellow. Preserve active Yellow transformation visuals.
- [x] Restore the retained fast-forward dash for upgraded Greens only in normal
  play: 0.1 units over 0.18 seconds, with a 0.6-second warning and speed wake.
- [x] Keep count-based speed for all Greens and sample fresh varied cooldowns
  on promotion, avoiding synchronized overdue casts. Keep the legacy switch.
- [x] Pass full editor and Play mode suites, including all-color grouping,
  ordinary-versus-special Green behavior, promotion timers, suspension and conversion.

### Ordinary Blue Initial Cooldown

- [x] Start ordinary Blues without shields. Count their initial cooldown from
  spawn, then play the existing 0.35-second shield power-up before protection.
- [x] Apply the same cooldown to Yellow-to-ordinary-Blue conversions. Preserve
  upgraded Blue group shields and ordinary shield recharge behavior.
- [x] Pass full editor and Play mode suites, including initial delay, suspension,
  long-frame timing, recharge, transformation and promotion.

### Special Purple And Yellow

- [x] Allow upgraded Purple to summon into the row immediately below the lowest
  occupied row, including the bottom game-ending row. Preserve adjacent-gap
  selection, horizontal bounds, color locks, level gates and overrides.
- [x] Give upgraded Yellow a visual-only imitation with no tendril or copied-enemy
  combat link. Copy appearance while retaining true Yellow counts, matching,
  group membership, promotion eligibility and clear rewards.
- [x] Reveal a disguised Yellow on any non-Yellow ordinary hit: flash, restore its
  Yellow triangle, let the shot pass through without a kill, and restart its cooldown.
  Yellow and magic shots still kill the actual Yellow group.
- [x] Continue the tongue's collision sweep after revealing disguises, including
  multiple disguised Yellows and a real target behind them within one frame.
- [x] Keep disguises independent of their source's survival. Detach ordinary
  transformation links if the Yellow promotes mid-transition. Preserve regular
  Yellow conversion behavior and do not let disguises promote copied-color groups.
- [x] Pass full editor and Play mode suites and inspect portrait, landscape and
  phone renders of the disguise, forward-row rift and reveal.

### Death-Digit Scoring

- [x] Multiply each defeated enemy's base points by its displayed cascade digit,
  using the same connection-depth calculation as the death animation.
- [x] Keep actual defeat counts, level thresholds and fleet-clear bonuses unchanged.
- [x] Cover branching totals, pre-clear level pricing and real-shot score integration.

### Replaceable Combat Presentation

- [x] Move enemy bodies and player artwork under visual-only children with
  explicit body references and stable, animation-independent hit bounds.
- [x] Expose firing, matching and defeat hooks plus Inspector cue-prefab slots.
  Keep default firing pulses, match rings and cascading death digits replaceable.
- [x] Support optional Animator triggers and presentation-only clip completion
  events, with delayed starts and fallback cleanup for custom cue prefabs.
- [x] Document the no-code art replacement contract in `docs/ART_REPLACEMENT.md`.
- [x] Verify replacement prefabs, branch delays, exactly-once hooks and unchanged
  combat through the full editor and Play mode suites.

### Quiet Player Color Assistance

- [x] Force the next player color to Blue when the nearest enemy in every
  occupied column is a special Blue, including uneven fronts and sparse fleets.
- [x] Exclude a color when all of its enemies occupy the immediately adjacent
  spaces above special Blues. One exposed enemy keeps the color eligible.
- [x] Preserve current ready colors, shots in flight, transforming-Yellow rules,
  logical disguise colors and next-wave reservations; recheck arriving fleets.
- [x] Verify selection, rerolls, exposed alternatives, gaps and refill color
  handling through editor and Play mode regression checks.

### Contact-Based Game Over

- [x] Require a final-row enemy's body to touch the live player before game over.
  Extend the grid to eleven rows so the final row reaches player height while
  keeping the original ten rows in place.
- [x] Stop further descent at occupied capacity but continue horizontal sweeping.
  Check contact after descent and Purple summons as well as during movement.
- [x] Stop fast player and fleet movement at first contact; treat screen wrapping
  as two edge segments, never as travel through the middle of the field.
- [x] Verify capacity, overlap, swept contact, wrapping, pause and recovery in the
  full editor and Play mode suites. Shields and death effects are not bodies.

### Independent Fired Missiles

- [x] Preserve straight and homing missiles when their Red shooter dies,
  including the final enemy of a fleet. Keep travel and player collision active
  during refill. Pause/game over still suspend missiles; scene restart or return
  to the menu clears them. Normal impact, offscreen and lifetime cleanup remain.
- [x] Cover shooter death, fleet clear, refill travel and hits, and pause/resume
  for both missile types in the editor and Play mode suites.

### Single-Use Blue Shields

- [x] Ordinary Blue shields power up once after their initial cooldown and stay
  broken permanently after absorbing a mismatch. Special group shields are unchanged.

### Compounding Magic-Shot Multipliers

- [x] Magic shots combine local connection depth with the preceding peak
  multiplier across chains of three or more. Equal-depth branches share numbers:
  2, 1, 2, 3, 4, 5 followed by local depths 2, 1, 2, 3 becomes 7, 6, 7, 8.
- [x] Chains of one or two use staggered unnumbered singleton pops and base points only.
  They neither advance nor reset the magic multiplier.
- [x] Hide the first enemy's numeral for all shots while retaining its x1 score.
  Preserve ordinary branch-depth multipliers and local animation timing.
- [x] Reset the magic counter on each new accepted shot, not rejected fire.
  Verify exact labels, score totals, frame steps, cancellation and fleet bonuses.

### Combo, Backgrounds And Leaderboard

- [x] Add a combo streak multiplier that snapshots when a shot is accepted,
  multiplies defeated-enemy points for that shot, advances after any enemy hit,
  resets after a miss or player hit, and ignores rejected fire commands.
- [x] Animate the star background with subtle drift, level-based tint changes
  and a brief transition flash when progression reaches a new level.
- [x] Add a local five-entry leaderboard shown on the main menu and game-over
  screen, recording positive final scores once per run.
- [x] Remove the color-wheel visual direction from the rebuild design. The
  bottom color-clear blocks are the retained visual language for color progress.

### Later Features Still Pending

- [x] Main menu and pause/resume, with Play, Resume, Restart and Main Menu
  navigation; preserve game over and final score. Pause with P, Escape, the visible
  pause control, focus loss or application backgrounding. Freeze timers, effects,
  in-flight tongues and refill delays without losing run state. Support mouse,
  keyboard and touch, and prevent menu input from firing or steering the player.
  Full editor and runtime coverage includes starting, pausing, resuming,
  returning to the menu and restarting from pause. See `docs/MENUS_AND_PAUSE.md`.
- [ ] Full animation coverage beyond the implemented combat and ability cues.
- [ ] App Store and web release preparation; Steam afterward.

Items above summarize milestones. The inventory below remains the detailed
reference for individual rules and original limitations.

## Status Key

- **Original: implemented** — behavior is present in the original Unity project.
- **Original: partial** — assets or code exist, but the feature is incomplete,
  inconsistent, or not fully connected to the interface.
- **Original: development-only** — test scene, cheat, or debugging behavior.
- **Original: not found / not included** — a capability or build artifact
  searched for but absent from the inspected repository.
- **Rebuild: confirmed** — a direction stated by the user in this conversation.
- **Rebuild: considered** — an idea raised for consideration, not yet a final
  requirement.
- **Rebuild: proposed structure** — an implementation approach discussed here;
  details remain open.

## Original Game

### Launch, Menu, and Scenes

- **Original: implemented** — main menu with a title, Play, and Quit. The menu
  title is **Color Chameleon**, while the repository and README use
  **Candy Cruisers**.
- **Original: partial** — a Leaderboard button is displayed, but it has no
  click behavior. The gameplay game-over leaderboard control is also unwired.
- **Original: implemented** — Play opens the gameplay scene.
- **Original: implemented** — gameplay scene contains the playfield, HUD,
  pause overlay, and game-over overlay.
- **Original: implemented** — pause and game-over flows offer resume/restart
  and return-to-menu actions.
- **Original: development-only** — RayCastTest and TestAnimation scenes are
  present but excluded from the normal build scene list.

### Player Controls and Survival

- **Original: implemented** — left and right arrow keys move the player.
- **Original: implemented** — horizontal movement wraps across the left and
  right edges of the playfield.
- **Original: implemented** — Space fires a single extend-and-retract tongue;
  the next shot waits for the previous tongue to reset.
- **Original: implemented** — touch input moves the player toward a touch
  target and supports a touch gesture for firing.
- **Original: partial** — touch movement attempts to wrap by moving through
  the nearer screen edge; touch bounds include a hard-coded screen-coordinate
  cutoff.
- **Original: implemented** — enemy missiles can hit the player.
- **Original: implemented** — a hit temporarily disables the player, then
  respawns them after about 1.5 seconds with flashing/invulnerability ending
  after about 3 seconds.
- **Original: implemented** — the player can repeatedly respawn; no finite
  lives counter was found.
- **Original: partial** — the README says P pauses, while the game uses Q for
  pause. P is a developer shortcut for forcing a purple enemy row.

### Color Matching and Enemy Chain Rules

- **Original: implemented** — five enemy colors are used: Red, Blue, Green,
  Purple, and Yellow.
- **Original: implemented** — the player's next shot color is selected from
  colors currently represented among enemies; if none are counted, a random
  color is used. Selection is uniform across available color types, not
  weighted by the number of enemies of each type (despite a source comment
  describing weighting).
- **Original: implemented** — a normal tongue kills enemies matching its
  current color. A mismatched shot can continue through the formation.
- **Original: implemented** — killing an enemy propagates to connected
  orthogonal neighbors of the same color.
- **Original: implemented** — an enemy with at least two adjacent enemies of
  its color becomes Special. The special visual uses a shared special sprite.
- **Original: implemented** — clearing the last enemy of a color grants a
  temporary magic/multicolor tongue and marks that color on the wheel.
- **Original: implemented** — magic can hit enemies regardless of their
  color; the stored magic value is capped at two.
- **Original: implemented** — successive successful shots increase a combo
  multiplier; a shot without an enemy hit resets it.
- **Original: implemented** — using magic contributes to a separate magic
  multiplier. At a threshold of five, the color wheel performs a spin.
- **Original: partial** — the wheel spins through three rotations and removes
  or deactivates wedges as magic builds; inspected code showed presentation
  feedback, but no gameplay reward or effect from the spin.
- **Original: partial** — color counts drive shot selection and some enemy
  abilities, but comments in the code acknowledge count drift during
  imitation and warping.

### Enemy Types and Abilities

- **Original: implemented** — Red enemies periodically fire straight missiles;
  Special Red enemies fire homing missiles.
- **Original: implemented** — Blue enemies generate a shield. A Special Blue
  enemy's shield can deflect a mismatched tongue; ordinary shields absorb a
  mismatched tongue and later reactivate.
- **Original: implemented** — Green enemies increase fleet movement speed;
  Special Green enemies add an additional speed contribution.
- **Original: implemented** — Purple enemies periodically warp another enemy
  into a nearby empty cell.
- **Original: partial** — Yellow enemies inspect nearby enemies and can adopt
  a neighbor's color. Special Yellow enemies attempt a disguise, but the
  comments describe copying abilities that are not fully reflected by the
  current ability dispatch.
- **Original: implemented** — ability cooldowns generally shorten as the
  global level rises.
- **Original: implemented** — approximate base cooldown ranges are Red
  missiles 4-8 seconds, Blue shields 25-45 seconds, Purple warps 10-30
  seconds, and Yellow imitation 10-18 seconds; level scaling modifies these
  timings.

### Formation, Waves, and Difficulty

- **Original: implemented** — the playfield is represented by 60 cells in a
  six-column by ten-row grid.
- **Original: implemented** — the opening formation contains 12 enemies.
- **Original: implemented** — the formation moves horizontally in a sweep and
  descends one row at each end of the sweep.
- **Original: implemented** — each descent adds six newly randomized enemies
  to the top row.
- **Original: implemented** — if enemies reach the bottom row when the fleet
  descends, the game ends.
- **Original: implemented** — clearing the formation awards a fleet-wipe
  bonus, resets the fleet position, and starts another batch. Batch size
  increases with level: 18 at level 1, 24 at levels 2-3, 30 at levels 4-6,
  and 36 from level 7 onward.
- **Original: implemented** — global level rises as cumulative enemy
  destruction reaches progressively larger thresholds: 18 for level 1, 54
  for level 2, 108 for level 3, 180 for level 4, and so on.
- **Original: implemented** — disconnected orthogonal groups that no longer
  connect to the top row are removed by the retreat check.
- **Original: partial** — any color/type can currently be selected for a new
  enemy; there is no level-based type unlock schedule or field-aware fairness
  director.
- **Original: partial** — new enemies are chosen individually at random; there
  are no authored six-enemy line patterns or constraints on combinations.

### Scoring and On-Screen Feedback

- **Original: implemented** — the apparent score award per enemy is
  `(100 + 10 * (level - 1)) * chainMultiplier * comboMultiplier *
  magicMultiplier`, with chain depth contributing to its multiplier.
- **Original: implemented** — a full fleet clear awards an additional
  level-scaled bonus.
- **Original: implemented** — small score/multiplier text appears near
  defeated enemies.
- **Original: implemented** — the gameplay HUD displays score, level, combo,
  and magic multiplier.
- **Original: partial** — the connected-match scoring traversal appears to
  increment score and enemy-destruction progression more than once for
  non-root enemies in a chain: once when discovered and again when dequeued.

### Menus and Persistence

- **Original: implemented** — pause menu stops game time and can resume.
- **Original: implemented** — restart resets score, level, multipliers, and
  reloads gameplay.
- **Original: implemented** — game-over menu shows the final score and offers
  restart/menu navigation.
- **Original: partial** — a Node.js/Express service stores scores in SQLite
  and exposes sorted score retrieval and score submission routes.
- **Original: partial** — Unity submits a score at game over to a hard-coded
  local address and always uses the player name “Suerte.”
- **Original: partial** — no in-game score retrieval/display, player-name
  entry, or working leaderboard-button action was found.

### Visuals, Animation, and Audio

- **Original: implemented** — a rotating galaxy backdrop is used in the menu
  and gameplay; gameplay also moves it vertically.
- **Original: implemented** — background art can change and fade in by level.
- **Original: implemented** — basic squash/stretch animation is used for
  selected actions, and the color wheel has a spin animation.
- **Original: implemented** — the player blinks during respawn protection;
  score popups and shield growth/shrink effects provide additional feedback.
- **Original: partial** — special states mostly swap sprites rather than
  giving each enemy a full animation language.
- **Original: partial** — color wedges and HUD text communicate some state,
  but there is no complete set of visual telegraphs for enemy abilities and
  matching rules.
- **Original: not found** — no in-game sound playback code or sound assets
  were found during inspection.

### Developer and Test Behavior

- **Original: development-only** — C logs enemy color counts.
- **Original: development-only** — D forces a descent; R, Y, B, G, and P force
  a descent with a particular enemy color; L forces a level increase.
- **Original: development-only** — Q pauses and also triggers a Red missile
  test when a Red enemy is present; X spins the wheel; T logs magic value;
  Z advances the background image.
- **Original: development-only** — a standalone raycast test script and two
  non-build test scenes are included.
- **Original: development-only** — `LaserController` is fully commented out
  and is not active gameplay.
- **Original: not found** — no automated project tests were found. The server
  test command is a placeholder that exits with an error.
- **Original: not included** — no playable desktop build was found in the
  repository.

## Rebuild Goals Discussed in This Chat

### Engine and Release Targets

- **Rebuild: confirmed** — continue with Unity.
- **Rebuild: confirmed** — release on the Apple App Store and the web, with
  Steam as a later target.
- **Rebuild: confirmed** — give the game a substantially better visual design.

### Enemy Unlocks and Controlled Spawning

- **Rebuild: confirmed** — Level 1 can spawn only Red and Blue enemies.
- **Rebuild: confirmed** — Green enemies become available starting at Level 2.
- **Rebuild: confirmed** — Yellow enemies become available starting at Level 4.
- **Rebuild: confirmed** — Purple enemies become available starting at Level 6.
- **Rebuild: confirmed** — the spawn system should track enemies currently on
  screen and make fairer choices about what enters next.
- **Rebuild: considered** — use authored five-enemy “line” configurations so
  rows look intentional and less cluttered than five independent random picks.
- **Rebuild: proposed structure** — select a whole five-slot line from
  level-eligible patterns, then evaluate it against the live screen composition
  and threat mix before spawning it. Newly unlocked types need not appear in
  every line unless the design later calls for a guaranteed introduction.

### Modular Animation Placeholders and Rule Telegraphs

- **Rebuild: confirmed** — build animations and visuals that explain game rules
  and enemy behavior to players.
- **Rebuild: confirmed** — use temporary generated placeholders so the game
  communicates clearly before final hand-drawn art is ready.
- **Rebuild: confirmed** — allow a human artist to replace placeholder art
  without rewriting gameplay systems.
- **Rebuild: proposed structure** — separate gameplay/collision objects from
  replaceable visual children; drive animation from semantic game events such
  as spawn, ability warning/action, match, chain, Special activation, and
  defeat.
- **Rebuild: proposed structure** — keep animation clips, states, parameters,
  sprite bounds, pivots, timing, and effect attachment points under a stable
  art handoff contract. Do not let animation frames determine gameplay results.
- **Rebuild: proposed structure** — use shared animator logic with replaceable
  clips where appropriate, allowing new placeholder or hand-drawn clips to
  occupy the same animation slots.
- **Rebuild: open design work** — enumerate the exact cues and visual language
  for each rule and enemy ability. Color can support a cue, but shape and
  motion should also make it readable.

## Source Map

- Main menu: Candy Cruisers/Assets/Scenes/MainMenu.unity and
  Candy Cruisers/Assets/Scripts/MainMenu.cs
- Gameplay scene and HUD: Candy Cruisers/Assets/Scenes/Gameplay.unity and
  Candy Cruisers/Assets/Scripts/InGameMenues.cs
- Player and tongue: Candy Cruisers/Assets/Scripts/PlayerController.cs and
  Candy Cruisers/Assets/Scripts/Tongue.cs
- Grid, movement, waves, retreats, and score traversal:
  Candy Cruisers/Assets/Scripts/GridManager.cs
- Enemy behavior and color abilities: Candy Cruisers/Assets/Scripts/Enemy.cs
- Colors and score/progression: Candy Cruisers/Assets/Scripts/ColorManager.cs,
  Candy Cruisers/Assets/Scripts/ScoreManagerSO.cs, and
  Candy Cruisers/Assets/Scripts/LevelManagerSO.cs
- Leaderboard client and service:
  Candy Cruisers/Assets/Scripts/LeaderboardManager.cs and GameServer/index.js
- Project overview and documented controls:
  Candy Cruisers/README.md

## Magic Retention Correction
- [x] A magic shot consumes all stored magic; only clearing the last enemy of a color earns another, never a partial chain or miss.

## Fleet Movement Update
- [x] Make each Green tick pulse a large rhythm-game arrow with green fill, a white rim and a dark outline; follow the fleet's direction and reversals, then fade inside the playfield edges.
- [x] Move the fleet on song-beat ticks whose distance matches the existing average Green-count and level-adjusted speed.
- [x] Pulse one Green per tick in repeating sequence, skipping dead or recolored enemies and including new Greens.
- [x] Preserve boundary contact, pause, wave reset, and special-Green dash behavior.
## Safe Player Respawn
- [x] A full enemy row at the player's height ends the run regardless of remaining lives or respawn protection, with the death animation before game over (five-wide early levels, six-wide later levels).
- [x] Respawn at the nearest position with at least one enemy-width of clear space between hitboxes for enemies occupying the player's row.
- [x] Use live enemy positions, keep the player fully inside the field, and defer blocked respawns until a safe gap opens without spending extra lives.
## Blue Shield Readability
- [x] Keep the Blue arc exterior and endpoints fixed while extending its thickness inward over the enemy, fading from a solid outer rim to a fully transparent inner edge.
## Arrival, Shield And Event Audio Update
- [x] Give special-Blue group shields a thick inward-fading band while preserving their contour and collision boundary.
- [x] Make regular row/wave arrivals grow from zero with a small rubber-band overshoot; retain distinct Purple warp portals.
- [x] Add replaceable sounds for Red firing and shield power-up. Green movement surges and special-Green dashes are visual-only.
- [x] Add distinct sounds for Yellow transformation, special-Yellow hiding/reveal, and Purple warp arrivals.
- [x] Play staggered enemy-death sounds with pitch rising along the chain multiplier, plus new-wave, level-up, and one-up cues.
- [x] Use the existing volume/mute/pause controls and a bounded reusable voice pool for event audio.
## Color-Clear Celebration
- [x] Scatter the last defeated group of each color into multicolored spacedust behind surviving enemies, preserving cascade timing and multiplier labels. Dust retains all six enemy colors during scatter and return.
- [x] Return color-clear spacedust to the player over two beats of the current song after its initial scatter, independently of tongue speed.
- [x] Render multicolored spacedust as soft round shimmering glints, retaining visible brightness and size until absorption at the player.
- [x] Randomize spacedust sizes independently of gameplay randomness; use the same shimmering glints for player death fragments and the game-over colored burst.
- [x] Start the missed-shot combo-break animation when the tongue begins retracting, without replaying it on arrival.
- [x] Combo-break text retains its last enemy color through the slash, then drains to white as its halves separate.
- [x] Use cumulative level thresholds of 10 * level * (level + 1), beginning with 20 defeated enemies for level two.
- [x] Pulse the solid progress fill in size on the music beat while a level-up is banked until the fleet clears; keep the overfill strip aligned.
- [x] Keep score/level text above life artwork and reserve compact-layout clearance for pulsing lives and progress fill.
- [x] Shift the shared refill measure anchor one beat later, independently of individual-beat timing, to correct the reported beat-four spawn alignment.
- [x] Add a 100-measure spoken counting metronome at Disco Descent's 115.03 beats per minute, with an explicit downbeat on "one" and F9 developer selection.
- [x] Let the refill hold shorten to fit the earliest available downbeat; a one-bar full set cleared on beat one spawns on the next measure's downbeat, including small frame delays.
- [x] Schedule enemy ability cooldowns in randomized whole beats against the playing song's beat map; Green and Orange windups finish on beats. Track changes and seeks retain pending beat intervals.
- [x] Animate each earned color bar from an oversized raised position into its resting slot.
- [x] Play ascending reward tones for successive earned bars; reset pitch when the bar sequence is lost or a new wave begins.
## Musical Reward Notes
- [x] Tune generated tonal effects and the tongue whistle to the playing soundtrack's relative-major scale, with per-song editable key metadata and preserved cue duration.
- [x] Play earned bars on the ascending C-major scale (C, D, E, F, G, A), resetting with the bar sequence.
- [x] Play cascade deaths as a two-octave C-major arpeggio (C, E, G, C, E, G), repeating for long chains with a stable pitched tone.
## White Background
- [x] Use a solid white gameplay background with no stars, tint shifts, or wave pulses; keep gameplay text readable in dark ink.
## Reward-Bar Jackpot
- [x] Apply the 75% selection-weight bonus independently to eligible Green and Purple when each exceeds 18% of the live fleet; exactly 18% receives no bonus.
- [x] Add F8 to skip gameplay music in editor/development builds.
- [x] Add Another Joe and Potential For Anything; use Another Joe on the menu and shuffle all four songs during gameplay, with per-track beat timing for rhythmic presentation.
- [x] Add labeled menu hotkeys for Play, Resume, Restart, Main Menu, pause, and starting-level adjustment.
- [x] Clear all earned bars and their spawn locks on accepted player hits and fatal defeat; rejected hits preserve bars.
- [x] Play ascending power-up tones on the major scale as each bar of a complete set disappears; retain descending power-down tones for partial sets.
- [x] Play a distinct slot-machine-style bell cascade and major-chord finale when every reward bar is filled, including at maximum lives.
## Magic Border
- [x] During live waves, pulse white/current player color independently of earned bars; run the clear transition for partial bar sets too, with descending power-down tones synchronized to earned-bar removal.
- [x] While a magic bullet is chambered, alternate the cabinet border between white and earned reward-bar colors; increase the flashing speed with each earned bar and restore the player tint when the shot is spent.
- [x] On a full-fleet clear with every bar earned, cycle the player and border through the bar colors while growing and removing each bar; hold white at the end, then adopt the next fleet's player color exactly when it spawns.
- [x] Randomize full-set bar removal with double-time white/color flashes; make the final bar the next player's color and guarantee it in the next fleet.
- [x] Use Disco Descent for gameplay and the previous track for the menu; pulse the gameplay border from the song's beat clock, using white/black when colorless.
- [x] Correct normal border to earned colors followed by one white step, or solid white with no bars; use a beat-count clear transition with continuous white/color pairs ending on the next player's color, not white.
# Main Menu Slime Presentation
- [x] Minimal main menu with a larger beat-pulsing BASS INVADERS title, THE RHYTHM IS OUT THERE subtitle, and saturated beat-colored perched character matching its gameplay size.
- [x] Tap/click anywhere or press Enter to fade the title and float the character into position before gameplay.
- [x] Flat-bottom slime placeholder with a planted base, upper-body jelly deformation and following eyes; movement and beat pulses preserve the grounded base and collision bounds.

## Green Tempo And Rhythm Arrows
- [x] Exclude special Greens from ordinary movement counts and pulse rotation.
- [x] Raise tempo by 3% per special only when its ability completes; allow one activation per special.
- [x] Share a chamfered dance-pad arrow with black outline, green gradient and white insets; keep activated specials' up arrows centered on their enemies.
- [x] Ease removed boosts down over one beat and animate detached down arrows when special Greens are cleared.
- [x] Preserve pause behavior and validate desktop layouts and the native iPhone simulator.

### Beat-based enemy actions

- [x] Remove the regular Green dash ability and its trail visuals, preserving regular beat-pulse fleet movement.
- [x] Retreat disconnected enemies one grid space per soundtrack beat.
- [x] Use one shared warning window for scheduled abilities: the beat before activation.
- [x] Run regular Yellow transformation and special Yellow disguise transition for two soundtrack beats.
