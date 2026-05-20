# Rationale_SHINOBU_126

Agent: SHINOBU_126
Role: VR_SOMATIC_COMFORT_ENGINEER
Status: PENDING VERIFICATION / BUILD BLOCKED BY EXTERNAL GENERATED-PROJECT ERRORS

## Decision 01 - Live XML Authority

Problem: The working memory summary was stale and claimed the SHINOBU_126 XML block was absent.
Solution: Re-read `Docs/Tasks/CURRENT_BATCH.md` by CLI and used the live `<AGENT_PROMPT id="SHINOBU_126">` block as authority. Task count is 20.
Rejected Alternatives: Continuing from stale status would have produced false scope and false task reconciliation.
Scalability potential: The XML requires one continuous comfort kernel from flat-screen subtle vignette to VR strong tunnel; no tier fork.
Hardware Impact: Scope stays inside VR somatic comfort, avoiding cross-domain rebuild churn.

## Decision 02 - Data Sovereignty

Problem: The assignment requires persistent history, profiles, derivatives, mock sickness samples, telemetry, and CSV scratch without local persistent `NativeArray` ownership.
Solution: Added `BufferID.ShinobuVRSomaticComfortWrite/Read/Derivatives/History/Profile/ComfortTelemetry/MockSickness/CsvScratch` and resolved them through `VaultNativeArray<T>`.
Rejected Alternatives: Private `NativeArray` fields with `Allocator.Persistent`; unmanaged maps outside DataVault.
Scalability potential: Low/Middle/High/Ultra all read the same fixed Vault buffers; only cadence and scalars change.
Hardware Impact: Fixed memory: state 64 B, derivatives 64 B, history 96 B, profiles 256 B, profile lookup 128 B, telemetry 24 KB, mock 8 KB, scratch 4 KB.

## Decision 03 - BufferID Collision Repair

Problem: Initial comfort IDs `70150-70157` collided with Quest DAG buffers in `BufferID`.
Solution: Moved comfort IDs to free contiguous range `70166-70173` after enum collision audit.
Rejected Alternatives: Leaving duplicate enum values would let Vault routes alias unrelated quest data.
Scalability potential: Stable buffer identities are required for all hardware profiles.
Hardware Impact: Prevents undefined reads/writes; no runtime cost.

## Decision 04 - Camera-Independent Kinematics

Problem: VR comfort must protect against KCC/vehicle angular acceleration without depending on camera FOV or camera rotation properties.
Solution: Source motion from `SignalBus<KccVelocitySignal>` and AUP/head state; compute local AUP deltas by subtracting double positions before float cast; compute quaternion delta with guarded normalization.
Rejected Alternatives: `Camera.main`, `Camera.fieldOfView`, HMD-only yaw, GameObject transform dependencies.
Scalability potential: Low quality samples derivatives less often; high/ultra can sample every frame while using the same math.
Hardware Impact: Derivative job is O(1), cadence collapses toward 5 Hz at low quality; no GC.

## Decision 05 - SomaticComfortStateDTO ABI

Problem: Rendering needs a compact stable comfort payload with no CS1612 property copies and ARM64-safe layout.
Solution: `SomaticComfortStateDTO` uses `[StructLayout(LayoutKind.Explicit, Size = 32)]` with offsets 0/4/8/12/16 and raw public fields. Editor/development validation checks `UnsafeUtility.SizeOf` plus field offsets.
Rejected Alternatives: Sequential layout, properties, `Pack=1`, mutable camera properties.
Scalability potential: Same 32-byte payload feeds all quality levels and shader consumers.
Hardware Impact: One 32-byte write/read copy via `UnsafeUtility.MemCpy`.

## Decision 06 - Dear Lie Comfort Strategy

Problem: Physically simulating vestibular inertia or counter-rotating camera motion would be expensive and likely nauseating.
Solution: Use optical fakes: EWMA FOV tunneling scalar, horizon-lock blend scalar/quaternion, and foveated multiplier under thermal/VRAM pressure.
Rejected Alternatives: Camera transform override in solver, rigidbody/capsule inertia simulation, runtime postprocess profile mutation.
Scalability potential: Low: stronger tunnel, lower derivative sample cadence, more foveated pressure. Middle: default smoothing. High/Ultra: per-frame derivatives and less aggressive tunnel while preserving the same safety clamps.
Hardware Impact: O(1) jobs replace camera/postprocess churn; estimated avoided cost 35-80 us/frame versus transform/inertia simulation, plus avoided profile GC.

## Decision 07 - Continuous Sample Cadence

Problem: A `HistoryDepth` scalar alone did not actually reduce CPU load on weak hardware.
Solution: Kept XML formula `historyDepth = (int)math.lerp(2, 8, GlobalQualityWeight)` and added derivative sample stride `math.lerp(12, 1, GlobalQualityWeight)`. FOV and horizon jobs still run every frame against the last valid derivative so visual easing remains continuous.
Rejected Alternatives: Always sampling derivatives at 60 Hz; binary low-end switch.
Scalability potential: Low quality trends toward 5 Hz derivative sampling at 60 FPS; Ultra samples every frame.
Hardware Impact: Saves two AUP double3 reconstructions, quaternion delta, atan2, and vector clamps on skipped derivative frames.

## Decision 08 - Foveated Pressure Bridge

Problem: VR frame misses cause sickness; fill-rate pressure must feed comfort without a direct dependency on a sibling rendering assembly.
Solution: Read typed global health/thermal pressure signals and convert them into `FoveatedScaleMultiplier`.
Rejected Alternatives: Direct renderer calls, hardware-class booleans, camera pipeline mutation.
Scalability potential: Thermal pressure smoothly increases peripheral resolution reduction as `GlobalQualityWeight` drops.
Hardware Impact: Scalar pressure read is O(signal count), expected tiny; GPU savings happen downstream.

## Decision 09 - Black Box And Dump Path

Problem: Non-finite comfort derivatives must be reconstructable without managed hot-path logs.
Solution: Added 300-entry `ComfortTelemetryEntry` Vault ring and exceptional binary dump to `Docs/AgentLogs/Dump_VR_SURGEON.bin`; existing main blackbox still dumps `Dump_SHINOBU_126.bin`.
Rejected Alternatives: `Debug.Log` per frame, managed lists, text telemetry in gameplay.
Scalability potential: Same schema across all quality levels.
Hardware Impact: 24 KB fixed telemetry memory; per-frame write is one 80-byte struct.

## Decision 10 - CSV And Designer Facade

Problem: Designers need comfort tuning without recompiling C#.
Solution: Added cold `Data/UX/vr_comfort_profiles.csv`, span-based ASCII parser, FNV-1a profile hashes, editor tuner sliders, UI Toolkit root, and telemetry graph.
Rejected Alternatives: `string.Split`, managed row objects, runtime file parsing, or persistent `NativeHashMap` outside Vault. NativeHashMap was rejected because current DataVault provides buffer handles, not map handles; a fixed hashed profile array preserves Vault ownership.
Scalability potential: Profiles can tune Low/Middle/High/Ultra comfort response from data.
Hardware Impact: Parser/editor allocations are cold/editor-only; gameplay path remains 0 B GC.

## Decision 11 - Build Guard

Problem: Compile verification is required, but user forbids builds while CPU >50% or any `dotnet/csc/VBCSCompiler` process is running.
Solution: Waited until guard cleared at CPU 18.4% with 0 active compiler processes, then ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1`. Build failed with 65 errors in unrelated/stale generated-project dependencies; no compile success is claimed.
Rejected Alternatives: Violating the CPU/compiler guard, claiming compile success without output, or hand-editing generated csproj files as a false compile proof.
Scalability potential: No runtime effect; protects developer hardware and parallel agents.
Hardware Impact: Build used `/m:1` to avoid parallel compile pressure.

## Decision 12 - Mock Solver Injection And Profile Lookup

Problem: The mock sickness path filled sample rows but did not inject those values into the derivative buffer or run the comfort evaluator, so it could not actually profile smoothing. The CSV path also lacked map-style lookup behavior while a private `NativeParallelHashMap` would violate Vault ownership.
Solution: `GenerateMockSicknessData()` now schedules sample generation, injects one deterministic sample into `SomaticDerivativeDTO`, then runs FOV and horizon jobs. CSV ingestion now writes profiles plus a Vault-backed open-address lookup slot buffer.
Rejected Alternatives: Sample-only mock data, private persistent `NativeParallelHashMap`, managed dictionaries, or runtime `string.Split`.
Scalability potential: Mock amplitude and sample cadence continue to use `GlobalQualityWeight`; lookup capacity is fixed and deterministic.
Hardware Impact: Adds one 16-byte * 8 lookup table = 128 bytes fixed Vault memory. Mock injection remains cold/test path; gameplay hot path unchanged.

## Decision 13 - Skipped-Frame Derivative Timing

Problem: Quality-scaled derivative cadence initially skipped frames but divided the multi-frame AUP/quaternion delta by single-frame `dt`, overestimating velocity and acceleration at low quality.
Solution: `ComputeSomaticDerivativesJob` now derives `sampleDt = dt * frameDelta`, clamps frame delta to 1..120, and divides deltas by the real sample interval.
Rejected Alternatives: Removing quality cadence or accepting inflated acceleration spikes under thermal pressure.
Scalability potential: Low-tier cadence saves CPU without mathematically lying about velocity magnitude; Ultra remains per-frame.
Hardware Impact: Adds two integer guards and one multiply in the sampled derivative job; prevents false FOV tunnel spikes on weak devices.

## Decision 14 - NaN Writeback Hardening And Audit Boundary

Problem: A corrupt previous-state scalar could survive into FOV/horizon EWMA lerp, and telemetry foveated writes used `math.max` without a finite predicate. Static enum audit also found an unrelated `BufferID` value collision at `70200` outside the comfort range.
Solution: Guarded previous FOV and horizon scalars before interpolation, guarded telemetry foveated writes before ring insertion, kept SHINOBU comfort IDs in the unique `70166-70174` range, and documented the unrelated `70200` collision without editing another domain's ownership.
Rejected Alternatives: Assuming seeded buffers never corrupt; silently editing Save/Construction enum values outside SHINOBU_126 scope.
Scalability potential: Low/Middle/High/Ultra all share the same finite writeback path; only cadence and scalar magnitudes vary.
Hardware Impact: Adds three scalar finite checks in hot scalar publication/recording paths; prevents NaN propagation into shader constants, telemetry hashes, and blackbox dumps.

## Decision 15 - FOV Baseline Semantics Repair

Problem: The FOV target formula treated flat-screen/VR baseline as a side multiplier and then used `max()` with `FovAggressiveness`, allowing profile aggressiveness to erase the mandated continuous 0.05-style flat to 0.8-style VR intervention strength.
Solution: Renamed the stress scalar to `motion01`, computed `interventionStrength = lerp(FlatScreenBaselineFovTunnel, VrBaselineFovTunnel, RuntimeComfortBlend01)`, then calculated `target = saturate(motion01 * interventionStrength * responseGain * comfortWeight)`.
Rejected Alternatives: Keeping a cosmetic baseline that only affects output when it is larger than aggressiveness; adding an `if (isVR)` branch.
Scalability potential: Flat/Middle/VR/Ultra all use the same scalar kernel. Runtime mode, user profile, and `GlobalQualityWeight` only reshape multipliers.
Hardware Impact: Same operation count class; replaces one `max` route with explicit multiply chain and preserves deterministic scalar output.

## Decision 16 - NaN Pressure Gate Hardening

Problem: The Burst comfort jobs still relied on `math.saturate()` and `math.max()` around inputs that could be NaN after memory corruption or bad signals. That can preserve NaN and poison FOV, horizon, pressure, and shader globals.
Solution: Added `SanitizeJob01()` and `SanitizeJobNonNegative()` in the comfort job file; guarded derivative magnitudes, runtime comfort blend, impact shock, pressure inputs, foveated shader publication, and managed pressure release before interpolation.
Rejected Alternatives: Trusting DataVault initialization, or relying on `math.saturate()` as a NaN scrubber.
Scalability potential: Low/Middle/High/Ultra keep the same math path; the guards only prevent corrupt inputs from changing quality cadence or shader pressure.
Hardware Impact: Adds a small number of scalar finite predicates in O(1) jobs; avoids catastrophic NaN propagation into global shader constants and telemetry hashes.

## Decision 17 - Shader Consumer For Somatic Foveation

Problem: The foveated multiplier was published in `_HectonVRSomaticComfortState`, but CoreLit did not consume it, leaving the pressure valve as a data payload instead of a render effect.
Solution: `Hecton_CoreLit.hlsl` now reads `_HectonVRSomaticComfortState.z/w` inside `HectonCoreLitEvaluateXRFoveatedMask()` and scales peripheral resolve weight continuously by pressure. This is a shader-only consumer, so there is no direct C# assembly dependency on a sibling render domain.
Rejected Alternatives: Editing `HectonXRRuntimeState` ownership, calling renderer APIs directly from the gameplay provider, or adding a hardware-tier branch.
Scalability potential: Weak devices can increase peripheral quantized resolve under pressure; high/ultra devices keep full baseline unless pressure rises.
Hardware Impact: Adds three scalar HLSL ops plus finite clamps in the foveated mask. Expected GPU savings happen by reducing peripheral shader work when pressure rises.

## Decision 18 - Sub-0.3 Quality Collapse Curve

Problem: The pressure valve used `math.lerp` and polynomial smoothing, but the explicit sub-0.3 collapse threshold from the polish mandate had no `math.step` proof in the C# kernel.
Solution: Added `lowQualityWindow = 1 - math.step(0.3f, quality)` and multiplied it by a smoothed polynomial curve before adding a small extra foveated pressure gain. The branch is data/math only; no hardware-class boolean or `if (IsLowEndHardware)` route exists.
Rejected Alternatives: A binary low-end hardware switch, or adding a cosmetic `math.step` that had no runtime effect.
Scalability potential: Weak devices under `0.3` shed peripheral render cost harder, middle/high/ultra remain on the previous continuous pressure curve.
Hardware Impact: Adds one `step`, one polynomial smooth, and one multiply/add in the O(1) FOV job; expected savings are GPU-side through stronger peripheral foveation under pressure.

## Decision 19 - Pressure Route Consistency

Problem: The Burst job used `max(VRAM, thermal, system)` for foveated scale, but shader publication used only `max(VRAM, thermal)`. A system-health pressure spike could increase DTO scale while publishing zero shader pressure.
Solution: `PublishSomaticComfortShaderState()` now publishes `max(VRAM, thermal, system)` into `_HectonVRSomaticComfortState.w`, matching the job-side pressure route.
Rejected Alternatives: Leaving system pressure as telemetry-only, or introducing a separate shader global that would duplicate ownership.
Scalability potential: Any pressure source now activates the same continuous peripheral foveation response.
Hardware Impact: Adds one scalar `max` and one finite guard in publication; prevents under-foveation during system pressure events.

## Decision 20 - AUP Compile-Wall Boundary

Problem: `VRSomaticProvider.Comfort.cs` imports `Hecton8.World` for `AbsoluteUniversePosition`; if this file lived in an isolated gameplay runtime asmdef, that would be a direct sibling runtime dependency risk.
Solution: Audited nearest asmdefs. `Assets/_Project/Scripts/Gameplay/VRSomaticProvider*.cs` and root `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` both resolve to the existing root `Hecton8.Core.asmdef`; `VRSomaticProvider.cs` already uses the same AUP authority. Kept that route and added no new asmdef reference.
Rejected Alternatives: Duplicating the AUP struct inside SHINOBU comfort code, adding a new Core contract mid-batch, or editing World ownership. Those would create a second AUP truth or broaden the compile wall beyond the assigned domain.
Scalability potential: All hardware tiers use the same subtract-before-float AUP conversion; only sampling cadence and comfort scalars vary.
Hardware Impact: Zero runtime cost; prevents layout drift and avoids a broad assembly refactor in a dirty 20-agent workspace.

## Decision 21 - Pressure Signal Type Audit

Problem: Managed pressure ingress used `math.saturate()` on `PressureLevel`, `FoveatedPressureTier`, and thermal `Severity`; if those fields were floats, a NaN could leak into the comfort pressure max route.
Solution: Audited `GlobalSignals.cs`. `PressureLevel`, `FoveatedPressureTier`, and thermal `Severity` are byte fields; float pressure inputs (`SystemHealthIndex01`, `GpuUtil01`, `Pressure01`) already pass through `Sanitize01`. No code patch was required.
Rejected Alternatives: Adding redundant finite checks around byte fields or editing signal DTO ownership outside SHINOBU_126.
Scalability potential: Pressure response remains continuous across tiers and still uses the same max(VRAM, thermal, system) route.
Hardware Impact: Zero runtime change; avoided unnecessary instructions in the signal ingestion path.

## Decision 22 - Unity Meta And Generated Project Drift

Problem: `VRSomaticProvider.Comfort.cs` is a Unity asset file but lacked a `.meta`, and the generated `Hecton8.Core.csproj` is stale enough to omit the comfort partial while including the editor tuner.
Solution: Added `VRSomaticProvider.Comfort.cs.meta` with a unique GUID. Did not manually edit generated csproj files; Unity regeneration owns those and the current dotnet build is already blocked by unrelated missing-domain symbols.
Rejected Alternatives: Hand-editing `Hecton8.Core.csproj`, moving DTOs into the base provider to satisfy stale IDE project files, or deleting editor facade references. Those would be generated-file churn or architectural backsliding.
Scalability potential: No runtime effect; keeps Unity asset identity stable across all build targets.
Hardware Impact: Zero runtime cost; prevents Unity GUID churn on import.

## Decision 23 - Complete Pressure Telemetry

Problem: The foveated pressure route now uses `max(VRAM, thermal, system)`, but the 300-frame telemetry ring only stored VRAM and thermal pressure. A pure system-health spike could affect foveation while leaving the autopsy dump unable to explain why.
Solution: Expanded `ComfortTelemetryEntry` from 64 to 80 bytes, added `SystemPressure01` at offset 52, shifted hashes to offsets 56/60/64, added explicit padding at 68/72, and bumped `Dump_VR_SURGEON.bin` writer version from 1 to 2.
Rejected Alternatives: Hiding system pressure inside `StateHash`, dropping `AupHash`, or keeping the one-cache-line row at the cost of forensic blindness.
Scalability potential: Low/Middle/High/Ultra share the same telemetry schema; richer autopsy data does not alter runtime comfort math.
Hardware Impact: Telemetry ring grows from 19.2 KB to 24 KB. Per-frame write remains one aligned blittable struct and one ring slot.

## Decision 24 - One-Shot ABI Gate

Problem: The status claimed editor-time struct validation, but the runtime file only contained explicit layout attributes and comments. After the telemetry row grew to 80 bytes, a future field edit could silently break the binary dump schema or ARM64 alignment.
Solution: Added `ValidateSomaticComfortLayouts()` behind `UNITY_EDITOR || DEVELOPMENT_BUILD`, invoked before first Vault buffer acquisition. It checks `UnsafeUtility.SizeOf` and `Marshal.OffsetOf` for `SomaticComfortStateDTO` and `ComfortTelemetryEntry`, then caches success in a static bool so reflection never repeats per frame.
Rejected Alternatives: Relying on source comments, validating only `SomaticComfortStateDTO`, or running `Marshal.OffsetOf` every schedule call.
Scalability potential: No runtime quality fork. Low/Middle/High/Ultra all consume the same validated ABI; the guard is cold and does not alter the comfort math.
Hardware Impact: Zero player-build cost. Editor/development pay one cold layout check per domain load before Vault allocation; hot-path reflection cost is eliminated by the one-shot gate.

## Decision 25 - Legacy Quest Fallback Continuum

Problem: `VRSomaticProvider` still selected multiple comfort thresholds with `_useQuest2ComfortFallback`, `IsQuest2LikeRuntime()`, `HardwareTierDetector.IsQuest3Like`, and `SystemInfo` device-name string probes. That violated the continuous quality law in an active comfort route.
Solution: Replaced the bool with `_comfortPressureFallbackWeight01`. `RefreshComfortProfileSelection()` derives that scalar from `1 - GlobalQualityWeight` and XR frame interval, refreshes it through global state, scalability events, and the KCC comfort tick, then every legacy comfort threshold uses `math.lerp` through the scalar.
Rejected Alternatives: Keeping Quest 2/Quest 3 device-name forks, deleting the legacy presentation fallback outright, or adding another hardware-tier enum.
Scalability potential: Weak devices drift toward the more protective comfort parameters; middle/high/ultra continuously reduce that extra protection as frame interval and quality improve.
Hardware Impact: Removes managed string comparisons and hardware boolean checks from activation; adds a few scalar lerps where thresholds are resolved.

## Decision 26 - Dump Failure Without Console Strings

Problem: Both SHINOBU dump catch paths concatenated `exception.Message` into `Debug.LogError`. A black-box failure path must not allocate managed strings or rely on editor console output.
Solution: Replaced both catch logs with fixed-hash `GlobalTelemetryBus.PublishPerformanceWarning` calls using the dump context hash and `exception.HResult`.
Rejected Alternatives: Swallowing the exception silently, keeping `Debug.LogError` under editor guards, or writing a second text sidecar during an I/O fault.
Scalability potential: Same path across all hardware tiers; failure evidence goes through the project telemetry bus instead of a per-build console.
Hardware Impact: Removes two managed string concatenations from exceptional forensic paths. Normal frame cost remains zero because the catch path only runs on dump I/O failure.

## Decision 27 - Shared Layout Validator Reconciliation

Problem: The base partial already had `OffsetOf<T>` and `ValidateNativeLayouts()`. The new comfort partial temporarily duplicated the helper, and the base validator still expected `ComfortTelemetryEntry` to be 64 bytes.
Solution: Removed the duplicate helper from the comfort partial and updated the base validator to expect the 80-byte telemetry row and its pressure/hash/padding offsets.
Rejected Alternatives: Keeping duplicate private partial helpers, disabling the base validator, or leaving a false 64-byte contract in place.
Scalability potential: No runtime algorithm change. One ABI route protects all quality levels.
Hardware Impact: Prevents false editor/development layout faults; no player-build hot-path cost.

## Decision 28 - Active Batch Drift Handling

Problem: A fresh CLI extraction of `Docs/Tasks/CURRENT_BATCH.md` returned no `<AGENT_PROMPT id="SHINOBU_126">` block, while the existing status/log trail contains a 20-task matrix and previous live extraction evidence.
Solution: Treated disk Status/Rationale/LOG as the durable extracted task authority for this continuation and recorded the current batch drift instead of deleting scope or inventing a new task count.
Rejected Alternatives: Restarting from a missing prompt, relying on chat memory, or erasing already implemented SHINOBU work because the batch file rotated.
Scalability potential: No runtime change. The process guard prevents task-scope drift under context compression.
Hardware Impact: Zero runtime cost.

## Decision 29 - Local AUP Delta Before Float Cast

Problem: `ComputeSomaticDerivativesJob` reconstructed two absolute `double3` positions and then subtracted them. That is safer than float world-space but still violates the AUP rule for local math because the solver only needs frame-to-frame local delta.
Solution: Replaced absolute reconstruction with `ResolveLocalAupDeltaMeters(current, previous)`, computing `((current.Grid - previous.Grid) * CellSize) + (current.Local - previous.Local)` per axis, validating finite double delta, and immediately casting the localized result to `float3`.
Rejected Alternatives: Keeping absolute `double3` reconstruction, duplicating AUP authority in a new contract, or moving the derivative solver into the World domain.
Scalability potential: Low/Middle/High/Ultra share the same precise local-delta math; quality only changes sample cadence.
Hardware Impact: Removes two absolute vector reconstructions and one vector subtraction from the sampled derivative job. More importantly, it removes 100km-scale precision debt before angular/linear acceleration math.

## Decision 30 - Comfort Telemetry Ring Cursor

Problem: `_somaticTelemetryCursor` wrapped modulo 300 at write time. After the first wrap, `Dump_VR_SURGEON.bin` could report only the small post-wrap cursor count instead of the last 300 frames.
Solution: Made the comfort cursor unbounded like the main blackbox cursor; modulo now happens only for slot indexing, and dump count/start math can recover the last `min(cursor, ringLength)` entries.
Rejected Alternatives: Leaving the forensic dump blind after long endurance sessions or adding a separate managed list for dump ordering.
Scalability potential: Same telemetry schema across all hardware levels; no extra buffers.
Hardware Impact: One integer branch for `int.MaxValue` protection on telemetry write; restores correct 300-frame autopsy coverage.

## Decision 31 - Mock Hook Job Ownership

Problem: `GenerateMockSicknessData()` checked `_somaticComfortHandle.IsCompleted` before scheduling new mock/evaluator jobs, but `IsCompleted` alone does not finalize Unity job safety ownership for the Vault-backed arrays.
Solution: The hook now calls `TryPublishCompletedSomaticComfortNoBlock()` after buffer acquisition and exits if `_somaticComfortJobScheduled` remains true. New mock writes only begin after the dispatcher finalizes the prior handle and publishes the completed write state.
Rejected Alternatives: Calling `JobHandle.Complete()` directly in the cold hook, resolving unsafe array pointers while a completed-but-unfinalized job still owns them, or ignoring the mock path because it is not gameplay hot path.
Scalability potential: Low/Middle/High/Ultra mock profiling keeps the same deterministic solver route; only scheduling safety changed.
Hardware Impact: Hot path unchanged. Cold profiler path adds one non-blocking dispatcher finalize attempt and prevents safety-handle faults during repeated mock injections.

## Decision 32 - CSV Scratch Seed Gate

Problem: `ShinobuVRSomaticCsvScratch` was requested from the DataVault but was not part of the seed readiness gate. A failed scratch acquisition could still mark the comfort domain seeded, contradicting the H-PHI claim that persistent comfort memory is Vault-owned and present.
Solution: Added `_somaticCsvScratch.IsCreated` to the seed gate before the seed/clear/mock jobs are scheduled.
Rejected Alternatives: Treating the scratch arena as optional dead weight, or deleting it and weakening the documented CSV zero-GC parsing route.
Scalability potential: No runtime quality fork. All tiers now share the same complete Vault acquisition contract before comfort state becomes active.
Hardware Impact: Cold boot guard only; no frame-path instructions added.

## Decision 33 - Active XML Reconciliation

Problem: An interim read saw the SHINOBU_126 block missing from `CURRENT_BATCH.md`, but a fresh cover-to-cover CLI extraction now shows the block present again at line 1372. The status file needed to stop reporting the drift as current.
Solution: Re-extracted the live XML block, verified the task count remains 20, and updated Status to record batch rotation resilience instead of a stale missing-block condition.
Rejected Alternatives: Trusting stale drift notes, trusting chat memory, or re-scoping after the active batch returned to the expected XML authority.
Scalability potential: No runtime effect. Process integrity prevents wrong-agent scope bleed in a 20-agent workspace.
Hardware Impact: Zero runtime cost.

## Decision 34 - Main Scheduler Job Ownership

Problem: `ScheduleSomaticComfortKernel()` attempted a non-blocking publish/finalize, then only returned when `_somaticComfortJobScheduled && !_somaticComfortHandle.IsCompleted`. If `IsCompleted` was true but `DispatcherJobSwap.TryFinalizeCompleted()` returned false, the scheduler could resolve unsafe Vault pointers while the job safety handle was still not finalized.
Solution: After `TryPublishCompletedSomaticComfortNoBlock()`, the main scheduler now exits on `_somaticComfortJobScheduled` regardless of `IsCompleted`. New simulation writes only begin after the dispatcher clears the scheduled flag through `PublishSomaticComfortStateFromWrite()`.
Rejected Alternatives: Calling blocking `Complete()`, trusting `IsCompleted` as an ownership fence, or resolving pointers under a completed-but-unfinalized handle.
Scalability potential: No quality fork. All tiers use the same job ownership contract; quality only affects sample cadence and foveated/FOV scalars.
Hardware Impact: One branch in the scheduling path. Prevents Unity safety-handle faults without blocking the simulation phase.

## Decision 35 - CSV Import Uses Vault Scratch

Problem: The editor comfort CSV facade still used `File.ReadAllBytes`, creating a managed staging array before invoking the otherwise span-based parser. The parser was allocation-free, but the import route did not use the declared Vault scratch arena.
Solution: `SomaticTunerWindow.ImportComfortCsv()` now requires `ShinobuVRSomaticCsvScratch`, reads the file into that Vault-owned `NativeArray<byte>` through an unsafe `Span<byte>`, and passes a `ReadOnlySpan<byte>` over the same scratch memory into `ParseComfortProfilesCsv`.
Rejected Alternatives: Keeping `File.ReadAllBytes`, adding a private persistent editor buffer, or parsing via `string.Split`. The import is editor-cold, but the route now matches the DataVault ownership proof.
Scalability potential: Low/Middle/High/Ultra profiles are still authored in CSV and written into the same fixed Vault profile/lookup buffers; no hardware fork.
Hardware Impact: No gameplay frame cost. Editor import avoids an extra managed byte-array staging allocation and keeps profile hydration tied to the declared 4096-byte scratch arena.

## Decision 36 - Runtime Comfort Blend Without XR Bool In Core Math

Problem: `ScheduleSomaticComfortKernel()` still converted `HectonXRRuntimeState.IsXRActive` into a binary `math.select(0.0625, 1.0)` before feeding `EvaluateFovTunnelingJob`. The current runtime registration remains XR-gated by the broader provider, but the FOV solver itself must not depend on a runtime-mode bool.
Solution: Replaced the XR-active select with `ResolveRuntimeComfortBlendTarget01(GlobalQualityWeight, _comfortPressureFallbackWeight01)`. The target now lerps `0.92..1.0` through `Smoothstep01(max(1 - quality, fallback))`, so weak/pressured devices increase protective intervention through continuous scalar math while the Burst job still uses the profile's flat/VR baselines.
Rejected Alternatives: Keeping the binary XR select, widening the runtime bootstrap to allocate/bind a VR provider for flat-screen play during this patch, or moving mode authority into the rendering assembly. Widening bootstrap would add scene/runtime allocation risk outside the requested comfort math fix.
Scalability potential: Low devices and throttled frame intervals bias toward stronger comfort; middle/high/ultra converge toward the less intrusive top-end target while retaining the same EWMA and foveated-pressure math.
Hardware Impact: Replaces one boolean select with one smoothstep/max/lerp scalar chain. CPU cost is still O(1); avoids mode-popping and removes a core-math binary switch.
First 20 Minutes moment: Swim and hazard response.
Route impact: Reduces nausea risk during early underwater movement and first impact/hazard moments without touching gameplay truth or save state.
Proof required: Unity import, Play Mode route pass, GCMonitor hot-path capture, and headset/frame-timing capture remain pending.
Parked work rejected: No binary `.h8bin` comfort payload loader was added because the active ledger marks those UX comfort binaries as `SCRIPT_TOOL_ONLY` with no runtime load proof; the current human bridge remains CSV-to-Vault.

## Decision 37 - VR Comfort Binary Payload Boundary

Problem: The project already contains `Data/UX/VR_Comfort_Profiles.h8bin`, a toaster variant, and an RTX overkill supplement. The polish mandate required binary/endian awareness, but the active SHINOBU_126 XML explicitly asks for `vr_comfort_profiles.csv` ingestion and the ledger classifies these binaries as `SCRIPT_TOOL_ONLY`.
Solution: Read the VR comfort binary layout, HLSL integration, verification JSON, and binary payload ledger. Confirmed the files are little-endian, 16-byte aligned, offline-tool validated, and not loaded by first-party runtime C#; only Python verification/data-truth tools reference them. Kept runtime implementation on the CSV-to-Vault path for this task.
Rejected Alternatives: Adding a gameplay `.h8bin` reader without a UX tier selector, ownership route card, runtime load proof, and staged DataVault swap; parsing JSON or binary in Tick; or silently claiming the existing binaries are runtime-wired.
Scalability potential: Low/Middle/High/Ultra can later map to the toaster/base/overkill payloads, but that requires a separate selector with hysteresis and generation-safe Vault swap.
Hardware Impact: Zero runtime change. Avoids adding cold file I/O and endian/migration code into the active comfort scheduler.
First 20 Minutes moment: Swim and hazard response.
Route impact: Prevents false data-readiness claims from blocking comfort math validation on the early movement route.
Proof required: A future binary ingest task needs an editor bake/load test, CRC validation, staged Vault swap, Unity import, Play Mode, and GCMonitor proof.
Parked work rejected: Runtime `.h8bin` loader and tier selector are parked because the active task's source of truth is CSV and current ledger status is `SCRIPT_TOOL_ONLY`.

## Decision 38 - Post-Compaction Verification Guard

Problem: Context compaction can make prior verification claims stale, and the build guard still forbids compile attempts when total CPU is over 50%.
Solution: Re-ran `git diff --check` on SHINOBU-touched files, re-ran the forbidden-token scan on the comfort runtime/editor files, checked git status for touched files, and re-ran the CPU/compiler guard. Static checks still pass; CPU is 100% and compiler process count is 0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` against an overloaded workstation, trusting pre-compaction memory, or claiming compile proof from static scans.
Scalability potential: No runtime algorithm change. Process discipline prevents verification churn from stealing CPU from parallel agents.
Hardware Impact: Zero player runtime cost; protects the developer machine and avoids a parallel-agent compile wall.
First 20 Minutes moment: Verification gate for swim/hazard comfort route.
Proof required: A later guarded Unity/project build when CPU drops below 50% and no compiler process is active.

## Decision 39 - CSV Import Fail-Closed Read

Problem: `ReadFileIntoScratch()` returned a positive byte count even if `FileStream.Read(Span<byte>)` ended before the expected CSV length. That could hydrate a truncated comfort profile table into Vault buffers from a partial editor import.
Solution: The editor facade now returns `-1` on short read, empty/oversized file, `IOException`, or `UnauthorizedAccessException`. Live CSV profile hashes were verified against the current file: `Novice=0xBC45CD7B`, `Veteran=0xE27847B4`, `Disabled=0xBFCE9925`, `Quest3=0x47CA36AA`, matching the code constants, so no hash-folding patch was made.
Rejected Alternatives: Parsing partial bytes, catching all exceptions without type discipline, or lowercasing the hash path when current data and constants are case-exact.
Scalability potential: Low/Middle/High/Ultra profiles still hydrate through the same Vault scratch buffer and fixed lookup table; corrupt imports fail closed instead of silently changing comfort behavior.
Hardware Impact: No gameplay frame cost. Editor-cold path adds two typed catches and a length equality guard; hot comfort scheduler remains unchanged.
First 20 Minutes moment: Swim and hazard response tuning.
Proof required: Unity editor import/menu interaction and Console check remain pending.

## Decision 40 - Remove Hardware-Class Fallback Names

Problem: The behavior had already been converted from Quest-specific detection to continuous pressure/quality fallback, but private constants, the fallback field/parameter name, and one black-box flag still used hardware/Quest terminology. That naming preserves the wrong mental model and invites future binary hardware branches.
Solution: Renamed the private fallback constants to `PressureFallback*`, nominal frame-safety constants to `Nominal*`, `_comfortHardwareFallbackWeight01` to `_comfortPressureFallbackWeight01`, and `BlackBoxFlagQuest2Fallback` to `BlackBoxFlagProtectiveFallback`. Runtime values and serialized field names were not changed; only private symbol names and inspector tooltips were made architecture-neutral.
Rejected Alternatives: Leaving hardware-class names around continuous math, changing serialized field names and risking inspector data churn, or deleting the authored `Quest3` CSV profile hash despite it being data, not a hardware probe.
Scalability potential: The fallback continuum remains Low/Middle/High/Ultra capable: quality and frame interval feed `_comfortPressureFallbackWeight01`, then all thresholds are resolved by `math.lerp`.
Hardware Impact: Zero runtime instruction change; this is compile-time symbol hygiene to preserve the no-binary-switch contract.
First 20 Minutes moment: Swim and hazard response.
Proof required: Unity import/serialized field inspection remains pending.

## Decision 41 - Mock And Seed Late-Frame Publication Fence

Problem: `GenerateMockSicknessData()` schedules the mock sample, derivative injection, FOV, and horizon jobs, then sets `_somaticComfortJobScheduled = true`. `EnsureSomaticComfortBuffers()` does the same for the cold seed/mock buffer initialization chain. Without immediate late-frame registration, either cold route can leave the write buffer owned by a scheduled handle until another runtime path happens to register the dispatcher callback.
Solution: Added `TryRegisterLateFrame()` directly after both cold comfort job chains are scheduled. The mock hook and seed path now mirror the main `ScheduleSomaticComfortKernel()` publication route and still avoid a blocking `JobHandle.Complete()`.
Rejected Alternatives: Calling `Complete()` in the cold hook or seed path, relying on the next gameplay scheduler tick, or publishing the write buffer before the job safety handle is finalized.
Scalability potential: Low/Middle/High/Ultra mock profiling and cold seeding still exercise the same continuous quality math; only the ownership fence changed.
Hardware Impact: One cold method call when the editor/profiler mock hook or boot seed route schedules jobs; no gameplay hot-path instructions added.
First 20 Minutes moment: Swim, fast roll, and collision comfort validation can now run the mock/seed route without a stranded publication handle.
Proof required: Static scan now; Unity import and Play Mode mock invocation remain pending behind the build/editor guard.

## Decision 42 - Post Loop 38 Verification Guard

Problem: The first static context check after the mock patch revealed the same missing late-frame registration in the cold seed route. Verification had to prove all comfort job schedule sites, not only the editor mock hook.
Solution: Patched the seed route, then re-ran whitespace diff scan, forbidden-token scan, Burst directive mismatch scan, schedule-site context scan, and CPU/compiler guard. Static proof passes; build remains forbidden by CPU policy.
Rejected Alternatives: Claiming the mock patch was sufficient, launching `dotnet build` while CPU is at 100%, or treating the seed route as harmless because it is cold.
Scalability potential: All quality tiers now share one publication contract for seed, mock, and main comfort jobs.
Hardware Impact: No hot-path cost. Cold routes pay one registration call. Build guard protected the workstation during parallel-agent load.
First 20 Minutes moment: Mocked high-roll and impact sickness scenarios can exercise the same visual comfort route without leaving scheduled write ownership unresolved.
Proof required: Unity import, Play Mode mock invocation, and headset/frame-timing capture remain pending.

## Decision 43 - Guarded Build Blocked By Construction Source Deletion

Problem: After the static scans passed, the CPU/compiler guard cleared at CPU=41.6% and zero compiler processes, making one guarded minimal build justified. The build failed before SHINOBU code on CS2001 because `Hecton8.Core.csproj` references `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, but that file is currently absent and shown as deleted in git status.
Solution: Recorded the exact blocker and stopped. Did not hand-edit `Hecton8.Core.csproj` because Unity owns generated project files, and did not revert the deleted Construction file because it is outside SHINOBU_126 ownership.
Rejected Alternatives: Reverting another agent's Construction deletion, modifying generated csproj by hand, or claiming compile proof from a build that never reached this domain.
Scalability potential: No runtime algorithm change. Compile-wall discipline preserves owner-local routing in a parallel workspace.
Hardware Impact: Build used `/m:1 --no-restore` and stopped after one external missing-source error; no further build attempts were made.
First 20 Minutes moment: Verification remains blocked before headset/mock route can be imported in Unity.
Proof required: Construction owner or Unity project regeneration must resolve the missing source reference before a meaningful SHINOBU compile proof can be collected.
