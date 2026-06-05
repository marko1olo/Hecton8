# Gemini Browser Image Asset Workflow

Purpose: practical operator protocol for using the already logged-in Edge/Gemini browser session to generate production candidate textures and visual source images for HECTON-8.

This is not a replacement for Unity proof. Generated images are source assets until imported, assigned, and checked in Unity against `TASTE.md`, `VISION_LOCKS.md`, and the relevant domain bible.

## What Was Proven

- Edge can be controlled through normal GUI actions: focus browser, click Gemini prompt field, paste English prompts, send, wait, right-click/download images.
- The current Edge session is already logged into Google accounts and can use Gemini image generation.
- Account switching works from the lower-left Gemini account avatar. If a generation limit appears, open the account menu and select another available account. No password is exposed in this flow.
- The current Edge process is not running with a Chrome DevTools Protocol endpoint on `9222/9223`. Standard UIAutomation also exposes too little of the Gemini DOM. Reliable background/DOM control requires launching a separate browser profile with `--remote-debugging-port` in advance, but that separate profile may not share the logged-in accounts.
- Practical fallback is GUI automation plus screenshots. It works.
- Gemini can produce usable seamless texture candidates when the prompt explicitly says `seamless`, `tileable`, `square`, and defines the material/channel.

## Hard Rules

- Do not save generated images directly into `Assets` until they are accepted. Save intake files under:
  - `Docs/GeneratedAssets/Gemini/`
  - `Docs/GeneratedAssets/Gemini/QA/`
- Do not save MCP/browser screenshots into `Assets/Screenshots`; that can trigger Unity import/rebuild loops. Use:
  - `Docs/Screenshots/`
  - `Docs/Orchestration/Captures/`
- Do not expose account names/emails in chat reports. It is fine to switch accounts when the user permits it, but final reports should say only that account switching worked.
- Do not accept a generated image just because it looks nice. For textures, check tiling and material usefulness.
- Do not import duplicate downloads. Keep one canonical source file, delete or archive duplicates.
- Do not treat generated albedo as a full PBR material. Normal, roughness, AO, height, masks, and Unity import settings still need real handling.

## Gemini UI Workflow

1. Open or focus:
   - `https://gemini.google.com/app`
2. If the page is in template mode, use the prompt field at the bottom.
3. If Gemini does not enter image-generation mode, click the `+` button near the prompt field and choose image creation.
4. Paste an English production prompt.
5. Send with the visible send arrow or normal Enter if the input is focused.
6. Wait until the generated image appears.
7. Download the image:
   - preferred: use the image overlay download button if visible;
   - fallback: right-click image -> `Save image as`.
8. Save to `Docs/GeneratedAssets/Gemini/` first.
9. Rename with project naming, for example:
   - `TX_H8_WetBasaltShoreline_Albedo_1428.png`
   - `TX_H8_PaleReefSand_Albedo_1428.png`
   - `TX_H8_ShallowFoamNoise_Mask_1428.png`
10. After QA, move accepted textures to the relevant Unity folder, for example:
    - `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/`
    - `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/`
    - `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/<family>/`
    - `Assets/_Project/Art/TEXTURES/Sky/`
    - `Assets/_Project/Art/TEXTURES/Weather/`

## Account Switching

Use only when Gemini reports generation limits or when the user explicitly asks.

1. Click the lower-left Gemini account avatar.
2. Select another available account from the list.
3. Wait for Gemini to reload. The URL may change to a `/u/<number>/...` route.
4. If a Google/Gemini information popup appears, close it.
5. Continue generation.

Do not write account identifiers into logs or reports. For controller proof, write: `Account switching tested: Gemini reloaded under another account and prompt field remained usable.`

## Texture Prompt Requirements

Always prompt in English. Include:

- `ONE seamless square tileable texture`
- exact PBR channel: `albedo`, `normal map`, `roughness map`, `height map`, `AO map`, or `mask`
- material subject and scale
- `orthographic top-down texture`
- `no perspective`
- `no horizon`
- `no object`, unless making an atlas/reference image
- `no text, no logo, no UI`
- `edges must tile cleanly`
- lighting constraints for albedo: `even diffuse daylight`, `no strong cast shadows`
- style rejection: `not cartoon`, `not painterly`, `not low-poly`, `not blurry`, `not dark noir`
- HECTON-8 target: bright photic-shallow, Subnautica-level or better, premium realistic game material

## Basalt Albedo Prompt Template

```text
Create ONE seamless square tileable PBR albedo texture for a premium Unity URP ocean survival game.
Subject: alien wet basalt shoreline rock, volcanic black-grey stone with subtle teal mineral staining, salt-water erosion, small pores, cracks, barnacle-like mineral speckles, believable natural detail.
Bright photic-shallow daylight, not dark noir, not cartoon, not painterly.
Orthographic top-down texture, no perspective, no objects, no horizon, no text, no logo, no UI.
Must tile cleanly on all edges.
High detail suitable for 2K/4K game material; realistic Subnautica-quality or better.
```

## Seam Fix Follow-Up Template

Use only if the first result has visible repeat seams or too many hero shapes.

```text
Revise the generated texture into a TRUE production seamless square tile.
Keep the same material, but fix tileability: edges must match invisibly on left/right and top/bottom, no visible seams in a 2x2 tiled preview.
Remove large recognizable repeated hero shapes; make the pattern more isotropic and stochastic with natural mid-scale variation.
Use even diffuse daylight suitable for PBR albedo: no strong cast shadows, no directional lighting, no perspective, no horizon, no objects, no text, no UI.
Output ONE square 1024x1024 or higher tileable albedo texture only.
```

## Normal Map Prompt Template

Use after uploading or attaching the accepted albedo/reference image.

```text
Create a seamless square OpenGL tangent-space normal map derived from the attached tileable rock albedo.
Preserve the same tile edges and material scale.
Encode surface relief only: cracks, pores, mineral ridges, eroded wet basalt roughness.
No color albedo, no lighting, no shadows, no text, no UI.
Output ONE square normal map texture suitable for Unity terrain material import.
```

## Roughness / AO / Height Prompt Template

Use after uploading or attaching the accepted albedo/reference image.

```text
Create a seamless square grayscale PBR <ROUGHNESS/AO/HEIGHT> map derived from the attached tileable rock albedo.
Preserve exact tile edges and material scale.
No color, no perspective, no lighting, no text, no UI.
Output ONE square grayscale texture only.
```

For roughness, wet basalt should not be uniformly glossy. Use varied roughness: wet cracks and mineral stains darker/smoother, dry raised stone lighter/rougher.

## QA

Minimum checks before import:

- Open the image in a seamless texture checker, for example `https://iliad.ai/seamless-texture-checker`.
- Also create a local 2x2 preview if needed.
- Reject or re-prompt if:
  - hard seams are visible;
  - the image contains perspective, horizon, objects, labels, UI, text, or logos;
  - the texture reads as a single unique illustration instead of a reusable material;
  - the texture is too blurry for close terrain/coastline use;
  - it is dark/noir when intended for surface or photic shallows;
  - it repeats too obviously at the intended Unity UV scale.

Repeating large forms are not automatically fatal if the texture will be blended with macro variation, decals, vertex color masks, slope masks, terrain splats, or secondary detail normals. They are fatal on large flat unbroken surfaces.

## Unity Import Route

Accepted example from the browser test:

- Source downloaded from Gemini:
  - `Docs/GeneratedAssets/Gemini/...`
- Canonical Unity texture:
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`

After import, a Unity owner must:

- set texture type/import settings correctly;
- assign it to the intended material or TerrainLayer;
- generate/assign missing PBR channels;
- capture Game View and Scene View proof;
- compare surface/coastline/photic-shallow result against required reference images.

## How To Command Codex To Do This

Use this prompt when assigning an agent to manual Gemini image generation:

```text
You are the HECTON-8 art-source production operator.
Read AGENTS.md, TASTE.md, VISION_LOCKS.md, PROJECT_BIBLES.md, quality.md, 3dmodel.md, 3DMODEL_TEXTURES_MATERIALS.md, 3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md, and the specific domain bible for the target asset.
Use the already open Edge/Gemini browser session to generate production candidate image assets manually.
For Gemini prompts, write in English and explicitly require seamless/tileable square PBR channel output, orthographic material view, no perspective, no text/logo/UI, and Subnautica-level or better realism for bright surface/photic-shallow HECTON-8 visuals.
If Gemini reports a limit, switch accounts from the lower-left account menu. Do not expose account names or emails in reports.
Download generated images to Docs/GeneratedAssets/Gemini first, not Assets.
Run visual QA with a seamless texture checker or local 2x2 preview.
Move only accepted canonical files into the relevant Assets/_Project/Art/TEXTURES folder with project naming.
Delete duplicate downloads.
Report exact files created/moved, QA result, and remaining Unity integration work.
Do not claim final game quality until the texture is imported, assigned, and proven in Unity screenshots.
```

Use this prompt when assigning an agent to manual orchestration:

```text
You are the local Codex orchestrator for HECTON-8.
Read AGENTS.md, HECTON8_ORCHESTRATOR.md, HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md, TASTE.md, VISION_LOCKS.md, PROJECT_BIBLES.md, and the domain bible for each task wave.
You must coordinate multiple agents manually through the Codex UI, keep their tasks independent, avoid Unity file-race conflicts, monitor completion reports, and give follow-up prompts only when needed.
Keep at least several agents working when there is safe parallel work, but do not assign overlapping Unity scene/import/compiler ownership.
For Unity work, designate one Unity owner at a time.
For art-source generation, use Gemini through the browser workflow in Docs/Orchestration/GEMINI_BROWSER_IMAGE_ASSET_WORKFLOW.md.
Track what was actually changed, what is only a report, what is blocked, and what still needs proof.
Never claim visual success without screenshots or direct Unity evidence.
```

