# VR Comfort Binary Layout

Owner: `VR_JERK_THRESHOLD_AUDIT`
Status: `COMFORT TUNED / PENDING RUNTIME VERIFICATION`
Evidence Class: `OFFLINE_TOOL_VALIDATED`

## File

Primary: `Data/UX/VR_Comfort_Profiles.h8bin`
Toaster: `Data/UX/VR_Comfort_Profiles_Toaster.h8bin`
RTX overkill supplement: `Data/UX/VR_Comfort_RTXOverkill.h8bin`

All game-ingested records are little-endian and 16-byte aligned.

| Record | Struct format | Size |
|---|---|---:|
| Header | `<8s14I` | 64 |
| Profile | `<II22f8I` | 128 |
| Velocity Curve | `<IIff` | 16 |
| Hash | `<IIII` | 16 |
| RTX Overkill Header | `<8s10I` | 48 |
| RTX Overkill Record | `<II6f` | 32 |

Header fields:

```text
magic[8] = H8VRCMF1
version
headerSize
profileStride
curveStride
hashStride
profileCount
curveRecordCount
hashRecordCount
profileOffset
curveOffset
hashOffset
totalBytes
payloadCrc32
flags
```

Flags:

```text
bit 0 = little endian
bit 1 = all section offsets and total byte length are 16-byte aligned
```

## Runtime Contract

Runtime consumers must treat this as immutable cold data. No JSON parsing in `Tick`, no private mutable profile copies, no hot-path hash construction. Read the profile record by precomputed FNV-1a ID, then sample the velocity LUT with fixed indexes or copied constants.

The toaster blob keeps only `6` velocity samples per profile. The RTX overkill supplement stores harmonic edge frequencies/amplitudes and gradient stops only; it must never change safety thresholds.

## Non-Applicability

Beer-Lambert, Dalton, and Sabine laws do not apply to this artifact. This file encodes psychophysical camera-comfort thresholds, not light attenuation, gas partial pressure, or acoustic reverberation.
