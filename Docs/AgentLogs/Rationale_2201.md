# Rationale 2201

## Route Classification
Renderer/script existence was rejected as activation proof. The audit used serialized scene/prefab/data GUID references and YAML enabled states because the task asks whether routes are absent, disabled, misconfigured, hidden, or visually ineffective.

## Key Decisions
- Deferred caustics is marked active only at renderer-feature level. It remains unaccepted because `HectonDeferredCausticsFeature` is gated by `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer`, and the runtime owner GUID was not found in searched scene/prefab/data assets.
- Crest underwater is marked serialized/enabled because `Crest.UnderwaterRenderer` appears directly in `02_HECTON_WORLD.unity`; custom WaterOptics/HectonUnderwaterVisuals are not marked active because their script GUIDs were not found in searched serialized assets.
- Crest foam input with disabled MeshRenderer is not marked visually disabled by that renderer alone because `_disableRenderer: 1` is expected for simulation input. Visible output still requires runtime/Frame Debugger proof.
- Authored mesh fakes are marked disabled when either GameObject `m_IsActive: 0` or MeshRenderer `m_Enabled: 0` suppresses them.

## Proof Boundary
No Unity slot, no Play Mode, no imports, no builds, no captures. All findings are static handoff findings.
