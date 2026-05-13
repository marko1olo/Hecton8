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
