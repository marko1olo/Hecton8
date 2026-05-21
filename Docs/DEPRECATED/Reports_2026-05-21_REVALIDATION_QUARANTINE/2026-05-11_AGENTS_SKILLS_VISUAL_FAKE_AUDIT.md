<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-11 Agents Skills Visual Fake Audit
Date: 2026-05-11
Status: PENDING VERIFICATION
Scope: `.agents-skills` actuality check for the current visual-realistic-fake production doctrine

Mandates followed:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/REND_Foveated_Simulation_LOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/PHYS_Fluid_Incursion_Interior.txt`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`
- `AGENTS.md`

## Current Truth

The registry is not all fine.

The zero-GC, memory, Addressables, AUP, ownership, and dispatcher rules remain directionally valid. The stale zone is physical/visual realism: several mandates still start from expensive physical truth, broad simulation, or old Unity physics/audio paths instead of the current rule:

> visual fake first, physical simulation only when gameplay correctness fails without it.

This pass updates the authority layer. It does not prove runtime frame time, GC, memory retention, Play Mode behavior, Unity console state, player build, or visual quality.

## What Was Wrong

1. No explicit registry-level mandate existed for the cinematic cheat protocol.
2. `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` still allowed or implied old expensive paths such as MINIMAL Bloom and FSR2-class temporal upscaling language.
3. `REND_Foveated_Simulation_LOD.txt` treated lower-rate simulation as the main optimization answer; it needed a fake-first gate.
4. `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt` carried too much physical-fluid language for a presentation layer and contained a contradictory LPPV/APV comment.
5. `PHYS_Fluid_Incursion_Interior.txt` described real slosh/mass/CoM mutation too broadly for non-critical interiors.
6. `CORE_Weather_Abyssal_FlowField_Currents.txt` allowed broad current sampling that can become simulation theater.
7. `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt` allowed expensive voxel/raymarch/fake-GI paths as if they were baseline lighting truth.
8. `PHYS_Tether_Cable_Acceleration_Constraints.txt` conflicted with AGENTS.md by approving Unity `ConfigurableJoint` and direct `Rigidbody.AddForce` paths.
9. `AUDIO_Hrtf_Binaural_Spatialization.txt` conflicted with the underwater audio contract by treating HRTF/ITD/ILD as default realism instead of optional headphone/accessibility work.
10. `PHYS_Physics_Integrity_Determinism_ForceMode.txt` contains stale/broken examples and same-method schedule/complete examples that must not be copied into production.
11. `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt` still allowed tool-side Unity Joint creation language for welds/anchors.
12. `PHYS_Kinematic_Interaction_Hands.txt` still contained direct `Rigidbody.AddForce`/`AddTorque` examples for grab and drag solve paths.

## What I Did

Added:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/README.md`

Updated with explicit 2026-05-11 override blocks:

- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Foveated_Simulation_LOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/PHYS_Fluid_Incursion_Interior.txt`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `.agents-skills/PHYS_Tether_Cable_Acceleration_Constraints.txt`
- `.agents-skills/AUDIO_Hrtf_Binaural_Spatialization.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`

Continuation corrections in the same May 11 pass:

- `AGENTS.md` now owns the stable authority spine and visual-fake-first rejection gate.
- `AGENTS.md` no longer requires LPPV; large dynamic mesh lighting is APV/probe approximation only after profiler and memory proof.
- `AUDIO_Hrtf_Binaural_Spatialization.txt` no longer treats HRTF/ITD/ILD as default underwater realism.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` now sets Bloom `OFF` for MINIMAL and LOW.
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt` no longer contains copy-pasteable same-method `Schedule().Complete()` examples.
- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt` now uses probe approximation language instead of LPPV language.
- `Docs/ARCHITECTURE/README.md`, `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`, and `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md` now define stable read order and report-vault trust boundaries.

Updated active docs to carry the same doctrine:

- `AGENTS.md`
- `.agents-skills/README.md`
- `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`
- `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`
- `Docs/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/QUALITY_GATES.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/Legacy_Backlog/beklog.txt`

## In-Game Result

No in-game result is claimed.

Expected production effect after engineers obey these docs:

- less CPU/GPU/RAM spent on invisible causes
- more use of shader/VFX/audio/haptic/proxy tricks
- physical simulation reserved for player control, collisions, damage truth, save-affecting state, and gameplay-critical hazards

## What Was Verified

Verified:

- relevant mandates were read before editing
- stale/conflicting mandate text was identified by static registry scan
- no runtime/shader source files were edited in this pass
- existing dirty runtime files were left untouched

Not verified:

- Unity import
- Play Mode
- Unity console
- GCMonitor
- profiler frame time
- memory retention
- player build
- visual quality

## Regression Model

CPU: documentation-only. Runtime CPU behavior unchanged.

GC: documentation-only. No hot path code added. Measured `0 B/frame` proof absent.

Memory: documentation-only. No assets, textures, buffers, scenes, prefabs, Addressables groups, or project settings changed.

Cadence: documentation-only. No tick/update/job/physics cadence changed.

Correctness: mandate authority is clearer. Risk remains that old lower sections in historical mandate files still contain stale examples; the 2026-05-11 override blocks are the current authority where conflicts exist.

Status: PENDING VERIFICATION.
