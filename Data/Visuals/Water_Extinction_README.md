# Water Extinction LUT

Status: OPTICS CALCULATED

## Files
- `Water_Extinction_Matrix.bin`: raw little-endian float16 matrix, shape `[256, 256, 256]`, axis order `[depth][turbidity][wavelength]`, `33554432` bytes.
- `Water_Fog_Density_LUT.bin`: raw little-endian float16 fog density per meter, 0-1500m inclusive, `3008` bytes.
- `Water_Extinction_GradientPreview.png`: generated ocean vertical color preview.
- `Water_Extinction_Hecton_CoreLit_Snippet.hlsl`: HLSL sampling snippet for `Hecton_CoreLit.hlsl`.
- `Water_Extinction_Matrix.json`: machine-readable axes, source inputs, hashes, and self-audit.

## Matrix Axes
- Depth: 0-1500m, 256 linear samples.
- Turbidity: 0.0-2.5, 256 linear samples.
- Wavelength: 470-700nm, 256 linear samples.

## Packing
Upload `Water_Extinction_Matrix.bin` as a single `4096x4096 R16F` texture.
Flat index:

```hlsl
flatIndex = ((depthIndex * 256) + turbidityIndex) * 256 + wavelengthIndex;
texel = uint2(flatIndex & 4095u, flatIndex >> 12);
```

## Verification
- Red transmittance at 10m: `0.00195026`.
- Red transmittance at 500m: `0.0000`.
- Fog density at 0m / 750m / 1500m: `0.00241852` / `0.01320648` / `0.02400208`.
- Deep silt fog authority: `Assets/_Project/Data/Biomes/AtmosphereProfiles/Atmos_AbyssalSilt.asset` at `0.024000` per meter.

## Fog And Silt Inputs
- Surface fog: `Assets/_Project/Data/Atmosphere/Profile_Underwater.asset` at `0.002100` per meter.
- Representative silt: `1.26578997` from `named silt/sediment RuntimeVisualProfiles`.
- Named silt/sediment profiles scanned: `14`.
- All turbidity profiles scanned: `216`.

## Source References
- NOAA Ocean Explorer ocean-color guidance: `https://oceanexplorer.noaa.gov/ocean-fact/red-color/`.
- Pope/Fry pure-water absorption reference listing: `https://opg.optica.org/ao/issue.cfm?issue=33&volume=36`.
- GI relay visual-fake handoff read by CLI: `Docs/Archive/Batch003/AgentLogs/LOG_RENDER_GI_RELAY_SYNC.md`.

## GI Relay Contract Read By CLI
- Depth palette fake present: `True`.
- Fog globals present: `True`.
- Runtime volumetric GI rejected: `True`.
- Low-tier snap states present: `True`.
- Single cubemap path present: `True`.

## Runtime Contract
This data is a deterministic premium presentation approximation. Runtime code should sample textures; it should not recompute Beer-Lambert exponentials per pixel and should not add volumetric water-optics simulation on MX350.
