# INPUT_DETERMINISM_BRIDGE Rationale

Status: PENDING VERIFICATION

## Bootstrap Decisions

Problem: Raw input cadence differs across VR, mouse, Steam Deck, and 60 Hz simulation, creating replay and lockstep desync risk.
Solution: Build a deterministic input snapshot bridge around a fixed 60 Hz tick and unmanaged ring buffer, then publish snapshots through the project signal path.
Rejected Alternatives: Direct `Input.GetAxis()` in consumers was rejected because hardware polling rates leak into gameplay. A Unity singleton input manager was rejected because project authority requires `GlobalRegistry` interfaces.
Scalability potential: Low tier uses identical bit-exact math with no visual overkill because input determinism has no Math LOD. Middle/High/Ultra can spend saved CPU on smoother presentation-only camera interpolation and VR compositor late-latching without changing simulation.
Hardware Impact: Expected gain on i3/MX350 is from removing duplicated consumer polling and managed input string paths; target is under 0.1 ms total and 0 B/frame GC, measured proof absent until Unity profiler/GCMonitor.

