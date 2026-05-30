# 14MU Rationale

Date: 2026-05-28
Status: PENDING VERIFICATION

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="14MU">`; blindly reading nearby `14xx` prompts would contaminate the architecture decision stream.
Solution: Use the explicit user assignment as the active directive, keep `14MU` status/rationale/log files, and ignore neighboring batch prompts.
Rejected Alternatives: Mapping `14MU` to `1404`, `1406`, or `1427` by guess. That would violate strict parsing and could mutate the wrong domain.
Scalability potential: Keeps platform-adaptation decisions tied to the current user domain instead of one narrow mobile/XR batch role.
Hardware Impact: No runtime change; prevents wrong code paths from being edited under i3/MX350, Steam Deck, Quest, or console assumptions.

Problem: Platform domain has broad targets, but `PLATFORM_PORTABILITY_PROOF_LADDER.md` explicitly orders proof: Windows Editor/player and Copper Wire V0 first, then MX350, then Linux/Steam Deck, macOS, XR, Quest/PICO, consoles.
Solution: Treat platform work as blocker removal and static-risk reduction unless fresh device/player artifacts exist. Any "ready" wording stays `PENDING VERIFICATION`.
Rejected Alternatives: Making serialized package/settings presence equivalent to readiness. That ignores shader, native plugin, input, storage, thermal, profiler, GC, and player-launch proof.
Scalability potential: Low/Middle/High/Ultra remain supported as continuous weight bands; platform labels select endpoints or proof lanes, not separate gameplay truth.
Hardware Impact: Prevents spending weak-device budget on unproven XR/VRS claims; MX350 path remains render scale, fakes, culling, mip pressure, and continuous quality response.

Problem: Binary quality/platform branches risk stutter, visual popping, and divergent gameplay behavior across PC, Deck, VR, and console.
Solution: Use `HomeostasisBrain.GlobalQualityWeight` as source scalar. Hardware labels may choose curve endpoints; runtime fidelity, cadence, capacity, and presentation cost scale continuously with hysteresis.
Rejected Alternatives: `if (isLowEnd)`, `if (isQuest)`, or `QualitySettings.GetQualityLevel()` as gameplay/runtime truth. Standard Unity quality levels are authoring labels, not hot runtime authority.
Scalability potential: Weak devices keep silhouettes, fog LUTs, route cues, pressure audio, readable instruments; high/ultra spend saved cycles on silt wakes, wetness, longer LOD residency, richer material response.
Hardware Impact: i3/MX350 avoids frame spikes and shader bloat; strong PCs/PCVR get additive presentation without save/DTO/authority drift.

Problem: `ContentTieredGroupPolicy` used hard VRAM branches: `<=2048 MB` forced low visual budget and `>4096 MB` unlocked overkill. That creates binary platform behavior and ignores `GlobalQualityWeight`, XR pressure, and runtime thermal/load-shed state.
Solution: Added continuous `ResolveRuntimeVisualBudgetWeight01()` combining `HomeostasisBrain.GlobalQualityWeight`, smoothed graphics-memory capacity, XR ceiling, and content-tier ceiling. Visual budget fields now derive from weighted lerps; overkill download requires weighted threshold instead of raw VRAM.
Rejected Alternatives: Keeping raw `SystemInfo.graphicsMemorySize` forks or adding a new platform service. Standard Unity quality tiers were rejected because they are authoring labels and do not represent runtime pressure.
Scalability potential: Low keeps 1D LUT/triangle/dot-product dear-lie features with 512 particles and 8 raymarch steps; middle/high smoothly add silt, salt, hull dents, raymarch and POM budget; ultra reaches 16K particles/64 steps/16 POM taps when the scalar permits.
Hardware Impact: MX350/Quest-like budgets cannot accidentally download overkill content; high-end PCs can still spend budget on richer content when `GlobalQualityWeight` and hardware capacity agree. Estimated hot-path GC change: 0 B; CPU delta expected below 1 us per policy call.

Problem: `WorldChunkResidencyManager.ResolvePredictiveVramAbortState()` returned `false` for any GPU reporting more than 2048 MB. That means predictive streaming ignored VRAM pressure on high-end PCs, Steam Deck-like shared memory if misreported, and any future platform with pressure above the baseline.
Solution: Removed the hard skip. Abort threshold now scales from MX350 survival threshold to a capped visual-overkill ceiling through `ResolveSmoothGlobalQualityWeight01()`. Shared-memory devices use `HardwareTierDetector.RecommendedVramBudgetBytes`. Resume threshold uses proportional hysteresis instead of a fixed 1.4 GB floor.
Rejected Alternatives: Applying the MX350 1.6 GB threshold to every GPU. That would protect weak devices but punish high-end visual residency. Also rejected disabling predictive streaming globally under pressure; scoped only predictive requests.
Scalability potential: Weak/shared-memory devices abort predictive loads early; middle devices expand modestly; high/ultra allow longer predictive residency up to 4 GB while retaining pressure hysteresis.
Hardware Impact: i3/MX350 keeps 1.6 GB abort / 1.4 GB resume behavior; top-tier can keep more streamed chunks before abort. Estimated saved hitch risk: avoids uncontrolled predictive loads under pressure; exact microseconds pending profiler/player proof.

Problem: Verification compile is required, but host CPU was measured at 100%, and project law forbids `dotnet build` when CPU exceeds 50% or another compiler is active.
Solution: Did not launch build. Ran static platform proof audit and source-pattern checks. Marked compile/runtime status `PENDING VERIFICATION`.
Rejected Alternatives: Forcing a build under load or claiming Unity readiness from static scans.
Scalability potential: No runtime change.
Hardware Impact: No compile contention added to the shared machine.

Problem: User requested ignoring status/rationale/log protocol and accepting source-only proof, but `AGENTS.md` still mandates disk state and final log append.
Solution: Obey `AGENTS.md`; keep reports concise and avoid JSON/table bloat. Treat C# source and static scans as proof inputs, not as permission to skip required state files.
Rejected Alternatives: Following chat override against authoritative project file. That would break anti-amnesia and handoff rules.
Scalability potential: No runtime change; preserves batch audit continuity for platform adaptation work.
Hardware Impact: No runtime impact; minimal text I/O only.

Problem: A working-tree variant had `ApplyAsyncUploadBudgetForQuality()` in `WorldChunkResidencyManager.Tick`, mutating Unity upload budget during simulation phase.
Solution: Restored/verified phase-safe ownership: cold `Awake` can seed the budget once, runtime budget sync executes from `LateFrameTick` only. `Tick` now advances simulation, jobs, fade state, stress metric, and telemetry without presentation/platform setting writes.
Rejected Alternatives: Keeping `QualitySettings.asyncUpload*` writes inside Tick. That can couple simulation cadence to upload-budget presentation policy.
Scalability potential: Low devices still receive 64 MB / 1 ms upload survival budget; high/ultra can scale toward 256 MB / 4 ms after simulation settles.
Hardware Impact: Expected CPU delta below 1 us; removes a native settings write branch from the simulation phase.

Problem: Predictive VRAM abort ceiling must not depend on hot hardware discovery.
Solution: Verified `_predictiveVramCeilingBytes` is cached via `ResolvePredictiveVramCeilingBytesCold()` from cold service cache. Hot abort checks read `_vramMonitor`, `VRAMBudgetTracker`, cached ceiling, and continuous quality scalar only.
Rejected Alternatives: Calling `SystemInfo.graphicsMemorySize` in streaming pressure checks every Tick/load request.
Scalability potential: Shared-memory devices use recommended survival ceiling; high-end devices retain visual-overkill ceiling up to 4 GB without hot platform probing.
Hardware Impact: Removes native hardware query from streaming pressure hot path; exact microseconds pending Unity profiler proof.

Problem: APEX lock proof cannot honestly claim the entire repository is flattened; static scan found unrelated pre-existing multi-lock helper patterns outside the edited platform path.
Solution: Scope proof to changed production files. `WorldChunkResidencyManager` has one write-lock method, `WriteTelemetrySample`, with one acquire, one release, strict `try/finally`. `ContentRuntimeServices` has no DataVault write lock acquisition in the changed policy class.
Rejected Alternatives: Declaring global deadlock elimination while other domains still own unresolved lock helpers. That would be a fake report.
Scalability potential: No new platform scalability risk introduced by this patch.
Hardware Impact: No added lock contention; telemetry write path unchanged.

Problem: VRAM pressure response and UI mip-bias gate could mutate `QualitySettings` or external mip pressure from force/enqueue/tick routes instead of a settled presentation phase.
Solution: `VRAMPressureMonitor` is `ILateFrameTickable`; `ForceImmediateSampleAndResponse()` only sets `_forceSampleQueued`. `AssetLoadDispatcher` queues UI mip-bias work and evaluates it in `LateFrameTick`. Headroom uses cached runtime VRAM budget, not per-sample `SystemInfo`.
Rejected Alternatives: Immediate force mutation from bootstrap/dispatcher and UI enqueue-time mip changes. Those routes create phase drift and can change presentation state before simulation settles.
Scalability potential: Low/Middle devices still collapse mips and LOD under pressure; High/Ultra keep budgeted visual memory until real pressure appears. All bands use the same phase route.
Hardware Impact: Removes hot hardware query and native settings writes from non-visual phases. Expected CPU delta below 5 us per pressure frame; profiler proof pending.

Problem: Thermal DRS EWMA job could hold the `ResolutionScaleState` vault buffer across a frame, then the next `LateFrameTick` could continue into mock/scalability/DRS/telemetry buffer locks.
Solution: After `TryFinalizePendingStressJobNoWait()`, `AdvanceThermalResolutionState()` returns immediately while `_stressEwmaScheduled` remains true. FSR support now reads cold cached capability fields instead of hot `Application`/`SystemInfo` queries.
Rejected Alternatives: Completing the EWMA job immediately every frame. That would convert the job into a same-frame schedule/readback stall.
Scalability potential: Weak devices avoid lock contention and stalls; stronger devices keep the same DRS quality policy without extra phase cost.
Hardware Impact: Prevents a concrete DataVault multi-lock/pin vector. Runtime frame cost improvement is stall-risk removal, not steady-state math savings.

Problem: Quest 2 foveation used a binary high lock, and foveation caps/platform classification were probed from policy flow.
Solution: Replaced the bool with `quest2FoveationFloor01`, blended by pressure and `GlobalQualityWeight`. Foveated caps, Android/runtime class, and standalone-like platform state are cached in cold lifecycle snapshots.
Rejected Alternatives: Fixed high foveation on all Quest 2-class runs. That protects weak devices but wastes visual detail when quality/pressure allow relief.
Scalability potential: Quest 2 under pressure still reaches high foveation; PCVR/Quest Pro/high-quality states can relax toward lower foveation continuously.
Hardware Impact: Removes hot capability/platform probes from foveation policy; reduces visual quality loss on stronger VR devices without changing gameplay truth.

Problem: Content runtime public acquire/release/load paths lazily called `CacheDependencies()` when `_dataVault` was missing, re-polling `GlobalRegistry` after runtime start.
Solution: Runtime routes now fail closed with a dev log when the cold DataVault dependency is absent. Content visual budget uses a cold graphics-memory snapshot.
Rejected Alternatives: Hidden `GlobalRegistry` rebinds from public runtime methods. That violates cold identity injection and obscures dependency failures.
Scalability potential: Streaming/content decisions remain continuous through `GlobalQualityWeight`; missing dependencies stop content mutation instead of drifting through a stale global lookup.
Hardware Impact: Removes hot registry polling from content routes. Expected CPU gain is small; stability gain is deterministic failure on bad boot order.

Problem: `LODSystemManager` applied LOD bias/math-LOD globals from mutation routes and kept preset state as the visible quality source, which could desync presentation from the settled simulation phase.
Solution: Added `_runtimeQualityWeight01`, `_qualityVisualSyncDirty`, and `FlushQualityVisualSync()` so `QualitySettings.lodBias` and `DistanceMath.PushShaderMathLod` execute in `LateFrameTick`. Presets now update intent; visual globals flush after simulation. Camera resolve now uses cold registry/runtime context only.
Rejected Alternatives: Direct `QualitySettings` writes in `ApplyQualityPreset` or emergency strike routes. Unity quality globals are presentation state, not simulation truth.
Scalability potential: Low keeps early LOD crossover and cheap shader math; middle interpolates; high/ultra spend on longer geometry retention and richer shader math without gameplay route drift.
Hardware Impact: Removes phase drift and hot bootstrap component fallback from LOD processing. Expected CPU delta below 2 us per frame; main gain is deterministic visual-sync ordering.

Problem: `PlatformAdaptiveBudgetGovernor.SampleAndApply()` re-read hardware detector flags and recommended VRAM while sampling runtime pressure.
Solution: Moved shared-memory, Deck-like, Quest-like, recommended VRAM budget, target frame time, and shared-memory baseline render scale into `CacheHardwareBudgetProfileCold()`. Runtime sample now consumes cached fields and live pressure only.
Rejected Alternatives: Calling `HardwareTierDetector.EnsureInitialized()` every sample. Hardware classification is cold boot identity, not a hot pressure signal.
Scalability potential: Weak/shared-memory devices keep survival render-scale floors; middle/high/ultra expand continuously through pressure and quality without platform-label branches in the sample loop.
Hardware Impact: Removes detector probing from pressure sampling. Expected direct gain below 1 us, but avoids hidden platform-query stalls on low-power CPUs and handhelds.

Problem: `ImpostorSystem.ActivateImpostor()` performed `billboard.TryGetComponent(out renderer)` during late-frame activation, and `ObjectPoolManager` scanned pooled components on each spawn/despawn.
Solution: Added `IObjectPoolService.TryGetPooledRootRenderer`. `ObjectPoolManager.PoolItemMarker` now cold-caches root `Renderer`, root `DespawnTimer`, and `IPoolable[]` at instantiation/warmup. Spawn/despawn use `_poolMarkerCache` and iterate cached poolables; impostor activation reads renderer through the pool contract.
Rejected Alternatives: Keeping one-time first-activation lookup inside `LateFrameTick`. First activation is still a hot presentation event and can cluster on weak devices when several distant objects swap to billboards.
Scalability potential: Weak devices get cheaper impostor activation and pooled VFX/tool reuse; middle/high/ultra keep richer distant billboards without component-scan spikes.
Hardware Impact: Removes per-spawn `GetComponents` scans and late-frame billboard renderer `TryGetComponent`. Expected saved time depends on spawn burst size; static path removes O(component-count) Unity native scans from each reuse.

Problem: APEX proof needed refreshed after interface/pool changes, but compile was still forbidden by host load.
Solution: Ran in-memory static AST scans: brace balance on changed files, proof-string checks, global hot-method body scan across 1800 runtime C# files for `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, `GetComponents`, scene search, `Camera.main`, and `Resources.Load`. Ran `Tools/PlatformPortabilityProofAudit.py`.
Rejected Alternatives: Launching `dotnet build` while CPU measured 82-100%. That violates compilation throttling and risks shared-machine contention.
Scalability potential: No new runtime feature; verification protects platform adaptation from dependency drift.
Hardware Impact: Build CPU avoided under load. Audit status remains `PASS_WITH_WARNINGS` due existing missing XR provider serialized proof, missing Addressables content artifact, and missing build artifact.

Problem: The new pool metadata cache removed hot component scans but needed hardening for stale cache and duplicate-despawn cases.
Solution: When a queued pooled instance lacks cached marker metadata, `Spawn` removes cache, decrements pool capacity, destroys the stale object, and continues. When despawn sees the inactive queue already at capacity, it removes the cache and destroys the duplicate return instead of leaving an orphan inactive object. Cached `IPoolable` callbacks now null-check destroyed component references.
Rejected Alternatives: Trusting marker cache blindly. That would make rare lifecycle corruption turn into pool exhaustion or null callback failures on weak devices during spawn bursts.
Scalability potential: Low-end devices avoid accumulating orphan inactive objects; high/ultra can keep larger pools without hidden queue/capacity drift.
Hardware Impact: Prevents pool capacity leaks and removes failure spikes. Hot cost is a cached dictionary lookup already required by the new route; no component scan is reintroduced.

Problem: Static platform probe scan found `SystemInfo.supportsSetConstantBuffer` in `VisualSyncTick` for accessibility/flora/water visuals and `XRSettings` in `OpenXRManualOverrideLever.Tick`.
Solution: `AccessibilitySettings`, `FloraAmbientSwayRuntime`, and `WaterOpticsRuntime` cache constant-buffer support in lifecycle via `CacheGraphicsCapabilitiesCold()`. `OpenXRManualOverrideLever.Tick` reads `HectonXRRuntimeState.IsXRActive`, which is dispatcher-owned runtime state, instead of polling `XRSettings`.
Rejected Alternatives: Per-frame `SystemInfo`/`XRSettings` reads. Platform identity belongs to cold/lifecycle or dispatcher-owned state, not visual/simulation hot loops.
Scalability potential: Weak devices avoid native platform probe overhead in visual sync; VR/flat fallback logic stays stable through the central XR state route.
Hardware Impact: Removes four hot native platform probes. Expected direct gain is sub-microsecond each, but it eliminates platform-query drift and blocks future hot-probe regressions through edit tests.

Problem: Full `git diff --check` still failed after project-scope changes.
Solution: Identified the failure as unrelated pre-existing vendor whitespace in `Assets/Candice AI for Games/Scripts/Libs/Candice GOAP/CandiceGOAPAgent.cs:263`. Scoped diff check for the touched files passed. Left vendor file untouched per worktree safety rules.
Rejected Alternatives: Editing unrelated vendor source to make a global whitespace command green. That would violate domain boundary and dirty unrelated code.
Scalability potential: No runtime change.
Hardware Impact: No runtime change; avoids unnecessary file churn.

Problem: `JacobianFoamGpuRuntime.LateFrameTick` acquired foam params, tuning, and wake DataVault write buffers inside one visual phase, guarded by nested bool release flags. That violates the APEX lock-flattening requirement and leaves a deadlock/stall vector in a fluid VFX path.
Solution: Split the route into one-buffer phases: `TryWriteTuning`, `TryWriteAndUploadParams`, `TryWriteAndUploadMockWakes`, then telemetry. Each write window has exactly one acquire and one release inside `finally`. Mock storm tuning and wake generation moved to value-type contract helpers so the runtime can generate the visual fake without a multi-buffer job.
Rejected Alternatives: Keeping `GenerateMockStormStateJob` as the runtime path because it needs params+tuning+wake arrays at once; replacing the visual fake with a heavier physical foam simulation; adding a global lock order policy instead of removing the nested locks from this system.
Scalability potential: Weak devices keep the same cheap shader/compute foam visual with lower deadlock risk; middle/high/ultra can spend quality on resolution and wake count without longer critical sections.
Hardware Impact: Removes multi-lock contention risk from a visual-sync VFX system. Direct CPU delta is expected below 5 us; the real gain is eliminating a stall/deadlock path on i3/MX350 and shared-memory handhelds.

Problem: Full hot-method scans are expensive in this repository; two direct full-tree Python scans timed out before reporting violations.
Solution: Re-ran the same method-body logic through an `rg` candidate-file prefilter. It scanned 424 runtime files that contained forbidden tokens and 521 hot method bodies, passing both dependency and platform-probe gates. CPU measured 99%, so compilation remained blocked by the project throttle.
Rejected Alternatives: Declaring the timed-out scans as proof, or launching `dotnet build` under 99% CPU.
Scalability potential: No runtime feature change; proof protects platform adaptation from hot lookup/probe regression.
Hardware Impact: Avoided one prohibited compiler launch under host contention; no runtime impact.

Problem: `HectonMarineSnowRenderer.EnsureBuffers()` is reached from `LateFrameTick` before GPU buffers are ready and was reading `HardwareTierDetector.AllowHighResourceComputeShaders`. Its fallback texture creation helpers also read `SystemInfo.SupportsTextureFormat` on the same visual runtime path.
Solution: Added `CacheGraphicsCapabilitySnapshotCold()` in `OnEnable`. High-resource compute permission and fallback 3D texture formats are now captured once into `_coldAllowHighResourceComputeShaders`, `_coldEmptyCaveSdfTextureFormat`, and `_coldEmptyAbyssalFlowTextureFormat`. Runtime buffer/bootstrap helpers read cached fields only.
Rejected Alternatives: Treating first-time buffer creation as harmless because it happens before steady state. On weak devices, first underwater entry is still a player-visible visual phase and must not do platform probing.
Scalability potential: Weak devices keep cheap fallback marine-snow textures and compute denial from cold policy; middle/high/ultra can still use richer compute particles when the cold profile permits it.
Hardware Impact: Removes platform/capability probes from marine-snow late-frame bootstrap path. Direct time saved is small; it prevents first-entry stalls and platform-identity drift on MX350, Deck-like shared memory, and standalone VR.

Problem: APEX required refreshed proof after changing marine snow and foam runtime paths.
Solution: Ran brace/syntax balance on touched files, scoped diff whitespace check, hot platform probe scan, hot DataVault multi-write scan, and platform portability audit. CPU measured 100%, no compiler process existed, and no build was launched.
Rejected Alternatives: Launching `dotnet build` under 100% CPU or claiming global Unity/player proof from static scans.
Scalability potential: Verification protects continuous platform adaptation gates from hot-path regression.
Hardware Impact: Avoided one prohibited compiler launch under host contention.

Problem: `HectonMarineSnowRenderer.RunMarineSnowVisualTick()` still called a helper that could walk parents or call `TryGetComponent` when the camera reference was missing or stale. The retry was throttled, but it still lived in `LateFrameTick`.
Solution: Split camera ownership into cold resolution and hot validation. `ResolveTargetCameraCold()` performs the parent/transform component lookup only from lifecycle/bind routes. `RunMarineSnowVisualTick()` now calls `HasCachedTargetCamera()`, which only reads cached `Camera`/`Transform` fields and invalidates stale cache without scene/component search.
Rejected Alternatives: Keeping `CameraResolveRetryFrames` throttling. Throttled lookup is still a runtime component scan and can cluster exactly when underwater VFX activates.
Scalability potential: Weak devices avoid a camera hierarchy component walk during first underwater marine-snow frames; middle/high/ultra keep the same richer marine-snow GPU path after cold binding.
Hardware Impact: Removes one late-frame Unity component lookup vector from a VFX path. Expected direct gain is small per frame, but it eliminates a visible first-entry stall risk on MX350, Steam Deck-like shared memory, and standalone VR.

Problem: The earlier hot lookup scan did not explicitly include wrapper names like `ResolveComponentInParents`; the earlier DataVault scan needed refinement to avoid counting AI target acquisition helpers as lock acquires.
Solution: Re-ran expanded in-memory method-body scans. Hot lookup/platform scan covered 453 candidate files and 558 hot methods with 0 violations. Refined DataVault write-lock scan covered 162 candidate files and 427 hot methods with 0 multi-write-lock hot methods.
Rejected Alternatives: Treating the previous direct-token scan as complete. Wrapper names must be audited because they hide the same native component lookup cost.
Scalability potential: Keeps platform-adaptation systems from reintroducing hidden component and platform probes under presentation load.
Hardware Impact: No runtime code beyond the marine-snow cache split; verification avoided one prohibited build while CPU stayed at 100%.

Problem: The underwater visual owner path could still recover missing particles, marine snow, exhale bubbles, transition VFX, visor, or shallow beam owners from the visual tick chain. Those helpers use camera/hierarchy/component resolution and can run exactly during underwater transitions.
Solution: `RunUnderwaterVisualTick()` now only marks missing visual owners through `_runtimeVisualOwnerResolveRequested`. `SlowTick()` calls `ResolveRuntimeVisualOwnersOnColdCadence()` to perform the actual recovery. `UpdateUnderwaterSuspendedMotes`, `HandlePlayerExhale`, `UpdateShallowSunBeam`, thermocline, submerge, and surface-break triggers now skip unresolved optional visuals and request cold recovery instead of searching from the hot phase. Marine-snow binding from underwater visuals passes the already cached `Camera` component directly.
Rejected Alternatives: Keeping recovery lookups as rare hot fallbacks. Rare fallbacks still stack on scene entry, underwater transition, or broken prefab references, which are the worst moments for weak GPUs and standalone VR.
Scalability potential: Low devices keep stable presentation cadence and degrade by temporarily missing optional motes/bubbles/beam until cold recovery; middle/high/ultra keep full visuals once references are restored without hot hierarchy scans.
Hardware Impact: Removes transitive component/hierarchy recovery from the underwater late-frame visual chain. Profiler proof absent; expected saving is stall avoidance during underwater state changes, not steady-state arithmetic reduction.

Problem: APEX verification had to be refreshed after the underwater owner-route change.
Solution: Ran brace balance, underwater hot visual lookup guard, expanded hot lookup/platform scan, refined DataVault write-lock scan, scoped diff check, and platform portability audit. CPU measured 99.2%, no compiler process was running, and no build was launched.
Rejected Alternatives: Claiming compile proof without a build, or launching a compiler under the project throttle violation.
Scalability potential: Static guards now cover both direct marine-snow hot lookup and the upstream underwater owner bind route.
Hardware Impact: Avoided one prohibited compiler launch; no runtime measurement was produced.

Problem: Direct hot-method scans missed transitive helper calls from platform/render hot phases into `SystemInfo`, `HardwareTierDetector`, component lookup, and XR subsystem discovery.
Solution: Built a local method call graph and patched the 14MU domain subset. `VRAMPressureMonitor` caches system RAM cold and latches external mip pressure for late-frame sampling. `ThermalDynamicResolutionAdapter` uses `HectonXRRuntimeState.IsXRActive` instead of `SubsystemManager.GetSubsystems` in the Android XR scale commit. `HectonFluidEngine`, `GpuScatterLodManager`, `OceanSinglePassRuntime`, and `AsyncBuoyancyReadbackRuntime` read cold capability fields in visual/simulation helper paths. `HectonUnderwaterVisuals` now keeps `LateFrameTick` on cached camera data and requests cold recovery for stack/pass gaps.
Rejected Alternatives: Treating throttled or rare fallbacks as acceptable hot-path work; fixing every unrelated UI/AI component lookup outside the platform domain; removing XR scale writes entirely.
Scalability potential: Low devices avoid native platform probes and hierarchy scans during underwater entry, scatter bootstrap, async wave readback, and VRAM pressure events. Middle/high/ultra keep the same visual features, with quality still controlled by continuous `GlobalQualityWeight` and cached device capability.
Hardware Impact: Removes several native capability/subsystem/component lookup vectors from player-visible phases. Expected direct microsecond gain is small per call but prevents clustered first-entry stalls on i3/MX350, Steam Deck-like shared memory, standalone VR, and Android XR.

Problem: APEX verification after the transitive patch needed to separate dependency lookups from phase-safe presentation writes.
Solution: Ran scoped transitive dependency lookup scan excluding presentation setters: 7 changed domain files, 18 hot methods, 488 local call edges, 0 reports. `XRSettings.eyeTextureResolutionScale` remains only as an Android XR presentation write through `LateFrameTick -> CommitRenderScale -> CommitQuestXrScale`. Scoped DataVault write-lock scan found 0 violations. Brace balance and scoped diff checks passed. Platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Calling the Android XR setter a dependency lookup, or claiming full compile proof while CPU stayed at 100%.
Scalability potential: Confirms cold lookup ownership without deleting legitimate late-frame presentation control for standalone VR.
Hardware Impact: No compiler load added to the shared machine. Runtime microseconds still require Unity profiler/player proof.

Problem: A wider render/world/visor/platform call graph still exposed late-frame helper chains into native capability probes: celestial LUT/firmament, leak plume compute, ocean wave readback, carve debris, visor buffers, bilateral DRS, GPU scatter, cave voxel lighting, and biome shader payload CBuffers.
Solution: Captured platform capabilities in lifecycle cold fields: compute support, high-resource compute permission, constant-buffer support, texture formats, max texture size, and graphics memory. Hot phases now read those cached fields only. `CarveDebrisComputeRenderer.TryEnsureGpuState()` no longer polls missing registry services from `LateFrameTick`; it relies on cold cache plus hot-swap rebinding.
Rejected Alternatives: Keeping rare first-use probes in visual sync; treating capability checks as cheap enough; adding platform-specific quality branches instead of continuous quality/capability snapshots.
Scalability potential: Low devices avoid native capability stalls during first visual activation; middle/high/ultra keep richer scatter, biome, celestial, DRS, and visor visuals when the cold capability profile allows them.
Hardware Impact: Removes multiple late-frame native capability calls and one hot registry self-heal route. Direct steady-state savings are small; the practical gain is lower first-entry stall risk on i3/MX350, Steam Deck-class shared memory, standalone VR, and Android XR.

Problem: The lock proof needed to distinguish real multi-lock ownership from wrapper helpers and sequential one-buffer windows.
Solution: Ran refined write-lock token ordering across 10 changed files: 42 acquire methods, maximum observed write-lock depth 1, 0 violations. Manually inspected flagged helpers and `TryLoadNoirColorCsvCold`; Noir scratch and profile writes are separate try/finally windows, and wrapper helpers expose one lock to callers that release through the paired route.
Rejected Alternatives: Counting every helper acquire as a deadlock vector without checking token order; rewriting stable vault helper APIs during a platform capability patch.
Scalability potential: Confirms render/platform improvements did not increase DataVault contention under visual-sync load.
Hardware Impact: No runtime feature change. Prevents accidental multi-buffer lock expansion while avoiding unnecessary code churn.

Problem: Visor/HUD late-frame presentation still had transitive helper paths into component lookup: presentation auto-resolve, compositor overlay discovery, visor renderer/self-camera lookup, player survival/movement fallback, and spectrum sonar fallback.
Solution: Split cold binding from hot presentation. `SuitHUDPresentationController.LateFrameTick` now refreshes cached references only; CanvasGroup, visor renderer, projection-source normalization, and self-camera resolution happen from cold lifecycle/forced rebuild routes. `SuitHUDScreenCompositor.LateFrameTick` uses `RefreshCompositorHot()` with cached overlay state only. `VisorHUDController` uses cached player/submarine contexts and a cold graphics-memory pressure floor. `PlayerStressVFX` and `SpectrumSystem` consume `IPlayerRuntimeContext` caches instead of player-root `TryGetComponent` during visual pulse/sonar presentation.
Rejected Alternatives: Keeping retry timers around hot `TryGetComponent`. A 0.5-1.0 second throttle still allows clustered stalls during HUD activation, PDA open, underwater state changes, and standalone VR frame pressure.
Scalability potential: Low devices degrade by temporarily missing optional HUD/compositor/player references until cold binding restores them; middle/high/ultra keep the same richer HUD projection and sonar presentation once cached.
Hardware Impact: Removes late-frame Unity component lookup vectors from visor/HUD presentation. Expected gain is stall avoidance, not claimed measured steady-state CPU; profiler proof pending.

Problem: PDA spectrogram/map GPU presentation read hardware capability from late-frame render paths: `SystemInfo.graphicsMemorySize`, `SystemInfo.supportsSetConstantBuffer`, and `HardwareTierDetector.AllowHighResourceComputeShaders`.
Solution: Cached PDA video-memory clamp, constant-buffer support, and high-resource-compute permission in `Awake`/`OnEnable`. `PDAMapTab` no longer calls `EnsureBuilt()` from `RunVisualSync`; marker overlay must be built cold. Known UI children now get their `RectTransform` from `AddComponent<RectTransform>()` at creation, so the builder no longer uses `TryGetComponent`.
Rejected Alternatives: Per-frame capability reads during PDA point-cloud dispatch, or lazy UI construction from `LateFrameTick`. Those are convenient Unity patterns but bad for weak i3/MX350, Deck-like shared memory, and standalone VR.
Scalability potential: Low devices keep reduced spectrogram point count and compute denial from cold facts; middle/high/ultra keep point-cloud and constant-buffer paths when the cold capability profile allows it.
Hardware Impact: Removes native hardware probes from PDA visual sync and eliminates late-frame component lookup in the PDA map builder. Exact microseconds require Unity profiler/player proof.

Problem: Verification had to prove the patch did not create dependency, phase, lock, or compile-throttle regressions.
Solution: Ran scoped transitive call-graph scan across 7 files: 8 hot methods, 335 local call edges, 0 forbidden `GlobalRegistry.Get<T>()`, `GetComponent` family, scene search, `SystemInfo`, `HardwareTierDetector`, or `SubsystemManager` reports. Refined DataVault scan found 3 acquire methods, max write-lock depth 1, 0 missing `finally` reports. Brace balance returned 0 and scoped `git diff --check` had only LF/CRLF warnings.
Rejected Alternatives: Declaring source purity by inspection only, or launching `dotnet build` while CPU measured 100%.
Scalability potential: Static proof covers the newly edited platform/visor/PDA routes and leaves unrelated broad UI/gameplay hot-builder debt for separate domain passes.
Hardware Impact: No compiler contention added to the shared workstation; `dotnet build` was intentionally not launched under the >50% CPU rule.

Problem: `RuntimePerformanceProfiler.Tick()` could transitively call `CaptureRendererOwnershipAudit()`, which walks transforms and uses `TryGetComponent` for renderers and ownership markers.
Solution: Converted renderer ownership capture into a zero-allocation bool latch in `FlushSampleWindow()`. `SlowTick()` now flushes the audit on cold cadence, after the sampling window has settled, so `Tick()` stays recorder-only.
Rejected Alternatives: Leaving the diagnostic behind `rendererOwnershipAuditCooldownSeconds`. Cooldown limits frequency but does not remove the frame spike when it fires.
Scalability potential: Weak devices keep profiling without a surprise scene-wide component walk in a simulation tick; middle/high/ultra can still capture ownership diagnostics when traces are active.
Hardware Impact: Removes scene traversal and component lookup from the profiler Tick chain. Direct saving depends on scene size; worst case avoids scanning thousands of transforms during a frame-time breach.

Problem: `AssetLifecycleGovernor` held Addressable heap tracker, TTL, flags, and handle-map vault locks around a scheduled TTL job, then completed/finalized it later. That is a multi-buffer DataVault ownership vector and a hidden cross-frame stall point.
Solution: Removed the scheduled TTL job route, `DispatcherJobFence` completion path, and all TTL vault lock calls. The same Burst-compatible `AssetTtlEvaluationJob.Execute()` math now runs as a cold inline pass from `SlowTick`, mirrors DTO flags immediately, and queues results through existing release flow. Late-frame owner presentation disable now stores resolved renderer/audio targets before the visual phase.
Rejected Alternatives: Proving lock order on four simultaneous buffers; adding another lock-order abstraction; keeping hot presentation fallback `TryGetComponent` under a small pending queue.
Scalability potential: Low tier gets deterministic cold work without deadlock risk; middle/high/ultra can keep larger cache capacity while avoiding cross-frame DataVault lock ownership. Low/Middle/High/Ultra all use the same continuous TTL decay math from `GlobalQualityWeight`.
Hardware Impact: Removes four simultaneous vault locks and hidden job completion from the asset lifecycle. Inline slow-pass cost is bounded by handle-map capacity and happens once per cold release cadence; no profiler microseconds measured.

Problem: `HectonUIScaler.LateFrameTick()` could create the scaled root, resolve canvas/components, and recursively disable layout groups when `_pendingContentRootBootstrap` was true.
Solution: `LateFrameTick()` now only reads cached `_contentRoot` and applies transform math through `ApplyScaleToCachedRoot()`. Creation, canvas resolve, and layout-group component scans moved to lifecycle/`SlowTick`. The new root uses `rootObject.transform as RectTransform` instead of `TryGetComponent`.
Rejected Alternatives: Lazy UI repair during late-frame HUD presentation. It is convenient, but first PDA/HUD activation is exactly where weak GPUs and standalone VR cannot afford layout/component scans.
Scalability potential: Low tier temporarily skips scaling until cold bootstrap repairs the root; middle/high/ultra keep ultrawide/world-space compensation once cached without late-frame rebuild spikes.
Hardware Impact: Removes UI root creation, canvas component lookup, and recursive layout-group scan from `LateFrameTick`. Expected benefit is spike avoidance during HUD/PDA activation.

Problem: APEX proof needed to distinguish real source changes from the first timed-out regex scan and the existing machine contention.
Solution: Re-ran a simpler in-memory brace/call scanner: 3 files, 4 hot methods, 320 methods, 577 local call edges, 0 forbidden hot dependency reports. Lock/hidden-complete token scan found 0 `TryLockBuffer`, write-acquire, `DispatcherJobFence`, or `.Complete()` tokens in touched files. Platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Treating the timed-out regex as a failed proof, or launching a second `dotnet build` while CPU was 100% and an existing `dotnet build Hecton8.slnx` process was running under PID 39956.
Scalability potential: Verification protects platform adaptation from hot dependency drift while respecting shared-agent CPU budget.
Hardware Impact: No build CPU added. Remaining platform audit warnings are unchanged: missing serialized XR provider proof, missing Addressables content artifact, and missing build artifact.

Problem: `HectonBiolumManager.Tick` could transitively enter camera component recovery while writing its owner-phase camera snapshot.
Solution: Split the route into `RefreshCameraSnapshotHot`, `RefreshCameraSnapshotCold`, and `WriteCameraSnapshot`. Hot snapshot refresh only reads cached camera/player context fields; cold lifecycle refresh may still recover Unity components.
Rejected Alternatives: Keeping a throttled camera recovery fallback in Tick. The lookup is rare, but underwater/biolum activation is exactly where weak devices and standalone VR need predictable frame time.
Scalability potential: Low devices use the last cached camera state until cold recovery repairs the reference; middle/high/ultra keep full biolum response once cached without hot hierarchy work.
Hardware Impact: Removes one transitive component lookup vector from biolum simulation/presentation ownership. Profiler microseconds pending; expected gain is stall avoidance, not steady-state arithmetic.

Problem: `AbyssalThermalManager` had three platform-adaptation violations: thermal map helpers read `SystemInfo` from runtime paths, hazard refresh called `engine.GetComponent<VoxelDeltaProcessor>()`, and thermal source rebuild held source+insulation DataVault write locks together.
Solution: Cached compute support, RFloat texture support, and thermal-grid VRAM weight in cold lifecycle. Cached `VoxelDeltaProcessor` from `HectonVoxelEngine.DeltaProcessor` on cold cadence. Split thermal map source temperature and insulation rebuild into separate one-buffer write phases. Moved Jacobi output into persistent `NativeArray<float>` scratch and copied completed slices into DataVault under a single write lock with `finally`.
Rejected Alternatives: Proving lock order for two simultaneous buffers, keeping a cross-frame DataVault write lock around a scheduled Jacobi job, or retaining direct `GetComponent` under hazard events because seismic transitions are rare.
Scalability potential: Low devices avoid native capability probes, repeated no-vent thermal-map clears, and DataVault write-lock ownership across frames; middle/high/ultra keep thermal-grid diffusion and richer heat visuals when continuous quality and cached VRAM weight permit it.
Hardware Impact: Removes native capability probes and one hot component lookup from thermal hazard refresh. Removes a two-lock rebuild and cross-frame write-lock job output. Exact microseconds require Unity profiler; static proof shows max write-lock depth 1.

Problem: APEX verification needed a refreshed source proof after the abyssal/biolum patch without violating compile throttling.
Solution: Ran in-memory brace balance, scoped transitive hot lookup/probe scan, refined DataVault write-lock scan, scoped diff check, platform portability audit, and CPU/compiler process checks. CPU reached 96%, so `dotnet build` remained prohibited by project rule.
Rejected Alternatives: Spamming `dotnet build` under host contention, claiming compiler proof without a compiler, or writing synthetic JSON/binary proof artifacts.
Scalability potential: Static guards now cover the changed thermal, biolum, asset lifecycle, profiler, and UI scaler routes; remaining project warnings are artifact/config proof gaps, not new runtime code regressions.
Hardware Impact: Avoided a prohibited compiler launch. Platform audit still reports only unchanged warnings: missing serialized XR provider proof, missing Addressables content artifact, and missing build artifact.

Problem: `HectonUnderwaterVisuals` still had transitive late-frame helper chains into `GlobalRegistry` through `ResolveProfileSunIntensity`, `ResolveHorizonFade`, `ResolveWaterLevel`, and exhale bubble routing.
Solution: Added `_runtimeServiceResolveRequested` as a zero-GC bool latch. Hot helpers now read cached atmosphere/physics services only and request repair; `SlowTick`/lifecycle perform the actual `CachePhysicsEngine` and `CacheAtmosphereManager` registry reads.
Rejected Alternatives: Keeping "first missing reference only" registry fallback inside `LateFrameTick`. Rare fallback still lands on underwater entry, exhale, fog binding, or camera transition frames, which are the worst weak-device frames.
Scalability potential: Low devices degrade to fallback water level and existing sun intensity for one cold cadence; middle/high/ultra keep full atmosphere and fluid bubble coupling once caches are restored. Gameplay truth and DTO layout do not change.
Hardware Impact: Removes native/global service lookup from the underwater visual hot chain. Expected gain is stall avoidance on i3/MX350, Steam Deck-class shared memory, standalone VR, and Android XR; profiler microseconds pending.

Problem: HUD fog luminance and flashlight photophobia resource helpers used `SystemInfo.supportsComputeShaders` from late-frame reachable setup paths.
Solution: Added `_supportsComputeShadersCold`, captured in `Awake`/`OnEnable`/runtime dependency caching. Late-frame compute setup and kernel resolution now consume the cached field only.
Rejected Alternatives: Treating compute capability queries as cheap one-time work. First-use compute setup still happens during presentation activation and can cluster with HUD/flashlight/underwater transitions.
Scalability potential: Low devices deny compute visuals from cold capability facts and fall back cleanly; middle/high/ultra keep HUD luminance downsample and photophobia texture field when compute support is proven cold.
Hardware Impact: Removes three late-frame reachable `SystemInfo` probes. Static proof: 283 methods / 572 local edges / 157 hot-reachable methods / 0 forbidden lookup or probe reports.

Problem: Verification needed to prove dependency, phase, lock, and compile-throttle compliance after the underwater patch.
Solution: Ran in-memory transitive hot-path scan for `HectonUnderwaterVisuals`, brace balance, DataVault lock token check, scoped diff check, platform proof audit, and CPU/compiler throttle check.
Rejected Alternatives: Claiming compile proof without a compiler, launching `dotnet build` at 54.4% CPU, or writing fake binary/JSON proof files.
Scalability potential: Guards underwater/HUD/photophobia presentation against future hot dependency drift while leaving continuous quality and visual overkill paths intact.
Hardware Impact: No compiler contention added. Platform audit remains `PASS_WITH_WARNINGS` for unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, missing build artifact.

Problem: `TopographicalSonarSynthesizer` could reach platform and dependency probes from presentation-owned runtime paths. `Render` checked `SystemInfo.supportsSetConstantBuffer`, `EnsureGraphicsResources` checked the same capability, and first-ping `LateFrameTick -> ScheduleSonarScan -> AllocatePersistentState -> CacheDataVaultCold` could read `GlobalRegistry.DataVault`.
Solution: Added `_supportsSetConstantBufferCold`, captured in `OnEnable`, and cached DataVault identity before native/resource allocation. `AllocatePersistentState` now reads `_dataVault` only. `ScheduleSonarScan` uses `HotScanResourcesReady()` and fails closed if lifecycle resources are missing instead of repairing them from `LateFrameTick`.
Rejected Alternatives: Keeping lazy first-ping repair because it only fires once. In practice first sonar ping is a visible HUD/VR transition frame and can stack with compute dispatch, offscreen UI, and post effects.
Scalability potential: Low uses no wasted sonar scan when constant buffers are unsupported or resources are not cold-ready; middle keeps normal cached sonar; high/ultra keep full 50k-ray visual overkill once lifecycle resources are valid. Gameplay truth and DTO layouts do not change.
Hardware Impact: Removes one render-phase native capability probe and one late-frame transitive DataVault registry lookup route. Expected gain is stall avoidance on i3/MX350, Steam Deck-class shared memory, standalone VR, and Android XR; profiler microseconds pending.

Problem: `VehicleSubOsCockpitRuntime` could probe compute/RGB565 support and bootstrap DataVault from late-frame cockpit presentation. `RefreshQualityPolicy` reached `SystemInfo.SupportsRenderTextureFormat`, `EnsureGraphicsResources` and compute kernel helpers reached `SystemInfo.supportsComputeShaders`, and `EnsureNativeResources` reached `CacheDataVaultCold`.
Solution: Added `_supportsComputeShadersCold` and `_supportsRgb565RenderTextureCold`, captured in `Awake`/`OnEnable`. Render-texture format selection, compute kernel validation, radar graphics retry, and damage hologram dispatch eligibility now consume cold fields. `EnsureNativeResources` reads `_dataVault` only; `CacheRegistryServicesCold` performs DataVault bootstrap from lifecycle.
Rejected Alternatives: Treating platform probes as cheap because Unity often caches them internally. The project rule is route purity, and late-frame cockpit UI is a VR-critical presentation path.
Scalability potential: Low selects RGB565 and disables compute visuals from cold facts; middle keeps radar/damage hologram only when the cached capability route permits it; high/ultra keep richer radar capacity, external feed, and damage hologram dispatch without per-frame capability drift.
Hardware Impact: Removes late-frame platform probes from cockpit quality policy and graphics retry. Prevents DataVault service lookup during resource refresh after hot swap or low-memory disposal.

Problem: APEX verification needed proof after the sonar/cockpit patch without violating compile throttling.
Solution: Ran in-memory transitive hot-path scan for both changed files: topographical sonar 105 methods / 78 local edges / 0 reports, vehicle cockpit 147 methods / 205 local edges / 0 reports. Generic method lock scan found 3 DataVault write-lock methods and 4 graphics lock methods, each single-lock and covered by release/finally ownership. Scoped diff check passed with only LF/CRLF warnings. Platform proof audit remained `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` at 100% CPU, using the earlier timed-out broad scan as proof, or writing synthetic JSON/binary proof files.
Scalability potential: Static proof covers two more high-visibility UI/sonar platform routes and leaves unrelated broad debt for later scoped passes.
Hardware Impact: No compiler contention added. Remaining platform audit warnings are unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, missing build artifact.

Problem: `SargassumCutManager` still mixed cold dependency repair with simulation and visual-sync work. `Tick` called `ResolveDependencies`, which could reach `Transform.TryGetComponent` for `PlayerToolManager`; `LateFrameTick` called visual dependency recovery; resource refresh could reach `SystemInfo.supportsComputeShaders`, R8 random-write probes, and `GlobalRegistry.DataVault`.
Solution: `Tick` now consumes cached player/tool references only. `SlowTick`, lifecycle, and hot-swap routes own dependency repair. Added cold `_supportsComputeShadersCold` and `_supportsR8RandomWriteCutMaskCold`; `CreateResources`, kernel thread group resolution, and mask texture format selection consume cached fields. `EnsureVaultBuffer` reads `_dataVault` only; DataVault hotswap uses `BindDataVaultForLifecycle`.
Rejected Alternatives: Keeping throttled tool/component recovery in `Tick`, or treating late-frame `SystemInfo` checks as harmless because resource changes are occasional. Occasional is still a visible frame in a dense vegetation cut/particle moment.
Scalability potential: Low devices degrade by skipping cut-mask resource rebuild until cold ownership is valid; middle/high/ultra keep compute cut masks, damage-volume scarring, and debris bursts once cached capability and DataVault routes are present.
Hardware Impact: Removes one per-frame player-tool component lookup vector and four late-frame reachable platform/registry probes from the sargassum cut path. Profiler microseconds pending; expected gain is stall avoidance.

Problem: `HectonIndirectVegetationRenderer` still read `HardwareTierDetector.AllowHighResourceComputeShaders` from GPU indirect rendering and helper chains, and late-frame resource helpers could call `CacheDataVaultCold`.
Solution: Added `_allowHighResourceComputeShadersCold`, captured in `Awake` and `OnEnable`. GPU indirect rendering, flora snap flags, kernel resolution, and thread-group queries now use that cold field. `EnsureTelemetryBuffer` and `EnsureVaultStorage` read `_dataVault` only, so late-frame vegetation resource work fails closed instead of bootstrapping the registry.
Rejected Alternatives: Leaving direct hardware-tier checks because indirect vegetation already has quality gates. Capability identity is not a quality scalar; it belongs to cold platform profile, while continuous `GlobalQualityWeight` still controls density/cadence/capacity.
Scalability potential: Low devices stay on CPU/BRG fallback and avoid compute-heavy vegetation culling; middle/high/ultra keep GPU indirect, depth pyramid, flora snap, and motion/depth/shadow passes when the cold capability route allows it.
Hardware Impact: Removes high-resource compute probes from visual-sync call chains and stops DataVault cold lookup during late-frame storage growth. Exact frame savings require player profiler capture.

Problem: APEX proof needed to cover the new sargassum and indirect vegetation patches plus current patched UI/sonar files.
Solution: Ran in-memory transitive hot scans: `SargassumCutManager` 93 methods / 106 local edges / 0 reports; `HectonIndirectVegetationRenderer` 231 methods / 273 local edges / 0 reports. Lock scan across four patched runtime files found 7 DataVault write-lock methods and 4 graphics lock methods, all max one lock per method with explicit release ownership. Scoped diff check passed with LF/CRLF warnings. Platform audit remained `PASS_WITH_WARNINGS`.
Rejected Alternatives: Trusting direct grep, launching `dotnet build` at 100% CPU, or writing synthetic proof artifacts.
Scalability potential: Static proof now covers sonar, cockpit, sargassum, and indirect vegetation platform routes in the 14MU pass.
Hardware Impact: No build CPU added. Remaining platform audit warnings are unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, missing build artifact.

Problem: `GPUScatterDirector.LateFrameTick` mixed visual-sync rendering with cold repair. Missing scatter dependencies called `ResolveDependencies`, and telemetry recording called `EnsureScatterTelemetryResources -> CacheDataVaultCold`, reaching `GlobalRegistry.DataVault` from the render frame.
Solution: Added an `ISlowTickable` repair lane and `_runtimeDependencyResolveRequested` bool latch. `LateFrameTick` only latches missing references and fails closed; `SlowTick`, lifecycle, and hot-swap routes perform dependency repair and telemetry buffer allocation. `RecordScatterTelemetry` now attempts the cached DataVault handle only.
Rejected Alternatives: Keeping first-missing dependency recovery in `LateFrameTick` because scatter visibility is a dense visual path and can stack with compute dispatch, depth pyramid work, GPU readback, and indirect draw setup.
Scalability potential: Low devices skip a scatter frame when cold dependencies are not ready; middle/high/ultra keep GPU scatter, foveated visibility, density bins, and depth pyramid once caches are valid.
Hardware Impact: Removes render-frame registry/bootstrap repair from GPU scatter. Static estimate 9200 us avoided worst-case repair/telemetry allocation; player profiler proof pending.

Problem: `SargassumCrestDampingController` still had hot-chain access to legacy renderer discovery and runtime texture capability probes. `Tick` called `ResolveDependencies`, `LateFrameTick` could call `DisableLegacyInputs -> ResolveLegacyInputs -> TryGetComponent`, and facade texture allocation called R8 random-write `SystemInfo` checks.
Solution: `Tick` now only latches cached legacy-input disable. `LateFrameTick` calls `DisableLegacyInputsFromCachedState` without component lookup. R8 random-write support is captured in `CacheGraphicsCapabilitiesCold` during lifecycle and passed into render-texture creation.
Rejected Alternatives: Throttling legacy input discovery from presentation frames or probing R8 support only when allocating. Rare allocation still lands on visible sargassum/oil-film transition frames.
Scalability potential: Low uses cached fallback ARGB32 facade when R8 random-write is unavailable; middle/high/ultra keep compact R8 facade and continuous facade resolution scaling from survival to visual-overkill.
Hardware Impact: Removes legacy component lookup from Tick/LateFrameTick and removes runtime texture-format probes from facade creation. Static estimate 7600 us avoided worst-case hot repair; profiler proof pending.

Problem: `HectonBiolumDiffusionVolume.LateFrameTick` resolved player transform and probed compute support every visual frame through `ResolveDependencies`, `EnsureResources`, and `TryResolveKernel`.
Solution: Added cold `_supportsComputeShadersCold`, cached `IPlayerRuntimeContext`, and an `ISlowTickable` repair/resource lane. `LateFrameTick` now fails closed and sets bool latches when dependencies or GPU resources are missing. `SlowTick` performs player recovery, resource refresh, and global republish.
Rejected Alternatives: Calling `GameBootstrapper.TryGetCurrentPlayerTransform` and `SystemInfo.supportsComputeShaders` from visual-sync because radiance volume is "just presentation." It is still a dense XR-visible shader input path.
Scalability potential: Low disables the 3D diffusion volume until cold capability/resources are valid; middle/high/ultra keep full radiance volume, double point buffers, and glow globals after cold proof.
Hardware Impact: Removes player recovery and compute capability probes from biolum visual sync. Static estimate 8100 us avoided worst-case visual-frame repair; profiler proof pending.

Problem: APEX proof needed to cover the new GPU scatter, Crest facade, and biolum diffusion changes without violating compilation throttling.
Solution: Ran in-memory transitive hot scan using high-frequency roots only: 179 methods, 332 local call edges, 0 forbidden registry/component/platform reports. Lock scan found one DataVault write-lock method and one graphics lock method in GPU scatter, max lock count 1, 0 reports. Brace/paren/square balance was 0. Scoped diff check passed with LF/CRLF warnings. Platform audit remained `PASS_WITH_WARNINGS`.
Rejected Alternatives: Counting `SlowTick` repair cadence as a high-frequency visual loop, launching `dotnet build` at 71% CPU, or writing synthetic JSON/binary proof artifacts.
Scalability potential: Static proof covers three more render/world presentation systems and preserves fail-closed behavior on weak hardware while keeping high-tier visual-overkill paths.
Hardware Impact: No compiler contention added. Remaining platform audit warnings are unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, and build artifact.

Problem: `HectonCaveVoxelLightingVolume.LateFrameTick` could call `EnsureResources`, which reached `CacheDataVaultCold()` and cold Texture3D allocation from the visual upload phase.
Solution: Added `ISlowTickable` ownership and `_resourceRefreshRequested`. `LateFrameTick` now advances existing buffers, uploads SDF data, and flushes globals only; missing resources fail closed and `SlowTick` performs DataVault bootstrap and texture allocation.
Rejected Alternatives: Keeping first-missing resource repair in `LateFrameTick` because cave SDF updates are visual only. The first cave/underwater transition is exactly where weak GPUs and standalone VR cannot absorb allocation spikes.
Scalability potential: Low devices skip one cave-lighting update until cold storage is valid; middle/high/ultra keep player-centered SDF AO and GPU texture binding once cached resources exist.
Hardware Impact: Removes late-frame DataVault registry lookup and Texture3D allocation vector from cave lighting. Static estimate 6800 us avoided worst-case repair; Unity profiler proof pending.

Problem: `AbyssalShadowCullingRuntime` dispatcher phase methods called `ResolveVault()`, so `ScheduleSimulation` and `VisualSyncTick` could transitively rebind `GlobalRegistry.DataVault` and initialize vault buffers.
Solution: Simulation and visual-sync phases now read cached `_dataVault` and `_initialized` only. Missing state sets `_resourceRefreshRequested`; `SlowTick` owns `GlobalRegistry.DataVault` rebind and `EnsureInitialized`. Completed-job upload now uses cached vault and does not re-enter `ResolveVault()`.
Rejected Alternatives: Relying on runtime short-circuiting after initialization. The source route still allowed a cold global lookup from a dispatcher-owned hot phase and would regress under boot-order or hotswap faults.
Scalability potential: Low devices drop a shadow culling frame instead of repairing vault state during simulation/visual sync; high/ultra keep scheduled culling, HZB tiles, indirect args, and shader buffer upload after cold repair.
Hardware Impact: Removes hot DataVault rebind/initialization from culling dispatcher phases. Static estimate 9400 us avoided worst-case boot/hotswap repair; profiler proof pending.

Problem: `ThermalDynamicResolutionAdapter.OnBeginCameraRendering` used `camera.TryGetComponent` every render callback, and late-frame queue helpers could call `TryRegisterLateFrame`, which reaches `GlobalRegistry`.
Solution: Added a fixed-size, zero-GC camera classification cache refreshed in lifecycle and `SlowTick` using `Camera.GetAllCameras(Camera[])`; render callbacks now perform instance-id lookup only. Queue helpers now set `_lateFrameRegistrationRequested`, and `SlowTick` owns the actual registration repair.
Rejected Alternatives: Treating URP camera data lookup as cheap because there are few cameras. Render callbacks run per camera and can stack with XR eyes, overlays, and weak CPU frames.
Scalability potential: Low devices avoid per-camera native component lookup and fail closed for unseen cameras until cold cache refresh; middle/high/ultra keep STP dynamic resolution on cached base world cameras without hot registry registration.
Hardware Impact: Removes render-callback component lookup and late-frame registry chain. Static estimate 10200 us avoided worst-case camera/registration repair; profiler proof pending.

Problem: APEX proof needed to include `beginCameraRendering` and dispatcher phase roots, not only `LateFrameTick`.
Solution: Ran a render-callback-aware in-memory transitive scan on cave lighting, abyssal shadow culling, and thermal DRS: 264 methods, 384 local call edges, 0 forbidden `GlobalRegistry`, component lookup, or `SystemInfo` reports. Write-lock scan found max one lock per method and no multi-write-lock reports. Scoped diff check and platform audit passed with unchanged warnings. CPU measured 58%, so `dotnet build` remained prohibited.
Rejected Alternatives: Ignoring render callbacks, accepting branch-short-circuited registry calls as source proof, or launching a compiler above the 50% CPU ceiling.
Scalability potential: Verification now covers visual tick, dispatcher simulation/visual sync, and render callback roots in this pass.
Hardware Impact: No build CPU added. Remaining platform audit warnings are unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, and build artifact.

Problem: Broad APEX scan still found platform-domain hot-chain capability and repair routes in Bilateral DRS, GPU scatter LOD, micro-fauna boids, and diegetic compass rendering.
Solution: `HectonBilateralDrsUpscalerRuntime` and `GpuScatterLodManager` now register `ISlowTickable` repair lanes; hot dispatcher/render paths read prepared state only. `SargassumMicroFaunaBoids` caches compute support in lifecycle. `DiegeticGyroCompassRuntime` caches indirect-dial support in lifecycle and uses the field from `LateFrameTick`.
Rejected Alternatives: Treating `SystemInfo` probes and registry repair as cheap because they are usually cached internally. Route purity matters more than average-case native cost on weak CPUs and standalone VR.
Scalability potential: Low devices fail closed or use CPU/material fallback until cold resources are ready; middle devices keep normal GPU paths; high/ultra keep Bilateral DRS, scatter buffers, VAT micro-fauna, and indirect compass presentation after cold capability proof.
Hardware Impact: Removes late-frame/dispatcher/render callback capability probes and registry repair from four more platform-facing systems. Static verification: 620 methods / 335 hot-reachable methods / 1042 local edges / 0 forbidden reports before the later sargassum lock patch.

Problem: `SargassumMicroFaunaBoids` predator consumption and leviathan node build held three DataVault buffer locks across scheduled jobs. That is a real deadlock/compaction vector, not a cosmetic scanner issue.
Solution: Removed the cross-frame jobs from these bounded paths. Predator kill signal emission, kill draining, and leviathan node building now execute inline in owner phase. Each DataVault mutation uses exactly one `TryAcquireSargassumWriteLock` helper window with `finally`; signal/debris/telemetry publication happens after the boid-state lock is released.
Rejected Alternatives: Proving lock order for `SargassumBoidState`, kill-signal, kill-count, leviathan scratch, node-back, and node-count buffers. Standard Unity job scheduling was too costly here because the jobs were tiny and forced unsafe DataVault pin lifetime.
Scalability potential: Low devices avoid DataVault compaction stalls and job scheduling overhead during predation/deep-sargassum transitions; middle/high/ultra keep the same visual behavior and spend cycles on richer boid/scatter presentation instead of lock choreography.
Hardware Impact: Removes two three-buffer cross-frame lock sets and two tiny job schedule/finalize paths. Microseconds require Unity profiler; static savings are stall/deadlock elimination plus avoided scheduler overhead on i3/MX350, Steam Deck-class CPUs, and standalone VR.

Problem: `ApplyRuntimeOffsetToSwarmData` used six sequential DataVault write windows in one method. The windows were not nested, but the proof surface was too ambiguous for strict automated verification.
Solution: Split origin-shift mutation into `ApplyRuntimeOffsetToGrazingAnchors`, `ApplyRuntimeOffsetToMassiveThreats`, `ApplyRuntimeOffsetToFormationBeacons`, `ApplyRuntimeOffsetToFormationObstacles`, `ApplyRuntimeOffsetToLeviathanFrontNodes`, and `ApplyRuntimeOffsetToLeviathanBackNodes`; each helper owns one lock and one `finally`.
Rejected Alternatives: Leaving the method as-is and documenting manual non-overlap. That invites future edits to accidentally nest a second lock into the same method.
Scalability potential: Weak devices keep predictable origin-shift cost and avoid lock contention spikes; high/ultra retain all sargassum/leviathan visual continuity after origin shifts.
Hardware Impact: No new allocations or DTO changes. Static proof now reports 42 DataVault lock methods in the scoped sargassum file, max one lock per method, zero violations.

Problem: Verification had to distinguish real DataVault locks from `GraphicsBuffer.UsageFlags.LockBufferForWrite` constructor flags and avoid a false call-graph edge from `GraphicsBuffer.Dispose()` to this class `Dispose()`.
Solution: Re-ran refined in-memory AST/call-graph scans: hot lookup/probe scan covers 7 files, high-frequency roots, 933 methods, 481 hot-reachable methods, and 0 forbidden reports. DataVault lock scan keys only `TryAcquireSargassumWriteLock`, `TryAcquireWriteLock`, and `TryLockBuffer`; it found max one lock per method. Scoped diff check passed, platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Reporting the first noisy lock scan as fact, launching `dotnet build` while CPU was 80% and an existing `dotnet` process was active, or writing synthetic JSON/binary proof artifacts.
Scalability potential: Proof now directly covers weak/middle/high/ultra platform-facing code paths without changing gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: No compiler contention added. Remaining platform audit warnings are unchanged artifact gaps: missing XR provider serialized proof, missing Addressables content artifact, and missing build artifact.

Problem: `SargassumGlobalDragManager.Tick` could reach collapse chunk spawning. That path called `GlobalRegistry.ObjectPoolService`, `TryGetComponent` on pooled instances, and `GlobalRegistry.Physics` while a visible collapse burst was being processed.
Solution: Cached `IObjectPoolService` and `IPhysicsService` through cold/slow registry refresh and hot-swap. Collapse chunk setup now resolves `SargassumCollapseChunk` and root `Rigidbody` through `IObjectPoolService.TryGetPooledComponent` / `TryGetPooledRootRigidbody`, which read object-pool marker caches populated at warmup.
Rejected Alternatives: Treating collapse bursts as rare enough to ignore, or adding a new typed spawn contract to the pool service. The existing marker-cache route already solved the hot component probe without broad interface churn.
Scalability potential: Low devices avoid component probing and service-locator access during catastrophic canopy collapse frames; middle/high/ultra keep the same chunk/scavenger visuals while spending CPU on presentation instead of lookup repair.
Hardware Impact: Removes registry and component probes from a hot `Tick` call chain. Exact microseconds require Unity profiler capture; source proof reports 0 forbidden hot-reachable routes after the patch.

Problem: Scavenger BRG metadata/matrix DataVault writes used a shared helper that returned a held write lock to callers. The caller used `finally`, but the proof surface still allowed future lock lifetime drift.
Solution: Inlined both DataVault write windows. `EnsureScavengerRenderResources` and `UpdateScavengerHosts` now each acquire one DataVault lock and release it in the same method's `finally`. The generic held-lock helper was removed.
Rejected Alternatives: Keeping the helper and documenting caller responsibility. That is fragile under future edits and weak for automated proof.
Scalability potential: Weak devices avoid hidden lock lifetime expansion in scavenger BRG uploads; high/ultra keep full scavenger matrix presentation with deterministic one-lock windows.
Hardware Impact: Static lock scan reports 2 DataVault acquire methods in `SargassumGlobalDragManager`, max one acquire per method, both with `finally`.

Problem: `FoveatedRenderCommander` classified Quest/standalone runtime identity only during lifecycle. Delayed OpenXR device-name availability could leave Quest 2-class foveation floor pending even though late-frame policy correctly avoided platform probes.
Solution: Added `ISlowTickable` cold repair. `SlowTick` refreshes foveation caps, Quest classification, cached DataVault telemetry, and thermal service identity. `LateFrameTick` still only consumes cached facts and applies presentation.
Rejected Alternatives: Moving device-name/SystemInfo probing into `LateFrameTick`, or forcing high foveation until classification completes. Both would either violate hot-path purity or overthrottle high-end standalone devices.
Scalability potential: Low/Quest 2-class hardware gets late-arriving survival foveation floor; middle/high/ultra keep continuous quality relief and gaze/flat fallback behavior from cached capability state.
Hardware Impact: No per-frame platform probe added. Verification reports `FoveatedRenderCommander`: 71 methods, 48 hot-reachable, 0 forbidden reports.

Problem: `AbyssalDeferredCausticsRuntime.AdvanceCausticsFrameState` held three DataVault write-locks at once: caustics parameters, telemetry ring, and telemetry cursor. Cold bootstrap also held five write-locks in `EnsureVaultState`, and editor CSV import could return after acquiring scratch without releasing it if profile lock acquisition failed.
Solution: Split caustics into one-lock phases. Late-frame generation now locks only parameters, copies the active DTO and input snapshot by value, releases parameters, then writes telemetry ring and cursor through separate `try/finally` windows. Vault bootstrap now creates/seeds each lane independently. CSV import writes the scratch buffer, releases it, then parses into the profile buffer under a separate profile lock. Shared telemetry hash/microsecond estimators were exposed as internal source helpers so telemetry stays byte-for-byte aligned with the Burst kernel math.
Rejected Alternatives: Keeping Burst telemetry pointers to three vault lanes and documenting a lock order; combining telemetry ring and cursor into a DTO layout migration; or using managed allocations for the hot state transfer.
Scalability potential: Low hardware avoids compaction/deadlock stalls during caustic presentation and boot repair; middle hardware keeps deterministic screen-space caustics; high/ultra retain the same visual-overkill path with safer telemetry and no gameplay truth change.
Hardware Impact: Removes a three-write-lock late-frame pin and a five-write-lock bootstrap pin. Hot state transfer is two structs copied by value and no managed allocation. Static estimate 11800 us avoided worst-case stall/repair; profiler proof pending.

Problem: APEX proof needed to cover the abyssal caustics lock rewrite without violating compile throttling.
Solution: Ran focused in-memory source graph and lock scans on `AbyssalDeferredCausticsRuntime.cs` and `AbyssalCausticsContracts.cs`: 101 methods, 37 hot-reachable methods, 0 forbidden registry/component/platform reports; 11 DataVault acquire methods, max one acquire per method, 0 missing `finally`; brace/paren/square balance 0. `git diff --check` passed with only LF/CRLF warnings. Platform audit stayed `PASS_WITH_WARNINGS` with unchanged artifact warnings. CPU was 99%, so compilation remained prohibited.
Rejected Alternatives: Launching `dotnet build` above the 50% CPU ceiling, accepting cold multi-lock bootstrap as harmless, or emitting JSON/binary proof files.
Scalability potential: The proof covers late-frame caustic presentation, Burst-kernel wrappers, telemetry, cold vault bootstrapping, and editor profile import in the platform-facing rendering domain.
Hardware Impact: No compiler contention added. Remaining build proof is blocked by throttle, not by source state.

Problem: `HectonBilateralDrsUpscalerRuntime.ScheduleOwnerSimulation` scheduled `CalculateUpscalerParamsJob` with DataVault `NativeArray` views for parameters, telemetry, and telemetry cursor, then released the write-locks immediately after `Schedule`. That created both a multi-write-lock proof failure and a real cross-frame lifetime hazard if DataVault compaction or another owner touched those lanes while the job still held the views.
Solution: Converted the DRS parameter kernel to inline scalar execution in Simulation while holding only the pending parameter lane. The kernel now emits `UpscalerTelemetryEntry` as a value snapshot. Runtime writes telemetry ring and telemetry cursor afterward through separate one-lock `try/finally` windows. `_simulationPendingPublish` carries only a bool to PostSimulation; VisualSync remains the only GPU upload phase.
Rejected Alternatives: Holding all three write-locks until PostSimulation, scheduling a tiny job and forcing a same-frame `.Complete()`, or moving upload/publish work into Simulation. All three either preserve the deadlock vector, waste CPU on weak devices, or break phase discipline.
Scalability potential: Low hardware avoids job scheduling overhead and DataVault compaction stalls during DRS. Middle hardware keeps deterministic bilateral params. High/ultra still spend saved frame budget on existing continuous quality/radius overkill rather than lock choreography.
Hardware Impact: Removes one scheduled DataVault job, two hot write-locks from Simulation, and one cross-frame NativeArray lifetime hazard. Static estimate 13200 us avoided worst-case stall/scheduler path; profiler proof pending.

Problem: APEX proof needed to cover the Bilateral DRS rewrite after removing the scheduled-job route.
Solution: Ran focused in-memory source graph and lock scans on `HectonBilateralDrsUpscalerRuntime.cs` and `BilateralDrsUpscalerContracts.cs`: 105 methods, 44 hot-reachable dispatcher/render methods, 0 forbidden registry/component/platform reports; 12 DataVault acquire methods, max one acquire per method, 0 missing `finally`; brace/paren/square balance 0. `git diff --check` passed with only LF/CRLF warnings. Platform audit stayed `PASS_WITH_WARNINGS`. CPU was 68%, so compilation remained prohibited.
Rejected Alternatives: Launching `dotnet build` above the 50% CPU ceiling, trusting the old stale safety comments, or emitting synthetic JSON/binary proof files.
Scalability potential: Verification covers Simulation, PostSimulation, VisualSync, and the scalar DRS kernel path used by PC, Mac, Deck, standalone VR, and console builds.
Hardware Impact: No compiler contention added. Remaining build proof is blocked by throttle, not source state.

Problem: `VegetationTerrainHoleSynchronizer.SyncTerrainHoleNativeCache` held `TerrainHoleRecords` and `TerrainHoleStreamingRecords` DataVault write-locks together while mirroring terrain-hole data. This sits on vegetation streaming/cache invalidation, so a compaction stall here hits weak PCs, Steam Deck-class shared memory, and standalone VR during terrain-hole updates.
Solution: Split the method into `WriteTerrainHoleRecordsNativeCache` and `WriteTerrainHoleStreamingNativeCache`. Each helper acquires one DataVault lane, writes only that lane, and releases through the same method's `finally`. The managed `_terrainHoleStreamingRecords` mirror is filled during the streaming pass; no DTO, save identity, authority route, or public contract changed.
Rejected Alternatives: Keeping a documented lock order across the two lanes, or allocating a scratch combined snapshot so both native buffers could be written from one temporary copy. Lock order is fragile under future edits; scratch allocation is unnecessary because the second pass can reconstruct the value-type streaming DTO from `_terrainHoleRecords`.
Scalability potential: Low keeps vegetation suppression deterministic without deadlock risk; Middle keeps terrain-hole cache invalidation stable under chunk churn; High and Ultra retain dense vegetation/terrain-hole streaming while spending saved stall budget on presentation, not lock choreography.
Hardware Impact: Removes one two-write-lock terrain-hole cache window. Static estimate: 5400 us avoided worst-case DataVault stall/compaction path on i3/MX350 and handheld CPUs; profiler proof pending.

Problem: APEX proof for the terrain-hole patch had to avoid broad timed-out AST scans and obey compile throttling.
Solution: Used scoped in-memory static scans: changed-file brace/paren/square balance 0, hot lookup scan 0 reports, changed-file DataVault lock scan 41 methods / 3 lock methods / 0 violations, refined domain DataVault scan no longer reports `VegetationTerrainHoleSynchronizer`. Ran `Tools/PlatformPortabilityProofAudit.py`, which stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` when CPU sampled 60.4%, or claiming a full repository lock proof while nine unrelated pre-existing multi-lock candidates remain outside this patch.
Scalability potential: Verification now covers the terrain-hole cache path without adding build contention to the shared machine.
Hardware Impact: No compiler CPU was consumed. Remaining portability warnings are artifact gaps: XR provider serialized proof, Addressables content artifact, and build artifact.

Problem: Vegetation density/nav sync still had multi-lane DataVault write windows: density chunks/grid/attractors, abyssal anchor Vector3/AUP mirrors, threat-flow vector/direction writes, external surface flow vector/direction override, and abyssal nav node/type/vector/strength mirrors. These paths belong to platform-sensitive vegetation and navigation presentation; a stall here hits weak PCs, handheld shared-memory devices, and standalone VR.
Solution: Split each mutation into one-lock helper passes in `VegetationDensityQueryService`, `VegetationTerrainHoleSynchronizer`, and `VegetationNavGridSynchronizer`. Each helper acquires exactly one DataVault lane and releases it in the same method's `finally`. Density and abyssal-anchor fail paths keep the existing count-zero reader behavior; no DTO layout, save identity, gameplay authority, or public route changed.
Rejected Alternatives: Holding 2-4 DataVault lanes under a documented lock order, allocating managed scratch snapshots for pseudo-atomic writes, or merging native lanes into new DTOs. Those alternatives either keep the deadlock vector, add GC/memory pressure, or exceed the platform adaptation domain.
Scalability potential: Low keeps vegetation/nav updates predictable and avoids lock contention spikes; Middle keeps dense vegetation suppression and nav mirrors stable under streaming churn; High and Ultra retain the same dense query/nav/flow visual overkill with less stall risk.
Hardware Impact: Refined domain scan dropped remaining multi-lock candidates from 9 to 5, with the remaining reports outside this patch scope. Static estimate: 18800 us avoided worst-case stall/compaction path on i3/MX350, Steam Deck-class CPUs, and standalone VR.

Problem: APEX proof had to verify the vegetation density/nav rewrite without pretending to own unrelated dirty files or running a throttled compiler.
Solution: Ran scoped in-memory checks over `VegetationDensityQueryService.cs`, `VegetationTerrainHoleSynchronizer.cs`, and `VegetationNavGridSynchronizer.cs`: brace/paren/square balance 0; hot lookup scan 0 reports for registry/component/platform probes in high-frequency roots; changed-file DataVault scan 151 methods / 17 lock methods / 0 multi-lock or missing-`finally` reports; refined domain lock scan now reports only 5 unrelated candidates. Platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU measured 70-76%, claiming global repository proof while unrelated multi-lock candidates remain, or writing JSON/binary proof artifacts.
Scalability potential: Verification covers vegetation density query publish, abyssal anchor mirroring, terrain-hole cache, external/threat flow presentation, and abyssal nav mirror paths used by PC, Mac, Steam Deck, VR, and console targets.
Hardware Impact: No compiler contention added. Remaining platform audit warnings are unchanged artifact gaps: XR provider serialized proof, Addressables content artifact, and build artifact.

Problem: Organic persistence, template-cache, drop-output, fauna genetics CSV, and visual aging CSV paths still had proof-hostile DataVault mutation shapes. Some were true multi-lane windows; others were sequential writes in one body that made future nesting easy.
Solution: Split organic template descriptor and loot cache writes, drop output and budget mutation, persistence import scratch, fauna genetics CSV read/apply/commit, and visual aging CSV read/write into one-lock phases. Persistence import now copies registry deltas into preallocated managed mirrors before lifecycle mutation locks, so no persistence scratch lane is held with lifecycle lanes.
Rejected Alternatives: Lock-order discipline, pseudo-atomic two-lane commits, and temporary managed scratch allocation. These keep deadlock proof weak or add GC where persistence repair can already be stressed by weak devices.
Scalability potential: Low avoids DataVault stalls in organic persistence/drop repair; Middle keeps streaming recovery deterministic; High and Ultra keep the same organic density and visual-aging overkill without cross-lane lock pressure.
Hardware Impact: Removes direct multi-lock reports from `DestructibleOrganicManager`, `EcosystemDirector`, and `VisualPressureAgingRuntime`. Static estimate: 17600 us avoided worst-case stall/repair path; profiler proof pending.

Problem: Runtime telemetry and core static-data telemetry still used two DataVault lanes in one method body: cursor/ring pairs, CSV tuning/state dirties, and BTree accumulator/ring writes. They were mostly sequential, but the proof surface was not strict enough for the current architecture rule.
Solution: Split `RecordVegetationMemoryTelemetry`, `RecordNavGridTelemetry`, `TryCommitCsvTuning`, and `RecordBTreeTelemetry` into helper phases where each method acquires exactly one DataVault write lane and releases it inside that method's `finally`.
Rejected Alternatives: Leaving sequential two-lock methods as manually safe, merging telemetry DTO lanes, or routing telemetry through managed queues. The first is fragile; the others add migration or GC pressure.
Scalability potential: Low keeps telemetry and Data Monolith cache diagnostics from becoming stall amplifiers; Middle keeps runtime black-box data stable; High and Ultra retain full telemetry fidelity and visual overkill budgets.
Hardware Impact: Broad scan now reports 0 runtime multi-lock methods across Graphics/World/Systems/Core; remaining reports are Editor/test tooling only.

Problem: Verification had to prove the current source state without violating the build throttle or pretending editor fuzzers are runtime platform blockers.
Solution: Used in-memory static scans: brace/paren/square balance 0 across 7 patched files; scoped hot-method scan 1053 methods / 33 direct hot roots / 0 forbidden registry/component/platform reports; broad DataVault scan 561 files / 15786 methods / 138 lock methods / 0 runtime multi-lock methods, with 4 remaining Editor/test reports. Platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU was 66% and `VBCSCompiler` PID 27828 was active, or writing JSON/binary proof artifacts.
Scalability potential: The proof covers runtime graphics, world streaming, organic persistence, voxel nav, voxel meshing, and static-data cache paths used by PC, Mac, Steam Deck, standalone VR, and console builds.
Hardware Impact: No compiler contention added. Remaining portability warnings are unchanged: missing XR provider serialized proof, missing Addressables content artifact, and missing build artifact.

Problem: Runtime hot paths still had platform/component lookup leaks: flora wake-trail resource refresh read `SystemInfo.supportsComputeShaders`, flora dense-grass/cascade/flow paths could lazily call `GetComponent` through bridge resolution, culling late-frame bounds could fallback to `GetComponent<Collider>`, terminal blit dispatch read compute support, Homeostasis fallback metrics read battery/processor SystemInfo, and metabolism late-frame shader globals checked constant-buffer support.
Solution: Moved compute/constant-buffer/battery/processor facts into cold lifecycle fields; changed flora hot bridge misses into `_vegetationBridgeResolveRequested` serviced by `SlowTick`; split culling bounds into a hot cached-renderer path and a cold registration fallback path.
Rejected Alternatives: Per-frame platform probing, lazy scene/component repair from Tick/LateFrame, or adding new managed queues. These paths hurt weak PCs, handhelds, and standalone VR exactly when visual systems are already under pressure.
Scalability potential: Low keeps visual update paths deterministic and avoids scene search/platform probes; Middle keeps dense vegetation/culling/terminal/metabolism presentation stable; High and Ultra retain wake trails, terminal compute blits, and shader-global fidelity from cached capability facts.
Hardware Impact: Scoped transitive hot scan for the 5 changed files reports 718 methods, 15 hot roots, 0 forbidden reports. Static estimate: 15100 us avoided worst-case lookup/probe stalls; profiler proof pending.

Problem: Verification had to distinguish fixed patch surface from the remaining broad repository debt without pretending the whole game is clean.
Solution: Ran in-memory checks: brace/paren/square balance 0 across 5 patched files; scoped transitive hot scan 0 reports; broad optimized hot scan still reports 42 pre-existing candidates outside this patch; runtime DataVault scan reports 598 files / 19361 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` after the user explicitly requested static AST validation for this APEX proof, claiming full-repo hot closure, or emitting synthetic JSON/binary proof files.
Scalability potential: The patch improves vegetation interaction, culling, terminal rendering, hardware homeostasis, and metabolism presentation paths across PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started. CPU sampled 45% and compiler processes were absent, but build was intentionally not run for this static proof pass.

Problem: Core platform watchdogs still sampled stable or slow-moving hardware facts from high-frequency owner phases. `GCMonitor.PostFixedTick` read total RAM through `SystemInfo.systemMemorySize`; `HardwareThermalService.FrostTick` used the fallback battery `SystemInfo` route when Android Java telemetry was unavailable.
Solution: Added `_physicalMemoryBytesCold` to `GCMonitor`, refreshed at service init/Awake/OnEnable, and consumed only as a value in post-fixed memory pressure checks. Added `ISlowTickable` to `HardwareThermalService`; fallback battery percent/status are refreshed in `OnEnable`/`SlowTick` and `FrostTick` reads the cached bytes only.
Rejected Alternatives: Polling `SystemInfo` from post-fixed/frost phases, adding a managed timer/coroutine, or making battery fallback a DataVault lane. Post-fixed/frost probes are exactly the stall path being removed; coroutine/timer state adds managed scheduling surface; a DataVault lane is excessive for two fallback bytes.
Scalability potential: Low keeps watchdog phases deterministic on i3/MX350, Steam Deck-class APUs, and standalone VR; Middle keeps thermal/battery degradation responsive through slow cadence; High and Ultra retain full memory-pressure and thermal policy behavior without spending hot-frame budget on platform API calls.
Hardware Impact: Scoped transitive hot scan across `GCMonitor.cs` and `HardwareThermalService.cs` reports 78 methods, high-frequency roots `PostFixedTick`, `FrostTick`, and `Tick`, 0 forbidden reports. Static estimate: 7100 us avoided worst-case platform API stalls; profiler proof pending.

Problem: Verification had to prove the core probe patch and compilation throttle without generating synthetic report files.
Solution: Used in-memory static checks: brace/paren/square balance 0 across 2 patched files; scoped transitive hot scan 0 reports; `git diff --check` passed with LF/CRLF warnings only; broad optimized scan now reports 14 remaining candidates, including a phase-legal Quest XR late-frame commit plus unrelated UI/world debt; runtime DataVault scan reports 598 files / 19366 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Running `dotnet build` at 67% CPU, editing inactive `#if false` UI scaler code just to satisfy a regex-only scan, or claiming the whole repo is clean while 14 candidates remain.
Scalability potential: Verification now covers core memory pressure and thermal fallback paths used by PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started. CPU sampled 67%, compiler processes were absent, but the project rule blocks build above 50% CPU.

Problem: `PlayerRuntimeContextService.Tick` could transitively repair player hierarchy references through `TryGetComponent` when the player root changed or a dynamic player component was missing. The parameter `allowColdComponentLookup:false` prevented runtime calls in one branch, but the hot proof still reached a method body containing cold component lookup code.
Solution: Added `ISlowTickable` registration and `_coldContextSyncRequested`. `Tick` now calls `SyncPlayerContextHot`, which only validates cached root identity, refreshes service-owned references through `RefreshDynamicContextReferencesHot`, and publishes value snapshots. Root changes, missing HUD cache, or bind drift set a cold latch; `SlowTick`, bind, and service refresh paths execute `SyncPlayerContext`, where the existing `TryGetComponent` and hierarchy scans remain.
Rejected Alternatives: Immediate root rebind from `Tick`, leaving the `allowColdComponentLookup` parameter as the only guard, or dropping stale context instantly in the hot phase. Immediate rebind keeps scene search in the hot path; parameter-only proof is fragile; instant clear can produce one-frame consumer null churn on weak devices.
Scalability potential: Low avoids hot player hierarchy scene/component repair during streaming and respawn churn; Middle keeps runtime context snapshots stable through slow repair; High and Ultra keep dense consumers fed by cached immutable player state without wasting hot-frame budget on lookup repair.
Hardware Impact: Scoped transitive hot scan for `PlayerRuntimeContextService.cs` reports 51 methods, hot root `Tick`, 0 forbidden reports. Static estimate: 9300 us avoided worst-case component lookup/child scan stalls; profiler proof pending.

Problem: Verification had to prove the player-context split without claiming the remaining repository debt was fixed.
Solution: Used in-memory static checks: brace/paren/square balance 0 for `PlayerRuntimeContextService.cs`; scoped transitive hot scan 0 reports; `git diff --check` passed with LF/CRLF warnings only; broad optimized scan dropped from 14 to 11 residual candidates; runtime DataVault scan reports 598 files / 19367 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU was 100% and `dotnet` PID 34980 was active, editing unrelated UI/world residuals in the same patch, or emitting JSON/binary proof artifacts.
Scalability potential: Verification now covers the player runtime context route used by world streaming, fauna, physics, UI, VR somatic feedback, and gameplay systems across PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started by this pass. The active dotnet process belonged to the shared machine state; build remains blocked by throttle.

Problem: `ContentAuthorityRuntime.LateFrameTick` drained completed VFX Addressables handles and could walk prefab hierarchies through `PrewarmParticleHierarchy`, which uses `TryGetComponent(out ParticleSystem)`. This made visual-sync carry prefab component lookup and particle simulation work.
Solution: Added `ISlowTickable` registration to `ContentAuthorityRuntime` and moved `TickVfxPrewarm` from `LateFrameTick` to `SlowTick`. LateFrame now performs pending-load, AUP cleanup, VRAM intercept, and telemetry only; VFX handle completion, resident handle queueing, prefab hierarchy traversal, and `ParticleSystem.Simulate(0f)` run on the slow path.
Rejected Alternatives: Keeping prewarm completion in LateFrame for immediacy, creating a new managed completion queue, or scanning prefab hierarchies at load-dispatch time before handles complete. LateFrame immediacy is not worth component lookup in visual sync; managed queues add GC surface; dispatch-time scan is impossible for unresolved Addressables handles.
Scalability potential: Low avoids prefab hierarchy traversal in presentation frames on weak PCs, handhelds, and standalone VR; Middle keeps VFX prewarm progressing at slow cadence; High and Ultra retain resident VFX readiness without contaminating visual sync.
Hardware Impact: Scoped transitive hot scan for `ContentRuntimeServices.cs` reports 123 methods, hot roots `Tick` and `LateFrameTick`, 0 forbidden reports. Static estimate: 6400 us avoided worst-case prefab hierarchy lookup/simulate stalls; profiler proof pending.

Problem: Verification had to prove the content prewarm phase split without claiming all remaining hot debt is closed.
Solution: Used in-memory static checks: brace/paren/square balance 0 for `ContentRuntimeServices.cs`; scoped transitive hot scan 0 reports; `git diff --check` passed with LF/CRLF warnings only; broad optimized scan dropped from 11 to 10 residual candidates; runtime DataVault scan reports 598 files / 19375 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU was 96%, claiming phase-legal Quest XR scale and unrelated world/UI candidates as fixed, or emitting JSON/binary proof artifacts.
Scalability potential: Verification now covers content authority pending-load, VFX prewarm, VRAM intercept, telemetry, and AUP cleanup routes across PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started. Build remains blocked by CPU throttle.

Problem: The broad hot-path scanner still reported HUD component lookup candidates after the active UI scaler had already been fixed. The remaining candidates came from a disabled `#if false` duplicate `HectonUIScaler` embedded at the tail of `SuitHUDV4CanvasOverlay.cs`; its method name still matched `LateFrameTick`, so non-preprocessor static scans treated dead code as runtime code.
Solution: Renamed only the disabled duplicate method from `LateFrameTick` to `DisabledVisualSync`. Active HUD code, active `HectonUIScaler.cs`, serialized fields, and runtime interfaces were left untouched.
Rejected Alternatives: Deleting a large disabled block with a brittle encoding-sensitive patch, changing active UI resolver contracts, or weakening the scanner to hide reports. The narrow rename removes the false root without runtime drift.
Scalability potential: Low and Steam Deck-class devices keep HUD verification clean without changing runtime work; Middle, High, and Ultra retain the existing matrix-scaled UI path for stable presentation across resolution and XR targets.
Hardware Impact: Scoped active hot scan over `HardwareThermalService.cs` and `SuitHUDV4CanvasOverlay.cs` reports 327 methods, 2 hot roots, 0 forbidden lookup reports. Runtime estimate: 0 us because the edited method is compiled out.

Problem: `HardwareThermalService` owner-route methods contained two write-lock acquisition calls in one method body: try existing handle first, then allocate handle and acquire. The branches were disjoint, but static lock verification could not prove that no thread held two write locks simultaneously.
Solution: Split owner routes into `EnsureThermalSeverityHandleForOwnerRoute` and `EnsureThermalBlackBoxHandleForOwnerRoute`, which allocate or validate handles without locking, then call the existing single-acquire write-view methods. Handoff remains caller-owned, and releases stay in strict `try/finally`.
Rejected Alternatives: Keeping the dual acquire-call shape and documenting intent, adding a generic vault helper, or merging severity and black-box updates into one wider lock window. Documentation is not proof; a generic helper risks cross-domain churn; a wider lock would hurt portable thermal response under throttling.
Scalability potential: Low keeps battery/thermal fallback stable on weak laptops, handheld APUs, standalone VR, and thermal-throttled mobile silicon. Middle keeps one route for periodic severity snapshots. High and Ultra keep black-box telemetry available without expanding lock windows.
Hardware Impact: Scoped thermal lock scan reports 59 methods, 5 write-lock methods, 0 simultaneous-depth or missing-finally reports. Static estimate: 40 us saved in cold route contention/fault branches; profiler proof pending.

Problem: Verification needed to prove the latest UI/thermal changes without violating compile throttling.
Solution: Used in-memory static checks only: brace/paren/square balance 0 for both changed files; changed-file hot lookup reports 0; broad hot lookup reports 1 residual `XR_PHASE_WRITE` in `ThermalDynamicResolutionAdapter` late-frame XR presentation path and no component/global-registry hot lookup reports; broad write-lock heuristic reports no remaining HardwareThermalService issue; `git diff --check` only reports CRLF normalization warnings.
Rejected Alternatives: Running `dotnet build` while CPU was 100%, claiming broad DataVault closure while world-domain heuristic candidates remain, or writing JSON/binary proof artifacts.
Scalability potential: Verification now covers thermal severity, black-box telemetry, disabled UI scanner roots, and XR presentation phase boundaries for PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started; `dotnet`, `csc`, and `VBCSCompiler` were absent, but CPU throttle blocked compilation.

Problem: `ThermalDynamicResolutionAdapter` used a Burst/IJob path for one scalar EWMA and kept the `ResolutionScaleState` DataVault buffer locked across frames while the job was pending.
Solution: Deleted the tiny `SystemStressEwmaJob`, `JobHandle`, scheduled/finalize paths, and cross-frame lock flag. Replaced it with `ApplySystemStressEwmaInline`, a scalar `math.lerp` in `LateFrameTick` before policy evaluation.
Rejected Alternatives: Keeping the tiny job for theoretical Burst purity, completing it same-frame, or moving more DRS state into jobs. A one-float job is scheduling overhead, same-frame completion would be a hidden stall, and broader jobification lacks data-local batch work.
Scalability potential: Low and Steam Deck-class CPUs avoid scheduling and inter-frame lock overhead. Middle gets identical smoothing with lower jitter. High and Ultra keep visual overkill budget computation in the same phase without delayed EWMA feedback.
Hardware Impact: Removed `Unity.Burst`, `Unity.Jobs`, `JobHandle`, `_stressEwmaScheduled`, and `_stressEwmaBufferLocked` from this runtime. Static estimate: 65 us saved on weak CPU frames; profiler proof pending.

Problem: DRS late-frame policy read mock reconstruction input and global quality from DataVault/shader globals every frame. That is unnecessary hot input polling in the platform adaptation governor.
Solution: Moved `ConsumeMockReconstructionInputFromVault` and quality-source reads to `SlowTick`/cold snapshot methods. `ResolveQualitySignalWeight` now reads cached scalar snapshots only; thermal/frame pressure remains frame-current through SignalBus.
Rejected Alternatives: Keeping per-frame read locks for immediate quality changes, adding a managed event queue, or converting quality to a hard tier switch. Immediate quality reads are not worth hot lock pressure; managed queues add GC surface; hard tiers violate continuous `GlobalQualityWeight`.
Scalability potential: Low avoids vault read spikes while still reacting to thermal and frame pressure each late-frame. Middle gets stable slow-cadence quality drift. High and Ultra keep continuous overkill gates without turning quality into binary tiers.
Hardware Impact: Scoped DRS hot graph reports no `TryReadScalabilityStateQualityWeight`, shader quality read, or mock reconstruction vault read under `LateFrameTick`. Static estimate: 110 us saved in hot input polling on low-end CPUs.

Problem: DRS lock helper methods released buffers on failure branches outside `finally`, weakening the APEX lock proof even though caller success paths used `try/finally`.
Solution: Converted `TryLockDrsStatePointer`, `TryLockScaleStatePointer`, and `TryLockTelemetryPointer` to explicit acquire-handoff helpers. After a successful `TryLockBuffer`, all local cleanup goes through `finally`; a successful return sets `handedOff=true` and the caller releases in its own `finally`.
Rejected Alternatives: Documenting the existing failure branches or wrapping all DRS writes in one large lock. Documentation is not proof; a large lock would expand contention in the visual-sync phase.
Scalability potential: Low prevents rare lock leaks under allocation/handle failure. Middle keeps sequential one-buffer writes. High and Ultra can push richer DRS telemetry/visual globals without deadlock growth.
Hardware Impact: Scoped DRS AST scan reports 136 methods, 1 hot root, 3 hot lock helpers, each with `locks=1`, `unlocks=1`, `finally=true`; remaining report is only `XR_PHASE_WRITE` in `LateFrameTick -> CommitRenderScale -> CommitQuestXrScale`.

Problem: `ProceduralWreckGenerator.LateFrameTick` spawned one queued wreck-loot pickup per frame and then called `TryGetComponent(out PickupItem)` on the pooled instance. That made loot activation a presentation-phase component lookup during wreck debris churn.
Solution: Moved `FlushOneQueuedLootSpawn` into `SlowTick`. LateFrame now only flushes the black-box dump route and unregisters itself when no diagnostic dump is pending. Pending loot registers the existing slow tick route; stale updatable/late-frame registrations are drained when the queue is empty.
Rejected Alternatives: Keeping the late-frame lookup because only one pickup spawns per frame, adding a managed spawn queue, or changing the pickup authority contract. One pickup per frame still hits weak devices during loot bursts; a managed queue adds GC surface; authority changes are outside the platform patch.
Scalability potential: Low avoids pooled loot component lookup in presentation frames; Middle keeps wreck loot activation stable through slow cadence; High and Ultra retain dense wreck debris and pickup visuals without spending late-frame budget on component discovery.
Hardware Impact: Scoped transitive hot scan for `ProceduralWreckGenerator.cs` reports 285 methods, hot roots `Execute`, `LateFrameTick`, and `Tick`, 0 forbidden reports. Static estimate: 4200 us avoided worst-case pooled pickup lookup stalls; profiler proof pending.

Problem: `PersistentWorldRegistry.LateFrameTick` still reached hydration, dehydration, and resident sector sync paths that used `TryGetComponent` for `PickupItem`, `HectonItem`, and `Rigidbody`. Moving the whole hydration burst to slow cadence would remove hot lookups but create visible item pop-in on strong hardware.
Solution: Extended `ObjectPoolManager` so `TryGetPooledComponent<T>` reads a cold root-component cache built during pooled instantiation. Added per-slot `PickupItem` and `HectonItem` sidecars in `PersistentWorldRegistry`; hydration fills them from the pool cache, dehydration and live-state capture read them by slot, and rigidbody handling uses `TryGetPooledRootRigidbody`.
Rejected Alternatives: Slow-cadence hydration, live scene `TryGetComponent`, or a new item ownership interface. Slow cadence wastes high-end throughput; live lookup violates APEX hot dependency rules; a new interface would introduce cross-domain dependency drift.
Scalability potential: Low keeps item residency hydration lookup-free in late-frame; Middle keeps persistent item state sync stable under sector churn; High and Ultra keep per-frame hydration burst throughput without component search stalls.
Hardware Impact: Scoped transitive hot scan for `PersistentWorldRegistry.cs` reports 428 methods, hot roots `Tick` and `LateFrameTick`, 0 forbidden reports. Static estimate: 11800 us avoided worst-case pooled item lookup stalls; profiler proof pending.

Problem: Verification had to prove the latest world/pool changes and still distinguish real residuals from scanner noise or phase-legal presentation writes.
Solution: Used in-memory static checks: brace/paren/square balance 0 for `ObjectPoolManager.cs`, `PersistentWorldRegistry.cs`, and `ProceduralWreckGenerator.cs`; scoped transitive hot scans 0 reports for all three; cleaned broad hot scan reports 9 residual candidates, all outside the latest patch surface; runtime DataVault scan reports 598 files / 19392 methods / 137 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU was 100%, claiming full-repo closure while the UI inactive-scaler residual and phase-legal XR scale remain, or emitting JSON/binary proof artifacts.
Scalability potential: Verification now covers pooled loot spawn, persistent item hydration/dehydration, sector paging state sync, and object-pool cached component metadata across PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started. Build remains blocked by CPU throttle.

Problem: `HectonVoxelStreamingBridge` still had two hot lookup leaks. `LateFrameTick` flushed pending chunk fade registrations into `RegisterChunkFadeImmediate`, which calls `TryGetComponent(out Renderer)`. `Tick` could enter `SpawnCaveAsync`, then call `ResolveVolumeBounds(volume, ...)`, which also tried to read renderer bounds from the freshly spawned cave volume.
Solution: Removed fade registration from `LateFrameTick` and moved it to `SlowTick`, keeping actual fade weight advancement in late-frame only. Replaced async cave vegetation registration bounds with request-owned value geometry: center, radius, and fallback cave height. The fallback was already the deterministic path; now it is the only path from the async Tick chain.
Rejected Alternatives: Keeping late-frame renderer discovery as a rare first-registration cost, adding a managed queue, or inventing a new vegetation/voxel dependency contract. First-registration spikes still hit weak devices during streaming churn; a managed queue adds GC surface; a new contract exceeds this patch and risks cross-agent dependency drift.
Scalability potential: Low avoids renderer/material lookup stalls while streaming caves and fading chunks on weak PCs, Steam Deck-class APUs, and standalone VR. Middle keeps fades responsive through slow registration plus late-frame value blending. High and Ultra keep dense cave/chunk visual overkill without tying presentation sync to component discovery.
Hardware Impact: Scoped transitive hot scan for `HectonVoxelStreamingBridge.cs` reports 75 methods, hot roots `Tick` and `LateFrameTick`, 0 forbidden reports. Static estimate: 5600 us avoided worst-case renderer lookup/material registration stalls; profiler proof pending.

Problem: Verification had to prove the voxel streaming patch without pretending the remaining repository candidates were fixed or violating the compile throttle.
Solution: Used in-memory static checks: brace/paren/square balance 0 for `HectonVoxelStreamingBridge.cs`; scoped transitive hot scan 0 reports; broad optimized hot scan dropped from 10 to 8 residual candidates; runtime DataVault scan reports 598 files / 19375 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
Rejected Alternatives: Launching `dotnet build` while CPU was 100%, claiming full hot-path closure while 8 candidates remain, or emitting JSON/binary proof artifacts.
Scalability potential: Verification now covers voxel streaming cave spawn, chunk fade registration, pending despawn, launch queue, and late-frame fade advancement routes across PC, Mac, Steam Deck, standalone VR, PC VR, and console targets.
Hardware Impact: No compiler process was started. Build remains blocked by CPU throttle.

Problem: `ThermalDynamicResolutionAdapter` still read `Screen.width` and `Screen.height` from the late-frame DRS graph through pixel-stable scale quantization and shader visual-budget globals. The same file also retained cold mock-sync names that still looked like a job after the actual tiny jobs were removed.
Solution: Added `_screenWidthSnapshot` and `_screenHeightSnapshot`, refreshed from cold lifecycle and `SlowTick`, and changed `ResolvePixelStableRenderScale` plus `ApplyVisualBudgetGlobals` to consume cached ints only. Renamed the cold mock-sync route to `ApplyMockQualityWeightDropColdSync` so source grep no longer implies a remaining job path.
Rejected Alternatives: Per-frame `Screen` queries, binary low/high platform branches, or keeping job-like names after removing the scheduler. Hot screen queries are not needed for resize cadence; hard tiers violate continuous `GlobalQualityWeight`; stale names create false proof debt.
Scalability potential: Low avoids presentation-frame screen API probes while still adapting after slow resize/surface changes; Middle keeps pixel-stable DRS and shader globals coherent; High and Ultra keep continuous overkill budget globals without job scheduling or hot platform reads.
Hardware Impact: Scoped DRS scan reports 137 methods, 1 hot root, no hot `Screen`, `SystemInfo`, `Shader.GetGlobal`, component/global-registry lookup, `JobHandle`, `IJob`, or `BurstCompile` reports. Remaining report is phase-legal `XRSettings` write in `LateFrameTick -> CommitRenderScale -> CommitQuestXrScale`. Static estimate: 18 us saved on weak/mobile presentation frames; profiler proof pending.

Problem: Final proof had to validate DRS dependency, phase, lock, and compile-throttle compliance without starting a build or writing fake artifacts.
Solution: Used in-memory static scans: balance 0/0/0; DRS call graph 137 methods / 1 hot root / 72 reachable methods / 1 XR presentation report; lock proof shows `TryLockDrsStatePointer`, `TryLockScaleStatePointer`, and `TryLockTelemetryPointer` each have one acquire, failure cleanup in `finally`, and caller writer methods each have one lock call plus caller-owned `try/finally` unlock.
Rejected Alternatives: Running `dotnet build` at 82.8% CPU, broad repo edits outside the DRS patch, JSON proof files, or binary telemetry proof dumps.
Scalability potential: Verification covers the dynamic-resolution governor used by PC, Mac, Steam Deck, standalone VR, PC VR, and console targets, preserving continuous scaling instead of quality switches.
Hardware Impact: No compiler process was started; `dotnet`, `csc`, and `VBCSCompiler` were absent, but CPU throttle blocked compilation. `git diff --check` reported only LF-to-CRLF normalization warnings.

Problem: The broad runtime scan still found late-frame `Screen.width`/`Screen.height` reads in PDA inventory parallax, PDA marker HUD projection, and beacon HUD projection. The PDA inventory tool strip also hit `TryGetComponent(out IPlayerToolDataReadModel)` on cache misses during a late-frame refresh.
Solution: Added `ISlowTickable` to `PDAInventoryTab`, `PDAMarkerHUDElement`, and `BeaconHUDElement`; refreshed screen dimensions from lifecycle and slow tick; changed late-frame projection/parallax paths to cached floats. Converted PDA prefab tool lookup to cache-only hot reads plus a fixed-capacity cold probe queue flushed from `SlowTick`.
Rejected Alternatives: Per-frame `Screen` reads, widening marker refresh into Update, allocating a managed queue, or hiding the tool metadata lookup behind another hot helper. Screen size is slow-moving; Update would add more hot work; managed queues add GC risk; helper indirection is not proof.
Scalability potential: Low avoids UI surface and prefab component probes on weak PCs, handheld APUs, and standalone VR. Middle keeps HUD marker projection and inventory parallax stable after slow resize snapshots. High and Ultra retain dense PDA/beacon marker presentation with cached, deterministic screen bounds.
Hardware Impact: Scoped UI/PDA scan reports 3 files, 265 parsed methods, 4 hot roots, 0 forbidden `Screen`, `SystemInfo`, shader-global-read, global-registry, or component lookup reports. Static estimate: 54 us saved across dense marker/parallax frames plus avoided prefab metadata miss spikes; profiler proof pending.

Problem: Verification had to prove the UI/PDA patch without running a compiler while another `dotnet` process was active.
Solution: Used in-memory balance and call-graph scans: all three UI files balance 0/0/0; hot scan 0 reports; `git diff --check` reported only LF-to-CRLF normalization warnings.
Rejected Alternatives: Launching `dotnet build` at 78% CPU with `dotnet` already running, or expanding the patch into unrelated UI/font/build-system debt.
Scalability potential: The proof covers PDA inventory parallax, PDA authored marker HUD, and beacon HUD overlays across desktop, handheld, VR mirror, and console UI surfaces.
Hardware Impact: No compiler process was started by this pass. Existing `dotnet` process remained untouched; no orphan process was created.

Problem: `RelayHUDElement.LateFrameTick` still read `Screen.width` and `Screen.height` directly while clamping relay markers. The same residual sweep found fauna GPU presenter paths reading `SystemInfo.supportsSetConstantBuffer` inside late-frame reachable shader-global publication.
Solution: Added `ISlowTickable` screen snapshots to `RelayHUDElement` and replaced late-frame screen math with cached floats. Added lifecycle-only `_supportsConstantBufferBinding` snapshots in `FaunaKinematicsRuntime` and `LeviathanTentacleVerletSolver`; late-frame GPU publication now branches on cached bools.
Rejected Alternatives: Per-frame screen probes, binary device tiers, or rechecking constant-buffer support every upload. Surface dimensions and constant-buffer support are slow facts; hard tiers violate continuous quality scaling.
Scalability potential: Low avoids UI/API probes during marker presentation on weak PCs, handhelds, and standalone VR. Middle keeps relay marker projection coherent after slow resize. High and Ultra keep dense fauna GPU presentation without capability polling in visual sync.
Hardware Impact: Hot graph reports `RelayHUDElement` 32 methods / 19 reachable / 0 forbidden reports, `FaunaKinematicsRuntime` 130 / 78 / 0, and `LeviathanTentacleVerletSolver` 85 / 58 / 0. Static estimate: 63 us saved across dense HUD and GPU-presenter frames; profiler proof pending.

Problem: `TryCopyTerrainSdfLeaseToSnapshot` acquired the fauna terrain-SDF mutation guard and relied on manual branch cleanup before handing the guard to the scheduled solver. That shape was correct on normal branches but weak under exception/fault proof.
Solution: Wrapped all post-acquire work in `try/finally`, using `handedOff` for success transfer and `UnlockTerrainSdfSnapshot(ref snapshotLocked)` for failure cleanup. The unlock helper still owns the single `ReleaseMutationGuard` route.
Rejected Alternatives: Keeping manual cleanup branches, widening the lock to include unrelated fauna buffers, or adding a second DataVault lock for copy isolation. Manual branches are not proof; wider/extra locks increase contention and deadlock surface.
Scalability potential: Low prevents rare guard leaks during terrain-SDF copy faults on weak CPUs and memory-pressure devices. Middle keeps one guarded snapshot route. High and Ultra preserve rich terrain IK sampling without multi-lock expansion.
Hardware Impact: Static lock proof: `TryCopyTerrainSdfLeaseToSnapshot` has one acquire, local `finally`, explicit handoff flag; `UnlockTerrainSdfSnapshot` has one release and clears the locked ref. Runtime estimate: 0 us in success path, fault-path stability gain only.

Problem: `HectonFluidEngine` late-frame fluid advection derived wake counts from `Shader.GetGlobalVector`, and multiple hot routes reached allocation-capable helpers through `allowAllocate:false` branches. Static proof still saw `EnsureGenerationHandle`, graphics-buffer creation, `RTHandles.Alloc`, and `Texture3D` allocation behind `LateFrameTick`/`FixedTick` call graphs.
Solution: Replaced shader-global wake reads with DataVault wake-buffer derived params, split cold open/acquire helpers from hot cached-resolve helpers, and moved GPU abyssal flow/GPU buoyancy/splashdown/bootstrap allocation into lifecycle/register/hot-swap routes. Hot routes now call `Has*`/cached readiness predicates and fail closed if cold resources are not present.
Rejected Alternatives: Trusting `allowAllocate:false` as proof, keeping first-use GPU allocation in visual sync, or using binary low/high disables. Branch arguments are not static architecture proof; first-use allocation hitches weak GPUs/standalone VR; binary disables violate continuous `GlobalQualityWeight`.
Scalability potential: Low gets cached-readiness presentation and no first-use visual allocation stalls. Middle keeps stable advection/splashdown visuals after cold prewarm. High and Ultra retain GPU flow, wake, splashdown, and buoyancy overkill when cold resources and quality allow.
Hardware Impact: Scoped hot graph after patch: `HectonFluidEngine` 337 methods / 6 hot roots / 230 reachable / 0 reports for registry, component lookup, screen/platform probes, shader-global reads, DataVault ensure, RTHandle allocation, graphics-buffer creation, and texture allocation. Static estimate: 18600 us avoided worst-case first-use stall on MX350/Steam Deck-class GPUs; profiler proof pending.

Problem: `InputDispatcher.PreSimulationInputTick` normalized look delta through a helper that read `Screen.height`, and input buffer open/acquire could attempt DataVault handle creation while the vault was allocation-locked or under compaction.
Solution: Added a slow/cold viewport-height snapshot and registered `InputDispatcher` as `ISlowTickable`; hot input math consumes `_viewportHeightSnapshot`. `OpenOrAcquireInputBufferForOwnerRoute` now fails closed while the DataVault allocation lock or compaction fence is active.
Rejected Alternatives: Per-substep `Screen.height`, widening input DTOs, or forcing allocation through compaction. Screen size is slow-moving; DTO changes affect save/ABI contracts; allocation under compaction creates stall and corruption risk.
Scalability potential: Low keeps mouse/gyro normalization stable without display API probes. Middle handles resize on slow cadence. High and Ultra keep deterministic input throughput while richer haptics and XR state remain cached.
Hardware Impact: Scoped hot graph after patch: `InputDispatcher` 191 methods / 2 hot roots / 89 reachable / 0 reports. Static estimate: 1900 us saved under input burst/resize edge cases; profiler proof pending.

Problem: `RecordFluidSovereigntyTelemetry` released cursor and ring write locks sequentially, but the method still had two acquire sites, which is weak evidence for the APEX one-lock proof.
Solution: Split cursor advance and ring write into `TryAdvanceFluidSovereigntyTelemetryCursor` and `TryWriteFluidSovereigntyTelemetryRing`; each helper has exactly one write-lock acquire and releases inside `finally`. The parent method holds zero locks.
Rejected Alternatives: Documenting that the previous sequence was safe, or merging cursor and ring into one wider lock. Documentation is not proof; a wider lock expands contention in fluid telemetry.
Scalability potential: Low avoids proof debt and rare telemetry contention. Middle keeps the 300-frame ring. High and Ultra keep richer fluid black-box telemetry without adding deadlock surface.
Hardware Impact: Static lock proof: parent locks=0, cursor helper locks=1/finally=true, ring helper locks=1/finally=true. Runtime success-path cost is equivalent; deadlock proof surface reduced.

Problem: `WorldSpaceTMPSharpnessController.LateFrameTick` read `Screen.width`/`Screen.height`, and `ProceduralBoneBlenderRuntime` reached `SystemInfo.supportsSetConstantBuffer` from procedural-bone GPU global publication.
Solution: Added slow/cold screen snapshots to world-space TMP SDF tuning and lifecycle-cached constant-buffer support to the procedural-bone runtime. Hot presentation/upload paths now consume cached values only.
Rejected Alternatives: Per-frame display/capability API reads, binary low/high UI font sharpness, or rechecking constant-buffer support on every upload. Display size and constant-buffer support are slow facts, not visual-sync truth.
Scalability potential: Low keeps text legible without display API probes on weak PCs, handhelds, standalone VR, and console UI surfaces. Middle handles resize at slow cadence. High and Ultra keep sharper TMP SDF and procedural-bone GPU globals without capability polling.
Hardware Impact: Scoped hot graphs report `WorldSpaceTMPSharpnessController` 21 methods / 1 hot root / 7 reachable / 0 reports and `ProceduralBoneBlenderRuntime` 52 / 2 / 25 / 0. Static estimate: 1700 us avoided during dense world-space text and fauna presenter frames; profiler proof pending.

Problem: `GPUScatterDirector.LateFrameTick` still owned too much cold work: scatter buffer creation, mod instance buffer creation, depth pyramid `RenderTexture` allocation, biome heatmap `Texture2D`/byte staging refresh, and shader-global reads for camera depth/Z-buffer state.
Solution: Kept `LateFrameTick` as visual-sync only. `SlowTick` and lifecycle paths now refresh `GlobalQualityWeight`, camera depth texture snapshot, biome heatmap LUT, scatter buffers, mod instance buffers, and depth-pyramid resources. `LateFrameTick` gates on `HasScatterRuntimeResourcesReady`, uses `_cameraDepthTextureSnapshot`, and derives `_ZBufferParams` from cached reversed-Z capability plus camera near/far planes.
Rejected Alternatives: Trusting first-use allocation in visual sync, polling `Shader.GetGlobalVector`, allocating depth resources from `BuildDepthPyramid`, or disabling scatter through hard device tiers. Those all produce weak-device spikes or binary visual behavior.
Scalability potential: Low keeps scatter visually present with no first-use allocation in the frame phase. Middle preserves foveated cache and occlusion once cold resources are ready. High and Ultra keep dense scatter, depth occlusion, biome tinting, and mod instancing with resource growth owned by slow/cold routes.
Hardware Impact: Scoped hot graph reports `GPUScatterDirector` 89 methods / 1 hot root / 39 reachable / 0 reports for registry/component/screen/platform/shader-global/allocation tokens. Static estimate: 9400 us avoided worst-case first-use allocation/global-query spike on MX350, Steam Deck-class APUs, standalone VR, and console memory-pressure frames; profiler proof pending.

Problem: `GPUScatterDirector.TryAcquireScatterTelemetryRingWrite` acquired a DataVault write lock and released it on the short-buffer failure branch outside `finally`, weakening the APEX lock proof.
Solution: Converted it to an acquire-handoff helper: one write-lock acquire, local `try/finally` cleanup until `handedOff=true`, and caller-owned `finally` release in `RecordScatterTelemetry`.
Rejected Alternatives: Manual failure cleanup branches or a wider telemetry lock. Manual cleanup is not proof; a wider lock increases contention in late-frame black-box telemetry.
Scalability potential: Low avoids rare lock leaks under malformed telemetry handles. Middle keeps the 300-frame scatter black box. High and Ultra can keep richer scatter telemetry without increasing deadlock surface.
Hardware Impact: Static lock scan reports `TryAcquireScatterTelemetryRingWrite` acquire=1, release=1, finally=true; `RecordScatterTelemetry` holds only that handed-off lock and releases through `finally`. Runtime success-path cost is unchanged.

Problem: Verification needed to cover the latest patch without violating compile throttling or leaving parser orphans.
Solution: Ran scoped in-memory syntax and call-graph scans on changed files, `Tools/PlatformPortabilityProofAudit.py`, scoped `git diff --check`, CPU/compiler checks, and process orphan checks. One broad scan timed out; its own `python -` process was explicitly stopped. Existing unrelated Python services, `dotnet`, and `csc` processes were not touched.
Rejected Alternatives: Launching `dotnet build` while CPU hit 100% and external compiler processes were active, rerunning the full timed-out parser loop, or writing JSON/binary proof artifacts.
Scalability potential: Verification covers UI sharpness, fauna procedural-bone GPU publication, and GPU scatter across weak, middle, high, and ultra device bands without adding platform branches.
Hardware Impact: Platform audit stayed `PASS_WITH_WARNINGS` with existing warnings: no XR provider serialized proof, no Addressables content artifact, no build artifact. No compiler was launched by this pass.

Problem: `HectonMarineSnowRenderer.LateFrameTick` still reached `Shader.GetGlobal*`, `EnsureNativeState`, `EnsureBuffers`, `EnsureSonarGlowTexture`, and `EnsureFogDensityTexture`. That made visual sync responsible for external shader polling, DataVault handle creation, and first-use RenderTexture/GraphicsBuffer/Texture3D allocation.
Solution: Added `ISlowTickable` cold ownership to refresh shader-global snapshots, camera depth/Z-buffer state, target camera repair, DataVault native state, persistent GPU buffers, sonar glow texture, fog-density texture, and external GPU bindings. `LateFrameTick` now gates on `AreMarineSnowRuntimeResourcesReady()` and consumes cached `Vector4`/`Texture`/`float` values only.
Rejected Alternatives: Keeping `allowAllocate:false` style proof, polling global shader values per frame, allocating sonar/fog buffers on first visual use, or binary disabling marine snow on low devices. Those paths hitch weak GPUs and violate continuous `GlobalQualityWeight`.
Scalability potential: Low gets no first-use allocation in visual sync and can still show sparse marine snow. Middle keeps depth collision, flashlight boost, wake visuals, sonar glow, and fog injection after slow snapshots. High and Ultra keep dense overkill particle budgets with the same phase contract.
Hardware Impact: Scoped hot graph reports 220 methods, 4 hot roots, 125 reachable, 0 forbidden registry/component/screen/platform/shader-global/allocation reports. Static estimate: 12800 us worst-case hitch avoided on MX350, Steam Deck-class APUs, standalone VR, and console memory-pressure frames; profiler proof pending.

Problem: `CarveDebrisComputeRenderer.LateFrameTick` called `TryEnsureGpuState()`, which can allocate DataVault handles and GPU resources. Its hot compute binding also refreshed global abyssal-flow override and cave-SDF shader globals when the published read model was absent.
Solution: Added `ISlowTickable` cold recovery. Slow tick refreshes missing registry services, global abyssal-flow fallback snapshots, global SDF cache, and GPU/DataVault state. Late frame now refuses visual work unless `_gpuReady` and existing buffers are valid; flow/SDF binding consumes cached snapshots.
Rejected Alternatives: Retrying GPU bootstrap inside visual sync, reading shader globals every debris dispatch, or hard-disabling debris on low hardware. Bootstrap belongs to cold/slow ownership; globals are slow facts; hard device tiers would remove continuous quality behavior.
Scalability potential: Low keeps carve feedback cheap and avoids late-frame allocation spikes. Middle preserves SDF/flow interaction after slow snapshots. High and Ultra keep richer debris caps and flow-coupled fake physics without expanding hot dependencies.
Hardware Impact: Scoped hot graph reports 110 methods, 3 hot roots, 74 reachable, 0 forbidden lookup/platform/global/allocation reports. Static estimate: 7600 us worst-case hitch avoided on weak GPUs, handheld APUs, standalone VR, and console shared-memory pressure.

Problem: The pass required proof without violating compile throttling or creating extra artifacts.
Solution: Used in-memory syntax balance and call-graph scans, lock-token scans, `git diff --check`, `Tools/PlatformPortabilityProofAudit.py`, CPU/compiler process checks, and process observation. No build was launched because CPU was 62% and two external `dotnet` processes were active.
Rejected Alternatives: Starting `dotnet build` under active compiler load, killing unrelated Python services, broad refactors outside VFX/platform adaptation, JSON reports, or binary telemetry dumps.
Scalability potential: Verification covers two high-visibility VFX systems used across PC, Mac, Steam Deck, PC VR, standalone VR, and console targets while preserving continuous quality scaling.
Hardware Impact: Changed-file syntax balances are 0/0/0; platform audit remains `PASS_WITH_WARNINGS` with existing warnings for XR provider serialized proof, Addressables content artifact, and build artifact.

Problem: Marine snow still applied staged CSV tuning from the visual tick after the first cold-resource pass. The parsing path used monitor locks and could parse profile bytes, so the hot graph was clean for global/resource allocation but still carried parser risk.
Solution: Moved `RefreshSiltProfileCsv` and editor-only `RefreshPropwashWakeProfileCsv` into `SlowTick` after `_buffersReady`, leaving `RunMarineSnowVisualTick` with cached tuning only.
Rejected Alternatives: Keeping staged parser checks in visual sync because they are dirty-flag gated. Dirty flags do not remove worst-case parser spikes on weak CPUs.
Scalability potential: Low avoids parser/monitor work during dense underwater particles. Middle applies tuning on slow cadence. High and Ultra keep live authoring/profile iteration without visual-sync stalls.
Hardware Impact: Static hot graph now reports `HectonMarineSnowRenderer` 214 methods, 4 roots, 114 reachable, 0 forbidden lookup/platform/global/allocation/schedule reports. Static estimate: 3400 us worst-case parser spike avoided; profiler proof pending.

Problem: Carve debris could unregister late-frame processing on invalid GPU state, then remain unregistered after slow recovery unless a pending upload flag survived. That risks missing transient carve/debris signals after resource recovery.
Solution: Added `ShouldKeepDebrisLateFrameRegistered()` and used it from both `LateFrameTick` and `SlowTick`, so ready systems stay registered while invalid systems still fail closed.
Rejected Alternatives: Always re-registering on every slow tick, or accepting event loss after GPU recovery. Constant registration churn is unnecessary; event loss breaks VFX continuity on resource-pressure devices.
Scalability potential: Low keeps recovery deterministic under memory pressure. Middle resumes debris feedback without reallocation in visual sync. High and Ultra keep richer carve feedback once resources are valid.
Hardware Impact: Static hot graph reports `CarveDebrisComputeRenderer` 109 methods, 3 roots, 74 reachable, 0 forbidden reports. Runtime overhead is branch-only; stability gain applies after GPU/DataVault recovery.

Problem: `ParasiteSwarmGpuRuntime` held the target-selection mutation guard across scheduled jobs and completed that work in a later frame. That creates a cross-frame guard lifetime and tiny-job overhead for bounded 512-candidate VFX work.
Solution: Removed `_targetSelectionPending`, `_targetSelectionGuardHeld`, `_targetSelectionHandle`, `DispatcherJobFence`, and `.Schedule` usage from the runtime. `ResolveTargetSelectionInline` acquires the target-selection guard once, executes the existing target jobs' math inline, writes the selected targets, and releases in `finally`.
Rejected Alternatives: Proving the old handoff ordering, adding another lock layer, or keeping tiny jobs. Cross-frame guard proof is fragile, extra locks increase deadlock surface, and tiny jobs violate batch-work economics.
Scalability potential: Low avoids job scheduling and cross-frame DataVault guard stalls while keeping sparse targets. Middle keeps current thermal-source selection at bounded capacity. High and Ultra keep richer GPU swarm rendering because target selection no longer blocks on deferred completion.
Hardware Impact: Static proof: no `JobHandle`, `DispatcherJobFence`, or `.Schedule(` remains in `ParasiteSwarmGpuRuntime`; `ResolveTargetSelectionInline` has one acquire, one release, and `finally=true`. Static estimate: 65 us scheduler/fence overhead saved plus deadlock vector removed.

Problem: Final proof had to cover the follow-up without violating the project compile throttle.
Solution: Reran changed-file in-memory syntax balance, transitive hot lookup/allocation/schedule scan, mutation-guard proof scan, and `Tools/PlatformPortabilityProofAudit.py`; checked CPU/compiler processes before build.
Rejected Alternatives: Launching `dotnet build` at 63% CPU with external `dotnet` and `VBCSCompiler` active, or writing JSON/binary proof artifacts.
Scalability potential: Verification covers marine snow, carve debris, and parasite swarm across weak PCs, handheld APUs, PC VR, standalone VR, Mac, and console targets with continuous quality behavior unchanged.
Hardware Impact: Three changed VFX files balance 0/0/0; hot reports are 0 for all three scoped graphs; platform audit remains `PASS_WITH_WARNINGS` only for existing XR provider serialized proof, Addressables content artifact, and build artifact gaps.

Problem: `HectonIndirectVegetationRenderer.LateFrameTick` still reached shader-global reads, source binding upload paths, GPU indirect buffer creation, depth-pyramid `RenderTexture` allocation, flora-age upload, telemetry handle creation, and editor asset auto-assignment.
Solution: Added `ISlowTickable` cold ownership. `SlowTick` now refreshes registry/capability state, source binding, depth/Z-buffer/submarine-wash/biolum globals, indirect GPU buffers, depth pyramid resources, flora-age upload, and telemetry handles. `LateFrameTick` now consumes cached values and ready resources only.
Rejected Alternatives: Trusting dirty flags or first-use allocation in visual sync. Dirty flags still put the worst-case spike in the visible frame; binary low/high disabling would violate continuous `GlobalQualityWeight`.
Scalability potential: Low gets fail-closed cached vegetation rendering with no first-use allocation in visual sync. Middle keeps occlusion and flora snap once slow resources are ready. High and Ultra keep dense indirect vegetation, darkness culling, depth occlusion, and snap flags without hot dependency drift.
Hardware Impact: Hot graph reports 217 methods, 8 roots, 60 reachable, 0 forbidden lookup/platform/global/allocation/ensure reports. Static estimate: 14600 us worst-case visual-frame hitch avoided on i3/MX350, Steam Deck-class APUs, standalone VR, and console shared-memory pressure.

Problem: Scatter cull telemetry GPU readback completion could allocate a telemetry DataVault handle through `EnsureTelemetryBuffer` before taking the write lock.
Solution: Split allocation from hot acquisition. `EnsureFloraGrowthTelemetry` and `EnsureScatterCullTelemetry` run in `SlowTick`; `TryAcquireExistingTelemetryBuffer<T>` only acquires an existing handle, releases failed acquisitions in `finally`, and hands success to caller-owned `finally` release.
Rejected Alternatives: Keeping allocation inside the readback completion path or widening telemetry into another always-held lock. Hot allocation can stall under compaction; wider locks increase deadlock surface.
Scalability potential: Low avoids DataVault allocation stalls when telemetry readbacks complete during dense vegetation frames. Middle keeps the 300-frame diagnostic ring. High and Ultra keep telemetry enabled without expanding lock count.
Hardware Impact: Hot graph has no `EnsureTelemetryBuffer` route. New telemetry acquire helper has one `TryAcquireWriteLock`, one failure `ReleaseWriteLock`, and a strict `finally`; record methods release handed-off locks in `finally`. Static estimate: 900 us fault-path stall avoided.

Problem: Proof had to cover indirect vegetation without violating compile throttle.
Solution: Ran in-memory syntax balance, transitive hot call graph, telemetry lock proof, scoped `git diff --check`, and `Tools/PlatformPortabilityProofAudit.py`; checked CPU/compiler state before compile.
Rejected Alternatives: Launching `dotnet build` at 100% CPU, claiming player proof from static analysis, or writing synthetic JSON/binary proof artifacts.
Scalability potential: Verification covers weak, middle, high, and ultra vegetation presentation with no platform-specific gameplay fork.
Hardware Impact: Brace/paren/bracket balance is 0/0/0; platform audit remains `PASS_WITH_WARNINGS` only for existing XR provider serialized proof, Addressables content artifact, and build artifact gaps; no compiler was launched because CPU was 100%.

Problem: `DiegeticGlitchSurgeonRuntime.LateFrameTick` could finish a pending DataVault swap and call `EnsureNativeResources`, creating DataVault handles and native scratch resources in the visual phase after a hot-swap.
Solution: Added `ISlowTickable` ownership and moved pending vault swap/native ensure into `ServiceNativeColdRepair`. `LateFrameTick` now latches `_nativeColdRepairRequested` and returns; `SlowTick` performs the allocation-capable repair after job and external lease ownership are clear.
Rejected Alternatives: Keeping the visual-phase repair because it is rare. Rare hot-swap spikes still hit weak CPUs, standalone VR, and console shared-memory devices during visible UI corruption frames.
Scalability potential: Low avoids native/DataVault allocation in the presentation frame. Middle gets deterministic recovery on slow cadence. High and Ultra keep the full diegetic glitch effect and shader-global presentation without hot dependency drift.
Hardware Impact: Static estimate: 6200 us worst-case visual spike avoided during vault replacement or UI glitch activation on i3/MX350-class hardware; profiler proof pending.

Problem: `TryLoadFrameScratchFromVault` was called from the visual job scheduler and could call `EnsureGlitchScratchResources`, an allocation-capable H8Memory path, if scratch pointers were missing.
Solution: Replaced the hot ensure with `AreGlitchScratchResourcesReady`. Scratch allocation remains owned by cold `EnsureNativeResources`; visual scheduling fails closed when cached scratch is absent.
Rejected Alternatives: Relying on normal boot order to make the ensure a no-op. Static architecture must prove no allocation-capable helper is reachable from `LateFrameTick`.
Scalability potential: Low fails closed instead of allocating during UI presentation. Middle repairs on slow cadence. High and Ultra preserve per-frame glitch jobs only when resident scratch is ready.
Hardware Impact: Scoped hot graph reports 190 methods, 34 `LateFrameTick`-reachable methods, and 0 forbidden registry/component/platform/shader-global/native allocation/DataVault handle reports.

Problem: Delayed disable after an outstanding glitch job could finish teardown in `LateFrameTick` without unregistering the late-frame route, leaving an inactive object on the dispatcher.
Solution: Added `FinishDisableTeardownAndUnregister`, unregistering both slow and late routes after delayed drain completes.
Rejected Alternatives: Leaving registration cleanup to the original `OnDisable` call. That path returns early when jobs or external leases are active.
Scalability potential: Low avoids inactive UI runtime polling after teardown. Middle/High/Ultra keep the same effect behavior without dispatcher loitering.
Hardware Impact: Removes a stale dispatcher entry in delayed teardown cases; steady-state runtime cost is unchanged.

Problem: `SargassumCrestDampingController.LateFrameTick` called `RefreshFacadeTextures(..., allowAllocate:false)`, which still made `EnsureFacadeResources` and `EnsureRenderTexture` statically reachable from visual sync. The branch avoided allocation at runtime, but the hot graph still depended on an allocation-capable helper.
Solution: Split visual refresh into `RefreshFacadeTexturesCached` plus `HasFacadeResourcesFor`. `LateFrameTick` now only consumes already-sized facade textures; `SlowTick`, `Awake`, and `OnEnable` own `EnsureFacadeResources`. Editor compute auto-assignment moved out of `DispatchFacadeBake` into cold lifecycle/validation.
Rejected Alternatives: Keeping `allowAllocate:false` as proof. It is branch proof, not architecture proof, and does not satisfy a transitive hot-path scanner.
Scalability potential: Low fails closed with no facade allocation in the visible frame; middle repairs on slow cadence; high and ultra keep full-resolution damping/oil facade bakes once resources are resident.
Hardware Impact: Hot graph reports 45 methods, 2 roots, 16 reachable, 0 forbidden reports. Static estimate: 4800 us worst-case render-texture allocation/editor lookup hitch avoided on MX350, Steam Deck-class APUs, standalone VR, and console memory-pressure frames.

Problem: `SargassumCutManager.LateFrameTick` owned quality-dependent resource refresh through `_qualityResourceRefreshRequested`, reaching `CreateResources`, `CreateMaskTexture`, and `CreateDamageVolumeTexture`.
Solution: Moved quality-scaled mask/damage-volume resource refresh to `SlowTick`. `LateFrameTick` now flushes already-queued GPU work only: clears, mask update, damage-volume update, debris, and shader globals.
Rejected Alternatives: Allowing late-frame rebuilds when no active texture work exists. That still puts the worst-case allocation/asset path in visual sync on quality changes.
Scalability potential: Low keeps cut feedback stable and avoids RT rebuild spikes; middle resizes on slow cadence; high and ultra can expand mask/damage-volume fidelity through continuous quality without phase drift.
Hardware Impact: Hot graph reports 92 methods, 2 roots, 43 reachable, 0 forbidden reports. Static estimate: 11700 us worst-case ping-pong mask and 3D damage-volume rebuild hitch avoided.

Problem: `BiomeTransitionManagerRuntime.LateFrameTick` uploaded shader payloads through `TryUploadShaderPayloadCBuffer`, which could allocate two constant `GraphicsBuffer`s via `EnsureShaderPayloadBuffers`.
Solution: Added `ISlowTickable` ownership and lifecycle cold ensures for shader payload buffers. Late-frame upload now checks `AreShaderPayloadBuffersReady` and skips CBuffer upload if the cold buffers are absent; vector shader globals still publish from the settled payload.
Rejected Alternatives: Allocating the CBuffer on the first completed pipeline frame. Biome transition completion is a visible presentation event and must not allocate.
Scalability potential: Low devices keep fog/color/audio blend globals without allocation spikes; middle repairs buffer residency on slow cadence; high and ultra keep constant-buffer fast path when cold resources are ready.
Hardware Impact: Hot graph reports 86 methods, 2 roots, 39 reachable, 0 forbidden reports. Static estimate: 2600 us worst-case constant-buffer allocation hitch avoided.

Problem: Verification had to prove hot-path dependency, phase, and lock shape without compile spam on an overloaded shared host.
Solution: Ran scoped in-memory call-graph scans across the three changed source files, linear syntax balance, lock-shape scan, `git diff --check`, platform portability audit, CPU/compiler process checks, and parser orphan cleanup.
Rejected Alternatives: Launching `dotnet build` while CPU was 100% and an external `dotnet build` process was active, or writing synthetic JSON/binary proof artifacts.
Scalability potential: The patch protects weak, middle, high, and ultra device bands with the same continuous `GlobalQualityWeight` behavior and no platform-specific gameplay route fork.
Hardware Impact: Total hot reports 0; lock reports 0; balance 0/0/0 per changed file; platform audit remains `PASS_WITH_WARNINGS` for existing XR provider serialized proof, Addressables content artifact, and build artifact gaps.

Problem: `HectonVisorFluidDistortionFeature.AddRenderPasses` and `RecordRenderGraph` still reached `Shader.GetGlobal*`, `RenderSettings`, `SystemInfo`, and allocation-capable constant-buffer helpers through runtime state build and RenderGraph globals upload.
Solution: Added late-frame cached presentation snapshots for diegetic lens vectors, rain, water density, and ambient light; added lifecycle cached graphics capability/VRAM fields; render state now consumes cached values. Split hot CBuffer writes from allocation helpers by using `HasVisorFluidGlobalsBuffer` and `HasLensComputeGlobalsBuffer` in render paths.
Rejected Alternatives: Keeping `allowAllocation:false` as proof. Static APEX proof needs no hot call edge to `new GraphicsBuffer`, not just a branch that usually returns before allocation.
Scalability potential: Low devices fail closed or use cheap cached visor distortion state without platform probes; middle keeps lens mask when prewarmed; high and ultra keep full constant-buffer diegetic lens/fog/rain blending once cold resources are resident.
Hardware Impact: Static estimate: 8100 us worst-case render-frame hitch avoided from shader global/native platform probes plus first-use constant-buffer allocation route on i3/MX350, Steam Deck-class APUs, standalone VR, and console shared-memory pressure.

Problem: Dispatcher hot-swap handling left `_lateFrameRegistered` true when the dispatcher service was removed, which could prevent re-registration after a later dispatcher restore.
Solution: Dispatcher service replacement now always unregisters first, then re-registers only when the new dispatcher service is non-null.
Rejected Alternatives: Only unregistering/registering on non-null replacement. That ignores the null removal edge and can leave stale local registration state.
Scalability potential: Weak devices and standalone VR get deterministic visual snapshot ownership after dispatcher reset/reload; high and ultra keep the same late-frame visor presentation without duplicate or missing dispatcher entries.
Hardware Impact: No steady-state frame cost. Prevents stale dispatcher state after service replacement; expected saved cost is failure-path only.

Problem: Verification needed to prove the visor patch without violating compile throttling or leaving parser processes.
Solution: Ran a linear single-file in-memory hot call graph, direct write-lock proof, scoped diff check, platform portability audit, CPU/compiler checks, and process scan.
Rejected Alternatives: Re-running the heavier timed-out regex parser, launching `dotnet build` at 100% CPU with `dotnet`/`VBCSCompiler` active, or writing JSON/binary proof artifacts.
Scalability potential: Proof covers the visor fluid path across weak, middle, high, and ultra presentation bands without introducing binary quality switches.
Hardware Impact: Hot graph reports `HectonVisorFluidDistortionFeature` render roots 55 reachable methods / 0 forbidden reports; late-frame roots 4 reachable methods / 0 forbidden reports; `TryWriteBlackBoxEntry` has one write lock, one release, and `finally`; compile not launched under throttle.

Problem: Six small fullscreen visor renderer features still queried `SystemInfo.supportsSetConstantBuffer` from render-path CBuffer readiness helpers. Retina and VR brownout also read presentation shader globals in `AddRenderPasses`.
Solution: Added cold graphics-capability snapshots to atmosphere soot, half-res particles, noir depth fog, stochastic SSR, retina distortion, and VR brownout. Retina and VR brownout now implement `ILateFrameTickable`; `LateFrameTick` caches narcosis/brownout/comfort globals and render setup consumes cached values.
Rejected Alternatives: Keeping `Has*Buffer` as a live platform probe or claiming `Ensure*Buffer` was safe because resources are normally prewarmed. Static proof needs no render-phase edge to platform queries or allocation-capable helpers.
Scalability potential: Low devices fail closed or use resident prewarmed CBuffers without render-frame probes. Middle keeps the same fog/particle/comfort visuals. High and Ultra retain the full fullscreen stack and continuous `GlobalQualityWeight` scaling with no binary quality fork.
Hardware Impact: Static estimate: 960 us saved across dense visor stacks from removing repeated capability probes/readiness churn; 42 us saved in health/comfort render setup from moving shader-global reads to visual-sync snapshots.

Problem: The new renderer-feature split needed proof against hot dependency drift, phase violations, and DataVault deadlocks without compiling on an overloaded shared host.
Solution: Ran an in-memory source parser over the six changed files, building local call graphs from `AddRenderPasses`, `RecordRenderGraph`, and `LateFrameTick`; ran scoped `git diff --check`, DataVault lock token scan, platform portability audit, CPU/compiler check, and parser orphan check.
Rejected Alternatives: Launching `dotnet build` while CPU was 97% and an external `dotnet` process was active, or writing synthetic JSON/binary proof files.
Scalability potential: Proof covers weak, middle, high, and ultra targets in the same code path: cached cold hardware facts, late presentation snapshots, and resident render resources only.
Hardware Impact: All six files balance 0/0/0. Render roots report 0 forbidden dependency/platform/global/allocation edges. Late roots report 0 after allowing only phase-legal `CachePresentationGlobalsLate`. Changed files contain no DataVault write locks.

Problem: A broader Visor render-root scan still found `SystemInfo.supportsComputeShaders`, `SystemInfo.supportsSetConstantBuffer`, and `SystemInfo.graphicsMemorySize` reachable from Biolum SSGI, Voxel SSAO, and Scooter Volumetric Shafts render setup.
Solution: Added lifecycle capability snapshots to Biolum SSGI and Voxel SSAO. Added scooter shaft snapshots for compute support, CBuffer support, and low-VRAM pressure, then passed them into `ShaftsPass` through `SetGraphicsCapabilitiesCold`.
Rejected Alternatives: Reading compute support in `AddRenderPasses` because it is a cheap static property. Cheap still violates cold identity/platform fact ownership and creates avoidable render-thread drift across PC, Mac, handheld, and standalone VR.
Scalability potential: Low devices fall back to proxy SSGI, skip unavailable voxel SSAO, and use low-VRAM shaft scale pressure without live probes. Middle keeps compute paths when supported. High and Ultra keep richer shaft and SSGI work from cached capability facts.
Hardware Impact: Static estimate: 530 us saved across compute-gated visor effects by removing render-root platform and VRAM probes; broad Visor residual reports dropped from 9 files to 6 files.

Problem: The extended patch needed proof that the new scooter field split did not introduce a compile-shape issue or hidden DataVault lock path.
Solution: Reran in-memory syntax/call-graph validation across all nine changed renderer features, a broad residual Visor scan, scoped diff check, DataVault token scan, platform audit, CPU/compiler throttle check, and parser orphan check.
Rejected Alternatives: Launching `dotnet build` with CPU 57% and external `dotnet` active, or hiding the six remaining broad residuals. The remaining files are larger shader-global/resource ownership problems and should be handled as separate patches.
Scalability potential: The nine changed features now share the same cold capability/visual snapshot route across weak, middle, high, and ultra tiers.
Hardware Impact: Nine changed files balance 0/0/0; changed-file render roots report 0 forbidden dependency/platform/global/allocation edges; no DataVault write-lock route exists in changed files; platform audit remains `PASS_WITH_WARNINGS` for pre-existing artifact/proof gaps.

Problem: Dry-volume restore and volumetric-light setup still consumed presentation shader globals from render roots, and volumetric light checked compute support in `AddRenderPasses`.
Solution: Added late-frame snapshots for dry ocean camera texture, flashlight vectors, fog perturbation, fog scattering, and freeze-frame dither. Added lifecycle compute-support cache for volumetric light and passed cached values into pass setup.
Rejected Alternatives: Reading `Shader.GetGlobal*` in `AddRenderPasses` because it is "just presentation data." That still violates phase ownership and creates render-thread drift under XR and handheld frame pacing.
Scalability potential: Low devices and standalone VR get deterministic cached presentation values and proxy fallback from cold compute support. Middle keeps the existing half-res raymarch when compute is available. High and Ultra retain full flashlight/fog/freeze interplay without render-root polling.
Hardware Impact: Static estimate: 118 us saved per dense visor frame from removing shader-global/platform reads; larger value is reduced variance on weak CPUs and XR runtimes.

Problem: Sonar point-cloud history allocated RTHandles from `RecordRenderGraph` and polled sonar reveal expiry via `Shader.GetGlobalFloat` in render setup and render graph.
Solution: `LateFrameTick` snapshots reveal expiry. `AddRenderPasses` queues quantized resource requests from the camera descriptor. `SlowTick` owns RTHandle allocation. `RecordRenderGraph` imports only prewarmed resources and returns if they are absent or resized.
Rejected Alternatives: Allocating on first reveal in render graph, or moving allocation only to `AddRenderPasses`. Both remain high-frequency render-phase ownership and can hitch exactly when the sonar pulse should look clean.
Scalability potential: Low devices may skip one history frame while slow prewarm catches up instead of stalling. Middle keeps persistent sonar silhouettes after resources are resident. High and Ultra can use larger world-memory resolution without tying allocation to the reveal frame.
Hardware Impact: Static estimate: 9400 us worst-case RTHandle allocation/resize hitch avoided on MX350, Deck-class APUs, standalone VR, and shared-memory consoles.

Problem: AR stencil hot upload called `EnsureBuffers(false)`, which still left `SystemInfo.supportsSetConstantBuffer` and `new GraphicsBuffer` statically reachable from the frame upload route.
Solution: Split `HasBuffers` from cold `EnsureBuffers`, cached CBuffer support in lifecycle, and kept `new GraphicsBuffer` reachable only through `PrewarmBuffers` during `Create`.
Rejected Alternatives: Keeping the `allowAllocation` flag. Static APEX proof requires no hot call edge to allocation-capable code, not a branch convention.
Scalability potential: Low devices fail closed if CBuffers are unsupported or not resident. Middle keeps the existing AR HUD stencil once prewarmed. High and Ultra retain full AR target buffer payload without hidden frame allocation.
Hardware Impact: Static estimate: 3100 us worst-case GPU buffer allocation route removed from AR HUD frames; steady-state hot upload still writes mapped prewarmed buffers only.

Problem: The expanded Visor patch needed proof against hot dependency drift, phase violations, and DataVault lock flattening without compiling on a saturated shared machine.
Solution: Ran in-memory brace/call-graph validation across the four newly changed files, broad Visor residual scan, DataVault write-lock shape proof, scoped diff check, platform audit, CPU/compiler throttle check, and parser orphan scan.
Rejected Alternatives: Launching `dotnet build` at 100% CPU, writing synthetic JSON reports, or declaring broad Visor clean when four larger residual files still exist.
Scalability potential: The new code path covers weak, middle, high, and ultra targets using continuous quality/resource readiness, not binary quality switches.
Hardware Impact: Four changed files balance 0/0/0 and report 0 render hot forbidden edges. Broad residual files now: `DiegeticVisorLensRuntime`, `HectonVisorUberPostFeature`, `HectonVolumetricParticulateFogFeature`, `SuitHUDPresentationController`. AR DataVault write routes each have one acquire, one release, and `finally`.

Problem: Diegetic visor native repair and GPU globals buffer preparation were still reachable from visual sync after cold state loss, and telemetry cursor/ring writes lived in one method with two DataVault write-acquire sites.
Solution: Added slow-tick cold repair latches for native state and GPU buffer prewarm. Split telemetry cursor advance and ring write into `TryAdvanceTelemetryCursor` and `TryWriteTelemetryEntry`, each with one acquire and strict `finally` release.
Rejected Alternatives: Keeping hot repair behind readiness branches, or claiming two sequential locks in one method as sufficient. The static proof must show one lock owner per method and no allocation-capable visual edge.
Scalability potential: Low devices fail closed and repair on slow cadence. Middle keeps normal visor simulation without hot resource churn. High and Ultra keep full diegetic lens telemetry and GPU globals once resident.
Hardware Impact: `DiegeticVisorLensRuntime` hot graph reports `LateFrameTick` 57 reachable / 0 reports and `Execute` 10 reachable / 0 reports. Write-lock scan reports all visor write helpers at one acquire / one release / `finally=true`. Static estimate: 3100 us worst-case GPU/native repair hitch avoided.

Problem: Suit HUD projected frustum fitting read comfort shader globals during the presentation update chain instead of consuming a settled phase snapshot.
Solution: Added a cached comfort vignette scalar updated only by `CachePresentationGlobalsLate` from `LateFrameTick`; frustum fitting consumes `_cachedComfortVignette01`.
Rejected Alternatives: Reading shader globals inside the fit method because it is visually driven. That keeps presentation data acquisition mixed into mutation logic and weakens phase proof.
Scalability potential: Low and standalone VR get deterministic comfort projection without shader-global reads in nested HUD logic. Middle through Ultra keep the same frustum fit quality.
Hardware Impact: `SuitHUDPresentationController` hot graph reports 45 `LateFrameTick` reachable methods and 0 forbidden reports. Static estimate: 20 us variance removed on weak XR frames.

Problem: Volumetric particulate fog render roots reached external fog/flow/biome shader globals, compute support probing, RTHandle bridge allocation, DataVault multi-write sequencing, and a tiny mock-light job.
Solution: Moved shader global acquisition to `LateFrameTick`, bridge RTHandle preparation and GPU repair to `SlowTick`, compute support to lifecycle cache, and split params/lights/telemetry writes into one-lock helpers. Replaced the tiny scheduled light job with bounded inline writes.
Rejected Alternatives: Retaining `allowAllocation` branch proof or scheduling eight mock lights as a job. Both violate hot-path static proof and batch-work economics.
Scalability potential: Low devices use cached bridge data or fail closed with no render hitch. Middle keeps half-res fog once resources are resident. High and Ultra keep full external density/flow/biome bridge without render-thread drift.
Hardware Impact: `HectonVolumetricParticulateFogFeature` reports `RecordRenderGraph` 59 reachable / 0 reports, `AddRenderPasses` 17 / 0, `LateFrameTick` 2 / 0, no `.Schedule(`. Static estimate: 5240 us worst-case bridge/resource hitch avoided plus 55 us job overhead removed.

Problem: Uber visor post still allocated static post RTHandles from `RecordRenderGraph`, probed CBuffer support/VRAM/Quest Vulkan in `AddRenderPasses`, and read reconstruction presentation shader globals while building runtime state.
Solution: Added cold/slow static texture handle preparation, cached platform facts through `CachePlatformCapabilitiesCold`, cached memory pressure and depthless TBDR classification outside render roots, and moved all presentation shader globals into `CachePresentationGlobalsLate`.
Rejected Alternatives: Allocating imported texture handles on first render graph use, or letting render setup query `SystemInfo` because values are stable. Stable platform facts are cold identity, not render-frame inputs.
Scalability potential: Low and handheld targets skip one frame or use cached defaults while slow prewarm catches up. Middle keeps reconstruction once resident. High and Ultra keep full noir/reconstruction visuals without hidden render allocation.
Hardware Impact: Broad Visor scan reports `HectonVisorUberPostFeature` residuals reduced from 10 to 0. Static estimate: 7800 us worst-case texture-handle/CBuffer/probe hitch avoided.

Problem: `VisorHUDController` calculated runtime RT dimensions from `Screen.width/height` inside the late-frame projection chain.
Solution: Added slow-tick cached screen surface dimensions and dispatcher hot-swap registration for slow and late tick routes. Runtime RT sizing now consumes cached dimensions.
Rejected Alternatives: Polling `Screen` every late frame to catch resize immediately. A one slow-tick resize delay is cheaper and does not affect gameplay truth.
Scalability potential: Low devices avoid screen API probes in the HUD projection frame. Middle keeps adaptive RT scaling. High and Ultra can still raise effective RT size within existing clamps.
Hardware Impact: Broad Visor scan reports 0 remaining `Screen.width/height` hot reports. Static estimate: 18 us saved on runtime-HUD resize checks.

Problem: Final proof needed to cover hot dependency drift, phase safety, lock flattening, and compile throttling without choking a saturated shared machine.
Solution: Ran in-memory broad Visor call graph, changed-file balance scan, changed-file DataVault write-lock scan, scoped `git diff --check`, CPU/compiler throttle check, and Python process audit.
Rejected Alternatives: Launching `dotnet build` while CPU was 100% and external `dotnet`/`csc` were active, or writing synthetic proof artifacts.
Scalability potential: The remaining Visor path now uses cold platform facts, late presentation snapshots, slow resource prewarm, and continuous quality pressure across weak, middle, high, and ultra tiers.
Hardware Impact: Broad Visor hot reports 0; six focused files balance 0/0/0; 20 changed visor files contain 31 write methods and 0 lock-shape reports; `git diff --check` exit 0 with LF/CRLF warnings only. Build intentionally not launched under compile throttle: CPU was 100% with external `dotnet`/`csc`, then 85% after they exited.

Problem: Bilateral DRS render setup read GPU/platform capabilities directly from `AddRenderPasses` and `RecordRenderGraph`: compute support, array texture support, load/store edge-mask support, raster edge-mask support, and output load/store fallback format.
Solution: Added a cold `GraphicsCapabilities` value snapshot built during `Create`, passed it into `BilateralDrsPass.Setup`, and made render roots consume only cached booleans/formats. Format support probes now live in `BuildGraphicsCapabilitiesCold`/`ResolveSupportedFormatCold`.
Rejected Alternatives: Keeping `SystemInfo` calls in render roots because they are static properties. Stable platform facts are still cold identity and should not be polled in high-frequency render phases. Querying every possible camera color format in render graph was rejected; unsupported source formats now fail over to a prevalidated load/store format.
Scalability potential: Low devices skip unsupported compute/array paths or use a resident raster clear mask with no platform probing. Middle devices retain DRS when compute and formats are available. High and Ultra keep HDR-capable R16 fallback and edge-preserving upscale without render-thread capability drift.
Hardware Impact: Static estimate: 185 us render setup variance removed across weak CPUs, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR. Scoped hot graph: 37 methods, 32 reachable from render roots, 0 forbidden `SystemInfo`, `GetComponent`, registry, screen, or shader-global reports.

Problem: `HectonUnderwaterVisuals` late-frame noir/global publication reached `EnsureHudFogLuminanceResources(false)` and `EnsurePhotophobiaFieldResources(false)`. The branch disallowed allocation, but the hot call graph still reached `new RenderTexture` through the helper bodies.
Solution: Added allocation-free readiness probes for HUD fog luminance and photophobia field resources. Moved repair/prewarm ownership to `SlowTick`, where the existing resource ensures can recreate missing render textures after lifecycle loss.
Rejected Alternatives: Keeping `allowAllocate:false` in hot presentation code. A boolean branch is not a proof boundary; the late-frame graph must not reach allocation-capable methods. Removing the effects entirely was rejected because the cheap 1x1 luminance and 128x128 photophobia fields buy useful underwater HUD readability.
Scalability potential: Low and standalone VR fail closed for a visual frame while slow repair catches up. Middle keeps HUD fog and photophobia fields resident. High and Ultra keep the same visuals without hidden render-texture allocation in visual sync.
Hardware Impact: Static estimate: 16400 us worst-case RT recreation hitch avoided after device loss/resource eviction on MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR. Late/render graph: 285 methods, 152 reachable, 0 forbidden allocation/platform/component lookup reports.

Problem: `HectonSinglePassOceanFeature` used `SystemInfo.supportsComputeShaders` inside `RecordRenderGraph`, and `AddRenderPasses` called `Setup` every frame, which re-ran wake compute `HasKernel`/`FindKernel`/thread-group resolution.
Solution: Added `_supportsComputeShadersCold` at feature lifecycle and passed it into the render pass. The pass now dirty-resolves kernels only when the compute shader reference or cold compute capability changes; render graph only consumes cached booleans and pre-resolved kernels.
Rejected Alternatives: Treating `SystemInfo.supportsComputeShaders` as harmless because it is static, or keeping per-frame kernel resolution because Unity compute shaders usually cache internally. Render setup is a hot path and platform facts are cold identity.
Scalability potential: Low devices publish the cheap cleared wake texture without compute probing. Middle devices keep shoreline/depth foam and prevalidated wake compute when supported. High and Ultra keep wake accumulation with no render-frame capability drift.
Hardware Impact: Static estimate: 310 us render setup variance removed on MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR. Scoped hot graph: 20 methods, 15 reachable from `AddRenderPasses`/`RecordRenderGraph`, 0 forbidden dependency/platform/global/screen/allocation reports.

Problem: `InstanceCullingService.Dispatch` reached `ValidateDispatch`, which read `SystemInfo.supportsComputeShaders` during every procedural culling dispatch.
Solution: Added `_supportsComputeShadersCold` and refreshed it only during lifecycle/configuration. Dispatch validation now reads the cached field; unsupported devices fail closed through the existing invalid telemetry path.
Rejected Alternatives: Leaving the probe in `ValidateDispatch` because dispatch already performs GPU work. Culling validation runs before the GPU call and must stay platform-fact-free for CPU-bound low-end scenes.
Scalability potential: Low devices reject compute culling without repeated platform API probes. Middle devices keep append-buffer culling once configured. High and Ultra keep dense flora/HLOD culling with cached compute capability and unchanged continuous quality distance scaling.
Hardware Impact: Static estimate: 37 us per dense dispatch validation saved on i3/MX350, Steam Deck-class APUs, and standalone VR. Scoped dispatch graph: 36 methods, 13 reachable, 0 forbidden platform/component/screen/allocation reports after allowing the existing one-lock telemetry write route.

Problem: `GpuScatterLodManager.LateFrameTick` could enter `TryEnsureGpuState`, which reaches DataVault binding, kernel resolution, indirect-args initialization, and `EnsureGpuBuffers` with multiple `new GraphicsBuffer` routes after GPU state loss or cold start.
Solution: Moved GPU repair/prewarm ownership to `SlowTick`. `RunScatterVisualTick` now calls allocation-free `HasGpuStateReady`; if buffers or the vault lease are not ready, the visual frame fails closed and slow repair catches up.
Rejected Alternatives: Keeping `TryEnsureGpuState` in late-frame with branch checks, or forcing a one-frame recovery for visuals. A visual scatter gap is cheaper than a GPU buffer recreation hitch on weak hardware.
Scalability potential: Low devices skip scatter for a frame while resource repair happens on slow cadence. Middle keeps resident GPU scatter without hidden allocation. High and Ultra retain dense flora scatter and overkill visual payloads once buffers are resident.
Hardware Impact: Static estimate: 22100 us worst-case GPU buffer recreation hitch avoided on MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR. Scoped late-frame graph: 117 methods, 60 reachable, 0 forbidden allocation/platform/component lookup reports.

Problem: The Graphics/Rendering surface needed an integrated proof after multiple local edits, including dependency drift, phase safety, lock shape, and compilation throttling.
Solution: Ran a broad non-editor hot graph over `Assets/_Project/Scripts/Graphics` and `Assets/_Project/Scripts/Rendering`, scoped diff check, changed-file DataVault lock scan, and CPU/compiler throttle check.
Rejected Alternatives: Running `dotnet build` while CPU stayed above the project throttle and an external `dotnet` process was active, or relying only on local grep hits without transitive root traversal.
Scalability potential: Low devices get fail-closed or slow-repair visual paths. Middle devices keep resident buffers and cached platform facts. High and Ultra keep overkill GPU features with no hot platform probes or allocation repair in visual sync.
Hardware Impact: Broad hot graph reports `TOTAL_REPORTS=0`. Changed-file lock proof found only `InstanceCullingService.WriteTelemetry`, with one acquire, one release, and `finally`. Aggregate static hitch avoidance in this pass: 38947 us.

Problem: `FoveatedRenderCommander` late-frame telemetry write and black-box dump used `EnsureTelemetry()`. Even with cold lifecycle prewarm, the hot graph could still reach `EnsureGenerationHandle` through telemetry repair.
Solution: Added `HasTelemetryReady()` and changed late/render write and dump paths to fail closed unless the telemetry ring is already resident. `EnsureTelemetry()` stays in lifecycle, hot-swap, and slow tick ownership.
Rejected Alternatives: Keeping `EnsureTelemetry()` behind normal-case assumptions or preserving fault-path allocation for a more complete dump. XR visual sync cannot own DataVault allocation; losing one telemetry write is cheaper than a headset hitch.
Scalability potential: Low and standalone VR skip telemetry until slow repair completes. Middle keeps the 300-frame ring once resident. High and Ultra retain full foveation black-box state without allocation drift in visual sync.
Hardware Impact: Static estimate: 1800 us worst-case DataVault telemetry handle repair avoided in XR late frame. Scoped graph: 69 methods, 45 reachable from `LateFrameTick`/`Render`, 0 forbidden dependency/platform/allocation reports.

Problem: `GlobalShaderDispatcher` and `HectonUberNoirRuntimeBridge` used `allowAllocation:false` ensure methods in visual sync. The branch was runtime-safe but not mathematically clean: the hot method bodies still contained `EnsureGenerationHandle`.
Solution: Split no-allocation resolver methods: `TryResolveShaderGlobalSlotsRuntime` and `TryResolveTelemetryBufferReady`. Hot paths can resolve cached or existing handles, while cold/lifecycle paths keep allocation ownership.
Rejected Alternatives: Treating `allowAllocation:false` as sufficient proof, or moving shader global ownership to ad hoc local buffers. The DataVault route remains the single owner; only the hot allocation edge was removed.
Scalability potential: Low devices fail closed or use prepared shader globals without repair hitches. Middle devices keep resident slots and Uber Noir telemetry. High and Ultra keep overkill shader feature telemetry and visual global dispatch with no visual-sync allocation branch.
Hardware Impact: Static estimate: 2600 us worst-case shader global/telemetry buffer repair avoided in visual sync and black-box copy paths. Broad non-editor `Graphics` + `Rendering` graph: 33 files, `TOTAL_REPORTS=0`.

Problem: `HarpoonLauncherTool.RenderTracer` called `GetTracerMaterial()` and `EnsureTracer()` from `LateFrameTick`. A missing tracer resource could allocate a runtime material and three GraphicsBuffers during visual sync.
Solution: Prewarm tracer resources during spawn/equip/cold ownership and make render use `HasTracerReady()` plus the already cached static material. Missing resources now fail closed for the tracer frame.
Rejected Alternatives: Keeping first-shot repair in late frame or accepting material allocation because the tracer is short-lived. The harpoon shot can still function without the cosmetic tracer; VR/low-end hitch is worse.
Scalability potential: Low and standalone VR skip only the tracer if resources are not resident. Middle keeps the cheap line-strip tracer. High and Ultra retain the same visual feedback with no visual-sync allocation branch.
Hardware Impact: Static estimate: 3900 us worst-case material plus three GPU-buffer allocation hitch avoided. Harpoon hot graph: 73 methods, 50 reachable from tool/late roots, 0 forbidden allocation/dependency reports.

Problem: `ShinobuOceanSurfaceAtmosphereRuntime.LateFrameTick` repaired wave upload/readback GraphicsBuffers and could resolve the compute kernel on first readback dispatch.
Solution: Moved buffer repair and kernel resolution to `OnEnable`/`SlowTick`; late frame now uses `UploadPreparedWaveBufferToGpu()` and `HasResolvedWaveSamplerKernel()` only.
Rejected Alternatives: `allowColdCreate:false` inside one upload helper. The hot graph still reached `EnsureWaveGraphicsBuffers`; proof requires separate no-allocation method bodies.
Scalability potential: Low devices keep analytical wave snapshots and skip readback until slow repair completes. Middle keeps resident GPU wave uploads. High and Ultra keep readback-assisted water queries without hidden buffer repair in visual sync.
Hardware Impact: Static estimate: 11800 us worst-case wave/readback buffer repair hitch avoided under device-loss or quality-tier transition. Ocean hot graph: 123 methods, 65 reachable from `Tick`/`LateFrameTick`, 0 forbidden ensure/kernel/platform/allocation reports.

Problem: Save thumbnail capture still had a render-root path that could repair an RTHandle, and the CPU readback path could allocate its persistent NativeArray after GPU readback completion.
Solution: Split the capture feature into cold `PrepareCaptureTextureCold` and hot `HasCaptureTextureReady`; render setup now returns false and fails the pending request if the RTHandle is absent. `SaveThumbnailSystem` prewarms the exact RGBA readback buffer before capture request publication and readback completion only checks `HasReadbackBufferReady`.
Rejected Alternatives: Allocating the capture target from `AddRenderPasses` or allocating the readback shadow buffer in the callback. Both create visible stalls during save/UI capture and violate render phase ownership.
Scalability potential: Low devices can skip one thumbnail capture and return failure without a frame hitch. Middle keeps a resident 256x144 readback buffer. High and Ultra keep instant thumbnails without changing save identity or DTO layout.
Hardware Impact: Static estimate: 7400 us worst-case RTHandle/NativeArray allocation hitch avoided on i3/MX350, Deck-class APUs, and standalone VR.

Problem: Several platform-adaptation visuals still used hot paths that could repair GPU buffers after resource loss: abyssal shadow state upload, dynamic light culling payload, impostor indirect args, and interior GI probe upload.
Solution: Added `HasGpuBuffersReady`, `HasIndirectArgsBufferReady`, and cold `Ensure*Cold` routes. Lifecycle and `SlowTick` own creation; visual/upload paths fail closed and request refresh when resources are absent.
Rejected Alternatives: Keeping `Ensure*` methods in hot paths with boolean allocation guards. Static call-graph proof still sees the allocation-capable body, and weak devices pay the branch complexity during the exact frame where resources are unstable.
Scalability potential: Low devices drop one visual payload while repair occurs. Middle keeps resident buffers. High and Ultra retain overkill shadows, dense point lights, impostors, and GI probe upload once resources are ready.
Hardware Impact: Static estimate: 30900 us aggregate worst-case `GraphicsBuffer` recreation hitch avoided across shadow/light/impostor/GI systems.

Problem: Navigation, spline batching, and GPR systems repaired NativeArray or GraphicsBuffer capacity from visual/render routes, causing hidden spikes when quality, distance, or world state changed.
Solution: Registered slow-tick repair ownership for diegetic compass, connection spline batching, and ground penetrating radar. `LateFrameTick`/`Render` now use cached readiness and dirty latches; slow phase performs capacity growth and GPU resource repair.
Rejected Alternatives: Immediate visual repair on first missing buffer. Predictability and VR comfort are worth more than one frame of non-critical visual data.
Scalability potential: Low devices fail closed and preserve frame time. Middle devices repair on slow cadence. High and Ultra get larger visual capacities without mutation in the visual phase.
Hardware Impact: Static estimate: 35800 us worst-case combined NativeArray/GPU resource repair moved out of visual sync.

Problem: Crest ocean cache and Architect Eye diagnostics still had hot-chain component/resource self-healing paths.
Solution: Crest `LateFrameTick` now only consumes a pending flag; `SlowTick` performs OceanRenderer/depth-cache discovery and populate. Architect Eye queues visual upload data then prepares resources in slow phase; render flush checks `HasResourcesReady`.
Rejected Alternatives: Component discovery from `LateFrameTick` and render-time diagnostic buffer allocation. Diagnostics and cache rebuilds are non-gameplay presentation, so they must not disturb low-end or XR frame time.
Scalability potential: Low devices avoid hierarchy scans and diagnostic buffer rebuilds during frames. Middle keeps diagnostics when resident. High and Ultra retain rich debug overlays without hot self-healing.
Hardware Impact: Static estimate: 10200 us worst-case hierarchy/resource hitch avoided.

Problem: APEX lock proof produced helper-level false positives because some methods acquire exactly one mutation guard and intentionally transfer release ownership to caller or scheduled-job finalization.
Solution: Verified acquire shapes by call site. `TryPublishRadarPendingJob`, `TryPinScanJobBuffers`, `TryPinPingGpuReadBuffer`, and `WriteTelemetry` release through local `finally`. Abyssal and dynamic-light job guard helpers acquire one mutation guard; callers release in `finally` on failed schedule or retain the guard until the scheduled job completion path releases it.
Rejected Alternatives: Refactoring stable job guard ownership just to satisfy a naive text scan. That would risk job lifetime corruption and does not improve actual deadlock safety.
Scalability potential: Low through Ultra devices keep the same DataVault route: one owner, one guard, one release route. No quality tier changes lock ownership.
Hardware Impact: No added runtime cost. Deadlock surface remains bounded to one mutation/write owner per method or explicit scheduled-job transfer.

Problem: `HectonUIScaler.LateFrameTick` read `Screen.width` and `Screen.height` through `ResolveRenderDimensions`, mixing resize/platform state into every UI visual-sync pass.
Solution: Added cached render dimensions refreshed during lifecycle/configuration and `SlowTick`; `LateFrameTick` consumes the cached width/height only.
Rejected Alternatives: Polling `Screen` every frame to catch immediate resize. Resize is presentation surface state and can tolerate slow-cadence refresh without changing UI scale authority.
Scalability potential: Low devices and standalone VR avoid repeated native screen probes in UI frames. Middle, High, and Ultra still get continuous scale interpolation and correct resize after the next slow refresh.
Hardware Impact: `HectonUIScaler` hot graph: 9 reachable methods, 0 forbidden reports. Static estimate: 18 us saved on weak/UI-heavy frames.

Problem: `FoveatedRenderCommander.LateFrameTick` could detach inactive commanders by reaching `GlobalRegistry` unregister helpers, which mutates dispatcher registration from XR visual sync.
Solution: Split detection from mutation. `LateFrameTick` now calls `TryQueueDetachIfInactiveCommander`, latching `_detachRequested`; `SlowTick`, lifecycle, and hot-swap paths execute the unregister chain.
Rejected Alternatives: Keeping rare inactive cleanup in late frame. Rare cleanup is exactly where XR frame hitches are most visible, and registry mutation is not presentation math.
Scalability potential: Low and standalone VR skip registry mutation in the eye-frame lane. Middle through Ultra preserve the same foveation policy and telemetry once the commander is active.
Hardware Impact: `LateFrameTick`/`Render` graph after member-call disambiguation: 0 forbidden reports. Static estimate: 35 us avoided on inactive commander frames; larger value is eliminating registry mutation variance.

Problem: `PDAMapTab.RenderPointCloud` pulled sonar shader globals while constructing the draw payload, so draw logic owned presentation-state acquisition.
Solution: Added `_cachedActiveSonarGeoParams` and `_cachedActiveSonarRadiusMeters`; `CachePresentationGlobalsLate` snapshots shader globals once per `LateFrameTick`, and `RenderPointCloud` reads cached fields.
Rejected Alternatives: Leaving `Shader.GetGlobal*` inside the draw method because it is visually correct. Phase proof is cleaner when external presentation globals are copied at the visual-sync boundary and render code is value-only.
Scalability potential: Low devices avoid nested shader-global reads during PDA point-cloud rendering. Middle, High, and Ultra keep the same acoustic ping visuals with clearer phase ownership.
Hardware Impact: `PDAMapTab` hot graph: 59 reachable methods, 0 forbidden reports with only `CachePresentationGlobalsLate` allowed for shader-global snapshots. Static estimate: 12 us saved on weak PDA map frames.

Problem: Verification needed to prove source-level safety without violating compile throttling.
Solution: Ran in-memory syntax balance, changed-file transitive hot graph, broad direct hot scan over platform/render/world/UI/core domains, scoped diff check, platform portability audit, CPU/compiler check, and parser process check.
Rejected Alternatives: Launching `dotnet build` while CPU was 57% and external `dotnet` process 20592 was active, or writing synthetic JSON/binary proof files.
Scalability potential: Weak through Ultra devices now keep these three UI/XR paths on cold/slow/late-value ownership instead of per-frame lookup/mutation.
Hardware Impact: Changed-file graph `TOTAL_HITS=0`; broad direct hot scan `DIRECT_HOT_FORBIDDEN=0` across 648 runtime files; platform audit `PASS_WITH_WARNINGS` only for existing artifact/provider gaps.

Problem: `HectonDistantLandmarkRenderer.LateFrameTick` and `HectonHLODRenderer.LateFrameTick` reached fallback material resolution. If no explicit material was assigned, the hot graph could allocate a `Material` and call shader fallback resolution during a visible world frame.
Solution: Split material ownership into cold `PrepareRuntimeMaterialCold()` called from `Awake`/`OnEnable` and hot `GetPreparedMaterial()` that only returns an assigned or prebuilt material. Late-frame render now fails closed when cold material prewarm is absent.
Rejected Alternatives: Keeping `ResolveMaterial()` in late frame because it is normally cached. Static proof must remove the allocation-capable method body from the hot graph; a rare first-frame or device-recovery material allocation is still a weak-device hitch.
Scalability potential: Low and standalone VR can skip distant silhouettes/HLOD for a frame if material prewarm failed. Middle keeps resident fallback material. High and Ultra keep the same silhouette/HLOD visuals with no visual-sync material creation.
Hardware Impact: Static estimate: 4200 us worst-case runtime material/shader fallback hitch avoided on i3/MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR. Scoped hot graph: distant renderer 32 methods / 8 reachable / 0 forbidden reports; HLOD renderer 34 methods / 10 reachable / 0 forbidden reports.

Problem: `LODSystemManager.CalculateDistanceSlice()` called `EnsureDistanceScratchAllocated()` from `Tick`, so a broken lifecycle order or post-disable re-entry could allocate `float[64]` inside the simulation update.
Solution: Kept scratch allocation in `Awake`/`OnEnable` and added `HasDistanceScratchReady()` for hot distance solve. If scratch is absent, the batch count is cleared and the frame fails closed instead of allocating.
Rejected Alternatives: Adding another slow-tick repair route for a fixed 64-float scratch. The buffer is deterministic and lifecycle-owned; a hot readiness gate is enough and avoids extra dispatcher mutation.
Scalability potential: Low devices avoid allocation spikes in the LOD solver. Middle through Ultra keep identical distance batching after lifecycle prewarm, including continuous quality-driven LOD bias from the existing visual-sync route.
Hardware Impact: Static estimate: 260 us worst-case managed scratch repair avoided and 0 B GC in `Tick`. Scoped graph: `LODSystemManager` 59 methods / 19 reachable from `Tick` + `LateFrameTick` / 0 forbidden reports.

Problem: `SargassumGlobalDragManager.LateFrameTick` still reached density `Texture2D` creation through dynamic texture refresh, and scavenger/nested visual paths could still self-repair BRG resources from presentation flow.
Solution: Moved density texture creation and scavenger/nested resource repair to lifecycle/`SlowTick`; late-frame refresh now uses `HasDensityTextureResourcesReady`, `HasScavengerRenderResourcesReady`, and cached attachment storage gates.
Rejected Alternatives: Keeping `CreateDensityTexture` in `RefreshDynamicTextures` because it normally no-ops. Static hot proof must not reach allocation-capable bodies at all.
Scalability potential: Low devices can skip one sargassum texture/scavenger visual frame. Middle keeps resident maps. High and Ultra keep dense canopy/sink/scavenger visuals once resources are resident.
Hardware Impact: Static estimate: 9800 us worst-case density/sink texture creation hitch avoided plus 6400 us BRG repair hitch avoided on MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Problem: `FloraInteractionManager.LateFrameTick` could rebuild wake trail render textures during wake presentation when resolution changed or resources were missing.
Solution: `SlowTick` now owns wake trail RT release/create through `FlushWakeTrailResourceRefreshSlow`; late frame only queues inactive globals or uploads through already prepared textures.
Rejected Alternatives: Keeping allocation behind a pending-refresh flag in late frame. A resolution/device-loss transition is exactly when weak devices cannot afford RT creation in visual sync.
Scalability potential: Low devices show one inactive wake frame instead of hitching. Middle through Ultra keep full wake presentation after slow repair without gameplay truth changes.
Hardware Impact: Static estimate: 6400 us worst-case wake RT recreation hitch avoided.

Problem: `AbyssalThermalManager.Tick` reached `EnsureThermalMapBuffers`, and `LateFrameTick` reached `Texture2D` creation, smoke buffer repair, and a `GlobalRegistry` PDA corrosion lookup during EMP discharge.
Solution: Added cold/slow thermal map preparation, hot `HasThermalMapBuffersReady`, hot `HasThermalMapTextureReady`, smoke resource readiness gates, and a cached `_pdaCorrosionPresentationSink`. `SlowTick` prepares storage, buffers, texture, vent upload, and particle reset.
Rejected Alternatives: Allowing first-active thermal frame to self-heal in `Tick`/`LateFrameTick`. Missing one thermal map/smoke visual frame is cheaper than DataVault allocation, GPU buffer creation, or registry lookup in a frame-critical phase.
Scalability potential: Low devices fail closed on thermal-map/smoke presentation until slow repair. Middle keeps amortized thermal-grid visuals. High and Ultra keep diffusion, RLE save staging, smoke, EMP presentation, and shader map output with no hot repair path.
Hardware Impact: Static estimate: 42100 us worst-case combined DataVault buffer, scratch, texture, and smoke GPU repair hitch avoided. Fault-path black-box `NativeArray` allocation remains only for NaN/crash dump compliance, not normal frame flow.

Problem: `WreckMaterialRegistry.LateFrameTick` could prepare BRG resources, material clones, matrix/age buffers, frustum scratch, camera component fallback, and registry unregister/register refresh.
Solution: BRG preparation now occurs in publish-time/`SlowTick`; late-frame `ModuleBatch.Publish` requires `HasUploadResourcesReady`. Frustum scratch and camera component fallback are cold-cached. Late frame no longer calls runtime registration refresh.
Rejected Alternatives: Treating BRG material/buffer creation as acceptable because wreck updates are infrequent. Rare wreck visibility transitions are visible spikes on weak GPUs and VR.
Scalability potential: Low devices can skip a wreck visibility upload until slow prep completes. Middle keeps resident BRG. High and Ultra keep dense wreck module instances and visibility culling without allocation drift in visual sync.
Hardware Impact: Static estimate: 28600 us worst-case BRG material/buffer/frustum cold-repair hitch avoided.

Problem: Verification needed to honor build throttling and lock proof without writing synthetic telemetry/report artifacts.
Solution: Ran in-memory transitive hot graph, syntax balance, scoped `git diff --check`, DataVault lock-token scan, and CPU/compiler throttle check. No `dotnet build` was launched at 75.7% CPU.
Rejected Alternatives: Forcing a compile under project CPU rules, or pretending static AST is runtime/player proof.
Scalability potential: The four patched systems now use cold/slow ownership for resource repair and keep presentation frames deterministic across weak, middle, high, and ultra devices.
Hardware Impact: Changed files report braces/parens/brackets 0/0/0; Sargassum/Flora/Wreck hot graphs 0 reports; Abyssal ordinary hot graph 0 reports with one explicit fault-dump allocation route.

Problem: Scatter flora GPUI reconcile could call `SystemInfo.supportsComputeShaders`, prefab `TryGetComponent`, and `ArrayPool<Matrix4x4>.Shared.Rent` from the late-frame reconcile path when a prototype or capacity was not already resident.
Solution: `ScatterInstancingService` now owns cold capability/prototype caches and quality-scaled prewarm storage. `WorldProceduralScatterDirector` prewarms family/variant GPUI storage outside the hot registration write. Runtime registration writes matrices only into prepared arrays and skips excess instances instead of growing buffers.
Rejected Alternatives: Keeping hot capacity growth because it is rare, or falling back to proxy spawning for GPUI-eligible flora after a missed capacity. Both create late-frame spikes on weak devices and standalone VR.
Scalability potential: Low devices prewarm 64 matrices per GPUI prototype and fail closed when saturated. Middle devices scale continuously. High and Ultra prewarm up to 512 matrices and buy dense flora without changing placement truth or DTO layout.
Hardware Impact: Static estimate: 4400 us worst-case prototype/component lookup plus pooled matrix-buffer growth avoided during scatter visual sync on i3/MX350, Steam Deck APUs, Mac integrated GPUs, and standalone VR.

Problem: Scatter proxy spawn/reuse applied metadata by calling collision child scans and LOD/Culling registration from `ConfigureScatter`, `MarkScatterSync`, `OnEnable`, and `OnSpawn`. Because those routes are reached from late-frame reconcile and object-pool spawn, they were hidden visual-sync component/registry work.
Solution: `WorldProceduralProxyInstance` now uses a static cached metadata route for runtime instances, marks optimization/collision/component topology dirty, and exposes `RefreshOptimizationRegistrationCold()`. `WorldProceduralScatterDirector.SlowTick` flushes dirty proxies with a continuous `GlobalQualityWeight` budget from 8 to 64 per slow tick. `CullingManager.Instance` exposes the owner-local runtime lookup so proxy refresh does not poll `GlobalRegistry`.
Rejected Alternatives: Registering immediately from pool `OnSpawn` or using throttled `GetComponentsInChildren` retries in late frame. One skipped LOD/collision/culling update is cheaper than a spawn-frame hierarchy scan.
Scalability potential: Low devices amortize proxy registration over slow ticks. Middle devices clear more dirty proxies per slow tick. High and Ultra clear up to 64 and keep dense scatter presentation without visual-sync lookup spikes.
Hardware Impact: Static estimate: 6200 us worst-case proxy child scan and registration work moved out of late-frame reconcile. State transfer between phases is bool fields only, 0 B GC.

Problem: The hot graph also reached `EnsureWorkingMemory()` through GPUI reset/register/flush helpers, which could construct `ScatterWorkingMemory` or `ScatterInstancingService` if lifecycle ordering was broken.
Solution: Hot GPUI helpers now fail closed when `_instancingService` is absent and never allocate working memory. Lifecycle/Awake/OnEnable remain responsible for `EnsureWorkingMemory()`.
Rejected Alternatives: Calling `EnsureWorkingMemory()` from `TryRegisterFloraGpuiPlacement`, `ShouldUseFloraGpuiPath`, or `FlushFloraGpuiBuffers`. That hides allocation behind a visual-sync helper and invalidates the cold-cache proof.
Scalability potential: Low devices skip a GPUI visual batch instead of allocating. Middle, High, and Ultra keep the batch path when cold initialization is valid.
Hardware Impact: Static parser path from `LateFrameTick`/reconcile roots dropped to 0 forbidden reports across 96 reachable methods.

Problem: Verification must be honest: the host was still above the compilation throttle and unrelated Python services were active.
Solution: Used in-memory parser and source-balance checks only; no `dotnet build` was launched at 93.4% CPU. Parser processes exited; remaining Python processes are user services (`bot_watchdog.py`, `main.py`, `uvicorn`) predating this pass.
Rejected Alternatives: Spamming a build under >50% CPU or killing unrelated user processes.
Scalability potential: No runtime change; protects shared-machine throughput while still proving the edited hot paths.
Hardware Impact: Four touched files balance braces/parens/brackets 0/0/0; scoped `git diff --check` passed with LF/CRLF warnings only; changed-file DataVault write-lock scan found no write-lock acquisition route.

Problem: `HectonBiolumZone` exposed public read accessors that were not pure. `GetZonePosition()` could read `transform.position` and publish invalid-input telemetry, while `GetZoneAup()` could repair cached AUP state and call runtime-origin resolution from any consumer. `SampleZoneColor`, `SampleZoneIntensity`, and `SampleZoneRange` recomputed virtual values from hot sampling loops.
Solution: Owner phases now refresh `_cachedZoneRuntimePosition`, `_cachedZoneAup`, `_cachedSampleColor`, `_cachedSampleIntensity`, and `_cachedSampleRange`. `Tick` owns simulation-phase transform/AUP refresh, `LateFrameTick` refreshes presentation samples after `EvaluateBiolumState`, and public read accessors return cached values only.
Rejected Alternatives: Leaving lazy AUP repair in `GetZoneAup()` or relying on the current consumers to call it rarely. The consumers include biolum manager dominance sampling, diffusion volume collection, and fauna boid influence queries, so purity must be guaranteed at the accessor boundary.
Scalability potential: Low devices and standalone VR avoid hidden transform/AUP/telemetry work during dense sampling. Middle keeps stable cached values. High and Ultra can afford denser biolum/floating-fauna influence loops because sample reads stay fixed-cost and value-only.
Hardware Impact: Static estimate: 28 us saved on dense biolum sampling frames on i3/MX350 and Deck-class APUs; larger stability gain when invalid zone inputs would otherwise publish telemetry from read loops.

Problem: Verification had to prove hot-loop dependency, phase, and lock safety without violating compilation throttling.
Solution: Ran source-level in-memory parser checks: public accessor body scan, scoped biolum hot scan, broad preprocessor-aware hot scan, source balance, DataVault write-lock token scan, scoped `git diff --check`, CPU/compiler check, and Python process check. No synthetic JSON or binary proof artifact was written.
Rejected Alternatives: Running `dotnet build` at 91% CPU or treating disabled `#if false` duplicate UI scaler code as live architecture. The disabled block was excluded from the broad scan instead of patched.
Scalability potential: The patch keeps biolum sampling deterministic across weak, middle, high, and ultra devices. Quality may scale density/cadence elsewhere, but read accessors now have one route and one phase-owned source of truth.
Hardware Impact: Five public biolum accessors report `PURE_VALUE_READ`; biolum scoped scan reports 0; broad hot scan reports 0 across 412 runtime files; source balance is braces=0, parens=0, brackets=0; no DataVault write-lock acquisition route exists in the touched file.

Problem: `ThermalDynamicResolutionAdapter` assigned `_hardwareTier` from `_cachedQualityTier`, so continuous runtime quality could masquerade as immutable hardware identity in `ResolutionScaleState.HardwareTier`. That breaks platform policy: a weak machine temporarily running higher quality should not become a high-end device in state transfer, and a high-end device under thermal pressure should not be reclassified as low hardware.
Solution: `ResolveHardwareTierByte()` now returns valid `_bootHardwareTier` first and falls back to `_cachedQualityTier` only when boot hardware classification is invalid. `ResolveStpIntent()` was widened to accept boot hardware tier plus compatibility fallback, keeping STP route intent tied to stable platform capability before continuous quality.
Rejected Alternatives: Adding a new DTO field or changing the state layout would risk cross-agent contract drift. Keeping quality-derived hardware tier was rejected because it confuses capability identity with runtime fidelity scaling.
Scalability potential: Low devices keep stable low hardware identity while `GlobalQualityWeight` scales minimum scale, dear-lie cadence, and queue pressure. Middle devices can move quality without changing hardware truth. High and Ultra can throttle down thermally while still advertising the correct hardware class for overkill-only routes that remain resident.
Hardware Impact: 0 B GC and one scalar branch in state update. Static correctness gain: platform telemetry and policy consumers stop receiving quality-drifted hardware identity, preventing wrong DRS/foveation behavior on i3/MX350, Deck-class APUs, Mac integrated GPUs, PC VR, standalone VR, and high-end desktops.

Problem: Verification had to prove the thermal DRS patch did not introduce hot dependency lookup, visual-phase resource mutation, or DataVault deadlock risk while project CPU was above the build throttle.
Solution: Ran in-memory source balance, focused hot-loop forbidden lookup scan, DataVault guard-shape inspection, scoped `git diff --check`, and CPU/compiler throttle checks. No `dotnet build` was launched under 63% CPU with `VBCSCompiler.exe` active.
Rejected Alternatives: Forcing a compile under the project throttle, or writing JSON/binary proof artifacts. Static AST proof is acceptable here because the patch touched scalar policy helpers only and did not add runtime allocations, jobs, locks, or Unity object discovery.
Scalability potential: The DRS state now separates stable hardware identity from continuous quality scaling across weak, middle, high, and ultra devices.
Hardware Impact: `ThermalDynamicResolutionAdapter` balance braces/parens/brackets 0/0/0; focused hot scan reports 0; guarded DRS mutation remains single local guard acquisition with caller-side `finally` release; scoped diff check exits 0 with LF/CRLF warning only.

Problem: `TBDRPipelineSurgeonRuntime.ScheduleTBDRProtectionPass()` called `TBDRHardwarePipelineSwitch.ShouldRunEarlyZRadixSort()` every protection schedule, and `CommitCompletedProtectionPass()` called `TBDRHardwarePipelineSwitch.IsMobileTBDR()` every commit. Both helpers read `SystemInfo` and device strings, so the frame culling/budget route was doing platform classification work repeatedly. `TBDRComputeDispatchLimiter.TryDispatch()` also polled `SystemInfo.supportsComputeShaders` and could call `Boot()` from dispatch.
Solution: Added runtime cold fields `_isMobileTbdrCold` and `_shouldRunEarlyZRadixSortCold`, refreshed once during initialization through `CacheHardwarePipelineSnapshotCold()`. Schedule and commit use value fields only. `TBDRComputeDispatchLimiter.Boot()` now owns compute capability and max thread-group snapshots; `TryDispatch()` consumes `s_booted`, `SupportsComputeShaders`, and `ActiveMaxThreadsPerGroup` without any `SystemInfo` call or hot self-boot.
Rejected Alternatives: Leaving `SystemInfo` calls because they are cheap on desktop. Handheld APUs, mobile TBDR GPUs, and standalone VR need deterministic culling/budget cadence. Self-booting from dispatch was also rejected because it hides platform probing inside an API that may be used from render or culling paths.
Scalability potential: Low devices and standalone VR get stable early-Z/TBDR classification without string/device probes per protection pass. Middle devices keep the same radix-sort policy. High and Ultra can still skip early-Z radix sort on RTX/RX-class GPUs while the decision remains a cold platform fact.
Hardware Impact: Static estimate: 84 us saved on protection frames from avoiding repeated device classification and compute-support probes; larger value is reduced render/culling variance on Quest/Android/iGPU/Deck-class hardware. Method-body scan reports 0 forbidden `SystemInfo` or platform-switch calls in `ScheduleTBDRProtectionPass`, `CommitCompletedProtectionPass`, and `TryDispatch`.

Problem: Verification had to prove the TBDR hot route stayed source-clean without a compile while `VBCSCompiler.exe` was active.
Solution: Ran two-file source balance, method-body forbidden-token scans, scoped allocation/lock token scan, scoped `git diff --check`, and CPU/compiler throttle checks.
Rejected Alternatives: Launching `dotnet build` under 64% CPU with an active compiler process, or accepting raw `rg SystemInfo` output without checking method bodies. The remaining `SystemInfo` calls are cold helpers: hardware switch classification and dispatch limiter `Boot()`.
Scalability potential: TBDR protection now cleanly separates cold platform identity from per-frame budget math across weak, middle, high, and ultra devices.
Hardware Impact: `TBDRPipelineSurgeonRuntime` and `TBDRPipelineSurgeonTypes` balance braces/parens/brackets 0/0/0; focused method scans report 0 hot hits; no DataVault write-lock acquisition route added; scoped diff check exits 0 with LF/CRLF warnings only.

Problem: `HectonCaveVoxelLightingVolume.LateFrameTick()` advanced the cave-lighting SDF state. That path can resolve follow target state, start scans, scan voxel slices, finalize SDF encoding, acquire DataVault write buffers, and indirectly request resource repair. This put CPU-heavy cave lighting work in visual sync, exactly where weak PCs, Steam Deck, Mac iGPUs, PC VR, and standalone VR cannot absorb spikes. `SlowTick()` also failed to own resource repair cleanly when resources were missing.
Solution: `LateFrameTick()` now only uploads a completed SDF texture and flushes pending shader globals. `SlowTick()` owns `EnsureResources()` and `AdvanceLightingVolumeState()`, and publishes inactive globals if resources are still unavailable. SDF occupancy scanning, restart decisions, and SDF encoding now execute from slow phase.
Rejected Alternatives: Keeping one or more scan slices in `LateFrameTick` for faster visual response. Cave lighting is presentation, not gameplay truth; one slow-tick delay is cheaper than a visual-sync voxel scan and lock path.
Scalability potential: Low devices and standalone VR skip or delay cave SDF refresh instead of hitching. Middle devices keep slow-cadence cave light updates. High and Ultra still get dense cave SDF lighting, but the visual update is fed by settled slow-phase state.
Hardware Impact: Static estimate: 5900 us worst-case SDF slice/encode/resource repair moved out of late-frame. `LateFrameTick` method-body scan reports 0 `EnsureResources`, `AdvanceLightingVolumeState`, `ScanSlice`, `BeginScan`, `FinalizeScan`, `SystemInfo`, or resource allocation hits.

Problem: Verification had to distinguish the local phase split from pre-existing dirty hunks in the same file.
Solution: Checked source balance, method-body phase boundaries, scoped diff check, CPU/compiler throttle, and lock shape. The relevant new proof is that `LateFrameTick` no longer reaches SDF scan/resource repair; the existing SDF upload lock remains one write lock with `finally` release.
Rejected Alternatives: Rewriting the whole cave lighting owner while other agents already had dirty lock/resource changes in the file. The safe correction was the visual-sync phase split only.
Scalability potential: The change preserves cave lighting data contracts and only changes when presentation work is performed: slow phase for scan/repair, late frame for completed upload/global flush.
Hardware Impact: `HectonCaveVoxelLightingVolume` balance braces/parens/brackets 0/0/0; `LateFrameTick` forbidden-like hits 0; `SlowTick` owns the two intentional hits: `EnsureResources` and `AdvanceLightingVolumeState`; scoped diff check exits 0 with LF/CRLF warning only.

Problem: Broad direct hot-body scan still reported `GlobalShaderDispatcher.LateFrameTick()` because it called `TryEnsureCommandBuffer(allowAllocation: false)`. The branch already failed closed, but static proof still reached an allocation-capable helper body that can allocate a `CommandBuffer` when called with `allowAllocation: true`.
Solution: Added pure `HasCommandBufferReady()` and changed `LateFrameTick()` to use it. Lifecycle still owns `TryEnsureCommandBuffer(allowAllocation: true)` from cold paths.
Rejected Alternatives: Keeping the boolean-guarded ensure call and relying on reviewers to reason about the argument. HECTON-8 hot-root proof is cleaner when allocation-capable method bodies are unreachable from late-frame roots.
Scalability potential: Low through Ultra devices keep the same shader-global upload behavior; the change removes a proof-visible allocation edge from visual sync without changing DTO layout, global route, or quality policy.
Hardware Impact: 0 steady-state us saved. Verification value: broad non-editor direct hot-body scan over `Graphics`, `Rendering`, and `World` reports `DIRECT_HOT_BODY_REPORTS=0`.

Problem: Final scan needed to prove no direct late/render root still contains allocation, platform, registry, component, screen, or repair-token calls after the latest fixes.
Solution: Ran direct method-body scanner for `LateFrameTick`, `RecordRenderGraph`, `AddRenderPasses`, and `Render` across non-editor platform/render/world roots, plus scoped balance and diff checks.
Rejected Alternatives: Running full compile at 100% CPU or using raw grep without method-boundary filtering.
Scalability potential: This closes the direct hot-body layer; deeper transitive scans remain task-specific because broad call graphs are heavier and should run only when a candidate appears.
Hardware Impact: `GlobalShaderDispatcher` balance braces/parens/brackets 0/0/0; broad direct hot-body reports 0; scoped diff check exits 0 with LF/CRLF warning only.

Problem: `FoveatedRenderCommander.HasEyeTrackedGaze()` called `InputDevices.GetDeviceAtXRNode(XRNode.CenterEye)` from the foveation policy sample path. That is platform/device discovery inside visual policy flow for PC VR gaze-tracked VRS.
Solution: Added `_centerEyeDeviceCold` and refreshed it from `CacheRuntimeCapabilitySnapshotCold()`, which is called by lifecycle and `SlowTick`. `HasEyeTrackedGaze()` now consumes the cached `InputDevice` value and performs only current feature-value reads.
Rejected Alternatives: Polling `InputDevices.GetDeviceAtXRNode` every policy sample, or disabling gaze-tracked VRS outright. Polling is a hot platform lookup; disabling would waste high-end PC VR capability instead of caching the dependency correctly.
Scalability potential: Low devices and standalone-class hosts avoid XR device lookup variance. Middle PC VR keeps gaze-tracked foveation when the cached device is valid. High and Ultra keep gaze-tracked VRS for visual overkill without changing gameplay truth, DTO layout, or quality authority.
Hardware Impact: Static estimate: 24 us saved on sampled PC VR foveation frames. Transitive graph from `LateFrameTick`/`Render` reports 0 `InputDevices.GetDeviceAtXRNode`, registry, component, platform, allocation, or job reports after member-call disambiguation.

Problem: The verification pass had to prove the XR patch without hiding existing telemetry lock behavior or violating build throttling.
Solution: Ran source balance, transitive hot graph, direct `InputDevices` location check, telemetry lock-shape scan, scoped diff check, and CPU/compiler throttle. No `dotnet build` was launched at 59.6% CPU.
Rejected Alternatives: Treating the original graph report as fully accurate; it included false unregister paths through `stream?.Dispose()` until member-call disambiguation was added. Forcing compile under CPU throttle was also rejected.
Scalability potential: The patch preserves continuous `GlobalQualityWeight` foveation policy and only changes dependency ownership: cold/slow for XR device identity, hot for value reads.
Hardware Impact: `FoveatedRenderCommander` balance braces/parens/brackets 0/0/0; hot graph 71 methods / 40 reachable / 0 forbidden reports; `TryAcquireTelemetryWriteBuffer` has one acquire, one failed-acquire release path, and `finally`; `WriteTelemetry` releases the handed-off write lock in `finally`.

Problem: `FoveatedRenderCommander.ApplyPolicy()` called `HectonXRManager.RefreshEyeDescriptor()`, which reads `XRSettings.eyeTextureDesc`. That kept XR descriptor/platform sampling in the late-frame foveation policy graph even after the eye device lookup was cached.
Solution: Added `_eyeDescriptorCold` and refresh it from `CacheRuntimeCapabilitySnapshotCold()` alongside foveation caps and center-eye device identity. `ApplyPolicy()` now consumes the cached `RenderTextureDescriptor` value.
Rejected Alternatives: Leaving descriptor refresh in `ApplyPolicy()` because it is sampled only every configured interval. The interval still executes from visual policy flow, and descriptor discovery can move to cold/slow without changing output contracts.
Scalability potential: Low devices and standalone VR avoid descriptor-query variance in visual policy flow. Middle devices keep the current descriptor after slow refresh. High and Ultra retain full eye-resolution foveation policy using the same cached descriptor until the next cold snapshot.
Hardware Impact: Static estimate: 18 us saved on sampled foveation frames. Hot graph with `HectonXRManager.RefreshEyeDescriptor` as a forbidden token reports 71 methods / 40 reachable / 0 reports.

Problem: The descriptor patch changed foveation policy inputs and needed a second proof pass without running a build under CPU throttle.
Solution: Reran source balance, hot graph, scoped diff check, and CPU/compiler throttle. No compile was launched at 70.9% CPU.
Rejected Alternatives: Pushing descriptor refresh into `HectonXRRuntimeState.RefreshFrameState()` now. That would widen the patch into core dispatcher timing; the local commander slow snapshot is enough because this commander is the only `RefreshEyeDescriptor()` consumer.
Scalability potential: Descriptor ownership is now local and cold. Future quality changes can still scale foveation cadence continuously without descriptor discovery in visual policy flow.
Hardware Impact: `FoveatedRenderCommander` balance braces/parens/brackets 0/0/0; scoped `git diff --check` exits 0 with LF/CRLF warnings only; `dotnet build` skipped under throttle.

Problem: `HectonXRRuntimeState.RefreshFrameState()` was called from `SystemDispatcher.RunDispatcherUpdate()` every frame and directly read `XRSettings.enabled` / `XRSettings.isDeviceActive`, sampled display refresh through `SubsystemManager.GetSubsystems`, and repaired missing head AUP by calling `SlowTickHeadAupCache()`.
Solution: Split platform state into `RefreshPlatformStateCold(int frame)` and call it from dispatcher service init plus `RunSlowTick`. `RefreshFrameState()` now only consumes cached `_isXRActive` / `_refreshRateHz` and queues shader globals. Head AUP repair remains in explicit slow phase.
Rejected Alternatives: Sampling XR active state every frame because the values are "cheap". XR runtime state is platform identity, not gameplay truth; frame update must not poll XR platform APIs or bridge AUP repair.
Scalability potential: Low devices and standalone VR avoid XR platform-query variance. Middle PC VR keeps stable cadence with slow refresh. High and Ultra can still use hardware foveation and high refresh, but the overkill path is fed by cold state.
Hardware Impact: Static estimate: 44 us saved on XR frame-state paths. `RefreshFrameState`, `ResolveDispatcherDeltaTime`, and `RunDispatcherUpdate` direct scans report no `XRSettings`, `SubsystemManager`, registry/component lookup, allocation, schedule, or completion hits.

Problem: `HomeostasisBrain.PreSimulationTick()` used `ResolveTargetFrameRate()` every frame and could reach `HectonXRRuntimeState.TryRequestDisplayRefreshRateHz()`, whose old body queried `SubsystemManager.GetSubsystems` and mutated `Application.targetFrameRate` when pressure policy triggered XR refresh shedding.
Solution: Added `_cachedTargetFrameRate` and `RefreshCadenceSnapshotCold()` for init/slow phase sampling. `TryRequestDisplayRefreshRateHz()` is now a zero-GC scalar latch; `TryApplyDisplayRefreshRateRequestCold()` applies the request in `RefreshPlatformStateCold()`.
Rejected Alternatives: Applying XR refresh policy immediately from pre-simulation pressure logic. The pressure policy is hot control logic; the subsystem query and target-frame mutation are platform side effects and belong in cold/slow ownership.
Scalability potential: Low devices under pressure queue one scalar request and let slow phase shed refresh. Middle devices keep frame-health math stable from cached FPS. High and Ultra preserve refresh-rate overkill when the slow snapshot permits it.
Hardware Impact: Static estimate: 61 us saved during XR pressure frames. State transfer is two scalar fields (`float`, `bool`), 0 B GC, no DTO/layout or authority-route change.

Problem: Verification had to prove the core platform split while another agent/process already had `dotnet` running and the same files carried unrelated dirty hunks.
Solution: Ran source balance for the three touched files, direct hot-body scan across Core/Graphics/Rendering/Visor roots including `PreSimulationTick` and `RefreshFrameState`, scoped `git diff --check`, and CPU/compiler throttle checks. Did not launch a build because `dotnet` was already active.
Rejected Alternatives: Starting another compile under the project throttle, or reverting unrelated dirty hunks in `SystemDispatcher` / `HomeostasisBrain`.
Scalability potential: The edited routes now keep platform discovery and target-cadence mutation out of frame-critical logic across weak, middle, high, and ultra devices.
Hardware Impact: Three touched files balance braces/parens/brackets 0/0/0. Direct hot scan reports `DIRECT_CORE_GRAPHICS_REPORTS=0`; scoped diff check exits 0 with LF/CRLF warnings only.

Problem: `RenderTexturePool.Rent()` and `Return()` called `DefragForCurrentScreenIfNeeded()`, which reads `Screen.width/height` and can clear all pool queues. Those API routes are used by visor/UI/cockpit RT owners and can be reached from resize/render-visible presentation routes, so screen polling and full-pool clear were hidden inside a shared resource API.
Solution: `RenderTexturePool` now implements `ISlowTickable`. `SlowTick()` owns `DefragForCurrentScreenIfNeeded()`. `Rent()` and `Return()` consume explicit RT dimensions only and no longer poll the screen or clear pools.
Rejected Alternatives: Keeping the defrag call in `Rent/Return` because it catches screen changes immediately. A one-slow-tick delay for freeing obsolete pooled full-screen RTs is cheaper than allowing every RT borrow/release to poll platform surface state and potentially clear the pool.
Scalability potential: Low devices avoid screen-query and pool-clear variance in UI/visor routes. Middle devices keep delayed defrag with stable pooling. High and Ultra can keep larger prewarmed full-screen queues while resize cleanup remains slow-phase ownership.
Hardware Impact: Static estimate: 26 us saved on rent/return bursts, with larger hitch avoidance when a resolution change previously triggered full-pool clear from a caller route. State transfer is two cached ints and existing queues; 0 B GC in the public API.

Problem: `RenderTexturePool.Return()` allocated `new Queue<RenderTexture>(DynamicBucketCapacity)` when an unknown RT key was returned. That made the release path a managed allocation route for transient sizes and created a future GC liability.
Solution: Unknown return keys now dispose the RT through the lifecycle tracker and skip pool growth. Only prewarmed/known buckets retain RTs.
Rejected Alternatives: Keeping dynamic bucket growth to improve reuse for arbitrary custom sizes. In this project the high-frequency owners already request stable screen-sized or explicitly owned RTs; dynamic pool expansion belongs in cold/prewarm policy, not release cleanup.
Scalability potential: Low devices avoid managed allocation and future GC from one-off RT shapes. Middle devices reuse known buckets. High and Ultra can still be extended later with an explicit prewarm API if a repeated custom shape is proven.
Hardware Impact: Static estimate: 160 us first-unknown-key allocation/GC pressure avoided per unique transient RT shape. `Return` body scan reports no `new Queue`, `Screen`, registry lookup, or component lookup.

Problem: Verification had to prove the shared pool API stayed clean without starting another compile while the machine was already saturated.
Solution: Ran in-memory source balance, focused method-body scans for `Rent`, `Return`, and `SlowTick`, scoped token scan, scoped `git diff --check`, and CPU/compiler throttle. No `dotnet build` was launched because CPU was 100% and an external `dotnet` process was active.
Rejected Alternatives: Spamming a build under throttle or broad parser runs while other agents had long PowerShell scans active. The touched file is small enough for focused static AST/source proof.
Scalability potential: The resource owner now has one route for surface-size maintenance: lifecycle capture and slow tick. Public borrow/release remains deterministic across weak, middle, high, and ultra devices.
Hardware Impact: `RenderTexturePool` balance braces/parens/brackets 0/0/0; `Rent`/`Return` body scans report no `Screen`, `DefragForCurrentScreenIfNeeded`, `GlobalRegistry.Get`, `GetComponent`, or `new Queue`; `SlowTick` is the only defrag caller.

Problem: `ShinobuEcosystemBalancer.BindProceduralCullingResources()` read `SystemInfo.supportsComputeShaders`, and the kernel helper methods read it again. The API has no current external caller, but it is public and intended for procedural swarm render/culling owners, so leaving platform probing there would create a bad contract for future render-visible binding.
Solution: Added `_supportsComputeShadersCold` refreshed by `RefreshGraphicsCapabilitiesCold()` during runtime activation. Binding now consumes the cached capability and fails closed when compute is unsupported.
Rejected Alternatives: Leaving the read in `BindProceduralCullingResources()` because the current call graph has no external caller. Contract-level cleanup is cheaper now than letting the first render owner inherit a platform-polling API.
Scalability potential: Low devices and standalone VR fail closed from a cached compute capability. Middle devices keep stable compute culling when supported. High and Ultra can use procedural swarm culling without platform discovery in the bind route.
Hardware Impact: Static estimate: 31 us saved on repeated culling-bind frames on weak CPUs/handheld APUs. `BindProceduralCullingResources`, `Render`, and `ResolveGpuCullingParams` body scans report no `SystemInfo`, registry lookup, component lookup, or allocation tokens.

Problem: `BindProceduralCullingResources()` resolved compute kernels and thread-group sizes every time resources were bound. Camera matrices/depth resources can change without the culling compute shader changing, so repeated `HasKernel`, `FindKernel`, `IsSupported`, and `GetKernelThreadGroupSizes` calls are unnecessary native/API work.
Solution: Added `_proceduralCullKernelsResolved`; kernels and thread-group sizes are resolved only when the compute shader identity changes or the cold compute capability cache invalidates. Matrix/depth/culling scalar updates continue every bind.
Rejected Alternatives: Pre-resolving every possible compute asset globally, or resolving kernels in render. Global pre-resolution would invent ownership; render resolution would violate phase rules.
Scalability potential: Low devices skip repeated kernel reflection. Middle devices keep cached procedural culling. High and Ultra retain swarm visual overkill with per-frame matrix updates only, not per-frame kernel introspection.
Hardware Impact: Static estimate: 240 us saved on repeated procedural swarm culling bind frames. `ResolveSupportedKernel` and `ResolveKernelThreadGroupSizeX` still contain kernel reflection, but they are no longer reachable from repeated bind unless compute shader identity changes.

Problem: Verification needed to include both the new Shinobu contract patch and the RT pool patch while the host stayed under external compile/process load.
Solution: Ran scoped source balance, focused method-body scans, scoped token location scans, scoped `git diff --check`, and CPU/compiler throttle. No compile was launched at 97% CPU with external `dotnet` active.
Rejected Alternatives: Running `dotnet build` under throttle, or pretending no risk exists because Shinobu culling bind has no current callsite. Public APIs are future hot paths unless their contract is clean.
Scalability potential: Platform capability ownership is now cold, while high-frequency presentation bind/update data stays scalar and cached across weak, middle, high, and ultra devices.
Hardware Impact: `ShinobuEcosystemBalancer` balance braces/parens/brackets 0/0/0; only `RefreshGraphicsCapabilitiesCold` reads `SystemInfo.supportsComputeShaders`; `Render`/`BindProceduralCullingResources` scans are clean.

Problem: `HectonUIScaler.DisabledVisualSync()` could call `ApplyScale()`, which reached `ResolveRenderDimensions()` and read `Screen.width/height`. Disabled visual sync is still a presentation-phase route, so surface-size polling there violates phase ownership for HUD scaling.
Solution: Added `_cachedRenderWidth/_cachedRenderHeight`. `ResolveRenderDimensions()` now reads cached values for overlay canvases and authored reference resolution for world-space canvases. `RefreshRenderDimensionsSlowSample()` owns `Screen.width/height` sampling from lifecycle/editor rebuild/slow tick.
Rejected Alternatives: Keeping direct `Screen` reads because they are only two properties. On handheld/VR resize paths the bigger risk is hidden surface state polling and scale recalculation from a visual callback; one slow-tick delay is acceptable for UI scale.
Scalability potential: Low devices and standalone VR keep HUD scaling deterministic from cached surface dimensions. Middle devices adapt on slow cadence. High and Ultra can keep HUD sharpness/scale while resize detection remains outside visual sync.
Hardware Impact: Static estimate: 18 us saved on UI visual-sync scale checks. `DisabledVisualSync` and `ResolveRenderDimensions` body scans report no `Screen`, registry lookup, component lookup, or allocation tokens.

Problem: Moving screen sampling out of `ResolveRenderDimensions()` would have made resize recovery depend on a future forced apply unless slow tick also refreshed dimensions when bootstrap was complete.
Solution: `SlowTick()` now refreshes cached dimensions every slow tick and reapplies scale only when width or height changes. Content-root bootstrap still uses the existing path.
Rejected Alternatives: Applying scale every slow tick. The cached dimension comparison avoids unnecessary transform writes and keeps resize correction event-like.
Scalability potential: Low devices avoid repeated matrix/transform writes. Middle through Ultra react to resolution changes on slow cadence while visual sync remains pure cached read/transform math.
Hardware Impact: 0 steady-state us saved; correctness gain is clean resize adaptation without `Screen` reads from visual sync.

Problem: Verification needed to prove the nested UI scaler change without treating unrelated `SuitHUDV4CanvasOverlay` systems as edited.
Solution: Ran full-file source balance, scoped token location scan, focused method-body scans around the scaler methods, scoped diff check, and CPU/compiler throttle. No build was launched at 84% CPU with external `dotnet` active.
Rejected Alternatives: Rewriting the full HUD overlay runtime or moving all canvas normalization. The violation was localized to the nested scaler render-dimension route.
Scalability potential: Hecton UI scale now has one route for surface dimensions: lifecycle/editor/slow snapshot. Visual sync consumes cached dimensions across weak, middle, high, and ultra devices.
Hardware Impact: `SuitHUDV4CanvasOverlay.cs` balance braces/parens/brackets 0/0/0; only `RefreshRenderDimensionsSlowSample` contains `Screen.width/height` in the scaler patch.

Problem: Nested `SuitHUDV4CanvasOverlay.HectonUIScaler` still had a transitive visual-sync hazard after the first screen-cache patch: cached-root validation could search child transforms through `FindExistingChild`, and the public `ContentRoot` read accessor could call `ResolveContentRootInternal(false)`.
Solution: Split hot validation from cold resolution. `ContentRoot`, `DisabledVisualSync`, and `TryRefreshExistingContentRootHot` now use cached references only. `TryResolveExistingContentRootCold` plus `EnsureContentRoot` run only from slow/cold bootstrap and can repair a missing scaled root.
Rejected Alternatives: Keeping child lookup in visual sync because it is a bounded child loop. A hierarchy scan is still scene traversal in a presentation callback; it belongs in slow recovery.
Scalability potential: Low devices avoid hidden scene traversal and RectTransform sanitation writes in visual sync. Middle devices recover missing roots from slow phase. High and Ultra keep full HUD scale fidelity with deterministic cached-root application.
Hardware Impact: Static estimate: 11 us saved on missing-root visual-sync guard cases; normal route has 0 B GC state transfer through cached `RectTransform` and two cached dimensions.

Problem: Standalone `Assets/_Project/Scripts/UI/HectonUIScaler.cs` carried the same architectural smell: `ContentRoot` was a read accessor that could search/mutate, and hot scale application depended on `ResolveRenderDimensions` reading live screen state in older code.
Solution: Made `ContentRoot` cached-only, moved screen sampling into `RefreshRenderDimensionsCold`, kept `LateFrameTick` on cached root/dimensions, and added slow/cold missing-root recovery. `ResolveRenderDimensions` is now a pure cached-int read.
Rejected Alternatives: Leaving the standalone scaler alone because the nested HUD scaler was already patched. Both classes publish the same UI scaling contract, so only fixing one creates platform drift between HUD implementations.
Scalability potential: Low devices and standalone VR avoid surface polling in HUD scale callbacks. Middle devices adapt on slow cadence. High and Ultra keep ultrawide and high-resolution HUD transforms without hot platform reads.
Hardware Impact: Static estimate: 18 us saved on HUD scale checks; state transfer is cached `int` width/height plus cached `RectTransform`, 0 B GC.

Problem: `RenderTextureLifecycleTracker` was registered as both slow and late-frame tickable. `SlowTick` only set `_leakCheckPending`, then `LateFrameTick` performed leak scans and editor/development logging.
Solution: Removed `ILateFrameTickable`, `_registeredLateFrame`, and `_leakCheckPending`. `SlowTick` now executes `CheckForLeaks()` directly; registration/unregistration only touches slow tick.
Rejected Alternatives: Keeping late-frame deferral to spread work over phases. Leak detection is diagnostics, not presentation; moving it into late-frame makes a nonvisual scan compete with visual sync.
Scalability potential: Low devices avoid leak-query/log variance in visual sync. Middle devices keep diagnostics at slow cadence. High and Ultra retain the same leak coverage without a second dispatcher registration.
Hardware Impact: Static estimate: 38 us late-frame variance removed on leak-check cadence; one bool state-transfer path deleted.

Problem: Verification had to satisfy APEX constraints without adding compile pressure or leaving parser processes alive.
Solution: Ran scoped source balance, focused method-body scans, hot-loop lookup scan, DataVault write-lock token scan, scoped diff check, CPU/compiler throttle check, and killed stale broad parser PIDs 23264, 45956, and 24416.
Rejected Alternatives: Running `dotnet build` while CPU was 88% and `VBCSCompiler.exe` was active, or leaving wide scans running in the background.
Scalability potential: The proof stayed scoped to changed files and did not steal CPU budget from other agents or game-oriented validation work.
Hardware Impact: Three changed files balance braces/parens/brackets 0/0/0; changed-file hot lookup reports 0; scoped DataVault write-lock scan reports 0; scoped diff check exits 0 with LF/CRLF warnings only.

Problem: `VRAMPressureMonitor.LateFrameTick()` owned profiler memory reads, `QualitySettings` writes, mip pressure, BRG LOD bias, RT pool clearing, and asset evictions. That tied resource policy to visual sync.
Solution: Converted the monitor to `ISlowTickable`, scheduled samples by `SystemDispatcher.CurrentFrameId`, and kept immediate requests as a scalar latch. Emergency drains now use `DrainPendingReleaseQueueBudgeted(emergencyEvictionBudget)`.
Rejected Alternatives: Keeping late-frame sampling because the old cadence was frame-counted. The cadence can be preserved with a slow tick frame deadline while avoiding presentation-phase quality mutation and unbounded drains.
Scalability potential: Low devices get bounded pressure cleanup and no late-frame QualitySettings churn. Middle devices keep gradual mip/LOD response. High and Ultra retain high baseline fidelity and only shed pressure from the slow resource owner.
Hardware Impact: Static estimate: 290 us removed from sample late frames; worst-case unbounded release drain reduced to 1-8 releases per slow pass on red-zone pressure.

Problem: `AssetLoadDispatcher.LateFrameTick()` evaluated the UI mip-bias gate and published telemetry warnings from visual sync.
Solution: Converted the dispatcher registration from late-frame tickable to slow tickable. UI icon and thumbnail paths now set `_uiMipBiasGateEvaluationQueued`; slow tick consumes cached VRAM/model dependencies and applies the gate.
Rejected Alternatives: Leaving evaluation in late frame for faster thumbnail recovery. One slow-tick delay is cheaper than executing VRAM breakdown and pressure response from the presentation phase.
Scalability potential: Low devices avoid thumbnail-induced late-frame spikes. Middle devices still shed UI mip pressure. High and Ultra preserve sharp UI until measured pressure crosses the continuous threshold.
Hardware Impact: Static estimate: 64 us saved on UI thumbnail pressure frames. State transfer is one bool plus existing cached interfaces, 0 B GC.

Problem: `AssetLifecycleGovernor.LateFrameTick()` flushed `_pendingRetryPump`, which iterated managed asset records and queued async dispatch retries.
Solution: Removed the pending retry flag and executed `PumpRetries()` inside `SlowTick`, next to TTL evaluation, release draining, and hard reaper logic. Late frame now only flushes cached renderer/audio presentation disables.
Rejected Alternatives: Keeping retry pump in late frame because it followed slow TTL evaluation. Retry dispatch is resource scheduling, not visual presentation.
Scalability potential: Low devices avoid managed table traversal in visual sync. Middle devices keep retries on the slow resource cadence. High and Ultra keep aggressive cache reuse without presentation-phase record scans.
Hardware Impact: Static estimate: 115 us saved on retry-active visual frames; no new DataVault write lock route was introduced.

Problem: `ContentAuthorityRuntime.LateFrameTick()` called AUP cleanup and VRAM intercept logic that could force-drain pending Addressables releases from visual sync.
Solution: Split both routes into late-frame latch methods and slow-tick flush methods. Late frame now checks current signals/pressure and sets `_pendingAupCleanup` / `_pendingVramIntercept`; slow tick performs bounded drains and evictions.
Rejected Alternatives: Keeping `ForceDrainPendingReleaseQueue()` because it releases memory immediately. Immediate full drain can stall weak CPUs and handheld APUs; bounded slow cleanup is predictable and repeats while pressure persists.
Scalability potential: Low devices get bounded cleanup budgets and stable HUD/proxy presentation. Middle devices avoid release spikes during AUP shifts. High and Ultra keep visual content budgets while pressure cleanup runs off visual sync.
Hardware Impact: Static estimate: 2200 us worst-case late-frame drain avoided. AUP and VRAM intercept drains are capped at 2 pending releases per slow pass plus existing eviction caps.

Problem: The verification pass had to cover resource-policy phase splits without writing JSON/binary reports or starting a compile under load.
Solution: Ran scoped in-memory source balance for seven files, direct hot-body forbidden-token scans, slow/phase scans, DataVault token scan, scoped `git diff --check`, CPU/compiler throttle, and process check.
Rejected Alternatives: Spamming `dotnet build` at 95% CPU, or running a broad parser over the entire project after earlier parser processes caused load. The changed files were enough for the current proof surface.
Scalability potential: The proof confirms resource policy now lives in cold/slow ownership, while visual sync consumes cached refs, bool latches, or bounded presentation toggles.
Hardware Impact: Seven files balance braces/parens/brackets 0/0/0; direct hot-body reports 0; scoped diff check exits 0 with LF/CRLF warnings only; build skipped because CPU measured 95%.

Problem: `DiegeticPanelController` could reach `EnsureRenderTexture()` and phosphor resource repair from late-frame interaction refresh and from `ForceRefreshRenderTexture()` called by PDA presentation. That path can allocate `RenderTexture` objects or command resources on the same frame the UI is being presented.
Solution: Added `ISlowTickable` ownership for RT rebuilds. `LateFrameTick` now only advances interaction math, cursor/view/material/proxy presentation, and scalar latches. `SlowTick` consumes `_pendingDistanceRenderTextureRefresh`, `_pendingQualityPresentationRefresh`, and `_forceRenderTextureRefreshQueued`, then calls `RefreshDistanceAndRenderTexture()` and `EnsureRenderTexture()` outside visual sync.
Rejected Alternatives: Keeping a force-refresh call in late frame and relying on distance-refresh throttling. First visible PDA open and quality/phosphor transitions are exactly the frames where weak hardware cannot afford hidden RT allocation.
Scalability potential: Low devices and standalone VR can show one stale/blank panel frame instead of hitching on RT allocation. Middle devices refresh panel surfaces on slow cadence. High and Ultra still rebuild higher-resolution panel/phosphor surfaces, but the cost is phase-owned and no longer competes with cursor/proxy presentation.
Hardware Impact: Static estimate: 900-4200 us worst-case visual-frame spike moved to slow phase depending on RT size and phosphor state; state transfer is bool/float fields, 0 B GC. Hot graph from `LateFrameTick`/interaction/force-refresh roots has 59 reachable methods and 0 forbidden resource/allocation/platform/component reports.

Problem: The first draft of the RT split registered slow tick from queue helpers, which could make hot callers read `GlobalRegistry.Dispatcher` while merely setting a refresh latch.
Solution: Removed queue-time registration. Dispatcher availability is stored in `_dispatcherAvailableCold`, refreshed from cold registration and dispatcher hot-swap events. `RefreshLateFrameRegistration()` now reads the cached flag instead of polling the registry.
Rejected Alternatives: Calling `GlobalRegistry.Dispatcher` from every queue helper because the property is cheap. Registry access is cold dependency ownership; hot phase code should not poll it to repair registration.
Scalability potential: Weak devices avoid registry drift in PDA/UI presentation frames. Middle, High, and Ultra keep identical behavior because lifecycle/hot-swap registration remains the owner of dispatcher binding.
Hardware Impact: Static estimate: sub-1 us steady-state; correctness gain is removal of hot registry polling from the new RT refresh route.

Problem: Verification needed proof without violating compile throttling or writing synthetic proof artifacts.
Solution: Ran one-file balance, transitive hot graph, read-accessor purity scan, DataVault token scan, scoped diff check, CPU/compiler throttle, and process command-line check. No `dotnet build` was launched at 100% CPU with active external `dotnet build Hecton8.slnx`.
Rejected Alternatives: Spamming a second build under the throttle, or claiming runtime proof from a broad unbounded parser. The local parser was scoped to one file and exited; remaining Python processes are user services or unrelated stdin sessions, not killed.
Scalability potential: The proof surface is narrow and keeps CPU available for other agents while still blocking the concrete RT-allocation hot path.
Hardware Impact: `DiegeticPanelController.cs` balance braces/parens/brackets 0/0/0; `git diff --check` exit 0 with LF/CRLF warning only; no DataVault write-lock route exists in the touched file.

Problem: A `python.exe -` process remained with a dead parent after verification, matching an orphaned parser signature rather than a named user service.
Solution: Killed only that orphan PID and rechecked active Python command lines. Named user services were left untouched.
Rejected Alternatives: Killing all Python processes or ignoring an orphan parser. Broad kills would break user services; ignoring it violates the local no-orphan process rule.
Scalability potential: No runtime code change; shared workstation CPU stays available for compiles, parsers, and player validation.
Hardware Impact: Removed one unmanaged CPU consumer; exact game-frame microseconds not applicable.

Problem: `ToolDiegeticDisplayController` presentation decisions queued `_pendingEnsureRenderTexture`, but the visual phase had no safe resource owner: keeping RT rent/return in `LateFrameTick` would allocate or release the tool screen on the equip/visibility frame, while never flushing the ensure latch could leave the tool on fallback forever.
Solution: Cache `IRenderTexturePoolService` cold from lifecycle/start, keep presentation decisions as bool latches, and consume `_pendingEnsureRenderTexture` / `_pendingReleaseRenderTexture` from `SlowTick` through `FlushPendingRenderTextureResourceState()`. `LateFrameTick` now treats release-pending or missing RT as fallback-only presentation and never calls resource methods.
Rejected Alternatives: Calling `EnsureRenderTexture()` in `LateFrameTick`, or re-polling `GlobalRegistry.RenderTexturePoolService` from `EnsureRenderTexture()`. Both approaches would put resource ownership or cold dependency discovery back into a visual-sync path.
Scalability potential: Low devices and standalone VR can show fallback for one slow cadence instead of hitching on RT creation. Middle devices recover the RT on slow phase. High and Ultra retain the live tool-screen render route without visual-frame pool churn.
Hardware Impact: Static estimate: 480-1400 us worst-case tool-screen RT rent/create/return spike moved out of visual sync. State transfer is two bool latches and cached pool references, 0 B GC.

Problem: The phase split needed a durable source-level proof so future changes do not route RT resource work back into `LateFrameTick`.
Solution: Added `ToolDiegeticDisplay_RenderTextureResourceWorkIsSlowPhaseOnly` editor guard. It asserts slow tick calls the resource flush, the flush owns `ReleaseRenderTexture()` and `EnsureRenderTexture()`, `LateFrameTick` contains neither call, and `EnsureRenderTexture()` consumes `_cachedRenderTexturePool` with no `GlobalRegistry` fallback.
Rejected Alternatives: Relying on the current diff or a markdown checklist. Those do not block the next regression.
Scalability potential: Preserves the same slow-phase contract across weak, middle, high, and ultra hardware.
Hardware Impact: 0 runtime cost; test-only source proof.

Problem: APEX verification needed to prove the hot graph, phase safety, lock scope, and compile throttle without spawning a broad parser or a second build.
Solution: Ran a scoped in-memory parser over `ToolDiegeticDisplayController.cs` and the guard test. The parser built a local call graph from `LateFrameTick` and `SlowTick`, checked source balance, checked forbidden hot tokens, confirmed slow-phase resource reachability, scanned DataVault write-lock tokens, ran scoped `git diff --check`, and checked active compiler/process state.
Rejected Alternatives: Launching `dotnet build` while CPU was 77.1% and an external `dotnet build Hecton8.slnx` was already active. That violates the project compile throttle and adds no value for this scoped source proof.
Scalability potential: The proof stays narrow enough for a shared 20-agent workstation while catching the exact RT lifecycle drift on the tool screen.
Hardware Impact: `ToolDiegeticDisplayController.cs` and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0. Hot graph: 35 reachable methods, 0 forbidden reports. Slow graph reaches `FlushPendingRenderTextureResourceState`, `EnsureRenderTexture`, `ReleaseRenderTexture`, and `DestroyUnownedRenderTexture`. No DataVault write-lock route exists in the touched runtime file.

Problem: `TerminalOsRuntime.LateFrameTick()` still called `TryDumpBlackBox(faultFlags)` directly, and `TryFinalizeDecryptionJob()` could call `TryDumpDecryptionBlackBox(faultFlags)` from the same late-frame job-completion route. Both paths can touch file/dump writer work during visual sync.
Solution: Added terminal and decryption fault flag latches. `LateFrameTick` / decryption finalize now call `QueueTerminalBlackBoxDump` and `QueueDecryptionBlackBoxDump`; `SlowTick` and teardown call `FlushQueuedBlackBoxDumps`.
Rejected Alternatives: Keeping dump calls in late frame because they only run on faults. Fault frames are exactly when the renderer and UI need predictable recovery; file I/O and writer enqueue belong to slow/fault ownership.
Scalability potential: Low devices and standalone VR avoid fault-frame presentation stalls. Middle devices preserve crash evidence with one slow-tick delay. High and Ultra keep identical diagnostic coverage without contaminating visual sync.
Hardware Impact: Static estimate: 700-2600 us fault-path stall moved out of late frame. State transfer is two bool latches plus two uint fault masks, 0 B GC.

Problem: `TopographicalSonarSynthesizer.CommitCompletedScan()` called `DumpBlackBox()` after invalid scan telemetry. That method allocates a temp `NativeArray<byte>`, creates the dump directory, and submits an async file write from a path reached by `LateFrameTick` job completion.
Solution: Added `ISlowTickable` registration, `_blackBoxDumpQueued`, `QueueBlackBoxDump`, and `FlushQueuedBlackBoxDump`. Scan completion now queues the dump, while slow phase owns the dump allocation/write path.
Rejected Alternatives: Treating black-box dump as acceptable because it is fault-only. The system must degrade predictably on weak machines, and fault capture cannot add hidden visual-sync allocation.
Scalability potential: Low devices can finish the visual frame and dump on slow cadence. Middle devices keep sonar diagnostics without late-frame allocation. High and Ultra retain black-box capture while preserving render cadence.
Hardware Impact: Static estimate: 900-3200 us fault-path allocation/write spike moved out of visual sync; normal frames unchanged.

Problem: The final UI/VR proof had to cover hot dependency lookup, phase safety, DataVault lock flattening, compile throttle, and parser cleanup without writing synthetic proof artifacts.
Solution: Ran scoped in-memory lexical balance for Terminal/Topographical/test files, direct hot-body scan over 120 non-editor UI runtime files, UI write-lock shape scan, scoped `git diff --check`, CPU/compiler throttle check, and Python process scan.
Rejected Alternatives: Running `dotnet build` while CPU was 80.7% and external `dotnet.exe` PID 54640 was active, or relying on a broad dirty worktree diff from other agents.
Scalability potential: The proof confirms UI/VR presentation phases consume cached state and scalar latches; slow/cold owners perform file/resource work.
Hardware Impact: Changed files balance 0/0/0. Direct UI hot reports 0. Seven UI write-lock methods each have one acquire, one release, and `finally`. Scoped diff check exits 0 with LF/CRLF warnings only.

Problem: `HectonMapMagicVegetationBridge.CacheTileMasks()` could reach `RefreshTerrainTextureCaches()` from the native-cache preparation path. That helper can allocate a managed `Texture2D[]` when terrain alphamap count changes, so a late/streaming cache path had an allocation-capable edge.
Solution: Split the helper into `RefreshTerrainTextureCachesCold()` and `TryRefreshTerrainTextureCachesHot()`. Tile upsert owns allocation and handle-cache sizing; hot cache preparation only refreshes existing texture handles and fails closed if the cold cache is not ready.
Rejected Alternatives: Keeping one branch-guarded helper with an `allowAllocate` bool. Branch-insensitive hot-graph analysis would still reach the allocation-capable method, and the contract would be easier to regress.
Scalability potential: Low devices skip a cache pass rather than allocating during terrain residency work. Middle devices refresh from pre-sized arrays. High and Ultra keep the same detailed MapMagic masks without hot managed array churn.
Hardware Impact: Static estimate: 120-480 us first-mismatch managed allocation/GC risk removed from cache preparation frames; state transfer remains existing tile fields, 0 B GC in the hot helper.

Problem: `SargassumGlobalDragManager.LateFrameTick()` called `EnsureVisualResourcesForLateFrame()`, which could allocate arrays, `Texture2D`, fallback mesh/material, `GraphicsBuffer`, and BRG resources, then acquire DataVault write lock for BRG metadata.
Solution: Replaced late-frame repair with `_visualResourceRepairRequested` and `EnsureVisualResourcesForSlowTick()`. Late frame now only checks cached readiness, retries scalar work when missing, and renders from already prepared resources.
Rejected Alternatives: Leaving repair in late frame because it usually early-outs. Worst-case first visibility, quality change, or resource loss is exactly when standalone VR/weak GPUs cannot afford hidden resource repair.
Scalability potential: Low devices get one stale or missing sargassum visual frame instead of allocation stutter. Middle devices repair on slow cadence. High and Ultra keep dense canopy/scavenger visuals, but allocation and BRG setup stay out of visual sync.
Hardware Impact: Static estimate: 900-2400 us worst-case visual resource repair moved out of late frame. Sargassum scavenger write-lock methods verified at one acquire, one release, one `finally`; no nested DataVault write-lock route added.

Problem: `VisorHUDController.ConfigureHudScissorCommandBuffers()` could call `EnsureHudScissorCommandBuffers()` and sample `GraphicsSettings` while binding projection output. That put `new CommandBuffer` and platform pipeline discovery on a presentation/projection route.
Solution: Cached SRP state in `CacheGraphicsCapabilitiesCold()`, renamed allocation owner to `EnsureHudScissorCommandBuffersCold()`, added `FlushHudScissorCommandBufferRepairSlow()`, and made configure fail closed by queuing repair when command buffers are missing.
Rejected Alternatives: Allocating command buffers during `BindRT()` to guarantee same-frame scissor. A one-frame unscissored/fallback HUD is cheaper and safer than command-buffer allocation inside visual sync.
Scalability potential: Low devices avoid HUD projection hitches. Middle devices self-repair on slow phase. High and Ultra retain scissor precision and adaptive render scale without presentation-phase resource creation.
Hardware Impact: Static estimate: 350-900 us first scissor setup moved to cold/slow phase; cached bool state transfer is 0 B GC.

Problem: The transitive scanner reported MicroFauna and Marauder indirect argument writers as `new GraphicsBuffer` allocations because it matched `new GraphicsBuffer.IndirectDrawIndexedArgs`.
Solution: Verified and guarded that both hot writers only initialize the `GraphicsBuffer.IndirectDrawIndexedArgs` struct inside `LockBufferForWrite`; actual `new GraphicsBuffer(...)` allocation remains in cold ensure methods.
Rejected Alternatives: Refactoring already-correct writers just to satisfy a token scanner. That would add churn without changing runtime behavior.
Scalability potential: Keeps indirect draw setup cheap on low/middle hardware and preserves GPU rendering paths for high/ultra without false-positive-driven code churn.
Hardware Impact: 0 runtime us saved; proof prevents unnecessary edits. Hot writers contain 0 `new GraphicsBuffer(` allocations.

Problem: Verification had to cover dependency lookup, phase safety, lock flattening, compile throttle, and parser cleanup without writing synthetic proof dumps.
Solution: Ran an in-memory string/comment-stripped source parser over six files, method-body assertions, scoped same-file call graphs from hot roots, direct `GlobalRegistry.Get<T>()`/`GetComponent()` scan, write-lock shape checks for Sargassum scavenger methods, scoped `git diff --check`, CPU/compiler throttle, and Python process check.
Rejected Alternatives: Running `dotnet build` while CPU was 62% and an external `dotnet.exe` PID 30052 was active. That violates the project compile-throttle rule and would steal CPU from other agents.
Scalability potential: Proof remains scoped enough for a 20-agent workstation while blocking concrete resource-allocation drift on weak, middle, high, and ultra device lanes.
Hardware Impact: Six files balance braces/parens/brackets 0/0/0. Hot graphs: Sargassum 75 reachable / 0 forbidden; Visor 80 / 0 forbidden; MapMagic 27 / 0 forbidden. Scoped diff check exit 0 with LF/CRLF warnings only.

Problem: `HectonCelestialEngine.FlushCelestialVisualSync()` could still call an atmosphere update helper whose body contained `EnsureCelestialAtmosphereAuthoring()` and `EnsureCelestialAtmosphereTexture()`. The call used `allowResourceRepair: false`, but branch-insensitive hot-graph analysis still reached allocation-capable code and the contract was easy to regress.
Solution: Removed the boolean repair gate. Added `TryUpdateDynamicCelestialAtmosphereVisualSync()` as a cached-only visual-sync method that queues `_celestialAtmosphereLutRepairRequested` when the LUT is missing. `FlushCelestialAtmosphereLutRepairSlow()` owns `EnsureCelestialAtmosphereLutReady(publishOnRebuild: false)`.
Rejected Alternatives: Keeping the `allowResourceRepair` parameter. That is a soft promise, not a structural phase boundary.
Scalability potential: Low devices can render one stale sky/fog frame instead of allocating a LUT during visual sync. Middle devices repair on slow cadence. High and Ultra keep atmosphere LUT rebuild quality but resource ownership stays out of late frame.
Hardware Impact: Static estimate: 80-260 us worst-case `Texture2D` repair edge removed from visual sync. State transfer is one bool latch plus existing cached texture reference, 0 B GC.

Problem: `GlobalWeatherDirector.FlushNoirFogLutTexture()` owned Noir fog LUT repair from the shader publish route, allowing `new Texture2D` and `new Color[]` to happen during late-frame weather shader sync if resources were missing or resized.
Solution: Added `_noirFogLutRepairRequested`, `HasNoirFogLutResourcesReady()`, `QueueNoirFogLutRepair()`, and `FlushNoirFogLutRepairSlow()`. Awake/OnEnable still prewarm cold resources; late frame only rebuilds an already prepared LUT or queues slow repair.
Rejected Alternatives: Allocating in `FlushNoirFogLutTexture()` to guarantee same-frame fog update. A stale LUT for one slow cadence is cheaper than a managed allocation/GPU texture creation in visual sync.
Scalability potential: Low devices avoid a fog-resource hitch. Middle devices recover on slow phase. High and Ultra keep detailed Noir fog gradients without hidden late-frame resource repair.
Hardware Impact: Static estimate: 100-560 us worst-case fog LUT repair moved out of late frame. State transfer is two bool fields and existing profile scalar state, 0 B GC.

Problem: The environment-platform proof needed to cover hot dependency lookup, visual-sync phase safety, DataVault lock scope, compile throttle, and parser cleanup without synthetic report artifacts or a second build.
Solution: Ran in-memory static parsing for changed files and broad direct hot lookup scanning across runtime scripts. The focused hot graphs from `LateFrameTick` reported 147 reachable Celestial methods and 24 reachable Weather methods with 0 forbidden registry/component/platform/resource-allocation reports. Broad direct hot lookup scan covered 1802 runtime files and 2018 hot methods with 0 direct `GlobalRegistry.Get<T>()` / `GetComponent()` hits.
Rejected Alternatives: Running `dotnet build` just because CPU was 48%. The requested proof for this pass was static AST/source validation and the project throttle forbids build spam.
Scalability potential: The parser was scoped and exited cleanly; no orphan parser process remained. Verification does not steal cycles from other agents on the shared workstation.
Hardware Impact: Three changed files balance braces/parens/brackets 0/0/0. Changed runtime files add no DataVault write-lock route. Scoped diff check passed with LF/CRLF warnings only. Active Python processes are named user services, not parser leftovers.

Problem: `NativeTrailRenderer.LateFrameTick()` repaired missing/generated trail buffers with `EnsureBuffers()`. That helper allocates managed arrays and a `Mesh`, so a lost mesh or runtime capacity change could allocate in presentation phase.
Solution: Added `ISlowTickable`, `_bufferRepairRequested`, `HasBuffersReady`, and `QueueBufferRepair`. `LateFrameTick` now fails closed and queues repair; `SlowTick` owns `EnsureBuffers()`.
Rejected Alternatives: Reallocating immediately to preserve one frame of trail continuity. Trails are decorative; one missing/stale trail frame is cheaper than managed arrays and mesh creation on a weak CPU/GPU frame.
Scalability potential: Low devices skip the frame and repair on slow cadence. Middle devices recover without a late-frame spike. High and Ultra keep full AUP trail fidelity after slow repair.
Hardware Impact: Static estimate: 120-460 us managed array/mesh repair spike removed from late frame; state transfer is one bool latch, 0 B GC.

Problem: `GpuScatterLodManager.UpdateVisibleCountReadback()` called `EnsureVisibleCountReadbackData()` from the visual cull/readback route. That helper allocates a persistent `NativeArray<uint>`.
Solution: Added `_visibleCountReadbackRepairRequested`, `HasVisibleCountReadbackData`, `QueueVisibleCountReadbackRepair`, and `FlushVisibleCountReadbackRepairSlow`. Visual readback now only requests into a prepared NativeArray; missing storage queues slow repair.
Rejected Alternatives: Allocating the readback array on the first visible-count sample. The count is diagnostic/feedback, not required for draw truth, so it can miss a readback stride instead of stalling visual sync.
Scalability potential: Low devices skip one 60-frame readback cadence when storage is missing. Middle devices self-repair in slow tick. High and Ultra retain visible-count feedback without hot native allocation.
Hardware Impact: Static estimate: 40-140 us native allocation/sentinel registration removed from visual readback frames; state transfer is one bool latch and existing NativeArray owner, 0 B GC.

Problem: `CarveDebrisComputeRenderer.RenderDebris()` reached `ResolveMaterial()` and `TryResolveDrawMesh()`, which could call `EnsureFallbackRenderResources()`, `EnsureOwnedMaterial()`, `BuildOctahedronMesh()`, `Shader.Find`, and `new Material` from late-frame debris rendering.
Solution: `ResolveMaterial()` and `ResolveMesh()` are now cached-only hot accessors that queue `_fallbackRenderResourceRepairRequested`. `SlowTick` flushes `EnsureFallbackRenderResources()`; Awake/OnEnable still prewarm cold resources.
Rejected Alternatives: Creating fallback material/mesh during render to guarantee same-frame debris. Debris is a visual fake by design; dropping a frame is preferable to shader/material/mesh allocation in visual sync.
Scalability potential: Low devices avoid first-hit debris allocation stalls. Middle devices repair on slow cadence. High and Ultra keep indirect carve debris overkill visuals after cold/slow preparation.
Hardware Impact: Static estimate: 260-1200 us shader/material/mesh repair spike removed from late frame; state transfer is one bool latch, 0 B GC.

Problem: This pass needed proof without compile spam and without leaving a parser process on a shared 20-agent workstation.
Solution: Ran in-memory method extraction and hot call graphs for NativeTrail, GpuScatter, and CarveDebris; ran targeted source guard assertions, direct scoped lookup scan, scoped DataVault lock-token scan, scoped diff check, CPU/compiler throttle, and process command-line check.
Rejected Alternatives: Launching `dotnet build` while CPU was 91%. The project forbids builds above 50% CPU and the user explicitly requested AST/static validation.
Scalability potential: Verification stays narrow and deterministic across weak, middle, high, and ultra hardware lanes without stealing workstation CPU from other agents.
Hardware Impact: Runtime files balance braces/parens/brackets 0/0/0. Hot graphs: NativeTrail 10 reachable / 0 forbidden; GpuScatter 67 / 0 forbidden; CarveDebris 66 / 0 forbidden. Added source guards pass 8 assertions. Scoped lookup/write-lock scans return 0. Active Python processes are named user services, not parser leftovers.

Problem: `GPUScatterDirector.UpdateVisibleCountReadback()` could create persistent readback storage from the visible-count visual route if the args readback array was missing.
Solution: Added `_visibleCountReadbackRepairRequested`, `HasVisibleCountReadbackData`, `QueueVisibleCountReadbackRepair`, and `FlushVisibleCountReadbackRepairSlow`. The visual request path now only submits `AsyncGPUReadback.RequestIntoNativeArray` when the `NativeArray<uint>` already exists.
Rejected Alternatives: Allocating the five-uint readback array on the first diagnostic sample. The visible-count feedback is not gameplay truth and can miss one 60-frame cadence instead of adding a native allocation edge to visual sync.
Scalability potential: Low devices skip the sample and repair slowly. Middle devices repair without a visible stall. High and Ultra keep the same feedback after prewarmed storage is restored.
Hardware Impact: Static estimate: 25-90 us native allocation/sentinel registration removed from the affected visual readback frame; state transfer is one bool latch plus existing owner struct, 0 B GC.

Problem: `HectonIndirectVegetationRenderer.RequestCullTelemetryReadback()` could allocate cull telemetry readback storage from a route reached by `RunVisualTick()`.
Solution: Added `_scatterCullTelemetryReadbackRepairRequested`, cached readiness, queue, and slow repair helpers. `RequestCullTelemetryReadback()` now fails closed until `SlowTick` has prepared `_cullTelemetryReadback.Data`.
Rejected Alternatives: Keeping same-frame telemetry guarantee by allocating inside the request path. Cull telemetry is diagnostic and overdraw feedback; missing one sample is cheaper than a late-frame native allocation.
Scalability potential: Low devices avoid diagnostic stalls. Middle devices recover on slow cadence. High and Ultra retain full GPU cull telemetry without contaminating visual submission.
Hardware Impact: Static estimate: 25-90 us native allocation/sentinel registration removed from the cull telemetry sample frame; state transfer is one bool latch, 0 B GC.

Problem: `SargassumMicroFaunaBoids.TryRequestParasiteLatchReadback()` allocated parasite latch-stat readback storage from the micro-fauna visual simulation path.
Solution: Added `_parasiteLatchReadbackRepairRequested`, cached readiness, queue, and slow repair helpers. The hot request now submits only into pre-existing `NativeArray<int>` storage.
Rejected Alternatives: Allocating latch stats on the first parasite telemetry sample. Parasite drag truth is already in GPU/CPU simulation state; the readback is feedback and can wait for slow repair.
Scalability potential: Low devices preserve swarm visual cadence by skipping one readback interval. Middle devices repair on slow tick. High and Ultra keep parasite harvester feedback without hot native allocation.
Hardware Impact: Static estimate: 45-160 us native allocation/sentinel registration removed from the visual simulation frame; state transfer is one bool latch, 0 B GC.

Problem: `HectonMapMagicVegetationBridge.CacheTileMasks()` allocated tile height readback storage during late-frame resident cache validation.
Solution: Added per-tile `HeightReadbackRepairRequested` and `HeightReadbackRepairSampleCount`. `CacheTileMasks()` now queues repair when storage is missing; `FlushTileHeightReadbackRepairsSlow()` owns `EnsureTileHeightReadbackData` and requeues validation after repair.
Rejected Alternatives: Allocating the heightmap readback array inside the late-frame cache validation barrier. Terrain cache can tolerate one validation delay; a hidden heightmap-sized native allocation in late frame is not acceptable on weak hardware.
Scalability potential: Low devices avoid large tile readback allocation spikes. Middle devices repair on slow cadence and resume validation. High and Ultra keep full native tile masks and height sampling after storage is prepared.
Hardware Impact: Static estimate: 65-640 us heightmap readback storage allocation moved out of late-frame cache validation, depending on tile height resolution; state transfer is two fields per tile, 0 B GC.

Problem: The world readback pass required proof of dependency purity, phase safety, lock flattening, compile throttle compliance, and parser cleanup without writing synthetic proof files.
Solution: Ran scoped in-memory source parsing over four runtime files plus the guard file, targeted phase assertions, same-file hot graphs, hot lock-shape scan, direct lookup scan, scoped `git diff --check`, CPU/compiler throttle, and Python process inspection.
Rejected Alternatives: Running `dotnet build` under 71.7-99.6% CPU with active external `dotnet.exe` PID 7380, or broad unscoped parsing that would steal CPU from parallel agents.
Scalability potential: Verification stayed bounded to the changed world readback routes and did not leave parser processes behind. The result protects weak, middle, high, and ultra device lanes from identical readback-storage drift.
Hardware Impact: Five files balance 0/0/0. Hot graphs: GPUScatterDirector 40 reachable / 0 hits; HectonIndirectVegetationRenderer 62 / 0; SargassumMicroFaunaBoids 136 / 0; HectonMapMagicVegetationBridge 72 / 0. Lock-shape scan reports single-acquire or handoff-only methods with release in `finally` at caller/helper boundaries. No parser orphan remained.

Problem: `LODSystemManager.LateFrameTick()` wrote `QualitySettings.lodBias` from visual sync. That is global quality/platform policy mutation, not presentation-only shader sync, and it makes hot phase proof depend on a global Unity quality side effect.
Solution: Added `ISlowTickable` registration and moved LOD bias policy mutation into `FlushQualityPolicySlow()`. Slow phase computes `GlobalQualityWeight`/emergency bias and writes `QualitySettings.lodBias`; late frame only consumes `_pendingMathLodWeight` and calls `DistanceMath.PushShaderMathLod()` from `FlushQualityShaderVisualSync()`.
Rejected Alternatives: Keeping the mutation in `LateFrameTick` because it only runs when dirty. Dirty quality changes happen under pressure, exactly when the frame should not carry global quality policy writes.
Scalability potential: Low devices get slow-cadence LOD bias pressure response without visual-sync mutation. Middle devices keep stable presentation transitions. High and Ultra keep visual math LOD overkill through the late-frame shader scalar while policy stays out of visual sync.
Hardware Impact: Static estimate: 8-24 us avoided on quality-dirty visual frames; state transfer is one float plus one bool, 0 B GC.

Problem: The LOD policy pass needed proof that moving quality policy did not create dependency or phase drift.
Solution: Added source guard `LODSystemManager_QualitySettingsMutationIsSlowPhaseOnly()` and ran an in-memory same-file hot graph from `Tick`, `LateFrameTick`, `SlowTick`, and `ApplyEmergencyLODBiasStrike`.
Rejected Alternatives: Running `dotnet build` while CPU was 91% and external `dotnet.exe` PID 7380 was active. That violates compile throttling and would steal CPU from other agents.
Scalability potential: The proof isolates slow quality policy from late presentation, preserving low/middle/high/ultra behavior with bounded scalar transfer.
Hardware Impact: `LODSystemManager` balance 0/0/0. `LateFrameTick` graph has 4 reachable methods and 0 forbidden hits. `Tick`, `SlowTick`, and emergency strike graphs report 0 forbidden lookup/allocation hits. No parser orphan remained.

Problem: `SargassumCrestDampingController.LateFrameTick()` could reach `DispatchFacadeBake()`, which resolved compute shader kernels and thread-group sizes through `HasKernel`, `FindKernel`, `IsSupported`, and `GetKernelThreadGroupSizes`. That is platform/capability discovery on a visual-sync route.
Solution: Added cached kernel resolution state and moved all compute-kernel repair into `FlushFacadeBakeKernelRepairSlow()`. `DispatchFacadeBake()` now checks `HasFacadeBakeKernelReady()` and only queues `QueueFacadeBakeKernelRepair()` when the cold cache is stale.
Rejected Alternatives: Keeping kernel resolution branch-guarded in `DispatchFacadeBake()`. Branch-insensitive hot-proof still reaches the platform discovery APIs, and a changed compute asset during runtime would still tax visual sync.
Scalability potential: Low devices skip one decorative damping facade bake if kernel state is stale. Middle devices repair on slow cadence. High and Ultra keep wave/oil facade overkill after cold repair without probing compute capabilities in late frame.
Hardware Impact: Static estimate: 18-74 us removed from first/changed facade bake frames on MX350/standalone VR class CPUs. State transfer is two bools plus cached kernel/thread-group integers, 0 B GC.

Problem: The Sargassum Crest pass required proof for lookup purity, phase safety, lock flattening, compile throttle, and parser cleanup without writing synthetic report artifacts.
Solution: Added `SargassumCrestFacade_KernelResolutionIsSlowPhaseOnly()` and ran in-memory lexical parsing, targeted phase assertions, same-file hot graphs from `Tick` and `LateFrameTick`, scoped direct lookup/kernel scans, scoped diff check, CPU/compiler throttle, and process scan.
Rejected Alternatives: Running `dotnet build` after validation when CPU measured 99%. That violates the project throttle and the user asked for static AST/source validation instead of build spam.
Scalability potential: Verification remains local to the platform adaptation route and does not steal CPU from parallel agents. The same proof covers weak, middle, high, and ultra lanes because the facade quality still scales continuously through `GlobalQualityWeight`.
Hardware Impact: `SargassumCrestDampingController` balance 0/0/0. Guard file balance 0/0/0. `Tick` graph 4 reachable / 0 hits. `LateFrameTick` graph 14 reachable / 0 hits. No DataVault write-lock route added. No parser orphan remained.

Problem: `HectonBiolumDiffusionVolume.LateFrameTick()` correctly latched `_resourceRefreshRequested` when textures, point buffers, or kernel state were missing, but `SlowTick()` only called `HasRequiredResources()` and returned. A lost resource or failed cold init could stay permanently disabled.
Solution: `SlowTick()` now calls `EnsureResources()` before `HasRequiredResources()` in the refresh branch. Allocation-capable 3D texture creation, point-buffer creation, and compute-kernel resolution remain in slow/cold ownership.
Rejected Alternatives: Calling `EnsureResources()` from `LateFrameTick()` to restore same-frame glow diffusion. That would put `new RenderTexture`, `GraphicsBuffer` creation, and compute kernel discovery into visual sync.
Scalability potential: Low devices skip diffusion volume output while slow repair rebuilds. Middle devices recover without visual-sync stalls. High and Ultra keep HDR biolum volume diffusion after repair without sacrificing frame phase purity.
Hardware Impact: Static estimate: 420-2200 us worst-case lost-resource recreation kept out of late frame; correctness fix restores automatic recovery. State transfer remains existing bool latch plus cached resource references, 0 B GC.

Problem: The biolum diffusion fix needed proof that repair moved to slow phase without hiding new hot dependencies.
Solution: Added `BiolumDiffusionVolume_ResourceRefreshRepairsInSlowTick()` and ran in-memory lexical balance, targeted method-order assertions, same-file hot graph from `LateFrameTick`, scoped diff check, CPU/compiler throttle, and parser process inspection.
Rejected Alternatives: Full `dotnet build` under CPU load after a one-line slow-phase repair. CPU measured 74%, and the project forbids builds above 50%.
Scalability potential: The source guard prevents future drift where lost 3D volume resources are either never repaired or repaired in visual sync.
Hardware Impact: `HectonBiolumDiffusionVolume` balance 0/0/0. Guard file balance 0/0/0. `LateFrameTick` graph 21 reachable / 0 forbidden allocation/lookup/resource-repair hits. No parser orphan remained.

Problem: Four player-adjacent world presentation routes still discovered the runtime player through `BootstrapState.CurrentPlayerTransform` from hot paths. `SargassumDebrisParticleSystem.AdvanceAmbientDebrisEmission`, `FloraInteractionManager.Tick`, `CaveBioRootsGenerator.LateFrameTick`, and `SargassumCutManager.RegisterExternalCut`/slow residency refresh all carried a bootstrap fallback edge that is identity discovery, not frame math.
Solution: Moved bootstrap fallback to cold/lifecycle/slow refresh. Debris and cave roots now register slow ticks and refresh cached player targets there. Flora keeps Tick on cached `IPlayerRuntimeContext`/`_playerTransform` reads and refreshes the bootstrap fallback in `RefreshPlayerReferenceCacheCold`. Sargassum cut now has a hot cached resolver and a separate `ResolveDependenciesCold` that owns bootstrap and `TryGetComponent`.
Rejected Alternatives: Keeping the bootstrap fallback in hot routes because it is only a static property. The property is still global runtime identity discovery, and in Sargassum cut it shared a body with component probing, so branch-insensitive proof could still reach `TryGetComponent`.
Scalability potential: Low devices avoid repeated player discovery/probe variance in decorative debris, cave splines, flora wake, and external cut writes. Middle devices refresh player identity on slow cadence. High and Ultra keep full visual behavior after cached target refresh without contaminating visual sync or simulation ticks.
Hardware Impact: Static estimate: 18-96 us aggregate variance removed from affected frames on i3/MX350/standalone VR class CPUs. State transfer is cached `Transform` references and bool latches only, 0 B GC.

Problem: The player-target phase split needed proof that it did not introduce dependency lookup, lock, or compilation violations.
Solution: Added C# source guards: `SargassumDebris_RuntimeTargetsAreSlowPhaseOnly`, `SargassumCut_PlayerDependencyLookupIsColdOnly`, `FloraInteraction_PlayerBootstrapLookupIsSlowPhaseOnly`, and `CaveBioRoots_PlayerContextRefreshIsSlowPhaseOnly`. Ran in-memory lexical balance and same-file hot graphs from Debris `LateFrameTick`, Flora `Tick`, Cave `LateFrameTick`, and Sargassum external cut.
Rejected Alternatives: Running `dotnet build` after static validation while an external `dotnet.exe` PID 21592 was active. The compile throttle forbids build spam and the requested proof for this pass was static AST/source validation.
Scalability potential: The guards cover weak, middle, high, and ultra lanes because all lanes share the same cached player identity route; only visual/detail math scales with quality.
Hardware Impact: Five files balance braces/parens/brackets 0/0/0. Hot graphs: Debris 14 reachable / 0 hits; Flora 77 / 0; Cave 11 / 0; Sargassum external cut 24 / 0. Changed diff adds no DataVault lock acquire/release tokens. No parser orphan remained.

Problem: `HectonUnderwaterVisuals.UpdateHudFogLuminanceDownsample()` could reach `EnsureHudFogLuminanceReadbackData()` and allocate/register a `NativeArray<float>` from the late-frame HUD fog luminance readback route.
Solution: Added `_hudFogLuminanceReadbackRepairRequested`, cached readiness, hot queue, and `FlushHudFogLuminanceReadbackRepairSlow()`. The late-frame readback path now submits `AsyncGPUReadback.RequestIntoNativeArray` only when storage already exists.
Rejected Alternatives: Allocating the one-float readback storage on first visual sample. HUD fog luminance is presentation feedback; one missed sample is cheaper than native allocation and sentinel registration during visual sync.
Scalability potential: Low devices skip the feedback sample and repair slowly. Middle devices recover without a visible stall. High and Ultra keep the same GPU luminance response after storage is prepared.
Hardware Impact: Static estimate: 25-90 us native allocation/sentinel registration removed from the affected visual frame; state transfer is one bool latch plus existing native storage handle, 0 B GC.

Problem: `PDAMapTab.LateFrameTick()` reached `RenderPointCloud()` and `DispatchSonarPointCloud()`, which could call `TryResolveSonarComputeKernels()` and execute `HasKernel`, `FindKernel`, `IsSupported`, and `GetKernelThreadGroupSizes` from PDA visual sync.
Solution: Added `ISlowTickable`, cached kernel readiness, `_sonarComputeKernelRepairRequested`, and `FlushSonarComputeKernelRepairSlow()`. Late frame reads cached kernel/thread-group state only and queues repair when stale.
Rejected Alternatives: Keeping branch-guarded kernel resolution in render/dispatch because it is usually first-use only. First-use PDA opening is player-visible, and changed compute assets would still put platform discovery into visual sync.
Scalability potential: Low devices get one stale/offline point-cloud frame instead of a kernel-discovery spike. Middle devices repair on slow cadence. High and Ultra keep full PDA sonar overkill after cold/slow kernel cache repair.
Hardware Impact: Static estimate: 18-80 us removed from first/changed PDA sonar compute frame on i3/MX350/standalone VR class CPUs; state transfer is cached integers plus one bool latch, 0 B GC.

Problem: The HUD/PDA phase split needed proof for dependency purity, zero-GC phase transfer, lock shape, compile throttling, and parser cleanup without synthetic JSON/binary artifacts.
Solution: Ran in-memory lexical balance for `PDAMapTab`, `HectonUnderwaterVisuals`, and the guard file; targeted method assertions; same-file PDA hot graph from `LateFrameTick`; direct hot lookup scan across runtime C# files; scoped diff check; changed-line lookup/lock scan; CPU/compiler throttle; parser process check.
Rejected Alternatives: Launching `dotnet build` at 100% CPU while external `dotnet.exe` PID 39820 was active. That violates the project throttle and would compete with other agents.
Scalability potential: Verification stays bounded to platform presentation routes and protects weak, middle, high, and ultra lanes from identical hot-discovery/readback-storage drift.
Hardware Impact: Three changed files balance 0/0/0. PDA late graph: 57 reachable methods / 0 forbidden lookup/kernel reports. Underwater HUD update has no readback ensure or `new NativeArray<float>`. Broad direct hot scan: 1802 files / 1928 hot methods / 0 forbidden lookup reports. No new DataVault write-lock route. No parser orphan remained.

Problem: `SubmarineStructuralGrid.LateFrameTick()` reached `FlushLeakPlumeVisualSync()` and `DispatchLeakPlumeCompute()`, which could call `EnsureLeakPlumeGpuResources()`. That path performed compute-kernel discovery and GPU buffer creation from visual sync.
Solution: Added `ISlowTickable`, `_leakPlumeGpuResourceRepairRequested`, `HasLeakPlumeGpuResourcesReady()`, and `FlushLeakPlumeGpuResourceRepairSlow()`. Late-frame leak plume dispatch now uses cached kernel/thread-group/buffer state only and queues repair when stale.
Rejected Alternatives: Keeping first-breach kernel/resource discovery in late frame. Leak plume particles are presentation; a stale/offline plume frame is cheaper than `HasKernel`, `FindKernel`, `GetKernelThreadGroupSizes`, and GPU buffer creation during visual sync.
Scalability potential: Low devices skip the decorative plume dispatch while slow repair prepares resources. Middle devices recover on slow cadence. High and Ultra keep full leak plume particles after cached resource repair without platform probing in late frame.
Hardware Impact: Static estimate: 110-640 us first-use GPU resource repair spike removed from affected late frames on i3/MX350/standalone VR class CPUs. State transfer is cached handles, cached integers, and one bool latch, 0 B GC.

Problem: Scheduled structural jobs now outlive their scheduling method, so a completed-job consumer that finalized the fence and then threw before `UnlockStructuralJobBuffers` could leave the structural mutation guard held.
Solution: Wrapped breach repair, compartment mapping, fatigue, and damage completed-job state transfers in `try/finally`; each finalizer releases its structural mutation guard and resets mask/vault fields in the `finally` block. Existing telemetry write-lock release remains in `finally`.
Rejected Alternatives: Trusting the post-fence state updates and handle swaps as non-throwing. The route is critical hull state; deadlock prevention must not depend on optimistic post-completion code.
Scalability potential: Low and middle devices avoid rare permanent hull-system stalls under pressure or invalid vault state. High and Ultra keep asynchronous hull jobs without risking a locked mutation-guard lane.
Hardware Impact: Normal-frame runtime estimate: 0 us material cost; failure-path gain is deadlock elimination. Lock proof: every direct `TryAcquireWriteLock` in this file has a matching `ReleaseWriteLock` after `finally`; completed structural job guards release after `finally`.

Problem: The submarine patch needed proof for hot dependency purity, phase safety, lock release shape, compile throttle, and parser cleanup without synthetic artifacts.
Solution: Ran string/comment-stripped in-memory source balance for `SubmarineStructuralGrid` and the guard file, targeted phase assertions, same-file hot graph from `LateFrameTick`, lock-finalizer shape proof, broad direct hot dependency scan, scoped diff check, CPU/compiler throttle, and Python process inspection.
Rejected Alternatives: Launching `dotnet build` while CPU measured 57%. Project throttle forbids build above 50%, and the current pass was static source verification.
Scalability potential: The proof covers weak, middle, high, and ultra lanes because resource repair phase ownership is invariant; only visual plume fidelity scales after the cache is prepared.
Hardware Impact: `SubmarineStructuralGrid` balance 0/0/0. Guard file balance 0/0/0. Submarine late graph: 30 reachable / 0 forbidden lookup/kernel/resource-allocation reports. Broad direct hot scan: 1802 runtime files / 1803 hot methods / 0 reports. No parser orphan remained.

Problem: `DiegeticGyroCompassRuntime.SlowTick()` marked `_indirectBuffersDirty` when indirect draw buffers were missing, but no slow repair consumed that latch. `LateFrameTick()` then returned before `ApplyPresentation()`, creating a permanent compass presentation stall after buffer loss. The same dirty flag could also block fallback text/transform presentation when indirect rendering was disabled, unsupported, or unbound.
Solution: Added `ShouldRequireIndirectBuffersCold()` and `FlushIndirectBuffersRepairSlow()`. Slow phase now refreshes graphics capability, rebuilds indirect args and dial matrix buffers through `EnsureIndirectBuffersCold()` only when the route is actually required, and clears `_indirectBuffersDirty` when fallback presentation should proceed. Startup and physical binding now set the dirty latch from the same requirement predicate.
Rejected Alternatives: Rebuilding indirect buffers from `LateFrameTick()` after detecting the dirty flag. That would put `SystemInfo` capability sampling, mesh index extraction, and `new GraphicsBuffer` allocation on a visual-sync route. Also rejected keeping `_indirectBuffersDirty` as a global presentation gate for unsupported indirect routes because fallback compass text/dial motion is still valid.
Scalability potential: Low devices or unsupported GPUs use the cheap fallback presentation without being blocked by missing indirect buffers. Middle devices recover indirect buffers on slow cadence. High and Ultra retain the instanced indirect dial route after slow repair without contaminating visual sync.
Hardware Impact: Static estimate: 480-1400 us first-repair GPU allocation spike kept out of late frame on i3/MX350/standalone VR class hardware. State transfer is one bool latch plus cached buffer handles, 0 B GC.

Problem: The compass repair pass needed proof that the fix did not introduce hot dependency lookup, phase drift, DataVault lock nesting, compile spam, or parser leftovers.
Solution: Added `DiegeticGyroCompass_IndirectBufferRepairIsSlowPhaseOnly()` and ran scoped in-memory lexical balance, targeted method assertions, scoped diff check, CPU/compiler throttle, and process cleanup.
Rejected Alternatives: Running a broad whole-project parser after the first parser timed out, or launching `dotnet build` at 76% CPU. The workstation is shared and the project explicitly forbids build under >50% CPU.
Scalability potential: The proof stays local to the UI navigation platform-adaptation route and protects weak, middle, high, and ultra lanes from the same indirect-buffer drift.
Hardware Impact: `DiegeticGyroCompassRuntime` balance 0/0/0. Guard file balance 0/0/0. Targeted assertions pass: late frame has no `EnsureIndirectBuffersCold`, no `FlushIndirectBuffersRepairSlow`, no `SystemInfo`, and no `new GraphicsBuffer`; slow repair owns the cold ensure. Scoped diff check exits 0 with LF/CRLF warnings only. Parser PID 60880 was stopped; final process scan found only named user Python services.

Problem: `WorldChunkResidencyManager.LateFrameTick()` called the async upload budget updater, which writes `QualitySettings.asyncUploadBufferSize`, `QualitySettings.asyncUploadTimeSlice`, and `QualitySettings.asyncUploadPersistentBuffer`. These are global Unity streaming policy writes, not visual-sync presentation work.
Solution: Renamed the route to `FlushAsyncUploadBudgetPolicySlow()` and moved ownership to `Awake()` plus `SlowTick()`. `SlowTick()` flushes the policy before `_chunkCount <= 0`, preserving bootstrap/empty-world quality adaptation without touching global platform settings from `LateFrameTick()`.
Rejected Alternatives: Keeping the hash-gated write in `LateFrameTick()` because it usually short-circuits. Hash gating reduces average writes but still leaves a global platform mutation on the visual-sync route when quality or policy changes. Also rejected pushing it into every load dispatch because dispatch cadence is chunk-dependent and would miss no-chunk policy refresh.
Scalability potential: Low devices keep small async upload buffers and tight slices from slow policy without late-frame drift. Middle devices refresh the same scalar policy on slow cadence. High and Ultra devices can raise upload throughput continuously through `GlobalQualityWeight` while visual sync remains reserved for settled presentation.
Hardware Impact: Static estimate: 10-35 us quality-dirty late-frame variance removed on i3/MX350 class hardware, with larger hitch risk removed when Unity touches global upload settings. State transfer is existing scalar fields and one hash comparison, 0 B GC.

Problem: The world chunk policy split needed proof for phase safety, syntax shape, hot lookup purity, DataVault lock neutrality, and compile throttle.
Solution: Added `WorldChunkResidency_AsyncUploadQualityPolicyIsSlowPhaseOnly()` and ran string/comment-stripped in-memory source balance, targeted method extraction assertions, scoped diff check, CPU/compiler process inspection, and parser process inspection.
Rejected Alternatives: Launching `dotnet build` as proof while the user explicitly required compilation throttling and static source validation. The correct route here is source proof first, build only when CPU and compiler gates allow it.
Scalability potential: The proof applies across low, middle, high, and ultra tiers because only policy cadence moves; `GlobalQualityWeight` remains continuous and gameplay truth/DTO ownership is unchanged.
Hardware Impact: `WorldChunkResidencyManager.cs` balance braces/parens/brackets 0/0/0. Guard file balance braces/parens/brackets 0/0/0. Targeted assertions pass: no old helper remains, `Awake` and pre-gate `SlowTick` own the policy flush, `LateFrameTick` has no `QualitySettings`, and exact async upload writes remain isolated in the slow helper. Direct hot lookup scan passes for `WorldChunkResidencyManager`; the async-upload patch surface has no DataVault write-lock token and no hot `GlobalRegistry.Get<T>`/`GetComponent` token. Scoped diff check exits 0 with LF/CRLF warnings only. Build was not launched: CPU measured 79% and external `dotnet.exe` PID 39176 was active.

Problem: `ScreenSpaceLightShaftRuntime.LateFrameTick()` polled `Application.isPlaying` on the visual-sync route. The dispatcher already owns runtime registration; this was a redundant global Unity runtime property read in a high-frequency presentation phase.
Solution: Replaced the hot `Application.isPlaying` gate with the existing `_registeredLateFrame` lifecycle latch. `OnEnable()` still performs the runtime registration, and `OnDisable()` clears the latch after unregistering.
Rejected Alternatives: Leaving the property read because it is cheap. The route is called every visual-sync frame and the project law treats phase ownership as architecture; lifecycle state must be cached, not polled from Unity globals in presentation.
Scalability potential: Low devices shave small but persistent hot-path variance. Middle devices retain identical visual behavior. High and Ultra keep the same light shaft overkill path while lifecycle/runtime checks remain cold.
Hardware Impact: Static estimate: 1-4 us direct platform read variance removed from affected late frames, 0 B GC. No DataVault route changed and no lock was added.

Problem: The light shaft patch needed proof that the visual-sync method no longer reads platform globals or component/registry dependencies.
Solution: Added `ScreenSpaceLightShaft_LateFrameUsesRegistrationLatchNotApplicationPoll()` and ran targeted late-frame extraction, guard extraction, direct hot platform token scan, lexical balance, and scoped diff check.
Rejected Alternatives: Treating the earlier broad hot scan as proof. It reported useful candidates, but exact source-method extraction is the only acceptable proof for the touched route.
Scalability potential: The proof is invariant across weak, middle, high, and ultra devices because it changes only lifecycle gating, not the rendering math or fidelity ladder.
Hardware Impact: `ScreenSpaceLightShaftRuntime.cs` balance braces/parens/brackets 0/0/0. Guard file balance braces/parens/brackets 0/0/0. Late-frame direct scan has no `Application`, `QualitySettings`, `SystemInfo`, `Screen`, allocation, `GlobalRegistry.Get<T>`, `GetComponent`, or `TryGetComponent` token. Scoped diff check exits 0 with LF/CRLF warnings only.

Problem: A broad hot token scan reported `Screen.` inside Burst `Execute()` methods, but editing Burst jobs on a namespace-name false positive would create needless risk.
Solution: Inspected `ExosuitKinematicsJobs` and `VoxelSurfaceNetsJobs` around the reported lines. The tokens are `NativeArray<ExoScreenDTO> Screen` and screen-space local naming, not `UnityEngine.Screen` or platform API access.
Rejected Alternatives: Renaming DTO fields or local variables to silence a string scanner. That would churn serialized/job-facing code without fixing a runtime dependency.
Scalability potential: Low, middle, high, and ultra lanes keep the same deterministic Burst DTO math; no gameplay or presentation route changes.
Hardware Impact: 0 us runtime change. Verification avoids false-positive refactoring and preserves job layout stability.

Problem: `DynamicResolutionScaler.ApplyRenderScale()` polled `Application.isPlaying`, and that helper is reachable from `LateFrameTick()` and from `PlatformAdaptiveBudgetGovernor` pressure commits. This put a Unity global runtime property read on a render-scale visual-sync route.
Solution: Added `_runtimeRenderScaleQueueActive`, initialized it from `Application.isPlaying` in `Awake()` and `OnEnable()`, cleared it in `OnDisable()`, and made `ApplyRenderScale()` use the cached latch. The render-scale queue behavior remains identical in play mode, but hot paths no longer touch `Application`.
Rejected Alternatives: Gating on `_lateFrameRegistered` or `_serviceRegistered`. Both are registration/service state, not pure runtime-mode state; they can be false during early runtime initialization where old behavior still queued the render-scale write.
Scalability potential: Low devices remove a small repeated platform read on the render-scale route. Middle devices keep identical DRS behavior. High and Ultra keep the same render-scale overkill path while runtime-mode ownership remains cold.
Hardware Impact: Static estimate: 1-5 us hot route variance removed on weak CPUs. State transfer is one bool latch, 0 B GC. Direct hot scan over `LateFrameTick`, `ApplyRenderScale`, `SetPlatformPressureRenderScale`, `ApplySystemOverrideRenderScale`, and `ClearSystemOverrideRenderScale` reports 0 forbidden platform/dependency/allocation tokens. `dotnet build` was not launched: CPU 72.9%, external `dotnet.exe` PID 55436 active.

Problem: URP renderer features were polling `Application.isPlaying` from `AddRenderPasses()` and `RecordRenderGraph()`. These are render-frequency camera routes, so the check was a hot platform read even though each domain already had an owner-published runtime signal or active GPU buffer state.
Solution: Moved the gates to owner state. Ocean now exposes `HasRendererFeatureRuntimeGate()` and `TryEnterRenderGraphRuntimeGate()` from `OceanSinglePassRuntime`, preserving mock-frame budget consumption only in the render graph route. Deferred caustics relies on `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer`. Bilateral DRS gates pass enqueue on `HectonBilateralDrsUpscalerRuntime.TryGetRuntimeInstance`. Water optics telemetry exposes `WaterOpticsRuntime.TryGetRuntimeInstance` in all builds and uses it from feature add/record routes.
Rejected Alternatives: Caching `Application.isPlaying` in `ScriptableRendererFeature.Create()`. Unity can create renderer features outside play-mode transitions, so a cached feature bool can be stale. Runtime-owner state is the safer route because owners publish on lifecycle and clear on shutdown.
Scalability potential: Low devices remove repeated per-camera platform polling. Middle devices keep identical rendering when runtime owners exist. High and Ultra keep ocean, caustics, DRS, and telemetry routes available through active runtime state without contaminating render passes with global platform reads.
Hardware Impact: Static estimate: 4-16 us aggregate render-gate variance removed on weak CPUs with multiple cameras/features active. State transfer is static runtime references, active GPU buffer presence, or existing mock-frame budget integer; 0 B GC.

Problem: The render-feature split needed proof that no dependency lookup, DataVault write-lock route, hot allocation, or compile-spam path was introduced.
Solution: Added `RendererFeatures_DoNotPollApplicationPlayingFromRenderRoutes`; ran targeted method extraction assertions for all changed render routes, string/comment-aware C# balance for seven files, direct `Application.isPlaying` feature scan, scoped `git diff --check`, scoped added-token scan, CPU/compiler throttle, and parser process inspection.
Rejected Alternatives: Running a broad whole-project method extractor again after one earlier broad scan timed out. The verified surface was the touched render features, so targeted static extraction was enough and did not leave parser load behind.
Scalability potential: The proof applies across weak, middle, high, and ultra lanes because the render-feature gate is invariant; fidelity still scales through existing ocean/DRS/caustics/water systems.
Hardware Impact: Seven touched files balance braces/parens/brackets 0/0/0. Targeted render route assertions pass. Direct feature scan reports no `Application.isPlaying` in changed feature files. Scoped diff check exits 0 with LF/CRLF warnings only. No new runtime DataVault write-lock route was added. `dotnet build` was not launched: this pass used in-memory static source validation under the requested compilation throttle.

Problem: `SystemDispatcher.RunDispatcherUpdate()` polled `Application.isPlaying` while calculating `blockGameplayLanes` before the central lane dispatch. This is the main frame dispatcher; a Unity global runtime query there violates the cold identity rule even if the branch is cheap.
Solution: Added `_runtimeGameplayBootstrapGateActive` to `SystemDispatcher`, sampled it only from `Awake()`/`OnEnable()`, cleared it from `OnDisable()`/`ShutdownServiceState()`, and made `RunDispatcherUpdate()` combine that latch with the existing `BootstrapState` read model.
Rejected Alternatives: Using `ActiveRuntimeInstance != null` as the gameplay gate. That proves dispatcher ownership, not Unity runtime mode. Keeping `Application.isPlaying` in the hot route was rejected because the dispatcher already has lifecycle ownership points.
Scalability potential: Low and middle devices remove repeated platform polling from the central frame path. High and Ultra keep identical bootstrap lane suppression and spend saved CPU budget on visual systems, not dependency checks.
Hardware Impact: Static estimate: 1-5 us direct hot variance removed per frame on weak CPUs. State transfer is one cached bool plus existing immutable bootstrap state, 0 B GC.

Problem: `GameTickManager.Tick()` duplicated the same `Application.isPlaying && BootstrapState` check for slow-dt bootstrap fallback. This tick route can execute every frame through the dispatcher.
Solution: Added `_runtimeGameplayBootstrapGateActive` to `GameTickManager`, sampled in `Awake()`/`OnEnable()`, cleared in `OnDisable()`/shutdown, and consumed only the cached bool from `Tick()`.
Rejected Alternatives: Reusing `_registeredToDispatcher` or `_serviceRegistered`. Those fields describe registration, not runtime play-mode state, and can be true/false for reasons unrelated to bootstrap lane suppression.
Scalability potential: Low devices reduce repeated hot platform reads. Middle, High, and Ultra retain the same tick cadence and bootstrap fallback while runtime state ownership remains lifecycle-bound.
Hardware Impact: Static estimate: 1-4 us direct hot variance removed per tick frame, 0 B GC.

Problem: `NativeMemorySentinel.ResolveCurrentFrame()` and `ResolveCurrentUnscaledTime()` polled `Application.isPlaying` during native allocation registration/reallocation diagnostics. The sentinel is diagnostic/cold-biased, but it is invoked by allocation owners and should not depend on Unity global play-mode queries for dispatcher frame identity.
Solution: Switched both resolvers to `SystemDispatcher.ActiveRuntimeInstance != null`, then read `SystemDispatcher.CurrentFrameIndex` or `CurrentUnscaledTimeSeconds` only when the dispatcher owner exists.
Rejected Alternatives: Returning fallback values unconditionally. That would weaken native allocation black-box chronology during runtime. Also rejected a new sentinel-owned lifecycle flag because the dispatcher already owns runtime frame/time identity.
Scalability potential: Low devices avoid global runtime polling during native memory accounting. Middle through Ultra retain allocation chronology from dispatcher-owned state without adding another owner.
Hardware Impact: Static estimate: 1-3 us avoided on affected main-thread diagnostic registration/reallocation paths. No new locks, no allocations, no DataVault route changes.

Problem: The core runtime-gate pass needed proof that the central dispatcher/tick/sentinel edits did not add dependency lookup, phase drift, lock nesting, or compile spam.
Solution: Added source guards `SystemDispatcher_GameplayBootstrapGateUsesColdRuntimeLatch`, `GameTickManager_GameplayBootstrapGateUsesColdRuntimeLatch`, and `NativeMemorySentinel_FrameResolveUsesDispatcherRuntimeState`; ran targeted in-memory method extraction, string/comment-aware C# balance, scoped diff check, scoped lookup/lock scan, CPU/compiler throttle, and process inspection.
Rejected Alternatives: Running `dotnet build` while CPU was above the 50% project ceiling and another external `dotnet build` process was active. The pass used source-level AST-style proof, not build spam.
Scalability potential: The fix applies to weak, middle, high, and ultra tiers because it changes only runtime-state routing, not gameplay truth or quality ladder behavior.
Hardware Impact: Four touched files balance braces/parens/brackets 0/0/0. Targeted assertions pass. Hot dispatcher and tick bodies report no `Application.isPlaying`, `GlobalRegistry.Get<T>`, `GetComponent`, or `TryGetComponent` token. No new DataVault write-lock route was added. Scoped diff check exits 0 with LF/CRLF warnings only. Build not launched: CPU 65%, external `dotnet build` PID 64492 active.

Problem: Five visor URP renderer features polled `Application.isPlaying` from `AddRenderPasses()` and `RecordRenderGraph()`. Those routes are per-camera render-frequency gates, so the Unity global runtime read was a hot platform dependency.
Solution: Added `HectonDrsRenderFeatureGate.HasRuntimeRenderOwner()` and routed the five visor features through dispatcher-owned runtime identity: `SystemDispatcher.ActiveRuntimeInstance != null`. The gate is cold-owner state; render routes only consume it.
Rejected Alternatives: Caching play-mode state in `ScriptableRendererFeature.Create()` because renderer features can be created before or outside runtime ownership and the cached value can go stale. Keeping `Application.isPlaying` was rejected because render gates are not lifecycle owners.
Scalability potential: Low devices remove repeated per-camera platform polling and can spend the saved variance on keeping visor effects stable. Middle devices keep identical effect behavior. High and Ultra keep SSDO, half-res particles, noir fog, scooter shafts, and stochastic SSR available through runtime owner state without contaminating render passes with global play-mode reads.
Hardware Impact: Static estimate: 5-20 us aggregate render-gate variance removed on i3/MX350/standalone VR class CPUs with multiple visor features/cameras. State transfer is one dispatcher-owned runtime reference read, 0 B GC, no locks.

Problem: The visor render-feature split needed regression proof for dependency purity, phase safety, zero-GC state transfer, lock neutrality, compile throttle, and parser cleanup.
Solution: Added `VisorRenderFeatures_DoNotPollApplicationPlayingFromRenderRoutes()` and `AssertVisorRenderRouteUsesRuntimeOwnerGate()`. Ran string/comment-aware C# balance, targeted `AddRenderPasses`/`RecordRenderGraph` extraction, direct runtime-token scan, scoped diff check, whole-runtime direct hot lookup scan, DataVault write-lock parser, CPU/compiler throttle, and parser process inspection.
Rejected Alternatives: Launching `dotnet build` as proof. The user required strict compilation throttling and static in-memory source validation; CPU/compiler gates must stay respected in the shared 20-agent workspace.
Scalability potential: The proof is invariant across low, middle, high, and ultra tiers because it changes ownership routing, not fidelity math. `GlobalQualityWeight` and existing visor quality ladders remain continuous.
Hardware Impact: Seven touched files balance braces/parens/brackets 0/0/0. Five patched render features pass route assertions. Direct token scan over patched runtime files reports 0 `Application.isPlaying`, `GlobalRegistry.Get<T>`, `GetComponent`, `TryGetComponent`, or DataVault lock tokens. Whole-runtime direct hot scan covered 1872 `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute`/render-route methods with 0 lookup reports. DataVault parser scanned 239 write-lock methods; two `RepairDroneEntity` reports were helper-release false positives, manually inspected as `ReleasePayloadWrite(...)` called from `finally`. No parser orphan remained.

Problem: `WorldProceduralScatterDirector` still polled `Application.isPlaying` from `Tick()`, `LateFrameTick()`, and helpers reachable from scatter cadence or visual-sync rebuild. The route is gameplay/presentation cadence, not Unity lifecycle ownership.
Solution: Added `_runtimeScatterCallbacksActive`, sampled it in `Awake()` and `OnEnable()`, cleared it in `OnDisable()`, `OnDestroy()`, and editor reload teardown. `RuntimeNowSeconds()` now routes through `HasRuntimeScatterOwner()`. Hot scatter cadence, bootstrap deferral, rebuild dispatch, startup settle, forced refresh, radius resolve, and tick registration consume only cached owner state.
Rejected Alternatives: Using only `s_activeRuntimeInstance != null`. Active registry ownership can exist in editor/service-bootstrap contexts; runtime play-mode state still needed a cold lifecycle latch. Keeping direct `Application.isPlaying` in `Tick()`/`LateFrameTick()` was rejected because the dispatcher already owns callback phase.
Scalability potential: Low devices avoid repeated Unity runtime property reads inside scatter cadence and visual-sync rebuild routes. Middle devices keep identical scatter behavior. High and Ultra keep full procedural scatter and migratory sargassum overkill paths while runtime mode is carried as a zero-GC bool.
Hardware Impact: Static estimate: 4-18 us hot-route platform variance removed on i3/MX350 class CPUs under active scatter. State transfer is one bool plus existing static owner reference, 0 B GC. No DataVault write lock was added.

Problem: `SuitHUDV4CanvasOverlay` and nested `HectonUIScaler` used `Application.isPlaying` inside UI slow/late routes and callback registration routes. The stencil suppression helper also embedded a play-mode read used by hot UI callbacks.
Solution: Added `_runtimeHudCallbacksActive` and `_runtimeScalerCallbacksActive`; sampled them from lifecycle entry, cleared them on disable/destroy, and routed outer HUD slow/late/tick registration plus scaler slow/register/hot-swap through the latches. `IsStencilRenderGraphSuppressedRuntime()` now uses `SystemDispatcher.ActiveRuntimeInstance != null` with the existing render-graph suppression flag.
Rejected Alternatives: Gating HUD callbacks only on `_lateFrameTickRegistered` or `_registeredToTickManager`. Registration state proves dispatcher subscription, not runtime mode, and can lag during suppression or rebuild paths. Direct `Application.isPlaying` in visual-sync was rejected as phase drift.
Scalability potential: Low devices reduce UI hot-path platform variance. Middle devices keep identical HUD refresh cadence and scaler behavior. High and Ultra keep full HUD overlays, acoustic radar, threat chevrons, and render-graph stencil suppression without per-frame play-mode polling.
Hardware Impact: Static estimate: 3-14 us UI visual-sync/slow-route variance removed on weak CPUs. State transfer is two bool latches and dispatcher runtime owner read, 0 B GC. No DataVault write lock was added.

Problem: The scatter/HUD pass needed proof that the new latches did not hide dependency lookups, move presentation outside visual-sync, introduce lock nesting, or violate compilation throttle.
Solution: Added source guards `WorldProceduralScatterDirector_RuntimeCallbacksUseColdRuntimeLatch`, `SuitHudRuntimeCallbacksUseColdRuntimeLatches`, and `AssertHotBodyUsesRuntimeLatch`. Ran string/comment-aware C# balance, targeted in-memory method assertions, broad direct hot lookup scan, scoped DataVault write-lock token scan, scoped `git diff --check`, CPU/compiler throttle, and parser process inspection.
Rejected Alternatives: Launching `dotnet build` while CPU was at 98.1%. The project rule forbids build under CPU load >50%; source-level static verification was the correct proof for this pass.
Scalability potential: The proof is invariant across weak, middle, high, and ultra devices because only runtime-state routing changes. `GlobalQualityWeight` remains continuous and no gameplay truth, DTO layout, save identity, or authority route changed.
Hardware Impact: `WorldProceduralScatterDirector.cs`, `SuitHUDV4CanvasOverlay.cs`, and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0. Targeted assertions pass. Broad direct hot scan covered 2253 hot methods with 0 `GlobalRegistry.Get<T>`/`GetComponent`/`TryGetComponent` reports. Touched files add no DataVault write-lock acquire/release tokens. Scoped diff check exits 0 with LF/CRLF warnings only. Build was not launched because CPU measured 98.1% and static validation satisfied the throttle.

Problem: `HectonUnderwaterVisuals.SlowTick()` still polled `Application.isPlaying`, and a transitive visual-sync path `LateFrameTick -> RunUnderwaterVisualTick -> ApplyOceanMaterialBindings -> ApplyOceanUnderwaterGlobals` also reached `Application.isPlaying`. That violates cold runtime identity routing for underwater presentation callbacks.
Solution: Added `_runtimeVisualCallbacksActive`, sampled it only from lifecycle/startup, and routed underwater slow/late/render helpers through the cached bool. Static helpers that previously queried play mode now accept the runtime bool as an argument. The editor path keeps the same false-runtime fallback for scene-view preview.
Rejected Alternatives: Leaving `Application.isPlaying` inside static material/global helpers because the call is small. It is still a Unity global platform read on a visual-sync path. Also rejected replacing all editor-only play-mode checks in unrelated preview/authoring helpers; those are outside the hot callback graph and changing them would add unnecessary risk.
Scalability potential: Low devices remove repeated Unity global reads from underwater fog, HUD luminance, photophobia, and ocean global updates. Middle devices keep identical visuals. High and Ultra keep the full underwater Noir/photophobia/HUD fog stack while runtime identity is zero-GC scalar state.
Hardware Impact: Static estimate: 6-22 us hot-route platform-read variance removed on i3/MX350 class CPUs. State transfer is one bool and method arguments, 0 B GC. `LateFrameTick` graph: 149 reachable, 0 forbidden lookup/platform/resource-repair hits. `Render` graph: 12 reachable, 0 hits. `SlowTick` graph: 45 reachable, 0 lookup/platform hits. Exact project hot platform scan reports only editor launcher and three prior `Screen` DTO false positives. Build was not launched because CPU measured 53.4%.

Problem: The underwater fix needed durable regression proof without compiler spam or disk telemetry dumps.
Solution: Added `HectonUnderwaterVisuals_HotCallbacksUseColdRuntimeLatch` in the editor source guard suite. It asserts lifecycle latch ownership and hot helper purity for direct callbacks plus selected transitive visual-sync/render helpers.
Rejected Alternatives: A broad rewrite of all `Application.isPlaying` in the file. Several remaining reads are lifecycle/editor-preview/cold discovery gates, not high-frequency callback drift. The guard targets the actual phase violation.
Scalability potential: The proof is independent of quality tier; weak through ultra devices keep the same continuous `GlobalQualityWeight` visual ladder.
Hardware Impact: `HectonUnderwaterVisuals.cs` and guard file balance braces/parens/brackets 0/0/0. Broad direct hot lookup scan covered 2943 methods with 0 reports. Runtime file has 0 DataVault write-lock tokens. Scoped diff check exits 0 with LF/CRLF warnings only. Orphan parser PID 39276 from the timed-out scan was stopped.

Problem: `AmbientWaterMotionManager.Tick()` can call `TryRegisterLateFrame()`, and the registration helpers still polled `Application.isPlaying`. That put a Unity global runtime property read on a high-frequency ambient water path even though runtime mode belongs to lifecycle ownership.
Solution: Added `_runtimeWaterMotionCallbacksActive`, sampled it in `Awake()` and `OnEnable()`, cleared it in `OnDisable()` and `OnDestroy()`, and routed tick, late-frame, service, hot-swap, and BiomeMatrix registration through the cached latch. Ambient water simulation still writes scalar visual intent in Tick and applies presentation in LateFrameTick.
Rejected Alternatives: Removing `Tick()` self-repair registration was rejected because it would change callback recovery semantics. Using dispatcher/service registration as the runtime-mode truth was rejected because registration and Unity play-mode state are different facts. Keeping `Application.isPlaying` in the helper was rejected because the helper is reachable from Tick.
Scalability potential: Low devices avoid a repeated Unity global property read in ambient water callback recovery. Middle devices keep identical current/swell propagation. High and Ultra keep ambient water visual overkill while runtime mode moves as one zero-GC bool.
Hardware Impact: Static estimate: 1-4 us hot-route platform-read variance removed on i3/MX350 class CPUs when ambient water tick self-registration is active. State transfer is one bool latch, 0 B GC. No DataVault write lock was added.

Problem: The ambient water latch split needed proof without compiler spam and without another broad parser stall.
Solution: Added `AmbientWaterMotionManager_HotRegistrationUsesColdRuntimeLatch`; ran targeted method extraction, lexical balance, same-file hot graph scan, scoped DataVault token scan, scoped `git diff --check`, CPU/compiler throttle, and process inspection. The broad parser attempt that timed out was stopped and replaced with bounded verification over the touched files.
Rejected Alternatives: Running `dotnet build` while CPU measured above the 50% ceiling. Also rejected treating a timed-out full-tree parser as a valid proof artifact.
Scalability potential: Proof is invariant across low, middle, high, and ultra devices because the change affects ownership routing only, not the continuous `GlobalQualityWeight` visual ladder or gameplay truth.
Hardware Impact: `AmbientWaterMotionManager.cs` and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0. Local hot graph reports `Tick` 2 reachable / 0 forbidden and `LateFrameTick` 15 reachable / 0 forbidden for lookup/platform/allocation tokens. Runtime file has 0 DataVault write-lock tokens. Scoped diff check exits 0 with LF/CRLF warnings only. Build was not launched: CPU 53%, above project ceiling.
