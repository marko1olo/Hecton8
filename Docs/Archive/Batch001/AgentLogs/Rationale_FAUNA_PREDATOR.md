# Rationale_FAUNA_PREDATOR

Status: VERIFIED MASTER GRADE (FAUNA DOMAIN; Hecton8.Core compiles with external warnings)

## Decision 0 - Domain Boundary

Problem: Predator steering, vision, utility, and pack target sharing must stay inside the fauna AI domain while parallel agents mutate other systems.
Solution: Primary edit target is `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`; cross-domain interaction must remain through existing interfaces, Native containers, GlobalRegistry, or signal queues already present in the project.
Rejected Alternatives: Direct references to player, voxel, combat, or rendering concrete classes would create cross-domain compile fragility during the batch.
Scalability potential: Low uses dominant-axis/rsqrt approximations and staggered SlowTick; Middle uses polynomial scoring and fixed lead; High uses S-curve steering and richer pack sync; Ultra can spend saved cycles on visual overkill without changing the AI contract.
Hardware Impact: Estimated gain on i3/MX350 is pending measurement; expected savings come from replacing ETA, pow, sqrt, and non-staggered sensor work with fixed constants and squared math.

## Decision 1 - Apex S-Curve vs Swarm Snap

Problem: Apex predators shared the same dominant-axis steering resolver as swarm predators, producing robotic 90-degree direction changes.
Solution: Added an apex-only S-curve steering path in `PredatorCognitionDomain` using `CinematicMath.FastNlerp` from current forward to a lateral-biased target direction. Non-apex predators still use dominant-axis steering or rsqrt normalization only where explicitly allowed.
Rejected Alternatives: Full spline/path solver and NavMesh steering were rejected because the prompt forbids NavMesh and the AI_DYNAMIC_NAVGRID_SDF mandate requires tactical vector math over main-thread path rebuilds.
Scalability potential: Low/Middle keep snap steering for cheap predators; High/Ultra apex predators spend extra quaternion nlerp work on organic mass and flanking motion.
Hardware Impact: Estimated cost is a few dozen scalar ops only for apex stalking/attacking slots; saved cost remains with Tier 1/2 enemies because their snap path is unchanged. Exact microseconds: PENDING VERIFICATION.

## Decision 2 - Utility Score Squaring

Problem: Predator utility state scores used polynomial drive curves but did not explicitly square final action scores.
Solution: Squared the final prowling/stalking/attacking/fleeing action scores before argmax using direct `score * score` math.
Rejected Alternatives: `Mathf.Pow`, `math.pow`, animation-curve assets, and managed lookup curves were rejected due Burst hot-path and zero-GC requirements.
Scalability potential: Low tier pays one multiply per action score; High/Ultra gets stronger readable action commitment without extra data dependencies.
Hardware Impact: Four multiplies per evaluated predator SlowTick; branch and allocation impact unchanged. Exact microseconds: PENDING VERIFICATION.

## Decision 3 - Predator Bite Impact Signal

Problem: Bite attacks queued combat damage and physical impulse, but the global impact signal corridor did not receive a `Mass * Velocity` predator impact packet.
Solution: Published `ImpactSignal` from `DispatchPredatorBiteImpulseToPlayer` after calculating impulse from predator mass and velocity.
Rejected Alternatives: Direct calls into audio or trauma systems were rejected because batch execution requires decoupling through existing queues.
Scalability potential: Low drains fewer soundscape impact signals; High/Ultra can render heavier impact audio/VFX from the same signal without changing fauna code.
Hardware Impact: One NativeQueue enqueue per successful player bite; no per-frame cost. Exact microseconds: PENDING VERIFICATION.

## Decision 4 - Acoustic, Wall, Pack, and SDF Paths

Problem: Predator perception needed through-wall acoustic acquisition, wall escape steering, pack target sharing, and ambush pull without adding heavy physics or managed dependencies.
Solution: Kept the existing DOD lanes: acoustic sight now uses explicit `math.distancesq` against the 2500 sq-meter threshold; wall escape uses the threat voxel/SDF probe, dominant X/Z normal, and `cross(normal, up)`; species pack sync uses `NativeParallelHashMap<int, float3>`; ambushers read the threat voxel SDF gradient and push toward local crevice maxima.
Rejected Alternatives: Per-frame `Physics.Raycast`, NavMesh carving, managed pack registries, and neighbor-scanned SDF gradients were rejected because they would violate zero-GC, tactical vector math, and 0.1 ms frame budget rules.
Scalability potential: Low uses acoustic snap and dominant-axis SDF fakes; Middle keeps pack sync in native hash maps; High/Ultra can spend visual budget on downstream animation/audio while the AI math stays predictable.
Hardware Impact: No new hot-path allocations. Existing director LOS remains capped to one scheduled `RaycastCommand` per 0.5 seconds, stricter than the per-predator budget. Exact microseconds: PENDING VERIFICATION.

## Decision 5 - Impact, Surge, Camouflage, Reciprocals, and MathGuard

Problem: Predator attack, locomotion, and presentation needed stronger cinematic output without adding per-frame garbage or unsafe math.
Solution: Predator bites now publish `ImpactSignal` from `Mass * Velocity`; forward speed surge uses the existing deterministic triangle pulse; camouflage remains on cached runtime material shader properties with depth/ambient parameters; steering divisions are expressed through precomputed reciprocals; final steering exits run through `MathGuard.IsFinite` and dominant-axis fallback.
Rejected Alternatives: Direct VFX/audio calls, random animation curves, shader material property blocks on standard geometry, runtime division-heavy steering weights, and unchecked steering vectors were rejected. MPB was specifically rejected because project AGENTS forbids MPB on standard geometry, so runtime material clones are the compliant shader state lane.
Scalability potential: Low gets cheap deterministic surge and finite fallback; Middle gets depth/ambient camouflage with cached shader IDs; High/Ultra can overdrive impact/audio/VFX consumers from the same `ImpactSignal` without more fauna AI work.
Hardware Impact: Attack signal cost is one queue publish only when a bite connects; surge is a triangle-wave scalar multiply; camouflage writes occur on cold material setup, not per-frame hot AI. Exact microseconds: PENDING VERIFICATION.

## Decision 6 - Wander Hash, Stagger, State SOA, and Combat API

Problem: Final predator tasks required random-looking patrols, staggered SlowTick, SOA state, no debug string churn, and compatibility with the consolidated combat damage API.
Solution: Replaced wander target selection with an integer LCG-style hash seeded by `WanderSequence` and AUP-centered coordinates; preserved existing SlowTick stagger via `_nextEvaluationTimes` and `(slot & 31)` offset; kept predator state in `NativeArray<byte>` with `math.select` transitions; verified no `string.Format` in fauna and no fixed-string interpolation hits in fauna; verified bite damage uses `CombatDamageSignal`, `CombatDamageSignalDetail`, `PackSignalMeta`, and `TryQueueDamage`.
Rejected Alternatives: `math.sin/cos` patrol noise, global synchronized utility evaluation, enum arrays wider than byte, debug string generation in AI paths, and legacy direct `HectonPlayerHealth.ApplyDamage` calls were rejected.
Scalability potential: Low gets deterministic hash wandering and staggered utility ticks; Middle/High use the same SOA buffers with richer steering; Ultra can raise visual consumers while state evaluation stays stable.
Hardware Impact: Wander refresh now uses fixed integer multiplies/shifts instead of float frac constants; SlowTick staggering avoids CPU spikes on i3/MX350. Exact microseconds: PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: The initial apex S-curve was species-tier gated but not explicitly hardware-tier gated in the native cognition lane.
Solution: Added `CognitionInputFlags.HighTierSmoothSteering`. `FaunaBrain.Compatibility` reads `GlobalRegistry.ScalabilityTier` and `GlobalRegistry.TargetMathPrecision` on the managed side, then passes a Burst-safe flag into `PredatorCognitionDomain`. The Burst job now uses `CinematicMath.FastNlerp` S-curve steering only for apex predators on High/Ultra tiers with high math precision; Low/Mx350/Mid fall back to dominant-axis snap.
Rejected Alternatives: Reading `GlobalRegistry` inside the Burst job was rejected as a managed/static dependency leak. Keeping S-curve for all apex predators was rejected by the Omega scalability requirement. Adding new NavMesh/pathing logic was rejected by the tactical vector math mandate.
Scalability potential: Low/Mx350 = dominant-axis snap, bitwise wander hash, acoustic through-wall fake, SDF fake, staggered SlowTick. Mid = same cheap cognition with richer downstream presentation. High/Ultra = apex S-curve and overkill impact/audio/VFX consumers without changing AI contracts.
Hardware Impact: Office i3/MX350 avoids quaternion nlerp and lateral S-curve math for apex predators; High/Ultra spends those saved cycles only when the platform budget allows it. Exact microseconds remain static estimates pending Unity profiler capture.

Honest calculations replaced with cinematic cheats:
- ETA intercept stayed a fixed `PlayerPosition + PlayerVelocity * 0.65f` lead.
- Vision uses squared dot-product comparisons, not angle/acos.
- Acoustic line of sight ignores walls when noise and squared range pass.
- Vortex steering uses voxel/SDF probe plus dominant X/Z normal, not synchronous raycast.
- Ambush pull uses one-cell SDF/hash gradient, not neighbor gradient scan.
- Tail surge and apex flanking use triangle-wave signals, not sine/animation curves.
- Wander patrol uses integer LCG-style hash, not sin/cos/random.

Final Git Diff:
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`: apex S-curve, action score squaring, explicit acoustic `math.distancesq`, LCG wander hash, high-tier smooth steering flag consumption, final steering finite guards.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs`: managed `GlobalRegistry.ScalabilityTier`/precision gate converted into native `HighTierSmoothSteering` flag.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: predator bite impact signal publish to `GlobalSignals`; existing concurrent hit-flash/camouflage material changes were preserved, not reverted.
- Diff stat at Omega: `FaunaBrain.Compatibility.cs` 15 insertions; `FaunaBrain.cs` 92 changed lines in working diff; `PredatorCognitionDomain.cs` 135 changed lines in working diff. Worktree contains concurrent non-owned edits, so the stat includes pre-existing/user changes in touched files.

Build Health:
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false`: passed, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false /nr:false`: now passes, 0 errors, 28 external warnings. The warnings are outside fauna in Unity URP package cache, GPUInstancer, and Crest shared/editor package code.

## Decision 7 - Continuation Build Reconciliation

Problem: The Omega build state changed after another dependency pass. The previous blocker in `VoxelDeltaProcessor.cs` disappeared, but `Hecton8.Core.csproj` still emits warnings outside the fauna domain.
Solution: Re-ran both the full core project and the predator domain project. Updated status and log to distinguish predator-owned verification from package/external warning debt.
Rejected Alternatives: Editing URP package cache, GPUInstancer, or Crest package code was rejected because it is outside the FAUNA_PREDATOR domain and would be dependency churn unrelated to predator cognition.
Scalability potential: No runtime behavior change; this is build-state documentation. Predator low/high Math LOD remains as documented in Omega polish.
Hardware Impact: 0 runtime impact. Documentation prevents integrator from chasing stale voxel errors and points remaining warning debt at the owning packages.
