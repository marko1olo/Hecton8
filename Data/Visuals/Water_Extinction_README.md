# Water Extinction LUT

Status: `OPTICS_LUT_VERIFIED`
Owner: `OPTICAL_EXTINCTION_LUT_BAKER`

## Files

- `Water_Extinction_Matrix_Toaster.bin`: raw little-endian float16 matrix,
  shape `64 x 64 x 3`, axis order `depth, turbidity, rgb`, `24576` bytes.
- `Water_Extinction_Matrix.bin`: raw little-endian float16 matrix, shape
  `256 x 256 x 3`, axis order `depth, turbidity, rgb`, `393216` bytes.
- `Water_Extinction_Matrix_Overkill.bin`: raw little-endian float16 matrix,
  shape `512 x 512 x 3`, axis order `depth, turbidity, rgb`, `1572864` bytes.
- `Water_Extinction_GradientPreview.png`: matplotlib preview of the main
  depth-vs-silt RGB transmittance.
- `Water_Extinction_GradientPreview_Overkill.png`: high-resolution preview for
  the RTX overkill variant.
- `Water_Extinction_Matrix.json`: generated manifest, hashes, formula, variants,
  alignment checks, and validation values.
- `Water_Extinction_Hecton_CoreLit_Snippet.hlsl`: reference HLSL sampling
  snippet for shader agents.

## Matrix Axes

- Depth: `0m..500m`, linear samples.
- Turbidity: `0.0..2.5`, linear samples.
- RGB: `red, green, blue`.

## Formula

```text
I = I0 * exp(-muRgb * (1 + turbidity) * depthMeters)
```

Absorption anchors:

- Red: `0.6240 m^-1`, pure-water 700nm anchor, `0.0019498552718089743`
  transmittance at 10m.
- Green: `0.0434 m^-1`, pure-water 530nm anchor.
- Blue: `0.0106 m^-1`, pure-water 470nm anchor, `0.004993438720703125`
  transmittance at 500m after half-float quantization.

The RGB coefficients are source-named pure-water absorption anchors from Robin
M. Pope and Edward S. Fry, "Absorption spectrum (380-700 nm) of pure water. II.
Integrating cavity measurements", Applied Optics 36(33), 8710-8723, 1997,
doi: `10.1364/AO.36.008710`. Silt/turbidity is applied as a separate project
multiplier, not hidden inside the pure-water constants.

Tone contract for downstream UI/log text: describe the effect as
`bilge-silt`, `pressure glass`, `floodlamp bleed`, `bulkhead shadow`, or
`rust-haze`. Do not relabel it with showroom fog terminology.

## Packing

Every payload is headerless row-major data:

```text
flatIndex = ((depthIndex * turbidityCount) + turbidityIndex) * 3 + channelIndex
```

Half-float scalar packing is little-endian Python `<e`. All `.bin` payloads are
16-byte aligned.

Preferred runtime import is RGB16F by selected tier: `64x64` toaster, `256x256`
main, or `512x512` overkill. Fallback is cold expansion to RGBAHalf with alpha
set to `1.0`. Exact raw fallback is `width*3 x height R16F`, with
`x = turbidityIndex * 3 + channelIndex` and `y = depthIndex`.

## Verification

Run:

```powershell
python Tools/OpticsBaker.py --verify
python Tools/VerifyOpticsBaker.py
```

Current decoded results:

- Main bytes: `393216`.
- Toaster bytes: `24576`.
- Overkill bytes: `1572864`.
- Red at 500m clear water: `0.0`.
- Red at 10m clear water: `0.0019498552718089743`.
- Blue at 500m clear water: `0.004993438720703125`.
- Variant binary alignment: `16` bytes.
- Local artifact FNV-1a collisions: `0`.
- Data sovereignty: `stateless_binary_lookup`.

Runtime readiness remains pending Unity import, shader sampling, profiler, and
GC evidence.
