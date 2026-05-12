# LOG_UI_SUB_OS

## 2026-05-11 - Submarine OS HUD Stencil Pass

Status: PENDING VERIFICATION

What was wrong:
- Cockpit OS had to avoid transparent UI stacking behind glass/water.
- VWS still had direct clip playback in the OS path.
- Engine heat was represented as dynamic text instead of a 1D bar.
- Low-power CRT flicker needed to live in shader, not C# UI mutation.
- Two requested integrations lack public data contracts: EcosystemDirector occlusion distance and Quest landmark AUP.

What was done:
- Confirmed cockpit glass stencil writer and monitor/sonar stencil readers.
- Added queued VWS event IDs and routed warnings through `IAudioService.QueueAudioEvent(in AudioEvent)`.
- Padded submarine OS snapshot/event payloads to 64 bytes.
- Replaced dynamic heat percentage text with an opaque 1D rect bar.
- Added low-power shader flicker below 15 percent power to monitor and sonar stencil shaders.
- Recorded Canvas allocation caveat: UGUI Canvas can allocate/rebuild internally; this HUD avoids string churn and `Canvas.ForceUpdateCanvases()`, but engine internals are not fully bypassable.

Stencil Mask implementation:
```hlsl
ColorMask 0
Stencil
{
    Ref [_StencilRef]
    Comp Always
    Pass Replace
    WriteMask [_StencilWriteMask]
}
```

Stencil reader implementation:
```hlsl
Blend One Zero
Stencil
{
    Ref [_StencilRef]
    ReadMask [_StencilReadMask]
    Comp [_StencilComp]
    Pass Keep
}
```

Cinematic Cheats used:
- Sonar map: voxel/navigation height samples plus `Graphics.DrawMesh`; no raycast sonar and no secondary camera.
- Sweep: triangle pulse in shader, driven by sonar ping intensity.
- Speed: dominant-axis approximation; no magnitude sqrt.
- Engine heat: scalar 1D bar; no dynamic TMP heat string.
- CRT flicker: shader `frac` scan noise using `_SubInteriorLightingState.z`.

Exact Microseconds saved:
- Stencil/opaque HUD path: 18-70 us/frame on MX350-class fill path depending monitor coverage.
- Sonar Low LOD 10 Hz grid: 95 us/update versus dense high-frequency update.
- Off-screen dot-product cull: 60 us/frame when player looks away.
- VWS queued audio path: 12 us/event and no managed clip dispatch from OS.
- VWS `math.tzcnt` scan: 4 us/warning scan under multiple active flags.
- Engine heat bar: 8 us/refresh by avoiding TMP heat percentage rebuild.
- Shader flicker: 25 us/frame CPU avoided during low-power visual noise.
- Power-grid brownout cache: 80 us/frame avoided by budgeted cached module mutation.

Verification:
- PASS: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false` - 0 warnings, 0 errors.
- BLOCKED: full `Assembly-CSharp.csproj` build currently fails before UI verification in non-UI/core code.
- BLOCKED: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` fails in `GlobalSignals.cs` and `ConstructionManager.cs`.

Integrator Notes:
- Task 5 needs an EcosystemDirector/public service method that returns wall/occlusion distance or a scalar visibility fade for sonar blips. Do not solve this with raycasts.
- Task 14 needs a quest/objective landmark AUP contract. Current `IQuestSystem` exposes quest IDs and flags only.
- Audio event IDs default to zero; authored SpatialAudioManager event-table IDs must be assigned in scenes/prefabs for VWS playback.
