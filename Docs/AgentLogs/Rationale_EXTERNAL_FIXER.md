# Rationale_EXTERNAL_FIXER
Date: 2026-05-23
Status: VERIFIED - FIRST HOT-PATH REGISTRY TRANCHE

## Decision 1

Problem: User requested broad autonomous repair across a dirty Unity project with many concurrent-agent edits.
Solution: Start with evidence-backed defects that can be fixed locally without changing public contracts or crossing ownership boundaries.
Rejected Alternatives: Global refactor sweep was rejected because it would collide with active agents and violate owner boundaries. Pure audit report was rejected because user explicitly requested fixes, not empty findings.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected until a concrete runtime path is patched; each patch must preserve continuous quality-weight doctrine if it touches runtime fidelity.
Hardware Impact: 0 us runtime gain until code changes land and are measured; no profiler proof claimed.

## Decision 2

Problem: Broad static scans can produce false positives from comments, docs, generated files, and editor-only paths.
Solution: Treat text hits as candidate defects only, inspect source context before patching, and label evidence as STATIC_SOURCE unless compile/runtime artifacts are produced.
Rejected Alternatives: Counting `rg` hits as proof was rejected by QA evidence law. Running Unity/dotnet blindly was rejected by CPU/compiler guard.
Scalability potential: Prevents noisy reports from hiding actual hot-path or compile defects.
Hardware Impact: 0 us runtime gain; reduces process risk.

## Decision 3

Problem: ScavengePopulator.ProcessSpawnQueue and DespawnChunk pulled ObjectPoolManager and WorldStateManager from GlobalRegistry during the slow-tick spawn/despawn path.
Solution: Cache ObjectPoolManager and WorldStateManager in lifecycle wiring and refresh them through IGlobalRegistryHotSwapListener for ObjectPool and WorldStateRuntime slots.
Rejected Alternatives: Per-spawn registry lookup was rejected as hot polling. Hard dependency injection constructor was rejected because MonoBehaviour lifecycle and active multi-agent scene wiring would make it wider than the defect.
Scalability potential: Low tier avoids repeated registry reads during resource streaming; Middle/High/Ultra keep the same spawn truth and can spend saved budget on visual density without changing DTO/save identity.
Hardware Impact: STATIC estimate only: removes 2 registry reads per active spawn-queue slow tick and 1 registry read per chunk despawn. No profiler microsecond claim.

## Decision 4

Problem: VoxelDeltaProcessor.EmitCaveInDustDecal resolved AbyssalFluidDecals from GlobalRegistry inside the carve commit side-effect path.
Solution: Cache AbyssalFluidDecalManager on enable, refresh through IGlobalRegistryHotSwapListener, and use the cached pointer during dust emission.
Rejected Alternatives: Leaving a static resolver was rejected because carve commit is a runtime path. Replacing the dust system with a new signal was rejected as a cross-domain route change outside this fix tranche.
Scalability potential: Low tier keeps cave-in dust optional and cheap; Middle/High/Ultra retain the same decal hook for richer cave-in visuals when the service exists.
Hardware Impact: STATIC estimate only: removes 1 registry read per cave-in dust emission. No profiler microsecond claim.

## Decision 5

Problem: Atlas6DirectiveSystem and AtlasSignalDecoder resolved AtlasSignal and FirstHour through GlobalRegistry in slow-tick/pulse/narrative decision paths.
Solution: Cache AtlasSignalSystem and FirstHourDirector in lifecycle wiring, refresh through IGlobalRegistryHotSwapListener, and read cached references in runtime decisions.
Rejected Alternatives: Replacing AtlasSignalSystem reads with a new DTO route was rejected because existing logic needs CurrentRevealStage and CurrentStrength and the current first-party service already owns those facts.
Scalability potential: Low tier reduces registry traffic on narrative polling; Middle/High/Ultra preserve the same signal truth and decode gates.
Hardware Impact: STATIC estimate only: removes 1 registry read per Atlas6 slow tick and 1 registry read per AtlasSignalDecoder slow tick/pulse sync. No profiler microsecond claim.

## Decision 6

Problem: The repository is heavily dirty, including pre-existing edits in VoxelDeltaProcessor and many unrelated files.
Solution: Verify only touched compile surface with Hecton8.Core.csproj and avoid reverting or staging unrelated changes.
Rejected Alternatives: Full repo cleanup or broad commit staging was rejected because it would capture other agents' changes and violate shared-worktree ownership.
Scalability potential: Keeps the repair tranche local and mergeable while preserving other domain work in progress.
Hardware Impact: 0 us runtime gain; process-risk reduction only.
