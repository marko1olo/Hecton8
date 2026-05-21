# Kinetic Character Animation - SHINOBU_136

Evidence class: STATIC_SOURCE. Unity import, Play Mode, profiler, GCMonitor, and player-build proof remain pending until a fresh artifact is linked.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not animation runtime, shader upload, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs`
- `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs`
- `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs`
- `Assets/_Project/Scripts/Editor/KineticCharacterAnimationTunerWindow.cs`
- `Assets/_Project/Data/character_rig_constraints.csv`

## Owner

`SHINOBU_136 / KINETIC_CHARACTER_ANIMATOR` owns the player/humanoid procedural animation solver that replaces Unity `Animator` state-machine/clip evaluation for the player presentation route.

## Runtime Route

- Input truth: `BufferID.PlayerKinematicState` for rollback-safe root state, `BufferID.VoxelSdfTexture3D` for wall bracing, and cold presentation/tool scalars from `PlayerSwimPresentationController`.
- Persistent memory: domain-local Vault buffers `(BufferID)13671360..13671371`.
- Solver: Burst jobs in `Assets/_Project/Scripts/Animation/KineticCharacter`.
- Output: `float4x4` bone matrices written to Vault buffer `(BufferID)13671365`, then uploaded to a double `GraphicsBuffer` and exposed as `_H8KineticCharacterBoneMatrices`.
- Tool identity: `PlayerToolManager.CurrentActiveToolHash` is cached at equip/despawn boundaries, `PlayerSwimPresentationController` submits it through `SubmitToolPose(..., toolHash)`, and `KineticCharacterFrameInputDTO.ActiveToolHash` carries it into deterministic support-grip bias and animation state hashing. No Equipment runtime type is imported by the solver.
- Fault forensics: 300-frame telemetry ring `(BufferID)13671368` with dump path `Docs/AgentLogs/Dump_KINETIC_ANIMATOR.bin`.

## Scaling

`HomeostasisBrain.GlobalQualityWeight` continuously controls SDF gradient sampling, IK iteration count, and active bone count. Low quality collapses to nearest SDF sampling, triangle breathing, minimum IK iterations, and base bones. Ultra quality keeps full active bones and higher IK iteration budget.

## Designer Control

`Assets/_Project/Data/character_rig_constraints.csv` is the human-readable tuning source. The editor tuner at `HECTON-8/Animation/Procedural Animation Tuner` loads it through a span/FNV parser without hot-path managed tokenization.

## Prohibitions

Do not add a Unity `Animator`, `AnimationClip`, `RuntimeAnimatorController`, or Animation Rigging route back onto the player. If authored animation data is needed, convert it into blittable Vault DTOs or shader/VAT scalar data during cold/editor phases.
