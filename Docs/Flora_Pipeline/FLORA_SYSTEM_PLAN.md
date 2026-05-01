# Flora System Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`

This file is the compact implementation plan for the HECTON-8 flora pipeline. It exists so future dialogs can start from `/Docs` instead of scanning large legacy markdown files in the repo root.

## 2026-05-01 Current-State Boundary

- This plan is a current execution contract, not runtime certification.
- Generated starter finals, texture request paths, and owner stack references require source/asset readback before surgery.
- Current project truth starts at `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## Read Order

Use this order only:

1. `AGENTS.md`
2. `Docs/PROCEDURAL_ASSET_PIPELINE.md`
3. `Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md`
4. `Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md`
5. `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
6. `Docs/Flora_Pipeline/FLORA_NEXT_DIALOG_PROMPT.md`
7. `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`

Legacy files in repo root are reference-only and must not be treated as the primary execution contract.

## Goal

Build one beautiful, universal, optimized flora system for kelp and coral that:

- keeps the current HECTON-8 owner stack
- produces believable underwater organics
- stays inside MX350 budgets
- uses GPU-driven motion only
- uses world-space/triplanar-friendly shading
- supports baked finals + GPUI/runtime scatter
- rejects unverified assets instead of silently accepting them

## Current Truth

- The runtime/world owner stack already exists.
- Generated starter flora finals already exist.
- Authored photoreal flora finals are still absent.
- Current shader/material/texture contract is behind the new procedural pipeline document.
- Missing imported coral texture families now have an owned request-packet path in `WorldProceduralFloraTextureAuthoring`.
- Existing root flora docs are large, inconsistent, and partially stale.
- Any claim of completion remains `PENDING VERIFICATION` until validator, logs, or profiler confirm it.

## Real Owner Stack

Do not replace this architecture.

- `WorldRuntimeBootstrapAuthoring`
- `WorldProceduralScatterDirector`
- `WorldProceduralFloraBakedStarterGenerator`
- `WorldProceduralFloraFinalVariantAuthoring`
- `WorldProceduralFloraTextureAuthoring`
- `WorldProceduralFloraMaterialAuthoring`
- `WorldProceduralFloraFinalVariantValidator`

Ownership rule:

- runtime stack owns selection, density, weighting, placement, streaming behavior
- editor flora pipeline owns mesh bake, material setup, texture lookup/request, final intake, validation, reports
- no standalone coral system
- no standalone seaweed system
- no parallel runtime renderer unless it is only a backend adaptation of the existing stack

## Supported Families

- `family.kelp.tall`
- `family.kelp.patch.dense`
- `family.kelp.canopy`
- `family.kelp.abyssal`
- `family.coral.low`
- `family.coral.branching`
- `family.coral.massive`
- `family.coral.plate`
- `family.coral.brittle`

## Hard Constraints

- no runtime mesh generation for flora finals
- no runtime texture generation for flora finals
- no raw runtime `Instantiate()` for scatter objects
- no unique material per instance
- no transparent shader for opaque flora geometry
- no UV-dependent authored-final contract
- no CPU animation
- no new parallel subsystem
- no guessing missing package or API signatures

## Required Visual Contract

Every final flora asset must converge to this contract:

- world-space triplanar-friendly projection
- GPU-only motion
- SSS approximation
- curvature-driven wetness
- fresnel water film
- `_NormalScale`
- HIGH-tier micro-parallax from `Mask.B`
- quality keywords:
  - `_QUALITY_MX350`
  - `_QUALITY_HIGH`
- one material family per batch

## Required Runtime Contract

- flora culling range: `60-120m`
- culling must be driven by Unity layer cull distances + GPUI distance culling
- LOD thresholds must converge to: `0.6 / 0.15 / 0.04 / 0`
- GPUI integration must reuse the proven rock bootstrap pattern
- scatter owner remains `WorldProceduralScatterDirector`

## Texture Contract

When real textures are needed:

- stop implementation
- output master prompts only
- use the exact prompt format from `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- include import settings
- wait for user-provided texture generation outputs

Production rule:

- editor-generated procedural texture assets are not final photoreal proof
- imported tiling texture sets are the target source for final materials

## Execution Sequence

Follow this order. Do not merge steps.

1. `BASE_MESH`
2. `TEXTURE_REQUEST_PACKET`
3. `SHADER_AND_MATERIAL`
4. `LOD_AND_COLLIDER`
5. `GPUI_AND_SCATTER_ALIGNMENT`
6. `VALIDATOR_AND_REPORT`

## Phase Plan

### Phase 1: Documentation Gate

- maintain the short docs in `/Docs`
- keep `/Docs` as the primary agent entry point
- mark root flora docs as legacy-reference only when touched

### Phase 2: Texture Source Cleanup

- `WorldProceduralFloraTextureAuthoring` stops acting as a final production texture generator
- it becomes imported-texture lookup + request-layer support
- if textures are missing, system must fail closed
- current request-layer output is `Hecton/Validation/Generate Missing Flora Texture Request Packet`
- imported coverage reconcile entry is `Hecton/Validation/Reconcile Imported Flora Coverage`

### Phase 3: Shader And Material Alignment

- upgrade `Hecton_KelpMaster.shader`
- upgrade `Hecton_CoralMaster.shader`
- align materials to the new shader contract
- default working tier is `_QUALITY_MX350`

### Phase 4: LOD And Final Intake Alignment

- bring starter/final LOD settings to `0.6 / 0.15 / 0.04 / 0`
- keep baked finals and generated starters separated from runtime selection logic
- keep family budgets enforced

### Phase 5: Validator Gate Expansion

- validator must reject:
  - missing real texture sets
  - stale shader contract
  - stale LOD thresholds
  - forbidden baggage on visual finals
  - broken family coverage/linkage

### Phase 6: GPUI / Scatter Alignment

- reuse rock GPUI bootstrap pattern
- do not invent a second flora runtime stack
- confirm play-mode flora path does not fall back to raw `Instantiate()`

## Acceptance Criteria

Minimum acceptance for a family:

- mesh fits the family budget
- material/shader matches the flora contract
- LOD group matches the exact thresholds
- validator passes
- status report reflects correct `generated-only` vs `authored`
- runtime/perf claims stay `PENDING VERIFICATION` until logs/profiler exist

## Immediate Priority

Work in this order:

1. documentation and clean agent handoff
2. texture-source cleanup
3. shader/material alignment
4. LOD/validator alignment
5. GPUI/scatter alignment
6. real texture request to user

## Expansion Backlog (Post-Flora)

These are required next verticals using the same procedural asset pipeline and owner-driven architecture. No new parallel runtime stack.

- GEOLOGY: rock families, rock clusters, cliff shelves, and seabed formations
- STRUCTURAL: base shells, station modules, wreck segments, structural props
- INTERIOR_DECOR: interior panels, conduits, clutter sets, modular trims
- COLONY_PARTS: colony segments, habitat limbs, docking bays
- VALIDATION: per-category validator/ruleset using the same pass/fail gates (budget, LOD, shader contract, texture contract)
- MATERIALS: category-specific material presets under the same shader contract; no per-instance materials

Each vertical must:
- declare category (ORGANIC / GEOLOGICAL / STRUCTURAL / INTERIOR_DECOR)
- use triplanar world-space projection
- keep MX350 budgets and culling distances per category
- fail closed until validator/report proof exists

Shared architecture rule:

- future verticals must inherit the same family/profile/rule/variant model defined in `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- flora remains the reference implementation for strict texture/material validation
- geology remains the reference implementation for runtime bootstrap and large-form fallback generation
- structural/interior/colony work must extend existing owners instead of creating a second scatter/runtime stack

## Evidence Files

- `Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `FLORA_TRANSFER_MASTER_STATUS.md`
- `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`

## Final Rule

Future dialogs must start from `/Docs`.
Root legacy flora docs may be opened only for concept recovery, never as the default source of truth.
