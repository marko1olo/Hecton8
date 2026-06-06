# Gemini Generation Queue 2026-06-06

Status: `STATIC_OPERATOR_QUEUE`.
Evidence class: `STATIC_DOC / BROWSER_GENERATION_PREP`.

This queue is the current browser/Gemini source-generation front for assets. It does not prove generation, Unity import, material binding, SpriteAtlas packing, Addressables residency, runtime visual quality, VRAM, memory, frame time, GC, or platform readiness.

## Why This Exists

Current asset blockers need route-aware source material, not more reports or blind generation:

- shoreline foam and wet edge are active-route research first: Crest/H8 foam slots and generated foam/contact sources already exist, so do not spend Gemini runs on foam beauty maps without a named missing slot/channel role;
- wet basalt is active-route research first: WetBasalt 1428/1429, Batch31 LocalPBR, PromotionPrep, and `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt` sources already exist, so do not generate more wet basalt without material-slot proof of a concrete missing role;
- photic terrain may still need clean shell sand or receiver-support sources after existing-source review;
- Aegir/sky needs better band and storm source layers;
- oxygen HUD needs an icon/mask route that is not a misused black silhouette;
- flora/coral/kelp need better material source families before proxy promotion can be retired.

## Browser Workflow

Use Gemini in browser for generation. Codex handles repository intake, manifests, audit, and routing.

1. Open Gemini.
2. Use the prompt pack:
   `Docs/GeneratedAssets/Gemini/Prompts/Batch30/3001_GEMINI_ASSET_GENERATION_PROMPT_PACK_20260606.md`
3. Research existing assets before any Gemini prompt. Suspended rows in the CSV are not generation targets.
4. Generate in this spend order:
   `B30-05`, `B30-06`, `B30-07`, `B30-08`, `B30-09`, `B30-10`, `B30-11`, `B30-12`, then `B30-03` only if existing shell/sand sources are insufficient.
5. Save downloads into:
   `Docs/GeneratedAssets/Gemini/Outputs/Batch30/`
6. Use the queue filename pattern from:
   `Docs/GeneratedAssets/Gemini/Batch30_PRIORITY_QUEUE_20260606.csv`
7. Copy and fill:
   `Docs/GeneratedAssets/Gemini/Batch30_BROWSER_INTAKE_MANIFEST_TEMPLATE_20260606.md`
8. Run intake audit before any derivation/import decision.

## Daily Spend

If using subscription/browser generation, spend attempts like this:

- First pass: spend on B30-05 and B30-06 Aegir/sky only after checking existing Aegir/cloud sources.
- Second pass: spend on B30-07 and B30-08 oxygen HUD only after checking existing HUD sprite/mask sources.
- Third pass: spend on B30-09 and B30-10 flora/coral/kelp only after checking existing imported/procedural family maps.
- Fourth pass: spend on B30-11 or B30-12 only if existing caustic/deep-biolum sources are insufficient.
- B30-01, B30-02, and B30-04 are suspended until a specific missing slot/channel/material role is proven from live source/assets.

Stop after two same-failure attempts per target unless the next prompt names the failure exactly.

## Hard Stop Conditions

Reject the output immediately if it has:

- text, logo, watermark, frame, or visible border;
- perspective scene, object showcase, hero render, or non-orthographic material view;
- baked directional light, cast shadows, vignette, or fake preview lighting;
- obvious tile boundaries or repeated hero shapes;
- black-crush/darkness used to hide weak content;
- toy-like, candy-gradient, muddy, blurry, crayon-like, or stock-image appearance.

## Current Files

- Queue: `Docs/GeneratedAssets/Gemini/Batch30_PRIORITY_QUEUE_20260606.csv`
- Prompt pack: `Docs/GeneratedAssets/Gemini/Prompts/Batch30/3001_GEMINI_ASSET_GENERATION_PROMPT_PACK_20260606.md`
- Intake template: `Docs/GeneratedAssets/Gemini/Batch30_BROWSER_INTAKE_MANIFEST_TEMPLATE_20260606.md`
- Output path: `Docs/GeneratedAssets/Gemini/Outputs/Batch30/`
- Audit path: `Docs/GeneratedAssets/Gemini/Audit/Batch30/`

## Integration Boundary

Gemini source outputs stay under `Docs/GeneratedAssets/Gemini/**`.

Do not copy to `Assets/**`, create SpriteAtlas assets, create Addressables entries, edit `.meta`, edit materials, or bind prefabs/scenes from this queue. Those actions require owner packets and Unity proof.

Final status: `READY_FOR_BROWSER_GENERATION`.
