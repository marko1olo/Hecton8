# LOG_SHINOBU_61

## 2026-05-18 - Predictive Apex Aggression / Sweet Lie LOS

What was wrong:
- Apex predator cognition had no SHINOBU_61-specific math-only surface for predictive intercept, sweet-lie LOS, quality-scaled ambush nodes, and black-box forensic output.
- No active `apex_predator_curves.h8bin` evidence was found in the current binary ledger/archive recon, so any legacy curve claim would be fake.
- Unity-generated project files were stale; no `Hecton8.AI.Cognition.csproj` included the new asmdef source set, so verification had to use Unity Bee/Roslyn response files directly.

What was done:
- Added `ShinobuApexBrainContracts.cs`, `ShinobuApexBrainJobs.cs`, and `ShinobuApexBrainVault.cs` under `Assets/_Project/Scripts/AI/Cognition`.
- Enabled unsafe code in `Hecton8.AI.Cognition.asmdef` only for `UnsafeUtility.MemClear` spawn reset and unmanaged vault access.
- Added `Hecton8.AI.Cognition.Editor.asmdef` and `LeviathanCortexTunerWindow.cs`.
- Created `Docs/Tasks/Status_SHINOBU_61.md` and `Docs/AgentLogs/Rationale_SHINOBU_61.md`.
- Runtime compile check passed through Unity Roslyn `Hecton8.AI.Cognition.rsp` plus SHINOBU sources.
- Editor compile check passed through filtered `Hecton8.Editor.rsp` plus `LeviathanCortexTunerWindow.cs`; Roslyn analyzer emitted USG0001 info only.

Cinematic Cheats used:
- Sweet-lie LOS: player-forward dot product + distance falloff + analytic SDF wall shadow + spatial hash canyon bias. No raycast, no linecast, no NavMesh.
- Slither fake: one analytic cave SDF plus quality-weighted head/mid/tail samples. Animation is expected to sell body fitting; authority only needs a stable potential field.
- Breach fake: `MockCombatDamageSignal` sends mathematical impact data to WFC/base systems instead of solving base deformation in apex cognition.

Exact Microseconds saved:
- Sweet-lie LOS vs full-body ray/line checks: estimated 60-120 us per active leviathan.
- SDF slither fake vs 8-16 physics rays/capsule checks: estimated 80-140 us per active leviathan.
- Continuous low-quality collapse from 16 to 2 nodes/head-only: estimated 45-90 us per active leviathan.
- No managed state machine/properties/log strings: estimated 10-25 us per 10-row batch.
- Signal DTO routing vs sibling-domain calls: estimated 20-40 us per frame.
- Total expected low-end i3/MX350 hot-path saving: roughly 180-320 us per active leviathan versus naive raycast/NavMesh/OOP control. Profiler proof is pending.

<SELF_AUDIT agent_id="SHINOBU_61" status="IMPLEMENTED_ROSLYN_COMPILE_PASSED_UNITY_PLAYMODE_PENDING">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">Archive/ledger recon found no active apex curve h8bin; emergency mock stats implemented.</TASK>
    <TASK id="02" result="PASS">Utility matrix phases replace OOP state classes.</TASK>
    <TASK id="03" result="PASS">Hot DTOs are public fields; ref state mutation API exists.</TASK>
    <TASK id="04" result="PASS">ApexStateDTO is explicit 64B; influence/signals/telemetry aligned.</TASK>
    <TASK id="05" result="PASS">MockPlayerAUP and Burst advance job implemented.</TASK>
    <TASK id="06" result="PASS">Predictive intercept formula implemented in local float3 space.</TASK>
    <TASK id="07" result="PASS">AcousticEchoTap fixed scan and AcousticMemoryHash implemented.</TASK>
    <TASK id="08" result="PASS">MockWorldSampler SDF slither steering implemented.</TASK>
    <TASK id="09" result="PASS">Sweet-lie LOS dot-product/SDF/hash fake implemented.</TASK>
    <TASK id="10" result="PASS">AggressionLevel buildup and ApexProximitySignal implemented.</TASK>
    <TASK id="11" result="PASS">GlobalQualityWeight lerps node count and SDF sample weights.</TASK>
    <TASK id="12" result="PASS">MockCombatDamageSignal implemented for WFC breach handoff.</TASK>
    <TASK id="13" result="PASS">IK_BiteTarget emitted as local float3.</TASK>
    <TASK id="14" result="PASS">Abyssal Trench biome hash/flag multiplies aggression.</TASK>
    <TASK id="15" result="PASS">GlobalPanicSignal emitted on strike.</TASK>
    <TASK id="16" result="PASS">DataVault buffers use UninitializedMemory where required; spawn reset uses MemClear.</TASK>
    <TASK id="17" result="PASS">300-frame telemetry ring and binary dump helpers implemented.</TASK>
    <TASK id="18" result="PASS">Leviathan Cortex Tuner EditorWindow implemented.</TASK>
    <TASK id="19" result="PASS">CSV scratch parser hashes apex_predator_stats.csv keys into vault tuning.</TASK>
    <TASK id="20" result="PASS">Editor scene gizmo draws red intercept sphere and yellow acoustic rings.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <ApexStateDTO size="64" alignment="8/16 compatible">
      <field name="AUP" offset="0" size="24" type="double3" />
      <field name="Velocity" offset="24" size="12" type="float3" />
      <field name="AggressionLevel" offset="36" size="4" type="float" />
      <field name="TargetHash" offset="40" size="4" type="uint" />
      <field name="AcousticMemoryHash" offset="44" size="4" type="uint" />
      <field name="Stamina" offset="48" size="4" type="float" />
      <field name="_padAlign0" offset="52" size="4" type="uint" />
      <field name="_pad0" offset="56" size="8" type="ulong" />
      <math>24+12+4+4+4+4+4+8 = 64 bytes, one L1 cache line target. No packed runtime layout.</math>
    </ApexStateDTO>
    <FalseSharing>Concurrent scratch rows and telemetry rows are 64B/128B explicit records; no atomic counter struct added.</FalseSharing>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    At GlobalQualityWeight below 0.3, node evaluation collapses toward 2 nodes, tail SDF is bypassed, midsection weight trends to zero, and visual overkill scalars shrink. Between middle and high weights, node count interpolates continuously and SDF samples fade in. At 1.0, the job evaluates up to 16 ambush nodes plus head/mid/tail steering.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PrivatePersistentArrays>ZERO in new runtime classes.</PrivatePersistentArrays>
    <VaultBufferHandles>ApexState=70609, MockPlayerAup=70610, AcousticEchoTap=70611, Tuning=70612, EmergencyStats=70613, MockWorldSampler=70614, Output=70615, ProximitySignal=70616, CombatDamageSignal=70617, PanicSignal=70618, InfluenceNodes=70619, TelemetryRing=70626, TelemetryCursor=70627, CsvScratch=70628, AmbushNodeScratch=70629.</VaultBufferHandles>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Applied to NativeArray fields in MockPlayerAupAdvanceJob and ApexBrainJob.</NoAlias>
    <InputJobHandle>TrySchedule consumes caller inputDependency.</InputJobHandle>
    <OutputJobHandle>TrySchedule returns scheduled ApexBrainJob handle without forced Complete.</OutputJobHandle>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Runtime asmdef references only Core.Contracts, Core.Memory, Burst, Collections, Jobs, Mathematics. No sibling runtime domain reference. Editor asmdef references AI.Cognition for tooling only.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: O(rays * physics_scene_complexity) LOS/body-fit plus path query overhead. After: O(nodes + acoustic_taps) pure math, where nodes lerp 2..16 and acoustic taps are fixed capped rows. Specific fake: dot-product visibility plus SDF/hash shadow instead of raycast occlusion.
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    Runtime Roslyn compile passed. Editor Roslyn compile passed with USG0001 analyzer info only. Forbidden API scan returned no SHINOBU matches. Unity import, Play Mode, Burst inspector, and profiler measurements remain pending.
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-18 - Titanium Hardening Pass

What was wrong:
- The ambush node scratch surface was not literal enough. `ApexInfluenceNode` carried local positions, but the XML explicitly requested a vault `NativeArray<float3>` scratchpad.
- `InterceptComputeTimeMs` was written as zero, which hid cost drift instead of tracking it.
- The dump header did not include an explicit endianness marker.
- Quality gates used interpolation and polynomial smoothing, but the hardware mandate explicitly demanded `math.step` as part of the continuum.

What was done:
- Added `AmbushNodeScratch` BufferID `70629`, resolved through DataVault, passed into `ApexBrainJob`, and filled with every evaluated ambush candidate.
- Added `math.step` gates for low-quality collapse and mid/tail SDF sample activation.
- Added deterministic `InterceptComputeTimeMs` estimate from evaluated nodes, acoustic tap cap, SDF sample gates, and `GlobalQualityWeight`.
- Added `TryRecordTelemetryHeartbeat(buffers, frame, projectRoot)` overload to dump the black box immediately after a completed fault frame.
- Added dump endian marker `0x01020304`.
- Recompiled runtime and editor checks successfully.

Cinematic Cheats used:
- Same sweet-lie LOS and analytic SDF fake, now with exact `float3` scratchpad positions for downstream consumers.

Exact Microseconds saved:
- Low-quality `math.step` gate avoids mid/tail SDF work: estimated 1.5-2.0 us per leviathan.
- Scratchpad write adds under 5 us at 16 nodes but removes downstream recomputation risk estimated at 10-20 us if consumers otherwise rebuild node positions.
- Compute-time telemetry is deterministic estimate only; profiler proof remains pending.

## 2026-05-18 - SignalBus Dependency Trap Fix

What was wrong:
- Vault arrays alone did not satisfy the strictest reading of "push SignalBus".
- A direct `SignalBus<T>` bridge by adding `Hecton8.Core` to AI.Cognition failed manual Roslyn compile because `ISignal` had duplicate type identity across `Hecton8.Core` and `Hecton8.Core.Contracts`.

What was done:
- Removed the direct `Hecton8.Core` runtime reference.
- Added optional `NativeQueue<T>.ParallelWriter` fields to `ApexBrainJob`.
- Added `ApexBrainVault.AttachSignalWriters(...)` so the Core/SignalBus owner can attach the three signal writers without AI.Cognition importing Core.
- Runtime compile check passed again. Editor compile check passed again with USG0001 info only.

Cinematic Cheats used:
- No new simulation. Signals still carry mathematical scalar intent: proximity rumble, base impact, panic radius.

Exact Microseconds saved:
- Avoided Core reference compile-wall expansion: developer iteration impact, not frame time.
- Optional queue enqueue cost estimated under 5 us for the three active signal lanes when enabled.

## 2026-05-18 - Fault Noise Reduction

What was wrong:
- Inactive mock targets were classified as fault rows. That would create black-box dump noise before player/mock target hydration.

What was done:
- Inactive target now means Dormant and zero authority output only.
- `ApexBrainFlags.Fault` and `FaultCode` now indicate non-finite input or non-finite SDF/LOS output.
- Runtime and editor compile checks passed again.

Cinematic Cheats used:
- No new cheat. This is forensic hygiene.

Exact Microseconds saved:
- Avoids cold false-positive dump IO. Frame cost change is effectively 0 us.

## 2026-05-18 - Signal Schedule Surface and Architecture Ledger

What was wrong:
- Optional SignalBus queue writers existed, but the public vault facade still required a manual create/attach/schedule sequence. That is an integration trap.
- The architecture boundary was only present in chat/status/rationale, not in `/Docs/ARCHITECTURE`.

What was done:
- Re-read `CURRENT_BATCH.md`, `Rationale_SHINOBU_61.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Replaced the `new ApexBrainJob` struct initializer in `TryCreateJob` with direct field assignment after `default`.
- Added `ApexBrainVault.TryScheduleWithSignalWriters(...)` so the owning Core/SignalBus bridge can pass `NativeQueue<T>.ParallelWriter` lanes without AI.Cognition referencing `Hecton8.Core`.
- Added `Docs/ARCHITECTURE/SHINOBU_61_APEX_COGNITION.md`.
- Added `Docs/AgentLogs/SELF_AUDIT_SHINOBU_61.xml`.
- Runtime Roslyn/Bee compile passed after these changes.
- Editor Roslyn compile passed after these changes; analyzer emitted USG0001 info only.
- Duplicate GUID scan returned one match for each new SHINOBU_61 meta GUID.
- Forbidden API scan remained clean for NavMesh, physics casts, properties, Pack=1, binary hardware switches, UnityEngine.Random, Time.deltaTime, JobHandle.Complete, LINQ, and foreach.

Cinematic Cheats used:
- No new physical simulation. The sweet-lie LOS remains dot product + SDF shadow + spatial-hash canyon bias.

Exact Microseconds saved:
- Direct field assignment is primarily hygiene; frame saving is 0 us measurable.
- The schedule-writer bridge prevents managed post-job relay. Expected saved cost versus managed relay is 10-25 us/frame when three signal lanes are active, pending profiler proof.

## 2026-05-18 - Optional NativeQueue Safety Hardening

What was wrong:
- `ApexBrainJob` had optional `NativeQueue<T>.ParallelWriter` fields for SignalBus bridging. The no-writer schedule path still carries those fields as default structs, which can trip Unity Jobs container safety validation even when the job never enqueues.

What was done:
- Added `NativeDisableContainerSafetyRestriction` to the three optional writer fields.
- Kept queue writes gated behind `EnableSignalQueueWrites`.
- Runtime Roslyn/Bee compile passed.
- Editor Roslyn compile passed; analyzer emitted USG0001 info only.

Cinematic Cheats used:
- No new simulation. This is job-container plumbing for the existing signal fake.

Exact Microseconds saved:
- No-writer path frame delta is 0 us; the change prevents a schedule-time safety failure.
- Avoiding a duplicate signal-emitting job prevents re-running the apex kernel, estimated 20-45 us per active 10-row batch.

## 2026-05-18 - Continuous Scheduler Frequency Gate

What was wrong:
- `GlobalQualityWeight` controlled ambush node density and SDF sample gates, but the scheduler facade still evaluated the apex kernel every frame unless the caller added its own policy.

What was done:
- Added `ApexBrainVault.ShouldEvaluateFrame(...)`.
- Wired the gate into `TrySchedule(...)` and `TryScheduleWithSignalWriters(...)`.
- The update cadence is computed with `math.lerp(5f, 60f, Smooth01(...))`: about 5 Hz at quality 0.1 and 60 Hz at quality 1.0.
- Runtime Roslyn/Bee compile passed after the cold-boot/octant edits, queue hardening, and scheduler gating.
- Editor Roslyn compile passed; analyzer emitted USG0001 info only.

Cinematic Cheats used:
- Temporal authority LOD: low quality reuses the last valid apex output between evaluations instead of recomputing invisible nuance every frame.

Exact Microseconds saved:
- At quality 0.1 the scheduler skips roughly 11 of 12 apex evaluations. For the 10-row cap, this avoids the full Burst job cost on skipped frames.
- At quality 1.0 the gate resolves to stride 1, so desktop overkill remains per-frame.

## 2026-05-18 - Rollback Determinism and Scratch Hygiene

What was wrong:
- The apex authority jobs still used `FloatMode.Fast`, which is wrong for rollback-relevant simulation state.
- Dormant/faulted rows could leave stale `AmbushNodeScratch` and `ApexInfluenceNode` rows, letting gizmos or future animation bridges read old predator intent.

What was done:
- Switched `MockPlayerAupAdvanceJob` and `ApexBrainJob` to `FloatMode.Deterministic`.
- Added `ClearAmbushRows(...)` and call sites for Dormant and faulted outputs.
- Faulted outputs now zero non-authority utility vectors/scalars, node counts, and visual scalars while retaining fault flags for black-box telemetry.
- Runtime Roslyn/Bee compile passed.
- Editor Roslyn compile passed; analyzer emitted USG0001 info only.

Cinematic Cheats used:
- No new simulation. This preserves the existing sweet-lie LOS and prevents stale fake ambush data from surviving after authority says Dormant/Fault.

Exact Microseconds saved:
- Scratch clearing costs under 2-4 us on Dormant/fault rows, but prevents downstream recomputation/debug pollution.
- Deterministic Burst trades a small amount of FP throughput for rollback stability; no profiler number claimed.

## 2026-05-18 - Duplicate-ID Audit Trail Closure

What was wrong:
- The active `LOG_SHINOBU_61.md` belonged to the later voxel Surface Nets duplicate prompt at the time of Loop 13, but the user resumed the earlier Apex Leviathan prompt.
- Without a pointer, reviewers could inspect the active log and falsely conclude Apex work stopped before the latest hardening.

What was done:
- Added a short pointer section to `Docs/AgentLogs/LOG_SHINOBU_61.md`.
- Left the voxel status/rationale/log content intact in Loop 13 to avoid cross-domain evidence contamination.
- Superseded by Loop 15: active SHINOBU_61 logs are now Apex again because the latest user prompt explicitly re-bound the agent to `PREDICTIVE_APEX_AGGRESSION_DIRECTOR`; Voxel evidence remains archived.
- Re-ran Apex static forbidden scans after the pointer patch. No matches were found for NavMesh, Physics casts, `Update()`, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, LINQ, `foreach`, hot DTO properties, `Pack=1`, binary hardware switches, `math.sincos`, or `FloatMode.Fast`.
- Re-ran `git diff --check` over touched Apex and audit files. No whitespace errors were reported; Git warned only that `Hecton8.AI.Cognition.asmdef` may normalize LF to CRLF.

Cinematic Cheats used:
- No runtime change. Apex still uses sweet-lie LOS: dot product, analytic SDF shadow, and spatial-hash canyon bias instead of body raycasts or NavMesh.

Exact Microseconds saved:
- Runtime delta from this closure is 0 us.
- The maintained Apex runtime estimates remain: roughly 140-260 us saved per active leviathan versus naive raycast/path/body-fit logic, plus cadence savings at low `GlobalQualityWeight`.
## Duplicate-ID Apex Continuation Pointer

What was wrong: The user resumed the earlier Apex Leviathan assignment while the active `SHINOBU_61` status/log files belonged to the later voxel Surface Nets duplicate prompt at that historical checkpoint.

What was done: Preserved this pointer in the Apex archive trail at the time. Loop 15 restored the active files to Apex and kept Voxel evidence in the separate `_VOXEL_SURFACE_NETS_ARCHIVE_20260518` files.

Cinematic Cheats used: Apex uses sweet-lie LOS: dot product, SDF shadow, and spatial-hash canyon bias instead of NavMesh or physics ray fans.

Exact Microseconds saved: Runtime impact is 0 us. The value is audit recovery and preventing wrong-domain integration work.

## 2026-05-18 - Acoustic Bank Continuum Hardening

What was wrong:
- `GlobalQualityWeight` already collapsed scheduler cadence, ambush nodes, and mid/tail SDF work, but acoustic memory still scanned up to 32 taps on every evaluated frame.
- That left a hidden high-tier sensory cost on low-quality hardware.

What was done:
- Added `ResolveAcousticTapLimit(...)` to `ApexBrainJob`.
- The acoustic scan window now uses `math.lerp(4f, 32f, qualityCurve)` and clamps to the actual `AcousticTaps` buffer length.
- `ResolveAcousticMemory(...)` receives the resolved tap limit.
- `InterceptComputeTimeMs` telemetry now uses the evaluated acoustic tap count, not the full bank capacity.
- Static forbidden scans stayed clean. Targeted Roslyn recheck is blocked by the project CPU guard after source edits; a 24-sample wait exited `ROSLYN_RECHECK_SKIPPED_CPU_GUARD` without launching `dotnet`. Prior Loop 12 compile proof is superseded.

Cinematic Cheats used:
- Sensory LOD: low quality lets the leviathan "hear" a small representative tap window while animation/audio can still sell presence. High quality restores full acoustic overkill.

Exact Microseconds saved:
- Estimated 4-8 us per active 10-row batch on low-quality evaluated frames by avoiding up to 28 tap iterations. Profiler proof remains pending.

## 2026-05-18 - Sweet Lie LOS Polish and AI Cognition Pack Purge

What was wrong:
- Sweet-lie LOS needed one more bounded mathematical hint for rock-between-prey cases without falling back to physics raycasts.
- `GlobalQualityWeight` could lower evaluated ambush node count while old high-quality scratch rows remained readable.
- The same AI Cognition runtime assembly still contained legacy `Pack=1` structs and a stalk job with non-mandated Burst flags.

What was done:
- Added a quality-gated midpoint analytic SDF line sample to the LOS lie. It blends only when quality rises through the continuum; low quality keeps the cheaper dot/SDF/hash fake.
- Cleared unevaluated `AmbushNodeScratch` and `ApexInfluenceNode` entries when node density drops.
- Added CSV hot-reload metadata to `ApexBrainTuning` inside existing 128B explicit padding.
- Removed remaining `Pack=1` in AI Cognition legacy files, converted hot legacy DTOs to explicit layouts, replaced vault wrapper `IsCreated` properties with methods, and changed `LeviathanStalkJob` to deterministic Burst with `[NoAlias]` NativeArray fields.
- Restored active SHINOBU_61 documentation to the current Apex prompt; Voxel evidence remains archived separately.

Cinematic Cheats used:
- LOS remains a sweet lie: dot product + distance + analytic SDF shadow + spatial hash canyon bias. High quality adds one midpoint SDF probe; still no raycast, no NavMesh, no collider sweep.

Exact Microseconds saved:
- Avoided returning to physics LOS: keeps the existing 60-120 us per active leviathan estimate versus ray/line checks.
- Low-quality midpoint bypass avoids roughly 0.3-0.5 us per active row compared with always sampling the line probe.
- Stale-node purge prevents downstream readers from paying recomputation/debug costs against invalid 16-node intent.

Verification:
- Static forbidden scan over `Assets/_Project/Scripts/AI/Cognition` returned no matches for `Pack=1`, `Sequential`, NavMesh, physics raycasts, managed StateMachine, hot DTO `{ get; }` properties, `UnityEngine.Random`, `Time.deltaTime`, `foreach`, LINQ, `JobHandle.Complete`, or sibling runtime domain references.
- Burst scan now shows `LeviathanStalkJob`, `MockPlayerAupAdvanceJob`, and `ApexBrainJob` all using deterministic mandated flags.
- `git diff --check` passed for AI Cognition and SHINOBU docs. Git only warned about existing LF-to-CRLF normalization.
- No `dotnet build` was launched; compiler proof remains blocked by the project CPU/process guard.

## 2026-05-19 - NaN Quarantine and False-Sharing Padding

What was wrong:
- Faulted non-finite input was detected but the job still executed AUP delta math, SDF sampling, LOS math, and `HashSpatial` before final output zeroing.
- Optional `NativeQueue<T>.ParallelWriter` fields had `NativeDisableContainerSafetyRestriction` without the mandated three-paragraph justification at the declaration site.
- Several parallel-written DTO rows were 8/16-byte aligned but not 64-byte stride multiples, leaving adjacent job indices able to share cache lines.

What was done:
- Added `WriteFaultRow(...)` and early return for non-finite state/target AUP or velocity before any spatial hash or SDF math.
- Added full safety justifications above the proximity, combat, and panic queue writer fields.
- Padded `MockPlayerAUP` to 128B, `ApexBrainOutputDTO` to 192B, legacy `AlphaLeviathanCognitionState` to 192B, and legacy `AlphaLeviathanSteeringOutput` to 128B.
- Updated `ApexBrainVault.ValidateLayouts()` to reject stale 96B/160B sizes.
- Updated architecture/status/rationale/self-audit evidence for Loop 16.

Cinematic Cheats used:
- No new physical truth. The apex still uses dot-product/SDF/spatial-hash Sweet Lie LOS; the new fault path simply refuses to run the fake on poisoned inputs.

Exact Microseconds saved:
- Corrupted rows now skip the full SDF/acoustic/node path instead of spending it before quarantine.
- False-sharing padding avoids worker cache-line ping-pong; no profiler number is claimed until Unity/Profiler proof is available.

Verification:
- Static forbidden scan over `Assets/_Project/Scripts/AI/Cognition` returned no matches for `Pack=1`, `Sequential`, NavMesh, physics raycasts, `Update()`, `UnityEngine.Random`, `Time.deltaTime`, LINQ/`foreach`, hot DTO properties, `JobHandle.Complete`, `FloatMode.Fast`, `math.sincos`, or sibling runtime references.
- `git diff --check` passed for AI Cognition and SHINOBU docs. Git only warned about existing LF-to-CRLF normalization.
- Compiler proof remains blocked by hardware guard: CPU sampled at 100% with compiler processes active. No `dotnet build` was launched.

## 2026-05-19 - Computed Fault Early-Out

What was wrong:
- Loop 16 quarantined non-finite input before `DowncastAupDelta`, SDF, LOS, and `HashSpatial`.
- A separate computed-fault path still existed after SDF/LOS evaluation. If sampler math produced a non-finite distance or LOS scalar, the row set fault flags but continued through aggression, ambush nodes, signal construction, telemetry construction, and spatial hashing before the late zeroing path.

What was done:
- Changed `computedFinite == false` to call `WriteFaultRow(..., 0x53484E4Eu)` and return immediately.
- Removed the now-dead active-path `faulted` selects and `faultCode` carrier.
- Re-ran static forbidden scans; no forbidden AI Cognition matches were found.
- Rechecked CPU/compiler guard. CPU remained 100%, so no targeted Roslyn compile or `dotnet build` was launched.

Cinematic Cheats used:
- No new physical truth. Sweet-lie LOS remains dot product + distance + analytic SDF/hash shadow; this pass only prevents poisoned fake-LOS math from leaking into downstream intent.

Exact Microseconds saved:
- Clean rows save a small number of dead `math.select` operations.
- Faulted computed rows skip biome, aggro, ambush-node evaluation, signals, telemetry construction, and spatial hash. No profiler number is claimed until Unity/Profiler proof is available.

## 2026-05-19 - Loop 18 Tuning and Sampler NaN Vaccination

What was wrong:
- Active SHINOBU_61 files were briefly contaminated by the duplicate Voxel prompt. That state was preserved separately as conflict evidence, then active files were restored to Apex.
- Cold tuning and sampler rows still had several recoverable NaN ingress points before SDF/LOS authority math.

What was done:
- Sanitized head/mid/tail tuning offsets before sampler fallback use.
- Sanitized emergency `float4` curve rows against emergency mock stats.
- Sanitized sampler origin, floor/ceiling span, and canyon bias.
- Sanitized target noise and target acoustic fallback scalar.
- Expanded computed finite checks to include pursuit vectors and intermediate LOS scalars.

Cinematic Cheats used:
- Same sweet-lie LOS and analytic SDF slither fake. This pass keeps the fake finite and bounded instead of escalating recoverable bad tuning into a fault row.

Exact Microseconds saved:
- Clean row cost changes by only scalar clamps.
- Saves fault-row churn and dump risk when cold tuning/CSV/sampler data is corrupt. No measured profiler number claimed.

## 2026-05-19 - Loop 19 Mock Target Generator Quarantine

What was wrong:
- The blind mock target job could still emit poisoned rows if both the frame delta and target fallback delta were invalid.
- A non-finite mock AUP stayed non-finite across frames until the main apex job faulted it later.

What was done:
- Reset non-finite mock target AUP before advancing it.
- Added deterministic `1/30f` delta fallback when both deltas are invalid.
- Clamped mock velocity to 120 m/s and required finite normalized forward output.

Cinematic Cheats used:
- No new physical simulation. The mock remains a deterministic blind target, just bounded enough to keep the predictive AI proof clean.

Exact Microseconds saved:
- Prevents bad mock data from forcing later apex fault rows and black-box churn. Normal mock rows pay only a few scalar/vector guards; no profiler number claimed.

## 2026-05-19 - Loop 20 Bounded Tuning Before Ambush Hashing

What was wrong:
- Positive-only sanitation still allowed huge finite CSV/tuning values.
- Huge ambush radii or SDF dimensions could overflow a candidate and reach `HashSpatial(candidate)` inside node scoring.

What was done:
- Added `SanitizeRange(...)` inside the Burst job.
- Bounded authority tuning fields before SDF/LOS/node math.
- Bounded sampler dimensions and SDF offsets before SDF use.

Cinematic Cheats used:
- Same sweet-lie LOS and octant-lattice ambush fake. This pass keeps the fake inside a finite design envelope.

Exact Microseconds saved:
- Prevents catastrophic overflow/fault churn from bad tuning. Normal rows pay fixed scalar clamp cost; no profiler number claimed.

## 2026-05-19 - Loop 21 Cold Vault Tuning Envelope

What was wrong:
- The hot Burst job bounded authority tuning, but the cold vault facade still stored any positive finite CSV/editor value.
- That made unmanaged vault memory less trustworthy than the job-local view and left future readers able to see absurd radii/speeds before the job sanitized them.

What was done:
- Replaced `ApexBrainVault.SanitizeTuning()` positivity-only guards with finite min/max envelopes matching `ApexBrainJob.ResolveTuning()`.
- Removed the dead vault `SanitizePositive(...)` helper.
- Re-ran static forbidden scans and `git diff --check` for the touched source.

Cinematic Cheats used:
- Same sweet-lie LOS and octant-lattice ambush fake. This pass keeps human-authored tuning bounded before it can exaggerate the fake into invalid math.

Exact Microseconds saved:
- 0 us claimed in the hot path; this is cold ingress hardening. It prevents later invalid-vector and dump churn from bad CSV/editor values.

Verification:
- Static forbidden scan over AI Cognition stayed clean for NavMesh/Physics casts, `Pack=1`, `Sequential`, `FloatMode.Fast`, `math.sincos`, hot DTO properties, runtime native allocations, LINQ/`foreach`, `Time.deltaTime`, `UnityEngine.Random`, and `JobHandle.Complete`.
- `git diff --check` passed for `ShinobuApexBrainVault.cs`; only LF-to-CRLF normalization warning was emitted.
- Targeted runtime Roslyn/Bee recheck passed at CPU 48.21%: `Temp/SHINOBU_61_CognitionCheck.dll`, timestamp 2026-05-19 02:04:08.
- Targeted editor Roslyn recheck passed at CPU 30.05%: `Temp/SHINOBU_61_EditorCheck.dll`, timestamp 2026-05-19 02:04:26. Analyzer emitted USG0001 info only.
- No `dotnet build` was launched.

## 2026-05-19 - Loop 22 Full CSV Tuning Surface

What was wrong:
- The zero-GC CSV bridge existed, but it only covered part of `ApexBrainTuning`.
- Designers could not tune damage, deterministic tick delta, head/mid/tail offsets, stamina recovery/cost, sweet-lie LOS weights, ambush radius, visual-overkill gain, or bite offset through `apex_predator_stats.csv`.
- That partial surface invites hardcoded C# edits for balance values.

What was done:
- Added stable ASCII key hashes for the missing gameplay-relevant float tuning fields.
- Extended `ApplyCsvValue(...)` to route those keys into the unmanaged tuning row.
- Kept simulation time, source hash, flags, and CSV metadata outside CSV mutation.
- Left `SanitizeTuning(...)` as the single cold vault envelope, so CSV values are bounded before downstream consumers read them.

Cinematic Cheats used:
- No physical truth was added. The sweet-lie LOS remains dot product + analytic SDF/hash shadow; this pass only returns more control of the fake to human tuning without recompilation.

Exact Microseconds saved:
- 0 us claimed in the frame hot path. CSV parsing is cold and scratch-backed.
- Expected saved cost is iteration and compile-wall avoidance: no C# recompile just to alter damage, offsets, stamina, sweet-lie shadow gain, ambush radius, visual-overkill gain, or bite offset.

Verification:
- Static forbidden scan over AI Cognition stayed clean for NavMesh/Physics casts, `Pack=1`, `Sequential`, `FloatMode.Fast`, `math.sincos`, hot DTO properties, runtime native allocations, LINQ/`foreach`, `Time.deltaTime`, `UnityEngine.Random`, and `JobHandle.Complete`.
- `git diff --check` passed for `ShinobuApexBrainVault.cs`; only LF-to-CRLF normalization warning was emitted.
- Initial guarded targeted runtime Roslyn recheck exited `ROSLYN_RECHECK_SKIPPED_CPU_GUARD CPU=85.3 COMPILERS=0`; no compiler was launched on that attempt.
- Later guarded targeted runtime Roslyn/Bee recheck passed at CPU 15.93%: `Temp/SHINOBU_61_CognitionCheck.dll`, timestamp 2026-05-19 02:14:01.
- Targeted editor Roslyn recheck passed at CPU 48.47%: `Temp/SHINOBU_61_EditorCheck.dll`, timestamp 2026-05-19 02:14:20. Analyzer emitted USG0001 info only.
- No `dotnet build` was launched.

## 2026-05-19 - Loop 23 Low-Quality Stale Node Write Collapse

What was wrong:
- The midpoint sweet-lie SDF line probe was correctly gated, but the ambush node resolver still ran a 16-lane loop on every active frame.
- At low quality the job evaluated 2 nodes, then repeatedly cleared the other 14 ambush scratch/influence rows even when they were already clean.
- The telemetry estimate only charged evaluated nodes and missed the one-frame stale-clear work during quality drops.

What was done:
- Added `ResolvePreviousEvaluatedNodeCount(...)` using the previous output row.
- Split `ResolveAmbushNodes(...)` into an evaluated-node loop and a stale-clear loop.
- Cleared stale rows only when previous evaluated count exceeds current evaluated count.
- Kept full 16-lane clearing for Dormant and fault rows.
- Added stale-clear micro-cost to deterministic telemetry estimate only when stale lanes are actually cleared.

Cinematic Cheats used:
- Same sweet-lie LOS: dot product + analytic SDF/hash shadow, no physics rays.
- Same octant-lattice ambush fake; this pass removes repeated low-quality housekeeping writes, not gameplay truth.

Exact Microseconds saved:
- Estimated steady low-quality saving is up to 14 scratch/influence clear branches and writes per active leviathan per evaluated frame after the first quality drop.
- No profiler number claimed until Unity profiler proof exists.

Verification:
- Static forbidden scan over AI Cognition stayed clean for NavMesh/Physics casts, `Pack=1`, `Sequential`, `FloatMode.Fast`, `math.sincos`, hot DTO properties, runtime native allocations, LINQ/`foreach`, `Time.deltaTime`, `UnityEngine.Random`, and `JobHandle.Complete`.
- `git diff --check` passed for `ShinobuApexBrainJobs.cs`; only LF-to-CRLF normalization warning was emitted.
- Initial Roslyn recheck was blocked by CPU/compiler guard at CPU 100% with active `csc` and `dotnet` compiler processes.
- Later guard-clean runtime Roslyn/Bee recheck passed at CPU 34.53%: `Temp/SHINOBU_61_CognitionCheck.dll`, timestamp 2026-05-19 02:22:24.
- Targeted editor Roslyn recheck passed at CPU 36.71%: `Temp/SHINOBU_61_EditorCheck.dll`, timestamp 2026-05-19 02:23:07. Analyzer emitted USG0001 info only.
- No `dotnet build` was launched.
