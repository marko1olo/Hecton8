# Rationale_SHINOBU_24

Date: 2026-05-18
Status: PENDING_VERIFICATION_EXTERNAL_COMPILE_BLOCK

## Decision 00: Domain Boundary

Problem: Scanner must target flora/fauna and data nodes while 20+ agents mutate adjacent AI, World, PDA, VFX, and Audio domains.
Solution: Own scanner DTOs, math kernel, mock spatial/SDF data, and signal payloads inside scanner/tool-facing code. Cross-domain output is typed SignalBus/GlobalSignals DTO only.
Rejected Alternatives: Direct references to flora/fauna/PDA classes would create compile-wall dependency risk and violate Signal Lane Segregation.
Scalability potential: Low uses bounded hash cells and 15Hz reacquire under stress; Middle keeps 30Hz/60Hz; High and Ultra can feed richer VFX/acoustic consumers without changing gameplay kernel.
Hardware Impact: i3/MX350 avoids Unity physics BVH and managed component lookup; expected gain is tens of microseconds per scan query and 0 B GC in hot path.

## Decision 01: Dear Lie Intersection

Problem: Polygonal or collider-perfect scan targeting is too expensive and alloc-prone for 5000 biota.
Solution: Treat every scannable as a sphere, use ray/sphere quadratic in Burst, then score by forward dot and distance.
Rejected Alternatives: Physics.Raycast, MeshCollider raycasts, Collider.GetComponent, and sorted RaycastHit arrays are slower, coupled, and not deterministic enough for this data-mining path.
Scalability potential: Low uses one sphere and midpoint SDF sample; Middle adds tighter beam dot threshold; High/Ultra can keep the same logic and spend saved CPU on beam/silt presentation.
Hardware Impact: i3/MX350 expected to keep query under 0.1 ms for bounded candidates; exact profiler proof remains blocked by unrelated compile failures.

## Decision 02: Black Box Telemetry

Problem: Scan stalls or NaN targeting failures need forensic proof without Debug.Log allocations.
Solution: Maintain a vault-owned 300-frame telemetry ring with target hash, candidate count, completion count, estimated microseconds, flags, and AUP; dump binary to `Docs/AgentLogs/Dump_SHINOBU_24.bin` and `Docs/AgentLogs/Dump_SHINOBU_24.h8dump` on budget breach or non-finite input.
Rejected Alternatives: Console logs and managed exception paths allocate and are not acceptable in hot paths.
Scalability potential: Low writes compact state only; High/Ultra can preserve richer debug fields under dev builds.
Hardware Impact: 300 fixed entries at 64 bytes each are negligible memory; per-frame write is a single indexed struct assignment.

## Decision 03: Strict DTO Layout

Problem: The scanner contract demanded exact ARM64-friendly DTO sizes and no property wrappers.
Solution: `ScanResultDTO` is 48 bytes with `double3 AUP`, `uint EntityHash`, `float Distance`, `float ScanProgress`, `uint _pad0`, `ulong _pad1`; `ScannableEntityMetadataDTO` is 16 bytes with hash, duration, required tool level, pad; `ScannerVfxDTO` is 32 bytes.
Rejected Alternatives: Storing radius and flags inside metadata polluted the requested 16-byte layout; radius now lives in `ScannerSpatialEntityDTO`.
Scalability potential: Low/Middle/High/Ultra all share identical memory stride, so no platform-specific serializer fork is needed.
Hardware Impact: i3/MX350 gets cache-stable scan records and avoids pipeline bubbles from unpredictable layout.

## Decision 04: Signal Routing

Problem: Completion, depletion, and acoustic feedback must cross domains without creating managed string or direct PDA/Ecosystem/Audio dependencies.
Solution: Push `EncyclopediaUnlockSignal` and `EntityDepletedSignal` through `SignalBus`, reuse `ScanCompleteSignal`, `ResourceDepletionDeltaSignal`, `ToolAcousticSignal`, and `AcousticPingSignal` for existing consumers.
Rejected Alternatives: String unlock notifications, direct PDA calls, world-object mutation, and owned AudioSource playback.
Scalability potential: Low devices can drop noncritical acoustic/VFX signals under SignalBus pressure; High/Ultra can layer richer consumers without scanner changes.
Hardware Impact: Emits fixed unmanaged payloads only; expected hot-path GC is 0 B.

## Decision 05: Human Control And CSV Overrides

Problem: Designers need control over binary scan timings without injecting runtime UI or string work into scanning.
Solution: `DataMiningTunerWindow` adjusts unmanaged vault settings in editor and draws the yellow cone/red lock line/blue hit sphere in SceneView; `TryApplyCsvOverrideLine` parses hash/duration spans into metadata without `Split` or LINQ.
Rejected Alternatives: runtime MonoBehaviour inspector loops, managed dictionaries, or per-frame file/string parsing.
Scalability potential: Low/Middle/High/Ultra use the same data; only cadence and VFX spend scale.
Hardware Impact: Editor and CSV work stay cold; player hot path remains native containers and Burst math.

## Decision 06: Compile Wall

Problem: Unity script compile cannot reach a clean project state because unrelated agents' domains currently fail.
Solution: Attempted Unity compile four times and targeted `dotnet build Hecton8.Core.csproj` runs; inspected logs/output and confirmed no error line references `ScannerDataMiningRouter.cs`, `DataMiningTunerWindow.cs`, or `ScannerDataMiningRouterEditTests.cs`. Latest dotnet block is external `GlobalTelemetryBus.Blackbox`, `SubmarineDynamicsRuntime`, and missing `GlobalPhysicsStateManager` SHINOBU_37 partial members.
Rejected Alternatives: Editing unrelated Core, Quest, Rendering, Construction, or Audio files would violate domain boundary and risk overwriting other agents.
Scalability potential: No runtime impact; this is integration-state evidence.
Hardware Impact: No player hardware impact.

## Decision 07: DataVault Sovereignty Repair

Problem: The first implementation passed zero-GC intent but still owned private persistent native containers inside `ScannerDataMiningRouter`, which violates H-Phi/Data Sovereignty and blocks vault relocation/black-box audits.
Solution: Replaced runtime-owned arrays/list/hash map with `VaultBufferHandle<T>` leases from `GlobalDataVault`: entities, metadata, occlusion zones, bucket heads, bucket next, result slot, result count, active state, VFX DTO, query stats, telemetry ring, and settings. The router now resolves views immediately before use and locks query buffers while a job owns them.
Rejected Alternatives: Keeping local `NativeArray`, `NativeList`, or `NativeParallelMultiHashMap` fields was faster to code but creates a feudal memory owner outside the vault. Releasing all `SystemID.GameplayTools` buffers on disable was also rejected because that owner may be shared with tool kinematics.
Scalability potential: Low uses the same compact vault buffers and bounded chain length; Middle/High/Ultra can grow capacities through the vault without changing scanner logic or creating new managed owners.
Hardware Impact: i3/MX350 avoids local allocator metadata churn and native hash-map iterator overhead; expected low-tier gain is 3-7 us per query window plus lower memory-fragmentation risk.

## Decision 08: Flat Bucket Spatial Hash

Problem: `NativeParallelMultiHashMap` provided convenient O(1) lookup but adds a separate persistent allocator object and iterator state, both unnecessary for a single-writer scanner mock grid.
Solution: Use `NativeArray<int> BucketHeads` and `NativeArray<int> BucketNext`. `BucketIndex(hash, bucketCount)` maps the spatial cell to a power-of-two bucket; each bucket walks a bounded linked chain with `MaxCandidatesPerCell`.
Rejected Alternatives: Unity physics queries, sorted hit arrays, scene scans, and native multi-hash maps. The flat hash accepts rare bucket collisions because ray/sphere math filters false positives cheaply.
Scalability potential: Low clamps cells/candidates aggressively; Middle/High/Ultra can raise entity capacity and candidate budget while keeping the same data shape. Visual overkill stays downstream in VFX, not gameplay truth.
Hardware Impact: On i3/MX350, removal of hash-map iterator overhead should save a few microseconds in dense cells and keeps all lookup lanes as sequential int arrays.

## Decision 09: ARM64 Runtime Layout Repair

Problem: Several supporting DTOs were size-aligned but not lane-ordered; signal structs also used `Pack=1`, which is forbidden for runtime memory under the new ARM64 mandate.
Solution: Reordered `ScannerSpatialEntityDTO`, `ActiveScanStateDTO`, and `ScannerTelemetryEntry` so `double3`, `long`, and `ulong` lanes precede 4-byte lanes, and removed `Pack=1` from `EncyclopediaUnlockSignal` and `EntityDepletedSignal`.
Rejected Alternatives: Relying on `[StructLayout(Size=...)]` alone; that proves stride but does not prove cheap ARM64 loads.
Scalability potential: Low/Middle/High/Ultra all consume identical strides, so save/dump tooling and Burst jobs do not need platform forks.
Hardware Impact: Quest/ARM64 avoids misaligned 8-byte loads in active state and telemetry; expected win is small per access but material in 300-frame telemetry and dense query sweeps.

## Decision 10: Designer Settings Bridge

Problem: The first editor facade changed a static settings struct; the prompt explicitly required Play Mode read/write through unmanaged vault memory.
Solution: Added `TryReadVaultSettings` and `TryWriteVaultSettings` to bridge the editor window to `BufferID.ShinobuScannerSettings` while preserving static fallback when the vault is absent.
Rejected Alternatives: Runtime UI, string-based UnityEvents, or inspector polling in the scan hot path.
Scalability potential: Designers can tune Low/Middle/High/Ultra cadence and beam costs without recompiling; gameplay truth remains a single unmanaged settings DTO.
Hardware Impact: Editor-only path adds 0 us to player hot scan/progression. Runtime reads one 80-byte settings DTO from vault per query cadence window.
