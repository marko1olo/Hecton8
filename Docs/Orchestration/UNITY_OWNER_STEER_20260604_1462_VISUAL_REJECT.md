# Unity Owner Steer 2026-06-04 1462 Visual Reject

Target visible thread: `Продолжить работу по логам`

Use as steer only. Do not start a new Unity owner.

## Evidence Reviewed

- `Docs/Screenshots/MCP/h8_1462_surface_main.png`
- `Docs/Screenshots/MCP/h8_1462_shoreline_close.png`
- `Docs/Screenshots/MCP/h8_1462_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1462_underwater_route.png`
- `Docs/Screenshots/MCP/h8_1462_regression_low_oblique.png`
- `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.csv`

## Steer Text

1462 is progress but NOT accepted.

Evidence:
- Surface is bright now, but the island still reads as a grey striped heightfield/terrain shell. It lacks authored shoreline rock breakup, material richness, wet/dry transitions, foam/salt contact, and Subnautica-floor detail.
- White dash/debug lines are still visible on the left water/horizon band in surface and underwater captures. These must be removed from production captures unless you can prove they are intentional HUD/navigation markers with proper art.
- The small brown celestial dot reads as a primitive placeholder. Aegir is better than the old white disc, but moons/sun/secondary celestial objects still need non-primitive material and scale treatment.
- Underwater captures are still below floor: broad flat grey-green seabed, white horizontal waterline/plane band, minimal readable geology, minimal coral/flora/fauna, weak route identity. This is not close to the reference photic/shallow screenshots.
- Use `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.csv` as a static queue. It found active-scene primitive refs and placeholder/proxy material refs. Treat active scene CRITICAL rows as suspect until Unity inspection proves otherwise.

Next pass priorities:
1. Remove visible debug/white dash artifacts from captures.
2. Fix the underwater white band and flat seabed read before adding decorative clutter.
3. Replace grey terrain-shell read with authored rocky/coastal breakup and real material response.
4. Keep screenshots in `Docs/Screenshots/MCP`, not `Assets`.
5. Do not use dry-land kelp/coral or primitive scatter as a fake pass. If placed flora/rocks look like primitives, remove or regenerate properly.
6. Do not call this visually accepted until GameView and SceneView captures show surface + 0-5 m + shallow route as bright, detailed, and Subnautica-floor or better.

No request to stop your current Unity proof loop. Continue, but reject 1462 as incomplete.
