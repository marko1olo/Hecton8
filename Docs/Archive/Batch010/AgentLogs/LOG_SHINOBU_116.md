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

## 2026-05-19 Cached Vault Consumer Polish

What was wrong:
- The SHINOBU-added macro biomass reader in `EcosystemDirector` called `ResolveDataVault()`. That helper can read `GlobalRegistry.DataVault` when `_dataVault` is empty, so a spawn-query path had a cold-discovery escape hatch.

What was done:
- `TryGetMacroVaultBiomassAvailability` now reads `_dataVault` directly and returns false when the cached Vault is absent.
- This preserves the existing cold initialization owner and keeps macro hydration behind `IEcosystemDirectorService` plus `MacroEcosystemVaultContract`.

Cinematic cheats used:
- No new simulation. The Dear Lie remains scalar biomass lookup plus visual hydration, not global fish entities.

Exact microseconds saved:
- Expected sub-microsecond per hydration query by skipping a possible registry property read. Source-only estimate; profiler proof is still absent.

Regression model:
- CPU: lower or equal on the query path.
- GC: no managed allocation added.
- Memory: no new buffers or fields.
- Cadence: unchanged; macro remains FrostTick producer, consumer remains pull-only.
- Correctness: if cold init has not cached `_dataVault`, macro read fails closed and legacy biomass fallback remains the route.

Verification:
- Extracted `TryGetMacroVaultBiomassAvailability` method body returned `HOT_METHOD_REGISTRY_CLEAN`.
- Macro forbidden-pattern scan returned no hits for Physics, managed dictionary authority, local native maps, coroutines, Unity random, or frame-counter usage in edited macro files.
- DTO/property/Pack=1 scan on macro runtime and contract returned no hits.
- `git diff --check` returned exit 0 with line-ending warnings only.
- Build was not launched: active `dotnet` processes existed and CPU samples were 72.8/14.8/49.5.

## 2026-05-19 Macro Consumer Handle Cache Polish

What was wrong:
- The macro consumer path no longer hit `GlobalRegistry`, but it still reacquired three typed Vault views through `TryGetBuffer<T>` on every biomass query.

What was done:
- Added cached `VaultBufferHandle<MacroEcosystemSectorVaultRecord>`, `VaultBufferHandle<MacroEcosystemSectorIndexRecord>`, and `VaultBufferHandle<MacroEcosystemTuningVaultRecord>` fields to `EcosystemDirector`.
- `TryGetMacroVaultBiomassAvailability` now resolves those handles through `TryResolveMacroEcosystemVaultSnapshot`.
- Runtime-state disposal resets the handles alongside `_dataVault`.

Cinematic cheats used:
- Unchanged. Global fish truth remains integer biomass sectors; visible fish are hydrated near the player.

Exact microseconds saved:
- Expected sub-microsecond per query by removing repeated typed view acquisition. Source-only estimate; profiler proof is still pending.

Regression model:
- CPU: lower or equal on successful macro query path.
- GC: no managed allocation added; handles are value-type metadata fields.
- Memory: three handle structs, no NativeArray cache and no private population storage.
- Cadence: unchanged.
- Correctness: handle generation remains checked by `VaultBufferHandle<T>.Resolve`; absent handles fail closed to the legacy biomass path.

Verification:
- Extracted `TryGetMacroVaultBiomassAvailability` plus helper body returned `HOT_METHOD_HANDLE_ROUTE_CLEAN`.
- Macro forbidden-pattern scan returned no hits.
- DTO/property/Pack=1 scan on macro runtime and contract returned no hits.
- `git diff --check` returned exit 0 with line-ending warnings only.
- Build was not launched: active `dotnet` processes existed and CPU samples were 97.2/100/99.8.

## 2026-05-19 Contract Mirror Layout Proof

What was wrong:
- The producer DTO layout was asserted, but the contract mirror records used by World were only source-matched by inspection.

What was done:
- `MacroEcosystemLayoutManifest` now asserts size and critical offsets for `MacroEcosystemSectorVaultRecord`, `MacroEcosystemSectorIndexRecord`, and `MacroEcosystemTuningVaultRecord`.

Cinematic cheats used:
- None. This is ABI proof for the existing Dear Lie route.

Exact microseconds saved:
- No hot-path saving claimed. This prevents a Vault type-mismatch fatal path before gameplay.

Regression model:
- CPU: one-time boot/editor offset checks only.
- GC: no hot-path allocation.
- Memory: no new runtime storage.
- Cadence: unchanged.
- Correctness: consumer mirror records must now match writer DTO stride/offsets before macro boot proceeds.

Verification:
- Assertion scan found contract mirror layout checks for sector, index, and tuning records.
- Macro forbidden-pattern scan returned no hits.
- Extracted consumer method stayed `HOT_METHOD_HANDLE_ROUTE_CLEAN`.
- `git diff --check` returned exit 0 with line-ending warnings only.
- Build was not launched: active `dotnet` processes existed and CPU samples were 94.2/100/99.8.

## 2026-05-19 Pack Directive Hygiene

What was wrong:
- `EcosystemDirector` still had explicit-layout structs with `Pack=1` in the same SHINOBU-touched ecosystem route. The offsets and sizes were already manually fixed, making Pack=1 redundant and hostile to the ARM64 mandate.

What was done:
- Removed `Pack=1` from the touched `EcosystemDirector` explicit-layout structs while preserving every `Size` and `FieldOffset`.

Cinematic cheats used:
- None. This is memory ABI hygiene.

Exact microseconds saved:
- No measured hot-path saving claimed. The change removes an ARM64 alignment hazard.

Regression model:
- CPU: neutral or lower if alignment-sensitive native array access was affected.
- GC: unchanged.
- Memory: same explicit struct sizes.
- Cadence: unchanged.
- Correctness: binary field offsets remain explicit; save/telemetry schema shape is preserved by `Size` and `FieldOffset`.

Verification:
- Touched-file Pack=1 scan across macro runtime, macro contract, and ecosystem consumer returned no hits.
- Macro forbidden-pattern scan returned no hits.
- Extracted consumer method stayed `HOT_METHOD_HANDLE_ROUTE_CLEAN`.
- `git diff --check` returned exit 0 with line-ending warnings only.
- Build was not launched: no dotnet/csc rows were returned, but CPU samples were 100/100/100.

## 2026-05-19 Snapshot Race Polish

What was wrong:
- Static review found that macro diffusion can write `ShinobuMacroEcosystemSectorFront` while consumers read it through the contract route.
- DataVault `TryLockBuffer` prevents relocation/compaction, but it is not a read/write coherency fence for external consumers.

What was done:
- Added `MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight`.
- Macro sets the flag before scheduling the FrostTick job chain and clears it only after `_activeJobHandle.Complete()` in the existing LateFrame/forced shutdown completion path.
- `EcosystemDirector` now reads tuning before the sector payload and rejects macro biomass while the flag is active, falling back to legacy biomass rather than consuming a torn front buffer.
- Static macro direct readers also reject reads while `_jobScheduled` is true.
- Architecture docs now include a route card for the macro Vault snapshot route with review disposition `YELLOW` because runtime proof is still gated.

Cinematic Cheats used:
- Consumer fallback is still data-only biomass; no physical fish, NavMesh, or Physics query is introduced to hide the unavailable macro snapshot.

Exact Microseconds saved:
- Frame-time savings are not claimed. The patch adds one `uint` flag test to the spawn query route and removes a deterministic torn-read failure mode.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_SNAPSHOT_GATE">
  <Race>Macro front buffer writes are fenced by `TuningFlagSnapshotWriteInFlight`; consumers fail closed during the scheduled job window.</Race>
  <NoBlocking>Consumers do not call `JobHandle.Complete()` and do not spin-wait for macro jobs.</NoBlocking>
  <RouteCard>Documented in `Docs/ARCHITECTURE/MACRO_ECOSYSTEM_MATHEMATICIAN.md`; disposition is `YELLOW` until Unity compile/runtime/profiler evidence exists.</RouteCard>
  <StaticChecks>Direct concrete World/Ambient macro coupling clean; macro forbidden-pattern scan clean; DTO property scan clean; Burst attribute count matches deterministic flag count.</StaticChecks>
  <BuildGate>No dotnet/csc process rows reported; latest `Get-Counter` CPU samples 99.4/37.1/86.5, so build was not launched.</BuildGate>
  <NaNGate>World contract reader now rejects non-finite positions before sector hashing.</NaNGate>
</SELF_AUDIT>

## 2026-05-19 Compile-Wall Route Polish

What was wrong:
- `EcosystemDirector` still had a concrete `Hecton8.Ecosystem.MacroEcosystemMathematicianRuntime` call in the biomass query route.
- Ambient was already behind `IEcosystemDirectorService`, but the service implementation still pulled from a sibling concrete runtime type.

What was done:
- Added `Assets/_Project/Scripts/Core/Contracts/MacroEcosystemVaultContract.cs` with explicit-layout contract records for macro sector, index, and tuning buffers plus shared sector hash/open-address probe math.
- Removed `using Hecton8.Ecosystem` from `EcosystemDirector`.
- Replaced the concrete macro runtime call with `TryGetMacroVaultBiomassAvailability`, which reads `ShinobuMacroEcosystemSectorFront`, `ShinobuMacroEcosystemIndexEntries`, and `ShinobuMacroEcosystemTuning` through `IDataVault` and contract records.
- Kept legacy `EcosystemDirector` biomass fallback unchanged when macro snapshot buffers are absent.

Cinematic Cheats used:
- Spawn hydration still reads one scalar sector record; no visual fish are simulated globally.
- The service route remains a Dear Lie over Vault biomass, not a physical population manager.

Exact Microseconds saved:
- Frame-time savings are not claimed. This is compile-wall and ownership hardening.
- Query complexity remains expected O(1): open-address sector index probe plus one sector/tuning read.
- Iteration-speed savings are structural: World no longer needs a concrete macro runtime reference for the hydration route.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_ROUTE_POLISH">
  <CompileGuard>No `World/EcosystemDirector.cs` or `AI/Ambient/AmbientBiotaDirector.cs` reference to `MacroEcosystemMathematicianRuntime` or `using Hecton8.Ecosystem` remains.</CompileGuard>
  <VaultRoute>Reader path: `IEcosystemDirectorService -> EcosystemDirector -> IDataVault -> MacroEcosystemVaultContract`.</VaultRoute>
  <Ownership>Macro runtime remains sole writer of `ShinobuMacroEcosystem*` buffers; World only reads contract-record snapshots.</Ownership>
  <StructLayout>
    <MacroEcosystemSectorVaultRecord size="32" offsets="0,8,12,16,20,24-31" />
    <MacroEcosystemSectorIndexRecord size="16" offsets="0,8,12" />
    <MacroEcosystemTuningVaultRecord size="64" offsets="0..60" />
  </StructLayout>
</SELF_AUDIT>

## 2026-05-19 Fault-Path and Biome Spec Polish Pass

What was wrong:
- The population job had previously used aggregate fault-counter thinking; a parallel sector solver must not write one shared fault location.
- Biome CSV ingestion existed, but player builds still needed a guaranteed numeric baseline when the editor CSV bridge is absent.
- Layout proof focused on the primary sector DTO; secondary Vault DTOs also carry rollback, telemetry, and Burst ABI risk.

What was done:
- Added/used `ShinobuMacroEcosystemFaultFlags` as one `uint` fault slot per sector. `EcosystemPopulationJob` writes only `FaultFlags[index]`; telemetry reduction reads the array and folds invalid-math state into the black box entry.
- Seeded default biome specs into the Vault during cold boot and kept CSV reload under `#if UNITY_EDITOR`. Player FrostTick no longer depends on file probing for biome capacity/resistance/toxin parameters.
- Fed biome spec carrying capacity, migration resistance, temperature optimum, and toxin penalty into LV and diffusion math.
- Hardened `FrostTick` sector count against short front/back/remainder/coord/fault buffers.
- Expanded `MacroEcosystemLayoutManifest` to assert offsets for sector coords, remainders, index entries, biome specs, tuning, telemetry, and 64-byte counters.

Cinematic Cheats used:
- Biome ecology remains scalar sector truth. Local fish, predators, and rare resources are hydration decisions over biomass and toxin/temperature weights.
- Migration remains 4-neighbor Jacobi leakage, not NavMesh or physics motion.
- Toxic reactor/vent pressure is a continuous coefficient cascade, not a spawned hazard object query.

Exact Microseconds saved:
- Per-sector fault flags avoid shared counter contention in `IJobParallelFor`; expected 5-30 us only under invalid-math/fault-heavy FrostTick windows, pending profiler proof.
- Player-runtime CSV file probing removed; saved cost is spike-risk removal, not claimed steady-frame measurement.
- Default spec seeding is cold boot only; hot path cost remains the existing table probe.
- Expanded layout manifest is cold one-time proof; hot path cost is 0 us after verification flag is set.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_POLISH">
  <TwentyTaskReconciliation>
    <Task id="01" result="PASS">No static fish spawner authority introduced.</Task>
    <Task id="02" result="PASS">No managed population dictionary authority introduced.</Task>
    <Task id="03" result="PASS">Hot DTOs remain public fields, no get/set property pattern.</Task>
    <Task id="04" result="PASS">Primary sector DTO 32B; secondary DTO sizes and offsets now asserted.</Task>
    <Task id="05" result="PASS">Mock 10k sectors still generated cold with deterministic biome hashes.</Task>
    <Task id="06" result="PASS">LV job now consumes biome capacity/temp/toxin coefficients.</Task>
    <Task id="07" result="PASS">Diffusion job consumes biome migration resistance and temperature suitability.</Task>
    <Task id="08" result="PASS">Dear Lie route remains service-backed; no GameObject spawn authority added.</Task>
    <Task id="09" result="PASS">Toxicity cascade now includes biome toxin penalty.</Task>
    <Task id="10" result="PASS">Runtime remains scheduled FrostTick job chain.</Task>
    <Task id="11" result="PASS">Diffusion pass count still derives from continuous GlobalQualityWeight.</Task>
    <Task id="12" result="PASS">Biomass endpoints remain uint plus remainder DTO fractions.</Task>
    <Task id="13" result="PASS">Sector hashing remains long-coordinate FNV route.</Task>
    <Task id="14" result="PASS">Jobs stay deterministic float mode; no Unity time/random/physics authority.</Task>
    <Task id="15" result="PASS">Vault buffers still request uninitialized memory and cold jobs initialize them.</Task>
    <Task id="16" result="PASS">Telemetry now reads per-sector fault flags, no shared parallel fault counter.</Task>
    <Task id="17" result="PASS">Editor tuner route unchanged.</Task>
    <Task id="18" result="PASS">Default spec seed plus editor-only CSV reload into Vault spec table.</Task>
    <Task id="19" result="PASS">Heatmap route unchanged, editor-only.</Task>
    <Task id="20" result="PASS_WITH_BUILD_GATE">Static audit updated; build remains subject to CPU/no-dotnet gate.</Task>
  </TwentyTaskReconciliation>
  <StructLayoutVerification>
    <EcosystemSectorDTO size="32" math="8+4+4+4+4+8 padding = 32; 32 % 16 = 0" />
    <BiomeEcosystemSpecDTO size="24" math="6 uint/float lanes * 4 = 24; 24 % 8 = 0" />
    <MacroEcosystemCounterDTO size="64" falseSharingFence="true" math="4+4+56 = 64; one cache line" />
    <FaultFlags elementSize="4" owner="ShinobuMacroEcosystemFaultFlags" note="uint array, one writer per sector index" />
  </StructLayoutVerification>
  <VaultStatus privatePersistentArrays="0">70433,70434,70435,70436,70437,70438,70439,70440,70441,70442,70447</VaultStatus>
  <PointerAliasing>NoAlias on independent job fields; per-sector fault writes are index-disjoint.</PointerAliasing>
  <DearLie>Global ecology remains O(sectors * diffusionSteps) scalar math; spawn hydration stays O(1) expected lookup and visual-only downstream instancing.</DearLie>
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

## 2026-05-19 Snapshot Fence And Hot Registry Polish

What was wrong: The previous report said the macro consumer read tuning first and failed closed during writes, but source showed the flag read happened after index resolution and only once. Macro `EnsureVaultState()` also still contained a possible `GlobalRegistry.DataVault` read reachable from `FrostTick` when `_vault` was absent. `EcosystemTelemetryReductionJob` carried `NativeDisableParallelForRestriction` without needing it.

What was done: Moved initial macro DataVault discovery into a cold activation helper and the existing hot-swap callback; `FrostTick` now uses cached `_vault` only. `EcosystemDirector.TryGetMacroVaultBiomassAvailability` now performs a pre-read tuning gate, reads the sector, then performs a post-read tuning gate that rejects write-in-flight, `Flags`, `StateHash`, or carrying-capacity drift. Removed `NativeDisableParallelForRestriction` from telemetry/counter NativeArray fields.

Cinematic Cheats used: The global ecosystem remains integer biomass truth only. Visual hydration still reads scalar sector biomass and lets local presentation systems fake visible life instead of simulating map-wide entities.

Exact microseconds saved: No measured microseconds claimed. Static benefit is route discipline and race closure. The extra post-read tuning check is one 64-byte record read plus scalar comparisons per macro biomass query.

Verification:
- `FROSTTICK_REGISTRY_CLEAN`
- `PRE_POST_TUNING_FENCE_WITH_CAPACITY_DRIFT_PRESENT`
- Edited-file scan for `NativeDisableParallelForRestriction`, `NativeDisableContainerSafetyRestriction`, runtime `Pack=1`, managed population dictionaries, Physics, Unity Random, foreach, LINQ, coroutine patterns returned no hits.
- `BURST_JOB_COUNT=7` and `BURST_FLAGS_DETERMINISTIC_EXACT`
- `git diff --check` exit 0 with line-ending warnings only.
- Build gate: `NO_DOTNET_OR_CSC`; CPU samples `81.7/93.8/61.7`; dotnet build was not launched.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_SNAPSHOT_POLISH">
  <TaskReconciliation>Tasks 01-20 remain source-implemented; Loop 21 tightened Task 08 consumer fencing, Task 10 FrostTick route discipline, Task 16 telemetry safety attributes, and Task 20 evidence accuracy.</TaskReconciliation>
  <SnapshotFence>Consumer reads tuning before and after sector read; write-in-flight, Flags drift, StateHash drift, or carrying-capacity drift fails closed to local legacy ecology.</SnapshotFence>
  <HotRegistry>Macro FrostTick extraction contains no `GlobalRegistry.DataVault`; cold activation and registry hot-swap own DataVault discovery.</HotRegistry>
  <SafetyAttributes>`NativeDisableParallelForRestriction` removed from macro telemetry reduction NativeArrays; pointer fields retain unsafe pointer attributes because jobs consume raw Vault pointers.</SafetyAttributes>
  <BuildGate>Compile/runtime/GC proof remains pending because CPU policy blocked dotnet build at 100/100/100 processor samples.</BuildGate>
</SELF_AUDIT>

## 2026-05-19 Quality Curve And Completion Fence Polish

What was wrong: Diffusion pass count consumed `GlobalQualityWeight`, but migration amplitude still used the same scalar effect once a pass existed. The completion path cleared `_jobScheduled` before it cleared `TuningFlagSnapshotWriteInFlight`, leaving a narrow proof gap for same-frame reentrant direct readers.

What was done: Added `ResolveQualityCurve`, `ResolveQualityFlowWeight`, and `QualityFlowWeight` on `BiomassDiffusionJob`. The curve uses sanitized `GlobalQualityWeight`, polynomial smoothing, `math.lerp`, and `math.step`; low quality keeps one Jacobi pass and reduced migration flow, while high quality restores five passes and full migration flow. Moved `_jobScheduled = false` after write-in-flight flag cleanup and telemetry patching.

Cinematic Cheats used: The macro ocean still treats prey, predators, and rare-resource pressure as scalar biomass fields. Low-quality devices slow hidden migration math rather than simulating fewer visible fish globally; local visual systems hydrate the lie near the player from O(1) sector reads.

Exact microseconds saved: No measured microseconds claimed. Static curve samples using C# positive-float truncation: q=0.10 -> 1 step/0.2500 flow, q=0.29 -> 1 step/0.2763 flow, q=0.50 -> 2 steps/0.4873 flow, q=0.75 -> 4 steps/0.8260 flow, q=1.00 -> 5 steps/1.0000 flow.

Verification:
- `BURST_EXACT=7 JOBS=7`
- Edited-file scan for Physics, Unity Random, foreach, LINQ, managed population dictionaries, `Time.deltaTime`, runtime `Pack=1`, `NativeDisableParallelForRestriction`, and `NativeDisableContainerSafetyRestriction` returned no hits.
- `git diff --check` exit 0 with line-ending warnings only.
- Build gate: dotnet processes were active; CPU samples were `16.7/36.5/39.2`; dotnet build was not launched.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_QUALITY_CURVE_POLISH">
  <TaskReconciliation>Tasks 01-20 remain source-implemented; Loop 22 tightened Task 10 completion fencing, Task 11 continuous quality scaling, and Task 20 evidence accuracy.</TaskReconciliation>
  <ScalabilityCurve>Below q=0.30 the integer diffusion pass count remains one; migration flow stays in the 0.25-0.2822 range. q=0.50 yields two passes and 0.4873 flow. q=0.75 yields four passes and 0.8260 flow. q=1.00 yields five passes and full flow.</ScalabilityCurve>
  <SnapshotFence>`_jobScheduled` remains true until the snapshot write-in-flight flag is cleared and telemetry is patched.</SnapshotFence>
  <CompileGate>Compile/runtime/GC proof remains pending because active dotnet processes violate the no-build gate.</CompileGate>
</SELF_AUDIT>

## 2026-05-19 Direct Macro Reader Fence Polish

What was wrong: Same-domain static macro readers rejected `_jobScheduled`, but they did not independently read tuning before and after sector access. Future direct macro consumers could have been less protected than `EcosystemDirector`.

What was done: `TryGetSectorBiomass` and `TryGetSectorSpawnWeights` now perform pre/post tuning reads, reject `TuningFlagSnapshotWriteInFlight`, and reject drift in `Flags`, `StateHash`, carrying-capacity fields, and temperature tuning fields.

Cinematic Cheats used: No new simulation. Static readers still expose scalar sector truth for local visual hydration and debug/editor tools; visible fish remain presentation over biomass.

Exact microseconds saved: No measured microseconds claimed. This adds one 64-byte tuning read and scalar comparisons to direct same-domain reads, trading sub-microsecond query cost for deterministic snapshot fencing.

Verification:
- `DIRECT_READERS_PRE_POST_TUNING_FENCE_PRESENT`
- `BURST_EXACT=7 JOBS=7`
- Edited-file forbidden-pattern scan returned no hits.
- `git diff --check` exit 0 with line-ending warnings only.
- Build gate: `NO_DOTNET_OR_CSC`; CPU samples were `72.2/100/99.2`; dotnet build was not launched.

<SELF_AUDIT id="SHINOBU_116" evidence="STATIC_SOURCE_DIRECT_READER_FENCE_POLISH">
  <TaskReconciliation>Tasks 08, 10, 11, 14, 16, and 20 received additional evidence; direct macro readers now match the contract consumer snapshot fence.</TaskReconciliation>
  <SnapshotFence>Static macro readers read tuning before and after sector access and fail closed on write-in-flight or tuning drift.</SnapshotFence>
  <BuildGate>Compile/runtime/GC proof remains pending because CPU samples exceeded the no-build threshold.</BuildGate>
</SELF_AUDIT>
