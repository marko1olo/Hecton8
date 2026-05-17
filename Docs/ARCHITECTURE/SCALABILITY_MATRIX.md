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

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE VERIFIED / RUNTIME PENDING
