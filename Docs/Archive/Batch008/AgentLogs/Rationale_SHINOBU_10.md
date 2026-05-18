# Rationale_SHINOBU_10

Date: 2026-05-17
Agent: SHINOBU_10
Domain: ECHELON 3 - Predator Cognition / Utility AI
Status: PENDING VERIFICATION

## Pre-Flight Evidence

- Read `Docs/Tasks/CURRENT_BATCH.md` with CLI regex extraction for `<AGENT_PROMPT id="SHINOBU_10" ...>`.
- Read `Docs/Tasks/Status_SHINOBU_10.md` before continuing.
- Read `Docs/PROJECT_STATE_STATIC_XRAY.md`; runtime proof remains absent, static/source evidence only.
- Re-read `AGENTS.md`; current repo law requires DataVault sovereignty, no hot-path GC, no NavMesh, no raycast, no runtime reflection, no private NativeArray ownership outside approved memory ownership.
- Domain boundary confirmed in `Docs/Actual Domains of Project.txt`: Predator Cognition is ECHELON 3, item 24, with steering integration adjacent to item 25 but not direct kinematics ownership.

## Decision 01 - Spatial Hash Without NavMesh

Problem: Apex predator targeting cannot use `NavMeshAgent`, A*, `Physics.OverlapSphere`, or O(N^2) prey scans. The initial pass used a private persistent `NativeParallelMultiHashMap`, which met the lookup requirement but violated the stricter DataVault sovereignty mandate.

Solution: Convert the target hash into DataVault-resident SOA buckets: fixed bucket-head array plus per-slot next-index array. Rebuild from active target slots once per cognition schedule and query adjacent 3D buckets inside Burst jobs. This preserves sector-hash O(nearby occupants) behavior without a private native map allocation.

Rejected Alternatives: Unity NavMesh and A* are wrong for a 3D ocean/cave volume and cause memory/path CPU pressure. `Physics.OverlapSphere` is a sync physics query and scales poorly. Private `NativeParallelMultiHashMap` is clean locally but fails H-Phi data sovereignty.

Scalability potential: Low tier uses 10Hz cognition and fixed hash buckets; movement can keep using last intent. Middle/high tiers can increase active apex slots or add richer presentation-only reactions without changing gameplay truth. Ultra tier spends saved CPU on visual overkill such as stronger wake/audio/biolum cues outside this domain.

Hardware Impact: Expected CPU pattern improvement versus O(N^2) scans is material on i3/MX350; exact microseconds remain PENDING MEASUREMENT because no Unity profiler/GCMonitor run exists.

## Decision 02 - Dear Lie Visibility

Problem: True line-of-sight raycasts/raymarches cause CPU spikes and couple AI to physics/world collision details.

Solution: Use dot-product view cones plus a single midpoint `HasThreatGridHeuristic` occlusion lookup. This fakes visibility with deterministic math and one sampler-style predicate.

Rejected Alternatives: `Physics.Raycast` and voxel DDA were rejected because they create repeated spatial traversal work where a stable fake is sufficient for player belief.

Scalability potential: Low tier keeps one midpoint lookup. High/Ultra can drive richer presentation from the same stimulus score without changing truth.

Hardware Impact: Avoids per-predator line traces; exact gain is PENDING MEASUREMENT.

## Decision 03 - AUP Precision

Problem: Absolute 100km universe coordinates cannot be cast to float before subtraction without jitter and incorrect utility scoring.

Solution: Store predator and target as `double3`, subtract in double, then cast the local delta to `float3` before distance, dot, and steering math.

Rejected Alternatives: `Vector3.Distance`, absolute float positions, and transform-space truth were rejected.

Scalability potential: Same math path works on low and high tiers; high tier can add visual detail around stable intent.

Hardware Impact: Prevents float jitter and avoids redundant transform reads; exact gain is PENDING MEASUREMENT.

## Decision 04 - ARM64 DTO Layout

Problem: `PredatorCognitionDTO` must be mutable in Burst without CS1612 copy traps and aligned for ARM64.

Solution: Use explicit field offsets with `double3 CurrentAUP` at 0, `double3 TargetAUP` at 24, `float3 ForwardVector` at 48, scalar fields at 60-72, and explicit byte pads to 80 bytes. Mutation uses `UnsafeUtility.AsRef`.

Rejected Alternatives: C# properties and `Pack=1` runtime structs were rejected for copies/misalignment.

Scalability potential: Direct SoA/DTO reads keep low-tier cache behavior predictable; high tier gets more AI slots without object state machines.

Hardware Impact: Prevents unaligned AUP reads on ARM64/Quest-class CPUs; exact gain is PENDING MEASUREMENT.

## Decision 05 - Human Balance Facade

Problem: Designers need to tune apex cognition coefficients without recompiling C#.

Solution: Provide `ApexCortexTunerWindow` under `#if UNITY_EDITOR` with sliders writing the unmanaged Vault tuning lane and a cold CSV reload path for `ai_behavior_overrides.csv`.

Rejected Alternatives: Runtime ScriptableObject mutation and C# constant edits were rejected.

Scalability potential: Low tier uses conservative coefficients/cadence; high/ultra can bias toward more visible stalking, orbiting, and threat drama while preserving the same utility kernel.

Hardware Impact: Editor-only surface has no player hot-path cost; CSV reload is cold/manual.

## Decision 06 - ARM64 Hot DTO Sweep

Problem: The SHINOBU cognition payloads still had runtime `Pack=1` layouts and one directly consumed tuning DTO stored a byte enum before float data. That is exactly the ARM64 cache/unaligned-read failure mode called out in the polish mandate.

Solution: Removed `Pack=1` from `PredatorCognitionDomain` runtime structs, expanded non-8-byte payloads (`AcousticMemoryEntry`, `CognitionControl`, `CognitionOutput`, `RetinalLightResult`) to explicit 8-byte multiples, and converted `SpeciesCognitionTuning` to public readonly fields with floats first and the byte enum plus pad bytes at the tail.

Rejected Alternatives: Leaving `Pack=1` because it compiled was rejected. Repacking all unrelated fauna IK/tentacle files was rejected for this SHINOBU_10 pass because those files belong to adjacent locomotion/IK domains and are not required for the predator utility kernel.

Scalability potential: Low/Quest/ARM64 avoids byte-packed reads in the AI tuning path. High/Ultra get the same deterministic data layout and can scale slot count or visual reaction density without changing the contract.

Hardware Impact: Removes a concrete unaligned-read hazard. Exact microseconds remain PENDING MEASUREMENT; this is a correctness/cache-layout fix, not a profiled claim.

## Decision 07 - Build And Compile Wall Boundary

Problem: Full compile checks can become a rebuild wall under concurrent agents. A previous build attempt exposed stale/concurrent processes and produced no useful source error output.

Solution: Ran a constrained `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly` after the dependency surface was checked. Build reached source compilation and failed only in `HectonSeismicTideDirector.cs` with missing `ILateFrameTickable.LateFrameTick()` implementation and missing `MockNarrativeTriggerSignal`.

Rejected Alternatives: Repeated full graph builds and editor builds were rejected to avoid compile-wall spam. Editing the seismic/tide/narrative domain was rejected as outside SHINOBU_10 ownership.

Scalability potential: Keeps AI work isolated from unrelated compile churn while preserving evidence. No gameplay scalability claim.

Hardware Impact: Developer iteration protection only; no runtime performance metric.

## Decision 08 - Vault Handles For New SHINOBU Lanes

Problem: The new SHINOBU acoustic memory, Apex tuning, and target-hash lanes were DataVault allocations, but the class stored them as raw private `NativeArray<T>` aliases. That expands the same H-Phi smell already present in the legacy domain file.

Solution: Replaced the new raw fields with `VaultBufferHandle<T>` fields and resolve temporary `NativeArray<T>` views only at the use sites that schedule jobs, clear buffers, read tuning, or rebuild the hash. Existing legacy aliases remain because converting the entire 6k-line domain would be a broad refactor outside this prompt; the new work no longer adds raw private native storage.

Rejected Alternatives: Keeping the raw new aliases was rejected. Converting every historical cognition array to handles in this pass was rejected because it would touch nearly every method in a load-bearing shared fauna bridge and create a higher compile-wall risk.

Scalability potential: Handle-backed lanes keep ownership tied to GlobalDataVault and allow generation validation if the vault moves or compacts buffers later.

Hardware Impact: Correctness/ownership fix. No microseconds claimed.

## Decision 09 - Signal Duplicate Removal

Problem: A local AI `MockDamageSignal` duplicated existing signal corridor DTOs already present under `Hecton8.Core.Contracts.Signals` and tracked by `GlobalSignals`.

Solution: Removed the AI-local damage mock and changed the mock stimulus job to use the existing `MockDamageSignal` fields: `Aup`, `Damage`, and `EntityId`.

Rejected Alternatives: Keeping a namespaced duplicate was rejected because it fragments the signal matrix. Replacing all local blind stimuli was rejected because `MockAcousticSignal` and `MockLightSource` are task-specific local stimulus DTOs, not global broadcast events.

Scalability potential: The damage lane can be unified with existing typed signal infrastructure later without migration glue.

Hardware Impact: No runtime performance claim; reduces integration risk.

## Decision 10 - BufferID Collision Correction

Problem: The previous tail IDs `605-608` collided with Tool Kinematics, Save Pager, and Biolum lanes under current concurrent workspace churn.

Solution: Moved SHINOBU-added BufferIDs to `70210-70213`, which are outside the scanned occupied ranges.

Rejected Alternatives: Leaving enum aliases was rejected because two domains could receive the same Vault memory lane and corrupt each other.

Scalability potential: Prevents cross-domain memory aliasing as the project scales.

Hardware Impact: Correctness fix only; no microseconds claimed.

## Decision 11 - Full SHINOBU Native Field Eviction

Problem: The previous polish pass still left legacy private `NativeArray<T>` aliases inside `PredatorCognitionDomain`. Even though the memory was DataVault-backed, the field shape violated the current H-Phi rule and made future agents likely to add local native ownership again.

Solution: Replaced SHINOBU-owned private native fields with `VaultArray<T>`, a thin wrapper around `VaultBufferHandle<T>`. The wrapper resolves generation-checked NativeArray views only for job submission or bounded utility methods, and direct index access uses `VaultBufferHandle<T>.GetElementAsRef` instead of owning a local `NativeArray<T>` field. Threat voxels and chemical breadcrumb snapshots are now `BorrowedArray<T>` wrappers because they are read-only external service snapshots, not SHINOBU-owned memory.

Rejected Alternatives: Keeping raw private `NativeArray<T>` fields was rejected. Converting borrowed world snapshots into SHINOBU-owned copied Vault buffers was rejected because it would duplicate cross-domain data and add per-frame copy pressure.

Scalability potential: Low tier keeps one authoritative Vault allocation per lane and no duplicate native hash or copied world grids. High/Ultra can scale slot count and debug visualization from the same handle-backed lanes without changing ownership.

Hardware Impact: Ownership/correctness fix. No microseconds claimed. Static scan now finds no `private static NativeArray<T>` or `private NativeArray<T>` SHINOBU-owned fields in the edited files.

## Decision 12 - Alias Escape Hatch Removal

Problem: The first H-Phi wrapper pass still contained a `NativeArray<T>` alias fallback inside `VaultArray<T>`. That technically removed private field declarations from the domain but kept an escape hatch for raw local array storage.

Solution: Removed the alias path from `VaultArray<T>`. SHINOBU-owned fields now resolve only through `VaultBufferHandle<T>`. External threat voxels and chemical breadcrumbs remain separated as `BorrowedArray<T>` because they are read-only snapshots owned by world/chemical systems.

Rejected Alternatives: Keeping `VaultArray<T>.Alias` was rejected as a paper compliance trick. Copying borrowed grids into new SHINOBU Vault lanes was rejected because it adds memory bandwidth and duplicate truth.

Scalability potential: Keeps low-tier memory traffic flat and prevents high-tier debug/visual expansion from forking gameplay truth.

Hardware Impact: Correctness/ownership fix only. Final controlled build after this change still reports no SHINOBU_10 source errors; current compile blockers are ecosystem/construction-domain errors.

## Decision 13 - L1 Cache Line Layout Sweep

Problem: A second layout audit found two non-cosmetic issues. `CognitionInput` placed `double3 FloatingOriginOffset` after several `float3` fields, which risks unaligned 8-byte reads on ARM64. `AlphaLeviathanDirective` declared `Size = 24` while its fields exceeded that footprint after the state enum and reserved bytes.

Solution: Moved the 8-byte-aligned input fields (`double3 FloatingOriginOffset`, `PlayerTargetAup`, `PackTargetAup`) to the top of `CognitionInput`, preserving field names and the 480-byte ABI size. Expanded `AlphaLeviathanDirective` to 32 bytes with explicit reserved tail bytes. Removed the stale alias/dispose/sentinel release layer so `ReleaseVaultHandle` only clears generation-checked handles after a completion fence.

Rejected Alternatives: Leaving the old field order because named access still worked was rejected. Shrinking or hiding padding behind implicit runtime layout was rejected. Copying borrowed voxel/chemical snapshots into SHINOBU-owned buffers was rejected because it creates duplicate truth and memory bandwidth.

Scalability potential: Low/ARM64 avoids misaligned AUP reads in the hot utility input lane. High/Ultra can scale more predator slots without changing the data contract.

Hardware Impact: Correctness/cache-layout fix only. No measured microseconds claimed. Controlled no-deps Core build after this change still reports no SHINOBU_10 source errors; current blockers are Homeostasis/Construction-domain errors.

## Decision 14 - NaN Guard And H8Dump Mirror

Problem: The second polish audit found that several reciprocal/rsqrt sites still used `DdaEpsilon = 0.000001f` as a denominator guard. That is acceptable for DDA/zero comparisons, but it does not satisfy the current survival mandate requiring `0.0001f` division guards. The Leviathan cortex blackbox also wrote the task-required `.bin`, but the current crash-telemetry mandate explicitly requires `.h8dump`.

Solution: Added `MathSafetyEpsilon = 0.0001f` and routed SHINOBU reciprocal/rsqrt guard sites that were using `DdaEpsilon` or local counts/weights through it. Pre-clamped denominators that are already >= `0.001f` or >= `1f` remain structurally safe. Retinal and Leviathan fatal dumps now write both `.bin` and `.h8dump` cold-path files from the same writer helpers.

Rejected Alternatives: Raising `DdaEpsilon` globally was rejected because it also gates geometric/visibility comparisons and could change behavior beyond division safety. Keeping `.bin` only was rejected because it failed the current blackbox file-format instruction. Replacing cold crash writes with a new async MMF worker was rejected for this SHINOBU pass because it would create cross-domain IO ownership beyond the predator cognition boundary.

Scalability potential: Low tier avoids NaN amplification from tiny denominator cases without changing visual truth. Middle/high/ultra tiers retain the same gameplay math and can spend saved stability on richer presentation lanes outside this domain.

Hardware Impact: Correctness/survivability fix. No microseconds claimed. Controlled no-deps Core build after the change still reports no SHINOBU_10 source errors; current blockers are WorldChunkResidency/GlobalPhysics SHINOBU_37-domain errors.
