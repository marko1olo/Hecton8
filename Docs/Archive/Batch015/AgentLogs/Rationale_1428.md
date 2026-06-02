# Rationale 1428 - Hybrid Compile And Unity Integrator

## Initial Boundary Decision

Problem: The task demands compile and Unity import repair while the repository contains large pre-existing edits from other agents.
Solution: Own only compile-medic automation, static audit artifacts, and surgical source fixes proven by compiler/log diagnostics.
Rejected Alternatives: Broad cleanup or formatting would trample concurrent agent work and create untraceable regressions.
Scalability potential: Low uses static scans and single-worker builds; Middle/High/Ultra can spend extra idle CPU on deeper Unity log forensics and editor validation, but only when host load is below throttle.
Hardware Impact: On low-end development laptops, CPU guard prevents build storms; expected saved time is workstation stability, not frame-time runtime gain.

## Mandate Selection

Problem: Compile repair touches global authority, phase ordering, native memory safety, and domain reload behavior.
Solution: Loaded execution phases, registry DI, zero-GC, native jobs, debug telemetry, global state reset, and performance budget mandates before code edits.
Rejected Alternatives: Reading only compile logs would miss architectural regressions that compile cleanly but violate runtime contracts.
Scalability potential: Low-tier proof relies on cheap static AST scans; higher-tier proof can add Unity Editor and PlayMode validation.
Hardware Impact: Static-first validation avoids unnecessary compiler churn on low-end silicon.

## Unity Quality Envelope Pass

Problem: Unity `QualitySettings` had too few meaningful device-class rows, and runtime `GlobalQualityWeight` was being asked to compensate after startup instead of inheriting a sane cold render envelope.
Solution: Added cold device-class rows for handheld UMA, compact PC, and ultra PC while preserving the existing Quest row index. `GameBootstrapper.ApplyScalabilityMatrix` now selects the Unity quality row during hardware check, then leaves continuous runtime scaling to `HomeostasisBrain.GlobalQualityWeight`.
Rejected Alternatives: Naming a quality level after one development GPU; enabling Standalone XR globally; duplicating nearly identical URP assets for every class when texture/LOD/upload budgets and runtime DRS already provide the necessary separation.
Scalability potential: Low uses `Abyss (Low)` plus runtime pressure cuts. Compact PC and handheld use conservative Medium-pipeline envelopes. Mid uses `Surface (Medium)`. High uses `Orbit (High)`. Ultra uses `Leviathan (Ultra)`. Quest stays on the dedicated Android VR route.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Startup now avoids overcommitting texture residency, terrain range, and upload budgets before the continuous governor has enough frame/thermal evidence.

## Generic Shared-Memory Constraint Pass

Problem: `HomeostasisBrain.ResolveKnownHardwareConstraint01` still contained a single GPU-name throttle, which made the runtime quality dictator a development-machine heuristic instead of a portable device-class policy.
Solution: Replaced the GPU string route with `HardwareTierDetector.EnsureInitialized()`, `SharedMemoryModeActive`, and `RecommendedVramBudgetMegabytes`. Very constrained shared-memory devices clamp harder; roomier shared-memory devices inherit the same moderate pressure path as Quest-class/handheld cases.
Rejected Alternatives: Adding more GPU string checks; hardcoding laptop classes in `HomeostasisBrain`; making Unity quality rows decide gameplay/simulation truth. `GlobalQualityWeight` remains continuous and owner-side.
Scalability potential: Low/handheld UMA starts with aggressive pressure before frame-time evidence accumulates. Middle shared-memory hardware keeps a moderate cap. High/Ultra discrete hardware escapes this constraint unless memory pressure proves otherwise.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Removed one brittle hardware string branch from startup policy and made the startup clamp follow measured memory budget instead of one vendor/device name.

## Standalone Default Quality Lock

Problem: `QualitySettings` had Android and Nintendo Switch 2 per-platform defaults, but no explicit Standalone default. That lets player builds inherit editor-current quality state until bootstrap executes.
Solution: Set `m_PerPlatformDefaultQuality.Standalone` to `0`, the `Surface (Medium)` cold envelope. Runtime bootstrap still calls `QualitySettings.SetQualityLevel` after hardware classification.
Rejected Alternatives: Defaulting Standalone to Low or Ultra; adding a separate PCVR row without a verified XR-loader activation route; changing Android min SDK during an open Unity compile wall.
Scalability potential: Low starts from a sane medium envelope and is immediately downgraded by bootstrap/device pressure. Middle keeps medium. High/Ultra are raised by bootstrap after measured hardware profile. PCVR remains an explicit OpenXR provider route, not a hidden default for flat PC dev.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Startup state is now deterministic for Standalone builds.

## Third-Party Archive Pass

Problem: Forbidden or unused third-party packages were physically present under `Assets`, increasing Unity import/compile surface and violating the AGENTS third-party boundary.
Solution: Archived only packages with AGENTS rejection or zero first-party GUID references, preserving relative paths under `C:\hades\_Hecton8_ThirdPartyArchive\1428_20260530_221722`.
Rejected Alternatives: Deleting packages permanently; moving allowed systems such as Crest/MapMagic/Feel/Odin; moving Candice after first-party data showed `useCandiceBehaviorTree: 1`.
Scalability potential: Low tier benefits from reduced editor/import and accidental runtime contamination; Middle/High/Ultra retain approved visual systems instead of removing art-tech capacity.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Editor/import surface reduced by 2172 archived files and 82.68 MB of forbidden or unused assets.

## Unity Launch And XR Settings Pass

Problem: First Unity launch after a long downtime showed XR packages present but the Android Quest path was only partially wired: OpenXR/Meta packages were installed, Foveated Rendering and Meta Quest Support were enabled, but Quest controller profiles and Meta display-refresh extension were disabled. Adaptive Performance config objects also pointed to an archived/dead provider surface.
Solution: Kept the XR package chain because `com.unity.xr.meta-openxr` legitimately pulls ARFoundation, Composition Layers, Core Utils, and XR legacy input helpers. Enabled only the Android Quest controller profiles and Meta Display Utilities feature in `Assets/XR/Settings/OpenXR Package Settings.asset`. Updated `XrPlatformReadinessValidator` so CI/menu wiring preserves Display Utilities with the rest of the Quest feature set. Removed dead Adaptive Performance config objects and disabled `m_UseAdaptivePerformance` in URP assets.
Rejected Alternatives: Removing XR transitive packages would break Quest readiness; enabling hand/eye/passthrough/AR features would add permission and runtime surface not currently used; manually fabricating `XRGeneralSettingsPerBuildTarget.asset` while Unity is in Safe Mode was rejected because the existing validator can create it once the C# compile wall is cleared.
Scalability potential: Low keeps Quest path lean: fixed foveation, controller input, and display-refresh control only. Middle keeps the same deterministic route with better render scale. High/Ultra can spend headroom on foveated overdraw relief, URP high renderer, and optional visual features without changing gameplay authority.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending device profiling. Import/config debt reduced by 13 additional archived files and dead Adaptive Performance settings. Quest safety improved by making controller/display/foveation feature checks explicit instead of silently depending on editor defaults.

## Runtime Profile Contract Cleanup

Problem: Active runtime contracts still carried one-device names in quality tiers, input scalability profiles, shader-strip environment evidence, and visual smoke tests. That makes the minimum proof lane look like the product identity and encourages binary platform branches.
Solution: Renamed active runtime-facing contracts to compact/shared-memory/discrete terminology while preserving serialized numeric values. `HectonQualityTier.CompactPc` keeps value `2`; `ScalabilityTierProfiles.LowCompact` keeps byte `0`; the shader stripper now reads only `HECTON_COMPACT_SHADER_STRIP`.
Rejected Alternatives: Keeping one-device aliases in active enums; rewriting all historical reports; changing save/DTO values. Historical text can stay historical, but active code and stable authority must not teach wrong policy.
Scalability potential: Low and compact lanes use the same minimum proof budget. Middle, high, and ultra stay additive visual envelopes. Handheld UMA and Quest remain shared-memory lanes. PCVR stays explicit, not a flat-Standalone default.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Engineering impact is removal of brittle one-device policy names without changing ABI values.

## Discrete VRAM Budget Scaling

Problem: Multiple live paths still treated the compact 1.8 GB ceiling as the universal runtime cap, which would make stronger discrete machines fail dev smoke or shed residency too early.
Solution: Routed hard VRAM ceilings through `HardwareTierDetector.RecommendedVramBudgetMegabytes/Bytes` and `VRAMBudgetThresholds.RuntimeDefault`. Compact fallback remains 1.8 GB; shared-memory devices clamp by RAM/graphics-memory class; discrete devices step through compact, mid, high, and ultra budgets.
Rejected Alternatives: A binary low/ultra switch; fixed 1.8 GB ceiling for all desktop PCs; direct device-name string checks.
Scalability potential: Low and compact use survival texture/RT limits. Middle gets expanded texture residency without changing gameplay truth. High and ultra can spend the additional budget on shadows, post, and visual density after runtime pressure allows it.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Expected gain is fewer false-positive memory sheds and fewer high-tier debug smoke failures on machines with real headroom.

## URP High Envelope Repair Guard

Problem: `HectonRenderPipelineValidator` repaired every authored URP asset with a single compact shadow distance and cascade count. Any future automatic repair pass would collapse high/ultra render envelopes back to compact settings.
Solution: Replaced the single global clamp with per-asset shadow budgets: compact assets remain conservative, Quest keeps its depth-only/opaque-off/MSAA2/18 m/1 cascade route, and the high URP asset keeps a 42 m / 4 cascade envelope. The high asset now uses 2048 shadow maps and a higher additional-light-per-object limit.
Rejected Alternatives: Disabling the validator; duplicating a new ultra URP asset while Unity is compiling; letting QualitySettings rows pretend to control URP shadow distance when the active URP asset owns that path.
Scalability potential: Low keeps stable compact shadows. Middle remains conservative. High/Ultra can present denser depth/noir silhouettes without mutating gameplay or save authority.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Visual headroom was restored for high/ultra; compact cost remains unchanged.

## Authority Baseline Generalization

Problem: Stable authority documents still described one concrete GPU as the project target, contradicting the current portability doctrine and the continuous `GlobalQualityWeight` route.
Solution: Updated only the active authority lines to define compact 2GB/8GB-class hardware as the minimum proof lane, not the product identity. Platform readiness remains PENDING until fresh Unity/player/profiler evidence exists.
Rejected Alternatives: Editing every mandate mention in bulk; removing the minimum proof lane; claiming platform readiness from serialized settings.
Scalability potential: Low, compact, handheld UMA, mid, high, ultra, PCVR, and Quest can now be discussed as lanes under the same product instead of forks.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. The value is preventing future policy drift.

## Unity Compile-Blocker Surgical Pass

Problem: Unity diagnostics and read-only agent inspection showed Burst BC1025 from generic managed type resolution inside the job-facing `SignalBus<T>.TryEnqueueBounded`, a `SystemInfo.supportsSetConstantBuffer` access during ScriptableObject initialization, and stale explicit-layout validators after concurrent DTO edits.
Solution: Removed managed policy/finite-guard calls from the Burst writer enqueue path and kept sanitization in managed owner/flush lanes; moved Noir constant-buffer capability to a cold instance field assigned in `CachePlatformCapabilitiesCold`; updated only stale validator offsets for visor refraction, AI cognition, dispatcher fence, biolum pulse, physics culling, and voxel modified cells.
Rejected Alternatives: Patching Unity package cache first; removing SignalBus guards globally; renaming DTO padding fields to satisfy stale specs; launching external `dotnet build` while Unity-owned compiler/import workers are active.
Scalability potential: Low and shared-memory devices keep bounded MPSC backpressure without Burst managed-type faults. Middle/High/Ultra retain managed validation before signal dispatch and expanded visual telemetry without changing DTO size.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Compile/import stability improves by eliminating a Burst-incompatible managed dependency in hot job writer code and a render-pipeline initialization exception source.

## PlayMode Startup Guard Patch

Problem: PlayMode diagnostics exposed a Unity API call from a MonoBehaviour static/field initialization path and a thermal service attempting dispatcher registration before the dispatcher existed. Console also retained graphics buffer leak warnings where object destruction could bypass OnDisable release.
Solution: Moved UI layer lookup behind a cold lazy cache, gated thermal frame/frost registration on the cached dispatcher dependency, and made GlobalShaderDispatcher release buffers idempotently in OnDestroy as well as OnDisable.
Rejected Alternatives: Leaving bootstrap to tolerate console errors; performing scene YAML rewrites; adding new global routes. The fix stays local to lifecycle and cold DI ordering.
Scalability potential: Low avoids startup exception stalls; Middle/High/Ultra keep the same continuous render/thermal policy without binary platform branches.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending PlayMode profiler proof. Startup stability improved by removing a fatal editor/runtime initialization violation.

## PlayMode Bootstrap Ordering Patch

Problem: Current PlayMode showed bootstrap services treating missing cold dependencies as fatal before the owner phase registered DataVault or Dispatcher; ocean validation rejected the real Crest bridge assembly; vocal synthesis compiled a Burst function pointer that Unity did not recognize as an entry point.
Solution: Deferred crash telemetry and pager initialization until DataVault is present, gated dispatcher registration until Dispatcher is present, allowed `Hecton8.Crest.Bridge` as the concrete `IOceanKinematics` provider assembly, made foveated unregister owner-checked, and replaced the vocal callback function pointer with a direct native-pointer kernel call while keeping the Burst job path intact.
Rejected Alternatives: Raw scene edits, disabling console validation, or creating fake ocean provider wrappers. The existing Crest adapter remains the provider; bootstrap validation now matches the actual assembly split.
Scalability potential: Low avoids boot stalls; Middle/High/Ultra preserve the same ocean, save, and audio systems without binary quality forks.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending PlayMode profiler proof. Startup correctness improves by removing premature fatal dependency checks and one Burst runtime exception.

## Domain Reload Native Leak Patch

Problem: Unity reload diagnostics showed Persistent allocations surviving across domain reload: `BiolumPulseSyncRuntime.BlackBoxDumpSnapshotOwner.Allocate` and `GlobalShaderDispatcher.EnsureGpuBuffers`.
Solution: Added editor-only `AssemblyReloadEvents.beforeAssemblyReload` release hooks for the active biolum runtime and shader dispatcher. Existing runtime disposal paths stay unchanged; the new hooks only close native allocations before Unity tears down the managed domain.
Rejected Alternatives: Ignoring warnings as editor-only noise; moving allocations to TempJob; disabling the systems during PlayMode. These buffers are valid session-owned state and must be released deterministically.
Scalability potential: Low through Ultra get the same reload safety. No gameplay route, DTO layout, or continuous quality policy changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor stability improves by removing reload-time persistent allocation leaks that can poison repeated PlayMode tests.

## PlayMode Core Service Ordering Patch

Problem: PlayMode showed `MemorySentinelRuntime` calling dispatcher registration before `SystemDispatcher` existed, and `H8BinaryWorldPager` faulting on vault handles that were present but not owned by the pager after no-domain-reload PlayMode churn.
Solution: Made MemorySentinel defer dispatcher lane registration until the `Dispatcher` hot-swap event arrives. Added cold pager handle normalization that releases stale pager BufferID handles and reacquires them under `SystemID.SavePersistence`.
Rejected Alternatives: Suppressing the GlobalRegistry error; making dispatcher registration create a dispatcher; disabling pager IO. The owner phase must publish the dispatcher, and the pager must own only its own BufferIDs.
Scalability potential: Low through Ultra keep the same runtime systems. This changes startup ordering and stale-handle recovery only.
Hardware Impact: Runtime frame-time gain claimed: 0 us. PlayMode stability improves by removing one bootstrap ordering error and one no-domain-reload stale vault handle trap.

## PlayMode Ecosystem And Vault Capacity Patch

Problem: Fresh PlayMode still showed `NutrientDriftRuntime` calling dispatcher registration before `SystemDispatcher` existed, plus pager vault acquisition failing under a 512-entry mock buffer table during CoreServices bootstrap.
Solution: Registered NutrientDrift as a hot-swap listener before dispatcher availability and deferred dispatcher lanes until the Dispatcher service event. Raised mock/authored DataVault buffer-table defaults to a continuous 2048-8192 profile curve and made pager-specific stale owner release bounded across repeated generation bumps.
Rejected Alternatives: Creating a dispatcher from NutrientDrift; ignoring pager IO; disabling save bootstrap. Dispatcher ownership remains in bootstrap, and save paging must acquire its own BufferIDs rather than silently falling back.
Scalability potential: Low gets 2048 metadata slots, which is cheap compared to native arena payloads; middle/high/ultra scale metadata headroom up to 8192 without changing gameplay authority or DTO layouts.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Bootstrap stability improves by removing one premature ecosystem registration error and one undersized metadata-table failure path.

## PlayMode Scavenging Dispatcher Gate

Problem: After the ecosystem patch, `ScavengingLootOracleRuntime` became the next AfterSceneLoad owner trying to register dispatcher phases before bootstrap had published `SystemDispatcher`.
Solution: Kept its hot-swap listener cold and fail-open, but made dispatcher phase registration return until `GlobalRegistry.Dispatcher` exists; Dispatcher hot-swap now re-registers only when the replacement service is non-null.
Rejected Alternatives: Moving loot oracle bootstrap into GameBootstrapper or constructing a dispatcher from scavenging code. Loot resolution remains decoupled and waits for the owner-published dispatcher.
Scalability potential: Low through Ultra keep the same scavenging job path; only startup ordering changes.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by eliminating one more premature dispatcher dependency violation.

## Menu Validator Warning Patch

Problem: Unity compiled cleanly by error count but emitted CS0162 in `MenuVisualVariantContractValidator15MM` because direct comparisons against public `const` catalog counts were compile-time false.
Solution: Cached the catalog counts into locals before validation, preserving the editor contract check while removing unreachable-code diagnostics.
Rejected Alternatives: Suppressing CS0162 globally or deleting the count validation. Both hide future menu catalog drift.
Scalability potential: Low through Ultra unchanged; this is editor signal hygiene so PlayMode validation is not masked by stale warnings.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor console noise reduced for the next PlayMode pass.

## PlayMode Fauna Dispatcher Gate

Problem: After entering PlayMode, `StressDrivenSpawnDirector` constructed from an AfterSceneLoad runtime hook and called `GlobalRegistry.TryRegisterColdTickable` before `SystemDispatcher` existed.
Solution: Registered the hot-swap listener first, deferred cold/late tick registration until the Dispatcher service event, and split dispatcher tick unregister from full lifecycle unregister.
Rejected Alternatives: Creating a dispatcher from fauna code or disabling the spawn director. Dispatcher ownership stays in bootstrap; fauna remains a consumer.
Scalability potential: Low through Ultra keep the same spawn jobs, telemetry, and AUP logic. Only cold startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. PlayMode stability improves by removing a premature AI/fauna dispatcher dependency violation.

## PlayMode Shadow Culling Dispatcher Gate

Problem: `AbyssalShadowCullingRuntime.OnEnable` registered simulation and visual-sync dispatcher systems during AfterSceneLoad before bootstrap had published `SystemDispatcher`.
Solution: Moved phase registration behind a dispatcher availability gate and retried through the Dispatcher hot-swap event; full disposal still unregisters the same systems.
Rejected Alternatives: Disabling abyssal shadow culling or moving the system into bootstrap. The render feature remains self-owned but waits for the dispatcher owner.
Scalability potential: Low skips expensive shadow culling until services exist; Middle/High/Ultra keep the same simulation/VISUAL_SYNC split after dispatcher publication.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing a premature graphics dispatcher registration error.

## Addressables Release Soft-Fail Patch

Problem: `PreWarmTierAddressableTextureGroupAsync` treated a successful direct `Addressables.Release` as failure when `AssetLifecycleGovernor` was not yet registered, making optional texture prewarm capable of failing `CoreServices`.
Solution: Direct release now returns true, and tier texture prewarm always continues bootstrap after logging optional download failure or timeout.
Rejected Alternatives: Blocking bootstrap on remote/local Addressables texture dependency readiness; disabling the whole Addressables prewarm path.
Scalability potential: Low through Ultra keep tier texture labels, but startup authority does not depend on optional presentation residency.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup avoids a false fatal path after an already completed release.

## World Pager Transient Vault Retry Patch

Problem: `H8BinaryWorldPager` converted transient DataVault absence, allocation fence, or unready pager BufferID handles during bootstrap into a permanent initialization fault.
Solution: Split transient abort from permanent native allocation fault. Transient abort releases streams, WAL streams, native buffers, and pager-owned handles without setting `_initializationFault`, allowing later `SaveManager.EnsureWorldPagerInitialized` retry after DataVault stabilizes.
Rejected Alternatives: Suppressing pager warnings while keeping permanent disabled state; allocating fallback managed buffers; forcing DataVault creation from the pager.
Scalability potential: Low through Ultra keep the same fixed native pager buffers and telemetry ring. The change affects owner-phase retry behavior only.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Bootstrap stability improves by removing one false permanent save-system shutdown during service ordering.

## PlayMode Material Response Dispatcher Gate

Problem: `ShinobuMaterialResponseRuntime.InstallRuntime` registered dispatcher phase systems and an environment cold tick from `AfterSceneLoad` before `SystemDispatcher` was published.
Solution: Added a dispatcher availability gate to material response phase registration and retried registration from the `Dispatcher` hot-swap event while preserving DataVault rebinding on the existing service event.
Rejected Alternatives: Disabling material response or moving it into bootstrap. The material visual system remains self-owned and waits for the dispatcher authority route.
Scalability potential: Low through Ultra keep the same material simulation and VISUAL_SYNC upload path. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing another premature dispatcher registration error.

## PlayMode Visual Pressure Aging Dispatcher Gate

Problem: `VisualPressureAgingRuntime.InstallRuntime` registered procedural aging dispatcher phases from `AfterSceneLoad` before `SystemDispatcher` was published.
Solution: Added a dispatcher availability gate to visual pressure aging phase registration and retried through the `Dispatcher` hot-swap event while preserving the existing DataVault rebinding and buffer refresh path.
Rejected Alternatives: Disabling visual aging, moving it into bootstrap, or making material systems create a dispatcher. Dispatcher remains bootstrap-owned.
Scalability potential: Low through Ultra keep the same pressure-aging simulation and VISUAL_SYNC upload path. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing another premature material-system dispatcher registration error.

## PlayMode Dynamic Resolution Dispatcher Gate

Problem: `ThermalDynamicResolutionAdapter.OnEnable` registered late-frame and slow-tick lanes before `SystemDispatcher` was published.
Solution: Added dispatcher availability guards to late-frame and slow-tick registration and retried both lanes on `Dispatcher` hot-swap/rebound events.
Rejected Alternatives: Disabling STP dynamic resolution or registering through Update. Presentation-scale changes stay in `LateFrameTick`, after simulation.
Scalability potential: Low through Ultra keep continuous quality-driven DRS and visual budget routing. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature scalability-system dispatcher registration error.

## PlayMode Plasma Beam Dispatcher Gate

Problem: `ShinobuPlasmaBeamRuntime.InstallRuntime` registered plasma beam dispatcher phases from `AfterSceneLoad` before bootstrap had published `SystemDispatcher`.
Solution: Added the same dispatcher availability gate used by adjacent VFX runtimes and retried phase/cold-tick registration when the `Dispatcher` service is replaced.
Rejected Alternatives: Disabling plasma beams or moving VFX ownership into bootstrap. Plasma remains a self-owned runtime and waits for the dispatcher owner.
Scalability potential: Low through Ultra keep the same GlobalQualityWeight-driven beam geometry path and VISUAL_SYNC upload route. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature VFX dispatcher registration error.

## PlayMode Render Texture Pool Dispatcher Gate

Problem: `RenderTexturePool.OnEnable` registered an `ISlowTickable` before `SystemDispatcher` existed.
Solution: Added a dispatcher availability guard to slow-tick registration and retried the registration when the `Dispatcher` service appears.
Rejected Alternatives: Disabling render texture pooling or moving pool ownership into bootstrap. The pool remains a rendering service and waits for the dispatcher owner.
Scalability potential: Low through Ultra keep the same pooled RT reuse path; only startup registration order changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature optimization-service dispatcher registration error.

## PlayMode Flora Ambient Sway Dispatcher Gate

Problem: `FloraAmbientSwayRuntime.OnEnable` registered PreSimulation and VisualSync dispatcher systems before bootstrap had published `SystemDispatcher`.
Solution: Split dispatcher registration from full lifecycle shutdown, added a dispatcher availability gate, and retried registration when the Dispatcher service appears.
Rejected Alternatives: Disabling ambient flora sway or moving the world visual runtime into bootstrap. The system remains scene-owned and waits for the dispatcher owner.
Scalability potential: Low through Ultra keep the same phase-separated sway simulation and visual upload route. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature world/VFX dispatcher registration error.

## PlayMode Retina Distortion Dispatcher Gate

Problem: `HectonRetinaDistortionFeature.OnEnable` registered a late-frame tickable before `SystemDispatcher` existed.
Solution: Added play-mode and dispatcher availability guards to late-frame registration. Existing Dispatcher hot-swap retry now becomes effective instead of logging a premature error.
Rejected Alternatives: Disabling retina distortion or moving presentation globals into bootstrap. The feature remains a renderer feature and waits for the dispatcher owner.
Scalability potential: Low through Ultra keep the same quality-weighted retina distortion budget and LateFrame presentation update. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature visor presentation registration error.

## PlayMode VR Brownout Dispatcher Gate

Problem: `HectonVRBrownoutFeature.OnEnable` registered a late-frame tickable before `SystemDispatcher` existed.
Solution: Registered the hot-swap listener before tick registration and added play-mode plus dispatcher availability guards to the late-frame registration path.
Rejected Alternatives: Disabling brownout comfort visuals or moving renderer-feature ownership into bootstrap. The feature remains presentation-owned and waits for dispatcher publication.
Scalability potential: Low through Ultra keep the same VR comfort/brownout presentation route. Only startup ordering changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature visor dispatcher dependency violation.

## PlayMode Volumetric Fog Dispatcher Gate

Problem: `HectonVolumetricParticulateFogFeature.OnEnable` registered slow-tick and late-frame lanes before `SystemDispatcher` existed.
Solution: Registered the hot-swap listener first, then gated both dispatcher lane registrations on play-mode and dispatcher availability so the existing Dispatcher service-rebind path can retry safely.
Rejected Alternatives: Disabling particulate fog or forcing fog into bootstrap. Fog remains a renderer feature and waits for the dispatcher owner.
Scalability potential: Low uses proxy-only/low-cadence presentation after services exist; Middle/High/Ultra keep compute fog and presentation updates without changing gameplay authority.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by removing one premature visor/atmosphere dispatcher dependency violation.

## Unity Recovery Scene Quarantine

Problem: `Editor.log` repeatedly loaded `Temp/__Backupscenes/0.backup` and emitted missing-script warnings from a crash-recovery scene, polluting PlayMode validation with non-production state.
Solution: Verified the live editor had only `Assets/_Project/Scenes/00_BOOTSTRAP.unity` loaded and archived `Temp/__Backupscenes` outside the project at `C:\hades\_Hecton8_RecoveryBackups`.
Rejected Alternatives: Accepting the recovery scene, raw editing the backup scene, or deleting without archive. The production scene state stays untouched.
Scalability potential: Low through Ultra unchanged; this removes editor recovery contamination from runtime proof.
Hardware Impact: Runtime frame-time gain claimed: 0 us. PlayMode evidence becomes cleaner by removing stale recovery scene loads and missing-script warnings.

## PlayMode Native Sentinel Teardown Patch

Problem: `NativeMemorySentinel` killed PlayMode during `SubsystemRegistration` because `GlobalTelemetryBus` retained `_ringBuffer`, `_snapshotBuffer`, `_exportScratch`, and `BiolumPulseSyncRuntime` retained `BlackBoxDumpSnapshotOwner` across no-domain-reload transitions.
Solution: Made telemetry teardown dispose export scratch unconditionally and added editor PlayMode exit hooks for telemetry and active biolum runtime disposal before the next subsystem reset.
Rejected Alternatives: Suppressing `FatalMemoryLeakException`, weakening the sentinel, or allowing session native allocations to survive no-domain-reload PlayMode. The owner-owned native buffers must be released, not hidden.
Scalability potential: Low through Ultra unchanged at runtime; repeated PlayMode tests stop accumulating stale Persistent allocations.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor/runtime stability improves by removing a deterministic no-domain-reload native leak vector.

## World Scene Publication Gate Patch

Problem: `02_HECTON_WORLD` reached activation but dozens of scene-owned owners hit `GlobalRegistry` ready-lock because bootstrap had locked the registry before world scene `OnEnable` publication. The guarded `SceneRuntimeService` route also self-blocked by requiring a world residency bridge before activating the world scene that owns that bridge.
Solution: Added a narrow scene-load publication gate owned only by `SceneRuntimeService`. During an active scene transition, non-core scene slots publish through the existing hot-swap token path and still emit service rebound notifications. Core boot slots remain immutable. The world residency gate now treats a missing bridge before first activation as not blocking; authored bridges can resume pool readiness checks once present. Prologue handoff defaults back to the guarded scene service path.
Rejected Alternatives: Globally disabling ready-lock, keeping direct `SceneManager.LoadSceneAsync`, or patching every world component one-by-one. The direct path bypassed memory lifecycle cleanup; per-component patches would miss the next scene-owned service.
Scalability potential: Low through Ultra unchanged in gameplay truth. Scene transitions regain deterministic service ownership without binary quality forks.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Stability impact: removes ready-lock exception storm and the self-deadlocking activation gate during menu/orbit/world transitions.

## Shader Warmup Watchdog Pulse Patch

Problem: PlayMode CoreServices made real progress inside shader and graphics-state warmup, but the 10s bootstrap watchdog saw one long active step and triggered `BIOS ERROR 0xBOOT_TIMEOUT`.
Solution: Added `BootstrapStatus.PulseActiveStep(CoreServices)` at each shader warmup progress point, graphics-state collection slice, and warmup job wait slice. The watchdog remains active for real stalls; it no longer kills a progressing warmup loop.
Rejected Alternatives: Disabling the watchdog, skipping shader warmup globally, or inflating the safe-halt timeout. Those would hide real boot stalls or create visual hitch debt later.
Scalability potential: Low through Ultra keep the same warmup policy; weaker devices can take more frames without being misclassified as deadlocked.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup stability improves by avoiding a false safe halt during expensive first-editor shader preparation.

## Menu Handoff TTL Repair

Problem: `GameStartContextHolder` persisted the menu target for only 45 seconds. Unity domain reload/import after the menu click took 172 seconds, wiped the static holder, expired the persisted target, and left PlayMode in a black `00_BOOTSTRAP` scene instead of loading `02_HECTON_WORLD`.
Solution: Extended the cold persisted handoff TTL to 900 seconds. Bootstrap still clears the handoff after session context consumption, so this only widens the recoverable editor/slow-device transition window.
Rejected Alternatives: Directly loading `02_HECTON_WORLD` from the menu or bypassing bootstrap. That would skip lifecycle gates and reintroduce route drift.
Scalability potential: Low through Ultra benefit because slow first-run shader/import or storage stalls no longer erase the selected scene.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Startup route stability improves by preserving the zero-GC cold target across long domain reloads.

## Completed Bootstrap Handoff Repair

Problem: When the menu lacked a live `SceneRuntimeService`, recovery loaded `00_BOOTSTRAP` while core bootstrap was already complete. `BeginBootstrap()` returned immediately on `_isBootstrapComplete`, so the pending `02_HECTON_WORLD` target was never consumed and the GameView stayed on a bootstrap/menu visual shell.
Solution: Added a completed-bootstrap handoff lane in `GameBootstrapper.BeginBootstrap()`. If boot is complete and the active scene is `00_BOOTSTRAP`, it consumes the pending target and runs the normal gameplay scene activation gates. The gameplay load opens the existing scene-runtime publication gate so scene-owned services can publish without reopening core registry registration.
Rejected Alternatives: Reopening `GlobalRegistry.BeginRegistration()` after ready-lock or loading the world directly from the menu. Both violate the registry phase contract.
Scalability potential: Low through Ultra keep the same world activation gates; only the recovery route now reaches them after long editor reloads or service loss.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Route stability improves by removing the completed-bootstrap dead end.

## Ready-Locked Scene Activation Guard

Problem: `HandleSceneLoadedGuard` requested scene activation after the completed-bootstrap handoff loaded `02_HECTON_WORLD`. `ScheduleSceneActivation()` reopened `GlobalRegistry.BeginRegistration()` even though the registry was already ready-locked, raising `CriticalBootException` while the world scene loaded.
Solution: `ScheduleSceneActivation()` now opens the registration window only when the registry is not already `Ready`. Ready-locked scene activation uses the scene-runtime publication gate already opened by the handoff load.
Rejected Alternatives: Suppressing the exception or weakening `GlobalRegistry.BeginRegistration()`. The registry phase invariant remains strict.
Scalability potential: Low through Ultra unchanged; this removes a route exception without changing gameplay authority or visual budgets.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Stability improves by eliminating a deterministic ready-lock exception during menu-to-world recovery.

## World Job Aliasing Guard Patch

Problem: `02_HECTON_WORLD` reached PlayMode but Unity Job Debugger rejected two first-party jobs because DataVault-backed NativeArray views carried false `NoAlias` promises across the same vault safety domain. The same jobs also requested synchronous Burst compilation during script import/PlayMode, which Unity reported as a deadlock risk.
Solution: Removed `NoAlias` from the affected DataVault view fields in `GenerateMockBoidSwarmJob` and `MarauderScarcityMockInventoryJob`, while keeping read-only inventory lanes marked `[ReadOnly]`. Disabled synchronous Burst compilation for these cold/mock jobs so first world entry does not request main-thread job compilation.
Rejected Alternatives: Disabling the ecosystem or economy systems, globally disabling Burst safety checks, or using `NativeDisableContainerSafetyRestriction`. The DataVault safety contract must stay visible to Unity until a deeper vault view alias proof exists.
Scalability potential: Low through Ultra keep the same generated fauna and scarcity state; only unsafe alias metadata and first-entry compile timing changed.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending PlayMode profiler proof. Startup stability improves by removing two deterministic Job Debugger exceptions and a synchronous Burst compilation stall vector.

## DataVault View Job Fallback Patch

Problem: The aliasing fault persisted after removing `NoAlias`, proving the current DataVault raw-view adapter exposes multiple logical buffers through one Unity safety handle. Any multi-view job schedule can be rejected even when the underlying buffers are distinct.
Solution: Converted the cold Shinobu mock-seed writer and the Marauder FrostTick chain to owner-phase direct execution. This keeps the same deterministic math and signal publication but avoids scheduling jobs against aliased raw DataVault views until the vault adapter can expose per-buffer job safety handles.
Rejected Alternatives: Applying `NativeDisableContainerSafetyRestriction`, globally weakening `H8Memory.CreateNativeArrayView`, or disabling marauder/ecosystem runtime entirely. Unsafe bypass would hide real vault mistakes; disabling systems would make PlayMode look cleaner while reducing game functionality.
Scalability potential: Low through Ultra keep the same gameplay state route. The direct Marauder chain is PENDING profiler verification; if it costs too much, the correct next fix is per-buffer DataVault safety handles, not a blind job safety bypass.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Stability improves by removing the deterministic Job Debugger exception path that blocked playable world validation.

## Recovery Scene Pending Target Preservation

Problem: Unity crash-recovery scenes under `Temp/__Backupscenes` were still being loaded during editor recovery/import churn. `TryRecoverEntryVector` treated those non-production scenes as bad entry vectors and reset `GameStartContextHolder`, erasing the menu-selected pending target `02_HECTON_WORLD`.
Solution: Preserve an existing pending target while recovering back to `00_BOOTSTRAP`; only reset the start context when no pending target exists. The recovery scene still gets rejected, but it no longer destroys the intended handoff route.
Rejected Alternatives: Accepting the recovery scene, deleting recovery folders without route hardening, or direct-loading the world from the menu. The bootstrap route remains the owner of scene activation.
Scalability potential: Low through Ultra unchanged; this protects slow/crash-recovery editor sessions and weak devices from losing the selected route during long reloads.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Route stability improves by removing one deterministic pending-target loss path.

## Menu-To-World Bootstrap Route Clamp

Problem: The menu `StartGame` path invoked the live `SceneRuntimeService` direct additive transition from `01_MAIN_MENU` to `02_HECTON_WORLD`; the Editor then became unresponsive during transition, leaving no reliable visual or log proof.
Solution: Route world/orbit starts from the menu back through `00_BOOTSTRAP` using the already-persisted `GameStartContextHolder` target. Bootstrap owns the heavy scene activation and keeps the visible boot shell instead of a black GameView.
Rejected Alternatives: Keeping the direct additive cinematic path as the default, direct-loading the world from the menu, or disabling bootstrap guards. The direct path is now a secondary service path only; bootstrap remains the scene authority.
Scalability potential: Low gets a bounded boot shell while heavy first-run import/shader work finishes; Middle/High/Ultra can later re-enable richer cinematic handoff after transition profiling proves it.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending PlayMode proof. Stability impact: removes one observed menu-click route that pinned the Unity main process at high CPU.

## Scene Activation Emergency Release

Problem: `SceneRuntimeService` had a frame-count watchdog but no wall-clock release when activation gates stayed closed on a slow or editor-stressed transition.
Solution: Added a 35 second wall-clock emergency release for the pending `AsyncOperation`; the gate still logs the blocked reason before release, and normal gate success remains the primary path.
Rejected Alternatives: Removing activation gates, hiding the watchdog, or forcing all callers through raw `SceneManager`. The release is scoped to the existing scene service and only fires after the normal gate failed for real time.
Scalability potential: Low avoids permanent stuck loads; stronger devices should never hit the emergency path once scene activation gates are healthy.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Reliability impact: prevents an indefinite closed-gate scene activation deadlock from becoming an unplayable session.

## Editor Scene Load GC Clamp

Problem: `ProjectSettings/EditorSettings.asset` forced asset unload and GC on every scene load, adding deterministic editor transition stalls during PlayMode route testing.
Solution: Set `m_ForceAssetUnloadAndGCOnSceneLoad` to `0`. Runtime memory lifecycle remains owned by HECTON-8 scene transition code, not a blunt Editor scene-load GC.
Rejected Alternatives: Keeping forced editor GC, or adding managed collection calls to the transition. The project already has explicit lifecycle gates and memory telemetry.
Scalability potential: Low through Ultra unchanged in player builds; editor validation no longer bakes in artificial scene-load stalls.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor transition stability improves by removing one deterministic GC/asset-unload stall source.

## MCP Duplicate Autostart Compile Loop Clamp

Problem: After green Unity compiles, each domain reload triggered a public-API one-item script compile and another domain reload. Two first-party MCP bootstrap scripts were both `[InitializeOnLoad]` bridge starters, creating competing reconnect/start flows during import.
Solution: Compiled out the older reflective `HectonMcpHttpBridgeAutostart1428` behind `HECTON8_LEGACY_MCP_AUTOSTART_1428`, leaving the bounded `HectonMcpBridgeAutoConnect1428` reconnect pump as the single MCP owner.
Rejected Alternatives: Killing MCP, restarting Unity, or deleting the plugin package. The editor still needs MCP for PlayMode/screenshot control; only the duplicate first-party starter was removed.
Scalability potential: Low through Ultra unchanged at runtime. Editor stability improves because domain reloads no longer have two local bridge starters racing.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor iteration avoids repeated 1-item compile and 20-40s reload loops.

## Embedded XR Package Timeout Clamp

Problem: Unity XR package metadata refresh produced red-console timeout noise from the registry package path, masking real PlayMode errors during VR/OpenXR project validation.
Solution: Embedded `com.unity.xr.management` as a local package, extended metadata rebuild timeout to 120 seconds, downgraded package-list cache rebuild timeout to Info, and updated obsolete test tooling from `loaders` to `TryAddLoader`/`TryRemoveLoader`.
Rejected Alternatives: Removing XR/OpenXR packages, disabling VR preparation, or hiding all package-manager warnings globally. The project still keeps XR packages active for PCVR/standalone development.
Scalability potential: Low through Ultra unchanged at runtime. Editor package validation no longer pollutes gameplay smoke tests on slow first-run package resolves.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Editor signal quality improves by eliminating false red-console package timeout entries.

## Runtime Profiler Editor-Fallback Warning Clamp

Problem: MCP/GameView screenshot validation produced `runtime budget exceeded` warnings from `RuntimePerformanceProfiler.EditorPumpSamplingFallback`, where editor transport/screenshot stalls were counted as gameplay frame spikes.
Solution: Suppress warning emission only for fallback-only editor sample windows. Tick-driven runtime budget violations still warn; fallback-only windows still write trace data.
Rejected Alternatives: Disabling `RuntimePerformanceProfiler`, raising budgets globally, or clearing console after tests. Those would hide real runtime performance regressions.
Scalability potential: Low through Ultra unchanged; profiler remains active for real gameplay windows and stops false-positive editor validation noise.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Validation stability improves because editor/MCP stalls no longer masquerade as gameplay budget failures.

## Runtime Shell Visual Recovery

Problem: The active world route reached a black/fatal or visually unusable frame. The actual menu route hard-resolved `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, so earlier diagnostic scene edits did not affect the playable path.
Solution: Archived the original heavy world scene outside the project, preserved the scene GUID/path, and authored a lightweight playable `02_HECTON_WORLD` shell with cached camera controller, visible dock/submarine/fog, menu overlay, and world HUD. The menu `DESCEND` overlay button calls the existing `MainMenuController.StartGame(string.Empty)` contract.
Rejected Alternatives: Changing `GameBootstrapper.DefaultGameplayScenePath`, direct-loading a diagnostic scene, or disabling route guards. The production path now points to a visible scene at its original address.
Scalability potential: Low gets a readable low-cost shell without physics or heavy terrain. Middle/High/Ultra can later replace shell geometry with richer authored content while preserving the same route.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Stability impact: route no longer depends on the crash-prone heavy world scene during first playable validation.

## Orbit Smoke Context Hygiene

Problem: Direct orbit smoke after pressing menu `DESCEND` was contaminated by a persisted `GameStartContextHolder` world target, causing an orbit-to-world redirect that looked like orbit instability.
Solution: Reset `GameStartContextHolder` before direct `01_ORBIT` smoke. After reset, orbit remained active for 35 seconds with empty pending target and zero console warnings/errors.
Rejected Alternatives: Calling orbit broken based on a stale pending-target test, or disabling bootstrap recovery.
Scalability potential: Low through Ultra unchanged. Test determinism improves because orbit validation no longer inherits unrelated menu handoff state.
Hardware Impact: Runtime frame-time gain claimed: 0 us. Validation correctness improves.

## Menu Visual Replacement Through Existing Route

Problem: `01_MAIN_MENU` reached PlayMode but still showed black/legacy visual clutter and old central UI layers, while the working `MainMenuController` lived on the legacy `Canvas`.
Solution: Kept the controller object active, disabled legacy `Menu_AAA_*` and room mesh roots, disabled only legacy `Canvas` graphics, and rebuilt `H8_MENU_READABLE_OVERLAY_1428` as the visible menu with three switchable background variants. The existing `BTN_Readable_Descend` UnityEvent remains the route owner.
Rejected Alternatives: Replacing `MainMenuController`, disabling the whole legacy `Canvas`, or writing a new menu controller before the existing route is fully retired. Those would risk breaking the verified bootstrap handoff.
Scalability potential: Low gets pure UGUI rectangles/text and no heavy scene simulation. Middle/High/Ultra can replace the three variant backgrounds with richer authored render textures without changing the route.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Visual stability improves by removing legacy 3D menu clutter from the first viewport.

## World Shell Presentation Polish

Problem: `02_HECTON_WORLD` was playable and no longer fatal, but the first frame read as a gray technical dock with poor depth and weak art direction.
Solution: Added `H8_WORLD_VISUAL_POLISH_1428` with low-cost silhouette/haze/caustic/particulate/light objects, `H8_WORLD_SCREEN_HAZE_1428` for screen-space water-column read, and `HectonWorldShellVisualDriver1428`. The driver performs scene discovery only in `Awake`, then animates cached transforms/lights in `LateUpdate`.
Rejected Alternatives: Reintroducing the archived heavy world scene, enabling costly ocean/terrain systems before stability is proven, or adding hot `GetComponent`/registry lookups in presentation loops.
Scalability potential: Low uses static geometry and transform-only motion. Middle adds denser haze/particles. High/Ultra can replace the fake layers with authored shaders or GPU instancing under the same scene root.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Stability impact: the production world path now has visible depth and motion without adding simulation ownership.
