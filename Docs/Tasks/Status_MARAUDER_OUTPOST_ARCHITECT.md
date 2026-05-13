# Status - MARAUDER_OUTPOST_ARCHITECT

Authority: CURRENT_BATCH.md AGENT_PROMPT id="MARAUDER_OUTPOST_ARCHITECT"
Role: HABITAT_ARCHITECT
Domain: ECHELON 6 - HABITAT & VEHICLES
State: PENDING VERIFICATION

## Prompt Extraction

- [x] Extracted XML prompt from Docs/Tasks/CURRENT_BATCH.md using PowerShell raw regex. DOD: strict tag isolation. Alternative rejected: editor/MCP partial reads. Estimate: 40 us parse after disk read.

## Mandates Loaded

- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt. DOD: no singleton BaseGenerator; service registration through GlobalRegistry. Alternative rejected: BaseGenerator.Instance. Estimate: 0 us runtime policy.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt. DOD: no managed allocation in Tick/render paths. Alternative rejected: managed WFC arrays and LINQ. Estimate: 0 B/frame target.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt. DOD: NativeArray SoA, Burst jobs, tracked lifecycle. Alternative rejected: managed jagged grid. Estimate: solver target under 250 us low tier.
- [x] MATH_Deterministic_RNG_SlotMachine.txt. DOD: hash-derived deterministic LCG seed. Alternative rejected: UnityEngine.Random/System.Random. Estimate: 1-3 us seed/mask generation.
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt. DOD: AUP shift applies to native matrix data, not Transform.position. Alternative rejected: scene-wide GameObject shift for shell. Estimate: 10-40 us per shift for matrix pool.
- [x] REND_GPU_Sovereignty.txt. DOD: shell rendered by GPU buffers/indirect path; no 500 wall GameObjects. Alternative rejected: prefab wall instantiation. Estimate: CPU draw overhead reduced from hundreds of renderer submissions to one family submit.
- [x] VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt. DOD: heightmap adaptation through MapMagic/global data contract. Alternative rejected: hardcoded seabed Y. Estimate: 20-80 us for bottom support pass.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt. DOD: 300-frame blackbox and binary dump path. Alternative rejected: Debug.Log-only failure reporting. Estimate: 0.5-2 us ring write.

## Task Checklist

### Loop 1 - Tasks 1-5

- [ ] Task 1 - SINGLETON ERADICATION: No BaseGenerator.Instance; register IOutpostGenerationService.
- [ ] Task 2 - SIGNAL MIGRATION: Consume SectorHydratedSignal and trigger on FirstBaseHash.
- [ ] Task 3 - ASMDEF ISOLATION: Hecton8.World.Outposts -> Contracts.
- [ ] Task 4 - GRID S.O.A.: 10x10x5 NativeArray<byte> WfcGrid.
- [ ] Task 5 - DETERMINISTIC SEED: LCG_Hash(WorldSeed + FirstBaseHash).

### Loop 2 - Tasks 6-10

- [ ] Task 6 - STRUCTURAL RULES: Burst bitwise adjacency and floor support rules.
- [ ] Task 7 - HEIGHTMAP ADAPTATION: Bottom nodes project to MapMagic/GlobalDataVault height; stilts/pillars generated.
- [ ] Task 8 - MATRIX EXTRACTION: WfcGrid to NativeArray<float4x4>.
- [ ] Task 9 - INDIRECT RENDERING: Dispatch matrices to GPU, zero CPU shell draw loop.
- [ ] Task 10 - INTERACTABLE SPAWNING: Minimal proxy GameObjects only for Datapad/SealedDoor via pool path.

### Loop 3 - Tasks 11-15

- [ ] Task 11 - RUST & WEAR: _OutpostAge01 scalar path for decay shader.
- [ ] Task 12 - AUP SHIFT SAFETY: Native matrix offsets on AupShiftSignal.
- [ ] Task 13 - MATH LOD: Low tier grid constrained to 5x5x3.
- [ ] Task 14 - ZERO-GC: Solver uses Native/TempJob and 0 managed bytes in hot path.
- [ ] Task 15 - OMEGA COMPILE CHECK: WFC constraints use bitwise operations, not managed arrays.

### Loop 4 - Re-Read And Self-Review

- [ ] Re-extract prompt after task 3 cadence.
- [ ] Re-read code for singleton, managed allocation, Instantiate wall, and public API drift.
- [ ] Verify compile and console status.

### Loop 5 - Polish Mandate

- [ ] Read POLISH_MANDATE only after tasks complete or blocked.
- [ ] Append final report to Docs/AgentLogs/LOG_MARAUDER_OUTPOST_ARCHITECT.md.

## Verification Ledger

- Compile status: PENDING VERIFICATION.
- Unity Console status: PENDING VERIFICATION.
- GC proof: measured proof absent.
- Frame/VRAM proof: measured proof absent.
