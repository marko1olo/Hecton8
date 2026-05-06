# System Reality Matrix

Date: 2026-05-07
Status: PENDING VERIFICATION

| System | Reality State | Readiness | Audit Read |
|---|---|---:|---|
| Bootstrap and initialization | Mixed | 45% | Real bootstrap code exists, but authority is split between `GameBootstrapper`, `SceneBootstrap`, and scene-owned runtime composition. |
| Scene flow / build settings | Real but messy | 68% | Build settings match intended flow. Scene asset surface contains extra sandbox/orbit/debug artifacts. |
| GlobalRegistry / service locator | Real | 74% | Widely used and central. Not cleanly sovereign because singleton residue remains everywhere. |
| Tick / dispatcher cadence | Real | 72% | `SystemDispatcher` is a meaningful runtime owner. Architecture docs undersell how much native Unity loop surface still exists. |
| Zero-GC policy | Mixed | 57% | Strong local excellence exists. Global enforcement does not. |
| Jobs / Burst adoption | Real | 64% | Large native compute footprint is genuine. Many `.Complete()` barriers dilute the benefit. |
| DOTS / Entities backend | Mostly paper | 18% | `Hecton8.World.Dots.asmdef` exists, but Entities package is not in active manifest and backend code is stubbed. |
| Save system | Real | 76% | Serious implementation with binary/native/integrity intent. Needs runtime validation, but not paperware. |
| Audio / DSP stack | Real | 73% | Serious custom implementation exists. Still mixed with legacy/static instance patterns. |
| Player movement / suit gameplay core | Real but overloaded | 55% | Large implementation exists, but owner complexity and dependency sprawl are severe. |
| Procedural world scatter / vegetation | Real but dangerous | 61% | Massive implementation surface exists. Owner files are now too large to trust cheaply. |
| Fauna / ecosystem | Real but integration-heavy | 58% | Not fake. High coupling and barrier risk remain. |
| UI / HUD runtime | Real and comparatively mature | 79% | Strongest evidence of deliberate polish-oriented engineering discipline. |
| Event bus architecture | Mixed | 62% | Deferred queue-backed buses exist, but project also carries static/event hybrid behavior. |
| Addressables / streaming discipline | Partial | 43% | Some real usage exists. Release/load governance is not yet convincing at project scale. |
| Object pooling discipline | Partial | 46% | Pooling systems exist, but project-wide purity is not believable yet. |
| Graphics / URP governance | Real policy, partial proof | 60% | Correct URP tier is active. Live frame proof is absent. |
| Documentation system | Real but stale-prone | 63% | Volume and intent are strong. Trust must be earned against current code and editor state. |
| Automated testing | Near-paper | 8% | Only a trivial package doc-example test was present in edit mode. |

## Implemented Versus Paper

Implemented in code with real weight:
- registry backbone
- dispatcher cadence
- custom save system
- procedural audio stack
- major procedural/world compute systems
- zero-GC HUD formatting discipline

Implemented, but architecturally compromised:
- bootstrap
- player runtime core
- fauna/world directors
- event infrastructure
- pooling/streaming discipline

Mostly on paper or weakly materialized:
- production DOTS backend
- strong automated regression harness
- project-wide purity of the anti-singleton mandate
- project-wide enforcement of no-coroutine/no-Update/no-hot-path-complete rules

## Free Critique

Praise where earned:
- This codebase contains real work. The team did not fake complexity with diagrams only.
- Some systems show senior-level intent and technical muscle.

Criticism where required:
- The project is carrying too many â€œimportantâ€ runtime owners at once.
- Large files are not a badge of seriousness anymore. They are now a reliability tax.
- Docs frequently describe the target architecture more cleanly than the code actually obeys it.
- DOTS currently functions more as a future-facing story than a live delivery asset.

## 2026-05-01 Reality Delta

Source-backed update after the May 1 audit pass:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` now provides the conceptual system classification layer: load-bearing, active/transitional, presentation/support, experimental/seam, and historical/evidence.
- Current first-party script surface is superseded by the May 4 sweep: `1078` C# files under `Assets/_Project/Scripts`, `519952` lines by PowerShell line count.
- `Docs/Reports/DOOMSDAY_FLAW_REPORT.md` is now the current high-risk failure map. It supersedes softer claims that event, headless, AUP, and job-safety risks were only theoretical.
- Event buses are no longer accurately described as having no breaker at all. `SystemDispatcher` contains `MaxLateFrameEventsPerFrame = 1000` and many static event lanes call `TryConsumeLateFrameEventDispatch()`. `HectonEventBus` also contains `MaxDispatchDepth = 4`. The remaining risk is narrower: no proven same-frame generation split across all event lanes and no runtime cascade proof.
- The original `FaunaBrain.UpdateBioluminescentHypnosis()` camera-specific headless violation is stale by current source recheck: the method now consumes `PlayerRuntimeContext.LookState`. Fauna headless proof is still absent because perception still uses player Transform/Rigidbody paths and no no-camera Play Mode test was run. `StorageCrate.OpenCrate()` remains source-patched but runtime-unverified.
- The hot-lane job barrier finding is not "all Complete calls are equally bad." The older documented Severity-High cases naming `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` are stale by May 2 strict grep. Current `.Complete(` hits are dispatcher completion callbacks in `ItemCatalog.cs` / `AssetLifecycleGovernor.cs` and one explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs`.
- Coroutine eradication has improved since the older Awaitable report. May 2 strict grep found `0` `StartCoroutine(` call sites under `Assets/_Project/Scripts`.
- Object pool exhaustion policy was tightened by code inspection: spawn exhaustion returns `null` and emits `GlobalTelemetryBus.PublishPoolExhausted(...)`; runtime expansion through `InstantiatePooled` is warmup-only by current source review.
- Unity MCP console proof from the previous May 1 report pass: error query returned `0` entries. Current delta pass attempted `read_console` twice and MCP returned `no_unity_session`; therefore this update has no fresh console proof. Play Mode, GCMonitor, profiler, and runtime deadlock proof remain absent.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` is the current conceptual entry point for system ownership and active risks. It does not upgrade any readiness score and does not provide runtime proof.

Readiness adjustments implied by this delta:

| System | Previous Read | Current Correction |
|---|---|---|
| Event bus architecture | Mixed / 62% | breaker exists; cascade-depth proof still incomplete |
| Automated testing | Near-paper / 8% | broad smoke infrastructure exists, but formal proof is still weak and some harnesses still use coroutines |
| Jobs / Burst adoption | Real / 64% | Burst metadata has improved, but local frame-lane barriers remain a production stall risk |
| Headless simulation | not separately scored | must be treated as critical risk because gameplay state still depends on camera/Animator presentation |
| Documentation trust | Real but stale-prone | confirmed stale-prone; active truth must now start from the May 4 documentation sweep, May 4 source/build/guard evidence, then current-source deltas |

Conceptual corrections:

- DOTS, Networking, and some modding surfaces are not load-bearing gameplay authority yet.
- Smoke testers and verifier scripts are QA support, not production gameplay ownership.
- UI/audio/VFX are important presentation layers, but gameplay state must not depend on presentation availability or animation events.
- World/scatter/voxel/fauna systems are load-bearing and high-risk; their problem is dependency gravity, not absence.

## 2026-05-02 Reality Delta

Source/log-backed update after the documentation actuality sweep:

- Fresh local command `dotnet build .\Hecton8.Core.csproj` exited `0` with `136 Warning(s)` and `0 Error(s)` in `00:01:24.05`; latest post-restore `dotnet build .\Hecton8.Core.csproj --no-restore` rerun exited `0` with `73 Warning(s)` and `0 Error(s)` in `00:00:23.95`.
- Current first-party source inventory is superseded by the May 4 sweep: `1118` `.cs` files under `Assets/_Project`, `1078` under `Assets/_Project/Scripts`, `519952` static script lines, `325` scripts directly under `Assets/_Project/Scripts`, and `33` direct public interfaces in `GlobalRegistryContracts.cs`.
- `Packages/manifest.json` still does not declare `com.unity.entities`; strict source grep found `0` active `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem` references under `Assets/_Project/Scripts`.
- `.codex-artifacts/dotnet-Hecton8.Core-2026-05-02-after-scatter-candidate-count-clamp.log` is not zero-byte evidence; it reports `Build succeeded`, `73 Warning(s)`, `0 Error(s)`.
- `unity-batch-autonomous-registry-sweep-rerun.log` contains stale `VehicleDockingModule.ResolveColliderRuntimeId` errors, then later records `ExitCode: 0` and `Tundra build success`; do not use the stale errors alone as current compile truth.
- Play Mode, GCMonitor, profiler, scene/prefab readback, and long-run memory retention remain unverified.

STATUS: PENDING VERIFICATION
