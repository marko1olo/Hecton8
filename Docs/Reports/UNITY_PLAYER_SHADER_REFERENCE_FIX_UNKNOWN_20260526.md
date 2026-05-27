# Unity Player Shader Reference Fix - UNKNOWN - 2026-05-26

## Verdict

Fixed a real player-build risk in first-20-minutes UI/tool visuals.

This is not a project-wide shader cleanup. Runtime code still has `46` raw `Shader.Find` matches in `33` non-Editor files.

## External Proof Checked

- Unity `Shader.Find`: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Shader.Find.html
- Unity shader loading: https://docs.unity.cn/Manual/shader-loading.html
- Unity shader prewarm: https://docs.unity.cn/Manual/shader-prewarm.html

## Why This Was Worth Fixing

Unity says a shader found by name may be absent from a player build if nothing references it.

The affected paths create runtime materials for visible first-20-minutes systems:

- PDA map hologram volume.
- Suit HUD dithered background and data-recording pulse.
- Harpoon tether tracer.

Editor-only success was not valid proof for these routes.

## Source Changes

| File | Change |
|---|---|
| `Assets/_Project/Scripts/PlayerPDA.cs` | Added serialized PDA hologram shader forwarding. |
| `Assets/_Project/Scripts/UI/PDASpectrumTab.cs` | Stored and forwarded PDA map hologram shader. |
| `Assets/_Project/Scripts/UI/PDAMapTab.cs` | Accepted hologram shader from owner and made name lookup editor/development only. |
| `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | Added serialized data pulse shader and made two HUD lookups editor/development only. |
| `Assets/_Project/Scripts/HarpoonLauncherTool.cs` | Added serialized tracer shader and made name lookup editor/development only. |

## Prefab References Added

| Prefab | Field | Shader GUID |
|---|---|---|
| `Assets/_Project/Prefabs/Player.prefab` | `pdaHologramMapShader` | `008170efd67158d4c99647a8518cdba7` |
| `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` | `ditheredUiBackgroundShader` | `021ae9f459be4094b8800c25a19d5d9e` |
| `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` | `dataRecPulseShader` | `129f45d4c7583aa40a6e7c83c60d446a` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab` | `tracerShader` | `1d324d2bc5d23144bbf8d997d03ddc1a` |

## Validation

| Check | Result |
|---|---|
| Touched `Shader.Find` preprocessor scan | All touched name lookups are under `UNITY_EDITOR || DEVELOPMENT_BUILD`. |
| Prefab YAML shape | Changed prefabs contain Unity YAML `GameObject`, `MonoBehaviour`, and `PrefabInstance` documents. |
| `git diff --check` | Passed, with LF/CRLF working-copy warnings only. |
| CLI build | `Docs/Reports/BUILD_UNKNOWN_SHADER_REFERENCE_FIX_20260526.log`; exit `0`; `0 Warning(s)`; `0 Error(s)`. |
| Documentation gates | `VerifyDocStructure.py pass=true`; `OOP_Doc_Scanner.py finalPass=true`; active docs `694`. |

## Remaining Shader Route Debt

Do not claim global cleanliness.

Remaining raw runtime matches include:

- SRP/render-feature shaders in `Visor/*Feature.cs` and `Rendering/*Feature.cs`.
- World/indirect draw shaders in `WreckMaterialRegistry`, `ImpostorSystem`, `GroundPenetratingRadarRuntime`, and outpost generation.
- Proxy/fallback materials in construction, resource distribution, asset lifecycle, and connection spline code.
- VFX runtime materials in plasma beam and debris renderers.

Some are acceptable cold/editor/dev fallback routes. Others need authored `Shader` or `Material` references, render-feature asset wiring, or explicit GraphicsSettings/variant proof.

## Proof Boundary

This pass proves C# compile and static asset-reference routing only.

Not proven:

- Unity import.
- Player build.
- Shader variant inclusion.
- Runtime visual check.
- Profiler or first-frame shader hitch data.
