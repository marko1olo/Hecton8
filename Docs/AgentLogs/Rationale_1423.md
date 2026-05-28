# Rationale_1423

Status: PENDING VERIFICATION

## Decision 001 - Domain Boundary

Problem: Agent 1423 is assigned to text allocation cleanup, but VWS crosses UI and Audio.
Solution: Limit write scope to `Assets/_Project/Scripts/UI`, `Assets/_Project/Scripts/Localization`, and VWS-facing files under `Assets/_Project/Scripts/Audio`. Cross-domain data flow must use existing interfaces or typed signal lanes.
Rejected Alternatives: Broad audio subsystem rewrite; changing DSP/VWS authority before source proof.
Scalability potential: Low tier uses bounded char buffers and drop-oldest UI warning presentation. Middle tier keeps full warning cadence. High tier may add richer visual distortion from saved CPU. Ultra tier may add presentation overkill only in `VISUAL_SYNC`.
Hardware Impact: Expected gain on i3/MX350 is reduced GC spikes from dynamic text paths; measured proof absent.

## Decision 002 - Mandate Set

Problem: Text cleanup touches localization, UI cadence, VWS/audio route, signal ownership, and telemetry.
Solution: Loaded 8 mandates: Babel localization, UI data streaming, zero-GC policy, execution phases, global registry DI, signal lane segregation, DSP audio SPSC, post-mortem telemetry.
Rejected Alternatives: Reading unrelated physics/world-generation mandates; would pollute domain decisions.
Scalability potential: Same formatter/pool supports weak devices through truncation/cadence and high devices through visual text effects without allocation.
Hardware Impact: Static policy reduces hot-path heap pressure; exact microsecond gain requires profiler/GCMonitor.

## Decision 003 - Build Throttle

Problem: Batch forbids build spam and forbids build during CPU contention or active `csc.exe`.
Solution: Use static source scans first. Before any build, sample CPU and compiler processes.
Rejected Alternatives: Immediate `dotnet build` after every edit; wastes host CPU and risks blocking other agents.
Scalability potential: Not runtime-facing; protects integration throughput.
Hardware Impact: Avoids local CPU contention; runtime impact none.

## Decision 004 - Task 01 Scan Shape

Problem: The target set contains many Editor windows and scanners with intentional string formatting, while the runtime UI/VWS paths must remain allocation-free.
Solution: Generated `Docs/Reports/UI_STRING_ALLOCATION_HITLIST_1423.json` from an `rg` lexical pass over UI, Audio, and Babel/localization files. Runtime-only follow-up excluded `**/Editor/**` and found no real hot-path hits for `.text =`, `string.Format`, `.ToString()`, or interpolation; two runtime false positives were literal glyph seed data containing `$` characters.
Rejected Alternatives: Editing every lexical hit regardless of Editor-only scope; running a heavy semantic compiler pass before identifying actual runtime files.
Scalability potential: Low/Middle/High/Ultra all benefit from preserving zero-allocation runtime text paths; Editor-only allocations do not affect gameplay frame pacing.
Hardware Impact: Static scan cost 1083702 us. Runtime gain from Task 01 alone is zero because this task only produced evidence; it prevents wasting edits on non-runtime code.

## Decision 005 - Localization Database Route

Problem: The prompt describes string-heavy localization, but the current runtime source may already have a binary Babel route. Editing without proving the route risks replacing a working zero-copy path with managed glue.
Solution: Treat `BabelDictionaryStore.FetchUtf8`/`TrackUtf8Lookup` and `LocRegistry.TryGetLocalizedSpan` as the hot database route. The database owner returns `ReadOnlySpan<byte>` over mapped/native bytes; consumers decode into caller-owned `Span<char>` using `TryWriteVisualSpanFromUtf8`.
Rejected Alternatives: Converting the database into managed `Dictionary<string,string>` caches; rewriting `LocalizationManager.Get`/`GetFormatted` first when static evidence marks them as compatibility APIs, not the observed VWS/subtitle path.
Scalability potential: Low tier reads only needed UTF-8 slices and truncates in fixed buffers. Middle tier keeps full caption cadence. High/Ultra tier can spend saved CPU on presentation effects without changing text truth ownership.
Hardware Impact: Static inspection estimate 9360000 us. Expected runtime benefit on i3/MX350 is avoiding managed string materialization per lookup; profiler proof still absent.

## Decision 006 - VWS Subtitle Route

Problem: VWS spans audio and UI, so a direct call from audio warning logic into TMP would violate lane segregation and create hidden ownership.
Solution: Preserve the existing route: VWS queues warning IDs, publishes `VocalCueSignal` for audio and `SubtitleCueSignal` for presentation through `SignalBus<T>`, Babel sync drains fixed snapshots, and `SubtitleManager` resolves text hashes into char buffers before TMP `SetCharArray`.
Rejected Alternatives: Passing managed subtitle strings inside warning DTOs; using `HectonEventBus` as a hot caption bus; scene searching for SubtitleManager from VWS.
Scalability potential: Low tier bounded signal queue drops safely. Middle tier preserves cadence. High/Ultra tier can add visual styling after text decode while leaving DTO layout unchanged.
Hardware Impact: Static inspection estimate 7320000 us. No source edit yet; benefit is correctness of planned write scope and prevention of cross-domain managed strings.

## Decision 007 - Buffer Pool Strategy

Problem: The assignment demands char buffer pooling, but the project already has `CharBufferPool.BabelLease`, a DataVault-backed native arena path, and TMP bridge arrays. Replacing this would add risk and churn.
Solution: Reuse the existing fixed pool and harden central formatting/truncation behavior. Any edit must preserve fixed capacity, no resizing, caller-owned spans, and fail-closed truncation.
Rejected Alternatives: Per-caption char arrays, `StringBuilder`, pooled managed builders, growable lists, and tiny Burst jobs for single subtitle formatting. These either allocate, resize, or add dispatcher overhead without a batch-local workload.
Scalability potential: Low uses same 512-char path with ellipsis. Middle keeps full VWS lines. High adds richer VisualSync styling. Ultra can layer presentation overkill without changing the zero-GC text route.
Hardware Impact: Static inspection estimate 4100000 us. Runtime gain from future edits should be reduced overflow fallback work and no allocator pressure; exact microseconds require later measurement.

## Decision 008 - Report And Telemetry Proof

Problem: The final report must prove zero-GC behavior, but static source facts are not runtime allocation measurements.
Solution: Defined `Docs/Reports/UI_ZERO_GC_OPTIMIZATION_REPORT_1423.json` schema with explicit fields for static scans, deleted calls, formatter proof, truncation proof, telemetry ring evidence, build throttle proof, and unverified claims. Future black-box UI telemetry must use a fixed 300-frame buffer and dump to `Docs/AgentLogs/Dump_1423.bin`.
Rejected Alternatives: Recording fake 0 B/frame, hiding missing profiler evidence, or treating Editor-only string cleanup as gameplay improvement.
Scalability potential: Low/Middle/High/Ultra reporting uses the same proof schema; continuous `GlobalQualityWeight` may affect optional presentation fidelity but not text truth or DTO layout.
Hardware Impact: Static planning estimate 1220000 us. Runtime impact none until telemetry/report implementation.

## Decision 009 - Invariant Numeric Formatting

Problem: Span-based numeric formatting was already allocation-free, but several paths relied on culture defaults. That can change decimal separators and warning text width under non-English OS culture.
Solution: Centralized int/float formatting in `ZeroGCFormatter` with `CultureInfo.InvariantCulture` provider overloads and routed `LocNumericArg.Float` through that formatter. This preserves span writes and avoids `ToString`.
Rejected Alternatives: `value.ToString(format, CultureInfo.InvariantCulture)` followed by copy; stackalloc plus managed string conversion; leaving culture default because current developer machines use dot decimal.
Scalability potential: Low tier gets deterministic compact numbers. Middle/High/Ultra can vary precision via existing continuous quality/cadence decisions without changing culture or allocation behavior.
Hardware Impact: Static/source edit estimate 1800000 us. Runtime allocation reduction versus old span path is zero because old path was already span-based; correctness gain is deterministic width/decimal formatting.

## Decision 010 - Placeholder Overflow Semantics

Problem: `LocRegistry` recognized Babel placeholders but if formatting could not fit, helper failure let decode fall back to literal token copying. That is not fail-closed for dynamic values.
Solution: Distinguish recognized placeholders from invalid literals. If a recognized caret or brace placeholder cannot write into the bounded destination span, clamp `charCursor` to `maxGlyphs`, mark truncation, and let the existing ellipsis path produce a bounded result.
Rejected Alternatives: Throwing on overflow; allocating a larger buffer; silently copying `{0}` or `^0` literals when a real dynamic argument exists.
Scalability potential: Low tier safely truncates long warning values. Middle tier usually fits. High/Ultra may use larger presentation capacities later but must keep the same fail-closed behavior.
Hardware Impact: Static/source edit estimate 2800000 us. Expected runtime gain is prevention of wasted literal fallback decode work under overflow; exact microseconds not measured.

## Decision 011 - TMP Localized Hash Helper

Problem: Future UI callers can still accidentally route localized hashes through managed strings even though SubtitleManager already uses `SetCharArray`.
Solution: Added `TmpTextNoAlloc.SetLocalized` so a TMP target can acquire a Babel lease, decode UTF-8 with `LocRegistry.TryWriteVisualSpanFromUtf8`, copy through the TMP bridge, and call `SetCharArray` without exposing a string route.
Rejected Alternatives: Direct `TMP_Text.text`, a managed `Get(hash)` wrapper, or adding scene-level dependencies from VWS into UI text objects.
Scalability potential: Low tier uses fixed 512-char lease and truncation. Middle tier preserves normal lines. High/Ultra can layer style/animation after the char array injection.
Hardware Impact: Static/source edit estimate 2000000 us. Runtime gain depends on future callers adopting this helper; SubtitleManager already had direct SetCharArray.

## Decision 012 - Build Deferral After Source Edits

Problem: The formatter edits touch compile-sensitive overloads, but project policy forbids builds during CPU contention and active compiler work.
Solution: Checked compiler process and CPU before building. No `csc.exe` was observed, but CPU sampled at 58.24%, above the 50% threshold, so compilation was deferred and static verification recorded instead.
Rejected Alternatives: Launching `dotnet build` against policy; claiming successful compile without running it.
Scalability potential: Not runtime-facing; protects integration throughput with 20+ active agents.
Hardware Impact: Avoided additional build CPU contention. Runtime impact none.

## Decision 013 - Hash To Caller-Owned Localization Writer

Problem: Hash-addressed localization consumers needed an explicit contract route that cannot return a new managed string.
Solution: Added `IBabelLocalization.TryWriteLocalized(uint, Span<char>, out int, bool)` and implemented it with `LocRegistry.TryWriteVisualSpanFromUtf8`. This exposes the already-owned UTF-8 database through caller-owned char storage.
Rejected Alternatives: Mutating legacy `Get(string)` behavior and pretending ring-buffer spans are permanent pinned char memory.
Scalability potential: Low tier can call with small fixed buffers and truncate. Middle/High/Ultra can use larger prewarmed presentation buffers without changing the contract.
Hardware Impact: Static/source edit estimate 2100000 us. Expected gain is avoiding future hash-to-string bridge calls; measured runtime gain absent.

## Decision 014 - UI Optimization Telemetry Ring

Problem: Overflow and missing localization failures needed numeric, unmanaged proof instead of managed warning strings.
Solution: Added `UIOptimizationTelemetryEntry` as a 64-byte DataVault-backed ring with 300 entries, buffer id 15070552, numeric `UIOptimizationFailureCode`, and dump path `Docs/AgentLogs/Dump_1423.bin`. Failure branches in `LocRegistry` write `MissingLocalizationHash`, `TextBufferOverflow`, or `FormatterOverflow`.
Rejected Alternatives: `Debug.Log` strings in failure branches; resizing text buffers; using HectonEventBus for hot UI failure telemetry.
Scalability potential: Low tier records truncated failures without heap pressure. Middle/High/Ultra keep the same ring and can add richer diagnostic visualization later.
Hardware Impact: Static/source edit estimate 4200000 us. Runtime overhead only on failure branches; no profiler proof.

## Decision 015 - VWS Reconciliation Scope

Problem: The task demanded a VWS rewrite, but source inspection showed the active route already dispatches unmanaged `VocalCueSignal` and `SubtitleCueSignal` instead of managed text.
Solution: Preserve the existing VWS signal route and do not add UI references to VWS. Presentation remains the only layer that resolves text hashes and calls `SetCharArray`.
Rejected Alternatives: Adding subtitle strings to VWS DTOs; adding direct TMP references in the audio system; changing signal layout without a real dynamic value requirement from current source.
Scalability potential: Low tier drops bounded subtitle signals. Middle/High/Ultra add visual presentation only after the signal is consumed by UI.
Hardware Impact: Static inspection estimate 1900000 us. Runtime change is zero by design; it avoids damaging an already-correct route.

## Decision 016 - Build Result

Problem: A build was required once the CPU gate allowed it, but failures outside the assignment domain can block syntax proof.
Solution: Ran `dotnet build Hecton8.Core.csproj --nologo` after CPU sampled 44.12% and no `csc.exe` was running. Build failed on `Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs` missing `XRPass` at lines 437 and 476.
Rejected Alternatives: Fixing Visor/XR from a localization agent; rerunning builds without addressing dependency; claiming compile success.
Scalability potential: Not runtime-facing.
Hardware Impact: Build wall time 54690 ms; no runtime impact.

## Decision 017 - Fuzzer And Truncation Tests

Problem: The formatter needed repeatable proof that spam writes and overflow handling stay inside fixed buffers.
Solution: Added `ZeroGCSubtitleFormatter1423EditTests` with a 500-warning mock formatter loop, invariant-culture numeric assertion, and fixed-buffer ellipsis/cursor assertions.
Rejected Alternatives: Runtime-only manual testing with no source artifact; using string.Format as an oracle.
Scalability potential: Low/Middle/High/Ultra all depend on the same bounded writer invariants.
Hardware Impact: Static/source edit estimate 3000000 us. Tests were not executed because build is blocked by out-of-domain XRPass errors.

## Decision 018 - Final Proof Honesty

Problem: Task 20 requested measured 0B GC for a 500-subtitle test, but no runtime/profiler pass could run after the compile dependency failure.
Solution: Final report records `runtime_gc_bytes = null` and `NOT_MEASURED_BUILD_BLOCKED_BY_CPU_THROTTLE`/build dependency status instead of inventing zero allocation evidence. Static scan and SHA-256 hashes are included.
Rejected Alternatives: Fake 0B result; hiding build failure; presenting regex audit as profiler proof.
Scalability potential: Proof schema can accept real low/middle/high/ultra runtime captures later without changing source code.
Hardware Impact: Report generation estimate 2700000 us. Runtime impact none.

## Decision 019 - APEX Failure Telemetry Hot Path

Problem: APEX self-audit required proof that UI text failure telemetry cannot allocate or cold-initialize dependencies from `LocRegistry` overflow/missing-key branches.
Solution: Verified `BabelSubtitleSyncRuntime.RecordUIOptimizationFailure` now returns unless `failureCode != None`, `s_initialized == true`, and `s_vault != null`, then acquires `UIOptimizationTelemetryBufferId` through `TryAcquireWriteLock` and releases in a `finally` block. This keeps failure reporting numeric, unmanaged, and non-initializing.
Rejected Alternatives: Calling `EnsureInitialized()` from every missing localization hash, writing managed `Debug.Log` strings, or resizing the char buffer on overflow.
Scalability potential: Low tier records only compact failure rows in a fixed 300-frame ring. Middle keeps the same proof lane. High and Ultra can visualize the ring later without changing DTO layout or text ownership.
Hardware Impact: Static inspection estimate 420000 us. Expected i3/MX350 gain is avoidance of cold GlobalRegistry/DataVault work during subtitle overflow faults; profiler proof absent.

## Decision 020 - APEX Verification Boundary

Problem: The final proof needed exact hashes, hot-method scans, DataVault lock lines, offset lines, and build throttling facts without pretending that static analysis equals runtime allocation evidence.
Solution: Emitted `Docs/Reports/UI_ZERO_GC_APEX_VERIFICATION_1423.json`, refreshed AST/final reports, and wrote SHA-256 companions. APEX CPU sample was 67.00% with no `csc.exe`, so no second `dotnet build` was launched after the previous out-of-domain `XRPass` failure.
Rejected Alternatives: Re-running a known-blocked build under CPU >50%, claiming measured 0B allocations, or treating cold whole-file `new` hits as gameplay text hot-path failures.
Scalability potential: Continuous proof cites `GlobalQualityWeight` routes for canvas dirty budget and lookup budget. Residual risk is documented: TMP `richText` and `stripRichText` are boolean sink APIs derived from a continuous scalar.
Hardware Impact: Report update estimate 900000 us. Runtime impact none until the project builds and profiler/GCMonitor capture can run.

## Decision 021 - Manual RTL Reversal Removal

Problem: The UI localization mandate forbids manual RTL reversal. Existing Babel paths reversed character order in `LocRegistry`, `LocalizedWorldSign`, and `RTLProcessor`, which can corrupt Arabic/Hebrew shaping, punctuation, and mixed-direction labels while pretending to be zero-GC.
Solution: Removed `RTLProcessor.cs` and `.meta`, removed `ReverseSpanInPlace`, made `LocRegistry.ResolveVisual` and `TryGetVisualBuffer` return logical TMP-ready text, and set `TMP_Text.isRightToLeftText` at subtitle, label swap, localized world sign, no-alloc TMP helper, and PDA decrypt sinks.
Rejected Alternatives: Keeping the reversal because it is allocation-free; adding a second custom BiDi implementation; returning a reversed thread-static buffer from `LocRegistry` under the name "visual".
Scalability potential: Low, middle, high, and ultra devices all use the same correct logical text. Quality can change styling density, not language correctness or text ownership.
Hardware Impact: Static/source edit estimate 1800000 us. Low-end gain is removal of thread-static reversal buffer growth and per-character reversal work for RTL labels; exact runtime microseconds not measured.

## Decision 022 - Continuous Rich Text Retention

Problem: `SubtitleManager` and `LabelSwapScheduler` used binary rich-text stripping thresholds derived from `GlobalQualityWeight`. This violates the continuous scalability pillar and causes a visible cliff between weak and stronger devices.
Solution: Added `BabelRichTextLodPolicy`. It computes `retention = saturate(lerp(0.16, 1.0, q*q*(3-2*q)))` where `q = saturate(GlobalQualityWeight)`, then compares a stable per-text hash threshold. Low devices retain a deterministic minority of styling; high devices retain all styling.
Rejected Alternatives: `if(isLowEnd)` branches, `quality < 0.5f` gates, or enabling/disabling TMP parsing as the primary quality control. TMP parsing stays enabled; the decoded content controls aggregate style density.
Scalability potential: Low keeps legible text and some styling. Middle increases retained styling smoothly. High reaches full localized styling. Ultra can spend saved budget on presentation effects without changing DTO layout or Babel ownership.
Hardware Impact: Static/source edit estimate 1600000 us. Expected i3/MX350 gain is lower styled-tag density without a visible binary drop; profiler proof absent.

## Decision 023 - PDA Decrypt Label Lease Safety

Problem: `PDADataArchaeologyDecryptLabel` was the only direct `TryGetVisualBuffer` consumer left after `LocRegistry` stopped returning reversed visual order. It also released `CharBufferPool` leases only after `SetCharArray`, so an exception path could leave the pool slot occupied.
Solution: Set `targetText.isRightToLeftText` from `LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage)` before `SetCharArray`, and wrapped the acquired lease in `try/finally`. `writeLength` remains `min(length, sourceCapacity, SlotCapacity)`, so both source and destination spans share the same bound.
Rejected Alternatives: Assuming PDA archaeology names never localize to RTL; adding a managed string fallback for decrypt text; leaving lease release after TMP injection.
Scalability potential: Low through ultra use the same bounded buffer and same scramble math. Quality already controls scramble intensity continuously; this change only fixes language correctness and pool safety.
Hardware Impact: Static/source edit estimate 650000 us. Runtime allocation impact is zero by construction; reliability gain is deterministic lease release under failure.

## Decision 024 - Final Build Throttle Compliance

Problem: The continuation edits are compile-sensitive, but the project rule forbids `dotnet build` when CPU is over 50% or another build/compiler process is active.
Solution: Re-sampled the host before a final build decision. CPU was 65.00%, `csc.exe` count was 0, and one dotnet build-related process was active: `AmplifyImpostors.Editor.csproj` PID 66444. No build was launched. Reports were refreshed with this latest sample.
Rejected Alternatives: Launching a second build to chase confidence; using the stale earlier 48.00% sample as final evidence; claiming compile success while the known `XRPass` blocker remains.
Scalability potential: Not runtime-facing. It preserves integration throughput while 20+ agents operate in the same workspace.
Hardware Impact: System check estimate 1700000 us beyond the first continuation throttle check. Runtime impact none; compile/test status remains pending.

## Decision 025 - Localized World Sign Fail-Closed Buffering

Problem: `LocalizedWorldSign.EnsureBuffer(requiredLength)` could allocate a larger `char[]` when a fallback or localized sign exceeded the current buffer. Even if this is colder than subtitles, it violates the fail-closed localization rule: long translated text must truncate safely, not grow memory at runtime.
Solution: Replaced growth with fixed 128-char buffers, added `CopySpanFailClosed`, bounded uppercase/display copy, ASCII ellipsis, and numeric `TextBufferOverflow` telemetry via `BabelSubtitleSyncRuntime.RecordUIOptimizationFailure`. Cursor proof: `safeLength = clamp(sourceLength, 0, sourceCapacity)`, `copyLimit <= 125` when truncating, `AppendAsciiEllipsis` clamps `cursor` to `capacity - 3`, so final `cursor <= 128`.
Rejected Alternatives: Keeping cold growth because it is rare; using `string.Substring`; expanding signs to preserve grammar; adding a physics-like layout solver for long text.
Scalability potential: Low devices get stable bounded signage. Middle/high/ultra keep the same text truth and can improve typography or visual treatment elsewhere; capacity growth never becomes a quality tier.
Hardware Impact: Static/source edit estimate 1450000 us. Low-end gain is preventing surprise heap growth on language change or missing-key fallback. Runtime profiler proof absent.

## Decision 026 - Broad Domain Polish Scan

Problem: Previous APEX proof covered modified hot paths. The user requested deeper self-search for hidden domain violations.
Solution: Scanned 152 non-Editor UI/Audio/Babel runtime C# files for managed string patterns, `foreach`, and scene-search calls. Counts: forbidden string patterns 0, `foreach` 0, scene search 0. Emitted `Docs/Reports/UI_ZERO_GC_POLISH_VERIFICATION_1423.json`. Final build-gate sample was CPU 68.00%, `csc.exe` 0, active `dotnet build Hecton8.slnx` PID 22412, so no new build was launched.
Rejected Alternatives: Reporting only the old modified-file scan; counting cold `new char[]` field initializers as hot faults; rewriting unrelated UI systems without a real violation.
Scalability potential: The scan validates the domain-wide runtime text route remains bounded on weak through ultra devices. No new visual feature was added.
Hardware Impact: Static scan/report estimate 2100000 us; build-throttle check estimate 1600000 us. Runtime impact none.

## Decision 027 - TmpTextNoAlloc Lease Finally Repair

Problem: `TmpTextNoAlloc.Set(ReadOnlySpan<char>)` acquired `CharBufferPool` leases and released them after `TMP_Text.SetCharArray`. If TMP throws during mesh/text ingestion, the release line is skipped and a fixed pool slot remains occupied.
Solution: Wrapped the `Lease`, `BabelLease`, and `EncyclopediaLease` branches in `try/finally`. The localized hash branch already used `finally`. The copy invariant did not change: `Copy` writes `min(source.Length, destination.Length)`, and `SetCharArray` receives exactly that length.
Rejected Alternatives: Expanding the pool, adding a managed string fallback, catching/swallowing TMP exceptions, or rewriting every UI sink without a specific lease-safety fault.
Scalability potential: Low devices avoid progressive pool starvation after rare TMP faults. Middle/high/ultra keep the same fixed-capacity text path; visual fidelity policy remains governed by continuous `GlobalQualityWeight`, not buffer growth.
Hardware Impact: Static/source edit estimate 520000 us. Runtime allocation delta is zero; reliability gain is deterministic release of occupied pool slots under exception paths.

## Decision 028 - Tiny Buffer Placeholder Overflow Cursor Repair

Problem: The earlier Babel placeholder overflow repair still used `charCursor = maxGlyphs`. When `maxGlyphs < 3`, the ellipsis branch cannot write dots, so promoting cursor to capacity can make the TMP bridge keep stale chars from the previous write length.
Solution: Replaced the production promotions with `charCursor = math.clamp(charCursor, 0, maxGlyphs)` at `LocRegistry.cs:1239`, `1262`, and `1274`; added an editor regression that asserts production source no longer contains `charCursor = maxGlyphs`.
Rejected Alternatives: Treating the defect as theoretical; clearing the whole buffer every write; allocating a clean managed string for tiny overflow output.
Scalability potential: Low devices with tiny emergency buffers fail closed without stale glyphs. Middle/high/ultra use the same invariant; higher quality may change capacity or styling density, not overflow semantics.
Hardware Impact: Static/source edit estimate 740000 us. Runtime allocation delta is zero; low-end reliability gain is bounded stale-glyph prevention under overflow.

## Decision 029 - Read Accessor Purity Split

Problem: Several read-style APIs violated the global systems doctrine by cold-initializing, completing pending cue work, or mutating localization telemetry during reads.
Solution: Split pure lookup from tracked decode: `TryGetLocalizedSpan` now uses `TryFindUtf8Slice`, while decode paths call `TrackLocalizedSpanLookupForDecode`. `TryGetLatestTelemetry`, `TryGetLatestUIOptimizationTelemetry`, `TryGetCue`, `ResolveElapsedSecondsSince`, and `ResolveCurrentAudioTimeSeconds` now read cached initialized state only.
Rejected Alternatives: Leaving hidden mutation because it is convenient for diagnostics; renaming every legacy compatibility API in one pass without a cross-domain route card.
Scalability potential: Low devices avoid surprise initialization/readback work in diagnostics. Middle/high/ultra can consume immutable snapshots at richer cadence without changing ownership.
Hardware Impact: Static/source edit estimate 1700000 us. Expected i3/MX350 gain is avoidance of accidental cold work from getter probes; profiler proof absent.

## Decision 030 - DataVault Post-Acquire Lock Finally Proof

Problem: `TryAcquireSubtitleWriteBuffer` acquired a DataVault write lock and, if post-acquire validation failed, released it directly instead of inside a `finally` block. That failed the strict proof requirement even though the branch is short.
Solution: Added `releaseOnExit` and wrapped post-acquire validation in `try/finally`. Failure or exception releases through `finally`; success sets `releaseOnExit = false` and transfers the still-held lock to the caller, which releases inside its own `finally`.
Rejected Alternatives: Documenting the direct release as "safe enough"; duplicating validation before lock only; swallowing validation exceptions.
Scalability potential: Same lock route on low through ultra devices; quality affects telemetry cadence and text presentation, not lock correctness.
Hardware Impact: Static/source edit estimate 420000 us. Runtime cost is one bool and a `finally` frame on acquisition path; acceptable because write acquisition is already a synchronization boundary.

## Decision 031 - Continuation4 Verification Boundary

Problem: After source repair, the reports and hashes had to reflect current files, but build execution was still forbidden by host load.
Solution: Emitted `Docs/Reports/UI_ZERO_GC_CONTINUATION4_AUDIT_1423.json` and refreshed AST/APEX/final optimization report hashes. Build was not launched: CPU sampled at 100.00% with `VBCSCompiler.exe` PID 18948 after dotnet PID 38260, then 87.00% with no compiler/dotnet process, then 96.00% with `csc.exe` PID 63300 and `dotnet.exe` PID 53008.
Rejected Alternatives: Running `dotnet build` under CPU >50%; reporting stale source hashes; claiming runtime GC proof from static scans.
Scalability potential: Not runtime-facing. The proof preserves integration throughput while other agents/builds use the machine.
Hardware Impact: Report/check estimate 2100000 us. Runtime impact none; compile/runtime status remains pending until the host and out-of-domain XRPass blocker allow verification.

## Decision 032 - Localized Span No-Refresh Validation

Problem: `TryGetLocalizedSpan` had been split away from telemetry, but it still called `IsValidUtf8Slice`, and that helper refreshed vault-backed UTF-8 bytes through `RefreshUtf8BytesFromVault`. That is a read-side mutation hidden behind a validator.
Solution: Added `IsValidUtf8SliceNoRefresh` and routed only `TryGetLocalizedSpan` through it. Decode/write paths continue to call `IsValidUtf8Slice`, preserving the owner-refresh behavior where the route is explicitly mutating presentation state.
Rejected Alternatives: Leaving a transitive refresh in a read accessor; removing refresh from all decode paths and risking stale vault handles during write/decode routes.
Scalability potential: Low devices avoid accidental DataVault handle refresh during pure byte-span probes. Middle/high/ultra keep the same decode performance and can refresh during owner decode/write phases.
Hardware Impact: Static/source edit estimate 620000 us. Runtime win is avoidance of hidden DataVault resolution in pure getter probes; profiler proof absent.

## Decision 033 - PDA Decrypt Double Decode Removal

Problem: `PDADataArchaeologyDecryptLabel.RenderHash` called `LocRegistry.GetLength(hash)` before `TryGetVisualBuffer(hash)`. Both route through decode/ring behavior, so one PDA render performed redundant localization work.
Solution: Removed the length prepass. `TryGetVisualBuffer` already returns source, length, and missing-key fallback. The later `writeLength = min(length, sourceCapacity, SlotCapacity)` bound still proves the TMP write cannot exceed the lease buffer.
Rejected Alternatives: Keeping double decode because the first call looked like a cheap length read; adding another cached field; allocating a temporary string for decrypted labels.
Scalability potential: Low devices save one decode route per dirty PDA archaeology label. Middle/high/ultra keep the same scramble math and presentation quality.
Hardware Impact: Static/source edit estimate 380000 us. Exact frame-time delta not measured.

## Decision 034 - Continuation5 Verification Boundary

Problem: After the read-purity and PDA fixes, artifacts needed fresh hashes, but compile execution was still prohibited.
Solution: Emitted `Docs/Reports/UI_ZERO_GC_CONTINUATION5_AUDIT_1423.json` and refreshed AST/APEX/final optimization report hashes. Build was not launched: CPU sampled 65.00% with no compiler/dotnet process, then after a 30-second wait sampled 100.00% with `dotnet.exe` PID 67140.
Rejected Alternatives: Launching `dotnet build` while CPU was above 50%; pretending editor source-regression tests executed; reporting old SHA-256 values.
Scalability potential: Not runtime-facing. The code changes preserve continuous quality scaling and fixed-buffer text ownership.
Hardware Impact: Report/check estimate 1600000 us. Runtime verification remains pending.

## Decision 035 - Label Swap RichText Policy Sync

Problem: `LabelSwapScheduler.ApplyEntry` decoded localized text into a Babel lease and pushed it through `TMP_Text.SetCharArray`, but it only synchronized `isRightToLeftText`. It did not set `TMP_Text.richText` from `BabelRichTextLodPolicy`, so a staged font/material swap could inherit stale rich-text parser state from a prior owner even while the decoded content used the continuous strip policy.
Solution: Set `text.richText = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing()` before RTL sync and before `SetCharArray`. Added an editor source-regression test that asserts this policy assignment exists before the char-array push.
Rejected Alternatives: Forcing `richText = true` globally; disabling rich text on low tier with a binary branch; relying on whatever TMP state existed before the font swap; adding a layout/physics-style overflow system.
Scalability potential: Low devices still receive deterministic tag-density reduction through `BabelRichTextLodPolicy.ShouldStrip(textHash)`, while the TMP parser state stays compatible with the continuous global quality scalar. Middle/high/ultra can retain more authored styling without changing text truth ownership, buffer layout, or signal DTOs.
Hardware Impact: Static/source/report estimate 1150000 us. Runtime allocation delta is zero by construction; expected low-end gain is prevention of stale parser-state visual faults during staged font swaps. Profiler proof absent.
