# LOG_SHINOBU_34

## 2026-05-17 - SHINOBU_MERKLE_TREE Closeout

What was wrong:
- Save delta architecture had WAL/MMF paging support in `H8BinaryWorldPager`, but no SHINOBU_34-owned Merkle tree layer that hashes DataVault-style unmanaged state leaves with XXHash3-128.
- Existing save compression around the pager was record/page oriented; it did not provide strict 16KB LZ4 subblocks with per-subblock CRC and selective load/decompression.
- Historic save docs/logs still mention v8/v9/v10 header sizes; implementing blind against v8 would have produced aligned-looking but semantically stale data.
- Dynamic/rest-state policy was missing from the Merkle layer; saving exact transient transforms would bloat files and lie about simulation authority.
- Human control for sector corruption and changed-branch visibility did not exist for this Merkle path.

What was done:
- Added `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs`.
- Added aligned blittable DTOs: `MerkleNodeDTO` 32B, `SectorEntry` 32B, `StateDeltaRecordDTO` 64B, `StateLeafDescriptor` 32B, `Lz4SubBlockHeader` 32B, `SaveMerkleWalAppendHeader` 64B, `SaveMerkleTelemetryEntry` 64B, `SaveMerkleEmergencyHeader64` 64B.
- Added Burst jobs for mock inventory generation, 4-byte deep mutation, mock descriptor generation, XXHash3-128 leaf hashing, 16-way branch reduction, changed-leaf delta extraction, tombstone pruning, LZ4 subblock compression, Dear Lie dehydration, tree copy, and telemetry ring writes.
- Added fixed native-arena delta extraction instead of auto-growing lists: delta header plus raw bytes, overflow flag, no managed growth.
- Added `TryAppendCompressedWalMmf()` for `slot_0.wal` with MMF write path, 64-byte header, record CRC, and FileStream fallback.
- Added WAL validation/rollback semantics: core WAL corruption restores `.bak`; MODP sidecar corruption is skipped/dropped.
- Added CSV override parser for `save_schema_overrides.csv` using `NativeArray<byte>` scratch and ASCII key hashes; supported keys: `sub_block_size`, `wal_bytes_per_second`, `math_lod`, `drop_cosmetic_threshold`.
- Added `State Delta X-Ray` editor window in `WalXRayWindow.cs` with Merkle snapshot display, changed-leaf grid, WAL validation, and `Corrupt Sector`.
- Added `H8WalInspector.TryCorruptSectorBytes()`.
- Added SaveMerkle BufferIDs `70270..70281` in `H8Memory.cs`.
- Added layout assertions in `BinaryLayoutManifest.cs`.
- Added `SaveStateMerkleTree.cs` to `Directory.Build.targets`.

Cinematic cheats used:
- Dear Lie snapshot: moving/distant objects save stable rest or `NeedsWake` plus quantized AUP, not exact transient rotation.
- Mod payload quarantine: MODP sectors can be dropped on CRC failure; base sectors remain authoritative.
- LZ4 raw fallback: incompressible subblocks stay raw instead of wasting CPU for negative compression.
- Low-tier I/O throttle: slow MicroSD path clamps to 16MB/s and drops cosmetic payload pressure before it stalls gameplay.

Exact microseconds saved / estimated:
- Zero-init bypass for 4369 Merkle nodes: estimated 2-5 us saved per tree allocation/init.
- Raw DTO/no-property diff path: estimated 0.4 us saved per 4096-leaf diff pass by avoiding property copies.
- Aligned 32-byte SectorEntry vs historic 28-byte layout: estimated 1-2 us avoided on ARM64 directory scans and no unaligned trap risk.
- Merkle branch reduction: estimated 20-60 us for 4369 nodes.
- Delta extraction: estimated 2-6 us per changed leaf plus memcpy instead of full-world rewrite.
- Tombstone prune: estimated 1-3 us per 1000 records plus memcpy, prevents long-play file growth.
- Throttle calculation: estimated 0.05 us; saves frame stalls rather than CPU.
- Telemetry ring write: estimated 0.2 us; dump is cold fault path.
- CSV override parse: estimated <50 us cold for tiny CSV, 0 us when timestamp unchanged.

Verification:
- `dotnet build .\Hecton8.Core.csproj /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` succeeded once with `0 Warning(s), 0 Error(s)` after restore regenerated Temp assets.
- Later core compile failed on concurrent external `ShinobuLogisticsRouter` errors, not SHINOBU_34 code.
- Editor compile retry failed on external editor/reference issues (`BlackboxXRayViewer`, `VerletTowTunerWindow`, `EconomyRecipeTunerWindow`, `SignalTrafficMonitorWindow`), not `WalXRayWindow`/State Delta code.
- Static scan on owned files found no `Pack=1`, no save JSON, no managed byte arrays, no `File.ReadAllBytes`, no `NativeList`, and no DTO properties.

Residual risk:
- No Unity Play Mode save/load roundtrip, profiler, GCMonitor, or Memory Profiler run was available in this pass.
- `TryAppendCompressedWalMmf()` is a worker-thread primitive; integration should call it from the existing save pager/background owner rather than the main thread.

## 2026-05-18 - SHINOBU_MERKLE_TREE Ultra-Polish Reconciliation

What was wrong:
- The previous low-tier cosmetic policy was not hard enough: LZ4 reported over-threshold cosmetic pressure after compression, but that did not physically remove bytes from the WAL payload.
- That meant the report could claim cosmetic drops while the compressed stream still carried the payload. False telemetry is worse than no telemetry in a save path.

What was done:
- Updated `SaveStateMerkleTree.cs` so `ScheduleCosmeticPayloadPrune()` runs a real fixed-arena compaction stage before LZ4.
- `CosmeticDeltaPayloadPruneJob` now parses each `StateDeltaRecordDTO`, drops `LeafFlagCosmetic` records only when `CosmeticDropThresholdBytes` is exceeded, rewrites `DeltaPayloadOffset`, clears `CompressedOffset`, and writes exact dropped byte/record counters.
- `Lz4SubBlockCompressionJob` no longer owns cosmetic policy and no longer writes fake drop counters. It now reports only stored bytes, block count, raw bytes, and failure state.
- Reset LZ4 counters at job start so stale failure state cannot bleed from a previous run.

Cinematic cheats used:
- Low-tier autosave now discards cosmetic-only delta records before compression. Gameplay truth remains in non-cosmetic sectors; visual continuity is rebuilt by runtime presentation after load.
- The stable-state Dear Lie remains unchanged: transient motion is saved as rest/needs-wake plus quantized AUP, not exact presentation transforms.

Exact microseconds saved / estimated:
- Cosmetic prune pass: estimated 8-35 us on i3/MX350 for large autosave spikes, dominated by linear memmove. The value is reduced WAL bytes and fewer MicroSD stalls, not CPU magic.
- LZ4 counter reset: sub-microsecond; prevents stale failure telemetry from causing false recovery work.
- Removing fake LZ4 drop accounting: 0 us meaningful CPU gain; fixes correctness of reported save shrink.

Verification:
- Static scan on `SaveStateMerkleTree.cs` and `WalXRayWindow.cs`: no `JsonUtility`, `System.Text.Json`, `new byte[]`, `File.ReadAllBytes`, `.ToArray()`, `NativeList`, `Pack=1`, LINQ query calls, or `foreach`.
- `git diff --check` was clean for SHINOBU_34-owned files. Git reported only line-ending warnings on existing tracked files.
- `dotnet build .\Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` failed on external compile walls in `GlobalTelemetryBus.Blackbox.cs`, `GlobalPhysicsStateManager.cs`, and `SubmarineDynamicsRuntime.cs`; no `SaveStateMerkleTree.cs` errors appeared in the captured output.

Residual risk:
- Runtime Unity Play Mode save/load roundtrip, profiler, GCMonitor, and Memory Profiler were not available in this pass.
- The Merkle layer exposes the prune stage as an explicit scheduled job. The save orchestrator must schedule it between delta extraction and LZ4 compression using vault-owned buffers.

## 2026-05-18 - SHINOBU_MERKLE_TREE Pipeline Hardening

What was wrong:
- The save path still had a hidden synchronization hazard: delta extraction writes byte length to counters, but a caller could read that counter on the main thread before scheduling LZ4. That pattern creates an autosave microfreeze under load.
- Several DTOs were size-aligned but not laid out with 8-byte lanes first. Size-only padding is not enough for ARM64 audit.
- `State Delta X-Ray` displayed count-based hot cells instead of the actual changed Merkle branches.

What was done:
- Added `TryResolveVaultBuffers()` to resolve all SHINOBU_34 buffers from `IDataVault` and `BufferID.SaveMerkle*`: current/previous trees, descriptors, delta records, delta bytes, pruned bytes, compressed bytes, LZ4 headers, telemetry ring, counters, and LZ4 hash table.
- Added `BufferID.SaveMerkleLz4HashTable = 70282`.
- Added `ScheduleVaultDeltaWalPipeline()` to schedule Merkle build, root-aborted delta extraction, cosmetic prune, LZ4 subblocks, and previous-tree copy as one dependency chain without mid-frame `Complete()`.
- Moved LZ4 counters to slots 8-11 so delta/prune telemetry survives compression.
- Rebuilt layouts and manifest assertions for `StateDeltaRecordDTO`, `Lz4SubBlockHeader`, `SaveMerkleWalAppendHeader`, `SaveMerkleTelemetryEntry`, `SaveMerkleEmergencyHeader64`, and `SaveMerkleEditorSnapshot`.
- Added a 256-bit changed-branch mask to `SaveMerkleEditorSnapshot`; `State Delta X-Ray` now paints actual Level2 dirty branches.

Cinematic cheats used:
- Low-tier still drops cosmetic-only deltas before compression when storage pressure crosses the threshold.
- Dear Lie save remains stable-rest/needs-wake plus quantized AUP, not exact transient transform state.

Exact microseconds saved / estimated:
- Counter-driven job chain: removes an estimated 0.05-0.6 ms main-thread sync-risk spike on busy autosaves.
- 8-byte-first WAL/telemetry layouts: estimated 1-3 us avoided on ARM64 scan/validation passes by reducing unaligned lane surprises.
- Actual X-Ray branch mask: editor-only; zero player runtime cost.

Verification:
- Static scan on owned runtime/editor files found no `Pack=1`, no JSON APIs, no `new byte[]`, no `File.ReadAllBytes`, no `.ToArray()`, no `NativeList`, no LINQ query calls, no `foreach`, no local `new NativeArray`, and no `.Complete()` in `SaveStateMerkleTree.cs`.
- `git diff --check` was clean for SHINOBU_34-owned files; git still reports line-ending warnings on pre-existing tracked files.
- `dotnet build .\Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` succeeded: 9 warnings, 0 errors.
- `dotnet build .\Hecton8.Editor.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` failed on external editor windows/tuners (`BlackboxXRayViewer`, `ResidencyStreamingTunerWindow`, `EconomyRecipeTunerWindow`, `VerletTowTunerWindow`, `SubmarineDynoTunerWindow`, `SignalTrafficMonitorWindow`). No `WalXRayWindow.cs` error appeared.

Residual risk:
- No Unity Play Mode save/load roundtrip, GCMonitor, profiler, Memory Profiler, or device I/O trace was available in this pass.
- Runtime readiness remains PENDING VERIFICATION until the save orchestrator calls the new pipeline from the project save cadence and a WAL roundtrip artifact exists.
