# QUALITY_GATES.md

Date: 2026-05-26
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC

Purpose: acceptance gates. This file defines what proof is required; it is not proof that anything passed.

## Authority Boundary

- Read `Docs/PROJECT_BASELINE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, and current source before using these gates.
- Root `quality.md` owns cross-domain proof language and screenshot/profiler review doctrine. This file owns executable hard gates and acceptance checklists.
- Current proof snapshots live in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Dated reports and archives are historical evidence only.
- A filled checklist is not proof without a current artifact path.

## Universal Proof Rules

- Static source proof does not prove runtime behavior.
- Compile proof does not prove Unity import, Console, Play Mode, profiler, GC, player build, save/load, shader, visual, or platform readiness.
- Runtime claims require artifact paths with command or capture context, timestamp, target, exit/result, and blocker class.
- Do not report `VERIFIED`, `COMPLETE`, or `PRODUCTION READY` from stale logs.

## Build Gate Protocol

| Gate | Rule |
|---|---|
| command | record exact build command, target, timestamp, exit code, warning/error count |
| shared servers | use non-shared compiler/server settings when required by task policy |
| queue discipline | one active compile owner per target; no parallel full builds |
| failure class | distinguish C# diagnostics from SDK/restore/environment failures |
| forbidden | claiming compile success from stale logs |

## Runtime Evidence Gates

| Gate | Required evidence | Blocks readiness claim |
|---|---|---|
| Unity import / Console | Unity log or Console export | Yes |
| Play Mode or player | run/capture log | Yes |
| Profiler | frame-time capture with target scene/load | Yes |
| GC | GC Alloc / GCMonitor / Memory Profiler artifact | Yes |
| Memory | Memory Profiler or platform memory capture | Yes |
| Rendering | Frame Debugger, RenderDoc, screenshot, GPU timing, or shader import proof | Yes |
| Save/load | write/read/migration/checksum failure artifacts | Yes |
| Platform | build artifact plus device/runtime proof for target platform | Yes |

## Native Memory and DataVault Gate

| Gate | Command / proof | Blocks merge |
|---|---|---|
| no new direct native allocation debt | `Tools\DataVaultSovereigntyAudit.py --fail-on-regression` or current successor | Yes |
| no new runtime native field debt | runtime/native ownership ledger or successor scanner | Yes |
| no duplicate central `BufferID` values | `Tools\BufferIDSovereigntyAudit.py --fail-on-duplicates` | Yes |
| no local numeric `BufferID` casts at final migration | `Tools\BufferIDSovereigntyAudit.py --fail-on-local-casts` | Yes when declared complete |
| native owner proof | owner, allocator, lifetime, disposal, phase, failure mode | Yes |

Rules:

- `H8Memory.cs` and `GlobalDataVault.cs` are the approved central native ownership surfaces.
- Private persistent native collection fields elsewhere are migration debt unless proven owner-local scratch.
- `MonoBehaviour` native fields need explicit disposal and lifecycle proof; otherwise they are debt.
- The zero-debt gate is expected to fail until remaining migrations complete. New regressions are not accepted.

## Job and Burst Gate

| Gate | Required state | Blocks merge |
|---|---|---|
| hot path GC | `0 B/frame` by profiler/GC artifact | Yes |
| same-frame schedule/readback | profiler proof or removal | Yes |
| hidden `.Complete()` | owner-dispatcher completion window or proof | Yes |
| Burst payload | unmanaged, no managed references, no captured lambdas | Yes |
| Unity object access from jobs | absent | Yes |
| deterministic math | no gameplay-authority `UnityEngine.Random` or wall-clock time | Yes |

## Global Authority Gate

| Gate | Rule | Blocks merge |
|---|---|---|
| registry | no new hot `GlobalRegistry.Get<T>` / `TryGet<T>` polling | Yes |
| first-party hot events | no new hot `HectonEventBus` traffic | Yes |
| SignalBus | new routes use typed `SignalBus<T>` with owner/capacity/overflow/telemetry | Yes |
| GlobalSignals | no new direct queue surface unless explicit bridge migration | Yes |
| DataVault | no new global native buffer without owner/lifetime route | Yes |
| route card | new or changed global route has a complete route card | Yes |
| review | route review disposition is `GREEN` | Yes |

## Performance Gate

Compact 2GB-VRAM / 8GB-RAM class hardware is the minimum supported proof lane.

Root `AGENTS.md` guardrails are the target standard: main thread `<= 12ms`, GC `0 B/frame`, SetPass `<= 600`, batches `<= 1800`, total memory `<= 4096MB`, compact VRAM hard ceiling `<= 1800MB`, texture memory `<= 900MB`, and render targets plus depth `<= 320MB`. Any temporary emergency threshold below must be named as a blocker ceiling, not a success target.

| Metric | Limit | Blocks merge | Evidence |
|---|---:|---|---|
| Total frame time | `<= 16.67ms p95` | Yes | 60s player/profiler capture |
| Main thread | `<= 12.0ms p95` | Yes | Unity Profiler |
| Single runtime system | `<= 0.1ms` unless cold/amortized | Yes | Profiler marker proof |
| Gameplay physics total | `<= 2.0ms p95` planning gate; `<= 5.0ms` spike ceiling | Yes above spike ceiling | Profiler/FixedStep capture |
| GC hot path | `0 B/frame` | Yes | GC Alloc / GCMonitor |
| VRAM guard | `<= 1.6GB` | Risk marker | Memory Profiler / platform counter |
| VRAM hard ceiling | `<= 1.8GB` | Yes | Memory Profiler / platform counter |
| Texture memory | `<= 900MB` | Yes | Memory Profiler |
| Render targets + depth | `<= 320MB` | Yes | Memory Profiler / RenderDoc |
| SetPass target | `<= 600` | Yes above emergency ceiling; otherwise route risk | Frame Debugger / Stats |
| SetPass emergency ceiling | `<= 800` hard blocker ceiling | Yes | Frame Debugger / Stats |
| Batches target | `<= 1800` | Yes above target without owner route and profiler proof | Frame Debugger / Stats |
| Total memory target | `<= 4096MB` | Yes above target without memory owner proof | Memory Profiler / platform counter |
| Native persistent memory | flat over 10 min idle | Yes | NativeMemorySentinel / Memory Profiler |

Load shed:

- VRAM above guard: request mip downgrade, drain release queue, reduce non-primary render targets.
- Frame time above `25ms` for 3 frames: lower quality weight target and reduce raymarch/post/boid/rigidbody budgets by owner order.
- Physics p95 above `2.0ms`: reduce solver scope or replace noncritical work with a visual fake.
- GC hot path above `0 B`: block until allocation source is removed.

## Asset Validation Gate

Run only after the production prefab, material/shader, and active scatter/profile route exist.

Geometry and LOD:

- Poly count within category budget.
- LOD Group has at least 3 levels.
- LOD thresholds target `0.6 / 0.15 / 0.04`.
- No missing mesh references.
- No noncritical dynamic Rigidbody on static props.

Shader and texture:

- Shader compiles without errors.
- GPU instancing enabled where applicable.
- Texture samples within budget.
- Runtime quality input is continuous (`GlobalQualityWeight`, material scalar, or source-required ABI).
- Albedo/mask/normal formats and mip settings match platform plan.

Scatter:

- Density within tile budget.
- Floor offset and clearance validated.
- No overlap with active base modules.
- Yaw randomization and placement constraints validated.
- No floating instances.

## First 20 Minutes Product Gate

| Gate | Rule | Blocks merge |
|---|---|---|
| route relevance | every product/runtime/content task names the route moment it improves | Yes |
| route blocker | off-route work names the blocker it removes | Yes |
| proof package | route claims require runtime, profiler, GC, memory, save/load, screenshot/clip proof | Yes |
| breadth control | net-new systems not needed by the route are parked | Yes |
| marketing send | no public send-ready state without real screenshot/clip/demo proof | Yes |

## Signoff Rule

An unchecked item means the task is not done. State what is missing and why.

Do not submit this file, or a copied checklist from it, as proof of work.
