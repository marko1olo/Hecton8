# Rationale_1708

Status: PENDING REBAKE AND FINAL COMPILE - SOURCE CSV COHERENT AT 6900 ROWS; CURRENT static_data.h8bin STALE AGAINST SOURCE AFTER CONCURRENT SOURCE-DATA DRIFT; CLI PROBE DRIFT PATCHED; REBAKE/CLI EXIT-0 AND PROJECT BUILD BLOCKED BY CPU/UNITY ROSLYN GATE

## Decision Records

### Decision 01 - 56-byte header preservation with 16-bit dialogue mask

Problem: Prompt requested a 16-bit dialogue mask in the 0x000B 56-byte header, but the live SaveFileHeader already uses all 56 bytes for indexed-sector save metadata and hashes.
Solution: Preserve the explicit 56-byte layout and encode `PlayerDialogueChoiceFlags` from the packed quest narrative band into the high 16 bits of existing `DeltaCount`; decode packed quest word count from the low 16 bits for v0x000B+.
Rejected Alternatives: Replacing the header sketch would corrupt indexed-sector offsets and invalidate existing checksum/header hash logic. Adding a new field would break the fixed header size.
Scalability potential: Low tier reads one `ushort` summary; middle/high/ultra can use the same flag mask for richer terminal material response without loading quest payload.
Hardware Impact: i3/MX350 avoids metadata quest payload parse for dialogue summary; estimated cold metadata path saving 50-200 us per slot preview depending on save size. Runtime hot path cost is one cached ushort read.

### Decision 02 - 64 KB save write paging

Problem: Native writes used 1 MB segments while task demanded 64 KB Save Paging Protocol.
Solution: Bound native write segments to OS allocation granularity, 64 KB, for Windows and Unix write loops.
Rejected Alternatives: Managed FileStream paging was rejected because it would add managed object paths and duplicate native writer authority. Frame-by-frame main-thread paging was rejected because the current save writer already runs in the async/background pipeline and main-thread yield must stay in SaveManager snapshot phases.
Scalability potential: Low tier gets shorter blocking kernel write spans; high/ultra retain throughput through the same native sequential writer.
Hardware Impact: i3/MX350 reduces individual native write burst from 1048576 bytes to 65536 bytes. Estimated stall granularity reduced by 16x; total disk time depends on storage device and is not claimed as eliminated.

### Decision 03 - SHINOBU_329 save-buffer AUP reconciliation

Problem: Safe AUP snap teleported the player transform, but `SaveData.playerStats` still held the pre-snap coordinate before optional primary self-repair.
Solution: After safe teleport, rewrite `playerStats` position/rotation/velocity and refresh `PlayerKinematicStateDTO` in the loaded save data before self-repair can write artifacts.
Rejected Alternatives: Scene reload/world reset was rejected because route card demands no world reset. Transform-only teleport was rejected because repaired primary saves would keep the bad coordinate.
Scalability potential: Low/mid/high/ultra all execute one DTO rewrite; no tier-specific gameplay truth changes.
Hardware Impact: i3/MX350 cost is four struct setter writes and one 48-byte DTO mirror rebuild, estimated under 5 us in main-thread load hydrate.

### Decision 04 - TerminalOS zero-GC dialogue visual sync

Problem: Terminal presentation needs dialogue-choice color response without runtime text/json loaders or quest payload parsing.
Solution: Cache SaveManager during GlobalRegistry cold injection/hot-swap, read `PlayerDialogueChoiceFlags`, map bit 0 with `math.select`, and upload `_TerminalOsStyle*` shader properties only when flags/blend changed.
Rejected Alternatives: Runtime file parse, string-keyed decision lookup, and per-frame GlobalRegistry polling were rejected. A Burst job was rejected because 16-bit bit counting is too small for scheduling overhead.
Scalability potential: Low tier snaps blend immediately; middle/high/ultra use continuous GlobalQualityWeight to slow/polish the color transition.
Hardware Impact: i3/MX350 steady-state is one ushort read, one bit test, and no shader upload when unchanged; changed state uploads three globals. Estimated unchanged-frame overhead under 10 us.

### Decision 05 - Data monolith boundary

Problem: Prompt referenced localization/lore migration to static_data.h8bin; target file had editor CSV probes and existing applied-lore span resolver.
Solution: Keep editor-only `Assets/_SourceData` CSV hot reloaders, verify runtime applied-lore lookup goes through H8AppliedLoreRuntime/static_data.h8bin.
Rejected Alternatives: Removing editor probes would degrade authoring but not improve runtime. Reintroducing StreamingAssets text fallback was rejected.
Scalability potential: Low tier avoids runtime text IO; high/ultra can consume richer monolith packets with same pointer route.
Hardware Impact: i3/MX350 avoids managed text loader allocations in production. No new DataVault ownership or buffer growth.

### Decision 06 - Verification gate

Problem: Project forbids launching dotnet build while CPU exceeds 50% or dotnet/csc is already running.
Solution: Checked CPU and processes; final gate reported CPU 100% and active dotnet processes. Build not launched. Static `git diff --check` and rg sweeps performed.
Rejected Alternatives: Launching another build would violate active project rule and risk compile contention with another agent.
Scalability potential: Not a runtime feature; preserves shared multi-agent workstation throughput.
Hardware Impact: Prevented additional compiler pressure on active hardware. Compile status remains PENDING VERIFICATION.

### Decision 07 - Header dialogue mask load preservation

Problem: Direct dialogue decisions can exist only in SaveFileHeader.DeltaCount high 16 bits; loading from packed quest words alone would erase those bits after critical backup promotion or primary self-repair.
Solution: Return a `ushort playerDialogueChoiceFlags` from SaveBinaryStorage load, TryLoadCandidate, critical backup promotion, self-repair, and static repair. Merge it with the packed narrative-band flags before hydration and before normalized save rewrites.
Rejected Alternatives: Re-reading metadata after load was rejected because it duplicates mapped-header work. Mirroring direct flags into quest words during load was rejected because quest-state ownership belongs to QuestStateManager.
Scalability potential: Low tier preserves one 16-bit state without loading dialogue JSON; middle/high/ultra can layer richer presentation off the same mask without changing save identity.
Hardware Impact: i3/MX350 cost is one `ushort` out parameter and one OR in cold load/repair paths. Estimated under 1 us and zero steady-state allocation.

### Decision 08 - TerminalOS editor-only CSV boundary

Problem: TerminalOS retained `_SourceData` CSV path/timestamp/probe state even though the file readers were already compiled out of player builds.
Solution: Keep the span parser and file hot-reload for `UNITY_EDITOR` authoring only, and gate the serialized CSV paths plus probe/timestamp fields behind `UNITY_EDITOR`; player cold-path setup now uses `_coldPathsReady` and does not resolve CSV paths.
Rejected Alternatives: Deleting the parser was rejected because editor live-tuning is useful and not a runtime text dependency. Loading terminal layout from StreamingAssets or JSON was rejected because applied lore/localized terminal text must route through `static_data.h8bin`.
Scalability potential: Low tier and player builds carry no CSV path/probe state; middle/high/ultra editor workflows keep fast authoring iteration without changing runtime truth.
Hardware Impact: i3/MX350 player startup avoids two path combines and CSV timestamp fields in TerminalOS cold init; estimated saving is under 20 us cold but removes the runtime text-loader surface entirely.

### Decision 09 - Terminal unlock save-bit bridge

Problem: `RecordPlayerDialogueChoiceFlag()` existed, but no runtime owner wrote bit 0 when a terminal decryption choice was actually solved; consuming `TerminalUnlockedSignal` from SaveManager would steal the event from other systems.
Solution: In `TerminalOsRuntime` owner finalize, after the decryption job is complete, scan the Vault-owned `DecryptionPuzzleDTO` rows only while `PlayerDialogueChoiceFlags` lacks bit 0. On first solved puzzle, OR `DialogueDecisionSaveFacilityMask` into SaveManager and force the same LateFrameTick shader style sync.
Rejected Alternatives: SignalBus consumption in SaveManager was rejected because `TryConsumeFrame` is not a multicast read. Writing the bit inside the Burst job was rejected because SaveManager is managed save authority and cannot be touched from Burst.
Scalability potential: Low tier scans at most terminal capacity until one solved terminal is recorded; middle/high/ultra get same deterministic bit and can layer richer GPU presentation on it.
Hardware Impact: i3/MX350 post-solve cost is one bounded DTO scan during owner finalize, then zero future scan due the flag guard. Estimated under 20 us for 64 terminals and 0 B allocation.

### Decision 10 - Final verification throttling

Problem: A second verification pass was required, but the workstation still reported CPU 100% and active dotnet compiler processes.
Solution: Do not start another compiler. Repeat static checks: diff whitespace, hot-token scan, text-loader scan, packed-header scan, and contextual reads around every suspicious grep hit.
Rejected Alternatives: Running dotnet build under contention was rejected by project rule. Ignoring grep hits was rejected because `WaitForCompletionAsync`, `TryGetComponent`, and editor `FileStream` need phase proof.
Scalability potential: Not a runtime feature; prevents multi-agent compile contention and keeps source verification deterministic.
Hardware Impact: Saved one additional build process on an overloaded machine. Latest blocked gate: CPU 100%, dotnet PIDs 3100/8156. Compile status remains pending; source patch status remains statically verified only.

### Decision 11 - Bounded packed quest count decode

Problem: Legacy/corrupt `DeltaCount` could pass through `checked((int)header.DeltaCount)` and throw before the loader reached its fail-closed validation path; v0x000B counts also needed an authoritative quest-layout cap.
Solution: Decode into an int without throwing, validate the count against `QuestRuntimeLayout.WordCapacity`, and reject both write and load paths when the count exceeds the live quest runtime word capacity.
Rejected Alternatives: Clamping was rejected because it would silently truncate persisted truth. Keeping only the raw-payload budget was rejected because 65535 words is technically small enough to allocate but invalid for the quest layout.
Scalability potential: Low/mid/high/ultra all share the same deterministic quest word capacity; no quality tier can change save identity.
Hardware Impact: Corrupt save path avoids exception unwinding and oversized array allocation. Normal path cost is one integer comparison in cold save/load.

### Decision 12 - Atomic dialogue flag backing

Problem: `ushort |=` is sufficient for single-threaded owner calls but weak as a persisted-truth contract if a future cold async completion records another dialogue bit.
Solution: Keep the public 16-bit API, store the bits in an int, read with `Volatile.Read`, merge with `Volatile.Write` on load, and record bits with `Interlocked.CompareExchange`.
Rejected Alternatives: A lock was rejected because this is one machine word. A NativeArray or DataVault lane was rejected because SaveManager already owns dialogue save truth.
Scalability potential: Low/mid/high/ultra receive the same deterministic 16-bit mask; presentation remains the only quality-scaled layer.
Hardware Impact: Hot read remains one volatile int read and cast. Rare write costs a short compare-exchange loop and allocates 0 B.

### Decision 13 - Current binary layout gate

Problem: The prompt requires `UnsafeUtility.SizeOf<T>()` proof for unmanaged save layouts; the source had explicit attributes but no cold runtime guard for the current 0x000B header path.
Solution: Add `TryValidateCurrentBinaryLayouts()` and call it from indexed write and header validation. It asserts current SaveFileHeader is exactly 56 bytes and 8-byte aligned, and QuestSaveHeader is exactly 64 bytes and 8-byte aligned.
Rejected Alternatives: Testing only via comments or external reports was rejected. Validating legacy 44-byte headers for 8-byte alignment was rejected because they are compatibility artifacts, not new/updated DTOs.
Scalability potential: No tier changes; all devices fail closed on layout drift before binary corruption.
Hardware Impact: Cold save/load cost is two `UnsafeUtility.SizeOf<T>()` calls and two comparisons. Runtime steady-state cost is 0.

### Decision 14 - Monolith layout acceptance gate and Terminal preview lock flattening

Problem: `static_data.h8bin` resident validation checked header/directory/hash integrity but accepted the blob before asserting current unmanaged monolith DTO sizes. TerminalOS applied-lore preview also opened the terminal state buffer before resolving monolith UTF-8 bytes.
Solution: Call `H8DataLayoutAudit.ValidateBlittableSizes()` inside `TryValidateResidentArena()` before resident header parsing completes, and resolve the applied-lore UTF-8 span before opening `_terminalStatesHandle`.
Rejected Alternatives: A separate parser layer was rejected because `H8StaticDataArena` is already the data owner. Holding the terminal state buffer across monolith lookup was rejected because it lengthens a cross-domain native-buffer access window for no benefit.
Scalability potential: Low/mid/high/ultra all fail closed on DTO drift; higher tiers can still consume richer monolith packets through the same span route without changing runtime ownership.
Hardware Impact: Cold monolith boot adds one existing size-audit call; steady-state cost is 0. Terminal preview applies the same bounded copy but no longer keeps terminal state buffer open during packet binary search and locale fallback.

### Decision 15 - PDA editor-only CSV scratch removal

Problem: `PDAEncyclopediaStreamer` is a Data Monolith consumer, but its editor-only metadata CSV ingest left a player-compiled serialized CSV path and a 64 KB native scratch buffer handle in the normal vault allocation set.
Solution: Gate `metadataCsvRelativePath`, `CsvScratchBufferId`, `CsvScratchBytes`, `_csvScratchHandle`, and all handle allocation/release/validation for that scratch buffer behind `UNITY_EDITOR`. Keep runtime metadata seeding through `H8AppliedLoreRuntime.GetPacketRecords()`.
Rejected Alternatives: Removing PDA CSV ingest was rejected because it is an editor authoring tool. Keeping the buffer in player builds was rejected because it is dead runtime capacity.
Scalability potential: Low/mid/high/ultra all get identical runtime monolith metadata behavior; editor workflows keep CSV ingestion for authoring only.
Hardware Impact: Player builds avoid one 64 KB native buffer plus one serialized string path in PDA bootstrap. Runtime steady-state cost is unchanged.

### Decision 16 - Packed quest section schema gate

Problem: Three SaveBinaryStorage read paths repeated only local magic/count checks for packed quest sections, so schema recognition and capacity bounds could drift between indexed load, indexed validation, and raw section reading.
Solution: Add one cold `TryValidatePackedQuestSectionHeader()` helper that validates magic, expected count, `QuestRuntimeLayout.WordCapacity`, and schema version `0/current`; force `WriteSchemaVersion()` on the serialized quest header before checksum.
Rejected Alternatives: Adding another DTO or sidecar schema table was rejected because QuestSaveHeader already owns the schema slot. Silent count clamp was rejected because it would mutate persisted truth.
Scalability potential: Low/mid/high/ultra all use the same save identity and fail-closed layout. Higher tiers can add richer quest presentation without changing packed truth.
Hardware Impact: Cold load adds one schema read and two integer comparisons. Steady-state frame cost is 0 B and 0 us.

### Decision 17 - Opt-in native write page pacing

Problem: Native save writes were split into 64 KB chunks, but the indexed save writer could still issue every chunk back-to-back on the background thread. Applying sleep to all native writes would risk accidental stalls for diagnostic or synchronous utility callers.
Solution: Keep the existing AsyncWriteManager authority and add `WriteAllPaged()` as the only paced entry point. The indexed save writer uses it; generic `WriteAll()` and `OverwriteAll()` remain unpaced. Windows and Unix loops sleep 1 ms only after non-final pages when the caller opted in.
Rejected Alternatives: Building a new save writer state machine was rejected because SaveManager already moves the verified save pipeline to `Awaitable.BackgroundThreadAsync`; duplicating ownership would create more failure surfaces. Global sleep inside every native writer was rejected because it could stall cold diagnostic or maintenance paths that are not guaranteed to run on the background thread.
Scalability potential: Low devices get softer disk pressure; mid/high/ultra keep the same binary format and checksum route. Presentation quality is unaffected.
Hardware Impact: i3/MX350 large indexed saves trade background completion latency for lower burst pressure. A 10 MB indexed save has at most 160 paced gaps; no managed allocation is introduced and generic native writes keep their previous latency profile.

### Decision 18 - Applied-lore monolith contract gate

Problem: `static_data.h8bin` could pass resident header/directory/hash validation while still containing applied-lore packet records with broken localization byte ranges, non-monotonic binary-search keys, or route records with empty packet/prerequisite hashes.
Solution: Extend `H8StaticDataArena.TryValidateResidentArena()` with a cold `IsAppliedLoreContractValid()` pass. It aliases existing resident sections as `ReadOnlySpan<T>`, verifies sorted `(PacketHash, LocaleHash)` packet keys, bounded non-empty UTF-8 ranges, sorted route hashes, route counts within fixed capacities, and nonzero hashes for used route slots.
Rejected Alternatives: Deferring this to `H8AppliedLoreRuntime` lookups was rejected because bad data would then fail late during UI use. Building a managed validation index was rejected because the resident section table already owns contiguous unmanaged records.
Scalability potential: Low/mid/high/ultra all reject the same corrupt monolith before gameplay/UI consumption. High tiers can add more applied-lore content without changing the boot contract or runtime lookup path.
Hardware Impact: Cold boot adds two span aliases and linear scans over 6300 packet records plus 414 route records on the current blob. Steady-state cost is 0 B and 0 us; i3/MX350 avoids late UI fallback churn on corrupt data.

### Decision 19 - Data Monolith CLI source-drift bridge

Problem: Full AppliedLore audit now reports `blob=6300 csv=6600`, so source-data changed after the active `static_data.h8bin` bake. The current `Tools/DataMonolithBakeCli` could not compile the current `H8DataMonolithCompiler` because the CLI Unity stubs were behind editor/compiler source drift.
Solution: Patch only the cold CLI stub layer: add `H8StaticDataArena.IsLoaded`, `EditorApplication.isPlayingOrWillChangePlaymode`, `Unity.Mathematics.math.min(uint,uint)`, and an `xxHash3.StreamingState` shim that buffers cold CLI file chunks and hashes the contiguous byte range through the included Unity `xxHash3.Hash64` implementation.
Rejected Alternatives: Running the stale prebuilt CLI exe was rejected because it would not prove the current compiler source. Forcing `dotnet run` while CPU stayed above the 50% gate was rejected by project compile-throttling rules. Hand-authoring `static_data.h8bin` bytes was rejected because the monolith compiler owns section offsets and checksum.
Scalability potential: No player-tier behavior changes. The payoff is build hygiene: weak machines avoid stale monolith runtime surprises once the bake window opens; high-tier workflows can safely rebake larger applied-lore sets from the same CLI route.
Hardware Impact: Runtime cost is 0. CLI-only stubs allocate during cold bake validation; current bake remained blocked by CPU gate after the stub fix, so no new runtime payload was emitted.

### Decision 20 - 28-section monolith proof-probe repair

Problem: Fresh bake produced a valid current monolith with `appliedLore=6900` and `appliedLoreRoutes=454`, but `DataMonolithLoadStressProbe` rejected it as `FailureHeader` because its manual validator still treated `H8DataSectionId.PhysicsConstants` as the final section count. That was correct before applied-lore packet/route sections became active and wrong for the current `static_data.h8bin` contract.
Solution: Update both load-stress and fail-closed CLI proof validators to require `(ushort)H8DataSectionId.AppliedLoreRoutes` as the section-count sentinel. Keep checksum, offset, record-size, localization, and corruption rejection checks unchanged.
Rejected Alternatives: Ignoring the CLI code 4 was rejected because it would leave a false red proof path. Removing the stress probe was rejected because the runtime gate needs cold binary proof. Loosening the header validator to accept any section count was rejected because it would let truncated section tables pass too far.
Scalability potential: No player-tier runtime change. The fix keeps weak-device bake validation aligned with the same monolith shape high-tier builds consume.
Hardware Impact: Runtime cost is 0. CLI validation avoids false-failing valid 8.2 MB monoliths; external compiler saturation still prevented a legal full CLI rerun after the patch.

### Decision 21 - CSV writer sink exclusion in player parser proof

Problem: `DataMonolithPlayerParserAbsenceProbe` failed after the load/fail-closed probes because it classified `QAEnduranceWatchdogBot.WriteRecord(FileStream, in QAEnduranceCsvRecord)` as a CSV parser route. The code is a cold QA output sink using `FileAccess.Write`; it does not read, parse, or load player static config.
Solution: Add `IsCsvWriterSink()` to the proof probe and skip lines that are explicit CSV writers (`FileAccess.Write`, `FileMode.Create`, `FileMode.Append`, or write/append/flush method declarations) while preserving detection for read/load/parse CSV paths.
Rejected Alternatives: Whitelisting the whole QA folder was rejected because it would hide future QA static-config readers. Editing `QAEnduranceWatchdogBot` was rejected because the runtime code was not the defect. Ignoring CLI code 6 was rejected because the gate should pass for the right reason.
Scalability potential: No player-tier runtime change. The validation signal becomes sharper for all hardware tiers because output telemetry no longer masks real static-data parser violations.
Hardware Impact: Runtime cost is 0. CLI proof scan adds a few ordinal string checks per CSV-looking line only during bake validation.

### Decision 22 - Stale applied-lore blob after concurrent source-data expansion

Problem: Full AppliedLore audit failed after another source-data expansion: CSV source now contains coherent 6900 rows, but the current static_data.h8bin still carries stale title bytes for P409_IBARRA_LOSS_CONVERSION_LEDGER_ARTIFACT/ja_JP.
Solution: Treat CSV as source of truth because --source-only passed and direct binary parse only proves internal blob consistency, not source/blob parity. Required correction is a legal DataMonolithBakeCli rebake once CPU <50 and no compiler process is active.
Rejected Alternatives: Editing the CSV row was rejected because the row parses correctly and source-only audit passed. Claiming runtime-ready from internal blob sort/range checks was rejected because parity failed. Launching bake during Unity Bee/Roslyn compile was rejected by compile-throttling law.
Scalability potential: Low/mid/high/ultra all depend on the same baked monolith; stale blob would show wrong localized text or lengths on every tier.
Hardware Impact: Runtime cost is 0 after rebake. Avoided adding another compiler process while CPU hit 100% and Unity csc.dll was already active. Timestamp: 2026-06-03T14:44:18.9960532+04:00.

### Decision 23 - Reverification under blocked compiler gate

Problem: Source-only AppliedLore audit remains green and direct blob structure is valid, but the required parity rebake cannot legally run while Unity Roslyn/Bee owns `dotnet` and CPU stays over the 50% ceiling.
Solution: Repeat only non-compiler proof: prompt extraction, source-only AppliedLore audit, target diff check, orphan `.meta` scan, direct binary section parser, hot-token sweep, Editor.log tail, and process command-line inspection.
Rejected Alternatives: Starting `dotnet run` for DataMonolithBakeCli under active VBCSCompiler/csc was rejected. Rewriting CSV was rejected because source-only audit proves source coherence. Killing Unity compiler processes was rejected because they are parented by another active Unity/Bee compile.
Scalability potential: No runtime tier change. The pending rebake is a content parity operation shared by low/mid/high/ultra.
Hardware Impact: Runtime cost is 0. Avoided one illegal concurrent bake/build while CPU sampled 83-100% for 12 checks. Timestamp: 2026-06-03T15:02:00+04:00.

### Decision 24 - Remove duplicate resident section parser

Problem: The applied-lore contract gate introduced `TryGetResidentSectionSpan`, duplicating the existing resident section span mechanics in `TryGetSectionSpan`.
Solution: Extract the shared arena-backed parser into `TryGetSectionSpanInArena`; public loaded queries and pre-load resident validation both call it.
Rejected Alternatives: Keeping duplicate code was rejected by anti-duplication rules. Using only the public loaded query was rejected because `TryValidateResidentArena()` runs before `_loaded` is set.
Scalability potential: No tier behavior changes; low/mid/high/ultra all use the same monolith section parser.
Hardware Impact: Runtime steady-state cost is unchanged. Cold validation removes one duplicated branch surface and keeps zero managed allocation. Timestamp: 2026-06-03T15:16:00+04:00.

### Decision 25 - Read-only PDA mock UTF8 and narrative-band dialogue proof

Problem: PDA mock lore lookup still resolved mock UTF-8 bytes through a mutable vault buffer in a read accessor, and the save header layout gate only proved the dialogue source word sat before the quest gameplay band, not inside the intended narrative band.
Solution: Switch `TryGetMockUtf8` to `TryReadVaultBuffer` with `NativeArray<byte>.ReadOnly` and keep the span creation on a read-only pointer. Extend `TryValidateCurrentBinaryLayouts()` with explicit `NarrativeWordStart`, `NarrativeWordCount`, and runtime word-capacity checks so dialogue flags cannot drift into quest/item/location truth.
Rejected Alternatives: Adding another PDA cache or helper buffer was rejected because the vault already owns the bytes. Rewriting the save header again was rejected because the 56-byte v0x000B header contract is already fixed and checksum-covered. Rebaking the monolith under CPU/compiler load was rejected by compile-throttling rules.
Scalability potential: Low/mid/high/ultra all read the same immutable PDA mock span and the same save-header dialogue mask. Higher tiers can add presentation response without moving the persisted bit source.
Hardware Impact: Runtime allocation remains 0 B. PDA read path removes mutable buffer exposure without adding work; save layout validation adds cold integer comparisons only. Timestamp: 2026-06-03T15:25:35+04:00.

### Decision 26 - TerminalOS read-only DataVault aliases

Problem: Several TerminalOS presentation/telemetry helpers read DataVault buffers through the mutable owner resolver even when they only needed snapshots for hash, lookup, or telemetry-copy work.
Solution: Route read-only consumers through `TryReadVaultBuffer`: gaze input capture, terminal-state lookup, panel state sampling, decryption puzzle/terminal telemetry sampling, and layout hash reads. Keep mutable resolver on setters, dirty marking, upload staging, telemetry-ring writes, job schedule writers, and owner ref APIs.
Rejected Alternatives: Converting every `TryOpenVaultBuffer` call was rejected because many callsites write owner state or pass buffers to jobs. Adding another read facade was rejected because `TryReadVaultBuffer` already exists in TerminalOS.
Scalability potential: Low/mid/high/ultra keep identical gameplay and presentation truth; higher tiers can still run richer panel/decryption visuals without expanding DataVault write ownership.
Hardware Impact: Runtime allocation remains 0 B. The change narrows mutable alias lifetime on LateFrame/UI telemetry paths; no extra jobs, locks, or buffers are introduced. Timestamp: 2026-06-03T15:30:04+04:00.

### Decision 27 - TerminalOS read helper made truly read-only

Problem: After moving callsites to `TryReadVaultBuffer`, the helper still resolved `IDataVault.TryReadHandle` and returned mutable `NativeArray<T>`, so the method name and callsite intent did not enforce read-only ownership.
Solution: Change `TryReadVaultBuffer<T>` to return `NativeArray<T>.ReadOnly` from `IDataVault.TryReadOnlyHandle`, then update every TerminalOS and TerminalProjection read callsite to consume the read-only type.
Rejected Alternatives: Adding a second helper was rejected because it would keep two read access routes. Blindly converting writer paths was rejected because screen commands, dirty flags, telemetry ring writes, uploads, and job owner buffers still require writable authority.
Scalability potential: Low/mid/high/ultra keep identical terminal behavior; high-tier presentation can expand without widening writable DataVault aliases.
Hardware Impact: Runtime allocation remains 0 B. This removes writable alias exposure from read-copy/hash/dump paths with no added buffers, locks, jobs, or scene lookups. Timestamp: 2026-06-03T15:40:00+04:00.

### Decision 28 - Terminal projection telemetry write lock flattening

Problem: `RecordTerminalInputTelemetry` still wrote the terminal-input black-box ring through the generic mutable buffer resolver instead of a bounded write lock with guaranteed release.
Solution: Build `TerminalInputTelemetryEntry` before acquiring the write lock, then write exactly one ring slot through `TryAcquireWriteLock` and release it in `finally`. Additional non-mutating validation/gizmo/bounds/fallback reads use `TryReadVaultBuffer`.
Rejected Alternatives: Converting bulk GPU memcpy upload sources to `NativeArray<T>.ReadOnly` was rejected until a compile window proves the unsafe pointer overload; those paths keep the existing proven alias and only copy bytes out. Taking multiple locks at once was rejected; the new helper owns exactly one telemetry ring lock.
Scalability potential: Low/mid/high/ultra keep identical telemetry and terminal projection behavior. Higher tiers can add richer projection diagnostics without expanding write-lock duration.
Hardware Impact: Runtime allocation remains 0 B. The write lock now encloses one index clamp, one struct assignment, and one cursor increment; projection fault math and DTO construction happen before the lock. Timestamp: 2026-06-03T15:55:00+04:00.
