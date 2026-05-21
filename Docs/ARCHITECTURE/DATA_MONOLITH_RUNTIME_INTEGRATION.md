# Data Monolith Runtime Integration

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Runtime Contract

The runtime loader may consume `Hecton8/DataMonolith/static_data.h8bin` from StreamingAssets. Current workspace scan did not find:

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`

Therefore Data Monolith readiness is `PENDING VERIFICATION`.

## Ownership

- immutable static data belongs to Data Monolith
- mutable save data belongs to the save container / paging protocols
- cross-domain native runtime buffers belong to `GlobalDataVault`
- Addressables or visual asset groups are delivery mechanisms, not gameplay truth stores

## Failure Rule

Runtime boot must fail closed or enter a documented fallback if the H8DM payload is required and missing. Silent fallback to generated defaults is rejected unless a route card names the owner, version, checksum, and diagnostic artifact.

## Non-Claims

No import, bake, boot, player-build, profiler, or checksum artifact is linked here.
