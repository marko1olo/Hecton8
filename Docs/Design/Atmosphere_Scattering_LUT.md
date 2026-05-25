# Atmosphere Scattering LUT

Date: 2026-05-17
Status: PENDING VERIFICATION  

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

Owner: ORBITAL_ATMOSPHERE_MATHEMATICIAN  
Evidence class: STATIC_SOURCE / PY_CLI only until Unity import, shader binding, visual capture, profiler, and GCMonitor exist.

## Scope

`Tools/AtmoPreview.py` bakes atmosphere presentation data for the space prologue. This is a visual-realistic fake, not a physical atmosphere simulator. Runtime should sample the baked payloads during rendering instead of integrating Rayleigh/Mie scattering per pixel on MX350.

## Files

Output directory: `Data/Precomputed/Atmosphere/`

| File | Shape | Format | Byte Count | Purpose |
|---|---:|---|---:|---|
| `atmosphere_density_matrix_rgba16f.bin` | `128 x 4` | raw little-endian half float | `1024` | altitude km, Rayleigh density, Mie density, absorption |
| `atmosphere_sky_gradient_rgba16f.bin` | `128 x 256 x 4` | raw little-endian half float | `262144` | linear RGB sky color plus mean transmittance |
| `atmosphere_sky_gradient_preview.png` | `256 x 128` | PNG RGB8 | variable | Python verification preview |
| `atmosphere_lut_manifest.json` | JSON | UTF-8 | variable | dimensions, scalar contract, hashes, gradient audit |

Binary contract:

- Scalar type: IEEE 754 `float16`.
- Endian: little-endian.
- Python packing contract: `struct.pack("<e", value)`.
- Header bytes: none.
- Indexing: C-order row-major.
- Runtime JSON parsing is cold-load only. Do not parse manifest data in a gameplay hot path.

## Rayleigh And Mie Formulas

Rayleigh coefficient per wavelength:

```text
betaR(lambda) =
  8 * pi^3 * (n^2 - 1)^2 * kingCorrection /
  (3 * molecularDensity * lambda^4)
```

Mie coefficient per wavelength:

```text
betaM(lambda) = referenceBeta * turbidity * (lambda / 550nm)^(-angstromAlpha)
```

Phase functions:

```text
RayleighPhase(mu) = 3 / (16 * pi) * (1 + mu^2)

MiePhase(mu, g) =
  3 / (8 * pi) *
  ((1 - g^2) * (1 + mu^2)) /
  ((2 + g^2) * (1 + g^2 - 2*g*mu)^(3/2))
```

The LUT uses these formulas as the base shape, then applies bounded art-direction terms for golden-hour warmth, horizon haze, and space fade. That is deliberate: the target is believable NASA-punk sky composition, not atmospheric science purity.

## Altitude Density Matrix

Rows are uniformly spaced from `0 km` to `100 km`:

```text
altitudeKm = row / 127 * 100
rayleighDensity = exp(-altitudeMeters / 8000)
mieDensity      = exp(-altitudeMeters / 1200)
absorption01    = 1 - exp(-(rayleighColumn * betaR_luma + mieColumn * betaM_luma + ozoneTerm))
```

The sky-gradient LUT uses a smoothstep vertical remap for presentation to remove the surface-to-space line. The density matrix remains uniform for deterministic sampling.

## Sunset To Void Black

The 2D LUT axis mapping is:

- X: sun elevation from `-8` to `82` degrees.
- Y: visual altitude from ocean horizon to `100 km` atmosphere edge.

Gradient audit checks:

- all samples finite
- maximum adjacent RGB delta <= `0.115`
- surface seam RGB delta <= `0.030`
- dark-space luminance <= `0.040`
- golden-hour horizon luminance >= `0.065`

If the seam threshold fails, the density/altitude curve must be adjusted before the manifest is accepted.

## Planet Curvature Fake

The space shader can make a `5000 m` mesh read as planetary by remapping distance logarithmically and applying a bounded horizon drop:

```text
remap = saturate(log2(1 + distanceMeters * 0.0022) / log2(1 + 5000 * 0.0022))

horizonDropMeters =
  (distanceMeters^2 / (2 * 280000)) *
  (0.25 + 0.75 * remap)
```

This is the Relativity Fake: the viewer gets a planet-scale curvature cue without planet-scale geometry, floating-origin stress, or real orbital depth.

## Scalability

Low: sample the half-float sky LUT directly. Use baked color and mean transmittance only.

Middle: blend this LUT with weather/turbidity scalars at cold or slow cadence.

High: keep the same LUT and add richer exposure response, stars, and higher-quality sun shafts.

Ultra: spend saved shader ALU on visual overkill: denser shafting, higher-res sky composition, and cinematic exposure transitions. The baseline binary layout does not change.

## Validation

Run:

```powershell
python Tools/AtmoPreview.py
python Tools/AtmoPreview.py --verify
python Tools/test_atmo_preview.py
```

Expected Python validation status: `PASS`.

`--verify` checks existing files without regenerating them. It validates exact byte counts, manifest scalar contract fields, SHA-256 values, and decoded half-float payload semantics.

Decoded payload checks:

- sky LUT is decoded from the written RGBA16F binary and re-audited for seam, adjacent gradient, void black, golden-hour luminance, and finite samples.
- density matrix is decoded from the written RGBA16F binary and checked for `128` rows, `0 km` to `100 km` altitude bounds, finite values, monotonic density falloff, and absorption range.
- a test corrupts the sky binary to contain `inf`, updates the manifest hash to match, and verifies that semantic validation still fails.

Runtime readiness remains PENDING VERIFICATION until Unity imports these files, the shader samples them, and GC/profiler/visual evidence exists.
