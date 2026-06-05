# HECTON-8 Atmosphere, Weather, Thermodynamics, And Macro Environment Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: tides, storms, turbidity, gas pockets, thermodynamics, vents, seismic pulses, celestial cycles, biome atmosphere, environmental hazards, and macro-world environmental proof.

## First-20 Route Hook

- First-20 moment: world load, first exit, swim, and first fair hazard need readable surface/shallow weather, turbidity, heat/gas warnings, and route-changing macro state.
- Route blocker removed: prevents the opening route from using decorative weather or invisible hazards that do not change navigation, survival, sound, or visibility decisions.
- Proof class: STATIC_DOC only; route acceptance still requires gameplay capture, hazard readability proof, and profiler/GC/GPU evidence for runtime fields or presentation changes.

## Prime Law

The environment is a pressure system, not a skybox. HECTON-8 atmosphere and macro-environment systems must change route, visibility, sound, pressure, heat, navigation, creature behavior, or survival risk. Decorative weather, random color shifts, and expensive physical simulation with no player-readable consequence are rejected.

The deep ocean should feel alive through controlled signals: tide, current, silt, heat, gas, seismic shock, sound propagation, and light failure. It should not become a full planet simulator.

## Truth Ownership

Macro environment truth is split:

- `world.md` owns biome placement, route context, environmental landmarks, and authored hazard zones.
- `water.md` owns current, turbidity, silt, wetness, and flooding presentation boundaries.
- `physics.md` owns force, pressure, flooding, and damage consequences.
- `survival.md` owns player physiology response to gas, heat, cold, pressure, radiation, and decompression.
- `ai.md` and `creatures.md` own behavior response to environmental state.
- `rendering.md` owns fog, light shafts, caustics, GI relays, and shader presentation.
- `audio.md` owns muffling, sonar, weather/metal stress sound, and warning mix.

Atmosphere owns macro state fields, cadence, authored environmental events, and proof that every environmental change has a route.

## Macro State Fields

Each environmental field must declare:

- field id;
- owner chunk/biome/sector;
- units;
- range;
- cadence;
- quality scaling;
- affected systems;
- save/persistence route if persistent;
- fallback when data is missing.

Accepted fields include current vector, turbidity, silt density, temperature, gas composition, pressure modifier, seismic intensity, tide offset, light attenuation, storm state, and vent hazard state.

## Weather And Tides

Weather and tides are deterministic directors, not random mood changes.

Rules:

- surface storm state may influence depth turbidity, sound, current, and route timing;
- tides may affect water level, access windows, and cave/shore hazards;
- celestial cycles use cheap periodic functions unless a player-facing route requires more;
- transition must be debounced and readable;
- no per-frame global weather reevaluation;
- no gameplay truth change from presentation-only sky or color grade.

## Thermodynamics And Vents

Heat is a hazard, resource, and evidence source.

Required:

- source id;
- heat output;
- falloff;
- update cadence;
- damage/survival coupling;
- creature/ecology response if any;
- visual/audio/haptic response;
- proof that the player can detect and react.

Thermal vents may use scalar fields, LUTs, local signed distance zones, or low-resolution grids. Full heat diffusion is accepted only when the result affects a player-readable decision and is amortized.

## Gas And Contamination

Gas dynamics must be readable and bounded.

Accepted routes:

- scalar compartment composition;
- partial pressure table;
- low-cadence diffusion;
- authored gas pocket volumes;
- detector/scanner UI;
- audio/visual warning;
- survival channel effect.

Rejected routes:

- particle-level gas simulation;
- invisible poison clouds with no instrument;
- gas changing gameplay without save/owner route;
- runtime per-cell chemistry for background spaces.

## Seismic And Collapse Events

Seismic events must have cause, cadence, and consequence.

Allowed consequences:

- silt burst;
- route blockage;
- sonar distortion;
- creature displacement;
- hull stress;
- rockfall proxy activation;
- black-box record;
- short camera/audio impulse.

The event may be faked through authored animation, physics proxies, shader masks, and audio. It does not need per-rock simulation unless a local player-facing collapse is being inspected.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` scales presentation density, field resolution, fog variation, silt particles, light shafts, audio layers, diagnostic overlays, and noncritical update cadence.

It must not change deterministic event schedule, survival math, route truth, save identity, hazard authority, or network/rollback state layout.

Compact keeps readable hazards, scalar fields, strong warnings, and cheap fog/silt. Middle adds richer local variation. High adds denser environmental response. Ultra adds cinematic macro atmosphere only after route readability and compact proof exist.

## Proof Artifacts

Atmosphere work must provide:

- field manifest;
- owner route per field;
- cadence and budget;
- compact readability capture;
- affected-system list;
- save/load note for persistent changes;
- black-box fields for hazards or fatal events;
- profiler/GC proof for runtime fields;
- GPU proof for fog/silt/lightshaft changes;
- explicit static-only label when no runtime verification ran.

## Rejection Gates

Reject atmosphere work if:

- weather is only a color grade;
- thermodynamics has no player-readable consequence;
- gas hazards are invisible or ownerless;
- per-particle/per-cell simulation is used where scalar state is enough;
- quality tier changes hazard truth;
- fog or silt removes route readability;
- environmental events have no cause or recovery;
- update cadence is per-frame without proof;
- public claims imply live macro simulation without profiler evidence.

## Acceptance Sentence

Atmosphere is accepted only when macro environmental state is owner-routed, deterministic, readable, cheap-first, continuously scalable, and capable of changing player decisions through pressure, heat, gas, visibility, sound, or route consequence.
