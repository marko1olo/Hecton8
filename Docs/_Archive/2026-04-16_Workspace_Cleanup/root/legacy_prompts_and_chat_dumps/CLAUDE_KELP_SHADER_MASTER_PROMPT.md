**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Claude Master Prompt - HECTON-8 Kelp Shader

You are editing a real production shader for HECTON-8, a Unity 6 URP AA game targeting NVIDIA MX350 2GB VRAM.

Your task is to improve the visual quality of the existing shader file:

`Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader`

You are not creating a brand-new experimental shader. You are upgrading the existing one in-place.

## Project Constraints

- Engine: Unity 6, URP
- Target GPU floor: NVIDIA MX350
- Visual style: NASA-Punk + Deep Sea Noir
- Scene context: underwater, many kelp instances on screen together with coral, terrain, fish, particles, decals
- Priority order:
  1. believable kelp surface
  2. stable performance
  3. compatibility with existing material authoring pipeline

## Non-Negotiable Rules

- Do not add extra passes unless absolutely required. Default expectation: keep one forward pass.
- Do not add screen-space dependencies:
  - no grab pass
  - no scene color sampling
  - no SSR
  - no expensive depth-based full-screen tricks inside the kelp shader
- Do not break GPU instancing.
- Do not require HDRP or Built-in pipeline features.
- Do not introduce runtime C# dependencies.
- Do not assume high-end desktop GPU budget.
- Do not turn kelp into glossy plastic or neon fantasy seaweed.

## Current Shader Context

The current shader already has:

- `_BaseMap`
- `_DetailMap`
- `_NormalMap`
- `_MaskMap`
- vertex color usage
- tip/base gradient
- rim lighting
- transmission
- caustic modulation
- sway in vertex stage
- midrib / edge shaping controls

Important existing properties already in the shader:

- `_BaseColor`
- `_TipColor`
- `_RimColor`
- `_TransmissionColor`
- `_Smoothness`
- `_AmbientStrength`
- `_RimPower`
- `_RimStrength`
- `_TransmissionStrength`
- `_EdgeTransmissionBoost`
- `_VertexTintStrength`
- `_AgeDarkening`
- `_MoistureBoost`
- `_DetailStrength`
- `_NormalStrength`
- `_BladeCurveNormalStrength`
- `_ThicknessStrength`
- `_SpecularNoiseStrength`
- `_MidribDarkening`
- `_MidribGlossBoost`
- `_EdgeWearDarkening`
- `_EdgeDetailBoost`
- `_CausticStrength`
- `_CausticScale`
- `_CausticSpeed`
- `_SwayAmplitude`
- `_SwayFrequency`
- `_SwaySpeed`
- `_SwayPhaseScale`

## Visual Goal

The kelp must read like real large brown kelp / laminaria family material:

- leathery but wet
- broad blades with a readable midrib
- softer transmitted light at thin edges
- slightly darker, tougher central rib
- subtle thickness variation
- non-uniform surface breakup
- mild age / moisture / wear variation
- underwater readability at both mid-distance and close range

Close-up should reveal detail without making distant kelp noisy.

## Specific Improvements Wanted

Improve the shader so it better sells:

1. **Blade anatomy**
- stronger separation of midrib vs lamina
- edges should feel thinner and more light-reactive
- central rib should feel slightly tougher / darker / glossier

2. **Surface realism**
- reduce “flat plastic sheet” look
- better broad wet-organic response
- more convincing breakup from normal/mask/detail interaction

3. **Underwater realism**
- transmission should feel like kelp tissue, not generic backlight
- caustic influence should stay restrained and believable
- rim light should support silhouette, not outline the whole blade like toon art

4. **Distance behavior**
- avoid sparkling / noisy shimmer at distance
- avoid hyper-contrast that destroys mass readability in dense kelp fields

## Performance Guardrails

Stay conservative.

- Minimize extra texture samples.
- Reuse existing maps if possible.
- Prefer math reshaping over adding new texture fetches.
- Avoid branches unless very justified.
- Avoid complex loops.
- Avoid expensive triplanar / parallax / subsurface approximations.

If you add properties, keep the count low and justify each one.

## Compatibility Guardrails

Preserve or respect the current material pipeline.

- Keep all existing properties working.
- Do not rename existing properties unless absolutely necessary.
- Do not break the current material authoring scripts that already bind these properties.
- If you add new properties, make them optional with sane defaults.

## Output Format

Return:

1. the full updated shader file
2. a short explanation of what changed
3. a flat list of any new properties added and why
4. a flat list of performance risks, if any

## Quality Bar

Bad outcome:
- prettier in isolation, heavier in scene
- stylized fantasy kelp
- plastic green strips with fake rim
- “more effects” instead of better material truth

Good outcome:
- restrained, filmic, wet-organic kelp
- better close-up realism
- still cheap enough for many instances on MX350

Work directly against the existing shader file, not a rewritten architecture.
