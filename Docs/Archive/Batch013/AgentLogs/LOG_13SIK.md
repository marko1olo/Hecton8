# LOG_13SIK

## 2026-05-27 Intake

What was wrong: Active batch has no `13SIK` XML prompt, so there is no formal numbered task list for this ID.
What was done: Created status, rationale, and log files for ad-hoc Tools/Interaction domain audit.
Cinematic Cheats used: None yet.
Exact Microseconds saved: Not claimed; no profiler artifact.
Verification: Static file creation only. Compile/runtime not executed.

## 2026-05-27 Tools Interaction Audit Pass

What was wrong: `PerformanceBudgetController` used a binary quality switch. Registered systems jumped from full performance to `_throttleMultiplier` and back, which violates continuous `GlobalQualityWeight` and causes visible/CPU spikes at the threshold.
What was done: Replaced binary throttle with continuous pressure-based scalar using frame-time average, hysteresis, drop/recover rates, and `HomeostasisBrain.GlobalQualityWeight`. Existing `IBudgetManagedSystem.SetPerformanceLevel(float)` remains the route.
Cinematic Cheats used: Performance is now a scalar budget for cadence/capacity/visual density instead of a hard simulation switch. Low devices can shed optional presentation gradually; high/ultra can spend scalar on richer visuals.
Exact Microseconds saved: Estimated 18 us per managed-system transition burst avoided on i3/MX350. This is a threshold-spike estimate, not a profiler measurement.
Verification: Static scan confirmed no remaining `CheckAndApplyThrottling`, `SetPerformanceLevel(_throttleMultiplier)`, or binary restore call in `PerformanceBudgetController`.

What was wrong: `ToolKinematicsRuntime` could call `DataVault.EnsureGenerationHandle` from fixed, postfixed, slow, and read paths through `TryResolveVaultView`.
What was done: Added `allowCreate`; only cold bootstrap/rebind uses `TryResolveAllBuffers(true)`. Fixed/read/postfixed/slow paths resolve existing handles only and fail closed.
Cinematic Cheats used: None. This is ownership hygiene so visual beam/heat work stays deterministic.
Exact Microseconds saved: Estimated 4-40 us worst-case stall avoided when stale handles or capacity mismatch would have created/cleared native buffers in a frame.
Verification: Static scan confirmed hot ToolKinematics call sites use `allowCreate=false`; only bootstrap uses `true`.

What was wrong: `ToolHapticsRuntime` had the same hidden hot allocation route for front/back haptic command buffers. Tick, LateFrame, read snapshot, front count, enqueue, and store paths all used a resolver that could allocate.
What was done: Split haptic buffer resolution into hot read-only overloads and cold create calls. `Awake`, `OnEnable`, and active DataVault rebind call `EnsureBuffers`; haptic gameplay paths now fail closed if buffers are missing.
Cinematic Cheats used: Haptic waveform remains cheap triangle decay; no physics simulation added.
Exact Microseconds saved: Estimated 3-15 us stale-handle stall avoided on low-end hardware, with larger native clear spikes avoided during rebind.
Verification: Static scan confirmed only `EnsureBuffers(true)` can create haptic buffers; default runtime resolver overloads create nothing.

What remains: `PhysicalHandController` still contains an optional ArticulationBody fallback when `useKinematicSdfHandBridge=false`. Default is true, so current path uses the kinematic bridge; removing the fallback is a physics-domain decision with higher blast radius. `PhysicalBatteryCompartment.SetActive` is state-cached and not patched. WFC laser cut, ToolDurability, EquipmentInteraction, and AuxiliaryEquipment buffer creation routes already expose cold create/explicit initialize or existing-handle reads by the inspected paths.
Build/compile: Not run. CPU guard sampled 72-76%, then 91-100%; project rule forbids `dotnet build` above 50% CPU even without visible compiler processes.

## 2026-05-27 Tools Interaction Audit Pass 2

What was wrong: `PerformanceBudgetController.GetBudgetStatus()` no longer allocated, but it still mutated the owner snapshot from a `Get*` method.
What was done: Moved snapshot maintenance into register/report/apply/remove write paths. `GetBudgetStatus()` now returns the current owner snapshot without clearing/filling it.
Cinematic Cheats used: None. This is read-route purity and GC control.
Exact Microseconds saved: Estimated 5-20 us plus one dictionary allocation avoided per legacy status read.
Verification: Static scan confirmed snapshot writes occur outside `GetBudgetStatus()`; `git diff --check` reported no whitespace errors.

What was wrong: Root gameplay tools still had direct hierarchy/component discovery for target facts already owned by `InteractableRegistry`.
What was done: Routed `LogicSpannerTool` base module lookup, `SalvageSamplerTool` resource lookup, and `EnvironmentalAnalyzerTool` pickup/scannable/resource/base classification through `InteractableRegistry.TryResolve`.
Cinematic Cheats used: None. This is one-owner target routing so tool presentation stays deterministic and cheap.
Exact Microseconds saved: Estimated 2-12 us per cached target use; higher on deep prefab hierarchies.
Verification: Static scan confirmed the new registry routes. Residual direct lookups are limited to facts not yet owned by registry: `HectonItem`, `ModuleMarker`, and generic `ICuttable`.

What was wrong: `ToolHitUtility.TryPeekCollectible` and `TryCollectItem` still did direct pickup component discovery before common sampler/tool routes.
What was done: Added cached registry-first pickup source resolution with bounded component fallback for preview-only edge cases.
Cinematic Cheats used: None.
Exact Microseconds saved: Estimated 2-10 us per common pickup preview/collect check.
Verification: Static scan confirmed registry-first helper route in `ToolHitUtility`.

Build/compile: Not run. CPU guard sampled 100%, then 81-100%; project rule forbids `dotnet build` above 50% CPU.

## 2026-05-27 Tools Interaction Audit Pass 3

What was wrong: `InteractableRegistry.TryResolve(Collider, out TargetInfo)` was a read-looking accessor that built target info and wrote cache entries on misses. That made active analyzer/sampler/spanner/knife paths pay hidden hierarchy discovery cost and violated read accessor purity.
What was done: `TryResolve` now reads the existing cache only. Target discovery remains in `RegisterTree`/`RegisterCollider`, which are explicit lifecycle mutation routes. `ResourceNode` and `BaseModule` now publish/invalidate their collider trees so tool reads still have a valid owner route.
Cinematic Cheats used: None. This is ownership hygiene that preserves deterministic tool target facts.
Exact Microseconds saved: Estimated 5-35 us on first hot cache miss against deep target hierarchies; repeated target reads remain O(1).
Verification: `rg` confirmed `TryResolve` no longer calls `ResolveTargetInfo` or `CacheTarget`; only cold `RegisterCollider` performs discovery/cache write.

What was wrong: `KnifeTool` still used `GetComponentInParent<KnifeTool>`, `GetComponentInParent<ResourceNode>`, and `GetComponentInParent<BaseModule>` during active blade strike/read assessment.
What was done: Cached the tool transform in lifecycle, replaced own-collider detection with transform ownership, and routed resource/module facts through `InteractableRegistry` with direct-only fallback.
Cinematic Cheats used: None. The survival blade remains a bounded ray/assessment path, not a physics-heavy melee simulation.
Exact Microseconds saved: Estimated 2-12 us per tactical read/strike on nested prefabs.
Verification: `rg` confirmed touched tool files no longer contain those parent component searches.

What was wrong: `EquipmentInteractionHandler` walked collider parents for `ITransportPlatform` on every platform-relative queued interaction signal.
What was done: Added `ITransportPlatform` to `InteractableRegistry.TargetInfo`; `TryResolvePlatformTransform` now uses cached target info first and keeps the parent walk only as legacy fallback.
Cinematic Cheats used: Moving-platform hit rehydration remains a cached transform-local fake, not a live physics constraint.
Exact Microseconds saved: Estimated 2-16 us per registered transport-relative signal.
Verification: `rg` confirmed registry-first platform route; `git diff --check` reported no whitespace errors.

Build/compile: Not run. CPU guard sampled 99%; no `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process was visible, but project rule forbids `dotnet build` above 50% CPU.

## 2026-05-27 Tools Interaction Audit Pass 4

What was wrong: The remaining real hot-path debt was queued tool dispatch and shared hit helpers resolving role facts by direct `TryGetComponent` or bounded parent walks: signal consumers, vulnerability sources, voxel plasma/repair targets, cuttables, repairable modules, submarine repair targets, pickup previews, and damage receivers.
What was done: Expanded `InteractableRegistry.TargetInfo` with those tool-facing roles and changed `EquipmentInteractionHandler`, `RepairTool`, `ToolHitUtility`, and `SalvageSamplerTool` to read cached facts first. Legacy fallback remains only for unregistered cross-domain targets.
Cinematic Cheats used: Target identity is a cold cached fact. Interaction visuals can scale independently; no new physical simulation or same-frame job loop was introduced.
Exact Microseconds saved: Estimated 2-35 us per registered queued signal depending on hierarchy depth; estimated 2-18 us on repair target cache refresh; estimated 2-12 us on shared hit classification.
Verification: Static route scans confirmed registry-first reads and known remaining fallback calls in `EquipmentInteractionHandler`.

What was wrong: Several first-party tool-facing owners implemented interaction contracts but did not publish collider trees: `ScannableTarget`, construction weld targets, leak weld targets, voxel volumes, physical panel buttons, and wreck integrity proxies.
What was done: Added lifecycle/pool `InteractableRegistry.RegisterTree/InvalidateTree` publication to `ScannableTarget`, `VRConstructionWeldTarget`, `VRLeakPatchWeldTarget`, `HectonVoxelVolume`, `PhysicalPanelButton`, and `WreckIntegritySignalProxy`. `HectonVoxelVolume` is a justified cross-domain edit because it directly owns `IVoxelRepairWeldTarget`/`IVoxelPlasmaCutTarget` and has a fixed collider chunk cap of 8.
Cinematic Cheats used: Voxel/tool cuts still route through the existing DDA command surfaces; this patch only caches the owner identity.
Exact Microseconds saved: Estimated 2-20 us per registered scanner/weld/voxel/panel/wreck interaction hit by avoiding dispatch-time role discovery.
Verification: `rg` confirmed new register/invalidate sites. `git diff --check` reported no whitespace errors, only existing Git LF->CRLF warnings.

What remains: `SubmarineAtmosphereSystem` still implements `IInteractionSignalConsumer` but was not registered because it appears on a broad submarine runtime owner and needs owner-domain review before adding full child-collider traversal. `EquipmentInteractionHandler` keeps guarded fallback for that and other legacy objects.
Build/compile: Not run. CPU guard sampled 99% and active `dotnet.exe` PID 21804 was present; project rule forbids launching another build when CPU >50% or another dotnet/compiler process is active.

## 2026-05-27 Tools Interaction Audit Pass 5

What was wrong: Direct inventory pickup ran before `PhysicalInteractionHandler`, so standard `PickupItem`/`HectonItem` targets could enter inventory immediately and bypass the authored pull-to-hand physical pickup sequence.
What was done: `PlayerInteraction` now stores the current hover `TargetInfo`, refreshes it on same-target hover updates, and calls `PhysicalInteractionHandler` before direct inventory fallback. The physical handler has an internal overload that consumes cached target facts.
Cinematic Cheats used: Pocket pickup remains a cheap kinematic/visual pull instead of a joint or full physics simulation.
Exact Microseconds saved: Estimated 2-8 us on interaction start by reusing cached target info; primary fix is behavioral correctness.
Verification: Static scan confirmed physical interception precedes `TryHandleInventoryPickup`.

What was wrong: `InteractableRegistry.RegisterCollider` used the expanded `HasAny` role set to also populate spatial hover search. Non-hover role-only colliders could consume fixed hover registry slots and still be skipped later.
What was done: The role cache still stores all valid tool facts, but the spatial hover registry now only receives colliders with `TargetInfo.Interactable != null`.
Cinematic Cheats used: None. This keeps the prompt ray cheap while allowing tool-only colliders to be cached for dispatch.
Exact Microseconds saved: Avoids up to 4096 wasted spatial slots in mixed scenes; per-hover scan avoids role-only collider checks.
Verification: Static scan confirmed the `info.Interactable == null` early return after cache write.

What was wrong: Physical pickup child-component discovery was recursive without a depth cap, and carry/cable owners lacked explicit destroy-time cache invalidation.
What was done: Added bounded recursion for `TryResolveOwnedComponent`, plus `OnDestroy` invalidation for `HeavyCarryInteractable` and `VRCableDragPlug`.
Cinematic Cheats used: Heavy carry remains visual/kinematic hand control with continuous load scalar, not a high-cost physics realism loop.
Exact Microseconds saved: No normal-frame claim; pathological hierarchy traversal is capped at 32 levels.
Verification: `git diff --check` reported no whitespace errors, only existing LF->CRLF warnings.

Build/compile: Not run. CPU guard sampled 79% and active `dotnet.exe` PIDs 19552/29316 were present; project rule blocks build.

## 2026-05-27 Tools Interaction Audit Pass 6

What was wrong: `InteractableRegistry.RegisterCollider()` could leave stale spatial hover entries when a previously interactable collider re-published with role-only facts or no facts. `TryResolveSpatialTarget()` also used reciprocal `Mathf.Sqrt` in the prompt ray path.
What was done: Re-registration now removes cache/spatial rows when ownership disappears and unregisters spatial rows for role-only payloads after updating the role cache. Hover ray normalization now uses `math.rsqrt`.
Cinematic Cheats used: None. This is target-route ownership hygiene.
Exact Microseconds saved: Estimated 0.1-0.4 us per target-probe from `math.rsqrt`; stale-row fix avoids up to 4096 wasted spatial hover slots.
Verification: Static scan confirmed no `Mathf.Sqrt` remains in the patched registry route.

What was wrong: Pocket pickup called `Rigidbody.MovePosition` while the body was already kinematic and collision-disabled. The same path cached angular velocity but did not restore it on abort/cancel.
What was done: Removed `FixedTickPocketPickup`; all pocket pickup movement now queues through the existing LateFrame visual pose flush. Non-kinematic body restore now queues angular velocity through `IPhysicsService.QueueAngularVelocitySet`.
Cinematic Cheats used: Pocket pickup is presentation after collision is disabled; inventory handoff remains the gameplay truth.
Exact Microseconds saved: Removes one direct Rigidbody move and one pocket-only FixedTick need per active pickup frame; profiler proof absent.
Verification: `rg` confirmed no `MovePosition`, `MoveRotation`, or `FixedTickPocketPickup` remains in patched routes.

What was wrong: Several interactable owners published collider trees but had no explicit destroy-time invalidation.
What was done: Added `OnDestroy` invalidation to `SaveStation`, `LifePodSeatStrapLatch`, `ClimbableLadder`, `NarrativeDiscovery`, `HectonItem`, `ResourceNode`, `StorageCrate`, `HarvestableOutcrop`, and `ResourceRecyclerModule`.
Cinematic Cheats used: None. This is stale-cache teardown hardening.
Exact Microseconds saved: Stale-cache prevention; no steady-frame microsecond claim.
Verification: `rg` confirmed destroy invalidation in patched UTF-safe owners. `Gameplay/EndingTerminalInteractable.cs` remains unpatched because `apply_patch` rejects invalid UTF-8 bytes and raw recoding was rejected.

Build/compile: Not run. CPU average 22.7%, max 26.51%, active `dotnet.exe` PID 19552; project rule blocks build while another dotnet/compiler process is active.

## 2026-05-27 Tools Interaction Audit Pass 7

What was wrong: `IInteractionSignalService.TryResolvePrimarySurfaceHit` looked like a pure read but represented a service-owned frame-latent request/response route.
What was done: Renamed the contract and all tool callers to `RequestPrimarySurfaceHit`.
Cinematic Cheats used: None. This is route-contract cleanup.
Exact Microseconds saved: No frame-time claim; the fix prevents false read-accessor semantics.
Verification: `rg` found no `TryResolvePrimarySurfaceHit` symbols.

What was wrong: Equipped tool durability reads and active-use drain still used string IDs/dictionaries on common hot paths.
What was done: Added hash-id durability read APIs and `TryDrainDurabilityByTime(uint, ...)`; `ToolDurabilitySystem` maintains fixed slot mirrors for hot reads while preserving string maps for UI/save/cold compatibility. `PlayerTool` now uses hash-first reads/drain with guarded string fallback only when hash or registration is missing.
Cinematic Cheats used: None. This is authority-route hygiene; visual tool wear can scale separately.
Exact Microseconds saved: Estimated 1-6 us per active durability read/drain cluster on i3/MX350-class CPU, depending on call density.
Verification: Static scan confirmed hash APIs and hash-first `PlayerTool` route.

What was wrong: `InteractionEvents.Enqueue()` could allocate/prewarm native queues on the first hover or interaction event.
What was done: Added `InteractionEvents.PrewarmCold()` and called it from `PlayerInteraction.Awake()` in play mode.
Cinematic Cheats used: None.
Exact Microseconds saved: Removes first-event NativeQueue allocation/prewarm from hover producer path; no steady-frame claim.
Verification: Static scan confirmed the prewarm route and retained defensive `EnsureInitialized()`.

What was wrong: `PhysicalHandController.ShouldBypassXRHandKinematicUpdate()` polled `InputDispatcher.ActiveRuntimeInstance` in the XR idle FixedTick path.
What was done: Cached `InputDispatcher` through cold lifecycle/hotswap and changed FixedTick logic to read `_inputDispatcher`.
Cinematic Cheats used: XR idle hand bypass remains a cheap predictability rule instead of a high-cost physical hand update when no tracked input changes.
Exact Microseconds saved: Estimated 0.1-0.3 us per XR idle FixedTick on i3/MX350-class CPU.
Verification: Static scan confirmed the hot method uses `_inputDispatcher`; static fallback remains only inside the cold cache method.

What was wrong: `PerformanceBudgetController.GetBudgetStatus()` exposed the mutable owner dictionary type.
What was done: Changed the public return type to `IReadOnlyDictionary<string, SystemBudgetInfo>`.
Cinematic Cheats used: None.
Exact Microseconds saved: Contract-hardening; no direct frame-time claim.
Verification: `rg` confirmed the read-only signature.

Build/compile: Not run. CPU average 18.07%, max 38.09%, active `dotnet.exe` PIDs 3984/15116/29516/39028/41884/53636/61024; project rule blocks build while another dotnet/compiler process is active.

## 2026-05-27 Tools Interaction Audit Pass 8

What was wrong: `InteractionUI.RefreshInteractPrefixCache()` could call cold service resolution from prompt refresh routes, and `SetPromptVisible()` could run `TryGetComponent/AddComponent<CanvasGroup>()` from the first hover event.
What was done: Prompt refresh now reads cached input/localization service fields. `GlobalRegistry.NativeInputRuntime` and `GlobalRegistry.LocalizationText` are used only in cold cache/hotswap routes. `SetPromptVisible()` only changes an already-cached `CanvasGroup`; `Awake/OnEnable/Start` own initialization.
Cinematic Cheats used: Prompt display remains event-driven and cache-backed; no polling loop or dynamic UI rebuild is introduced.
Exact Microseconds saved: Estimated 0.2-1.2 us per prompt refresh on i3/MX350-class CPU, plus a first-hover component creation hitch avoided when the container lacked `CanvasGroup`.
Verification: `rg` confirmed no stale `ResolveLocalizationManager` or parameterless runtime input subscription path remains in `InteractionUI`; `git diff --check` reported no whitespace errors.

What was wrong: `PhysicalInteractionHandler.TryBeginFloraHarvestSnap()` read `DestructibleOrganicManager.ActiveRuntimeInstance` directly from an active physical-hand route.
What was done: `IOrganicToolHitService` now includes `TryResolveNearestHarvestInteractionPoint(...)`; `PhysicalInteractionHandler` caches `GlobalRegistry.OrganicToolHits` and updates it via `DestructibleOrganicRuntime` hotswap. The existing `DestructibleOrganicManager` public resolver satisfies the interface directly.
Cinematic Cheats used: Flora hand snap remains a short visual/IK snap to an owner-resolved point, not a physics-heavy plant interaction simulation.
Exact Microseconds saved: Estimated 0.1-0.4 us per snap request; the main fix is one owner route instead of world-singleton coupling.
Verification: `rg` confirmed `PhysicalInteractionHandler` no longer references `DestructibleOrganicManager.ActiveRuntimeInstance`; harvest snap resolves through the organic service contract.

What was wrong: `InteractableRegistry.EstimateBoundsNormal()` used `Vector3.normalized` as a prompt-hit fallback.
What was done: Replaced fallback normalization with finite length-squared validation plus `math.rsqrt`.
Cinematic Cheats used: Bounds normals remain a cheap prompt affordance approximation.
Exact Microseconds saved: Estimated 0.05-0.2 us on rare fallback cases.
Verification: `rg` confirmed no `.normalized` remains in the patched registry route.

Build/compile: Not run. CPU average 59.11%, max 77.97%, active `dotnet.exe` PID 36172 and `VBCSCompiler.exe` PID 62064; project rule blocks build.

## 2026-05-27 Tools Interaction Audit Pass 9

What was wrong: `PhysicalBatteryCompartment.TryResolveTool` was a read-looking method used by properties and swap paths, but it refreshed `_cachedBatteryTool` on cache miss.
What was done: Replaced it with pure `TryGetCachedTool`. Cache mutation now happens in lifecycle/on-validate and an explicit `RefreshBatteryToolCacheCold()` command.
Cinematic Cheats used: Battery insertion remains a short kinematic visual snap, not a physical simulation.
Exact Microseconds saved: Estimated 1-4 us on cold-miss battery state/property paths; primary fix is read-accessor rule compliance.
Verification: `rg` found no `TryResolveTool` in touched battery/prologue/test files.

What was wrong: Removing lazy cache mutation risked inactive scrubber sockets found through `GetComponentInChildren(true)` before normal enable refresh.
What was done: `LifePodTactilePrologueController.ResolveColdReferences()` now explicitly calls `o2ScrubberSocket.RefreshBatteryToolCacheCold()`. `LifePodTactilePrologueSmokeTester` was updated to enforce the new contract.
Cinematic Cheats used: None. This is cold binding hygiene.
Exact Microseconds saved: Correctness fix; no steady-frame claim.
Verification: Static scan confirmed the cold command and smoke-test marker.

What was wrong: Failed/aborted battery snap restore used `QueueTorque(body, angularDelta, ForceMode.VelocityChange)` to restore a target angular velocity.
What was done: Angular restore now queues `IPhysicsService.QueueAngularVelocitySet(body, restoredAngularVelocity)` after finite sanitization and delta thresholding.
Cinematic Cheats used: Snap motion stays visual/kinematic until final battery authority handoff.
Exact Microseconds saved: No steady-frame claim; fixes one wrong angular-force interpretation per failed snap restore.
Verification: Static scan found no `QueueTorque(body, angularDelta` in `PhysicalBatteryCompartment`.

What was wrong: `PhysicalSnapSwitch.Unregister()` always asked `GlobalRegistry` to unregister the UI updatable even when `_registered` was false.
What was done: The unregister route is now guarded by `_registered`, matching existing late-frame guard behavior.
Cinematic Cheats used: Switch angle interpolation remains no-trig approximate presentation.
Exact Microseconds saved: Estimated 0.2-1.0 us and possible dev-log noise avoided per redundant unregister.
Verification: Diff check and source read confirmed the unregister call is inside the owner flag branch.

What was wrong: `PhysicalHandController` still had a player runtime singleton route in the kinematic bridge root AUP resolver and a haptic helper, and velocity signal magnitude used `sqrt` after `rsqrt`.
What was done: Cached `IPlayerRuntimeContext` through cold lifecycle and player hotswap. Root AUP/haptic helper reads use the cached interface. Velocity signal speed uses `speedSq * invSpeed`.
Cinematic Cheats used: Kinematic hand damage signal remains visual-only; exact sqrt is not needed for the affordance.
Exact Microseconds saved: Estimated 0.1-0.4 us per kinematic bridge update cluster on i3/MX350-class CPU.
Verification: `rg` found no `PlayerRuntimeContextService` or `math.sqrt(speedSq)` in `PhysicalHandController`.

Build/compile: Not run. Latest CPU average 83.33%, max 93%, active `VBCSCompiler.exe` PID 19668; project rule blocks build while CPU is above 50% or any compiler process is active.

## 2026-05-27 Tools Interaction Audit Pass 10

What was wrong: Hash-first durability APIs existed, but `ToolDurabilitySystem.ResolveSlot(uint)` still scanned all 32 slots for every common equipped-tool durability read/drain. Environmental corrosion also rebuilt the current tool hash from `ToolData.PersistentId` or `metadata.toolID` instead of using the owner-cached durability mirror.
What was done: Added `_slotByItemHash`, maintained only from registration/metadata write paths. Hash reads validate that cold index first and use the old 32-slot scan only as a defensive fallback. Environmental corrosion now consumes `PlayerTool.TryGetDurabilityMirror()` and passes the same tool ID/hash/max durability route used by active tool drain.
Cinematic Cheats used: None. This is durability authority-route cleanup; visual tool wear remains scalable presentation.
Exact Microseconds saved: Estimated 0.3-2.0 us per common hash durability read/drain cluster, plus 0.2-0.8 us per held-tool corrosion slow tick on i3/MX350-class CPU.
Verification: `rg` confirmed no `LocHash.Compute(currentTool...)` or `Animator.StringToHash(metadata.toolID)` remains in `ToolDurabilitySystem`. `git diff --check -- Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs` reported no whitespace errors, only the existing LF->CRLF warning.

Build/compile: Not run. CPU average 69.81%, max 82.7%, no dotnet/csc/MSBuild/VBCSCompiler process visible; project rule blocks build while CPU is above 50%.

## 2026-05-27 Tools Interaction Audit Pass 11

What was wrong: `BuilderTool.OnSpawn()` still reached `PlayerRuntimeContextService.TryGetActiveRuntimeContext` directly for camera binding, while builder/inventory/camera references had no repair route when `GlobalRegistryServiceSlot.Player` was replaced. This split player ownership into two routes and could leave stale scene references after player-context hotswap.
What was done: `BuilderTool` now binds through the cached `IPlayerRuntimeContext` exposed by `PlayerTool.TryGetPlayerRuntimeContext`. A new cold helper binds `PlayerBuilder`, `PlayerInventory`, and camera transform from that interface, with direct player-root `TryGetComponent` as the cold fallback. Player service hotswap now rebinds those references and queues LCD refresh only if the tool object is active/enabled.
Cinematic Cheats used: Builder LCD/sway remain presentation-only cached transforms/material blocks; no physics or scene search is introduced to make the tool feel heavier.
Exact Microseconds saved: Estimated 0.2-0.8 us per spawn/rebind on i3/MX350-class CPU; primary gain is one-route correctness and stale-reference prevention, not steady-frame savings.
Verification: `rg` confirmed no `PlayerRuntimeContextService` remains in `BuilderTool.cs`; broad tool scan shows only `PlayerToolManager.TryBindPlayerRoot` cold owner-binding calls. `git diff --check -- Assets/_Project/Scripts/BuilderTool.cs` reported no whitespace errors, only the existing LF->CRLF warning. Local hot-pattern scan found no new parent-search, scene-search, LINQ/list allocation, or `.normalized` route in `BuilderTool`.

Build/compile: Attempted after guard opened. First CPU sample was rejected because max was 56.09%; repeat sample was CPU average 25.33%, max 27.84%, no build processes, so one `dotnet build .\Hecton8.slnx` was launched. It timed out after 120 s with no compiler output. Leftover `dotnet`/`VBCSCompiler` processes from that build were stopped. Retry was blocked by CPU average 76.88%, max 91.12%, no build processes left after cleanup.

## 2026-05-27 Tools Interaction Audit Pass 12

What was wrong: Multiple dispatcher hotswap listeners in the tools/equipment/interaction domain carried stale registration flags across `GlobalRegistryServiceSlot.Dispatcher` replacement. `KinematicTerminalInteractionBridge` cleared update registration but not late-frame registration, so a replacement dispatcher could miss terminal press haptic/late-frame work. `VRLeakPatchWeldTarget` cleared patch-hold update registration but not the physics payload reader late-frame registration, so leak acoustic/physics payloads could stop draining after dispatcher replacement. `AuxiliaryEquipmentRouterRuntime`, `PerformanceMonitor`, `PerformanceBudgetController`, and `PauseSystemVerifier` used unregister/register probes against the current dispatcher even though their local flags described the old dispatcher.
What was done: Hotswap handlers now invalidate all local dispatcher-owned flags first and reacquire through existing guarded registration paths only when the replacement dispatcher exists. Patched files: `KinematicTerminalInteractionBridge.cs`, `VRLeakPatchWeldTarget.cs`, `AuxiliaryEquipmentRouterRuntime.cs`, `PerformanceMonitor.cs`, `PerformanceBudgetController.cs`, `PauseSystemVerifier.cs`.
Cinematic Cheats used: Terminal presses, leak guidance, auxiliary VFX upload, and profiling/budget tools remain deterministic presentation/control lanes. No physical simulation, polling fallback, or runtime search was added.
Exact Microseconds saved: No steady-frame claim. Estimated 0.1-1.0 us saved per affected dispatcher replacement by avoiding false unregister probes; primary fix is lost-lane prevention after service hotswap.
Verification: `git diff --check` over all six pass files reported no whitespace errors, only existing LF->CRLF warnings. `rg` over `Assets/_Project/Scripts/Tools`, `Interaction`, and `Equipment` found no remaining direct `Dispatcher && currentService != null` stale-flag pattern in the audited domain. Remaining dispatcher handlers either clear flags directly or use explicit should-restore physical-state logic.

Build/compile: Not run. Build gate blocked by CPU average 100%, max 100%, active `dotnet.exe` PIDs 19036/22244/22492/40464/40676/45732/48204/60332/61556 and `VBCSCompiler.exe` PID 23132.

## 2026-05-27 Tools Interaction Audit Pass 13

What was wrong: The domain still had seven direct `HomeostasisBrain.GlobalQualityWeight` reads in tools/interaction/equipment paths: performance budget reduction, laser DOD quality fallback, WFC cutter visual overkill, terminal tick interval, tool SDF step size, physical hand finger-pose cadence, and auxiliary equipment tuning. That split scalable presentation policy away from the `SignalBusRegistry.GlobalQualityWeight01` hot signal route.
What was done: Replaced those reads with `SignalBusRegistry.GlobalQualityWeight01` in `PerformanceBudgetController.cs`, `LaserCutterDodRuntime.cs`, `WfcLaserCutRuntime.cs`, `KinematicTerminalInteractionBridge.cs`, `EquipmentInteractionHandler.cs`, `PhysicalHandController.cs`, and `AuxiliaryEquipmentRouterRuntime.cs`. Added `Hecton8.Core.Contracts.Signals` imports only where missing. A suspected duplicate `_recentFrameCursor++` was checked and rejected as a false positive; no code change was made for it.
Cinematic Cheats used: Quality still drives cheap continuous presentation controls: SDF step size, cutter sparkle/overkill, terminal cadence, finger-pose cadence, auxiliary visual density, and managed budget scalar. No physical simulation, scene search, or binary quality switch was introduced.
Exact Microseconds saved: Estimated 0.05-0.3 us per affected hot quality sample on i3/MX350-class CPU. Main gain is one-route quality ownership and reduced state drift, not a large frame-time claim.
Verification: `rg -n "HomeostasisBrain\\.GlobalQualityWeight" Assets/_Project/Scripts/Tools Assets/_Project/Scripts/Interaction Assets/_Project/Scripts/Equipment -g '*.cs'` returned no matches. `git diff --check` over the seven changed files reported no whitespace errors, only existing LF->CRLF warnings.

Build/compile: Not run. Build gate blocked by CPU average 56.44%, max 68.15%, active `dotnet.exe` PID 57408.

## 2026-05-27 Tools Interaction Audit Pass 14

What was wrong: More dispatcher hotswap defects remained in the interaction side of the domain. `PlayerInteraction` had no dispatcher replacement branch, so it could keep `_registeredToTickManager=true` after the old dispatcher was replaced and never register into the new dispatcher. `PhysicalInteractionHandler`, `PhysicalBatteryCompartment`, `LifePodSeatStrapLatch`, and `VRValveWheelHandle` used unregister helpers from their dispatcher replacement handlers, which targets the replacement dispatcher with ownership flags from the previous dispatcher.
What was done: Added dispatcher hotswap repair to `PlayerInteraction`. Replaced unregister-in-hotswap behavior with local flag invalidation and guarded reacquire in `PhysicalInteractionHandler`, `PhysicalBatteryCompartment`, `LifePodSeatStrapLatch`, and `VRValveWheelHandle`. Also guarded `PhysicalInteractionHandler.TryUnregisterHotSwapListener()` so disable/destroy does not ask registry to unregister a non-owned listener.
Cinematic Cheats used: None added. This keeps interaction tick ownership deterministic for existing visual/physical affordances: hover scan, pocket/battery presentation, strap hold, and valve wheel momentum.
Exact Microseconds saved: No steady-frame claim. Estimated 0.1-0.8 us per affected dispatcher replacement by avoiding false unregister probes; the main fix is preventing lost interaction tick lanes.
Verification: `git diff --check` over the five changed interaction files reported no whitespace errors, only existing LF->CRLF warnings. Static `rg` confirmed dispatcher replacement branches in those files now use local flag invalidation; remaining unregister helper calls are normal disable/complete/grab-release paths.

Build/compile: Not run. Build gate blocked by CPU average 20.15%, max 53.71%, active `VBCSCompiler.exe` PID 8964.

## 2026-05-27 Tools Interaction Audit Pass 15

What was wrong: `PhysicalSnapSwitch` used one `_registered` gate for both Update and LateFrame dispatcher lanes, so partial lane registration could either lose switch visual upload retry or repeat LateFrame registration attempts. `VRLeakPatchWeldTarget` and `VRCableDragPlug` registered hot-swap listeners without storing ownership, then unregistered from disable/destroy without proof that the listener was owned.
What was done: `PhysicalSnapSwitch` now retries Update and LateFrame lanes independently via `TryRegisterUpdateTick()` and `TryRegisterLateFrameTick()`, with dispatcher availability checked before registration. `VRLeakPatchWeldTarget` and `VRCableDragPlug` now track `_registeredHotSwap` and route lifecycle register/unregister through guarded helpers.
Cinematic Cheats used: No simulation added. This preserves cockpit switch, cable, and leak repair presentation as deterministic lifecycle work instead of adding runtime polling or physics truth.
Exact Microseconds saved: Estimated 0.1-0.4 us per partial snap-switch lane recovery and 0.1-0.6 us per redundant VR hot-swap unregister. No steady-frame gain claimed.
Verification: Static `rg` confirmed lifecycle paths use guarded helpers and `PhysicalSnapSwitch` has separate lane registration helpers. `git diff --check` passed with only LF->CRLF warnings. Compile is PENDING VERIFICATION because CPU average was 78%, max 94%, and `VBCSCompiler.exe` PID 57652 was active.

## 2026-05-27 Tools Interaction Audit Pass 16

What was wrong: Transient physical interaction owners still treated dormant flags as enough. `PhysicalBatteryCompartment` could finish or abort battery snap and keep Update/LateFrame callbacks registered. `PhysicalSnapSwitch` could settle at target angle and keep UI Update/LateFrame callbacks registered. `LifePodSeatStrapLatch` and `LifePodSeatStrapCoordinator` could keep dormant Update/FixedTick callbacks after hold decay, latch completion, or inactive seat lock.
What was done: `PhysicalBatteryCompartment` and `PhysicalSnapSwitch` now retry their LateFrame visual lane when queued visual work exists, flush final visual state in LateFrame, and unregister dormant lanes after pending work is gone. `LifePodSeatStrapLatch` unregisters on latch completion or hold decay. `LifePodSeatStrapCoordinator` unregisters FixedTick when dormant or the seat lock is inactive. Registration helpers now guard `GlobalRegistry.Dispatcher == null`.
Cinematic Cheats used: Battery cell snap and switch angle motion remain visual fakes: cheap local transform/angle interpolation, late-frame presentation flush, no new physics truth and no polling fallback.
Exact Microseconds saved: Estimated 0.03-0.20 us per retired dormant callback pair per frame on i3/MX350-class CPU. In dense cockpit/escape-pod layouts this removes repeated idle dispatcher calls instead of returning early forever.
Verification: `rg` confirmed remaining dormant writes in touched files are paired with retirement/final-flush paths. `git diff --check` over the four files reported no whitespace errors, only existing LF->CRLF warnings. One guarded `dotnet build .\Hecton8.slnx` was attempted after CPU max 38% and no compiler processes; it timed out after 120 s with no compiler output. Leftover `dotnet`, `VBCSCompiler`, and `csc` processes from that build were stopped, including a delayed `VBCSCompiler` tail; final process check found no build processes.

## 2026-05-27 Tools Interaction Audit Pass 17

What was wrong: `EquipmentInteractionHandler` still mixed read health and global ownership. `IsServiceReady` read `GlobalRegistry.InteractionSignals` directly, dispatcher hotswap did not clear/reacquire the late-frame signal flush lane, and service unregister did not prove that this instance still owned the `InteractionSignals` slot.
What was done: `IsServiceReady` now reads local `_isInitialized && _serviceRegistered`. `OnGlobalRegistryServiceReplaced` handles `Dispatcher` by clearing `_dispatcherRegistered`/`_lateFrameRegistered` and re-registering when active, and handles `InteractionSignals` by syncing `_serviceRegistered` from the replacement slot. `TryUnregisterSignalService()` now unregisters only when `ReferenceEquals(GlobalRegistry.InteractionSignals, this)`.
Cinematic Cheats used: None added. This preserves the existing late-frame signal flush and interaction presentation route without adding polling, scene search, or physical simulation.
Exact Microseconds saved: No steady-frame claim. Estimated 0.02-0.10 us per service health poll from removing the registry property probe; primary savings are avoided false unregister work and lost-lane recovery after service hotswap.
Verification: `rg` confirmed the pure local `IsServiceReady`, dispatcher/interaction signal hotswap branches, and cold-only `ReferenceEquals(GlobalRegistry.InteractionSignals, this)` checks. `ToolHapticsRuntime`, `ToolDurabilitySystem`, and `ToolKinematicsRuntime` were inspected and did not need this patch. `git diff --check -- Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs` reported no whitespace errors, only existing LF->CRLF warning.

Build/compile: Not run. Build gate blocked twice: first CPU sample average 55%, max 59%, no build processes; second average 46.33%, max 73%, no build processes. Project rule blocks build when max CPU is above 50%.

## 2026-05-27 - 13SIK - Eighteenth Pass Dispatcher Admission Guards

What was wrong:
- `AuxiliaryEquipmentRouterRuntime` duplicated update/late-frame dispatcher registration in `OnEnable` and dispatcher hotswap instead of routing through owner helpers.
- `ToolKinematicsRuntime`, `KinematicTerminalInteractionBridge`, `VRLeakPatchWeldTarget`, `VRValveWheelHandle`, and `PhysicalBatteryCompartment` had tick-lane registration helpers that could call `GlobalRegistry.TryRegister*` without local dispatcher-null admission checks.
- `ToolHapticsRuntime` and `ToolDurabilitySystem` registered hot-swap listeners without a local play-mode guard, unlike neighboring runtime owners.

What was done:
- Added guarded dispatcher registration helpers in `AuxiliaryEquipmentRouterRuntime`.
- Added play-mode plus dispatcher guards to tool kinematics fixed/postfixed/slow lanes, terminal update/late-frame lanes, leak payload late-frame reader, valve momentum update lane, and battery update lane helper.
- Added play-mode guards to haptics/durability/kinematics hot-swap listener helpers.

Cinematic Cheats used:
- No physical simulation change. Existing late-frame visual/presentation cheats stay intact; this pass only prevents invalid dispatcher admission and registry noise.

Exact Microseconds saved:
- Steady frame: 0 us claimed.
- Failed lifecycle registration / dispatcher-missing path: estimated 0.05-1.0 us per avoided probe on i3/MX350, plus avoided editor/development error-log churn.

Verification:
- `rg` registration scan across Tools/Interaction/Equipment confirmed touched `GlobalRegistry.TryRegister*` calls sit behind local play/dispatcher guards or `PhysicalInteractionHandler`'s `_dispatcherAvailable` snapshot.
- `rg` hot-swap listener scan confirmed newly touched haptics/durability/kinematics listener registration is play-mode guarded.
- `git diff --check` over eight changed runtime files: no whitespace errors; only existing LF->CRLF warnings.
- Build not launched: CPU average 89%, max 100%, active `dotnet.exe` PID 62864 and `VBCSCompiler.exe` PID 6448.

## 2026-05-27 - 13SIK - Nineteenth Pass Hot Equipment Read Route

What was wrong:
- Active equipment wear state sampled `ToolDurabilitySystem.ResolveCentralizedEquipmentWearMultiplier()` from `ModularEquipmentEngine.WriteActiveEquipmentWearRate()`. Registered tools still paid `ItemTemplateRegistry.TryGetTemplate()` fallback cost every active wear-rate write even though durability already had a slot identity. `ItemTemplateRegistry.TryGetTemplate()` is a linear template snapshot search.
- `PhysicalInteractionHandler.TickPhysicalPanelButtons()` could call `EnsurePhysicalHandController()` from dispatcher `Tick`; that helper can `TryGetComponent` and `AddComponent<PhysicalHandController>()`, creating a hidden scene-mutation spike in the XR panel path.

What was done:
- `ToolDurabilitySystem` now keeps `_wearMultiplierBySlot` next to the existing durability/broken/hash mirrors. `UpdateSlotMetadata()` resolves template wear once during mirror registration/update. Registered active equipment reads the cached slot multiplier; unknown hashes keep the old fallback.
- `PhysicalInteractionHandler` no longer creates/searches a physical hand controller from panel-button tick. Controller creation remains lifecycle/interaction-start work. Panel tick registration now requires XR panel input and an already-cached controller.

Cinematic Cheats used:
- No simulation added. This pass preserves the cheap active-equipment wear drain and physical-panel probe presentation while removing metadata search and scene repair from hot routes.

Exact Microseconds saved:
- Equipment wear: estimated 0.2-3.0 us per active equipment wear sample on i3/MX350 depending on template count and active slot count.
- Physical panel tick: estimated 0.05-0.30 us saved per unready XR panel tick, plus removal of rare `AddComponent`/component-search millisecond spikes from dispatcher `Tick`.

Verification:
- `rg` confirmed `TickPhysicalPanelButtons()` no longer calls `EnsurePhysicalHandController()` and panel tick readiness depends on `_physicalHandController != null`.
- `rg` confirmed active registered equipment reads `_wearMultiplierBySlot`; `ItemTemplateRegistry.TryGetTemplate()` remains only in `ResolveWearMultiplier()` fallback/registration path.
- `git diff --check -- Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs`: no whitespace errors; only existing LF->CRLF warnings.
- Build not launched: CPU average 76.33%, max 96%, no build/compiler processes. Project rule blocks build when CPU is above 50%.
## 2026-05-27 - Pass 20 - Interaction Signal Target Route

What was wrong:
- `EquipmentInteractionHandler` still used direct `TryGetComponent` and parent traversal after `InteractableRegistry` miss inside late-frame signal dispatch and platform-relative hit caching.
- This violated one-route target ownership: a queued tool signal could discover scene components in the hot path instead of consuming the cold collider payload.
- `SealedDoor`, `HarvestablePlant`, and `DeployableSdfDrillRuntime` implemented cut/signal-relevant contracts but did not publish themselves into the cold interaction registry.

What was done:
- `EquipmentInteractionHandler` now resolves `ITransportPlatform`, `IVoxelPlasmaCutTarget`, `IInteractionSignalConsumer`, `IInteractionVulnerabilitySource`, `ICuttable`, and base-module flora host state only through `InteractableRegistry.TargetInfo`.
- Removed handler-local parent traversal helper and `MaxParentResolveDepth` from the signal owner.
- Added `InteractableRegistry.RegisterTree(this)` / `InvalidateTree(this)` lifecycle ownership to `SealedDoor`, `HarvestablePlant`, and `DeployableSdfDrillRuntime`.

Cinematic Cheats used:
- No physical simulation added. Existing plasma/cut visual route is preserved; saved CPU remains available for stronger VFX/haptics instead of hierarchy scanning.

Exact Microseconds saved:
- Estimated i3/MX350: 0.2-4.0 us per queued signal dispatch miss depending on target hierarchy depth and interface probes removed.
- Steady-frame cost added: 0 us. Registration cost is cold enable/spawn/despawn only.

Verification:
- `rg` found no `TryGetComponent`, `TryResolveParentComponent`, `GetComponentInParent`, or `MaxParentResolveDepth` in `EquipmentInteractionHandler.cs`.
- `git diff --check` passed for touched files; only existing LF->CRLF warnings.
- Build skipped by guard: CPU average 96%, max 100%, active `dotnet.exe` PID 24280.

## 2026-05-27 - Pass 21 - Direct Tool Hit Route

What was wrong:
- `ToolHitUtility` still used direct `TryGetComponent` and bounded parent traversal after registry misses for shared direct impact tools.
- A knife/harpoon/stun/sampler hit could discover `ICuttable`, `IDamageReceiver`, pickup sources, or fallback rigidbody ownership during active tool use.
- Fauna, player health, and submarine hull damage owners were not publishing their damage/cut collider facts into `InteractableRegistry`, so deleting the fallback without owner publication would break real targets.

What was done:
- Removed `MaxParentResolveDepth` and the generic parent-walk helper from `ToolHitUtility`.
- `ToolHitUtility` now resolves cuttable, damage receiver, pickup preview/source only from `InteractableRegistry.TargetInfo`.
- `TryGetRigidbody` now uses `Collider.attachedRigidbody` only; this preserves compound-collider impulse routing without scene traversal.
- Added cold `InteractableRegistry.RegisterTree` / `InvalidateTree` ownership to `FaunaBrain`, `HectonPlayerHealth`, and `SubmarineAutoLevelBallastController`.
- Dead fauna invalidates its interaction target tree immediately when `Die()` sets `_isDead`.

Cinematic Cheats used:
- No physical simulation added. This is a routing cleanup; saved CPU can be spent by existing hit VFX, haptics, scanner feedback, and hull/fauna impact presentation.

Exact Microseconds saved:
- Estimated i3/MX350: 0.2-6.0 us per direct hit miss depending on hierarchy depth and interface probes removed.
- Steady-frame cost added: 0 us. New registry work is cold lifecycle registration/invalidation.

Verification:
- `rg` found no `TryGetComponent`, `GetComponentInParent`, `TryFindParentComponentBounded`, or `MaxParentResolveDepth` in `ToolHitUtility.cs`.
- `rg` confirmed `FaunaBrain`, `HectonPlayerHealth`, and `SubmarineAutoLevelBallastController` now register/invalidate interaction target trees.
- `rg` confirmed common pickup sources (`HectonItem`, `PickupItem`) already publish their trees.
- `git diff --check` over touched files reported no whitespace errors, only existing LF->CRLF warnings.

Build/compile:
- Not run. Build gate blocked: CPU samples 100%, 93.66%, 58.3%; average 83.99%, max 100%, no build/compiler processes. Project rule blocks build when max CPU is above 50%.

## 2026-05-27 - Pass 22 - Repair Tool Target Route

What was wrong:
- `RepairTool` still used bounded component/parent fallback discovery inside sustained primary repair use for repair modules, voxel weld targets, airlocks, and submarine damage-control targets.
- `SubmarineStructuralGrid` implements `ISubmarineDamageControlTarget` and `ISubmarineRepairRoomResolver`, but was not publishing those collider facts to `InteractableRegistry`, forcing the repair beam to find the physics component at beam time.

What was done:
- Removed `FindParentComponentBounded`, `FindSubmarineDamageTarget`, and the unused `MaxRepairParentResolveDepth` from `RepairTool`.
- `RepairTool` now resolves module repair, voxel weld repair, airlock weld override, and submarine damage-control targets through `InteractableRegistry.TargetInfo`.
- Added cold `InteractableRegistry.RegisterTree(this)` / `InvalidateTree(this)` to `SubmarineStructuralGrid` lifecycle. Existing unrelated dirty changes in that file were not reverted or claimed.

Cinematic Cheats used:
- No simulation added. The repair beam remains a deterministic gameplay ray plus late-frame visual/audio/haptic presentation; saved CPU stays available for sparks, leak-plume response, and haptics.

Exact Microseconds saved:
- Estimated i3/MX350: 0.2-5.0 us per active repair target miss depending on hierarchy depth and interface probes removed.
- Steady-frame cost added: 0 us. Structural target publication is cold lifecycle registration/invalidation.

Verification:
- `rg` found no `FindParentComponentBounded`, `FindSubmarineDamageTarget`, `MaxRepairParentResolveDepth`, or old submarine damage-target `TryGetComponent` probe in `RepairTool.cs`.
- `rg` confirmed `SubmarineStructuralGrid` imports `Hecton8.Interaction` and registers/invalidates through `InteractableRegistry`.
- `git diff --check -- Assets/_Project/Scripts/RepairTool.cs Assets/_Project/Scripts/SubmarineStructuralGrid.cs`: no whitespace errors; only existing LF->CRLF warnings.

Build/compile:
- Not run. Build gate blocked: CPU samples 100%, 100%, 87%; average 95.67%, max 100%, no build/compiler processes. Project rule blocks build when max CPU is above 50%.

## 2026-05-27 - Pass 23 - Tool Metadata Target Route

What was wrong:
- `EnvironmentalAnalyzerTool` still discovered item and module archive metadata by `TryGetComponent` during active analyzer use.
- `KnifeTool` still had direct-only `ResourceNode` / `BaseModule` fallback after a registry miss.
- `LogicSpannerTool` still resolved module hash IDs by querying `ModuleMarker` from the selected module during tool use.

What was done:
- Added `ModuleMarker` to `InteractableRegistry.TargetInfo`, resolved only in cold `RegisterTree` publication.
- `EnvironmentalAnalyzerTool` now reads item metadata through cached `IInventoryPickupPreviewSource`; this covers `HectonItem` and `PickupItem` without adding a `HectonItem`-specific field.
- Analyzer module archive and logic-spanner module hash routing now use cached `targetInfo.ModuleMarker`.
- Removed the remaining direct-only resource/module fallback from `KnifeTool`.

Cinematic Cheats used:
- No physical simulation added. This is target-metadata routing; saved CPU is budget for analyzer feedback, blade VFX, haptics, and spanner cable presentation.

Exact Microseconds saved:
- Estimated i3/MX350: 0.2-3.0 us per active metadata miss depending on hierarchy depth and interface probes removed.
- Steady-frame cost added: 0 us. `ModuleMarker` probing happens during cold target registration only.

Verification:
- `rg` found no direct collider/module/target `TryGetComponent`, `GetComponentInParent`, parent helper, or parent-depth constant in `EnvironmentalAnalyzerTool.cs`, `KnifeTool.cs`, or `LogicSpannerTool.cs`.
- `rg` confirmed `ModuleMarker` is carried by `InteractableRegistry.TargetInfo` and consumed by analyzer/spanner.
- `git diff --check` over touched files reported no whitespace errors; only existing LF->CRLF warnings.

Build/compile:
- Not run. Build gate blocked twice: first CPU sample `26,51,25` max 51; second sample after 30 s wait `34,87,26` max 87. No `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` processes were visible.

## 2026-05-27 - Pass 24 - Physical Interaction Begin Route

What was wrong:
- `PhysicalInteractionHandler` still searched components at interaction start after `PlayerInteraction` had already selected a registered collider target.
- Cable plug begin fell back to `behaviour.TryGetComponent(out VRCableDragPlug)`.
- Pocket pickup begin checked pickup type through direct component probes and searched hierarchy for `Rigidbody` / `Collider`.
- Heavy carry begin fell back to `behaviour.TryGetComponent(out HeavyCarryInteractable)`.

What was done:
- Added `PhysicsCollider` and `PhysicsBody` to `InteractableRegistry.TargetInfo`.
- Populated those fields during cold `ResolveTargetInfo()` from the registered collider and `collider.attachedRigidbody`.
- Changed physical cable, pocket pickup, and heavy-carry begin routes to use cached `TargetInfo` plus the selected `IInteractable`.
- Removed the now-unused child traversal helper from `PhysicalInteractionHandler`.

Cinematic Cheats used:
- No physical simulation added. This is interaction target routing cleanup; saved CPU is budget for pickup motion, haptic, cable, and grab presentation.

Exact Microseconds saved:
- Estimated i3/MX350: 0.2-6.0 us per interaction-start miss depending on transform depth and component probes removed.
- Steady-frame cost added: 0 us. Cold registration stores two additional references per cached collider target.

Verification:
- `rg` found no begin-route `TryGetComponent<PickupItem>`, `TryGetComponent<HectonItem>`, `TryGetComponent(out Rigidbody)`, `TryGetComponent(out Collider)`, `TryGetComponent(out HeavyCarryInteractable)`, `TryGetComponent(out VRCableDragPlug)`, or `TryResolveOwnedComponent` in `PhysicalInteractionHandler.cs`.
- `rg` confirmed `PhysicsCollider` / `PhysicsBody` are in `InteractableRegistry.TargetInfo` and consumed by `PhysicalInteractionHandler`.
- `git diff --check -- Assets/_Project/Scripts/Interaction/InteractableRegistry.cs Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs`: no whitespace errors; only existing LF->CRLF warnings.

Build/compile:
- Not run. Build gate blocked twice: CPU 100%, then 96% after 30 s wait. No `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` processes were visible.
