# WORLD_RESOURCE_SPAWNER Rationale

Status: PENDING VERIFICATION
Mandates loaded:
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## 2026-05-12 - Preflight
Problem: The XML extraction initially failed because the batch tag includes attributes after `id`, while the first regex expected the closing `>` immediately after the id.
Solution: Use an attribute-aware CLI regex over `Docs/Tasks/CURRENT_BATCH.md`, isolate only `WORLD_RESOURCE_SPAWNER`, and discard neighboring prompt content from decision scope.
Rejected Alternatives: IDE tab context and broad MCP-style reads were rejected because batch prompts can truncate or leak neighboring agent tasks.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process control, not runtime.
Hardware Impact: Estimated 0 us runtime gain on i3/MX350; prevents architecture contamination.

Problem: The ore work crosses deterministic placement, terrain projection, render instancing, interaction hydration, save deltas, and telemetry.
Solution: Load eight mandates covering zero-GC, native jobs, deterministic RNG, AUP, MapMagic projection, SoA resource layout, binary save delta rules, and blackbox telemetry.
Rejected Alternatives: Loading the entire mandate registry was rejected as high-noise. Loading only RNG was rejected because the task also requires rendering, save, and interaction boundaries.
Scalability potential: Low uses reduced ore iterations and cheap projection; Middle uses full sector masks; High adds denser dormant render instances; Ultra spends saved cycles on richer visual overkill while keeping authority deterministic.
Hardware Impact: Expected gain is from replacing thousands of ore GameObjects with SoA + indirect rendering; exact microseconds remain PENDING VERIFICATION until compile/profile.
