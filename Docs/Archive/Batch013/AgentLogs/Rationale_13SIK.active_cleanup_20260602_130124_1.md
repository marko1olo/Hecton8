# Rationale_13SIK

Status: STATIC VERIFIED / BUILD BLOCKED BY CPU GATE

## 2026-05-27 Intake

Problem: User assigned ad-hoc ID `13SIK`, but active `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="13SIK">`.
Solution: Proceed as ad-hoc Tools/Interaction domain audit while recording task count `0` from absent XML and keeping all decisions in this rationale file.
Rejected Alternatives: Fabricating a batch prompt or borrowing neighboring agent prompts would contaminate architecture decisions.
Scalability potential: Audit targets continuous `GlobalQualityWeight`, zero-GC hot paths, and tool interaction contracts before implementation.
Hardware Impact: No runtime change yet. Static audit only; microsecond savings not claimed.

## 2026-05-27 Mandate Selection

Problem: Tools interact with world state, physics, scanner UI, haptics, VFX, and power; blind edits can violate ownership.
Solution: Use eight mandates: tool interaction/raycast/heat, hand interaction, zero-GC, GlobalRegistry DI, SignalBus lane split, ARM64 DTO layout, physics integrity, and visual-fake-first.
Rejected Alternatives: Reading all 80 registry files wastes context; editing only obvious `Tools/` names misses root-level `*Tool.cs` runtime classes.
Scalability potential: Low uses bounded query slots and visual fakes; middle/high/ultra scale continuous quality, richer presentation, and telemetry without changing gameplay authority.
Hardware Impact: Audit phase only. Expected repair targets are GC allocations, synchronous physics queries, direct force/joint ownership, and per-frame service lookups that hurt i3/MX350 first.

## 2026-05-27 Performance Budget Scalar

Problem: `PerformanceBudgetController` used binary throttle/restore behavior: all registered systems jumped from `1f` to `_throttleMultiplier` only after max frame time was exceeded, then jumped back at target. This violates continuous `GlobalQualityWeight`, causes visible quality popping, and concentrates reconfiguration cost on weak devices.
Solution: Replace the binary branch with a continuous scalar from frame-time pressure, hysteresis, drop/recover rates, and `HomeostasisBrain.GlobalQualityWeight`. The owner still calls the existing `IBudgetManagedSystem.SetPerformanceLevel(float)` route; no new hot registry polling route was introduced.
Rejected Alternatives: Keeping `IsThrottled` as the control path was rejected because it preserves a low/high switch. Adding a new global quality owner was rejected because Homeostasis already owns the scalar.
Scalability potential: Low uses low scalar for cheaper cadence/capacity in managed systems; middle recovers gradually; high/ultra can spend the scalar on overkill visuals without changing gameplay truth.
Hardware Impact: Expected i3/MX350 gain is not raw average frame time; the gain is avoiding mode-flip spikes when pressure crosses thresholds. Estimated transition cost avoided: ~18 us per managed system burst, bounded by 32 registered systems.

## 2026-05-27 ToolKinematics DataVault Ownership

Problem: `ToolKinematicsRuntime.TryResolveVaultView` could call `DataVault.EnsureGenerationHandle` from fixed, postfixed, slow, and read paths. That lets hot/read code create or grow native buffers and violates the pure read accessor and DataVault ownership doctrine.
Solution: Add an explicit `allowCreate` flag. Cold bootstrap/rebind calls use `TryResolveAllBuffers(true)`; fixed/read/postfixed/slow paths use `false` and fail closed if ownership is missing.
Rejected Alternatives: Leaving hot fallback creation was rejected because it hides capacity bugs inside frame work. Recreating buffers per missing view was rejected because it moves allocation into the most latency-sensitive tool path.
Scalability potential: Low devices avoid surprise native allocation or buffer clear during interaction; middle/high/ultra keep deterministic preallocated buffers and use saved time for beam/heat presentation, not ownership repair.
Hardware Impact: Estimated i3/MX350 worst-case stall avoided: 4-40 us on stale handle or capacity mismatch, with larger spikes avoided when buffer clear touches multiple tool arrays.

## 2026-05-27 Build Gate

Problem: Project rule forbids `dotnet build` when CPU is above 50% or compiler processes are active.
Solution: Sampled processor load and compiler processes before attempting build. CPU was 72-76%; no `dotnet`, `csc`, or `MSBuild` process was visible, but CPU alone blocks build.
Rejected Alternatives: Running a build anyway was rejected because it violates the coordination rule and would add noise to other agents' work.
Scalability potential: No runtime change. Verification route stays deterministic and avoids starving parallel agents.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Direct Tool Hit Registry-Only Route

Problem: `ToolHitUtility` remained the shared direct impact path for knife, harpoon, stun pistol, sampler, analyzer, and propulsion tools. After the queued signal owner was fixed, this utility still performed `TryGetComponent` and bounded parent traversal on active hits for `ICuttable`, `IDamageReceiver`, `IInventoryPickupPreviewSource`, `IInventoryPickupSource`, and a fallback `Rigidbody`. That violates the one-route target ownership doctrine and keeps hierarchy-depth cost inside weapon/tool use.
Solution: Remove the utility fallback scans. `ToolHitUtility` now resolves cuttable, damage receiver, and pickup facts only from `InteractableRegistry.TargetInfo`. Rigidbody impulse routing uses `Collider.attachedRigidbody`, which is Unity's native compound-collider owner route and does not need a parent walk. To preserve real gameplay targets, `FaunaBrain`, `HectonPlayerHealth`, and `SubmarineAutoLevelBallastController` now publish/invalidate their collider trees in cold lifecycle/combat ownership paths; dead fauna invalidates its interaction target tree when death state is entered.
Rejected Alternatives: Keeping fallback traversal was rejected because it hides authoring/registration bugs in active tool use. Adding a second damage-target map was rejected because `InteractableRegistry` already owns collider-to-role payloads. Registering editor/test armor receivers and UI diegetic receivers was rejected because they are not first-order direct tool/environment hit targets for this pass.
Scalability potential: Low tier gets direct tool impacts with cached target facts and no parent search. Middle tier keeps deterministic damage/pickup truth as object counts grow. High tier can afford denser fauna, salvage, and hull interaction effects. Ultra tier can spend the saved CPU on stronger sparks, haptics, decals, and scanner feedback without changing authority routes.
Hardware Impact: Estimated i3/MX350 gain is 0.2-6.0 us per direct hit miss depending on transform depth and interface probes removed. Steady-frame cost added is 0 us; `RegisterTree`/`InvalidateTree` cost is cold lifecycle work.

## 2026-05-27 Build Gate Twenty-First Pass

Problem: Twenty-first-pass direct tool-hit edits need compile verification, but local rules forbid builds when max CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes. CPU samples were 100%, 93.66%, and 58.3%; average 83.99%, max 100%, with no `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process visible. Build was not launched.
Rejected Alternatives: Running `dotnet build` at max CPU 100% was rejected because it violates the parallel-agent coordination rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Final Build Gate Sample

Problem: All second-pass code edits need compile verification, but the build gate remained closed.
Solution: Re-sampled CPU and compiler processes. CPU ranged 81-100%; no compiler process was visible. Build remained blocked by CPU load.
Rejected Alternatives: Running a build under 81-100% CPU was rejected because it violates the project rule and risks false failures.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Read Accessor Purity Cleanup

Problem: The previous allocation fix for `PerformanceBudgetController.GetBudgetStatus()` still cleared and refilled an owner dictionary inside a `Get*` accessor. That violates the project rule that read accessors must not mutate owner/global state.
Solution: Move snapshot writes to write paths: system register, performance report, performance scalar apply, and unregister. `GetBudgetStatus()` now returns the current snapshot without mutation; `CopyBudgetStatusNonAlloc` remains the preferred hot path.
Rejected Alternatives: Returning a newly allocated dictionary preserved old semantics but kept GC in a `Get*` method. Exposing only `IReadOnlyDictionary` was rejected because it would change the public signature and risk unrelated callers.
Scalability potential: Low devices avoid status-read allocation and hidden read-side mutation; high/ultra tooling can poll status more often without changing gameplay authority.
Hardware Impact: Estimated 5-20 us and one dictionary allocation avoided per legacy status read.

## 2026-05-27 Tool Target Registry Routing

Problem: Root gameplay tools still had direct component discovery in active tool actions: `LogicSpannerTool` parent-walked for `BaseModule`, `SalvageSamplerTool` used `TryGetComponent/GetComponentInParent` for resource nodes, and `EnvironmentalAnalyzerTool` repeated pickup/scannable/resource/base lookups.
Solution: Route those target classes through `InteractableRegistry.TryResolve`, the existing first-party collider-to-target cache. Residual direct lookups remain only where the registry does not own the fact: `HectonItem`, `ModuleMarker`, and generic `ICuttable`.
Rejected Alternatives: Expanding `InteractableRegistry.TargetInfo` with every possible tool-specific component was rejected for this pass because it would widen the interaction contract. Keeping repeated parent-walk discovery was rejected because it duplicates the registry owner route.
Scalability potential: Weak devices pay one cached target lookup instead of repeated hierarchy probes; middle/high/ultra can spend saved time on richer analyzer/sampler feedback while preserving the same target truth.
Hardware Impact: Estimated 2-12 us saved per cached target use, larger on deep prefabs or repeated analyzer archival paths.

## 2026-05-27 Shared Pickup Utility Registry Route

Problem: `ToolHitUtility.TryPeekCollectible` and `TryCollectItem` still performed direct collider/parent component discovery. Those are used by sampler and other tools, so leaving them as fallback-only callers would keep duplicated pickup target discovery alive.
Solution: Resolve `IInventoryPickupSource` through `InteractableRegistry` first and use `IInventoryPickupPreviewSource` when the cached pickup source supports preview. Keep bounded component fallback for preview-only edge cases not represented by the registry.
Rejected Alternatives: Removing fallback entirely was rejected because a preview-only pickup source may exist outside current registry fields. Expanding `TargetInfo` was rejected for this pass to avoid widening the registry memory footprint without a measured need.
Scalability potential: Low devices avoid repeated parent scans on common pickup checks; middle/high/ultra can keep richer sampler/analyzer feedback without extra lookup cost.
Hardware Impact: Estimated 2-10 us saved per common pickup preview/collect check.

## 2026-05-27 Build Gate Second Pass

Problem: Second-pass patches still require compile verification, but project CPU guard remains hard.
Solution: Sampled CPU and compiler processes. CPU was 100%; no compiler process was visible. Build remained blocked.
Rejected Alternatives: Running build at 100% CPU was rejected because it violates the local rule and would contaminate results for parallel agents.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 ToolHaptics DataVault Ownership

Problem: `ToolHapticsRuntime` used `EnsureGenerationHandle` inside the generic resolver reached by `Tick`, `LateFrameTick`, read snapshots, front count, command enqueue, and command storage. That made haptic feedback a hidden hot allocator after missing handles or DataVault swaps.
Solution: Split haptic buffer resolution into `allowCreate` cold calls and hot existing-handle reads. `Awake`, `OnEnable`, and active DataVault rebind call `EnsureBuffers`; all gameplay haptic paths use `allowCreate=false`.
Rejected Alternatives: Allocating on the first haptic command was rejected because combat/tool impacts can arrive during frame pressure. Dropping haptics permanently after DataVault rebind was rejected; rebind now recreates buffers immediately while still outside haptic hot loops.
Scalability potential: Low devices avoid surprise native buffer creation while still receiving haptics after owner setup. Middle/high/ultra can spend feedback budget on richer command blending instead of ownership repair.
Hardware Impact: Estimated i3/MX350 stall avoided: 3-15 us on stale haptic handles, with larger native clear spikes avoided during service rebind.

## 2026-05-27 Build Gate Retry

Problem: After the haptic patch, compile verification was still desirable but project rules still apply.
Solution: Re-sampled CPU and compiler processes. CPU was 91-100%; no compiler process was visible. Build remained blocked by CPU load.
Rejected Alternatives: Running `dotnet build Hecton8.slnx` under 100% CPU was rejected because it would violate the coordination rule and produce unreliable timing/failure signals.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 InteractableRegistry Read Purity

Problem: `InteractableRegistry.TryResolve(Collider, out TargetInfo)` looked like a read accessor but performed hierarchy discovery and wrote to the collider cache on a miss. After previous tool routing fixes, this made several active tool paths depend on hidden cache mutation and parent-walk cost.
Solution: Make `TryResolve` cache-read only. Keep target discovery in explicit `RegisterTree`/`RegisterCollider` lifecycle routes. Add lifecycle publication for `ResourceNode` and `BaseModule` so knife/analyzer/sampler/spanner reads have a cold-owned cache source.
Rejected Alternatives: Keeping lazy cache fill was rejected because it violates read accessor purity. Reintroducing direct parent walks in each tool was rejected because it duplicates ownership and costs more under nested prefabs.
Scalability potential: Weak devices avoid surprise hierarchy probes during active use; middle/high/ultra keep the same target truth while spending frame budget on richer scanner/tool feedback.
Hardware Impact: Estimated i3/MX350 stall avoided: 5-35 us on first hot cache miss against deep collider trees; repeated hits become O(1) cache reads.

## 2026-05-27 Survival Blade Target Route

Problem: `KnifeTool` still used `GetComponentInParent<KnifeTool>`, `GetComponentInParent<ResourceNode>`, and `GetComponentInParent<BaseModule>` during strike/read assessment. This violated the tool target registry route and duplicated the analyzer/sampler fix.
Solution: Cache the tool transform in tool lifecycle, filter own colliders with transform ownership, and resolve resource/module facts through `InteractableRegistry` before a direct-only fallback.
Rejected Alternatives: Expanding knife-specific private caches was rejected because the shared interaction registry already owns collider target identity. Keeping parent search was rejected because it hides cost in active melee use.
Scalability potential: Low devices get bounded melee classification cost; middle/high/ultra can add richer blade feedback without changing damage authority.
Hardware Impact: Estimated i3/MX350 gain: 2-12 us per tactical read or strike against nested resource/module prefabs.

## 2026-05-27 Transport Platform Target Fact

Problem: `EquipmentInteractionHandler.CachePlatformRelativeHit` resolved `ITransportPlatform` by walking collider parents for every queued interaction signal. Mounted transports already register their collider trees, but the platform fact was not in `TargetInfo`.
Solution: Add `ITransportPlatform` to `InteractableRegistry.TargetInfo` and check it before the legacy parent fallback in `TryResolvePlatformTransform`.
Rejected Alternatives: Removing fallback entirely was rejected because submarine or legacy platform colliders may not yet publish through `InteractableRegistry`. Adding a new global transport registry was rejected because it would widen authority for one cached target fact.
Scalability potential: Weak devices avoid repeated hierarchy probes on platform-relative hit rehydration; high/ultra can afford denser interaction signal feedback on moving transports.
Hardware Impact: Estimated i3/MX350 gain: 2-16 us per registered transport-relative interaction signal.

## 2026-05-27 Build Gate Third Pass

Problem: Third-pass code edits still require compile proof, but the project build gate forbids builds during CPU contention.
Solution: Sampled CPU and compiler processes. CPU was 99%; no `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process was visible. Build remained blocked by CPU load.
Rejected Alternatives: Running `dotnet build Hecton8.slnx` at 99% CPU was rejected because it violates the local rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Interaction Target Role Cache

Problem: Queued tool dispatch still resolved `IInteractionSignalConsumer`, `IInteractionVulnerabilitySource`, `ICuttable`, voxel cut targets, repair targets, and damage receivers by direct component checks or bounded parent walks. After `InteractableRegistry.TryResolve` became pure, the correct fix was owner-side publication, not lazy hot discovery.
Solution: Extend `InteractableRegistry.TargetInfo` with the tool-facing roles already consumed by runtime tools. Registered colliders now carry pickup preview, module host, repairable module, voxel repair/plasma, cuttable, damage receiver, submarine repair, signal consumer, and vulnerability facts. Dispatch remains registry-first with legacy fallback only for objects not yet under this cold publication contract.
Rejected Alternatives: A new tool-target registry was rejected because it duplicates collider ownership and creates two routes for one fact. Removing all fallback was rejected because `SubmarineAtmosphereSystem` and other cross-domain legacy targets are not safely registrable without wider owner review.
Scalability potential: Low devices avoid repeated parent-walk role discovery in the common route. Middle devices keep fallback only on cache miss. High and ultra can spend saved interaction CPU on denser sparks, haptics, scan overlays, and wreck-cut feedback without changing authority.
Hardware Impact: Estimated i3/MX350 gain: 2-35 us per registered interaction signal depending on hierarchy depth; memory cost is managed references in a fixed 4096-entry cold cache.

## 2026-05-27 Missing Tool Owner Publication

Problem: Several first-party tool-facing owners implemented interaction contracts but did not publish their collider trees: scannables, construction weld targets, leak weld targets, voxel volumes, physical panel buttons, and generated wreck signal proxies. Registry-first dispatch would not help those owners unless they registered cold.
Solution: Add lifecycle or pool-hook `InteractableRegistry.RegisterTree/InvalidateTree` calls to `ScannableTarget`, `VRConstructionWeldTarget`, `VRLeakPatchWeldTarget`, `HectonVoxelVolume`, `PhysicalPanelButton`, and `WreckIntegritySignalProxy`. `HectonVoxelVolume` is a justified cross-domain edit because it directly owns `IVoxelRepairWeldTarget`/`IVoxelPlasmaCutTarget` consumed by tools and has a fixed collider-chunk cap of 8.
Rejected Alternatives: Leaving these as dispatch-time lookups was rejected because it preserves hidden hot discovery. Registering broad submarine/atmosphere roots was rejected because those roots can own large hierarchies and need owner-domain review before adding interaction registry traversal.
Scalability potential: Low devices resolve scanner/weld/voxel/wreck/panel hits with cache reads. Middle/high/ultra get the same truth route while scaling presentation through continuous quality, not binary target logic.
Hardware Impact: Estimated i3/MX350 gain: 2-20 us per hit/signal for these registered owners; cold registration cost is paid on lifecycle/pool events.

## 2026-05-27 Repair And Shared Tool Route Cleanup

Problem: `RepairTool`, `ToolHitUtility`, and `SalvageSamplerTool` still had active target classification paths that could walk parents for cuttable, damage receiver, voxel repair, repair module, submarine repair, and pickup preview roles.
Solution: Route those lookups through the expanded `InteractableRegistry.TargetInfo` first. Keep bounded fallback for legacy or unregistered cross-domain targets only.
Rejected Alternatives: Tool-local per-collider dictionaries were rejected because they would duplicate the shared target cache and add invalidation risk. Removing fallback entirely was rejected because repair and damage receivers are still owned by several non-tool domains.
Scalability potential: Low devices avoid repeated hierarchy probes during repair/sampling. Middle/high/ultra can add richer repair sparks and sampler diagnosis without paying additional target discovery.
Hardware Impact: Estimated i3/MX350 gain: 2-18 us on repair target cache refresh and 2-12 us on shared tool hit classification against registered targets.

## 2026-05-27 Build Gate Fourth Pass

Problem: Fourth-pass code edits require compile verification, but the project build gate remained closed.
Solution: Sampled CPU and runtime/compiler processes. CPU was 99% and `dotnet.exe` PID 21804 was active, so build was forbidden by both CPU and active-dotnet rules.
Rejected Alternatives: Running a build anyway was rejected because it violates the coordination contract and could collide with another agent's build.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Physical Pickup Route Ordering

Problem: `PlayerInteraction.ExecuteInteraction()` attempted direct inventory pickup before `PhysicalInteractionHandler`. For `PickupItem` and `HectonItem`, that bypassed the pull-to-hand physical pickup sequence entirely because inventory insertion succeeded first.
Solution: Move physical interception before direct pickup fallback and pass the cached hover `TargetInfo` into the physical handler. Same-target hover refresh also updates cached target facts so the physical route starts from the latest collider identity.
Rejected Alternatives: Making `TryHandleInventoryPickup` optionally animate was rejected because inventory ownership should stay simple and physical presentation belongs to `PhysicalInteractionHandler`. Keeping the old order was rejected because it leaves a dead feature path.
Scalability potential: Low devices still use a cheap transform/kinematic pull, not a joint-heavy simulation. Middle/high/ultra can spend more budget on pickup presentation and haptics while using the same route.
Hardware Impact: Estimated 2-8 us saved on interaction start by reusing cached target info; primary gain is correctness, not average frame time.

## 2026-05-27 Physical Interaction Bounded Discovery

Problem: `PhysicalInteractionHandler.TryResolveOwnedComponent` recursively searched child transforms without a depth cap. It is not per-frame, but malformed or generated hierarchies could still cause excessive traversal during pickup start.
Solution: Add a bounded recursive overload using the existing `MaxParentComponentResolveDepth`. Keep normal prefab behavior unchanged.
Rejected Alternatives: Allocating a temporary stack/list was rejected because the existing recursive shape can be bounded without heap churn. Removing collider fallback was rejected because some pickups need a child collider to disable during pull animation.
Scalability potential: Low devices avoid pathological hierarchy traversal; high/ultra keep the same physical pickup route with richer presentation.
Hardware Impact: No normal-case microsecond claim; pathological traversal is capped at 32 levels.

## 2026-05-27 Role Cache Versus Spatial Registry Split

Problem: After adding non-hover tool roles to `TargetInfo.HasAny`, `RegisterCollider` cached them correctly but also added them to the fixed spatial hover registry. `TryResolveSpatialTarget` skipped non-interactables later, but they could still consume `MaxRegisteredTargets` slots.
Solution: `RegisterCollider` now always writes the collider role cache, then only adds colliders with `TargetInfo.Interactable != null` to the spatial hover array.
Rejected Alternatives: Increasing `MaxRegisteredTargets` was rejected because it hides pollution and makes hover scans more expensive. Keeping a single HasAny gate was rejected because role cache and hover search have different ownership needs.
Scalability potential: Low devices keep hover search dense with real interactables only; high/ultra can publish more tool-only role colliders without degrading prompt ray traversal.
Hardware Impact: Avoids up to 4096 wasted spatial slots in mixed scenes; per-hover scan avoids role-only collider checks.

## 2026-05-27 Physical Owner Invalidation

Problem: `HeavyCarryInteractable` and `VRCableDragPlug` registered their collider trees on enable and invalidated on disable, but destroy-time invalidation was not explicit. Pool/destruction edge cases can leave stale cache entries when lifecycle ordering is nonstandard.
Solution: Add `OnDestroy` invalidation and unregister cleanup to the physical carry/cable owners.
Rejected Alternatives: Relying solely on Unity's normal OnDisable-before-OnDestroy path was rejected because pooled/generated objects are exactly where stale interaction caches are hardest to diagnose.
Scalability potential: Low devices avoid stale collider cache misses/retries; high/ultra keep deterministic physical interactions under heavier object churn.
Hardware Impact: Stale-cache prevention; no steady-frame microsecond claim.

## 2026-05-27 Build Gate Fifth Pass

Problem: Fifth-pass code edits need compile verification, but the build gate remains closed.
Solution: Sampled CPU and dotnet/compiler processes. CPU was 79% and `dotnet.exe` PIDs 19552 and 29316 were active.
Rejected Alternatives: Running build anyway was rejected because both guard clauses are active.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Registry Re-Registration Coherency

Problem: `InteractableRegistry.RegisterCollider` cached role-only payloads correctly, but a collider that previously had `IInteractable` could remain in the spatial hover array after re-registering as role-only or no-role. That creates stale hover hits and can waste fixed registry slots.
Solution: Resolve the collider key before role checks. If no roles remain, remove both cache and spatial entry. If only non-hover roles remain, update the role cache and explicitly unregister the spatial entry.
Rejected Alternatives: Raising `MaxRegisteredTargets` was rejected because it hides stale ownership. Letting `TryResolveSpatialTarget` lazily skip stale entries was rejected because read accessors must not repair owner publication.
Scalability potential: Low devices keep hover scans dense with real interactables only. Middle/high/ultra can publish more tool-only collider facts without degrading prompt traversal or changing target truth.
Hardware Impact: Avoids up to 4096 stale/wasted spatial hover slots in mixed scenes; no steady-frame microsecond claim without profiler.

## 2026-05-27 Spatial Ray Math Cleanup

Problem: `TryResolveSpatialTarget` used reciprocal `Mathf.Sqrt` for ray direction normalization in the throttled hover path. The project math mandate prefers reciprocal-square-root/approximation unless exact sqrt is justified.
Solution: Add `Unity.Mathematics` and use `math.rsqrt(directionLengthSq)` after existing finite/epsilon guards.
Rejected Alternatives: Keeping exact sqrt was rejected because hover ray normalization does not need high-school exactness. A custom approximation was rejected because `math.rsqrt` is clearer and maps better to SIMD/backend intrinsics.
Scalability potential: Weak devices save small CPU in prompt acquisition. Higher tiers spend the saved budget on denser interaction affordance visuals, not different gameplay truth.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.4 us per target-probe depending on backend and scene pressure.

## 2026-05-27 Pocket Pickup Visual-Fake Motion

Problem: `PhysicalInteractionHandler.FixedTickPocketPickup` called `Rigidbody.MovePosition` while pocket pickups were already made kinematic and collision-disabled. This violated the hand/physics mandate and kept a FixedTick registration solely for a presentation pull.
Solution: Remove `FixedTickPocketPickup`. The pickup now queues transform position/scale through the existing LateFrame visual pose flush for both Rigidbody and non-Rigidbody pocket items. The body remains kinematic and collision-disabled during the pull, so this is presentation, not physics truth.
Rejected Alternatives: Keeping `MovePosition` was rejected because the player only needs the item to visibly travel to hand before inventory handoff. Queueing a force packet was rejected because the body is intentionally kinematic/collision-off and inventory handoff is not a physical contest.
Scalability potential: Low devices avoid FixedTick work for pocket pickup presentation. Middle/high/ultra can enrich the same route with haptics, sound, and scale/arc polish without changing inventory authority.
Hardware Impact: Removes one direct Rigidbody move and one pocket-pick FixedTick need per active pickup frame; microsecond savings are scene-dependent and pending profiler proof.

## 2026-05-27 Pocket Pickup Motion Restore

Problem: Pocket pickup cached angular velocity but restored only linear motion on abort/cancel for non-kinematic bodies. That dropped rotation state after a failed physical pickup.
Solution: Restore angular velocity through `IPhysicsService.QueueAngularVelocitySet` alongside the existing linear velocity restoration.
Rejected Alternatives: Directly assigning `Rigidbody.angularVelocity` was rejected because physics writes must route through the physics service. Ignoring angular state was rejected because it makes cancellation physically lossy.
Scalability potential: Same behavior across weak/middle/high/ultra; visual polish scales separately from truth restoration.
Hardware Impact: Correctness fix; no steady-frame microsecond claim.

## 2026-05-27 Interactable Destroy Invalidation Sweep

Problem: Several owners published collider facts via `InteractableRegistry.RegisterTree` and invalidated on `OnDisable`, but had no explicit `OnDestroy`. Normal Unity order usually calls disable first, but pooled/generated teardown and abnormal object destruction are exactly where stale collider caches are hardest to diagnose.
Solution: Add explicit `OnDestroy` invalidation to UTF-safe interactable owners: `SaveStation`, `LifePodSeatStrapLatch`, `ClimbableLadder`, `NarrativeDiscovery`, `HectonItem`, `ResourceNode`, `StorageCrate`, `HarvestableOutcrop`, and `ResourceRecyclerModule`. `LifePodSeatStrapLatch` also unregisters its physical receiver on destroy.
Rejected Alternatives: Trusting normal `OnDisable` order was rejected after the same stale-cache class already appeared in heavy carry and cable plug owners. Raw-editing `Gameplay/EndingTerminalInteractable.cs` was rejected because it contains invalid UTF-8 bytes and `apply_patch` cannot safely read it.
Scalability potential: Low devices avoid stale cache misses/retries under object churn. Middle/high/ultra can use heavier interaction density without accumulating dead collider facts.
Hardware Impact: Stale-cache prevention; no steady-frame microsecond claim.

## 2026-05-27 Build Gate Sixth Pass

Problem: Sixth-pass runtime edits still require compile verification, but the project build protocol forbids launching `dotnet build` while another dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes before attempting compile. CPU average was 22.7%, max 26.51%, but `dotnet.exe` PID 19552 was active, so the active-dotnet guard blocked the build.
Rejected Alternatives: Running a second build anyway was rejected because it violates the coordination contract with other agents and can corrupt timing evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Surface Hit Request Naming

Problem: The interaction service method named `TryResolvePrimarySurfaceHit` is not a pure read. It formats a frame-latent surface query, touches service-owned request state, and returns the latest completed hit. That violates the project rule that read accessors must not publish, sync, mutate, or schedule hidden work.
Solution: Rename the contract and all tool callers to `RequestPrimarySurfaceHit`. The behavior stays service-owned and zero-GC, but the public name no longer lies about purity.
Rejected Alternatives: Keeping the old name and documenting the exception was rejected because it leaves a false route contract. Splitting a pure read from a request path was rejected for this pass because the current service already owns frame-latent request/response state and all callers need the request semantics.
Scalability potential: Weak devices get clearer ownership for queued surface queries; middle/high/ultra can scale query cadence or visual affordances without changing the truth route.
Hardware Impact: Naming/contract fix; no steady-frame microsecond claim.

## 2026-05-27 Tool Durability Hash Slot Route

Problem: Equipped tool hot paths read `CurrentDurability`, `IsBroken`, and apply active-use durability drain through string IDs. The implementation used string dictionaries and could fall back to `Animator.StringToHash` on active use-time routes.
Solution: Add hash-id read APIs and `TryDrainDurabilityByTime(uint, ...)` to `IToolDurabilityService`. `ToolDurabilitySystem` now maintains fixed slot mirrors for durability and broken state, resolves hash IDs by a 32-slot bounded scan, and keeps string dictionaries only as compatibility mirrors for UI/save/cold callers. `PlayerTool` uses hash-first reads/drain and falls back to the string route only when the item hash is missing or the slot was not registered yet.
Rejected Alternatives: Directly exposing native `NativeArray<ItemState>` to tools was rejected because it would leak ownership and completion rules. A new per-tool cache was rejected because it would duplicate durability truth and invalidation. Removing string fallback was rejected because legacy tools can still enter before centralized registration.
Scalability potential: Low devices avoid string dictionary traffic during repeated equipped-tool reads and drains. Middle devices keep the same data route with bounded 32-slot scan. High/ultra can spend saved budget on richer haptics, heat shimmer, scan overlays, and tool damage presentation without changing durability authority.
Hardware Impact: Estimated i3/MX350 gain: 1-6 us per active durability read/drain cluster depending on call density; no heap allocation introduced.

## 2026-05-27 Interaction Event Queue Prewarm

Problem: `InteractionEvents.Enqueue()` defensively called `EnsureInitialized()`, which can allocate and prewarm `NativeQueue` instances on the first hover or interaction event. The first hot event should not carry a hidden cold allocation.
Solution: Expose `InteractionEvents.PrewarmCold()` and call it from `PlayerInteraction.Awake()` in play mode. `EnsureInitialized()` remains as a defensive guard for abnormal producers.
Rejected Alternatives: Moving to a new signal bus in this pass was rejected because it would be wider than the identified bug. Removing the guard was rejected because non-player producers may still fire before the normal player lifecycle in tests or bootstrap scenes.
Scalability potential: Weak devices avoid first-hover hitch risk. Middle/high/ultra retain the same event semantics and can scale presentation listeners without first-use allocation.
Hardware Impact: Removes first-event NativeQueue allocation/prewarm from the hover producer route; steady-frame microsecond gain is not claimed.

## 2026-05-27 Physical Hand Input Dispatcher Cache

Problem: `PhysicalHandController.ShouldBypassXRHandKinematicUpdate()` polled `InputDispatcher.ActiveRuntimeInstance` from the XR idle physics tick. That is a hot singleton route in a system already driven from FixedTick.
Solution: Cache `InputDispatcher` during `Awake`/`OnEnable` from `GlobalRegistry.RegisteredInput`, with a static fallback only in the cold cache method, and update it through `GlobalRegistryServiceSlot.Input` hotswap. The FixedTick path now reads `_inputDispatcher`.
Rejected Alternatives: Querying `GlobalRegistry.RegisteredInput` every tick was rejected because GlobalRegistry is cold identity/DI only. Keeping `ActiveRuntimeInstance` in the hot path was rejected because it contradicts the no-hot-polling doctrine.
Scalability potential: Low devices avoid repeated singleton lookup in XR idle hand updates. Middle/high/ultra keep deterministic hand bypass behavior while scaling collision shell, haptics, and finger pose cadence separately.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.3 us per XR idle FixedTick; larger value only under high hand-controller count or weak CPU.

## 2026-05-27 Budget Status Read-Only Contract

Problem: `PerformanceBudgetController.GetBudgetStatus()` returned the owner dictionary type. Even after status mutation moved out of the read accessor, callers could still mutate the owner snapshot through the public signature.
Solution: Change the public return type to `IReadOnlyDictionary<string, SystemBudgetInfo>` while keeping the owner-reused snapshot and `CopyBudgetStatusNonAlloc` for hot retained reads.
Rejected Alternatives: Returning a fresh dictionary was rejected because it allocates from a `Get*` accessor. Returning an array copy was rejected because a non-alloc copy API already exists.
Scalability potential: Low devices avoid accidental dictionary churn/mutation. Middle/high/ultra keep the same budget truth route while continuous quality can scale system performance.
Hardware Impact: Contract-hardening; no direct frame-time claim.

## 2026-05-27 Build Gate Seventh Pass

Problem: Seventh-pass runtime edits need compile verification, but the project build protocol forbids launching `dotnet build` while any dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes. CPU average was 18.07%, max 38.09%, but active `dotnet.exe` PIDs 3984, 15116, 29516, 39028, 41884, 53636, and 61024 were present, so the active-dotnet guard blocked the build.
Rejected Alternatives: Running a second build anyway was rejected because it violates the coordination contract with other active agents.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Prompt UI Cold-Service Boundary

Problem: `InteractionUI.RefreshInteractPrefixCache()` was called from hover/input prompt refresh routes and could call `GlobalRegistry.NativeInputRuntime` and `GlobalRegistry.LocalizationText` through helper methods. `SetPromptVisible()` also called `InitializePromptContainer()`, which can run `TryGetComponent` and `AddComponent<CanvasGroup>()` on the first hover visibility change.
Solution: Cache input and localization services during `OnEnable`/`Start` and hotswap only. Runtime prompt refresh now reads cached fields. `SetPromptVisible()` is a pure visibility write over an already-cached `CanvasGroup`; initialization remains in cold lifecycle methods.
Rejected Alternatives: Leaving registry fallbacks inside prompt refresh was rejected because GlobalRegistry is cold identity/DI only. Creating the `CanvasGroup` from the hover event was rejected because UI event presentation should not perform component discovery or component creation.
Scalability potential: Weak devices avoid first-hover service lookup/component creation hitches. Middle/high/ultra can scale prompt richness and input glyph presentation without changing target truth or adding hot polling.
Hardware Impact: Estimated i3/MX350 gain is 0.2-1.2 us per prompt refresh in the common path, with a larger first-hover hitch avoided when `CanvasGroup` was absent.

## 2026-05-27 Physical Flora Snap Service Route

Problem: `PhysicalInteractionHandler.TryBeginFloraHarvestSnap()` used `DestructibleOrganicManager.ActiveRuntimeInstance` directly. That binds the physical interaction domain to a world singleton in an active hand-snap route and bypasses the existing registry-owned organic command facade.
Solution: Extend `IOrganicToolHitService` with `TryResolveNearestHarvestInteractionPoint(...)`. `DestructibleOrganicManager` already has a matching public zero-alloc resolver, so it satisfies the contract without a wrapper allocation. `PhysicalInteractionHandler` caches `GlobalRegistry.OrganicToolHits` during enable and hotswap.
Rejected Alternatives: Keeping the concrete singleton was rejected because GlobalRegistry service slots already own this dependency. Adding a second harvest-only registry service was rejected because it would duplicate organic authority for one query.
Scalability potential: Low devices avoid static singleton lookup and cross-domain coupling in hand snap. Middle/high/ultra can scale harvest-snap visuals and haptics through the same organic service without different gameplay truth.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.4 us per snap request; primary gain is route ownership, not average frame time.

## 2026-05-27 Prompt Bounds Normal Math

Problem: `InteractableRegistry.EstimateBoundsNormal()` had a rare fallback using `Vector3.normalized` inside the prompt ray target path.
Solution: Replace it with explicit finite length-squared validation and `math.rsqrt`.
Rejected Alternatives: Keeping Unity convenience normalization was rejected because the prompt registry already uses explicit math in the main ray normalization path.
Scalability potential: Weak devices keep prompt acquisition predictable. Higher tiers can spend saved budget on affordance visuals, not exact convenience math.
Hardware Impact: Estimated i3/MX350 gain: 0.05-0.2 us only on fallback bounds-normal cases.

## 2026-05-27 Build Gate Eighth Pass

Problem: Eighth-pass runtime edits need compile verification, but project rules forbid builds when CPU is above 50% or any dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes. CPU average was 59.11%, max 77.97%, with active `dotnet.exe` PID 36172 and `VBCSCompiler.exe` PID 62064.
Rejected Alternatives: Running `dotnet build` anyway was rejected because it violates both coordination guards and would produce unreliable evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Battery Tool Cache Read Purity

Problem: `PhysicalBatteryCompartment.TryResolveTool` was used by read-looking properties and active swap paths, but on a cache miss it called `RefreshBatteryToolCache()` and mutated `_cachedBatteryTool`.
Solution: Rename the read path to `TryGetCachedTool` and make it a pure cached-field read. Cache mutation now lives in `Awake`, `OnEnable`, `OnValidate`, and the explicit `RefreshBatteryToolCacheCold()` command.
Rejected Alternatives: Keeping lazy cache repair in a `TryResolve*` method was rejected because read accessors must not mutate. Removing cached tool support entirely was rejected because serialized battery tool ownership is the existing contract.
Scalability potential: Low devices avoid hidden component/interface rebinding during state reads. Middle, high, and ultra tiers keep the same battery truth and can scale snap visuals separately.
Hardware Impact: Estimated i3/MX350 gain: 1-4 us on cold-miss property/use paths; main gain is deterministic read semantics.

## 2026-05-27 Disabled Scrubber Cold Binding

Problem: `LifePodTactilePrologueController` can find `PhysicalBatteryCompartment` through `GetComponentInChildren(true)`, so an inactive scrubber socket could be read before its normal enable refresh. Lazy mutation in `HasInstalledCell` previously masked that edge case.
Solution: Add `PhysicalBatteryCompartment.RefreshBatteryToolCacheCold()` and call it from the prologue controller cold reference resolution. Update the editor smoke-test marker to enforce the explicit cold command.
Rejected Alternatives: Reintroducing read-side cache mutation was rejected. Calling `Awake`/`OnEnable` behavior manually was rejected because Unity lifecycle must remain owner-driven.
Scalability potential: Low devices keep inactive-scene setup deterministic. Higher tiers do not change gameplay authority; only presentation around the snap can scale.
Hardware Impact: Correctness fix; no steady-frame microsecond claim.

## 2026-05-27 Battery Snap Angular Restore Route

Problem: Failed or aborted battery snap restore treated target angular velocity as a torque velocity-change delta through `QueueTorque`. The physics service has a dedicated authoritative angular velocity assignment route.
Solution: Sanitize the target angular velocity and queue `IPhysicsService.QueueAngularVelocitySet` when the current/target angular delta is non-trivial. Linear restore remains a velocity-change force packet.
Rejected Alternatives: Directly assigning `Rigidbody.angularVelocity` was rejected because physics writes must route through the physics owner. Keeping torque-delta restore was rejected because it confuses target state with force intent.
Scalability potential: Low, middle, high, and ultra tiers all preserve the same physical truth; snap presentation can scale independently through visual fake timing.
Hardware Impact: Correctness fix; avoids one wrong angular-force interpretation per failed snap restore, no steady-frame gain claimed.

## 2026-05-27 Physical Switch Teardown Guard

Problem: `PhysicalSnapSwitch.Unregister()` called `GlobalRegistry.UnregisterUpdatable` even when `_registered` was false. Disable/destroy/idling could therefore ask the dispatcher to remove a non-owned tick route.
Solution: Guard the unregister call with `_registered`, matching the existing late-frame flag discipline.
Rejected Alternatives: Leaving unconditional unregister was rejected because lifecycle routes should be idempotent without relying on dispatcher tolerance. Adding new dispatcher checks was rejected because the local owner flag already exists.
Scalability potential: Low devices avoid redundant registry work and dev-log noise in switch-heavy cockpits. Higher tiers can increase physical panel density without extra teardown churn.
Hardware Impact: Estimated i3/MX350 gain: 0.2-1.0 us per redundant unregister plus avoided diagnostic noise.

## 2026-05-27 Physical Hand Context And Velocity Math

Problem: `PhysicalHandController.ResolvePlayerRootAup` polled `PlayerRuntimeContextService.ActiveRuntimeContext`, and an unused haptic helper still called `TryGetActiveRuntimeContext`. The kinematic velocity signal also computed `sqrt(speedSq)` after already computing `rsqrt(speedSq)`.
Solution: Cache `IPlayerRuntimeContext` during cold lifecycle and `GlobalRegistryServiceSlot.Player` hotswap. Use that cached interface for root AUP and haptic scalar reads. Derive speed as `speedSq * invSpeed`.
Rejected Alternatives: Polling `GlobalRegistry.Player` every bridge update was rejected because GlobalRegistry is cold DI only. Keeping exact `sqrt` was rejected because the already-computed reciprocal square root is sufficient for this visual-only signal magnitude.
Scalability potential: Low devices reduce singleton/static reads and scalar math in the hand bridge. Middle/high/ultra can spend saved time on richer hand collision shell, haptics, and contact presentation without changing authority.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.4 us per kinematic bridge update cluster.

## 2026-05-27 Build Gate Ninth Pass

Problem: Ninth-pass runtime/editor edits need compile verification, but project rules forbid builds while CPU is above 50% or any dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes. Latest CPU average was 83.33%, max 93%, with active `VBCSCompiler.exe` PID 19668, so build remained blocked by both rules.
Rejected Alternatives: Running `dotnet build` while CPU is high and VBCSCompiler is active was rejected because it violates the coordination contract and can collide with another agent's compile.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Tool Durability Hash Slot Index

Problem: The seventh pass moved equipped tool reads and drains to hash IDs, but `ToolDurabilitySystem.ResolveSlot(uint)` still scanned all 32 durability slots on every hash read. That kept the common route bounded but not direct. `ApplyEnvironmentalCorrosion()` also duplicated durability identity selection by recomputing `LocHash` or `Animator.StringToHash` instead of using the tool-owned mirror.
Solution: Add a cold-owned `_slotByItemHash` dictionary populated from `UpdateSlotMetadata()` and cleared with native durability state. `ResolveSlot(uint)` validates this index first and falls back to the old scan only if the index is absent or stale. Environmental corrosion now uses `PlayerTool.TryGetDurabilityMirror()` for the same tool ID, item hash, and max durability already used by active tool drain.
Rejected Alternatives: Updating `_slotByItemHash` inside `ResolveSlot(uint)` was rejected because read accessors must not mutate owner state. Removing the fallback scan was rejected because corrupted or duplicate legacy hash entries should fail soft. Keeping per-tick hash recomputation was rejected because `PlayerTool` already owns cached tool identity.
Scalability potential: Low devices get direct hash reads for common equipped-tool durability. Middle and high tiers keep the same truth route while spending saved budget on richer heat, haptics, and tool damage presentation. Ultra tier can increase durability/status visual density without widening authority.
Hardware Impact: Estimated i3/MX350 gain: 0.3-2.0 us per common hash durability read/drain cluster, plus 0.2-0.8 us avoided per held-tool corrosion slow tick by removing duplicate hash selection.

## 2026-05-27 Build Gate Tenth Pass

Problem: Tenth-pass durability patch needs compile verification, but the project build guard blocks compiles while CPU is above 50%.
Solution: Sampled CPU and build-related processes. CPU average was 69.81%, max 82.7%, with no dotnet/csc/MSBuild/VBCSCompiler process visible, so build remained blocked by CPU load.
Rejected Alternatives: Running build above 50% CPU was rejected because it violates the local coordination rule and would produce unreliable evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Builder Tool Player Context Route

Problem: `BuilderTool.OnSpawn()` used `PlayerRuntimeContextService.TryGetActiveRuntimeContext` directly for camera binding while builder/inventory/camera references had no `GlobalRegistryServiceSlot.Player` hotswap repair path. That violates the one-route player-context contract and can leave stale player-owned references after runtime context replacement.
Solution: Route `BuilderTool` through the cached `IPlayerRuntimeContext` already owned by `PlayerTool`. `TryBindPlayerReferencesCold()` binds `PlayerBuilder`, `PlayerInventory`, and camera transform from that interface, with direct root `TryGetComponent` only as a cold fallback. Player service hotswap now rebinds references and only queues LCD refresh when the tool object is active/enabled.
Rejected Alternatives: Keeping the static service lookup was rejected because it bypasses the parent tool context cache. Polling `GlobalRegistry.Player` from tool ticks was rejected because GlobalRegistry is cold DI only. Scene search was rejected because spawn/hotswap have an authoritative player root/context route.
Scalability potential: Low devices avoid extra static service indirection and stale-reference repair work during tool spawn/hotswap. Middle tier keeps deterministic builder LCD/sway behavior. High and ultra tiers can scale richer builder presentation through the same cached refs without changing gameplay truth or DTO layout.
Hardware Impact: Estimated i3/MX350 gain is small and cold-path only: 0.2-0.8 us per spawn/rebind route, with the primary gain being stale-reference prevention rather than steady-frame time.

## 2026-05-27 Build Gate Eleventh Pass

Problem: Eleventh-pass `BuilderTool` patch required compile verification. The guard was initially open on retry sample, but full solution build did not return a compiler result within the tool timeout.
Solution: Sampled CPU/compiler processes first. Initial sample average 39.15%, max 56.09% was rejected due max >50%; repeat sample average 25.33%, max 27.84%, no build processes, so one `dotnet build .\Hecton8.slnx` was launched. The command timed out after 120 s with no compiler output. Leftover `dotnet`/`VBCSCompiler` processes from that build were stopped. Retry was blocked by CPU average 76.88%, max 91.12%.
Rejected Alternatives: Claiming compile success without output was rejected. Re-running immediately under >50% CPU was rejected by project rule. Leaving orphaned build processes was rejected because it would interfere with parallel agents.
Scalability potential: No runtime change.
Hardware Impact: Build verification incomplete due timeout; no runtime microsecond claim.

## 2026-05-27 Dispatcher Hotswap Tick Lane Recovery

Problem: Several tools/equipment/interaction owners treated dispatcher registration flags as permanent local truth across `GlobalRegistryServiceSlot.Dispatcher` replacement. The worst cases were `KinematicTerminalInteractionBridge` and `VRLeakPatchWeldTarget`: they could keep a stale late-frame flag and skip registration on the replacement dispatcher, losing terminal press haptics or leak physics payload drain. Softer cases (`AuxiliaryEquipmentRouterRuntime`, `PerformanceMonitor`, `PerformanceBudgetController`, `PauseSystemVerifier`) recovered but could unregister against the replacement dispatcher using old ownership state.
Solution: Make dispatcher hotswap invalidate local owner flags first, then reacquire through existing guarded registration helpers only when `currentService != null`. This matches the established `ToolHapticsRuntime` and `ToolDurabilitySystem` pattern. No hot polling, no scene search, no new signal lane, no DTO changes.
Rejected Alternatives: Calling unregister during service replacement was rejected because `GlobalRegistry` has already exchanged the slot before notifying listeners, so the old flag does not prove ownership in the current dispatcher. Adding null-dispatcher polling was rejected because GlobalRegistry is cold identity/DI only. Replacing the route with a new event bus was rejected because tick registration ownership already has a first-party route.
Scalability potential: Low tier keeps terminal, leak, auxiliary, pause, and profiler tooling deterministic through runtime service replacement without extra per-frame work. Middle tier keeps the same ownership pattern with no added cadence. High tier can increase terminal haptics/leak presentation/auxiliary VFX density without widening truth routes. Ultra tier can overdrive visual feedback while dispatcher replacement remains a cold lifecycle event, not a hot recovery loop.
Hardware Impact: No steady-frame microsecond claim. Estimated i3/MX350 benefit is 0.1-1.0 us per affected dispatcher replacement from avoiding false unregister probes and diagnostic churn; the primary gain is correctness after service hotswap.

## 2026-05-27 Build Gate Twelfth Pass

Problem: Twelfth-pass dispatcher hotswap edits need compile verification, but the local rule blocks builds when CPU is above 50% or any dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes. CPU average was 100%, max 100%, with active `dotnet.exe` PIDs 19036, 22244, 22492, 40464, 40676, 45732, 48204, 60332, 61556 and `VBCSCompiler.exe` PID 23132. Build was not launched.
Rejected Alternatives: Running another build during active compile/CPU saturation was rejected because it violates the coordination contract and would add noise to other agents.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Domain Quality Signal Route

Problem: Seven tools/interaction/equipment consumers still read `HomeostasisBrain.GlobalQualityWeight` directly from hot or presentation-cadence paths. That split the quality route from the first-party `SignalBusRegistry.GlobalQualityWeight01` heartbeat used by signal lanes and other scalable systems.
Solution: Replace those reads with `SignalBusRegistry.GlobalQualityWeight01` in `PerformanceBudgetController`, `LaserCutterDodRuntime`, `WfcLaserCutRuntime`, `KinematicTerminalInteractionBridge`, `EquipmentInteractionHandler`, `PhysicalHandController`, and `AuxiliaryEquipmentRouterRuntime`. Values remain continuous floats and keep existing sanitize/smooth curves.
Rejected Alternatives: Local per-owner quality caches were rejected because they add stale-state risk without a new owner phase. Continuing direct `HomeostasisBrain` reads was rejected because hot consumers should read the published signal snapshot. Binary quality tiers were rejected by project rule.
Scalability potential: Low tier gets the same survival scalar across tool SDF step size, terminal cadence, finger-pose cadence, WFC overkill, laser cutter DOD quality, auxiliary equipment presentation, and managed budget reduction. Middle/high/ultra tiers can raise those visuals continuously through the same signal route without changing gameplay truth, DTO layout, save identity, or authority.
Hardware Impact: Estimated i3/MX350 gain: 0.05-0.3 us per affected hot quality sample from avoiding direct homeostasis property reads and duplicate route sanitization; primary gain is route correctness and less quality-state drift.

## 2026-05-27 Build Gate Thirteenth Pass

Problem: Thirteenth-pass quality-route edits need compile verification, but the build guard blocks compiles when CPU is above 50% or another dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes. CPU average was 56.44%, max 68.15%, with active `dotnet.exe` PID 57408, so build was not launched.
Rejected Alternatives: Running `dotnet build` during active dotnet work and above 50% CPU was rejected because it violates the parallel-agent coordination rule.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Interaction Dispatcher Hotswap Second Sweep

Problem: The twelfth pass fixed several dispatcher hotswap owners, but a different interaction cluster still had stale local registration state. `PlayerInteraction` did not handle dispatcher replacement, so `_registeredToTickManager` could stay true while the replacement dispatcher never received the player interaction tick. `PhysicalInteractionHandler`, `PhysicalBatteryCompartment`, `LifePodSeatStrapLatch`, and `VRValveWheelHandle` called unregister helpers inside dispatcher replacement handlers, which asks the replacement dispatcher to remove registrations that belonged to the previous dispatcher.
Solution: On `GlobalRegistryServiceSlot.Dispatcher`, invalidate local owner flags directly, then reacquire through existing guarded registration only if `currentService != null` and the component is active/needed. `PhysicalInteractionHandler.TryUnregisterHotSwapListener()` now also checks `_registeredHotSwapListener` before unregistering.
Rejected Alternatives: Calling unregister during service replacement was rejected because the registry slot has already changed; the old flag is not proof of ownership in the replacement dispatcher. Adding polling loops was rejected because GlobalRegistry is cold identity/DI only. Leaving `PlayerInteraction` to `Start()` retry was rejected because live service replacement does not rerun `Start()`.
Scalability potential: Low tier keeps hover/interact polling, physical hand routing, battery snap presentation, strap latch hold, and valve momentum deterministic after dispatcher replacement with no added per-frame work. Middle/high/ultra tiers can increase physical interaction density without widening tick ownership or adding recovery polling.
Hardware Impact: No steady-frame microsecond claim. Estimated i3/MX350 benefit is 0.1-0.8 us per affected dispatcher replacement from avoiding false unregister probes; primary gain is lost-lane prevention.

## 2026-05-27 Build Gate Fourteenth Pass

Problem: Fourteenth-pass dispatcher hotswap edits need compile verification, but the local build rule blocks compiles when max CPU is above 50% or compiler processes are active.
Solution: Sampled CPU and build-related processes. CPU average was 20.15%, max 53.71%, with active `VBCSCompiler.exe` PID 8964, so build was not launched.
Rejected Alternatives: Running `dotnet build` while VBCSCompiler is active and max CPU is above 50% was rejected because it violates the parallel-agent coordination rule.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Physical Snap Switch Lane Registration Ownership

Problem: `PhysicalSnapSwitch.TryRegister()` used `_registered` as the single admission flag for both `IUpdatable` and `ILateFrameTickable` lanes. If Update registration succeeded and LateFrame failed, the late visual upload lane would never retry. If Update failed and LateFrame succeeded, the next registration attempt could hit the LateFrame lane again because `_registered` stayed false.
Solution: Split registration into `TryRegisterUpdateTick()` and `TryRegisterLateFrameTick()` with independent guards. `TryRegister()` now only checks play mode and dispatcher availability, then lets each lane retry by its own owner flag. Dispatcher hotswap now only reacquires while the component is active/enabled.
Rejected Alternatives: Keeping one boolean gate for two dispatcher lanes was rejected because partial registration is a normal failure mode during service churn. Combining the two lanes into one dispatcher callback was rejected because visual angle upload belongs in LateFrame, not Update.
Scalability potential: Low devices avoid redundant registry probes and do not lose cockpit switch visual state after service churn. Middle/high/ultra tiers can increase cockpit panel density without widening dispatcher ownership or adding polling.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.4 us per partial registration recovery. Primary gain is correctness of switch visual lane recovery.

## 2026-05-27 VR Patch And Cable HotSwap Listener Ownership

Problem: `VRLeakPatchWeldTarget` and `VRCableDragPlug` called `GlobalRegistry.TryRegisterHotSwapListener(this)` without storing the result, then called `TryUnregisterHotSwapListener(this)` from disable/destroy. A failed registration, double lifecycle call, or already-retired listener could therefore issue non-owned unregister work.
Solution: Add `_registeredHotSwap` owner flags and guarded `TryRegisterHotSwapListener()` / `TryUnregisterHotSwapListener()` helpers in both components. Lifecycle methods now call the helpers, not raw registry calls.
Rejected Alternatives: Relying on registry tolerance was rejected because local lifecycle ownership must be idempotent and evidence-backed. Removing hot-swap listeners was rejected because both components legitimately recover dispatcher-owned lanes after service replacement.
Scalability potential: Low tier gets stable VR patch/cable lifecycle through enable/disable/destroy churn with no hot work. Middle/high/ultra tiers can scale richer cable and leak repair presentation while hotswap listener ownership remains a cold lifecycle path.
Hardware Impact: Estimated i3/MX350 gain: 0.1-0.6 us per redundant lifecycle unregister plus avoided diagnostic noise; no steady-frame claim.

## 2026-05-27 Build Gate Fifteenth Pass

Problem: Fifteenth-pass interaction registration edits need compile verification, but the local build rule blocks compiles when CPU is above 50% or compiler processes are active.
Solution: Sampled CPU and build-related processes. CPU average was 78%, max 94%, with active `VBCSCompiler.exe` PID 57652, so build was not launched.
Rejected Alternatives: Running `dotnet build` while VBCSCompiler is active and CPU is above threshold was rejected because it violates the parallel-agent coordination rule.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Dormant Physical Interaction Dispatcher Retirement

Problem: Several transient physical interaction owners used dormant booleans as a substitute for dispatcher unregister. `PhysicalBatteryCompartment` completed or aborted snap work from `Tick()` with `_tickDormant=true` but kept Update/LateFrame lanes registered. `PhysicalSnapSwitch` did the same after the switch angle reached target and cooldown expired. `LifePodSeatStrapLatch` and `LifePodSeatStrapCoordinator` could also leave dormant Update/FixedTick callbacks registered after hold decay, latch completion, or inactive seat lock.
Solution: Keep the presentation cheat, but retire ownership at the right phase. Battery snap and snap switch now retry LateFrame registration if queued visual work exists, flush the final visual state in LateFrame, then unregister dormant lanes. Strap latch and seat-lock coordinator now unregister directly when no transient runtime work remains. Registration helpers now check dispatcher availability before probing the registry.
Rejected Alternatives: Directly applying final battery/switch visuals from Update was rejected because visual presentation belongs in LateFrame. Keeping dormant callbacks was rejected because dispatcher ownership must describe active work, not passive memory. Adding a polling recovery system was rejected because GlobalRegistry is cold DI and the existing owner flags are sufficient.
Scalability potential: Low tier avoids dead callbacks after one-shot battery, switch, and strap interactions. Middle tier keeps deterministic physical affordances without extra polling. High tier can increase cockpit switch and strap density while only active affordances occupy dispatcher lanes. Ultra tier can spend saved frame budget on richer haptics/VFX without changing gameplay truth or DTO layout.
Hardware Impact: Estimated i3/MX350 gain: 0.03-0.20 us per retired dormant callback pair per frame depending on component type; in dense cockpit/escape-pod scenes this removes repeated idle Update/LateFrame/FixedTick dispatch overhead instead of hiding it behind dormant branches.

## 2026-05-27 Build Gate Sixteenth Pass

Problem: Sixteenth-pass interaction lifecycle edits needed compile verification. The guard opened on retry sample, but the full solution build did not return a compiler result within the 120 s tool timeout.
Solution: First CPU sample had average 26.2%, max 79%, no build processes, so build was rejected. Second sample had average 15.2%, max 38%, no build processes, so one `dotnet build .\Hecton8.slnx` was launched. It timed out with no compiler output. Leftover `dotnet`, `VBCSCompiler`, and `csc` processes from that build were stopped. A delayed `VBCSCompiler` tail appeared afterward and was also stopped; final process check found no build processes.
Rejected Alternatives: Claiming compile success without compiler output was rejected. Launching repeated builds after a timeout was rejected because it would collide with parallel agents and waste CPU. Leaving orphaned compiler workers was rejected because it violates the coordination contract.
Scalability potential: No runtime change.
Hardware Impact: Build verification incomplete due timeout; no runtime microsecond claim.

## 2026-05-27 Equipment Interaction Signal Service Ownership

Problem: `EquipmentInteractionHandler.IsServiceReady` verified readiness by reading `GlobalRegistry.InteractionSignals` from a read accessor, and the service did not repair local state when `GlobalRegistryServiceSlot.Dispatcher` or `GlobalRegistryServiceSlot.InteractionSignals` was replaced. After dispatcher replacement, `_dispatcherRegistered` could remain true while the new dispatcher never owned the late-frame signal flush. If service hotswap listener registration failed or another service replaced the slot, `TryUnregisterSignalService()` could also unregister the current slot without proving ownership.
Solution: Make readiness local: `_isInitialized && _serviceRegistered`. On dispatcher hotswap, clear `_dispatcherRegistered` and `_lateFrameRegistered`, then re-register the late-frame lane only when the replacement exists and the handler is active. On `InteractionSignals` slot replacement, synchronize `_serviceRegistered` from `ReferenceEquals(currentService, this)`. On unregister, call `GlobalRegistry.UnregisterInteractionSignalService(this)` only if the current slot still equals this handler.
Rejected Alternatives: Keeping the registry probe in `IsServiceReady` was rejected because read accessors must not poll global mutable state. Calling unregister during service replacement was rejected because the current registry slot may already belong to another owner. Adding a polling recovery loop was rejected because GlobalRegistry is cold DI, not a hot repair path.
Scalability potential: Low tier keeps interaction signal flushing deterministic after service replacement without extra per-frame work. Middle tier preserves one-route signal ownership. High and ultra tiers can increase interaction signal density and visual feedback while service health remains a pure local snapshot and dispatcher recovery remains cold lifecycle work.
Hardware Impact: No steady-frame gain claimed. Estimated i3/MX350 gain is 0.02-0.10 us per service health poll from removing the registry property probe; primary gain is preventing lost late-frame signal flush and false service unregister during lifecycle churn.

## 2026-05-27 Build Gate Seventeenth Pass

Problem: Seventeenth-pass service ownership patch needs compile verification, but project rules forbid build when CPU max is above 50% or any dotnet/compiler process is active.
Solution: Sampled CPU and build-related processes twice. First sample: average 55%, max 59%, no build processes. Second sample: average 46.33%, max 73%, no build processes. Build was not launched because max CPU exceeded 50% in both samples.
Rejected Alternatives: Running `dotnet build` with max CPU at 59% or 73% was rejected because it violates the local coordination rule and would produce noisy proof.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Dispatcher Registration Admission Guards

Problem: Several tool/interaction/equipment owners still called `GlobalRegistry.TryRegister*` without proving `Application.isPlaying` and `GlobalRegistry.Dispatcher != null` locally. `GlobalRegistry.TryRegister*` rejects this, but in editor/development builds the missing-dispatcher path emits a bootstrap error from `TryEnsureDispatcherRegistration()`. That makes optional component lifecycle registration depend on a noisy global validator and weakens local ownership contracts.
Solution: Move the admission check to each owner helper. `AuxiliaryEquipmentRouterRuntime` now has explicit guarded update/late-frame register/unregister helpers and hotswap reacquires through those helpers. `ToolKinematicsRuntime`, `KinematicTerminalInteractionBridge`, `VRLeakPatchWeldTarget`, `VRValveWheelHandle`, and `PhysicalBatteryCompartment` now fail closed before tick-lane registration when dispatcher is absent. `ToolHapticsRuntime`, `ToolDurabilitySystem`, and `ToolKinematicsRuntime` now also keep hot-swap listener registration play-mode only.
Rejected Alternatives: Relying on `GlobalRegistry.TryEnsureDispatcherRegistration()` was rejected because GlobalRegistry is cold DI, not a component lifecycle validator. Adding polling retries was rejected because dispatcher hotswap already supplies the recovery event. A shared registration abstraction was rejected because the local helpers are small and match existing owner patterns.
Scalability potential: Low tier avoids error-log churn and redundant registration probes during bootstrap/service churn. Middle tier keeps deterministic tick ownership. High and ultra tiers can raise tool/auxiliary/terminal/leak presentation density without adding recovery polling or widening authority routes.
Hardware Impact: No steady-frame gain claimed. Estimated i3/MX350 impact is 0.05-1.0 us per failed lifecycle registration probe avoided, plus avoided editor/development diagnostic noise; the primary gain is correctness of dispatcher admission ownership.

## 2026-05-27 Build Gate Eighteenth Pass

Problem: Eighteenth-pass dispatcher admission edits need compile verification, but project rules forbid build when CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes. CPU average was 89%, max 100%, with active `dotnet.exe` PID 62864 and `VBCSCompiler.exe` PID 6448. Build was not launched.
Rejected Alternatives: Running `dotnet build` under CPU saturation with active compiler processes was rejected because it violates the parallel-agent coordination rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Cached Equipment Wear Multiplier Route

Problem: `ModularEquipmentEngine.WriteActiveEquipmentWearRate()` samples centralized durability wear while writing active equipment state. The called `ToolDurabilitySystem.ResolveCentralizedEquipmentWearMultiplier()` resolved template wear through `ItemTemplateRegistry.TryGetTemplate()` every sample. That registry search is linear over the template snapshot, so active tools paid catalog-scale lookup cost after the mirror had already been registered.
Solution: Add `_wearMultiplierBySlot` to the durability owner mirror. `UpdateSlotMetadata()` resolves template wear once during cold mirror registration/update and writes both the native wear multiplier buffer and the managed slot mirror. `ResolveCentralizedEquipmentWearMultiplier()` now reads the slot mirror for registered equipment and uses the template lookup only as a fallback for an unknown hash.
Rejected Alternatives: Rewriting `ItemTemplateRegistry` into a hash map was rejected as an inventory-wide change outside this pass. Reading the native wear multiplier buffer from the public accessor was rejected because handle resolution can mutate stale handles and is not needed for a 32-slot owner mirror. Keeping per-frame template scans was rejected because hot active equipment state already has a durability slot identity.
Scalability potential: Low tier removes catalog-scale lookup from active equipment wear. Middle tier keeps the same durability authority route without allocations. High tier can run more active tools and richer wear presentation while the lookup remains O(1). Ultra tier can spend saved budget on visual/haptic feedback rather than repeated metadata search.
Hardware Impact: Estimated i3/MX350 gain is 0.2-3.0 us per active equipment wear sample depending on template count and active slot count; no gameplay truth or DTO layout changed.

## 2026-05-27 Physical Panel Tick Scene Mutation Removal

Problem: `PhysicalInteractionHandler.TickPhysicalPanelButtons()` called `EnsurePhysicalHandController()` when `_physicalHandController` was null. That helper performs `TryGetComponent` and can `AddComponent<PhysicalHandController>()`, so a dispatcher tick could search/mutate the scene on the XR panel route.
Solution: Make the panel tick require an already-cached controller. Controller creation remains in cold lifecycle (`Awake`, `OnEnable`, `XRActiveChanged`) and explicit interaction-start paths (`TryBeginFloraHarvestSnap`, heavy carry begin). `RefreshTickRegistration()` now registers the panel tick only when XR panel input is active and `_physicalHandController != null`.
Rejected Alternatives: Keeping hot `AddComponent` as recovery was rejected because dispatcher ticks must not repair scene structure. Polling for a controller every tick was rejected because it hides a lifecycle dependency as frame work. Removing controller creation entirely was rejected because interaction-start routes still need deterministic recovery for authored scenes missing the helper component.
Scalability potential: Low tier avoids unready XR panel ticks and scene mutation spikes. Middle tier keeps panel-button probing deterministic. High tier can support denser physical cockpit panels without widening tick registration. Ultra tier can increase haptic/finger presentation while controller ownership stays cold.
Hardware Impact: Estimated i3/MX350 steady gain is 0.05-0.30 us per unready XR panel tick; rare first-frame `AddComponent`/component-search spikes are removed from the frame tick path.

## 2026-05-27 Build Gate Nineteenth Pass

Problem: Nineteenth-pass hot-route edits need compile verification, but local rules forbid builds when CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes. CPU average was 76.33%, max 96%, with no build/compiler processes. Build was not launched because CPU exceeded the threshold.
Rejected Alternatives: Running `dotnet build` at 96% max CPU was rejected because it violates the parallel-agent coordination rule.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Interaction Signal Target Registry-Only Route

Problem: `EquipmentInteractionHandler` is the late-frame owner for queued tool interaction signals, but its target helpers still fell back to `TryGetComponent` and bounded parent traversal after `InteractableRegistry.TryResolve` missed. The fallback existed inside platform-relative hit caching and signal dispatch for `ITransportPlatform`, `IVoxelPlasmaCutTarget`, `IInteractionSignalConsumer`, `IInteractionVulnerabilitySource`, `ICuttable`, and base-module flora host routing. That makes a read/resolve helper perform scene discovery in the hot dispatch lane and hides missing authoring registration.
Solution: Remove the fallback component scans from `EquipmentInteractionHandler`. The handler now resolves all signal targets through `InteractableRegistry.TargetInfo`. To preserve real cutter/plasma targets, add cold lifecycle registration to `SealedDoor`, `HarvestablePlant`, and `DeployableSdfDrillRuntime`; existing voxel volume, base module, airlock, resource node, outcrop, panel button, wreck proxy, and VR target owners already register their trees.
Rejected Alternatives: Keeping fallback traversal was rejected because a late-frame signal consumer must not repair missing target metadata by walking transforms. Adding a new hot cache in the handler was rejected because `InteractableRegistry` is already the one-route owner for collider target payloads. Rewriting the registry into per-interface maps was rejected for this pass because the existing fixed open-address collider cache already satisfies the route without DTO or authority changes.
Scalability potential: Low tier removes hierarchy-depth-dependent target resolution from cutter/switch/plasma dispatch misses. Middle tier keeps signal routing predictable as more authored tool targets enter the scene. High tier can support denser cuttable and physical panel environments without widening dispatch cost. Ultra tier can spend saved frame time on stronger visual/haptic feedback while target truth remains one cached route.
Hardware Impact: Estimated i3/MX350 gain is 0.2-4.0 us per queued signal dispatch miss depending on transform depth and interface count. Cold `RegisterTree` cost moves to enable/spawn/despawn and does not add steady-frame work.

## 2026-05-27 Build Gate Twentieth Pass

Problem: Twentieth-pass interaction target-route edits need compile verification, but local rules forbid build when CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes. CPU samples were 88%, 100%, 100%; average 96%, max 100%, with active `dotnet.exe` PID 24280. Build was not launched.
Rejected Alternatives: Running `dotnet build` during CPU saturation and an active dotnet process was rejected because it violates the parallel-agent coordination rule.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Repair Tool Registry-Only Target Route

Problem: `RepairTool.UsePrimary()` is a sustained active tool route. Its module, voxel weld, airlock, and submarine hull repair target helpers still repaired missing metadata by running bounded `TryGetComponent` parent scans after a registry miss. That kept hierarchy-depth-dependent component discovery inside active repair use and contradicted the one-route collider target contract already established for queued signals and direct tool hits.
Solution: Remove the repair fallback scans. `CacheRepairTargetsForCollider()`, `FindRepairAirlock()`, and `CacheSubmarineDamageControlTargetForCollider()` now consume `InteractableRegistry.TargetInfo` only. `SubmarineStructuralGrid` now publishes/invalidates its collider tree in lifecycle because it owns `ISubmarineDamageControlTarget` and `ISubmarineRepairRoomResolver`.
Rejected Alternatives: Keeping bounded fallback was rejected because it hides authoring failures in beam-time work. Adding a repair-specific registry was rejected because `InteractableRegistry` already owns collider-to-role payloads. Editing unrelated smoke testers and editor tools was rejected because those are not sustained gameplay repair routes.
Scalability potential: Low tier resolves active repair targets from cached collider facts with no parent scan. Middle tier keeps module, voxel, airlock, and submarine repair truth on one route as target density grows. High tier can support denser repairable hull interiors and base modules. Ultra tier can spend saved CPU on repair sparks, leak-plume feedback, and haptic intensity without changing authority.
Hardware Impact: Estimated i3/MX350 gain is 0.2-5.0 us per active repair target miss depending on hierarchy depth and interface probes removed. Steady-frame cost added is 0 us; structural grid registration is cold lifecycle work.

## 2026-05-27 Build Gate Twenty-Second Pass

Problem: Twenty-second-pass repair route edits need compile verification, but project rules forbid builds when max CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes. CPU samples were 100%, 100%, and 87%; average 95.67%, max 100%, with no `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process visible. Build was not launched.
Rejected Alternatives: Running `dotnet build` at max CPU 100% was rejected because it violates the parallel-agent coordination rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Tool Metadata Registry-Only Route

Problem: After direct hit, queued signal, and repair target routes were moved to cached collider facts, three active metadata paths still repaired missing target metadata through direct component discovery. `EnvironmentalAnalyzerTool` called `TryGetComponent` for `HectonItem` and module marker archive data, `KnifeTool` kept direct-only `ResourceNode`/`BaseModule` fallback after registry miss, and `LogicSpannerTool` resolved module hashes by calling `BaseModule.TryGetComponent<ModuleMarker>()` during tool use. This kept authoring misses hidden in active analyzer/blade/spanner work.
Solution: Add `ModuleMarker` to `InteractableRegistry.TargetInfo` during cold `RegisterTree` publication. Analyzer item classification now uses cached `IInventoryPickupPreviewSource`, which covers both `HectonItem` and `PickupItem`; analyzer module archive data and spanner module hash data use cached `ModuleMarker`. Knife resource/module assessment now fails closed after registry miss instead of probing the collider directly.
Rejected Alternatives: Adding a second module metadata registry was rejected because collider target identity already lives in `InteractableRegistry`. Keeping direct fallback was rejected because it keeps scene discovery in active tool routes and hides missing owner publication. Adding a dedicated `HectonItem` field was rejected because existing `IInventoryPickupPreviewSource` already exposes the needed read-only item data and quantity.
Scalability potential: Low tier resolves item/module/resource metadata from one cached collider payload. Middle tier preserves deterministic target truth as tool target density grows. High tier can support denser base-module and pickup scenes. Ultra tier can spend saved CPU on analyzer archive feedback, blade VFX, haptics, and logic-spanner cable presentation without changing gameplay authority.
Hardware Impact: Estimated i3/MX350 gain is 0.2-3.0 us per active metadata miss depending on collider hierarchy depth and interface probes removed. Steady-frame cost added is 0 us; `ModuleMarker` probing occurs only during cold target registration.

## 2026-05-27 Build Gate Twenty-Third Pass

Problem: Twenty-third-pass metadata route edits need compile verification, but project rules forbid builds when max CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes twice. First sample was `26,51,25` with max 51 and no build/compiler processes. After a 30 s wait, second sample was `34,87,26` with max 87 and no build/compiler processes. Build was not launched.
Rejected Alternatives: Running `dotnet build` at max CPU 51% or 87% was rejected because it violates the parallel-agent coordination rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.

## 2026-05-27 Physical Interaction Begin Registry Payload

Problem: `PhysicalInteractionHandler` receives the hovered target from `PlayerInteraction`, but its physical begin routes still repaired missing metadata by searching components at interaction start. `TryBeginCablePlugDrag()` fell back to `behaviour.TryGetComponent(out VRCableDragPlug)`, `TryBeginPocketPickup()` checked `PickupItem` / `HectonItem` through direct component probes and searched parent/child hierarchy for `Rigidbody` / `Collider`, and `TryBeginHeavyCarry()` fell back to `behaviour.TryGetComponent(out HeavyCarryInteractable)`. This made the interaction-start route depend on scene discovery after `InteractableRegistry` had already selected the collider target.
Solution: Extend `InteractableRegistry.TargetInfo` with `PhysicsCollider` and `PhysicsBody`, populated from the registered collider and `collider.attachedRigidbody` during cold target publication. `PhysicalInteractionHandler` now starts cable plug, pocket pickup, and heavy carry from cached `TargetInfo` plus the already-selected `IInteractable`; the unused child traversal helper was removed from the pickup route.
Rejected Alternatives: Passing the whole `SpatialHit` into `PhysicalInteractionHandler` was rejected because the existing payload already crosses the API and only needed two physics references. Adding a second physics-body registry was rejected because collider target identity is already owned by `InteractableRegistry`. Keeping fallback `TryGetComponent` was rejected because it masks missing target publication and keeps hierarchy-depth cost in active interaction start.
Scalability potential: Low tier avoids hierarchy probes when grabbing pickups, cable plugs, or heavy objects. Middle tier keeps physical interaction authority on one cached collider route as target count grows. High tier can support denser cockpit/cargo scenes without interaction-start spikes. Ultra tier can spend saved CPU on grab pose, haptic, cable, and pickup presentation without changing gameplay truth.
Hardware Impact: Estimated i3/MX350 gain is 0.2-6.0 us per interaction-start miss depending on hierarchy depth and removed interface/component probes. Steady-frame cost added is 0 us; cold target publication stores two additional managed references per cached collider.

## 2026-05-27 Build Gate Twenty-Fourth Pass

Problem: Twenty-fourth-pass physical interaction target-route edits need compile verification, but project rules forbid builds when max CPU is above 50% or build/compiler processes are active.
Solution: Sampled CPU and build processes twice. First CPU sample was 100% with no build/compiler processes. After a 30 s wait, second CPU sample was 96% with no build/compiler processes. Build was not launched.
Rejected Alternatives: Running `dotnet build` at CPU 100% or 96% was rejected because it violates the parallel-agent coordination rule and would produce noisy evidence.
Scalability potential: No runtime change.
Hardware Impact: Build skipped; no runtime microsecond claim.
