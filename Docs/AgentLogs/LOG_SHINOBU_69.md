# LOG_SHINOBU_69

## 2026-05-18 Session Start

What was wrong: No SHINOBU_69 status/rationale/log files existed, and `CURRENT_BATCH.md` contains duplicate SHINOBU_69 prompts.

What was done: Initialized disk memory for the RLE/WAL SaveSystem prompt matching the user directive.

Cinematic Cheats used: Save stable/dehydrated state and critical sparse deltas instead of full live-object truth.

Exact Microseconds saved: PENDING VERIFICATION. Static estimate only until code path and profiling are verified.

## 2026-05-18 WAL/RLE State Compression Pass

What was wrong:
- Pager WAL used RLE and CRC32, but new WAL payload identity was not explicitly XXHash3-backed.
- Cosmetic save pruning was tied to static byte thresholds instead of a continuous `GlobalQualityWeight` input.
- Merkle/pager telemetry dump identity was stale: `Dump_SAVE_MERKLE_TREE.bin` / SHINOBU_03 names did not match `Dump_SAVE_SURGEON.bin`.
- AUP dehydration already stored sector-local data, but the job called the lower helper directly instead of the local save facade.
- Full solution compile is blocked by `RealtimeCSG.csproj` referencing 216 missing source files under `Assets/RealtimeCSG/...`.

What was done:
- Added `PageFlagPayloadHashXxHash3` to `H8BinaryWorldPager`; new page/WAL payload checks use `xxHash3.Hash64`, while legacy records without the flag still validate with CRC32.
- Kept CRC32 for WAL tail/hot-state corruption checks so the editor corruption injector still proves rollback on CRC failure.
- Added `SaveStateMerkleTree.ResolveRuntimeConfigForQuality(baseConfig, globalQualityWeight, systemStress01)` to continuously scale cosmetic retention, sub-block size, and WAL B/s.
- Routed `DearLieDehydrationJob` through `QuantizeAupForSave`, preserving sector + local half-offset persistence.
- Renamed default save forensic dumps to `Docs/AgentLogs/Dump_SAVE_SURGEON.bin` and `Dump_SAVE_SURGEON.h8dump`.
- Wrote `Docs/AgentLogs/SelfAudit_SHINOBU_69.xml`.

Cinematic Cheats used:
- Cosmetic debris/state is expendable visual state under low quality/stress instead of preserving every small transform.
- AUP is persisted as sector + local offset, not world-size doubles.
- RLE remains the cheap MicroSD path for byte-run sector payloads; no text or managed compression was introduced.

Exact Microseconds saved:
- XXHash3 vs bytewise software CRC32 for new 256KB payload identity: estimated 10-80 us/page on low-end CPU; must be profiled on target hardware.
- Cosmetic pruning under low `GlobalQualityWeight`: expected 100-2000 us avoided per autosave burst when debris/cosmetic sectors dominate.
- AUP sector-local packing: expected 1-5 us per 1000 saved entity states plus disk bytes avoided.
- Dump-name changes: 0 us normal path; cold fault-path only.

Verification:
- `rg` audit: no JsonUtility/BinaryFormatter/System.Text.Json in SaveSystem/SaveManager/SaveBinaryStorage target surface.
- `rg` audit: no `byte[]` allocations in `H8BinaryWorldPager`, `SaveStateMerkleTree`, or `SaveDeltaCompression`.
- `rg` audit: no get/set DTO properties in SaveSystem.
- `git diff --check` passed for touched SHINOBU files; only line-ending warning on existing H8BinaryWorldPager.
- `dotnet build Hecton8.Core.csproj --no-restore` passed with existing warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore` passed with existing warnings.
- `dotnet build Hecton8.slnx --no-restore` failed at `RealtimeCSG.csproj` because referenced plugin sources are missing; no SHINOBU compile error surfaced before that wall.

## 2026-05-18 Ultra-Think Polish Recheck

What was wrong:
- SaveSystem Burst jobs lacked `CompileSynchronously`.
- NativeArray aliasing contracts were implicit.
- Dehydrated AUP payload used the half-offset helper rather than the requested `SectorKey + float3 localOffset` WAL snapshot DTO.
- `SaveDeltaCompression` pulled `Hecton8.Gameplay` only for `SuitUpgradeResolver.SupportedMask`.
- The self-audit XML did not yet print the requested forensic sections.

What was done:
- Added 32B `SaveAupLocalOffset32` with millimeter-quantized local floats and moved `MockStatePayload` into the compiled SaveSystem codec surface.
- Changed `DearLieDehydrationJob` to persist sector-keyed local float offsets with flags.
- Added layout manifest checks for `SaveAupLocalOffset32` and `MockStatePayload`.
- Hardened every SaveSystem Burst job with `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`, and `[NoAlias]`.
- Added continuous `microSdPressure01` WAL budgeting and polynomial smoothing.
- Removed the direct gameplay dependency by preserving the wire-compatible `0x7FF` suit upgrade save mask locally.

Cinematic Cheats used:
- Far/rest entities serialize stable state plus `NeedsWake`, not full live transforms.
- Low `GlobalQualityWeight` sheds cosmetic records and lowers WAL pressure instead of preserving decorative noise.

Exact Microseconds saved:
- AUP local DTO: estimated 50 us per 1000 dehydrated entities versus raw AUP serialization.
- Burst alias hardening: microsecond-class per RLE/LZ4 pass pending Burst Inspector proof.
- MicroSD budget smoothing: saves IO stalls proportional to dropped cosmetic bytes; no exact runtime number claimed.

Verification:
- Static SaveSystem Burst scan clean: every SaveSystem Burst job has synchronous Fast/Standard directives.
- Static SaveSystem serializer scan clean for the patched surface: no JsonUtility/BinaryFormatter/System.Text.Json/new byte[] hot-loop additions.
- `dotnet build Hecton8.Core.csproj --no-restore` is currently blocked before SaveSystem diagnostics by unrelated `PlayerBuilder` Construction DTO errors, `LocRegistry math.reversebytes` errors, and `EconomyRuntimeInstaller TradeMarauderDirector` errors. No unrelated fix was attempted.

## 2026-05-18 Vault Law / AUP Origin Recheck

What was wrong:
- `H8BinaryWorldPager` still had private persistent `NativeQueue` and `NativeParallelHashMap` state in the autosave path.
- `SaveAupLocalOffset32` stored `SectorKey`, but the active helper could derive the origin from absolute coordinates instead of consuming the SectorKey origin stream.
- The continuous quality function used smoothing and lerp but did not yet include the mandated `math.step` low-quality collapse.
- Root `SaveData` imported `Hecton8.Gameplay` for numeric archaeology save-schema constants.
- Legacy indexed-sector corruption proof allocated a small managed `byte[]` header buffer.

What was done:
- Replaced pager write/read command queues and read-result map with fixed `GlobalDataVault` buffers: `SaveWorldPagerWriteCommands`, `SaveWorldPagerReadCommands`, `SaveWorldPagerReadResults`.
- Kept pager worker behavior but changed queue storage to vault-backed rings with explicit cursors and fixed capacities.
- Added `QuantizeAupLocalOffset32(absolute, sectorOrigin, sectorKey, ...)` and optional `DearLieDehydrationJob.SectorOriginMeters`; fallback origin derivation remains only for blind mocks.
- Added a `math.step(0.3f, quality)` survival band blended through `SmoothUnit` and `math.lerp` so low `GlobalQualityWeight` sheds cosmetic WAL data harder without touching critical state.
- Removed the gameplay import from `SaveData`; the persisted archaeology capacities and discovery word count now live as local save-schema constants.
- Replaced the corruption proof header heap array with `stackalloc Span<byte>`.
- Updated `SelfAudit_SHINOBU_69.xml` with pager command/result DTO layout and the expanded vault buffer list.

Cinematic Cheats used:
- Decorative save data is dropped under low quality/IO stress; critical AUP and sector state remain authoritative.
- Moving objects save AUP plus `NeedsWake`, not expensive mid-air cosmetic transform truth.

Exact Microseconds saved:
- Pager ring/vault path: static estimate 20-60 us during saturated command/result bursts versus queue/hash-map indirection; runtime proof pending.
- Sector-origin local AUP: same 32B WAL payload as before, but stronger correctness; no new runtime bloat.
- Cosmetic survival band: sub-1 us math cost, IO savings proportional to dropped cosmetic bytes on MicroSD.
- Legacy corruption proof stack buffer: removes one 16B managed allocation per invocation; normal path cost is 0 us.

Verification:
- Static `rg` gate: no `NativeQueue` or `NativeParallelHashMap` remains in `H8BinaryWorldPager`; existing `SaveBinaryStorage` cold `Allocator.Temp` lookup maps are unchanged and outside the pager persistent path.
- Static `rg` gate: patched save surface has no JsonUtility/BinaryFormatter/System.Text.Json, Pack=1, or `Hecton8.Gameplay` import.
- No dotnet build launched in this recheck because the user explicitly prohibited it unless strictly needed.

## 2026-05-18 False-Sharing / Layout Recheck

What was wrong:
- `H8BinaryWorldPager` had adjacent worker/main atomic counters after the vault migration. Correctness was guarded by `Interlocked`/`Volatile`; cache-line isolation was not.
- `TryEnqueueWrite` still reached a WAL pressure helper that could call `FileStream.Flush(true)` and `Thread.Sleep(1)` on the caller thread when the WAL exceeded 16MB.
- Touched primary WAL/AUP/master-hash DTO declarations still used `Pack = 8` even though explicit `Size` and padding fields already define the layout.

What was done:
- Added `[StructLayout(LayoutKind.Explicit, Size = 64)] CacheLineInt` with `FieldOffset(0)` and moved hot pager counters into it.
- Routed all `Interlocked`/`Volatile` accesses for those counters through `.Value`.
- Removed the caller-thread WAL backpressure flush/sleep from `TryEnqueueWrite`; durable WAL flush stays on the background worker before world-data mutation.
- Removed `Pack = 8` from `SaveAupLocalOffset32`, `MockStatePayload`, `PageWriteCommand`, `PageReadCommand`, `PageReadResult`, `PagerTelemetryEntry`, `SaveMasterHashV10Result`, and `SaveFileHeaderV10` while preserving explicit sizes.

Cinematic Cheats used:
- No new simulation. The active fake remains dehydrated local-AUP truth plus cosmetic WAL pruning under low `GlobalQualityWeight`.

Exact Microseconds saved:
- Estimated 5-25 us during saturated MicroSD WAL worker/main contention, plus removal of a possible 1ms+ caller-thread stall from `Flush(true)`/`Thread.Sleep(1)` under 16MB WAL pressure. Normal frame impact is expected to be 0 us. No profiler measurement was run.

Verification:
- `git diff --check` clean except CRLF warnings.
- Static `rg` gate: no pager `NativeQueue`/`NativeParallelHashMap`/`Allocator.Persistent`.
- Static `rg` gate: no JsonUtility/BinaryFormatter/System.Text.Json/new indexed-block byte array/Pack=1/Pack=4/Hecton8.Gameplay in the patched surface.
- Static `rg` gate: no unconverted padded counter references in `H8BinaryWorldPager`.
- No dotnet build launched.

## 2026-05-18 CS1612 / Managed Codec Hygiene Recheck

What was wrong:
- Root `SaveData` still exposed flag properties on serialized blittable DTOs, even though the WAL/Merkle DTOs were field-only.
- `SaveBinaryPayloadCodec` consumed those properties while packing/unpacking fauna state flags.
- Binary read recreated archaeology arrays immediately after `SaveData.CreateNew()` had already created bounded arrays.

What was done:
- Removed `ProceduralFaunaStateDTO.isLargeThreatZone`, `ProceduralFaunaStateDTO.blocked`, and `HibernatedFaunaStateDTO.isLargeThreat`; codec now reads/writes `flags` directly.
- Converted `ExternalScavengerSiteDTO.IsValid`, `PDAMarkerEntryDTO.HasAupPosition`, `SteamCloudSaveCandidate`, and `SteamCloudSaveResolution` away from property accessors.
- Updated the single PDA marker call site required by the `HasAupPosition()` method conversion.
- Replaced binary-read archaeology and encrypted audio-log array recreation with `SaveData.EnsureExactArrayCapacity` plus `Array.Clear`.
- Removed stale `Hecton8.Gameplay` import from `SaveBinaryPayloadCodec`; legacy genetics masks resolve through `Hecton8.World.GeneticTraitProfile`.

Cinematic Cheats used:
- No new simulation. The save lie remains stable/rest dehydration plus cosmetic pruning under low `GlobalQualityWeight`.

Exact Microseconds saved:
- Codec flag accessors: estimated 2-6 us per 10k fauna flag codec touches.
- Binary read archaeology/audio-log reset: removes up to six cold managed array allocations per save read.
- No frame-time gain claimed; WAL/RLE autosave freeze work remains the main win.

Verification:
- Static `rg` gate: no DTO property accessors remain in `SaveData` or `SaveBinaryPayloadCodec`.
- Static `rg` gate: SaveSystem still has service class convenience properties on `H8BinaryWorldPager`; no hot DTO properties were found.
- Static `rg` gate: no `Hecton8.Gameplay` import remains in the audited save codec surface.
- Static `rg` gate: remaining `new byte[]` hits in `SaveBinaryPayloadCodec` are legacy item-genetics migration outputs, not WAL/RLE hashing/saving loop allocations.
- `git diff --check` clean except CRLF warnings.
- No dotnet build launched per explicit instruction.

## 2026-05-19 Reentry Hygiene / Static Gate Recheck

What was wrong:
- Active `SHINOBU_69` audit files were overwritten by the later VFX duplicate prompt, while the current user request is explicitly SaveSystem WAL/RLE.
- The restored static gate found a stale `using Hecton8.Gameplay;` in `SaveBinaryPayloadCodec.cs`.

What was done:
- Archived the VFX-contaminated active files to `Docs/Archive/Batch009_Reentry_SHINOBU_69_VFX_Contamination_20260519_011534`.
- Restored SaveSystem `Status/Rationale/LOG/SelfAudit` from `Docs/Archive/Batch009_Reentry_SHINOBU_69_SaveSystem`.
- Re-extracted the first role-qualified `SHINOBU_69` prompt and confirmed 20 tasks.
- Removed the stale gameplay import from `SaveBinaryPayloadCodec.cs`.

Cinematic Cheats used:
- No new simulation. The active save fake remains stable/rest state dehydration plus `NeedsWake`, with cosmetic WAL payloads shed under low `GlobalQualityWeight`.

Exact Microseconds saved:
- Runtime: 0 us directly from this reentry hygiene pass.
- Iteration/build hygiene: compile-wall blast radius reduced by cutting one sibling gameplay import from the persistence codec.

Verification:
- `SelfAudit_SHINOBU_69.xml` parses as XML.
- Static `rg` gate: no `JsonUtility`, `BinaryFormatter`, `System.Text.Json`, `Pack=1`, `Pack=4`, or `Hecton8.Gameplay` in audited SaveSystem surface.
- Static `rg` gate: no `NativeQueue`, `NativeParallelHashMap`, or `Allocator.Persistent` in `H8BinaryWorldPager`.
- Remaining `new byte[]` hits are legacy item-genetics migration outputs and static/cold `SaveBinaryStorage` scratch arrays, not WAL/RLE hashing/autosave loops.
- `git diff --check` clean for the code change except CRLF warnings.
- No `dotnet build` launched per explicit user instruction.
