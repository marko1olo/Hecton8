# LOG_SHINOBU_21

## 2026-05-17 - Physiology and Decompression Runtime

What was wrong:
- No SHINOBU-owned 16-compartment physiology authority existed in the active Physiology asmdef.
- Existing survival/health surfaces still expose legacy scalar health concepts; direct rewrite of `HectonPlayerHealth` would cross the Gameplay boundary and create a compile wall during a concurrent batch.
- No vault-owned 300-frame physiology blackbox existed for O2/nitrogen/trauma failure autopsy.
- No designer-facing metabolic editor facade or CSV override path existed for the new unmanaged physiology rows.

What was done:
- Added aligned unmanaged DTO contract in `ShinobuPhysiologyData.cs`.
- Added Burst jobs in `ShinobuPhysiologyJobs.cs`: mock environment drop, signal ingest, 16-compartment Haldane decompression, O2/adrenaline/toxemia/hypothermia solver, vitals export, heartbeat pulse emission, blackbox writer.
- Added vault-backed runtime in `ShinobuPhysiologyRuntime.cs`: leases all persistent state from GlobalDataVault, computes AUP depth in double before float job input, monitors `biology_constants.csv`, writes fatal dumps to `Docs/AgentLogs/Dump_AUTOPSY_REPORT.bin`.
- Added `Hecton8.Physiology.Editor` and `MetabolicControlCenterWindow.cs`: Play Mode sliders and 16-tissue histogram with red M-value breach bars.
- Added BufferIDs `ShinobuPhysiologyVitals` through `ShinobuBiologyCsvOverrides` in `H8Memory.BufferID`.
- Changed `Hecton8.Physiology.asmdef` to allow unsafe code and reference Core/Core.Contracts/Core.Memory/Burst/Collections/Jobs/Mathematics.

Cinematic Cheats used:
- Dear Lie decompression: 16 scalar Haldane tissues, not organs or blood-flow.
- Nitrogen narcosis: one `NarcosisSeverity` scalar over 4 atm, not hallucination entities.
- Low-tier survival: one fastest tissue compartment when `_MATH_LOD_LOW` or `SystemHealthIndex01 > 0.85`.
- Presentation overkill is decoupled: UI/audio/shader agents consume scalars; gameplay truth stays numeric.

Exact Microseconds saved:
- Exact profiler measurement: 0 us claimed. No Unity Profiler run was executed.
- Engineering estimates: low-tier one-compartment path saves 15-45 us/frame versus all 16 compartments at 50-64 bodies; bitmask trauma saves 2-8 us versus status-class dispatch; SignalBus pulse path saves 2-8 us versus managed event fan-out; row-0 telemetry saves 1-4 us at 64 bodies versus per-entity ring writes.
- Total estimated low-tier saving: 20-65 us/frame in the stressed scenario. This is an estimate, not a measured result.

<SELF_AUDIT>
20-TASK CHECK:
01 [PASS] Binary graveyard scanned; missing tables route to `GenerateEmergencyMockMetabolism()`.
02 [PASS WITH COMPATIBILITY CAVEAT] Physiology truth is vault DTO, not `float Health`; legacy Gameplay facade remains untouched to avoid compile wall.
03 [PASS] Raw DTO fields and `GetVitalsRef()` with `UnsafeUtility.ArrayElementAsRef<PhysiologyDTO>`.
04 [PASS] `PhysiologyDTO` 32 bytes; `DecompressionStateDTO` 80 bytes; no `Pack=1` in Physiology.
05 [PASS] Mock pressure, toxemia, combat, predator, medical, and environment signals added.
06 [PASS] Burst Haldane decompression kernel implemented.
07 [PASS] Narcosis is scalar only; no fake actors.
08 [PASS] O2 drain from heart/adrenaline/trauma/toxemia/hypothermia.
09 [PASS] Trauma bitmask bits 0-3 implemented.
10 [PASS] Newton cooling, thermal suit bit, shiver scalar.
11 [PASS] Adrenaline spike, 20% swim bonus, 60s decay default, crash fatigue x2.
12 [PASS] Toxemia accumulates and medical purge runs over 10 seconds.
13 [PASS] Low LOD fastest-tissue branch implemented.
14 [PASS] Typed `CardiacPulseSignal` emitted through `SignalBus<CardiacPulseSignal>`.
15 [PASS] 16-byte `VitalsExportDTO`.
16 [PASS] AUP depth isolated in double, job receives float depth.
17 [PASS] 300-frame row-0 blackbox ring and fatal dump path.
18 [PASS] Editor-only Metabolic Control Center sliders.
19 [PASS] Root CSV monitor with span-based ASCII parser and hashed keys.
20 [PASS] Editor histogram with red M-value breach bars.

ARM64 STRUCT LAYOUT:
`PhysiologyDTO` size 32:
- offset 0 `float BloodOxygen`
- offset 4 `float TissueNitrogen`
- offset 8 `float CoreTemperature`
- offset 12 `uint ActiveTraumaMask`
- offset 16 `float HeartRate`
- offset 20 `float Adrenaline`
- offset 24 `uint _pad0`
- offset 28 `uint _pad1`

`DecompressionStateDTO` size 80:
- offset 0 `fixed float TissueTensions[16]` = 64 bytes
- offset 64 `float AmbientPressure`
- offset 68 `float AscentRate`
- offset 72 `ulong _pad0`

ZERO-GC CHECK:
- Burst jobs contain no LINQ, no `foreach`, no boxing, no managed strings, no `new NativeArray`.
- Runtime `Tick()` uses cached services and vault handles; CSV byte buffer is cold-allocated in `Awake`.
- Editor allocations are editor-only and not in player runtime.

AUP CHECK:
- `WriteEnvironmentSeed()` computes player Y from AUP grid/local values as double.
- Depth is clamped and cast to float before scheduling Burst jobs.
- No absolute AUP is cast directly to float inside physiology math.

DEAR LIE CHECK:
- Nitrogen narcosis is faked as `NarcosisSeverity`.
- Decompression sickness is faked as M-value breach mask and scalar risk.
- No organs, blood vessels, fake monster spawns, or per-trauma polymorphic objects.

DEPENDENCY CHECK:
- Runtime references Core, Core.Contracts, Core.Memory, Burst, Collections, Jobs, Mathematics only.
- No sibling domain runtime dependency was added.
- Heartbeat uses local typed `SignalBus<CardiacPulseSignal>`, not UnityEvents or string events.

H-PHI CHECK:
- Persistent arrays are leased from GlobalDataVault via `VaultBufferHandle<T>`.
- Runtime does not own private persistent `NativeArray` fields.
- Signal queue is owned by the global typed SignalBus lane, not by the physiology runtime.

BLACKBOX CHECK:
- Active: `ShinobuPhysiologyTelemetryRing` is a 300-entry vault ring.
- Fatal O2 or invalid math dumps binary autopsy data to `Docs/AgentLogs/Dump_AUTOPSY_REPORT.bin`.

COMPILE GUARD:
- `dotnet build Hecton8.Core.csproj --no-restore` was attempted once and is blocked by non-physiology errors in ecosystem/global telemetry/drone/audio domains.
- Targeted Unity Bee csc for `Hecton8.Physiology` succeeded.
- Targeted Unity Bee csc for `Hecton8.Physiology.Editor` succeeded after adding `Hecton8.Core.Contracts`.
</SELF_AUDIT>

Files changed:
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef`
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs`
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs`
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs`
- `Assets/_Project/Scripts/Physiology/Editor/Hecton8.Physiology.Editor.asmdef`
- `Assets/_Project/Scripts/Physiology/Editor/MetabolicControlCenterWindow.cs`
- `Docs/Tasks/Status_SHINOBU_21.md`
- `Docs/AgentLogs/Rationale_SHINOBU_21.md`
- `Docs/AgentLogs/LOG_SHINOBU_21.md`
