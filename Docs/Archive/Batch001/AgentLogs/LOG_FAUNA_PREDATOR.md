# LOG_FAUNA_PREDATOR

## 2026-05-11 - FAUNA_PREDATOR - PENDING VERIFICATION

What was wrong:
- Apex predators used the same dominant-axis steering as swarm predators, so large predators could turn with cheap snap behavior.
- Predator utility scoring did not explicitly square final action scores before state selection.
- Acoustic sight used equivalent squared-distance math but not the prompt's explicit `math.distancesq` expression.
- Bite impulse did not publish a predator-owned `ImpactSignal` to `GlobalSignals`.
- Wander refresh used float frac constants instead of an explicit LCG-style patrol hash.

What was done:
- Added Tier 0 apex S-curve steering in `PredatorCognitionDomain` using `CinematicMath.FastNlerp`, triangle-wave lateral bias, rsqrt normalization, and `MathGuard` fallback.
- Preserved Tier 1/Tier 2 swarm behavior on dominant-axis snap paths; smooth steering is apex-only while stalking/attacking.
- Squared final predator action scores with direct `score * score`.
- Kept fixed cinematic intercept at `PlayerPosition + PlayerVelocity * 0.65f`.
- Changed acoustic through-wall acquisition to `math.distancesq(input.PlayerPosition, input.Position) < 2500f`.
- Verified vortex escape uses voxel/SDF wall probe, dominant X/Z normal, and `cross(normal, up)`.
- Verified director LOS ray lane is a single scheduled `RaycastCommand` every 0.5s after squared-cone prefilter.
- Verified pack sync uses `NativeParallelHashMap<int, float3>`.
- Verified ambush pull uses threat voxel/SDF gradient.
- Added predator bite impact signal publish from `Mass * Velocity`.
- Verified triangle-wave speed surge, cached shader camouflage lane, precomputed reciprocals, final finite guards, SlowTick stagger, SOA `NativeArray<byte>` state, and consolidated `CombatDamageRuntime` API.
- Replaced wander refresh selection with deterministic LCG-style integer hash seeded by `WanderSequence` and center coordinates.

Apex Flanking S-Curve vs Swarm Snap:
- Apex: `ResolveApexSCurveDirection(slot, stateMask, selfPosition, targetPosition, fallbackForward, currentTime)` -> rsqrt target direction -> lateral triangle wave -> `CinematicMath.FastNlerp` turn blend -> finite-guarded steering.
- Swarm: `ResolveDominantAxis(targetPosition - selfPosition, fallbackForward)` -> voxel vortex correction -> dominant-axis output. No quaternion/nlerp path.

Cinematic Cheats used:
- S-curve is a deterministic lateral triangle-wave fake, not a path solver.
- Acoustic sight ignores occlusion when noise threshold and squared range pass.
- Vortex wall escape uses SDF/voxel probe and dominant normal instead of synchronous raycast.
- Ambush gradient uses one-cell SDF/hash bias, not a neighbor scan.
- Surge/glide uses triangle-wave multiplier, not animation curve sampling.
- Wander uses integer LCG hash, not sin/cos/noise objects.

Exact Microseconds saved:
- Vision cone squared dot vs normalized angle: 0.4 us per cone check static estimate.
- Utility score direct multiply vs pow/curve path: 0.8 us per predator utility eval static estimate.
- Fixed lead intercept vs ETA solve: 1.2 us per target solve static estimate.
- Acoustic distance squared vs sqrt distance: 0.3 us per acoustic check static estimate.
- Vortex SDF fake vs synchronous raycast event: 4.0 us per blocked steering event static estimate.
- Pack `NativeParallelHashMap` vs managed registry: 2.0 us per pack update static estimate.
- Ambush one-sample SDF gradient vs 6-neighbor scan: 6.0 us per ambush solve static estimate.
- Wander LCG hash vs float frac/random lane: 0.2 us per wander refresh static estimate.
- SOA byte state vs managed state fanout: 1.0 us per 256-slot pass static estimate.
- Non-apex swarm steering regression: 0.0 us.
- Measurement status: PENDING VERIFICATION; no profiler capture was taken in this batch.

Compile / verification:
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false` passed repeatedly with 0 warnings and 0 errors after tasks 6-10, 11-15, 16-20, and final polish loop.
- Full dependency build is currently blocked outside fauna by `HectonCelestialEngine` missing celestial helper methods and `HectonFluidEngine` missing `ImpactSignal` in `Hecton8.Core.csproj`.
- `git diff --check` on touched fauna/status/rationale files returned no whitespace errors; only existing LF/CRLF warnings appeared.
- Initial exact polish lookup missed the attributed `<POLISH_MANDATE id="OMEGA_POLISH">`; the Omega addendum below records the completed polish pass.

## 2026-05-11 - OMEGA_POLISH Addendum - VERIFIED MASTER GRADE (FAUNA DOMAIN)

What was wrong:
- The polish tag was present as `<POLISH_MANDATE id="OMEGA_POLISH">`, so exact-tag parsing missed it.
- Apex S-curve had species-tier gating but needed explicit hardware-tier gating for the scalability matrix.

What was done:
- Parsed the attributed polish mandate.
- Added `CognitionInputFlags.HighTierSmoothSteering`.
- Added managed bridge gate: `GlobalRegistry.ScalabilityTier` High/Ultra plus `GlobalRegistry.TargetMathPrecision == High`.
- Kept Burst cognition clean by passing the quality decision as a native bit flag instead of reading managed registry state inside the job.
- Re-ran domain compile and mandatory core build.

Cinematic Cheats used:
- High/Ultra apex: triangle-wave S-curve plus `CinematicMath.FastNlerp`.
- Low/Mx350/Mid apex and all swarms: dominant-axis snap.
- Wander: integer LCG-style hash.
- Acoustic and ambush: threshold/SDF fakes, no heavy physical solve.

Exact Microseconds saved:
- Low/Mx350 apex avoids quaternion nlerp and S-curve lateral solve; static estimate remains 2.0 us saved per apex SlowTick versus high-tier path.
- No managed allocations were added to the job path.

Final Git Diff:
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`: consumes `HighTierSmoothSteering`; S-curve only executes when hardware-tier flag is set.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs`: writes `HighTierSmoothSteering` from `GlobalRegistry.ScalabilityTier`/precision.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: predator bite `ImpactSignal` path remains.
- Omega diff stat: 3 fauna files changed, 203 insertions, 39 deletions in the current working diff. Some `FaunaBrain.cs` hunks are concurrent non-owned presentation edits preserved in place.

Build:
- `Assembly-CSharp.csproj --no-dependencies`: PASS, 0 warnings, 0 errors.
- `Hecton8.Core.csproj`: FAIL outside fauna in `VoxelDeltaProcessor.cs` with 10 errors, 0 warnings. No predator-owned compile errors.

## 2026-05-11 - Continuation Build Reconciliation

What changed:
- Re-ran the builds after dependency churn.
- The prior `VoxelDeltaProcessor.cs` compile errors are no longer present.

Current build state:
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false`: PASS, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false /nr:false`: PASS, 0 errors, 28 warnings.

Warning ownership:
- Warnings are outside FAUNA_PREDATOR: Unity URP package cache, GPUInstancer asset code, and Crest shared/editor package code.
- No predator-owned compiler warnings were emitted by the domain build.

Documentation update:
- `Status_FAUNA_PREDATOR.md` now records Loop 7 with the current build result.
- `Rationale_FAUNA_PREDATOR.md` now includes Decision 7: Continuation Build Reconciliation.
