# 3DMODEL_FAUNA

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC / AUTHORING_STANDARD
Scope: generated creatures, shells, jaws, fins, tails, tentacles, skeletons, carcasses, VAT-ready bodies, and fauna equipment attachments.

## First-20 Route Hook

- First-20 moment: first readable threat silhouette, distant creature pass, carcass/evidence object, shell/organ scan, or shallow fauna encounter that teaches avoidance, risk, and route pressure.
- Route blocker removed: prevents first-route creatures from becoming scaled capsules, decorative fish, unreadable hitboxes, or runtime animation masking weak anatomy.
- Proof class: STATIC_DOC until body-plan declaration, deformation/topology proof, hitbox alignment, LOD/material proof, animation fallback note, compact capture, and route screenshot/clip exist.

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

## 6. GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale LOD0 segment density, shell plate count, membrane subdivision, secondary appendage count, material mask richness, VAT frame density, near-field wetness/detail maps, and optional diagnostic captures. It must not change hitbox truth, attack reach, weak spot identity, locomotion authority, save identity, or creature AI decisions.

Compact fauna must keep the body plan, attack silhouette, weak spots, deformation masks, and collision proxies readable. Middle may add better deformation loops and material separation. High may add richer appendages, scars, shell overlaps, and VAT smoothness. Ultra may add hero-only silhouette breaks and bake detail, but never uses runtime mesh generation or visual triangles as physics truth.

## 7. LOD And Animation

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

## 8. Collision And Hitboxes

Fauna collision uses primitives:

- Capsule for spine/body mass.
- Spheres for head, joints, organs, and weak points.
- Capsules for fins, arms, tentacles, and tails.
- Boxes for armored plates or broad shell regions.

LOD0 render mesh is never used as MeshCollider. Bite, lunge, and damage routing must reference hitbox primitives or authored sockets, not visual triangles.

## 8A. Runtime And Hot-Path Boundary

Fauna runtime truth is AI owner state, animation/VAT/skinning owner state, hitbox primitives, attack/contact sockets, weak spot identity, and save identity. The visual mesh is presentation.

Hot paths must not generate or mutate creature geometry, deformation topology, blendshape/VAT frames, UVs, masks, LOD chains, hitboxes, collider proxies, material instances, or skeleton/socket identity. Runtime may animate authored rigs/VAT, select approved LODs, drive shader masks, and read cached hitbox/socket data. `GlobalQualityWeight` may scale presentation density, animation/VAT sampling, diagnostics, and LOD distance only; it must not change attack reach, hitbox truth, weak spot identity, locomotion authority, AI decisions, or save state.

## 9. Rejection Gates

Reject if:

- Bendable zones lack edge loops or have long triangles crossing deformation axes.
- Vertex color masks are decorative instead of semantic.
- Hitboxes do not align with visible attack/weak zones.
- Material slots exceed need and break instancing without proof.
- LOD2 destroys threat silhouette or weak spot readability.
- Any runtime path generates creature geometry during gameplay.

## 10. Proof Artifacts

Fauna generation must output:

- creature family, seed, body-plan declaration, locomotion type, and deformation route;
- skeleton, VAT, blendshape, or rigid-socket ownership note;
- LOD triangle counts and silhouette screenshots at expected encounter distances;
- topology debug capture showing deformation loops around joints, jaws, fin roots, tentacles, and tail bases;
- vertex color channel summary for deformation, bioluminescence, AO, and damage/wetness masks;
- UV/material zone report for head, weak spots, organs, shell, flesh, membrane, and emission;
- hitbox/collider proxy layout aligned to visible attack and weak zones;
- animation fallback route for Compact, Middle, High, and Ultra quality lanes;
- render capture with textures on and with flat material override, proving the mesh is not primitive anatomy hidden by shaders.

Runtime claims require profiler and GC proof. Static mesh standards alone do not prove animation cost, skinning cost, VAT upload cost, or encounter behavior.

## 11. Acceptance Sentence

A generated fauna asset is accepted only when its mesh, deformation topology, semantic vertex streams, material zones, LODs, and hitboxes make the creature readable, threatening, animatable, and performant without runtime geometry generation or texture tricks hiding primitive forms.
