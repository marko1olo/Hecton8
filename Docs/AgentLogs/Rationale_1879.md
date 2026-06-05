# Rationale 1879

Evidence class: STATIC_DOC / STATIC_SOURCE

## Decisions

1. One future Unity owner must own mesh import/refresh, prefab relink, validator execution, screenshot capture, profiler capture, and audit reruns. Splitting those steps across agents would create prefab and import-state races.
2. Mesh-source authoring, material/source inventory, static validator preparation, and report prep can run in parallel because they do not mutate target prefabs or Unity import state.
3. The 1879 contract covers the full 1867 product-face blocker set, including loose legacy roots `Item_Titanium.prefab`, `STRUCTURES.prefab`, and `Buildings/Cube.prefab`. They are not allowed to disappear from the proof contract because they are outside the main tool/resource/transport/player/sky/ocean buckets.
4. Sky/ocean source cleanup keeps the three Crest input-plane hidden-input route possible only with future Unity/Frame Debugger proof. Static YAML is not enough.
5. `GlobalQualityWeight` is documented as continuous presentation scaling only. It must not alter item ids, recipes, anchors, collider truth, save identity, transport presets, player movement truth, or sky/ocean authority routes.

## Rejected Shortcuts

- "Just replace prefabs" was rejected. Each category requires source mesh, materials, collider split, anchors, LOD/HLOD, validation, rollback, and proof.
- Darkness, fog, storm, silt, eclipse, and UI overlays were rejected as product-face primitive resolution.
- Static source reports were not upgraded to visual or runtime acceptance.
- 1878 sky/ocean validator now exists as `Hecton8/Validation/Sky-Ocean Source Primitive Gate`; Unity/menu execution remains pending.
