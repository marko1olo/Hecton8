# QUALITY_GATES.md
## SECONDARY LAYER: QUALITY CONTROL
Date: 2026-05-11
Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION
Apply only after production result exists.
Source of truth for asset specs: PROCEDURAL_ASSET_PIPELINE.md
Performance tooling: SYSTEMS_CONTRACTS.md (BenchmarkRunner.cs)

2026-05-11 current-state boundary:

- This stable file is the acceptance-gate authority. Dated reports are evidence/counter snapshots only.
- This is a gate/checklist contract, not evidence that any asset passed.
- Do not fill or cite this document as proof without a real prefab/material/scatter profile and fresh validation output.
- Current project truth starts at `AGENTS.md`, `.agents-skills/README.md`, task-relevant mandates, `Docs/README.md`, `Docs/ARCHITECTURE/README.md`, current source, and then May 11 evidence reports.
- Current completed full Core dependency build in the active documentation boundary is `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, and `CS_WRITES_AFTER_END=0`.
- Unity MCP, Unity Console, Play Mode, profiler, GCMonitor, scene/prefab gameplay, player build, import, frame-time, memory, and visual quality proof remain absent.

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

### Thresholds (MX350)
| Metric     | Limit      | Blocks merge |
|------------|------------|--------------|
| Frame Time | <= 16.67ms | Yes          |
| Per-system tick | <= 0.1ms | Yes          |
| VRAM       | <= 1.6GB   | Yes          |
| Half-Res VRAM cap | <= 1.6GB | Yes          |
| SetPass    | <= 800     | Yes          |
| GC Alloc   | 0          | Yes          |
| Draw Calls | <= 1/type  | Yes          |

CTO hard rules:

- Any single runtime system tick above `0.1ms` is suspicious and blocks merge until a profiler trace proves the cost is cold, amortized, or moved out of the frame-critical lane.
- Half-Res rendering modes do not get a larger memory budget; VRAM remains capped at `1.6GB`.
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
