# HECTON-8 VFX, Particles, And Visual Consequences Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: particles, GPU VFX, leaks, sparks, silt, bubbles, caustic response, tool effects, impact effects, damage feedback, pooling, and VFX proof gates.

## First-20 Route Hook

- First-20 moment: first exit, swim, resource pickup, tool interaction, and first hazard response need owned visual consequences: silt wake, bubbles, leaks, tool heat, sonar pulses, damage feedback, and biolum route/danger cues.
- Route blocker removed: prevents the opening route from using decorative particles, unowned glow, or full-screen noise instead of cause-readable effects tied to survival, tools, water, machinery, or threats.
- Proof class: STATIC_DOC only; route acceptance still requires compact/normal captures, effect owner and pool proof, spam prevention, memory/VRAM notes for textures, and profiler/GPU evidence when runtime effects change.

## Prime Law

VFX are consequences. They are not screen filler.

Every effect must answer what happened, where it happened, why the player cares, and what system owns the cause. If an effect only makes the screen busier, reject it. HECTON-8 needs pressure cracks, silt disturbance, welding heat, hull sparks, sonar pulses, oxygen leaks, biological pulses, and machine failures, not generic particle confetti.

## Truth Ownership

VFX owns visual response, pooling, spawn presentation, shader parameters, and effect lifetime. It does not own damage, power, water fill, AI threat, tool truth, inventory, or survival truth.

Effect spawns must consume typed events, owner snapshots, or cold authoring data. VFX systems must not search the scene, query physics repeatedly, or infer gameplay truth from particle state.

## Effect Families

Production families:

- pressure: cracks, condensation snap, gasket mist, glass stress;
- water: silt wake, leak spray, bubble burst, caustic shimmer, wetness pulse;
- tool: cutter heat, weld plume, scanner ping, drill dust, repair spark;
- machinery: overload arc, pump vibration spray, valve vent, cable flash;
- biological: biolum pulse, mucus trail, blood/cloud, soft tissue twitch;
- impact: hull scrape, debris shard, sediment kick, shield/armor response;
- narrative/evidence: black-box corruption, archive static, ghosted sensor replay.

Each family has event id, owner, lifetime, pool, tier scaling, and rejection screenshot.

Biolum VFX may carry beauty, navigation, and danger at the same time. A pulse can make a route readable, sell alien ecology, mark a resource or creature state, and also create attraction/noise/risk through an owner system. Random always-on glow that only hides weak lighting or empty art is rejected.

## Pooling And Runtime Law

Required:

- fixed pools for recurring effects;
- no instantiate/destroy during gameplay spikes;
- no CPU readback from particle state except diagnostics;
- GPU particles or shader approximations for dense ambience;
- hard caps per region, source, and effect family;
- event coalescing for spammy sources;
- no new materials per effect instance.

For many effects, the best implementation is a shader parameter, decal, VAT, atlas flipbook, audio cue, or haptic pulse. Simulate only when visible consequence cannot be faked cheaply.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale particle count, spawn cadence, flipbook resolution, light contribution, secondary trails, decal density, shader distortion, and diagnostic overlays. It must not change damage truth, hazard truth, or owner state.

Compact keeps cause-readable silhouettes, pooled low-count particles, decals, shader approximations, and audio/UI reinforcement. Middle adds density. High adds richer local response. Ultra adds cinematic layering only within hard caps.

## Production Packet

Any VFX, particle, flipbook, leak, spark, silt, tool effect, or pooled presentation change must declare:

- effect cause owner;
- pool capacity, overflow policy, and lifetime;
- spawn cadence and spam suppression;
- texture/flipbook/material route;
- light/decal/audio/haptic coupling if any;
- Compact and High captures;
- GPU/profiler/memory proof if runtime or flipbook work changed;
- fallback when effect budget is exhausted.

VFX that is always-on decoration, implies false gameplay truth, or allocates at spawn time is rejected.

## Proof Artifacts

VFX work must provide:

- effect family and cause owner;
- pool capacity and overflow policy;
- compact-tier screenshot;
- normal-tier screenshot;
- profiler/GPU proof if runtime effect changed;
- memory/VRAM budget if textures/flipbooks changed;
- spam prevention note;
- no-instantiation hot-path scan if implemented.

## Rejection Gates

Reject:

- VFX with no named cause;
- particles used to hide weak geometry;
- constant full-screen noise;
- unbounded particle counts;
- effect material clones;
- CPU particle readback in gameplay;
- effect state that lies about damage, water, oxygen, threat, or power.

## Acceptance Sentence

VFX is accepted only when each effect is an owned consequence, pooled, scalable, readable on compact tier, and proven not to spend runtime budget on decorative noise.
