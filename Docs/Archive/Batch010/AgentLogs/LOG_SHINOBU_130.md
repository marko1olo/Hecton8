# LOG_SHINOBU_130

## Session Start

What was wrong: Assignment extracted for PDA encyclopedia streamer; status/rationale/log were absent, so no previous-batch state was present.
What was done: Created fresh tracking files and selected relevant mandate registry entries before code.
Cinematic Cheats used: Planned typewriter reveal to mask TextMeshPro vertex rebuild cost.
Exact Microseconds saved: PENDING VERIFICATION; no Unity profiler evidence yet.

## Implementation Report

What was wrong:
- PDA lore presentation needed a path that does not deserialize JSON, build managed strings, or push large text through `TMP_Text.text`.
- Existing `PDADataLogTab` is an audio-log surface with managed strings; replacing it in-place would risk unrelated regressions.
- Unlock truth needed a contiguous rollback-safe Vault block, not managed collections.

What was done:
- Extended `CharBufferPool` with four 8192-char encyclopedia page leases for long TMP submissions.
- Added `PDAEncyclopediaStreamer`: MMF-backed UTF-8 spans through `BabelDictionaryStore.GetUtf8`, manual UTF-8 decode into `Span<char>`, `TMP_Text.SetCharArray`, `ScanCompleteSignal`/`LoreFragmentScannedSignal` routing, 128-byte `EncyclopediaStateDTO`, CAS atomic mask writes, AUP discovery metadata, and 300-frame telemetry dumps.
- Added Vault-backed emergency mock lore bytes and `BabelIndexDTO` offsets.
- Added cold CSV metadata ingest from `lore_metadata.csv` into Vault scratch.
- Added UI Toolkit editor tuner with lock/unlock/select/CSV/raw UTF-8 hex x-ray.
- Added `Docs/ARCHITECTURE/PDA_ENCYCLOPEDIA_STREAMER.md`.

Cinematic Cheats used:
- "Dear Lie" typewriter throttles Canvas vertex rebuild while presenting as terminal text.
- Dynamic tokens resolve during UTF-8 decode; no `string.Replace`.
- Low hardware gets smaller reveal chunks; High/Ultra gets faster reveal without changing the data contract.

Exact Microseconds saved:
- Exact profiler numbers are not available. CPU guard blocked compile/profiler launch (`csc.exe` absent, CPU 100 -> 79 -> 74, still >50).
- Estimates pending profiler: JSON/string load spike removed; Canvas rebuild expected to spread 100-3000 us depending paragraph length; unlock lookup expected 1-10 us per signal.

Compile status:
- Not run. Local rule forbids `dotnet build` while CPU load >50 or another compiler is active.

<SELF_AUDIT>
  <AGENT id="SHINOBU_130" role="PDA_ENCYCLOPEDIA_STREAMER" />
  <BYTE_LAYOUT>
    <EncyclopediaStateDTO size="128" masks="Mask0@0 Mask1@8 Mask2@16 Mask3@24" entries="256" />
    <PdaEncyclopediaRuntimeStateDTO size="128" />
    <PdaEncyclopediaEntryMetaDTO size="64" />
    <PdaEncyclopediaTelemetryEntry size="64" ring="300" />
  </BYTE_LAYOUT>
  <VAULT_BUFFERS>
    <Buffer id="70560" name="EncyclopediaStateDTO" />
    <Buffer id="70561" name="PdaEncyclopediaRuntimeStateDTO" />
    <Buffer id="70562" name="PdaEncyclopediaEntryMetaDTO[256]" />
    <Buffer id="70563" name="PdaEncyclopediaTelemetryEntry[300]" />
    <Buffer id="70564" name="TelemetryCursor" />
    <Buffer id="70565" name="MockUtf8Bytes" />
    <Buffer id="70566" name="MockBabelIndexDTO" />
    <Buffer id="70567" name="CsvScratchBytes" />
  </VAULT_BUFFERS>
  <GC_AUDIT status="PENDING_PROFILER">
    Runtime searches show no `JsonUtility`, `File.ReadAllText`, `Encoding.GetString`, `TMP_Text.text`, or `string.Concat` in `PDAEncyclopediaStreamer`.
    Editor window uses strings only inside `#if UNITY_EDITOR`.
  </GC_AUDIT>
  <BLOCKERS>
    Compile/profiler verification blocked by CPU guard. No fake zero-GC claim recorded.
  </BLOCKERS>
</SELF_AUDIT>

## Loop 6 Polish Report

What was wrong:
- The player AUP token route still depended on a registry convenience slot during setup, but the rationale did not explicitly prove hot-path caching.
- The "Dear Lie" reveal existed as scalar component state; the XML asked for `TypewriterTextJob`, and the Vault law is cleaner with a 64-byte DTO row.
- Per-frame reveal could re-enter `BabelDictionaryStore.GetUtf8()` for an unchanged active entry, adding lookup/telemetry work that has no visual value.
- The black-box ring could report bytes/chars/ticks but could not distinguish real H8LR/MMF/Babel bytes from Vault mock fallback bytes.

What was done:
- `Tick` now refreshes PDA visibility from the static PDA state only when the `PDAEvents` lane is unavailable; normal runtime visibility is event-driven.
- Player pose for `^DISCOVERY_DISTANCE^` uses cached `IPlayerRuntimeContext` refreshed by `IGlobalRegistryHotSwapListener`; no registry polling is hidden in token formatting.
- Added `PdaTypewriterStateDTO` as a 64-byte Vault row `(BufferID)70569`; `TypewriterTextJob` now owns reveal accumulator/count math with Burst Fast/Standard and `[NoAlias]`.
- Active UTF-8 source cache now stores unmanaged pointer, byte length, and source bits after first successful MMF or Vault mock lookup per entry.
- Runtime state flags encode source bits at 8-9; Loop 8 remaps labels to `1=H8LR`, `2=Babel fallback`, `3=Vault mock`. Telemetry packs stream state, source bits, and canvas-split proof, and state hashes mix bytes/flags.
- `Docs/ARCHITECTURE/PDA_ENCYCLOPEDIA_STREAMER.md`, `Docs/Tasks/Status_SHINOBU_130.md`, and this rationale/log lane were updated with the new 70569/typewriter/source-cache facts.

Cinematic Cheats used:
- The typewriter effect remains the deliberate presentation lie: the player sees a noir terminal reveal while TMP geometry rebuild work is spread across frames.
- No physical or narrative "simulation" was introduced. Dynamic tokens are cheap byte/char substitution inside the decode stream.

Exact Microseconds saved:
- Measured proof is still absent because the project compile wall is external. Static estimate only: active-source cache removes one O(log n) Babel lookup plus Babel telemetry write per reveal frame; event-driven visibility removes one static PDA state read per UI tick when events are registered. Canvas savings remain expected 100-3000 us spike spreading depending paragraph size, pending profiler.

Compile status:
- No new `dotnet build` was launched in this pass. Existing guarded build failure remains external: `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is missing from `Hecton8.Core.csproj`.

<SELF_AUDIT phase="LOOP_6_POLISH">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS">Runtime lore path still has no JSON or `File.ReadAllText`; CSV remains cold `FileStream.Read(Span<byte>)`.</Task>
    <Task id="02" status="PASS">Static scan found no runtime concat/string format/`TMP_Text.text` path in `PDAEncyclopediaStreamer.cs`.</Task>
    <Task id="03" status="PASS">Bitmask remains raw public fields in `EncyclopediaStateDTO`; no hot properties.</Task>
    <Task id="04" status="PASS">Primary DTO remains explicit 128 bytes; mask offsets remain 0/8/16/24.</Task>
    <Task id="05" status="PASS">Vault mock UTF-8 slab remains deterministic fallback.</Task>
    <Task id="06" status="PASS_WITH_ADAPTER">Real source remains existing MMF-backed `BabelDictionaryStore`; `ExtractLoreSpanJob` proves binary lookup shape for Vault mock index.</Task>
    <Task id="07" status="PASS">Manual UTF-8 decoder still writes into pooled `Span<char>` and submits via `SetCharArray`.</Task>
    <Task id="08" status="PASS">`TypewriterTextJob` now exists and writes reveal state into Vault buffer 70569.</Task>
    <Task id="09" status="PASS">Unlock route remains SignalBus snapshot + CAS-backed atomic OR.</Task>
    <Task id="10" status="PASS">Canvas split state is recorded and packed into runtime/telemetry flags.</Task>
    <Task id="11" status="PASS">Decode budget and typewriter speed consume continuous `GlobalQualityWeight`; no binary tier branch added.</Task>
    <Task id="12" status="PASS">Dynamic tokens are still stream-decoded, not `string.Replace`.</Task>
    <Task id="13" status="PASS">Distance token uses cached player context, AUP subtraction first, localized `float3`, then span formatting.</Task>
    <Task id="14" status="PASS">Unlock truth remains a contiguous 128-byte Vault block.</Task>
    <Task id="15" status="PASS">State/runtime/typewriter rows are Vault-owned; state rows still cold-cleared by Burst job after uninitialized allocation.</Task>
    <Task id="16" status="PASS">Telemetry ring now includes source/canvas flags and source-aware state hash.</Task>
    <Task id="17" status="PASS">Editor tuner remains UI Toolkit and editor-only allocation quarantine.</Task>
    <Task id="18" status="PASS">CSV metadata path remains cold byte parser.</Task>
    <Task id="19" status="PASS">Raw UTF-8 hex x-ray remains editor-only.</Task>
    <Task id="20" status="FAIL_BLOCKED_EXTERNAL">Static scan passes for this file; Unity compile/profiler zero-GC proof remains blocked by missing external World source.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EncyclopediaStateDTO size="128" alignment="16-byte multiple">
      0..31: four `ulong` masks. 32..55: three `long` discovery grid fields. 56..71: four `float` fields. 72..127: fourteen `uint` metadata/state fields. Total 128 = 16 * 8.
    </EncyclopediaStateDTO>
    <PdaTypewriterStateDTO size="64" alignment="64-byte row">
      0 CharAccumulator f32; 4 GlobalQualityWeight f32; 8 VisibleChars u32; 12 DecodedChars u32; 16 CharsRenderedThisFrame u32; 20 LastFrame u32; 24 Flags u32; 28 StateHash u32; 32/40/48/56 four reserved u64 lanes. Total 64 = one L1 cache line.
    </PdaTypewriterStateDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Decode budget uses `math.lerp(32, 2048, q)`. Typewriter job uses smoothstep polynomial `q*q*(3-2*q)` and `math.lerp(18, 1600, curve)` chars/second. Below 0.3, only small visible increments reach TMP; above 0.7, larger chunks buy richer PDA presentation without changing authoritative bytes.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private runtime arrays in `PDAEncyclopediaStreamer.cs`. Vault buffers: 70560 state, 70561 runtime, 70562 metadata, 70563 telemetry ring, 70564 telemetry cursor, 70565 mock UTF-8, 70566 mock index, 70567 CSV scratch, 70568 lookup result, 70569 typewriter state.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Clear job mutates 70560/70561/70569 with `[NoAlias]` in cold bootstrap. Extract job reads 70566 and writes 70568 with `[ReadOnly]/[WriteOnly]/[NoAlias]`. Typewriter job mutates 70569 with `[NoAlias]` in VISUAL_SYNC. No `.Complete()` exists in `PDAEncyclopediaStreamer.cs`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef or sibling-domain reference was introduced by Loop 6. Existing root `Hecton8.Core.asmdef` already owns this file and has broad pre-existing references; isolating root UI assembly is outside SHINOBU_130's patch boundary.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: one large page could push one large TMP rebuild after full decode, O(n) visible geometry in one frame. After: same O(n) total decode, but visible submission is amortized by quality-weighted typewriter cadence; per-frame TMP work is bounded by reveal step instead of page length.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## Ultra Polish Report

What was wrong:
- The previous runtime had correct direction but still contained rough architecture: mock-first lore resolution, unused `ExtractLoreSpanJob`, component-owned title/meta `char[]`, `NativeArrayOptions.ClearMemory` on the state rows despite Task 15, and only one dump filename.
- Compile proof was previously skipped only by CPU guard; a later guarded build exposed an external missing World source before PDA compilation could be isolated.

What was done:
- Runtime lore resolution is now MMF-first through `BabelDictionaryStore.GetUtf8()`; Vault mock is fallback only for empty/`ERROR` source spans.
- Added Vault buffer `(BufferID)70568` for `BabelLookupResultDTO[1]`; mock extraction now runs `ExtractLoreSpanJob` with exact Burst flags and `[NoAlias]`.
- Replaced runtime title/meta private arrays with `CharBufferPool.Lease`; body/title/meta leases are acquired during cold bootstrap and released on disable.
- `EncyclopediaStateDTO` and runtime state buffers now request `NativeArrayOptions.UninitializedMemory`; cold boot clears the 128-byte state rows via Burst `IJob.Run()` without `.Complete()`.
- Added `^DISCOVERY_DISTANCE^`/`^DISTANCE^` token path: player AUP is read through `GlobalRegistry.Player`, discovery AUP is subtracted first, the localized delta is cast to `float3`, and meters are appended into `Span<char>`.
- Fault dump writes both `Docs/AgentLogs/Dump_SHINOBU_130.bin` and `Docs/AgentLogs/Dump_PDA_STREAMER.bin`.
- Architecture doc updated for MMF-first routing, buffer 70568, AUP distance token, and dual dump paths.

Cinematic Cheats used:
- "Dear Lie" remains the typewriter reveal: Canvas/TMP vertex cost is throttled by presentation cadence, not by compromising lore state.
- Dynamic tokens are resolved inside the UTF-8 decode stream so no `string.Replace` pass exists.
- Mock lore is byte-addressed in Vault and looked up through the same Burst index shape, so testing does not require JSON or prefab text.

Exact Microseconds saved:
- Profiler proof remains blocked by external compile wall. Estimates only: mock lookup O(n) -> O(log n), duplicate mock-first data miss avoided, managed per-component title/meta arrays removed, and state zero-fill moved to cold 128-byte Burst clear.

Compile status:
- Guarded build command: `dotnet build .\Hecton8.slnx --no-restore -v:minimal`.
- Guard condition: CPU=5, `csc.exe` absent.
- Result: FAILED before PDA proof on missing external source `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` referenced by `Hecton8.Core.csproj`.

<SELF_AUDIT phase="ULTRA_POLISH">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS">Runtime PDA streamer has no JSON or `File.ReadAllText` lore load path; CSV ingest is cold `FileStream.Read(Span<byte>)` only.</Task>
    <Task id="02" status="PASS">Hot UI text goes through `Span<char>` and `TMP_Text.SetCharArray`; no runtime concat/string format scan hits.</Task>
    <Task id="03" status="PASS">Unlock truth is raw public fields in `EncyclopediaStateDTO`; no `get; set;` bitmask properties.</Task>
    <Task id="04" status="PASS">`EncyclopediaStateDTO` is explicit 128 bytes; validation checks size and mask offsets.</Task>
    <Task id="05" status="PASS">Vault-backed mock UTF-8 slab and `BabelIndexDTO` table exist for isolated testing.</Task>
    <Task id="06" status="PASS_WITH_ADAPTER">`ExtractLoreSpanJob` binary-searches `BabelIndexDTO` and writes `BabelLookupResultDTO`; real MMF bytes come from existing `BabelDictionaryStore` because the ledger says H8LR lore reader is not ready.</Task>
    <Task id="07" status="PASS">Manual UTF-8 decoder writes UTF-16 chars into pooled spans; invalid bytes become replacement chars and trigger dump path.</Task>
    <Task id="08" status="PASS">Typewriter reveal throttles TMP submissions through `_visibleLength`.</Task>
    <Task id="09" status="PASS">Scan/lore signals set bitmask through CAS-backed atomic OR.</Task>
    <Task id="10" status="PASS">Static/dynamic Canvas references are validated and dynamic text Canvas is isolated when bound.</Task>
    <Task id="11" status="PASS">Decode budget and reveal speed consume continuous `HomeostasisBrain.GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS">`^TOKEN^` replacements happen inside the UTF-8 decode stream.</Task>
    <Task id="13" status="PASS">Discovery distance subtracts player/discovery AUP first, casts local delta to `float3`, and formats meters into the page span.</Task>
    <Task id="14" status="PASS">Authoritative unlock state is a contiguous 128-byte Vault DTO, memcpy/rollback compatible.</Task>
    <Task id="15" status="PASS">State rows request `UninitializedMemory`; cold clear uses Burst `IJob.Run()`.</Task>
    <Task id="16" status="PASS">300-entry telemetry ring records frame/hash/unlocks/chars/ticks/fault and dumps on fault.</Task>
    <Task id="17" status="PASS">UI Toolkit tuner can refresh/select/lock/unlock/ingest/x-ray.</Task>
    <Task id="18" status="PASS">Cold CSV parser hashes names and maps bit indices from byte spans.</Task>
    <Task id="19" status="PASS">Editor raw UTF-8 hex x-ray uses runtime span writer; editor string conversion is quarantined.</Task>
    <Task id="20" status="FAIL_BLOCKED_EXTERNAL">Static no-GC scan passed, but Unity profiler and compile proof are blocked by missing external World source in `Hecton8.Core.csproj`.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <EncyclopediaStateDTO size="128">
      Mask0: offset 0, size 8; Mask1: offset 8, size 8; Mask2: offset 16, size 8; Mask3: offset 24, size 8.
      LastDiscoveryGridX/Y/Z: offsets 32/40/48, size 8 each.
      LastDiscoveryLocalX/Y/Z: offsets 56/60/64, size 4 each.
      GlobalQualityWeight: offset 68, size 4.
      Hash/count/revision/state uint fields: offsets 72..124, size 4 each.
      Final size is 128 bytes, exact 16-byte multiple and two 64-byte cache lines.
    </EncyclopediaStateDTO>
    <RuntimeStateDTO size="128" />
    <EntryMetaDTO size="64" />
    <TelemetryEntry size="64" falseSharing="single writer ring; 64-byte stride" />
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is saturated to [0,1]. Decode budget lerps 32 -> 2048 chars/frame. Reveal speed lerps 18 -> 1600 chars/second. Below 0.3 the page advances slowly, spreading TMP vertex rebuilds; above 0.7 it reveals large chunks while keeping the same byte-span and bitmask contracts.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime streamer declares zero private array allocations after polish. Persistent buffers requested from Vault: 70560 state, 70561 runtime, 70562 metadata, 70563 telemetry, 70564 cursor, 70565 mock UTF8, 70566 mock index, 70567 CSV scratch, 70568 lookup result.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Clear job: consumes no dependency in current cold boot, mutates 70560/70561 with `[NoAlias]`, runs synchronously during bootstrap only.
    Extract job: reads 70566 with `[ReadOnly][NoAlias]`, writes 70568 with `[WriteOnly][NoAlias]`, runs during mock fallback lookup.
    No runtime `.Complete()` calls remain in `PDAEncyclopediaStreamer`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef reference was added. `PDAEncyclopediaStreamer.cs` lives under root `Hecton8.Core`; using directives route through existing Core/Contracts/Memory/World types and GlobalRegistry interfaces. Build is blocked by external missing World file, not by a PDA compile diagnostic yet.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: opening a large lore page could force one O(n) decode plus one large TMP geometry rebuild in a single frame.
    After: O(n) decode is amortized across frames by budget, and visible TMP submission is limited by quality-weighted typewriter cadence. The illusion buys frame time without changing authoritative lore bytes.
  </DEAR_LIE>
</SELF_AUDIT>

## Loop 7 Polish Report

What was wrong:
- The runtime PDA file still named `Hecton8.World.AbsoluteUniversePosition` directly. The data came from contract signals, but the UI domain did not need to carry a World namespace dependency in its own logic.
- The editor-time layout proof covered the primary 128-byte bitmask DTO only. The 64-byte telemetry/typewriter rows and the AUP copy row were not visible in the tuner.

What was done:
- Removed `using Hecton8.World` and all explicit `AbsoluteUniversePosition` locals from `PDAEncyclopediaStreamer.cs`.
- Added owner-local `PdaAup48` with explicit 48-byte layout: three `long` grid fields, three `float` local fields, a `uint` pad, and a final `ulong` lane.
- Contract signal/player AUP values are copied into `PdaAup48` immediately. Discovery-distance token math uses `HectonPhysicsContract.AupSectorSizeMetersInt`, clamps sector deltas, casts the localized delta to `float3`, and formats meters into the active span.
- Added `ValidatePdaStreamerLayouts` and extended the UI Toolkit tuner to show all PDA DTO sizes and critical offsets.
- Updated `Docs/ARCHITECTURE/PDA_ENCYCLOPEDIA_STREAMER.md`, `Status_SHINOBU_130.md`, and `Rationale_SHINOBU_130.md`.

Cinematic Cheats used:
- No new simulation. The existing typewriter presentation lie remains the only runtime visual fake; Loop 7 only reduces ownership ambiguity and strengthens static proof.

Exact Microseconds saved:
- No frame-time claim. This pass removes compile-wall risk and unaudited layout risk. Token math remains sub-us; profiler proof is still blocked by the external missing World source.

Compile status:
- No new `dotnet build` was launched. The known external blocker remains `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` missing from `Hecton8.Core.csproj`.

<SELF_AUDIT phase="LOOP_7_POLISH">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS">Runtime lore path still has no JSON or `File.ReadAllText`.</Task>
    <Task id="02" status="PASS">Static scan found no runtime `TMP_Text.text`, concat, string format, or `Encoding.GetString` in `PDAEncyclopediaStreamer.cs`.</Task>
    <Task id="03" status="PASS">Unlock masks remain raw public fields; no hot properties.</Task>
    <Task id="04" status="PASS">Primary DTO is 128 bytes; full layout validator now also checks runtime/meta/telemetry/typewriter/AUP rows.</Task>
    <Task id="05" status="PASS">Vault mock database still byte-backed and deterministic.</Task>
    <Task id="06" status="PASS_WITH_ADAPTER">MMF-first Babel source remains authoritative; mock lookup remains Burst binary search through 70568.</Task>
    <Task id="07" status="PASS">UTF-8 decode remains manual into pooled `Span<char>`.</Task>
    <Task id="08" status="PASS">`TypewriterTextJob` remains Burst Fast/Standard over Vault buffer 70569.</Task>
    <Task id="09" status="PASS">SignalBus unlock route still writes bitmask via CAS-backed atomic OR.</Task>
    <Task id="10" status="PASS">Canvas split proof remains packed into state/telemetry flags.</Task>
    <Task id="11" status="PASS">Reveal/decode cadence still consumes continuous `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS">Dynamic tokens still resolve in the decode stream.</Task>
    <Task id="13" status="PASS">AUP token now uses owner-local `PdaAup48`; no direct World helper call remains.</Task>
    <Task id="14" status="PASS">Rollback truth remains contiguous 128-byte Vault DTO.</Task>
    <Task id="15" status="PASS">Uninitialized Vault rows and cold Burst clear remain unchanged.</Task>
    <Task id="16" status="PASS">300-frame telemetry ring remains source-aware.</Task>
    <Task id="17" status="PASS">Editor tuner now exposes extended layout x-ray.</Task>
    <Task id="18" status="PASS">Cold CSV parser remains byte-span based.</Task>
    <Task id="19" status="PASS">Raw UTF-8 hex x-ray remains editor-only.</Task>
    <Task id="20" status="FAIL_BLOCKED_EXTERNAL">Static proof improved; compile/profiler proof still blocked by missing external World source.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EncyclopediaStateDTO size="128">Masks at 0/8/16/24; discovery grid at 32/40/48; local floats at 56/60/64; scalar state fields through 124. 128 = 16-byte multiple.</EncyclopediaStateDTO>
    <PdaEncyclopediaRuntimeStateDTO size="128" sourceBytesOffset="92" />
    <PdaEncyclopediaEntryMetaDTO size="64" />
    <PdaEncyclopediaTelemetryEntry size="64" flagsOffset="48" capacityOffset="60" />
    <PdaTypewriterStateDTO size="64" reserved3Offset="56" />
    <PdaAup48 size="48" reserved1Offset="40">0/8/16 long grid fields; 24/28/32 float locals; 36 uint pad; 40 ulong pad. 48 = 16-byte multiple.</PdaAup48>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    No binary quality switch was added. Below 0.3, the typewriter job advances small visible counts and minimizes TMP dirtied characters. High/Ultra use the same source pointer and Vault state but reveal larger chunks.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No private runtime arrays were added. Vault buffers remain 70560..70569.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    No new jobs were added. Existing clear/extract/typewriter jobs keep `[NoAlias]`; no `.Complete()` scan hits.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    `PDAEncyclopediaStreamer.cs` no longer has a direct `Hecton8.World` using or explicit World AUP local. It consumes Core contracts/signals, GlobalRegistry slots, Vault, and TMP only.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The Dear Lie remains quality-weighted typewriter reveal: same O(n) total text work, bounded per-frame TMP submission.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## Loop 8 H8LR Source-Truth Report

What was wrong:
- The streamer still treated the small Babel dictionary path as primary proof. That was false for real encyclopedia pages because `Data/Lore/Encyclopedia.h8bin` is H8LR raw UTF-8, not the Babel balance dictionary.
- A 8192-char page could deadlock longer entries after filling the char buffer while `_sourceByteCursor` still had unread bytes.
- A 4-byte UTF-8 scalar crossing the page/budget edge could be marked invalid instead of deferred.

What was done:
- Promoted `PdaH8lrLoreStore` to primary source: H8LR first, Babel fallback second, Vault mock last.
- Added Vault buffer `(BufferID)70570` as an 8 MiB H8LR native mirror fallback for platforms without MMF.
- Changed the default/editor hash to `0xAEC57EAC`, matching the first H8LR record in `Data/Lore/Encyclopedia.h8bin`.
- Expanded `CharBufferPool.EncyclopediaPageCapacity` to 32768 chars and added rolling-window continuation for entries larger than one page.
- Added H8LR header/record DTO layout validation to `ValidatePdaStreamerLayouts` and the UI Toolkit tuner.
- Guarded MMF pointer release so failed `AcquirePointer` cannot cascade into a bad `Dispose()`.

Cinematic Cheats used:
- The Dear Lie remains typewriter reveal. H8LR source bytes are true; presentation is throttled. Low-tier devices dirty fewer TMP characters per frame while high/ultra reveal faster using the same source pointer.

Exact Microseconds saved:
- No measured claim. Static source proof only. Expected gain is eliminating a false fallback path and preventing full-entry string materialization; profiler/GC proof remains pending.

Verification:
- Parsed `Data/Lore/Encyclopedia.h8bin`: magic `0x524C3848`, version `1`, count `2`, record0 `0xAEC57EAC offset=48 length=25003`, record1 `0xBC52DB39 offset=25056 length=16861`, file bytes `41920`.
- Static scan over `PDAEncyclopediaStreamer.cs` and `PdaH8lrLoreStore.cs` found no direct World namespace/type, JSON, `File.ReadAllText`, `Encoding.GetString`, `TMP_Text.text`, concat/string format, `foreach`, `.Complete()`, `Pack=1`, `NativeHashMap`, or `NativeList` hits.
- No `dotnet build` was launched; known compile wall is still the external missing World source previously recorded.

<SELF_AUDIT phase="LOOP_8_H8LR_SOURCE_TRUTH">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS">Runtime lore loading uses H8LR/Babel/native spans, not JSON or `File.ReadAllText`.</Task>
    <Task id="02" status="PASS">Hot PDA text still writes `Span<char>` and `TMP_Text.SetCharArray`; static scan found no runtime string assembly hits.</Task>
    <Task id="03" status="PASS">Bitmask remains raw public fields in the Vault DTO.</Task>
    <Task id="04" status="PASS">State/runtime/meta/telemetry/typewriter/AUP/H8LR DTO layouts are validated.</Task>
    <Task id="05" status="PASS">Vault mock database remains deterministic fallback for CI/editor isolation.</Task>
    <Task id="06" status="PASS">Real H8LR `.h8bin` is now the first source; mock lookup still uses `ExtractLoreSpanJob` for binary-search proof.</Task>
    <Task id="07" status="PASS">UTF-8 decoder remains allocation-free and now defers 4-byte scalars at budget/page boundaries.</Task>
    <Task id="08" status="PASS">`TypewriterTextJob` remains the Dear Lie reveal throttle.</Task>
    <Task id="09" status="PASS">Signal unlock route still performs CAS-backed atomic OR into the dense masks.</Task>
    <Task id="10" status="PASS">Canvas split state remains validated and recorded.</Task>
    <Task id="11" status="PASS">Decode budget and reveal cadence still use continuous `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS">Token replacement remains inside the decode stream.</Task>
    <Task id="13" status="PASS">AUP distance token still subtracts player/discovery AUP and casts localized delta to `float3`.</Task>
    <Task id="14" status="PASS">Unlock state remains contiguous 128-byte rollback truth.</Task>
    <Task id="15" status="PASS">State rows still use `UninitializedMemory` and cold Burst clear.</Task>
    <Task id="16" status="PASS">300-frame telemetry ring and dual dump filenames remain active.</Task>
    <Task id="17" status="PASS">UI Toolkit tuner now uses the real H8LR default hash and displays H8LR DTO layout.</Task>
    <Task id="18" status="PASS">CSV metadata ingest remains cold byte-span parser.</Task>
    <Task id="19" status="PASS">Raw UTF-8 hex x-ray now checks H8LR before Babel/mock.</Task>
    <Task id="20" status="FAIL_BLOCKED_EXTERNAL">Static no-GC proof improved; Unity profiler/GC proof is still unavailable because compile/runtime proof is blocked outside this domain.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EncyclopediaStateDTO size="128">Mask0/1/2/3 offsets 0/8/16/24, grid longs 32/40/48, local floats 56/60/64, quality 68, uint state fields 72..124. 128 bytes = two 64-byte lines.</EncyclopediaStateDTO>
    <PdaH8lrHeaderDTO size="16">Magic offset 0, Version 4, Count 8, Reserved0 12. 16-byte aligned.</PdaH8lrHeaderDTO>
    <PdaH8lrRecordDTO size="16">Hash offset 0, ByteOffset 4, ByteLength 8, Reserved0 12. 16-byte aligned.</PdaH8lrRecordDTO>
    <PdaAup48 size="48">GridX/Y/Z offsets 0/8/16, LocalX/Y/Z offsets 24/28/32, Reserved0 36, Reserved1 40. 48-byte multiple of 16.</PdaAup48>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below quality 0.3 the decoder consumes the same H8LR bytes but advances small char budgets and a slow typewriter cursor, reducing TMP rebuild pressure. Higher weights lerp toward larger decode chunks and faster reveal; no binary low/high switch was added.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No private runtime arrays were added. Vault buffers now requested: 70560 state, 70561 runtime, 70562 metadata, 70563 telemetry, 70564 cursor, 70565 mock UTF8, 70566 mock index, 70567 CSV scratch, 70568 lookup result, 70569 typewriter, 70570 H8LR mirror.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Clear/extract/typewriter jobs keep `[NoAlias]`. H8LR reader is a cold/lazy byte-span source, not a Burst job. No `.Complete()` calls exist in the PDA runtime/H8LR reader scan.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No asmdef reference or sibling-domain concrete dependency was added. The reader lives under `Hecton8.UI` and uses Core contracts/Vault plus `System.IO.MemoryMappedFiles` behind platform guards.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    Before: one large page risked immediate full TMP rebuild or a stalled buffer. After: page reveal is windowed and quality-weighted; complexity remains O(n) total bytes but per-frame work is bounded by decode budget and visible-character cadence.
  </THE_DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
