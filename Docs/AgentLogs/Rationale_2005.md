# Rationale 2005

Date: 2026-06-04  
Scope: static source package only.

1. Shoreline rocks were split from underwater rocks because the dry-land risk audit proves `depth == 0` is ambiguous. Any underwater family using raw depth 0 can leak onto dry terrain.
2. Existing `PFB_Geo_*` prefabs are treated as candidate sources, not proof. Static presence of prefabs, LODGroups, or manifests does not satisfy visual/material/collider acceptance.
3. `WorldProceduralProxy` assets are rejected for product routes because sampled proxy prefabs reference Unity built-in primitive meshes and procedural families still allow proxy fallback.
4. GeologyForge is the primary route because it already owns deterministic CSV profiles, LOD output, manifest output, layout self-audit, and a 300-frame bake telemetry buffer. RockSculptor is reserved for hero erosion forms and is rejected if it falls back to `Default-Material`.
5. Wet basalt is blocked at material level. `MAT_H8SurfaceWetBasaltReal_1428` and `TX_H8_WetBasaltShoreline_Albedo_1428.png` are candidates only; the package requires normal, packed MRAO/wetness, detail, waterline mask, and foam/salt residue masks.
6. Packed texture channel order is explicitly not guessed because the texture bible and URP hot-path mandate carry different MRAO ordering language. Future executor must lock the actual shader contract before authoring.
7. Texture arrays/shared materials are the default path. SVT, GPU Resident Drawer, and HLOD are allowed only after platform/editor proof, not static claims.
8. Quality scaling uses continuous `GlobalQualityWeight` across resolution, variants, density, AO/detail, and distance behavior. Gameplay truth, route ownership, collider semantics, and placement authority cannot change by quality lane.

