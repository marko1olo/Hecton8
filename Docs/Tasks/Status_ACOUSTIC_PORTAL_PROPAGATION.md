# Status_ACOUSTIC_PORTAL_PROPAGATION

Agent: DSP_ACOUSTIC_LEAD  
Prompt: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="ACOUSTIC_PORTAL_PROPAGATION">`  
Domain: Echelon 8 DSP Acoustic Radar / Audio Propagation  
Task count: 19  
Status policy: PENDING VERIFICATION until Unity/Burst evidence exists.

## Setup
- [x] Prompt extracted | DOD: PowerShell raw-read regex captured only the full `ACOUSTIC_PORTAL_PROPAGATION` XML block from cover to cover | Alternative rejected: MCP/basic read because batch files can truncate or bleed neighboring prompts | Estimate: 900 us
- [x] Domain loaded | DOD: `Docs/Actual Domains of Project.txt` read; acoustic work mapped to Echelon 8 DSP Acoustic Radar / perception | Alternative rejected: editing outside assigned audio propagation boundary | Estimate: 600 us
- [x] Mandates selected | DOD: 8 task-relevant mandate files loaded before coding: acoustic occlusion, DSP SPSC, zero-GC, native jobs, blackbox telemetry, AUP, GlobalRegistry, cinematic fake-first | Alternative rejected: relying on AGENTS summary only | Estimate: 1500 us
- [x] Existing code inventory | DOD: read `SpatialAudioManager`, `IAudioService`, `AbsoluteUniversePosition`, `AcousticOcclusionUtility`, `VoxelDynamicNavGridRuntime`, `HabitatGraphManager`, `ConstructionManager`, and asmdefs | Alternative rejected: inventing APIs or direct dependencies on unknown systems | Estimate: 2600 us

## Primary Tasks
- [x] 1. SINGLETON ERADICATION | DOD: `rg` found no `AcousticManager.Instance`; implementation stays on `GlobalRegistry.Audio`/`IAudioService` | Alternative rejected: inventing a replacement singleton | Estimate: 500 us
- [x] 2. SIGNAL MIGRATION | DOD: added blittable `SoundEmissionSignal` with `AcousticAup` source and a prewarmed `NativeQueue<SoundEmissionSignal>` ingress | Alternative rejected: managed event classes/delegates in the audio path | Estimate: 1800 us
- [x] 3. ASMDEF ISOLATION | DOD: created `Hecton8.Audio.Propagation.asmdef` with pure Burst/contracts and referenced it from core/tests | Alternative rejected: moving `SpatialAudioManager` into a new asmdef because that creates contract cycles | Estimate: 2200 us
- [x] 4. DEAD CODE HUNT | DOD: straight-line SDF remains untouched; portal path only replaces source presentation when habitat/voxel graph route exists | Alternative rejected: deleting `AcousticOcclusionUtility` or globally bypassing SDF | Estimate: 1300 us
- [x] 5. HABITAT SOUND GRAPH | DOD: added read-only construction/habitat accessors and adapted `EdgeOffsets`, `EdgeDestinations`, `EdgeFlags`, `RoomVolumes` into acoustic nodes/edges | Alternative rejected: mutating habitat flood data or duplicating the construction graph | Estimate: 2600 us
- [x] 6. VOXEL CAVE GRAPH | DOD: reads `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc` waypoints into acoustic portal nodes without touching nav-grid storage | Alternative rejected: rebuilding voxel portal topology in audio | Estimate: 2100 us
- [x] 7. BURST PATHFINDING (`AcousticPathJob`) | DOD: added `[BurstCompile]` `IJob` Dijkstra/BFS hybrid over capped native nodes with `NativeList<int>` open/closed sets; child assembly Roslyn compile clean | Alternative rejected: managed `List<T>` A* in playback | Estimate: 3200 us
- [x] 8. DISTANCE DELAY | DOD: result delay is `TrueDistanceMeters / 1480f` and playback uses `AudioSource.PlayDelayed` capped for source-pool safety | Alternative rejected: straight-line distance delay through walls | Estimate: 900 us
- [x] 9. CORNER DIFFRACTION | DOD: path reconstruction counts intermediate nodes; each corner applies 0.70794576 gain and a 2000Hz/corner low-pass cap; post-audit verified normal pooled playback applies `Transmission01` | Alternative rejected: per-surface diffraction simulation | Estimate: 1000 us + 450 us audit fix
- [x] 10. VIRTUAL SOURCE PROJECTION | DOD: `SpatialAudioManager` repositions normal and low-pass playable sources to `LastPortalAup` before LOD/Haas/pan and adds ITD-derived pan offset | Alternative rejected: panning from the true source through solid walls | Estimate: 1600 us + 600 us audit fix
- [x] 11. BULKHEAD LOW-PASS | DOD: sealed habitat edge flags propagate into `AcousticPortalFlags.SealedBulkhead`, capping cutoff to 400Hz and adding 10ms | Alternative rejected: treating sealed bulkheads as hard blockers, which kills useful muffled leakage | Estimate: 800 us
- [x] 12. ROOM REVERB COUPLING | DOD: habitat `RoomVolumes` are copied into portal nodes and translated into source `reverbZoneMix` via Sabine volume scaling | Alternative rejected: running a new FDN solver in audio path | Estimate: 900 us
- [x] 13. AUP SHIFT SAFETY | DOD: route query, nodes, cache, and virtual source use `AcousticAup`/`AbsoluteUniversePosition`; runtime floats are only final presentation | Alternative rejected: caching raw world-space `Vector3` across origin shifts | Estimate: 1200 us
- [x] 14. MAX NODES LIMIT | DOD: job, native buffers, habitat adapter, and voxel adapter are hard-capped at 30 nodes / 60 edges | Alternative rejected: unbounded graph search on playback | Estimate: 600 us
- [x] 15. REPROJECTION CACHE | DOD: fixed 16-entry cache reuses path results when source/listener AUPs remain within 1m | Alternative rejected: dictionary cache or per-emitter managed objects | Estimate: 1300 us
- [x] 16. ZERO-GC | DOD: hot route uses persistent `NativeArray`, `NativeList<int>` open/closed sets, prewarmed `NativeQueue`, and fixed managed arrays allocated only during initialization | Alternative rejected: per-emission lists, LINQ, or managed heap paths | Estimate: 2200 us
- [x] 17. MATH LOD (THE DEAR LIE) | DOD: `Low`, `Mx350`, and `Unknown` tiers skip portal A* and remain on straight-line SDF/open fallback | Alternative rejected: running acoustic A* on toaster hardware | Estimate: 500 us
- [x] 18. TELEMETRY | DOD: 300-entry `NativeArray<AcousticTelemetryEntry>` blackbox records pathfinding ms, nodes, corners, distance, delay, cutoff, flags, hash; NaN dumps to `Dump_ACOUSTIC_PORTAL_PROPAGATION.bin` | Alternative rejected: chat-only diagnostics or managed log spam | Estimate: 1800 us
- [x] 19. OMEGA COMPILE CHECK: [BLOCKED BY DEPENDENCY] | DOD: Unity Roslyn `Hecton8.Audio.Propagation.rsp` compiles clean after post-audit fixes; `Hecton8.Core.rsp` now fails on unrelated `GasDynamicsSolver.TrySetRoomSubmergedFraction`; edit-test compile is blocked by missing `Hecton8.Core.ref.dll`; Unity batchmode remains blocked by an already-open project instance | Alternative rejected: editing atmosphere/fauna/modding/inventory or killing the user's Unity process | Estimate: acoustic compile 12500 us, core wall 56300 us, edit-test wall 18900 us

## Iteration Log
- Loop 0: Prompt, domain, and mandates loaded. No source code edited yet.
- Loop 1: Existing APIs inventoried. Confirmed `GlobalRegistry.Audio` service boundary, no `AcousticManager.Instance`, voxel macro portal route API, and habitat CSR graph with private module positions. Next: isolated Burst propagation kernel plus read-only adapters.
- Loop 2: Tasks 1-10 implemented. `Hecton8.Audio.Propagation.rsp` compiles clean under Unity Roslyn. `Hecton8.Core.rsp` is blocked by unrelated fauna/modding/visor interface/signal errors after resolving the new audio assembly.
- Loop 3: Tasks 11-18 self-read and marked. Room reverb coupling tightened after review. Second `Hecton8.Core.rsp` check still reports only non-audio blockers.
- Loop 4: Task 19 closed as dependency-blocked with compile evidence. Acoustic child assembly green; full project compile wall is outside assigned domain.
- Loop 5: Recursive prompt re-read complete. Voxel audit found only `TryBuildMacroPortalRouteNonAlloc`; no voxel mutation calls in the acoustic path. `<POLISH_MANDATE>` tag absent from batch file, so final polish used anti-bloat/static diff checks.
- Loop 6: Patient re-audit found normal `PlayAtPoint` computed an audible portal AUP but still positioned the `AudioSource` at the true source and skipped portal transmission gain. Fixed normal playback to use `audiblePosition` and `Transmission01`; restored the no-eviction helper to non-portal locals; hardened `AcousticPathJob` against uncreated result arrays, invalid AUP input, and zero-capacity scratch lists. `Hecton8.Audio.Propagation.rsp` remains green. Full core is now blocked by unrelated `GasDynamicsSolver.TrySetRoomSubmergedFraction`; tests are blocked by the missing core ref DLL.
