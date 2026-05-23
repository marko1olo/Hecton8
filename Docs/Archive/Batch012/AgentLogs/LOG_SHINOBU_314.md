# LOG_SHINOBU_314

## 2026-05-22 Carrion Decay Biomass Solver
What was wrong: Creature death aftermath had no dedicated unmanaged carrion truth route. Existing scans found no delayed runtime corpse `Object.Destroy` offenders, but the engine still lacked a proof artifact and a biomass decay path from `EntityDeathSignal` into nutrient/scavenger systems.

What was done: Added `NutrientDriftRuntime_Carrion.cs` as an isolated partial over the existing nutrient runtime. Added Vault buffers `71250-71259`, `CarrionStateDTO[5000]`, death ingress ring, tuning DTO, profile DTOs, attraction records, 300-frame telemetry, and fault dump route `Docs/AgentLogs/Dump_SHINOBU_314.bin`. Wired `FrostTick` so carrion death transition runs before biomass decay, decay runs before nutrient advection, and attraction records publish after job completion. Added `BiomassDecayTunerWindow`, `LiveRotDebugGizmo`, `OOP_Destroy_Scanner`, `carrion_decay_profiles.csv`, route card, status, rationale, and JSON report artifacts.

Cinematic Cheats used: The Dear Lie. Carrion is emitted as chemical resource attraction in `WorldSpatialHashGrid` with zero custom scavenger AI. Rot is a data field and nutrient scalar, not a physical particle/blood cloud simulation.

Exact microseconds saved: No exact profiler timing was produced because build/profiler execution was gated. Static expected savings: 200-2000 us per corpse cleanup cluster by avoiding Unity Transform hierarchy delayed destruction; 20-60 us/frame avoided by not adding a hot global manager/polling path; 0 B managed allocation in carrion runtime hot paths by static scan for LINQ/foreach/list allocation.

Verification: `ConvertFrom-Json` passed for both AI reports. Runtime delayed-destroy scan over 214 AI/Environment/Fauna/Gameplay files returned zero findings. Carrion runtime scan returned no `new List`, LINQ `.Select/.Where`, or `foreach`. DTO layout source scan confirms 64-byte explicit DTOs and mandated `CarrionStateDTO` offsets. `git diff --check` produced only an LF/CRLF warning for already-modified `H8Memory.cs`. Full C# build was not launched: CPU sample was 92.5% with active `csc:11364` and `dotnet:12700`, which violates the project rebuild gate.

## 2026-05-22 Carrion Polish Pass
What was wrong: The carrion partial had a compile-risk boundary: external Burst job structs referenced carrion flags/default constants through `NutrientDriftRuntime`, while several constants were private. `FaunaBrain.Die()` also avoided `Destroy` but did not reliably publish the shared death signal for local predation/death paths, so some dead creatures could remain presentation-only instead of becoming carrion DTOs.

What was done: Promoted required carrion constants to public const fields and routed default tuning/sanitization through those fields. Added `FaunaBrain.PublishCarrionDeathSignal()` at the death edge, publishing `EntityDeathSignal` without `Object.Destroy`. Added `EntityHash` dedupe in `ProcessEntityDeathJob` before carrion slot allocation so combat and fauna death signals cannot create duplicate active carrion rows.

Cinematic Cheats used: Kept corpse presentation as shader/death-spiral fakery while the gameplay resource is a 64B DTO plus spatial chemical attraction record. No ragdoll corpse object or scavenger-specific manager was added.

Exact microseconds saved: No profiler timing; build remained gated. Static expected saving from dedupe is one avoided extra 64B carrion row and one avoided extra active-row scan/injection path per duplicate death. Death-edge signal write is cold; no per-frame managed allocation added.

Verification: Carrion hot-path scan still returns no `new List`, LINQ `.Select/.Where`, `foreach`, or runtime managed collection offenders. `FaunaBrain`/carrion targeted scan shows only lifecycle material/texture cleanup `Destroy` calls, not delayed creature corpse cleanup. JSON reports parse. `git diff --check` reports only LF/CRLF warnings for already-modified `H8Memory.cs` and `FaunaBrain.cs`. Full C# build not launched: latest gate CPU 61.1%, active Unity `dotnet` PID 3056.

## 2026-05-22 Carrion Ultra-Polish Pass
What was wrong: Low-quality decay still evaluated `math.exp`, so the advertised ALU shedding was false. `CalculateBiomassDecayJob` stored the temperature-multiplied decay rate back into `CarrionStateDTO`, creating compounding rot-rate drift across Frost ticks. Fauna-owned death signals did not preserve species hash for carrion profile lookup. The scanner could overwrite the shared aggregate AI report.

What was done: Added `math.step(0.4f, GlobalQualityWeight)` gating so low quality skips `math.exp` and `math.smoothstep(0.4f, 0.95f)` blends only above the threshold. Preserved base `DecayRate` in the DTO. Routed Fauna species hash through `EntityDeathSignal.SourceHash` under `EntityDeathSignal.FlagFaunaBrainCarrion`, with carrion ingress resolving `OriginalSpeciesHash` from that route. `ProcessEntityDeathJob` now skips generic unflagged duplicate signals when an active row already exists for the same `EntityHash`, preserving Fauna-owned species profile data. Added one-shot CSV load attempt state until explicit reload. Patched `OOP_Destroy_Scanner` to upsert `shinobu314CarrionDecay` instead of replacing `AI_OPTIMIZATION_REPORT.json`. Added SHINOBU_314 to the binary payload integration ledger.

Cinematic Cheats used: Unchanged. Corpse gameplay truth is DTO biomass plus nutrient/scavenger scalar fields; the visible corpse remains presentation/pool/shader fakery. No corpse physics broadphase or custom scavenger AI loop was introduced.

Exact microseconds saved: No profiler timing. Static ALU saving at low quality is one skipped `math.exp` per active corpse per Frost tick; at 5000 active corpses, 5000 transcendental evaluations are removed. Duplicate death protection avoids one extra 64B carrion row and one extra nutrient/attraction pass per duplicate signal.

Verification: XML prompt re-extracted with the full `<AGENT_PROMPT id="SHINOBU_314" ...>` tag. Carrion runtime hot-path scan found no `Pack=1`, hot properties, LINQ, hot `foreach`, managed list/dictionary allocation, or private native collection ownership. Runtime delayed corpse-destroy scan returned zero findings; only lifecycle texture cleanup remains in nutrient runtime. JSON reports parse. `git diff --check` reports only LF/CRLF warnings for already-modified `H8Memory.cs`, `FaunaBrain.cs`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. Build not launched: latest gate CPU 92.3%, active Unity `dotnet` PID 1680.

<SELF_AUDIT agent="SHINOBU_314" domain="Echelon 3 Ecosystem and AI" taskCount="20">
  <Task id="01" status="PASS" proof="rg archaeology over AI/Environment plus Fauna/Gameplay runtime destroy/corpse patterns"/>
  <Task id="02" status="PASS" proof="NutrientDriftRuntime_Carrion partial; no standalone carrion manager"/>
  <Task id="03" status="PASS" proof="existing EntityDeathSignal lane only; no CreatureKilledSignal"/>
  <Task id="04" status="PASS" proof="no delayed creature Destroy offenders; lifecycle texture/material cleanup left scoped"/>
  <Task id="05" status="PASS" proof="no managed List<Corpse>; carrion rows in Vault buffers"/>
  <Task id="06" status="PASS" proof="GenerateMockMassExtinctionJob writes bounded 5000-row synthetic carrion/fauna rows"/>
  <Task id="07" status="PASS" proof="ProcessEntityDeathJob drains ingress, dedupes EntityHash, preserves Fauna-owned species rows, clears FaunaStateDTO mirror"/>
  <Task id="08" status="PASS" proof="CalculateBiomassDecayJob integrates biomass and clears Active below epsilon"/>
  <Task id="09" status="PASS" proof="temperature sample from NutrientCellDTO scales effective decay without compounding base DecayRate"/>
  <Task id="10" status="PASS" proof="InjectCarrionNutrientsJob folds BiomassLostLastTick into nutrient injection grid"/>
  <Task id="11" status="PASS" proof="CarrionAttractionRecordDTO publishes WorldSpatialHashGrid transient chemical resource"/>
  <Task id="12" status="PASS" proof="GlobalQualityWeight step 0.4 skips exp; smoothstep 0.4..0.95 blends exponential fidelity"/>
  <Task id="13" status="PASS" proof="corpse AUP minus grid origin AUP in double before local float index"/>
  <Task id="14" status="PASS" proof="all carrion jobs use Burst FloatMode.Deterministic"/>
  <Task id="15" status="PASS" proof="CarrionTelemetryEntry[300] plus Dump_SHINOBU_314.bin fault path"/>
  <Task id="16" status="PASS" proof="BiomassDecayTunerWindow UI Toolkit facade mutates Vault tuning"/>
  <Task id="17" status="PASS" proof="ReadOnlySpan<byte> FNV-1a CSV parser into CarrionDecayProfileDTO[64]"/>
  <Task id="18" status="PASS" proof="LiveRotDebugGizmo reads CarrionStateDTO and draws biomass/toxicity bars"/>
  <Task id="19" status="PASS" proof="OOP_Destroy_Scanner structural scanner with aggregate upsert report"/>
  <Task id="20" status="PASS" proof="self-audit, layout scans, JSON parse, diff check, build gate recorded"/>
  <StructLayout name="CarrionStateDTO" size="64" pack1="false" offsets="CorpseAUP:0:24,InitialBiomass:24:4,CurrentBiomass:28:4,OriginalSpeciesHash:32:4,ToxicityEmissionRate:36:4,AgeSeconds:40:4,BiomassLostLastTick:44:4,DecayRate:48:4,Flags:52:4,EntityHash:56:4,_pad0:60:4"/>
  <Scalability low="linear subtraction, exp skipped below 0.4" mid="smoothstep blend" high="exact exponential rot" truth="quality never changes DTO layout, save identity, or route"/>
  <Vault ids="71250,71251,71252,71253,71254,71255,71256,71257,71258,71259" privateNativeArrays="0" owner="SystemID.AIEcology"/>
  <DependencyGraph jobs="ProcessEntityDeathJob->CalculateBiomassDecayJob->InjectCarrionNutrientsJob->RecordCarrionTelemetryJob" noAlias="true" completion="owner LateFrame/teardown fence only"/>
  <CompileGuard siblingRuntimeReference="false" directFaunaToEcosystemClassCoupling="false" build="GATED"/>
  <DearLie before="custom scavenger AI and corpse physics broadphase" after="spatial chemical resource scalar" complexityBefore="O(scavengers*corpses)+PhysX" complexityAfter="O(activeCarrion) Frost-tick bounded records"/>
</SELF_AUDIT>

## 2026-05-22 Carrion Contract Drift Pass
What was wrong: Fauna/Ecosystem ownership routing for species-specific carrion used a duplicated scalar flag value in two domain files. That was ABI drift risk: one producer/consumer edit could silently break profile lookup while the 64B `EntityDeathSignal` payload still looked valid.

What was done: Added `EntityDeathSignal.FlagFaunaBrainCarrion` to the existing Core signal contract. Replaced local magic constants in `FaunaBrain` and `NutrientDriftRuntime_Carrion` with that central compile-time flag. Moved `GenerateMockMassExtinctionJob` variation from a custom hash to `Unity.Mathematics.Random.CreateFromIndex(math.hash(seed,index))`. Updated scanner/report/route artifacts to name the signal contract instead of the scalar value. No DTO field, offset, capacity, or BufferID changed.

Cinematic Cheats used: Unchanged. Carrion remains a DTO biomass/nutrient/chemical-resource proxy; presentation remains visual fakery and does not own gameplay truth.

Exact microseconds saved: Runtime cost is unchanged at 0 additional instructions beyond the existing bit test; the gain is preventing species-profile route drift. Mock RNG is cold/editor stress cost and remains unmanaged. Full compile still gated: latest sample CPU 100.0%, `csc.exe` none, `dotnet` none.

Verification: JSON reports parse. Carrion/Fauna/Core code scan shows no `UnityEngine.Random`, `System.Random`, stale carrion flag constant, or `FlagEcologyCull`. Carrion hot-path scan shows no `Pack=1`, hot properties, LINQ, `foreach`, or managed collection allocation. Runtime corpse-destroy scan with top-level `#if UNITY_EDITOR` exclusion returned `RUNTIME_HITS=0`; a wider token scan only hit an editor-only SHINOBU_319 scanner facade.

## 2026-05-22 Carrion Dependency Trim Pass
What was wrong: The carrion publisher in `FaunaBrain` used `CombatDamageRuntime.ResolveTargetId(gameObject)` to derive corpse entity identity. That tied carrion identity to Gameplay combat target routing even though carrion death-afterlife truth only needs a stable fauna-local key.

What was done: Added `FaunaCarrionDeathHashSalt` and generated `EntityDeathSignal.EntityHash` via `ResolveStableFaunaHash(FaunaCarrionDeathHashSalt, 0u)`. Species profile identity remains `EntityDeathSignal.SourceHash` guarded by `EntityDeathSignal.FlagFaunaBrainCarrion`.

Cinematic Cheats used: Unchanged. Stable hash identity is enough for DTO dedupe and spatial food proxy; no GameObject/component identity lookup is needed for carrion truth.

Exact microseconds saved: Death-edge-only change. Avoids one combat runtime target resolution call for each fauna death event; no frame-loop cost claim without profiler.

## 2026-05-22 Carrion Mock Counter Polish
What was wrong: `GenerateMockMassExtinctionJob` seeded `CarrionRuntimeCountersDTO.TotalActiveBiomass` from the random biomass of row 0 multiplied by count. That made the stress harness counter noisy and unrepresentative for 5000 synthetic corpses.

What was done: Replaced the seed-dependent value with `Tuning.DefaultBiomass * 1.3f * count`, where `1.3` is the exact mean of the mock biomass multiplier range `0.35..2.25`. The authoritative active biomass is still recomputed by `CalculateBiomassDecayJob` on the next Frost tick.

Cinematic Cheats used: No physical spawn or reduction pass was added; the mock counter uses a closed-form expected value.

Exact microseconds saved: Avoided an extra reduction job or buffer clear pass for cold stress setup; one scalar multiply at index 0.

## 2026-05-22 Carrion Timing Boundary Polish
What was wrong: `CarrionTelemetryEntry.BurstExecutionMicroseconds` was patched with the parent nutrient solver schedule-to-finalize microseconds. That made the 0.5ms carrion budget conservative but polluted by unrelated nutrient drift work.

What was done: Added `_carrionScheduleTicks`, sampled immediately before scheduling the carrion death/decay/injection/telemetry subchain. `FinishCompletedScheduledJob()` now calls `ResolveCarrionSolverMicros(now, micros)` before patching carrion telemetry.

Cinematic Cheats used: No profiler readback or timing job was introduced. A single owner-side timestamp gives a bounded subchain window without blocking the job graph.

Exact microseconds saved: No runtime saving claim; this is forensic precision. Added one `Stopwatch.GetTimestamp()` per Frost tick.

## 2026-05-22 Carrion Import Trim
What was wrong: `NutrientDriftRuntime_Carrion.cs` still imported `Hecton8.Gameplay` after carrion identity no longer used combat target IDs.

What was done: Removed the unused Gameplay import. Carrion partial now routes through Core signals/contracts, Vault memory, and the existing World spatial hash API.

Cinematic Cheats used: Unchanged.

Exact microseconds saved: 0 runtime; compile-wall hygiene only.

## 2026-05-22 Signal Matrix Recheck
What was wrong: Task 03 required explicit cross-reference against the interconnect matrix and Core signal declaration, not just relying on prior knowledge of `EntityDeathSignal`.

What was done: Re-read `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`; current authority points hot routes to typed `SignalBus<T>` and `GLOBAL_AUTHORITY_BOUNDARIES.md`. Rechecked `GlobalSignals.cs`: `SignalBus<EntityDeathSignal>` is configured with capacity 64, `EntityDeathSignal` is size-validated at 64 bytes, and no new `CreatureKilledSignal` lane was added.

Cinematic Cheats used: Not applicable.

Exact microseconds saved: Avoided a duplicate signal lane and queue snapshot; static capacity avoided is one additional 64-entry signal corridor.

## 2026-05-22 Scanner Report Durability Pass
What was wrong: `OOP_Destroy_Scanner` could regenerate a thinner SHINOBU_314 report and drop route/timing/RNG proof fields already present in stable JSON.

What was done: Added generator fields for stable source path, route proof, deterministic RNG, mock biomass counter, 300-frame black box, carrion subchain telemetry timing, DataMonolith pending state, CSV bridge, editor facade, and live gizmo.

Cinematic Cheats used: Not applicable; editor-only forensic proof.

Exact microseconds saved: 0 runtime; prevents future evidence drift without adding runtime code.

## 2026-05-22 CSV Species Key Route Fix
What was wrong: Fauna-owned death signals route species identity as numeric `ComputeStableSpeciesId()`, while the carrion CSV parser hashed text names with FNV. Species-specific rows could miss silently.

What was done: `species_key` now accepts `default`, decimal speciesID, `0x` hash, or text token. `FindProfile` falls back to the default row keyed by `CarrionRouteHash`; reload clears stale profile rows. The seed CSV now includes `default`, `1001`, and `8001` rows.

Cinematic Cheats used: None; this is identity-route hygiene.

Exact microseconds saved: 0 runtime claim. It prevents bad tuning misses while keeping lookup bounded to 64 rows at Frost cadence.

## 2026-05-22 NaN Fault Vaccination Pass
What was wrong: A corrupt active carrion row could carry NaN state past the decay pass or leave stale `ShinobuCarrionFaultFlags` visible after the bad row had already been sanitized.

What was done: Added `CarrionStateDTO.FlagMathFault` inside the existing 64B DTO flag lane. Death ingress sanitizes biomass scale, default biomass, epsilon, decay rate, and toxicity seed before DTO creation. Decay validates AUP, biomass, age, decay, and toxicity before math. Injection clears stale fault flags at tick start, consumes current faults into telemetry, and sanitizes nutrient/attraction scalars before grid/hash publication.

Cinematic Cheats used: No physical corpse cleanup or fault objects; one DTO flag is enough for proof and recovery.

Exact microseconds saved: Prevents NaN poison from forcing later grid/hash recovery scans. Cost is a few finite checks per active corpse on Frost cadence.

## 2026-05-22 Guarded Core Build Attempt
What was wrong: Compile proof was stale after the NaN and CSV route hardening.

What was done: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` only after the guard showed CPU below policy and no `dotnet/csc` process active. The build failed with 11 external errors in `PredatorCognitionDomain.AcousticSdf.cs`, `RadiationHazardGrid.cs`, `VRSomaticProvider.Comfort.cs`, and `PlayerKinematicsRuntime_HandIK.cs`.

Cinematic Cheats used: Not applicable.

Exact microseconds saved: No runtime saving. This records an external compile wall; no SHINOBU_314 touched file emitted a compiler error before the wall stopped the build. Further rebuild attempts are gated again: latest guard CPU 57%, still above the 50% launch threshold.
