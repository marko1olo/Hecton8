# PLATFORM_STEAM_DECK Rationale

Status: PENDING VERIFICATION

## Decision 1: Do Not Rewrite Data Archivist MMF Blindly

Problem: `SaveBinaryStorage`, crash telemetry, replay, lore and telemetry use `System.IO.MemoryMappedFiles` plus unsafe `AcquirePointer`. The user requested POSIX MMF, but this is a save-format and binary telemetry owner, not a simple path patch.

Solution: remove direct Win32 helpers first, add build/preflight blockers for MMF/unsafe sites, and document Linux player soak as mandatory. Keep binary layout stable.

Rejected Alternatives: replacing MMF with `FileStream` everywhere would risk save corruption, performance regressions, and unverified atomicity. Gating out MMF entirely would break black-box telemetry and persistence evidence.

Scalability potential: Low tier keeps existing fixed binary buffers; Middle/High can keep MMF windows and async export once Linux proof exists; Ultra can spend saved compatibility time on richer telemetry, not format churn.

Hardware Impact: direct microseconds saved are unmeasured. Main benefit is eliminating Win32-only calls and preventing false Linux readiness.

## Decision 2: Remove `kernel32.dll` Instead Of Keeping It Behind Guards

Problem: `RuntimeWatchdog`, `CrashTelemetryBuffer`, and `SaveBinaryStorage.AsyncWriteManager` contained direct `kernel32.dll` P/Invoke for file attribute/sparse-file helpers.

Solution: replace file length probing with managed `FileInfo.Length` and turn Windows sparse hint into a platform-neutral no-op. The file remains valid without sparse optimization.

Rejected Alternatives: keeping `#if UNITY_STANDALONE_WIN` satisfies compilation but violates the zero-Win32 portability goal. Implementing Linux `ioctl/fallocate` would add native surface and platform variance.

Scalability potential: Low tier avoids native API crossing; Middle/High can later add a PAL-backed sparse allocator if profiler proves disk layout is material.

Hardware Impact: likely neutral. Sparse hint removal may increase disk footprint for large sparse files; CPU impact is cold path only and PENDING MEASUREMENT.

## Decision 3: Native Plugin Parity Is A Blocker, Not A Hub Install

Problem: `liblz4.dll` and `HectonAudioKernel.dll` are Windows binaries. Linux/macOS/Android equivalents are absent from local file inventory.

Solution: flag missing `.so`/`.dylib` as blockers, extend native audio bridge to compile platform attempts when binaries exist, and keep fallback status as `PluginUnavailable` when absent.

Rejected Alternatives: assuming `DllImport("liblz4")` will find a system library on Steam Deck. Steam Deck builds must package exact plugin/importer metadata or use a verified managed/Burst fallback.

Scalability potential: Low tier can fall back to managed/procedural audio output; High/Ultra can use platform native kernels when binaries are provided.

Hardware Impact: native audio bridge change is cold/init only. LZ4 native parity impact is PENDING MEASUREMENT.

## Decision 4: Preflight Scanner Instead Of Manual Grep Report

Problem: Windows editor grep is not a CI gate and cannot catch future path/PInvoke/case regressions.

Solution: add editor build preflight plus Roslyn analyzer scaffold for path literals, P/Invoke, forbidden namespaces, case-sensitive assets, shader barriers, native plugin parity, and Steam Deck evidence.

Rejected Alternatives: docs-only audit. It would be stale immediately and does not protect Linux/Deck build attempts.

Scalability potential: Low tier benefits from hard fail before bad builds; High/Ultra can add stricter warnings without runtime cost.

Hardware Impact: editor-only. Runtime microseconds saved: 0.

## Decision 5: Fix Unity Import Blockers Before Trusting The Audit

Problem: Unity batch mode initially failed before the Steam Deck preflight method could run. `ScannableFragment` depended on `string.AsSpan()` extension resolution, and `HectonShaderVariantStripper` resolved `Environment` against the project namespace instead of `System.Environment`.

Solution: add a zero-allocation `H8DataHash.ComputeFnv1A32(string)` overload, use it in `ScannableFragment`, and qualify editor environment access as `global::System.Environment`.

Rejected Alternatives: adding broad `using System` patches everywhere or ignoring Unity batch failures because dotnet build passed. Unity importer is the authority for this project.

Scalability potential: Low/Middle/High/Ultra all get the same deterministic hash path without allocations. High-end systems do not need a different path.

Hardware Impact: runtime hash change is allocation-neutral and comparable instruction count. Estimated saving: 0 us; risk reduction is compile/runtime compatibility, not frame time.

## Decision 6: Editor Path Defects Are Real But Not Runtime Architecture

Problem: strict path scan found editor-only Windows path assumptions in Unity reload summary and SpaceEngine research validation.

Solution: use `Application.consoleLogPath` for Unity editor logs, resolve SpaceEngine root through `HECTON_SPACEENGINE_ROOT`, and assemble archive paths with `Path.Combine` segments.

Rejected Alternatives: suppressing these findings in the scanner. The code was cheap to fix and keeping Windows-only literals would hide future Linux editor validation defects.

Scalability potential: Low-tier/dev machines can configure external research paths without code changes; high-end research workstations can point the env var to larger local datasets.

Hardware Impact: editor-only. Runtime microseconds saved: 0.

## Decision 7: Keep Remaining MMF Blocks As Blocks

Problem: The earlier strict preflight had 28 MMF `AcquirePointer` blocker rows across save, replay, telemetry, crash dump, and lore encyclopedia code.

Solution: remove only low-risk pointer usage and leave deeper storage paths blocked pending Linux player soak, alignment audit, and mmap map-count budgeting. Removing Win32 helpers was safe; rewriting all MMF transport blindly would touch save integrity and black-box telemetry.

Rejected Alternatives: claiming POSIX readiness because `System.IO.MemoryMappedFiles` exists on .NET. Unity player, IL2CPP, Linux kernel limits, and unsafe pointer alignment still need device evidence.

Scalability potential: Low-tier fallback can later use bounded `FileStream` windows; Middle/High can keep MMF where profiled; Ultra can add larger telemetry windows after platform proof.

Hardware Impact: no runtime change in this pass. Estimated saved/added frame time: 0 us.

## Decision 8: Replace Low-Risk MMF Pointer Sites With Sequential IO

Problem: The first POSIX preflight still treated smoke corruption, replay export, lore index reads, global telemetry export, crash telemetry export, and save-file primary writes as unsafe MMF pointer risks. Several of those paths do not require a live mapped pointer to preserve binary format.

Solution: move cold/dev/export paths and `SaveBinaryStorage.WriteAll` to bounded `FileStream`/managed-view writes while preserving byte layout. Primary save writes now copy unmanaged segments through a fixed 64 KB static scratch buffer into a sequential file stream. Smoke corruption reads the 8-byte indexed-sector header with direct stream reads and flips one payload byte by position.

Rejected Alternatives: deleting binary telemetry or replacing the save format would fake portability and risk data loss. Rewriting the remaining read-window and sector-override mapped-pointer logic now was rejected because those paths are deeper hot/storage integrity code and need Linux save replay proof.

Scalability potential: Low tier gets fewer Linux `mmap`/alignment hazards and simpler sequential IO. Middle/High can still reintroduce profiled platform-specific mapping behind a PAL later if a real Deck/Linux profiler capture shows the sequential path is too slow. Ultra can spend the saved compatibility risk on richer telemetry only after platform proof.

Hardware Impact: frame-time impact is 0 us in normal gameplay unless a save/write/export is executing. Save write throughput may be slightly lower than MMF on Windows but is more predictable on Linux/Steam Deck. Estimated strict preflight improvement: 4 blocker rows removed in this continuation; total strict blockers reduced to 11.

## Decision 9: Keep `SaveBinaryStorage` Read Windows And Sector Override Mapped Until Device Proof

Problem: The latest preflight leaves 8 blocker rows in `SaveBinaryStorage`: cached read window acquire/release, read-only full-file mapping acquire/release, and sector override commit acquire/release. These are not isolated diagnostics; they feed indexed save reads and in-place sector override commit behavior.

Solution: mark them blocked by Linux player soak, alignment audit, and mmap budget verification instead of rewriting them under Windows-only evidence. The scanner now names the exact rows and keeps status `PENDING VERIFICATION`.

Rejected Alternatives: replacing those readers with a broad managed byte-array load would increase memory pressure and could hide large-save regressions on Steam Deck shared memory. A rushed stream rewrite could also break checksum, directory, and sector override atomicity.

Scalability potential: Low tier can later use bounded stream windows if profiling proves acceptable. Middle/High can keep MMF windows where POSIX/IL2CPP proof is clean. Ultra can raise cache windows only under platform memory policy.

Hardware Impact: no new runtime change. Risk is now explicit instead of hidden. Estimated microseconds saved: 0 us; estimated verification risk reduced by blocking unsupported claims.

## Decision 10: Remove Runtime MMF Instead Of Proving It On Windows

Problem: Steam Deck/Linux support cannot be claimed while first-party runtime code depends on `System.IO.MemoryMappedFiles`, view accessors, and `AcquirePointer`. Earlier caution kept `SaveBinaryStorage` MMF read windows and sector commit mapped because save integrity risk was real, but the user's current directive is maximum platform compatibility before dependency installs.

Solution: replace runtime MMF transport with portable file/native-buffer paths while preserving byte layouts. `SaveBinaryStorage` read-only mappings now use `NativeArray<byte>` snapshots; cached read windows are bounded 1 MB `NativeArray<byte>` pages; sector override commit edits a bounded native buffer and writes it back sequentially. `DodReplayRecorder`, `CrashTelemetryBuffer`, `LoreMmfEncyclopedia`, `DataArchaeologyRuntime`, `UserOptionsPersistence`, and the unused NativeArray extension no longer use `MemoryMappedFile` APIs.

Rejected Alternatives: keeping `#if UNITY_STANDALONE_WIN` MMF code would satisfy Windows and keep Linux suspicious. Loading save files into managed `byte[]` and pinning them was rejected because it would put large save snapshots into the GC heap. Replacing save format or deleting replay/telemetry evidence was rejected because it would hide correctness data.

Scalability potential: Low tier/Steam Deck gets fewer kernel mapping and pointer-alignment hazards. Middle/High can later reintroduce a profiled platform storage PAL if real Linux/Deck captures prove sequential/native-buffer IO is too slow. Ultra tier can spend recovered compatibility risk on richer replay/telemetry after device proof.

Hardware Impact: hot-frame impact remains 0 us for normal gameplay. Cold save/replay writes may trade Windows mmap throughput for predictable cross-platform `FileStream` chunks. Estimated microseconds saved per frame: 0; estimated blocker reduction: runtime MMF API blockers reduced from 8 strict rows to 0 by static scan.

## Decision 11: Native Binaries Remain The Honest Steam Deck Blocker

Problem: Removing MMF does not make Steam Deck ready. LZ4 and audio native plugins still have Windows binaries only, and Steamworks Linux runtime evidence is absent.

Solution: keep status `PENDING VERIFICATION`, keep native plugin parity as a blocker, and record that Unity Hub modules cannot produce `.so`/`.dylib` plugin parity or SteamInput behavior.

Rejected Alternatives: assuming `DllImport("liblz4")` will resolve on Steam Deck, or assuming Windows Steamworks/overlay behavior maps to Linux without `libsteam_api.so` and Steam client proof.

Scalability potential: Low tier can use managed/Burst fallback compression/audio only if implemented and measured. High/Ultra can use native paths per platform once binaries/importers are explicit.

Hardware Impact: no runtime frame gain. Risk reduction is architectural honesty: the remaining blockers are native binary/proof blockers, not hidden mmap calls.
