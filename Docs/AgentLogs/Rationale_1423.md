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

## Decision 036 - Remaining TMP Char-Array Sinks RichText Ownership

Problem: A follow-up scan found more TMP sinks that call `SetCharArray` after Babel/localization decode but did not own the TMP rich-text parser state locally. `SuitHUDV4CanvasOverlay`, `LocalizedWorldSign`, and the PDA decrypt label could inherit stale `TMP_Text.richText` from an earlier UI owner. PDA decrypt is stricter: scrambled glyphs must not be parsed as TMP tags at all.
Solution: `SuitHUDV4CanvasOverlay.SetLocalizedRtlState` now derives `label.richText` from `BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing()` before RTL sync. `LocalizedWorldSign.RefreshLocalizedText` sets the same policy before `SetCharArray`. `PDADataArchaeologyDecryptLabel` forces `targetText.richText = false` in `Awake`, `RenderHash`, and `Clear` because decrypt/scramble output is not authored rich text.
Rejected Alternatives: Leaving parser state inherited from prefab or previous owner; disabling all rich text globally; using a binary low-end switch; attempting a runtime tag sanitizer over scrambled PDA glyphs.
Scalability potential: Low devices keep deterministic authored tag-density reduction through `ShouldStrip(textHash)` where authored rich text is valid. Middle/high/ultra retain more styling through the same continuous scalar. PDA decrypt remains an intentionally cheap visual fake and does not consume rich-text parser budget on any tier.
Hardware Impact: Static/source edit estimate 1320000 us. Runtime allocation delta is zero; expected low-end gain is prevention of visual/parser faults without extra buffers. Profiler proof absent.

## Decision 037 - Staged Babel Commit Lock Release Proof

Problem: `LocRegistry.TryCommitStagedBabelDictionary` had many post-validation return paths that manually called `AbortBabelDictionaryStage`. The code was probably recoverable, but it did not satisfy the evidence requirement that a staged DataVault lock/handle is released from one proofable `finally` block.
Solution: Wrapped the validated commit body in `try/finally` and moved the final staged release to `AbortBabelDictionaryStage()` at `LocRegistry.cs:799-801`. Invalid stage descriptors still return before the finally and do not abort a potentially unrelated stage. Once the stage identity is validated, every exit releases the staged buffer route.
Rejected Alternatives: Duplicating `AbortBabelDictionaryStage()` on every branch; adding a catch that swallows corruption; expanding the staged dictionary buffer; moving the entire async import system without a route card.
Scalability potential: Same staged dictionary path on low through ultra devices. Quality may affect presentation density, not dictionary ownership or lock correctness.
Hardware Impact: Static/source edit estimate 380000 us. Runtime cost is one `finally` frame during dictionary commit, outside per-frame subtitle presentation. It prevents lock leakage on early failure paths.

## Decision 038 - Residual Fault Boundary

Problem: The deeper audit found violations that are real but not safe to hot-patch inside this pass without cross-domain contract migration: legacy read-style `LocRegistry` APIs still decode into compatibility rings; `SubtitleCueSignal` is declared in a Core.Contracts namespace while physically owned by a UI file; `SubtitleManager` still exposes managed string compatibility APIs and a managed `OnCueChanged` event.
Solution: Documented these as residual faults in `Docs/Reports/UI_ZERO_GC_CONTINUATION7_AUDIT_1423.json` and final status. I did not rename public APIs, move signal DTO files, or remove compatibility methods while the project cannot compile and other agents own adjacent domains. The active VWS route remains hash/DTO based and does not carry managed subtitle text.
Rejected Alternatives: Performing a broad public API rename without route-card proof; moving `SubtitleCueSignal` across assembly/namespace boundaries during a known blocked build; claiming the compatibility APIs are harmless.
Scalability potential: The repaired hot presentation route scales continuously through `GlobalQualityWeight`. The residual API cleanup should be handled as a route migration so low/middle/high/ultra behavior stays identical at the data-contract level.
Hardware Impact: Report/audit estimate 520000 us. Runtime gain from this decision is zero because it is a boundary decision; it prevents a risky patch under active integration contention.

## Decision 039 - Subtitle Cue Signal Physical Contract Ownership

Problem: `SubtitleCueSignal` was declared in namespace `Hecton8.Core.Contracts.Signals` but physically lived in `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`. That violates one-route ownership for a VWS/audio-to-UI typed lane: the contract namespace said Core, the source file said UI implementation.
Solution: Moved `SubtitleCueSignal` into `Assets/_Project/Scripts/Core/Contracts/Signals/SubtitleCueSignal.cs` with unchanged explicit 64-byte layout. Added lane constants to the contract (`ExpectedCapacity=32`, `MaxFrameSignals=64`, `LowTierFrameSignals=64`, `LaneHash=0x53554331`) and changed `BabelSubtitleSyncRuntime` to configure `SignalBus<SubtitleCueSignal>` from those constants.
Rejected Alternatives: Leaving the namespace/file ownership mismatch; moving all subtitle presentation runtime into Core; changing payload fields; keeping duplicated lane constants in UI.
Scalability potential: Low tier and high tier consume the same bounded unmanaged lane. Continuous quality may change presentation density after consumption, never lane layout, capacity truth, or hash identity.
Hardware Impact: Static/source edit estimate 840000 us. Runtime allocation delta is zero; the change removes ownership ambiguity without adding work to the hot VWS route.

## Decision 040 - Scanner Proof And Current Batch Boundary

Problem: `OOP_Voice_Scanner_X_011` hard-coded proof that `SubtitleCueSignal` exists inside `BabelSubtitleSyncRuntime.cs`, so the correct contract move would have produced stale scanner evidence. Also, current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="1423">`; it currently starts with another agent prompt.
Solution: Updated the scanner to read `Assets/_Project/Scripts/Core/Contracts/Signals/SubtitleCueSignal.cs` and assert the UI runtime file no longer declares the signal. Added a 1423 editor source-regression for this physical ownership. Kept the last verified 1423 prompt hash in reports and explicitly marked current-batch 1423 extraction as unavailable instead of absorbing another agent's prompt.
Rejected Alternatives: Reporting old scanner semantics; rewriting unrelated batch files; switching domains to the neighboring 1400 prompt; claiming prompt re-extraction succeeded when it did not.
Scalability potential: Not runtime-facing. It protects verification integrity while the actual VWS route remains bounded and continuous-quality-safe.
Hardware Impact: Static/source/report estimate 1450000 us. Build was not launched because CPU sampled 82.00% with active `dotnet.exe` PID 30956.

## Decision 041 - Audio Caption Hash-Only Request

Problem: The spatial audio caption lane still carried a managed `CaptionText` reference in `AudioCaptionRequest`. Current captions are static constants, but the deferred request contract made managed text payloads acceptable in a presentation hot path and forced `AudioCaptionOverlay` helper methods to consume `string`.
Solution: Removed `CaptionText` from `AudioCaptionRequest`. The request now carries `CaptionHashId`, spatial origin, AUP fallback state, duration, and intensity only. `AudioCaptionEvents.TryResolveCaptionTextSpan` resolves the static caption constants as `ReadOnlySpan<char>` at the UI edge, and `AudioCaptionOverlay` writes/hash-compares fixed buffers from that span.
Rejected Alternatives: Keeping the static string bridge because it currently points at interned constants; adding a new managed cache; migrating the entire audio caption listener system to `SignalBus<T>` inside this patch without a route card.
Scalability potential: Low devices avoid deferred managed text references and still get bounded fail-closed caption truncation. Middle/high/ultra devices can spend quality budget on presentation density, not payload ownership. No binary tier branch was added.
Hardware Impact: Static/source edit estimate 980000 us. Runtime allocation delta is expected to be zero or positive because request payloads no longer retain a managed text field, but profiler proof is absent.

## Decision 042 - Continuation9 Build Wall And Report Boundary

Problem: CONT9 source and report artifacts needed a real build decision. Build throttling allows compilation only when CPU is under 50% and no compiler/dotnet process is active; prior passes were blocked by host load. A build was finally allowed, but the project still did not compile.
Solution: Sampled CPU at 10.00% and confirmed no active `dotnet`, `csc`, or `VBCSCompiler` process. Ran exactly one `dotnet build Hecton8.Core.csproj --nologo -clp:ErrorsOnly -maxcpucount:1`. Build failed after 96.4 s with 56 errors in out-of-domain `Assets/_Project/Scripts/ModularEquipmentEngine.cs:1284-1311` (`CS8168`, `CS8350`). Wrote forensic evidence to `Docs/AgentLogs/Dump_1423_CONT9_BUILD_FAILURE.txt` and refreshed all 1423 JSON hashes with this failure state.
Rejected Alternatives: Running a second build, patching `ModularEquipmentEngine.cs` from the localization/VWS agent domain, claiming tests or profiler ran, or leaving reports with the obsolete `dotnet_build_launched=false` state.
Scalability potential: Not runtime-facing. The report now preserves truthful verification state while the game build is blocked outside this domain.
Hardware Impact: Build wall time 96400 ms; report/build evidence estimate 1900000 us. Runtime status remains unmeasured.

## Decision 043 - Audio Caption Legacy String Resolver Purge

Problem: CONT9 removed managed caption strings from `AudioCaptionRequest`, but `AudioCaptionEvents.ResolveCaptionText(uint)` still exposed a public managed `string` resolver and the caption hash fields were computed from named `*CaptionText` string constants during static initialization.
Solution: Deleted `ResolveCaptionText(uint)`, removed the named caption text constants, replaced the hash fields with precomputed `public const uint` FNV-1a values, and kept `TryResolveCaptionTextSpan` as the only production caption text resolver. Source graph scan found no production callers of `ResolveCaptionText`.
Rejected Alternatives: Keeping the method because it returned interned constants; marking it obsolete while still preserving a managed string route; routing through `LocHash.Compute(CaptionText)` at type initialization after the hot request path was already hash-only.
Scalability potential: Low devices avoid the compatibility string route and cold static hash work. Middle/high/ultra behavior is identical: the route remains hash plus bounded span presentation, with quality controlled elsewhere by `GlobalQualityWeight`.
Hardware Impact: Static/source edit estimate 760000 us. Runtime allocation proof remains absent; expected gain is removal of one managed API surface and static hash computation from this caption route.

## Decision 044 - Continuation10 Verification Boundary

Problem: After deleting the legacy resolver, proof artifacts needed fresh hashes, but compilation was not permissible under current host state and known dependency wall.
Solution: Sampled CPU at 87.00% and found no active `dotnet`, `csc`, or `VBCSCompiler` process. No build launched because CPU exceeded the 50% throttle and the latest build already fails in out-of-domain `ModularEquipmentEngine.cs`. Emitted `UI_ZERO_GC_CONTINUATION10_AUDIT_1423.json` and refreshed AST/APEX/final reports.
Rejected Alternatives: Re-running a known-blocked build under CPU >50%; claiming source-regression tests executed; leaving CONT9 hashes after CONT10 source edits.
Scalability potential: Not runtime-facing. The caption route stays cheap and deterministic across low/middle/high/ultra devices.
Hardware Impact: Report/check estimate 1350000 us. Runtime status remains unmeasured.

## Decision 045 - Audio Caption Babel-First Fixed Ring

Problem: The audio caption route was hash-only, but the queue storage still used nullable lazy payload arrays behind `EnsureInitialized`, and the accept/dispatch guard still proved text existence only through built-in English spans. That left two faults: hidden runtime allocation risk around event registration/enqueue and rejection of future Babel-only caption hashes before the UI writer could decode them.
Solution: Preallocated `_pendingEvents` and `_nextFrameEvents` as fixed static readonly `AudioCaptionPayload[32]` rings, removed `AudioCaptionEvents.EnsureInitialized`, added `HasCaptionText` with `LocRegistry.TryGetLocalizedSpan` first and fixed English fallback second, added `TryWriteCaptionText(uint, Span<char>, out int, out int, out bool)`, and changed `AudioCaptionOverlay.HandleCaptionRequested` to decode directly into `slot.TextBuffer.AsSpan()` before `TMP_Text.SetCharArray`.
Rejected Alternatives: Migrating `IAudioCaptionEventListener` to a new `SignalBus<T>` inside this patch without a route-card review; keeping lazy arrays because registration is usually cold; resolving text through a managed string bridge; rejecting Babel-only caption hashes until a later localization content pass.
Scalability potential: Low devices keep a bounded 32-event ring and a 128-char per-slot buffer with fail-closed ellipsis for fallback captions. Middle devices keep the same route with normal cadence. High and Ultra devices can spend saved presentation budget on richer HUD caption styling through the existing continuous `GlobalQualityWeight` policies without changing payload ownership or caption truth.
Hardware Impact: Static/source/report estimate 1500000 us. Expected i3/MX350 benefit is removing hidden queue allocation surface and one old span-copy/hash path from spatial caption presentation. Runtime GC/profiler proof remains absent.

## Decision 046 - Audio Caption Double Lookup Purge

Problem: The CONT11 Babel-first writer still paid for two localization probes on the localized route. `TryWriteCaptionText` first called pure `LocRegistry.TryGetLocalizedSpan`, then called `TryWriteVisualSpanFromUtf8(captionHashId, ...)`, which repeats the tracked lookup before decode. `Dispatch` also revalidated caption text after `Enqueue` already accepted the hash.
Solution: Added `LocRegistry.TryWriteKnownLocalizedSpanFromUtf8` so a caller that already holds a valid `ReadOnlySpan<byte>` can decode it directly into a caller-owned char span. Changed `AudioCaptionEvents.TryWriteCaptionText` to use that known-span writer and removed the redundant `Dispatch` `HasCaptionText` guard. Source regression now asserts no second tracked lookup in this path.
Rejected Alternatives: Keeping the duplicate lookup because the source is small; caching `ReadOnlySpan<byte>` inside `AudioCaptionPayload` where span lifetime and struct storage would be invalid; migrating the entire listener dispatch to `SignalBus<T>` without a route-card; running `dotnet build` while CPU was 93% and `dotnet.exe` PID 62104 was active.
Scalability potential: Low devices remove one localized-caption table lookup and one dispatch-side existence probe. Middle devices keep the same bounded caption cadence. High and Ultra devices can spend saved UI budget on existing `GlobalQualityWeight`-driven caption styling; no binary low-end/high-end branch or physical simulation was added.
Hardware Impact: Static/source/report estimate 1250000 us. Measured runtime savings and runtime GC bytes remain absent because compilation/profiler verification is blocked.

## Decision 047 - Audio Caption Managed Listener Purge

Problem: The caption payload no longer carried managed strings, but `AudioCaptionEvents` still owned an `IAudioCaptionEventListener` object array and invoked managed callbacks from the deferred caption route. That violated the zero-GC/VWS route intent even though the immediate allocation count was not proven bad.
Solution: Removed the audio caption listener interface, listener slot array, object Register/Unregister, and callback Dispatch. `AudioCaptionOverlay` now registers as an integer-counted pull consumer only when UI and late-frame ticking are active, then drains fixed-ring payloads through `ConsumeNextPendingCaption` during `LateFrameTick`. The dispatcher `FlushPending` remains a compatibility/drop hook and does not deliver managed callbacks.
Rejected Alternatives: Adding a new `SignalBus<T>` lane without a route-card; keeping callbacks because they no longer carry strings; registering a managed overlay object reference in `AudioCaptionEvents`; building while CPU was 100% and `dotnet.exe` PID 18776 was active.
Scalability potential: Low devices process at most the existing 32 queued caption payloads and use the late-frame dispatch budget before each drain. Middle/high/ultra retain the same data route and can spend `GlobalQualityWeight` budget on visual styling, not payload truth or listener topology. No binary device switch was added.
Hardware Impact: Static/source/report estimate 1450000 us. Expected i3/MX350 gain is removal of managed callback/interface traversal from the caption route and no per-frame queue clear when no payloads exist. Runtime profiler proof remains absent.

## Decision 048 - VWS Fallback Ownership And Terminal CharArray Purge

Problem: A follow-up domain scan found two remaining managed-string ownership problems. `AudioCaptionEvents` still physically owned fixed English fallback caption literals even after the caption route became hash/span based, and the submarine OS/BIOS terminal components cloned static text literals into managed `char[]` arrays with `.ToCharArray()` during cold initialization.
Solution: Added `VwsCaptionFallbackCatalog` as the explicit fallback owner and changed `AudioCaptionEvents` to expose only hash aliases plus delegated `TryResolveCaptionTextSpan`. Replaced submarine OS and BIOS static `char[]` literals with `ReadOnlySpan<char>` expression properties and bounded `AppendSpan`/`CopySpan` writes into existing buffers. Added editor source-regression checks for both the fallback ownership boundary and `.ToCharArray()` removal.
Rejected Alternatives: Removing the fallback text before the Babel runtime/static-data path proves caption keys exist; adding JSON-only keys that the current static artifact route may not load; keeping cold `.ToCharArray()` clones because they are not per-frame; rewriting terminal text into a physical/layout simulation instead of bounded visual text buffers.
Scalability potential: Low devices avoid cold managed array clones for terminal labels and keep a fixed fallback route for missing caption data. Middle devices keep identical bounded buffers. High and Ultra can spend `GlobalQualityWeight` budget on existing rich text/canvas styling paths, not on text truth ownership. No binary `isLowEnd` branch was added.
Hardware Impact: Static/source/report estimate 1350000 us. Expected i3/MX350 benefit is smaller managed cold-init surface and clearer fallback ownership. Runtime GC/profiler proof remains absent. Build was not launched because CPU sampled 100% even though no active `dotnet`/`csc`/`VBCSCompiler` process was found.

## Decision 049 - Small UI Static CharArray Purge

Problem: Broad UI runtime scan after CONT14 still found cold `.ToCharArray()` clones in small UI components: loading screen labels, save preview fallback labels, builder overlay title/template, and PDA Atlas timer/numeric templates.
Solution: Replaced those static `char[]` clones with `ReadOnlySpan<char>` literal/template properties and copied the spans into existing instance buffers before `SetCharArray` or `LocNumericBuffer.TryWrite`. The changed-file scan now reports 0 `.ToCharArray()` and 0 accidental one-argument `SetCharArray(span)` calls in those four files.
Rejected Alternatives: Blindly converting `SuitHUDV4CanvasOverlay` in the same patch; that file has 29 remaining `.ToCharArray()` hits and one mutable static memory-breach hex buffer. Changing it safely requires a local mutable instance buffer route, not a mechanical span property. `LocRegistry` also still exposes one missing-key `char[]` fallback because legacy `TryGetVisualBuffer` returns `char[]`.
Scalability potential: Low devices avoid small cold managed array clones during UI screen bootstrap. Middle/high/ultra behavior is unchanged. This does not alter gameplay truth, save identity, DTO layout, or authority routes.
Hardware Impact: Static/source/report estimate 1250000 us. Expected i3/MX350 gain is reduced cold managed UI allocation surface. Runtime profiler proof remains absent. Build was not launched because CPU sampled 100% and active `dotnet.exe` PID 13464 was present.

## Decision 050 - Terminal Boot Span Status Purge

Problem: Fresh source scan superseded stale CONT32 `.ToCharArray()` residuals, but found a live 1423-domain contract smell in terminal boot presentation: `TerminalBootSequence` built a TMP payload through string-typed status variables and `AppendString(string)`, while `HectonOSBootManager` private resolvers returned strings only to call `.AsSpan()`.
Solution: Converted those private status/vector routes to `ReadOnlySpan<char>` and kept all writes bounded through existing preallocated `char[]` buffers before `TMP_Text.SetCharArray`. No public API changed, no new DataVault route, no new signal, no new managed queue.
Rejected Alternatives: Writing another JSON proof artifact, deleting compatibility string APIs outside the local terminal builders, or running `dotnet build` while CPU was 100% and `VBCSCompiler.exe` PID 53464 was active.
Scalability potential: Low devices keep the same cheap terminal fake with fewer managed text surfaces. Middle/high/ultra devices keep identical payload truth and may spend `GlobalQualityWeight` budget on presentation styling elsewhere; no binary quality switch was added.
Hardware Impact: Static/source scan estimate 45000000 us including broad domain and hot-method scans. Runtime GC/profiler proof remains absent.

## Decision 051 - Terminal Boot Source Regression Lock

Problem: The current terminal boot source already satisfies the span-status contract, but without a C# guard a later cleanup could reintroduce `AppendString` or private string-return resolvers while static reports stayed stale.
Solution: Added `ZeroGCSubtitleFormatter1423EditTests.TerminalBootStatusText_UsesSpanStatusRoutesNotStringBridges`, asserting the `TerminalBootSequence` span append/status route and `HectonOSBootManager` span boot/status resolvers. Production source scan over the guarded files reports 0 hits for the forbidden string bridge patterns.
Rejected Alternatives: Writing another JSON/report artifact, running a build while CPU sampled 97.10% with active `VBCSCompiler.exe` PID 53464, or adding a runtime reflection/profiler check that cannot execute while the project compile wall remains out of domain.
Scalability potential: Not gameplay-facing. The guard preserves the cheap visual terminal fake across low/middle devices and leaves high/ultra presentation budget to existing continuous quality systems rather than text allocation bridges.
Hardware Impact: Static/source edit estimate 620000 us. Runtime GC/profiler proof remains absent because build/test execution is still blocked by compilation throttle and the known out-of-domain compile wall.

## Decision 052 - Runtime Domain Forbidden Text Bridge Regression

Problem: One-file terminal checks do not prevent future runtime UI/Audio/Babel regressions from reintroducing managed text bridge APIs in adjacent files after reports become stale.
Solution: Added `RuntimeUiAudioBabelDomain_HasNoForbiddenManagedTextBridgePatterns`, a cold editor source-regression that scans runtime UI, Audio, `LocRegistry.cs`, and `SpatialAudioManager.cs`, skipping `/Editor/`, for forbidden managed text bridge tokens. A separate `rg --pcre2 --glob '!**/Editor/**'` scan over the same domain returned 0 hits.
Rejected Alternatives: Emitting another JSON audit, scanning Editor tooling as if it were runtime, or launching compilation while CPU was 72.02% even though no compiler/dotnet process was active.
Scalability potential: Not runtime-facing. The guard protects low/middle devices from text allocation drift and leaves high/ultra devices to spend quality budget on existing presentation systems, not bridge churn.
Hardware Impact: Static/source edit estimate 900000 us. No measured runtime microseconds saved; it is a compile-time regression tripwire.

## Decision 053 - Vehicle Sub OS Button Lock Flattening

Problem: `VehicleSubOsCockpitRuntime` scheduled a Burst `IJobParallelFor` for only 32 cockpit buttons and held six DataVault write locks across the scheduled work. That violated lock flattening and overpaid scheduling/fence overhead for a tiny visual-only animation.
Solution: Replaced the job with deterministic same-phase button kinematics using fixed preallocated scratch arrays, then published state/progress/offset/matrix snapshots through sequential single-lock helpers. `PressCockpitButton` now reads immutable snapshots and writes target/state through one-lock calls with rollback of target on state-write failure. Telemetry acquire failure release now also uses `finally`.
Rejected Alternatives: Keeping the tiny job because it was Burst-compiled; combining all button fields into a new DTO buffer inside this patch; holding multiple DataVault locks and relying on lock order discipline.
Scalability potential: Low devices avoid job scheduling and cross-frame lock residency for a 32-element visual fake. Middle/high/ultra keep identical button visuals and can spend budget on cockpit presentation elsewhere. No gameplay truth or authority route changed.
Hardware Impact: Static/source edit estimate 2200000 us. Runtime microseconds are not measured; expected gain is removal of a tiny job and six simultaneous write-lock residency windows.

## Decision 054 - Generic DataVault Writer Constraint Alignment

Problem: The new `TryWriteCockpitVaultBuffer<T>` helper was declared with `where T : struct` while calling `IsExactVaultHandle<T>`, whose contract is `where T : unmanaged`. That is a compile-time constraint mismatch in the 1423 patch surface.
Solution: Changed the writer helper to `where T : unmanaged`, matching `TryReadCockpitVaultBuffer<T>`, `TryReadMutableCockpitVaultBuffer<T>`, `ReleaseCockpitVaultHandle<T>`, and the DataVault handle validator.
Rejected Alternatives: Waiting for `dotnet build` to discover an obvious generic constraint defect; loosening `IsExactVaultHandle<T>` back to `struct`; bypassing handle validation.
Scalability potential: Not runtime-facing. It preserves the same single-lock writer route for low/middle/high/ultra devices without adding allocations, jobs, or device-tier branches.
Hardware Impact: Static/source edit estimate 180000 us. Build was not launched because CPU sampled 100.00%; no active compiler/dotnet process was present, but the throttle forbids compilation above 50%.

## Decision 055 - Font Streaming Prefetch Lock And Phase Purge

Problem: `FontStreamingManager` still carried a visual font-swap prefetch path through DataVault-owned hash/slice buffers and a scheduled lookup job. Earlier variants held write locks; the current source had moved toward resolved handles, but the architecture was still heavier than the visual cheat warranted and left teardown/fence complexity inside a UI LateFrame owner. The language-change callback also wrote TMP status/alpha immediately instead of transferring state for LateFrame presentation.
Solution: Deleted the FontStreamingManager DataVault prefetch buffers, job handle, buffer lifecycle, and job dispatch. Added `LocRegistry.ResolveVisibleTextOffsetPrefetchBudget` and `TryResolveVisibleTextOffsetSlice` so `CollectSwapQueue` can resolve only a continuous-quality-bounded number of UTF-8 slices during the LateFrame swap queue build. Extended `LabelSwapScheduler.Enqueue` to carry an optional prefetched slice in its existing fixed queue entry. Removed direct `UpdateStatusLabel` and `ApplyVisibleAlpha` from `HandleLanguageChanged`; the callback now changes state only and LateFrame performs presentation through the existing readiness/swap flow.
Rejected Alternatives: Reintroducing `try/finally` around a DataVault job buffer route; holding two write locks in a stable order; keeping the job because it was Burst-visible; letting language-change events touch TMP immediately for faster feedback.
Scalability potential: Low devices do no job scheduling and only resolve as many slices as `GlobalQualityWeight` allows. Middle devices prefetch a larger bounded prefix. High and Ultra keep the same route and spend the saved budget on richer TMP/rich-text presentation elsewhere. No binary device switch, gameplay-truth route, DTO layout, or save identity changed.
Hardware Impact: Static/source edit estimate 1500000 us. Expected low-end gain is removal of a job/fence/DataVault buffer path from a visual font swap and fewer phase-crossing presentation writes. Runtime microseconds and GC bytes remain unmeasured because the project still has an out-of-domain build wall and CPU sampled 100%.

## Decision 056 - Language Event Presentation Deferral

Problem: `PDADataLogTab.HandleLanguageChanged` and `PDAShellChrome.HandleLanguageChanged` still performed presentation refresh work from the language-change callback path. That created phase ambiguity: localization state could mutate UI labels, lists, detail text, and chrome text before the LateFrame visual sync point.
Solution: Added `_localizedPresentationDirty` and `_localizedChromeDirty` flags. Language-change handlers now only transfer state. `PDADataLogTab.LateFrameTick` performs narrative reset, localization cache rebuild, static text application, list/detail refresh, and play-button refresh. `PDAShellChrome.LateFrameTick` performs localized text cache refresh and chrome refresh before the closed-PDA early return.
Rejected Alternatives: Leaving immediate refreshes because the callback is infrequent; moving the work into `Tick`; allocating a managed command queue. The state transfer is one or two bool writes and does not allocate.
Scalability potential: Low devices can coalesce multiple language/chrome invalidations into the next LateFrame. Middle/high/ultra keep identical visual results and can spend quality budget on presentation effects after the simulation phase has settled.
Hardware Impact: Static/source edit estimate 760000 us. Runtime microseconds are not measured. Expected low-end gain is fewer phase-crossing UI refreshes and less duplicate presentation work during language swaps.

## Decision 057 - PDA Decryption Native Init Lock Flattening

Problem: `PDADecryptionSpectrogramPanel.EnsureNativeResources` acquired the stage-target DataVault write lock and then acquired the telemetry-ring write lock before releasing the first one. That is a direct two-lock residency window during native resource initialization.
Solution: Split initialization into two sequential lock scopes. Stage targets are acquired, cleared, and released in a `finally`. Only after that release does the method acquire telemetry, clear it, set `_nativeReady`, and release telemetry in its own `finally`. `ClearNativeState` was split into `ClearStageTargets` and `ClearTelemetryRing`.
Rejected Alternatives: Relying on a stable lock order; keeping a `telemetryLocked` bool inside a nested lock; merging both buffers into a new DTO in this patch. Lock flattening was sufficient and lower risk.
Scalability potential: Low devices avoid a deadlock vector during PDA scanning resource rebuilds. Middle/high/ultra keep the same cheap spectrogram visual fake and scale point count through existing continuous quality policy; no binary device switch was added.
Hardware Impact: Static/source edit estimate 520000 us. Runtime microseconds are not measured; expected benefit is reduced lock residency and deadlock surface, not a visible frame-time win.

## Decision 058 - Wrist HUD Scratch Publish And Fail-Closed State

Problem: `WristHologramHudRuntime` held multiple DataVault write locks for one visual HUD frame and separately held acoustic/counter locks during acoustic tap injection. The first lock-flattening pass introduced scratch publishers but still allowed a bad publish order: a failed quad-buffer publish could be followed by a state publish advertising the new `ActiveQuadCount`, making the draw path read stale quad DTOs as current data.
Solution: Deleted the multi-lock frame/acoustic acquire helpers. HUD frame construction now reads the font atlas and builds into preallocated scratch arrays/spans. Sequential publishers write quads, telemetry, counters, and state through one lock each. Publisher methods return `bool`; if quad publish fails, `PublishWristHudScratch` sets `ActiveQuadCount = 0` and `StateFlagGpuUploadFault` before state publish.
Rejected Alternatives: Holding all locks in a fixed order; using a tiny job for the scratch copy; publishing state first for immediacy; leaving stale quads visible. The correct visual failure mode is no quads for that frame, not stale quads under a fresh state.
Scalability potential: Low devices avoid multi-lock residency and job/fence overhead for a wrist HUD visual fake. Middle devices keep stable HUD cadence. High and Ultra keep the same truth route and can spend saved budget on richer hologram distortion through the existing `GlobalQualityWeight`-controlled math LOD and presentation parameters.
Hardware Impact: Static/source edit estimate 1800000 us. Runtime profiler proof is absent. Expected low-end gain is shorter DataVault write-lock residency and no same-frame multi-lock deadlock vector.

## Decision 059 - Integrator Static Validation Boundary

Problem: The user requested proof without JSON/binary reports and with strict build throttling. A previous broad method-body scanner timed out, so claiming AST or complete method-body proof would be false.
Solution: Used bounded source scans and explicit method counters. Modified runtime files have 10 hot method declarations by `rg`; direct `GlobalRegistry.Get<` and non-`TryGetComponent<` scan over all modified runtime files returned 0 hits anywhere in those files. Runtime UI/Audio/Babel managed text bridge scan returned 0 hits. Lock counters proved `PDADecryptionSpectrogramPanel.EnsureNativeResources ACQ=2 REL=2 FINALLY=2`, `WristHologramHudRuntime.BuildTextQuadsOwnerPhase ACQ=0`, and each Wrist publisher `ACQ=1 REL=1 FINALLY=1`.
Rejected Alternatives: Writing new JSON reports; claiming Roslyn AST after PowerShell parser failure; running `dotnet build` while CPU was 100% and `dotnet.exe` PID 55948 was active.
Scalability potential: Not runtime-facing, but it preserves the proof boundary needed for low/middle/high/ultra quality paths to remain decoupled from build contention and report churn.
Hardware Impact: Static/check estimate 2100000 us. Build was not launched. Runtime GC/profiler proof remains absent.

## Decision 060 - PDA Projection Scratch Publish Lock Flattening

Problem: A continuation DataVault scan found a live 1423-domain lock violation in `WristHologramHudRuntime_PdaScreenProjector`. `PdaProjectorLateFrameTick` acquired state, input, telemetry, and cursor write locks together before compiling a purely visual screen-space PDA projection. `EnsurePdaProjectionNativeBuffers` acquired six write locks together just to seed tuning/profile/telemetry buffers.
Solution: Removed the `acquiredMask` helpers and moved PDA projection computation into preallocated owner scratch arrays. LateFrame now builds input/state/telemetry in scratch spans and publishes via `TryWritePdaProjectionVaultBuffer<T>`, which has exactly one `TryAcquireWriteLock`, one `ReleaseWriteLock`, and one `finally`. Native init seeds tuning, telemetry, cursor, and profiles through the same sequential one-lock flush helpers. Publish failure does not upload GPU payload; it attempts a fail-closed state write with `PdaProjectionFlagGpuUploadFault`.
Rejected Alternatives: Relying on stable lock order; retaining the old multi-lock helpers because projection is visual-only; adding a job or new DataVault DTO route; patching unrelated acoustic portal locks inside `SpatialAudioManager` without DSP Acoustic Radar ownership.
Scalability potential: Low devices avoid multi-lock residency for a visual PDA projection fake and keep matrix work local to LateFrame scratch. Middle devices keep the same cadence. High and Ultra can spend existing `GlobalQualityWeight` budget on refraction/curvature/profile visuals without changing truth ownership or introducing binary tier branches.
Hardware Impact: Static/source edit estimate 1700000 us. Runtime microseconds and GC bytes are not measured. Build was not launched because CPU sampled 96.00%; no active compiler/dotnet process was present, but the throttle forbids compilation above 50%.

## Decision 061 - Beacon Unit Label Span Route Purge

Problem: `BeaconHUDElement.RebuildLocalizationCache` still built its distance pattern through `string unitLabel = LocalizedMeasurementFormatter.ResolveDistanceUnitLabel(...)`. After the Beacon caller was fixed, `LocalizedMeasurementFormatter` still exposed unused public managed-string unit-label resolvers that cold-read `GlobalRegistry.LocalizationText`, leaving a future hot-path regression route.
Solution: Changed Beacon distance-cache rebuild to consume `ResolveDistanceUnitLabelSpan(_distanceLanguage, manager)` and copy the returned span into `_distancePatternBuffer` with the existing bounded cursor. Removed unused public `ResolveDistanceUnitLabel` and `ResolveTemperatureUnitLabel` string resolver overloads from `LocalizedMeasurementFormatter`; only span label resolvers remain. Added source-regression coverage in `ZeroGCSubtitleFormatter1423EditTests`.
Rejected Alternatives: Keeping the string resolvers because they currently had no callers; adding a managed cache string; deleting the fallback/key string helpers in the same patch even though they are interned-literal selectors used by the span route.
Scalability potential: Low devices avoid a managed label bridge during Beacon localization cache rebuilds. Middle/high/ultra keep identical visual behavior and existing quality routing; no gameplay truth, save identity, DTO layout, DataVault buffer, or binary quality switch changed.
Hardware Impact: Static/source edit estimate 680000 us. Runtime microseconds and GC bytes are not measured. Build was not launched because CPU sampled 100.00%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 062 - Localization Callback Phase Deferral Sweep

Problem: A fresh in-domain sweep found presentation cache rebuilds still executing directly from localization/input/service callbacks in `BeaconHUDElement`, `InteractionUI`, `SuitHUDV4CanvasOverlay`, and `AcousticEcholocationTranslator`. These callbacks are not the visual sync phase, so direct `RebuildLocalizationCache`, `RefreshLocalizedPromptCache`, `RefreshLocalizedCache`, or `InvalidateVisualCaches` calls create phase drift even when they do not allocate.
Solution: Converted the callback paths to fixed-field dirty-state transfer. `BeaconHUDElement` stores `_pendingDistanceLanguage` and applies through `ApplyPendingLocalizationRefresh` in `LateFrameTick`. `InteractionUI` queues `_promptPresentationDirty` and applies `ConfigurePromptText`/`RefreshLocalizedPromptCache` at the start of `LateFrameTick`. `SuitHUDV4CanvasOverlay` queues `_localizedPresentationDirty` and processes it inside `ProcessPendingRuntimeCanvasRefresh`, which is called from `LateFrameTick`. `AcousticEcholocationTranslator` queues `_localizedPresentationDirty` and applies `RefreshLocalizedCache` before sonar/bark presentation in `LateFrameTick`.
Rejected Alternatives: Running refreshes immediately because language changes are infrequent; adding a managed command queue; adding a DataVault buffer for UI-only dirty flags; broad rewriting of existing managed prompt strings in `InteractionUI` inside the same phase-safety patch.
Scalability potential: Low devices coalesce multiple callback invalidations into one LateFrame visual update. Middle devices preserve the current presentation cadence. High and Ultra can still spend continuous `GlobalQualityWeight` budget in existing HUD/TMP visual systems; no binary device switch, gameplay truth owner, save identity, DTO layout, or authority route changed.
Hardware Impact: Static/source edit estimate 1600000 us. Changed-runtime hot `LateFrameTick` scan reported `GlobalRegistry.Get=0` and non-Try `GetComponent=0`. Changed-runtime managed text bridge scan returned 0 hits. Changed-runtime DataVault lock scan returned 0 hits. Build was not launched: first sample CPU 69.00% with active `dotnet`/`csc`/`VBCSCompiler` process count 0; final sample CPU 100.00% with active `dotnet build Hecton8.slnx` PID 39956.

## Decision 063 - Interaction Prompt Enum And Span Route

Problem: After phase deferral, `InteractionUI.SamplePromptState` still used a managed prompt construction contract: `BuildPrompt` returned `string`, `_cachedPrompt` stored a managed prompt reference, and `UpdatePrompt(string)` copied the string into TMP. Most branches reused cached/serialized strings, but the hot route still encoded presentation identity as managed text instead of a compact source token.
Solution: Replaced the built-in prompt construction route with `PromptSource : byte`. `SamplePromptState` resolves target state to `PromptSource`, caches that enum with the existing collider/state hash, and `TryApplyPromptSource` resolves the chosen prompt as `ReadOnlySpan<char>` only at the TMP sink. `ApplyPromptSpan` expands input glyphs into the existing fixed `_promptCharBuffer` and calls `SetCharArray`; fallback copying is bounded by `min(prompt.Length, _promptCharBuffer.Length)`. The public `CurrentPrompt` and `UnityEvent<string>` remain as a compatibility bridge, invoked only when the prompt source changes.
Rejected Alternatives: Removing `UnityEvent<string>` in this patch, because designer/public contract migration needs a route card; adding a DataVault buffer for prompt identity, because this is local UI presentation state; keeping `_cachedPrompt` because it was usually a reference to an existing string.
Scalability potential: Low devices avoid string-identity churn in the prompt hot path and keep the cheap TMP char-array presentation route. Middle/high/ultra devices keep identical prompt behavior; existing continuous HUD/TMP quality systems can spend `GlobalQualityWeight` budget on presentation effects without changing gameplay truth or input authority.
Hardware Impact: Static/source edit estimate 880000 us. Runtime microseconds and GC bytes are not measured. Method-body scans over the prompt hot route report 0 `new`, `string.Format`, `.ToString(`, `foreach`, `.text =`, `GlobalRegistry.Get<`, and `GetComponent` hits. Build was not launched because CPU sampled 100.00% then 94.00%; no active compiler/dotnet process was present either time, but the throttle forbids compilation above 50%.

## Decision 064 - Wrist HUD Dead Multi-Lock Release Purge

Problem: After the Wrist HUD lock-flattening patch, source still contained unused `WristHudWriteMask*` constants and `ReleaseWristHudAcquiredBuffers(uint)`. Callers were gone, but the stale helper still encoded a multi-lock release path and made source proof weaker than the code path.
Solution: Removed the dead constants and helper. Strengthened `WristHudFrameBuild_UsesScratchBuffersAndSingleLockPublishers` so it rejects `WristHudWriteMask` and `ReleaseWristHudAcquiredBuffers` anywhere in `WristHologramHudRuntime.cs`, not only inside `BuildTextQuadsOwnerPhase`.
Rejected Alternatives: Rewriting `DiegeticGlitchSurgeonRuntime.TryResolveGlitchVaultBuffer` as a lock leak; inspection proved it uses `TryResolveHandle`/`TryReadHandle`, not `TryAcquireWriteLock`. Leaving the dead Wrist helper because it had no callers was also rejected; stale lock helpers are future regression surface.
Scalability potential: Low devices keep the current one-lock Wrist HUD fake with no stale multi-lock route. Middle devices keep identical cadence. High and Ultra remain free to spend continuous `GlobalQualityWeight` in hologram distortion and quad density without changing truth ownership, DTO layout, or lock topology.
Hardware Impact: Static/source edit estimate 520000 us. Runtime microseconds are not measured. UI write-lock method scanner found 0 methods with more than one `TryAcquireWriteLock` or missing `finally`; Wrist publishers each remain `ACQ=1 REL=1 FINALLY=1`. Build was not launched because CPU sampled 95.60%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 065 - UI DataVault Acquire Failure Finally Hardening

Problem: The lock topology scan proved no UI method held more than one write lock, but four single-lock acquire helpers still released post-acquire validation failures outside `finally`: `TryAcquireGlitchVaultWriteBuffer`, `TryAcquireExistingVaultWriteBuffer`, `TryAcquireWristHudVaultBuffer`, and `TryAcquireBlackBoxWriteBuffer`. A future exception or early return between validation and release would leave a write lock held.
Solution: Converted each helper to `releaseOnExit = true` inside a `try/finally` after successful `TryAcquireWriteLock`. Successful validation sets `releaseOnExit = false` and transfers lock ownership to the existing caller release route. Failed validation and exceptions release in `finally` and clear the out buffer.
Rejected Alternatives: Treating `ACQ=1` as mathematically sufficient; changing success ownership so helpers copy data and release immediately; adding new DataVault buffers; broad rewriting `TryResolveHandle` read/mutable alias routes that do not acquire write locks.
Scalability potential: Low devices avoid persistent lock stalls under compaction/failure edge cases. Middle devices keep the same visual cadence. High and Ultra keep identical presentation behavior and continuous `GlobalQualityWeight` controls; no binary quality switch, DTO layout, save identity, or authority owner changed.
Hardware Impact: Static/source edit estimate 740000 us. Runtime microseconds are not measured. Repaired helper counters are `ACQ=1 REL=1 FINALLY=1 releaseOnExitTokens=3` each. Broad UI write-lock method scanner found 0 methods with `ACQ>1` or `FINALLY<1`. Build was not launched because CPU sampled 99.81%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 066 - Diegetic Glitch Cold Writer Lock Route Repair

Problem: A deeper diff audit showed that several `DiegeticGlitchSurgeonRuntime` cold/default writer paths had been moved from explicit write locks to mutable `TryResolveGlitchVaultBuffer` aliases: initial state/tuning/table seeding, mock text seeding, CSV table override, and editor table reload. `TryResolveHandle` is a current-phase mutable view; using it for owner writes weakens DataVault sovereignty and bypasses the one-lock proof even if the methods are cold/editor paths.
Solution: Split cold writes into single-buffer writer scopes. `InitializeVaultDefaults` now delegates to `TrySeedGlitchStateDefaults`, `TrySeedGlitchTuningDefaults`, and `TrySeedGlitchTableDefaults`, each with one acquire, one release, and one `finally`. `SeedMockText` writes original/work buffers through `TryWriteMockTextBuffer`, one buffer at a time. `TryApplyCsvOverride` reads the editor CSV into stack memory and then locks only the glitch table while parsing into the vault buffer. `ReloadGlitchTableForEditor` now uses the same explicit table write-lock route.
Rejected Alternatives: Treating cold/editor paths as exempt; holding CSV scratch and table locks simultaneously; keeping a DataVault CSV scratch buffer lock for an editor-only file read; rewriting the 14-buffer scheduled pointer-job ABI in this same patch. The pointer-job migration needs a separate scratch/publish design because raw job pointers currently require long-lived buffer pins.
Scalability potential: Low devices avoid silent DataVault writer aliasing during initialization and editor hot reload edge cases. Middle devices keep identical visual behavior. High and Ultra keep the same glitch-table fake and continuous `GlobalQualityWeight` controls; no binary quality switch, gameplay truth owner, DTO layout, or save route changed.
Hardware Impact: Static/source edit estimate 980000 us. Runtime microseconds are not measured. Cold writer counters are `ACQ=1 REL=1 FINALLY=1 RESOLVE=0 LEGACY_LOCK=0` for the five writer helpers; `InitializeVaultDefaults` and `SeedMockText` now have zero direct lock calls and zero mutable resolve aliases. Build was not launched because CPU sampled 98.27%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 067 - Diegetic Scheduled Scratch And Babel Stage Pin Purge

Problem: The previous residual audit was correct: `DiegeticGlitchSurgeonRuntime` still held fourteen relocation-protected DataVault pins across a scheduled visual job chain. That made the DataVault proof depend on a long-lived multi-buffer pin mask. `LocRegistry` also staged async Babel dictionary bytes and override CSV scratch through DataVault buffer pins even though both are file-ingest scratch, not authoritative runtime facts.
Solution: Moved diegetic glitch frame computation to persistent H8Memory scratch buffers. LateFrame copies current vault state into scratch, jobs run only on scratch pointers, and completion publishes state/work text/text span/signals/quads/radar/synth/telemetry/cursor through sequential one-buffer write-lock helpers. External ASCII scramble leases now copy the glitch table into a dedicated scratch table and expose that pointer, not the DataVault table. Babel dictionary staging and override CSV loading now allocate H8Memory byte scratch and release it through H8Memory, removing DataVault pins from async file ingest.
Rejected Alternatives: Keeping the 14-buffer pin chain because the lock order was stable; replacing the Burst chain with synchronous main-thread loops; adding a new monolithic DataVault DTO; holding a DataVault lock around async file IO; introducing binary low/high device branches. The chosen path preserves existing jobs and visual fakes while removing long-lived DataVault pin ownership.
Scalability potential: Low devices avoid DataVault pin stalls and can skip scheduling while external leases or vault swaps are pending. Middle devices keep the same cheap triangle/hash-based glitch visual fake. High and Ultra keep the same continuous `GlobalQualityWeight` math LOD inside existing jobs and can spend saved lock/fence budget on richer presentation without changing gameplay truth, DTO layout, save identity, or authority routes.
Hardware Impact: Static/source edit estimate 2600000 us. Runtime microseconds and GC bytes are not measured. Static counters: scheduled glitch methods `LEGACY=0 RESOLVE=0`, publisher helpers `ACQ=1 REL=1 FINALLY=1`, LocRegistry stage/CSV methods `LEGACY=0`. Build was not launched because CPU sampled 69.05%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 068 - Diegetic Scratch NativeArray Alias Purge

Problem: CONT50 removed DataVault pins but left persistent `NativeArray<T>` scratch fields in `DiegeticGlitchSurgeonRuntime`. That still created long-lived managed struct aliases to unmanaged memory inside a runtime manager and weakened the Data Sovereignty proof.
Solution: Replaced the persistent scratch `NativeArray<T>` fields with raw pointers allocated by `H8Memory.AllocateRaw` and released by `H8Memory.FreeRaw`. `EnsureGlitchScratchResources` runs from cold native resource initialization and only null-checks after that. Hot frame load copies read snapshots into raw scratch; jobs consume raw scratch pointers; completion publishes through sequential one-buffer DataVault write-lock helpers with `finally` release.
Rejected Alternatives: Keeping `NativeArray<T>` fields because they are convenient for `GetUnsafePtr`; returning to DataVault pinned buffers; replacing the job chain with synchronous main-thread loops; adding a monolithic scratch DTO in DataVault; launching a build while CPU sampled above the throttle.
Scalability potential: Low devices keep short DataVault residency and no persistent relocation alias fields. Middle devices keep identical glitch cadence. High and Ultra keep the same continuous `GlobalQualityWeight` math LOD in existing jobs and can spend saved lock/fence budget on richer presentation without binary device switches or gameplay truth changes.
Hardware Impact: Static/source edit estimate 680000 us. Runtime microseconds and GC bytes are not measured. Verification counters: scheduled glitch hot methods report zero write locks, legacy pins, mutable resolves, forbidden text allocations, and raw allocation; `PublishGlitchScratchBuffer<T>` has one writer helper, one release, and one `finally`; `EnsureGlitchScratchPointer<T>` is the sole raw allocation site and is cold init. Build was not launched because CPU sampled 92.59%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.

## Decision 069 - HUD Notification Span Sink And Queue Lock Finally

Problem: A fresh UI sink audit found that `HUDNotification` still exposed `ShowWarning/ShowCritical/ShowInfo(string)` and a private `Enqueue(string, NotificationSeverity)` path. This kept a managed string entrypoint directly on the runtime HUD owner even though span and `FixedCharBuffer` routes already existed. The same file also had `TryAcquireQueueWrite` releasing a validation-failed DataVault write lock outside `finally`.
Solution: Removed the string entrypoints from `HUDNotification`; `ToolHitUtility` and `HectonAPI.UI` compatibility string APIs now convert to `ReadOnlySpan<char>` before entering the HUD sink or `NotificationEvents`. `HUDNotification.TryAcquireQueueWrite` now uses `releaseOnExit` inside `try/finally`; failed validation releases inside `finally`, while successful validation transfers lock ownership to caller scopes that already release in `finally`.
Rejected Alternatives: Deleting public mod/tool string compatibility APIs without a migration route; keeping the HUD runtime owner as the managed string boundary; treating the queue validation branch as too small to need `finally`; launching a build while CPU sampled at 100%.
Scalability potential: Low devices avoid string-API drift at the HUD sink and remove a lock-leak edge case in the notification queue. Middle devices keep identical notification behavior. High and Ultra retain the same continuous presentation quality path; no gameplay truth owner, DTO layout, save identity, authority route, or binary quality switch changed.
Hardware Impact: Static/source edit estimate 820000 us. Runtime microseconds and GC bytes are not measured. Static counters show changed player-runtime text bridge tokens 0, cold lookup tokens 0, serialized removed method bindings 0, and HUD queue writer `ACQ=1 REL=1 FINALLY=1`. Build was not launched because CPU sampled 100.00%; active compiler/dotnet process count was 0, but throttle forbids compilation above 50%.
