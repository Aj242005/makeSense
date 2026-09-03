---
version: 1
slug: "assets-scripts-ui-f1telemetryhud-cs"
primary_target: "Assets/Scripts/UI/F1TelemetryHUD.cs"
related_targets: ["Assets/Scripts/UI/PitWallDashboardController.cs","Assets/UI/PitWallDashboard.uxml","Assets/UI/PitWallDashboard.uss"]
---

Scope: the two surfaces the user asked for — the pre-session starting screen and the telemetry
surfaces (in-race HUD plus the full-screen pit wall). Visitor mode: Operate throughout; the starting
screen carries one Persuade beat (commit to a session) but its job is still selection.

Audience and task: one person who drives the stint from the keyboard and then reads what the models
made of it. Pre-session they pick a circuit and a compound. In-session they read at a glance while
steering. Stationary they interrogate the attribution.

Constraints: Unity built-in font only (no font assets exist). Integrated graphics, no discrete GPU —
UI cost is charged against the same frame budget. No fabricated data: hardcoded sector bests must go,
and the physics heuristic baseline may never be labelled as model output.

## Direction contract

THESIS: An engineering console where every comparable quantity is shown twice — what the driver did
and what the model commanded — so the coupling between tyre state and energy is readable at a glance.
Refuses the category default: seven identically boxed cards with the AI's answer smallest.

OWN-WORLD: Six-step cool neutral ramp on near-black; hue reserved entirely for meaning — magenta for
commanded, warm white for actual, F1 sector purple/green/yellow, compound red/yellow/white, amber and
red for caution. Hairline rules only where two coordinate systems meet, never as panel decoration.
Unity sans at three sizes per zone; labels small, uppercase and dim; numerals large, bold and
right-aligned in fixed-width columns. Everything scales from screen height, never fixed pixels.

RAISE (glass cockpit, declined challenger's neighbour): command-versus-actual is the organising
relationship, not a feature — magenta bug against white needle, everywhere.
RAISE (cracktro, declined): enclosure discipline — hierarchy from rule weight, ink density and
position; a boundary only where coordinate systems meet.
RAISE (Game Boy four-shade field, declined): a hard-enumerated tone ramp, so no arbitrary greys exist
and hue never leaks into decoration.
RAISE (phosphor terminal, declined): state prints itself inline in the row it governs — a model that
is not running says MODEL OFFLINE in place of its own numbers, never a badge in a corner.

STORY: The visitor picks a circuit and sees its real geometry draw itself; drives; and reads, both at
speed and afterwards, where their lap time actually went and what the model would have done instead.

FIRST VIEWPORT: Starting screen, full-bleed over the live scene. Left 62% is the circuit plate — the
actual centreline outline at large scale, sector-coloured, start/finish ticked, corner ticks at
detected corners, with a real elevation profile strip beneath it and measured facts under that.
Right 38% is the session sheet: circuit rows (selected row inverts), then three compound rows with
real grip and wear multipliers, then the launch action at the bottom right. Control legend as a
key/value grid along the foot. Centre of the in-race HUD stays empty; telemetry lives in four corner
strips plus a left tyre/ERS rail.

SIGNATURE INTERACTION: the circuit plate draws itself — on selection the outline sweeps from the
start/finish line around the lap over ~600 ms, sector by sector, corner ticks landing as the sweep
passes them, and the world behind the scrim rebuilds to the chosen circuit.

MOTION GRAMMAR: one shared clock. Numeric readouts damp toward their target rather than snapping;
the sweep is the only entrance animation; the rev strip and the car blip are the only continuous
motion. Nothing decorative moves.

FORM: Engineering console — candidate 2 of my ordered grounded list, user-pinned over the roll's
assignment (candidate 6, the motorsport setup sheet). Seed key addf936b, mode operate, kind pick.
Build path code-led: no image generation on this machine, so the comp round is skipped by contract.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the
verdict, DESIGN.md, and every shipping raster carrying its provenance.
