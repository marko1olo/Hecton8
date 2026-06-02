# HECTON-8 Math, Determinism, And AUP Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: Absolute Universe Position, floating origin, deterministic RNG, hot-path math, distance checks, SIMD/Burst policy, math LOD, CI math gates, replay safety, and numerical proof.

## 1. Prime Law

Math is gameplay architecture. It is not helper code.

HECTON-8 rejects jitter, float drift, nondeterministic rolls, hot-path square roots, frame-time seeds, transform-position authority, and platform-specific math assumptions hidden inside gameplay systems. A world with good art still feels cheap if vehicles jitter after a rebase, AI forgets deterministic stimuli, save files restore positions differently, or hot loops waste budget normalizing vectors that only needed squared comparison.

Every simulation-scale coordinate, random outcome, distance test, path cost, physics packet, voxel edit, and save hash must have a stable math route. Presentation may smooth. Authority must remain finite, deterministic where required, and owner-routed.

## 2. Truth Ownership

Math does not own gameplay facts. It defines the allowed representation and operations used by the owning systems.

Domain ownership:

- `systems.md` owns dispatcher phase and route purity.
- `data.md` owns DTO layout, alignment, and buffer representation.
- `vehicles.md`, `physics.md`, `voxels.md`, `ai.md`, `world.md`, and `persistence.md` own their domain facts.
- `math.md` owns coordinate precision law, deterministic RNG law, hot-path math law, CI math gate expectations, and numeric proof requirements.

No system may bypass math law by saying its local case is "small." Small violations become visible as jitter, bad saves, path drift, broken collision, or CPU waste when multiplied across the project.

## 3. AUP Spatial Authority

AUP is the simulation-scale spatial authority.

Required coordinate model:

- sector/grid component: signed 64-bit integer vector or approved equivalent;
- local offset: float or millimeter-quantized local representation within the current sector;
- shift generation ID;
- source system ID;
- finite flags;
- optional camera-relative cache for presentation only.

`Transform.position` is presentation. It is not save identity, physics authority, navigation authority, deterministic hash input, or post-rebase truth.

Forbidden:

- reconstructing AUP authority from camera-relative Unity positions;
- saving world-space `Transform.position`;
- hashing transient scene instance IDs as spatial truth;
- using stale shift generation data in physics, rendering, navigation, save, or telemetry;
- interpolating gameplay authority across pre-shift and post-shift coordinates.

## 4. Floating Origin And Sync Fence

Every origin shift creates a 300-frame sync fence.

Required shift protocol:

1. increment shift generation ID;
2. write black-box fence record;
3. freeze or gate gameplay-side integration at the shift boundary;
4. rebuild CPU-side AUP-derived caches;
5. invalidate render, navigation, physics, save, and telemetry caches carrying old shift ID;
6. resume only after owner systems acknowledge the new shift ID;
7. keep drift probes alive for the full fence window.

Fence fields:

- shift generation ID;
- frame index;
- shift vector in millimeters or approved unit;
- camera AUP before and after;
- physics hash;
- render hash;
- navigation hash;
- save hash;
- max observed error;
- non-finite flags.

Clearing the fence because the scene looks fine is rejected. Visual smoothness is not proof of spatial correctness.

## 5. Drift Probe Law

Every AUP-sensitive system owns a cheap drift probe.

Probe rules:

- compare integer or squared-distance deltas;
- do not use square roots;
- do not allocate;
- do not format strings in hot paths;
- emit hashes and max error, not prose;
- include shift generation ID;
- dump through `telemetry.md` on fatal drift.

Probe cadence scales with `GlobalQualityWeight`, but authority error budgets do not disappear on low hardware. Low tier may probe less often. Low tier may not corrupt world position truth.

## 6. Deterministic RNG And Slot-Machine Law

Gameplay randomness uses deterministic weighted selection from authored tables.

A roll must be derived from:

- world seed or save seed;
- stable entity/chunk/AUP ID;
- table version;
- roll index;
- explicit salt.

Use integer weights and integer threshold comparison. Floating cumulative gameplay weights are forbidden.

Forbidden authority seeds:

- `UnityEngine.Random`;
- `Random.Range`;
- `System.Random` without a deterministic owned seed route;
- wall-clock time;
- frame time;
- object instance ID;
- mutable transform position.

Changing table order requires a version bump. Any gameplay-affecting roll must be replay-loggable: seed inputs, table version, roll index, threshold, selected ID.

## 7. Hot-Path Distance And Normalization

Default hot-path law:

- prefer squared-distance comparisons;
- use `math.rsqrt(math.max(dot(v, v), epsilon))` for inverse length when a normalized vector is actually required;
- use LUTs, dominant-axis approximation, L1/Linf approximations, or authored direction tables for visual-only low-tier work when acceptable;
- use fixed-point or replay-tested scalar routes for deterministic gameplay authority when approximate reciprocal-square-root bit identity would be unsafe.

Forbidden in runtime hot paths without documented suppression:

- `math.sqrt(`;
- `Mathf.Sqrt(`;
- `Vector3.Distance(`;
- `math.length(`;
- `math.normalize(`;
- `.normalized`;
- Quake-style bit hacks;
- unsupported SIMD width assumptions;
- platform-deterministic gameplay depending on approximate CPU/GPU math identity.

Suppression must include owner, reason, tier, and profiler or replay artifact path.

## 8. SIMD And Burst Policy

Burst auto-vectorized SoA loops are the default. Hand-written intrinsics are allowed only when:

- Burst Inspector proves auto-vectorization is insufficient;
- target feature checks exist;
- scalar fallback exists;
- benchmark proves a meaningful gain;
- deterministic gameplay authority is not broken.

Allowed lanes:

- Compact/i3: Burst auto-vectorization or guarded v128 fallback;
- Middle: auto-vectorized plus guarded v128 where proven;
- High: guarded v256/AVX paths only with benchmark proof;
- Ultra: richer visual batch math after player-facing benefit is shown.

Do not mandate v512, AVX-512, or future Burst behavior without project package and hardware proof.

## 9. Math LOD

Math LOD is continuous, not binary.

`GlobalQualityWeight` may scale:

- probe cadence;
- sample density;
- solver iteration count;
- visual approximation quality;
- telemetry detail;
- path smoothing samples;
- secondary motion fidelity;
- particle or swarm math density.

It must not change:

- save identity;
- authority coordinate representation;
- DTO layout;
- deterministic gameplay result;
- collision truth;
- RNG table version;
- route ownership.

Compact chooses cheaper approximations and lower cadence. High and Ultra spend saved budget on visual density, not on changing truth.

## 10. CI Math Gate

Runtime math gates must scan first-party runtime source and fail unreviewed hot-path violations.

Minimum banned token list:

- `math.sqrt(`;
- `Mathf.Sqrt(`;
- `Vector3.Distance(`;
- `math.length(`;
- `math.normalize(`;
- `.normalized`;
- `Random.Range`;
- `UnityEngine.Random`;

The gate must exclude Editor code, generated files, smoke testers, comments, and documented cold paths. Each violation must report file, line, token, owner/suppression state, and suggested replacement: squared-distance, rsqrt, LUT, dominant-axis, deterministic hash, or editor-only.

No suppression is valid because "it is probably cheap."

## 11. Proof Artifacts

Math-sensitive work must provide:

- owner and domain;
- coordinate representation;
- shift generation handling if spatial;
- deterministic seed route if random;
- distance/normalization policy if hot path;
- Burst/SIMD route and fallback if optimized;
- CI suppression comments and artifact paths if banned tokens remain;
- drift probe or finite-value proof where relevant;
- replay proof for deterministic authority changes;
- profiler/GC proof for runtime hot-path changes;
- black-box fields through `telemetry.md` when failure can corrupt state.

Static proof cannot claim runtime determinism. Runtime determinism requires replay, profiler, and target-platform evidence.

## 12. Rejection Gates

Reject math work if:

- AUP-sensitive systems use raw `Transform.position` as authority;
- saves hash or store transient world-space positions;
- origin shifts lack 300-frame fence evidence;
- gameplay randomness uses Unity random or frame-time seeds;
- hot loops use sqrt/length/normalized without proof;
- approximate SIMD changes gameplay truth without replay proof;
- quality tiers change deterministic outcomes;
- math reports omit finite-value, drift, profiler, or replay evidence.

## 13. Acceptance Sentence

Math work is accepted only when coordinates, random outcomes, distance checks, solver cost, quality scaling, and failure evidence are owner-routed, finite, deterministic where required, hot-path efficient, and proven with artifacts rather than assumed from code shape.
