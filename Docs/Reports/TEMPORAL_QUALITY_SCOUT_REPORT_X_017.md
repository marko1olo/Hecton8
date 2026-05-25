# TEMPORAL_QUALITY_SCOUT_REPORT_X_017

Agent: X_017  
Role: TEMPORAL_DISPATCH_AND_QUALITY_HOMEOS_SCOUT  
Domain: ECHELON 1 Core Infrastructure - Tick Dispatcher & Time Dilation / Scalability Dictator  
Mode: read-only static audit  
C# source modified: false  
Build/compile run: false

## Scope

Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` via `<AGENT_PROMPT id="X_017">`.

Files examined:
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/GameTickManager.cs`
- `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`
- `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs`
- `Assets/_Project/Scripts/MathLodApproximation.cs`
- `Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs`
- `Assets/_Project/Scripts/Core/ScalabilityContract.cs`

Mandates read:
- `ARCH_Execution_Phases.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `REND_DescriptorBinding_Reality_Check.txt`
- `REND_GPU_Sovereignty.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Dispatcher Phase Map

`SystemDispatcher` is declared at `Assets/_Project/Scripts/Core/SystemDispatcher.cs:65-67` with `DefaultExecutionOrder(-9950)`.

Core cadence constants:

| Constant | Value | Frequency | Evidence |
|---|---:|---:|---|
| `FastTickIntervalSeconds` | `1.0 / 60.0` | 60 Hz | `SystemDispatcher.cs:71` |
| `SlowTickIntervalSeconds` | `0.1` | 10 Hz | `SystemDispatcher.cs:72` |
| `ThermalCriticalSlowTickIntervalSeconds` | `0.2` | 5 Hz | `SystemDispatcher.cs:73` |
| `ColdTickIntervalSeconds` | `1.0` | 1 Hz | `SystemDispatcher.cs:74` |
| `FrostTickIntervalSeconds` | `5.0` | 0.2 Hz | `SystemDispatcher.cs:75` |
| `FixedStepSeconds` | `0.02` | 50 Hz | `SystemDispatcher.cs:121` |
| `MaxFixedSubstepsPerFrame` | `3` | n/a | `SystemDispatcher.cs:122` |
| `MaxCadenceSubstepsPerFrame` | `4` | n/a | `SystemDispatcher.cs:76` |

`RunDispatcherUpdate` sequence, source `SystemDispatcher.cs:5017-5201`:
1. Advance frame index and sample unscaled delta at `5019-5040`.
2. Run `HomeostasisBrain.PreSimulationTick(unscaledDeltaTime)` at `5042`.
3. Refresh `GlobalQualityWeight` at `5050`.
4. Resolve time dilation scalar and multiply delta at `5065-5072`.
5. Run master PRE_SIMULATION at `5073-5075`.
6. Run master SIMULATION at `5119-5120`.
7. Run frame, fast, unscaled fast, fixed, bucketed slow, slow, cold, and frost ticks at `5170-5179`.
8. Run master POST_SIMULATION at `5182`.

Late frame sequence, source `SystemDispatcher.cs:5358-5462`:
- Begins late-frame swap window at `5375`.
- Completes visual jobs and flushes fallback shader state at `5378-5392`.
- Runs `RunMasterVisualSyncPhase()` at `5392`.
- Drains bridges/signals and resets native arena at `5418-5455`.

Master phases:

| Phase | Method | Evidence | Fact |
|---|---|---|---|
| PRE_SIMULATION | `RunMasterPreSimulationPhase` | `SystemDispatcher.cs:2700-2740` | Calls `TimeSliceScheduler.BeginFrame(HomeostasisBrain.GlobalQualityWeight, timing.FrameId)` at `2708`; invokes systems whose dispatcher phase is PreSimulation. |
| SIMULATION | `RunMasterSimulationPhase` | `SystemDispatcher.cs:2742-2837` | Schedules `ScheduleSimulation`, combines `JobHandle`s, registers active simulation job; no immediate `.Complete()` in this phase. |
| POST_SIMULATION | `RunMasterPostSimulationPhase` | `SystemDispatcher.cs:2839-2894` | Calls `DispatcherJobFence.TryComplete(..., forceComplete: true)` when pending simulation jobs exist. |
| VISUAL_SYNC | `RunMasterVisualSyncPhase` | `SystemDispatcher.cs:2896-2939` | Visual sync can be shed; `VisualSyncTick` is skipped when shed. |

## Tick Scheduling Algorithms

Fixed tick, `SystemDispatcher.cs:6017-6056`:
- Accumulator input: scaled `deltaTime`.
- Cap: `FixedStepSeconds * MaxFixedSubstepsPerFrame = 0.06`.
- Loop: while accumulator >= `0.02` and substeps < `3`, dispatch fixed step and subtract `0.02`.
- If still overloaded after three substeps, accumulator is reset to `0`.
- Result: catch-up is bounded; excessive backlog is dropped.

Fast tick, `SystemDispatcher.cs:6188-6231`:
- Accumulate scaled delta.
- Dispatch `FastTick(1/60)` while accumulator >= interval and substeps < `4`.
- If still saturated, clamp leftover accumulator to one interval.

Unscaled fast tick, `SystemDispatcher.cs:6233-6273`:
- Same structure as fast tick.
- Uses unscaled delta and `UnscaledFastTick(1/60)`.

Slow tick, `SystemDispatcher.cs:6275-6394`:
- Interval comes from `ResolveSlowTickIntervalSeconds()`.
- Base interval: `0.1` seconds.
- Thermal critical interval: `0.2` seconds.
- Homeostasis emergency branch returns `1.0` seconds at `6373-6376`.
- Quality interval when bucket pressure is minimal: `lerp(0.1, 0.2, 1 - SmoothStep01(_globalQualityWeight01))` at `6383-6387`.
- Dispatches up to four slow ticks per frame, then clamps leftover to one interval.

Bucketed slow tick, `SystemDispatcher.cs:6331-6371`:
- Runs `IBucketedSlowTickable` only when `bucketer.IsSlowBucketActive(bucketedTickable.SimulationBucketId)` is true.

Cold tick, `SystemDispatcher.cs:6396-6438`:
- Accumulate scaled delta.
- Dispatch `ColdTick()` while accumulator >= `1.0` and substeps < `4`.
- If saturated, clamp leftover to `1.0`.

Frost tick, `SystemDispatcher.cs:6441-6478`:
- Accumulate scaled delta.
- If accumulator < `5.0`, return.
- Otherwise subtract `5.0` once and dispatch one `FrostTick()`.
- No loop and no explicit backlog clamp.

`GameTickManager`, source `Assets/_Project/Scripts/GameTickManager.cs`:
- Class declaration at `48`.
- Serialized `slowTickInterval = 0.5f` at `116`, so manager-level slow tick is 2 Hz.
- `Tick(float deltaTime)` at `336-386` invokes registered tickables and then `ProcessSlowTickIfNeeded`.
- `FixedTick(float fixedDeltaTime)` at `405-452` invokes fixed tickables.
- `ProcessSlowTickIfNeeded` at `463-475` adds delta, returns if below interval, otherwise resets accumulator to `0` and executes one slow tick.
- Drift fact: long-frame excess delta is discarded rather than subtracted or looped.

## Homeostasis Math

Scalability constants from `Assets/_Project/Scripts/Core/ScalabilityContract.cs`:

| Constant | Value | Evidence |
|---|---:|---|
| `HomeostasisFrameTimeWindow` | `120` | `17` |
| `HomeostasisBlackBoxCapacity` | `300` | `18` |
| `HomeostasisTelemetryCadenceFrames` | `60` | `19` |
| `HomeostasisRecoveryArmFrames` | `3000` | `20` |
| `HomeostasisRecoveryStepFrames` | `60` | `21` |
| `HomeostasisFrostPollSeconds` | `5f` | `22` |
| `HomeostasisFpsEwmaAlpha` | `0.1f` | `23` |
| `HomeostasisShiEwmaAlpha` | `0.12f` | `24` |
| `HomeostasisJitterUnstableSigmaMs` | `2.0f` | `25` |
| `TargetFrameMilliseconds` | `16.667f` | `34` |

Frame sampling, `HomeostasisBrain.cs:398-437`:
- `currentFps = clamp(1000 / max(0.001, frameMs), 1, 1000)` at `409`.
- `_fpsEwma = ComputeFrameEwma(_fpsEwma, currentFps, 0.1, _fpsEwma > 0)` at `410`.
- Frame milliseconds are stored in a 120-sample ring at `414-419`.
- Jitter sigma formula at `421-437`: `sqrt(max(0, sumSq/count - mean*mean))`.
- Jitter is rolling standard deviation, not EWMA.

EWMA implementation, `HomeostasisBrain.cs:1251-1275`:
- `ComputeFrameEwmaBurst` uses finite guard for current value.
- If unseeded or previous value is non-finite/non-positive, it returns the current value.
- Alpha is clamped to `[0,1]`.
- Formula: `lerp(previousValue, safeCurrent, safeAlpha)`.
- Function pointer compiled at `HomeostasisBrain.cs:267`.

System health, `HomeostasisBrain.cs:352-389`:
- Base SHI formula in Burst/fallback at `1228-1247`: `saturate(temp01*0.5 + batteryPressure01*0.3 + jitter01*0.2)`.
- Dictator can raise raw SHI at `HomeostasisBrain.ScalabilityDictator.cs:573-615`.
- Smoothed SHI formula: `seeded ? lerp(previous, rawShi, 0.12) : rawShi` at `378-380`.
- Result is saturated and passed through `ApplyHardwareShiFloor`.

Dictator raw SHI, `HomeostasisBrain.ScalabilityDictator.cs:573-615`:
- `frameOverTarget01 = saturate((frameMs - targetFrameMs) / targetFrameMs)`.
- `frameCurve = frameOverTarget01 * frameOverTarget01`.
- `vramGuard01 = saturate((vramPressure - 0.8) / (1 - 0.8))`.
- `vramCurve = vramGuard01^2 * (3 - 2*vramGuard01)`.
- `thermal01 = saturate((cpuTempC - 55) / 30)`.
- `jitter01 = saturate(jitterSigmaMs * 0.5)`.
- Polynomial: `saturate(frameCurve*0.35 + vramCurve*0.45 + thermal01*0.15 + jitter01*0.05)`.
- VRAM pressure over `0.85` raises raw pressure above `0.86`.

VRAM pressure, `HomeostasisBrain.ScalabilityDictator.cs:540-556`:
- Samples `Profiler.GetAllocatedMemoryForGraphicsDriver()`.
- Formula: `saturate(graphicsBytes / graphicsMemoryBudgetBytes)`.
- Mock pressure can raise the value.
- No EWMA exists on this path.

Memory constraints, `HomeostasisBrain.ScalabilityDictator.cs:1474-1523`:
- `mx350` GPU name maps known hardware constraint to `1.0`.
- System RAM constraint: `saturate((12288 - systemMemoryMb) / 8192)`.
- VRAM constraint: `saturate((4096 - graphicsMemoryMb) / 3072)`.
- Hardware pressure is max of model, RAM, and VRAM constraints.
- Hardware max quality is `lerp(1, SurvivalHardwareMaxQualityWeight, SmoothStep01(pressure))`.
- No memory usage EWMA was found.

## GlobalQualityWeight Flow

Source: `HomeostasisBrain.ScalabilityDictator.cs`.

Property and derived scalars:
- `GlobalQualityWeight => SanitizeQualityWeight01(_globalQualityWeight, 0f)` at `210`.
- `FractionalTimeSlice => lerp(0.1, 1.0, weight)` at `213` and `496-499`.
- `TargetRenderScale01 => lerp(0.5, 1.0, weight)` at `216` and `502-505`.

State update, `895-942`:
- `frameError01 = saturate((frameMs - targetFrameMs) / targetFrameMs)`.
- Integral increases by `frameError01 * frameSeconds`; decays by `frameSeconds * 0.25` when under threshold.
- Derivative is `max(0, frameError01 - previousError)`.
- `pidStress = saturate(error*0.55 + integral*0.30 + derivative*0.15)`.
- `stress = max(systemHealth, vramPressure, thermalIndex, pidStress)`.
- `desired = min(saturate(1 - stress), hardwareCeiling)`.
- Quality drops immediately when desired is below current.
- Quality recovers by at most `DefaultQualityRecoveryPerSecond * frameSeconds`.

Vault and downstream publication, `992-1042`:
- Writes `SystemHealthDTO` at `1009-1014`.
- Updates global quality at `1016`.
- Writes `ScalabilityStateDTO` at `1017-1022`.
- Calls `MathLodRuntimeConfig.PublishConfig` at `1023-1032`.
- Applies dynamic resolution at `1040`.
- Publishes shader globals at `1041`.

Shader global path, `969-977`:
- Uses `Shader.SetGlobalFloat` for `_GlobalQualityWeight` and `_H8GlobalQualityWeight`.
- Skips publication when delta is below `0.0005`, unless forced.
- This is not a CBuffer path.

Environment lighting CBuffer path:
- DTO definition: `HectonLightingRuntime_DayNightRelay.cs:20-30`.
- DTO population: `1145-1154`, with quality in `AmbientColor.w` and `SHQualityWeight`.
- Constant buffer allocation: `407-449`, double-buffered `GraphicsBuffer.Target.Constant`.
- Upload: `357-397`, `LockBufferForWrite`, `MemCpy`, `SetGlobalConstantBuffer`.

## Struct Layout Ledger

| Struct | Size | Evidence | 16-byte | Verdict |
|---|---:|---|---|---|
| `CriticalMemoryPressureEvent` | 32 | `SystemDispatcher.cs:39-46` | yes | Explicit offsets, explicit tail pad. |
| `MasterRollbackRuntimeStateProbeDTO` | 96 | `SystemDispatcher.cs:141-164` | yes | Explicit offsets, 16-byte multiple. |
| `HomeostasisBlackBoxEntry` | 64 | `HomeostasisBrain.cs:74-110` | yes | Four 16-byte lanes. |
| `SystemHealthDTO` | 16 | `HomeostasisBrain.ScalabilityDictator.cs:21-28` | yes | Runtime validator at `1889`. |
| `ScalabilityStateDTO` | 16 | `HomeostasisBrain.ScalabilityDictator.cs:33-40` | yes | Runtime validator at `1890`. |
| `MockHeavyLoadSignal` | 16 | `HomeostasisBrain.ScalabilityDictator.cs:45-54` | yes | Runtime validator at `1891`. |
| `MockTerrainSamplerStatus` | 16 | `HomeostasisBrain.ScalabilityDictator.cs:59-66` | yes | Runtime validator at `1892`. |
| `ScalabilityTelemetryEntry` | 32 | `HomeostasisBrain.ScalabilityDictator.cs:71-80` | yes | Runtime validator at `1893`. |
| `ScalabilityTuningDTO` | 16 | `HomeostasisBrain.ScalabilityDictator.cs:86-93` | yes | Runtime validator at `1894`. |
| `MathLodConfigDTO` | 64 | `MathLodApproximation.cs:245-264` | yes | Four 16-byte lanes; validator at `239`. |
| `EnvironmentLightingDTO` | 64 | `HectonLightingRuntime_DayNightRelay.cs:20-30` | yes | Four 16-byte lanes; CBuffer stride is 64. |

`MathLodConfigDTO` field map:
- Offset 0: `GlobalQualityWeight`
- Offset 4: `FractionalTimeSlice`
- Offset 8: `MinJacobiIterations` (`float`)
- Offset 12: `MaxJacobiIterations` (`float`)
- Offset 16: `PadeResidualCeiling`
- Offset 20: `BhaskaraResidualCeiling`
- Offset 24: `MathLodPressure01`
- Offset 28: `ActiveIterationBudget` (`float`)
- Offset 32: `Frame`
- Offset 36: `Flags`
- Offset 40: `LastFrameMs`
- Offset 44: `VramPressure01`
- Offset 48: `ThermalPressure01`
- Offset 52: `ReservedQualityLane0`
- Offset 56: `StateHash`
- Offset 60: `_pad0`

`EnvironmentLightingDTO` field map:
- Offset 0: `AmbientColor` (`float4`)
- Offset 16: `FogColor` (`float4`)
- Offset 32: `DirectionalLightColor` (`float4`)
- Offset 48: `SunIntensity`
- Offset 52: `MoonIntensity`
- Offset 56: `SHCoefficientCount`
- Offset 60: `SHQualityWeight`

## Risks

| ID | Severity | Evidence | Fact |
|---|---|---|---|
| RISK_01 | medium | `SystemDispatcher.cs:6373-6376` | `_homeostasisSlowTick2Hz` returns a 1.0 second interval; name says 2 Hz, behavior is 1 Hz. |
| RISK_02 | medium | `GameTickManager.cs:463-475` | Slow tick accumulator resets to zero and discards excess delta. |
| RISK_03 | medium | `SystemDispatcher.cs:6441-6478` | Frost tick subtracts one interval only; large excess remains. |
| RISK_04 | high | `SystemDispatcher.cs:2849-2857` | PostSimulation force-completes pending simulation jobs. |
| RISK_05 | low | `HomeostasisBrain.ScalabilityDictator.cs:969-977` | `GlobalQualityWeight` shader route is global floats, not CBuffer. |
| RISK_06 | low | `HomeostasisBrain.ScalabilityDictator.cs:540-556,1474-1523` | No memory usage EWMA found; VRAM pressure is instantaneous and hardware memory constraints are static. |

## Scalability Notes

Low/MX350:
- GPU name containing `mx350` maps to hardware constraint `1.0` at `HomeostasisBrain.ScalabilityDictator.cs:1491-1498`.
- Hardware ceiling constrains max quality at `1478-1483`.
- TimeSliceScheduler minimum budget is `0.10 ms` at `SystemDispatcher.cs:7134`.

Middle:
- Continuous `GlobalQualityWeight` drives fractional time slice, render scale, Math LOD config, and scheduler budget.
- Bucketed slow tick reduces work through simulation bucket admission.

High:
- TimeSliceScheduler interpolates to `1.10 ms` before ultra band.
- Quality recovery is gradual, not instant.

Ultra:
- TimeSliceScheduler upper budget is `2.00 ms`.
- `VisualOverkill` flags exist in `SystemBit` and Math LOD config flags.

## Verification

No C# source files were modified.  
Compile was not run because the task was read-only and no source was changed.  
Byte layout claims are based on explicit `StructLayout(LayoutKind.Explicit, Size = N)` plus existing `UnsafeUtility.SizeOf<T>()` validators where present.
