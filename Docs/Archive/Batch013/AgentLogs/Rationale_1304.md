# Rationale 1304 - MEMORY_SOVEREIGN_WORLD_VOXEL_EXORCIST

Date: 2026-05-25
Domain: Assets/Project/Scripts/World/Voxel
Status: APEX_TARGET_ALIAS_ZERO_EXTERNAL_COMPILE_BLOCKED

## Decision 001 - Static AST Before Mutation

Problem: The assignment targets persistent native aliases, but raw text search cannot distinguish field declarations from local method-scoped `NativeArray<T>` views.
Solution: Use a Roslyn AST scanner constrained to `Assets/Project/Scripts/World/Voxel` to produce a JSON hit list with file, class/struct, field, type, and line.
Rejected Alternatives: Regex-only scan. It misses declaration context and can either under-report toxic fields or over-report safe locals.
Scalability potential: Low uses no Unity load; Middle/High/Ultra can reuse the scanner as a gate before runtime proof.
Hardware Impact: Static scan cost is offline only; expected runtime gain is preventing stale unmanaged pointers that can stall or crash i3/MX350 during voxel deformation.

## Decision 002 - No Runtime Claim Without Proof

Problem: The mandate demands Zero-GC and memory safety, but this phase starts without Unity profiler, GCMonitor, or Play Mode evidence.
Solution: Mark runtime and GC status as PENDING VERIFICATION until compile/static gates and runtime artifacts exist.
Rejected Alternatives: Claiming 0 B/frame from code review. That is not accepted by AGENTS.md.
Scalability potential: Low/Middle/High/Ultra claims remain separated from measured proof.
Hardware Impact: Prevents false optimization reports that hide regressions on low-end silicon.

## Decision 003 - Batch Path Drift To Actual First-Party Voxel Scope

Problem: The prompt declares `Assets/Project/Scripts/World/Voxel`, but that path is absent. The repository authority in AGENTS.md declares first-party code under `Assets/_Project`, and actual voxel terrain sources are distributed under `Assets/_Project/Scripts/World/*Voxel*`, `Assets/_Project/Scripts/World/*Sdf*`, and root-level first-party voxel scripts such as `HectonVoxelVolume.cs`.
Solution: Use `Assets/_Project` as the corrected first-party root and restrict analysis to voxel/SDF terrain files. This is a critical boundary correction, not a domain expansion.
Rejected Alternatives: Creating the missing path, scanning third-party A* voxel utilities, or ignoring the live first-party voxel engine. All three produce false proof.
Scalability potential: Low/Middle/High/Ultra paths all rely on the same owner map; fixing the live path is mandatory before runtime tuning.
Hardware Impact: Prevents stale persistent voxel native buffers from remaining in the actual runtime path on i3/MX350.

## Decision 004 - Use Existing Roslyn Audit Executable

Problem: Direct PowerShell-hosted Roslyn loading failed earlier because `Microsoft.CodeAnalysis` dependency versions conflicted with available `System.Runtime.CompilerServices.Unsafe` assemblies. A new scanner build would violate the build guard while other dotnet processes are active.
Solution: Use the already-built `Tools/VaultNativeAliasRoslynAudit/bin/Debug/net10.0/VaultNativeAliasRoslynAudit.exe` without compiling new code. It produced 0 parse failures.
Rejected Alternatives: Hand-authored regex-only hit list, new Add-Type scanner compilation, or forcing the net8 executable despite missing runtime.
Scalability potential: Low/Middle/High/Ultra all benefit from a repeatable static gate. The scanner is offline and costs no frame time.
Hardware Impact: Prevents false-negative native alias retention that would crash low-end devices during vault compaction; no runtime cost.

## Decision 005 - No Phase 1 C# Mutation Before Route Map

Problem: The hit list crosses sonar SDF publishing, delta compaction, marching cubes scratch, and GPU upload staging. Blindly replacing fields with handles would alter public read models and async generation lifecycle.
Solution: Stop Phase 0 at proof artifacts and ownership map. Phase 1 must start with `HectonVoxelVolume` sonar SDF because it has an existing vault descriptor path and the smallest public blast radius.
Rejected Alternatives: Migrating all `HectonVoxelEngine` scratch arrays in one pass, changing `Hecton8.Core.Contracts` signatures, or deleting local buffers without an audio-material vault route.
Scalability potential: Low uses vault-backed SDF and fail-closed read access; Middle uses same route with normal cadence; High/Ultra spend saved CPU on denser visible meshing and richer sonar material response, driven by `GlobalQualityWeight`.
Hardware Impact: Limits regression risk on i3/MX350 by attacking one owner route at a time instead of destabilizing async cave generation.

## Decision 006 - Treat VoxelSurfaceNetsVaultBuffers As Phase-Local Only

Problem: Roslyn flags `VoxelSurfaceNetsVaultBuffers` as 18 forbidden native fields, but the type is intended as a resolved view aggregate returned from `TryResolveViews`.
Solution: Do not mark the aggregate as a persistent owner by itself; mark it forbidden only if stored across frames or outside method/job scheduling scope. Keep the field list in the ledger because the type can become toxic if cached.
Rejected Alternatives: Removing the aggregate immediately, or suppressing it entirely from the report.
Scalability potential: Low/Middle/High/Ultra preserve the same vault handle model; only view lifetime discipline changes.
Hardware Impact: Prevents stale relocated views without forcing extra per-field resolution overhead in the same scheduling phase.

## Decision 007 - Compile Gate Deferred By Active Dotnet Processes

Problem: AGENTS.md forbids launching dotnet build while another dotnet/csc process is running. Current process scan showed active dotnet processes after the scanner run.
Solution: Do not launch project compile in this window. Record Roslyn parse proof and leave compile status pending until dotnet/csc are clear.
Rejected Alternatives: Running build anyway, or claiming compile health from Roslyn parse success.
Scalability potential: Build validation is offline only and does not affect runtime tiers.
Hardware Impact: Avoids starving the shared workstation and interfering with other agents; no runtime impact.

## Decision 008 - Localize GPU Upload LockBuffer Views

Problem: `VoxelSurfaceNetsGpuUploadDispatcher` stored `_lockedVertices`, `_lockedIndices`, and `_lockedIndirectArgs` as class fields even though those views are valid only between `GraphicsBuffer.LockBufferForWrite` and the scheduled copy fence.
Solution: Remove the three fields and keep the locked `NativeArray` views as locals inside `TryBeginUpload`, then pass them by value into `VoxelSurfaceGpuUploadCopyJob`.
Rejected Alternatives: Keep fields and clear them after unlock. That still leaves stale native aliases on the dispatcher object between schedule and finalize.
Scalability potential: Low keeps no stale pointer across GPU upload frames; Middle/High/Ultra preserve the same double-buffered upload path and spend saved safety margin on denser visible chunks.
Hardware Impact: Removes three long-lived native view aliases from an object reachable on low-end i3/MX350 frames; expected CPU gain is small (<1 us/frame), crash-risk gain is material during relocation/driver stalls.

## Decision 009 - Release Sonar Descriptor Lock In Finally

Problem: `TryClearSonarSdfVaultDescriptor` acquired a descriptor write lock and released it after branch logic, leaving a future edit or non-finite edge case capable of skipping release.
Solution: Wrap descriptor mutation in `try/finally` and release `BufferID.VoxelSdfPayloadDescriptor` under `SystemID.WorldStreaming` in the `finally`.
Rejected Alternatives: Leave linear release after the mutation block. The mandate requires hard lock-release structure, not convention.
Scalability potential: Low/Middle/High/Ultra all rely on the same vault descriptor; lock discipline scales independently from visual quality.
Hardware Impact: Prevents writer-lock leaks that would stall later voxel/GPR frames on weak silicon; expected saved time is failure-path only, potentially entire frame stall avoided.

## Decision 010 - Delete Throwing SurfaceNets State Ref Accessors

Problem: `VoxelSurfaceNetsVault.GetStateAsRef` and `GetStateAsReadOnlyRef` constructed managed `InvalidOperationException` from a read accessor path. Project-wide search found no callers.
Solution: Remove both dead accessors and keep existing fail-closed `TryResolveViews`/job creation paths.
Rejected Alternatives: Return a static dummy ref. That would create a hidden mutable global fallback and conceal invalid state writes.
Scalability potential: Low avoids managed exception stalls; Middle/High/Ultra keep direct job-buffer views through explicit handle resolution.
Hardware Impact: Removes an allocation/exception path from the surface-nets vault API; expected hot gain is 0 us because unused, failure-path gain is avoiding managed exception construction.

## Decision 011 - Correct 1304 Blackbox Dump Route

Problem: SurfaceNets crash export wrote the secondary agent dump to `Dump_SHINOBU_308_Voxel.bin`, which is not the assigned 1304 artifact route.
Solution: Change the secondary dump filename to `Dump_1304_Voxel.bin` while preserving the existing primary mesh-surgeon dump.
Rejected Alternatives: Only document the mismatch. The mandate requires the binary file path to exist under this agent id.
Scalability potential: Low/Middle/High/Ultra use the same dump artifact; fidelity scaling does not change crash forensic ownership.
Hardware Impact: No frame impact; dump path is cold fault I/O only.

## Decision 012 - No Claim Of Absolute Exorcism Yet

Problem: Full AST after APEX still reports 1930 forbidden persistent native candidates across first-party scripts and 18 candidates in `VoxelSurfaceNetsVaultBuffers`. `HectonVoxelVolume` still owns four private sonar `NativeArray<byte>` fields with no audio-material `BufferID` route.
Solution: Record partial completion and block the sonar buffer deletion until a proper audio-material vault descriptor/BufferID is introduced or an existing route is proven. Do not fake zero.
Rejected Alternatives: Delete private sonar buffers and drop audio-material payloads, or reuse unrelated save buffer ids. Both would corrupt cross-domain read contracts.
Scalability potential: Low can continue using current fail-closed legacy snapshots; Middle/High/Ultra need the same descriptor route before visual overkill sonar material fidelity can be safely moved into the vault.
Hardware Impact: Remaining risk on i3/MX350 is stale private sonar aliases during relocation; not resolved yet. The safe patch removes 3 aliases now and avoids a larger compile-risk rupture under CPU saturation.

## Decision 013 - Move Sonar Audio Materials Into Vault Contract

Problem: `HectonVoxelVolume` could not delete private sonar audio-material arrays without a first-party vault route; the old descriptor only identified `BufferID.VoxelSdfTexture3D`.
Solution: Add `BufferID.VoxelSdfAudioMaterialIds = 621` and extend `VoxelSdfPayloadDescriptorDTO` to 80 bytes with `AudioMaterialByteCount`, `AudioMaterialBufferId`, `AudioMaterialBufferGeneration`, and `_pad0`.
Rejected Alternatives: Reuse save-game material buffers, omit audio materials from the public overload, or create a second descriptor buffer. Reuse would violate one-fact/one-owner; omission would break consumers; a second descriptor doubles synchronization risk.
Scalability potential: Low reads the same compact byte material ids; Middle/High/Ultra can preserve richer sonar material response without creating another owner route.
Hardware Impact: Removes four scene-lifetime private byte arrays from `HectonVoxelVolume`; on i3/MX350 the direct CPU gain is unproven, but relocation and stale-pointer failure risk is materially lower.

## Decision 014 - Transient Native Scratch Beats Cross-Frame Vault Write Locks

Problem: Publishing directly into vault buffers with an async Burst job would hold vault write locks across frames; completing the encode synchronously would remove locks but risks a visible hitch.
Solution: Use two local `NativeArray<byte>` scratch buffers in `PublishSonarSdfSnapshotAsync`, register them as `TempJob`, encode asynchronously, copy into vault buffers under short write locks, then dispose in `finally`.
Rejected Alternatives: Private scene-lifetime double buffers, cross-frame write locks on vault-owned buffers, or synchronous encode into final vault memory. Private buffers were the defect; cross-frame locks block defrag; synchronous encode spends too much frame time for a 129^3 grid.
Scalability potential: Low keeps publish off hot sample paths; Middle uses the same async route; High/Ultra can raise publish cadence or material fidelity through quality-weighted callers without changing DTO layout.
Hardware Impact: Expected low-end benefit is avoiding vault relocation stalls and persistent alias crashes; transient native allocation remains cold publish cost and is not a managed-GC allocation.

## Decision 015 - Lease Exact Vault Instance For Compaction Reads

Problem: `VoxelDeltaProcessor` copies the published SDF in a scheduled job after the accessor returns; a read-only view alone is not enough if the vault relocates while the copy job is pending.
Solution: `TryAcquirePublishedSonarSdfPayloadReadLease` now locks `BufferID.VoxelSdfTexture3D` under `SystemID.TerrainSeams` and stores the exact `IDataVault` reference in the lease so release unlocks the same vault instance.
Rejected Alternatives: Keep old double-buffer lease counters or unlock through the current `_cachedDataVault`. Old counters only protected private arrays; current-vault unlock can leak a lock if the DataVault service is hot-swapped.
Scalability potential: Low/Middle/High/Ultra compaction uses the same protected source-copy window; fidelity scaling does not change lock semantics.
Hardware Impact: Prevents defrag relocation during terrain-seam source copy; cost is one vault lock/unlock pair per scheduled compaction.

## Decision 016 - Stop At UI Compile Wall

Problem: Guard cleared, but `dotnet build Hecton8.Core.csproj --no-restore --nologo` failed with 24 `CS0246 FixedUiEventQueue<>` errors in UI/Visor files before the compiler reached voxel-specific validation.
Solution: Record `[BLOCKED BY DEPENDENCY]` and do not edit UI/Visor code from the voxel-domain task.
Rejected Alternatives: Patch UI queue types from this agent or claim compile success from Roslyn parse. The first violates domain ownership; the second is a false report.
Scalability potential: Build validation is assembly-wide; runtime tier scaling is unaffected by the external UI dependency wall.
Hardware Impact: No runtime impact from the failed compile attempt. Remaining risk is that voxel semantic compile errors, if any, are hidden behind the unrelated UI type failure.

## Decision 017 - Release Stale Sonar Lease Before Requeue

Problem: `VoxelDeltaProcessor.TrySchedulePendingCompaction` could acquire a sonar SDF read lease and then requeue when `publishedSonarVersion < RequiredSonarVersion` without releasing the lease.
Solution: Release `sourceSdfReadLease` before requeueing the compaction request.
Rejected Alternatives: Rely on later scratch cleanup. That path is not reached on the early requeue branch.
Scalability potential: Low/Middle/High/Ultra all use the same compaction lease route; fidelity scaling does not alter lease lifetime.
Hardware Impact: Prevents a vault lock leak that can stall defrag or later SDF readers on low-end silicon. Normal path cost is one existing unlock on a rare stale-version branch.

## Decision 018 - Move Carve Ingress Queue Into GlobalDataVault

Problem: `VoxelDeltaProcessor` owned a private scene-lifetime `NativeQueue<VoxelCarveEvent>` and prewarmed it with enqueue/dequeue traffic.
Solution: Replace it with `BufferID.ShinobuDeltaCrusherCarveEventQueue`, store only a `VaultGenerationHandle<VoxelCarveEvent>` plus head/count metadata, and mutate the ring under `TryAcquireWriteLock`/`ReleaseWriteLock`.
Rejected Alternatives: Managed fixed array, keeping `NativeQueue` and unregistering it more carefully, or sending only `SignalBus<VoxelCarveEvent>`. Managed array violates the unmanaged ownership target; retaining `NativeQueue` leaves the alias; SignalBus alone drops backlog/coalescing semantics.
Scalability potential: Low drains 1 event/frame; Middle/High/Ultra drain continuously up to 4 events/frame through `GlobalQualityWeight` without changing buffer layout.
Hardware Impact: Removes one persistent private native collection and one cold prewarm allocation path. Expected frame gain is small; relocation safety gain is material.

## Decision 019 - Scheduled Compaction Request Stores Metadata Only

Problem: `ScheduledCompactionRequest` stored nine `NativeArray` views across frames while the compaction job was running.
Solution: Keep only metadata and the sonar SDF read lease in the request; resolve compaction scratch buffers from vault handles after the job completes.
Rejected Alternatives: Retain views for convenience, or complete the job immediately to avoid cross-frame state. The first leaves stale aliases; the second creates a same-frame schedule/readback stall.
Scalability potential: Low/Middle/High/Ultra keep the same compaction job, but the owner object no longer caches mutable native views.
Hardware Impact: Removes nine AST-visible cross-frame native view fields; normal path adds one vault handle resolve group after job completion.

## Decision 020 - Delete Dead Non-Uniform Compacted State Native Views

Problem: `CompactedChunkState` still declared three `NativeArray` fields for non-uniform compacted storage, but current call sites construct only uniform RLE compacted states.
Solution: Remove the dead non-uniform native-view fields and constructor; compacted state now stores only RLE metadata.
Rejected Alternatives: Keep dormant non-uniform fields for future work. Future non-uniform compacted storage needs a vault descriptor route, not hidden struct-held native views.
Scalability potential: Low uses uniform RLE; Middle/High/Ultra can add non-uniform visual overkill later through a vault-backed DTO without changing current save identity.
Hardware Impact: Removes three persistent alias candidates and shrinks compacted-state copy payload. Measured runtime gain is not available.

## Decision 021 - Do Not Claim Absolute Zero

Problem: Final scanner still reports target-scope candidates: four `ChunkDeltaState` vault-backed native slice fields and eighteen `VoxelSurfaceNetsVaultBuffers` phase-local view fields. Project-wide first-party count is 1906 forbidden candidates.
Solution: Report partial hardening with exact residuals and hash `3e3a8c35c94bc3e80f3275f2b3d6760c15c3c07582c903db0e971b04d08c0618`.
Rejected Alternatives: Rename fields, suppress scanner output, or claim that vault-backed slices equal zero. That would be a false optimization report.
Scalability potential: Low/Middle/High/Ultra need a future chunk-state handle/slice refactor before absolute memory sovereignty can be claimed.
Hardware Impact: Remaining `ChunkDeltaState` slices are the next real risk during vault relocation; no measured frame cost is available yet.

## Decision 022 - Match Publish Scratch Allocator To TempJob Lifetime

Problem: `PublishSonarSdfSnapshotAsync` registered two scratch arrays as `TempJob` lifetime but allocated them with `Allocator.Persistent`.
Solution: Change both publish scratch arrays to `Allocator.TempJob` and keep the existing `finally` disposal.
Rejected Alternatives: Keep `Allocator.Persistent` for safety. That contradicts the lifetime proof and makes the scratch look like a persistent allocation.
Scalability potential: Low/Middle/High/Ultra keep the same async publish route; only allocator lifetime is corrected.
Hardware Impact: Removes a cold persistent native allocation route. Frame-time gain is unmeasured; memory lifetime accuracy improves.

## Decision 023 - Replace ChunkDeltaState Native Slices With Pool Slot Metadata

Problem: `ChunkDeltaState` still stored four `NativeArray` subarray slices inside registry/pool state. Even though the physical owner was the vault, the struct still cached relocation-sensitive physical views.
Solution: Remove `DirtyMaskWords`, `SdfValueBits`, `MaterialIds`, and `CellFlags` from `ChunkDeltaState`. Store only `ChunkCoord`, `VoxelSize`, `DirtyCellCount`, `PoolSlot`, and `VaultBacked`; resolve dirty-mask/SDF/material/flag subarrays from vault handles at the phase where they are used.
Rejected Alternatives: Suppressing the scanner or renaming the fields. That would not remove stale pointer risk.
Scalability potential: Low keeps fixed 256 chunk slots; Middle/High/Ultra can increase pool capacity by vault buffer size without changing state layout.
Hardware Impact: Removes four registry-held native aliases from the delta processor. Cost is extra vault handle resolution at phase boundaries; expected low-end gain is relocation safety, not raw CPU.

## Decision 024 - Remove SurfaceNets View Aggregate Native Fields

Problem: `VoxelSurfaceNetsVaultBuffers` was phase-local, but it still declared 18 `NativeArray` fields and therefore failed a strict AST gate.
Solution: Convert the aggregate to store only `IDataVault` plus `VoxelSurfaceNetsVaultHandles`. Existing member names now resolve `NativeArray<T>` through properties from the vault handle.
Rejected Alternatives: Keeping the aggregate and documenting it as safe. The user's APEX review explicitly rejected proof by explanation when a code-level removal was feasible.
Scalability potential: Low/Middle/High/Ultra keep the same job signatures; only the resolution boundary changed.
Hardware Impact: Removes 18 AST-visible native view fields. Property resolution adds handle lookups before scheduling and diagnostics; no managed allocation is introduced.

## Decision 025 - Compile Wall Is External After Target Alias Removal

Problem: After target alias removal, compile validation was required. The build guard was clear, but `dotnet build Hecton8.Core.csproj --no-restore --nologo` failed in non-voxel domains.
Solution: Record the exact external wall: 2 `CS0122` errors in `Audio/AcousticPortalPropagation.cs` and 16 `CS0177` errors in `TetherInstance.cs`. Do not patch Audio/Tether from the 1304 voxel mandate.
Rejected Alternatives: Editing adjacent domains to force green build, or claiming compile success from Roslyn parse.
Scalability potential: Compile health is cross-domain and independent of voxel quality tiers.
Hardware Impact: No runtime impact from the compile wall itself; release verification remains incomplete until those external errors are fixed.

## Decision 026 - Correct The Actual VoxelDelta Blackbox Route

Problem: APEX recheck found that status claimed `Dump_1304_Voxel.bin`, while `VoxelDeltaProcessor` still pointed its dump constant at `Dump_1312_VoxelPaging.bin`.
Solution: Change `VoxelBlackBoxDumpRelativePath` to `Docs/AgentLogs/Dump_1304_Voxel.bin` and rerun the route text scan.
Rejected Alternatives: Leaving the mismatch as documentation debt. The blackbox route is a proof artifact, not cosmetic text.
Scalability potential: Low/Middle/High/Ultra use the same crash dump route; quality scaling does not alter forensic ownership.
Hardware Impact: No frame cost; fault-path binary dump now lands in the correct 1304 artifact.

## Decision 027 - Make Private Layout Validation Agent-Owned

Problem: The private DTO validator was reachable through a 1304 alias but still used `Agent1312` method/assert names, which made proof ownership ambiguous.
Solution: Rename the private layout validator helpers to `ValidateAgent1304PrivateLayouts`, `AssertAgent1304ExplicitLayout`, and `AssertAgent1304Offset`.
Rejected Alternatives: Keeping a compatibility alias. The user requested byte-level evidence, and cross-agent naming is unacceptable in that evidence.
Scalability potential: Low/Middle/High/Ultra receive the same editor layout gate.
Hardware Impact: Editor-only; prevents future ARM64 offset drift before it reaches weak silicon.

## Decision 028 - Reorder VoxelMeshingTuningDTO For ARM64 Field Priority

Problem: `VoxelMeshingTuningDTO` had `ulong LastCsvWriteTicks` at offset 56 after 4-byte fields, violating the 8-byte-first mandate despite an aligned total size.
Solution: Move `LastCsvWriteTicks` to offset 0 and shift 4-byte fields after it while preserving the 64-byte explicit struct size.
Rejected Alternatives: Relying on total-size alignment only. The mandate requires field-order alignment discipline, not just size divisibility.
Scalability potential: Low/Middle/High/Ultra tuning data remains stable; `GlobalQualityWeight` keeps continuous scaling at offset 8.
Hardware Impact: Prevents unaligned wide-field access risk on ARM64 Quest-class devices; runtime microseconds unmeasured.

## Decision 029 - Editor Fuzzer And Validator Stay Cold

Problem: Defrag race fuzzing and layout verification require managed editor orchestration, but runtime hot paths must not gain managed allocations or extra thread churn.
Solution: Add `VoxelMemorySovereigntyValidator1304` under `World/VoxelSurfaceNets/Editor` with editor-only layout assertions and a vault defrag race fuzzer.
Rejected Alternatives: Running fuzzer logic in player builds or adding hot runtime assertions. That would spend frame time on proof machinery.
Scalability potential: Low uses the validator to prevent stale handle regressions; Middle/High/Ultra get the same gate before increasing surface fidelity.
Hardware Impact: 0 player runtime us; editor-only cold validation.

## Decision 030 - Classify New Tokens Instead Of Hiding Them

Problem: Text scan still finds `new` tokens in touched files, including DTO value construction, core memory allocator construction, smoke-test TempJob arrays, and editor diagnostics.
Solution: Record the classification instead of claiming lexical zero. Production target Roslyn native-owner fields are zero; `HectonVoxelVolume` retains two cold `Allocator.TempJob` publish scratch arrays disposed in `finally`; `H8Memory` persistent allocations are the core memory authority; smoke-test allocations are non-production.
Rejected Alternatives: Renaming or suppressing `new` tokens, or deleting smoke/core tooling to make a grep pass.
Scalability potential: Low/Middle/High/Ultra need honest allocation class boundaries before performance claims.
Hardware Impact: Hot-path managed GC claim remains limited to scanned production paths; profiler proof is still required for exact us.

## Decision 031 - AUP Local-Delta Rule Is Preserved With One Legacy Bridge Noted

Problem: The user required proof that AUP spatial calculations subtract a double origin before any float downcast.
Solution: Verify `HectonFloatingOrigin.ToRuntimePosition(double3,double3)` returns `ToVector3(absoluteUniversePosition - committedTotalOffset)`, SurfaceNets priority uses `AupPrecisionMath.LocalDeltaDouble` then `DowncastLocalDelta`, and SDF sampling subtracts `VolumeOrigin` in double before casting local sample coordinates to float.
Rejected Alternatives: Claiming all Vector3 bridges are harmless. `HectonVoxelVolume.TryResolveRuntimeAbsoluteVector` remains a legacy absolute-vector bridge and is documented as a residual risk if used for long-lived absolute storage.
Scalability potential: Low/Middle/High/Ultra all share the same AUP determinism route; quality controls must not alter coordinate truth.
Hardware Impact: Prevents precision loss and spatial jitter on large worlds; exact microseconds unmeasured.

## Decision 032 - Compile Caught Private Pad Validator Debt

Problem: The editor layout validator used `nameof(VoxelCarveTelemetryEntry._pad0)` and `nameof(CarveCellWrite._pad*)` from outside those private nested structs. That made the proof code itself fail compilation.
Solution: Keep padding fields private and pass the private field names as string literals into the reflection-backed offset check. Remove the 1312 helper indirection so `ValidateAgent1304PrivateLayouts` owns the byte-map proof directly.
Rejected Alternatives: Making padding fields public/internal only to satisfy `nameof`, or deleting private padding checks. Both weaken the DTO encapsulation/offset proof.
Scalability potential: Low/Middle/High/Ultra all get the same editor gate before ARM64 builds; quality scaling does not alter DTO layout.
Hardware Impact: 0 player runtime us; prevents private DTO alignment drift from reaching Quest/i3/MX350-class builds.

## Decision 033 - AUP Proof Includes Dev And Editor DTO Bridges

Problem: Runtime AUP math was local-delta clean, but dev smoke/fuzzer code still populated legacy `float3` carve bridge fields from literal absolute values. That leaves a proof gap even if it is not a production hot path.
Solution: Convert those writes to `double3 hitAup/endAup`, subtract `double3 originAup`, and only cast the resulting local delta into `float3`.
Rejected Alternatives: Dismissing the issue as smoke/editor-only. The assignment demanded byte/line-level proof, not contextual excuses.
Scalability potential: Low/Middle/High/Ultra preserve one coordinate rule across validation and runtime code.
Hardware Impact: 0 measured frame change; removes precision-policy ambiguity before tests are reused on large AUP coordinates.

## Decision 034 - Route Scan Overrides Prior Report Text

Problem: The report claimed the 1304 dump route was fixed, but target grep still found `Dump_1312_VoxelPaging.bin` and a stale SHINOBU smoke sentinel.
Solution: Change `VoxelBlackBoxDumpRelativePath` to `Docs/AgentLogs/Dump_1304_Voxel.bin`, update the smoke sentinel to the same filename, and require target grep to return 0 hits for `Agent1312`, `Dump_1312`, and `Dump_SHINOBU`.
Rejected Alternatives: Only updating docs, or keeping old sentinel text for compatibility. Crash dump ownership is a hard artifact route.
Scalability potential: Low/Middle/High/Ultra all dump to the same fixed forensic file; no quality tier changes the route.
Hardware Impact: 0 normal-path us; fault-path diagnostics now land in the owner file without cross-agent ambiguity.

## Decision 035 - Project Build Wall Is External And Reproducible

Problem: After local compile defects were removed, the assembly build still fails.
Solution: Rerun the guarded build and record the exact remaining wall: 2 `CS0122` errors in Audio and 19 Tether errors (16 `CS0177`, 3 `CS0246`). No current build error points at the 1304 voxel files.
Rejected Alternatives: Editing Audio/Tether from the voxel memory mandate, or claiming green compile. Both violate domain boundaries/evidence rules.
Scalability potential: Build status is cross-domain; voxel quality tiers remain unaffected until the external wall is cleared.
Hardware Impact: No runtime impact from the compile wall itself. Release verification remains blocked by non-voxel owners.

## Decision 036 - Blackbox Ring Writes Require Vault Write Lock

Problem: `WriteBlackBoxSample` wrote directly into the vault-backed `ShinobuDeltaCrusherVoxelBlackBox` buffer after `TryResolveHandle`. During defrag/relocation this can race the vault owner and invalidate the fixed 300-frame crash proof.
Solution: Replace the direct resolve path with `TryAcquireBlackBoxBuffer`, which validates the handle/length first, re-ensures only when the handle is stale or invalid, acquires `TryAcquireWriteLock` under `SystemID.TerrainSeams`, and releases through `ReleaseBlackBoxBuffer` in `finally`. `DumpBlackBox` now also locks while copying the ring to disk.
Rejected Alternatives: Keeping direct `TryResolveHandle` because writes are small. Size is irrelevant; unmanaged relocation safety requires the vault lock.
Scalability potential: Low/Middle/High/Ultra keep the same 300-entry ring. Quality weight does not alter telemetry layout or dump ownership.
Hardware Impact: Normal frame cost only when fault telemetry is written; expected cost is one short vault lock/unlock pair. Failure-path correctness gain is preventing corrupted dumps or relocation races on weak silicon.

## Decision 037 - SurfaceNets Jobs Need Full Fence-Window Vault Pins

Problem: `VoxelSurfaceNetsVault` created Burst job and GPU upload jobs from resolved vault-owned `NativeArray` views, but the DataVault relocation pin did not live for the full scheduled-job or GPU-copy fence window. A defrag between schedule and finalize could stale the physical view.
Solution: Add `VoxelSurfaceNetsJobBufferLease` and `VoxelSurfaceNetsGpuUploadSourceLease`. `TryScheduleMockDensityPinned`, `TryScheduleExtractionPinned`, and `TryScheduleHzbCullPinned` acquire exact buffer locks before creating jobs and return the lease to the dispatcher. `VoxelSurfaceNetsGpuUploadDispatcher` stores `_pendingSourceLease` and releases it only after `DispatcherJobFence.TryFinalizeCompleted` and GPU buffer unlock. Old `TrySchedule*` entrypoints now fail closed.
Rejected Alternatives: Releasing pins immediately after job scheduling; documenting caller discipline while keeping old APIs active; completing jobs synchronously to avoid cross-frame leases. The first two leave stale-view risk; the third violates frame-time/job doctrine.
Scalability potential: Low uses the same cheap cadence with fewer relocation crashes; Middle/High/Ultra can raise meshing cadence through `GlobalQualityWeight` without changing ownership or DTO layout.
Hardware Impact: Expected normal cost is short lock/unlock pairs per scheduled meshing/upload window. On i3/MX350 and Quest-class ARM64 this avoids relocation stalls, corrupted uploads, and stale native pointer crashes; no measured microsecond saving claimed.

## Decision 038 - Proof Route Must Not Carry 1312 Names

Problem: A later line review found `VoxelDeltaProcessor` still had `Docs/AgentLogs/Dump_1312_VoxelPaging.bin` and `AssertAgent1312*`/`ValidateAgent1312*` private layout helper names despite status text claiming 1304 ownership.
Solution: Set the dump path to `Docs/AgentLogs/Dump_1304_Voxel.bin`, collapse the private layout validator into `ValidateAgent1304PrivateLayouts`, and rename assertion helpers to `AssertAgent1304ExplicitLayout`/`AssertAgent1304Offset`.
Rejected Alternatives: Leaving the stale names because runtime behavior was equivalent. The dump route and byte-map validator are proof artifacts; cross-agent ownership drift invalidates the audit trail.
Scalability potential: Low/Middle/High/Ultra use one crash artifact and one layout proof route; quality weight must not alter forensic ownership.
Hardware Impact: 0 normal-path us. Fault-path binary dumps and ARM64 layout validation now point to the correct owner file and validator names.

## Decision 039 - SurfaceNets Output Buffers Require Write Locks

Problem: Loop 11 pinned SurfaceNets job buffers, but output buffers were acquired through read-pin semantics. Jobs then wrote through views that the vault had not granted as writers.
Solution: Add `WriteMask` to `VoxelSurfaceNetsJobBufferLease`, route output buffers through `TryAcquireWriteLock`, and release write locks before read pins in `ReleaseJobBufferLease`.
Rejected Alternatives: Treat job ownership as implicit because the scheduler is the only caller, or serialize jobs with `.Complete()` to avoid lease windows. Implicit ownership breaks DataVault relocation rules; forced completion violates frame-time/job doctrine.
Scalability potential: Low keeps safe low-cadence extraction; Middle/High/Ultra can raise chunks per frame by `GlobalQualityWeight` without changing ownership semantics.
Hardware Impact: Expected cost is a short write-lock/unlock pair per scheduled SurfaceNets job. Gain is preventing stale writes/corrupted buffers during defrag on Quest/i3/MX350-class hardware; no measured microseconds claimed.

## Decision 040 - Crater AUP Must Stay Double Until Local Runtime Projection

Problem: Crater stamps and resource crater cluster math still carried absolute positions through `float3`/`Vector3` bridges. That violates the AUP rule when the world origin is far from zero.
Solution: Store `VoxelCraterStamp.position` as `double3` in a 32-byte explicit DTO, compute collapse deltas/bounds in double-space, and downcast only through `HectonFloatingOrigin.ToRuntimePosition` or the finite/range-checked legacy MapMagic bridge.
Rejected Alternatives: Keep `float3` because crater radii are small, or classify MapMagic absolute vectors as harmless. Small radius does not protect large absolute coordinates; legacy bridges need explicit fail-closed gates.
Scalability potential: Low/Middle/High/Ultra share one coordinate truth path. Quality weight may change cadence/visual density, not coordinate authority or DTO layout.
Hardware Impact: Removes large-world precision loss and collision/deformation jitter risk. Normal CPU cost is negligible relative to crater write loops; exact profiler microseconds unavailable.

## Decision 041 - Build/Scanner Gate Respects CPU Load And User Instruction

Problem: After the final AUP patch, a fresh Roslyn run or `dotnet build` would be ideal proof, but the machine reported CPU load at 100 and the user explicitly forbade frequent dotnet/build attempts under load.
Solution: Stop at static text scans, brace checks, diff checks, and previously captured Roslyn hashes. Record that final build/scanner proof is pending a clear CPU guard.
Rejected Alternatives: Run build anyway, or claim the previous build/scanner covers files changed afterward. Both would violate the operator instruction or overstate proof.
Scalability potential: Validation scheduling is independent of device quality tiers; runtime code remains deterministic.
Hardware Impact: Avoids adding local machine contention. Release verification remains incomplete until CPU is below guard and a single controlled build/scanner pass can run.

## Decision 042 - Failed Route Grep Beats Previous Status Text

Problem: After writing the Loop 12 status/log, a fresh route grep still found `Dump_1312_VoxelPaging.bin` and `Agent1312` helper names in `VoxelDeltaProcessor.cs`.
Solution: Patch the actual source after the failed grep: set `VoxelBlackBoxDumpRelativePath` to `Docs/AgentLogs/Dump_1304_Voxel.bin` and rename the private layout helper route to `ValidateAgent1304PrivateLayouts`/`AssertAgent1304*`.
Rejected Alternatives: Leave the log as-is because the intended fix was described. The source is the artifact; report text without code proof is invalid.
Scalability potential: Low/Middle/High/Ultra share one crash dump route and one layout validator ownership route.
Hardware Impact: 0 normal-path us. Prevents fault dumps and ARM64 proof failures from being attributed to the wrong agent route.

## Decision 043 - Loop 13 Uses One AST Audit, No Build

Problem: The prior source route proof was invalid because the source still carried stale 1312 strings when re-grepped. A text-only correction was not enough; the native alias proof also needed a fresh AST artifact after the actual source patch.
Solution: Patch the source again, rerun one Roslyn native alias audit under CPU/dotnet guard, and record the exact hash. Do not run project build in the same pass.
Rejected Alternatives: Skip AST to avoid dotnet entirely, or run full build. Skipping AST would leave the user's requested persistent-native proof stale; running build would violate the "rare build" instruction and is already known to hit external Audio/Tether wall.
Scalability potential: Low/Middle/High/Ultra retain the same memory authority route; verification cadence does not alter runtime behavior.
Hardware Impact: 0 player runtime us. Offline scanner cost was 19.8 s wall time; no frame-time claim.

## Decision 044 - HectonVoxelEngine Native Scratch Is Classified, Not Hidden

Problem: Text scan finds many `NativeArray` fields in `HectonVoxelEngine.VoxelStreamingScratchSlot` and static Marching Cubes tables. Calling the whole touched file "zero native arrays" would be false.
Solution: Use the Roslyn classifier and report the boundary: `HectonVoxelEngineForbidden=0`, while cold `DataVaultExempt*` scratch/table allocations remain present and registered with `NativeMemorySentinel`.
Rejected Alternatives: Delete/migrate the full voxel mesh pipeline scratch graph during a route/AUP correction pass, or pretend grep hits are harmless without classification. The first is a high-blast-radius rewrite; the second is dishonest.
Scalability potential: Low uses the same scratch graph with bounded capacities; Middle/High/Ultra can increase quality/cadence through existing `GlobalQualityWeight`/pipeline gates without changing DTO layout.
Hardware Impact: No measured gain. Residual scratch memory remains a known non-DataVault-exempt area requiring a dedicated route card if the mandate expands beyond the strict SurfaceNets/Delta/sonar payload fix.

## Decision 045 - Managed Reference Registries Are Cold But Not Zero-New

Problem: A stricter runtime grep for managed construction found `new object()` and `new List<>` in voxel runtime files. These are cold Unity-reference registries and synchronization objects, but they still falsify any absolute claim of "no managed new anywhere in runtime code".
Solution: Record the boundary explicitly. Keep the current code unchanged in this pass because replacing Unity object/reference registries with unmanaged GlobalDataVault descriptors changes ownership semantics and needs a separate controlled migration.
Rejected Alternatives: Claiming the scan is clean because the allocations are cold, or replacing the registries with managed arrays. Cold allocations are still managed `new`; managed arrays would not satisfy the strict zero-managed-allocation demand.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. A future unmanaged bridge would store stable instance IDs or vault descriptors and keep UnityEngine.Object dereference in a cold owner phase only.
Hardware Impact: No frame-time gain claimed. Current risk is GC pressure only at cold initialization/registration paths, not per-frame string/LINQ/boxing churn. Release-level "absolute zero managed new" remains blocked until the bridge migration is done.

## Decision 046 - Published SDF Registry Can Be Intrusive Without Ownership Drift

Problem: `HectonVoxelVolume` used a static `List<HectonVoxelVolume>` for published SDF read candidates. It was cold, but it was still a managed container allocation inside runtime voxel code.
Solution: Replace it with an intrusive linked registry stored on each `HectonVoxelVolume` instance. Register/unregister owner phases mutate `_publishedNext`, `_publishedPrev`, and `_publishedRegistered`; read paths walk `s_activePublishedHead` by direct references.
Rejected Alternatives: Replacing the list with a managed array, or moving the registry to a NativeArray of object handles in the same pass. A managed array still allocates; an instance-id/GDV handle bridge would require a larger resolver contract and controlled build.
Scalability potential: Low/Middle/High/Ultra keep the same max published volume cap. Quality weight does not alter registry identity or SDF authority.
Hardware Impact: Removes one cold managed List allocation and its capacity backing array. Hot-path iteration remains O(n) over the same capped candidates; no measured microseconds claimed.

## Decision 047 - MCTables Gate Must Not Allocate A Lock Object

Problem: `MCTables` used `static readonly object _initLock = new object()`. It serialized cold table init/shutdown but violated the strict text scan for managed construction.
Solution: Replace the object lock with an integer interlocked gate using `Interlocked.CompareExchange`, `SpinWait`, and `Volatile.Write`.
Rejected Alternatives: Removing synchronization entirely, or keeping the object because init is cold. Cold does not satisfy the current APEX scan; removing synchronization risks double native table allocation/disposal.
Scalability potential: All quality tiers share one static table route. Quality weight does not alter table ownership.
Hardware Impact: Removes one cold heap allocation. Init/shutdown contention cost is negligible and outside frame hot paths.

## Decision 048 - Streaming Scratch Lock Object Replaced With Value Scope

Problem: `HectonVoxelEngine` still allocated `_streamingScratchGate = new object()` for scratch-slot serialization.
Solution: Replace the object lock with an integer interlocked gate and a value-type `StreamingScratchGateScope`; all existing guarded sections keep `try/finally` release semantics via `using`.
Rejected Alternatives: Removing synchronization, or leaving the object because scratch allocation is cold. Scratch slot mutation crosses async generation windows; synchronization stays mandatory.
Scalability potential: Low/Middle/High/Ultra keep identical scratch ownership. Quality weight may alter capacity/cadence, not the gate contract.
Hardware Impact: Removes one cold heap allocation. Runtime contention is still bounded to scratch preparation windows; no measured frame-time gain claimed.

## Decision 049 - Small Runtime Index Stacks Use FixedList

Problem: `VoxelDeltaProcessor._chunkStateFreeStack` and `HectonVoxelVolume._terrainHoleHandles` used managed `int[]` buffers for fixed-capacity integer lanes.
Solution: Replace them with `FixedList4096Bytes<int>` and `FixedList128Bytes<int>` respectively. These are value-type fixed buffers with no managed array object.
Rejected Alternatives: NativeArray migration. These buffers are owner-local, tiny, and do not cross job/domain boundaries; native allocation would add lifecycle burden without sovereignty gain.
Scalability potential: Capacity remains fixed and deterministic for all quality tiers.
Hardware Impact: Removes two cold managed array allocations. Hot-path index access remains direct and bounded; no measured microseconds claimed.

## Decision 050 - Leak Sentinel Should Be Intrusive

Problem: `VoxelVolumeLeakSentinel` used three static managed arrays to track volume lifecycle states.
Solution: Move sentry state onto each `HectonVoxelVolume` instance and keep one intrusive linked list. Destroy-pending pump still runs from the late-frame driver and reports after the same 300-frame deadline.
Rejected Alternatives: Keeping fixed arrays because telemetry is cold, or moving Unity object references into NativeArray. Fixed arrays violate the current scan; NativeArray cannot hold managed Unity object references.
Scalability potential: Low/Middle/High/Ultra lifecycle proof remains the same. No quality tier changes leak telemetry ownership.
Hardware Impact: Removes three cold managed array allocations. Pump complexity remains O(active tracked volumes); no measured frame-time gain claimed.

## Decision 051 - FixedChunkRegistry Occupancy Bitmap Can Be Inline

Problem: `FixedChunkRegistry<T>` stored occupancy flags in a managed `byte[]`.
Solution: Replace `_occupied` with `FixedList4096Bytes<byte>`, initialized to the existing fixed capacity. Key/value storage remains managed arrays and is not claimed fixed in this pass.
Rejected Alternatives: Rewriting the whole generic registry to unmanaged storage. The values include owner-local structs with disposal semantics; full migration needs a separate route card and compile window.
Scalability potential: Registry capacity remains `InitialChunkRegistryCapacity=256` for every tier.
Hardware Impact: Removes one cold managed byte-array allocation per registry instance. Remaining key/value arrays are still a known non-green boundary.

## Decision 052 - Marching Cubes Seed Tables Must Not Allocate Managed Arrays

Problem: `MCTables.Init()` still seeded `_edgeTable` and `_triTable` from cold managed `new int[256]` and `new int[4096]` literals. Cold init still fails the current APEX text gate.
Solution: Convert both literals to method-local `ReadOnlySpan<int>` stackalloc tables and copy into the existing unmanaged native tables.
Rejected Alternatives: Keep managed arrays because init is cold, or generate the tables procedurally at runtime. Cold managed arrays fail the requested proof; procedural generation adds CPU work and risk for fixed lookup data.
Scalability potential: Low/Middle/High/Ultra share the same lookup data. Quality weight changes cadence/fidelity elsewhere, not table identity.
Hardware Impact: Removes two cold managed array allocations. No frame-time gain claimed; exact compiler verification is pending because CPU guard blocked build.

## Decision 053 - Delta Volume Registry Can Be Intrusive

Problem: `VoxelDeltaProcessor.FixedVolumeRegistry` allocated `HectonVoxelVolume[]` for registered and pending-rebuild volume lanes.
Solution: Make the registry a value-type intrusive list and store lane-specific next/prev/registered fields directly on `HectonVoxelVolume`.
Rejected Alternatives: Replace with another managed fixed array, or move Unity object references into `NativeArray`. The first is still managed heap; the second is invalid for managed Unity object references.
Scalability potential: Capacity remains 64 for all tiers. Low devices get no heap container; high/ultra can still issue more visual work through existing continuous quality budgets without changing volume identity.
Hardware Impact: Removes two cold managed volume-reference arrays and one registry-object allocation boundary. Iteration remains bounded by 64; no measured microseconds claimed.

## Decision 054 - Residual Managed Boundaries Are Real, Not Hidden

Problem: A broader APEX scan still finds managed arrays/lists in save DTO projection, Unity collider/mesh pools, shader vector upload buffers, cave snapshot arrays, active volume registries, and deferred PhysX queues.
Solution: Document the exact residual line groups instead of claiming release green. Do not rewrite Unity reference pools into unmanaged memory in this pass; those need a descriptor bridge or Unity-owner phase contract.
Rejected Alternatives: Deleting arrays to satisfy grep, converting UnityEngine.Object references to NativeArray, or claiming `Array.Empty<T>()` is zero-GC enough for the stated release gate. All three would be false or unsafe.
Scalability potential: Low/Middle/High/Ultra still run with bounded capacities, but absolute heap-free release proof remains blocked until those lanes get explicit owner bridges.
Hardware Impact: No additional frame-time claim. Remaining risk is cold heap pressure and Unity-reference ownership, not hot string/LINQ churn.

## Decision 055 - AirPocket Registry Is Pure Unmanaged State

Problem: `HectonVoxelEngine` stored air-pocket centers, extents, and oxygen refill fractions in three managed arrays even though the data is pure scalar geometry.
Solution: Replace the three arrays with `FixedList4096Bytes<AirPocketEntry>` and a 32-byte explicit `AirPocketEntry` DTO.
Rejected Alternatives: Keep the arrays because capacity is fixed, or move the lane to `NativeArray`. Fixed managed arrays still fail the heap scan; a native allocation adds lifecycle and DataVault ownership overhead for 64 scalar records.
Scalability potential: Capacity stays 64 across Low/Middle/High/Ultra. Quality weight does not alter air-pocket identity.
Hardware Impact: Removes three cold managed array allocations. Runtime lookup remains O(64) worst case; no measured microseconds claimed.

## Decision 056 - Active Volume Bounds Cache Can Be Inline

Problem: `_activeVolumeLocalBounds` used a managed `List<Bounds>` even though it stores only local gizmo bounds and is index-aligned with active volume references.
Solution: Replace it with `FixedList4096Bytes<ActiveVolumeLocalBoundsEntry>` and a 24-byte explicit DTO. Unity object references remain in managed lists because they cannot be placed in unmanaged containers safely.
Rejected Alternatives: Move `GameObject`/`HectonVoxelVolume` references into unmanaged storage, or drop bounds caching. Unmanaged Unity references are invalid; dropping cache changes editor/debug behavior.
Scalability potential: Bounds capacity remains aligned with `ActiveVolumeRegistryCapacity=64` across all tiers.
Hardware Impact: Removes one cold managed `List<Bounds>` allocation and backing array. No hot-frame saving measured.

## Decision 057 - Engine DTO Validator Must Cover New Inline Lanes

Problem: Adding `AirPocketEntry` and `ActiveVolumeLocalBoundsEntry` without a byte-layout validator leaves ARM64 proof incomplete.
Solution: Add `HectonVoxelEngine.ValidateAgent1304EnginePrivateLayouts` under `UNITY_EDITOR` and call it from `VoxelMemorySovereigntyValidator1304`.
Rejected Alternatives: Report byte maps only in markdown. Manual docs drift; validator gives a cold editor gate when Unity compilation is available.
Scalability potential: Layout validation is tier-independent and does not alter runtime quality.
Hardware Impact: 0 player runtime us; editor-only proof path. Compile validation is pending CPU gate.

## Decision 058 - Mesh Pool Occupancy Flags Can Be Inline Bytes

Problem: `HectonVoxelEngine` still used two managed `bool[]` arrays to track occupancy for preallocated voxel surface and PhysX bake mesh pools. The mesh arrays themselves contain Unity `Mesh` references and cannot be moved to unmanaged storage, but the occupancy flags are pure scalar state.
Solution: Replace both bool arrays with `FixedList4096Bytes<byte>` and use byte flags (`0/1`). Add `EnsureVoxelMeshPoolOccupancyFlags()` with an interlocked initialization gate so the fixed lists are populated once without allocating a managed lock object.
Rejected Alternatives: Moving `Mesh` references into unmanaged storage, keeping `bool[]` because it is cold, or using a `NativeArray<byte>`. Unity references in unmanaged containers are invalid; cold managed arrays still fail the current scan; a native allocation would add lifecycle burden for 256 scalar flags.
Scalability potential: Low/Middle/High/Ultra keep the same pool sizes and identity route. Quality weight can change meshing cadence elsewhere, not mesh ownership or occupancy layout.
Hardware Impact: Removes two cold managed bool-array allocations. Hot acquire/release remains bounded O(256); no measured microseconds claimed.

## Decision 059 - One Build Attempt Only Under Clear CPU Guard

Problem: The latest stackalloc/FixedList edits needed compiler validation, but the operator explicitly forbids frequent dotnet/build attempts and forbids builds while CPU is above 50% or dotnet/csc is active.
Solution: Run one `dotnet build Hecton8.Core.csproj --no-restore --nologo` only after the guard reported `cpu=42` and `dotnet_csc_count=0`. Stop after the external compile wall and do not retry.
Rejected Alternatives: Re-running builds after every patch, or claiming compiler proof without a build attempt. The first violates the operator instruction; the second is a false report.
Scalability potential: Build scheduling is offline and tier-independent. It does not alter Low/Middle/High/Ultra runtime behavior.
Hardware Impact: 0 player runtime us. Build failed with 126 external errors in non-1304 domains, so release compile proof remains blocked by integration debt, not by measured voxel frame cost.

## Decision 060 - Crater Registries Can Be Inline Fixed Lists

Problem: `HectonVoxelVolume` still allocated two managed `VoxelCraterStamp[]` buffers for runtime crater replay and resource-crater collapse clustering, even though `VoxelCraterStamp` is already an explicit unmanaged 32-byte DTO and the lane is capped at 16 entries.
Solution: Replace both arrays with `FixedList4096Bytes<VoxelCraterStamp>`, clear the fixed lists on volume reset, and write entries through `Add`/index replacement. Replace public array exposure with `TryGetCraterStamp` so the rebuild path reads one DTO at a time. Add count-vs-length guards that clear the affected crater registry and return fail-closed before any out-of-range access.
Rejected Alternatives: Keeping arrays because the cap is small, returning the fixed list by value, or moving the lane to `NativeArray`. Small managed arrays still fail the current heap scan; returning a 4096-byte fixed list by value is unnecessary copying; a native allocation adds lifecycle overhead for 16 explicit DTOs.
Scalability potential: Low/Middle/High/Ultra keep the same 16-stamp cap. Quality weight may change carve cadence elsewhere, not crater DTO layout or coordinate ownership.
Hardware Impact: Removes two cold managed crater-array allocations and one public managed array route. Hot crater merge/collapse remains O(16); no measured microseconds claimed.

## Decision 061 - Do Not Rewrite Cave Graph Arrays In This Loop

Problem: Remaining cave graph arrays (`CaveNode[]`, `CaveTunnel[]`, `CaveEntrance[]`, `CaveStructure[]`) are managed, but they are variable-size graph snapshots consumed by the rebuild scratch path. Nodes/entrances could fit in `FixedList4096Bytes`; tunnels/structures can exceed 4096 bytes at current capacities.
Solution: Leave the graph snapshot arrays as explicit residuals until a unified graph snapshot bridge is designed. Partial conversion would split the graph ownership model and still leave the largest arrays managed.
Rejected Alternatives: Converting only nodes/entrances, or forcing tunnels/structures into an undersized fixed list. Partial conversion gives false release optics; undersized fixed lists would drop cave geometry or force silent truncation.
Scalability potential: Low/Middle/High/Ultra need a single graph snapshot contract, likely vault-backed or scratch-lease-backed, before this can be honestly marked green.
Hardware Impact: No new runtime gain. Remaining cave graph arrays are cold rebuild inputs, but they remain real managed allocations and must not be hidden.

## Decision 062 - Cave DTO Layout Must Be Explicit

Problem: Cave graph DTOs enter `NativeArray<T>`, stackalloc spans, and voxel density jobs, but `CaveNode`, `CaveTunnel`, `CaveEntrance`, `CaveStructure`, `CaveGenerationParams`, and `CaveSpawnData` relied on sequential layout and stale size comments. `CaveTunnel` in particular had a 52-byte field sum before tail padding and therefore failed the explicit ARM64 size proof.
Solution: Convert the cave runtime DTOs to `LayoutKind.Explicit` with fixed `Size` and named `FieldOffset` maps. Add `VoxelMemorySovereigntyValidator1304` assertions for size and offsets so Unity Editor validation can fail closed on drift.
Rejected Alternatives: Leave sequential layout because the fields are blittable, or pad the managed snapshot arrays instead. Sequential layout is not a proof artifact; padding arrays would not fix NativeArray element stride or Burst ABI drift.
Scalability potential: Low/Middle/High/Ultra use identical cave DTO layout. Quality weight can scale cave generation cadence/detail, not DTO stride or save identity.
Hardware Impact: No measured frame-time gain. This removes ARM64 misalignment risk for cave graph NativeArrays and prevents hidden layout drift on Quest/mobile silicon.

## Decision 063 - Chthonic Collider Circle LUT Must Not Be A Managed Array

Problem: `HectonVoxelEngine` stored a static `float2[24]` unit-circle lookup for smooth chthonic pillar collider meshes. The data is scalar and read-only, so the managed array allocation was avoidable.
Solution: Replace the array with `GetChthonicPillarColliderUnitCircle(int index)`, a switch-backed value LUT returning `float2` structs. This keeps the deterministic one-dimensional lookup and avoids per-call trigonometry.
Rejected Alternatives: Compute `sin/cos` per segment, or keep the array because it is cold. Trig is unnecessary work for a fixed 24-segment fake collider; cold managed arrays still fail the current APEX heap scan.
Scalability potential: Low/Middle/High/Ultra keep the same 24-segment collider approximation. Quality weight can scale collider/visual cadence elsewhere, not this fixed seam collider route.
Hardware Impact: Removes one cold managed array allocation. No measured microsecond gain claimed; it is heap-boundary cleanup with unchanged visual approximation.

## Decision 064 - FixedList Count Drift Must Fail Closed

Problem: After managed arrays were replaced with `FixedList*Bytes<T>`, some side counters remained trusted as if they could never drift. `HectonVoxelVolume.TrackTerrainHoleHandle` and `UnregisterTerrainHoles` indexed by `_terrainHoleHandleCount`; if that count became corrupted above FixedList length, the code could throw a managed out-of-range exception instead of failing closed. `HectonVoxelEngine` air-pocket registration also used `_airPocketCount` as a validity gate while the real storage is `_airPocketEntries.Length`.
Solution: Add count-vs-length-vs-capacity guards before indexed iteration or `AddNoResize`. Corrupt air-pocket and terrain-hole registries now record a 1304 blackbox sample through `RecordVoxelRegistryCorruptionForAgent1304`, clear local scalar registry state, and return/degrade without managed exceptions. `TrySampleAirPocket` clamps read iteration without mutating the read accessor.
Rejected Alternatives: Trusting the counters because corruption should not happen, or adding managed exception handling around the loops. Counter trust is not fail-closed; managed catch/throw paths violate the hot-path failure mandate.
Scalability potential: Low/Middle/High/Ultra keep the same registry capacities and behavior. Quality weight does not change registry identity; it only controls cadence elsewhere.
Hardware Impact: Normal path cost is a few integer comparisons in registration/unregistration, estimated below 1 us and unmeasured. Failure path avoids managed exception construction and records a binary dump sample for post-mortem analysis.

## Decision 065 - Active Bounds Lane Must Not Desync From Managed Volume Lanes

Problem: Active volume object/component lanes are still managed reference lists, while local bounds are now a `FixedList4096Bytes<ActiveVolumeLocalBoundsEntry>`. If the FixedList were unexpectedly full, the previous code would still add object/component references and leave the bounds lane shorter than the identity lanes.
Solution: Guard `_activeVolumeLocalBounds.Length >= _activeVolumeLocalBounds.Capacity` before adding `GameObject` and `HectonVoxelVolume` references. The impossible-at-current-capacity branch now fails closed instead of creating a split registry.
Rejected Alternatives: Keep the post-add conditional, or migrate Unity object references into unmanaged storage. The first accepts silent lane drift; the second is invalid because UnityEngine.Object references are managed and require a dedicated descriptor bridge.
Scalability potential: Capacity remains 64 logical active volumes for all tiers. High/Ultra visual work still scales through mesh generation budgets, not active-volume identity layout.
Hardware Impact: Normal path cost is one integer comparison, estimated below 1 us and unmeasured. It prevents debug/gizmo and despawn registry corruption if capacity constants drift later.

## Decision 066 - ChunkAddress Must Not Expose Object Equality In The Hot Registry Path

Problem: `ChunkAddress` is the key for dirty and compacted chunk registries. It implemented `IEquatable<ChunkAddress>` but also exposed `Equals(object)`, keeping an object-based comparison route in a file under strict boxing scrutiny. Current registry lookups already call the typed overload directly.
Solution: Remove `Equals(object)` and leave `Equals(ChunkAddress)` plus `GetHashCode`. Post-scan shows no `Equals(object` or `object obj` hits in `VoxelDeltaProcessor.cs`.
Rejected Alternatives: Keep the override because it is conventional for value types. Convention is not enough under the current no-boxing audit, and the known registry path does not need it.
Scalability potential: Low/Middle/High/Ultra chunk lookup semantics are unchanged. Quality weight affects drain/compaction cadence, not key equality.
Hardware Impact: Normal path uses the same typed ulong compare. No measured microsecond gain; the change removes an avoidable object-comparison surface.

## Decision 067 - ThermalMeltEvent Is A Real AUP DTO

Problem: `ThermalMeltEvent` carries absolute-universe melt coordinates into the voxel delta processor but used implicit sequential layout. It contains a 24-byte `double3` plus legacy `Vector3` fallback and two floats, giving no explicit ARM64 stride proof.
Solution: Convert `ThermalMeltEvent` to explicit 48B layout with `double3` first, `Vector3` second, two float lanes, and a 4-byte pad. Add size/offset assertions to `ValidateAgent1304PrivateLayouts`.
Rejected Alternatives: Leave it sequential because it is currently a managed event/request struct, or drop the legacy `Vector3` lane. Sequential layout lacks a proof artifact; dropping the legacy lane changes upstream compatibility.
Scalability potential: Low/Middle/High/Ultra share the same event ABI. Quality weight can throttle melt/carve cadence but must not alter DTO layout or coordinate authority.
Hardware Impact: No measured runtime gain. The change removes ARM64 layout ambiguity for a spatial voxel request and keeps the authoritative coordinate in double precision.

## Decision 068 - MeshData Vertex Structs Are A Unity Stride Boundary

Problem: `VoxelSurfaceVertex` and `VoxelColliderVertex` remain `LayoutKind.Sequential`, and their natural strides are 76B and 12B. A naive 8-byte padding pass would satisfy the text mandate but break Unity `MeshData.SetVertexBufferParams`, which currently defines tight vertex streams matching those strides.
Solution: Do not pad these two Unity mesh upload structs in this loop. Record them as a Unity API boundary requiring either byte-level vertex writes or matching descriptor redesign before explicit 80B/16B layout can be safe.
Rejected Alternatives: Forcing `Size=80`/`Size=16` immediately. That would make `GetVertexData<T>` no longer match the vertex buffer descriptors and can corrupt mesh uploads.
Scalability potential: Low/Middle/High/Ultra visual mesh fidelity remains controlled by existing continuous budgets. Vertex stream layout is an API contract, not a quality tier switch.
Hardware Impact: No change. This avoids introducing a mesh corruption regression while preserving an explicit residual for a future dedicated mesh-stream route card.

## Decision 069 - VoxelSdfRaycastHit Is A NativeArray DTO

Problem: `VoxelSdfRaycastHit` is stored in `NativeArray<VoxelSdfRaycastHit>` by the SDF raymarch job, but it had implicit layout and a 33-byte field sum before tail padding. That is not an ARM64 proof artifact.
Solution: Convert it to `LayoutKind.Explicit, Size=40` with `Point@0`, `Normal@12`, `Distance@24`, `Density@28`, `Hit@32`, and named byte padding through offset 39. Add validator assertions in `VoxelMemorySovereigntyValidator1304`.
Rejected Alternatives: Leave it sequential because the fields are simple, or replace `Hit` with a bool for API readability. Sequential layout gives no byte map; runtime bool is rejected in DTOs.
Scalability potential: Low/Middle/High/Ultra use the same raycast result stride. Quality weight can throttle raymarch cadence, not DTO layout.
Hardware Impact: No measured microsecond gain. It removes ARM64 stride ambiguity for a Burst/native raycast result and prevents future hidden padding drift.

## Decision 070 - Pending Queue Corruption Must Not Throw Managed Exceptions

Problem: `VoxelDeltaProcessor` pending carve, compaction, and thermal melt lanes still use bounded managed arrays with mirror counters. If a counter or head drifted, loops and pop/enqueue paths could index out of range and throw a managed exception instead of failing closed.
Solution: Add queue-state validators before each indexed hot lane. Corruption writes `VoxelBlackBoxPendingQueueCorruptionFlag`, encodes queue/head/count/capacity into the blackbox focus field, clears the affected lane, and returns.
Rejected Alternatives: Wrap loops in managed try/catch, or claim the counters cannot corrupt. Catch blocks allocate/route through managed failure semantics; unguarded trust violates fail-closed policy.
Scalability potential: Low/Middle/High/Ultra keep the same bounded capacities. Quality weight scales enqueue/drain cadence elsewhere, not queue identity or authority.
Hardware Impact: Normal path adds bounded integer comparisons, estimated below 1 us and unmeasured. Failure path avoids managed exception construction and preserves a binary forensic sample.

## Decision 071 - Build Run Was Allowed Once, Still External-Walled

Problem: After multiple C# patches, syntax needed a compile signal, but the operator forbids frequent builds and forbids builds while CPU is above 50% or dotnet/csc is active.
Solution: Run one `dotnet build Hecton8.Core.csproj --no-restore --nologo` only after guard reported `cpu=43` and `dotnet_csc_count=0`. Stop at the first returned failure set and do not rerun.
Rejected Alternatives: Skip compiler proof entirely, or repeatedly build after every micro-patch. The first hides syntax risk; the second violates operator scheduling constraints.
Scalability potential: Build validation is offline and tier-independent.
Hardware Impact: 0 player runtime us. Build failed with 48 external errors outside the returned 1304 file set; voxel syntax was not flagged in the returned stream, but project compile is still not green.

## Decision 072 - Chunk State Pool Must Not Be A Managed Array

Problem: `VoxelDeltaProcessor` still allocated `ChunkDeltaState[DirtyChunkStatePoolCapacity]` for the dirty-chunk lease pool. The cell payloads were already vault-backed, but the pool metadata itself remained a managed heap array and had no explicit runtime layout proof.
Solution: Replace the pool array with three inline `FixedList4096Bytes<ChunkDeltaState>` banks and keep the existing `FixedList4096Bytes<int>` free stack. Add `TryAddChunkStatePoolSlot`, `TryGetChunkStatePoolSlot`, and `TrySetChunkStatePoolSlot`; corrupt slot/free-count state writes `VoxelBlackBoxChunkStatePoolCorruptionFlag`, clears the free stack, and fails closed. Convert `ChunkDeltaState` to explicit 32B and `CompactedChunkState` to explicit 24B, then validate offsets in `ValidateAgent1304PrivateLayouts`.
Rejected Alternatives: Keep the managed array because it is fixed size, move pool metadata into a new `NativeArray`, or compress the pool to two banks by shrinking slot types. Fixed managed array still fails the APEX heap scan; a new local native allocation would violate the DataVault ownership route; shrinking slot fields adds conversion risk for no measured gain.
Scalability potential: Low/Middle/High/Ultra keep the same 256 dirty-chunk capacity. `GlobalQualityWeight` still controls carve/compaction cadence; it does not alter DTO layout, pool identity, or save authority.
Hardware Impact: Removes one cold managed metadata array allocation and adds bounded integer bank selection on lease/release, estimated below 1 us and unmeasured. Failure path now records a binary blackbox sample instead of allowing out-of-range managed exception flow.

## Decision 073 - One Compile Signal After Pool Layout Change

Problem: Loop 26 changed `VoxelDeltaProcessor` storage layout and private DTO layout. Text and brace checks were not enough to catch `FixedList4096Bytes<T>` generic constraints or explicit-layout compiler failures.
Solution: Wait for the build guard to clear (`cpu=8`, `dotnet_csc_count=0`) and run exactly one `dotnet build Hecton8.Core.csproj --no-restore --nologo`. The returned stream failed in external domains and did not report errors in the 1304 modified voxel files.
Rejected Alternatives: Running builds while CPU was 62, or skipping compiler signal entirely after a structural C# change. The first violates AGENTS.md and operator instruction; the second hides syntax risk.
Scalability potential: Build validation is offline and tier-independent.
Hardware Impact: 0 player runtime us. Project compile remains blocked by external domains; this loop did not create a returned compiler error in the modified voxel files.

## Decision 074 - Chunk Registries Must Not Hide Managed Key/Value Arrays

Problem: `VoxelDeltaProcessor.FixedChunkRegistry<T>` was a managed class with managed `ChunkAddress[]` and `T[]` storage. It backed dirty chunk state, compacted state, and write-version state, so the loop 26 pool cleanup still left a real heap-backed registry route in the same runtime authority path.
Solution: Convert `FixedChunkRegistry<T>` to an inline unmanaged-constrained struct and store keys/values/occupancy as SoA `FixedList4096Bytes` banks. The owner now holds the registry inline; lazy initialization fills exactly `InitialChunkRegistryCapacity` logical slots and fails closed if bank capacity is ever insufficient. Dirty/compacted/write-version public API stayed unchanged.
Rejected Alternatives: Keep arrays because capacity is fixed, use `NativeArray` for the registries, or create three fully separate typed registries. Fixed arrays remain heap allocations; local `NativeArray` ownership would add a non-vault persistent native allocation; three duplicated registries increase code surface without changing the storage contract.
Scalability potential: Low/Middle/High/Ultra keep the same 256 chunk registry capacity. `GlobalQualityWeight` still scales carve/compaction cadence and write budgets, not registry identity, layout, or save authority.
Hardware Impact: Removes one managed registry object allocation plus six managed backing arrays across the three registry instances. Normal lookup remains O(256) linear scan as before; no measured microsecond gain claimed.

## Decision 075 - Loop 27 Uses Static Verification Only

Problem: The operator explicitly ordered rare dotnet/build usage, and loop 26 already consumed one controlled compile attempt after structural layout changes. Loop 27 touched storage internals but did not change public signatures or DTO offsets.
Solution: Run brace balance, removed-pattern scan, hot-token scan, prompt hash extraction, and `git diff --check`; do not run another `dotnet build` in this loop. Mark compile proof as `PENDING VERIFICATION`.
Rejected Alternatives: Launch another build immediately, or claim compile health from static text. Repeated builds violate the operator constraint; static scans are source evidence only.
Scalability potential: Verification choice is offline and tier-independent.
Hardware Impact: 0 player runtime us. No profiler or player capture was produced.

## Decision 076 - Marching Cubes Tables Belong To DataVault

Problem: `MCTables` owned `_edgeTable` and `_triTable` as static persistent `NativeArray<int>` buffers. These lookup tables were read by Marching Cubes jobs but had no GlobalDataVault descriptor, no relocation pin, and no explicit owner route.
Solution: Add `BufferID.VoxelMarchingCubesEdgeTable = 644` and `BufferID.VoxelMarchingCubesTriTable = 645`, publish both tables through `IDataVault.EnsureGenerationHandle<int>`, write them under `SystemID.TerrainSeams`, and expose them to jobs only through `MCTables.JobTableLease`. The lease locks both buffers before scheduling and unlocks in `Dispose`.
Rejected Alternatives: Keeping static persistent arrays because the data is read-only, rebuilding the tables per job, or storing managed arrays. Static persistent arrays violate DataVault ownership; per-job rebuild is wasted CPU; managed arrays fail the Zero-GC boundary.
Scalability potential: Low/Middle/High/Ultra share the same 256/4096 integer LUTs. Quality weight can scale meshing cadence and chunk count, not MC table identity or layout.
Hardware Impact: Removes two persistent unmanaged aliases outside the vault. Normal path adds two DataVault read locks per MC phase; measured microseconds unavailable. Failure gain is preventing table relocation/use-after-free ambiguity during defrag.

## Decision 077 - Streaming Scratch Growth Must Fail Before Managed Exception Flow

Problem: `TryEnsureStreamingScratchSlotCapacity` relied on downstream allocation/growth and previously handled overflow through a managed exception/log route. Extreme or corrupt dimensions could reach scratch growth before a bounded fail-closed decision.
Solution: Add hard capacity guards for grid dimension, height points, total points, total cells, edge vertex scratch, raw mesh scratch, and spawn-point scratch before `EnsureStreamingScratchSlotCapacity` is called. Invalid requests now report the existing voxel mesh scratch overflow flag and return false.
Rejected Alternatives: Keeping `catch (Exception ex)`, or silently clamping all requested dimensions. Managed exception flow violates hot failure policy; silent clamp can corrupt mesh topology and collider/nav consistency.
Scalability potential: Low uses smaller quality-resolved raw mesh scratch; Middle/High/Ultra can spend higher scratch ceilings through continuous quality capacity, but all tiers share the same maximum safety envelope.
Hardware Impact: Normal path adds integer range checks, estimated below 1 us and unmeasured. Failure path avoids managed exception construction and uncontrolled native scratch growth.

## Decision 078 - Do Not Hide Remaining Runtime Boundaries

Problem: After Loop 28, target scans still show managed arrays/lists in voxel runtime, a generic local native scratch allocation route, core fatal exception constructors, and one `catch (Exception ex)` in `HectonVoxelVolume`. Claiming release-grade Zero-GC would be false.
Solution: Record the residuals explicitly and keep status as pending verification. Loop 28 only removes the MC table persistent native aliases and hardens streaming scratch capacity validation.
Rejected Alternatives: Suppressing the scanner output, calling cold Unity reference pools acceptable without a route card, or claiming that static read-only data has no ownership cost.
Scalability potential: Low/Middle/High/Ultra still need a larger descriptor bridge for cave graph snapshots, Unity object/mesh pools, pending requests, and streaming scratch slot ownership.
Hardware Impact: No measured frame gain claimed. Residual managed boundaries remain a real release blocker until routed or documented as Unity API reference lanes.

## Decision 079 - Loop 28 Avoids Another Build

Problem: The operator explicitly ordered rare dotnet/build usage, and recent controlled builds already failed in external domains. Loop 28 touched source-level ownership and scratch guards, but the user specifically warned not to launch dotnet/build every pass.
Solution: Run prompt extraction, removed-pattern scans, runtime token scans, brace balance, and `git diff --check`; do not run `dotnet build` in Loop 28.
Rejected Alternatives: Launching another build immediately, or reporting compile success from static scans. Repeated builds violate the instruction; static checks are not compiler proof.
Scalability potential: Verification scheduling is offline and does not alter runtime tier behavior.
Hardware Impact: 0 player runtime us. Compile proof remains pending and project-wide build health remains blocked by known external compile walls.

## Decision 080 - Rebuild Loop Must Not Log Managed Exceptions

Problem: `HectonVoxelVolume.ProcessQueuedRebuildsAsync` caught `Exception ex` and routed it to `H8Debug.LogException`. That contradicts the fail-closed mandate and left a scanner-visible managed exception/log branch in the voxel rebuild path.
Solution: Remove the catch block. The `finally` block now detects a stuck `Baking` state for the same runtime stamp, marks the volume `Pending`, requeues it, and writes a 1304 mesh-pipeline blackbox sample through `HectonVoxelEngine.RecordVoxelRebuildFailClosedForAgent1304`.
Rejected Alternatives: Keeping the catch as editor-only, or replacing it with another typed managed catch. Editor-only still leaves a managed exception route; typed catches hide the same failure model under a narrower name.
Scalability potential: Low/Middle/High/Ultra all fail closed to the same pending rebuild state. Quality weight changes rebuild cadence/capacity elsewhere, not failure semantics.
Hardware Impact: Normal path is unchanged. Failure path removes managed exception logging allocation and records a fixed binary sample instead; measured microseconds unavailable.

## Decision 081 - Mesh Pool Warmup Cancellation Must Be Boolean

Problem: Cold mesh pool warmup caught `OperationCanceledException` and generic `Exception exception`, and inner warm/acquire loops used `ct.ThrowIfCancellationRequested()`. This left managed exception handling and editor log routing in the voxel mesh pool lane.
Solution: Replace cancellation throws in mesh pool warmup/acquire loops with `ct.IsCancellationRequested` checks and fail-closed returns. Remove both catch blocks and keep only `finally` for `_voxelMeshPoolWarmupRunning` teardown.
Rejected Alternatives: Keep `OperationCanceledException` because it is conventional, or log generic warmup failures in editor. Conventional cancellation is still managed exception flow; generic editor logging is not a binary blackbox artifact.
Scalability potential: Low/Middle/High/Ultra preserve the same staggered mesh pool prewarm. Quality weight does not change pool identity or failure behavior.
Hardware Impact: Normal path saves no claimed frame time. Cancellation/failure path avoids exception construction and stack logging; measured proof unavailable.
