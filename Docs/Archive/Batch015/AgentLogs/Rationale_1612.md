# Rationale 1612

Date: 2026-06-01
Status: PENDING VERIFICATION

## R0 - Session Bootstrap

Problem: Agent 1612 must integrate narrative POI/scanner lore routing without managed strings in hot paths, but no current-batch ledgers existed.
Solution: Created fresh current-batch status and rationale ledgers, selected eight relevant mandates, and scoped the first loop to archaeology before code.
Rejected Alternatives: Starting with source edits before locating existing scanner/POI systems would risk inventing dependencies and breaking other agents' work.
Scalability potential: Low uses hash-only triggers and bounded work; Middle/High/Ultra can add richer visual/audio consequences in presentation lanes without changing gameplay truth.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding GC spikes from narrative string/event flow; no measured microseconds yet.

## R1 - Build Policy

Problem: The assignment contains a build task, but the user explicitly forbids dotnet build after small edits and AGENTS forbids builds under CPU/compiler contention.
Solution: Treat build as a gated exceptional action only after structural interface changes or Burst/job syntax risk, with CPU and compiler process checks first.
Rejected Alternatives: Running dotnet build as routine verification would starve parallel agents and violate direct user constraint.
Scalability potential: Static-source verification preserves cluster throughput; real runtime proof remains pending Unity/import/profiler artifacts.
Hardware Impact: Avoids multi-core MSBuild contention on the host; saved wall-clock CPU is host-side, not game runtime.

## R2 - Existing Hash Route Preserved

Problem: The prompt demanded removal of string lore IDs, but source inspection showed the runtime applied-lore route already uses `uint` packet hashes in `NarrativeDiscovery`, `ScannableFragment`, `NarrativeSpatialTriggerAuthoring`, and `MessageTerminal`.
Solution: Preserve the existing hash route and avoid deleting cold authoring labels that are not on the hot signal path. Proof route: `ScannableFragment` -> `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` -> `LoreFragmentScannedSignal`/`ScanCompleteSignal`.
Rejected Alternatives: A broad scanner rewrite or blind YAML/prefab mutation would risk breaking other agents' bindings and would not improve the runtime path.
Scalability potential: Low keeps scan completion as one hash/AUP payload; Middle/High/Ultra can add richer presentation reactions from the same immutable hash without changing gameplay truth.
Hardware Impact: No new runtime work. Low-end i3/MX350 avoids GC and avoids extra bus duplication; measured runtime gain absent.

## R3 - PDA Fail-Closed Corrupted Record

Problem: A missing or corrupt applied-lore span could leave the PDA without a meaningful body update and risk a broken player-facing state.
Solution: Added `WriteCorruptedBody(uint hash)` in `PDAEncyclopediaStreamer`, writing `[CORRUPTED DATA RECORD]` plus hex hash into the existing character lease before `SetCharArray`.
Rejected Alternatives: Throwing exceptions, assigning TMP strings, or allocating a formatted string would violate the UI streaming mandate.
Scalability potential: Low displays one short corrupted line; Middle/High/Ultra can layer CRT/noise/material effects on the same numeric fault without changing text ownership.
Hardware Impact: Prevents exception/freeze path and keeps UI delivery bounded. Estimated low-end gain is avoidance of a managed allocation/fault path; profiler microseconds not measured.

## R4 - Audio Glitch DTO In Existing Payload Padding

Problem: Audio-log events had no unmanaged corruption parameters, so degraded lore audio could not be represented without side-channel state or managed clip labels.
Solution: Replaced the existing 8-byte payload padding with explicit-layout `AudioGlitchParametersDTO` and derived corruption/bitcrush/pitch/bandpass data in `AudioLogSystem`.
Rejected Alternatives: A new audio event type, string effect names, or direct `SpatialAudioManager` API churn would add dependency surface and risk sibling-agent conflicts.
Scalability potential: Low uses bitcrush/bandpass/static fake; Middle increases pitch/bandpass variance; High/Ultra can spend saved CPU on richer DSP while consuming the same DTO.
Hardware Impact: No extra payload size; uses previously unused 8 bytes. Low-end i3/MX350 avoids managed side-channel lookup. Measured microseconds absent.

## R5 - Evidence Graph Cycle Scanner In Compiler

Problem: Route records could reference prerequisites in a cycle, creating an impossible evidence graph that runtime UI could not solve.
Solution: Added cold editor/compiler graph validation in `H8DataMonolithCompiler` for self-prerequisites and DFS cycle detection. Failure text includes `FatalArchitectureException`.
Rejected Alternatives: Runtime graph walking in PDA hot path or a separate JSON report/test artifact would add allocations or produce ignored paperwork.
Scalability potential: Low/Middle/High/Ultra all benefit from a deterministic baked graph; runtime presentation tiers remain independent.
Hardware Impact: Runtime impact is 0 us because validation is cold compiler/editor work. Build/import execution was not run in this pass.

## R6 - Burst Jobs Not Invented Without Route Card

Problem: Tasks 08 and 11 requested new Burst authority jobs, but the project already has hash-bit prerequisite validation and lore-driven world-impact signals. Adding another writer would create a second authority route.
Solution: Documented those tasks as partial/pending route card, preserved existing first-party path, and added only cold validation plus presentation-safe runtime glue.
Rejected Alternatives: Direct DataVault scatter/voxel write locks from this agent would cross domain boundaries without owner, phase, lock, or proof contract.
Scalability potential: Low avoids unsafe duplicate authority; Middle/High/Ultra can consume existing biome/acoustic signals to buy richer visuals after owner approval.
Hardware Impact: Avoids hidden `.Complete()` or tiny-job overhead on weak CPUs. Runtime microseconds saved are not claimed.

## R7 - Dry-Run Trace

Problem: The pipeline must be reasoned through without relying on a compile/build loop.
Solution: Static trace: player scans fragment -> scanner/fragment has applied lore hash -> `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` publishes hash/AUP payload -> PDA consumes hash bus -> prerequisite route checks packet bit indexes -> unmet prerequisite marks encrypted/corrupted state -> PDA renders from fixed char buffer using `SetCharArray`; audio-log route derives DTO from depth/hash and publishes unmanaged payload.
Rejected Alternatives: Treating this as proven runtime behavior would be false without Unity/profiler logs.
Scalability potential: Low shows short text/static; Middle/High/Ultra add stronger CRT/audio degradation and biome dressing from same hash signal.
Hardware Impact: Expected low-end benefit is stable no-string path; measured proof absent.

## R8 - APEX Verifier Extended Instead Of New Report Format

Problem: The project needed proof that PDA corrupted records and audio-log degradation stay phase-safe and zero-GC, but JSON reports were explicitly rejected and the existing verifier did not cover those new routes.
Solution: Extended `H8NarrativeApexVerifier` to inspect PDA corrupted-record char-span delivery and audio glitch DTO/late-frame flush routing directly from C# source. Audio audit files are parsed as extra audit inputs without adding them to hot-root traversal, avoiding false `GlobalRegistry` registration findings from unrelated audio service lifecycle helpers.
Rejected Alternatives: A separate report generator or broad runtime test harness would add I/O and likely require build/editor execution. Adding audio files to normal hot-root traversal would flag lifecycle registration paths outside this agent's route.
Scalability potential: Low/Middle/High/Ultra share the same verifier contract; presentation richness can evolve while verifier locks the data route.
Hardware Impact: Runtime impact is 0 us. Editor-only source scan cost exists only when manually invoked.

## R9 - Continuous Audio Glitch Quality Scaling

Problem: Audio glitch DTO values were derived from depth/hash corruption, but did not yet consume the project-wide continuous `GlobalQualityWeight`.
Solution: `ResolveAudioGlitchParameters` now applies `GlobalQualityWeight` as a continuous scalar for bitcrush, pitch, and bandpass intensity. Low devices get tamed processing; high/ultra get stronger data-ghost coloration using the same DTO layout.
Rejected Alternatives: Binary low/high quality branches or separate audio payload structs would violate the scalability pillar and risk DTO drift.
Scalability potential: Low = restrained corruption parameters; Middle = full corruption baseline; High/Ultra = stronger pitch/bandpass overdrive for richer acoustic ghosts.
Hardware Impact: Adds several scalar math operations on playback start only, not per audio sample and not per frame. No measured runtime microseconds.

## R10 - Build Gate Remained Closed

Problem: The APEX request demanded proof while forbidding build spam; a live `dotnet` process was also present on the host.
Solution: Used static `rg`, source-window review, `git diff --check`, targeted method-body scanning, and process inspection. Did not launch `dotnet build`, MSBuild, csc, or Unity compilation.
Rejected Alternatives: Building during another active dotnet process would violate host throttling and user command.
Scalability potential: Preserves parallel-agent host throughput.
Hardware Impact: Avoided additional compile load on the workstation; game runtime impact unchanged.

## R11 - Final APEX Static Checkpoint

Problem: Final response needed current proof after context compaction, not stale memory.
Solution: Re-ran agent-scope hot-method scan, lock scan, line-whitespace scan, targeted symbol search, `git diff --check`, and process inspection. Active `dotnet` processes `13952` and `31232` kept the build gate closed.
Rejected Alternatives: Running a full project scan or build would burn host CPU under known contention and exceed the user's no-spam compilation rule.
Scalability potential: Keeps source-level contracts enforced while preserving low-end/cluster host throughput; runtime richness still scales through `GlobalQualityWeight`.
Hardware Impact: No game runtime delta. Host CPU avoided another compile workload; exact CPU milliseconds not claimed.

## R12 - Finite Audio Ghost Payloads

Problem: The audio-log glitch DTO route could accept malformed public-producer values and could derive NaN presentation values from bad depth, radiation, volume, or quality input.
Solution: Added `AudioGlitchParametersDTO.Sanitize`, enqueue-side sanitization, playback-side safe DTO transfer, and finite `Sanitize01` guards around depth/radiation/quality/acoustic impact math.
Rejected Alternatives: Throwing exceptions, dropping all corrupted audio, or relying on editor data purity would leave a runtime memory-corruption vector in the presentation lane.
Scalability potential: Low uses bounded restrained bitcrush/bandpass; Middle/High/Ultra still scale through continuous `GlobalQualityWeight` without changing DTO layout or gameplay truth.
Hardware Impact: Adds bounded scalar comparisons only on audio-log playback or scan-complete signal handling, not per sample and not per frame. Measured runtime microseconds absent.

## R13 - Audio Playback Lifecycle Cancellation

Problem: Full audio-log playback queued glitch presentation but did not preserve the bitcrush-route result in playback state, and `StopPlayback` could leave a pending `LateFrameTick` clip queued after the user/system stopped the log.
Solution: Store the full-playback `QueuePlaybackVisualSync` return value, sanitize all playback durations through `ResolvePlaybackDuration`, sanitize event duration payloads at `AudioLogEvents.Enqueue`, and clear/unregister pending visual sync during `StopPlayback`.
Rejected Alternatives: Listener-side cleanup would allow stale presentation state to cross phases; clamping only in authored data would not protect public producers or corrupted runtime payloads.
Scalability potential: Low avoids stale stopped audio and NaN timers; Middle/High/Ultra still get stronger glitch presentation through the same DTO and `GlobalQualityWeight` without extra truth routes.
Hardware Impact: Adds scalar checks on playback start/enqueue/stop paths only. No per-sample DSP work, no per-frame allocation, and no measured runtime microseconds.

## R14 - MessageTerminal Phase Hygiene

Problem: `MessageTerminal.Tick` accepted raw `deltaTime`, `StartPlayback` trusted authored/clip duration, and pending terminal UnityEvent payload refs were not cleared on lifecycle exit. A NaN timer or stale queued event can leak presentation state across phases even if the applied-lore hash lane is clean.
Solution: Added finite `SanitizeDeltaTime`, bounded `ResolvePlaybackDuration`, centralized `ClearQueuedTerminalEvents`, and source-verifier counters for terminal time guards and queue cleanup.
Rejected Alternatives: Removing the existing `UnityEvent<string>` callbacks would break serialized compatibility and outside-domain consumers; adding a second signal lane for these legacy audio/UI callbacks would be a route-card change, not a safe local hardening pass.
Scalability potential: Low devices avoid bad timers and disabled-object event retention; Middle/High/Ultra keep the same TerminalOS hash preview path and can spend presentation budget on CRT/material/audio effects without changing state ownership.
Hardware Impact: Adds scalar checks in `Tick` and playback start only. The steady-state cost is bounded branch math; no allocations, no scene lookups, no DataVault locks, and no measured microseconds.

## R15 - MessageTerminal Presentation Scalar Boundaries

Problem: After phase queue cleanup, terminal presentation still had scalar leak points: `blinkInterval` could remain NaN/Infinity outside `Range` enforcement, queued static audio volume used `Mathf.Clamp01` without an explicit finite guard, and editor-filled clip duration persisted raw `AudioClip.length`.
Solution: Added `SanitizeBlinkInterval`, `Sanitize01`, runtime blink interval clamp, queued static audio volume clamp, and editor duration normalization through the existing duration sanitizer. Extended source verifier counters for presentation scalar guards.
Rejected Alternatives: Trusting inspector `Range` attributes and Unity `Mathf.Clamp01` would not prove finite state transfer; adding a new audio route would be overreach for a local terminal scalar defect.
Scalability potential: Low avoids NaN blink/audio state and disabled instrument oddities; Middle/High/Ultra keep the same first-party hash preview route and can use saved stability margin for richer CRT/audio material response in `LateFrameTick`.
Hardware Impact: Adds bounded scalar comparisons on NewMessage ticks and queue paths only. No allocation, no registry lookup, no DataVault lock, no job completion, and no measured runtime microseconds.

## R16 - TerminalOS Graphics Rebuild Phase Gate

Problem: `TerminalOsRuntime.SlowTick` flushed pending graphics-resource rebuilds, including RenderTexture release/recreation and dirty-state forcing, outside the visual-sync phase. That could run while scheduled jobs still held native-buffer aliases and violated the phase contract for presentation resource mutation.
Solution: Moved rebuild flushing into `LateFrameTick` after scheduled job finalization. `FlushPendingGraphicsResourceRebuild` now returns a boolean and refuses to release or recreate graphics resources while format, click resolve, terminal interaction, or decryption jobs remain scheduled. The pending flag is preserved for the next visual-sync pass.
Rejected Alternatives: Calling `.Complete()` to force a rebuild would stall the frame. Leaving rebuild in `SlowTick` would keep presentation mutation in the wrong phase. Creating a second rebuild queue would add route surface without need.
Scalability potential: Low avoids RT churn during deferred jobs; Middle/High/Ultra keep continuous `GlobalQualityWeight` resolution scaling, but actual graphics resource mutation is phase-owned and job-safe.
Hardware Impact: Adds branch checks only when a graphics rebuild is pending. No new allocation in steady hot path, no DataVault write lock, no forced job completion, and no measured runtime microseconds.

## R17 - TerminalOS Runtime Quality Rebuild Unblock

Problem: `RefreshScalabilityPolicy` computed a target RenderTexture resolution from `GlobalQualityWeight`, but returned early during play mode whenever the texture array already existed. Runtime quality changes could update cadence and panel data, but RT resolution stayed frozen.
Solution: Removed the play-mode texture-exists early return. Resolution changes now update `_textureResolution` and queue the existing visual-sync rebuild path. The actual RenderTexture release/recreate still happens only through the `LateFrameTick` scheduled-job gate.
Rejected Alternatives: Keeping fixed runtime RT resolution violates continuous quality scaling. Rebuilding immediately inside the quality resolver would mix policy calculation with presentation mutation. Adding binary tier branches would violate the scalability pillar.
Scalability potential: Low can downshift TerminalOS RT cost after hardware/load feedback; Middle/High/Ultra can upscale terminal clarity through the same continuous scalar and existing shader/material route.
Hardware Impact: No steady-frame allocation. Resolution check already existed; the new behavior only queues a rebuild when aligned target resolution changes. No measured runtime microseconds.

## R18 - ScannableFragment Late-Frame Queue Cleanup

Problem: `ScannableFragment` queued presentation/audio and legacy `UnityEvent<string>` scan-complete payloads for `LateFrameTick`, but lifecycle exit did not clear those pending fields. A disabled or reused fragment could retain stale presentation state or a managed unlock-id reference.
Solution: Added `ClearQueuedLateFrameWork` and called it from `OnDisable`, `OnDestroy`, and `ResetState`. The method clears pending visual/audio/event flags, scalar progress payloads, particle position, and `_pendingCompleteEventUnlockId`. The first-party applied-lore unlock path remains hash/AUP through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`.
Rejected Alternatives: Removing `UnityEvent<string>` would break serialized compatibility. Dispatching legacy events immediately from scan completion would move presentation/mod callbacks into the simulation path.
Scalability potential: Low avoids stale fragment callbacks and disabled-object presentation drift; Middle/High/Ultra keep richer scan VFX/audio in `LateFrameTick` without changing the hash authority route.
Hardware Impact: Cleanup runs on lifecycle/reset only. No new hot allocation, no DataVault lock, no registry lookup, and no measured runtime microseconds.

## R19 - NarrativeDiscovery Cached Lore Hash

Problem: `NarrativeDiscovery.Interact` and `TryGetSpatialTrigger` computed `LocHash.ComputeAscii(discoveryId)` from a managed string during runtime interaction/spatial reads. It did not allocate, but it kept string-derived identity work on the runtime route instead of using the cold cached hash model used elsewhere in this domain.
Solution: Added `_cachedLoreHash`, computed it in `RefreshAupTriggerCache`, and routed both `ILoreUnlockSink.TryUnlockByHash` and `NarrativeSpatialTriggerAuthoring.LoreHash` through the cached value. Discovery hash and applied-lore packet hash routes remain unchanged.
Rejected Alternatives: Reusing `_cachedDiscoveryHash` would change hash semantics because `NarrativeEvents.ComputeDiscoveryHash` uses `LocHash.Compute`, while lore unlocks used ASCII FNV. Removing the legacy lore sink call would break existing LoreDatabase consumers.
Scalability potential: Low avoids repeated managed-string hash work on interaction-heavy scenes; Middle/High/Ultra can keep dense POI layouts without increasing interaction route cost.
Hardware Impact: Removes per-interaction/per-read hash loops over `discoveryId`; adds one cached uint field and cold hash calculation on enable/validate/configure. No measured runtime microseconds.

## R20 - HectonNarrativeDirector Cached POI Hash Route

Problem: `HectonNarrativeDirector.GetNearestUndiscoveredPOI` and `DispatchAupNarrativePoiSolvedResult` still derived discovery hashes from managed `discoveryId` strings during runtime POI selection or solved-result dispatch.
Solution: Added `_poiDiscoveryHashes` as a cold registry cache parallel to `_poiDiscoveryIds`, filled it from `NarrativeSpatialTriggerAuthoring.PoiHash`, used `poi.DiscoveryHash` for nearest-POI filtering, and used `_poiDiscoveryHashes[poiIndex]` for solved-result dispatch with `poiHash` fallback.
Rejected Alternatives: Removing `_poiDiscoveryIds` would break save/compatibility identity flow. Recomputing from strings in dispatch is cheap but violates the hash-only hot-route rule. Adding a new signal lane would duplicate existing AUP POI route ownership.
Scalability potential: Low removes repeated string-hash loops in dense POI scenes; Middle/High/Ultra can increase POI density and terminal/scanner dressing while preserving flat hash identity routing.
Hardware Impact: Runtime measured: 0. Expected low-end i3/MX350 gain is removal of one FNV loop per POI candidate/solved dispatch; added memory is one `uint[64]` cold allocation.

## R21 - Applied Lore World-Impact Phase Split

Problem: `ConsumeAppliedLoreWorldImpactSignals` ran from `LateFrameTick` and called `H8AppliedLoreRuntime.TryRaiseScanCompleteWorldImpact`, which can publish `BiomeChangedSignal`. That signal is not pure UI; it can feed biome/world consumers, so draining it from visual sync was a phase leak.
Solution: Moved `ConsumeAppliedLoreWorldImpactSignals` to `Tick`. It still reads the unmanaged `ScanCompleteSignal` snapshot and publishes only unmanaged `BiomeChangedSignal`/`ToolAcousticSignal`. The direct `ISpatialAudioNarrativeRadioSink` call is now deferred through `_pendingAppliedLoreAudioGhost01` and `_hasPendingAppliedLoreAudioGhost`, then flushed in `LateFrameTick`.
Rejected Alternatives: Leaving the drain in `LateFrameTick` would keep world-impact publication in the wrong phase. Moving the direct audio sink call into `Tick` would mix presentation control with simulation/update work. Adding a new queue object or managed event would violate the zero-GC route.
Scalability potential: Low gets deterministic world-impact signals without visual-phase drift; Middle/High/Ultra can increase audio/biome presentation intensity from the same flat signal route and continuous quality systems.
Hardware Impact: Runtime measured: 0. Added cost is one snapshot scan in `Tick` and two scalar fields for phase transfer; no allocation, no lock, no registry lookup in the hot method.

## R22 - PDA Universal Quality Guard

Problem: `PDAEncyclopediaStreamer` used raw `math.saturate(HomeostasisBrain.GlobalQualityWeight)` at decode, unlock-state, and token-formatting sites. If the global scalar ever became NaN/Infinity during platform adaptation, PDA decode budget and typewriter cadence could inherit invalid math.
Solution: Added `ResolveGlobalQualityWeight01` and routed PDA decode budget, runtime DTO state, and `QUALITY` token formatting through it. The resolver is pure scalar code: finite global quality clamps to [0..1], invalid quality falls back to `0.5f`.
Rejected Alternatives: Reading the previous value back from DataVault inside the resolver would create a hidden hot-path buffer dependency. Binary low/high quality branches would violate continuous scaling.
Scalability potential: Low survives bad platform telemetry with stable middle throughput; Middle/High/Ultra keep continuous quality-driven decode/typewriter speed without DTO or save identity changes.
Hardware Impact: Runtime measured: 0. Cost is one finite check and clamp at existing PDA quality read sites. No allocation, no lock, no scene lookup, no build run.

## R23 - Applied Lore World-Impact Idempotence

Problem: SignalBus snapshots persist until the next post-simulation flush. If a future dispatcher path observes the same `ScanCompleteSignal` snapshot more than once, applied-lore world impact could republish biome/audio consequences for a single reveal.
Solution: Cached the last processed `EntryHash`, `ScanId`, and `SourceId` in `HectonNarrativeDirector_PoiTriggers` and skipped duplicates before `H8AppliedLoreRuntime.TryRaiseScanCompleteWorldImpact`. Lifecycle clear resets the cache.
Rejected Alternatives: Mutating or filtering the shared SignalBus snapshot would violate lane ownership. A managed HashSet/ring would allocate and overbuild a one-signal idempotence problem.
Scalability potential: Low avoids duplicate biome/audio churn; Middle/High/Ultra can make reveal consequences stronger without risking repeated scatter/fog/audio pulses from one observation.
Hardware Impact: Runtime measured: 0. Adds three uint comparisons per scan-complete signal and three uint fields. No DataVault lock, no allocation, no registry lookup.

## R24 - PDA Accessibility Reveal Route

Problem: PDA lore reveal was tied entirely to typewriter pacing. On weak devices or for long articles, the player had no zero-GC route to reveal the already decoded text quickly without waiting for presentation cadence.
Solution: Added `RequestInstantReveal` as a public request setter and applied it only inside `LateFrameTick` through `ForceRevealDecodedTextIfRequested`. The method advances `_visibleLength` to the decoded buffer length and clears `_charAccumulator`; existing `SetCharArray` submission remains the only text output route.
Rejected Alternatives: Immediate TMP mutation from the request method would violate phase ownership. Assigning `.text` or constructing a full string would violate the PDA streaming mandate. Forcing full-source decode in one frame would risk stalls on long articles.
Scalability potential: Low lets players bypass slow typewriter reveal for decoded text; Middle/High/Ultra keep atmospheric typewriter by default and can expose the same request through controls/accessibility bindings.
Hardware Impact: Runtime measured: 0. Added steady cost is one bool branch per visible PDA LateFrame. No allocation, no lock, no registry lookup, no build run.

## R25 - PDA UI Rescale Accessibility Route

Problem: The project already emits unmanaged `UIRescaleRequestSignal` after localized font swaps, but the PDA encyclopedia did not consume it. PDA lore text could stay at authored scale even when a UI accessibility/font-rescale source requested a continuous scale change.
Solution: Added cold `SignalBus<UIRescaleRequestSignal>` initialization, baseline TMP font capture, a `LateFrameTick` snapshot consumer, finite `FontScale` clamps, and primitive duplicate suppression by frame/source/reason/font-scale bits. Font scalar mutation stays in visual sync and text content still flows only through `SetCharArray`.
Rejected Alternatives: Using managed UI events would allocate and couple PDA to a service object. Using `TryConsumeFrame` would steal the legacy UI rescale lane from other consumers. Calling scene lookup or registry resolution during `LateFrameTick` would violate cold-dependency rules.
Scalability potential: Low uses larger readable PDA text without changing decode cadence; Middle uses default font scale; High/Ultra can combine larger readable text with denser CRT/noise presentation while preserving the same hash/span text route.
Hardware Impact: Runtime measured: 0. Added work is one snapshot length check per active PDA LateFrame plus a bounded loop over 32-byte unmanaged signals when present. No allocation, no DataVault lock, no scene lookup, no build run.

## R26 - UI Rescale Broadcast Preservation

Problem: `DiegeticHudManualLayout.FlushGlobalRescaleRequests` used `SignalBus<UIRescaleRequestSignal>.TryConsumeFrame`, advancing the legacy cursor for a shared UI signal. That makes the rescale lane less universal: layout rebuild can consume the signal before PDA/accessibility or other snapshot consumers reason about it.
Solution: Replaced the destructive read with `GetFrameSnapshot`, added primitive duplicate suppression for frame/source/reason/font-scale bits, and reset dedupe state during subsystem registration. The layout still rebuilds registered HUD transforms only when a new rescale payload is observed.
Rejected Alternatives: Keeping `TryConsumeFrame` preserves legacy semantics but violates broadcast-lane ownership. Adding a managed event/service router would allocate and introduce direct dependencies. A managed set of seen signals would overbuild a four-field idempotence problem.
Scalability potential: Low avoids duplicate layout rebuild and keeps accessibility scaling signals available to PDA; Middle keeps default behavior; High/Ultra can add more diegetic UI consumers without fighting a destructive queue cursor.
Hardware Impact: Runtime measured: 0. Flush path adds a snapshot length check and primitive comparisons over 32-byte signals. No allocation, no DataVault lock, no scene lookup, no build run.

## R27 - Accessibility Text Scale Producer

Problem: PDA and diegetic layout now consume `UIRescaleRequestSignal`, but there was no active accessibility/settings producer for player text scale. The only producer was localized font swap, so text accessibility was still not a complete service.
Solution: Added finite `textScale` ownership and `SetTextScale(float)` to `AccessibilitySettings`, publishing only from `VisualSyncTick` through `FontStreamingManager.RequestAccessibilityTextScale`. `FontStreamingManager` sanitizes the scale, publishes the existing unmanaged 32-byte signal with reason `2`, and passes the same payload directly to `DiegeticHudManualLayout.ApplyGlobalRescaleRequest` for immediate layout refresh. Snapshot consumption remains available for PDA and other broadcast readers.
Rejected Alternatives: New global accessibility bus was rejected because `UIRescaleRequestSignal` already exists and is the correct first-party UI broadcast lane. Writing text scale into `AccessibilityConfigDTO` was rejected because it would mutate the 16-byte shader cbuffer contract and force unrelated renderer consumers to update. Direct TMP mutation from settings was rejected because presentation owners must apply text changes in visual sync.
Scalability potential: Low uses readable larger PDA/diegetic text with one scalar signal; Middle keeps default scale; High/Ultra can combine readable text with sharper terminal/PDA shaders and richer CRT dressing without changing gameplay truth, DTO layout, or lore hashes.
Hardware Impact: Runtime measured: 0. Changed-scale cost is one finite clamp and one bounded signal enqueue from `VisualSyncTick`; steady unchanged scale exits on primitive comparisons. No allocation, no DataVault lock, no scene lookup, no build run.

## R28 - Persisted Accessibility Text Scale Service

Problem: The accessibility text-scale lane had a producer and consumers, but the player-facing settings route was incomplete. Without persistence and SettingsPanel controls, the service remained a developer API instead of a usable option.
Solution: Added `Hecton_TextScale` to `SettingsManager`, loaded/reset/applied it through the existing options batch, exposed it in `SettingsPanel` with a cached slider action and prebuilt percent labels, and initialized `UIRescaleRequestSignal` at the producer before publish.
Rejected Alternatives: Editing prefab YAML was rejected because scene assets are shared with other agents and brittle. A new accessibility manager was rejected because `AccessibilitySettings`, `SettingsManager`, and `UIRescaleRequestSignal` already own the route. Live TMP `.text` preview was rejected because PDA/diegetic text owners must apply presentation through visual-sync and `SetCharArray`.
Scalability potential: Low uses larger text for readable PDA and diegetic panels with minimal work; Middle keeps default 1.0 scale; High/Ultra can combine larger readable text with higher terminal/PDA render resolution and richer CRT presentation without changing lore hashes, save identity, DTO layout, or gameplay truth.
Hardware Impact: Runtime measured: 0. Added steady hot cost is none in scanner/PDA text streaming. Settings UI cost is cold row creation plus slider callback scalar clamp; apply path is one finite clamp and one unmanaged signal enqueue. No DataVault lock, no managed collection growth, no build run.

## R29 - Persisted UI Motion Comfort Service

Problem: Text accessibility had a player-facing scalar, but UI shock motion still used authored shake intensity only. Destructive/reset UI feedback could remain uncomfortable on weak/mobile/VR-adjacent setups, and there was no persisted scalar route to reduce it without deleting the cue.
Solution: Added persisted `UiMotionScale` in `SettingsManager`, a cold auto-created settings slider in `SettingsPanel`, VisualSync-owned `AccessibilitySettings.SetUiMotionScale`, and a static finite scalar consumed by `UIScreenShake.LateFrameTick`. Transform writes remain only in `LateFrameTick`; settings and accessibility transfer only primitive floats/bools.
Rejected Alternatives: Disabling all camera/VFX shake was rejected as cross-domain overreach. A new managed accessibility event was rejected because this is a UI-only scalar and not a gameplay truth route. Editing prefab YAML was rejected because scene-authored settings panels are shared with other agents.
Scalability potential: Low uses 0.0-0.35 motion for comfortable stable UI; Middle keeps 0.6-1.0 authored shock; High/Ultra can retain full UI impact while using saved comfort budget for sharper terminal/PDA presentation. This does not change lore hashes, save identity, DTO layout, or scanner authority.
Hardware Impact: Runtime measured: 0. UIScreenShake adds finite scalar checks and one multiply inside an existing LateFrame-only effect. Settings UI allocation is cold row construction only. No DataVault lock, no registry lookup in hot methods, no managed string output.

## R30 - Audio-log Subtitle Phase Bridge

Problem: `SystemDispatcher` runs `ILateFrameTickable.LateFrameTick` before flushing late-frame event arteries. `AudioLogEvents.FlushPending` can therefore invoke `SubtitleManager.OnAudioLogEvent` after the subtitle visual tick for that frame. The previous callback path could call audio-log subtitle preparation immediately, including cue notification and sensory pulse emission, from the event flush phase instead of the subtitle visual owner phase.
Solution: Added a fixed `PendingAudioLogSubtitleEvent[8]` ring in `SubtitleManager`. `OnAudioLogEvent` now writes only `AudioLogEventType`, `LogHash`, and sanitized `DurationSeconds` into the ring. `LateFrameTick` drains the ring through `DrainPendingAudioLogEventsVisualSync`, then dispatches `HandleAudioLogPlaybackStarted`/`HandleAudioLogPlaybackEnded`. The callback no longer registers tickables, allocates, calls presentation handlers, touches TMP, or emits sensory pulse signals.
Rejected Alternatives: Calling presentation handlers directly from `OnAudioLogEvent` was rejected because dispatcher order makes it post-visual-tick work. A managed event/list/queue was rejected because audio-log subtitle transfer is bounded and primitive. Reordering `SystemDispatcher` late-frame arteries was rejected as cross-domain risk for 20+ agents.
Scalability potential: Low gets deterministic one-frame-late subtitle/audio cue presentation without managed allocation or callback stalls; Middle keeps current diegetic audio-log behavior; High/Ultra can add richer waveform/sensory dressing because callback work remains a bounded value copy.
Hardware Impact: Runtime measured: 0. Added cost is at most one bounded 8-slot ring drain per active subtitle visual tick and three primitive fields per queued audio-log event. No DataVault write lock, no registry lookup, no component lookup, no managed string output, no build run.

## R31 - MessageTerminal Cached Hash Event Route

Problem: `MessageTerminal` already published applied-lore terminal preview through a fixed signal lane, but local read tracking and playback/new-message events still used `messageId` strings as the runtime identity route. `UpdatePendingMessage` could consult a string `HashSet`, and playback completion from `Tick` still pulled a legacy id before queuing completion.
Solution: Added baked `MessageEntry.messageHash`, cold `_messageHashes`, and flat `_readMessageHashes`. Pending-message scans now use cached uint identity and read-hash checks. Playback/new-message event queues carry both uint hash and legacy string; `LateFrameTick` fires hash events first, then old `UnityEvent<string>` for serialized compatibility.
Rejected Alternatives: Removing legacy string UnityEvents would break scene/prefab bindings. Replacing the read set with a managed `HashSet<uint>` was rejected because the message count is tiny and a flat array avoids managed collection overhead in the scanned route. Computing the hash in `Tick` was rejected; completion reads the cached uint by index.
Scalability potential: Low can run many outpost terminals without string read-set checks in pending scans; Middle keeps current terminal behavior; High/Ultra can add richer terminal/PDA preview dressing while the message identity route remains a primitive hash.
Hardware Impact: Runtime measured: 0. Hot-method scanner found zero registry/component lookup, allocation, resize, and HashSet tokens in `Tick`, `LateFrameTick`, `StartPlayback`, `CompletePlayback`, `UpdatePendingMessage`, and `FlushQueuedTerminalEvents`. Build remained skipped because CPU was `87%` and active `dotnet` PID `18584` was present.

## R32 - Scanner LoreFragment AUP Payload

Problem: Task 07 required scanner completion to publish `LoreFragmentScannedSignal` with hash/AUP, but the scanner only emitted `ScanCompleteSignal` plus legacy scan events. PDA could consume `LoreFragmentScannedSignal`, yet that payload had no AUP and fell back to last-discovery state.
Solution: Expanded `LoreFragmentScannedSignal` to a 64-byte explicit-layout payload with `AbsoluteUniversePosition` at offset 0 and primitive identity fields at offsets 48/52/56/60. `ScannerDataMiningRouter.RouteCompletionIfNeeded` now publishes the signal with AUP and paired-complete flags. `PDAEncyclopediaStreamer` reads the AUP directly from the signal. `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` propagates finite AUP into the same lore-fragment lane.
Rejected Alternatives: Keeping AUP only in `ScanCompleteSignal` was rejected because it leaves Task 07 incomplete and forces PDA fallback logic. Adding a second managed scanner callback was rejected because it duplicates authority and reintroduces managed routing. Changing `ScanCompleteSignal` alone was rejected because PDA already has a lore-fragment consumer.
Scalability potential: Low receives one aligned 64-byte scan payload and no string path; Middle/High/Ultra can attach richer PDA/audio/world reactions from the same hash/AUP signal without changing gameplay truth.
Hardware Impact: Runtime measured: 0. Added cost is one 64-byte SignalBus enqueue per completed scan and one flag/AUP read in PDA snapshot consumption. No allocation, no DataVault write lock, no scene lookup, no compiler run.

## R33 - Scanner LoreFragment AUP Integrity

Problem: The new AUP-bearing lore-fragment lane had two hardening gaps: hash-only producers could pass a stale `FlagHasAup`, and PDA read `signal.PositionAup` but still called `UnlockEntry` with `hasPreciseAup=false`.
Solution: Masked `FlagHasAup` out of `TryRaisePacketUnlocked`, cleared it in `TryRaisePacketUnlockedAt` when AUP is non-finite, and propagated `hasSignalAup` into the PDA unlock state update. Tightened `H8NarrativeApexVerifier` to inspect the target struct body and count hash-only flag stripping.
Rejected Alternatives: Trusting caller flags was rejected because payload metadata becomes unprovable. Treating AUP as display-only was rejected because PDA distance/state tokens need the same discovery fact. Running a build was rejected because CPU sampled 91 percent.
Scalability potential: Low keeps one truthful 64-byte payload and deterministic fallback; Middle/High/Ultra can layer richer PDA/audio/biome presentation from the same hash/AUP fact without extra route identity.
Hardware Impact: Runtime measured: 0. Cost is two bitwise masks on publication and one bool branch per lore signal. No allocation, no DataVault write lock, no scene lookup, no build run.

## R34 - Scanner Paired Signal Dedup

Problem: Scanner and applied-lore finite-AUP routes publish both `ScanCompleteSignal` and `LoreFragmentScannedSignal` for one reveal. PDA consumed both snapshots in the same `LateFrameTick`, causing duplicate unlock/select work for the same hash/source even though the second pass usually found the entry already unlocked.
Solution: Added a local primitive duplicate guard in `PDAEncyclopediaStreamer`: lore-fragment signals marked `FlagPairedScanComplete` are skipped when a matching scan-complete payload exists in the same snapshot. `H8AppliedLoreRuntime` now sets that paired flag only on the finite-AUP route that also publishes `ScanCompleteSignal`, and clears it on hash-only or non-finite routes.
Rejected Alternatives: Destructive `TryConsumeFrame` was rejected because the lane is broadcast. A managed `HashSet`/queue was rejected because source snapshots are already bounded and primitive. Removing one of the two signals was rejected because world-impact/PDA/lore consumers use different first-party views of the same scan fact.
Scalability potential: Low keeps one PDA state write per reveal and avoids repeated metadata/prerequisite work; Middle keeps existing fan-out; High/Ultra can add richer PDA/audio/biome reactions without duplicate UI unlock churn.
Hardware Impact: Runtime measured: 0. Added cost is a bounded primitive scan over current-frame scan-complete payloads only when lore-fragment signals are present. Removed cost is a duplicate `UnlockEntry`/metadata path for paired scanner/applied-lore reveals. No allocation, no DataVault lock, no scene lookup, no build run.

## R35 - Scanner ScanEvents Cold Prewarm

Problem: `ScannerDataMiningRouter.RouteCompletionIfNeeded` still publishes the legacy `ScanEvents.TryRaiseEntryDiscovered` bridge after the first-party scanner signals. If no listener registered first, `ScanEvents.Enqueue` could lazily allocate persistent native queues from the scanner completion path.
Solution: Added `ScanEvents.EnsureInitializedCold` and called it from `ScannerDataMiningRouter.OnEnable`, before runtime completion can enqueue scan events. Extended `H8NarrativeApexVerifier` to require both the cold method and scanner prewarm call.
Rejected Alternatives: Deleting `ScanEvents` was rejected because legacy listener compatibility is outside this pass. Moving `TryRaiseEntryDiscovered` earlier or later does not remove the lazy-allocation hazard. A new bridge queue would duplicate the existing owner.
Scalability potential: Low avoids a first-scan native queue allocation hitch; Middle keeps current scan-event listener behavior; High/Ultra can layer richer scan feedback on the already-warmed bridge while first-party hash/AUP signals remain authoritative.
Hardware Impact: Runtime measured: 0. Steady completion path keeps the same enqueue work, but queue creation/prewarm is forced into cold `OnEnable`. No new DataVault lock, scene lookup, managed string path, or build run.
