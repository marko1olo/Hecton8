# LUT Memory Layout

Status: PENDING VERIFICATION
Owner: OFFLINE_PRECOMPUTE_MATHEMATICIAN
Date: 2026-05-14

## Scope

`Tools/MathLUTGenerator.py` bakes raw little-endian `float32` tables into `Data/Precomputed/`.
The binary files contain no headers. Runtime readers must use constants or `math_lut_manifest.json`
for dimensions, then copy the payload into pre-sized native buffers.

No Unity C# was authored for this task.

## Files

| File | Shape | Byte Count | Row-Major Columns |
|---|---:|---:|---|
| `sabine_reverb_rt60.bin` | `40 x 25` | `4000` | `rt60Seconds` |
| `dalton_gas_toxicity.bin` | `2001 x 5` | `40020` | `ambientPressureAtm`, `oxygenPartialPressureAtm`, `nitrogenPartialPressureAtm`, `oxygenToxicity01`, `nitrogenNarcosisMultiplier` |
| `gerstner_wave_weather.bin` | `100 x 16 x 5` | `32000` | `speed`, `amplitude`, `steepness`, `directionX`, `directionZ` |
| `caustics_dispersion_offsets.bin` | `101 x 3` | `1212` | `redUvOffset`, `greenUvOffset`, `blueUvOffset` |
| `ecosystem_coefficients.json` | JSON | variable | `BirthRate`, `DeathRate`, `FeedRate`, equilibrium diagnostics |

## Binary Contract

- Scalar type: IEEE 754 `float32`.
- Endian: little-endian.
- Python packing contract: every float is written with `struct.pack("<f", value)`.
- Scalar byte size: `4`.
- Header bytes: none.
- Alignment assumption: payload can be copied into a tightly packed `float` buffer.
- Indexing: C-order row-major.
- Integrity metadata: `math_lut_manifest.json` records byte counts and SHA-256 for each binary payload and
  `ecosystem_coefficients.json`. Check hashes during cold load only; do not compute them in gameplay hot paths.

Burst-side reader contract:

1. Verify exact byte count before copying.
2. Verify SHA-256 during cold load if integrity checking is enabled.
3. Allocate the destination buffer from the project-owned data vault or cold loading system.
4. Copy only after byte-count validation passes.
5. Treat dimensions as constants from this document or `math_lut_manifest.json`.
6. Do not parse strings or JSON in a gameplay hot path.

UnsafeUtility read pattern, expressed as layout guidance only:

```text
expectedBytes = elementCount * 4
if fileBytes != expectedBytes: reject file
copy raw bytes into pre-sized float buffer
sample rowMajorIndex = ((outer * strideA) + inner) * columnCount + column
```

## Table Equations

### Sabine Reverb

`RT60 = 0.161 * V / A`

`V` is room volume in cubic meters. `A` is equivalent absorption area, computed from a cube proxy:

`A = 6 * V^(2/3) * absorptionCoefficient`

The value is clamped to `[0.05, 12.0]` seconds. This is an audio fake, not room-accurate acoustic truth.

Axis mapping:

- volume axis: `40` linearly spaced samples from `10.0` to `10000.0` cubic meters.
- absorption axis: `25` linearly spaced samples from `0.05` to `0.95`.

### Dalton Gas Toxicity

Depth samples are integer meters from `0` to `2000`.

`ambientAtm = 1 + depthMeters / 10.1325`

`oxygenPartialPressureAtm = ambientAtm * 0.2095`

`nitrogenPartialPressureAtm = ambientAtm * 0.7808`

`oxygenToxicity01 = saturate((oxygenPartialPressureAtm - 1.4) / 0.2)`

`nitrogenNarcosisMultiplier = 1 + saturate((nitrogenPartialPressureAtm - 3.2) * 0.18, max 7)`

Axis mapping:

- row index `i` is `depthMeters = i`.
- row count is `2001`.

### Gerstner Weather

There are `100` weather states from calm to hurricane. Each state has `16` waves.

Each wave stores:

`speed, amplitude, steepness, directionX, directionZ`

Direction is stored as a precomputed unit vector, not an angle, to avoid runtime `sin` and `cos`.

The weather presets are deterministic heuristic presets, not profiler-proven physical optima. The offline
generator uses a smooth calm-to-hurricane curve, low-discrepancy angular distribution, and local integer-hash
jitter from a fixed seed. Runtime/profiler verification is still required before calling them production-optimal.

Axis mapping:

- weather state `0` is calm, `99` is hurricane.
- wave index is `0..15`.

### Caustics Dispersion

Depth samples represent `0m` to `-100m` in one-meter increments.

The table stores UV offsets for red, green, and blue channels. The curve uses offline exponential
attenuation to avoid runtime `exp` in caustic chromatic dispersion logic.

Axis mapping:

- row index `i` is `depthMeters = -i`.
- row count is `101`.

### Ecosystem Coefficients

`ecosystem_coefficients.json` comes from a damped Lotka-Volterra biomass run with logistic prey capacity.

`dPrey = BirthRate * prey * (1 - prey / CarryingCapacity) - FeedRate * prey * predator`

`dPredator = PredatorConversion * FeedRate * prey * predator - DeathRate * predator`

The baker executes `1,000,000` integration steps and exports the stable prey/predator equilibrium
plus the requested `BirthRate`, `DeathRate`, and `FeedRate` constants.

## Scalability Notes

Low / Middle: sample LUTs directly and avoid transcendental functions in Burst jobs.

High / Ultra: spend saved CPU on richer visual overkill, such as more Gerstner states, higher caustic
resolution, or denser acoustic zones, without changing the runtime layout contract.

## Validation

Run:

```powershell
python Tools/MathLUTGenerator.py
python Tools/MathLUTGenerator.py --verify
python Tools/test_math_lut_generator.py
```

Expected validation status: `PASS`.

`--verify` checks existing files without regenerating them. It validates exact byte counts,
manifest scalar contract fields, and SHA-256 values for the four `.bin` payloads plus byte
count and SHA-256 metadata for `ecosystem_coefficients.json`.
