# Rationale 2301

Static audit only. Unity, Play Mode, builds, imports, and profiler were not run.

Decision: do not patch source in this worker. The task asks for live-proof checklist and minimal patch plan; Unity is owned by another worker. Source changes without live writer logs would risk moving ownership blindly.

Ownership call:
- `HectonAtmosphereManager` should own atmosphere state/profile resolution and shader atmosphere payloads.
- `HectonUnderwaterVisuals` should own camera-local underwater visual state, underwater fog density/color, underwater camera background, Crest underwater pass enforcement, and underwater shader globals.
- `HectonCelestialEngine` should not be the primary fog truth owner. Its current `RenderSettings.fog*` writes are surface presentation polish and late-frame readability floors. Keep only if it consumes atmosphere state and does not fight underwater state.

Proof boundary: all findings are static source/YAML/screenshot evidence. Live writer order and final values require Unity-owner frame logging.
