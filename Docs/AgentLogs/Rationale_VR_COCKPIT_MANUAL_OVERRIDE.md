# Rationale - VR_COCKPIT_MANUAL_OVERRIDE

## Decision 1 - Isolated VR lever assembly must still receive physical hands

Problem: `PhysicalHandReceiverRegistry` was internal to the Core assembly, while the prompt requires a new `Hecton8.UI.VR` assembly. Without a bridge, the lever cannot receive `PhysicalInteractionHandler` hand callbacks.

Solution: expose the existing fixed-size registry as `public static` without changing storage, lookup, or semantics. This keeps the existing zero-GC collider lookup and avoids a new dispatcher.

Rejected Alternatives: standard Unity `GetComponentInParent<IPhysicalPanelButtonReceiver>()` from overlap results would add component traversal to the physical interaction hot path. A new singleton hand registry would violate the prompt and create integration conflict.

Scalability potential: Low uses the same O(1) receiver table. Middle/High/Ultra can add more cockpit controls without switching lookup model.

Hardware Impact: i3/MX350 avoids per-overlap component walks; estimated saved 3-12 us in dense cockpit overlap frames.

## Decision 2 - Kinematic lever, no physics joint

Problem: manual override must feel physical in OpenXR but remain deterministic and cheap.

Solution: solve hand projection in lever local space, then integrate a damped scalar spring over native angle/velocity arrays. The visual lever is just local rotation.

Rejected Alternatives: Unity `HingeJoint` would introduce solver-order variance, constraint jitter, and dependency on physics timestep; it is explicitly banned by prompt reread requirement.

Scalability potential: Low keeps reduced IK smoothing and same scalar solver. Middle/High keep smoother IK. Ultra can drive extra sparks/audio from the same angle signal without changing simulation.

Hardware Impact: i3/MX350 scalar solve is estimated under 2 us; joint solver replacement avoids unpredictable physics island cost.

## Decision 3 - Manual override as typed signal before prologue handoff

Problem: the lever must trigger prologue completion but consumers need an explicit cockpit-control event.

Solution: add `ManualOverridePulledSignal` to the existing typed `SignalBus<T>` lanes and publish `PrologueCompleteSignal` after latch.

Rejected Alternatives: direct scene transition or hard reference to prologue director would create an agent-order dependency and break the EventBus/GlobalRegistry decoupling rule.

Scalability potential: Low consumers can listen only for prologue completion. High/Ultra systems can consume angle, velocity, and hand side for overkill cockpit feedback.

Hardware Impact: fixed lane capacity 8; estimated publish cost under 1 us, no managed event fanout.
