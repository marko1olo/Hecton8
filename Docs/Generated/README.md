# Generated Documentation Artifacts

Date: 2026-06-08
Status: GENERATED ARTIFACT BOUNDARY
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / GENERATED_OUTPUT

This folder stores machine-generated documentation artifacts that are too large or too volatile for `Docs/` root. These files are static/generated outputs only. They are not architecture doctrine, compile proof, Unity import proof, runtime proof, profiler proof, player-build proof, or platform proof.

Generated output paths:

- `DEPENDENCY_GRAPH.md`
- `DEPENDENCY_GRAPH.json`
- `DEPENDENCY_GRAPH.cache.json`

Optional generated output path:

- Expected output path `Docs/Generated/PROJECT_ATLAS_HPHI.md` is produced by `Tools/HectonPhiStaticAudit.py` only after that scanner completes. It is currently absent in this workspace.

Root `Docs/DEPENDENCY_GRAPH.md` is only the stable tool-entry contract. Generated graph bodies must stay here.

Checked-in generated files may be placeholders or stale snapshots unless regenerated in the current run. Treat them as `STATIC_DOC` evidence only until `Tools/BuildArchitectureAtlas.py`, `Tools/HectonPhiStaticAudit.py`, or the relevant validator rewrites them and `Tools/AtlasCheck.py` accepts the generated markdown.

Current dependency graph state on 2026-06-08: `Tools/BuildArchitectureAtlas.py` regenerated `DEPENDENCY_GRAPH.md/json/cache.json`; `Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5601`. This is static reference integrity only, not architecture doctrine, compile proof, Unity import proof, runtime proof, profiler proof, player-build proof, or platform proof.

Freshness boundary on 2026-06-08: the checked-in dependency graph reflects the current `Tools/BuildArchitectureAtlas.py` static scan and passes `Tools/AtlasCheck.py`. Cite it only as `STATIC_SOURCE / STATIC_DOC / GENERATED_OUTPUT`; it is not Unity import, runtime, profiler, player-build, or platform proof.

Current H-Phi atlas state on 2026-05-28: `Tools/HectonPhiStaticAudit.py --no-fail` timed out after 300 seconds in this workspace before producing `Docs/Generated/PROJECT_ATLAS_HPHI.md`. Do not cite that missing artifact as current proof.
