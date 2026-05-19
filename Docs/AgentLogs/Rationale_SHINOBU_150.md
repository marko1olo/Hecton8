# Rationale_SHINOBU_150

Status: IMPLEMENTED / COMPILE BLOCKED BY UNRELATED DEPENDENCIES
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
