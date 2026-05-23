# SHINOBU_311 Rationale

## Route Selection
Problem: Predator hearing must consume first-party acoustic lanes without reintroducing scene-query colliders.
Solution: Integrate into `PredatorCognitionDomain` because it owns predator cognition arrays, active slots, SDF snapshots, and the scheduled AI chain. Stage SignalBus snapshots into GlobalDataVault, then run Burst jobs before the main cognition job.
Rejected Alternatives: New `CreatureHeardNoiseSignal` and MonoBehaviour trigger sensors. Both add duplicate routes and managed fan-out.
Scalability potential: Low uses one SDF probe and strict signal cap; Middle raises probes and signal cap; High keeps full predator set; Ultra spends saved collider cost on denser occlusion and debug telemetry.
Hardware Impact: i3/MX350 avoids per-predator trigger/linecast work; expected win is tens to hundreds of microseconds in busy predator scenes, plus lower GC risk.

## Legacy Scan Boundary
Problem: `SphereCollider` appears in fauna files, but not all colliders are hearing.
Solution: Preserve POI and lunge CCD colliders; remove only confirmed hearing colliders if found.
Rejected Alternatives: Grep-and-delete by type name. That would break unrelated fauna influence and combat collision.
Scalability potential: Keeps collision ownership unchanged while moving sensory acoustics to data jobs.
Hardware Impact: No direct gain from deletion because no predator hearing collider was found; prevents regression cost.

## Burst Acoustic Kernel
Problem: Predator hearing needed physically plausible falloff without per-creature colliders or same-frame raycasts.
Solution: Stage existing SignalBus lanes into `AcousticStimulusDTO[128]`, subtract source/listener AUP in double, cast only the delta to float3, compute `InitialIntensity / max(lengthsq(delta), 0.01)`, then apply SDF occlusion with 1..8 probes driven by continuous `GlobalQualityWeight`.
Rejected Alternatives: `Physics.Linecast`, trigger spheres, AudioSource volume polling, and per-source managed event listeners. They create hot managed fan-out and scene query dependency.
Scalability potential: Low one probe and 128 staged cap; Middle 3-5 probes; High/Ultra up to 8 probes and richer editor visualization without changing gameplay truth ownership.
Hardware Impact: i3/MX350 expected to avoid collider broadphase and transform sync; expected gain 80-250 us/frame in acoustic-heavy scenes, with visual stealth feedback bought from saved cycles.

## Cognition Injection
Problem: Hearing must affect predator behavior without adding a second cognition authority.
Solution: `EvaluateAcousticOcclusionJob` writes to `CognitionCore` and `CognitionControl` through `UnsafeUtility.AsRef` after acoustic jobs and before `PredatorCognitionJob`; acoustic memory stores source runtime position, direction, intensity, and bucket hash.
Rejected Alternatives: Public setters or HectonEventBus callbacks. They would add cold managed routes and obscure ownership.
Scalability potential: Low only stores strongest heard source; Middle/High reuse the existing acoustic memory ring; Ultra can render the memory and source rays in X-Ray.
Hardware Impact: Direct flat-array mutation avoids managed dispatch and copies; estimated 15-40 us/frame saved versus per-predator method dispatch.

## Diagnostics and Black Box
Problem: Acoustic faults must be explainable after a frame budget breach or NaN.
Solution: `SensoryTelemetryEntry[300]` records stimuli, heard count, occlusion count, max intensity, estimated microseconds, and hash; fault dumps write `Docs/AgentLogs/Dump_SHINOBU_311.bin`.
Rejected Alternatives: Debug.Log-only telemetry or editor-only state. Logs are lossy and not crash-resident.
Scalability potential: Low keeps fixed 300 entries; Ultra can add richer X-Ray bars without changing runtime DTO layout.
Hardware Impact: Fixed ring write is sub-microsecond scale; avoids string allocations and runtime log spam.

## BufferID Sovereignty
Problem: Initial acoustic Vault IDs `71980..71988` overlap parasite VFX lanes in `H8Memory.cs`; the actual occupied set is `71980..71987` plus `71989,71990`.
Solution: Move acoustic lanes to local high IDs `72760..72768` and record the boundary in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Editing the shared `BufferID` enum during a parallel batch. The repo already contains local numeric high-ID routes; a scoped local range avoids a core compile-wall touch.
Scalability potential: Low/Middle/High/Ultra all use the same stable IDs; quality only changes probe count and optional telemetry, not ownership.
Hardware Impact: Correctness fix; prevents wrong Vault handle resolution and cross-domain cache pollution.

## Read Accessor Purity and Alias Proof
Problem: Editor diagnostics were able to call read accessors that performed cold Vault acquisition and CSV load.
Solution: Read accessors now return false/zero if handles are not already created; writes/bootstrap remain the only mutating routes. Acoustic Burst fields and raw cognition pointers now carry `[NoAlias]`; pointer mutation checks array lengths before `UnsafeUtility.AsRef`.
Rejected Alternatives: Leaving `EnsureAcousticSdfVaultBuffers()` inside `TryRead*` for convenience. It violates Global Systems Doctrine and can introduce hidden cold work from UI.
Scalability potential: Low devices avoid accidental editor/runtime read spikes; high devices still receive the same X-Ray data once the owner has booted.
Hardware Impact: Avoids cold allocation/CSV stalls from diagnostic reads and gives Burst clearer alias metadata for SIMD planning.

## Counter False Sharing Closure
Problem: A parallel mock path writing a shared stimulus count through `System.Threading.Interlocked` risks Burst import failure and cache-line contention even if capped.
Solution: Move the counter lane to `AcousticCounter64DTO` explicit 64-byte layout and set the mock count once in the owner schedule; `GenerateMockAcousticSignalsJob` writes deterministic fixed slots by `mockIndex`.
Rejected Alternatives: Pointer-based atomic increment/decrement from every worker. It satisfies a literal append-counter reading but is weaker for determinism and false-sharing control.
Scalability potential: Low uses fewer fixed mock slots; Middle/High/Ultra raise slot count via `GlobalQualityWeight` without changing DTO layout or authority.
Hardware Impact: i3/MX350 avoids shared counter cache bouncing and a Burst/IL2CPP atomic import risk; estimated gain is stability plus a few microseconds in mock stress scenes.

## Stale Counter Cleanup
Problem: Main-domain release/reset paths still referenced the removed int-count field after the counter lane moved to `AcousticCounter64DTO`.
Solution: Replace every cleanup/reset reference with `_acousticSdfStimulusCounter` and verify no stale symbol remains.
Rejected Alternatives: Leaving it for compiler discovery. That would violate fail-fast discipline and block unrelated agents at import time.
Scalability potential: No quality behavior change; cleanup now matches the single Vault counter truth across all tiers.
Hardware Impact: Compile/import correctness, no runtime timing claim.

## SDF Authority Preference
Problem: The inherited predator threat snapshot used direct World singleton bridges before acoustic occlusion consumed the bytes.
Solution: Acoustic occlusion now reads the published Vault voxel-SDF snapshot through a local Core-contract reader over `VoxelSdfPayloadDescriptorDTO` and `BufferID.VoxelSdfTexture3D`; old bridges remain only fallback for the pre-existing shared predator threat path, not the acoustic truth route.
Rejected Alternatives: New acoustic-specific world contract, a direct concrete World scheduler dependency, or edits across sibling runtime assemblies. Those widen the compile wall during a parallel batch.
Scalability potential: All tiers share the same SDF truth; quality only changes acoustic probe count from 1 to 8.
Hardware Impact: Authority-route hardening. Timing is unchanged when the Vault snapshot exists; fallback cost remains pre-existing.

## SDF Descriptor Validation
Problem: A loose Vault reader can accept a stale or wrong-owner SDF descriptor during world-streaming churn.
Solution: Validate descriptor handle BufferID/SystemID/generation, payload byte count, payload BufferID, payload owner, valid flag, SDF handle BufferID/SystemID/generation, generation match, finite origin, and positive cell size before scheduling the acoustic occlusion job.
Rejected Alternatives: Trusting descriptor row 0 without owner/generation checks. That can feed wrong bytes into Burst and corrupt stealth truth.
Scalability potential: No quality behavior change; low through ultra use the same verified SDF truth with different probe counts.
Hardware Impact: A few cold scalar checks in schedule; prevents expensive invalid occlusion work and undefined behavior.

## Dead Local Acoustic Queue Removal
Problem: `PredatorCognitionDomain` still carried an unreferenced local mock acoustic queue route that duplicated the SHINOBU_311 Vault-backed mock stimulus path.
Solution: Delete the unused local acoustic/light DTOs, scheduler facade, and mock job after `rg` verified zero call sites.
Rejected Alternatives: Leaving dead acoustic queue code "just in case." It fragments auditory truth and increases compile surface.
Scalability potential: Mock stress coverage remains in `GenerateMockAcousticSignalsJob`, continuously scaled by `GlobalQualityWeight`.
Hardware Impact: Compile/import surface reduction; no runtime frame claim because the legacy route was uncalled.

## Editor and Scanner
Problem: Designers need tuning without recompiles, and Task 18 asks for AST proof that OOP hearing queries are gone.
Solution: `Acoustic Sensory X-Ray` mutates vault tuning through the public diagnostics facade and exposes an opt-in mock signal flag. Its polling path writes numeric fields without dynamic string concatenation. `OOP_Hearing_Scanner` now parses scoped AI/Fauna/Sensory source with Roslyn `CSharpSyntaxTree`, walks `InvocationExpressionSyntax`, writes `Docs/Reports/SHINOBU_311_AI_OPTIMIZATION_REPORT.json`, and upserts shared report fields without deleting other agents.
Rejected Alternatives: Inspector-only MonoBehaviours, retaining the weaker lexical pass, or adding new broad asmdef references. Existing editor Roslyn scanner surface is reused without runtime dependency expansion.
Scalability potential: Low displays histogram and current signals; Middle/High show SceneView discs; Ultra can expand source heatmaps using the same DTOs.
Hardware Impact: Editor-only allocations do not touch runtime; runtime hot path remains Burst and NativeArray-backed.

## Compile Edge Re-Audit
Problem: `PredatorCognitionDomain.AcousticSdf.cs` carried a stale `using Hecton8.World` after the acoustic SDF reader moved to Core-contract Vault descriptors.
Solution: Remove the sibling namespace import and rerun targeted source scans for World scheduler helpers, direct singleton bridge names, and forbidden acoustic physics invocations.
Rejected Alternatives: Leaving the import as harmless. Under compile-wall rules dormant sibling imports are still evidence debt.
Scalability potential: No behavior change; all tiers keep the same Vault SDF truth and continuous probe count.
Hardware Impact: Compile/import surface reduction; runtime timing unchanged.

## AUP Helper Compile Fix
Problem: The import purge exposed a local SHINOBU_311 helper with an explicit `AbsoluteUniversePosition` parameter, causing CS0246 in the narrow `Hecton8.Core.csproj` build.
Solution: Delete the helper and call `signal.PositionAup.ToAbsoluteDouble3()` directly on the SignalBus payload value. This keeps AUP reconstruction on the payload type and avoids restoring a World namespace import in the acoustic partial.
Rejected Alternatives: Re-adding `using Hecton8.World` or editing the AUP contract. Re-adding the import would undo the compile-edge cleanup; moving AUP types is outside SHINOBU_311 scope.
Scalability potential: No quality behavior change; all tiers still stage double3 source AUP before local float attenuation.
Hardware Impact: Compile correctness; no runtime timing claim.

## Raw Blackbox Dump
Problem: The blackbox fault path used `BinaryWriter` field-by-field serialization, which is cold but weaker than the requested raw fixed-row `.h8dump` style.
Solution: Write a 16-byte stackalloc little-endian header, then stream the raw `NativeArray<SensoryTelemetryEntry>` bytes via `ReadOnlySpan<byte>` over the unsafe native pointer. The dump is now `16 + TelemetryLength * sizeof(SensoryTelemetryEntry)` bytes.
Rejected Alternatives: Keeping `BinaryWriter` or adding a managed DTO serializer. Both introduce avoidable managed writer behavior and can drift from the actual in-memory ABI.
Scalability potential: No quality behavior change; low through ultra use the same 300-row forensic ring.
Hardware Impact: Fault-path only. The fixed raw write avoids per-field writer calls and preserves exact cache-line telemetry rows for offline autopsy.

## Narrow Compile Boundary
Problem: A guarded narrow Core build was needed to prove the SHINOBU_311 CS0246 fix without polluting the developer machine with reusable compiler servers.
Solution: Shut down stale build servers left by the prior narrow build, then run `dotnet build Hecton8.Core.csproj` with `--disable-build-servers`, `/nr:false`, `/p:UseSharedCompilation=false`, and `-maxcpucount:1`. SHINOBU_311 no longer appears in the error list.
Rejected Alternatives: Launching a full solution build or editing external Gameplay/VR/HandIK code. That violates domain scope and compile-wall discipline.
Scalability potential: No runtime behavior change.
Hardware Impact: Verification only. Remaining compile blockers are external: `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, and `PlayerHandIkConfigFlags`.

## Shared Report Synchronization
Problem: The stable SHINOBU_311 report carried the narrow compile result, but the aggregate AI optimization report block did not.
Solution: Add only the stable `narrowCoreCompile` field inside the existing `shinobu311AcousticHearing` object, then parse both JSON reports and rerun owned stale-report scans. Loop 15 later demoted this field to pending because C# changed after the last guarded compile.
Rejected Alternatives: Regenerating the aggregate report wholesale. That would risk deleting unrelated agent sections during parallel work.
Scalability potential: No runtime behavior change; the report now preserves the same low-through-ultra acoustic route proof already implemented in code.
Hardware Impact: Documentation/proof only. No frame-time claim.

## Stale Compile Marker Cleanup
Problem: Owned documentation still named the pre-Loop-12 compile marker after the newer guarded build expanded the external blocker set to Gameplay/VR/Combat/KCC.
Solution: Remove the stale marker from status/rationale prose and keep the current marker only in stable generated reports and current compile-state notes.
Rejected Alternatives: Launching another build for a documentation-only proof fix. C# did not change, and the last guarded build already showed no SHINOBU_311 errors.
Scalability potential: No runtime behavior change; proof artifacts now match the latest low-through-ultra acoustic route.
Hardware Impact: Documentation/proof only. No frame-time claim.

## Subagent Race and Forensics Closure
Problem: Static audit found five remaining edge failures: owner-phase acoustic staging could overwrite Vault stimuli while a prior job was still pending, SDF occlusion raymarched candidates already below hearing threshold, read facades resolved mutable Vault handles during possible writer windows, blackbox dump throttled before proving IO success, and raw rows contained estimated rather than measured chain timing.
Solution: Move acoustic SignalBus staging out of `BeginDispatcherFrame` and keep it only inside `ScheduleFrameEvaluation` after `_evaluationScheduled` guard; add raw-intensity threshold cull before SDF sampling; switch read facades to `OpenRead()` and return false/zero while `_evaluationScheduled`; set dump throttle state only after successful raw write; patch the latest telemetry row with measured chain microseconds before dump.
Rejected Alternatives: Double-buffering stimuli, retaining readback during writer jobs, or leaving measured timing as a managed-side overlay. Double-buffering widens Vault footprint; readback during writer windows violates doctrine; overlay timing does not survive raw `.h8dump`.
Scalability potential: Low quality saves the most because inaudible sounds stop before even one SDF tap; middle/high keep richer occlusion only for audible candidates; ultra still uses 8 taps but on a strictly reduced candidate set.
Hardware Impact: Prevents live NativeArray data races, removes wasted SDF byte taps on weak CPUs, and makes fault dumps forensically useful without extra runtime allocation.

## Scanner Report Generator Stability
Problem: Manual JSON proof fields could be erased by the next `OOP_Hearing_Scanner` menu run, and the root list included nested `AI/Sensory` below `AI`.
Solution: Extend `BuildReport` and `BuildSharedReportBlock` to emit the full proof fields, including `blackBoxDumpFormat`, `narrowCoreCompile`, race/read/cull/dump booleans, and use non-overlapping roots `AI`, `Fauna`, and optional top-level `Sensory`.
Rejected Alternatives: Treating generated reports as one-off files. The CTO reads files; scanner output must be stable evidence, not a regression source.
Scalability potential: No runtime behavior change; proof now tracks the same low-through-ultra route after regeneration.
Hardware Impact: Editor/report-only. No frame-time claim.

## Loop 12 Compile Recheck
Problem: Runtime/editor C# changed after the last SHINOBU_311-clean compile, so a narrow compile proof was required once the guard cleared.
Solution: Wait until CPU sampled 22.97% and no dotnet/csc/VBCSCompiler processes were active, then run `dotnet build Hecton8.Core.csproj` with disabled build servers, `/nr:false`, no shared compilation, and `-maxcpucount:1`. The resulting error list contains no SHINOBU_311 files.
Rejected Alternatives: Full solution build or editing external Gameplay/VR/Combat/KCC blockers. That violates domain scope and would expand the compile wall.
Scalability potential: No runtime behavior change.
Hardware Impact: Verification only. Remaining blockers are external Gameplay comfort/horizon-lock symbols, Combat status-effect `math.select`, and KCC/metabolism contract constants.

## Idle Acoustic Frame Suppression
Problem: Frames with predator cognition work but no real/mock acoustic stimuli still scheduled the SHINOBU_311 attenuation, occlusion, and telemetry jobs. That violated the project rule against tiny/no-op jobs and left stale diagnostic result rows unless the jobs cleared them.
Solution: Add an owner-thread idle path in `ScheduleAcousticSdfIntegration`: write one zero-stimulus telemetry row, clear active-slot acoustic results, reset stale measured acoustic chain timing, and return the incoming dependency without setting `_acousticSdfEvaluationJobScheduled`.
Rejected Alternatives: Keep the three empty jobs for telemetry symmetry, or skip telemetry entirely. Empty jobs waste dispatcher budget; skipping telemetry leaves the X-Ray timeline stale.
Scalability potential: Low devices benefit most because silent frames now avoid three job admissions; middle/high/ultra keep full Burst math once a real or mock acoustic signal exists.
Hardware Impact: Silent-frame cost drops to one owner-thread O(activePredators) clear plus one 64-byte telemetry write instead of three scheduled jobs.

## SDF Out-Of-Volume Fail-Open
Problem: `SampleThreatVoxelSigned01` returned `0.0` for out-of-bounds or invalid SDF indices, which `EvaluateSdfOcclusion` interpreted as partial dampening. At streaming boundaries this could create false rock muffling outside the published SDF truth.
Solution: Return `1.0` for non-finite sample positions, outside-grid voxels, and invalid flattened indices. Only valid in-volume negative SDF samples can dampen sound.
Rejected Alternatives: Treat unknown SDF as mild occlusion. Unknown bytes are not an authority proof and must not alter predator hearing truth.
Scalability potential: Quality still scales ray steps continuously; fail-open applies uniformly across low through ultra without changing DTO layout or ownership.
Hardware Impact: Correctness fix; avoids wasted debugging around boundary-only false occlusion.

## Subagent P1 Audit Closure
Problem: Independent audit found three proof/architecture risks: dump path construction lived in the fault runtime path, scanner reports claimed compile-gated proof after new C# changes, and `ClosestPoint` detection could false-positive unqualified/local methods.
Solution: Cache the dump path and create its directory during cold init; keep raw dump file emission because Task 14 requires it on budget/non-finite faults. The compile marker was temporarily demoted to `PENDING_AFTER_LOOP14_CPU_GUARD_BLOCKED`; Loop 21 superseded it with a guarded narrow build showing no SHINOBU_311 errors. Narrow `ClosestPoint` detection to collider-like receivers instead of all member or identifier calls.
Rejected Alternatives: Removing budget dumps, preserving stale build-gated PASS, or keeping blanket `ClosestPoint` token matching. Those either violate the XML task or weaken evidence quality.
Scalability potential: No gameplay math change; low-through-ultra runtime path keeps the same acoustic fidelity curve. Editor/scanner proof is now narrower and more honest.
Hardware Impact: Fault path no longer constructs path strings/directories; build remains blocked by CPU guard, not by agent choice.

## No-Due Frame Blackbox Closure
Problem: The no-stimulus idle telemetry row existed only inside `ScheduleAcousticSdfIntegration`, so cadence-skipped frames with active predators but no due cognition work could leave the 300-frame acoustic blackbox stale.
Solution: After SignalBus staging and due-flag evaluation, `ScheduleFrameEvaluation` now calls `RecordAcousticSdfIdleTelemetryFromCurrentTuning(frameId)` before the no-work early return. The helper refreshes continuous `GlobalQualityWeight`, preserves tuning scale, writes one idle `SensoryTelemetryEntry`, and clears active acoustic result rows without scheduling any acoustic jobs.
Rejected Alternatives: Scheduling a tiny telemetry job every silent frame, or accepting stale rows until the next predator cognition cadence. Tiny jobs violate dispatcher doctrine; stale rows violate Task 14 forensic requirements.
Scalability potential: Low devices keep a cheap owner-thread 64-byte row and zero Burst admissions on silent cadence-skipped frames; middle/high/ultra still spend the full inverse-square/SDF chain only when real or mock stimuli exist.
Hardware Impact: Preserves a per-frame blackbox trace while avoiding three job admissions and all SDF taps on silent no-due frames. Build proof is pending because CPU guard sampled 77%.

## Parallel Result False-Sharing Closure
Problem: `AcousticEvaluationResultDTO` was 80 bytes and written by parallel attenuation/occlusion jobs. Adjacent rows overlapped cache lines, so two worker cores could invalidate each other while writing different predators.
Solution: Expand `AcousticEvaluationResultDTO` to an explicit 128-byte stride with payload through byte 79 and `ulong` reserved padding at offsets `80, 88, 96, 104, 112, 120`. The ABI validator now expects 128 bytes and align >=8.
Rejected Alternatives: Keep the 80-byte row to save memory, or split source AUP/runtime/direction into multiple arrays. The 80-byte row violates the false-sharing mandate; splitting arrays would widen the Vault route and increase integration risk during parallel batch work.
Scalability potential: Low devices avoid cache-line ping-pong when acoustic jobs run; middle/high/ultra retain the same quality curve and can spend saved stalls on 1..8 SDF probes without changing authority.
Hardware Impact: Costs +48 bytes per predator result row. On the current capacity envelope this is a small fixed Vault increase in exchange for removing a parallel write contention class.

## Idle Owner-Write Race Closure
Problem: The idle telemetry/result clear path could run inside `ScheduleAcousticSdfIntegration` after `SwarmAnalysisJob` had already been scheduled with `_activeSlots`. That left owner-thread reads of `_activeSlots` in the same scheduling window as a scheduled job read.
Solution: Add `HasAcousticSdfWorkPending()` and move the no-work idle telemetry write/clear into `ScheduleFrameEvaluation` before the first job admission. Silent frames now bypass `ScheduleAcousticSdfIntegration` entirely after job handoff.
Rejected Alternatives: Schedule a tiny idle telemetry job after `SwarmAnalysisJob`, rely on Unity safety allowing concurrent read-only access, or keep opening acoustic Vault lanes after job handoff only to return the same dependency. Tiny jobs violate dispatcher doctrine; post-handoff owner work is unnecessary and harder to reason about.
Scalability potential: Low devices keep the same silent-frame cheap path; middle/high/ultra avoid a latent safety edge while preserving full acoustic math when stimuli exist.
Hardware Impact: One owner-thread counter/tuning read before scheduling; removes a race/safety risk without adding jobs.

## Admission Retry / Scanner Conservatism
Problem: Independent audit found two remaining proof holes. If `SwarmAnalysisJob.TryScheduleParallelAdmitted` failed after acoustic staging, `_lastScheduledFrame` advanced and blocked same-frame retry. The scanner also missed `ClosestPoint` calls on collider variables with non-collider names because it lacked a semantic model.
Solution: Leave `_lastScheduledFrame` unchanged on swarm admission failure when `hasAcousticSdfWork` is true, preserving staged same-frame acoustic stimuli for retry. Change `OOP_Hearing_Scanner` to flag any member `ClosestPoint(...)` invocation in scoped AI/Fauna/Sensory source as forbidden unless a future semantic pass proves otherwise.
Rejected Alternatives: Dropping acoustic input under lane pressure, or keeping receiver-name heuristics. Both create false confidence: the first loses first-party SignalBus facts, the second can miss real collider calls named `hitbox` or `body`.
Scalability potential: Low devices under scheduler pressure preserve acoustic truth for retry; high/ultra behavior is unchanged. Scanner conservatism affects editor proof only.
Hardware Impact: No runtime hot-path cost. Retry fix is a branch on admission failure; scanner change is editor-only.

## Tuning Bridge Closure
Problem: `AcousticTuningDTO.MaxDistanceMeters` existed in snapshots and editor writes, but the attenuation/occlusion jobs only used per-profile `MaxDistanceSq`. The X-Ray facade also reset max distance and fault budget to constants on every tuning write.
Solution: Clamp each profile's max-distance squared by sanitized `Tuning.MaxDistanceMeters * Tuning.MaxDistanceMeters` in both Burst candidate loops. Add UI Toolkit sliders for max distance and fault budget and preserve those values through `TryWriteAcousticSdfTuning`.
Rejected Alternatives: Leaving max distance as a decorative editor field, or scaling hearing range by `GlobalQualityWeight`. Decorative fields violate the human tuning bridge; quality must not change gameplay truth ownership or predator hearing authority.
Scalability potential: Low devices can use authored shorter hearing radii to avoid unnecessary SDF probes; middle/high/ultra keep the same continuous ray-step quality curve and can raise authored range without recompiling C#.
Hardware Impact: One scalar multiply and min per predator job row. Savings occur when tuned range rejects candidates before inverse-square best selection and SDF occlusion; no profiler number claimed.

## Cold-Path Proof Closure
Problem: Subagent audit found that if acoustic Vault creation failed during cold boot, frame scheduling could later call the allocating `EnsureAcousticSdfVaultBuffers()` route and trigger Vault allocation plus CSV/path FileStream work from a hot dispatcher frame.
Solution: Add `AreAcousticSdfVaultBuffersReady()` and make frame-owned acoustic staging, idle telemetry, and integration fail closed unless all acoustic handles already exist. The allocating ensure route remains limited to cold initialization and explicit mutable tuning writes.
Rejected Alternatives: Retrying Vault allocation from `ScheduleFrameEvaluation` for resilience. That hides cold work in the hot path and violates the dispatcher phase contract.
Scalability potential: Low devices avoid surprise allocation and disk IO during thermal pressure; middle/high/ultra preserve the same acoustic quality curve once boot allocation is present.
Hardware Impact: Hot schedule path now performs handle-readiness booleans only; it cannot trigger cold Vault allocation or CSV FileStream fallback.

## Unsafe Pointer Proof Closure
Problem: The raw `_cores`/`_controls` pointer mutation used `[NativeDisableUnsafePtrRestriction]` with only a short invariant comment, leaving incomplete proof for the unsafe owner-row write.
Solution: Add the required `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` block covering ownership, dependency order, bounds checks, ABI stride, pointer lifetime, and non-publication.
Rejected Alternatives: Replacing raw pointers with interface setters or native array aliases. Setters reintroduce dispatch; aliases widen the job alias surface and fight Burst vectorization.
Scalability potential: No quality behavior change; proof now matches the same low-through-ultra mutation route.
Hardware Impact: Documentation/proof only; no runtime instructions added.

## Tuning Write Evaluation Fence
Problem: The X-Ray tuning route is a legitimate mutable editor/designer bridge, but it could still open the tuning Vault while `_evaluationScheduled` marked an acoustic job chain active.
Solution: `TryWriteAcousticSdfTuning` now returns false while `_evaluationScheduled` is true before opening the tuning Vault or entering the cold allocating ensure route.
Rejected Alternatives: Allowing live editor mutation and relying on job timing, or forcing a `.Complete()` to make the write immediate. Live mutation risks a read/write race; `.Complete()` would serialize the dispatcher and violate the job doctrine.
Scalability potential: Low devices avoid editor-induced contention during thermal pressure; middle/high/ultra preserve the same continuous `GlobalQualityWeight` probe curve once the next safe tuning write lands.
Hardware Impact: One branch on a cold/editor write path; no runtime frame-time claim. It removes a race class without changing DTO layout, authority route, or gameplay truth ownership.

## Hooke Retry / Fault / Priority Closure
Problem: Independent audit found four edge failures after Loop 22. Non-finite acoustic rows marked `AcousticFaultNonFinite` but finalization only dumped for budget faults; admission retry preserved only same-frame retries; movement signals could fill the 128-slot staging cap before combat/ping lanes; and the unsafe pointer proof paragraph repeated invariants instead of rejected alternatives.
Solution: Add `AcousticFaultNonFinite` to the finalization dump predicate; add `_acousticSdfPendingStimulusRetry` so staged stimuli survive across frames until the acoustic chain consumes them; stage combat, ping, then movement with fixed quotas and copy dropped valid stimuli into telemetry; rewrite `SAFETY_JUSTIFICATION_PARAGRAPH_2` with rejected alias/setter/shadow-state alternatives.
Rejected Alternatives: Waiting for a later budget fault to dump NaN state, same-frame-only retry, movement-first FIFO staging, native-array aliases, managed setter command buffers, and duplicate acoustic patch arrays. Each one either loses the one-route acoustic fact, creates shadow state, or weakens Burst alias proof.
Scalability potential: Low devices under scheduler pressure keep critical combat/ping acoustics and dump non-finite state immediately; middle/high/ultra preserve the same continuous SDF probe curve and only spend more probes after priority staging admits the signal.
Hardware Impact: The retry latch is cold/admission-failure logic; priority staging is O(staged signals) with the same 128 cap. It avoids wasted downstream cognition on low-priority overflow and improves forensic recovery without adding jobs or changing DTO layout.

## Invalid Ingress Fault Telemetry
Problem: Invalid acoustic SignalBus payloads could be rejected before the Burst chain and counted only as dropped/overflow stimuli, which hid non-finite upstream AUP or intensity faults from the blackbox dump predicate.
Solution: Validate movement, ping, and combat ingress scalars plus AUP before append. Mark `AcousticCounterFlagInvalidIngress` in the 64-byte counter, copy it into telemetry `Reserved1`, and fold the flag into `AcousticFaultNonFinite` inside `RecordAcousticTelemetryJob`.
Rejected Alternatives: Counting invalid ingress as ordinary overflow or waiting for downstream result math to observe NaN. Ordinary overflow hides the producer fault; downstream math never runs for rejected invalid payloads.
Scalability potential: Low through ultra keep the same capped staging and continuous ray-step curve; the extra fault bit does not change gameplay truth, capacity, save identity, or DTO layout.
Hardware Impact: Adds finite checks in the owner staging pass only. No new job, no allocation, and no additional Vault lane.

## Read-Only Handle Tightening
Problem: Three hot owner-phase helper paths inspected acoustic counter/tuning state through mutable Vault `Open()` even though they never wrote those buffers.
Solution: Use `OpenRead()` in `MarkAcousticSdfDueWhenStimuliPresent`, `HasAcousticSdfWorkPending`, and `IsAcousticMockSignalModeEnabled`. Mutable `Open()` remains confined to actual owner writes, scheduled mutable job output buffers, cold initialization, and the fenced tuning write bridge.
Rejected Alternatives: Leaving mutable resolution because the current implementation did not allocate. That is weaker proof for read-only schedule logic and makes future audits harder.
Scalability potential: Low through ultra behavior is unchanged; quality still scales ray probes continuously from 1 to 8 and does not alter DTO layout, save identity, or authority route.
Hardware Impact: No new jobs or allocations. This tightens access intent and reduces accidental mutable-handle expansion in hot scheduling code.

## Invalid-Only Idle Fault Closure
Problem: Poincare audit found that invalid-only ingress set `AcousticCounterFlagInvalidIngress` with `Value == 0`, so the acoustic job chain was skipped and idle telemetry wrote a default row without counter flags.
Solution: Idle telemetry now reads the staged 64-byte counter via `OpenRead()`, copies `Value`, `Reserved0`, and `Flags`, folds invalid ingress into `AcousticFaultNonFinite`, and triggers the raw blackbox dump after writing the idle row.
Rejected Alternatives: Treating invalid-only ingress as normal silence, or scheduling empty attenuation/occlusion/telemetry jobs just to fold the flags. Silence hides the producer fault; empty jobs violate dispatcher doctrine.
Scalability potential: Low through ultra keep the same acoustic quality curve and hard cap; invalid-only fault accounting does not change gameplay truth or DTO layout.
Hardware Impact: One owner-thread counter read on idle telemetry frames. No new jobs, no allocations, and no SDF taps.

## Retry Latch Drift Cleanup
Problem: The pending-retry frame integer was write-only, and the Loop 25 read-handle tightening accidentally left the retry flag writer using read-intent resolution.
Solution: Delete `_acousticSdfPendingStimulusRetryFrame` and all assignments. Restore mutable `Open()` only in `MarkAcousticSdfPendingRetry`, where the owner writes the 64-byte counter flag.
Rejected Alternatives: Keeping a write-only diagnostic sidecar, or writing through `OpenRead()` because the returned `NativeArray` is technically mutable. Both weaken the access-intent proof.
Scalability potential: No quality behavior change; low through ultra still preserve staged acoustic stimuli across admission failure using the boolean latch and counter flag.
Hardware Impact: Removes one dead static field and keeps retry flag mutation on the explicit owner-write route. No frame-time claim.

## Dump Path Fault-Path Retry Closure
Problem: `TryDumpAcousticSdfBlackBox` could re-enter `EnsureAcousticSdfDumpPathCold()` when the cached path was empty, which allowed managed `Path` and `Directory` work inside a fault export path after cold setup failure.
Solution: Add a cached-path gate so the fault writer only uses the cached path and returns if the cold route did not produce one. Loop 29 supersedes the failed-attempt lifetime so later cold/tuning-safe routes can retry.
Rejected Alternatives: Retrying path construction from the fault writer, or suppressing dump attempts entirely. Retrying adds managed path/directory work under a fault condition; suppressing all attempts would violate the 300-frame blackbox mandate when cold setup succeeded.
Scalability potential: Low through ultra keep identical acoustic math and telemetry layout. The change only removes a managed retry edge from fault forensics; `GlobalQualityWeight` and gameplay truth are unchanged.
Hardware Impact: Fault-path allocation risk is removed. No hot-frame profiler number is claimed; steady-state acoustic jobs and DTO layouts are unchanged.

## Recoverable Cold Dump Path Retry
Problem: Boole audit correctly found that the Loop 28 one-shot latch made a cold path exception permanent for the current domain state, so later recoverable filesystem/path conditions could still suppress required blackbox dumps.
Solution: Set `_acousticSdfDumpPathInitialized` only after path resolution and directory creation succeed; clear it on catch. `EnsureAcousticSdfVaultBuffers()` now retries `EnsureAcousticSdfDumpPathCold()` before returning when buffers already exist, which gives cold initialization and editor/tuning-safe calls a recovery route without entering `Path`/`Directory` code from `TryDumpAcousticSdfBlackBox`.
Rejected Alternatives: Retrying from the fault writer, or keeping one failed cold attempt terminal. Fault retry risks managed path work under budget/NaN failure; terminal failure violates blackbox recoverability.
Scalability potential: No low/middle/high/ultra gameplay difference. This is route reliability only; quality continues to scale ray steps and mock density continuously.
Hardware Impact: One cold branch when ensuring acoustic buffers. Fault writer remains cached-path-only; no hot acoustic job cost or DTO layout change.
