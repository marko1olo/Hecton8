# Rationale 1710 - Acoustic Occlusion & UI Particle Engineer

State: IMPLEMENTED WITH BLOCKED DEPENDENCIES

## Session Start
Problem: Domain authority must be source-backed through active docs instead of a retired standalone domain file.
Solution: Treat XML role plus assigned paths as the active domain: `Assets/_Project/Scripts/Audio/`, `Assets/_Project/Scripts/UI/`, and explicitly named `Assets/_Project/Scripts/SpatialAudioManager.cs`.
Rejected Alternatives: Guessing broader project ownership would risk cross-domain edits.
Scalability potential: Low/Middle/High/Ultra unchanged; this is authority scoping only.
Hardware Impact: 0 us runtime impact.

Problem: Relevant mandates required before coding.
Solution: Selected audio DSP, acoustic occlusion, GlobalRegistry DI, SignalBus, execution phases, struct layout, telemetry, and AUP mandates.
Rejected Alternatives: Reading unrelated AI/rendering mandates would add no proof for audio/UI allocation removal.
Scalability potential: Low uses cached audio fake/distance fallback; Middle/High/Ultra can raise occlusion cadence and DSP richness via continuous GlobalQualityWeight.
Hardware Impact: 0 us runtime impact.

## Implementation Decisions

Problem: Master audio did not have a global depth-pressure low-pass tied to listener depth.
Solution: Added `ApplyMasterDepthPressureLowPass` to `SpatialAudioManager`. It consumes cached listener AUP runtime Y in the presentation tick, maps `-50m..-300m` to `22000Hz..400Hz` through precomputed log-frequency constants, smooths with existing `FastDecayBlend`, clamps finite state, and writes `MasterDepthPressureLowPassCutoffHz` only when cutoff changes by more than 1 Hz.
Rejected Alternatives: Reading depth from the absent `AcousticStateDTO` would require inventing contested vault ownership. Linear-Hz interpolation was rejected because perceived cutoff collapse is weaker than a logarithmic audio curve. Per-source pressure simulation was rejected as cost without authority need.
Scalability potential: Low/Middle/High/Ultra all receive the same deterministic presentation curve; richer existing virtual voice budget still scales through `GlobalQualityWeight`.
Hardware Impact: Estimated 3 us saved on i3/MX350 on unchanged frames by avoiding redundant mixer `SetFloat`; no new heap allocation.

Problem: `UIParticleEffect` created a runtime particle GameObject and `ParticleSystem` component when no prefab was available.
Solution: Rewrote the class around explicit `[SerializeField] ObjectPoolManager _uiEffectPool`, `_authoredParticlePrefab`, and fallback cached `IObjectPoolService`. Spawn uses `allowExpand=false`; disable/despawn returns the instance to pool. The old `particlePrefab` serialization is preserved with `FormerlySerializedAs`.
Rejected Alternatives: Keeping a fallback procedural particle was rejected because it hides pool bootstrap failure as a runtime allocation. Pool expansion was rejected because the manager itself marks expansion forbidden by mandate.
Scalability potential: Low devices avoid heap fragmentation and Canvas churn; Middle/High/Ultra can use denser authored particle prefabs from the same prewarmed pool and atlas.
Hardware Impact: Estimated 250 us saved per previous missing-prefab fallback on low-end silicon; prevents allocation spikes rather than improving steady arithmetic.

Problem: UI particle materials could accidentally clone renderer materials or force extra canvas batches.
Solution: Added a shared atlas material field. Enforcement uses `ParticleSystemRenderer.sharedMaterial` only; CanvasRenderer-only prefabs fail closed because runtime canvas material binding would undermine the no-rebuild guarantee. No `.material` access exists.
Rejected Alternatives: Assigning `renderer.material` was rejected because it clones materials and breaks batching. Runtime Canvas material/texture assignment or hierarchy parenting was rejected as Canvas churn.
Scalability potential: Low uses one shared atlas; Middle/High/Ultra can increase particle counts while staying atlas-bound.
Hardware Impact: Estimated 80 us saved per spawn that would otherwise clone/rebatch; exact GPU draw-call impact requires Unity profiler capture.

Problem: Batch demanded `AcousticStateDTO` at BufferID 72430 with 16-byte layout.
Solution: Blocked the task. Static search proves no `AcousticStateDTO` exists. `H8Memory.cs` assigns `72430` to `SpatialAudioVirtualVoiceTuning`; `VocalWarningSystem.cs` also hard-casts `72430` for alarm state. Live audio tuning struct is `VirtualVoiceTuningSnapshot`, explicit size 32 bytes.
Rejected Alternatives: Reassigning BufferID 72430 or adding a new struct under the same ID was rejected as data corruption. Adding a validator for a nonexistent type was rejected as fake proof.
Scalability potential: Preserving one buffer owner keeps all hardware tiers deterministic; no tier-specific data aliasing.
Hardware Impact: 0 us runtime; prevents undefined memory alias cost/crash.

Problem: Batch demanded managed `OnAudioFilterRead` purification and infrasound injection.
Solution: Blocked the task because `PlayerCriticalProceduralAudioRenderer.cs` has no `OnAudioFilterRead`. It registers a native bridge through `NativeAudioKernelRingBufferDescriptor` and `HectonSensoryKernelNativeBridge.TryRegisterWithRetryGate`.
Rejected Alternatives: Adding `OnAudioFilterRead` would reverse existing DSP-thread doctrine and reintroduce Unity managed callback risk.
Scalability potential: Native bridge route is better for all tiers; Low avoids callback overhead, Ultra can receive richer native kernel synthesis without managed allocations.
Hardware Impact: 0 us added; preserves existing native path.

Problem: Build verification was required but host rules forbid compiler launch under load or active compiler.
Solution: Sampled host state twice. CPU was 100%; dotnet process count was 1 then 2. Did not launch build. Ran static scans, JSON validation, and `git diff --check` instead.
Rejected Alternatives: Starting `dotnet build` under active load was rejected as a direct rule violation and would interfere with other agents.
Scalability potential: Verification policy only; no runtime effect.
Hardware Impact: 0 us runtime.

Problem: Nan/Infinity depth could poison mixer parameters.
Solution: `math.select(0f, listenerRuntimeY, math.isfinite(listenerRuntimeY))` converts non-finite depth to open-water Y before curve evaluation. Smoothed cutoff resets to 22000 Hz if it leaves `[400,22000]` or becomes non-finite.
Rejected Alternatives: Letting `math.lerp` consume non-finite input was rejected because it can propagate invalid mixer state.
Scalability potential: All tiers receive fail-open audio instead of speaker-static failure.
Hardware Impact: Fault guard cost is scalar math only; estimated below 1 us per presentation tick on i3/MX350.

Problem: Prior proof artifact path used JSON despite the current directive banning JSON reports.
Solution: Removed the JSON proof artifact and kept evidence in C# source diff, status, rationale, and log only.
Rejected Alternatives: Keeping JSON as parallel proof was rejected because it adds I/O and is not accepted by the current batch directive.
Scalability potential: No runtime impact across Low/Middle/High/Ultra.
Hardware Impact: 0 us runtime.

Problem: `SpatialAudioManager` still classified playback routes by reading `AudioClip.name` on cache miss.
Solution: Removed the token classifier and cache zero route flags by stable clip entity id. Explicit mixer groups, dedicated threat entry points, and hashed/procedural event routes remain authority.
Rejected Alternatives: Adding a new route table/global surface was rejected because it would be a dependency expansion without a route card. Keeping string-name heuristics was rejected as hidden native-string bridge risk and non-authoritative content coupling.
Scalability potential: Low/Middle/High/Ultra all avoid first-play name parsing. Future richer route metadata must arrive through an explicit authored data contract, not clip-name convention.
Hardware Impact: Estimated 2-5 us avoided per uncached clip route on i3/MX350; removes first-use string bridge risk rather than changing steady-state mix math.

Problem: The new master depth LPF path would keep attempting `AudioMixer.SetFloat` if the exposed parameter name was configured but missing from the resolved mixer.
Solution: Check the `SetFloat` return value and fail closed by disabling the master LPF availability flag until the next cold refresh/validation.
Rejected Alternatives: Logging every failed write was rejected as runtime noise and string allocation risk. Changing all legacy mixer parameters was rejected as unrelated blast radius.
Scalability potential: Low/Middle/High/Ultra avoid repeated failed mixer parameter writes; authored mixer wiring still controls the effect.
Hardware Impact: Estimated 1-3 us avoided per presentation frame when the exposed parameter is missing.

Problem: `HectonMusicDirector` editor debug state still read `AudioClip.name` for override and stinger clips.
Solution: Replace those reads with stable interned debug labels while preserving authored `CueId` for normal music voices.
Rejected Alternatives: Hashing clip names was rejected because it still reads the native string. Adding new debug metadata tables was rejected as overengineering for editor-only state.
Scalability potential: Low/Middle/High/Ultra unaffected in gameplay; editor instrumentation no longer depends on native clip names.
Hardware Impact: Runtime player cost is 0 us under `UNITY_EDITOR` guard; editor debug path avoids native string bridge.

Problem: `UIParticleEffect.Play` still fetched the pooled instance `Transform` through `GameObject.transform` per burst after the pool refactor.
Solution: Cache the instance `Transform` immediately after pool spawn and clear it on despawn; hot `Play` writes position through the cached reference.
Rejected Alternatives: Re-resolving through `GetComponent` or keeping the property bridge was rejected as avoidable hot-path work. Adding a wrapper component was rejected as assembly churn.
Scalability potential: Low-tier avoids extra native bridge calls during frequent UI bursts; Middle/High/Ultra can spend the saved budget on denser authored pooled particles through the existing quality scalar.
Hardware Impact: Estimated below 1 us per UI burst on i3/MX350, but removes a repeated native property bridge from the steady presentation path.

Problem: `UIAudioFeedback` created `EventTrigger` components and `EventTrigger.Entry` objects while registering UI controls at runtime.
Solution: Remove runtime creation. Hover audio now binds only when the button already has an authored `EventTrigger` with a `PointerEnter` entry; click, slider, and toggle audio registration is unchanged.
Rejected Alternatives: Adding a new hover relay component was rejected as new topology. Keeping auto-created triggers was rejected as runtime UI component allocation.
Scalability potential: Low-tier avoids UI bootstrap heap churn; Middle/High/Ultra keep hover richness through authored prefabs without runtime structure mutation.
Hardware Impact: Estimated 15-40 us avoided per button that previously lacked an EventTrigger, plus one managed object avoided per missing pointer-enter entry.

Problem: Full-depth master low-pass previously raised the final cutoff on low quality, which diluted the explicit `-300m -> 400Hz` acoustic requirement.
Solution: Make the target log-frequency endpoint fixed at `400Hz` for all tiers; keep `GlobalQualityWeight` only on smoothing sharpness.
Rejected Alternatives: Quality-dependent cutoff was rejected because hardware tier must not change authored sensory truth. Removing quality entirely was rejected because cadence/smoothing can still scale presentation cost without changing the final state.
Scalability potential: Low/Middle/High/Ultra now share the same abyssal endpoint; higher tiers reach it with sharper presentation response.
Hardware Impact: No extra runtime cost; removes two constants and one lerp from the master LPF path.

Problem: The new master depth-pressure low-pass could remain stuck on the mixer after service shutdown at depth.
Solution: Add `ResetMasterDepthPressureLowPass()` into `SpatialAudioManager.ShutdownServiceState`, forcing the exposed cutoff back to `22000Hz` beside the existing threat and parasite mixer resets. Restored temporary `OnDisable` to non-destructive runtime resource retention; destroy/editor reload still releases resources.
Rejected Alternatives: Leaving reset to the next bootstrap frame was rejected because the AudioMixer is global state. Releasing telemetry/runtime resources on every disable was rejected because a temporary component disable should not invalidate cold-owned buffers.
Scalability potential: Low/Middle/High/Ultra all fail open to clear audio after manager shutdown; no tier changes.
Hardware Impact: 0 us steady-state runtime; one cold mixer write only during shutdown.

Problem: Live audio synthesis callbacks needed a new verification pass after the shutdown patch.
Solution: Re-read `DynamicMusicGranularSynthesizer.OnAudioFilterRead` and `VocalBankPlaybackRuntime.OnAudioFilterRead`; neither allocates managed arrays or uses LINQ/string formatting in the callback bodies. The vocal decoder still performs decode work while holding its mutation guard.
Rejected Alternatives: Releasing the vocal guard before decode was rejected because the vault buffer pointer would not be provably pinned against compaction without an explicit contract change.
Scalability potential: Low/Middle/High/Ultra preserve the existing native-buffer callback path; no unsafe lock shortening.
Hardware Impact: 0 us changed; risk avoided is stale-pointer decode during compaction.

Problem: `SpatialAudioManager` still created a development-only RAM overlay with `new GameObject`, `AddComponent<Canvas>`, and `AddComponent<TextMeshProUGUI>` from runtime bootstrap before updating it from `LateFrameTick`.
Solution: Convert the overlay to an optional serialized pre-authored TMP label. The manager now allocates only the fixed char buffer when that label exists and updates it through `SetCharArray`.
Rejected Alternatives: Keeping dev-build object creation was rejected because development builds still need allocation discipline. Building a replacement overlay prefab from this script was rejected; authoring owns UI hierarchy.
Scalability potential: Low/Middle/High/Ultra unchanged in shipping; development/profiling builds stop masking audio allocation defects with debug UI construction.
Hardware Impact: Estimated one-time 200-500us and several managed/native objects avoided when development audio residency overlay is enabled without an authored label.

Problem: `UIAudioFeedback` still inferred button sound roles from `button.name` using case-insensitive token scans during hierarchy registration.
Solution: Replace name-token inference with serialized authored `Button[]` reference arrays for primary and destructive cues. Unlisted buttons use the secondary cue.
Rejected Alternatives: Keeping string fallback was rejected because content names are not an audio route contract. Adding a new marker component was rejected as new UI topology for a routing problem this manager can own.
Scalability potential: Low/Middle/High/Ultra all avoid native object-name bridging during UI registration; richer authored menus can still map exact high-value buttons without runtime string parsing.
Hardware Impact: Estimated below 1us per registered button on i3/MX350, but removes a brittle native string bridge and token scan from repeated UI open/enable paths.

Problem: A mis-authored UI particle prefab containing a root `UIParticleEffect` would be invoked by `ObjectPoolManager.NotifySpawn`, risking nested pool lifecycle recursion.
Solution: Reject such authored prefabs before spawning through a cold `TryGetComponent` root check, and keep a post-spawn cached-component guard as a fail-closed backstop.
Rejected Alternatives: Letting the pool callback run and cleaning afterward was rejected because `NotifySpawn` fires before the caller receives the instance. Adding a new prefab validator component was rejected as topology churn.
Scalability potential: Low/Middle/High/Ultra all avoid accidental recursive UI particle controller graphs; high-tier particle richness remains in the authored particle prefab, not in nested controllers.
Hardware Impact: 0us steady-state `Play`; one cold prefab root component check during enable/spawn.

Problem: `BeaconHUDElement` still built its 16 HUD icon slots with `Instantiate` and added missing `CanvasGroup` components during runtime bootstrap.
Solution: Allocate only slot metadata/char buffers cold; bind visible icon roots from a prewarmed `ObjectPoolManager` pool, require root `CanvasGroup`, cache the owning pool per slot, and despawn on disable or object-pool hot-swap. Retry uses cached pool references only; no registry fallback is read from `LateFrameTick`.
Rejected Alternatives: Keeping cold `Instantiate` was rejected because HUD enable is runtime UI structure mutation. Calling `Warmup` from this component was rejected because it hides missing scene warmup behind allocation. Adding `CanvasGroup` fallback was rejected because prefab authoring owns UI hierarchy.
Scalability potential: Low avoids heap/UI hierarchy construction; Middle/High/Ultra can raise icon styling, animation, and shader richness inside the same prewarmed prefab/pool.
Hardware Impact: Estimated 0 steady-state GC and about 16 prefab instantiations plus up to 16 component adds removed from HUD bootstrap on i3/MX350.

Problem: `HectonMusicDirector` could hide missing scene prewarm by warming the runtime director pool and by falling back to `ObjectPoolManager.ActiveRuntimeInstance`.
Solution: Resolve only `GlobalRegistry.ObjectPoolService`, require a registered pool and a positive available count, then spawn the configured runtime director with expansion disabled.
Rejected Alternatives: Runtime `Warmup` was rejected because scene bootstrap must own pool capacity. Direct `ObjectPoolManager.ActiveRuntimeInstance` fallback was rejected because it bypasses the cold DI route.
Scalability potential: Low/Middle/High/Ultra all share the same authored prewarm contract; richer music director behavior must come from configured pool capacity, not runtime self-repair.
Hardware Impact: Removes one possible cold allocation/registry bypass path during music director bootstrap; steady-state impact is 0 us.

Problem: `AtmosphericAudioRuntimeInstaller` added missing atmospheric/audio components at runtime, including the critical procedural audio renderer.
Solution: Treat these systems as authored player/listener contracts. Missing components now fail closed with development warnings; renderer binding only occurs if the active listener already owns `PlayerCriticalProceduralAudioRenderer`.
Rejected Alternatives: `AddComponent` fallback was rejected because component fabrication during runtime masks scene authoring failures and can trigger managed/native allocation spikes. Creating placeholder audio components was rejected because it would form a parallel audio authority.
Scalability potential: Low avoids runtime component construction; Middle/High/Ultra can still bind richer authored atmospheric components with no installer topology change.
Hardware Impact: Removes up to four runtime component constructions on the player/listener bootstrap path; steady-state impact remains 0 us.

Problem: The status claimed `BeaconHUDElement` was pool-only, but a fresh source read showed `AllocateDisplaySlotsCold()` still called `Instantiate` and repaired missing `CanvasGroup` through `AddComponent`.
Solution: Move icon instances behind `IObjectPoolService`. `Awake` allocates only fixed slot metadata and text buffers; `OnEnable` binds prewarmed icon instances if the pool has enough capacity; `OnDisable` and object-pool hot-swap return owned instances. The icon prefab must already provide root `CanvasGroup`.
Rejected Alternatives: Retaining `Instantiate` as "cold" was rejected because HUD enable can occur after bootstrap and still mutates UI hierarchy. Runtime `Warmup` was rejected because pool capacity belongs to bootstrap/scene authoring. Adding missing `CanvasGroup` was rejected because prefab authoring owns presentation structure.
Scalability potential: Low avoids runtime hierarchy construction; Middle/High/Ultra can improve the pooled icon prefab visuals without changing code or allocation behavior.
Hardware Impact: Removes 16 possible runtime prefab instantiations and up to 16 component additions from beacon HUD enable on i3/MX350; steady-state remains 0 B/frame.

Problem: `UIParticleEffect` still called `ObjectPoolManager.Spawn` before proving that a registered pool had an available instance, and a child `UIParticleEffect` inside the authored prefab could receive `OnEnable` during `SetActive(true)` before the post-spawn guard ran.
Solution: Add a pre-spawn `HasPool`/`GetAvailableCount` gate, clear partial stale instances before rebinding, and scan the authored prefab subtree with a pre-capacity static `List<UIParticleEffect>` before calling `Spawn`.
Rejected Alternatives: Letting `Spawn` emit pool-exhaust telemetry was rejected because missing prewarm should fail closed without side effects. Root-only prefab validation was rejected because Unity activates children during `SetActive(true)`.
Scalability potential: Low/Middle/High/Ultra all use the same prewarmed particle contract; higher tiers only increase authored count through `GlobalQualityWeight`, never through runtime expansion.
Hardware Impact: Removes one possible pool-exhaust telemetry path and recursive effect graph from UI window enable; steady-state `Play` remains 0 B/frame.

Problem: The UI particle prefab validation still depended on a static `List<UIParticleEffect>` that could grow if authoring exceeded the initial capacity.
Solution: Replace it with a fixed `Transform[64]` DFS stack and clear retained references on every return path. Overflow fails closed before pool spawn.
Rejected Alternatives: Increasing List capacity was rejected because it preserves a hidden growth path. `GetComponentsInChildren` was rejected because it can allocate arrays.
Scalability potential: Low/Middle/High/Ultra all share the same no-growth validation path; richer particle prefabs must stay inside the fixed validation budget or be split by authoring.
Hardware Impact: Removes one cold managed list growth risk from UI effect enable; steady-state `Play` remains 0 B/frame.

Problem: `SpatialAudioManager` cave acoustics used a `List<HectonVoxelVolume>` scratch and resolved `WorldCaveDirector.ActiveRuntimeInstance` from the presentation refresh when the cache was empty.
Solution: Replace the cave scratch with fixed `HectonVoxelVolume[32]`, consume `WorldCaveDirector.CopyActiveVolumesTo`, and bind the director through cold `ActiveRuntimeInstanceChanged` subscription.
Rejected Alternatives: Retaining `CollectActiveVolumes(List<T>)` was rejected because `Add` still has a growth cliff if cave count exceeds the old capacity. Polling the static runtime owner from the presentation path was rejected because dependency identity belongs to cold lifecycle.
Scalability potential: Low caps cave acoustic candidates at a fixed no-growth budget; Middle/High/Ultra can raise the compile-time capacity with explicit proof, while existing Sabine/interior quality still scales through presentation parameters.
Hardware Impact: Removes one List growth risk and one presentation-path static owner poll from cave acoustic refresh on compact hardware; steady-state heap remains 0 B/frame.

Problem: Acoustic translator, terminal boot, and caption overlays still built UI roots/children through `new GameObject`, `AddComponent`, and destructive child clearing.
Solution: `AcousticEcholocationTranslator.cs` now binds existing overlay roots, CanvasGroups, Images, TMP labels, rules, and eight caption slots. Missing authoring fails closed before registering `_uiBuilt`.
Rejected Alternatives: Runtime prefab repair and child rebuilding were rejected because they mutate Canvas hierarchy during gameplay and hide missing scene authoring. Object-pooling these fixed HUD subtrees was rejected because they are stable overlay structure, not burst instances.
Scalability potential: Low avoids Canvas rebuild/GC spikes; Middle/High/Ultra can ship richer authored overlay art with the same binding contract.
Hardware Impact: Removes runtime construction of three overlay roots plus child TMP/Image/CanvasGroup repair from sonar UI activation; steady-state remains 0 B/frame.

Problem: `SonarHoloCompass` self-created its root, frame, and 16 dot markers, then repaired `CanvasGroup`/`Image` components.
Solution: The compass now requires a pre-authored `SonarHoloCompass` root with `RingOuter`, `RuleH`, `RuleV`, and 16 `Dot` children with `Image` components. Binding arrays are allocated once and reused; no hierarchy mutation remains.
Rejected Alternatives: Keeping the generated compass was rejected because acoustic HUD structure belongs to authored Canvas. Pooling the compass dots was rejected because the dot count is fixed and already Math-LOD gated by `GlobalQualityWeight`.
Scalability potential: Low renders 4 active dots via existing quality scalar; Middle/High/Ultra can expose all 16 authored dots and richer materials without changing the code path.
Hardware Impact: Removes one root allocation, three frame child allocations, 16 dot allocations, and component repair from compass bootstrap; steady-state hot scanner remains clean.

Problem: `ShaderCompassRibbon` still created its root, repaired missing `CanvasGroup`/`Image`, and allocated a runtime `Material` from a shader.
Solution: Convert the ribbon to an authored-only UI element. It now binds a serialized or named `ShaderCompassRibbon` root, requires existing `CanvasGroup` and `Image`, accepts an authored shared material, and writes the offset through `Shader.SetGlobalFloat`.
Rejected Alternatives: Runtime material construction was rejected because compass material identity must be authored and batchable. Setting per-instance material properties was rejected because it would require a clone or mutate a shared asset.
Scalability potential: Low avoids Canvas hierarchy/material allocation; Middle/High/Ultra can use richer authored ribbon shaders/materials without runtime material churn.
Hardware Impact: Removes one root allocation, two component repair paths, and one runtime material allocation from compass bootstrap; steady-state remains a scalar global shader write with deadband.

Problem: `HectonMusicDirector.ResolveDependencies()` still used static runtime fallbacks for world zone, biome matrix, and DirectorAI when slow/presentation evaluation found missing references.
Solution: Move those routes to existing cold owners: `WorldZoneDirector.ActiveRuntimeInstanceChanged`, `GlobalRegistry.BiomeMatrix`, and `GlobalRegistry.EncounterDirector`. Explicit inspector references remain authoritative and are not overwritten by runtime services.
Rejected Alternatives: Adding new events to `BiomeMatrixDirector` or `HectonDirectorAI` was rejected as cross-domain contract expansion. Keeping static fallbacks inside slow dependency refresh was rejected because cold registry/event routes already exist.
Scalability potential: Low/Middle/High/Ultra all read stable cached music context; richer music layering still comes from existing profiles and tension weights, not from hot dependency repair.
Hardware Impact: Removes three conditional static runtime fallback probes from the music context refresh path; expected gain is sub-microsecond per slow refresh, but dependency ownership is cleaner and deterministic.

Problem: Relay and AR waypoint HUD paths still fabricated UI structure at runtime: relay marker fail-safe built marker layers/Images/TMP labels, while `ARWaypointOverlay` created and destroyed 16 marker slots and repaired TMP/sharpness components.
Solution: Convert both paths to authored-only binding. Relay bootstrap now validates and warns in development builds without Canvas mutation. `ARWaypointOverlay` binds an authored root plus `Waypoint_0..15` slot children with existing `CanvasGroup`, `Fill`, `Outline`, and `Label` components, preserves authored sprites, and fails closed on incomplete authoring.
Rejected Alternatives: Keeping development-only fabrication was rejected because dev builds must expose missing authoring instead of hiding allocation spikes. Pooling fixed waypoint slots was rejected because the marker set is stable HUD structure, not a burst effect. Adding missing TMP/sharpness components was rejected because component topology belongs to the prefab.
Scalability potential: Low devices avoid runtime Canvas hierarchy construction and child destruction; Middle/High/Ultra can use richer authored marker sprites/materials and labels without changing code or adding allocation paths.
Hardware Impact: Removes one relay marker layer, one relay marker subtree, 16 waypoint roots, 32 waypoint Images, 16 TMP labels, 16 sharpness component repairs, and child-destroy churn from HUD bootstrap paths; steady-state remains `0B/frame`.

Problem: `BeaconHUDElement.cs` still contained the original prefab-instantiation HUD path in the actual source, and the serialized prefab field falsely advertised a runtime icon route.
Solution: Remove `beaconIconPrefab`, allocate only fixed `BeaconIconDisplay` metadata/char buffers, and bind each slot to pre-authored `iconContainer` children with an existing root `CanvasGroup`. `BeaconRegistry.GetAllBeacons()` was verified as a fixed static array route.
Rejected Alternatives: Keeping `Instantiate` as cold setup was rejected because HUD enable can occur in gameplay and still mutates Canvas hierarchy. Keeping `AddComponent<CanvasGroup>` was rejected because prefab hierarchy is an authoring contract. Keeping the dead prefab field was rejected because it invites a route the code no longer supports.
Scalability potential: Low avoids HUD hierarchy spikes entirely; Middle/High/Ultra can increase visual richness through authored icon children, shaders, and animation without changing code or enabling runtime allocation.
Hardware Impact: Removes up to 16 prefab instantiations and 16 component additions from beacon HUD bootstrap/reenable on i3/MX350; steady-state HUD scan remains free of registry polling, component lookup, LINQ, string formatting, and runtime fabrication.

Problem: `BuilderStatusOverlay` built its panel, header rule, and all TMP labels at runtime, then registered UI ticking even if the generated hierarchy was the only valid route.
Solution: Convert the overlay to authored binding. The component now serializes/binds its root, CanvasGroup, panel Image, header rule, and fixed text children, and refuses to tick when that authored contract is incomplete.
Rejected Alternatives: Keeping runtime fallback construction was rejected because builder HUD can enable during gameplay and mutate Canvas hierarchy. Pooling this panel was rejected because it is stable overlay structure, not a burst visual.
Scalability potential: Low avoids Canvas construction spikes; Middle/High/Ultra can use richer authored panel materials, layout, and animation without changing the code path.
Hardware Impact: Removes one root, one CanvasGroup repair, one Image repair, one header Image allocation, and eight TMP label allocations from builder HUD activation; steady-state remains char-buffer/TMP `SetCharArray`.

Problem: `FontStreamingManager` created a `FontStreamingStatus` root and `StatusLabel` during runtime font swap bootstrap, and repaired missing CanvasGroup/Image/TMP components.
Solution: Make the status overlay optional authored UI. Font swapping now continues without visual status authoring; when the overlay exists, the manager binds existing CanvasGroup/Image/TMP only and updates it with the existing fixed char buffer.
Rejected Alternatives: Keeping development/runtime UI construction was rejected because language switching can occur after gameplay starts. Blocking the font swap when the status panel is missing was rejected because the panel is presentation, not font ownership.
Scalability potential: Low keeps language changes allocation-flat even without the status panel; Middle/High/Ultra can ship richer authored status art while the scheduler cost remains staged and deterministic.
Hardware Impact: Removes one status root allocation, two component repair paths, and one TMP label allocation from font-streaming bootstrap; steady-state remains the existing staged label scheduler.

Problem: `HectonSubmarineOsDisplay` still fabricated the submarine OS panel at runtime, including the root, panel Image, TMP status/log/metric labels, subsystem icon slots, and engine heat bar.
Solution: Convert the display to authored-only binding. The component now resolves an existing `HectonSubmarineOsDisplay` root, binds existing Canvas/Image/TMP/RectTransform references, gates dispatcher registration on complete bindings, and keeps all steady updates on fixed char buffers plus cached UI references.
Rejected Alternatives: Keeping a runtime fallback panel was rejected because the submarine OS can enable during gameplay and mutate Canvas hierarchy. Pooling this panel was rejected because it is persistent cockpit HUD structure, not a burst instance. Adding missing UI components was rejected because prefab topology is an authoring contract.
Scalability potential: Low avoids cockpit HUD hierarchy spikes and still receives essential text/status output; Middle/High/Ultra can use richer authored icon art, panel materials, and heat-bar shaders without changing code or adding allocation paths.
Hardware Impact: Removes one HUD root construction path, multiple Image/TMP component constructions, four subsystem icon slot builds, and one engine heat-bar build from submarine OS activation on i3/MX350-class hardware; steady-state remains fixed buffer writes and cached `Image.fillAmount`.

Problem: `HectonOSBootManager` still generated its boot overlay root, background, CanvasGroup, console TMP label, and text registry component at runtime, then destroyed existing children before rebuilding the panel.
Solution: Convert boot overlay setup to authored-only binding. The manager now binds a pre-authored `HectonOSBootManagerOverlay` root, existing CanvasGroup/Image, and a `ConsoleText` TMP label carrying `HectonTextNode`; tick registration is gated by `_uiBuilt`.
Rejected Alternatives: Keeping runtime overlay construction was rejected because boot/recovery UI can trigger during gameplay and mutate Canvas hierarchy. Calling `TMP_TextRegistry.EnsureRegistered` from binding was rejected because it can repair components through AddComponent. Clearing child UI was rejected as destructive scene mutation.
Scalability potential: Low avoids boot overlay hierarchy spikes and keeps the same fixed char-buffer sequence output; Middle/High/Ultra can use richer authored background materials, typography, and animation without changing code or adding allocation paths.
Hardware Impact: Removes one boot overlay root construction path, one CanvasGroup repair, one Image repair, one TMP label construction, one HectonTextNode repair path, and child-destroy churn from boot/recovery activation; steady-state remains fixed char-buffer text reveal and cached CanvasGroup alpha writes.

Problem: `BIOSMessageStreamer.EnsureRuntimeInstalled(GameObject)` still repaired missing authoring by adding `BIOSMessageStreamer` to a host at runtime.
Solution: Preserve the public method signature but remove component fabrication. The method now only probes authored presence, keeping compatibility while enforcing prefab/scene ownership.
Rejected Alternatives: Removing the method was rejected as unnecessary public API churn. Keeping `AddComponent` was rejected because cockpit BIOS terminal ownership must be authored, not repaired during runtime.
Scalability potential: Low avoids a hidden MonoBehaviour construction path; Middle/High/Ultra can use richer authored BIOS terminal visuals while the text stream remains fixed-buffer and cached-reference only.
Hardware Impact: Removes one possible runtime component construction and its managed/native allocation path. Steady-state remains unchanged: fixed char buffers, cached TMP label, and `LateFrameTick` only while pending text exists.

Problem: `TMP_TextRegistry.EnsureRegistered(TMP_Text)` was a central runtime repair path that added missing `HectonTextNode` components.
Solution: Make registry membership authored-only. `EnsureRegistered` now accepts only existing `HectonTextNode` components and can re-register an active authored node after registry reset; `SetMetadata` fails closed without authored ownership.
Rejected Alternatives: Patching only each caller was rejected because it would leave the central repair vector intact. Removing `EnsureRegistered` was rejected as unnecessary public API churn while callers still exist.
Scalability potential: Low avoids hidden TMP registry component construction across all UI surfaces; Middle/High/Ultra can keep richer authored text surfaces with the same registry path.
Hardware Impact: Removes one global `AddComponent<HectonTextNode>` allocation route. The fixed `Dictionary<int,int>(2048)` remains a cold preallocated owner map, not a hot allocation.

Problem: Localization helper entry points still repaired missing UI ownership by adding `LocalizedTMPAutoSizer` and `LocalizedLayoutMirror` components from static configuration methods.
Solution: Keep the public APIs but remove MonoBehaviour fabrication. `LocalizedTMPAutoSizer.Configure` now applies direct one-shot TMP sizing, wrapping, overflow, and current RTL state when no authored component exists. `LocalizedLayoutMirror.ConfigureRuntime` now fails closed unless the owner already carries an authored mirror component. Localization service miss retries are throttled to one lookup per dispatcher frame.
Rejected Alternatives: Adding components from the helper methods was rejected because these APIs are called by many UI builders and hide allocation under configuration. Removing the public methods was rejected as unnecessary call-site churn. Direct one-shot layout mirroring without captured defaults was rejected because repeated RTL toggles can drift alignment/pivot state.
Scalability potential: Low avoids hidden component construction across menu/PDA/subtitle UI paths; Middle/High/Ultra can use richer authored auto-sizer and mirror components where ongoing language reaction is required.
Hardware Impact: Removes two central runtime component construction vectors. Direct text fallback remains scalar TMP field assignment only; authored components continue to update in `LateFrameTick` under the existing per-frame apply budget.
