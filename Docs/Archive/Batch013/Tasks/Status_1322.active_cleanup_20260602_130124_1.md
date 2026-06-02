# Status 1322 - MEMORY_SOVEREIGN_FLUID_ENGINE_EXORCIST

Status hygiene: active. Original assignment source is `Docs/Tasks/CURRENT_BATCH.md`, `<AGENT_PROMPT id="1322">`; root `current_batch.md` was absent.

Domain: Fluid Engine / Physics memory sovereignty.
Primary target: `Assets/_Project/Scripts/HectonFluidEngine.cs`.
Task count: 20.

## Mandates Selected Before Coding

- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt | DOD: DataVault-owned native state; hot paths use handles/views only. | Rejected: local persistent NativeArray fields.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt | DOD: no hot allocations; vault handles for native buffers. | Rejected: TempJob/persistent local substitutes.
- [x] DATA_Runtime_Struct_Layout_ARM64.txt | DOD: explicit runtime DTO layout, 8-byte multiple. | Rejected: Sequential/Pack=1 runtime DTOs.
- [x] PHYS_Fluid_Incursion_Interior.txt | DOD: compartment gameplay state plus presentation fake. | Rejected: invisible continuous fluid truth.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt | DOD: cached dependencies; registry cold only. | Rejected: hot GlobalRegistry polling.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt | DOD: 300-frame 64B telemetry ring and dump path. | Rejected: managed string logs as proof.
- [x] OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt | DOD: fake-first water/pressure presentation. | Rejected: per-volume internal simulation without player-visible truth.

## Loop 1 - Tasks 01-05

- [x] Task 01: EXHAUSTIVE_PRIMARY_TARGET_INQUISITION | DOD: strict field scanner ledger `Docs/Reports/VAULT_EXORCISM_LEDGER_1322_BEFORE.json` found exactly 39 persistent native fields. | Rejected: raw grep-only mutation without line/type ledger. | Estimate: -unbounded crash risk, ~0 us steady state.
- [x] Task 02: OWNERSHIP_PROVENANCE_AND_LIFECYCLE_MAPPING | DOD: all 39 mapped to `SystemID.Fluid` with `BufferID` range `1322000-1322038`; telemetry/cursor `1322039-1322040`. | Rejected: shared generic physics owner IDs. | Estimate: prevents relocation fault, ~0 us hot path.
- [x] Task 03: DEPENDENCY_GRAPH_IMPACT_ANALYSIS | DOD: public read accessors now resolve read-only vault views through `FluidVaultBuffer.AsReadOnly()`. | Rejected: storing raw aliases for UI/readback consumers. | Estimate: no GC, one handle resolve per read accessor.
- [x] Task 04: DTO_LAYOUT_EXTRACTION_AND_VERIFICATION | DOD: fluid DTOs used by migrated buffers are explicit or primitive; new `FluidTelemetryEntry` is explicit 64B. | Rejected: Sequential telemetry DTO. | Estimate: ARM64 aligned; no frame cost.
- [x] Task 05: TELEMETRY_RING_INTEGRATION_PLANNING | DOD: planned and implemented 300-row ring, cursor, dump path `Docs/AgentLogs/Dump_1322_FluidEngine.bin`. | Rejected: managed log strings as black box. | Estimate: telemetry write only on events/faults.

## Loop 2 - Tasks 06-10

- [x] Task 06: VAULT_DESCRIPTOR_SUBSTITUTION | DOD: zero direct persistent `NativeArray` fields remain in `HectonFluidEngine.cs`; replaced with `FluidVaultBuffer<T>` descriptor wrappers. | Rejected: keeping private arrays behind comments. | Estimate: eliminates stale pointer lifetime.
- [x] Task 07: COLD_BOOT_BUFFER_REGISTRATION | DOD: `Ensure(...)` routes all migrated buffers through `GlobalDataVault.EnsureGenerationHandle<T>`. | Rejected: `new NativeArray<T>(Allocator.Persistent)`. | Estimate: cold allocation only.
- [x] Task 08: PHASE_LOCAL_VIEW_RESOLUTION | DOD: wrapper resolves current views at access time and refreshes stale generation handles. | Rejected: cached `NativeArray<T>` alias fields. | Estimate: handle resolve cost, no GC.
- [x] Task 09: IRONCLAD_TRY_FINALLY_LOCKING | DOD: write-lock mutation path uses `TryAcquireWriteLock` and releases in `finally`; telemetry records contention. | Rejected: unchecked raw writer alias. | Estimate: fault branch only telemetry, hot setter lock cost accepted as bridge.
- [x] Task 10: BURST_JOB_SIGNATURE_RECONCILIATION | DOD: job structs still receive transient `NativeArray<T>` views through implicit local resolution, not handles. | Rejected: passing handles into Burst kernels. | Estimate: no extra job allocation.

## Loop 3 - Tasks 11-15

- [x] Task 11: READ_ACCESSOR_PURIFICATION | DOD: read properties use `TryReadOnlyHandle` via `AsReadOnly()`, no grow, no scene search, no job completion. | Rejected: read accessor allocation/registration. | Estimate: pure resolve only.
- [x] Task 12: EXPLICIT_DTO_REFACTORING | DOD: new telemetry DTO explicit; validator checks primary fluid DTO sizes. | Rejected: changing unrelated contract DTOs without violation. | Estimate: 64B ring entries.
- [x] Task 13: SCALABILITY_WEIGHT_PRESERVATION | DOD: existing continuous `GlobalQualityWeight` math retained; telemetry records quality scalar. | Rejected: low/high binary switch. | Estimate: no additional branch tiering.
- [x] Task 14: TELEMETRY_RING_IMPLEMENTATION | DOD: `FluidTelemetryEntry` ring/cursor in DataVault, records resolve success, contention, non-finite force/torque, dump events. | Rejected: managed list/string telemetry. | Estimate: fault/event only.
- [x] Task 15: BLACKBOX_DUMP_ROUTING | DOD: non-finite force/torque triggers raw binary dump to `Docs/AgentLogs/Dump_1322_FluidEngine.bin`. | Rejected: background pointer export across vault alias lifetime. | Estimate: cold fault path only.

## Loop 4 - Tasks 16-20

- [x] Task 16: BROAD_DOMAIN_CONFLICT_CHECK | DOD: `git status --short` before sweep showed only own target/new validator in physics scope. | Rejected: touching unrelated active files. | Estimate: no runtime effect.
- [x] Task 17: UNCONTESTED_FILE_EXORCISM | DOD: fluid/buoyancy scope scan found zero persistent native field violations; no sibling edit needed. | Rejected: fake churn in clean files. | Estimate: no runtime effect.
- [x] Task 18: ARM64_ALIGNMENT_VALIDATOR_INTEGRATION | DOD: added `Assets/_Project/Scripts/Physics/Editor/FluidMemorySovereigntyValidator1322.cs`. | Rejected: doc-only layout claim. | Estimate: editor-only.
- [x] Task 19: ZERO_GC_HOT_PATH_VERIFICATION | DOD: modified hot paths contain no `new NativeArray`, no LINQ, no string formatting; dumps remain cold fault paths. | Rejected: hidden persistent local collection. | Estimate: no GC in primary frame path.
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: `Docs/Reports/VAULT_EXORCISM_REPORT_1322.json` reports before=39, after=0, audited SHA-256 list. | Rejected: chat-only proof. | Estimate: no runtime effect.

## Compile / Verification Attempts

- [x] Static scan: `HectonFluidEngine.cs` persistent native field count = 0.
- [x] Static scan: fluid/buoyancy physics scope persistent native field count = 0.
- [x] Build gate checked before compile: CPU 19.09% / 15.74%, no `dotnet` or `csc`.
- [ ] Compile: `dotnet build Hecton8.Core.csproj --no-restore` failed before fluid validation on unrelated dependency `Assets/_Project/Scripts/SubmarineStructuralGrid.cs(2248,71): DamageControlTelemetryEntry` missing.
- [x] Revalidation 2026-05-26: status/rationale and 1322 XML block re-read after repeated directive.
- [ ] Rebuild retry blocked: CPU sampled at 100%/100% and active `dotnet` processes exist.
