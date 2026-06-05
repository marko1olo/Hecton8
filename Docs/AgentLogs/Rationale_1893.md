# Rationale 1893

Scope decision:
- Static YAML and `.mat.meta` evidence only.
- No Unity APIs or AssetDatabase.
- No prefab/material/source/asset edits.

Mandates selected:
- QA_Evidence_Text_Filter_Audit: report claims must stay at STATIC_SOURCE / STATIC_DOC unless runtime artifacts exist.
- REND_URP_Graphics_HotPath_Optimization_HLOD: material/source routes affect visual quality and scalability; compact mode cannot hide default/package/placeholder debt.

Classification decisions:
- Resolved GUID does not mean accepted material route.
- Package-cache `Lit.mat`, tool placeholders, `MAT_PlayerSwimBlockout`, flat resource shells, and third-party prototype checker materials are blocked.
- Sargassum input materials are hidden-input candidates only in `Ocean_Crest.prefab`; they require future runtime/Frame Debugger proof.
- `Mat_Visor_Glass` and `MAT_Diegetic_HUD_V4_Projection` are non-body product-face routes needing channel/Unity proof, not broad body material donors.
- No unresolved GUID rows were found in the current static scan.
