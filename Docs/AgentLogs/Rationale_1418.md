# Rationale 1418 - VOXEL_TERRAIN_AND_MARCHING_CUBES_COMPACTOR

Status: STATIC APEX PLUS VOLUME LEASE RE-AUDITED - BUILD BLOCKED BY CONTENTION

## Decision 000 - Start Gate

Problem: The assignment requires replacing cross-frame voxel TempJob scratch exchange with DataVault leasing, but source APIs and existing lifecycle are not yet proven.
Solution: Run Phase 0 static archaeology before touching code. Use mandates for voxel MC, carving persistence, native memory jobs, arena allocator, zero GC, ARM64 layout, telemetry, and execution phases as the active constraint set.
Rejected Alternatives: Direct substitution by invented lease API was rejected because AGENTS forbids invented dependencies and public API mutation without proof. Blind dotnet build was rejected because CPU/compiler throttling is mandated.
Scalability potential: Low uses bounded queued carve processing and fail-closed scratch overflow; middle keeps normal cadence; high and ultra spend saved stability on faster mesh presentation and richer near-field visuals, not changed gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is removal of allocation spikes from TempJob churn; numeric profiler proof absent until Unity/GCMonitor run.

## Decision 001 - Do Not Rebuild Existing Vault Scratch Pipeline

Problem: The prompt expects TempJob transfer allocations between `VoxelDeltaProcessor` and `HectonVoxelEngine`, but static source scan found zero `Allocator.TempJob` and zero `new NativeArray<...>` hits in both target files.
Solution: Preserve the existing DataVault scratch route shape: computed scratch IDs with stride 60 per slot for WorldStreaming scratch, plus `ShinobuDeltaCrusher*` TerrainSeams buffers for carve state. Add only the missing 1418-specific blackbox dump fanout.
Rejected Alternatives: Replacing the existing scratch system with a new 1418000-range buffer set was rejected because it would duplicate a working owner route, increase Vault surface area, and violate one fact -> one owner -> one route.
Scalability potential: Low keeps bounded scratch capacities and fail-closed overflow; middle uses normal cadence; high/ultra already scale raw mesh scratch through `HomeostasisBrain.GlobalQualityWeight` for faster visual mesh extraction.
Hardware Impact: Avoids new allocator churn and preserves existing MX350 bounded scratch caps. Source-only estimate: saves unpredictable TempJob allocation spikes; profiler proof absent.

## Decision 002 - 1418 Dump Path Integration

Problem: Existing mesh blackbox dumped to generic and 1315 paths, but the current assignment requires `Docs/AgentLogs/Dump_1418_VoxelCompaction.bin`.
Solution: Add `VoxelMeshPipelineBlackBoxCompactionDumpRelativePath` and write the same 300-entry, 64-byte `VoxelMeshPipelineTelemetryEntry` ring to that file when a development/editor fault dump triggers.
Rejected Alternatives: Creating a second `VoxelCompactionTelemetryEntry` ring was rejected because the existing ring already records BufferID, SystemID, generation, state hash, mesh queue counters, pool pressure, and scratch overflow flags at 64 bytes. A duplicate ring would increase memory and risk divergent telemetry.
Scalability potential: Low/middle/high/ultra share the same bounded dump path; high-tier visual overkill does not alter telemetry layout or gameplay truth.
Hardware Impact: No frame hot-path allocation added. Fault-path writes only in editor/development guarded dump flow; runtime hot path writes one unmanaged ring entry as before.

## Decision 003 - Streaming Scratch BufferID Collision Repair

Problem: `StreamingScratchVaultBufferBase = 74500` generated computed scratch IDs `74500..74979`, colliding with declared `PersistentWorldRegistry` and vegetation `BufferID` values, including `VegetationSurfaceDefragScratchBiomeLayers = 74500`.
Solution: Move the computed WorldStreaming scratch range to `76500..76979` (`StreamingScratchVaultBufferBase = 76500`, stride 60, 8 slots). The range is below the DataVault `TryLockBuffer` flat metadata ceiling and had no source hit in the numeric BufferID scan.
Rejected Alternatives: `1418000+` was rejected because `TryLockBuffer` rejects high IDs outside the flat metadata array. `76000` was rejected because `GameBootstrapper` already casts `(BufferID)76000` for shader warmup telemetry.
Scalability potential: Low/middle/high/ultra keep the same lane count and capacities; this changes identity routing only, not quality or gameplay truth.
Hardware Impact: Prevents two systems from locking/resolving the same vault metadata slot. Estimated low-end gain is correctness and removal of collision-driven retry/fail-closed churn; profiler proof absent.

## Decision 004 - Keep TryLockBuffer Pin For Job-Lifetime Scratch Views

Problem: The prompt names `TryReadOnlyHandle`, but the current mesh pipeline passes mutable `NativeArray<T>` scratch views into jobs with `[ReadOnly]`/`[NoAlias]` where applicable and requires relocation pinning across awaited job completion windows.
Solution: Keep the existing `TryLockStreamingScratchJobLifetime`/`UnlockStreamingScratchJobLifetime` route for job-lifetime pins. It collects every slot buffer id, locks them through `IDataVault.TryLockBuffer`, schedules/awaits jobs, and unlocks in `finally` or lease disposal. Pure `TryReadOnlyHandle` remains used for table/blackbox readbacks and validation, not long-lived job pins.
Rejected Alternatives: Converting job fields to `NativeArray<T>.ReadOnly` was rejected because current Burst job signatures and write-output fields require `NativeArray<T>`; changing all signatures would be a public pipeline churn risk without fixing a measured bug. Passing `VaultGenerationHandle<T>` into jobs was rejected because Burst jobs must receive physical unmanaged views, not vault descriptors.
Scalability potential: Low keeps fewer/shorter scratch jobs via existing quality caps; middle/high/ultra retain the same lane ownership while allowing higher raw mesh scratch capacity through continuous `GlobalQualityWeight`.
Hardware Impact: Keeps relocation pinned only during explicit job windows and avoids same-frame allocation. Estimated i3/MX350 effect is stable memory identity under carve spam; runtime proof absent.

## Decision 005 - Build Blocked By Host Contention

Problem: Task 15 requests a single build, but the compile throttle protocol forbids launching `dotnet build` when CPU load exceeds 50% or another compiler/dotnet process is active.
Solution: Sampled host CPU and compiler processes before build. Latest CPU load was 100%, and an existing `dotnet` process was active at PID 55080. Marked the build gate `BLOCKED_BY_CONTENTION` and relied on static source scans plus a new editor guard test.
Rejected Alternatives: Starting a second build was rejected because it violates the batch prompt and risks starving sibling agents. Ignoring the new test compile risk was rejected, so the test was kept source-simple and covered by static token checks.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged by the skipped build; verification waits for a quiet host while the code path remains bounded and fail-closed.
Hardware Impact: Avoided additional CPU pressure on an already saturated host. No runtime gain claimed from the skipped build; static proof only until contention clears.

## Decision 006 - APEX Proof Artifact And Residual Faults

Problem: A chat-only completion claim is insufficient; the verifier requires exact line numbers, static counts, hashes, and declared residual risk.
Solution: Wrote `Docs/Reports/VOXEL_COMPACTION_OPTIMIZATION_REPORT_1418.json`, its `.sha256` sidecar, and `Assets/_Project/Tests/Editor/VoxelCompaction1418EditTests.cs`. The report records modified line numbers, target hot-path zero-GC counts, BufferID routes, lock/finally proof, DTO offsets, continuous quality hooks, and the build contention sample.
Rejected Alternatives: Claiming live 50000-hit profiler proof was rejected because no Unity test/build was run under contention. Merging the delta processor 80-byte carve blackbox into `Dump_1418_VoxelCompaction.bin` was rejected because it would corrupt the 64-byte mesh telemetry dump format.
Scalability potential: Low uses bounded queue/coalescing and lower scratch/drain budgets; middle uses the same smooth scalar path; high and ultra spend extra scratch capacity on faster mesh presentation while preserving topology truth and DTO layout.
Hardware Impact: Static-only improvement claim: removes a BufferID collision that could cause vault-route contention/fail-closed churn on i3/MX350. Measured microseconds are not available without profiler execution; report records estimates only.

## Decision 007 - Remove Nested Published SDF Sampling Lease

Problem: A second-pass audit found `HectonVoxelEngine.TrySampleNearestSonarSdf` acquiring `TryAcquirePublishedSonarSdfPayloadReadLease`, then calling `volume.TrySampleSonarSdf`, which re-entered the same published SDF read-lock and descriptor path through `HectonVoxelVolume.TrySampleDensity`. `GlobalDataVault.TryLockBuffer` is owner-reentrant for the same `SystemID`, so this was not a deadlock, but it was a wasteful read accessor inside an already valid lease.
Solution: Use the `candidateSdf`, dimensions, origin, cell size, and range from the existing lease directly with `VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear`, then compute `density01` as `saturate(max(0,density)/range)`. Release still happens through `ReleasePublishedSonarSdfPayloadReadLease` in `finally`.
Rejected Alternatives: Leaving the nested `volume.TrySampleSonarSdf` call was rejected because read accessors must be pure and should not perform hidden extra descriptor/lock work when the physical view is already leased. Rewriting `HectonVoxelVolume.TrySampleDensity` was rejected because multiple local systems legitimately use that public standalone accessor.
Scalability potential: Low avoids redundant lock/metadata work during repeated sonar sampling; middle keeps identical density math; high and ultra can spend saved budget on denser sonar presentation without changing SDF truth ownership or mesh topology.
Hardware Impact: Expected low-end i3/MX350 effect is lower metadata contention in sonar sampling paths under active voxel scenes. Measured microseconds remain unproven because build/test/profiler execution is still blocked by CPU 100% plus an active `dotnet` process.

## Decision 008 - Adjacent TempJob Findings Stay Out Of 1418 Patch

Problem: A wider voxel/marching/SDF/terrain-name scan found 74 `Allocator.TempJob`/native staging hits outside `HectonVoxelEngine.cs` and `VoxelDeltaProcessor.cs`. Most are editor/dev/smoke-test bake tools, but `VoxelDynamicNavGridRuntime` and `VegetationTerrainHoleSynchronizer` are runtime external consumers with residual TempJob staging.
Solution: Record the grouped findings in `Docs/Reports/VOXEL_COMPACTION_OPTIMIZATION_REPORT_1418.json` and do not patch them inside 1418. The authoritative domain file separates voxel MC/carving from funnel/pathfinding and vegetation/BRG scatter ownership. Editing those systems now would be a cross-domain route change without an owner route card.
Rejected Alternatives: Silently ignoring adjacent TempJob hits was rejected because the user requested an honest search for remaining problems. Opportunistically rewriting nav-grid/vegetation staging was rejected because it would alter external authority routes without proof, build, or owner review.
Scalability potential: Low devices still carry risk in those external owner domains until migrated; middle/high/ultra behavior for the 1418 MC exchange remains bounded by the repaired scratch range and direct SDF lease path.
Hardware Impact: No claimed runtime gain from unmodified adjacent systems. The residual risk is documented for follow-up owner-domain migration; measured impact is unknown.

## Decision 009 - Pre-Reject Farther Sonar SDF Candidates

Problem: `TrySampleNearestSonarSdf` still performed a trilinear SDF decode before checking whether the candidate volume was farther than an already accepted payload by `ResolveSdfPayloadBoundsDistanceSq`.
Solution: Move bounds-distance rejection before `VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear`. This keeps the exact same nearest-volume selection rule, but avoids SDF decode work for candidates that cannot win.
Rejected Alternatives: Leaving the post-sample bounds check was rejected because it wastes per-query math on low-end devices with multiple active voxel volumes. Adding a cached nearest-volume registry was rejected as a broader authority-route change without runtime profiler proof.
Scalability potential: Low skips unnecessary trilinear samples earlier; middle/high/ultra keep identical SDF truth and can spend saved cycles on denser sonar presentation.
Hardware Impact: Expected i3/MX350 effect is lower per-query math when multiple voxel volumes are active. Exact microseconds remain unproven because compilation/profiler execution is blocked by CPU 100% and active `dotnet` PID 55080.

## Decision 010 - Strengthen Static Guard Against Missing Bounds Token

Problem: The new editor guard checked ordering with `IndexOf("ResolveSdfPayloadBoundsDistanceSq") < IndexOf("VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear")`. If the bounds token disappeared, `IndexOf` would return `-1` and could still satisfy the less-than assertion.
Solution: Add an explicit `StringAssert.Contains("ResolveSdfPayloadBoundsDistanceSq", sampleBlock)` before the ordering assertion.
Rejected Alternatives: Trusting the ordering assertion alone was rejected because it could produce a false positive. Running Unity tests was rejected under the current build gate because CPU remains 100% with active `dotnet`.
Scalability potential: This is proof hardening only; runtime scalability remains the bounds pre-reject plus continuous `GlobalQualityWeight` capacity paths.
Hardware Impact: No runtime impact; it reduces verifier false positives.

## Decision 011 - Collapse Internal Voxel Volume Density Scan Lock Churn

Problem: A post-report voxel-domain audit found `HectonVoxelVolume` private density scan loops calling the public `TrySampleDensity` accessor for every probe. This repeatedly re-acquired `BufferID.VoxelSdfTexture3D` and re-read payload descriptors inside burrow route, organic root mound, and seismic collapse scans. It did not allocate `TempJob`, but it was avoidable DataVault metadata churn in the same published SDF domain.
Solution: Convert the internal loops to acquire one `TryAcquirePublishedSonarSdfPayloadReadLease` per method, pass the leased `NativeArray<byte>.ReadOnly` payload into private helpers, sample through `TrySamplePublishedDensity`, and release through `ReleasePublishedSonarSdfPayloadReadLease` in `finally`. Public `TrySampleDensity` remains available for standalone external consumers.
Rejected Alternatives: Rewriting the public accessor was rejected because external systems still need a fail-closed one-shot read API. Holding no lease and trusting descriptor stability was rejected because DataVault compaction fences can move buffers. Moving these scans to a new cache was rejected because that would create a second SDF truth route.
Scalability potential: Low devices avoid repeated lock/descriptor work during fauna burrow and terrain collapse scans. Middle devices keep identical SDF math. High and ultra can spend the saved metadata budget on denser visual/sonar queries without changing topology truth, DTO layout, or authority ownership.
Hardware Impact: Expected i3/MX350 effect is reduced `VoxelSdfTexture3D` lock churn under repeated local density probes. Exact microseconds remain `UNPROVEN` because build/test/profiler execution is blocked by the final gate sample: CPU 90% and active `dotnet` PID 20440.

## Decision 012 - Reject Wider Seismic Shockwave Lease Window

Problem: `TryApplySeismicShockwave` can call `TryResolveSeismicCollapseAnchor` up to eight times, so one larger SDF read lease around the whole stamp loop looked like a possible follow-up optimization.
Solution: Keep the shorter per-anchor lease window. The stamp loop calls `CarveCrater` after resolving each anchor, and `CarveCrater` changes bake state and routes a delta operation. Holding a published SDF read lease across that mutation would mix read phase and write/deformation phase ownership.
Rejected Alternatives: A single lease across the whole shockwave loop was rejected because it would pin `VoxelSdfTexture3D` through `CarveCrater`. Staging all anchors into a new managed array was rejected as a hot-path allocation risk. Adding a new global cache was rejected because it would duplicate SDF truth ownership.
Scalability potential: Low devices still benefit from single-lease root/burrow scan fixes, while seismic stamps keep shorter, safer lock windows. Middle/high/ultra preserve exact terrain topology and avoid wider lock contention during deformation.
Hardware Impact: No new runtime gain claimed. This is a safety decision: avoid holding DataVault read locks through authoritative deformation calls.

## Decision 013 - Strengthen Single-Lease Static Guard

Problem: `VoxelVolumeBulkDensityScans_UseSinglePublishedSdfLease` proved the presence of a published SDF read lease, but did not prove there was exactly one acquisition in each owning scan block.
Solution: Add `CountOccurrences` and assert exactly one `TryAcquirePublishedSonarSdfPayloadReadLease` in burrow, root mound, and seismic scan blocks. This is editor-only verification hardening.
Rejected Alternatives: A regex dependency was rejected; the guard uses simple `IndexOf` iteration to avoid adding a new assembly dependency to the editor test.
Scalability potential: No runtime behavior change. The verifier now protects the intended low-device lock-churn reduction against future regression.
Hardware Impact: No runtime impact.
