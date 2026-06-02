# PDA Encyclopedia Streamer

Owner: `PDAEncyclopediaStreamer` / Echelon 8 Presentation & UX.

Source anchors: `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`, `Assets/_Project/Scripts/UI/Editor/PDAEncyclopediaTunerWindow.cs`.

Runtime path:

- Primary payload route:
  - Owner: `PDAEncyclopediaStreamer`.
  - Binary source: `Data/Lore/Encyclopedia.h8bin`.
  - First reader: owner-local `PdaH8lrLoreStore`.
  - Editor/Standalone: H8LR memory-maps the file.
  - No-MMF platforms: Vault `(BufferID)70570` stores the 8 MiB native mirror.
  - Mirror proof: `VaultGenerationHandle<byte>`.
  - Read view: phase-local `NativeArray<byte>`.
  - Forbidden state: persistent bare Vault pointer without generation proof.

- `PdaH8lrLoreStore.TryGetUtf8()` is a pure span lookup. It no longer mutates last-depth/key/prefetch counters; any tracked lookup evidence must stay in explicit telemetry routes.

- `BabelDictionaryStore.FetchUtf8()` remains a pure legacy fallback after H8LR.
- Telemetry and linked-audio publish only from explicit `TrackUtf8Lookup()` owner-phase calls.
- PDA streamer uses the pure span path.
- Deterministic Vault mock slab is final CI/editor fallback when no real binary source resolves.

- Active UTF-8 source pointer is cached per entry after first H8LR/Babel/Vault lookup.
- Per-frame reveal reuses cached unmanaged pointer and byte length instead of re-querying source indexes.

- The hot path does not call JSON, `Encoding.GetString`, `TMP_Text.text`, or string concatenation. UTF-8 is decoded into a pooled `CharBufferPool.EncyclopediaLease` page and submitted with `TMP_Text.SetCharArray`.

- `CharBufferPool.EncyclopediaPageCapacity` is `32768` chars.
- Larger entries stream as rolling windows.
- After visible window drain, decoded char count resets and byte cursor continues from same cached source span.

- Unlock state is `EncyclopediaStateDTO`: exactly 128 bytes in Vault buffer `(BufferID)70560`, with four raw `ulong` masks for 256 dense unlock bits plus AUP/revision metadata.

- Adjacent Vault buffers `(BufferID)70561..70570`: metadata, runtime state, CSV scratch, Burst lookup slot, 64-byte typewriter DTO, H8LR mirror bytes, telemetry ring.

- Contract AUP values are copied immediately into owner-local `PdaAup48` primitive fields. The PDA runtime no longer names `Hecton8.World.AbsoluteUniversePosition` directly; distance math uses `HectonPhysicsContract.AupSectorSizeMetersInt` and clamp rails.

- Editor-time validation checks PDA transfer rows: 128-byte state/runtime, 64-byte metadata/telemetry/typewriter, 48-byte local AUP copy, 16-byte H8LR header/record.

- Editor x-ray facades and CSV ingest bridges compile only inside `#if UNITY_EDITOR`.
- Examples: `EditorTrySnapshot()`, `EditorTryWriteRawUtf8Hex()`, `TryIngestLoreMetadataCsvFromProject()`, `TryIngestLoreMetadataCsv()`.
- They are not player/runtime APIs.

Signals:

- `ScanCompleteSignal` unlocks lore with precise AUP metadata.

- `LoreFragmentScannedSignal` unlocks by hash and carries scanner/applied-lore AUP when `FlagHasAup` is set. Hash-only legacy producers must clear `FlagHasAup`; PDA falls back to last known discovery AUP only for those legacy payloads.
- When `FlagPairedScanComplete` is set and the same snapshot already contains a matching `ScanCompleteSignal`, PDA skips the lore-fragment unlock to avoid duplicate state writes. Other consumers still receive both unmanaged signal views.

Scalability:

- `HomeostasisBrain.GlobalQualityWeight` continuously controls decode budget and typewriter reveal rate.

- `TypewriterTextJob` stores reveal accumulator/counts in Vault buffer `(BufferID)70569`; low quality resolves small visible increments, high/ultra quality pushes larger text chunks while keeping the same byte-source contract.

- Low hardware streams small chunks; high/ultra reveals larger chunks without changing the data contract.

- Discovery distance tokens subtract player AUP from stored discovery AUP via `PdaAup48`, clamp impossible deltas, cast to `float3`, and format into active `Span<char>`.

Telemetry:

- Runtime state flags encode active source at bits `8..10`.
- Values: `1 = H8LR`, `2 = Babel fallback`, `3 = Vault mock`, `4 = Data Monolith AppliedLore`.
- Black-box ring packs stream state in low byte and source/canvas bits above.

- `Data/Lore/Encyclopedia.h8bin` is claimed by `PDAEncyclopediaStreamer` through the narrow H8LR reader. The older Narrative `LoreMmfEncyclopedia` still expects H8LE and is not treated as an H8LR reader.

Failure evidence:

- On invalid UTF-8/fault detection the streamer dumps the fixed telemetry ring to `Docs/AgentLogs/Dump_PDAEncyclopediaStreamer_BlackBox.bin`.
