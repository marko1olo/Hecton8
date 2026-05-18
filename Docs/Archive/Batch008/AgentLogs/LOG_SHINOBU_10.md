# LOG_SHINOBU_10

## 2026-05-17 - Apex Cognition Utility AI Pass

Status: PENDING VERIFICATION. Static/source evidence only. No Unity Play Mode, Unity Console, profiler, GCMonitor, Memory Profiler, player build, or runtime frame capture was produced.

### What Was Wrong

- Predator targeting previously risked O(N^2) scans or physics-style queries; that is not acceptable for 100+ apex predators.
- The first spatial hash implementation used a private persistent `NativeParallelMultiHashMap`, which satisfied lookup speed but violated the stricter DataVault sovereignty rule.
- Runtime cognition structs still carried `Pack=1` layouts in the SHINOBU path, and `SpeciesCognitionTuning` placed a byte enum before float fields.
- Visibility was too easy to overbuild into raycasts/raymarches; SHINOBU must fake sight with deterministic math.
- Designer tuning required a human bridge without C# recompiles.

### What Was Done

- Added explicit `PredatorCognitionDTO` with 80-byte layout and direct fields, mutated through `UnsafeUtility.AsRef`.
- Added unmanaged mock signals: `MockAcousticSignal`, `MockLightSource`, and `MockDamageSignal`.
- Added a Burst mock stimulus probe to exercise acoustic recall, light fear spikes, and damage/flee/aggression behavior without direct player/physics coupling.
- Added Vault-backed `NativeArray<float4>` acoustic memory lane and Vault-backed Apex Cortex tuning lane.
- Replaced private native target hash ownership with DataVault-resident sector hash arrays:
  - `PredatorCognitionTargetHashBucketHeads`
  - `PredatorCognitionTargetHashNext`
- Implemented adjacent-sector spatial hash lookup in `SwarmAnalysisJob`.
- Kept potential-field steering output as desired velocity/avoidance intent; AI does not own movement.
- Kept AUP-safe local math: subtract `double3` AUP values before casting deltas to `float3`.
- Replaced true LOS with the Dear Lie: dot/range score plus one midpoint threat-grid heuristic.
- Added alpha leviathan blackbox oscillation detection and dump path `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin`.
- Added editor-only `ApexCortexTunerWindow` with sliders, CSV reload, and SceneView intent gizmos.
- Removed `Pack=1` from SHINOBU cognition runtime structs and the directly consumed `SpeciesCognitionTuning` DTO; padded non-8-byte payloads to 8-byte multiples.

### Cinematic Cheats Used

- Sight: dot product against predator/player forward vectors instead of raycasts.
- Occlusion: one midpoint threat-grid lookup instead of raymarch/DDA.
- Hearing: `float4` last-known acoustic memory instead of object tracking.
- Pack hunting: vector spread/orbit forces instead of a group behavior tree.
- Toaster cadence: low-tier cognition can run at reduced cadence while movement consumes the last output.

### Exact Microseconds Saved

Measured proof: 0 us claimed. No profiler artifact exists.

Static expected savings:

- Replacing O(N^2)/physics target scans with sector hash lookup should reduce neighbor work to occupied adjacent buckets.
- Replacing raycasts/DDA with dot product plus midpoint lookup removes repeated physics/world traversal.
- Removing private native hash ownership reduces allocation/sentinel complexity, not directly measured CPU.

### Verification

- Controlled compile command: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`
- Compile result: BLOCKED BY EXTERNAL DEPENDENCY.
- External errors:
  - `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs(303,113): CS0535` missing `ILateFrameTickable.LateFrameTick()`.
  - `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs(357,35): CS0246` missing `MockNarrativeTriggerSignal`.
- SHINOBU targeted banned API scan: PASS, no `NavMeshAgent`, `Physics.Raycast`, `Physics.OverlapSphere`, `Vector3.Distance`, `AstarPath`, or `Seeker` in edited SHINOBU files.
- SHINOBU targeted `Pack=1` scan: PASS for `PredatorCognitionDomain.cs` and `FaunaDataTemplate.cs`.
- Whitespace check: PASS for touched SHINOBU files, CRLF warnings only.

<SELF_AUDIT>
20_TASK_CHECK:
Task 01 [PASS] Mock cognition profiles generated into Vault when legacy binary profile is not proven.
Task 02 [PASS] NavMesh/A*/physics targeting banned in edited SHINOBU files.
Task 03 [PASS] `PredatorCognitionDTO` uses direct fields and `UnsafeUtility.AsRef`.
Task 04 [PASS] Primary DTO is explicit 80-byte ARM64-safe layout.
Task 05 [PASS] Mock acoustic/light/damage signals and stimulus job implemented.
Task 06 [PASS] Burst utility scoring kernel drives state choice.
Task 07 [PASS] `NativeArray<float4>` acoustic memory bank is Vault-backed.
Task 08 [PASS] Dot-product photophobia implemented.
Task 09 [PASS] Potential-field desired velocity output preserved.
Task 10 [PASS] Spatial hash targeting implemented as Vault SOA bucket heads plus next-chain.
Task 11 [PASS] Pack flanking/orbit/separation logic in Burst.
Task 12 [PASS] Damage flinch/rage-style utility bias implemented through mock damage path.
Task 13 [PASS] Frustum stalking uses player-facing dot math.
Task 14 [PASS] Low-tier cognition cadence gating present.
Task 15 [PASS] AUP delta math subtracts in double before float cast.
Task 16 [PASS] Dear Lie midpoint occlusion implemented.
Task 17 [PASS] 300-frame cortex telemetry and dump path active.
Task 18 [PASS] Apex Cortex Tuner editor window added.
Task 19 [PASS] SceneView intent gizmo toggle added.
Task 20 [PASS] Cold span CSV ingestor added.

ARM64_CHECK:
`PredatorCognitionDTO` byte layout:
0..23   `double3 CurrentAUP`
24..47  `double3 TargetAUP`
48..59  `float3 ForwardVector`
60..63  `float Hunger`
64..67  `float Fear`
68..71  `uint TargetID`
72      `byte CurrentState`
73..79  explicit pad bytes `_pad0.._pad6`
Total: 80 bytes, multiple of 8.

ZERO_GC_CHECK:
No LINQ, closures, `FindObjectOfType`, `GetComponent`, `new NativeArray`, or string-based hot-path targeting was introduced in the edited SHINOBU files. CSV and binary dump use File I/O only on cold/manual/fatal paths.

AUP_CHECK:
Target and current AUP are `double3`. Utility math subtracts absolute coordinates first, then casts the local delta to `float3` for dot, length-squared, and steering calculations.

DEAR_LIE_CHECK:
True line-of-sight is faked by dot/range gates plus a single midpoint threat-grid lookup. No physics raycast is used for SHINOBU sight.

DEPENDENCY_CHECK:
No new direct dependency on player, physics, NavMesh, A*, or sibling runtime managers. Cross-domain data enters through Vault buffers, abstract input DTOs, or editor-only facade code. `BufferID` additions were appended at non-conflicting tail values 605-608.

H_PHI_CHECK:
New acoustic memory, tuning, and target-hash storage live in GlobalDataVault buffers. The private persistent `NativeParallelMultiHashMap` was removed.

BLACKBOX_CHECK:
Alpha leviathan telemetry ring remains 300 frames and dumps to `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin` on oscillation/fatal cortex state.

COMPILE_GUARD:
Core no-deps compile reached source compilation and is blocked by external seismic/tide errors, not SHINOBU_10 source errors in the reported build output.
</SELF_AUDIT>

## 2026-05-17 - Ultra Polish Re-Audit Addendum

Status: PENDING VERIFICATION. This addendum corrects defects found after re-reading the mandate and current files.

### Corrections

- New SHINOBU lanes are no longer stored as raw private `NativeArray<T>` fields:
  - `_acousticMemoryFloat4BankHandle`
  - `_apexCortexTuningHandle`
  - `_predatorTargetHashBucketHeadsHandle`
  - `_predatorTargetHashNextHandle`
- Use-site code resolves temporary `NativeArray<T>` views from `VaultBufferHandle<T>` before job scheduling, tuning reads/writes, acoustic debug reads, and target-hash rebuilds.
- Removed AI-local `MockDamageSignal`; mock damage now uses existing `Hecton8.Core.Contracts.Signals.MockDamageSignal`.
- Corrected BufferID collisions. SHINOBU-added IDs moved from colliding `605-608` to:
  - `PredatorCognitionAcousticFloat4Bank = 70210`
  - `PredatorCognitionApexCortexTuning = 70211`
  - `PredatorCognitionTargetHashBucketHeads = 70212`
  - `PredatorCognitionTargetHashNext = 70213`

### Re-Verification

- Targeted scan found no `Pack=1`, `NativeParallelMultiHashMap`, `new NativeArray`, NavMesh, raycast, OverlapSphere, `Vector3.Distance`, `FindObjectOfType`, `FindObjectsOfType`, `GetComponent<`, or `Enumerable.` in edited SHINOBU files.
- BufferID scan confirmed no SHINOBU collision with 605-608 after the move.
- `dotnet restore Hecton8.Core.csproj -v:minimal`: restore up to date.
- Controlled no-deps Core build still fails outside SHINOBU:
  - `DroneCognitionJob.cs`: missing `PathWaypointDTO`, `MockSdfGrid`.
  - `DroneFleetManager.cs`: missing `DroneFleetTuningConstants`, `MockSdfGrid`, `DroneFleetAutomationStats`, `DroneFleetDebugRoute`, `DroneNativeMinHeapNode`, `DroneAStarTelemetry`.
- No SHINOBU compiler errors appeared in the captured output.

### Residual Risk

- `PredatorCognitionDomain.cs` still contains legacy private `NativeArray<T>` Vault aliases predating this pass. They are DataVault-backed and not locally allocated, but they are not yet full `VaultBufferHandle<T>` ownership surfaces. Full conversion is a broader fauna-domain refactor and remains PENDING VERIFICATION.
- `TryLoadBehaviorOverridesCsvCold()` still uses cold/manual file I/O and `File.ReadAllText`; it is not called during initialization or hot ticks. Runtime hot-path I/O remains absent.

## 2026-05-17 - H-Phi Native Field Eviction Addendum

Status: PENDING VERIFICATION. This addendum supersedes the residual private-array note above for SHINOBU-owned buffers.

### What Was Wrong

- Legacy cognition buffers in `PredatorCognitionDomain.cs` were still stored as private raw `NativeArray<T>` fields, even though they were DataVault-backed.
- That field shape invited local native ownership and violated the stricter H-Phi reading of the polish mandate.

### What Was Done

- Converted SHINOBU-owned cognition, retinal, acoustic, tuning, species, claim, telemetry, and spatial-hash storage fields to `VaultArray<T>`, a lightweight wrapper over `VaultBufferHandle<T>`.
- `VaultArray<T>` resolves generation-checked `NativeArray<T>` views only at job submission or bounded helper call sites.
- Direct slot mutation uses `VaultBufferHandle<T>.GetElementAsRef`, so DTO/state writes mutate Vault memory without private `NativeArray<T>` storage.
- External threat voxel and chemical breadcrumb snapshots now use `BorrowedArray<T>` wrappers. They remain read-only borrowed world-service views and are not disposed by SHINOBU.

### Cinematic Cheats Used

- No new physical truth was added. Sight remains dot product plus midpoint threat-grid lookup.
- Spatial targeting remains fixed bucket-head/next-chain hashing, not physics overlap or pathfinding.

### Exact Microseconds Saved

Measured proof: 0 us claimed.

Static expected savings:

- Removes one ownership class of private native aliasing without adding copied buffers.
- Keeps world threat/chemical grids borrowed rather than duplicating them into SHINOBU-owned memory.

### Re-Verification

- Static private native ownership scan: PASS for edited SHINOBU files; no `private static NativeArray<T>` or `private NativeArray<T>` SHINOBU-owned fields remain.
- Targeted banned API scan: PASS for edited SHINOBU files.
- `dotnet restore Hecton8.Core.csproj -v:minimal`: up to date.
- Controlled no-deps Core build reached source compilation after the final wrapper correction and failed outside SHINOBU_10:
  - `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs(563,21)` / `(683,21)`: readonly-field assignments in the ecosystem balancer.
  - `Assets/_Project/Scripts/Construction/DroneFleetManager.cs(1190-1285)`: missing drone helper symbols including `ResolveDroneVaultBuffer`, `RegisterNativeArrayIfFallback`, and `ReleaseDroneVaultBuffer`.
- No SHINOBU compiler errors appeared in the captured output.

### Residual Risk

- Runtime proof is still absent: no Unity Play Mode, Unity Console, profiler, GCMonitor, Memory Profiler, or player build artifact.
- CSV override reload remains cold/manual and uses `File.ReadAllText`; it is not on initialization or Tick paths.

## 2026-05-17 - L1 Cache Layout Addendum

Status: PENDING VERIFICATION.

### What Was Wrong

- `CognitionInput` kept `double3 FloatingOriginOffset` behind several `float3` fields. That is bad ARM64 hygiene for a hot utility input DTO.
- `AlphaLeviathanDirective` declared `Size = 24` while its fields exceeded the declared footprint.
- The old release helpers still used `Alias`/`NativeMemorySentinel` naming even after the handle conversion, which made the H-Phi audit ambiguous.

### What Was Done

- Moved `FloatingOriginOffset`, `PlayerTargetAup`, and `PackTargetAup` to the top of `CognitionInput`.
- Preserved `CognitionInputSizeBytes = 480`.
- Expanded `AlphaLeviathanDirective` to 32 bytes and added explicit `Reserved3..Reserved7` tail padding.
- Removed stale alias/dispose/sentinel release helpers. `ReleaseVaultHandle<T>` now only clears `VaultArray<T>` handles after a completion fence.

### Cinematic Cheats Used

- No new physical simulation was added. Visibility still uses dot/range plus midpoint threat-grid lookup.

### Exact Microseconds Saved

Measured proof: 0 us claimed. This was a layout/correctness fix, not a benchmarked optimization.

### Re-Verification

- Static SHINOBU scan: PASS for no `Pack=1`, no private raw `NativeArray<T>` fields, no `NativeArray<T> Alias`, no `VaultArray<T>(NativeArray<T>)`, no `NativeParallelMultiHashMap`, no NavMesh/raycast/OverlapSphere/`Vector3.Distance`.
- `git diff --check`: PASS for touched SHINOBU files; CRLF warnings only.
- Controlled no-deps Core build reached source compilation and failed outside SHINOBU_10:
  - `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`: missing scalability dictator helper methods.
  - `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`: `DroneFleetBlackBoxEntry.Reserved0` mismatch.
- No `PredatorCognitionDomain`, `FaunaDataTemplate`, `H8Memory`, or `ApexCortexTunerWindow` compiler errors appeared in the captured output.

### Residual Risk

- Runtime proof remains absent: no Play Mode, Unity Console, profiler, GCMonitor, Memory Profiler, player build, or frame-time artifact.

## 2026-05-18 - NaN Survival / H8Dump Forensic Addendum

Status: PENDING VERIFICATION. Static/source evidence plus controlled CLI compile boundary only.

### What Was Wrong

- Several SHINOBU reciprocal/rsqrt sites still used `DdaEpsilon = 0.000001f` as a denominator guard.
- `DdaEpsilon` is useful for geometry zero tests, but it is below the current mandated `0.0001f` denominator floor.
- Leviathan cortex telemetry wrote `Dump_LEVIATHAN_CORTEX.bin`; the current blackbox mandate also requires `.h8dump` on fatal state.

### What Was Done

- Added `MathSafetyEpsilon = 0.0001f` in `PredatorCognitionDomain`.
- Re-routed SHINOBU `math.rcp/math.rsqrt` guard sites that used `DdaEpsilon` or local counts/weights through `MathSafetyEpsilon`.
- Left denominators that are pre-clamped to `>= 0.001f` or `>= 1f` intact because they are already above the mandated floor.
- Added cold writer helpers for retinal and Alpha Leviathan blackbox dumps.
- Retinal fatal dump now writes:
  - `Docs/AgentLogs/Dump_FAUNA_RETINAL_ADAPTATION.bin`
  - `Docs/AgentLogs/Dump_FAUNA_RETINAL_ADAPTATION.h8dump`
- Leviathan cortex fatal dump now writes:
  - `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin`
  - `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.h8dump`

### Cinematic Cheats Used

- No new physics truth was added.
- Sight remains dot/range utility plus one midpoint threat-grid lookup.
- The predator remains a cheap mathematical liar, not a raycast/pathfinding consumer.

### Exact Microseconds Saved

Measured proof: 0 us claimed.

Static expected effect:

- Removes NaN amplification risk from tiny reciprocal denominators.
- Adds mandated crash artifact format without adding gameplay hot-path I/O.

### Verification

- Static reciprocal guard scan: PASS for the previously unguarded/under-guarded SHINOBU `rcp/rsqrt` sites.
- Static forbidden API scan: PASS for edited SHINOBU files; no NavMesh, raycast, OverlapSphere, `Vector3.Distance`, Find/GetComponent, LINQ, or string formatting hits.
- Static `Pack=1`/private-native-alias scan: PASS for edited SHINOBU files.
- Scoped `BufferID` scan: PASS for current `PredatorCognition*` entries; no duplicate values inside the `BufferID` enum slice.
- `git diff --check`: PASS; CRLF warnings only.
- Controlled no-deps compile: BLOCKED BY EXTERNAL DEPENDENCY. Latest command:
  - `dotnet build .\Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`
- Compile blockers are outside SHINOBU_10:
  - `WorldChunkResidencyManager.cs`: `IAmbientBiotaService.IsApexInSector` missing.
  - `GlobalPhysicsStateManager.cs`: missing SHINOBU_37 physics-culling partial helpers/types.
- No `PredatorCognitionDomain`, `FaunaDataTemplate`, `H8Memory`, or `ApexCortexTunerWindow` compiler errors appeared in the captured compiler output.

### Regression Model

- CPU: no new hot-path loops; only scalar epsilon changes in existing math.
- GC: no new hot-path managed allocation. New file I/O remains cold fatal dump only.
- Memory: no new persistent buffers.
- Cadence: no cognition schedule change.
- Correctness: tiny-denominator behavior is safer; any behavioral delta is limited to near-zero vectors/counts.
- Failure modes: Unity runtime, GCMonitor, profiler, Play Mode, and player-build proof remain absent.
