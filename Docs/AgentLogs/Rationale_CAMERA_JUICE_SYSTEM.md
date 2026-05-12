# Rationale_CAMERA_JUICE_SYSTEM

Status: PENDING VERIFICATION

## Mandate Selection

Problem: Camera juice must be rebuilt as deterministic procedural math without singleton access, clip-driven shake, or GC in hot paths.
Solution: Apply GlobalRegistry for service discovery, EventBus-style signal consumption where existing contracts allow it, Zero-GC mandate for LateUpdate math, cinematic fake-first for screen trauma instead of physical camera simulation, performance budget for MX350/i3 constraints, crash telemetry/black-box rule for state recording, deterministic math for stable seeds, AUP mandate for local-space shake safety, and VR stencil/comfort constraints for XR paths.
Rejected Alternatives: Cinemachine impulse and AnimationClip-driven shake are too heavyweight for minor bumps and introduce hidden evaluation overhead. Direct CameraShake.Instance calls create tight dependencies and conflict with parallel agent work.
Scalability potential: Low uses 30Hz sampled noise with interpolation and no translation in VR. Middle evaluates full per-frame six-axis noise. High adds richer directional roll and FOV response. Ultra can spend saved CPU on stronger visual response and layered rotational detail without changing gameplay authority.
Hardware Impact: Target is 0 B/frame GC and less than 0.1 ms on i3/MX350. Estimated gain against clip/impulse path is 20-80 us CPU saved per impact burst, pending profiler proof.

## Initial Decisions

Problem: Batch identity ambiguity between role MOTION_ENGINEER and prompt id CAMERA_JUICE_SYSTEM.
Solution: Use CAMERA_JUICE_SYSTEM for status/log file names because prompt explicitly says Log to Status_CAMERA_JUICE_SYSTEM.md.
Rejected Alternatives: Status_MOTION_ENGINEER.md would violate the prompt-specific file name.
Scalability potential: Not runtime-affecting.
Hardware Impact: None.

