# LOG_SHINOBU_272

## 2026-05-21 - Physiological Gas Toxicity Integration

What was wrong:
- Survival oxygen was still an oxygen-bar/resource model; gas physics did not own PPO2/PPN2/PPCO2.
- Haldane tissue tension used total ambient pressure as the tissue target, which cannot distinguish air from heliox.
- Health had no gas-toxicity bridge except legacy stress/toxicity concepts.
- Visual hypoxia was based on oxygen percentage-style fakes, not oxygen partial pressure.
- Black-box dump path did not match SHINOBU_272.

What was done:
- Added unmanaged `GasPhysiologyStateDTO` and `BreathingGasFractionsDTO`, both explicit 32-byte layouts with validation.
- Added Vault buffer IDs: `ShinobuBreathingGasFractions = 70214`, `ShinobuGasPhysiologyStates = 70239`.
- Added `GenerateMockBreathingGasJob`, `CalculatePartialPressuresJob`, `IntegrateBloodGasTensionsJob`, and `CalculateCnsToxicityJob`.
- Integrated PPO2/PPN2/PPCO2 into `ShinobuPhysiologyRuntime`, `HectonPlayerHealth`, telemetry, visual sync, dev overlay, editor tuner, CSV profile ingest, and respawn nitrogen baseline.
- Added latest `PhysiologyStateSignal` bridge and unmanaged `CombatDamageSignal` route for severe CNS/CO2/hypoxia.
- Added architecture note: `Docs/ARCHITECTURE/PHYSIOLOGICAL_GAS_TOXICITY_SHINOBU_272.md`.

Cinematic cheats used:
- Hypoxia tunnel is a scalar from PPO2, not simulated retinal/brain oxygen transport.
- Narcosis is a continuous N2 partial-pressure scalar; input owner can consume it as lag without physiology touching input.
- CO2 and CNS toxicity are scalar accumulators; no molecular gas simulation.

Exact microseconds saved/estimated:
- Mock breathing gas: ~1 us/entity, 0 GC.
- Dalton partial pressures: ~1 us/entity, 0 GC.
- Haldane integration: ~2 us/entity low quality, ~6 us/entity full 16 compartments.
- CNS/CO2/hypoxia toxicity: ~1 us/entity.
- Damage route: ~1 us only on toxic frames.
- Main-thread latest signal bridge: <1 us per completed physiology tick.

Verification:
- Static direct-dependency scan found no physiology calls to `HectonPlayerHealth` or `TakeDamage`.
- `git diff --check` reported only line-ending warnings.
- Build was not run: CPU preflight reported 100%, `csc.exe` count 0. Project rule forbids `dotnet build` above 50% CPU.

<SELF_AUDIT>
GasPhysiologyStateDTO = 32 bytes: O2=0, N2=4, CO2=8, CNS=12, Narcosis=16, StaminaDrain=20, Flags=24, pad=28.
BreathingGasFractionsDTO = 32 bytes: O2=0, N2=4, CO2=8, inert reserve=12, GasHash=16, Flags=20.
Telemetry ring = 300 * 64 bytes, dump path `Docs/AgentLogs/Dump_SHINOBU_272.bin`.
Hot path managed allocations: 0 by static scan; editor/dev UI and cold CSV IO are outside runtime simulation.
Vault locks cover vitals, decompression, tissues, coefficients, environment, scalars, gas states, breathing gas, export, telemetry, pulse, toxemia, pressure, combat, predator, medical.
</SELF_AUDIT>

## 2026-05-21 - Polish Loop After Subagent Audit

What was wrong:
- CSV hot-reload polling was still called from `Tick`, putting managed file IO/date checks on the runtime frame path.
- Environment seed and NativeArray resolves happened before Vault locks; tuning rows were read/written without lock coverage.
- Task 16 only displayed gas state; it did not directly tune CNS/hypoxia thresholds.
- Task 19 had no real `Physiology_OOP_Scanner` artifact.
- Health used local numeric gas source/status constants instead of named contract constants.

What was done:
- Moved CSV ingestion to cold Vault initialization only; `physiological_gas_profiles.csv` now also writes gas tuning thresholds.
- Added 64-byte `GasPhysiologyTuningDTO` in local Vault buffer `70215`; jobs consume it by value.
- Moved physiology job locks before environment write/resolves; added gas tuning and physiology tuning to the lock set.
- Removed public live `GetVitalsRef`; editor/test injectors and read accessors fail closed while a job is scheduled.
- Added UI Toolkit sliders for CNS rate, narcosis threshold, hypoxia/anoxia PPO2, and CO2 toxicity.
- Added `Physiology_OOP_Scanner` and generated `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_272.json`; shared report JSON contains `shinobu272PhysiologyOopScanner`.
- Moved stable gas ABI constants into `PhysiologyStateSignal`; removed SHINOBU_272 references to local `H8Memory` enum additions.

Cinematic cheats used:
- Hypoxia remains a single shader scalar, `_HypoxiaSignal`; no CPU post-process volume mutation.
- Gas toxicity is one packed shader vector in slot 11; GPU handles presentation curves.

Exact microseconds saved/estimated:
- Removed runtime CSV poll: saves one managed IO/stat check per second and eliminates worst-case storage latency from gameplay frames.
- Lock-before-resolve: no direct frame-time saving claimed; prevents race/debug stalls.
- Gas tuning row: 64 B copy per scheduled tick, below 0.1 us.
- Scanner/editor tooling: 0 us player-build runtime.

Verification:
- `Physiology_OOP_Scanner.StaticMirror` findings: 0.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parsed successfully with `ConvertFrom-Json`.
- Focused `git diff --check` reports line-ending warnings only.
- Build still not launched: CPU=100%, `csc.exe`=1.

<SELF_AUDIT rev="2">
STRUCTS: `GasPhysiologyStateDTO` 32 B offsets 0/4/8/12/16/20/24/28; `BreathingGasFractionsDTO` 32 B offsets 0/4/8/12/16/20/24/28; `GasPhysiologyTuningDTO` 64 B offsets 0..60 at 4-byte stride.
VAULT: local gas buffers `70214`, `70215`, `70239`; existing telemetry ring `70226`.
LOCKS: all scheduled read/write lanes lock before seed/resolve/schedule and unlock after dispatcher finalization.
HOT GC: no managed allocations in jobs; CSV/file IO is cold initialization only.
COMPILE GUARD: no direct Physiology reference to `HectonPlayerHealth`; CombatDamage lane initialization remains Core-owned.
REPORT: Task 01-20 PASS static; compile verification blocked by machine policy until CPU is under 50% and no `csc.exe` is running.
</SELF_AUDIT>

## 2026-05-21 - Polish Loop 9 Fence Audit

What was wrong:
- Four cold mock editor/test injectors (`InjectMockCombatDamage`, `InjectMockPredatorAggro`, `InjectMockToxemia`, `InjectMockMedicalItem`) still resolved and wrote Vault lanes without checking the active physiology job fence.
- This contradicted the claimed owner-phase fence even though normal gameplay ticks do not call those injectors.

What was done:
- Added `_jobScheduled` fail-closed guards to the four injectors.
- Re-read SHINOBU_272 XML prompt and the binary payload ledger after context compaction.
- Re-ran focused scans for DTO properties, direct health/depth-damage coupling, report JSON validity, and diff whitespace.

Cinematic cheats used:
- No new physical simulation was added. Hypoxia/CNS/CO2 still publish scalar shader data; editor injectors remain cold test controls only.

Exact microseconds saved/estimated:
- Normal physiology tick: 0 us change; injectors are not in the scheduled job chain.
- Cold injector call: one branch, sub-0.1 us; avoids race/debug stalls and stale Vault writes during play-mode tooling.

Verification:
- DTO property scan under Physiology: 0 hits.
- Direct Physiology scan for `UnityEngine.Random`, `TakeDamage`, hard depth damage, and `HectonPlayerHealth`: 0 runtime hits.
- Shared and SHINOBU_272 report JSON parse successfully.
- Focused `git diff --check` reports CRLF warnings only.
- Build not launched: CPU=100%, `csc.exe`=0, `dotnet`=1.
## 2026-05-21 - Loop 10 Rendering Boundary / Safety Proof Closure

What was wrong: `ShinobuPhysiologyRuntime` still projected physiology shader globals directly through `HectonShaderGlobalDataVaultBridge.PublishPhysiology*`, queue writers carried broad `NativeDisableContainerSafetyRestriction`, and `PhysiologyStateSignal` had implicit holes at offsets 18-19 and 54-55.

What was done: Physiology now emits unmanaged signal truth only. `GlobalShaderDispatcher` reads the current `PhysiologyStateSignal` snapshot/latest bridge and owns shader projection into decompression/gas toxicity slots and `_HypoxiaSignal`. Queue writer broad safety disables were removed; tissue slice mutation uses the narrower parallel-for restriction with an explicit slice/fence proof. `PhysiologyStateSignal` remains 64 bytes but now exposes gas CNS/CO2 severity bytes at offsets 18/19 and explicit padding at 54; layout validation checks the gas signal contract.

Cinematic Cheats used: Hypoxia/CNS/CO2 remain scalar shader fakes; no post-process volume mutation, UI prefab spawn, or CPU visual simulation.

Exact Microseconds saved: removes Physiology-owned shader bridge work from the Burst-adjacent completion path; estimated <1 us moved out of physiology, plus avoids unbounded compile-wall/authority cost. Rendering projection scans at most 64 signal rows, estimated <2 us on i3/MX350.

Verification: direct Physiology shader bridge scan 0 hits; `NativeDisableContainerSafetyRestriction` scan in SHINOBU jobs 0 hits; DTO property scan 0 hits; shared/sidecar physics report JSON parse with SHINOBU_272 section present; focused `git diff --check` reports CRLF warnings only. Build not launched: `CPU=100`, `csc.exe=0`, `dotnet=0`.
