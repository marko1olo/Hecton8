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
