# 3DMODEL_EQUIPMENT_PROPS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC / AUTHORING_STANDARD
Scope: tools, devices, cockpit parts, lab machinery, storage containers, cables, handholds, consoles, valves, pumps, lights, sensors, and small generated set dressing.

## First-20 Route Hook

- First-20 moment: first repair tool, scanner target, pump/valve, cockpit readout, salvage container, cable route, or handhold that teaches the player machinery under pressure.
- Route blocker removed: prevents opening-route equipment from becoming decorative clutter, primitive silhouettes, unreadable displays, or interaction anchors with no functional visual truth.
- Proof class: STATIC_DOC until function report, socket/anchor report, LOD/collider proof, display/material proof, Unity import evidence, compact capture, and route screenshot exist.

## 1. Prop Law

Generated props must look manufactured, handled, repaired, and used under pressure. A prop is not accepted because it has a primitive silhouette and a material. It must communicate function: grip, hinge, screw, latch, display, sensor, vent, cable route, seal, wear, and scale.

Every generated prop must define:

- Function category.
- Interactable surfaces and sockets.
- Hand/contact zones when relevant.
- Material slots.
- LOD chain.
- Collision proxy.
- Pivot and orientation contract.

## 2. Geometry Requirements

Small equipment needs deliberate bevels because players inspect it at close range.

- All visible hard edges above 30 degrees get bevel/chamfer.
- Handles need inner radius and grip ridges.
- Cylinders need enough radial segments for near view: minimum 12 LOD0 for small parts, 24+ for hero cylinders.
- Screens/displays need inset geometry, glass bevel, and emissive mask.
- Fasteners and bolts can be geometry at LOD0 but must become normal/mask detail in LOD1/LOD2.
- Cables use tubes or bevelled ribbons with capped ends; no unthickened curves.

## 3. Material And Mask Rules

Default prop material zones:

- Painted metal or composite casing.
- Exposed metal edge/wear.
- Rubber grip/gasket.
- Glass/display/emissive.
- Dirt, corrosion, and wetness masks.

Vertex color contract:

- R = edge wear/contact polish.
- G = grime/rust/wetness accumulation.
- B = AO/cavity darkness.
- A = emissive/display/decal eligibility or interaction highlight mask.

Generated display text, labels, and warning marks must use atlas/decal slots, not unique material clones for each prop.

## 4. UV Rules

Props need unique UVs for player-facing zones. Hidden underside and repeated fastener trim may use trim sheets. Texel density:

- Handheld hero tools: 1024 px/m equivalent.
- Standard equipment: 512 px/m.
- Background clutter: 256 px/m or trim atlas.

UV seams belong on undersides, back faces, or panel borders. Seams across handles, display faces, or curved hero edges are rejected unless hidden by trim.

## 5. LOD Rules

LOD0:

- Full bevels, sockets, hand contact detail, labels, display masks.

LOD1:

- Preserve function silhouette and interaction surfaces.
- Collapse small bolts/cables into normals or masks.

LOD2:

- Preserve broad shape and readable handle/display silhouette.
- No tiny fastener geometry.

HLOD:

- Cluster mesh or card for clutter groups.

## 6. Collision And Interaction

Collision uses primitives:

- BoxCollider for casing.
- CapsuleCollider for handles and pipes.
- SphereCollider for knobs.
- Convex hull under 100 triangles only for irregular handheld props.

Interaction raycast targets may use a simplified trigger surface or named socket. Visual mesh triangles are not interaction truth.

All prop geometry, labels, display masks, UVs, LODs, sockets, pivots, colliders, and interaction anchors are generated and validated offline. Runtime may only load serialized prefabs, read named anchors, and update approved material/UI state. Runtime does not create prop meshes, cook prop colliders, generate labels, unwrap UVs, or search visual triangles for interaction truth.

## 6A. Runtime And Hot-Path Boundary

Equipment runtime truth is serialized prefab identity, pivot, socket names, interaction anchors, collider proxies, material slots, and approved UI/display state. Hot paths must not create meshes, labels, UVs, display atlases, colliders, sockets, pivots, material instances, or per-prop metadata.

Runtime may animate lights, displays, needles, damage state, wetness, and interaction highlighting only through predeclared material/UI channels and cached owner references. `GlobalQualityWeight` may choose prebuilt fidelity variants, label sharpness, LOD distance, and optional display detail; it must not change interaction truth, collider truth, socket identity, save identity, or material channel semantics.

## 7. Continuous Quality Scaling

`GlobalQualityWeight` scales prop fidelity through offline asset variants: bevel segment count, decal density, texture resolution, label sharpness, LOD transition distance, wear mask precision, and optional hero-screen emissive detail. It never changes pivot identity, socket names, interaction anchors, collider truth, material channel semantics, or runtime generation law.

Compact props still require visible function, bevels on inspected edges, readable silhouettes, shared material families, and proxy colliders. Higher quality levels add closer inspection detail; they do not turn a primitive into an accepted prop after the fact.

## 8. Rejection Gates

Reject if:

- The prop has no visible function.
- A close-view hard edge lacks bevel.
- Display/emissive areas are not masked.
- Collision uses visual LOD0.
- UV seams cut across player-facing handles/screens.
- Material count breaks batching without proof.

## 9. Proof Artifacts

Equipment and prop generation must output:

- function category, seed, pivot, handedness, orientation, socket, and interaction anchor report;
- LOD triangle counts and close-view silhouette capture;
- bevel, radial segment, handle thickness, screen inset, and cable thickness report;
- UV density, seam placement, atlas/decal slot, and material slot report;
- vertex color channel summary for wear, grime/wetness, AO, and display/decal masks;
- collider and interaction trigger layout;
- flat-material screenshot proving the prop is functional geometry before texture detail;
- final-material screenshot proving labels, display masks, corrosion, and wetness support function.

## 10. Acceptance Sentence

A generated prop is accepted only when it communicates its function through geometry, uses offline-authored bevels, masks, anchors, LODs, and proxy colliders, remains batchable through shared material routes, and proves that player-facing details are not just texture noise on a primitive.
