# Rationale 1886

Static-only discovery was required. I did not execute Unity, import assets, bake textures, run PlayMode, capture screenshots, profile, build, or modify project source/assets.

Decision: select `AITextureControlMapBaker/Shinobu269` as the strongest reusable pipeline. Evidence: it already owns control-map template generation, texture ingestion, import settings, material binding, reports, and fixed-size black-box telemetry. It needs a product-face-scoped manifest before use because the existing prefab binding route is broad.

Decision: list `ShallowsBioForgeBatchBaker` as the second strongest reference route. Evidence: it owns atlas authoring/import/material binding/reporting for albedo, normal, ORM, and MatCap, but its ORM shader layout is organic-specific and not universal MRAO.

Decision: reject direct reuse of sky/ocean/terrain materials for product-face pickups/tools/vehicles/suit. Evidence: those routes own environmental visual identity and include shader-specific water/Crest/sky assets. They can inform visual language only.

Decision: future work must audit packed-mask channels before relink. Evidence: ToolDecayLit, UberNoir, ProceduralBio, MraoAtlasLit, and MasterLit expose different packed-channel contracts.

