# QUALITY_GATES.md
## SECONDARY LAYER: QUALITY CONTROL
Date: 2026-05-14
Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION
Apply only after production result exists.
Source of truth for asset specs: PROCEDURAL_ASSET_PIPELINE.md
Performance tooling: SYSTEMS_CONTRACTS.md source x-ray. `BenchmarkRunner.cs` is a target-contract label and is absent in current first-party source.

2026-05-14 current-state boundary:

- 2026-05-14 DOC_AUDIT override: `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` found the cited May 11 build artifacts absent from the current filesystem. Do not use those paths as current proof. Current R43 root `Hecton8*.csproj` no-restore CLI compile evidence is `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; this is not Unity runtime proof.
- This stable file is the acceptance-gate authority. Dated reports are evidence/counter snapshots only.
- This is a gate/checklist contract, not evidence that any asset passed.
- Do not fill or cite this document as proof without a real prefab/material/scatter profile and fresh validation output.
- Current project truth starts at `AGENTS.md`, `.agents-skills/README.md`, task-relevant mandates, `Docs/README.md`, `Docs/ARCHITECTURE/README.md`, current source, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and then older evidence reports.
- May 11 report text claimed historical Core dependency build evidence at `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, but DOC_AUDIT did not find that summary or raw log. This is not current artifact-backed proof in the workspace.
- Unity MCP, Unity Console, Play Mode, profiler, GCMonitor, scene/prefab gameplay, player build, import, frame-time, memory, and visual quality proof remain absent.
- 2026-05-14 DOC_AUDIT R43 update: current external root-project CLI compile surface is clean under single-project no-restore checks, but Quality Gates remain `PENDING VERIFICATION` because Unity import/Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene/prefab, and visual proof are still absent.

2026-05-12 permanent build gate protocol:

| Gate | Rule |
|---|---|
| build command | `dotnet build <target> --no-restore -m:2 /nr:false` |
| shared servers | run `dotnet build-server shutdown` after build attempts |
| host assumption | i5-1135G7 class 4C/8T; never saturate all cores |
| queue discipline | one active compile owner per target; do not launch parallel full builds |
| failure classification | distinguish C# diagnostics from SDK/restore/environment failures |
| proof | record command, target, exit code, warning/error count, and blocker class |
| forbidden | reporting compile success from stale logs |

Known historical blocker:

- `Hecton8.Editor.csproj` previously failed before C# with `NETSDK1004` because `Temp/obj/Hecton8.Editor/project.assets.json` was missing. That is not a C# diagnostic.

---

## WHEN TO USE THIS DOCUMENT

[REQ] Gates run only if all three exist:
  - prefab in project folder
  - working material/shader
  - active scatter profile in MapMagic

[FORBID] Create new validator if production result does not exist.
[FORBID] Treat filled checklist as equivalent to finished asset.
[FORBID] Create new AssetValidator.cs or similar editor tool
         as part of asset pipeline work.
[REQ] If AssetValidator.cs already exists in project:
      extend it. Do not create a new one.

---

## ASSET VALIDATION GATE

Run after STEP 5 in PROCEDURAL_ASSET_PIPELINE.md.
Tool: existing AssetValidator.cs if present.

### Geometry and LOD
[ ] Poly count <= category budget
[ ] LOD Group: minimum 3 levels
[ ] LOD thresholds: 0.6 / 0.15 / 0.04
[ ] CrossFade = Dithered on close transitions
[ ] LOD2 silhouette preserved
[ ] No missing mesh references

### Shader
[ ] Compiles without errors or warnings
[ ] GPU Instancing = ON
[ ] Texture samples <= 8
[ ] Triplanar projection active
[ ] Quality keywords declared (_QUALITY_MX350 / _QUALITY_HIGH)
[ ] No dynamic branch if() in runtime path

### Textures
[ ] Wrap Mode = Repeat on all maps
[ ] sRGB = On for Albedo only
[ ] BC7 for Albedo/Mask, BC5 for Normal/Detail
[ ] Max Size <= 2048 hero / 1024 scatter
[ ] Read/Write = Off
[ ] Generate Mip Maps = On

### Colliders
[ ] Collider count <= 3 per instance
[ ] No MeshCollider on LOD0
[ ] No Dynamic Rigidbody on static props
[ ] Collider type matches category table

### Instancing
[ ] GPU Instancer prototype registered
[ ] Draw Calls <= 1 per prefab type at 5k instances
[ ] Buffer Size = Auto-grow
[ ] Frustum + Occlusion Culling enabled

---

## PERFORMANCE GATE

Run before merge to main.
Tooling: BenchmarkRunner.cs defined in SYSTEMS_CONTRACTS.md.

### MX350 HARD BUDGET GATE

These budgets are hard gates for the minimum supported machine. They do not prove quality; they only prevent known bad submissions from entering main.

| Domain | MX350/i3 Limit | Blocks merge | Evidence |
|---|---:|---|---|
| Total frame time | <= 16.67ms p95 | Yes | 60s Player capture |
| Main thread | <= 12.0ms p95 | Yes | Unity Profiler |
| Gameplay physics total | <= 2.0ms p95 planning gate; <= 5.0ms absolute spike ceiling | Yes above 5.0ms; above 2.0ms requires load-shed/fake plan | Profiler markers, FixedStep capture |
| Single runtime system | <= 0.1ms unless cold/amortized | Yes | Profiler marker proof |
| GC hot path | 0 B/frame | Yes | GC Alloc column / GCMonitor |
| VRAM used | <= 1.6GB guard, <= 1.8GB hard | Yes at hard; guard breach requires load-shed evidence | Memory Profiler / platform counter |
| Texture memory | <= 900MB | Yes | Memory Profiler |
| Render targets + depth | <= 320MB | Yes | Memory Profiler / RenderDoc |
| SetPass | <= 600 target, <= 800 hard | Yes at hard | Frame Debugger / Stats |
| Batches | <= 1800 hard | Yes | Frame Debugger / Stats |
| Native persistent memory | flat over 10 min idle | Yes | NativeMemorySentinel + Memory Profiler |

Load-shed requirement:

| Trigger | Required Response |
|---|---|
| usedVRAM / totalVRAM > 0.90 | request mip downgrade, drain release queue, reduce non-primary RTs |
| frame_time > 25ms for 3 frames | reduce raymarch/post/boid/rigidbody budgets by tier order |
| physics p95 > 2.0ms | disable noncritical dynamic bodies, reduce solver scope, or replace with visual fake before accepting merge risk |
| GC hot path > 0 B | block merge until allocation source is removed |

### Thresholds (MX350)
| Metric     | Limit      | Blocks merge |
|------------|------------|--------------|
| Frame Time | <= 16.67ms | Yes          |
| Per-system tick | <= 0.1ms | Yes          |
| VRAM guard | <= 1.6GB | Risk marker; merge requires load-shed proof if exceeded |
| VRAM hard ceiling | <= 1.8GB | Yes |
| Half-Res VRAM guard | <= 1.6GB | Risk marker; half-res does not buy extra VRAM |
| SetPass    | <= 800     | Yes          |
| GC Alloc   | 0          | Yes          |
| Draw Calls | <= 1/type  | Yes          |

CTO hard rules:

- Any single runtime system tick above `0.1ms` is suspicious and blocks merge until a profiler trace proves the cost is cold, amortized, or moved out of the frame-critical lane.
- Half-Res rendering modes do not get a larger memory budget. The guard remains `1.6GB`; the hard ceiling remains `1.8GB` on MX350.
- Synchronous `Physics.BakeMesh(...)` on the main thread is forbidden. Mesh baking must be asynchronous/job-bound or replaced by a cinematic collider fake for noncritical/distant geometry.
- Any water/light/flow/pressure/deformation/cable/particle/ambience feature must document why the visual fake path is insufficient before adding runtime simulation.
- No manual checklist can override profiler, console, or PlayMode evidence.

### Test scene requirements
[REQ] 3000 instances of one type on screen.
[REQ] Flashlight active. Fog density = production value.
[REQ] Record 60 seconds. Use p95 frame time, not average.
[FORBID] Write performance_report.md manually without real benchmark run.

---

## SCATTER VALIDATION

Run after scatter profile activation in MapMagic.

[ ] Density <= 1200 instances per 1000m tile
[ ] Floor Offset Y in range -0.2 to -0.8m
[ ] Clearance >= 2.5m from player spawn coordinates
[ ] No overlap with active base modules
[ ] Yaw randomization 0-360 deg active
[ ] No floating instances

---

## SIGNOFF CHECKLIST

Final check before closing task.
Fill only if every item can be True.

[ ] Prefab exists: Assets/Prefabs/[Category]/[Name].prefab
[ ] Material exists and assigned: MAT_[Category]_[Name]
[ ] Shader compiles without errors
[ ] GPU Instancer prototype registered
[ ] MapMagic scatter profile active in active graph
[ ] LOD Group: 3+ levels, thresholds 0.6 / 0.15 / 0.04
[ ] Asset Validation Gate: all checks passed
[ ] Performance Gate: frame time <= 16.67ms on test scene
[ ] No missing references

[REQ] Empty item = task not done. State what is missing and why.
[FORBID] Submit this checklist as proof of work
         without existing prefab.
