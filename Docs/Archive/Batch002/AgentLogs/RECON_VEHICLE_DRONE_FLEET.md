# RECON_VEHICLE_DRONE_FLEET

Scan command:
`rg -n "Transform\.LookAt|\.LookAt\(|Quaternion\.Slerp|\.Slerp\(" Assets/_Project/Scripts -g '*.cs'`

## Findings

- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs:573` uses `Quaternion.SlerpUnclamped(...)`. Classification: scene/cinematic runtime, not drone fleet code. Risk: non-Burst managed transform interpolation if used on hot vehicle cameras. Action: leave untouched; outside assigned vehicle-drone runtime.
- `Assets/_Project/Scripts/Gameplay/HostileFlora.cs:8` contains a comment naming `Quaternion.Slerp`. Classification: documentation/comment only. Action: no code change.
- `Assets/_Project/Scripts/Gameplay/HostileFlora.cs:309` uses `Quaternion.Slerp(...)`. Classification: fauna/flora gameplay, not vehicle/drone fleet. Risk: managed bone aiming interpolation. Action: logged for fauna owner; outside assigned domain.
- `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs:311` uses `Vector3.Slerp(...)`. Classification: editor-only world builder. Action: no runtime vehicle impact.

## Vehicle/Drone Domain Result

No `Transform.LookAt`, `Quaternion.Slerp`, or `Vector3.Slerp` offender was found inside `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`, `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`, or `Assets/_Project/Scripts/Construction/RepairDroneHub.cs`.

Required replacement policy remains: Burst drone rotation uses `quaternion.LookRotationSafe` from normalized velocity and docking rotation uses `CinematicMath.FastNlerp`.
