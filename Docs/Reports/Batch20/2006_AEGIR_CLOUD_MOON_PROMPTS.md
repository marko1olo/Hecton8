# Batch20 Worker 2006 - Aegir Cloud Moon Prompt Packet

Status: prompt and source contract packet only. No images generated. No asset imports. No Unity proof.

## Global Negative Contract

Do not generate placeholder gradients, flat circles, sticker planets, noisy sine stripes, muddy dark sky, black/noir coverups, low-detail billboards, primitive sphere reads, washed-out sun fog, fake horizon walls, or generic space art. Surface daylight must stay bright, premium, readable, and physically suggestive without requiring simulation.

All channel roles below are requested contracts for future source art. They are not claims about current files.

## Aegir Source Prompts

### SKY20_2006_AEGIR_ALBEDO_CLOUD_BANDS

Create a 4096x2048 equirectangular gas giant albedo texture for Aegir, a methane-rich blue-violet gas giant visible from HECTON-8's ocean surface. It must read as a massive atmospheric body, not a painted marble. Use layered cloud belts, broad storm lanes, subtle polar compression, deep cobalt and violet methane haze, pale turquoise upper clouds, and warm rim-lit storm highlights. Preserve premium authored detail at full resolution. No hard black space background. No rings. No moons. No text.

Output: RGB color albedo/cloud bands. Alpha unassigned.

### SKY20_2006_AEGIR_STORM_LUMA_SOURCE

Create a 4096x2048 equirectangular Aegir storm source texture designed for RGB luma sampling. Use storm cells, vortices, atmospheric rivers, and filamentary turbulence that can drive glow/detail masks by luminance. Keep values readable in RGB, not hidden in a single channel. Avoid assuming a channel contract. The result must work as a color-luma storm texture under sRGB sampling.

Output: RGB storm/glow luma source. Alpha unassigned.

### SKY20_2006_AEGIR_HORIZON_VEIL_CONTEXT

Create a 4096x2048 soft atmospheric veil support texture for Aegir horizon integration. It must help the planet blend into bright surface haze without hiding it behind fog. Include subtle limb scattering, methane haze diffusion, and non-uniform atmospheric softness. No opaque fog shelf, no grey wash, no hard sticker edge.

Output: RGB color veil/detail source. Alpha optional only if explicitly routed later.

## Surface Cloud Prompts

### SKY20_2006_SURFACE_DAY_CLOUD_PANORAMA

Create a 4096x2048 equirectangular bright alien ocean-surface sky cloud panorama for HECTON-8. The sky must support clear daylight, ocean navigation, and Aegir/moon readability. Include high-altitude cloud streaks, photic haze, coastal humidity, and layered cloud banks with depth. Keep horizon readable and premium. Do not make storm-night art. Do not darken the surface to hide weak cloud detail.

Output: RGB cloud color/source panorama. Alpha unassigned.

### SKY20_2006_CLOUD_DENSITY_EROSION_SOURCE

Create a 2048x2048 tileable cloud density and erosion source for atlas packing. It must contain large readable cloud masses, medium breakup, and fine erosion detail without white noise. The texture should survive mipmapping and compression. No flat Perlin mush, no binary blobs, no text, no stars.

Output: RGB grayscale-compatible source. Channel packing must be performed by the atlas tool, not by the image generator unless a final channel contract is supplied.

### SKY20_2006_HIGH_CLOUD_WISPS

Create a 2048x2048 tileable high-cloud source with thin streaks, cirrus-like alien wisps, and layered directional flow. It must add premium sky motion at low cost. Avoid heavy storm shelves, black clouds, and repeating obvious swirls.

Output: RGB high-cloud source. Alpha unassigned.

## Moon Source Prompts

### SKY20_2006_MOON_ALBEDO_SET

Create six distinct 2048x2048 moon albedo textures for HECTON-8's visible moons: Ione, Khepri, Nammu, Pelagia, Thalos, and Varda. Each must be moon-specific, not reused terrain rock. Show crater fields, mare-like basins, ice or mineral tint variation, terminator-readable macro shapes, and silhouettes that remain clear under bright surface sky. Keep color restrained enough for celestial scale but not muddy.

Output: RGB albedo per moon. Alpha unassigned.

### SKY20_2006_MOON_NORMAL_HEIGHT_FUTURE_ROUTE

Create optional 2048x2048 moon relief source maps for the six moons. These are future-route assets only because the current moon shader has no normal/height texture slots. Relief must support crater rims, basin ridges, ejecta, and readable limb shape without noisy terrain tiling.

Output: pending future shader contract. Do not pack channels until the shader/material route is defined.

### SKY20_2006_MOON_PHASE_TERMINATOR_FUTURE_ROUTE

Create optional terminator softness/phase support sources for six moons. These are future-route assets only because current phase is shader procedural, not texture-slot driven. The images must guide soft terminator falloff and Aegir fill readability without baking a single fixed phase.

Output: pending future shader contract. No current active slot.

## Horizon And Ocean Relation Prompts

### SKY20_2006_HORIZON_VEIL_MASK_SOURCE

Create a 2048x1024 source texture for bright ocean horizon atmospheric veil. It must preserve coastline and waterline readability while adding distance haze. No fog wall, no black vignette, no storm-only coverup, no flat gradient.

Output: RGB source unless a final mask channel contract is supplied.

### SKY20_2006_PLANET_SHINE_WATER_CONTEXT

Create visual reference source for Aegir and moon shine interacting with a bright ocean surface: soft color cues, subtle water glints, and readable horizon tint. It must not become a night scene. Keep surface daylight, navigation contrast, and photic water clarity.

Output: reference art only unless later converted into authored texture slots.

## Import And Validation Requirements

- Any numeric mask must be imported Linear. Any color-luma source may remain sRGB if the shader explicitly consumes luma from color.
- Mipmaps are mandatory for sky, cloud, and celestial textures.
- Compression must be selected per platform after artifact inspection; do not assume current enum meaning from YAML alone.
- Generated assets must include a written role: albedo, luma storm source, density, erosion, high-cloud source, base moon albedo, future relief, or future terminator. No unlabeled "cool texture" imports.
- Any source route that requires dark surface exposure to look acceptable is rejected.
