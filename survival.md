# HECTON-8 Survival, Damage, And Physiology Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: oxygen, pressure, hull/suit integrity, trauma, clarity, power, temperature, radiation, hypoxia, decompression, death prevention, survival recovery, and survival proof gates.

## First-20 Route Hook

- First-20 moment: swim, first resource trip, first fair hazard, and save/load return need visible oxygen, pressure, depth, damage/faint/death reasons, and recovery consequences.
- Route blocker removed: prevents the opening route from becoming a harmless collection loop or a hidden spreadsheet death timer with no instrument read, counterplay, or recorded physical cause.
- Proof class: STATIC_DOC only; route acceptance still requires formula/unit tests, compact UI readability proof, black-box fatal-state fields, save/load proof for persistent survival state, and profiler/GC evidence for runtime changes.

## Prime Law

Survival is not bars draining on a HUD. Survival is physical truth pressing on every route decision. HECTON-8 rejects invisible spreadsheet depletion, generic health pools, clean death loops, and damage that only spawns VFX.

Every survival system must make the player change behavior: retreat, repair, seal, ration, breathe, listen, power down, move slower, accept risk, or abandon salvage.

## Truth Ownership

Survival owns physiology and survival resources:

- oxygen;
- pressure envelope;
- hull/suit integrity;
- power dependency for life support;
- internal/external temperature;
- clarity/sensor trust;
- hypoxia, gas toxicity, decompression, radiation, wounds, trauma;
- faint, death, recovery, and safe-zone restoration state.

Survival does not own physics forces, render glitch passes, audio playback, UI text, tool hit truth, save layout, or vehicle motion. It publishes stable survival state. Other systems present or persist it through their own routes.

## Core Channels

Survival and damage must not collapse into one health number.

Required channels:

- `INTEGRITY`: hull, suit, body structure, pressure resistance.
- `POWER`: life support, pumps, heating, sensors, tools.
- `CLARITY`: sonar, HUD, vision, scanner trust, cognition noise.
- `OXYGEN`: breathing reserve and stress cost.
- `THERMAL`: internal temperature and insulation state.
- `CONTAMINATION`: radiation, toxin, parasite, biofilm, gas.

Each channel is continuous and clamped. Cross-channel bleed must be explicit, event-driven, and documented.

## Pressure And Oxygen Law

Pressure must be a route constraint, not a random death timer.

Required:

- depth-to-pressure mapping;
- safe envelope;
- warning envelope;
- critical envelope;
- fatal envelope;
- structural fatigue memory;
- repair or mitigation route;
- instrument feedback;
- black-box record.

Oxygen must scale with route cost, panic/stress, suit integrity, leak state, exertion, and life-support power where relevant. It must not become generic stamina. Refill sources must have physical identity and route risk.

Oxygen experience lock: early oxygen should be immediately understandable, broadly comparable to Subnautica's reserve-and-route-planning model. The player has a visible supply, can upgrade tanks, can plan longer trips, and may later use physical extension routes such as hoses/tethers from a ship or base. Ignoring oxygen can kill immediately.

Early survival channel lock: oxygen and pressure are the first active survival pressures. Thermal, gas, contamination, radiation, and complex multi-channel failures may enter later as route complexity grows.

## Damage And Trauma

Damage intake must route through typed damage packets, not direct health mutation.

A damage event must include:

- magnitude;
- source id;
- local point or AUP point;
- damage type flags;
- affected channel;
- depth/pressure context where relevant;
- owner route;
- rejection/fallback behavior.

Presentation may add visor cracks, audio groans, haptics, screen wounds, power flicker, and glitch response. Presentation cannot invent damage truth.

## Temperature, Gas, And Contamination

Temperature, gas toxicity, decompression, and radiation are slow pressure systems. They should build dread through delayed consequence and instrumentation, not surprise punishment.

Rules:

- thermal changes use bounded deterministic formulas;
- gas partial pressure changes require clear source and warning;
- decompression must be tied to ascent behavior and depth history;
- radiation must leave instrument and material evidence;
- contamination must have scan/removal/counterplay route;
- recovery preserves scars unless the specific treatment removes them.

## Death, Faint, And Recovery

Death must be explainable from recorded state. Faint/recovery must not erase physical consequence.

Required:

- one-shot fatal event guard;
- grace or recovery window when design requires readability;
- no chained unavoidable death loops;
- safe-zone or last-stable-state route;
- stress/damage preservation unless a repair route changed it;
- black-box dump for fatal state;
- no coroutine-only death/recovery state machines in gameplay truth.

Ordinary death policy: respawn at a base or safe anchor, drop carried resources where appropriate, preserve core tools unless an authored special case says otherwise, and retain enough evidence to teach why the player died.

## UI And Presentation Boundary

Survival UI is instrument output, not the owner of survival.

HUD/visor/cockpit panels must show the state through:

- oxygen;
- pressure;
- integrity;
- power;
- clarity;
- temperature;
- contamination;
- failure reason;
- confidence/trust where sensors degrade.

UI text must be zero-GC. Warnings must be readable on compact hardware. Glitch visuals must never hide the last usable survival decision.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` scales presentation: warning richness, audio layers, haptics, visor effects, material damage decals, wound decals, cockpit response, and telemetry verbosity.

It must not change survival formulas, death eligibility, recovery state, channel layout, save identity, or resource authority. Compact must preserve warning clarity. Ultra may add sensory brutality, not different survival truth.

## Proof Artifacts

Survival work must provide:

- channel ownership table;
- formula or state transition for every changed resource;
- zero-GC hot-path proof;
- deterministic replay or unit test for formulas;
- compact UI readability proof;
- black-box fields for last 300 frames;
- save/load proof for persistent damage or physiology state;
- death/faint/recovery test;
- profiler proof for runtime changes;
- explicit `PENDING UNITY/PROFILER VERIFICATION` when only static docs changed.

## Rejection Gates

Reject survival work if:

- it is just health, oxygen, or stamina bars;
- damage directly mutates health outside the owner route;
- UI owns truth;
- failure gives no physical reason;
- fatal events can fire repeatedly;
- quality tier changes survival math;
- recovery erases structural consequence without treatment;
- warnings allocate strings or depend on high-end effects;
- pressure, gas, radiation, or temperature has no player counterplay;
- report claims runtime proof without tests/profiler.

## Acceptance Sentence

Survival is accepted only when oxygen, pressure, damage, clarity, power, temperature, and contamination are owned, deterministic, readable, scary through physical consequence, zero-GC in hot paths, and proved with tests or explicit pending-runtime status.
