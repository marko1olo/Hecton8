# MATERIAL_DECAY_ARTIST Rationale

STATUS: PENDING VERIFICATION

## Decision 0: Scope and Mandate Selection
Problem: Equipment corrosion is shader-only presentation damage and must not become runtime mesh deformation, decal spam, or per-object material cloning.
Solution: Use Visual Fake First: one shared packed rust atlas, scalar/global shader inputs, local-space shader math, and CBUFFER-compatible properties in Hecton_CoreLit.hlsl.
Rejected Alternatives: Rust decals add GameObjects/draws and violate prompt. Mesh pitting/deformation adds geometry and CPU/GPU cost for presentation-only truth. Material clones break batching and memory discipline.
Scalability potential: Low disables POM and UV distortion; Middle uses roughness/normal blend; High enables 4-step POM; Ultra can spend saved geometry cost on deeper shader depth and blood/wetness polish.
Hardware Impact: i3/MX350 avoids extra renderers, avoids new geometry, avoids per-frame GC. Expected CPU gain vs decal/deform path: 0.05-0.30ms depending object count; GPU cost remains texture/ALU gated and PENDING VERIFICATION.
