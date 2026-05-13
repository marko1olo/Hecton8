# RTG_DECAY_SIMULATOR Status

Agent: THERMAL_ENGINEER
Domain: Radioisotope Thermals / Power-Thermal Systems
Task Count: 19
Status: IMPLEMENTED / UNITY VALIDATED / PENDING GLOBAL DOTNET BUILD - PROJECT DEPENDENCY WALL

## Mandates Read
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt

## Loop 1 - Tasks 1-5
- [x] 1. SINGLETON ERADICATION: Purge `PowerGeneratorManager.Instance` | Justification: `rg` found no RTG singleton target; new runtime uses no `Instance` manager, only component registration and GlobalRegistry services. Rejected alternative: classic singleton. Estimate: 6 us cold scan impact.
- [x] 2. SIGNAL MIGRATION: N/A, SOA state read by Logistics Grid | Justification: `IPowerComponent.PowerRating` now reads SOA-backed current output; HUD/logistics can query by contract without event spam. Rejected alternative: single-use event for wattage polling. Estimate: 2 us per logistics read.
- [x] 3. ASMDEF ISOLATION: `Hecton8.Power.Generators` -> Contracts | Justification: created `Hecton8.Power.Generators` and `Hecton8.Power.Generators.Contracts` asmdefs. Rejected alternative: dumping RTG into Core assembly. Estimate: 0 us runtime.
- [x] 4. DEAD CODE HUNT: Eradicate `Update()` methods inside `RTG_Item.cs` | Justification: no `RTG_Item.cs` exists; new RTG contains no `Update`, `FixedUpdate`, or `LateUpdate`. Rejected alternative: inventing legacy dependency. Estimate: 0 us.
- [x] 5. S.O.A. RTG DATA | Justification: defined persistent `NativeArray<float>` lanes for start times, half-lives, base output, current output, normalized output, flags, and telemetry. Rejected alternative: per-component float-only truth. Estimate: 3-12 us per 64 RTGs at cold cadence.

## Loop 2 - Tasks 6-10
- [x] 6. BURST DECAY JOB | Justification: `RtgDecayJob : IJobParallelFor` schedules from ColdTick leader lane and processes all slots. Rejected alternative: per-RTG MonoBehaviour Tick. Estimate: 8-20 us per 64 RTGs at 1 Hz.
- [x] 7. PADE APPROXIMATION | Justification: `RtgDecayMath.ResolvePadeExpNegative` uses guarded denominator and `math.rcp`; no `math.exp` in generator code. Rejected alternative: `math.exp` in hot job. Estimate: 2-5 us saved per 64 RTGs.
- [x] 8. HEAT INJECTION | Justification: RTG publishes radiation sources and heat signals, plus a narrow `IThermodynamicsService.TryInjectTransientHeatSource` bridge into AbyssalThermalManager. Rejected alternative: physical thermal diffusion per RTG. Estimate: 4 us cold path.
- [x] 9. LOGISTICS COUPLING | Justification: power grid reads RTG wattage through `IPowerComponent.PowerRating`; no direct `FluidPipeGraphRuntime` dependency was introduced. Rejected alternative: concrete pipe runtime dependency. Estimate: 1 us query.
- [x] 10. UI READOUT | Justification: exposed `OutputNormalized01`, `CurrentOutputWatts`, and `IRtgDecayOutputReader.TryGetRtgCurrentOutput`. Rejected alternative: per-frame string HUD update. Estimate: 0 B GC.

## Loop 3 - Tasks 11-12, 18
- [x] 11. DEPLETION THRESHOLD | Justification: decay job sets dead flag below 5%; power goes to 0 while radiation stays active. Rejected alternative: removing radiation when power dies. Estimate: 1 us.
- [x] 12. REPROCESSING | Justification: added `IRadioisotopeThermalReprocessable` plus `TryReprocessForFabricator` hook yielding depleted isotope hash only when dead. Rejected alternative: direct Fabricator edit during multi-agent churn. Estimate: 0 us until queried.
- [x] 18. SAVE SYSTEM SYNC | Justification: `SaveData` v70 persists fixed RTG source ids, start times, and flags through `SaveBinaryPayloadCodec`. Rejected alternative: runtime-only decay reset on load. Estimate: save-only.

## Loop 4 - Tasks 13-17, 19
- [x] 13. AUP SHIFT SAFETY | Justification: decay uses `SystemDispatcher.CurrentUnscaledTimeSeconds`; AUP conversion only happens for spatial radiation/heat signals. Rejected alternative: sector time math. Estimate: 0 us.
- [x] 14. MATH LOD | Justification: Low/Unknown/MX350 uses FrostTick with a 10-second gate; Mid+ uses 1 Hz ColdTick. Rejected alternative: uniform 1 Hz on toaster. Estimate: 90% low-tier job dispatch reduction.
- [x] 15. ZERO-GC | Justification: hot/cadence path uses persistent NativeArrays, static instance slots, and no managed collections in the tick/job path. Rejected alternative: Lists/strings in tick. Estimate: 0 B target.
- [x] 16. BLACKBOX DUMP | Justification: implemented 300-entry NativeArray ring and GlobalTelemetryBus markers for `ActiveRtgs` and `AverageRtgHealth`; NaN/capacity faults dump `Dump_RTG_DECAY_SIMULATOR.bin`. Rejected alternative: Debug.Log-only postmortem. Estimate: 64 B * 300 native upper bound.
- [x] 17. EVENT BUS | Justification: emits `HUDNotificationSignal` once when an RTG drops below 20%. Rejected alternative: UI singleton call. Estimate: O(1) queue push.
- [x] 19. OMEGA COMPILE CHECK | Justification: Pade denominator is clamped before `math.rcp`; edit-mode tests cover zero, large, and negative inputs. Rejected alternative: blind denominator use. Estimate: 0 us after guard.

## Compile Checkpoints
- [!] Checkpoint A after tasks 1-5: BLOCKED BY DEPENDENCY - Unity MCP session unavailable; `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /nodeReuse:false` fails on pre-existing assembly reference holes before RTG-specific validation.
- [!] Checkpoint B after tasks 6-10: BLOCKED BY DEPENDENCY - same Unity session/build dependency wall.
- [!] Checkpoint C after tasks 11-19: UNITY SCRIPT VALIDATION PASSED / DOTNET BLOCKED - latest `dotnet build` run on 2026-05-13 returned 113 unrelated missing namespace/type errors before project-wide proof.
- [!] Omega polish: UNITY VALIDATED / PENDING DOTNET GLOBAL VERIFICATION - static Omega audit completed, touched C# scripts validate clean, focused RTG EditMode tests pass, repo build remains red on unrelated dependencies.

## Omega Polish Evidence
- [x] Prompt re-read from `CURRENT_BATCH.md` after core task completion.
- [x] Polish mandate read only after Tasks 1-19 were implemented.
- [x] Scoped scan found no `math.exp`, `Update()`, `FixedUpdate()`, or `LateUpdate()` in `Assets/_Project/Scripts/Power/Generators`.
- [x] Scoped scan found no `foreach`, `string.Format`, `String.Format`, `.ToString(`, string interpolation marker, `math.sqrt`, or `math.normalize` in RTG runtime/test scope.
- [x] Remaining floating divisions in RTG runtime were replaced with `math.rcp()` multiplication.
- [x] Burst job numeric flag literals were replaced with named bitmask constants.
- [x] ASMDEF JSON parsed successfully for generator, contracts, and edit-mode test assemblies.
- [x] Unity MCP `validate_script` passed with zero diagnostics for `RadioisotopeThermalGenerator.cs`, RTG contracts, RTG tests, `SaveData.cs`, `SaveBinaryPayloadCodec.cs`, `AbyssalThermalManager.cs`, and `GlobalRegistryContracts.cs`.
- [x] Focused EditMode test run `Hecton8.Tests.Editor.RtgDecayMathTests` passed 5/5 tests in 0.3635108 seconds.
- [!] `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /nodeReuse:false` blocked by existing project-wide missing references: fluids, scheduling, memory layout, audio propagation, CCD, radar/resource read models, tether signal, acoustic types.

## Loop 5 - AAA Hardening Pass
- [x] Read-only RTG output/telemetry queries no longer allocate native buffers when called before runtime registration. Justification: query paths now fail closed until SOA exists. Rejected alternative: cold allocation inside UI/logistics polling. Estimate: avoids one persistent 128-slot native allocation from accidental read.
- [x] RTG save writing is leader-owned and writes all active slots in one pass. Justification: prevents duplicate/stale RTG records while keeping every component registered for load restore. Rejected alternative: each RTG appending independently. Estimate: 1 fixed 128-slot pass per save.
- [x] Loaded/registered RTGs resolve a local decay snapshot immediately. Justification: avoids one-cadence false full-power read after load. Rejected alternative: wait until next ColdTick/FrostTick. Estimate: one scalar Pade evaluation per RTG on load/register.
- [x] Pade decay upgraded to range-reduced eighth-power reciprocal. Justification: half-life checkpoint now tests near 0.5 without using `math.exp`. Rejected alternative: LUT/table lookup or transcendental exact exp. Estimate: a few multiplies per RTG, still cold cadence.
- [x] Thermodynamics bridge no longer double-publishes heat when injection succeeds; fallback signal only publishes when the bridge is unavailable. Justification: one heat event per RTG cadence. Rejected alternative: duplicate GlobalSignals traffic. Estimate: one avoided signal push per active RTG per cadence.
- [x] Blackbox RTG entries now include `AverageHealth01`; dump format bumped to v2. Justification: crash dump now carries both mandated `ActiveRtgs` and `AverageRtgHealth`. Rejected alternative: telemetry-only average with incomplete dump. Estimate: +4 bytes per telemetry entry.
