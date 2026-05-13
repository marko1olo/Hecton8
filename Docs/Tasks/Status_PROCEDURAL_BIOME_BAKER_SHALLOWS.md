# Status - PROCEDURAL_BIOME_BAKER_SHALLOWS

Agent: TECHNICAL_ARTIST
Domain: ECHELON 3 FLORA, FAUNA & BIOTA / Editor Offline Bake
Prompt ID: PROCEDURAL_BIOME_BAKER_SHALLOWS
Status: PENDING VERIFICATION

## Source Prompt

- Extracted from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex for `<AGENT_PROMPT id="PROCEDURAL_BIOME_BAKER_SHALLOWS">`.
- Primary XML task count: 12.
- Recursive re-verification: pending after tasks 1-12 are done or blocked.

## Relevant Mandates Read

- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `.agents-skills/MATH_Deterministic_RNG_SlotMachine.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Checklist

- [ ] 01. Create `Rule_Shallows_TubeCoral.asset`. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 02. Author coral spherical branch axiom. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 03. Author upward kelp axiom. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 04. Configure porous rock noise/subtraction rule. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 05. Bind `MAT_ProceduralBio_Shallows`. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 06. Verify vertex color R root-to-tip gradient. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 07. Generate 50 coral, 100 kelp, 50 rock prefabs. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 08. Verify `LODGroup` LOD0/LOD1/LOD2 and LOD2 triangles `<150`. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 09. Verify shared atlas/material use across all 200 assets. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 10. Confirm zero runtime procedural generation allocation path. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 11. Bake convex `MeshCollider` only on rocks. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.
- [ ] 12. Omega compile check N/A for data authoring, with editor import/console proof. Justification: pending. Alternatives rejected: pending. Microsecond estimate: pending.

## Iteration Log

### Loop 0 - Intake

- Read AGENTS, domain map, mandate registry, procedural asset pipeline, flora pipeline, procedural world architecture.
- Located editor-only Bio-Forge owner: `Assets/_Project/Scripts/Editor/ProceduralGen`.
- Existing generator supports L-system SDF flora and rock mode, but batch count is fixed at 100 and rock mode currently only adds noise to a sphere. Exact 50/100/50 output and porous subtraction need a narrow editor automation patch.
