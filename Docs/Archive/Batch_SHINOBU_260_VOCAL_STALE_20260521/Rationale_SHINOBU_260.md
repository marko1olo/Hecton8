# Rationale_SHINOBU_260

Status: STATIC VERIFIED / H8VB SIDECAR VALIDATED / BUILD SKIPPED BY CPU GATE

## Decision 001: Runtime Voice Is Binary Stream, Not Unity Managed Clip Graph

Problem: SHINOBU_260 requires protagonist voice playback without heavyweight `AudioClip`, JSON manifests, string runtime lookups, or per-line GameObject/AudioSource instantiation.
Solution: Use an offline CSV-to-H8BIN baker, cold MMF/file load into native byte/index buffers, `VocalCueSignal` with 32-bit FNV-1a phrase hash, explicit 32-byte `VocalStateDTO`, and Burst-compatible ADPCM decode plus mathematical radio filter into the DSP buffer.
Rejected Alternatives: Unity `AudioClip` imports and AudioSource pooling are rejected because they keep managed asset graphs resident and do not satisfy the zero managed dialogue loading mandate. JSON manifests are rejected because runtime parsing allocates and violates the binary bridge law.
Scalability potential: Low uses ADPCM, sample stride, one-pole/bandpass approximation, and smaller decode cadence. Middle uses full ADPCM decode plus Dear Lie filter. High adds richer filter state and telemetry. Ultra can consume Vorbis-authored banks offline while runtime keeps ADPCM-compatible deterministic decode unless a proven native Vorbis decoder is integrated.
Hardware Impact: Estimated low-end i3/MX350 gain is 200-600 microseconds avoided per voice trigger versus AudioClip load/instantiation spikes, plus tens to hundreds of MB avoided for large dialogue sets.

## Decision 002: H8BIN Payload Uses Little-Endian Aligned Records

Problem: The vocal bank needs deterministic random access by phrase hash and stable ARM64-safe runtime metadata.
Solution: Define a compact file header and 32-byte aligned index records sorted by hash. Payload offset and byte length point into contiguous ADPCM/Vorbis payload bytes. Runtime copies index entries to aligned unmanaged structs before hot reads.
Rejected Alternatives: Text sidecars, ScriptableObject lookup tables, and dictionary keyed by string are rejected because they are either allocation-heavy, editor-only authoring facades, or create hot hash/string work.
Scalability potential: Low stores mono ADPCM at 22.05/24 kHz authored from 44.1 kHz masters. Middle stores 44.1 kHz ADPCM. High/Ultra can store higher-fidelity payload variants in the same format with quality weight selecting decode stride and filtering cost continuously.
Hardware Impact: Binary search over aligned records is cache-local; expected trigger lookup cost remains under 5 microseconds for thousands of lines on i3/MX350.

## Decision 003: Warning Voice Route Uses Hash Signal, Not Legacy Clip Handoff

Problem: `VocalWarningSystem` previously depended on serialized `AudioClip[]` bundles and `PlayerCriticalProceduralAudioRenderer.TrySubmitVocalWarningClip`, keeping a managed voice route alive inside the audio domain.
Solution: Route warning speech through `VocalCueSignal` with `VocalWarningHashes.FromWarningId`, continuous radio distortion scalar, priority inversion (`255 - warningId`), and estimated subtitle duration. The SHINOBU_260 decoder owns playback.
Rejected Alternatives: Keeping renderer PCM staging was rejected because it leaves a managed `AudioClip` dependency in the director/protagonist voice surface. Rebuilding the full warning UI/localization stack was rejected because subtitles already consume hash IDs.
Scalability potential: Low quality uses coarse ADPCM stride and stronger radio quantization. Middle keeps ADPCM stride 2-3. High/Ultra keep stride 1 and cleaner radio texture without changing hash identity or warning authority.
Hardware Impact: Expected i3/MX350 gain is 200-600 us per warning trigger by bypassing `AudioClip.GetData` and managed PCM scratch fill; memory residency drops by the removed warning clip table size.

## Decision 004: Mock Bank Generation Completes Only At Cold Boot

Problem: CI and empty workspaces need a valid voice bank even when XTTS/RVC models are absent, but hot-path job completion is forbidden.
Solution: `GenerateMockVocalBankJob` creates a deterministic H8ADPCM bank into Vault mock bytes during cold setup only. The single `JobHandle.Complete()` is outside gameplay/DSP and never in the frame decode path.
Rejected Alternatives: Creating a managed WAV/AudioClip fallback was rejected because it violates the runtime bank contract. Deferring mock generation into the first audio block was rejected because audio thread stalls are unacceptable.
Scalability potential: Low devices use the same mock ABI with ADPCM stride collapse. High/Ultra use the same bank route; only decoder quality/filter density changes.
Hardware Impact: Cold generation cost is amortized at boot. Runtime saves 0.2-0.6 ms per first cue versus constructing Unity clip data or retrying missing resources.

## Decision 005: Keep VocalStateDTO 32 Bytes And Move Payload ABI To Codec Row

Problem: The batch XML mandates `VocalStateDTO` exactly 32 bytes, but the decoder also needs payload offset, byte length, codec, quality, and radio state.
Solution: Keep `VocalStateDTO` as the exact 32-byte playhead row (`PhraseHashID`, `CurrentSampleIndex`, `TotalSamples`, speed, volume, flags, pad). Store file/codec/filter metadata in a separate 64-byte `VocalCodecStateDTO`.
Rejected Alternatives: Expanding `VocalStateDTO` to 64 bytes was rejected after rereading the XML because it violates the ABI mandate. Packing payload offset into the 32-byte row was rejected because it would delete required fields or create unaligned 64-bit data.
Scalability potential: Low/Middle/High/Ultra change only codec row scalars and decoder stride; the state DTO identity stays stable for tools and telemetry.
Hardware Impact: The 32-byte state fits half an L1 cache line and the 64-byte codec row isolates less frequently changed payload/filter fields, reducing audio-thread cache pressure on i3/MX350-class CPUs.

## Decision 006: Vorbis Is Packable But Not Claimed As Burst Runtime Playback

Problem: Python can pack Vorbis, but a real Vorbis decoder in Burst is non-trivial and cannot be truthfully claimed without a validated native decoder.
Solution: `voice_baker.py` supports Vorbis payload packing through `ffmpeg`; runtime accepts the ABI but rejects Vorbis records closed with `StateFlagVorbisUnsupported`. H8ADPCM is the profiled deterministic runtime route for this pass.
Rejected Alternatives: Fake Vorbis decode or managed `NVorbis`-style runtime decode was rejected because it violates zero-GC and evidence-based coding.
Scalability potential: Low through Ultra use ADPCM stride/filter scalability today. Future Ultra may add a native Vorbis backend behind the same record ABI without changing SignalBus or state DTOs.
Hardware Impact: Avoids unknown heap allocation and branch-heavy managed decode on mobile. Current ADPCM random-access block walk bounds decode to at most 64 deltas per emitted sample.

## Decision 007: Dear Lie Masks Synthesis Artifacts With Cheap DSP

Problem: AI voice artifacts need aesthetic masking without AudioMixer allocations or heavy physical radio simulation.
Solution: Use one-pole low state, band state, soft saturation, deterministic static, and quality-scaled quantization directly in the Burst decoder.
Rejected Alternatives: AudioMixer effect chains, convolution, and simulated radio propagation were rejected because they add managed graph dependencies or needless CPU/asset cost.
Scalability potential: Low uses stronger quantization and coarse sample stride. Middle softens the fake. High/Ultra reduce quantization, keep stride 1, and retain enough static to fit the deep-sea noir aesthetic.
Hardware Impact: Saves roughly 50-250 us per DSP block versus managed mixer routing/effect graph setup, with zero extra runtime assets.

## Decision 008: Move SHINOBU_260 Vault IDs Away From SHINOBU_160

Problem: The draft `71860..71869` BufferID lane collided with `SHINOBU_160` asynchronous telemetry exporter local IDs, which would make Vault ownership ambiguous and break one fact / one owner routing.
Solution: Move vocal synthesis to `72420..72429` in `H8Memory.cs`, update architecture docs, and document the rejected range. The runtime still resolves buffers through enum names, not numeric literals.
Rejected Alternatives: Keeping the collision and relying on owner IDs was rejected because BufferID is part of the route identity. Moving the telemetry exporter was rejected because it is an existing sibling owner outside SHINOBU_260 scope.
Scalability potential: Low/Middle/High/Ultra all keep the same buffer identities; quality changes only codec math, not ownership.
Hardware Impact: Prevents accidental aliasing of native buffers that would cause corrupt cache reads/writes. The gain is correctness rather than frame time; estimated avoided failure cost is unbounded because aliasing could poison telemetry and DSP state.

## Decision 009: CSV Is Cold Span Parse, Not Runtime String Split

Problem: Designers need CSV control over priority and radio distortion, but gameplay cannot allocate strings or parse rows on the audio thread.
Solution: Load `Docs/Audio/dialogue_script.csv` during cold/slow phases into Vault byte scratch, parse `ReadOnlySpan<byte>`, hash `StringID` with FNV-1a, and store sorted `VocalDialogueMetadataDTO` rows in Vault.
Rejected Alternatives: `string.Split`, `List<T>`, `Dictionary<string,...>`, ScriptableObject metadata tables, and JSON sidecars were rejected because they allocate or duplicate the binary owner route.
Scalability potential: Low devices parse at boot/slow tick only; High/Ultra can carry more metadata rows without changing the playback DTO ABI.
Hardware Impact: Avoids per-cue managed parse/hash allocations; expected saving is 3-12 microseconds per cue plus zero GC pressure in gameplay.

## Decision 010: Editor Waveform Overlay Reads Runtime State Without Owning Playback

Problem: Audio engineers need a live waveform and playhead scalar overlay, but editor tooling must not become an alternate playback owner.
Solution: `Digital Voice Forge` reads `VocalBankPlaybackRuntime.TryGetEditorWaveformSample` and `TryGetEditorState` from Vault-derived pointers and draws the UI Toolkit oscilloscope/editor labels. Runtime authority remains the SignalBus + Vault route.
Rejected Alternatives: Capturing a managed copy of the audio buffer or adding a second editor-side playback decoder was rejected because it creates duplicate truth and extra allocations.
Scalability potential: Low devices can ignore the editor view; High/Ultra authoring machines can display richer waveform telemetry without changing runtime route.
Hardware Impact: Editor-only overhead. Runtime DSP cost remains unchanged; no additional audio-thread allocation is introduced.

## Decision 011: Remove Legacy Vocal Warning Clip Submission API

Problem: `PlayerCriticalProceduralAudioRenderer.TrySubmitVocalWarningClip` remained as an unused public `AudioClip` ingestion route for warning voice, keeping a managed PCM staging API alive after `VocalWarningSystem` moved to `VocalCueSignal`.
Solution: Delete the unused public warning-clip submission method. Warning voice now enters playback through `VocalCueSignal` hash payloads only; the renderer's remaining authored clip paths are non-SHINOBU_260 procedural/ambient domains.
Rejected Alternatives: Leaving the method "unused" was rejected because public APIs become accidental dependencies for future agents and violate the clip-eradication proof surface. Removing unrelated music/SFX clip systems was rejected as outside the SHINOBU_260 domain.
Scalability potential: Low/Middle/High/Ultra all use the same signal route for voice warnings; quality changes decoder stride/filter density only.
Hardware Impact: Removes one possible 262144-float managed staging allocation path for vocal warnings and prevents `AudioClip.GetData` spikes on future warning playback.

## Decision 012: H8VB Must Validate As Its Own Sidecar, Not As Failed Data Monolith

Problem: `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` uses the SHINOBU_260 `H8VB` magic. A monolith-only validator can correctly reject it as non-H8DM but still poison CI with a false foreign-schema failure.
Solution: Add a source-backed H8VB route to `Tools/h8bin_validator.py` before H8DM parsing. It checks 64-byte header ABI, 32-byte sorted records, FNV bank hash over records plus payload, 16-byte payload alignment, payload bounds, mono/sample-rate lanes, supported runtime codecs, and H8ADPCM block headers. Unknown foreign magics still fail closed.
Rejected Alternatives: Renaming the vocal bank to avoid `.h8bin` was rejected because the binary sidecar is intentional. Pretending H8VB is a Data Monolith section was rejected because it would mix owners and corrupt schema proof. Suppressing all foreign `.h8bin` errors was rejected because it would hide real unowned payloads.
Scalability potential: Low/Middle/High/Ultra devices all consume the same H8VB identity; quality changes decoder stride/filter density only. The validator never changes gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: Runtime frame impact is zero. CI/integration gain is concrete: `vocal_banks.h8bin` now reports `H8VB_SCHEMA_VALIDATED` while the remaining current gate failure is the unrelated missing `static_data.h8bin`.

## Decision 013: H8VB Payload Contiguity Includes Explicit Alignment Padding

Problem: `voice_baker.py` correctly aligns every payload start to 16 bytes, but the sidecar validator initially advanced the next expected offset by `byte_length` only. Any production bank with a non-16-byte payload length would falsely fail as non-contiguous.
Solution: Advance H8VB record cursors through `align16(byte_offset + byte_length)`, verify the padding range stays inside the payload, and fail if any padding byte is non-zero. Add a two-record ADPCM regression where the first payload is 36 bytes and the second begins after 12 bytes of padding.
Rejected Alternatives: Removing inter-record alignment was rejected because ARM64-safe binary payload reads need predictable aligned starts. Allowing arbitrary padding was rejected because it would hide corrupted bytes inside the signed bank payload.
Scalability potential: Low/Middle/High/Ultra all keep the same binary identity; payload alignment only affects cache safety and CI proof, not runtime quality policy.
Hardware Impact: Runtime cost is 0 us. CI avoids false production-bank failure after large XTTS batches; expected validator overhead is a bounded padding-byte scan.

## Decision 014: Remove Broad Vault MemClear From Vocal Setup

Problem: The runtime requested SHINOBU_260 Vault buffers with `NativeArrayOptions.UninitializedMemory` and then bulk-cleared state, telemetry, waveform, mock bank bytes, mock records, CSV metadata, and CSV scratch with `UnsafeUtility.MemClear`.
Solution: Initialize only the state row, codec row, counters row, first debug slots, CSV metadata count, and deterministic telemetry rows. The mock generator writes explicit header/record fields instead of relying on a prior full-bank clear.
Rejected Alternatives: Keeping broad `MemClear` was rejected because it weakens the ZeroInit-bypass proof and wastes cold boot cycles. Leaving telemetry fully uninitialized was rejected because early blackbox dumps need deterministic rows.
Scalability potential: Low devices save cold boot memory bandwidth. Middle/High/Ultra keep identical buffer ownership and runtime DSP behavior.
Hardware Impact: Cold-path saving is roughly 200-500 microseconds for current buffer sizes on i3/MX350-class hardware. Hot-path DSP remains zero-GC and does not clear a decoded output buffer.

## Decision 015: Listener Fallback Must Mix, Not Own The Master Buffer

Problem: `OnAudioFilterRead` can be attached to an `AudioListener` fallback to satisfy the prompt without allocating a new AudioSource GameObject. A decoder that always overwrites the callback buffer would mute the entire project mix in that attachment mode.
Solution: Add an explicit mix flag to the Burst decode function pointer. Listener fallback mode adds voice samples to the existing buffer and does not silence idle/fault blocks. Dedicated source-driver mode can still overwrite its own host buffer.
Rejected Alternatives: Always-overwrite was rejected because it is only valid for a dedicated source buffer. Creating a new runtime AudioSource/driver clip was rejected because SHINOBU_260's prompt forbids AudioClip/AudioSource instantiation for the director voice route. Rewriting to DSPGraph was rejected for this pass because the XML specifically requires `OnAudioFilterRead`; it remains the future production route.
Scalability potential: Low/Middle/High/Ultra keep the same route; quality weight only changes stride/filter density. High/Ultra can later swap the callback seam for DSPGraph without changing H8VB or `VocalCueSignal`.
Hardware Impact: CPU delta is negligible; the gain is correctness. It prevents full-mix loss and avoids a fallback AudioSource/AudioClip allocation path.

## Decision 016: Vorbis Is Explicitly Archival Until A Native Decoder Exists

Problem: The baker can generate Vorbis payloads, but the current runtime decoder intentionally supports only PCM16/H8ADPCM.
Solution: Keep Vorbis in the file ABI but require `--allow-runtime-unsupported-vorbis` for authoring and remove Vorbis from the default Editor codec options.
Rejected Alternatives: Claiming Vorbis playback was rejected as false evidence. Managed Vorbis decode was rejected because it violates zero-GC hot-path policy.
Scalability potential: Low through Ultra use H8ADPCM now. Future Ultra may add native Vorbis behind the same ABI.
Hardware Impact: Prevents accidental runtime failure and managed decoder pressure on i3/MX350/mobile silicon.

## Decision 017: MMF Release Requires Audio-Callback Fence

Problem: Hot reload or teardown can release a `MemoryMappedViewAccessor` while `OnAudioFilterRead` still holds the raw bank pointer.
Solution: Guard callback entry with `_bankReleaseInProgress`, track `_audioCallbackInFlight`, clear bank pointers before waiting, and release the MMF only after the in-flight count reaches zero.
Rejected Alternatives: Trusting Unity callback order was rejected because audio and main thread scheduling are not a proof. Locking from the audio callback was rejected because locks can cause dropouts.
Scalability potential: All tiers share the same safety route; quality policy is unaffected.
Hardware Impact: Avoids rare access violations and undefined reads during hot reload/shutdown; steady-state cost is one interlocked increment/decrement per callback.

## Decision 018: Runtime CSV Polling Is Editor-Only After Cold Boot

Problem: The first pass parsed CSV metadata on every `SlowTick`, which is designer-friendly but undesirable in player runtime.
Solution: Keep cold boot CSV parse for Task 17 proof, but wrap repeated `SlowTick` reload in `UNITY_EDITOR`. Production playback uses the bank record metadata plus the boot-loaded Vault table.
Rejected Alternatives: Polling the source CSV indefinitely was rejected because it keeps runtime file I/O alive. Removing the cold parser was rejected because Task 17 explicitly requires the C# side to parse the same CSV during cold boot.
Scalability potential: Low devices avoid repeated source-file reads. High/Ultra authoring machines keep editor hot-reload convenience.
Hardware Impact: Removes recurring slow-tick file I/O from player runtime; avoids unpredictable disk stalls.

## Decision 019: Current Verification Is Static Source, Not Runtime Proof

Problem: CPU sampled at 100 percent and project policy forbids `dotnet build` above 50 percent CPU or while compiler processes run.
Solution: Run Python syntax/tests/baker/scanner/H8VB validator and skip build under the explicit gate. Record unrelated validator failures separately.
Rejected Alternatives: Launching `dotnet build` anyway was rejected because it violates the user's command and AGENTS build gate.
Scalability potential: No runtime effect.
Hardware Impact: Preserves developer hardware and avoids compile-wall contention. Unity import/profiler/GCMonitor/player proof remains pending.

## Decision 020: DSP Playhead Uses Direct Ref State Mutation

Problem: The Burst decoder still used local `VocalStateDTO` and `VocalCodecStateDTO` copies before writing them back at the end of the callback. This is not a managed allocation, but it leaves Task 03's direct-memory mutation proof weaker than required.
Solution: Bind the incoming state pointer with `VocalStateDTO.AsRef(state)` and bind the codec pointer with `UnsafeUtility.AsRef<VocalCodecStateDTO>(codec)`. The decoder now mutates `CurrentSampleIndex`, `SourcePosition`, flags, filter state, and codec predictor fields through refs in the hot loop.
Rejected Alternatives: Keeping local copies was rejected because it looks like the same hidden-copy pattern the CS1612 mandate is meant to eliminate. Direct pointer arithmetic for every field was rejected because typed refs preserve explicit DTO field layout while staying Burst-compatible.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Quality still controls stride/filter density continuously and does not change the state ABI.
Hardware Impact: Expected steady-state gain is small, roughly 1-5 microseconds per active callback by removing the copy-back pair and reducing ambiguity for Burst alias analysis. Main impact is structural proof for the DSP hot path.

## Decision 021: Remove Dead Renderer VWS PCM Branch, Keep Metal Grain Scratch

Problem: Sub-agent audit found `PlayerCriticalProceduralAudioRenderer` still carried dead vocal-warning PCM playback state: VWS double buffers, pending buffer indices, a per-sample renderer mix hook, and a VWS radio degradation clone. No producer wrote those buffers anymore, but the path still weakened Task 01 proof and forced extra Vault buffer checks.
Solution: Remove the VWS PCM renderer branch entirely and keep warning voice ownership in `VocalWarningSystem -> VocalCueSignal -> VocalBankPlaybackRuntime`. Rename the remaining managed scratch from `_vwsClipManagedScratch` to `_metalStressClipManagedScratch` because it only feeds cold authored metal stress grain import.
Rejected Alternatives: Deleting all authored `AudioClip` use in `PlayerCriticalProceduralAudioRenderer` was rejected because hull/boiling/metal-grain SFX are outside SHINOBU_260 director/protagonist voice ownership. Leaving the dead VWS PCM lane was rejected because dead public-ish audio paths become future managed-dialogue regressions.
Scalability potential: Low/Middle/High/Ultra voice route is unchanged and remains H8VB/ADPCM with continuous quality stride. The procedural critical audio renderer no longer carries a duplicate warning-voice quality path.
Hardware Impact: Avoids resolving/clearing two 262144-float VWS Vault buffers, removes dead per-frame VWS readiness checks, and removes one per-sample `RenderVocalWarningSample` call from the critical audio mix loop. Estimated saving is roughly 2-20 microseconds per active audio block plus about 2 MiB less Vault pressure for the removed double buffer lane.
