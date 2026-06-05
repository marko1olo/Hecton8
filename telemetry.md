# HECTON-8 Telemetry And Black Box Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: black-box rings, crash dumps, profiler markers, budget watchdogs, fault evidence, debug flags, post-mortem records, proof artifacts, and anti-fake reporting.

## First-20 Route Hook

- First-20 moment: boot, world load, first shallow exit, swim, interaction, hazard response, and save/load faults must leave bounded evidence instead of chat guesses.
- Route blocker removed: silent route failures, unowned profiler markers, missing 300-record black boxes, log spam used as proof, and crash reports with no owner/sequence/fault class.
- Proof class: `STATIC_DOC` until route-critical systems emit black-box records, dump manifests, profiler captures, GC proof, and failure/recovery artifacts.

## 1. Prime Law

No critical HECTON-8 system may fail silently.

If physics, AI, vehicles, voxel terrain, streaming, persistence, rendering, audio, UI, water, construction, or generated asset validation can break the player experience, it must leave enough bounded evidence to explain the last relevant states. "I do not know why it broke" is not an acceptable release answer.

Telemetry is not a logging spam system. It is a bounded evidence system:

- fixed capacity;
- unmanaged or preallocated storage;
- owner-defined fields;
- deterministic write cadence;
- no hot-path allocation;
- no unbounded strings;
- exportable binary evidence;
- clear proof labels.

## 2. Truth Ownership

Telemetry does not own gameplay truth. It records owner-published facts.

Each critical system owns its own black-box record schema:

- physics owns force, collision, pressure, damage, and NaN recovery facts;
- vehicles own kinematic state, docking/EVA state, component damage, and cockpit confidence;
- streaming owns residency, load handle, release queue, and memory pressure facts;
- persistence owns save version, section hash, checksum, migration, and corruption facts;
- AI owns Director state, token pressure, stimulus cause, path request state, and behavior transition facts;
- rendering owns GPU budget, active render features, material variant count, and quality load-shed facts;
- water owns flow field, turbidity, fill ratio, silt emission, and caustic route facts;
- UI owns stale source, focus, localization expansion, and update allocation facts.

No telemetry path may mutate the state it records. Read accessors stay pure.

## 3. Black Box Ring Contract

Critical runtime systems must maintain a fixed-size ring covering the last 300 relevant frames or ticks.

Default record law:

- capacity: 300 records;
- struct: blittable/unmanaged;
- layout: explicit or sequential with documented byte size;
- owner: one system;
- allocator: persistent or DataVault-owned, disposed by owner;
- writes: owner phase only;
- reads: diagnostic/export only;
- strings: forbidden in hot records; use hashes;
- Unity object references: forbidden;
- dynamic growth: forbidden.

A system may sample every frame, fixed tick, slow tick, or owner event depending on risk. The cadence must be stated. Increasing capacity above 300 requires memory proof and a reason tied to post-mortem value.

## 4. Required Fault Fields

Every critical black box must contain enough fields to answer:

- which owner wrote the record;
- which frame/tick/sequence this represents;
- which entity, chunk, lane, route, or buffer was affected;
- what state hash existed before and after;
- what error flags were raised;
- whether data was finite;
- which quality weight and load-shed state was active;
- whether a queue, buffer, or budget limit rejected work;
- what recovery path ran;
- whether the system degraded, disabled, dumped, or continued.

If a crash dump cannot name owner, route, sequence, and fault class, it is too weak.

## 5. Dump Protocol

Fault triggers:

- NaN or non-finite data;
- queue overflow or repeated admission refusal;
- stale DataVault handle;
- unowned GlobalRegistry route;
- invalid save checksum or migration failure;
- physics recovery escalation;
- repeated frame-budget breach;
- render feature over-budget;
- asset validation fatal error;
- explicit developer command in Editor or Development build.

Dump output:

- binary `.bin` ring in `Docs/AgentLogs/Dump_[Owner].bin` or the approved runtime dump directory;
- compact manifest with build GUID, platform, scene, owner, schema version, record size, record count, and trigger;
- no managed string spam in the hot capture path;
- no fresh task allocation per fault;
- no player-build dump path that blocks the main thread without a release-approved crash route.

Shipping builds may reduce telemetry breadth, but critical fault breadcrumbs must remain if the system can corrupt save data, trap the player, break route truth, or crash.

## 6. Profiler Marker Law

Every nontrivial runtime system must expose stable profiler markers:

- `H8.[Domain].[Operation]` naming;
- static readonly marker allocation;
- no runtime string concatenation;
- marker coverage around the actual owner work, not only wrapper calls;
- GPU markers for custom render passes, compute dispatches, and heavy upload paths;
- budget owner named in the report.

Profiler markers do not prove performance by existing. They become proof only when a current capture cites scene, hardware, quality weight, duration, and result.

## 7. Budget Watchdog

Runtime systems with measurable hot cost must publish or record budget state:

- assigned budget in microseconds or milliseconds;
- last measured or sampled cost;
- p95 or sampled window where available;
- load-shed trigger;
- load-shed action;
- recovery result;
- proof state.

Any single runtime system above 0.1 ms is suspicious until profiler proof and player-visible value exist. Optimization is not the goal; telemetry exists so saved frame time can be spent on better visuals safely.

## 8. Debug Flags And Build Boundaries

Debug visibility must be controlled:

- Editor and Development build debug paths are guarded;
- shipping build diagnostic payloads are minimal and safe;
- conditional symbols are stable;
- debug toggles do not allocate on toggle spam;
- debug views never become gameplay truth;
- debug UI follows `ui.md` zero-GC text rules.

Do not hide debug logic in production gameplay branches. Do not use debug flags to change simulation results unless the build is explicitly a test harness.

## 9. GlobalQualityWeight Scaling

Compact telemetry records only critical owner state, fault flags, hashes, and budget refusals. Middle adds more domain-specific counters. High adds richer marker/capture metadata and optional visual debug overlays. Ultra may add dense evidence for hero systems, but it must not change gameplay truth, save identity, DTO layout, or authority route.

Telemetry quality scaling changes evidence detail, not the facts being recorded.

## 10. Proof Artifacts

Telemetry work must provide:

- owner and schema;
- record struct size and capacity;
- allocator/lifetime/disposal route;
- write cadence and dispatcher phase;
- dump trigger list;
- sample binary or manifest when implemented;
- profiler marker names;
- hot-path GC proof when runtime code changed;
- failure/recovery repro when possible;
- explicit `PENDING VERIFICATION` for Unity/player/profiler claims not run.

## 11. Rejection Gates

Reject telemetry work if:

- it uses `Debug.Log` spam as proof;
- it allocates strings or lists in hot capture paths;
- it records Unity object references in hot records;
- it grows buffers at runtime;
- it lacks owner, phase, schema, or disposal route;
- it hides gameplay mutation inside diagnostic reads;
- it claims profiler proof without a current capture;
- it cannot explain the last 300 relevant states of a critical failure.

## 12. Acceptance Sentence

Telemetry is accepted only when critical systems leave bounded, allocation-free, owner-routed evidence with clear schema, dump triggers, profiler markers, quality-scaled detail, and proof artifacts sufficient to explain failures without corrupting runtime truth.
