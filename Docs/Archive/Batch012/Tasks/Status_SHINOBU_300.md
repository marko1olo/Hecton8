# SHINOBU_300 Status

Agent: SHINOBU_300
Domain: ECOSYSTEM_MACRO_BALANCER / Echelon 3 Ecosystem Director (Macro)
Source Prompt: Docs/Tasks/CURRENT_BATCH.md `<AGENT_PROMPT id="SHINOBU_300">`
Task Count: 20
Status: STATIC PATCH APPLIED; TASK 15 EXACT BURST TIMING AND TASK 20 RUNTIME/COMPILE PROOF STILL PENDING; DOTNET BUILD GATED BY ACTIVE COMPILER/CPU POLICY

## Mandates Read
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Iterative Loops
- Loop 1 Tasks 01-05: archaeology/interface route implemented; static rg verification complete.
- Loop 2 Tasks 06-10: Burst mock data, LV kernel, clamps, diffusion, and presentation scalars implemented; stale-field rg verification complete.
- Loop 3 Tasks 11-15: continuous quality substeps, AUP sector hashing, deterministic layout, uninitialized Vault buffers, telemetry dump path implemented; layout self-audit hook added.
- Loop 4 Tasks 16-19: editor tuner/gizmo adapted, CSV route retained, scanner/report added; `git diff --check` clean.
- Loop 5 Task 20: self-audit routine added; dotnet build not launched because active compiler server PID 6776 was present.
- Loop 6 Polish: canonical macro sector DTO moved to `Hecton8.Core.Contracts.EcosystemSectorDTO`; producer/consumers now share one DataVault generic type; SHINOBU_300 report path isolated from neighboring `AI_OPTIMIZATION_REPORT.json`.
- Loop 7 Polish: `EcosystemDirector.TryGetBiomassAvailability` read path made pure by moving Vault handle refresh to cold/owner phases and replacing fallback slot creation with read-only lookup; macro AUP hash route now uses absolute `X/Z` with intentional `Y=0` horizontal biomass layer.
- Loop 8 Polish: `OOP_Spawner_Scanner` now emits SHINOBU_300 canonical report fields and runs a lightweight syntax-tree pass over type/method/invocation nodes without adding Roslyn/editor assembly dependencies.
- Loop 9 Polish: removed unused `Hecton8.World` namespace import from macro runtime and clarified static `float3` read bridges as legacy absolute-meter adapters; primary route remains AUP/contract/Vault.
- Loop 10 Polish: `MacroEcosystemTunerWindow` now exposes alpha/beta/delta/gamma sliders, mutates `MacroEcosystemTuningDTO` in-place through `UnsafeUtility.AsRef`, and renders a stacked area chart instead of line traces.
- Loop 11 Polish: CSV route retargeted to `macro_ecosystem_coefficients.csv` with legacy fallback; `BiomeEcosystemSpecDTO` expanded to 64B and stores optional per-biome alpha/beta/delta/gamma overrides.
- Loop 12 Polish: `ShinobuEcosystemBalancer` legacy 32B `LotkaVolterraMacroJob` is now fallback-only when canonical `ShinobuMacroEcosystemSectorFront` exists; scanner writes both stable and aggregate AI optimization report surfaces.

## Verification
- XML prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` after implementation pass.
- `rg` stale macro fields: no `sector.LocalTemperature`, `sector.ToxinLevel`, old telemetry fields, or contract snapshot readers remain in macro/consumer scope.
- `rg` contract route: no `Hecton8.Ecosystem.EcosystemSectorDTO`, no hot `VaultGenerationHandle<MacroEcosystemSectorVaultRecord>`, no hot `NativeArray<MacroEcosystemSectorVaultRecord>`.
- `rg` read-purity route: macro Vault handle refresh remains in `RefreshMacroEcosystemVaultHandlesCold`; `TryResolveMacroEcosystemVaultSnapshot` only reads cached descriptors; public sector/biomass reads now use `TryResolve*SlotReadOnly` helpers.
- `rg` AUP route: runtime and World consumer hash macro biomass from absolute AUP `X/Z` and `SectorY=0`, matching `StressDrivenSpawnDirector`.
- Scanner route: generated JSON path remains `Docs/Reports/SHINOBU_300_AI_OPTIMIZATION_REPORT.json`; scanner output preserves canonical DTO, read-purity, and AUP hash fields.
- Scanner proof: JSON records 1685 scanned files, 43 candidate files, 0 Instantiate hits, 0 coroutine hits, 1 non-authority `EcosystemHealthDirector` dictionary hit, and 0 macro truth violations.
- `rg` OOP macro scope: 0 `Instantiate`, 0 coroutine simulation loops, 1 documented non-macro `EcosystemHealthDirector` dictionary.
- `git diff --check`: pass; only line-ending warnings.
- Compile-wall scan: macro runtime no longer imports `Hecton8.World`; `World/EcosystemDirector` still has pre-existing `Hecton8.Ecosystem` usage for `MigrationDirector`, not a new SHINOBU_300 dependency.
- Build: NOT RUN. Latest gate sample showed active `dotnet` PID 1548 and CPU 100%; project rule forbids launching dotnet build while another dotnet/csc process is running or CPU is above 50%.
- Latest compile gate sample showed active `VBCSCompiler` PID 2036; no build/rebuild launched.
- Latest post-patch CPU gate sampled `_Total Processor Time = 94.4%`; build/rebuild still blocked even though no dotnet/csc/VBCSCompiler process was present.
- Task 16 static proof: tuner has `PredatorConversionRate`, `UnsafeUtility.AsRef`, and `DrawStackedArea`.
- Task 17 static proof: primary CSV name is `macro_ecosystem_coefficients.csv`, parser still uses `ReadOnlySpan<byte>`, and biome spec coefficient offsets are asserted at 24/28/32/36.
- Task 19 static proof: `OOP_Spawner_Scanner` writes stable `SHINOBU_300_AI_OPTIMIZATION_REPORT.json` and upserts `shinobu300MacroEcosystem` into `AI_OPTIMIZATION_REPORT.json`.
- One-owner proof: `ShinobuEcosystemBalancer.RunMacroBiomassPass` exits when `ShinobuMacroEcosystemSectorFront` generation exists, demoting `ShinobuEcosystemSectors` to fallback-only.
- Self-audit artifact: `Docs/Reports/SHINOBU_300_SELF_AUDIT.xml`.

## Checklist
- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN | DOD: rg archaeology found MacroEcosystemMathematicianRuntime, World/EcosystemDirector, StressDrivenSpawnDirector, FaunaDirector, ShinobuEcosystemBalancer, and no macro Instantiate loop | Rejected: duplicate class creation before scan | Est: 120 us saved per FrostTick by avoiding competing manager dispatch
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE | DOD: existing MacroEcosystemMathematicianRuntime made partial; SHINOBU300 self-audit isolated in separate partial file | Rejected: new MonoBehaviour simulation owner | Est: 35 us saved by keeping one Vault route
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION | DOD: no new hot signal lane added; consumers use Vault/read facade | Rejected: GlobalSignals/EventBus hot biomass broadcast | Est: 8 us saved per consumer tick
- [x] Task 04 MONOBEHAVIOUR_SPAWNER_INQUISITION | DOD: rg scanner shows no Instantiate/coroutine macro-simulation hit in ecosystem/fauna/world macro scope | Rejected: deleting presentation-only visual/hydration systems outside domain | Est: 400+ us avoided versus GameObject biomass spawns
- [x] Task 05 DICTIONARY_BASED_SECTOR_TRACKING_PURGE | DOD: macro truth kept in flat NativeArray sector buffer; one residual EcosystemHealthDirector infection map documented as non-macro | Rejected: Dictionary<Vector3Int,* > macro biomass authority | Est: 60 us saved from hash-map traversal
- [x] Task 06 EMERGENCY_MOCK_LOTKA_VOLTERRA_DATA | DOD: mock sector initializer writes flora/prey/predator/capacity distributions with unstable buckets | Rejected: waiting for authored biome data | Est: 0 us runtime saved, removes integration blocker
- [x] Task 07 BURST_DIFFERENTIAL_EQUATION_KERNEL | DOD: EcosystemPopulationJob integrates flora/prey/predator LV/logistic equations over flat pointers | Rejected: MonoBehaviour Update integration | Est: 250 us saved versus managed loop
- [x] Task 08 BIOMASS_CARRYING_CAPACITY_LIMITER | DOD: finite clamps and carrying-capacity sanitizers bound all species masses | Rejected: unbounded differential growth | Est: crash prevention, no direct us claim
- [x] Task 09 BURST_ADJACENT_SECTOR_DIFFUSION | DOD: BiomassDiffusionJob migrates flora/prey/predator by adjacent sector gradients | Rejected: entity migration GameObjects | Est: 300 us saved at 4k sectors
- [x] Task 10 THE_DEAR_LIE_PRESENTATION_SCALARS | DOD: DominantSpeciesMask packs dominant bit plus density bytes for cheap consumers | Rejected: spawning fish from macro truth | Est: 20 us saved per visual consumer scan
- [x] Task 11 CONTINUOUS_SCALABILITY_INTEGRATION_STEPS | DOD: GlobalQualityWeight resolves continuous 1-6 LV substeps and diffusion cadence | Rejected: binary low/high switch | Est: low-tier saves ~80% LV substep work
- [x] Task 12 AUP_PRECISION_SECTOR_HASHING | DOD: double/int64 sector coord resolver used for Vault lookups and diffusion distance | Rejected: float absolute coordinate hash | Est: precision fault prevention
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE | DOD: explicit 64B/16B layouts and deterministic Burst float mode retained | Rejected: platform-variable managed state | Est: deterministic audit route, no direct us claim
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD: existing UninitializedMemory Vault acquire route retained for sector/front/back/remainder buffers | Rejected: redundant MemClear | Est: ~70 us saved on cold sector buffer allocation
- [ ] Task 15 TELEMETRY_ECOSYSTEM_RECORDER | DOD: 300-entry ring records flora/prey/predator/capacity, diffusion transfers, density, substeps, flags and dumps Dump_SHINOBU_300.bin on fault | Remaining: exact Burst-only execution time needs dispatcher/profiler proof; current code patches schedule-to-finalize elapsed time | Rejected: unknown-crash reporting | Est: debug time saved, no frame us claim
- [x] Task 16 MACRO_ECOSYSTEM_TUNER_WINDOW | DOD: editor graph reads telemetry as stacked area, exposes alpha/beta/delta/gamma sliders, and writes tuning via `UnsafeUtility.AsRef` | Rejected: recompilation for tuning visibility and copy/write DTO facade | Est: editor-only
- [x] Task 17 CSV_ECOSYSTEM_PROFILES_INGESTOR | DOD: cold `ReadOnlySpan<byte>` parser targets `macro_ecosystem_coefficients.csv`, stores capacity plus optional alpha/beta/delta/gamma in 64B biome spec rows, and keeps legacy CSV fallback | Rejected: string split/float.Parse in FrostTick | Est: zero hot-path allocation
- [x] Task 18 LIVE_BIOMASS_DEBUG_GIZMO | DOD: gizmo colors biomass densities from Vault without debug GameObjects | Rejected: scene debug spawns | Est: editor-only, avoids transform churn
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: OOP_Spawner_Scanner writes stable SHINOBU report and aggregate `AI_OPTIMIZATION_REPORT.json` section; legacy AI macro job demoted to canonical-buffer fallback | Rejected: prose-only eradication claim or clobbering shared aggregate report | Est: proof artifact, no runtime work
- [ ] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: self-audit hook, XML artifact, rg stale-field scan, rg OOP scan, contract TypeHash scan, diff-check targeted; dotnet build gated by active compiler/CPU policy | Remaining: Unity import/Play Mode/profiler/GC proof | Rejected: unverified completion claim | Est: prevents hidden layout/NaN failures
