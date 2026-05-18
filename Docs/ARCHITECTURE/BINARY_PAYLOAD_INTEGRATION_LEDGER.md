# HECTON-8 Binary Payload Integration Ledger

Date: 2026-05-18
Owner lane: H8BIN_GRAVEYARD_AUDITOR
Status: STATIC SOURCE / FILESYSTEM LEDGER, RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Purpose

This file is the stable architecture ledger for generated HECTON binary payloads found under
`Data`, `Assets/_Project/Data`, and the current archived black-box dump path. It exists because
agent logs and CSV scans are evidence trails, not durable project authority.

This ledger does not authorize deletion by itself. A file is safe to delete or quarantine only
after its owning gameplay/rendering/data system confirms that no build, bake, runtime convention,
Addressables hook, StreamingAssets copy step, or external packager consumes it.

## Evidence

- Inventory artifact: `Docs/Archive/Batch008/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv`
- Current hygiene artifact: `Docs/Archive/Batch008/AgentLogs/BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK2.json`
- Original audit log: `Docs/Archive/Batch008/AgentLogs/LOG_H8BIN_GRAVEYARD_AUDITOR.md`
- Auditor status: `Docs/Archive/Batch008/Tasks/Status_H8BIN_GRAVEYARD_AUDITOR.md`
- Archive movement log: `Docs/Archive/Batch008/AgentLogs/LOG_ARCHIVE_BATCH_008.md`
- Verifier: `Tools/VerifyBinaryHygiene.py`
- Recheck command before Batch008 archive move: `python Tools\VerifyBinaryHygiene.py --report <active AgentLogs output path now archived as the current hygiene artifact above>`

Current recheck result before SHINOBU_50 alignment repair:

- Target product/generated payload set: 47 files.
- Global verifier scope: 65 `.bin` / `.h8bin` files.
- Global verifier status: `BINARY_HYGIENE_FAILED`.
- Misaligned count: 16.
- Product misalignment: `Data/Balance/Baked/Babel_Dictionary.h8bin`, 1295 bytes, remainder 15.
- Other 15 misalignments: Bakery editor/plugin fixtures under `Assets/Editor/x64/Bakery`.

SHINOBU_50 update on 2026-05-18:

- `Data/Balance/Baked/Babel_Dictionary.h8bin` is now 1296 bytes, remainder 0, with header `FileByteLength=1296` and payload CRC `0x199CAC7A`.
- `Data/Balance/Baked/H8StaticData.bin` now stores the same Babel CRC in its static header.
- `Docs/AgentLogs/BinaryHygiene_SHINOBU_50.json` still reports global `BINARY_HYGIENE_FAILED`, but no longer because of the balance Babel payload. Remaining failures are third-party Bakery binaries plus archived dump artifacts.

`Assets/_Project/Data/UI/GlitchTable.bytes` is included in this ledger because the user-requested
scope was binary assets, not only the verifier's `.bin` / `.h8bin` extension set.

## Classification Key

| Class | Meaning |
|---|---|
| `ACTIVE_RUNTIME_WIRED` | Current main runtime source resolves or opens the exact payload path. Unity scene/profiler proof is still pending unless stated separately. |
| `ACTIVE_CODEPATH_NOT_SCENE_PROVEN` | A runtime component can load the file, but no prefab/scene/bootstrap reference proves that component is live. |
| `READER_PRESENT_NOT_WIRED` | A C# reader exists for this exact format/path family, but no production instantiation was found. |
| `EDITOR_OR_TEST_ONLY` | Current exact load is editor tooling, tests, or inspector-only code. |
| `SCRIPT_TOOL_ONLY` | Python/data docs/manifests know the file; first-party runtime/editor C# does not currently load it. |
| `STATIC_LEDGER_MIRROR_ONLY` | Binary asset mirrors data embedded directly in code. |
| `ARCHIVE_DUMP_ONLY` | Historical dump evidence, not product content. |
| `THIRD_PARTY_EDITOR_BINARY` | Vendor/editor binary outside HECTON Python payload ownership. |

## Hard Current Findings

- Three product payloads are proven by static source to be wired into main runtime:
  `Data/Audio/Acoustic_LUT.bin`, `Data/Visuals/Water_Extinction_Matrix.bin`, and
  `Data/Visuals/Biolum_Profiles.bin`.
  Water-extinction wiring is through Unity's `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` hook
  on `LutArrayResolver.EnsureLoadedAndBound`, not through a scene/prefab caller.
- `Data/Visuals/Biolum_Profiles.bin` has a real runtime reader in
  `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`. SHINOBU_74 added a
  scene-local `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` host fallback on 2026-05-18 and then
  removed the singleton/Awake self-registration guard in favor of an atomic process ownership claim.
  The code path is now statically wired. The indirect vegetation shader consumes the packed
  `_BiolumGpuColorBuffer` by instance ID and guards reads by the exact published GPU page count.
  The four-state Dear Lie fallback is selected by template/species group modulo four in the
  indirect vegetation shader, packed into the existing spatial pulse TEXCOORD lane rather than a
  new interpolator. Its runtime frame counter now advances once per dispatcher Tick rather than
  through blackbox telemetry writes, so fault dumps cannot perturb mock RNG or shader frame clock.
  The CPU oscillator Burst job now uses deterministic float mode for DTO phase/color mutation.
  The active 50,000-instance CPU path uses a smoothed triangle/hash waveform fake instead of
  per-instance trigonometric pulse evaluation, and squared-distance wavefront/falloff math instead
  of per-instance sqrt for presentation-only glow ripples. `GlobalQualityWeight` now also drives
  update cadence from 5Hz low-quality scheduling to per-frame high-quality scheduling.
  Unity shader import, scene, profiler, and Frame Debugger proof are
  still pending.
- `Data/Balance/Baked/H8StaticData.bin` and `Data/Balance/Baked/Babel_Dictionary.h8bin` are small
  balance-store artifacts. They are not the authoritative StreamingAssets DataMonolith
  `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, which is currently absent.
- `Data/Balance/Baked/Babel_Dictionary.h8bin` alignment is repaired. Header/checksum and alignment
  semantics are owned by `H8DataBaker`; future dictionary changes must go through the baker path.
- `Data/Lore/Encyclopedia.h8bin` is an `H8LR` raw UTF-8 lore blob. Current C# `LoreMmfEncyclopedia`
  expects an `H8LE` index plus separate payload stream; it is not an H8LR reader. Treat the H8LR blob
  as script/tool-only until a dedicated H8LR runtime reader or converter exists.
- Most low/toaster/high/ultra variants are legitimate Math LOD payload ideas, but without a tier
  selector they are disk ballast, not scalability.

## Safe Integration Rules

1. Binary readers must load in boot/cold paths or explicit lazy-read paths only. No JSON parsing, file
   probing, string construction, or heap allocation in `Tick`, `LateUpdate`, `FixedUpdate`, Burst jobs,
   shader upload loops, or per-frame UI paths.
2. Runtime systems must acquire payload ownership through existing domain owners: `GlobalDataVault`,
   `GlobalRegistry` interfaces, typed signal lanes, or cold bootstrap injection. Do not wire direct
   cross-domain concrete references.
3. Tiered payload families require hysteresis. Low, middle, high, and ultra selection must not flip
   every frame or during the same visual beat.
4. If a payload is a visual/audio fake, prefer it over live simulation. If the fake saves CPU, spend
   the saved budget on high-tier visual/audio richness, not on unnecessary physical truth.
5. Never patch generated binary bytes by hand when the format has a header, CRC, offsets, or manifest.
   Fix the generator and rebake.

## Active Payloads

| File | Current status | Runtime/code evidence | Action |
|---|---|---|---|
| `Data/Audio/Acoustic_LUT.bin` | `ACTIVE_RUNTIME_WIRED` | `SpatialAudioManager.cs` defines `AcousticLutRelativePath`, calls `TryLoadAcousticLutFallbackCold`, reads the file in a cold init path, and the audio manager is prefab/bootstrap wired. | Keep. This is a valid acoustic cinematic cheat: sampled Sabine/damping lookup instead of live acoustic solving. |
| `Data/Visuals/Water_Extinction_Matrix.bin` | `ACTIVE_RUNTIME_WIRED` | `LutArrayResolver.EnsureLoadedAndBound` is marked `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`, resolves `Data/Visuals/Water_Extinction_Matrix.bin`, and `GlobalShaderDispatcher` consumes the bound texture. | Keep. This is a valid Beer-Lambert visual LUT fake. Runtime proof still needs Unity/profiler evidence. |

## Candidate Payloads With Reader But Missing Wiring

| File | Current status | Mechanic | Logical insertion point | Blocker |
|---|---|---|---|---|
| `Data/Visuals/Biolum_Profiles.bin` | `ACTIVE_RUNTIME_WIRED`, shader/scene/profiler proof pending | Bioluminescence pulse/color/profile table for global glow state and species tuning. | Keep the `BiolumPulseSyncRuntime` scene-local runtime host fallback and verify in Unity shader import, Profiler, and Frame Debugger. | Static boot hook exists with atomic ownership claim, no static runtime instance; `Hecton_IndirectVegetation.shader` now decodes `_BiolumGpuColorBuffer` by instance ID, clamps reads to `_publishedGpuColorCount`, and selects four global fallback states by template/species group; runtime capture still required. |
| `Data/Balance/Baked/H8StaticData.bin` | `READER_PRESENT_NOT_WIRED` | Small static balance record table with `StaticDataStore.OpenDefault()`. | Either make it a dev-only section producer for the DataMonolith, or wire it as a temporary Core data service behind a stable interface. | Current production authority is the absent StreamingAssets DataMonolith, not this small file. |
| `Data/Balance/Baked/Babel_Dictionary.h8bin` | `READER_PRESENT_NOT_WIRED`, `ALIGNED_PRODUCT_FILE` | Small Babel string pool paired with `H8StaticData.bin`. | Keep aligned through `H8DataBaker`, then wire only with the chosen static-data source of truth. | 1296 bytes, 16-byte aligned, payload CRC `0x199CAC7A`. |

## Editor/Test Only Payloads

| File | Current status | Mechanic | Logical insertion point | Action |
|---|---|---|---|---|
| `Data/Economy/Crafting_Costs.h8bin` | `EDITOR_OR_TEST_ONLY` | Crafting recipe/ingredient SoA hydration payload. | Runtime crafting/economy DataVault importer if the crafting owner wants binary recipes. | Do not wire from this audit. Current exact consumer is `EconomyRecipeTunerWindow`. |
| `Data/Narrative/First_Hour_Quests.h8qdag.bin` | `EDITOR_OR_TEST_ONLY` | First-hour quest DAG binary. | Quest bootstrap through `QuestDagDataLoading.TryLoadOshinoOrGenerateMock` if the quest owner promotes it. | Current caller found is editor inspector `NarrativeDagInspectorWindow`. |

## Full Product/Generated Inventory

| # | File | Bytes | Class | Responsibility / mechanic | Logical application or action |
|---:|---|---:|---|---|---|
| 1 | `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` | 1534512 | `SCRIPT_TOOL_ONLY` | Full `H8BD` Babel localization dictionary, hashed text pool for localization/content. | Package or copy through a real localization bootstrap if required; otherwise it is Unity import ballast. Exact asset GUID/path is not runtime-wired. |
| 2 | `Assets/_Project/Data/UI/GlitchTable.bytes` | 64 | `STATIC_LEDGER_MIRROR_ONLY` | HUD glitch glyph substitution table. | Current `GlitchTable.cs` embeds the bytes directly. Keep only if designers need the asset as authoring evidence. |
| 3 | `Data/AI/Navigation_Tuning.h8bin` | 1280 | `SCRIPT_TOOL_ONLY` | AI path/potential-field tuning cache. | Logical owner is AI navigation bootstrap/DataVault import. No main runtime load found. |
| 4 | `Data/Audio/Acoustic_LUT.bin` | 524288 | `ACTIVE_RUNTIME_WIRED` | Acoustic RT60/damping LUT. | Keep and verify in Unity with GC/profiler. |
| 5 | `Data/Balance/Baked/Babel_Dictionary.h8bin` | 1296 | `READER_PRESENT_NOT_WIRED`, `ALIGNED_PRODUCT_FILE` | Small balance string pool. | Alignment repaired. Do not wire until source-of-truth decision is made. |
| 6 | `Data/Balance/Baked/H8StaticData.bin` | 896 | `READER_PRESENT_NOT_WIRED` | Small static balance DTO lookup blob. | Reconcile with DataMonolith. Do not let both contracts become parallel truth. |
| 7 | `Data/Economy/Crafting_Costs.h8bin` | 7424 | `EDITOR_OR_TEST_ONLY` | Crafting recipe/ingredient cost table. | Promote only through economy owner and DataVault importer. |
| 8 | `Data/Economy/Crafting_Costs_Toaster.h8bin` | 2464 | `SCRIPT_TOOL_ONLY` | Reduced low-tier crafting-cost payload. | Needs runtime tier selector before it has value. |
| 9 | `Data/Economy/Ore_Distribution.h8bin` | 1776 | `SCRIPT_TOOL_ONLY` | Deterministic ore distribution / LCG spawn table. | Logical owner is resource spawn. No load found. |
| 10 | `Data/Economy/Submarine_Upgrade_Stat_Map.h8bin` | 176 | `SCRIPT_TOOL_ONLY` | Submarine upgrade stat map/curve. | Logical owner is submarine upgrade/progression. No load found. |
| 11 | `Data/Ecosystem/Organic_Entropy_Regrowth.h8bin` | 195344 | `SCRIPT_TOOL_ONLY` | Organic entropy/regrowth table. | Logical owner is ecosystem regrowth. No load found. |
| 12 | `Data/Environment/Tide_Harmonics.bin` | 9600 | `SCRIPT_TOOL_ONLY` | Base tide harmonic coefficients. | Logical owner is environment tide system. No load found. |
| 13 | `Data/Environment/Tide_Harmonics.index.h8bin` | 96 | `SCRIPT_TOOL_ONLY` | Tide harmonic sidecar/index. | Must be wired together with a tide reader, not independently. |
| 14 | `Data/Environment/Tide_Harmonics_Low.bin` | 2400 | `SCRIPT_TOOL_ONLY` | Low-tier tide approximation. | Needs environment tier selector with hysteresis. |
| 15 | `Data/Environment/Tide_Harmonics_Ultra.bin` | 38400 | `SCRIPT_TOOL_ONLY` | Ultra tide harmonic variant. | Needs environment tier selector and visual overkill policy. |
| 16 | `Data/Habitat/HabitatPressureBudget.h8bin` | 2704 | `SCRIPT_TOOL_ONLY` | Habitat pressure/failsafe budget table. | Logical owner is habitat logistics/pressure. No load found. |
| 17 | `Data/Localization/en_US.bin` | 60928 | `SCRIPT_TOOL_ONLY` | English localization binary. | Logical owner is localization bootstrap. No main load found. |
| 18 | `Data/Localization/en_US_Taxonomy.h8bin` | 27536 | `SCRIPT_TOOL_ONLY` | Taxonomy localization/classification payload. | Logical owner is taxonomy/scanner/localization. No load found. |
| 19 | `Data/Localization/Radio/marauder_radio_interceptions.h8bin` | 7872 | `SCRIPT_TOOL_ONLY` | Marauder radio interception payload. | Logical owner is audio log/radio narrative. No load found. |
| 20 | `Data/Lore/Encyclopedia.h8bin` | 41920 | `SCRIPT_TOOL_ONLY` | `H8LR` raw UTF-8 lore blob with two records. | Needs dedicated H8LR runtime reader or conversion to the existing H8LE index+payload model. Current `LoreMmfEncyclopedia` cannot read it directly. |
| 21 | `Data/Lore/PdaTechnicalLogs.h8bin` | 59120 | `SCRIPT_TOOL_ONLY` | Full `H8PT` PDA technical log table/text/extra visuals. | Logical owner is PDA data-log UI. Needs zero-GC lookup reader before use. |
| 22 | `Data/Lore/PdaTechnicalLogs_Toaster.h8bin` | 19120 | `SCRIPT_TOOL_ONLY` | Compact low-tier PDA technical log payload. | Needs PDA tier selector before use. |
| 23 | `Data/Narrative/First_Hour_Quests.h8qdag.bin` | 496 | `EDITOR_OR_TEST_ONLY` | Quest DAG binary. | Promote only through quest runtime bootstrap. |
| 24 | `Data/Physics/Submarine_RuntimePack.bin` | 1152 | `SCRIPT_TOOL_ONLY` | Submarine hydrodynamics/runtime verification pack. | Logical owner is submarine physics. No load found. |
| 25 | `Data/Precomputed/Atmosphere/atmosphere_density_matrix_rgba16f.bin` | 1024 | `SCRIPT_TOOL_ONLY` | Atmosphere density RGBA16F LUT. | Logical owner is atmosphere rendering. No load found. |
| 26 | `Data/Precomputed/Atmosphere/atmosphere_sky_gradient_rgba16f.bin` | 262144 | `SCRIPT_TOOL_ONLY` | Sky gradient RGBA16F LUT. | Logical owner is atmosphere/sky renderer. No load found. |
| 27 | `Data/Precomputed/caustics_dispersion_offsets.bin` | 1216 | `SCRIPT_TOOL_ONLY` | Caustics dispersion offset table. | Logical owner is caustics shader/upload path. No load found. |
| 28 | `Data/Precomputed/dalton_gas_toxicity.bin` | 128128 | `SCRIPT_TOOL_ONLY` | Dalton gas toxicity base matrix. | Logical owner is atmosphere/toxicity hazard. No load found. |
| 29 | `Data/Precomputed/dalton_gas_toxicity_overkill.bin` | 96112 | `SCRIPT_TOOL_ONLY` | High/overkill toxicity variant. | Needs hazard/atmosphere tier selector. |
| 30 | `Data/Precomputed/dalton_gas_toxicity_toaster.bin` | 4080 | `SCRIPT_TOOL_ONLY` | Low-tier toxicity variant. | Needs hazard/atmosphere tier selector. |
| 31 | `Data/Precomputed/gerstner_wave_weather.bin` | 32000 | `SCRIPT_TOOL_ONLY` | Gerstner wave/weather LUT. | Logical owner is water/weather. No load found. |
| 32 | `Data/Precomputed/Reverb_LUT.bin` | 262400 | `SCRIPT_TOOL_ONLY` | Reverb/acoustic validation LUT. | Runtime already uses `Data/Audio/Acoustic_LUT.bin`; avoid duplicate acoustic truth. |
| 33 | `Data/Precomputed/sabine_reverb_rt60.bin` | 4000 | `SCRIPT_TOOL_ONLY` | Sabine RT60 lookup. | Superseded for runtime by `Acoustic_LUT.bin` unless audio owner says otherwise. |
| 34 | `Data/System/VFX_Budgets.h8bin` | 1344 | `SCRIPT_TOOL_ONLY` | VFX particle/VRAM budget catalog. | Logical owner is VFX budget/scalability bootstrap. No load found. |
| 35 | `Data/System/Visual_Scalability_Matrix.bin` | 2048 | `SCRIPT_TOOL_ONLY` | Visual LOD/scalability matrix. | Should be wired to visual scalability authority before any low/high/ultra payload selection. No load found. |
| 36 | `Data/UX/VR_Comfort_Profiles.h8bin` | 1472 | `SCRIPT_TOOL_ONLY` | VR comfort profile table. | Logical owner is UX/VR comfort runtime. No load found. |
| 37 | `Data/UX/VR_Comfort_Profiles_Toaster.h8bin` | 1120 | `SCRIPT_TOOL_ONLY` | Low-tier VR comfort profile table. | Needs UX tier selector. |
| 38 | `Data/UX/VR_Comfort_RTXOverkill.h8bin` | 560 | `SCRIPT_TOOL_ONLY` | High/overkill VR comfort supplement. | Needs UX tier selector and headset/platform guard. |
| 39 | `Data/Visuals/Biolum_Profiles.bin` | 25936 | `ACTIVE_RUNTIME_WIRED`, shader/scene/profiler proof pending | Bioluminescence profile table. | SHINOBU_74 added the runtime host fallback, purged static-instance/Awake ownership, wired indirect vegetation packed-buffer shader consumption, guarded shader reads by actual published GPU page count, packed the Dear Lie sync group into the existing spatial pulse TEXCOORD lane, detached frame counter advancement from blackbox telemetry writes, moved the CPU oscillator Burst job to deterministic float mode, replaced per-instance trigonometric pulse work with a smoothed triangle/hash waveform fake, replaced active pulse/damage sqrt distance with squared-distance math, and made `GlobalQualityWeight` drive update cadence from 5Hz to per-frame; verify with Unity shader import, Profiler, and Frame Debugger before claiming measured frame impact. |
| 40 | `Data/Visuals/Refraction_LUT_RGBA16F.bin` | 524288 | `SCRIPT_TOOL_ONLY` | Base refraction LUT. | Logical owner is water/refraction shader path. No load found. |
| 41 | `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin` | 131072 | `SCRIPT_TOOL_ONLY` | Minimal low-tier refraction LUT. | Needs visual scalability selector. |
| 42 | `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin` | 2097152 | `SCRIPT_TOOL_ONLY` | Ultra refraction LUT. | Needs visual scalability selector and VRAM budget gate. |
| 43 | `Data/Visuals/Water_Extinction_Matrix.bin` | 393216 | `ACTIVE_RUNTIME_WIRED` | Base water extinction LUT. | Keep and profile. |
| 44 | `Data/Visuals/Water_Extinction_Matrix_Overkill.bin` | 1572864 | `SCRIPT_TOOL_ONLY` | High/overkill water extinction variant. | Current resolver loads only the base file. Needs selector. |
| 45 | `Data/Visuals/Water_Extinction_Matrix_Toaster.bin` | 24576 | `SCRIPT_TOOL_ONLY` | Toaster water extinction variant. | Current resolver uses analytical fallback on low-memory targets, not this file. |
| 46 | `Data/Visuals/Water_Fog_Density_LUT.bin` | 3008 | `SCRIPT_TOOL_ONLY` | Water fog density preview/validation LUT. | No main runtime load found. |
| 47 | `Docs/Archive/Batch007/AgentLogs/Dump_HEADLESS_SCENARIO_RUNNER.bin` | 16 | `ARCHIVE_DUMP_ONLY` | Archived black-box/headless dump. | Keep only as archive evidence; never package as product content. |

## Non-Target Binary Verifier Contamination

The current hygiene verifier also scans 19 Bakery editor/plugin `.bin` files under
`Assets/Editor/x64/Bakery`. They are not HECTON Python-generated payloads. If the hygiene gate is
intended to police product data only, the verifier needs an explicit vendor/editor exclusion. If the
gate is intended to police every `.bin`, Bakery fixture ownership must be handled by a third-party
asset hygiene task, not by data payload owners.

| # | File | Bytes | Alignment | Classification | Action |
|---:|---|---:|---|---|---|
| B1 | `Assets/Editor/x64/Bakery/hwtestdata/alphabuffer.bin` | 2 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B2 | `Assets/Editor/x64/Bakery/hwtestdata/alphaid2.bin` | 0 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B3 | `Assets/Editor/x64/Bakery/hwtestdata/direct0.bin` | 52 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B4 | `Assets/Editor/x64/Bakery/hwtestdata/heightmaps.bin` | 0 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B5 | `Assets/Editor/x64/Bakery/hwtestdata/ib32.bin` | 28 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B6 | `Assets/Editor/x64/Bakery/hwtestdata/lmid.bin` | 4 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B7 | `Assets/Editor/x64/Bakery/hwtestdata/lmlod.bin` | 4 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B8 | `Assets/Editor/x64/Bakery/hwtestdata/lms.bin` | 18 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B9 | `Assets/Editor/x64/Bakery/hwtestdata/settings.bin` | 10 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B10 | `Assets/Editor/x64/Bakery/hwtestdata/vbtrace.bin` | 96 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B11 | `Assets/Editor/x64/Bakery/hwtestdata/vbtraceUV0.bin` | 32 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B12 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part0.bin` | 7 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B13 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part1.bin` | 12597 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B14 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part2.bin` | 628 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B15 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part3.bin` | 88 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B16 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part0.bin` | 7 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B17 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part1.bin` | 12497 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B18 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part2.bin` | 584 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B19 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part3.bin` | 84 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |

Other binary-like assets observed outside the product/generated target set:

- `Assets/_Project/Diagnostics/auto_baseline_test.raw` - diagnostics raw evidence, not a generated HECTON runtime payload in this pass.
- `Assets/MapMagic/Generators/Biomes/Runtime/Sources/*.raw` - MapMagic biome raw source assets, third-party/runtime authoring material.
- `Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/ConfigData.bytes` - Odin editor plugin config.

## Integration Backlog

| Priority | Task | Owner domain | Reason |
|---:|---|---|---|
| 0 | Keep `Data/Balance/Baked/Babel_Dictionary.h8bin` rebaked through `H8DataBaker`. | Core data / baker owner | SHINOBU_50 repaired the 16-byte alignment failure; future drift must fail hygiene again. |
| 1 | Decide one static-data source of truth: StreamingAssets DataMonolith or small `Data/Balance/Baked` stores. | Core data / bootstrap | Parallel static-data contracts will produce false reads and stale payloads. |
| 2 | Verify `BiolumPulseSyncRuntime` host in Unity scene/profiler. | VFX | Runtime host fallback is statically wired through an atomic ownership claim and latest narrow Assembly-CSharp build passes; Unity import, Frame Debugger, and Profiler proof are still missing. |
| 3 | Add H8LR reader or convert `Encyclopedia.h8bin` to H8LE index+payload. | Narrative/PDA | Current generated lore blob is not consumed by the current C# MMF reader. |
| 4 | Promote PDA `H8PT` reader if PDA technical logs are intended for runtime. | PDA/UI/Narrative | Binary has good lookup contract but no runtime reader found. |
| 5 | Build a visual scalability selector for refraction, water-extinction variants, VFX budgets, VR comfort, tide, and Dalton variants. | Rendering/UX/Environment | Tier binaries are useless without hysteresis and platform gates. |
| 6 | Scope `Tools/VerifyBinaryHygiene.py` to product payloads or explicitly exempt Bakery. | Build/QA | Current gate mixes product payload drift with vendor editor fixtures. |

## Regression Model

CPU: documentation-only pass, no runtime CPU change. Future payload wiring must stay cold-path or
lazy-read and must not add per-frame file probes.

GC: documentation-only pass, no managed allocation change. Future readers must use caller-owned
buffers, `NativeArray`, `GlobalDataVault`, or fixed cold allocations only.

Memory: no payloads were deleted or loaded by this pass. Future tier selectors must account for MX350
VRAM and avoid loading low/base/ultra variants simultaneously unless explicitly budgeted.

Cadence: tier changes require hysteresis. Immediate low/high/ultra flipping is rejected.

Correctness: stale generated binary claims are subordinated to fresh filesystem and verifier output.
The stale "46 aligned payloads" statement in older docs is not current truth.

## Hot Path Impact

This ledger changes docs only. Runtime hot-path and GC impact were not measured in this pass; no
per-frame or allocation saving is claimed. No C# source was modified.

## Failure Modes

- Reintroducing a misaligned `Babel_Dictionary.h8bin` can break strict binary hygiene gates and any
  reader that assumes 16-byte sections.
- Keeping H8LR lore without a runtime reader produces false content-readiness.
- Keeping multiple acoustic, refraction, water, tide, and toxicity tables without selectors inflates
  package/import surface and can hide stale data.
- Broad verifier scope can fail product gates because of third-party editor fixtures unrelated to
  HECTON payload ownership.
