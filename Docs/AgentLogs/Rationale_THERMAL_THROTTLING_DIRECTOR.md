# Rationale_THERMAL_THROTTLING_DIRECTOR

Status: PENDING VERIFICATION

## Decision 0: Domain Boundary and Mandate Selection
Problem: Thermal throttling must cross hardware, rendering, foveated simulation, haptics, telemetry, and UI boundaries without concrete cross-domain dependencies.
Solution: Use GlobalRegistry-owned interfaces for immediate commands and typed signal structs for broadcast state changes. Thermal polling cadence is FrostTick only, with hot paths reading cached bytes.
Rejected Alternatives: A classic BatteryManager singleton and per-frame SystemInfo/Android polling were rejected because they violate registry and zero-GC mandates.
Scalability potential: Low uses render scale and static distant simulation; Middle restores VFX gradually; High keeps richer foveated thresholds; Ultra can trade saved cycles into denser visible VFX while keeping thermal rollback.
Hardware Impact: i3/MX350 and Quest avoid OS-level downclock spikes by preemptively shedding VFX/render scale/tick cadence. Estimated saved cost is workload-dependent; initial target is 500-3000 us during thermal pressure.
