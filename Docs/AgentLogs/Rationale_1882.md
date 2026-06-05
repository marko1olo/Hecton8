# Rationale 1882

Evidence boundary: static only. Static material YAML and path scans can prove asset names, GUID bindings, and missing texture slots. They cannot prove visual quality, import health, SRP Batcher state, runtime cost, or final acceptance.

Decision: mark most RuntimeVisualProof and RuntimeShell materials as candidate role references, not final sources. Reason: inspected YAML shows no assigned albedo, normal, metallic/gloss, or occlusion maps on most candidates. HECTON-8 texture rules require documented PBR maps, not flat color parameters.

Decision: mark `PLAYER_VISOR_GLASS_RIM` as `PARTIAL_SOURCE_STATIC`. Reason: `Mat_Visor_Glass.mat` binds `SuitVisor.shader` and resolves project-owned droplet/runoff textures. Missing scratch/fingerprint/grime maps and primitive visor mesh prevent acceptance.

Decision: reject unresolved GUID `31321ba15b8f8eb4c954353edc038b1d`, package/default `Lit.mat`, placeholder paths, and `MAT_PlayerSwimBlockout`. Reason: task and root bibles reject default, blockout, flat, unresolved, and debug material routes for product-face visuals.

Decision: keep collider/anchor truth out of material package. Reason: player `CapsuleCollider`, `HandAnchor`, transport `RiderAnchor`/`DismountAnchor`, occupancy, drive, mount/dismount, AUP, and survival/tool/movement truth are not material ownership.
