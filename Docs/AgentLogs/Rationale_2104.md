# Rationale_2104

1. Existing tools were not sufficient for this exact task. `Tools/MaterialAudit.py` covers material/texture source debt, and existing ProductFace/generated-asset audits cover other source contracts, but there was no single read-only validator joining active-scene primitive mesh refs, renderer null material slots, default/package/proxy material refs, unresolved material GUIDs, and unresolved texture GUIDs into one Batch21 handoff.

2. I added a narrow static utility instead of modifying assets or runtime systems. The task explicitly permitted a Docs/Tools-only static utility/report and forbade Unity, imports, builds, scenes, prefabs, materials, and scripts.

3. Every finding is labeled `STATIC_SOURCE` with `PENDING VERIFICATION`. Static text proves only that a YAML/material reference exists in source; it does not prove runtime binding, visual quality, importer state, prefab override application, or frame cost.

4. Severity is route-aware. Active scene debt and surface/sky/ocean/photic/medium/product-face route debt are escalated because those domains are under the project visual floor. Diagnostic/editor/test candidates are downgraded.

5. Output writes are constrained to `Docs`. The utility refuses forbidden Unity roots so it cannot be used as an accidental asset mutator.

6. Low/Middle/High/Ultra consequence handling is documented in the Markdown report. The validator does not choose replacement art; it routes debt so later owner passes can remove primitive/null/default/proxy contamination without weakening the visual floor.
