# Rationale_1898

ID: 1898
Evidence class: STATIC_SOURCE, STATIC_DOC, STATIC_AUDIT_TEXT

## Decisions

1. In-place prefab internals replacement is the default future strategy. Reason: existing construction family and buildable data already reference the current `Construction/Final` prefab paths. Recreating prefabs risks GUID/reference damage.
2. ScifiFacility FBX models are allowed source candidates. Direct ScifiFacility prefab drop-in is blocked because prefab packaging does not preserve HECTON sockets, templates, power metadata, collision policy, LOD/HLOD, or proof reports, and two ScifiFacility prefabs contain built-in Cube refs.
3. `WreckagePrefabFactory` is allowed only for wreck/debris after real hull/debris/COL source exists. Static inspection found `Assets/_Project/BakedGeometry/Wreckage` empty and `Assets/Prefabs/Environment/Wrecks` missing.
4. `ConstructionBootstrapAuthoring` is blocked as production final authoring. It contains the primitive construction definitions but is protected by `AllowLegacyPrimitiveFinalAuthoring`; future work must not loosen that gate.
5. `PFB_SargassumCollapseChunk` requires an ownership decision before mutation because it has no scanned family link and `SargassumGlobalDragManager` has a hardcoded fallback path to it.
6. No runtime proof is claimed. The packet uses `PENDING UNITY` for Unity/import/prefab/profiler/player evidence.

## Main Risk

The most dangerous implementation risk is a future agent preserving prefab paths while silently replacing internals through the legacy primitive authoring route or direct ScifiFacility prefab drop-in. That would keep GUIDs but preserve or reintroduce primitive/proxy art and bypass socket/power/template proof.
