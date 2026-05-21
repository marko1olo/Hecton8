# PDA Encyclopedia Streamer

Owner: SHINOBU_130 / Echelon 8 Presentation & UX.
Source anchors: `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`, `Assets/_Project/Scripts/UI/Editor/PDAEncyclopediaTunerWindow.cs`.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Runtime path:
- `PDAEncyclopediaStreamer` reads real encyclopedia payloads from `Data/Lore/Encyclopedia.h8bin` through the owner-local `PdaH8lrLoreStore` first. The H8LR reader maps the file on Editor/Standalone and uses Vault buffer `(BufferID)70570` as an 8 MiB native mirror fallback on platforms without MMF. The fallback mirror is passed as a `VaultGenerationHandle<byte>` and resolved as a phase-local `NativeArray<byte>` view before reads; `PdaH8lrLoreStore` no longer persists a bare Vault pointer without generation proof.
- `PdaH8lrLoreStore.TryGetUtf8()` is a pure span lookup. It no longer mutates last-depth/key/prefetch counters; any tracked lookup evidence must stay in explicit telemetry routes.
- `BabelDictionaryStore.FetchUtf8()` remains a pure legacy fallback after H8LR. Telemetry and linked-audio publishing are restricted to explicit `TrackUtf8Lookup()` owner-phase calls; the PDA streamer uses the pure span path. The deterministic Vault mock slab is only the final CI/editor fallback when no real binary source resolves.
- The active UTF-8 source pointer is cached per entry after the first H8LR/Babel/Vault lookup. Per-frame reveal work reuses the cached unmanaged pointer and byte length instead of re-querying source indexes.
- The hot path does not call JSON, `Encoding.GetString`, `TMP_Text.text`, or string concatenation. UTF-8 is decoded into a pooled `CharBufferPool.EncyclopediaLease` page and submitted with `TMP_Text.SetCharArray`.
- `CharBufferPool.EncyclopediaPageCapacity` is 32768 chars. Entries larger than the page stream as rolling windows: once the visible window drains, the decoded char count resets and the byte cursor continues from the same cached source span.
- Unlock state is `EncyclopediaStateDTO`: exactly 128 bytes in Vault buffer `(BufferID)70560`, with four raw `ulong` masks for 256 dense unlock bits plus AUP/revision metadata.
- Metadata, runtime state, CSV scratch, a Burst lookup result slot, a 64-byte typewriter DTO, H8LR mirror bytes, and a 300-frame telemetry ring are stored in adjacent Vault buffers `(BufferID)70561..70570`.
- Contract AUP values are copied immediately into owner-local `PdaAup48` primitive fields. The PDA runtime no longer names `Hecton8.World.AbsoluteUniversePosition` directly; distance math uses `HectonPhysicsContract.AupSectorSizeMetersInt` and clamp rails.
- Editor-time validation now checks all PDA transfer rows: 128-byte state, 128-byte runtime, 64-byte metadata, 64-byte telemetry, 64-byte typewriter, 48-byte local AUP copy, 16-byte H8LR header, and 16-byte H8LR record.
- Editor x-ray facades and CSV ingest bridges that can cold-bootstrap Vault state or perform file I/O, including `EditorTrySnapshot()`, `EditorTryWriteRawUtf8Hex()`, `TryIngestLoreMetadataCsvFromProject()`, and `TryIngestLoreMetadataCsv()`, are compiled only inside `#if UNITY_EDITOR`; they are not player/runtime API surfaces.

Signals:
- `ScanCompleteSignal` unlocks lore with precise AUP metadata.
- `LoreFragmentScannedSignal` unlocks by hash and reuses the last known discovery AUP when no precise position is present.

Scalability:
- `HomeostasisBrain.GlobalQualityWeight` continuously controls decode budget and typewriter reveal rate.
- `TypewriterTextJob` stores reveal accumulator/counts in Vault buffer `(BufferID)70569`; low quality resolves small visible increments, high/ultra quality pushes larger text chunks while keeping the same byte-source contract.
- Low hardware streams small chunks; high/ultra reveals larger chunks without changing the data contract.
- Discovery distance tokens subtract player AUP from stored discovery AUP through `PdaAup48`, clamp impossible sector deltas, cast the localized delta to `float3`, and format into the active `Span<char>`.

Telemetry:
- Runtime state flags encode active source at bits 8-9: `1 = H8LR`, `2 = Babel fallback`, `3 = Vault mock`. The black-box ring packs stream state in the low byte and source/canvas bits above it.
- `Data/Lore/Encyclopedia.h8bin` is now claimed by SHINOBU_130 through the narrow H8LR reader. The older Narrative `LoreMmfEncyclopedia` still expects H8LE and is not treated as an H8LR reader.

Failure evidence:
- On invalid UTF-8/fault detection the streamer dumps the fixed telemetry ring to both `Docs/AgentLogs/Dump_SHINOBU_130.bin` and `Docs/AgentLogs/Dump_PDA_STREAMER.bin`.
