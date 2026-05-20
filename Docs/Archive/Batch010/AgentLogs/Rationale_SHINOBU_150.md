# Rationale_SHINOBU_150

Status: PENDING VERIFICATION / COMPILE BLOCKED BY UNRELATED DEPENDENCIES
Evidence class: STATIC_SOURCE until Unity Profiler, GCMonitor, Burst, and Play Mode logs exist.

## Decision 00 - Domain And Architecture Boundary

Problem: SHINOBU_150 owns localization and subtitle presentation, but the repo contains third-party UI/audio packages and active parallel agents.
Solution: Confine edits to first-party `Assets/_Project` and stable docs. Use typed payloads, native buffers, and cold facades. Do not mutate third-party vendors or other agents' domains.
Rejected Alternatives: Deleting vendor JSON/audio code would be out of domain and creates compile risk. Direct dependencies on missing agent systems would create integration debt.
Scalability potential: Low = bounded dirty text queue and fixed mock locale buffers. Middle = larger registered text capacity. High = richer telemetry. Ultra = editor x-ray and visual-overkill debugging without gameplay truth cost.
Hardware Impact: Expected low-end gain is avoiding boot-time JSON/string dictionaries and per-frame UI string allocations; static estimate 50-500 us avoided during text bursts on i3/MX350, pending profiler proof.

## Decision 01 - Native Text Pipeline Shape

Problem: Task requires MMF-style UTF-8 span extraction, but Unity/Burst cannot return `ReadOnlySpan<byte>` from jobs and true `MemoryMappedFile` APIs are managed/cold-only.
Solution: Implement a native database facade over unmanaged byte slabs and sorted hash entries; cold MMF/file mapping owns the bytes, hot path receives pointer/offset/length views and decodes into preallocated char buffers.
Rejected Alternatives: `Encoding.UTF8.GetString`, JSON, `Dictionary<string,string>`, and `string.Format` all violate zero-GC subtitle delivery.
Scalability potential: Low = small fixed buffers and strict dirty budget. Middle = larger slab and cue capacity. High = richer telemetry and more subtitles per frame. Ultra = longer narrative slabs and editor hex/x-ray previews.
Hardware Impact: Sequential byte scans and binary search keep cache predictable. Expected per 256-char subtitle decode under 100 us on i3/MX350; pending profiler proof.

## Decision 02 - Timing Authority

Problem: Frame-rate timing drifts from spoken audio during stalls.
Solution: Subtitle cue evaluation consumes an explicit `AudioFrameClock` value and sample-rate derived progress. Runtime APIs accept the clock from the DSP/audio owner instead of polling `Time.deltaTime`.
Rejected Alternatives: Coroutines, `WaitForSeconds`, `Time.time`, and `Time.deltaTime` are frame-bound and desync during hitches.
Scalability potential: Low through Ultra use the same audio clock; quality weight changes presentation cadence only, not subtitle truth.
Hardware Impact: One unsigned integer compare per cue; expected <10 us for 64 cues on MX350-class CPU, pending profiler proof.

## Decision 03 - Subtitle Cue ABI

Problem: Subtitle state needed direct Burst mutation without CS1612 property copies and with fixed ARM64 stride.
Solution: Added `SubtitleCueDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]` with raw public fields and byte pads at offsets 20-31. Evaluation and clear jobs use `UnsafeUtility.AsRef` over the Vault pointer.
Rejected Alternatives: Sequential layout and C# properties were rejected because field offsets become convention rather than proof, and property mutation of structs creates copy/writeback risk.
Scalability potential: Low = 64 fixed cues with cheap visibility math. Middle = same stride with larger Vault count if needed. High = richer telemetry. Ultra = editor x-ray over active cue slots.
Hardware Impact: One 32-byte cache line per cue; 64 cues cost roughly 2 KB scan and estimated <10 us on i3/MX350, pending Burst/profiler proof.

## Decision 04 - Cue Signal And Rollback Fence

Problem: Narrative/UI coupling through direct calls would make subtitle state look like gameplay truth.
Solution: Added a 16-byte `SubtitleCueSignal` lane and `FlagVisualOnlyNoRollback` on every cue. The cue DTO is Vault-backed presentation state and is documented as excluded from rollback/Merkle truth.
Rejected Alternatives: Reusing the older 32-byte `SubtitleSignal` only would miss the requested compact audio-frame cue contract. Adding cue DTOs to rollback mirrors was rejected because subtitles are "Dear Lie" presentation.
Scalability potential: Low = 8 low-tier signal snapshot budget. Middle/High/Ultra = 64 max frame cue signals and staged presentation.
Hardware Impact: 16-byte signals halve queue bandwidth versus 32-byte subtitle events; expected savings are small per cue but predictable during narrative bursts.

## Decision 05 - Compile Guard

Problem: Project policy forbids `dotnet build` while CPU is above 50% or `dotnet/csc` is active.
Solution: Checked process list and CPU before Loop 1 compile. No `dotnet/csc` was visible, but CPU samples were 56.9%, 92.8%, and 100%, so compile verification is deferred.
Rejected Alternatives: Running build through the guard would violate batch policy and interfere with parallel agents.
Scalability potential: Not runtime-facing.
Hardware Impact: Avoided adding a full compile load during a saturated CPU window.

## Decision 06 - Dynamic Token Syntax

Problem: Babel subtitles need variable replacement for patterns already authored as `{0}` while the prompt forbids `string.Format`.
Solution: Extended the UTF-8 decode loop to detect brace placeholders and write integers through `ZeroGCFormatter.FastIntToChars` directly into the destination span. Existing `^0..^3` syntax remains supported.
Rejected Alternatives: `string.Format`, interpolation, or pre-expanded managed text caches were rejected because they allocate exactly when subtitle bursts happen.
Scalability potential: Low = integer-only placeholders. Middle = same formatter with richer authoring. High/Ultra = formatted numeric variants via `{0:format}` without changing the hot ABI.
Hardware Impact: Expected 5-40 us and one managed string allocation avoided per formatted subtitle on i3/MX350-class CPUs, pending profiler proof.

## Decision 07 - DSP Frame Timer

Problem: `Time.deltaTime` subtitle timers desync during frame stalls and cannot be compared to audio playback authority.
Solution: Store subtitle start/duration as audio sample frames. `SubtitleManager` now derives remaining time, typewriter reveal, and timed audio-log cue reveal from `BabelSubtitleSyncRuntime.CurrentAudioFrame`.
Rejected Alternatives: Coroutine sleeps and accumulated delta time were rejected because they follow the render loop, not spoken audio.
Scalability potential: Low through Ultra keep the same truth clock. Quality weight can alter UI dirty cadence only.
Hardware Impact: Integer subtraction and one divide per visible text lane; estimated <5 us per frame for active subtitles.

## Decision 08 - Continuous UI Dirty Budget

Problem: Canvas rebuild cost spikes when many labels dirty in one frame; binary quality tiers violate the GlobalQualityWeight mandate.
Solution: `LabelSwapScheduler` resolves a smooth dirty budget from `HomeostasisBrain.GlobalQualityWeight`, clamped by pending work and the old hard maximum.
Rejected Alternatives: Fixed 18-label drain and low/high tier rich-text toggles were rejected because they do not scale continuously with thermal pressure.
Scalability potential: Low = 2 labels/frame and rich-text stripped. Middle = gradual budget expansion. High = near old max. Ultra = full budget plus editor diagnostics.
Hardware Impact: Expected to flatten 0.05-0.2 ms TMP/canvas dirty bursts on MX350-class hardware, pending profiler proof.

## Decision 09 - Signal Lane Instead Of Direct Calls

Problem: Narrative/audio producers should not depend directly on the subtitle manager instance or a UI object lifetime.
Solution: Added `SubtitleCueSignal` as a 16-byte unmanaged `SignalBus` lane. Producers can publish hash/start/duration/priority while the UI consumes only frame snapshots.
Rejected Alternatives: Direct `SubtitleManager.DisplaySubtitle` dependencies and managed event delegates were rejected because they bind domains and risk allocations.
Scalability potential: Low = 8 low-tier snapshot budget. Middle/High/Ultra = 64 compact cue signals per frame.
Hardware Impact: 16-byte payload reduces bandwidth versus existing 32-byte subtitle signal path; expected savings are small but deterministic under burst dialogue.

## Decision 10 - AUP Directional Arrow

Problem: Directional subtitles linked to spatial audio can point wrong at large world offsets if positions are cast to float first.
Solution: Compute source-camera delta through `AbsoluteUniversePosition.ToCameraRelativeFloat3` and only then derive left/right/behind arrows.
Rejected Alternatives: `Transform.position` deltas were rejected as they are not authoritative under floating-origin/AUP shifts.
Scalability potential: Low = simple arrow. Middle = cue flags. High/Ultra = richer spatial subtitle UI using same AUP delta.
Hardware Impact: One AUP delta and dot products per directional cue; estimated <3 us per cue.

## Decision 11 - Rollback Fence

Problem: Subtitle visible progress is a presentation lie and must not pollute deterministic gameplay state.
Solution: Added `FlagVisualOnlyNoRollback`, exposed `RollbackStateExcluded`, and documented that producers synchronize narrative intent/audio state, not subtitle progress.
Rejected Alternatives: Mirroring cue DTOs into rollback/Merkle state was rejected because it increases network/state churn for non-gameplay truth.
Scalability potential: Low through Ultra share the same exclusion. High-end presentation can become richer without changing rollback payloads.
Hardware Impact: Avoids serializing and hashing presentation-only DTOs; savings depend on netcode cadence, pending integration proof.

## Decision 12 - Editor X-Ray

Problem: Raw hash tables and byte offsets are hard to audit without decoding through the same path the runtime uses.
Solution: Added `BabelSyncTunerWindow` as an editor-only UI Toolkit facade for telemetry, audio-frame offset, compact cue publish, raw UTF-8 hex, and decoded span preview.
Rejected Alternatives: Runtime debug UI was rejected because it would add player-facing code and could contaminate allocation measurements.
Scalability potential: Low devices pay zero runtime cost. High/Ultra development machines get better x-ray tooling.
Hardware Impact: Editor-only allocations are acceptable; runtime impact is zero when the window is not present.

## Decision 13 - Compile Wall

Problem: After CPU dropped to 14% and no `dotnet/csc` process was active, `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed.
Solution: Treat this as dependency-blocked, not a green verification. The reported errors are missing unrelated types in `HectonVisorUberPostFeature`, `GlobalRegistryContracts`, `DeferredDecalPass`, `ModularEquipmentEngine`, and `SomaticTunerWindow`; no SHINOBU_150 file errors were emitted.
Rejected Alternatives: Editing visor/equipment/somatic domains was rejected as outside SHINOBU_150 and would violate the domain boundary.
Scalability potential: Not runtime-facing.
Hardware Impact: Not runtime-facing.

## Decision 14 - Compile-Wall Isolation Rollback

Problem: SHINOBU_150 originally touched global compile surfaces by adding subtitle buffer IDs to `H8Memory.cs` and manual entries to generated `Hecton8.Core.csproj`.
Solution: Remove SHINOBU_150 from those global surfaces. The runtime now uses domain-local cast constants `(BufferID)15070550` and `(BufferID)15070551`, and Unity/asmdef ownership handles source inclusion.
Rejected Alternatives: Keeping core enum additions was rejected because adjacent agents are actively editing ID ranges and collisions become global authority debt. Keeping `.csproj` edits was rejected because generated project files are not source authority.
Scalability potential: Low through Ultra get the same Vault route without adding a new sibling assembly dependency or global enum churn.
Hardware Impact: No frame-time gain; protects iteration speed and reduces compile-wall blast radius.

## Decision 15 - Vault Views And Dispatcher-Chained Cue Evaluation

Problem: Persistent private `NativeArray` fields in `BabelSubtitleSyncRuntime` violated the Vault law, and cue evaluation scheduled from `PreSimulationTick` could be invisible to the dispatcher dependency graph.
Solution: Store only `VaultBufferHandle<T>` and resolve transient `NativeArray<T>` views per access. `ScheduleCueEvaluation` returns the scheduled handle or `JobHandle.CombineDependencies(dependsOn, pendingHandle)` when a job is already active. Completion is guarded by `IsCompleted` before `Complete()`.
Rejected Alternatives: Holding persistent `NativeArray` fields was rejected because ownership belongs to the GlobalDataVault. Calling `Complete()` immediately after scheduling was rejected outside cold boot because it blocks the main thread and breaks Kahn-style dispatch.
Scalability potential: Low = one-frame tolerant cue evaluation with 64 fixed slots. Middle = same handle model with larger Vault capacity. High = richer telemetry. Ultra = editor x-ray and higher cue burst budget without local persistent allocations.
Hardware Impact: Expected hot-path gain is small but structural: avoids local persistent native ownership and prevents arbitrary main-thread fences; 64-cue scan remains estimated below 10 us on i3/MX350 pending profiler proof.

## Decision 16 - Babel Registry Dictionary Bridge Purge

Problem: `LocRegistry.Reload(Dictionary<GameLanguage, Dictionary<string,string>>...)` allowed legacy managed tables to hydrate the zero-GC registry, which polluted Task 02 even if subtitles used hash lookups.
Solution: Remove the bridge and make `LocRegistry.ReloadBinaryOrMock(GameLanguage)` the only registry reload path. `LocalizationManager.RefreshRuntimeRegistry()` now triggers binary/static UTF-8 authority or the unmanaged emergency mock. The older `LocalizationManager` string APIs remain isolated compatibility/mod/editor debt and no longer feed Babel.
Rejected Alternatives: Keeping the bridge as "cold only" was rejected because the assignment explicitly requires dictionary purge for localization lookup. Deleting every legacy string-returning UI API in this pass was rejected because many non-SHINOBU callers still compile against it and it would become a cross-domain rewrite.
Scalability potential: Low = emergency mock and binary UTF-8 spans with no managed registry hydration. Middle = static arena block. High = MMF staged swap. Ultra = larger caller-owned spans and x-ray previews without changing lookup authority.
Hardware Impact: Removes cold locale reload table walk and UTF-16 to UTF-8 copy from the Babel route; expected 0 us per-frame gain, but avoids milliseconds of cold reload heap churn on i3/MX350-class systems.

## Decision 17 - Long Lore Decode Ceiling

Problem: The fallback compatibility decode path capped `ResolveRaw`/debug decode at 1024 glyphs, which can truncate the requested 500-word paragraph audit before TMP commit.
Solution: Raise `MaxDecodedGlyphs` to 4096. Loop 12 later replaced the old thread-static storage with a fixed decode ring. Keep true long-form lore APIs span-owned: large PDA pages should call `TryWriteVisualSpanFromUtf8(...)` with a caller-provided buffer sized for the page.
Rejected Alternatives: Allocating per-page managed strings or growing char arrays during gameplay was rejected. Making every subtitle lease 4096 chars was rejected because short subtitles should not inflate common UI pool footprint.
Scalability potential: Low = subtitles still use small leases and dirty-budget throttling. Middle = 4096-glyph fallback for audits/debug previews. High/Ultra = page-owned fixed spans for megabyte lore windows.
Hardware Impact: The final compatibility path uses prewarmed fixed slots, not first-use growth. Hot decode remains span scanning. Avoids repeated truncation/fallback passes during long-lore verification.

## Decision 18 - Legacy Format Api Quarantine

Problem: `LocalizationManager.FormatLocalized` still called `string.Format` for legacy string-returning UI APIs. That API is not the Babel hot path, but the formatter name violates the SHINOBU assignment and can pull allocation-heavy formatting back into runtime callers.
Solution: Replace it with a minimal compatibility formatter based on `string.Create`, placeholder parsing, and primitive `TryFormat` into the destination span. Unsupported object formatting returns the template and logs during development.
Rejected Alternatives: Keeping `string.Format` behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD` was rejected because scans remain dirty. Rewriting every caller to span APIs in this pass was rejected because it crosses main menu, modding, gameplay, and world domains.
Scalability potential: Low through Ultra Babel path stays hash/span/TMP. Legacy callers get a bounded fallback and should migrate to `TryWriteLocalizedInt` or `LocRegistry.TryWriteVisualSpanFromUtf8` for hot UI.
Hardware Impact: Removes reflection-style composite formatting from legacy compatibility calls. The legacy method still returns a string, so it is not claimed as 0-GC proof.

## Decision 19 - Format Spec Preservation

Problem: The first quarantine formatter removed `string.Format`, but it treated `{0:F1}` the same as `{0}`, which silently changes legacy UI text.
Solution: Parse the optional colon format span and pass it into numeric `TryFormat`. Formatted nonnumeric placeholders fail closed to the original template and development warning path.
Rejected Alternatives: Ignoring format suffixes was rejected as silent localization corruption. Reintroducing `string.Format` for formatted cases was rejected because scans and runtime allocation behavior would regress.
Scalability potential: Low through Ultra preserve authored numeric precision without reintroducing reflection-style formatting. Babel subtitle hot path remains the primary zero-GC route.
Hardware Impact: One extra bounded scan over the placeholder format span in legacy string APIs. No added Babel hot-path cost.

## Decision 20 - Babel Lease Capacity Correction

Problem: `CharBufferPool.RequiredBabelTextCapacity` was 128 chars, so hash-routed subtitles could truncate normal dialogue even after the registry decode ceiling was raised.
Solution: Raise Babel native arena and TMP bridge slots to 512 chars while keeping the older 256-char HUD slot unchanged. Fix prewarm so it touches each buffer by its own capacity.
Rejected Alternatives: Raising every HUD slot to 512 was rejected because it bloats non-Babel UI. Using the 32768-char encyclopedia page for subtitles was rejected because subtitle bursts need many small concurrent leases.
Scalability potential: Low = same 500-slot lease count with 512-char subtitle cap and dirty-budget throttling. Middle/High/Ultra = richer subtitle lines without allocating. Megabyte lore remains page/window streamed through encyclopedia lanes or caller-owned spans.
Hardware Impact: Cold managed TMP bridge footprint increases by roughly 384 KB versus the old 128-char lane. The native Babel arena is Vault-owned when available, not a private pool allocation. Runtime subtitle truncation risk drops without per-subtitle allocation.

## Decision 21 - CharBufferPool Native Ownership Eradication

Problem: `CharBufferPool` still had a persistent private `NativeArray<char>` fallback and `NativeBitArray` lease tracker, which violated the Vault law even though the primary SHINOBU runtime had already moved to Vault handles.
Solution: Remove the local native fallback and bitset. Babel resolves Vault buffer `(BufferID)70540` transiently when a DataVault exists; no-vault/editor mock routes write into the prewarmed TMP bridge `char[]` slot. Lease ownership uses the existing fixed `ulong[8]` bitmap.
Rejected Alternatives: Keeping the local `NativeArray<char>` as a cold fallback was rejected because it normalizes off-Vault persistent native ownership. Allocating a new native arena on demand was rejected for the same reason. Forcing failure when no Vault exists was rejected because CI/editor mock subtitle routes still need a deterministic zero-GC fallback.
Scalability potential: Low = fixed 500x512 subtitle lanes with no native fallback allocation. Middle/High/Ultra = Vault-backed native staging if the UI Vault is live, richer text still bounded by the same lease capacity, and megabyte lore stays in encyclopedia/page spans.
Hardware Impact: Removes one cold persistent native allocation of 256000 chars and one native bitset allocation in no-vault routes. Frame-time gain is near 0 us; the architectural gain is eliminating fragmentation and ownership ambiguity.

## Decision 22 - Runtime Dictionary And Mod Injection Quarantine

Problem: After the Babel registry bridge was removed, `LocalizationManager` still carried runtime-facing dictionary parse/injection APIs and the mod facade still accepted `Dictionary<string,string>` translation tables. Those paths did not hydrate Babel anymore, but they preserved the exact managed-table mutation shape Task 02 was written to kill.
Solution: Remove `LocalizationManager.InjectEntries(...)` and its runtime JSON parser from the localization owner. Move the legacy JSON parser into `Assets/_Project/Scripts/Editor/LocalizationEditorJsonTableParser.cs` for key generation and CJK font validation only. Replace `HectonAPI.Localization.InjectTable(Dictionary<string,string>)` with a rejected future `InjectBabelEnvelope(ReadOnlySpan<byte>)` seam, while `ModLocalizationBridge` remains a no-op for discovered JSON localization files.
Rejected Alternatives: Keeping dictionary injection as a disabled no-op was rejected because the public signature would keep mod authors targeting the wrong authority. Deleting editor JSON tools was rejected because key generation and font coverage validation are human-control tooling, not runtime lookup. Building a modded Babel envelope format in this pass was rejected as a cross-domain contract that needs data-baker ownership.
Scalability potential: Low = no runtime table mutation or file-read heap churn during mod discovery. Middle = Babel hash lookup remains the only runtime authority. High = editor tools can still validate glyph coverage. Ultra = future binary mod envelopes can stage through the same MMF/hash/span route without changing subtitle delivery.
Hardware Impact: Hot subtitle frame cost remains the same; the gain is removing cold runtime heap churn and public API pressure toward string dictionaries. Static estimate: avoids milliseconds of mod/locale table parse churn and hundreds of KB to MB of managed heap on i3/MX350-class hardware when mods or legacy language assets are present.

## Decision 23 - LocRegistry Layout And Subtitle Queue Hardening

Problem: After the dictionary bridge was purged, `LocRegistry` still used a managed `HashSet<int>` to suppress repeated missing-key development logs, several registry DTOs relied on sequential layout, and `SubtitleManager` kept a managed `List<SubtitleRequest>` queue for legacy string subtitles.
Solution: Replace missing-key suppression with four fixed `ulong` bloom-mask lanes, convert the registry DTO/signal/telemetry structs to explicit 16/24/32/64-byte layouts, and replace the subtitle legacy string queue with an 8-slot fixed ring. Local subtitle readonly structs now expose fields instead of get-only properties, and `LocalizationManager.CurrentLanguage` now reads a private backing field without a settable property.
Rejected Alternatives: Keeping `HashSet<int>` as "development only" was rejected because it normalizes managed collection ownership inside the localization registry. Keeping sequential layout was rejected because ARM64 alignment should be source-proof, not runtime luck. Expanding the subtitle queue with a larger managed collection was rejected because the existing command/buffer queues already use fixed power-of-two rings.
Scalability potential: Low = fixed subtitle queues and bloom mask avoid cold managed growth during missing-key storms. Middle = same 8-slot user-facing queue with Babel command queue priority. High = explicit DTOs allow larger Vault-backed arrays without changing field offsets. Ultra = telemetry can be bulk-copied and x-rayed because each registry frame is a 64-byte cache-line record.
Hardware Impact: Per-frame savings are small but concrete: legacy `List.RemoveAt(0)` O(n) shifting is replaced with O(1) ring dequeue, and missing-key storms no longer allocate/grow a `HashSet`. Static estimate: 1-15 us avoided during subtitle burst queue churn on i3/MX350-class CPUs; profiler proof remains pending.

## Decision 24 - Legacy Decode Buffer Ring

Problem: `LocRegistry.ResolveRaw` and `TryGetRawBuffer` used a `[ThreadStatic]` decode buffer that allocated on first use and returned the same array for consecutive lookups on one thread. That creates both a hot first-use allocation risk and a data alias bug for call sites that fetch label and unit spans before copying.
Solution: Replace the grow-on-first-use thread-static buffer with a fixed 16-slot prewarmed `char[4096]` ring selected through `Interlocked.Increment`. The Babel hot path remains caller-owned `Span<char>`; the ring is compatibility containment for legacy raw-buffer APIs.
Rejected Alternatives: Keeping the thread-static buffer and calling it "cold" was rejected because first use can occur from HUD/PDA code after boot. Returning a freshly allocated array per lookup was rejected outright. Rewriting every PDA/HUD caller in this pass was rejected because it crosses multiple presentation owners; the fixed ring removes the immediate allocation and aliasing hazard without changing their public contracts.
Scalability potential: Low = no first-use allocation when a legacy raw-buffer lookup happens during a stressed frame. Middle = 16 slots tolerate nested label/unit/template lookup bursts. High = hot subtitle route still bypasses the ring with caller-owned spans. Ultra = future PDA megabyte pages can migrate to page-owned spans without carrying this compatibility seam forward.
Hardware Impact: Removes one potential `char[4096]` allocation per thread from the legacy lookup path and prevents duplicate lookup overwrite bugs. Static estimate: 0 us steady-state hot subtitle gain, but first-use hitch and alias retry paths are removed; profiler proof remains pending.

## Decision 25 - LocNumericBuffer Fixed Ring

Problem: `LocNumericBuffer` still followed the older thread-static numeric formatter contract: first lookup could allocate a staging array, overflow could allocate `new char[capacity]`, and growth logic preserved a hidden managed escape hatch for HUD/Babel numeric templates.
Solution: Replace thread-static staging with a fixed 16-slot prewarmed `char[4096]` ring selected through `Interlocked.Increment`. Remove dynamic growth and make overflow a bounded in-buffer truncation with ASCII ellipsis. The hot caller-owned `TryWrite(...)` overloads remain the preferred path; the ring is compatibility staging for APIs that must return `char[]`.
Rejected Alternatives: Keeping grow-on-overflow was rejected because localization templates can become long after translation and the worst time to allocate is the first stressful HUD burst. Reducing slots to one shared buffer was rejected because nested label/unit/template lookups can alias. Moving this into `CharBufferPool` was rejected because `LocNumericBuffer` is a small compatibility formatter and should not consume subtitle lease lanes.
Scalability potential: Low = fixed 4096-char slots avoid surprise heap growth under translated HUD templates on weak devices. Middle = 16 slots cover nested metric formatting without aliasing. High = richer numeric precision formats use the same bounded ring. Ultra = long lore remains page-owned spans and does not bloat numeric HUD staging.
Hardware Impact: Removes the dynamic `char[capacity]` expansion path from HUD numeric formatting. Static estimate: avoids a one-frame managed allocation spike when a translated template overflows previous capacity; steady-state CPU gain is negligible, but GC risk is removed. Profiler proof remains pending.

## Decision 26 - Audio-Frame Visual Corruption Clock

Problem: `LocalizationManager` had removed frame-time subtitle truth, but PDA corrosion windows, madness override windows, and visual corruption seeds still used `Time.unscaledTime`. That keeps localized presentation effects tied to Unity frame time and can drift from the audio subtitle authority during hitches.
Solution: Store external PDA corrosion and madness override expiration as audio sample frames derived from `AudioSettings.dspTime * AudioSettings.outputSampleRate`. Seed and roll buckets use DSP-frame math, not Unity time. Active windows use `uint` wrap-safe signed-diff comparison, with interval caps below `2^31` frames so 100-hour runs survive audio-frame wrap.
Rejected Alternatives: Keeping `Time.unscaledTime` was rejected because it violates the audio-clock mandate for localization presentation state. `Time.frameCount` was rejected for effect lifetime because it is render cadence, not audio cadence. Saturating at `uint.MaxValue` was rejected after review because it truncates active windows on frame wrap.
Scalability potential: Low = corruption buckets remain cheap integer/double DSP math with no extra allocations. Middle = same clock supports stable PDA corrosion during hitches. High = richer visual text corruption can consume the same audio-frame buckets. Ultra = high-frequency shader/UI corruption can remain visually synchronized without touching rollback truth.
Hardware Impact: CPU change is negligible: a few double multiplies/divides and integer comparisons in visual text evaluation. Static gain is correctness and drift removal, not frame-time speed; no managed allocations are introduced.
