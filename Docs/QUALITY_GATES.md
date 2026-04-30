# QUALITY_GATES.md
## SECONDARY LAYER: QUALITY CONTROL
Status: SECONDARY
Verification: PENDING VERIFICATION
Apply only after production result exists.
Source of truth for asset specs: PROCEDURAL_ASSET_PIPELINE.md
Performance tooling: SYSTEMS_CONTRACTS.md (BenchmarkRunner.cs)

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
| VRAM       | <= 1.6GB   | Yes          |
| SetPass    | <= 800     | Yes          |
| GC Alloc   | 0          | Yes          |
| Draw Calls | <= 1/type  | Yes          |

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
