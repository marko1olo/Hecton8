# Flora Final Bake Intake

> Legacy note: use [Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md](/c:/hades/Hecton8/Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md) as the short agent entry point. This README stays as the baked-finals intake contract and folder-local source of truth.

Source of truth for real flora final prefabs.

## Purpose

- This folder is for baked or hand-authored final flora prefabs.
- Runtime world selection does not generate these assets.
- `proxy` and `final` remain separate layers.

## Supported Families

- `family.kelp.tall`
- `family.kelp.patch.dense`
- `family.kelp.canopy`
- `family.kelp.abyssal`
- `family.coral.low`
- `family.coral.branching`
- `family.coral.massive`
- `family.coral.plate`
- `family.coral.brittle`

## Intake Rules

- Put prefabs anywhere under this folder.
- Preferred layout: one subfolder per family, using safe family token names:
  - `family_kelp_tall`
  - `family_kelp_patch_dense`
  - `family_kelp_canopy`
  - `family_kelp_abyssal`
  - `family_coral_low`
  - `family_coral_branching`
  - `family_coral_massive`
  - `family_coral_plate`
  - `family_coral_brittle`
- Alternative layout: prefab file names may also start with the safe family token, for example:
  - `PFB_family_kelp_tall__hero_a.prefab`
  - `PFB_family_coral_plate__ledge_b.prefab`
- Optional metadata tokens may be added as extra `__token` suffixes on the prefab name:
  - `__wN` -> variant weight override, example `__w3`
  - `__sMIN-MAX` -> uniform scale range override in percent, example `__s92-108`
  - full example: `PFB_family_kelp_tall__hero_a__w3__s92-108.prefab`
  - metadata tokens do not define logical variant identity; they only change intake settings for the same visual variant
  - only one `__wN` token and one `__sMIN-MAX` token are allowed per prefab name
  - invalid metadata is fail-closed in intake:
    - the prefab is skipped instead of being linked with fallback defaults
    - generated `GEN_` fallback variants stay active for that family until the authored prefab name is fixed
- Quality fallback rule:
  - `GEN_` prefabs are starter fallbacks only.
  - If a family folder contains at least one non-`GEN_` baked flora prefab, intake ignores the `GEN_` starters for that family and links only the authored finals.
  - If logical duplicates still exist after metadata-token stripping, intake keeps one deterministic winner and warns; validator still treats the situation as a failure that must be cleaned up.
  - duplicate winner policy prefers the prefab with more explicit intake metadata (`__wN`, `__sMIN-MAX`), then falls back to stable name ordering.

## Rebuild Path

1. Place or regenerate flora prefabs here.
2. Run `Hecton/Authoring/Rebuild World Runtime Stack`.
3. `WorldProceduralFloraBakedStarterGenerator` regenerates owned `GEN_` starter finals.
4. `WorldProceduralFloraFinalVariantAuthoring` links discovered prefabs into matching procedural families as real final variants.
5. Placeholder final variants remain only as fallback where no real flora final prefab exists.

## Starter Generator Path

- Run `Hecton/Authoring/Generate Procedural Flora Baked Starters` to create optimized `LODGroup`-based starter finals in the family folders.
- Generated starter assets use the `GEN_` prefix and are owned by the generator.
- Current starter target is deterministic multi-variant coverage per supported flora family, not a fixed `3`-variant cap.
- Each generated starter prefab currently contains:
  - root `LODGroup`
  - `__LOD0`, `__LOD1`, `__LOD2`
  - exact thresholds: `0.6 / 0.15 / 0.04 / 0`
  - near-field transition mode: `LODFadeMode.CrossFade` with animated crossfading enabled
  - cull is handled by the final `LODGroup` cull step plus runtime visibility rules
- Current generated family forms:
  - kelp:
    - tall: `banner / broadleaf / colossus / frondcrest / lamina / lance / lean / paddle / ribbon / rope / sail / seedling / stalk / tower`
    - patch dense: `bladder / brush / drape / frilltuft / nest / paddlespray / patch / patch_tall / ring / sheet / sheetwall / tuft`
    - canopy: `crown / fan / featherfan / frond / laminaria / mantle / oar / paddlefan / rosette / sheetwall / splay / tapestry / veil`
    - abyssal: `braid / cathedral / cowl / lantern / mantle / nodule / pennant / petal / reed / shroud / strap / tatterveil / veilwall / whip`
  - coral:
    - low: `bed / knoll / plate`
    - branching: `branch / fan / mass`
    - massive: `boulder / head / porous`
    - plate: `ledge / shelf / stack`
    - brittle: `crown / fan / halo / lace / spire / sprig / thicket`
- These starter assets are production-safe placeholders for the baked-final pipeline, not a substitute for later photorealistic hand-authored or baked flora art.

## Validation Path

- Run `Hecton/Validation/Validate Procedural Flora Final Variants`.
- Validator checks:
  - family resolution from folder/file naming
  - optional flora metadata token parsing for `weight` and `uniformScaleRange`
  - duplicate logical variant identity after metadata-token stripping
  - renderer/material presence
  - triangle/material-slot/renderer budget per flora family, measured from the highest visible LOD renderers
  - forbidden runtime baggage on visual finals (`Collider`, `Rigidbody`, `Animator`, `ParticleSystem`, `AudioSource`)
  - renderer default cost guards:
    - `shadowCastingMode` should be `Off`
    - `receiveShadows` should be `false`
    - `lightProbeUsage` should be `Off`
    - `reflectionProbeUsage` should be `Off`
    - `motionVectorGenerationMode` should be `ForceNoMotion`
  - LOD recommendation threshold
  - family-level authored (`a`) vs generated (`g`) coverage summary
  - texture-source sanity:
    - imported texture assets are expected for real photoreal finals
    - procedural editor-generated texture assets are not treated as final photoreal proof
  - shader contract:
    - `_QUALITY_MX350` / `_QUALITY_HIGH`
    - `_NormalScale`
    - world-space triplanar flora material contract
    - exact flora LOD thresholds and crossfade settings
  - generated texture-source guard:
    - procedural editor-generated `.asset` textures are starter-only fallback proof
    - authored finals using those generated textures fail closed
  - imported texture contract:
    - imported flora textures are expected under `Assets/_Project/Art/Textures/WorldProceduralFlora/Imported/<familyId>/`
    - exact naming:
      - `albedo___<familyId>.png`
      - `detail___<familyId>.png`
      - `normal___<familyId>.png`
      - `mask___<familyId>.png`
    - exact manual importer contract:
      - `albedo`: `Default`, `sRGB On`, `Wrap Repeat`, `Mip Maps On`, `Read/Write Off`, `Max Size <= 2048`
      - `detail`: `Default`, `sRGB Off`, `Wrap Repeat`, `Mip Maps On`, `Read/Write Off`, `Max Size <= 1024`
      - `normal`: `Normal Map`, `sRGB Off`, `Wrap Repeat`, `Mip Maps On`, `Read/Write Off`, `Max Size <= 2048`
      - `mask`: `Default`, `sRGB Off`, `Wrap Repeat`, `Mip Maps On`, `Read/Write Off`, `Max Size <= 2048`
    - validator fails closed when imported flora maps break this contract

## Family Budgets

Use these as the hard authored-final intake limits. `GEN_` starters already sit below them; real photoreal finals must stay inside the same caps unless the validator budget is deliberately changed first.

| Family | Max Renderers | Max Material Slots | Max Triangles | LOD Recommended At |
| --- | --- | --- | --- | --- |
| `family.kelp.tall` | `12` | `6` | `8000` | `4500` |
| `family.kelp.patch.dense` | `18` | `8` | `12000` | `6500` |
| `family.kelp.canopy` | `14` | `6` | `10000` | `5500` |
| `family.kelp.abyssal` | `14` | `6` | `9000` | `5200` |
| `family.coral.low` | `10` | `4` | `7000` | `3500` |
| `family.coral.branching` | `16` | `6` | `12000` | `6500` |
| `family.coral.massive` | `12` | `5` | `9000` | `5000` |
| `family.coral.plate` | `12` | `5` | `8500` | `4500` |
| `family.coral.brittle` | `14` | `6` | `9500` | `5200` |

## Authoring Policy

- Replace `GEN_` starters family-by-family, not all at once.
- Keep authored finals inside the table above before asking for budget expansion.
- Prefer one `LODGroup` root with cheap `LOD1`, not high-detail single-LOD hero meshes.
- Exact flora target thresholds are `0.6 / 0.15 / 0.04 / 0`.
- Atlas planning rule:
  - do not merge flora maps into atlases while family texture coverage is incomplete or importer contracts are still failing
  - current target is one tiling set per family
  - atlas consolidation becomes worth evaluating only after all target families have one clean imported set each and validator/report stay green
- If one authored prefab enters a family, that family's `GEN_` starters are intentionally ignored by intake.
- Use metadata tokens only for intake-facing controls:
  - `__wN` for spawn frequency weight
  - `__sMIN-MAX` for uniform scale spread
  - malformed `w` / `s` tokens are treated as validator warnings and should be fixed before rebuild signoff
  - duplicate `__w...` or duplicate `__s...` tokens on the same prefab name are invalid
  - do not clone one visual variant into multiple prefabs by changing only `__wN` / `__sMIN-MAX`; validator treats that as a duplicate logical variant
- Treat `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md` as the per-family readiness ledger:
  - coverage `aX/gY`
  - expected linked real finals vs actual linked real finals
  - authored-overrides-generated semantics for mixed families
  - linked real finals vs placeholders
  - max budget triangles
  - logical `variantId` readback after metadata-token stripping
  - linked intake `weight` and `scale` readback per prefab
    - `*` after `weight` or `scale` means that value came from explicit prefab-name metadata, not family default
  - headroom to budget ceiling once the report is regenerated on the new build

## Constraints

- Do not place proxy prefabs here.
- Do not use this folder for runtime-generated temporary assets.
- Prefabs here must be optimized for MX350 target budgets.
