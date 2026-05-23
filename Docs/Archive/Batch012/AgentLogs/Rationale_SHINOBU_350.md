# Rationale_SHINOBU_350

Status: POLISH_R7_STATIC_VERIFIED_BUILD_WITHHELD_CPU_GUARD

## Decision 000 - Preflight Boundary

Problem: SHINOBU_350 touches global authority, Vault memory, UI presentation, and possible signal lanes. Unbounded edits would create compile walls with parallel agents.
Solution: Treat Cartography/Fog as the owner domain, use partial integration if a runtime exists, prefer owner-local Vault buffers and existing signal lanes, and write route evidence before adding public surface.
Rejected Alternatives: New standalone manager and new MapUpdated signal were rejected before archaeology because they violate partial integration and signal-lane discipline.
Scalability potential: Low uses sparse cadence and bitmask truth; Middle keeps deterministic reveal; High/Ultra spend saved CPU on denser visual sync and shader presentation only.
Hardware Impact: Expected low-end gain is removal of managed dictionaries/GameObjects from exploration tracking; estimate pending source scan, target under 0.1 ms steady-state on i3/MX350.

## Decision 001 - Mandate Set

Problem: The prompt spans memory, AUP, deterministic jobs, GPU upload, telemetry, and signal routing.
Solution: Loaded 8 relevant mandates: zero-GC, ARM64 layout, AUP determinism, AUP precision, registry DI, execution phases, signal segregation, crash telemetry.
Rejected Alternatives: Reading every mandate was rejected as noise; reading only UI mandate was rejected as incomplete because the requested system is a native/Vault pipeline.
Scalability potential: Mandates force continuous quality/cadence and buffer scaling rather than tier toggles.
Hardware Impact: Mandates bias toward flat NativeArray<ulong>, no managed map structures, and bounded GPU upload pages for MX350-class bandwidth.

## Decision 002 - Existing Owner Reuse

Problem: The batch demanded partial integration if a cartography runtime existed, but no HectonCartographyRuntime was present. The actual owner was PlayerExplorationTracker plus CartographyGridJobs.
Solution: Extend the existing owner and Vault job set in place, preserving the dispatcher phase adapters already registered by PlayerExplorationTracker.
Rejected Alternatives: A new HectonFogOfWarManager would duplicate GlobalRegistry ownership and create a hot scene dependency.
Scalability potential: Low/Middle/High/Ultra all read the same NativeArray<ulong>; only cadence, upload frequency, and shader visual density scale.
Hardware Impact: Avoids an extra MonoBehaviour update path and scene lookup; low-end gain estimated 20-40 us versus an additional manager plus registry polling.

## Decision 003 - 10m Voxel Contract

Problem: The prior cartography constant represented 50m cells and did not satisfy the 10x10x10m AUP division requirement.
Solution: Add VoxelSizeMeters=10 and bind MacroCellSizeMeters to that value so all existing flatten/decode/save paths use the same cell size.
Rejected Alternatives: Runtime-mutating cell size was rejected because DTO/save identity and bit layout would stop being stable.
Scalability potential: Weak devices keep the same truth grid but update less often; higher tiers upload/display more frequently and keep stronger visual density.
Hardware Impact: Per-cell math cost remains integer/bitwise; larger bit churn is bounded by cadence and sonar word-run masks.

## Decision 004 - Dear Lie Sonar

Problem: Per-voxel sonar reveal scales badly for large pings and wastes CPU on exact geometry where a cartography reveal only needs believable explored state.
Solution: For sonar flags, compute y/z rows and reveal contiguous x ranges with ulong masks through AtomicOrCount.
Rejected Alternatives: A full SDF surface pass for every sonar ping was rejected as too expensive for a discovery mask.
Scalability potential: Low uses the row-range lie; Middle keeps SDF mask for non-sonar POI/acoustic reveals; High/Ultra spend saved CPU in shader glow and upload cadence.
Hardware Impact: Worst-case sonar mutation shifts from thousands of single-bit CAS attempts to row-range word masks; MX350/i3 expected to save hundreds of microseconds on large pings.

## Decision 005 - GPU Upload Buffering

Problem: The packed R8 buffer could be written on the same GraphicsBuffer currently bound for hologram rendering.
Solution: Add A/B GraphicsBuffer swap in PDAMapTab while retaining the shader-only virtual 3D volume path.
Rejected Alternatives: GameObject voxel/cube rendering was rejected because it destroys batching and violates object-based renderer purge.
Scalability potential: Low uploads less often; Ultra can upload every cadence without active-buffer write hazards.
Hardware Impact: Avoids avoidable GPU/CPU synchronization stalls; low-end benefit depends on driver, estimated 50-150 us during upload frames.

## Decision 006 - Telemetry And Forensics

Problem: The previous black box lacked SHINOBU dump naming, RLE compression, mutation timing, and a 32-byte state DTO.
Solution: Add CartographyStateDTO at Vault buffer 71437, expand telemetry to 80 bytes, record RLE permille/mutation microseconds/flags, and dump to Dump_SHINOBU_350.bin.
Rejected Alternatives: Chat-only reports and managed diagnostic dictionaries were rejected because crash proof must survive context loss.
Scalability potential: Low/Middle/High/Ultra use identical truth telemetry; only thresholds and visual use differ.
Hardware Impact: One 32-byte state DTO and 300x80B ring are cache-stable; expected memory cost is 24KB telemetry plus 32B state.

## Decision 007 - CSV Cold Path

Problem: The scanner profile ingest previously loaded a managed byte[] before parsing into Vault scratch memory.
Solution: Stream bytes directly into the preallocated CsvScratch NativeArray and parse deterministic FNV-1a token hashes from that buffer.
Rejected Alternatives: File.ReadAllBytes, string.Split, and float.Parse were rejected because they hide cold-path allocations and culture-dependent parsing.
Scalability potential: Low uses smaller profile radii/cadence through data; Middle/High/Ultra can raise sonar radius to 500m without changing truth layout.
Hardware Impact: Eliminates one managed byte[] during profile reload; runtime impact is zero because ingest remains cold/editor-owned.

## Decision 008 - Read Accessor Purity And Cold Registry Boundary

Problem: Ultra-polish review found read-shaped APIs still called `InitializeExplorationMask()` or Vault ensure paths, and player AUP resolution could touch `GlobalRegistry.Player` from tick-time code.
Solution: Split cached Vault reads into `TryReadCartographyBuffers` and command ownership into `TryEnsureCartographyBuffers`; make telemetry/tuning/prepare/mask reads fail closed without allocation; rename player cache refresh to an explicit cold command and make AUP reads use the cached movement reference only.
Rejected Alternatives: Keeping lazy initialization in `TryGet*` accessors was rejected because it violates the global read-accessor doctrine and hides structural mutation behind UI/editor reads.
Scalability potential: Low/Middle/High/Ultra all use the same truth buffers; this change affects authority hygiene and cache predictability, not visual tiering.
Hardware Impact: Removes cold Vault handle recovery and registry lookup from read paths; i3/MX350 impact is small per call but prevents worst-case UI read spikes and compile-wall-style hidden side effects.

## Decision 009 - Compile Guard After Missing Restore Assets

Problem: A guarded `dotnet build Hecton8.Core.csproj --no-restore` could not reach C# compilation because `Temp/obj/Hecton8.Core/project.assets.json` is absent.
Solution: Record NETSDK1004 as an environment/restore precondition, then stop before running restore because follow-up CPU samples rose above the 50% build guard.
Rejected Alternatives: Running a restore/build while CPU sampled 56% then 85% was rejected by the explicit local hardware protection rule.
Scalability potential: No runtime scalability change; this preserves iteration hardware and avoids creating parallel compiler pressure.
Hardware Impact: Avoided a high-load restore/compile during 85% CPU pressure; no C# error list exists yet for SHINOBU_350 because compilation did not start.

## Decision 010 - Legacy PDA Exploration Vault Eviction

Problem: A second ultra-polish pass found the legacy PDA exploration mask still owned a private `NativeBitArray` and `NativeList<int>` inside `PlayerExplorationTracker`, contradicting the Vault-law audit even though the SHINOBU_350 fog truth was Vault-backed.
Solution: Add Vault lanes now assigned as `71459 LegacyExplorationWords`, `71460 LegacyExploredBitIndices`, and `71461 LegacyExploredBitIndexCount`; route legacy dense Morton save/read/copy operations through cached Vault views and remove the private native containers.
Rejected Alternatives: Keeping the local native containers as "legacy" was rejected because it preserved two memory authorities for exploration state in the same owner.
Scalability potential: Low/Middle/High/Ultra keep identical save identity and bit layout; quality only changes cadence/presentation and not legacy mask truth.
Hardware Impact: Removes two persistent owner-local native allocations from the MonoBehaviour; low-end gain is memory ownership hygiene and fewer teardown/native-sentinel operations, not ALU savings.

## Decision 011 - Roslyn AST OOP Map Scanner

Problem: The OOP map eradication proof used lexical string search and wrote a flat top-level report block into a shared JSON file.
Solution: Rebuild `OOP_Map_Scanner` around `CSharpSyntaxTree` AST traversal with lexical fallback only on parse exception, and upsert a `shinobu_350_sonar_cartography_fog_of_war` section in the shared rendering report.
Rejected Alternatives: Keeping text-only search was rejected because Task 19 explicitly demanded AST proof and because whole-file overwrite can erase adjacent agents' report sections.
Scalability potential: Editor/proof only; no runtime quality curve changes.
Hardware Impact: No player-runtime cost. Editor scanner cost is cold and bounded to UI/Cartography source files.

## Decision 012 - R3 Read Purity And Editor Assembly Hardening

Problem: Static audit found three residual risks: `OOP_Map_Scanner` used Roslyn without an explicit Cartography editor asmdef precompiled-reference edge, read-shaped accessors still reached `TryResolveViews`, and the SceneView gizmo used an ensure path plus a debug job that mutates Vault debug buffers while drawing.
Solution: Added explicit Roslyn precompiled references to `Hecton8.Cartography.Editor.asmdef`; added the read route now hardened as `CartographyVault.TryReadOnlyViews` on `IDataVault.TryReadOnlyHandle`; routed `TryReadCartographyBuffers`/`CartographyVault.TryGetTuning` through that read path; changed `OnDrawGizmos` to sample `DiscoveryWords` directly without writing `DebugVoxels`; changed tuning writes to `UnsafeUtility.AsRef<CartographyTuningDTO>`.
Rejected Alternatives: Relying on transitive Roslyn DLL visibility was rejected because asmdefs are compile-wall boundaries. Keeping gizmo debug-buffer mutation was rejected because a read visualization must not publish or mutate proof buffers. Widening the patch into core Vault was rejected as out-of-domain.
Scalability potential: Runtime truth and quality curve are unchanged; Low/Middle/High/Ultra still scale cadence/upload/shader work continuously while read access remains side-effect-free.
Hardware Impact: No measured frame-time delta. The change removes possible editor/read spikes from resolve/ensure paths and prevents unnecessary debug-buffer writes during SceneView repaint; low-end impact is stability and fewer cold-side cache writes, not ALU throughput.

## Decision 013 - R4 Designer Voxel Tuning Without Truth Layout Mutation

Problem: Task 16 required a live VoxelSizeMeters slider, but directly changing the 10m voxel truth grid would mutate the 1D bit index contract, save metadata, rollback snapshot identity, and shader packing ABI. A second risk remained in the Vault route: the newly evicted legacy PDA lanes could make core sonar reads fail under allocation lock if an older Vault only had core cartography buffers.
Solution: Treat `CartographyTuningDTO.CellSizeMeters` as designer-controlled player reveal diameter over the immutable 10m truth grid. The Editor slider is active and writes the Vault DTO through `UnsafeUtility.AsRef`; `ApplyCartographyFrameDiscoveryJob` consumes the value as `PlayerRevealRadiusMeters`, using the exact single-bit path at 10m and the existing row-range Dear Lie above 10m. Split `CartographyVaultHandles` and `CartographyVaultBuffers` into core/legacy readiness so missing legacy PDA cache lanes fail only legacy read helpers, not the core `NativeArray<ulong>` sonar truth path.
Rejected Alternatives: Runtime-changing `CartographyGridConstants.VoxelSizeMeters` was rejected because it would corrupt persisted bit indices and make old RLE/save payloads ambiguous. Replacing player reveal with a managed per-cell list was rejected for GC and object ownership. Keeping all-or-nothing `TryResolveExisting` was rejected because optional legacy cache buffers should not block the authoritative cartography bitmask route.
Scalability potential: Low keeps 10m truth and slow cadence; Middle can tune 20-40m player reveal shells for smoother designer testing; High/Ultra can use larger local reveal shells while GPU/shader presentation buys the visual overkill. GlobalQualityWeight still changes cadence/presentation only and does not change DTO layout, save identity, or authority route.
Hardware Impact: At 10m the old one-bit path is unchanged. Above 10m, reveal expansion uses row-range `ulong` masks rather than per-voxel object work; estimated MX350/i3 cost remains bounded to yz rows and touched words, typically tens of microseconds for local shells. Core/legacy split avoids a full cartography read failure and prevents retry churn when allocation is locked.

## Decision 014 - R5 Read-Only Vault Consumer Surface

Problem: The R3/R4 read route stopped allocation/ensure side effects, but consumer readbacks still received mutable `NativeArray<T>` views through `TryReadHandle`. That left a technical hole: a future read-shaped method could accidentally mutate Vault truth while still passing the "no ensure" purity scan.
Solution: Added `CartographyVaultReadBuffers`, a separate read DTO whose fields are all `NativeArray<T>.ReadOnly`. `CartographyVault.TryReadOnlyViews` resolves every core lane with `IDataVault.TryReadOnlyHandle`; optional legacy PDA lanes resolve through a matching read-only helper. `PlayerExplorationTracker.TryReadCartographyBuffers`, legacy mask reads, telemetry reads, tuning reads, prepare-info reads, and the editor gizmo now consume the read-only DTO. Mutable `CartographyVaultBuffers` remains confined to owner command routes: dispatcher mutation, save/upload staging, tuning writes, RLE generation, CSV boot/editor ingest, and teardown.
Rejected Alternatives: Keeping mutable read buffers was rejected because naming alone cannot protect authority boundaries. Converting every owner path to read-only was rejected because writers would then re-open mutable views deeper in the call stack, hiding the mutation point. Editing the core Vault implementation was rejected as out-of-domain; the existing `TryReadOnlyHandle` bridge is the sanctioned consumer surface.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged because quality must not alter truth ownership. The gain is authority hygiene: the same 10m bitmask can be read by PDA UI, save metadata, telemetry, and gizmo code without receiving write-capable views.
Hardware Impact: Runtime ALU savings are negligible; the value is preventing accidental writes and cache dirtying from diagnostic/UI reads. On i3/MX350-class machines this avoids future read-side retry/mutation spikes and keeps shared Vault rows cold unless an owner command actually writes.

## Decision 015 - R6 BufferID Collision Repair

Problem: Source inventory found a hard BufferID collision. `CartographyVaultBufferIds.LegacyExploredBitIndexCount` used local cast `71440`, while `DynamicPointLightCullingVaultIds.Sources` already owns `71440` and its documented range continues through `71458`. If both systems run, the Vault can bind an `int[1]` legacy PDA count lane and a light-source record lane to the same numeric identity, corrupting authority and type expectations.
Solution: Keep core cartography truth lanes `71420..71437` unchanged and move optional legacy PDA cache lanes to the unused active-source range `71459..71461`: `LegacyExplorationWords=71459`, `LegacyExploredBitIndices=71460`, `LegacyExploredBitIndexCount=71461`. This avoids touching `H8Memory.cs` during a parallel batch and preserves the authoritative discovery bitmask/save/shader ABI.
Rejected Alternatives: Editing the shared `BufferID` enum in `H8Memory.cs` was rejected because the task can be repaired in the local cast range and core headers are high-conflict. Keeping only the count lane at `71459` while leaving `71438/71439` was rejected because a contiguous optional legacy block is easier to audit. Moving the core truth range was rejected because it would churn established docs and save/proof identity unnecessarily.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The change protects data sovereignty; quality still changes cadence and presentation only, not buffer identity semantics for core truth.
Hardware Impact: No frame-time improvement is claimed. The impact is failure prevention: it removes a possible cross-domain Vault alias that could produce type-size mismatch, stale generation reads, or corruption when light culling and PDA legacy cache initialize in the same session.

## Decision 016 - R6 Guarded Build Boundary

Problem: After CPU/dotnet guards cleared, `dotnet build Hecton8.Core.csproj --no-restore` reached C# compilation but failed before validating SHINOBU_350 because Construction files reference `Hecton8.Habitat.Deformation` through a namespace/assembly route that the project build did not resolve.
Solution: Record the failure as an out-of-domain compile-wall dependency: `Assets/_Project/Scripts/Construction/HatchLockJobs.cs(12,45)` and `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs(15,45)` both emit CS0234 for `Hecton8.Habitat`. Do not patch Construction or Habitat from the cartography agent.
Rejected Alternatives: Adding a direct cartography reference, moving Habitat files into Hecton8.Core, or editing Construction aliases were rejected because they are outside SONAR_CARTOGRAPHY_FOG_OF_WAR and would create cross-domain churn.
Scalability potential: No runtime quality change. The build failure is dependency routing, not cartography math or shader scalability.
Hardware Impact: The guarded build consumed 15.06s and produced no SHINOBU_350 compiler diagnostics. Further retries without dependency ownership would waste IO/CPU and violate the compile-wall protocol.

## Decision 017 - R7 Telemetry ABI Ledger And Dump Header

Problem: R7 audit found stale SHINOBU_133 ledger text still documenting the cartography telemetry ring with the obsolete pre-expansion row stride, while current source and SHINOBU_350 route cards use explicit 80-byte telemetry rows. The cartography dump header also wrote magic/version/cursor/count but did not write the record size, so a standalone forensic decoder could silently interpret the expanded ring with the old stride.
Solution: Mark the historical ledger block as superseded for active ABI purposes, document the 80-byte telemetry offsets, add `71437 CartographyState` and optional legacy PDA lanes `71459..71461`, and bump the cartography black-box dump schema to version 2 with `UnsafeUtility.SizeOf<CartographyTelemetryEntry>()` emitted before cursor/count.
Rejected Alternatives: Editing `H8Memory.cs` was rejected because no active enum collision exists after R6 and shared memory headers are high-conflict. Leaving the old ledger note was rejected because it creates a binary payload ambiguity. Adding a managed dump manifest object was rejected because the existing binary dump path only needs a fixed scalar header.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The improvement is forensic scalability: tools can decode the same truth ring regardless of quality cadence, upload frequency, or presentation tier because the row size is encoded in the dump.
Hardware Impact: Runtime frame cost is unchanged; the dump path is fault-only. The header adds one 4-byte write during crash/over-budget serialization and prevents offline misdecode of a 24KB ring.
