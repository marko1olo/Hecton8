# RECON_PLAYER_KINEMATICS

Command:
`rg -n "CharacterController\.Move|Rigidbody\.MovePosition" Assets/_Project/Scripts`

## Offenders

- `Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs:122`
  - Match: `_hullRigidbody.MovePosition(nextRuntimePosition);`
  - Domain: Vehicle/submarine station keeping, not player locomotion.
  - Action: Logged only. Do not edit from PLAYER_KINEMATICS without integrator approval.

- `Assets/_Project/Scripts/HectonPlayerSpawner.cs:856`
  - Match: `playerRigidbody.MovePosition(position);`
  - Domain: Spawn/teleport placement, not continuous player locomotion.
  - Action: Logged only. Spawn placement may be valid one-shot teleport authority; integrator should classify.

## Player Kinematics Result

No `CharacterController.Move` matches were found under `Assets/_Project/Scripts`.
No direct `Rigidbody.MovePosition` match was found in `HectonPlayerMovement.cs`.
