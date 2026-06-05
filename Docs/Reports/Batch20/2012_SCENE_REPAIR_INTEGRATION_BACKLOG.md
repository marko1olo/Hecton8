# 2012 Scene Repair Integration Backlog

Status: STATIC INTEGRATION BACKLOG / NO UNITY / NO ASSETS EDITS
Worker: Batch20 2012
Date: 2026-06-04

## Authority Read

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `presentation.md`
- `performance.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Evidence Boundary

This backlog is static integration work. It does not prove Unity state, scene visual quality, import health, Play Mode behavior, profiler cost, GC state, Frame Debugger state, or active material bindings.

Current Unity owner route is single-owner only: `Продолжить работу по логам`.

Encoding debt: `taskslocal/batch20_unity_slot_visual_proof_and_scene_repair/BATCH_INDEX.txt` and `2012_SCENE_REPAIR_INTEGRATION_BACKLOG_AND_OWNER_HANDOFF.txt` contain mojibake for the Unity owner name. The corrected owner name above is used here. The corrupted spelling is not propagated.

`Docs/Actual Domains of Project.txt` was missing during this run. Narrow domain used: Batch20 surface/shallow product-face visual repair integration, generation handoff, static validator coordination, and Unity-slot serialization.

## Completed Batch20 Evidence Read

Task files read: `BATCH_INDEX.txt`, `2001` through `2012` task files.

Completed Batch20 status/log/rationale files found: `2002`, `2003`, `2004`, `2005`, `2007`.

Completed report counts used:

- `2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.csv`: 13 static findings.
- `2003_RULE_MATRIX.csv`: 16 placement-rule rows.
- `2004_FLORA_CORAL_VARIANT_MATRIX.csv`: 10 variant rows.
- `2004_TEXTURE_CHANNEL_CONTRACTS.csv`: 34 channel rows.
- `2005_ROCK_VARIANT_MATRIX.csv`: 10 rock rows.
- `2005_TEXTURE_CHANNEL_CONTRACTS.csv`: 8 texture rows.
- `2005_COLLIDER_LOD_CONTRACTS.csv`: 10 collider/LOD rows.
- `2007_OCEAN_ROUTE_CONTRACTS.csv`: 16 route contract rows.
- `2007_RISK_LEDGER.csv`: 15 risk rows.
- Supplemental Batch20 reports found for sky/Aegir/moons, ProductFace, visual-proof runbook, first-hour audits, fauna package, import churn, wet basalt seam-fix, and dry-land scatter risk. They are treated as static reports only unless a later Unity owner creates proof artifacts.

Missing completed sibling status/log evidence: `2001`, `2006`, `2008`, `2009`, `2010`, `2011`. Their task files and some Batch20 reports exist, but completion cannot be invented.

## Integration Order

### Lane 0 - No-Unity Static Intake

Runs without Unity, build, imports, or Assets edits.

1. Static surface/shallow ledger reconciliation.
   - Inputs: `2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.md`, `.csv`, `2002_STATIC_EVIDENCE_COMMANDS.txt`.
   - Owner route: integration planner plus future Unity owner.
   - Output proof: ledger rows mapped to queue IDs and blockers.
   - Stop condition: any item requiring runtime state remains `PENDING UNITY SLOT`.

2. Placement rule repair specification reconciliation.
   - Inputs: `2003_KELP_ROCK_DRY_LAND_PLACEMENT_SPEC.md`, `2003_RULE_MATRIX.csv`, `2003_CANDIDATE_RULE_PATCHES.diff.txt`.
   - Owner route: placement/runtime rule owner, not 2012.
   - Output proof: candidate repair list with no direct application.
   - Stop condition: no rule asset edits outside the Unity/editor owner lane.

3. Static ProductFace debt mapping.
   - Inputs: `PRODUCT_FACE_SOURCE_MANIFEST_PLAN_20260604.md`, `product_face_source_manifest_draft_20260604.csv`, `PRODUCT_FACE_UNITY_OWNER_RELINK_CHECKLIST_20260604.md`, 2002 null-material and primitive findings.
   - Owner route: ProductFace relink owner, then Unity slot owner.
   - Output proof: relink prerequisites and static manifest coverage.
   - Stop condition: no blind relinks; no channel guessing from filenames.

4. Static validator queue setup.
   - Inputs: `UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md`, `unity_visual_proof_capture_shotlist_20260604.csv`, `UNITY_IMPORT_CHURN_READONLY_AUDIT_20260604.md`, `WORLD_PROCEDURAL_SCATTER_DRY_LAND_RISK_AUDIT_20260604.md`.
   - Owner route: static validator owner, then Unity slot owner.
   - Output proof: read-only commands or runbook entries, never runtime claims.
   - Stop condition: no destructive script; no validator that writes into `Assets` without explicit owner approval.

5. GlobalQualityWeight matrix intake.
   - Inputs: `2010_QUALITY_SCALABILITY_MATRIX_AND_PROOF_CHECKLIST.txt`, `quality.md`, `performance.md`, 2004/2005/2007 quality consequence notes.
   - Owner route: quality/scalability matrix owner, then final Unity owner.
   - Output proof: continuous Low/Middle/High/Ultra matrix with proof mapping.
   - Stop condition: binary quality switches, ugly compact lane, or quality changing gameplay truth.

### Lane 1 - GUI/Manual Candidate Generation

Runs outside Unity. It may use browser/Gemini/manual tools only when an orchestrator provides access and traceable outputs.

5. Flora/coral source candidate generation.
   - Inputs: `2004_PROMPT_PACKS.md`, `2004_TEXTURE_CHANNEL_CONTRACTS.csv`, `2004_FLORA_CORAL_VARIANT_MATRIX.csv`.
   - Owner route: manual/Gemini candidate owner, then BioForge/Flora bake owner.
   - Output proof: source bitmap files, prompt provenance, rejection notes, channel-intent sheet.
   - Forbidden: claiming images exist without files; dark/noir surface prompt defaults; undefined packed channels.

6. Geology/wet basalt source candidate generation.
   - Inputs: `2005_TEXTURE_CHANNEL_CONTRACTS.csv`, `WET_BASALT_SEAMFIX_QA_CHECKLIST_20260604.md`, `2005_ROCK_VARIANT_MATRIX.csv`.
   - Owner route: manual/Gemini candidate owner, then GeologyForge bake owner.
   - Output proof: source bitmap files, tiling QA, wetness/foam/waterline mask notes.
   - Forbidden: primitive rocks, placeholder material fallback, fog/distance hiding.

7. Sky/Aegir/moon/cloud candidate generation or source validation.
   - Inputs: `SKY_AEGIR_MOONS_SOURCE_ROLE_PACKAGE_20260604.md`, `sky_aegir_moons_source_roles_20260604.csv`, `sky_aegir_moon_texture_prompts_20260604.md` if orchestrator explicitly supplies it as generated prompt evidence.
   - Owner route: manual/Gemini candidate owner, then sky/Aegir validation owner.
   - Output proof: texture candidates or source-slot validation packet.
   - Forbidden: flat gradients, placeholder circles, darkening surface time of day as repair.

### Lane 2 - Editor-Only Bake / Import Preparation

Serialized. Runs only when an editor owner is assigned. Does not steal the active Unity slot from `Продолжить работу по логам`.

8. BioForge/Flora bake package.
   - Inputs: 2004 package, source candidates, 2003 placement gates.
   - Owner route: BioForge/Flora editor owner.
   - Output proof: bake manifest, variant validation, texture/channel report, LOD/collider/family links, no proxy primitive route.
   - Stop condition: source images absent; proxy/default material fallback; dry-land placement gates unresolved.

9. GeologyForge bake package.
   - Inputs: 2005 package, wet basalt QA, source candidates, 2003 placement gates.
   - Owner route: GeologyForge editor owner.
   - Output proof: deterministic manifest, 10 rock variants or explicit rejected rows, 10 collider/LOD validations, material slot proof.
   - Stop condition: packed mask/wetness/foam-waterline contract unresolved; `Default-Material` or placeholder fallback.

10. ProductFace channel contract lock.
   - Inputs: ProductFace manifest draft, 2004/2005 channel contracts, sky/ocean route-owned contracts.
   - Owner route: ProductFace static/editor owner.
   - Output proof: shader-by-shader channel contract with allowed material routes and rejection cases.
   - Stop condition: ORM/MRAO/ARM/MRAO semantics guessed; source package missing.

### Lane 3 - Unity Slot Visual Proof

Only one owner controls Unity. Current owner must hand over explicitly.

11. Initial visual proof capture and triage.
   - Inputs: `2001_UNITY_SLOT_VISUAL_PROOF_CAPTURE_AND_TRIAGE.txt`, `UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md`, shotlist CSV, 2002/2007 blockers.
   - Owner route: Unity slot owner.
   - Output proof: Game View / Scene View matching pair, shoreline closeup, 0-5 m shallows, 20-50 m medium depth, Aegir long shot, Aegir crop, 360 sky pan, quality-lane notes.
   - Stop condition: Unity slot not free; compile/import running; CPU/build contention; proof path would write under `Assets`.

12. Ocean/shoreline/waterline proof.
   - Inputs: `2007_OCEAN_SHORELINE_RENDER_PROOF_PACKET.md`, `2007_OCEAN_ROUTE_CONTRACTS.csv`, `2007_UNITY_PROOF_CHECKLIST.md`, `2007_RISK_LEDGER.csv`.
   - Owner route: Unity slot owner with ocean/render owner handoff.
   - Output proof: Frame Debugger/RenderGraph, profiler/GC/memory/VRAM, shore/waterline/shallow captures.
   - Stop condition: Crest vendor mutation requested; physical water sim proposed before premium fake; no profiler proof for suspicious cost.

13. Sky/Aegir/moon/cloud validation.
   - Inputs: sky source package, 2002 sky blockers, 2007 Aegir horizon risk.
   - Owner route: Unity slot owner with sky/Aegir source owner.
   - Output proof: horizon/long/crop/360 capture packet, source slot proof, haze/fog sanity notes.
   - Stop condition: normal surface becomes muddy/dark; Aegir/moons become flat procedural placeholders.

14. ProductFace relink and contract application.
   - Inputs: ProductFace relink checklist, source manifest draft, 2002 null-material/primitive findings.
   - Owner route: Unity slot owner with ProductFace owner.
   - Output proof: relink report, validator output, material slot proof, before/after screenshots.
   - Stop condition: blind relink; deleted assets without scoped proof and `.meta` handling; channel contract unresolved.

15. Placement rule repair application.
   - Inputs: 2003 candidate rule diffs and rule matrix, 2002 placement findings, BioForge/Geology bakes.
   - Owner route: Unity slot or editor owner responsible for placement assets.
   - Output proof: rule diff, generated placement preview, before/after dry-land and shallow screenshots, overdraw/profiler when density changes.
   - Stop condition: deleting kelp/rocks as repair; GlobalQualityWeight changes placement truth; runtime scene searches introduced.

### Lane 4 - Final Unity Repair / Recapture

16. Final recapture after all accepted repairs.
   - Inputs: all accepted editor/Unity output packets, shotlist CSV, low/middle/high/ultra matrix.
   - Owner route: single final Unity owner.
   - Output proof: final capture manifest, screenshots outside `Assets`, profiler/GC/Frame Debugger packet, unresolved blocker register.
   - Stop condition: any three-pillar failure; any proof class missing for a claim; any runtime crash/NaN without Black Box dump expectation.

## 3-Strikes Protocol

For any editor/Unity owner in this queue:

1. Attempt 1: fix own compile/import break manually and re-check the exact error.
2. Attempt 2: narrow the changed chunk, isolate the dependency, and re-check.
3. Attempt 3: if the break depends on another owner or unstable sibling output, revert only the owner-created broken chunk, mark `[BLOCKED BY DEPENDENCY]`, write an integrator note, and stop that item.

Do not break the build to prove a point. Do not revert or overwrite unrelated changes by other agents.

## Black Box Expectations

2012 did not touch critical runtime systems. If later repair touches Physics, Voxel, AI, ocean runtime readback, placement runtime, or global authority routes, the owner must require:

- last 300 frames in a fixed-size telemetry ring;
- deterministic dump artifact on crash/NaN/error;
- owner/phase/lane/capacity/failure-mode fields;
- no managed hot-path allocation in telemetry write;
- proof artifact or explicit `PENDING VERIFICATION`.

Use `Docs/AgentLogs/Dump_[ID].bin` only when an explicit ID exists. Otherwise use system name and timestamp.

## GlobalQualityWeight Gates

`GlobalQualityWeight` is continuous. No item in this queue may create low/high binary switches.

- Low/Compact: cheap route remains authored, bright/readable, silhouette-correct, water/sky/shoreline legible, and non-primitive.
- Middle: default player-facing product route passes graphics, optimization, and gameplay gates.
- High: richer material response, variants, density, reflections, foam breakup, cloud depth, or biological detail only after profiler/render proof.
- Ultra: sensory overkill only; no gameplay truth, DTO layout, save identity, collider truth, placement authority, or route ownership change.

## Three-Pillar Acceptance

- Graphics: surface, sky, Aegir, moons, coastline, ocean surface, waterline, photic shallows, and medium-depth hero routes are bright/readable/premium and not hidden by darkness, fog, blur, or angle tricks.
- Optimization: no hot-path GC, no hidden `.Complete()`, no runtime mesh/texture generation, no unproven render pass over 0.1 ms, no import churn from proof under `Assets`.
- Gameplay: route readability, hazards, resources, item identity, shore entries, depth cues, and return-path cues remain clear. Quality scaling does not change truth.

## Dispatch Result

Parallel-safe no-Unity lanes: static ledger, placement spec, ProductFace static contract, static validators, manual/Gemini source generation.

Serialized editor-only lanes: BioForge/Flora bake, GeologyForge bake, ProductFace channel lock.

Serialized Unity-slot lanes: initial capture, ocean proof, sky proof, relink/application, placement application, final recapture.

Final status: integration backlog complete as static planning. Runtime/editor/profiler/player proof remains pending by design.
