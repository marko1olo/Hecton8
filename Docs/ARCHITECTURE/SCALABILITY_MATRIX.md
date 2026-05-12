# Scalability Matrix

Date: 2026-05-12
Status: SOURCE VERIFIED / RUNTIME PENDING
Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE VERIFIED / RUNTIME PENDING
