# Rationale 1891

Evidence class: STATIC_SOURCE / STATIC_DOC.

Decision: treat Shinobu269 as reusable only for editor-side texture import, material creation patterns, reports, and black-box telemetry. Do not approve direct ProductFace prefab relink through the existing generic CSV.

Reason: `AITextureMaterialBinder.AssignMaterialFromManifest` and `ApplyMaterialToPrefab` can already save prefab material-slot changes when `ai_texture_prefab_bindings.csv` matches. ProductFace needs stronger owner-slot proof, channel contract validation, and dry-run separation before any prefab write.

Decision: require a ProductFace-specific manifest.

Reason: prior discovery and static shader evidence show multiple packed map dialects: MRAO, ARM, ORM, and PackedV1. Filename tokens are not enough to protect shader/material truth.

Decision: import and relink are separate phases.

Reason: import may safely create/update texture and material assets after validation. Prefab mutation has higher blast radius and must belong to a relink owner, not the ingestion watcher or generic binder.
