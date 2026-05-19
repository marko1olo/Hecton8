# Rationale_SHINOBU_141

Status: PENDING VERIFICATION

## Initial Mandate Selection
Problem: SOA inventory routing crosses runtime DTO layout, native jobs, AUP, typed signal, and telemetry domains.
Solution: Read and bind implementation to DATA_Inventory_Resources_Items_SOA_Layout, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem.
Rejected Alternatives: Starting from PlayerInventory only would miss DataVault/generation/disposal boundaries and would likely create another local native heap.
Scalability potential: Low uses bounded time-sliced resource scans; middle increases per-frame slot window; high/ultra spend saved CPU on presentation signals and richer editor telemetry, not gameplay truth bloat.
Hardware Impact: Expected low-end i3/MX350 gain comes from replacing object/list scans with contiguous 32-byte records and Burst linear reads. Static estimate before profiling: removes managed GC spikes and converts 100k-slot scan from pointer-chasing to streaming memory access.

## Global Authority Constraint
Problem: Task requests GlobalDataVault ownership, but adding or changing global routes requires owner/phase/cadence/failure-mode evidence.
Solution: Reuse existing vault/signal APIs if present; if missing, implement owner-local compile-safe inventory infrastructure and document the absent integration instead of inventing cross-domain surface.
Rejected Alternatives: Adding new GlobalRegistry slots or catch-all EventBus routes would violate authority boundaries and create merge conflicts with 20+ agents.
Scalability potential: Owner-local buffers can later be promoted to vault handles without changing DTO layout or job kernels.
Hardware Impact: Avoids extra indirection and registry polling in hot paths on weak CPUs.
