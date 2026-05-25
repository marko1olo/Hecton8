# Scalability Matrix

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Authority

Runtime authority is the continuous float `HomeostasisBrain.GlobalQualityWeight` in range `0.0..1.0`.

`ScalabilityStateDTO` is a 16-byte source DTO:

| Offset | Field |
|---:|---|
| 0 | `GlobalQualityWeight` |
| 4 | `FractionalTimeSlice` |
| 8 | `VramPressure` |
| 12 | `ThermalIndex` |

Shader source currently uses `_GlobalQualityWeight` and `_H8GlobalQualityWeight` as common sinks. `_GlobalQualityParameters` is not the current source authority; if introduced later, it must be derived presentation data:

| Component | Required meaning if introduced |
|---:|---|
| `.x` | `GlobalQualityWeight` |
| `.y` | `FractionalTimeSlice` |
| `.z` | `VramPressure` |
| `.w` | `ThermalIndex` |

The float4 must not change gameplay truth ownership, DTO layout, save identity, rollback identity, or authority route.

## Rejected Pattern

Binary graphics switching is rejected. Hardware labels may select curve endpoints, but runtime fidelity must resolve through the continuous scalar.

The source enum `HectonScalabilityTier` is label vocabulary only. It must not own gameplay truth, DTO layout, save identity, or authority route.

Forbidden wording in active contracts:

- binary hardware branches;
- two-endpoint cap pairs;
- hard quality-cutoff syntax;
- two-point comparison reports, except legacy projections or sampled bands over continuous scalar.

## Continuous Scaling Rules

| Axis | `0.0` survival endpoint | `1.0` visual-overkill endpoint | Scaling rule |
|---|---|---|---|
| vertex displacement | disabled or single-axis approximation | full authored displacement | amplitude, frequency layers, and evaluation count scale by weight |
| pixel samples | 1-2 samples | authored max sample count | sample count = `round(lerp(min,max,weight))` with hysteresis |
| raymarch steps | minimum visible steps | zone-capped high steps | steps scale by weight and distance, never by gameplay truth |
| dither clipping | aggressive clip and impostor fallback | low clip, dense near-field detail | threshold uses smooth curve from weight and distance |
| simulation cadence | longer time slice for presentation-only systems | full presentation cadence | gameplay authority cadence is unchanged unless owner contract allows a visual-only cadence |
| dynamic resolution | reduced internal scale with floor | native or proved supersample ceiling | render scale derives from weight and VRAM pressure |
| telemetry | minimum ring writes | full diagnostic density | capacity/cadence may scale, entry layout may not |

## Hardware Interpretation

| Weight band | Expected behavior |
|---|---|
| `0.00..0.24` | Quest/MX350 survival path: stable frame before fidelity |
| `0.25..0.49` | low/middle path: reduced samples, early LOD, shader fakes |
| `0.50..0.74` | high path: fuller near-field presentation, guarded volumetrics |
| `0.75..1.00` | ultra path: extra density and hero-zone visuals within measured budget |

These bands are documentation labels. Runtime consumers must use the float.

## Non-Claims

Static source reads found the DTO and shader globals. No profiler, GCMonitor, Frame Debugger, Unity import, player build, or visual capture proof is linked here.
