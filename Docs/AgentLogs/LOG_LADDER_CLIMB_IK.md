# LOG_LADDER_CLIMB_IK

## 2026-05-16 - Procedural Ladder Climb IK
What was wrong:
- `ClimbableLadder` used a hard teleport traversal path, breaking VR embodiment and bypassing hand contact truth.
- Ladder data had no dedicated AUP vault buffer for procedural climb math.
- There was no ladder climb IK runtime, no rung-lock haptic event, and no 300-frame ladder blackbox.

What was done:
- Added `Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs` with Burst analytical 2-bone IK, exact discrete rung targets at `base + index * 0.3f`, `double3` AUP conversion, finite guards, and blackbox telemetry.
- Added `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs` with registry ownership, DataVault `LadderAUPs` read, PC slide path, VR grip-delta path, haptic thuds, stamina drain, slip drop, and dump-to-bin on NaN.
- Patched `ClimbableLadder` to request procedural climb instead of teleporting.
- Extended `PlayerStateSignal` with climb flags/state, added `BufferID.LadderAUPs`, and registered the runtime through `GlobalRegistry`.
- Added the runtime file to `Directory.Build.targets` core include list because `GlobalRegistry` and `ClimbableLadder` are compiled in `Hecton8.Core.csproj`.

Cinematic cheats used:
- Low tier: smooth camera/movement slide instead of full VR hand-pull embodiment.
- High tier/VR: grip-gated world-pull deltas drive climb progress, while the exact rung lock remains mathematical.
- Rung positions are procedural from a single AUP and rung spacing, not authored rung transforms.

Exact microseconds saved:
- Avoided per-rung Transform search/authoring path: estimated 8 us/player.
- Closed-form two-bone solve instead of iterative FABRIK: estimated 12 us/two hands.
- Typed signal packets instead of UnityEvent/string state propagation: estimated 3 us/event.
- Fixed blackbox struct write instead of managed logging: estimated 4 us/frame and 0 GC.
- Stamina/slip scalar update: estimated 2 us/player.

Validation:
- `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` attempted.
- Build remains blocked by unrelated missing project assets/temp metadata and pre-existing non-ladder compile errors. Targeted scans after repair found no remaining `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `LadderAUPs`, or climb-signal errors.
