# HECTON-8 Performance, Zero-GC, And Runtime Budget Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: frame budgets, zero-GC hot paths, memory/VRAM budgets, load shedding, arena allocation, jobs/Burst discipline, profiler gates, and runtime performance proof.

## First-20 Route Hook

- First-20 moment: world load, bright shallow exit, swim/orient, resource/tool interaction, hazard response, and save/load must fit compact budgets without flattening the route.
- Route blocker removed: fake optimization claims, hot-path allocations, unowned memory/VRAM growth, and load-shed rules that drop route readability or survival warnings.
- Proof class: `STATIC_DOC` until compact-route profiler, GCMonitor or equivalent GC proof, Memory/VRAM captures, and screenshot/clip evidence exist.

## Prime Law

Performance is the currency used to buy immersion. It is not an excuse to make the game ugly, empty, low-poly, or sterile. HECTON-8 must spend frame time only on player-readable pressure, danger, route, material truth, sound, control, and evidence. If performance work does not protect or improve a player-visible route, gameplay clarity, stability, compact-lane survival, or high-tier sensory richness, it is not priority work.

Any system that adds more than `0.1 ms` to a frame is suspicious until profiler evidence proves it is cold, amortized, load-shed, or essential. This is a measured-review trigger, not permission to delete visible quality or player decision value. Any hot-path managed allocation is a defect. Any performance report without proof is a story.

## Truth Ownership

Performance does not own gameplay truth. It owns budgets, measurement, load-shed policy, allocation law, and rejection gates.

Domain owners decide what matters. Performance decides whether the chosen route is affordable, scalable, and proved. If a visual or gameplay feature cannot fit the compact lane, it must define a cheaper premium approximation or be rejected.

## Process Gate And Unity Slot Law

Heavy proof actions are a shared workstation resource, not an agent-local right.

Before launching `dotnet`, `csc`, Unity batchmode, import, profiler capture, player build, asset reimport, or any equivalent heavy proof action:

- sample local CPU load and active `Unity`, `Unity Hub`, `dotnet`, `csc`, `VBCSCompiler`, `MSBuild`, `Unity.ILPP.Runner`, `UnityShaderCompiler`, `ShaderCompiler`, and `AssetImportWorker` processes;
- if CPU is above `50%`, a compile/import/build is already active, or Unity is importing/compiling, do not start another heavy action;
- return or report `BUILD_GATE_BLOCKED: <reason>` and continue with static/scoped work only;
- after two blocked attempts over unchanged process state, stop that lane with the exact blocker instead of polling;
- never convert a static scan, watchdog status, or proof-packet gate pass into Unity import, Play Mode, profiler, visual, player-build, or release readiness.

`Tools/ProofGate/unity_process_proof_watchdog.py` is the current static process/proof sampler. It may summarize busy Unity/compiler/import state, raw screenshot groups, proof-packet status, and dirty log tokens. It must not launch Unity, enter Play Mode, profile, build, kill processes, accept visual quality, or take the Unity slot.

## Frame Budget Law

Every runtime feature must name:

- owner phase;
- expected budget or estimate in microseconds, explicitly marked as estimate until profiled;
- target hardware tier;
- profiler marker;
- update cadence;
- load-shed path;
- failure behavior;
- proof artifact.

Frame time must be treated as a shared resource. No system may assume spare time because it looks small in isolation. The compact hardware lane is mandatory, not optional. Measured microseconds require a current profiler artifact; do not invent exact numbers for reports, rationale, or status files.

## Zero-GC Law

Hot paths allocate `0 B`.

Forbidden in hot paths:

- LINQ;
- `new` managed objects;
- string interpolation, `ToString()`, `string.Format`, or concatenation;
- `foreach` through interfaces or dictionaries;
- runtime reflection;
- coroutines for repeated gameplay logic;
- allocating physics queries;
- scene search APIs;
- runtime JSON/CSV parsing;
- uncached lambdas or delegates;
- material/mesh/texture cloning.

Hot UI uses preallocated buffers and zero-GC text routes. Hot gameplay uses structs, NativeArrays, owner-owned buffers, registry handles, and bounded query arrays.

## Arena And Native Memory Law

Persistent native state belongs to `GlobalDataVault`, an owner-local persistent buffer, or an explicitly named allocator route. Frame scratch that cannot live in persistent buffers uses `HectonArenaAllocator` only when bounded.

Arena rules:

- 64-byte alignment unless a smaller alignment is proved sufficient;
- high-water telemetry per subsystem;
- reset only at dispatcher-approved boundary after jobs are complete;
- overflow returns failure and disables optional work;
- no managed fallback in hot path;
- arena memory is frame-lifetime only;
- no arena allocations inside Burst jobs;
- no storing arena spans, pointers, or buffers in fields.

## Jobs And Burst Discipline

Jobs are accepted only for batched, data-local work with amortized scheduling and dispatcher-owned completion. Tiny job fan-out is fake optimization.

Forbidden:

- `Schedule()` followed by same-method `Complete()`;
- hidden `.Complete()` in read accessors;
- jobs that move more memory than they compute;
- TempJob allocations as persistent state;
- passing native containers across domains without owner and lifetime;
- jobs whose completion window is not documented.

Burst jobs must use finite math guards, deterministic modes where required, explicit inputs, and profiler markers around schedule and complete windows.

## Memory And VRAM Budgets

Runtime memory must be budgeted before content is accepted.

Required:

- RAM budget for persistent runtime buffers;
- managed heap cap and known allocation sites;
- VRAM budget for textures, RTs, geometry, compute buffers, shadows, post stack, and driver reserve;
- texture streaming policy;
- pool capacities;
- overflow/load-shed behavior;
- Memory Profiler proof when runtime memory changed.

Uncompressed runtime textures, uncontrolled render targets, material clones, and per-instance mesh buffers are rejected unless a proof packet shows necessity and load shedding.

## Load-Shed Law

Load shedding is not a panic button. It is an authored state machine.

Triggers:

- frame-time sustained overload;
- VRAM pressure;
- RAM pressure;
- thermal trend;
- GPU compute overrun;
- packet/telemetry queue overflow;
- streaming backlog.

Response order must preserve gameplay truth and route readability. Drop decorative particles before instruments. Drop secondary shadows before route lights. Drop far creature polish before local threat telegraphs. Never drop the only readable hazard, return path, or survival warning.

Memory pressure trigger:

- `used/total > 0.90` on an owned RAM, VRAM, texture residency, or render-target budget is an immediate load-shed trigger;
- first response is noncritical mip/residency downgrade, speculative load cancellation, release-queue drain, and non-primary render-target reduction;
- do not claim success until Memory Profiler/platform counters show the pressure resolved and the route capture still preserves readability.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` scales cadence, capacity, LOD distance, optional diagnostics, presentation density, texture target, particle count, shadow quality, and update frequency.

It must not change gameplay truth, save identity, DTO layout, command authority, rollback hash fields, resource math, or public platform claims.

Compact is minimum survival readability. Middle is full game truth with disciplined presentation. High adds richer proof-backed visuals. Ultra spends budget on sensory density after the compact lane is already shippable.

## Proof Artifacts

Performance work must provide:

- profiler marker names;
- compact hardware target;
- frame-time capture or explicit static-only label;
- GC allocation proof;
- Memory Profiler proof when memory changed;
- VRAM or render target budget when graphics changed;
- load-shed trigger and response list;
- arena/native allocation owner and lifetime;
- job schedule/complete proof;
- known allocation waiver if unavoidable;
- black-box telemetry field when over-budget can occur.

## Rejection Gates

Reject work if:

- it allocates in hot path;
- it adds runtime cost with no player-readable result;
- it uses optimization as an excuse for ugly output;
- it has no compact lane;
- it has no profiler marker;
- it hides job completion;
- it creates private persistent native buffers without owner;
- it uses runtime parsing as normal gameplay route;
- it reports microseconds without profiler evidence;
- it uses binary quality switches instead of continuous scaling.

## Acceptance Sentence

Performance work is accepted only when it preserves immersion, proves hot paths are allocation-free, owns budgets and load-shed behavior, scales continuously, and uses measured evidence instead of optimism or static claims.
