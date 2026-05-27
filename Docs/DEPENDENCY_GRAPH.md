# Dependency Graph

Date: 2026-05-26
Status: GENERATED ARTIFACT STUB
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / GENERATED_SOURCE_INDEX

Purpose: stable root path for dependency-graph tooling. The root file is intentionally short; the graph body is generated evidence, not base doctrine.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/DEPENDENCY_GRAPH.md`.

## Tool Contract

Inputs and outputs:

- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.json`
- `Docs/Generated/DEPENDENCY_GRAPH.cache.json`
- `Docs/Generated/PROJECT_ATLAS_HPHI.md`
- `Tools/BuildArchitectureAtlas.py`
- `Tools/HectonPhiStaticAudit.py`
- `Tools/AtlasCheck.py`

Regenerate:

```powershell
python Tools/BuildArchitectureAtlas.py
```

Validate:

```powershell
python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md
```

## Non-Claims

- This file is not compile proof.
- This file is not runtime proof.
- This file is not a live architecture diary.
- Do not paste generated graph bodies into root unless a tool contract explicitly requires it.
