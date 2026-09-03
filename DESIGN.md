# Design

The GridSense engineering console: the pre-session grid screen, the in-race overlay, and the
pit wall. Recorded from the built surfaces, not from intentions.

## Two rules everything obeys

**1. Command versus actual.** Anything a model recommends is drawn in Command magenta beside what
the driver actually did. Plain ink is always the actual. This is never inverted, and magenta is
never used for anything else — a physics heuristic baseline is drawn in `Ink` and labelled
"BASELINE", because calling it a target would claim a model produced it.

Where no model term exists (lap time, sector splits), the comparison is simply absent rather than
manufactured, and the absence is stated in the label.

**2. Enclosure discipline.** A rule appears only where two coordinate systems meet. Panels do not
get borders for being panels. On the in-race overlay exactly one panel draws a rule under its
title — the Track 1 command strip, the one place a command reading sits against an actual reading —
and that rule is Command magenta.

## Tone ramp

Six neutrals. There are no other greys anywhere in the interface.

| Token | Value | Use |
|---|---|---|
| `Ground` | `#07090B` | the void behind everything |
| `Panel` | `#0E1217` | a readable plate over the 3D scene |
| `Rule` | `#212A34` | hairlines, axis lines, bar tracks |
| `InkDim` | `#7C8A98` | labels, units, inactive (5.35:1 on Panel) |
| `Ink` | `#AEBAC5` | body values |
| `InkHi` | `#EAF0F5` | the actual reading, headlines |

`InkDim` measures 4.11:1 on a `Rule` fill, so it is not used for text on filled chips; those use
`Ink`.

## Hue, reserved entirely for meaning

| Token | Value | Means |
|---|---|---|
| `Command` | `#FF2D9B` | a model's recommendation. Nothing else, ever |
| `Energy` | `#35C8FF` | the ERS / electrical channel |
| `Ok` | `#2FD37A` | within limits, model ready |
| `Caution` | `#F0A62E` | approaching a limit, heuristic fallback |
| `Warning` | `#FF4A4A` | past a limit |
| `SectorBest` / `SectorPersonal` / `SectorSlower` | `#B44BFF` / `#2FD37A` / `#F0C93A` | F1 timing convention |
| `Sector1` / `Sector2` / `Sector3` | `#4FD6C0` / `#F0C93A` / `#E86AB4` | position along the lap |
| `CompoundSoft` / `Medium` / `Hard` | `#FF4A4A` / `#F0C93A` / `#EAF0F5` | real sidewall codes |

Track-position hues are deliberately distinct from `Energy` and `SectorBest`: an earlier build
emitted those two exactly, so cyan meant ERS on one panel and sector 1 on another.

## Type

Unity's built-in sans is the only face in the project. Hierarchy is carried by size, weight, case
and colour instead of by a display face. Seven steps, in design units at 1080p:

`Micro 9` · `Label 11 bold` · `Body 13` · `Value 18 bold` · `Metric 30 bold` · `Hero 46 bold` ·
`Display 64 bold`

Labels are uppercase and dim; values carry the weight. Every style clips rather than wraps, so
prose of unknown length (a model's rationale string) goes through `Theme.Wrapped`.

## Scale

Everything derives from `Theme.U(designPixels)`, where the scale is `Screen.height / 1080`, clamped
to **0.89–2.60**. The floor exists so the smallest face never falls below 8px: below that, text
would stop shrinking while its container carried on, and clip. Verified collision-free from
1024×768 to 4K.

## Composition

**Starting screen** — full-bleed over the live scene. Left 62% is the circuit plate: the real
centreline outline drawn from `TrackVisualBuilder.GenerateCenterline`, sector-coloured, with corner
ticks at detected corners, an elevation profile beneath, and four measured facts under that (lap
distance, corner count, elevation range, tightest radius — all computed from the geometry). Right
38% is the session sheet: circuit rows, compound rows, a session block, and the launch action. The
session block degrades adaptively on short screens; the model-readiness rows survive longest.

**In-race overlay** — the centre of the screen belongs to the circuit. Telemetry occupies four
corner strips plus a left tyre/ERS rail, leaving a clear band of roughly 1432×818 at 1080p. Panels
carry a `Rank` (Lead / Primary / Reference) driving plate alpha and title ink.

**Pit wall** — three columns: stint, Track 3 degradation isolation, Track 1 energy deployment.
Signed attribution bars grow from a drawn centre axis. The 95% confidence band is drawn at the
width the model reports and is never narrowed for looks.

## Motion

One clock. Numeric readouts damp toward their target rather than snapping (`Theme.Damped`). The
circuit outline sweeps from the start/finish line over ~620 ms on selection, over a dim ghost of
the full shape so the circuit is legible immediately. Nothing decorative moves.

## Data honesty

The rule the interface is built around: **a readout may never look more authoritative than its
source.**

- Model state is three-valued and printed inline in the row it governs: `LIVE` (trained ONNX
  loaded), `HEURISTIC` (runner present, model absent — it still emits output), `OFFLINE` (no
  runner). "PPO · LIVE" is only shown when `IsModelLoaded` is true.
- `CarPhysicsController.OptimalBatteryEnergyPct` is a physics heuristic and is always labelled one.
- Compound grip and wear read from the simulation: grip from
  `TyreCompoundProfile.BasePeakFriction` (1.28/1.15/1.02 → 100/90/80), wear from
  `CarPhysicsController.WearMultiplierFor` (2.20/1.25/0.70). Note that
  `TyreCompoundProfile.BaseWearRateMultiplier` holds different values and is read by nothing in the
  physics — do not display it.
- There are no placeholder times. Sector bests start empty and read `--.---` until set.

## Drawing

IMGUI surfaces build from `Theme` primitives only: `Fill`, `HRule`, `VRule`, `Plate`, `Text`,
`Wrapped`, `Bar`, `Chevron`, `Tick`. Glyphs are drawn, never Unicode or emoji — an earlier build
used 🏎️ ⚡ 🔋 ➔ ✓, which render as tofu in the built-in font. Rendered strings stay inside
Latin-1 for the same reason; the minus sign in a signed delta is ASCII, because a tofu there
inverts the reading.

Style variants are cached (`Theme.Align` / `Styled` / `Tint`). `OnGUI` runs at least twice a frame,
so allocating a `GUIStyle` per readout is real cost on the integrated-graphics target.

## Known limits

- No font asset exists. A condensed face dropped into `Theme.Style()` would lift the whole system.
- Bars carry no tick rulers. The elevation strip is the only place a measured axis appears, and it
  is the pattern the four bars should eventually follow.
- Nothing here has been rendered. No Unity editor or .NET SDK was available when this was written,
  so every value above is verified from source and arithmetic, never from a frame.
