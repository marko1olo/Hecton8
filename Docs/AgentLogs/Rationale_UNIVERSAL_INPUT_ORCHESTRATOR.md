# Rationale - UNIVERSAL_INPUT_ORCHESTRATOR

Status: PENDING VERIFICATION

## Initial Decisions

Problem: Input domain must support PC, Mac, Steam Deck, Quest VR without hardware-specific gameplay polling.
Solution: Use Unity Input System `InputAction` assets at the Unity boundary, convert once per pre-simulation frame into blittable input state/signal structs and bitmasks, then feed existing determinism/registry/signal infrastructure if present.
Rejected Alternatives: Legacy `UnityEngine.Input`, `InputManager.Instance`, device-name string branches, and per-frame `Gamepad.current` lookup are rejected by CTRL_Device_Abstraction_Haptics and zero-GC mandates.
Scalability potential: Low = direct cached action reads and binary haptic cull; Middle = EWMA gyro smoothing and delta-gated haptics; High = priority haptic blending; Ultra = device-specific trigger/haptic extensions behind adapters only.
Hardware Impact: i3/MX350 target is under 0.20 ms input+haptics budget if reads are cached and haptic writes are delta-gated; current measured proof absent.

Problem: Multiple agents are rewriting adjacent systems.
Solution: Only use existing `GlobalRegistry` interfaces or typed EventBus/signal queues; add contracts by expansion only if source proves no existing owner.
Rejected Alternatives: Direct references to player, submarine, haptics director concrete classes, or scene search wiring.
Scalability potential: Low = no-op fallback if service absent; Middle/High/Ultra = richer providers can register without changing consumers.
Hardware Impact: Interface/queue boundary preserves cache behavior and avoids scene scans on low-end silicon.

Problem: Critical input failure needs post-mortem evidence.
Solution: Push current input scheme hash and high-level frame state into a fixed-size blackbox buffer or existing telemetry owner once discovered.
Rejected Alternatives: Debug.Log spam, unbounded text traces, or exception-driven diagnostics.
Scalability potential: Low = 300-entry scheme/action ring; High/Ultra = extra device/smoothing/haptic state fields if budget allows.
Hardware Impact: 300 compact entries are sub-20 KB scale; no VRAM impact.
