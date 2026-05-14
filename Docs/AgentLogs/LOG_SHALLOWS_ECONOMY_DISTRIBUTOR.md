# SHALLOWS_ECONOMY_DISTRIBUTOR Log

## 2026-05-14 - Ore LCG Weights

Status: PENDING VERIFICATION

What was wrong:
- `WORLD_RESOURCE_SPAWNER` had ore type rolls that were effectively global random Titanium/Copper/BasaltIron and did not know the drop-pod crash-site AUP.
- `IWorldResourceSpawnerReadModel` exposed positions only, so `TERRAIN_GPR_SYSTEM` could not distinguish Copper/Titanium/Silver.
- GPR held a concrete `ProceduralOreSpawner` reference, blocking ore economy asmdef isolation.
- No `DropPodLandedSignal(AUP)` lane existed in the active signal corridor.
- Blackbox telemetry did not carry `LocalTitaniumCount`.

What was done:
- Added `DropPodLandedSignal` as a 64-byte unmanaged AUP signal and consumed it through `SignalBus<DropPodLandedSignal>`.
- Extended `IWorldResourceSpawnerReadModel` with `TryGetOreTypes` and `LocalTitaniumCount`.
- Added `WorldOreTypeIds` with stable ids for None, BasaltIron, Copper, Titanium, and Silver.
- Added `Hecton8.World.Economy.asmdef` under `World/Resources` and removed the GPR concrete spawner dependency by resolving `IWorldResourceSpawnerReadModel`.
- Updated the Burst ore generation job to compute drop-pod distance from ore absolute coordinates to DropPodAUP, then use integer percent weights: near 70/30/0, far 40/40/20, tapered in between.
- Added Copper vein bias: next accepted roll after Copper gets an 85% Copper bias if within 2m; Low/MX350/Unknown uses sector-seed hash mask instead of distance.
- Added GPR ore filtering: HUD/API can call `SetOreFilterType`; non-matching ore pings write GPU alpha/strength at 0.1.
- Added `LocalTitaniumCount` to ore telemetry ring and binary dump.
- Added local graphics buffer upload helpers inside the economy assembly boundary to avoid depending on Core-internal `GraphicsBufferUploadUtility`.

Cinematic cheats used:
- Integer percent weights instead of simulating geological resource pressure.
- Linear distance-squared taper with const reciprocal instead of curve assets or runtime designer curves.
- Low-tier sector hash mask for Copper clump continuity instead of spatial neighbor search.
- GPR alpha/strength suppression by 0.1 instead of rebuilding the radar visual set.
- Existing triangle-wave fallback terrain height remains untouched and compatible with cold ore generation.

Exact microseconds saved / cost avoided:
- Rejected NativeHashMap/grid Copper clumping: saves estimated 40-120 us per sector generation on i3/MX350 and avoids native memory churn.
- Rejected managed ore-type copies for GPR: saves 128-2048 element copy and 0 B GC per scan; estimated 5-30 us avoided depending on active ore count.
- Rejected per-frame quota correction: saves all steady-frame cost; new ore economy cost remains cold-path only.
- Low-tier clump hash mask avoids one `distancesq` after Copper predecessor; estimated 0.04 us saved per affected candidate on MX350.
- Const reciprocal in taper avoids one candidate-level reciprocal expression; estimated sub-0.01 ms sector-level saving, recorded because polish mandate required it.

Verification:
- `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` succeeded with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj` remains red on unrelated global assembly-reference gaps; filtered output showed no errors matching the edited files.
- Unity MCP refresh failed because the local MCP endpoint at `127.0.0.1:8088` was unavailable.
- Standalone compiler validation could not load Unity's Roslyn dependency `System.Text.Encoding.CodePages`.
- `git diff --check` passed for edited files.

## 2026-05-14 - Ore Economy Post-Pass Hardening

Status: PENDING VERIFICATION

What was wrong:
- GPR persistent ping compaction decayed `GprSignalStrength` in-place. That made ore filter changes operate on already-eroded signal data instead of stable raw ping strength.
- Same-object ore read-model discovery used `GetComponents<MonoBehaviour>()`, which allocates a managed array during cold dependency probing.
- Ore blackbox dump path still used `Dump_WORLD_RESOURCE_SPAWNER.bin`, not the mandated `Dump_SHALLOWS_ECONOMY_DISTRIBUTOR.bin`.
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains this agent id, so the required periodic prompt re-extraction is now blocked by batch hygiene drift.

What was done:
- Preserved raw signal strength in `GprSignalStrength`; decay and 0.1 non-match filtering now write only the GPU/display payload.
- Recomputed highest GPR signal from active display values every job instead of carrying stale previous max.
- Replaced cold `GetComponents<MonoBehaviour>()` array allocation with a bounded `List<MonoBehaviour>` probe and explicit clear.
- Renamed ore telemetry dump target to `Dump_SHALLOWS_ECONOMY_DISTRIBUTOR.bin`.
- Revalidated affected assemblies with Unity Bee response files: `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`.

Cinematic cheats used:
- Display-side GPR filtering remains a cheap alpha/strength attenuation, not a radar rebuild.
- Raw/display split buys higher-tier GPR visual richness without changing ore authority or generation cadence.

Exact microseconds saved / cost avoided:
- Avoided one cold managed component array allocation during GPR dependency discovery; exact allocation size depends on same-object component count.
- Avoided filter-change radar rebuilds entirely; estimated 5-50 us per filter change depending on active ping count.
- Added display max recompute inside existing compaction loop; expected cost under 0.005 ms on i3/MX350 at 128 pings.

Verification:
- `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` passed with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj -v:minimal /m:1` passed with 0 warnings and 0 errors.
- Unity Bee response-file csc validation passed for `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`.
- `git diff --check` passed for edited files, with line-ending warnings only.
- Unity Editor console, PlayMode, GCMonitor, profiler, and scene wiring are not verified in this session.

## 2026-05-14 - Real Drop-Pod Anchor Regeneration

Status: PENDING VERIFICATION

What was wrong:
- A sector could generate from the fallback player AUP before the real `DropPodLandedSignal` arrived.
- Once the real signal arrived, the stored anchor updated, but the active sector was not regenerated, so first-hour ore weights could remain player-anchored.
- `IGroundRadarService` did not explicitly state that `GprSignalStrengthReadOnly` is raw while the GPU ping buffer is display-filtered.

What was done:
- Added a drop-pod anchor dirty bit to `ProceduralOreSpawner`.
- First real drop-pod AUP, or any later changed drop-pod AUP, now forces the current sector to regenerate against the real crash-site anchor.
- Existing depletion masks are reloaded, not wiped, so mined ore stays depleted across anchor refresh.
- Added public XML comments clarifying raw GPR lanes, display-ready GPU pings, ore filter ids, scan windows, and ore type constants.

Cinematic cheats used:
- Anchor refresh is a cold-sector reroll, not a continuous economy simulation.
- Depletion masks remain authoritative, avoiding expensive reconciliation passes or per-node object state.

Exact microseconds saved / cost avoided:
- Rejected immediate signal-drain regeneration: avoids scheduling work from LateFrame and keeps cadence controlled by sector refresh.
- Rejected depletion wipe/rebuild: avoids full authoritative node reconciliation and prevents mined ore resurrection.
- Added cost is one cold boolean branch plus exact AUP field comparisons only when drop-pod signals exist; 0 B/frame hot path.

Verification:
- `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` passed with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj -v:minimal /m:1` passed with 0 warnings and 0 errors.
- Unity Bee response-file csc validation passed for `Hecton8.World.Contracts`, `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`.
- Scoped forbidden-pattern scan over edited ore/GPR files returned `NO_MATCHES`.
- Unity Editor console, PlayMode, GCMonitor, profiler, and scene wiring are not verified in this session.
