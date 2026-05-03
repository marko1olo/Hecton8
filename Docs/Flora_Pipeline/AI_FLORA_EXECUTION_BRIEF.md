# AI Flora Execution Brief

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`

Short entry point for kelp/coral/flora work. This file does not replace `AGENTS.md` or `Docs/PROCEDURAL_ASSET_PIPELINE.md`. It exists so an agent does not need to parse large legacy flora docs before acting.

## 2026-05-02 Current-State Boundary

- Use this as flora execution orientation, not as runtime proof.
- Completion, beauty, performance, texture import, material validity, and scene/runtime scatter wiring remain `PENDING VERIFICATION` until validators, console, profiler, or asset readback prove them.
- Current project truth starts at `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## Authority Order

1. `AGENTS.md`
2. `Docs/PROCEDURAL_ASSET_PIPELINE.md`
3. `Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md`
4. `Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md`
5. `Docs/Flora_Pipeline/FLORA_NEXT_DIALOG_PROMPT.md`
6. `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`
7. `FLORA_TRANSFER_MASTER_STATUS.md`
8. Legacy concept/reference docs only:
   - `Vodorosli.md`
   - `Coralli.md`
   - `работа с водорослями.md`
   - `работа с кораллами.md`
   - `VODOROSLI_TRANSFER_LEDGER.md`
   - `CORALLI_TRANSFER_LEDGER.md`

## Current Owner Stack

- `WorldRuntimeBootstrapAuthoring`
- `WorldProceduralScatterDirector`
- `WorldProceduralFloraBakedStarterGenerator`
- `WorldProceduralFloraFinalVariantAuthoring`
- `WorldProceduralFloraMaterialAuthoring`
- `WorldProceduralFloraTextureAuthoring`
- `WorldProceduralFloraFinalVariantValidator`

Owner rule:

- Runtime world stack owns selection, quotas, density, weighting, placement, and streaming-facing behavior.
- Editor flora pipeline owns geometry baking, material assignment, texture request/import lookup, baked finals, validation, and reports.
- Do not create a parallel seaweed/coral runtime subsystem.

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

## Hard Rules

- No new parallel subsystem for kelp/coral rendering or placement.
- No runtime mesh generation for flora finals.
- No runtime texture generation for flora finals.
- No raw runtime `Instantiate()` for scatter objects. Pool or GPUI path only.
- Triplanar world-space projection is mandatory for flora finals.
- Motion must stay GPU-only.
- One material family per batch. No unique materials per instance.
- Photoreal shader contract is mandatory:
  - SSS approximation
  - curvature-driven wetness
  - micro-parallax on HIGH tier
  - fresnel water film
  - `_NormalScale`
- If exact API or package signature is unknown, stop and request the exact signature. Do not guess.

## Current Truth

- Generated starter flora finals exist.
- Authored photoreal flora finals are still absent.
- Missing imported texture families now have an owned request-layer output via `WorldProceduralFloraTextureAuthoring`.
- Current imported texture gap remains:
  - `family.coral.massive`
  - `family.coral.plate`
  - `family.coral.brittle`
- Current status for beauty/perf/runtime proof remains `PENDING VERIFICATION`.
- Existing runtime owner stack is real and must be reused.
- Existing large flora docs contain useful concepts, but they are not the primary execution contract anymore.

## Execution Workflow

1. `BASE_MESH`
2. `TEXTURE_REQUEST_PACKET`
3. `SHADER_AND_MATERIAL`
4. `LOD_AND_COLLIDER`
5. `GPUI_AND_SCATTER_ALIGNMENT`
6. `VALIDATOR_AND_REPORT`

Meaning:

- Mesh work stays in the existing flora builders and baked starter/final authoring flow.
- Texture work stops at prompt output until the user provides imported textures.
- Shader/material work must align to the procedural pipeline contract before rebuild signoff.
- LOD thresholds must follow `0.6 / 0.15 / 0.04 / 0`.
- GPUI integration must reuse the already proven rock bootstrap pattern, not invent a new plugin contract.
- Validator/report are the gate. No gate pass, no signoff.

## Texture Request Contract

When real textures are needed:

- Stop implementation at texture generation.
- Output only the required master prompts and import settings defined by `Docs/PROCEDURAL_ASSET_PIPELINE.md`.
- Do not generate placeholder production textures in code.
- Do not silently accept procedural editor-generated texture assets as final photoreal deliverables.
- Use `Hecton/Validation/Generate Missing Flora Texture Request Packet` to emit the current missing-family prompt packet.

## Evidence Locations

- `FLORA_TRANSFER_MASTER_STATUS.md`
- `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`
- `WorldProceduralFloraFinalVariantValidator` output
- Unity console / profiler / runtime logs

## Default Working Assumptions

- Use MX350-safe defaults first.
- Keep default flora material quality on `_QUALITY_MX350` unless explicit high-tier authoring is being verified.
- Treat generated starter finals as fallback coverage, not as final art completion.
- Every claimed fix remains `PENDING VERIFICATION` until logs, validator output, or profiler data confirms it.
- Future dialogs should start from `/Docs` before touching root legacy markdown files.
