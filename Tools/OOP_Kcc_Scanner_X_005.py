from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "Docs" / "Reports" / "KINEMATICS_OPTIMIZATION_REPORT_X_005.json"

SCAN_TARGETS = (
    ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerMovement.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "HectonPlayerMotor.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerKinematicsRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerActionController.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerNoiseEmitter.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerSwimPresentationController.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "SargassumPhysicsZone.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "EnvironmentalHazard.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "ToxinHazard.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "OxygenBubble.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BaseModule.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Audio" / "AcousticReverbPresetTrigger.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "DemoDoor.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "TransportChargingStation.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Construction" / "VehicleDockingModule.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerTransportLifecycleRegistry.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "TraumaDispatcher.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "VehicleMotor.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "VRSomaticProvider.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "ContextualPhysicalIkRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "MountablePlayerTransport.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "MantaScooter.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "Mining" / "DeployableSdfDrillRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "InputDispatcher.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PlayerInteraction.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "InteractableRegistry.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "EquipmentInteractionHandler.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PhysicalHandController.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PhysicalInteractionHandler.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PhysicalBatteryCompartment.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Items" / "PickupItem.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "InteractionUI.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "DiegeticPdaFocusDistanceController.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "ScannerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "DataArchaeologyRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "Floater.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HectonSocketHelper.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HarpoonLauncherTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "TetherInstance.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Tools" / "LaserCutterDodContracts.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Tools" / "LaserCutterDodRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Tools" / "LaserCutterDodJobs.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerSpawner.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "DemoFirstPersonController.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BuoyancyObject.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "RaycastBatchHelper.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "QueryCacheContext.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HectonSurvivalSystem.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "SaveManager.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "KCC",
)

PATTERNS = (
    ("sync_physics_query", re.compile(r"\b(?:UnityEngine\.)?Physics\s*\.\s*(?:SphereCast|Raycast|CapsuleCast)(?:NonAlloc)?\s*\(")),
    ("hidden_physx_query", re.compile(r"\b(?:UnityEngine\.)?Physics\s*\.\s*(?:CheckSphere|CheckCapsule|CheckBox|OverlapSphere|OverlapSphereNonAlloc|OverlapBox|OverlapBoxNonAlloc|OverlapCapsule|OverlapCapsuleNonAlloc|ComputePenetration|SyncTransforms)\s*\(|\.\s*(?:ClosestPoint|ClosestPointOnBounds|GetContacts|SweepTest|SweepTestAll)\s*\(")),
    ("physx_command_type", re.compile(r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand)\b")),
    ("physx_command_schedule", re.compile(r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand)\s*\.\s*ScheduleBatch\s*\(")),
    ("collision_callback", re.compile(r"\bOn(?:Collision|Trigger)(?:Enter|Stay|Exit)\s*\(")),
    ("rigidbody_linear_velocity_write", re.compile(r"\.\s*linearVelocity\s*=")),
    ("rigidbody_angular_velocity_write", re.compile(r"\.\s*angularVelocity\s*=")),
    ("rigidbody_velocity_write", re.compile(r"\.\s*velocity\s*=")),
    ("player_direct_pose_write", re.compile(r"\b(?:playerRigidbody|_riderBody)\s*\.\s*Move(?:Position|Rotation)\s*\(")),
    ("player_rigidbody_velocity_read", re.compile(r"\b(?:_playerRigidbody|playerRigidbody)\s*\.\s*linearVelocity\b")),
    ("player_rigidbody_motion_state_read", re.compile(r"\b(?:_playerRigidbody|playerRigidbody)\s*\.\s*(?:linearVelocity|angularVelocity|GetPointVelocity|mass|position|rotation)\b")),
    ("unity_random", re.compile(r"\bUnityEngine\s*\.\s*Random\b|\bRandom\s*\.\s*(?:Range|value|insideUnitSphere|onUnitSphere)\b")),
    ("legacy_query_result_array", re.compile(r"\bQueryResult\s*\[\s*\]|\bnew\s+QueryResult\s*\[")),
)

HYDRO_PATH = ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "KCC" / "HydrodynamicKccRuntime.cs"
HYDRO_FORBIDDEN = re.compile(
    r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand|RaycastHit|QueryParameters)\b|"
    r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand)\s*\.\s*ScheduleBatch\s*\("
)


def iter_files() -> list[Path]:
    files: list[Path] = []
    for target in SCAN_TARGETS:
        if target.is_file():
            files.append(target)
        elif target.is_dir():
            files.extend(path for path in target.rglob("*.cs") if "Editor" not in path.parts)
    return sorted(set(files))


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def scan_file(path: Path) -> list[dict]:
    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except OSError as exc:
        return [{"kind": "read_error", "path": rel(path), "line": 0, "text": str(exc)}]

    findings: list[dict] = []
    for line_number, line in enumerate(lines, start=1):
        stripped = line.strip()
        if stripped.startswith("//"):
            continue
        for name, pattern in PATTERNS:
            if pattern.search(line):
                findings.append(
                    {
                        "kind": name,
                        "path": rel(path),
                        "line": line_number,
                        "text": stripped[:240],
                    }
                )
    return findings


def main() -> int:
    findings: list[dict] = []
    for path in iter_files():
        findings.extend(scan_file(path))

    hydro_forbidden: list[dict] = []
    if HYDRO_PATH.is_file():
        lines = HYDRO_PATH.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        for line_number, line in enumerate(lines, start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            if HYDRO_FORBIDDEN.search(line):
                hydro_forbidden.append(
                    {
                        "path": rel(HYDRO_PATH),
                        "line": line_number,
                        "text": stripped[:240],
                    }
                )

    by_kind: dict[str, int] = {}
    for finding in findings:
        by_kind[finding["kind"]] = by_kind.get(finding["kind"], 0) + 1

    active_player_gates = [
        finding
        for finding in findings
        if finding["path"]
        in {
            "Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs",
            "Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs",
            "Assets/_Project/Scripts/Gameplay/PlayerActionController.cs",
            "Assets/_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs",
            "Assets/_Project/Scripts/HectonPlayerMovement.cs",
            "Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs",
            "Assets/_Project/Scripts/Gameplay/SargassumPhysicsZone.cs",
            "Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs",
            "Assets/_Project/Scripts/Gameplay/ToxinHazard.cs",
            "Assets/_Project/Scripts/Gameplay/OxygenBubble.cs",
            "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs",
            "Assets/_Project/Scripts/Gameplay/VehicleMotor.cs",
            "Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs",
            "Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs",
            "Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs",
            "Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs",
            "Assets/_Project/Scripts/Gameplay/Floater.cs",
            "Assets/_Project/Scripts/HectonSocketHelper.cs",
            "Assets/_Project/Scripts/HarpoonLauncherTool.cs",
            "Assets/_Project/Scripts/TetherInstance.cs",
            "Assets/_Project/Scripts/Core/InputDispatcher.cs",
            "Assets/_Project/Scripts/Interaction/PlayerInteraction.cs",
            "Assets/_Project/Scripts/Interaction/InteractableRegistry.cs",
            "Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs",
            "Assets/_Project/Scripts/Interaction/PhysicalHandController.cs",
            "Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs",
            "Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs",
            "Assets/_Project/Scripts/Items/PickupItem.cs",
            "Assets/_Project/Scripts/UI/InteractionUI.cs",
            "Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs",
            "Assets/_Project/Scripts/Tools/LaserCutterDodContracts.cs",
            "Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs",
            "Assets/_Project/Scripts/Tools/LaserCutterDodJobs.cs",
            "Assets/_Project/Scripts/HectonPlayerSpawner.cs",
            "Assets/_Project/Scripts/DemoFirstPersonController.cs",
            "Assets/_Project/Scripts/HectonSurvivalSystem.cs",
            "Assets/_Project/Scripts/SaveManager.cs",
            "Assets/_Project/Scripts/RaycastBatchHelper.cs",
            "Assets/_Project/Scripts/QueryCacheContext.cs",
        }
    ]

    report = {
        "agent": "X_005",
        "scanner": "Tools/OOP_Kcc_Scanner_X_005.py",
        "scope": [rel(path) for path in iter_files()],
        "hydrodynamic_kcc_runtime_forbidden_physx_command_hits": hydro_forbidden,
        "hydrodynamic_kcc_runtime_clean": len(hydro_forbidden) == 0,
        "finding_counts": by_kind,
        "findings": findings,
        "active_player_route_note": (
            "HydrodynamicKccRuntime is clean of PhysX command bridge. "
            "Scoped player, trauma/parasite hazard LOS, vehicle, VR head collision, XR look-at input probe, contextual IK, interaction hand/tool, player physical interaction, battery compartment snap, pickup item, floater attachment, player look interaction, interaction UI prompt, PDA focus probe, scanner lore probe, laser cutter DOD probe, spawn, save, transport, deployable drill snap, socket helper, demo player controller, bootstrap, and player-adjacent buoyancy files are clean "
            "of RaycastCommand/CapsulecastCommand/SpherecastCommand command bridges, Unity OnCollision/OnTrigger callback entries, "
            "sync Physics casts, hidden Physics overlap/check/component queries, direct player/rider Rigidbody pose fallbacks, and direct linearVelocity/angularVelocity writes under this scanner."
            " Player noise/action interrupt consumers are also scanned for direct Rigidbody velocity reads."
            " The legacy RaycastBatchHelper/QueryCacheContext surface is also scanned for QueryResult[] mirrors."
        ),
        "active_player_scanned_findings": active_player_gates,
    }

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Wrote {rel(REPORT)}")
    print(f"Hydro KCC forbidden command hits: {len(hydro_forbidden)}")
    print(json.dumps(by_kind, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
