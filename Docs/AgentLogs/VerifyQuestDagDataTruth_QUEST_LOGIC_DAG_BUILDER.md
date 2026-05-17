# QUEST_LOGIC_DAG_BUILDER Data Truth Audit

Status: QUEST_DAG_DATA_TRUTH_VERIFIED
Checks: 10
Failed: 0

| Check | Status | Detail |
|---|---:|---|
| `graph_contract` | PASS | nodes=4 max=32 bitsPerQuest=2 |
| `hashes_and_lore` | PASS | graphHashes=8 nodeAndTriggerHashes=8 loreEntries=2 |
| `lore_tone` | PASS | diegeticLabels=dirty_noir placeholderTokens=0 sterileTokens=0 |
| `binary_layout` | PASS | bytes=496 sha256=01D823243E1315F2C7263806C59967173B9315F6E4052A5726C6AEF27FC2E156 tierOffset=304 |
| `scalability_tiers` | PASS | Low/Middle stripped; High/Ultra gradient+harmonic overkill data present |
| `generated_constants` | PASS | constants=123 staticClasses=9 runtimeLogic=0 |
| `project_atlas_fit` | PASS | declaredDomains=4,73,75,78 atlasDomainMap=85 |
| `struct_endianness` | PASS | structFormats=15 endian=< |
| `h_phi_reports` | PASS | hPhiChecks=37 sweepCommands=35 |
| `stale_evidence` | PASS | staleEvidenceMarkers=0 |
