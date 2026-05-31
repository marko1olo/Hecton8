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
