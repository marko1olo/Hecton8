# Status_ACOUSTIC_PORTAL_PROPAGATION

Agent: DSP_ACOUSTIC_LEAD  
Prompt: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="ACOUSTIC_PORTAL_PROPAGATION">`  
Domain: Echelon 8 DSP Acoustic Radar / Audio Propagation  
Task count: 19  
Status policy: PENDING VERIFICATION until Unity/Burst evidence exists.

## Setup
- [x] Prompt extracted | DOD: PowerShell raw-read regex captured only the full `ACOUSTIC_PORTAL_PROPAGATION` XML block from cover to cover | Alternative rejected: MCP/basic read because batch files can truncate or bleed neighboring prompts | Estimate: 900 us
- [x] Domain loaded | DOD: `Docs/Actual Domains of Project.txt` read; acoustic work mapped to Echelon 8 DSP Acoustic Radar / perception | Alternative rejected: editing outside assigned audio propagation boundary | Estimate: 600 us
- [x] Mandates selected | DOD: 8 task-relevant mandate files loaded before coding: acoustic occlusion, DSP SPSC, zero-GC, native jobs, blackbox telemetry, AUP, GlobalRegistry, cinematic fake-first | Alternative rejected: relying on AGENTS summary only | Estimate: 1500 us
- [x] Existing code inventory | DOD: read `SpatialAudioManager`, `IAudioService`, `AbsoluteUniversePosition`, `AcousticOcclusionUtility`, `VoxelDynamicNavGridRuntime`, `HabitatGraphManager`, `ConstructionManager`, and asmdefs | Alternative rejected: inventing APIs or direct dependencies on unknown systems | Estimate: 2600 us

## Primary Tasks
- [ ] 1. SINGLETON ERADICATION | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 2. SIGNAL MIGRATION | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 3. ASMDEF ISOLATION | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 4. DEAD CODE HUNT | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 5. HABITAT SOUND GRAPH | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 6. VOXEL CAVE GRAPH | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 7. BURST PATHFINDING (`AcousticPathJob`) | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 8. DISTANCE DELAY | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 9. CORNER DIFFRACTION | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 10. VIRTUAL SOURCE PROJECTION | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 11. BULKHEAD LOW-PASS | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 12. ROOM REVERB COUPLING | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 13. AUP SHIFT SAFETY | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 14. MAX NODES LIMIT | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 15. REPROJECTION CACHE | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 16. ZERO-GC | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 17. MATH LOD (THE DEAR LIE) | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 18. TELEMETRY | Justification pending | Alternative rejected: pending | Estimate: pending
- [ ] 19. OMEGA COMPILE CHECK | Justification pending | Alternative rejected: pending | Estimate: pending

## Iteration Log
- Loop 0: Prompt, domain, and mandates loaded. No source code edited yet.
- Loop 1: Existing APIs inventoried. Confirmed `GlobalRegistry.Audio` service boundary, no `AcousticManager.Instance`, voxel macro portal route API, and habitat CSR graph with private module positions. Next: isolated Burst propagation kernel plus read-only adapters.
