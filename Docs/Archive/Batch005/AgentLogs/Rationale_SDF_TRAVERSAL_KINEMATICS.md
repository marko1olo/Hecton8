# Rationale_SDF_TRAVERSAL_KINEMATICS

Status: PENDING VERIFICATION

## Decision 0 - Batch Bootstrap
Problem: Player tight-gap swimming requires SDF-aware movement without coupling to unfinished voxel, camera, haptic, stress, or audio agents.
Solution: Start from existing locomotion code and prefer cached interfaces or existing NativeQueue/EventBus signal contracts. Use SDF math only where gameplay collision correctness needs it; camera roll, haptic scrape, audio scrape, and stress are presentation/feedback signals.
Rejected Alternatives: Direct concrete references to camera, audio, haptic, stress, or voxel runtime classes. Standard Unity `ComputePenetration` as player movement authority because prompt explicitly requires purge.
Scalability potential: Low uses 4-tap tetrahedral gradient and conservative correction. Middle uses 6-tap central gradient. High/Ultra can raise visual feedback density through camera roll/audio/haptic cadence without increasing collision truth.
Hardware Impact: Target is i3/MX350. Expected low-tier gain comes from avoiding main-thread penetration recovery loops and keeping squeeze math bounded. Measured proof absent.

## Decision 1 - SDF Open-Space Gradient
Problem: Capsule sweeps can report a corner block even when the voxel SDF contains nearby passable clearance. Standard collision normals do not know the centerline of the procedural gap.
Solution: Sample trilinear SDF density around the player, invert the density gradient toward lower/clearer space, remove the component parallel to intended movement, then normalize before applying a lateral squeeze correction.
Rejected Alternatives: `Physics.ComputePenetration`, multi-ray fan recovery, or moving forward along the SDF gradient. Unity penetration recovery is explicitly banned for player movement and forward gradient injection can accelerate through a blocker.
Scalability potential: Low uses 4-tap tetrahedral gradient. Middle uses 6-tap central gradient. High can raise camera/haptic/audio response density. Ultra can spend saved CPU on denser SDF publication or stronger visual squeeze cues without changing collision truth.
Hardware Impact: i3/MX350 estimate is about +9 us for 6-sample mode and about +6 us for tetra mode on squeeze frames only. Idle frames pay branch/metadata checks only.

## Decision 2 - Contract-Only Consequences
Problem: Tight-gap traversal needs animation, stress, haptic, camera, audio, and telemetry consequences without taking direct dependencies on systems owned by other agents.
Solution: Publish `PlayerStateSignal(StateSqueezing)`, `HapticRequest`, `AcousticPingSignal`, and camera impact signals through existing global signal lanes. Physiology consumes the latest player-state squeeze signal and applies stress cause 6.
Rejected Alternatives: Direct references to `CameraJuiceSystem`, haptic components, audio emitters, or physiology runtime. These would create cross-domain dependency knots and break simultaneous-agent integration.
Scalability potential: Low keeps consequence cadence sparse. Middle uses per-squeeze haptic and occasional scrape audio. High/Ultra can increase presentation intensity downstream without touching movement math.
Hardware Impact: Signal writes are estimated at +1 us each and 0 B/frame GC. Cost is bounded and only active during a valid squeeze.

## Decision 3 - After-CCD Placement
Problem: Squeezing before CCD would allow a lateral correction to tunnel the player through a 1m solid wall.
Solution: Run squeeze only after the scheduled capsule sweep result is consumed in `CompleteScheduledSweepInLateFrameSwapWindow`, and candidate-check the lateral correction with SDF density before accepting it.
Rejected Alternatives: Update-time transform correction, pre-sweep offsetting, or camera-only fake without movement correction. Pre-sweep correction invalidates CCD; camera-only fake still leaves the player stuck on the invisible corner.
Scalability potential: Low tier uses the same safety gate with fewer samples. Middle/High can keep the same authoritative correction and spend extra budget on camera roll, audio scrape density, and IK consumers.
Hardware Impact: Candidate check adds one trilinear sample and a fixed branch on squeeze frames. Expected low-end cost is about +2 us and prevents expensive stuck-recovery loops.

## Decision 4 - AUP and DataVault Boundary
Problem: SDF payloads can become stale during floating-origin shifts, and direct voxel-volume reads inside jobs would violate the job boundary.
Solution: Snapshot metadata on the simulation side, prefer `GlobalDataVault` buffer `VoxelSdfTexture3D` when it matches active volume metadata, reject squeeze while origin shifting, and pass only `NativeArray<byte>` plus scalar metadata into Burst-safe math.
Rejected Alternatives: Holding a managed voxel-volume reference in the job, sampling Unity `Texture3D`, or trusting stale SDF coordinates across an AUP shift.
Scalability potential: Low can use tiny published SDF grids and tetra sampling. Middle can use active-volume SDF. High/Ultra can publish denser DataVault SDF buffers while keeping the locomotion job unchanged.
Hardware Impact: Metadata checks are constant-time. DataVault reuse avoids allocation and avoids main-thread texture API cost. Estimate: 0 B/frame, +4 us for payload/sample setup on squeeze frames.

## Decision 5 - Compile Wall Handling
Problem: Required build proof is blocked by project-wide assembly/reference failures outside the SDF edit set.
Solution: Captured `Docs/AgentLogs/Build_SDF_TRAVERSAL_KINEMATICS_Hecton8Core.log` and `Docs/AgentLogs/Build_SDF_TRAVERSAL_KINEMATICS_Hecton8Core_latest.log`, validated changed gameplay/physiology scripts through Unity MCP with zero diagnostics, and marked task 19 blocked by global dependency instead of editing unrelated asmdefs.
Rejected Alternatives: Adding broad asmdef references, deleting attributes, or mutating project graph to force a green local build. Those are integrator-level changes and risk breaking other agents.
Scalability potential: No runtime impact. Once the global asmdef graph is repaired, the SDF interpolation path can be Burst-verified without changing the implementation.
Hardware Impact: None at runtime. Build blocker prevents measured Burst timing; estimates remain analytical until integrator fixes the dependency graph.

## OMEGA POLISH CHANGES
Problem: Initial SDF trilinear sample path used hot float division for world-to-grid conversion and byte decode, and sample-mode selection used equality instead of the mandated bitmask style.
Solution: Replaced `sample = delta / safeCellSize` with `sample = delta * math.rcp(safeCellSize)`, replaced `/ 255.0f` with `InvEncodedSdfByteMax`, and changed tetra mode checks to `(sampleMode & SdfSampleModeTetra4) != 0`.
Rejected Alternatives: LUT for trilinear SDF decode and dominant-axis-only squeeze for all tiers. LUT would add memory traffic for a single byte-to-float scale; dominant-axis-only correction would reduce tight-gap quality on mid/high tiers and break the requested 6-sample behavior.
Scalability potential: Low keeps tetra 4-tap and sparse consequences. Middle keeps 6-tap correction. High uses the same authoritative math and lets camera/audio/haptic/IK consumers add overkill visuals. Ultra can increase SDF resolution upstream without changing locomotion code.
Hardware Impact: Division removal saves approximately 1-2 us on i3/MX350 squeeze frames depending on Burst lowering. GC remains 0 B/frame.
Cinematic Cheats used: Camera roll via `CameraJuiceSignals.PublishImpact`; low-amplitude gear scrape haptic; random fabric scrape acoustic ping; 60 percent speed penalty to sell body rotation without simulating shoulders/capsule deformation.
Code Diff Snapshot:
```text
Assets/_Project/Scripts/Core/GlobalSignals.cs      |  511 ++++++++-
Assets/_Project/Scripts/Core/Memory/H8Memory.cs    |   47 +-
Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs | 283 ++++-
Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs | 1127 ++++++++++++++++++--
Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs | 29 +
5 files changed, 1884 insertions(+), 113 deletions(-)
```
Targeted SDF diff summary: `VoxelSdfTexture3D` buffer id added; `PlayerKinematicsBodyJob` now accepts SDF metadata and performs Burst-safe trilinear sampling; `HectonPlayerMotor` applies post-CCD orthogonal squeeze correction and emits decoupled signals; physiology consumes squeeze stress; telemetry records squeeze intervention count.

## Decision 6 - Idle SDF Cost and Bounds Clamp Recheck
Problem: The first implementation could spend 4-6 trilinear reads in `PlayerKinematicsBodyJob` on every fixed tick when an SDF payload existed, and the trilinear sampler clamped out-of-bounds samples to the nearest voxel. That is suspicious on i3/MX350 and unsafe for candidate squeeze validation near SDF borders.
Solution: Added `SdfGradientProbeRequested` so expensive gradient telemetry runs only when a recent squeeze state exists. Low tier now skips idle SDF payload snapshotting; higher tiers keep richer center-density telemetry. `TrySampleSdfTrilinear` now rejects positions outside published half-cell bounds before interpolation.
Rejected Alternatives: Removing body-job SDF sampling entirely, or leaving clamp semantics because `HectonVoxelVolume.TrySampleDensity` clamps. Removing the job probe weakens telemetry and the original task; clamping is acceptable for visualization but wrong for accepting locomotion escape candidates.
Scalability potential: Low samples SDF only near a real squeeze. Middle/High can retain center-density telemetry on idle frames. Ultra can use denser upstream SDF buffers without increasing low-tier cost.
Hardware Impact: Low-tier idle frames save the payload lookup and all SDF trilinear reads. Estimated low-end saving is 4-10 us per fixed tick in SDF-covered areas, while squeeze frames still pay bounded 4/6-tap cost. Safety improvement blocks false squeeze approval at SDF borders.

## Decision 7 - Capsule-Line Clearance and Agent Dump
Problem: A single center-point candidate sample can approve a squeeze where the centerline is open but the capsule endpoints still sit in denser SDF. The existing kinematic telemetry dump also wrote physics/IK files but not the mandated SDF traversal dump file.
Solution: Validate candidate SDF at the squeezed capsule centerline endpoints before accepting the correction, reduce positive candidate density tolerance from `0.12` to `0.02`, and write `Dump_SDF_TRAVERSAL_KINEMATICS.bin` from the same fixed 300-frame telemetry ring on fault.
Rejected Alternatives: Full capsule-radius SDF clearance, extra Physics queries, or accepting the previous center-only test. Full radius clearance would make the cinematic squeeze behave like the fat capsule again; Physics queries violate the SDF traversal mandate and add main-thread cost. Center-only was too permissive near head/feet line contacts.
Scalability potential: Low/Middle/High/Ultra all use the same endpoint safety because it is only paid on accepted squeeze candidates. Higher tiers can still add visual overkill downstream; collision authority remains bounded.
Hardware Impact: Adds two trilinear reads only on squeeze candidate frames, estimated +3-4 us on i3/MX350. The stricter `0.02` solid tolerance blocks wall debt while preserving quantization slack near the zero crossing.

## Decision 8 - Signal Semantics and Stress Ownership
Problem: `PlayerStateSignal.Intensity01` was briefly overloaded as both squeeze presentation strength and physiology stress delta. That weakens animation/IK consumers, hides stress units, and risks under-scaled physiology because the stress runtime integrates on its own slow-tick cadence.
Solution: Keep `Intensity01` as normalized squeeze strength from the motor. Physiology consumes the latest `StateSqueezing` signal and applies a deterministic `SqueezeStressPerSlowTick` equal to `0.1/s` converted to the physiology slow-tick interval.
Rejected Alternatives: Directly calling physiology from locomotion, passing fixed-delta stress through a presentation signal, or dropping intensity data from the state signal. Direct calls violate the domain boundary; fixed-delta data in the signal couples locomotion to physiology tick policy; dropping intensity would damage downstream animation/IK quality.
Scalability potential: Low devices keep one latest-state read and one scalar stress impulse. Middle/High/Ultra keep full squeeze intensity available for richer animation, camera, haptic, and IK consumers without increasing locomotion cost.
Hardware Impact: No extra hot-path allocation and no new container scan. Estimated cost stays +1 us in the physiology consumer path, with clearer ownership and 0 B/frame GC.

Verification: Unity MCP standard validation passes for `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, and `PlayerStressMetricsRuntime.cs`. `git diff --check` is clean and the hot-path `rg` audit has no matches. Full build proof is still blocked by known project-wide asmdef/reference failures outside this domain; latest build-log blockers are captured in `Docs/AgentLogs/Build_SDF_TRAVERSAL_KINEMATICS_Hecton8Core_latest.log`. Latest Unity console blocker is external `HectonUnderwaterVisuals.cs(7393,1)` CS1022.

## Decision 9 - Motor and Job SDF Source Consistency
Problem: The kinematics job could prefer a compatible `GlobalDataVault` `VoxelSdfTexture3D` buffer while the motor-side squeeze acceptance path sampled only the published voxel payload. That can create false disagreements where telemetry probes one buffer and authoritative squeeze correction accepts or rejects against another.
Solution: Added `ResolveSdfTraversalPayload` in `HectonPlayerMotor` so the motor validates expected payload length, then prefers the compatible DataVault buffer exactly as the kinematics snapshot path does. Gradient, candidate, and endpoint validation now use the same resolved source.
Rejected Alternatives: Always force the published payload, or allocate/copy the vault buffer into a motor-owned cache. Forcing published data discards the global shared SDF contract; copying introduces allocation/ownership risk and stale data.
Scalability potential: Low keeps the same sparse squeeze sampling with no extra reads. Middle/High/Ultra can receive denser upstream SDF buffers through the vault without changing motor logic.
Hardware Impact: One `TryGetBuffer` and length check only during squeeze candidate resolution. Estimated cost is below +1 us on i3/MX350 candidate frames, with no GC and no idle-frame cost.

## Decision 10 - SDF Voxel Count Overflow Guard
Problem: SDF length validation multiplied `x*y*z` as `int` in multiple paths. A corrupt or impossible dimension payload could overflow before the length comparison, then flow into trilinear indexing with a misleading expected length.
Solution: Added `PlayerKinematicsBodyJob.TryResolveSdfVoxelCount`, using `long` multiplication and an `int.MaxValue` ceiling. The trilinear sampler, kinematics snapshot, and motor squeeze path all use the helper before accepting the payload.
Rejected Alternatives: Trusting upstream voxel metadata, duplicating guards in each caller, or clamping dimensions. Trusting metadata leaves a crash vector; duplicated guards drift; clamping would silently sample the wrong SDF.
Scalability potential: All tiers get the same fail-fast guard. High/Ultra can still publish larger SDFs up to safe NativeArray limits; low tier pays the guard only when a payload is considered.
Hardware Impact: Three integer comparisons plus one 64-bit multiply chain during SDF payload acceptance/sample calls. Estimated cost is below +1 us on i3/MX350 candidate frames and avoids undefined index debt. GC remains 0 B/frame.

Verification: Unity MCP standard validation passes for `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, and `PlayerStressMetricsRuntime.cs`. Focused `git diff --check` is clean. Console errors remain outside locomotion.

## Decision 11 - Hot Helper Inlining
Problem: Tight-gap squeeze frames call small SDF helpers repeatedly: trilinear sample, voxel-count guard, byte decode, normalization, payload source resolution, and squeeze sample-step resolution. Without explicit inlining hints, IL2CPP/Burst may keep avoidable call overhead in the candidate path.
Solution: Added `MethodImplOptions.AggressiveInlining` to the SDF Burst helpers in `PlayerKinematicsRuntime` and the motor-side SDF payload/sample-step helpers in `HectonPlayerMotor`.
Rejected Alternatives: Manually expanding helper code into the call sites, or leaving compiler heuristics alone. Manual expansion would duplicate safety logic and increase bug surface; relying on heuristics is weaker when the helpers sit inside a high-frequency candidate path.
Scalability potential: Low tier benefits most because tetrahedral squeeze frames stay as cheap as possible. Middle/High/Ultra keep the same semantics and can spend the saved budget on richer downstream visual consequences.
Hardware Impact: Expected gain is below 1 us per squeeze candidate frame on i3/MX350, but it removes avoidable overhead from repeated helper calls with 0 B/frame GC.

Verification: Active XML prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`. Unity MCP standard validation passes for `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, and `PlayerStressMetricsRuntime.cs`. Focused `git diff --check` is clean and the hot-path debt audit has no matches. Latest Unity console blocker is external `HectonUnderwaterVisuals.cs(7393,1)` CS1022.

## Decision 12 - SDF Border Sampling Precision
Problem: The trilinear sampler rejected samples outside the published half-cell bounds, then clamped accepted samples to `grid - 1.001`. That prevented exact sampling of the last voxel center and biased border density slightly inward.
Solution: Keep the half-cell reject gate, but clamp accepted samples to exact `grid - 1.0`. The `x1/y1/z1 = min(x0 + 1, grid - 1)` logic already makes last-center sampling safe.
Rejected Alternatives: Keeping the inward bias, or allowing clamp-only behavior at all out-of-bounds positions. The bias can matter at tight-gap SDF volume borders; clamp-only behavior can falsely approve traversal outside the published SDF.
Scalability potential: All tiers get more accurate border density with no extra samples. Higher tiers can still publish larger SDF volumes without changing locomotion.
Hardware Impact: No measurable cost change. The max clamp constant changes only; branch and sample count are identical. GC remains 0 B/frame.

Verification: Unity MCP standard validation passes for `PlayerKinematicsRuntime.cs`, `HectonPlayerMotor.cs`, and `PlayerStressMetricsRuntime.cs`. Focused `git diff --check` is clean and the hot-path debt audit has no matches.
