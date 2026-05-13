# WORLD_RESOURCE_SPAWNER Recon

## Domain Scan
- `Assets/_Project/Scripts/World/Resources` did not exist before implementation.
- Post-create scan of `Assets/_Project/Scripts/World/Resources`, `World/ResourceDistributionDirector.cs`, and `ResourceNode.cs` found no `public static Instance`, `FindObjectOfType`, `Resources.Load`, `UnityEngine.Random`, coroutine, or Unity `Update` methods in the touched ore path.
- `Hecton8.Core.csproj` is Unity-generated and has not regenerated while the Editor project is open, so it does not yet list `ProceduralOreSpawner.cs`.

## Build Verification
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked by unrelated global dependency holes: missing `Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `InputSignal`, and `PendingSwap`.
- Unity batchmode validation could not open the project because Unity Editor is already running on the project.

## Integrator Notes
- `Hecton8.World.Resources.asmdef` was not created. A contracts-only asmdef cannot compile until `GlobalSignals`, dispatcher registration, AUP, MapMagic height payload access, and render-upload contracts are moved/exposed through contract assemblies.
- `ResourceDepletionDeltaSignal` is the Data Archivist handoff. Persist sector hash + word index + 64-bit word mask; do not persist generated ore positions.
