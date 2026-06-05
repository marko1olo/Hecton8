# Rationale 1856 - Organic Misc Final Mesh Rebuild Packet

Evidence class: STATIC_SOURCE
Date: 2026-06-04

## Decisions

1. Keep both OrganicMisc final variants blocked.
   Static source and prefab text prove visible Unity primitive mesh references in both final prefabs. Relinking the existing assets would preserve the defect.

2. Do not use `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals` as a recovery path.
   The source creates visible `PrimitiveType.Cylinder`, `PrimitiveType.Sphere`, and `PrimitiveType.Capsule` children through `GameObject.CreatePrimitive`. The existing quality gate must remain in place.

3. Treat baked flora and BioForge assets as source/inspiration, not drop-in replacements.
   Baked flora currently has zero static built-in primitive refs, but prior Batch18 evidence records missing manifest/named visual proof. BioForge contains source mesh assets with no manifest files found under the folder. Neither set proves exact egg-cluster or giant-plant compatibility.

4. Use the amended source files as implementation patterns only.
   `WorldProceduralSeaweedMeshBuilder.cs` is relevant to the giant plant holdfast/stalk/frond route. `WorldProceduralCoralMeshBuilder.cs` is relevant to substrate, cavities, porous pads, membrane supports, and coral-adjacent forms. `WorldProceduralFloraBakedStarterGenerator.cs` is a useful three-LOD starter pipeline pattern. None is proof of an OrganicMisc final until OrganicMisc-specific manifests, textures, renders, validators, and family integration checks exist.

5. Keep gameplay truth separate from visible art.
   Egg spawn/scanner/nest semantics and giant-plant interaction anchors should be metadata-only or hidden child anchors. Visible art must be non-primitive organic mesh. Simple primitive colliders are allowed only as invisible `COL_*` proxies when they do not carry visible mesh renderers.

6. Scale with continuous quality.
   `GlobalQualityWeight` may change mesh density, material feature cadence, LOD residency, membrane/frond density, emissive accent density, and proof capture breadth. It must not change family identity, gameplay anchors, save identity, or truth ownership.
