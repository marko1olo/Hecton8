# Gemini Batch30 Browser Intake Manifest Template - 2026-06-06

Status: `TEMPLATE`.
Evidence class: `STATIC_DOC`.

Copy this file beside every downloaded Gemini output and fill the fields. The filled manifest is still source-only evidence. It does not prove Unity import, material binding, SpriteAtlas packing, Addressables residency, runtime visual quality, memory, frame time, or GC.

## Identity

- Source file:
- Queue id:
- Prompt id:
- Generated time:
- Browser/tool:
- Operator:
- Intended family:
- Intended role:
- Owner route:
- Reference scope:

## Prompt

Paste the exact prompt used:

```text

```

## Download Handling

- Saved path:
- Filename matches queue pattern: `YES/NO`
- Source dimensions:
- Format:
- Alpha present: `YES/NO`
- Any browser watermark/text/logo/frame: `YES/NO`

## First Visual Triage

- Orthographic/source texture or sprite: `YES/NO`
- No perspective scene/object render: `YES/NO`
- No text/logo/watermark/border: `YES/NO`
- No baked directional lighting/shadows: `YES/NO`
- Tiling risk visible before audit: `LOW/MED/HIGH`
- Color/material identity fit: `LOW/MED/HIGH`
- Reject reason if rejected:

## Required Audit

Run:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root <downloaded_png> --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch30/<target_id>
```

Audit outputs:

- CSV:
- Markdown:
- Tile preview path:
- Contact sheet path:

Audit verdict:

- `SOURCE_REJECT`
- `SOURCE_HOLD_FOR_REPROMPT`
- `SOURCE_CANDIDATE_FOR_DERIVATION`

## Unity Boundary

Do not copy this source into `Assets/**` until the matching owner route produces:

- import role decision;
- compression/mip/sRGB/linear settings;
- channel manifest if mask/packed texture;
- material/SpriteAtlas/receiver slot readback;
- visual screenshot/capture in route context;
- memory/render/Frame Debugger/GC proof where required.

Final status: `SOURCE_ONLY_PENDING_AUDIT`.
