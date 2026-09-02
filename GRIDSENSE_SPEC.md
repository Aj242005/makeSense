# GridSense — Full Build Prompt (Consolidated, Post-Review)

You are building GridSense, an AI Motorsport Intelligence system in Unity, per the original build spec below (sections 0-8). Before writing any code, read this entire preamble — it encodes hard lessons from a prior full build cycle of this exact project. Violating any rule here is not a minor slip; each one caused a real, previously-shipped bug.

## Non-negotiable process rules

1. Never invent section/step names or content. Every "Section X" or "Step X.Y" you reference must be pulled verbatim from the spec text actually provided to you. If you don't have the text for a section, STOP and ask for it — do not infer, do not reconstruct from a generic mental model of what a game engine project "usually" has next (this caused a fully invented "Audio System & Spatial Engine Audio" step and an invented "Integration, Testing & Validation" section in the prior build; audio is explicitly OUT OF SCOPE per Section 3.8 and Section 7 — never revisit it).
2. Never fabricate a citation. If you're stating a design rationale that is your own judgment call, say so plainly ("this is my own design decision, not a spec requirement"). Do not invent a quote and attribute it to the spec. This happened twice previously (a fake Section 8 quote, an invented CarState justification) and is treated as a serious violation.
3. Never report a fix as verified based on config/code inspection alone. "The material is assigned in the inspector" or "the transform values look correct" is not verification. Verification means: you ran it, you watched the actual output (rendered image, gameplay behavior, log of real values changing over time), and you are describing what you actually observed. This project previously had THREE separate incidents of a "verified" render being visually wrong when actually opened (grayscale Red Bull Ring, bright-red Yas Marina, monochrome-blue Bahrain), and one incident of a "verified, stable on track" car that had zero track colliders and fell into the void — caught only because the user manually ran the build. Every visual or interactive claim must include actual evidence a human can independently check.
4. Never test player input by scripting values directly into the control layer. A test that sets throttle = 1.0 in code proves the physics work; it does not prove keyboard input reaches that code. If verifying input handling, either demonstrate an actual OS-level keypress producing a logged response, or explicitly tell the user "I cannot simulate real input myself — please test this directly and report back."
5. When you find a real bug while fixing something else, fix it and flag it — don't silently patch around it or ignore it because it's out of the current step's scope, but also don't expand scope beyond what's needed to fix it.
6. Any addition not explicitly asked for (new field, new system, new file) must be flagged in your report, even if it seems obviously useful. Don't let small unprompted extras accumulate silently.
7. Stay strictly within each section's IN SCOPE list. If you're about to build something from an OUT OF SCOPE list "because it's more accurate/complete," stop and flag it instead.

## Specific technical landmines from the prior build — check for these proactively

- Unity's Input System: check ProjectSettings/ProjectSettings.asset -> activeInputHandler before writing any input code. If it's set to New Input System only (1) but you write legacy Input.GetKey() calls (or vice versa), input will silently fail with zero errors and zero response. Either commit fully to one system and set the project setting to match, or write dual-handler polling from the start. Set runInBackground: 1 so a build doesn't lose input on focus edge cases.
- Colliders are not automatic. Visual meshes (MeshRenderer + MeshFilter) do NOT provide physics collision. Every track surface, kerb, and any geometry a Rigidbody-driven car needs to rest on must have an explicit, serialized MeshCollider — verify this is actually saved into the scene file, not just attached transiently during an editor session (this exact gap caused the car to fall through the floor into the void in the prior build; it went undetected because verification never included pressing Play and watching for more than a static frame).
- Substring-based material/name classifiers are unreliable. A classifier matching "red" inside "Acuredbullring" misclassified 92.5% of one circuit. A classifier matching "rail" inside "tentrail" misclassified an entire circuit's road surface as barrier metal. Use word-boundary regex matching at minimum, and prefer geometric/topological classification (bounding dimensions, normal orientation, UV aspect ratio) over name-substring matching for any circuit with generic/indexed submesh names — verify per-circuit rather than assuming one method works uniformly across all assets.
- Legacy scan artifacts (sky domes / horizon shells): at least 2 of the 5 circuit source scans contain massive inverted hemispherical "sky dome" or horizon-card meshes (1-3km scale) left over from the original scan capture. If auto-classified into an opaque material, these occlude high-altitude/aerial camera views entirely. Explicitly search for and disable non-drivable, non-structural enclosing geometry before trusting any aerial-angle verification render.
- Prefab instance material overrides can silently fail to persist. If you modify MeshRenderer.sharedMaterial on a prefab instance in a scene via script without recording the property modification or unpacking the prefab, Unity can discard the override on reload. For circuit/car assets where materials must be permanent, unpack to a plain scene GameObject or properly record prefab overrides — verify materials survive a scene reload before reporting done.
- Reflection probes can create runaway color-tinting feedback loops. A Realtime reflection probe capturing a strongly-colored object (e.g., a blue car) at close range can flood the whole scene with that color via specular/indirect bounce. Prefer baked reflection probes for static content; be cautious with Realtime probes near large colored objects.
- Deferred rendering can fail silently on integrated graphics in some configurations, producing desaturated/broken G-buffer output. If colors look wrong project-wide despite correct material assignments, check whether Forward rendering resolves it before chasing material-level causes.
- Isolate visual-mesh transform fixes from physics/collision transforms. If you rotate or reposition a visual mesh to fix an orientation bug (e.g., a car model imported upside down), you must separately verify the Rigidbody, WheelColliders, and any collision components are still correctly aligned — fixing only the visual layer while leaving physics misaligned produces a car that looks right but behaves wrong (falls through floor, etc.).
- Textures: the confirmed source glTF files had zero embedded texture references at the start of the prior build. Before assuming textureless, explicitly inspect the raw .gltf JSON for images[]/textures[] entries and relative file paths — do not assume based on the FBX conversion alone, since Blender's glTF import may not carry forward external texture references if they weren't co-located with the .gltf file at import time. If textures are added later, separately verify UV coordinates survived the Blender-to-FBX conversion pipeline — textures are useless without valid UVs.

## Reward-function / RL-specific lessons (relevant if retraining Track 1)

- Keep per-step/per-substep reward terms bounded (roughly [-1, +1] scale). An unbounded delayed penalty (e.g., -10/substep triggered by a distant cliff condition) will blow out PPO's critic (explained_variance collapsing toward 0, value_loss exploding) and produce a policy that looks like it's learning but is actually degrading — watch explained_variance as the real health signal, not just ep_rew_mean.
- Avoid hard reward cliffs for continuous quantities (e.g., battery SoC) — use a continuous tracking/gradient term against a target trajectory instead, or the policy will learn brittle bang-bang behavior at the cliff boundary rather than genuine situational strategy.
- Always run a short benchmark (10-50k steps) and report real measured throughput before committing to a full training run — do not trust theoretical vectorized-environment throughput estimates; real PPO wall-clock time includes rollout collection and gradient updates, not just environment stepping.
- Any physics constant used in a matched Python training environment must be pulled directly from the actual C# source values, not re-derived from the spec's prose description — verify with an explicit numerical parity check (run identical action sequences through both, compare resulting state trajectories) before trusting the training environment matches the real game.

## Process discipline

- Work through the spec section by section (0 -> 8), not the whole document at once. After each section/step, stop and report back — do not proceed automatically to what you assume comes next.
- For each report, explicitly self-check against that section's OUT OF SCOPE list.
- When something you built needs verification I can't fully judge from a text report (visual correctness, gameplay feel, runtime behavior), attach real evidence (render, log, or an honest "I cannot verify this myself, please test directly").
- Always use the actual current date/hardware context I give you (this build targets a laptop with integrated graphics only — no discrete GPU — keep every rendering/performance decision honest about that constraint, including being willing to say if 60fps isn't achievable and 30fps is the real floor).
- Report ANY change to source assets immediately when it happens, not just at a review checkpoint. This includes: replacing a car or circuit model, fetching new assets from any external source (Sketchfab or otherwise), changing texture sources, or any other swap of what geometry/material data the project is actually using. State explicitly which files changed and why, in the same message where you make the change — never let an asset swap happen silently in the background of other work. If a car or circuit model is ever replaced, explicitly re-verify (not assume) that the vehicle controller, WheelColliders, Rigidbody setup, collision setup, and any rig-dependent script references still correctly match the new model's hierarchy before reporting anything built on top of it as working.

## Explicitly excluded from this build, permanently

Engine audio / spatial audio systems of any kind. Do not propose this, do not build it, do not suggest it as a "next step." This is out of scope per the original spec (Section 3.8, Section 7) and was explicitly rejected by name during the prior build cycle. If you find yourself about to suggest an audio-related step, stop — this is the one thing you are guaranteed to get wrong if you don't check.

---

# GridSense — AI Motorsport Intelligence Engine
## Build Prompt for Autonomous Coding Agent (Unity Edition)

You are building GridSense, an AI Motorsport Intelligence system that couples two capabilities into one product:

1. Energy Deployment Intelligence — a real-time decision engine that recommends optimal electrical energy deployment (push / balanced / hold / save) and scores the risk-reward of overtake windows, subject to a finite energy budget and rule compliance.
2. Tyre Degradation Isolation — a predictive model that strips fuel load, traffic, and track-evolution noise out of lap-time data to produce a clean tyre degradation curve, validated post-session against real pace.

The defining feature of this product, and the thing every part of your implementation should reinforce, is that these two capabilities are coupled, not separate. Tyre state is a live input to the energy-deployment risk calculation; energy-deployment aggressiveness (including braking aggressiveness, see Section 4) is a live input to the tyre wear forecast. They share one "car state" object. Do not build these as two independent systems bolted together at the UI layer — the coupling must be structural.

This build targets Unity, not a web/Electron stack — chosen specifically for native rendering performance against the ultra-high-quality full-scale circuit scans, access to mature vehicle-physics tooling, and in-process ML inference with zero IPC latency. Target platform: Windows standalone build only — do not spend time on other platform builds.

---

## 0. Assets — sourcing and inspection

You do not have pre-placed asset folders this time. You need to source the 3D models yourself first, then inspect them, before anything else.

**Asset sourcing**: Fetch the required models directly from Sketchfab — the F1 car and the 5 circuits (Bahrain International Circuit, Red Bull Ring, Shanghai International Circuit, Suzuka Circuit, Yas Marina Circuit). Use whatever approach and tooling you judge best for searching, downloading, and extracting Sketchfab models programmatically (API access, download scripts, whatever fits) — this is your call on implementation, but the outcome must be:
- Real, complete circuit and car models actually downloaded and present in the project, not placeholders.
- Prefer models that include textures where available, since we want textured, visually complete assets this time rather than the flat-color-material fallback used previously. Search deliberately for options with textures/materials included, not just the first result.
- Report back exactly which Sketchfab model (name, author/source, license) you used for each of the 6 assets (car + 5 circuits), so asset provenance and licensing are clear and traceable. Do not proceed silently — this report is mandatory before moving to import/inspection.
- If a asset source or model is later replaced, swapped, or re-fetched for any reason during the build, report that immediately in the same message, per the Process discipline rule below — this is not optional and has caused real confusion in a prior build of this project when it wasn't done.

**Once sourced**, proceed with inspection exactly as follows: identify the actual file formats and polygon/texture density present. Report actual triangle counts and texture sizes/status (present or absent, and if present, resolution and format) back before proceeding, since this determines how aggressive the optimization pass in Section 6 needs to be, and whether Section 6's texture-budget guidance or its flat-material fallback guidance applies.

If the downloaded format is glTF: convert to FBX via Blender, not the glTFast package — Blender imports glTF natively and exports FBX, which Unity supports natively with zero package dependency. This sidesteps package-registry compatibility issues on newer Unity Editor versions and lines up with the Blender-based decimation pass already required in Section 6, so it's one tool doing both jobs rather than two separate dependencies. If the downloaded format is already FBX or another Unity-native format, state that and adjust the pipeline accordingly rather than forcing an unnecessary conversion step.

Do not treat the raw imported/converted asset as final: it feeds the LOD/optimization pipeline in Section 6, not the runtime scene directly — the bottleneck at high source density is GPU/VRAM throughput, not the import format. Preserve original source files; keep converted/import-ready assets in Assets/Environments/<circuit-name>/. Normalize scale between the car model and each circuit on import — mismatched scale is a common and immediately visible bug, catch it before building anything on top. Different Sketchfab sources may use wildly different scale conventions between models — check each one individually, do not assume consistency across sources the way a single unified scan dataset might have had.

For each circuit, produce a track metadata asset (a Unity ScriptableObject, not just a JSON file, so it's usable directly by the physics/AI systems) containing: total length, an ordered array of corner definitions (apex position, corner number, severity), sector boundaries (3 sectors, standard convention), and a start/finish line position. Derive this from the mesh geometry (centerline extraction) where possible; where not reliable, use public reference data for these five real circuits (corner counts and layout are public information, not proprietary telemetry) and flag clearly which values came from which source.

---

## 1. Tech stack

- Engine: Unity (LTS version), C#. Windows standalone build only.
- Render pipeline: URP (Universal Render Pipeline) — locked, not conditional. Confirmed target hardware is a laptop CPU (H-series) with no discrete GPU, integrated graphics only. HDRP is not viable on this hardware; do not attempt it. Get the best achievable visual result within URP by leaning hard on the optimization techniques in Section 6 (baked lighting, aggressive LOD, capped texture resolution) rather than by reaching for a heavier render pipeline the hardware can't sustain.
- Vehicle physics foundation: do not hand-roll rigid-body integration and wheel/tire plumbing from scratch — start from Unity's built-in WheelCollider system, or better, a proven vehicle-physics asset (Vehicle Physics Pro or RCC — Realistic Car Controller — are the standard choices in the Unity ecosystem) as the base rigid-body/suspension/tire-contact layer. Your actual engineering effort goes into layering the custom tire thermal/wear model, the energy/regen system, and the AI coupling on top of that foundation (Section 3) — not into re-deriving basic vehicle dynamics plumbing that already exists and is well-tested.
- ML training: stays in Python, offline, not in Unity. fastf1 for real F1 telemetry/timing/stint data ingestion, pygam/interpret (EBM) for the interpretable tyre degradation model, PyTorch + stable-baselines3 for the RL energy-deployment policy. Train offline, export trained models to ONNX.
- ML inference at runtime: load the exported ONNX models directly inside Unity via Unity Sentis (Unity's in-engine neural network inference runtime, formerly Barracuda) — this runs inference in-process, in the same frame loop as the physics simulation, with no IPC, no local server, no network hop. This is the actual mechanism that delivers "highest response time" for the decision engine specifically.
- Dashboard / pit-wall UI: build inside Unity using UI Toolkit (Unity's modern retained-mode UI system — its UXML/USS markup is conceptually close to HTML/CSS) rather than legacy uGUI. For charts (tyre degradation curve, energy timeline), either build simple custom line/bar renderers in UI Toolkit (not hard for the chart types you need) or use a lightweight Unity charting asset if one fits — do not attempt to embed an actual web view/React app inside the Unity build; that reintroduces the exact overhead this switch was meant to remove.
- Local data persistence: for session/race history (not on the real-time critical path), SQLite via a Unity-compatible package is sufficient and keeps everything self-contained in one desktop app with no external service dependency. Postgres/Redis are not needed once the backend is no longer a live service — drop them.

---

## 2. System architecture

[Offline, Python]                          [Runtime, Unity/C#, single process]
 fastf1 ingestion -> training ->               Physics sim (fixed timestep)
 ONNX export  -----------------> Sentis <-------- CarState (shared struct)
                                       |            |
                                       +-> Track3/Track1 inference, in-process
                                                    |
                                          UI Toolkit pit-wall dashboard
                                          (reads CarState + inference output
                                           every frame, no network/IPC hop)

Everything at runtime — physics, both AI models' inference, and the dashboard — lives in one Unity process. The only external boundary is the offline training pipeline, which runs once (or periodically, as you retrain) and produces ONNX files consumed at startup.

CarState is the shared struct both models read from and write to, and the dashboard renders from. Define it once as a C# struct/class, single source of truth:

public struct CarState {
    public int Lap;
    public int Sector;
    public float DistanceIntoLapM;
    public TyreCompound Compound;         // enum: Soft, Medium, Hard
    public float TyreWearPct;             // 0-100, from degradation model
    public float TyreTempC;
    public float TyreWearRateCurrent;     // instantaneous slope, feeds energy model's risk calc
    public float EnergyRemainingPct;
    public EnergyMode DeploymentMode;     // enum: Push, Balanced, Hold, Save
    public BrakingAggressiveness Braking; // enum: Normal, Aggressive — affects regen gain, lockup/brake-fade risk
    public float FuelLoadKg;
    public float GapAheadS;
    public bool HasGapAhead;
    public float GapBehindS;
    public bool HasGapBehind;
    public bool DirtyAir;                 // true if within defined gap of car ahead
    public float TrackEvolutionFactor;    // session-progression grip multiplier
}

Note: the float? (Nullable<float>) fields above (GapAheadS, GapBehindS) will not serialize through Unity's Inspector/YAML serializer even inside a [Serializable] struct. Use a plain float paired with an explicit bool (e.g. GapAheadS + HasGapAhead) instead, from the start.

---

## 3. Physics engine — build exactly this, nothing more

This scope is deliberately bounded, layered on top of the vehicle-physics foundation from Section 1. Everything IN SCOPE below is required, built as custom systems on top of the base asset/WheelCollider plumbing. Everything OUT OF SCOPE is explicitly forbidden — if you find yourself implementing something from the out-of-scope list "because it's more accurate," stop and flag it instead; it's scope creep.

Guiding principle unchanged from the original spec: this is a decision-realistic physics engine — it exists to produce believable lap times, tyre wear, and energy consumption for the AI models to reason about. Unity's native rendering and physics headroom raises how good this can look and feel, but the scope boundary itself doesn't move just because the engine is more capable.

### 3.1 Tyre model — IN SCOPE
- Combined-slip Pacejka "Magic Formula" for longitudinal/lateral force vs. slip ratio/slip angle — if your chosen vehicle-physics asset already implements this (several do), verify its implementation and coefficient set rather than assuming; if it uses a simpler model, implement Pacejka yourself on top of the asset's wheel/contact data.
- Thermal model: surface/carcass temp as a function of slip energy dissipated, track temp, cooling rate; grip is a function of distance from optimal temp window.
- Wear model: cumulative wear driven by slip energy — keep the true wear value available internally for validation even though the AI model shouldn't get to see it directly (it should have to infer it, same as the real challenge).
- Three compounds (soft/medium/hard) with distinct base grip, wear rate, operating temp window.
- Tyre pressure as a simple secondary multiplier on contact patch/grip.

OUT OF SCOPE: FEA contact-patch deformation, rubber compound chemistry/blistering simulated at material level — model these as effects on the grip curve, full stop.

### 3.2 Vehicle dynamics — IN SCOPE
- Rigid body with load transfer (longitudinal under braking/acceleration, lateral under cornering), computed from CG height/wheelbase/track width — largely provided by the Section 1 foundation asset; verify and tune rather than reimplement.
- Per-corner spring/damper affecting how fast load transfer settles; anti-roll bar stiffness as a tunable parameter.
- Ride height as an input to the aero model below.

OUT OF SCOPE: full chassis flex/torsional rigidity modeling, detailed suspension kinematics beyond what the foundation asset provides, structural failure/breakage.

### 3.3 Aerodynamics — IN SCOPE
- Downforce and drag as speed-dependent curves (Unity AnimationCurve is a natural fit for authoring these).
- DRS as a discrete downforce/drag state toggle.
- Dirty air model (feeds DirtyAir in CarState, load-bearing for Track 1 overtake logic): a following car within a defined following-distance loses a defined percentage of downforce. Simple distance-based falloff is sufficient.
- Aero balance (front/rear split) as a tunable setup parameter.

OUT OF SCOPE: CFD-derived 3D aero maps, ground-effect tunnel simulation, real wake field simulation.

### 3.4 Powertrain / energy — IN SCOPE (Track 1 core)
- ICE power curve + a single simplified hybrid energy pool with deployment modes (push/balanced/hold/save), each with a defined power delta and draw rate.
- Battery/energy state depleting across a stint against a defined per-race budget — Formula-E-style explicit finite energy, not F1's non-public proprietary hybrid logic (state this framing choice in the README).
- Regeneration under braking, deliberately modeled as a genuine energy-gain lever: harder/later braking recovers more energy per event. Paired with its cost — braking harder/later raises lockup probability (spikes localized tyre wear/flat-spot risk in the tyre model) and raises brake temperature faster (feeds Section 3.5's thermal model, pushing toward the fade window sooner). The BrakingAggressiveness field in CarState must be a first-class, visible signal, not buried logic.
- Fuel mass loss over a stint feeding back into vehicle weight -> tyre load -> lap time.

OUT OF SCOPE: real MGU-H/turbo thermal recovery modeling, ICE internal combustion thermodynamics.

### 3.5 Brakes — IN SCOPE (minimal)
Simple thermal model: temperature rises under use, cools on straights, stopping power fades outside an operating window. Use a physically realistic duct-cooling coefficient — verify the cooling time constant against real carbon-carbon brake behavior (discs should hold heat for multiple seconds under normal use, not return to ambient within ~1 second of releasing the pedal).

OUT OF SCOPE: disc/pad material wear, brake failure.

### 3.6 Track & environment — IN SCOPE
- Per-corner grip coefficient, elevation, banking from the track metadata ScriptableObject (Section 0).
- Track evolution: session-progression grip multiplier — one of the three confounds the Track 3 challenge explicitly asks you to isolate. Implement it as a genuine confound the tyre model has to work to strip out, not something exposed directly to it.
- Basic weather: a single grip multiplier; rain-tyre compound state optional, not required for MVP.

OUT OF SCOPE: rain radar simulation, curb contact meshes, surface micro-texture beyond what HDRP/URP materials give you visually for free.

### 3.7 Opponent traffic — IN SCOPE (minimal, needed for realism)
Rule-based opponent cars with target lap times and basic defend/block logic — enough to generate real dirty-air and overtake-window scenarios. Use generic car/driver identifiers only — do not use real F1 drivers' actual car numbers or names, even as flavor.

OUT OF SCOPE: opponents with equally deep AI reasoning as the player-facing engine.

### 3.8 Simulation core — IN SCOPE
Fixed-timestep physics via Unity's FixedUpdate — this is native to the engine and already decoupled from render framerate by default, which is a genuine advantage over the manual decoupling the web/Electron version required. Both AI models' Sentis inference calls should run on the same fixed-timestep cadence, not gated behind render frame completion.

OUT OF SCOPE: multiplayer netcode/rollback, VR support, force-feedback haptics, engine audio synthesis, damage/failure simulation beyond tyres+brakes, any official F1 team names/liveries/driver likenesses or FIA regulatory text reproduced verbatim — use generic names/original liveries; reference real physical rules (energy limits, compound concepts) without protected branding.

---

## 4. Track 3 — Tyre Degradation Isolation Model

- Ingest real stint data via fastf1 (Python, offline) for at least 3-5 real races.
- Feature set: lap-in-stint, fuel-corrected pace, gap-to-car-ahead, session-progression index, compound, track temp.
- Model: pygam GAM or an Explainable Boosting Machine — prioritize interpretability so you can show, per feature, how much of the observed lap-time delta is attributable to fuel vs. traffic vs. evolution vs. true tyre wear. The residual after regressing out confounds is your isolated degradation curve.
- Export to ONNX; load via Sentis; run inference every fixed-timestep tick against live CarState data from the Unity physics simulation.
- Validation loop, first-class dashboard feature: hold out real race-day stints, compare predicted degradation curve against real pace with an error band. Additionally validate against your own physics engine's ground-truth wear signal (Section 3.1) — a known-correct answer real F1 data doesn't give you. Use both. Present error bands honestly — do not visually imply tighter confidence than the model actually has.
- Wire this dashboard into the actual running scene, not just as an offline artifact — confirm it's visible and toggleable (with a stated key/method) in the built, playable application, and verify this by actually running the build, not by confirming the code exists.

---

## 5. Track 1 — Energy Deployment Engine

- Train offline (Python, stable-baselines3 PPO or contextual bandit) inside a training harness that mirrors the Unity physics environment closely enough that the learned policy transfers — either by exporting the Unity physics behavior into a matched Python simulation for training, or by using Unity's ML-Agents toolkit to train directly against the real Unity environment (ML-Agents is the more faithful option if training-environment fidelity turns out to matter; flag which you used and why).
- Action space: deployment mode and braking aggressiveness at upcoming braking zones (per Section 3.4) — the policy should be free to trade one energy-gain lever against the other, not choose in isolation.
- Reward function: net position/time gained, minus energy-budget violation penalty, minus a risk term for failed overtake attempts, minus additional tyre wear from aggressive deployment or aggressive braking (reads TyreWearRateCurrent — the coupling, made concrete), minus a brake-fade risk term. The energy gained from aggressive braking must show up as a genuine reward-positive term so the policy actually weighs it against these costs. See the Reward-function lessons section above before designing this.
- Export to ONNX; run via Sentis at runtime, same fixed-timestep cadence as Track 3.
- Output per tick: recommended deployment mode, braking-aggressiveness recommendation for the next zone, a numeric risk-reward score for any live overtake window, and a short explanation (feature importances, saliency attribution, or equivalent) of why. This explainability output is a required deliverable, not optional polish — verify it is actually wired into CarState and visible/accessible at runtime before considering Section 5 done.
- Re-evaluate every lap or sector tick — model-predictive-control style, not a fixed pre-race plan.

---

## 6. Rendering — the environment pipeline, tuned for integrated graphics

Note: actual triangle counts for these assets, once inspected in Section 0, are likely to be far lower than "hundreds of millions" — expect low hundreds of thousands to low millions per circuit. Right-size all optimization aggressiveness to the real measured numbers, not a worst-case assumption. Textures may or may not exist in the source — verify per Section 0 rather than assuming either way.

Confirmed target hardware is integrated graphics, no discrete GPU — this is the tightest constraint in the whole spec:

- LOD: use Unity's built-in LODGroup component, calibrated to actual measured triangle counts. Protect the drivable track surface and kerbs at 100% fidelity — zero decimation, zero culling on anything the car's wheels contact, at any distance. Use cross-fade to avoid visible popping.
- Instancing: use Unity's GPU instancing for repeated geometry (barriers, curb segments, grandstand modules, trees, marshal posts).
- Occlusion culling: bake it per circuit via Unity's built-in system. Track ribbons should be OccludeeStatic only (never OccluderStatic) — they shouldn't waste compute acting as occluders. Explicitly validate tunnel/underpass/narrow-passage sections for false-positive culling, with real camera-position evidence, not an assumption.
- Lighting — bake everything you can: static lighting for the track environment goes into lightmaps via Unity's baked GI. Real-time shadows only for the car itself. Real-time global illumination is off the table on this hardware. Be cautious with Realtime reflection probes near strongly-colored objects (see landmines section above).
- Texture budget: KTX2/ASTC-compressed textures with a firm resolution cap per LOD tier, if textures exist; if not, use a calibrated flat PBR material palette instead (verify against Section 0's texture findings).
- Post-processing — minimal: skip expensive post effects (heavy bloom, screen-space reflections, high-sample-count ambient occlusion). Use FSR or Unity's built-in upscaling to render at a lower internal resolution and upscale, rather than pushing native resolution on hardware that can't sustain it.
- Adaptive quality: use URP's Dynamic Resolution and quality-tier settings as the fallback if frame time overruns budget (target 60fps / 16.6ms as the floor — be honest in testing about whether even 30fps is a more realistic floor and report that rather than forcing a number that doesn't hold).
- Acceptance target: state explicit numbers in your README once built — sustained fps at a stated resolution measured on the actual target laptop (not a hypothetical reference tier), circuit load time to first drivable frame, and confirmation that physics/AI FixedUpdate tick rate holds steady under render load. Lead with the worst-case circuit's numbers, not the best case. Given the integrated-graphics constraint, treat "does this actually run acceptably on the real target machine" as the top acceptance criterion, above raw visual fidelity.

---

## 7. Non-goals — do not build these under this task

Multi-tenant auth/IAM, real sim-racing platform (iRacing/ACC/rFactor2) telemetry integration, multiplayer, VR, force-feedback hardware support, engine audio, any platform build beyond Windows standalone. These belong to a later phase and are out of scope here.

---

## 8. Definition of done

You are done with this task when: the physics engine (built on the Section 1 vehicle-physics foundation) produces a full simulated race stint with believable lap times and tyre wear; the Track 3 model, trained offline and validated against both simulated ground truth and at least one real fastf1 race, produces an isolated degradation curve with a validation plot, running as live in-engine Sentis inference; the Track 1 model produces live per-tick deployment and braking recommendations with a risk-reward score during a simulated stint, also via in-engine Sentis inference; the UI Toolkit pit-wall dashboard renders all of the above live, reading directly from CarState with no external process involved; at least one of the five real circuits loads at correct scale with the car model, LODs/occlusion culling/baked lighting active; and the whole thing builds and runs as a standalone Windows executable via Unity's Build Settings — no Electron, no local server process, no network dependency at runtime.

Before declaring this done, you must:
- Actually launch the built standalone executable (or describe in detail exactly how the user should do so, and wait for their confirmation) and confirm: the car spawns correctly on the track surface and stays there under gravity/physics for a sustained period; keyboard input actually moves the car (not a scripted test bypassing input); the tyre validation dashboard and energy deployment explainability output are visible and accessible in the running application, not just present as offline artifacts or unwired code.
- Report explicitly on anything from this spec you could not complete or had to simplify further, and why, including the measured fps on the actual target laptop (leading with the worst-case circuit) and what quality trade-offs were needed to hit it.
- Produce a technical completion report (verification matrix against the acceptance criteria above, with evidence, plus honest disclosure of any simplifications/deviations) AND a separate pitch-facing summary document (plain-language innovation narrative, demo-worthy visual artifacts, audience-friendly performance numbers) — every number in both must be traceable to an actual verified test/report from this build, never restated from memory or estimated.
- Prepare the repository for GitHub: a correct Unity+Python .gitignore, a file-size audit flagging anything needing Git LFS, and an honest first-commit message reflecting the real state of the project (including any known exclusions or limitations) rather than an overstated "complete" framing. Do not actually push.
