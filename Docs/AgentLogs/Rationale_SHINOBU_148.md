# Rationale_SHINOBU_148

Agent: SHINOBU_148
Domain: EQUIPMENT_THERMAL_AND_BATTERY_GRID
Status: PENDING VERIFICATION

## Decision 00 - Mandate Selection And Ownership Boundary
Problem: Equipment heat and battery math currently may be scattered across individual tool MonoBehaviours, creating per-object update cost and cross-domain coupling risk.
Solution: Use the task-relevant mandates for tool heat/power ownership, ARM64 layout, zero-GC hot paths, native job lifecycle, dispatcher phases, signal lanes, AUP determinism, and power-grid graph boundary before any code generation.
Rejected Alternatives: Reading only AGENTS.md was rejected because SHINOBU_148 explicitly requires registry mandates. Inventing DataVault or Thermodynamic Grid APIs was rejected because 20+ agents are active and cross-domain concrete dependencies are forbidden.
Scalability potential: Low uses reduced cadence and flat 32-byte DTOs; Middle keeps normal cadence; High adds denser telemetry; Ultra spends saved simulation cost on VFX/audio consumers, not heavier gameplay truth.
Hardware Impact: Expected gain on i3/MX350 comes from replacing per-tool MonoBehaviour scalar loops and managed collections with a contiguous Burst O(N) pass. Static estimate pending source scan.

## Decision 01 - Initial Data Contract Target
Problem: Tool state requires deterministic layout for ARM64, Burst, rollback snapshots, and blind MemCpy publication.
Solution: Target `ActiveEquipmentDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]` with raw public fields at mandated offsets and named padding bytes at 24-31.
Rejected Alternatives: Auto-layout structs were rejected because offset drift would break ARM64/cache and netcode snapshot assumptions. Properties were rejected because CS1612 and stack-copy overhead are explicit task failures.
Scalability potential: Same 32-byte truth struct across Low/Middle/High/Ultra; richer visuals consume a read snapshot rather than bloating truth.
Hardware Impact: Four DTOs fit in two 64-byte cache lines; sequential pass reduces L1 miss risk on i3/MX350 versus pointer-chasing MonoBehaviours.
