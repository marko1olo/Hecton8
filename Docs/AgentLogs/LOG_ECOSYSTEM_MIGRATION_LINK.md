# LOG_ECOSYSTEM_MIGRATION_LINK

## 2026-05-16 - Macro Swarm DB To Active Simulation Bridge

What was wrong:
- OSHINO/H8 macro swarms were abstract database payloads and macro travel records only. Loaded sectors had no bridge that claimed active fish slots from the ambient SOA.
- Legacy-style scene spawning was forbidden. `BufferID.EntityAUPs` was also not a safe target because the current project owns that lane for loot/entity AUP data, not active ecology boids.
- Chunk unload risked losing active visual biomass because no path packed hydrated fish back into a macro swarm.
- Capacity overflow had multiple possible entry points: DB import cap, hydration scratch cap, dehydration scratch cap, active macro cap, and active biota slot cap.

What was done:
- Extended `IEcosystemDirectorService` with vault import, hydration claim, and dehydration repack contracts.
- Extended `IAmbientBiotaService` with active macro hydration and macro-hydrated biota packing contracts.
- Added fixed 64-byte `EntitySpawnSignal` and registered/published it through `GlobalSignals`.
- Imported `MacroSwarm` records from `GlobalDataVault` macro database payload handles using fixed-stride native reads, then sanitized biomass, speed, AUP, hash, and genome fields.
- Routed sector hydration into `AmbientBiotaMacroHydrationJob`, which scans fixed SOA state lanes and claims only inactive slots.
- Converted macro sector authority into runtime AUP offsets through deterministic hash offsets, with non-finite position rejection before any SOA write.
- Added low-tier border ring hydration and high-tier SDF-gated cave emergence flags.
- Added stress culling: `SystemStress01 > 0.7` hydrates 50 percent of visual biomass while abstract macro biomass remains authoritative.
- Added unload seam: `SectorDehydratedSignal` packs macro-hydrated active ambient biota back into one `MacroSwarm` before legacy biomass fallback.
- Added capacity overflow blackbox pushes and changed macro-swarm blackbox dump target to `Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin`.

Cinematic cheats used:
- Low tier: instant border-ring fish spawn with billboard flag. No cave math, no per-fish SDF, no prefab path.
- Middle tier: deterministic radius fill from macro swarm biomass into ambient SOA slots.
- High/Ultra: one published SDF sample at hydration center gates cave emergence. The fish still use deterministic inward/deep offsets and `FlagSdfEmergence`; downstream VFX can sell the cave swim-out without ecology sampling every fish.
- Stress adaptation: visual fish count halves under high system stress; macro biomass remains abstract and recoverable.

Exact microseconds saved / estimated:
- Prefab path rejected: structural savings versus Instantiate/Destroy; expected multi-ms spike avoided for 64 fish, not measured in Unity profiler.
- Vault native import: estimated 18 us for 64 fixed-stride records.
- Hydration job: estimated 42 us for 64 visual boids at normal stress.
- Inactive-slot SOA claim: estimated 31 us for 64 claims at 2048 capacity.
- Low-tier border offsets: estimated 7 us for 64 offsets.
- High-tier SDF gate: one center sample, estimated 3 us, replacing 64+ per-fish SDF samples.
- Stress cull at >0.7: estimated 21 us saved and 32 visual slots avoided for a 64-fish swarm.
- Blackbox push: estimated 4 us excluding rare file dump.

Validation:
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` executed.
- Final result: blocked by unrelated compile wall. Current errors are in XR refresh rate, item acquisition signal, submarine structural grid, bioluminescence VFX, vault probe diagnostics, and visor fluid distortion files.
- No compiler errors were reported in the edited ecology/global-signal files before the external wall stopped validation.

## 2026-05-16 - H-Phi Multiplatform Inquisition Pass

What was wrong:
- Macro hydration counters and macro scratch buffers still had local native ownership.
- Macro DTOs were not all explicit `Pack=1` layouts.
- `EntitySpawnSignal` lacked finite-payload sanitization.
- Hydration/dehydration service calls used `Schedule().Complete()` for tiny bounded jobs.

What was done:
- Added vault BufferID lanes for macro swarms, arrivals, counters, blackbox, mutation scalars, hydration scratch, dehydration scratch, and biota macro counters.
- Replaced local macro scratch NativeLists with vault NativeArray lanes and explicit count fields.
- Converted `MacroSwarm`, `MacroSwarmArrival`, `MacroSwarmTelemetryEntry`, and `EntitySpawnSignal` to explicit `Pack=1` layouts.
- Added `EntitySpawnSignal` guard code and sanitizer.
- Replaced `Schedule().Complete()` in ambient macro hydration/dehydration with `Run()`.
- Added `FlagHighTierOverkill` so high-end presentation can consume the saved CPU through downstream visual lanes without direct VFX coupling.

Cinematic cheats used:
- Toaster: border ring, no per-fish SDF, no local allocation, no prefab.
- God-mode: same macro signal path carries SDF emergence plus high-tier overkill flag for downstream cave particles/material response.

Exact microseconds saved / estimated:
- Removed tiny job schedule/fence overhead: estimated 5-15 us per macro hydrate/dehydrate call, measured proof absent.
- Vault scratch arrays replace NativeList metadata mutation: estimated <5 us steady-state, primary gain is ownership hygiene.
- High-tier overkill flag cost: 0-1 us; visual cost is deferred to downstream visual owners.

Validation:
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` executed again.
- Latest result: blocked by unrelated `SubmarineFluidDynamics` duplicate handle fields at lines 658-679.
- No compiler errors were reported in edited AI/ecology/global-signal files.

## 2026-05-16 - Ecology Vault/ABI Titanium Pass

What was wrong:
- `EcosystemDirector` still had legacy persistent `NativeArray` ownership outside the macro bridge: population front/back buffers, biomass buffers, blackbox rings, headless fauna SOA, apex overlap scratch, spawn-gate scratch, and flora predator AUP upload staging.
- Several ecology structs were still sequential `Pack=4`, which is unacceptable for ARM64/Quest ABI assumptions.
- Whale-fall acoustics still used `PhysicsEventBus.NotifyAcousticImpulse`, a legacy bus dependency inside ecology.
- A fresh scan found `Schedule().Complete()` had reappeared in ambient macro hydration/dehydration.

What was done:
- Added `BufferID` lanes 288-320 for ecology population, biomass, headless fauna, apex scratch, and flora predator AUP upload buffers.
- Replaced local persistent `NativeArray` allocations in `EcosystemDirector.AllocateRuntimeState()` with `GlobalDataVault.GetBuffer<T>(..., SystemID.AIEcology, ...)`.
- Removed dispose/sentinel ownership for the vault-owned array aliases. The only local native ownership left in this director is lookup hash maps and save-time `NativeList` staging, because the current vault API is `NativeArray<T>` only and save serialization depends on list length slices.
- Converted `EcosystemSectorSaveRecord`, `EcosystemBiomassSaveRun`, `FaunaMutationTelemetryEntry`, `SectorPopulationState`, `BiomassImpactEvent`, `BiomassTelemetryEntry`, `ApexTerritorySample`, and `ApexTerritoryOverlapResult` to explicit `Pack=1` layouts.
- Replaced whale-fall `PhysicsEventBus` emission with typed `AcousticPingSignal` on `GlobalSignals`.
- Replaced ambient macro hydration/dehydration `Schedule().Complete()` calls with direct `Run()` again.

Cinematic cheats used:
- Toaster: no prefab spawning, no per-fish SDF, no local persistent NativeArray allocation, no physics impulse bus for whale-fall ecology audio.
- High/Ultra: `EntitySpawnSignal.FlagHighTierOverkill`, SDF emergence flags, and typed leviathan acoustic pings let downstream presentation spend GPU/CPU on cave exits, silt, salt/visor materials, and richer audio without ecology coupling to VFX/audio implementations.

Exact microseconds saved / estimated:
- Vault eviction: <5 us steady-state; primary win is allocation ownership hygiene and lower teardown/sentinel risk.
- ABI explicit layouts: 0 us runtime; prevents platform-specific padding failure.
- EventBus purge to typed acoustic lane: estimated 1-4 us per active whale-fall slow tick by avoiding listener-bucket dispatch from ecology.
- `Run()` over `Schedule().Complete()` for bounded bridge jobs: estimated 5-15 us per macro hydrate/dehydrate call.

Validation:
- Static scan found no `Schedule().Complete`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `Instantiate`, `new GameObject`, or `StartCoroutine` in the edited ecology bridge files.
- Static scan found no `StructLayout(LayoutKind.Sequential)` or `Pack = 4` in `EcosystemDirector`, `AmbientBiotaDirector`, or `MacroSwarm`.
- Static scan found no local `new NativeArray`, `H8Memory.Allocate`, or NativeArray sentinel ownership left in `EcosystemDirector` / `AmbientBiotaDirector`.
- `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` executed.
- Latest result: blocked by unrelated compile wall in `RepairTool`, `HectonUnderwaterVisuals`, and `World/SargassumMicroFaunaBoids`. No compiler errors were reported in the edited ecology/global-signal files.

## 2026-05-16 - Save Staging Eviction And Build Green

What was wrong:
- Save snapshot staging was still private native storage after the larger ecology vault eviction pass.
- `EcosystemPopulationBalancer` still used sequential Pack=1 DTOs, which left ABI offsets implicit for ARM64/Quest.
- Previous validation records were stale after concurrent dependency fixes removed the external compile wall.

What was done:
- Moved ecosystem save snapshot sectors and biomass runs to `GlobalDataVault` arrays with explicit record counts and slice returns.
- Converted `EcosystemPopulationCoefficient`, `EcosystemPopulationSectorState`, `EcosystemPopulationCullEvent`, `EcosystemPopulationFreeSlot`, and `EcosystemPopulationTelemetryEntry` to explicit Pack=1 layouts.
- Re-ran static scans for hot-path stalls, standard Unity updates, managed formatting/spawn calls, local native array/list allocation, sequential/Pack=4 structs, and legacy event-bus usage in the edited ecology bridge files.
- Re-ran `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false`.

Cinematic cheats used:
- Toaster: fixed-count vault staging, no save-list resize, no prefab path, no per-fish SDF, stress-halved visual hydration.
- Middle: deterministic SOA hydration and dehydration with abstract biomass continuity.
- High/Ultra: SDF emergence and high-tier overkill signal flags remain available for downstream silt, salt/visor material response, cave exits, and richer ambient presentation without ecology taking direct VFX ownership.

Exact microseconds saved / estimated:
- Save staging vault arrays: estimated <5 us steady-state and avoids resize-driven save hitches near streaming/I/O pressure.
- Explicit ABI DTOs: 0 us runtime; prevents platform padding failure.
- Bounded `Run()` bridge jobs over schedule/fence: estimated 5-15 us saved per macro hydrate/dehydrate call.
- Stress cull remains the largest frame-time lever: estimated 21 us and 32 visual slots saved for a 64-fish swarm when `SystemStress01 > 0.7`.

Validation:
- Build: `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` succeeded in 67.48 s with 0 warnings and 0 errors.
- Static scan found no `Schedule().Complete`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `Instantiate`, `new GameObject`, or `StartCoroutine` in the edited ecology bridge files.
- Static scan found no `StructLayout(LayoutKind.Sequential)` or `Pack = 4` in `EcosystemDirector`, `AmbientBiotaDirector`, `EcosystemPopulationBalancer`, or `MacroSwarm`.
- Static scan found no local `new NativeArray`, `new NativeList`, `H8Memory.Allocate`, or native-array/list sentinel ownership in `EcosystemDirector`, `AmbientBiotaDirector`, or `EcosystemPopulationBalancer`.
- Legacy signal scan found no active `EventBus`, managed delegate, `Action<>`, or `Func<>` usage in the edited bridge files; the only hit was a documentation comment containing the word "event".
- Unity PlayMode, Quest/Android, Metal/Mac, and Steam Deck runtime passes were not executed in this shell.

## 2026-05-16 - Final Revalidation Dependency Wall

What was wrong:
- The filesystem produced an intermediate clean build after ecology ABI polish, but concurrent non-ecology edits reopened the compile wall during final validation.
- The current blocker is `Assets/_Project/Scripts/LaserCutter.cs`: `LaserCutterEvents` references `_pendingEvents`, `_nextFrameEvents`, `_nextFrameEventCount`, and `_isDispatching` after those fields were removed by another gameplay-tool refactor.

What was done:
- Re-ran validation repeatedly after each wall changed.
- Confirmed the earlier core bridge `BitConverter.SingleToUInt32Bits` error disappeared.
- Confirmed the `SpatialAudioManager` missing helper wall disappeared.
- Stopped at the `LaserCutterEvents` field-removal wall because it is outside AI/ecology and restoring gameplay-tool event queue ownership from this agent would violate the domain boundary after repeated dependency strikes.
- Updated `Status_ECOSYSTEM_MIGRATION_LINK.md` and `Rationale_ECOSYSTEM_MIGRATION_LINK.md` to the latest state instead of preserving the stale intermediate build-green claim.

Cinematic cheats used:
- No new runtime cheat added in this final validation pass.
- Existing ecology path remains: low-tier border fake, stress-halved visual biomass, high-tier SDF emergence flags, and high-tier overkill signal flags.

Exact microseconds saved / estimated:
- No additional ecology runtime savings in this dependency-wall pass.
- Latest failed build wall time: 65.24 s.
- Prior intermediate clean build wall time: 67.48 s.

Validation:
- Latest command: `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false`.
- Latest result: failed with 45 CS0103 errors in `Assets/_Project/Scripts/LaserCutter.cs`, all missing `LaserCutterEvents` queue/state fields.
- Edited ecology bridge files still have clean static scans for `Schedule().Complete`, Unity `Update` methods, `string.Format`, prefab spawning, local native array/list allocation, sequential/Pack=4 ABI structs, and active legacy EventBus/delegate usage.
