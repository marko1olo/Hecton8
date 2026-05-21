# Snell Refraction LUT Shader Mapping

Date: 2026-05-17
Status: `PENDING UNITY VERIFICATION`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: `SNELL_LENS_REFRACTION_LUT`
Payload: `Data/Visuals/Refraction_LUT_RGBA16F.bin`

## Scope

This document defines the SHINOBU sampling contract for the baked Snell-law
diving-mask and porthole refraction LUT. Runtime shaders sample one RGBA16F
texture instead of solving Water -> Glass -> Air refraction per pixel on MX350.

## Binary Contract

- Raw file: `Data/Visuals/Refraction_LUT_RGBA16F.bin`
- Header: none
- Scalar: IEEE 754 half-float
- Endian: little-endian, Python `struct.pack("<e", value)`
- Texture format: `R16G16B16A16_SFloat`
- Shape: `256 x 256 x 4`
- Texture dimensions: `256 x 256`
- Axis order in raw bytes: `glassCurvatureY, viewAngleX, rgba`
- Row-major flat index: `((curvatureIndex * 256) + viewAngleIndex) * 4 + channelIndex`
- Expected bytes: `256 * 256 * 4 * 2 = 524288`
- Bytes per texel: `4 * 2 = 8`
- Row stride: `256 * 8 = 2048` bytes
- Byte offset: `0`
- Manifest sentinels: `binaryLayoutSentinels` records origin, midpoint, perpendicular-tail, and tail half-float byte probes.
- FP16 audit: `validation.halfQuantization` records float32-to-half max/mean error and the derived half-ULP bound at `maxUvOffset`.
- Sample coordinate contract: `sampleUv = ((axis01 * (axisCount - 1)) + 0.5) / axisCount`, using `_H8SnellRefractionLut_TexelSize`.

Axis mapping:

- X / `viewAngleIndex`: `0..255`, maps linearly from `0..70` degrees.
- Y / `curvatureIndex`: `0..255`, maps linearly from `0..1` glass curvature.
- `viewAngleIndex == 0` is exact perpendicular incidence and stores zero in all channels.

Channel layout:

- `R`: red radial UV offset magnitude.
- `G`: green radial UV offset magnitude.
- `B`: blue radial UV offset magnitude.
- `A`: shared tangential curvature UV offset magnitude.

The RGBA layout is deliberate. Full chromatic XY vectors would require six half
channels. SHINOBU reconstructs full XY UV offsets from a radial/tangent basis:
RGB provide color-specific radial offset, A provides shared tangential curvature.

## IOR Constants

```text
Water IOR = 1.33
Glass IOR, red   = 1.497482
Glass IOR, green = 1.5000
Glass IOR, blue  = 1.506103
Air IOR = 1.00
```

Glass RGB split is a Cauchy dispersion fit, not hand-tuned color fringing:

```text
Fraunhofer C/D/F wavelengths = 656.2725 / 589.2938 / 486.1327 nm
glassAbbeNumberVd = 58.0
nFMinusNC = (glassDLineIor - airIor) / glassAbbeNumberVd
cauchyB = nFMinusNC / ((1 / fraunhoferF^2) - (1 / fraunhoferC^2))
cauchyA = glassDLineIor - cauchyB / fraunhoferD^2
n(lambda) = cauchyA + cauchyB / lambda^2
```

The baker applies two Snell interfaces:

```text
Water -> Glass -> Air
n1 * sin(theta1) = n2 * sin(theta2)
```

The glass surfaces diverge with curvature, which makes the slight glass IOR
variance visible as chromatic split. Total internal reflection is detected at
the glass-to-air interface, clamped offline, and recorded in the manifest.

## Optical Geometry

The manifest records the spherical-cap geometry used by the baker:

```text
surfaceRadius = (apertureRadius^2 + sagitta^2) / (2 * sagitta)
edgeTilt = asin(apertureRadius / surfaceRadius)
effectiveSampleUvScale = effectiveSamplePlaneOffset / viewportHalfWidth
effectiveSamplePlaneOffset = centerThickness * 0.5
maxUvOffset = apertureRadius / viewportHalfWidth
sharedTangentialScale = lensCenterThickness / apertureRadius
```

Lens dimensions:

```text
apertureRadius = 0.12 m
outerSagitta = 0.0169 m
innerSagitta = 0.0098 m
centerThickness = 0.0336 m
effectiveSamplePlaneOffset = 0.0168 m, derived from centerThickness * 0.5
viewportHalfWidth = 1.2 m
```

These lens dimensions are recorded in the manifest assumption ledger as
presentation-only authoring values because this batch prompt did not provide
production CAD. They are not claimed as runtime physics authority.

## 2D Texture Import

Preferred import path:

1. Cold-load the raw binary after exact byte-count validation.
2. Upload as a `Texture2D` with `GraphicsFormat.R16G16B16A16_SFloat`.
3. Width = `256`, height = `256`, mipmaps off, wrap clamp, bilinear filtering.
4. Treat the texture as linear data, never sRGB.
5. Never parse JSON or allocate loader buffers in gameplay hot paths.

Fallback:

- If RGBA16F texture upload fails, kill the refraction pass and use
  `float4(0, 0, 0, 0)` as the cold-load fallback. Do not solve Snell in shader
  on LOW/MX350 as a fallback.

Tier payloads:

- Minimum-budget/Celeron: `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin`, `128 x 128`, `131072` bytes.
- Low/Middle: `Data/Visuals/Refraction_LUT_RGBA16F.bin`, `256 x 256`, `524288` bytes.
- High/Ultra: `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin`, `512 x 512`, `2097152` bytes.

All three binary payloads are flat, headerless, little-endian RGBA16F and
16-byte aligned by byte count. Their row strides are `1024`, `2048`, and
`4096` bytes respectively; every row starts on a 16-byte boundary for direct
cold-load upload. Each tier binary also carries row-major byte sentinels in the
manifest so SHINOBU-side cold-load validation can catch wrong stride, endian, or
channel addressing before upload.

## SHINOBU Sampling

```hlsl
TEXTURE2D(_H8SnellRefractionLut);
SAMPLER(sampler_H8SnellRefractionLut);
float4 _H8SnellRefractionLut_TexelSize; // xy = 1/width,height; zw = width,height

float2 H8SnellLutUv(float viewAngleDegrees, float glassCurvature01)
{
    float view01 = saturate(viewAngleDegrees * rcp(70.0));
    float curvature01 = saturate(glassCurvature01);
    float2 axis01 = float2(view01, curvature01);
    return (axis01 * (_H8SnellRefractionLut_TexelSize.zw - 1.0) + 0.5)
        * _H8SnellRefractionLut_TexelSize.xy;
}

void H8SampleSnellRefraction(
    float2 screenUv,
    float2 lensCenterUv,
    float viewAngleDegrees,
    float glassCurvature01,
    out float2 uvRed,
    out float2 uvGreen,
    out float2 uvBlue)
{
    float2 radial = screenUv - lensCenterUv;
    float lenSq = max(dot(radial, radial), 1.0e-8);
    float2 radialDir = radial * rsqrt(lenSq);
    float2 tangentDir = float2(-radialDir.y, radialDir.x);

    float2 lutUv = H8SnellLutUv(viewAngleDegrees, glassCurvature01);
    half4 lut = SAMPLE_TEXTURE2D(_H8SnellRefractionLut, sampler_H8SnellRefractionLut, lutUv);

    float2 sharedTangential = tangentDir * (float)lut.a;
    uvRed = screenUv + radialDir * (float)lut.r + sharedTangential;
    uvGreen = screenUv + radialDir * (float)lut.g + sharedTangential;
    uvBlue = screenUv + radialDir * (float)lut.b + sharedTangential;
}
```

Color composition example:

```hlsl
float2 uvR;
float2 uvG;
float2 uvB;
H8SampleSnellRefraction(screenUv, _LensCenterUv, viewAngleDegrees, glassCurvature01, uvR, uvG, uvB);

half3 color;
color.r = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvR).r;
color.g = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvG).g;
color.b = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvB).b;
```

Runtime rules:

- `viewAngleDegrees` should come from cached view/glass geometry inputs.
- `glassCurvature01` should come from the mask/porthole material constant or
  a precomputed mask, not from per-pixel physical glass geometry.
- Do not call `asin`, `sin`, `tan`, or solve Snell in this post path on LOW/MX350.
- Do not sample raw `float2(view01, curvature01)`; use the texel-center
  remap above so all tier endpoints address the first/last baked samples.
- Clamp final UVs to the legal opaque-texture sampling region if SHINOBU runs
  near screen edges.

## Scalability

Low: one RGBA16F LUT sample plus three opaque-texture channel samples. No Snell
trig in shader.

Middle: same LUT with smoother material-mask blending and stronger grime masks.

High: same LUT plus richer edge chromatic intensity and porthole condensation.

Ultra: 512x512 LUT plus manifest `extraData` fields for harmonic wet-glass
noise, edge chromatic boost, micro-scratch strength, glass cracks, wet streaks,
and local noir glare. Do not inflate the Low binary contract.

## Verification

Run:

```powershell
python Tools/SnellBaker.py
python Tools/SnellBaker.py --verify
python Tools/test_snell_baker.py
python Tools/VerifySnellRefractionLut.py
```

Expected Python evidence:

- `SNELL_BAKER_STATUS: LENS BAKED`
- `bytes=524288`
- `zeroPerpendicular=True`
- total internal reflection boundary unit test passes
- generated variant byte counts: `131072`, `524288`, `2097152`
- FNV-1a output filename collision count: `0`
- half-float quantization max error stays inside the derived half-ULP bound
- binary layout sentinel bytes match raw little-endian payload offsets
- sample coordinate contract maps normalized endpoints to texel centers for all tiers

Runtime readiness remains `PENDING UNITY VERIFICATION` until Unity imports the
texture, SHINOBU samples it, and profiler/GC/visual evidence exists.
