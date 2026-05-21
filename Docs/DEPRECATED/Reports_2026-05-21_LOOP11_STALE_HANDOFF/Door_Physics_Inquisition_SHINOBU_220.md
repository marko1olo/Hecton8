# Door Physics Inquisition - SHINOBU_220

- Generated UTC: 2026-05-20T22:24:34.4422764Z
- Domain: ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT
- Physical door mention files: 3
- Collider door mention files: 1
- Transform/Animator door mention files: 1
- Owned route physics hits: 0

Verdict: SHINOBU-owned runtime route is compliant when owned route physics hits remain 0. Wider door mentions are inventory for neighboring legacy files, not authority for emergency bulkhead closure.

Cinematic cheat: `BaseAirlock` publishes a typed intent, `BulkheadContainmentRuntime` maintains CSR/KCC mathematical closure planes, and `Hecton8_UberNoir` deforms the visual panel in shader. No GameObject door body, collider door slab, or Animator state machine is required for the SHINOBU-owned emergency seal.

Files:
- `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs` collider=false transformMotion=false ownedRuntime=true line=516 snippet=`throw new FatalArchitectureException("SHINOBU_220 bulkhead DTO layout mismatch.");`
- `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs` collider=false transformMotion=false ownedRuntime=true line=115 snippet=`[Tooltip("Amber color shown while emergency bulkhead lockdown overrides player control.")]`
- `Assets/_Project/Scripts/Gameplay/SealedDoor.cs` collider=true transformMotion=true ownedRuntime=false line=77 snippet=`[Tooltip("Can the door be cut? Set to false for permanently sealed doors.")]`
