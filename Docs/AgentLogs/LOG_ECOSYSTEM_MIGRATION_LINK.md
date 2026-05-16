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

## 2026-05-16 - Current-Disk Green Revalidation

What was wrong:
- The previous ECOSYSTEM_MIGRATION_LINK status was stale after integration repaired the external `LaserCutterEvents` compile wall.
- Without a fresh build, the agent record would still claim dependency-blocked even though current disk could compile.

What was done:
- Re-read `Status_ECOSYSTEM_MIGRATION_LINK.md`, `Rationale_ECOSYSTEM_MIGRATION_LINK.md`, and the exact XML prompt from `CURRENT_BATCH.md`.
- Ran `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` on current disk.
- Re-ran focused ecology bridge scans for synchronous job fences, standard Unity update loops, managed formatting, prefab spawning, local native allocation calls, sequential/Pack=4 DTOs, and legacy EventBus/delegate usage.
- Updated status and rationale from dependency-blocked to build-green evidence while preserving the platform runtime gap.

Cinematic Cheats used:
- Toaster: vault-cached macro import, border-ring hydration, stress-halved visual biomass, no prefab path, no per-fish SDF reads.
- Middle: deterministic SOA visual hydration and dehydration back into `MacroSwarm`.
- High/Ultra: SDF emergence and `FlagHighTierOverkill` remain available for downstream volumetric silt, visor salt/material response, cave exits, and richer ambient presentation without direct ecology-to-VFX coupling.

Exact Microseconds saved / estimated:
- Latest desktop C# build validation time: 59,970,000 us.
- Low-tier border offsets remain estimated at 7 us for 64 offsets.
- Stress cull remains estimated at 21 us and 32 visual slots saved for a 64-fish swarm when `SystemStress01 > 0.7`.
- SDF gate remains one center sample, estimated at 3 us, replacing 64+ per-fish SDF samples.
- No new runtime microsecond claim was added in this pass.

Validation:
- Build: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed 00:00:59.97.
- Focused ecology scan found no `Schedule().Complete`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `Instantiate`, `new GameObject`, or `StartCoroutine` in `EcosystemDirector`, `AmbientBiotaDirector`, `EcosystemPopulationBalancer`, or `MacroSwarm`.
- Focused ecology scan found no `StructLayout(LayoutKind.Sequential)` or `Pack = 4` in those ecology bridge files.
- Focused ecology scan found no local `new NativeArray`, `new NativeList`, `H8Memory.Allocate`, or native-array/list sentinel ownership calls in those ecology bridge files.
- Focused legacy-signal scan found no active `EventBus`, `Action<>`, `Func<>`, or delegate usage; the only hit was the word "event" inside a comment.
- Unity PlayMode, profiler, GC monitor, Quest/Android player build, Metal/Mac player build, Steam Deck runtime pass, and IL2CPP strip build were not executed.

## 2026-05-16 - Macro Blackbox Heartbeat And Signal Dedup Pass

What was wrong:
- Macro-swarm blackbox evidence was event-driven. The mandate asks for the last 300 frames of high-level state, so event-only telemetry was insufficient.
- A duplicate `LaserCutterEventPayload` signal definition existed across gameplay/core in the current integration tail, creating ambiguous listener signatures and violating typed-lane uniqueness.
- After those issues were addressed, the current build wall moved to unrelated Construction/Fauna files.

What was done:
- Added `MacroSwarmBlackBoxFlagHeartbeat`.
- Added cached macro swarm biomass/hash state fields.
- Added `PushMacroSwarmHeartbeatBlackBox()` and called it from the existing `ILateFrameTickable` path after scheduled macro travel completion.
- Kept full `PushMacroSwarmBlackBox()` event writes as the validation/dump path; those still scan active swarms and dump `Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin` on invalid state.
- Hardened the canonical core `LaserCutterEventPayload` to explicit `Pack=1` field offsets after current disk removed the gameplay duplicate.

Cinematic Cheats used:
- Toaster: heartbeat is one cached ring write per frame, not a 256-swarm scan.
- Middle: full macro hydration/dehydration still records event snapshots.
- High/Ultra: SDF emergence and high-tier overkill flags remain available for downstream silt, visor salt/material response, cave exits, and richer ambient presentation without ecology taking VFX ownership.

Exact Microseconds saved / estimated:
- O(1) heartbeat write: estimated 1-2 us per frame.
- Rejected full per-frame 256-swarm telemetry scan: estimated 8-20 us avoided per frame on low-end CPU, measured proof absent.
- Latest failed build validation wall time: 65,970,000 us.

Validation:
- Static ecology scans found no `Schedule().Complete`, standard Unity update loop, prefab spawn, `string.Format`, local native allocation call, sequential/Pack=4 ecology DTO, or active legacy EventBus/delegate usage in the ecology bridge files.
- Duplicate laser-cutter signal scan found only the canonical core `LaserCutterEventPayload` struct and `LaserCutterEventType` enum; `LaserCutter.cs` now only references that typed lane.
- `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` failed with 17 errors outside ecology: `DroneFleetManager.cs` double3/float3 conversion errors and `PredatorCognitionDomain.cs` NativeArray/NativeParallelHashMap ownership/type mismatches plus missing `_speciesTuningById`.
- Unity PlayMode, profiler, GC monitor, Quest/Android player build, Metal/Mac player build, Steam Deck runtime pass, and IL2CPP strip build were not executed.

## 2026-05-16 - Vault-Handle Eviction And Physics Wall

What was wrong:
- `EcosystemDirector` still retained long-lived `NativeArray<T>` field aliases after earlier vault migration.
- The sector food heatmap bind path retained an external `NativeArray<byte>` alias instead of copying into an ecology-owned DataVault lane.
- Current validation no longer fails in Construction/Fauna; it now fails outside ecology in `PhysicsApplySystem.cs` with force-packet helper, queue, validation-buffer, and BufferID references missing.

What was done:
- Added a local `VaultNativeArray<T>` wrapper in `EcosystemDirector` that stores `IDataVault` plus `VaultBufferHandle<T>` and resolves live views at use sites.
- Converted the long-lived ecology arrays and `HeadlessEntitySoA` lanes from raw `NativeArray<T>` fields to vault-handle wrappers.
- Added `BufferID.EcosystemSectorFoodHeatmapR8` and copied sector food heatmaps into that vault lane instead of retaining an external alias.
- Re-ran static scans for private NativeArray owner fields, local NativeArray/List/Queue allocation calls, synchronous `Schedule().Complete`, Unity update loops, managed formatting/spawn paths, ABI pack drift, and legacy bus/delegate usage.
- Re-ran `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false`.

Cinematic Cheats used:
- Toaster: copied heatmap data stays vault-local, no MicroSD/runtime file read, no prefab path, no per-fish SDF, stress-halved visual hydration.
- Middle: deterministic SOA hydration/dehydration continues through vault-resolved buffers.
- High/Ultra: SDF emergence and high-tier overkill signal flags still buy downstream silt, salt/visor material response, cave exits, and richer ambient presentation without ecology owning VFX.

Exact Microseconds saved / estimated:
- Wrapper resolution cost is expected to be micro-scale and cold-path dominated; profiler proof was not executed.
- Avoided external heatmap alias and runtime I/O pressure; estimated benefit is hitch-risk removal rather than a measured frame-time number.
- Existing heartbeat estimate remains 1-2 us per frame; rejected full 256-swarm scan remains 8-20 us avoided per frame.
- Latest build-wall evidence: 31.47 s = 31,470,000 us. This is compile wall time, not runtime performance.

Validation:
- Build: failed with 67 errors in `Assets/_Project/Scripts/PhysicsApplySystem.cs`, outside AI/ecology.
- Focused ecology scan found no `Schedule().Complete`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `Instantiate`, `new GameObject`, or `StartCoroutine` in the ecology bridge files; only core signal diagnostics still use `Debug.LogWarning/Error`.
- Focused native ownership scan found no private `NativeArray<T>` fields and no local `new NativeArray`, `new NativeList`, `new NativeQueue`, `H8Memory.Allocate`, or native sentinel registration calls in the ecology bridge files.
- Remaining local native ownership: `_biomassIndexByKey` and `_sectorIndexByKey` NativeHashMap lookup indices in `EcosystemDirector`; documented because DataVault has no hash-map lane contract.
- ABI scan found no sequential or Pack=4/8 ecology DTOs in `EcosystemDirector`, `AmbientBiotaDirector`, ecosystem files, or `MacroSwarm`; broader `GlobalSignals.cs` still contains unrelated sequential Pack=1 signal structs outside this ecology pass.
- Legacy signal scan found no active `EventBus`, `Action<>`, `Func<>`, or delegate usage in the ecology bridge files; hits were comments/documentation terms only.
- Unity PlayMode, profiler, GC monitor, Quest/Android player build, Metal/Mac player build, Steam Deck runtime pass, and IL2CPP strip build were not executed.

## 2026-05-16 - Vault Index Sovereignty Closure

What was wrong:
- `EcosystemDirector` still owned two persistent `NativeHashMap<long,int>` lookup tables for sector and biomass key resolution.
- The previous status was blocked by external physics errors, but current source state could be rebuilt after restore.

What was done:
- Added explicit Pack=1 `EcosystemIndexEntry` records.
- Added `BufferID.EcosystemSectorIndexEntries` and `BufferID.EcosystemBiomassIndexEntries`.
- Replaced `_sectorIndexByKey` and `_biomassIndexByKey` with vault-backed open-addressed tables.
- Updated biomass diffusion, macro-swarm predator attraction, sector apex lookup, restore, clear, and slot creation paths to use bounded vault index probes.
- Removed native hash-map allocation, disposal, and sentinel registration from the ecology director.
- Ran restore, static scans, and final desktop C# build validation.

Cinematic Cheats used:
- Toaster: fixed 16-byte index entries, no native hash-map allocator ownership, no linear 512-cell diffusion scan, no prefab path, no per-fish SDF.
- Middle: deterministic SOA hydration/dehydration and biomass diffusion stay bounded by configured capacities.
- High/Ultra: SDF emergence and high-tier overkill signal flags remain available for downstream volumetric silt, visor salt/material response, cave exits, and richer ambient presentation without ecology owning VFX.

Exact Microseconds saved / estimated:
- Removed local native hash-map allocator ownership: init/teardown stall risk removed; steady-state savings unprofiled.
- Rejected linear biomass lookup: avoids up to 512 comparisons per neighbor probe at default capacity. Open-address table is expected O(1), unprofiled.
- Index memory cost: default 512 biomass slots -> 1024 entries * 16 bytes = 16,384 bytes; default 128 sector slots -> 256 entries * 16 bytes = 4,096 bytes.
- Final build validation: 75.17 s = 75,170,000 us. This is compile wall time, not runtime performance.

Validation:
- Restore: `dotnet restore Hecton8.Core.csproj /p:UseSharedCompilation=false` reported all projects up to date.
- Build: `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` succeeded with 0 warnings / 0 errors in 00:01:15.17.
- Native ownership scan found no `NativeHashMap`, private native owner field, local `new NativeArray/List/Queue/HashMap`, `H8Memory.Allocate`, or native sentinel collection registration in ecology bridge files.
- Anti-bloat scan found no `Schedule().Complete`, standard Unity update loop, `string.Format`, `Instantiate`, `new GameObject`, or `StartCoroutine` in ecology bridge files.
- ABI scan found no sequential or Pack=4/8 ecology DTOs in ecology bridge files.
- Legacy signal scan found no active `EventBus`, `Action<>`, `Func<>`, or delegate usage in ecology bridge files; hits were documentation/comment wording only.
- Unity PlayMode, profiler, GC monitor, Quest/Android player build, Metal/Mac player build, Steam Deck runtime pass, and IL2CPP strip build were not executed.
