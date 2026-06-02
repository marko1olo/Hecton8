# Dependency Graph

Date: 2026-06-02
Status: GENERATED ARTIFACT STUB / GENERATED COUNTS STALE
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / GENERATED_SOURCE_INDEX

Purpose: stable root path for dependency-graph tooling. The root file is intentionally short; the graph body is generated evidence, not base doctrine.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/DEPENDENCY_GRAPH.md`.

## Tool Contract

Inputs and outputs:

- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.json`
- `Docs/Generated/DEPENDENCY_GRAPH.cache.json`
- `Tools/BuildArchitectureAtlas.py`
- `Tools/HectonPhiStaticAudit.py`
- `Tools/AtlasCheck.py`

Optional generated output:

- `PROJECT_ATLAS_HPHI.md` is produced only when `Tools/HectonPhiStaticAudit.py` completes.
- It is currently absent in this workspace and must not be cited as current proof.

Regenerate:

```powershell
python Tools/BuildArchitectureAtlas.py
```

Validate:

```powershell
python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md
```

## Current Static Snapshot

2026-05-28 regeneration:

- `python Tools/BuildArchitectureAtlas.py` rewrote `Docs/Generated/DEPENDENCY_GRAPH.md`, `.json`, and `.cache.json`.
- `python Tools/AtlasCheck.py` returned `ATLAS_CHECK_PASS references=5807`.
- Generated graph reported `220` asmdefs scanned and `167` first-party asmdefs under `Assets/_Project`; those generated counts are historical until the graph is regenerated.

Current source-backed static filesystem check:

- `rg --files Assets/_Project -g '*.asmdef'` reports `171` first-party asmdefs.
- Generated graph regeneration was not run in this pass; generated graph counts remain historical until `BuildArchitectureAtlas.py` is rerun.
- For current onboarding, use `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` before citing graph counts.

This is static reference integrity only. It does not prove compile, Unity import, Play Mode, profiler, GC, player build, save/load, platform, or visual correctness.

## Non-Claims

- This file is not compile proof.
- This file is not runtime proof.
- This file is not a live architecture diary.
- Do not paste generated graph bodies into root unless a tool contract explicitly requires it.
