# LOG_SHINOBU_103

## 2026-05-19 Data Monolith Compiler Pass

What was wrong:
- `static_data.h8bin` authority was absent from `Assets/StreamingAssets/Hecton8/DataMonolith/`.
- Bootstrap tolerated a missing Data Monolith outside the editor by treating `Missing` as acceptable.
- Monolith DTOs used or inherited packed/sequential layout risk; `H8ItemRecord` had an 80-byte shape but a stale 64-byte constant.
- Runtime arena hydration used `File.ReadAllBytes`, causing a blob-sized managed allocation before native copy.
- Compiler ignored current designer table names `Fauna.csv`, `Economy.csv`, and `Physics.csv`.
- Header/directory/section-table writes relied on native struct copy instead of explicit Little-Endian emission.

What was done:
- Rebuilt `H8DataBlobHeader` as 16 bytes, `H8DataBlobDirectory` as 64 bytes, and `H8DataSectionEntry` as 16 bytes with explicit field offsets.
- Removed `Pack=1` from Data Monolith DTOs and added explicit `H8EconomyRecord`, `H8PhysicsConstantsRecord`, and 64-byte `H8DataMonolithTelemetryEntry`.
- Extended `H8DataMonolithCompiler` to parse `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv`; inject FNV-1a IDs; build aligned sections; emit explicit Little-Endian header/directory/table; and seal payload bytes `[16..end)` with XXHash3-64.
- Replaced per-record managed scratch allocation in the baker with stack scratch and a fail-closed 256-byte record limit.
- Updated `H8StaticDataArena` to request payload/telemetry buffers from `GlobalDataVault`, use `UninitializedMemory`, attempt MMF on desktop, fall back to direct `FileStream.Read(Span<byte>)` into Vault-owned bytes, validate magic/version/header size/directory/checksum, and expose `GetSectionSpan<T>`.
- Added Burst-decorated item hash lookup helper plus `ReadOnlySpan<H8ItemRecord>` wrapper.
- Added runtime telemetry ring and dump output to `Docs/AgentLogs/Dump_DATA_MONOLITH.bin` and `Docs/AgentLogs/Dump_SHINOBU_103.bin` on load failure or >50ms read.
- Added UI Toolkit `H8DataMonolithCompilerWindow` with source list timestamps, `BAKE MONOLITH`, schema/template generation, reflection layout manifest, and live binary inspector.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the new authority route.

Cinematic cheats used:
- The Data Monolith is a Dear Lie for static data: one binary section table replaces runtime text parsing, reflection, and scattered data probes.
- Text is a UTF-8 byte pool with offsets/lengths, not managed strings.
- Lookup is sorted fixed-stride binary search, not dictionary hydration.

Exact microseconds saved estimates:
- Runtime CSV/JSON parsing removed from this authority path: 3000-20000 us at boot depending source size.
- Managed blob staging removed for a 10 MB payload: expected multi-ms GC/heap pressure avoidance on i3/MX350-class hardware.
- Stack scratch in editor bake: thousands of short-lived allocations removed for large record sets.
- Section span access: 5-50 us saved per large table access versus managed copy/list hydration.
- Per-frame cost: 0 us; all work is boot/editor/cold-path.

Verification:
- `git diff --check` passed for touched files; only repository line-ending warnings were reported.
- Static grep found no `Pack=1`, DTO `get; set;`, runtime `File.ReadAllBytes`, `string.Split`, or private owned `new NativeArray<byte>` fallback in Data Monolith runtime/compiler code. Remaining byte arrays are editor-only inspector/localization scratch.
- `dotnet build` and Unity bake were not launched because CPU guard samples reported 96-100% total CPU. No dotnet/csc process was active, but the explicit >50% CPU rule blocks compile verification.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS_STAGED">Player bootstrap now hard-fails Data Monolith absence/invalidity via `FatalArchitectureException`; editor missing-file tolerance remains for import/CI.</TASK>
    <TASK id="02" status="PASS_STAGED">Runtime Data Monolith path no longer uses text parsing or `File.ReadAllBytes`; broader non-domain runtime CSV hotloaders are outside this domain and were not edited.</TASK>
    <TASK id="03" status="PASS_STAGED">Header, directory, and section table use explicit layouts and raw fields.</TASK>
    <TASK id="04" status="PASS_STAGED">Header=16, Directory=64, SectionEntry=16; DTO layouts are explicit; validation gate exists in `H8DataLayoutAudit`.</TASK>
    <TASK id="05" status="PASS_STAGED">Header/directory/section table use explicit Little-Endian writers; record payloads fail closed on non-Little-Endian editor hosts.</TASK>
    <TASK id="06" status="PASS_STAGED">Current CSVs are ingested; hash IDs are derived from authored strings.</TASK>
    <TASK id="07" status="PASS_STAGED">Blob assembly aligns sections to 16 bytes and records section offsets/sizes/counts.</TASK>
    <TASK id="08" status="PASS_STAGED">XXHash3-64 seal covers bytes after the 16-byte header.</TASK>
    <TASK id="09" status="PASS_STAGED">String pool stores UTF-8 bytes with offsets/lengths; records do not contain managed strings.</TASK>
    <TASK id="10" status="PASS_STAGED">Runtime validates header and checksum before readiness; failIfMissing path throws.</TASK>
    <TASK id="11" status="PASS_STAGED">Desktop MMF first; Android/iOS/WebGL skip MMF and use direct FileStream fallback.</TASK>
    <TASK id="12" status="PASS_STAGED">`GetSectionSpan<T>(uint)` and typed overload return pointer-backed spans.</TASK>
    <TASK id="13" status="PASS_STAGED">Physics constants section bakes mass/drag/buoyancy/crush/AUP defaults from `Physics.csv`.</TASK>
    <TASK id="14" status="PASS_STAGED">Item-backed recipe/loot references fast-fail in baker.</TASK>
    <TASK id="15" status="PASS_STAGED">Burst binary-search helper exists with `[BurstCompile]` flags and `[NoAlias]` pointer path.</TASK>
    <TASK id="16" status="PASS_STAGED">Vault payload arena uses `NativeArrayOptions.UninitializedMemory`; no private fallback byte arena remains.</TASK>
    <TASK id="17" status="PASS_STAGED">300-entry telemetry ring/cursor use Vault IDs 71104/71105; payload uses 71103.</TASK>
    <TASK id="18" status="PASS_STAGED">UI Toolkit compiler window created.</TASK>
    <TASK id="19" status="PASS_STAGED">Schema/template generator plus reflection layout manifest created.</TASK>
    <TASK id="20" status="PASS_STAGED">Binary inspector validates checksum and lists section table.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <H8DataBlobHeader size="16">0:uint Magic; 4:ushort FormatVersion; 6:ushort HeaderBytes; 8:ulong Checksum64.</H8DataBlobHeader>
    <H8DataBlobDirectory size="64">0:uint Magic; 4:ushort FormatVersion; 6:ushort SectionCount; 8:uint SectionTableOffset; 12:uint SectionTableBytes; 16:uint BlobBytes; 20:uint DataStartOffset; 24:uint LocalizationOffset; 28:uint LocalizationBytes; 32:uint Flags; 36:uint WorldSeed; 40:uint AppVersionHash; 44-60:uint Reserved0-4.</H8DataBlobDirectory>
    <H8DataSectionEntry size="16">0:uint SectionId; 4:uint RecordSize; 8:uint Count; 12:uint OffsetBytes.</H8DataSectionEntry>
    <H8ItemRecord size="80">0:uint HashId; 4:uint RecordIndex; 8:uint CategoryHash; 12:uint Flags; 16/24:ulong RecipeMask0/1; 32-44:float Mass/Volume/Quality/Heat; 48:uint YieldHash; 52/56:uint UTF8 offsets; 60/64:uint lengths; 68/70:ushort stack/count; 72:uint Cost; 76:float AccessFrequency.</H8ItemRecord>
    <Telemetry size="64">H8DataMonolithTelemetryEntry is one 64-byte cache line.</Telemetry>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    The monolith format is universal. Runtime owners use `GlobalQualityWeight` after reading fixed sections. At low weight, consumers can process fewer records, skip high-frequency scans, and use nearest section/index lookups; high/ultra consumers can scan richer sections or upload full spans. No low/high binary split was introduced.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>
    Payload buffer: BufferID 71103. Telemetry ring: BufferID 71104. Telemetry cursor: BufferID 71105. No private persistent byte arena is allocated by `H8StaticDataArena`; missing or unresolved `GlobalDataVault` fails closed.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    No new scheduled jobs were added. Burst lookup helper consumes a no-alias item pointer and returns a copied record. Runtime load is synchronous boot IO and does not inject JobHandle dependencies.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling runtime assembly reference was added. Data runtime routes through Core/GlobalRegistry/DataVault. Compile verification is pending because CPU guard blocked build launch.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: runtime/parser truth could require O(total CSV bytes) parsing and scattered probes. After: O(1) section routing plus O(log n) sorted item lookup; text is byte-pool offsets, not managed string hydration.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 Post-Mandate Addendum

What was still weak:
- Failed runtime file reads could reset cached arena/telemetry handles before writing the black-box dump.
- The success telemetry entry recorded the final `Loaded` state with zero IO ticks/path flags instead of the actual MMF/FileStream route.
- Recursive `Data/Balance` source discovery could see generated `Baked` manifests or future `Schemas` templates as source inputs.

What was done:
- `H8StaticDataArena` now records/dumps telemetry before arena shutdown on read failure.
- `H8StaticDataArena` now stores actual `_lastReadTicks` and `_lastReadPathFlags` and writes them into the final `Loaded` telemetry event.
- `H8DataMonolithCompiler` now catches the entire bake pipeline, not only blob write, so parse/cross-reference failures populate `LastError`.
- `H8DataMonolithCompiler` now excludes `Data/Balance/Baked` and `Data/Balance/Schemas` from source enumeration and watcher-triggered rebakes.
- `H8DataMonolithCompiler` is now a public editor type so Unity `-executeMethod ...BakeFromMenu` can call the batch bake route without relying on internal-type reflection.
- `H8CreatureSoAReconstructJob` and `H8ItemSoAReconstructJob` now use required Burst flags and `[NoAlias]` NativeArray fields.

Cinematic cheats used:
- Still one binary truth: generated artifacts cannot feed back into the monolith source route.

Exact microseconds saved:
- Runtime frame cost: 0 us.
- Editor loop prevention: avoids useless rebakes caused by generated schema/manifest writes; scale depends on CSV size, expected several ms to seconds on weak machines during large authoring batches.
- Same-domain SoA unpack jobs: estimated 2-10 us saved per large reconstruction pass by removing alias pessimism and default Burst settings.

Verification:
- `git diff --check` passed for touched files; only repository line-ending warnings were reported.
- Static Data Monolith scan after the addendum found no `Pack=1`, DTO auto-properties, runtime `File.ReadAllBytes`, `.Split()`, direct sibling-domain reference, `UnityEngine.Random`, or `Time.deltaTime`.
- Static Data Monolith Burst scan found no remaining bare `[BurstCompile]` in `Assets/_Project/Scripts/Data/Monolith`.
- CPU guard remained active at `89-99%`; `dotnet build` and Unity bake are still deferred.

## 2026-05-19 Compile Gate Attempt Addendum

What was wrong:
- The first guarded C# compile gate did not reach SHINOBU_103 diagnostics. It stopped on `CS2001` because `Hecton8.Core.csproj` still includes `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`, while the working tree has that tracked World-domain source file and its `.meta` deleted.

What was done:
- Ran exactly one build after CPU/dotnet guard allowed it: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
- Captured the blocking error: `CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs' could not be found. [C:\hades\Hecton8\Hecton8.Core.csproj]`.
- Verified with git that the missing file is tracked and currently deleted: `D Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.
- Did not restore or replace the World file from HEAD. That file is outside the Data Monolith boundary and may be an intentional deletion by another agent; hiding the dependency break would violate one-owner/one-route.

Cinematic cheats used:
- None. This is an integration gate failure, not a runtime data-path optimization.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Workstation protection: no repeated compile attempts after the external `CS2001`; avoided burning additional build cycles while the first blocking source reference is unresolved.

Verification:
- SHINOBU_103 remains implemented but not compile-proven.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` still requires a successful compile/import and guarded bake before it can be claimed as a present payload artifact.
- Follow-up correction: the missing World source is absent from HEAD/index; the stale `Hecton8.Core.csproj` include was removed as a one-line build metadata fix.

## 2026-05-19 Static Polish Addendum

What was wrong:
- `H8StaticLocalizationReference` was 12 bytes, which violated the domain policy that monolith DTOs stay 8/16-byte aligned even when currently cold.
- `H8DataMonolithCompilerWindow` could show generated schema/baked CSV files in the source list, creating false source-route evidence.
- CSV schema generation had designer-friendly hardcoded templates, but the reflection proof for raw struct templates was too weak for Task 19.
- `TryReadLocalizedText` trusted the destination char span size before UTF-8 decode.

What was done:
- Padded `H8StaticLocalizationReference` to 16 bytes and added it to `H8DataLayoutAudit`.
- Added `Encoding.UTF8.GetCharCount` guard before zero-allocation decode into caller-owned char spans.
- Made `H8DataMonolithCompiler.IsSourcePath` absolute/relative-safe and routed compiler-window source display through it.
- Added reflection-generated struct CSV templates for `H8ItemRecord`, `H8CreatureTraitRecord`, `H8EconomyRecord`, and `H8PhysicsConstantsRecord`.

Cinematic cheats used:
- No new visual cheat. This is structural hardening: binary metadata stays fixed-stride and generated artifacts cannot masquerade as authored truth.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Cold UI decode: avoids exception path on undersized buffers; cost is one preflight char-count scan only when text is requested.
- Editor: prevents generated-file source churn and schema drift; savings are authoring-loop dependent.

Verification:
- Static grep after patch found no Data Monolith `Pack=1`, runtime `File.ReadAllBytes`, `.Split()`, direct sibling-domain reference, `UnityEngine.Random`, `Time.deltaTime`, or bare `[BurstCompile]`.
- `git diff --check` on touched SHINOBU_103 files passed; only line-ending warnings were reported.

## 2026-05-19 CSV Surface Addendum

What was wrong:
- The compiler supported the intended four CSV tables, but the live header surface had not been proven after the last polish pass.

What was done:
- Read `Data/Balance/Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv`.
- Confirmed row counts: Items=4, Fauna=3, Economy=3, Physics=3.
- Confirmed generated `Data/Balance/Baked` payloads exist but remain excluded from Data Monolith source enumeration.
- Rechecked CPU/dotnet guard: CPU samples were `100, 98, 94, 76, 62%` and a `dotnet` process was active, so no second build was launched.

Cinematic cheats used:
- None new. This is source-route proof: human CSV remains authoring truth, generated binaries remain output evidence only.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Editor: avoids a predictable failed bake/import loop if headers drift; current headers match parser aliases.

Verification:
- `static_data.h8bin` is still absent. This is correct until the compile/import and bake gates run under the CPU/process guard.

## 2026-05-19 Economy Cross-Reference Gate Addendum

What was wrong:
- Task 14 named Economy item/recipe references explicitly. The validator rejected broken recipe and loot references, but optional Economy columns such as `item_id`, `recipe`, or `ingredients` could still pass unchecked.

What was done:
- `H8DataMonolithCompiler` now stores raw Economy rows in editor-only scratch state.
- Before writing the blob, the baker validates optional Economy item/reference fields against the Item hash set.
- The 64-byte `H8EconomyRecord` ABI remains unchanged because current live `Economy.csv` has no item-reference fields.

Cinematic cheats used:
- Runtime foreign-key checking remains nonexistent. Broken references die once in the editor bake gate, not every boot or lookup.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Future runtime avoidance: no defensive item-lookup branch is added for Economy records.

## 2026-05-19 Final Blob Alignment Addendum

What was wrong:
- Section offsets were 16-byte aligned, but the final file size could end on an arbitrary UTF-8 pool byte count. That leaves a product binary hygiene edge case even though runtime section walks are safe.

What was done:
- `H8DataMonolithCompiler.BuildBlob` now calls `Align16(stream)` after all sections and before directory/checksum patching.
- Directory `BlobBytes` and the XXHash3 seal include the trailing padding; section counts still exclude that padding.

Cinematic cheats used:
- No runtime branch. Alignment is paid once at bake time with 0-15 padding bytes.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Future validation: avoids binary-ledger misalignment churn and keeps mmap readers on clean 16-byte blob boundaries.

## 2026-05-19 Unsigned UTF-8 Offset ABI Addendum

What was wrong:
- The text pool used byte lengths, but DTO offsets were signed `int` fields with `-1` missing sentinels. Task 09 requires unsigned string-pool offsets.

What was done:
- Converted Data Monolith UTF-8 offset fields to `uint`.
- `LocalizationPool` now emits `uint` offsets and `uint.MaxValue` as the missing sentinel.
- `H8StaticDataArena` exposes unsigned zero-allocation text span/decode paths while preserving guarded signed overloads for legacy callers.
- `LocRegistry` was touched only at the static-data alias boundary: it now rejects offsets above `int.MaxValue` before writing into its existing int-indexed UTF-8 table.

Cinematic cheats used:
- Missing strings are represented by a sentinel value, not separate validity flags or runtime dictionary lookups.

Exact microseconds saved:
- Runtime: 0 us/frame.
- ABI cleanup prevents future per-record signed/unsigned translation when exporting the monolith to native/GPU consumers.

## 2026-05-19 Verification Guard Addendum

What was wrong:
- Compile/bake verification is still hardware-gated, not logic-gated.

What was done:
- Rechecked CPU/process guard after ABI hardening. Samples were `95, 100, 89, 78, 77%`; `dotnet` PID `22952` and `csc` PID `67260` were active.
- Did not launch `dotnet build`.
- Rechecked stale `HectonMapMagicVegetationBridgeFloraCollisionProxies` reference; no match remains in `Hecton8.Core.csproj` or source tree.

Cinematic cheats used:
- None.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Workstation protection: avoided competing with an active compiler process.

## 2026-05-19 Player Vault Purge Addendum

What was wrong:
- `H8StaticDataArena` still owned a no-vault persistent `NativeArray<byte>` fallback. That preserved a second memory owner for the monolith payload and made H-PHI/Vault accounting incomplete.
- Load telemetry still had a fallback-native-array flag even though XML Task 11 requires the FileStream RAM path to use `GlobalDataVault`.

What was done:
- Removed the private byte-arena allocation, sentinel registration, owned-dispose branch, and fallback-native-array telemetry flag.
- `TryAllocateArena` now succeeds only when `GlobalRegistry.DataVault` returns BufferID `71103` with enough capacity.
- MMF and FileStream remain as loading routes, but both hydrate the same Vault-owned payload view.
- If the Vault is absent, the arena fails closed through `ReadFailed`; non-editor player bootstrap already escalates that to `FatalArchitectureException`.

Cinematic cheats used:
- No runtime simulation was added. The static-data Dear Lie remains one binary table plus offsets: no parser fallback, no managed dictionary hydration, no second memory truth.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Boot memory: prevents one hidden blob-sized native allocation outside Vault accounting; the saved CPU depends on payload size and avoids allocator/sentinel churn in the no-vault path.

Verification:
- Static scan no longer finds `_arenaOwnedByNativeArray`, `PathFlagFallbackNativeArray`, or `new NativeArray<byte>` in `H8StaticDataArena`.
- Data Monolith static scan remains clean for `Pack=1`, DTO auto-properties, runtime `File.ReadAllBytes`, `.Split()`, bare `[BurstCompile]`, and `JobHandle.Complete` in the touched monolith domain.
- Compile and Unity bake remain pending: latest guard sampled `100, 49.74, 77.31, 87.71, 96.53, 53.42, 43.98, 39.76%` CPU with active `csc` PID `59156` and `dotnet` PID `24932`.

## 2026-05-19 Spec Reconciliation / Mock Boundary Addendum

What was wrong:
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md` still described the pre-hardening ABI: header fields were wrong, `H8ItemRecord` was listed as 64 bytes, Economy/PhysicsConstants section IDs were absent, and the text said records were "packed".
- That documentation would make the next consumer build a wrong stride reader against an 80-byte item section.

What was done:
- Updated the spec header table to `Magic:uint`, `FormatVersion:ushort`, `HeaderBytes:ushort`, `Checksum64:ulong`.
- Added section IDs `25 Economy` and `26 PhysicsConstants`.
- Updated critical sizes: `H8ItemRecord=80`, `H8EconomyRecord=64`, `H8PhysicsConstantsRecord=64`, `H8DataMonolithTelemetryEntry=64`, `H8StaticLocalizationReference=16`.
- Replaced "packed" wording with explicit-layout/source-owned-size wording.
- Rechecked targeted static-data mock/parser routes. Production boot still goes through `H8StaticDataArena.TryInitializeFromStreamingAssets`; non-editor failure escalates to `FatalArchitectureException`. Editor missing-file tolerance is the CI/import fallback, not a runtime emergency monolith.

Cinematic cheats used:
- Static text is still a UTF-8 pool plus offsets, not runtime string object hydration.
- Broken item/economy references still die in the editor baker, not through runtime repair logic.

Exact microseconds saved:
- Runtime: 0 us/frame.
- Future integration: prevents a predictable item-section stride corruption (`80` bytes source vs stale `64` bytes doc) and avoids a wasted compile/import/bake loop.

Verification:
- Fixed-string doc scan no longer finds the stale `H8ItemRecord | 64`, stale header-world/app-version wording, or "explicitly packed" phrase in the Data Monolith spec.
- `git diff --check` over SHINOBU_103 files reports only CRLF normalization warnings.
- `static_data.h8bin` is still absent; bake remains blocked until guarded compile/import is allowed.
- Latest guard sampled `75.91, 75.41, 19.24, 60.46, 54.98, 93.55%` CPU with active `csc` PID `69316` and `dotnet` PID `69060`; no `dotnet build` was launched.

## 2026-05-19 UTF-8 Slice Metadata Addendum

What was wrong:
- Some static-data text fields stored only an unsigned UTF-8 offset and depended on null-terminated scanning.
- Task 09 requires the binary contract to expose offset plus byte length so readers can use bounded spans.

What was done:
- Added byte-length fields to existing reserved slots in `H8CreatureTraitRecord`, `H8BiomeRecord`, `H8AudioClipRegistryRecord`, `H8GhostModuleRecord`, and `H8SopErrorRecord`.
- Updated CSV and JSON baker paths to emit those lengths.
- Updated static localization alias extraction and audio Addressables key decoding to use bounded offset+length reads.
- Updated `DATA_MONOLITH_H8BIN_SPEC.md` with the UTF-8 slice contract and the `uint.MaxValue`/`0` missing sentinel.

Cinematic cheats used:
- Text remains a single UTF-8 pool plus scalar offsets and lengths. No runtime managed string table, dictionary hydration, or parser fallback was introduced.

Exact microseconds saved:
- Runtime hot path: 0 us/frame.
- Cold text alias/key lookup: avoids one null-terminator scan per length-bearing text slice; estimated 1-40 us across a small static batch, larger for localization-heavy bakes.

Verification:
- Static scan no longer finds direct `localizationPool.Add(...)` assignments for the length-bearing fields.
- Fixed record sizes are unchanged because only reserved 4-byte slots were reused.
- Compile remains pending under CPU/process guard. Latest samples were `13.51, 23.91, 74.3, 35.15, 100, 100, 100, 100%`; no active `dotnet`/`csc`, but the >50% samples still block build.
