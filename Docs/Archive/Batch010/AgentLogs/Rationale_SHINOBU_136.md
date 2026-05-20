# SHINOBU_136 Rationale

Status: STATIC VERIFIED / COMPILE BLOCKED BY CPU GATE

## Decision 0 - Prompt Isolation
Problem: Batch file contains neighboring agent prompts with overlapping animation terms.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_136">` through `</AGENT_PROMPT>` using CLI regex and discarded neighboring tasks.
Rejected Alternatives: Basic contextual reading of CURRENT_BATCH.md; it exposes adjacent SHINOBU_135/137 content and can corrupt architecture decisions.
Scalability potential: Keeps implementation scoped to player/humanoid kinetic animation; no cross-domain work that creates unnecessary runtime surface.
Hardware Impact: No runtime cost. Prevents accidental systems that would tax i3/MX350.

## Decision 1 - Fresh Disk Memory
Problem: Status and rationale files were missing for the current batch.
Solution: Created fresh `Docs/Tasks/Status_SHINOBU_136.md` and `Docs/AgentLogs/Rationale_SHINOBU_136.md` before code edits.
Rejected Alternatives: Chat-only state; violates anti-amnesia and reporting protocol.
Scalability potential: No runtime path affected.
Hardware Impact: No runtime impact.

## Decision 2 - Mandate Selection
Problem: SHINOBU_136 spans animation, IK, native memory, AUP, phase scheduling, DTO alignment, and crash telemetry.
Solution: Read 8 mandates before code: contextual IK, FABRIK/ground snapping, ARM64 layout, AUP determinism, execution phases, zero-GC, native memory/jobs, and blackbox telemetry.
Rejected Alternatives: Reading every registry file; excessive context without improving this domain. Reading only animation files; misses Vault, AUP, and telemetry acceptance gates.
Scalability potential: Low/Middle/High/Ultra path will use continuous GlobalQualityWeight for IK iteration count, cadence, and optional secondary motion.
Hardware Impact: Expected low-end gain comes from replacing Animator graph/object traversal with flat Burst math and striding; estimate remains PENDING until code and profiler evidence exist.

## Decision 3 - Animator Mandate Conflict Resolution
Problem: ANIM_Contextual_Physical_IK contains an older PlayableGraph/Animator integration option, while SHINOBU_136 explicitly requires total Animator eradication and direct matrix output to Vault.
Solution: Treat the batch prompt and AGENTS.md Animator removal requirement as dominant for this task. Use Burst jobs over flat DTOs and Vault/GraphicsBuffer matrix output, not Animator stream handles.
Rejected Alternatives: Keeping Animator as a hidden writeback surface; it preserves the black box and violates the user directive.
Scalability potential: Flat matrix output scales from minimal bone sets on weak devices to extra secondary bones/finger chains on Ultra without switching pipelines.
Hardware Impact: Removes Animator evaluation and graph traversal from player/humanoid runtime path; microsecond savings PENDING static and runtime verification.

## Decision 4 - Player Route Over Legacy ContextualPhysicalIkRig
Problem: `PlayerKinematicsRuntime` still cold-resolved `ContextualPhysicalIkRig`, a legacy Animator/PlayableGraph bridge with private native buffers, even though prefab/scene GUID scans show no active instances.
Solution: Removed the player route to `ContextualPhysicalIkRig`; player hand bracing now remains in Vault telemetry and the new kinetic solver samples SDF directly. The legacy source is left inert for separate ownership cleanup because deleting the whole adjacent runtime would risk non-domain compile damage.
Rejected Alternatives: Keeping the hidden bridge as fallback; it reintroduces Animator graph ownership. Deleting all contextual IK source in this task; too broad and referenced by neighboring runtime code.
Scalability potential: Low/Middle/High/Ultra all use one Burst/Vault path; no hidden late-frame Animator cost on high tier and no legacy graph on low tier.
Hardware Impact: Saves old graph injection and transform stream work if a rig is accidentally present. Estimate: 60-250 us on i3/MX350 class CPU; exact profiler proof pending.

## Decision 5 - Vault Buffer ID Collision Correction
Problem: Initial domain-local IDs `71360..71371` collided with `TerminalOsRuntime`.
Solution: Moved kinetic animation Vault handles to `13671360..13671371`, verified by static grep that only this domain uses the new range.
Rejected Alternatives: Reusing Terminal IDs and relying on type separation; one BufferID must have one owner and one route.
Scalability potential: Stable IDs make the same buffer route valid across all tiers without runtime ambiguity.
Hardware Impact: Prevents undefined Vault aliasing, torn data, and impossible forensic dumps. Runtime cost: 0 us.

## Decision 6 - Explicit DTO Layout And ARM64 Padding
Problem: Procedural animation hot data must be blittable, memcpy-safe, and aligned for ARM64/NEON.
Solution: Defined explicit DTOs: `ProceduralBoneDTO` 64 bytes, `ProceduralIKTargetDTO` 32 bytes, `KineticCharacterRigDTO` 192 bytes, `KineticCharacterFrameInputDTO` 272 bytes, `KineticCharacterTuningDTO` 128 bytes, `KineticCharacterFrameStatsDTO` 64 bytes, `KineticAnimationTelemetryEntry` 64 bytes.
Rejected Alternatives: Sequential layout and properties; both hide padding/copies and are unacceptable for hot NativeArray iteration.
Scalability potential: Low tier can evaluate fewer bones over the same flat layout; Ultra can evaluate more secondary bones without changing the data contract.
Hardware Impact: Prevents unaligned ARM64 reads and false-sharing-prone small telemetry records. Estimate: avoids worst-case cache-line split stalls; exact runtime measurement pending.

## Decision 7 - SDF Wall Brace And Breathing Dear Lie
Problem: Wall bracing and breathing can become expensive if implemented through Unity physics, Animator clips, or full-body physics.
Solution: Wall bracing samples byte SDF data in Burst and computes normals only when quality >= 0.24. Breathing is a scalar sine/triangle fake applied to pelvis/chest offsets.
Rejected Alternatives: `Physics.Raycast`, MeshCollider queries, AnimationClip blending, procedural respiratory physics.
Scalability potential: Low uses nearest SDF and triangle breathing. Middle adds smoothed sine and limited IK. High/Ultra use more IK iterations and full active bone count.
Hardware Impact: Estimate: 20-180 us avoided for raycast-heavy contact frames; 5-40 us avoided for breathing clip/physics work.

## Decision 8 - Matrix Upload Boundary
Problem: The solver must output matrices to Vault, but rendering also needs GPU-visible matrix data without per-bone Transform writes.
Solution: Jobs write `float4x4` matrices to Vault, `LateFrameTick` copies changed matrices to prewarmed double `GraphicsBuffer` with `LockBufferForWrite`, and shader globals expose the buffer/scalars.
Rejected Alternatives: Writing Transform hierarchy, SkinnedMeshRenderer bone Transform updates, per-frame buffer allocation, and Animator stream output.
Scalability potential: Low tier uploads fewer active bones; Ultra uploads the full 18-bone mock/default set and can scale to the configured capacity.
Hardware Impact: Avoids hierarchy traversal and managed bone object churn. GPU upload proof pending Unity runtime.

## Decision 9 - Designer CSV Without Hot-Path GC
Problem: Designers need tweakable rig constants, but `string.Split`/LINQ/list parsing would violate play-mode zero-GC policy.
Solution: Added `character_rig_constraints.csv`, an editor button, and a span/FNV parser that accepts `ReadOnlySpan<byte>` or `ReadOnlySpan<char>`, skipping `_`, `-`, and spaces in keys.
Rejected Alternatives: ScriptableObject-only constants requiring C# recompile, managed CSV tokenization, or binary-only tuning.
Scalability potential: Designers can author Low/Middle/High/Ultra numeric curves without changing code; runtime consumes the same blittable tuning DTO.
Hardware Impact: Hot path cost is 0. Cold/editor parser avoids managed token churn; estimate: 20-200 us saved per reload vs split-based parsing, plus avoided GC.

## Decision 10 - Compile-Wall Containment
Problem: The new kinetic solver must not create a direct dependency from the runtime job assembly into sibling Gameplay implementation types.
Solution: Removed direct `PlayerKinematicsHandTarget` usage from the KineticCharacter files. The solver now reads core/contracts buffers plus SDF and receives presentation/tool/damage scalars through the player bridge.
Rejected Alternatives: Passing `PlayerKinematicsHandTarget` arrays into kinetic jobs; that makes animation depend on Gameplay internals and damages compile isolation.
Scalability potential: The same solver can run for player or humanoid NPCs by swapping Vault inputs, not by importing each owner implementation.
Hardware Impact: Runtime cost unchanged; compile-wall protection reduces iteration churn and prevents cross-domain rebuild cascade.

## Decision 11 - Rollback Frame Fence Tightening
Problem: The current producer of `LockstepPlayerKinematicState.Frame` can be backed by Unity `Time.frameCount`, and the swim presentation bridge also used `Time.frameCount` for duplicate-call filtering.
Solution: Kinetic animation ignores the producer frame for its DTO and uses the runtime-owned `_frameCounter` passed into every Burst job. The swim bridge now filters by `HectonArenaAllocator.CurrentFrameSequence` with a local monotonic fallback when the arena frame sequence is unavailable. Player AUP, velocity, and forward are consumed as data; frame identity is owned by this solver lane.
Rejected Alternatives: Trusting the producer frame or Unity frame counter blindly; both let Unity-time values leak into rollback-facing animation telemetry or the parameter feed.
Scalability potential: Low/Middle/High/Ultra paths share one monotonic solver frame source, so quality shedding never changes state identity.
Hardware Impact: Runtime cost is 0 us. Determinism risk is reduced without extra memory or branching.

## Decision 12 - Editor Facade Compile Guard
Problem: The tuner window must never leak UnityEditor/UI Toolkit dependencies into player runtime import if a future folder move or asmdef change occurs.
Solution: Wrapped `KineticCharacterAnimationTunerWindow` in `#if UNITY_EDITOR` in addition to placing it under `Assets/_Project/Scripts/Editor`.
Rejected Alternatives: Relying only on the folder convention; acceptable in Unity, but weaker than an explicit compile guard.
Scalability potential: No runtime path affected. Designers retain cold slider/CSV control across tiers.
Hardware Impact: Runtime cost is 0 us; prevents accidental player-build editor reference churn.

## Decision 13 - Editor Readout Formatting Hygiene
Problem: The tuner readout initially used managed numeric formatting in the editor update callback.
Solution: Replaced `ToString("0.000")` with quantized millivalue caching and manual digit composition, updating the label only when matrix count or quality bucket changes.
Rejected Alternatives: Leaving standard string formatting in a repeated editor callback; editor-only, but it normalizes bad habits in a system whose runtime contract is zero-GC.
Scalability potential: No runtime path affected. Designers still see quality from 0.000 to 1.000.
Hardware Impact: Runtime cost is 0 us. Editor allocation pressure is reduced during play-mode tuning.

## Decision 14 - GPU Constant Dirty Fence Correction
Problem: Static review found the GPU constant dirty check compared the previous `_activeCharacterCount` to the uploaded count before assigning the active count from latest telemetry.
Solution: Compute `activeCharacters` from the latest telemetry entry first, compare that value to `_uploadedCharacterCount`, then assign `_activeCharacterCount`.
Rejected Alternatives: Waiting for runtime symptoms; the bug is visible by code inspection and could leave shader globals stale when matrix count stays constant but active character count changes.
Scalability potential: Low/Middle/High/Ultra all publish correct active-character shader scalars without forcing extra buffer uploads.
Hardware Impact: Runtime cost is unchanged. Correctness improves without extra memory or branches beyond the existing dirty predicate.

## Decision 15 - Binary Ledger Lane And Rsqrt Tightening
Problem: The binary payload ledger did not name SHINOBU_136 despite the new CSV-to-Vault tuning bridge, and two hot math sites used `math.sqrt` where guarded `rsqrt` form is enough.
Solution: Added the SHINOBU_136 kinetic matrix payload lane to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. Replaced velocity magnitude and two-bone target distance with finite-guarded `math.rsqrt` expressions.
Rejected Alternatives: Leaving the route only in the agent log; stable architecture docs are the project memory. Keeping sqrt because it is readable; the mandate specifically favors guarded reciprocal-square-root patterns for hot Burst math.
Scalability potential: The ledger states the low-to-ultra matrix route and keeps the CSV bridge discoverable for future agents. Rsqrt keeps low-tier ALU pressure marginally lower while preserving the same high-tier pose path.
Hardware Impact: Expected micro-gain is sub-5 us for one player but removes a bad math precedent from the solver. Runtime profiler proof remains pending.

## Decision 16 - FABRIK Angle Reconstruction Rsqrt Sweep
Problem: A static post-polish scan still found one `math.sqrt` in the hot two-bone FABRIK angle reconstruction, even though it was guarded with `math.max(0f, ...)`.
Solution: Replaced the final sqrt with `sinSq * math.rsqrt(math.max(sinSq, 0.000001f))`, preserving zero output for a fully extended limb and keeping NaN vaccination explicit.
Rejected Alternatives: Leaving the guarded sqrt because it is mathematically safe; acceptable math still becomes bad precedent in a domain that explicitly mandates guarded rsqrt patterns in Burst hot loops.
Scalability potential: Low-tier and thermal-throttled paths now avoid all sqrt instructions in the SHINOBU kinetic solve scan; Middle/High/Ultra keep identical pose topology and only increase active bones/iterations through `GlobalQualityWeight`.
Hardware Impact: Expected gain is sub-2 us for one player on i3/MX350 class hardware, but it reduces scalar sqrt pressure and keeps the Burst kernel easier to audit. Build/profiler proof remains blocked by the CPU gate.

## Decision 17 - Attribute-Tolerant Prompt Extraction
Problem: A narrow prompt extraction regex that expected `<AGENT_PROMPT id="SHINOBU_136">` failed because the batch tag also contains `role` and `chat_name` attributes.
Solution: Re-extracted with `<AGENT_PROMPT[^>]*id="SHINOBU_136"[^>]*>[\s\S]*?</AGENT_PROMPT>` and reconfirmed 20 task entries from disk.
Rejected Alternatives: Trusting compressed chat state or exact XML tag shape; both violate the batch prompt protocol when attributes change.
Scalability potential: No runtime path affected. It prevents adjacent-agent task contamination during future polish passes.
Hardware Impact: Runtime cost is 0 us.

## Decision 18 - Local Denominator Guard Hardening
Problem: The SDF sampler divided by `SdfCellSize` after caller-side sanitization, and a few reciprocal sites were safe by prior branch but not locally guarded at the operation.
Solution: Moved the guard into the math site: `SdfCellSize` is clamped with `math.max(math.abs(...), 0.0001f)` before reciprocal; brace weight, active telemetry inverse, and quaternion normalization now carry explicit denominator guards at point of use.
Rejected Alternatives: Trusting serialized field ranges and upstream clamps; that is fragile under hot reload, prefab corruption, or future job reuse.
Scalability potential: Low/Middle/High/Ultra all consume the same finite SDF path. Low quality still drops gradient taps; high quality keeps gradient normals without accepting divide-by-zero risk.
Hardware Impact: Expected runtime delta is below 1 us for one player. The real gain is preventing one NaN from entering matrix/telemetry/GPU upload paths during long endurance runs.

## Decision 19 - UnsafeUtility Offset Proof
Problem: The editor facade verified DTO offsets with `Marshal.OffsetOf`, while the mandate asks for `UnsafeUtility` layout proof.
Solution: Replaced the editor helper with `UnsafeUtility.GetFieldOffset(FieldInfo)`. Runtime `KineticCharacterAnimatorLayout.Validate()` remains size-only to avoid reflection in the runtime boot/tick guard.
Rejected Alternatives: Moving reflection offset checks into runtime `Validate()`; it would pollute a cold-but-runtime path called by `EnsureVaultBuffers()`.
Scalability potential: No runtime path affected. The editor proof now matches the native layout mechanism used by Unity/Burst.
Hardware Impact: Runtime cost is 0 us; editor-only reflection occurs only in the tuner layout report.

## Decision 20 - Serialized Matrix Runtime On Player Prefab
Problem: The player swim bridge still had a hidden play-mode `AddComponent<KineticCharacterAnimatorRuntime>()` fallback when the prefab lacked a serialized kinetic runtime component.
Solution: Added the kinetic matrix runtime MonoBehaviour to `Player.prefab`, wired `kineticMatrixRuntime` to its fileID, and reduced `EnsureKineticMatrixRuntimeCold()` to an existing-component `TryGetComponent` lookup only.
Rejected Alternatives: Keeping runtime component creation as a convenience fallback; it is a standard Unity scene mutation path and makes the matrix owner depend on play-mode bootstrap order.
Scalability potential: Low/Middle/High/Ultra all start from the same serialized Vault owner. Weak hardware avoids surprise cold component creation; high-tier runs the same matrix path without prefab ambiguity.
Hardware Impact: Runtime hot path unchanged. Cold startup avoids one component allocation/serialization mutation route and removes a failure mode where animation silently fails until `AddComponent` runs.

## Decision 21 - Unmanaged GPU Upload Contract
Problem: The GPU matrix upload helper used `where T : struct` while performing raw `UnsafeUtility.MemCpy` into a locked `GraphicsBuffer`; that type contract is weaker than the memory operation and could admit managed-field structs in future edits.
Solution: Changed `CreateStructuredLockBuffer`, `UploadNativeArray`, and `ResolveSafeWriteCount` to `where T : unmanaged`. The current route uploads only `float4x4`, so behavior is unchanged while the compiler now enforces the raw-memory contract.
Rejected Alternatives: Keeping `where T : struct` and relying on code review; that leaves the unsafe boundary dependent on discipline instead of type rules. Adding runtime reflection checks; that adds cold complexity without protecting Burst/memcpy callers as cleanly.
Scalability potential: Low/Middle/High/Ultra all use the same matrix upload fence. It protects future expanded bone payloads and secondary matrix streams without adding tier-specific branches.
Hardware Impact: Runtime cost is 0 us. It prevents a future managed-payload upload regression that would otherwise surface as compile/runtime instability rather than a clean type error.

## Decision 22 - Active Tool Hash Preservation
Problem: `SubmitToolPose(..., toolHash)` accepted the Equipment active-tool identity but the frame DTO discarded it before the Burst solver, leaving tool-specific grip behavior and rollback state hash blind to a real input fact.
Solution: Added `ActiveToolHash` to `KineticCharacterFrameInputDTO`, padded the DTO to 272 bytes, set `InputFlagToolHashValid`, used the hash for deterministic secondary support-grip bias in `ProceduralLocomotionPhaseJob`, and included the hash in the animation `StateHash`.
Rejected Alternatives: Importing Equipment runtime grip tables directly; that would create a sibling-domain dependency. Ignoring the hash because pose matrix is enough; that loses one fact at the boundary and makes two tools with the same pose indistinguishable to telemetry/hash proof.
Scalability potential: Low quality keeps a cheap scalar support grip; higher quality increases the left-hand support blend through the existing quality curve without changing pipeline or adding managed lookups.
Hardware Impact: Runtime cost is sub-1 us for one player: a few scalar hash-derived math operations. It removes a correctness blind spot rather than claiming measurable frame-time gain.

## Decision 23 - Active Tool Hash Producer Cache
Problem: After preserving `ActiveToolHash` inside the kinetic DTO, the producer bridge still submitted literal `0u`, so the solver's tool-specific path would remain inert in real play.
Solution: Cached the active tool hash in `PlayerToolManager` at equip/despawn boundaries and exposed the cached value through `CurrentActiveToolHash`. `PlayerSwimPresentationController` now submits that cached hash to `KineticCharacterAnimatorRuntime`; when item persistent-id is absent, the cold owner uses `LocHash.Compute(metadata.toolID)` rather than the existing `RuntimeToolId` route.
Rejected Alternatives: Computing `LocHash.Compute(persistentId)` inside the per-frame swim presentation bridge; that puts string/hash work on the animation producer path. Falling back to `RuntimeToolId`; existing tool code can derive it through `Animator.StringToHash`, which is not acceptable as a kinetic identity source. Importing Equipment runtime tables into the Burst solver; that violates compile-wall isolation and turns one pose fact into a sibling-domain dependency.
Scalability potential: Low/Middle/High/Ultra all use one scalar hash route. Low keeps cheap support-grip bias; higher tiers can spend quality weight on stronger off-hand stabilization without adding managed lookups.
Hardware Impact: Expected hot-path gain versus per-frame hashing is sub-5 us on i3/MX350 class hardware. The primary value is making Task 11 real at the producer boundary without adding GC or direct Equipment solver dependency.

## Decision 24 - DataVault Hot-Swap GPU Binding Fence
Problem: On `DataVault` service replacement, the kinetic runtime completed jobs and cleared Vault handles, but GPU skinning globals/material bindings could still reference the previous matrix buffer until a later upload/clear path.
Solution: Call `ClearGpuSkinningBinding()` immediately after `ClearHandles()` in the DataVault hot-swap handler before reacquiring buffers or regenerating the emergency mock rig.
Rejected Alternatives: Waiting for the next `LateFrameTick` upload decision; that can present stale matrices across a Vault swap/origin-reset boundary. Releasing the graphics buffers on every Vault replacement; heavier than required because the GPU buffer objects can remain allocated while their binding is invalidated.
Scalability potential: Low/Middle/High/Ultra share the same explicit stale-binding fence. It prevents weak devices from rendering stale character matrices after memory pressure or registry rebinds and keeps high-tier shader globals coherent.
Hardware Impact: Steady-state runtime cost is 0 us. Hot-swap cost is a few shader/material scalar writes, acceptable because the event is not per-frame.
