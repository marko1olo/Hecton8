# 2026-05-03 Registry Service/Renderable And Job Barrier Guard
Date: 2026-05-07

Status: PENDING VERIFICATION

## 2026-05-04 Supersession Note

This report is May 3 source/build evidence. Current guard truth is the May 4 post-repair `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` plus regenerated `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`: exit `0`, `UnsafeUtility.MemCpy outside guard = 0`, unauthorized Unity loop methods `0`, `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, runtime Find API review hits `8`, and global registry self-registration inventory `500`. Treat May 3 guard-clean inventory below as historical.

## Mandates Followed

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`

## What Was Wrong

`GlobalRegistry.Renderables.Register(this)` still had blind success flags in three runtime owners.
The broader service scan also found blind `_isInitialized = true` flags after `RegisterDebrisService(this)` and `RegisterPhysicsService(this)`.
These are the same truth-state defect as blind tick registration: if the registry rejects registration because of capacity, duplicate state, null state, or future guard behavior, the local flag can claim ownership that does not exist in the authoritative registry.

`HectonMapMagicVegetationBridge` used one `_eventsSubscribed` flag for two different contracts: `TerrainTile` static events and `HectonFloatingOrigin` listener ownership. That made floating-origin listener state dependent on a local flag instead of the authoritative origin listener registry.

The project also had no repeatable source guard for this regression class. Manual `rg` sweeps found the issue, but future agents could reintroduce the same pattern silently.

The previous job-barrier source inventory had synchronous `.Run(` sites. The current guard inventory reports `0` source `.Run(` hits under `Assets/_Project/Scripts/**/*.cs`; future reintroductions are guard failures and must be replaced with scheduled jobs, bounded direct kernels, or documented cold async lanes.

## What I Did

- Added `Tools/ReloadAudit/Scan-FoundationGuards.ps1`.
- Generated `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`.
- Changed renderable registration flags to truth-state reads:
- `HectonUnderwaterVisuals`: `_registeredRenderable = GlobalRegistry.Renderables.Contains(this);`
- `HectonSubmarineOS`: `_registeredRenderable = GlobalRegistry.Renderables.Contains(this);`
- `MissionMarkerSystem`: `_registeredRenderable = GlobalRegistry.Renderables.Contains(this);`
- Expanded the guard to scan broad `GlobalRegistry.Register*(...this...)` service registrations, not only dispatcher/renderable registrations.
- Changed service initialization flags to slot ownership reads:
- `DebrisManager`: `_isInitialized = ReferenceEquals(GlobalRegistry.Debris, this);`
- `PhysicsApplySystem`: `_isInitialized = ReferenceEquals(GlobalRegistry.Physics, this);`
- Guarded their teardown unregister calls with the same slot ownership check.
- Split `HectonMapMagicVegetationBridge` event state from floating-origin listener state.
- Changed floating-origin listener ownership to `_originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);` after registration.
- Expanded the guard to hard-fail `HectonFloatingOrigin.RegisterListener(this); _flag = true` blind flags.
- Expanded the guard to inventory direct `InputManager.Instance` access and hard-fail any source path classified as hot cadence.
- Expanded the guard to hard-fail broad physics layer masks outside Editor code: `LayerMask=-1`, `~0`, or direct all-layer literals in `Physics.*` / `RaycastCommand` lines.
- Moved `FloraInteractionManager` cascade phase seed recompute to a scheduled `IJobParallelFor` lane completed during `SystemDispatcher` late-frame swap before GPU buffer upload.
- Promoted synchronous job `.Run(` sites from source inventory to hard guard failures after first-party runtime source reached `0` hits.

## Guard Output

Current generated source scan:

| Guard | Count |
|---|---:|
| Global registry self-registration sites | 495 |
| Blind registry flag drift | 0 |
| Origin shift listener blind flag drift | 0 |
| Synchronous job `.Run(` sites | 0 |
| Hot-path synchronous job `.Run(` review sites | 0 |
| Completion `.Complete(` text hits | 1 |
| Direct raw-array listener dispatch | 0 |
| GlobalRegistry.Input nullable misuse | 0 |
| Direct `InputManager.Instance` sites | 0 |
| Hot-path direct `InputManager.Instance` review sites | 0 |
| Optimization singleton residue | 0 |
| Unauthorized Unity loop methods | 0 |
| Legacy coroutine sites | 0 |
| Forbidden runtime asset API sites | 0 |
| Release-reachable direct hot-path Debug.Log sites | 0 |
| Release-reachable one-hop Debug.Log review sites | 0 |
| Broad physics layer masks outside Editor | 0 |
| Runtime Find API text hits outside Editor folder | 0 |

`Blind registry flag drift`, `Origin shift listener blind flag drift`, synchronous job `.Run(` sites, direct raw-array listener dispatch, `GlobalRegistry.Input` nullable misuse, hot-path direct `InputManager.Instance` access, optimization singleton residue, unauthorized Unity loop methods, legacy coroutine sites, forbidden runtime asset API sites, release-reachable direct hot-path `Debug.Log` sites, and broad physics masks are hard failures in the guard script.
`.Complete(` remains an inventory signal. Current `.Run(` source inventory is `0`; any future `.Run(` reintroduction is a blocking source defect before owner-level dispatcher-window review.
Release-reachable one-hop `Debug.Log` review sites are conservative source-classifier inventory, not a hard gate until owner-level review proves direct gameplay cadence.

## Job Barrier Classification

| Owner | Current static reading | Action |
|---|---|---|
| `Assets/_Project/Scripts/**/*.cs` | Current source guard reports `0` `.Run(` hits and treats non-zero as exit-code failure. | Keep the guard in CI and reject reintroduced inline job execution before owner-level dispatcher review. |
| Completion callbacks | Current `.Complete(` inventory contains the guarded `DispatcherJobSwap.TryComplete` source-level pattern. | Do not classify this as a Unity `JobHandle.Complete()` stall without owner/runtime evidence. |

## Evidence

- Guard command: `Tools/ReloadAudit/Scan-FoundationGuards.ps1`
- Guard output: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- Guard status: `Blind registry flag drift = 0`, `Origin shift listener blind flag drift = 0`, synchronous job `.Run(` sites `0` hard gate clear
- May 3 guard hard zeroes at report time: raw `UnsafeUtility.MemCpy` outside guard `0`, legacy `PlayerSignalEvents.On*` subscriptions `0`, direct raw-array listener dispatch `0`, `GlobalRegistry.Input` nullable misuse `0`, hot-path direct `InputManager.Instance` review sites `0`, optimization singleton residue `0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, broad physics layer masks outside Editor `0`, runtime Find API text hits outside Editor folder `0`. Current May 4 guard truth is listed in the supersession note.
- Build command: `dotnet build Hecton8.Core.csproj -v:minimal -nr:false -m:1 -p:UseSharedCompilation=false`
- Build log: `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-foundation-guard-physicsmask.log`
- Build result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- Warning cleanup: removed dead `UIAudioFeedback` pitch-variation inspector fields because `IAudioService.PlayStatic2D` has no pitch parameter.
- Diff check: `git diff --check` on touched files returned exit code `0`; only CRLF normalization warnings were printed.

No Unity Play Mode was launched.
No MCP console log was available.
No GCMonitor or profiler numbers were captured.

## Regression Model

CPU: renderable/service/origin-listener registration now performs O(N) bucket containment or constant ownership checks only on lifecycle registration/teardown, not per-frame. This is cold/lifecycle cost.

GC: no new managed allocation is introduced in gameplay hot paths. The guard script and generated report are tooling/docs only.

Memory: no runtime buffers, textures, native containers, scene assets, or prefab assets were changed.

Cadence: renderable draw cadence, physics/debris service cadence, and floating-origin rebase cadence are unchanged. Flora cascade phase seed publication now waits for late-frame job completion before GPU upload instead of using a synchronous job-run barrier.

Correctness: local `_registeredRenderable`, `_isInitialized`, and `_originShiftListenerRegistered` flags now mirror authoritative registry/listener ownership. If registration fails or ownership changes before teardown, unregister will not claim a registration that never existed or no longer belongs to the instance.

## Failure Modes

- The guard script only scans source text; it cannot prove registry capacity under scene load.
- No source `.Run(` inventory remains and the guard now fails on reintroduction, but local job scheduling/completion decisions can still hide runtime stalls until profiler evidence exists.
- Future registration APIs outside the current broad `GlobalRegistry.Register*(...this...)`, `GlobalRegistry.Renderables.Register(this)`, and `HectonFloatingOrigin.RegisterListener(this)` patterns need explicit guard expansion.
- Dirty worktree state means this report is not a clean-PR diff boundary.

STATUS: PENDING VERIFICATION
