# Rationale_1303 - MEMORY_SOVEREIGN_TETHERS_EXORCIST

## Initial Boundary

Problem: Task authority is restricted to persistent native memory hazards in `Assets/Project/Scripts/Physics/Tethers`.
Solution: Confine scans and mutations to the tether domain unless a cross-domain interface proof is required.
Rejected Alternatives: Scanning all physics systems would create neighbor-task contamination and false architectural dependencies.
Scalability potential: Low uses static source proof first; Middle/High/Ultra only receive expanded visual/simulation work after memory ownership is proven.
Hardware Impact: Static archaeology has no runtime cost; expected runtime target remains 0 B GC and no unmanaged alias UAF on i3/MX350.

## Mandate Selection

Problem: Tether memory exorcism crosses physics truth, Native memory, DTO layout, telemetry, phase ownership, AUP, Zero-GC, and fake-first doctrine.
Solution: Loaded 8 mandate files: tether physics, native memory/jobs, ARM64 DTO layout, telemetry, execution phases, zero-GC, AUP precision, cinematic fake-first.
Rejected Alternatives: Loading the whole registry would pollute context and violate the 2-8 mandate read rule.
Scalability potential: Low/Middle/High/Ultra all share the same ownership law; visual complexity scales later through `VISUAL_SYNC`, not simulation truth bloat.
Hardware Impact: Mandate-driven audit prevents MX350 stalls from GC, job completion misuse, and stale NativeArray aliases.

## Domain Path Correction

Problem: Prompt states `Assets/Project/Scripts/Physics/Tethers`, but that path is absent. Current first-party tree uses `Assets/_Project`, and `Assets/_Project/Scripts/Physics/Tethers` exists.
Solution: Use `Assets/_Project/Scripts/Physics/Tethers` as the corrected domain root for authoritative scans. Inspect adjacent `Assets/_Project/Scripts/Physics/Tether*.cs` and `Cable132` only as dependency context unless a cross-domain fix is proven necessary.
Rejected Alternatives: Creating `Assets/Project` would fabricate a parallel domain. Editing adjacent solver files without evidence would violate domain boundaries.
Scalability potential: Low/Middle/High/Ultra unaffected; this is path authority repair, not runtime design.
Hardware Impact: No runtime impact. Prevents scanning an empty/nonexistent path and producing a false clean report.

## Phase 0 Strict Root Ledger

Problem: Task 01 demanded AST separation between field aliases and harmless locals/job parameters.
Solution: Ran the existing compiled `Tools/VaultNativeAliasRoslynAudit` net10 binary against corrected root `Assets/_Project/Scripts/Physics/Tethers`; output `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1303_STRICT_ROOT.json` reports 1 scanned file, 0 parse failures, 0 native fields, 0 forbidden candidates.
Rejected Alternatives: Regex-only final evidence would not satisfy the Roslyn mandate; rebuilding the tool was rejected because CPU was 88.46% and the project build gate forbids dotnet build under load.
Scalability potential: Low/Middle/High/Ultra all get identical ownership proof; there is no runtime work in the strict contracts folder.
Hardware Impact: 0 us runtime. Scanner execution is offline; no i3/MX350 frame cost.

## Boundary Leak Classification

Problem: The strict corrected root is clean, but the live tether owner is `Assets/_Project/Scripts/TetherInstance.cs`, outside the prompt folder, and it stores 25 persistent `NativeArray<T>` physical Vault aliases at lines 236-260.
Solution: Classified the file as a critical boundary leak and mapped each alias to its existing `BufferID`, including `TetherCablePositions(67)`, `TetherCableBlackBox(72)`, `TetherVerletPositions(326)`, `TetherVerletNodeFaultFlags(337)`, `VerletCableGpuSplinePoints(578)`, `VerletCableTensionForces(580)`, and `VerletCableTuning(583)`.
Rejected Alternatives: Reporting the strict root as clean without naming the actual runtime owner would be a fake pass. Editing `TetherInstance.cs` during Phase 0 was rejected because this turn is archaeology and the file is outside the strict root.
Scalability potential: Low: handles only, low visual segment density. Middle: same handles with default interpolation. High: extra visual spline density. Ultra: visual overkill only; simulation truth and DTO layout remain unchanged.
Hardware Impact: Planned Phase 1 removes stale physical aliases and protects live compaction; expected gain on i3/MX350 is crash-risk removal and less pointer lifetime pressure, not a claimed measured microsecond win yet.

## Telemetry And Dump Route Gap

Problem: Existing tether telemetry uses 64-byte entries and 300-frame capacity concepts, but `TetherInstance` still names legacy dump files `Docs/AgentLogs/Dump_VERLET_CABLES.bin` and `.h8dump`.
Solution: Phase 0 report specifies `Docs/AgentLogs/Dump_1303_Tethers.bin` as the required black-box raw ring route and keeps `TetherCableBlackBox(72)` / `TetherCableBlackBoxHead(212)` as the existing Vault lanes.
Rejected Alternatives: Managed string logging or chat-only crash notes are not forensic proof and violate the black-box rule.
Scalability potential: Low/Middle/High/Ultra write the same fixed telemetry stride; quality may scale cadence only, not layout.
Hardware Impact: Fixed 300-entry ring writes are bounded; no managed allocation. Phase 1 must avoid hot path formatting and background-thread allocation bursts.

## Compile Verification Gate

Problem: The workflow asks for compile verification after tasks 1-5, but Phase 0 changed only docs/report JSON and CPU was 88.46%.
Solution: Did not launch dotnet build. Recorded the skipped compile explicitly in `Status_1303.md` and `VAULT_EXORCISM_REPORT_1303.json`.
Rejected Alternatives: Violating the CPU/compiler gate to produce a ceremonial build marker would be worse than no build and could interfere with other agents.
Scalability potential: No runtime effect. It preserves shared build bandwidth for active code owners.
Hardware Impact: Avoided unnecessary CPU contention on the host; no gameplay microsecond claim.

## Phase 1 Descriptor Substitution

Problem: `TetherInstance` held 25 persistent physical `NativeArray<T>` Vault aliases. Any live compaction could invalidate those addresses while the MonoBehaviour still believed it owned stable memory.
Solution: Removed every class-level native physical alias and retained only `VaultGenerationHandle<T>` descriptors. Added phase-local resolution helpers so methods receive fresh views at the execution point.
Rejected Alternatives: Keeping shadow arrays for convenience or exposing raw arrays to visual systems would preserve the stale-pointer failure mode.
Scalability potential: Low/Middle/High/Ultra all share the same truth route. Quality can scale node count and visual upload density, but ownership remains handle-only.
Hardware Impact: No measured microsecond claim. Expected effect on i3/MX350 is removing crash/undefined-access risk during Vault relocation and reducing long-lived pointer pressure.

## Phase 1 Write Lock Discipline

Problem: Phase-local mutable views still need explicit Vault writer fences when initialization, job scheduling, publish, clear, and tuning paths mutate shared buffers.
Solution: Added `TryAcquireDataVaultCableArray`, `TryAcquireDataVaultCableSlice`, and `ReleaseDataVaultCableWriteLock`. Core Verlet initialization, solver schedule window, canonical publish, telemetry clear, cable clear, and tuning default paths release acquired locks in `finally`.
Rejected Alternatives: Releasing unconditionally after a failed acquire was rejected because it could release a lock owned by an outer phase. Holding locks across frames was rejected because compaction must be able to regain ownership after scheduling.
Scalability potential: Low skips on contention instead of corrupting state; Middle/High/Ultra can spend saved stability budget on more visual spline density, not more ownership complexity.
Hardware Impact: Lock checks are branch/metadata work; no measured frame saving claimed. The intended gain is deterministic fail-closed behavior on low-end silicon during relocation pressure.

## Cold Registration And ClearMemory Decision

Problem: The batch prompt asks for `NativeArrayOptions.UninitializedMemory` where buffers are fully overwritten, but these buffers are shared slabs for 64 tether slots and are not globally overwritten at creation.
Solution: Retained `ClearMemory` for shared cable, telemetry, visual, and tuning slabs. Only slot-local spans are overwritten later; global uninitialized memory would leak stale state across inactive slots.
Rejected Alternatives: Using `UninitializedMemory` to satisfy a checklist would be faster on paper but unsafe for telemetry, smoothing, and inactive-slot reads.
Scalability potential: Low/Middle/High/Ultra get deterministic zero baselines. Optional future optimization requires per-slot first-write proof, not global uninitialized allocation.
Hardware Impact: Zero-fill cost is cold-path only. No runtime frame cost is claimed; avoiding stale cross-slot data is more valuable than a fake boot-time micro-optimization.

## Continuous Quality Preservation

Problem: Segment and iteration selection must consume continuous `GlobalQualityWeight`, not a hard low/high switch.
Solution: `ResolveVerletIterationCount` and `ResolveVerletSegmentCount` now use `ResolveTetherQualityWeight`, `Smooth01`, and `math.lerp` to scale from low survival to max slot capacity.
Rejected Alternatives: Fixed segment count and binary quality branches were rejected because they violate the scalability pillar and spend low-end frame time unnecessarily.
Scalability potential: Low uses fewer Verlet segments and taut-line visual fake under stress; Middle scales interpolation; High/Ultra spend budget on more cable points and visual overkill.
Hardware Impact: Low-end i3/MX350 receives fewer node operations when `GlobalQualityWeight` is low. Exact microseconds not measured because compile/build gate remains closed.

## Phase 1 Verification Gate

Problem: C# code changed and compile verification is required. The host gate later opened, but the build failed in an unrelated core/UI dependency before tether compilation.
Solution: Ran `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` only after `CPU=26`, `dotnet=0`, `csc=0`. Captured failure: missing `FixedUiEventQueue<>` in `BaseIntegrityHUD.cs`, `PDAIntrusionManager.cs`, `NotificationEvents.cs`, and `SpectrumSystem.cs` under `Hecton8.Core.csproj`. Static tether proof remains valid: whole-scripts Roslyn parser reports 2417 files parsed, 0 parse failures, and 0 native field findings for `Assets/_Project/Scripts/TetherInstance.cs`.
Rejected Alternatives: Editing UI/Core queue infrastructure from this tether memory task would violate domain boundaries. Re-running build while dotnet processes remained active after failure was rejected by the build gate.
Scalability potential: No runtime effect. Tether verification is blocked by a foreign dependency until the integrator repairs or stages `FixedUiEventQueue<>`.
Hardware Impact: Build failure has no gameplay microsecond meaning. It prevents compile proof, not static memory proof.

## APEX Recheck - Unprotected Rest-Length Mutation

Problem: The first Phase 1 pass still had three mutation paths that wrote through transient views without local lock proof: `RebaseVerletSolverOrigin` re-resolved solver arrays internally, `ApplyVerletRestLengthTarget` re-resolved rest lengths while the caller already held the lock, and `ApplyVerletPlasticDeformation` mutated rest lengths after job completion without taking a writer lock.
Solution: `RebaseVerletSolverOrigin` now consumes the caller's already locked `NativeArray<float3>` views; rest-length target receives the locked `NativeArray<float>` directly; plastic deformation acquires `TetherVerletSegmentRestLengths` via `TryAcquireDataVaultCableSlice` and releases it in `finally`.
Rejected Alternatives: Assuming the outer phase "probably" owns the lock was rejected because the code itself must prove ownership. Re-resolving a mutable view for convenience was rejected because it hides compaction races.
Scalability potential: Low/Middle/High/Ultra all get the same fail-closed memory route. Visual density can scale; Vault mutation law does not.
Hardware Impact: No measured microsecond gain. Expected low-end gain is prevention of relocation-era undefined access on i3/MX350 class devices.

## APEX Recheck - Failure Telemetry Payload

Problem: Lock/resolve/length failures previously had insufficient binary context for postmortem triage.
Solution: `TetherVerletTelemetryEntry` repurposes offsets 48/52/56/60 as `BufferId`, `Generation`, `FailureCode`, and `Reserved0`/observed buffer id. Failure branches write one fixed 64-byte row and increment the head. Validator asserts these offsets.
Rejected Alternatives: Managed log messages and string-formatted failure reasons were rejected for hot/fault branch GC risk.
Scalability potential: All quality levels share the same 300-frame ring. Quality may change how often optional telemetry is sampled, never the DTO layout.
Hardware Impact: Failure path writes one cache-line-sized row. Normal frames pay no string or heap cost.

## APEX Recheck - AUP Force Formula

Problem: Endpoint force application must not cast absolute AUP coordinates to float before subtracting the local origin/delta.
Solution: Endpoint force now builds `anchorAup` and `payloadAup` as `double3`, computes `deltaAup = payloadAup - anchorAup` in double precision, normalizes in double, then casts only the normalized direction components to `float`.
Rejected Alternatives: `float3(payloadAup)` and world-space `Vector3` subtraction were rejected because they lose precision at large AUP offsets.
Scalability potential: Low through Ultra retain deterministic truth; visual cable simplification can happen after this force route, not before.
Hardware Impact: Double subtraction is tiny compared to a frame; it prevents large-coordinate jitter and bad force direction on long sessions.

## APEX Recheck - Stress Harness Boundary

Problem: Task 16 required a deterministic stress generator instead of subjective "tested mentally" language.
Solution: Added Burst `GenerateMockTetherLoadJob` with caller-owned `NativeArray<float3>`, `NativeArray<byte>`, and `NativeArray<float>` views, deterministic integer hash, no scene/registry/Vault lookup, and no managed references.
Rejected Alternatives: Editor-only managed loops or scene-spawned test GameObjects were rejected because they do not stress the same data-local path.
Scalability potential: Low can run short arrays; Middle/High/Ultra can run tens of thousands of elements without changing the job ABI.
Hardware Impact: Not executed in this shell. Expected load is pure parallel math with no GC; actual microseconds require Unity Profiler or editor test run.

## Remaining Runtime Verification Wall

Problem: Task 17 live defragmentation race execution and compile verification require a Unity/DataVault runtime context and a clear build gate. Current shell gate is closed by active dotnet processes and CPU >50%.
Solution: Recorded the wall explicitly. Regenerated static reports and did not fake a runtime fuzzer pass or launch a forbidden build.
Rejected Alternatives: Calling the task green without Editor/PlayMode execution was rejected. Running `dotnet build` under the current gate was rejected by project law.
Scalability potential: No gameplay path changes. This is verification debt, not runtime feature debt.
Hardware Impact: Avoided host contention. Runtime proof remains pending, not claimed.

## APEX Recheck - Prompt Source Correction

Problem: The user's shorthand path `current_batch.md` is absent at repo root, and the first exact-tag regex failed because the real current tag includes extra attributes: `<AGENT_PROMPT id="1303" role="..." chat_name="1303">`.
Solution: Used `Docs/Tasks/CURRENT_BATCH.md` as the authoritative current batch file and re-extracted with an attribute-tolerant CLI regex. The extracted block contains 20 tasks and SHA-256 `9a3528042794113df9c5d3c4840d010ac34b37f3eff28dacd9a611dff5917309`.
Rejected Alternatives: Treating the missing root file as success, or relying only on the stale extracted prompt copy, was rejected.
Scalability potential: No runtime effect. It prevents cross-agent prompt contamination.
Hardware Impact: 0 us runtime. This is proof hygiene only.

## APEX Recheck - Build Gate State

Problem: A fresh compile check after the APEX code patch is still required, but the local rule forbids build under active dotnet/csc or CPU pressure.
Solution: Sampled the gate: `CPU=41.87`, `dotnet=7`, `csc=0`; build remained forbidden because active dotnet processes exist. Did not launch `dotnet build`.
Rejected Alternatives: Starting another build while seven dotnet processes are live was rejected because it violates the coordination rule and can corrupt shared verification signal for 20+ agents.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional host contention; compile proof remains pending.

## APEX Recheck - Literal New Token Cleanup

Problem: The previous Zero-GC report was technically accurate for managed heap allocations, but it still left `new` tokens in diff-added hot code as value-type construction. That is weak evidence under the user's literal static-scan requirement.
Solution: Converted modified hot-path struct creation to `default` plus field assignment in `TetherInstance.cs` and `TetherVerletJobs.cs`. The refreshed scan now reports `diffAddedNewKeywordCount=0`, forbidden text patterns `0`, and managed heap `new` in audited hot ranges `0`.
Rejected Alternatives: Arguing that `new float3` and `new Vector3` are harmless value-type construction was true but insufficient for this APEX review.
Scalability potential: No gameplay truth change. Low/Middle/High/Ultra still scale through the same continuous quality math.
Hardware Impact: No measured microsecond gain. The value is auditability and eliminating false positives in hot code.

## APEX Recheck - Explicit AUP Local Delta

Problem: The endpoint force proof used `payloadAup - anchorAup` directly. That is mathematically equivalent when both are produced from the same origin, but it did not visibly prove the required origin-subtraction route.
Solution: Rewrote the route to compute `anchorLocal64 = anchorAup - origin64`, `payloadLocal64 = payloadAup - origin64`, then `deltaLocal64 = payloadLocal64 - anchorLocal64`; only the normalized local delta is cast to `Vector3`.
Rejected Alternatives: Keeping the shorter absolute-delta expression was rejected because future reviewers could misread it as direct AUP-to-float use.
Scalability potential: Same deterministic force truth across Low/Middle/High/Ultra; visual simplification remains after force truth.
Hardware Impact: Negligible arithmetic delta; avoids large-coordinate precision ambiguity on Quest/ARM64.

## APEX Recheck - Independent Hot-Path Audit

Problem: A local text scan alone is not enough independent evidence for hidden managed hot-path hazards.
Solution: Ran existing `SignalBusContractAuditCli` net10 binary with `--include-hot-path-heuristics`. It scanned 2419 C# files and 71 shaders. Tether-owned files `TetherInstance.cs`, `TetherVerletJobs.cs`, and `VerletCableDTOs.cs` have 0 findings in that report.
Rejected Alternatives: Running `dotnet build` was rejected by user instruction and active compiler gate; running no secondary audit was rejected as too shallow.
Scalability potential: Static evidence only.
Hardware Impact: 0 runtime cost.

## APEX Replay - No Build Static Confirmation

Problem: The user explicitly repeated the APEX demand and warned not to spam dotnet/build.
Solution: Re-read `Status_1303.md` and `Rationale_1303.md`, re-extracted prompt id 1303 from `Docs/Tasks/CURRENT_BATCH.md`, and ran only lightweight CLI checks: diff-added managed token scan, NativeArray declaration text scan, existing report consistency, and `git diff --check`.
Rejected Alternatives: Launching `dotnet build` or another heavy build-like verification was rejected by direct user instruction.
Scalability potential: No runtime change; this is confirmation only.
Hardware Impact: 0 us runtime. Latest static confirmation: prompt hash unchanged, diff-added `new=0`, forbidden managed text patterns `0`, DTO size failures `0`.

## APEX Replay - Fault Dump No-Throw Boundary

Problem: The blackbox dump path still had an unchecked `GetSubArray(telemetryOffset, capacity)` range and caller-side path construction that could throw on corrupted metadata or invalid project path before the writer's IO catches ran.
Solution: Added `telemetryOffset + capacity` validation in `DumpVerletTelemetryOnce`, moved path construction into `TryResolveTetherDumpPaths`, and added `TetherBlackBoxDumpWriter.TryWritePrimaryAndLegacy` so handleable managed failures return false without escaping the fault path. The old void writer remains only as a compatibility wrapper.
Rejected Alternatives: Claiming "no managed exception possible" over `System.IO` was rejected. OS file writes can still fail; the implemented guarantee is fail-closed containment for handleable managed exceptions and no managed exception propagation from the tether fault route.
Scalability potential: Low/Middle/High/Ultra all keep the same fixed telemetry ring and dump format. Quality changes simulation/visual density only, not forensic layout or dump authority.
Hardware Impact: 0 normal-frame cost. Fault path adds bounded branch checks and bool returns; it avoids repeated crash-path unwinding on i3/MX350-class hardware when telemetry metadata is corrupt.

## APEX v4 - Value-Type New Purge

Problem: The literal scanner still reported value-type `new` tokens in hot DTO/job/signal paths. They do not allocate managed heap, but they weaken Zero-GC evidence and hide real managed risks in the same token class.
Solution: Rewrote hot `GpuCableSplinePointDTO`, `GpuCableDrawParamsDTO`, `ImpactSignal`, `TetherTensionSignal`, `TetherSnappedSignal`, `VehicleCommandSignal`, `CableMaterialDTO`, `SdfSampleDTO`, `CableTensionForceDTO`, `CableSnappedSignal`, `CableAabbDTO`, and `VerletCableBlackBoxEntry` construction to `default` plus field assignment or `math.float4`.
Rejected Alternatives: Defending value-type constructors as harmless was technically correct but not a clean proof artifact under the user's literal static rule.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. This is auditability and hidden-allocation triage, not a fidelity change.
Hardware Impact: Measured gain `0 us`; expected frame-time gain `0 us`. The concrete result is static proof: Roslyn value-type creations in audited files are now `0`.

## APEX v4 - Stack-Only Native View

Problem: `VerletCableNodeBuffer.Nodes` was a public `NativeArray<VerletNodeDTO>` field in a regular struct, so the Roslyn native alias scanner correctly classified it as a forbidden persistent native alias candidate.
Solution: Converted `VerletCableNodeBuffer` to `ref struct`, forcing stack-only lifetime and keeping the unsafe `GetNodeRef` helper from escaping into persistent owners.
Rejected Alternatives: Adding comments or suppressing the scanner would preserve the possible stale-alias lifetime. Removing the helper outright was rejected because archived CS1612 work documents it as an intentional ref-access path.
Scalability potential: Same across Low/Middle/High/Ultra. It constrains lifetime, not simulation quality.
Hardware Impact: 0 us runtime. It removes a static lifetime hazard and reduces compaction/UAF risk.

## APEX v4 - ARM64 DTO Tail Cleanup

Problem: Several explicit DTOs had tail `ulong` pads/reserved fields after 4-byte fields. Sizes were already multiples of 8, but the field-order proof did not satisfy the requested largest-to-smallest rule.
Solution: Converted unused private tail pads in `TetherNodeDTO`, `TetherConstraintDTO`, `TetherForcePacketDTO`, `TetherAupTelemetryEntry`, and `TetherTelemetryEntry` to explicit private byte pads. Reordered `CableSnappedSignal` tail fields to 4-byte, then ushort, then byte lanes while keeping size 64.
Rejected Alternatives: Moving `TetherAupTelemetryEntry.AnchorAUP` and `TetherTelemetryEntry.AnchorAUP` to offset 0 was rejected because those are legacy cross-domain telemetry ABIs used by Cable132/Harpoon/TetherAUP dump and introspection paths. That requires a versioned ABI migration, not a silent offset rewrite.
Scalability potential: No quality behavior changes. Low/Middle/High/Ultra share the same DTO ABI.
Hardware Impact: 0 us measured. Alignment map now reports 24 numeric explicit sizes, 0 size%8 failures, and only 2 documented legacy ABI order exceptions.

## APEX v4 - No Build Decision

Problem: The user explicitly repeated that dotnet/build should be rare and not attempted on every pass.
Solution: Used existing compiled static analyzers and `git diff --check`; did not launch `dotnet build`.
Rejected Alternatives: Running a ceremonial build after static parse success was rejected because previous compile was already blocked by unrelated `FixedUiEventQueue<>` errors and the user requested restraint.
Scalability potential: No runtime effect.
Hardware Impact: Avoided shared host contention. Runtime proof remains blocked until Unity Editor/DataVault context exists.

## APEX v5 - Existing Analyzer Replay

Problem: The user repeated the APEX review demand and specifically banned repeated build/dotnet attempts. A chat-only assurance would be invalid; the owned reports had to be refreshed from source.
Solution: Re-read `Status_1303.md`, `Rationale_1303.md`, `AGENTS.md`, domain boundaries, Unity MCP skill notes, and the 8 selected mandate files. Re-extracted `<AGENT_PROMPT id="1303">` from `Docs/Tasks/CURRENT_BATCH.md`. Reran existing compiled `VoxelRuntimeHotPathAudit.exe` and `VaultNativeAliasRoslynAudit.exe` without invoking `dotnet build`.
Rejected Alternatives: Launching a ceremonial compile was rejected by direct user instruction. Rewriting cold arrays into DataVault/NativeArray was rejected because it would reintroduce the alias class this task is removing.
Scalability potential: Low/Middle/High/Ultra unchanged. This replay validates ownership and auditability; visual overkill remains a presentation concern, not a reason to bloat solver truth.
Hardware Impact: 0 us measured, 0 us claimed. Static replay result: hot-path Roslyn hash `cfb0730f657ea1f4b0dd02821184bb4b99b17735201ee1ce2fcf6be381744242`; whole-scripts Vault alias hash `b8223115ac4f9dbd841fab89ca83ebac61f78612dc02e272bde490af0766f2d7`; owned forbidden persistent candidates `0`.

## APEX v5 - Literal Managed Token Result

Problem: Literal text scans are noisy, but the user requested hard proof for `new`, string formatting, LINQ, boxing, direct scene lookup, physics allocating calls, and hidden completes.
Solution: Regenerated `Docs/Reports/ZERO_GC_HOTPATH_SCAN_1303.json` from a case-sensitive `Select-String` scan over the 4 owned files plus Roslyn AST output. Current result: forbidden text patterns `0`, raw `new` `14`, diff-added `new` `0`, managed heap `new` in audited hot ranges `0`, value-type `new` in audited hot ranges `0`.
Rejected Alternatives: Claiming "zero new filewide" was rejected because it is false. The remaining raw `new` tokens are 11 fixed cold arrays, one cold/capacity `GraphicsBuffer`, and two fault-path `FileStream` instances.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The result protects the hot frame path and keeps fault-dump IO out of solver cadence.
Hardware Impact: 0 us measured, 0 us claimed. The value is proof discipline, not frame-time improvement.

## APEX v5 - Build Suppression Decision

Problem: Build verification is still desirable but currently low value because the previous compile wall was in unrelated Core/UI code and the user explicitly told this agent not to run dotnet/build repeatedly.
Solution: Did not launch `dotnet build`, did not rebuild analyzer projects, and used already-built net10 audit executables only.
Rejected Alternatives: Running another build to satisfy bureaucracy was rejected. It would not isolate tether correctness while `FixedUiEventQueue<>` remains an unrelated compile wall.
Scalability potential: No runtime effect.
Hardware Impact: Avoided host contention for parallel agents. Runtime fuzzer and GCMonitor proof remain `PENDING VERIFICATION` until Unity Editor/DataVault context exists.

## APEX v6 - Slot Reservation Bitmask

Problem: The v5 raw `new` ledger still contained a static `bool[64]` reservation array in `TetherInstance`. It was cold, but it was still a managed heap object and a weak proof point under the user's literal scan.
Solution: Replaced `_dataVaultSlotReservations` with `ulong s_dataVaultSlotReservationMask`. Acquire scans 64 bits, reserve uses `|=`, release uses `&= ~`. `SubsystemRegistration` reset now writes one scalar zero.
Rejected Alternatives: Moving the reservation state into `NativeArray` was rejected because it would reintroduce native alias ownership. Adding a lock was rejected because Unity instance lifecycle is main-thread here and the previous array had no thread-safety guarantee either.
Scalability potential: Low/Middle/High/Ultra unchanged. This is ownership hygiene, not fidelity logic.
Hardware Impact: 0 us measured. One cold managed allocation removed; raw `new` count drops `14 -> 13`.

## APEX v6 - Analyzer Replay No Build

Problem: After the bitmask patch, proof hashes had to be refreshed without spamming `dotnet build`.
Solution: Reran existing compiled Roslyn binaries only. Hot-path audit hash is `2d75b3135cd202dbf95cad4012857c141fec51528911d7b8eba3896c10210f66`; whole-scripts Vault hash is `a8920ccb4bc926880c51855d4d97b30bc8bfc0aeb1fe507bc5f5c1e9e2c29531`; owned forbidden persistent native candidates remain `0`.
Rejected Alternatives: Launching a new build was rejected by direct user instruction and because the prior compile wall was unrelated Core/UI `FixedUiEventQueue<>`.
Scalability potential: No runtime effect. Verification debt remains Unity-context only.
Hardware Impact: Avoided host contention. No runtime microsecond claim.

## APEX v7 - Corrupted Range Fail-Closed Guards

Problem: Several slice helpers validated ranges with `offset + length`. Under corrupted `int` metadata that expression can overflow before comparison and create a false valid window.
Solution: Replaced those guards with subtraction-form checks: `offset > limit` and `length > limit - offset`. The same rule is applied against `totalLength`, resolved Vault array length, and acquired write-lock buffer length.
Rejected Alternatives: Keeping the old addition-form check was rejected because it is only safe under trusted metadata. Throwing exceptions was rejected because the tether fault path must fail closed and emit telemetry, not unwind managed state.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid data. Corrupted metadata now exits before `GetSubArray`, so low-end devices avoid repeated exception churn during fault storms.
Hardware Impact: Normal path pays a few integer branches. Measured gain `0 us`; correctness gain is removal of a corrupted-range overflow class.

## APEX v7 - Dump Size Overflow Gate

Problem: The `.h8dump` writer computed `DumpHeaderBytes + ring.Length * entrySize` in `int`. A corrupted or unexpected ring length could overflow before `SetLength`/MMF creation.
Solution: Compute payload and total size in `long`, return false when the payload is non-positive or total exceeds `int.MaxValue`, then cast only after the gate.
Rejected Alternatives: Claiming `NativeArray.Length` is always small was rejected because crash dump code exists specifically for corrupted-state evidence. Replacing fault-path `FileStream` with a new runtime service was rejected in this pass because no existing background unmanaged writer ownership exists in this domain.
Scalability potential: Dump format and 300-frame ring stay fixed across Low/Middle/High/Ultra; quality scales simulation/visual work, not forensic layout.
Hardware Impact: 0 normal-frame cost. Fault path gains deterministic refusal before OS file APIs when size metadata is invalid.

## APEX v7 - Analyzer Replay No Build

Problem: After the fail-closed patch, evidence had to be refreshed, but the user explicitly warned not to launch dotnet/build repeatedly.
Solution: Reran existing compiled static analyzers only: hot-path Roslyn audit, Vault native alias audit, SignalBus hot-path audit, JobCompletionAudit, literal managed-token scan, and `git diff --check`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction and because previous compile proof was blocked by unrelated Core/UI `FixedUiEventQueue<>` errors.
Scalability potential: No runtime effect. This is proof refresh and fault containment, not a new simulation feature.
Hardware Impact: Avoided shared host contention. Current static proof: raw `new=13`, forbidden managed text patterns `0`, owned forbidden native aliases `0`, owned SignalBus errors `0`, DTO size%8 failures `0`.

## APEX v8 - Background Dump Queue

Problem: Task 15 explicitly required background serialization of the 300-frame tether telemetry ring, but the existing route was synchronous. A naive worker cannot hold a `NativeArray<T>` after Vault lock release without preserving a stale physical alias.
Solution: Added `TetherBlackBoxDumpWriter.TryQueuePrimaryAndLegacy`. It copies the ring into an unmanaged snapshot while the Vault lock is still held, stores the snapshot behind an `IntPtr` descriptor, signals a background worker, and only then lets `TetherInstance` release the blackbox locks. `TryWritePrimaryAndLegacy` remains a no-throw fallback when queueing fails.
Rejected Alternatives: Holding the original `NativeArray` for the worker was rejected as stale-pointer debt. Keeping a static typed `byte*` field was rejected after the Roslyn NativeAlias audit correctly classified it as a forbidden persistent native alias. Claiming a C# background worker is free of managed allocation was rejected; `Thread`/`AutoResetEvent` are explicit cold/fault allocations.
Scalability potential: Low/Middle/High/Ultra share the same fixed dump format and 300-frame forensic ring. Quality does not alter dump authority.
Hardware Impact: 0 normal-frame cost. Fault path adds one unmanaged memcpy and a worker signal; no measured microsecond saving is claimed.

## APEX v8 - Defrag Race Fuzzer

Problem: Task 17 had only a blocked note, not an implementation. Without a fuzzer, the Vault relocation contract was not stressable against live tether jobs.
Solution: Added `TetherMemorySovereigntyValidator1303` under `#if UNITY_EDITOR` in `TetherVerletJobs.cs`. It creates a test `GlobalDataVault`, registers tether buffers, acquires write locks, schedules `GenerateMockTetherLoadJob`, `TetherVerletIntegrationJob`, and `VerletCableSolverJob`, while a background thread repeatedly calls `RequestEditorForceDefragmentation` and `FrostTickDefrag`. After release it reacquires the generation handle and checks read-only access after `GenerateMockVaultRelocationForValidation`.
Rejected Alternatives: A CLI-only fake pass was rejected because it cannot prove Unity job/Vault relocation behavior. Moving the fuzzer to an Editor folder was rejected because the job structs are internal runtime types and exposing them as public API only for a validator would widen the release surface.
Scalability potential: Low can lower `StressNodeCount` in a future profile; Middle/High/Ultra can increase iterations. This fuzzer is editor-only and does not affect runtime quality scaling.
Hardware Impact: Runtime cost is 0 us. Editor fuzzer cost is intentionally heavy and unmeasured because Unity Editor was not launched in this pass.

## APEX v8 - Static Replay After Fuzzer

Problem: The new worker/fuzzer code adds legitimate `new` tokens and job completions. Hiding them would make the Zero-GC report false.
Solution: Regenerated `ZERO_GC_HOTPATH_SCAN_1303.json`, `APEX_V8_STATIC_REVIEW_1303.json`, `VAULT_EXORCISM_REPORT_1303.json`, hot-path Roslyn audit, NativeAlias audit, SignalBus audit, and JobCompletion audit. Current proof: forbidden text patterns `0`, broad `catch (Exception)` `0`, owned forbidden persistent native aliases `0`, raw `new=17`, and managed heap `new` in audited solver/frame hot ranges `0`.
Rejected Alternatives: Reporting filewide zero `new` was rejected because Task 15's background worker cannot exist in C# without managed worker objects unless a pre-existing native crash service owns that thread.
Scalability potential: Runtime scalability unchanged. Fault/editor tooling now exposes the true verification cost instead of polluting the solver path.
Hardware Impact: No build, no runtime profiler. Static replay only; microsecond claim remains 0 measured / 0 saved.

## APEX v9 - Registered Dump Snapshot

Problem: The v8 background dump queue used `UnsafeUtility.Malloc` for the process-lifetime snapshot buffer without registering that raw pointer in `NativeMemorySentinel`, and the idle worker state had no explicit subsystem reload release path.
Solution: Added `NativeMemorySentinel.RegisterPointer` immediately after snapshot allocation, `NativeMemorySentinel.Unregister` before every successful snapshot `UnsafeUtility.Free`, and an idle `SubsystemRegistration` release path that signals/disposes the worker event and frees only when no dump is pending or writing.
Rejected Alternatives: Leaving the pointer as an undocumented process-lifetime allocation was rejected because the native memory mandate requires owner/label/lifetime tracking for raw persistent allocation. Freeing the buffer while a worker is writing was rejected because that would create the exact stale-pointer class this task removes.
Scalability potential: Low/Middle/High/Ultra runtime solver behavior unchanged. The correction only hardens fault-path ownership and editor/domain reload hygiene.
Hardware Impact: 0 normal-frame cost. Fault-path snapshot allocation now pays one cold Sentinel registration; no measured microsecond saving is claimed.

## APEX v9 - Analyzer Replay No Build

Problem: The Sentinel patch required fresh proof, but the user explicitly forbade repeated dotnet/build attempts.
Solution: Reran existing compiled static analyzers only: hot-path Roslyn audit, Vault native alias audit, SignalBus audit, JobCompletionAudit, literal managed-token scan, boxing-risk text scan, and `git diff --check`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Claiming runtime GCMonitor proof was rejected because Unity Editor was not launched.
Scalability potential: No runtime feature change. Verification still scales by static proof in shell and Unity fuzzer/profiler later.
Hardware Impact: Avoided build contention. Current proof: hot-path hash `0f90eac6f4e950109366bfdf34778811eef6f18bc263b6944036e71e719f3b6f`, native alias hash `ec09fb0999cdd9e91db16bb8a7b06a231d0694ff0e44d9777fa977bb7ea00a9d`, owned forbidden native aliases `0`, forbidden managed patterns `0`, boxing-risk text hits `0`.

## APEX v10 - Idle Worker Snapshot Release

Problem: The v9 subsystem reload path signaled the dump worker and attempted one short join before deciding whether to free the Sentinel-registered unmanaged snapshot. If the worker exited just after that check, the snapshot could remain allocated until another cleanup opportunity.
Solution: `TryReleaseIdleWorkerState` now signals the worker, attempts a join, disposes the signal to break a late wait, attempts a second join, and releases the snapshot only when the worker is stopped or observably dead.
Rejected Alternatives: Unconditionally freeing the snapshot was rejected because a still-alive worker could be inside `TryWriteQueuedDumpFile`. Leaving the v9 single-join logic was rejected because it weakened the native ownership proof on domain reload.
Scalability potential: Low/Middle/High/Ultra runtime solver behavior unchanged. This is fault/reload lifecycle hardening, not fidelity work.
Hardware Impact: 0 normal-frame cost. Reload/fault cleanup pays at most two 100 ms bounded joins outside gameplay frame execution; no microsecond saving is claimed.

## APEX v10 - Analyzer Replay No Build

Problem: The cleanup patch changed source and invalidated v9 static hashes, but the user explicitly warned not to spam dotnet/build.
Solution: Reran existing compiled static analyzers and text scans only. Hot-path hash is `7b8b527d31275961907deffda176d81af60a13311e0e8a4fa42cbd32ac2c5212`; whole-scripts native alias hash is `0994f81a4a2def4d40239687ba4a6e97c7ac10a9a8384bb05c359d02447d1988`; strict root hash remains `254906112e60fba00917c34dafe995f2cc66cd70ff89c10a0df3faa68edf7087`; forbidden managed patterns remain `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction and because this pass only changed fault/reload cleanup logic.
Scalability potential: No runtime feature change. Unity Editor fuzzer and GCMonitor proof remain a later runtime-verification step, not a shell claim.
Hardware Impact: Avoided build contention. Current proof: raw `new=17`, owned forbidden native aliases `0`, owned SignalBus errors `0`, DTO size%8 failures `0`.

## APEX v11 - Failed Queue Descriptor Scrub

Problem: `TryQueuePrimaryAndLegacy` could populate `s_primaryPath`, `s_legacyPath`, and `s_pendingByteCount`, transition to `DumpStatePending`, then fail on a null/disposed signal or handled exception. State returned to idle, but stale descriptor fields were not explicitly scrubbed before a later queue attempt.
Solution: Added `ClearPendingDumpDescriptor()` and call it on failed queue handoff and handled queue exceptions. This keeps the next fault dump from inheriting stale file paths or byte counts after a partial handoff failure.
Rejected Alternatives: Leaving stale fields because state was idle was rejected; crash dump code must be auditable in corrupted states. Allocating a managed queue object was rejected because it would add another fault-path heap object without improving ownership.
Scalability potential: Low/Middle/High/Ultra unchanged. The dump format and 300-frame ring remain fixed; quality scaling never changes forensic layout.
Hardware Impact: 0 normal-frame cost. Fault-path impact is three scalar/static field clears; measured saving `0 us`, correctness gain is stale descriptor elimination.

## APEX v11 - Signal-Null Worker Cleanup

Problem: The v10 reload cleanup joined a worker when a signal existed. A rare inconsistent state with `s_dumpSignal == null` and a live `s_dumpWorker` would skip the join attempt and defer Sentinel snapshot release.
Solution: Added an `else if` path that attempts a bounded join on a live worker even when the signal reference is already null, then releases the Sentinel-registered snapshot only if the worker is stopped or observably dead.
Rejected Alternatives: Unconditionally freeing the snapshot was rejected because a live worker may still write it. Spinning until the worker stops was rejected because reload cleanup must stay bounded.
Scalability potential: No fidelity change. Low devices avoid native leak persistence after fault/reload edges; high-tier devices receive identical behavior.
Hardware Impact: 0 normal-frame cost. Reload/fault cleanup pays at most the existing bounded join window; measured saving `0 us`.

## APEX v11 - Analyzer Replay No Build

Problem: The v11 fault-path patch invalidated v10 hashes, but the user explicitly repeated not to launch dotnet/build repeatedly.
Solution: Reran existing analyzer binaries only: hot-path Roslyn, Vault native alias, SignalBus, a targeted job-completion scan, literal managed-token scan, JSON validation, and `git diff --check`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Claiming Unity fuzzer execution was rejected because Unity MCP/Editor execution is unavailable in this shell.
Scalability potential: Static proof only; solver quality remains driven by continuous `GlobalQualityWeight` and existing taut-line visual fake under low fidelity.
Hardware Impact: Avoided build contention. Current proof: raw `new=17`, forbidden managed patterns `0`, direct `Rigidbody.AddForce` in owned files `0`, owned forbidden native aliases `0`, owned SignalBus errors `0`.

## APEX v12 - Managed Scratch Array Removal

Problem: The v11 report correctly classified ten `TetherInstance` arrays as cold per-instance scratch, but they still produced managed heap objects and weakened the user's literal `new` audit.
Solution: Replaced bend points, bend normals, anchor positions, anchor velocities, solver anchors, segment lengths, segment rest lengths, bend volumes, and bend runtime stamps with scalar fields and fixed-index `ref` accessors. Active windows still use `_bendPointCount`, `MaxAnchors`, and `MaxSegments`; invalid index access returns a discard slot instead of throwing.
Rejected Alternatives: Moving the scratch into `NativeArray` was rejected because it would reintroduce private native alias ownership. Keeping arrays as "cold enough" was rejected because the APEX audit explicitly demanded removal of avoidable managed allocations. `InlineArray` was rejected because Unity C# profile compatibility is not guaranteed here.
Scalability potential: Low/Middle/High/Ultra gameplay truth is unchanged. The change removes managed scratch debt without increasing simulation fidelity; visual overkill remains gated in `VISUAL_SYNC` by continuous `GlobalQualityWeight`.
Hardware Impact: 0 us measured. Static proof improves: raw `new` drops `17 -> 7`; solver/frame hot ranges remain `0` managed heap `new`; Roslyn parse failures remain `0`.

## APEX v12 - Analyzer Replay No Build

Problem: The scalar-slot patch invalidated v11 static hashes and could have introduced syntax risk through `ref` accessors.
Solution: Reran existing compiled analyzers only. `VoxelRuntimeHotPathAudit` parsed 4 files with 0 parse failures and hash `36ea55fdeaf0273f6950f4ff9746bc998cbd0c2449722d26df5708e9029f5434`; `VaultNativeAliasRoslynAudit` reports strict-root forbidden `0`, owned forbidden persistent native aliases `0`, whole hash `2320e4632c846b77c9daa0a4a316599dd9f303ac94b8e8b48198a99446fcb4f5`; SignalBus owned errors `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction and because this pass is source-local and already Roslyn-parsed. Claiming Unity fuzzer execution was rejected because Unity Editor/MCP execution is unavailable.
Scalability potential: No runtime fidelity change. Verification debt remains Unity Editor fuzzer and GCMonitor, not shell-build ceremony.
Hardware Impact: Avoided shared build contention. `git diff --check` reports only CRLF normalization warnings, no whitespace errors.

## APEX v13 - Successful Dump Drain Descriptor Scrub

Problem: `DrainPendingDump` wrote the queued primary/legacy dump files and returned the dump worker to idle, but the static descriptor fields still contained the last primary path, legacy path, and byte count until another cleanup path ran.
Solution: Added `ClearPendingDumpDescriptor()` at `TetherBlackBoxDumpWriter.cs:489` after both queued file writes and before `DumpStateIdle`. The state transition is now `Pending -> Writing -> descriptor cleared -> Idle`.
Rejected Alternatives: Leaving stale fields because the state flag was idle was rejected; crash code must be forensically unambiguous after corrupted-state execution. Allocating a managed descriptor object or queue was rejected because scalar static cleanup is enough and adds no heap pressure.
Scalability potential: Low/Middle/High/Ultra solver behavior unchanged. The correction is fault-path hygiene only; visual fidelity still scales through continuous quality routes and existing visual fake paths.
Hardware Impact: 0 normal-frame cost, 0 us measured. Fault-path impact is three scalar/null stores after a dump drain.

## APEX v13 - Analyzer Replay No Build

Problem: The descriptor scrub changed fault-path source and invalidated v12 hashes, but the user explicitly forbade repeated dotnet/build attempts.
Solution: Reran existing static analyzers and text scans only. Hot-path Roslyn hash is `5f66f84bee5d61523e932fa2c2fc612439dc65d0f4e20d4b9a071c1c7b41b2e9`; whole-scripts native alias hash is `b2004f09f0b39302f1dffca207714de2dc55edce4ffbc73511138f2c205538ef`; owned forbidden persistent native aliases remain `0`; forbidden managed text patterns remain `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Claiming runtime GCMonitor or Unity fuzzer proof was rejected because Unity Editor execution is unavailable in this shell.
Scalability potential: Static proof only; no simulation iteration, math fidelity, or quality branch changed.
Hardware Impact: Avoided build contention for parallel agents. Current proof: raw `new=7`, owned SignalBus errors `0`, DTO size%8 failures `0`, job frame-path blockers `0`, JSON reports parse.

## APEX v14 - Legacy Dump Wrapper Queue-First Route

Problem: Two older call sites still used `TetherBlackBoxDumpWriter.WritePrimaryAndLegacy`. The compatibility wrapper went straight to synchronous writer fallback, so old callers could bypass the v8 queued snapshot path.
Solution: Changed `WritePrimaryAndLegacy` to attempt `TryQueuePrimaryAndLegacy` first, then use `TryWritePrimaryAndLegacy` only if queueing fails. This gives `TetherManager` and `TetherAupVerletJobs` the same queued handoff behavior without changing their call signatures.
Rejected Alternatives: Editing every older caller to duplicate queue/fallback logic was rejected because it increases drift. Removing the synchronous fallback was rejected because catastrophic queue failure would drop the mandated dump instead of failing closed with best-effort evidence.
Scalability potential: Low/Middle/High/Ultra runtime solver behavior unchanged. Fault dump work stays outside the normal frame path.
Hardware Impact: 0 normal-frame cost. Fault path avoids synchronous IO by default after queue setup; measured saving `0 us` because no runtime profiler was launched.

## APEX v14 - MemoryMappedFile Sidecar Removal

Problem: Primary `.h8dump` write used `MemoryMappedFile.CreateFromFile`, `CreateViewAccessor`, and `AcquirePointer` on standalone/editor paths. Regex `new` did not catch those managed sidecars, so v13 was too generous.
Solution: Removed `System.IO.MemoryMappedFiles` and the whole MMF branch. Primary `.h8dump` now writes the stack header plus unmanaged ring payload through `WriteStreamPayload`, same as the non-standalone path.
Rejected Alternatives: Keeping MMF for fault-path throughput was rejected because hidden managed allocations are worse evidence than slower crash-only stream writes. A platform-native writer was rejected because no existing Core/native bridge owns it in this domain.
Scalability potential: Dump format stays fixed across Low/Middle/High/Ultra. Quality never changes forensic layout.
Hardware Impact: 0 normal-frame cost. Fault-path throughput may be lower, but managed sidecar count is reduced; measured saving `0 us`.

## APEX v14 - Analyzer Replay No Build

Problem: v14 source changes invalidated v13 static evidence, but the user explicitly warned not to run dotnet/build repeatedly.
Solution: Reran existing static analyzers and text scans only. Hot-path analyzer stdout hash is `62d3154585aac613c1dd75a7e1c2c7f74ea0d683d07bb3c440b9de9845264454`; whole-scripts native alias hash is `b2004f09f0b39302f1dffca207714de2dc55edce4ffbc73511138f2c205538ef`; owned forbidden persistent native aliases remain `0`; `MemoryMappedFile/CreateViewAccessor` hits are `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Faking Unity fuzzer or GCMonitor proof was rejected.
Scalability potential: Static proof only; no solver iteration or visual quality branch changed.
Hardware Impact: Avoided build contention. Current proof: raw `new=7`, forbidden patterns `0`, owned SignalBus errors `0`, DTO size%8 failures `0`, job frame-path blockers `0`.

## APEX v15 - Atomic DataVault Slot Reservation

Problem: `TetherInstance.s_dataVaultSlotReservationMask` used a non-atomic static read/modify/write path for `_dataVaultSlot` reservation. The path is cold, but two instances racing through bootstrap could reserve the same GlobalDataVault slot or clear another instance bit on release.
Solution: Converted the mask to `long`, reset it with `System.Threading.Volatile.Write`, and implemented acquire/release as compare-exchange loops with `System.Threading.Interlocked.CompareExchange`. Bit 63 remains valid because the mask is used as bits, not signed magnitude.
Rejected Alternatives: A managed `lock` object was rejected because it adds another heap object and hides contention. A `bool[]` reservation table was rejected because APEX v6 removed that managed array. Assuming all bootstrap happens on one Unity thread was rejected because the prompt asked for release-grade descriptor consistency, not convention.
Scalability potential: Low/Middle/High/Ultra behavior is identical. This does not change solver truth, quality, DTO layout, or visual fidelity; it removes a rare bootstrap race that could corrupt Vault ownership.
Hardware Impact: 0 normal-frame cost. Cold slot acquisition/release pays a few atomic instructions; measured saving `0 us`, correctness gain is removal of duplicate slot ownership.

## APEX v15 - Analyzer Replay No Build

Problem: The atomic slot patch and report-correction pass invalidated v14 evidence. The earlier report also had two proof defects: VaultNativeAlias current CLI expects `--output`, and a case-insensitive text scan counted `math.select` as LINQ.
Solution: Reran existing analyzer binaries and regenerated reports only. Current proof: hot-path hash `62d3154585aac613c1dd75a7e1c2c7f74ea0d683d07bb3c440b9de9845264454`; strict native alias hash `254906112e60fba00917c34dafe995f2cc66cd70ff89c10a0df3faa68edf7087`; whole native alias hash `eb69bf6ba43aeaf038a57870c8a68675eaf3ce185ffce791029adea5b18bbedb`; forbidden managed patterns `0`; MemoryMappedFile patterns `0`; owned forbidden persistent native aliases `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Leaving stale analyzer hashes in `VAULT_EXORCISM_REPORT_1303.json` was rejected because the CTO reads files, not chat. Claiming runtime GCMonitor or Unity fuzzer proof was rejected because Unity Editor was not launched.
Scalability potential: Static proof only; no simulation iteration, quality branch, or cinematic cheat changed. Existing continuous `GlobalQualityWeight` routes remain untouched.
Hardware Impact: Avoided build contention for parallel agents. Current proof: raw `new=7`, hot-path managed heap `new=0`, owned SignalBus errors `0`, DTO size%8 failures `0`, job frame-path blockers `0`, JSON reports parse.

## APEX v16 - Telemetry DTO ARM64 Repack

Problem: v15 still carried two `legacyAbiOrderExceptions`: `TetherAupTelemetryEntry` and `TetherTelemetryEntry` placed 4-byte scalar fields before `double3 AnchorAUP`. Size was 64 and aligned, but field order violated the stricter ARM64 directive.
Solution: Repacked both explicit-layout telemetry DTOs so `AnchorAUP` starts at offset `0`, all 4-byte fields start at offset `24`, and byte padding remains at offsets `56..63`. `VerletCableLayout.ValidateTetherAupLayouts()` now asserts the new offsets for both structs.
Rejected Alternatives: Keeping the exceptions as "legacy ABI" was rejected because this batch is the memory/layout exorcism for tethers. Changing public field names was rejected because object initializers and readers across Cable132/Harpoon/AUP should keep source compatibility.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. The correction is data layout only; continuous `GlobalQualityWeight` and visual fake routes are untouched.
Hardware Impact: 0 normal-frame cost, 0 us measured. ARM64 memory layout proof improves: DTO map now reports explicit structs `26`, size%8 failures `0`, high-to-low order failures `0`, legacy ABI exceptions `0`.

## APEX v16 - Raw Prompt Hash Correction

Problem: v15 report used stale normalized prompt hash `6a477d1c3c9f2028d788ea18d9fa530be4c4852ce05d44792c82133ad30482c0`. Re-extraction from the current CRLF `Docs/Tasks/CURRENT_BATCH.md` XML block gives raw UTF-8 hash `9a3528042794113df9c5d3c4840d010ac34b37f3eff28dacd9a611dff5917309`.
Solution: v16 proof artifacts use the raw current XML hash and keep task count `20`. The normalized hash is no longer used as the live proof key.
Rejected Alternatives: Leaving the stale hash in current `VAULT_EXORCISM_REPORT_1303.json` was rejected because it breaks traceability from report to batch file. Rewriting history-only v15 report files was rejected; v16 supersedes them with corrected evidence.
Scalability potential: No runtime effect. This is evidence hygiene for anti-amnesia protocol.
Hardware Impact: 0 us. No build launched; existing analyzers only.

## APEX v16 - Analyzer Replay No Build

Problem: DTO layout changed and required fresh static evidence, but the user explicitly repeated not to run dotnet/build repeatedly.
Solution: Reran existing static analyzer binaries and text scans only. Hot-path hash `62d3154585aac613c1dd75a7e1c2c7f74ea0d683d07bb3c440b9de9845264454`; whole native alias hash `f5bfe52c3cab0b2f06dc14c0ec544163cf0e165e64334ea522738a2f8ad8b848`; owned forbidden native aliases `0`; forbidden managed text patterns `0`; DTO order failures `0`.
Rejected Alternatives: Running `dotnet build` was rejected by direct user instruction. Claiming Unity fuzzer or GCMonitor execution was rejected because Unity Editor was not launched.
Scalability potential: Static proof only. No solver iteration count, quality branch, or visual overkill path changed.
Hardware Impact: Avoided build contention. Current proof: raw `new=7`, hot-path managed heap `new=0`, owned SignalBus errors `0`, DTO size%8 failures `0`, DTO order failures `0`, job frame-path blockers `0`.
