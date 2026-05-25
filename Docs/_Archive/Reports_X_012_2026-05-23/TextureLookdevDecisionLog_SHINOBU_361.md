# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Texture Lookdev Decision Log

Status: ACTIVE / PENDING ART QA

## 2026-05-23 REF_SEED_001 First Candidate

Decision: REJECT AS STYLE ANCHOR.

Reason:

- Reads as repeated rounded sci-fi bathroom tile.
- Too much square panel repetition.
- Not enough layered wall construction.
- Amber bars read like baked emissive, unsafe for albedo/source reference.
- Does not answer the actual asset need: habitat wall system with base skin, service conduits, and separate instrument/tool attachments.

What to do instead:

1. Generate `NEXT_RETRY_WALL_SYSTEM.md`.
2. Use four layer jobs before continuing wall blockers:
   - `WALL_LAYER_001_BasePressureSkin`
   - `WALL_LAYER_002_ServiceConduitOverlay`
   - `WALL_LAYER_003_InstrumentAttachmentKit`
   - `WALL_LAYER_004_WallTrimHeight`
3. Use accepted wall layers as references for `B01_007` through `B01_015`.

Rejected alternative:

- Saving the first seed as `LOOKDEV_APPROVED_REF_SEED_001.png`. It would poison later wall prompts into tile repetition.

Evidence class: USER_IMAGE_REVIEW / STATIC_DOC.

## 2026-05-23 Layered Wall Candidate A Set

Files received:

- `C:\Users\danat\Downloads\лейер 1.png`
- `C:\Users\danat\Downloads\лейер трубы.png`
- `C:\Users\danat\Downloads\инструменты.png`
- `C:\Users\danat\Downloads\нормаль.png`

Copied to:

- `Docs/ArtDrop/SHINOBU_361/LayeredWallSystem/WALL_LAYER_001_BasePressureSkin/CANDIDATE_WALL_LAYER_001_A.png`
- `Docs/ArtDrop/SHINOBU_361/LayeredWallSystem/WALL_LAYER_002_ServiceConduitOverlay/CANDIDATE_WALL_LAYER_002_A.png`
- `Docs/ArtDrop/SHINOBU_361/LayeredWallSystem/WALL_LAYER_003_InstrumentAttachmentKit/CANDIDATE_WALL_LAYER_003_A.png`
- `Docs/ArtDrop/SHINOBU_361/LayeredWallSystem/WALL_LAYER_004_WallTrimHeight/CANDIDATE_WALL_LAYER_004_A.png`

Decision:

- `WALL_LAYER_001_BasePressureSkin`: REJECT FINAL / KEEP ONLY AS MATERIAL COLOR NOTE. It still reads as repeated rounded panels.
- `WALL_LAYER_002_ServiceConduitOverlay`: KEEP AS COMPOSITION REFERENCE / NOT FINAL ALBEDO. Good service-route logic, but too ink-outlined and illustrated.
- `WALL_LAYER_003_InstrumentAttachmentKit`: KEEP AS SHAPE REFERENCE / NOT FINAL ATLAS. Good kit vocabulary, but baked shadows and product-render cleanliness must be removed for source art.
- `WALL_LAYER_004_WallTrimHeight`: REJECT FINAL FOR NOW / KEEP AS VALUE NOTE. It inherits the rejected panel grid; regenerate after base wall is corrected.

Next action:

- Regenerate only `WALL_LAYER_001_BasePressureSkin` with `Prompt 1B - Base Wall Retry` in `START_HERE_WALL.md`.
- Do not regenerate the height/normal source until the base pressure skin is accepted.

Evidence class: USER_IMAGE_REVIEW / LOCAL_FILE_COPY / STATIC_DOC.

## 2026-05-23 Layered Wall Round 2 Prompt Tightening

Decision:

- Active operator file is now `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/START_HERE_WALL.md`.
- Run `Prompt 1C - Base Wall Retry` first.
- Do not regenerate normal/height until base wall candidate passes.
- Keep `WALL_LAYER_002_A` and `WALL_LAYER_003_A` only as references, not final source art.

Reason:

- Candidate A improved the wall direction, but the base and height still encode a repeated rounded-panel grid.
- Normal/height generated from a rejected base repeats the same structural error.
- Service conduits and instrument atlas contain useful shape grammar, but both need shadow/outline/product-render cleanup before final use.

Evidence class: USER_IMAGE_REVIEW / STATIC_DOC.

## 2026-05-23 Layered Wall Round 3 Base-Only Lock

Decision:

- Active operator file remains `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/START_HERE_WALL.md`.
- Current immediate prompt is now `Prompt 1D - Base Wall`.
- Generate only the base wall: `CANDIDATE_WALL_LAYER_001_C01.png` through `CANDIDATE_WALL_LAYER_001_C03.png`.
- Do not use rejected base or rejected normal image as references.
- Do not generate pipes, instruments, normal, ORM, or Unity assets until the base wall passes.

Reason:

- The generated base and height images still show rounded rectangle panel grammar.
- The service and instrument images are useful only as temporary design evidence, not final albedo source art.
- The next correct step is a mostly uninterrupted monolithic pressure-wall substrate.

Evidence class: USER_IMAGE_REVIEW / STATIC_DOC.
