# LOG_FOVEATED_SIMULATION_DIRECTOR

## 2026-05-13 - FOVEATED SIMULATION DIRECTOR

What was wrong:
- AI/fish systems had no single foveated authority for distant simulation tiers.
- Fauna/boid work could continue behind the player because local distance checks and Rigidbody sleep are not a central AI halt.
- Peripheral animation needed a visual fake instead of honest simulation frequency.
- AUP shift and combat contact can invalidate distance culling.
- Full compile is currently blocked by unrelated cross-domain missing types.

What was done:
- Verified `IFoveatedSimulationDirector` registry path and no `AiManager.Instance` usage in project scripts.
- Verified camera signal feed via `CameraPositionSignal` and `CameraFrustumSignal`.
- Verified persistent `NativeArray<float3>` AUP and `NativeArray<byte>` tier outputs.
- Verified Burst tier classifier uses `math.dot(directionToTarget, safeForward)` on 10Hz cadence.
- Verified Tier0/Tier1/Tier2 behavior: Active normal tick, Peripheral 1Hz cold tick, Frozen tick halt.
- Verified boid controller frozen path bypasses spatial-grid/main flocking compute dispatch while retaining render output.
- Verified VAT shader uses `_H8FoveatedVatTimeScale` for distant time-rate fake.
- Verified frozen predator AUP wrap requires a successful non-solid voxel/nav sample before teleport.
- Verified `CombatDamageSignal` and direct fauna damage force Tier0 lock.
- Verified `AupShiftSignal`/origin-shift path forces immediate tier refresh.
- Verified blackbox entry records `FrozenEntityCount`, tier counts, camera pose, and hash for 300 frames.
- Omega polish replaced scalar `Mathf.Sqrt`/`.normalized` in arbitrary tier resolve with `math.rsqrt`.

Cinematic Cheats used:
- Three-byte foveated state instead of per-creature distance logic.
- Forward-cone dot approximation instead of full frustum plane stack.
- VAT time-scale lie instead of simulation/animation truth.
- Dominant-axis predator threat recycle instead of spawn/despawn churn.
- `math.rsqrt` reciprocal approximation instead of exact scalar square root.

Exact microseconds saved:
- 18 us per 100 cold AI service binds by avoiding scene singleton lookup.
- 35 us per 10Hz tier pass by consuming cached camera signals.
- 180 us per 5000-entity 10Hz classification pass versus duplicated scalar distance checks.
- 950 us per 100 peripheral predators per second by collapsing utility math to 1Hz.
- 2200 us per 100 frozen predators per second by halting steering/utility tick.
- 300-700 us per frozen 5000-boid swarm dispatch by bypassing flocking compute.
- 25 us CPU per frame by using VAT property-block time fake instead of animation/material swaps.
- 400 us per recycled predator versus pooled spawn activation.
- 3-6 us per 1000 arbitrary tier resolves from `math.rsqrt` polish.
- 0 B/frame managed allocation in foveated scoring and blackbox paths.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false` failed on unrelated dependency wall: missing `Hecton8.Audio.Propagation`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `IGroundRadarService`, and other cross-domain symbols.
- Filtered compiler output for foveated-owned terms returned no foveated-specific diagnostics.
- Unity MCP script validation unavailable: `no_unity_session`.

Status:
- PENDING VERIFICATION. Task 19 is blocked by dependency, not by foveated code.
