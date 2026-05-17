# Rationale_CSV_DATA_MONOLITH_SYNC

## Decision 0 - Authority And Edit Boundary

Problem: Static balance data has no existing Core/Data pipeline; only `InventoryCost.cs` exists under the authoritative folder.

Solution: Implement a new self-contained static-data subsystem under `Assets/_Project/Scripts/Core/Data/` and source sheets under `Data/Balance/`. Keep runtime as binary MMF + numeric hashes only. Keep CSV parsing and validation as cold-path tooling.

Rejected Alternatives: Reusing ScriptableObjects would keep balance truth inside Unity and violate the spreadsheet-first objective. JSON would preserve authoring errors and runtime parsing debt. Editing world/economy assemblies would exceed the assigned domain.

Scalability potential: Low uses one monolith and direct MMF reads. Middle adds hot-reload in Editor only. High/Ultra can rebake more records and richer text dictionaries without runtime parse cost.

Hardware Impact: i3/MX350 gains by avoiding managed object graphs and string parsing at boot; expected lookup cost is native hash lookup plus pointer dereference, not C# object traversal.

## Decision 1 - CSV Layout

Problem: The hash tool must read the first CSV column while schema versioning requires a `version_id` header.

Solution: Make `Id` the first column and include `version_id` as the second required header on every sheet. Hashes are generated from `Id`; version enforcement is explicit and visible in Excel.

Rejected Alternatives: Putting `version_id` in a metadata row would break the "first column ID" hash mandate. Putting `version_id` first would make the hash tool useless. Hiding version in filenames would not stop mixed-schema rows.

Scalability potential: Low and Middle keep one visible authoring table per domain. High and Ultra can add more numeric columns without changing runtime parser cost because CSV parsing remains cold bake only.

Hardware Impact: i3/MX350 runtime impact is zero because the shipped file contains no CSV headers, no strings, and no parseable text.

## Decision 2 - Binary Format

Problem: Runtime balance lookup must be O(1), zero-GC, and protected from half-written files.

Solution: Fixed 64-byte static header, fixed 16-byte lookup entries (`uint Hash`, type, size, `long Offset`), 16-byte-aligned fixed records, and CRC32 over payload bytes. Records are sorted by access frequency before writing.

Rejected Alternatives: Sequential scan is O(n). Managed dictionaries keyed by string violate runtime identity rules. Field-by-field `BinaryWriter` hides layout drift and does not prove memcpy layout.

Scalability potential: Low reads a compact monolith. Middle/High/Ultra add more records while retaining the same lookup table and alignment law. Frequently accessed records stay near the start for MicroSD seek behavior.

Hardware Impact: i3/MX350 gains from contiguous reads and no managed object hydration. Cheap storage avoids random CSV/JSON parse reads.

## Decision 3 - Cold Parser Boundary

Problem: CSV validation needs rich errors, but gameplay cannot pay for strings or parsing.

Solution: Keep manual CSV parsing, `TryParse`, schema checks, and NASA-style messages inside `H8DataBaker` only. `StaticDataStore` never reads CSV and never calls parse APIs.

Rejected Alternatives: Loading CSV directly in boot would violate zero-GC and convert spreadsheet mistakes into runtime stalls. Silent default values were rejected because bad balance data must fail fast.

Scalability potential: Low-tier devices run the same runtime path as high-tier devices. High-tier balance size increases only the binary byte count and hash table capacity.

Hardware Impact: i3/MX350 avoids `float.Parse`, `string.Split`, and managed DTO construction in play.

## Decision 4 - Runtime Lookup Container

Problem: The loader needs O(1) `uint -> long` lookup without managed string keys or runtime dictionaries.

Solution: `StaticDataStore` builds a `NativeParallelHashMap<uint,long>` from the fixed lookup table during boot. `GetRecord<T>` uses that native map and returns a ref into mapped bytes.

Rejected Alternatives: `Dictionary<string,T>` is explicitly forbidden by the polish mandate. `Dictionary<uint,long>` is managed and not Burst/native safe. Binary search on a sorted table is allocation-free but O(log n), not the requested O(1).

Scalability potential: Low tier keeps compact capacity equal to record count. High/Ultra can raise record count without changing the API or storing strings.

Hardware Impact: i3/MX350 pays a cold native-map allocation and then avoids managed lookup churn during gameplay.

## Decision 5 - String Pool

Problem: Balance records need readable names/descriptions for tools and UI, but the runtime balance binary must contain only numeric data.

Solution: `H8DataBaker` writes text once into `Babel_Dictionary.h8bin` and stores `NameHash`/`DescriptionHash` in records. Text identity uses the same FNV-1a route as UI localization.

Rejected Alternatives: Inline record strings destroy fixed record sizes. Shared managed string tables at runtime create heap ownership and localization coupling.

Scalability potential: Low tier can ship minimal text blocks. Ultra can carry richer descriptions without changing numeric record stride.

Hardware Impact: i3/MX350 avoids pulling descriptive text when only physics/economy numbers are needed.

## Decision 6 - Editor Hot Reload

Problem: Designers need spreadsheet edits to rebake quickly, but runtime cannot own a file watcher or poll disk under load.

Solution: Implement hot reload under `#if UNITY_EDITOR` using `FileSystemWatcher`, debounce, `H8DataBaker.BakeDefault()`, and `AssetDatabase.Refresh()`. Gate rebakes when `SignalBusRegistry.SystemStress01 > 0.9`.

Rejected Alternatives: Player-build watcher would waste IO and file handles. Direct concrete DataVault calls were rejected because the assigned domain does not own DataVault interfaces for static balance ingestion.

Scalability potential: Low/Middle editor machines avoid rebake storms through debounce and stress pause. High/Ultra machines can rebake larger CSVs without changing runtime code.

Hardware Impact: i3/MX350 player builds pay zero watcher cost; editor-only IO avoids runtime frame impact.

## Decision 7 - CRC And Atomic Write

Problem: A half-written monolith would poison boot and produce silent balance corruption.

Solution: Write `.tmp`, flush, replace atomically, and store CRC32 over payload bytes. Runtime recomputes CRC before accepting the map.

Rejected Alternatives: File length checks miss torn writes. Whole-file CRC including the CRC field complicates patching and increases error risk.

Scalability potential: Low uses compact CRC overhead. Ultra can grow data size with linear boot verification, still outside hot path.

Hardware Impact: i3/MX350 pays a cold sequential read. That is cheaper and safer than runtime exception handling or corrupt state repair.

## Decision 8 - File Handle And Black Box Safety

Problem: The loader must map binary data without trapping designers out of Excel edits or leaving post-failure state invisible.

Solution: Open baked files with shared read/write/delete flags, release the MMF pointer explicitly, dispose all native and OS handles, and maintain a 300-entry fixed `NativeArray<H8StaticDataTelemetryEntry>` circular buffer with dump support on NaN/error paths.

Rejected Alternatives: Exclusive file locks were rejected because they block authoring iteration. Managed byte-array ownership was rejected because it hides lifetime and creates GC pressure. Unlogged lookup failures were rejected because post-mortem data is mandatory.

Scalability potential: Low keeps one compact telemetry ring and one shared map. Middle/High/Ultra can increase record count without widening telemetry or changing the lookup API.

Hardware Impact: i3/MX350 avoids repeated open/close churn and managed heap retention; telemetry writes are fixed-size struct stores.

## Decision 9 - Final Verification Boundary

Problem: The pipeline needed proof that the data-domain code compiles and that the PlayMode sanity test is at least build-valid.

Solution: Build `Hecton8.Core.csproj` and `Hecton8.PlayModeTests.csproj` with project references enabled. Fix the only local test compile issue by importing `System` for `string.AsSpan()`. Treat existing duplicate-contract warnings as out-of-scope warnings because they are pre-existing and non-fatal.

Rejected Alternatives: Stopping at source inspection was rejected because Task 20 demands `dotnet build`. Editing cross-domain duplicate contract structure was rejected because it is outside the Data Monolith mandate.

Scalability potential: Low/Middle use the same binary path and sanity scan. High/Ultra can add broader test matrices without changing the runtime store contract.

Hardware Impact: Build verification has no runtime hardware cost. The validated runtime path remains one native hash lookup plus one pointer dereference, with 0 bytes of GC allocation per `GetRecord<T>` call after boot.

## Decision 10 - Vault-Owned Static Data Blackbox

Problem: The inquisition rejected local NativeArray ownership. The earlier blackbox design kept the 300-entry static-data telemetry ring inside `StaticDataStore`, which made the loader stateful and violated the GlobalDataVault sovereignty rule.

Solution: Move static-data telemetry ownership to `GlobalDataVault` through `BufferID.StaticDataTelemetryRing` and `BufferID.StaticDataTelemetryCursor`. `StaticDataStore` now stores only vault handles, resolves raw pointers when recording or dumping telemetry, and clears handles on shutdown.

Rejected Alternatives: Keeping a private `NativeArray<H8StaticDataTelemetryEntry>` was rejected as stateful ownership. Managed logging queues were rejected because they allocate and do not support fixed post-mortem binary dumps.

Scalability potential: Low uses one 300-entry ring. Middle/High/Ultra can increase the central vault allocation policy without changing the loader API or `GetRecord<T>` hot path.

Hardware Impact: i3/MX350 avoids loader-owned native lifetime churn and keeps telemetry as fixed struct writes. High-end machines can record more static-data events through vault policy without managed allocation.

## Decision 11 - Quest And Android Alignment Defense

Problem: ARM64/Quest builds are less forgiving of implicit padding and unaligned binary reinterpretation. A silent struct size drift would turn the `.h8bin` file into corrupted gameplay data.

Solution: Every shipped binary contract uses `StructLayout(LayoutKind.Sequential, Pack = 1, Size = N)`. The baker executes `ValidateLayoutContracts()` before writing and rejects any ABI drift. The non-MMF fallback path reads file bytes directly into 16-byte-aligned unmanaged memory instead of using a managed scratch array.

Rejected Alternatives: Trusting CLR default packing was rejected because it can drift by platform and field changes. `File.ReadAllBytes` fallback was rejected because it creates a managed array and breaks the zero-GC runtime rule.

Scalability potential: Low and Middle use the same compact records. High and Ultra can add new record types only by declaring explicit size and updating the layout gate.

Hardware Impact: i3/MX350 and Quest get deterministic aligned reads with no managed scratch allocation. PC high tier gets the same deterministic binary with MMF-backed zero-copy access.

## Decision 12 - Fresh PlayMode Verification Boundary

Problem: A fresh PlayMode `dotnet build` no longer reaches test compilation reliably because the Unity-generated project depends on transient `Temp/bin/Debug` metadata for third-party and generated assemblies. Current failures name missing DLLs such as `Crest.dll`, `EasySave3.dll`, `GPUInstancer.dll`, and `Hecton8.Core.dll`; one retry with cache-repopulated metadata stalled and left orphaned `dotnet` processes.

Solution: Treat Core compile as the authoritative data-domain compile proof for this loop and record PlayMode as blocked by generated dependency state. Stop orphaned `dotnet` processes after timeout. Do not edit generated `.csproj` structure or unrelated third-party assembly generation to fake a pass.

Rejected Alternatives: Editing generated project files was rejected because Unity overwrites them and it exceeds the data-domain source boundary. Claiming the previous PlayMode pass as fresh proof was rejected because the current Temp state no longer supports it.

Scalability potential: Low/Middle need deterministic local build caches for test verification. High/Ultra CI should run Unity Editor assembly generation before PlayMode `dotnet build`, then run the same static-data sanity test.

Hardware Impact: No runtime hardware impact. The blocked step is editor/build infrastructure; `StaticDataStore.GetRecord<T>` remains one native hash lookup plus pointer dereference and 0 bytes GC.

## Decision 13 - Packed Lookup Type Guard

Problem: The runtime lookup stored only `uint Hash -> long Offset`. That preserved O(1), but a caller could request `GetRecord<H8PhysicsStaticRecord>(itemHash)` and reinterpret a valid item record as physics data.

Solution: Use the guaranteed 16-byte record alignment as a data cheat: pack the record type into the low four bits of the stored `long` while preserving the aligned offset in the high bits. `GetRecord<T>` masks the offset, compares the packed type against the static generic contract, and returns the zero missing record on mismatch.

Rejected Alternatives: A managed `Dictionary<uint,RecordMeta>` violates the runtime mandate. A second native map doubles boot map construction and lookup pressure. Trusting call sites leaves a silent data-corruption route.

Scalability potential: Low/Middle keep one compact native map. High/Ultra can add more record families as long as the low-nibble type space is managed deliberately; the monolith API does not change.

Hardware Impact: i3/MX350 and Quest pay one integer mask and one small compare per successful lookup. That is cheaper than widening the map or copying records, and it keeps 0 bytes GC in `GetRecord<T>`.

## Decision 14 - Memory Authority And Collision Rejection

Problem: The non-MMF fallback path still allocated raw unmanaged memory inside the loader, bypassing `H8Memory` authority. The baker also relied too heavily on later lookup insertion to expose duplicate row hashes.

Solution: Route fallback memory through `H8Memory.AllocateRaw` and `H8Memory.FreeRaw` under `SystemID.CoreDataVault`. Add a cold `HashSet<uint>` collision gate during CSV bake so duplicate FNV IDs fail before binary write.

Rejected Alternatives: Private `UnsafeUtility.Malloc/Free` was rejected because it hides ownership from the memory sentinel. Runtime duplicate handling was rejected because spreadsheet identity errors must fail at bake time, not during player boot.

Scalability potential: Low uses the same direct aligned fallback buffer. Middle/High/Ultra can expand sheet count without changing runtime lookup behavior; collision pressure is caught by the cold compiler.

Hardware Impact: Quest/Android fallback keeps aligned unmanaged reads without managed scratch arrays. i3/MX350 pays the duplicate check only during bake; gameplay remains one native hash lookup plus pointer dereference.

## Decision 15 - CSV Schema Sovereignty Gate

Problem: The baker found the `Id` column by name anywhere in the CSV. That violates the first-column hash rule and lets Excel authors reorder identity away from column zero while the hash manifest and human contract still imply first-column authority.

Solution: Reject any sheet where `Id` is not column zero, reject duplicate required headers, and require canonical lowercase ASCII snake_case row IDs. Add regression tests for both bad header order and bad key grammar.

Rejected Alternatives: Auto-normalizing keys or accepting `Id` anywhere was rejected because it hides spreadsheet drift. A runtime repair path was rejected because CSV mistakes must fail in the cold compiler.

Scalability potential: Low/Middle keep simple Excel sheets with a visible stable key column. High/Ultra can add hundreds of numeric columns without changing the identity contract.

Hardware Impact: i3/MX350 and Quest pay 0 us at runtime. The key/header scans are cold bake-only work and prevent malformed binaries from ever reaching boot.

## Decision 16 - Range And Header Integrity Defense

Problem: Type enforcement accepted impossible numeric values if they were syntactically valid, and the runtime header validation did not cross-check `RecordCount`, `LookupCount`, and `RecordBytes` tightly enough before building the native lookup.

Solution: Add schema-level min/range constraints for non-negative quantities and `[0,1]` scalar fields. Add boot-time header consistency checks and reject invalid packed record types before storing lookup values.

Rejected Alternatives: Allowing designers to encode invalid values for later gameplay clamps was rejected because it pushes balance corruption into unrelated systems. Trusting the CRC alone was rejected because CRC verifies bytes, not semantic shape.

Scalability potential: Low devices keep the same binary hot path. High/Ultra can expand sheet breadth while the compiler remains the single semantic firewall.

Hardware Impact: Runtime cost is boot-only integer checks plus existing CRC. Gameplay lookup remains one native map hit, one mask, one type compare, and one pointer dereference with 0 bytes GC.

## Decision 17 - Zero-Copy Babel Receiver

Problem: The baker emitted `Babel_Dictionary.h8bin`, but the text side of the monolith did not have a domain-owned zero-copy runtime receiver with the same checksum, alignment, and O(1) lookup discipline as numeric records.

Solution: Add `BabelDictionaryStore`, which memory-maps the dictionary on Editor/Standalone, uses `H8Memory.AllocateRaw` fallback under `SystemID.CoreDataVault` on non-MMF platforms, validates header and CRC, builds a `NativeParallelHashMap<uint,long>` of packed UTF8 slices, and returns `ReadOnlySpan<byte>` for hash lookups.

Rejected Alternatives: Loading all text into managed strings was rejected because it creates heap ownership and boot churn. A linear scan over dictionary entries was rejected because it makes UI/name lookup O(n). Reusing the numeric `StaticDataStore` was rejected because record refs and UTF8 byte spans have different failure contracts.

Scalability potential: Low uses compact UTF8 slices and O(1) lookup. Middle/High/Ultra can grow descriptions and localization text while keeping numeric records fixed and the text path zero-copy.

Hardware Impact: i3/MX350 and Quest pay boot-only validation plus one native hash lookup per text request. Hot access returns a span over mapped/aligned bytes and allocates 0 bytes GC.

## Decision 18 - Compile Boundary Honesty

Problem: After the Babel receiver patch, the full Core build no longer exits green because other active agents or stale edits broke cross-domain World/Submarine files. The compiler reports missing fields in `SargassumMicroFaunaBoids.cs` and `SubmarineFluidDynamics.cs`, not in Core/Data.

Solution: Restore missing NuGet assets, rerun Core build, record the exact dependency wall, and continue validating this domain with static scans and binary inspection. Do not edit World/Submarine files from the Data Pipeline role.

Rejected Alternatives: Claiming the previous green build still applies was rejected as stale evidence. Fixing cross-domain fauna/submarine state fields was rejected because it violates the assigned domain boundary and risks fighting other active agents.

Scalability potential: Low/Middle/High/Ultra are unaffected in the data runtime path; CI must clear the cross-domain compile wall before full project verification can be re-stamped.

Hardware Impact: No runtime hardware impact from the dependency wall. Data-domain runtime remains MMF or aligned `H8Memory` fallback, native lookups, and zero-GC span/ref access.

## Decision 19 - Babel Blackbox Sovereignty

Problem: The Babel text receiver was zero-copy, but it still failed the blackbox parity standard. A corrupted or missing `Babel_Dictionary.h8bin` could return an empty span without leaving a fixed post-mortem trail, and adding a private ring would violate the GlobalDataVault sovereignty rule.

Solution: Route Babel diagnostics through the same vault-owned `BufferID.StaticDataTelemetryRing` and `StaticDataTelemetryCursor` used by the numeric store. Record open success, missing-file, short-read, header, CRC, duplicate slice, out-of-bounds slice, and missing-hash events as fixed `H8StaticDataTelemetryEntry` values. Add `DumpBlackBox()` so either static-data receiver can export the shared 300-entry ring.

Rejected Alternatives: A local `NativeArray<H8StaticDataTelemetryEntry>` was rejected as stateful ownership. Managed `Debug.Log` or string diagnostics were rejected because they allocate and do not preserve the last 300 events. A separate Babel-only buffer ID was rejected because the data monolith is one subsystem and already has a static-data telemetry ring.

Scalability potential: Low/Middle keep one compact 300-entry telemetry ring for numeric and text static-data faults. High/Ultra can raise the vault allocation policy later without changing either store API. Successful text reads remain a native hash lookup and span construction; richer Babel dictionaries only affect boot indexing and disk bytes.

Hardware Impact: i3/MX350 and Quest pay no added cost on successful `GetUtf8` calls. Error and miss paths write one fixed 64-byte struct to vault memory. Dumping the ring is explicit disk IO, not frame work.

## Decision 20 - Static/Babel Pair CRC Gate

Problem: The static monolith header already stores `BabelCrc32`, but the runtime text receiver only validated the dictionary against itself. That permits a stale but internally valid `Babel_Dictionary.h8bin` to load beside a newer `H8StaticData.bin`, corrupting name/description hash resolution without tripping either individual CRC gate.

Solution: Expose `StaticDataStore.BabelCrc32` and add expected-CRC overloads to `BabelDictionaryStore.OpenDefault`, `Open`, and `TryReload`. After the Babel file passes its own header/CRC/index validation, the receiver compares `_header.PayloadCrc32` against the static header's expected CRC and rejects the pair on mismatch with telemetry.

Rejected Alternatives: Reading the static header manually at each Babel open was rejected because it duplicates validated loader logic. Ignoring stale pair risk was rejected because hot reload can replace one file before the other. Embedding strings back into static records was rejected because it breaks fixed-width numeric records and zero-copy balance access.

Scalability potential: Low/Middle get one boot-time checksum compare. High/Ultra can ship larger or localized Babel dictionaries while retaining pair-level integrity. Editor hot reload can use the same overload and reject torn static/text updates.

Hardware Impact: i3/MX350 and Quest pay one boot/reload `uint` compare and no extra cost on successful `GetUtf8` calls. No profiler microsecond delta was captured.

## Decision 21 - Reload Lifecycle Idempotence

Problem: Both runtime receivers disposed their native lookup maps without resetting the container fields, and `TryReload()` closed before calling `Open()`, which also closes. Under hot reload, failed opens, or repeated shutdown, that creates avoidable double-release pressure around native containers and OS handles.

Solution: Reset `_lookup` to `default` immediately after `NativeParallelHashMap.Dispose()` in both stores, let `Open()` own the single close/reopen transition for `TryReload()`, and add sanity-test coverage for `TryReload()` plus double `Shutdown()`. Add `FileOptions.SequentialScan` to the static-data fallback stream so non-MMF platforms get the same linear IO hint as Babel.

Rejected Alternatives: Relying on disposed container state was rejected because Unity native-container validity semantics should not be used as lifecycle policy. Keeping double-close reload was rejected because it increases handle churn without improving correctness. Adding a managed reload coordinator was rejected because the stores already own the precise OS/native resources.

Scalability potential: Low/Middle avoid reload instability during spreadsheet iteration and Quest/Android fallback reads remain linear. High/Ultra can hot-reload larger binaries without multiplying close/reopen work.

Hardware Impact: i3/MX350 and Steam Deck benefit from linear fallback IO and less reload handle churn. The successful `GetRecord<T>` and `GetUtf8` paths are unchanged: native hash lookup plus ref/span access, 0 bytes GC.

## Decision 22 - CSV Structural Corruption Gate

Problem: The cold CSV reader accepted malformed structural states too late. An unclosed quoted field could merge multiple rows into one cell, and extra orphan cells could be silently ignored because schema validation only reads required columns.

Solution: Reject EOF inside quotes with `InvalidDataException`, and reject any row whose parsed cell count differs from the header count before returning `H8CsvTable`. Add regression tests for unclosed quotes and row-width drift.

Rejected Alternatives: Letting `ValidateRow()` catch missing required values was rejected because quote drift can hide the real authoring fault behind misleading type or void errors. Ignoring extra cells was rejected because Excel copy/paste mistakes should fail the bake, not become invisible metadata.

Scalability potential: Low/Middle get clearer authoring failures with no player cost. High/Ultra can expand sheet width while the same structural gate protects every row.

Hardware Impact: i3/MX350 and Quest runtime cost is 0 us. The added checks are cold bake-only: one EOF flag check and one row-width compare per parsed row.
