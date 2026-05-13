# AUDIO_VWS_SYSTEM Log

## 2026-05-12 - DSP_ACOUSTIC_LEAD - AUDIO_VWS_SYSTEM

What was wrong:
- Warning producers were allowed to cross into audio/caption behavior instead of emitting a neutral warning signal.
- Bitchin' Betty had no authoritative fixed-priority runtime in `GlobalRegistry`.
- Queue behavior was vulnerable to managed/dynamic patterns and overlap because warning IDs, cooldowns, and priority were not centralized.
- Multilingual clips risked string/dictionary lookup patterns instead of flat warning-ID indexing.
- No VWS black box existed for the last 300 frames of high-level state.

What was done:
- Added `IVocalWarningSystem`, `VocalWarningId`, warning hashes, and `GlobalRegistry.VocalWarnings` registration.
- Added `VocalWarningSignal`, `VitalWarningSignal`, `CrushWarningSignal`, and `SubtitleSignal` lanes to `GlobalSignals`.
- Built `VocalWarningSystem` with `NativeQueue<byte>` ingress, fixed `NativeArray<byte>` queue capacity 16, `NativeArray<float>` cooldowns, flat `AudioClip[]` language bundles, subtitle emission, and 300-frame telemetry dump to `Docs/AgentLogs/Dump_AUDIO_VWS_SYSTEM.bin`.
- Added Burst-compatible `VwsCooldownDecayJob` and `VwsPrioritySortJob`.
- Routed VWS playback through `PlayerCriticalProceduralAudioRenderer` using double-buffered PCM submission, 50 ms preempt fade, ambient current ducking, and radio degradation fake.
- Converted submarine OS, player vital, crush depth, structural breach, and ballast/fluid warning producers to publish signals instead of owning warning playback.
- Added installer wiring so the listener object owns the VWS runtime when procedural critical audio is installed.

Cinematic Cheats used:
- Replaced physical speaker/radio simulation with one-pole low-pass plus 5-sample bit-crush hold.
- Replaced mixer snapshot/global duck with a scalar 0.5 ambient-current multiplier while VWS is active.
- Replaced overlapping AudioSource playback with scalar DSP cursor, fixed PCM buffers, and 50 ms preempt fade.
- Low/MX350/Unknown tiers disable radio degradation entirely; High/Ultra retain the fake for audible hardware damage.

Exact measured microseconds saved:
- 0.0 us measured. No profiler pass could execute because verification stops before playmode/runtime.

Budgeted microseconds from bounded implementation:
- Signal publish/enqueue: ~0.8 us per warning.
- Fixed priority queue scan/sort at 16 IDs: <=1.0 us per SlowTick burst.
- Cooldown Burst decay over six floats: <=0.2 us per SlowTick.
- VWS mix injection: ~0.02 us/sample while active.
- 50 ms preempt fade: ~0.03 us/sample only during fade.
- Radio degradation fake: ~0.04 us/sample on Middle/High/Ultra only; Low/MX350/Unknown save that branch.
- Removed overlapping managed audio requests during bursts: estimated 2-8 us on i3/MX350 plus zero VWS-path GC.

Verification:
- Purge scan: no `VWSManager`, `VocalWarningSystem.Instance`, `List<AudioClip>`, `Dictionary<string, AudioClip>`, coroutine cooldown, or string clip queue in VWS warning path.
- `git diff --check` on touched files: no whitespace errors; Git reports existing LF-to-CRLF normalization warnings.
- Compile remains blocked by dependency wall before VWS/Burst diagnostics: missing `Hecton8.Core.Memory`, `Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `IDataVault`, `SystemID`, `GlobalDataVault`, `SyncFenceSignal`, `StateCorrectionSignal`, `InputSignal`, `CartographyAup`, and map reveal types.
- Task status remains `PENDING VERIFICATION`; Task 3 and Task 19 are marked `[BLOCKED BY DEPENDENCY]`.

## 2026-05-13 - DSP_ACOUSTIC_LEAD - AUDIO_VWS_SYSTEM Non-Build Recheck

What was wrong:
- VWS still had hot-path registry reads for renderer state and warning playback helpers.
- `NativeQueue<byte>` admission was capped logically but not prewarmed, leaving first-burst native allocation risk.
- `CancelCurrentWarning()` did not cancel active renderer PCM playback.
- Severity/gain/sample inputs needed stronger finite guards before entering native state and the DSP mix.

What was done:
- Cached renderer, subtitle, localization, and quality-tier dependencies during lifecycle setup.
- Registered VWS as an `IScalabilityChangedEventListener` so radio-degradation tier gating updates from the event lane instead of hot registry polling.
- Prewarmed the VWS staging queue to 16 entries in `EnsureNativeStorage()`.
- Added `CancelVocalWarningPlayback()` with an audio-thread-consumed cancel flag.
- Added finite fallback for warning severity, fallback cooldown, submitted voice gain, and mixed VWS PCM samples.
- Added inspector tooltips/headers and XML docs on VWS public contract/runtime additions.

Cinematic Cheats used:
- Kept the failing-speaker effect as scalar one-pole low-pass plus bit-crush hold.
- Kept Low/MX350/Unknown on the cheapest no-degradation branch.
- Preserved 50 ms scalar preempt fade and avoided fader objects, coroutines, or mixer snapshots.

Exact measured microseconds saved:
- 0.0 us measured. User explicitly prohibited `dotnet build`; no Unity/profiler run was launched.

Budgeted microseconds from this pass:
- Removed 2-4 hot-path registry property reads across VWS Tick/SlowTick helpers.
- Removed first-warning native queue allocation risk by prewarming 16 entries cold.
- Cancel path adds one volatile check only while the VWS sample renderer is active.

Verification:
- Re-extracted `<AGENT_PROMPT id="AUDIO_VWS_SYSTEM" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-aware CLI regex. Task count remains 19.
- Static purge scan found no `VWSManager`, `VocalWarningSystem.Instance`, `List<AudioClip>`, `Dictionary<string, AudioClip>`, coroutine, `Enum.IsDefined`, `.ToString()`, or `string.Format` in VWS audio files.
- `git diff --check` on touched VWS files/logs returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- No `dotnet build` command was launched.
- Status remains `PENDING VERIFICATION`; runtime/profiler evidence is still absent.

## 2026-05-13 - DSP_ACOUSTIC_LEAD - AUDIO_VWS_SYSTEM Lifecycle Hardening Recheck

What was wrong:
- Lifecycle caching fixed hot registry polling but exposed a load-order fault: VWS could enable before renderer/subtitle/localization services registered, leaving cached service refs null.
- Disabling VWS or replacing `PlayerCriticalAudioRuntime` could leave already-submitted PCM warning playback alive in the renderer after the VWS queue owner disappeared.
- Public cancellation and queue clearing assumed native queue/array storage still existed.

What was done:
- Registered `VocalWarningSystem` as `IGlobalRegistryHotSwapListener` + `IGlobalRegistryHotSwapRefListener`.
- Rebound cached `PlayerCriticalProceduralAudioRenderer`, `SubtitleManager`, and `LocalizationManager` through the existing deferred `GlobalRegistry` service-rebound lane.
- Reselected the flat localized clip bundle on localization service rebound.
- Centralized cancellation in `CancelRendererPlaybackAndClearQueues()`.
- Cancelled active/pending renderer VWS playback before VWS unregisters.
- Cancelled the previous renderer instance when `PlayerCriticalAudioRuntime` is hot-swapped.
- Guarded queue clearing with `NativeQueue.IsCreated` and `NativeArray.IsCreated`.

Cinematic Cheats used:
- Preserved the scalar DSP path: no fader object, coroutine, mixer snapshot, or `AudioSource` churn.
- Kept service rebinding on the registry event lane instead of hot polling.
- Kept Low/MX350/Unknown on the no-radio-degradation branch.

Exact measured microseconds saved:
- 0.0 us measured. User explicitly prohibited `dotnet build`; no Unity/profiler run was launched.

Budgeted microseconds from this pass:
- Avoids reintroducing 2-4 registry property reads per VWS Tick/SlowTick helper.
- Teardown/hot-swap cancellation is cold lifecycle only; no added per-sample or per-frame cost.
- Maintains bounded queue clear cost at 16 IDs.

Verification:
- Re-extracted `<AGENT_PROMPT id="AUDIO_VWS_SYSTEM" ...>` from `Docs/Tasks/CURRENT_BATCH.md`; primary objective count is 19.
- Static purge scan across VWS audio files and converted producers found no `VWSManager`, `VocalWarningSystem.Instance`, `List<AudioClip>`, `Dictionary<string, AudioClip>`, coroutine marker, `Enum.IsDefined`, `PlayClipAtPoint`, `PlayOneShot`, or `QueueAudioEvent`.
- Hot registry scan in `VocalWarningSystem.cs` now only finds cold lifecycle cache refresh reads.
- `git diff --check` on touched files returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- No `dotnet build` command was launched.
- Status remains `PENDING VERIFICATION`; runtime/profiler evidence is still absent.
