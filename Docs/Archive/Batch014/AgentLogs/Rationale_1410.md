# Rationale 1410 - Dispatcher Phase Alignment

Date: 2026-05-28
Status: STATIC APEX RECHECK11 COMPLETE / BUILD BLOCKED BY CPU AND ACTIVE DOTNET

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

## Decision 014 - GlobalRegistry Math Precision Visual Sync Deferral

Problem: A bounded APEX recheck found a real core route: `SystemDispatcher.RunDispatcherUpdate` calls `FrameTimeWatchdog.TickMathPrecisionTransition`, which delegates to `GlobalRegistry.TickMathPrecisionTransition`. The unacceptable version of that route wrote `_H8MathLodLowBlend` with `Shader.SetGlobalFloat` before the dispatcher reached visual sync.
Solution: Keep math precision transition truth as primitive `int` state during dispatcher update. Stage `_pendingMathPrecisionShaderLevel`, `_pendingMathPrecisionShaderLowBlendMilli`, and `_mathPrecisionShaderDirty`; drain them through `GlobalRegistry.FlushMathPrecisionShaderState()` from `SystemDispatcher.RunDispatcherLateFrame`.
Rejected Alternatives: Move the entire transition ticker to LateFrame; rejected because C# math precision state is dispatcher authority and may be read as frame state. Leave the shader write in update because it is only one scalar; rejected because phase doctrine forbids presentation upload before visual sync.
Scalability potential: Low/middle/high/ultra keep the same continuous 60-frame math-precision ramp while GPU keyword/global publication is isolated to the presentation phase.
Hardware Impact: Removes a pre-simulation driver submission risk on i3/MX350. Runtime microseconds are not claimed without profiler proof. Static rg-first recheck cost: 1200105 microseconds.

## Decision 015 - DistanceMath Shader LOD Visual Sync Deferral

Problem: Recheck3 found a real missed route: `RuntimeWatchdog.Tick -> FrameTimeWatchdog.Tick -> DistanceMath.PushShaderMathLod` and `LODSystemManager.ApplyQualityPreset -> DistanceMath.PushShaderMathLod` reached `Shader.SetGlobalFloat` outside visual sync.
Solution: Convert `DistanceMath.PushShaderMathLod` into a staging API. It writes `_pendingShaderMode`, `_pendingShaderWeight`, and `_hasPendingShaderState`; `SystemDispatcher.RunDispatcherLateFrame` drains the upload through `DistanceMath.FlushVisualSyncShaderState()`.
Rejected Alternatives: Keep `DistanceMath` immediate because it is a low-level helper; rejected because helper APIs are still phase-visible when called by `Tick`. Move `FrameTimeWatchdog.Tick` to LateFrame; rejected because its quality truth and telemetry remain dispatcher/update authority.
Scalability potential: Low uses cheap math/far distance approximation; middle and high get continuous quality weights; ultra keeps visual-overkill shader state without changing gameplay truth.
Hardware Impact: Removes a pre-visual-sync shader global/keyword upload route. Transfer is enum + float + bool only. Static recheck3 scan cost: 466615 microseconds.

## Decision 016 - Logistics Highlight LateFrame Staging

Problem: `LogicSpannerTool.OnEquip/OnUnequip` called `ConnectionSplineBatchRenderer.SetLogisticsPathHighlightActive`, which directly wrote `_HectonLogisticsPathHighlight` through `Shader.SetGlobalFloat` during a gameplay event.
Solution: Stage the requested highlight state in `s_pendingLogisticsPathHighlightActive` plus `s_logisticsPathHighlightDirty`. `ConnectionSplineBatchRenderer.LateFrameTick` calls `FlushLogisticsPathHighlightState()` and performs the single shader upload there.
Rejected Alternatives: Treat OnEquip/OnUnequip as harmless lifecycle; rejected because equipment events occur during gameplay and can be driven by input cadence. Use a managed delegate/event queue; rejected because it would allocate or hide route ownership.
Scalability potential: Low/middle/high/ultra share one cheap shader bool route. Strong devices can spend visual budget in the renderer batches, not in event-time driver calls.
Hardware Impact: Removes one direct event-time GPU upload path. Runtime transfer adds two static bool fields only; no managed allocation tokens in stage/flush methods.

## Decision 017 - Build Gate Revalidated After Recheck3

Problem: After recheck3 code edits, a final build would be justified only if host contention cleared.
Solution: Sampled CPU and compiler processes immediately before the build decision. Latest sample was CPU 57% with active `dotnet` PID 30904 and `csc` PID 45636, so build remained blocked and no `dotnet build` was launched.
Rejected Alternatives: Start a second compiler anyway because static proof is strong; rejected because the explicit rule blocks when CPU is above 50% or when compiler processes are active.
Scalability potential: Development-host protection only; preserves shared throughput for parallel agents.
Hardware Impact: Runtime impact is none. Validation remains static until the compiler gate clears.

## Decision 018 - Spectrum Public Sonar Presentation Deferral

Problem: `SpectrumSystem.TriggerActiveSonarPing` could call `EmitSonarPulse`, which directly wrote sonar shader globals and could call `IAudioService.PlayStatic2D` through `TryPlayAbyssalAnchorReturn`. That route is player/input cadence, not guaranteed visual sync.
Solution: Stage sonar mode, primary/echo pulse, reveal, lidar, passive radar, acoustic mapping, and abyssal return audio into primitive/`Vector4` fields. `LateFrameTick` now calls `FlushQueuedSpectrumShaderGlobals()` and `FlushQueuedSpectrumAudio()` after visual state evaluation.
Rejected Alternatives: Treat sonar ping as harmless because it is "visual only"; rejected because shader/audio API calls still cross into presentation backends. Move gameplay sonar effects to LateFrame; rejected because energy drain, spatial signals, acoustic pings, and AUP discovery are gameplay/domain truth and must keep their owner route.
Scalability potential: Low tier keeps cheap screen-space pulse scalars and delayed audio. Middle/high/ultra keep the same queue but can spend visual currency inside the late-frame sonar shader payload, with `SpectrumSystem.ResolveActiveSonarGeoQualityWeight()` continuously reading `HomeostasisBrain.GlobalQualityWeight`.
Hardware Impact: i3/MX350 avoids event-time GPU/audio backend submission from active sonar. Runtime microseconds are not claimed without profiler capture. Static recheck4 hot rows report reference `new` 0, `string.Format` 0, `.ToString()` 0, LINQ 0, `foreach` 0.

## Decision 019 - Recheck4 Build Gate Blocked

Problem: Recheck4 edited `SpectrumSystem` and the editor regression tests, so a final build would be useful only if the host compilation gate allowed it.
Solution: Sampled CPU/processes before build. CPU was 90%, with active `csc` PID 63300 and `dotnet` PID 53008. Wrote `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json` and did not invoke `dotnet build`.
Rejected Alternatives: Start a build during compiler contention to satisfy optics; rejected by explicit batch and AGENTS throttling rule.
Scalability potential: Development-host protection only; preserves parallel agent throughput.
Hardware Impact: Runtime impact is none. Validation remains static until CPU <= 50% and compiler processes are absent.

## Decision 020 - Residual Feedback Routes Must Stage Value Payloads

Problem: Recheck5 found remaining non-shader presentation feedback routes outside strict late-frame ownership: active-sonar acoustic dispatch, fabricator audio/haptics, flashlight audio, submarine cavitation feedback, ballast air-release/flood feedback, music stingers, fauna procedural audio, critical structural haptics, and Manta spawn/despawn/unequip headlight clears.
Solution: Convert each route to private primitive/value-type pending state and drain through existing `ILateFrameTickable` owners. The only backend calls left in these domains are inside `Flush*`, `LateFrameTick`, or helper methods reached by late-frame flushes.
Rejected Alternatives: Managed delegate queues, `List<T>` feedback mailboxes, or immediate backend calls with "small cost" justification. All were rejected because they either allocate or violate phase sovereignty.
Scalability potential: Low tier drops or compresses optional presentation feedback without changing gameplay truth. Middle/high/ultra tiers keep the same route and can spend visual/audio/haptic budget inside late-frame dispatch windows.
Hardware Impact: i3/MX350 avoids event-time driver/audio/haptic backend entry. Exact runtime microseconds are not claimed without Unity profiler capture; recheck5 static scan cost was 12546036 us.

## Decision 021 - Recheck5 Zero-GC Proof Is Text Scan, Not Runtime Allocation Capture

Problem: The coordinator requested proof that modified hot paths contain no heap allocations or managed formatting/query constructs. A previous scan attempt was invalid because a regex masked most files after `//` comments.
Solution: Fix the scanner so block comments, line comments, strings, and chars are masked independently, then anchor method extraction to explicit C# method declarations with access modifiers. The final scan found 71/71 methods and 27/27 route methods.
Rejected Alternatives: Keep the broken `missingMethods=64` artifact or manually assert correctness. Rejected because missing method bodies are not evidence.
Scalability potential: Proof quality is offline-only, but it prevents heap-backed phase queues from entering low/middle/high/ultra presentation routes.
Hardware Impact: Final scan counts: reference `new` 0, value-type `new` 3, `string.Format` 0, `.ToString()` 0, LINQ 0, `foreach` 0. The three `new` tokens are struct payload construction only: `AcousticPingSignal` and `AudioPingTriggerPayload`.

## Decision 022 - DataVault Sovereignty Remains Spectrum-Owned

Problem: Recheck5 touched Spectrum active sonar and AUP discovery routes, so lock ownership and buffer identity had to be re-proven.
Solution: No new `GlobalDataVault` buffers were introduced. Existing Spectrum buffers remain `AupDiscoveryGridBufferId=(BufferID)71030` and `ActiveSonarGeoTelemetryRingBufferId=(BufferID)71031`; write locks at `SpectrumSystem.cs:2898`, `:2947`, and `:3712` release inside `finally` at `:2919`, `:2987`, and `:3742`.
Rejected Alternatives: Move late-frame feedback staging into `GlobalDataVault` as a global heap; rejected because same-owner private value fields are cheaper and preserve one owner/one route.
Scalability potential: Low tier can keep small private pending payloads; higher tiers can increase visual payload richness behind the same buffer IDs and late-frame telemetry ring without changing authority.
Hardware Impact: No new lock contention lane was added. Recheck5 only verifies existing lock/finally correctness and keeps presentation staging outside cross-domain vault ownership.

## Decision 023 - Build Gate Remains Hard-Blocked

Problem: After report regeneration, a final build would be useful but the host still violates the compilation throttle rule.
Solution: Sampled `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json` before any build attempt. CPU was 66%, with active `dotnet` PID 50672 and `VBCSCompiler` PID 28580, so `dotnet build` was not invoked.
Rejected Alternatives: Start a build to create a more comforting final answer. Rejected because the explicit rule forbids build under CPU > 50% or active compiler process.
Scalability potential: Development-host protection only; keeps parallel agent work from compounding compiler contention.
Hardware Impact: Runtime impact is none. Syntax validation remains static until CPU and compiler-process gates clear.

## Decision 024 - Late-Frame Feedback Payloads For Residual Gameplay Routes

Problem: A stricter corrected callgraph scan after recheck5 found more routes where `Tick`, `FixedTick`, or `PostFixedTick` could reach haptic/audio/notification backends through helper methods. The concrete roots were LifePod extinguisher spray haptics, mounted transport entanglement haptics/critical notification/structural-stress audio, ballast PID hull-stress fallback audio, and VR somatic velocity-anchor haptics.
Solution: Replace each direct backend call with owner-local primitive/value-type pending state and drain it from the existing or newly registered `LateFrameTick`. No delegate queue, `List<T>`, managed array, or cross-domain vault lane was introduced.
Rejected Alternatives: Treat haptics and notifications as "small" and leave them in simulation cadence; rejected because backend entry cost still belongs to presentation. Use a central managed feedback queue; rejected because it would allocate and hide ownership.
Scalability potential: Low tier keeps cheap one-frame-late haptic/audio fakes. Middle/high/ultra keep the same route and can spend richer presentation work inside late-frame without altering gameplay truth.
Hardware Impact: i3/MX350 avoids event-time haptic/audio backend calls from the affected roots. Static route scan after the patch reports 0 forbidden backend findings across 351 roots; profiler microseconds are not claimed.

## Decision 025 - Recheck6 Scanner Correction And Build Gate

Problem: A broad substring backend scan flagged `DroneFleetManager.HeadlessNativeBuffersCreated` through the text `DroneServiceCommandBuffers`. That is not `CommandBuffer` API usage and would be a false defect.
Solution: Tighten backend matching to expression-level patterns such as `CommandBuffer.` and `Graphics.` and rerun the full candidate scan. The corrected scan reports 259 candidate files, 24100 methods, 351 roots, and 0 forbidden backend findings.
Rejected Alternatives: Patch `DroneFleetManager` based on a substring false positive; rejected because it would be churn. Hide the initial false candidate; rejected because proof limitations must remain visible.
Scalability potential: Offline proof accuracy prevents unnecessary changes while preserving late-frame sovereignty for low, middle, high, and ultra hardware.
Hardware Impact: Static route scan elapsed 40881435 us. Build gate sample was CPU 47% with active `VBCSCompiler` PID 14544, so `dotnet build` was not invoked.

## Decision 026 - Transport One-Shot Audio Must Not Escape Tick/FixedTick

Problem: Expanded recheck7 backend scan added `IAudioService.PlayAtPoint`, `PlayStatic2D`, `QueueHullStressSignal`, and `AudioSource.Play/Stop` patterns. It found a real route in `MountablePlayerTransport`: `Tick -> ConsumeMountedInteractInputSignals -> DismountRider -> DismountRiderInternal -> PlayTransportOneShot` and `FixedTick -> ApplyMountedVehicleKinematics -> TryAdvanceMacroFloraEntanglement -> ... -> DismountRiderInternal -> PlayTransportOneShot`, both reaching `IAudioService.PlayAtPoint`.
Solution: Remove `PlayTransportOneShot`. Stage mount/dismount one-shot feedback as `TransportAudioOneShotRequest` containing `Vector3 Position`, `float Volume`, and `byte ClipKind`; call `FlushQueuedTransportAudio()` from `LateFrameTick`, where the byte selector resolves `mountSound` or `dismountSound` method-local before `IAudioService.PlayAtPoint`.
Rejected Alternatives: Store `AudioClip` inside the pending request; rejected because it would persist a managed reference across phase transfer. Use a managed queue for multiple one-shots; rejected because the route is optional feedback and single-slot coalescing is cheaper and zero-GC.
Scalability potential: Low devices get one-frame-late cheap audio feedback with no simulation backend call. Middle/high/ultra can spend richer transport audio mixing in the late-frame/audio domain without changing rider or damage authority.
Hardware Impact: i3/MX350 avoids event-time audio backend entry from both input and fixed-kinematic chains. Expanded route scan after the patch reports 266 candidate files, 24565 methods, 359 roots, 0 forbidden backend findings, elapsed 80548914 us.

## Decision 027 - LifePod Pending Haptic Cleanup Must Be Fail-Closed

Problem: `LifePodFireExtinguisherNozzle.TryUnregisterLateFrameTick` cleared pending haptic state only after a successful late-frame registration path. If registration was denied or bypassed, stale pending state could survive disable/unregister.
Solution: Keep the unregister call conditional on `_registeredLateFrame`, but always clear `_pendingSprayHapticDirty` and reset `_pendingSprayHaptic` afterward.
Rejected Alternatives: Leave stale state because it would probably be overwritten; rejected because fail-closed cleanup is required for deterministic presentation ownership. Allocate a new request object on enable/disable; rejected because the value payload already supports zero-GC reset.
Scalability potential: Low/middle/high/ultra all get deterministic cleanup; no quality-tier behavior changes.
Hardware Impact: Runtime cost is two field writes on unregister. Zero-GC scan reports `TryUnregisterLateFrameTick` has reference `new` 0, `string.Format` 0, `.ToString()` 0, LINQ 0, and `foreach` 0.

## Decision 028 - Recheck7 Build Gate Remains Blocked

Problem: After recheck7 edits, a final compilation check would be useful, but the explicit build throttle forbids build under active compiler/dotnet process.
Solution: Sampled the build gate before any build attempt. CPU was 45%, but `dotnet` PID 14652 was active, so `dotnet build` was not invoked. Wrote `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json`.
Rejected Alternatives: Run `dotnet build` because CPU is below 50%; rejected because the rule also forbids launching while another dotnet/compiler process is active.
Scalability potential: Development-host protection only; avoids stealing compile capacity from parallel agents.
Hardware Impact: Runtime impact is none. Syntax validation remains static until CPU <= 50% and no `dotnet`, `csc`, or `VBCSCompiler` process is active.

## Decision 029 - Notification Producers Must Not Register UI Text From SlowTick

Problem: Recheck8 found `Atlas6DirectiveSystem`, `EndingSystem`, and `FirstHourDirector` `SlowTick`/gameplay helper routes still calling notification registration/push APIs directly. `NotificationEvents` itself is deferred, but `RegisterMessage` mutates the global notification backing store before the UI phase.
Solution: Add owner-local fixed-char pending notification requests and flush `RegisterMessage` plus `TryPushRegistered*` only from each owner `LateFrameTick`. The transfer is bounded: four slots, 512 chars per slot, no managed queue/list/delegate.
Rejected Alternatives: Leave direct `NotificationEvents.TryPush*` because it eventually queues; rejected because global message registration is still presentation-owned state. Use a central managed notification mailbox; rejected because it hides ownership and can allocate.
Scalability potential: Low devices get one-frame-late UI hints with bounded payloads. Middle/high/ultra can render richer notification presentation in the UI lane without touching simulation cadence.
Hardware Impact: i3/MX350 avoids notification message registration from slow simulation cadence. Static zero-GC scan reports reference `new` 0, `string.Format` 0, `.ToString()` 0, LINQ 0, `foreach` 0 for the modified notification queue/flush methods.

## Decision 030 - Residual Material And Hull-Stress Backends Must Stay In LateFrame

Problem: `HectonVoxelStreamingBridge.QueueVolumeDespawn` still flushed despawns when the fixed despawn queue filled, which could call `ReleaseChunkFadeMaterial -> Material.SetFloat` from `SlowTick`. `SubmarineAutoLevelBallastController.EmitPidHullStressSignal` still tried `IAudioService.QueueHullStressSignal` directly from the post-fixed PID route before falling back to the late-frame queue.
Solution: Make voxel despawn saturation fail closed with numeric telemetry instead of inline flushing. Make ballast PID hull stress always stage `HullStressSignal` and let `FlushDynamicFloodFeedback` publish the procedural audio signal from `LateFrameTick`.
Rejected Alternatives: Grow the despawn queue; rejected as heap/complexity. Keep the audio-service fast path; rejected because the same signal already has a late-frame owner route.
Scalability potential: Low tier drops stale-volume despawn overflow for one frame and keeps structural audio cheap. Middle/high/ultra retain the same late-frame presentation route and can spend extra budget on richer dissolve/audio rendering.
Hardware Impact: Removes a material-state upload route and an audio-service backend route from simulation phases. Profiler microseconds are not claimed; targeted route scan reports 0 forbidden backend findings over the recheck8 target set.

## Decision 031 - Player Action Audio Requires A No-Audio Consumable Truth Path

Problem: `PlayerActionController.Tick -> CompleteAction` and cancellation could reach `IAudioService.PlayAtPoint`; immediate consumables also called `ConsumableItem.TryConsume(item, _audioService)`, which could reach `PlayStatic2D`.
Solution: Add `ConsumableItem.TryConsumeWithoutAudio` for gameplay truth, then stage action completion/cancel audio in `PlayerActionController` and flush it from `LateFrameTick`.
Rejected Alternatives: Drop custom `ItemData.useSound`; rejected because it loses authored feedback. Convert every item clip to an `AudioEvent` table id in this patch; rejected because no stable authored ids exist on `ItemData` and broad content migration is outside a safe dispatcher fix.
Scalability potential: Low devices get one-frame-late action feedback; higher tiers can spend audio mix complexity in the audio lane. The solution does not alter inventory, survival, or save truth.
Hardware Impact: No heap allocation tokens in the modified action audio methods. Known limitation: the pending action audio payload keeps an existing `AudioClip` asset reference to preserve authored `useSound`; this is zero-GC by token scan but not an unmanaged DTO.

## Decision 032 - Recheck8 Build Gate Blocked

Problem: After recheck8 edits, a final build would be justified only if the explicit CPU/compiler gate allowed it.
Solution: Sampled `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json`; CPU was 100% and two compiler/dotnet processes were active, so `dotnet build` was not invoked.
Rejected Alternatives: Run the build to create a stronger-looking final answer; rejected because it directly violates the compilation resource throttle.
Scalability potential: Development-host protection only.
Hardware Impact: Runtime impact is none. Syntax validation remains static until CPU <= 50% and no `dotnet`, `csc`, or `VBCSCompiler` process is active.

## Decision 033 - Player Action Audio Payload Must Be Unmanaged

Problem: Recheck8 moved `PlayerActionController` audio backend calls into `LateFrameTick`, but `_pendingActionAudioClip` still stored a managed `AudioClip` reference between Tick and LateFrame. That satisfied no-heap token scans but failed the stricter unmanaged phase-transfer requirement.
Solution: Replace the pending fields with `ActionAudioRequest`, explicit layout size 32: `Vector3 Position` at offset 0, `uint EventId` at 12, `uint ItemHash` at 16, `byte ClipKind` at 20, `byte Dirty` at 21, and explicit padding at 22/24/28. Resolve `AudioClip` only during `FlushQueuedActionAudio`; if an authored `UseAudioEventId` exists, route through `IAudioService.QueueAudioEvent`.
Rejected Alternatives: Keep `AudioClip` in the pending request and call it "zero allocation"; rejected because it is not an unmanaged DTO. Drop legacy `ItemData.useSound`; rejected because it would silently remove authored feedback. Add a central managed action-audio queue; rejected because it hides ownership and can allocate.
Scalability potential: Low tier carries one 32-byte request and can use generic clip fallback. Middle/high/ultra can author `UseAudioEventId` entries that resolve to richer SpatialAudioManager table content without changing gameplay truth.
Hardware Impact: Static scan reports stale managed pending audio clip matches 0 and backend calls outside `FlushQueuedActionAudio` 0. Runtime microseconds are not claimed without profiler capture.

## Decision 034 - Action Audio Must Consume Continuous Quality

Problem: The recheck9 audio payload fix initially had no explicit continuous `GlobalQualityWeight` consumption in the new presentation flush path.
Solution: Add `ResolveActionAudioPresentationVolume()`, reading `HomeostasisBrain.GlobalQualityWeight`, finite-saturating it, and applying `math.lerp(0.75f, 1f, quality)` to both `CoreAudioEvent` and `PlayAtPoint` paths.
Rejected Alternatives: Use `if (isLowEnd)` or a tier enum; rejected by the continuous scalability pillar. Scale gameplay consumption duration or item effects; rejected because quality weight must not alter gameplay truth, save identity, or DTO authority.
Scalability potential: Low devices get quieter cheap feedback while preserving timing; middle/high/ultra can map authored event IDs to richer presentation content in the audio lane. No binary low-end switch was introduced.
Hardware Impact: Zero-GC recheck9 includes `ResolveActionAudioPresentationVolume` and reports reference `new` 0, `string.Format` 0, `.ToString()` 0, LINQ 0, `foreach` 0.

## Decision 035 - Recheck9 Build Attempt Timed Out

Problem: After recheck9 code edits, the build gate briefly allowed one final compile attempt: CPU 36%, active compiler/dotnet count 0.
Solution: Ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` once. It timed out after 300000 ms with exit code 124 and did not produce a compile verdict. Confirmed remnant PIDs 17856 and 34600 were the same `Hecton8.slnx` build command and stopped them. Current report records `PENDING_CLI_COMPILE_TIMEOUT`.
Rejected Alternatives: Immediately run a second build; rejected because the first attempt already consumed five minutes and repeating would violate compilation resource throttling intent. Claim green build from silence; rejected because no compile verdict exists.
Scalability potential: Development-host protection only.
Hardware Impact: No runtime impact. Static verification remains current proof; syntax remains pending CLI compile until a clean build window is explicitly available.

## Decision 036 - Player Action Camera Bob Must Be LateFrame-Owned

Problem: Recheck10 found that `PlayerActionController.Tick` still called `CameraJuiceProcessor.RegisterActionBob`, and action cancel/complete routes still called `ClearActionBob`. Camera bob is presentation/camera juice, not gameplay truth, so direct mutation during action simulation cadence violates the phase contract.
Solution: Add `ActionCameraBobRequest`, explicit layout size 16: `float Intensity` at offset 0, `float Frequency` at 4, `byte Command` at 8, and explicit padding through byte 15. `Tick` now only stages apply/clear requests; `LateFrameTick` drains `FlushQueuedActionCameraBob` and resolves `CameraJuiceProcessor` method-local.
Rejected Alternatives: Keep direct camera juice calls because camera movement is user-visible; rejected because presentation feedback belongs in late-frame. Store a managed command object; rejected because phase transfer must be unmanaged and zero-GC.
Scalability potential: Low uses 0.65x action bob intensity, middle/high interpolate continuously, ultra reaches 1.15x intensity through `HomeostasisBrain.GlobalQualityWeight` without changing action duration or inventory truth.
Hardware Impact: Removes camera feedback backend entry from action `Tick`. Exact runtime microseconds are not claimed without profiler capture; zero-GC scan reports reference `new` 0 for all new camera bob methods.

## Decision 037 - Battery Charger Insert Audio Must Not Publish From InsertBattery

Problem: `BatteryCharger.InsertBattery` updated slot truth and immediately called `IAudioService.PlayAtPoint`. The operation can be invoked from gameplay interaction cadence, so direct audio backend entry there violates late-frame feedback ownership.
Solution: Make `BatteryCharger` implement `ILateFrameTickable` and add `ChargerAudioRequest`, explicit layout size 16: `Vector3 Position` at offset 0, `byte ClipKind` at 12, `byte Dirty` at 13, `ushort Reserved0` at 14. `InsertBattery` now queues the byte-selected request; `FlushQueuedChargerAudio` resolves `insertSound` and calls `PlayAtPoint` from `LateFrameTick`.
Rejected Alternatives: Store the `AudioClip` in the pending request; rejected because it persists a managed reference across phase transfer. Route through `GlobalDataVault`; rejected because same-owner single-slot feedback is cheaper and does not need cross-domain ownership.
Scalability potential: Low uses 0.70x charger insert volume, middle/high interpolate continuously, ultra reaches 1.0x through `HomeostasisBrain.GlobalQualityWeight`. Gameplay charge and inventory slot truth are unchanged.
Hardware Impact: Removes one audio backend call from the insert route. Exact method extraction reports `BatteryCharger.InsertBattery` `PlayAtPoint(` count 0; backend call remains only in `FlushQueuedChargerAudio`.

## Decision 038 - Recheck10 Proof Artifact False Positive Corrected

Problem: The first recheck10 JSON contained `batteryDirectInsertPlayAtPointMatches = 1` because the regex crossed from `InsertBattery` into the following `FlushQueuedChargerAudio` method. That contradicted the exact method-bound extraction and would make the final proof untrustworthy.
Solution: Re-run method-bound extraction for `public bool InsertBattery(...)` and correct the report field to 0. Keep the direct root scanner artifact visible: it scanned 1797 runtime script files, 1772 root methods, and reported 0 findings.
Rejected Alternatives: Explain the false positive only in chat; rejected because disk artifacts are authoritative. Delete the field; rejected because the field is useful as a regression guard when method extraction is correct.
Scalability potential: Offline proof correction only; runtime behavior is unchanged across low, middle, high, and ultra hardware.
Hardware Impact: Runtime impact is zero. Static direct-root scan elapsed 36925516 microseconds; recheck10 zero-GC scan elapsed 886545 microseconds.

## Decision 039 - Recheck10 Build Gate Blocked By CPU

Problem: Recheck10 corrected runtime code and report artifacts, so a final compilation would be useful only if the explicit throttle allowed it.
Solution: Sampled CPU and active compiler processes immediately before the build decision. CPU was 83%, active `dotnet/csc/VBCSCompiler` process count was 0. Because CPU exceeds 50%, `dotnet build` was not invoked and `Docs/Reports/DISPATCHER_BUILD_GATE_1410.json` was updated.
Rejected Alternatives: Launch build because no compiler process was active; rejected because the rule blocks on CPU > 50% independently of process count. Claim the previous timed-out build as a compile verdict; rejected because exit code 124 is no verdict.
Scalability potential: Development-host protection only; avoids stealing CPU from parallel agents.
Hardware Impact: Runtime impact is none. Static proof remains current; syntax remains pending CLI compile.

## Decision 040 - Static Consumable Utility Must Not Publish Audio

Problem: Recheck11 found a public API trap in `ConsumableItem.TryConsume(ItemData,HectonSurvivalSystem,IAudioService)`: it applied gameplay truth and then called `IAudioService.PlayStatic2D`. Current searched runtime call sites do not use that overload, but preserving a public direct-backend route makes future inventory/user actions able to bypass `LateFrameTick`.
Solution: Preserve the overload signature and remove the direct audio backend call. Gameplay truth remains in `ApplyEffects`; authored use audio is handled by the existing `PlayerActionController` deferred action audio path.
Rejected Alternatives: Leave it because current call-site count is zero; rejected because public APIs are architectural route surfaces. Add a static managed audio queue; rejected because no owner phase exists and it would invite heap-backed transfer.
Scalability potential: Low/middle/high/ultra keep consumable truth identical. Presentation richness remains in the action audio late-frame owner, which already scales volume through `HomeostasisBrain.GlobalQualityWeight`.
Hardware Impact: Removes a direct audio backend call from a public gameplay utility. Static method-bound extraction reports backend count 0 for the overload after patch.

## Decision 041 - Recheck11 Build Gate Blocked

Problem: Recheck11 touched C# source, so a build would be useful only under the explicit throttle gate.
Solution: Sampled CPU/processes. CPU was 100%; active `dotnet` PID 13464 was building `MapMagic.MicroSplat.csproj`. `dotnet build Hecton8.slnx` was not invoked.
Rejected Alternatives: Build anyway because this was a small edit; rejected because CPU and active-dotnet gates both fail. Claim static proof as syntax proof; rejected because only compiler output can prove syntax.
Scalability potential: Development-host protection only.
Hardware Impact: Runtime impact is none. Syntax remains pending CLI compile.
