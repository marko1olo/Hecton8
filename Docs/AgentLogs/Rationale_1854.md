# Rationale 1854 - World Support Visible Carrier Replacement Packet

Evidence class: STATIC_SOURCE
Date: 2026-06-04

## Decisions

1. Hidden support truth is separated from visible carrier art.
   - Reason: the family assets define spawn, zone, pocket, spacing, clustering, and heatmap truth. The current failure is that those supports are represented by built-in primitive visible meshes. Replacement must keep marker logic hidden and replace only presentation with premium carrier art.

2. No existing candidate folder was promoted to drop-in production replacement.
   - Reason: prior report 1851 records missing manifest/proof and shallow visual proof gaps for many baked flora assets. Static inventory confirms useful candidates but not runtime, import, material, collider, LOD, or screenshot proof.

3. WorldProceduralProxy, WorldRuntime/ProceduralPlaceholders, current WorldSupport/Final primitives, and AI proxy objects remain invalid replacement candidates.
   - Reason: task 1854 explicitly forbids proxy shortcuts and AI proxy objects. Prior reports also reject primitive finals and placeholder-style assets.

4. The future route should be an editor/offline support-carrier authoring pass, not runtime procedural generation.
   - Reason: the project bans runtime mesh/texture generation for gameplay. The amended coral and seaweed builder sources are editor builders and can inform offline generated mesh families, manifests, LODs, material sets, and proof artifacts.

5. Hazard vent replacement must reject `bubble vent atlas - bad - redo.png`.
   - Reason: prior reports identify that atlas as invalid. Hazard carriers need a dedicated material/texture/VFX proof path, not a known-bad texture reused as evidence.

6. Primitive or convex `COL_*` objects may be used only as invisible colliders or support volumes.
   - Reason: root art rules reject visible built-in primitives, while collider guidance permits simple proxy colliders when hidden and separated from visual LOD0.

7. GlobalQualityWeight consequences are continuous.
   - Compact: fewer ornaments, simpler shader features, lower VFX density, same readable carrier silhouette.
   - Middle: full LOD1/LOD2 visual identity with conservative route cues.
   - High: LOD0 forms, richer materials, stronger biolum/current/debris cues.
   - Ultra: extra micro-detail, decals, particles, wetness, and local variation without changing gameplay truth ownership.

