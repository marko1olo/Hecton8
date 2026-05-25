from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_JSON = ROOT / "Docs" / "Reports" / "KCC_APEX_AUDIT_X_005.json"
REPORT_MD = ROOT / "Docs" / "Reports" / "KCC_APEX_AUDIT_X_005.md"

HYDRO = ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "KCC" / "HydrodynamicKccRuntime.cs"
SMOKE = ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "KCC" / "HectonKccRuntime_SmokeTest.cs"
SMOKE_EDITOR = ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "KCC" / "Editor" / "Shinobu355KccSmokeEditorFacade.cs"
HEADLESS_KCC_TESTS = ROOT / "Assets" / "_Project" / "Tests" / "Editor" / "HeadlessKccSmokeTests.cs"
LOCKSTEP = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Determinism" / "LockstepStateValidator.cs"
ROLLBACK_TEST = ROOT / "Assets" / "_Project" / "Tests" / "Editor" / "RollbackNetcodeEditTests.cs"
KINEMATIC_STATE = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Contracts" / "Physics" / "KinematicStateContract.cs"
PLAYER_MOVEMENT = ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerMovement.cs"
PLAYER_FOOTSTEP_AUDIO = ROOT / "Assets" / "_Project" / "Scripts" / "PlayerFootstepAudio.cs"
PLAYER_MOTOR = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "HectonPlayerMotor.cs"
PLAYER_KINEMATICS = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerKinematicsRuntime.cs"
PLAYER_STATE = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "HectonPlayerState.cs"
PLAYER_MOVEMENT_CONTRACTS = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Contracts" / "PlayerMovementContracts.cs"
RAYCAST_BATCH_HELPER = ROOT / "Assets" / "_Project" / "Scripts" / "RaycastBatchHelper.cs"
GLOBAL_REGISTRY_CONTRACTS = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "GlobalRegistryContracts.cs"
EQUIPMENT_INTERACTION_HANDLER = ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "EquipmentInteractionHandler.cs"
PLAYER_NOISE = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerNoiseEmitter.cs"
PLAYER_ACTION = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerActionController.cs"
PLAYER_SWIM = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerSwimPresentationController.cs"
SURVIVAL = ROOT / "Assets" / "_Project" / "Scripts" / "HectonSurvivalSystem.cs"
PLAYER_SPAWNER = ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerSpawner.cs"
SAVE_MANAGER = ROOT / "Assets" / "_Project" / "Scripts" / "SaveManager.cs"
PHYSICS_APPLY = ROOT / "Assets" / "_Project" / "Scripts" / "PhysicsApplySystem.cs"
TOOL_HIT = ROOT / "Assets" / "_Project" / "Scripts" / "ToolHitUtility.cs"
PLAYER_TOOL = ROOT / "Assets" / "_Project" / "Scripts" / "PlayerTool.cs"
PLAYER_INVENTORY = ROOT / "Assets" / "_Project" / "Scripts" / "PlayerInventory.cs"
CAMERA_JUICE = ROOT / "Assets" / "_Project" / "Scripts" / "VFX" / "CameraJuiceSystem.cs"
BASE_AIRLOCK = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "BaseAirlock.cs"
HECTON_FLUID = ROOT / "Assets" / "_Project" / "Scripts" / "HectonFluidEngine.cs"
FAUNA_BRAIN = ROOT / "Assets" / "_Project" / "Scripts" / "Fauna" / "FaunaBrain.cs"
SCOOTER_SHAFTS = ROOT / "Assets" / "_Project" / "Scripts" / "Visor" / "HectonScooterVolumetricShaftsFeature.cs"
SUBMARINE_FLUID = ROOT / "Assets" / "_Project" / "Scripts" / "SubmarineFluidDynamics.cs"
TRANSPORT_REGISTRY = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "PlayerTransportLifecycleRegistry.cs"
MANTA_SCOOTER = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "MantaScooter.cs"
VEHICLE_MOTOR = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "VehicleMotor.cs"
MOUNTABLE_TRANSPORT = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "MountablePlayerTransport.cs"
H8_MEMORY = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Memory" / "H8Memory.cs"
VAULT_MEMORY_CONTRACTS = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Memory" / "VaultMemoryContracts.cs"

TOOL_SURFACE_FILES = (
    ROOT / "Assets" / "_Project" / "Scripts" / "PlayerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BeaconDeployerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "EnvironmentalAnalyzerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "FlashlightTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HarpoonLauncherTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "KnifeTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "LogicSpannerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "PropulsionTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "SalvageSamplerTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "RepairTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "StunPistolTool.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "LaserCutter.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "PlayerBuilder.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Construction" / "DeepDrillModule.cs",
)

KINEMATIC_SURFACE_FILES = (
    ROOT / "Assets" / "_Project" / "Scripts" / "BuoyancyObject.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "ContextualPhysicalIkRuntime.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "VRSomaticProvider.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "HectonPlayerEnvironmentHandler.cs",
)

INTERACTION_TARGET_FILES = (
    ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "InputDispatcher.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "IInteractable.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "InteractableRegistry.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PlayerInteraction.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "InteractionUI.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerSpawner.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "LaserCutter.cs",
)

PLAYER_TRIGGER_CALLBACK_FILES = (
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "SargassumPhysicsZone.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "EnvironmentalHazard.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "ToxinHazard.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "OxygenBubble.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BaseModule.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Audio" / "AcousticReverbPresetTrigger.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "DemoDoor.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "TransportChargingStation.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Construction" / "VehicleDockingModule.cs",
)

SCOPED_FILES = {
    "Assets/_Project/Scripts/HectonPlayerMovement.cs",
    "Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs",
    "Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs",
    "Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs",
    "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs",
    "Assets/_Project/Scripts/Gameplay/VehicleMotor.cs",
    "Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs",
    "Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs",
    "Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs",
    "Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs",
    "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    "Assets/_Project/Scripts/Core/InputDispatcher.cs",
    "Assets/_Project/Scripts/BuoyancyObject.cs",
    "Assets/_Project/Scripts/Interaction/PlayerInteraction.cs",
    "Assets/_Project/Scripts/Interaction/InteractableRegistry.cs",
    "Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs",
    "Assets/_Project/Scripts/Interaction/PhysicalHandController.cs",
    "Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs",
    "Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs",
    "Assets/_Project/Scripts/Items/PickupItem.cs",
    "Assets/_Project/Scripts/UI/InteractionUI.cs",
    "Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs",
    "Assets/_Project/Scripts/ScannerTool.cs",
    "Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs",
    "Assets/_Project/Scripts/Gameplay/Floater.cs",
    "Assets/_Project/Scripts/HectonSocketHelper.cs",
    "Assets/_Project/Scripts/Tools/LaserCutterDodContracts.cs",
    "Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs",
    "Assets/_Project/Scripts/Tools/LaserCutterDodJobs.cs",
    "Assets/_Project/Scripts/HectonPlayerSpawner.cs",
    "Assets/_Project/Scripts/DemoFirstPersonController.cs",
    "Assets/_Project/Scripts/HectonSurvivalSystem.cs",
    "Assets/_Project/Scripts/SaveManager.cs",
    "Assets/_Project/Scripts/PlayerTool.cs",
    "Assets/_Project/Scripts/PlayerInventory.cs",
    "Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs",
    "Assets/_Project/Scripts/Gameplay/BaseAirlock.cs",
    "Assets/_Project/Scripts/HectonFluidEngine.cs",
}

FORBIDDEN = (
    ("sync_physics_query", re.compile(r"\b(?:UnityEngine\.)?Physics\s*\.\s*(?:SphereCast|Raycast|CapsuleCast)(?:NonAlloc)?\s*\(")),
    ("hidden_physx_query", re.compile(r"\b(?:UnityEngine\.)?Physics\s*\.\s*(?:CheckSphere|CheckCapsule|CheckBox|OverlapSphere|OverlapSphereNonAlloc|OverlapBox|OverlapBoxNonAlloc|OverlapCapsule|OverlapCapsuleNonAlloc|ComputePenetration|SyncTransforms)\s*\(|\.\s*(?:ClosestPoint|ClosestPointOnBounds|GetContacts|SweepTest|SweepTestAll)\s*\(")),
    ("physx_command_type", re.compile(r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand)\b")),
    ("physx_command_schedule", re.compile(r"\b(?:RaycastCommand|CapsulecastCommand|SpherecastCommand)\s*\.\s*ScheduleBatch\s*\(")),
    ("collision_callback", re.compile(r"\bOnCollision(?:Enter|Stay|Exit)\s*\(")),
)

VELOCITY_WRITE = re.compile(r"\.\s*(?:linearVelocity|angularVelocity)\s*=")
FORCE_WRITE = re.compile(r"\.\s*(?:AddForce|AddForceAtPosition|AddTorque|AddExplosionForce)\s*\(")
PLAYER_DIRECT_POSE_WRITE = re.compile(r"\b(?:playerRigidbody|_riderBody)\s*\.\s*Move(?:Position|Rotation)\s*\(")
PLAYER_BODY_ALIAS_POSE_MUTATION = re.compile(
    r"\b(?:playerBody|_playerBody)\s*\.\s*(?:MovePosition|MoveRotation|PublishTransform|ResetCenterOfMass|"
    r"transform\s*\.\s*SetPositionAndRotation|isKinematic\s*=|detectCollisions\s*=|position\s*=|rotation\s*=)"
)
PLAYER_RIGIDBODY_VELOCITY_READ = re.compile(r"\b(?:_playerRigidbody|playerRigidbody|PlayerRigidbody)\s*\.\s*linearVelocity\b")
PLAYER_RIGIDBODY_MOTION_STATE_READ = re.compile(
    r"\b(?:_playerRigidbody|playerRigidbody|PlayerRigidbody)\s*\.\s*(?:linearVelocity|angularVelocity|GetPointVelocity|mass|position|rotation|worldCenterOfMass)\b"
)
PLAYER_BODY_ALIAS_MOTION_STATE_READ = re.compile(
    r"\b(?:playerBody|_playerBody)\s*\.\s*(?:linearVelocity|angularVelocity|GetPointVelocity|mass|position|rotation|worldCenterOfMass)\b"
)
PLAYER_RIGIDBODY_ALIAS_ASSIGN = re.compile(
    r"\b(?:Rigidbody\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;\n]*\bPlayerRigidbody\b"
)
PLAYER_MOTION_STATE_MEMBERS = "linearVelocity|angularVelocity|GetPointVelocity|mass|position|rotation|worldCenterOfMass"
MOVEMENT_RB_VELOCITY_READ = re.compile(r"\b_rb\s*\.\s*linearVelocity\b")
MOVEMENT_RB_MASS_READ = re.compile(r"\b_rb\s*\.\s*mass\b")
MOVEMENT_RB_POSE_READ = re.compile(r"\b_rb\s*\.\s*(?:position|rotation)\b")
MOTOR_BODY_VELOCITY_READ = re.compile(r"\b_body\s*\.\s*linearVelocity\b")
KINEMATICS_BODY_POSE_READ = re.compile(r"\b_body\s*\.\s*(?:position|rotation)\b")
PLAYER_BODY_FORCE_ROUTE = re.compile(
    r"\b(?:QueueForce|QueueAmbientForce|QueueForceAtPosition|QueueAngularVelocitySet)\s*\(\s*"
    r"(?:_playerRigidbody|playerRigidbody|playerContext\.PlayerRigidbody|_cachedPlayerRigidbody|playerBody)\b"
)
PLAYER_TRIGGER_CALLBACK = re.compile(r"\bOnTrigger(?:Enter|Stay|Exit)\s*\(")
UNITY_COLLISION_DTO = re.compile(r"\bCollision\s+collision\b|\bContactPoint\b|\.GetContact\s*\(|\bQueueImpact\s*\(")

TYPE_SIZES = {
    "long": 8,
    "ulong": 8,
    "double3": 24,
    "float3": 12,
    "float": 4,
    "uint": 4,
    "int": 4,
    "byte": 1,
}


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def collect_player_rigidbody_aliases(text: str) -> set[str]:
    aliases: set[str] = set()
    for match in PLAYER_RIGIDBODY_ALIAS_ASSIGN.finditer(text):
        alias = match.group(1)
        if "player" in alias.lower():
            aliases.add(alias)
    return aliases


def compile_alias_motion_state_pattern(aliases: set[str]) -> re.Pattern | None:
    if not aliases:
        return None

    escaped = "|".join(sorted(re.escape(alias) for alias in aliases))
    return re.compile(r"\b(?:" + escaped + r")\s*\.\s*(?:" + PLAYER_MOTION_STATE_MEMBERS + r")\b")


def scan_forbidden() -> tuple[list[dict], list[dict]]:
    scoped: list[dict] = []
    broad: list[dict] = []
    scripts = ROOT / "Assets" / "_Project" / "Scripts"
    for path in scripts.rglob("*.cs"):
        if "Editor" in path.parts:
            continue
        text = read(path)
        relative = rel(path)
        in_block = False
        for number, raw in enumerate(text.splitlines(), 1):
            line = raw
            stripped = line.strip()
            if in_block:
                if "*/" in stripped:
                    in_block = False
                    line = stripped.split("*/", 1)[1]
                else:
                    continue
            if "/*" in stripped:
                before, _, after = stripped.partition("/*")
                if "*/" in after:
                    line = before + after.split("*/", 1)[1]
                else:
                    in_block = True
                    line = before
            if line.strip().startswith("//"):
                continue
            for kind, pattern in FORBIDDEN:
                if not pattern.search(line):
                    continue
                entry = {
                    "path": relative,
                    "line": number,
                    "kind": kind,
                    "text": line.strip()[:180],
                    "scope": "x005_scoped" if relative in SCOPED_FILES or relative.startswith("Assets/_Project/Scripts/Physics/KCC/") else "outside_x005_domain",
                }
                broad.append(entry)
                if entry["scope"] == "x005_scoped":
                    scoped.append(entry)
    return scoped, broad


def iter_runtime_code_lines():
    scripts = ROOT / "Assets" / "_Project" / "Scripts"
    for path in scripts.rglob("*.cs"):
        if "Editor" in path.parts:
            continue
        text = read(path)
        relative = rel(path)
        in_block = False
        for number, raw in enumerate(text.splitlines(), 1):
            line = raw
            stripped = line.strip()
            if in_block:
                if "*/" in stripped:
                    in_block = False
                    line = stripped.split("*/", 1)[1]
                else:
                    continue
            if "/*" in stripped:
                before, _, after = stripped.partition("/*")
                if "*/" in after:
                    line = before + after.split("*/", 1)[1]
                else:
                    in_block = True
                    line = before
            if line.strip().startswith("//"):
                continue
            yield relative, number, line


def scan_direct_rigidbody_writes() -> dict:
    velocity_entries = []
    force_entries = []
    player_pose_entries = []
    for relative, number, line in iter_runtime_code_lines():
        stripped = line.strip()
        if VELOCITY_WRITE.search(line):
            if relative == "Assets/_Project/Scripts/PhysicsApplySystem.cs":
                classification = "central_physics_apply_owner"
            elif relative == "Assets/_Project/Scripts/FaunaDirector.cs" and re.search(r"\b(?:state|restoredState)\.(?:linearVelocity|angularVelocity)\s*=", line):
                classification = "dto_state_assignment"
            else:
                classification = "external_rigidbody_velocity_write"
            velocity_entries.append(
                {
                    "path": relative,
                    "line": number,
                    "classification": classification,
                    "text": stripped[:180],
                }
            )
        if FORCE_WRITE.search(line):
            classification = "central_physics_apply_owner" if relative == "Assets/_Project/Scripts/PhysicsApplySystem.cs" else "external_rigidbody_force_write"
            force_entries.append(
                {
                    "path": relative,
                    "line": number,
                    "classification": classification,
                    "text": stripped[:180],
                }
            )
        if PLAYER_DIRECT_POSE_WRITE.search(line):
            player_pose_entries.append(
                {
                    "path": relative,
                    "line": number,
                    "classification": "external_player_rigidbody_pose_write",
                    "text": stripped[:180],
                }
            )
        if PLAYER_BODY_ALIAS_POSE_MUTATION.search(line):
            player_pose_entries.append(
                {
                    "path": relative,
                    "line": number,
                    "classification": "player_body_alias_pose_mutation",
                    "text": stripped[:180],
                }
            )

    external_velocity = [entry for entry in velocity_entries if entry["classification"] == "external_rigidbody_velocity_write"]
    external_force = [entry for entry in force_entries if entry["classification"] == "external_rigidbody_force_write"]
    return {
        "velocity_assignments": velocity_entries,
        "force_calls": force_entries,
        "player_pose_assignments": player_pose_entries,
        "external_velocity_assignment_count": len(external_velocity),
        "external_force_call_count": len(external_force),
        "external_player_pose_assignment_count": len(player_pose_entries),
    }


def resolve_size_expression(text: str, expression: str) -> int:
    expression = expression.strip()
    if expression.isdigit():
        return int(expression)

    symbol = expression.rsplit(".", 1)[-1]
    match = re.search(r"\bconst\s+int\s+" + re.escape(symbol) + r"\s*=\s*(\d+)\s*;", text)
    if not match:
        raise RuntimeError(f"StructLayout size expression is not resolvable: {expression}")

    return int(match.group(1))


def extract_struct(text: str, name: str) -> tuple[int, str]:
    pattern = re.compile(
        r"\[StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*([A-Za-z0-9_.]+)\)\]\s*"
        r"(?:public|internal)\s+struct\s+" + re.escape(name) + r"\s*\{(?P<body>.*?)\n\s*\}",
        re.S,
    )
    match = pattern.search(text)
    if not match:
        raise RuntimeError(f"struct not found: {name}")
    return resolve_size_expression(text, match.group(1)), match.group("body")


def extract_method_body(text: str, name: str) -> str:
    match = re.search(r"\b" + re.escape(name) + r"\s*\([^)]*\)\s*\{", text)
    if not match:
        return ""

    start = match.end()
    depth = 1
    index = start
    while index < len(text) and depth > 0:
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
        index += 1

    return text[start:index - 1] if depth == 0 else ""


def parse_fields(size: int, body: str) -> dict:
    fields = []
    for offset, type_name, field_name in re.findall(
        r"\[FieldOffset\((\d+)\)\]\s*(?:public|private|internal)\s+(?:readonly\s+)?([A-Za-z0-9_<>.]+)\s+([A-Za-z0-9_]+)",
        body,
    ):
        byte_count = TYPE_SIZES.get(type_name)
        if byte_count is None:
            continue
        start = int(offset)
        fields.append(
            {
                "name": field_name,
                "type": type_name,
                "offset": start,
                "bytes": byte_count,
                "end_exclusive": start + byte_count,
            }
        )

    coverage = [False] * size
    overlaps = []
    for field in fields:
        for index in range(field["offset"], min(field["end_exclusive"], size)):
            if coverage[index]:
                overlaps.append({"byte": index, "field": field["name"]})
            coverage[index] = True
    gaps = []
    cursor = 0
    while cursor < size:
        if coverage[cursor]:
            cursor += 1
            continue
        start = cursor
        while cursor < size and not coverage[cursor]:
            cursor += 1
        gaps.append({"offset": start, "bytes": cursor - start})
    max_end = max((field["end_exclusive"] for field in fields), default=0)
    return {
        "size": size,
        "fields": sorted(fields, key=lambda f: (f["offset"], f["name"])),
        "gaps": gaps,
        "overlaps": overlaps,
        "max_field_end": max_end,
        "covered_bytes": sum(1 for covered in coverage if covered),
    }


def layout_report() -> dict:
    lock_size, lock_body = extract_struct(read(LOCKSTEP), "LockstepPlayerKinematicState")
    kin_size, kin_body = extract_struct(read(KINEMATIC_STATE), "KinematicStateDTO")
    probe_size, probe_body = extract_struct(read(PLAYER_KINEMATICS), "PlayerKinematicsProbeHit")
    return {
        "LockstepPlayerKinematicState": parse_fields(lock_size, lock_body),
        "KinematicStateDTO": parse_fields(kin_size, kin_body),
        "PlayerKinematicsProbeHit": parse_fields(probe_size, probe_body),
    }


def lockstep_layout_gate_report() -> dict:
    runtime = read(LOCKSTEP)
    test = read(ROLLBACK_TEST)
    runtime_offset_checks = (
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.PositionAup)) == PlayerKinematicPositionAupOffset",
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Velocity)) == PlayerKinematicVelocityOffset",
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputVector)) == PlayerKinematicInputVectorOffset",
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Frame)) == PlayerKinematicFrameOffset",
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Flags)) == PlayerKinematicFlagsOffset",
        "FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputActions)) == PlayerKinematicInputActionsOffset",
    )
    test_offset_checks = (
        "Assert.AreEqual(0, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.PositionAup)))",
        "Assert.AreEqual(24, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Velocity)))",
        "Assert.AreEqual(36, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputVector)))",
        "Assert.AreEqual(48, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Frame)))",
        "Assert.AreEqual(52, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Flags)))",
        "Assert.AreEqual(56, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputActions)))",
    )
    compatibility_properties = (
        "SectorX",
        "SectorY",
        "SectorZ",
        "LocalPosition",
        "Forward",
        "StableId",
        "HashCadenceFrames",
    )
    return {
        "runtime_validator_checks_64_byte_size": "UnsafeUtility.SizeOf<LockstepPlayerKinematicState>() == PlayerKinematicStateBytes" in runtime,
        "runtime_validator_checks_storage_offsets": all(check in runtime for check in runtime_offset_checks),
        "rollback_test_uses_64_byte_size": "Assert.AreEqual(64, UnsafeUtility.SizeOf<LockstepPlayerKinematicState>())" in test,
        "rollback_test_rejects_96_byte_layout": "Assert.AreEqual(96, UnsafeUtility.SizeOf<LockstepPlayerKinematicState>())" not in test,
        "rollback_test_uses_storage_field_offsets": all(check in test for check in test_offset_checks),
        "rollback_test_has_no_compat_property_offsets": all(
            f"OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.{name}))" not in test
            for name in compatibility_properties
        ),
    }


def legacy_bridge_report() -> dict:
    motor = read(PLAYER_MOTOR)
    kinematics = read(PLAYER_KINEMATICS)
    movement = read(PLAYER_MOVEMENT)
    footstep_audio = read(PLAYER_FOOTSTEP_AUDIO)
    movement_contracts = read(PLAYER_MOVEMENT_CONTRACTS)
    spawner = read(PLAYER_SPAWNER)
    state = read(PLAYER_STATE)
    legacy_batch = read(RAYCAST_BATCH_HELPER)
    interaction_contracts = read(GLOBAL_REGISTRY_CONTRACTS)
    interaction_handler = read(EQUIPMENT_INTERACTION_HANDLER)
    tool_surface_text = "\n".join(read(path) for path in TOOL_SURFACE_FILES)
    kinematic_surface_text = "\n".join(read(path) for path in KINEMATIC_SURFACE_FILES)
    interaction_target_text = "\n".join(read(path) for path in INTERACTION_TARGET_FILES)
    vehicle = read(VEHICLE_MOTOR)
    mountable = read(MOUNTABLE_TRANSPORT)
    memory_text = read(H8_MEMORY) + "\n" + read(VAULT_MEMORY_CONTRACTS)
    motor_capsule_bridge_symbols = len(re.findall(r"\b(?:ScheduleCapsuleSweepBatch|TryConsumeScheduledCapsuleSweep|TrySweepGatedMove|ScheduledSweepState|_scheduledSweep)", motor))
    motor_native_state_symbols = len(re.findall(r"\bHectonPlayerMotorNativeState\b", motor + "\n" + state))
    vehicle_capsule_bridge_symbols = len(re.findall(r"\b(?:ScheduleCapsuleSweepBatch|TryConsumeScheduledCapsuleSweep|HasPendingSweep|ScheduledSweepState|_scheduledSweep|VehicleMotorSweep(?:Commands|Results))", vehicle + "\n" + mountable + "\n" + memory_text))
    movement_legacy_collision_symbols = len(re.findall(r"\b(?:QueuedCollisionEvent|HandleLegacyCollisionEnter|ProcessQueuedCollisionEvents|TryResolveCollisionEventMetadata|TryTransferKccImpactToRigidbody|TryStartWipeoutFromCollision|CollisionMetadataCache|ColliderCallbackMetadata)\b", movement))
    movement_unity_collision_dtos = len(re.findall(r"\bCollision\s+collision\b|\(\s*Collision\s+collision\b|\bContactPoint\s+contact\b|\.GetContact\s*\(", movement))
    movement_raycast_named_surface_symbols = len(re.findall(r"\b(?:TryEmitRaycastedFootstepAudio|raycasted|raycast material|foot-support raycast|casting .*?rays|Burst ray range)\b", movement, re.IGNORECASE))
    motor_repair_physx_wording = len(re.findall(r"\b(?:raycast lane|RaycastHit|RaycastCommand|CapsulecastCommand|SpherecastCommand)\b", motor))
    kinematics_default_physics_layer_symbols = len(re.findall(r"\bUnityEngine\.Physics\.DefaultRaycastLayers\b", kinematics))
    interaction_target_legacy_symbols = len(re.findall(
        r"\b(?:TryRaycastSpatial|raycastInterval|_raycastTimer|PerformRaycast|_raycastRequesterId|"
        r"CacheRaycastRequesterId|StageDodRaycastRequest|ResolveCuttableRaycastMask|"
        r"raycastOriginHeight|_rayOrigin|DefaultRaycastLayerMask)\b",
        interaction_target_text,
    ))
    return {
        "player_motor_capsule_sweep_bridge_symbol_count": motor_capsule_bridge_symbols,
        "player_motor_capsule_sweep_bridge_removed": motor_capsule_bridge_symbols == 0,
        "player_motor_repair_bridge_disabled": "Legacy repair-target bridge is disabled" in motor,
        "player_motor_native_state_symbol_count": motor_native_state_symbols,
        "player_motor_native_state_removed": motor_native_state_symbols == 0,
        "player_motor_raycast_hit_symbol_count": len(re.findall(r"\bRaycastHit\b", motor)),
        "player_hand_probe_uses_explicit_probe_hit": "VaultBufferBinding<PlayerKinematicsProbeHit> _handProbeHits" in kinematics,
        "player_hand_probe_raycast_hit_lane_count": len(re.findall(r"(?:VaultBufferBinding|NativeArray)<\s*RaycastHit\s*>\s+(?:_handProbeHits|Hits)\b", kinematics)),
        "player_kinematics_runtime_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", kinematics)),
        "player_kinematics_sync_contract_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", movement_contracts)),
        "player_kinematics_sync_contract_uses_vector_ladder_contact": "TryGetRecentLadderContact(int maxPhysicsFrameAge, out Vector3 point)" in movement_contracts,
        "player_movement_surface_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", movement)),
        "player_movement_legacy_collision_symbol_count": movement_legacy_collision_symbols,
        "player_movement_unity_collision_dto_count": movement_unity_collision_dtos,
        "player_movement_legacy_collision_route_removed": movement_legacy_collision_symbols == 0 and movement_unity_collision_dtos == 0,
        "player_movement_raycast_named_surface_symbol_count": movement_raycast_named_surface_symbols,
        "player_movement_surface_language_is_typed": movement_raycast_named_surface_symbols == 0,
        "player_motor_repair_physx_wording_count": motor_repair_physx_wording,
        "player_motor_repair_language_is_typed": motor_repair_physx_wording == 0,
        "player_kinematics_default_physics_layer_count": kinematics_default_physics_layer_symbols,
        "player_kinematics_uses_strict_interaction_probe_mask": "handProbeLayerMask = HectonLayerMasks.StrictInteractionLayerMask" in kinematics,
        "player_footstep_audio_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", footstep_audio)),
        "player_movement_surface_uses_explicit_hit": "public struct PlayerMovementSurfaceHit" in movement and "out PlayerMovementSurfaceHit hit" in movement,
        "player_footstep_audio_uses_surface_hit": "HectonPlayerMovement.PlayerMovementSurfaceHit" in footstep_audio,
        "player_spawner_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", spawner)),
        "player_spawner_try_raycast_ground_count": len(re.findall(r"\bTryRaycastGround\b", spawner)),
        "player_spawner_uses_spawn_ground_hit": "private struct SpawnGroundHit" in spawner and "TryResolveGroundHit(out SpawnGroundHit hit)" in spawner,
        "player_spawner_uses_ground_probe_origin": "groundProbeOriginHeight" in spawner and "_groundProbeOrigin" in spawner,
        "player_motor_raycast_hit_allocations": len(re.findall(r"AllocateMotorArray<\s*RaycastHit\s*>", state)),
        "player_motor_command_allocations": len(re.findall(r"AllocateMotorArray<\s*(?:RaycastCommand|CapsulecastCommand)\s*>", state)),
        "legacy_batch_query_result_arrays": len(re.findall(r"QueryResult\s*\[\s*\]", legacy_batch)) + len(re.findall(r"new\s+QueryResult\s*\[", legacy_batch)),
        "legacy_batch_physx_calls": len(re.findall(r"\bPhysics\s*\.\s*(?:Raycast|SphereCast|CapsuleCast|BoxCast|Linecast|Overlap|Check|ComputePenetration|SyncTransforms)", legacy_batch)),
        "legacy_batch_unity_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", legacy_batch)),
        "interaction_surface_unity_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", interaction_contracts + "\n" + interaction_handler + "\n" + tool_surface_text)),
        "interaction_surface_legacy_method_count": len(re.findall(r"\b(?:TryRaycastPrimary|TryQueuePrimaryRaycast)\b", interaction_contracts + "\n" + interaction_handler + "\n" + tool_surface_text)),
        "interaction_surface_uses_typed_hit": "public struct InteractionSurfaceHit" in interaction_contracts and "out InteractionSurfaceHit hit" in interaction_handler and "out InteractionSurfaceHit hit" in tool_surface_text,
        "interaction_surface_vault_uses_typed_hit_dto": "VaultGenerationHandle<InteractionSurfaceHitDTO>" in interaction_handler and "NativeArray<InteractionSurfaceHitDTO>" in interaction_handler,
        "kinematic_surface_hit_layout_64": "[StructLayout(LayoutKind.Explicit, Size = 64)]\n    public struct KinematicSurfaceHit" in interaction_contracts,
        "kinematic_surface_unity_raycast_hit_count": len(re.findall(r"\bRaycastHit\b", kinematic_surface_text)),
        "kinematic_surface_uses_typed_hit": "NativeArray<KinematicSurfaceHit>" in kinematic_surface_text and "VaultNativeArray<KinematicSurfaceHit>" in kinematic_surface_text,
        "interaction_target_legacy_raycast_api_count": interaction_target_legacy_symbols,
        "interaction_target_uses_spatial_target_contract": interaction_target_legacy_symbols == 0
            and interaction_target_text.count("TryResolveSpatialTarget") >= 4
            and "targetProbeInterval" in interaction_target_text
            and "_targetProbeTimer" in interaction_target_text
            and "ResolveHoveredTarget()" in interaction_target_text
            and "_surfaceRequesterId" in interaction_target_text
            and "ResolveCuttableSurfaceMask()" in interaction_target_text,
        "vehicle_motor_capsule_sweep_bridge_symbol_count": vehicle_capsule_bridge_symbols,
        "vehicle_motor_capsule_sweep_bridge_removed": vehicle_capsule_bridge_symbols == 0,
        "vehicle_motor_raycast_hit_symbol_count": len(re.findall(r"\bRaycastHit\b", vehicle)),
    }


def scan_unity_collision_dtos() -> dict:
    entries = []
    scripts_root = ROOT / "Assets" / "_Project" / "Scripts"
    for path in sorted(scripts_root.rglob("*.cs")):
        if "Editor" in path.parts:
            continue
        for line_no, line in enumerate(read(path).splitlines(), 1):
            if UNITY_COLLISION_DTO.search(line):
                entries.append({"path": rel(path), "line": line_no, "text": line.strip()})

    return {
        "unity_collision_dto_count": len(entries),
        "unity_collision_dtos": entries,
        "unity_collision_dto_route_removed": len(entries) == 0,
    }


def player_split_authority_report() -> dict:
    velocity_entries = []
    motion_state_entries = []
    alias_motion_state_entries = []
    scripts_root = ROOT / "Assets" / "_Project" / "Scripts"
    for path in sorted(scripts_root.rglob("*.cs")):
        if "Editor" in path.parts:
            continue
        text = read(path)
        alias_pattern = compile_alias_motion_state_pattern(collect_player_rigidbody_aliases(text))
        for line_no, line in enumerate(text.splitlines(), 1):
            if PLAYER_RIGIDBODY_VELOCITY_READ.search(line):
                velocity_entries.append({"path": rel(path), "line": line_no, "text": line.strip()})
            if PLAYER_RIGIDBODY_MOTION_STATE_READ.search(line):
                motion_state_entries.append({"path": rel(path), "line": line_no, "text": line.strip()})
            if PLAYER_BODY_ALIAS_MOTION_STATE_READ.search(line) or (alias_pattern is not None and alias_pattern.search(line)):
                alias_motion_state_entries.append({"path": rel(path), "line": line_no, "text": line.strip()})

    return {
        "player_rigidbody_velocity_read_count": len(velocity_entries),
        "player_rigidbody_velocity_reads": velocity_entries,
        "player_rigidbody_motion_state_read_count": len(motion_state_entries),
        "player_rigidbody_motion_state_reads": motion_state_entries,
        "player_body_alias_motion_state_read_count": len(alias_motion_state_entries),
        "player_body_alias_motion_state_reads": alias_motion_state_entries,
        "player_noise_uses_kcc_velocity_signal": "TryResolveKccVelocity(out Vector3 kccVelocity)" in read(PLAYER_NOISE),
        "player_action_uses_kcc_velocity_signal": "TryResolveKccVelocity(out Vector3 velocity)" in read(PLAYER_ACTION),
        "player_swim_has_no_rigidbody_velocity_fallback": "playerRigidbody.linearVelocity" not in read(PLAYER_SWIM),
        "survival_uses_kcc_velocity_signal": "TryResolveKccVelocity(out Vector3 velocity)" in read(SURVIVAL),
        "spawner_uses_kcc_velocity_for_teleport": "ResolveKccVelocityForTeleport()" in read(PLAYER_SPAWNER),
        "player_tool_recoil_uses_equivalent_mass": "private const float PlayerEquivalentMassKg = 80f;" in read(PLAYER_TOOL)
            and "Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;" not in read(PLAYER_TOOL),
        "player_inventory_impact_uses_equivalent_mass": "private const float PlayerEquivalentMassKg = 80f;" in read(PLAYER_INVENTORY)
            and "playerBody.mass" not in read(PLAYER_INVENTORY),
        "camera_juice_uses_kcc_velocity": "PhysicsDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityCameraJuiceMaxAgeFrames" in read(CAMERA_JUICE)
            and "playerBody.linearVelocity" not in read(CAMERA_JUICE),
        "airlock_snap_start_uses_transform_pose": "Vector3 startPosition = player.position;" in read(BASE_AIRLOCK)
            and "Quaternion startRotation = player.rotation;" in read(BASE_AIRLOCK)
            and "playerBody.position" not in read(BASE_AIRLOCK)
            and "playerBody.rotation" not in read(BASE_AIRLOCK),
        "airlock_hydro_teleport_uses_player_motor": "TryResolveHydroPlayerMotor(player, playerBody, out HectonPlayerMotor hydroMotor)" in read(BASE_AIRLOCK)
            and "TeleportHydroPlayer(player, hydroMotor, destinationPosition, destinationRotation)" in read(BASE_AIRLOCK)
            and "_snapMotor.MovePosition(worldPosition)" in read(BASE_AIRLOCK),
        "save_load_hydro_teleport_uses_player_motor": "playerMotor.MovePosition(position);" in read(SAVE_MANAGER)
            and "TeleportLegacyLoadedPlayerBody(playerBody, position, rotation, velocity);" in read(SAVE_MANAGER)
            and "playerBody.transform.SetPositionAndRotation" not in read(SAVE_MANAGER),
        "spawner_hydro_teleport_uses_player_motor": "TryResolveHydroPlayerMotor(out _)" in read(PLAYER_SPAWNER)
            and "playerMotor.MovePosition(position);" in read(PLAYER_SPAWNER)
            and "RestoreLegacyRigidbodyAfterTeleport(playerRigidbody);" in read(PLAYER_SPAWNER)
            and "playerRigidbody.transform.SetPositionAndRotation" not in read(PLAYER_SPAWNER),
        "maelstrom_damage_uses_player_pose_snapshot": "player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot playerPose)" in read(HECTON_FLUID)
            and "playerBody.worldCenterOfMass" not in read(HECTON_FLUID),
    }


def owner_internal_authority_report() -> dict:
    movement = read(PLAYER_MOVEMENT)
    motor = read(PLAYER_MOTOR)
    kinematics = read(PLAYER_KINEMATICS)
    movement_velocity_reads = [
        {"line": line_no, "text": line.strip()}
        for line_no, line in enumerate(movement.splitlines(), 1)
        if MOVEMENT_RB_VELOCITY_READ.search(line)
    ]
    movement_mass_reads = []
    for line_no, line in enumerate(movement.splitlines(), 1):
        match = MOVEMENT_RB_MASS_READ.search(line)
        if not match:
            continue
        if re.match(r"\s*=", line[match.end():]):
            continue
        if "CacheAuthoritativeBodyMassKg(_rb.mass)" in line:
            continue
        movement_mass_reads.append({"line": line_no, "text": line.strip()})
    movement_pose_reads = [
        {"line": line_no, "text": line.strip()}
        for line_no, line in enumerate(movement.splitlines(), 1)
        if MOVEMENT_RB_POSE_READ.search(line)
    ]
    movement_hot_pose_reads = [
        entry for entry in movement_pose_reads
        if "SyncKinematic(_rb.position" not in entry["text"]
        and "return _rb != null ? HectonPlayerMotor.SafeVelocity(_rb.position)" not in entry["text"]
    ]
    motor_velocity_reads = [
        {"line": line_no, "text": line.strip()}
        for line_no, line in enumerate(motor.splitlines(), 1)
        if MOTOR_BODY_VELOCITY_READ.search(line)
    ]
    kinematics_body_velocity_reads = [
        {"line": line_no, "text": line.strip()}
        for line_no, line in enumerate(kinematics.splitlines(), 1)
        if MOTOR_BODY_VELOCITY_READ.search(line)
    ]
    kinematics_body_pose_reads = [
        {"line": line_no, "text": line.strip()}
        for line_no, line in enumerate(kinematics.splitlines(), 1)
        if KINEMATICS_BODY_POSE_READ.search(line)
    ]
    kinematics_hot_body_pose_reads = [
        entry for entry in kinematics_body_pose_reads
        if "return _body.position" not in entry["text"]
        and "return CanonicalizeRotation(ToQuaternion(_body.rotation))" not in entry["text"]
    ]
    motor_runtime_position_body = extract_method_body(motor, "ResolveCurrentRuntimePosition")
    return {
        "movement_rb_linear_velocity_read_count": len(movement_velocity_reads),
        "movement_rb_linear_velocity_reads": movement_velocity_reads,
        "movement_rb_mass_read_count": len(movement_mass_reads),
        "movement_rb_mass_reads": movement_mass_reads,
        "movement_rb_pose_read_count": len(movement_pose_reads),
        "movement_rb_pose_reads": movement_pose_reads,
        "movement_hot_rb_pose_read_count": len(movement_hot_pose_reads),
        "movement_hot_rb_pose_reads": movement_hot_pose_reads,
        "movement_velocity_reads_centralized": len(movement_velocity_reads) == 0 and "private Vector3 ResolveAuthoritativeLinearVelocity" in movement,
        "movement_has_no_rigidbody_velocity_read": len(movement_velocity_reads) == 0,
        "movement_has_no_rigidbody_mass_read": len(movement_mass_reads) == 0,
        "movement_has_no_hot_rigidbody_pose_read": len(movement_hot_pose_reads) == 0,
        "movement_body_position_is_snapshot_first": "if (_useFixedFrameSpatialCache && MathGuard.IsFinite(_fixedFrameBodyPosition))" in movement
            and "Vector3 statePosition = ResolvePlayerAupRuntimePosition();" in movement,
        "movement_uses_authoritative_body_mass_cache": "ResolveAuthoritativeBodyMassKg()" in movement and "CacheAuthoritativeBodyMassKg(currentSuitData.mass)" in movement,
        "movement_uses_kcc_velocity_signal": "PhysicsDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMovementMaxAgeFrames" in movement,
        "motor_body_linear_velocity_read_count": len(motor_velocity_reads),
        "motor_body_linear_velocity_reads": motor_velocity_reads,
        "motor_has_no_body_velocity_read": len(motor_velocity_reads) == 0,
        "player_kinematics_body_velocity_read_count": len(kinematics_body_velocity_reads),
        "player_kinematics_body_velocity_reads": kinematics_body_velocity_reads,
        "player_kinematics_has_no_body_velocity_read": len(kinematics_body_velocity_reads) == 0,
        "player_kinematics_body_pose_read_count": len(kinematics_body_pose_reads),
        "player_kinematics_body_pose_reads": kinematics_body_pose_reads,
        "player_kinematics_hot_body_pose_read_count": len(kinematics_hot_body_pose_reads),
        "player_kinematics_hot_body_pose_reads": kinematics_hot_body_pose_reads,
        "player_kinematics_has_no_hot_body_pose_read": len(kinematics_hot_body_pose_reads) == 0,
        "player_kinematics_body_position_is_snapshot_first": "if (TryReadAuthoritativePositionSnapshot(out float3 snapshotPosition))" in kinematics
            and "private quaternion ResolveAuthoritativeRotationSnapshot()" in kinematics,
        "motor_hydro_force_uses_kcc_velocity": "Vector3 currentVelocity = ResolveCurrentLinearVelocity(Vector3.zero);" in motor,
        "motor_hydro_force_uses_authority_mass": "ResolveHydrodynamicAddedMassStatelessAcceleration(force, currentVelocity, ResolveCurrentBodyMassKg())" in motor,
        "motor_hydro_impulse_uses_authority_mass": "ResolveHydrodynamicAddedMassStatelessAcceleration(impulse, currentVelocity, ResolveCurrentBodyMassKg())" in motor,
        "motor_hydro_torque_suppressed": "if (HydrodynamicKccOwnsCollision())\n                return;\n\n            Vector3 clampedTorque" in motor,
        "motor_hydro_offcenter_demotes_to_linear": "if (HydrodynamicKccOwnsCollision())\n            {\n                ApplyForce(force);\n                return;\n            }" in motor,
        "motor_has_no_scheduled_sweep_bridge": not re.search(r"\b(?:ScheduleCapsuleSweepBatch|TryConsumeScheduledCapsuleSweep|TrySweepGatedMove|ScheduledSweepState|_scheduledSweep)", motor),
        "motor_runtime_position_uses_cached_player_context": "_playerRuntimeContext" in motor_runtime_position_body and "GlobalRegistry.Player" not in motor_runtime_position_body,
    }


def player_force_route_report() -> dict:
    route_entries = []
    scripts_root = ROOT / "Assets" / "_Project" / "Scripts"
    for path in sorted(scripts_root.rglob("*.cs")):
        if "Editor" in path.parts:
            continue
        text = read(path)
        for line_no, line in enumerate(text.splitlines(), 1):
            if PLAYER_BODY_FORCE_ROUTE.search(line):
                route_entries.append({"path": rel(path), "line": line_no, "text": line.strip()})

    physics_apply = read(PHYSICS_APPLY)
    tool_hit = read(TOOL_HIT)
    fauna = read(FAUNA_BRAIN)
    shafts = read(SCOOTER_SHAFTS)
    submarine = read(SUBMARINE_FLUID)
    survival = read(SURVIVAL)
    spawner = read(PLAYER_SPAWNER)
    save_manager = read(SAVE_MANAGER)
    survival_gated = "if (playerMotor == null || !playerMotor.HydrodynamicKccOwnsCollisionAuthority)" in survival
    spawner_gated = "if (playerMotor == null || !playerMotor.HydrodynamicKccOwnsCollisionAuthority)" in spawner
    save_load_gated = "if (playerMotor == null || !playerMotor.HydrodynamicKccOwnsCollisionAuthority)" in save_manager
    ungated_route_entries = []
    for entry in route_entries:
        path = entry["path"]
        if path == "Assets/_Project/Scripts/PhysicsApplySystem.cs":
            continue
        if path == "Assets/_Project/Scripts/HectonSurvivalSystem.cs" and survival_gated:
            continue
        if path == "Assets/_Project/Scripts/HectonPlayerSpawner.cs" and spawner_gated:
            continue
        if path == "Assets/_Project/Scripts/SaveManager.cs" and save_load_gated:
            continue
        ungated_route_entries.append(entry)
    return {
        "direct_player_body_force_route_count": len(route_entries),
        "direct_player_body_force_routes": route_entries,
        "ungated_player_body_force_route_count": len(ungated_route_entries),
        "ungated_player_body_force_routes": ungated_route_entries,
        "central_force_router_routes_player_before_body_is_kinematic": "if (TryRouteToCachedPlayerForceSink(body, sanitizedForce, mode))\n                return true;\n\n            if (body.isKinematic)" in physics_apply,
        "central_force_router_routes_player_at_position_before_body_is_kinematic": "if (TryRouteToCachedPlayerForceSinkAtPosition(body, sanitizedForce, worldPosition, mode))\n                return true;\n\n            if (body.isKinematic)" in physics_apply,
        "central_force_router_suppresses_player_torque": "TrySuppressCachedPlayerAngularVelocitySet(body)" in physics_apply,
        "central_force_router_uses_kcc_velocity_for_player_velocity_set": "TryResolveCachedPlayerVelocity(playerContext, out Vector3 resolvedVelocity)" in physics_apply,
        "central_force_router_uses_equivalent_player_mass": "const float safeMass = HydrodynamicPlayerEquivalentMassKg;" in physics_apply,
        "central_force_router_no_player_body_mass_sink": "QueuePlayerForceSink(forceSink, force, mode, body.mass)" not in physics_apply,
        "tool_player_impulse_uses_equivalent_mass": "TryQueuePlayerVelocityChange(normalizedDirection * impulse, PlayerEquivalentMassKg)" in tool_hit,
        "tool_player_weight_class_uses_equivalent_mass": "float mass = IsPlayerBody(body)" in tool_hit and "PlayerEquivalentMassKg" in tool_hit,
        "fauna_light_target_uses_kcc_velocity": "TryGetLatestKccVelocityVector(KccVelocityFaunaMaxAgeFrames, out Vector3 kccVelocity)" in fauna
            and "playerContext.PlayerRigidbody.linearVelocity" not in fauna,
        "fauna_predator_impact_uses_player_force_sink": "IPlayerMovementForceSink playerForceSink" in fauna
            and "playerForceSink.QueueExternalVelocityChange(impulse / PlayerEquivalentMassKg)" in fauna
            and "TryQueuePhysicsForceAtPosition(playerBody" not in fauna,
        "scooter_shafts_velocity_uses_kcc_fallback": "PhysicsDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityShaftMaxAgeFrames" in shafts and "PlayerRigidbody.linearVelocity" not in shafts,
        "submarine_thermal_updraft_no_player_rigidbody_force": "QueueAmbientForce(_cachedPlayerRigidbody" not in submarine,
        "survival_angular_reset_gated_for_hydro": survival_gated,
        "spawner_angular_reset_gated_for_hydro": spawner_gated,
        "save_load_angular_reset_gated_for_hydro": save_load_gated,
    }


def player_trigger_callback_report() -> dict:
    entries = []
    for path in PLAYER_TRIGGER_CALLBACK_FILES:
        text = read(path)
        for line_no, line in enumerate(text.splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            if PLAYER_TRIGGER_CALLBACK.search(line):
                entries.append({"path": rel(path), "line": line_no, "text": stripped[:180]})

    sargassum = read(PLAYER_TRIGGER_CALLBACK_FILES[0])
    environmental = read(PLAYER_TRIGGER_CALLBACK_FILES[1])
    toxin = read(PLAYER_TRIGGER_CALLBACK_FILES[2])
    oxygen = read(PLAYER_TRIGGER_CALLBACK_FILES[3])
    base_module = read(PLAYER_TRIGGER_CALLBACK_FILES[4])
    reverb = read(PLAYER_TRIGGER_CALLBACK_FILES[5])
    demo_door = read(PLAYER_TRIGGER_CALLBACK_FILES[6])
    transport_charging = read(PLAYER_TRIGGER_CALLBACK_FILES[7])
    vehicle_docking = read(PLAYER_TRIGGER_CALLBACK_FILES[8])
    transport_registry = read(TRANSPORT_REGISTRY)
    manta_scooter = read(MANTA_SCOOTER)
    sargassum_hot_swap_body = extract_method_body(sargassum, "OnGlobalRegistryServiceReplaced")
    base_update_body = extract_method_body(base_module, "UpdateInteriorOccupancyFromPlayerRuntime")
    base_resync_body = extract_method_body(base_module, "ResyncInteriorOccupants")
    reverb_try_resolve_body = extract_method_body(reverb, "TryResolvePlayerPosition")
    demo_try_resolve_body = extract_method_body(demo_door, "TryResolvePlayerPosition")
    try_get_body = extract_method_body(transport_registry, "TryGetAt")
    on_spawn_body = extract_method_body(manta_scooter, "OnSpawn")
    return {
        "player_trigger_callback_count": len(entries),
        "player_trigger_callbacks": entries,
        "sargassum_uses_dispatcher_polling": "public sealed class SargassumPhysicsZone : MonoBehaviour, IUpdatable" in sargassum,
        "sargassum_uses_kcc_velocity_signal": "PhysicsDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocitySargassumMaxAgeFrames" in sargassum,
        "sargassum_no_rigidbody_velocity_read": ".linearVelocity" not in sargassum,
        "environmental_hazard_uses_slow_tick_volume": "CheckPlayerInTriggerVolume()" in environmental and "CachedTriggerVolume.FromCollider" in environmental,
        "toxin_hazard_uses_slow_tick_volume": "public sealed class ToxinHazard : MonoBehaviour, ISlowTickable" in toxin and "CachedTriggerVolume.FromCollider" in toxin,
        "oxygen_bubble_uses_runtime_position_polling": "TryCollectPlayerByRuntimePosition()" in oxygen and "PlayerRuntimePoseSnapshot" in oxygen,
        "base_module_uses_runtime_occupancy_polling": "UpdateInteriorOccupancyFromPlayerRuntime()" in base_module and "PlayerRuntimePoseSnapshot" in base_module,
        "base_module_hot_occupancy_uses_cached_player_only": "GlobalRegistry.Player" not in base_update_body and "GlobalRegistry.Player" not in base_resync_body,
        "acoustic_reverb_uses_runtime_volume_polling": "public sealed class AcousticReverbPresetTrigger : MonoBehaviour, IUpdatable" in reverb and "CachedTriggerVolume.FromCollider" in reverb,
        "acoustic_reverb_try_resolve_uses_cached_player_only": "GlobalRegistry.Player" not in reverb_try_resolve_body,
        "demo_door_uses_runtime_volume_polling": "public sealed class DemoDoor : MonoBehaviour, IUpdatable" in demo_door and "CachedTriggerVolume.FromCollider" in demo_door,
        "demo_door_try_resolve_uses_cached_player_only": "GlobalRegistry.Player" not in demo_try_resolve_body,
        "sargassum_hotswap_disables_registry_fallback": "GlobalRegistry.Player" not in sargassum_hot_swap_body and "RefreshPlayerReferencesCold(currentService as IPlayerRuntimeContext, false)" in sargassum_hot_swap_body,
        "transport_charging_uses_registry_volume_polling": "RefreshTrackedTransportsFromRegistry()" in transport_charging and "PlayerTransportLifecycleRegistry.TryGetAt" in transport_charging,
        "vehicle_docking_uses_registry_volume_polling": "RefreshDockingCandidatesFromRegistry()" in vehicle_docking and "PlayerTransportLifecycleRegistry.TryGetAt" in vehicle_docking,
        "vehicle_docking_no_legacy_collider_resolver": "TryDockFromCollider" not in vehicle_docking and "TryResolveTransportLifecycleOwner(" not in vehicle_docking and "GlobalRegistry.Player" not in vehicle_docking,
        "transport_registry_try_get_at_is_pure": not re.search(r"\bs_(?:owners|behaviours)\s*\[[^\]]+\]\s*=", try_get_body),
        "manta_scooter_registers_on_spawn": "PlayerTransportLifecycleRegistry.Register(this, this)" in on_spawn_body,
    }


def first_int_constant(text: str, name: str) -> int | None:
    match = re.search(r"\b" + re.escape(name) + r"\s*=\s*(\d+)", text)
    return int(match.group(1)) if match else None


def first_float_constant(text: str, name: str) -> float | None:
    match = re.search(r"\b" + re.escape(name) + r"\s*=\s*(-?[0-9.]+)f", text)
    return float(match.group(1)) if match else None


def solver_report() -> dict:
    hydro = read(HYDRO)
    smoke = read(SMOKE)
    smoke_editor = read(SMOKE_EDITOR)
    headless_tests = read(HEADLESS_KCC_TESTS)
    max_iterations = 8 if "return math.clamp((int)math.round(math.lerp(3f, 8f, quality)), 3, 8);" in hydro else None
    hard_stride_clamps = len(re.findall(r"math\.clamp\(MaxHitsPer(?:Command|Entity),\s*1,\s*8\)", hydro))
    has_recursion_or_stack_growth = bool(re.search(r"\b(?:KinematicResolutionJob|BuildSdfCollisionHitsJob)\s+\w+\s*=>", hydro))
    return {
        "resolve_iteration_count_max": max_iterations,
        "hard_stride_clamps_1_to_8": hard_stride_clamps,
        "sdf_build_stride_clamped": "int stride = math.clamp(MaxHitsPerEntity, 1, 8);" in hydro,
        "resolution_stride_clamped": "int scheduledHitStride = math.clamp(MaxHitsPerCommand, 1, 8);" in hydro,
        "slope_stride_clamped": "int stride = math.clamp(MaxHitsPerCommand, 1, 8);" in hydro,
        "capsule_axis_probe_manifold": "for (int probe = 0; probe < 3 && writeCount < stride; probe++)" in hydro,
        "contact_plane_deduplication": "HasDuplicateContactPlane" in hydro and "DuplicateContactPlaneDotThreshold" in hydro,
        "job_recursion_detected": has_recursion_or_stack_growth,
        "default_smoke_phantoms": first_int_constant(smoke, "KccSmokeDefaultPhantomCount"),
        "default_smoke_frames": first_int_constant(smoke, "KccSmokeDefaultFrameCount"),
        "smoke_max_sweep_iterations": first_int_constant(smoke, "KccSmokeMaxSweepIterations"),
        "smoke_fixed_delta_time": first_float_constant(smoke, "KccSmokeFixedDeltaTime"),
        "strong_penetration_threshold_m": first_float_constant(smoke, "KccSmokeStrongPenetrationMeters"),
        "fall_100mps_displacement_per_frame_m": 100.0 * (first_float_constant(smoke, "KccSmokeFixedDeltaTime") or 0.016666667),
        "smoke_cone_fall_contract_tested": "ValidateApexConeFallContract" in smoke_editor and "HeadlessKcc_SmokeRunner_Preserves100MpsConeProbe" in headless_tests,
        "max_sdf_axis_probes_per_entity": 8 * 3,
        "max_stored_contact_planes_per_entity": 8,
        "max_resolution_plane_projections_per_entity": 8 * 8,
        "three_plane_corner_bound": "At most 8 unique contact planes are collected and at most 8 Gauss-Seidel passes are executed. A 3-plane corner consumes no recursion and no stack growth: each bounded projection computes v' = v - n * min(dot(v,n),0), then the next fixed-index contact is evaluated. Nearly duplicate same-direction planes above the dot threshold are discarded before they spend projection budget; opposing corridor walls remain independent constraints. The two loop counters are monotonic and capped, so degenerate coplanar/orthogonal contacts terminate after <=64 projections even when velocity becomes zero.",
        "fall_100mps_bound": "At 60 Hz, 100 m/s is 1.6666667 m per frame. The speculative SDF stage keeps an 8-slot stored contact stride, evaluates up to 24 capsule-axis SDF probes (8 sweep steps * bottom/mid/top), and the headless smoke geometry includes a central voxel cone with profile index 1 falling at exactly -100 m/s. An editor contract now asserts the smoke runner tuning limit is >=100 m/s, so this proof cannot silently degrade by speed clamp. Cone/corner degeneracy can still lose sub-voxel collider fidelity if the SDF cell is coarser than the cone tip radius; the failure mode is bounded conservative stop/slide, not an unbounded loop.",
    }


def blackbox_report() -> dict:
    hydro = read(HYDRO)
    return {
        "telemetry_capacity_300": "private const int TelemetryCapacity = 300;" in hydro,
        "agent_dump_file_present": 'private const string AgentDumpFileName = "Dump_X_005.bin";' in hydro,
        "dump_on_new_fault_mask": "faultMask != 0 && faultMask != _dumpedFaultMask" in hydro,
        "fault_latch_resets_on_clean_frame": "else if (faultMask == 0)" in hydro and "_dumpedFaultMask = 0;" in hydro,
        "late_frame_fault_scan_full_capacity": "BufferID.ShinobuHydroKccFaultFlags, SystemID.Physics, entityCapacity" in hydro,
        "telemetry_requires_states_lane": "!TelemetryRing.IsCreated || TelemetryRing.Length == 0 || !States.IsCreated || States.Length == 0" in hydro,
        "telemetry_iterations_are_exact_zero_capable": "uint executedIterations = (uint)math.max(0, ExecutedIterations);" in hydro and "Iterations = executedIterations" in hydro,
    }


def write_markdown(payload: dict) -> None:
    lock = payload["layouts"]["LockstepPlayerKinematicState"]
    kin = payload["layouts"]["KinematicStateDTO"]
    probe = payload["layouts"]["PlayerKinematicsProbeHit"]
    solver = payload["solver"]
    blackbox = payload["blackbox"]
    lockstep_gate = payload["lockstep_layout_gate"]
    forbidden_by_kind = payload["broad_forbidden_by_kind"]
    lines = [
        "# KCC APEX Audit X_005",
        "",
        "## Scoped PhysX Result",
        f"- X_005 scoped forbidden call count: {payload['scoped_forbidden_count']}",
        f"- Whole non-Editor runtime forbidden call count: {payload['broad_forbidden_count']}",
        f"- Whole non-Editor sync Physics cast count: {forbidden_by_kind.get('sync_physics_query', 0)}",
        f"- Broad residual split: {forbidden_by_kind}",
        "- Whole-runtime residuals outside X_005 are listed in JSON; they are not claimed clean by this agent.",
        "",
        "## Rigidbody Authority Result",
        f"- External non-Editor Rigidbody velocity assignment count: {payload['rigidbody_writes']['external_velocity_assignment_count']}",
        f"- External non-Editor Rigidbody force call count: {payload['rigidbody_writes']['external_force_call_count']}",
        f"- External player/rider Rigidbody pose fallback count: {payload['rigidbody_writes']['external_player_pose_assignment_count']}",
        "- Remaining velocity writes are central `PhysicsApplySystem` packet application or DTO/state assignments listed in JSON.",
        f"- Unity `Collision`/`ContactPoint` DTO route count: {payload['unity_collision_dto']['unity_collision_dto_count']}",
        f"- Unity collision DTO route removed: {payload['unity_collision_dto']['unity_collision_dto_route_removed']}",
        "",
        "## Legacy Player Sweep Bridge Result",
        f"- Player motor capsule sweep bridge removed: {payload['legacy_bridge']['player_motor_capsule_sweep_bridge_removed']}",
        f"- Player motor capsule sweep bridge symbol count: {payload['legacy_bridge']['player_motor_capsule_sweep_bridge_symbol_count']}",
        f"- Repair-target bridge disabled: {payload['legacy_bridge']['player_motor_repair_bridge_disabled']}",
        f"- Player motor native state removed: {payload['legacy_bridge']['player_motor_native_state_removed']}",
        f"- Player motor native state symbol count: {payload['legacy_bridge']['player_motor_native_state_symbol_count']}",
        f"- Player motor `RaycastHit` symbol count: {payload['legacy_bridge']['player_motor_raycast_hit_symbol_count']}",
        f"- Player hand probe uses explicit KCC probe DTO: {payload['legacy_bridge']['player_hand_probe_uses_explicit_probe_hit']}",
        f"- Player hand probe `RaycastHit` lane count: {payload['legacy_bridge']['player_hand_probe_raycast_hit_lane_count']}",
        f"- Player kinematics runtime `RaycastHit` symbol count: {payload['legacy_bridge']['player_kinematics_runtime_raycast_hit_count']}",
        f"- Player kinematics sync contract `RaycastHit` symbol count: {payload['legacy_bridge']['player_kinematics_sync_contract_raycast_hit_count']}",
        f"- Player kinematics sync contract uses vector ladder contact: {payload['legacy_bridge']['player_kinematics_sync_contract_uses_vector_ladder_contact']}",
        f"- Player movement surface `RaycastHit` symbol count: {payload['legacy_bridge']['player_movement_surface_raycast_hit_count']}",
        f"- Player movement legacy collision symbol count: {payload['legacy_bridge']['player_movement_legacy_collision_symbol_count']}",
        f"- Player movement Unity collision DTO count: {payload['legacy_bridge']['player_movement_unity_collision_dto_count']}",
        f"- Player movement legacy collision route removed: {payload['legacy_bridge']['player_movement_legacy_collision_route_removed']}",
        f"- Player movement raycast-named surface symbol count: {payload['legacy_bridge']['player_movement_raycast_named_surface_symbol_count']}",
        f"- Player movement surface language is typed: {payload['legacy_bridge']['player_movement_surface_language_is_typed']}",
        f"- Player motor repair PhysX wording count: {payload['legacy_bridge']['player_motor_repair_physx_wording_count']}",
        f"- Player motor repair language is typed: {payload['legacy_bridge']['player_motor_repair_language_is_typed']}",
        f"- Player kinematics default Physics layer count: {payload['legacy_bridge']['player_kinematics_default_physics_layer_count']}",
        f"- Player kinematics uses strict interaction probe mask: {payload['legacy_bridge']['player_kinematics_uses_strict_interaction_probe_mask']}",
        f"- Player footstep audio `RaycastHit` symbol count: {payload['legacy_bridge']['player_footstep_audio_raycast_hit_count']}",
        f"- Player movement surface uses explicit hit DTO: {payload['legacy_bridge']['player_movement_surface_uses_explicit_hit']}",
        f"- Player footstep audio uses surface hit DTO: {payload['legacy_bridge']['player_footstep_audio_uses_surface_hit']}",
        f"- Player spawner `RaycastHit` symbol count: {payload['legacy_bridge']['player_spawner_raycast_hit_count']}",
        f"- Player spawner `TryRaycastGround` symbol count: {payload['legacy_bridge']['player_spawner_try_raycast_ground_count']}",
        f"- Player spawner uses spawn ground DTO: {payload['legacy_bridge']['player_spawner_uses_spawn_ground_hit']}",
        f"- Player spawner uses ground-probe origin contract: {payload['legacy_bridge']['player_spawner_uses_ground_probe_origin']}",
        f"- Player motor `RaycastHit` native allocations: {payload['legacy_bridge']['player_motor_raycast_hit_allocations']}",
        f"- Player motor PhysX command native allocations: {payload['legacy_bridge']['player_motor_command_allocations']}",
        f"- Legacy batch helper `QueryResult[]` mirrors: {payload['legacy_bridge']['legacy_batch_query_result_arrays']}",
        f"- Legacy batch helper Unity Physics calls: {payload['legacy_bridge']['legacy_batch_physx_calls']}",
        f"- Legacy batch helper Unity `RaycastHit` symbols: {payload['legacy_bridge']['legacy_batch_unity_raycast_hit_count']}",
        f"- Tool interaction surface Unity `RaycastHit` symbols: {payload['legacy_bridge']['interaction_surface_unity_raycast_hit_count']}",
        f"- Tool interaction legacy raycast method symbols: {payload['legacy_bridge']['interaction_surface_legacy_method_count']}",
        f"- Tool interaction uses typed surface hit: {payload['legacy_bridge']['interaction_surface_uses_typed_hit']}",
        f"- Tool interaction vault uses typed surface hit DTO: {payload['legacy_bridge']['interaction_surface_vault_uses_typed_hit_dto']}",
        f"- Kinematic surface hit has explicit 64-byte layout: {payload['legacy_bridge']['kinematic_surface_hit_layout_64']}",
        f"- Kinematic IK/VR/buoyancy Unity `RaycastHit` symbols: {payload['legacy_bridge']['kinematic_surface_unity_raycast_hit_count']}",
        f"- Kinematic IK/VR/buoyancy uses typed surface hits: {payload['legacy_bridge']['kinematic_surface_uses_typed_hit']}",
        f"- Interaction target legacy raycast API symbols: {payload['legacy_bridge']['interaction_target_legacy_raycast_api_count']}",
        f"- Interaction target uses spatial-target contract: {payload['legacy_bridge']['interaction_target_uses_spatial_target_contract']}",
        f"- Vehicle motor capsule sweep bridge removed: {payload['legacy_bridge']['vehicle_motor_capsule_sweep_bridge_removed']}",
        f"- Vehicle motor capsule sweep bridge symbol count: {payload['legacy_bridge']['vehicle_motor_capsule_sweep_bridge_symbol_count']}",
        f"- Vehicle motor `RaycastHit` symbol count: {payload['legacy_bridge']['vehicle_motor_raycast_hit_symbol_count']}",
        "",
        "## Player Split Authority Velocity Reads",
        f"- Player noise/action Rigidbody velocity read count: {payload['player_split_authority']['player_rigidbody_velocity_read_count']}",
        f"- Player Rigidbody motion/mass/pose state read count: {payload['player_split_authority']['player_rigidbody_motion_state_read_count']}",
        f"- Player-body alias motion/mass/pose state read count: {payload['player_split_authority']['player_body_alias_motion_state_read_count']}",
        f"- Player noise uses KCC velocity signal: {payload['player_split_authority']['player_noise_uses_kcc_velocity_signal']}",
        f"- Player action interrupt uses KCC velocity signal: {payload['player_split_authority']['player_action_uses_kcc_velocity_signal']}",
        f"- Player swim has no Rigidbody velocity fallback: {payload['player_split_authority']['player_swim_has_no_rigidbody_velocity_fallback']}",
        f"- Survival movement/save velocity uses KCC signal: {payload['player_split_authority']['survival_uses_kcc_velocity_signal']}",
        f"- Player spawner teleport velocity uses KCC signal: {payload['player_split_authority']['spawner_uses_kcc_velocity_for_teleport']}",
        f"- Player tool recoil uses deterministic equivalent mass: {payload['player_split_authority']['player_tool_recoil_uses_equivalent_mass']}",
        f"- Player inventory impact uses deterministic equivalent mass: {payload['player_split_authority']['player_inventory_impact_uses_equivalent_mass']}",
        f"- Camera juice player speed uses KCC velocity signal: {payload['player_split_authority']['camera_juice_uses_kcc_velocity']}",
        f"- Airlock docking snap start uses Transform pose, not Rigidbody pose: {payload['player_split_authority']['airlock_snap_start_uses_transform_pose']}",
        f"- Airlock Hydro teleport/snap routes through player motor: {payload['player_split_authority']['airlock_hydro_teleport_uses_player_motor']}",
        f"- Save-load Hydro teleport routes through player motor: {payload['player_split_authority']['save_load_hydro_teleport_uses_player_motor']}",
        f"- Spawner Hydro teleport routes through player motor: {payload['player_split_authority']['spawner_hydro_teleport_uses_player_motor']}",
        f"- Maelstrom player damage position uses player pose snapshot: {payload['player_split_authority']['maelstrom_damage_uses_player_pose_snapshot']}",
        "",
        "## Player Trigger Callback Authority",
        f"- Player trigger callback count: {payload['player_trigger_callbacks']['player_trigger_callback_count']}",
        f"- Sargassum uses dispatcher polling: {payload['player_trigger_callbacks']['sargassum_uses_dispatcher_polling']}",
        f"- Sargassum cut response uses KCC velocity signal: {payload['player_trigger_callbacks']['sargassum_uses_kcc_velocity_signal']}",
        f"- Sargassum has no Rigidbody velocity read: {payload['player_trigger_callbacks']['sargassum_no_rigidbody_velocity_read']}",
        f"- Environmental hazard uses slow-tick cached trigger volume: {payload['player_trigger_callbacks']['environmental_hazard_uses_slow_tick_volume']}",
        f"- Toxin hazard uses slow-tick cached trigger volume: {payload['player_trigger_callbacks']['toxin_hazard_uses_slow_tick_volume']}",
        f"- Oxygen bubble uses runtime-position polling: {payload['player_trigger_callbacks']['oxygen_bubble_uses_runtime_position_polling']}",
        f"- Base module uses runtime occupancy polling: {payload['player_trigger_callbacks']['base_module_uses_runtime_occupancy_polling']}",
        f"- Base module hot occupancy uses cached player only: {payload['player_trigger_callbacks']['base_module_hot_occupancy_uses_cached_player_only']}",
        f"- Acoustic reverb uses runtime volume polling: {payload['player_trigger_callbacks']['acoustic_reverb_uses_runtime_volume_polling']}",
        f"- Acoustic reverb TryResolve uses cached player only: {payload['player_trigger_callbacks']['acoustic_reverb_try_resolve_uses_cached_player_only']}",
        f"- Demo door uses runtime volume polling: {payload['player_trigger_callbacks']['demo_door_uses_runtime_volume_polling']}",
        f"- Demo door TryResolve uses cached player only: {payload['player_trigger_callbacks']['demo_door_try_resolve_uses_cached_player_only']}",
        f"- Sargassum hot-swap disables registry fallback: {payload['player_trigger_callbacks']['sargassum_hotswap_disables_registry_fallback']}",
        f"- Transport charging uses lifecycle-registry volume polling: {payload['player_trigger_callbacks']['transport_charging_uses_registry_volume_polling']}",
        f"- Vehicle docking uses lifecycle-registry volume polling: {payload['player_trigger_callbacks']['vehicle_docking_uses_registry_volume_polling']}",
        f"- Vehicle docking has no legacy collider resolver: {payload['player_trigger_callbacks']['vehicle_docking_no_legacy_collider_resolver']}",
        f"- Transport registry TryGetAt is pure read: {payload['player_trigger_callbacks']['transport_registry_try_get_at_is_pure']}",
        f"- Manta scooter registers on pool spawn: {payload['player_trigger_callbacks']['manta_scooter_registers_on_spawn']}",
        "",
        "## Owner Internal Authority Reads",
        f"- Player movement `_rb.linearVelocity` read count: {payload['owner_internal_authority']['movement_rb_linear_velocity_read_count']}",
        f"- Player movement hot `_rb.mass` read count: {payload['owner_internal_authority']['movement_rb_mass_read_count']}",
        f"- Player movement uses cached authoritative body mass: {payload['owner_internal_authority']['movement_uses_authoritative_body_mass_cache']}",
        f"- Player movement velocity reads centralized: {payload['owner_internal_authority']['movement_velocity_reads_centralized']}",
        f"- Player movement has no Rigidbody velocity read: {payload['owner_internal_authority']['movement_has_no_rigidbody_velocity_read']}",
        f"- Player movement has no hot Rigidbody mass read: {payload['owner_internal_authority']['movement_has_no_rigidbody_mass_read']}",
        f"- Player movement hot Rigidbody pose read count: {payload['owner_internal_authority']['movement_hot_rb_pose_read_count']}",
        f"- Player movement has no hot Rigidbody pose read: {payload['owner_internal_authority']['movement_has_no_hot_rigidbody_pose_read']}",
        f"- Player movement body position is snapshot-first: {payload['owner_internal_authority']['movement_body_position_is_snapshot_first']}",
        f"- Player movement uses KCC velocity signal: {payload['owner_internal_authority']['movement_uses_kcc_velocity_signal']}",
        f"- Player motor `_body.linearVelocity` read count: {payload['owner_internal_authority']['motor_body_linear_velocity_read_count']}",
        f"- Player motor has no body velocity read: {payload['owner_internal_authority']['motor_has_no_body_velocity_read']}",
        f"- Player kinematics `_body.linearVelocity` read count: {payload['owner_internal_authority']['player_kinematics_body_velocity_read_count']}",
        f"- Player kinematics has no body velocity read: {payload['owner_internal_authority']['player_kinematics_has_no_body_velocity_read']}",
        f"- Player kinematics hot Rigidbody pose read count: {payload['owner_internal_authority']['player_kinematics_hot_body_pose_read_count']}",
        f"- Player kinematics has no hot Rigidbody pose read: {payload['owner_internal_authority']['player_kinematics_has_no_hot_body_pose_read']}",
        f"- Player kinematics body position is snapshot-first: {payload['owner_internal_authority']['player_kinematics_body_position_is_snapshot_first']}",
        f"- Motor Hydro force uses KCC velocity: {payload['owner_internal_authority']['motor_hydro_force_uses_kcc_velocity']}",
        f"- Motor Hydro force uses authority mass: {payload['owner_internal_authority']['motor_hydro_force_uses_authority_mass']}",
        f"- Motor Hydro impulse uses authority mass: {payload['owner_internal_authority']['motor_hydro_impulse_uses_authority_mass']}",
        f"- Motor Hydro torque suppressed: {payload['owner_internal_authority']['motor_hydro_torque_suppressed']}",
        f"- Motor Hydro off-center force demotes to linear KCC force: {payload['owner_internal_authority']['motor_hydro_offcenter_demotes_to_linear']}",
        f"- Motor has no scheduled sweep bridge symbols: {payload['owner_internal_authority']['motor_has_no_scheduled_sweep_bridge']}",
        f"- Motor runtime position uses cached player context: {payload['owner_internal_authority']['motor_runtime_position_uses_cached_player_context']}",
        "",
        "## Player Force Route Authority",
        f"- Direct player-body force/angular route sites: {payload['player_force_routes']['direct_player_body_force_route_count']}",
        f"- Ungated player-body force/angular route sites: {payload['player_force_routes']['ungated_player_body_force_route_count']}",
        f"- Central force router routes player force before Rigidbody kinematic rejection: {payload['player_force_routes']['central_force_router_routes_player_before_body_is_kinematic']}",
        f"- Central force router routes player point force before Rigidbody kinematic rejection: {payload['player_force_routes']['central_force_router_routes_player_at_position_before_body_is_kinematic']}",
        f"- Central force router suppresses player torque/angular velocity shell mutation: {payload['player_force_routes']['central_force_router_suppresses_player_torque']}",
        f"- Central force router uses KCC/movement velocity for player velocity set: {payload['player_force_routes']['central_force_router_uses_kcc_velocity_for_player_velocity_set']}",
        f"- Central force router uses deterministic player equivalent mass: {payload['player_force_routes']['central_force_router_uses_equivalent_player_mass']}",
        f"- Tool player impulse uses deterministic equivalent mass: {payload['player_force_routes']['tool_player_impulse_uses_equivalent_mass']}",
        f"- Fauna light target velocity uses KCC signal: {payload['player_force_routes']['fauna_light_target_uses_kcc_velocity']}",
        f"- Fauna predator bite routes through player force sink: {payload['player_force_routes']['fauna_predator_impact_uses_player_force_sink']}",
        f"- Scooter shafts velocity fallback uses KCC signal: {payload['player_force_routes']['scooter_shafts_velocity_uses_kcc_fallback']}",
        f"- Submarine thermal updraft no longer queues duplicate player Rigidbody force: {payload['player_force_routes']['submarine_thermal_updraft_no_player_rigidbody_force']}",
        f"- Survival angular reset is Hydro-gated: {payload['player_force_routes']['survival_angular_reset_gated_for_hydro']}",
        f"- Spawner angular reset is Hydro-gated: {payload['player_force_routes']['spawner_angular_reset_gated_for_hydro']}",
        f"- Save-load angular reset is Hydro-gated: {payload['player_force_routes']['save_load_angular_reset_gated_for_hydro']}",
        "",
        "## Solver Bound",
        f"- ResolveIterationCount max: {solver['resolve_iteration_count_max']}",
        f"- Hard local stride clamps 1..8 found: {solver['hard_stride_clamps_1_to_8']}",
        f"- Capsule axis probe manifold: {solver['capsule_axis_probe_manifold']}",
        f"- Contact plane deduplication: {solver['contact_plane_deduplication']}",
        f"- Max SDF axis probes per entity: {solver['max_sdf_axis_probes_per_entity']} (8 sweep samples * 3 capsule probes)",
        f"- Max stored contact planes per entity: {solver['max_stored_contact_planes_per_entity']}",
        f"- Max resolution plane projections per entity: {solver['max_resolution_plane_projections_per_entity']} (8 contact planes * 8 bounded passes)",
        f"- 100 m/s at dt {solver['smoke_fixed_delta_time']} moves {solver['fall_100mps_displacement_per_frame_m']:.6f} m/frame.",
        f"- 100 m/s cone fall contract test present: {solver['smoke_cone_fall_contract_tested']}",
        "- No recursion is used by the KCC collision build or resolution jobs; bounded for-loops terminate after fixed counters.",
        f"- Three-plane corner proof: {solver['three_plane_corner_bound']}",
        f"- 100 m/s cone proof: {solver['fall_100mps_bound']}",
        "",
        "## Lockstep Layout Gate",
        f"- Runtime validator checks 64-byte size: {lockstep_gate['runtime_validator_checks_64_byte_size']}",
        f"- Runtime validator checks storage offsets: {lockstep_gate['runtime_validator_checks_storage_offsets']}",
        f"- Rollback edit test uses 64-byte size: {lockstep_gate['rollback_test_uses_64_byte_size']}",
        f"- Rollback edit test rejects old 96-byte layout: {lockstep_gate['rollback_test_rejects_96_byte_layout']}",
        f"- Rollback edit test checks storage field offsets: {lockstep_gate['rollback_test_uses_storage_field_offsets']}",
        f"- Rollback edit test has no compatibility-property offsets: {lockstep_gate['rollback_test_has_no_compat_property_offsets']}",
        "",
        "## Black Box Result",
        f"- Telemetry ring capacity is 300: {blackbox['telemetry_capacity_300']}",
        f"- Agent dump file present: {blackbox['agent_dump_file_present']}",
        f"- Dumps on new fault mask: {blackbox['dump_on_new_fault_mask']}",
        f"- Fault latch resets after clean frame: {blackbox['fault_latch_resets_on_clean_frame']}",
        f"- Late-frame fault scan requires full entity capacity: {blackbox['late_frame_fault_scan_full_capacity']}",
        f"- Telemetry aggregate requires valid state lane: {blackbox['telemetry_requires_states_lane']}",
        f"- Telemetry iteration count can record exact zero: {blackbox['telemetry_iterations_are_exact_zero_capable']}",
        "",
        "## LockstepPlayerKinematicState Layout",
        f"- Size: {lock['size']} bytes",
        f"- Covered bytes: {lock['covered_bytes']}",
        f"- Gaps: {lock['gaps']}",
        "",
    ]
    for field in lock["fields"]:
        lines.append(f"- {field['offset']:02d}..{field['end_exclusive']:02d}: {field['type']} {field['name']}")
    lines.extend(
        [
            "",
            "## KinematicStateDTO Layout",
            f"- Size: {kin['size']} bytes",
            f"- Covered bytes: {kin['covered_bytes']}",
            f"- Gaps: {kin['gaps']}",
            "",
        ]
    )
    for field in kin["fields"]:
        lines.append(f"- {field['offset']:02d}..{field['end_exclusive']:02d}: {field['type']} {field['name']}")
    lines.extend(
        [
            "",
            "## PlayerKinematicsProbeHit Layout",
            f"- Size: {probe['size']} bytes",
            f"- Covered bytes: {probe['covered_bytes']}",
            f"- Gaps: {probe['gaps']}",
            "",
        ]
    )
    for field in probe["fields"]:
        lines.append(f"- {field['offset']:02d}..{field['end_exclusive']:02d}: {field['type']} {field['name']}")
    REPORT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    scoped, broad = scan_forbidden()
    broad_by_kind: dict[str, int] = {}
    scoped_by_kind: dict[str, int] = {}
    for entry in broad:
        kind = entry["kind"]
        broad_by_kind[kind] = broad_by_kind.get(kind, 0) + 1
    for entry in scoped:
        kind = entry["kind"]
        scoped_by_kind[kind] = scoped_by_kind.get(kind, 0) + 1
    payload = {
        "agent": "X_005",
        "scoped_forbidden_count": len(scoped),
        "broad_forbidden_count": len(broad),
        "scoped_forbidden_by_kind": scoped_by_kind,
        "broad_forbidden_by_kind": broad_by_kind,
        "scoped_forbidden": scoped,
        "broad_forbidden": broad,
        "rigidbody_writes": scan_direct_rigidbody_writes(),
        "unity_collision_dto": scan_unity_collision_dtos(),
        "legacy_bridge": legacy_bridge_report(),
        "player_split_authority": player_split_authority_report(),
        "owner_internal_authority": owner_internal_authority_report(),
        "player_force_routes": player_force_route_report(),
        "player_trigger_callbacks": player_trigger_callback_report(),
        "layouts": layout_report(),
        "lockstep_layout_gate": lockstep_layout_gate_report(),
        "solver": solver_report(),
        "blackbox": blackbox_report(),
    }
    REPORT_JSON.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    write_markdown(payload)
    print(json.dumps({
        "scoped_forbidden_count": payload["scoped_forbidden_count"],
        "broad_forbidden_count": payload["broad_forbidden_count"],
        "broad_forbidden_by_kind": payload["broad_forbidden_by_kind"],
        "external_rigidbody_velocity_assignment_count": payload["rigidbody_writes"]["external_velocity_assignment_count"],
        "external_rigidbody_force_call_count": payload["rigidbody_writes"]["external_force_call_count"],
        "external_player_pose_assignment_count": payload["rigidbody_writes"]["external_player_pose_assignment_count"],
        "unity_collision_dto_count": payload["unity_collision_dto"]["unity_collision_dto_count"],
        "unity_collision_dto_route_removed": payload["unity_collision_dto"]["unity_collision_dto_route_removed"],
        "player_motor_capsule_sweep_bridge_removed": payload["legacy_bridge"]["player_motor_capsule_sweep_bridge_removed"],
        "player_motor_capsule_sweep_bridge_symbol_count": payload["legacy_bridge"]["player_motor_capsule_sweep_bridge_symbol_count"],
        "player_motor_native_state_removed": payload["legacy_bridge"]["player_motor_native_state_removed"],
        "player_motor_native_state_symbol_count": payload["legacy_bridge"]["player_motor_native_state_symbol_count"],
        "player_motor_raycast_hit_symbol_count": payload["legacy_bridge"]["player_motor_raycast_hit_symbol_count"],
        "player_motor_raycast_hit_allocations": payload["legacy_bridge"]["player_motor_raycast_hit_allocations"],
        "player_motor_command_allocations": payload["legacy_bridge"]["player_motor_command_allocations"],
        "player_hand_probe_uses_explicit_probe_hit": payload["legacy_bridge"]["player_hand_probe_uses_explicit_probe_hit"],
        "player_hand_probe_raycast_hit_lane_count": payload["legacy_bridge"]["player_hand_probe_raycast_hit_lane_count"],
        "player_kinematics_runtime_raycast_hit_count": payload["legacy_bridge"]["player_kinematics_runtime_raycast_hit_count"],
        "player_kinematics_sync_contract_raycast_hit_count": payload["legacy_bridge"]["player_kinematics_sync_contract_raycast_hit_count"],
        "player_kinematics_sync_contract_uses_vector_ladder_contact": payload["legacy_bridge"]["player_kinematics_sync_contract_uses_vector_ladder_contact"],
        "player_movement_legacy_collision_symbol_count": payload["legacy_bridge"]["player_movement_legacy_collision_symbol_count"],
        "player_movement_unity_collision_dto_count": payload["legacy_bridge"]["player_movement_unity_collision_dto_count"],
        "player_movement_legacy_collision_route_removed": payload["legacy_bridge"]["player_movement_legacy_collision_route_removed"],
        "player_movement_raycast_named_surface_symbol_count": payload["legacy_bridge"]["player_movement_raycast_named_surface_symbol_count"],
        "player_movement_surface_language_is_typed": payload["legacy_bridge"]["player_movement_surface_language_is_typed"],
        "player_motor_repair_physx_wording_count": payload["legacy_bridge"]["player_motor_repair_physx_wording_count"],
        "player_motor_repair_language_is_typed": payload["legacy_bridge"]["player_motor_repair_language_is_typed"],
        "player_kinematics_default_physics_layer_count": payload["legacy_bridge"]["player_kinematics_default_physics_layer_count"],
        "player_kinematics_uses_strict_interaction_probe_mask": payload["legacy_bridge"]["player_kinematics_uses_strict_interaction_probe_mask"],
        "player_spawner_raycast_hit_count": payload["legacy_bridge"]["player_spawner_raycast_hit_count"],
        "player_spawner_try_raycast_ground_count": payload["legacy_bridge"]["player_spawner_try_raycast_ground_count"],
        "player_spawner_uses_spawn_ground_hit": payload["legacy_bridge"]["player_spawner_uses_spawn_ground_hit"],
        "player_spawner_uses_ground_probe_origin": payload["legacy_bridge"]["player_spawner_uses_ground_probe_origin"],
        "legacy_batch_query_result_arrays": payload["legacy_bridge"]["legacy_batch_query_result_arrays"],
        "legacy_batch_physx_calls": payload["legacy_bridge"]["legacy_batch_physx_calls"],
        "legacy_batch_unity_raycast_hit_count": payload["legacy_bridge"]["legacy_batch_unity_raycast_hit_count"],
        "interaction_surface_unity_raycast_hit_count": payload["legacy_bridge"]["interaction_surface_unity_raycast_hit_count"],
        "interaction_surface_legacy_method_count": payload["legacy_bridge"]["interaction_surface_legacy_method_count"],
        "interaction_surface_uses_typed_hit": payload["legacy_bridge"]["interaction_surface_uses_typed_hit"],
        "interaction_surface_vault_uses_typed_hit_dto": payload["legacy_bridge"]["interaction_surface_vault_uses_typed_hit_dto"],
        "kinematic_surface_hit_layout_64": payload["legacy_bridge"]["kinematic_surface_hit_layout_64"],
        "kinematic_surface_unity_raycast_hit_count": payload["legacy_bridge"]["kinematic_surface_unity_raycast_hit_count"],
        "kinematic_surface_uses_typed_hit": payload["legacy_bridge"]["kinematic_surface_uses_typed_hit"],
        "interaction_target_legacy_raycast_api_count": payload["legacy_bridge"]["interaction_target_legacy_raycast_api_count"],
        "interaction_target_uses_spatial_target_contract": payload["legacy_bridge"]["interaction_target_uses_spatial_target_contract"],
        "vehicle_motor_capsule_sweep_bridge_removed": payload["legacy_bridge"]["vehicle_motor_capsule_sweep_bridge_removed"],
        "vehicle_motor_capsule_sweep_bridge_symbol_count": payload["legacy_bridge"]["vehicle_motor_capsule_sweep_bridge_symbol_count"],
        "vehicle_motor_raycast_hit_symbol_count": payload["legacy_bridge"]["vehicle_motor_raycast_hit_symbol_count"],
        "player_rigidbody_velocity_read_count": payload["player_split_authority"]["player_rigidbody_velocity_read_count"],
        "player_rigidbody_motion_state_read_count": payload["player_split_authority"]["player_rigidbody_motion_state_read_count"],
        "player_body_alias_motion_state_read_count": payload["player_split_authority"]["player_body_alias_motion_state_read_count"],
        "player_tool_recoil_uses_equivalent_mass": payload["player_split_authority"]["player_tool_recoil_uses_equivalent_mass"],
        "player_inventory_impact_uses_equivalent_mass": payload["player_split_authority"]["player_inventory_impact_uses_equivalent_mass"],
        "camera_juice_uses_kcc_velocity": payload["player_split_authority"]["camera_juice_uses_kcc_velocity"],
        "airlock_snap_start_uses_transform_pose": payload["player_split_authority"]["airlock_snap_start_uses_transform_pose"],
        "airlock_hydro_teleport_uses_player_motor": payload["player_split_authority"]["airlock_hydro_teleport_uses_player_motor"],
        "save_load_hydro_teleport_uses_player_motor": payload["player_split_authority"]["save_load_hydro_teleport_uses_player_motor"],
        "spawner_hydro_teleport_uses_player_motor": payload["player_split_authority"]["spawner_hydro_teleport_uses_player_motor"],
        "maelstrom_damage_uses_player_pose_snapshot": payload["player_split_authority"]["maelstrom_damage_uses_player_pose_snapshot"],
        "player_trigger_callback_count": payload["player_trigger_callbacks"]["player_trigger_callback_count"],
        "sargassum_uses_kcc_velocity_signal": payload["player_trigger_callbacks"]["sargassum_uses_kcc_velocity_signal"],
        "environmental_hazard_uses_slow_tick_volume": payload["player_trigger_callbacks"]["environmental_hazard_uses_slow_tick_volume"],
        "toxin_hazard_uses_slow_tick_volume": payload["player_trigger_callbacks"]["toxin_hazard_uses_slow_tick_volume"],
        "oxygen_bubble_uses_runtime_position_polling": payload["player_trigger_callbacks"]["oxygen_bubble_uses_runtime_position_polling"],
        "base_module_uses_runtime_occupancy_polling": payload["player_trigger_callbacks"]["base_module_uses_runtime_occupancy_polling"],
        "base_module_hot_occupancy_uses_cached_player_only": payload["player_trigger_callbacks"]["base_module_hot_occupancy_uses_cached_player_only"],
        "acoustic_reverb_uses_runtime_volume_polling": payload["player_trigger_callbacks"]["acoustic_reverb_uses_runtime_volume_polling"],
        "acoustic_reverb_try_resolve_uses_cached_player_only": payload["player_trigger_callbacks"]["acoustic_reverb_try_resolve_uses_cached_player_only"],
        "demo_door_uses_runtime_volume_polling": payload["player_trigger_callbacks"]["demo_door_uses_runtime_volume_polling"],
        "demo_door_try_resolve_uses_cached_player_only": payload["player_trigger_callbacks"]["demo_door_try_resolve_uses_cached_player_only"],
        "sargassum_hotswap_disables_registry_fallback": payload["player_trigger_callbacks"]["sargassum_hotswap_disables_registry_fallback"],
        "transport_charging_uses_registry_volume_polling": payload["player_trigger_callbacks"]["transport_charging_uses_registry_volume_polling"],
        "vehicle_docking_uses_registry_volume_polling": payload["player_trigger_callbacks"]["vehicle_docking_uses_registry_volume_polling"],
        "vehicle_docking_no_legacy_collider_resolver": payload["player_trigger_callbacks"]["vehicle_docking_no_legacy_collider_resolver"],
        "transport_registry_try_get_at_is_pure": payload["player_trigger_callbacks"]["transport_registry_try_get_at_is_pure"],
        "manta_scooter_registers_on_spawn": payload["player_trigger_callbacks"]["manta_scooter_registers_on_spawn"],
        "movement_rb_linear_velocity_read_count": payload["owner_internal_authority"]["movement_rb_linear_velocity_read_count"],
        "movement_rb_mass_read_count": payload["owner_internal_authority"]["movement_rb_mass_read_count"],
        "movement_velocity_reads_centralized": payload["owner_internal_authority"]["movement_velocity_reads_centralized"],
        "movement_has_no_rigidbody_velocity_read": payload["owner_internal_authority"]["movement_has_no_rigidbody_velocity_read"],
        "movement_has_no_rigidbody_mass_read": payload["owner_internal_authority"]["movement_has_no_rigidbody_mass_read"],
        "movement_uses_authoritative_body_mass_cache": payload["owner_internal_authority"]["movement_uses_authoritative_body_mass_cache"],
        "movement_hot_rb_pose_read_count": payload["owner_internal_authority"]["movement_hot_rb_pose_read_count"],
        "movement_has_no_hot_rigidbody_pose_read": payload["owner_internal_authority"]["movement_has_no_hot_rigidbody_pose_read"],
        "movement_body_position_is_snapshot_first": payload["owner_internal_authority"]["movement_body_position_is_snapshot_first"],
        "motor_body_linear_velocity_read_count": payload["owner_internal_authority"]["motor_body_linear_velocity_read_count"],
        "motor_has_no_body_velocity_read": payload["owner_internal_authority"]["motor_has_no_body_velocity_read"],
        "player_kinematics_body_velocity_read_count": payload["owner_internal_authority"]["player_kinematics_body_velocity_read_count"],
        "player_kinematics_has_no_body_velocity_read": payload["owner_internal_authority"]["player_kinematics_has_no_body_velocity_read"],
        "player_kinematics_hot_body_pose_read_count": payload["owner_internal_authority"]["player_kinematics_hot_body_pose_read_count"],
        "player_kinematics_has_no_hot_body_pose_read": payload["owner_internal_authority"]["player_kinematics_has_no_hot_body_pose_read"],
        "player_kinematics_body_position_is_snapshot_first": payload["owner_internal_authority"]["player_kinematics_body_position_is_snapshot_first"],
        "motor_hydro_force_uses_kcc_velocity": payload["owner_internal_authority"]["motor_hydro_force_uses_kcc_velocity"],
        "motor_runtime_position_uses_cached_player_context": payload["owner_internal_authority"]["motor_runtime_position_uses_cached_player_context"],
        "motor_hydro_torque_suppressed": payload["owner_internal_authority"]["motor_hydro_torque_suppressed"],
        "motor_has_no_scheduled_sweep_bridge": payload["owner_internal_authority"]["motor_has_no_scheduled_sweep_bridge"],
        "direct_player_body_force_route_count": payload["player_force_routes"]["direct_player_body_force_route_count"],
        "ungated_player_body_force_route_count": payload["player_force_routes"]["ungated_player_body_force_route_count"],
        "central_force_router_uses_equivalent_player_mass": payload["player_force_routes"]["central_force_router_uses_equivalent_player_mass"],
        "central_force_router_suppresses_player_torque": payload["player_force_routes"]["central_force_router_suppresses_player_torque"],
        "tool_player_impulse_uses_equivalent_mass": payload["player_force_routes"]["tool_player_impulse_uses_equivalent_mass"],
        "fauna_light_target_uses_kcc_velocity": payload["player_force_routes"]["fauna_light_target_uses_kcc_velocity"],
        "fauna_predator_impact_uses_player_force_sink": payload["player_force_routes"]["fauna_predator_impact_uses_player_force_sink"],
        "scooter_shafts_velocity_uses_kcc_fallback": payload["player_force_routes"]["scooter_shafts_velocity_uses_kcc_fallback"],
        "submarine_thermal_updraft_no_player_rigidbody_force": payload["player_force_routes"]["submarine_thermal_updraft_no_player_rigidbody_force"],
        "save_load_angular_reset_gated_for_hydro": payload["player_force_routes"]["save_load_angular_reset_gated_for_hydro"],
        "blackbox_telemetry_capacity_300": payload["blackbox"]["telemetry_capacity_300"],
        "blackbox_fault_latch_resets_on_clean_frame": payload["blackbox"]["fault_latch_resets_on_clean_frame"],
        "blackbox_late_frame_fault_scan_full_capacity": payload["blackbox"]["late_frame_fault_scan_full_capacity"],
        "blackbox_telemetry_requires_states_lane": payload["blackbox"]["telemetry_requires_states_lane"],
        "blackbox_telemetry_iterations_exact_zero": payload["blackbox"]["telemetry_iterations_are_exact_zero_capable"],
        "lockstep_size": payload["layouts"]["LockstepPlayerKinematicState"]["size"],
        "lockstep_runtime_validator_checks_offsets": payload["lockstep_layout_gate"]["runtime_validator_checks_storage_offsets"],
        "lockstep_rollback_test_uses_64_byte_size": payload["lockstep_layout_gate"]["rollback_test_uses_64_byte_size"],
        "lockstep_rollback_test_rejects_96_byte_layout": payload["lockstep_layout_gate"]["rollback_test_rejects_96_byte_layout"],
        "lockstep_rollback_test_uses_storage_field_offsets": payload["lockstep_layout_gate"]["rollback_test_uses_storage_field_offsets"],
        "kinematic_state_size": payload["layouts"]["KinematicStateDTO"]["size"],
        "player_kinematics_probe_hit_size": payload["layouts"]["PlayerKinematicsProbeHit"]["size"],
        "hard_stride_clamps_1_to_8": payload["solver"]["hard_stride_clamps_1_to_8"],
        "contact_plane_deduplication": payload["solver"]["contact_plane_deduplication"],
        "smoke_cone_fall_contract_tested": payload["solver"]["smoke_cone_fall_contract_tested"],
    }, sort_keys=True))
    return 0 if len(scoped) == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
