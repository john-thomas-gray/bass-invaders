# Difficulty playtesting

Open **Candy Cruisers > Difficulty Playtesting** in Unity. The tools are available
in the Editor and development builds on desktop and iOS (iPhone Operating System).
They are off by default. They use a local rule-based AI (artificial intelligence)
player and do not require a service, account, or trained neural network.

## Record the two human baselines

1. Enter a participant label, such as `john-skilled`. Click **Arm human recording
   and open game**, then start a run normally. Play without invincibility or spawn
   overrides. A run ends at game over, or pause and use **Playtesting > Stop and
   save recording** to save a partial session.
2. Repeat with `brother-new`, on the same game revision, level map and device/input
   setup. For comparisons, give both players the same starting stage and rules.
3. In the Editor window, select one participant's recordings and name the profile
   `skilled` or `inexperienced`. Click **Create profile from selection**. Repeat
   for the other participant. Assisted runs and bot runs cannot enter calibration.

The development build has a **Playtesting** button in the main and pause menus.
**Record human run** starts recording from the beginning of the run. On a phone,
retrieve its `Playtesting/Runs` folders from the app's data container using Xcode's
Devices and Simulators window. Copy them into the desktop `Playtesting/Runs`
folder before calibrating. Put profiles in the phone's `Playtesting/Profiles`
folder to select them in its development panel.

A run apiece is a starting sample, not a reliable estimate of a population's skill.
Record several runs per person, preferably on different days. Keep a later human
run out of calibration and compare the bot against it. If your brother does not
reach stage 7, there is no measured inexperienced-player benchmark for stage 7.
Record a separate run starting there to obtain one. Habituation matters: someone
who has practiced dozens of calibration runs no longer represents a first-time player.

## Recorded information

Each run has a directory under `Application.persistentDataPath/Playtesting/Runs`.
The Editor's **Open recordings, profiles and batch reports** button reveals it.
On this project's macOS builds, the parent persistent-data folder normally lives
under `~/Library/Application Support/<company>/<product>`; use the reveal button
for the authoritative location.

- `metadata.json`: participant, controller, platform/input family, build/version,
  starting stage, complete resolved authoring configuration, configuration hash,
  seed for automated runs and the initial Unity random state.
- `events.jsonl`: append-only JSON (JavaScript Object Notation) records for
  accepted shots, movement commands in world units, shot contacts, completions,
  cancellations, charge awards, damage, threats and fleet clears. State samples
  occur every 0.2 seconds of active run time. Events include frame number and run
  time. This is an action/measurement log, not a video or deterministic replay.
- `summary.json`: final outcome, score, reached stage, lives and stage-specific
  measurements. Written through a temporary file on normal stop, game over,
  scene unload and application quit. A crash can leave an incomplete event log
  without a summary; incomplete runs are not calibration inputs.
- `profile.json`: the exact benchmark profile used by an autonomous run, when one
  was selected.

Ordinary and magic shots are measured separately: fired/completed/interrupted
counts, hit rate, kills per shot, contacts per shot, and snapshot efficiency.
Magic contacts measure successive chain hits within a shot; the longest magic-hit
streak measures consecutive successful magic shots. An ordinary shot or damage
breaks that magic-shot streak. Other measurements include actual charge grants,
awards at the charge cap, damage, combo breaks/peak, fleet clears, environmental
Orange kills, movement distance, combat time, ready-to-fire time and live-enemy
exposure. A cancelled shot is recorded as interrupted, not a completed miss.

Shot capacity is a **stationary board snapshot at launch**: evaluate reachable
horizontal firing lanes using current hit bounds, shields, linked Yellow groups
and normal/magic penetration. The query does not advance combat or spend shields.
Efficiency is actual kills divided by the best such lane's kill count, averaged
only over completed shots with a nonzero opportunity. Preserve zero-opportunity
shots in the other counts. A moving magic shot, changed shield or newly spawned
enemy can produce a value above 1; the raw value is retained. This denominator is
not a prediction of the maximum kills possible under future movement and timing.

## Stage normalization and calibration

A separate benchmark is stored for every observed stage. Stage 7's larger action
count does not overwrite stage 1's accuracy or behavior. Each stage reports:

- shots and kills per minute of alive, active combat;
- kills per live-enemy second (the integral of enemy count over alive combat time);
- normal and magic hit rates and mean snapshot-relative efficiency;
- charge, damage and combo counts with their exposure denominators retained.

Pauses, menus and death presentation do not add combat exposure. Refills add run
elapsed time but do not add combat time. Respawns and invulnerability are not
movement actions. Stage attribution for a shot stays with its launch stage even
if its final hit advances progression.

The initial policy maps ready-to-fire delay, average movement speed, hit rates,
shot efficiency and visible-threat avoidance to movement and firing decisions.
Threat episodes currently mean a missile within three world units whose current
heading intersects the player. Time until that heading clears the player is a
coarse avoidance-duration measure, not a human reaction-time measurement.
Threats that overlap are tracked one at a time. These are explicit approximations
and do not identify why a player made a decision.

The driver has delayed decisions and imperfect aim. It calls the same `Move` and
`Fire` methods as human input, respects stun/respawn, and receives no free charges,
colors, damage prevention or direct kills. It uses the shared keyboard movement
speed on both platforms. Touch movement can be faster than that speed; a profile
from touch play therefore needs separate outcome validation. Present-board group
queries also give it more precise spatial information than a human sees. Its
behavior is a fitted test policy, not proof of equivalent human skill.

Stages/types with fewer than five completed recorded shots are marked as sparse
or unmeasured. An unobserved shot type uses the selected unmeasured fallback;
unseen stages use the whole fallback policy. No fallback is presented as human
calibration. A different level configuration is marked as transferred. Recalibrate
after changing combat rules; matching map hashes alone cannot establish that two
builds have identical behavior.

## Run experiments

Choose a profile, first seed, starting stage, number of runs and time limit in the
Editor window, then click **Run autonomous batch**. Optionally set a target stage;
the run stops when it reaches that stage. Automated desktop runs continue when
the window loses focus. Manual pause and phone backgrounding still pause a run.
Use **Stop batch / save recording** to stop early.

**Add level-map scenario** accepts a validated alternate level map and copies it
under `Playtesting/Scenarios`. Add several maps to test different palettes, fleet
layouts and spawn lines. Each scenario receives the same seed range, with the
requested number of runs per scenario. Numbered colors and other existing game
randomness supply randomized configurations within those rules. The source
`Assets/Resources/LevelMap.json` is never rewritten. On a development device,
scenario paths are relative to its persistent-data playtesting folder.

A separate random generator controls bot choices so observation/aiming does not
consume the gameplay random stream. Seeds aid repeated comparisons, but music,
frame scheduling and different actions can change the sequence of game random
calls. They do not guarantee identical encounters or cross-device replays.

`Playtesting/Batches/<batch-id>.json` holds outcomes and the complete run summaries.
The companion CSV (Comma-Separated Values) has one row per run and stage, including
observed metrics, human targets, configuration, platform, seed, assistance flag
and evidence status. Compare stages separately. If you need an overall skill
score later, average stage-normalized distances with explicit stage weights;
do not pool raw late-stage kills into one score.

Use the same scenario and seed ranges for both profiles. Count target reached,
game over and time limit separately: surviving the time limit is a censored
observation, not completing the game. Report the distribution over runs and the
sample counts, then compare against held-out human play. Bot metrics are outcomes
to validate, not guaranteed quotas. A high hit rate alone does not establish that
the model reproduces human strategy, difficulty or survival.

Automated runs do not submit scores or unlock human starting levels. Existing
human recordings and profiles are retained. The Editor keeps playtesting options
between scene reloads; use **Disable playtesting** to return to ordinary play.

For device or command-line automation, a development build also accepts a
`BASS_INVADERS_PLAYTEST_OPTIONS` environment variable containing a serialized
`PlaytestOptions` object. It is read once at launch, then the saved options carry
the batch across scene reloads. For example, use `mode: "bot"`, `autoStart: true`,
`runs: 10`, `limitSeconds: 180`, `seed: 12345`, and a unique `batchId`. Omitted fields
keep their defaults. Setting `mode: "off"` on a later launch disables the tools.

## Validation commands

Use the Unity version in `ProjectSettings/ProjectVersion.txt`:

- `-batchmode -executeMethod CandyCruisers.Editor.GameplayChecks.Run -quit`
  runs the full existing gameplay checks plus recorder/calibration checks.
- `-batchmode -executeMethod CandyCruisers.Editor.RuntimeGameplayChecks.Run`
  runs the existing live gameplay suite and exits when finished.
- `-batchmode -executeMethod CandyCruisers.Editor.PlaytestRuntimeChecks.RunDesktop`
  and `RunPhone` exercise real two-seed batches, pause handling, file output,
  report creation and leaderboard/unlock isolation. `RunPhone` uses phone
  dimensions in the Editor; it does not establish physical-device behavior.
