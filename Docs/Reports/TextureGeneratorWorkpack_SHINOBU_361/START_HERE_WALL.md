# START HERE - WALL BASE ROUND 3

This is the only active wall file.

## Verdict On Current 4 Images

Saved candidates:

- `C:\hades\Hecton8\Docs\ArtDrop\SHINOBU_361\LayeredWallSystem\WALL_LAYER_001_BasePressureSkin\CANDIDATE_WALL_LAYER_001_A.png`
- `C:\hades\Hecton8\Docs\ArtDrop\SHINOBU_361\LayeredWallSystem\WALL_LAYER_002_ServiceConduitOverlay\CANDIDATE_WALL_LAYER_002_A.png`
- `C:\hades\Hecton8\Docs\ArtDrop\SHINOBU_361\LayeredWallSystem\WALL_LAYER_003_InstrumentAttachmentKit\CANDIDATE_WALL_LAYER_003_A.png`
- `C:\hades\Hecton8\Docs\ArtDrop\SHINOBU_361\LayeredWallSystem\WALL_LAYER_004_WallTrimHeight\CANDIDATE_WALL_LAYER_004_A.png`

Do not move them into `Assets`.

Keep only as temporary evidence:

- Pipes: good service idea, but too illustrated and too black-lined.
- Instruments: good object vocabulary, but too product-render and too shadowed.

Reject as final:

- Base wall: still a rounded rectangle panel grid.
- Height/normal source: inherits the rejected grid.

## Do Now

Generate only the base wall again. Do not generate pipes, instruments, normal, ORM, or Unity assets until this passes.

Save 3 variants here:

`C:\hades\Hecton8\Docs\ArtDrop\SHINOBU_361\LayeredWallSystem\WALL_LAYER_001_BasePressureSkin`

Names:

- `CANDIDATE_WALL_LAYER_001_C01.png`
- `CANDIDATE_WALL_LAYER_001_C02.png`
- `CANDIDATE_WALL_LAYER_001_C03.png`

Do not use the rejected base or rejected normal image as references. If the generator requires a reference, use no image reference for this pass.

## Prompt 1D - Base Wall

```text
Create a seamless albedo source texture for one large continuous HECTON-8 abyssal habitat interior wall pressure skin.

This must be a monolithic cast pressure-shell surface, not modular panels, not tiles, not a hatch sheet, not corridor wallpaper, not a spaceship wall panel grid. It should feel like a large engineered wall substrate several meters wide before separate pipes, rails, tools, decals, sensors, and service boxes are installed on top.

Composition: 90 to 95 percent uninterrupted warm off-white ceramic-composite wall material. Use broad calm fields first. Add only two or three long partial structural seams near the edges or crossing off-center; the seams must be open-ended, non-closed, non-repeating, and must not connect into rectangles. Add faint manufacturing stress bands, subtle molded pressure transitions, tiny ceramic pores, very shallow embedded titanium reinforcement hairlines, and a few isolated screw wells. No centered module. No repeated cells. No visible grid.

Material taste: premium subsea industrial NASA-punk, slightly darker and heavier than Subnautica but clean, maintained, and expensive. Warm ivory/off-white composite, rare satin titanium slivers, extremely thin graphite gasket traces, pale mineral dust only in a few lower recesses, light polished maintenance wear, micro scratches. No horror grime, no rust junk, no military paint.

Texture-source discipline: this is albedo only. No directional lighting, no cast shadows, no ambient-occlusion blobs, no black ink outlines, no comic rendering, no product-render bevel shading. Details must be material color and subtle surface staining only, not fake lighting.

Strict avoid list: no bathroom tile, no square panels, no rounded rectangle panels, no repeated panel cells, no closed rectangular seam loops, no centered hatch, no orange glow bars, no warning labels, no fake letters, no numbers, no logos, no symbols, no border, no watermark, no perspective scene.

Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, perfect seamless tiling.
```

## Pass Gate

Pass only if:

- from far away it reads as one continuous pressure-wall skin
- the eye cannot find a rectangle grid
- there is enough calm surface to place pipes and instruments later
- the material is beautiful, expensive, subsea-industrial, and not grimdark

Fail if:

- it is just cleaner bathroom tile
- it repeats rounded panels
- seams form rectangles
- it looks like an illustrated concept wall instead of albedo source
- it contains black outline art or fake shadows

## Freeze

Everything else waits until the base wall passes.
