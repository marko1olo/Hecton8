# Rationale 1885

Evidence boundary: static YAML and static docs only. No Unity, build, runtime, prefab, asset, source, `.meta`, scene, binary, import, bake, screenshot, profiler, or DataMonolith action was allowed.

Decisions:

- Marked player/tool/resource/transport/sky visible primitive state as `RED_STATIC_PRIMITIVE`, not accepted.
- Marked current tool explicit `ANCHOR_*` names as `MISSING_STATIC_EVIDENCE` in the report narrative instead of inventing anchors from mesh bounds.
- Preserved current serialized owner references as the authority targets: player cameras/HUD/visor/swim attachments, tool `_toolData`, world `itemData`, pickup/highlight/scan components, resource data assets, transport anchors/contracts/presets, sky follow camera route, and Crest input/runtime components.
- Classified only the exact `Ocean_Crest` sargassum Crest input planes as `HIDDEN_INPUT_CANDIDATE_PENDING_PROOF`; no broad hidden-input exception was granted.
- Classified `Item_Titanium`, `STRUCTURES.prefab`, and `Buildings/Cube.prefab` as quarantine-or-relink decisions requiring production-reference proof.

Highest risks:

- Player hand/camera/HUD/visor/swim transforms can drift silently during visual relink.
- Tool relink lacks explicit current `ANCHOR_*` transforms; future owner must add/compare named anchors instead of using decorative mesh bounds as truth.
- Copper and titanium data paths must remain canonical: `Data_Copper.asset` and `Data_TitaniumScrap.asset`.
- Transport `RiderAnchor` and `DismountAnchor` are mount/dismount safety truth and require exact local transform comparison.
- Crest input planes need exact hidden-input proof; visible primitive input planes remain art debt.
