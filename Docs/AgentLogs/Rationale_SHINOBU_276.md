# SHINOBU_276 Rationale

Date: 2026-05-21
Status: LOOP 30 GLOBAL QUALITY LIVE ROUTE AND SCANNER ATOMIC UPSERT / COMPILE BLOCKED BY EXTERNAL MISSING SOURCE
Domain: Echelon 4 Player, Kinematics & Tools / Exosuit 6DoF Kinematics

## Decision 00: Authority And Scope Gate

Problem: The prompt requires a large Exosuit physics replacement while 20+ agents may be editing adjacent systems. Direct dependencies on absent systems or cross-domain rewrites would create compile walls.
Solution: Start with repository archaeology, then add a bounded Echelon 4 kinematics module that uses unmanaged DTOs, Burst jobs, static validation, and documented adapter points. Global authority routes must stay cold/injected or DataVault/snapshot based.
Rejected Alternatives: A duplicate mech rig was rejected because Task 01 requires hijacking existing controllers if present. A global Rigidbody purge was rejected because non-exosuit physics is outside the assigned domain.
Scalability potential: Low uses 2 SDF depenetration iterations and cheap hydraulic damping; Middle increases collision fidelity; High records denser telemetry and smoother clamp normals; Ultra spends saved CPU on presentation/IK fidelity, not gameplay truth bloat.
Hardware Impact: Target is sub-20 us fixed-tick solver on i3/MX350 for one suit. Static estimate only until profiler proof exists.

## Decision 01: Mandate Set

Problem: The task spans physics, AUP, DTO layout, jobs, SDF, and phase ownership.
Solution: Read these 8 mandates before implementation: `PHYS_Physics_Integrity_Determinism_ForceMode`, `CORE_Submarine_Vehicles_Kinematics_AUP`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `DATA_Runtime_Struct_Layout_ARM64`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline`, `ARCH_Execution_Phases`.
Rejected Alternatives: Reading the whole registry was rejected as context noise. Reading only physics was rejected because ARM64/AUP/DataVault constraints are primary acceptance gates.
Scalability potential: Mandates force continuous quality weight, hysteresis, and phase ownership across Low, Middle, High, Ultra.
Hardware Impact: Mandate-driven flat native data and AUP-local math avoid managed GC and float-origin jitter on weak/mobile silicon.

## Decision 02: Exact State DTO

Problem: The existing `ExosuitStateDTO` stored hydraulic pressure and anchor normal in the authority state, violating the batch layout and making rollback snapshots ambiguous.
Solution: Replace it with the exact 64-byte layout: `AUP_Position`, `Velocity`, `AngularVelocity`, `ThrusterHeat`, `Flags`, `ReservedLock`, padding. Move hydraulic pressure to `ExoScreenDTO` and push normals to solver output.
Rejected Alternatives: Keeping compatibility aliases was rejected because properties/aliases would invite CS1612-style metadata mutation and break blind MemCpy assumptions.
Scalability potential: Low/Middle/High/Ultra all read one fixed DTO. Fidelity scales through jobs and tuning, not through state shape.
Hardware Impact: ARM64-safe field offsets reduce unaligned access risk on Quest-class devices; static gain is correctness, not a measured microsecond win.

## Decision 03: Runtime Bridge Instead Of Global Rigidbody Purge

Problem: `HectonPlayerMovement` is a large shared player authority with non-exosuit movement. Deleting Rigidbody paths globally would sabotage unrelated player, swimming, and transport domains.
Solution: Add `TrySubmitExosuitKinematicAuthority` and guard exosuit grapple/jump jet force paths when `ExosuitKinematicsRuntime` is active. The runtime receives intent as unmanaged frame input and drives the kinematic DataVault route.
Rejected Alternatives: Removing `Rigidbody` from the player controller was rejected because the controller still owns non-exosuit locomotion. Adding a second mech rig was rejected because an exosuit runtime already exists.
Scalability potential: Low runs 2 collision substeps; Middle 4-5; High 6-7; Ultra 8 and smoother presentation. Same player bridge remains valid.
Hardware Impact: Avoiding exosuit PhysX force/joint routes removes solver stalls for the suit path. Static estimate: saves the PhysX wait entirely when runtime is present; measured ms pending compile/runtime.

## Decision 04: SDF Adapter Boundary

Problem: The prompt requires Voxel SDF collision, but repository archaeology found no public Burst-safe voxel sampler contract; public voxel APIs are managed/read-model surfaces.
Solution: Initial implementation kept the exosuit job on an analytic SDF payload and provided a named `ExosuitSdfCollisionJob` with the same depenetration contract. Decision 10 supersedes this as the active collision route: the runtime now snapshots the owner-published voxel SDF payload and the analytic SDF remains fallback only.
Rejected Alternatives: Calling `HectonVoxelVolume` or registry voxel services from inside Burst was rejected because it would violate zero-GC, Burst, and phase ownership rules.
Scalability potential: Low uses cheapest cylinder/floor SDF; Middle/High/Ultra increase substeps through continuous quality. A real voxel payload can spend saved cycles on denser gradients later.
Hardware Impact: Analytic SDF is predictable on i3/MX350 and Quest 3. It is less geometrically complete than voxel SDF, but does not risk managed stalls.

## Decision 05: Heat Without Inventory Coupling

Problem: The task asks thruster heat plus SOA Inventory battery drain, but no stable Agent 141 battery-cell contract was exposed in the exosuit domain.
Solution: Implement heat generation, cooling, overheat hysteresis, and thruster disable in `ExosuitStateDTO.Flags`. Leave battery drain as a blocked cross-domain adapter, not a guessed dependency.
Rejected Alternatives: Directly referencing inventory internals was rejected because it would create a hard cross-domain dependency and fail with concurrent agents.
Scalability potential: Heat cadence is continuous and independent of quality; low devices do not change gameplay truth, only solver fidelity.
Hardware Impact: Heat math is scalar, branch-light, and expected below 1 us. Battery drain awaits owner route.

## Decision 06: Verification Gate

Problem: Compile verification is required, but initial CPU load sampled at 100 percent and the project protocol forbids dotnet/Unity build under that load. A later gate sample cleared at 23 percent with no `dotnet/csc`.
Solution: Run non-build static checks first. After the gate cleared, launch one narrow `dotnet build .\Hecton8.Core.csproj --no-restore /m:1 /p:BuildInParallel=false`. It failed before SHINOBU_276 compile diagnostics on pre-existing `CS2001: Assets/_Project/Scripts/IBuildPlacementRule.cs could not be found`; git status shows that file deleted outside this task.
Rejected Alternatives: Launching dotnet under high CPU was rejected by explicit instruction. Editing the generated project file or recreating `IBuildPlacementRule.cs` was rejected because that deleted build-placement route belongs to another domain and prior logs show it is an existing compile wall. Reporting a fake compile pass was rejected.
Scalability potential: Static checks do not prove runtime performance; profiler proof remains required once build gate opens.
Hardware Impact: No benchmark generated. The target remains sub-20 us for one suit; proof remains blocked by an unrelated missing-source compile wall.

## Decision 07: Core Facade Instead Of Sibling Runtime Call

Problem: The first bridge from `HectonPlayerMovement` directly referenced `ExosuitKinematicsRuntime`. That was functionally small but architecturally wrong: player authority should not hard-call a sibling Physics runtime surface for hot input submission.
Solution: Move action bits and `ExosuitFrameInputDTO` into `Hecton8.Core.Contracts`; add `Hecton8.Core.ExosuitKinematicAuthority` as a pending unmanaged DTO bridge. `ExosuitKinematicsRuntime` binds/unbinds the input `VaultGenerationHandle`, consumes pending player input in its owner phase, and writes the Vault row before scheduling the solver.
Rejected Alternatives: Keeping the static Physics runtime bridge was rejected because it creates a sibling compile-wall smell. Polling `GlobalRegistry.DataVault` from every player call was rejected because hot authority reads must use cached routes. Direct player writes into the Vault row during the solver read window were rejected because they can race the scheduled job.
Scalability potential: Low/Middle/High/Ultra all write the same 32-byte intent row; solver quality remains controlled by `GlobalQualityWeight`, not by player bridge shape.
Hardware Impact: Static route improvement. It removes repeated global route lookup in the exosuit player bridge and keeps the hot input path to one 32-byte struct assignment; measured microseconds pending profiler.

## Decision 08: Tuning Fields Must Affect Solver Math

Problem: The editor mandate required Suit Mass, SDF Epsilon, Gravity Multiplier, and Max Substeps. A tuner that wrote fields not consumed by Burst would be a fake facade.
Solution: Extend `ExosuitTuningDTO` to 80 bytes with `SdfEpsilonMeters`, `GravityMultiplier`, and `MaxSubsteps`; hash/sanitize these fields; wire them into the integrator, standalone SDF job, CSV parser, UI Toolkit sliders, layout verifier, self-audit, and architecture docs.
Rejected Alternatives: Leaving hard-coded SDF skin, gravity, and 8-step cap was rejected because human tuning would not affect runtime. Changing `ExosuitStateDTO` was rejected because rollback truth layout is fixed at 64 bytes.
Scalability potential: Low uses wider epsilon and 2-step contact; middle scales through 4-6; high/ultra tighten epsilon and reach the tuned max substeps. The quality curve is continuous and does not alter authority ownership or DTO identity.
Hardware Impact: Low-end silicon can reduce SDF iterations and tolerate a thicker skin. High-end devices spend the saved cycles on smoother contact/haptic/silt presentation. Exact cost remains unmeasured until Unity profiler can run.

## Decision 09: Tiny Job Collapse

Problem: The initial runtime scheduled a one-row mock-input job followed by a one-row integration job. That violates the project preference against tiny jobs and same-frame schedule/readback patterns without profiler proof.
Solution: Keep `GenerateMockExosuitInputsJob` as an isolated fuzzer/fallback artifact for Task 05, but fold production procedural input blending into `ExosuitKinematicIntegrationJob` via `ProceduralWeightMilli`. Runtime now schedules only the integration job.
Rejected Alternatives: Deleting the mock job was rejected because the batch explicitly requested it. Keeping it scheduled every fixed tick was rejected because it adds scheduler overhead for one row.
Scalability potential: The single-job route scales from weak devices to ultra without changing gameplay truth; higher quality still spends work inside the solver where it is amortized by contact fidelity.
Hardware Impact: Removes one scheduled job from the normal exosuit frame. Exact savings depend on Unity job scheduler overhead and are not claimed without profiler proof.

## Decision 10: Owner-Published Voxel SDF Route

Problem: The previous proof still treated analytic `MockTerrainSDF` as the active collision source, even though the repository already exposes a byte-encoded published voxel SDF payload used by fauna, animation, and audio. That left Task 07 under-strength and risked a fake "Voxel SDF" claim.
Solution: Add a read-only `NativeArray<byte>` voxel SDF lane to `ExosuitKinematicIntegrationJob`, initially snapshot voxel metadata in runtime owner phase, and alias `BufferID.VoxelSdfTexture3D` when the byte count validates. Decision 15 supersedes the direct metadata call with a single Vault descriptor route. The Burst solver now converts positive-solid signed distance into exosuit clearance, uses nearest sampling at low quality, blends to trilinear by `GlobalQualityWeight`, and blends from cheap radial normals to finite-difference SDF normals as quality rises. `MockTerrainSDF` remains only an emergency fallback.
Rejected Alternatives: Calling voxel MonoBehaviours from inside Burst was rejected. Allocating a SHINOBU-owned copy of the SDF was rejected because cave geometry is owned by the world/voxel system. MeshCollider, Physics.Raycast, and Rigidbody sweeps were rejected as non-deterministic scene physics.
Scalability potential: Low uses one nearest byte lookup and cheap normal approximation; middle blends in trilinear distance; high/ultra pays six extra finite-difference reads for contact normals and uses the saved PhysX budget for haptics/silt/acoustic presentation.
Hardware Impact: The low path is bounded to O(substeps) byte loads for one suit and avoids PhysX broadphase/contact solving. High quality adds SDF taps but remains cache-local and branch-light. Exact profiler numbers remain pending behind the CPU build gate.

## Decision 11: Subagent Audit Closure

Problem: Static audit found two authority faults and two polish faults: the Core facade could still write the Vault input row, SHINOBU_276 lanes had mixed `GameplayPlayer` and `Physics` owners, CSV polling could run in development fixed ticks, and optional readback could mutate `Transform.position`.
Solution: Core facade is now pending DTO only; `ExosuitKinematicsRuntime` is the single writer of `BufferID.ShinobuExosuitFrameInput` in its owner phase. Every SHINOBU_276 generation handle is requested with `SystemID.Physics`. CSV ingest runs once during cold Vault initialization, with periodic reload restricted to `UNITY_EDITOR`. Solver readback no longer drives scene transform state; gizmos and editor labels read Vault diagnostics only.
Rejected Alternatives: A write-window exception was rejected because it still split ownership. Mixed player/physics Vault handles were rejected because the input row is a physics-owned simulation fact. Development-build CSV polling was rejected because managed file IO in fixed tick can stall QA/player builds. Transform drive was rejected because it creates a second scene authority beside AUP state.
Scalability potential: Low, Middle, High, and Ultra all share the same owner route and DTO layout. Quality only scales SDF taps, substeps, CCD contribution, and presentation signal density; it never changes who owns the fact.
Hardware Impact: Static correctness gain. The route removes a potential fixed-tick file IO stall and prevents cache/ownership ambiguity around the 32-byte input row. Exact microsecond proof remains pending behind the CPU build gate.

## Decision 12: Span CSV And Seed Discipline

Problem: The cold CSV parser was allocation-free but still indexed a `NativeArray<byte>` directly and loaded with a per-byte `FileStream.ReadByte()` loop. The deterministic mock/procedural input route also mixed frame/quality/action but not explicit stable entity and sector hashes.
Solution: Load CSV bytes into the Vault scratch buffer through `Span<byte>` over the native pointer, then parse a zero-copy `ReadOnlySpan<byte>` slice into `ExosuitTuningDTO`. Add explicit `StableEntityHash` and `SectorHash` fields to the mock/procedural jobs; runtime seeds them with `ExoSourceHash` and an AUP-kilometer sector hash before scheduling the integrator.
Rejected Alternatives: Managed `byte[]`, `string.Split`, per-byte `ReadByte`, and frame-only random seeds were rejected. Widening DTOs for RNG material was rejected because RNG seed material is job parameter state, not rollback truth.
Scalability potential: CSV remains cold/editor-only and does not alter runtime fidelity. RNG determinism is identical across Low, Middle, High, and Ultra; quality still only changes solver fidelity and optional presentation density.
Hardware Impact: Cold boot parser avoids managed array allocation and reduces IO calls. External-authority runtime cost is unchanged because procedural RNG is disabled when player input is present.

## Decision 13: External SDF Fence And Scanner Truth

Problem: The owner-published voxel SDF route still had three acceptance faults: SHINOBU_276 could pass an external `VoxelSdfTexture3D` `NativeArray<byte>` into a scheduled Burst job without a DataVault read fence, the metadata accessor pruned `s_activePublishedVolumes` inside a `TryGet*` read, and minimum-quality SDF sampling still paid the trilinear 8-tap decode before lerping with weight zero. The editor scanner also emitted a green "purged" statement regardless of hit counts.
Solution: This pass first moved the metadata read to pure `HectonVoxelVolume.TryReadClosestPublishedSonarSdfPayload`, locked `BufferID.VoxelSdfTexture3D`, and made low-quality sampling skip trilinear taps. Decision 15 supersedes the direct metadata read with `VoxelSdfPayloadDescriptorDTO`; the pure `TryRead*` route remains available to other owner-local consumers, but SHINOBU runtime no longer imports `Hecton8.Caves`. `Exosuit_Physics_Inquisition` now reports pass/fail verdicts and separates guarded legacy `ApplyExosuit*` Rigidbody routes from unguarded forbidden hits.
Rejected Alternatives: Copying the voxel SDF into a SHINOBU-owned buffer was rejected because cave geometry belongs to the voxel/world owner and would create a second geometry fact. Continuing to use raw `HectonVoxelVolume` arrays was rejected because scheduled jobs need a stable buffer fence. A binary low-quality branch was rejected; the trilinear path is gated by the continuous smoothstep weight. Leaving the scanner green was rejected because proof artifacts must be falsifiable.
Scalability potential: Low uses one nearest byte SDF lookup and radial normal, Middle blends toward trilinear by a continuous curve, High/Ultra pay trilinear and finite-difference normals for smoother contact and spend saved PhysX budget on haptics/silt/acoustics. Authority route and DTO layout are identical at all quality weights.
Hardware Impact: Low-quality samples avoid eight decoded SDF loads per distance query when the smoothstep weight is zero. The lock adds owner-phase correctness cost only around the scheduled job window and prevents undefined relocation/dispose races on weak/mobile silicon.

## Decision 14: Padding Offset Proof Without Private Member Access

Problem: `ExosuitLayoutVerifier` tested `ExosuitStateDTO._pad0` with `nameof(ExosuitStateDTO._pad0)` from a separate static verifier class. The field is intentionally private padding; `nameof` on that private member can trip accessibility before layout proof runs.
Solution: Keep `_pad0` private and pass the literal field name `"_pad0"` into `Marshal.OffsetOf`. This preserves offset validation while keeping padding out of the DTO API.
Rejected Alternatives: Making `_pad0` public was rejected because padding is not a gameplay or editor contract. Deleting the padding offset test was rejected because ARM64 layout proof needs the final 64-byte boundary checked.
Scalability potential: No quality curve change. Low, Middle, High, and Ultra still share the same 64-byte rollback DTO; this only protects the verifier compile surface.
Hardware Impact: No frame-time claim. This removes a SHINOBU-owned compile-risk hidden behind the external `IBuildPlacementRule.cs` wall and keeps the ARM64 padding proof executable once the wall is cleared.

## Decision 15: Descriptor-Bound Voxel SDF Contract

Problem: The voxel SDF route still paired metadata from `HectonVoxelVolume.TryReadClosestPublishedSonarSdfPayload` with bytes from `BufferID.VoxelSdfTexture3D`. Length validation alone could pair current metadata with stale or wrong-volume bytes, and the Physics runtime imported concrete `Hecton8.Caves`.
Solution: Add `VoxelSdfPayloadDescriptorDTO` in `Hecton8.Core.Contracts` and `BufferID.VoxelSdfPayloadDescriptor`. The voxel owner holds the descriptor write lock while refreshing the byte SDF and then writes the 64-byte descriptor, preventing consumers from locking an old descriptor during a byte-buffer refresh. SHINOBU reads only the descriptor and locked byte buffer, verifies `BufferId`, `ByteCount`, `BufferGeneration`, flags, dimensions, range, and finite origin/cell data, then schedules Burst over that immutable pair. The concrete `Hecton8.Caves` import was removed from `ExosuitKinematicsRuntime`.
Rejected Alternatives: Keeping metadata and bytes on separate routes was rejected because it violates one fact -> one route. Copying voxel bytes into a SHINOBU-owned buffer was rejected because cave geometry belongs to the world/voxel owner. Calling HectonVoxelVolume from physics was rejected as cross-domain concrete coupling.
Scalability potential: Low/Middle/High/Ultra still scale only SDF taps, substeps, and presentation density. Descriptor validation does not alter gameplay truth, DTO layout, save identity, or quality curve.
Hardware Impact: No frame-time claim. This removes a stale-pair correctness hazard and a concrete-domain compile dependency while keeping the runtime hot path to one 64-byte descriptor read plus a locked byte-array alias.

## Decision 16: Scanner Dominance And Stale Report Repair

Problem: The editor inquisition used a file-level authority guard and append-only aggregate JSON. A single guard anywhere in `HectonPlayerMovement` could make unrelated force sinks look guarded, and stale scanner data could survive future runs.
Solution: Track `ExosuitKinematicAuthority.HasActiveAuthority` inside the current legacy method scope, classify indirect sinks such as `ApplyMotorForce`, `ApplyMotorVelocityChange`, and `ApplyClampedAccelerationForce`, and upsert the aggregate report node by replacing the existing JSON object. Add source hash and UTC ticks to make stale artifacts visible.
Rejected Alternatives: Roslyn control flow was rejected for this pass because the editor scanner is already source-text based and the method-scope dominance check catches the known legacy exosuit routes with less compile surface. Keeping append-only JSON was rejected because it preserves stale PASS/FAIL data.
Scalability potential: Editor-only proof path; runtime quality curve is unchanged.
Hardware Impact: Editor-only. It improves falsifiability, not frame time.

## Decision 17: SHINOBU Origin Boundary Trim

Problem: After the voxel descriptor fix, `ExosuitKinematicsRuntime` still carried a `using Hecton8.World` import and two direct `HectonFloatingOrigin.CurrentTotalOffsetDouble` reads. The import was unused, but it left a false sibling-domain edge in the proof surface and kept origin resolution spread across call sites.
Solution: Remove the World namespace import and route owner-phase origin reads through `ResolveRuntimeOriginAupDouble()`, which consumes `GlobalSignals.CurrentRuntimeOriginAup()` and converts the finite AUP snapshot to `double3`. The solver still receives one `MockTerrainSDF.CameraAup` snapshot before scheduling and performs all Burst collision math as `state.AUP_Position - terrain.CameraAup`.
Rejected Alternatives: Keeping the unused import was rejected because compile-wall scans should be falsifiable. Direct `HectonFloatingOrigin.CurrentTotalOffsetDouble` reads in SHINOBU runtime were rejected because the domain should expose one local origin read route. Adding a new origin DTO/Vault lane was rejected for this pass because no owner-published route exists for SHINOBU to own, and inventing one would create a shadow fact.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged; the quality curve still scales SDF taps and substeps only. The origin route change does not alter DTO layout, save identity, rollback truth, or authority ownership.
Hardware Impact: No measured frame-time claim. The practical gain is boundary hygiene and fewer direct registry-backed origin call sites in SHINOBU-owned runtime code.

## Decision 18: Descriptor Fence Critical Audit Closure

Problem: Parallel audit found four critical SDF route faults: `BufferID.VoxelSdfPayloadDescriptor` collided with `WristHudState` at value 560; SHINOBU read descriptor/SDF buffers with `TryGetBuffer`, which mutates external-view generation state; `TryAcquireWriteLock` ignored `TryLockBuffer` read locks; and the voxel descriptor published a captured runtime origin that could become stale after origin shifts. The budget path also wrote dumps for every >0.1 ms solver completion, creating avoidable disk IO on slow frames.
Solution: Move `VoxelSdfPayloadDescriptor` to free lane 620, keep `WristHudState` at 560, and document the new external ID. Patch `GlobalDataVault` so reader and writer fences are mutually exclusive: writer acquisition rejects active `BlockFlagLocked`/reader counts, and reader locks reject active writers with a post-lock recheck. SHINOBU now locks descriptor/SDF buffers, reads both through `VaultGenerationHandle<T>` plus `TryReadHandle`, checks `OwnerSystemId == WorldStreaming`, and validates SDF generation from the handle instead of `TryGetBufferGeneration`. `HectonVoxelVolume` rebases descriptor origin from captured runtime origin plus captured AUP offset to current runtime origin before publishing, clears the descriptor if origin proof fails, and records `sdfHandle.Generation` directly after the byte write instead of issuing a second generation query. The budget-overrun telemetry-only compromise in this decision is superseded by Decision 21, which restores the source-XML-required dump after the row is patched with elapsed time and `BudgetExceeded`.
Rejected Alternatives: Moving Wrist HUD IDs was rejected as another domain's ownership. Keeping `TryGetBuffer` was rejected because read accessors must not mutate global generation/external-view state. A SHINOBU-owned copy of the SDF was rejected because cave geometry has a single WorldStreaming owner. Leaving the telemetry-only budget path as current behavior was rejected in Decision 21 because Task 15 requires a dump on slow solver frames.
Scalability potential: Low uses the same descriptor fence and nearest SDF sampling, Middle/High/Ultra only spend additional SDF taps and substeps. The fix does not change DTO layout, authority ownership, save identity, or the continuous quality curve.
Hardware Impact: No profiler claim. The fence patch removes a real race/stale-origin hazard; budget diagnostics can perform disk IO on slow frames as required by Task 15, with `_lastDumpFrame` bounding duplicate same-frame writes.

## Decision 19: Read-Fence Unlock Symmetry

Problem: `TryLockBuffer` now resolves flat metadata before incrementing the block read count, but `TryUnlockBuffer` still resolved the legacy metadata map. If those two metadata surfaces diverged after compaction, tombstone, or a flat-only read path, a successful read lock could fail to unlock and leave `Reserved1` set on the descriptor or SDF byte block.
Solution: Route `TryUnlockBuffer` through `TryReadFlatMetadata`, matching the lock acquisition path. I also read `ReleaseWriteLock` semantics and confirmed it clears `ActiveWriterSystemID` without bumping `meta.Version`, so publishing `sdfHandle.Generation` into `VoxelSdfPayloadDescriptorDTO.BufferGeneration` is coherent.
Rejected Alternatives: Keeping map-based unlock was rejected because lock/unlock would not use one proof route. Bumping generation manually in the publisher was rejected because Vault write locks do not create a new generation and a synthetic value would force SHINOBU into analytic fallback.
Scalability potential: Low, Middle, High, and Ultra keep the same descriptor fence. Quality continues to scale only SDF taps, substeps, and presentation density.
Hardware Impact: No profiler claim. This removes a lock leak risk around the external SDF read window; frame cost is unchanged outside the owner-phase unlock.

## Decision 20: Standalone Job NaN Vaccination

Problem: The primary integration job had finite guards, but the standalone SDF collision, hydraulic dampening, magnetic clamp, and metabolism jobs could still receive NaN state/input/tuning values if a test harness or future dispatcher lane schedules them directly.
Solution: Add `ExosuitMathGuards` for Burst-safe finite sanitization and apply it to the standalone jobs before distance, pressure, clamp, and heat math. Re-scan confirmed every exosuit job still uses deterministic Burst compile flags.
Rejected Alternatives: Leaving helper jobs untreated was rejected because the mandate says every mathematical job must survive zero denominators and non-finite inputs. Moving all helpers into the primary integrator was rejected because these jobs are independent artifacts and need local protection.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged. The patch only clamps invalid inputs before existing quality curves.
Hardware Impact: No profiler claim. The extra scalar finite checks are only paid when these standalone jobs are scheduled; the primary one-job runtime route is unchanged.

## Decision 21: Budget Dump Reconciliation

Problem: The original SHINOBU_276 XML explicitly requires the 300-frame telemetry ring to dump when the solver exceeds 0.1 ms. The previous telemetry-only budget flag reduced IO risk but under-satisfied Task 15.
Solution: After finalizing the job, the runtime patches the last telemetry row with elapsed milliseconds and `BudgetExceeded`; if the threshold is breached, it calls `DumpTelemetryBuffer()`. The existing `_lastDumpFrame` guard prevents duplicate writes when the same frame is also faulted.
Rejected Alternatives: Keeping budget as telemetry-only was rejected because the source XML is the authoritative task contract. Calling dump before `PatchLastTelemetryElapsed` was rejected because the forensic row would miss the measured CPU time and budget flag.
Scalability potential: Low, Middle, High, and Ultra solver truth remains unchanged. The diagnostic dump is driven by measured budget breach, not quality tier.
Hardware Impact: Diagnostic IO can occur on slow frames as required by the task; duplicate same-frame writes are suppressed by `_lastDumpFrame`.

## Decision 22: DTO Padding API And Dump Guard Timing

Problem: A proof pass exposed two small but real acceptance issues. `ExosuitStateDTO._pad0` had briefly become public, turning padding into API surface. `DumpTelemetryBuffer()` also armed `_lastDumpFrame` before resolving the telemetry ring and cursor, so a transient resolve failure could suppress a same-frame fault/budget dump retry.
Solution: Keep `_pad0` private and retain offset proof through `Marshal.OffsetOf("_pad0")`. Move `_lastDumpFrame = frame` until after telemetry/cursor buffers resolve successfully, preserving duplicate suppression only after dump data exists.
Rejected Alternatives: Public padding was rejected because padding is not gameplay truth, editor tuning, or a cross-domain contract. Arming the dump guard before data resolution was rejected because the black-box requirement needs the best available same-frame forensic attempt.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged; the fix does not alter DTO size, authority route, save identity, or quality curve.
Hardware Impact: No frame-time claim. This is API hygiene plus forensic reliability on exceptional paths.

## Decision 23: External SDF Read-Lock Release Before Diagnostic IO

Problem: After job finalization, budget and fault dumps could write files before `UnlockJobBuffers()` released the external `VoxelSdfPayloadDescriptor` and `VoxelSdfTexture3D` read locks. A slow diagnostic write should not extend the world SDF writer exclusion window.
Solution: Call `UnlockVoxelSdfPayloadBuffers()` immediately after `PatchLastTelemetryElapsed()` and before any budget dump or readback signal emission. SHINOBU-owned job buffers remain locked until telemetry/output readback finishes, then `UnlockJobBuffers()` releases the remaining local lanes.
Rejected Alternatives: Holding all locks until after file IO was rejected because external world data should not wait on SHINOBU forensic disk writes. Unlocking every SHINOBU lane before output/signal readback was rejected because those local rows are still being read for diagnostics in the same owner phase.
Scalability potential: Low, Middle, High, and Ultra solver behavior is unchanged. This only shortens the external SDF read-lock window after completed jobs.
Hardware Impact: No profiler claim. On weak storage or slow diagnostic frames, the world SDF writer is no longer blocked behind SHINOBU dump IO.

## Decision 24: Pure Vault Read Route For Runtime Views

Problem: The SHINOBU runtime helper `TryResolveBuffer<T>` used `IDataVault.TryResolveHandle`, which records generation faults on failures and increments resolve counters in collection-check builds. That is useful for diagnostics, but it means a read-shaped owner-phase helper can mutate Vault diagnostic state.
Solution: Route `TryResolveBuffer<T>` and public editor-facing `TryReadTuning` through `IDataVault.TryReadHandle`, matching the external SDF descriptor/byte route. The helper still validates handle generation, owner/type data in checks, created state, and non-empty length before exposing the `NativeArray`.
Rejected Alternatives: Keeping `TryResolveHandle` was rejected because the doctrine says read accessors must be pure. Adding a second write-specific resolve helper was rejected because SHINOBU already acquires locks before job views and does not need a mutating fault recorder for normal owner-phase or editor-facade buffer reads.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged; the route change does not alter DTO layout, authority ownership, quality curve, or save identity.
Hardware Impact: No measured frame claim. In collection-check/editor builds this avoids diagnostic counter writes on routine SHINOBU buffer views; player hot path remains unmanaged and allocation-free.

## Decision 25: Player Bridge Residual Rigidbody Mutation Gate

Problem: After grapple, jump, and gravity guards, `HectonPlayerMovement` still executed generic motor/Rigidbody mutators after successful exosuit authority submission: environment handler motor flush, queued external kinematic velocity, high-speed KCC sweep scheduling, wall-scrape feedback, wipeout recovery force, procedural damping, velocity clamp, and exosuit support foot ray probes. Those paths could still fight the unmanaged `ExosuitStateDTO` truth row.
Solution: Drive the player bridge with `exosuitKinematicAuthority` for the full post-submit phase. `_environmentHandler.ExecuteStep`, `ApplyQueuedExternalKinematicForces`, and `ApplyHighSpeedWipeoutSweep` now take an `applyToMotor` gate; when the exosuit runtime owns movement, environment force/stress evaluation still consumes buffers and records stress, but direct motor acceleration/velocity writes are suppressed. KCC wall scrape feedback, wipeout recovery, procedural damping, and velocity clamps only run under `!exosuitKinematicAuthority`. GroundCheck also disables legacy exosuit foot slope probes under active authority, leaving contact truth to the byte-SDF integrator. The same touched player surface had three cinematic-focus blackbox read helpers on `TryResolveHandle`; those are now `TryReadHandle` so read-shaped Vault access stays pure.
Rejected Alternatives: Deleting shared Rigidbody/KCC code was rejected because non-exosuit player movement still owns those routes. Skipping the entire environment handler was rejected because hull-stress and presentation signals are not movement authority and should keep draining their buffers. Leaving generic clamps active was rejected because they can mutate `_rb.linearVelocity` after the Vault authority row has been submitted.
Scalability potential: Low uses no legacy foot ray probes or KCC sweeps in exosuit authority frames and relies on 2-step byte-SDF contact. Middle/High/Ultra spend extra contact taps inside the Burst integrator only; the player bridge stays a stable unmanaged intent route at every `GlobalQualityWeight`.
Hardware Impact: Removes two exosuit foot support probes per grounded authority frame, suppresses KCC sweep scheduling, and blocks generic motor velocity clamps/damping from the heavy suit path. No measured microsecond claim until the external `IBuildPlacementRule.cs` compile wall is cleared.

## Decision 26: Tuning Write Facade Fence

Problem: After making SHINOBU runtime reads pure, the editor-facing `TryWriteTuning` path still needed an explicit mutable Vault route. A tuning writer that exposes a writable `NativeArray` through read-shaped handle access blurs the doctrine boundary and can race future read locks around the solver tuning row.
Solution: Route `TryWriteTuning` through `IDataVault.TryAcquireWriteLock(in handle, SystemID.Physics, out NativeArray<ExosuitTuningDTO>)`, write only sanitized tuning into index zero, and release through `ReleaseWriteLock` in `finally`. `TryReadTuning` remains on `TryReadHandle`, so read and write facades now use separate proof routes. Architecture, binary payload ledger, and self-audit files were updated to match the code route.
Rejected Alternatives: Keeping direct writes through a read view was rejected because read accessors must be pure. Caching a private tuning copy was rejected because tuning truth belongs to the Vault row and would create a shadow state. Changing `ExosuitTuningDTO` layout was rejected because this is a route-fence defect, not a payload-shape issue.
Scalability potential: Low, Middle, High, and Ultra all consume the same tuning row; only `GlobalQualityWeight`, SDF epsilon, gravity multiplier, and max substeps modulate fidelity. The writer-lock fence does not change gameplay truth, DTO layout, or authority ownership.
Hardware Impact: No measured frame claim. The lock is editor/cold tuning-path work and keeps the Burst solver from observing partially written tuning fields on weak/mobile silicon.

## Decision 27: Public Read Owner Fence

Problem: The public editor-facing read facades resolved SHINOBU BufferIDs through `TryGetGenerationHandle` and pure `TryReadHandle`, but the local helper did not explicitly reject a same-ID row owned by a non-Physics system outside collection-check builds.
Solution: Add `handle.SystemID == (uint)SystemID.Physics` before `TryReadHandle` in `TryReadExistingBuffer<T>`. This makes `TryReadState`, `TryReadScreen`, `TryReadLastTelemetry`, and `TryReadTuning` fail closed unless the DataVault row is owned by the SHINOBU physics lane.
Rejected Alternatives: Relying on `TryReadHandle` owner validation was rejected because that guard is collection-check scoped. Adding a second public owner parameter was rejected because SHINOBU public readers always target Physics-owned rows and should not expose ownership choice to editor tools.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged; this is route proof only and does not alter DTO layout, save identity, quality curve, or authority ownership.
Hardware Impact: One integer compare per editor/readback access. No fixed-tick solver cost and no managed allocation.

## Decision 28: CSV Scratch And Tuning Writer Fence

Problem: The cold/editor CSV ingestion path still wrote the native scratch buffer and tuning row through `TryResolveBuffer<T>`, which now intentionally uses `TryReadHandle`. Even though the path is cold/editor and owner-phase, it violated the read/write route split established for the public tuning facade.
Solution: Route `TryApplyCsvOverrides` through `TryAcquireWriteLock` for `ShinobuExosuitCsvScratch` while reading file bytes into the native scratch lane, then acquire `TryAcquireWriteLock` for `ShinobuExosuitTuning` before parsing and committing a sanitized tuning DTO. Both writer fences release in `finally`; `_lastCsvWriteTicks` advances only after an invalid file is consumed or a parse/commit attempt completes under the fences.
Rejected Alternatives: Keeping owner-phase unfenced writes was rejected because it depends on timing instead of route proof. Copying the CSV into a managed byte array was rejected because the Task 17 bridge requires native scratch/`ReadOnlySpan<byte>` parsing. Holding a private tuning cache was rejected because tuning truth belongs to the Vault row.
Scalability potential: Low, Middle, High, and Ultra still consume one tuning DTO and scale only through `GlobalQualityWeight`, SDF epsilon, gravity multiplier, and max substeps. The fence does not change gameplay truth, DTO layout, save identity, or authority owner.
Hardware Impact: No fixed-tick solver cost. The extra writer locks are cold boot/editor reload work and prevent a weak device from observing a partially written tuning row if tooling changes occur near a solver read window.

## Decision 29: Local Handle Owner Fence

Problem: `GlobalDataVault.TryAcquireWriteLock` validates generation and active readers/writers but does not reject a `VaultGenerationHandle<T>` whose `SystemID` is not the SHINOBU Physics owner. Public read facades had an owner guard, but local buffer views and tuning writer entry still treated `BufferID != 0` as enough proof.
Solution: Strengthen `IsHandleCreated<T>` to require `handle.SystemID == (uint)SystemID.Physics`. `TryResolveBuffer<T>` now fails closed on non-Physics local handles, `TryWriteTuning` checks the fetched generation handle before writer-lock acquisition, and CSV ingestion checks both scratch and tuning handles before taking writer locks.
Rejected Alternatives: Patching Core writer-lock semantics globally was rejected because this task is scoped to SHINOBU_276 and Core lock behavior may be relied on by other owners. Keeping BufferID-only checks was rejected because BufferID collisions have already occurred in this project and owner is part of the proof route.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged. The owner fence does not alter tuning values, quality curves, DTO layout, save identity, or authority ownership.
Hardware Impact: One integer compare per local SHINOBU buffer view/write gate. No allocation, no fixed-tick solver math cost beyond branch-local validation.

## Decision 30: Owner-Phase Write Fence

Problem: After the CSV writer fence, two SHINOBU owner-phase paths still wrote through private `TryResolveBuffer<T>` views: cold emergency seed data and per-fixed-tick frame input/tuning/terrain staging. They were not job hot loops, but they were still mutable fact writes without explicit writer ownership.
Solution: Add `TryAcquireWriteBuffer<T>` and `ReleaseWriteBuffer<T>` wrappers that require a Physics-owned local generation handle, acquire `TryAcquireWriteLock`, reject empty arrays, and release failed acquisitions. `GenerateEmergencyMockExoData` now seeds state, tuning, input, fallback terrain/flow/crush, output, screen, haptic/silt/acoustic rows, telemetry cursor, and footstep accumulator under writer fences. `WriteFrameInputs` now sanitizes tuning, consumes pending Core input, and writes the input/terrain/flow/crush staging rows under writer fences before the solver job locks them for Burst.
Rejected Alternatives: Patching Core writer-lock owner validation globally was rejected as out of scope and risky for other agents. Reusing the old job lock route for cold/frame writes was rejected because it would blur job admission fences with owner mutation fences. Taking a second writer lock inside `PatchLastTelemetryElapsed` was rejected because the completed job window still holds the scheduled job locks until readback is patched and external locks are released; Decision 33 supersedes the old route by acquiring writer locks before scheduling the job.
Scalability potential: Low, Middle, High, and Ultra solver behavior is unchanged; `GlobalQualityWeight` still controls only fidelity/cadence/presentation, not fact ownership. The fence only changes mutation admission around owner-phase staging.
Hardware Impact: Cold boot pays multiple writer-lock checks once. Fixed tick pays five writer-lock checks before scheduling one suit solver; Burst math remains unchanged and the cost is bounded outside the vectorized integration kernel.

## Decision 31: Core Facade Owner Fence

Problem: `Hecton8.Core.ExosuitKinematicAuthority` had become a pending DTO bridge, but `Bind`, `HasActiveAuthority`, and `Unbind` still treated `BufferID != 0` as enough proof. A stale or foreign-owned input handle could mark authority active even though SHINOBU runtime only accepts `SystemID.Physics` lanes.
Solution: Add `VaultGenerationHandle.SystemID == SystemID.Physics` checks to facade bind and authority state, and require the same owner when unbinding. Invalid binds now clear the cached handle and pending DTO so stale intent cannot survive a failed owner proof. The Core facade still stores only `ExosuitFrameInputDTO` as pending unmanaged intent; it does not write the Vault and does not query Vault during player hot input submission.
Rejected Alternatives: Trusting the current Physics runtime caller was rejected because route proof should be local at the facade boundary. Making the facade perform a Vault read or write for validation was rejected because the pending bridge must stay allocation-free and avoid hot `GlobalRegistry`/Vault polling from the player path.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged. Quality still only changes solver math and presentation cadence; the owner fence cannot alter gameplay truth, DTO layout, or authority route.
Hardware Impact: One integer compare at bind/authority/unbind boundaries. No Burst job cost, no managed allocation, no fixed-tick Vault access from the player bridge.

## Decision 32: Unity Meta Identity For New Core Surfaces

Problem: The new Core facade and exosuit frame-input contract source files were untracked Unity assets without `.meta` files. Unity would generate GUIDs on import, creating nondeterministic asset identity and noisy concurrent-agent churn.
Solution: Add stable `MonoImporter` `.meta` files for `Assets/_Project/Scripts/Core/ExosuitKinematicAuthority.cs` and `Assets/_Project/Scripts/Core/Contracts/ExosuitKinematicsContracts.cs`. Existing SHINOBU `GroundRadarContracts.cs` and `Exosuit_Physics_Inquisition.cs` metas remain unchanged.
Rejected Alternatives: Letting Unity import generate metas was rejected because it creates uncontrolled GUIDs. Editing generated `.csproj` files was rejected because those are ignored/generated and must be refreshed by Unity import, not by hand.
Scalability potential: No runtime quality effect. This protects editor/import determinism only.
Hardware Impact: Editor/import hygiene only; no frame-time or Burst solver cost.

## Decision 33: Scheduled Job Writer Fence

Problem: A read-only audit found that the scheduled integration job still acquired SHINOBU mutable lanes through a relocation/read-style `TryLockBuffer` followed by `TryResolveBuffer`/`TryReadHandle`. The Burst job mutates State, Tuning, Output, Screen, Telemetry, Cursor, Footstep, Haptic, Silt, and Acoustic rows, so a generic scheduled-buffer fence was not enough proof of writer ownership.
Solution: Replace the old `TryLockJobBuffers`/`BindJobBufferViews` route with `TryAcquireJobBufferViews`. Mutable job lanes now use `TryAcquireJobWriteBuffer`, which validates the expected BufferID, requires a Physics-owned handle, acquires `TryAcquireWriteLock`, and passes the returned `NativeArray<T>` directly into the job. Read-only terrain, flow, and crush-depth lanes use `TryAcquireJobReadBuffer` and a read relocation fence. External voxel descriptor/SDF byte lanes remain read-locked and are released before diagnostic file IO.
Rejected Alternatives: Keeping read locks and relying on owner-phase timing was rejected because a scheduled writer needs a writer fence. Copying the Vault rows into private NativeArrays was rejected because SHINOBU must not own shadow persistent memory. Patching Core lock semantics globally was rejected because the local route can prove ownership without widening cross-agent blast radius.
Scalability potential: Low, Middle, High, and Ultra math curves are unchanged. `GlobalQualityWeight` still scales substeps, SDF taps, normals, CCD, and presentation signals only; it does not alter fact ownership, DTO layout, or the writer/read fence contract.
Hardware Impact: No profiler claim. This closes a race/ownership defect around scheduled native writes; Burst math cost is unchanged and the extra lock checks happen at job admission, not per SDF sample.

## Decision 34: Dedicated Static Scanner JSON Truth

Problem: The self-audit previously implied the shared optimization report already contained a SHINOBU scanner node. The editor menu that writes that aggregate node was not executed in this CLI pass, so the proof artifact claim was stronger than the evidence.
Solution: Add `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_276.json` as the dedicated CLI proof artifact for this pass. It records the static route verdict, the job writer-lock fence, the external compile wall, and that the aggregate scanner node remains pending Unity editor menu execution.
Rejected Alternatives: Manually injecting a synthetic aggregate node was rejected because it would pretend the editor scanner ran. Leaving only the shared JSON was rejected because Copernicus correctly identified the missing dedicated proof surface.
Scalability potential: No runtime quality change. The proof artifact documents the same continuous quality route across weak, middle, high, and ultra devices.
Hardware Impact: Documentation/proof only. No frame-time claim and no Unity import/profiler proof implied.

## Decision 35: Core Pending Intent Rebind Fence

Problem: The Core pending bridge cleared stale data on invalid bind and unbind, but a valid rebind could preserve a pending DTO and sequence from the previous input handle. `TryConsumePendingFrameInput` and `TrySubmitFrameInput` also relied on local pending/bound flags instead of rechecking the full Physics-owned authority proof.
Solution: Clear pending DTO, pending sequence, and pending flag on every valid bind transition; reset sequence on invalid bind and unbind; gate both submit and consume through `HasActiveAuthority()`. The facade remains a pending unmanaged DTO bridge and still performs no Vault read/write.
Rejected Alternatives: Preserving queued input across rebind was rejected because a handle transition is an authority boundary, not a gameplay continuity guarantee. Adding Vault validation in the facade was rejected because player hot input submission must stay allocation-free and must not poll global state.
Scalability potential: Low, Middle, High, and Ultra behavior is unchanged. `GlobalQualityWeight` still scales only solver fidelity and presentation cadence; pending intent authority does not affect DTO layout, save identity, or fact ownership.
Hardware Impact: One owner-proof branch per submit/consume boundary and no Burst/job cost. No measured frame claim.

## Decision 36: Live Global Quality Route

Problem: Avicenna found that `GlobalQualityWeight` was executable fiction: runtime frame input, tuning sanitizers, and Burst jobs forced quality to `1f`, so low and middle quality paths could not run while the docs claimed continuous scalability.
Solution: Replace the hardwired exosuit quality constant with `DefaultQualityWeight` only as invalid-data fallback. Runtime now stages each frame with `min(HomeostasisBrain.GlobalQualityWeight, ExosuitTuningDTO.GlobalQualityWeight)`, and Burst jobs resolve quality from input/tuning with the same cap. Standalone SDF jobs use their explicit `GlobalQualityWeight` parameter capped by tuning.
Rejected Alternatives: Reading `HomeostasisBrain` from Burst was rejected because jobs must stay pure unmanaged kernels. Treating editor tuning quality as the sole source was rejected because the project-wide thermal dictator must be able to reduce fidelity continuously at runtime. Binary low/high switches were rejected by doctrine.
Scalability potential: Low now reaches the nearest-SDF, widened-epsilon, reduced-substep path; Middle blends toward trilinear and extra probes; High and Ultra consume the same DTOs but pay for finite-difference normals and max substeps. Quality changes do not alter ownership, DTO layout, save identity, or rollback route.
Hardware Impact: Static route fix. On weak devices, reachable low quality avoids trilinear and finite-difference SDF taps and trends to two substeps. Profiler proof remains blocked by the external missing-source compile wall.

## Decision 37: Atomic Scanner Aggregate Upsert

Problem: The editor inquisition updated the shared `PHYSICS_OPTIMIZATION_REPORT.json` with unlocked read/modify/write and direct `File.WriteAllText`, so concurrent agent scanners could clobber each other's nodes or leave stale proof.
Solution: Add a lock-file guarded aggregate read/modify/write path and temp-file atomic replace. The dedicated SHINOBU report write also uses the atomic helper, while the aggregate merge holds the lock for both read and write.
Rejected Alternatives: Leaving only the dedicated JSON was rejected because the shared aggregate remains an editor scanner contract. Locking only the final write was rejected because it still allows stale read/modify/write clobber.
Scalability potential: No runtime quality effect. This improves proof reliability under the 20+ agent concurrency model.
Hardware Impact: Editor-only file IO. No frame-time or Burst solver cost.

## Decision 38: Quality Sanitizer Select Order

Problem: A read-only audit found the live quality route could still be destroyed if `SanitizeQualityWeight` selected fallback when the input value was finite. That would make low and middle quality unreachable while the runtime and docs claimed continuous `GlobalQualityWeight`.
Solution: Prove the sanitizer route preserves finite input values and falls back only for non-finite values. The active source uses `math.select(safeFallback, value, math.isfinite(value))`; scalar CLI proof verifies `0`, `0.25`, `0.62`, `1`, and `NaN`.
Rejected Alternatives: Leaving a fallback-first route was rejected because it pins fidelity. Replacing it with hardware-tier branches was rejected because HECTON quality must stay continuous and cannot alter authority or DTO layout.
Scalability potential: Low devices can now reach nearest-only SDF, widened epsilon, and reduced substeps; middle devices blend toward trilinear and secondary probes; high/ultra spend the same state layout on tighter SDF and richer presentation.
Hardware Impact: No measured profiler claim. Static path proof restores the low-quality branch that avoids high-tap voxel SDF work on weak devices.

## Decision 39: Telemetry Elapsed Patch Write Gate

Problem: `PatchLastTelemetryElapsed` mutates the latest telemetry row after job completion, but a generic `TryResolveBuffer` call looked like a read-shaped access path even though the scheduled job writer locks are still held.
Solution: Gate this mutation through `TryOpenHeldJobWriteBuffer`, which requires `_jobBuffersLocked`, a Physics-owned handle, exact telemetry/cursor BufferIDs, and a valid non-empty view before the elapsed time and budget flag are patched.
Rejected Alternatives: Taking a second writer lock inside the completed job window was rejected because the first scheduled writer lock is intentionally still held. Leaving the generic helper was rejected because it weakens the proof boundary for a mutation.
Scalability potential: No quality behavior changes. The telemetry proof remains identical for Low, Middle, High, and Ultra; only the mutation gate is clearer.
Hardware Impact: One bounded branch path during completed job readback. No Burst solver math cost and no frame-time claim.

## Decision 40: Live Unguarded Legacy Scope Counter

Problem: The editor scanner emitted `unguardedLegacyMovementHits` in its JSON verdict but never incremented it, so a legacy `ApplyExosuit*` method without `ExosuitKinematicAuthority.HasActiveAuthority` could appear clean unless a direct Rigidbody line was separately classified.
Solution: Track legacy method start line/source, detect scope closure through `UpdateLegacyScope`, increment `unguardedLegacyMovementHits` when the scope closes without the authority guard, and fail closed for unterminated unguarded scopes.
Rejected Alternatives: Counting every legacy method reference as a hard failure was rejected because guarded legacy code is explicitly warning-only while the kinematic authority exists. Ignoring method-level guard absence was rejected because it leaves scanner verdicts weaker than the policy text.
Scalability potential: Editor-only scanner. It protects the movement authority route that lets quality scale SDF work instead of letting legacy PhysX routes fight the solver.
Hardware Impact: Editor-only string scan. No runtime frame cost.

## Decision 41: Borrowed Voxel SDF Generation Owner Fence

Problem: The external SDF acquire route validated `descriptor.OwnerSystemId == WorldStreaming`, but it did not prove that the descriptor or byte SDF generation handles themselves were WorldStreaming-owned. A stale or foreign-owned same-BufferID handle could expose data if the payload row claimed the right owner.
Solution: Require the descriptor handle to prove exact `BufferID.VoxelSdfPayloadDescriptor` and `SystemID.WorldStreaming`, and require the byte SDF handle to prove exact `BufferID.VoxelSdfTexture3D`, `SystemID.WorldStreaming`, nonzero generation, and equality to `descriptor.BufferGeneration` before the SDF array reaches the scheduled Burst job.
Rejected Alternatives: Trusting payload fields alone was rejected because owner proof must be attached to the route, not only to mutable data. Acquiring SHINOBU ownership of the SDF payload was rejected because WorldStreaming remains the single owner and SHINOBU is a read-only consumer.
Scalability potential: Low, Middle, High, and Ultra quality math is unchanged. The low nearest-SDF path and high trilinear/finite-difference path consume the same borrowed payload after stronger owner proof.
Hardware Impact: Admission-only integer compares. No per-sample SDF cost and no managed allocation.

## Decision 42: Player Collider And Center-Of-Mass Authority Gate

Problem: `HectonPlayerMovement` computed `exosuitKinematicAuthority`, but `UpdateDynamicCollisionProfile` still wrote `CapsuleCollider.radius/height/center` and `UpdateHeavyTowRuntimeResponse` still wrote `Rigidbody.centerOfMass` in the same active-authority fixed tick. That violated the one-authority route even though force, KCC, damping, clamp, and foot probes were already gated.
Solution: Pass `exosuitKinematicAuthority` into both methods. Dynamic collision continues to update blend/timer state but returns before `ApplyResolvedCollisionProfile` while authority is active. Heavy tow continues presentation camera pitch/roll/offset blending but skips `ApplyCenterOfMassIfChanged` while authority is active.
Rejected Alternatives: Disabling the whole methods was rejected because camera/presentation blending is not movement truth and should not pop. Letting Rigidbody/collider writes remain as "small corrections" was rejected because small Unity physics mutations still create a second movement authority beside the Vault SDF solver.
Scalability potential: Low through Ultra movement truth stays identical. Quality remains a continuous SDF solver/presentation scalar; this gate prevents unrelated player physics code from changing the authority route at any quality.
Hardware Impact: One branch per method in the player fixed path. It removes active-authority collider/center-of-mass mutation work and avoids downstream PhysX broadphase/center-of-mass churn; profiler proof remains pending.

## Decision 43: Authority Mutation Scanner Route

Problem: The editor scanner could catch unguarded legacy `ApplyExosuit*` scopes, but it did not detect dynamic collision and heavy-tow mutation routes that write `CapsuleCollider`/`Rigidbody` state while exosuit authority is active.
Solution: Add authority mutation route tracking for `UpdateDynamicCollisionProfile` and `UpdateHeavyTowRuntimeResponse` call/scope evidence. Unguarded call lines or method scopes with physics mutation sinks now fail the scanner; guarded routes are counted separately.
Rejected Alternatives: Relying on this manual audit was rejected because the proof artifact must catch regressions. Scanning every collider/Rigidbody use globally as a hard failure was rejected because non-exosuit and lifecycle routes exist outside SHINOBU authority.
Scalability potential: Editor-only. It protects the continuous-quality SDF route by preventing silent reintroduction of Unity physics mutation during kinematic authority.
Hardware Impact: Editor-only string scan. No runtime cost.

## Decision 44: Read-Shaped Mutation Name Closure

Problem: Two SHINOBU runtime helpers and one adjacent player bridge helper still advertised mutation through read-shaped names. `TryResolveVoxelSdfPayload` takes Vault read locks and mutates external SDF lock flags, `TryResolveHeldJobWriteBuffer` exposes writer-locked rows for telemetry mutation, and `ResolveHeavyTowWinchRuntime` lazily fills a cached component reference with `TryGetComponent`.
Solution: Rename them to `TryAcquireVoxelSdfPayload`, `TryOpenHeldJobWriteBuffer`, and `EnsureHeavyTowWinchRuntime`. Add `RefreshHeavyTowActive()` for the player diagnostics path so one cache refresh/read feeds all heavy-tow debug fields in a block.
Rejected Alternatives: Keeping old names was rejected because project doctrine treats read accessors as pure. Taking a second writer lock for telemetry elapsed patching was rejected because the scheduled job writer locks are intentionally still held until readback/dump patching finishes. Removing heavy-tow presentation refresh was rejected because tow camera response is non-authoritative presentation and remains valid outside SHINOBU movement truth.
Scalability potential: Low, Middle, High, and Ultra movement truth is unchanged. The change protects the proof boundary: quality still scales SDF taps/substeps/presentation, not ownership or DTO identity.
Hardware Impact: Runtime solver cost unchanged. Diagnostics now avoid repeated heavy-tow active helper calls in the same block; exact gain is not claimed without profiler proof.

## Decision 45: Player Authority Leak Closure

Problem: Halley found four remaining player-side mutation leaks beside SHINOBU authority. Wall kick queued a Rigidbody velocity change and wrote motor velocity after authority was computed. Voxel no-clip recovery moved the motor and zeroed velocity unconditionally. Transport carrier motion and ladder snap could write motor position, velocity, or rotation before the later `TrySubmitExosuitKinematicAuthority` call established the fixed-tick authority flag.
Solution: Compute an early `exosuitActive && ExosuitKinematicAuthority.HasActiveAuthority()` gate before carrier and ladder mutation, pass that suppression flag into carrier and ladder routes, and keep the later `TrySubmitExosuitKinematicAuthority` call as the frame-input submit boundary. Carrier suppression consumes the platform delta bookkeeping without moving the player. Wall kick now receives the active authority flag and returns before motor/Rigidbody mutation. Voxel no-clip was renamed to `ApplyVoxelNoClipFailsafe` and preserves black-box dumps while skipping recovery motor writes under authority.
Rejected Alternatives: Treating no-clip or wall-kick as emergency exceptions was rejected because they still create a second movement authority. Moving the frame-input submit to the very top of fixed tick was rejected because it would change the current input staging order more than needed. Skipping carrier bookkeeping was rejected because accumulated platform deltas would create a later positional spike.
Scalability potential: Low, Middle, High, and Ultra exosuit truth remains the same byte-SDF kinematic route. These gates stop quality-independent Unity motor corrections from bypassing the solver.
Hardware Impact: Active exosuit frames no longer perform wall-kick motor/force writes, no-clip recovery motor writes, carrier motor writes, or ladder snap motor writes. Exact microseconds require profiler; branch cost is one boolean gate per route.
