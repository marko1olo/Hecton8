# Status - PROCEDURAL_BIOME_BAKER_SHALLOWS

Agent: TECHNICAL_ARTIST
Domain: ECHELON 3 FLORA, FAUNA & BIOTA / Editor Offline Bake
Prompt ID: PROCEDURAL_BIOME_BAKER_SHALLOWS
Status: PENDING VERIFICATION

## Source Prompt

- Extracted from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex for `<AGENT_PROMPT id="PROCEDURAL_BIOME_BAKER_SHALLOWS">`.
- Primary XML task count: 12.
- Recursive re-verification: complete after second-pass bake and validation.

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

- [x] 01. Create `Rule_Shallows_TubeCoral.asset`. Justification: Unity-created `BioRuleData` asset copied from isolated bake with preserved `.meta`; DOD practice: asset database ownership, no raw YAML hand-write. Alternatives rejected: placeholder markdown and manual YAML. Microsecond estimate: 0 us runtime.
- [x] 02. Author coral spherical branch axiom. Justification: `F[+F][-F][^F][&F][/F][\F]` with thick SDF capsule profile, 40 SDF resolution, LOD2 budget 120. Alternatives rejected: single blob mesh and runtime growth. Microsecond estimate: 0 us runtime.
- [x] 03. Author upward kelp axiom. Justification: `F[+F][-F]F` with replacement `F[+F]F[-F]F`, narrow angle, ribbon SDF profile for upward broad-leaf forms. Alternatives rejected: Rigidbody/cloth kelp and per-blade scripts. Microsecond estimate: saves estimated 200-600 us/frame versus 200 animated scripts.
- [x] 04. Configure porous rock noise/subtraction rule. Justification: `PorousRock` profile uses 3D Simplex displacement plus deterministic subtractive pore spheres (`_rockPoreCount: 18`). Alternatives rejected: physics booleans and runtime cave carving. Microsecond estimate: 0 us runtime.
- [x] 05. Bind `MAT_ProceduralBio_Shallows`. Justification: all rules reference material GUID `f669d8458f3703841b4ed34a8236b192`; shader is triplanar procedural bio. Alternatives rejected: per-family materials and duplicate atlas sets. Microsecond estimate: SRP batching avoids material-state churn; exact us not profiled.
- [x] 06. Verify vertex color R root-to-tip gradient. Justification: isolated Unity validation passed after final-span mesh color normalization; validator rejects weak R range. Alternatives rejected: loosening validation or shader-side height reconstruction. Microsecond estimate: avoids 20-60 us/frame of runtime mask reconstruction at 200 visible assets.
- [x] 07. Generate 50 coral, 100 kelp, 50 rock prefabs. Justification: copied outputs contain TubeCoral=50, Kelp=100, PorousRock=50 prefabs; meshes=600. Alternatives rejected: fixed 100/100/100 stock batch and manual duplicates. Microsecond estimate: 0 us runtime generation.
- [x] 08. Verify `LODGroup` LOD0/LOD1/LOD2 and LOD2 triangles `<150`. Justification: isolated Unity validator passed `LOD2<150`; main text scan found 200 LOD groups and 200 LOD blocks. Alternatives rejected: single LOD meshes. Microsecond estimate: LOD2 target reduces far-mesh vertex work; exact frame gain scene-dependent.
- [x] 09. Verify shared atlas/material use across all 200 assets. Justification: main text scan found 600 material references to the same material GUID and 4 shared Shallows atlas textures. Alternatives rejected: individual textures per variant. Microsecond estimate: avoids material/texture bind churn; exact us not profiled.
- [x] 10. Confirm zero runtime procedural generation allocation path. Justification: generation code remains editor-only under `Assets/_Project/Scripts/Editor/ProceduralGen`; prefabs contain no MonoBehaviours per Unity validation. Alternatives rejected: runtime L-system expansion/scatter generation. Microsecond estimate: avoids all procedural generation allocations and expected >100 us streamed-placement spikes.
- [x] 11. Bake convex `MeshCollider` only on rocks. Justification: main text scan found floraColliders=0, rockColliders=50, rockConvex=50, `Collision_LOD2` children=50; second-pass validator checks collider offset against the LOD renderer offset. Alternatives rejected: flora mesh colliders, root-space misaligned rock colliders, and per-leaf collision. Microsecond estimate: avoids broadphase/collider cost for 150 flora prefabs.
- [x] 12. Omega compile check N/A for data authoring, with editor import/console proof. Justification: isolated Unity 6000.4.1f1 batchmode compiled ProceduralGen and logged `Validation passed`; direct Roslyn compile of `Hecton8.Editor.ProceduralGen` returned `CscExit=0`; main `dotnet build Hecton8.Core.csproj` returned `Build succeeded. 0 Warning(s). 0 Error(s).` Alternatives rejected: editing cross-domain systems or relying on text-only prefab inspection. Microsecond estimate: 0 us runtime.

## Iteration Log

### Loop 0 - Intake

- Read AGENTS, domain map, mandate registry, procedural asset pipeline, flora pipeline, procedural world architecture.
- Located editor-only Bio-Forge owner: `Assets/_Project/Scripts/Editor/ProceduralGen`.
- Existing generator supports L-system SDF flora and rock mode, but batch count is fixed at 100 and rock mode currently only adds noise to a sphere. Exact 50/100/50 output and porous subtraction need a narrow editor automation patch.

### Loop 1 - Tasks 1-5 Authoring

- Patched `BioRuleData` with SDF profiles, ribbon controls, and rock pore controls.
- Added `ShallowsBioForgeBatchBaker` menu/CLI path for exact Safe Shallows authoring and shared material/atlas creation.
- Re-extracted XML prompt after first 3 tasks via PowerShell regex from `Docs/Tasks/CURRENT_BATCH.md`.
- Main Unity compile path blocked by unrelated Core/Database errors; shifted to isolated Unity project to avoid cross-domain edits.

### Loop 2 - Tasks 6-7 First Bake

- Ran isolated Unity bake in `.codexbuild/ShallowsBakeProject_20260514_030549`.
- First bake produced required counts but failed vertex color R validation on 61 assets.
- Fixed final mesh vertex color R normalization in `BioForgeGenerator.CreateUnityMesh`.

### Loop 3 - Tasks 8-9 HLOD/Atlas Verification

- Reran isolated bake; validator passed counts, LOD2 triangle ceiling, shared material, and atlas binding.
- Detected Burst safety exceptions in editor decimation log despite validation pass.
- Added `NativeDisableParallelForRestriction` on the deterministic three-vertex output range in `BioForgeEdgeCollapseDecimationJob`.
- Re-extracted XML prompt again after verification phase.

### Loop 4 - Tasks 10-12 Clean Bake And Copyback

- Reran isolated bake after Burst safety fix; log contains `Validation passed. Coral=50, Kelp=100, Rocks=50, Total=200, LOD2<150`.
- Copied Shallows rules, material, atlas PNGs, 600 mesh assets, and 200 prefabs back into the main project with `.meta` files.
- Main text scan: `LODGroup=200`, `m_LODs=200`, `badShadows=0`, `floraColliders=0`, `rockColliders=50`, `rockConvex=50`, `materialRefs=600`.

### Loop 5 - Self-Review And Polish

- Re-read assignment and verified all 12 primary checklist rows are checked.
- Read `<POLISH_MANDATE>` only after all 12 checklist rows were checked.
- Converted Burst SDF mode selection to explicit bit flags and reran isolated Unity validation; validation passed.
- First `dotnet build Hecton8.Core.csproj` was blocked by unrelated missing UI source `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs`; this was not edited from the flora domain.

### Loop 6 - Second-Pass AAA Audit

- Re-read status, rationale, and XML assignment before the second-pass audit.
- Found and fixed two asset-quality defects: `TextureRoot` path casing now matches `Assets/_Project/Art/TEXTURES`, and rock colliders now live on `Collision_LOD2` children with the same local offset as the visible LOD renderers.
- Changed the Safe Shallows bake to ensure generated folders instead of deleting them, preserving generated asset GUIDs on re-bake.
- Strengthened validation to require MatCap atlas binding, exactly one renderer per LOD, non-null LOD meshes, zero shadow casters, and aligned convex rock colliders.
- Reran isolated Unity bake: `Validation passed. Coral=50, Kelp=100, Rocks=50, Total=200, LOD2<150`.
- Copied the staged output back into the main project without deleting target folders. Main scan: `Rules=3`, `Prefabs=200`, `MeshAssets=600`, `Textures=4`, `CollisionChildren=50`, `MeshColliders=50`, `FloraColliders=0`, `LODGroups=200`, `MeshRenderers=600`, `BadShadowLines=0`, `materialRefs=600`.
- Final `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).`

### Loop 7 - XML Status Correction And Bake Throughput Audit

- Re-extracted the exact XML assignment from `Docs/Tasks/CURRENT_BATCH.md`; core prompt still mandates `STATUS: PENDING VERIFICATION`, so the status header was corrected back from polish wording while preserving the compile/validation proof.
- Audited isolated bake log behavior. `Debug.Log` generated a stack trace for every generated prefab in batchmode, expanding `Logs/ShallowsBioForgeStageBake_Rerun4.log` to 681 KB and wasting editor bake time.
- Patched `BioForgeGenerator` to skip cancelable progress bar work in `Application.isBatchMode` and emit generated-prefab summaries with `LogOption.NoStacktrace`.
- Strengthened Safe Shallows validation again: every prefab must have exactly three renderers, and each renderer must keep receive shadows off, forced no motion vectors, light/reflection probes off, and dynamic occlusion off.
- Verification after this source-only pass: `dotnet build Hecton8.Core.csproj` succeeded with `0 Warning(s), 0 Error(s)`; prefab text scan found `Prefabs=200`, `MeshRenderers=600`, `BadCastShadows=0`, `BadReceiveShadows=0`, `BadDynamicOccludee=0`, `BadLightProbeUsage=0`, `BadReflectionProbeUsage=0`, `MeshColliders=50`, `CollisionChildren=50`.
