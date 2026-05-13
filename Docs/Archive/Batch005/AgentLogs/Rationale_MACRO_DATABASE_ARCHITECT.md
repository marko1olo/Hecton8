# Rationale_MACRO_DATABASE_ARCHITECT

Status: PENDING VERIFICATION

## Decision Log

### Initial Boundary
Problem: H8_MacroDB must cover 4 million sectors without permanent RAM residency or plugin dependency.
Solution: Build an isolated Core Database assembly with a service interface in Contracts, fixed binary `.h8db` pages, pointer-based B-Tree traversal, and native cache ownership hooks.
Rejected Alternatives: SQLite/plugin storage rejected by prompt and GC risk. Managed `Dictionary<ulong, byte[]>` rejected because it duplicates global world state in RAM and creates managed allocations.
Scalability potential: Low uses 1km hydration radius and minimal cache; Middle uses 2km; High keeps broader prefetch; Ultra spends saved CPU on wider speculative hydration and richer telemetry.
Hardware Impact: Estimated low-end i3/MX350 gain is removal of multi-million-entry managed state, avoiding GC spikes and limiting MicroSD I/O to local sector windows.

### Mandatory Mandates
Problem: Native pager touches contracts, persistence, streaming, AUP, unsafe memory, and telemetry at once.
Solution: Use mandate set: registry DI, binary save, crash telemetry, AUP, native jobs/memory, zero-GC, world residency, persistent registry.
Rejected Alternatives: Reading only AGENTS.md rejected because batch explicitly requires registry mandates.
Scalability potential: Mandates force tiered radius, hysteresis, and blackbox evidence instead of a fixed platform assumption.
Hardware Impact: Expected low-end benefit is bounded cache and I/O work; high-tier benefit is expanded prefetch without changing authority.

### Loop 1 Tasks 1-5
Problem: The macro database needed a service boundary, typed hydration signal, isolated assembly, global-state audit, and deterministic file root.
Solution: Added `IMacroDatabaseService`, `IMacroDatabaseNativeCacheOwner`, and `IMacroDatabaseSignalSink` contracts; added `GlobalRegistry.RegisterMacroDatabase`; added `SectorHydratedSignal` bridge into the native signal bus; created `Hecton8.Core.Database.asmdef`; implemented `.h8db` header with root node offset and 4096-byte fixed node pages.
Rejected Alternatives: `Database.Instance`, string events, SQLite, JSON/RLE save blobs, and direct World/Bootstrap dependencies inside the database assembly were rejected. The composition root may reference the database implementation, but the database assembly references only the Contracts project assembly.
Scalability potential: Low keeps only local sectors and a 1km radius; Middle uses 2km; High/Ultra can increase prefetch radius without changing the B-Tree key authority.
Hardware Impact: Estimated low-end i3/MX350 gain is removing permanent RAM residency for million-scale world state and keeping MicroSD seeks bounded to local sector windows.

### Loop 2 Tasks 6-10
Problem: B-Tree traversal must be pointer-based and background-capable without managed serializers.
Solution: Implemented page-local SoA offsets for sector hash array, file offset array, and child offsets; traversal reads via `UnsafeUtility.ReadArrayElement`; file mapping uses `MemoryMappedFile`; AUP-to-sector hashing uses absolute grid/local coordinates; hydration has an `Awaitable.BackgroundThreadAsync()` path.
Rejected Alternatives: Managed node object graphs, `BinaryFormatter`, `Dictionary<ulong, byte[]>`, Transform-relative world positions, and main-thread disk traversal were rejected.
Scalability potential: Toaster path touches fewer hashes; high-end path widens radius and speculative hydration while using the same page format.
Hardware Impact: Estimated 30-80 us saved per warm traversal versus managed node/serializer paths by avoiding heap churn and virtual object hops; actual disk latency still dominates cold faults.

### Loop 3 Tasks 11-14
Problem: Hydrated payloads need native cache ownership, dirty eviction, offline repack, and AUP shift immunity.
Solution: Extended `GlobalDataVault` with a native `NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>` cache whose handles carry `IntPtr`; dirty sectors append to the `.h8db` tail and update B-Tree offsets; `TryRepackOffline` rebuilds a compact destination database by walking live B-Tree payloads; keys are derived from absolute AUP sector coordinates.
Rejected Alternatives: Database-owned managed caches, in-place payload rewrites, runtime stop-the-world defrag, and floating-origin-relative keys were rejected.
Scalability potential: Low devices pay only dirty append cost during eviction; Ultra can run offline repack more aggressively from menus without changing runtime semantics.
Hardware Impact: Estimated low-end benefit is bounded cache memory and no GC during dehydration; append-only writes favor MicroSD sequential throughput over random overwrite churn.

### Loop 4 Tasks 15-18
Problem: The pager must provide Math LOD, zero-GC traversal, blackbox telemetry, and compile evidence.
Solution: Configured Low/Middle/High/Ultra radius tiers with Low at 1km, default Middle at 2km; hot traversal uses caller/native scratch arrays, MMF pointers, and vault native cache; blackbox stores 300 `MacroDatabaseTelemetryEntry` records with cache bytes and page faults; explicit file-handle shutdown releases view pointer, accessor, MMF, and stream.
Rejected Alternatives: One middle-ground radius, LINQ/string/boxing traversal, Debug.Log-only diagnostics, and leaked file handles were rejected.
Scalability potential: Low and Middle reduce I/O; High and Ultra spend saved cycles on wider residency and telemetry.
Hardware Impact: Expected low-end gain is fewer page faults and no managed allocations in traversal; top-tier devices can hold more cache and prefetch more sectors.

### Compile Wall
Problem: Unity compile is blocked before macro database verification by unrelated errors in `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs` local variable shadowing and a pre-existing Burst resolver failure for `Hecton8.UI.Tools`.
Solution: Ran Unity refresh/compile, read console, ran `validate_script` on the new macro database scripts and the modified vault; those scripts validate with 0 diagnostics. Kept task status as PENDING VERIFICATION and marked compile check dependency-blocked instead of falsifying a clean build.
Rejected Alternatives: Editing the audio/Burst dependency domain was rejected as outside `MACRO_DATABASE_ARCHITECT` authority. Claiming full compile success was rejected.
Scalability potential: No runtime scalability claim changes until whole-project compile clears.
Hardware Impact: No hardware gain claim from compile validation; implementation estimates remain design-level pending integration.

### Loop 5 Self-Review
Problem: First self-read found dirty payloads could have tracked caller memory instead of the vault-owned copy, and append writes used `FileStream.Write` beside the memory map.
Solution: Dirty map now records the vault-owned handle when cache storage succeeds; appends now write payload header/data directly through the mapped pointer with `UnsafeUtility.MemCpy`.
Rejected Alternatives: Trusting external dirty pointers and split file/MMF writes were rejected because they can stale or desynchronize under eviction.
Scalability potential: Stable vault-owned dirty handles keep low-end eviction deterministic; direct mapped writes reduce syscall overhead for high-frequency dirty append bursts.
Hardware Impact: Estimated 5-15 us saved per dirty append on low-end storage path by avoiding extra stream write calls and avoiding stale-pointer defensive recovery.

### OMEGA POLISH CHANGES
Problem: The first complete pager used integer division in sector-radius and AUP-to-sector conversion, which violates the frame-time audit even when the work is small.
Solution: Added cold `_sectorSizeRcp` setup and converted runtime sector radius/coordinate conversion to reciprocal multiplication. Re-ran owned-file scans for `foreach`, `.ToString(`, `string.Format`, string interpolation, LINQ markers, `math.sqrt`, and `math.normalize`; no hot-path hits remain. B-Tree traversal still uses direct page offsets, `UnsafeUtility.ReadArrayElement`, and mapped payload pointers.
Rejected Alternatives: LUT or triangle-wave cheats were rejected because the pager stores identity keys and persistent payload offsets, not visual simulation. Approximate animation math would corrupt persistence. Standard Unity object serialization and managed dictionaries were rejected again because they trade deterministic paging for GC and reflection.
Scalability potential: Low uses 1km hydration radius and smallest page-fault window; Middle uses 2km; High uses 3km; Ultra uses 4km plus broader cache/prefetch. Cheap devices get fewer faults and no hot managed allocations. Top-tier devices spend saved time on more resident sectors and blackbox visibility.
Hardware Impact: Reciprocal multiplication saves roughly 1-5 us across large radius-window builds on i3/MX350 class hardware, depending on query density. Direct MMF append remains an estimated 5-15 us saved per dirty payload versus split stream writes. Native cache avoids managed allocation spikes that can cost milliseconds under sector churn.
Final Git Diff: Added `MacroDatabaseContracts.cs`, `Hecton8.Core.Database.asmdef`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, and `MacroDatabaseSignalBridge.cs`. Modified project contracts/asmdefs, `GlobalRegistry`, `GlobalSignals`, `GlobalDataVault`, and `GameBootstrapper` only where required for service registration, native cache ownership, and typed hydration signals. The repository already contained unrelated dirty edits; those were left intact.
Verification: Unity MCP `validate_script` reports 0 diagnostics for the contract, file format, service, vault cache hook, and signal bridge. `git diff --check` passes on owned touched files with CRLF normalization warnings only. Full Unity compile remains blocked by unrelated `AcousticEcholocationRaymarch.cs` shadowing and missing `Hecton8.UI.Tools` Burst resolver dependency, so status stays PENDING VERIFICATION rather than falsely marked master-grade.

### Loop 6 Re-Audit
Problem: Follow-up compile proved that the first async implementation satisfied static validation but failed Unity C# compile with `CS4004: Cannot await in an unsafe context`. Self-review also found missing dirty-sector flushing on shutdown, `int3` sector coordinates that were not AUP-scale authoritative, weak existing-file header validation, and failed initialization leaving partially initialized native state.
Solution: Moved the await state machine into non-unsafe `H8MacroDatabaseAsyncHydration`; the unsafe service now exposes locked staging/store methods only. Added `HydrateRadiusAsync` to the contract as an interface expansion, not a mutation. Added `_fileGate` to prevent MMF pointer remap races, `_asyncHydrateScratch` for staged background query results, `_dirtyPayloadKeys` plus shutdown `FlushDirtyPayloadsLocked`, `SectorCoord64` hashing, stronger header/root/append/sector-size validation, failed-init `Shutdown`, and locked blackbox dump.
Rejected Alternatives: Keeping `await` inside the unsafe class was rejected by compiler evidence. Making the whole database safe by removing pointers was rejected because the task requires pointer reads and MMF page traversal. Tracking dirty payloads only in a hash map was rejected because NativeParallelHashMap has no zero-GC ordered flush path. Editing current Ecosystem/UI/Burst blockers was rejected as outside MacroDB authority.
Scalability potential: Low tier still uses 1km radius and now has safer shutdown persistence; Middle/High/Ultra keep wider windows without changing hash identity. The async path performs B-Tree traversal off the main thread and stages only compact metadata before main-thread cache publication.
Hardware Impact: Main-thread stall reduction remains page-fault dependent, but moving traversal off-thread prevents large B-Tree scans from consuming the 12ms main-thread budget. Shutdown dirty flush prevents lost modifications without a runtime compaction pass. 64-bit sector keys preserve AUP authority if later content exceeds current 100km assumptions.
Verification: Unity MCP `validate_script` reports 0 diagnostics for `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, `GlobalDataVault.cs`, and `MacroDatabaseSignalBridge.cs`. Unity compile no longer reports MacroDB errors; current console blockers are outside domain: `EcosystemDirector` telemetry fields, `SuitHUDV4CanvasOverlay` duplicate hot-swap method, and missing `Hecton8.Vehicles.VFX` Burst dependency. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` remains red with stale/missing generated assembly references and is not proof against this isolated asmdef.

### Loop 7 Persistence Hardening
Problem: Self-review found `_sectorCoordsByHash` was being populated for every queried sector, including sectors with no cached payload. Long travel could pollute the coordinate map and weaken distance eviction. Dirty append also advanced the file append cursor before B-Tree offset update success, which could leak unreachable payload records on a failed upsert.
Solution: Added `_sectorCoordWindowScratch` and staged `SectorCoord64` values beside sector hashes without caching them globally. Coordinates are now cached only after payload hit/store succeeds and removed when payloads evict. Dirty append records the previous append offset and rolls it back if `UpsertPayloadOffset` fails. Existing/opened files and new files now reject lengths above `MaxFileBytes`, and append/node allocation paths guard signed offset overflow before mapping.
Rejected Alternatives: Keeping coordinates for every query was rejected because it turns a residency index into a travel-history index. Runtime B-Tree compaction on every failed upsert was rejected as too expensive and unnecessary; append rollback is cold failure handling. Expanding the Memory asmdef back toward the monolithic Core assembly was rejected because that creates circular dependency pressure.
Scalability potential: Low devices retain only hydrated/cached sector coordinates and avoid map pollution during narrow 1km scans. Middle/High/Ultra can query wider windows without retaining empty-space history. The append rollback keeps long-session files bounded to committed records until explicit offline repack.
Hardware Impact: Coordinate-map cleanup prevents native hash-map pressure under long traversal on i3/MX350 class hardware; expected gain is avoidance of rare eviction stalls rather than steady-frame speed. Append rollback saves disk/file growth after failed upsert and prevents later repack from scanning dead tail records. Overflow guards are cold checks with negligible steady-frame cost.
Verification: Re-extracted the `MACRO_DATABASE_ARCHITECT` prompt from `CURRENT_BATCH.md`; task count remains 18. Unity MCP `validate_script` reports 0 diagnostics for MacroDB contracts, file format, service, vault cache hook, and signal bridge. Unity refresh/compile no longer reports MacroDB or current `GlobalDataVault` errors. Current blockers are outside domain: `HectonUnderwaterVisuals` hot-swap interface mismatch and Burst resolving `Hecton8.Vehicles.VFX` -> `Hecton8.Core`.

### Loop 8 Dirty Eviction Safety
Problem: Eviction attempted `TryAppendDirtyPayloadLocked` but evicted the sector even when the append/upsert failed. Because `_dirtyPayloads` stores the vault-owned pointer, evicting that payload could free the pointer while the dirty map still retained it. A later shutdown flush would then read freed memory. Corrupt child nodes also lacked fail-closed key-count guards, and some payload/node offset checks used additive bounds that could wrap on corrupt files.
Solution: `EvictDistant` now skips dirty sectors when append/upsert fails. Sector coordinates are removed only after the cache owner no longer reports the payload. B-Tree traversal/copy/update/insert paths now reject node key counts outside the fixed page capacity. Payload pointer validation, node lookup, header validation, and file alignment now use overflow-safe bounds.
Rejected Alternatives: Evicting dirty sectors and hoping shutdown could recover was rejected because it creates a use-after-free path. Removing coords for every requested eviction was rejected because batch eviction has no per-key result. Full transaction journaling for every dirty append was rejected for this loop because append rollback plus fail-closed eviction removes the concrete data-loss path without turning runtime eviction into a save-system transaction layer.
Scalability potential: Low tier keeps dirty sectors resident if disk is full or corrupt instead of losing state; this preserves authored world truth on weak storage. High/Ultra can still evict clean sectors aggressively and spend cache on broader residency without sacrificing dirty persistence.
Hardware Impact: Added checks are branch-only cold/eviction-path work. Expected steady-frame cost is 0 us; worst-case eviction cost adds one native hash lookup per candidate. Avoided failure cost is severe: no freed dirty pointer and no corrupted long-session flush.
Verification: Unity MCP `validate_script` reports 0 diagnostics for all MacroDB-owned scripts and `GlobalDataVault`. Hot-path scan reports no LINQ/string allocation/sqrt/normalize markers in owned MacroDB files. Unity compile reaches ready state but remains blocked outside domain by duplicate methods in `HectonUnderwaterVisuals` and Burst resolver failure for missing `Hecton8.Prologue.Space`.
