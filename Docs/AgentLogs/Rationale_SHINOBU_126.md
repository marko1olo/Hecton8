# Rationale_SHINOBU_126

Agent: SHINOBU_126
Role: VR_SOMATIC_COMFORT_ENGINEER
Status: PENDING VERIFICATION / BUILD BLOCKED BY COMPILER PROCESS GUARD

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
Hardware Impact: Fixed memory: state 64 B, derivatives 64 B, history 96 B, profiles 256 B, profile lookup 128 B, telemetry 19.2 KB, mock 8 KB, scratch 4 KB.

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
Hardware Impact: 19.2 KB fixed telemetry memory; per-frame write is one 64-byte struct.

## Decision 10 - CSV And Designer Facade

Problem: Designers need comfort tuning without recompiling C#.
Solution: Added cold `Data/UX/vr_comfort_profiles.csv`, span-based ASCII parser, FNV-1a profile hashes, editor tuner sliders, UI Toolkit root, and telemetry graph.
Rejected Alternatives: `string.Split`, managed row objects, runtime file parsing, or persistent `NativeHashMap` outside Vault. NativeHashMap was rejected because current DataVault provides buffer handles, not map handles; a fixed hashed profile array preserves Vault ownership.
Scalability potential: Profiles can tune Low/Middle/High/Ultra comfort response from data.
Hardware Impact: Parser/editor allocations are cold/editor-only; gameplay path remains 0 B GC.

## Decision 11 - Build Guard

Problem: Compile verification is required, but user forbids builds while CPU >50% or any `dotnet/csc/VBCSCompiler` process is running.
Solution: Ran static audits and guard. Latest guard was CPU 32% with 7 active `dotnet` processes, so no build was launched.
Rejected Alternatives: Violating explicit build guard or claiming compile success without output.
Scalability potential: No runtime effect; protects developer hardware and parallel agents.
Hardware Impact: Avoided build contention.

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
