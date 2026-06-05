# Rationale 1808 - AEGIR_SKY_ACTIVE_PATH_AUDITOR

## Decisions

1. Evidence boundary: this pass uses static YAML/source/material/import metadata only. It will not claim runtime activity, visual acceptance, profiler cost, Frame Debugger state, or player capture quality.
2. Scope boundary: sky/Aegir/moon/cloud/celestial scene bindings, materials, shaders, and direct texture metadata only. No broad archive sweep and no Unity/runtime takeover.
3. Surface brightness lock: disabled/noir scene objects and dark backdrop names cannot be used as surface acceptance or as cover for weak sky/water art. They will be classified as candidate/stale/inactive unless active YAML evidence proves otherwise.
4. Active-route selection: static owner evidence points to `Mat_HectonSky` plus `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` as the likely active route. Disabled `H8_AEGIR_SKY_BACKDROP_1428`, inactive baked-disc cards, and inactive noir sky objects remain candidate/stale only.
5. Edit boundary: no material or shader edits were made. Missing texture GUIDs, unresolved common shader GUID `650dd9526735d5b46b79224bc6e94025`, flat moon cards, and quality-scalar gaps are reported as risks for Unity-slot proof instead of patched blindly.
