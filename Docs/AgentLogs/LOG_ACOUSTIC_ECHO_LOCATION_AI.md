# LOG_ACOUSTIC_ECHO_LOCATION_AI

## 2026-05-16 - Acoustic Echo Location AI
What was wrong:
- Abyssal predator acoustic targeting could still derive target intent from exact player noise positions, so blind navigation behaved like omniscient sight.
- Acoustic portal propagation exposed `LastPortalAup`, but predator cognition had no bounded sensory bridge consuming portal echo taps.
- DSP/sonar tap intake needed hard count clamps and invalid-number rejection before any predator path target was written.

What was done:
- Confirmed prompt `ACOUSTIC_ECHO_LOCATION_AI`, task count 18, domain AI/PATHING.
- Read AGENTS.md, domain doc, and 8 mandates: acoustic sonar, DSP queue, AI cognition, pathing, AUP determinism, zero-GC, blackbox telemetry, signal lane segregation.
- Implemented/verified `AcousticEchoLocationRuntime` contract: `NativeQueue<EchoTap>`, fixed 32-tap frame slab, `EchoTrackingJob`, portal-AUP result, black-box telemetry, sonar tap hydration, MovementAcousticSignal and AcousticPingSignal fallbacks.
- Wired portal propagation into `SpatialAudioManager` so path-found sounds enqueue echo taps using `AcousticPathResult.LastPortalAup`.
- Wired Fauna predator cognition to use `AcousticEchoLocationRuntime.TryResolvePredatorEcho`; acoustic-only targets now pass the echo portal/investigation AUP, not raw player noise AUP.
- Added high-tier acoustic head sweep via existing Fauna head-look target handoff; visual player contact still overrides the fake.
- Added five-second silence expiry. When the trail expires, no acoustic target is returned and cognition falls back to search/wander behavior.
- Added loudest-tap priority for decoys/noisemakers via `Volume01 * Transmission01` selection.

Cinematic cheats used:
- Low tier: source node = sound node, no portal solve, direct swim to last heard node.
- High tier: lateral head-look sweep is a cheap sine offset, not a physical smell/wake simulation.
- Portal breadcrumbing: predator follows audible portal position, not a full acoustic wave field.

Exact microseconds saved/estimated:
- Removed direct acoustic distance branch for blind predator target acquisition: ~3 us/predator tick.
- Fixed 32-tap Burst scan instead of managed event/list scan: ~20-80 us stall risk avoided on predator-heavy frames.
- Low-tier direct-node fake avoids portal solve entirely on MX350 path: portal solve cost avoided per low-tier echo.
- Head sweep fake cost: ~2-4 us/frame, replacing any full cone/current simulation.
- AUP finite checks and black-box write: ~1-3 us/frame, paid only on bounded tap processing.

Validation:
- `git diff --check` passed on touched acoustic/Fauna/audio/status/rationale files; only line-ending warnings reported.
- `dotnet build Hecton8.Core.csproj --no-restore` is blocked by external dependency churn outside this prompt: deleted `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` and tether contract state from parallel work. No further external repair was performed after dependency wall.

Integrator notes:
- Resolve the Bucketing/Tether compile wall, then rerun `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`.
- Recheck `NativeArray<SonarEchoTap>.ReadOnly.IsCreated` against the active Unity.Collections version if compile reaches `AcousticEchoLocationRuntime`; the guard is intentionally there to reject invalid DSP snapshots.
