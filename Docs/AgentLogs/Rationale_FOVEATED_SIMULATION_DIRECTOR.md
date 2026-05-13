# Rationale_FOVEATED_SIMULATION_DIRECTOR

Status: PENDING VERIFICATION

## Decision 0: Fresh State Creation

Problem: Batch protocol requires durable status/rationale files before implementation. Existing files were absent.
Solution: Created explicit status and rationale files under `Docs/Tasks` and `Docs/AgentLogs`.
Rejected Alternatives: Chat-only progress was rejected because the CTO protocol reads disk logs, not chat history.
Scalability potential: No runtime impact. Low/Middle/High/Ultra unchanged.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 1: Registry Service Instead of AI Singleton

Problem: The foveated director must be shared by fauna and boids without creating an `AiManager.Instance` dependency or forcing scene-order coupling.
Solution: Added `IFoveatedSimulationDirector` to `Hecton8.Core.Contracts` and registered the concrete director through `GlobalRegistryServiceSlot.FoveatedSimulationDirector`.
Rejected Alternatives: A scene singleton and `FindObjectOfType` were rejected because they create hidden boot dependencies and cold lookup cost during agent wake-up.
Scalability potential: Low uses the same contract with tighter thresholds; Middle/High/Ultra can swap richer director logic behind the same interface without touching fauna/boids.
Hardware Impact: Estimated 18 us saved on i3/MX350 during 100 cold AI service binds by avoiding scene hierarchy search.

## Decision 2: SignalBus Camera Feed

Problem: Tier scoring needs camera position/frustum data, but direct camera polling would make AI depend on render objects and could drift from the culling frame.
Solution: Added fixed-size `CameraPositionSignal` and `CameraFrustumSignal` payloads and consumed their latest snapshots in the director.
Rejected Alternatives: `Camera.main`, direct transform references, and per-brain distance checks were rejected because they duplicate work and break deterministic ownership.
Scalability potential: Low/Middle can publish cheap forward vectors; High/Ultra can publish stricter frustum metadata without changing AI consumers.
Hardware Impact: Estimated 35 us saved per 10Hz pass on MX350 by consuming cached signal state instead of object lookups.

## Decision 3: Persistent Native Tier Buffers

Problem: 5000 boids and 100 predators cannot allocate or traverse managed state for foveated classification.
Solution: Added persistent `NativeArray<float3>` AUP storage and `NativeArray<byte>` tier storage, with a Burst job producing tier, cadence, distance, and frozen counts.
Rejected Alternatives: Managed arrays plus LINQ/filter passes were rejected as GC-prone and cache-hostile.
Scalability potential: Low freezes at 150m, Middle at 300m, High/Ultra can increase distance budgets or add visual overkill while the same byte tier output drives consumers.
Hardware Impact: Estimated 120 us saved per 5000-entity 10Hz pass on i3/MX350 versus managed object traversal.

## Decision 4: Centralized Distance Authority

Problem: Fauna scripts had local player-distance LOD/sleep decisions that could disagree with the foveated tier table.
Solution: Rewired fauna sleep/slow paths to read `FoveatedSimulationTier` state supplied by the director and left raw player distance only for gameplay utility inputs.
Rejected Alternatives: Keeping per-script `DistanceToPlayer` thresholds was rejected because each script would silently fork the LOD policy.
Scalability potential: Low gets brutal freeze bands; High/Ultra can spend the recovered CPU on richer nearby cognition and visual threat recycling.
Hardware Impact: Estimated 45 us saved per frozen predator frame by stopping steering/current branches before math-heavy utility paths.
