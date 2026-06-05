# 2103 Coral Reef Flora Prompt Requirements

ID: 2103  
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW  
Status: STATIC VERIFIED prompt requirements only. No Gemini/browser generation was run. No image output exists from this task.

## Global Negative Prompt

No text, no labels, no watermark, no UI, no logo, no perspective camera angle, no horizon, no visible light source, no cast shadows, no baked directional lighting, no black/noir grading, no muddy haze, no blur, no cartoon finish, no toy plastic, no primitive mesh look, no flat vector pattern, no candy reef palette, no random full-surface bioluminescence, no alpha-card vegetation wall, no whole-object render when the row asks for material source.

## TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604

Basis:

- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` rank 4.
- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv` row `2004.coral.branching`.
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv` row `SRC_2004_CORAL_BRANCH_CALCITE_4K`.

Prompt:

Create ONE seamless square tileable PBR material source for shallow alien branching coral in a bright photic reef. Subject: calcified coral surface with porous cups, growth rings, chipped pale cyan and pearl mineral edges, muted coral-violet tissue stains, small sediment in pores, broken tip wear, welded-branch cavity cues, and AO-ready pore depth. It must be a realistic premium Unity URP material source, colorful but not candy, alien but physically believable. Orthographic top-down material view, even diffuse daylight, no whole coral object render, no perspective, no horizon, no text, no logo, no UI, no baked shadows, no directional highlights. Output one image only.

Output requirement:

- 4096x4096 preferred.
- Square, seamless, tileable.
- Albedo plus height-like material source only, not a final PBR stack.
- Usable later for albedo, normal, roughness, AO, and sparse localized emission derivation.
- Geometry-backed branching coral only; no flat decal final.

Reject if:

- visible seams;
- perspective coral object or whole branch silhouette;
- candy colors, random neon, dark/noir reef, painterly aquarium art;
- smooth tube coral, plastic antler coral, bouquet shape, unwelded branch cues;
- baked shadows, labels, text, logo, UI, watermark;
- blurry mush, repeated pore stamps, low-poly/proxy read;
- random glow instead of localized cavity/tip emission cues.

Future QA command after an actual output exists:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604
```

## TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604

Basis:

- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` rank 8.
- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv` rows `2004.kelp.tall.hero`, `2004.kelp.patch.filler`, `2004.kelp.canopy.silhouette`.
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv` rows `SRC_2004_KELP_BLADE_FIBER_4K`, `SRC_2004_KELP_HOLDFAST_ROOT_4K`, `SRC_2004_KELP_CANOPY_EDGE_4K`.

Prompt:

Create ONE seamless square tileable PBR material source for photic-zone kelp blade and holdfast tissue in a premium Unity URP ocean survival game. Subject: tough wet olive-teal kelp fibers, lengthwise ribs, torn blade edge grain, darker holdfast root pads, sand abrasion, small root cavities, salt and mineral speckles, subtle healed scars, restrained cyan biological traces, and wet roughness variation. It must support geometry-backed kelp with rooted holdfasts and thick blade shells, not flat alpha-card fields. Orthographic top-down material source, even diffuse daylight, no whole plant object, no perspective, no horizon, no text, no logo, no UI, no baked lighting. Output one image only.

Output requirement:

- 4096x4096 preferred.
- Square, seamless, tileable.
- Albedo plus height-like organic material source only, not a final PBR stack.
- Usable later for blade/root albedo, detail, normal, AO, roughness, sparse edge translucency/emission derivation.
- Must preserve readable bright photic kelp identity without muddy dark mass.

Reject if:

- flat ribbon wallpaper or alpha-card dependency;
- whole kelp object render, horizon, scene background, perspective;
- muddy dark mass, candy colors, random neon glow, plastic wetness;
- repeated obvious stripes, blurry mush, baked shadows;
- text, logo, labels, UI, watermark;
- no holdfast/root cue and no blade fiber/rib cue.

Future QA command after an actual output exists:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604
```

## Intake Rule

STATIC VERIFIED:

The prompts above produce source candidates only. Even a `PASS_STATIC` candidate remains PENDING VERIFICATION until manual 2x2/3x3 tile review, PBR derivation, Unity import, shader binding, in-scene captures, overdraw/profiler, and GC/memory/VRAM proof exist.
