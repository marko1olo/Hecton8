# Status 2202 - Underwater Plane/Slab Offender Resolver

Evidence class: STATIC VERIFIED only. No Unity slot, no Play Mode, no scene save, no build, no deletion.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `terrain.md`
- `water.md`
- `world.md`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Governing Mandates

- Surface, photic shallows, and medium-depth hero routes must stay bright, readable, beautiful, and materially rich; darkness cannot hide weak geometry.
- Flat planes, primitive slabs, random sheets, and placeholder/proxy terrain are rejected as production visuals.
- Static source proof does not equal active renderer proof; Unity-owner captures are required before acceptance.
- Visual fakes are allowed only when premium and believable; cheap-looking planes, curtains, and water shells fail even if fast.
- Any disable/removal route requires reference proof, rollback path, and before/after route screenshots.
- `GlobalQualityWeight` may scale visual density/cadence only; layer/culling behavior cannot change gameplay truth.
- Terrain/water acceptance requires route readability, material truth, compact/high proof, and no generic blue fog or empty black void.
- No broad cleanup. No deletion. First pass is renderer/layer/material isolation with proof.

## Static Work Completed

- Inspected `h8_1472_underwater_0_5m.png` and `h8_1472_underwater_20_50m_route.png`.
- Parsed `Assets/_Project/Scenes/02_HECTON_WORLD.unity` for plane/slab/sheet/curtain/ceiling/rib/ribbon/mass/occlusion/noir/foam/water/underwater/shell/proxy names.
- Extracted candidate GameObject active state, renderer enabled state, transform, mesh reference, and material GUID from YAML.
- Cross-checked named offenders against `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.csv`.
- Inspected material YAML for top suspects.
- Checked persistence across existing 1469-1472 underwater screenshots by static image color scan: pale/yellow horizontal rows persist in 1469, 1470, 1471, and 1472 underwater captures.

## Current Finding

Top active pale-sheet suspect is `H8_DEPTH_LOW_SHELF_1428`: active GameObject, active renderer, built-in primitive cube mesh, position `x:0 y:-0.9 z:30`, scale `x:58 y:1.15 z:8`, material `MAT_H8_SurfaceLittoralShelf_1430` with beige opaque base color `{r:0.68,g:0.64,b:0.5,a:1}`. This matches the visible pale/yellow horizontal slab class in 1472.

## Proof Packet

- `Docs/Reports/Batch22/2202_UNDERWATER_PLANE_SLAB_OFFENDER_MATRIX.md`
- `Docs/Reports/Batch22/2202_UNDERWATER_PLANE_SLAB_OFFENDER_MATRIX.csv`
- `Docs/Orchestration/UNITY_OWNER_HANDOFF_2202_UNDERWATER_SLABS.md`
- `Docs/AgentLogs/Rationale_2202.md`
- `Docs/AgentLogs/LOG_2202.md`

## Verification State

PENDING VERIFICATION: Unity-owner scene hierarchy inspection, layer/camera mask inspection, renderer isolation, and before/after screenshots at `0.5m` and `20-50m route`.
