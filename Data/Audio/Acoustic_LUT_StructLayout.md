# Acoustic_LUT.bin Struct Layout

Status: PENDING VERIFICATION
Owner: SABINE_REVERB_MATRIX_GEN
Domain: DATA/AUDIO

## Binary Contract

`Data/Audio/Acoustic_LUT.bin` is a headerless raw binary LUT. It is row-major by volume index, then absorption index.

```text
format              = <ff
volumeCount         = 256
absorptionCount     = 256
recordBytes         = 8
expectedFileBytes   = 256 * 256 * 8 = 524288
fileAlignmentBytes  = 16
simdGroupFormat     = <ffff
simdGroupBytes      = 16
```

Record layout:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AcousticReverbLutEntry
{
    public float Rt60Seconds;
    public float HighFrequencyDamping;
}
```

The original XML requires `<ff`, so each logical record remains exactly two little-endian float32 values. SHINOBU can still ingest the blob in 16-byte groups because every 16-byte SIMD group contains two adjacent `<ff>` records:

```text
group[n] = record[n * 2] + record[n * 2 + 1]
```

The full file length is a 16-byte multiple. No header bytes exist in `Acoustic_LUT.bin`.

Runtime index:

```text
volume01 = saturate((log10(volumeM3) - log10(10.0)) / (log10(100000.0) - log10(10.0)))
volumeIndex = round(volume01 * 255)

absorption01 = saturate((absorption - 0.01) / (0.99 - 0.01))
absorptionIndex = round(absorption01 * 255)

recordIndex = volumeIndex * 256 + absorptionIndex
byteOffset = recordIndex * sizeof(AcousticReverbLutEntry)
```

SHINOBU `IAudioOutputJob` should receive the selected pair in its block-level parameter snapshot. Do not perform string lookup or per-sample LUT indexing in the DSP sample loop.

## Axes

Volume is log-spaced from `10.0m3` to `100000.0m3`.

Absorption is linearly spaced from `0.01` to `0.99`.

The matrix uses:

```text
RT60 = 0.161 * V / (S * alpha)
S = 6 * V^(2/3)
RT60 max clamp = 10.0 seconds
```

`S` is an equal-volume cube surface proxy. It is a deterministic acoustic fake for runtime control, not a claim of physical architectural accuracy.

## Material Presets

The baker exposes four smoke-test alpha presets, but the values are not duplicated constants. They are loaded from `Data/Audio/Acoustic_Material_Profiles.json` and written back into the manifest with profile provenance.

```text
Rock  = basalt_rock.coefficients.absorption      = 0.22
Metal = steel_hull.coefficients.absorption       = 0.10
Sand  = sand_silt.coefficients.absorption        = 0.74
Coral = coral_calcified.coefficients.absorption  = 0.48
```

The raw LUT remains generic by absorption axis; material IDs map to absorption coefficients before indexing. These presets are QA/control aliases for the four requested classes, not a claim that every rock, metal, sand, or coral surface has a universal acoustic absorption value.

## Constant Provenance

The manifest carries a `constantProvenance` block so scalar lineage is visible without adding bytes to `Acoustic_LUT.bin`.

```text
Sabine coefficient     = prompt formula authority
Volume/absorption axes = prompt dimensions
Pressure               = P0 + rho * g * depth
Water loss             = Thorp absorption at 16kHz
Amplitude retention    = Beer-Lambert in dB/km form
Mock pressure          = hydrostatic pressure at 500m test depth
```

The Thorp coefficients are named in `Tools/SabineBaker.py` and emitted under `physics.thorpCoefficients`; they are no longer anonymous literals inside the formula body.

## Damping

`HighFrequencyDamping` is a retention ratio in `[0.0, 1.0]`. `1.0` is bright, `0.0` is fully damped.

Because the prompt requires a 2D Volume/Absorption matrix but also requires pressure-based damping, the baker maps the volume row to the project depth envelope `0..1500m`. Pressure is derived, not authored:

```text
P = P0 + rho * g * depth
pressureCorrection = 1 + (P - P0) / seawaterBulkModulus
```

High-frequency seawater loss uses Thorp absorption at `16kHz`, then Beer-Lambert amplitude retention:

```text
absorptionDbPerKm = thorp(16kHz)
waterRetention = 10 ^ (-(absorptionDbPerKm * pressureCorrection * pathKm) / 20)
materialRetention = sqrt(1 - alpha)
HighFrequencyDamping = saturate(materialRetention * waterRetention)
```

`pathKm` uses the same equal-volume cube authority as the RT60 path. This keeps runtime data one raw `<ff>` record per matrix cell and avoids a third axis.

## Manifest

`Data/Audio/Acoustic_LUT.manifest.json` is a build-time audit sidecar. Runtime hot paths should not parse it.

It records:

- little-endian contract
- 16-byte file alignment
- derived physics constants
- material profile provenance
- constant provenance
- mock-room pressure contract
- FNV-1a semantic IDs
- toaster/i3 and RTX-overkill quality tier hints with `extraData`
- `PROJECT_ATLAS.md` Audio-family boundary

## Sabine Limits

Sabine assumes a diffuse field, static room boundaries, and uniform absorption. It breaks down for open water, narrow corridors, heavy occlusion, strongly directional reflections, and rooms with nonuniform material patches. HECTON-8 uses this as a stable control signal for reverb tails, then spends high-tier audio budget on early reflections, convolution tails, and binaural detail outside this binary contract.
