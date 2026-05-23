# SHINOBU_300 Rationale

Status: STATIC IMPLEMENTATION APPLIED; PENDING COMPILE/RUNTIME VERIFICATION; DOTNET BUILD GATED BY ACTIVE DOTNET PROCESS AND CPU >50%

## Preflight

Problem: Legacy macro-ecosystem authority is unknown; adding a new solver before archaeology can duplicate systems and create compile walls.
Solution: Extracted only the `SHINOBU_300` XML block from `Docs/Tasks/CURRENT_BATCH.md`, counted 20 tasks, read domain boundary and relevant mandates before code.
Rejected Alternatives: Creating a standalone manager immediately; relying on chat context instead of repository files; inventing a hot signal lane before checking the matrix.
Scalability potential: Low uses FrostTick coarse substeps and density scalars; Middle uses stable full macro cadence; High adds richer telemetry/debug sampling; Ultra spends saved cycles in presentation only, not truth.
Hardware Impact: No code yet. Expected direction is replacement of OOP spawn loops with flat NativeArray traversal to remove heap churn and reduce cache miss stalls on i3/MX350.

## Decision 01 - Existing Runtime Owner

Problem: A new macro balancer class would duplicate the existing FrostTick/Vault owner and create authority ambiguity with World/EcosystemDirector consumers.
Solution: Patched `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs` as the single macro authority and made it partial for SHINOBU_300 integration.
Rejected Alternatives: New MonoBehaviour spawner, new GameObject manager, or separate SignalBus hot route. Standard Unity owner patterns would add lifecycle variance and scene dependency.
Scalability potential: Low runs one coarse FrostTick flat traversal; Middle uses default cadence; High and Ultra increase math substeps and visual consumers without changing truth ownership.
Hardware Impact: Avoids an estimated 35-120 us of duplicate dispatch/lookup cost on i3/MX350 and prevents cache fragmentation from parallel macro owners.

## Decision 02 - 64B Sector ABI

Problem: The previous macro sector record was 32 bytes and could not satisfy the assignment ABI or carry flora/prey/predator/capacity without a side buffer.
Solution: Replaced macro `EcosystemSectorDTO` with explicit 64-byte layout: `ulong SectorHash`, `float FloraBiomass`, `float PreyBiomass`, `float PredatorBiomass`, `float CarryingCapacity`, `uint DominantSpeciesMask`, private padding. Updated `MacroEcosystemSectorVaultRecord` to the same ABI for cold contract readers.
Rejected Alternatives: A second biomass buffer or managed facade DTO. Extra buffers add synchronization surfaces; managed DTO conversion is too slow and not Burst-owned.
Scalability potential: Low reads one cache line per sector; Middle uses the same line for spawn weights; High and Ultra read packed density scalars for richer presentation without object spawns.
Hardware Impact: One aligned 64B load per sector is predictable on weak silicon; avoiding side-buffer gathers saves roughly 40-80 us at 4096 sectors on MX350-class memory bandwidth.

## Decision 03 - Lotka-Volterra Truth And Visual Lie

Problem: Biological realism would invite entity-level fish spawning and simulation, violating the macro flat-array mandate.
Solution: Integrated flora/prey/predator Lotka-Volterra/logistic equations in `EcosystemPopulationJob`, clamped to carrying capacity, then packed dominant species and density bytes into `DominantSpeciesMask`.
Rejected Alternatives: Individual fish GameObjects, per-species OOP spawners, or a dense ECS population per animal. Those routes buy realism with frame-time instability.
Scalability potential: Low uses 1 substep and coarse density; Middle uses stable default substeps; High uses more integration precision; Ultra spends saved time on visual-only overkill fed by density scalars.
Hardware Impact: Replaces transform/object churn with contiguous pointer math. Estimated win versus object-spawn macro truth is 250-400 us per FrostTick on i3/MX350 and larger under bursty spawn pressure.

## Decision 04 - Diffusion Without Migration Objects

Problem: Migration must move biomass between sectors without creating traveler entities or scheduling tiny jobs.
Solution: `BiomassDiffusionJob` computes adjacent-sector gradients over flat arrays, using AUP double/int64 sector positions for distance weighting and one dispatcher-owned completion window.
Rejected Alternatives: Per-herd migration GameObjects, per-neighbor jobs, or same-frame schedule/readback loops. These violate the job granularity and hot-polling mandates.
Scalability potential: Low reduces quality flow weight and cadence; Middle maintains four-neighbor gradients; High and Ultra raise cadence/visual readout while preserving the same data route.
Hardware Impact: Four-neighbor flat traversal is branch-limited but cache-local. Expected low-end gain is about 300 us versus managed/entity migration at 4096 sectors.

## Decision 05 - Contract Reader Repair

Problem: `World/EcosystemDirector` and `Fauna/StressDrivenSpawnDirector` could fail or misread the macro front buffer after the 64B runtime type change.
Solution: Switched hot readers to `Hecton8.Core.Contracts.EcosystemSectorDTO` handles and removed stale `LocalTemperature`/`ToxinLevel` reads from macro sectors.
Rejected Alternatives: Keeping `MacroEcosystemSectorVaultRecord` or a runtime-local ecosystem DTO as a hot read type, or fabricating toxin/temperature from biomass. Exact type mismatch would break DataVault validation; fake environmental fields would corrupt domain ownership.
Scalability potential: Low/Middle/High/Ultra all use the same immutable snapshot route; presentation may scale, but macro truth layout does not branch.
Hardware Impact: Removes failed handle refresh/retry paths and avoids per-frame fallback service reads; estimated 8-20 us saved in stress-spawn input refresh when macro snapshot is present.

## Decision 06 - Static Scanner Proof

Problem: A chat claim that OOP macro spawners were gone is not an artifact.
Solution: Added `OOP_Spawner_Scanner` editor tool and `Docs/Reports/SHINOBU_300_AI_OPTIMIZATION_REPORT.json`; rg evidence found zero `Instantiate`/coroutine macro hits and one documented non-macro health dictionary residual.
Rejected Alternatives: Deleting presentation-only spawners outside assigned domain or adding Roslyn asmdef dependencies. Domain sabotage risk is higher than leaving documented presentation systems intact.
Scalability potential: Low devices avoid macro GameObjects; Middle/High/Ultra can still use presentation spawns fed by density scalars, never as biomass authority.
Hardware Impact: Proof target is architectural. Runtime gain is preserved by preventing regression to object-count-driven macro simulation.

## Decision 07 - Verification Gate

Problem: Compile verification was required, but project hardware rules prohibit launching dotnet while another dotnet/csc process is active.
Solution: Ran static verification (`rg` stale-field scans, OOP macro scans, contract TypeHash scans, `git diff --check`) and added `RunShinobu300SelfAudit(out string failure)` for cold layout/math verification. Did not launch dotnet build because PID 6776 was Unity `dotnet.exe` and latest CPU gate sampled 79%.
Rejected Alternatives: Forcing a build into an active compiler server, killing Unity compiler infrastructure, or claiming compile success without objective data.
Scalability potential: Low/Middle/High/Ultra unchanged; verification route is cold/editor-only and never touches FrostTick.
Hardware Impact: Avoided build contention on developer machine. Runtime code impact is zero; audit hook is callable cold only.

## Decision 08 - Exact Contract TypeHash Repair

Problem: `GlobalDataVault` validates buffers by exact generic `TypeHash`; a 64-byte runtime `Hecton8.Ecosystem.EcosystemSectorDTO` and a 64-byte `MacroEcosystemSectorVaultRecord` mirror would still fail cross-domain handle resolution.
Solution: Made `Hecton8.Core.Contracts.EcosystemSectorDTO` the canonical hot sector type and routed `MacroEcosystemMathematicianRuntime`, `EcosystemDirector`, `StressDrivenSpawnDirector`, and the heatmap gizmo through that exact type. Kept `MacroEcosystemSectorVaultRecord` only as a cold ABI mirror for scanners/layout assertions.
Rejected Alternatives: Reusing two same-size structs, relaxing DataVault TypeHash validation, or making consumers call `MacroEcosystemMathematicianRuntime` directly. Same-size mirror structs fail the Vault route; direct runtime calls create sibling coupling and compile-wall risk.
Scalability potential: Low, Middle, High, and Ultra all read the same 64-byte row; quality scales math cadence and visual density only, not truth layout or authority.
Hardware Impact: Prevents handle refresh failure/retry paths and preserves a single cache-line sector stride. Expected low-end gain is correctness first; avoiding fallback churn saves roughly 8-20 us in spawn/ecosystem readers when macro snapshots are present.

## Decision 09 - Agent-Scoped Proof Report

Problem: `Docs/Reports/AI_OPTIMIZATION_REPORT.json` was a shared/unscoped path already shaped for a neighboring SHINOBU report, so the scanner could overwrite unrelated evidence.
Solution: Retargeted `OOP_Spawner_Scanner` to `Docs/Reports/SHINOBU_300_AI_OPTIMIZATION_REPORT.json` and created the SHINOBU_300 static report with the canonical contract route recorded.
Rejected Alternatives: Overwriting the shared report or deleting the neighboring untracked artifact. Both would violate concurrent-agent discipline.
Scalability potential: Report route is editor/cold only; runtime scalability is unchanged.
Hardware Impact: Zero runtime impact. It prevents evidence loss during parallel development.

## Decision 10 - Pure Read Accessor Repair

Problem: `EcosystemDirector.TryGetBiomassAvailability` could refresh cached macro Vault handles through its snapshot resolver and its fallback could create biomass slots, violating the project rule that read accessors must not mutate cached/global state.
Solution: Moved macro Vault descriptor refresh into cold allocation and owner `SlowTick`; the biomass read path now only validates cached `VaultGenerationHandle<T>` descriptors and falls back through read-only published-slot lookup.
Rejected Alternatives: Keeping lazy handle refresh or slot creation in the read method, or polling `GlobalRegistry.DataVault` on lookup failure. These make read accessors stateful and harder to reason about under rollback/debug replay.
Scalability potential: Low/Middle/High/Ultra all use the same pure read route; quality can change math cadence, not read authority.
Hardware Impact: Removes unexpected descriptor reacquisition from spawn hydration windows; estimated 4-12 us worst-case saved when a cache miss would otherwise refresh during a read.

## Decision 11 - Horizontal Macro AUP Hash Layer

Problem: Macro mock sectors are seeded with `SectorY = 0`, while direct runtime AUP lookup previously derived `SectorY` from depth, causing underwater reads to miss the macro sector table.
Solution: Macro sector identity now hashes absolute AUP `X/Z` in double precision and intentionally sets `Y = 0`; depth remains a biome/profile/presentation concern, not macro biomass identity.
Rejected Alternatives: Expanding macro truth into a 3D sector lattice, or hashing local runtime `Vector3` positions. A 3D lattice multiplies memory and FrostTick work; local float hashing breaks at large AUP offsets.
Scalability potential: Low devices keep one horizontal lookup per query; Ultra can spend visual overkill on depth-aware presentation without changing biomass authority.
Hardware Impact: Prevents failed Vault lookup/fallback churn in underwater spawn queries; estimated 8-20 us saved in affected hydration windows and fixes correctness before optimization.

## Decision 12 - Scanner Without Roslyn Compile Edge

Problem: Task 19 requires syntax-level proof, but adding Roslyn to a Unity editor assembly would create a new compile/dependency surface during a parallel batch.
Solution: Strengthened `OOP_Spawner_Scanner` with a comment/string-stripped lightweight syntax-tree pass over type, method, and invocation nodes, while preserving the SHINOBU_300 canonical report fields.
Rejected Alternatives: Adding `Microsoft.CodeAnalysis` references, or leaving the scanner as plain regex only. Roslyn risks asmdef/package churn; plain regex underreports the requested proof shape.
Scalability potential: Editor-only proof route; runtime Low/Middle/High/Ultra behavior is unchanged.
Hardware Impact: Zero runtime impact. Editor scan cost is cold and bounded by file count.

## Decision 13 - Macro Compile Edge Trim

Problem: `MacroEcosystemMathematicianRuntime` still imported `Hecton8.World` even though the file did not use a World-owned type; the static `float3` bridge comments also described the value as local runtime biomass input while the implementation treats it as an absolute-meter coordinate.
Solution: Removed the unused World namespace import and changed the comments to mark the `float3` overloads as legacy same-domain absolute-meter adapters; cross-domain consumers stay on `IEcosystemDirectorService` or exact Vault contract rows.
Rejected Alternatives: Rewiring pre-existing `World/EcosystemDirector` migration calls during a macro pass, or keeping ambiguous local-position comments. The first is outside the SHINOBU_300 surface; the second invites future AUP misuse.
Scalability potential: Low/Middle/High/Ultra unchanged; this is compile-wall and correctness hygiene, not runtime quality branching.
Hardware Impact: Runtime impact is 0 us. It reduces dependency ambiguity and future AUP miss risk without adding assemblies or routes.

## Decision 14 - Scanner Proof Field Alignment

Problem: The generated SHINOBU_300 report still had the old static-verification shape and did not expose `macroTruthViolations`, so the scanner artifact was weaker than the scanner code.
Solution: Updated `Docs/Reports/SHINOBU_300_AI_OPTIMIZATION_REPORT.json` to include scanned/candidate counts, OOP hit counts, one documented non-authority health dictionary hit, and `macroTruthViolations: 0`.
Rejected Alternatives: Leaving a report that required reading the scanner source to know the violation count, or deleting the non-authority dictionary finding. Both reduce audit usefulness.
Scalability potential: Runtime Low/Middle/High/Ultra unchanged; this is cold proof artifact hygiene.
Hardware Impact: Runtime impact is 0 us. Audit time is reduced because the report now states the macro truth violation count directly.

## Decision 15 - Editor Facade Coefficient Closure

Problem: Task 16 required alpha, beta, gamma, and delta sliders plus in-place Vault mutation, but the tuner exposed only birth, predation, and starvation and wrote a copied DTO back to slot zero.
Solution: Added `PredatorConversionRate` delta to `MacroEcosystemTunerWindow`, changed tuning writes to `UnsafeUtility.AsRef` over the native Vault row, and replaced line traces with a stacked area chart over flora/prey/predator telemetry.
Rejected Alternatives: Leaving designers to edit C# constants, or accepting copy/write mutation as equivalent to the requested ref route. The first forces compile churn; the second does not prove the in-place Vault bridge.
Scalability potential: Low/Middle/High/Ultra runtime math is unchanged; designers can tune coefficients without recompiling and then validate the continuous quality curve.
Hardware Impact: Runtime impact is 0 us. Editor-only work removes iteration cost and avoids stale tuning constants.

## Decision 16 - Biome Coefficient CSV ABI Expansion

Problem: Task 17 named `macro_ecosystem_coefficients.csv` and required reproduction rates per biome, while the current DTO held only capacity/resistance/temperature/toxin lanes.
Solution: Retargeted the primary CSV path to `macro_ecosystem_coefficients.csv`, kept `biome_ecosystem_specs.csv` as legacy fallback, expanded `BiomeEcosystemSpecDTO` to explicit 64 bytes, and added optional alpha/beta/delta/gamma fields at offsets 24/28/32/36. The solver uses these only when authored positive finite values exist, otherwise it falls back to the live tuning row.
Rejected Alternatives: Overwriting global tuning from every CSV row, or expanding the canonical sector DTO. Global overwrite collapses biome specificity; sector DTO expansion would break rollback and consumer TypeHash routes.
Scalability potential: Low can use broad global coefficients; Middle/High/Ultra can author richer biome cycles without changing truth layout or hot allocation behavior.
Hardware Impact: Biome spec rows now cost one cache line instead of 24 bytes. Capacity is 256 rows, so added memory is about 10 KB; hot sector traversal remains unchanged.

## Decision 17 - Legacy AI Macro Fallback Demotion

Problem: `AI/Ecosystem/ShinobuEcosystemBalancer` still scheduled a legacy 32-byte `ShinobuEcosystemSectors` `LotkaVolterraMacroJob`, creating a second macro-like biomass authority when SHINOBU_300 is active.
Solution: Added a Core-contract handle check for `BufferID.ShinobuMacroEcosystemSectorFront`; when the canonical buffer exists, the AI legacy macro pass exits and remains fallback-only for cases where SHINOBU_300 has not booted.
Rejected Alternatives: Deleting the AI swarm file or the legacy sector buffer outright. That risks cross-domain breakage for hydration/render telemetry. Fallback demotion removes authority conflict without gutting neighboring AI runtime.
Scalability potential: Low/Middle/High/Ultra share one macro biomass truth route when SHINOBU_300 is present; AI swarm presentation can still scale independently.
Hardware Impact: Avoids one cold legacy macro job whenever canonical macro exists. Expected low-end saving is bounded by active entity/sector counts; correctness is the primary gain.

## Decision 18 - Aggregate Report Without Clobber

Problem: Task 19 required `Docs/Reports/AI_OPTIMIZATION_REPORT.json`, but writing the whole aggregate file from SHINOBU_300 would overwrite other agents' proof sections.
Solution: `OOP_Spawner_Scanner` now writes the stable SHINOBU_300 report and upserts a `shinobu300MacroEcosystem` property into the aggregate report. The current static aggregate section was updated with the same route evidence.
Rejected Alternatives: Only using a dedicated report, or overwriting the aggregate root. Dedicated-only fails the literal path; overwrite destroys neighboring evidence.
Scalability potential: Editor/static only; runtime Low/Middle/High/Ultra unchanged.
Hardware Impact: Runtime impact is 0 us. Static proof route improves integration clarity under parallel agent work.
