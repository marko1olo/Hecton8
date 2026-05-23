# SHINOBU_314 Status

Agent: SHINOBU_314
Role: CARRION_DECAY_BIOMASS_SOLVER
Domain: Echelon 3 Ecosystem and AI
Task count: 20
State: STATIC SOURCE POLISHED, EXTERNAL COMPILE WALL

## Mandates Read
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Signal_Lane_Segregation.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt

## Archaeology
- CURRENT_BATCH.md extracted by XML id `SHINOBU_314`; task count = 20.
- Domain file maps the task to Echelon 3: Ecosystem and AI.
- Initial destroy scan in `Assets/_Project/Scripts/AI` and `Assets/_Project/Scripts/Environment`: no delayed creature-destruction cleanup found; only editor/lifecycle texture/mesh cleanup in `OnDestroy`.
- Existing data surface found: `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs` with `NutrientCellDTO`, DataVault handles, slow-tick Burst jobs, telemetry dump path, and `HomeostasisBrain.GlobalQualityWeight`.
- Polish archaeology found `FaunaBrain.Die()` already avoids creature `Destroy`, but local predation/death could bypass carrion unless it publishes `EntityDeathSignal`.
- Task 03 matrix recheck: `SYSTEM_INTERCONNECT_MATRIX.md` delegates route authority to typed `SignalBus<T>`/`GLOBAL_AUTHORITY_BOUNDARIES.md`; `GlobalSignals.cs` configures `SignalBus<EntityDeathSignal>` capacity 64 and validates `EntityDeathSignal` size 64.

## Checklist
- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN - DOD: rg scan over AI/Environment plus Fauna/Gameplay extension; rejected blind implementation. Runtime delayed destroy hits: 0. Estimate saved: 200-2000 us per corpse cleanup cluster by avoiding hierarchy teardown.
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE - DOD: isolated `NutrientDriftRuntime_Carrion.cs` partial; rejected standalone manager. Estimate saved: 20-60 us/frame in registry/polling overhead avoided.
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION - DOD: reused `EntityDeathSignal` from `GlobalSignals`; rejected new death lane. Estimate saved: one queue/snapshot lane and 64B*capacity duplicate memory.
- [x] Task 04 GAMEOBJECT_DESTROY_INQUISITION - DOD: runtime scan found no delayed creature `Destroy` calls; lifecycle texture/mesh cleanup left untouched. Estimate saved: prevents reintroduction, 0 us current direct removal.
- [x] Task 05 MANAGED_CORPSE_LIST_PURGE - DOD: no `List<Corpse>` runtime offenders found; carrion state stored in flat Vault arrays. Estimate saved: avoids future pointer-chasing list scans, O(N) cache miss path rejected.
- [x] Task 06 EMERGENCY_MOCK_MASS_EXTINCTION - DOD: `GenerateMockMassExtinctionJob` creates bounded synthetic carrion/fauna rows. Alternative rejected: waiting for gameplay kills. Estimate: 5000-corpse test without scene objects.
- [x] Task 07 BURST_DEATH_TRANSITION_KERNEL - DOD: `ProcessEntityDeathJob` drains unmanaged death ingress, dedupes active carrion rows by `EntityHash`, and clears local fauna active flags. Alternative rejected: `Destroy()`/MonoBehaviour death timers. Estimate saved: 200+ us per corpse hierarchy event.
- [x] Task 08 BURST_EXPONENTIAL_DECAY_KERNEL - DOD: `CalculateBiomassDecayJob` applies age-based exponential decay and epsilon retirement. Alternative rejected: coroutine timer decay. Estimate saved: 0 B GC and no scheduler coroutine overhead.
- [x] Task 09 ENVIRONMENTAL_TEMPERATURE_MULTIPLIER - DOD: nutrient temperature sample scales decay from cold preservation to hot acceleration. Alternative rejected: state-machine rot zones. Estimate: one cell sample per active corpse per Frost tick.
- [x] Task 10 NUTRIENT_GRID_INJECTION_MATH - DOD: `InjectCarrionNutrientsJob` folds biomass delta into `NutrientCellDTO` injection grid. Alternative rejected: particle/blood cloud logic. Estimate saved: no managed VFX spawn or per-frame object tracking.
- [x] Task 11 THE_DEAR_LIE_SCAVENGER_ATTRACTION - DOD: bounded attraction records publish to `WorldSpatialHashGrid` chemical resource events. Alternative rejected: custom scavenger AI scripts. Estimate saved: per-scavenger polling eliminated.
- [x] Task 12 CONTINUOUS_SCALABILITY_DECAY_APPROXIMATION - DOD: `GlobalQualityWeight` drives smooth linear-to-exponential blend. Alternative rejected: low/high binary tier switch. Estimate: low quality bypasses most exp influence while preserving progression.
- [x] Task 13 AUP_PRECISION_GRID_LOCALIZATION - DOD: corpse AUP minus grid origin in double before float index. Alternative rejected: absolute float cast. Estimate: precision bugs avoided at 100 km map edge.
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE - DOD: Burst jobs use `FloatMode.Deterministic`; spatial attraction is non-authoritative. Alternative rejected: saving/hash spatial acceleration records. Estimate: no false Merkle/state-ring churn.
- [x] Task 15 TELEMETRY_DECAY_RECORDER - DOD: `CarrionTelemetryEntry[300]` and dump path `Dump_SHINOBU_314.bin`. Alternative rejected: log-only fault diagnosis. Estimate: 300-frame postmortem, 0 B hot path allocation.
- [x] Task 16 DECAY_TUNER_EDITOR_WINDOW - DOD: UI Toolkit `BiomassDecayTunerWindow` reads telemetry and writes tuning. Alternative rejected: inspector-only serialized fields. Estimate: editor-only, 0 runtime cost.
- [x] Task 17 CSV_DECAY_PROFILES_INGESTOR - DOD: `ReadOnlySpan<byte>` parser plus `carrion_decay_profiles.csv`. Alternative rejected: `float.Parse`/managed row objects. Estimate: cold boot only, 0 B hot path.
- [x] Task 18 LIVE_ROT_DEBUG_GIZMO - DOD: SceneView `LiveRotDebugGizmo` reads raw carrion DTOs and draws biomass/toxicity bars. Alternative rejected: runtime gizmo MonoBehaviours. Estimate: editor-only, 0 runtime cost.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR - DOD: `OOP_Destroy_Scanner` and JSON reports. Alternative rejected: Roslyn asmdef dependency. Estimate: runtime scan 214 files, 0 delayed-destroy offenders.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION - DOD: `CarrionDecaySelfAudit.BuildSelfAuditXml`, static layout scan, JSON validation, diff whitespace check. Alternative rejected: chat-only report. Estimate: self-audit reports 64B DTO and 71250-71259 buffer route.

## Iteration Log
- Loop 0: preflight only.
- Loop 1: Tasks 01-05 archaeology and purge proof. Static runtime scan found zero delayed corpse `Destroy` and zero `List<Corpse>` offenders in AI/Environment/Fauna/Gameplay runtime paths.
- Loop 2: Tasks 06-10 runtime kernels. Added Vault buffers, mock mass extinction, death transition, exponential decay, temperature multiplier, and nutrient injection.
- Loop 3: Tasks 11-15 route/telemetry. Added spatial hash Dear Lie records, continuous quality blend, AUP double-localization, deterministic Burst attributes, 300-frame telemetry and dump path.
- Loop 4: Tasks 16-19 facades/proof. Added tuner window, CSV profiles, live rot gizmo, scanner, and report JSON.
- Loop 5: Task 20 self-audit/static verification. Ran JSON validation, delayed-destroy rg scan, hot-path LINQ/foreach scan, DTO layout rg, and git diff whitespace check. Full Unity/dotnet compile not launched under project rebuild-protection rule.
- Loop 6: Polish mandate pass. Exposed carrion constants used by external Burst job structs, routed `FaunaBrain.Die()` into `EntityDeathSignal`, and added `EntityHash` dedupe before carrion slot allocation.
- Loop 7: Ultra-polish audit. Fixed low-quality `math.exp` bypass, preserved base decay rate instead of re-multiplying temperature every Frost tick, routed Fauna-owned species through `EntityDeathSignal.SourceHash`, made profile CSV load attempt one-shot until explicit reload, protected aggregate report upsert, prevented unflagged duplicate death signals from overwriting Fauna-owned species profiles, and registered the route in the binary payload ledger.
- Loop 8: Contract drift, RNG, and dependency audit. Promoted the Fauna carrion ownership flag into `EntityDeathSignal.FlagFaunaBrainCarrion`, removed local magic constants in Fauna/Ecosystem consumers, moved mock mass-extinction variation to `Unity.Mathematics.Random.CreateFromIndex`, replaced carrion entity identity with a fauna-local stable hash instead of `CombatDamageRuntime.ResolveTargetId`, and updated scanner/report artifacts to name the central signal contract.
- Loop 9: Mock telemetry polish. Replaced seed-dependent `TotalActiveBiomass = biomass(index0)*count` with the deterministic expected multiplier mean `1.3*DefaultBiomass*count`; real totals are recomputed by the decay job.
- Loop 10: Carrion timing polish. Added `_carrionScheduleTicks` and patched carrion telemetry with the carrion subchain schedule-to-finalize window instead of parent nutrient solver time.
- Loop 11: Compile-wall import trim. Removed unused `Hecton8.Gameplay` import from `NutrientDriftRuntime_Carrion.cs` after moving carrion entity identity off combat target routing.
- Loop 12: Scanner report durability. Extended `OOP_Destroy_Scanner` generated JSON to preserve route, deterministic RNG, mock biomass, telemetry timing, DataMonolith pending, and facade/gizmo proof fields on future editor runs.
- Loop 13: CSV species-key route fix. Found mismatch between Fauna numeric `ComputeStableSpeciesId()` and name-hashed CSV rows; added parser support for `default`, decimal speciesID, `0x` hash, and token FNV plus default fallback profile.
- Loop 14: NaN fault vaccination hardening. Added `CarrionStateDTO.FlagMathFault`, finite checks for AUP/biomass/age/decay/toxicity, sanitized death-ingress biomass/toxicity scalars, current-tick fault clearing, and sanitized nutrient/attraction scalars before injection/hash publication.

## Verification
- PASS: `Docs/Reports/AI_OPTIMIZATION_REPORT.json` and `Docs/Reports/SHINOBU_314_AI_OPTIMIZATION_REPORT.json` parse via `ConvertFrom-Json`.
- PASS: runtime delayed-destroy scan over 214 AI/Environment/Fauna/Gameplay files returned zero findings.
- PASS: `NutrientDriftRuntime_Carrion.cs` scan returned no `new List`, LINQ `.Select/.Where`, or `foreach`.
- PASS: `FaunaBrain.Die()` now emits `EntityDeathSignal` without `Object.Destroy`; `ProcessEntityDeathJob` prevents duplicate corpse DTO rows and refuses to overwrite an existing Fauna-owned row with a generic unflagged duplicate.
- PASS: Fauna death signals now carry species hash through `SourceHash` under `EntityDeathSignal.FlagFaunaBrainCarrion`; carrion ingress resolves that hash without direct fauna-to-ecosystem class coupling.
- PASS: `EntityDeathSignal` remains explicit-layout 64B; adding compile-time flag constants did not change field offsets or payload size.
- PASS: Carrion `EntityDeathSignal.EntityHash` is generated through `ResolveStableFaunaHash(FaunaCarrionDeathHashSalt, 0)`, not Gameplay combat target routing.
- PASS: `NutrientDriftRuntime_Carrion.cs` has no `using Hecton8.Gameplay`; carrion route uses Core signals, Vault memory, and the existing World spatial hash API only.
- PASS: Low `GlobalQualityWeight < 0.4` path skips `math.exp`; mid/high quality uses `math.smoothstep(0.4, 0.95)` to blend toward exact exponential.
- PASS: `GenerateMockMassExtinctionJob` uses unmanaged deterministic `Unity.Mathematics.Random` seeded by `math.hash(seed,index)`; no `UnityEngine.Random`.
- PASS: Mock mass-extinction counters use deterministic expected biomass instead of first-row RNG biomass.
- PASS: `CalculateBiomassDecayJob` preserves base `DecayRate`; temperature multiplier no longer compounds across Frost ticks.
- PASS: `OOP_Destroy_Scanner` now upserts `shinobu314CarrionDecay` into the aggregate report instead of overwriting other agents.
- PASS: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` includes SHINOBU_314 BufferIDs and ABI boundary.
- PASS: `CarrionTelemetryEntry.BurstExecutionMicroseconds` now uses carrion subchain timing rather than the full nutrient solver window.
- PASS: `OOP_Destroy_Scanner` generator now emits `mockCounterBiomass`, `blackBoxTelemetryFrames`, `telemetryTimingRoute`, `dataMonolithRuntimeProof`, and stable source path fields instead of reducing the report to a violation counter.
- PASS: `carrion_decay_profiles.csv` now carries `species_key` rows for `default`, `1001`, and `8001`; parser clears stale profile rows on reload and falls back to default when a fauna species ID has no exact profile.
- PASS: Invalid active carrion rows now retire with `FlagMathFault`; death ingress sanitizes biomass/toxicity scalars before DTO creation; `InjectCarrionNutrientsJob` clears stale `ShinobuCarrionFaultFlags` at tick start and folds only current-pass math faults into telemetry/dump routing.
- PASS: Source brace/preprocessor scan: `NutrientDriftRuntime_Carrion.cs 167/167 0/0`, `FaunaBrain.cs 611/611 5/5`, `GlobalSignals.cs 860/860 7/7`; `OOP_Destroy_Scanner.cs` raw brace scan is string-regex polluted but preprocessor is 1/1 and JSON reports parse.
- PASS: Runtime corpse-destroy re-scan with top-level `#if UNITY_EDITOR` filter returned `RUNTIME_HITS=0`; the only wide-scan `WaitForSeconds` token was an editor-only SHINOBU_319 scanner facade.
- PASS: Carrion default/telemetry constants referenced by external job structs are public and centralized.
- PASS: DTO field-offset static scan confirms `CarrionStateDTO` first mandated offsets and 64-byte explicit layout in source.
- PASS: `git diff --check` for touched files returned only LF/CRLF warnings for already-modified `H8Memory.cs`, `FaunaBrain.cs`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- PENDING: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent; carrion CSV bridge is static/cold source proof, not DataMonolith runtime readiness.
- BLOCKED BY DEPENDENCY: guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was launched when CPU was below policy and no compiler process was active. It failed with 11 errors in unrelated domains: `PredatorCognitionDomain.AcousticSdf.cs`, `RadiationHazardGrid.cs`, `VRSomaticProvider.Comfort.cs`, and `PlayerKinematicsRuntime_HandIK.cs`. No SHINOBU_314 touched file error was emitted before the external compile wall stopped the build.
- GATED: no further build attempt allowed now; latest guard sampled CPU 57% with no active compiler process, still above the 50% launch threshold.
