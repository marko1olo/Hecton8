# Unity Runtime Trap Deeper Pass - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Status: SOURCE FIX / STRM RESIDUAL CLEARED IN CURRENT SOURCE / BUILD BOUNDARY
Evidence class: STATIC_SOURCE / STATIC_POLICY / BUILD_GUARD

## What Was Wrong

- `OrganicDebrisProfile.RebuildCache` still used `GetComponentsInChildren<Collider>(true)`.
- That Unity overload returns a new `Collider[]` during cache rebuild.
- `UnityApiTrapDetector` allowed `COLD ALLOC:` waivers for copied-array APIs.
- The detector could also read tooltip/string text as executable API usage.
- Runtime `Resources.Load` existed earlier in `RuntimeShaderReferenceCatalog.cs:45`.
- Local STRM mandate forbids runtime `Resources.Load` with zero exceptions.
- Current source no longer has that hit; the catalog is registered from bootstrap state.

## What Changed

- `Assets/_Project/Scripts/Gameplay/DebrisManager.cs`
  now uses owner `List<Collider>` scratch for collider discovery.
- `Assets/_Project/Scripts/Editor/UnityApiTrapDetector.cs`
  now hard-flags runtime `Resources.Load`.
- The detector no longer accepts `COLD ALLOC:` for `Renderer.sharedMaterials`,
  `Mesh.vertices` getter, or generic `GetComponents*<T>` array overloads.
- The detector strips string literals before line comments, so strings like
  URLs cannot hide real code on the same line.

## Static Proof

| Check | Result |
|---|---|
| `GetComponentsInChildren<Collider>(true)` in `DebrisManager.cs` | `0` hits |
| Runtime `Resources.Load` outside Editor folders | `0` hits |
| Runtime `mesh.vertices/triangles/uv` property setters | `0` hits |
| `RemoveAt(0)` in scripts | only `Editor/LODStatisticsWindow.cs` |
| Release-reachable runtime `Shader.Find(...)` exact scan | `0` hits |
| `git diff --check` on touched source | pass; LF/CRLF warnings only |

## Cleared Residual

| Check | Current Result |
|---|---|
| Runtime `Resources.Load` scan | `0` hits |
| `GameBootstrapper` serialized catalog field | present |
| `RuntimeShaderReferenceCatalog.Register(...)` route | present |
| Catalog asset scene binding | `00_BOOTSTRAP.unity:598` references GUID `66443d0a1f184aef87c6fd729fd8f401` |

Why this was not claimed as my source fix:

- The source changed during the pass under concurrent agent work.
- Current proof supports the route, but attribution is not assigned here.

Residual caveat:

- Unity import, scene load, and player-build shader inclusion were not run.
- Keep import/boot validation on the route before calling this player-proven.
- Keep `TryGet*` accessors pure.

## Build Boundary

- Initial build guard: `BUILD_UNKNOWN_RUNTIME_TRAP_DEEPER_PASS_20260526.log`.
- Retry guard: `BUILD_UNKNOWN_RUNTIME_TRAP_DEEPER_PASS_RETRY_20260526.log`.
- Final guard: `BUILD_UNKNOWN_RUNTIME_TRAP_DEEPER_PASS_FINAL_GUARD_20260526.log`.
- Those earlier attempts did not launch: CPU stayed above the allowed `50%`
  threshold for `30` retry attempts and `10` final attempts.
- Post-scanner recheck log:
  `BUILD_UNKNOWN_RUNTIME_TRAP_DEEPER_PASS_POST_SCANNER_RECHECK_20260526.log`.
- Post-scanner guard launched legally at CPU `48`, compiler process count `0`.
- Build exit code: `1`.
- Boundary: `62` `MSB3202` errors for missing Unity-generated `.csproj` files.
- The build did not reach C# compilation, so this pass has no compile-green proof.

## Runtime Proof Missing

No Unity import, Console, Play Mode, player build, shader import, profiler,
GCMonitor, Memory Profiler, scene wiring, visual, or platform gate was run.

## Documentation Gate

- `VerifyDocStructure.py`: `pass=true`, `activeDocCount=666`.
- `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=666`.
- Two existing architecture paragraphs were split into contract bullets without
  changing facts.
- `OOP_Doc_Scanner.py` now skips files that vanish during inventory and
  secondary architecture/word-count reads.
  This protects the gate from concurrent deletes without restoring them.
- Two concurrently touched marketing/data docs were converted back to UTF-8 BOM.

## Microseconds Saved

Runtime: `0 us` claimed. This pass is static route cleanup and detector
hardening, not measured frame-time optimization.
