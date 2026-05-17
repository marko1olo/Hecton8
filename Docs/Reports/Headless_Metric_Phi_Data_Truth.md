# METRIC_PHI Data Truth Audit

Status: DATA_TRUTH_VERIFIED
Evidence class: CLI_PYTHON_STATIC_DATA. Unity/runtime proof remains PENDING VERIFICATION.

## Checks

| Check | Status | Artifact | Detail |
|---|---:|---|---|
| `h_phi_status` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | PHI CALCULATED |
| `h_phi_omega_static` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | VERIFIED MASTER GRADE STATIC_SOURCE ONLY |
| `h_phi_domain_count` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | domain_index_count=85 |
| `h_phi_report_fresh_for_eligible_sources` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | generated_at=2026-05-17T01:46:41 eligible_files=5015 report_files=5015 newest=Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs newest_time=2026-05-17T00:25:53.373942 |
| `h_phi_graph_exists` | PASS | `Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png` | bytes=718046 |
| `top_3_lowest_purity_files` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | Assets/_Project/Scripts/HectonPlayerMovement.cs, Assets/_Project/Scripts/WorldProceduralScatterDirector.cs, Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs |
| `h_phi_data_sovereignty_honesty` | PASS | `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` | DataSovereignty=0.019743027 strict=0.089045936 |
| `project_atlas_85_domain_section` | PASS | `Docs/PROJECT_ATLAS.md` | domain_rows=85 |
| `beer_lambert_basis` | PASS | `Data/Visuals/Water_Extinction_Matrix.json` | BeerLambert / Beer-Lambert extinction with pure-water absorption anchors. |
| `optics_little_endian` | PASS | `Data/Visuals/Water_Extinction_Matrix.json` | byteOrder=little-endian pack=<e |
| `optics_scalability_variants` | PASS | `Data/Visuals/Water_Extinction_Matrix.json` | variants=['main_mx350', 'rtx_overkill', 'toaster_i3'] |
| `optics_stateless_lookup` | PASS | `Data/Visuals/Water_Extinction_Matrix.json` | {'globalRegistryRequired': False, 'hotPathJsonParsingAllowed': False, 'lookupModel': 'stateless_binary_lookup', 'runtimePrivateStateRequired': False} |
| `optics_harmonic_overkill` | PASS | `Data/Visuals/Water_Extinction_Matrix.json` | rtxOverkillData.harmonicNoise present |
| `dalton_basis` | PASS | `Data/Precomputed/dalton_gas_toxicity_manifest.json` | H8.DaltonGasToxicity.v2 |
| `dalton_scalability_tiers` | PASS | `Data/Precomputed/dalton_gas_toxicity_manifest.json` | tiers=['high', 'middle', 'rtx_overkill', 'toaster_i3'] |
| `dalton_stateless_lookup` | PASS | `Data/Precomputed/dalton_gas_toxicity_manifest.json` | {'atlasDomain': '46 Hypoxia & Gas Toxicity', 'atlasFamily': 'Environment and survival', 'dataSovereignty': 'stateless aligned binary lookup; no per-system private solver state', 'hotPathAllocation': '0 B/frame', 'loadCadence': 'cold_load_only', 'phase': 'SIMULATION produces gas danger scalar; VISUAL_SYNC consumes presentation fields', 'rtxOverkillBinary': 'Data/Precomputed/dalton_gas_toxicity_overkill.bin', 'toasterBinary': 'Data/Precomputed/dalton_gas_toxicity_toaster.bin'} |
| `math_lut_little_endian` | PASS | `Data/Precomputed/math_lut_manifest.json` | byteOrder=little-endian |
| `math_lut_sabine_present` | PASS | `Data/Precomputed/math_lut_manifest.json` | sabine_reverb_rt60.bin in manifest |
| `math_lut_caustics_aligned` | PASS | `Data/Precomputed/math_lut_manifest.json` | caustics paddingBytes=4 |
| `sabine_acoustic_basis` | PASS | `Data/Audio/Acoustic_LUT.manifest.json` | recordFormat=<ff surfaceProxy present |
| `acoustic_scalability_tiers` | PASS | `Data/Audio/Acoustic_LUT.manifest.json` | tiers=['high', 'middle', 'rtx_overkill', 'toaster_i3'] |
| `acoustic_stateless_lookup` | PASS | `Data/Audio/Acoustic_LUT.manifest.json` | {'atlasAssemblies': ['Hecton8.Audio.Propagation', 'Hecton8.Audio.Synthesis', 'Hecton8.Audio.Echolocation', 'Hecton8.Audio.Virtualization'], 'atlasFamily': 'Audio', 'audioThreadLookup': 'forbidden', 'dataSovereignty': 'stateless raw binary lookup; no private runtime coefficient solver', 'snapshotCadence': 'block_or_control_update_only'} |
| `economy_million_step` | PASS | `Docs/Reports/Economy_MonteCarlo_Audit.json` | steps=1541057 million=True |
| `economy_no_failures` | PASS | `Docs/Reports/Economy_MonteCarlo_Audit.json` | status=ECONOMY PROVEN failures=0 |
| `economy_p99_threshold` | PASS | `Docs/Reports/Economy_MonteCarlo_Audit.json` | p99=59.285135135135164 threshold=60.0 |
| `fnv_collision_count` | PASS | `Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json` | collision_count=0 |
| `fnv_record_coverage` | PASS | `Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json` | records=1018 |
| `lore_binary_contract` | PASS | `Data/Lore/Encyclopedia.manifest.json` | alignment=16 endian=little |
| `lore_noir_signal_density` | PASS | `Data/Lore/Encyclopedia.manifest.json` | noir_signal_count=252 |
| `lore_stateless_lookup` | PASS | `Data/Lore/Encyclopedia.manifest.json` | {'data_sovereignty_static_score': 1.0, 'lookup_model': 'stateless binary search over sorted 16-byte records; payload is raw UTF-8 slice', 'private_runtime_state_required': False, 'unity_runtime_proof': 'PENDING VERIFICATION'} |
| `lore_scalability_profiles` | PASS | `Data/Lore/Encyclopedia.manifest.json` | profiles=['rtx_overkill', 'toaster'] |
| `tech_lore_manifest` | PASS | `Data/Lore/PdaTechnicalLogs.manifest.json` | magic=H8PT entries=100 collisions=0 |
| `verify_sweep_pass` | PASS | `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` | status=VERIFY_SWEEP_PASS requiredFailures=0 selfCheckPending=False |
| `verify_sweep_command_coverage` | PASS | `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` | totalCommands=35 results=35 |
| `verify_replay_hasher_reference` | PASS | `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` | returnCode=0 |
| `all_data_binaries_aligned16` | PASS | `Data` | files=43 unaligned=0 |
| `python_struct_endianness` | PASS | `Tools` | format_sites=274 failures=0 |

## Summary

- Total checks: 37
- Failed checks: 0
- Binary files scanned: 43
- Struct format sites scanned: 274
- Economy Monte Carlo steps: 1541057
- FNV collision count: 0
