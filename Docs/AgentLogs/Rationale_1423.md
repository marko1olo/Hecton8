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
