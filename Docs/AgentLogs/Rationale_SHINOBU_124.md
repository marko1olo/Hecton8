# Rationale_SHINOBU_124

## 2026-05-19 Preflight

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="SHINOBU_124">`, so the mandated XML task count is 0.
Solution: Record the missing XML as a hard evidence fact, use the inline user directive only as local task context, and avoid neighboring prompt influence.
Rejected Alternatives: Reading SHINOBU_120 as a proxy, inventing a task count, or pulling archived prompts into the current batch.
Scalability potential: No runtime effect; prevents wrong-domain code from being added under batch ambiguity.
Hardware Impact: 0 us runtime gain on i3/MX350; process-only guard.

Problem: Flora currently risks physical-collider interaction cost if bending is handled by object-level physics.
Solution: Target a visual fake: a Vault-owned 3D displacement/impulse field sampled by shaders, fed by vehicle force injection through decoupled interfaces.
Rejected Alternatives: Per-blade Rigidbody/Collider, trigger volumes per plant, or Unity physics queries for every vegetation instance.
Scalability potential: Low uses coarse field stride and decay; Middle tightens cell size/update cadence; High adds wake persistence; Ultra spends saved CPU on richer shader harmonics and higher field resolution.
Hardware Impact: Estimated savings versus collider vegetation is unmeasured but structurally removes per-flora physics broadphase and managed callback risk; expected target is under 0.1 ms CPU update on i3/MX350 after implementation.

Problem: `H8Memory.BufferID` is already dirty in the shared workspace and high numeric ranges are being used by other agents through private casts.
Solution: Keep the new flora sway Vault IDs private to `FloraInteractionManager` at `71580..71582`, after an exact-source scan found no collisions.
Rejected Alternatives: Editing the shared enum while another agent owns unrelated memory IDs, or reusing `WakeVectorBuffer` as a fake 3D field.
Scalability potential: One authoritative field can scale resolution, cell size, update cadence, and damping from weak hardware to visual-overkill hardware without changing the contract.
Hardware Impact: Avoids cross-agent merge churn; runtime effect is 0 us beyond the actual field update.

Problem: Existing shader path bent flora from a direct submarine sphere global, which is a one-source shortcut and not a Vault field.
Solution: Generate a 3D `float4` displacement field from decoupled wake sources, store it in Vault, upload through double `GraphicsBuffer`, and make the vegetation vertex shader sample `_HectonFloraSwayDisplacementField`.
Rejected Alternatives: Per-plant colliders, per-blade physics callbacks, compute simulation of water volume, or adding a direct `VehicleMotor` reference.
Scalability potential: Weak devices use 8-ish resolution, larger cells, fewer source slots, and slower updates; middle/high/ultra continuously densify to 16^3 nodes, smaller cells, more sources, faster refresh, and higher displacement gain.
Hardware Impact: Structural saving is removal of submarine-to-flora collider requirement; field update is bounded to 4096 nodes and 16 sources worst case, with `GlobalQualityWeight` reducing nodes/sources/update rate before thermal failure.

Problem: Compile verification is required but the explicit CPU gate forbids dotnet/csc while total CPU is over 50%.
Solution: Run non-build static gates only and record the build as blocked by CPU gate after repeated `Processor(_Total)` samples stayed at 100%.
Rejected Alternatives: Launching dotnet anyway, claiming Unity compile success without evidence, or waiting indefinitely while 20+ agents keep the machine saturated.
Scalability potential: No runtime effect; preserves shared workstation stability.
Hardware Impact: Prevents build contention; runtime estimate remains pending profiler data.

## 2026-05-19 XML Restoration / Titanium Pass

Problem: `CURRENT_BATCH.md` now contains the real 20-task `SHINOBU_124` prompt, invalidating the earlier XML-absent status.
Solution: Re-extract the XML by ID, rewrite `Status_SHINOBU_124.md` with the 20-task matrix, and keep all old "missing XML" facts only as historical log lines.
Rejected Alternatives: Continue using the inline prompt only, or let neighboring SHINOBU tasks influence architecture.
Scalability potential: Process-level correction only; prevents wrong-domain code.
Hardware Impact: 0 us runtime.

Problem: The first pass used a CPU `float4` loop and kept `Pack=1` telemetry, which violated the explicit ARM64/Burst mandate.
Solution: Replace the field storage with `FloraDisplacementDTO` `[StructLayout(LayoutKind.Explicit, Size = 16)]`, offsets 0/12; use `FloraSwayFieldTelemetryEntry` explicit 64B; validate via `UnsafeUtility.SizeOf/GetFieldOffset`.
Rejected Alternatives: `float4` as an implicit contract, `Pack=1`, C# properties, or managed class state for nodes.
Scalability potential: Low/Middle/High/Ultra all use the same 16B ABI and only change resolution/cadence/math.
Hardware Impact: Prevents ARM64 unaligned access penalties; expected gain is structural, not profiler-measured yet.

Problem: Collider bending and large-flora proxy colliders contradicted the visual-field mandate.
Solution: Remove `Physics.OverlapSphereNonAlloc` from `FloraInteractionManager` dynamic bend collection and replace `HectonMapMagicVegetationBridgeFloraCollisionProxies` with pure no-op partial methods so partial call sites remain but collider/proxy types disappear from the source lane.
Rejected Alternatives: NonAlloc physics queries, trigger scripts, or pooled `BoxCollider` proxies for procedural sway.
Scalability potential: Weak devices avoid broadphase work; high-end devices spend saved CPU on 64^3 field and shader interpolation.
Hardware Impact: Removes one per-frame physics query and proxy GameObject churn from the flora sway lane.

Problem: Source-force scatter would need atomics or write races when many bodies affect one cell.
Solution: Use a deterministic cell-driven gather: `AccumulateFloraForcesJob` reads bounded wake sources and writes exactly one cell index per worker; `[NoAlias]` fields let Burst vectorize safely.
Rejected Alternatives: Atomic scatter, per-source NativeQueue fanout, or CPU loops over 64^3 nodes.
Scalability potential: Source limit scales continuously with `GlobalQualityWeight`; 16^3 low and 64^3 ultra use the same kernel.
Hardware Impact: Avoids false sharing and atomics; expected i3/MX350 saving depends on active source count and needs profiler proof.

Problem: A localized field can smear stale displacement when the player crosses a quantized origin boundary.
Solution: Quantize the grid from camera/player AUP, subtract source AUP in double/long space before float cast, and make the decay job reset active nodes when resolution/origin changes.
Rejected Alternatives: Casting absolute 100km positions to float, or keeping stale rows after a center jump.
Scalability potential: Low uses coarser cells and fewer origin changes; ultra tightens cells without precision drift.
Hardware Impact: Prevents jitter/stretch faults; reset cost is a Burst linear pass instead of main-thread zeroing.

Problem: Missing `flora_stiffness_profiles.h8bin` or CSV must not crash boot or force managed parsing in runtime lanes.
Solution: Add Vault-owned fallback rules (`71583`) and a cold CSV ingestor using native scratch (`71584`), byte-level FNV-1a hashing, and unmanaged rule mutation.
Rejected Alternatives: `string.Split`, `File.ReadAllLines`, ScriptableObjects, or failing initialization when the baker payload is absent.
Scalability potential: Designers can tune stiffness without C# recompilation; runtime shader path remains stable.
Hardware Impact: Hot path 0 B GC; CSV import is cold/editor only.

Problem: Designers needed a facade and forensic visibility without adding runtime GameObjects.
Solution: Add UI Toolkit `Procedural Flora Sway Tuner`, live max/resolution/cell readout, sliders for decay/current/mass, mock/gizmo toggles, and `OnDrawGizmos` line sampling from Vault.
Rejected Alternatives: in-game debug text, runtime arrows, or requiring code edits for tuning.
Scalability potential: Weak devices tune decay/current down; high/ultra raise field density and visual response.
Hardware Impact: Editor-only allocation surface; no player hot-path GC.

Problem: The shader still relied on direct sphere/interaction offsets after the field was added.
Solution: Sample `_HectonFloraSwayDisplacementField`, use vertex color red as stiffness/tip mask, use nearest sampling at low quality and trilinear sampling above the quality curve, and suppress direct sphere/player offsets while the field is active.
Rejected Alternatives: CPU leaf deformation, direct submarine sphere as authoritative bending, or always-eight-tap sampling on low quality.
Scalability potential: 16^3 nearest on low, 64^3 trilinear on ultra.
Hardware Impact: Moves interaction cost to vertex ALU/structured-buffer reads; CPU broadphase removed.

Problem: The no-op large-flora source still carried `CollisionProxy` names in method call sites and filename, so literal grep gates could misclassify the lane as proxy-backed.
Solution: Rename the partial source to `HectonMapMagicVegetationBridgeFloraVisualSway.cs`, rename the five no-op partial methods to visual-sway names, and update the local call sites in `HectonMapMagicVegetationBridge.cs`.
Rejected Alternatives: Keep proxy names with comments, or delete call sites and risk partial-lifecycle compile drift.
Scalability potential: No runtime algorithm change; it hardens the proof that procedural sway has no PhysX representation from weak devices to ultra.
Hardware Impact: 0 us measured; prevents accidental reactivation of collider proxy churn.

Problem: Touched runtime files still contained legacy `Pack=1` layouts, which violates the ARM64 alignment mandate even if those structs predated SHINOBU_124.
Solution: Convert `ParasiteNode` to explicit 64B layout with `double BirthTimeSeconds` at offset 0, and convert `AbyssalPathTelemetryEntry` to explicit 64B layout with manual padding at offsets 56 and 60.
Rejected Alternatives: Ignore adjacent Pack=1 as "not my task", or change field sizes without preserving 64B stride.
Scalability potential: Uniform explicit DTO layouts keep mobile ARM64 and desktop SIMD paths predictable across quality levels.
Hardware Impact: Avoids unaligned double access risk on i3/MX350-class and ARM64 targets; exact gain pending profiler proof.

Problem: `FloraSwayTunerWindow.cs` was a new Unity asset without a `.meta` file, leaving GUID generation to local Unity import.
Solution: Add `FloraSwayTunerWindow.cs.meta` with a fixed GUID.
Rejected Alternatives: Let Unity generate a local GUID later, which would be nondeterministic across agents.
Scalability potential: Editor-only asset hygiene; no runtime scalability effect.
Hardware Impact: 0 us runtime.

Problem: Compile proof is still required, but the latest CPU gate sample ended at `75.3%`.
Solution: Do not launch dotnet/csc; run only static gates and keep Task 20 open until the build gate is legal.
Rejected Alternatives: Build under load, or claim compile success from static review.
Scalability potential: No runtime effect; protects shared workstation throughput.
Hardware Impact: Prevents build contention; runtime frame cost remains pending Unity/profiler proof.
