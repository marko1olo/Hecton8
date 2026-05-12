# RENDER_VFX_POST Recon

Scan command:
`rg -n "GrabPass|_CameraOpaqueTexture" Assets/_Project/Art/Shaders`

Initial findings:
- `Assets/_Project/Art/Shaders/SuitVisor.shader:226` declares `_CameraOpaqueTexture` in a transparent visor shader.
- `Assets/_Project/Art/Shaders/SuitVisor.shader:917` samples `_CameraOpaqueTexture` for scene refraction.
- `Assets/_Project/Art/Shaders/SuitVisor.shader:923` performs a second `_CameraOpaqueTexture` high tap for extra refraction.

No `GrabPass` usage was found under `Assets/_Project/Art/Shaders`.

Continuation scan command:
`rg -n "CameraOpaqueTexture|_CameraOpaqueTexture|GrabPass" Assets/_Project/Art/Shaders -g "*.shader"`

Continuation findings:
- No `GrabPass` usage remains under `Assets/_Project/Art/Shaders`.
- No `_CameraOpaqueTexture` declaration or sample remains under `Assets/_Project/Art/Shaders`.
- `HectonVisorUberPost.shader` still samples `_BlitTexture` exactly once at line 233.

Integrator note:
The old transparent-material opaque-texture path is removed from `SuitVisor.shader`. Visual acceptance is still PENDING VERIFICATION because shader import, Game View capture, and Frame Debugger proof were not available in this CLI-only pass.
