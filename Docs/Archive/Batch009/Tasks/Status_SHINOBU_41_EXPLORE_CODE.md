# Status - SHINOBU_41_EXPLORE_CODE

Status: PENDING VERIFICATION
Scope: Codebase archaeology only. No source or asset edits.

- [x] Task 1 - Confirm governing docs and domain | Justification: DOD authority spine used: AGENTS.md, domain file, live CURRENT_BATCH SHINOBU_41 block | Alternatives Rejected: ad hoc search without domain boundary | Estimate: 20 us
- [x] Task 2 - Identify relevant mandates | Justification: Selected VOX_MapMagic_Voxel_Seam, VOX_Voxel_SDF, ARCH_Global_Registry, ARCH_Execution_Phases, OPT_Native_Memory, DBG_Telemetry, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC | Alternatives Rejected: relying on AGENTS.md only | Estimate: 30 us
- [ ] Task 3 - Locate MapMagic bridge contracts | Justification: terrain truth must use existing bridge contracts | Alternatives Rejected: inferred SDK calls | Estimate: 50 us
- [ ] Task 4 - Locate voxel/SDF runtime contracts | Justification: SHINOBU_41 needs terrain/voxel seam evidence | Alternatives Rejected: speculative SDF API assumptions | Estimate: 50 us
- [ ] Task 5 - Locate GlobalDataVault/native ownership APIs | Justification: Data sovereignty forbids local native ownership guesses | Alternatives Rejected: new NativeArray ownership in logic | Estimate: 50 us
- [ ] Task 6 - Locate tick/dispatcher/global registry APIs | Justification: decoupled agent integration depends on known lifecycle contracts | Alternatives Rejected: direct singleton references | Estimate: 50 us
- [ ] Task 7 - Locate telemetry/blackbox/editor/tests/asmdef constraints | Justification: compile and crash-forensics risks must be evidence-based | Alternatives Rejected: chat-only risk guesses | Estimate: 60 us
- [ ] Task 8 - Scan assigned domain for Physics.Raycast/MeshCollider terrain sampling violations | Justification: project forbids terrain truth via Physics.Raycast/MeshCollider | Alternatives Rejected: assuming compliance | Estimate: 80 us
