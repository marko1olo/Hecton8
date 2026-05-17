# PATH_FUNNEL_NAVMESH_FIXER Log

## Surgical Record - 2026-05-16

What was wrong:
- Assigned `Assets/_Project/Scripts/AI/Pathfinding/` folder did not exist.
- Funnel smoothing had no owned Burst path, no sector-local/AUP contract, no WFC door invalidation bridge, and no blackbox telemetry in this domain.
- Radius-only corner protection was insufficient; portal contracts needed an SDF clearance lane from the navgrid owner.
- Full `dotnet build` validation was blocked by upstream non-pathfinding compile errors outside AI/PATHING at this pass.

What was done:
- Added `Hecton8.AI.Pathfinding.asmdef` to isolate the new AI pathing module.
- Added `PathFunnelContracts.cs` with `NavPortal`, `PathFunnelResult`, `PathFunnelMathLod`, invalidation payloads, active-path records, and 300-frame telemetry entries.
- Added `FunnelSmoothingJob.cs`: Burst `IJob`, no `Vector3`, XZ cross-product funnel string pulling, Low/Middle/High/Ultra look-ahead, stressed look-ahead 1, NaN/collinear guards, door block checks against `WfcOutpostGrid`, radius erosion, SDF clearance clamp, and AUP blit output.
- Added `PathFunnelSchedule.cs`: PRE_SIMULATION schedule helper and POST_SIMULATION readback helper that refuses to force-complete unfinished jobs.
- Added `PathFunnelNavmeshRuntime.cs`: cached `IDataVault`, `SignalBus<WfcOutpostStateChangedSignal>` consumption, exact 500-bit corridor masks, door-close invalidation, bounded invalidation ring, and binary blackbox dump path.
- Updated `Docs/Tasks/Status_PATH_FUNNEL_NAVMESH_FIXER.md` and `Docs/AgentLogs/Rationale_PATH_FUNNEL_NAVMESH_FIXER.md` with task-by-task DOD, rejected alternatives, scalability tiers, and hardware impact.

Cinematic cheats used:
- Chose XZ scalar cross-product string pulling for WFC outpost corridors instead of angle math or 3D physical steering.
- Used bounded Math LOD look-ahead rather than full-corridor smoothing on every request.
- Used pre-eroded SDF clearance carried on the portal instead of sampling raw SDF texture data inside AI.
- Door invalidation is exact bitmask membership, not resimulated obstacle physics.

Exact microseconds saved:
- Cross-product funnel versus angle/acos smoothing: estimated 20-80 us per 32-portal path and 30-50 scalar ALU ops per portal.
- WFC door bitmask invalidation versus corridor cell scans: estimated 20-80 us per door event.
- Stressed look-ahead 1 versus high-tier 16: estimated 20-60 us per long path during frame pressure.
- Non-blocking readback versus forced job completion: estimated 50-300 us sync spike avoided.
- Telemetry blackbox write: estimated under 1 us/frame; dump allocation occurs only on explicit dump/crash path.

Verification:
- Static anti-bloat scans passed for owned pathfinding files: no `Vector3`, managed lists, `NativeList`, Unity message loops, `GameObject.Find`, A*, or Unity NavMesh.
- `dotnet restore .\Assembly-CSharp.csproj` succeeded.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` failed with 33 upstream non-pathfinding errors and 0 `PathFunnel`/`AI\Pathfinding` matches. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log`.
- Earlier `dotnet build .\Assembly-CSharp.csproj --no-restore -m:2 /nr:false /v:minimal /clp:ErrorsOnly` failed with 217 non-pathfinding errors and 0 pathfinding matches. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.

Integrator note:
- At this pass, do not treat the build as green. The pathfinding module was statically clean, but project validation was blocked by unrelated non-pathfinding dependency errors.

## Multiplatform/H-Phi Polish Record - 2026-05-16

What was wrong:
- The runtime ownership model was not strict enough for the follow-up gate: private persistent path invalidation arrays are unacceptable when GlobalDataVault exists.
- `NavPortal` still relied on sequential packing; that leaves ABI interpretation to the runtime/compiler instead of making Quest/ARM64 layout explicit.
- The blackbox dump path used a managed `byte[]` copy before file export.
- The previous build evidence was stale: Core dependency count changed, and Assembly-CSharp needed a fresh restore/build attempt.

What was done:
- Path invalidation state now resolves through vault handles for `PathFunnelActivePaths`, `PathFunnelCellMasks`, `PathFunnelInvalidations`, `PathFunnelTelemetryRing`, and `PathFunnelRuntimeState` under `SystemID.AIPathfinding`.
- `PathFunnelRuntimeState` stores active count, ring cursors, telemetry cursor, invalidation count, last path/corridor/sector/cell, dump request, and vault generation in one explicit 64-byte block.
- All pathing binary structs use explicit `Pack = 1` field offsets. 64-bit fields remain on aligned offsets where those structs contain them.
- Blackbox export now streams from the native telemetry pointer through `ReadOnlySpan<byte>` and `FileStream`; no managed `byte[]` copy remains.
- AUP grid conversion now uses fixed inverse cell-size multiply; no runtime divide remains in the AUP conversion path.
- Re-ran static debt scans and build probes. At this pass Core build still showed 33 non-pathfinding errors and Assembly-CSharp showed 217 non-pathfinding errors, primarily missing RealtimeCSG source files plus Core dependency failures. Both logs had zero pathfinding matches. Later records supersede this build snapshot; use the newest Source Truth Reconciliation record for current blocker counts.

Cinematic cheats used:
- Kept WFC door response as exact 500-bit corridor mask tests instead of simulating obstacles or broad path physics.
- Preserved tiered look-ahead: Low 2, stressed 1, High/Ultra 16. This is the "Dear Lie" path: cheap scalar geometry under load, smoother silhouette when budget exists.
- Did not add visual systems because the XML says VFX N/A. Saved pathing budget is explicitly reserved for presentation owners to spend on silt, visor, hull, and particle overkill.

Exact microseconds saved:
- Cross-product funnel versus angle/acos smoothing remains estimated at 20-80 us per 32-portal path and 30-50 scalar ALU ops per portal.
- Door bitmask invalidation versus corridor scans remains estimated at 20-80 us per door event.
- Stressed look-ahead 1 versus 16 remains estimated at 20-60 us per long path during frame pressure.
- DataVault eviction does not claim a measured frame-time win; its gain is lifetime safety and avoiding private persistent native ownership. Measured profiler proof is absent.

Verification:
- `rg` hard-ban scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Struct layout scan confirmed `Pack = 1`, explicit offsets, `PathFunnelRuntimeState`, `SystemID.AIPathfinding`, and path funnel vault `BufferID` entries.
- `Select-String` over both build logs found zero `PathFunnel`, `AI\Pathfinding`, `AI/Pathfinding`, `PathFunnelRuntimeState`, or `AIPathfinding` matches.

Integrator note:
- Build is still blocked. Do not label this runtime-verified. Static AI/PATHING audit is clean; Unity import, Play Mode, profiler, GCMonitor, and player build remain pending after upstream dependency repair.

## Survival Re-Audit Record - 2026-05-16

What was wrong:
- Invalidation telemetry still had drift risk: replacing or unregistering an already-invalidated active path could leave `InvalidatedPathCount` overstated.
- Repeated close events for the same cell/path could enqueue duplicate invalidation payloads and consume blackbox ring capacity without new state.
- Ring cursors used integer modulo; branch wrap is cheaper and sufficient for fixed-size native rings.
- `AgentRadiusMeters` was clamped to non-negative but not sanitized for NaN/Infinity before radius erosion.

What was done:
- `FunnelSmoothingJob` now treats non-finite `AgentRadiusMeters` as zero before applying corner/radius erosion.
- `PathFunnelNavmeshRuntime` now decrements invalidated active count when invalidated paths are re-registered or unregistered.
- WFC invalidation now skips paths already marked invalidated, so `PathInvalidationCount` and invalidation-ring payloads represent state transitions only.
- Invalidation read/write cursors and telemetry cursor now use `AdvanceRingCursor` branch wrapping instead of `%`.
- Re-ran static anti-bloat scans, finite/division scans, Core build, Assembly-CSharp build, and build-log pathfinding filters.

Cinematic cheats used:
- Kept the door reaction as exact bitmask membership and transition telemetry instead of replaying obstacle simulation.
- Kept the prompt-local visual boundary: VFX remains N/A in this XML, so no shader, particle, or presentation-domain files were touched.

Exact microseconds saved:
- Modulo removal is nominal sub-microsecond and not profiler-measured; no fake frame-time claim is made.
- Duplicate invalidation suppression prevents avoidable recovery work and preserves the 300-frame blackbox for real transitions.
- Core pathing estimates remain unchanged: cross-product funnel saves an estimated 30-50 scalar ALU ops per portal versus angle math; door bitmask invalidation saves an estimated 20-80 us per door event versus corridor scans.

Verification:
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in the owned pathing files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 0 with 0 errors.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:2 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 216 missing `RealtimeCSG.csproj` source-file errors and zero pathfinding matches.

Integrator note:
- Owned AI/PATHING source is static verified master grade. Full Unity assembly, import, Play Mode, profiler, GCMonitor, Burst inspector, and player-build proof remain pending until the missing RealtimeCSG package source references are repaired.

## Blackbox Exception-Survival Record - 2026-05-16

What was wrong:
- The binary dump path still used filesystem APIs directly. If the path, directory, or stream failed, the dump request could throw during the crash-diagnosis path.
- Core build evidence changed during parallel workspace work: `Hecton8.Core.csproj` is no longer green and now fails in non-pathfinding World/VFX/RepairTool files.

What was done:
- Added `PathFunnelTelemetryFlags.BlackBoxDumpFailed`.
- Converted `DumpBlackBox` to `TryDumpBlackBox` and contained filesystem failure on the explicit dump path.
- `LateFrameTick` now clears stale dump-failure state on a new request, writes the normal heartbeat, and marks `BlackBoxDumpFailed` if the binary dump cannot be created.
- `PatchTelemetryFlags` updates the just-written telemetry slot on dump failure, so the current 300-frame ring captures the failed dump request immediately.
- Re-ran owned pathing hard-ban scans, cursor/division scans, Core build, and build-log pathfinding filters.

Cinematic cheats used:
- No new simulation or presentation work. This is crash-survival hardening only. The XML still marks VFX N/A.

Exact microseconds saved:
- No measured runtime saving claimed. The normal frame path remains one telemetry struct write; dump I/O still happens only on explicit crash/dump request.

Verification:
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in owned pathing files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 137 non-pathfinding errors and zero pathfinding matches.
- `dotnet restore .\Assembly-CSharp.csproj` exits 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 because `Unity.RenderPipelines.Universal.Runtime.dll` is locked by another process; that pass's Assembly log had zero pathfinding matches.

Integrator note:
- Current compile wall is outside AI/PATHING: `FloraInteractionManager`, `SargassumMicroFaunaBoids`, `RepairTool`, `HectonUnderwaterVisuals`, and a locked URP build output. Do not attribute those failures to the funnel module.

## ABI Tail-Byte Audit Record - 2026-05-16

What was wrong:
- `PathFunnelResult`, `PathFunnelActivePath`, and `PathFunnelInvalidation` used explicit `Pack = 1` layouts, but their fixed-size tails were unnamed bytes.
- That was not a runtime allocation problem, but it left avoidable ambiguity for ARM64/Quest binary payload review and blackbox decoding.

What was done:
- Added `Reserved0` to `PathFunnelResult` at offset 28.
- Added `Reserved0` to `PathFunnelActivePath` at offset 28.
- Added `Reserved0` and `Reserved1` to `PathFunnelInvalidation` at offsets 26 and 28.
- Updated status and rationale with the exact reason: ABI safety, no fake microsecond claim.

Cinematic cheats used:
- None. This is data-layout hardening inside AI/PATHING only.

Exact microseconds saved:
- 0 measured. No runtime speed claim is made for explicit tail-byte coverage.

Verification:
- Struct layout scan confirmed explicit `Reserved*` tail fields in `PathFunnelResult`, `PathFunnelActivePath`, and `PathFunnelInvalidation`.
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in owned pathing files.
- `git diff --check` reported no whitespace errors; only existing LF-to-CRLF warnings for touched files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 7 non-pathfinding compiler errors in Core diagnostics/World/Audio files and zero pathfinding matches.
- `dotnet restore .\Assembly-CSharp.csproj` exits 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` timed out after logging 216 missing RealtimeCSG source errors and zero pathfinding matches.

Integrator note:
- This patch does not touch World, VFX, URP, RealtimeCSG, shaders, or presentation-domain files. Current validation blockage remains outside AI/PATHING.

## Extreme-Value Survival Record - 2026-05-16

What was wrong:
- AUP conversion handled NaN/Infinity but not finite coordinates large enough to overflow `double` to `long` grid casts.
- The invalidation ring accepted a capacity of 1, which makes the read/write cursor scheme unable to represent one queued invalidation.
- Inspector-set capacities were unbounded, creating avoidable native memory pressure risk in the vault allocation path.

What was done:
- Added grid-coordinate bounds before AUP casts in `FunnelSmoothingJob`.
- Out-of-range AUP conversion now marks `AupFallback` and returns deterministic zero-grid output instead of relying on unchecked cast behavior.
- Changed invalidation capacity minimum to 2 and clamped active-path/invalidation capacities to 4096 in `PathFunnelNavmeshRuntime`.

Cinematic cheats used:
- None. This is survival hardening for deterministic math and bounded native memory.

Exact microseconds saved:
- No measured saving claimed. The patch trades a few scalar checks for deterministic failure behavior and prevents pathological allocation pressure.

Verification:
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in owned pathing files.
- Struct/capacity scan found explicit tail `Reserved*` fields, `Min(2)` invalidation capacity, 4096 max vault capacities, and `IsSafeLongGridCoordinate`.
- `git diff --check` reported no whitespace errors; only LF-to-CRLF warnings for touched files.
- At that pass, build logs still had zero pathfinding matches. Full build validation remained blocked by non-pathfinding Core/RealtimeCSG dependencies.

Integrator note:
- No cross-domain edits. Presentation overkill remains outside this XML because VFX is N/A.

## Asmdef Contract Reference Record - 2026-05-16

What was wrong:
- `PathFunnelNavmeshRuntime` uses `Hecton8.Core.Contracts.Signals`, but `Hecton8.AI.Pathfinding.asmdef` did not directly reference `Hecton8.Core.Contracts`.
- That made the new assembly rely on transitive Core visibility for the WFC signal lane.

What was done:
- Added `Hecton8.Core.Contracts` to the pathfinding asmdef references.

Cinematic cheats used:
- None. This is assembly graph correctness.

Exact microseconds saved:
- 0 measured. No runtime speed claim; compile-boundary fix only.

Verification:
- Asmdef reference scan shows direct `Hecton8.Core.Contracts` reference present beside Core and Memory.
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- `git diff --check` reported no whitespace errors; only LF-to-CRLF warnings for touched files.
- At that pass, build logs still had zero pathfinding matches; full build validation remained blocked outside AI/PATHING.

Integrator note:
- This does not introduce a direct dependency on WFC implementation classes; it keeps the typed signal contract boundary.

## Focused Bee Response Probe Record - 2026-05-16

What was wrong:
- Root build logs were blocked outside AI/PATHING, and the generated root `.csproj` surface did not include the new pathfinding assembly.

What was done:
- Located `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Pathfinding.rsp`.
- Confirmed the response file lists all four owned source files: `FunnelSmoothingJob.cs`, `PathFunnelContracts.cs`, `PathFunnelNavmeshRuntime.cs`, and `PathFunnelSchedule.cs`.
- Confirmed the response file includes direct references to `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`.
- Ran Unity Roslyn csc against the response file with output redirected to `Temp/CodexPathfindingCheck`.

Cinematic cheats used:
- None. Verification-only pass.

Exact microseconds saved:
- 0 measured. Verification has no runtime claim.

Verification:
- Probe exits 1 with only `CS0006` for missing `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll`; evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_PathfindingRsp.log`.

Integrator note:
- Once Core ref generation is repaired, rerun this focused response probe before claiming Unity/Burst compile proof.

## WFC Contract Constant Purge Record - 2026-05-16

What was wrong:
- Pathfinding carried local WFC magic values for cell count, mask word count, and the door-open flag.
- The same values already exist in Core contracts, so the duplicate pathing constants could drift from persistence and signal producers.

What was done:
- `PathFunnelConstants.WfcOutpostCellCount` now aliases `WfcOutpostPersistenceConstants.CellCount`.
- `PathFunnelConstants.WfcCellMaskWordCount` derives from the contract cell count.
- `PathFunnelConstants.WfcDoorOpenFlag` now aliases `WfcOutpostCellStateFlags.DoorOpen`.

Cinematic cheats used:
- Exact bitmask invalidation remains the cheap door-state truth; no new simulation or presentation work.

Exact microseconds saved:
- 0 measured. This is interface drift prevention, not runtime optimization.

Verification:
- Contract alias scan confirms `PathFunnelConstants` now uses `WfcOutpostPersistenceConstants.CellCount`, derives mask words from that count, and aliases `WfcOutpostCellStateFlags.DoorOpen`.
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in owned pathing files.
- `git diff --check` reported no whitespace errors; only LF-to-CRLF warnings for touched files.
- Focused Bee response probe still exits 1 only on missing upstream `Hecton8.Core.ref.dll`; no pathfinding diagnostic emitted.

Integrator note:
- No new signal was invented. Pathing stays on the existing `WfcOutpostStateChangedSignal` typed lane.

## Compile Snapshot Record - 2026-05-16

What was wrong:
- Earlier compile status was stale under concurrent work. At this snapshot, Core had recovered, but the status file still named old Core blockers.

What was done:
- Reran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly`.
- Reran `dotnet restore .\Assembly-CSharp.csproj`.
- Reran `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly`.
- Updated status and rationale for that snapshot to stop carrying then-stale Core failure evidence.

Cinematic cheats used:
- None. Verification-only pass.

Exact microseconds saved:
- 0 measured. Compile evidence has no runtime claim.

Verification:
- At this snapshot, `Hecton8.Core.csproj` exited 0.
- `Assembly-CSharp.csproj` exited 1 with 237 missing RealtimeCSG source-file errors and zero pathfinding matches.

Integrator note:
- This snapshot is superseded by later records. Owned pathfinding diagnostics did not appear in those build logs.

## Homeostasis Truth Record - 2026-05-16

What was wrong:
- The Burst job executed stressed one-portal smoothing when `Stressed != 0`, but `PathFunnelResult.MathLod` still reported the requested tier.
- That made result/blackbox consumers unable to distinguish real High/Ultra smoothing from homeostasis-degraded smoothing.

What was done:
- Added `ResolveEffectiveMathLod` in `FunnelSmoothingJob`.
- `PathFunnelResult.MathLod` now stores the effective tier.
- `ResolveLookAhead` now receives the effective tier, so reporting and execution use the same byte.

Cinematic cheats used:
- Toaster/homeostasis cheat remains one-portal smoothing under stress; this patch makes the cheat explicit in result telemetry.

Exact microseconds saved:
- 0 measured. The executed math path already existed; this is blackbox truth and diagnostics correctness.

Verification:
- Source scan confirms `result.MathLod = ResolveEffectiveMathLod()` and `ResolveLookAhead(result.MathLod)`.

Integrator note:
- Consumers should treat `PathFunnelResult.MathLod` as executed/effective LOD, not requested LOD.

## Current Compile Wall Refresh - 2026-05-16

What was wrong:
- The previous Core-pass statement became stale under concurrent workspace changes.
- At that pass, `Hecton8.Core.csproj` failed outside AI/PATHING before owned pathfinding code could be validated through the project build.

What was done:
- Reran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly`.
- Reran `dotnet restore .\Assembly-CSharp.csproj`.
- Reran `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly`.
- Rewrote `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log` and `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.
- Rescanned both logs for `PathFunnel`, `AI\Pathfinding`, `AI/Pathfinding`, `Hecton8.AI.Pathfinding`, `AIPathfinding`, `FunnelSmoothingJob`, and `PathFunnelResult`.

Cinematic cheats used:
- None. Verification-only pass.

Exact microseconds saved:
- 0 measured. Compile evidence has no runtime claim.

Verification:
- At that pass, `Hecton8.Core.csproj` exited 1 with 49 non-pathing missing contract symbol errors.
- `Assembly-CSharp.csproj` exited nonzero with 216 missing RealtimeCSG source-file errors.
- Both logs had 0 owned pathfinding matches.

Integrator note:
- Validation blockers were contract owner drift and RealtimeCSG package source debt outside AI/PATHING. Do not treat this as a pathfinding compile diagnostic.

## Burst-Safe AUP Contract Constant Record - 2026-05-16

What was wrong:
- AUP conversion used the contract static ref property `HectonPhysicsContract.OneOverAupSectorSizeMeters` inside the Burst job.
- That risks pulling contract static constructor/property mechanics into the Burst kernel.

What was done:
- Kept the authoritative `HectonPhysicsContract.AupSectorSizeMetersDouble` constant for sector size.
- Changed inverse sector size to a compile-time `const double 1.0d / HectonPhysicsContract.AupSectorSizeMetersDouble`.

Cinematic cheats used:
- None. This is Burst-safety polish.

Exact microseconds saved:
- 0 measured. This avoids a compile/runtime integration hazard; no speed claim.

Verification:
- Source scan confirms `inverseCellSize` is now a compile-time `const double` and no pathfinding code calls `HectonPhysicsContract.OneOverAupSectorSizeMeters`.

Integrator note:
- Pathfinding still follows the shared Core contract sector size; it no longer touches the contract static inverse property from Burst code.

## Source Truth Reconciliation Record - 2026-05-16

What was wrong:
- Status/rationale text said the Burst AUP inverse was already a compile-time constant, but a re-read of `FunnelSmoothingJob.cs` found the source still using `HectonPhysicsContract.OneOverAupSectorSizeMeters`.
- Active-path register/unregister resolved the invalidation ring and discarded it with `_ = invalidations`.
- Unknown `MathLod` bytes could be reported back unchanged even though execution fell through to the Low look-ahead path.

What was done:
- Changed `FunnelSmoothingJob` to derive `inverseCellSize` as `const double 1.0d / HectonPhysicsContract.AupSectorSizeMetersDouble`.
- Added `PathFunnelResultFlags.InvalidMathLod` and normalized unknown Math LOD requests to Low before selecting look-ahead.
- Added `TryResolveActivePathMutationViews` so register/unregister touch only active paths, active cell masks, and runtime state.
- Refreshed Core and Assembly-CSharp build logs and rescanned them for owned pathfinding symbols.

Cinematic cheats used:
- Invalid LOD now takes the toaster-safe Low path instead of leaking a bogus tier into telemetry.

Exact microseconds saved:
- 0 measured. This pass is source-truth, telemetry correctness, and narrower vault mutation scope; no profiler/Burst Inspector timing was collected.

Verification:
- `rg -n "OneOverAupSectorSizeMeters|HectonPhysicsContract\.OneOver|ref readonly" Assets/_Project/Scripts/AI/Pathfinding` returns no matches.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding` reports only line-ending warnings.
- At that pass, `Hecton8.Core.csproj` exited 1 with 3 missing non-pathing contract source files and zero pathfinding matches.
- At that pass, `Assembly-CSharp.csproj` exited nonzero with 216 RealtimeCSG missing source errors and zero pathfinding matches.

Integrator note:
- That pass's compile walls were outside AI/PATHING: missing Core contract source files and RealtimeCSG package source references. The owned pathfinding log scans remained clean.

## Blackbox Catch Narrowing Record - 2026-05-16

What was wrong:
- `TryDumpBlackBox` used a broad `catch (Exception)`.
- That contained expected dump I/O failures, but it could also suppress unexpected runtime faults while pretending the system only had a filesystem failure.

What was done:
- Replaced the broad catch with explicit `IOException`, `UnauthorizedAccessException`, `NotSupportedException`, and `ArgumentException` catches.
- Preserved the existing `BlackBoxDumpFailed` telemetry path for expected file/path failures.

Cinematic cheats used:
- None. This is crash-evidence hygiene, not presentation work.

Exact microseconds saved:
- 0 measured. The dump path is explicit/non-hot; no runtime speed claim.

Verification:
- `rg -n "catch \\(Exception" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` returns no matches.
- Hard-ban pathfinding scan still returns no forbidden hot-path patterns.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` reports only LF-to-CRLF warnings.

Integrator note:
- Expected dump file/path failures still set `PathFunnelTelemetryFlags.BlackBoxDumpFailed`. Unexpected exceptions are no longer swallowed by this domain.

## WFC Contract Boundary Record - 2026-05-17

What was wrong:
- The Burst door check accepted any portal cell index inside the current `WfcGridBitmasks` buffer length.
- That let an oversized or stale vault buffer become the semantic authority instead of the WFC contract cell count.
- Blocked-path telemetry used the zero-based portal index for `ProcessedPortalCount`, so a block on portal 0 reported zero processed portals.

What was done:
- Added `PathFunnelResultFlags.InvalidWfcCell`.
- `IsDoorCellBlocked` now rejects cells outside `PathFunnelConstants.WfcOutpostCellCount` before checking the vault buffer length.
- Blocked-path telemetry now writes `ProcessedPortalCount = i + 1`.
- `RegisterActivePath` now stores `CellCount` from successful WFC bitmask writes only; invalid corridor cells no longer inflate active-path metadata.
- Refreshed Core and Assembly-CSharp build logs and rescanned both for owned pathfinding symbols.

Cinematic cheats used:
- The door truth remains a cheap WFC flag lookup, but malformed out-of-contract cells are now a flagged diagnostic instead of false door physics.

Exact microseconds saved:
- 0 measured. This is correctness hardening and telemetry truth; no profiler/Burst Inspector timing was collected.

Verification:
- Source scan confirms `InvalidWfcCell`, contract-count gating, and `ProcessedPortalCount = i + 1`.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding` reports only LF-to-CRLF warnings.
- At that pass, `Hecton8.Core.csproj` exited 1 with 2 non-pathing `HectonPlayerMotor` interface errors and zero pathfinding matches.
- At that pass, `Assembly-CSharp.csproj` exited nonzero with 216 RealtimeCSG missing source errors, 5 non-pathing Core caller/contract errors, and zero pathfinding matches.

Integrator note:
- That pass's compile walls were outside AI/PATHING: `HectonPlayerMotor`, EquipmentInteraction contracts, `TetherManager`, and RealtimeCSG package source references. Later records supersede the blocker counts.

## Unique WFC Cell Count And Validation Refresh - 2026-05-17

What was wrong:
- `PathFunnelActivePath.CellCount` counted successful calls into the bitmask writer, but duplicate corridor cells can target an already-set bit.
- That made metadata larger than the unique WFC bitmask truth used for door invalidation.
- Status evidence still described stale Core build failures even after the current Core build succeeded.

What was done:
- `SetPathCell` now returns false when the target WFC mask bit is already set.
- Active-path registration increments `validCellCount` only for a new bit write.
- Re-read the original XML prompt and refreshed status/rationale/log evidence.
- Confirmed the timed Assembly-CSharp build process is no longer running.
- Updated current build truth: Core green, Assembly-CSharp blocked by RealtimeCSG missing sources only in the current log.

Cinematic cheats used:
- Duplicate WFC corridor entries are collapsed by the bitmask itself. No extra container, sort, or managed dedup pass was added.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected for this correctness pass.

Verification:
- `rg -n "validCellCount|SetPathCell|wordValue|CellCount =" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` shows unique-bit counting.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding Docs/Tasks/Status_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/Rationale_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/LOG_PATH_FUNNEL_NAVMESH_FIXER.md` reports only LF-to-CRLF warnings.
- `Hecton8.Core.csproj` exits 0 with `Build succeeded.`.
- Current `Assembly-CSharp` build log contains 216 `RealtimeCSG.csproj` CS2001 missing source errors and 0 owned pathfinding matches.

Integrator note:
- Do not route Assembly-CSharp failures to AI/PATHING on the current evidence. The remaining validation blocker is RealtimeCSG package source debt; owned pathfinding code has no current build-log diagnostics.

## Signal/Vault Phase Truth Record - 2026-05-17

What was wrong:
- WFC door-close invalidation used the vault cell as `CurrentFlags` when `BufferID.WfcOutpostGrid` was present.
- Save persistence writes that vault from the same `WfcOutpostStateChangedSignal` lane, so the vault can be one phase behind the signal.
- A close signal could therefore be skipped if the signal said closed but the vault still said open.

What was done:
- `WfcOutpostStateChangedSignal.CurrentFlags` now drives close-transition detection.
- `PathFunnelConstants.WfcMutableFlagMask` aliases the shared persistence mask.
- The vault cell is still read and compared as an audited snapshot.
- `PathFunnelTelemetryFlags.WfcVaultSignalMismatch` is written into the 300-frame blackbox when the vault and signal disagree.
- No new signal lane, managed delegate, or cross-domain WFC class dependency was introduced.

Cinematic cheats used:
- The cheap door truth is still one byte mask and one exact corridor bit test. No physics, raycasts, or broad path scans were added.

Exact microseconds saved:
- 0 measured. This pass prevents missed invalidations; no profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "ResolveCurrentCellFlags|WfcVaultSignalMismatch|WfcMutableFlagMask|SignalBus<WfcOutpostStateChangedSignal>|ReadOnlySpan<WfcOutpostStateChangedSignal>" Assets/_Project/Scripts/AI/Pathfinding` confirms signal-authoritative close handling and mismatch telemetry.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- Struct-layout scan still shows explicit `Pack = 1` payloads and reserved tail coverage.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding Docs/Tasks/Status_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/Rationale_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/LOG_PATH_FUNNEL_NAVMESH_FIXER.md` reports only LF-to-CRLF warnings.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current build status remains from the previous evidence: Core green, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches. This pass is source/static verified only.

## WFC Mutable Mask Isolation Record - 2026-05-17

What was wrong:
- Runtime close detection masked signal `CurrentFlags` but still tested raw `PreviousFlags`.
- The Burst door-block check read raw WFC vault bytes.
- Future or reserved WFC bits could leak into pathing door truth if producers changed payload usage.

What was done:
- `ProcessWfcStateSignal` now masks `signal.PreviousFlags` through `PathFunnelConstants.WfcMutableFlagMask`.
- Invalidation payloads now receive masked previous/current cell flags.
- `FunnelSmoothingJob.IsDoorCellBlocked` now masks `WfcGridBitmasks[cellIndex]` through the same shared mutable mask before checking `WfcDoorOpenFlag`.

Cinematic cheats used:
- None. This is interface hygiene for the cheap WFC door-bit path.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "previousFlags|signal\\.PreviousFlags|WfcMutableFlagMask|cellFlags =|IsDoorCellBlocked|ResolveCurrentCellFlags" Assets/_Project/Scripts/AI/Pathfinding` confirms masked previous/current signal flags and masked Burst vault bytes.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- `git diff --check -- Assets/_Project/Scripts/AI/Pathfinding Docs/Tasks/Status_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/Rationale_PATH_FUNNEL_NAVMESH_FIXER.md Docs/AgentLogs/LOG_PATH_FUNNEL_NAVMESH_FIXER.md` reports only LF-to-CRLF warnings.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- This is a source/static validation pass only. Current compile evidence remains unchanged from prior logs.

## Blackbox Transient Flag Truth Record - 2026-05-17

What was wrong:
- `WfcVaultSignalMismatch` was written into `PathFunnelRuntimeState.TelemetryFlags` and would stay set after one vault/signal disagreement.
- That made later blackbox frames look like they still had a phase-order mismatch even when the next frames were clean.

What was done:
- Added `PathFunnelTelemetryFlags.TransientFrameMask`.
- `LateFrameTick` now captures `writtenTelemetryFlags`, writes the blackbox frame, then clears transient bits from runtime state.
- Dump failure patching now uses the captured current-frame flags plus `BlackBoxDumpFailed`, so a same-frame mismatch is not lost when the explicit dump fails.
- `BlackBoxDumpFailed` remains persistent until the next dump request clears it.

Cinematic cheats used:
- None. This is blackbox truth hygiene.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "TransientFrameMask|writtenTelemetryFlags|patchedTelemetryFlags|WfcVaultSignalMismatch|TelemetryFlags" Assets/_Project/Scripts/AI/Pathfinding` confirms frame-scoped mismatch telemetry and persistent dump-failure patching.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.

## Editor-Time Registry Guard Record - 2026-05-17

What was wrong:
- `PathFunnelNavmeshRuntime.OnEnable` touched `GlobalRegistry` and vault handles without a play-mode guard.
- Editor/import-time enable could register runtime tick owners outside the dispatcher lifecycle.

What was done:
- Added `if (!Application.isPlaying) return;` at the start of `OnEnable`.
- Runtime fast tick, late-frame tick, hot-swap registration, and vault handle resolution now occur only in play mode.

Cinematic cheats used:
- None. This is lifecycle hygiene.

Exact microseconds saved:
- 0 measured. This is cold-path safety, not a frame-time optimization.

Verification:
- `rg -n "Application\\.isPlaying|OnEnable\\(|TryRegisterFastTickable|TryRegisterLateFrameTickable|TryRegisterHotSwapListener" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` confirms play-mode-gated registration.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.

## Telemetry Ring Contract Clamp Record - 2026-05-17

What was wrong:
- The blackbox writer and dump path used the resolved vault buffer length directly.
- If a vault buffer for the same `BufferID` resolved larger than 300 entries, pathfinding would write/dump beyond the mandated 300-frame window.

What was done:
- Added `ResolveTelemetryRingLength`.
- `TryResolveTelemetryViews`, `WriteTelemetry`, and `TryDumpBlackBox` now clamp to `PathFunnelConstants.TelemetryFrames`.
- Dump byte count is bounded to the fixed 300-entry payload.

Cinematic cheats used:
- None. This is postmortem I/O contract hardening.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "ResolveTelemetryRingLength|TelemetryFrames|WriteTelemetry|TryDumpBlackBox|InitializeRuntimeState\\(runtimeStateBuffer, ResolveTelemetryRingLength" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` confirms blackbox ring and dump clamping.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.

## Agent Radius Clamp Telemetry Record - 2026-05-17

What was wrong:
- `TightenPortalForRadius` made malformed `AgentRadiusMeters` safe but did not expose the sanitation in `PathFunnelResult.Flags`.
- Negative radius input was also silently treated as zero.

What was done:
- Added `PathFunnelResultFlags.AgentRadiusClamped`.
- Non-finite radius now sets `NonFiniteInput | AgentRadiusClamped` and clamps to zero.
- Negative finite radius now sets `AgentRadiusClamped` and clamps to zero.

Cinematic cheats used:
- None. This is NaN-vaccination telemetry.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "AgentRadiusClamped|AgentRadiusMeters|NonFiniteInput \\| PathFunnelResultFlags.AgentRadiusClamped|radius < 0f|TightenPortalForRadius" Assets/_Project/Scripts/AI/Pathfinding` confirms radius sanitation is telemetry-visible.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.

## AUP Sidecar Clamp Truth Record - 2026-05-17

What was wrong:
- `ConvertWaypointsToAup` silently converted only the prefix when `WaypointAups.Length` was smaller than the produced waypoint count.
- Result consumers could see a larger `WaypointCount` with no flag showing the optional deterministic AUP sidecar was incomplete.

What was done:
- Added `PathFunnelResultFlags.AupOutputClamped`.
- `ConvertWaypointsToAup` now compares `WaypointAups.Length` against the safe waypoint count and sets the flag when the sidecar is undersized.
- The job still writes only valid prefix entries and allocates nothing.

Cinematic cheats used:
- None. This is deterministic output truth hygiene.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "AupOutputClamped|ConvertWaypointsToAup|safeWaypointCount|WaypointAups\\.Length" Assets/_Project/Scripts/AI/Pathfinding` confirms AUP sidecar clamp telemetry.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.

## Portal Input Clamp Truth Record - 2026-05-17

What was wrong:
- `FunnelSmoothingJob` clamped `PortalCount` to `Portals.Length` and then used the clamped count for completion truth.
- A caller requesting more portals than the native buffer held could receive `Complete` after only the available prefix was processed.
- A negative portal count could collapse to a direct start-goal path instead of failing as malformed input.

What was done:
- Added `PathFunnelResultFlags.PortalInputClamped`.
- Negative `PortalCount` now returns `PathFunnelStatus.InvalidInput`.
- Positive portal counts with missing or empty `Portals` buffers return `InvalidInput`.
- Partial-lookahead status now compares `portalLimit` against the caller-requested portal count, not the clamped native buffer length.
- Truncated positive buffers now use the last available portal midpoint as the effective goal and mark partial/truncated telemetry.

Cinematic cheats used:
- None. This is correctness at the corridor boundary.

Exact microseconds saved:
- 0 measured. No profiler or Burst Inspector timing was collected.

Verification:
- `rg -n "PortalInputClamped|ResolveRequestedPortalCount|PortalCount < 0|requestedPortalCount|ResolvePortalCount\\(" Assets/_Project/Scripts/AI/Pathfinding` confirms malformed/truncated portal input handling.
- Hard-ban pathfinding scan returns no forbidden hot-path patterns.
- No `dotnet build` was run on this pass per user instruction.

Integrator note:
- Current compile evidence remains unchanged: Core green from the prior log, full Assembly-CSharp blocked by RealtimeCSG missing source files with zero pathfinding matches.
