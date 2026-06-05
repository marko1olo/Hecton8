# 2012 Unity Slot Queue

Status: STATIC QUEUE / SINGLE UNITY OWNER ONLY / NO UNITY RUN BY 2012

Owner name override: `Продолжить работу по логам`. If an older owner line in this file shows mojibake, ignore it and use this override.

Current Unity owner name: `Продолжить работу по логам`.

Do not start Unity-slot work until that owner explicitly hands over. No other Batch20 item may control Unity concurrently.

## Queue Rules

- Proof artifacts go under `Docs/Reports/Batch20/VisualProof/...`, not `Assets`.
- Unity owner must check compile/import state and CPU/build contention before starting.
- Do not run builds unless a later explicit task requires it.
- Do not edit Crest/vendor assets.
- Do not delete Assets or `.meta` files during this queue without scoped reference proof, replacement route, rollback path, and import verification.
- Stop after 3 failed owner-caused compile/import repair attempts and mark `[BLOCKED BY DEPENDENCY]`.

## Slot 1 - Baseline Capture Before Repair

Inputs:
- `taskslocal/batch20_unity_slot_visual_proof_and_scene_repair/2001_UNITY_SLOT_VISUAL_PROOF_CAPTURE_AND_TRIAGE.txt`
- `Docs/Reports/Batch20/UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md`
- `Docs/Reports/Batch20/unity_visual_proof_capture_shotlist_20260604.csv`
- `Docs/Reports/Batch20/2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.csv`
- `Docs/Reports/Batch20/2007_RISK_LEDGER.csv`

Required proof:
- Game View / Scene View matching pair from same pose.
- Shoreline closeup at player inspection distance.
- 0-5 m photic shallows.
- 20-50 m medium depth.
- Aegir long shot.
- Aegir crop showing texture/cloud/storm quality.
- 360 sky pan sequence with sun, clouds, moons, Aegir, horizon, coastline, ocean.
- Low/Middle/High/Ultra `GlobalQualityWeight` consequences as continuous behavior notes.

Stop if:
- Unity slot is not free.
- Proof path targets `Assets`.
- Scene or Game View is too dark/muddy to evaluate surface/shallow art.

## Slot 2 - ProductFace Primitive And Null Material Relink

Inputs:
- 2002 rows `B20-2002-001`, `B20-2002-002`, `B20-2002-009`, `B20-2002-012`.
- `Docs/Reports/Batch20/PRODUCT_FACE_UNITY_OWNER_RELINK_CHECKLIST_20260604.md`
- `Docs/Reports/Batch20/product_face_source_manifest_draft_20260604.csv`

Required proof:
- Active scene object/material relink report.
- ProductFace validator output.
- Before/after captures for visible repaired objects.
- Frame Debugger material/renderer sanity for product-face route objects.

Stop if:
- Channel contract is undefined.
- Replacement source is missing.
- Relink would overwrite another owner's active edit.

## Slot 3 - Placement Rule Repair

Inputs:
- `Docs/Reports/Batch20/2003_KELP_ROCK_DRY_LAND_PLACEMENT_SPEC.md`
- `Docs/Reports/Batch20/2003_RULE_MATRIX.csv`
- `Docs/Reports/Batch20/2003_CANDIDATE_RULE_PATCHES.diff.txt`
- Accepted BioForge/Flora and GeologyForge bake manifests.

Required proof:
- Rule diff or serialized change report.
- Dry-land/surface edge before/after capture.
- Photic shallows before/after capture.
- Overdraw/profiler proof if density changes.

Stop if:
- Proposed repair deletes shallow ecology instead of fixing submerged/substrate gates.
- `GlobalQualityWeight` changes dry/submerged placement truth.
- Runtime scene searches or hot polling are introduced.

## Slot 4 - Ocean/Shoreline/Waterline Proof

Inputs:
- `Docs/Reports/Batch20/2007_OCEAN_SHORELINE_RENDER_PROOF_PACKET.md`
- `Docs/Reports/Batch20/2007_OCEAN_ROUTE_CONTRACTS.csv`
- `Docs/Reports/Batch20/2007_UNITY_PROOF_CHECKLIST.md`
- `Docs/Reports/Batch20/2007_RISK_LEDGER.csv`

Required proof:
- Shoreline/waterline closeups.
- Surface-to-5 m transition capture.
- 20-50 m medium depth capture.
- Frame Debugger or RenderGraph Viewer evidence.
- Profiler/GC/memory/VRAM proof for active route.
- Crest boundary inspection without vendor mutation.

Stop if:
- Fake-first becomes flat/cheap water.
- Physical simulation is proposed before shader/material/source fake options.
- Suspicious runtime cost has no profiler/load-shed proof.

## Slot 5 - Sky/Aegir/Moon/Cloud Validation

Inputs:
- `Docs/Reports/Batch20/SKY_AEGIR_MOONS_SOURCE_ROLE_PACKAGE_20260604.md`
- `Docs/Reports/Batch20/sky_aegir_moons_source_roles_20260604.csv`
- 2002 row `B20-2002-006`
- 2007 risk `R-2007-014`

Required proof:
- Aegir long shot.
- Aegir crop.
- Moon/cloud/horizon pan.
- Source slot proof for day/night/blend/cookies as applicable.
- Surface haze/fog sanity check.

Stop if:
- Repair darkens normal surface.
- Aegir/moons/clouds read as flat placeholders.
- Scene View and Game View disagree without explanation.

## Slot 6 - Final Repair Recapture

Inputs:
- Outputs from Slots 1-5.
- Accepted editor bake manifests and relink reports.
- `Docs/Reports/Batch20/2012_BLOCKER_REGISTER.csv`.

Required proof:
- Full shotlist recaptured after accepted repairs.
- Compact/Middle/High/Ultra consequence notes.
- Three-pillar acceptance note.
- Remaining blocker list with proof labels.
- Black Box expectation note if Physics/Voxel/AI/critical runtime routes were touched by later owners.

Stop if:
- Any surface/shallow/medium-depth hero route fails the visual floor.
- Hot-path GC or unproven suspicious render/runtime cost is introduced.
- Gameplay readability is worse after repair.
