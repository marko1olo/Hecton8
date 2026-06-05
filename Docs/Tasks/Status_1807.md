# Status_1807

Agent ID: 1807
Role: SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC
State: ACTIVE
Proof mode: STATIC_SOURCE / STATIC_DOC only until Unity implementer produces editor/runtime artifacts.

## Task Checklist

| # | Task | State | Proof Label |
|---:|---|---|---|
| 01 | Create Status_1807.md with all tasks and proof labels. | DONE | STATIC_DOC |
| 02 | Read required docs and 2-8 relevant mandates. | DONE | STATIC_DOC |
| 03 | Inspect shoreline/waterline tool and contract paths. | PENDING | STATIC_SOURCE |
| 04 | Inspect foam/water/wet-rock material YAML for texture slots, missing maps, shader names, package boundaries. | PENDING | STATIC_SOURCE |
| 05 | Inspect existing foam/waterline mesh paths and classify as candidate/active/unknown. | PENDING | STATIC_SOURCE |
| 06 | Inspect current screenshots from 1801/1802 for visual failure mode and write diagnosis. | PENDING | STATIC_DOC |
| 07 | Build bake input CSV listing material, mesh, texture, profile, and intended bake role. | PENDING | STATIC_DOC |
| 08 | Define offline bake products: foam edge masks, wet/dry masks, sediment bands, long-swell read cards, contact foam ribbons, caustic edge overlays if applicable. | PENDING | STATIC_DOC |
| 09 | Define exact editor/offline generation route using existing tools first. | PENDING | STATIC_DOC |
| 10 | Define material slot assignment plan without editing material files. | PENDING | STATIC_DOC |
| 11 | Define Compact/Middle/High/Ultra behavior for waterline richness. | PENDING | STATIC_DOC |
| 12 | Define profiler/Frame Debugger evidence needed after Unity application. | PENDING | STATIC_DOC |
| 13 | Define screenshot angles: glancing water, vertical waterline, close wet rock, wide coast, underwater edge. | PENDING | STATIC_DOC |
| 14 | Mark unsafe or rejected placeholder assets. | PENDING | STATIC_SOURCE |
| 15 | Identify missing textures/masks that require generation or user-provided art. | PENDING | STATIC_SOURCE |
| 16 | Create a Unity-slot implementer prompt. | PENDING | STATIC_DOC |
| 17 | Create an offline image/texture generation prompt if masks/textures are missing. | PENDING | STATIC_DOC |
| 18 | Append LOG_1807.md with findings and proof state. | PENDING | STATIC_DOC |
| 19 | Final scan: no fake Unity proof, no destructive instructions, no darkness-hides-it route. | PENDING | STATIC_DOC |
| 20 | Mark final state STATIC BAKE SPEC COMPLETE or BLOCKED BY MISSING STATIC TOOLING. | PENDING | STATIC_DOC |

## First-20-Minutes Route Impact

Removes the surface/coastline visual blocker for the first exit route: broad flat ocean read, weak shoreline contact foam, and weak wet basalt waterline breakup.
