# LOG_H8BIN_GRAVEYARD_AUDITOR

## 2026-05-17 - Binary Payload Graveyard Audit

### Scope

Audited HECTON Python/generated binary payloads under `Data`, `Assets/_Project/Data`, and the archived headless dump path. Primary evidence artifacts:

- `Docs/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv` - 47 target rows with byte length, magic/header, source refs, tool/data refs.
- `Docs/AgentLogs/BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR.json` - current project verifier output.
- `Tools/VerifyBinaryHygiene.py` - current verifier scans every `.bin` and `.h8bin`, excluding broad build/cache directories only.

### What Was Wrong

Archive evidence and live disk are not the same. The old Batch007 hygiene reports talk about 46 product `.bin/.h8bin` payloads. Current `Tools/VerifyBinaryHygiene.py` reports 65 `.bin/.h8bin` files because it also counts Bakery editor/plugin binary fixtures. It fails with 16 misaligned files: 15 Bakery editor/plugin chunks plus one product file, `Data/Balance/Baked/Babel_Dictionary.h8bin`.

`Data/Balance/Baked/Babel_Dictionary.h8bin` is live-red: 1295 bytes, 16-byte remainder 15. Archive notes claimed this file had been padded to 1296 bytes. The current disk has drifted.

`Assets/_Project/Data/UI/GlitchTable.bytes` is not included by `VerifyBinaryHygiene.py` because the verifier only scans `.bin` and `.h8bin`. It is still a binary asset in the user-requested scope.

Several binaries have parsers or generated constants but no production runtime wiring found. A reader class is not proof of gameplay use.

### What Was Done

Ran deterministic filesystem inventory, current hygiene verifier, header/magic inspection, tool/report correlation, exact-name and stem searches across source/tests/scenes/prefabs, and GUID checks for imported Unity binary assets.

Classification terms:

- `ACTIVE_RUNTIME_WIRED`: current main runtime code opens the file or a direct code path is boot-wired.
- `ACTIVE_CODEPATH_NOT_SCENE_PROVEN`: runtime reader exists and searches the file, but scene/prefab bootstrap wiring was not proven by static refs.
- `EDITOR_OR_TEST_ONLY`: used by editor tooling or tests, not found in main runtime.
- `READER_PRESENT_NOT_WIRED`: parser/reader exists, but no production instantiation or exact payload path assignment found.
- `SCRIPT_TOOL_ONLY`: generated and verified by Python/data reports; no current first-party runtime/editor code loads it.
- `STATIC_LEDGER_MIRROR_ONLY`: binary mirrors a source table; current code uses hardcoded mirror, not the asset.
- `ARCHIVE_DUMP_ONLY`: black-box/archive dump, not product content.
- `THIRD_PARTY_EDITOR_BINARY`: vendor/editor payload, not HECTON Python-generated content.

### Full Product/Generated Inventory

| # | File | Bytes / magic | Responsibility / mechanic | Generator or report evidence | Main-code usage verdict |
|---:|---|---:|---|---|---|
| 1 | `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` | 1,534,512 / `H8BD` | Full Babel localization dictionary: hashed UTF-8 text pool for multi-language UI/content. | `Tools/BabelCompiler.py`, `Tools/VerifyBabel.py`, `Tools/VerifyBabelDictionary.py`, manifest beside file. | `SCRIPT_TOOL_ONLY` for this exact asset. GUID has no refs. Current readers use root `Data/Balance/Baked` or a bare `Babel_Dictionary.h8bin` relative path, not this nested asset path. Dead/import ballast unless a packaging step copies it. |
| 2 | `Assets/_Project/Data/UI/GlitchTable.bytes` | 64 / ASCII glyph bytes | HUD glitch substitution table. | Unity `.bytes` asset; mirrored by source comment. | `STATIC_LEDGER_MIRROR_ONLY`. `GlitchTable.cs` embeds the same bytes in static arrays; asset GUID has no refs. Runtime does not load it. |
| 3 | `Data/AI/Navigation_Tuning.h8bin` | 1,280 / `H8AN` | AI navigation/path tuning constants for path sim/verifier. | `Data/AI/Navigation_Tuning.manifest.json`, `Tools/AiPathSim.py`, `Tools/VerifyAiNavigationTuning.py`. | `SCRIPT_TOOL_ONLY`. No main code literal load found. |
| 4 | `Data/Audio/Acoustic_LUT.bin` | 524,288 / raw float table | Sabine RT60 plus damping LUT: replaces runtime acoustic equation sweep with one sampled fallback pair. | `Data/Audio/Acoustic_LUT.manifest.json`, `Tools/SabineBaker.py`, `Tools/VerifySabineBaker.py`. | `ACTIVE_RUNTIME_WIRED`. `SpatialAudioManager` constant path `Data/Audio/Acoustic_LUT.bin`, bootstrap registers `SpatialAudioManager`, runtime cold-loads it. |
| 5 | `Data/Balance/Baked/Babel_Dictionary.h8bin` | 1,295 / `H8AB` | Small balance string pool paired with `H8StaticData.bin`. | `Data/Balance/Baked/Babel_Dictionary.manifest.json`, CSV data monolith logs. | `READER_PRESENT_NOT_WIRED` and `MISALIGNED_PRODUCT_FILE`. `BabelDictionaryStore.OpenDefault()` can read it; only tests instantiate the store in static scan. Current file violates 16-byte alignment. |
| 6 | `Data/Balance/Baked/H8StaticData.bin` | 896 / `H8SD` | Small static balance monolith: fixed DTO lookup records. | `Data/Balance/Baked/H8StaticData.manifest.json`, CSV data monolith logs. | `READER_PRESENT_NOT_WIRED`. `StaticDataStore.OpenDefault()` can read it; production bootstrap separately probes absent `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`. |
| 7 | `Data/Economy/Crafting_Costs.h8bin` | 7,424 / `H8CR` | Crafting recipe/ingredient SoA hydration payload. | `Tools/CraftingCostsBaker.py`, `Tools/VerifyCraftingCosts.py`, manifests/audits. | `EDITOR_OR_TEST_ONLY`. `EconomyRecipeTunerWindow` loads this exact path and hydrates vault buffers; no main runtime load found. |
| 8 | `Data/Economy/Crafting_Costs_Toaster.h8bin` | 2,464 / `H8CT` | Reduced crafting-cost payload for low-tier/toaster validation. | `Tools/CraftingCostsBaker.py`, `Tools/VerifyCraftingCosts.py`, toaster manifest. | `SCRIPT_TOOL_ONLY`. No current runtime/editor load found. |
| 9 | `Data/Economy/Ore_Distribution.h8bin` | 1,776 / `H8OL` | Deterministic ore distribution / LCG spawn table. | `Tools/OreLcgBaker.py`, `Tools/VerifyOreLcgBaker.py`, independent verifier. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 10 | `Data/Economy/Submarine_Upgrade_Stat_Map.h8bin` | 176 / `H8UP` | Submarine upgrade stat curve/map binary. | `Tools/UpgradeCurveBaker.py`, upgrade layout/validation JSON. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 11 | `Data/Ecosystem/Organic_Entropy_Regrowth.h8bin` | 195,344 / `H8OR` | Organic regrowth/entropy precomputed table. | `Tools/WorldEntropySim.py`, `Tools/VerifyOrganicEntropy.py`, ecosystem manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 12 | `Data/Environment/Tide_Harmonics.bin` | 9,600 / raw float table | Base tide harmonic coefficients. | `Data/Environment/Tide_Harmonics.manifest.json`, `Tools/VerifyTideBaker.py`. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 13 | `Data/Environment/Tide_Harmonics.index.h8bin` | 96 / index header | Tide harmonic index/metadata sidecar. | Tide manifest and SHINOBU notes. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 14 | `Data/Environment/Tide_Harmonics_Low.bin` | 2,400 / raw float table | Low-tier tide harmonic approximation. | Tide manifest/verifiers. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 15 | `Data/Environment/Tide_Harmonics_Ultra.bin` | 38,400 / raw float table | Ultra-tier tide harmonic overkill table. | Tide manifest/verifiers. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 16 | `Data/Habitat/HabitatPressureBudget.h8bin` | 2,704 / `H8HP` | Habitat pressure/failsafe budget table. | Habitat layout docs, `Tools/VerifyHullStressBudget.py`. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 17 | `Data/Localization/en_US.bin` | 60,928 / `H8LB` | English localization binary emitted from localization JSON. | `Tools/LocToBinary.py`. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 18 | `Data/Localization/en_US_Taxonomy.h8bin` | 27,536 / `H8TX` | Taxonomy localization/classification payload. | `Tools/Taxonomy/compile_taxonomy.py`, `verify_taxonomy.py`, manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 19 | `Data/Localization/Radio/marauder_radio_interceptions.h8bin` | 7,872 / `H8RD` | Marauder radio interception data. | `Data/Localization/Radio/generate_marauder_radio.py`, verifier/report JSON. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 20 | `Data/Lore/Encyclopedia.h8bin` | 41,920 / `H8LR` | Lore encyclopedia binary payload/index format. | `Tools/LorePacker.py`, `Tools/VerifyLore.py`, manifest. | `READER_PRESENT_NOT_WIRED`. `LoreMmfEncyclopedia` exists and generic serialized paths exist, but no exact `Encyclopedia.h8bin` path or scene/prefab assignment found. |
| 21 | `Data/Lore/PdaTechnicalLogs.h8bin` | 59,120 / `H8PT` | PDA technical log payload, high/full version. | `Tools/PackPdaTechnicalLogs.py`, `Tools/VerifyPdaTechnicalLogs.py`, manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 22 | `Data/Lore/PdaTechnicalLogs_Toaster.h8bin` | 19,120 / `H8PT` | PDA technical log reduced/toaster payload. | Same PDA packer/verifier. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 23 | `Data/Narrative/First_Hour_Quests.h8qdag.bin` | 496 / `H8QG` | First-hour quest DAG: 64-byte header, node/trigger/edge/tier tables. | `Tools/QuestCompiler.py`, `VerifyQuestDag*`, generated constants. | `EDITOR_OR_TEST_ONLY`. Loader exists, but only editor inspector/tests call the default binary path in static scan. |
| 24 | `Data/Physics/Submarine_RuntimePack.bin` | 1,152 / `H8HY` | Submarine hydrodynamics/runtime verification pack. | `Tools/SubmarinePhysicsSim.py`, `Tools/VerifySubmarineHydrodynamicsData.py`. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 25 | `Data/Precomputed/Atmosphere/atmosphere_density_matrix_rgba16f.bin` | 1,024 / raw RGBA16F | Atmosphere density LUT. | `Tools/AtmoPreview.py`, atmosphere LUT manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 26 | `Data/Precomputed/Atmosphere/atmosphere_sky_gradient_rgba16f.bin` | 262,144 / raw RGBA16F | Atmosphere sky gradient LUT. | `Tools/AtmoPreview.py`, atmosphere LUT manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 27 | `Data/Precomputed/caustics_dispersion_offsets.bin` | 1,216 / raw table | Caustics dispersion offset lookup. | `Tools/MathLUTGenerator.py`, math LUT manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 28 | `Data/Precomputed/dalton_gas_toxicity.bin` | 128,128 / `H8GT` | Dalton gas toxicity base matrix. | `Tools/DaltonGasToxicityBaker.py`, `Tools/VerifyDaltonGasToxicity.py`, manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 29 | `Data/Precomputed/dalton_gas_toxicity_overkill.bin` | 96,112 / `H8GX` | Dalton gas toxicity high/overkill variant. | Dalton baker/verifier manifest. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 30 | `Data/Precomputed/dalton_gas_toxicity_toaster.bin` | 4,080 / `H8GL` | Dalton gas toxicity low-tier variant. | Dalton baker/verifier manifest. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 31 | `Data/Precomputed/gerstner_wave_weather.bin` | 32,000 / raw float table | Gerstner/weather wave lookup. | `Tools/MathLUTGenerator.py`, math LUT manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 32 | `Data/Precomputed/Reverb_LUT.bin` | 262,400 / `H8RV` | Reverb/acoustic validation LUT. | `Tools/AcousticValidator.py`, tests. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 33 | `Data/Precomputed/sabine_reverb_rt60.bin` | 4,000 / raw float table | Sabine RT60 lookup. | `Tools/MathLUTGenerator.py`, math LUT manifest. | `SCRIPT_TOOL_ONLY`. Main runtime uses `Data/Audio/Acoustic_LUT.bin` instead. |
| 34 | `Data/System/VFX_Budgets.h8bin` | 1,344 / `H8VB` | VFX particle/VRAM budget catalog. | `Tools/ValidateVfxParticleBudgetCatalog.py`, `Tools/VerifyVramBudgets.py`, manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 35 | `Data/System/Visual_Scalability_Matrix.bin` | 2,048 / `H8VG` | Visual LOD/scalability matrix. | `Tools/VisualLodMatrixBaker.py`, `Tools/VerifyVisualLodMatrix.py`, manifest. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 36 | `Data/UX/VR_Comfort_Profiles.h8bin` | 1,472 / `H8VR` | VR comfort profile table. | `Tools/VerifyVrComfortData.py`, UX layout/verification docs. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 37 | `Data/UX/VR_Comfort_Profiles_Toaster.h8bin` | 1,120 / `H8VR` | Low-tier VR comfort profile table. | Same VR comfort verifier/docs. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 38 | `Data/UX/VR_Comfort_RTXOverkill.h8bin` | 560 / `H8VR` | High/overkill VR comfort variant. | Same VR comfort verifier/docs. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 39 | `Data/Visuals/Biolum_Profiles.bin` | 25,936 / `H8BI` | Bioluminescence pulse/color/profile table. | `Tools/BiolumWaveform.py`, visual schema/manifest/tests. | `ACTIVE_CODEPATH_NOT_SCENE_PROVEN`. `BiolumPulseSyncRuntime` resolves `Data/Visuals/Biolum_Profiles.bin`; no scene/prefab GUID reference found for the component. |
| 40 | `Data/Visuals/Refraction_LUT_RGBA16F.bin` | 524,288 / raw RGBA16F | Base Snell/refraction LUT. | `Tools/OpticsBaker.py`, `Tools/VerifySnellRefractionLut.py`. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 41 | `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin` | 131,072 / raw RGBA16F | Minimal/low refraction LUT. | Optics/Snell verifier. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 42 | `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin` | 2,097,152 / raw RGBA16F | Ultra refraction LUT. | Optics/Snell verifier. | `SCRIPT_TOOL_ONLY`. No runtime tier selector found. |
| 43 | `Data/Visuals/Water_Extinction_Matrix.bin` | 393,216 / raw R16 half table | Beer-Lambert water extinction LUT streamed to texture. | `Tools/OpticsBaker.py`, `Tools/VerifyOpticsBaker.py`, water README. | `ACTIVE_RUNTIME_WIRED`. `GlobalShaderDispatcher` calls `LutArrayResolver`, which resolves this exact path from StreamingAssets/persistent/project `Data/Visuals`. |
| 44 | `Data/Visuals/Water_Extinction_Matrix_Overkill.bin` | 1,572,864 / raw R16 half table | High/overkill water extinction variant. | Optics baker/water README. | `SCRIPT_TOOL_ONLY`. Current resolver loads only `Water_Extinction_Matrix.bin`; no overkill selector found. |
| 45 | `Data/Visuals/Water_Extinction_Matrix_Toaster.bin` | 24,576 / raw R16 half table | Toaster water extinction variant. | Optics baker/water README. | `SCRIPT_TOOL_ONLY`. Current low-memory resolver uses analytical fallback, not this file. |
| 46 | `Data/Visuals/Water_Fog_Density_LUT.bin` | 3,008 / raw table | Water fog density preview/validation LUT. | `Tools/WaterColorPreview.py`, tests, data truth audit. | `SCRIPT_TOOL_ONLY`. No main code load found. |
| 47 | `Docs/Archive/Batch007/AgentLogs/Dump_HEADLESS_SCENARIO_RUNNER.bin` | 16 / dump bytes | Archived black-box/headless dump evidence. | Archive agent log path only. | `ARCHIVE_DUMP_ONLY`. Not product content. |

### Main-Code Use Summary

Confirmed product payloads with current main runtime use:

- `Data/Audio/Acoustic_LUT.bin`: boot-wired through `SpatialAudioManager`; loaded cold from project root.
- `Data/Visuals/Water_Extinction_Matrix.bin`: rendering path through `GlobalShaderDispatcher` and `LutArrayResolver`.

Runtime readers or runtime classes exist, but static scene/bootstrap wiring is not proven:

- `Data/Visuals/Biolum_Profiles.bin`: `BiolumPulseSyncRuntime` loads it, but no scene/prefab GUID reference found.
- `Data/Balance/Baked/H8StaticData.bin`: `StaticDataStore` can read it, but only tests instantiate the store in current scan.
- `Data/Balance/Baked/Babel_Dictionary.h8bin`: `BabelDictionaryStore` can read it, but only tests instantiate the store in current scan; file is also misaligned.
- `Data/Lore/Encyclopedia.h8bin`: generic MMF reader exists, but no exact path assignment found.

Editor/test only:

- `Data/Economy/Crafting_Costs.h8bin`: editor recipe tuner loads it.
- `Data/Narrative/First_Hour_Quests.h8qdag.bin`: editor inspector/tests load it.

Everything else in the 47-row product/generated set is `SCRIPT_TOOL_ONLY`, `STATIC_LEDGER_MIRROR_ONLY`, or `ARCHIVE_DUMP_ONLY` by static evidence.

### Non-Target Binary Files Found

Current `VerifyBinaryHygiene.py` also counts these 19 `.bin` files under Bakery editor/plugin data:

- `Assets/Editor/x64/Bakery/hwtestdata/alphabuffer.bin` - misaligned 2 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/alphaid2.bin` - 0 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/direct0.bin` - misaligned 52 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/heightmaps.bin` - 0 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/ib32.bin` - misaligned 28 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/lmid.bin` - misaligned 4 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/lmlod.bin` - misaligned 4 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/lms.bin` - misaligned 18 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/settings.bin` - misaligned 10 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/vbtrace.bin` - aligned 96 bytes.
- `Assets/Editor/x64/Bakery/hwtestdata/vbtraceUV0.bin` - aligned 32 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part0.bin` - misaligned 7 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part1.bin` - misaligned 12,597 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part2.bin` - misaligned 628 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part3.bin` - misaligned 88 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part0.bin` - misaligned 7 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part1.bin` - misaligned 12,497 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part2.bin` - misaligned 584 bytes.
- `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part3.bin` - misaligned 84 bytes.

Classification: `THIRD_PARTY_EDITOR_BINARY`. Do not merge these into HECTON Python payload ownership. If the binary hygiene gate is meant to police only product data, its scope is wrong. If it is meant to police every `.bin`, Bakery must be explicitly exempted or vendor fixtures must be quarantined outside the scanned tree.

Other binary-ish assets outside the product/generated `.bin/.h8bin` set:

- `Assets/_Project/Diagnostics/auto_baseline_test.raw` - project diagnostics raw, no HECTON Python payload evidence in this pass.
- `Assets/MapMagic/Generators/Biomes/Runtime/Sources/*.raw` - MapMagic biome source raws, vendor/runtime source assets.
- `Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/ConfigData.bytes` - Odin editor plugin config.

### Cinematic Cheats Used

The useful payloads are mostly deliberate cinematic cheats, not full simulation:

- Acoustic: one sampled Sabine/damping lookup replaces live acoustic surface/volume solving during fallback.
- Water extinction: packed Beer-Lambert LUT streams to a texture instead of per-pixel dynamic optical integration.
- Biolum profiles: precomputed pulse/color profile bytes feed visual rhythm instead of simulating biology.
- Tide/Dalton/refraction/toaster/overkill variants are valid Math LOD ideas, but current code does not prove that the tier selectors actually consume most of them.

### Exact Microseconds Saved

No files were deleted and no runtime code was changed. Exact measured saving from this audit: `0 us/frame`.

Potential savings are not claimed. Removing dead payloads could reduce import/build/cold disk surface, but that requires a separate deletion/quarantine change and build validation.

### Required Follow-Up

1. Fix or rebake `Data/Balance/Baked/Babel_Dictionary.h8bin`; it is the only misaligned product payload.
2. Decide whether `Tools/VerifyBinaryHygiene.py` should exclude Bakery/editor vendor fixtures. Current gate fails because it counts them.
3. Wire or quarantine the many `SCRIPT_TOOL_ONLY` LOD variants. Keeping low/high/ultra binaries without a runtime selector is disk clutter, not scalability.
4. Decide the static-data source of truth: current code has split contracts between `H8StaticData.bin`/`Babel_Dictionary.h8bin` and the absent StreamingAssets `static_data.h8bin`.

## 2026-05-18 - Stable Documentation And Integration Marking Pass

### What Was Wrong

The first audit was too easy to lose because the full result lived in agent logs and CSV evidence. Stable architecture docs still carried an older co-op-side claim that active binary payload scans found `46` aligned files. That claim is stale on current disk.

The first audit also over-classified `Data/Lore/Encyclopedia.h8bin` as having a runtime reader. Reinspection showed a format mismatch: the blob is `H8LR`, while current C# `LoreMmfEncyclopedia` expects an `H8LE` index plus separate payload stream.

### What Was Done

Created stable architecture authority:

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`

Updated stable navigation and actuality docs:

- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`

Reran hygiene:

```text
python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK.json
VERIFY_BINARY_HYGIENE: status=BINARY_HYGIENE_FAILED
binaryCount=65
misalignedCount=16
```

Fresh target inventory remains `47` product/generated binary assets in this audit scope.

### Integration Marking

Confirmed as statically main-runtime wired:

- `Data/Audio/Acoustic_LUT.bin`
- `Data/Visuals/Water_Extinction_Matrix.bin`

Runtime codepath exists, scene/bootstrap not proven:

- `Data/Visuals/Biolum_Profiles.bin`

Reader exists, but production static-data wiring is unresolved:

- `Data/Balance/Baked/H8StaticData.bin`
- `Data/Balance/Baked/Babel_Dictionary.h8bin`

Editor/test only:

- `Data/Economy/Crafting_Costs.h8bin`
- `Data/Narrative/First_Hour_Quests.h8qdag.bin`

Corrected to script/tool-only:

- `Data/Lore/Encyclopedia.h8bin` because current C# lore MMF reader does not read `H8LR`.

### Code/Binary Changes

No runtime C# was changed. No generated binary was hand-edited.

Reason: the only obvious broken payload, `Data/Balance/Baked/Babel_Dictionary.h8bin`, must be rebaked by its owning baker. Hand-padding would risk header/CRC/manifest drift. Most other payloads need domain-owner selectors or boot wiring and are not safe to connect from an audit lane.

### Cinematic Cheats

No new runtime cheat was added. The ledger marks existing cheat payloads:

- Acoustic LUT: cold sampled acoustic fake.
- Water extinction LUT: Beer-Lambert visual fake.
- Biolum profiles: visual pulse fake, reader present but host not proven.
- Tiered refraction/water/tide/Dalton/PDA variants: valid Math LOD ideas, currently not runtime selectors.

### Exact Microseconds Saved

Measured runtime saving: `0 us/frame`.

This pass is documentation and evidence correction only. It prevents false integration work but does not change frame time until owners rebake, wire, quarantine, or remove payloads.

### Independent Cross-Check Delta

Explorer cross-check agreed on the broad classifications and found one apparent conflict: it suggested downgrading `Data/Visuals/Water_Extinction_Matrix.bin` because no normal C# caller invokes `LutArrayResolver.EnsureLoadedAndBound()`.

Manual recheck rejected that downgrade. `LutArrayResolver.EnsureLoadedAndBound()` has `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`; Unity owns the call. Static classification stays `ACTIVE_RUNTIME_WIRED`, with the evidence clarified in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

This is still static source evidence only. Unity import/execution, visual result, frame time, and GC remain `PENDING VERIFICATION`.

Final hygiene recheck after reconciliation:

```text
python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK2.json
VERIFY_BINARY_HYGIENE: status=BINARY_HYGIENE_FAILED
binaryCount=65
misalignedCount=16
```

No source code, Unity YAML, generated binary, or project setting was modified in this pass.
