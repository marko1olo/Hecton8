# LOG_SDF_TRAVERSAL_KINEMATICS

## 2026-05-13 - Tight-Gap Swimming SDF Traversal
What was wrong:
- Player movement depended on capsule sweep/slide behavior that can stall on voxel/wreck corner contacts even when nearby SDF clearance exists.
- Tight-gap feedback had no decoupled locomotion-owned squeeze state for animation, physiology, camera, haptic, audio, or telemetry consumers.
- Full Burst compile proof is currently blocked by project-wide asmdef/reference failures outside this edit set.

What was done:
- Extended `HectonPlayerMotor` after scheduled CCD consumption with SDF-aware lateral squeeze correction.
- Added Burst-safe SDF trilinear sampling and open-space gradient helpers in `PlayerKinematicsBodyJob`.
- Wired `VoxelSdfTexture3D` through `GlobalDataVault` when metadata-compatible, with fallback to the active published voxel SDF payload.
- Orthogonalized squeeze direction against intended movement before applying velocity correction.
- Added 6-tap central gradient for normal tiers and 4-tap tetrahedral gradient for low tier.
- Added `PlayerStateSignal(StateSqueezing)` flags, haptic gear scrape, camera impact roll cue, fabric scrape acoustic ping, physiology stress coupling, and squeeze intervention telemetry.
- Omega polish replaced SDF hot-path divisions with `math.rcp`/constant reciprocal and converted SDF sample-mode checks to bitmask tests.

Cinematic Cheats used:
- Camera roll impact cue instead of simulated body/capsule deformation.
- Low-amplitude haptic scrape instead of wall contact microphysics.
- Random gated fabric scrape acoustic ping instead of continuous material friction simulation.
- 60 percent speed penalty to sell shoulder/body rotation without changing capsule shape.
- SDF gradient centerline pull instead of expensive multi-contact penetration recovery.

Exact Microseconds saved or spent:
- Removed player `Physics.ComputePenetration` dependency: no remaining use found under `Assets/_Project/Scripts`; avoids unbounded main-thread recovery spikes. Exact saved value cannot be measured until the global compile wall is cleared.
- 6-tap gradient cost estimate: +9 us on i3/MX350 squeeze frames.
- 4-tap tetra gradient cost estimate: +6 us on i3/MX350 squeeze frames, about 3 us saved versus 6-tap.
- Candidate SDF density check: +2 us on squeeze frames.
- Signal fanout cost estimate: +1 us each for player state, haptic, acoustic, and telemetry writes.
- Omega reciprocal polish: estimated 1-2 us saved on squeeze frames versus float divisions.
- GC: 0 B/frame in added SDF math and signal paths.

Verification:
- `validate_script` passed with zero diagnostics for `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, and `PlayerStressMetricsRuntime.cs`.
- `rg ComputePenetration Assets/_Project/Scripts` returned no matches.
- `Select-String` audit found no `foreach`, `string.Format`, `$"..."`, `.ToString()`, `math.sqrt`, or `math.normalize` in touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` remains red due global dependencies: `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, and `BinaryBlittableSafeAttribute` resolution failures. Log captured at `Docs/AgentLogs/Build_SDF_TRAVERSAL_KINEMATICS_Hecton8Core.log`.

Status:
- Core tasks 1-18 complete.
- Task 19 marked blocked by global dependency; changed scripts validate locally under Unity MCP.
- Global status remains `PENDING VERIFICATION` until the integrator repairs the assembly graph and Burst build proof can run.

## 2026-05-13 - Patient Recheck Upgrade
What was wrong:
- Body-job SDF gradient telemetry could run on idle fixed ticks when an SDF payload existed.
- Low tier still paid SDF payload lookup cost outside active squeeze windows.
- Trilinear SDF sampling clamped out-of-bounds positions, which could falsely validate a squeeze candidate near the edge of a published SDF volume.

What was done:
- Added `SdfGradientProbeRequested` to gate expensive body-job SDF gradient telemetry behind recent `PlayerStateSignal(StateSqueezing)`.
- Low tier now skips idle SDF payload snapshotting; non-low tiers retain center-density telemetry for richer diagnostics.
- `TrySampleSdfTrilinear` now fails outside published half-cell bounds before interpolation.

Cinematic Cheats used:
- No new simulation honesty added. The system still uses lateral SDF centerline pull, camera impact roll, haptic scrape, random acoustic scrape, and speed penalty instead of real body deformation/contact microphysics.

Exact Microseconds saved:
- Low-tier idle SDF-covered frames: estimated 4-10 us saved by avoiding payload lookup and 1/4/6 trilinear sample paths.
- Squeeze frames: unchanged bounded cost, with one extra bounds branch per trilinear sample.
- GC: still 0 B/frame.

Verification:
- Unity MCP standard validation passes for `PlayerKinematicsRuntime.cs`, `HectonPlayerMotor.cs`, and `PlayerStressMetricsRuntime.cs`.
- Touched-file text audit reports no `foreach`, `string.Format`, `$"..."`, `.ToString()`, `math.sqrt`, `math.normalize`, `/ 255.0f`, or `/ safeCellSize`.
- Editor console currently reports unrelated `DeployableSdfDrillRuntime.cs` errors from the mining domain; not edited.

## 2026-05-13 - Capsule Clearance and SDF Dump Upgrade
What was wrong:
- Squeeze candidate approval still relied on center density plus one lateral candidate sample. That could pass when the center point looked open but the capsule endpoint line still intersected denser SDF.
- Positive SDF tolerance was `0.12`, too permissive for a locomotion accept path because positive density is documented as solid.
- The existing fault telemetry dump did not emit `Dump_SDF_TRAVERSAL_KINEMATICS.bin`.

What was done:
- Added `TryValidateSdfSqueezeCapsuleLine` to sample both capsule centerline endpoints after the proposed squeeze offset.
- Reduced `SdfSqueezeMaxCandidateSolidDensity` to `0.02`, leaving quantization tolerance without accepting meaningful wall debt.
- Added `Dump_SDF_TRAVERSAL_KINEMATICS.bin` using the existing fixed 300-frame kinematics telemetry ring.

Cinematic Cheats used:
- Still no real body deformation. The system uses SDF centerline pull, endpoint safety, speed penalty, camera roll, haptic scrape, and random acoustic scrape.

Exact Microseconds saved or spent:
- Endpoint safety adds two trilinear reads only on squeeze candidate frames, estimated +3-4 us on i3/MX350.
- Dump path has 0 frame cost outside fault handling.
- GC remains 0 B/frame in the hot squeeze path.

Verification:
- `PlayerKinematicsRuntime.cs` validates through Unity MCP.
- `HectonPlayerMotor.cs` MCP validation timed out/disconnected after the earlier clean pass; `dotnet build Hecton8.Core.csproj --no-restore` reports only known global assembly/reference blockers and no new SDF/helper syntax errors.
- `git diff --check` clean.

## 2026-05-13 - Signal Semantics and Stress Ownership Recheck
What was wrong:
- `PlayerStateSignal.Intensity01` was overloaded as both squeeze presentation strength and stress delta.
- That made downstream animation/IK less useful and made physiology depend on a locomotion-side delta instead of its own slow-tick cadence.

What was done:
- `HectonPlayerMotor` now emits normalized squeeze intensity in `PlayerStateSignal.Intensity01`.
- `PlayerStressMetricsRuntime` converts the latest `StateSqueezing` signal into `SqueezeStressPerSlowTick`, preserving the requested `0.1/s` oxygen stress behavior as physiology-owned logic.

Cinematic Cheats used:
- No new physical simulation. The squeeze remains SDF centerline correction plus speed penalty, camera roll, low haptic scrape, and gated fabric scrape.

Exact Microseconds saved or spent:
- Runtime cost unchanged in locomotion.
- Physiology keeps one latest-state read and one scalar max operation, estimated +1 us on the consumer path.
- GC remains 0 B/frame.

Verification:
- Targeted diff audit confirms motor emits squeeze intensity while physiology applies `SqueezeStressPerSlowTick`.
- Unity MCP standard validation passes for `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, and `PlayerStressMetricsRuntime.cs`.
- `git diff --check` is clean and the hot-path `rg` audit has no matches.
- Full build proof is still blocked by known project-wide asmdef/reference failures outside this domain; latest blockers are captured in `Docs/AgentLogs/Build_SDF_TRAVERSAL_KINEMATICS_Hecton8Core_latest.log`.

## 2026-05-13 - DataVault Consistency Recheck
What was wrong:
- `PlayerKinematicsRuntime` could sample a compatible DataVault SDF buffer while `HectonPlayerMotor` sampled only the published voxel payload for authoritative squeeze acceptance.
- That could let telemetry and movement judge different SDF data when an upstream system publishes a denser or relocated shared buffer.

What was done:
- Added `ResolveSdfTraversalPayload` in `HectonPlayerMotor`.
- Motor squeeze now validates expected SDF length, then uses `GlobalDataVault` `VoxelSdfTexture3D` when compatible before gradient, candidate, and capsule endpoint sampling.

Cinematic Cheats used:
- No new simulation. This is source-of-truth alignment for the same SDF centerline squeeze, endpoint safety, speed penalty, camera roll, haptic scrape, and gated fabric scrape.

Exact Microseconds saved or spent:
- Adds one DataVault `TryGetBuffer` plus length check only on squeeze candidate frames, estimated below +1 us on i3/MX350.
- Idle-frame cost is unchanged.
- GC remains 0 B/frame.

Verification:
- Unity MCP standard validation passes for `HectonPlayerMotor.cs`.
- Focused `git diff --check` is clean.
- Hot-path `rg` audit has no matches.

## 2026-05-13 - SDF Count Overflow Guard
What was wrong:
- SDF expected-length checks multiplied dimensions as `int`, which could overflow on corrupt or impossible payload metadata before the length comparison.

What was done:
- Added `PlayerKinematicsBodyJob.TryResolveSdfVoxelCount` with long-backed multiplication and an `int.MaxValue` cap.
- Routed the trilinear sampler, kinematics snapshot, and motor squeeze path through that helper.

Cinematic Cheats used:
- None added. This is crash prevention for the same SDF squeeze path.

Exact Microseconds saved or spent:
- Adds below +1 us on SDF payload acceptance/candidate frames.
- Prevents invalid SDF metadata from reaching interpolation/index math.
- GC remains 0 B/frame.

Verification:
- Unity MCP standard validation passes for `PlayerKinematicsRuntime.cs` and `HectonPlayerMotor.cs`.
- Focused `git diff --check` is clean.
- Follow-up Unity MCP validation also passes for `PlayerStressMetricsRuntime.cs`.
- Unity console remained red from external compile errors, not SDF traversal diagnostics.

## 2026-05-13 - Hot Helper Inlining Pass
What was wrong:
- The squeeze candidate path repeatedly calls small SDF helpers for sampling, decode, voxel-count validation, payload resolution, and sample-step selection.
- Those helpers were safe but not explicitly marked for inlining.

What was done:
- Re-extracted the active `SDF_TRAVERSAL_KINEMATICS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Added `MethodImplOptions.AggressiveInlining` to SDF helpers in `PlayerKinematicsRuntime`.
- Added `MethodImplOptions.AggressiveInlining` to motor-side SDF payload and sample-step helpers in `HectonPlayerMotor`.

Cinematic Cheats used:
- None added. This is CPU polish for the same SDF squeeze and presentation signals.

Exact Microseconds saved or spent:
- Estimated below 1 us saved per squeeze candidate frame on i3/MX350.
- No idle-frame cost.
- GC remains 0 B/frame.

Verification:
- Unity MCP standard validation passes for `PlayerKinematicsRuntime.cs`, `HectonPlayerMotor.cs`, and `PlayerStressMetricsRuntime.cs`.
- Focused `git diff --check` is clean.
- Hot-path debt audit has no matches.
- Latest Unity console blocker is external `HectonUnderwaterVisuals.cs(7393,1)` CS1022.

## 2026-05-13 - SDF Border Sampling Precision
What was wrong:
- Accepted SDF samples near the published volume border were clamped to `grid - 1.001`, so the last voxel center could not be sampled exactly.

What was done:
- Kept the half-cell out-of-bounds reject.
- Changed the accepted sample clamp to exact `grid - 1.0`.

Cinematic Cheats used:
- None added. This is precision cleanup for the same squeeze acceptance path.

Exact Microseconds saved or spent:
- No measurable cost change.
- Same branches and same trilinear sample count.
- GC remains 0 B/frame.

Verification:
- Unity MCP standard validation passes for `PlayerKinematicsRuntime.cs`, `HectonPlayerMotor.cs`, and `PlayerStressMetricsRuntime.cs`.
- Focused `git diff --check` is clean.
- Hot-path debt audit has no matches.
