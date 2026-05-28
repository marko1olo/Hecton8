# Rationale_1411

Status: STATIC_VERIFIED_WITH_BUILD_BLOCKED_BY_CPU_CONTENTION

## Session Initialization

Problem: PCIe upload pipeline may contain hot full-array GraphicsBuffer/ComputeBuffer uploads.
Solution: Build a source-derived hit list before code edits; constrain changes to Graphics, World, and VFX unless a shared upload primitive already exists.
Rejected Alternatives: Blind wrapper insertion was rejected because it can create stale ping-pong buffers and public API churn.
Scalability potential: Low uses coarse dirty-page uploads and deferred distant pages; Middle uses normal page budget; High coalesces pages aggressively; Ultra spends saved bandwidth on richer visual instance payloads without changing gameplay truth.
Hardware Impact: Estimated i3/MX350 gain cannot be measured yet. Static target is to replace O(total instances) PCIe uploads with O(dirty pages) uploads.

## Loop 1 Static Inquisition

Problem: Native vegetation renderer uploads full Matrix4x4 and HectonVegetationInstanceData buffers every LateFrameTick through SyncSourceBinding -> BindInstanceNativeArrays.
Solution: Use the producer front/back BufferIndex plus instance count as the first upload cache key, then extend the native read token with optional dirty page masks for page-granular upload. Keep bounds updates independent from upload skips.
Rejected Alternatives: Rewriting MapMagic aggregate ownership first was rejected because the bridge already uses front/back DataVault handles; the hemorrhage is renderer-side repeated PCIe upload of unchanged front buffers.
Scalability potential: Low defers dirty pages with a small byte budget; Middle uploads normal coalesced page runs; High/Ultra can upload all dirty pages in one visual sync and spend saved bus bandwidth on richer metadata lanes.
Hardware Impact: For 10000 vegetation instances, one redundant native-source frame currently costs 1280000 bytes for matrices plus metadata. Skipping unchanged front buffers removes that entire transfer on static frames; one dirty page for matrices+metadata costs 32768 bytes.

Problem: Page dirty tracking cannot be inferred from current MapMagic aggregate source.
Solution: Add an additive contract path for NativeArray<byte> matrix/data dirty pages without forcing all producers to provide it immediately. Existing producers get coarse generation-skip behavior; future mutators can publish page masks.
Rejected Alternatives: Per-instance dirty flags were rejected because 100000 instances would force long scans and poor cache locality. Managed BitArray was rejected due to GC and non-Burst semantics.
Scalability potential: Low uses 512-instance or deferred 256-instance effective upload slices through budget; Middle uses 256; High and Ultra coalesce adjacent dirty pages to reduce lock/unlock overhead.
Hardware Impact: One index mutation at 99999 with 256-page size transfers 16384 bytes per 64B lane instead of 6400000 bytes per 100000-element lane, a 390.625x reduction for that lane.

## Loop 2 Runtime Code

Problem: HectonIndirectVegetationRenderer used one renderer-owned staging buffer for native Matrix4x4 and metadata uploads, which cannot safely accept partial dirty pages without stale ping-pong state.
Solution: Materialized A/B GraphicsBuffer pairs for matrices and metadata. Full refresh writes both A and B so both buffers start coherent. Dirty-page refresh marks per-buffer backlogs and publishes only after the selected write buffer has no deferred dirty pages.
Rejected Alternatives: Writing dirty pages into the current front buffer was rejected because LockBufferForWrite may stall or race the GPU. Copying front to back on the GPU before dirty upload was rejected because it needs command-buffer ordering proof not present in this renderer path.
Scalability potential: Low quality publishes only when dirty backlog drains under a small byte budget; Middle drains normal page runs; High and Ultra raise the byte budget continuously and coalesce longer runs.
Hardware Impact: Static vegetation frames now avoid the old 1.28 MB/frame upload at 10000 instances. Initial changed aggregate costs up to 2.56 MB because A/B are mirrored, but this occurs on publication, not every visual frame.

Problem: Dirty page uploads needed exact pointer math without hidden managed staging.
Solution: Added GraphicsBufferUploadUtility.UploadNativeArrayDirtyPages with safe count resolution, page coalescing, LockBufferForWrite<T>(startIndex,count), long byte offsets, UnsafeMemoryCopyGuard.TryMemCpy, and finally UnlockBufferAfterWrite.
Rejected Alternatives: NativeArray.GetSubArray plus SetData was rejected because it still routes through SetData validation and does not prove mapped memory. Per-element writes were rejected as lock amplification.
Scalability potential: Low uses budgeted pages across frames; Middle uses moderate coalescing; High/Ultra can drain all contiguous dirty runs and spend bandwidth on richer shader-visible metadata.
Hardware Impact: For a 100000 Matrix4x4 lane, one last-page mutation transfers 10240 bytes with 256-page math instead of 6400000 bytes. Full page worst case is 16384 bytes, still 390.625x below the full lane.

## Loop 3 Ping-Pong Dry Run

Problem: Classic double-buffer failure: frame N uploads dirty page 0 into B and publishes B; frame N+1 uploads page 1 into A and publishes A, but A still has stale page 0.
Solution: Each dirty mutation is mirrored into per-buffer dirty backlogs. Frame 1 index 5 marks page 0 dirty for A and B, writes page 0 to B, clears B page 0, publishes B, and leaves A page 0 dirty. Frame 2 index 500 marks page 1 dirty for A and B, writes pages 0 and 1 to A, clears A pages, publishes A. A now has both index 5 and index 500.
Rejected Alternatives: Uploading only the current source dirty pages to the next write buffer was rejected because it fails the frame 2 stale page test. Publishing a write buffer with deferred pages was rejected because it displays half-state.
Scalability potential: Low budget can spread A backlog over several frames without publishing partial data; Middle drains a few pages per frame; High and Ultra drain adjacent runs quickly and still avoid front-buffer writes.
Hardware Impact: Low-end silicon trades visibility latency for frame stability. Worst case massive dirty wave does not flicker because old front remains bound until the selected back buffer is coherent.

Problem: Build verification could consume CPU during shared multi-agent execution.
Solution: Per protocol, sampled Win32_Processor and checked csc.exe before any build. CPU load remained 100 percent and the latest csc count was 1, so dotnet build was not launched and Task 15 remains BLOCKED_BY_CONTENTION.
Rejected Alternatives: Running dotnet build anyway was rejected because the user explicitly banned meaningless CPU contention and this host was already saturated.
Scalability potential: Static checks continue while build resources are unavailable; full compile can run later when CPU is below 50 percent.
Hardware Impact: Avoided adding build pressure to a saturated workstation; no runtime hardware impact.

## Loop 6 APEX Data Sovereignty Repair

Problem: APEX self-audit found my renderer dirty-page backlogs were persistent `NativeArray<byte>` fields in a `MonoBehaviour`, violating the Data Sovereignty persistent alias ban.
Solution: Replaced the dirty backlogs with `VaultGenerationHandle<byte>` fields and secured BufferID values 74603-74606: matrix A, matrix B, metadata A, metadata B. `EnsureUploadedDirtyPageCapacity` now allocates/grows through `GlobalDataVault` via `EnsureVaultStorage`; `TryAcquireUploadedDirtyPagesForWrite` acquires all four writer locks inside method scope; both success paths release in caller `finally`, and failed partial acquisition releases in the acquisition method `finally`.
Rejected Alternatives: Keeping H8Memory persistent scratch was rejected after the audit because it is fast but violates owner-local memory law. Recomputing dirty pages every frame was rejected because it destroys the ping-pong backlog proof and reintroduces full scans.
Scalability potential: Low keeps the same 32 KiB budget and defers publication until the selected back buffer is clean; Middle drains more pages; High/Ultra can spend the 2 MiB budget ceiling on richer vegetation metadata without changing gameplay truth.
Hardware Impact: No measured runtime numbers. Static gain remains PCIe reduction from 6.4 MB matrix lane to 10.24-16.384 KiB dirty page on the 100000-instance test. The new Vault route adds lock bookkeeping but removes unmanaged alias risk under compaction.

Problem: Updated build gate after repair still showed a saturated host.
Solution: Sampled CPU/csc/dotnet again before any build. Latest final sample: CPU 100 percent, csc count 1, dotnet count 1, VBCSCompiler count 0. Build intentionally not launched.
Rejected Alternatives: Using the approved `dotnet build` prefix anyway was rejected because the active project rule forbids builds when CPU is over 50 percent or compiler/dotnet work is active.
Scalability potential: Static proof continues; runtime compile remains pending until host contention clears.
Hardware Impact: Avoided adding more process contention to a saturated workstation.

## Loop 7 Prompt Count Correction

Problem: My ledger mislabeled the 6 mandatory constraints as source directives. Re-extraction proved `AGENT_PROMPT id="1411"` contains 20 `Task NN:` entries, plus 6 mandatory constraints and 4 self-audit questions.
Solution: Correct status and reports to state `Prompt XML Task Count: 20`, `Mandatory Constraint Count: 6`, and `Self Audit Question Count: 4`.
Rejected Alternatives: Keeping the earlier `Source Directive Count: 6` was rejected because it contradicts `Docs/Tasks/CURRENT_BATCH.md` lines 989-1075.
Scalability potential: No runtime impact. It reduces coordination risk for integrators comparing prompt coverage against implementation coverage.
Hardware Impact: No hardware impact.

## Loop 8 VFX Debris PCIe Repair

Problem: Secondary domain scan found `CarveDebrisComputeRenderer.UploadRange` still used `GraphicsBuffer.SetData` for injected debris ranges. It was already range-limited and double-uploaded to A/B buffers for coherence, but it still violated the mapped-upload mandate and could hit visual-sync PCIe paths after carve/debris signals.
Solution: Replaced `SetData` with `LockBufferForWrite<T>(safeStart, safeCount)`, long byte-offset source pointer math, `UnsafeMemoryCopyGuard.TryMemCpy`, and `finally UnlockBufferAfterWrite<T>(safeCount)`. Changed carve debris GPU buffers to `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>` so mapped writes are valid.
Rejected Alternatives: Leaving it because it was not a full-array per-frame upload was rejected; the domain directive requires mapped uploads where supported. Adding a separate dirty page system was rejected here because debris already tracks min/max injected range and does not need page expansion.
Scalability potential: Low/Middle only upload injected debris ranges; High/Ultra can keep richer debris particle payloads without switching to full buffer uploads.
Hardware Impact: Static benefit is avoiding Unity `SetData` validation/copy path for carve debris position/velocity ranges. No measured runtime numbers.

Problem: Updated build gate after debris repair still showed CPU contention.
Solution: Sampled CPU/csc/dotnet again. Latest final sample: CPU 100 percent, csc count 1, dotnet count 1, VBCSCompiler count 0. Build intentionally not launched.
Rejected Alternatives: Running build with CPU at 100 percent was rejected by compilation resource throttling.
Scalability potential: Compile remains pending; static AST and text gates continue.
Hardware Impact: Avoided additional CPU contention.

## Loop 10 Deferred Backlog Guard

Problem: Dirty-page fallback checked `source dirty == false && ContentRevision changed` before checking renderer-owned deferred backlog. If a future producer clears its dirty flags after release, low-quality time-slicing could degrade into a full upload before the selected back buffer finishes draining.
Solution: Added `HasUploadedWriteDirtyPageBacklog` and `HasDirtyPageBacklog` read-only DataVault checks. The full-upload fallback now executes only when source dirty flags are empty, revision changed, and the selected renderer write buffer has no deferred dirty pages.
Rejected Alternatives: Always trusting producer dirty flags was rejected because producer lifetime is outside this renderer. Holding write locks while falling back to full upload was rejected because `BindInstanceNativeArrays` clears dirty pages and would recursively acquire the same lock lane.
Scalability potential: Low continues draining deferred dirty pages over multiple frames; Middle drains normal page runs; High/Ultra still drain immediately under the larger continuous byte budget.
Hardware Impact: No measured runtime numbers. Static prevention: avoids unnecessary full `instanceCount * 128B` upload when renderer backlog already contains exact dirty pages.

## Loop 11 Combined Budget Guard

Problem: The low-tier upload budget was applied per lane composition imperfectly. If matrix dirty pages consumed the full frame budget, metadata dirty upload still received a tiny positive budget and `UploadNativeArrayDirtyPages` intentionally uploaded one progress page, allowing combined matrix+metadata traffic to exceed the visual-sync byte budget.
Solution: Added `GraphicsBufferUploadUtility.ResolveFirstDirtyPageBytes<T>` and gated metadata upload in `BindInstanceNativeDirtyPages`. Metadata now uploads only when the remaining combined budget can cover its first dirty page, except when no matrix bytes were uploaded and metadata is the only progress path. `PcieBandwidthGuard1411SelfTest` now asserts the matrix+metadata composition does not overshoot.
Rejected Alternatives: Changing `UploadNativeArrayDirtyPages` to hard-fail when `maxBytesThisFrame` is smaller than one page was rejected because it can deadlock progress if future page sizes exceed the minimum budget. Binary low/high gating was rejected; the fix preserves the continuous `GlobalQualityWeight` byte budget.
Scalability potential: Low drains one or more exact pages without surprise over-budget metadata spill; Middle drains both lanes when the smooth budget covers them; High and Ultra retain immediate coalesced upload under larger budgets.
Hardware Impact: No runtime measurement. Static prevention: at 32 KiB budget, two 16 KiB matrix pages no longer trigger an additional 16 KiB metadata upload in the same visual-sync pass.
