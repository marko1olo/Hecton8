# SHINOBU_103 Status

Agent: SHINOBU_103
Domain: ECHELON 1 / Data Monolith (Static DB)
Task Count: 20
Status: IMPLEMENTED_BLOCKED_BY_EXTERNAL_COMPILE_WALL_AFTER_SCAVENGING_NATIVE_EDITOR_CSV_GATE

## Mandates Selected

- [x] DATA_Runtime_Struct_Layout_ARM64.txt
- [x] DATA_Save_Persistence_Binary_Delta_Checksum.txt
- [x] TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- [x] ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt
- [x] MATH_AUP_Determinism_Sync.txt

## Execution Loop 0

- [x] Prompt extracted | Justification: strict batch prompt isolation used; neighboring XML ignored. | Alternatives Rejected: IDE tab memory; unsafe under batch protocol. | Estimate: 40 us
- [x] Hygiene checked | Justification: no existing status/rationale files found, fresh batch memory allowed. | Alternatives Rejected: reading previous batch logs; forbidden unless explicitly ordered. | Estimate: 25 us
- [x] Relevant mandates read | Justification: binary static-data work touches file layout, checksum, zero-GC runtime, native memory, bootstrap fail-fast, telemetry, and AUP constants. | Alternatives Rejected: broad mandate sweep; slower and less precise than task-scoped mandates. | Estimate: 220 us
- [x] Ultra-think pre-flight reread | Justification: reread XML assignment, rationale, binary payload ledger, and global authority boundary before code. | Alternatives Rejected: relying on chat memory; rejected under amnesia protocol. | Estimate: 310 us

## Execution Loop 1: ABI / Boot Gate / IO Path

- [x] Task 01 implementation staged | Justification: non-editor bootstrap now fails fatal when `static_data.h8bin` is missing or invalid; editor keeps missing-file tolerance for CI/import iteration. | Alternatives Rejected: permanent mock/missing success in player; keeps Ghost Engine alive. | Estimate: 0 us/frame, boot-only branch
- [x] Task 02 implementation staged | Justification: runtime Data Monolith path now consumes binary arena only; CSV/JSON parsing remains editor compiler input, not boot/player hydration. | Alternatives Rejected: runtime text fallback; violates binary authority. | Estimate: 3000-20000 us boot saved depending CSV size
- [x] Task 03 implementation staged | Justification: hot DTOs in monolith types use public fields and no properties. | Alternatives Rejected: property wrappers on NativeArray records; creates hidden method calls/copies. | Estimate: 5-25 us per large section walk avoided
- [x] Task 04 implementation staged | Justification: removed `Pack=1` from monolith DTOs and rebuilt explicit 8/16-byte-aligned layouts. | Alternatives Rejected: x86-only packed structs. | Estimate: 5-25 us per cold scan, prevents ARM64 unaligned penalties
- [x] Task 05 implementation staged | Justification: header/directory/section table are emitted explicitly little-endian and runtime fails closed on endian mismatch. | Alternatives Rejected: blind unmanaged header copy. | Estimate: 0 us/frame, corruption prevention at boot

## Execution Loop 2: Baker / Sections / Validation

- [x] Task 06 implementation staged | Justification: CSV ingestion now reads all current designer tables and parallelizes file reads before deterministic single-owner record assembly. | Alternatives Rejected: runtime CSV loaders and ignored Fauna/Economy/Physics aliases. | Estimate: 3000-20000 us runtime boot saved; editor import scales with file count
- [x] Task 07 implementation staged | Justification: compiler writes header, directory, section table, and 16-byte-aligned sections into one blob. | Alternatives Rejected: scattered per-table binaries. | Estimate: 20-80 us lookup locality saved over fragmented file probes
- [x] Task 08 implementation staged | Justification: baker seals bytes `[16..end)` with XXHash3-64 and runtime recomputes before readiness. | Alternatives Rejected: trusting timestamps/file length. | Estimate: 0 us/frame, boot corruption prevention
- [x] Task 09 implementation staged | Justification: names/descriptions are pooled into null-terminated UTF-8 bytes; records store offsets and byte lengths. | Alternatives Rejected: C# string references in binary records. | Estimate: 50-500 us UI cold lookup saved when avoiding managed string hydration
- [x] Task 13 implementation staged | Justification: `Physics.csv` now bakes into `PhysicsConstants` with AUP sector/world-bound fields and conservative defaults. | Alternatives Rejected: drifting C# constants. | Estimate: 0 us/frame, deterministic binary truth
- [x] Task 14 implementation staged | Justification: baker aborts on broken item-backed recipe and loot references. | Alternatives Rejected: runtime foreign-key discovery. | Estimate: 100-1000 us runtime fault handling avoided

## Execution Loop 3: Runtime Access / Telemetry

- [x] Task 10 implementation staged | Justification: runtime validates magic, version, header size, directory, and XXHash3 before `Ready`. | Alternatives Rejected: optimistic load status. | Estimate: 0 us/frame, boot-only integrity cost
- [x] Task 11 implementation staged | Justification: runtime attempts MMF first and falls back to direct FileStream into Vault-owned native memory; if `GlobalDataVault` is unavailable the arena fails closed instead of allocating private persistent bytes. | Alternatives Rejected: managed blob staging and no-vault private arena fallback. | Estimate: one blob-size allocation and copy removed
- [x] Task 12 implementation staged | Justification: `GetSectionSpan<T>` returns pointer-backed `ReadOnlySpan<T>` from the section directory. | Alternatives Rejected: per-call list copies. | Estimate: 5-50 us per large table access
- [x] Task 15 implementation staged | Justification: sorted item lookup uses Burst-decorated binary search pointer helper and a `ReadOnlySpan` wrapper. | Alternatives Rejected: dictionary hydration. | Estimate: 2-20 us per lookup burst batch depending count
- [x] Task 16 implementation staged | Justification: Vault payload buffer requests use `NativeArrayOptions.UninitializedMemory` before direct overwrite. | Alternatives Rejected: memset before disk read and private fallback allocation. | Estimate: up to several ms at 10 MB boot size
- [x] Task 17 implementation staged | Justification: 300-entry telemetry ring/cursor lives in Vault and dumps `Dump_DATA_MONOLITH.bin` plus agent dump on failure or >50 ms read. | Alternatives Rejected: chat-only profiling. | Estimate: 0 us/frame, boot forensics only

## Execution Loop 4: Human Facade

- [x] Task 18 implementation staged | Justification: UI Toolkit compiler window lists CSVs with UTC modification stamps and exposes a large bake button plus error status. | Alternatives Rejected: hidden menu-only bake. | Estimate: 0 us/frame, editor-only
- [x] Task 19 implementation staged | Justification: window generates template CSVs and a reflection-derived binary layout manifest. | Alternatives Rejected: hand-maintained schema docs. | Estimate: 0 us/frame, editor-only
- [x] Task 20 implementation staged | Justification: window opens `static_data.h8bin`, validates XXHash3, and displays section offsets/counts. | Alternatives Rejected: running the game to inspect binary structure. | Estimate: 0 us/frame, editor-only

## Execution Loop 5: Static Self-Audit

- [x] Re-read prompt after staged tasks | Justification: extracted SHINOBU_103 XML after task groups to prevent task drift. | Alternatives Rejected: memory-only reconciliation. | Estimate: 120 us
- [x] Forbidden pattern scan | Justification: searched Data Monolith code for `Pack=1`, DTO properties, runtime `File.ReadAllBytes`, `Split`, and managed staging. | Alternatives Rejected: visual inspection only. | Estimate: 250 us
- [x] CPU guard check | Justification: `Get-Counter` reported 96-100% CPU and no dotnet/csc process; build/bake verification deferred under explicit CPU protection rule. | Alternatives Rejected: launching `dotnet build` into >50% CPU load. | Estimate: prevented unbounded compile-wall cost

## Execution Loop 6: Post-Mandate Hardening

- [x] Telemetry dump ordering repaired | Justification: failed reads now record/dump telemetry before arena handle reset, and successful file loads record actual IO ticks/path flags. | Alternatives Rejected: zero-tick placeholder Loaded event; insufficient black-box proof. | Estimate: 0 us/frame, boot-only forensic correction
- [x] Generated-source exclusion staged | Justification: `Data/Balance/Baked` manifests and `Data/Balance/Schemas` templates are excluded from bake source enumeration and file-watch triggers. | Alternatives Rejected: recursive source ownership over generated artifacts; creates parallel-truth risk. | Estimate: 0 us/frame, editor-only loop prevention
- [x] Second static guard pass | Justification: re-scanned Data Monolith files after hardening for `Pack=1`, DTO properties, runtime `File.ReadAllBytes`, `.Split()`, and direct sibling-domain references. | Alternatives Rejected: waiting for compile to catch architecture issues; compile is blocked by CPU guard. | Estimate: 200 us
- [x] Domain Burst jobs hardened | Justification: existing Data Monolith SoA reconstruct jobs now use required Burst flags and `[NoAlias]` fields. | Alternatives Rejected: leaving same-domain jobs on default Burst compile settings. | Estimate: 2-10 us per large reconstruction pass depending table size

## Execution Loop 7: Compile Gate Attempt

- [x] CPU/dotnet guard passed before build | Justification: build was only started after CPU pressure dropped below the project guard and no `dotnet`/`csc` compiler process was active. | Alternatives Rejected: launching during 89-100% CPU saturation. | Estimate: prevented workstation compile contention
- [x] Compile attempted once | Justification: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was necessary after static checks and used a single MSBuild node. | Alternatives Rejected: Unity batch build first; too expensive while C# project compile gate was unproven. | Estimate: 68 s wall time
- [x] External compile wall isolated | Justification: compiler stopped before Data Monolith diagnostics on missing tracked World file `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` (`CS2001`). Git shows this file and its `.meta` are deleted in the working tree; `Hecton8.Core.csproj` still references it. | Alternatives Rejected: restoring or recreating World-domain code from HEAD; outside SHINOBU_103 ownership and would overwrite another agent/user deletion. | Estimate: 0 us/frame, integration blocker only

## Execution Loop 8: Static Polish After External Blocker

- [x] 12-byte DTO eliminated | Justification: `H8StaticLocalizationReference` is now 16 bytes with explicit padding and is covered by `H8DataLayoutAudit`. | Alternatives Rejected: treating it as harmless cold helper; rejected because all monolith DTOs must be ARM64-aligned by default. | Estimate: prevents misaligned native array stride, 0 us/frame current hot path
- [x] UTF-8 decode guard hardened | Justification: `TryReadLocalizedText` now computes required char count before decoding into caller-owned storage. | Alternatives Rejected: allowing `Encoding.UTF8.GetChars` to throw on undersized UI buffers. | Estimate: 0 us/frame, prevents cold UI exception path
- [x] Editor facade source list cleaned | Justification: compiler window now filters generated `Data/Balance/Baked` and `Data/Balance/Schemas` paths using the same absolute/relative-safe source authority predicate as the baker. | Alternatives Rejected: showing generated files as input evidence; violates one-route source truth. | Estimate: 0 us/frame, editor-only confusion prevention
- [x] Reflection schema generation strengthened | Justification: schema action now emits reflection-derived struct CSV templates in addition to designer-friendly authoring templates and layout manifest. | Alternatives Rejected: hardcoded-only CSV headers; insufficient proof for Task 19. | Estimate: 0 us/frame, editor-only schema drift prevention

## Execution Loop 9: Stale Project Reference Triage

- [x] Missing World file state reclassified | Justification: later git evidence showed `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is no longer in HEAD or the index, while `Hecton8.Core.csproj` still referenced it. | Alternatives Rejected: continuing to mark it as a working-tree deletion by another agent. | Estimate: 0 us/frame, build-gate accuracy
- [x] Stale `.csproj` include removed | Justification: removed exactly one `Compile Include` for a source file absent from HEAD so the next guarded build can reach real compiler diagnostics. | Alternatives Rejected: recreating a deleted World implementation; rejected as domain sabotage and stale code resurrection. | Estimate: prevents repeat 68 s build failure on the same `CS2001`

## Execution Loop 10: Source CSV Compatibility

- [x] Current balance headers checked | Justification: `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv` headers map to parser aliases used by the baker. | Alternatives Rejected: assuming headers from prompt without reading disk. | Estimate: 0 us/frame, bake-fail prevention
- [x] Generated balance payloads confirmed excluded | Justification: existing `Data/Balance/Baked/*.h8bin` and manifests are generated artifacts and are filtered from monolith source enumeration. | Alternatives Rejected: treating old `H8StaticData.bin` or Babel payload as authoritative monolith input. | Estimate: prevents editor source-route recursion
- [x] CPU/dotnet guard rechecked | Justification: CPU sampled `100, 98, 94, 76, 62%` and a `dotnet` process was active, so a second build is forbidden. | Alternatives Rejected: retrying build immediately after stale include removal. | Estimate: avoided compile contention under active system load

## Execution Loop 11: Economy Cross-Reference Hardening

- [x] XML Task 14 reread | Justification: original prompt explicitly names Economy recipe/item references, not only generic recipe and loot tables. | Alternatives Rejected: accepting recipe/loot-only validation as sufficient. | Estimate: 0 us/frame, editor-only correctness gate
- [x] Economy raw row validation staged | Justification: baker now preserves raw Economy rows and validates optional `item_id`, `item`, `output`, `output_id`, `recipe_output`, `recipe_output_id`, `ingredients`, `ingredient_ids`, `recipe`, and `recipe_items` fields against the baked Item hash set before output. | Alternatives Rejected: changing the 64-byte `H8EconomyRecord` ABI to store references not present in current CSV; unnecessary ABI churn. | Estimate: 0 us/frame, editor-only fail-fast
- [x] Final blob alignment staged | Justification: baker now pads the final stream to 16 bytes after the last section so `static_data.h8bin` file length is aligned, not only section starts. | Alternatives Rejected: relying on only payload-start alignment; ledger hygiene treats misaligned product binaries as debt. | Estimate: 0 us/frame, 0-15 bytes per bake
- [x] UTF-8 pool offset ABI corrected | Justification: string-pool offsets in Data Monolith DTOs are now `uint` with `uint.MaxValue` missing sentinel, matching Task 09; `LocRegistry` gets a bounds-guarded cast because it is the only static LocData alias consumer. | Alternatives Rejected: keeping signed offsets to preserve `-1`; rejected because the binary contract explicitly calls for unsigned offsets and byte lengths. | Estimate: 0 us/frame, ABI correctness
- [x] Static source audit repeated | Justification: forbidden runtime patterns stayed absent; new validation is editor-only and does not alter Data Monolith runtime allocation path. Latest CPU sampled `95, 100, 89, 78, 77%` with active `dotnet` PID `22952` and `csc` PID `67260`, so compile remains forbidden. | Alternatives Rejected: running build under CPU guard violation. | Estimate: avoided compile contention while preserving Task 14 precision

## Execution Loop 12: Player Vault Purge

- [x] XML and ledger reread | Justification: extracted the full SHINOBU_103 block and reread the binary payload ledger plus Data Monolith spec after context compaction. | Alternatives Rejected: trusting regex output that missed prompt attributes. | Estimate: 150 us
- [x] Relevant mandates reread | Justification: ARM64 layout, binary checksum, designer facade, zero-GC, native memory/jobs, bootstrap, telemetry, AUP, and cinematic cheat mandates were reloaded before code edits. | Alternatives Rejected: coding from stale compressed memory. | Estimate: 260 us
- [x] Player private arena fallback removed | Justification: `H8StaticDataArena` now resolves payload bytes only from `GlobalDataVault` BufferID `71103`; no private persistent `NativeArray<byte>` fallback remains in player/runtime source. | Alternatives Rejected: keeping a no-vault arena because it is convenient; violates XML Task 11 and Vault law. | Estimate: avoids one blob-sized native allocation outside Vault ownership
- [x] Telemetry path flags corrected | Justification: load telemetry no longer reports fallback-native-array; valid resident bytes are Vault-backed whether MMF or FileStream performed the copy. | Alternatives Rejected: preserving a dead fallback flag that would mislead black-box forensics. | Estimate: 0 us/frame
- [x] Static post-purge audit repeated | Justification: source scan found no `_arenaOwnedByNativeArray`, `PathFlagFallbackNativeArray`, `new NativeArray<byte>`, Data Monolith `Pack=1`, DTO auto-properties, runtime `File.ReadAllBytes`, `.Split()`, bare `[BurstCompile]`, or mid-frame `JobHandle.Complete` in the touched monolith domain. | Alternatives Rejected: relying on visual diff. | Estimate: 240 us
- [x] CPU/dotnet guard rechecked | Justification: CPU samples were `100, 49.74, 77.31, 87.71, 96.53, 53.42, 43.98, 39.76%` with active `csc` PID `59156` and `dotnet` PID `24932`; compile remains forbidden. | Alternatives Rejected: launching build while another compiler process is active. | Estimate: avoided compile contention

## Execution Loop 13: Spec Reconciliation / Mock Boundary

- [x] XML and spec reread | Justification: re-extracted SHINOBU_103 prompt and reread the binary payload ledger plus Data Monolith spec before touching documentation. | Alternatives Rejected: relying on stale self-audit wording after ABI edits. | Estimate: 120 us
- [x] Static-data mock boundary checked | Justification: targeted scan found production Data Monolith boot routes through `H8StaticDataArena.TryInitializeFromStreamingAssets`; editor missing-file tolerance remains the only CI/import fallback, while player boot throws `FatalArchitectureException`. | Alternatives Rejected: creating a runtime emergency monolith mock; rejected by Task 01. | Estimate: 0 us/frame
- [x] Runtime parser purge rechecked | Justification: scoped scan of Data Monolith, bootstrap, LocRegistry, and static-data-facing inventory/economy files found no runtime Data Monolith `File.ReadAllBytes`, `CSVReader`, `JsonConvert`, Newtonsoft, or `.Split()` parser route. | Alternatives Rejected: broad repo scan as pass/fail; other agents own unrelated mocks/parsers. | Estimate: 220 us
- [x] Data Monolith spec corrected | Justification: `DATA_MONOLITH_H8BIN_SPEC.md` now matches current ABI: header fields, section IDs 25/26, 80-byte item record, 64-byte economy/physics/telemetry records, 16-byte static localization reference, and explicit-layout wording. | Alternatives Rejected: leaving docs with a 64-byte item record that would mislead future consumers. | Estimate: 0 us/frame, documentation correctness
- [x] Diff hygiene checked | Justification: `git diff --check` over SHINOBU_103 files reported only existing CRLF normalization warnings and no whitespace errors. | Alternatives Rejected: waiting for compile to catch markdown/source hygiene. | Estimate: 30 us
- [x] CPU/dotnet guard rechecked | Justification: CPU samples were `75.91, 75.41, 19.24, 60.46, 54.98, 93.55%` with active `csc` PID `69316` and `dotnet` PID `69060`; compile remains forbidden. | Alternatives Rejected: launching `dotnet build` while another compiler process is active. | Estimate: avoided compile contention

## Execution Loop 14: UTF-8 Slice Completion

- [x] Task 09 reread | Justification: XML requires text pool references to be offsets plus byte lengths, not just null-terminated offsets. | Alternatives Rejected: accepting null-terminated scan-only records as close enough. | Estimate: 0 us/frame
- [x] Text-bearing DTO reserved slots repurposed | Justification: creature, biome, audio registry, ghost module, and SOP error records now store UTF-8 byte lengths without increasing their fixed record sizes. | Alternatives Rejected: enlarging records and invalidating section strides; rejected because existing reserved slots were sufficient. | Estimate: 0 us/frame, ABI precision
- [x] Baker length emission completed | Justification: CSV/JSON conversion now records byte lengths for all newly length-bearing text fields using the same `LocalizationPool.Add(..., out byteCount)` route as items/economy/physics. | Alternatives Rejected: recomputing lengths from null terminators in runtime consumers. | Estimate: saves 1 linear UTF-8 scan per static LocData alias/audio key cold lookup
- [x] Runtime length route completed | Justification: static localization alias extraction and audio addressable-key decode now validate bounded offset+length spans before decoding; null-terminated scan overloads remain for legacy callers. | Alternatives Rejected: breaking signed legacy API callers while fixing the ABI. | Estimate: 1-40 us saved on cold text lookup batches depending string count
- [x] Spec text-slice contract updated | Justification: the architecture spec now states that text-bearing records use unsigned offsets plus byte lengths with `uint.MaxValue`/`0` as the missing sentinel. | Alternatives Rejected: leaving the null-terminated-only implication. | Estimate: documentation correctness
- [x] Static text-slice audit repeated | Justification: scan found no direct `localizationPool.Add(...)` assignments for the length-bearing fields and no stale `TryBuildStaticLocalizationReference(offset-only)` route. | Alternatives Rejected: relying on diff inspection only. | Estimate: 120 us
- [x] CPU/dotnet guard rechecked | Justification: CPU samples were `13.51, 23.91, 74.3, 35.15, 100, 100, 100, 100%`; no active `dotnet`/`csc`, but samples above 50% still forbid build. | Alternatives Rejected: launching build because compilers were absent but CPU guard still failed. | Estimate: avoided compile contention

## Execution Loop 15: Android/Quest StreamingAssets Hardening

- [x] StreamingAssets URI gap identified | Justification: `Application.streamingAssetsPath` can be a non-filesystem URI on Android/Quest, while the arena previously entered `File.Exists`/`FileStream` directly. | Alternatives Rejected: assuming desktop filesystem semantics on Quest; rejected because it would hard-fail valid packaged blobs before checksum. | Estimate: 0 us/frame, boot-only correction
- [x] URI staging path implemented | Justification: non-filesystem StreamingAssets URIs are synchronously staged to `Application.temporaryCachePath` via `UnityWebRequest`/`DownloadHandlerFile`, then the existing Vault-backed FileStream/checksum reader handles the cached file. | Alternatives Rejected: `UnityWebRequest.downloadHandler.data`; rejected because it creates a managed blob-sized byte array. | Estimate: avoids one managed blob allocation on Android/Quest boot
- [x] Telemetry path flag added | Justification: staged URI reads set `PathFlagStreamingUriStaged` before the same Vault-backed read path records telemetry. | Alternatives Rejected: hiding the staging hop under generic FileStream telemetry; insufficient black-box proof. | Estimate: 0 us/frame
- [x] Early failure telemetry hardened | Justification: fatal missing, too-small, too-large, and no-vault allocation failures now record/dump telemetry before returning when the Vault is available. | Alternatives Rejected: only dumping read/checksum failures; insufficient for boot forensics. | Estimate: 0 us/frame
- [x] Architecture docs updated | Justification: Data Monolith spec and binary payload ledger now state the Android/Quest URI staging route. | Alternatives Rejected: code-only behavior change; future boot owners need the I/O truth. | Estimate: 0 us/frame
- [x] Static audit repeated | Justification: Data Monolith scan found no `File.ReadAllBytes`, `.Split()`, Newtonsoft/JsonConvert, `Pack=1`, DTO auto-properties, private arena fallback, bare `[BurstCompile]`, or local `JobHandle.Complete`. `git diff --check` reports only CRLF warnings. | Alternatives Rejected: relying on the later full build. | Estimate: 180 us
- [x] Guarded compile attempted | Justification: CPU samples were `11.94, 7.6, 17.4, 10.54%` and no active `dotnet`/`csc` processes existed, so one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was justified after code edits. | Alternatives Rejected: skipping compile after changing platform I/O. | Estimate: 97.7 s wall time
- [x] External compile wall isolated | Justification: build failed with 79 errors before Data Monolith diagnostics. First blockers are missing external types/namespaces: `Hecton8.Animation.KineticCharacter`, `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `DynamicDecalFrameStats`, `ActiveEquipmentDTO`, `MesofaunaTuningDTO`, and `MacroEcosystemSectorVaultRecord`. | Alternatives Rejected: creating placeholder DTOs or editing Gameplay/Visor/Equipment/Fauna/World code from Data Monolith ownership. | Estimate: 0 us/frame, integration blocker only
- [x] Second compile suppressed | Justification: after the small early-telemetry micro-patch, a second build would only rediscover the known external missing-type wall and consume another compile slot. Static syntax/pattern audit stayed clean. | Alternatives Rejected: hammering `dotnet build` to prove an unrelated broken tree is still broken. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 16: URI Staging Symbol Hygiene

- [x] Runtime symbol collision fixed | Justification: `H8StaticDataArena` exposes a `Directory` property, so the StreamingAssets staging code now calls `System.IO.Directory.CreateDirectory` explicitly instead of relying on unqualified name resolution. | Alternatives Rejected: leaving the symbol to compiler luck behind an already-known external compile wall. | Estimate: 0 us/frame, prevents a deterministic C# name-resolution failure when Data Monolith diagnostics become reachable
- [x] Data Monolith static audit repeated | Justification: reran targeted scans for runtime `File.ReadAllBytes`, `.Split()`, Newtonsoft/JsonConvert, `Pack=1`, DTO auto-properties, private arena fallback, local `JobHandle.Complete`, and bare Burst attributes. | Alternatives Rejected: treating the one-line patch as too small to audit. | Estimate: 90 us
- [x] Build suppressed under guard | Justification: CPU samples were `99.23, 99.82, 98.26, 99.81%` with no compiler process; the >50% samples still violate the user compile guard, and the known external missing-type wall remains. | Alternatives Rejected: launching `dotnet build` after a one-line symbol/doc fix under CPU pressure. | Estimate: avoided another compile slot
- [x] Bootstrap route rechecked | Justification: `InitializeBootstrapAllocators()` registers `GlobalDataVault` before `InitializeBootstrapDataMonolith()`, and player builds pass `failIfMissing=true`. | Alternatives Rejected: assuming boot order from prior memory. | Estimate: 0 us/frame, boot-only proof
- [x] Compile-wall boundary recorded | Justification: `H8DataHash`, `H8DataMonolithTypes`, `H8StaticDataArena`, and `H8CreatureSoAReconstructJob` are currently compiled by `Hecton8.Core.csproj`; no dedicated Data Monolith runtime asmdef exists. | Alternatives Rejected: adding a new asmdef now would force a circular Core/Data dependency because `GameBootstrapper` in Core calls `H8StaticDataArena`. | Estimate: 0 us/frame, integration debt documented instead of hidden

## Execution Loop 17: Prebuild Artifact Gate / Stale Docs Correction

- [x] Atomic monolith output gate staged | Justification: `BakeAll` now writes `static_data.h8bin.tmp`, validates magic/version/header bytes, XXHash3, directory byte count, section order, record strides, ranges, and 16-byte alignment, then promotes the temp file. | Alternatives Rejected: direct overwrite of the production blob; can leave a half-written artifact that still blocks boot. | Estimate: 0 us/frame, editor/build-only corruption gate
- [x] Player build preprocessor staged | Justification: `H8DataMonolithBuildPreprocessor` bakes and re-validates the monolith before player builds at callback order `-9100`. | Alternatives Rejected: relying on a manual menu bake before release; preserves Ghost Engine artifact drift. | Estimate: 0 us/frame, build-only guard
- [x] Stale architecture docs corrected | Justification: four architecture docs no longer claim runtime `File.ReadAllBytes`, permissive player boot, or mandatory `hash32` authoring pairs. | Alternatives Rejected: leaving docs to instruct the next agent toward already-fixed stale behavior. | Estimate: 0 us/frame, documentation correctness
- [x] Static guard pass repeated | Justification: scans found no stale Data Monolith doc claims, no runtime `File.ReadAllBytes`/`File.WriteAllBytes`, no `.Split()`, no Newtonsoft/JsonConvert, no `Pack=1`, no DTO auto-properties, no private arena fallback, no local `JobHandle.Complete`, and no bare Burst attributes in scoped Data Monolith sources. | Alternatives Rejected: treating editor-only build gate work as exempt from architecture scan. | Estimate: 160 us
- [x] Build suppressed under CPU guard | Justification: CPU samples were `75.51, 86.94, 87.23, 88.06, 75.53, 44.17%`; no `dotnet`/`csc` process was active, but >50% samples forbid a new build. `static_data.h8bin` remains absent until Unity bake/import runs. | Alternatives Rejected: launching `dotnet build` under explicit user hardware guard and known external missing-type compile wall. | Estimate: avoided another compile slot

## Execution Loop 18: Editor Facade Import Boundary

- [x] Editor facade import hole isolated | Justification: `H8DataMonolithCompilerWindow.cs` was tracked but had no `.meta`, and current generated csproj files did not include Data Monolith editor sources. | Alternatives Rejected: claiming Task 18/19/20 proof from filesystem-only presence. | Estimate: 0 us/frame, editor import correctness
- [x] Dedicated editor asmdef staged | Justification: added `Hecton8.DataMonolith.Editor.asmdef` with only Core plus Unity Burst/Collections/Mathematics references and Editor platform scope. | Alternatives Rejected: leaving the compiler inside the broad `Hecton8.Editor` surface with unpredictable import state; adding a runtime Data asmdef now is blocked by Core bootstrap circularity. | Estimate: reduces editor compile blast radius, 0 us/frame
- [x] Stable Unity GUIDs staged | Justification: added `.meta` for the compiler window and the DataMonolith editor asmdef so Unity import does not mint nondeterministic local GUIDs. | Alternatives Rejected: relying on Library-generated metas; not valid for team/source-controlled editor tools. | Estimate: 0 us/frame, prevents editor menu/facade drift
- [x] Architecture source truth corrected | Justification: `DATA_MONOLITH_H8BIN_SPEC.md` and the binary payload ledger now mention the editor asmdef/import boundary and no longer claim editor `File.WriteAllBytes` output. | Alternatives Rejected: leaving a stale write path in the binary contract after the atomic temp-promote gate. | Estimate: 0 us/frame
- [x] Editor/runtime isolation scan repeated | Justification: Data Monolith editor/runtime sources contain no direct sibling domain namespace imports and the scoped forbidden-pattern scan stayed clean. | Alternatives Rejected: broad repo failure as SHINOBU_103 pass/fail; other owners still have unrelated emergency mocks and editor parsers. | Estimate: 120 us
- [x] Build suppressed under CPU guard | Justification: CPU samples were `54.6, 60.44, 100, 100%`; no compiler process was active, but the explicit >50% guard forbids another build. | Alternatives Rejected: launching a compile to regenerate ignored csproj files under saturated CPU. | Estimate: avoided compile contention

## Execution Loop 19: Static Consumer Mock Boundary

- [x] Scavenging consumer audit performed | Justification: Task 01 names existing data consumers; `ScavengingLootOracle` was the remaining runtime path that defaulted to a generated emergency CDF when resolving real loot. | Alternatives Rejected: treating bootstrap fail-closed as sufficient while a downstream consumer still synthesized static data. | Estimate: 0 us/frame until first queued loot request
- [x] Production loot fake demoted | Justification: `ScavengingLootOracle` now primes its Vault loot table from `H8StaticDataArena` `LootCdf` records and player builds with no monolith loot rows resolve no eligible loot instead of scheduling the emergency table. | Alternatives Rejected: deleting the emergency mock; rejected because editor self-audit/manual tooling still needs a deterministic fallback. | Estimate: avoids 4 fake CDF entries becoming production truth
- [x] Zero-GC bridge kept | Justification: bridge copies `ReadOnlySpan<H8LootCdfRecord>` into an existing Vault `NativeArray<LootTableEntryDTO>` with no file I/O, arrays, LINQ, or text parsing. | Alternatives Rejected: routing through CSV, `File.ReadAllBytes`, or a managed list at runtime. | Estimate: 0 B hot-path allocation; cold first-use copy bounded by loot row count
- [x] Docs updated | Justification: Data Monolith spec and binary payload ledger now record the Scavenging `LootCdf` consumer bridge and state that it is static-source proof only. | Alternatives Rejected: code-only cross-domain consumer mutation. | Estimate: 0 us/frame
- [x] Static scans repeated | Justification: Data Monolith owned runtime/editor files still have no direct sibling-domain namespace imports; Scavenging references only the monolith owner API for this static-data bridge. Diff whitespace check passed with CRLF warnings only. | Alternatives Rejected: broad repo emergency-mock deletion. | Estimate: 150 us
- [x] Build suppressed | Justification: CPU/process guard sampling timed out twice, indicating the host is not safe for a new build probe; known external missing-type compile wall still exists. | Alternatives Rejected: launching `dotnet build` after a consumer patch under an unresolved hardware guard. | Estimate: avoided another compile slot

## Execution Loop 20: Inspector Gate Hardening

- [x] Task 20 inspector contract rechecked | Justification: the UI Toolkit inspector still had an ad hoc checksum/section display that could diverge from the release prebuild validator. | Alternatives Rejected: claiming Task 20 from a local checksum label only; rejected because player builds use a stricter artifact gate. | Estimate: 0 us/frame, editor-only
- [x] Shared validator surfaced | Justification: `H8DataMonolithCompilerWindow.InspectBinary()` now calls `H8DataMonolithCompiler.TryValidateOutputBlob()` before printing local binary diagnostics. | Alternatives Rejected: duplicating validator rules in the window; creates two static-data truth routes. | Estimate: 0 us/frame, editor-only
- [x] Docs updated | Justification: the H8BIN spec and binary payload ledger now state that the inspector must use the same validation contract as the prebuild artifact gate. | Alternatives Rejected: code-only editor behavior; next owner could reintroduce a weaker inspector. | Estimate: 0 us/frame
- [x] Static guard repeated | Justification: `rg` confirmed the window calls `TryValidateOutputBlob`; editor/runtime Data Monolith sources still have no direct sibling domain imports; diff whitespace check reports only CRLF normalization warnings. | Alternatives Rejected: launching a build for an editor-only one-line contract bridge while the external compile wall is already known. | Estimate: 60 us
- [x] Build suppressed | Justification: no `dotnet build` was launched because this patch is editor-only, static checks covered the changed route, and the last guarded build is blocked by external missing contracts before Data Monolith diagnostics. | Alternatives Rejected: burning another compile slot to rediscover unrelated missing types. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 21: Facade Error Preservation

- [x] Cross-reference error route rechecked | Justification: Task 18 requires validation errors in the editor facade; `RefreshAll()` called `InspectBinary()`, and the inspector validation could overwrite `H8DataMonolithCompiler.LastError` after a failed bake. | Alternatives Rejected: trusting the transient status label while the static compiler error state was overwritten by a missing/stale blob check. | Estimate: 0 us/frame, editor-only
- [x] Inspector made non-destructive | Justification: `TryValidateOutputBlob(out error, updateLastError: false)` lets the inspector surface blob validity without destroying baker/cross-reference failure messages. | Alternatives Rejected: duplicating error state in the window; rejected because the compiler remains the single owner of bake validation state. | Estimate: 0 us/frame, editor-only
- [x] Baker error displayed | Justification: the binary inspector now prints `last-baker-error=` when the compiler has a stored bake/cross-reference failure. | Alternatives Rejected: hiding failures behind Console logs only; designers need the facade to show them. | Estimate: 0 us/frame, editor-only
- [x] Static guard repeated | Justification: `rg` confirmed the non-destructive inspector call and last-baker-error line; direct sibling-domain import scan remains clean. Editor-only `File.ReadAllText/ReadAllLines` remain cold source ingestion and do not touch runtime. | Alternatives Rejected: broad repo parser deletion outside SHINOBU_103 authority. | Estimate: 70 us
- [x] Build suppressed | Justification: no `dotnet build` was launched because this patch is editor-only, diff/static scans covered the route, and the known external compile wall remains before Data Monolith diagnostics. | Alternatives Rejected: burning another compile slot to rediscover unrelated missing contracts. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 22: Runtime Directory Gate Parity

- [x] Runtime/editor validator drift found | Justification: editor validation rejected wrong section order, wrong record sizes, empty sections with nonzero offsets, and bad data-start alignment, while runtime `IsDirectoryValid()` only checked broad ranges. | Alternatives Rejected: relying on the prebuild gate alone; runtime must still reject tampered or stale blobs. | Estimate: 0 us/frame, boot-only validation
- [x] Shared record-size authority added | Justification: `H8DataLayoutAudit.GetExpectedRecordSize()` now owns expected section stride math and the editor compiler delegates to it. | Alternatives Rejected: maintaining separate editor/runtime stride switches; creates drift between proof surfaces. | Estimate: 0 us/frame
- [x] Runtime directory gate tightened | Justification: runtime now requires section count 26, section ids in canonical order, expected record size, exact data-start offset, 16-byte data-start alignment, zero offset for empty sections, section offsets after data start, and localization directory/table mirror. | Alternatives Rejected: permissive range-only directory acceptance. | Estimate: boot-only O(section-count), 26 iterations
- [x] Static guard repeated | Justification: `rg` confirmed the shared record-size path; diff whitespace check reports only CRLF normalization warnings; no direct sibling domain imports or scoped runtime parser/property/Pack violations were found. | Alternatives Rejected: launching full build under known external compile wall for a boot validator patch. | Estimate: 90 us
- [x] Build suppressed | Justification: no `dotnet build` was launched because static checks cover the source route and a prior guarded build already fails on external missing contracts before Data Monolith diagnostics. | Alternatives Rejected: spending another compile slot on unrelated domain blockers. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 23: Cross-Reference Provenance Gate

- [x] Task 14 validator weakness found | Justification: recipe/loot/economy checks rejected bad hashes but lost file/line/token evidence after recipe sorting and `LootCdf` rebuild. | Alternatives Rejected: accepting anonymous owner/hash errors; insufficient for designer fail-fast repair. | Estimate: 0 us/frame, editor-only
- [x] Source-row provenance staged | Justification: CSV rows now carry path/line metadata, and JSON items/recipes get synthetic source-index rows so validation can report exact authored source. | Alternatives Rejected: adding source fields to runtime DTOs; rejected as ABI waste. | Estimate: 0 us/frame
- [x] Raw-source reference validation staged | Justification: item `recipe`, recipe `output`/`ingredients`, loot `item`, and economy item/recipe fields are validated before blob output from raw source rows, with packed token indices for semicolon lists. | Alternatives Rejected: validating only baked records; post-sort records lose source and can hide the bad field. | Estimate: 0 us/frame, editor-only
- [x] Docs updated | Justification: Data Monolith spec and binary payload ledger now document the source-provenance cross-reference gate. | Alternatives Rejected: code-only validator behavior; future owners need the Task 14 failure contract. | Estimate: 0 us/frame
- [x] Static guard repeated | Justification: `rg` confirmed the old baked-record recipe/loot validation hooks are gone, raw provenance hooks are present, runtime forbidden-pattern scan stayed clean, and direct sibling namespace scan stayed clean. | Alternatives Rejected: relying on visual diff. | Estimate: 100 us
- [x] Build suppressed | Justification: no `dotnet build` was launched because the patch is editor-only, no `dotnet`/`csc` process was present, and the last guarded build is still blocked by unrelated missing contracts before Data Monolith diagnostics. | Alternatives Rejected: spending another compile slot to rediscover external Gameplay/Visor/Equipment/Fauna/World errors. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 24: Automated Bake Debounce Gate

- [x] Source watcher fault isolated | Justification: `AssetPostprocessor.OnPostprocessAllAssets` called `BakeAll()` synchronously during import, while the filesystem watcher baked on the next editor update with no write-stability window. | Alternatives Rejected: accepting import-time bake storms as editor-only; rejected because this creates false failed monoliths and iteration stalls. | Estimate: prevents repeated editor bake passes during CSV save/import bursts
- [x] Single-owner bake scheduler staged | Justification: both AssetPostprocessor and FileSystemWatcher now route through `H8DataMonolithFileSystemWatcher.RequestBake()`, storing the last source-change tick and one pending flag. | Alternatives Rejected: two independent auto-bake routes; violates one route -> one proof. | Estimate: 0 us/frame, editor-only
- [x] Debounce and compile guard staged | Justification: pending auto-bakes wait 0.75 seconds after the latest source change, skip while Unity is compiling, and prevent overlapping `BakeAll()` calls with an interlocked in-progress flag. | Alternatives Rejected: baking a half-written CSV or while scripts are compiling. | Estimate: avoids one or more full editor bake passes per multi-write source save
- [x] Static guard repeated | Justification: `rg` confirmed direct source-watcher bake calls were removed, debounce fields are present, scoped forbidden-pattern scans stayed clean, direct sibling namespace scan stayed clean, and `git diff --check` reports only CRLF normalization warnings. | Alternatives Rejected: using a full build to verify editor automation under a known external compile wall. | Estimate: 90 us static audit
- [x] Build suppressed | Justification: no `dotnet build` was launched because the patch is editor-only automation and the previous guarded build is blocked by external missing contracts before Data Monolith diagnostics. | Alternatives Rejected: burning another compile slot for a watcher debounce patch. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 25: Bounded CSV Worker Gate

- [x] Task 06 concurrency weakness isolated | Justification: `ReadCsvSourcesParallel` created one `Task.Run` worker per CSV file, which scales poorly if content authors split large data across many source files. | Alternatives Rejected: assuming current small CSV count remains permanent; rejected because Task 06 targets large static-data import. | Estimate: prevents unbounded editor threadpool pressure
- [x] Bounded worker pool staged | Justification: CSV import now creates at most `min(fileCount, max(1, Environment.ProcessorCount - 1))` workers and distributes files with an interlocked index. | Alternatives Rejected: serial import and unbounded per-file task fanout. | Estimate: editor-only; one wake per worker instead of one wake per file
- [x] Empty-source guard staged | Justification: zero CSV files now return immediately without allocating a zero-length worker array and calling `Task.WaitAll`. | Alternatives Rejected: relying on `Task.WaitAll` empty-array behavior; unnecessary editor work. | Estimate: trivial editor-only
- [x] Static guard repeated | Justification: `rg` confirmed bounded worker markers; forbidden runtime pattern scan, direct sibling namespace scan, and diff whitespace check remain clean except CRLF warnings. | Alternatives Rejected: launching a build for editor-only ingestion scheduling while the external compile wall remains. | Estimate: 70 us static audit
- [x] Build suppressed | Justification: no `dotnet build` was launched because the change is editor-only and the last guarded build is blocked by external contracts before Data Monolith diagnostics. | Alternatives Rejected: spending another compile slot after a bounded Task scheduler edit. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 26: Facade Literal Bake Button Gate

- [x] Task 18 literal UI drift isolated | Justification: the compiler facade exposed `BAKE MONOLITH`, but the button was a normal 160 px toolbar control despite the XML asking for a giant bake button. | Alternatives Rejected: treating wording as cosmetic; rejected because this is the primary human-control command. | Estimate: 0 us/frame, editor-only
- [x] Bake command prominence staged | Justification: the bake button is now 260 x 42 px, bold, and vertically centered in the toolbar. | Alternatives Rejected: hidden menu-only bake and ordinary toolbar-sized primary command. | Estimate: 0 us/frame, editor-only
- [x] Static guard repeated | Justification: `rg` confirmed the button dimensions/style, diff whitespace check reports only CRLF warnings, and runtime forbidden-pattern/direct-sibling scans stayed clean. | Alternatives Rejected: launching a build or Unity import for UI Toolkit style-only proof under known external compile wall. | Estimate: 40 us static audit
- [x] Build suppressed | Justification: no `dotnet build` was launched because the change is editor UI style-only and the known external compile wall still exists. | Alternatives Rejected: spending a compile slot on a literal facade polish patch. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 27: Hot Reload Locality Gate

- [x] Same-process reload bounce removed | Justification: `NotifyBake()` now queues the validated monolith path directly instead of connecting to the editor's own loopback socket and allocating an encoded TCP payload. | Alternatives Rejected: self-IPC for same-process editor reload; it can target the wrong Unity instance on port collision and wastes editor work. | Estimate: editor-only; avoids one TCP connect and payload allocation per play-mode bake
- [x] Reload path authority constrained | Justification: socket packets are capped to 1024 chars and accepted only when the full path equals the authoritative `static_data.h8bin` output. | Alternatives Rejected: accepting arbitrary loopback paths; violates one static-data owner route. | Estimate: 0 us/frame, editor-only input rejection
- [x] Socket lifecycle hardened | Justification: hot reload stops on play-mode exit, assembly reload, and editor quit; failed listener startup now closes the listener and clears thread state. | Alternatives Rejected: relying on background thread shutdown alone. | Estimate: prevents editor shutdown/reload leak risk
- [x] Build suppressed | Justification: no `dotnet build` was launched because this is editor-only hot-reload plumbing, static scans are sufficient for the changed route, and the external compile wall remains before Data Monolith diagnostics. | Alternatives Rejected: spending a compile slot for a loopback/socket lifecycle patch. | Estimate: avoided ~98 s repeat wall time

## Execution Loop 28: Scavenging Native Editor CSV Gate

- [x] Editor managed CSV staging removed | Justification: the Scavenging loot self-audit facade now reads selected CSV files through `FileStream.Read(Span<byte>)` directly into a Temp `NativeArray<byte>` before invoking the existing native byte parser. | Alternatives Rejected: `File.ReadAllBytes` plus managed `byte[]` copy before native parsing; rejected because static-data consumer tooling should not preserve whole-file managed staging. | Estimate: editor-only; removes one file-sized managed allocation and one byte-copy loop per manual loot CSV import
- [x] Short-read failure surfaced | Justification: the editor facade now rejects incomplete file reads before parsing instead of passing partial bytes into the loot distribution parser. | Alternatives Rejected: trusting a single stream read; unsafe for edited files or shared writer handles. | Estimate: 0 us/frame, editor-only correctness gate
- [x] Consumer boundary documented | Justification: the H8BIN spec and binary payload ledger now state that Scavenging's editor/manual CSV self-audit is a native ingest path, while production loot still comes from `LootCdf` monolith rows. | Alternatives Rejected: leaving the consumer bridge as code-only behavior. | Estimate: 0 us/frame
- [x] Build suppressed | Justification: no `dotnet build` was launched because the change is editor-only Scavenging self-audit I/O, CPU guard samples were `100, 100, 100%`, and active `dotnet` worker processes were present. | Alternatives Rejected: spending a compile slot under an explicit hardware-guard violation to rediscover unrelated missing contracts. | Estimate: avoided ~98 s repeat wall time

## Tasks

- [x] Task 01: EMERGENCY_MOCK_ERADICATION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 02: LEGACY_PARSER_PURGE [IMPLEMENTED / PENDING COMPILE]
- [x] Task 03: CS1612_ENCAPSULATION_PURGE [IMPLEMENTED / PENDING COMPILE]
- [x] Task 04: ARM64_PADDING_RECONSTRUCTION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 05: ENDIANNESS_CONTRACT_ENFORCEMENT [IMPLEMENTED / PENDING COMPILE]
- [x] Task 06: MULTI_THREADED_CSV_INGESTION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 07: BINARY_BLOB_ASSEMBLY_KERNEL [IMPLEMENTED / PENDING COMPILE]
- [x] Task 08: CRYPTOGRAPHIC_SEAL_GENERATION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 09: THE_DEAR_LIE_TEXT_POOL [IMPLEMENTED / PENDING COMPILE]
- [x] Task 10: RUNTIME_ARENA_HYDRATION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 11: CONTINUOUS_MEMORY_MAPPING_FALLBACK [IMPLEMENTED / PENDING COMPILE]
- [x] Task 12: ZERO_COST_SECTION_ROUTING [IMPLEMENTED / PENDING COMPILE]
- [x] Task 13: AUP_CONSTANTS_BAKING [IMPLEMENTED / PENDING COMPILE]
- [x] Task 14: FAST_FAIL_CROSS_REFERENCE_VALIDATION [IMPLEMENTED / PENDING COMPILE]
- [x] Task 15: BURST_COMPATIBLE_LOOKUP_HELPERS [IMPLEMENTED / PENDING COMPILE]
- [x] Task 16: ZERO_INIT_OVERHEAD_BYPASS [IMPLEMENTED / PENDING COMPILE]
- [x] Task 17: TELEMETRY_BAKER_RECORDER [IMPLEMENTED / PENDING COMPILE]
- [x] Task 18: DATA_MONOLITH_COMPILER_WINDOW [IMPLEMENTED / PENDING COMPILE]
- [x] Task 19: CSV_SCHEMA_GENERATOR [IMPLEMENTED / PENDING COMPILE]
- [x] Task 20: LIVE_BINARY_INSPECTOR_GIZMO [IMPLEMENTED / PENDING COMPILE]

## Verification Blockers

- [ ] Compile verification blocked by external domain errors after a guarded build attempt: Gameplay/Visor/Equipment/Fauna/World missing DTOs/namespaces stop `Hecton8.Core.csproj` before full-project verification can prove Data Monolith changes. Editor facade import proof now also requires Unity regeneration/import of `Hecton8.DataMonolith.Editor.asmdef`.
- [ ] Unity bake deferred: requires guarded compile/import first, then a guarded Unity batch execution slot.
- [ ] `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent; runtime player boot will intentionally fail closed until the editor baker emits the binary.
