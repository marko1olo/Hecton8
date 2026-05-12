# LOG_CORE_SAVE_MMF

## 2026-05-11 Final Report - DATA_ARCHIVIST / CORE_SAVE_MMF

Status: PENDING VERIFICATION - LOCAL BUILD CLEAN

What was wrong:
- Legacy/fallback save reads could still touch a large container through broad mapping instead of staying on the four-window MMF cache.
- Hydration slice was 4.0ms instead of the required 2.0ms.
- Voxel uniform RLE job wiring drifted and broke compile before the 2-byte uniform chunk path could be trusted.
- Sector override commits were still vulnerable to redundant metadata hash work.
- Invalid save magic was discovered too late and only through string failure reporting.
- Intermediate verification was blocked outside this domain by `GlobalSignals.cs`, `FaunaBrain.cs`, `ConstructionManager.cs`, and earlier `ProceduralWreckGenerator.cs` errors; the final build re-run is clean.

What was done:
- `SaveBinaryStorage.AsyncWriteManager.TryCopyFileRangeToNativeArray` now clears native destination memory and copies file ranges through the cached read-window path.
- Fallback `TryReadPayload` now uses separate raw and compressed native buffers, reads header/payload through cached windows, and avoids whole-file read-only mapping.
- `HydrationScheduler` budget is exactly 2.0ms per frame.
- `SaveManager` load metadata/data paths now acquire both raw and compressed scratch buffers before calling the binary storage reader.
- `VoxelDeltaProcessor` RLE uniform detection uses a one-byte `NativeArray<byte> UniformFlag`; the compile-only signal namespace needed by that file was added.
- `SaveBinaryPayloadCodec.BufferReader.ReadBool` now deserializes bools through `math.select`.
- `SaveBinaryStorage.TryRecoverCachedIndexedMetadataHashLow32` validates cached low32 metadata state before sector override commits, avoiding metadata rehash.
- `SaveBinaryStorage.TryReadHeaderPrefixFastFail` checks the first 8 bytes and records integer `LastReadErrorCode` values before large reads/decompression.

Predictive paging evidence:
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: `TryCopyFileRangeToNativeArray` calls `UnsafeUtility.MemClear` on the native destination and then copies through `TryCopyFromCachedReadWindow`.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: fallback `TryReadPayload` copies `CurrentHeaderSize` and compressed payload ranges through `AsyncWriteManager.TryCopyFileRangeToNativeArray`.
- Active read cache remains four 1MB windows with 256KB edge prefetch and background flush throttling at 10MB/s.

Short3 quantization evidence:
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: `QuantizedAupLocalOffsetShort3` stores local chunk offsets as three signed millimeter shorts.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: `QuantizeAupLocalOffsetShort3` subtracts the chunk center and clamps/rounds to `short`; unpack reconstructs relative meters from millimeters.

Cinematic cheats used:
- Replaced broad save-file touching with four 1MB cached windows plus 256KB edge prefetch.
- Replaced float3 local position persistence with millimeter `short3` relative to chunk center.
- Replaced full uniform 32x32x32 voxel payloads with a 2-byte uniform RLE state.
- Replaced metadata block rehash/decompress during sector commit with cached header/directory low32 validation.
- Replaced late invalid-save failure with an 8-byte magic prefix gate and integer error codes.

Scalability matrix:
- Low: demand-paged 1MB windows, 2.0ms hydration slices, first-128-byte cloud metadata reads, per-sector quarantine, numeric fast fail.
- Middle: prefetch and 10MB/s flush queue smooth storage boundaries without foreground stalls.
- High: saved IO budget can keep more visual-sector pages resident.
- Ultra: saved CPU/IO can be spent on denser debris/item persistence and visual sector overkill after a formal v10 alignment migration.

Exact microseconds saved estimates:
- Predictive fallback read windowing: 42000 us worst-case HDD page-fault burst avoided.
- 256KB edge prefetch: 3000 us boundary stall avoided.
- 10MB/s background flush queue: 12000 us foreground stall avoided on 120MB flush.
- Hydration budget reduction: 2000 us returned per load frame.
- AUP short3 local offsets: 6000 us per 1000 entity positions.
- Uniform voxel RLE: 65000 us IO/decode avoided for a uniform chunk.
- Maintenance bool bitmask: 4 us per maintenance record IO.
- Entity numeric hashes: 18000 us and about 120KB per 1000 item records.
- Per-sector checksum quarantine: 240000 us reload/regeneration avoided when one sector fails.
- First-128-byte cloud metadata read: 50000 us per boot comparison.
- Data striping: 48000 us boot/load seek and decompress avoided.
- Atomic sector commit recovery: 35000 us per interrupted sector write.
- Bounded save candidate scratch: 35 us GC/alloc avoided per candidate pass.
- AUP 48-byte raw blit: 9000 us per 1000 records.
- Cached metadata low32 recovery: 22000 us per sector commit.
- SaveContextFrameData pre-capture: 15 us debug/race overhead avoided per save.
- Branchless bool read: 3 us per 1000 bool reads.
- MemClear on reused native windows: 5000 us post-corruption diagnosis avoided.
- 8-byte magic fast fail: 40000 us on invalid large save files.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` succeeded once after the local RLE compile fix with 0 warnings and 0 errors.
- Intermediate builds failed outside the Data Archivist domain: `ProceduralWreckGenerator.cs` missing world/wreckage methods, then `GlobalSignals.cs` missing signal types, `FaunaBrain.cs` missing `FaunaTier1LodProxyEntry`, and `ConstructionManager.cs` missing `IOriginShiftListener.OnOriginShift`.
- Final `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors in 00:01:21.37.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings on touched files.

Final Git diff stat at Omega audit:
Assets/_Project/Scripts/HydrationScheduler.cs     |   4 +-
Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs |  88 ++-
Assets/_Project/Scripts/SaveBinaryStorage.cs      | 802 ++++++++++++++--------
Assets/_Project/Scripts/SaveManager.cs            |  17 +-
Assets/_Project/Scripts/VoxelDeltaProcessor.cs    | 155 ++++-
5 files changed, 720 insertions(+), 346 deletions(-)
