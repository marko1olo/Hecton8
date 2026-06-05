# Rationale 1857

Evidence class: `STATIC_SOURCE`

## Decision: Reject Current Final Prefab

`PFB_SargassumCollapseChunk.prefab` is under a production `Final` path and contains a visible Unity built-in primitive mesh reference:

`m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}`

That violates the generated/production asset floor. Primitive collision proxy is not the issue; visible primitive art is the issue.

## Decision: Classify As Latent Relink Risk

Static GUID/path scans did not prove active serialized scene/data/prefab placement outside the prefab itself.

However, `SargassumGlobalDragManager` owns `collapseChunkPrefab`, spawns it in collapse/impact paths, and its editor validation loads this exact production Final prefab path when the field is null.

Therefore the prefab is not a safe orphan. It is:

`LATENT_RELINK_RISK / PRODUCTION-PATH PRIMITIVE FINAL`

## Decision: Prefer Rebuild Over Deletion

`SargassumCollapseChunk` is a real pooled rigidbody behavior with spawn/despawn/scrap/silt/scavenger hooks. Static evidence does not justify deleting the prefab while runtime source still has a relink route.

Primary path is an in-place non-primitive rebuild if the feature remains intended.

Quarantine is valid only after the feature owner removes or repoints the path fallback and reference scans prove it is unreachable.

Deletion is valid only after feature retirement and clean reference proof.
