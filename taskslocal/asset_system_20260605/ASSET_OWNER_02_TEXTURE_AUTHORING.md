# Asset Owner 02 - Texture Authoring

Mission: turn static source candidates into route-owned PBR texture packs. Do not import or promote final materials without Unity proof from Owner 01.

Read first:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_REVIEW_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `streaming.md`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

Priority packs:

- P0 support: foam/contact mask replacement for active rejected `foam.png`.
- Wet basalt shoreline: remove watermark/baked-light/repeating-island risk.
- Shell/sand photic bed: author clean albedo/normal/ORM or MRAO with route-readable breakup.
- Foam/contact masks: RGBA mask for salt rim, bubble breakup, wet contact, residue. Do not reuse `foam.png` as visible sheet.
- Aegir/cloud stack: compose from `clouds0_diff.png`, `Aegir_storms.png`, and sky sources; baked disc is prototype only.

Manual review constraints:

- `TX_H8AegirGasGiantBakedDisc_1428.png` is not final hero Aegir art.
- `foam.png` is rejected as visible world shoreline art.
- Generated wet basalt/shell/sand sheets are source-only until cleaned and channel-authored.
- Flora/coral stacks are plausible source material but blocked by material/readback proof and streaming-mip proof.

Required output:

- Clean source/contact sheets in `Docs/GeneratedAssets/...`, not `Assets`.
- A concise manifest stating source image, generated maps, channel packing, intended material slot, compression target, mip/streaming rule, and visual risks.
- Do not write final assets under `Assets` unless explicitly assigned import work and process gate is clean.
- Every generated source pack must map back to a recipe section: foam/contact, Aegir/cloud, wet basalt/shell sand, or UI sprite role.
- Existing foam/contact and Aegir/cloud prototypes are source-only; next pass should clean them, not direct-import them.

Acceptance blocker:

- Unity material readback and screenshots remain required before any final promotion.
