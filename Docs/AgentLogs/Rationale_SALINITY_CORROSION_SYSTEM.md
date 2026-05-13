# SALINITY_CORROSION_SYSTEM Rationale

STATUS: PENDING VERIFICATION

## Intake

Problem: Items currently have no verified salinity-driven degradation path in the extracted assignment.
Solution: Treat durability as S.O.A. inventory state updated on FrostTick by a Burst-compatible kernel and broadcast typed signals for consumers.
Rejected Alternatives: MonoBehaviour item polling, classic ItemDurabilityManager singleton, material swaps, and string event names; all violate AGENTS hot-path and registry rules.
Scalability potential: Low uses one scalar rust blend and 5s FrostTick decay; Middle adds richer shader scratches; High adds extra detail map response; Ultra can spend saved CPU on denser first-person material detail.
Hardware Impact: i3/MX350 target is one contiguous pass over fixed slots, estimated under 25 us for 40 slots and 0 B GC if existing architecture permits direct NativeArray ownership.

Problem: Multiple agents are modifying adjacent systems.
Solution: Use contracts, GlobalRegistry interfaces, and typed EventBus signals where existing seams exist; avoid concrete cross-domain references unless already established in code.
Rejected Alternatives: Direct calls into biome, audio, HUD, or save classes from corrosion logic.
Scalability potential: Signal-based consumers can load-shed independently by tier.
Hardware Impact: Avoids per-frame polling and cache-missing manager chains on low-end silicon.
