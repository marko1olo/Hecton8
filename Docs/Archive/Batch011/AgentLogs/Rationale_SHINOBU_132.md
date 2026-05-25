# Rationale_SHINOBU_132

## Active Decision Journal

Problem: Unity joint/LineRenderer cable paths violate deterministic cable simulation and force CPU mesh rebuilds.
Solution: Keep SHINOBU cable domain on Burst Verlet and move CaveBioRootsGenerator visual cable/vine path to ConnectionSplineBatchRenderer pipe-link submissions.
Rejected Alternatives: ConfigurableJoint chains, Rigidbody rope links, and LineRenderer point updates; all feed non-deterministic PhysX or managed mesh churn.
Scalability potential: Low uses fewer solver iterations and spline samples; middle keeps stable Verlet with reduced visual vertices; high/ultra spends saved CPU on spline density and shader-side cable richness.
Hardware Impact: i3/MX350 gains from removing per-frame LineRenderer mesh rebuild and PhysX joint solver load; estimated cable/vine path savings are tens to hundreds of microseconds under dense scenes.

Problem: Removing generated bio-root children made the old child-index deactivation path unsafe for authored child objects.
Solution: Preserve only legacy `_BioRoot_` cleanup and route active visuals through stable long link IDs in the shared spline renderer.
Rejected Alternatives: Deactivate every child by index or keep hidden LineRenderer GameObjects alive; both leak old assumptions into the new procedural route.
Scalability potential: Low/middle/high/ultra all reuse the same service path; only descriptor density and shader presentation scale elsewhere.
Hardware Impact: Prevents accidental hierarchy churn and avoids per-root component updates on low-end CPUs.

Problem: Native pointers in Burst jobs lacked explicit alias proof for Node buffers.
Solution: Add [NoAlias] to CableNodeDTO* fields and keep mutation through raw pointers/ref semantics.
Rejected Alternatives: NativeArray indexer property mutation or interface-driven solver abstractions; both add copies/dispatch risk.
Scalability potential: SIMD-friendly aliasing helps all tiers; high tier can afford more iterations without raising C# overhead.
Hardware Impact: NEON/AVX auto-vectorization is less likely to be blocked; expected microsecond savings scale with node count.

Problem: Telemetry dump contract names Dump_CABLE_SURGEON.bin while existing path only emitted SHINOBU-specific dump.
Solution: Write both Dump_SHINOBU_132.bin and Dump_CABLE_SURGEON.bin from the same telemetry ring.
Rejected Alternatives: Alias via docs only; forensic tooling needs the requested binary path.
Scalability potential: No runtime allocation path; dump occurs only on failure/manual forensic capture.
Hardware Impact: No steady-frame cost beyond the already scheduled fixed ring write.

Problem: Guarded dotnet build cannot prove SHINOBU in current workspace because generated project state and unrelated domains are broken.
Solution: Stop after one guarded build, record compile wall, inspect csproj inclusion/static scans before any further build. `Hecton8.Core.csproj` is generated and currently omits untracked `CablePhysicsSolver132.cs`/`TetherAupVerletJobs.cs`, so manual csproj surgery was rejected.
Rejected Alternatives: Repeated builds, broad csproj rewrites, or touching unrelated domains; those violate compile-wall and domain boundary.
Scalability potential: Maintains iteration health for all agents.
Hardware Impact: Avoids wasting CPU on known stale/unrestored generated project state.

Problem: SHINOBU_132 reacquired `SignalBus<PhysicsEventPayload>` by configuring the global lane from a FixedTick scheduling path.
Solution: Remove hot `Configure` and only call `EnsureInitialized`; Core `GlobalSignals` remains the single owner of PhysicsEventPayload lane capacity/hash.
Rejected Alternatives: Per-domain signal lane reconfiguration or a private duplicate queue; both create split ownership and potential hot-path allocation/rebind risk.
Scalability potential: Low/middle/high/ultra all share the same owner-local event lane; solver work scales by quality, not by signal setup.
Hardware Impact: Removes a repeated hot-path global configuration branch from the fixed-tick cable schedule.

Problem: SHINOBU_132 FixedTick finalized the mock job with direct `JobHandle.Complete()`.
Solution: Route completion through `DispatcherJobFence.TryFinalizeCompleted` for tick finalization and reserve `TryComplete(forceComplete: true)` for teardown.
Rejected Alternatives: Blocking Complete after `IsCompleted` or pushing a new dispatcher dependency API into Core; the first violates the mandate wording, the second expands the compile wall.
Scalability potential: Low tiers avoid hidden sync points; high/ultra can raise iterations without converting finalization into a main-thread stall.
Hardware Impact: Prevents accidental fixed-tick wait amplification on thermally constrained CPUs.

Problem: Camera AUP for the cable mock was reconstructed directly from presentation `Vector3`.
Solution: Derive camera AUP from `GlobalRegistry.Player.PlayerMovement.CurrentAup` plus local camera offset when available, with `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3` as the fallback.
Rejected Alternatives: Raw `AbsoluteUniversePosition.FromRuntimePosition(cameraPosition)` in the hot schedule path; it ignores the movement owner's current AUP fact.
Scalability potential: All tiers get the same AUP authority; visual spline density remains the scaling variable.
Hardware Impact: No measurable cost increase; prevents large-world jitter that would otherwise burn QA time and produce solver faults.

Problem: Legacy tether AUP presentation jobs still used `FloatMode.Fast`, and force flush used steady-state `ForceMode.Force`.
Solution: Switch legacy tether spline/memcpy Burst jobs to deterministic mode and route flushed tether packets as `ForceMode.Acceleration`.
Rejected Alternatives: Treating legacy tether code as out-of-scope; it shares the cable domain and would keep rollback and physics-apply rot visible.
Scalability potential: Deterministic presentation keeps rollback/debug hashes stable; acceleration routing scales better across different mass bodies.
Hardware Impact: Trades negligible presentation math speed for deterministic replay and avoids mass-dependent force instability.

Problem: DTO layout validation used reflection in a runtime-callable path.
Solution: Replace reflection offset lookup with `Marshal.OffsetOf<T>` using the existing explicit layout metadata.
Rejected Alternatives: Leaving reflection cold-path only or deleting offset validation; the first is AOT-risky, the second weakens ARM64 layout proof.
Scalability potential: No hot-path impact; keeps layout verification deterministic across low/high hardware.
Hardware Impact: No frame impact; lower runtime/AOT fragility during boot validation.

Problem: `TetherManager` kept Vault-resolved `NativeArray` telemetry aliases in private fields.
Solution: Evict the aliases; retain only `VaultBufferHandle` fields and resolve `NativeArray` views locally inside write/dump methods.
Rejected Alternatives: Treating aliases as harmless because the Vault owns allocation; the mandate is scan-visible and should not depend on reader interpretation.
Scalability potential: All tiers keep the same Vault-owned ring without persistent managed-owner views.
Hardware Impact: No direct microsecond gain claimed; reduces stale-view risk after Vault generation changes.

Problem: CaveBioRootsGenerator routed spline visuals through static `ConnectionSplineBatchRenderer` wrappers every root submit/remove.
Solution: Cache `IConnectionSplineBatchRendererService` through `GlobalRegistry.TryGet` and call the service contract directly.
Rejected Alternatives: Keep static wrappers or add a new renderer dependency; static wrappers hide service lookup in a hot path, and a new dependency would expand the compile surface.
Scalability potential: Low tiers still submit fewer descriptors by root count/intensity; high tiers keep the same route with richer shader presentation.
Hardware Impact: Removes repeated static wrapper/service resolution overhead from dense cave root ticks.

Problem: Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the SHINOBU_132 XML block.
Solution: Treat the explicit user assignment plus existing SHINOBU_132 persisted status/rationale/log records as the active authority, and document the batch mismatch instead of reading neighboring SHINOBU_200+ tasks into this domain.
Rejected Alternatives: Infer tasks from adjacent batch agents or edit broad batch files; both would contaminate domain scope and break the strict parsing rule.
Scalability potential: No runtime impact; preserves coordination safety while many agents work in parallel.
Hardware Impact: Avoids compile-wall churn from unrelated batch work.

Problem: `TetherManager.ResolveShinobu132CameraContext` still depended on `GlobalRegistry.Player` in the fixed-tick route.
Solution: Move player service lookup to `RefreshColdDependencyCache`, cache camera and player movement references, and use those cached owners when deriving camera AUP.
Rejected Alternatives: Poll `GlobalRegistry.Player` during fixed-tick math or derive camera AUP only from `Vector3`; the first violates hot global lookup discipline, the second loses owner-local AUP authority.
Scalability potential: Low/middle/high/ultra keep identical AUP ownership while spline density and solver iterations remain quality-scaled.
Hardware Impact: Removes a hot global registry lookup and prevents large-world jitter faults; expected cost saving is low microseconds, correctness impact is larger.

Problem: `CablePhysicsDebugGizmo132` used `GlobalDataVault.TryGetLatestCreated`, creating a latest-created singleton dependency instead of the active registry route.
Solution: Resolve `IDataVault` through `GlobalRegistry.DataVault` in the editor/debug visualization path.
Rejected Alternatives: Keep latest-created lookup as harmless debug code; it can inspect the wrong Vault after hot-swap or tests and weakens authority proof.
Scalability potential: No gameplay cost; debug visualization follows the same owner route as runtime.
Hardware Impact: No steady-frame saving claimed.

Problem: Legacy `TetherInstance.ApplyReducedMassReactionForce` queued a player reaction as `ForceMode.Force`.
Solution: Convert the reaction to acceleration by dividing by finite player mass, clamp with `_maxCableAcceleration`, finite-guard the vector, and queue `ForceMode.Acceleration`.
Rejected Alternatives: Keep mass-dependent force or bypass player reaction entirely; the first destabilizes different body masses, the second changes tow-cable gameplay behavior.
Scalability potential: Cheap devices get predictable, bounded acceleration; high/ultra can spend more solver/spline budget without mass-dependent force spikes.
Hardware Impact: No measurable CPU saving; reduces physics solver instability and QA rollback variance.

Problem: `TetherVisualGpuSplineCopyJob` still used `FloatMode.Fast` in a rollback-relevant tether presentation buffer path.
Solution: Switch the job to `FloatMode.Deterministic` while keeping the same `[NoAlias]` NativeArray fields.
Rejected Alternatives: Treat presentation data as outside determinism; GPU spline point tension and positions feed debug/blackbox surfaces and must replay consistently.
Scalability potential: Presentation determinism is invariant across tiers; quality weight controls vertex count, not math mode.
Hardware Impact: Negligible ALU cost accepted to prevent cross-platform drift.

Problem: `TetherManager.OnOriginShift` could pull a mutable `ref NativeArray<float3>` view out of `TetherInstance`.
Solution: Delete the external ref-return API and internalize fallback visual staging rebase inside `TetherInstance.RebaseVisualStagingRuntime`.
Rejected Alternatives: Keep the ref-return because it avoided a tiny method call; it leaked a Vault-backed mutable view across ownership boundaries and made future Vault-generation safety harder.
Scalability potential: Low/middle/high/ultra behavior is unchanged; origin-shift work remains linear in active visual point count only when an origin shift occurs.
Hardware Impact: No frame-time saving claimed. Authority surface is smaller and origin-shift mutation remains owner-local.

Problem: Legacy `TetherInstance.RunVerletSolver` executed Burst job structs synchronously via `.Run()` and `.Execute()`.
Solution: Schedule integration, constraint solve, and telemetry as a dependency chain, store the pending `JobHandle`, finalize through `DispatcherJobFence` in the next fixed solve, and prevent visual reads while the solve is pending. Teardown/origin-shift forces the fence only to avoid mutating or releasing live job buffers.
Rejected Alternatives: Keep `.Run()` for same-frame peak tension or call `Complete()` immediately after `Schedule`; both preserve the main-thread stall. Rewriting the entire monolith into method-local Vault views was rejected for this pass because it would touch too much gameplay behavior at once.
Scalability potential: Low tiers avoid synchronous solver cost on the fixed-tick caller; high/ultra can spend the saved fixed-tick headroom on more cable nodes/spline density in the active SHINOBU_132 path.
Hardware Impact: Expected gain is the removal of direct fixed-thread solver execution for the legacy tether path. Exact microseconds require Unity profiler proof; static proof only shows the `.Run()`/`.Execute()` call sites are gone.

Problem: `TetherVisualGpuSplineCopyJob` was not scheduled; it was called in a manual loop through `Execute`.
Solution: Remove the pseudo-job and replace it with an explicit bounded copy helper until this legacy visual upload can be migrated to the same ticketed LockBufferForWrite job path as SHINOBU_132.
Rejected Alternatives: Pretend a direct `Execute(i)` loop was a Burst job or add an immediate schedule+complete pair. Both would be false architecture.
Scalability potential: Visual copy remains bounded by point count; active SHINOBU_132 spline renderer remains the scalable route.
Hardware Impact: No saving claimed; this removes fake job architecture and keeps the code honest.
