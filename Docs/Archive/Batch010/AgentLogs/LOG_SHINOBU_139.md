# LOG_SHINOBU_139

## 2026-05-19 Static Implementation Report

Status: STATIC IMPLEMENTATION, COMPILE/RUNTIME PROOF PENDING

What was wrong:
- No coral-specific data-only growth engine existed in the current source surface.
- `coral_growth_rules.h8bin` is absent from `Assets/StreamingAssets`, so a hard dependency on the binary payload would break initialization.
- Legacy object-based coral generation could not be safely deleted because no bounded `CoralSpawner.cs` or `ReefGenerator.cs` exists; the broad scatter director is outside this agent's surgical domain.
- Initial rough path lacked HZB culling and needed a code-level self-audit routine, not a report-only claim.

What was done:
- Added `Assets/_Project/Scripts/World/ProceduralCoral/` runtime assembly and editor assembly.
- Added explicit ARM64 DTOs, including 128-byte `CoralBranchDTO`, 64-byte counters/telemetry/proxies, 32-byte pulse DTO, 16-byte HZB tile DTO.
- Added Vault buffer IDs `71390..71409`; all persistent NativeArray memory is Vault-owned.
- Added deterministic integer L-System expansion over Vault scratch buffers, local collision/SDF fake constraints, camera-relative matrix extraction, HZB culling, indirect draw args, shader sway scalars, bioluminescent pulse staging, capsule proxy staging, telemetry ring, binary/CSV rule loaders, blackbox dump writer, UI Toolkit tuner, layout validator, debug gizmo, and architecture self-audit.
- Added docs: `Docs/ARCHITECTURE/PROCEDURAL_CORAL_GROWTH_ENGINE.md` and a binary-ledger lane for absent `coral_growth_rules.h8bin`.

Cinematic cheats used:
- Coral current sway is a Dear Lie: CPU matrices stay stable; shader consumes `CoralGpuSwayDTO` flow/amplitude/stiffness scalars.
- Terrain collision uses a cheap seabed SDF clearance and bounded local branch overlap probes instead of mesh colliders or physics queries.
- Bioluminescence is data-only `SyncPulseDTO` staging, not light objects.
- HZB tile culling skips hidden matrices before GPU vertex work.

Exact microseconds saved, estimates pending profiler proof:
- GameObject hierarchy avoidance: ~3-15 us per 100 branches.
- Fixed-stride branch DTO cache layout: ~2-4 us per 4k branch scan.
- No per-dispatch NativeList allocation: ~8-20 us per sector generation.
- Shader sway instead of CPU matrix animation: ~65-130 us per 4k instances per frame.
- Telemetry ring write: cost ~2-5 us per generation event; dump is cold IO.
- Large `UninitializedMemory` buffers: ~30-120 us cold allocation saving depending buffer pressure.
- Count-window zero-init trim: ~55-220 us per generation by removing full branch/debug/spatial/pulse/proxy clears.
- GPU upload no-grow default: avoids surprise driver/buffer allocation during matrix upload; exact cost is driver-dependent and profiler-pending.

Compile guard:
- No dotnet build launched. CPU measured 79% then 100%; project rule forbids rebuild under >50% CPU.
- Static search found no `foreach`, `List<T>`, DTO properties, `Instantiate`, `new GameObject`, `Complete()`, `UnityEngine.Random`, `Time.deltaTime`, or `Pack=1` in the coral domain.
- Loop 5 correction: stochastic branch variation and gates now use deterministic `Unity.Mathematics.Random` seeded from stable uint hashes; render visibility random is stable across frames to avoid density flicker.
- Loop 6 correction: telemetry field is `BurstComputeUs`; dispatcher can write actual measured microseconds through `TryRecordMeasuredBurstTimeUs()`. Editor telemetry readout now hashes values and reuses a `StringBuilder`, rebuilding only when data changes.
- Loop 7 correction: `MockSectorTriggerJob` no longer includes `SimulationFrameCounter` in reef layout seed. Frame data remains in `SimulationFrame` and counter hash; persistent reef layout now matches sector hash/world seed/save record semantics.
- Loop 8 correction: `EvaluateCoralLSystemJob` and `InjectBioluminescenceNodesJob` no longer clear full branch/debug/pulse buffers. `BranchCount` and `SyncPulseCount` define the valid windows; stale slots are not consumed.
- Loop 9 correction: `ProceduralCoralGpuUploadDispatcher.UploadFromVault()` defaults to no allocation. Buffer creation/resizing must happen through explicit boot/prewarm `EnsureGraphicsResources()` or an intentional `allowAllocation:true` call.
- Loop 10 correction: `SpatialCellCount` now owns the spatial valid window, collision proxies also use `CollisionProxyCount`, and count-consuming jobs fail closed when `Counters` is absent instead of scanning full uninitialized capacity.
- Runtime asmdef references only `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages. No sibling runtime assembly reference was added.

<SELF_AUDIT agent_id="SHINOBU_139" domain="Echelon 2 World Generation / Procedural Coral Growth Engine" status="STATIC_SOURCE_COMPILE_PENDING">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Binary scan plus deterministic emergency mock rules.</TASK>
    <TASK id="02" status="PASS">No exact legacy coral spawner found; new pipeline creates zero GameObjects.</TASK>
    <TASK id="03" status="PASS">DTOs use public unmanaged fields only.</TASK>
    <TASK id="04" status="PASS">`CoralBranchDTO` explicit 128-byte layout plus editor validator.</TASK>
    <TASK id="05" status="PASS">`MockSectorTriggerJob` injects deterministic AUP/sector trigger.</TASK>
    <TASK id="06" status="PASS_WITH_NOTE">Integer L-System expansion uses Vault-owned fixed scratch buffers with NativeList semantics. Direct `NativeList<uint>` allocation was rejected because this Vault API exposes `NativeArray<T>` ownership only.</TASK>
    <TASK id="07" status="PASS">`ConstrainCoralGrowthJob` applies local collision pruning and seabed SDF fake.</TASK>
    <TASK id="08" status="PASS">Dear Lie shader sway; no per-frame matrix animation.</TASK>
    <TASK id="09" status="PASS">Camera-AUP-relative extraction writes `float4x4` to Vault with `UnsafeUtility.MemCpy`.</TASK>
    <TASK id="10" status="PASS">Continuous `GlobalQualityWeight` resolves to `CoralPaddedCounterDTO.EffectiveQualityWeight`, then drives depth, density, limits, constraints, pulse/proxy/sway behavior.</TASK>
    <TASK id="11" status="PASS">Tip branches emit `SyncPulseDTO` records into Vault.</TASK>
    <TASK id="12" status="PASS">Root AUP maps to sector hash; save surface is seed/hash/payload hash only.</TASK>
    <TASK id="13" status="PASS">Root/thick branches stage lightweight `CapsuleColliderDTO` records.</TASK>
    <TASK id="14" status="PASS">All Burst jobs use deterministic float mode; stochastic gates use deterministic `Unity.Mathematics.Random`; reef layout seed excludes `SimulationFrameCounter`, which is retained only for telemetry/counter hashes; no non-deterministic RNG/time APIs.</TASK>
    <TASK id="15" status="PASS">Large Vault buffers request `NativeArrayOptions.UninitializedMemory`; boot hydration avoids blanket clears for branch/scratch/spatial/matrix/proxy/pulse/debug/CSV buffers, and generation relies on logical counts as valid windows.</TASK>
    <TASK id="16" status="PASS">300-entry telemetry ring and `Dump_CORAL_ARCHITECT.bin` writer exist.</TASK>
    <TASK id="17" status="PASS">UI Toolkit tuner exists with sliders/buttons and telemetry readout.</TASK>
    <TASK id="18" status="PASS">CSV parser reads bytes into Vault scratch, hashes tokens to opcodes, stages parsed rules in stack scratch, and commits only after valid records exist.</TASK>
    <TASK id="19" status="PASS">Editor gizmo reads Vault debug segments and draws branch x-ray.</TASK>
    <TASK id="20" status="PASS_STATIC">Job-chain audit and cold architecture audit routine exist; compile/runtime proof pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <CoralBranchDTO size="128" alignment="16-byte-multiple">
      <Field name="LocalMatrix" offset="0" size="64"/>
      <Field name="PrefabHash" offset="64" size="4"/>
      <Field name="GenerationDepth" offset="68" size="4"/>
      <Field name="SectorAUP" offset="72" size="24"/>
      <Field name="Stiffness" offset="96" size="4"/>
      <Field name="Radius" offset="100" size="4"/>
      <Field name="StateFlags" offset="104" size="4"/>
      <Field name="ParentIndex" offset="108" size="4"/>
      <Field name="StableId" offset="112" size="4"/>
      <Field name="SectorHash" offset="116" size="4"/>
      <Field name="_pad0" offset="120" size="4"/>
      <Field name="_pad1" offset="124" size="4"/>
      <Math>64+4+4+24+4+4+4+4+4+4+4+4 = 128; 128 % 16 = 0.</Math>
    </CoralBranchDTO>
    <CoralPaddedCounterDTO size="64" false_sharing_guard="single_cache_line">
      <Field name="BranchCount" offset="0" size="4"/>
      <Field name="InstructionCount" offset="4" size="4"/>
      <Field name="DepthReached" offset="8" size="4"/>
      <Field name="CollisionProxyCount" offset="12" size="4"/>
      <Field name="RenderMatrixCount" offset="16" size="4"/>
      <Field name="SyncPulseCount" offset="20" size="4"/>
      <Field name="FaultFlags" offset="24" size="4"/>
      <Field name="StateHash" offset="28" size="4"/>
      <Field name="TelemetryCursor" offset="32" size="4"/>
      <Field name="CsvRuleCount" offset="36" size="4"/>
      <Field name="ActiveRuleCount" offset="40" size="4"/>
      <Field name="BinaryRuleCount" offset="44" size="4"/>
      <Field name="TipCount" offset="48" size="4"/>
      <Field name="PrunedCount" offset="52" size="4"/>
      <Field name="SpatialCellCount" offset="56" size="4"/>
      <Field name="EffectiveQualityWeight" offset="60" size="4"/>
      <Math>15 integer fields * 4 bytes + 1 float field * 4 bytes = 64; one counter record per cache line.</Math>
    </CoralPaddedCounterDTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight=0.3`, the resolved `EffectiveQualityWeight` in `CoralPaddedCounterDTO` drives the full chain: L-System max depth lerps toward 2-3, instruction limit toward 64, branch limit toward 48, render density toward 0.22, pulse density toward 0.12, proxy depth toward trunk-only, sway amplitude toward low scalar values, and overlap probes toward the short bound. The algorithm preserves macro silhouette while shedding tip density, debug matrix count, VFX pulses, and collision proxy count.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_arrays="0">
    <Buffer id="71390" name="Rules"/>
    <Buffer id="71391" name="InstructionScratchA"/>
    <Buffer id="71392" name="InstructionScratchB"/>
    <Buffer id="71393" name="Branches"/>
    <Buffer id="71394" name="TurtleStack"/>
    <Buffer id="71395" name="SpatialCells"/>
    <Buffer id="71396" name="RenderMatrices"/>
    <Buffer id="71397" name="IndirectArgs"/>
    <Buffer id="71398" name="SectorTriggers"/>
    <Buffer id="71399" name="CollisionProxies"/>
    <Buffer id="71400" name="SyncPulses"/>
    <Buffer id="71401" name="TelemetryRing"/>
    <Buffer id="71402" name="TelemetryCursor"/>
    <Buffer id="71403" name="Tuning"/>
    <Buffer id="71404" name="CsvScratch"/>
    <Buffer id="71405" name="Counters"/>
    <Buffer id="71406" name="DebugSegments"/>
    <Buffer id="71407" name="GpuSway"/>
    <Buffer id="71408" name="SelfAudit"/>
    <Buffer id="71409" name="HzbTiles"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias status="PASS">All NativeArray fields in Burst jobs are annotated with `[NoAlias]` where mutable/read-only arrays are passed.</NoAlias>
    <JobGraph>input -> EvaluateCoralLSystemJob -> ConstrainCoralGrowthJob -> InjectBioluminescenceNodesJob -> StageCollisionProxiesJob -> ExtractCoralRenderMatricesJob -> CoralSelfAuditJob -> output</JobGraph>
    <MockGraph>input -> MockSectorTriggerJob -> output</MockGraph>
    <BlockingCompleteCalls status="PASS">No `JobHandle.Complete()` calls in the coral domain.</BlockingCompleteCalls>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Runtime asmdef references `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`. It does not reference sibling world, VFX, physics, AI, ecosystem, or presentation runtime assemblies. Editor asmdef references same-domain coral runtime plus Core/editor-safe dependencies only.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <Sway>Before: CPU per-frame branch matrix animation O(n). After: CPU O(0) per-frame for sway, shader vertex displacement consumes `CoralGpuSwayDTO` scalars.</Sway>
    <Collision>Before: Unity physics/mesh collider creation scales with objects and scene registration. After: generation-time bounded local probes O(n*k) plus data-only capsule staging.</Collision>
    <Rendering>Before: blind matrix submission O(n) GPU vertex waste behind terrain. After: O(n) CPU HZB tile test before matrix emission, reducing hidden GPU work.</Rendering>
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <StaticSearch>No DTO properties, `foreach`, `List&lt;T&gt;`, `Instantiate`, `new GameObject`, `Complete()`, `UnityEngine.Random`, `Time.deltaTime`, or `Pack=1` found in coral domain.</StaticSearch>
    <Compile>NOT RUN: no generated coral/domain csproj exists; broad build was not launched under the project CPU/build-gate policy. Unity import, Burst compile, Play Mode, profiler, and Frame Debugger proof remain pending.</Compile>
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 Loop 11 GPU Upload Hardening

What was wrong: `ProceduralCoralGpuUploadDispatcher.UploadFromVault()` used `LockBufferForWrite` with straight-line unlocks. If the editor/runtime graphics path threw between lock and unlock, the buffer lifetime could remain poisoned. `CoralGpuSwayDTO.SectorHash` was also left as zero, so Vault/debug consumers had a state hash but no owner-local sector fact.

What was done: Matrix and indirect-args mapped writes now unlock through `try/finally`. `ExtractCoralRenderMatricesJob` captures the first live branch sector hash from the authoritative `[0, BranchCount)` window with an explicit boolean capture flag, not a zero sentinel, and writes it to `CoralGpuSwayDTO.SectorHash`.

Cinematic cheat retained: current sway remains GPU-side scalar deformation; CPU matrices are not animated per frame.

Exact microseconds saved: 0 claimed. This pass is failure-mode hardening and owner-proof cleanup, not a speed claim. It prevents rare graphics-buffer lock corruption and avoids downstream missing-owner branching.

Verification: static forbidden-pattern scan is clean for DTO properties, `Instantiate`, `new GameObject`, `List<T>`, `foreach`, `.Complete()`, `UnityEngine.Random`, `Time.deltaTime`, and `Pack=1`. Count-window scan is clean for `Branches.Length` fallback and full branch/debug/spatial/pulse/proxy clears. Runtime LINQ/string-format scan is clean. Runtime asmdef still references only Core Contracts, Core Memory, and Unity Burst/Collections/Jobs/Mathematics. `git diff --check` reports only existing LF-to-CRLF normalization warnings. `dotnet build` was not launched: no owner-local ProceduralCoral csproj exists, and broad `Hecton8.World.Dots.csproj` is not valid owner-local proof.

## 2026-05-19 Loop 12 Editor Facade Churn Pass

What was wrong: the editor tuner derived project root with `Application.dataPath.Substring(...)` every CSV poll/button path. This is not gameplay hot path, but it is avoidable allocation in the requested human-control facade.

What was done: cached the project root string per `ProceduralCoralTunerWindow` instance. Telemetry label churn remains bounded by the existing telemetry hash gate.

Cinematic cheat used: none; this is editor-facade hygiene.

Exact microseconds saved: runtime 0. Editor path saves one substring allocation per CSV poll/button access; exact editor timing not measured.

## 2026-05-19 Loop 13 Layout Proof Pass

What was wrong: layout validation proved `CoralPaddedCounterDTO` and `CoralGpuSwayDTO` sizes but not the offsets now used as contracts for count windows and sway owner metadata.

What was done: added explicit offset checks for `CoralPaddedCounterDTO.BranchCount`, `CollisionProxyCount`, `RenderMatrixCount`, `SyncPulseCount`, `SpatialCellCount`, plus `CoralGpuSwayDTO.FlowAndAmplitude`, `BoundsAndDensity`, `FaultAndFrame`, `SectorHash`, and `StateHash`.

Cinematic cheat used: none; this is ARM64 proof hardening.

Exact microseconds saved: runtime 0. Editor validation becomes stricter with reflection-only cold cost.

## 2026-05-19 Loop 14 Transactional Rule Hydration

What was wrong: CSV/H8BIN parser code cleared the live rule table before proving that the incoming file had valid records. Empty or corrupt tuning input could erase the deterministic fallback grammar and push the next generation into `FaultNoRules`.

What was done: binary and CSV rule parsers now stage into a 16-record stackalloc scratch table and commit to Vault only after `parsedCount > 0`. Commit writes one default sentinel after the active rules so stale tail rules are not scanned.

Cinematic cheat used: none; this is data integrity hardening.

Exact microseconds saved: runtime generation 0. Failure path avoids a no-rule generation pass and preserves the last valid grammar.

## 2026-05-19 Loop 15 Boot Zero-Init Purge

What was wrong: first hydration still called `ClearArray()` over large uninitialized buffers. That preserved memset cost despite count-window ownership.

What was done: boot hydration now writes only small sentinels, fallback rules, and default tuning. Instruction scratch, branches, turtle stack, spatial cells, render matrices, proxies, pulses, debug segments, telemetry ring, CSV scratch, and HZB tiles are not blanket-cleared at boot.

Cinematic cheat used: none; this is memory sovereignty/zero-init enforcement.

Exact microseconds saved: not measured. Static removal covers the first-hydration memset of high-capacity branch/matrix/scratch/proxy/pulse/debug/CSV buffers; profiler proof remains pending.

## 2026-05-19 Loop 16 Single-Read CSV Hot Reload

What was wrong: `TryPollCsvRules()` read CSV bytes for hash comparison, then called `TryLoadCsvRules()` and read the file again for parsing. The committed payload could differ from the hashed payload if the file changed between reads.

What was done: added `TryCommitCsvRules()`; load and poll paths now commit from the exact Vault scratch bytes already read.

Cinematic cheat used: none; this is content-route hardening.

Exact microseconds saved: one CSV open/read on changed poll. No runtime generation claim.

## 2026-05-19 Loop 17 Debug Hash Sentinel Removal

What was wrong: editor gizmo skipped debug segments with `SectorHash == 0u`, even though zero can be a valid folded sector hash.

What was done: removed the sentinel check and used the existing `Counters.BranchCount` valid window as the sole authority.

Cinematic cheat used: none; this is debug correctness.

Exact microseconds saved: runtime 0; editor removes one branch per segment draw.

## 2026-05-19 Loop 18 Cold Self-Audit Sector Hash Capture

What was wrong: `TryRunArchitectureAudit()` left `CoralSelfAuditResultDTO.SectorHash` at zero while the job-chain audit populated it.

What was done: cold audit now captures the first live branch sector hash with a boolean capture flag and writes it to the audit result.

Cinematic cheat used: none; this is forensic consistency.

Exact microseconds saved: 0. Adds one cold audit branch and removes an ambiguous zero-owner proof.

## 2026-05-19 Loop 19 Zero Quality Fallback Fix

What was wrong: `EvaluateCoralLSystemJob` treated `GlobalQualityWeight == 0.0f` as missing and fell back to tuning quality. This broke the required continuous continuum at the Minimum Survival endpoint.

What was done: quality source selection now uses `trigger.Flags` for initialization proof. A valid trigger can carry `0.0f`.

Cinematic cheat used: none; this restores the scalability law.

Exact microseconds saved: not measured. The zero-quality path can now collapse to the intended minimum branch/instruction density instead of being inflated by fallback quality.

## 2026-05-19 Loop 20 Cold Audit Utilization Window Fix

What was wrong: cold audit divided live branches by full Vault branch capacity, not the logical generated branch window.

What was done: `TryRunArchitectureAudit()` now computes utilization as `live / BranchCount`, matching `CoralSelfAuditJob`.

Cinematic cheat used: none; this is forensic metric correctness.

Exact microseconds saved: 0. Audit data no longer penalizes low-quality shallow reefs for unused reserve capacity.

## 2026-05-19 Loop 21 Effective Quality Route Unification

What was wrong: the L-system stage accepted exact trigger quality `0.0f`, but downstream constraint/render/pulse/proxy stages still recomputed quality from `Tuning[0]`. That made a sector-trigger quality override only partially true.

What was done: `CoralPaddedCounterDTO` now carries `EffectiveQualityWeight` at offset 60. Generation writes the resolved quality once; downstream jobs consume that counter-owned scalar through `ResolveEffectiveQuality()`. Layout validation checks the new offset, and cold audit flags non-finite effective quality.

Cinematic cheat used: none; this preserves the continuous scalability law across existing visual fakes.

Exact microseconds saved: no measured speed claim. Cost is one 4-byte read per downstream job from the existing 64-byte counter; the gain is correctness and elimination of duplicate quality ownership.

Verification: forbidden hot-path pattern scan clean; quality-regression scan found no downstream `math.saturate(tuning.GlobalQualityWeight)` fallback; zero-init tail scan clean; scoped `git diff --check` clean except CRLF warnings; direct trailing-whitespace scan clean for tracked and untracked SHINOBU_139 files. No build launched: CPU measured 74%, above the project gate, and no owner-local ProceduralCoral csproj exists.

## 2026-05-19 Loop 22 NaN Vaccination And Rule Scalar Route

What was wrong: fallback branches could still leave poisoned turtle/debug local state alive, and constraint fallback could continue overlap math with the stale non-finite `local`. CSV/H8BIN rule angle/length/radius scalars were also hydrated but underused by the interpreter.

What was done: added finite-first scalar/quaternion helpers, reset turtle start/end/mid on non-finite branch fallback, reloaded `local` after constraint fallback, skipped non-finite pulse/proxy/audit candidates, and hardened cold tuning sanitizers. Rule scalar ingestion now clamps angle/length/radius, and `InterpretStream()` applies those scalars per opcode to branch length, radius decay, and turn/pitch/roll angles.

Cinematic cheat used: existing Dear Lie remains intact: CPU still publishes stable matrices and bounded scalar data; shader sway carries the visual motion. This pass prevents invalid CPU data from reaching that fake.

Exact microseconds saved: no speed claim. Added finite checks and one capped rule lookup per interpreted opcode; cost is deterministic and bounded. The gain is preventing NaN propagation into render matrices, collision proxies, sync pulses, and debug geometry.

Verification: forbidden pattern scan clean; NaN-regression scan clean for old tuning math and trigger-quality fallbacks; zero-init tail scan clean; direct trailing-whitespace scan clean; scoped `git diff --check` clean except CRLF warnings. No build launched: CPU measured 100%, above the project gate.

## 2026-05-19 Loop 23 Spatial Cell Valid Window Compaction

What was wrong: `SpatialCellCount` advertised a dense valid window, but the job wrote spatial cells at branch indices. Pruned/dead branches could leave stale cell records inside the consumer-visible range.

What was done: `ConstrainCoralGrowthJob` now uses a compact `spatialWrite` cursor. Only live branches write `CoralSpatialCellDTO`, and `SpatialCellCount` equals the compact write count.

Cinematic cheat used: none; this is count-window correctness for the local spatial hash fake.

Exact microseconds saved: no speed claim. Avoids stale downstream occupancy reads without restoring full-buffer clears.

## 2026-05-19 Loop 24 HZB Precheck NaN Guard

What was wrong: matrix extraction sanitized matrices only after HZB culling and passed raw `branch.Radius` into the occlusion bias. A corrupted radius could reach HZB math before the branch was rejected.

What was done: `ExtractCoralRenderMatricesJob` now rejects non-finite branch matrices before HZB and clamps radius through `SafePositive()` before `IsOccluded()`.

Cinematic cheat used: HZB remains the cheap visibility fake; this patch hardens its inputs instead of adding physics or renderer objects.

Exact microseconds saved: no speed claim. Adds a bounded finite check and scalar clamp to prevent NaN cull inputs before Vault matrix publication.

Verification: forbidden hot-path pattern scan clean; runtime allocation/string scan clean; NaN-regression scan clean; spatial-window regression scan clean; runtime code line-length check clean; direct trailing-whitespace scan clean; scoped `git diff --check` passed with CRLF warnings only. No build launched: CPU measured 100%, above the project gate, and no owner-local ProceduralCoral csproj exists.

## 2026-05-19 Loop 25 Telemetry Overwrite NaN Guard

What was wrong: dispatcher-facing measured Burst timing could write NaN/Infinity into `CoralGenerationTelemetryEntry.BurstComputeUs`, making the blackbox ring itself non-finite.

What was done: `TryRecordMeasuredBurstTimeUs()` now finite-checks the measurement, writes `0f` on invalid input, and marks the telemetry row with `FaultNonFinite`.

Cinematic cheat used: none; this is forensic hardening.

Exact microseconds saved: no speed claim. Adds one finite check on a cold measurement overwrite path and keeps crash dumps parseable.

Verification: forbidden hot-path pattern scan clean; runtime allocation/string scan clean; spatial/HZB regression scan clean; direct trailing-whitespace scan clean; scoped `git diff --check` passed with CRLF warnings only. Targeted source read confirmed `math.max(burstComputeUs, 0f)` is reachable only in the finite branch. No build launched: CPU measured 100%, above the project gate.

<SELF_AUDIT_UPDATE loop="25" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="PASS_HARDENED">Spatial cells now use a compact live-write cursor; `SpatialCellCount` is no longer a branch-count alias after pruning.</TASK>
    <TASK id="09" status="PASS_HARDENED">Matrix extraction rejects non-finite branch matrices and clamps branch radius before HZB occlusion bias math.</TASK>
    <TASK id="16" status="PASS_HARDENED">Telemetry measured-time overwrite rejects NaN/Infinity, writes finite zero, and raises `FaultNonFinite` in the row.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <STRUCT_LAYOUT_VERIFICATION delta="unchanged">
    <CoralPaddedCounterDTO size="64" effectiveQualityOffset="60" spatialCellCountOffset="56"/>
    <CoralBranchDTO size="128" localMatrixOffset="0" sectorAupOffset="72" tailPadding="120,124"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE delta="unchanged">`EffectiveQualityWeight` remains the single downstream quality fact for constraint, render, pulse, and proxy jobs.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">Loop 23-25 added no persistent private allocations and no new Vault IDs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH delta="unchanged">Burst job NativeArray fields remain `[NoAlias]`; jobs still return chained `JobHandle` values without `.Complete()`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Runtime asmdef still has no direct sibling runtime references; build not launched because CPU measured 100% and no owner-local ProceduralCoral csproj exists.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION delta="hardened">HZB remains the visibility fake and shader-side sway remains the current fake; latest work only prevents poisoned CPU data from reaching those fakes.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Loop 27 Self-Audit Solver Fault Propagation

What was wrong: final `CoralSelfAuditResultDTO.Flags` could drop faults already recorded in `CoralPaddedCounterDTO.FaultFlags`.

What was done: `CoralSelfAuditJob` now seeds its local fault mask from the counter before OR-ing audit-local findings.

Cinematic cheat used: none; this is blackbox proof integrity.

Exact microseconds saved: no speed claim. Adds no new loop work; it preserves the existing counter fault mask.

<SELF_AUDIT_UPDATE loop="27" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="20" status="PASS_HARDENED">Final self-audit result now carries solver faults plus audit-local faults in one route.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">No new memory or buffer IDs were introduced.</H_PHI_VAULT_STATUS>
</SELF_AUDIT_UPDATE>

Verification: forbidden hot-path pattern scan clean; runtime allocation/string scan clean; audit/spatial/HZB regression scan clean with `CoralSelfAuditJob` confirmed to seed from `counter.FaultFlags`; runtime code line-length check clean; direct trailing-whitespace scan clean; scoped `git diff --check` passed with CRLF warnings only. No build launched: CPU measured 100%, above the project gate.

## 2026-05-19 Loop 28 Editor Layout Proof Widened

What was wrong: layout validator still allowed size-correct but offset-wrong drift in rule scalar, telemetry, counter fault, and self-audit DTO fields.

What was done: added explicit `UnsafeUtility.GetFieldOffset` checks for `CoralLSystemRuleDTO`, `CoralGenerationTelemetryEntry`, `CoralPaddedCounterDTO.FaultFlags`, and `CoralSelfAuditResultDTO` critical fields.

Cinematic cheat used: none; this is ARM64 ABI proof.

Exact microseconds saved: runtime 0. Prevents an editor-time layout drift from becoming a Quest/ARM64 runtime fault.

<SELF_AUDIT_UPDATE loop="28" agent="SHINOBU_139">
  <STRUCT_LAYOUT_VERIFICATION delta="hardened">Editor validation now proves critical offsets beyond struct size for rule scalars, telemetry faults, counter faults, and self-audit output.</STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">No runtime memory route changed.</H_PHI_VAULT_STATUS>
</SELF_AUDIT_UPDATE>

Verification: validator-offset scan confirmed all new critical offset checks; forbidden hot-path pattern scan clean; runtime allocation/string scan clean; runtime code line-length check clean; direct trailing-whitespace scan clean; scoped `git diff --check` passed with CRLF warnings only. No build launched: CPU measured 100%, above the project gate.

## 2026-05-19 Loop 29 GPU Dispatcher Count And Sway Guards

What was wrong: upload count was cast from uint to int before clamping, and shader sway globals accepted raw float4 lanes.

What was done: added uint-safe count clamping, minimum vertex-count repair, active instance count tracking for zero-draw suppression, and finite fallback conversion before shader global publication.

Cinematic cheat used: shader-side sway remains the Dear Lie; this pass only hardens the scalar lane feeding it.

Exact microseconds saved: no measured claim. Zero-instance frames now skip `DrawProceduralIndirect`; corrupt counts no longer become malformed submissions.

<SELF_AUDIT_UPDATE loop="29" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="08" status="PASS_HARDENED">Shader sway global vectors are finite-gated at the upload facade.</TASK>
    <TASK id="09" status="PASS_HARDENED">Indirect instance count is uint-clamped and zero-instance draws are suppressed.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <DEAR_LIE_CONFIRMATION delta="hardened">CPU still avoids per-frame sway matrix mutation; dispatcher now prevents NaN scalar feed into that shader fake.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Loop 30 GPU Prewarm Cap And Rollback Guard

What was wrong: explicit GPU prewarm accepted arbitrary capacity and did not roll back partial `GraphicsBuffer` creation on constructor failure.

What was done: `EnsureGraphicsResources()` now clamps capacity to `MaxRenderMatrices`, catches cold allocation failure, releases partial buffers, and resets active instance count.

Cinematic cheat used: none; this hardens the BRG/indirect draw resource facade.

Exact microseconds saved: no runtime speed claim. Prevents oversized prewarm and partial driver resource leaks on allocation failure.

<SELF_AUDIT_UPDATE loop="30" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="09" status="PASS_HARDENED">GPU prewarm is clamped to coral matrix capacity and fails closed.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">No new Vault buffers or native arrays were introduced.</H_PHI_VAULT_STATUS>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Loop 26 Self-Audit Owner Hash And Radius Guard

What was wrong: job-chain self-audit reported the last live branch sector hash while cold audit reported the first live sector hash. It also used raw branch radii in overlap probes.

What was done: `CoralSelfAuditJob` now captures the first live sector hash, checks non-finite effective quality and branch radius, and uses sanitized radii for overlap depth.

Cinematic cheat used: none; this is forensic correctness.

Exact microseconds saved: no speed claim. Adds bounded checks to prevent poisoned audit output and route drift between hot and cold proof paths.

<SELF_AUDIT_UPDATE loop="26" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="20" status="PASS_HARDENED">Job-chain audit now matches cold audit sector ownership and guards effective quality plus branch radius finiteness.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH delta="unchanged">`CoralSelfAuditJob` remains the final scheduled job in the chain and still writes only `SelfAudit[0]`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">No new persistent memory, handles, or buffer IDs were introduced.</H_PHI_VAULT_STATUS>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Loop 30 Static Verification Closure

What was wrong: Loop 30 had behavior and rationale recorded, but the state file still marked static proof pending.

What was done: reran scoped static verification after the dispatcher line-wrap cleanup and moved the on-disk state to static source verified while keeping compile pending behind the CPU gate.

Cinematic cheat used: unchanged. CPU still emits stable instance matrices; current sway remains shader-side scalar deformation.

Exact microseconds saved: no measured claim. Cold prewarm is bounded; zero-instance draw suppression remains the only direct draw-call reduction in this polish pass.

<SELF_AUDIT_UPDATE loop="30" agent="SHINOBU_139">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="09" status="PASS_VERIFIED">GPU upload and prewarm path is statically guarded against corrupt counts, zero draws, non-finite sway lanes, oversized prewarm capacity, and partial buffer allocation leaks.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <H_PHI_VAULT_STATUS private_native_arrays="0" new_buffers="0">No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` was introduced. Vault buffer IDs remain `71390..71409`.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD status="PENDING">No `dotnet build` was launched because CPU measured 100 percent and no owner-local ProceduralCoral csproj exists.</COMPILE_GUARD>
  <STATIC_PROOF>Forbidden hot-path pattern scan clean; runtime allocation/string scan clean; dispatcher regression scan clean; dispatcher line-length scan clean; direct trailing-whitespace scan clean; scoped `git diff --check` passed with CRLF warnings only.</STATIC_PROOF>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Loop 31 Burst And Compile-Wall Audit

What was wrong: no new defect found; this was a re-audit of the two boundaries most likely to rot under parallel-agent pressure.

What was done: verified that seven coral jobs carry deterministic Burst compile flags, the runtime asmdef references only `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages, and sibling-domain `using` scan returned no hits.

Cinematic cheat used: none.

Exact microseconds saved: no runtime speed claim. This preserves dev iteration isolation and Burst determinism rather than changing frame cost.

<SELF_AUDIT_UPDATE loop="31" agent="SHINOBU_139">
  <COMPILE_GUARD status="PASS_STATIC">Runtime asmdef has zero sibling runtime references; editor asmdef references this domain runtime plus core/editor-safe dependencies only.</COMPILE_GUARD>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH status="UNCHANGED">No job dependency route changed. Job structs remain the seven deterministic Burst jobs already chained by the Vault facade.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <STATIC_PROOF>Matched seven exact deterministic Burst attributes and seven job structs; sibling-domain using scan returned no hits. No `dotnet build` was launched because CPU measured 100 percent.</STATIC_PROOF>
</SELF_AUDIT_UPDATE>
