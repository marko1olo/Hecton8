# Rationale - DIEGETIC_DAMAGE_HOLOGRAPHER

Status: PENDING VERIFICATION.

## Decision 0 - Mandate Selection

Problem: Cockpit damage hologram crosses diegetic UX, GPU compute, damage events, telemetry, and MX350 budget constraints.
Solution: Read the registry mandates for diegetic physical UI, zero-GC UI streaming, MX350 compute kernels, GPU ownership, zero-GC policy, frame/VRAM budgets, crash telemetry, and hull damage feedback before code changes.
Rejected Alternatives: Treating it as a normal Canvas HUD was rejected because the prompt explicitly forbids Unity UI Canvas and requires Graphics.DrawMeshInstancedIndirect. A CPU-driven per-frame mesh rebuild was rejected because it violates zero-GC and MX350 frame budget policy.
Scalability potential: Low uses a static dashboard warning icon and no compute; Middle uses 512-point capped point cloud; High uses active scan/flicker with flooding tint; Ultra can spend saved cycles on denser glow, extra scan layers, and higher-frequency visual sync without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding GameObject cube instantiation and CPU readbacks; estimated hot-path CPU savings versus naive CPU mesh/canvas path: 150-500 us PENDING VERIFICATION.
