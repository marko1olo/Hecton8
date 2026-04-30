# System Reality Matrix

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
- The project is carrying too many “important” runtime owners at once.
- Large files are not a badge of seriousness anymore. They are now a reliability tax.
- Docs frequently describe the target architecture more cleanly than the code actually obeys it.
- DOTS currently functions more as a future-facing story than a live delivery asset.

## 2026-05-01 Reality Delta

Source-backed update after the May 1 audit pass:

- Current first-party script surface under `Assets/_Project/Scripts`: `1020` C# files, `466768` lines by PowerShell line count.
- `Docs/Reports/DOOMSDAY_FLAW_REPORT.md` is now the current high-risk failure map. It supersedes softer claims that event, headless, AUP, and job-safety risks were only theoretical.
- Event buses are no longer accurately described as having no breaker at all. `SystemDispatcher` contains `MaxLateFrameEventsPerFrame = 1000` and many static event lanes call `TryConsumeLateFrameEventDispatch()`. The remaining risk is narrower: no proven same-frame generation split across all event lanes, and `HectonEventBus` tracks dispatch depth without a hard max-depth cap.
- The strongest newly verified headless violations are `FaunaBrain.UpdateBioluminescentHypnosis()` using `runtimeContext.PlayerCamera` as gameplay truth and `StorageCrate.OpenCrate()` depending on an Animator event to leave `Opening`.
- The hot-lane job barrier finding is not "all Complete calls are equally bad." The currently documented Severity-High cases are `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick`.
- Coroutine eradication is partial, not complete. `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md` reports 15 remaining `StartCoroutine(` call sites, concentrated in runtime smoke/verifier harnesses.
- Object pool exhaustion policy was tightened by code inspection: spawn exhaustion returns `null` and emits `GlobalTelemetryBus.PublishPoolExhausted(...)`; runtime expansion through `InstantiatePooled` is warmup-only by current source review.
- Unity MCP console proof from the previous May 1 report pass: error query returned `0` entries. Current delta pass attempted `read_console` twice and MCP returned `no_unity_session`; therefore this update has no fresh console proof. Play Mode, GCMonitor, profiler, and runtime deadlock proof remain absent.

Readiness adjustments implied by this delta:

| System | Previous Read | Current Correction |
|---|---|---|
| Event bus architecture | Mixed / 62% | breaker exists; cascade-depth proof still incomplete |
| Automated testing | Near-paper / 8% | broad smoke infrastructure exists, but formal proof is still weak and some harnesses still use coroutines |
| Jobs / Burst adoption | Real / 64% | Burst metadata has improved, but local frame-lane barriers remain a production stall risk |
| Headless simulation | not separately scored | must be treated as critical risk because gameplay state still depends on camera/Animator presentation |
| Documentation trust | Real but stale-prone | confirmed stale-prone; active truth must now start from May 1 reports and current-source deltas |

STATUS: PENDING VERIFICATION
