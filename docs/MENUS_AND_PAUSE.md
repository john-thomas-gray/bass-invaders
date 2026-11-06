# Menus And Pause

The gameplay scene opens on a black, title-only main menu. BASS INVADERS pulses
to the playing soundtrack. On arrival, the complete title descends from above
the screen, traverses back and forth and drops at its turns like the fleet.
It starts above the right edge, drops, sweeps to the left edge, drops, sweeps to
the right edge, then drops to its final height before sliding horizontally to
center. Every direction change has an equal vertical drop between horizontal sweeps.
The title waits until the first menu beat, then each vertical and horizontal leg
starts and ends on the menu song's 140-beats-per-minute grid.
Once it reaches its usual position, the subtitle tilts up from a flat baseline
and its light sweeps upward. The colored slime then arrives by one randomly
chosen entrance: a flip from the right, a flight and skid from the left followed
by a pivot and counterclockwise flip, or a descent in a white conical tractor
beam with traveling rings. The beam is wider than the entire character during
descent and draws over him, so he appears inside it. On landing it fans shut
to a bright narrow line, then retracts upward into space. All three land at the
original perch, at normal gameplay size.
`MenuIntroAnimation` owns these paths and stage durations; the title keeps its
live soundtrack pulse throughout. Tap or click outside menu controls, or
press Enter or Space, to fade the title and float the slime down to its gameplay position
over 1.2 seconds. The opening fleet starts afterward; input and enemy activity
remain locked throughout the arrival. Repeated taps do not restart it. THE
RHYTHM IS OUT THERE sits beneath the title, changes color on the beat, and fades
with it on arrival. Once the intro settles, **Song Selection (S)** opens in the
upper-right corner. The rest of the menu has no previews or leaderboard.
The soundtrack continues uninterrupted through the arrival. During the intro,
a tap, click, Space or Enter skips directly to the settled menu without starting
gameplay. The next activation starts the normal departure. Once the intro has
finished naturally, the first activation starts gameplay as usual.
On the first launch, the tractor-beam version is mandatory and all skip/start
inputs are consumed until its beam has zipped away. Completion is saved under
`CandyCruisers.MenuIntro.Seen`; quitting early leaves it unseen. Subsequent
launches choose randomly and allow skipping. Animation Preview does not read
or change the first-launch restriction or completion flag.

The solid-colored subtitle glows as one continuous light source. The perched
slime's body uses the subtitle's current saturated palette color, changing
directly to the next color on each beat without following the subtitle's white
pulse brightening; its eyes and pupils retain their authored colors.
The subtitle has a fine
white outline and a softer, wider local glow to keep its faces readable against
the light. The main title is opaque white and the slime keeps its normal menu
artwork; both block the light
and cast projected shadows without being darkened. Light escapes around and
through gaps in the actual letter and sprite silhouettes, not prearranged rays.
The light is treated as behind the artwork: enclosed letter counters such as
the opening in R transmit light even when a lower stroke lies beneath them.
The shadow shader integrates transmission rather than stopping at the first
stroke. The lower outer edges rise at 15 degrees from the subtitle's outermost
bottom ink corners, measured from font glyphs rather than the text layout box.
Adjust `MenuBacklight.LowerEdgeAngle` to change this angle.
The light follows the subtitle's beat color, breathes with the soundtrack, and
fades with the menu. It does not appear in gameplay or pause menus.
`MenuBacklight` draws the current font and sprite silhouettes into a reusable
offscreen texture. `Assets/Resources/MenuLight.shader` controls source width,
falloff and shadow contrast; `MenuBacklight.Intensity` controls the beat response.
`GameSession.DrawMinimalMainMenu` controls the subtitle glow. The shadow follows
the title pulse and departing slime. Render textures resize with the window and
are released with the session.

`GameSession.MenuArrivalSeconds` controls the descent duration;
`MinimalTitleRect` controls the title position and beat expansion.
`PlayerSlimeVisual` owns the replaceable flat-bottom body and upper-body jelly
wobble. The bottom row remains fixed, deformation increases toward the top,
and the eyes follow the wobble. The player beat pulse is anchored at the feet.
These visual changes do not resize collision bounds.

Use P, Escape or the pause button in the upper-right playfield header to pause.
P, Escape or Resume continues the same run. Losing focus or backgrounding the
application pauses an active run; returning to it does not resume automatically.
The pause menu also offers Restart, Main Menu, and a **Beat & Clock** panel with
independent saved voice metronome, visual beat counter, and round clock switches.
The clock defaults off and displays elapsed round time as `00:00` (minutes:seconds).
It counts during gameplay and fleet refills, freezes while paused or after fatal defeat,
and resets for each new run. Hiding it keeps the elapsed time running. When both
displays are enabled, the clock and beat counter share unused space outside the
playfield: side gutters on wider screens or the lower margin on tall phones.
They compact or stack when needed, respect the safe area, and keep a fixed size
through magic pulses and other gameplay animation.
These controls appear only while
paused. Back returns to the pause menu without resuming; F9 and F10 toggle voice
and visual metronomes while paused; F7 toggles the clock. The main menu has no metronome controls. Game over shows only a white
GAME OVER title above screen center; the final score is still saved to the local leaderboard.
R restarts a current run.

**Song Selection** is available from the settled main menu and while paused.
All songs are checked by default. Click or tap a song's checkbox to include or
exclude it from gameplay; at least one song must stay checked. Choices save
immediately and survive restarts. Previous and Next page through the soundtrack.
On desktop, S opens the menu, Up/Down selects a row, Left/Right changes pages,
and Space or Enter toggles the focused song. Back, Escape, P, or S closes the
panel; closing it during pause keeps the game paused. The menu consumes taps
and keyboard shortcuts so editing a playlist cannot start or restart a run.
The menu song and gameplay shuffle both honor the selection. Changing the
selection replaces an excluded current song, preserving pause when applicable.
Levels use their configured tracks when any are selected; when none overlap,
they use the player's selected soundtrack. The separate voice metronome setting
is unaffected by song selection.

Pause buttons show their hotkeys in parentheses: Resume (P / Enter),
Restart (R), and Main Menu (M). After GAME OVER fades in, Enter also restarts.
Left and Right still adjust the main menu's starting level as hidden testing
shortcuts. Typing one or two number keys directly enters the
starting level, capped at 99. Numeric-keypad Enter works wherever Enter does.
Main Menu and level-selection keys are active only in their corresponding menus;
Escape remains an alternate pause/resume shortcut.

Fatal fleet contact first enters a Dying state. Player controls, fleet,
abilities and missiles freeze, with no regular death animation triggered.
The current music slows dramatically in pitch and playback speed while fading to silence.
The scene, rim, effects and score
fade to full black over four beats of the song's tempo captured at that hit;
the intact player stays visible above the blackout and glides to centerstage throughout the fade.
The fade keeps its original four-beat duration even as the music slows; music is fully
stopped at blackout, leaving the subsequent shatter at its normal speed and volume.
Only after the fade completes does the game-over death cue begin, replacing
the player with a multicolored powder explosion inspired by the Death Star reference.
At the 0.14-second fracture, a narrow, nearly vertical shockwave disc tilts eight degrees
and expands beyond the frame. Its bright white crest has soft edges and a close trailing
ripple; the near edge broadens as it passes. A dense spray of 960 soft dust grains uses
every enemy color unlocked at the current run level, even if absent from the fleet.
The uneven radial spray continues spreading and grows with perspective, without spark
streaks or a fiery plume. Both dust and shockwaves clear to black at 2.4 seconds.
The origin follows the centered
player-death artwork and its geometry adapts to portrait, landscape and the rim crunch;
it never changes gameplay time, camera framing or hitboxes.
A glass-shattering sound starts at 0.08 seconds, just ahead of the player's
0.14-second first-fragment marker, with stereo echoes and a 2.8-second reverberant tail. The old descending
game-over cadence no longer plays. Ordinary recoverable deaths do not black
out the scene or stop the soundtrack.
Score submission waits for the fade and then the full death cue (another 2.4 seconds for the
placeholder). Once the death cue finishes, white GAME OVER text above screen center fades in
over 0.65 seconds against the black screen, using the same Bungee font as the main
title. No panel, score, leaderboard or buttons are drawn. Input remains blocked
until the text is fully visible; then a tap or click returns to the main menu,
Enter or R restarts, and Space or M returns to the main menu. The transition uses unscaled
time; pause cannot obscure or stall it.

Pause is separate from the underlying Playing/Refilling state. It freezes
scaled time, locks player input, and suspends the fleet, spawning, abilities and
missiles. Tongue travel, death effects, cooldowns, recovery and refill delays
remain intact. Resuming restores the previous time scale and the correct
underlying suspension state. In particular, resuming a refill allows player
movement while keeping the fleet suspended until the new batch arrives.

Menu clicks are excluded from player gestures. Pause discards pointer movement;
starting or resuming also suppresses gameplay input through the following frame
to prevent menu activation from firing a shot.

Restart and Main Menu reload the gameplay scene, clearing the old run and its
effects. Restart bypasses the main menu and starts a new run. Returning to the
menu leaves the new scene idle until Play. Leaving or destroying a paused
session restores global time so the next scene cannot remain frozen.

`GameSession` owns menu state, navigation and drawing. `PlayerMovement` filters
menu gestures. `LocalLeaderboard` stores positive game-over scores in
`PlayerPrefs` and keeps the top five by score. `MenuChecks` covers pause state
and preserved timers; the full runtime suite also exercises menu startup,
returning from pause, real-frame tongue freezing, resume and restart from pause.
