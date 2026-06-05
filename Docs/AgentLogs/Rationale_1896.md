# Rationale 1896

Evidence rule used: static text proves source presence and semantics only. It does not prove Unity import, material assignment, visual quality, runtime cost, or screenshots.

Key decisions:

1. Accept only `_ToolScreenTex.rgb` as the live sampled texture contract because the fragment shader samples only `_ToolScreenTex`.
2. Block `_BaseMap`, `_MainTex`, and `_EmissionMap` because they are declared but not sampled.
3. Treat `ToolDiegeticDisplayController` alias binding to `_MainTex`, `_BaseMap`, and `_EmissionMap` as compatibility behavior, not channel proof.
4. Keep production screen material contract blocked because no project-owned material/prefab/scene reference to the shader GUID was found in scoped paths.
5. Permit current shader effects only where source proves them: procedural fallback scanline, heat/battery bars, visual-overkill grid/sweep, fault pulse, critical flash, and type tint.
6. Block scratches, grime, wetness, emissive glyph masks, packed screen masks, oxygen hints, sonar hints, and cockpit reuse until sampled slots, owner data, material bindings, and Unity captures exist.

Low/Middle/High/Ultra consequence:

- Low: readable owner truth and physical screen carrier; no flat emissive rectangle.
- Middle: material separation around screen plus proven readable text.
- High: richer sampled screen wear only after channel contract update.
- Ultra: layered glass/condensation/glyph detail only as presentation; no new gameplay truth.

Final state: `BLOCKED_CHANNEL_CONTRACT_REQUIRED` for production material/channel promotion. Minimal shader display-signal contract is `ACCEPTED_CHANNEL_CONTRACT_STATIC_MINIMAL`.
