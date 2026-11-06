# Level map

Edit **`Assets/Resources/LevelMap.json`** in a text editor. This is the only
source for authored level progression. The former Level Progression window,
level ScriptableObjects, Initial Wave Lines and enemiesToDefeat settings have been removed.

Save the file and start a new run. The Unity Editor reads the saved file again
at run start; an in-progress run keeps its loaded settings and reservations.
Desktop and iOS (iPhone Operating System) builds package the same file. Rebuild
an installed game to include file changes.

## Format

The outer `levels` object uses consecutive keys starting at `"1"`. Each key
identifies a level, so there is no redundant `levelNumber` setting. Object key
order does not matter. Levels beyond the last entry reuse the last entry's
settings and complete fleet sequence; their spawn cursors still reset.

```json
{
  "schemaVersion": 3,
  "levels": {
    "1": {
      "backgroundImage": "",
      "possibleSoundtrackTracks": [],
      "fleets": [
        {
          "possibleColors": ["Red", "Blue"],
          "initialFleet": ["r0rr0r"],
          "lineSpawns": ["0r0r0r"]
        },
        {
          "possibleColors": ["Red", "Blue", "Purple"],
          "requiredColors": ["Purple"],
          "initialFleet": ["112211", "221122"],
          "lineSpawns": ["121212", "212121"]
        }
      ]
    }
  }
}
```

Level settings:

| Field | Meaning |
| --- | --- |
| `backgroundImage` | Sprite resource path relative to `Assets/Resources`, without an extension; empty or null means black. |
| `possibleSoundtrackTracks` | Names from Gameplay Music Player's soundtrack registry, without extensions. An empty list permits the full soundtrack. |
| `fleets` | Ordered, nonempty list of independently authored fleet objects. |

Fleet settings:

| Field | Meaning |
| --- | --- |
| `possibleColors` | Nonempty candidate pool for resolving this fleet's numbered groups and opening wildcards. Literal colors must also belong to this pool. Use full color names: Red, Blue, Green, Purple, Yellow, Orange. |
| `requiredColors` | Optional list of colors that must actually appear in this fleet's opening. Omission means `[]`; explicit null is invalid. Required colors must belong to `possibleColors`. |
| `initialFleet` | Nonempty list of opening row strings, from top to bottom; at least one position must contain an enemy. |
| `lineSpawns` | Ordered list of incoming top-row patterns for this fleet. An empty list adds no enemies on descent; existing enemies still descend normally. |

`schemaVersion` is currently 3. Each fleet owns its color pool, opening layout
and incoming lines. Version 2's level-wide `possibleColors`, `requiredColors`,
`fleetSpawns` and `lineSpawns` have been replaced by `fleets` objects. Older
versions are rejected with a version error. `spawnPaletteSource` is not a setting:
using the resolved opening palette is a fixed fleet rule.

All fields above are required except `requiredColors`. Unknown settings, missing
required fields, nonconsecutive level keys, duplicate keys, invalid colors and
impossible spawn patterns are reported as errors. Invalid maps block Play from
starting a run and also block player builds; they do not silently select
procedural defaults. The authoring file is JSON (JavaScript Object Notation).

## Spawn characters and positions

| Character | Meaning |
| --- | --- |
| `r`, `b`, `g`, `p`, `y`, `o` | Red, Blue, Green, Purple, Yellow, Orange; uppercase also works. |
| `0` | Empty cell; does not count as an enemy or player color. |
| `1` through `6` | Random color identifiers; different digits bind to different colors. |
| `*` | Independent random color for this cell. Separate stars may match or differ; they do not share a numbered assignment. |

Each string describes one row's grid positions from left to right. For example,
`["0r0r0r", "b0b0b0"]` places three Reds in the top row's second, fourth and sixth
positions and three Blues in the next row's first, third and fifth positions.
Blanks remain real gaps rather than shifting the remaining enemies together.

Rows may have 1–6 positions, and fleets may have 1–11 rows. All patterns within
one fleet must use the same width. Different fleets may have different widths.
Early levels can use six positions with only three enemies; there is no level-number gate on authored widths. Each fleet's
`initialFleet` list length sets its row count. Put every row in its own string;
`/` is not a spawn character. Completely blank incoming lines are valid; completely blank fleets are not.

Patterns are literal layouts. They are not reordered or regrouped by the old
procedural row generator. To limit matching runs, write those limits into the
patterns, for example `11221`, `22112`, `21122`, `12211`, `12112`, `21221`.

## Round-wide randomness

A round starts when a fresh fleet is planned and ends when that fleet is cleared.
Its allowed palette and all numbered assignments are captured once in an
immutable reservation. Every numbered character in that fleet and every
incoming line uses those assignments, including lines that wrap to the start of
the list. A new round rolls fresh assignments for its next scripted fleet.
A reroll can legitimately choose the same colors as the previous round.

Use `*` in either `initialFleet` or `lineSpawns`. For example, `1**001`
keeps both `1` cells the same color while rolling each star separately. Opening
stars draw from this fleet's `possibleColors`; the resolved cells are captured
in the fleet plan. Optional required colors and fixed incoming colors still
have to appear in the opening, so stars supply them when needed.

Incoming stars roll again on every line spawn, including when the line list
loops. They choose only from the captured opening palette, excluding cleared
colors and an Orange that is already alive. A star leaves a gap if no legal
color remains. Stars also respect the one-Orange limit within a row; an authored
literal or numbered Orange reserves that slot before stars roll.

For `312213`, if `1 = Red`, `2 = Blue`, and `3 = Green`, the resolved row is
`grbbrg`. A later `123123` row in that round resolves to `rbgrbg`.

`possibleColors` is the candidate pool, not the palette used for subsequent
spawns. The planner resolves `initialFleet`, then captures the distinct colors
actually present in those opening cells. Incoming lines and Purple summons must
stay within that captured palette for the entire fleet. For example, an opening
`rrpprr` permits Red and Purple only, even if Blue belongs to `possibleColors`.
A new full fleet captures its own palette; it does not inherit the previous one.

Digits bind to distinct colors. Numbered groups appearing only in incoming lines
must also resolve to opening colors. A literal incoming color can constrain the
random opening choices, but cannot introduce an absent color. Planning chooses
compatible assignments; a fleet is invalid if no assignment satisfies its
opening, incoming lines, optional required colors and the Orange limit. For
example, `rrpprr` followed by `bbbbbb` is invalid, as is an incoming `123123`
requiring three distinct colors after that two-color opening.

Every literal and required color must belong to `possibleColors`. Required
colors must appear in the opening itself, rather than only in incoming lines.
Assignments stay fixed as enemies are cleared; the palette is never recomputed
from survivors or expanded by incoming enemies.

Orange remains limited to one live enemy. Validation chooses digit bindings
that respect that limit. Incoming Orange cells remain blank while an Orange is
already alive. A cleared color stays locked for the current clear streak:
later script cells for that color remain blank, including numbered cells.
Assignments never change to refill those gaps. Purple summons use the same
captured allowed palette and cleared-color locks.

## Fleet completion and incoming lines

Each level starts at `fleets` entry zero. Clear every enemy from that fleet,
including incoming-line enemies and Purple summons, before the next entry can
spawn. Partial clears and high scores never skip entries. Clearing the last
fleet advances exactly one level and starts entry zero of the new level.

- Each full clear completes one `fleets` entry.
- The progress bar shows completed fleets divided by the number of entries.
- A successful descent consumes one entry in the active fleet's `lineSpawns`
  when that list is nonempty.
- The incoming-line cursor loops to zero at the end of its list. Activating a
  fresh fleet starts that fleet's own incoming sequence at entry zero.
- Entering a different level or starting/restarting a run resets both cursors
  and completed-fleet progress.
- Pause and failed descents do not consume line entries.

For three `fleets` entries, the bar moves to one third after the first full
clear and two thirds after the second. The third full clear advances the level
and resets the bar. Fleet entries do not loop within normal progression; the
next level begins when the last entry is cleared. Levels beyond the map repeat
the final level's entire sequence under each new level number.

## Configuration, plans and live state

`LevelCatalog` contains ordered `LevelDefinition` objects. Each level owns its
background, music settings and ordered `FleetDefinition` objects. A fleet
definition owns the candidate colors, optional required colors, opening rows and
incoming lines. No separate data transfer classes are needed by the explicit
loader.

`SpawnScript.Plan` validates a fleet's assignments and produces a `FleetPlan`.
The plan captures resolved cells, opening colors, number bindings and copied
incoming scripts. `FleetPlan.SpawnPalette` is the opening palette itself, so it
cannot drift from the initial cells. `FleetState` exposes the live grid's counts
and tracks cleared-color locks separately from those authored definitions.

## Progress, presentation and shared state

Defeated enemies still award points, and each full clear still awards the fleet
bonus. Neither enemy counts nor score multipliers change level progression.
There is no enemiesToDefeat setting or banked progress toward a future level.
Starting from a later level begins with its first fleet and zero completed fleets.

Background images must be imported as sprites under `Assets/Resources`, for
example `Assets/Resources/Backgrounds/Nebula.png` is referenced as
`"Backgrounds/Nebula"`. Images retain their proportions and crop to cover the
screen on desktop and phone layouts. Missing sprite paths are reported as
errors. The main menu stays black.

Soundtrack names refer to the existing registry, such as `AnotherJoe` or
`Contact`. Missing local audio is skipped; no unrelated track substitutes for a
restricted pool. Audio recordings remain local-only resources.

`EnemyGrid.Fleet` remains the shared source of live colors, counts, cleared
rewards and future reservations. The active plan also owns the captured palette,
number bindings, row dimensions and incoming scripts. Player shot weights ignore
gaps and use the actual occupied cells' row depths.

Bottom bars describe colors that have participated in the current fleet, not
all possible colors in the level. Cleared colors retain their slots through the
clear celebration. Incoming lines and summons use only the opening palette.
Incoming reservations cannot change outgoing bar slots; they switch together when the next fleet spawns.

Run **Candy Cruisers > Run Level Map Checks** for focused validation, or
**Candy Cruisers > Run Checks** for the full gameplay suite. These checks cover
parsing, invalid maps, required colors, sparse layouts, numbered bindings,
independent fleet widths and palettes, optional required colors, incoming-line
loops and resets, level/reset behavior, clear locks, full-clear advancement and shared
fleet state. Runtime checks cover desktop and phone layouts.
