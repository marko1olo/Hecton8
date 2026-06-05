# Kinetic Character Animation - SHINOBU_136

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Owner domain: animation/player procedural kinematics

Evidence class: STATIC_SOURCE. Unity import, Play Mode, profiler, GCMonitor, and player-build proof remain pending until a fresh artifact is linked.

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

- Tool identity:
  - `PlayerToolManager.CurrentActiveToolHash` cached at equip/despawn boundaries;
  - `PlayerSwimPresentationController` submits through `SubmitToolPose(..., toolHash)`;
  - `KineticCharacterFrameInputDTO.ActiveToolHash` feeds support-grip bias and animation hash.
- Solver imports no Equipment runtime type.

- Fault forensics: 300-frame telemetry ring `(BufferID)13671368` with dump path `Docs/AgentLogs/Dump_KINETIC_ANIMATOR.bin`.

## Scaling

`HomeostasisBrain.GlobalQualityWeight` continuously controls SDF gradient sampling, IK iteration count, and active bone count.

Low quality uses nearest SDF sampling, triangle breathing, minimum IK iterations, and base bones. Ultra keeps full bones and higher IK budget.

## Designer Control

`Assets/_Project/Data/character_rig_constraints.csv` is the human-readable tuning source. The editor tuner at `HECTON-8/Animation/Procedural Animation Tuner` loads it through a span/FNV parser without hot-path managed tokenization.

## Prohibitions

Do not add Unity `Animator`, `AnimationClip`, `RuntimeAnimatorController`, or Animation Rigging route back onto player.

If authored animation data is needed, convert it to blittable Vault DTOs or shader/VAT scalar data during cold/editor phases.
