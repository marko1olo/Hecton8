# SHINOBU_248 Shockwave NaN Route Card

Date: 2026-05-21
Owner: SHINOBU_248 / SHOCKWAVE_NAN_AUDITOR_AND_LINK
Evidence: STATIC_SOURCE only. Unity import, Burst Inspector, Play Mode, profiler, GCMonitor, shader render, and player-build proof remain pending.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Authority

One fact: explosive shockwave force and cavitation presentation scalars.
One owner: `AbyssalCavitationRuntime` under `SystemID.VehiclesPhysics`. The prior `SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD` is historical for the original buffer range and is superseded for the live NaN/cavitation route delta by this card.
One route: GlobalDataVault DTO rows for shockwave/input/force/visual/telemetry, then existing `PhysicsApplySystem` drain and typed `SignalBus` broadcasts.
Required proof route before GREEN: 300-entry `ShockwaveTelemetryEntry` ring, staged `Docs/AgentLogs/Dump_SHINOBU_248.bin` black-box dump path, and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`; static source visibility alone is not runtime proof.

| Field | Value |
|---|---|
| Route ID | `SHINOBU_248_SHOCKWAVE_NAN` |
| Owner | `SHINOBU_248 / SHOCKWAVE_NAN_AUDITOR_AND_LINK` with runtime owner `AbyssalCavitationRuntime` under `SystemID.VehiclesPhysics`. |
| Instrument | `GlobalDataVault` DTO rows for shockwave/input/force/visual/telemetry, existing `PhysicsApplySystem` force-packet drain, and typed `SignalBus` broadcasts. |
| Producer phase | `AbyssalCavitationRuntime.ScheduleSimulation(inputDependency)` after shockwave event ingestion. |
| Consumer phase | `PhysicsApplySystem.DrainCavitationForcePackets` after the job fence; shader consumers read visual sphere DTO/scalar output. |
| Cadence/capacity | Simulation-tick scheduled batch; capacity bounded by the Vault buffers listed below. |
| Overflow/failure | Saturation, non-finite math, or NaN guard hits increment counters, clamp through the math guard, and must be recorded into telemetry before GREEN. |
| Shutdown/disposal | Owning runtime/vault releases or clears buffers; this route card does not authorize private persistent native ownership. |
| Proof required before GREEN | Fresh Unity import, clean Console, Play Mode, profiler/GCMonitor, player build, 300-frame telemetry fault-path artifact, and fresh `Tools/Division_By_Zero_Scanner.py` output. |
| Review disposition | PENDING VERIFICATION / STATIC_SOURCE only under R50. |

## Vault Buffers

- `71560` `ShockwaveEvents`
- `71561` `ShockwaveCounters`
- `71562` `EntitySnapshots`
- `71563` `ForcePackets`
- `71571` `ForceTransportPackets`
- `71564` `VisualSpheres`
- `71565` `TelemetryRing`
- `71566` `OrdnanceProfiles`
- `71567` `CsvScratch`
- `71568` `Tuning`
- `71569` `SdfDescriptor`
- `71570` `SdfVoxels`

All buffers are acquired from GlobalDataVault with `NativeArrayOptions.UninitializedMemory`. Runtime persists `VaultGenerationHandle<T>` descriptors only and opens method-local views through `IDataVault.TryResolveHandle(...)`. No private persistent native collection or pointer-bearing `VaultBufferHandle<T>` handle is introduced.

## Hot Route

`ScheduleSimulation(inputDependency)` schedules:

1. `PropagateShockwavesJob`
2. `CompactShockwavesJob`
3. `EvaluateSanitizedShockwaveJob`
4. `UpdateCavityShaderParamsJob`
5. `RecordShockwaveTelemetryJob`

The scheduled handle is returned to callers and registered with H8Memory. Hot simulation entry points fail closed through `IsRuntimeReady`; cold owner phases are responsible for Vault initialization. Main-thread force application is confined to `PhysicsApplySystem.DrainCavitationForcePackets` after the job fence is complete.

Public writer entry points for tuning and SDF state also fail closed through `IsRuntimeReady`: `TryApplyTuning`, `TryWriteSdfVolume`, and `TryClearSdfVolume` do not cold-bootstrap Vault ownership. Residual `EnsureInitialized` calls are cold owner lifecycle, cold CSV load, editor refresh/mutator, or editor/development mock harness surfaces.

`DrainCavitationForcePackets` resolves `GlobalPhysicsStateManager` once per drain, tries packet `RigidbodySlot` first, and falls back to folded entity hash only when the slot is stale or absent. `PhysicsApplySystem.EnsureRuntimeInstance()` remains an integrator debt because replacing it needs a force-sink injection API outside this SHINOBU_248 patch.

## Math Guard

The inverse-square denominator is:

`distanceSq = math.max(math.select(0f, rawDistanceSq, math.isfinite(rawDistanceSq)), tuning.EpsilonClampValue)`

Direction uses `delta * math.rsqrt(math.max(distanceSq, epsilon))` when the radial vector is valid. Exact-overlap epsilon-clamped cases use a deterministic hash-derived unit vector from entity hash, source hash, frame index, and SHINOBU_248 salt. The epsilon path increments `EpsilonClampCount`.

If accumulated force becomes non-finite despite the guards, the kernel clears both force vector and `forceSq` before the active-packet gate, so no active zero-force packet with stale NaN comparison state reaches the drain. Shockwave active checks also reject non-finite radius, max radius, peak pressure, expansion speed, and epicenter AUP before propagation/evaluation/visual upload.

## Dear Lie

The visual cavitation bubble is not a fluid simulation. CPU writes `CavitationVisualSphereDTO` rows to the shader buffer. `Hecton8_UberNoir` consumes sphere radius, pressure-derived intensity, age, quality, and phase to fake refraction/collapse.

## Black-Box Dump

Editor/development builds register a cold `Application.logMessageReceived` fault hook. Exceptions, errors, and asserts attempt one reentrant-guarded dump when no scheduled writer job is active. `TryDumpBlackBox` resolves the Unity project root through `Application.dataPath`, writes `Dump_SHINOBU_248.bin.tmp`, then replaces/moves the final artifact with delete+move fallback when `File.Replace` is unsupported or fails.

## Compile Wall

No new asmdef is introduced. No direct sibling runtime dependency is added by this route card. Current code uses existing Core/World AUP contracts already present in the monolithic project surface. If Cavitation is later split into a dedicated asmdef, AUP conversion must move behind a Contracts DTO or cached owner interface before split approval.

## Verification

Latest static scanner: `Tools/Division_By_Zero_Scanner.py`

- Errors: `0`
- Out-of-domain warnings: `68`
- Info: `62`
- Cavitation runtime errors: `0`
- Focused descriptor scan: no `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `.Resolve(...)`, `.ptr`, `ResolvePointer`, `GetElementAsRef`, or standalone `GenerationID` residue in Cavitation/editor scope.

Compile was not launched because the latest CPU gate sampled `99%`, above the project limit of `50%`; no `dotnet`/`csc` process was active at that sample.
