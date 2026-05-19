# LOG_SHINOBU_50

## 2026-05-18 - Babel Alignment And Lore Retrieval Repair

What was wrong:
- `Data/Balance/Baked/Babel_Dictionary.h8bin` was the known 1295-byte anomaly. It was not divisible by 16 and its header/CRC described the bad length.
- Runtime Babel lookup still depended on an extra native lookup/index copy instead of treating the `.h8bin` index/blob as the flat source of truth.
- The initial Babel pass lacked explicit Burst endianness validation, XOR lore decryption, quality-weight throttling, SignalBus voice-link emission, and exact Burst compile flags.
- Human inspection existed only as raw binary evidence; designers had no focused Babel diagnostics window showing padding and decryption behavior.

What was done:
- Repaired the balance Babel payload to 1296 bytes with header `FileByteLength=1296`, payload CRC `0x199CAC7A`, and SHA-256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- Updated `Data/Balance/Baked/H8StaticData.bin` and both manifests so the static header and manifest CRC match the repaired Babel payload.
- Reworked `H8DataBaker.WriteBabelDictionary()` to write `BabelIndexDTO` rows and align final file length through `AlignUp16`.
- Added strict 16-byte `BabelIndexDTO`, 16-byte `BabelLookupResultDTO`, `MockTextRequestSignal`, `MockUIBuffer`, `MockSpanConverter`, and `PlayVoiceOverSignal`.
- Reworked `BabelDictionaryStore` to use MMF for aligned files, padded raw memory fallback for legacy misaligned files, pointer binary search over the mapped index, unmanaged `ERROR` fallback span, 300-frame telemetry, and slow-lookup dump to `Docs/AgentLogs/Dump_BABEL_FIXER.bin`.
- Added exact Burst jobs: `BabelBinarySearchKernel`, `BabelEndiannessValidationJob`, `BabelLoreXorDecryptJob`, and `MockSpanCountJob`.
- Added continuous `GlobalQualityWeight` request-budget math via `BabelLookupScalability.ResolveFrameLookupBudget`.
- Added `GetUtf8(hash, linkedAudioHashes)` to push `PlayVoiceOverSignal` through `SignalBus` without audio-domain references.
- Added `Babel Dictionary Diagnostics` editor facade with search, alignment/padding display, `loc_overrides.csv` polling, aligned save, and XOR decryption preview.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the repaired Babel payload state.

Cinematic cheats used:
- The Dear Lie: no string-management framework. Text truth is only `(hash -> byte offset, byte length)` into unmanaged UTF-8.
- Crypto visual fake: lore clearance is a single XOR mask pass over bytes, not a narrative object graph or dynamic string model.
- MMF paging: future large dictionaries can let the OS page text data instead of eagerly copying the full blob.

Exact microseconds saved:
- Measured Unity profiler delta: `0 us/frame` evidence, because no Unity profiler/play-mode run was executed in this pass.
- Static hot-path estimate: replacing hash-map hydration/string lookup with pointer binary search saves approximately `2-8 us` per 500-entry UI burst on i3/MX350-class CPUs; current 26-entry balance payload is below stable profiler granularity.
- Exact steady-state padding cost: `0 us/frame`; the one-byte repair is cold-load/header work only.
- Exact GC saved on runtime lookup: `0 B/frame` by construction; lookup returns `ReadOnlySpan<byte>` and never allocates strings.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false` passed with 0 errors.
- `python Tools\VerifyBabelDictionary.py` passed: 45 sources, 32672 entries, 17 languages, 1534512 bytes, alignment 16.
- Direct balance payload check passed: 1296 bytes, CRC `0x199CAC7A`, 26 entries, 0 bad slices.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50.json` still reports global `BINARY_HYGIENE_FAILED`; remaining failures are Bakery editor binaries and archived dumps, not the balance Babel payload.

<SELF_AUDIT agent_id="SHINOBU_50">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Legacy 1295-byte anomaly repaired and padded reader retained.</TASK>
    <TASK id="02" status="PASS">No `Dictionary&lt;uint,string&gt;` in touched Babel files; runtime uses flat index/blob.</TASK>
    <TASK id="03" status="PASS">`BabelIndexDTO` has fields only; no properties.</TASK>
    <TASK id="04" status="PASS">`BabelIndexDTO` is exactly 16 bytes.</TASK>
    <TASK id="05" status="PASS">Blind mocks and span count job exist without Terminal/UI refs.</TASK>
    <TASK id="06" status="PASS">Burst binary search kernel exists with exact flags.</TASK>
    <TASK id="07" status="PASS">Endianness job detects reversed magic and applies `math.reversebytes()` to index fields.</TASK>
    <TASK id="08" status="PASS">Raw UTF-8 template bytes returned; no token formatting.</TASK>
    <TASK id="09" status="PASS">Missing hashes return unmanaged `ERROR` span.</TASK>
    <TASK id="10" status="PASS">XOR lore decryption job exists.</TASK>
    <TASK id="11" status="PASS">`GlobalQualityWeight` controls per-frame lookup budget.</TASK>
    <TASK id="12" status="PASS">Existing locale swap path is background read plus POST_SIMULATION commit; no duplicate owner added.</TASK>
    <TASK id="13" status="PASS">No AUP/double3 query data in Babel DTOs/jobs.</TASK>
    <TASK id="14" status="PASS">MMF path kept for aligned files; padded raw fallback for legacy files.</TASK>
    <TASK id="15" status="PASS">Voice links emit `PlayVoiceOverSignal` through `SignalBus`.</TASK>
    <TASK id="16" status="PASS">No full zero-init blob copy; fallback clears only padding.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry and slow lookup dump path exist.</TASK>
    <TASK id="18" status="PASS">Editor diagnostics window exists with search, CRC, alignment, padding.</TASK>
    <TASK id="19" status="PARTIAL">Editor CSV monitor and aligned save exist; live runtime Vault mutation is not claimed because no Babel blob/index BufferID ownership exists.</TASK>
    <TASK id="20" status="PASS">Editor XOR mask preview exists.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    `BabelIndexDTO`: offset 0 `uint StringHash` 4b; offset 4 `uint ByteOffset` 4b; offset 8 `uint ByteLength` 4b; offset 12 `uint _pad0` 4b; total 16b.
    `BabelLookupResultDTO`: offset 0 `uint TextHash`; offset 4 `uint ByteOffset`; offset 8 `uint ByteLength`; offset 12 `uint Flags`; total 16b.
    `H8StaticDataTelemetryEntry`: total 64b, black-box ring row sized to one cache line.
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    `GlobalQualityWeight` is saturated and smoothed. At weak weights the lookup budget collapses to 20 requests/frame; at 1.0 it drains the requested count. No binary low/high switch.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime store no longer owns a private persistent index NativeArray. Persistent black-box buffers requested: `BufferID.StaticDataTelemetryRing`, `BufferID.StaticDataTelemetryCursor`.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    Jobs consume caller-provided `JobHandle` dependencies and output their scheduled handle. No arbitrary main-thread `Complete()` was added. Job fields use `NoAlias`; pointer index uses `NativeDisableUnsafePtrRestriction`.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Babel lane added no direct sibling runtime assembly dependency. Voice/audio integration is signal-only.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: potential managed string/hash-map lookup and formatting framework, O(lookup + allocation + format). After: O(log N) binary search plus raw byte span; formatting/decryption are caller-owned jobs.
  </DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 Bottom Anchor Loop15 CPU-Locked Static Closure - 2026-05-19

What was wrong:
- A fresh compile is required after Loop14, but the machine remained at 100% CPU while no compiler workers were active.
- Launching `dotnet build` under that load would violate the AGENTS.md hardware guard.

What was done:
- Refused to run `dotnet build` under 100% CPU.
- Verified project-surface includes: `LocRegistry.cs`, `GlobalRegistryContracts.cs`, `LocalizationManager.cs`, `BabelLocalizationContract.cs`, `H8StaticDataContracts.cs`, and `BabelDictionaryStore.cs` each appear exactly once in `Hecton8.Core.csproj`.
- Verified `IBabelLocalization` is defined only in `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs`.
- Verified SHINOBU hot files still have no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no local native allocation signatures, and no weak Burst flags.
- Re-probed `Data/Balance/Baked/Babel_Dictionary.h8bin`.

Cinematic Cheats used:
- No new simulation was added. Babel remains a byte-span facade: hash lookup over flat 16-byte rows, raw UTF-8 span return, optional XOR byte math.

Exact Microseconds saved:
- Runtime: `0 us/frame`, `0 B/frame`.
- Iteration: avoids a false compiler-green claim while the build is legally blocked by machine load.

Verification:
- Babel verifier remained PASS in Loop14: 32672 records, 1534512 bytes, alignment 16.
- Direct balance probe remained PASS: 1296 bytes, mod16 0, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- Compile recheck: STILL DEFERRED, CPU samples `100`, `100`, compiler worker count 0.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP15_CPU_LOCKED_STATIC_CLOSURE">
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS by static/verifier evidence. Build proof remains pending because the hardware guard forbids launching `dotnet build` at 100% CPU.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`BabelIndexDTO`: offset 0 hash u32, 4 offset u32, 8 length u32, 12 pad u32, total 16. `BabelTelemetryEntry`: 64 bytes. Payload: 1296 bytes, mod16 0.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Weak quality keeps lookup work near the 20-request floor; high/ultra drains full queues. No binary low/high switch was added.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Handles remain BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Babel lookup/XOR/mock jobs retain `[NoAlias]`; no new runtime job graph was introduced in Loop15.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Contract extraction stands: `IBabelLocalization` is contract-only in `Hecton8.Core.Contracts`; no direct Babel sibling Runtime reference was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Text remains raw UTF-8 bytes addressed by hash, not a managed string table.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_50 Loop14 Contract Extraction Report - 2026-05-19

What was wrong:
- The Babel runtime sidecar existed, but the `IBabelLocalization` interface still lived in `GlobalRegistryContracts.cs`.
- That header is not contract-clean: it imports multiple sibling runtime namespaces and still contains legacy `Pack=1` DTOs owned by other domains.
- Keeping Babel mocks tied to that header preserved compile-wall pressure even though the hot Babel lookup path was already allocation-free.

What was done:
- Added `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs` under the contract-only namespace `Hecton8.Core.Contracts`.
- Removed the old `IBabelLocalization` declaration from `GlobalRegistryContracts.cs`.
- Added `using Hecton8.Core.Contracts;` to `LocalizationManager.cs` so the concrete manager implements the contract-only interface.
- Added the new contract source file to `Hecton8.Core.csproj`.
- Left the full `LocalizationManager` monolith in place; moving it is a separate cross-domain migration, not a SHINOBU_50-safe edit.

Cinematic Cheats used:
- The Dear Lie is unchanged: Babel serves raw UTF-8 byte spans by hash. The UI still believes it has text; the dictionary lane only owns offsets, lengths, and optional XOR byte math.

Exact Microseconds saved:
- Runtime: `0 us/frame`, `0 B/frame`.
- Compile-wall gain: Babel-only mocks can now reference the contract-only interface instead of the heavy registry-contract header. Runtime lookup cost remains O(log N) over 16-byte rows.

Verification:
- `rg` contract routing: PASS, only `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs` defines `public interface IBabelLocalization`.
- `python Tools\VerifyBabelDictionary.py`: PASS, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- `python Tools\VerifyBabel.py`: PASS, records 32672, sources 45, bytes 1534512, alignment 16, endian little, collisions 0.
- Direct balance probe: PASS, `Data/Balance/Baked/Babel_Dictionary.h8bin` length 1296, mod16 0, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- `git diff --check` on SHINOBU touched files: PASS with line-ending warnings only.
- Binary hygiene: GLOBAL FAIL remains in `Docs/AgentLogs/BinaryHygiene_SHINOBU_50_Loop14.json`; the 17 misaligned files are Bakery editor/plugin binaries or archived dump artifacts, not Babel product payloads.
- Compile recheck: DEFERRED by hardware guard. CPU samples stayed above 50% after the code change (`99.43`, `100`), and no `dotnet/csc/MSBuild/VBCSCompiler` process was active. No build was launched under load.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP14_CONTRACT_EXTRACTION">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">1295-byte anomaly remains repaired to 1296 bytes with mod16 == 0.</TASK>
    <TASK id="02" result="PASS">No `Dictionary&lt;uint,string&gt;` in SHINOBU hot files; runtime uses flat rows plus unmanaged UTF-8 bytes.</TASK>
    <TASK id="03" result="PASS">`BabelIndexDTO` uses raw fields, not hot-path properties.</TASK>
    <TASK id="04" result="PASS">`BabelIndexDTO` remains 16 bytes.</TASK>
    <TASK id="05" result="PASS">Mock request/buffer/span converter path remains blind to concrete UI/Terminal systems.</TASK>
    <TASK id="06" result="PASS">Burst binary search remains O(log N) with exact Burst flags.</TASK>
    <TASK id="07" result="PASS">Endian validation remains present.</TASK>
    <TASK id="08" result="PASS">Dynamic tokens remain raw bytes; no `string.Format` in Babel lookup.</TASK>
    <TASK id="09" result="PASS">Missing hash fallback remains unmanaged `ERROR` bytes.</TASK>
    <TASK id="10" result="PASS">Lore XOR decrypt job remains byte-wise and Burst-compatible.</TASK>
    <TASK id="11" result="PASS">Continuous `GlobalQualityWeight` lookup budget remains active.</TASK>
    <TASK id="12" result="PASS">Locale swap staging remains validated before commit.</TASK>
    <TASK id="13" result="PASS">No AUP data enters Babel DTO/query surface.</TASK>
    <TASK id="14" result="PASS">MMF/aligned fallback path remains active.</TASK>
    <TASK id="15" result="PASS">Voice link remains a typed signal, not direct Audio runtime coupling.</TASK>
    <TASK id="16" result="PASS">UTF-8 blob/index ownership remains Vault/MMF-based.</TASK>
    <TASK id="17" result="PASS">300-frame telemetry ring remains present.</TASK>
    <TASK id="18" result="PASS">Editor diagnostics facade remains present.</TASK>
    <TASK id="19" result="PASS">CSV override ingestor remains present.</TASK>
    <TASK id="20" result="PASS">Live XOR preview remains present.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`BabelIndexDTO`: offset 0 `uint StringHash`, offset 4 `uint ByteOffset`, offset 8 `uint ByteLength`, offset 12 `uint _pad0`, total 16 bytes. `BabelTelemetryEntry`: 64 bytes. Legacy payload math remains `(1295 + 15) &amp; ~15 = 1296`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, visible lookup batches collapse toward the 20-request floor through polynomial `math.lerp`/saturation. High/Ultra drains full request queues.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Zero private persistent array ownership for Babel truth. Handles: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Lookup/prefetch/XOR/mock jobs retain `[NoAlias]` and return `JobHandle`s. Loop14 only moved the interface contract; it added no new job graph.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>`IBabelLocalization` is now in `Hecton8.Core.Contracts`; Babel mocks no longer need the heavy `GlobalRegistryContracts.cs` interface definition. No direct Babel sibling Runtime reference was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: Babel interface attached to a concrete registry header. After: contract-only hash-to-byte-span interface; runtime text remains raw bytes and O(log N) lookup.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_50 Loop13 Build-Wall And Babel Alignment Report - 2026-05-18

What was wrong:
- The prior Loop12 note correctly deferred compile because the hardware guard blocked a build, but that state became stale once the CPU/compiler window cleared.
- Fresh serial compilation then exposed moving project-surface drift outside the Babel lane: missing/generated compile includes, foreign DTO call-shape drift, duplicate AssetLifecycle helper methods, and an addressable byte-estimator race in World.
- The Babel payload itself was not the failing component: product Babel binaries remained 16-byte aligned, and the 1295-byte legacy balance file remained repaired to 1296 bytes.

What was done:
- Removed the duplicate native-handle helper block from `AssetLifecycleGovernor` and kept a single helper implementation.
- Kept the existing World-owned `EstimateAddressableChunkBytes` method as the only byte-estimator implementation after the concurrent race created a duplicate.
- Preserved the hard Babel `GlobalRegistry` interface sidecar and did not introduce any direct sibling Runtime dependency from the Babel lane.
- Re-ran Core, Editor, PlayModeTests, Babel verifier, direct balance payload probe, hot-file forbidden scan, and binary hygiene.

Cinematic Cheats used:
- Babel remains the Dear Lie: the runtime never owns C# strings. It serves `(hash -> byte offset, byte length)` into unmanaged UTF-8 and lets UI/audio lanes render or signal from bytes.
- Dynamic tokens remain raw template bytes; no `string.Format` path was added to SHINOBU hot lookup.

Exact Microseconds saved:
- Babel runtime delta: `0 us/frame`, `0 B/frame` added by the compile-wall stitches.
- Lookup architecture retained: O(log N) contiguous 16-byte row search instead of managed dictionary/string hydration; estimated 2-8 us saved per 500-entry UI burst on i3/MX350-class silicon, unprofiled.
- Iteration-time recovery: current Core/Editor/PlayModeTests missing-symbol wall is removed.

Verification:
- Core build: PASS, 0 errors, 8 warnings.
- Editor build: PASS, 0 errors, 1 warning.
- PlayModeTests build: PASS, 0 errors, 0 warnings.
- `python Tools\VerifyBabelDictionary.py`: PASS, 45 sources, 32672 entries, 17 languages, 1534512 bytes, alignment 16.
- Direct balance probe: PASS, `Data/Balance/Baked/Babel_Dictionary.h8bin` length 1296, mod16 0, header 32, entries 26, index offset 32, data offset 448, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- Hot-file forbidden scan: PASS, no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no weak Burst flags, no raw H8Memory allocation/free, no `string.Format`, no `FindObjectOfType`, no `GameObject.Find` in SHINOBU hot files.
- Binary hygiene: GLOBAL FAIL remains in `Docs/AgentLogs/BinaryHygiene_SHINOBU_50_Loop13.json`; 17 misaligned files are Bakery editor binaries or archived dump artifacts, not Babel product payloads.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP13_BUILD_WALL_RECHECK">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">1295-byte anomaly repaired and defended by 16-byte padding. Balance payload is 1296 bytes and mod16 == 0.</TASK>
    <TASK id="02" result="PASS">No `Dictionary&lt;uint,string&gt;` in SHINOBU hot files. Runtime truth is flat index rows plus unmanaged UTF-8 bytes.</TASK>
    <TASK id="03" result="PASS">`BabelIndexDTO` uses public fields, not hot-path properties/private setters.</TASK>
    <TASK id="04" result="PASS">`BabelIndexDTO` layout is 16 bytes: u32 hash, u32 byte offset, u32 byte length, u32 pad.</TASK>
    <TASK id="05" result="PASS">Mock request/buffer/span converter path exists without Terminal/UI concrete dependencies.</TASK>
    <TASK id="06" result="PASS">Burst binary search operates O(log N) over sorted 16-byte rows with exact Burst flags.</TASK>
    <TASK id="07" result="PASS">Endian validation path detects reversed magic and reverses u32 index fields defensively.</TASK>
    <TASK id="08" result="PASS">Dynamic tokens are returned as raw template bytes; formatting remains outside Babel.</TASK>
    <TASK id="09" result="PASS">Missing hashes return Vault-backed unmanaged `ERROR` bytes instead of null/exception.</TASK>
    <TASK id="10" result="PASS">Lore XOR decryption job operates over unmanaged bytes and mask data.</TASK>
    <TASK id="11" result="PASS">`GlobalQualityWeight` controls a continuous polynomial lookup budget; weak devices cap visible work to 20 requests.</TASK>
    <TASK id="12" result="PASS">Locale swap is staged/validated before pointer commit; no main-thread raw locale freeze is introduced by SHINOBU.</TASK>
    <TASK id="13" result="PASS">No AUP/double3 data is carried through Babel DTOs or lookup queries.</TASK>
    <TASK id="14" result="PASS">Aligned MMF path exists for dictionary mapping; misaligned legacy files use aligned fallback memory.</TASK>
    <TASK id="15" result="PASS">Voice-over linkage is a `PlayVoiceOverSignal`, not a direct Audio runtime call.</TASK>
    <TASK id="16" result="PASS">Babel blob/index ownership routes through Vault/MMF; no repeated UTF-8 blob allocation loop exists.</TASK>
    <TASK id="17" result="PASS">300-frame telemetry ring records lookup/missing/time/padding data and dumps on suspicious lookup cost.</TASK>
    <TASK id="18" result="PASS">Editor diagnostics facade exposes searchable entries, CRC/alignment, and padding visibility.</TASK>
    <TASK id="19" result="PASS">CSV override ingestor parses project-root overrides into Vault scratch and mutates/appends UTF-8 slices.</TASK>
    <TASK id="20" result="PASS">Editor XOR mask preview exists for live decrypted lore inspection.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    `BabelIndexDTO`: offset 0 `uint StringHash` size 4; offset 4 `uint ByteOffset` size 4; offset 8 `uint ByteLength` size 4; offset 12 `uint _pad0` size 4; total 16 bytes, 16-byte aligned stride.
    `BabelLookupResultDTO`: 16 bytes for fixed result transfer.
    `BabelTelemetryEntry`: explicit 64-byte row to avoid false sharing when telemetry writes become parallel.
    Balance payload math: legacy 1295 -> `(1295 + 15) &amp; ~15 = 1296`; 1296 % 16 = 0.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, visible lookup batches collapse toward the 20-request floor through polynomial `math.lerp`/saturation; text hydration spreads across frames instead of spiking. Above the ramp, high/ultra hardware drains full request queues and can spend CPU on decrypted preview/diagnostics. There is no binary low-end switch in the SHINOBU lookup budget.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    SHINOBU hot ownership declares zero private persistent array ownership for Babel truth. Handles used: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Consumes caller-visible dependency handles, schedules lookup/prefetch/XOR/mock jobs, and returns the resulting `JobHandle` for upstream combination. Independent job arrays are marked `[NoAlias]`; search reads mapped/padded index truth by pointer rather than copying into a local persistent table.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Babel has no direct assembly/runtime reference to sibling gameplay domains. Integration goes through `GlobalRegistry` `IBabelLocalization`, `GlobalDataVault`, and typed `SignalBus` payloads. Loop13 non-Babel stitches aligned generated project surfaces with existing source files only.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: managed text framework tendency, dictionary hydration, string formatting, and concrete lane calls. Complexity under UI bursts was O(N managed hydration + allocation + formatting). After: O(log N) binary search per requested hash over 16-byte rows, raw span return, and optional byte-wise XOR. Heavy rendering and token expansion stay in presentation lanes.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_50 Compile-Wall Linkage Recheck - 2026-05-18

What was wrong:
- Fresh `Hecton8.Core.csproj` build initially failed outside Babel on generated project drift: `SignalWardenRuntime` preserved `WaterlineBreachSignal`, but the project file did not compile the existing waterline contract file.
- After that link was restored, `HectonNetworkManager` exposed the same issue for existing rollback netcode contracts/runtime and memory sentinel contracts.
- `HectonRollbackNetcodeRuntime.cs` used `IJob.Run()` but lacked the `Unity.Jobs` import.

What was done:
- Added existing contract/runtime compile includes to `Hecton8.Core.csproj`: `ShinobuOceanSurfaceAtmosphereContracts.cs`, `HectonRollbackNetcodeRuntime.cs`, `RollbackNetcodeContracts.cs`, `MemorySentinelSignals.cs`, and `MemorySentinelContracts.cs`.
- Added `using Unity.Jobs;` to `HectonRollbackNetcodeRuntime.cs`.
- Preserved the Babel sidecar registry path: `GlobalRegistry.RegisterBabelLocalizationRuntime(IBabelLocalization)` remains an interface-only slot; no concrete UI/audio/runtime dependency was introduced.

Cinematic Cheats used:
- Babel Dear Lie remains intact: the game still receives byte offsets and byte lengths into unmanaged UTF-8, not managed strings.
- Waterline/rollback fixes were linkage-only; no simulation was added to Babel and no new physics/render truth was created.

Exact Microseconds saved:
- Babel lookup hot path remains `0 B/frame`; measured Unity profiler delta not claimed.
- `BabelIndexDTO` lookup remains O(log N) over contiguous 16-byte rows; static estimate remains 2-8 us saved per 500-entry UI burst versus managed/hash-map hydration on i3/MX350-class CPUs.
- Compile-wall recovery is iteration-time only; 0 us/frame runtime gain.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 8 warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 1 warning.
- `dotnet build Hecton8.PlayModeTests.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `python Tools\VerifyBabelDictionary.py` -> PASS, 32672 entries, 1534512 bytes, alignment 16.
- `python Tools\VerifyBabel.py` -> PASS, 32672 records, alignment 16, little endian.
- Direct balance payload probe -> PASS: `Data/Balance/Baked/Babel_Dictionary.h8bin` is 1296 bytes, aligned16, CRC `0x199CAC7A`, 26 entries, 0 bad slices.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50_Latest.json` -> GLOBAL FAIL remains; 17 failures are Bakery editor binaries and archived dumps, not Babel product payloads.
- SHINOBU hot-file forbidden scan -> PASS: no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no weak Burst flags, no `string.Format`, no `FindObjectOfType`, no `GameObject.Find`.
- `git diff --check` on SHINOBU touched files -> PASS with line-ending warnings only.

<SELF_AUDIT agent_id="SHINOBU_50" pass="COMPILE_WALL_LINKAGE_RECHECK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">1295-byte anomaly resolved; balance Babel payload is 1296 bytes and 16-byte aligned.</TASK>
    <TASK id="02" status="PASS">No `Dictionary&lt;uint,string&gt;`; Babel data is flat index rows plus unmanaged UTF-8 blob.</TASK>
    <TASK id="03" status="PASS">Hot DTOs expose fields; no properties on `BabelIndexDTO`.</TASK>
    <TASK id="04" status="PASS">`BabelIndexDTO` uses uint hash/offset/length/pad, total 16 bytes.</TASK>
    <TASK id="05" status="PASS">Mock text request, UI buffer, span converter, and count job prove lookup without Terminal/UI concrete refs.</TASK>
    <TASK id="06" status="PASS">Binary search kernels use exact Burst flags and `NoAlias`.</TASK>
    <TASK id="07" status="PASS">Endianness validation handles reversed magic and `math.reversebytes()`.</TASK>
    <TASK id="08" status="PASS">Dynamic tokens remain raw bytes; Babel does not format `^0` templates.</TASK>
    <TASK id="09" status="PASS">Missing hash fallback returns Vault-backed unmanaged `ERROR` bytes.</TASK>
    <TASK id="10" status="PASS">Lore decryption is XOR byte math over caller/Vault buffers.</TASK>
    <TASK id="11" status="PASS">`GlobalQualityWeight` drives continuous lookup budget with `math.lerp`/smooth polynomial ramp.</TASK>
    <TASK id="12" status="PASS">Async locale stage pads source bytes and commits validated data in post-simulation.</TASK>
    <TASK id="13" status="PASS">No AUP or `double3` enters Babel lookup DTOs.</TASK>
    <TASK id="14" status="PASS">Aligned files use MMF path; misaligned legacy data is copied to padded Vault bytes.</TASK>
    <TASK id="15" status="PASS">Narrative audio link emits typed `PlayVoiceOverSignal`; no direct Audio reference.</TASK>
    <TASK id="16" status="PASS">Babel buffers resolve through DataVault handles; no private raw fallback ownership remains in SHINOBU path.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring exists and 64-byte telemetry rows prevent false-sharing class drift.</TASK>
    <TASK id="18" status="PASS">Babel Dictionary Diagnostics editor facade exists.</TASK>
    <TASK id="19" status="PASS">`loc_overrides.csv` parser mutates/appends active UTF-8 bytes without managed dictionary truth.</TASK>
    <TASK id="20" status="PASS">Editor XOR mask preview exists for live decryption inspection.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DTO name="BabelIndexDTO" size="16" alignment="16">offset 0 `uint StringHash`; offset 4 `uint ByteOffset`; offset 8 `uint ByteLength`; offset 12 `uint _pad0`; 4+4+4+4=16.</DTO>
    <DTO name="BabelLookupResultDTO" size="16" alignment="16">offset 0 `uint TextHash`; offset 4 `uint ByteOffset`; offset 8 `uint ByteLength`; offset 12 `uint Flags`; 4+4+4+4=16.</DTO>
    <DTO name="BabelTelemetryEntry" size="64" alignment="64">active telemetry fields occupy offsets 0..43; `_pad0.._pad4` fill offsets 44..63, one L1 cache line.</DTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below weak quality, lookup batches collapse to max 20 requests per frame. Above the threshold, a saturated cubic ramp feeds `math.lerp(lowBudget, requested, smooth)`, so 0.1 thermal mode avoids encyclopedia spikes and 1.0 desktop drains full batches.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault handles: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Search/XOR/mock jobs mark independent buffers with `[NoAlias]`. Visible prefetch consumes a caller dependency and returns a `JobHandle`; SHINOBU lookup code does not hide arbitrary `Complete()` calls in the hot path.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Babel added no direct sibling runtime dependency; external compile-wall stitches only added existing source files to generated project linkage and did not duplicate mock contracts.</COMPILE_GUARD>
  <DEAR_LIE>Before: managed string/dictionary mental model with formatting pressure. After: O(log N) binary search over 16-byte rows and raw unmanaged UTF-8 spans; rendering agents own token expansion.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 Compile-Wall Recheck - 2026-05-18

What was wrong:
- Fresh Core verification no longer matched the earlier log. The active worktree had a new external compile wall in `SaveSystem/H8BinaryWorldPager.cs` and `SaveSystem/SaveDeltaCompression.cs`.
- The first failed pass reported 46 SaveSystem errors around missing pager queue/result state plus two AUP variable typos. This was outside Babel, but it blocked proof that the Babel lane compiles.
- Previous `dotnet build` invocations left orphan `dotnet/csc` workers after returning.

What was done:
- Kept Babel code unchanged during this stitch.
- Applied a minimal SaveSystem compile-wall stitch: fixed the `sectorOrigin`/`sectorOriginMeters` symbol swaps and preserved the pager's Vault-backed command/result buffers after concurrent SaveSystem edits settled.
- Rechecked Babel payload integrity and compiler state after killing stale `dotnet/csc` workers.

Cinematic Cheats used:
- Babel Dear Lie unchanged: raw UTF-8 spans over aligned binary rows, no managed string table.
- SaveSystem stitch was not a simulation or feature pass; no new runtime visual behavior was introduced.

Exact Microseconds saved:
- Babel runtime: `0 us/frame` changed in this stitch.
- Compile iteration: one active 46-error wall removed from the Core target.
- Lookup estimate remains `2-8 us` saved per 500-entry UI burst versus managed dictionary/string hydration on i3/MX350-class CPUs.

Verification:
- `python Tools\VerifyBabelDictionary.py` -> PASS, 32672 entries, 1534512 bytes, alignment 16.
- Direct balance payload probe -> PASS: 1296 bytes, mod16=0, header length 1296, CRC `0x199CAC7A`, 26 entries, 0 bad slices, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 9 warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `dotnet build Hecton8.PlayModeTests.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50.json` -> GLOBAL FAIL remains: 17 misaligned files are non-Babel Bakery editor fixtures and archived dumps.
- SHINOBU hot-file forbidden scan -> PASS: no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint>`, no `Pack = 1`, no weak Burst precision, no `H8Memory.AllocateRaw`, no `H8Memory.FreeRaw`, no `string.Format`, no scene search calls.

<SELF_AUDIT agent_id="SHINOBU_50" pass="COMPILE_WALL_RECHECK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Babel payload is 1296 bytes; padding math is `(1295 + 15) &amp; ~15 = 1296`.</TASK>
    <TASK id="02" status="PASS">Babel hot files contain no `Dictionary&lt;uint,string&gt;`; lookup remains flat arrays/pointers.</TASK>
    <TASK id="03" status="PASS">`BabelIndexDTO` uses public fields, no hot properties.</TASK>
    <TASK id="04" status="PASS">`BabelIndexDTO` is 16 bytes: offsets 0/4/8/12.</TASK>
    <TASK id="05" status="PASS">Mock request/span contracts remain dependency-free.</TASK>
    <TASK id="06" status="PASS">Burst search kernels compile with exact flags and `NoAlias`.</TASK>
    <TASK id="07" status="PASS">Endian guard remains present.</TASK>
    <TASK id="08" status="PASS">Dynamic tokens are raw bytes, not formatted strings.</TASK>
    <TASK id="09" status="PASS">Missing hashes return unmanaged `ERROR` bytes.</TASK>
    <TASK id="10" status="PASS">Lore XOR job remains byte math.</TASK>
    <TASK id="11" status="PASS">Lookup budget uses `GlobalQualityWeight` via smooth polynomial lerp.</TASK>
    <TASK id="12" status="PASS">Locale staging validates padded bytes before swap.</TASK>
    <TASK id="13" status="PASS">No AUP data in Babel DTOs.</TASK>
    <TASK id="14" status="PASS">MMF path exists for aligned dictionaries; legacy misalignment uses padded Vault buffer.</TASK>
    <TASK id="15" status="PASS">Voice link remains SignalBus-only.</TASK>
    <TASK id="16" status="PASS">Babel persistent buffers route through Vault handles.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry exists with 64-byte row.</TASK>
    <TASK id="18" status="PASS">Editor diagnostics facade exists.</TASK>
    <TASK id="19" status="PASS">CSV override path patches active Vault bytes and supports longer append slices.</TASK>
    <TASK id="20" status="PASS">Editor XOR preview exists.</TASK>
  </TASK_RECONCILIATION>
  <COMPILE_GUARD>Core, Editor, and PlayModeTests compile after the external SaveSystem stitch. Babel added no sibling runtime concrete reference; GlobalRegistry exposes `IBabelLocalization` through a sidecar.</COMPILE_GUARD>
  <BINARY_HYGIENE>Product Babel payload is aligned. Global hygiene still fails on 17 non-Babel Bakery/archive binaries.</BINARY_HYGIENE>
</SELF_AUDIT>

# SHINOBU_50 Registry Sidecar Re-Audit - 2026-05-18

What was wrong:
- The Babel interface existed as `IBabelLocalization`, but `GlobalRegistry` only stored the concrete `LocalizationManager`.
- That meant isolated mock providers could not register the allocation-free Babel interface without dragging the concrete localization runtime surface.

What was done:
- Added a lightweight `_babelLocalizationRuntime` sidecar in `GlobalRegistry`.
- Added `RegisterBabelLocalizationRuntime(IBabelLocalization)` and `UnregisterBabelLocalizationRuntime(IBabelLocalization)`.
- `TryGet<IBabelLocalization>` now resolves the interface sidecar directly.
- `LocalizationManager` now registers the Babel interface sidecar on boot and unregisters it before the concrete runtime on destroy.

Cinematic cheat used:
- No new text manager, no broad domain migration. The game still believes it has localization services; Babel consumers get only byte-span interface truth.

Exact microseconds saved:
- Runtime frame path: 0 us/frame.
- Compile-wall/CI path: removes concrete `LocalizationManager` requirement for Babel-only mock providers; exact wall-clock compile saving requires CI timing.

Verification:
- `git diff --check` over SHINOBU touched files: passed with line-ending warnings only.
- `python Tools\VerifyBabelDictionary.py`: passed, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- `python Tools\VerifyBabel.py`: passed, records=32672, sources=45, bytes=1534512, alignment=16, endian=little, hashCollisions=0.
- Direct balance payload probe: `Data/Balance/Baked/Babel_Dictionary.h8bin` is 1296 bytes, aligned16, CRC `0x199CAC7A`, 26 entries, bad_slices=0.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50_Loop09.json`: global fail remains; 17 failures are Bakery editor fixtures plus archived dumps, not Babel.
- Build recheck deferred: external `dotnet`/Unity compiler process active and CPU load 90-100%; AGENTS.md forbids launching another build under that condition.

<SELF_AUDIT agent_id="SHINOBU_50" pass="REGISTRY_SIDECAR_REAUDIT">
  <TASKS>01-20 remain PASS from prior reconciliation; Loop 09 adds interface registry hardening without changing binary lookup behavior.</TASKS>
  <STRUCT_LAYOUT>BabelIndexDTO offset0 u32 StringHash, offset4 u32 ByteOffset, offset8 u32 ByteLength, offset12 u32 _pad0; total 16 bytes. BabelTelemetryEntry total 64 bytes.</STRUCT_LAYOUT>
  <SCALABILITY>GlobalQualityWeight still drives polynomial lookup budget through BabelLookupScalability; weak hardware caps large visible-text batches, high/ultra drains full batches.</SCALABILITY>
  <H_PHI_VAULT>Babel data buffers remain Vault/MMF owned: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT>
  <POINTER_ALIASING>Search/endian/XOR/mock jobs retain NoAlias. Registry sidecar adds no job and no pointer aliasing surface.</POINTER_ALIASING>
  <COMPILE_GUARD>New Babel provider path is `IBabelLocalization`; mock CI can bind through GlobalRegistry without concrete runtime-domain references.</COMPILE_GUARD>
  <DEAR_LIE>Babel remains byte offsets and unmanaged UTF-8 spans, not a managed string framework.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 CSV Append And Final Recheck - 2026-05-18

What was wrong:
- Task 19's first implementation was too narrow: it parsed `loc_overrides.csv`, but longer replacement strings were rejected and forced a rebake path.
- Status/Rationale still contained a stale external Core compile wall note after the codebase moved.
- The final audit needed explicit proof that hot Babel files did not carry weak Burst flags, `Pack=1`, managed string dictionaries, `Find*` calls, or direct runtime-domain calls.

What was done:
- Hardened `LocRegistry.TryApplyLocOverridesCsv` so CSV overrides are parsed into `BufferID.BabelOverrideCsvScratch` with a 1 MiB Vault scratch buffer.
- Added `BabelOverrideMutationGuardMask` around active blob/index mutation.
- Equal-or-shorter replacements still mutate in-place and clear the old tail.
- Longer replacements now append at `AlignUp16(_utf8ByteLength)`, clear alignment gaps/tail padding, update `_utf8ByteLength`, and update the active `LocalizationEntryDTO.ByteOffset` and `ByteLength`.
- Added a development/editor POST_SIMULATION poll in `LocalizationManager` at 0.5 s cadence. It uses the existing localization dispatcher owner and calls `LocRegistry`; no new runtime-domain owner was created.
- Updated telemetry to record CSV applied/rejected counts inside the 64-byte `BabelTelemetryEntry`.
- Re-ran Core, Editor, PlayModeTests builds and static binary checks.

Cinematic Cheats used:
- Dear Lie preserved: text remains a monolithic unmanaged UTF-8 byte blob plus 16-byte index rows. The game never owns C# strings for Babel lookup truth.
- CSV live patch is an authoring fake over the same Vault buffer, not a second runtime dictionary or localization object graph.
- Longer text edits buy designer iteration without rebaking, while runtime lookup cost remains unchanged.

Exact Microseconds saved:
- Measured Unity profiler delta: not claimed; no Unity profiler/GCMonitor was available in this CLI pass.
- Babel hot lookup GC: `0 B/frame` by static construction.
- CSV polling: `0 us/frame` in shipping path; development/editor poll is bounded to 0.5 s cadence and only touches File I/O when the timestamp changes.
- Static estimate remains `2-8 us` saved per 500-entry UI burst on i3/MX350-class CPUs versus managed/hash-map hydration.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `dotnet build Hecton8.PlayModeTests.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 0 warnings.
- `python Tools\VerifyBabelDictionary.py` -> PASS, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- Direct balance payload probe -> PASS: `Data/Balance/Baked/Babel_Dictionary.h8bin` bytes=1296, aligned16=True, header_len=1296, CRC `0x199CAC7A`, entries=26, bad_slices=0.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50.json` -> GLOBAL FAIL remains because of 15 Bakery editor binaries plus 2 archived dump files; balance Babel is aligned.
- SHINOBU hot-file forbidden scan -> PASS: no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no weak Burst flags, no `string.Format`, no `FindObjectOfType`, no `GameObject.Find` in `LocRegistry`, `BabelDictionaryStore`, `H8StaticDataContracts`, or `BabelLocalizationManagerWindow`.
- Legacy note: `LocalizationManager.FormatLocalized` retains a pre-existing editor/development `string.Format` branch. SHINOBU's added Babel hot path and CSV Vault path do not call it.

REGRESSION MODEL:
- CPU: lookup path unchanged after CSV append; CSV ingest is cold/dev authoring work O(file bytes + overrides * log N).
- GC: Babel lookup remains span-over-unmanaged bytes. Editor tools allocate by design; runtime CSV poll is dev/development-only and not part of shipping hot path.
- Memory: persistent Babel native buffers resolve through Vault handles: `BabelUtf8Blob`, `BabelIndexTable`, `BabelTelemetryRing`, `BabelStagedLocale`, `BabelDecryptionMask`, `BabelOverrideCsvScratch`, `BabelErrorUtf8`, and `BabelDictionaryMappedBytes`.
- Cadence: visible text prefetch returns a scheduled `JobHandle`; CSV monitoring polls every 0.5 s in POST_SIMULATION only.
- Correctness: current balance Babel payload is 16-byte aligned; DTO layout is 16/64-byte aligned; global hygiene failure is non-Babel.

<SELF_AUDIT agent_id="SHINOBU_50" pass="CSV_APPEND_FINAL_RECHECK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">1295-byte anomaly repaired: current balance file is 1296 bytes, and 1296 % 16 = 0.</TASK>
    <TASK id="02" status="PASS">No `Dictionary&lt;uint,string&gt;` in SHINOBU Babel hot files; lookup is flat index plus UTF-8 blob.</TASK>
    <TASK id="03" status="PASS">Hot DTOs expose fields, not properties/private setters.</TASK>
    <TASK id="04" status="PASS">`BabelIndexDTO` layout is exactly 16 bytes.</TASK>
    <TASK id="05" status="PASS">Mock signal/buffer/span proof exists without Terminal/UI concrete references.</TASK>
    <TASK id="06" status="PASS">Burst binary search jobs use exact flags and `[NoAlias]`.</TASK>
    <TASK id="07" status="PASS">Endianness validation handles reversed magic and `math.reversebytes()`.</TASK>
    <TASK id="08" status="PASS">Dear Lie tokens remain raw UTF-8 bytes; no formatting in Babel lookup.</TASK>
    <TASK id="09" status="PASS">Missing hash fallback uses Vault-backed unmanaged `ERROR` bytes.</TASK>
    <TASK id="10" status="PASS">Lore decryption is XOR byte math with a Vault/caller mask.</TASK>
    <TASK id="11" status="PASS">`GlobalQualityWeight` drives a continuous polynomial lookup budget.</TASK>
    <TASK id="12" status="PASS">Async locale swap stages padded bytes in Vault and commits during POST_SIMULATION.</TASK>
    <TASK id="13" status="PASS">No AUP/double3 values enter Babel query DTOs or jobs.</TASK>
    <TASK id="14" status="PASS">MMF path exists for aligned files; legacy misalignment is padded before runtime use.</TASK>
    <TASK id="15" status="PASS">Voice links emit `PlayVoiceOverSignal` through SignalBus only.</TASK>
    <TASK id="16" status="PASS">SHINOBU-owned persistent Babel native memory resolves through Vault handles.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry exists and Babel telemetry row is 64 bytes.</TASK>
    <TASK id="18" status="PASS">`Babel Dictionary Diagnostics` editor facade exists.</TASK>
    <TASK id="19" status="PASS">CSV override parser patches active Vault bytes and now supports longer replacements via append plus index update.</TASK>
    <TASK id="20" status="PASS">Editor XOR decryption preview exists.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DTO name="BabelIndexDTO" size="16">offset 0 StringHash uint size 4; offset 4 ByteOffset uint size 4; offset 8 ByteLength uint size 4; offset 12 _pad0 uint size 4; total 16, aligned to 16.</DTO>
    <DTO name="BabelLookupResultDTO" size="16">offset 0 TextHash uint size 4; offset 4 ByteOffset uint size 4; offset 8 ByteLength uint size 4; offset 12 Flags uint size 4; total 16, aligned to 16.</DTO>
    <DTO name="BabelTelemetryEntry" size="64">offsets 0..43 hold frame/hash/slice/counters/time/language/flags; offsets 44..51 hold CSV counters; offsets 52..63 are explicit uint padding; total 64, one L1 cache line.</DTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>At weak `GlobalQualityWeight`, lookup budget collapses to min(20, requested). The curve uses saturate + smoothstep polynomial + `math.lerp` through `BabelLookupScalability.ResolveFrameLookupBudget`; no binary low/high hardware switch remains.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Persistent Babel buffers requested at boot/runtime: `BabelUtf8Blob`, `BabelIndexTable`, `BabelTelemetryRing`, `BabelStagedLocale`, `BabelDecryptionMask`, `BabelOverrideCsvScratch`, `BabelErrorUtf8`, `BabelDictionaryMappedBytes`, `StaticDataTelemetryRing`, `StaticDataTelemetryCursor`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Visible prefetch consumes caller dependency and returns scheduled handle; reader fences are combined through `JobHandle.CombineDependencies`. Binary search, visible prefetch, mock request, RTL reverse, XOR, and span count jobs use `[NoAlias]` on separate arrays where applicable.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>SHINOBU hot files added no sibling runtime concrete dependency. `Hecton8.Core.asmdef` already has historical sibling references outside this task; SHINOBU did not mutate asmdefs. Audio integration is `SignalBus&lt;PlayVoiceOverSignal&gt;` only.</COMPILE_GUARD>
  <DEAR_LIE>Before: managed string/dictionary localization mindset with rebake pressure. After: O(log N) binary search over contiguous 16-byte rows and raw UTF-8 byte spans; CSV edits mutate Vault bytes without creating a second runtime truth.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 Final Recheck - 2026-05-18

What was wrong:
- The original 1295-byte Babel payload was already repaired on disk during this lane, but the project compile graph still had stale generated `.csproj` holes: `BabelDictionaryStore.cs` and `WristHologramHudRuntime.cs` were absent from the Core compile surface used by tests.
- Task 19 was too weak: editor CSV ingest existed, but live runtime blob mutation was not enforced.

What was done:
- Added the missing Core compile includes so `Hecton8.PlayModeTests.csproj` can resolve `BabelDictionaryStore` and the existing `WristHudQuadTransformDTO`.
- Added `LocRegistry.TryApplyLocOverridesCsv`: a Native/Vault scratch CSV parser for project-root `loc_overrides.csv` that binary-searches the active Babel index and patches the UTF-8 blob plus `ByteLength` in-place when the replacement is equal-or-shorter than the current slice.
- Wired the `Babel Dictionary Diagnostics` editor ingest button/poller to call the runtime override path when an active blob exists; longer override strings remain editor-save/rebake work to preserve immutable offsets.
- Reverified the repaired product payload: `Data/Balance/Baked/Babel_Dictionary.h8bin` is 1296 bytes, `mod16=0`, header length 1296, CRC `0x199CAC7A`, 26 entries, 0 bad slices.

Cinematic Cheats used:
- Dear Lie preserved: offsets and byte lengths are the source of truth; no runtime string framework.
- CSV live patch is a bounded in-place typo patch, not a dynamic allocator or slice shifter.
- XOR lore preview remains byte math, not narrative object construction.

Exact Microseconds saved:
- Runtime steady-state padding cost: `0 us/frame`.
- Runtime CSV polling cost: `0 us/frame` in player hot path; dev ingest only when called.
- Static estimate for 500-entry UI burst: `2-8 us` saved on i3/MX350-class CPU by avoiding managed/hash-map hydration and using contiguous 16-byte row binary search.
- Exact GC on lookup path: `0 B/frame` by construction; spans over unmanaged bytes.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 9 warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors, 1 warning.
- `dotnet build Hecton8.PlayModeTests.csproj --no-restore -m:1 /nr:false /p:BuildProjectReferences=false /p:UseSharedCompilation=false /p:RunAnalyzers=false -v:minimal /clp:ErrorsOnly` -> PASS, 0 errors.
- `python Tools\VerifyBabelDictionary.py` -> PASS, 32672 entries, 1534512 bytes, alignment 16.
- `python Tools\VerifyBabel.py` -> PASS, 32672 records, alignment 16, little endian.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50.json` -> GLOBAL FAIL remains; all remaining misaligned files are Bakery editor payloads or archived dump files, not `Data/Balance/Baked/Babel_Dictionary.h8bin`.

---

# SHINOBU_50 Ultra Polish Recheck - 2026-05-18

What was wrong:
- The previous Babel surface still had weak `[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]` attributes inside `LocRegistry`.
- `ResolveLookupBudgetForCurrentQuality()` still used a binary `qualityWeight < 0.5f` decision.
- The Babel UI telemetry row was 48 bytes, not one 64-byte L1 cache line.
- Legacy misaligned dictionary fallback and unmanaged `ERROR` fallback could still use local persistent memory when the Vault was unavailable.
- Latest Core compile is blocked by untracked Economy code, not by Babel.

What was done:
- Replaced all remaining Babel jobs with exact Burst flags and `[NoAlias]`.
- Replaced the binary lookup cap with `BabelLookupScalability.ResolveFrameLookupBudget(globalQualityWeight, requestedCount)`: weak weights cap at 20, then polynomial-ramp via `math.lerp` to full request count.
- Padded `BabelTelemetryEntry` to 64 bytes.
- Routed Babel persistent memory through `GlobalDataVault`: `BabelUtf8Blob`, `BabelIndexTable`, `BabelTelemetryRing`, `BabelStagedLocale`, `BabelDecryptionMask`, `BabelOverrideCsvScratch`, `BabelErrorUtf8`, and `BabelDictionaryMappedBytes`.
- Refused to patch external untracked Economy compile failures.

Cinematic Cheats used:
- Dear Lie preserved: the runtime still serves `(hash -> byte offset, byte length)` into unmanaged UTF-8, not managed strings.
- Decryption remains XOR byte math over a caller-owned span, not a narrative object framework.

Exact Microseconds saved:
- Measured Unity profiler delta: not claimed.
- Runtime lookup GC: `0 B/frame` by construction.
- Cold fallback fragmentation: removed from SHINOBU-owned Babel code by Vault routing.
- Static estimate remains `2-8 us` saved per 500-entry UI burst on i3/MX350-class CPUs versus managed/hash-map hydration.

Verification:
- First `dotnet build Hecton8.Core.csproj --no-restore ...` after hardening reported external untracked `TradeMarauderRuntime.cs` errors while timed-out workers were still alive; after cleaning recent orphan workers, serial Core build passed with 0 errors.
- Latest `dotnet build Hecton8.Core.csproj --no-restore ...` -> PASS, 0 errors, 0 warnings.
- Latest `dotnet build Hecton8.Editor.csproj --no-restore ...` -> PASS, 0 errors, 0 warnings.
- Latest `dotnet build Hecton8.PlayModeTests.csproj --no-restore ...` -> PASS, 0 errors, 0 warnings.
- Forbidden scan on Babel runtime files -> PASS: no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint>`, no `Pack = 1`, no weak Burst attribute, no `H8Memory.AllocateRaw`, no `H8Memory.FreeRaw`.
- Layout probe -> PASS: `BabelIndexDTO` 16 bytes, `BabelLookupResultDTO` 16 bytes, `BabelTelemetryEntry` 64 bytes.

<SELF_AUDIT agent_id="SHINOBU_50" pass="ULTRA_POLISH_RECHECK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">1295-byte anomaly repaired; current balance Babel file is 1296 bytes and mod16=0.</TASK>
    <TASK id="02" status="PASS">No `Dictionary&lt;uint,string&gt;`; Babel lookup is flat index plus UTF-8 blob.</TASK>
    <TASK id="03" status="PASS">Hot DTOs expose fields only.</TASK>
    <TASK id="04" status="PASS">`BabelIndexDTO` is 16 bytes: 0 hash, 4 offset, 8 length, 12 pad.</TASK>
    <TASK id="05" status="PASS">Mock signal/buffer/span job exist without Terminal or UI concrete references.</TASK>
    <TASK id="06" status="PASS">Burst binary search kernels use exact flags and `NoAlias`.</TASK>
    <TASK id="07" status="PASS">Endianness validation uses reversed magic and `math.reversebytes()`.</TASK>
    <TASK id="08" status="PASS">Dear Lie dynamic tokens remain raw bytes; no `string.Format` in Babel lookup.</TASK>
    <TASK id="09" status="PASS">Missing hash fallback uses vault-backed unmanaged `ERROR` bytes.</TASK>
    <TASK id="10" status="PASS">Lore decryption is XOR over bytes with a vault/caller mask.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight lookup budget is continuous polynomial math.</TASK>
    <TASK id="12" status="PASS">Locale swap stages padded bytes in the Vault and commits after validation.</TASK>
    <TASK id="13" status="PASS">No AUP/double3 in Babel queries.</TASK>
    <TASK id="14" status="PASS">Aligned files use MMF; misaligned legacy fallback uses Vault padded bytes.</TASK>
    <TASK id="15" status="PASS">Voice link emits `PlayVoiceOverSignal` via SignalBus only.</TASK>
    <TASK id="16" status="PASS">No private Babel raw allocation fallback remains.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry exists; Babel UI telemetry row is 64 bytes.</TASK>
    <TASK id="18" status="PASS">Editor diagnostics facade exists.</TASK>
    <TASK id="19" status="PASS">CSV override parser patches equal-or-shorter UTF-8 slices in Vault-backed active data.</TASK>
    <TASK id="20" status="PASS">Editor XOR decryption preview exists.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DTO name="BabelIndexDTO" size="16">StringHash offset=0 size=4; ByteOffset offset=4 size=4; ByteLength offset=8 size=4; _pad0 offset=12 size=4.</DTO>
    <DTO name="BabelLookupResultDTO" size="16">TextHash offset=0 size=4; ByteOffset offset=4 size=4; ByteLength offset=8 size=4; Flags offset=12 size=4.</DTO>
    <DTO name="BabelTelemetryEntry" size="64">Fields occupy offsets 0..43; `_pad0.._pad4` fill offsets 44..63 to one L1 cache line.</DTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>At GlobalQualityWeight below 0.5, lookup requests collapse to min(20, requested). Above 0.5, a cubic smoothstep ramp feeds `math.lerp(lowBudget, requested, smooth)`. No low/high hardware boolean is used.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Persistent Babel buffers are Vault handles: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes, StaticDataTelemetryRing, StaticDataTelemetryCursor.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Visible prefetch consumes caller dependency and returns scheduled handle; registered reader fences are combined, not blindly completed. Search/XOR/mock jobs mark separate arrays with `NoAlias`; pointer index uses unsafe pointer only over mapped/padded binary truth.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Babel added no direct sibling runtime dependency; audio integration is `SignalBus&lt;PlayVoiceOverSignal&gt;` only. Latest Core wall is external Economy.</COMPILE_GUARD>
  <DEAR_LIE>Before: managed string/dictionary mindset, O(hash hydration + allocation + formatting). After: O(log N) binary search over contiguous 16-byte rows plus raw byte spans; formatting/decryption remains caller-owned byte math.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 Bottom-File Compile-Wall Linkage Addendum - 2026-05-18

What was wrong:
- The earlier bottom report is superseded by a fresher compile-wall pass.
- Fresh Core initially failed on generated-project drift, not Babel: missing existing Waterline, Rollback Netcode, and Memory Sentinel contract/runtime compile includes.
- `HectonRollbackNetcodeRuntime.cs` used `IJob.Run()` without importing `Unity.Jobs`.

What was done:
- `Hecton8.Core.csproj` now includes the existing files needed by current source truth: `ShinobuOceanSurfaceAtmosphereContracts.cs`, `HectonRollbackNetcodeRuntime.cs`, `RollbackNetcodeContracts.cs`, `MemorySentinelSignals.cs`, and `MemorySentinelContracts.cs`.
- `HectonRollbackNetcodeRuntime.cs` now imports `Unity.Jobs`.
- No duplicate mock signal was invented; no Babel direct runtime-domain reference was added.

Cinematic Cheats used:
- Babel remains byte-span truth only: unmanaged UTF-8 blob plus 16-byte sorted index rows.

Exact Microseconds saved:
- Runtime Babel delta: `0 B/frame`, 0 new Babel frame work.
- Compile-wall fix is iteration-time recovery only.

Verification:
- Core build: PASS, 0 errors, 8 warnings.
- Editor build: PASS, 0 errors, 1 warning.
- PlayModeTests build: PASS, 0 errors, 0 warnings.
- Babel verifiers: PASS, 32672 full entries, 1296-byte balance payload, alignment 16.
- Global binary hygiene: still FAIL only on Bakery editor binaries and archived dumps; Babel product payloads are aligned.

<SELF_AUDIT agent_id="SHINOBU_50" pass="BOTTOM_FILE_FINAL_RECHECK">
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS; see full task-by-task XML above. No SHINOBU task was downgraded by the compile-wall linkage stitch.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`BabelIndexDTO`: offset 0 hash u32, 4 offset u32, 8 length u32, 12 pad u32, total 16. `BabelTelemetryEntry`: total 64.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>`GlobalQualityWeight` drives continuous lookup budget through polynomial `math.lerp`; no binary hardware switch.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Search/XOR/mock jobs use `[NoAlias]`; visible prefetch returns `JobHandle` instead of hiding a broad completion.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Babel's new registry entry is `IBabelLocalization` sidecar only; external stitches align generated project files with existing contracts.</COMPILE_GUARD>
  <DEAR_LIE>Runtime text is still `(hash -> byte offset, byte length)`, not managed strings.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 FutureCommandEnvelope Project-Surface Addendum - 2026-05-18

What was wrong:
- A fresh Core compile-wall recheck exposed `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs(179,49): CS0246 FutureCommandEnvelope`.
- The unmanaged DTO already exists in `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs`, but that source file was not included in `Hecton8.Core.csproj`.
- Duplicating the DTO would create ABI drift between the public mod API and the validator.

What was done:
- Added `Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs` to `Hecton8.Core.csproj` next to the existing ModdingAPI compile includes.
- Preserved the existing 64-byte explicit-layout `FutureCommandEnvelope` ABI: offsets 0 opcode, 4 signature, 8 `double3` AUP, 32 `float4` payload, 48 integrity hash, 56 padding.
- No direct Babel-to-Modding runtime dependency was introduced; this is generated project surface alignment only.

Cinematic Cheats used:
- Babel remains the same Dear Lie: hash-to-offset lookup over unmanaged UTF-8 bytes. No managed string framework was added.

Exact Microseconds saved:
- Babel runtime: `0 us/frame`, `0 B/frame`.
- Integration/iteration: removes one current missing-symbol Core compiler wall once CPU rules permit a serial build.

Verification:
- `python Tools\VerifyBabelDictionary.py` -> PASS, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- Direct balance probe -> PASS: `Data/Balance/Baked/Babel_Dictionary.h8bin` length 1296, mod16 0, header size 32, entry count 26, data offset 448, file length 1296, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_SHINOBU_50_Loop12.json` -> GLOBAL FAIL remains with 17 non-Babel misaligned files.
- `git diff --check` on touched SHINOBU/Core project files -> PASS with line-ending warnings only.
- `dotnet build` recheck -> DEFERRED by AGENTS.md hardware guard: CPU samples stayed above 50% and external `dotnet/csc` workers appeared intermittently. No compiler was launched under load by SHINOBU_50.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP12_PROJECT_SURFACE_RECHECK">
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS from the Babel lane. Loop12 was not a new Babel task; it was a compile-wall stitch caused by an omitted existing ModdingAPI source file.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`BabelIndexDTO` remains 16 bytes: offset 0 hash u32, 4 byte offset u32, 8 byte length u32, 12 pad u32. `FutureCommandEnvelope` source ABI is 64 bytes with explicit offsets 0/4/8/32/48/56; no `Pack=1`.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Babel lookup budgeting still consumes continuous `GlobalQualityWeight` through polynomial `math.lerp`; weak hardware caps visible batch work while high/ultra drains full queues.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Babel declares no private persistent arrays in the hot ownership path; active handles remain BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, and BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Babel Burst lookup/XOR/mock jobs retain `[NoAlias]` on independent arrays and return/combine `JobHandle`s; Loop12 did not add a new job graph.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling Runtime reference was added to Babel. The `FutureCommandEnvelope` repair only aligns `Hecton8.Core.csproj` with an existing source file already in the ModdingAPI namespace.</COMPILE_GUARD>
  <DEAR_LIE>Babel still serves raw UTF-8 spans by byte offset; the UI/audio/modding layers receive signals or binary envelopes instead of concrete runtime calls.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_50 Bottom Anchor Loop13 Build-Wall And Babel Alignment Report - 2026-05-18

What was wrong:
- Loop12 correctly deferred compile under AGENTS.md hardware guard, but that was stale after the CPU/compiler window cleared.
- Fresh serial compilation exposed non-Babel project-surface drift and duplicate helper fallout while other agents were moving files.
- Babel product payloads were not the failing asset: full dictionary and balance dictionary remained 16-byte aligned.

What was done:
- Removed the duplicate AssetLifecycle native-handle helper block and kept one implementation.
- Kept the World-owned addressable byte estimator as the single implementation after a duplicate-method race.
- Preserved the hard `GlobalRegistry` `IBabelLocalization` sidecar; no direct Babel sibling Runtime reference was added.
- Re-ran Core, Editor, PlayModeTests, Babel verifier, direct balance probe, hot-file forbidden scan, and binary hygiene.

Cinematic Cheats used:
- Babel still lies cheaply: runtime text is only `(hash -> byte offset, byte length)` over unmanaged UTF-8. Token formatting and presentation remain outside the dictionary lane.

Exact Microseconds saved:
- Babel compile-wall stitches add `0 us/frame` and `0 B/frame`.
- The retained lookup design avoids managed dictionary/string hydration; estimated 2-8 us saved per 500-entry UI burst on i3/MX350-class silicon, unprofiled.

Verification:
- Core build: PASS, 0 errors, 8 warnings.
- Editor build: PASS, 0 errors, 1 warning.
- PlayModeTests build: PASS, 0 errors, 0 warnings.
- Babel verifier: PASS, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- Direct balance probe: PASS, `Data/Balance/Baked/Babel_Dictionary.h8bin` length 1296, mod16 0, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- Hot-file forbidden scan: PASS, no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no weak Burst flags, no raw H8Memory allocation/free, no `string.Format`, no `FindObjectOfType`, no `GameObject.Find`.
- Binary hygiene: GLOBAL FAIL remains in `Docs/AgentLogs/BinaryHygiene_SHINOBU_50_Loop13.json`; the 17 misaligned files are Bakery editor/plugin binaries or archived dump artifacts, not Babel product payloads.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP13_BOTTOM_ANCHOR">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">1295-byte anomaly repaired to 1296 bytes with mod16 == 0.</TASK>
    <TASK id="02" result="PASS">No `Dictionary&lt;uint,string&gt;`; Babel uses flat index rows plus unmanaged UTF-8 bytes.</TASK>
    <TASK id="03" result="PASS">Hot DTO fields are raw public fields, not CS1612-triggering properties.</TASK>
    <TASK id="04" result="PASS">`BabelIndexDTO` is exactly 16 bytes.</TASK>
    <TASK id="05" result="PASS">Mock request/output/converter path exists without concrete UI/Terminal dependency.</TASK>
    <TASK id="06" result="PASS">Burst binary search is O(log N) over sorted 16-byte rows.</TASK>
    <TASK id="07" result="PASS">Endian validation reverses u32 fields when reversed magic is detected.</TASK>
    <TASK id="08" result="PASS">Dynamic tokens remain raw template bytes; Babel does not format strings.</TASK>
    <TASK id="09" result="PASS">Missing hashes return unmanaged `ERROR` fallback bytes.</TASK>
    <TASK id="10" result="PASS">Lore decryption is byte-wise XOR in Burst-compatible data.</TASK>
    <TASK id="11" result="PASS">Continuous `GlobalQualityWeight` limits lookup budget without binary hardware switches.</TASK>
    <TASK id="12" result="PASS">Locale swaps are staged/validated before pointer commit.</TASK>
    <TASK id="13" result="PASS">No AUP/double3 data in Babel DTO/query surface.</TASK>
    <TASK id="14" result="PASS">Aligned MMF path exists; legacy misaligned input is copied into aligned fallback memory.</TASK>
    <TASK id="15" result="PASS">Voice link emits `PlayVoiceOverSignal`, no direct Audio runtime reference.</TASK>
    <TASK id="16" result="PASS">UTF-8 blob/index ownership routes through Vault/MMF, no repeated blob allocation loop.</TASK>
    <TASK id="17" result="PASS">300-frame telemetry ring and dump path exist for lookup anomalies.</TASK>
    <TASK id="18" result="PASS">Editor diagnostics facade exposes entries, CRC, alignment, and padding.</TASK>
    <TASK id="19" result="PASS">CSV override ingestor mutates/appends Vault-backed UTF-8 slices.</TASK>
    <TASK id="20" result="PASS">Editor XOR preview exists for live decrypted lore inspection.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`BabelIndexDTO`: offset 0 u32 hash, 4 u32 offset, 8 u32 length, 12 u32 pad, total 16. `BabelTelemetryEntry`: 64-byte row for false-sharing avoidance. 1295 padding math: `(1295 + 15) &amp; ~15 = 1296`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, visible lookup batches collapse toward the 20-request floor through polynomial `math.lerp`/saturation. High/Ultra drains full request queues and can spend CPU on decrypted diagnostics.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Zero private persistent array ownership for Babel truth. Handles: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Lookup/prefetch/XOR/mock jobs return `JobHandle`s for upstream combination; independent arrays use `[NoAlias]`; pointer search reads mapped/padded index truth.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling Runtime reference was added to Babel. Interfaces/signals route through `GlobalRegistry`, `GlobalDataVault`, and typed `SignalBus` payloads.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: managed text framework tendency. After: O(log N) hash lookup to byte span; rendering, token expansion, and audio stay in their lanes.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_50 Bottom Anchor Loop14 Contract Extraction Report - 2026-05-19

What was wrong:
- `IBabelLocalization` still lived in `GlobalRegistryContracts.cs`, a heavy registry header that imports sibling runtime namespaces and still contains legacy `Pack=1` DTOs owned by other domains.
- Babel-only mocks therefore still had a compile-wall dependency on the registry-contract monolith even though the runtime lookup path was already zero-GC.

What was done:
- Added `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs` and `.meta`.
- Removed the `IBabelLocalization` declaration from `GlobalRegistryContracts.cs`.
- Added `using Hecton8.Core.Contracts;` to `LocalizationManager.cs`.
- Added the new contract file to `Hecton8.Core.csproj`.

Cinematic Cheats used:
- Babel remains raw hash-to-byte-span lookup over unmanaged UTF-8. No managed text framework was introduced.

Exact Microseconds saved:
- Runtime: `0 us/frame`, `0 B/frame`.
- Compile-wall pressure reduced for Babel-only mocks/providers by moving the interface into `Core.Contracts`.

Verification:
- Contract routing scan: PASS, only `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs` defines `public interface IBabelLocalization`.
- `python Tools\VerifyBabelDictionary.py`: PASS, 45 sources, 32672 entries, 1534512 bytes, alignment 16.
- `python Tools\VerifyBabel.py`: PASS, records 32672, sources 45, bytes 1534512, alignment 16, endian little, collisions 0.
- Direct balance payload: PASS, 1296 bytes, mod16 0, CRC `0x199CAC7A`.
- Binary hygiene: GLOBAL FAIL remains in `Docs/AgentLogs/BinaryHygiene_SHINOBU_50_Loop14.json`; failures are Bakery editor/plugin binaries or archived dump artifacts, not Babel payloads.
- Compile recheck: DEFERRED by AGENTS.md CPU guard after code change; CPU samples remained `99.81`, `100`; no `dotnet/csc/MSBuild/VBCSCompiler` process was active.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP14_BOTTOM_ANCHOR">
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS by static/verifier evidence; compile recheck is pending solely because the hardware guard blocks launching `dotnet build` under current CPU load.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`BabelIndexDTO`: offset 0 hash u32, 4 offset u32, 8 length u32, 12 pad u32, total 16. `BabelTelemetryEntry`: 64 bytes. 1295 -> 1296 padding remains verified.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, visible lookup batches collapse toward the 20-request floor; high/ultra drains full queues.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Handles: BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Existing lookup/prefetch/XOR/mock jobs retain `[NoAlias]` and return `JobHandle`s; Loop14 added no runtime job.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>`IBabelLocalization` now routes through `Hecton8.Core.Contracts`; no direct Babel sibling Runtime reference was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Text remains a byte slice, not a managed string table.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_50 Bottom Anchor Loop15 CPU-Locked Static Closure - 2026-05-19

What was wrong:
- A fresh compile is required after Loop14, but the machine remained at 100% CPU while no compiler workers were active.
- Launching `dotnet build` under that load would violate the AGENTS.md hardware guard.

What was done:
- Refused to run `dotnet build` under 100% CPU.
- Verified project-surface includes: `LocRegistry.cs`, `GlobalRegistryContracts.cs`, `LocalizationManager.cs`, `BabelLocalizationContract.cs`, `H8StaticDataContracts.cs`, and `BabelDictionaryStore.cs` each appear exactly once in `Hecton8.Core.csproj`.
- Verified `IBabelLocalization` is defined only in `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs`.
- Verified SHINOBU hot files still have no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no local native allocation signatures, and no weak Burst flags.
- Re-probed `Data/Balance/Baked/Babel_Dictionary.h8bin`.

Cinematic Cheats used:
- No new simulation was added. Babel remains a byte-span facade: hash lookup over flat 16-byte rows, raw UTF-8 span return, optional XOR byte math.

Exact Microseconds saved:
- Runtime: `0 us/frame`, `0 B/frame`.
- Iteration: avoids a false compiler-green claim while the build is legally blocked by machine load.

Verification:
- Babel verifier remained PASS in Loop14: 32672 records, 1534512 bytes, alignment 16.
- Direct balance probe remained PASS: 1296 bytes, mod16 0, CRC `0x199CAC7A`, SHA256 `E15A4465D85A1296AC8D63E5493417A23DDA1AB9B325BBAEA912B1B56D08DB96`.
- Compile recheck: STILL DEFERRED, CPU samples `100`, `99.43`, `100`, `100`, `100`, compiler worker count 0.

<SELF_AUDIT agent_id="SHINOBU_50" pass="LOOP15_CPU_LOCKED_STATIC_CLOSURE">
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS by static/verifier evidence. Build proof remains pending because the hardware guard forbids launching `dotnet build` at 100% CPU.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`BabelIndexDTO`: offset 0 hash u32, 4 offset u32, 8 length u32, 12 pad u32, total 16. `BabelTelemetryEntry`: 64 bytes. Payload: 1296 bytes, mod16 0.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Weak quality keeps lookup work near the 20-request floor; high/ultra drains full queues. No binary low/high switch was added.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Handles remain BabelUtf8Blob, BabelIndexTable, BabelTelemetryRing, BabelStagedLocale, BabelDecryptionMask, BabelOverrideCsvScratch, BabelErrorUtf8, BabelDictionaryMappedBytes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Babel lookup/XOR/mock jobs retain `[NoAlias]`; no new runtime job graph was introduced in Loop15.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Contract extraction stands: `IBabelLocalization` is contract-only in `Hecton8.Core.Contracts`; no direct Babel sibling Runtime reference was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Text remains raw UTF-8 bytes addressed by hash, not a managed string table.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
