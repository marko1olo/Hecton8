# Rationale 1873

Evidence ceiling: STATIC_SOURCE / STATIC_DOC.

## Decisions

1. `Sky_System.prefab` source cleanup must remove or quarantine the enabled built-in primitive sphere. Reason: scene override evidence in `02_HECTON_WORLD.unity` does not protect future scenes, prefab reverts, addressable instantiation, or validator gates.

2. `Ocean_Crest.prefab` source cleanup must make the three Crest input planes hidden-input-only at source or replace them with explicit non-rendering input carriers. Reason: `_disableRenderer: 1` plus scene renderer-disable overrides are useful but still static text; source MeshRenderers remain enabled in the prefab.

3. `SargassumMicroFaunaBoids.boidMesh` cannot stay a built-in plane unless future captures and Frame Debugger proof show it is never player-visible as a primitive card. Preferred route is an authored/generator-produced micro-fauna/VAT/impostor mesh with material and motion proof.

4. Runtime acceptance must be assigned to one future Unity owner, not split across sky, ocean, and micro-fauna claims. Reason: surface proof requires the same route frame to satisfy graphics, optimization, and gameplay readability.

5. Static YAML is not acceptance. All sky/ocean acceptance language remains `PENDING UNITY SLOT` until captures, Frame Debugger, profiler, GC, and tier comparison artifacts exist.

## Low / Middle / High / Ultra Consequences

- Low/Compact: bright readable ocean, Aegir/sky/moon silhouette, waterline identity, and route cues must survive with lower cost. Primitive leaks or ugly fallback fail.
- Middle: adds richer shoreline foam, photic clarity, cloud/water response, and route dressing without changing truth.
- High: spends cost on reflections, cloud depth, Aegir/moon atmosphere, richer foam/refraction, and controlled fauna density.
- Ultra: visual overkill only. It may add sensory richness, not gameplay truth, save identity, DTO layout, or route ownership.
