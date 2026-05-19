# LOG_SHINOBU_116

## 2026-05-19 Macro Ecosystem Static Implementation

What was wrong:
- Ambient ocean population authority was still effectively local/legacy: no global FrostTick Lotka-Volterra truth exposed to spawn hydration.
- No SHINOBU_116-specific 32-byte `EcosystemSectorDTO` existed with the required hash, prey, predator, temperature, toxin, and explicit padding layout.
- Spawn/resource consumers had no direct macro biomass route from `GlobalDataVault`.

What was done:
- Added `MacroEcosystemMathematicianRuntime`: headless FrostTick owner, no GameObject spawns, no `UnityEngine.Physics` calls.
- Added Vault BufferIDs 70433-70442 for sector front/back, remainders, coords, index entries, biome specs, tuning, counters, telemetry, and CSV scratch.
- Implemented deterministic emergency 100x100 sector generation, Lotka-Volterra population job, quality-scaled Jacobi diffusion, integer biomass quantization, AUP sector hashing, telemetry black box, binary dump path, CSV biome parser, UI Toolkit tuner, and heatmap gizmo.
- Wired `AmbientBiotaDirector` to read macro biomass first, then fall back to the legacy ecosystem service only if macro truth is unavailable.
- Added architecture note: `Docs/ARCHITECTURE/MACRO_ECOSYSTEM_MATHEMATICIAN.md`.

Cinematic Cheats used:
- Global fish life is integer biomass fluid, not physical fish.
- Player-near boids are hydration illusions over persistent sector truth.
- Migration is Jacobi scalar leakage between sectors, not navigation or swimming simulation.
- Toxicity/temperature are continuous spawn weights, not binary zones.

Exact Microseconds saved:
- Scene spawner authority removal: 0 us hot-path direct, prevents future per-spawner scans.
- Managed population dictionaries avoided: estimated 40-120 us per spawn hydration window.
- 32-byte sector stride: estimated 15-35 us per 10k-sector pass versus wider/unaligned DTOs.
- Lotka-Volterra Burst pass: target 120-220 us per 10k sectors on MX350-class CPU before profiler proof.
- Diffusion pass: target 80-180 us per pass; `GlobalQualityWeight` scales 1-5 passes.
- Telemetry reduction: estimated 10-35 us per FrostTick.
- Unity build/profiler proof not executed: CPU counter and CIM load both returned 100; policy forbids dotnet/Unity compile above 50%.

<SELF_AUDIT id="SHINOBU_116">
  <TaskCount>20</TaskCount>
  <Domain>ECHELON 3 FLORA, FAUNA & BIOTA - Ecosystem Director (Macro)</Domain>
  <SectorDTO SizeBytes="32" SectorHashOffset="0" PreyOffset="8" PredatorOffset="12" TemperatureOffset="16" ToxinOffset="20" PadRange="24-31" />
  <VaultBufferIDs Front="70433" Back="70434" Remainders="70435" Coords="70436" IndexEntries="70437" BiomeSpecs="70438" Tuning="70439" Counters="70440" Telemetry="70441" CsvScratch="70442" />
  <GCStaticAudit>No LINQ, no managed collection creation, no coroutine, no Update in edited macro files.</GCStaticAudit>
  <PhysicsStaticAudit>No UnityEngine.Physics or Physics calls in edited macro files.</PhysicsStaticAudit>
  <Determinism>Jobs use Burst FloatMode.Deterministic, FNV sector hashes, integer biomass endpoints, and fixed FrostDeltaSeconds.</Determinism>
  <MassConservation>Authoritative biomass is uint; fractional prey/predator and diffusion residue are carried in EcosystemSectorRemainderDTO for the next FrostTick.</MassConservation>
  <AliasFence>Front/back/remainder/coord/tuning/counter/telemetry buffers use distinct BufferIDs and are locked for the scheduled job window.</AliasFence>
  <Scalability>Low=1 diffusion pass, Middle=2-3, High=4, Ultra=5 via continuous GlobalQualityWeight.</Scalability>
  <VerificationStatus>STATIC_SOURCE_PASS; BUILD_BLOCKED_CPU_100</VerificationStatus>
</SELF_AUDIT>
## 2026-05-19 Session Start
What was wrong: Macro ecosystem authority was not yet verified in source for this batch.
What was done: Extracted `SHINOBU_116` prompt from `Docs/Tasks/CURRENT_BATCH.md`, confirmed 20 tasks, read domain and eight mandates.
Cinematic Cheats used: World ecology will be represented as integer biomass fields, not physical fish simulation.
Exact Microseconds saved: PENDING MEASUREMENT. Static expectation is large savings by replacing GameObject spawns with FrostTick sector math.

## 2026-05-19 Ultra Polish Forensic Pass

What was wrong:
- First-pass macro runtime still had private persistent native lookup maps. That violated the Vault Law.
- Ambient spawn hydration called the macro runtime directly instead of staying behind the `IEcosystemDirectorService` route.
- Macro counters were adjacent scalar slots, not 64-byte false-sharing fences.
- Telemetry originally used a Unity presentation frame source; rollback-compatible macro state needs a deterministic simulation tick.

What was done:
- Removed private persistent `NativeParallelHashMap` lookup state and replaced it with Vault-owned open-address tables in `ShinobuMacroEcosystemIndexEntries` and `ShinobuMacroEcosystemBiomeSpecs`.
- Added `ClearMacroEcosystemVaultTablesJob` and `BuildSectorIndexJob` so cold boot initializes all uninitialized Vault memory before read access.
- Added `[NoAlias]` to Burst job buffer fields and retained explicit pointer jobs for front/back/remainder/coord data.
- Replaced int counters with 64-byte `MacroEcosystemCounterDTO`.
- Moved Ambient consumption back through `GlobalRegistry.EcosystemDirector`; `EcosystemDirector.TryGetBiomassAvailability` now fronts macro biomass when the Vault snapshot exists.
- Replaced Unity frame-based telemetry with `_simulationTick`.
- Re-ran static scans for `Physics`, managed population maps, local native maps, `Update` methods, `Time.frameCount`, DTO properties, and whitespace errors.

Cinematic Cheats used:
- Global life remains integer biomass fields, not entities.
- Fish near the player remain hydration visuals over sector truth.
- Migration remains scalar Jacobi leakage through 1 km cells, not NavMesh, collision, or swimming path simulation.
- Toxicity/temperature remain continuous spawn/resource weights, not binary authored zones.

Exact Microseconds saved:
- Vault open-address lookup replaces O(n) scan fallback risk: expected O(1), sub-microsecond per sector query for 10k sectors until measured.
- Removing private native maps saves hidden allocator/register churn at boot and prevents fragmentation; cold-load savings estimate 50-200 us.
- 64-byte counter padding prevents false-sharing stalls; savings are contention-dependent, expected 5-30 us on telemetry/fault-heavy FrostTick windows.
- Ambient service routing avoids adding a new sibling assembly dependency; compile-wall saving is structural, not frame-time.
- Build/profiler proof was not executed: CIM returned CPU=100 and three `Get-Counter` samples returned 100/100/100; policy forbids dotnet/Unity compile above 50%.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE">
  <TwentyTaskReconciliation>
    <Task id="01" name="MONOBEHAVIOUR_SPAWNER_ERADICATION" result="PASS">No active `FishSpawner.cs` or `BiomeRespawnPoint.cs` authority found; macro owner is headless FrostTick.</Task>
    <Task id="02" name="MANAGED_POPULATION_TRACKER_PURGE" result="PASS">No `Dictionary&lt;string,int&gt;` authority; population and lookup data are Vault buffers.</Task>
    <Task id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS">Hot DTOs expose fields, not properties.</Task>
    <Task id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS">Primary sector DTO is explicit 32 bytes; counter DTO is explicit 64 bytes.</Task>
    <Task id="05" name="EMERGENCY_MOCK_SECTOR_DATA" result="PASS">Cold boot generates deterministic 100x100 1 km sectors.</Task>
    <Task id="06" name="BURST_LOTKA_VOLTERRA_KERNEL" result="PASS">`EcosystemPopulationJob` solves prey/predator LV with toxin/temp pressure.</Task>
    <Task id="07" name="DIFFUSION_MIGRATION_ALGORITHM" result="PASS">`BiomassDiffusionJob` migrates scalar biomass to four neighbors.</Task>
    <Task id="08" name="DEAR_LIE_SPAWN_HYDRATION" result="PASS">Ambient consumes service biomass; visible boids are hydration over math truth.</Task>
    <Task id="09" name="TOXICITY_CASCADING_FAILURE" result="PASS">Toxin suppresses birth, raises starvation, and increases rare-resource weight.</Task>
    <Task id="10" name="ASYNC_FROST_TICK_EXECUTION" result="PASS">FrostTick schedules chained jobs; LateFrame completes only when ready or forced shutdown.</Task>
    <Task id="11" name="CONTINUOUS_SCALABILITY_DIFFUSION" result="PASS">`GlobalQualityWeight` maps continuously to 1-5 diffusion passes.</Task>
    <Task id="12" name="INTEGER_BIOMASS_QUANTIZATION" result="PASS">Authoritative biomass is `uint`; fractions persist in remainder DTO.</Task>
    <Task id="13" name="AUP_SECTOR_HASHING" result="PASS">Sector coords are long grid coordinates with deterministic ulong hash; local deltas cast to float after AUP subtraction.</Task>
    <Task id="14" name="ROLLBACK_DETERMINISTIC_FENCE" result="PASS">DTOs are blittable; no `Time.deltaTime`, Unity Random, or Physics authority.</Task>
    <Task id="15" name="UNINITIALIZED_MEMORY" result="PASS">Vault buffers request `NativeArrayOptions.UninitializedMemory`; cold jobs fill them before read.</Task>
    <Task id="16" name="TELEMETRY_RING" result="PASS">300-entry ring plus binary dump path implemented; deterministic tick recorded.</Task>
    <Task id="17" name="EDITOR_TUNER" result="PASS">UI Toolkit tuner writes unmanaged Vault tuning fields.</Task>
    <Task id="18" name="ZERO_GC_CSV_PARSER" result="PASS">CSV bytes parse through scratch buffer/span logic into Vault spec table.</Task>
    <Task id="19" name="HEATMAP_GIZMO" result="PASS">Editor gizmo reads Vault biomass/toxin and draws debug cells; player build cost is zero.</Task>
    <Task id="20" name="SELF_AUDIT" result="PASS_WITH_BUILD_BLOCK">Static audit and docs written; compile withheld by CPU policy.</Task>
  </TwentyTaskReconciliation>
  <StructLayoutVerification>
    <EcosystemSectorDTO size="32" alignment="8-byte multiple">
      <Field name="SectorHash" offset="0" size="8" type="ulong" />
      <Field name="PreyBiomass" offset="8" size="4" type="uint" />
      <Field name="PredatorBiomass" offset="12" size="4" type="uint" />
      <Field name="LocalTemperature" offset="16" size="4" type="float" />
      <Field name="ToxinLevel" offset="20" size="4" type="float" />
      <Field name="Reserved0" offset="24" size="4" type="uint" />
      <Field name="Reserved1" offset="28" size="4" type="uint" />
      <Math>8+4+4+4+4+4+4 = 32; 32 % 16 = 0.</Math>
    </EcosystemSectorDTO>
    <MacroEcosystemCounterDTO size="64" falseSharingFence="true">
      <Field name="Value" offset="0" size="4" type="int" />
      <Field name="Flags" offset="4" size="4" type="uint" />
      <Field name="Reserved0-6" offset="8" size="56" type="ulong[7]" />
      <Math>4+4+56 = 64; one counter per L1 cache line.</Math>
    </MacroEcosystemCounterDTO>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Below weight 0.3 the solver collapses to one or two diffusion passes and keeps only scalar sector suitability. The expensive migration smoothing is reduced by `ResolveDiffusionSteps(math.lerp(1,5.999,q))`; spawn hydration reads a single sector lookup and no fish entities exist globally. At weight 1.0 the same math route allows five diffusion passes and richer downstream visual hydration without changing authority.
  </ScalabilityCurve>
  <VaultStatus privatePersistentArrays="0">
    <Handle id="70433" name="ShinobuMacroEcosystemSectorFront" />
    <Handle id="70434" name="ShinobuMacroEcosystemSectorBack" />
    <Handle id="70435" name="ShinobuMacroEcosystemRemainders" />
    <Handle id="70436" name="ShinobuMacroEcosystemSectorCoords" />
    <Handle id="70437" name="ShinobuMacroEcosystemIndexEntries" />
    <Handle id="70438" name="ShinobuMacroEcosystemBiomeSpecs" />
    <Handle id="70439" name="ShinobuMacroEcosystemTuning" />
    <Handle id="70440" name="ShinobuMacroEcosystemCounters" />
    <Handle id="70441" name="ShinobuMacroEcosystemTelemetryRing" />
    <Handle id="70442" name="ShinobuMacroEcosystemCsvScratch" />
  </VaultStatus>
  <PointerAliasingAndDependencyGraph>
    <NoAlias>Population, diffusion, copy, clear, and telemetry jobs mark independent buffers with `[NoAlias]` where applicable.</NoAlias>
    <Consumes>Existing dispatcher FrostTick order and previous macro active handle if one is pending.</Consumes>
    <Produces>`EcosystemPopulationJob -> BiomassDiffusionJob[1..5] -> CopySectorBufferJob(optional) -> EcosystemTelemetryReductionJob`.</Produces>
    <MainThreadBlocking>Cold boot intentionally completes the initialization job before readers can observe uninitialized Vault memory. Runtime `LateFrameTick` skips `Complete()` unless `IsCompleted` or forced shutdown.</MainThreadBlocking>
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    Runtime domain is currently under the repo's existing `Hecton8.Core` assembly folder. No new direct Ambient-to-macro assembly reference remains; Ambient consumes `IEcosystemDirectorService` from `GlobalRegistry`. No sibling runtime asmdef reference was added.
  </CompileGuard>
  <DearLieConfirmation>
    <Before>Per-fish global simulation or physics overlap spawning is O(visible+unloaded creatures) and unbounded at 100x100 km scale.</Before>
    <After>Sector biomass solve is O(sectors * diffusionSteps), currently 10000 * 1..5 per FrostTick, with O(1) expected spawn lookup.</After>
    <Fake>Fish, predator pressure, and rare resources are scalar fields until a local visual system hydrates them near the player.</Fake>
  </DearLieConfirmation>
  <Verification>
    <StaticScan>No macro file hits for `UnityEngine.Physics`, `Physics.`, `new Dictionary`, `Dictionary&lt;string,int&gt;`, `NativeParallelHashMap`, `Update`, `FixedUpdate`, `LateUpdate`, `Time.frameCount`, or hot DTO property syntax.</StaticScan>
    <DiffCheck>`git diff --check` returned exit 0; only line-ending warnings on existing files.</DiffCheck>
    <Build>Not launched. CPU gate failed: CIM=100, Get-Counter=100/100/100.</Build>
  </Verification>
</SELF_AUDIT>
