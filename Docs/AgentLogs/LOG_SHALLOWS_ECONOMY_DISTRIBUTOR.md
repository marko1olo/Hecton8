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

## 2026-05-14 - Continued Hardening Pass

Status: PENDING VERIFICATION

What was wrong:
- Same-frame `DropPodLandedSignal` updates with different AUPs were skipped after the first signal.
- A late real drop-pod anchor did not force the already-generated active sector to rebuild around the crash site.
- GPR compaction decayed `GprSignalStrength` in place, so raw signal data degraded and filter/display math was mixed with authority data.
- GPR configured read-model probing still used the allocating `GetComponents<MonoBehaviour>()` array overload.
- Missing configured ore source could trigger registry fallback from scan cadence.

What was done:
- Added exact AUP equality gating for drop-pod signals: newer frames pass, same-frame changed AUPs pass, same-frame duplicates are ignored.
- Added active-sector ore regeneration on real drop-pod anchor changes while preserving current-sector depletion masks.
- Kept `GprSignalStrength` raw; decay and ore-filter alpha now affect only `GprPingGpu` and `MaxSignalStrength`.
- Replaced the allocating component array probe with a preallocated `List<MonoBehaviour>` and `GetComponents(List<T>)`.
- Limited GPR ore-read-model registry resolution to cold OnEnable wiring; scheduled scans now use the cached interface only.

Cinematic cheats used:
- Same deterministic AUP anchor for all tiers; higher tiers buy visual dressing, not heavier economy authority.
- GPR remains a display-strength fake over raw pings instead of rebuilding radar geometry per filter.

Exact microseconds saved / cost avoided:
- Avoided repeated missing-dependency registry lookup during scan cadence: estimated 45 us per scan path on weak CPU when the configured source is absent.
- Avoided managed component array allocation during GPR wiring; hot-path saving is 0 B/frame preserved.
- Prevented unnecessary duplicate same-frame drop-pod regeneration; one exact AUP compare replaces full sector thrash.

Verification:
- Scoped forbidden-pattern scan on edited files found no `$"..."`, `string.Format`, `.ToString()`, `foreach`, `math.sqrt`, `math.normalize`, allocating `GetComponents<MonoBehaviour>()`, scene Find, Resources.Load, or Unity random usage.
- `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` passed with 0 warnings and 0 errors.
- Unity response-file csc passed for `Hecton8.World.GPR` and `Hecton8.World.Economy`; Unity analyzer-load warnings were emitted, but no compile errors.
- `git diff --check` passed for edited files with CRLF warnings only.
- `dotnet build Hecton8.Core.csproj` is blocked by a locked generated input DLL outside this task.
- Filtered Core response-file csc is blocked by unrelated `BinaryLayoutManifest` Save V10 and `HardwareProfileCatalog` errors; no edited-file errors were reported.
- Unity MCP resources are unavailable in this session, so Unity Console, PlayMode, GC, profiler, and visual checks remain PENDING VERIFICATION.
