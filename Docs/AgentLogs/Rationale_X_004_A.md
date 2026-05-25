# Rationale X_004_A

Problem: Simulation/presentation leak audit must detect hidden helper chains without editing runtime code.
Solution: Static grep first, manual route inspection second. Use ARCH_EXECUTION_PHASES as owner boundary: SIMULATION/POST_SIMULATION cannot write Material/Shader/Renderer/Light/Particle/Audio/TMP/GPU upload/ObjectPool/SetActive sinks; VISUAL_SYNC owns those effects.
Rejected Alternatives: Running Unity or dotnet build adds no proof for read-only route ownership and risks active-agent contention. Blind sink reporting was rejected because presentation owners are allowed to write sinks in VISUAL_SYNC.
Scalability potential: Low/Middle/High/Ultra remain unaffected by this read-only audit. Findings will prefer stable snapshots or typed signals into VISUAL_SYNC so low silicon avoids scene writes in simulation while high/ultra can spend extra work on presentation consumers.
Hardware Impact: Audit itself has no runtime impact. Fix pattern impact is expected to remove hot-path scene writes from i3/MX350 simulation phases and move cost to bounded visual cadence.

Problem: Confirm whether suspicious hull/fluid/ecology files had direct helper-chain leaks from Tick/FixedTick/PostFixedTick/PreSimulationTick/ScheduleSimulation/Execute into presentation sinks.
Solution: Treat deferred dirty flags as non-findings unless the listed entrypoint itself calls a sink helper. Verified examples: HabitatFluidIncursionDirector FixedTick/PostFixedTick writes simulation and dirty flags, Render uploads; FloraAmbientSwayRuntime PreSimulationTick writes DataVault, VisualSyncTick uploads; HectonFluidEngine FixedTick queues water/flow/GPU work, LateFrameTick flushes shader/GPU/particle work.
Rejected Alternatives: Reporting LateFrameTick/Render/VisualSyncTick presentation sinks as violations was rejected because the mission asked for routes from the listed simulation entrypoints and VISUAL_SYNC is the expected presentation owner. Reporting property-id declarations was rejected because Shader.PropertyToID is cold metadata, not a sink.
Scalability potential: Low/Middle/High/Ultra route shape remains correct when presentation work is deferred. Low devices can skip dirty uploads; high/ultra can spend extra shader/GPU work in visual phases without contaminating simulation.
Hardware Impact: No runtime source change. No measured microsecond gain. Static audit reduces risk of i3/MX350 simulation spikes by identifying no required hot-path fixes in the inspected scope.
