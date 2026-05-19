# SHINOBU_130 PDA_ENCYCLOPEDIA_STREAMER

Status: POLISH IN PROGRESS / COMPILE BLOCKED BY EXTERNAL WORLD SOURCE
Batch: CURRENT_BATCH.md
Domain: Echelon 8 Presentation & UX / PDA Encyclopedia Streaming
Task Count: 20

## Mandates Selected Before Coding

- [x] UI_Data_Streaming_ZeroGC_Optimization.txt
- [x] UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- [x] DATA_Runtime_Struct_Layout_ARM64.txt
- [x] DATA_Save_Persistence_Binary_Delta_Checksum.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- [x] ARCH_Signal_Lane_Segregation.txt

## Loop 0 - Archaeology

- [x] Read selected mandates | DOD: registry read before code | Rejected: direct implementation from prompt memory | Estimate: 0 us runtime
- [x] Extract SHINOBU_130 XML block | DOD: CLI regex over CURRENT_BATCH.md cover-to-cover block | Rejected: MCP/truncated read | Estimate: 0 us runtime
- [x] Scan existing PDA/UI/Narrative code | DOD: located CharBufferPool, BabelDictionaryStore, Vault, SignalBus, PDAEvents | Rejected: greenfield duplicate MMF service | Estimate: 0 us runtime

## Loop 1 - Tasks 01-05

- [x] Task 01 JSON_DESERIALIZATION_ERADICATION | DOD: new runtime path uses `BabelDictionaryStore.GetUtf8`/Vault bytes, no JSON or `File.ReadAllText` | Rejected: managed lore JSON loader | Estimate: avoids multi-MB GC spike; exact us pending profiler
- [x] Task 02 STRING_CONCATENATION_PURGE | DOD: hot UI writes use `Span<char>`, `ZeroGCFormatter`, `TMP_Text.SetCharArray` | Rejected: `TMP_Text.text`, `string.Concat`, `Encoding.GetString` | Estimate: 50-2000 us/frame avoided on lore reveal, pending profiler
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: `EncyclopediaStateDTO` exposes raw public fields and is accessed by `ref` from Vault | Rejected: properties/List/string state | Estimate: 1-5 us lookup path saved, pending profiler
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: `[StructLayout(LayoutKind.Explicit, Size = 128)]` plus `ValidateEncyclopediaStateLayout` checks size and mask offsets | Rejected: sequential/unverified layout | Estimate: 0.1-1 us lookup stability gain, pending profiler
- [x] Task 05 EMERGENCY_MOCK_LORE_DATABASE | DOD: deterministic Vault-backed UTF-8 slab and `BabelIndexDTO` offsets | Rejected: waiting on Narrative/Agent 103 | Estimate: 0 us runtime dependency risk

## Loop 2 - Tasks 06-10

- [x] Task 06 MMF_TEXT_EXTRACTION_KERNEL | DOD: reused existing MMF-backed `BabelDictionaryStore`; added Burst `ExtractLoreSpanJob` for index lookup DTOs | Rejected: second MMF owner | Estimate: 10-100 us cold lookup saved by reusing existing store
- [x] Task 07 ZERO_GC_UTF8_DECODING | DOD: manual UTF-8 to UTF-16 decode into `CharBufferPool.EncyclopediaLease.Span` | Rejected: `System.Text.Encoding.GetString` | Estimate: allocation spike removed; exact us pending profiler
- [x] Task 08 THE_DEAR_LIE_TYPEWRITER_EFFECT | DOD: visible character cursor throttles `SetCharArray` submissions in LateFrame | Rejected: instant wall-of-text vertex rebuild | Estimate: 100-3000 us frame spike spread across frames, pending profiler
- [x] Task 09 BITMASK_UNLOCK_ROUTING | DOD: reads `ScanCompleteSignal` and `LoreFragmentScannedSignal`; sets mask via CAS-backed atomic OR | Rejected: direct object dependency or non-authoritative list | Estimate: 1-10 us per unlock
- [x] Task 10 HARDWARE_AWARE_CANVAS_SPLITTING | DOD: serialized static/dynamic Canvas split validated; dynamic Canvas sorting isolated when provided | Rejected: dirtying one monolithic PDA canvas | Estimate: 100-1000 us rebuild scope reduction, pending profiler

## Loop 3 - Tasks 11-15

- [x] Task 11 CONTINUOUS_SCALABILITY_TEXT_SPEED | DOD: decode budget and typewriter rate consume `HomeostasisBrain.GlobalQualityWeight` continuously | Rejected: high/low binary switch | Estimate: 0.1 ms spike cap target, pending profiler
- [x] Task 12 DYNAMIC_TOKEN_REPLACEMENT | DOD: decoder intercepts `^TOKEN^` and inserts formatted values into the destination span | Rejected: `string.Replace` | Estimate: 10-500 us and allocation avoided per tokenized page
- [x] Task 13 AUP_PRECISION_DISCOVERY_MARKERS | DOD: `ScanCompleteSignal.PositionAup` copied into `EncyclopediaStateDTO` and metadata DTO | Rejected: storing GameObject/Transform references | Estimate: deterministic rollback-safe location, 0 us object lookup
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: unlock truth is contiguous 128-byte Vault DTO `(BufferID)70560` with raw masks | Rejected: managed collections | Estimate: blind memcpy-compatible
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | DOD: Vault metadata/telemetry/mock buffers use `UninitializedMemory`; 128-byte state is cleared by Burst job on cold boot | Rejected: per-frame managed initialization | Estimate: cold boot only; exact us pending profiler

## Loop 4 - Tasks 16-20

- [x] Task 16 TELEMETRY_PDA_RECORDER | DOD: 300-entry Vault ring records frame, hash, chars, bytes, decode/canvas ticks, fault | Rejected: "unknown crash" report | Estimate: 64 bytes/frame while PDA open
- [x] Task 17 ENCYCLOPEDIA_TUNER_EDITOR_WINDOW | DOD: UI Toolkit window can refresh, select hash, lock/unlock all, ingest CSV | Rejected: runtime debug strings | Estimate: editor-only allocation quarantine
- [x] Task 18 CSV_LORE_INGESTOR | DOD: cold parser reads file into Vault scratch span and parses hash/bit without `ReadAllText` | Rejected: managed CSV split/string arrays | Estimate: cold path only, no play-session GC
- [x] Task 19 LIVE_TEXT_DEBUG_GIZMO | DOD: editor raw UTF-8 hex x-ray plus selected gizmo progress bar | Rejected: runtime text debug allocations | Estimate: editor-only
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static searches completed; profiler GC proof pending because Unity compile/profiler run blocked by CPU guard | Rejected: fake zero-GC report | Estimate: PENDING VERIFICATION

## Verification

- [ ] Compile check obeying CPU/csc guard | BLOCKED: first attempt skipped by CPU guard; later guarded `dotnet build .\Hecton8.slnx --no-restore` ran at CPU=5/csc=none and failed before PDA compile proof on missing external source `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` referenced by `Hecton8.Core.csproj`
- [x] Self-review pass 1 | JSON/string hot path scan: no runtime `JsonUtility`, `File.ReadAllText`, `Encoding.GetString`, `TMP_Text.text`, `string.Concat`
- [x] Self-review pass 2 | Layout scan: `EncyclopediaStateDTO` size 128, mask offsets 0/8/16/24, raw public fields
- [x] Self-review pass 3 | Span/TMP scan: `ReadOnlySpan<byte>` to `Span<char>` decoder and `SetCharArray`
- [x] Self-review pass 4 | Signal/rollback scan: SignalBus consumption, Vault ref access, atomic CAS OR
- [x] Self-review pass 5 | Telemetry/debug scan: 300-frame ring, dump path, editor raw hex, architecture doc

## Loop 5 - Ultra Polish Reconciliation

- [x] Re-read SHINOBU_130 XML block after polish mandate | DOD: CLI regex extracted own `<AGENT_PROMPT id="SHINOBU_130"...>` cover-to-cover | Rejected: relying on compressed chat memory | Estimate: 0 us runtime
- [x] Re-read binary payload ledger and architecture docs | DOD: confirmed `Babel_Dictionary.h8bin` is current aligned MMF path and `Data/Lore/Encyclopedia.h8bin` H8LR reader is not ready for this streamer | Rejected: claiming H8LR support without reader | Estimate: avoids duplicate MMF owner
- [x] Runtime private array purge | DOD: title/meta/body buffers are leased from `CharBufferPool`; no private `char[]` remains in `PDAEncyclopediaStreamer` | Rejected: component-owned runtime text arrays | Estimate: removes per-component managed buffer footprint; exact us pending profiler
- [x] MMF-first fallback order | DOD: `ResolveActiveUtf8` tries `BabelDictionaryStore.GetUtf8()` first and falls back to Vault mock only on empty/`ERROR` sentinel | Rejected: mock-first path hiding real baked data | Estimate: 10-100 us cold lookup stability, pending profiler
- [x] Task 06 actual job use | DOD: mock lookup now uses `ExtractLoreSpanJob` with `[BurstCompile(...Fast/Standard)]` and `[NoAlias]` over a Vault result slot `(BufferID)70568` | Rejected: ordinary linear managed-side loop as proof path | Estimate: O(log n) lookup vs O(n) mock scan
- [x] Task 13 distance token | DOD: `^DISCOVERY_DISTANCE^` subtracts player AUP from discovery AUP, casts local delta to `float3`, formats into `Span<char>` | Rejected: absolute double formatting and GameObject references | Estimate: deterministic UI math, 0 object lookup
- [x] Task 15 uninitialized boot | DOD: `EncyclopediaStateDTO` and runtime state buffers are requested with `NativeArrayOptions.UninitializedMemory`; cold boot clear uses Burst `IJob.Run()` without `.Complete()` | Rejected: OS zero-fill and arbitrary `Schedule().Complete()` | Estimate: 128-byte cold clear; exact us pending profiler
- [x] Dump path reconciliation | DOD: fault dump writes both `Dump_SHINOBU_130.bin` and `Dump_PDA_STREAMER.bin` | Rejected: satisfying only one protocol name | Estimate: fault-only

## Loop 6 - Hot-Path Cache And Blackbox Tightening

- [x] Hot player-context route hardened | DOD: `GlobalRegistry.Player` is resolved only in cold lifecycle / hot-swap listener; AUP distance token reads cached `IPlayerRuntimeContext` | Rejected: hidden per-token registry lookup in the decode path | Estimate: removes one static registry read per distance-token render; exact us pending profiler
- [x] PDA visibility hot polling reduced | DOD: `Tick` calls `RefreshVisibility()` only when PDA event registration is unavailable; `OnPDAEvent` remains the normal state route | Rejected: per-frame static `PlayerPDA.IsOpen` polling when event lane exists | Estimate: sub-us per frame, but removes an avoidable global read from the UI lane
- [x] Task 08 literal job reconciled | DOD: `TypewriterTextJob` writes reveal accumulator and visible count into Vault buffer `(BufferID)70569` with Burst Fast/Standard and `[NoAlias]` | Rejected: scalar-only typewriter state as task proof | Estimate: spreads TMP vertex rebuild across frames; exact canvas us pending profiler
- [x] Active UTF-8 source cache | DOD: first successful MMF/native span lookup stores unmanaged pointer, byte length, and source flags for the active entry | Rejected: per-frame `BabelDictionaryStore.GetUtf8()` lookup during reveal | Estimate: avoids O(log n)+telemetry lookup per reveal frame; exact us pending profiler
- [x] Source-aware blackbox flags | DOD: runtime state and telemetry now encode `1=MMF/Babel`, `2=Vault mock`; state hashes mix source bytes/flags | Rejected: ambiguous fault dump that cannot distinguish real MMF read from fallback | Estimate: 0 us meaningful frame gain; improves dump forensic value
- [x] Static source scan after Loop 6 | DOD: `rg` found no runtime JSON, `Encoding.GetString`, `TMP_Text.text`, hot string formatting, private arrays, local `NativeList`/`NativeHashMap`, or `Pack=1` in `PDAEncyclopediaStreamer.cs` | Rejected: build claim without source proof | Estimate: verification only

## Loop 7 - Compile-Wall And Layout Audit Tightening

- [x] Re-extracted SHINOBU_130 XML block | DOD: CLI regex over `CURRENT_BATCH.md` after Loop 6 | Rejected: trusting compacted chat memory | Estimate: 0 us runtime
- [x] Direct World AUP naming removed from PDA runtime | DOD: `PDAEncyclopediaStreamer.cs` no longer has `using Hecton8.World` or explicit `AbsoluteUniversePosition` locals; contract signal/player AUP fields are copied into owner-local `PdaAup48` | Rejected: UI runtime naming sibling World domain type in its own logic | Estimate: compile-wall risk reduction, no frame gain claimed
- [x] Owner-local AUP math hardened | DOD: `PdaAup48` stores primitive grid/local fields, distance token uses `HectonPhysicsContract.AupSectorSizeMetersInt`, clamps sector deltas, and returns zero on non-finite axis math | Rejected: calling World static AUP helpers from the PDA runtime | Estimate: sub-us token path; deterministic 100km jitter guard preserved
- [x] Full DTO layout validation added | DOD: `ValidatePdaStreamerLayouts` checks 128/128/64/64/64/48 byte rows and source/telemetry/typewriter/AUP padding offsets | Rejected: proving only `EncyclopediaStateDTO` while telemetry/typewriter rows were unaudited | Estimate: editor/cold verification only
- [x] Tuner layout x-ray extended | DOD: UI Toolkit tuner now reports DTO sizes and critical offsets in editor-only text | Rejected: runtime debug labels | Estimate: 0 runtime overhead
- [x] Static source scan after Loop 7 | DOD: `rg` found no direct World namespace/type in PDA runtime and no runtime JSON/Encoding/TMP `.text`/concat/foreach/`.Complete()`/`Pack=1` hits | Rejected: launching another build while external compile wall is unchanged | Estimate: verification only
