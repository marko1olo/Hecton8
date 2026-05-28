# Rationale 1425

Status: SOURCE PATCHED / APEX STATIC VERIFIED / VWS AND COMBAT NARROW LANES PATCHED / NO BUILD LAUNCHED
Builds Launched: 0
Reports Generated: 0 JSON, 0 binary dumps

## Decision Log

Problem: Runtime static scan found `GlobalRegistry.UnregisterLateFrameTickable` inside `ShinobuOceanSurfaceAtmosphereRuntime.LateFrameTick`, executed after deferred GPU readback disposal.
Solution: Added internal `SystemDispatcher.UnregisterLateFrameTickableDirect` and called it from LateFrameTick after readback buffers are disposed. DOD pattern: dispatcher owns hot lane mutation; GlobalRegistry remains cold identity/DI route.
Rejected Alternatives: Leaving disabled object registered until OnDisable; that fails because OnDisable already ran and pending readback completion happens later. Replacing with a quiesce bool was rejected because it preserves stale dispatcher membership. Calling dotnet build was rejected by CPU/build policy.
Scalability potential: Low/Middle/High/Ultra all remove registry indirection from the deferred cleanup path; high tiers do not gain a new feature, they avoid one avoidable control-plane hop.
Hardware Impact: i3/MX350 class avoids one registry route and service guard during deferred cleanup. Estimated saving: 0.5-3 us per cleanup event; not a per-frame steady-state claim.

Problem: SignalBus payload purity could be broken later even though current source scan found no managed fields.
Solution: Added Editor generic-constraint test over loaded `ISignal` value types using `where T : unmanaged, ISignal`, then strengthened it with `RuntimeHelpers.IsReferenceOrContainsReferences<T>()`. DOD pattern: compile-time generic constraint plus runtime reference-containment guard as proof artifact.
Rejected Alternatives: JSON audit report; manual list of every payload; rewriting already-unmanaged payloads.
Scalability potential: Low through Ultra keep signal lanes blittable and Burst-compatible; no gameplay truth changes.
Hardware Impact: No immediate runtime delta; prevents future managed payload from introducing GC or copy overhead.

Problem: Hot-swap reference rebinding had to be proven without touching production audio services.
Solution: Added local Editor mock `IAudioService` and `IGlobalRegistryHotSwapListener` probe to assert replacement binds the new service reference without registry polling. DOD pattern: cold cached reference updated by listener event.
Rejected Alternatives: Live GlobalRegistry test mutation; adding new production listener to unrelated classes; direct dependency on SpatialAudioManager.
Scalability potential: Low devices avoid service lookup churn; higher tiers keep hotswap resilience without a per-frame cost.
Hardware Impact: Editor-only verification; production runtime unchanged.

Problem: Public methods with DataVault write locks exist across domains; blind hoisting can break public service APIs during a dirty multi-agent session.
Solution: Mapped 73 public lock surfaces and sampled live queue routes. Current sampled methods release in `finally`; no signature changed without full caller graph and compile gate.
Rejected Alternatives: Breaking `IAudioService`, combat status, or public static queue signatures in a no-build window.
Scalability potential: Lock ownership remains visible inside owner route; future hoist needs a dedicated API migration slice.
Hardware Impact: No runtime delta from this pass; avoids compile break and agent collision.

Problem: Static singleton surfaces remain in repo.
Solution: Scanned static `Instance` accessors and direct usages. No singleton was deleted because several are compatibility/editor routes or still have in-repo callers.
Rejected Alternatives: Removing ABI compatibility accessors such as `AsyncLoadHelper.Instance => null`; deleting used routes without replacement interface.
Scalability potential: No immediate runtime improvement; prevents unsafe breakage.
Hardware Impact: 0 us from this pass.

Problem: `ShinobuOceanSurfaceAtmosphereRuntime.TryApplyTunerValues` held waves, weather, atmosphere, and optional Beaufort profile write locks at the same time.
Solution: Split the tuner into `TryPrepareTunerHandle` plus one-lock write helpers for weather, atmosphere, waves, and profiles. Each successful write lock is released in that helper's `finally`; the public tuner method no longer owns or releases write locks.
Rejected Alternatives: Keeping the stacked locks because the caller is editor-only; batching all values into a new DTO without a compile window.
Scalability potential: Low/Middle/High/Ultra keep the same visual tuning knobs while removing a deadlock vector. Weak devices gain stability; high-end devices keep overkill wave tuning without lock chains.
Hardware Impact: No steady-frame claim; editor/tuner write path avoids holding up to four DataVault write locks simultaneously.

Problem: `LogisticsNetworkGraph.WritePowerBlackBoxSample` acquired telemetry ring and cursor write locks simultaneously.
Solution: Split ring and cursor acquisition. The method writes the telemetry entry under the ring lock, releases it in `finally`, then acquires the cursor lock and updates the cursor under a second `finally`. DOD pattern: single owner route, no nested write locks.
Rejected Alternatives: Creating a combined ring+cursor DTO in the dirty tree; dropping blackbox telemetry entirely.
Scalability potential: Low devices avoid deadlock stalls in power telemetry; high-end devices retain the 300-frame blackbox ring without wider lock scope.
Hardware Impact: Expected frame-time delta is near 0 us; risk reduction is lock-order safety, not throughput.

Problem: `MantaEmergencyWreck.ResidencyRuntime.LateFrameTick` called `TryGetComponent` after respawning a dehydrated wreck from the pool.
Solution: Added a static last-spawned Manta component cache updated by `OnSpawn` and cleared by `OnDespawn`; hydration resolves the component via `TryResolveLastSpawnedWreck` with no Unity component discovery in LateFrameTick.
Rejected Alternatives: Dictionary cache keyed by instance ID, which adds cold allocation and a managed hash lookup; leaving `TryGetComponent` in the frame phase.
Scalability potential: Low devices remove a Unity hierarchy/component lookup from rare hydration; higher tiers keep identical residency visuals and physics.
Hardware Impact: Removes one component lookup per Manta wreck hydration event; no per-frame steady-state claim.

Problem: The stricter lock scan found `VocalWarningSystem.TryAcquireVwsWriteViews` acquiring a monolithic queue/priority/cooldown/current/dispatch/profile/tuning/telemetry writer batch, while public `TryQueueWarning` allowed external domains to mutate the VWS queue directly.
Solution: Converted VWS storage access to `TryResolveVwsOwnerViews`, using current-phase owner aliases and zero DataVault write-lock calls inside VWS. Converted public `TryQueueWarning` into a thin `SignalBus<VocalWarningSignal>` producer so external domains no longer mutate VWS native queue state. DOD pattern: producers publish unmanaged signals; VWS owner phase drains signal snapshots and mutates its own storage.
Rejected Alternatives: Keeping one giant writer fence batch; splitting only telemetry and leaving public direct queue mutation; using a managed command queue.
Scalability potential: Low devices avoid lock contention and audio/physics service coupling; middle/high/ultra tiers retain the same priority and dispatch jobs while keeping vocal warning admission asynchronous.
Hardware Impact: No measured throughput claim; removes 11/12 simultaneous VWS writer fences and one public cross-domain mutation route.

Problem: `SubmarineFluidDynamics` cached `IVocalWarningSystem`, refreshed it from `GlobalRegistry.VocalWarnings`, and called `TryQueueWarning` for ballast warning emission.
Solution: Removed the cached VWS field, hot-swap branch, cold refresh, and clear-cache entry. The ballast warning now publishes a `VocalWarningSignal` directly through `SignalBus<VocalWarningSignal>`.
Rejected Alternatives: Retaining the direct service dependency because it was cold-cached; adding another hot-swap listener path around an avoidable dependency.
Scalability potential: Low/Middle/High/Ultra all get the same warning semantics without physics depending on audio runtime lifetime.
Hardware Impact: One direct service call and service cache branch removed from ballast warning path; primary gain is dependency isolation.

Problem: Combat target mutation still uses 18-lock bundles and sometimes nests with armor/status locks.
Solution: Left combat as a documented residual because register/unregister/clear paths coordinate side-state moves across combat, armor, and status buffers. Subagent audit identified narrower safe candidates, but a compile-gated slice is required.
Rejected Alternatives: Blindly replacing full combat view helpers with partial views under a dirty multi-agent tree; using mutable owner aliases for public combat methods without a command-lane migration.
Scalability potential: Future combat slice should convert common health/protection/hit-profile sync into narrow owner-phase commands while preserving full locks only for structural target table mutation.
Hardware Impact: 0 us from this pass; avoids destabilizing combat target identity without compile/test proof.

Problem: Combat common sync paths (`SyncTargetHealth`, `SyncTargetProtection`, `SyncTargetHitProfile`, `RefreshTargetHitProfile`) used the full 18-lock combat target bundle even when they only touched health/protection/hit-profile arrays.
Solution: Added narrow current-phase owner-view helpers in `CombatDamageRuntime_VaultViews`: target slot read-only lookup, health owner views, protection owner views, hit-profile owner views, and lookup clear owner view. Rewired the common sync paths and cold lookup clear to use those helpers instead of `TryAcquireCombatTargetWriteLocks`.
Rejected Alternatives: Holding reduced groups of writer locks; this still violates the no-simultaneous-lock rule. Rewriting structural register/unregister side-state moves without compile/test proof was rejected.
Scalability potential: Low devices avoid broad combat lock residency on common sync calls; higher tiers keep identical combat truth while reducing lock contention around dense target updates.
Hardware Impact: No measured frame claim. Static lock count for the changed combat sync paths drops from 18 combat target write locks to 0 combat target write locks.

Problem: Combat queue-reject telemetry held telemetry ring and telemetry state writer locks simultaneously.
Solution: Replaced the ring+state writer-fence helper with `TryResolveCombatTelemetryOwnerViews` and rewired `RecordQueueRejectTelemetry` to write the blackbox sample through current-phase owner views.
Rejected Alternatives: Splitting ring and cursor into two writer-lock windows; this can advance cursor without entry on partial failure and still performs lock traffic on a diagnostic route.
Scalability potential: Low/Middle/High/Ultra retain blackbox visibility without a deadlock vector in anomaly telemetry.
Hardware Impact: Removes two simultaneous telemetry writer locks from the queue-reject route; throughput not measured.

Problem: A private `ClearSlot(int)` wrapper still acquired armor, status, and the full combat target lock bundle, but source search showed no call sites outside the local overload with explicit views.
Solution: Deleted the unused wrapper and kept the active `ClearSlot(int, ref CombatDamageVaultViews)` helper used by `UnregisterTarget` under the existing structural transaction.
Rejected Alternatives: Keeping dead lock-bearing code for theoretical reuse; rewriting register/unregister identity moves without compile/test proof.
Scalability potential: Low devices avoid future accidental resurrection of a wide lock path; high/ultra tiers keep identical combat target cleanup semantics.
Hardware Impact: 0 us measured. Static call-site count for `ClearSlot(int)` lock wrapper dropped from 1 definition / 0 callers to 0 definitions.
