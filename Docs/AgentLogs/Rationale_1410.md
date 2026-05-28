# Rationale 1410 - Dispatcher Phase Alignment

Date: 2026-05-28
Status: PENDING VERIFICATION

## Decision 001 - Discovery Before Mutation

Problem: Presentation writes can be embedded in many managers and callback styles. Editing before a complete static map risks partial fixes and new phase drift.
Solution: Run a scriptable static scan over `Assets/_Project/Scripts` before any runtime C# mutation. Ledger output will drive a bounded hit list.
Rejected Alternatives: Manual browsing first; too easy to miss helper methods called by `Tick` or legacy Unity callbacks.
Scalability potential: Low/Middle/High/Ultra all benefit from deterministic phase ownership; high tiers spend extra work in `VISUAL_SYNC`, not simulation.
Hardware Impact: i3/MX350 gains from consolidated GPU/audio/haptic submissions at frame end; estimated CPU win is PENDING VERIFICATION until profiler proof.

## Decision 002 - Build Throttling

Problem: The batch directive forbids frequent `dotnet build` runs and the host may have competing compiler load.
Solution: Use static text/AST scans for Tasks 01-14. Run a single final build only after checking CPU load and `csc.exe` absence.
Rejected Alternatives: Compile after every small edit; wastes host CPU and violates resource discipline.
Scalability potential: Preserves shared machine throughput for 20+ agents while still allowing final proof.
Hardware Impact: Prevents avoidable CPU contention on the host; game runtime impact is none.

## Decision 003 - Candidate-Filtered Static Parser

Problem: A naive full-file call graph scan over every C# body timed out and produced false method nodes from control statements.
Solution: Use a candidate-filtered parser over files containing presentation/readback tokens, mask comments and strings, exclude control statements, and traverse same-file call tokens from `Tick`, `FixedTick`, and `SimulationTick` only. Proof artifact: `Docs/Reports/DISPATCHER_PHASE_DEPENDENCY_SCAN_1410.json`.
Rejected Alternatives: Treat broad regex hits as facts; rejected because it misreported `if (...)` as a method and would drive false refactors.
Scalability potential: Low/Middle/High/Ultra receive the same deterministic phase contract. The scanner cost is offline-only and does not tax runtime.
Hardware Impact: 111605207 microseconds offline static verification. Runtime impact is zero.

## Decision 004 - Voxel Chunk Fade Overflow Fail-Closed

Problem: `HectonVoxelStreamingBridge.RegisterChunkFade` flushed pending fade registrations when its fixed queue filled. Because `SpawnCaveAsync` is launched from `Tick`, that overflow path could reach `Material.SetFloat` before `LateFrameTick`.
Solution: Remove the inline flush. On queue overflow, emit numeric telemetry `ChunkFadePendingQueueFullWarningHash` and skip the optional dissolve registration. Normal registration still flushes in `LateFrameTick`.
Rejected Alternatives: Grow the queue or allocate a managed overflow list; rejected because it violates zero-GC state buffering. Force an immediate flush; rejected because it is the phase violation.
Scalability potential: Low tier drops optional fade when saturated. Middle/high/ultra keep the same queue route and can raise configured capacity later without changing simulation truth.
Hardware Impact: i3/MX350 avoids mid-simulation material submission and driver work. Estimated saved stall is bounded but profiler-unverified; static win is removal of 2 simulation-reachable presentation chains.

## Decision 005 - Evidence-Gated Non-Edits

Problem: The batch names shader globals, haptics, audio, and MPB paths, but broad edits without a runtime violation would create refactoring churn and risk new phase bugs.
Solution: After the corrected scanner reported zero runtime simulation-reachable shader/audio/haptic/readback chains, only the confirmed material fade overflow was changed. Existing systems such as `SpatialAudioManager`, `HectonMusicDirector`, `OrbitalRelativityDirector`, and `InteractionHighlighter` already stage dirty flags/primitive fields and flush in `LateFrameTick`.
Rejected Alternatives: Rewrite already-deferred systems to satisfy the wording of the batch; rejected because objective disk state already satisfies the phase rule.
Scalability potential: Low/Middle/High/Ultra keep deterministic simulation. Presentation systems remain free to scale fidelity in `LateFrameTick`/`VISUAL_SYNC`.
Hardware Impact: Avoids unnecessary code churn and prevents extra registration or delegate overhead. Runtime allocation delta from this decision is 0 bytes.

## Decision 006 - Continuous Fade Presentation Weight

Problem: The chunk fade presentation path was late-frame correct after the overflow fix, but the presentation curve did not explicitly consume continuous `GlobalQualityWeight`.
Solution: `TickChunkFade`, which is only called from `LateFrameTick`, now resolves `HomeostasisBrain.GlobalQualityWeight` and blends linear fade with smoothstep fade through `math.lerp(fade01, smoothFade01, quality01)`.
Rejected Alternatives: Binary low/high quality branches; rejected because HECTON-8 requires continuous scaling. Physical voxel fade simulation; rejected because a material dissolve scalar is the correct cinematic cheat.
Scalability potential: Low = linear cheap dissolve; Middle = partial smoothing; High = smoother curve; Ultra = same branchless curve, preserving cycles for additional visual density elsewhere.
Hardware Impact: Adds a few scalar operations only inside the presentation loop. No simulation truth or streaming authority changes. No managed allocation tokens in the modified hot methods.

## Decision 007 - Build Gate Blocked By Host Contention

Problem: Task 15 asks for one final build, but the compile resource throttle rule forbids starting a build while CPU is above 50% or another compiler is active.
Solution: Sampled CPU and compiler processes to `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json`. CPU was 100% and active `dotnet` PID 40436 existed, so build was not launched.
Rejected Alternatives: Run `dotnet build` anyway to satisfy optics; rejected because it violates the user directive and project resource law.
Scalability potential: Preserves shared host capacity for sibling agents and avoids adding build contention to an already saturated machine.
Hardware Impact: Development-host protection only; runtime impact is none.

## Decision 008 - One-Frame Latency Mask Proof

Problem: Presentation deferral can be mistaken for input or simulation latency if its boundary is not stated.
Solution: By deferring `Shader.SetGlobal` to `VISUAL_SYNC`, the CPU completes its simulation math uninterrupted. The GPU driver overhead is consolidated at the end of the frame, preventing mid-simulation thread stalls. The 16.67ms latency is imperceptible to the player but provides critical timing stability for the physics solver on the i3 architecture.
Rejected Alternatives: Same-frame mid-simulation material upload; rejected because it couples driver work to simulation cadence.
Scalability potential: Low/Middle devices gain more stable frame pacing; High/Ultra devices spend the stable late-frame window on richer visual submission.
Hardware Impact: i3/MX350 avoids a known class of mid-frame render-state stalls; exact microsecond gain requires profiler capture and is not claimed beyond static elimination of 2 chains.

## Decision 009 - APEX Domain-Wide Direct Token Audit

Problem: The first final report proved same-file simulation reachability, but the repeated APEX order required a deeper check of diegetic UI, shader dispatchers, and `Execute` methods so no direct presentation token remained hidden outside the call graph.
Solution: Ran `Docs/Reports/DISPATCHER_APEX_PRESENTATION_SCAN_1410.json` over UI, Visor, Rendering, Graphics, World, and Core. It scanned 658 files and 378 root methods named `Tick`, `FixedTick`, `SimulationTick`, or `Execute`; candidate count was 0.
Rejected Alternatives: Rely only on the earlier dependency scan; rejected because it did not explicitly state the direct `Execute`/UI surface.
Scalability potential: Confirms no direct UI/GPU/audio/haptic submission is happening in the audited root methods, preserving late-frame scaling lanes for weak, middle, high, and ultra hardware.
Hardware Impact: Offline scan cost 8892650 microseconds. Runtime impact is none.

## Decision 010 - APEX Extractor Correction

Problem: The previous report/test extractor matched call sites before method declarations, so `RegisterChunkFade`, `TickChunkFade`, and `ResolveChunkFadeQualityWeight01` proof rows could point at callers instead of method bodies.
Solution: Anchor the test extractor to explicit C# method declarations and regenerate report evidence from declaration lines. The corrected proof now reports `RegisterChunkFade` at line 456, `TickChunkFade` at line 526, `ResolveChunkFadeQualityWeight01` at line 667, and Homeostasis route methods by declaration.
Rejected Alternatives: Keep the earlier report and explain it in chat; rejected because a broken proof artifact is not evidence.
Scalability potential: Offline proof quality does not change runtime, but it prevents future false acceptance of phase violations on low, middle, high, and ultra hardware.
Hardware Impact: Runtime impact is zero. Offline rescan cost was 38366137 microseconds for the whole `Assets/_Project/Scripts` tree.

## Decision 011 - Homeostasis PreSimulation Shader Deferral

Problem: The corrected audit found a real cross-partial route: `HomeostasisBrain.PreSimulationTick -> ApplyPressurePolicy -> ApplyDictatorPressurePolicy` could publish scalability shader globals through `RefreshMathLodLowScalar`, `UpdateCullingMultiplier`, and `PublishQualityShaderGlobals`.
Solution: Replace PreSimulation shader publication with primitive dirty flags and float payload fields in `HomeostasisBrain.ScalabilityDictator`; add `HomeostasisBrain.FlushVisualSyncShaderState()` and call it from the SystemDispatcher visual-sync lane.
Rejected Alternatives: Leave scalability globals in PreSimulation because they are "only scalars"; rejected because `Shader.SetGlobalFloat` is still presentation upload and violates `ARCH_Execution_Phases`.
Scalability potential: Low/middle/high/ultra keep identical quality truth ownership while the GPU upload phase is isolated. Dirty flags avoid uploading unchanged quality/culling scalars.
Hardware Impact: Adds primitive flag checks in visual sync only. Removes PreSimulation driver submission risk; profiler microseconds are not claimed without Unity capture.

## Decision 012 - Key-Only FixedList Fade Transfer

Problem: The late-frame chunk fade transfer no longer stored a pending `GameObject[]`, but the intermediate proof still used a managed `long[]` key array. That was zero steady-state allocation but not a strict enough answer to the heap-transfer mandate.
Solution: Replace `_pendingChunkFadeKeys` with `Unity.Collections.FixedList512Bytes<long>`. `RegisterChunkFade` stores only the cave key with `AddNoResize`; `FlushPendingChunkFadeRegistrations` resolves `GameObject` method-local through `TryGetActiveVolume` inside `LateFrameTick`; `ClearPendingChunkFadeRegistrations` calls `Clear()`.
Rejected Alternatives: Keep `long[]` because it allocates only cold; rejected because the coordinator asked for no heap transfer surface. Store `GameObject` references; rejected because presentation queue state must not persist managed object aliases across phases.
Scalability potential: Low drops optional fade on saturation; middle/high/ultra can increase capacity later by choosing a larger fixed-list type or DataVault buffer without changing simulation truth.
Hardware Impact: Removes one managed array field and one managed reference lane from the deferred transfer path. Runtime microseconds are not claimed; static token proof reports `_pendingChunkFadeVolumes` = 0 and `long[] _pendingChunkFadeKeys` = 0.

## Decision 013 - Readback Candidate Reclassification

Problem: The latest full parser reported one confirmed readback because same-file token traversal could not infer overload types in `HectonIndirectVegetationRenderer`.
Solution: Manually traced the root. `Execute` at line 804 calls `IsVisibleInDarkness(float3)` at line 867. The readback route belongs to the outer `IsVisibleInDarkness(Vector3)` at line 4577 and `ResolveBiolumIntensityScalar` at line 3221, which are not called by the Burst job body. Report classification changed to `FALSE_POSITIVE_OVERLOAD_CHAIN_TOKEN_TRAVERSAL`.
Rejected Alternatives: Leave the JSON with `confirmedReadbackFindings = 1`; rejected because it would falsely mark a GPU readback fault. Silence the candidate entirely; rejected because scanner limitations must remain visible.
Scalability potential: No runtime change. The static proof now separates real phase violations from scanner imprecision for weak, middle, high, and ultra hardware.
Hardware Impact: Offline verification only. Latest scan cost: 247460347 microseconds. Runtime impact is zero.
