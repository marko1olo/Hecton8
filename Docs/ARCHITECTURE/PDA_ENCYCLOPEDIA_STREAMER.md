# PDA Encyclopedia Streamer

Owner: SHINOBU_130 / Echelon 8 Presentation & UX.

Runtime path:
- `PDAEncyclopediaStreamer` reads encyclopedia payloads as UTF-8 byte spans from `BabelDictionaryStore.GetUtf8()` first; when the MMF returns the `ERROR` sentinel or no baked dictionary exists, it falls back to a Vault-backed mock UTF-8 slab.
- The active UTF-8 source pointer is cached per entry after the first MMF/native lookup. Per-frame reveal work reuses the cached unmanaged pointer and byte length instead of re-querying Babel.
- The hot path does not call JSON, `Encoding.GetString`, `TMP_Text.text`, or string concatenation. UTF-8 is decoded into a pooled `CharBufferPool.EncyclopediaLease` page and submitted with `TMP_Text.SetCharArray`.
- Unlock state is `EncyclopediaStateDTO`: exactly 128 bytes in Vault buffer `(BufferID)70560`, with four raw `ulong` masks for 256 dense unlock bits plus AUP/revision metadata.
- Metadata, runtime state, CSV scratch, a Burst lookup result slot, a 64-byte typewriter DTO, and a 300-frame telemetry ring are stored in adjacent Vault buffers `(BufferID)70561..70569`.
- Contract AUP values are copied immediately into owner-local `PdaAup48` primitive fields. The PDA runtime no longer names `Hecton8.World.AbsoluteUniversePosition` directly; distance math uses `HectonPhysicsContract.AupSectorSizeMetersInt` and clamp rails.
- Editor-time validation now checks all PDA transfer rows: 128-byte state, 128-byte runtime, 64-byte metadata, 64-byte telemetry, 64-byte typewriter, and 48-byte local AUP copy.

Signals:
- `ScanCompleteSignal` unlocks lore with precise AUP metadata.
- `LoreFragmentScannedSignal` unlocks by hash and reuses the last known discovery AUP when no precise position is present.

Scalability:
- `HomeostasisBrain.GlobalQualityWeight` continuously controls decode budget and typewriter reveal rate.
- `TypewriterTextJob` stores reveal accumulator/counts in Vault buffer `(BufferID)70569`; low quality resolves small visible increments, high/ultra quality pushes larger text chunks while keeping the same byte-source contract.
- Low hardware streams small chunks; high/ultra reveals larger chunks without changing the data contract.
- Discovery distance tokens subtract player AUP from stored discovery AUP through `PdaAup48`, clamp impossible sector deltas, cast the localized delta to `float3`, and format into the active `Span<char>`.

Telemetry:
- Runtime state flags encode active source at bits 8-9: `1 = MMF/Babel`, `2 = Vault mock`. The black-box ring packs stream state in the low byte and source/canvas bits above it.
- `Data/Lore/Encyclopedia.h8bin` is not claimed by this streamer. The binary ledger classifies it as H8LR script/tool-only until a dedicated H8LR reader or converter exists.

Failure evidence:
- On invalid UTF-8/fault detection the streamer dumps the fixed telemetry ring to both `Docs/AgentLogs/Dump_SHINOBU_130.bin` and `Docs/AgentLogs/Dump_PDA_STREAMER.bin`.
