#!/usr/bin/env python3
"""Validate visor trauma material profile CSV coverage against the Batch34 atlas slice contract."""

from __future__ import annotations

import csv
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TRAUMA_CSV = ROOT / "Assets/_Project/Data/Decals/visor_trauma_profiles.csv"
ALIAS_CSV = ROOT / "Assets/_Project/Data/Decals/visor_decal_profiles.csv"
SLICE_CONTRACT = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/TextureArrays/TX_B34_VisorTrauma_DecalArray_SliceContract.json"
)
RUNTIME_SOURCE = ROOT / "Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs"
DEFERRED_DECAL_PASS_SOURCE = ROOT / "Assets/_Project/Scripts/Visor/DeferredDecalPass.cs"
COMBAT_DAMAGE_SOURCE = ROOT / "Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs"
SIGNAL_BUS_SOURCE = ROOT / "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"
POWER_GRID_SOURCE = ROOT / "Assets/_Project/Scripts/PowerGrid.cs"
SCRIPT_ROOT = ROOT / "Assets/_Project/Scripts"
EXPECTED_HEADER = ["source", "atlasSlice", "lifetimeSeconds", "radiusMeters", "projectionDepthMeters"]
REQUIRED_SOURCE_BY_SLICE = {
    0: "scorch",
    1: "blood",
    2: "acid",
    3: "hull_dent",
    4: "glass_crack",
    5: "burn",
    6: "salt_crust",
    7: "glass_smudge",
    8: "barnacle_colony",
    9: "brine_flora_smear",
    10: "sponge_pore_stain",
    11: "spore_pod_smear",
    12: "egg_sac_membrane",
    13: "carcass_trace",
    14: "resource_nodule_trace",
    15: "data_core_circuit_trace",
}
RUNTIME_CONSTANT_BY_SOURCE = {
    "scorch": ("Scorch", 0),
    "blood": ("Blood", 1),
    "acid": ("Acid", 2),
    "hull_dent": ("HullDent", 3),
    "glass_crack": ("GlassCrack", 4),
    "burn": ("Burn", 5),
    "salt_crust": ("SaltCrust", 6),
    "glass_smudge": ("GlassSmudge", 7),
    "barnacle_colony": ("BarnacleColony", 8),
    "brine_flora_smear": ("BrineFloraSmear", 9),
    "sponge_pore_stain": ("SpongePoreStain", 10),
    "spore_pod_smear": ("SporePodSmear", 11),
    "egg_sac_membrane": ("EggSacMembrane", 12),
    "carcass_trace": ("CarcassTrace", 13),
    "resource_nodule_trace": ("ResourceNoduleTrace", 14),
    "data_core_circuit_trace": ("DataCoreCircuitTrace", 15),
}
GAMEPLAY_DAMAGE_MASK_CONSTANTS = {
    "Pressure": "GameplayDamagePressureMask",
    "Thermal": "GameplayDamageThermalMask",
    "Impact": "GameplayDamageImpactMask",
    "Parasite": "GameplayDamageParasiteMask",
    "Radioactive": "GameplayDamageRadioactiveMask",
    "Toxic": "GameplayDamageToxicMask",
    "Emp": "GameplayDamageEmpMask",
    "MicroFracture": "GameplayDamageMicroFractureMask",
}


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def load_rows(path: Path, errors: list[str]) -> list[dict[str, str]]:
    if not path.exists():
        errors.append(f"missing CSV: {display(path)}")
        return []

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != EXPECTED_HEADER:
            errors.append(f"{display(path)} header mismatch: {reader.fieldnames}")
            return []
        return list(reader)


def fnv1a_lower_ascii(token: str) -> int:
    value = 2166136261
    for byte in token.encode("ascii"):
        if 65 <= byte <= 90:
            byte += 32
        value ^= byte
        value = (value * 16777619) & 0xFFFFFFFF
    return value or 1


def source_hash_constant_name(runtime_constant: str) -> str:
    return f"{runtime_constant}ProfileSourceHash"


def validate_csv(path: Path, rows: list[dict[str, str]], errors: list[str]) -> None:
    seen_sources: set[str] = set()
    seen_slices: set[int] = set()
    for index, row in enumerate(rows, start=2):
        source = (row.get("source") or "").strip()
        if not source:
            errors.append(f"{display(path)} row {index}: empty source")
        if source in seen_sources:
            errors.append(f"{display(path)} row {index}: duplicate source token {source}")
        seen_sources.add(source)

        try:
            atlas_slice = int((row.get("atlasSlice") or "").strip())
        except ValueError:
            errors.append(f"{display(path)} row {index}: invalid atlasSlice {row.get('atlasSlice')}")
            continue
        if atlas_slice < 0 or atlas_slice > 15:
            errors.append(f"{display(path)} row {index}: atlasSlice out of range {atlas_slice}")
        if atlas_slice in seen_slices:
            errors.append(f"{display(path)} row {index}: duplicate atlasSlice {atlas_slice}")
        seen_slices.add(atlas_slice)

        for field, minimum, maximum in (
            ("lifetimeSeconds", 0.1, 60.0),
            ("radiusMeters", 0.025, 8.0),
            ("projectionDepthMeters", 0.025, 2.0),
        ):
            try:
                value = float((row.get(field) or "").strip())
            except ValueError:
                errors.append(f"{display(path)} row {index}: invalid {field} {row.get(field)}")
                continue
            if value < minimum or value > maximum:
                errors.append(f"{display(path)} row {index}: {field} out of range {value}")

    missing_slices = sorted(set(REQUIRED_SOURCE_BY_SLICE) - seen_slices)
    if missing_slices:
        errors.append(f"{display(path)} missing atlas slices: {missing_slices}")

    for atlas_slice, expected_source in REQUIRED_SOURCE_BY_SLICE.items():
        if expected_source not in seen_sources:
            errors.append(f"{display(path)} missing required source token for slice {atlas_slice}: {expected_source}")


def load_slice_contract(errors: list[str]) -> dict[int, str]:
    if not SLICE_CONTRACT.exists():
        errors.append(f"missing slice contract: {display(SLICE_CONTRACT)}")
        return {}

    payload = json.loads(SLICE_CONTRACT.read_text(encoding="utf-8-sig"))
    slices = payload.get("slices", []) or []
    contract_slices = {
        int(entry.get("slice")): str(entry.get("runtimeDecalType", "")).strip()
        for entry in slices
        if isinstance(entry.get("slice"), int)
    }
    if set(contract_slices) != set(REQUIRED_SOURCE_BY_SLICE):
        errors.append("slice contract does not cover the same 0..15 atlas slice range as visor CSV")
    return contract_slices


def parse_shifted_uint_constant(expression: str) -> int | None:
    text = expression.strip()
    shifted = re.fullmatch(r"1u\s*<<\s*(\d+)", text)
    if shifted:
        return 1 << int(shifted.group(1))
    literal = re.fullmatch(r"(\d+)u", text)
    if literal:
        return int(literal.group(1))
    return None


def load_combat_damage_type_constants(errors: list[str]) -> dict[str, int]:
    if not COMBAT_DAMAGE_SOURCE.exists():
        errors.append(f"missing combat damage source: {display(COMBAT_DAMAGE_SOURCE)}")
        return {}

    text = COMBAT_DAMAGE_SOURCE.read_text(encoding="utf-8-sig")
    match = re.search(r"public\s+static\s+class\s+CombatDamageTypes\s*\{(?P<body>.*?)\n\s*\}", text, re.DOTALL)
    if not match:
        errors.append("missing CombatDamageTypes class in combat source")
        return {}

    constants: dict[str, int] = {}
    for constant_match in re.finditer(
        r"public\s+const\s+uint\s+(?P<name>[A-Za-z0-9_]+)\s*=\s*(?P<value>[^;]+);",
        match.group("body"),
    ):
        parsed = parse_shifted_uint_constant(constant_match.group("value"))
        if parsed is not None:
            constants[constant_match.group("name")] = parsed
    return constants


def validate_runtime_bridge(errors: list[str], contract_slices: dict[int, str]) -> None:
    if not RUNTIME_SOURCE.exists():
        errors.append(f"missing runtime source: {display(RUNTIME_SOURCE)}")
        return

    text = RUNTIME_SOURCE.read_text(encoding="utf-8-sig")
    constants = {
        match.group("name"): int(match.group("value"))
        for match in re.finditer(
            r"public\s+const\s+uint\s+(?P<name>[A-Za-z0-9_]+)\s*=\s*(?P<value>\d+)u\s*;",
            text,
        )
    }
    profile_source_constants = {
        match.group("name"): int(match.group("value"), 16)
        for match in re.finditer(
            r"private\s+const\s+uint\s+(?P<name>[A-Za-z0-9_]+ProfileSourceHash)\s*=\s*0x(?P<value>[0-9A-Fa-f]+)u\s*;",
            text,
        )
    }
    gameplay_damage_constants = {
        match.group("name"): int(match.group("shift"))
        for match in re.finditer(
            r"private\s+const\s+uint\s+(?P<name>GameplayDamage[A-Za-z0-9_]+Mask)\s*=\s*1u\s*<<\s*(?P<shift>\d+)\s*;",
            text,
        )
    }
    combat_damage_constants = load_combat_damage_type_constants(errors)
    for combat_name, runtime_name in GAMEPLAY_DAMAGE_MASK_CONSTANTS.items():
        combat_value = combat_damage_constants.get(combat_name)
        runtime_shift = gameplay_damage_constants.get(runtime_name)
        runtime_value = None if runtime_shift is None else 1 << runtime_shift
        if combat_value is None:
            errors.append(f"missing CombatDamageTypes.{combat_name} source constant")
        elif runtime_value != combat_value:
            errors.append(f"runtime bridge {runtime_name} expected {combat_value}, got {runtime_value}")
    expected_runtime_entry_count = len(REQUIRED_SOURCE_BY_SLICE) + len(REQUIRED_SOURCE_BY_SLICE) - 1
    if f"DefaultBatch34MaterialProfileRuntimeEntries = {expected_runtime_entry_count}" not in text:
        errors.append(f"runtime default material profile entry count must be {expected_runtime_entry_count}")
    if "EnsureDefaultMaterialProfiles()" not in text:
        errors.append("runtime cold storage must seed default Batch34 material profiles without editor CSV IO")
    if "SeedDefaultMaterialProfiles(profiles)" not in text:
        errors.append("runtime default profile seeding path is missing")
    if "parsedProfiles < DefaultBatch34MaterialProfileRuntimeEntries" not in text:
        errors.append("editor CSV load must reject partial Batch34 material profile coverage")
    if "_materialProfileCount = SeedDefaultMaterialProfiles(profiles);" not in text:
        errors.append("partial CSV load must restore default material profiles before returning failure")
    if "TrySeedDefaultMaterialProfileWithAlias(" not in text:
        errors.append("runtime default material profiles must include compact material-hash aliases")
    for source, (constant_name, expected_value) in RUNTIME_CONSTANT_BY_SOURCE.items():
        actual_value = constants.get(constant_name)
        if actual_value != expected_value:
            errors.append(f"runtime constant {constant_name} expected {expected_value}, got {actual_value}")
        profile_constant = source_hash_constant_name(constant_name)
        expected_profile_hash = fnv1a_lower_ascii(source)
        actual_profile_hash = profile_source_constants.get(profile_constant)
        if actual_profile_hash != expected_profile_hash:
            errors.append(
                f"runtime source-hash constant {profile_constant} expected 0x{expected_profile_hash:08X}, got {actual_profile_hash}"
            )
        if expected_value != 0 and f'EqualsLowerAscii(sourceToken, "{source}")' not in text:
            errors.append(f"runtime CSV alias missing for source token {source}")
        if expected_value != 0 and f"DynamicDecalMaterialHashes.{constant_name}" not in text:
            errors.append(f"runtime CSV alias missing constant reference {constant_name}")

    if "TryResolveKnownMaterialProfileAlias(sourceToken, out uint aliasHash)" not in text:
        errors.append("ParseMaterialProfilesCsv does not insert known numeric material-hash aliases")
    if "aliasProfile.SourceHash = aliasHash;" not in text:
        errors.append("ParseMaterialProfilesCsv alias path does not overwrite SourceHash")
    if "if (materialHash >= AtlasSliceCount)" not in text:
        errors.append("ResolveDecalTypeFromMaterial must reject non-compact material hashes before variant mapping")
    if "Mix(materialHash) & DecalAtlasPackedMask" in text:
        errors.append("ResolveAtlasSliceFromMaterial must not map unknown material hashes to random Batch34 atlas slices")
    atlas_resolver_match = re.search(
        r"internal\s+static\s+uint\s+ResolveAtlasSliceFromMaterial\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not atlas_resolver_match:
        errors.append("missing ResolveAtlasSliceFromMaterial runtime resolver")
    else:
        atlas_body = atlas_resolver_match.group("body")
        if ": DynamicDecalMaterialHashes.Scorch" not in atlas_body:
            errors.append("ResolveAtlasSliceFromMaterial must route non-compact/bad hashes to Scorch slice 0")
    if "return PackDecalPayload(ResolveRequestDecalType(materialPayload), ResolveRequestAtlasSlice(materialPayload));" not in text:
        errors.append("ResolveRequestDecalPayload must normalize both packed and raw request material payloads")
    if "decalPayload = DynamicDecalVaultRuntime.ResolveRequestDecalPayload(request.MaterialHash);" not in text:
        errors.append("decal matrix generation must resolve request MaterialHash through ResolveRequestDecalPayload")
    if "TryResolveMaterialProfileForRequest(" not in text:
        errors.append("signal impact profile lookup must fall back from preferred/source hash to compact material hash")
    if "TryResolveMaterialProfile(preferredHash, profiles, profileCapacity, out profile)" not in text:
        errors.append("profile request lookup must try preferred/source hash first")
    if "TryResolveMaterialProfile(materialHash, profiles, profileCapacity, out profile)" not in text:
        errors.append("profile request lookup must fall back to material hash when preferred hash misses")
    if "ulong ingestKey," not in text:
        errors.append("signal impact enqueue must include ingestKey entropy for stable visual variation")
    if "(uint)ingestKey" not in text or "(uint)(ingestKey >> 32)" not in text:
        errors.append("signal impact StableSeed must mix both halves of the deterministic ingest key")
    if "request.StableSeed = Mix(materialHash ^ profileHash ^ frame);" in text:
        errors.append("signal impact StableSeed must not collapse to material/profile/frame only")
    enqueue_match = re.search(
        r"private\s+static\s+bool\s+TryEnqueueRequest\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not enqueue_match:
        errors.append("missing TryEnqueueRequest failure-path bridge")
    else:
        enqueue_body = enqueue_match.group("body")
        if "if (!IsInitializedForRead())" not in enqueue_body or "AccumulateDroppedIngress(1);" not in enqueue_body:
            errors.append("TryEnqueueRequest must count no-data/stale-owner enqueue drops before returning false")
    if "HighSpeedImpactSignal.ComposeMaterialHash" in text:
        errors.append("visor trauma high-speed bridge must not treat composed entity/material ids as Batch34 decal material hashes")
    if "signal.SourceHash ^ signal.TargetHash" in text:
        errors.append("visor trauma high-speed bridge must not use entity source/target hashes as material profile keys")
    required_bridge_tokens = (
        "ResolveHighSpeedImpactVisualMaterialHash(in signal)",
        "ResolveHighSpeedImpactProfileHash(in signal)",
        "ResolveCombatDamageVisualMaterialHash(in signal)",
        "ResolveCombatDamageProfileHash(in signal)",
        "ResolveGameplayDamageMaskVisualMaterialHash",
        "GameplayDamagePressureMask",
        "GameplayDamageThermalMask",
        "GameplayDamageImpactMask",
        "GameplayDamageToxicMask",
        "GameplayDamageEmpMask",
        "GameplayDamageMicroFractureMask",
        "ResolveProfileDecalType(materialHash, atlasSlice)",
        "resolvedDecalType == DynamicDecalMaterialHashes.GlassCrack",
    )
    for token in required_bridge_tokens:
        if token not in text:
            errors.append(f"runtime combat/impact visual-material bridge missing token: {token}")
    high_speed_bridge_match = re.search(
        r"private\s+static\s+uint\s+ResolveHighSpeedImpactVisualMaterialHash\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not high_speed_bridge_match:
        errors.append("missing high-speed impact visual-material bridge")
    else:
        high_speed_body = high_speed_bridge_match.group("body")
        if "signal.MaterialHash != 0u && signal.MaterialHash < AtlasSliceCount" not in high_speed_body:
            errors.append("high-speed impact bridge must only accept non-zero compact MaterialHash values as Batch34 material ids")
        if "return signal.MaterialHash;" not in high_speed_body:
            errors.append("high-speed impact bridge must preserve explicit compact Batch34 material ids")
        if "return DynamicDecalMaterialHashes.HullDent;" not in high_speed_body:
            errors.append("high-speed impact bridge must use hull dent as the safe unset/unknown material fallback")
    high_speed_profile_match = re.search(
        r"private\s+static\s+uint\s+ResolveHighSpeedImpactProfileHash\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not high_speed_profile_match:
        errors.append("missing high-speed impact profile-hash bridge")
    else:
        high_speed_profile_body = high_speed_profile_match.group("body")
        if "return 0u;" not in high_speed_profile_body:
            errors.append("high-speed impact profile bridge must leave profile lookup to the resolved compact material hash fallback")
        if "signal.MaterialHash" in high_speed_profile_body:
            errors.append("high-speed impact profile bridge must not treat composed entity/material hashes as profile keys")
    if re.search(r"if\s*\(\s*signal\.MaterialHash\s*!=\s*0u\s*\)\s*return\s+signal\.MaterialHash\s*;", text):
        errors.append("high-speed impact bridge must not return arbitrary non-zero MaterialHash as a visual material id")
    combat_visual_match = re.search(
        r"private\s+static\s+uint\s+ResolveCombatDamageVisualMaterialHash\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not combat_visual_match:
        errors.append("missing ResolveCombatDamageVisualMaterialHash bridge")
    else:
        combat_visual_body = combat_visual_match.group("body")
        if "damageType != 0u ? damageType : signal.SourceHash" in combat_visual_body:
            errors.append("combat visual-only bridge must not treat arbitrary non-zero DamageType as a Batch34 material id")
        for token in (
            "signal.SourceHash == damageType",
            "TryResolveCompactVisualOnlyMaterial(signal.SourceHash",
            "TryResolveKnownVisualOnlySourceMaterial(signal.SourceHash",
            "IsKnownGameplayDamageMask(damageType)",
            "ResolveGameplayDamageMaskVisualMaterialHash(damageType)",
            "TryResolveCompactVisualOnlyMaterial(damageType",
            "return DynamicDecalMaterialHashes.Scorch;",
        ):
            if token not in combat_visual_body:
                errors.append(f"combat visual-only bridge missing guarded material resolver token: {token}")
        gameplay_mask_index = combat_visual_body.find("IsKnownGameplayDamageMask(damageType)")
        damage_compact_index = combat_visual_body.find("TryResolveCompactVisualOnlyMaterial(damageType")
        if gameplay_mask_index < 0 or damage_compact_index < 0 or gameplay_mask_index > damage_compact_index:
            errors.append("combat visual-only bridge must classify known gameplay masks before compact DamageType fallback")
    if "private const uint GameplayDamageKnownMask" not in text:
        errors.append("combat visual-only bridge must define a known gameplay damage mask before classifying VisualOnly DamageType")
    if "SubmarineHullDentVisualSourceHash" not in text or "DynamicDecalMaterialHashes.HullDent" not in text:
        errors.append("combat visual-only bridge must preserve the submarine hull dent source-hash alias")
    if "value != 0u && value < AtlasSliceCount" not in text:
        errors.append("compact visual-only material resolver must reject zero/unset hashes before atlas-slice lookup")
    combat_profile_match = re.search(
        r"private\s+static\s+uint\s+ResolveCombatDamageProfileHash\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not combat_profile_match:
        errors.append("missing ResolveCombatDamageProfileHash bridge")
    else:
        profile_body = combat_profile_match.group("body")
        if "CombatDamageSignal.VisualOnlyFlag" not in profile_body or "signal.SourceHash" not in profile_body or ": 0u" not in profile_body:
            errors.append("combat damage profile hash bridge must only allow SourceHash profile lookup for VisualOnly signals")
    gameplay_bridge_match = re.search(
        r"private\s+static\s+uint\s+ResolveGameplayDamageMaskVisualMaterialHash\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not gameplay_bridge_match:
        errors.append("missing gameplay damage-mask to visor material bridge")
    else:
        bridge_body = gameplay_bridge_match.group("body")
        expected_mappings = (
            ("GameplayDamagePressureMask | GameplayDamageMicroFractureMask", "DynamicDecalMaterialHashes.GlassCrack"),
            ("GameplayDamageThermalMask", "DynamicDecalMaterialHashes.Burn"),
            ("GameplayDamageToxicMask | GameplayDamageRadioactiveMask", "DynamicDecalMaterialHashes.Acid"),
            ("GameplayDamageEmpMask", "DynamicDecalMaterialHashes.DataCoreCircuitTrace"),
            ("GameplayDamageParasiteMask", "DynamicDecalMaterialHashes.Blood"),
            ("GameplayDamageImpactMask", "DynamicDecalMaterialHashes.HullDent"),
        )
        for mask_token, material_token in expected_mappings:
            if mask_token not in bridge_body or material_token not in bridge_body:
                errors.append(f"gameplay damage-mask bridge missing {mask_token} -> {material_token}")
    if "? SanitizeAtlasSlice(slice)" not in text:
        errors.append("CSV profile parser must sanitize out-of-range atlas slices instead of wrapping them")
    if "SanitizeAtlasSlice(profile.AtlasSlice)" not in text:
        errors.append("signal profile packing must sanitize profile atlas slices before request payload packing")
    if "% AtlasSliceCount" in text:
        errors.append("runtime atlas slice handling must not modulo-wrap bad CSV/profile data")
    for atlas_slice, source in REQUIRED_SOURCE_BY_SLICE.items():
        if atlas_slice == 0:
            continue
        constant_name = RUNTIME_CONSTANT_BY_SOURCE[source][0]
        expected_type = contract_slices.get(atlas_slice, "")
        mapping = f"DynamicDecalMaterialHashes.{constant_name} => DynamicDecalMaterialHashes.{expected_type}"
        if expected_type and mapping not in text:
            errors.append(f"ResolveDecalTypeFromMaterial missing slice {atlas_slice} mapping: {mapping}")

    trauma_rows = load_rows(TRAUMA_CSV, errors)
    for row in trauma_rows:
        source = (row.get("source") or "").strip()
        if source not in RUNTIME_CONSTANT_BY_SOURCE:
            continue
        runtime_constant, compact_value = RUNTIME_CONSTANT_BY_SOURCE[source]
        profile_constant = source_hash_constant_name(runtime_constant)
        lifetime = (row.get("lifetimeSeconds") or "").strip()
        radius = (row.get("radiusMeters") or "").strip()
        depth = (row.get("projectionDepthMeters") or "").strip()
        if compact_value == 0:
            expected_seed = (
                f"TrySeedDefaultMaterialProfile(profiles, {profile_constant}, "
                f"DynamicDecalMaterialHashes.{runtime_constant}, {lifetime}f, {radius}f, {depth}f)"
            )
        else:
            expected_seed = (
                f"TrySeedDefaultMaterialProfileWithAlias(profiles, {profile_constant}, "
                f"DynamicDecalMaterialHashes.{runtime_constant}, {lifetime}f, {radius}f, {depth}f)"
            )
        if expected_seed not in text:
            errors.append(f"runtime default profile seed mismatch for {source}: expected `{expected_seed}`")


def validate_render_lifecycle(errors: list[str]) -> None:
    if not DEFERRED_DECAL_PASS_SOURCE.exists():
        errors.append(f"missing render feature source: {display(DEFERRED_DECAL_PASS_SOURCE)}")
        return

    text = DEFERRED_DECAL_PASS_SOURCE.read_text(encoding="utf-8-sig")
    if text.count("DynamicDecalVaultRuntime.TryInitializeColdStorage();") < 2:
        errors.append("DeferredDecalPass must initialize DynamicDecalVaultRuntime on Create and DataVault replacement")
    required_tokens = (
        "GlobalRegistryServiceSlot.DataVault",
        "DynamicDecalVaultRuntime.ResetColdStorageForRebind();",
        "DynamicDecalVaultRuntime.TryInitializeColdStorage();",
        "DynamicDecalVaultRuntime.IsColdStorageReady()",
        "EnsureDecalAtlasHandle(settings != null ? settings.decalAtlas : null);",
        "GlobalRegistryServiceSlot.Player",
        "DynamicDecalVaultRuntime.RefreshColdPlayerContext();",
        "GlobalRegistryServiceSlot.Dispatcher",
        "TryRegisterLateFrame();",
        "TryUnregisterLateFrame();",
    )
    for token in required_tokens:
        if token not in text:
            errors.append(f"DeferredDecalPass lifecycle missing token: {token}")


def validate_signal_bus_bridge(errors: list[str]) -> None:
    if not SIGNAL_BUS_SOURCE.exists():
        errors.append(f"missing SignalBus source: {display(SIGNAL_BUS_SOURCE)}")
        return

    text = SIGNAL_BUS_SOURCE.read_text(encoding="utf-8-sig")
    match = re.search(
        r"private\s+static\s+bool\s+TryCoalesceCombatDamage\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s*\}",
        text,
        re.DOTALL,
    )
    if not match:
        errors.append("missing SignalBus TryCoalesceCombatDamage owner route")
        return

    body = match.group("body")
    required_tokens = (
        "if ((incoming.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)",
        "return false;",
        "((existing.Flags ^ incoming.Flags) & CombatDamageSignal.VisualOnlyFlag) != 0",
    )
    for token in required_tokens:
        if token not in body:
            errors.append(f"SignalBus combat damage coalescing missing visual-only preservation token: {token}")


def validate_power_grid_visual_only_origin(errors: list[str]) -> None:
    if not POWER_GRID_SOURCE.exists():
        errors.append(f"missing PowerGrid source: {display(POWER_GRID_SOURCE)}")
        return

    text = POWER_GRID_SOURCE.read_text(encoding="utf-8-sig")
    start = text.find("private void PublishElectricShortCircuitDamageSignal")
    end = text.find("private void PublishNodeBrownoutSignal", start)
    if start < 0 or end < 0 or end <= start:
        errors.append("missing PowerGrid flooded short-circuit visual-only publisher block")
        return

    publish_block = text[start:end]
    if "ImpactAup = double3.zero" in publish_block:
        errors.append("PowerGrid flooded short-circuit visual-only signal must use node impact AUP, not world-origin double3.zero")

    required_publish_tokens = (
        "TryResolvePowerNodeImpactAup(node, out double3 impactAup)",
        "s_x001PowerGridSignalPushDropCount++;",
        "ImpactAup = impactAup",
        "DamageType = (uint)DamageTypeMask.Emp",
        "Hecton8.Core.Contracts.Signals.CombatDamageSignal.VisualOnlyFlag",
        "SignalBus<CombatDamageSignal>.TryPushTracked(in signal, ref s_x001PowerGridSignalPushDropCount)",
    )
    for token in required_publish_tokens:
        if token not in publish_block:
            errors.append(f"PowerGrid flooded short-circuit visual-only publisher missing token: {token}")

    required_helper_tokens = (
        "private static bool TryResolvePowerNodeImpactAup",
        "Vector3 position = node.transform.position;",
        "math.all(math.isfinite(runtimePoint))",
        "CombatDamageSignalCodec.FromRuntimePoint(runtimePoint)",
        "CombatDamageSignalCodec.IsFiniteAup(impactAup)",
    )
    for token in required_helper_tokens:
        if token not in text:
            errors.append(f"PowerGrid node impact AUP helper missing token: {token}")


def validate_visual_only_damage_origin_scan(errors: list[str]) -> None:
    if not SCRIPT_ROOT.exists():
        errors.append(f"missing script root: {display(SCRIPT_ROOT)}")
        return

    for path in SCRIPT_ROOT.rglob("*.cs"):
        if path.parts and "Editor" in path.parts:
            continue
        if path == SIGNAL_BUS_SOURCE:
            continue

        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8", errors="ignore")

        for match in re.finditer(r"CombatDamageSignal\.VisualOnlyFlag", text):
            window_start = max(0, match.start() - 900)
            window_end = min(len(text), match.end() + 900)
            window = text[window_start:window_end]
            if "CombatDamageSignal" not in window:
                continue
            if "ImpactAup = double3.zero" in window:
                line = text.count("\n", 0, match.start()) + 1
                errors.append(
                    f"{display(path)}:{line}: visual-only CombatDamageSignal publishes world-origin ImpactAup double3.zero"
                )


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    trauma_rows = load_rows(TRAUMA_CSV, errors)
    alias_rows = load_rows(ALIAS_CSV, errors)
    if trauma_rows:
        validate_csv(TRAUMA_CSV, trauma_rows, errors)
    if alias_rows:
        validate_csv(ALIAS_CSV, alias_rows, errors)
    if trauma_rows and alias_rows and trauma_rows != alias_rows:
        errors.append("visor_decal_profiles.csv is not byte-equivalent in row content to visor_trauma_profiles.csv")
    contract_slices = load_slice_contract(errors)
    validate_runtime_bridge(errors, contract_slices)
    validate_render_lifecycle(errors)
    validate_signal_bus_bridge(errors)
    validate_power_grid_visual_only_origin(errors)
    validate_visual_only_damage_origin_scan(errors)

    print("BATCH34_VISOR_TRAUMA_PROFILE_CSV_VALIDATOR")
    print(f"traumaCsv={display(TRAUMA_CSV)}")
    print(f"aliasCsv={display(ALIAS_CSV)}")
    print(f"sliceContract={display(SLICE_CONTRACT)}")
    print(f"runtimeSource={display(RUNTIME_SOURCE)}")
    print(f"renderFeature={display(DEFERRED_DECAL_PASS_SOURCE)}")
    print(f"signalBus={display(SIGNAL_BUS_SOURCE)}")
    print(f"powerGrid={display(POWER_GRID_SOURCE)}")
    print(f"scriptRoot={display(SCRIPT_ROOT)}")
    print(f"requiredSlices={len(REQUIRED_SOURCE_BY_SLICE)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
