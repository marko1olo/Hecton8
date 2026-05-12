# ASSET_SCOUT Quality Gate Cross-Reference

Date: 2026-05-12
Status: PENDING VERIFICATION

Source gate: `Docs/QUALITY_GATES.md`

## Gate Conflicts

- Texture format gate requires BC7 for Albedo/Mask and BC5 for Normal/Detail. Static scan found `134` strict world-format violations and `1,126` auto/uncompressed/RGBA/RGB risk entries.
- Max texture size gate requires hero <= 2048 and scatter <= 1024. Static scan found `27` textures over 2048 px.
- Read/Write gate requires Off. Static scan found `10` readable textures and Unity mesh probe found `1,485` readable mesh assets.
- LOD gate requires 3+ LOD levels / thresholds. Unity prefab probe found `Assets/_Project/Prefabs/Hecton Ocean.prefab` using `HectonWaterMesh` at `80,000` triangles without LODGroup.
- Transparency/fill-rate gate is not explicit in the checklist but AGENTS/graphics rules reject expensive transparent overdraw without proof. Material scan found `656` transparent/fill-rate-risk materials.
- Baked AO / static lighting gate cannot be certified. `02_HECTON_WORLD` additive probe found `412` mesh renderers and `0` static renderers.
- Addressables split/duplicate gate cannot be certified. `Assets/AddressableAssetsData` is absent.

## Static Budget Snapshot

- `02_HECTON_WORLD` texture dependency estimate: `404.714MB`.
- Texture partition authority: `900MB`.
- Static texture snapshot is below partition, but runtime residency, lightmaps, render targets, and mesh/compute buffers were not profiled.

## Decision

STATUS: BUDGET COMPLIANT for the static `02_HECTON_WORLD` texture dependency estimate only.

STATUS: NOT BUDGET COMPLIANT for the global asset pool until oversized/format/read-write/audio/transparent/LOD findings are resolved or waived with profiler evidence.

