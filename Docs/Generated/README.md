# Generated Documentation Artifacts

Date: 2026-05-26
Status: GENERATED ARTIFACT BOUNDARY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / GENERATED_OUTPUT

This folder stores machine-generated documentation artifacts that are too large or too volatile for `Docs/` root.

Generated output paths:

- `DEPENDENCY_GRAPH.md`
- `DEPENDENCY_GRAPH.json`
- `DEPENDENCY_GRAPH.cache.json`
- `PROJECT_ATLAS_HPHI.md`

Root `Docs/DEPENDENCY_GRAPH.md` is only the stable tool-entry contract. Generated graph bodies must stay here.

Checked-in generated files may be placeholders or stale snapshots unless regenerated in the current run. Treat them as `STATIC_DOC` evidence only until `Tools/BuildArchitectureAtlas.py`, `Tools/HectonPhiStaticAudit.py`, or the relevant validator rewrites them and `Tools/AtlasCheck.py` accepts the generated markdown.

Current dependency graph state on 2026-05-26: `Tools/BuildArchitectureAtlas.py` regenerated `DEPENDENCY_GRAPH.md/json/cache.json`; `Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS`. This is static reference integrity only, not Unity/runtime proof.
