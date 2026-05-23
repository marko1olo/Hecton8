# LOG_SHINOBU_311

## 2026-05-22 - SENSORY_ACOUSTIC_ECHO_INTEGRATOR
What was wrong:
- Predator acoustic hearing had no Burst-backed inverse-square/SDF route into `PredatorCognitionDomain`.
- Existing acoustic runtime was echo trail oriented; no direct predator cognition injection from `MovementAcousticSignal`, `AcousticPingSignal`, and `CombatDamageSignal`.
- Static scan needed proof that AI/Fauna hearing was not using `Physics.CheckSphere`, `Physics.Linecast`, or `Collider.ClosestPoint`.

What was done:
- Added `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs`.
- Added `AcousticStimulusDTO` explicit 32-byte layout: `double3 EpicenterAUP@0`, `float InitialIntensity@24`, `uint SoundTypeHash@28`.
- Added GlobalDataVault acoustic buffers `72760..72768`.
- Staged existing SignalBus acoustic lanes into `AcousticStimulusDTO[128]`.
- Scheduled `GenerateMockAcousticSignalsJob`, `CalculateAcousticAttenuationJob`, `EvaluateAcousticOcclusionJob`, and `RecordAcousticTelemetryJob` before `PredatorCognitionJob`.
- Implemented double-first AUP subtraction, inverse-square attenuation, SDF occlusion, direct `UnsafeUtility.AsRef` cognition mutation, acoustic memory writes, and 300-frame black box telemetry.
- Added `Assets/_Project/Scripts/Editor/AcousticSensoryXRayWindow_SHINOBU311.cs` with UI Toolkit tuning, SceneView debug discs, `OOP_Hearing_Scanner`, and `AcousticStimulusDTO` layout guard.
- Added `Docs/ARCHITECTURE/SHINOBU_311_ACOUSTIC_SENSORY_ROUTE.md`.
- Added `Docs/Reports/SHINOBU_311_AI_OPTIMIZATION_REPORT.json` and updated shared `Docs/Reports/AI_OPTIMIZATION_REPORT.json`.

Cinematic Cheats used:
- SDF occlusion probes replace physics linecasts.
- Continuous quality controls ray probe count from 1 to 8.
- Strongest-source acoustic memory replaces per-source persistent objects.
- Deterministic mock acoustic sources replace scene debug emitters.

Exact Microseconds saved:
- Trigger/listener route avoided: estimated 80-250 us/frame in acoustic-heavy predator scenes.
- Direct cognition mutation versus managed setter/event fan-out: estimated 15-40 us/frame.
- No blanket vault MemClear for fully overwritten acoustic buffers: estimated 8-30 us at allocation/reinitialization.
- Static scanner found 0 OOP acoustic query violations in 68 AI/Fauna files.

Verification:
- JSON reports validated with `ConvertFrom-Json`.
- `git diff --check` passed for touched files except existing CRLF normalization warning on `PredatorCognitionDomain.cs`.
- Compile not launched: CPU sampled 79% then 100%; active `dotnet.exe` PIDs 1548 and 13972 were present. Project rule forbids starting dotnet/csc under those conditions.

## 2026-05-22 - Polish Audit Closure
What was wrong:
- `71980..71988` conflicted with `ShinobuParasite*` BufferIDs.
- Diagnostic read accessors called cold Vault acquisition/CSV bootstrap.
- Mock acoustic generator was not the XML-required `IJobParallelFor`.
- Burst jobs lacked explicit `[NoAlias]` proof on separate Vault lanes.

What was done:
- Moved acoustic Vault lanes to `72760..72768` and recorded the boundary in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Made acoustic `TryRead*` diagnostics pure: no Vault allocation, no CSV load, no cold mutation.
- Converted `GenerateMockAcousticSignalsJob` to opt-in `IJobParallelFor` with deterministic fixed-slot writes.
- Added `[NoAlias]` to acoustic job NativeArray and pointer lanes.
- Added core/control length guards before `UnsafeUtility.AsRef` cognition mutation.
- Changed `OOP_Hearing_Scanner` to upsert shared JSON instead of overwriting unrelated agent reports.

Cinematic Cheats used:
- Production path still uses SignalBus/SDF math only; mock emitters are an editor/stress-test fake, not scene objects.

Exact Microseconds saved:
- Collision fix is correctness, not a speed claim.
- Pure reads avoid hidden cold allocation/CSV stalls from editor polling.
- `[NoAlias]` allows Burst to keep vectorization assumptions on attenuation/occlusion lanes; exact profiler timing remains pending under build guard.

Verification:
- Forbidden runtime OOP acoustic query scan returned 0 hits.
- Hot DTO/property scan returned 0 hits for `get; set;`, `Pack=1`, `new NativeArray`, LINQ, and `foreach` in `PredatorCognitionDomain.AcousticSdf.cs`.
- JSON reports validated with `ConvertFrom-Json`.
- `git diff --check` passed for touched paths, with CRLF normalization warnings only on pre-existing line-ending state.
- Build not launched: guard sampled CPU 100% with active `dotnet.exe` PIDs 3056 and 14220.

<SELF_AUDIT agent="SHINOBU_311">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">Grep archaeology run over AI/Fauna/Sensory acoustic terms.</TASK>
    <TASK id="02" result="PASS">Integrated as `PredatorCognitionDomain` partial, no competing runtime.</TASK>
    <TASK id="03" result="PASS">Used existing `MovementAcousticSignal`, `AcousticPingSignal`, `CombatDamageSignal` lanes.</TASK>
    <TASK id="04" result="PASS">No predator hearing `SphereCollider` found; unrelated POI/CCD colliders preserved.</TASK>
    <TASK id="05" result="PASS">No managed sound listener route found in target domain; SignalBus route owns input.</TASK>
    <TASK id="06" result="PASS">`GenerateMockAcousticSignalsJob` is opt-in Burst `IJobParallelFor` with deterministic fixed-slot writes.</TASK>
    <TASK id="07" result="PASS">`CalculateAcousticAttenuationJob` uses double AUP delta then inverse-square local float math with `[NoAlias]`.</TASK>
    <TASK id="08" result="PASS">`EvaluateAcousticOcclusionJob` samples SDF bytes, no `Physics.Linecast`.</TASK>
    <TASK id="09" result="PASS">Ray probes resolve continuously from 1..8 through `GlobalQualityWeight` and `RayStepScale`.</TASK>
    <TASK id="10" result="PASS">Cognition drives/memory mutate by bounded `UnsafeUtility.AsRef` before `PredatorCognitionJob`.</TASK>
    <TASK id="11" result="PASS">Result DTO writes normalized direction, runtime source position, source AUP, sound hash, listener hash.</TASK>
    <TASK id="12" result="PASS">Acoustic gameplay-affecting jobs use `FloatMode.Deterministic` for rollback truth.</TASK>
    <TASK id="13" result="PASS">Vault temp lanes use uninitialized memory where overwritten; 64-byte counter is owner-written and mock jobs write fixed slots to avoid shared counter contention.</TASK>
    <TASK id="14" result="PASS">300-frame `SensoryTelemetryEntry` ring and `Dump_SHINOBU_311.bin` fault route exist.</TASK>
    <TASK id="15" result="PASS">UI Toolkit X-Ray reads telemetry, tunes Vault DTO, and exposes mock stress flag.</TASK>
    <TASK id="16" result="PASS">Cold CSV hearing profile parser uses native scratch and `ReadOnlySpan<byte>`.</TASK>
    <TASK id="17" result="PASS">SceneView acoustic debug discs read staged `AcousticStimulusDTO`.</TASK>
    <TASK id="18" result="PASS">`OOP_Hearing_Scanner` emits stable and shared JSON proof; Loop 8 upgraded the scanner to Roslyn AST targeted parsing without runtime dependency expansion.</TASK>
    <TASK id="19" result="PASS">Editor layout guard asserts `AcousticStimulusDTO` size 32 and align 8.</TASK>
    <TASK id="20" result="PASS">Self-audit, route doc, report JSON, forbidden-query scan, and compile guard recorded.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <AcousticStimulusDTO size="32" align="8">`double3 EpicenterAUP` offset 0 size 24; `float InitialIntensity` offset 24 size 4; `uint SoundTypeHash` offset 28 size 4; padding 0; 32 bytes = 4 * 8 and 2 * 16.</AcousticStimulusDTO>
    <Counter size="64">`AcousticCounter64DTO` is one cache line: `Value@0`, `Capacity@4`, `Flags@8`, reserved padding through `@56`; this prevents false sharing if the count lane is touched near parallel work.</Counter>
    <Telemetry size="64">`SensoryTelemetryEntry` is one cache line for black-box rows.</Telemetry>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Quality 0 collapses SDF occlusion to 1 probe; middle quality reaches 3..5 probes through smooth polynomial lerp; quality 1 uses 8 probes. Quality changes ALU cost/diagnostic richness only, never DTO layout, BufferID, save identity, or cognition authority.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No private `NativeArray` ownership was added. Vault lanes: 72760 stimuli, 72761 count, 72762 results, 72763 telemetry ring, 72764 telemetry cursor, 72765 profiles, 72766 profile count, 72767 tuning, 72768 CSV scratch. `71980..71987` plus `71989,71990` rejected as parasite VFX ownership.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>Consumes `_scheduledSwarmHandle`; outputs `mock -> attenuation -> occlusion -> telemetry -> PredatorCognitionJob` dependency chain. No arbitrary `.Complete()` in schedule. `[NoAlias]` applied to separate job NativeArray/pointer lanes. Core/control raw pointers have length guards before mutation.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>Runtime changes are in Fauna partial plus Core contracts already referenced by that domain; no sibling runtime asmdef reference added. Build skipped under CPU 100% and active dotnet PIDs 3056,14220.</COMPILE_GUARD>
  <DEAR_LIE>Rejected trigger hearing and per-predator linecasts. SDF byte taps approximate acoustic rock occlusion. Before: PhysX broadphase/linecast pressure plus managed fan-out. After: O(P * S * Q) bounded contiguous Burst math with P active predators, S capped stimuli 128, Q continuous 1..8 probes.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-22 - Polish Re-Audit Delta
What was wrong:
- The mock job still used `System.Threading.Interlocked` on a raw counter pointer, which is unnecessary for deterministic stress data and risks Burst/import fragility.
- The editor X-Ray `Tick()` allocated dynamic strings through label concatenation and numeric `.ToString()`.
- The scanner report language overstated AST proof while the implementation was a legacy lexical scan.
- Acoustic SDF consumed the inherited predator threat snapshot after direct World singleton fallback existed in that shared path.

What was done:
- Added `AcousticCounter64DTO` as a 64-byte explicit-layout counter row and validated it in the ABI guard.
- Changed `GenerateMockAcousticSignalsJob` to schedule exactly `mockWriteCount` indices and write `Stimuli[mockIndex]`; the owner writes the counter once before scheduling.
- Replaced X-Ray status/timing labels with disabled `IntegerField`/`FloatField` controls updated through `SetValueWithoutNotify`.
- Changed SHINOBU_311 report wording and shared report block; later loops superseded this with token-scan verdict plus separate compile proof.
- Replaced the direct concrete World scheduler dependency with a local Core-contract Vault reader over `VoxelSdfPayloadDescriptorDTO`.
- Made acoustic occlusion consume only that Vault SDF route; direct singleton bridges remain fallback for pre-existing non-acoustic predator threat behavior.

Cinematic Cheats used:
- The Dear Lie remains SDF byte taps instead of PhysX linecasts. Mock acoustic waves are deterministic fixed-slot synthetic stimuli, not scene emitters.

Exact Microseconds saved:
- Fixed-slot mock writes remove shared counter cache bouncing in stress mode; expected win is small but removes a Burst atomic import hazard.
- X-Ray polling no longer allocates dynamic status strings each editor update.
- Vault SDF preference is an authority hardening change, not a new timing claim.

Verification:
- Static scan shows no atomic interlocked route, raw mock counter pointer, or stale int-count field route in the acoustic partial.
- Static scan shows no `.text =` writes in X-Ray `Tick()`; remaining `.ToString()` calls are cold scanner report builders.
- Reports now state Roslyn AST targeted proof after Loop 8 scanner upgrade.
- `git diff --check` passed with repository LF/CRLF warnings only on `PredatorCognitionDomain.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Build not launched: CPU 100% with active `VBCSCompiler.exe` PID 6484.

## 2026-05-22 - Core-Contract SDF Reader Delta
What was wrong:
- The first Vault SDF preference used a concrete World-domain scheduler class as a convenience bridge.

What was done:
- Added `TryReadAcousticPublishedVoxelSdfSnapshot` inside the `PredatorCognitionDomain` partial. It reads `VoxelSdfPayloadDescriptorDTO` and `VoxelSdfTexture3D` through `IDataVault` and `BufferID` only.
- `EvaluateAcousticOcclusionJob` now receives this Core-contract Vault SDF snapshot directly. If no Vault SDF exists, the SDF lane is empty and occlusion fails open instead of borrowing singleton bridge data.
- Patched all cleanup/reset sites from the removed int-count field to `_acousticSdfStimulusCounter`.
- Hardened SDF snapshot validation with exact BufferID/SystemID/generation checks, expected byte count, owner system, valid flag, finite origin, and positive cell size.
- Removed the dead local mock acoustic/light probe route after `rg` showed no callers; SHINOBU_311 Vault mock stimuli are now the only predator acoustic mock path in this domain.
- Checked `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; file is absent, so no DataMonolith bake/boot proof is claimed for SHINOBU_311 hearing profiles.

Cinematic Cheats used:
- Same Dear Lie: byte SDF taps replace linecasts.

Exact Microseconds saved:
- No timing claim; this is compile-wall and authority-route hardening.

Verification:
- Static scan confirms no concrete World scheduler route, spawn-SDF header route, or stale int-count field remains in the SHINOBU_311 source. Remaining `Interlocked` hits are pre-existing non-acoustic pack/claim jobs in `PredatorCognitionDomain.cs`.
- Focused source scan is clean for acoustic `Interlocked`, `System.Threading`, `new NativeArray`, LINQ, `foreach`, `Pack=1`, hot DTO properties, concrete World scheduler calls, stale counter names, and raw mock counter pointers.
- Focused source scan is clean for the removed local mock acoustic DTO, scheduler facade, and `NativeQueue` route.
- Data Monolith proof is explicitly scoped as missing in this workspace; cold CSV parser remains the fallback bridge.
- Brace/preprocessor-style lexical balance passed on touched C# files; JSON reports parse; diff check reports only existing LF/CRLF warnings.
- Build not launched: latest guard sampled CPU 99.23% with active `dotnet.exe` PID 12344.

## 2026-05-22 - Loop 8 AST Scanner / Compile Edge Pass
What was wrong:
- `PredatorCognitionDomain.AcousticSdf.cs` still imported `Hecton8.World` after the SDF route moved to Core-contract Vault descriptors.
- Task 18 asked for AST proof, while the prior scanner was lexical and only honestly reported token-scan evidence.

What was done:
- Removed the stale `Hecton8.World` import; SHINOBU_311 acoustic SDF code now references Core contracts/memory for `VoxelSdfPayloadDescriptorDTO`, `BufferID`, and `SystemID`.
- Upgraded `OOP_Hearing_Scanner` to Roslyn `CSharpSyntaxTree` parsing over AI/Fauna/Sensory scope and `InvocationExpressionSyntax` matching for `Physics.CheckSphere`, `Physics.Linecast`, and `Collider.ClosestPoint`.
- Updated the SHINOBU_311 report and shared AI report block; later loops split scanner verdict from compile proof.

Cinematic Cheats used:
- No runtime cheat changed in this loop; the maintained cheat is SDF byte-tap acoustic occlusion instead of PhysX linecasts or trigger spheres.

Exact Microseconds saved:
- AST scanner is editor-only. Compile-edge cleanup has no frame-time claim. The runtime saving remains the earlier 80-250 us/frame estimate for avoiding collider/linecast hearing in acoustic-heavy predator scenes.

Verification:
- Exact runtime forbidden-call scan over AI/Fauna excluding Editor returned zero `Physics.CheckSphere`, `Physics.Linecast`, or `.ClosestPoint()` invocations.
- Targeted source scan found no `Hecton8.World`, `SpawnZoneSdfValidationScheduler`, `TryReadVoxelSdfSnapshot`, or singleton SDF bridge names in `PredatorCognitionDomain.AcousticSdf.cs`.

## 2026-05-22 - Loop 9 Narrow Compile Error Fix
What was wrong:
- Narrow build surfaced one SHINOBU_311-owned CS0246: local helper `ToAbsoluteDouble3(in AbsoluteUniversePosition)` still referenced the World AUP type after the namespace import was removed.
- The same build surfaced 5 external errors in `VRSomaticProvider.Comfort.cs` and `PlayerKinematicsRuntime_HandIK.cs`; those are outside SHINOBU_311 ownership.

What was done:
- Removed the local AUP helper and changed acoustic signal ingestion to call `signal.PositionAup.ToAbsoluteDouble3()` directly at the two SignalBus call sites.
- Left external Gameplay/VR/HandIK errors untouched.

Cinematic Cheats used:
- No runtime route change; the Dear Lie remains SDF byte taps instead of PhysX hearing.

Exact Microseconds saved:
- No runtime timing claim. This is compile correctness and compile-wall hygiene.

Verification:
- Source scan now finds no explicit `AbsoluteUniversePosition` type name or `Hecton8.World` namespace reference in `PredatorCognitionDomain.AcousticSdf.cs`; only `signal.PositionAup.ToAbsoluteDouble3()` calls remain.
- Build rerun is blocked by active `dotnet.exe` processes after the prior compile attempt: 1716, 5652, 13176, 15352, 19416, 21912, 22460.
- After a 45-second wait the same `dotnet.exe` processes remained active, so no second build was launched.

## 2026-05-22 - Loop 10 Raw Blackbox Dump Hardening
What was wrong:
- `TryDumpAcousticSdfBlackBox` used `BinaryWriter` and wrote telemetry fields one by one. That was cold fault-path code, but it did not match the raw fixed-row dump requirement.

What was done:
- Replaced `BinaryWriter` with a 16-byte stackalloc little-endian header and a raw `ReadOnlySpan<byte>` over `NativeArray<SensoryTelemetryEntry>`.
- Dump layout is now: magic `0x53333131`, frame, row count, row stride, then raw 64-byte telemetry rows.

Cinematic Cheats used:
- No runtime simulation changed; acoustic occlusion remains SDF byte taps instead of PhysX linecasts.

Exact Microseconds saved:
- Fault path only. No frame-time claim. Per-field writer calls are removed from the blackbox dump path.

Verification:
- Source scan found no `BinaryWriter` or field-by-field `writer.Write` calls in SHINOBU_311 acoustic partial.
- State-aware brace scan on `PredatorCognitionDomain.AcousticSdf.cs` returned depth 0.
- Idle MSBuild/VBCS servers from the previous narrow compile were shut down with `dotnet build-server shutdown`.
- Narrow Core compile rerun used `--disable-build-servers`, `/nr:false`, `/p:UseSharedCompilation=false`, and `-maxcpucount:1`. SHINOBU_311 owned compile error is gone.
- Remaining Core compile blockers are external to SHINOBU_311: `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, and `PlayerHandIkConfigFlags` in Gameplay/VR/HandIK files.

## 2026-05-22 - Loop 11 Report Sync / Static Recheck
What was wrong:
- The aggregate `Docs/Reports/AI_OPTIMIZATION_REPORT.json` SHINOBU_311 block lacked the narrow compile marker already present in the stable SHINOBU_311 report.

What was done:
- Added the SHINOBU_311-clean narrow compile marker to the existing `shinobu311AcousticHearing` object only; Loop 12 updates the marker to the current external Gameplay/Combat/KCC/VR blocker set.
- Re-read the full SHINOBU_311 XML block from `Docs/Tasks/CURRENT_BATCH.md` and reran cheap static proof checks.

Cinematic Cheats used:
- No runtime route change; acoustic hearing still uses inverse-square attenuation plus SDF byte taps instead of trigger spheres or PhysX linecasts.

Exact Microseconds saved:
- No runtime timing claim. This loop is report consistency and static verification.

Verification:
- `Docs/Reports/SHINOBU_311_AI_OPTIMIZATION_REPORT.json` and `Docs/Reports/AI_OPTIMIZATION_REPORT.json` parse with `ConvertFrom-Json`.
- SHINOBU_311 editor/report scan finds no stale `scannerUsesRoslynAst:false`, `PASS_SCOPED_TOKEN`, per-frame `.text =`, or interpolated string hits.
- SHINOBU_311 acoustic partial scan remains clean for `BinaryWriter`, `AbsoluteUniversePosition`, `Hecton8.World`, concrete World SDF helpers, `Interlocked`, `System.Linq`, `foreach`, `Pack=1`, and hot DTO auto-properties.
- `git diff --check` reports only existing LF/CRLF warnings in `PredatorCognitionDomain.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- No new build was launched because no C# source changed after the last guarded narrow compile.

<SELF_AUDIT agent="SHINOBU_311" revision="loop11">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">Legacy acoustic query archaeology used `rg`; no hidden predator hearing collider route was found.</TASK>
    <TASK id="02" result="PASS">Integrated as `PredatorCognitionDomain` partial, no competing standalone runtime.</TASK>
    <TASK id="03" result="PASS">Consumed existing `MovementAcousticSignal`, `AcousticPingSignal`, and `CombatDamageSignal` lanes; no invented signal.</TASK>
    <TASK id="04" result="PASS">No predator hearing `SphereCollider` was present; unrelated POI/CCD colliders were preserved.</TASK>
    <TASK id="05" result="PASS">No managed sound-listener route remained in target domain; hot input is SignalBus staged into Vault.</TASK>
    <TASK id="06" result="PASS">`GenerateMockAcousticSignalsJob` is Burst `IJobParallelFor`, opt-in, deterministic, fixed-slot.</TASK>
    <TASK id="07" result="PASS">`CalculateAcousticAttenuationJob` performs double AUP delta then guarded inverse-square local float math.</TASK>
    <TASK id="08" result="PASS">`EvaluateAcousticOcclusionJob` samples published Vault SDF bytes and rejects `Physics.Linecast`.</TASK>
    <TASK id="09" result="PASS">SDF probe count scales continuously from 1 to 8 through `GlobalQualityWeight` and tuning scale.</TASK>
    <TASK id="10" result="PASS">Cognition acoustic memory and drive scalars mutate through bounded `UnsafeUtility.AsRef` before the main cognition job.</TASK>
    <TASK id="11" result="PASS">Result DTO keeps normalized direction, source AUP, runtime source position, listener hash, and sound hash.</TASK>
    <TASK id="12" result="PASS">Gameplay-affecting acoustic jobs use deterministic Burst float mode for rollback truth.</TASK>
    <TASK id="13" result="PASS">Fully overwritten temp Vault lanes use uninitialized memory; the count lane is a 64-byte owner-written DTO, not a contended worker counter.</TASK>
    <TASK id="14" result="PASS">300-frame telemetry ring exists; fault dump is 16-byte LE header plus raw 64-byte telemetry rows.</TASK>
    <TASK id="15" result="PASS">UI Toolkit Acoustic Sensory X-Ray reads telemetry and mutates Vault tuning without hot gameplay ownership.</TASK>
    <TASK id="16" result="PASS">Cold CSV profile parser uses native scratch and `ReadOnlySpan<byte>`; no hot managed parser route.</TASK>
    <TASK id="17" result="PASS">SceneView debug draws acoustic wave discs from Vault DTOs.</TASK>
    <TASK id="18" result="PASS">`OOP_Hearing_Scanner` uses Roslyn `CSharpSyntaxTree` invocation AST and writes stable plus aggregate JSON proof.</TASK>
    <TASK id="19" result="PASS">Editor `InitializeOnLoad` guard asserts `AcousticStimulusDTO` size 32 and align 8.</TASK>
    <TASK id="20" result="PASS">XML prompt re-read, status/rationale/log updated, static scans clean, and guarded narrow Core compile reports no SHINOBU_311 errors.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <AcousticStimulusDTO size="32" align="8">`double3 EpicenterAUP` offset 0 size 24; `float InitialIntensity` offset 24 size 4; `uint SoundTypeHash` offset 28 size 4; padding 0; 32 = 4*8 = 2*16.</AcousticStimulusDTO>
    <AcousticCounter64DTO size="64" align="8">`Value@0`, `Capacity@4`, `Flags@8`, reserved fields through `@56`; one L1 cache line prevents false sharing on the count lane.</AcousticCounter64DTO>
    <SensoryTelemetryEntry size="64">Fixed 64-byte raw blackbox row; dump size is 16 + 300*64 bytes.</SensoryTelemetryEntry>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Low quality uses one SDF tap and capped mock stress; middle quality reaches 3-5 taps; ultra uses 8 taps. `math.lerp`/smooth quality shaping changes ALU depth and diagnostics only, not DTO layout, BufferID ownership, save identity, or cognition authority.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Zero new private persistent NativeArray ownership. Vault lanes: 72760 stimuli, 72761 64-byte counter, 72762 results, 72763 telemetry ring, 72764 telemetry cursor, 72765 profiles, 72766 profile count, 72767 tuning, 72768 CSV scratch. `71980..71987` plus `71989,71990` was rejected as parasite VFX ownership.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Consumes the existing predator swarm handle; acoustic subchain is mock, attenuation, occlusion, telemetry, then predator cognition. No schedule-time `.Complete()`. Separate NativeArray and raw pointer lanes are marked `[NoAlias]`; core/control pointer writes check lengths and slot bounds.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime asmdef dependency was added. Runtime source no longer imports `Hecton8.World` in the acoustic partial. Guarded narrow `Hecton8.Core.csproj` compile removed the SHINOBU_311 CS0246; remaining blockers are external Gameplay/VR/HandIK symbols. Current guard saw active `dotnet.exe` PID 20672, and no rebuild was needed after docs/report-only changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Trigger spheres and per-predator PhysX linecasts were replaced with bounded Burst inverse-square math and byte SDF taps. Complexity moved from scene-query broadphase plus managed fan-out to O(P*S*Q) contiguous math where S is capped at 128 and Q is continuous 1..8.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

<SELF_AUDIT agent="SHINOBU_311" revision="loop12">
  <SUBAGENT_FINDINGS_CLOSED>P0 staging race closed; P1 pre-raymarch cull closed; P1 read facades use read handles and writer-window guard; P2 dump throttle moved after successful IO; P2 measured chain timing patched into raw telemetry rows.</SUBAGENT_FINDINGS_CLOSED>
  <RACE_GUARD>Acoustic SignalBus staging no longer runs in `BeginDispatcherFrame`; the only `PrepareAcousticSdfSignals(frameId)` call is in `ScheduleFrameEvaluation` after `_evaluationScheduled` rejects overlapping job chains.</RACE_GUARD>
  <READBACK_GUARD>Telemetry/result/stimulus/count read facades return false/zero while `_evaluationScheduled` is true and resolve buffers through `OpenRead()`. Tuning read also uses `OpenRead()`.</READBACK_GUARD>
  <RAYMARCH_CULL>Occlusion now checks `raw < max(profile.HearingThreshold, Tuning.MinReceivedThreshold)` before any SDF sample, restoring Task 07 instant cull semantics.</RAYMARCH_CULL>
  <BLACKBOX_FORENSICS>Finalization patches latest `SensoryTelemetryEntry.EstimatedMicroseconds` with measured chain microseconds before raw dump. Dump throttle state is set only after header and raw rows are written.</BLACKBOX_FORENSICS>
  <REPORT_STABILITY>`OOP_Hearing_Scanner` generator now emits the same proof fields as stable/shared JSON, including race/read/cull/dump markers and `narrowCoreCompile`; scanner roots are non-overlapping `AI`, `Fauna`, optional top-level `Sensory`.</REPORT_STABILITY>
  <COMPILE_GUARD>Compile recheck launched after guard cleared to CPU 22.97% and no compiler processes. Narrow Core build reports no SHINOBU_311 errors; remaining failures are external Gameplay/VR/Combat/KCC blockers.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-22 - Loop 12 Subagent Race / Readback Hardening
What was wrong:
- Subagent audit found a P0 race: `BeginDispatcherFrame` could restage acoustic stimuli while the previous evaluation chain was still pending.
- SDF occlusion still raymarched raw candidates below the hearing threshold.
- Diagnostic read facades used mutable Vault resolution and could read during writer windows.
- Blackbox dump throttled before IO success.
- Raw telemetry rows kept estimated microseconds even when measured chain timing existed.
- The scanner generator could erase manually preserved proof fields on the next menu run.

What was done:
- Removed acoustic staging from `BeginDispatcherFrame`; staging remains inside `ScheduleFrameEvaluation` after `_evaluationScheduled` gate.
- Added raw threshold cull before `EvaluateSdfOcclusion`.
- Switched read facades to `OpenRead()` and made telemetry/result/stimulus/count reads return false/zero while `_evaluationScheduled`.
- Moved `_acousticSdfFaultDumped` and `_acousticSdfLastDumpFrame` writes after successful raw dump write.
- Patched the latest telemetry row with measured chain microseconds before readback/dump.
- Updated report generator output to preserve `blackBoxDumpFormat`, `narrowCoreCompile`, race/read/cull/dump proof fields, and removed nested `AI/Sensory` duplicate root.

Cinematic Cheats used:
- The Dear Lie is tighter: only audible candidates spend SDF taps; inaudible candidates die at inverse-square threshold.

Exact Microseconds saved:
- Worst-case mobile saving is `rejectedStimuli * activePredators * raySteps` SDF byte taps per scheduled acoustic chain. No profiler number claimed under current build guard.

Verification:
- Static scan shows only one `PrepareAcousticSdfSignals(frameId)` call remains, inside `ScheduleFrameEvaluation`.
- Static scan shows `OpenRead()` in acoustic diagnostic reads and `raw < hearingThreshold` before SDF occlusion.
- Runtime forbidden acoustic query scan remains empty for `Physics.CheckSphere`, `Physics.Linecast`, and `.ClosestPoint()` in AI/Fauna.
- Compile recheck launched after guard cleared to CPU 22.97% and no compiler processes. Narrow Core build reports no SHINOBU_311 errors; remaining 51 errors are external Gameplay/VR/Combat/KCC blockers.

## 2026-05-22 - Loop 13 Proof Hygiene
What was wrong:
- Owned status/rationale text still referenced the pre-Loop-12 compile marker after the newer guarded build changed the external blocker set.

What was done:
- Removed stale compile marker prose; Loop 15 later demoted compile proof to pending after post-build C# changes.

Cinematic Cheats used:
- None. Documentation proof hygiene only.

Exact Microseconds saved:
- 0 runtime us; prevents stale audit failures.

Verification:
- No C# changed, so no rebuild was launched.

## 2026-05-22 - Loop 14 Idle Job Suppression / SDF Fail-Open
What was wrong:
- Silent frames with predator cognition work still scheduled three SHINOBU_311 acoustic jobs.
- SDF samples outside the published voxel volume returned `0.0`, creating false partial dampening at streaming boundaries.

What was done:
- Added `RecordAcousticSdfIdleTelemetryAndClearResults` and an early return in `ScheduleAcousticSdfIntegration` when staged and mock stimulus counts are zero; idle rows reset stale measured acoustic chain timing.
- Changed out-of-volume, non-finite, and invalid-index SDF samples to fail open at `1.0`.
- Updated reports, generator proof fields, route doc, and binary ledger.

Cinematic Cheats used:
- Silent frames now use a one-row idle telemetry fake instead of proving silence with three scheduled jobs.

Exact Microseconds saved:
- Saves three job admissions and their dependency chain on silent predator frames. No profiler number claimed before guarded compile/profiler proof.

Verification:
- JSON reports parse with `idleFramesBypassAcousticJobs=true` and `sdfOutOfBoundsFailOpen=true`.
- Static scan of the acoustic partial still finds no `BinaryWriter`, `Hecton8.World`, `Interlocked`, `System.Linq`, `foreach`, `Pack=1`, or hot DTO auto-properties.

## 2026-05-22 - Loop 15 Subagent P1 Audit Closure
What was wrong:
- Fault dump path construction still happened in the runtime fault path.
- `OOP_Hearing_Scanner` could report unqualified `ClosestPoint()` as confirmed `Collider.ClosestPoint`.
- Reports still carried a build-gated compile marker even though Loop 14 changed C# after the last guarded compile.

What was done:
- Cached `Dump_SHINOBU_311.bin` path and directory creation during cold init.
- Narrowed scanner `ClosestPoint` proof to collider-like member receivers.
- Demoted report compile proof to `PENDING_AFTER_LOOP14_CPU_GUARD_BLOCKED` and changed scanner verdict to `PASS_ROSLYN_AST_TOKEN_SCAN`.

Cinematic Cheats used:
- None. This is proof and fault-path hygiene.

Exact Microseconds saved:
- Fault-only path construction removed. Runtime hot path unchanged.

Verification:
- Rebuild not launched: CPU sampled 99%, then 76%, then 100%; no active compiler process. Project rule blocks build above 50% CPU.
- Idle telemetry hash arithmetic is explicitly unchecked.

## 2026-05-22 - Loop 16 No-Due Frame Blackbox Closure
What was wrong:
- Idle acoustic telemetry was only written once `ScheduleAcousticSdfIntegration` ran. Silent frames with active predators but no due cognition cadence could leave the 300-frame ring stale.
- The XML extraction command was too narrow and failed when `AGENT_PROMPT` had additional attributes after the id.

What was done:
- Added owner-phase `RecordAcousticSdfIdleTelemetryFromCurrentTuning(frameId)` before the no-work early return in `ScheduleFrameEvaluation`.
- The helper refreshes continuous `GlobalQualityWeight`, preserves tuning ray-step scale, writes one idle telemetry row, and clears stale active-slot result rows without scheduling acoustic jobs.
- Re-ran robust CLI prompt extraction: 20 tasks, 30145 characters.
- Added `idleNoDueFramesWriteTelemetry=true` to stable/shared reports and synchronized route docs plus the binary ledger.

Cinematic Cheats used:
- Silence is represented by a single fixed telemetry row, not by scheduling attenuation/occlusion/telemetry jobs to prove that nothing happened.

Exact Microseconds saved:
- Saves three acoustic job admissions and all SDF taps on silent no-due frames while preserving blackbox continuity. No profiler number claimed before guarded compile/profiler proof.

Verification:
- Build not launched: CPU guard sampled 77%, no active dotnet/csc/VBCSCompiler. Rule blocks rebuild above 50% CPU.
- Post-verification guard sampled 80%, no active dotnet/csc/VBCSCompiler. Build remains blocked by rule, not by choice.
- Static prompt extraction reports `PROMPT_OK taskCount=20 length=30145`.

## 2026-05-22 - Loop 17 Parallel Result False-Sharing Closure
What was wrong:
- `AcousticEvaluationResultDTO` was 80 bytes. It is a parallel write target for attenuation and occlusion jobs, so adjacent predator result rows could share cache lines.

What was done:
- Expanded `AcousticEvaluationResultDTO` to explicit 128-byte stride.
- Payload remains through byte 79; reserved `ulong` padding now occupies offsets `80, 88, 96, 104, 112, 120`.
- Updated `AcousticEvaluationResultDtoSizeBytes` and ABI validation to expect 128 bytes and alignment >=8.
- Updated stable/shared scanner reports, route card, and binary ledger with `parallelResultFalseSharingGuard=true`.

Cinematic Cheats used:
- None. This is memory-layout hardening for the Burst result lane.

Exact Microseconds saved:
- No profiler number claimed. The expected gain is removal of false-sharing stalls under parallel acoustic writes; memory cost is +48 bytes per predator result slot.

Verification:
- JSON stable/shared reports parse and show `AcousticEvaluationResultDTO=128` plus `parallelResultFalseSharingGuard=true`.
- Static scan finds no remaining old 80-byte result stride markers in owned code/docs.

<SELF_AUDIT_DELTA agent="SHINOBU_311" loop="17">
  <STRUCT_LAYOUT_UPDATE>
    <AcousticEvaluationResultDTO size="128" align=">=8">`SourceAUP@0` size 24; `RuntimeSourcePosition@24` size 12; `Direction@36` size 12; `ReceivedIntensity@48`, `RawInverseSquareIntensity@52`, `OcclusionMultiplier@56`; hashes and compact flags through `@79`; reserved `ulong` padding at `@80,@88,@96,@104,@112,@120`. Final stride is two full 64-byte cache lines, preventing adjacent-row false sharing during parallel writes.</AcousticEvaluationResultDTO>
  </STRUCT_LAYOUT_UPDATE>
  <H_PHI_VAULT_STATUS>Same Vault lane `72762`; only row stride changed from 80 to 128. No new BufferID, owner, SignalBus route, or save identity was introduced.</H_PHI_VAULT_STATUS>
  <DEPENDENCY_GRAPH>No schedule route changed: `mock -> attenuation -> occlusion -> telemetry -> PredatorCognitionJob`. The result row padding only changes memory stride.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Compile proof remains pending because current machine guard shows active csc/VBCSCompiler/dotnet and CPU saturation. No rebuild launched.</COMPILE_GUARD>
</SELF_AUDIT_DELTA>

## 2026-05-22 - Loop 18 Idle Owner-Write Race Closure
What was wrong:
- Silent due frames could enter `ScheduleAcousticSdfIntegration` after `SwarmAnalysisJob` had been scheduled. The idle path then read `_activeSlots` and wrote telemetry/results from owner code after a dependency handoff.

What was done:
- Added `HasAcousticSdfWorkPending()` before the first job admission.
- Moved no-work idle telemetry/result clear into `ScheduleFrameEvaluation` before `SwarmAnalysisJob.TryScheduleParallelAdmitted`.
- Silent frames now bypass `ScheduleAcousticSdfIntegration` entirely after swarm job handoff.
- Kept the idle branch inside `ScheduleAcousticSdfIntegration` as a defensive `return dependency`.
- Updated report generator, stable/shared JSON, route card, and ledger with `idleTelemetryBeforeFirstJobSchedule=true`.
- Added `idleSkipsAcousticIntegrationAfterJobHandoff=true` to generated proof.

Cinematic Cheats used:
- Silent frames remain a single owner-phase telemetry row, not a scheduled proof job.

Exact Microseconds saved:
- No profiler number claimed. The fix removes a safety/race class while preserving the three-job admission saving from Loop 14/16.

Verification:
- Static source inspection shows `RecordAcousticSdfIdleTelemetryFromCurrentTuning(frameId)` at line before `SwarmAnalysisJob.TryScheduleParallelAdmitted`, and the idle branch in `ScheduleAcousticSdfIntegration` returns the dependency without touching owner arrays.
- Build still blocked by active compiler/CPU guard from Loop 17 sample.

## 2026-05-22 - Loop 19 Admission Retry / Scanner Conservatism
What was wrong:
- Subagent audit found that acoustic work staged before a failed `SwarmAnalysisJob` admission could be dropped because `_lastScheduledFrame` advanced and blocked same-frame retry.
- `OOP_Hearing_Scanner` only treated `ClosestPoint` as collider-like when the receiver name contained `Collider`, which missed real colliders named `hitbox`, `body`, or `volume`.

What was done:
- Changed the swarm admission failure branch to avoid advancing `_lastScheduledFrame` when `hasAcousticSdfWork` is true.
- Changed scanner detection to flag any member `ClosestPoint(...)` call in scoped source.
- Added `admissionFailureKeepsAcousticRetryOpen=true` and `closestPointMemberAccessConservative=true` to generated/stable/shared reports and synchronized docs.

Cinematic Cheats used:
- None. This is scheduler truth preservation and scanner proof hardening.

Exact Microseconds saved:
- 0 runtime us claimed. The retry fix prevents input loss under lane pressure; scanner change is editor-only.

Verification:
- Static source check confirms the admission failure branch now gates `_lastScheduledFrame = frameId` behind `!hasAcousticSdfWork`.
- Runtime forbidden scan remains clean for `.ClosestPoint(`, `Physics.CheckSphere`, and `Physics.Linecast` in the acoustic partial.
- Build not launched: CPU 100%, active `csc.exe` PID 27808, `dotnet.exe` PID 24076, and `VBCSCompiler.exe` PID 6564.

## 2026-05-22 - Loop 20 Tuning Bridge Closure
What was wrong:
- `AcousticTuningDTO.MaxDistanceMeters` was exposed through snapshots but did not gate Burst attenuation or occlusion.
- The X-Ray facade reset `MaxDistanceMeters` to `50f` and `FaultMicroseconds` to `1000f` on every tuning write.

What was done:
- Added a sanitized tuning max-distance squared clamp in `CalculateAcousticAttenuationJob` and `EvaluateAcousticOcclusionJob`.
- Added X-Ray UI Toolkit sliders for `Max Distance m` and `Fault Budget us`.
- Updated generated/stable/shared reports, route card, binary ledger, status, and rationale with `maxDistanceTuningAppliedInJobs=true` and `editorFacadeOwnsMaxDistanceAndFaultBudget=true`.

Cinematic Cheats used:
- No new physics. The range gate is still a cheap scalar clamp before the existing SDF visual-occlusion fake.

Exact Microseconds saved:
- No profiler number claimed. Tuned shorter ranges skip over-range candidates before SDF probes; worst-case savings scale with rejected predators * stimuli * raySteps.

Verification:
- JSON stable/shared reports parse and show both new proof fields as `true`.
- Forbidden acoustic partial scan remains clean for hidden `.Complete`, physics hearing calls, world dependency, LINQ/foreach, `BinaryWriter`, `Interlocked`, `Pack=1`, and hot DTO auto-properties.
- `git diff --check` passed for the Loop 20 touched files.
- Build not launched: CPU 96.43%, active `VBCSCompiler.exe` PID 6564. Second guard sample was CPU 65.94% with no compiler processes, still above the 50% threshold.

## 2026-05-22 - Loop 21 Subagent Cold-Path Proof Closure
What was wrong:
- Subagent audit found that hot frame scheduling could call the allocating acoustic Vault ensure route if cold acoustic allocation failed during boot.
- Unsafe pointer fields had only a short invariant comment instead of the mandated three-paragraph proof.
- The scanner report generator would regress the precise parasite BufferID ownership text on rerun.

What was done:
- Added `AreAcousticSdfVaultBuffersReady()` and changed frame-owned acoustic staging, idle telemetry, and integration to fail closed unless all acoustic Vault handles already exist.
- Kept `EnsureAcousticSdfVaultBuffers()` only on cold initialization and explicit tuning write routes.
- Added `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` above the raw `_cores`/`_controls` pointer fields.
- Fixed report generator collision text to `71980..71987 plus 71989,71990`.
- Added `hotScheduleDoesNotAllocateAcousticVault=true` and `unsafePointerJustificationParagraphs=true` to generated/stable/shared reports and architecture proof.

Cinematic Cheats used:
- No new simulation. The scheduler now skips acoustic work when the required native handles are not already boot-owned.

Exact Microseconds saved:
- No profiler number claimed. The fix removes a hidden cold Vault allocation/FileStream failure mode from hot frames.

Verification:
- Static scans stayed clean for forbidden runtime acoustic physics/audio/world/Complete/LINQ/foreach/BinaryWriter/Interlocked patterns.
- First guarded narrow build found one owned CS8156 at `PredatorCognitionDomain.AcousticSdf.cs(730,74)` plus external errors; the owned error was fixed by copying the tuning DTO before `in` sanitization.
- Second guarded narrow build produced no SHINOBU_311 errors. Remaining blockers are external `ConstructionManager.cs` missing deconstruction/refund/loot/teardown symbols and `HabitatDeconstructionTransactionKernel`.

## 2026-05-22 - Loop 22 Tuning Write Evaluation Fence
What was wrong:
- The editor/designer tuning write route could open and mutate `AcousticTuningDTO` while `_evaluationScheduled` marked a live acoustic job chain.
- The route could also enter the cold allocating `EnsureAcousticSdfVaultBuffers()` path from that write call before proving the job window was closed.

What was done:
- Added an `_evaluationScheduled` fence at the top of `TryWriteAcousticSdfTuning`, before any Vault open or cold allocation route.
- Added `tuningWritesRejectScheduledEvaluation=true` to the scanner generator and stable/shared reports.
- Updated the route card, binary ledger, status, rationale, and this log.

Cinematic Cheats used:
- None. This is a data-race fence on the designer bridge; the acoustic runtime still uses the SDF visual-occlusion fake instead of scene physics.

Exact Microseconds saved:
- No profiler number claimed. The fix removes a read/write race class and avoids an editor-induced cold Vault entry while jobs are active.

Verification:
- Static scan of `PredatorCognitionDomain.AcousticSdf.cs` remains clean for hidden `.Complete`, direct acoustic Physics calls, `.ClosestPoint`, `Hecton8.World`, `AbsoluteUniversePosition`, `System.Linq`, `foreach`, `BinaryWriter`, `Interlocked`, `Pack=1`, and hot DTO auto-properties.
- JSON stable/shared reports parse and contain `tuningWritesRejectScheduledEvaluation=true`.
- Narrow compile not launched after the C# edit: CPU sampled 100% with active `dotnet.exe`, `csc.exe`, and `VBCSCompiler.exe`. That Loop 22 marker is superseded by the Loop 23 compile marker after later C# changes.

## 2026-05-22 - Loop 23 Hooke Retry / Fault / Priority Closure
What was wrong:
- Non-finite acoustic faults set `AcousticFaultNonFinite`, but finalization did not include that flag in the raw dump predicate.
- Admission failure preserved only same-frame retry; if no second scheduler call happened that frame, later staging could overwrite the unconsumed acoustic stimuli.
- Movement staging could fill the 128-slot cap before combat/ping lanes.
- `SAFETY_JUSTIFICATION_PARAGRAPH_2` did not explicitly list rejected unsafe-pointer alternatives.

What was done:
- Added non-finite fault dumps to `FinalizeAcousticSdfTelemetry`.
- Added `_acousticSdfPendingStimulusRetry` and frame marker so staged stimuli survive across frames until the acoustic chain consumes them.
- Changed staging to combat, ping, then movement with fixed quotas; valid drops are written through `AcousticCounter64DTO.Reserved0` into `SensoryTelemetryEntry.Reserved0`, with counter flags copied to `Reserved1`.
- Rewrote unsafe pointer paragraph 2 to reject native-array aliases, setter command buffers, and duplicate acoustic patch arrays.
- Updated generator/stable/shared reports, route card, ledger, status, and rationale with Loop 23 proof and `PENDING_AFTER_LOOP23_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- Still no scene physics. The acoustic route remains capped SignalBus staging plus SDF visual-occlusion math instead of colliders, linecasts, or managed listener fan-out.

Exact Microseconds saved:
- No profiler number claimed. Priority staging prevents high-value signal loss at cap; retry/dump fixes are correctness and forensic hardening. No new jobs were added.

Verification:
- Static source check confirms the non-finite dump predicate, pending retry latch, priority staging order, drop telemetry copy, and rewritten unsafe pointer paragraph.
- Forbidden acoustic partial scan returned no hits for hidden `.Complete`, direct acoustic Physics calls, `.ClosestPoint`, `Hecton8.World`, `AbsoluteUniversePosition`, `System.Linq`, `foreach`, `BinaryWriter`, `Interlocked`, `Pack=1`, or hot DTO auto-properties.
- JSON stable/shared reports parse and contain `PENDING_AFTER_LOOP23_CPU_GUARD_BLOCKED`, `nonFiniteFaultDumpsBlackBox=true`, `admissionFailurePreservesStagedStimuliAcrossFrames=true`, and `priorityLaneStagingAndDropTelemetry=true`.
- `git diff --check` returned only existing LF/CRLF warnings for `PredatorCognitionDomain.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Narrow compile not launched: CPU sampled 98.65% with active `dotnet.exe` PIDs 10868, 16144, 23696, 25628, 27564, 28368, and 28480.

## 2026-05-23 - Loop 24 Invalid Ingress Fault Telemetry
What was wrong:
- Non-finite acoustic ingress could be rejected before the Burst attenuation/occlusion chain and appear only as a dropped stimulus, avoiding the non-finite blackbox dump predicate.

What was done:
- Added `AcousticCounterFlagInvalidIngress`.
- Movement, ping, and combat staging now finite-check source scalars plus AUP before append.
- Invalid ingress sets the counter flag, copies through telemetry `Reserved1`, and folds into `AcousticFaultNonFinite`.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `invalidIngressFaultTelemetry=true`.

Cinematic Cheats used:
- None added. The route still replaces physics hearing with capped SignalBus staging and SDF acoustic occlusion math.

Exact Microseconds saved:
- No savings claimed. This is fault observability and NaN containment with no new jobs or buffers.

Verification:
- Static scan found all invalid ingress flag call sites and no forbidden acoustic hot-path patterns.
- JSON stable/shared reports parse and contain `invalidIngressFaultTelemetry=true`.
- `git diff --check` returned only existing LF/CRLF warnings for `PredatorCognitionDomain.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Narrow compile not launched: CPU sampled 57.88% with active `dotnet.exe` PIDs 20844, 21968, 27036, 28124, 28164, 28740, and 29756.

## 2026-05-23 - Loop 25 Read-Only Handle Tightening
What was wrong:
- Three hot owner-phase helper paths inspected acoustic counter/tuning state through mutable Vault `Open()` even though they did not write those buffers.

What was done:
- Changed `MarkAcousticSdfDueWhenStimuliPresent`, `HasAcousticSdfWorkPending`, and `IsAcousticMockSignalModeEnabled` to use `OpenRead()`.
- Left mutable `Open()` only where the owner writes, schedules mutable job output buffers, cold-initializes, or executes the fenced tuning write bridge.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `hotReadOnlyHelpersUseOpenRead=true`.
- Compile proof marker was demoted to `PENDING_AFTER_LOOP25_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- None added. The acoustic route remains capped SignalBus staging plus SDF occlusion taps instead of PhysX hearing.

Exact Microseconds saved:
- No profiler number claimed. This is authority and audit hardening with no new jobs, buffers, or allocations.

Verification:
- Targeted static scan confirms no `.Complete`, hot DTO auto-properties, `foreach`, `System.Linq`, `BinaryWriter`, `Interlocked`, `Pack=1`, direct acoustic Physics query, `Hecton8.World`, or `AbsoluteUniversePosition` pattern in the acoustic partial.
- JSON stable/shared reports parse and contain `hotReadOnlyHelpersUseOpenRead=true`.
- Narrow compile not launched: CPU sampled 100% with active `dotnet.exe` PIDs 20844, 21968, 27036, 28124, 28164, 28740, and 29756.

## 2026-05-23 - Loop 26 Invalid-Only Idle Fault Closure
What was wrong:
- Subagent Poincare found that invalid-only acoustic ingress marked `AcousticCounterFlagInvalidIngress` while leaving `AcousticCounter64DTO.Value == 0`.
- The scheduler then treated the frame as no acoustic work and wrote an idle telemetry row that did not copy counter flags, so no `AcousticFaultNonFinite` row or dump was produced.

What was done:
- `RecordAcousticSdfIdleTelemetryAndClearResults` now reads the staged counter through `OpenRead()` before clearing stale results.
- Idle telemetry copies staged count, dropped count, and counter flags into `StimulusCount`, `Reserved0`, and `Reserved1`.
- Invalid ingress in idle telemetry sets `AcousticFaultNonFinite` and calls the raw blackbox dump after the row is written.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `invalidOnlyIdleFaultTelemetry=true`.
- Compile proof marker was demoted to `PENDING_AFTER_LOOP26_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- No fake physics or empty jobs. Invalid-only fault accounting rides the owner idle telemetry row and raw dump path.

Exact Microseconds saved:
- No profiler number claimed. The rejected alternative would add empty job scheduling; the implemented path adds one owner-thread counter read only on idle telemetry frames.

Verification:
- Poincare P1 finding is closed by code: idle row now copies counter flags and folds invalid ingress to `AcousticFaultNonFinite`.
- Narrow compile not launched: CPU sampled 100% with active `csc.exe` PID 25728 and active `dotnet.exe` PIDs 2404, 21144, 22160, 25820, 25892, 26580, 28336, and 30268.

## 2026-05-23 - Loop 27 Retry Latch Drift Cleanup
What was wrong:
- `_acousticSdfPendingStimulusRetryFrame` was write-only: assigned on retry mark/reset but never read.
- The Loop 25 read-handle tightening accidentally left `MarkAcousticSdfPendingRetry` writing the counter flag through `OpenRead()`.

What was done:
- Removed `_acousticSdfPendingStimulusRetryFrame` declaration and all assignments.
- Restored `_acousticSdfStimulusCounter.Open()` in `MarkAcousticSdfPendingRetry`; read-only helpers remain on `OpenRead()`.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `retryLatchNoWriteOnlyFrameField=true` and `pendingRetryCounterWriteUsesMutableOpen=true`.
- Compile proof marker was demoted to `PENDING_AFTER_LOOP27_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- None added. This is access-route cleanup on the existing retry latch.

Exact Microseconds saved:
- No profiler number claimed. One dead static field was removed; runtime behavior is unchanged except the retry counter flag uses the correct owner-write handle.

Verification:
- Static search found no remaining `_acousticSdfPendingStimulusRetryFrame`.
- Narrow compile not launched: CPU sampled 39%, but active `dotnet.exe` PIDs 2404, 21144, 22160, 25820, 25892, 28336, and 30268 remained.
- Second guard sample: CPU sampled 30%, but the same active `dotnet.exe` PIDs remained, so rebuild stayed blocked by the compiler-process rule.

## 2026-05-23 - Loop 28 Dump Path Fault-Path Retry Closure
What was wrong:
- `TryDumpAcousticSdfBlackBox` could still call `EnsureAcousticSdfDumpPathCold()` if the cached path was empty.
- If cold path setup failed earlier, a later budget/non-finite fault could retry managed `Path.GetFullPath`, `Path.GetDirectoryName`, and `Directory.CreateDirectory` work inside the fault writer.

What was done:
- Added a cached-path gate for fault export.
- Loop 29 supersedes the failed-cold-attempt lifetime so later cold/tuning-safe routes can retry.
- `TryDumpAcousticSdfBlackBox()` now fails closed when the cold latch is unset or the cached path is empty; it no longer retries path/directory resolution from the fault route.
- Reset clears the latch with the rest of acoustic domain state.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `dumpPathResolutionFaultPathRetryBlocked=true`.
- Compile proof marker was demoted to `PENDING_AFTER_LOOP28_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- None added. This is forensic path hardening; acoustic gameplay still uses the existing inverse-square plus SDF "Dear Lie" instead of physics linecasts.

Exact Microseconds saved:
- No profiler number claimed. The rejected alternative could run managed path/directory work during fault export; the implemented path adds one boolean branch before binary dump.

Verification:
- Static search confirms `_acousticSdfDumpPathInitialized` is declared, set in cold path, checked by the dump writer, and reset with acoustic state.
- Narrow compile not launched: CPU sampled 100% with active `dotnet.exe` PIDs 2404, 21144, 22160, 25820, 25892, 28336, and 30268.

## 2026-05-23 - Loop 29 Recoverable Cold Dump Path Retry
What was wrong:
- Boole audit found the Loop 28 latch made one cold dump-path exception permanent for the current acoustic domain state.
- That protected the fault writer from managed path work, but it also meant a later recoverable filesystem state could still lose `Dump_SHINOBU_311.bin`.

What was done:
- `EnsureAcousticSdfDumpPathCold()` now sets `_acousticSdfDumpPathInitialized` only after path resolution and directory creation succeed.
- The catch path leaves `_acousticSdfDumpPathInitialized=false`, allowing later cold retries.
- `EnsureAcousticSdfVaultBuffers()` retries cold dump-path setup even when acoustic Vault buffers are already created, so editor/tuning-safe calls can recover the path without touching the fault writer.
- Report generator, stable/shared JSON, route card, binary ledger, status, and rationale now include `dumpPathColdFailureRetryable=true`.
- Compile proof marker was demoted to `PENDING_AFTER_LOOP29_CPU_GUARD_BLOCKED`.

Cinematic Cheats used:
- None added. The existing acoustic SDF fake remains unchanged; this pass only preserves fault forensics.

Exact Microseconds saved:
- No profiler number claimed. The fault writer remains cached-path-only; the added work is a cold retry branch in safe ensure routes.

Verification:
- Static search confirms cold path success sets the latch, cold failure clears it, the fault writer checks the cached latch/path only, and route/ledger/report markers are synchronized to Loop 29.
- Narrow compile not launched: CPU sampled 28%, but active `dotnet.exe` PIDs 2404, 21144, 22160, 25820, 25892, 28336, and 30268 remained.
