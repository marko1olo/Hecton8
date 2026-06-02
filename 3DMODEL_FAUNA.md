# 3DMODEL_FAUNA

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: generated creatures, shells, jaws, fins, tails, tentacles, skeletons, carcasses, VAT-ready bodies, and fauna equipment attachments.

## 1. Fauna Mesh Law

Fauna geometry must support animation, readability, and fear. A generated creature made from scaled capsules or bone sticks is rejected unless it is a hidden rig proxy. The saved visual mesh must include a coherent body plan, silhouette hierarchy, asymmetry, pressure-adapted anatomy, scars, tissue thickness, hard/soft material separation, and readable attack/contact zones.

Runtime movement is not permission for runtime mesh generation. Fauna bodies, skinned meshes, blendshape/VAT frames, normals, tangents, UVs, masks, LODs, and hit proxies are authored or baked offline.

## 2. Body Plan Requirements

Every fauna generator must declare:

- Primary locomotion type: swimmer, crawler, ambush, drifting, burrowing, tentacled, armored.
- Skeletal or VAT deformation route.
- Material zones: shell, flesh, membrane, eye/lens, teeth/claws, bioluminescent organs.
- Gameplay contact zones: head, jaw, tail, claw, weak spot, armor plate.
- Collision/hitbox proxy set.
- LOD and animation fallback route.

Generated bodies must include silhouette contrast: thick mass, thin appendage, fin/membrane, mouth/jaw or sensing organ, and material breaks. A uniformly smooth tube is rejected.

## 3. Topology For Deformation

Skinned and VAT-ready meshes need deformation-safe topology:

- Edge loops around shoulders, hips, jaw hinges, fin bases, tentacle bases, and tail roots.
- Even longitudinal segments along bendable parts.
- No long thin triangles crossing joints.
- No poles directly on high-bend creases.
- Symmetry may be used for base construction, but final mesh must add deterministic asymmetry.
- Mouths, gills, eyes, vents, and bite zones require separate loops or material borders.

Tentacles and tails use ring sections with consistent vertex order for VAT/bend shader compatibility. Ring count scales by asset class and LOD; LOD2 may collapse to a coarse tube or card, but attachment points remain stable.

## 4. Vertex Color And UV Contract

Default fauna vertex color semantics:

- R = deformation amplitude or secondary motion weight. Rigid shell = low. Fins/tentacles = high.
- G = bioluminescence mask/phase.
- B = baked AO/cavity/underside darkness.
- A = damage reveal, armor hardness, wetness, mucus, or shader blend mask.

UVs must support unique head/jaw/eye/weak spot detail. Mirroring is allowed for hidden bilateral body areas but forbidden on named weak spots, eyes, jaws, or readable scars unless the design explicitly wants symmetry.

## 5. Material Rules

Minimum material zones:

- Flesh/tissue.
- Hard shell/scale/bone/teeth/claw when present.
- Eye/lens/wet organ when present.
- Bioluminescent organ when present.

Use material slots or packed masks. Do not create one material per small body part. Instancing and SRP Batcher compatibility matter more than arbitrary material variety.

## 6. LOD And Animation

LOD0:

- Complete silhouette and deformation loops.
- Full material zone separation.
- High quality normals/tangents.

LOD1:

- Preserve attack silhouette, head, jaws, fins, tentacles, weak spots.
- Reduce inner mouth detail, small scales, scars, and minor appendages into normals/masks.

LOD2:

- Preserve threat readability and hit proxy alignment.
- Collapse tiny appendages and surface detail.
- Use VAT/impostor route if animation cost or skinning cost is too high.

Fauna LODs must maintain hitbox alignment. A creature cannot visually shrink away from its attack/hit truth.

## 7. Collision And Hitboxes

Fauna collision uses primitives:

- Capsule for spine/body mass.
- Spheres for head, joints, organs, and weak points.
- Capsules for fins, arms, tentacles, and tails.
- Boxes for armored plates or broad shell regions.

LOD0 render mesh is never used as MeshCollider. Bite, lunge, and damage routing must reference hitbox primitives or authored sockets, not visual triangles.

## 8. Rejection Gates

Reject if:

- Bendable zones lack edge loops or have long triangles crossing deformation axes.
- Vertex color masks are decorative instead of semantic.
- Hitboxes do not align with visible attack/weak zones.
- Material slots exceed need and break instancing without proof.
- LOD2 destroys threat silhouette or weak spot readability.
- Any runtime path generates creature geometry during gameplay.
