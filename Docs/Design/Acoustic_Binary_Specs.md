# Acoustic Binary Specs

Status: PENDING VERIFICATION
Owner: SOUNDSCAPE_SABINE_BAKER
Generated file: `Data/Precomputed/Reverb_LUT.bin`

## Scope

`Tools/AcousticValidator.py` bakes the dedicated reverb RT60 table required by the `AUDIO_SPATIALIZATION` path. This is separate from `sabine_reverb_rt60.bin`, which is an older `40 x 25` headerless math LUT and does not cover the required `100,000m3` upper volume range.

## Binary Layout

`Reverb_LUT.bin` is little-endian and has one fixed `256` byte header followed by one row-major `256 x 256` float32 RT60 payload.

Expected bytes:

```text
headerBytes = 256
payloadBytes = 256 * 256 * 4 = 262144
fileBytes = 262400
```

Header fields:

| Offset | Type | Meaning |
|---:|---|---|
| 0 | char[8] | magic `H8RVLUT1` |
| 8 | uint32 | version, currently `1` |
| 12 | uint32 | header bytes, `256` |
| 16 | uint32 | volume axis count, `256` |
| 20 | uint32 | absorption axis count, `256` |
| 24 | uint32 | material count, `4` |
| 28 | uint32 | damping band count, `4` |
| 32 | uint32 | payload bytes, `262144` |
| 36 | uint32 | payload CRC32 |
| 40 | float32 | minimum volume, `10.0m3` |
| 44 | float32 | maximum volume, `100000.0m3` |
| 48 | float32 | minimum absorption, `0.01` |
| 52 | float32 | maximum absorption, `0.99` |
| 56 | float32 | RT60 clamp minimum, `0.05s` |
| 60 | float32 | RT60 clamp maximum, `12.0s` |
| 64 | float32 | Sabine coefficient, `0.161` |
| 72 | float32[4] | damping frequency bands: `500, 2000, 8000, 16000Hz` |
| 88 | float32[16] | material damping ratios, row-major: Steel, Rock, Coral, Water |
| 152 | bytes[104] | reserved zero padding |

Payload indexing:

```text
rowMajorIndex = volumeIndex * 256 + absorptionIndex
payloadByteOffset = 256 + rowMajorIndex * 4
rt60Seconds = read_float32_le(payloadByteOffset)
```

The volume axis is log-spaced from `10m3` to `100000m3`. The absorption axis is linearly spaced from `0.01` to `0.99`.

Runtime index mapping:

```text
volume01 = saturate((log10(volumeM3) - log10(10.0)) / (log10(100000.0) - log10(10.0)))
volumeIndex = round(volume01 * 255)

absorption01 = saturate((absorption - 0.01) / (0.99 - 0.01))
absorptionIndex = round(absorption01 * 255)
```

Use nearest lookup for Low/Middle tiers. High/Ultra may bilerp the four nearest cells during cold parameter updates, not in the DSP sample loop.

## Equations

The RT60 payload uses the Sabine equation:

```text
RT60 = 0.161 * V / (S * a)
```

`V` is volume in cubic meters. `a` is the absorption coefficient. `S` is an equal-volume cube surface proxy:

```text
S = 6 * V^(2/3)
```

The result is clamped to `[0.05, 12.0]` seconds before being packed as little-endian float32. This is a deterministic acoustic fake for runtime control, not architectural acoustic truth.

## Material Damping

The header stores four high-frequency damping curves. Values are feedback-path retention ratios, where `1.0` means bright reflection and `0.05` means near-total high-band loss.

Material order:

```text
0 Steel
1 Rock
2 Coral
3 Water
```

The baker computes each band from:

```text
normalizedFrequency = (frequencyHz / 16000) ^ 0.65
seawaterLoss = (1 - exp(-frequencyHz / 12000)) * 0.42
totalAbsorption = baseAbsorption + hfSlope * normalizedFrequency + seawaterLoss * waterCoupling
dampingRatio = clamp(1 - totalAbsorption, 0.05, 0.98)
```

Steel uses low base absorption. Rock adds rough-boundary high-band loss. Coral adds porous organic scatter. Water uses the strongest volumetric high-frequency loss.

Pressurized air rationale: pressurized compartments preserve more high-frequency detail than open seawater because the volumetric seawater loss term is absent. Runtime readers that need dry compartment tails can bias toward the Steel/Rock rows or reduce water-coupling before a future air-specific table is approved. The current binary is underwater-tuned by design.

## Scalability

Low: one nearest-cell lookup plus a material damping row selected by acoustic semantic. Use Schroeder/zone reverb and no real-time convolution.

Middle: same lookup, denser zone updates, optional bilerp outside the DSP sample loop.

High: use saved CPU for hybrid early reflections and richer cave/metal zone sends.

Ultra: use the same RT60 authority but spend the extra budget on convolution tails and binaural detail. Do not change this binary layout during the batch.

## Validation

Run:

```powershell
python Tools/AcousticValidator.py
python Tools/AcousticValidator.py --verify-only
```

Validation checks:

- exact byte size: `262400`
- header constants and payload CRC32
- finite RT60 and damping values
- RT60 clamp range `[0.05, 12.0]`
- recursive edge cases: Small locker, Crew compartment, Pressurized corridor, Mega-Cave, Giant Void
- Mega-Cave error below `0.01%`

Expected terminal status:

```text
STATUS: ACOUSTICS BAKED
```
