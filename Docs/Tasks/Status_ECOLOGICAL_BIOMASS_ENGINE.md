# ECOLOGICAL_BIOMASS_ENGINE Status

Agent: APEX_DIRECTOR
Domain: ECHELON 3 FLORA, FAUNA & BIOTA
Prompt: Lotka-Volterra Predator/Prey Pacing
Status: PENDING VERIFICATION

## Mandates Read Before Coding

- AI_Director_Encounter_Manager.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- MATH_Deterministic_RNG_SlotMachine.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

## State Machine

- [x] Task 1 - SINGLETON ERADICATION: extended existing `EcosystemDirector`/`IEcosystemDirectorService`; no new singleton | Justification: GlobalRegistry service pattern | Rejected: standalone manager singleton | Estimate: 0.0 us/frame
- [x] Task 2 - SIGNAL MIGRATION: `EntityDeathSignal` is mirrored into `SignalBus<EntityDeathSignal>` and drains into local biomass impact queue | Justification: non-destructive typed snapshot | Rejected: stealing legacy queue from EncounterDirector | Estimate: 1.5 us/frame when signals exist
- [!] Task 3 - ASMDEF ISOLATION: [BLOCKED BY DEPENDENCY] `Hecton8.AI.Ecology` cannot be isolated without breaking existing `Hecton8.Core -> Hecton8.Ecosystem` concrete references | Justification: kept new bridge in `IEcosystemDirectorService` contract | Rejected: adding an asmdef that creates an immediate compile cycle | Estimate: 0.0 us/frame
- [x] Task 4 - DEAD CODE HUNT: fauna spawn selection now multiplies by local biomass; encounter token costs query biomass through service contract | Justification: contract read before job schedule | Rejected: hardcoded director weights | Estimate: 2.0 us/cold tick
- [x] Task 5 - SECTOR BIOMASS GRID: added `NativeArray<float>` prey/predator front/back buffers on absolute 50 m macro-cells | Justification: sparse SoA, Cartography-sized cells | Rejected: full-world dense array/GameObjects | Estimate: 0.0 us/frame, ~18 us cold seed burst
- [x] Task 6 - FROST TICK MATH: `BiomassLotkaVolterraJob` runs every 5 s with requested equations | Justification: Burst `IJobParallelFor`, clamped Euler | Rejected: per-frame MonoBehaviour simulation | Estimate: 35-70 us per 512-cell FrostTick
- [x] Task 7 - CAPACITY CLAMP: cell carrying capacity derives from biome/food density and clamps 0.1..1.0 | Justification: biome ID reuse from sector table | Rejected: unbounded LV integration | Estimate: 3 us/cell seed only
- [x] Task 8 - SPAWN CREDIT MODIFIER: predator scarcity doubles Leviathan cost; prey overgrowth halves Swarm cost | Justification: `EncounterThreatAuthoringSnapshot` copy modified before Burst job | Rejected: concrete ecosystem dependency inside job | Estimate: 2 us/cold tick
- [x] Task 9 - DEPLETION PERSISTENCE: biomass saved as sbyte 0-100 RLE records packed into existing `EcosystemSectorSaveRecord` section used by `SaveBinaryStorage` | Justification: no storage signature churn | Rejected: new save section touching broad pipeline | Estimate: save-only cold path
- [x] Task 10 - MIGRATION DIFFUSION: adjacent macro-cell diffusion kernel in Burst job | Justification: four-neighbor NativeHashMap lookups | Rejected: agent migration objects | Estimate: 30 us/512 cells when enabled
- [x] Task 11 - VISUAL FLORA COUPLING: low prey biomass publishes `_HectonBiomassOvergrowth` global scalar | Justification: shader global decouples flora presentation | Rejected: direct `FloraInteractionManager` call | Estimate: 0.4 us/cold tick
- [x] Task 12 - PLAYER FISHING IMPACT: `ItemAcquiredSignal` fish hashes deduct prey biomass by `1.0f * quantity` | Justification: typed signal snapshot, local macro-cell impact | Rejected: inventory polling | Estimate: 1 us per fish signal
- [x] Task 13 - AUP SHIFT SAFETY: macro-cell keys use `AbsoluteUniversePosition.ToAbsoluteDouble3`; no grid data shifts | Justification: absolute coordinates, shifted read index only | Rejected: rebasing biomass arrays | Estimate: 0.0 us during shift
- [x] Task 14 - ZERO-GC: hot paths use NativeArrays/NativeHashMap/fixed queues; only NaN dump path touches IO | Justification: no managed collections in FrostTick/signal drains | Rejected: LINQ/List allocations | Estimate: 0 B hot path
- [x] Task 15 - MATH LOD: Low scalability tier disables diffusion | Justification: `GlobalRegistry.ScalabilityTierProfileByte == 0` gate | Rejected: balanced mid-tier compromise | Estimate: saves ~30 us/512 cells on MX350
- [x] Task 16 - OVERHUNTING HUD: active Scanner + local depleted biomass publishes `HUDNotificationSignal` warning hash | Justification: existing HUD signal lane | Rejected: UI object spawn | Estimate: 1 us/frame while scanner active
- [x] Task 17 - BLACKBOX DUMP: global biomass sums push to telemetry and 300-entry native blackbox; NaN dumps `Dump_ECOLOGICAL_BIOMASS_ENGINE.bin` | Justification: fixed circular NativeArray | Rejected: managed rolling logs | Estimate: 2 us/cold tick
- [x] Task 18 - EVENT BUS ALARM: predator biomass zero emits `ProgressionEventSignal` with `SectorCleared` hash once per cell | Justification: event bus alarm, latch byte prevents spam | Rejected: polling quest state | Estimate: 1 us/cell only on cold tick
- [!] Task 19 - OMEGA COMPILE CHECK: [BLOCKED BY DEPENDENCY] Unity MCP session unavailable; local `dotnet build Hecton8.Core.csproj` blocked by pre-existing missing assembly references, but filtered output shows no new `EcosystemDirector.cs`/`EncounterDirector.cs` errors | Justification: fail-fast evidence recorded | Rejected: claiming a green compile without Unity | Estimate: verification-only

## Compile Attempts

- Attempt 1: Unity MCP `validate_script` for edited files failed with `no_unity_session`.
- Attempt 2: `dotnet build .\Hecton8.Core.csproj --no-restore /v:minimal` failed before project compile due existing missing references: `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, acoustic path types, and related project-wide symbols.
- Attempt 3: filtered `dotnet build` output for edited files reported only pre-existing `GlobalSignals.cs` line 5/3362 and `GlobalRegistryContracts.cs` line 9/893 dependency errors; no `EcosystemDirector.cs` or `EncounterDirector.cs` diagnostics appeared.
- Attempt 4: `git diff --check` on edited files passed, line-ending warnings only.
- Attempt 5: `<POLISH_MANDATE>` extraction returned `NO_POLISH_MANDATE`; anti-bloat scan found no new TODO/HACK/managed hot-path collections in edited ecology paths.

## Iteration Notes

- Loop 0: prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`. Source scan found existing `World/EcosystemDirector`, `EncounterDirector`, `HectonDirectorAI`, `GlobalSignals`, and `IEcosystemDirectorService`.
- Loop 1: tasks 1-5 implemented. Read code after patch; fixed biomass service exposure and spawn-weight bridge.
- Loop 2: tasks 6-10 implemented. Re-read prompt using CLI with attribute-aware XML extraction; added LV job, capacity clamp, save RLE, diffusion, and encounter token modifiers.
- Loop 3: tasks 11-15 implemented. Re-read new ecology methods; added shader scalar, fish signal path, AUP macro keys, zero-GC native impact queue, and low-tier diffusion gate.
- Loop 4: tasks 16-18 implemented. Re-read telemetry/event methods; added scanner warning, telemetry blackbox, NaN dump, and SectorCleared latch.
- Loop 5: task 19 verification attempted. Unity MCP unavailable and project build blocked by unrelated dependency wall; recorded as blocked, not green.
- Loop 6: Polish phase attempted after all tasks checked/blocked. No `<POLISH_MANDATE>` tag exists in `CURRENT_BATCH.md`; ran anti-bloat scan manually.
