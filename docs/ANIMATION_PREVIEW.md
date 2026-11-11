# Animation Preview

Menu Arrival includes the descending title, subtitle tilt and rising light,
then the slime entrance. By default, All Three plays Flip Right, Skid Left and
Tractor Beam in sequence with a settled-menu hold between them. Its Character
Entrance selector can also isolate any one version. Previewing never marks the
real first-launch intro as seen. Start Game still
begins at the fully settled menu and previews the separate gameplay departure.

Open `Assets/Scenes/AnimationPreview.unity` and enter Play mode. Open
**Candy Cruisers > Animation Preview** for the companion control panel.
The panel's folder and play controls can also open and start the scene.

Choose an animation to load its prepared scenario. Use Replay to reset it,
Pause to hold a frame, and Step to advance one frame. Speed ranges from 0.1x
to 2x. Color, level, and beats per minute rebuild the scenario; minimum duration
and sound effects can be changed during playback. With Loop off, playback
pauses at the end of the selected cycle. Each scenario has a minimum running
time so a short cycle setting cannot cut off its later examples.

Presets cover idle beat pulses, title arrival, ordinary and magic tongue,
shield deflection, enemy arrival and death, color-clear dust, wave transition,
life loss and gain, combo break, the full game-over sequence, Red shots,
Green tempo arrows, Purple summon, Yellow imitation, Orange burst, and
missile-following eyes.

- Menu Arrival ends on the idle menu. Start Game starts there and holds on gameplay afterward.
- Idle begins with partial progress and two powered bars, then shows the real
  pending-level beat flash with partially banked progress.
- Tongue, Magic Tongue and Shield Deflection each run stationary, rightward,
  leftward, stopping and reversing shots through the actual player firing path.
  Magic clears singles and multiple groups, earns bars and ends with a depowering miss.
- Enemy Spawn and Enemy Death include every color, basic sprites and special
  trios with horizontal and bent chains. Replays randomize the fleet layout.
  Orange retains its unique always-special, single-enemy gameplay rule.
- Life Gain includes the complete reward-bar wave and following fleet spawn.
- Purple Summon covers every color and summons that complete special trios.
- Yellow Imitation pairs regular and special copies across all eligible colors
  and target variants. Ordinary Yellow cannot copy Orange, so the Orange example
  pairs a regular Red copy with the special Orange disguise.
- Missile Gaze includes an orbit, diagonal near miss, and four real angled hits.

The stage loads the current Gameplay scene additively at runtime, so it uses
the real artwork and presentation code rather than copied animations. Replay
unloads that entire temporary world, including transient particles and cues.
The source Gameplay scene is not edited. Gameplay input and leaderboard writes
are disabled; only abilities relevant to the current scenario run. A silent, controllable
beat clock replaces music; the Sound effects toggle enables effect audio.

The scene and its editor-only controller are not included in player builds.
Leaving Play mode restores time scale, audio pause, spawn override, and
developer invincibility settings. Preview controls persist for the editor
session. Run **Candy Cruisers > Run Animation Preview Checks** to exercise
every preset in Play mode. Like the project's other automated checks, this
command closes the test editor when complete; run it in an isolated project.
