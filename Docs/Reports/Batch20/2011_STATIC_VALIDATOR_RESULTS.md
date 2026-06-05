# 2011 Static Validator Results

Evidence boundary: static only. No Unity, no builds, no Assets edits, no runtime proof.

## Scripts Run

1. `ProductFaceStaticRouteAudit.py`
   - Exit: 0.
   - Output: `ERROR: 0`, `WARNING: 0`, `INFO: 0`, `No findings.`
   - Result: `STATIC VERIFIED` for this route-contract tool only. It does not clear generated asset primitive mesh or material assignment debt.

2. `MaterialAudit.py`
   - Exit: 0.
   - Root: `Assets/_Project`; resolve root: `Assets`; sample size: `128`.
   - Textures: 139.
   - Materials: 356.
   - Energy failures: 0.
   - Energy warnings: 0.
   - Texture read errors: 0.
   - Import issue textures: 5.
   - Estimated texture MiB: 504.231 / 900.0, status `PASS`.
   - Materials with issues: 65.
   - Materials with unresolved texture refs: 21.
   - Unresolved texture refs: 50.
   - Surface materials with unresolved texture refs: 14.
   - Surface unresolved texture refs: 31.
   - Surface unresolved blocker materials: 14.
   - Surface migration queue rows: 58, priorities `BLOCKER=14`, `MEDIUM=12`, `LOW=32`.
   - Channel packing candidates: 58.
   - Candidate saved MiB estimate: 212.28.
   - Result: `STATIC REJECTED` for unresolved refs, surface blockers, channel packing, and material issue debt. No visual proof claim.

3. `VerifyVisualLodMatrix.py`
   - Exit: 0.
   - Output: `VERIFY_VISUAL_LOD_MATRIX_OK`.
   - Binary: `Data/System/Visual_Scalability_Matrix.bin`.
   - Manifest: `Data/System/Visual_Scalability_Matrix.manifest.json`.
   - Bytes: 2048.
   - Endianness: little.
   - Aligned16: true.
   - Hash collisions: 0.
   - Tiers: 4.
   - Extra records: 4.
   - God mode density ratio vs Pro: 9.097.
   - Result: `STATIC VERIFIED` for binary/manifest integrity only.

4. `VisualStressSim.py`
   - Exit: 0.
   - Evidence boundary: `PYTHON_OFFLINE_NOT_RUNTIME_PROOF`.
   - Status: `PASS`.
   - TOASTER: 1560.0 MiB, density 14.800, gpuCyclesP95 106.017.
   - DECK: 2600.0 MiB, density 69.600, gpuCyclesP95 124.848.
   - PRO: 5432.0 MiB, density 338.400, gpuCyclesP95 658.827.
   - GOD_MODE: 11418.0 MiB, density 3078.400, gpuCyclesP95 4841.835.
   - God mode / Pro density ratio: 9.097.
   - Result: `STATIC VERIFIED` offline estimate only. Not profiler proof.

## Skipped Scripts

1. `GeneratedAssetProductionAudit.py`
   - Skipped because default run writes `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.json` and `.md`.
   - Existing `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md` was inspected.

2. `TextureAuditAndBakeDirector_SHINOBU_361.py`
   - Skipped because it writes output directories and JSON/CSV/markdown/manifest/prompt/queue artifacts.

3. `Crest_Quarantine_Polish_Audit.py`
   - Skipped because it hardcodes write to `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json`.
   - Existing report was inspected.

4. `VisualLodMatrixBaker.py`
   - Skipped because it writes `Data/System/Visual_Scalability_Matrix.bin` and manifest.

5. `PolishMandateStaticAudit.py`
   - Skipped because it writes JSON/markdown report files.

6. `ArchitectureRiskHotlistAudit.py`
   - Skipped because it writes JSON/markdown report files.

## Existing Static Outputs Aggregated

- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
  - Packages scanned: 434.
  - Fatal issues: 0.
  - Error issues: 83.
  - Warning issues: 1281.
  - Product-face prefab built-in primitive mesh issues: 42.
  - Final prefab built-in primitive mesh issues: 21.
  - Missing manifests: 338.
  - Missing named proof: 338.
  - Surface/shallow visual proof pending: 338.

- `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json`
  - Status: `FAIL`.
  - Static failure visible in inspected head: `easy_save_defaults_no_crest_assemblies`.
  - Many quarantine checks are `PASS`, including package outside Unity visibility, bridge folder ownership, adapter reference removed, and vendor-neutral bridge checks.
  - Result: partial static boundary evidence only; vendor boundary not closed.

- `Docs/Reports/Batch19/1909_STATIC_VISUAL_DEBT_AUDIT_PROOF_MATRIX.csv`
  - Confirms prior static debt rows for surface/shallow, terrain/coastline, sky/ocean/Aegir/moon, flora/coral/kelp, product-face, PBR channels, and proof artifacts.
  - Used as historical static evidence, not runtime proof.

## Placement Rule Static Evidence

- `rg --files Assets/_Project/Data/World/ProceduralPlacementRules`: 74 files.
- Kelp/coral/rock matching placement rule files: 30.
- `ProceduralRule_rule_kelp_starter.asset`: `minDepthMeters=0`, `maxDepthMeters=180`, `maxSlopeDegrees=18`, `requiredHeatmapChannel=kelp_density`.
- `ProceduralRule_rule_coral_reef.asset`: `minDepthMeters=0`, `maxDepthMeters=600`, `maxSlopeDegrees=40`, `requiredHeatmapChannel=coral_density`.
- `ProceduralRule_rule_rocks_shelf.asset`: `minDepthMeters=20`, `maxDepthMeters=5000`, `minSlopeDegrees=8`, `maxSlopeDegrees=58`, preferred biome references present.
- Result: placement rule contracts exist, but dry-land/wrong-depth placement is still `PENDING UNITY/WORLD PLACEMENT PROOF`.

## Proof Matrix Summary

- ProductFace route-contract tool is clean, but historical generated asset audit still rejects product-face primitive meshes and default/package material debt.
- Material/texture channel debt remains open: unresolved refs, surface blockers, missing detail maps, and channel packing candidates.
- Generated asset production readiness remains open: manifests, named proof, and surface/shallow visual proof are missing at scale.
- Crest quarantine is not fully closed because the existing static report is `FAIL`.
- Visual LOD matrix static integrity passes, and offline stress passes, but runtime LOD/profiler/capture proof is absent.
- Polish and architecture risk audits require a future allowed-output slot or stdout-only tool changes.
- Placement rule files define depth/slope contracts for kelp/coral/rocks, but scene placement correctness is unproved.

## No-Proof Claims Rejected

- No in-game proof.
- No Unity import proof.
- No material render proof.
- No current screenshot proof.
- No profiler or GC proof.
- No frame-time proof.
- No active scene placement proof.
- No claim that static ProductFace zero findings clears visual debt.
