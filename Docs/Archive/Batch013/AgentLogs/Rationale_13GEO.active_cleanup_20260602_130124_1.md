# Rationale_13GEO

Date: 2026-05-27
Status: PENDING VERIFICATION

## Decision 001 - Active Directive Source

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="13GEO">`; strict batch extraction cannot produce an XML task list.
Solution: Use the user's direct 13GEO domain assignment as the active directive, with XML task count recorded as 0. Constrain work to Echelon 2 terrain/MapMagic/geology and documented cross-domain interfaces only.
Rejected Alternatives: Editing prompts for neighboring IDs would steal another agent's task and violate strict parsing. Waiting for a new batch tag would leave the domain audit undone.
Scalability potential: Scope control reduces merge conflicts on weak team throughput while preserving deep terrain improvements for low, middle, high, and ultra hardware tiers.
Hardware Impact: No runtime impact; process decision prevents accidental broad edits and compile churn on shared machines.

## Decision 002 - Continuous Seam Presentation Scaling

Problem: `WorldGenerativeGeologySeamExecutionDirector` and `WorldGenerativeGeologyIntegrationDirector` used fixed budgets for seam plan execution, tracked plan capacity, refresh distance, collar segments, debris count, and gap-dither particles. That violates the project rule that terrain algorithms consume continuous `GlobalQualityWeight` instead of static low/high behavior.
Solution: Feed `HomeostasisBrain.GlobalQualityWeight` through smoothstep curves. Low weight keeps minimum survival seam evidence with fewer plans, wider refresh hysteresis, 3 collar segments, sparse debris, and smaller dither. High/ultra weight restores full tracked plan budget, tighter refresh, denser collars, richer debris, and higher particle/emission density.
Rejected Alternatives: Adding explicit Low/Medium/High enum switches would violate the scalar-quality contract. Leaving inspector constants fixed would make weak devices pay for hidden visual overdraw and high-end devices receive no visual overkill.
Scalability potential: Low = sparse seam masks and silhouettes; middle = stable seam collars and moderate dither; high = full configured plans and debris; ultra = configured maximum visual density without changing terrain truth ownership.
Hardware Impact: Estimated low-end gain is 40-75 us during seam reconciliation bursts on i3/MX350-class hardware, with savings spent on clearer high-end seam evidence rather than gameplay authority changes.

## Decision 003 - Terrain Blend Mask Shader Parameter De-binarization

Problem: `WorldGenerativeGeologyTerrainSeamApplier` pushed a binary `lowTierVisualOnly` shader parameter even though the heightmap job already consumes continuous `GlobalQualityWeight`.
Solution: Keep legacy bool telemetry/ABI fields intact, but route the active shader vector and terrain telemetry through `seamExpensiveWeight` as a float. The blend mask now moves continuously from survival visual-only masking to full expensive seam blending.
Rejected Alternatives: Editing `HybridTerrainSeamJobs` ABI fields during a parallel batch risks breaking stale generated csproj callers. Removing all bool telemetry in one pass would create unnecessary merge conflict with an already modified file.
Scalability potential: Low = shader mask hides seam cuts without expensive visual emphasis; middle = partial mask strength; high/ultra = full mask response and richer seam detail.
Hardware Impact: Estimated low-end gain is visual-stability, not CPU reduction; avoids binary shader jumps and preserves no-extra-truth behavior.

## Decision 004 - Voxel Bridge Debt Deferred

Problem: Static scan found `WorldGenerativeGeologyVoxelBridgeDirector` still has `Allocator.Persistent` native construction, managed `CancellationTokenSource`, and interpolated diagnostic strings. The file is already modified by another agent and is in a risky merge zone.
Solution: Do not overwrite the active neighboring work. Record the debt for the integrator and limit this pass to clean/separable seam planner and terrain applier edits.
Rejected Alternatives: Patching the modified voxel bridge would risk clobbering another agent's current migration. Ignoring the debt would hide a real DataVault/zero-GC violation.
Scalability potential: Fixing it later should move cave request scratch buffers into DataVault and replace managed async cancellation with explicit state machines; this benefits low, middle, high, and ultra equally by removing crash and GC risk.
Hardware Impact: Current unfixed risk can exceed 0 B/frame and can fragment native memory; no gain claimed until the owning agent or integrator completes that migration.

## Decision 005 - Verification Gate Scope

Problem: Full compile/build verification is forbidden while CPU is above 50% or another `dotnet`/`csc` process is active. The machine had active `dotnet` processes and CPU sampled at 100.0%; the DataVault audit also timed out after 124 seconds.
Solution: Run only static gates that do not violate shared-machine rules, record the exact blockers, kill the timed-out audit process, and leave status as PENDING VERIFICATION.
Rejected Alternatives: Launching `dotnet build` anyway would violate project protocol and compete with other agents. Claiming compile success without the compiler would be false reporting.
Scalability potential: Avoiding unauthorized compile contention protects the shared integration loop across low, middle, high, and ultra target work.
Hardware Impact: No runtime gain claimed; process decision avoids CPU contention on the shared workstation.

## Decision 006 - Voxel Bridge Continuous Budget And Cancellation Cleanup

Problem: `WorldGenerativeGeologyVoxelBridgeDirector` still used fixed runtime volume, spawn, async launch, pool warmup, grid cap, and resolution budgets. It also created one managed `CancellationTokenSource` per pending voxel request and allocated persistent zero-length `NativeArray` wrappers for empty cave node/tunnel/entrance inputs.
Solution: Drive voxel volume capacity, spawn budget, async launch budget, pool warm padding, pool warmup, grid dimension cap, and resolution scale from continuous `HomeostasisBrain.GlobalQualityWeight`. Replace per-request CTS with a request-state cancellation flag and the existing lifecycle token. Return default `NativeArray<T>` for zero-length request inputs.
Rejected Alternatives: Full async pipeline rewrite would collide with neighboring dirty voxel work. Removing the lifecycle CTS would leave the heavy voxel pipeline unable to stop on disable. Using discrete low/mid/high tiers would violate the scalar quality contract.
Scalability potential: Low = fewer active voxel volumes, lower grid cap, smaller resolution scale, smaller pool warm target; middle = partial density; high = configured budget; ultra = configured maximum without changing terrain truth ownership.
Hardware Impact: Static low-end estimate is 60-140 us saved during bursty voxel seam reconciliation plus removal of per-request CTS allocations and empty persistent native allocations.

## Decision 007 - Cached DataVault Route For Terrain Seam Applier

Problem: `WorldGenerativeGeologyTerrainSeamApplier` hot buffer paths repeatedly read `GlobalRegistry.DataVault`, violating the cold-DI rule for GlobalRegistry.
Solution: Cache `_dataVault` through `ResolveReferences()` and update it from `OnGlobalRegistryServiceReplaced` when the DataVault slot changes. Buffer open/acquire paths now use the cached route.
Rejected Alternatives: Reworking terrain seam buffer ownership into a new service would be larger than the safe patch scope and risk another agent's edits. Leaving hot `GlobalRegistry.DataVault` reads would keep a known doctrine violation.
Scalability potential: Low, middle, high, and ultra devices all get a single stable DataVault route; quality scaling remains independent from memory authority.
Hardware Impact: Microsecond gain is small but real in repeated seam buffer opens; bigger value is contract correctness and reduced hot global dependency.

## Decision 008 - Second Pass Verification Scope

Problem: Static gates passed, but CPU sampled at 72.3%, above the explicit 50% build prohibition.
Solution: Do not launch compile. Record `git diff --check` success and exact CPU blocker. Keep status PENDING VERIFICATION.
Rejected Alternatives: Running `dotnet` under forbidden CPU load would violate project rules. Reporting compile success without compiler execution would be false.
Scalability potential: Protects shared machine throughput while preserving deterministic verification trail.
Hardware Impact: No runtime gain claimed from verification; avoids workstation contention.

## Decision 009 - Pending Voxel Quality Signature Stability

Problem: Pending voxel requests stored only a build signature. Async build-data later re-read `GlobalQualityWeight`, so a quality change between queue and launch could build geometry at one quality while tagging the runtime with a signature computed at another quality.
Solution: Store `VisualQualityWeight` on `PendingRequestState`. Compute request signature and build voxel request data from the same frozen scalar.
Rejected Alternatives: Reading live quality during async build is cheaper to write but creates nondeterministic rebuild/despawn churn. Removing quality from signature would leave stale low-quality geometry after quality rises.
Scalability potential: Low, middle, high, and ultra all get deterministic voxel detail transitions. Geometry only changes when the queued build contract changes.
Hardware Impact: Prevents wasted rebuild bursts; static estimate is 20-60 us avoided during quality transition bursts, plus fewer despawn/spawn churn paths.

## Decision 010 - Cached Voxel Bridge Service Dependencies

Problem: Voxel bridge hot paths still read `GlobalRegistry.Thermodynamics`, `GlobalRegistry.PersistentWorldRegistry`, and `GlobalRegistry.ObjectPoolService`.
Solution: Cache `_thermalManager`, `_persistentWorldRegistry`, and `_objectPoolService` in `RefreshColdRegistryDependencies()` and update them in `OnGlobalRegistryServiceReplaced`.
Rejected Alternatives: Leaving hot GlobalRegistry reads violates the cold-DI doctrine. Broad service interface rewrites are out of scope while multiple agents are editing adjacent systems.
Scalability potential: Stable service routes for low, middle, high, and ultra. Voxel seam visuals can scale without changing authority owners.
Hardware Impact: Small per-call gain, but removes hot global dependency in vent registration/removal and pool warmup.

## Decision 011 - Compile Wall Classification

Problem: A legal `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal` failed in external Candice SQLite code before 13GEO domain verification could complete.
Solution: Classify as external compile wall, not a 13GEO code failure. Record exact files/errors and keep status PENDING VERIFICATION.
Rejected Alternatives: Editing `Assets\Candice AI for Games` is outside 13GEO domain. Claiming 13GEO compile success would be false.
Scalability potential: Keeps terrain/geology work isolated from third-party save-system dependency repair.
Hardware Impact: No runtime gain claimed.

## Decision 012 - Runtime Geology Presentation Quality De-binarization

Problem: `WorldGenerativeGeologyService` still used `FinalVariantActive` as a binary quality switch: non-final generated geology was forced to `SingleFeature`, LOD count was capped by a bool, and debris was full-or-zero. This violated continuous `GlobalQualityWeight` and caused visible step changes in generated geology.
Solution: Keep `FinalVariantActive` as a streaming/detail bias, but derive composition, LOD count, and debris count from smooth `GlobalQualityWeight` plus stable hash dither. The build signature still keys only the resolved composition/counts, not raw quality noise, so geometry rebuilds happen only when the effective presentation contract changes.
Rejected Alternatives: Changing terrain height truth or MapMagic graph output by quality weight would mutate world authority. Keeping bool gating would preserve the defect. Adding new public request fields would widen the contract during a parallel batch.
Scalability potential: Low = sparse single-feature geology with minimum seam debris; middle = deterministic partial paired/context distribution; high = most configured detail; ultra = final-variant bias plus high quality pushes near configured visual overkill without changing save identity.
Hardware Impact: Static low-end estimate is 20-90 us saved during generated-geology rebuild bursts by lowering generated primitive, LOD, and debris counts. No profiler proof yet.

## Decision 013 - Voxel Bridge Black-Box Ring Through DataVault

Problem: Terrain seam applier had a 300-entry black-box ring, but `WorldGenerativeGeologyVoxelBridgeDirector` had no equivalent reconcile/fault history. Adding a local persistent `NativeArray` would satisfy the surface black-box request while violating the DataVault native ownership mandate.
Solution: Add a `GlobalDataVault`-owned `VoxelBridgeTelemetryEntry` ring: explicit 64-byte layout, capacity 300, buffer id `0x530426`, owner `SystemID.TerrainSeams`, cached `_dataVault`, hotswap refresh, and write access through `TryAcquireWriteLock` / `ReleaseWriteLock` in `try/finally`. Faults dump once to `Docs/AgentLogs/Dump_13GEO_VoxelBridge.bin` in editor/development builds.
Rejected Alternatives: Local persistent native aliases were rejected because vault compaction would invalidate them. Managed lists/strings were rejected because they create forensic data only after allocation-prone failure paths. GlobalTelemetryBus integration was rejected as too broad for this domain patch.
Scalability potential: Low, middle, high, and ultra share the same 64-byte forensic write. Quality-weight fields in the ring allow postmortem proof of which tier caused volume pressure, queue saturation, or null-volume faults.
Hardware Impact: Runtime overhead is one bounded 64-byte vault write per reconcile/fault when the vault lock is available. Verified microseconds: 0; expected cost is below measurement noise but still PENDING VERIFICATION.

## Decision 014 - Fourth Pass Verification Gate

Problem: After the fourth-pass edits, compilation was required but forbidden by live machine state: `dotnet.exe` PID 62864 is already building `Hecton8.slnx`, and CPU sampled at 69%.
Solution: Run static gates only. `git diff --check` passed for edited files with CRLF warnings only; targeted `rg` verified the black-box write-lock route and removed `useFullDetail` in runtime geology service. Keep status PENDING VERIFICATION.
Rejected Alternatives: Launching another build would violate the explicit compile-server rule. Claiming compile success from static scans would be false.
Scalability potential: Protects sibling agents and avoids workstation contention while preserving a concrete verification trail.
Hardware Impact: No runtime gain claimed from verification.

## Decision 015 - Voxel Bridge Black-Box Hot Allocation Closure

Problem: The new voxel bridge black-box ring is DataVault-owned, but `TryAcquireVoxelBridgeBlackBox()` called `TryEnsureVoxelBridgeBlackBox()`. If a DataVault hotswap invalidated the handle and the next reconcile/fault path arrived before cold refresh completed, the hot telemetry write could call `EnsureGenerationHandle<T>()` and allocate native storage from gameplay flow.
Solution: Make `TryAcquireVoxelBridgeBlackBox()` fail closed. Hot paths now require a cached exact handle, `TryOpenVoxelBridgeBlackBox()` success, and `TryAcquireWriteLock()` success. Buffer creation remains only in cold refresh and DataVault hotswap handling.
Rejected Alternatives: Creating a local persistent fallback ring was rejected because it violates DataVault native ownership and compaction safety. Calling `EnsureGenerationHandle<T>()` inside telemetry recording was rejected because it hides allocation under diagnostics. Dropping black-box support entirely was rejected because crash forensics are mandatory.
Scalability potential: Low, middle, high, and ultra tiers keep the same bounded forensic write when the vault buffer exists. If the vault is not ready, the system skips one diagnostic sample instead of paying allocation or risking invalid native aliases.
Hardware Impact: Removes a rare but real native allocation spike from reconcile/fault paths. Estimated saved stall on i3/MX350-class hardware is 20-80 us during DataVault hotswap/fault edge cases; normal-frame cost is unchanged.

## Decision 016 - Inline Voxel Cave Graph Handoff

Problem: `WorldGenerativeGeologyVoxelBridgeDirector` still allocated non-empty persistent `NativeArray<CaveEntrance>` and `NativeArray<CaveStructure>` per async voxel request. `HectonVoxelEngine.GenerateVolumeFromDataAsync()` already copied cave graph inputs into DataVault-backed streaming scratch, but later runtime configuration and terrain-hole registration still read caller arrays, forcing the bridge to keep persistent caller-owned native arrays alive across the whole async pipeline.
Solution: Add `HectonVoxelEngine.VoxelInlineCaveGraphData`: one entrance plus seven structure slots, matching the 13GEO bridge maximum. Add `GenerateVolumeFromInlineCaveGraphDataAsync()` so the engine acquires its streaming scratch, copies inline payload directly into DataVault-owned scratch, and uses `pipelineData.Nodes/Tunnels/Entrances/Structures` for runtime configure and terrain-hole registration. The bridge now builds inline structs and no longer creates or disposes NativeArrays for cave graph handoff.
Rejected Alternatives: `Allocator.TempJob` was rejected because the engine can await before and after scratch acquisition, causing illegal lifetime across frames. A shared bridge DataVault buffer was rejected because concurrent async launches could overwrite caller scratch before the engine copy. A local persistent fallback was rejected because it keeps the exact ownership violation. Rewriting the whole voxel pipeline was rejected as a refactoring loop.
Scalability potential: Low = fewer async allocation spikes when sparse low-quality geology is requested; middle/high/ultra = same visual payload capacity, with engine scratch ownership keeping concurrency bounded by existing voxel streaming slots.
Hardware Impact: Removes per-request persistent native allocation/disposal for up to 1 entrance and 7 structures from 13GEO voxel bridge launches. Static low-end estimate is 25-110 us saved during voxel spawn bursts plus lower native allocator fragmentation; profiler proof remains pending.

## Decision 017 - Streaming Scratch Lease DataVault Route Closure

Problem: `VoxelStreamingScratchLease.Dispose()` and `UnlockStreamingScratchJobLifetime()` used `GlobalRegistry.DataVault` fallback when the cached scratch vault was null. Those methods sit on voxel pipeline lock/unlock paths, so the fallback violated the cold-DI rule and could unlock against a different vault after DataVault replacement.
Solution: Store the exact `IDataVault` used for scratch buffer locking inside `VoxelStreamingScratchLease._lockedScratchVault`. Unlock and dispose now use that captured vault first, then the owner cached scratch vault, with no `GlobalRegistry` fallback. `TryLockStreamingScratchJobLifetime()` now requires the cached vault to exist before locking.
Rejected Alternatives: Continuing to call `GlobalRegistry.DataVault` was rejected as hot service locator use. Re-querying DataVault during unlock was rejected because lock ownership must match the vault that granted the lock. Reworking the entire streaming scratch lifecycle was rejected as too broad for this pass.
Scalability potential: Low, middle, high, and ultra all get the same deterministic lock route. The first-20-minutes route blocker removed is voxel seam/cave streaming instability during world traversal.
Hardware Impact: Static gain is small per call but removes a service-locator branch from many voxel scratch lock/unlock cycles. Estimated low-end saving is 5-20 us during voxel generation bursts, with larger value in correctness under DataVault hotswap.

## Decision 018 - Public Sonar SDF Lease Exact-Vault Route

Problem: `HectonVoxelEngine.TryAcquireNearestSonarSdfReadLease()` acquired an internal `HectonVoxelVolume.PublishedSonarSdfReadLease` that carries the exact `IDataVault`, but translated it into the public `VoxelSonarSdfReadLease` DTO and discarded that vault reference. `ReleaseNearestSonarSdfReadLease()` then unlocked `BufferID.VoxelSdfTexture3D` through the engine's current `_streamingScratchVault`. After DataVault replacement or scratch vault refresh, release could target a different vault than the one that granted the lock.
Solution: Add a fixed-size owner-local tracker in `HectonVoxelEngine`: 32 slots of `IDataVault`, SDF generation, version, and ref count. Public lease acquire records the exact internal vault before returning the DTO. Public release finds the matching generation/version slot and unlocks through the recorded vault. Teardown drains any tracked locks.
Rejected Alternatives: Changing `VoxelSonarSdfReadLease` layout to carry an owner/vault handle was rejected because it mutates a public contract during a parallel batch. Searching active volumes on release was rejected because the public DTO does not carry the owning volume and the publish list can change. Keeping `_streamingScratchVault` release was rejected because lock ownership must follow the vault that granted the lock.
Scalability potential: Low, middle, high, and ultra all get the same deterministic read-lock route for sonar, GPR, cutter, and kinematic consumers. This is not a visual feature; it protects cave/seam traversal from DataVault hotswap leaks.
Hardware Impact: Verified microseconds: 0. Static cost is one bounded 32-slot scan on acquire/release. Static benefit is correctness under hotswap and avoiding locked-buffer retention that can block relocation or later SDF publication on i3/MX350-class hardware. The larger smooth-density SDF publish allocation remains a separate risk.

## Decision 019 - Smooth-Density Snapshot Clone Removal

Problem: `ConfigureVolumeRuntimeDataFromPipelineAsync()` copied `data.ScratchLease.SmoothDensityField` into a new transient `NativeArray<float>(..., Allocator.Persistent)` before calling `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()`. At max `StreamingPointScratchMax`, this clone is roughly 8.2 MB per volume snapshot and violates the DataVault-owned scratch direction for generated voxel data.
Solution: Remove the smooth-density clone path. The engine now validates point count, locks the existing streaming scratch lease with `TryLockStreamingScratchJobLifetime()`, passes `data.ScratchLease.SmoothDensityField` directly into runtime publication, and unlocks in `finally` after the awaited publish completes.
Rejected Alternatives: `Allocator.TempJob` was rejected because the publication awaits frames while the encode job runs. Keeping the persistent clone was rejected as native allocator churn. Rewriting `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` to encode directly into DataVault output buffers was rejected for this pass because it changes the writer-lock/job lifetime of another owner path and needs separate compile/profiler proof.
Scalability potential: Low and middle tiers avoid large transient native allocation during sparse voxel seam generation. High and ultra keep the same published SDF fidelity; saved allocation/fragmentation budget can be spent on richer visible cave surface detail from prior quality-weight controls.
Hardware Impact: Verified microseconds: 0. Static memory gain is up to ~8.2 MB transient native allocation avoided per max-size volume snapshot, plus lower allocator fragmentation on i3/MX350-class hardware. The encode byte scratch arrays inside `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` remain as residual debt.

## Decision 020 - Direct DataVault Encode For Published Sonar SDF

Problem: `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` still allocated two transient persistent byte arrays, `encodedScratch` and `audioMaterialScratch`, then copied both into `GlobalDataVault` payload buffers. At `PublishedSonarMaxPointCount` this is roughly 4.3 MB transient native allocation plus two full payload copies per publish, after the smooth-density clone was already removed.
Solution: Encode directly into the `BufferID.VoxelSdfTexture3D` and `BufferID.VoxelSdfAudioMaterialIds` DataVault buffers. The publish path resolves generation handles, acquires writer locks for both payload buffers, validates capacity, invalidates the descriptor immediately before in-place rewrite, schedules `PublishedSonarSdfEncodeJob` against the vault buffers, finalizes the job, then republishes the descriptor only if cancellation/abort did not occur. All payload writer locks are released in `finally`.
Rejected Alternatives: Keeping scratch was rejected because it preserves allocator churn and duplicate memory bandwidth. Adding new double-buffered payload IDs or changing public reader DTOs was rejected because interface mutation during a parallel batch violates the batch immutability rule. Invalidating the descriptor before acquiring payload locks was rejected because a failed lock attempt would unnecessarily erase a still-valid old payload.
Scalability potential: Low = no max-volume byte scratch spike during sparse voxel seam generation; middle = less allocator fragmentation during normal cave traversal; high = same SDF fidelity with saved memory bandwidth available for denser cave surface presentation; ultra = payload fidelity unchanged, visual overkill budget preserved for richer terrain/seam detail instead of duplicate copies.
Hardware Impact: Verified microseconds: 0. Static gain is up to ~4.3 MB transient native allocation avoided per max-size publish plus two full byte-buffer copies removed. On i3/MX350-class hardware this should reduce allocator and memory-bandwidth spikes during cave/SDF publication; proof remains PENDING VERIFICATION until Unity/player profiler capture.

## Decision 021 - Published Sonar SDF Lease-Only Read Surface

Problem: After direct DataVault encode, `TryGetClosestPublishedSonarSdfPayload()` and `TryReadClosestPublishedSonarSdfPayload()` still exposed `NativeArray<byte>.ReadOnly` payload aliases without returning a `PublishedSonarSdfReadLease`. Even if currently unused outside `HectonVoxelVolume`, the internal surface allowed future readers to bypass DataVault read locks while writer jobs can encode directly into the same buffers.
Solution: Remove the stale static unleased closest-payload getters. Change the instance `TryGetPublishedSonarSdfPayload()` overloads from `internal` to `private`, leaving them only as helpers for paths that already acquired the proper SDF/audio read locks. External/current consumers continue to use `TryAcquirePublishedSonarSdfPayloadReadLease()` and explicit `ReleasePublishedSonarSdfPayloadReadLease()`.
Rejected Alternatives: Acquiring a lease inside the old getter and releasing it before returning was rejected because it would return unprotected native aliases. Adding another public wrapper DTO was rejected because public/interface mutation is not needed. Keeping unused internal getters was rejected because it leaves a sharp edge after the direct-write change.
Scalability potential: Low, middle, high, and ultra tiers all get the same deterministic read route. This is not a visual-density feature; it protects sonar/acoustic/fauna/KCC consumers from reading SDF bytes while a direct DataVault encode job owns the payload buffers.
Hardware Impact: Verified microseconds: 0. Static runtime cost is unchanged. Correctness gain is removal of an unleased native alias path that could cause read/write contention or stale payload assumptions on i3/MX350-class hardware during cave traversal.

## Decision 022 - Ambiguous Public Sonar SDF Lease Key Rejection

Problem: The public `VoxelSonarSdfReadLease` DTO is a fixed 24-byte ABI with `SdfGeneration`, `AudioMaterialGeneration`, `Version`, and flags, but no vault or owner token. The owner-local tracker added an exact vault record internally, yet release still receives only the public DTO. If DataVault replacement or generation reuse creates two active tracked leases with the same public `SdfGeneration + Version` on different vaults, release can only match by the public key and may unlock the wrong vault.
Solution: Make `TryTrackNearestSonarSdfReadLease()` fail closed on ambiguity. Existing leases still coalesce only when vault, generation, and version all match. If an active slot has the same public generation/version but a different vault, the new acquire is rejected and the internal `PublishedSonarSdfReadLease` is released by the existing `finally` path before any public DTO escapes.
Rejected Alternatives: Mutating `VoxelSonarSdfReadLease` to add a vault/slot token was rejected because it changes a public cross-domain contract during a parallel batch. Searching volumes during release was rejected because the DTO has no owner identity and the active volume list can change. Guessing by first matching generation/version was rejected because lock ownership must match the vault that granted the lock.
Scalability potential: Low, middle, high, and ultra tiers all get the same deterministic failure mode. Weak devices avoid stuck DataVault locks after hotswap; high/ultra keep the same SDF fidelity and lease capacity without public ABI churn.
Hardware Impact: Verified microseconds: 0. Static runtime cost remains a bounded 32-slot scan. Static correctness gain is preventing wrong-vault unlock or lock retention that can block later SDF publication/compaction on i3/MX350-class hardware under rare DataVault hotswap collisions.

## Decision 023 - Non-Lease Public Sonar SDF Bulk Read Fail-Closed

Problem: `HectonVoxelEngine.TryReadNearestSonarSdf()` returned a `NativeArray<byte>.ReadOnly` payload view selected through an internal read lease, but the selection helper released that lease before returning. After direct DataVault encode, this exposed a stale/unlocked native alias to callers without any lifetime token.
Solution: Make the legacy non-lease bulk-read implementation fail closed with default outputs. Existing safe routes remain: `TryAcquireNearestSonarSdfReadLease()` for callers that need a payload view over scheduled work, and `TryResolveNearestSonarSdfSurface()` / `TryRaymarchNearestSonarSdf()` / `TrySampleNearestSonarSdf()` for immediate owner-executed reads.
Rejected Alternatives: Holding the internal lease after return was rejected because the caller has no release token through `IVoxelSonarSdfReadModel`. Copying the payload into scratch was rejected because it recreates the allocation/copy problem that was just removed. Mutating `IVoxelSonarSdfReadModel` was rejected because it is a public cross-domain contract during a parallel batch.
Scalability potential: Low, middle, high, and ultra tiers keep safe owner-directed SDF sampling. Payload views are available only through explicit leases, so saved SDF memory bandwidth is not undermined by legacy aliasing.
Hardware Impact: Verified microseconds: 0. Static CPU impact is neutral. Correctness gain is removal of an unlocked native payload escape that could read during DataVault direct-encode publication on i3/MX350-class hardware.

## Decision 024 - Exact DataVault Ownership For Voxel Streaming Scratch

Problem: `VoxelStreamingScratchSlot` stored many `VaultGenerationHandle<T>` descriptors but did not store which `IDataVault` created them. After DataVault replacement, slot disposal, lease array resolution, and job-lifetime locking could use the engine's current `_streamingScratchVault` against handles created by the previous vault. The engine also ignored `GlobalRegistryServiceSlot.DataVault`, so the cached scratch vault was not intentionally rebound.
Solution: Store `IDataVault Vault` on each scratch slot. Resolve lease arrays through `slot.Vault`, release slot handles through `slot.Vault`, and acquire job-lifetime locks through `slot.Vault`. Add DataVault hotswap handling: if no slots are active, dispose old slots and bind the current vault; if slots are active, record `_pendingStreamingScratchVault`, block new acquisitions, and finalize teardown only after active leases release. Mesh marching-cubes table acquisition in hot voxel phases now uses cached `_streamingScratchVault` instead of forcing a fresh `GlobalRegistry.DataVault` read.
Rejected Alternatives: Releasing old handles through the current vault was rejected because generation handles are only meaningful to the vault that issued them. Immediate hotswap disposal while slots are in use was rejected because scheduled voxel jobs may still hold phase-local views and locks. Adding a cross-domain global scratch broker was rejected as too broad for this pass.
Scalability potential: Low = weak devices avoid hotswap stalls and stale scratch leaks during sparse voxel generation; middle = stable cave/seam generation across normal streaming; high/ultra = dense voxel mesh phases can keep using saved cycles for visual overkill without risking vault-owner corruption.
Hardware Impact: Verified microseconds: 0. Static gain is correctness under DataVault hotswap plus removal of hot `GlobalRegistry.DataVault` reads from 13GEO marching-cubes table call sites. On i3/MX350-class hardware this protects against allocator/compaction stalls and wrong-vault release failures during world traversal.

## Decision 025 - Fourteenth Pass Build Timeout Classification

Problem: After static gates passed and the build gate briefly became legal, `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` did not finish within the 124-second tool timeout and emitted no compiler diagnostics. The timed-out command left two `dotnet.exe` build processes running, which pushed CPU above the project limit.
Solution: Stop the visible child build processes created by this build attempt, run `dotnet build-server shutdown` to clear the remaining VBCS compiler server, record the timeout as inconclusive verification, and keep status PENDING VERIFICATION. Do not claim compile success or infer 13GEO failure without compiler diagnostics.
Rejected Alternatives: Letting orphaned build processes continue was rejected because it starves sibling agents and violates the CPU gate. Treating a timeout with no errors as success was rejected as false reporting. Editing unrelated external compile blockers was rejected as outside 13GEO domain.
Scalability potential: Protects shared workstation throughput for all low/middle/high/ultra validation lanes.
Hardware Impact: No runtime gain. Process cleanup returned the machine to idle compiler state after the timed-out verification attempt.

## Decision 026 - DataVault Hotswap Payload Owner Split

Problem: `HectonVoxelEngine.OnGlobalRegistryServiceReplaced(DataVault)` correctly kept `_streamingScratchVault` on the old vault while active scratch slots drained, but then used that old vault to ensure published sonar SDF payload capacity. That can allocate or grow `VoxelSdfPayloadDescriptor`, `VoxelSdfTexture3D`, and `VoxelSdfAudioMaterialIds` in a retired vault after service replacement. The same hotswap path also had a rollback edge: A->B sets pending teardown, then B->A clears pending but left `_teardownStreamingScratchRequested` true, so finalization could assign `_streamingScratchVault = null`.
Solution: Split ownership by data class. Sonar SDF payload capacity now uses the `currentVault` supplied by the hotswap event because it is cross-domain published payload state. Scratch/MCTables remain on `_streamingScratchVault` while old voxel jobs drain. `RebindStreamingScratchVault()` now cancels teardown when the requested vault equals the active scratch vault.
Rejected Alternatives: Forcing immediate scratch disposal was rejected because scheduled voxel jobs may still hold scratch and MC table locks. Keeping sonar payload growth on the old vault was rejected because retired DataVault mutation is not an owner phase. Adding a new global scratch broker was rejected as scope inflation. Leaving rollback teardown alive was rejected because pending=null plus teardown=true can null the scratch route.
Scalability potential: Low = avoids retired-vault allocation spikes and scratch outages on weak hardware during service replacement. Middle = stable voxel cave/seam generation during normal streaming. High = dense voxel SDF and terrain seam visuals keep using saved memory bandwidth. Ultra = visual overkill remains tied to current published SDF payloads, not stale vault buffers.
Hardware Impact: Verified microseconds: 0. Static gain is correctness plus avoiding stale-vault payload growth up to the published sonar SDF payload capacity path during hotswap. On i3/MX350-class hardware this prevents allocator/compaction stalls and scratch acquisition outage after DataVault bounce; Unity/profiler proof remains pending.

## Decision 027 - Published Sonar SDF Exact Payload Read Locks

Problem: After direct DataVault encode, `TryReadPublishedSonarVaultPayload()` still resolved both `VoxelSdfTexture3D` and `VoxelSdfAudioMaterialIds` for every caller and read `VoxelSdfPayloadDescriptor` without a descriptor read lock. SDF-only density/raycast/gradient callers held only the SDF payload lock but still created an audio material read alias. The audio material sampler held only the audio payload lock but still created an SDF read alias. Descriptor publication is write-locked, so unpinned descriptor reads could observe publication/invalidations without participating in the DataVault lock protocol.
Solution: Keep the public lease contracts unchanged and make the private helper explicit. `TryReadPublishedSonarVaultPayload()` now requires at least one payload type, locks `VoxelSdfPayloadDescriptor` while reading metadata, and resolves SDF or audio payload aliases only when the caller requested that payload and already owns the corresponding read lock. SDF-only paths pass `requireAudioMaterial: false`; the audio material sampler passes `requireSdf: false`; combined sonar/audio lease paths still request both buffers.
Rejected Alternatives: Locking both payload buffers for all sampling was rejected because density/raycast paths do not need audio material ids and would create avoidable contention. Returning unmanaged payload aliases without locks was rejected because the publisher writes directly into the same vault buffers. Adding a new public DTO was rejected because the private helper could close the defect without cross-domain ABI churn.
Scalability potential: Low = weak devices avoid unnecessary payload alias contention on density/raycast probes. Middle = normal cave traversal keeps descriptor/payload reads deterministic while SDF publication runs. High = sonar/audio consumers still get combined payload leases when needed. Ultra = visual overkill SDF density remains available without widening public contracts or adding extra scratch copies.
Hardware Impact: Verified microseconds: 0. Static gain is one fewer unnecessary payload alias resolution on SDF-only reads and one fewer unnecessary SDF alias on audio-only reads, plus correctness under direct DataVault encode. On i3/MX350-class hardware this reduces lock contention risk during cave/seam traversal; Unity/player profiler proof remains pending.
