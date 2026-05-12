# LOG_UI_DIEGETIC_INPUT

## 2026-05-11 - UI_DIEGETIC_INPUT Final Report
STATUS: PENDING VERIFICATION

What was wrong:
- 2D/EventSystem terminal interaction was not acceptable for diegetic NASA-punk screens.
- Scroll dials needed a PAL path instead of raw New Input reads.
- Damage/glare/CRT polish needed shader fakes, not camera or physics work.
- Mechanical feedback needed queue-based haptics/audio and fixed payloads.

What was done:
- Added `PlayerInputState.ScrollDelta`, `InputManager.TryReadUiScrollWheel`, and dispatcher forwarding.
- Extended `DiegeticPanelInputEvent` with `AnalogDelta` and `Scroll`.
- Hard-capped panel reach to 2 m through AUP distance.
- Added damage glitch and flashlight glare material channels.
- Added `PhysicalPanelDial.cs` with `.meta`.
- Forwarded scroll through `KinematicTerminalInteractionBridge`.
- Added NativeQueue `AudioEvent` hooks for keyboard, dial, snap switch, and physical panel button.
- Preserved zero-GC keyboard char buffer, tooltips, boot-log buffers, and math.select highlight path.

Cinematic cheats used:
- Mouse-to-panel uses plane projection and reciprocal multiply, not physics raycast or UI raycast.
- CRT curvature, scanlines, damage glitch, flashlight glare are shader ALU/hash bands, not camera shake or mesh deformation.
- Dial/switch rotations use approximate no-trig axis rotation for visual motion.
- Haptics/audio use fixed queued payloads, not spawned components.

Mouse-to-UV projection code without divisions:
```csharp
float planeDistance = math.dot(panelOrigin - rayOriginWs, panelNormal) * math.rcp(denom);
worldHit = rayOriginWs + rayDirection * planeDistance;
localHit = math.transform(_panelData.WorldToLocal, worldHit);

float2 invCanvasSize = math.rcp(safeCanvasSize);
float2 uv = new float2(
    (localHit.x + _panelData.HalfSize.x) * invCanvasSize.x,
    (localHit.y + _panelData.HalfSize.y) * invCanvasSize.y);
```

Exact microseconds saved, estimated pending profiler:
- EventSystem/GraphicRaycaster removal: 80-250 us per active terminal frame.
- No string keyboard/boot-log path: 30-200 us per edit/log refresh plus zero transient managed garbage.
- Queue audio instead of ad-hoc AudioSource work: 50-300 us per authored mechanical interaction.
- Reciprocal projection math: 1-3 us per hot cursor solve.

Verification:
- PASS: `dotnet build C:\hades\Hecton8\Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false`
- PASS: `dotnet build C:\hades\Hecton8\Hecton8.Input.csproj --no-restore -p:BuildProjectReferences=false`
- BLOCKED: `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false` fails in external Construction/Physics files, not UI_DIEGETIC_INPUT files.

Git diff summary:
- Modified: `Hecton_DiegeticPanelUnlit.shader`, `InputDispatcher.cs`, `PlayerInputState.cs`, `InputManager.cs`, `PhysicalSnapSwitch.cs`, `DiegeticPanelController.cs`, `PhysicalPanelButton.cs`, `PhysicalTerminalKeyboard.cs`, `KinematicTerminalInteractionBridge.cs`.
- Added: `PhysicalPanelDial.cs`, `PhysicalPanelDial.cs.meta`.
- Logs: `Status_UI_DIEGETIC_INPUT.md`, `Rationale_UI_DIEGETIC_INPUT.md`, `LOG_UI_DIEGETIC_INPUT.md`.
