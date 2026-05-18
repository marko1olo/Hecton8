# Rationale_SHINOBU_69

Agent: SHINOBU_69  
Domain: Data Archivist / SaveSystem WAL RLE  
Status: POLISH PASS ACTIVE - DTO ACCESSORS PURGED; FALSE-SHARING HARDENED; STATIC VERIFIED; CORE BUILD VERIFICATION BLOCKED BY EXTERNAL NON-SHINOBU COMPILE WALL  

## Decision 00 - Prompt Disambiguation

Problem: `CURRENT_BATCH.md` contains duplicate `AGENT_PROMPT id="SHINOBU_69"` blocks. The user explicitly requested RLE/XXHash3/WAL save compression, while a later duplicate describes laser VFX.

Solution: Use the first SHINOBU_69 block whose role is `RLE_STATE_COMPRESSOR_AND_WAL_ARCHITECT`; ignore the later duplicate as neighboring batch contamination. This preserves strict parsing and matches the user's domain directive.

Rejected Alternatives: Executing the laser block would violate the user's SaveSystem request and domain boundary. Editing both domains would create cross-domain sabotage.

Scalability potential: Low tier avoids disk stalls through sparse deltas and cosmetic payload shedding; Middle keeps critical sector deltas; High retains more telemetry/debris state; Ultra can preserve richer non-critical state without changing the binary core.

Hardware Impact: On i3/MX350 with MicroSD, avoiding text saves and full-world writes targets millisecond-class autosave freezes; exact gain requires Unity/IO profiling.

## Decision 01 - Mandate Set

Problem: Save compression touches native memory, AUP authority, binary layout, threading, and crash recovery. Generic Unity serialization would be too slow and allocation-heavy.

Solution: Apply Save binary delta/checksum, AUP determinism, ARM64 struct layout, Zero-GC, Native Jobs, Execution Phases, Telemetry Blackbox, and compression-dictionary mandates.

Rejected Alternatives: JsonUtility/System.Text.Json/BinaryFormatter were rejected because they allocate, bloat payloads, and cannot safely hash unmanaged DataVault pages.

Scalability potential: Low drops cosmetic sectors by continuous weight; Middle writes critical gameplay plus limited world state; High/Ultra retain richer diagnostics and non-critical dehydrate snapshots.

Hardware Impact: Expected low-end gain comes from smaller WAL writes, fewer main-thread copies, and no text serialization. Current numbers are static estimates only.

## Decision 02 - WAL Payload Hash Authority

Problem: `H8BinaryWorldPager` already had RLE and CRC32, but CRC32 was doing two jobs: WAL corruption tail proof and raw payload identity. The prompt requires XXHash3 for WAL-save state identity while keeping crash recovery deterministic.

Solution: Add `PageFlagPayloadHashXxHash3` for newly written pages/WAL records. New payload identity uses `xxHash3.Hash64` folded to the existing 32-bit header slot; old pages without the flag still validate with CRC32. CRC32 remains the tail/hot-state corruption tripwire because Task 20 explicitly wants failed CRC32 detection after live WAL corruption.

Rejected Alternatives: Replacing every CRC32 reader unconditionally would corrupt compatibility with already-written pages. Expanding the 64-byte page/WAL headers would require a versioned disk migration outside this prompt.

Scalability potential: Low/Middle/High/Ultra all read the same compact header. Higher tiers can keep more WAL state, but hash validation cost stays bounded and branchless after the flag check.

Hardware Impact: XXHash3 is faster than bytewise CRC for payload identity on large sectors. Expected low-end gain is microsecond-class per 256KB page versus the old software CRC loop; exact value requires Unity Profiler on i3/MX350.

## Decision 03 - Continuous Quality Cosmetic Pruning

Problem: Cosmetic debris/state saves can inflate WAL writes during MicroSD autosaves. Existing pruning used only a static byte threshold, so it could not react to `GlobalQualityWeight`.

Solution: Add `ResolveRuntimeConfigForQuality(baseConfig, globalQualityWeight, systemStress01)`. The function continuously scales cosmetic drop threshold, LZ4 sub-block size, and WAL bytes-per-second from weak-device survival to visual overkill. It preserves the existing `CosmeticDeltaPayloadPruneJob` contract and avoids direct dependency on the world sampler implementation.

Rejected Alternatives: A binary low/high switch violates the scalability pillar. Pulling `GlobalWorldSampler` directly into SaveSystem would add a cross-domain dependency; callers can pass the sampled weight through the existing config path.

Scalability potential: Low tier sheds cosmetic records aggressively. Middle keeps small cosmetic deltas only when the WAL is cheap. High/Ultra retain richer non-critical state and spend the saved cycles on visual persistence.

Hardware Impact: On i3/MX350 + MicroSD, expected gain is reduced WAL bytes and fewer sub-block writes under thermal/load stress. Exact saved time depends on dirty cosmetic sectors.

## Decision 04 - AUP Local Offset Persistence

Problem: Raw absolute AUP doubles are too expensive and brittle for save deltas. The prompt explicitly requires translating AUP to local offsets before persistence.

Solution: Route `DearLieDehydrationJob` through `QuantizeAupForSave`, which now writes `SaveAupLocalOffset32`: 32-bit `SectorKey`, three millimeter-quantized 32-bit local offsets, shift id, flags, and explicit padding. The older half-offset helper remains available only as legacy compact helper surface.

Rejected Alternatives: Saving absolute `double3` per entity wastes bytes and amplifies precision noise in deltas. Storing camera-relative floats would not be authoritative after an origin shift.

Scalability potential: Low through Ultra use identical authoritative sector-key/local-float state. Higher tiers can layer cosmetic transform detail separately without changing the core AUP record.

Hardware Impact: Smaller AUP payloads reduce WAL bandwidth and hash input size; expected savings are proportional to entity count.

## Decision 05 - Dump Identity and Compile Wall

Problem: SHINOBU pager blackbox files still used stale SHINOBU_03 naming, and Merkle telemetry defaulted to `Dump_SAVE_MERKLE_TREE.bin` while the prompt requires `Dump_SAVE_SURGEON.bin`. Full solution compile also fails on missing RealtimeCSG plugin sources unrelated to this domain.

Solution: Rename SHINOBU save dumps to `Dump_SAVE_SURGEON.bin/.h8dump`, resolve Merkle default telemetry dumps into `Docs/AgentLogs`, and record a self-audit XML. Treat `RealtimeCSG.csproj` missing files as an external compile wall; verify `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` targets separately.

Rejected Alternatives: Leaving stale dump names would break crash-forensics routing. Editing or removing RealtimeCSG references would be cross-domain sabotage.

Scalability potential: Dump naming has no frame cost. Correct dump routing improves low-end diagnosis when MicroSD stalls or WAL corruption happens.

Hardware Impact: Normal path cost is zero. Fault-path dump cost is disk-bound and acceptable only on crash/corruption tripwire.

## Decision 06 - AUP Payload Precision Upgrade

Problem: The earlier dehydrated save payload used sector coordinates plus half-float local offsets. That is compact, but the new mandate explicitly calls for `SectorKey + float3 localOffset` before WAL save, not half-only authority.

Solution: Add `SaveAupLocalOffset32` as a 32-byte ARM64-safe DTO: `SectorKey`, `ShiftFrameId`, three millimeter-quantized local floats, flags, and two explicit padding fields. `DearLieDehydrationJob` now writes `MockStatePayload.LocalAup` through this DTO. `BinaryLayoutManifest` asserts every offset.

Rejected Alternatives: Raw absolute `double3` remains rejected because it bloats WAL and creates drift-prone deltas. Half-only offsets are retained only as legacy helper surface, not as the requested authoritative dehydrated mock payload.

Scalability potential: Low tier keeps the same 32B truth record and drops cosmetic records around it. Middle/High/Ultra can append richer presentation-only cosmetic state without changing authoritative save math.

Hardware Impact: On i3/MX350 + MicroSD, 32B local AUP avoids 24B double3 plus metadata expansion and keeps hash input cache-aligned. Estimated gain is about 50 us per 1000 dehydrated entities versus raw AUP serialization, IO gain depends on entity count.

## Decision 07 - Burst and Alias Contract Hardening

Problem: SaveSystem jobs had correct Burst math flags but did not force synchronous compile and did not tell Burst that source/destination/counter buffers are separate. That leaves vectorization and first-run compile behavior weaker than the mandate requires.

Solution: Add `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` to every SaveSystem Burst job and `[NoAlias]` to isolated `NativeArray` fields.

Rejected Alternatives: Trusting Burst defaults was rejected because the save loop has to be deterministic in startup behavior and not pay hidden JIT-style costs during first autosave. Interface arrays and virtual hot paths were not introduced.

Scalability potential: Low tier benefits from SIMD-friendly RLE/LZ4/compaction. High/Ultra keep richer save telemetry and cosmetic payloads while still using the same devirtualized job kernels.

Hardware Impact: Expected low-end gain is microsecond-class per compression/hash pass through better NEON/AVX alias assumptions; exact gain requires Burst Inspector and Quest/i3 profiling.

## Decision 08 - Compile-Wall Dependency Cut

Problem: `SaveDeltaCompression` pulled `Hecton8.Gameplay` only for `SuitUpgradeResolver.SupportedMask`. That is a direct domain dependency inside save compression and violates the compile-wall direction even though the current project is partly monolithic.

Solution: Remove the gameplay using and keep the wire-compatible supported mask as `0x7FFUL` inside SaveSystem. This preserves existing packed suit save bits 0..10 without forcing SaveSystem compression to compile against gameplay resolver code.

Rejected Alternatives: Routing through `GlobalRegistry.SuitUpgrades` would be worse: runtime service lookup in a binary codec and a wider dependency. Leaving the using in place would preserve compile-wall rot for no runtime value.

Scalability potential: No visual tier distinction; the win is architectural isolation. Low through Ultra all use the same stable binary mask.

Hardware Impact: Frame-time gain is effectively zero. Iteration-time gain is reduced recompilation blast radius once SaveSystem is split into a proper assembly.

## Decision 09 - Continuous WAL Budget

Problem: The previous WAL budget helper accepted a `bool slowMicroSdIo`. That is acceptable as a compatibility wrapper, but the core math must breathe with continuous pressure.

Solution: Add a float `microSdPressure01` overload and feed it through the same polynomial `SmoothUnit` curve used by `GlobalQualityWeight`. The bool overload now delegates to the continuous path.

Rejected Alternatives: A hard low-end switch was rejected because thermal pressure and MicroSD stalls vary continuously.

Scalability potential: Low/MX350 clamps toward 16 MiB/s and drops cosmetic records earlier; Middle ramps progressively; High/Ultra keep larger WAL budgets when IO is healthy.

Hardware Impact: Expected gain is fewer long autosave bursts on MicroSD. The helper itself costs under 1 us; saved time is IO-bound and proportional to avoided dirty cosmetic bytes.

## Decision 10 - Pager Command Queues Moved Into GlobalDataVault

Problem: `H8BinaryWorldPager` still owned private persistent `NativeQueue<PageWriteCommand>`, `NativeQueue<PageReadCommand>`, and `NativeParallelHashMap<uint, PageReadResult>`. That violated Vault Law and created a second allocator authority exactly in the MicroSD autosave path. Root `SaveData` also imported `Hecton8.Gameplay` only to read archaeology save-schema constants.

Solution: Replace the local containers with fixed vault-backed rings: `SaveWorldPagerWriteCommands`, `SaveWorldPagerReadCommands`, and `SaveWorldPagerReadResults`. The pager keeps only lightweight `VaultBufferHandle<T>` references and integer cursors; all command/result storage is owned by `GlobalDataVault`. SaveData now owns the persisted numeric schema capacities directly (`256` partial scans, `1024` scan states, `16` discovery words) instead of importing gameplay runtime.

Rejected Alternatives: Keeping NativeQueue was rejected because it hides persistent allocation ownership behind pager lifecycle. A managed `Queue<T>` was rejected because it allocates and would be worse than the violation. A cross-domain EventBus dependency was rejected because save paging already has a local worker contract. Keeping a gameplay import for two constants was rejected as compile-wall rot.

Scalability potential: Low/MX350 gets fixed-capacity command memory and predictable backpressure. Middle/High/Ultra can raise capacities through vault budgeting without changing the pager code shape.

Hardware Impact: Expected gain is not a huge per-frame number; it is allocator sovereignty and fewer hash-map/queue indirections under autosave pressure. Static estimate: 20-60 us saved during saturated enqueue/result bursts on weak CPUs, pending profiler proof.

## Decision 11 - Explicit Sector-Origin AUP and Survival-Band Math

Problem: The previous `SaveAupLocalOffset32` path wrote `SectorKey`, but the fallback origin was derived from absolute coordinates. That is acceptable for blind mocks, but not strong enough for the prompt's "relative to SectorKey origin" rule. The quality curve also used polynomial smoothing and `math.lerp`, but not `math.step` as requested by the polish mandate.

Solution: Add an explicit sector-origin overload and optional `DearLieDehydrationJob.SectorOriginMeters` input. When origins are present, save local offset is `absolute - sectorOrigin` before millimeter quantization; when the blind mock has no origin stream, the old derived-origin fallback remains. The config resolver now uses `math.step(0.3f, quality)` as a low-quality survival-band gate combined with `SmoothUnit` and `math.lerp`.

Rejected Alternatives: Saving raw absolute `double3` was rejected again for WAL size and precision drift. A binary low/high quality switch was rejected; the step only gates the low-quality cosmetic survival band, while the output still changes through a polynomial curve.

Scalability potential: Low drops decorative payloads harder below 0.3 and keeps critical AUP truth. Middle ramps smoothly. High/Ultra retain more cosmetic WAL payload while preserving the same authoritative local-offset record.

Hardware Impact: AUP math remains microsecond-class. The main low-end win is fewer cosmetic bytes written to MicroSD; exact stall reduction is IO-bound and must be measured on the target device.

## Decision 12 - Legacy Corruption Proof Stack Buffer

Problem: `SaveBinaryStorage` still allocated `new byte[IndexedSectorBlockHeaderSize]` while executing the indexed-sector corruption proof path. It is editor/smoke oriented, but it sits in the save corruption surface and contradicts the no-managed-scratch posture.

Solution: Replace the heap array with `stackalloc Span<byte>` and read the file header through the span slice overload. This keeps the corruption injector proof allocation-free without changing the binary format.

Rejected Alternatives: Leaving the heap allocation was rejected because proof tools are part of the save reliability surface. Moving this path into the WAL pager was rejected because indexed persistent-world sector corruption is a legacy storage concern, not a WAL command concern.

Scalability potential: No visual tier effect. It removes a cold managed allocation from a diagnostic path and keeps the save proof surface cleaner on weak machines.

Hardware Impact: Removes one 16-byte managed allocation per corruption proof invocation; frame-time effect is zero in normal gameplay.

## Decision 13 - Pager Counter False-Sharing Hardening

Problem: After moving pager queues/results into `GlobalDataVault`, `H8BinaryWorldPager` still kept many worker/main thread counters as adjacent scalar `int` fields. `Interlocked` and `Volatile` make the values coherent, but they do not prevent cache-line ping-pong when the MicroSD worker is appending WAL records while the main thread enqueues writes or samples telemetry. A separate issue was worse: `TryEnqueueWrite` still called `ApplyWalBackpressureIfNeeded`, which could execute `FileStream.Flush(true)` and `Thread.Sleep(1)` on the caller thread under WAL pressure.

Solution: Add a 64-byte explicit-layout `CacheLineInt` DTO and move hot pager counters into it: pending write/read/result counts, queue counts, telemetry cursor, dropped/error/completed counters, WAL replay/corruption counters, and queue high-watermark. All atomic and volatile operations now address the `.Value` field inside an isolated cache line. Remove main-thread WAL backpressure flushing from `TryEnqueueWrite`; enqueue now only copies to the vault ring and returns. The durable `Flush(true)` remains in the background worker WAL append path before world mutation. Touched primary WAL/AUP/master-hash DTOs also dropped `Pack = 8` while retaining explicit `Size` and manual padding.

Rejected Alternatives: Leaving adjacent scalar counters was rejected because the previous self-audit only proved no new parallel atomic array, not no worker/main false-sharing risk. Keeping `Flush(true)`/`Thread.Sleep(1)` in `TryEnqueueWrite` was rejected because it directly contradicts the MicroSD autosave freeze goal. A full rewrite to pointer-backed custom SPSC queues was rejected because the current rings are already vault-owned and protected by narrow locks; changing that now would widen the risk surface.

Scalability potential: Low/MX350 gets less cache-line invalidation during autosave pressure. Middle keeps the same fixed rings. High/Ultra can sample richer pager telemetry without forcing the writer counters to share cache lines.

Hardware Impact: Estimated gain is contention-dependent, roughly 5-25 us during saturated WAL enqueue/dequeue bursts on weak CPUs, plus removal of a possible 1ms+ caller-thread stall when WAL length exceeded the micro-stall threshold. Normal frames are expected to be unchanged; profiler proof is still absent.

## Decision 14 - Save DTO Accessor Purge

Problem: The primary SaveSystem DTOs were field-only, but the root save schema still had small flag properties on blittable `SaveData` DTOs (`ProceduralFaunaStateDTO`, `HibernatedFaunaStateDTO`) and readonly/get-only struct properties in the save codec/cloud helper surface. Those properties are cold compared with the WAL writer, but they are still method calls hidden behind struct member access and keep CS1612-style rot in the persistence contract.

Solution: Remove the flag properties from the DTOs and make `SaveBinaryPayloadCodec` read/write the public `flags` byte directly with the existing constants. Convert `ExternalScavengerSiteDTO.IsValid` and `PDAMarkerEntryDTO.HasAupPosition` to methods, updating the single PDA marker call site. Convert `SteamCloudSaveCandidate` and `SteamCloudSaveResolution` to readonly fields. In the same pass, remove duplicate archaeology and encrypted audio-log array allocations during binary read by reusing the bounded arrays already created by `SaveData.CreateNew()` and clearing them. Remove the stale `Hecton8.Gameplay` import from `SaveBinaryPayloadCodec`; legacy genetics masks already come from `Hecton8.World.GeneticTraitProfile`.

Rejected Alternatives: Leaving properties was rejected because the audit should not depend on "this is probably cold" as a justification. Adding bool fields was rejected because it would change binary layout. Rewriting all legacy managed SaveData arrays into NativeArrays was rejected because that is a broad legacy format migration outside the WAL/RLE hot path.

Scalability potential: Low/MX350 benefits from cleaner binary reads and fewer hidden struct calls during cold load/migration. Middle/High/Ultra keep the same save format while the WAL/Merkle path remains the real scalable save system.

Hardware Impact: Expected hot-frame gain is negligible because this is mostly cold codec/schema cleanup. It removes up to six duplicate managed array allocations per binary save read and avoids property method calls when serializing/deserializing fauna flags. Legacy item-genetics migration still allocates `byte[]` outputs because the current `SaveData` model stores those fields as managed arrays.

## Decision 15 - Reentry Hygiene and Gameplay Import Regression Cut

Problem: Active `SHINOBU_69` status/rationale files were overwritten by the later duplicate VFX prompt even though the current user request explicitly targets SaveSystem WAL/RLE compression. After restoring SaveSystem context, the static gate also found `SaveBinaryPayloadCodec.cs` had a stale `using Hecton8.Gameplay;`, recreating a compile-wall dependency in the persistence codec.

Solution: Archive the active VFX-contaminated files under `Docs/Archive/Batch009_Reentry_SHINOBU_69_VFX_Contamination_20260519_011534`, restore the SaveSystem audit files from `Docs/Archive/Batch009_Reentry_SHINOBU_69_SaveSystem`, re-extract the first role-qualified `RLE_STATE_COMPRESSOR_AND_WAL_ARCHITECT` prompt, and remove the stale gameplay import from `SaveBinaryPayloadCodec.cs`. Genetics masks remain resolved through `Hecton8.World.GeneticTraitProfile`.

Rejected Alternatives: Mixing VFX and SaveSystem logs was rejected because it destroys auditability under a duplicated prompt ID. Treating the second prompt as current truth was rejected because the user explicitly requested RLE/XXHash3/WAL saves. Running `dotnet build` was rejected in this pass because the user explicitly said not to launch it until needed and prior build attempts already hit unrelated external compile walls.

Scalability potential: Low/MX350 keeps the same reduced WAL/RLE path and cosmetic shedding; Middle/High/Ultra retain richer non-critical save payloads through the existing continuous `GlobalQualityWeight` gates. The reentry fix changes no runtime math; it protects the SaveSystem scope and compile wall.

Hardware Impact: Runtime frame-time impact is zero. Iteration-time impact is reduced dependency blast radius by removing a sibling gameplay import from the codec. The static gate confirms no text serializer or `Hecton8.Gameplay` dependency remains in the audited SaveSystem surface.
