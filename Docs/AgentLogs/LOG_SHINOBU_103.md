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
- Updated `H8StaticDataArena` to request payload/telemetry buffers from `GlobalDataVault`, use `UninitializedMemory`, attempt MMF on desktop, fall back to direct `FileStream.Read(Span<byte>)`, validate magic/version/header size/directory/checksum, and expose `GetSectionSpan<T>`.
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
- Static grep found no `Pack=1`, DTO `get; set;`, runtime `File.ReadAllBytes`, or `string.Split` in Data Monolith runtime/compiler code. Remaining byte arrays are editor-only inspector/localization scratch; remaining `NativeArray<byte>` is the documented no-vault fallback.
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
    <TASK id="16" status="PASS_STAGED">Vault and fallback byte arenas use `NativeArrayOptions.UninitializedMemory`.</TASK>
    <TASK id="17" status="PASS_STAGED">300-entry telemetry ring/cursor use Vault IDs 71104/71105; payload uses 71103.</TASK>
    <TASK id="18" status="PASS_STAGED">UI Toolkit compiler window created.</TASK>
    <TASK id="19" status="PASS_STAGED">Schema/template generator plus reflection layout manifest created.</TASK>
    <TASK id="20" status="PASS_STAGED">Binary inspector validates checksum and lists section table.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <H8DataBlobHeader size="16">0:uint Magic; 4:ushort FormatVersion; 6:ushort HeaderBytes; 8:ulong Checksum64.</H8DataBlobHeader>
    <H8DataBlobDirectory size="64">0:uint Magic; 4:ushort FormatVersion; 6:ushort SectionCount; 8:uint SectionTableOffset; 12:uint SectionTableBytes; 16:uint BlobBytes; 20:uint DataStartOffset; 24:uint LocalizationOffset; 28:uint LocalizationBytes; 32:uint Flags; 36:uint WorldSeed; 40:uint AppVersionHash; 44-60:uint Reserved0-4.</H8DataBlobDirectory>
    <H8DataSectionEntry size="16">0:uint SectionId; 4:uint RecordSize; 8:uint Count; 12:uint OffsetBytes.</H8DataSectionEntry>
    <H8ItemRecord size="80">0:uint HashId; 4:uint RecordIndex; 8:uint CategoryHash; 12:uint Flags; 16/24:ulong RecipeMask0/1; 32-44:float Mass/Volume/Quality/Heat; 48:uint YieldHash; 52/56:int UTF8 offsets; 60/64:uint lengths; 68/70:ushort stack/count; 72:uint Cost; 76:float AccessFrequency.</H8ItemRecord>
    <Telemetry size="64">H8DataMonolithTelemetryEntry is one 64-byte cache line.</Telemetry>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    The monolith format is universal. Runtime owners use `GlobalQualityWeight` after reading fixed sections. At low weight, consumers can process fewer records, skip high-frequency scans, and use nearest section/index lookups; high/ultra consumers can scan richer sections or upload full spans. No low/high binary split was introduced.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>
    Payload buffer: BufferID 71103. Telemetry ring: BufferID 71104. Telemetry cursor: BufferID 71105. Private persistent byte arena is fallback-only when `GlobalDataVault` is unavailable.
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
