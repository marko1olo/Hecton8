# ANIM_PROCEDURAL_LEGS_IK Rationale

Status: PENDING VERIFICATION

## Decision 1: Extend existing contextual IK owner

Problem: VR lower-body presence needs legs without creating a second animation authority.
Solution: Extend `ContextualPhysicalIkRuntime` and `ContextualPhysicalIkRig`, because the existing system already owns batched ground probes, PlayableGraph injection, two-bone Burst math, origin-shift rebasing, and black-box telemetry.
Rejected Alternatives: A new MonoBehaviour IK manager would duplicate scheduling and risk two owners writing the same bones. Unity Animator foot IK was rejected because the prompt requires Burst math and the mandates forbid Animator foot IK hot paths.
Scalability potential: Low disables non-XR foot IK. Middle keeps stepped 2-bone legs. High increases visual fidelity through existing muscle bulge/secondary chains. Ultra keeps all lower-body presentation active with smoother stepping.
Hardware Impact: i3/MX350 avoids an additional scheduler and keeps work in the existing batched raycast path; estimated gain versus separate manager is one fewer registry tick and zero extra command buffers.

## Decision 2: Use typed KCC velocity signal

Problem: Lower-body swim/step posture needs player velocity without a concrete `HectonPlayerMovement` dependency.
Solution: Add `KccVelocitySignal` to `PhysicsDeterminismSignals`, emitted from `PlayerKinematicsRuntime` and read as latest signal by contextual IK.
Rejected Alternatives: Polling Rigidbody from IK would couple animation to physics ownership. Reading `GlobalRegistry.Player` every tick was rejected because dependencies must be cached or signal-driven.
Scalability potential: Low consumes only the latest velocity. Middle/High/Ultra can use the same signal to add richer stride prediction without changing the producer.
Hardware Impact: A 32-entry NativeQueue lane plus latest snapshot is sub-kilobyte persistent memory and avoids per-frame scene/component lookup on i3/MX350.

## Decision 3: Visual fake over physical lower-body sim

Problem: Full lower-body physical simulation is not affordable for VR presence on MX350/i3 and would create collision authority ambiguity.
Solution: Use batched seabed rays, squared-distance step triggers, triangle-wave foot lift, and existing two-bone solver. This is a deterministic presentation fake.
Rejected Alternatives: VRIK/full-body IK, ragdoll legs, and per-joint physics were rejected as too expensive and too unstable for first-person VR.
Scalability potential: Low: disabled when non-XR. Middle: 2-bone stepped legs. High: velocity-aware swimming fallback. Ultra: existing muscle tension path can add visible overkill without changing solver authority.
Hardware Impact: Reuses existing raycast batch; added math is linear in two feet per player rig, estimated under 0.01 ms on i3/MX350 before Unity verification.
