# English Image Prompt Templates

Use English for Gemini image generation. Be specific about output type, material, perspective, lighting, and exclusions.

## Seamless PBR Albedo Texture

```text
Create ONE seamless square tileable PBR albedo texture for a premium Unity/Unreal game.
Subject: [MATERIAL SUBJECT].
Material details: [COLOR], [SURFACE FEATURES], [WEAR/EROSION], [SMALL DETAIL].
Lighting: even diffuse daylight suitable for albedo, no strong cast shadows, no directional studio lighting.
View: orthographic top-down material texture, no perspective, no horizon, no objects, no text, no logo, no UI.
Edges must tile cleanly left/right and top/bottom.
High detail suitable for 2K/4K game material.
Style: realistic, premium, not cartoon, not painterly, not low-poly, not blurry.
Output ONE square texture only.
```

## Basalt Shoreline Example

```text
Create ONE seamless square tileable PBR albedo texture for a premium Unity URP ocean survival game.
Subject: alien wet basalt shoreline rock, volcanic black-grey stone with subtle teal mineral staining, salt-water erosion, small pores, cracks, barnacle-like mineral speckles, believable natural detail.
Bright photic-shallow daylight, not dark noir, not cartoon, not painterly.
Orthographic top-down texture, no perspective, no objects, no horizon, no text, no logo, no UI.
Must tile cleanly on all edges.
High detail suitable for 2K/4K game material; realistic Subnautica-quality or better.
```

## Seam Fix Follow-Up

```text
Revise the generated texture into a TRUE production seamless square tile.
Keep the same material, but fix tileability: edges must match invisibly on left/right and top/bottom, no visible seams in a 2x2 tiled preview.
Remove large recognizable repeated hero shapes; make the pattern more isotropic and stochastic with natural mid-scale variation.
Use even diffuse daylight suitable for PBR albedo: no strong cast shadows, no directional lighting, no perspective, no horizon, no objects, no text, no UI.
Output ONE square 1024x1024 or higher tileable albedo texture only.
```

## Normal Map From Existing Texture

Attach/upload the accepted albedo/reference first.

```text
Create a seamless square OpenGL tangent-space normal map derived from the attached tileable material texture.
Preserve the same tile edges and material scale.
Encode surface relief only: cracks, pores, ridges, eroded roughness, and small material detail.
No color albedo, no lighting, no shadows, no text, no logo, no UI.
Output ONE square normal map texture suitable for Unity/Unreal material import.
```

## Roughness Map From Existing Texture

```text
Create a seamless square grayscale PBR roughness map derived from the attached tileable material texture.
Preserve exact tile edges and material scale.
White = rough/dry/matte, black = smooth/wet/glossy.
No color, no perspective, no lighting, no shadows, no text, no logo, no UI.
Output ONE square grayscale texture only.
```

## Height Map From Existing Texture

```text
Create a seamless square grayscale PBR height map derived from the attached tileable material texture.
Preserve exact tile edges and material scale.
White = raised surface, black = recessed cracks and pits.
No color, no perspective, no lighting, no shadows, no text, no logo, no UI.
Output ONE square grayscale texture only.
```

## Concept Reference Image

Use this for mood/concept, not tileable texture.

```text
Create a realistic high-detail concept reference image for [ASSET/SCENE].
Purpose: visual direction for a premium game art pass.
Include [KEY FEATURES].
Avoid [BAD FEATURES].
Lighting: [LIGHTING].
Camera: [CAMERA].
No text, no logo, no UI.
Style: realistic, production art reference, not cartoon, not painterly unless explicitly requested.
```

