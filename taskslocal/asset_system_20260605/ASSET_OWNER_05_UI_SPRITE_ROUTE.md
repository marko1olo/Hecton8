# Asset Owner 05 - UI Sprite Route

Mission: classify static UI/resource sprites for HUD/tool/inventory route use without claiming UI runtime readiness.

Read first:

- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `localization.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`

Required checks:

- Separate finished-looking UI source candidates from masks/silhouettes/prototype art.
- Confirm `Assets/_Project/Art/Sprites/ui/OXYGEN.png` is the detailed oxygen icon candidate and `Assets/_Project/Art/Sprites/oxygen-tank.png` is a mask/silhouette unless later proof contradicts it.
- List likely HUD/inventory/tool/icon route candidates under `Assets/_Project/Art/Sprites` and `Assets/_Project/Art/TEXTURES`.
- Identify atlas/import proof still required: sprite import type, sRGB, mipmaps off/on by role, atlas ownership, TMP/UX runtime binding, localization/font atlas impact.

Proof output:

- Static route table under `Docs/Reports/AssetSystem_20260605/`.
- No Unity import, Atlas, prefab, or scene mutation.
- No claim of HUD/UI readiness without runtime UI proof.
