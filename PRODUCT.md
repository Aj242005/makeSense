# Product

<!-- impeccable:product-schema 1 -->

## Platform

desktop

Unity 6 (URP 17.4) standalone, Windows x86_64 only, built from `Assets/Scenes/MainSimulation.unity`.
This value is deliberately outside the four-value `web | ios | android | adaptive` enum because none
of them is true: the web HTML/CSS detector has no verdict on this codebase, and `ios.md` / `android.md`
do not apply. Later agents should skip both.

## Users

A race engineer or performance engineer reviewing a stint, and the driver-operator running it. One
person usually plays both parts here: they drive the car from the keyboard, then read what the models
made of the lap. The situation is a single-seat desk session on a laptop, not a pit gantry — so the
in-race surface is read in glances at speed, and the pit-wall surface is read stationary, in depth.

## Product Purpose

GridSense couples two motorsport-intelligence capabilities into one product and makes the coupling
legible:

1. **Energy Deployment Intelligence (Track 1)** — recommends optimal electrical deployment
   (Push / Balanced / Hold / Save) plus a braking-aggressiveness recommendation for the next zone,
   scores the risk-reward of live overtake windows 0-100, and explains why.
2. **Tyre Degradation Isolation (Track 3)** — strips fuel load, traffic/dirty air and track evolution
   out of observed lap-time delta to leave a clean tyre-degradation signal, with a 95% confidence band
   and a residual against the physics engine's ground-truth wear.

Success is that a user can watch the models disagree with what they just did, and understand why.

## Positioning

The two capabilities are **structurally coupled, not bolted together at the UI layer**. Tyre state is a
live input to the energy risk calculation; deployment and braking aggressiveness are live inputs to the
tyre wear forecast. They share one `CarState`. A neighbouring lap-time tool can show a degradation
curve; it cannot show that curve moving because of a deployment decision made two corners ago.

## Operating Context

- Keyboard driving: WASD / arrows, Shift for MGU-K boost, S / Down for regen, F for DRS, R to reset,
  Esc to menu.
- Session shape: a stint of `SimulationCore.TotalStintLaps` laps on one compound, chosen before launch.
- Four circuits: Bahrain 5.412 km / 15 turns, Spa-Francorchamps 7.004 km / 19 turns,
  Monaco 3.337 km / 19 turns, Monza 5.793 km / 11 turns. Circuit and compound are chosen on the
  starting screen; changing circuit rebuilds the world geometry live.
- Target hardware is a laptop with **integrated graphics, no discrete GPU**. This is the tightest
  constraint in the project. 30 fps may be the honest floor; UI cost is charged against the same budget.

## Capabilities and Constraints

- `CarState` is the single source of truth: lap, sector, distance, compound, tyre wear %, tyre temp,
  instantaneous wear rate, energy remaining %, deployment mode, braking mode, fuel kg, gaps ahead and
  behind, dirty-air flag, track-evolution factor.
- `TyreDegradationExplainability` carries the per-lap pace decomposition in seconds
  (fuel / traffic / evolution / true tyre), predicted wear with lower and upper confidence bounds,
  ground-truth wear, and residual error.
- `EnergyDeploymentExplainability` carries the recommended mode and braking, the 0-100 overtake
  risk-reward score and its category, four attribution factors, and a human-readable rationale string.
- Inference is Unity Sentis 2.2.0 over `Assets/Resources/energy_deployment_ppo.onnx` and
  `tyre_degradation_ebm.onnx`, in-process, on the physics fixed-timestep cadence.
- **Known gap being closed by this work:** `SimulationCore`, `Track1InferenceRunner` and
  `Track3InferenceRunner` exist in code but are absent from `MainSimulation.unity`, as is the
  UI Toolkit `PitWallDashboard`. Until wired, no model output reaches any screen.
- No font assets exist in the project. All UI is limited to Unity's built-in sans unless a face is added.
- Engine audio is permanently out of scope per the build spec and was rejected by name.

## Brand Commitments

- Name: **GridSense**. Descriptor in use: "AI Motorsport Intelligence".
- No real F1 team, driver, or sponsor identities. Opponents use generic identifiers
  (`RIVAL-ALPHA`, `CAR-BETA`). Tyre compounds are referred to by their real generic
  names (Soft C3 / Medium C2 / Hard C1).

## Evidence on Hand

- Real, working: full vehicle physics (Pacejka tyre, thermal, aero, powertrain, brakes), four
  procedurally generated circuits with verified closed geometry, both trained ONNX models,
  `TelemetryDatabase` sample logging.
- Real but unwired: `SimulationCore`, both inference runners, `PitWallDashboard.uxml` / `.uss` /
  controller.
- **Not real, must not be presented as real:** `BestS1Time` / `BestS2Time` / `BestS3Time` are
  hardcoded placeholder constants in `F1TelemetryHUD`. There is no rival lap database, no historical
  session archive, and no real-world fastf1 data loaded at runtime. `CarPhysicsController`'s
  `OptimalBatteryEnergyPct` and `OptimalTyreWearPct` are a physics-side heuristic baseline, not model
  output, and may only be labelled as a heuristic.

## Product Principles

1. **The coupling is the product.** Any surface that shows tyre state or energy state must make the
   link between them visible, not present them as two neighbouring readouts.
2. **Attribution over assertion.** Show what the model decomposed the number into, never just the
   number. A recommendation without its reason is not this product's output.
3. **Honest uncertainty.** Confidence bands are drawn at their true width; a model that is not
   running says so rather than degrading to a plausible-looking value.
4. **The task outranks the display.** This is read at 300 km/h and while stationary; legibility and
   glanceability beat expression wherever they conflict.
5. **Every frame is charged to integrated graphics.** UI cost is a real budget line, not free.

## Accessibility & Inclusion

Colour is never the sole carrier of meaning: sector, compound, risk category and model state each
need a text or shape channel alongside their hue. Minimum on-screen text size is set for a laptop
panel read at arm's length, and the in-race surface must stay legible over a bright desert circuit
as well as a dark tunnel.
