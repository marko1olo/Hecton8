# SHINOBU_258 Rationale

Date: 2026-05-20
Status: ACTIVE

## Decision 1

Problem: The user-provided polish mandate used placeholder `[YourID]`, but `CURRENT_BATCH.md` contains the concrete Data Monolith validator assignment.
Solution: Adopt `SHINOBU_258` as the active identity and restrict implementation to external `.h8bin` validation.
Rejected Alternatives: Continuing under `ARCH_AUDIT` would blur documentation audit work with binary validator implementation.
Scalability potential: External validation catches bad static payloads before low-end devices pay runtime fault cost and before high/ultra content tables amplify bad data.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI/editor-only tool.

## Decision 2

Problem: The XML prompt example says magic `H8BN`, while current source defines `H8DataLayoutConstants.BlobMagic = 0x4D443848u`.
Solution: Treat `H8DM` little-endian bytes `48 38 44 4D` as the authoritative magic and make the validator source-derived where possible.
Rejected Alternatives: Implementing prompt example magic would reject every real project Data Monolith payload.
Scalability potential: Source-truth validation prevents cross-platform bootstrap divergence.
Hardware Impact: Runtime impact 0 us on i3/MX350; prevents invalid payload boot attempts.

## Decision 3

Problem: The validator must verify checksums without Unity or C# runtime access.
Solution: Port Unity Collections `xxHash3.Hash64` to Python stdlib and hash `bytes[16..end)` via `mmap`.
Rejected Alternatives: Importing Unity assemblies violates standalone CI execution; requiring the external `xxhash` package adds an undeclared dependency on headless CI.
Scalability potential: CI can validate large payloads on cheap runners without loading Unity.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI memory pressure stays bounded by OS page cache.

## Decision 4

Problem: A validator that hardcodes section sizes would drift as soon as Data Monolith C# layouts change.
Solution: Parse source constants, `H8DataSectionId`, `SectionOrder`, `StructLayout(LayoutKind.Explicit, Size=...)`, `FieldOffset`, and `GetExpectedRecordSize` from C# files at runtime.
Rejected Alternatives: A fixed Python table would pass stale data and contradict the SHINOBU_258 prompt.
Scalability potential: Low/mid/high/ultra content tables can grow without changing the validator as long as C# source remains the authority.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI-only parse cost.

## Decision 5

Problem: The prompt demands statistical payload validation without turning CI into a 2GB full-scan bottleneck every local run.
Solution: Full header, directory, section, checksum, and alignment proof always run; payload records use deterministic 5% sampling by default, with `--thorough` for nightly/full validation.
Rejected Alternatives: Full payload unpacking on every quick local check would punish iteration and invite developers to skip the validator.
Scalability potential: Cheap runners get fast structural proof; nightly servers can spend time for full record validation.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI read bandwidth scales with sample mode.

## Decision 6

Problem: Current `Assets/StreamingAssets` is not Data Monolith ready.
Solution: Run the new validator against the current tree and record the red gate: three unbaked CSV artifacts, missing `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, and zero `.h8bin` files.
Rejected Alternatives: Marking the tool successful without checking current target would hide the actual production payload gap.
Scalability potential: Prevents low-end runtime boot from silently accepting source CSVs or absent monolith bytes.
Hardware Impact: Runtime impact 0 us on i3/MX350; prevents invalid payload boot path from reaching runtime.

## Decision 7

Problem: Task 17 could become decorative if CSV-to-bin diff only parsed CSV and did not inspect binary item hashes.
Solution: Added `--csv-diff SOURCE_CSV GENERATED_H8BIN` that extracts `Items.HashId` from the target blob and fails when source hashes are absent from the binary.
Rejected Alternatives: A report-only CSV parser would not prove bake propagation.
Scalability potential: Designers can verify balance CSV bake propagation without launching Unity.
Hardware Impact: Runtime impact 0 us on i3/MX350; editor/CI-only.

## Decision 8

Problem: `UNBAKED_ARTIFACT` findings were fatal but initially did not tell the owner which path must be changed.
Solution: Add remediation text and active references to each text-artifact finding. The scan is intentionally limited to `Assets/_Project/Scripts` and `Docs/ARCHITECTURE` to avoid slow archival log traversal.
Rejected Alternatives: Scanning all `Docs/AgentLogs` and `Docs/Archive` found historical context but pushed validation too close to the 15 second CI warning budget.
Scalability potential: CI output now points low-level runtime owners to source-only/baked-binary routes without increasing gameplay cost.
Hardware Impact: Runtime impact 0 us on i3/MX350; current CI scan is 10.158064 seconds on the local checkout.

## Decision 9

Problem: The validator proof existed in tool output and SHINOBU docs, but the central binary payload ledger still only said the Data Monolith file was absent. Future owners could miss that StreamingAssets itself is currently blocked by unbaked text artifacts.
Solution: Add a SHINOBU_258 entry to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` naming the validator, the current JSON proof, the failing CSV runtime artifacts, and the missing `static_data.h8bin` route.
Rejected Alternatives: Leaving the evidence only in `Docs/Reports` would make the red gate easy to ignore during architecture review.
Scalability potential: Low-tier devices avoid text parsing and missing-payload boot failure; middle/high/ultra keep one binary authority path for richer static data tables.
Hardware Impact: Documentation-only change. Runtime impact 0 us on i3/MX350 until owners move the CSV inputs and bake binary payloads.

## Decision 10

Problem: Removing CSV files from `StreamingAssets` is not enough if runtime C# still contains cold-loader code paths that can reintroduce text truth with one future asset check-in.
Solution: Add a runtime source scan to `Tools/h8bin_validator.py`. It skips `Editor` folders, flags `.csv/.json/.xml` `StreamingAssets` loaders as `RUNTIME_TEXT_STREAMINGASSETS_LOAD`, and allows explicit override only with `--allow-runtime-text-loaders`.
Rejected Alternatives: A filesystem-only gate catches current files but misses latent runtime readers. Editing those domain readers in this pass would cross SHINOBU_258's external-validator boundary and require Unity compile ownership.
Scalability potential: Low-tier devices avoid cold text parsing and string/file churn; high/ultra keep static data growth on binary/Vault routes instead of parallel CSV contracts.
Hardware Impact: Runtime impact 0 us on i3/MX350 from this tool change. Current CI gate is 5.395143 seconds after cache and bytes-first scan optimization.

## Decision 11

Problem: The first runtime source scan pushed the current gate to 15.631635 seconds and logged a performance warning, which violates the validator's own CI speed target.
Solution: Cache artifact-reference source text once per process, then scan runtime C# via `os.walk` with `Editor` directory pruning and bytes-level early rejection before UTF-8 line splitting.
Rejected Alternatives: Keeping the slow scan would train developers to bypass the validator. Using external `rg` would be faster locally but violates standalone Python CI assumptions.
Scalability potential: Cheap CI runners keep fast proof while nightly `--thorough` remains available for deeper payload checks.
Hardware Impact: Runtime impact 0 us on i3/MX350. Current validator wall time dropped from 15.631635s to 5.395143s on this checkout.

## Decision 12

Problem: The JSON report has exact findings, but domain owners need a stable architecture route card summarizing what must move and what must replace it.
Solution: Add `Docs/ARCHITECTURE/STREAMINGASSETS_TEXT_RUNTIME_MIGRATION_SHINOBU_258.md` with the current four filesystem violations, thirteen runtime loader categories, and a six-step migration contract.
Rejected Alternatives: Chat-only remediation would be lost. Editing all owning runtime systems now would cross domain boundaries and require Unity compile ownership outside SHINOBU_258.
Scalability potential: Each domain can migrate to binary/Vault ownership without reintroducing text parsing on low-tier hardware or divergent data routes on high-tier content sets.
Hardware Impact: Documentation-only change. Runtime impact 0 us on i3/MX350 until the owning domains remove the loaders.

## Decision 13

Problem: A literal `<AGENT_PROMPT id="SHINOBU_258">` extraction fails because the current batch tag includes `role` and `chat_name` attributes.
Solution: Re-extract with an attribute-aware regex: `<AGENT_PROMPT\b[^>]*id="SHINOBU_258"[^>]*>.*?</AGENT_PROMPT>`.
Rejected Alternatives: Treating the prompt as absent would contradict the current `CURRENT_BATCH.md`; reading neighboring prompts would contaminate the domain.
Scalability potential: Correct prompt anchoring prevents cross-domain edits and keeps the validator scoped to external binary CI proof.
Hardware Impact: Runtime impact 0 us on i3/MX350; documentation/process proof only.

## Decision 14

Problem: The current StreamingAssets report changed under concurrent work: a new Atmosphere CSV appeared under `Assets/StreamingAssets/Hecton8/storm_depth_impact_profiles.csv`, and manual remediation lists became stale immediately.
Solution: Add a deterministic `migration_summary` object to the JSON report. It groups `UNBAKED_ARTIFACT` and `RUNTIME_TEXT_STREAMINGASSETS_LOAD` blockers by route owner, source-data destination, and required binary route.
Rejected Alternatives: Moving CSV files out of `StreamingAssets` in this pass would cross owning domains and break fallback paths without a baked binary replacement. Chat-only owner summaries decay on the next report run.
Scalability potential: Low-tier devices get earlier removal of text parsing routes because owners can work from a machine-readable migration map; high/ultra data growth remains on binary/Vault routes instead of parallel CSV truth.
Hardware Impact: Runtime impact 0 us on i3/MX350. CI-only JSON grouping cost is below the file scan noise floor; the clean solo gate ran in 12.66832 seconds after the change.

## Decision 15

Problem: The current blocker set spans 9 route owners, so treating it as one Data Monolith failure hides ownership and invites cross-domain edits.
Solution: Classify owners as Atmosphere, Core/Origin, Core/Signals, Equipment/Auxiliary, Fauna, Power/Logistics, Thermodynamics, UI/TerminalOS, and World/OfflineHadalArchBaker in `migration_summary`.
Rejected Alternatives: A single generic "move CSV" remediation loses owner, source root, and binary route information. Editing all 14 loader sites would violate SHINOBU_258's external-validator domain.
Scalability potential: Each owner can migrate independently to source-data plus aligned `.h8bin`, preserving one fact -> one owner -> one route -> one proof artifact.
Hardware Impact: Runtime impact 0 us on i3/MX350. Future runtime savings are unclaimed until owning domains remove loaders and bake binary payloads.

## Decision 16

Problem: The validator work was correct but still left real runtime code paths capable of reading text data from `StreamingAssets`, so the user challenge about looping on Python scripts was valid.
Solution: Remove the lowest-risk runtime text routes directly: Signals tuning/capacity, Atmosphere weather/Beaufort, StormPropagation impact profiles, and TerminalOS layout now resolve to `Assets/_SourceData/...` behind editor/source-data guards or deterministic player fallbacks.
Rejected Alternatives: Allowlisting CSV files would preserve parallel runtime truth. Baking a full `static_data.h8bin` here would cross Data Monolith baker ownership and invent payload data outside SHINOBU_258's validator domain.
Scalability potential: Low devices avoid deployed text file IO/parsing; middle/high/ultra devices keep the same authority route and can receive richer rows once the binary bake exists.
Hardware Impact: Runtime frame impact 0 us claimed because these were cold routes. Cold boot and editor/player separation improve by removing future `StreamingAssets` text probes from cleaned domains.

## Decision 17

Problem: Player builds still need deterministic behavior after CSV probes are removed from runtime `StreamingAssets`.
Solution: Keep existing in-code/fixed fallback rows active in player builds while marking binary payload hydration as pending. This fails closed at the Data Monolith gate without breaking player boot by searching for missing text files.
Rejected Alternatives: Throwing hard runtime failures before `static_data.h8bin` exists would block unrelated playtests. Keeping player CSV parsing would violate Data Monolith doctrine.
Scalability potential: Low/mid/high/ultra all share the same fallback truth until binary hydration replaces it; `GlobalQualityWeight` remains a continuous fidelity scalar and does not change DTO layout or authority route.
Hardware Impact: Runtime hot path 0 us. Player cold file IO for the cleaned routes is removed; future binary hydration should replace fallback rows with mmap/Vault bytes.

## Decision 18

Problem: Architecture documents still reported the stale 5-artifact/14-loader gate after runtime route cleanup.
Solution: Update SHINOBU_258 docs, the binary payload ledger, and adjacent route cards to record the current 2-artifact/6-loader red gate and the exact remediated source-data paths.
Rejected Alternatives: Leaving stale evidence would make the next owner chase already-fixed routes or distrust the validator report.
Scalability potential: Accurate owner lists let remaining domains migrate independently without cross-domain source coupling or duplicated text truth.
Hardware Impact: Documentation-only. Runtime impact 0 us.

## Decision 19

Problem: After the first cleanup pass, six runtime text loader sites and two actual text artifacts still kept the StreamingAssets text gate red.
Solution: Convert Core/Origin, Fauna, Power, Thermodynamics, Auxiliary, and Hadal Forge text paths to `Assets/_SourceData/...` or editor-only routes, then physically move the remaining Auxiliary and Hadal CSVs out of `Assets/StreamingAssets`.
Rejected Alternatives: Leaving Auxiliary hidden behind a filename constant would let runtime text truth return without the validator seeing a direct `.csv` line. Deleting the CSVs would discard authored source data. Generating a placeholder `.h8bin` would fake payload readiness.
Scalability potential: Low devices no longer pay deployed text-file lookup/parsing for these routes; middle/high/ultra can receive richer authored tables once the binary bake owns them.
Hardware Impact: Runtime hot path 0 us. Cold player file IO for the cleaned CSV routes is removed. Current validator wall time dropped to 1.067837s because text scanning now has no blocker expansion work.

## Decision 20

Problem: Moving domain CSVs into `Assets/_SourceData` exposed a Data Monolith bake risk: the compiler's broad source root could parse unrelated domain files and then silently ignore unknown table names, allowing a structurally valid but semantically sparse blob.
Solution: Narrow `H8DataMonolithCompiler.SourceFolder` to `Assets/_SourceData/DataMonolith`, point the file watcher at that root, and document domain `_SourceData/*` folders as non-monolith inputs unless an explicit parser/section route exists.
Rejected Alternatives: Keeping broad `_SourceData` discovery would create accidental cross-domain input coupling. Moving every domain CSV into the monolith would violate one owner/one route and invent parser ownership outside SHINOBU_258.
Scalability potential: Low/mid/high/ultra builds keep one immutable static-data route instead of parallel source folders whose rows may or may not enter the blob.
Hardware Impact: Runtime impact 0 us on i3/MX350. Editor bake discovery scans fewer folders and avoids false source coupling.

## Decision 21

Problem: Unknown CSV table names were ignored by `ParseRow`, so a bake could pass while required production sections stayed empty.
Solution: Add an upfront `IsRecognizedCsvTable` gate in `ParseCsv` and throw on unrecognized monolith tables. Existing `Data/Balance/armor_penetration_matrix.csv` and `Data/Balance/btree_tuning_profiles.csv` remain explicit non-monolith cold-tuning exceptions because current source owners consume them directly.
Rejected Alternatives: Rejecting all unknown files under `Data/Balance` would break established cold tuning routes without moving their owners. Continuing silent ignore would let bad payloads reach CI as "valid."
Scalability potential: Production sections must be intentionally authored and parsed before low/high/ultra content growth can claim payload readiness.
Hardware Impact: Runtime impact 0 us on i3/MX350. The gate moves failure to editor bake time before any device loads bad bytes.

## Decision 22

Problem: There was no direct batchmode bake entrypoint for CI to produce and validate `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` outside the menu/build callback path.
Solution: Add `H8DataMonolithCompiler.BakeFromCommandLine()`, which runs `BakeAll`, validates the output blob, logs a concrete failure, and exits Unity batchmode with code 1 on failure.
Rejected Alternatives: Relying on a menu item is not CI automation. Running Unity bake in this pass was rejected because the current assignment is a source/docs/static proof pass and the active gate is already red on missing payload.
Scalability potential: CI can create the same immutable static blob for cheap and ultra devices instead of relying on local manual editor state.
Hardware Impact: Runtime impact 0 us on i3/MX350. CI/editor-only command path.

## Decision 23

Problem: A valid header, directory, section table, checksum, and alignment do not prove production static-data readiness if critical sections have zero rows.
Solution: Add `ValidateProductionSectionCoverage(dataSet)` before blob output. It rejects missing rows for Items, Creatures, Biomes, Recipes, LootCdf, VoxelMaterials, AudioClipRegistry, VfxScalars, DepthPressureCurve, ToolHeatCapacity, SubmarineHullConstants, PhysicsMaterials, GhostModules, SpawnCreditCosts, LightAttenuationCurve, SopErrors, HudLayouts, SectorPageDirectory, Economy, and PhysicsConstants.
Rejected Alternatives: Letting the external validator accept a sparse but structurally correct blob would convert a content-coverage failure into a runtime boot/content failure. Requiring Quest/Narrative/Radiation rows in this pass was rejected because their owner routes are not proven boot-critical in the current docs.
Scalability potential: Low-tier and ultra-tier builds now require the same immutable data coverage before payload generation; quality can scale fidelity, not whether truth exists.
Hardware Impact: Runtime impact 0 us on i3/MX350. Failure moves to editor bake before any device reads incomplete static data.

## Decision 24

Problem: A hard coverage exception is correct for CI, but a designer/editor user needs to see section counts and missing tables before pressing bake.
Solution: Add `TryAnalyzeProductionCoverage(...)` and a `Production Coverage` panel/action to `H8DataMonolithCompilerWindow`. The window reuses the same parser/finalizer/cross-reference path and does not write `static_data.h8bin`.
Rejected Alternatives: Keeping only post-bake exceptions slows authoring. Generating placeholder rows would fake production data and violate one fact -> one owner -> one route.
Scalability potential: Required sections are visible before low/high/ultra content expansion; quality tiers can add richness without changing the static truth route.
Hardware Impact: Runtime impact 0 us on i3/MX350. Editor-only source scan/report.

## Decision 25

Problem: The `Schemas` button generated templates only for Items, Fauna, Economy, and Physics even though the production coverage gate now requires additional sections.
Solution: Expand schema templates for Biomes, Recipes, LootCdf, VoxelMaterials, AudioRegistry, VfxScalars, ToolHeat, SubmarineHull, PhysicsMaterials, GhostModules, SpawnCredits, SopErrors, HudLayout, and SectorPages, plus optional Quest/Narrative/Radiation templates. The target folder remains `Data/Balance/Schemas`, which is excluded from compiler source discovery.
Rejected Alternatives: Leaving partial templates would make the new coverage gate correct but hostile. Writing templates into active source roots would accidentally create empty/placeholder runtime truth.
Scalability potential: Designers can author required base rows once, then scale density/fidelity through content values and `GlobalQualityWeight` without route drift.
Hardware Impact: Runtime impact 0 us on i3/MX350. Editor-only file generation when the user presses `Schemas`.

## Decision 26

Problem: A concurrent VFX task reintroduced `Assets/StreamingAssets/vehicle_wake_profiles.csv`, restoring a deployed text artifact and a cold reader route outside the binary payload contract.
Solution: Move the source CSV and `.meta` to `Assets/_SourceData/VFX/Propwash`, then compile propwash CSV staging buffers, background thread, file IO, and parser refresh only under `UNITY_EDITOR`. Player builds keep no-op lifecycle methods and deterministic default `PropwashWakeProfileDTO` rows until a VFX `.h8bin` or Data Monolith route hydrates `PropwashGpuWakeProfiles`.
Rejected Alternatives: Allowlisting the CSV would preserve parallel runtime truth. Leaving only an empty player path would depend on control flow instead of a compile-time source boundary. Deleting the CSV would discard designer source data.
Scalability potential: Low devices avoid deployed text IO and 24 KB of cold CSV staging buffers; middle/high/ultra can still use richer wake profile authoring in editor and later consume binary-hydrated rows without changing DTO layout or authority route.
Hardware Impact: Runtime hot path 0 us. Player cold allocation surface reduced by six 4 KB byte buffers plus two lock objects when compiled outside `UNITY_EDITOR`; Unity/player proof remains pending.

## Decision 27

Problem: The active Data Monolith payload is still absent, and current sources only cover Items, Fauna/Creatures, Economy, and PhysicsConstants. Generating placeholder production rows would create false payload readiness.
Solution: Materialize excluded schema headers in `Data/Balance/Schemas` and write `Docs/Reports/SHINOBU_258_DataMonolith_SourceCoverage.md` with current authored row counts and missing production sections. The compiler already excludes `Data/Balance/Schemas`, so these headers support authoring without satisfying the bake gate.
Rejected Alternatives: Baking a sparse or placeholder `static_data.h8bin` would greenwash runtime readiness. Moving domain `_SourceData` CSVs into the monolith would violate owner/route boundaries. Running Unity bake now would fail on missing production sections and consume editor time without new content.
Scalability potential: Low/mid/high/ultra builds keep one immutable static-data route; designers can fill the missing sections with real rows instead of hidden C# defaults or runtime text files.
Hardware Impact: Runtime impact 0 us on i3/MX350. This is editor/source authoring infrastructure only; the next measured runtime change comes after a real `.h8bin` exists.

## Decision 28

Problem: Row-count coverage can be faked because Data Monolith CSV parsers default blank IDs and blank references to hash `0`. A one-line empty template could satisfy the sparse-section gate while baking unusable static truth.
Solution: Add real seed rows for the missing required non-generated sections under `Data/Balance`, add compact filename aliases for schema-style CSV names, fix the `VoxelMaterials` schema header to `melting_point_c`, and harden `ValidateCrossReferences` with nonzero identity checks plus Biome/Voxel/PhysicsMaterial, Voxel/YieldItem, GhostModule/Recipe, SpawnCredit/Creature, SectorPage/Biome, and BiomeHeatmap/Biome semantic links.
Rejected Alternatives: Leaving the gate as count-only would greenwash content coverage. Generating a fake `static_data.h8bin` would bypass the real Unity bake route. Requiring `SectorPages.byte_count > 0` was deferred because the world-page byte payload route is not present; the rows are marked as directory coverage only.
Scalability potential: Low devices and high-end builds now share the same immutable authored truth before quality scaling; richer ultra content can add rows without changing authority route or DTO layout.
Hardware Impact: Runtime impact 0 us on i3/MX350. Editor bake failure moves bad data out of player boot; no runtime memory or hot-path code added.

## Decision 29

Problem: Even with real rows and semantic references, the compiler could still bake duplicated identities, NaN/Infinity floats, inverted depth ranges, zero audio bank hashes, impossible AUP sector coordinates, or useless item quantities. The external validator would catch some of this only after a bad `.h8bin` exists.
Solution: Add editor-bake validation for duplicate production hashes, finite numeric fields, positive critical item/physical/crafting quantities, nonzero audio banks, valid depth ranges, and sector AUP bounds. Unity batchmode bake was checked but not launched because the CPU guard sampled 99.815823 percent.
Rejected Alternatives: Running Unity bake under 99 percent CPU would violate the local build guard. Relying only on post-bake Python sampling leaves preventable source errors until after blob emission.
Scalability potential: Low/mid/high/ultra builds receive the same deterministic static truth; quality weight can scale fidelity without masking invalid source data.
Hardware Impact: Runtime impact 0 us on i3/MX350. Bad source rows fail in editor/CI before player boot and before any device maps corrupt payload bytes.

## Decision 30

Problem: Runtime owners in touched source-data routes still had normal player fallback paths through `GlobalDataVault.TryGetLatestCreated()` or a private standalone `GlobalDataVault.Create()`, which violates the new DataVault doctrine outside diagnostics/bootstrap.
Solution: Split `ShinobuOceanSurfaceAtmosphereRuntime` vault resolution into registered-owner and diagnostic-snapshot routes, then restrict owner/tuner paths to `GlobalRegistry.DataVault`. Gate `SubmarineOsThermalGridRuntime` latest-created and standalone-vault fallback behind `UNITY_EDITOR`; player builds now fail closed if no registered vault is injected.
Rejected Alternatives: Keeping latest-created as convenience runtime fallback preserves hidden authority. Removing editor fallback too would break existing tuner/smoke workflows without adding runtime safety.
Scalability potential: Low/mid/high/ultra builds now share one DataVault owner route; quality can scale ocean/power fidelity without swapping memory authority.
Hardware Impact: Runtime hot path 0 us. Player memory fragmentation risk is reduced by removing the 2 MB standalone Vault arena fallback from non-editor builds.

## Decision 31

Problem: `Data/Balance/HudLayout.csv` had more values than headers, and `ReadCsvRows` used `Min(headers, values)`, silently truncating authored data before bake.
Solution: Fix `HudLayout.csv` to the actual 64-byte `H8HudLayoutRecord` schema and make `ReadCsvRows` throw on any header/value count mismatch.
Rejected Alternatives: Allowing extra `m32/m33` cells would imply a 72-byte HUD matrix record that does not exist. Continuing truncation would produce false authoring proof.
Scalability potential: All tiers consume the same baked HUD layout record; richer UI variants must add explicit fields or sections, not hidden CSV tails.
Hardware Impact: Runtime impact 0 us. Editor/CI fails malformed CSV before `static_data.h8bin` emission.

## Decision 32

Problem: `FabricationAssemblerRuntime.ResolveVault()` used `GlobalDataVault.TryGetLatestCreated()` as a normal runtime fallback, which can bind fabrication state to an implicit latest-created owner instead of the registered DataVault service.
Solution: Remove the latest-created fallback from fabrication. The runtime now uses cached `_vault` or `GlobalRegistry.DataVault`, then fails closed until bootstrap/injection provides the service.
Rejected Alternatives: Keeping latest-created for "survival" would hide boot-order faults and create a second authority route for fabrication buffers.
Scalability potential: Low/mid/high/ultra builds keep the same Fabrication Vault route; quality can scale visual payloads without changing state ownership.
Hardware Impact: Runtime hot path 0 us. Removes one hidden global lookup/fallback path from the fabrication owner.

## Decision 33

Problem: Multiple normal runtime domains still selected `GlobalDataVault.TryGetLatestCreated()` when the registered DataVault service was missing. That preserved a hidden second owner route for gameplay, lighting, thermodynamics, ecosystem, construction, PDA, localization, UI, and mod sandbox state.
Solution: Replace those fallbacks with registered `GlobalRegistry.DataVault` resolution or fail-closed behavior in FaunaSimulationEngine, InteriorGIProbeVolumeRuntime, HectonGIRelaySystem, ThermodynamicsHazardGridRuntime, AbyssalThermodynamicsSolver, VehicleMotor, VolcanicUpdraftDirector, ChemicalInfluenceGrid, BiomeTransitionManagerRuntime, MigrationDirector, MacroEcosystemMathematicianRuntime, DroneFleetManager, PlayerExplorationTracker, ScavengingLootOracle, LocRegistry, DiegeticGlitchSurgeonRuntime, and FutureCommandSandboxValidator.
Rejected Alternatives: Keeping latest-created as a convenience fallback would mask boot/injection defects and let normal gameplay bind to whichever Vault happened to be created last. Creating local standalone Vaults was rejected as a global heap split.
Scalability potential: Low/mid/high/ultra builds now share one DataVault injection route; `GlobalQualityWeight` can change cadence/fidelity without changing memory authority.
Hardware Impact: Runtime hot-frame saving 0 us claimed. The meaningful gain is lower fragmentation and lower boot-order ambiguity on low-end silicon.

## Decision 34

Problem: The remaining latest-created callsites needed classification after the runtime cuts, and one gizmo route was diagnostic by intent but not compile-time guarded.
Solution: Classify the residual callsites as diagnostic/editor/smoke/crash routes and add `UNITY_EDITOR` guarding to `MacroEcosystemHeatmapGizmo.OnDrawGizmos()`. Keep `SignalWardenRuntime.EnsureInitializedForCrashDumpRoute()` and `CraftingRuntimeSmokeTester` latest-created uses because crash/postmortem and batch smoke routes are explicitly allowed by the doctrine.
Rejected Alternatives: Cutting crash dump fallback would reduce forensic coverage. Cutting smoke-test fallback would make batch validation dependent on scene/bootstrap order. Leaving the macro ecosystem gizmo unguarded would keep a player-build diagnostic ambiguity.
Scalability potential: Runtime tiers keep one owner route; editor diagnostics can inspect Vault state without becoming deployed truth.
Hardware Impact: Runtime hot-frame saving 0 us claimed. Player builds no longer compile the macro ecosystem heatmap latest-created probe.

## Decision 35

Problem: PDA cartography upload jobs were scheduled into `H8Memory.RegisterActiveJob(SystemID.UI, handle)`, but successful non-blocking finalization and forced structural drains did not clear the active-job registration. The same forced helper was also named as teardown while being used by buffer clear/init mutation paths.
Solution: Add a single completion marker that clears `_cartographyUploadPending` and registers `default` with `H8Memory`. Use it from non-blocking finalization and blocking drains. Split the structural mutation call path and annotate it as `[BLOCKING_SYNC_POINT]` because it clears the same Vault buffers written by the upload job.
Rejected Alternatives: Removing the blocking drain outright would allow clear/init to race the upload writer. Leaving the helper named teardown hid the fact that normal structural mutation can force a drain.
Scalability potential: Low/mid/high/ultra builds keep the same cartography upload route; the fix improves telemetry/fence truth without changing visual cadence scaling.
Hardware Impact: Hot-frame saving 0 us claimed. Prevents stale active-job bookkeeping and documents the rare structural stall instead of hiding it.

## Decision 36

Problem: `MacroEcosystemMathematicianRuntime` used `CompleteScheduledJobForTeardown()` from DataVault hot-swap and cold rebind paths. The forced completion is structurally necessary before swapping Vault handles, but the method name hid a non-teardown blocking route.
Solution: Split the call sites into `CompleteScheduledJobForVaultSwapBarrier()` for DataVault replacement/rebind and `CompleteScheduledJobForTeardown()` for disposal. Both call the same blocking implementation and carry `[BLOCKING_SYNC_POINT]` comments.
Rejected Alternatives: Removing the forced completion would let the old job write through handles while the owner swaps Vault state. Keeping the generic teardown name would keep the audit ambiguous.
Scalability potential: The ecosystem solver keeps one Vault owner route across low/mid/high/ultra tiers; only the rare structural swap barrier blocks.
Hardware Impact: Runtime saving 0 us claimed. The change makes the blocking barrier auditable without altering solver cadence.

## Decision 37

Problem: Physics culling and thermodynamics jobs were registered as active jobs in some paths but did not consistently clear `H8Memory` ownership after non-blocking completion or forced teardown completion. That weakens black-box proof and can make an already-completed writer look active.
Solution: Register the physics culling combined handle when scheduled and clear it after result publication or discard. Clear thermodynamics active-job registration after late-frame finalize and after teardown drains in `ThermodynamicsHazardGridRuntime` and `AbyssalThermodynamicsSolver`. Annotate forced drains as `[BLOCKING_SYNC_POINT]`.
Rejected Alternatives: Treating active-job registration as optional telemetry would leave forensic state ambiguous. Removing forced teardown drains would risk releasing Vault handles while jobs can still write.
Scalability potential: All quality tiers keep the same dispatcher/job route; the fix improves proof and cleanup without changing cadence or math LOD.
Hardware Impact: Hot-frame saving 0 us claimed. The gain is correctness of job ownership telemetry and avoiding stale active-job state on low-end and high-end runs.

## Decision 38

Problem: After latest-created DataVault cuts, several selected runtime helpers still used `GlobalRegistry.DataVault` as a fallback from resolve/schedule paths. This preserved a second DI route inside hot helpers even when the owner already had cached/hot-swap DataVault fields.
Solution: Replace selected fallback reads with cached-field-only resolution in `HectonFluidEngine`, `HectonUnderwaterVisuals`, `PlayerCriticalProceduralAudioRenderer`, `ProceduralBoneBlenderRuntime`, and `SargassumMicroFaunaBoids`. `SargassumMicroFaunaBoids` now handles DataVault replacement through `IGlobalRegistryHotSwapListener`, fences pending native writers, clears Vault handles, and reacquires in the owner swap window.
Rejected Alternatives: Continuing `_vault ?? GlobalRegistry.DataVault` hides bootstrap faults and keeps registry as a runtime heap lookup. Broad mechanical removal across all files was rejected because some callsites are cold/editor/smoke or need owner-specific swap barriers.
Scalability potential: Low/mid/high/ultra builds keep the same cached DataVault owner route; quality can scale math cadence and visual density without changing memory authority.
Hardware Impact: Hot-frame saving 0 us claimed. The measurable value is lower hidden ownership ambiguity and fewer registry reads in schedule/resolve helpers on low-end silicon.

## Decision 39

Problem: `ContextualPhysicalIkRig` read `GlobalRegistry.Player` and `GlobalRegistry.ScalabilityTier` from scheduled capture/FOV/cold-shiver paths. That violates cold-registry doctrine and makes IK presentation depend on registry polling during animation work.
Solution: Add cached `IPlayerRuntimeContext` and cached `HectonQualityTier`. Player context updates through `IGlobalRegistryHotSwapListener`; quality updates through `ScalabilityEvents`. Capture/FOV/cold-shiver now read local cached fields.
Rejected Alternatives: Per-frame registry caching was rejected because it still makes the registry a hot read surface. Scene/camera search fallback in the capture path was rejected because it would be worse than registry polling.
Scalability potential: Low tier can keep cheaper IK/FOV decisions from the same cached quality source; high/ultra can keep richer appendage presentation without changing authority route.
Hardware Impact: Hot-frame saving 0 us claimed. Registry reads are removed from IK capture and upper-arm FOV evaluation.

## Decision 40

Problem: Fauna spine and tentacle solvers polled `GlobalRegistry.ScalabilityTier` in solver tick/reset paths. The actual runtime quality source should be a typed signal/snapshot, not registry polling.
Solution: Register `FaunaKinematicsRuntime` and `LeviathanTentacleVerletSolver` with `ScalabilityEvents`; keep cold initial tier from bootstrap and consume `ScalabilityChangedEvent` for updates. Solver ticks now use cached `_qualityTier`.
Rejected Alternatives: Leaving the registry read in `Tick` would preserve hot polling. Using a binary low/high switch was rejected; existing continuous `GlobalQualityWeight` remains the solver fidelity input and tier is only policy metadata.
Scalability potential: Weak devices and high-end devices share the same signal route; continuous `GlobalQualityWeight` still controls segment/iteration curves.
Hardware Impact: Hot-frame saving 0 us claimed. Removes registry tier reads from two fauna solver tick paths.

## Decision 41

Problem: `SargassumGlobalDragManager` polled `GlobalRegistry.SargassumCut` inside buoyancy collapse and nested attachment update loops. These loops can run from tick paths and should not use registry as a hot service bus.
Solution: Cache `SargassumCutManager` during enable and rebind it via `IGlobalRegistryHotSwapListener`. Runtime loops now use `_cutManager`.
Rejected Alternatives: Re-reading registry each loop was rejected as hot polling. Removing cut integration would lose gameplay feedback between cutting and sargassum collapse.
Scalability potential: Low-tier runs avoid registry reads while preserving cheap cut/collapse fakes; high/ultra can spend saved budget on denser visuals rather than service lookup.
Hardware Impact: Hot-frame saving 0 us claimed. Registry reads are removed from cut sampling loops.

## Decision 42

Problem: `PDAInventoryTab` read `GlobalRegistry.Player` during auto-resolve, `GlobalRegistry.NativeInputManager` during inventory parallax publishing, and `GlobalRegistry.Audio` during UI sound playback. Those are UI work-path service reads, not cold dependency injection.
Solution: Add `IGlobalRegistryHotSwapListener`, cache `IPlayerRuntimeContext`, `InputManager`, and `IAudioService` during enable, and rebind the cached references on player/input/audio service replacement. Auto-resolve, runtime parallax, and UI sound paths now use local cached fields.
Rejected Alternatives: Per-call `GlobalRegistry` reads were rejected as hot polling. Scene search fallback was rejected because it is slower and less deterministic than registry hot-swap. Removing UI sound/parallax was rejected because it would cut presentation instead of fixing the route.
Scalability potential: Low-tier UI avoids service lookup while preserving cheap shader parallax; middle/high/ultra can keep richer PDA presentation without changing player/input/audio authority.
Hardware Impact: Hot-frame saving 0 us claimed. The concrete gain is removal of registry reads from UI work paths and lower dependency ambiguity.

## Decision 43

Problem: `SpectrumSystem` used `GlobalRegistry.Audio` in passive radar and abyssal anchor return paths, and DataVault replacement needed explicit handle rebind behavior to stay on one owner route.
Solution: Cache `IAudioService` and `SpatialAudioManager` cold, update them through hot-swap, and handle `GlobalRegistryServiceSlot.DataVault` by releasing old Spectrum Vault handles and reacquiring through the current DataVault route. Passive radar and return audio use cached services only.
Rejected Alternatives: Keeping `GlobalRegistry.Audio` in radar/return paths was rejected as hot polling. Leaving stale Vault handles across DataVault replacement was rejected because jobs and telemetry buffers must belong to the current owner.
Scalability potential: Weak devices can lower sonar cadence/visual density through existing quality controls without changing audio/DataVault authority; high/ultra can spend saved budget on richer sonar presentation.
Hardware Impact: Hot-frame saving 0 us claimed. The change removes audio registry reads from sonar/audio work paths and makes DataVault replacement explicit.

## Decision 44

Problem: `HectonFloatingOrigin` read `GlobalRegistry.Player` and `GlobalRegistry.Submarine` from safe-teleport and critical drift tracker paths. Floating-origin operations are authority-critical and should not poll registry during runtime work.
Solution: Cache `IPlayerRuntimeContext` and `ISubmarineRuntimeContext` at owner boot and rebind both through the existing hot-swap listener. Safe-teleport reset and submarine drift tracking now consume cached contexts.
Rejected Alternatives: Polling registry during every safe-teleport/drift update was rejected as a hot DI route. Resolving transforms through scene search was rejected because origin safety needs deterministic owner context.
Scalability potential: All quality tiers keep the same AUP/floating-origin truth route; quality can scale visual effects around shifts without changing player/submarine authority.
Hardware Impact: Hot-frame saving 0 us claimed. Registry reads were removed from origin safety and drift tracking work.

## Decision 45

Problem: `DestructibleOrganicManager` polled `GlobalRegistry.PlayerInventory`, `GlobalRegistry.PersistentWorldRegistry`, and `GlobalRegistry.Audio` from drop drain, persistence sync, harvest audio, and spore audio routes. Those paths can run repeatedly during flora updates and should consume cached owner interfaces.
Solution: Add `IGlobalRegistryHotSwapListener`, cache `PlayerInventory`, `PersistentWorldRegistry`, `IAudioService`, and `SpatialAudioManager` on enable, and rebind them on service replacement. Drain/persistence/audio paths now use cached fields.
Rejected Alternatives: Per-drop/per-audio registry lookup was rejected as hot polling. Dropping persistence/audio behavior was rejected because it would hide gameplay state instead of fixing authority routing.
Scalability potential: Low-tier flora can keep cheap interaction/audio fakes without global lookup churn; high/ultra can scale denser flora/audio presentation through quality controls without changing inventory/world/audio authority.
Hardware Impact: Hot-frame saving 0 us claimed. Registry reads were removed from flora drop drain, persistence, and audio event paths.

## Decision 46

Problem: The remaining ranked hot registry mutations in `GlobalSignals.SignalBus<T>.FlushPreSimulation` and `SystemDispatcher.RunDispatcherUpdate` are shared-core kill-switch/time precision routes. They mutate global state from central lanes, but they are not safe to replace with a local mechanical cache because they define cross-domain authority.
Solution: Classify them as `PENDING CORE ROUTE CARD`. A correct fix must introduce an owner-owned frame-state/quality-pressure route or typed signal handoff, then move publication out of read/flush helpers. No shared-core edit was made in this pass.
Rejected Alternatives: Replacing these calls with cached fields would hide the same global mutation under another name. Deleting them would remove kill-switch and precision progression behavior without an owner route.
Scalability potential: The future route must preserve continuous `GlobalQualityWeight` pressure and kill-switch derivation across low/mid/high/ultra without registry polling.
Hardware Impact: Runtime saving 0 us claimed. This pass prevents unsafe shared-core churn and records the next architecture cut explicitly.

## Decision 47

Problem: `GlobalRegistry.SystemKillSwitchMask` was still a hot global read surface for VFX/ambient simulation consumers. `HectonFluidEngine` and `SargassumMicroFaunaBoids` read the registry mask directly to gate abyssal flow and fauna ambient drift, while the actual producers already route through core kill-switch setters.
Solution: Add `SystemKillSwitchBitsSignal` as a 32-byte explicit-layout typed lane. `GlobalRegistry.SetSystemKillSwitchBits` now publishes the previous/current/changed mask after a successful atomic CAS, and the two consumers cache the latest frame snapshot from `SignalBus<SystemKillSwitchBitsSignal>`.
Rejected Alternatives: Reusing `KillSwitchSignal` was rejected because that lane carries homeostasis/system-health `ulong` state, not the registry bitmask contract. Keeping direct `SystemKillSwitchMask` reads was rejected as hot polling. Removing the kill-switch behavior was rejected because it is the current emergency VFX shed route.
Scalability potential: Low/mid/high/ultra builds keep the same kill-switch truth route; `GlobalQualityWeight` and quality tiers can still scale fidelity/cadence without changing ownership or DTO layout.
Hardware Impact: Hot-frame saving 0 us claimed. The concrete gain is removal of direct registry mask reads from abyssal flow and sargassum fauna simulation paths.

## Decision 48

Problem: Side-agent audit confirmed deeper shared-core mutation debt: `GlobalSignals` and `SystemDispatcher` still call `SetSystemKillSwitchBits`, `PublishAbsoluteUniverseTime`, `TickMathPrecisionTransition`, and `GlobalRegistry.JobAdmission` from phase loops. Cutting only consumers does not make those owner routes green.
Solution: Classify the remaining calls as core route-card work. The next safe cut must define the owner, phase, cadence, payload layout, overflow behavior, telemetry, reset/shutdown behavior, and full consumer list before moving dispatcher time, math precision, or AUP job-admission barriers.
Rejected Alternatives: Blindly replacing `PublishAbsoluteUniverseTime` with a time signal was rejected until all absolute-time consumers are mapped. Blindly replacing math precision was rejected because the registry path also updates shader globals. Removing `JobAdmission` access from signal publish without dispatcher-owned barrier state would risk AUP shift races.
Scalability potential: The future route must preserve continuous pressure/quality scaling across weak, mid, high, and ultra devices without a binary switch and without changing gameplay truth ownership.
Hardware Impact: Runtime saving 0 us claimed. This is an evidence boundary: no unsafe rewrite was made under an incomplete owner map.

## Decision 49

Problem: `GlobalSignals.Publish(AupPreShiftSignal)` and `Publish(AupShiftSignal)` directly called `GlobalRegistry.JobAdmission?.SetAupBarrierActive(...)`. That made a signal publish helper mutate a scheduler service through the registry, even though `SystemDispatcher` already owns AUP pre-shift fencing and caches `_jobAdmission`.
Solution: Move the AUP admission barrier mutation into `SystemDispatcher`. `RequestAupPreShiftPause` sets the cached job-admission barrier true before allocation lock/fence completion, and `ReleaseAupPreShiftPause` sets it false. `GlobalSignals` now only finds the active dispatcher and sends the AUP request/release through the dispatcher owner.
Rejected Alternatives: Leaving `GlobalRegistry.JobAdmission` in `GlobalSignals` was rejected as a hot/cross-phase registry dependency leak. Resolving the job-admission service again from dispatcher release was rejected because `_jobAdmission` is already maintained through cold resolve and hot-swap. Removing the barrier was rejected because AUP shifts must defer non-critical scheduling.
Scalability potential: All quality tiers keep the same AUP barrier semantics; low-tier devices can shed non-critical jobs during shifts without changing authority, while high/ultra keep deterministic barrier behavior during denser work.
Hardware Impact: Hot-frame saving 0 us claimed. The concrete gain is removal of a registry service lookup from AUP signal publish helpers and tighter dispatcher ownership of the scheduling barrier.

## Decision 50

Problem: `GlobalRegistry.AbsoluteUniverseTime` remained a direct read surface in celestial, weather, random-event, physics-water, and seismic/tide fallback paths. That kept global time as a pull-based registry fact even after `GlobalTimeSyncSignal` and `CelestialRuntimeSnapshot` existed.
Solution: Remove all direct consumers. Celestial uses owner-local `Time.timeAsDouble` before publishing `CelestialRuntimeSnapshot` and `GlobalTimeSyncSignal`; weather consumes the snapshot it already read; random events refresh a local scalar from `SignalBus<GlobalTimeSyncSignal>` and keep read accessors pure; physics-water uses the caller fallback time when the celestial snapshot is invalid; seismic/tide caches snapshot time or `Time.timeAsDouble` only as a dispatcher-absence fallback.
Rejected Alternatives: Removing `SystemDispatcher.PublishAbsoluteUniverseTime` before the consumer scan was rejected because the registry producer and `TickMathPrecisionTransition` needed separation. Polling `GlobalRegistry.AbsoluteUniverseTime` inside RandomEventSystem was rejected as hot/cold authority drift. Adding a new one-off time lane was rejected because `GlobalTimeSyncSignal` already exists and has a 32-byte explicit layout.
Scalability potential: Low tier reads cached scalar time and keeps cheap triangle-wave fakes; middle/high/ultra can consume richer celestial snapshots and signal telemetry without changing gameplay authority, DTO layout, or save identity.
Hardware Impact: Hot-frame saving 0 us claimed. The measurable proof is removal of every direct `GlobalRegistry.AbsoluteUniverseTime` callsite under `Assets/_Project/Scripts`; low-end gain is lower registry dependency ambiguity and fewer hot-pull surfaces.

## Decision 51

Problem: Once all `GlobalRegistry.AbsoluteUniverseTime` consumers were removed, `SystemDispatcher.RunDispatcherUpdate` still wrote `Time.timeAsDouble` into the registry every frame. That was a dead global mutation inside the dispatcher phase.
Solution: Delete the dispatcher call to `GlobalRegistry.PublishAbsoluteUniverseTime(Time.timeAsDouble)`. Keep the internal registry method/property surface in place because public/API removal needs compile proof and downstream assembly confidence.
Rejected Alternatives: Deleting `GlobalRegistry.AbsoluteUniverseTime` and `_absoluteUniverseTimeBits` immediately was rejected as compile-surface churn without Unity/import proof. Keeping the dead dispatcher write was rejected because it preserves a global mutation with no reader.
Scalability potential: All quality tiers now consume time through owner-local celestial solve, celestial snapshot, or typed signal cache. `GlobalQualityWeight` remains independent from time authority and does not change gameplay truth ownership.
Hardware Impact: Hot-frame saving 0 us claimed. The proof is route hygiene: dispatcher no longer writes an unused global time field every frame.

## Decision 52

Problem: `SystemDispatcher.RunDispatcherUpdate` still called `GlobalRegistry.TickMathPrecisionTransition(Time.frameCount)` directly. The transition mutates shader math LOD blend state, while `FrameTimeWatchdog` is the component that initiates math precision degradation and already owns cold-bound precision writer delegates.
Solution: Add a cold-bound `MathPrecisionTransitionTicker` delegate to `FrameTimeWatchdog` and expose an internal facade `FrameTimeWatchdog.TickMathPrecisionTransition(int frame)`. `SystemDispatcher` now calls that facade instead of referencing `GlobalRegistry` directly.
Rejected Alternatives: Moving the blend fields and shader keyword writes out of `GlobalRegistry` was rejected without Unity compile/runtime proof because many systems still read `MathPrecision`, `TargetMathPrecision`, and `MathPrecisionLowBlend01`. Removing the transition tick was rejected because the 60-frame degradation ramp must still complete.
Scalability potential: Low-tier degradation still ramps shader math LOD over time; high/ultra recovery still routes through existing watchdog precision writes. This narrows dependency routing without changing continuous quality behavior.
Hardware Impact: Hot-frame saving 0 us claimed. The concrete proof is dispatcher no longer has a direct `GlobalRegistry.TickMathPrecisionTransition` callsite; the remaining registry reference is a watchdog-owned cold delegate.

## Decision 53

Problem: `--csv-diff` accepted a source CSV that produced zero hash-bearing rows. That made the designer CSV-to-binary bridge capable of reporting a meaningless 0-row match, hiding missing `hash32/hash/hash_id/id/item_id/name` columns or empty source exports.
Solution: Make `csv_to_bin_diff` fail closed with `CSV_DIFF_EMPTY_SOURCE`, `CSV_DIFF_ZERO_HASH`, or `CSV_DIFF_NO_HASH_ROWS` before comparing against binary `Items.HashId`. Hash column names are now matched case-insensitively so spreadsheet capitalization does not become a false red gate.
Rejected Alternatives: Leaving the 0-row path as success was rejected because it proves no binary fidelity. Requiring one exact lowercase column name was rejected because designer exports vary capitalization and the validator must catch structural absence, not cosmetic case.
Scalability potential: Low/mid/high/ultra runtime behavior is unchanged. This is external CI proof that authoring bridges produce real binary hash rows before runtime consumes unmanaged payloads.
Hardware Impact: Runtime saving 0 us. CI overhead is negligible; it prevents bad binary payloads from reaching boot.

## Decision 54

Problem: A new `Assets/StreamingAssets/Hecton8/locomotion_environment_profiles.csv` appeared after another route pass. Runtime `StreamingAssets` text is a Data Monolith violation even when the current runtime code only exposes a parser method and no loader callsite.
Solution: Move the CSV and `.meta` to `Assets/_SourceData/Physics/KCC`, add folder metas, and add a `Physics/KCC` migration-summary rule so future KCC locomotion text regressions name the correct owner. Runtime binary hydration remains pending for the KCC owner.
Rejected Alternatives: Allowlisting the CSV was rejected because it would preserve a parallel runtime truth path. Editing `HydrodynamicKccRuntime` was rejected because no `StreamingAssets` loader callsite was present; the owned fix was source-data relocation and validator classification.
Scalability potential: Weak devices and high-end devices keep identical KCC truth ownership. Future binary/Vault hydration can scale presentation or cadence without changing DTO layout or runtime authority.
Hardware Impact: Runtime saving 0 us claimed. The concrete gain is removal of a deployed text artifact from the runtime payload tree and restoration of the SHINOBU_258 red gate to binary-payload absence only.

## Decision 55

Problem: The JUnit report could declare `failures=2` for missing static payload/no `.h8bin` files while emitting no concrete `<failure>` nodes because those errors were path-bearing findings that did not match any `.h8bin` file testcase.
Solution: Track emitted file-related findings and emit synthetic testcases for every remaining error, including directory and required-payload findings. Also replace the stale hardcoded metrics date with the current local run date.
Rejected Alternatives: Relying on the top-level JUnit `failures` attribute was rejected because CI dashboards and humans need named failure nodes. Leaving the 2026-05-20 stamp was rejected because it creates stale proof trails.
Scalability potential: No runtime scalability effect. This improves CI observability for low/mid/high/ultra payload gates without touching runtime authority.
Hardware Impact: Runtime saving 0 us. CI report generation cost remains trivial and external to Unity.

## Decision 56

Problem: `--fail-fast` could raise during C# schema parsing before the main validator state existed, which meant an ARM64 layout error could terminate without writing JSON/JUnit proof artifacts.
Solution: Parse schema/layout findings with fail-fast suppressed, carry those findings into the main validator state, then honor `--fail-fast` before runtime artifact/file traversal. The first fatal layout error still stops the pipeline, but the report is always emitted.
Rejected Alternatives: Keeping immediate parser abort was rejected because CI needs machine-readable proof. Ignoring schema findings until after file traversal was rejected because `--fail-fast` must remain a quick local path.
Scalability potential: No runtime scalability effect. This strengthens external payload/layout gating before low/mid/high/ultra devices ever see the binary.
Hardware Impact: Runtime saving 0 us. CI behavior is deterministic and avoids manual traceback archaeology.

## Decision 57

Problem: A section entry that pointed past EOF or overlapped another section was reported as fatal, but the malformed range was still admitted into later payload sampling. That could convert a clean validator finding into a Python `struct.unpack_from` crash.
Solution: Mark out-of-file, fixed-range overlap, and section-overlap ranges invalid for sampling while preserving the fatal finding. Only byte ranges proven inside file and non-overlapping enter `entries_by_name`.
Rejected Alternatives: Adding broad `try/except` around sampling was rejected because it hides the bad range instead of preserving the exact section-table fault. Sampling overlapped bytes was rejected because it creates misleading secondary payload errors.
Scalability potential: No runtime scalability effect. This keeps the Dear Lie sampling path cheap and deterministic while full table proof stays exact.
Hardware Impact: Runtime saving 0 us. CI avoids crash-only failure modes on corrupt baker output.

## Decision 58

Problem: The regression suite did not explicitly prove several mandatory SHINOBU_258 failure classes: `Pack=1`, bad struct-size alignment, AUP bound overflow, bad section ranges, malformed RLE, and fail-fast report persistence.
Solution: Expand `Tools/test_h8bin_validator.py` with synthetic corrupt blobs and schema fragments for each class. The suite now covers 22 cases and validates that corruption returns exit code 1 with named findings instead of traceback.
Rejected Alternatives: Relying on live project data was rejected because the current live gate has no `.h8bin` files. Manual inspection of validator code was rejected because CI needs repeatable corruption fixtures.
Scalability potential: No runtime scalability effect. The external firewall blocks invalid payloads before any target tier allocates unmanaged memory from them.
Hardware Impact: Runtime saving 0 us. CI cost increased by seconds in Python tests only; runtime/player cost remains zero.

## Decision 59

Problem: Section-table corruption findings named the failure but did not always include the 32-byte hex context required by Task 18. Alignment, overlap, and EOF failures pointed at the broken table fields without showing the bytes.
Solution: Emit `format_hexdump` around each corrupt section-table row for section order, record size, empty offset, alignment, fixed-range overlap, out-of-file, and section overlap findings. Tests now assert hexdump presence for section alignment and out-of-file routes.
Rejected Alternatives: Printing only the numeric offset was rejected because the baker programmer still needs byte-level context. Hex dumping the invalid payload offset for EOF was rejected because that offset may not exist; the section directory row is the real corrupt data.
Scalability potential: No runtime scalability effect. This improves CI for all target tiers by keeping broken binary payloads out before boot.
Hardware Impact: Runtime saving 0 us. CI report size increases by one small text block per fatal section-table finding.

## Decision 60

Problem: The C# layout parser was still vulnerable to syntax drift. It could miss valid explicit-layout structs when authors used `StructLayoutAttribute`, namespaced/global attributes, extra attributes between `StructLayout`/`FieldOffset` and declarations, alternate readonly/partial modifier order, or casted integer expressions such as `(int)16`.
Solution: Widen the struct and field regexes to accept namespaced/global attribute forms and intervening attributes, add balanced named-argument extraction for `Size`/`Pack`, and fix integer-cast sanitization so `(int)16` resolves to `16`. Added a regression struct that only fails if the variant `FieldOffsetAttribute((int)1)` field is actually parsed.
Rejected Alternatives: Relying on the current project style was rejected because the validator is supposed to be a CI firewall against future schema drift. Switching to a heavyweight C# parser was rejected for this external Python tool because the prompt requires standalone headless execution and current syntax coverage is narrow enough for deterministic regex plus tests.
Scalability potential: No runtime scalability effect. Stronger static layout extraction protects weak and high-end targets equally before binary payloads enter unmanaged memory.
Hardware Impact: Runtime saving 0 us. CI parser cost is negligible; the gain is fewer false-green layout passes.

## Decision 61

Problem: `sample_indices` returned full Python lists. On massive sections, `--thorough` could allocate a list with every record index, and default 5 percent sampling could allocate a large set/list before reading any mmap bytes.
Solution: Make `--thorough` return `range(count)` and make default sampling return a deterministic iterator that yields first record, last record, then a coprime-stride pseudo-random walk without holding all indices. Added tests for a billion-record thorough path and default non-list edge sampling.
Rejected Alternatives: Keeping `list(range(count))` was rejected because it violates the zero-init/mmap spirit of Task 14. Capping sample count was rejected because it would weaken the requested 5 percent sampling contract.
Scalability potential: No runtime scalability effect. CI can inspect low-end and high-end payload sizes without memory spikes from index staging.
Hardware Impact: Runtime saving 0 us. CI memory use is reduced from O(sample_count) index storage to O(1) for traversal state.

## Decision 62

Problem: `--csv-diff` reads `Items.HashId` through a probe `ValidationState`. With `--fail-fast`, a corrupt diff target could raise inside the probe before its finding was copied into the main report.
Solution: Force the probe state to run fail-fast-neutral, append probe errors into the main validator state, then raise `FailFastAbort` from the main state path. Added a regression with a valid target directory, an empty external diff target, and `--csv-diff --fail-fast`.
Rejected Alternatives: Letting the probe raise directly was rejected because it can lose machine-readable evidence. Ignoring `--fail-fast` after probe failure was rejected because local quick checks must still stop early.
Scalability potential: No runtime scalability effect. This strengthens the designer CSV-to-binary bridge for all target payload tiers.
Hardware Impact: Runtime saving 0 us. CI behavior is deterministic and avoids traceback-only binary diff failures.

## Decision 63

Problem: The current user request asked to create the SHINOBU_258 validator, but disk state already contains the validator, regression suite, reports, status, and rationale from prior SHINOBU passes. Blindly rewriting it would risk a refactoring loop and could damage a passing CI firewall.
Solution: Re-read the XML prompt, domain map, status, rationale, and task-relevant mandates; then verify the existing implementation with py_compile, XXH3 self-test, corruption tests, and the live StreamingAssets gate. No Python source edit was made because the objective evidence shows the validator already satisfies the requested creation scope.
Rejected Alternatives: Creating a new parallel validator was rejected because it would split the binary proof route. Generating a placeholder `static_data.h8bin` was rejected because binary payload baking belongs to the Data Monolith compiler/Unity batch route and fake bytes would undermine the gate.
Scalability potential: Low, middle, high, and ultra tiers keep one immutable binary payload contract; CI blocks absent or corrupt payloads before any target hardware maps unmanaged data.
Hardware Impact: Runtime saving 0 us on i3/MX350; this is CI/editor-only verification. The live gate correctly prevents player boot from accepting absent static data.

## Decision 64

Problem: Section-table validation proved byte ranges, but a corrupt non-empty section could still declare `record_size == 0` or a stride smaller than its parsed C# explicit-layout struct. That allowed later hash extraction or sampled field reads to walk bytes using a false record stride.
Solution: Add a record-stride gate before admitting sections into `entries_by_name`, and mirror the guard inside `validate_record_sample`. `RECORD_SIZE_ZERO`, `RECORD_SIZE_UNDER_STRUCT`, and `FIELD_EXCEEDS_RECORD_SIZE` are fatal named findings with section-row hex/layout context; invalid payload strides are not sampled.
Rejected Alternatives: Letting `struct.unpack_from` fail was rejected because traceback-only CI output does not identify the ABI fault. Sampling only the fields that fit was rejected because that would hide an impossible runtime ABI: C# expects the full struct stride.
Scalability potential: No runtime scalability effect. The external firewall now blocks malformed low/mid/high/ultra payloads before any target maps them into unmanaged memory.
Hardware Impact: Runtime saving 0 us. CI avoids false-green or traceback-only validation when an Editor baker emits a short stride.

## Decision 65

Problem: `--csv-diff` can point at an external generated `.h8bin` outside the primary `--target-dir`. Its item-hash extraction path read the section table directly, which made the designer bridge weaker than the main validator for corrupt offsets/ranges.
Solution: Run the external diff target through `validate_h8bin_file` with fail-fast disabled before extracting `Items.HashId`. If the probe has any fatal binary finding, copy those findings into the main report and stop before hash comparison noise.
Rejected Alternatives: Keeping a lightweight direct reader was rejected because CSV diff is a proof bridge, not a best-effort parser. Emitting `CSV_TO_BIN_MISSING_HASHES` after a corrupt probe was rejected because the binary target is unreadable, so comparison is invalid.
Scalability potential: No runtime scalability effect. Designers get the same binary ABI firewall for low/mid/high/ultra payload checks whether they run the main target gate or a focused CSV diff.
Hardware Impact: Runtime saving 0 us. CI/editor-only cost is one mmap validation pass over the diff target and prevents traceback/noisy failure reports.

## Decision 66

Problem: Task 20 requires `mmap` handles to close even when fail-fast aborts. The checksum path released its `memoryview` in a local `finally`, but the function still carried a wider `payload_view` variable and end-of-function cleanup branch that made the lifetime proof harder to audit.
Solution: Isolate checksum memoryview ownership in `compute_payload_checksum(mm_obj, header_size)`. The helper creates the view, computes XXH3, and releases in `finally`; fail-fast findings occur after the helper returns, so no exported pointer can keep the mmap locked during unwind.
Rejected Alternatives: Keeping the wider function-scope `payload_view` was rejected because it weakens the Task 20 proof. Replacing mmap with bytes was rejected because Task 14 requires mmap/no full-file staging.
Scalability potential: No runtime scalability effect. Headless CI can validate low/mid/high/ultra payloads without file-lock leaks blocking a following Unity bake.
Hardware Impact: Runtime saving 0 us. CI memory remains mmap/page-cache based; regression proves fail-fast checksum mismatch exits without `BufferError` or traceback.

## Decision 67

Problem: The latest polish audit found source-truth gaps: directory flag bit 1 was treated as RLE even though current H8DM C# source does not define an RLE flag, the section-alignment constant could theoretically regress below the 16-byte floor, integer AUP fields were not bounded, recipe/loot references could skip proof when the `Items.HashId` master set was empty, and `--csv-diff` compared hashes but not known item field values.
Solution: Make RLE probing source-backed via parsed `RleDirectoryFlag` and reject all unknown directory bits as `DIRECTORY_FLAGS_UNSUPPORTED`; enforce `SectionAlignmentBytes >= 16` and use `max(16, SectionAlignmentBytes)` for file/data/section alignment; validate integer AUP fields named `Aup*`/`AUP*`; add `REFERENCE_MASTER_EMPTY`; and compare known numeric `H8ItemRecord` CSV fields such as `Cost`, `MassKg`, `VolumeM3`, `MaxStack`, and `YieldHash` against mmap-read binary records.
Rejected Alternatives: Hardcoding RLE bit 1 was rejected because it invents a binary contract not present in H8DM source. Silently accepting an empty reference master was rejected because it is not foreign-key proof. Hash-only CSV diff was rejected because it cannot prove that designer numeric changes were padded and baked into the intended field offsets.
Scalability potential: No runtime gameplay behavior changes. Low, middle, high, and ultra tiers all consume the same binary ABI; this pass strengthens the external gate before any target maps payload bytes into unmanaged memory.
Hardware Impact: Runtime saving 0 us. CI cost remains mmap/stream based; the regression suite increased to 43 tests and blocks misowned flags, integer coordinate overflow, and CSV field drift before player boot.

## Decision 68

Problem: The RLE corruption regressions set directory flag bit `1`, but the synthetic C# schema did not declare `RleDirectoryFlag`. The hardened validator correctly treated bit `1` as unsupported and never reached the RLE probe.
Solution: Add `public const uint RleDirectoryFlag = 1u;` to the malformed-RLE regression schema and keep a separate no-RLE-source bit `1` test for `DIRECTORY_FLAGS_UNSUPPORTED`.
Rejected Alternatives: Removing the unsupported-flags gate was rejected because it would allow unknown directory semantics into CI. Hardcoding RLE bit `1` in validator logic was rejected because C# schema remains the authority.
Scalability potential: No runtime scalability effect. The external binary firewall keeps compression semantics source-driven for weak, middle, high, and ultra payloads.
Hardware Impact: Runtime saving 0 us. CI coverage now proves RLE probes and unsupported-flag rejection independently.

## Decision 69

Problem: A new `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` exists with magic `H8VB`. Parsing it as an `H8DM` Data Monolith blob produced a cascade of false header/directory/section errors instead of identifying the real ownership boundary.
Solution: Add a known-foreign `.h8bin` magic table and fail closed with `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` for `H8VB`/Audio/VocalBank before any H8DM directory parsing. The JSON self-audit now records this guard.
Rejected Alternatives: Allowlisting `H8VB` was rejected because SHINOBU_258 has no proof of the vocal bank ABI. Treating it as corrupt H8DM bytes was rejected because it hides the real missing proof route and pollutes CI output.
Scalability potential: Low-tier and ultra-tier runtime behavior is unchanged. This protects one owner/one route by forcing Audio/VocalBank to provide its own validator or integrate with an approved domain `.h8bin` gate.
Hardware Impact: Runtime saving 0 us. CI output is reduced from a multi-error H8DM cascade to one precise foreign-schema blocker.

## Decision 70

Problem: The user mandate explicitly asks for AST-style `[FieldOffset]` parsing. The previous parser had been hardened regex, but a single regex still owned the critical `StructLayout -> struct -> FieldOffset` extraction path and could miss combined C# attribute lists.
Solution: Replace the regex-first struct/field extractor with a lightweight syntax-tree scanner. It strips comments, reads balanced attribute blocks, walks declaration braces, extracts `StructLayout` and `FieldOffset` calls from attribute lists, then parses only the final field declaration statement. Regression now covers `[Serializable, StructLayout(...)]` and `[NonSerialized, FieldOffset(...)]`.
Rejected Alternatives: Pulling Roslyn into CI was rejected because SHINOBU_258 must stay standalone Python/headless. Keeping the old regex as authority was rejected because it did not satisfy the current AST mandate strongly enough.
Scalability potential: No runtime behavior changes. The same H8DM ABI is protected across low/mid/high/ultra devices; the scanner reduces false-green risk when C# attribute style changes.
Hardware Impact: Runtime saving 0 us. CI parser cost remains small; current H8DM source parses 32 structs and the regression suite increased to 44 tests.

## Decision 71

Problem: The JUnit writer emitted synthetic failure testcases for non-file payload errors, but the root `tests` attribute still used only the number of `.h8bin` file metrics. The live report had two `<testcase>` nodes and `tests="1"`.
Solution: Set the JUnit `tests` attribute after all file and synthetic testcases are emitted, using the actual testcase count. Add a regression that parses the XML and compares the attribute to the node count.
Rejected Alternatives: Leaving the mismatch was rejected because CI dashboards can misreport proof artifacts. Counting only files was rejected because missing required payloads are first-class validation failures.
Scalability potential: No runtime scalability effect. It keeps CI evidence reliable for every target tier before runtime consumes binary payloads.
Hardware Impact: Runtime saving 0 us. CI XML generation adds one tiny tree count after report construction.

## Decision 72

Problem: The prior `H8VB` handling was too conservative after source evidence was available. It correctly stopped the H8DM directory cascade, but it still left a valid `Audio/VocalBank` binary as a generic foreign-schema blocker and failed to prove bank hash, record ordering, payload contiguity, codec support, and ADPCM block headers.
Solution: Promote `H8VB` to a source-backed sidecar validation route inside `Tools/h8bin_validator.py`. The validator now dispatches on magic before H8DM parsing, enforces the 64-byte header and 32-byte index from `Tools/voice_baker.py`/`VocalBankContracts.cs`, recomputes FNV-1a over records plus payload, rejects unsupported runtime codecs, validates contiguous mono payload records, and samples every ADPCM block header for step/reserved byte validity.
Rejected Alternatives: Allowlisting `H8VB` was rejected because it would permit opaque runtime bytes. Keeping `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` was rejected once the ABI had enough static source proof. Parsing it as Data Monolith was rejected because `H8VB` is not `H8DM` and would produce false failures.
Scalability potential: Low-tier, middle, high, and ultra builds share the same vocal bank ABI. The validator blocks corrupt or unsupported audio bytes before runtime; SHINOBU_260 still owns actual audio-thread and visual/audio overkill proof.
Hardware Impact: Runtime saving 0 us. This is CI/editor-only proof. Live validation processes the 19,680-byte bank through mmap in the existing gate and removes the false H8DM cascade; current player-facing blocker is only missing `static_data.h8bin`.
