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
