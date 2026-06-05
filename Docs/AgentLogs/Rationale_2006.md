# Rationale 2006

The packet is static because task constraints forbade Unity, GUI/browser generation, and Assets edits. Runtime visual claims are therefore marked PENDING VERIFICATION.

Main decisions:

- Treated `Aegir_storms.png` as an RGB/luma source only. The shader samples RGB luma and no source file proved channel-specific semantics.
- Flagged the prologue Aegir prefab as a primitive-route risk because it uses Unity built-in sphere mesh GUID `0000000000000000e000000000000000`.
- Flagged `Sky_System.prefab` as a primitive-route risk for the same built-in sphere GUID.
- Marked sky atlas ownership unresolved because `HectonSkyTools.cs` and `HectonSkyAtlasGenerator.cs` write the same output path with different documented flow semantics.
- Marked SceneView/Game consistency unresolved because `SceneViewSkyboxEnforcer.cs` only affects SceneView and cannot prove GameView parity.
- Marked moon source route weak because current moon materials reuse terrain rock color textures and `Hecton_CelestialMoon.shader` has no normal/height texture slots.
- Required continuous `GlobalQualityWeight` consequences for Low/Middle/High/Ultra without changing gameplay truth or authority route.
