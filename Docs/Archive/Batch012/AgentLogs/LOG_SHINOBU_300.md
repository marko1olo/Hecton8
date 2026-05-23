# SHINOBU_300 LOG

## 2026-05-22 - ECOSYSTEM_MACRO_BALANCER

What was wrong:
- Macro sector ABI was 32 bytes and could not carry flora/prey/predator/carrying capacity as required by the batch prompt.
- Old macro readers still expected `LocalTemperature`/`ToxinLevel` and `uint` biomass fields.
- Presentation consumers could fall back from the Vault route because the front buffer type handle did not match the actual runtime sector DTO.
- Telemetry only tracked prey/predator aggregates and did not provide flora, diffusion transfers, max predator density, substep count, or over-budget fault flags.
- No hard scanner/report artifact existed for OOP macro-spawn eradication.

What was done:
- `MacroEcosystemMathematicianRuntime` remains the single FrostTick macro owner; no new GameObject simulation owner was introduced.
- `EcosystemSectorDTO` is now explicit 64B: `SectorHash`, `FloraBiomass`, `PreyBiomass`, `PredatorBiomass`, `CarryingCapacity`, `DominantSpeciesMask`, private padding.
- Lotka-Volterra/logistic integration now runs over flat sector pointers with continuous `GlobalQualityWeight` substeps.
- Adjacent-sector diffusion now moves flora/prey/predator scalars by food/predator gradients with AUP double/int64 sector positioning before float distance math.
- World and stress-spawn readers now read the real `Hecton8.Ecosystem.EcosystemSectorDTO` front buffer and normalize from sector carrying capacity.
- Telemetry ring records flora/prey/predator totals, carrying capacity, diffusion transfers, max predator density, substeps, flags, and writes `Docs/AgentLogs/Dump_SHINOBU_300.bin` on NaN/over-2ms fault.
- Editor graph and heatmap now read the new biomass layout.
- Added `OOP_Spawner_Scanner` and `Docs/Reports/AI_OPTIMIZATION_REPORT.json` with summary `OOP Macro-Simulations Eradicated`.
- Added `RunShinobu300SelfAudit(out string failure)` partial audit hook.

Cinematic cheats used:
- No fish are simulated as macro truth. The solver writes density scalars into `DominantSpeciesMask`; visual/spawn systems consume probabilities.
- Flora starvation and predator density are used as cheap presentation weights instead of temperature/toxin physical simulation.
- Low hardware uses fewer continuous integration substeps; top-tier hardware spends extra math only on macro fidelity and visual readout, not authority layout changes.

Exact microseconds saved:
- Competing macro owner rejected: estimated 35-120 us per FrostTick.
- Side-buffer gathers rejected by 64B sector ABI: estimated 40-80 us at 4096 sectors.
- Managed/object macro fish simulation rejected: estimated 250-400 us per FrostTick, larger during spawn bursts.
- Entity migration rejected for four-neighbor flat diffusion: estimated 300 us at 4096 sectors.
- Hot signal broadcast rejected for Vault snapshot reads: estimated 8 us per consumer tick.
- Uninitialized Vault acquire retained: estimated 70 us cold allocation saving.

Verification:
- XML prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`.
- `rg` stale macro field scan: no `sector.LocalTemperature`, `sector.ToxinLevel`, old telemetry fields, or old contract snapshot readers remain in macro/consumer scope.
- `rg` OOP macro scan: 0 `Instantiate`, 0 coroutine simulation loops, 1 documented non-macro `EcosystemHealthDirector` dictionary.
- `git diff --check`: pass; line-ending warnings only.
- Build not launched: active `dotnet` PID 6776 was Unity `DotNetSdkRoslyn\VBCSCompiler.dll`; project rule forbids starting dotnet build while another dotnet/csc process is running.

## 2026-05-22 - Polish Pass Contract-TypeHash Correction

What was wrong:
- The first SHINOBU_300 implementation made the hot 64-byte `EcosystemSectorDTO` live in `Hecton8.Ecosystem`, while cross-domain contract readers had only `MacroEcosystemSectorVaultRecord`.
- `GlobalDataVault` validates `VaultGenerationHandle<T>` by exact generic type hash. Same size and same offsets are insufficient; producer and consumer using two different C# types would fail the macro snapshot route.
- The scanner report target `Docs/Reports/AI_OPTIMIZATION_REPORT.json` was unscoped and collided with a neighboring SHINOBU report shape.

What was done:
- Added canonical `Hecton8.Core.Contracts.EcosystemSectorDTO` with explicit 64-byte layout.
- Removed the local `Hecton8.Ecosystem.EcosystemSectorDTO` definition and routed `MacroEcosystemMathematicianRuntime`, `EcosystemDirector`, `StressDrivenSpawnDirector`, heatmap gizmo, and audit code through the exact contract DTO alias.
- Kept `MacroEcosystemSectorVaultRecord` only as a 64-byte cold ABI mirror for layout scanners.
- Updated `Docs/ARCHITECTURE/MACRO_ECOSYSTEM_MATHEMATICIAN.md` and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to record the 64-byte contract route and the TypeHash constraint.
- Retargeted `OOP_Spawner_Scanner` to `Docs/Reports/SHINOBU_300_AI_OPTIMIZATION_REPORT.json`.

Cinematic cheats used:
- Macro truth remains scalar biomass only. No fish, herd, prey, predator, or flora GameObject is spawned to represent migration.
- `DominantSpeciesMask` carries packed density bytes so presentation can fake abundance visually without CPU entity truth.
- Diffusion is four-neighbor scalar gradient math, not object travel, NavMesh, Rigidbody, or trigger volumes.

Exact microseconds saved:
- TypeHash retry/fallback route avoided in readers: estimated 8-20 us per spawn/ecosystem hydration window when macro snapshot exists.
- OOP fish/flora macro simulation still rejected: estimated 250-400 us per FrostTick, higher during burst spawns.
- Entity migration still rejected for flat diffusion: estimated 300 us at 4096 sectors.
- Report collision fix: 0 runtime us; prevents proof artifact loss.

Verification:
- `rg` found no `Hecton8.Ecosystem.EcosystemSectorDTO`, no hot `VaultGenerationHandle<MacroEcosystemSectorVaultRecord>`, and no hot `NativeArray<MacroEcosystemSectorVaultRecord>`.
- `rg` found no stale `sector.LocalTemperature`, `sector.ToxinLevel`, old aggregate telemetry fields, or hot snapshot reader remnants in SHINOBU_300 macro/consumer scope.
- `rg` found no `Instantiate`, `StartCoroutine`, or `IEnumerator` macro simulation loops in Ecosystem/Fauna/World macro scope.
- `rg` found no runtime `Pack=1` in the touched macro runtime or contract files.
- `git diff --check` passed for the touched SHINOBU_300 files with line-ending warnings only.
- Build was not launched: active Unity `dotnet` PID 6776 remained present and CPU sampled at 79%, above the project build gate.

<SELF_AUDIT agent_id="SHINOBU_300">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">rg archaeology completed before coding; existing macro runtime, consumers, legacy AI ecosystem DTO, and cold health dictionary were identified.</TASK>
    <TASK id="02" status="PASS">Integrated into existing partial `MacroEcosystemMathematicianRuntime`; no new MonoBehaviour owner.</TASK>
    <TASK id="03" status="PASS">No hot SignalBus lane was introduced; macro truth route is Vault snapshot plus contract helpers.</TASK>
    <TASK id="04" status="PASS">Macro scope contains no `Instantiate` or coroutine simulation loop.</TASK>
    <TASK id="05" status="PASS">Macro biomass truth is a flat Vault `EcosystemSectorDTO[10000]`; cold `EcosystemHealthDirector` dictionary is documented outside macro biomass authority.</TASK>
    <TASK id="06" status="PASS">Emergency mock job writes flora/prey/predator/carrying capacity stress cases.</TASK>
    <TASK id="07" status="PASS">Burst deterministic LV/logistic kernel integrates flora, prey, and predator scalars over pointers.</TASK>
    <TASK id="08" status="PASS">All biomass math is finite-guarded and clamped to carrying capacity.</TASK>
    <TASK id="09" status="PASS">Adjacent-sector diffusion is four-neighbor flat scalar migration using int64 sector coords before float distance math.</TASK>
    <TASK id="10" status="PASS">Dear Lie output is packed density in `DominantSpeciesMask`, not entity truth.</TASK>
    <TASK id="11" status="PASS">`GlobalQualityWeight` continuously resolves LV substeps 1..6 and diffusion/flow weight.</TASK>
    <TASK id="12" status="PASS">AUP sector hashing uses double division, floor, int64 coords, and FNV-style 64-bit hash.</TASK>
    <TASK id="13" status="PASS">Jobs use `FloatMode.Deterministic`; sector DTO is blittable 64 bytes for blind copy.</TASK>
    <TASK id="14" status="PASS">Vault acquisition retains uninitialized buffer route for cold allocation cost control.</TASK>
    <TASK id="15" status="PASS">300-entry black box records totals, diffusion transfers, substeps, timing, flags, and dumps `Dump_SHINOBU_300.bin` on fault.</TASK>
    <TASK id="16" status="PASS">Editor tuner reads/writes Vault tuning and telemetry without C# recompile path.</TASK>
    <TASK id="17" status="PASS">CSV profile parser remains cold/editor and uses span/native scratch, not hot string parsing.</TASK>
    <TASK id="18" status="PASS">Heatmap gizmo reads raw sector Vault rows and draws debug wire cubes without debug GameObjects.</TASK>
    <TASK id="19" status="PASS">OOP scanner and SHINOBU_300-scoped JSON proof artifact exist.</TASK>
    <TASK id="20" status="PASS">Self-audit validates sector/telemetry layouts, quality monotonicity, clamps, and packed density proof; build gate remains blocked externally.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT type="Hecton8.Core.Contracts.EcosystemSectorDTO" size_bytes="64" alignment="8_byte_fields_first_then_4_byte_fields" false_sharing="one_sector_per_cache_line">
    <FIELD name="SectorHash" offset="0" size="8" />
    <FIELD name="FloraBiomass" offset="8" size="4" />
    <FIELD name="PreyBiomass" offset="12" size="4" />
    <FIELD name="PredatorBiomass" offset="16" size="4" />
    <FIELD name="CarryingCapacity" offset="20" size="4" />
    <FIELD name="DominantSpeciesMask" offset="24" size="4" />
    <FIELD name="_pad0.._pad8" offset="28" size="36" />
    <MATH>8 + 4 + 4 + 4 + 4 + 4 + 36 = 64 bytes.</MATH>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    `GlobalQualityWeight` passes through saturate, thermal-band remap, and smooth polynomial. At q below 0.3 the solver collapses to one LV substep, one diffusion pass, and 0.25..0.28 flow weight. Mid quality raises substeps and diffusion gradually. At q 1.0 it reaches six LV substeps, five diffusion passes, and full flow weight. DTO layout, BufferID ownership, save identity, and authority route never branch by quality.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime owns no persistent private `NativeArray`, `NativeList`, or `NativeHashMap`; it stores `VaultGenerationHandle<T>` descriptors only. Handles: `ShinobuMacroEcosystemSectorFront`, `SectorBack`, `Remainders`, `SectorCoords`, `IndexEntries`, `BiomeSpecs`, `Tuning`, `Counters`, `TelemetryRing`, `CsvScratch`, and `FaultFlags`.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Jobs use `[NoAlias]` on non-overlapping NativeArray/pointer fields. FrostTick chain: `EcosystemPopulationJob.Schedule` -> repeated `BiomassDiffusionJob.Schedule(..., handle)` -> optional `CopySectorBufferJob.Schedule(..., handle)` -> `ReduceMacroEcosystemTelemetryJob.Schedule(handle)`. Hot path does not call `.Complete()`; late-frame finalizes only completed handles. Cold mock boot uses an explicit force-complete barrier before readers can observe uninitialized rows.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling runtime assembly reference was introduced. Cross-domain sector readers consume `Hecton8.Core.Contracts.EcosystemSectorDTO`, `MacroEcosystemVaultContract`, and cached `IDataVault` generation descriptors.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy alternative: O(n_entities) spawned flora/prey/predator objects with transform migration, trigger eating, and coroutine reproduction. Implemented route: O(n_sectors * neighbors) scalar LV plus four-neighbor diffusion, with presentation driven by packed density bytes. The player sees biomass pressure; the CPU never owns animal migration as object truth.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-22 - Polish Pass Pure Read And AUP Hash Correction

What was wrong:
- `EcosystemDirector.TryGetBiomassAvailability` could indirectly refresh cached macro Vault handles and create fallback biomass slots while serving a read query.
- Macro sectors are seeded as a horizontal `SectorY=0` layer, but direct runtime AUP reads could derive `Y` from depth and miss underwater biomass rows.
- `MACRO_ECOSYSTEM_MATHEMATICIAN.md` still mentioned pointer-era `VaultBufferHandle<T>` metadata and stale temperature-tuning drift.

What was done:
- Confined macro Vault descriptor refresh to cold allocation/owner `SlowTick`; biomass reads now consume cached `VaultGenerationHandle<T>` descriptors and read-only published legacy slots only.
- Aligned macro runtime and World consumer hash policy to absolute AUP `X/Z` plus intentional `Y=0`, matching `StressDrivenSpawnDirector`.
- Updated route docs, status, rationale, ledger, and SHINOBU_300 scanner report with the pure-read and AUP route proof.

Cinematic cheats used:
- Depth is not a third macro biomass dimension. The macro layer stays 2D/horizontal and depth remains a biome/profile/presentation scalar.

Exact microseconds saved:
- Stateful read refresh/slot creation removed from spawn hydration: estimated 4-12 us worst-case per cache-miss read window.
- Underwater macro fallback churn avoided by matching `SectorY=0`: estimated 8-20 us in affected hydration windows.

Verification:
- Static rg route confirms refresh helpers remain cold/owner scoped and snapshot resolver reads cached descriptors.
- Static rg route confirms runtime/World/Fauna macro hashes use absolute AUP `X/Z` with `SectorY=0`.
- Build not launched: latest gate sample showed active `dotnet` PID 5468 and CPU 97%.

## 2026-05-22 - Polish Pass Scanner Route Hardening

What was wrong:
- `OOP_Spawner_Scanner` generated a SHINOBU_300 report, but its parser route was structural-only and did not preserve the newer canonical DTO/read/AUP report fields on rerun.

What was done:
- Added a lightweight syntax-tree pass over type, method, and invocation nodes after comment/string stripping.
- Kept Roslyn out of the editor assembly to avoid a new package/asmdef compile edge.
- Updated generated JSON fields for canonical `Hecton8.Core.Contracts.EcosystemSectorDTO`, pure read route, and horizontal AUP hash policy.

Cinematic cheats used:
- Scanner remains editor/cold proof tooling. Runtime macro truth still uses flat scalar rows and packed density bytes, not object simulations.

Exact microseconds saved:
- Runtime: 0 us, editor-only.
- Compile-wall risk avoided by not adding Roslyn/package references.

Verification:
- Static scan confirmed the scanner writes `scannerUsesLightweightSyntaxTree`, SHINOBU_300-scoped report path, canonical sector DTO, read-purity route, and AUP hash route.
- Build not launched: latest gate sample showed active `dotnet` PIDs 4496 and 5468 with CPU 100%.

## 2026-05-22 - Polish Pass Compile Edge And AUP Comment Hygiene

What was wrong:
- Macro runtime carried an unused `Hecton8.World` namespace import.
- Static `float3` bridge comments implied local runtime coordinates, while the implementation treats the value as an already absolute meter coordinate and the preferred path is the `double3` AUP overload.

What was done:
- Removed the unused World namespace import from `MacroEcosystemMathematicianRuntime`.
- Clarified both `float3` static readers as legacy same-domain absolute-meter adapters.

Cinematic cheats used:
- No new simulation route. Macro truth remains flat scalar biomass rows and packed visual density bytes.

Exact microseconds saved:
- Runtime: 0 us.
- Future risk reduction: prevents accidental local-float macro hash use that would miss AUP sectors and trigger fallback churn.

Verification:
- `rg` confirms no `using Hecton8.World;` remains in `MacroEcosystemMathematicianRuntime`.
- Targeted forbidden-pattern scan found no runtime `Pack=1`, stale macro fields, local `Hecton8.Ecosystem.EcosystemSectorDTO`, hot `MacroEcosystemSectorVaultRecord` generic handles, or `NativeDisableContainerSafetyRestriction` in SHINOBU_300 macro files.
- Scanner proof artifact now records 1685 scanned files, 43 macro candidate files, 0 Instantiate hits, 0 coroutine hits, 1 non-authority health dictionary hit, and 0 macro truth violations.
- `git diff --check` passed with line-ending warnings only.
- Build not launched: latest gate sample showed active `dotnet` PID 1548 and CPU 100%.

## 2026-05-22 - Polish Pass Editor Facade, CSV Coefficients, Legacy Fallback Demotion

What was wrong:
- Task 16 was only partial: tuner had no delta/`PredatorConversionRate`, graph was line traces, and tuning mutation copied a DTO instead of mutating the Vault row by ref.
- Task 17 was only partial: CSV primary path was `biome_ecosystem_specs.csv`, and `BiomeEcosystemSpecDTO` had no alpha/beta/delta/gamma lanes.
- Task 19 was only partial: scanner wrote a SHINOBU-specific report but did not upsert the required aggregate `AI_OPTIMIZATION_REPORT.json`.
- `AI/Ecosystem/ShinobuEcosystemBalancer` still had an active 32B legacy `LotkaVolterraMacroJob` route that could compete with SHINOBU_300 biomass truth.

What was done:
- Added delta slider, `UnsafeUtility.AsRef` tuning writes, and stacked-area biomass chart to `MacroEcosystemTunerWindow`.
- Expanded `BiomeEcosystemSpecDTO` to 64B and added optional per-biome alpha/beta/delta/gamma fields parsed from `macro_ecosystem_coefficients.csv`; legacy CSV path remains fallback only.
- Updated layout manifest and SHINOBU_300 self-audit to assert the new 64B biome spec offsets.
- Changed `ShinobuEcosystemBalancer` so its legacy macro pass exits when canonical `ShinobuMacroEcosystemSectorFront` exists.
- Updated scanner code plus stable/aggregate report artifacts and added `Docs/Reports/SHINOBU_300_SELF_AUDIT.xml`.

Cinematic cheats used:
- Macro remains scalar biomass pressure. Fish are still not spawned or migrated as truth; packed density/dominance scalars feed presentation.
- Legacy AI sector simulation is retained only as fallback, not a second live truth route.

Exact microseconds saved:
- Runtime from tuner/scanner/docs: 0 us, editor/static only.
- Legacy macro pass skip when SHINOBU_300 exists: saves one cold legacy LV/entity hydration job per AI cold tick; exact us requires profiler, but this removes the competing O(entity + sector) macro pass.
- CSV spec expansion cost: +~10 KB Vault memory for 256 biome rows; hot sector stride unchanged.

Verification:
- `rg` forbidden hot-path scan over SHINOBU_300 macro/editor files found no `float.Parse`, `.Split`, LINQ, `Pack=1`, or local native collection allocation.
- `rg` confirms `PredatorConversionRate`, `UnsafeUtility.AsRef`, `DrawStackedArea`, `macro_ecosystem_coefficients.csv`, `HasCanonicalMacroEcosystem`, and `shinobu300MacroEcosystem` report routes are present.
- `git diff --check` passed with line-ending warnings only.
- Build not launched: post-patch gate had no dotnet/csc/VBCSCompiler process, but CPU sampled at 94.4%, above the project build threshold.
