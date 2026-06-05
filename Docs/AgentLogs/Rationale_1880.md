# Rationale 1880

Evidence class: STATIC_SOURCE / STATIC_DOC.

Decisions:

- Marked all body material texture roles as `MISSING_SOURCE_REQUIRED` unless a credible project-owned production material/texture source existed.
- Treated `Mat_Tool_*_Placeholder`, package `Lit.mat`, `Mat_ToolTrial_*`, diagnostic/error, runtime flat-color/checkerboard, and scanner marker materials as forbidden for product-face body relink.
- Used shader-local packed mask semantics for `Hecton_ToolDecayLit`: R Metallic, G AO, B Smoothness, A EmissionMask.
- Did not promote runtime visual proof materials to tool production candidates because static scan does not prove tool role assignment, import settings, or screenshots.

No source, prefab, material asset, texture, scene, `.meta`, binary, generated mesh, Unity, dotnet, import, bake, PlayMode, profiler, or Data Monolith action was run.
