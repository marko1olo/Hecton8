# Support Corpora Documentation Actuality Audit - 2026-06-05

Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_FILESYSTEM / CSV_HEADER_SAMPLE
Owner: SUPPORT_CORPORA_DOC_ACTUALITY_AUDIT

## Scope

Mission scope: documentation actuality audit for support corpora under data, audio, atmosphere, generated, profile, and template folders.

Write scope used:

- `Docs/Reports/DocumentationCompleteness_20260605/SUPPORT_CORPORA_DOC_ACTUALITY_AUDIT_20260605.md`

No support corpus, stable documentation, source, prefab, asset, scene, import setting, task status, rationale, or log file was edited.

No Unity, dotnet build, importer, test, Play Mode, profiler, GCMonitor, Frame Debugger, player build, scene save, or asset import command was run.

## Authority And Mandates Read

Authority/index files read or sampled:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `Docs/README.md`
- `Docs/Data/Profiles/README.md`
- `Docs/Generated/README.md`
- `Docs/Reports/README.md`
- `Docs/AI_Texturing_Templates/README.md`
- `Docs/Reports/DocumentationCompleteness_20260605/DOC_COMPLETENESS_SYNTHESIS_AND_PATCH_QUEUE_20260605.md`
- `data.md`
- `authoring.md`
- `audio.md`
- `atmosphere.md`
- `rendering.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`
- `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`
- `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`
- `Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`
- `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`

Mandates followed:

- `QA_Evidence_Text_Filter_Audit`
- `TOOL_Designer_Facades_CSV_Binary_Bridge`
- `DATA_Runtime_Struct_Layout_ARM64`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `CORE_Weather_Abyssal_FlowField_Currents`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`

## Static Corpus Inventory

Target support corpus markdown entry points found:

- `Docs/Data/Profiles/README.md`
- `Docs/Generated/README.md`
- `Docs/Reports/README.md`
- `Docs/AI_Texturing_Templates/README.md`

Target support corpus markdown entry points missing:

- `Docs/Data/README.md`
- `Docs/Audio/README.md`
- `Docs/Atmosphere/README.md`

Target support corpus files sampled by path and leading metadata only:

- `Docs/Data/hull_materials.csv`
- `Docs/Data/lighting_gradient_profiles.csv`
- `Docs/Data/light_culling_profiles.csv`
- `Docs/Data/Profiles/ambient_lighting_profiles.csv`
- `Docs/Data/Profiles/flora_biome_sway_profiles.csv`
- `Docs/Data/Profiles/water_extinction_profiles.csv`
- `Docs/Data/Profiles/water_optics_profiles.csv`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_profile_usage_20260605.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/Audio/audio_stem_rules.csv`
- `Docs/Audio/dialogue_script.csv`
- `Docs/Audio/synth_presets.csv`
- `Docs/Atmosphere/gas_diffusion_profiles.csv`
- `Docs/Generated/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.json`
- `Docs/Generated/DEPENDENCY_GRAPH.cache.json`

## Audit Answers

### Are support corpora marked as authoring/profile/template data, not runtime proof?

Partially.

Clear markings:

- `Docs/README.md` classifies `Docs/Data`, `Docs/Audio`, `Docs/Atmosphere`, and `Docs/AI_Texturing_Templates` as support corpora and explicitly separates them from runtime proof.
- `Docs/Data/Profiles/README.md` marks profile CSVs as `STATIC AUTHORING DATA`, not runtime proof or Data Monolith payload authority.
- `Docs/Generated/README.md` marks dependency graph outputs as generated/static only and warns that checked-in generated files can be stale.
- `Docs/AI_Texturing_Templates/README.md` marks the folder as editor-output templates, not runtime assets, proof artifacts, or quality-scaling contracts.
- `Docs/Reports/README.md` says reports are evidence snapshots, not doctrine.

Weak markings:

- `Docs/Data` root has CSV files but no local README. Agents entering through `Docs/Data/hull_materials.csv`, `Docs/Data/lighting_gradient_profiles.csv`, or `Docs/Data/light_culling_profiles.csv` must rely on the higher-level `Docs/README.md`.
- `Docs/Audio` has six CSVs and no local README. The folder has no local statement that ledgers/remediation/profile usage are evidence/support data only and not DSP/runtime readiness.
- `Docs/Atmosphere` has `gas_diffusion_profiles.csv` and no local README. The stable route card references it as cold tuning input, but the folder itself does not state this boundary.

### Are Data Monolith, audio DSP/runtime, atmosphere simulation, and AI texturing boundaries routed to correct stable docs?

Partially.

Correct stable destinations exist:

- Data Monolith runtime/file proof routes to `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`, `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `data.md`, and `authoring.md`.
- Audio DSP/runtime routes to `audio.md`, `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`, `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`, and topology rows in `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` / `SOURCE_SYSTEMS_REALITY_MAP.md`.
- Atmosphere simulation routes to `atmosphere.md`, `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`, `Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`, and atmosphere rows in `PROJECT_RUNTIME_TOPOLOGY.md` / `SOURCE_SYSTEMS_REALITY_MAP.md`.
- AI texturing/template boundaries route conceptually to `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `rendering.md`, and `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`.

Routing gaps:

- `Docs/Data/Profiles/README.md` says not Data Monolith authority, but does not link the Data Monolith stable docs by path.
- `Docs/Audio` lacks a README that routes CSV ledgers and stem/dialogue/synth support files to the audio runtime/DSP stable docs.
- `Docs/Atmosphere` lacks a README that routes `gas_diffusion_profiles.csv` to the base atmosphere route card and atmosphere bible.
- `Docs/AI_Texturing_Templates/README.md` names editor menu outputs but does not link the texture generation/material/procedural asset stable docs or the ARM packing pipeline.

### Are generated artifacts and profiles indexed enough for agents to find current owners without reading archives?

Partially.

Enough:

- `Docs/Data/Profiles/README.md` lists all four profile CSVs in that subfolder and gives schema/hash/owner strings.
- `Docs/Generated/README.md` names dependency graph outputs, the optional absent H-Phi atlas, and validator expectations.
- `Docs/AI_Texturing_Templates/README.md` states editor tool ownership at menu level and expected suffixes.

Not enough:

- `Docs/Data` root CSVs are not covered by `Docs/Data/Profiles/README.md`.
- `Docs/Audio` files have no folder index or owner map. Large ledgers include evidence columns and pending statuses, but there is no local entry point telling agents which CSVs are authoring support, evidence ledgers, remediation queues, or runtime-blocked sidecar inputs.
- `Docs/Atmosphere/gas_diffusion_profiles.csv` is not indexed locally.
- `Docs/Generated/README.md` still records the 2026-05-28 dependency graph state while `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` says that graph count is stale against the 2026-06-01 first-party asmdef count until regenerated.

## Top 10 Gaps

1. `Docs/Audio/README.md` is missing. This is the largest local entry-point gap: six audio CSVs exist, including ledgers and remediation matrices, but the folder does not route agents to `audio.md`, `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`, or `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`.

2. `Docs/Atmosphere/README.md` is missing. `Docs/Atmosphere/gas_diffusion_profiles.csv` is referenced by `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md` as cold tuning input, but the folder has no local boundary that it is authoring/profile data, not atmosphere simulation proof.

3. `Docs/Data/README.md` is missing. `Docs/Data` root contains CSVs outside `Docs/Data/Profiles`, but only the subfolder has a README. The root support corpus relies on `Docs/README.md` instead of a local owner/schema/runtime-proof boundary.

4. `Docs/Data/light_culling_profiles.csv` has no sampled header row, schema metadata, owner, or evidence label. It starts directly with data-like rows, so agents cannot identify parser/owner/readiness from the file without source search.

5. `Docs/Data/hull_materials.csv` has a comment header but no sampled schema version, schema hash, owner, parser, or runtime-proof warning. The name implies physics/material gameplay relevance, so the missing boundary is higher risk than a pure visual tuning table.

6. `Docs/Audio/dialogue_script.csv` has column headers but no sampled schema/version/owner/evidence metadata. `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md` defines the route, but the CSV itself does not point to it.

7. `Docs/Audio/audio_asset_ledger.csv` contains `PENDING_OWNER` and `PENDING_ADDRESSABLES` rows, but the folder lacks a README that states this ledger is static evidence/remediation input, not asset-import, Addressables, mixer, DSP, or runtime readiness.

8. `Docs/Audio/audio_profile_usage_20260605.csv` is large and has an `evidence_class` column, but no local README explains its source scan boundary, owner, regeneration command, or connection to audio proof artifacts. Static rows can be mistaken for current runtime configuration.

9. `Docs/Generated/README.md` and `Docs/Generated/DEPENDENCY_GRAPH.md` are stale by their own surrounding authority context: `Docs/Generated/README.md` records 2026-05-28 graph generation, while `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` says the generated graph is stale against the 2026-06-01 asmdef count until regenerated.

10. `Docs/AI_Texturing_Templates/README.md` correctly says templates are not runtime proof, but it does not link the stable asset/material authority chain: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `rendering.md`, and `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`.

## Boundary Findings

Data Monolith:

- Stable docs correctly state that `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` presence, header parsing, and CLI proof do not equal Unity import, runtime boot, player, profiler, or save/load proof.
- Support folders do not claim Data Monolith readiness.
- Local support corpus routing to those stable docs is incomplete in `Docs/Data`.

Audio DSP/runtime:

- Stable docs correctly keep audio runtime proof pending and reject managed callback synthesis/decode as production route.
- `Docs/README.md` correctly says `Docs/Audio` is content/profile data only.
- `Docs/Audio` local indexing is absent, so agents entering through audio CSVs can miss the DSP/native proof wall.

Atmosphere simulation:

- Stable docs define base atmosphere ownership, DataVault buffers, phases, failure modes, and proof-before-GREEN requirements.
- `Docs/README.md` correctly says `Docs/Atmosphere` is authoring data only.
- `Docs/Atmosphere` local indexing is absent, so `gas_diffusion_profiles.csv` does not self-route to the atmosphere route card.

AI texturing/templates:

- `Docs/AI_Texturing_Templates/README.md` correctly says editor-output templates are not runtime assets, proof artifacts, or quality contracts.
- The README lacks direct links to stable generated texture/material/procedural asset authorities.

Generated artifacts:

- `Docs/Generated/README.md` has the correct generated/static boundary.
- Current dependency graph freshness is PENDING VERIFICATION because the generated graph state predates later static source counts.

## Regression Model

- CPU: static filesystem/doc/header sampling only. No runtime CPU path touched.
- GC: no runtime code changed. No `0 B/frame` claim.
- Memory: no Unity asset import, Addressables, DataVault, audio, atmosphere, or generated asset path touched.
- Cadence: no dispatcher, importer, audio, atmosphere, generation, or test cadence touched.
- Correctness: report adds static documentation gaps only. Stable docs remain unchanged.

## Continuous Quality Consequences

`GlobalQualityWeight` does not change documentation truth or proof state. It changes how much support-corpus navigation and freshness scoring is worth maintaining.

| Lane label | Consequence |
|---|---|
| Low | Local README files must prevent false runtime-proof claims without forcing agents to read large CSVs or archives. |
| Middle | Support CSVs should expose owner, schema/version, evidence class, and stable route doc at the folder or file header level. |
| High | Generated and profile corpora should expose regeneration command, last accepted artifact, stale-state warning, and proof class. |
| Ultra | Full support-corpus graph checks can score freshness across profiles, generated docs, generated assets, and proof artifacts. Runtime readiness still requires Unity/player/profiler artifacts. |

## Residual Risks

- CSV bodies were not bulk-read by design. Findings are based on folder inventory, README/index content, and leading metadata rows only.
- Other agents have unrelated dirty worktree changes. This report does not classify or depend on those changes.
- `Docs/GeneratedAssets` appears to be a separate generated asset support corpus, but it was not part of the requested targeted README set. If it is intended to be active support corpus, it needs a separate audit.
- No runtime/data-monolith/audio/native/import readiness is claimed.

Final status: PENDING VERIFICATION.
