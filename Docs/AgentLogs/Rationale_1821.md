# Rationale 1821

Evidence class: STATIC_DOC / STATIC_SOURCE only.

1. The shoreline/waterline fix is specified as offline baked masks and existing shader/material input routing, not a new runtime simulation. This follows the cinematic-cheat mandate and avoids blind Crest realtime depth/foam camera enablement.

2. Third-party package assets under `Assets/Crest`, MapMagic, GPUInstancer, MeshBaker, and SciFiFacility are treated as read-only source/reference pools. First-party wrappers, first-party materials, and generated project outputs are the only acceptable future mutation targets.

3. `MAT_H8_SurfaceFoamRibbons_1428` is not accepted as production-ready because static YAML shows empty `_BaseMap` and `_MainTex`. `MAT_H8SurfaceShoreFoam_1428` is the safer current foam material candidate because it is wired to a first-party foam texture and the first-party shoreline shader.

4. The scene contains foam ribbon and shoreline foam objects, but static YAML marks them inactive. The report therefore routes activation, material binding, screenshot proof, profiler proof, and Frame Debugger proof to a later Unity slot instead of upgrading static existence to runtime proof.

5. `MAT_SurfaceIslandWetBasalt_1428` is weaker than `MAT_H8SurfaceWetBasaltReal_1428` because it lacks a normal map in static material YAML. It stays as a color/composition candidate, not the primary wet basalt proof path.

6. Existing tools are used first where they fit: `CausticOpticsBaker1719` for caustic flipbook/light cookie/waterline mask, `BiomeSplatmapForgeWindow` for terrain control channels, `ShorelineFoamGraft` for runtime-facing foam DTO/profile validation. A dedicated packed shoreline contact mask generator is absent by static scan, so the spec defines that missing offline tool requirement explicitly.
