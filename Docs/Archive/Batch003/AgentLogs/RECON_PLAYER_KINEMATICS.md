# RECON_PLAYER_KINEMATICS

Status: PENDING VERIFICATION

Scan command:
`rg -n "CharacterController\.Move|Rigidbody\.MovePosition|\.MovePosition\(" Assets/_Project/Scripts`

Findings:
- `CharacterController.Move`: no direct call found.
- `VehicleDockingModule.cs:591`, `VehicleDockingModule.cs:631`: docking-domain `Rigidbody.MovePosition` wrapper usage, outside player locomotion ownership.
- `FaunaBrain.cs:3880`, `FaunaBrain.cs:5264`, `FaunaBrain.cs:5341`: fauna-domain rigidbody positioning, outside player locomotion ownership.
- `BaseAirlock.cs:725`: airlock snap body positioning, outside player locomotion ownership.
- `HectonPlayerSpawner.cs:856`: spawn-only player rigidbody placement. Cold path; not a locomotion controller.
- `HectonPlayerMovement.cs:2477`: player motor wrapper recovery path. No direct `Rigidbody.MovePosition` call added here.
- `HectonPlayerMotor.cs:596`: single owner wrapper around `_body.MovePosition`; existing motor contract.
- `HectonFloatingOrigin.cs:329`: origin-shift rebase path; not per-frame locomotion.
- `MountablePlayerTransport.cs:863`, `MountablePlayerTransport.cs:1410`: transport/rider attachment movement, outside this batch ownership.
- `PlayerKinematicsRuntime.cs:341`: no-clip fail-safe calls motor wrapper only when solid voxel recovery is active.
- `LifePodSeatStrapCoordinator.cs:257`, `LifePodSeatStrapCoordinator.cs:267`, `LifePodSeatStrapCoordinator.cs:279`: life-pod restraint positioning, outside this batch ownership.
- `PhysicalInteractionHandler.cs:830`: interaction object body positioning, outside player locomotion ownership.
- `SubmarineStationKeepingController.cs:122`: submarine hull station-keeping, outside player locomotion ownership.
- `VehicleMotor.cs:950`: vehicle motor wrapper, outside player locomotion ownership.

Decision:
Do not rewrite non-player owners in this batch. Player path stays through `HectonPlayerMotor` or recovery-only code; direct `CharacterController` remains absent.
