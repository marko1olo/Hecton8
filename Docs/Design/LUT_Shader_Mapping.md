# LUT Shader Mapping

Status: `PENDING UNITY VERIFICATION`
Owner: `OPTICAL_EXTINCTION_LUT_BAKER`
Payload: `Data/Visuals/Water_Extinction_Matrix.bin`

## Scope

This document defines the SHINOBU sampling contract for the baked Beer-Lambert
water extinction LUT. Runtime shaders must sample this 2D texture data instead
of evaluating `exp(-mu * depth)` per pixel on MX350.

## Binary Contract

- Header: none.
- Scalar: IEEE 754 half-float.
- Endian: little-endian, Python `struct.pack("<e", value)`.
- Alignment: every `.bin` payload is 16-byte aligned.
- Axis order: `depth, turbidity, rgb`.
- Row-major flat index:

```text
((depthIndex * turbidityCount) + turbidityIndex) * 3 + channelIndex
```

Axis mapping:

- `depthIndex`: maps linearly from `0m..500m`.
- `turbidityIndex`: maps linearly from `0.0..2.5`.
- `channelIndex`: `0=red`, `1=green`, `2=blue`.

Formula used by the baker:

```text
I = I0 * exp(-muRgb * (1 + turbidity) * depthMeters)
```

Coefficient anchors:

```text
red   = 0.6240 m^-1  -> pure-water 700nm anchor, 0.195% at 10m
green = 0.0434 m^-1  -> pure-water 530nm anchor
blue  = 0.0106 m^-1  -> pure-water 470nm anchor, survives at 500m
```

These are source-named pure-water absorption anchors from Robin M. Pope and
Edward S. Fry, "Absorption spectrum (380-700 nm) of pure water. II.
Integrating cavity measurements", Applied Optics 36(33), 8710-8723, 1997,
doi: `10.1364/AO.36.008710`. The turbidity axis is the project silt multiplier
layered on top of pure water.

## Variant Payloads

| Variant | File | Shape | Bytes | Purpose |
|---|---|---:|---:|---|
| `toaster_i3` | `Water_Extinction_Matrix_Toaster.bin` | `64 x 64 x 3` | `24576` | stripped-down Celeron/i3 lookup |
| `main_mx350` | `Water_Extinction_Matrix.bin` | `256 x 256 x 3` | `393216` | required LOW/MX350 contract |
| `rtx_overkill` | `Water_Extinction_Matrix_Overkill.bin` | `512 x 512 x 3` | `1572864` | high-end dense blend path |

All three files are raw little-endian `<e` half-float arrays and are 16-byte
aligned. The manifest stores FNV-1a artifact IDs and verifies local collisions
as zero.

## 2D Texture Import

Preferred import path:

1. Cold-load the binary after byte-count validation.
2. Pick one of the three variant files by hardware tier.
3. If the platform supports `R16G16B16_SFloat` sampling, upload the payload
   directly as an RGB half texture using the variant dimensions.
4. If RGB16F sampling is unsupported, cold-expand to `R16G16B16A16_SFloat`
   with alpha set to `1.0`.
5. Never parse JSON or allocate loader buffers in gameplay hot paths.

Fallback exact-raw path:

- Upload as `R16_SFloat`, `width*3 x height`.
- Texel mapping: `x = turbidityIndex * 3 + channelIndex`, `y = depthIndex`.
- Use point `Load` and manual interpolation. Do not rely on bilinear filtering
  across the interleaved RGB lanes.

## Shader Sampling

For the preferred RGB/RGBA half texture:

```hlsl
float2 H8WaterExtinctionUv(float depthMeters, float turbidity)
{
    float depth01 = saturate(depthMeters * rcp(500.0));
    float turbidity01 = saturate(turbidity * rcp(2.5));
    return float2(turbidity01, depth01);
}

half3 H8SampleWaterExtinction(float depthMeters, float turbidity)
{
    float2 uv = H8WaterExtinctionUv(depthMeters, turbidity);
    return SAMPLE_TEXTURE2D_LOD(_H8WaterExtinctionLut, sampler_H8WaterExtinctionLut, uv, 0).rgb;
}
```

Texture states:

- Filter: bilinear for the preferred RGB/RGBA texture.
- Wrap: clamp.
- Mips: off.
- Color space: linear data texture, never sRGB.

## Runtime Rules

- The shader consumes transmittance directly: `litColor.rgb *= extinction.rgb`.
- Do not call `exp`, `pow`, or spectral wavelength loops for this extinction
  path on LOW/MX350.
- Turbidity must come from a cached biome/weather scalar, not from per-particle
  sediment simulation.
- Missing/invalid LUT fallback: use `half3(1.0, 1.0, 1.0)` and log a cold-load
  validation error. Do not crash gameplay.

## Scalability

Toaster/Low: `64x64x3` or `256x256x3`, one RGB/RGBA 2D texture sample. No
runtime exponentials.

Middle: same LUT family, smoother biome/weather turbidity blending at slow cadence.

High: `512x512x3` overkill payload plus richer fog strata, light shafts, and
biolum response.

Ultra: `512x512x3` overkill payload plus manifest harmonic-noise bands for dense
light-shaft shimmer. The extra harmonic data is presentation-only; the
Beer-Lambert transmittance remains the hard data source.

Lore-facing labels must stay industrial: `bilge-silt`, `pressure glass`,
`floodlamp bleed`, `bulkhead shadow`, and `rust-haze`. Do not rename this path
with showroom fog terminology in UI, debug panels, or shader comments.

## Verification

Run:

```powershell
python Tools/OpticsBaker.py
python Tools/OpticsBaker.py --verify
python Tools/VerifyOpticsBaker.py
```

Expected:

- `OPTICS_BAKER_STATUS: PASS`
- `bytes=393216`
- `aligned16=True`
- `redAt500mClear=0.0`
- `blueAt500mClear` greater than `0.0`
- `fnvArtifactIds=6 collisions=0`

Runtime readiness remains `PENDING UNITY VERIFICATION` until Unity imports the
texture, SHINOBU samples it in shader, and profiler/GC evidence exists.
