# AUDIO_VWS_SYSTEM Status

Prompt: `AUDIO_VWS_SYSTEM`
Role: `DSP_ACOUSTIC_LEAD`
Domain: Audio / Presentation UX
Status: PENDING VERIFICATION

Mandates loaded before coding:
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Checklist

- [x] Task 1 - Singleton eradication | DOD: `IVocalWarningSystem` contract + `GlobalRegistry.VocalWarnings`; no `VWSManager.Instance` hits | Alternative rejected: scene singleton/audio manager static | Estimate: 0.0 us hot path
- [x] Task 2 - Signal migration | DOD: VWS consumes `VocalWarningSignal`, `VitalWarningSignal`, `CrushWarningSignal`, `BrownoutSignal`; direct submarine/crush-depth calls converted | Alternative rejected: player/submarine invoking `IAudioService` for warnings | Estimate: 0.8 us enqueue
- [x] Task 3 - ASMDEF isolation | DOD: [BLOCKED BY DEPENDENCY] current audio scripts sit in `Hecton8.Core` and reference gameplay/world/UI contracts outside pure Contracts+Unity Audio | Alternative rejected: fake asmdef split that strands `PlayerCriticalProceduralAudioRenderer` dependencies | Estimate: blocked
- [x] Task 4 - Dead code hunt | DOD: no `VWSManager`, `List<AudioClip>`, or `Dictionary<string, AudioClip>` queue remains in VWS/audio warning path; ingress is `NativeQueue<byte>` | Alternative rejected: managed clip queue | Estimate: 0.6 us enqueue
- [x] Task 5 - Priority queue S.O.A. | DOD: `NativeArray<byte> _vwsQueue` fixed at 16 slots; staging `NativeQueue<byte>` is prewarmed to 16 and ingress caps at 16 before promotion | Alternative rejected: dynamic queue object graph | Estimate: <=1.0 us bounded scan
- [x] Task 6 - Enumerated priorities | DOD: `VocalWarningId` maps Crush=1, Hull=2, Oxygen=3, Radiation=4, Power=5 | Alternative rejected: string priority names | Estimate: 0.0 us
- [x] Task 7 - Sub-priority Burst sort | DOD: `VwsPrioritySortJob` insertion-sorts byte IDs on `SlowTick` | Alternative rejected: LINQ/sort delegate | Estimate: <=0.7 us at 16 slots
- [x] Task 8 - Preemptive interruption | DOD: higher priority pending request triggers scalar 50 ms fade in DSP loop; public cancel now requests audio-thread-safe renderer cancellation | Alternative rejected: coroutine/fader object allocation | Estimate: ~0.03 us/sample while fading
- [x] Task 9 - Cooldown bitmask | DOD: `NativeArray<float> _cooldowns` length 6 decays through Burst job | Alternative rejected: per-warning timers/coroutines | Estimate: <=0.2 us/SlowTick
- [x] Task 10 - DSP integration | DOD: `AudioClip.GetData` enters double-buffered PCM lane; renderer mixes VWS in producer DSP path | Alternative rejected: `AudioSource.PlayStatic2D` warning playback | Estimate: ~0.02 us/sample mix cost
- [x] Task 11 - Dynamic ducking | DOD: VWS active state multiplies procedural ambient current by 0.5 in `MixAndFilterBlock` | Alternative rejected: mixer snapshot transition/all-bus duck | Estimate: ~0.01 us/sample branch
- [x] Task 12 - Radio degradation fake | DOD: compromised habitat flag applies one-pole low-pass + 5-sample bit-crush hold in DSP | Alternative rejected: physical speaker simulation/protons | Estimate: ~0.04 us/sample only while active
- [x] Task 13 - Multilingual VWS | DOD: `ILocalizationLanguageChangedListener` swaps flat `AudioClip[]` bundle through contiguous enum bounds, no `Enum.IsDefined` boxing | Alternative rejected: `Dictionary<string, AudioClip>` lookup | Estimate: cold event only
- [x] Task 14 - Subtitle coupling | DOD: playback emits `SubtitleSignal` and calls zero-GC `SubtitleManager.DisplaySubtitle(hash, span, duration)` | Alternative rejected: direct string caption events from producers | Estimate: cold event only
- [x] Task 15 - AUP shift safety | DOD: VWS state contains no AUP subscribers; playback cursors live inside DSP scalar state and are not reset by origin shifts | Alternative rejected: world-position-bound audio source warning | Estimate: 0.0 us on AUP shift
- [x] Task 16 - Zero-GC dictionary | DOD: clips are flat `AudioClip[]` indexed by warning byte ID | Alternative rejected: string-keyed audio table | Estimate: O(1) array read
- [x] Task 17 - Math LOD | DOD: Low/MX350/Unknown disables radio degradation branch | Alternative rejected: balanced always-on degradation | Estimate: saves ~0.04 us/sample on low tier during warning
- [x] Task 18 - No coroutines | DOD: cooldowns decay in `SlowTick` through Burst job; no coroutine/start coroutine in VWS | Alternative rejected: timed coroutine cooldowns | Estimate: <=0.2 us/SlowTick
- [x] Task 19 - Omega compile check | DOD: [BLOCKED BY DEPENDENCY] compile routes stop on pre-existing Core project missing namespaces before VWS/Burst diagnostics | Alternative rejected: reverting unrelated agents or editing generated csproj dependencies | Estimate: blocked

## Iteration Log

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; batch state was clean. Code audit pending.
- Loop 1: Tasks 1-5 implemented. Compile attempt 1: Unity batch compile could not own the already-open editor project; `dotnet build Hecton8.Core.csproj` is blocked by pre-existing generated project reference errors (`Hecton8.Core.Memory`, `Hecton8.Cartography`, `Hecton8.Physics.Determinism`) before VWS-specific validation.
- Loop 2: Tasks 6-10 implemented and prompt re-extracted from `CURRENT_BATCH.md`. VWS interruption uses scalar DSP state, not fader objects.
- Loop 3: Tasks 11-14 audited. No producer-side caption/audio call remains in the warning path; subtitles emit from the VWS playback point.
- Loop 4: Tasks 15-18 audited. AUP shifts are intentionally ignored by VWS state; low-tier DSP degradation is gated by `GlobalRegistry.ScalabilityTier`; no coroutine markers found in `VocalWarningSystem`.
- Loop 5: Compile wall confirmed after Unity batch launch, `dotnet build Hecton8.Core.csproj`, `dotnet build Hecton8.slnx`, and `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false`. All stop on missing `Hecton8.Core.Memory`, `Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `IDataVault`, `SystemID`, and signal types outside this audio task.
- Loop 6: `OMEGA_POLISH` executed after all tasks were checked or blocked. Purge scan found no VWS singleton, `List<AudioClip>`, `Dictionary<string, AudioClip>`, or coroutine in the warning path. Fixed two polish issues: staged queue cannot grow beyond 16 pending IDs, and language switching no longer uses `Enum.IsDefined`.
- Loop 7: User-requested non-build recheck on 2026-05-13. Re-extracted prompt with attribute-aware CLI regex from `CURRENT_BATCH.md`; task count remains 19. No `dotnet build` launched. Fixed hot-path service polling by caching renderer/subtitle/localization/tier in lifecycle and listening to `ScalabilityEvents`; prewarmed the staging `NativeQueue<byte>`; added renderer cancel flag, finite guards for severity/gain/PCM samples, and inspector/XML documentation. Static purge scan clean; `git diff --check` clean except LF-to-CRLF warnings.
- Loop 8: Non-build lifecycle recheck found a late-registration fault: cached renderer/subtitle/localization references could stay null if VWS enabled first. VWS now registers as a `GlobalRegistry` hot-swap/ref listener and rebinds those services through the existing deferred service-rebound lane. Public cancel/queue clearing guards disposed native storage, and VWS disables/renderer hot-swaps explicitly cancel orphaned PCM playback. No `dotnet build` launched.
