# Organic Entropy Regrowth SHINOBU Contract

Status: PENDING_UNITY_VERIFICATION

## Binary

Path: `Data/Ecosystem/Organic_Entropy_Regrowth.h8bin`
Magic: `H8OR`
Version: `2`
Endianness: little-endian only (`<`)
Alignment: every section offset and total byte size must be 16-byte aligned.

Header layout: `<4s19I`, 80 bytes.

Fields:

1. magic
2. version
3. endian probe `0x01020304`
4. flags
5. day count
6. grid width
7. grid height
8. biome count
9. curve record count
10. final cell count
11. apex respawn LUT bytes
12. biome record stride
13. day record stride
14. cell record stride
15. biome offset
16. day curve offset
17. apex respawn LUT offset
18. final cell offset
19. schema hash
20. total bytes

## Runtime Use

Low/i3: sample every tenth day and use final-cell records as static scatter weights.
Middle: sample every fifth day and drive cheap detritus tint.
High: sample every second day and drive layered flora tint/sway.
Ultra: sample every day and use per-record visual hashes for harmonic bloom, overgrowth scars, and biolum residue.

## Math

Nutrient diffusion is derived from Fickian macro eddy diffusion:

`nutrientDiffusionPermille = round(K * secondsPerDay / (cellSizeMeters^2) * 1000)`

With `K = 0.6674962962962963 m2/s`, `secondsPerDay = 86400`, `cellSizeMeters = 512`, the quantized daily diffusion is `220 permille`.

Organic growth uses Q10 temperature scaling as the biological rate basis, then quantizes to byte lanes for SHINOBU ingest. Nutrient accounting is macro detritus/nitrate debt, not particle chemistry. Redfield C:N:P is fixed at `106:16:1` for nutrient basis metadata.
