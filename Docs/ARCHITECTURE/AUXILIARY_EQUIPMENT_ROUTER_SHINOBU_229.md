# Auxiliary Equipment Router - SHINOBU_229

Status: Compile verification blocked by stale Unity-generated project files and unrelated sibling-agent missing types.

## Authority

`AuxiliaryEquipmentRouterRuntime` owns deployed flare, sensor ping, and gravity tether lifecycle records. It does not own lighting, sonar synthesis, or tether physics solvers.

The runtime is created once from `GameBootstrapper` during equipment interaction dependency registration. Tool facades do not allocate on first use.

## Vault Buffers

- `ShinobuAuxiliaryDeployments`: `DeployedAuxiliaryDTO[1024]`, 64 bytes per record, AUP at offset 0.
- `ShinobuAuxiliaryStates`: `AuxiliaryStateDTO[1024]`, 16 bytes per record.
- `ShinobuAuxiliaryActiveEquipmentState`: `ActiveEquipmentDTO[1024]`, auxiliary-only mirror; not the modular equipment engine buffer.
- `ShinobuAuxiliaryRouteCounters`: per-slot signal counters.
- `ShinobuAuxiliaryVfxMatrices`: staged presentation matrices after AUP subtraction.
- `ShinobuAuxiliaryTelemetryRing`: 300-frame black box.

## Signal Lanes

- `AuxiliaryFlareLightSignal`: AUP, intensity, range, deterministic source hash.
- `AuxiliarySonarRequestSignal`: AUP, current radius, expansion rate, max radius.
- `AuxiliaryTetherConnectionSignal`: projectile AUP, anchor AUP, rest length.

## Legacy Facades

`DeployableFlare`, `GravTrap`, `GravityTetherTool`, and the scanner pulse path are compatibility shells. They no longer own Light, ParticleSystem, Rigidbody, Collider buffers, Unity joints, per-object pulse drawers, or local radar pulse lifetime.

`ScannerTool` still owns its scientific scan and lore query responsibilities, but its radar pulse visual request is now a `TryDeploySensorPing` call into the auxiliary router. The authored scan radius is stored in `AuxiliaryStateDTO.Scalar0` and emitted as `AuxiliarySonarRequestSignal.MaxRadius`.

## Blocked Residue

`TetherManager.cs` contains one cold `new GameObject("TetherInstance")` in the tether/cable physics domain. This router does not edit it. The scanner reports it as cross-domain residue.

## Verification Notes

`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted once after CPU guard cleared. It failed before clean verification because ignored generated `Hecton8.Core.csproj` had not imported new auxiliary files and the wider repo still contains unresolved sibling-agent symbols. Stable Unity `.meta` files were added for the new folders/scripts; generated `.csproj` was not edited as source.
