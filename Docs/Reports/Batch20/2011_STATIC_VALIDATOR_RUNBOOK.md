# 2011 Static Validator Runbook For Visual Debt

Evidence boundary: `STATIC VERIFIED` only. This runbook forbids Unity, Unity MCP, dotnet build, player build, destructive cleanup, and intentional `Assets/**` writes.

## Authority Loaded

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`
- `quality.md`
- `performance.md`
- `presentation.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `Docs/Tasks/POLISH.txt`
- `taskslocal/batch19_art_source_and_static_proof/1909_STATIC_VISUAL_DEBT_AUDIT_PROOF_MATRIX.txt`

## Static Proof Rules

1. Static scans can prove file existence, text contracts, YAML fields, script side effects, historical report counts, and offline binary/JSON consistency.
2. Static scans cannot prove Unity import state, active scene binding, material render result, screenshot quality, profiler cost, frame-time, GC, placement in the live world, or in-game visual quality.
3. Any historical screenshot/report remains old evidence unless a current Unity owner capture exists.
4. Continuous quality scaling is mandatory. Static matrix rows must describe Compact, Middle, High, and Ultra consequence without changing gameplay truth.
5. Missing evidence is `PENDING` or `OPEN`, never complete.

## Safe Execution Gate

A tool is executable in this 2011 lane only when inspection proves one of these:

- stdout-only behavior with no output path writes;
- read-only file access;
- optional report writing disabled by default.

A tool is skipped when:

- it writes a default report, JSON, markdown, CSV, binary, manifest, or queue file;
- it hardcodes a shared output path;
- it mutates `Assets/**`, `ProjectSettings/**`, `Packages/**`, Unity data, or generated binaries;
- side effects are ambiguous.

## Executable Validators

### ProductFace Static Route Audit

Command:

```powershell
python Tools\ProductFaceStaticRouteAudit.py --root .
```

Purpose: product-face static route contract check.

Accepted proof: counts and findings printed to stdout.

Rejected claim: this cannot clear generated asset primitive mesh debt, material assignment debt, Unity material binding, or product-face screenshots.

### Material Audit

Command:

```powershell
python Tools\MaterialAudit.py --root Assets/_Project --resolve-root Assets --sample-size 128
```

Purpose: first-party material/texture channel, unresolved ref, import, budget, and packing scan.

Accepted proof: stdout summary only. No `--json`, `--markdown`, or `--csv-prefix` in this lane.

Rejected claim: this cannot prove Unity import/render output or visual quality.

### Visual LOD Matrix Verify

Command:

```powershell
python Tools\VerifyVisualLodMatrix.py
```

Purpose: verify static visual scalability binary/manifest consistency.

Accepted proof: binary alignment, bytes, hash collision, tier, and extra-record counts.

Rejected claim: this cannot prove runtime LOD behavior, GPU cost, or frame-time.

### Visual Stress Sim

Command:

```powershell
python Tools\VisualStressSim.py --frames 720 --seed 8808
```

Purpose: deterministic offline visual scalability stress estimate.

Accepted proof: offline tier estimates and self-audit status.

Rejected claim: this is explicitly `PYTHON_OFFLINE_NOT_RUNTIME_PROOF`, not profiler or in-game proof.

### Targeted `rg` Scans

Commands:

```powershell
rg --files Tools
rg --files Assets/_Project
rg --files Assets/_Project/Data/World/ProceduralPlacementRules
rg --files Assets/_Project | rg -i "(ProceduralRule_|PlacementRule|Biome|Kelp|Coral|Rock|TerrainLod|WorldRuntime|ProceduralPlaceholders)"
rg -n --glob '!*.meta' "(Placeholder|Default-Material|m_Name: MAT_family_|m_Name:.*Placeholder|m_Materials:|guid: 0000000000000000|fileID: 10303|fileID: 2100000)" Assets/_Project/Materials Assets/_Project/Prefabs Assets/_Project/Data
```

Purpose: path inventory, placement-rule discovery, placeholder/default material risk discovery.

Accepted proof: static path/text matches.

Rejected claim: static matches alone cannot classify active renderer visibility or scene placement.

## Skipped Validators

- `Tools/GeneratedAssetProductionAudit.py`: writes default Batch18 JSON/markdown. Use existing `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md` unless output paths are explicitly allowed.
- `Tools/TextureAuditAndBakeDirector_SHINOBU_361.py`: writes audit, manifest, prompt, queue, JSON, markdown, and CSV outputs. No read-only lane found.
- `Tools/Crest_Quarantine_Polish_Audit.py`: hardcoded write to `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json`. Existing report inspected.
- `Tools/VisualLodMatrixBaker.py`: writes `Data/System/Visual_Scalability_Matrix.bin` and manifest. Use verifier and stress sim for static proof.
- `Tools/PolishMandateStaticAudit.py`: writes report files. Future allowed-output task needed.
- `Tools/ArchitectureRiskHotlistAudit.py`: writes report files. Future allowed-output task needed.

## Coverage Map

- ProductFace primitive/default/package material risk: ProductFace route audit plus Batch18 generated asset audit plus Batch19 1909 matrix.
- Material/texture channel contracts: MaterialAudit stdout plus 3DMODEL texture/material authority.
- Generated asset production route risk: existing Batch18 generated asset production audit.
- Crest/ocean vendor boundary: existing Crest quarantine report only; no rerun because writer tool.
- Visual LOD/scalability: VerifyVisualLodMatrix and VisualStressSim.
- Polish mandates: authority read; writer audit skipped.
- Hot-path risks: authority read; ArchitectureRiskHotlistAudit skipped because writer-only in this lane.
- Placement rule risk for kelp/rocks/coral dry-land/wrong depth: selected YAML reads and path counts. Static contracts exist, scene proof pending.

## Required Rejection Language

- Static manifest is not visual proof.
- Source image or generated texture candidate is not Unity material proof.
- Channel name is not shader contract.
- Old screenshot is not current pass proof.
- Compact does not mean cheap or muddy.
- Source preparation is not import, binding, placement, profiler, or player proof.
