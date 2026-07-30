# KCC millimeter-quantization precision fix — measured proof

Date: 2026-07-30. Test: `Shinobu355_KccSmoke_100Phantoms_10000Frames_NoNanEscapeRollbackDesync`
(`Hecton8.Tests.FirstSlice`, EditMode).

## Result

| | ErrorFlags | decode | failures |
|---|---|---|---|
| before | `74` | Escape(2) \| PrecisionDrift(8) \| SdfInvalid(64) | 838500 |
| after  | `66` | Escape(2) \| SdfInvalid(64) | 743920 |

`PrecisionDrift` (bit 3) cleared. 94580 fewer failing frames. Bee rebuilt `Hecton8.Core.dll`
in the same run and the log carries zero `error CS`, so the DLL under test is the patched one.

Logs: `Logs/firstslice_results.xml` (before), `Logs/kcc_after_fix.xml` (after).

## Cause

`HydrodynamicKccMath.QuantizeMillimeter` multiplied by `(double)InvMillimeterScale`. The float
nearest `0.001` is `0.0010000000474974513`, so widening it to double carries a `+4.75e-8`
RELATIVE bias — the absolute error therefore grows with the coordinate:

| distance from AUP origin | quantization error |
|---|---|
| 1.5 km | 0.07 mm |
| 21 km | 1.00 mm (crosses the gate) |
| 99 km | 4.70 mm |

The smoke test seeds `StartAup = (99000, -1500, 99000)`, so `AdvanceDriftProbe` measured ~4.7 mm
against a 1.0 mm threshold at FRAME 1, before any physics ran. Below ~21 km it passes, which is
why this hid until the harness moved to a 99 km sector.

Fix: added `InvMillimeterScaleExact = 1.0d / 1000.0d` and used it in `QuantizeMillimeter`. The
float constant is retained for float-domain callers, where no widening occurs.

## Still open — NOT fixed by this

`ErrorFlags=66` remains. `HectonKccRuntime_SmokeTest.cs:436` raises `Escape|SdfInvalid` together
from one condition: `HostileCurrent` (:742-749) drives ±280 m/s through a 48³ × 4 m = 192 m SDF
box, crossing the 96 m half-extent in ~21 frames, and `SampleSdfStatic` (:1076-1083) returns
`-4096` for any out-of-grid sample. 743920/1000000 = 74% of frames land outside the grid.

Three non-equivalent repairs, and the choice is not the implementer's to make alone:
(a) grow the grid to cover the drive envelope, (b) bound the hostile current, (c) return
free-space for out-of-grid samples instead of treating them as escapes. Option (c) changes
collision SEMANTICS project-wide: today "no data here" is indistinguishable from "4096 m inside
rock", because negative means inside solid — a sentinel that cannot lose.
