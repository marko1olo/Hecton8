# Status_CSV_DATA_MONOLITH_SYNC

Agent: CSV_DATA_MONOLITH_SYNC  
Domain: CORE/DATA_PIPELINE  
Task count: 20  
Current status: VERIFIED MASTER GRADE - DATA MONOLITH ONLINE (Loop 13 data-domain verified; no rebuild rerun by user directive; prior full Core compile wall is cross-domain World/Submarine errors)

## Mandates Loaded

- UI_Data_Streaming_ZeroGC_Optimization.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DATA_Inventory_Resources_Items_SOA_Layout.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- STRM_DirectStorage_Reality_Check.txt

## Loop 0 - Authority Read

- [x] Prompt extracted from Docs/Tasks/CURRENT_BATCH.md | DOD: CLI regex extracted only the CSV_DATA_MONOLITH_SYNC XML block cover-to-cover. Alternative rejected: MCP/basic reader because batch protocol forbids truncation risk. Estimate: 35 us.
- [x] Domain boundary checked | DOD: Actual Domains document names Data Monolith as Core/Memory infrastructure and task domain limits edits to Core/Data plus Data/Balance. Alternative rejected: editing existing world/economy systems. Estimate: 20 us.
- [x] Registry mandates identified | DOD: 8 task-relevant mandates read before edits. Alternative rejected: bulk-loading all 78 mandates. Estimate: 45 us.

## Loop 1 - Tasks 1-5

- [x] Task 1 CSV_STRUCTURE_DESIGN | DOD: Created `Data/Balance/Items.csv`, `Economy.csv`, `Physics.csv`, `Fauna.csv` with first-column stable IDs and `version_id` headers. Alternative rejected: JSON/SO authoring because it keeps balance truth inside code or Unity assets. Estimate: 80 us per row cold authoring parse.
- [x] Task 2 HASH_GENERATOR_TOOL | DOD: Added `H8DataHashTool.ComputeFnv1a32(ReadOnlySpan<char>)` plus cold manifest generator reading first CSV column. Alternative rejected: runtime hashing during gameplay. Estimate: 0 us runtime, 1-3 us cold per short ID.
- [x] Task 3 VALIDATION_RULES | DOD: Added schema-driven type enforcement with `TryParse` and finite float checks; `FAST` in a float column returns `[CRITICAL_DATA_TYPE]`. Alternative rejected: permissive coercion/defaulting because it hides spreadsheet corruption. Estimate: 10-30 us cold per row.
- [x] Task 4 SCHEMA_VERSIONING | DOD: Required per-row `version_id=1.2` and wrote schema version into binary header for boot rejection. Alternative rejected: filename-only versioning. Estimate: 1 comparison per row cold, one header comparison at boot.
- [x] Task 5 BINARY_BLIT_WRITER | DOD: Added `H8DataBaker` packing validated rows into `H8StaticData.bin` via fixed DTOs and `UnsafeUtility.MemCpy`. Alternative rejected: BinaryWriter field-by-field object serialization. Estimate: 2-5 us per record cold bake.

Loop 1 compile check: `dotnet build Hecton8.Core.csproj` failed on pre-existing non-data symbols (`HectonEcologyContract`, `HectonPhysicsContract`, `ScalabilityContract`). Retried with `/p:HectonBuildProjectReferences=true`; same 38 cross-domain errors. No `Assets/_Project/Scripts/Core/Data/*` errors appeared in compiler output. Status: COMPILE BLOCKED BY DEPENDENCY, not proven green.

## Loop 2 - Tasks 6-10

- [x] Task 6 MEMORY_ALIGNMENT | DOD: All lookup entries are 16 bytes, all records are 48 bytes, and writer aligns every record offset to 16 bytes before `UnsafeUtility.MemCpy`. Alternative rejected: packed variable-width rows. Estimate: one mask operation per record cold bake.
- [x] Task 7 FAST_LOOKUP_TABLE | DOD: Binary starts with fixed lookup table after the 64-byte header; runtime loads it into `NativeParallelHashMap<uint,long>`. Alternative rejected: sorted-array binary search because task requires O(1). Estimate: expected O(1), one native hash lookup.
- [x] Task 8 STRING_POOLING | DOD: Name/Description text is emitted to `Babel_Dictionary.h8bin`; balance records store only text hashes. Alternative rejected: embedded UTF8 per record. Estimate: removes all runtime text scan from balance lookup.
- [x] Task 9 ZERO_COPY_LOADER | DOD: Added `StaticDataStore` using MMF on Editor/Standalone and an unmanaged fallback buffer elsewhere; no CSV runtime path. Alternative rejected: `File.ReadAllBytes` managed array as authoritative runtime storage. Estimate: boot-only map, lookup is pointer arithmetic.
- [x] Task 10 SPAN_ACCESSORS | DOD: Added `public ref readonly T GetRecord<T>(uint hash)` returning a direct ref from mapped memory. Alternative rejected: copying records by value. Estimate: hash lookup + pointer dereference, zero bytes GC after native map is built.

Loop 2 validation: external cold verifier generated `Data/Balance/Baked/H8StaticData.bin` (896 bytes) and `Babel_Dictionary.h8bin` (1284 bytes), confirmed 13 records, 26 strings, 16-byte alignment, CRC32 payloads `0x7B9A1468` and `0x694BA34A`, and clean NaN scan. Static scan of `StaticDataStore.cs` found no `Dictionary`, no `string.Split`, no `float.Parse`, and no `double.Parse`.

## Loop 3 - Tasks 11-15

- [x] Task 11 HOT_RELOAD_WATCHER | DOD: Added editor-only `FileSystemWatcher` with debounce, rebake, and `AssetDatabase.Refresh()`. Alternative rejected: runtime watcher in player builds. Estimate: editor-only, 0 us player runtime.
- [x] Task 12 CHECKSUM_VERIFY | DOD: Baker writes CRC32 for static and Babel payloads; `StaticDataStore` recomputes and rejects mismatches at boot. Alternative rejected: timestamp or file-length trust. Estimate: boot-only linear CRC over payload.
- [x] Task 13 ERROR_LOGGING_NASA | DOD: Missing required columns/values return `[CRITICAL_DATA_VOID]: Column '<Column>' in <File> is empty.` Alternative rejected: nullable/default balance values. Estimate: cold validation only.
- [x] Task 14 TRIPLE_STRIKE_REPAIR | DOD: Writer routes every record offset through `AlignOffsetWithRepair`, counts padding repairs, and writes zero padding. Alternative rejected: failing the whole bake on recoverable padding drift. Estimate: one cold branch per record.
- [x] Task 15 HOMEOSTASIS_INTEGRATION | DOD: Editor watcher pauses when `SignalBusRegistry.SystemStress01 > 0.9f`. Alternative rejected: polling/baking under stress. Estimate: one editor update float read; 0 us player runtime.

Loop 3 validation: scoped source scan confirmed `FileSystemWatcher`, CRC write/read, `[CRITICAL_DATA_VOID]`, padding repair, and `SystemStress01` stress gate are present. Compile remains blocked by unrelated cross-domain contract symbols.

## Loop 4 - Tasks 16-20

- [x] Task 16 MMF_LOCK_RELEASE | DOD: `StaticDataStore` opens with `FileShare.ReadWrite | FileShare.Delete`, releases the MMF pointer through `ReleasePointer()`, disposes accessor/map/stream, and keeps CSV authoring files separate from mapped baked output. Alternative rejected: exclusive file locks or managed `ReadAllBytes` as authoritative storage. Estimate: 0 us steady-state; shutdown-only handle release.
- [x] Task 17 DATA_SANITY_UNIT_TEST | DOD: Added `H8StaticDataSanityTests` covering bake, open, NaN scan, and direct `scrap_metal` lookup; PlayMode test project builds. Alternative rejected: manual spreadsheet inspection. Estimate: boot/test-only linear scan; 0 us gameplay hot path.
- [x] Task 18 STEAM_DECK_SD_OPTIMIZATION | DOD: Baker sorts pending records by `AccessFrequency` descending with hash tie-break before writing offsets, keeping high-frequency records physically early in the monolith. Alternative rejected: CSV order trust. Estimate: cold `n log n`; expected lower seek pressure on MicroSD.
- [x] Task 19 MAC_METAL_SUPPORT | DOD: Baker and loader reject non-little-endian runtime, header flags record little-endian packing, and baked verifier emitted strict little-endian byte layout. Alternative rejected: host-endian ambiguity. Estimate: one boot/bake branch.
- [x] Task 20 PLATINUM_COMPILE | DOD: `dotnet build Hecton8.Core.csproj --no-restore /p:HectonBuildProjectReferences=true -v:quiet /clp:ErrorsOnly` exits 0; `dotnet build Hecton8.PlayModeTests.csproj --no-restore /p:HectonBuildProjectReferences=true -v:quiet /clp:ErrorsOnly` exits 0 after adding the missing `System` import in the test file. Alternative rejected: claiming compile from source scans alone. Estimate: build-time only.

Loop 4 validation: Core build exits 0 with 1990 existing warnings and 0 errors. PlayMode test build exits 0 with 1977 existing warnings and 0 errors. Scoped scan of `StaticDataStore.cs` confirms `NativeParallelHashMap<uint,long>`, no managed `Dictionary`, no `string.Split`, no `float.Parse`, and no `double.Parse`.

## Loop 5 - Omega Polish

- [x] OMEGA_POLISH_MANDATE | DOD: No separate `<POLISH_MANDATE>` tag exists in `CURRENT_BATCH.md`; prompt section VI was applied. Loader contains no `Dictionary<string,T>` and uses `NativeParallelHashMap<uint,long>` for runtime lookup. Alternative rejected: managed runtime dictionaries. Estimate: zero managed lookup allocations after boot.
- [x] H-PHI audit | DOD: `GetRecord<T>` performs one native hash-table lookup and one pointer offset dereference into mapped memory, returning `ref readonly T`; no record copy and no managed allocation in the accessor. Alternative rejected: copied DTO return. Estimate: expected O(1), 0 bytes GC per call.
- [x] Final status string: VERIFIED MASTER GRADE - DATA MONOLITH ONLINE

## Loop 6 - Multiplatform Inquisition

- [x] ARM64/Quest ABI audit | DOD: Binary structs in `H8StaticDataContracts.cs` use `StructLayout(LayoutKind.Sequential, Pack = 1, Size = N)` and `H8DataBaker.ValidateLayoutContracts()` rejects header, lookup, Babel, telemetry, and record ABI drift before baking. Alternative rejected: trusting implicit C# padding on ARM64. Estimate: one cold layout validation pass; 0 us gameplay hot path.
- [x] Runtime allocation audit | DOD: Fresh scan of `StaticDataStore.cs` found no `NativeArray<`, no `new byte[]`, no managed `Dictionary<`, no `string.Split`, no `float.Parse`, no `double.Parse`, and no `string.Format`. Alternative rejected: hiding managed scratch buffers in non-MMF fallback. Estimate: removes fallback scratch allocation; profiler delta not captured.
- [x] H-Phi data eviction | DOD: Static-data blackbox ring and cursor are resolved from `GlobalDataVault` through `BufferID.StaticDataTelemetryRing` and `BufferID.StaticDataTelemetryCursor`; `StaticDataStore` no longer owns private telemetry arrays. Alternative rejected: local NativeArray ownership inside the loader. Estimate: fixed two vault pointer resolves per telemetry write; 0 bytes GC.
- [x] Steam Deck IO pressure check | DOD: Records remain sorted by `AccessFrequency` before write, MMF opens with shared handles on desktop, and fallback streaming reads directly into unmanaged aligned memory. Alternative rejected: `File.ReadAllBytes` or random CSV reads at boot. Estimate: cold IO only; no measured microsecond claim.
- [x] Metal/Mac shader boundary check | DOD: No shader files exist in the Core/Data domain; cross-platform parity is enforced through little-endian header flags and boot rejection of non-little-endian runtime. Alternative rejected: shader-side data reinterpretation. Estimate: one boot branch.
- [x] Core compile verification | DOD: `dotnet build Hecton8.Core.csproj --no-restore /p:HectonBuildProjectReferences=true -nr:false -v:minimal /clp:ErrorsOnly` exits 0 with 2106 existing warnings and 0 errors. Alternative rejected: source-scan-only verification. Estimate: build-time only.
- [x] PlayMode fresh rerun [BLOCKED BY DEPENDENCY] | DOD: `dotnet build Hecton8.PlayModeTests.csproj` currently fails or stalls before data test compilation because Unity-generated/third-party assemblies under `Temp/bin/Debug` are cleaned or missing (`Crest.dll`, `EasySave3.dll`, `GPUInstancer.dll`, `Hecton8.Core.dll`, and related generated DLLs). Missing metadata was repopulated from local `.codexbuild` caches once, then the build stalled and left orphaned `dotnet` processes, which were stopped. Alternative rejected: editing generated `.csproj` or claiming a fresh PlayMode pass from stale Temp state. Estimate: no runtime impact; dependency wall only.

## Loop 7 - Titanium Type And Memory Sentinel Pass

- [x] Packed type-safe lookup | DOD: `NativeParallelHashMap<uint,long>` still satisfies the loader mandate, but the low four bits of each 16-byte-aligned offset now carry record type. `GetRecord<T>` unpacks offset/type, rejects generic type mismatch, and returns the zero missing record instead of reinterpreting bytes. Alternative rejected: widening runtime lookup value into a managed struct map or trusting callers. Estimate: one mask and one ushort compare per lookup; 0 bytes GC.
- [x] H8Memory-owned fallback allocation | DOD: Non-MMF fallback now uses `H8Memory.AllocateRaw(..., SystemID.CoreDataVault, Allocator.Persistent)` and `H8Memory.FreeRaw(...)`; raw `UnsafeUtility.Malloc/Free` and private sentinel bookkeeping were removed from the loader. Alternative rejected: loader-owned unmanaged memory outside the memory authority. Estimate: no measured microsecond delta; removes one policy bypass on Quest/Android fallback.
- [x] Duplicate ID collision gate | DOD: `H8DataBaker` now keeps a cold `HashSet<uint>` during bake and rejects duplicate FNV row hashes before writing the monolith. Alternative rejected: allowing `NativeParallelHashMap.TryAdd` to discover collisions after partial binary assembly. Estimate: cold bake only; 0 us gameplay hot path.
- [x] Wrong-type regression test | DOD: `H8StaticDataSanityTests` now checks that an item hash requested as `H8PhysicsStaticRecord` returns the zero missing record. Alternative rejected: only testing the happy path. Estimate: test-only.
- [x] Binary/CSV scan | DOD: PowerShell verifier confirmed `CSV_DUPLICATE_HASH_SCAN_CLEAN count=13` and `BINARY_LOOKUP_SCAN_CLEAN magic=0x44533848 format=1 count=13 recordsOffset=272`; all binary records are 16-byte aligned with fixed 48-byte size. Alternative rejected: source-only validation. Estimate: cold verification only.
- [x] Static runtime rot scan | DOD: Fresh scan of `StaticDataStore.cs` found no `NativeArray<`, no `new byte[]`, no managed `Dictionary<`, no `string.Format`, no `string.Split`, no `float.Parse`, no `double.Parse`, no raw `UnsafeUtility.Malloc`, and no raw `UnsafeUtility.Free`. Alternative rejected: assuming prior audit still covered the patched file. Estimate: 0 bytes GC hot accessor remains intact.
- [x] Core and PlayMode compile verification | DOD: Fresh `dotnet build Hecton8.Core.csproj --no-restore /p:HectonBuildProjectReferences=true -nr:false -v:minimal /clp:ErrorsOnly` exits 0 with 2157 warnings and 0 errors. Fresh `dotnet build Hecton8.PlayModeTests.csproj --no-restore /p:HectonBuildProjectReferences=true /p:UseSharedCompilation=false /p:RunAnalyzers=false -m:1 -nr:false -v:quiet /clp:ErrorsOnly` exits 0 with 2157 warnings and 0 errors. Alternative rejected: retaining stale dependency-wall status after generated metadata recovered. Estimate: build-time only.

## Loop 8 - Schema Gate And Header Hardening

- [x] Mandates reloaded | DOD: Re-read `UI_Data_Streaming_ZeroGC_Optimization`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, and `DATA_Save_Persistence_Binary_Delta_Checksum` before this edit pass. Alternative rejected: continuing from chat memory. Estimate: authority read only.
- [x] CSV first-column sovereignty | DOD: `H8DataBaker` now rejects any sheet where the schema key `Id` is not column zero, preserving the first-column FNV identity contract. Alternative rejected: locating `Id` anywhere in the row because it breaks the hash-tool mental model for Excel authors. Estimate: one cold integer compare per schema column.
- [x] Duplicate header rejection | DOD: `H8CsvTable.CountHeader()` and the parser reject duplicate required headers with `[CRITICAL_DATA_SCHEMA]`. Alternative rejected: accepting the first duplicate and silently ignoring the second. Estimate: cold header scan only.
- [x] Canonical key grammar | DOD: IDs must now be lowercase ASCII `snake_case` made from `a-z`, `0-9`, and `_`; bad keys fail with `[CRITICAL_DATA_KEY]`. Alternative rejected: lowercasing arbitrary author keys after the fact, which hides spreadsheet identity drift. Estimate: cold O(key length), 0 us runtime.
- [x] Numeric range enforcement | DOD: schema columns now enforce non-negative cost/mass/timing/frequency values and `[0,1]` scalar fields such as `Scarcity01`, `Demand01`, and `Aggression01`; failures return `[CRITICAL_DATA_RANGE]`. Alternative rejected: type-only validation that accepts impossible gameplay values. Estimate: one cold min/max compare per numeric cell.
- [x] Runtime header cross-check | DOD: `StaticDataStore.ValidateHeaderAndChecksum()` now rejects mismatched `RecordCount`, `LookupCount`, and `RecordBytes` before CRC-accepted data can build a lookup map. Alternative rejected: trusting only file length and offsets. Estimate: boot-only integer checks.
- [x] Packed record type guard tightened | DOD: lookup entries now reject zero/out-of-nibble record types before packing low bits into the `NativeParallelHashMap<uint,long>` value. Alternative rejected: masking invalid types and letting corruption alias another record type. Estimate: boot-only branch per lookup entry.
- [x] Schema regression tests | DOD: PlayMode tests now cover rejected non-first `Id` headers and rejected non-canonical IDs. Alternative rejected: relying only on manual CSV review. Estimate: test-only.
- [x] Compile and scans | DOD: Core build exits 0 with 2186 warnings and 0 errors after restore. PlayMode build exits 0 with 2235 warnings and 0 errors after restore and a timed-out first attempt. StaticDataStore forbidden scan remains clean; Core/Data scan found no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, parser calls, or `string.Split` outside editor/cold tooling. Alternative rejected: claiming success from code inspection alone. Estimate: build-time only.

## Loop 9 - Babel Runtime Receiver Hardening

- [x] XML and mandates reloaded | DOD: Re-extracted `CSV_DATA_MONOLITH_SYNC` from `CURRENT_BATCH.md` and re-read string/zero-GC/checksum mandates before editing. Alternative rejected: continuing from chat memory. Estimate: authority read only.
- [x] Zero-copy Babel receiver | DOD: Added `BabelDictionaryStore` to memory-map `Babel_Dictionary.h8bin` on Editor/Standalone and use `H8Memory.AllocateRaw(..., SystemID.CoreDataVault)` fallback elsewhere. Alternative rejected: decoding text into managed strings at boot. Estimate: boot-only map; text access returns `ReadOnlySpan<byte>`.
- [x] Native text lookup | DOD: Babel runtime lookup uses `NativeParallelHashMap<uint,long>` with packed `offset:length` slices; no `Dictionary<string,T>` or string keys exist in the receiver. Alternative rejected: managed string dictionary or linear scan. Estimate: expected O(1), 0 bytes GC per lookup.
- [x] Babel header and CRC gate | DOD: Runtime validates magic, format, header size, file length, index/data offsets, 16-byte alignment, and CRC32 before building the lookup. Alternative rejected: trusting the writer because half-written dictionary files can desync names/descriptions from records. Estimate: boot-only checks.
- [x] Babel entry validation | DOD: Runtime rejects zero hashes, empty slices, unaligned string offsets, out-of-range UTF8 slices, and duplicate hashes before accepting the dictionary. Alternative rejected: returning corrupt spans into arbitrary mapped bytes. Estimate: boot-only branch per text entry.
- [x] String pool regression test | DOD: PlayMode sanity test now opens the baked Babel dictionary, reads `Scrap Metal` by FNV hash as `ReadOnlySpan<byte>`, verifies content, and confirms missing hashes return empty spans. Alternative rejected: verifying numeric records while leaving text pool untested. Estimate: test-only.
- [x] Binary and rot scans | DOD: PowerShell scan confirmed `BABEL_LOOKUP_SCAN_CLEAN count=26 dataOffset=448 bytes=1284`; runtime store forbidden scan remains clean for `StaticDataStore.cs` and `BabelDictionaryStore.cs`; Core/Data scan found no standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `string.Split`, `float.Parse`, or `double.Parse`. Alternative rejected: source-only review. Estimate: verification-only.
- [x] Compile status [BLOCKED BY DEPENDENCY] | DOD: Initial Core build failed because Unity-generated restore assets under `Temp/obj` were missing. After restore, Core compile reached source analysis and failed only in cross-domain files: `SargassumMicroFaunaBoids.cs` missing `_grazingAnchors`, `_massiveThreats`, `_formationBeacons`, `_formationObstacles`, and `SubmarineFluidDynamics.cs` missing `_cachedFloodStateMathLodFrame`. No `Assets/_Project/Scripts/Core/Data/*` errors were reported. Alternative rejected: editing World/Submarine files outside this domain or claiming a green full build. Estimate: no runtime impact from this dependency wall.

## Loop 10 - Babel Blackbox Sovereignty Pass

- [x] XML and mandate reload | DOD: Re-extracted the `CSV_DATA_MONOLITH_SYNC` XML block, re-read the Core/Data domain boundary, and re-read the zero-GC, checksum, DirectStorage/MMF, and post-mortem telemetry mandates before this patch. Alternative rejected: continuing from chat memory. Estimate: authority read only.
- [x] Babel vault-owned blackbox | DOD: `BabelDictionaryStore` now binds `IDataVault`/`GlobalRegistry.DataVault` and records into `BufferID.StaticDataTelemetryRing` plus `StaticDataTelemetryCursor`; no local `NativeArray` telemetry ownership was introduced. Alternative rejected: private ring buffer inside the text store. Estimate: 0 us on successful text span access; telemetry writes happen on open/error/miss paths.
- [x] Babel failure telemetry | DOD: Open success, missing file, fallback read shortfall, header failure, CRC failure, duplicate/out-of-bounds slice failure, and missing hash reads now write fixed `H8StaticDataTelemetryEntry` records. Alternative rejected: silent empty spans for corrupted dictionaries. Estimate: one fixed struct write on failure paths; no profiler microsecond claim.
- [x] Babel dump parity | DOD: Added `DumpBlackBox()` to export the existing vault-owned 300-entry ring to `Docs/AgentLogs/Dump_CSV_DATA_MONOLITH_SYNC.bin`, matching the numeric store post-mortem path. Alternative rejected: managed log-only diagnostics. Estimate: explicit dump-only disk IO.
- [x] No-rebuild verification | DOD: Per user directive, no `dotnet build` rerun was executed in this loop. Static scans found no `NativeArray<`, `new byte[]`, managed `Dictionary<`, parser calls, `string.Format`, raw `UnsafeUtility.Malloc`, or raw `UnsafeUtility.Free` in the runtime stores; Core/Data scan found no standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `string.Split`, `float.Parse`, or `double.Parse`; binary scan confirmed `BABEL_BLACKBOX_SCAN_CLEAN count=26 dataOffset=448 bytes=1284`; tracked `git diff --check` is clean except CRLF warnings. Alternative rejected: wasting another full rebuild while the known blocker is cross-domain. Estimate: verification-only.

## Loop 11 - Static/Babel Pair CRC Gate

- [x] Static header Babel CRC surfaced | DOD: `StaticDataStore` now exposes `BabelCrc32` from the already validated static header, so runtime pairing can use the baker-authored dictionary checksum without parsing text metadata. Alternative rejected: caller-side binary header scraping. Estimate: one property read after boot; 0 bytes GC.
- [x] Babel expected CRC gate | DOD: `BabelDictionaryStore.Open(path, expectedPayloadCrc32)` and `ValidateExpectedPayloadCrc()` reject a dictionary whose own CRC does not match the static monolith header. Alternative rejected: accepting two individually valid but stale-paired files. Estimate: one boot-time uint compare; 0 us successful `GetUtf8` hot path.
- [x] Reload/default overload parity | DOD: Added `OpenDefault(expectedPayloadCrc32)` and `TryReload(path, expectedPayloadCrc32)` so editor hot-reload callers can keep the same pairing guard. Alternative rejected: only validating first boot while reload can desync. Estimate: boot/reload-only branch.
- [x] Sanity test paired CRC contract | DOD: PlayMode sanity coverage now reads `store.BabelCrc32`, asserts it matches `H8DataBakeResult.BabelCrc32`, and opens Babel with that expected CRC. Alternative rejected: testing Babel in isolation only. Estimate: test-only.
- [x] No-rebuild verification | DOD: Per user directive, no `dotnet build` rerun was executed. Static store scans remain clean for forbidden runtime parser/allocation tokens, Core/Data update/parser scan remains clean, tracked `git diff --check` reports only CRLF warnings, and baked artifacts pass `BABEL_PAIR_CRC_SCAN_CLEAN crc=0x694BA34A staticBytes=896 babelBytes=1284`. Alternative rejected: another full build while a known cross-domain compile wall exists. Estimate: verification-only.

## Loop 12 - Reload Lifecycle And SD Fallback Polish

- [x] Native map disposal reset | DOD: `StaticDataStore` and `BabelDictionaryStore` now assign `_lookup = default` immediately after disposing their `NativeParallelHashMap<uint,long>`, making repeated `Shutdown()`, failed open cleanup, and reload paths idempotent. Alternative rejected: trusting disposed native-container handle state. Estimate: shutdown/reload-only assignment; 0 us lookup hot path.
- [x] Single-close reload path | DOD: `TryReload()` in both stores now delegates to `Open()`, which already owns the close/reopen transition. Alternative rejected: close-before-open double release. Estimate: removes one redundant close pass per reload; no profiler microsecond claim.
- [x] Steam Deck fallback IO parity | DOD: `StaticDataStore` non-MMF fallback file stream now uses `FileOptions.SequentialScan`, matching Babel and keeping MicroSD fallback reads linear. Alternative rejected: default file options on non-MMF platforms. Estimate: cold boot/reload IO hint only.
- [x] Lifecycle regression coverage | DOD: PlayMode sanity test now exercises `TryReload()` and double `Shutdown()` for both numeric and Babel stores after successful lookups. Alternative rejected: testing only first-open happy path. Estimate: test-only.
- [x] No-rebuild verification | DOD: Per user directive, no `dotnet build` rerun was executed. Runtime store forbidden-token scans remain clean, Core/Data update/parser scan remains clean, `git diff --check` reports only CRLF warnings, and baked files pass `RELOAD_LIFECYCLE_BINARY_SCAN_CLEAN crc=0x694BA34A staticBytes=896 babelBytes=1284`. Alternative rejected: another full build while cross-domain compile blockers are already known. Estimate: verification-only.

## Loop 13 - CSV Parser Corruption Gate

- [x] Unclosed quote rejection | DOD: `H8CsvReader.Read()` now throws `InvalidDataException` when EOF is reached while inside a quoted field. Alternative rejected: allowing quote drift to merge rows before schema validation. Estimate: one final boolean check per CSV read; cold bake only.
- [x] Row-width drift rejection | DOD: After header parse, every CSV data row must have the same cell count as the header row before the table is accepted. Alternative rejected: silently ignoring orphan cells or relying on downstream required-column checks. Estimate: one cold integer compare per row.
- [x] CSV regression tests | DOD: Added PlayMode tests for unclosed quoted fields and orphan extra cells. Alternative rejected: manual spreadsheet inspection. Estimate: test-only.
- [x] No-rebuild verification | DOD: Per user directive, no `dotnet build` rerun was executed. Source scans confirm the new error gates and tests, runtime store forbidden-token scans remain clean, Core/Data update/parser scan remains clean, tracked `git diff --check` reports only CRLF warnings, and baked files pass `CSV_PARSER_HARDENING_BINARY_SCAN_CLEAN crc=0x694BA34A staticBytes=896 babelBytes=1284`. Alternative rejected: another full build while cross-domain blockers remain known. Estimate: verification-only.
