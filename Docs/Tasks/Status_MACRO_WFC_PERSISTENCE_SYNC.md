# MACRO_WFC_PERSISTENCE_SYNC Status

Role: BACKEND_ENGINEER
Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE / Data Archivist Persistence
Prompt: MACRO_WFC_PERSISTENCE_SYNC
Status: PENDING VERIFICATION

## Mandates Read
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- STRM_Persistent_Object_Registry.txt
- STRM_ModuleDTO_LZ4_Dictionary.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- NET_Logistics_Sync_BitPacking_Reconciliation.txt

## Loop 1: Tasks 1-5
- [x] Task 1: Extend IAsyncPersistenceService | Justification: Added native-grid persist/restore calls to the registry-facing async persistence contract; DOD practice: decoupled service boundary. Alternative rejected: WFC concrete save hook. Estimate: <1 us dispatch.
- [x] Task 2: Consume WfcOutpostStateChangedSignal | Justification: SaveManager scans the WFC signal snapshot, validates sector/cell, and persists only mutable state changes; DOD practice: fixed-size signal lane. Alternative rejected: managed event callbacks. Estimate: <20 us for 8 signals.
- [x] Task 3: ASMDEF isolation Core.Database -> Contracts | Justification: WFC payload/status contracts live in `Hecton8.Core.Contracts`; `Hecton8.Core.Database` already references Contracts only. Alternative rejected: database assembly owning gameplay status enums. Estimate: 0 us runtime.
- [x] Task 4: Request NativeArray<byte> WfcGrid from Data Vault | Justification: Added `BufferID.WfcOutpostGrid` and SaveManager `GetBuffer<byte>` request through `IDataVault`; DOD practice: central native buffer ownership. Alternative rejected: SaveManager-owned persistent grid copy. Estimate: cold allocation only; hot lookup <5 us.
- [x] Task 5: Burst ulong packing job for 10x10x5 grid | Justification: Added `[BurstCompile] PackWfcOutpostMutableStateJob` packing four mutable planes into 32 `ulong` words. Alternative rejected: managed BitArray/per-byte blob. Estimate: <10 us for 500 cells.
- [x] Compile verification after Tasks 1-5 | Result: [BLOCKED BY DEPENDENCY] Contracts/Memory/Persistence/Database Unity Roslyn response-file checks pass in isolation; full Core compile is blocked by unrelated missing Audio.Virtualization, AI.Cognition, and IOutpostGenerationService references from concurrent work.

## Loop 2: Tasks 6-10
- [x] Task 6: Dirty flag only on bit change | Justification: Signal drain compares previous/current mutable bits and snapshot hash skips identical sector payloads before `MarkDirty`; DOD practice: dirty-on-transition. Alternative rejected: dirty every state signal. Estimate: <20 us for 8 signals.
- [x] Task 7: MacroDB query before WFC on SectorHydratedSignal | Justification: Bounded `SectorHydratedSignal` snapshot drain probes MacroDB and applies valid WFC payloads into the DataVault grid. Alternative rejected: per-frame MacroDB polling. Estimate: <20 us for 4 bounded probes when cached.
- [x] Task 8: Saved bitmask injection into WFC solver | Justification: Current World outpost runtime now calls `TryApplyWfcOutpostStateOverride` through `GlobalRegistry.AsyncPersistence` before scheduling solve, into a separate mutable-state grid consumed during matrix extraction. Alternative rejected: injecting mutable bits into the topology/adjacency byte grid, which corrupts cell kind masks. Estimate: restore decode <20 us plus 500-byte clear before cold generation.
- [x] Task 9: RLE/SaveBinaryPayloadCodec payload compression | Justification: Added versioned WFC bitmask payload header and byte-RLE encode/decode in existing codec. Alternative rejected: sidecar codec or JSON blob. Estimate: <=288 bytes worst case, lower for default grids.
- [x] Task 10: Absolute Sector Hash keys for AUP shift safety | Justification: WFC persistence uses the incoming absolute `sectorHash` directly for `MarkDirty`, `TryAppendDirtyPayload`, and `TryGetPayload`. Alternative rejected: derived page payload hash. Estimate: 0 us extra.
- [x] Compile verification after Tasks 6-10 | Result: [BLOCKED BY DEPENDENCY] Unity Roslyn response-file checks pass for Contracts, Memory, Persistence.Paging, and Database with current changes; full Core check is blocked by unrelated Audio.Virtualization, AI.Cognition, FaunaKinematicsRuntime, and IOutpostGenerationService references.

## Loop 3: Tasks 11-15
- [x] Task 11: Math LOD exactness note | Justification: Persisted mutable truth stays exact across all quality tiers; DOD practice: deterministic save truth separated from visual tiering. Alternative rejected: approximate/fake saved state. Estimate: 0 us runtime branch.
- [x] Task 12: Background Awaitable IO phase | Justification: MacroDB dirty append is queued through Unity `Awaitable.BackgroundThreadAsync()` and returns to main thread before telemetry. Alternative rejected: synchronous Tick-time MMF append. Estimate: hot path queues only; disk append off main thread.
- [x] Task 13: Zero-GC packing audit | Justification: WFC hot path uses fixed `NativeArray` buffers, `ReadOnlySpan` signal snapshots, for-loops, and a Burst `IJob`; anti-bloat removed a written-only append counter and replaced four branch tests with masked OR writes. Alternative rejected: managed `BitArray`, arrays, LINQ, or branch-heavy bool packing. Estimate: 0 B managed allocation in packing path; measured proof absent.
- [x] Task 14: Telemetry WfcBytesSaved | Justification: Successful persisted payload publishes `GlobalTelemetryBus.PublishModTelemetry(WFCP, WFBS, savedBytes)`. Alternative rejected: string log or per-sector managed diagnostic event. Estimate: <1 us telemetry enqueue.
- [x] Task 15: Burst compile check for ulong packing loop | Justification: [BLOCKED BY DEPENDENCY] Unity/Bee Roslyn response-file checks pass for Contracts, Memory, Persistence.Paging, and Database; `Hecton8.Core` Bee compile reaches unrelated Audio Virtualization, AI Cognition/Fauna, and `IOutpostGenerationService` blockers. Actual Burst import remains blocked by Unity MCP/editor unavailability. Alternative rejected: claiming Burst verified without Unity import. Estimate: packing target <10 us for 500 cells.
- [x] Compile verification after Tasks 11-15 | Result: [BLOCKED BY DEPENDENCY] Support assemblies touched by this pass compile via Unity/Bee response files. Full `Hecton8.Core` remains blocked by unrelated Audio/AI/Outpost generation dependencies; Unity MCP console transport is unavailable at 127.0.0.1:8088.

## Loop 4: Recursive Re-Verification
- [x] Re-extract prompt after every 3 task completions | Result: extraction complete after Task 3, Task 6, and Task 9+ from Docs/Tasks/CURRENT_BATCH.md using CLI regex against the XML tag.
- [x] Re-read own code for missed dependency/corruption cases | Result: static scan found the current World WFC grid packs topology/adjacency into the same byte lanes as mutable persistence flags; fixed via separate mutable-state grid and metadata merge instead of topology overwrite.
- [x] Bitmask length mismatch guard discards invalid DB payload | Result: codec validates magic/version/header dimensions/plane count/word count/raw length/stored length; SaveManager rejects corrupt length and leaves fresh WFC base path available.

## Loop 5: Omega Polish
- [x] Read POLISH_MANDATE after all tasks done or blocked | Result: read only after Tasks 1-15 and recursive guards were checked or dependency-blocked.
- [x] Execute anti-bloat pass | Result: removed written-only WFC append counter/`System.Threading`, converted Burst packer to branchless masked OR writes, confirmed one MacroDB compaction method set plus one `FrostTick`, and reran Unity/Bee response-file verification.

## Loop 6: Post-Compaction Recheck
- [x] Re-extract prompt | Result: `Docs/Tasks/CURRENT_BATCH.md` XML block extracted with id-attribute regex because the tag carries role/chat attributes. Alternative rejected: exact tag text match. Estimate: 40 us parse after disk read.
- [x] Payload validation hardening | Result: WFC codec now rejects unknown payload flags and raw payloads whose stored length is not exactly the expected raw bitmask length. Alternative rejected: accepting forward flags silently. Estimate: 0 us on valid payload except one mask check.
- [x] Branchless restore unpack | Result: restore path now reads the four mutable bit planes with direct shifts/ORs, removing four branch tests per cell. Alternative rejected: readable branch loop. Estimate: 2,000 branch tests removed per full restore.
- [x] Sector contamination guard | Result: SaveManager tracks the sector currently represented by the DataVault mutable grid, clears/restores on sector switch, then applies the incoming changed cell. Alternative rejected: reusing one global mutable grid across sector hashes. Estimate: cold sector switch clear = 500 byte writes.
- [x] World mutable-state integration | Result: `MarauderOutpostGenerationService` owns `_wfcMutableStateGrid`, restores it via `IAsyncPersistenceService`, and `MarauderOutpostMatrixExtractionJob` merges mutable bits into shell/proxy metadata without overwriting topology/adjacency bits. Alternative rejected: direct topology grid injection. Estimate: one 500-byte clear and one interface call per cold generation.
- [x] Verification rerun | Result: `Hecton8.Core.Contracts` response-file check passes; `dotnet build Hecton8.Core.csproj` remains blocked by unrelated missing Audio/AI/Physics/World contracts; Unity/Bee support check now fails earlier in `Hecton8.Core.Memory` due unrelated GlobalDataVault defrag symbols from concurrent work; `Hecton8.World.Outposts` response check is blocked by missing stale `1300` `Hecton8.Core.ref.dll`.

## Loop 7: Mutable-State Purity Recheck
- [x] Zero-hash generation clear | Result: World outpost mutable-state restore now clears `_wfcMutableStateGrid` before rejecting `sectorHash == 0`, preventing stale restored bits during debug/invalid generation requests. Alternative rejected: early-return before clear. Estimate: 500 byte writes on cold generation only.
- [x] Exact mutable-grid writes | Result: SaveManager now writes exact low-nibble mutable flags into mutable-state grids instead of preserving non-mutable bits. Alternative rejected: carrying topology-preservation code after the contract moved to separate mutable grids. Estimate: one mask/OR removed on changed-cell write and restore cell write.
- [x] Recheck verification | Result: `Hecton8.Core.Contracts` response-file check still passes; full `dotnet build Hecton8.Core.csproj` timed out at 132 s under the existing project-wide dependency wall, and no lingering dotnet/MSBuild process remained after timeout.

## Loop 8: Binary Boundary And Extraction Cost Recheck
- [x] Exact WFC payload length | Result: `SaveBinaryPayloadCodec.TryReadWfcOutpostBitmaskPayload` now requires `length == PayloadHeaderBytes + storedBytes`, so trailing bytes in a MacroDB WFC payload reject as corruption. Alternative rejected: accepting valid prefix plus trailing garbage. Estimate: one integer equality check on restore.
- [x] Matrix extraction mutable read cost | Result: `MarauderOutpostMatrixExtractionJob` now reads `MutableGrid[cellIndex]` directly; the per-solid-cell `IsCreated`/length branch was removed because the service allocates the grid with `WfcGrid`. Alternative rejected: defensive per-cell branch in a Burst extraction loop. Estimate: one branch and one length compare removed per solid extracted cell.
- [x] Static verification | Result: grep confirms exact-length guard, no `MutableGrid.IsCreated` branch in outpost extraction, no old `UnpackWfcOutpostGrid`, and no `immutableMask` in SaveManager WFC path. `Hecton8.Core.Contracts` response-file check still passes.

## Loop 9: Signal Backpressure And Telemetry Recheck
- [x] Batch-file extraction recheck | Result: `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="MACRO_WFC_PERSISTENCE_SYNC">`; current batch file appears rotated to other prompts. Continued from this status file and rationale log to avoid false extraction claims. Alternative rejected: fabricating a fresh prompt extraction. Estimate: 0 us runtime.
- [x] Full WFC signal snapshot drain | Result: `DrainWfcOutpostStateChangedSignals` now scans the full bounded `WfcOutpostStateChangedSignal` snapshot instead of only the first 8 entries, applies same-sector mutations into the mutable grid, and persists once per dirty sector group. Alternative rejected: silent drop of valid entries 9..128. Estimate: common same-sector burst saves up to 7 redundant 500-cell pack passes versus the old 8-signal cap path; worst-case alternating sectors remains bounded by signal lane capacity.
- [x] WfcBytesSaved baseline correction | Result: telemetry now reports saved bytes versus the old 500-byte mutable grid baseline: `CellCount - payloadBytes`, not `PackedWordBytes - payloadBytes`. Alternative rejected: reporting 0 bytes saved for the 288-byte worst-case packed payload. Estimate: <1 us integer subtraction; data quality improvement only.
- [x] Verification recheck | Result: static scans confirm full snapshot loop, no old state-signal cap constant, exact payload boundary guards, direct mutable extraction read, and corrected telemetry baseline. `Hecton8.Core.Contracts` Bee/Roslyn response-file compile exits 0. `Hecton8.Core` Bee/Roslyn response-file compile remains blocked by unrelated Audio Virtualization, AI Cognition/Fauna, UI Diegetic, World Ore, Outpost generation, and Power WFC dependency errors before a clean runtime/Burst proof can be produced.

## Loop 10: Worktree Drift Reapplication
- [x] Re-read current code after user continuation | Result: `SaveManager.cs` had drifted back to the old 8-entry WFC state-change cap while prior status/rationale expected the full snapshot fix. Alternative rejected: trusting stale logs over current file contents. Estimate: 0 us runtime.
- [x] Reapply WFC signal batching | Result: restored the full snapshot loop, lazy DataVault grid resolve, contiguous sector-group persistence, stale cap removal, and `CellCount - payloadBytes` telemetry baseline. Alternative rejected: leaving docs/code mismatch for the Integrator. Estimate: common same-sector burst again avoids up to 7 redundant pack passes versus the reverted cap path.
- [x] Verification recheck after drift fix | Result: `git diff --check` reports no whitespace errors for touched files; static scans confirm no `MaxWfcOutpostStateSignalsPerTick`, full signal snapshot loop, exact codec guards, direct mutable extraction read, and corrected telemetry baseline. `Hecton8.Core.Contracts` Bee/Roslyn response-file compile exits 0; `Hecton8.Core` still fails on unrelated Audio Virtualization, AI Cognition/Fauna, Prologue, Outpost generation, Power WFC, and World Ore symbols.
