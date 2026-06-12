# Status 2203

ID: 2203
Role: PHOTIC_TEXTURE_SOURCE_AND_GEMINI_PROMPT_PACK
Evidence class: STATIC VERIFIED

## Mandates Recorded

- Gemini/source art is not production-ready until manifest, static audit, visual tile review, channel-role plan, derivation proof, and Unity preview exist.
- Surface, shoreline, and photic shallows require bright readable premium material identity; darkness cannot hide weak terrain or water art.
- Prompts must be English, square, seamless, tileable, orthographic/top-down where applicable, with no perspective, text, logo, border, or baked albedo lighting.
- Albedo, height, roughness, normal, caustic, wetness, and mask roles must be generated/derived deliberately; one image cannot serve as every PBR channel.
- Compact lane must preserve material identity under VRAM/texture budgets; high/ultra may add resolution, decals, richer normals, and wetness layers without changing material truth.
- `Tools/GeminiTextureIntakeAudit.py` rejects seam/band mismatch, dark/clipped albedo, saturated channels, non-square sources, and writes CSV/Markdown/2x2 previews.

## Task Status

- 01 Authorities read: DONE.
- 02 Existing Gemini outputs/manifests/audits inspected: DONE.
- 03 Source states summarized: DONE.
- 04 Intake audit script inspected: DONE. No narrow bug proven. No script edit made.
- 05-07 Texture taxonomy, material intent, and prompt pack written: DONE.
- 08-09 Redo and image-to-image follow-up prompts included: DONE.
- 10-17 Budget, naming, intake, rejection, derivation, hardware, and proof path written: DONE.
- 18 Prompt pack written: DONE.
- 19 Intake checklist written: DONE.
- 20 Generation queue README written: DONE.
- 21 Log appended: DONE.

## Files Written

- `Docs/Tasks/Status_2203.md`
- `Docs/Reports/Batch22/2203_PHOTIC_TEXTURE_PROMPT_PACK.md`
- `Docs/Reports/Batch22/2203_TEXTURE_INTAKE_CHECKLIST.md`
- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md`
- `Docs/AgentLogs/Rationale_2203.md`
- `Docs/AgentLogs/LOG_2203.md`

## Current Asset State

- Wet basalt 1428: `SOURCE_ONLY / REJECT`.
- Wet basalt 1429: `SOURCE_ONLY / REJECT`.
- Wet basalt 1429 periodic mean: `REJECT`.
- Batch21 photic seabed substrate: `SOURCE_REFERENCE_ONLY / REJECT`.
- Batch21 photic shell sand substrate: `SOURCE_REFERENCE_ONLY / REJECT`.
- `CANDIDATE`: none.
- `READY_FOR_DERIVATION`: none.

## Verification

STATIC VERIFIED: local docs, manifests, audit CSV/Markdown, and audit script inspected. Unity was not run. No browser automation. No asset import.
