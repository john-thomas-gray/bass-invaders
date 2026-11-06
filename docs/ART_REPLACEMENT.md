# Character Art And Animation Replacement

## Player Death

Recoverable player hits invoke `CharacterVisuals.PlayerDefeat`, including the normal
Defeated event and Animator trigger. The player's existing defeat-prefab slot
replaces the entire placeholder, including its duration or completion event.
With no prefab assigned, recoverable hits use `PlayerDeathBurst`: it snapshots
the body and eyes, flashes white, then scatters colored fragments for 0.9 seconds.
Fatal game-over hits invoke `CharacterVisuals.PlayerFatalDefeat`.
`GameOverBlackout` keeps the left pupil spinning counterclockwise and the right
clockwise throughout the four-beat rise. At centerstage, `PlayerMeltDeath`
continues each orbit through a final 180-degree turn over 0.8 seconds, easing to a
stop at the authored default gaze. The round pupils morph into thicker X's during
that same half-turn. The X diameter is 30 percent smaller than the original version.
After a short hold, one silhouette continuously flattens and widens as its color
fades to white. A single smooth curve carries it from the body into a thin white
horizontal line that reaches both physical screen edges, with no pause at a
puddle stage.
It then contracts into a small white point and fades out like a tube television
switching off. The 2.8-second cue snapshots the current body and face; its floor
stays fixed as it collapses. The original body hands off at the same size and
position; facial features fade before flattening into stray strips. Line width
follows the camera viewport, including after a phone rotation. The line
and final point retain a minimum pixel thickness on small screens.
No explosion, shockwave, colored dust or shattering sound accompanies final death.
The four-beat blackout moves the artwork to centerstage before this cue starts.
The real sprites stay hidden until the scene restarts for
fatal contact, or until respawn after a recoverable hit. Unlike enemy defeats, it
never displays a digit.

During `GameSession.RunState.Dying`, gameplay is suspended and the session drives
the cue timer exactly once per frame. Game over follows cue completion; the
prefab duration remains a fallback for missing animation events. Pause cannot
overlay this short fatal transition. For recoverable hits, `PlayerMovement`
advances the cue during its recovery timer, without freezing the fleet. The
replacement becomes controllable at its 0.33-second jump apex, independently of
the outgoing death cue. The descent is invincible without flashing; landing at
1.5 seconds starts the separate 1.5-second flashing invulnerability interval.

## Saved Hierarchy

Each enemy prefab has a gameplay root with `CharacterVisuals`, a `Visuals`
child, and a `Visuals/Body` sprite renderer. The player has the same visual
root containing its body, eyes and pupils. Its tongue remains a separate child
so firing geometry is not moved by body animation. Grid identity, movement,
abilities and scoring remain on gameplay objects.

`TongueShot` creates a separate `Tongue tip bulb` sprite at runtime. It sits
slightly beyond the line endpoint, is 2.2 times the authored end width, and
has a solid center with a narrow antialiased edge. It inherits the endpoint color
and magic-shot scaling. `TongueShot` alone controls its visibility; player restart,
respawn and invulnerability flashing must not enable an inactive bulb. Replace that sprite or its
renderer independently when final tongue artwork is available; it has no hitbox
and does not alter tongue collision distance.

`CharacterVisuals` stores explicit body/root references and serialized local
hit bounds. Animate the visual children, not the gameplay root. Body sprite,
offset and scale changes do not change tongue or player collision bounds.
Shield activation remains ability-driven, with hit bounds independent of its
power-up artwork. Edit the bounds deliberately only when changing gameplay.

The idempotent `Candy Cruisers > Upgrade Visual Children` editor command migrates
the five enemy prefabs and saved gameplay scene without regenerating gameplay.

## Replace Body Art

Open the relevant enemy prefab under `Assets/Prefabs` and replace the sprite on
`Visuals/Body`. Keep its body reference assigned on `CharacterVisuals`. Fit the
new artwork using the Body child's scale and offset; leave the gameplay root
and its saved hit bounds alone. Use the spawner's existing `Special Sprite`
slot to replace the shared special design. Orange shares this slot, including
when imitated by Yellow.

The unchanged supplied sheet remains at `Assets/Resources/EnemyBeatSheet.png`.
The editor command copies its six 64 by 64 frames into
`Assets/Resources/EnemyBeatFrames.png`, adding two transparent pixels around
each frame. Sprite rectangles cover only the original pixels; the surrounding
margin prevents sampling slivers from adjacent enemies during animation.
The leftmost source pair is Regular A/B, the middle pair is retained for
reference, and the rightmost pair is Special A/B. Centered full-rectangle sprites,
point filtering, original transparency, no mesh extrusion, no mipmaps and no texture compression
preserve the pixels on desktop and iOS (iPhone Operating System). The import
scale keeps the previous sprite width so shield sizing and saved hitboxes stay
consistent. The previous normal, squid, sentinel and fruit artwork remains in
`Assets/Art`.

`CharacterVisuals.RefreshBeat` selects a frame through `EnemyBeatSprites` from
the whole-number song beat: A on even beats and B on odd beats. It uses the same
tempo map as the existing beat pulse, so new arrivals immediately join the same
frame, skipped rendering frames do not cause drift, and pausing music freezes
the frame. Yellow's displayed copy and transformation overlay follow the same
frame. Art outside those two pairs is left alone. The editor command
`Candy Cruisers > Use Beat Enemy Sprites` reapplies the sheet and prefab/scene
assignments while retaining the old assets.

For animated sprite sheets, put an Animator on `Visuals`, assign it to the
actor's `CharacterVisuals.Animator` field, and animate the Body child's sprite
or transform. The existing color warnings and disguise effects still control
body tint; do not keyframe tint unless intentionally replacing that presentation.

## Firing, Matching And Defeat

The actor's Inspector has three independent `PresentationCue` prefab slots:

| Slot | Trigger | Default |
| --- | --- | --- |
| Fire Prefab | Accepted player fire or actual Red missile creation | Short colored pulse |
| Match Prefab | Each enemy actually removed by a matching clear | Expanding colored ring |
| Defeat Prefab | The same confirmed removal | Existing cascading shards and numbered digit |

An empty slot uses the current placeholder. Assigning a prefab replaces that
placeholder; it does not stack another copy on top. Match and defeat effects
are detached before the enemy is removed, so they finish after the enemy's
gameplay object has gone. Both use `(depth - 1) * 0.065` seconds of cascade delay.
Defeat scoring uses that same depth immediately, without waiting for animation.

Create a prefab with `PresentationCue` on its root and put its sprites or
Animator under an `Artwork` child. Assign that child to the cue's Artwork field
so it starts at the correct cascade time. The effect spawns at the source body
position; author its dimensions in world units. Set Duration to its maximum
playback time after the delay. Tint Sprites applies the enemy/player color once;
disable it for precolored drawings. The instantiated component exposes Color
and Depth for optional custom presentation scripts. Multiplier is the score and
label value: hide it when it is 1. Depth controls local cascade timing, while
Multiplier may continue increasing across multiple chains hit by a magic shot.

## Hooks And Clip Events

`CharacterVisuals` exposes Inspector events Fired, Matched and Defeated plus
optional Animator trigger names (Fire, Match, Defeat by default). Only valid
trigger parameters are sent. These events are notifications after gameplay has
accepted the action, not requests to perform it. Enemy body animation stops
when the enemy is removed, so put lasting match/death animation on cue prefabs.

The `VisualAnimationEvents` component on Visuals accepts the clip event
`OnAnimationFinished` and forwards it to the actor's Animation Finished event.
For detached cue prefabs, Started runs once after the branch delay. A clip on
the cue root can call `PresentationCue.Finish`. For an Animator on Artwork,
add `VisualAnimationEvents` beside it and call `OnAnimationFinished`; the relay
finds the parent cue without additional code or wiring. Duration is always
a fallback, so a missing clip event cannot leave a permanent effect behind.
Completed fires only once. A Finish call before the branch delay is ignored.

Never wire these presentation events to damage, spawning, scoring or movement.
No shipped completion callback performs those operations. Fleet refill keeps
its existing wait for detached effects, while enemy removal, score and color
clear rewards occur immediately. Scene reload clears all effects; destroying
the grid also cleans up its detached match/death effects.

## Verification

`PresentationChecks` runs in both full editor and Play mode suites. It checks
saved visual-child hierarchies, animation-independent hit detection, accepted
fire only, exactly-once match/defeat hooks, custom prefab replacement, color and
depth context, delayed starts and cleanup without a clip completion event.
It also runs `EnemyBeatSpriteChecks` for pixel-exact extraction, transparent
sampling margins around all six frames, sheet regions, frame boundaries,
30/60/120-frame timing, promotion, Yellow imitation and stable hitboxes.
`EnemyBeatSpriteRuntimeChecks.RunDesktop` and `RunPhone` verify both designs
against actual song playback, pause and resume at desktop and phone sizes.
## Special Blue Shield Shock

A mismatched ordinary tongue hitting a special Blue shield emits a blue-white
electrical pulse from contact to the player. Its travel lasts one beat of the
current soundtrack, captured at contact; `TongueShot.ShieldShockSeconds` supplies
the default-tempo fallback. Very close contact shortens it to the remaining
tongue return time so the existing stun and return speed are unchanged. Two child
line renderers supply a jagged white core and wider blue glow, leaving a gray
tongue trail behind the moving front. `ShieldShockArrived` starts
`PlayerShockVisual`: a large 0.12-second impact flash, then a black soot body,
30%-size pupils and three wisps of smoke. Once the tongue finishes returning,
the eyes blink and 30 sprite-matched soot fragments fall away over 0.7 seconds,
revealing the already-selected next color. Soot shares the player's visual sorting group with explicit
body/soot/eye/pupil ordering, so it never covers the face; original face orders
are restored afterward. Temporary soot is excluded from extra-life art copies.
Controls resume on normal tongue
completion, independently of this visual recovery. Damage and restart clear
the effect and restore eye proportions. Pause freezes it. Blue and magic shots
bypass the shock; the existing immediate stun and slow return remain unchanged.

# Reward-Bar Trampoline

Extra-life miniatures use `LifeSlimeArtwork` to copy the actual player's sprite
pieces, tints and neutral local proportions. Their independent jelly texture
uses the player's same silhouette generator; eyes use the original sprites.
Copies never inherit the player's live deformation, transform or gaze.
`PlayerSlimeVisual` eases the live pupils toward the nearest active missile and
back to their authored resting gaze when none remain. Eye whites and collision
bounds stay fixed, and gaze motion composes with jelly deformation and beat pulses.
`LifeSlimeChoreography` supplies a shared offbeat pulse and one
side-to-side measure every five measures; switches respect the current track's
downbeat offset. Its one-shot flip is an exception to measure-boundary switching.
`PlayerLifeGainAnimation` schedules the offscreen entrance and two-beat upward
flight from `FullSetCelebration.FinalSpikeTime`, clips entry at the border and
arcs above the destination ledge before landing. `PlayerLifeIcons` owns the
consumed spare's optional takeoff flip, the stable tally slots, queues and cleanup.
The replacement travels sideways from takeoff with a curved rise and descent.
Its ascent eases inward toward the playfield center by up to 80% of a body width,
independently of its eventual landing position. The drawn rise and real-player
apex handoff use the same inward target.
Before takeoff, `PlayerLifeIcons.AnticipationSeconds` holds it in its life slot
for 0.2 seconds: the first half compresses to 50% height and the second holds
that pose. Rotation waits until takeoff. The spring stretches to 120% height,
then returns to neutral at the existing apex; control and landing timing stay unchanged.
`PlayerLifeIcons.LossVerticalScale` supplies subtle takeoff compression, flight
stretch and landing compression. The icon drawing and the real actor share
that curve, returning to neutral scale at the apex handoff and at landing.
`CharacterVisuals.SetJumpStretch` deforms only the artwork from its feet, leaving
the actor transform and collision bounds untouched.
`CharacterVisuals.BeginLanding` adds a shared 0.3-second compression and rebound
after replacement-life landings, reward-bar flips and the title-screen descent.
It composes with flight stretch and beat pulses, pauses with game time, and resets
on damage or restart. Tune `LandingSeconds` and `LandingScale` for its feel.
At `ApexSeconds`, it hands the downward jump to the real `PlayerMovement` actor;
the actor is controllable and invincible in the air, but its flashing protection
timer begins only on landing.
Life-count changes and respawn safety remain gameplay-owned.

Full-set clears send fixed-width vertical spikes across the earned bars from
left to right. `FullSetCelebration.BarSpiked` fires once as each bar rises;
after each bar's peak, `PowerDownProgressFor` drives the same color, glow and
highlight drain renderer used for partial-set power-downs. Waiting bars stay
lit, and each drain finishes at its existing removal boundary without changing
reward or sound timing.
`GameSession` checks the full neutral body width against the visible bar surface
(excluding slot gaps). At least 10% horizontal overlap launches the player.
At 10-50% overlap, lifting the right side rotates counterclockwise and lifting
the left side rotates clockwise. Above 50%, rightward movement rotates clockwise
and leftward movement counterclockwise. Stationary players follow the lifted
side, defaulting clockwise when centered. Keyboard and touch movement share the
same direction tracking, including across screen wrapping.
`PlayerCelebrationFlip` then animates only the visual children with a small hop
and one rotation, landing upright. Movement, collision bounds, pause behavior,
and the reserved next-fleet downbeat are unchanged. Partial-set power-downs do
not launch the player. The rightmost bar supplies the final color, which the
session reserves in the next fleet.
