# Rationale_QUALITY_INTEGRATOR

Problem: User asked to continue honest work after a broad integration/cost-estimation pass.
Solution: Continue as a meta-quality integrator: rebuild current state from disk, label evidence classes, run focused compile validation, and only patch objective defects.
Rejected Alternatives: Do not restore deleted agent logs via git, do not claim Unity Console status without MCP, do not rewrite unrelated systems, do not treat static search as runtime proof.
Scalability potential: Low/Middle/High/Ultra runtime tiers are protected by avoiding speculative bloat. Runtime scalability changes require profiler or gameplay evidence.
Hardware Impact: 0 us/frame until runtime code changes are made and measured.

Problem: Required status/rationale files were absent after Docs cleanup; `Docs/Tasks/CURRENT_BATCH.md` is empty.
Solution: Recreate only `Status_QUALITY_INTEGRATOR.md`, `Rationale_QUALITY_INTEGRATOR.md`, and `LOG_QUALITY_INTEGRATOR.md` as current-session evidence trail. Treat missing batch prompt as a hygiene caveat, not a reason to revert other agents' deletions.
Rejected Alternatives: Restoring all deleted Docs/Tasks and Docs/AgentLogs would overwrite concurrent cleanup. Proceeding without local state violates anti-amnesia protocol.
Scalability potential: Documentation-only. No Low/Middle/High/Ultra runtime behavior changed.
Hardware Impact: 0 us/frame.

Problem: `NativeArenaArray_WritesInsideIJobParallelFor` failed because Unity requires `m_Length`, `m_MinIndex`, and `m_MaxIndex` as three consecutive fields for `[NativeContainerSupportsMinMaxWriteRestriction]`; the container used `_length` and placed safety before min/max.
Solution: Rename `_length` to `m_Length`, place `m_Length/m_MinIndex/m_MaxIndex` consecutively, and keep `m_Safety` after those fields. This preserves the arena-backed zero-copy container and Unity job safety slicing.
Rejected Alternatives: Removing `[NativeContainerSupportsMinMaxWriteRestriction]` would reduce safety in parallel jobs. Rewriting allocator ownership was unrelated. Boxing through managed arrays would violate Zero-GC policy.
Scalability potential: Low/Middle/High/Ultra all keep the same arena allocation path and job-safe index restriction; no extra memory path added.
Hardware Impact: 0 us/frame claimed. The fix removes a test/runtime exception path rather than measured per-frame cost.

Problem: `ObserverRelativeCelestialBodyFixedDirectionFollowsObserverOneToOneWithConstantOffset` failed in EditMode because `ResolveObserverWorldPosition` preferred `SceneView.lastActiveSceneView.camera` before an explicitly assigned `observerTransform`.
Solution: Return `observerTransform.position` first when assigned; only fall back to SceneView/player camera when no explicit observer exists.
Rejected Alternatives: Changing the test to follow SceneView would make authoring and tests non-deterministic. Removing SceneView fallback would reduce editor preview ergonomics.
Scalability potential: Low/Middle/High/Ultra behavior remains deterministic; no new allocations or service dependencies.
Hardware Impact: 0 us/frame claimed. Branch order only.

Problem: `ResolveSkyColorsReadsScriptProfilesAsSourceOfTruth` failed after the documented surface readability-floor patch: expected raw profile zenith red 0.120 while runtime clamps to `SurfaceReadableSkyZenithFloor.r` 0.160.
Solution: Update the test name and assertions to the current contract: script profile feeds the pipeline, then horizon compression and readability floors are applied.
Rejected Alternatives: Deleting readability floors would violate the UI_DIEGETIC_INPUT DOD recorded in Docs and would regress surface visibility. Ignoring the test would leave CI red.
Scalability potential: Low devices keep the cheap scalar clamp; high-end devices can spend saved render budget elsewhere. No new shader pass or texture allocation.
Hardware Impact: 0 us/frame claimed; test-only update, runtime already had the clamp.

Problem: `Hecton8.Core.rsp` compile was blocked by cross-assembly contract drift and duplicate lifecycle additions while other agents were editing the same files.
Solution: Keep edits at the contract boundary: Core references `Hecton8.Vehicles.Physics.Contracts`, `HectonPlayerMovement` exposes the required `IPlayerMovementContracts` and registry/listener adapters once, and duplicate method bodies were collapsed when source evidence showed identical signatures.
Rejected Alternatives: Broad player-controller refactor, deleting the new contracts, or reverting concurrent agent work. The compile errors were objective; behavior rewrites were not justified.
Scalability potential: Low/Middle/High/Ultra tiers are unaffected at runtime except that existing scalability cache invalidation now has a valid listener path. No new per-frame allocation path was introduced.
Hardware Impact: 0 us/frame claimed; compile/link fix only.

Problem: Simulation bucketing contracts pulled Core implementation types into a lower-level contract assembly and `ModuloSimulationBucketer` attempted to use `NativeMemorySentinel` across an assembly boundary.
Solution: Keep `ISimulationBucketer` and `IBucketedSlowTickable` as pure contracts, retain persistent `H8Memory` ownership in bucketing, and remove the sentinel dependency that would create an assembly cycle.
Rejected Alternatives: Adding a Core reference to the contracts assembly or moving sentinel internals into the bucketing package. Both increase coupling and make future parallel-agent merges worse.
Scalability potential: Low devices keep the same bucketed slow-tick cadence; High/Ultra can still raise bucket density through the existing bucketer without a new dependency.
Hardware Impact: 0 us/frame claimed; assembly-boundary repair only.

Problem: World paging methods were added to `IAsyncPersistenceService`, while `SaveManager` and `H8BinaryWorldPager` had duplicate/partial implementations from overlapping edits.
Solution: Make `SaveManager` implement one canonical pager bridge, initialize `out H8WorldPageReadTicket` before all returns, collapse duplicate pager methods/fields, and make only pointer-heavy `H8BinaryWorldPager` methods unsafe so `RunWorkerAsync` can await legally.
Rejected Alternatives: Removing world-pager API from the interface, leaving class-wide unsafe and replacing `Awaitable` with a new thread abstraction, or adding a second persistence service. Those would either break callers or create a parallel save path.
Scalability potential: Low tier keeps async chunk paging off the main thread; High/Ultra can use the same queue-backed path for larger world churn without additional service indirection.
Hardware Impact: 0 us/frame claimed. Expected benefit is preserving the existing off-thread pager, but no profiler measurement was run.

Problem: Unity PlayMode verification crashed/disconnected the Editor after the compile was clean.
Solution: Record the failure as `[BLOCKED BY UNITY EDITOR CRASH]`: MCP lost session, refresh timed out, and process evidence showed Unity Bug Reporter attached to `Crash_2026-05-13_125415739`. Do not report PlayMode pass/fail.
Rejected Alternatives: Killing Unity/BugReporter processes, treating `Hecton8.PlayModeTests.rsp` compile success as runtime pass, or hiding the crash behind "pending".
Scalability potential: No runtime scalability conclusion can be drawn from a crashed PlayMode runner.
Hardware Impact: 0 us/frame claimed.
