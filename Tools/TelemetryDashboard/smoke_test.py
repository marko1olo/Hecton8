from __future__ import annotations

import csv
import json
import gc
import os
import re
import shutil
import stat
import struct
import sys
import time
from pathlib import Path

sys.dont_write_bytecode = True

import server


SMOKE_ROOT_BASE = server.PROJECT_ROOT / "Temp" / "CodexValidation" / "BLACKBOX_TELEMETRY_VISUALIZER_SMOKE"
SMOKE_ROOT = SMOKE_ROOT_BASE / f"run_{os.getpid()}"


def remove_tree_with_retry(path: Path) -> None:
    if not path.exists():
        return

    def onerror(function, value, _exc_info) -> None:
        target = Path(value)
        if target.exists():
            mode = stat.S_IWRITE | stat.S_IREAD
            if target.is_dir():
                mode |= stat.S_IEXEC
            os.chmod(target, mode)
        for _ in range(3):
            try:
                function(value)
                return
            except PermissionError:
                gc.collect()
                time.sleep(0.05)
        function(value)

    last_error: PermissionError | None = None
    for attempt in range(8):
        try:
            shutil.rmtree(path, onerror=onerror)
            return
        except PermissionError as exc:
            last_error = exc
            gc.collect()
            time.sleep(0.05 * (attempt + 1))
            if not path.exists():
                return

    if last_error is not None:
        raise last_error


def assert_sargassum_dump_layout_contract() -> None:
    source_path = server.PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "World" / "SargassumMicroFaunaBoids.cs"
    source = source_path.read_text(encoding="utf-8")

    def int_const(name: str) -> int:
        match = re.search(rf"private const int {re.escape(name)} = ([0-9]+);", source)
        assert match, f"missing int const {name}"
        return int(match.group(1), 10)

    def uint_const(name: str) -> int:
        match = re.search(rf"private const uint {re.escape(name)} = (0x[0-9A-Fa-f]+)u;", source)
        assert match, f"missing uint const {name}"
        return int(match.group(1), 16)

    def string_const(name: str) -> str:
        match = re.search(rf'private const string {re.escape(name)} = "([^"]+)";', source)
        assert match, f"missing string const {name}"
        return match.group(1)

    def struct_block(name: str) -> str:
        struct_start = source.index(f"private struct {name}")
        start = source.rfind("[StructLayout", 0, struct_start)
        assert start >= 0, f"missing StructLayout for {name}"
        next_struct = source.find("[StructLayout", struct_start + 1)
        assert next_struct > start, f"missing next struct after {name}"
        return source[start:next_struct]

    def assert_field_offsets(block: str, expected: list[tuple[int, str]]) -> None:
        for offset, field in expected:
            pattern = rf"\[FieldOffset\({offset}\)\]\s+public\s+[^;]+\s+{re.escape(field)};"
            assert re.search(pattern, block), f"missing {field} at offset {offset}"

    def method_block(signature: str) -> str:
        signature_start = source.index(signature)
        open_brace = source.index("{", signature_start)
        depth = 0
        for index in range(open_brace, len(source)):
            char = source[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    return source[signature_start : index + 1]
        raise AssertionError(f"unterminated method {signature}")

    def assert_contains_all(block: str, expected: list[str], label: str) -> None:
        for text in expected:
            assert text in block, f"missing {text!r} in {label}"

    def assert_before(block: str, first: str, second: str, label: str) -> None:
        first_index = block.find(first)
        second_index = block.find(second)
        assert first_index >= 0, f"missing {first!r} in {label}"
        assert second_index >= 0, f"missing {second!r} in {label}"
        assert first_index < second_index, f"expected {first!r} before {second!r} in {label}"

    assert int_const("FoodChainTelemetryCapacity") == server.SARGASSUM_FOOD_CHAIN_CAPACITY
    assert int_const("FoodChainTelemetryEntrySizeBytes") == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
    assert int_const("PredatorKillSignalDrainLimit") == server.SARGASSUM_FOOD_CHAIN_MAX_PENDING_KILL_SIGNALS
    assert uint_const("FoodChainTelemetryMagicLow") == server.SARGASSUM_FOOD_CHAIN_MAGIC_LOW
    assert uint_const("FoodChainTelemetryMagicHigh") == server.SARGASSUM_FOOD_CHAIN_MAGIC_HIGH
    assert string_const("FoodChainTelemetryDumpPath") == "Docs/AgentLogs/Dump_SARGASSUM_FOOD_CHAIN.bin"
    assert source.count("private bool _foodChainTelemetryDumpFailureLogged;") == 1
    assert source.count("private bool _foodChainTelemetryDumpSourceUnavailableLogged;") == 1
    assert server.SARGASSUM_FOOD_CHAIN_HEADER.size == server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES
    assert server.SARGASSUM_FOOD_CHAIN_ENTRY.size == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES

    assert "public static SargassumMicroFaunaBoids Instance => s_activeRuntimeInstance;" in source
    assert "internal static SargassumMicroFaunaBoids ActiveRuntimeInstance => Instance;" in source
    assert source.count("private static bool s_duplicateRuntimeOwnerLogged;") == 1
    reset_static_block = method_block("private static void ResetStaticState")
    assert_contains_all(
        reset_static_block,
        [
            "s_x001SargassumMicroFaunaBoidsSignalPushDropCount = 0;",
            "s_activeRuntimeInstance = null;",
            "s_duplicateRuntimeOwnerLogged = false;",
        ],
        "ResetStaticState",
    )

    awake_block = method_block("private void Awake")
    on_enable_block = method_block("private void OnEnable")
    try_register_block = method_block("private void TryRegister()")
    try_register_service_block = method_block("private void TryRegisterService")
    try_unregister_service_block = method_block("private void TryUnregisterService")
    runtime_owner_gate_block = method_block("private bool TryAbortForUsableExistingRuntime")
    runtime_usable_block = method_block("private static bool IsSargassumMicroFaunaRuntimeUsable")
    runtime_reconcile_block = method_block("private void ReconcileRuntimeOwnerFromRegistryReplacement")
    runtime_retire_block = method_block("private void RetireRuntimeRoutesAfterOwnershipLoss")
    runtime_restore_block = method_block("private void RestoreRuntimeRoutesAfterOwnershipGain")
    duplicate_log_block = method_block("private void LogDuplicateRuntimeOwnerDetected")
    service_replaced_block = method_block("public void OnGlobalRegistryServiceReplaced")

    assert_contains_all(
        awake_block,
        ["if (Application.isPlaying && TryAbortForUsableExistingRuntime())", "return;"],
        "Awake",
    )
    assert_before(
        awake_block,
        "if (Application.isPlaying && TryAbortForUsableExistingRuntime())",
        "CacheGraphicsCapabilitiesCold();",
        "Awake",
    )
    assert_contains_all(
        on_enable_block,
        [
            "if (Application.isPlaying && TryAbortForUsableExistingRuntime())",
            "TryRegisterService();",
            "if (Application.isPlaying && !_serviceRegistered)",
            "TryRegister();",
        ],
        "OnEnable",
    )
    assert_before(
        on_enable_block,
        "if (Application.isPlaying && TryAbortForUsableExistingRuntime())",
        "SargassumGlobalDragManager.Register(this);",
        "OnEnable",
    )
    assert_before(on_enable_block, "TryRegisterService();", "CacheGraphicsCapabilitiesCold();", "OnEnable")
    assert_before(on_enable_block, "TryRegisterService();", "SargassumGlobalDragManager.Register(this);", "OnEnable")
    assert_before(on_enable_block, "TryRegisterService();", "TryRegister();", "OnEnable")
    assert_contains_all(
        try_register_block,
        ["if (!Application.isPlaying || !_serviceRegistered)", "GlobalRegistry.TryRegisterFixedTickable"],
        "TryRegister",
    )
    assert_contains_all(
        try_register_service_block,
        [
            "if (_serviceRegistered || !Application.isPlaying)",
            "if (TryAbortForUsableExistingRuntime())",
            "GlobalRegistry.RegisterSargassumMicroFaunaRuntime(this);",
            "_serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumMicroFauna, this);",
            "s_activeRuntimeInstance = this;",
        ],
        "TryRegisterService",
    )
    assert_before(
        try_register_service_block,
        "if (TryAbortForUsableExistingRuntime())",
        "GlobalRegistry.RegisterSargassumMicroFaunaRuntime(this);",
        "TryRegisterService",
    )
    assert_contains_all(
        try_unregister_service_block,
        [
            "if (ReferenceEquals(GlobalRegistry.SargassumMicroFauna, this))",
            "GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(this);",
            "if (ReferenceEquals(s_activeRuntimeInstance, this))",
            "s_activeRuntimeInstance = null;",
            "_serviceRegistered = false;",
        ],
        "TryUnregisterService",
    )
    assert_before(
        try_unregister_service_block,
        "if (ReferenceEquals(s_activeRuntimeInstance, this))",
        "GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(this);",
        "TryUnregisterService",
    )
    assert_before(
        try_unregister_service_block,
        "_serviceRegistered = false;",
        "GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(this);",
        "TryUnregisterService",
    )
    assert_contains_all(
        runtime_owner_gate_block,
        [
            "SargassumMicroFaunaBoids active = s_activeRuntimeInstance;",
            "if (IsSargassumMicroFaunaRuntimeUsable(active))",
            "LogDuplicateRuntimeOwnerDetected(active);",
            "Destroy(gameObject);",
            "GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(active);",
            "SargassumMicroFaunaBoids registered = GlobalRegistry.SargassumMicroFauna;",
            "if (IsSargassumMicroFaunaRuntimeUsable(registered))",
            "s_activeRuntimeInstance = registered;",
            "LogDuplicateRuntimeOwnerDetected(registered);",
            "GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(registered);",
        ],
        "TryAbortForUsableExistingRuntime",
    )
    assert_contains_all(
        runtime_usable_block,
        ["runtime != null && runtime._serviceRegistered && runtime.isActiveAndEnabled"],
        "IsSargassumMicroFaunaRuntimeUsable",
    )
    assert_contains_all(
        service_replaced_block,
        [
            "case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:",
            "ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);",
        ],
        "OnGlobalRegistryServiceReplaced",
    )
    assert_contains_all(
        runtime_reconcile_block,
        [
            "currentService is SargassumMicroFaunaBoids currentRuntime",
            "s_activeRuntimeInstance = currentRuntime;",
            "bool ownsRuntime = ReferenceEquals(currentRuntime, this);",
            "_serviceRegistered = ownsRuntime;",
            "if (_runtimeRoutesRetiredAfterOwnershipLoss)",
            "RestoreRuntimeRoutesAfterOwnershipGain();",
            "RetireRuntimeRoutesAfterOwnershipLoss();",
            "if (ReferenceEquals(previousService, this))",
            "_serviceRegistered = false;",
            "if (ReferenceEquals(s_activeRuntimeInstance, this))",
            "s_activeRuntimeInstance = null;",
        ],
        "ReconcileRuntimeOwnerFromRegistryReplacement",
    )
    assert_contains_all(
        runtime_retire_block,
        [
            "if (_runtimeRoutesRetiredAfterOwnershipLoss)",
            "SargassumGlobalDragManager.Unregister(this);",
            "FlashlightEvents.Unregister(this);",
            "SpectrumEvents.UnregisterSonarPingListener(this);",
            "HectonFloatingOrigin.UnregisterListener(this);",
            "GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);",
            "GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);",
            "GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);",
            "_runtimeRoutesRetiredAfterOwnershipLoss = true;",
        ],
        "RetireRuntimeRoutesAfterOwnershipLoss",
    )
    assert "TryUnregisterHotSwapListener" not in runtime_retire_block
    assert_contains_all(
        runtime_restore_block,
        [
            "if (!Application.isPlaying || !isActiveAndEnabled)",
            "RefreshColdRegistryDependencies();",
            "RefreshDependencies();",
            "SargassumGlobalDragManager.Register(this);",
            "FlashlightEvents.Register(this);",
            "SpectrumEvents.RegisterSonarPingListener(this);",
            "HectonFloatingOrigin.RegisterListener(this);",
            "TryRegister();",
            "_runtimeRoutesRetiredAfterOwnershipLoss = false;",
        ],
        "RestoreRuntimeRoutesAfterOwnershipGain",
    )
    assert_contains_all(
        duplicate_log_block,
        [
            "if (s_duplicateRuntimeOwnerLogged)",
            "s_duplicateRuntimeOwnerLogged = true;",
            "Duplicate runtime owner detected.",
            "before service/tick registration.",
            "H8Debug.LogError(",
        ],
        "LogDuplicateRuntimeOwnerDetected",
    )

    food_chain_dump_block = method_block("private unsafe void TryDumpFoodChainTelemetry")
    assert_contains_all(
        food_chain_dump_block,
        [
            "if (_foodChainTelemetryDumped)",
            "!TryReadOnlySargassumVaultArray(",
            "if (!_foodChainTelemetryDumpSourceUnavailableLogged)",
            "_foodChainTelemetryDumpSourceUnavailableLogged = true;",
            "Food-chain telemetry dump source unavailable. path=",
            'anomalyHash.ToString("X8")',
            "bool wrote = TryWriteFoodChainTelemetryDump(foodChainTelemetryRing, anomalyHash);",
            "_foodChainTelemetryDumped = wrote;",
            "if (!wrote && !_foodChainTelemetryDumpFailureLogged)",
            "_foodChainTelemetryDumpFailureLogged = true;",
            "Hecton8.Core.H8Debug.LogError(",
            "Food-chain telemetry dump failed. path=",
            "FoodChainTelemetryDumpPath",
            'anomalyHash.ToString("X8")',
        ],
        "TryDumpFoodChainTelemetry",
    )

    food_chain_writer_block = method_block("private unsafe bool TryWriteFoodChainTelemetryDump")
    assert_contains_all(
        food_chain_writer_block,
        [
            "int capacity = math.min(foodChainTelemetryRing.Length, FoodChainTelemetryCapacity);",
            "int entrySize = UnsafeUtility.SizeOf<FoodChainTelemetryEntry>();",
            "const int headerBytes = (sizeof(uint) * 3) + (sizeof(int) * 3);",
            "NativeFaultDumpWriter.CreateTransientPayload(",
            "FoodChainTelemetryDumpPayloadLabel",
            "UnsafeUtility.WriteArrayElement<uint>(destination, 0, FoodChainTelemetryMagicLow);",
            "UnsafeUtility.WriteArrayElement<uint>(destination + sizeof(uint), 0, FoodChainTelemetryMagicHigh);",
            "UnsafeUtility.WriteArrayElement<int>(destination + sizeof(uint) * 2, 0, entrySize);",
            "UnsafeUtility.CopyStructureToPtr(ref entry, rows + i * entrySize);",
            "NativeFaultDumpWriter.TryWriteAll(FoodChainTelemetryDumpPath, payload, byteCount);",
            "NativeFaultDumpWriter.DisposeTransientPayload(",
        ],
        "TryWriteFoodChainTelemetryDump",
    )

    food_chain_block = struct_block("FoodChainTelemetryEntry")
    assert "[StructLayout(LayoutKind.Explicit, Size = 64)]" in food_chain_block
    assert_field_offsets(
        food_chain_block,
        [
            (0, "FrameIndex"),
            (4, "StateHash"),
            (8, "SourceHash"),
            (12, "Flags"),
            (16, "ActiveBoidCount"),
            (20, "ConsumedBoidCount"),
            (24, "PendingKillJob"),
            (28, "LodTier"),
            (32, "FieldCenterWS"),
            (44, "EventPositionWS"),
            (56, "AnomalyHash"),
            (60, "SimulationTime"),
        ],
    )

    assert int_const("BoidSensoryBlackBoxCapacity") == server.SARGASSUM_BOID_SENSORY_CAPACITY
    assert int_const("BoidSensoryBlackBoxEntrySizeBytes") == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES
    assert uint_const("BoidSensoryBlackBoxMagicLow") == server.SARGASSUM_BOID_SENSORY_MAGIC_LOW
    assert uint_const("BoidSensoryBlackBoxMagicHigh") == server.SARGASSUM_BOID_SENSORY_MAGIC_HIGH
    assert string_const("BoidSensoryBlackBoxDumpPath") == "Docs/AgentLogs/Dump_SARGASSUM_BOID_SENSORY.bin"
    assert source.count("private bool _boidSensoryBlackBoxDumpFailureLogged;") == 1
    assert source.count("private bool _boidSensoryBlackBoxDumpSourceUnavailableLogged;") == 1
    assert server.SARGASSUM_BOID_SENSORY_HEADER.size == server.SARGASSUM_BOID_SENSORY_HEADER_BYTES
    assert server.SARGASSUM_BOID_SENSORY_ENTRY.size == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES

    boid_sensory_dump_block = method_block("private unsafe void TryDumpBoidSensoryBlackBox")
    assert_contains_all(
        boid_sensory_dump_block,
        [
            "if (_boidSensoryBlackBoxDumped)",
            "if (!boidSensoryBlackBox.IsCreated)",
            "if (!_boidSensoryBlackBoxDumpSourceUnavailableLogged)",
            "_boidSensoryBlackBoxDumpSourceUnavailableLogged = true;",
            "Boid sensory blackbox dump source unavailable. path=",
            'anomalyHash.ToString("X8")',
            "bool wrote = TryWriteBoidSensoryBlackBoxDump(boidSensoryBlackBox, anomalyHash);",
            "_boidSensoryBlackBoxDumped = wrote;",
            "if (!wrote && !_boidSensoryBlackBoxDumpFailureLogged)",
            "_boidSensoryBlackBoxDumpFailureLogged = true;",
            "Hecton8.Core.H8Debug.LogError(",
            "Boid sensory blackbox dump failed. path=",
            "BoidSensoryBlackBoxDumpPath",
            'anomalyHash.ToString("X8")',
        ],
        "TryDumpBoidSensoryBlackBox",
    )

    boid_sensory_writer_block = method_block("private unsafe bool TryWriteBoidSensoryBlackBoxDump")
    assert_contains_all(
        boid_sensory_writer_block,
        [
            "int capacity = math.min(boidSensoryBlackBox.Length, BoidSensoryBlackBoxCapacity);",
            "int entrySize = UnsafeUtility.SizeOf<BoidSensoryBlackBoxEntry>();",
            "const int headerBytes = (sizeof(uint) * 3) + (sizeof(int) * 3);",
            "NativeFaultDumpWriter.CreateTransientPayload(",
            "BoidSensoryBlackBoxDumpPayloadLabel",
            "UnsafeUtility.WriteArrayElement<uint>(destination, 0, BoidSensoryBlackBoxMagicLow);",
            "UnsafeUtility.WriteArrayElement<uint>(destination + sizeof(uint), 0, BoidSensoryBlackBoxMagicHigh);",
            "UnsafeUtility.WriteArrayElement<int>(destination + sizeof(uint) * 2, 0, entrySize);",
            "UnsafeUtility.CopyStructureToPtr(ref entry, rows + i * entrySize);",
            "NativeFaultDumpWriter.TryWriteAll(BoidSensoryBlackBoxDumpPath, payload, byteCount);",
            "NativeFaultDumpWriter.DisposeTransientPayload(",
        ],
        "TryWriteBoidSensoryBlackBoxDump",
    )

    on_disable_block = method_block("private void OnDisable")
    release_buffers_block = method_block("private void ReleaseBuffers")
    for lifecycle_label, lifecycle_block in (
        ("OnDisable", on_disable_block),
        ("ReleaseBuffers", release_buffers_block),
    ):
        assert_contains_all(
            lifecycle_block,
            [
                "_foodChainTelemetryDumped = false;",
                "_foodChainTelemetryDumpSourceUnavailableLogged = false;",
                "_foodChainTelemetryDumpFailureLogged = false;",
                "_boidSensoryBlackBoxDumped = false;",
                "_boidSensoryBlackBoxDumpSourceUnavailableLogged = false;",
                "_boidSensoryBlackBoxDumpFailureLogged = false;",
            ],
            lifecycle_label,
        )

    boid_sensory_block = struct_block("BoidSensoryBlackBoxEntry")
    assert "[StructLayout(LayoutKind.Explicit, Size = 64)]" in boid_sensory_block
    assert_field_offsets(
        boid_sensory_block,
        [
            (0, "FrameIndex"),
            (4, "StateHash"),
            (8, "Flags"),
            (12, "ActiveThreatCount"),
            (16, "SubmarineThreat"),
            (32, "FlashlightThreat"),
            (48, "AcousticPingRadii"),
        ],
    )


def assert_sargassum_owner_local_runtime_routes() -> None:
    def read_project_source(*parts: str) -> str:
        return (server.PROJECT_ROOT / Path(*parts)).read_text(encoding="utf-8")

    def method_block(source: str, signature: str) -> str:
        start = source.find(signature)
        assert start >= 0, f"missing method signature {signature!r}"
        open_brace = source.find("{", start)
        assert open_brace >= 0, f"missing method body {signature!r}"
        depth = 0
        for i in range(open_brace, len(source)):
            char = source[i]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    return source[open_brace + 1 : i]
        raise AssertionError(f"unterminated method body {signature!r}")

    def method_block_after(source: str, anchor: str, signature: str) -> str:
        anchor_start = source.find(anchor)
        assert anchor_start >= 0, f"missing method anchor {anchor!r}"
        return method_block(source[anchor_start:], signature)

    def assert_contains_all(block: str, expected: list[str], label: str) -> None:
        for text in expected:
            assert text in block, f"missing {text!r} in {label}"

    def assert_before(block: str, first: str, second: str, label: str) -> None:
        first_index = block.find(first)
        second_index = block.find(second)
        assert first_index >= 0, f"missing {first!r} in {label}"
        assert second_index >= 0, f"missing {second!r} in {label}"
        assert first_index < second_index, f"expected {first!r} before {second!r} in {label}"

    drag = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumGlobalDragManager.cs")
    assert "public static SargassumGlobalDragManager Instance => s_activeRuntimeInstance;" in drag
    assert "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cutManager);" in drag
    assert "_cutManager = GlobalRegistry.SargassumCut;" not in drag
    drag_on_enable = method_block(drag, "private void OnEnable")
    drag_register = method_block(drag, "private void TryRegister()")
    drag_register_save = method_block(drag, "private void TryRegisterSaveOwner")
    drag_tick = method_block(drag, "public void Tick(float dt)")
    drag_late_tick = method_block(drag, "public void LateFrameTick")
    drag_origin_shift = method_block(drag, "public void OnOriginShift")
    drag_unregister_service = method_block(drag, "private void TryUnregisterService")
    drag_replaced = method_block(drag, "public void OnGlobalRegistryServiceReplaced")
    drag_reconcile = method_block(drag, "private void ReconcileRuntimeOwnerFromRegistryReplacement")
    drag_retire = method_block(drag, "private void RetireRuntimeRoutesAfterOwnershipLoss")
    drag_restore = method_block(drag, "private void RestoreRuntimeRoutesAfterOwnershipGain")
    drag_raise_strain = method_block(drag, "public static bool TryRaiseEntanglementStrain")
    drag_raise_massive = method_block(drag, "public static bool TryRaiseMassiveDisplacement")
    drag_register_massive = method_block(drag, "public void RegisterMassiveDisplacement")
    drag_dispatch_strain = method_block(drag, "private static void DispatchEntanglementStrainToListener")
    drag_dispatch_massive = method_block(drag, "private static void DispatchMassiveDisplacementToListener")
    drag_sample_detailed = method_block(
        drag,
        "internal bool SampleDetailedInfluence(\n            Vector3 positionWS,\n            float radius,\n            Vector3 movementVelocityWS,",
    )
    drag_update_disruption = method_block(drag, "private bool UpdateDisruptionZones")
    drag_register_disruption = method_block(drag, "private int RegisterOrReinforceDisruptionZone")
    drag_sample_disruption = method_block(drag, "private DisruptionSample SampleDisruptionNoDrift")
    drag_resolve_max_sink = method_block(drag, "private float ResolveMaximumSinkDepthWS")
    drag_update_scavengers = method_block(drag, "private void UpdateScavengerHosts")
    drag_update_nested = method_block(drag, "private void UpdateNestedAttachmentBatches")
    drag_register_scavenger = method_block(drag, "internal bool RegisterSettledCollapseChunk")
    drag_unregister_scavenger = method_block(drag, "internal void UnregisterSettledCollapseChunk")
    assert_contains_all(
        drag_on_enable,
        ["TryRegisterService();", "if (!_serviceRegistered)", "TryRegister();"],
        "SargassumGlobalDragManager.OnEnable",
    )
    for later in [
        "ResolveActiveNestingPrototypes();",
        "RefreshColdRegistryDependencies();",
        "EnsureScavengerRenderResources();",
        "HectonFloatingOrigin.RegisterListener(this);",
        "TryRegister();",
    ]:
        assert_before(drag_on_enable, "TryRegisterService();", later, "SargassumGlobalDragManager.OnEnable")
    assert_contains_all(
        drag_register,
        ["if (Application.isPlaying && !_serviceRegistered)", "TryRegisterSaveOwner();"],
        "SargassumGlobalDragManager.TryRegister",
    )
    assert_contains_all(
        drag_tick,
        [
            "dt = math.isfinite(dt) ? math.max(0f, dt) : 0f;",
            "bool texturesChanged = UpdateDisruptionZones(dt);",
            "_pendingNestedRenderDeltaTime = dt;",
        ],
        "SargassumGlobalDragManager.Tick",
    )
    assert_contains_all(
        drag_late_tick,
        [
            "float nestedRenderDeltaTime = math.isfinite(_pendingNestedRenderDeltaTime)",
            "? math.max(0f, _pendingNestedRenderDeltaTime)",
            ": 0f;",
            "UpdateScavengerHosts(nestedRenderDeltaTime);",
            "RenderNestedAttachmentsAndScavengers(nestedRenderDeltaTime);",
        ],
        "SargassumGlobalDragManager.LateFrameTick",
    )
    assert_contains_all(
        drag_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!IsFiniteVector3(shiftOffset)",
            "!math.isfinite(shiftSqrMagnitude)",
            "shiftSqrMagnitude <= 0.0001f",
            "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
        ],
        "SargassumGlobalDragManager.OnOriginShift",
    )
    for origin_shift_path_parts, origin_shift_label in [
        (("Assets", "_Project", "Scripts", "BiomeSamplerCache.cs"), "BiomeSamplerCache.OnOriginShift"),
        (("Assets", "_Project", "Scripts", "World", "AbyssalFluidDecalManager.cs"), "AbyssalFluidDecalManager.OnOriginShift"),
        (("Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs"), "SargassumCrestDampingController.OnOriginShift"),
    ]:
        origin_shift_block = method_block(read_project_source(*origin_shift_path_parts), "public void OnOriginShift")
        assert_contains_all(
            origin_shift_block,
            [
                "Vector3 shiftOffset = shiftData.ShiftOffset;",
                "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
                "if (!isActiveAndEnabled ||",
                "!MathGuard.IsFinite(shiftOffset) ||",
                "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
                "shiftSqrMagnitude <= 0.0001f)",
                "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
            ],
            origin_shift_label,
        )
        assert_before(
            origin_shift_block,
            "!MathGuard.IsFinite(shiftOffset) ||",
            "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
            f"{origin_shift_label} rejects nonfinite shift before cached state mutation",
        )
    finite_shift_cached_state_contracts = [
        (
            ("Assets", "_Project", "Scripts", "Atmosphere", "HectonSurfaceWeatherDirector.cs"),
            "HectonSurfaceWeatherDirector.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "_pendingThunderPosition += -shiftOffset;",
        ),
        (
            ("Assets", "_Project", "Scripts", "Atmosphere", "SurfaceWeatherVfxRig.cs"),
            "SurfaceWeatherVfxRig.OnOriginShift",
            "shiftSqrMagnitude <= 0.000001f)",
            "RebaseBoltPositions(shiftOffset);",
        ),
        (
            ("Assets", "_Project", "Scripts", "WorldGenerativeGeologyIntegrationDirector.cs"),
            "WorldGenerativeGeologyIntegrationDirector.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "_lastPlanRefreshPosition += -shiftOffset;",
        ),
        (
            ("Assets", "_Project", "Scripts", "SubmarineFluidDynamics.cs"),
            "SubmarineFluidDynamics.OnOriginShift",
            "shiftSqrMagnitude <= 0.000001f)",
            "ResetSloshHistoryForOriginShift();",
        ),
        (
            ("Assets", "_Project", "Scripts", "VFX", "HectonMarineSnowRenderer.cs"),
            "HectonMarineSnowRenderer.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "Vector3 runtimeOffset = -shiftOffset;",
        ),
        (
            ("Assets", "_Project", "Scripts", "World", "Biolum", "HectonBiolumDiffusionVolume.cs"),
            "HectonBiolumDiffusionVolume.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "_needsClear = true;",
        ),
        (
            ("Assets", "_Project", "Scripts", "Gameplay", "DataArchaeologyRuntime.cs"),
            "DataArchaeologyRuntime.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "float3 runtimeDelta = -(float3)shiftOffset;",
        ),
    ]
    for origin_shift_path_parts, origin_shift_label, threshold_fragment, mutation_fragment in finite_shift_cached_state_contracts:
        origin_shift_block = method_block(read_project_source(*origin_shift_path_parts), "public void OnOriginShift")
        assert_contains_all(
            origin_shift_block,
            [
                "Vector3 shiftOffset = shiftData.ShiftOffset;",
                "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
                "!MathGuard.IsFinite(shiftOffset) ||",
                "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
                threshold_fragment,
                mutation_fragment,
            ],
            origin_shift_label,
        )
        assert_before(
            origin_shift_block,
            "!MathGuard.IsFinite(shiftOffset) ||",
            mutation_fragment,
            f"{origin_shift_label} rejects nonfinite shift before runtime state mutation",
        )
    interior_gi_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "Lighting", "InteriorGIProbeVolumeRuntime.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        interior_gi_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble))",
            "double3 shiftedRootAup = shiftData.NewTotalOffsetDouble + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);",
            "_rootAup = shiftedRootAup;",
            "_visualDirty = true;",
        ],
        "InteriorGIProbeVolumeRuntime.OnOriginShift finite committed-root bridge",
    )
    assert_before(
        interior_gi_origin_shift,
        "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble))",
        "_rootAup = shiftedRootAup;",
        "InteriorGIProbeVolumeRuntime.OnOriginShift rejects bad committed total before root AUP mutation",
    )
    assert_before(
        interior_gi_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_visualDirty = true;",
        "InteriorGIProbeVolumeRuntime.OnOriginShift rejects bad/no-op shifts before GPU upload dirty flag",
    )
    surface_weather_rebase = method_block(
        read_project_source("Assets", "_Project", "Scripts", "Atmosphere", "SurfaceWeatherVfxRig.cs"),
        "private void RebaseBoltPositions",
    )
    assert_contains_all(
        surface_weather_rebase,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "_boltRenderer.GetPositions(_boltPoints);",
            "_boltPoints[i] -= shiftOffset;",
        ],
        "SurfaceWeatherVfxRig.RebaseBoltPositions finite lightning geometry mutation",
    )
    assert_before(
        surface_weather_rebase,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_boltRenderer.GetPositions(_boltPoints);",
        "SurfaceWeatherVfxRig.RebaseBoltPositions rejects nonfinite shift before LineRenderer state readback",
    )
    fluid_engine_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "HectonFluidEngine.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        fluid_engine_origin_shift,
        [
            "_lastOriginShiftSequence = shiftData.Sequence;",
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_pendingOriginShiftOffset += shiftOffset;",
            "ApplyOriginShiftRebase(shiftOffset);",
        ],
        "HectonFluidEngine.OnOriginShift finite pending job rebase",
    )
    assert_before(
        fluid_engine_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_pendingOriginShiftOffset += shiftOffset;",
        "HectonFluidEngine.OnOriginShift rejects nonfinite shift before deferred rebase accumulation",
    )
    fluid_engine_apply_rebase = method_block(
        read_project_source("Assets", "_Project", "Scripts", "HectonFluidEngine.cs"),
        "private void ApplyOriginShiftRebase",
    )
    assert_contains_all(
        fluid_engine_apply_rebase,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "float3 runtimeOffset = new float3(",
        ],
        "HectonFluidEngine.ApplyOriginShiftRebase finite runtime buffer rebase",
    )
    assert_before(
        fluid_engine_apply_rebase,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "float3 runtimeOffset = new float3(",
        "HectonFluidEngine.ApplyOriginShiftRebase rejects nonfinite shift before buffer mutation",
    )
    ar_waypoint_overlay = read_project_source("Assets", "_Project", "Scripts", "UI", "ARWaypointOverlay.cs")
    ar_waypoint_origin_shift = method_block(ar_waypoint_overlay, "public void OnOriginShift")
    ar_waypoint_rebase = method_block(ar_waypoint_overlay, "private void RebaseExternalRuntimeWaypointPresentation")
    ar_waypoint_finite = method_block(ar_waypoint_overlay, "private static bool IsFiniteRuntimeVector")
    assert_contains_all(
        ar_waypoint_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteRuntimeVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "HideRenderedSlots();",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "RebaseExternalRuntimeWaypointPresentation(-shiftOffset);",
            "_targetCanvas = null;",
        ],
        "ARWaypointOverlay.OnOriginShift finite AUP-backed waypoint bridge",
    )
    assert_before(
        ar_waypoint_origin_shift,
        "if (!IsFiniteRuntimeVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "RebaseExternalRuntimeWaypointPresentation(-shiftOffset);",
        "ARWaypointOverlay.OnOriginShift rejects nonfinite shifts before waypoint/cache mutation",
    )
    assert_before(
        ar_waypoint_origin_shift,
        "if (shiftSqrMagnitude <= 0.000001f)",
        "RebaseExternalRuntimeWaypointPresentation(-shiftOffset);",
        "ARWaypointOverlay.OnOriginShift rejects no-op shifts before UI hierarchy rebuild",
    )
    assert_contains_all(
        ar_waypoint_rebase,
        [
            "if (!externalWaypoint.Active || externalWaypoint.UseTransform)",
            "externalWaypoint.PositionAup.TryToRuntimeFloat3(out float3 resolvedRuntimePosition)",
            "externalWaypoint.PresentationPosition = new Vector3(",
            "Vector3 rebasedPosition = externalWaypoint.PresentationPosition + runtimeOffset;",
            "if (!IsFiniteRuntimeVector(rebasedPosition))",
            "externalWaypoint.HasPositionAup = false;",
            "externalWaypoint.PresentationPosition = rebasedPosition;",
        ],
        "ARWaypointOverlay.RebaseExternalRuntimeWaypointPresentation AUP source-of-truth fallback",
    )
    assert_before(
        ar_waypoint_rebase,
        "externalWaypoint.PositionAup.TryToRuntimeFloat3(out float3 resolvedRuntimePosition)",
        "Vector3 rebasedPosition = externalWaypoint.PresentationPosition + runtimeOffset;",
        "ARWaypointOverlay.RebaseExternalRuntimeWaypointPresentation prefers AUP over stale Vector3 cache",
    )
    assert_contains_all(
        ar_waypoint_finite,
        ["return math.all(math.isfinite(new float3(value.x, value.y, value.z)));"],
        "ARWaypointOverlay.IsFiniteRuntimeVector finite guard",
    )
    native_trail = read_project_source("Assets", "_Project", "Scripts", "VFX", "NativeTrailRenderer.cs")
    native_trail_origin_shift = method_block(native_trail, "public void OnOriginShift")
    native_trail_refresh_last = method_block(native_trail, "private bool TryRefreshLastSampleRuntimePosition")
    assert_contains_all(
        native_trail_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "ClearTrail();",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "if (_hasLastSample && !TryRefreshLastSampleRuntimePosition())",
            "_meshDirty = true;",
        ],
        "NativeTrailRenderer.OnOriginShift finite AUP trail bridge",
    )
    assert_before(
        native_trail_origin_shift,
        "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "_meshDirty = true;",
        "NativeTrailRenderer.OnOriginShift rejects nonfinite shifts before mesh rebuild request",
    )
    assert_before(
        native_trail_origin_shift,
        "if (shiftSqrMagnitude <= 0.000001f)",
        "_meshDirty = true;",
        "NativeTrailRenderer.OnOriginShift rejects no-op shifts before mesh rebuild request",
    )
    assert_contains_all(
        native_trail_refresh_last,
        [
            "if (!_lastSampleAup.TryToRuntimeFloat3(out float3 runtime) ||",
            "!math.all(math.isfinite(runtime)))",
            "_lastSampleRuntimePosition = new Vector3(runtime.x, runtime.y, runtime.z);",
            "return true;",
        ],
        "NativeTrailRenderer.TryRefreshLastSampleRuntimePosition finite AUP conversion",
    )
    localized_world_sign = read_project_source("Assets", "_Project", "Scripts", "LocalizedWorldSign.cs")
    localized_world_sign_origin_shift = method_block(localized_world_sign, "public void OnOriginShift")
    localized_world_sign_finite = method_block(localized_world_sign, "private static bool IsFiniteRuntimeVector")
    assert_contains_all(
        localized_world_sign_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteRuntimeVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "_hasAupPosition = false;",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "Vector3 runtimePosition = shiftData.ToRuntimePosition(_absoluteUniversePositionDouble);",
            "if (!IsFiniteRuntimeVector(runtimePosition))",
            "_cachedTransform.position = runtimePosition;",
        ],
        "LocalizedWorldSign.OnOriginShift finite world-sign binding",
    )
    assert_before(
        localized_world_sign_origin_shift,
        "if (!IsFiniteRuntimeVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "_cachedTransform.position = runtimePosition;",
        "LocalizedWorldSign.OnOriginShift rejects nonfinite shifts before transform mutation",
    )
    assert_before(
        localized_world_sign_origin_shift,
        "if (!IsFiniteRuntimeVector(runtimePosition))",
        "_cachedTransform.position = runtimePosition;",
        "LocalizedWorldSign.OnOriginShift validates converted runtime position before transform mutation",
    )
    assert_contains_all(
        localized_world_sign_finite,
        [
            "return math.isfinite(value.x) &&",
            "math.isfinite(value.y) &&",
            "math.isfinite(value.z);",
        ],
        "LocalizedWorldSign.IsFiniteRuntimeVector finite guard",
    )
    flood_waterline = read_project_source("Assets", "_Project", "Scripts", "Visor", "InternalFloodWaterlineRuntime.cs")
    flood_waterline_origin_shift = method_block(flood_waterline, "public void OnOriginShift")
    assert_contains_all(
        flood_waterline_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "float shiftY = shiftOffset.y;",
            "if (shiftSqrMagnitude <= 0.000001f || math.abs(shiftY) <= 0.000001f)",
            "_currentWaterlineY -= shiftY;",
            "_targetWaterlineY -= shiftY;",
            "QueueShaderGlobals(_hasWaterline ? _currentFill01 : 0f);",
        ],
        "InternalFloodWaterlineRuntime.OnOriginShift finite Y-axis waterline bridge",
    )
    assert_before(
        flood_waterline_origin_shift,
        "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "_currentWaterlineY -= shiftY;",
        "InternalFloodWaterlineRuntime.OnOriginShift rejects nonfinite full shift before waterline mutation",
    )
    assert_before(
        flood_waterline_origin_shift,
        "if (shiftSqrMagnitude <= 0.000001f || math.abs(shiftY) <= 0.000001f)",
        "QueueShaderGlobals(_hasWaterline ? _currentFill01 : 0f);",
        "InternalFloodWaterlineRuntime.OnOriginShift skips no-op/horizontal shifts before shader global flush",
    )
    construction_manager = read_project_source("Assets", "_Project", "Scripts", "ConstructionManager.cs")
    construction_origin_shift = method_block(construction_manager, "public void OnOriginShift")
    assert_contains_all(
        construction_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "BaseDegradationSystem.ApplyOriginShift(in shiftData);",
            "DroneFleetManager.ApplyOriginShift(shiftOffset);",
            "RecoverHabitatJointsAfterOriginShift(in shiftData);",
        ],
        "ConstructionManager.OnOriginShift finite construction subsystem ingress",
    )
    for mutation in [
        "BaseDegradationSystem.ApplyOriginShift(in shiftData);",
        "DroneFleetManager.ApplyOriginShift(shiftOffset);",
        "RecoverHabitatJointsAfterOriginShift(in shiftData);",
    ]:
        assert_before(
            construction_origin_shift,
            "if (!IsFiniteVector(shiftOffset) ||",
            mutation,
            f"ConstructionManager.OnOriginShift rejects nonfinite/no-op shifts before {mutation}",
        )
    vr_somatic_provider = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Gameplay",
        "VRSomaticProvider.cs",
    )
    vr_somatic_origin_shift = method_block(vr_somatic_provider, "public void OnOriginShift")
    assert_contains_all(
        vr_somatic_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "CompleteSomaticComfortForBarrier();",
            "_lastObservedAupShiftSequence = shiftData.Sequence;",
            "PublishShaderState();",
            "float3 shift = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
            "RebaseHandArray(handTargets, shift);",
            "RebaseHandArray(handPhysicalPositions, shift);",
        ],
        "VRSomaticProvider.OnOriginShift finite VR feedback bridge",
    )
    for mutation in [
        "CompleteSomaticComfortForBarrier();",
        "_lastObservedAupShiftSequence = shiftData.Sequence;",
        "PublishShaderState();",
        "RebaseHandArray(handTargets, shift);",
    ]:
        assert_before(
            vr_somatic_origin_shift,
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            mutation,
            f"VRSomaticProvider.OnOriginShift rejects bad/no-op shifts before {mutation}",
        )
    somatic_kinematics = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Gameplay",
        "SomaticKinematicsRuntime.cs",
    )
    somatic_kinematics_origin_shift = method_block(somatic_kinematics, "public void OnOriginShift")
    assert_contains_all(
        somatic_kinematics_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "CompleteScheduledKinematicsInPostFixedOrShutdown(true);",
            "state.SectorOriginAup = shiftData.NewTotalOffsetDouble;",
            "state.ShiftFrameId = shiftData.Sequence;",
            "PublishOriginShiftFence(in state, in shiftData);",
        ],
        "SomaticKinematicsRuntime.OnOriginShift finite player kinematic sector bridge",
    )
    for mutation in [
        "CompleteScheduledKinematicsInPostFixedOrShutdown(true);",
        "state.SectorOriginAup = shiftData.NewTotalOffsetDouble;",
        "state.ShiftFrameId = shiftData.Sequence;",
        "PublishOriginShiftFence(in state, in shiftData);",
    ]:
        assert_before(
            somatic_kinematics_origin_shift,
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            mutation,
            f"SomaticKinematicsRuntime.OnOriginShift rejects bad/no-op shifts before {mutation}",
        )
    pda_marker_registry = read_project_source("Assets", "_Project", "Scripts", "PDA", "PDAMarkerRegistry.cs")
    pda_marker_origin_shift = method_block(pda_marker_registry, "public void OnOriginShift")
    assert_contains_all(
        pda_marker_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "if (_markerCount == 0)",
            "TryResolveRuntimePosition(in record.positionAup, out Vector3 runtimePosition)",
            "CommitMarkerRevision(0u);",
        ],
        "PDAMarkerRegistry.OnOriginShift finite marker runtime bridge",
    )
    for mutation in [
        "TryResolveRuntimePosition(in record.positionAup, out Vector3 runtimePosition)",
        "CommitMarkerRevision(0u);",
    ]:
        assert_before(
            pda_marker_origin_shift,
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            mutation,
            f"PDAMarkerRegistry.OnOriginShift rejects bad/no-op shifts before {mutation}",
        )
    ui_scaler = read_project_source("Assets", "_Project", "Scripts", "UI", "HectonUIScaler.cs")
    ui_scaler_origin_shift = method_block(ui_scaler, "public void OnOriginShift")
    assert_contains_all(
        ui_scaler_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_lastScreenWidth = -1;",
            "ApplyScaleToCachedRoot(contentRoot, force: true);",
        ],
        "HectonUIScaler.OnOriginShift finite UI scale refresh bridge",
    )
    assert_before(
        ui_scaler_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_lastScreenWidth = -1;",
        "HectonUIScaler.OnOriginShift rejects bad/no-op shifts before scale cache reset",
    )
    suit_hud = read_project_source("Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs")
    suit_hud_origin_shift = method_block(suit_hud, "public void OnOriginShift")
    suit_hud_scaler_origin_shift = method_block_after(suit_hud, "public void DisabledVisualSync()", "public void OnOriginShift")
    assert_contains_all(
        suit_hud_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "if (IsStencilRenderGraphSuppressedRuntime())",
            "_canvasStateApplied = false;",
            "QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);",
        ],
        "SuitHUDV4CanvasOverlay.OnOriginShift finite HUD refresh bridge",
    )
    for mutation in [
        "if (IsStencilRenderGraphSuppressedRuntime())",
        "_canvasStateApplied = false;",
        "QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);",
    ]:
        assert_before(
            suit_hud_origin_shift,
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            mutation,
            f"SuitHUDV4CanvasOverlay.OnOriginShift rejects bad/no-op shifts before {mutation}",
        )
    assert_contains_all(
        suit_hud_scaler_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_lastScreenWidth = -1;",
            "ApplyScaleToResolvedContentRoot(_contentRoot, force: true);",
        ],
        "SuitHUDV4CanvasOverlay scaler OnOriginShift finite UI scale bridge",
    )
    assert_before(
        suit_hud_scaler_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_lastScreenWidth = -1;",
        "SuitHUDV4CanvasOverlay scaler OnOriginShift rejects bad/no-op shifts before scale cache reset",
    )
    spatial_audio = read_project_source("Assets", "_Project", "Scripts", "SpatialAudioManager.cs")
    spatial_audio_origin_shift = method_block(spatial_audio, "public void OnOriginShift")
    assert_contains_all(
        spatial_audio_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "DumpVirtualVoiceBlackBox();",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "CompleteVirtualVoiceSort();",
        ],
        "SpatialAudioManager.OnOriginShift finite virtual-voice barrier bridge",
    )
    assert_before(
        spatial_audio_origin_shift,
        "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "CompleteVirtualVoiceSort();",
        "SpatialAudioManager.OnOriginShift rejects nonfinite shifts before virtual voice barrier",
    )
    assert_before(
        spatial_audio_origin_shift,
        "if (shiftSqrMagnitude <= 0.000001f)",
        "CompleteVirtualVoiceSort();",
        "SpatialAudioManager.OnOriginShift rejects no-op shifts before virtual voice barrier",
    )
    camera_rig = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "HectonPlayerCameraRig.cs")
    camera_rig_origin_shift = method_block(camera_rig, "public void OnOriginShift")
    assert_contains_all(
        camera_rig_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "if (cameraTransform != null)",
            "_originShiftTrackingLockFrame = SystemDispatcher.CurrentFrameIndex;",
        ],
        "HectonPlayerCameraRig.OnOriginShift finite camera tracking lock bridge",
    )
    assert_before(
        camera_rig_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_originShiftTrackingLockFrame = SystemDispatcher.CurrentFrameIndex;",
        "HectonPlayerCameraRig.OnOriginShift rejects bad/no-op shifts before camera tracking lock",
    )
    fabricator = read_project_source("Assets", "_Project", "Scripts", "Fabricator.cs")
    fabricator_origin_shift = method_block(fabricator, "public void OnOriginShift")
    assert_contains_all(
        fabricator_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "_fabricatorAupCached = false;",
            "CacheFabricatorAup();",
            "ApplyAssemblyVisualProgress(_assemblyProgress01, IsPausedNoPower);",
        ],
        "Fabricator.OnOriginShift finite crafting AUP bridge",
    )
    assert_before(
        fabricator_origin_shift,
        "if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "_fabricatorAupCached = false;",
        "Fabricator.OnOriginShift rejects bad/no-op shifts before crafting cache reset",
    )
    fabricator_ui = read_project_source("Assets", "_Project", "Scripts", "HectonFabricatorUI.cs")
    fabricator_ui_origin_shift = method_block(fabricator_ui, "public void OnOriginShift")
    assert_contains_all(
        fabricator_ui_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_recipeListRoot.hasChanged = true;",
            "_selectedHologramMatrixInitialized = false;",
            "InvalidateHologramMatrixCache();",
        ],
        "HectonFabricatorUI.OnOriginShift finite hologram/UI cache bridge",
    )
    assert_before(
        fabricator_ui_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_selectedHologramMatrixInitialized = false;",
        "HectonFabricatorUI.OnOriginShift rejects bad/no-op shifts before hologram cache reset",
    )
    base_airlock = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "BaseAirlock.cs")
    base_airlock_origin_shift = method_block(base_airlock, "public void OnOriginShift")
    assert_contains_all(
        base_airlock_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "_bulkheadPoseSnapshotValid = false;",
            "_bulkheadPoseShiftSequence = shiftData.Sequence;",
            "_pressurizationPublishPending = true;",
        ],
        "BaseAirlock.OnOriginShift finite bulkhead/pressurization bridge",
    )
    assert_before(
        base_airlock_origin_shift,
        "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "_bulkheadPoseSnapshotValid = false;",
        "BaseAirlock.OnOriginShift rejects bad/no-op shifts before pressurization republish",
    )
    vehicle_docking = read_project_source("Assets", "_Project", "Scripts", "Construction", "VehicleDockingModule.cs")
    vehicle_docking_origin_shift = method_block(vehicle_docking, "public void OnOriginShift")
    assert_contains_all(
        vehicle_docking_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "if (_dockedTransport == null || _dockedBehaviour == null)",
            "FinalizeDockedTransport();",
            "SnapDockedBodyToAnchor()",
            "RefreshDockedRelativeAup(anchor.position);",
        ],
        "VehicleDockingModule.OnOriginShift finite docked-body bridge",
    )
    assert_before(
        vehicle_docking_origin_shift,
        "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "FinalizeDockedTransport();",
        "VehicleDockingModule.OnOriginShift rejects bad/no-op shifts before docking finalize/snap",
    )
    vr_cable_drag = read_project_source("Assets", "_Project", "Scripts", "Interaction", "VRCableDragPlug.cs")
    vr_cable_drag_origin_shift = method_block(vr_cable_drag, "public void OnOriginShift")
    assert_contains_all(
        vr_cable_drag_origin_shift,
        [
            "Vector3 offset = shiftData.ShiftOffset;",
            "float offsetSqrMagnitude = offset.sqrMagnitude;",
            "if (!IsFiniteVector(offset) || !math.isfinite(offsetSqrMagnitude) || offsetSqrMagnitude <= 0.000001f)",
            "_manualPlugPosition -= offset;",
        ],
        "VRCableDragPlug.OnOriginShift finite manual plug bridge",
    )
    assert_before(
        vr_cable_drag_origin_shift,
        "if (!IsFiniteVector(offset) || !math.isfinite(offsetSqrMagnitude) || offsetSqrMagnitude <= 0.000001f)",
        "_manualPlugPosition -= offset;",
        "VRCableDragPlug.OnOriginShift rejects bad/no-op shifts before manual pose rebase",
    )
    loot_magnet = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "Loot", "LootMagnetSystem.cs")
    loot_magnet_origin_shift = method_block(loot_magnet, "public void OnOriginShift")
    assert_contains_all(
        loot_magnet_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!IsFiniteFloat3(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "ForceCompleteAndCommitScheduledJobForBarrier();",
            "ReapplyPulledProxyRuntimePoses(in rebaseViews);",
        ],
        "LootMagnetSystem.OnOriginShift finite job/AUP proxy bridge",
    )
    assert_before(
        loot_magnet_origin_shift,
        "if (!IsFiniteFloat3(shiftOffset) ||",
        "ForceCompleteAndCommitScheduledJobForBarrier();",
        "LootMagnetSystem.OnOriginShift rejects bad shifts before scheduled job barrier",
    )
    assert_before(
        loot_magnet_origin_shift,
        "if (shiftSqrMagnitude <= 0.000001f)",
        "ForceCompleteAndCommitScheduledJobForBarrier();",
        "LootMagnetSystem.OnOriginShift rejects no-op shifts before scheduled job barrier",
    )
    foveated_manager = read_project_source("Assets", "_Project", "Scripts", "Core", "FoveatedSimulationManager.cs")
    foveated_origin_shift = method_block(foveated_manager, "public void OnOriginShift")
    assert_contains_all(
        foveated_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= MinimumVelocityDelta)",
            "ForceCompleteFrameJobsInPostSimulationWindow();",
            "_visualFromPositions[i] -= shiftOffset;",
            "_visualTargetCacheDirty = true;",
        ],
        "FoveatedSimulationManager.OnOriginShift finite visual target bridge",
    )
    assert_before(
        foveated_origin_shift,
        "if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= MinimumVelocityDelta)",
        "ForceCompleteFrameJobsInPostSimulationWindow();",
        "FoveatedSimulationManager.OnOriginShift rejects bad/no-op shifts before visual target rebase",
    )
    radiation_grid = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "RadiationHazardGrid.cs")
    radiation_origin_shift = method_block(radiation_grid, "public void OnOriginShift")
    assert_contains_all(
        radiation_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_lastShiftSequence = shiftData.Sequence;",
            "RecordTelemetry(_gridOriginAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagOriginShift);",
        ],
        "RadiationHazardGrid.OnOriginShift finite telemetry bridge",
    )
    assert_before(
        radiation_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_lastShiftSequence = shiftData.Sequence;",
        "RadiationHazardGrid.OnOriginShift rejects bad/no-op shifts before telemetry sequence advance",
    )
    foundation_pylon_gpu = read_project_source("Assets", "_Project", "Scripts", "Construction", "FoundationPylonGpuBatch.cs")
    foundation_pylon_origin_shift = method_block(foundation_pylon_gpu, "public void OnOriginShift")
    assert_contains_all(
        foundation_pylon_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "_cachedOriginAup = shiftData.NewTotalOffsetDouble;",
            "ClearUploadedBatch();",
        ],
        "FoundationPylonGpuBatch.OnOriginShift finite GPU origin snapshot bridge",
    )
    assert_before(
        foundation_pylon_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_cachedOriginAup = shiftData.NewTotalOffsetDouble;",
        "FoundationPylonGpuBatch.OnOriginShift rejects bad/no-op shifts before GPU cache discard",
    )
    sdf_drill = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "Mining", "DeployableSdfDrillRuntime.cs")
    sdf_drill_origin_shift = method_block(sdf_drill, "public void OnOriginShift")
    assert_contains_all(
        sdf_drill_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector3(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "Vector3 runtime = _anchorAup.ToRuntimeFloat3();",
            "_anchorRuntimePosition = new float3(runtime.x, runtime.y, runtime.z);",
            "_cachedTransform.position = runtime;",
        ],
        "DeployableSdfDrillRuntime.OnOriginShift finite AUP anchor bridge",
    )
    assert_before(
        sdf_drill_origin_shift,
        "if (!IsFiniteVector3(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "Vector3 runtime = _anchorAup.ToRuntimeFloat3();",
        "DeployableSdfDrillRuntime.OnOriginShift rejects bad/no-op shifts before anchor projection",
    )
    weld_target = read_project_source("Assets", "_Project", "Scripts", "Construction", "VRConstructionWeldTarget.cs")
    weld_target_origin_shift = method_block(weld_target, "public void OnOriginShift")
    assert_contains_all(
        weld_target_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "CacheCornerRuntimePositions();",
            "UpdateWeldGlowProxyRegistration();",
        ],
        "VRConstructionWeldTarget.OnOriginShift finite weld proxy bridge",
    )
    assert_before(
        weld_target_origin_shift,
        "if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "CacheCornerRuntimePositions();",
        "VRConstructionWeldTarget.OnOriginShift rejects bad/no-op shifts before weld proxy refresh",
    )
    buoyancy_displacement = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Physics",
        "Buoyancy",
        "BuoyancyDisplacementRuntime.cs",
    )
    buoyancy_displacement_origin_shift = method_block(buoyancy_displacement, "public void OnOriginShift")
    assert_contains_all(
        buoyancy_displacement_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "_cachedSectorAup = shiftData.NewTotalOffsetDouble;",
        ],
        "BuoyancyDisplacementRuntime.OnOriginShift finite sector snapshot bridge",
    )
    assert_before(
        buoyancy_displacement_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_cachedSectorAup = shiftData.NewTotalOffsetDouble;",
        "BuoyancyDisplacementRuntime.OnOriginShift rejects bad/no-op shifts before sector snapshot mutation",
    )
    async_buoyancy = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Physics",
        "Buoyancy",
        "AsyncReadback",
        "AsyncBuoyancyReadbackRuntime.cs",
    )
    async_buoyancy_origin_shift = method_block(async_buoyancy, "public void OnOriginShift")
    assert_contains_all(
        async_buoyancy_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "ApplyOriginSnapshot(in shiftData);",
        ],
        "AsyncBuoyancyReadbackRuntime.OnOriginShift finite readback origin bridge",
    )
    assert_before(
        async_buoyancy_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "ApplyOriginSnapshot(in shiftData);",
        "AsyncBuoyancyReadbackRuntime.OnOriginShift rejects bad/no-op shifts before origin snapshot mutation",
    )
    gerstner_wave = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Physics",
        "Buoyancy",
        "AnalyticalGerstnerWaveRuntime.cs",
    )
    gerstner_origin_shift = method_block(gerstner_wave, "public void OnOriginShift")
    assert_contains_all(
        gerstner_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "ApplyOriginSnapshot(in shiftData);",
        ],
        "AnalyticalGerstnerWaveRuntime.OnOriginShift finite wave origin bridge",
    )
    assert_before(
        gerstner_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "ApplyOriginSnapshot(in shiftData);",
        "AnalyticalGerstnerWaveRuntime.OnOriginShift rejects bad/no-op shifts before wave origin snapshot mutation",
    )
    biome_boundary = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "World",
        "Biomes",
        "BiomeBoundarySdfRuntime.cs",
    )
    biome_boundary_origin_shift = method_block(biome_boundary, "public void OnOriginShift")
    assert_contains_all(
        biome_boundary_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_lastOriginShiftSequence = shiftData.Sequence;",
        ],
        "BiomeBoundarySdfRuntime.OnOriginShift finite telemetry sequence bridge",
    )
    assert_before(
        biome_boundary_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_lastOriginShiftSequence = shiftData.Sequence;",
        "BiomeBoundarySdfRuntime.OnOriginShift rejects bad/no-op shifts before sequence stamp",
    )
    biome_transition = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "World",
        "Biomes",
        "BiomeTransitionManagerRuntime.cs",
    )
    biome_transition_origin_shift = method_block(biome_transition, "public void OnOriginShift")
    assert_contains_all(
        biome_transition_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_lastOriginShiftSequence = shiftData.Sequence;",
        ],
        "BiomeTransitionManagerRuntime.OnOriginShift finite shader/telemetry sequence bridge",
    )
    assert_before(
        biome_transition_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_lastOriginShiftSequence = shiftData.Sequence;",
        "BiomeTransitionManagerRuntime.OnOriginShift rejects bad/no-op shifts before sequence stamp",
    )
    toxic_chemistry = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Atmosphere",
        "ToxicOutgassingChemistryRuntime.cs",
    )
    toxic_chemistry_origin_shift = method_block(toxic_chemistry, "public void OnOriginShift")
    assert_contains_all(
        toxic_chemistry_origin_shift,
        [
            "Vector3 shiftVector = shiftData.ShiftOffset;",
            "float3 shift = new float3(shiftVector.x, shiftVector.y, shiftVector.z);",
            "float shiftSqrMagnitude = math.lengthsq(shift);",
            "if (!math.all(math.isfinite(shift)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "_pendingRebaseCells += new int3((int)math.round(cells.x), (int)math.round(cells.y), (int)math.round(cells.z));",
            "_gridOriginAup = shiftData.NewTotalOffsetDouble;",
            "_hasPendingRebase = math.any(_pendingRebaseCells != int3.zero);",
        ],
        "ToxicOutgassingChemistryRuntime.OnOriginShift finite chemical grid bridge",
    )
    assert_before(
        toxic_chemistry_origin_shift,
        "if (!math.all(math.isfinite(shift)) ||",
        "_pendingRebaseCells += new int3((int)math.round(cells.x), (int)math.round(cells.y), (int)math.round(cells.z));",
        "ToxicOutgassingChemistryRuntime.OnOriginShift rejects bad/no-op shifts before pending rebase accumulation",
    )
    assert_before(
        toxic_chemistry_origin_shift,
        "_pendingRebaseCells += new int3((int)math.round(cells.x), (int)math.round(cells.y), (int)math.round(cells.z));",
        "_gridOriginAup = shiftData.NewTotalOffsetDouble;",
        "ToxicOutgassingChemistryRuntime.OnOriginShift uses committed total AUP after deriving rebase cells",
    )
    storm_runtime = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Atmosphere",
        "StormPropagation",
        "ShinobuStormPropagationRuntime.cs",
    )
    storm_origin_shift = method_block(storm_runtime, "public void OnOriginShift")
    assert_contains_all(
        storm_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "_cachedOriginFallbackAup = SanitizeAup(shiftData.NewTotalOffsetDouble);",
        ],
        "ShinobuStormPropagationRuntime.OnOriginShift finite storm origin fallback bridge",
    )
    assert_before(
        storm_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_cachedOriginFallbackAup = SanitizeAup(shiftData.NewTotalOffsetDouble);",
        "ShinobuStormPropagationRuntime.OnOriginShift rejects bad/no-op shifts before fallback origin mutation",
    )
    stress_spawn = read_project_source("Assets", "_Project", "Scripts", "Fauna", "StressDrivenSpawnDirector.cs")
    stress_spawn_origin_shift = method_block(stress_spawn, "public void OnOriginShift")
    assert_contains_all(
        stress_spawn_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "if (!math.all(math.isfinite(origin)))",
            "_floatingOriginSnapshotValid = false;",
            "_dumpFaultPending = 1;",
            "_cachedFloatingOriginOffset = origin;",
            "_cachedFloatingOriginSequence = shiftData.Sequence;",
        ],
        "StressDrivenSpawnDirector.OnOriginShift finite spawn origin snapshot bridge",
    )
    assert_before(
        stress_spawn_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_cachedFloatingOriginOffset = origin;",
        "StressDrivenSpawnDirector.OnOriginShift rejects bad/no-op shifts before cached origin mutation",
    )
    assert_before(
        stress_spawn_origin_shift,
        "if (!math.all(math.isfinite(origin)))",
        "_cachedFloatingOriginOffset = origin;",
        "StressDrivenSpawnDirector.OnOriginShift rejects nonfinite total AUP before cached origin mutation",
    )
    fauna_brain = read_project_source("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs")
    fauna_brain_origin_shift = method_block(fauna_brain, "public void OnOriginShift")
    assert_contains_all(
        fauna_brain_origin_shift,
        [
            "float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shiftOffset);",
            "if (!math.all(math.isfinite(shiftOffset)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "_voxelRouteOriginShiftRefreshActive = true;",
            "RefreshVoxelRouteRuntimeCacheFromAup(in shiftData);",
            "RefreshForcedMigrationTargetFromAup(in shiftData);",
        ],
        "FaunaBrain.OnOriginShift finite route/hunt target bridge",
    )
    assert_before(
        fauna_brain_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) ||",
        "_voxelRouteOriginShiftRefreshActive = true;",
        "FaunaBrain.OnOriginShift rejects bad/no-op shifts before route cache refresh",
    )
    organic_manager = read_project_source("Assets", "_Project", "Scripts", "World", "DestructibleOrganicManager.cs")
    organic_origin_shift = method_block(organic_manager, "public void OnOriginShift")
    assert_contains_all(
        organic_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;",
            "Vector3 resolvedRuntimePosition = ToRuntimeVector3(runtimePosition);",
            "if (!IsFiniteVector(resolvedRuntimePosition))",
            "record.Position = resolvedRuntimePosition;",
        ],
        "DestructibleOrganicManager.OnOriginShift finite corpse attractor bridge",
    )
    assert_before(
        organic_origin_shift,
        "if (!IsFiniteVector(shiftOffset) ||",
        "double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;",
        "DestructibleOrganicManager.OnOriginShift rejects bad/no-op shifts before corpse cache reproject",
    )
    assert_before(
        organic_origin_shift,
        "if (!IsFiniteVector(resolvedRuntimePosition))",
        "record.Position = resolvedRuntimePosition;",
        "DestructibleOrganicManager.OnOriginShift rejects nonfinite per-record runtime conversion",
    )
    flora_interaction = read_project_source("Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs")
    flora_interaction_origin_shift = method_block(flora_interaction, "public void OnOriginShift")
    assert_contains_all(
        flora_interaction_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!isActiveAndEnabled ||",
            "!IsFiniteVector3(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.0001f)",
            "CompleteWakeDecayJob(forceComplete: true, dispatcherSwapWindow: false);",
            "ClearFloraSwayDisplacementField(forceUpload: true);",
            "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
        ],
        "FloraInteractionManager.OnOriginShift finite flora interaction cache bridge",
    )
    assert_before(
        flora_interaction_origin_shift,
        "if (!isActiveAndEnabled ||",
        "CompleteWakeDecayJob(forceComplete: true, dispatcherSwapWindow: false);",
        "FloraInteractionManager.OnOriginShift rejects bad/no-op shifts before wake/sway cache mutation",
    )
    indirect_vegetation = read_project_source("Assets", "_Project", "Scripts", "World", "HectonIndirectVegetationRenderer.cs")
    indirect_vegetation_origin_shift = method_block(indirect_vegetation, "public void OnOriginShift")
    assert_contains_all(
        indirect_vegetation_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_cachedCullCameraPosition -= shiftOffset;",
            "_explicitBounds.center -= shiftOffset;",
        ],
        "HectonIndirectVegetationRenderer.OnOriginShift finite indirect draw cache bridge",
    )
    assert_before(
        indirect_vegetation_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "_cachedCullCameraPosition -= shiftOffset;",
        "HectonIndirectVegetationRenderer.OnOriginShift rejects bad/no-op shifts before draw bounds mutation",
    )
    micro_fauna = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs")
    micro_fauna_origin_shift = method_block(micro_fauna, "public void OnOriginShift")
    assert_contains_all(
        micro_fauna_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteVector3(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.0001f)",
            "InvalidateViewPoseCache();",
            "ApplyRuntimeOffsetToSwarmData(-shiftOffset);",
        ],
        "SargassumMicroFaunaBoids.OnOriginShift finite swarm cache bridge",
    )
    assert_before(
        micro_fauna_origin_shift,
        "if (!IsFiniteVector3(shiftOffset) ||",
        "ApplyRuntimeOffsetToSwarmData(-shiftOffset);",
        "SargassumMicroFaunaBoids.OnOriginShift rejects bad/no-op shifts before swarm cache mutation",
    )
    collapse_chunk = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumCollapseChunk.cs")
    collapse_chunk_origin_shift = method_block(collapse_chunk, "public void OnOriginShift")
    assert_contains_all(
        collapse_chunk_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!isActiveAndEnabled ||",
            "!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_snagConnectedAnchor -= shiftOffset;",
            "RebaseWorldSpaceParticles(siltTrail, _siltTrailShiftParticles, shiftOffset);",
        ],
        "SargassumCollapseChunk.OnOriginShift finite collapse particle bridge",
    )
    assert_before(
        collapse_chunk_origin_shift,
        "if (!isActiveAndEnabled ||",
        "_snagConnectedAnchor -= shiftOffset;",
        "SargassumCollapseChunk.OnOriginShift rejects bad/no-op shifts before snag/particle mutation",
    )
    biolum_manager = read_project_source("Assets", "_Project", "Scripts", "World", "Biolum", "HectonBiolumManager.cs")
    biolum_origin_shift = method_block(biolum_manager, "public void OnOriginShift")
    assert_contains_all(
        biolum_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float3 shift = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
            "float shiftSqrMagnitude = math.lengthsq(shift);",
            "if (!math.all(math.isfinite(shift)) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_touchRipples[i].RuntimePosition -= shift;",
        ],
        "HectonBiolumManager.OnOriginShift finite touch-ripple bridge",
    )
    assert_before(
        biolum_origin_shift,
        "if (!math.all(math.isfinite(shift)) ||",
        "_touchRipples[i].RuntimePosition -= shift;",
        "HectonBiolumManager.OnOriginShift rejects bad/no-op shifts before touch-ripple mutation",
    )
    procedural_scatter = read_project_source("Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs")
    procedural_scatter_origin_shift = method_block(procedural_scatter, "public void OnOriginShift")
    assert_contains_all(
        procedural_scatter_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))",
            "InvalidateObserverAbsolutePositionCache();",
            "RebuildFloraGpuiMatricesForCommittedOrigin();",
        ],
        "WorldProceduralScatterDirector.OnOriginShift finite GPUI scatter bridge",
    )
    assert_before(
        procedural_scatter_origin_shift,
        "if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||",
        "InvalidateObserverAbsolutePositionCache();",
        "WorldProceduralScatterDirector.OnOriginShift rejects bad/no-op shifts before GPUI rebuild",
    )
    thermodynamics_hazard_grid = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Thermodynamics",
        "ThermodynamicsHazardGridRuntime.cs",
    )
    thermodynamics_hazard_origin_shift = method_block(thermodynamics_hazard_grid, "public void OnOriginShift")
    assert_contains_all(
        thermodynamics_hazard_origin_shift,
        [
            "float safeCellSize = math.max(1f, cellSizeMeters);",
            "float3 shift = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);",
            "if (!math.all(math.isfinite(shift)))",
            "int3 shiftCells = (int3)math.round(shift / safeCellSize);",
            "if (math.all(shiftCells == int3.zero))",
            "_pendingRebaseCells += shiftCells;",
            "_shiftSequence = shiftData.Sequence;",
        ],
        "ThermodynamicsHazardGridRuntime.OnOriginShift finite cell rebase bridge",
    )
    assert_before(
        thermodynamics_hazard_origin_shift,
        "if (!math.all(math.isfinite(shift)))",
        "_pendingRebaseCells += shiftCells;",
        "ThermodynamicsHazardGridRuntime.OnOriginShift rejects nonfinite shift before pending cell rebase",
    )
    assert_before(
        thermodynamics_hazard_origin_shift,
        "if (math.all(shiftCells == int3.zero))",
        "_shiftSequence = shiftData.Sequence;",
        "ThermodynamicsHazardGridRuntime.OnOriginShift rejects zero-cell shift before sequence advance",
    )
    abyssal_thermodynamics_solver = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Thermodynamics",
        "AbyssalThermodynamicsSolver.cs",
    )
    abyssal_thermodynamics_origin_shift = method_block(abyssal_thermodynamics_solver, "public void OnOriginShift")
    assert_contains_all(
        abyssal_thermodynamics_origin_shift,
        [
            "float3 shiftOffset = new float3(",
            "shiftData.ShiftOffset.x,",
            "shiftData.ShiftOffset.y,",
            "shiftData.ShiftOffset.z);",
            "if (!math.all(math.isfinite(shiftOffset)) || math.lengthsq(shiftOffset) <= 0.000001f)",
            "_lastShiftSequence = shiftData.Sequence;",
            "tuning->LastShiftSequence = shiftData.Sequence;",
        ],
        "AbyssalThermodynamicsSolver.OnOriginShift finite thermal sequence bridge",
    )
    assert_before(
        abyssal_thermodynamics_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) || math.lengthsq(shiftOffset) <= 0.000001f)",
        "_lastShiftSequence = shiftData.Sequence;",
        "AbyssalThermodynamicsSolver.OnOriginShift rejects bad/no-op shifts before solver sequence advance",
    )
    assert_before(
        abyssal_thermodynamics_origin_shift,
        "if (!math.all(math.isfinite(shiftOffset)) || math.lengthsq(shiftOffset) <= 0.000001f)",
        "tuning->LastShiftSequence = shiftData.Sequence;",
        "AbyssalThermodynamicsSolver.OnOriginShift rejects bad/no-op shifts before DataVault tuning write",
    )
    mapmagic_vegetation = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "World",
        "HectonMapMagicVegetationBridge.cs",
    )
    mapmagic_origin_shift = method_block(mapmagic_vegetation, "public void OnOriginShift")
    mapmagic_try_apply_shift = method_block(mapmagic_vegetation, "private bool TryApplyWorldOffsetToAllChunks")
    mapmagic_queue_shift = method_block(mapmagic_vegetation, "private void QueuePendingWorldOffset")
    mapmagic_apply_pending_shift = method_block(mapmagic_vegetation, "private void TryApplyPendingWorldOffset")
    mapmagic_clear_pending_shift = method_block(mapmagic_vegetation, "private void ClearPendingWorldOffset")
    mapmagic_apply_shift_immediate = method_block(mapmagic_vegetation, "private void ApplyWorldOffsetToAllChunksImmediate")
    mapmagic_finite_vector = method_block(mapmagic_vegetation, "private static bool IsFiniteVector")
    assert_contains_all(
        mapmagic_origin_shift,
        [
            "if (!isActiveAndEnabled)",
            "TryApplyWorldOffsetToAllChunks(shiftData.ShiftOffset, -shiftData.NewTotalOffsetDouble, refreshResidency: true);",
        ],
        "HectonMapMagicVegetationBridge.OnOriginShift routes through finite queued offset bridge",
    )
    assert_before(
        mapmagic_origin_shift,
        "if (!isActiveAndEnabled)",
        "TryApplyWorldOffsetToAllChunks(shiftData.ShiftOffset, -shiftData.NewTotalOffsetDouble, refreshResidency: true);",
        "HectonMapMagicVegetationBridge.OnOriginShift rejects inactive runtime before chunk offset bridge",
    )
    assert_contains_all(
        mapmagic_try_apply_shift,
        [
            "float offsetSqrMagnitude = offset.sqrMagnitude;",
            "if (!IsFiniteVector(offset) ||",
            "!math.isfinite(offsetSqrMagnitude) ||",
            "!math.all(math.isfinite(newTotalUniverseOffsetDouble)))",
            "ClearPendingWorldOffset();",
            "if (offsetSqrMagnitude <= 0.000001f)",
            "QueuePendingWorldOffset(offset, newTotalUniverseOffsetDouble);",
            "ApplyWorldOffsetToAllChunksImmediate(offset, newTotalUniverseOffsetDouble, refreshResidency);",
        ],
        "HectonMapMagicVegetationBridge.TryApplyWorldOffsetToAllChunks finite queued offset ingress",
    )
    assert_before(
        mapmagic_try_apply_shift,
        "if (!IsFiniteVector(offset) ||",
        "QueuePendingWorldOffset(offset, newTotalUniverseOffsetDouble);",
        "HectonMapMagicVegetationBridge.TryApplyWorldOffsetToAllChunks rejects bad offset before queueing",
    )
    assert_contains_all(
        mapmagic_queue_shift,
        [
            "Vector3 accumulatedOffset = _hasPendingWorldOffset ? _pendingWorldOffset + offset : offset;",
            "if (!IsFiniteVector(accumulatedOffset) ||",
            "!math.all(math.isfinite(newTotalUniverseOffsetDouble)))",
            "ClearPendingWorldOffset();",
            "_pendingWorldOffset = accumulatedOffset;",
            "_pendingWorldOffsetDouble = newTotalUniverseOffsetDouble;",
            "_hasPendingWorldOffset = true;",
        ],
        "HectonMapMagicVegetationBridge.QueuePendingWorldOffset finite pending accumulation",
    )
    assert_before(
        mapmagic_queue_shift,
        "if (!IsFiniteVector(accumulatedOffset) ||",
        "_pendingWorldOffset = accumulatedOffset;",
        "HectonMapMagicVegetationBridge.QueuePendingWorldOffset rejects nonfinite accumulated pending shift",
    )
    assert_contains_all(
        mapmagic_apply_pending_shift,
        [
            "Vector3 pendingOffset = _pendingWorldOffset;",
            "double3 pendingTotalOffset = _pendingWorldOffsetDouble;",
            "_pendingWorldOffset = default;",
            "_pendingWorldOffsetDouble = default;",
            "_hasPendingWorldOffset = false;",
            "if (!IsFiniteVector(pendingOffset) ||",
            "pendingOffset.sqrMagnitude <= 0.000001f ||",
            "!math.all(math.isfinite(pendingTotalOffset)))",
            "ApplyWorldOffsetToAllChunksImmediate(pendingOffset, pendingTotalOffset, refreshResidency: false);",
        ],
        "HectonMapMagicVegetationBridge.TryApplyPendingWorldOffset finite pending drain",
    )
    assert_before(
        mapmagic_apply_pending_shift,
        "if (!IsFiniteVector(pendingOffset) ||",
        "ApplyWorldOffsetToAllChunksImmediate(pendingOffset, pendingTotalOffset, refreshResidency: false);",
        "HectonMapMagicVegetationBridge.TryApplyPendingWorldOffset rejects bad pending shift before chunk mutation",
    )
    assert_contains_all(
        mapmagic_clear_pending_shift,
        ["_pendingWorldOffset = default;", "_pendingWorldOffsetDouble = default;", "_hasPendingWorldOffset = false;"],
        "HectonMapMagicVegetationBridge.ClearPendingWorldOffset lifecycle reset",
    )
    assert_contains_all(
        mapmagic_apply_shift_immediate,
        [
            "float offsetSqrMagnitude = offset.sqrMagnitude;",
            "if (!IsFiniteVector(offset) ||",
            "!math.isfinite(offsetSqrMagnitude) ||",
            "!math.all(math.isfinite(newTotalUniverseOffsetDouble)) ||",
            "offsetSqrMagnitude <= 0.000001f)",
            "_totalUniverseOffsetDouble = newTotalUniverseOffsetDouble;",
        ],
        "HectonMapMagicVegetationBridge.ApplyWorldOffsetToAllChunksImmediate finite chunk rebase",
    )
    assert_before(
        mapmagic_apply_shift_immediate,
        "if (!IsFiniteVector(offset) ||",
        "_totalUniverseOffsetDouble = newTotalUniverseOffsetDouble;",
        "HectonMapMagicVegetationBridge.ApplyWorldOffsetToAllChunksImmediate rejects bad offset before universe offset mutation",
    )
    assert_contains_all(
        mapmagic_finite_vector,
        [
            "return math.isfinite(value.x) &&",
            "math.isfinite(value.y) &&",
            "math.isfinite(value.z);",
        ],
        "HectonMapMagicVegetationBridge.IsFiniteVector finite guard",
    )
    gpu_scatter_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "World", "GPUScatterDirector.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        gpu_scatter_origin_shift,
        [
            "double3 newTotalOffsetDouble = shiftData.NewTotalOffsetDouble;",
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!math.all(math.isfinite(newTotalOffsetDouble)) ||",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude))",
            "_scatterAupGenerationOffsetXZDouble = new double2(newTotalOffsetDouble.x, newTotalOffsetDouble.z);",
            "_lastFoveatedCenter += -shiftOffset;",
        ],
        "GPUScatterDirector.OnOriginShift finite scatter/AUP bridge",
    )
    assert_before(
        gpu_scatter_origin_shift,
        "!math.all(math.isfinite(newTotalOffsetDouble)) ||",
        "_scatterAupGenerationOffsetXZDouble = new double2(newTotalOffsetDouble.x, newTotalOffsetDouble.z);",
        "GPUScatterDirector.OnOriginShift rejects nonfinite total AUP offset before scatter generation mutation",
    )
    assert_before(
        gpu_scatter_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_lastFoveatedCenter += -shiftOffset;",
        "GPUScatterDirector.OnOriginShift rejects nonfinite shift before foveated cache rebase",
    )
    for origin_shift_path_parts, origin_shift_label, threshold_fragment, mutation_fragment in [
        (
            ("Assets", "_Project", "Scripts", "World", "FloraRegrowthDirector.cs"),
            "FloraRegrowthDirector.OnOriginShift",
            "shiftSqrMagnitude <= 0.000001f)",
            "ApplyOriginShiftToCachedFloraState(runtimeOffset);",
        ),
        (
            ("Assets", "_Project", "Scripts", "World", "HectonOctahedralImpostorRenderer.cs"),
            "HectonOctahedralImpostorRenderer.OnOriginShift",
            "shiftSqrMagnitude <= 0.0001f)",
            "drawBounds.center -= shiftOffset;",
        ),
    ]:
        origin_shift_block = method_block(read_project_source(*origin_shift_path_parts), "public void OnOriginShift")
        assert_contains_all(
            origin_shift_block,
            [
                "Vector3 shiftOffset = shiftData.ShiftOffset;",
                "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
                "!MathGuard.IsFinite(shiftOffset) ||",
                "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
                threshold_fragment,
                mutation_fragment,
            ],
            origin_shift_label,
        )
        assert_before(
            origin_shift_block,
            "!MathGuard.IsFinite(shiftOffset) ||",
            mutation_fragment,
            f"{origin_shift_label} rejects nonfinite shift before world/render cache mutation",
        )
    for render_bounds_path_parts, render_bounds_label, mutation_fragment in [
        (
            ("Assets", "_Project", "Scripts", "World", "HectonHLODRenderer.cs"),
            "HectonHLODRenderer.OnOriginShift",
            "drawBounds.center -= shiftOffset;",
        ),
        (
            ("Assets", "_Project", "Scripts", "World", "HectonDistantLandmarkRenderer.cs"),
            "HectonDistantLandmarkRenderer.OnOriginShift",
            "Vector3 runtimeOffset = -shiftOffset;",
        ),
    ]:
        render_bounds_origin_shift = method_block(
            read_project_source(*render_bounds_path_parts),
            "public void OnOriginShift",
        )
        assert_contains_all(
            render_bounds_origin_shift,
            [
                "Vector3 shiftOffset = shiftData.ShiftOffset;",
                "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
                "!IsFinite(shiftOffset) ||",
                "!float.IsFinite(shiftSqrMagnitude) ||",
                "shiftSqrMagnitude <= 0.0001f)",
                mutation_fragment,
            ],
            f"{render_bounds_label} finite checked render bounds rebase",
        )
        assert_before(
            render_bounds_origin_shift,
            "!IsFinite(shiftOffset) ||",
            mutation_fragment,
            f"{render_bounds_label} rejects nonfinite shift before render bounds mutation",
        )
    gpu_scatter_lod_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "Rendering", "Scatter", "GpuScatterLodManager.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        gpu_scatter_lod_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "bool hasFiniteShift = IsFiniteVector(shiftOffset) && math.isfinite(shiftSqrMagnitude);",
            "if (_hasExplicitDrawBounds && hasFiniteShift && shiftSqrMagnitude > 0.000001f)",
            "_drawBounds.center -= shiftOffset;",
            "else if (_hasExplicitDrawBounds && !hasFiniteShift)",
        ],
        "GpuScatterLodManager.OnOriginShift finite explicit draw bounds rebase",
    )
    assert_before(
        gpu_scatter_lod_origin_shift,
        "bool hasFiniteShift = IsFiniteVector(shiftOffset) && math.isfinite(shiftSqrMagnitude);",
        "_drawBounds.center -= shiftOffset;",
        "GpuScatterLodManager.OnOriginShift validates shift before explicit draw bounds mutation",
    )
    world_spatial_hash_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "World", "WorldSpatialHashGrid.cs"),
        "internal static void HandleOriginShift",
    )
    assert_contains_all(
        world_spatial_hash_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
            "ClearAcousticDensityMapForOriginShift();",
            "if (shiftSqrMagnitude <= 0.000001f)",
            "Vector3 runtimeOffset = -shiftOffset;",
            "RebaseTransientSignalRuntimePositions(runtimeOffset);",
        ],
        "WorldSpatialHashGrid.HandleOriginShift finite/no-op spatial hash rebase",
    )
    assert_before(
        world_spatial_hash_origin_shift,
        "if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude))",
        "Vector3 runtimeOffset = -shiftOffset;",
        "WorldSpatialHashGrid.HandleOriginShift rejects nonfinite shift before spatial cache rebase",
    )
    wreck_material_registry = read_project_source("Assets", "_Project", "Scripts", "World", "WreckMaterialRegistry.cs")
    wreck_origin_shift = method_block(wreck_material_registry, "public void OnOriginShift")
    wreck_has_usable_shift = method_block(wreck_material_registry, "private static bool _HasUsableShift")
    assert_contains_all(
        wreck_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "if (!_hasPublishedWreck || !_HasUsableShift(shiftOffset))",
            "Vector3 runtimeOffset = -shiftOffset;",
            "_publishedWorldBounds.center += runtimeOffset;",
        ],
        "WreckMaterialRegistry.OnOriginShift finite shift helper route",
    )
    assert_contains_all(
        wreck_has_usable_shift,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "MathGuard.IsFinite(shiftOffset) &&",
            "MathGuard.IsFinite(shiftSqrMagnitude) &&",
            "shiftSqrMagnitude > 0.0001f;",
        ],
        "WreckMaterialRegistry._HasUsableShift finite helper",
    )
    assert_before(
        wreck_origin_shift,
        "if (!_hasPublishedWreck || !_HasUsableShift(shiftOffset))",
        "Vector3 runtimeOffset = -shiftOffset;",
        "WreckMaterialRegistry.OnOriginShift validates shift before wreck bounds mutation",
    )
    submarine_ballast_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "Gameplay", "SubmarineAutoLevelBallastController.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        submarine_ballast_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!IsFinite(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_previousPidError = float3.zero;",
            "_pendingTelemetryFlags |= PidTelemetryFlagOriginShiftReset;",
        ],
        "SubmarineAutoLevelBallastController.OnOriginShift finite PID reset",
    )
    assert_before(
        submarine_ballast_origin_shift,
        "!IsFinite(shiftOffset) ||",
        "_previousPidError = float3.zero;",
        "SubmarineAutoLevelBallastController.OnOriginShift rejects nonfinite shift before PID reset",
    )
    vehicle_motor = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "VehicleMotor.cs")
    vehicle_motor_origin_shift = method_block(vehicle_motor, "public void OnOriginShift")
    vehicle_motor_apply_shift = method_block(vehicle_motor, "private void ApplyOriginShift")
    assert_contains_all(
        vehicle_motor_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude))",
            "ApplyOriginShift(shiftOffset, shiftData.IsSafeTeleport != 0);",
            "_visualTeleportPending = true;",
        ],
        "VehicleMotor.OnOriginShift finite safe-teleport bridge",
    )
    assert_before(
        vehicle_motor_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "ApplyOriginShift(shiftOffset, shiftData.IsSafeTeleport != 0);",
        "VehicleMotor.OnOriginShift rejects nonfinite shift before vehicle state bridge",
    )
    assert_contains_all(
        vehicle_motor_apply_shift,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= MinVectorMagnitudeSq)",
            "_entanglementAnchorPosition -= shiftOffset;",
            "_lastBlockingImpactPoint -= shiftOffset;",
        ],
        "VehicleMotor.ApplyOriginShift finite cached kinematics",
    )
    assert_before(
        vehicle_motor_apply_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_entanglementAnchorPosition -= shiftOffset;",
        "VehicleMotor.ApplyOriginShift rejects nonfinite shift before entanglement anchor mutation",
    )
    mountable_transport_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "Gameplay", "MountablePlayerTransport.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        mountable_transport_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!IsFiniteVector(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_previousPlatformPosition -= shiftOffset;",
        ],
        "MountablePlayerTransport.OnOriginShift finite platform cache",
    )
    assert_before(
        mountable_transport_origin_shift,
        "!IsFiniteVector(shiftOffset) ||",
        "_previousPlatformPosition -= shiftOffset;",
        "MountablePlayerTransport.OnOriginShift rejects nonfinite shift before platform cache mutation",
    )
    debris_manager = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "DebrisManager.cs")
    debris_tick = method_block(debris_manager, "public void Tick(float deltaTime)")
    debris_origin_shift = method_block(debris_manager, "public void OnOriginShift")
    debris_apply_shift = method_block(debris_manager, "private void ApplyShiftToBuffer")
    assert_contains_all(
        debris_tick,
        [
            "float pendingShiftSqrMagnitude = _pendingShiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(_pendingShiftOffset) ||",
            "!MathGuard.IsFinite(pendingShiftSqrMagnitude))",
            "_pendingShiftOffset = Vector3.zero;",
            "else if (pendingShiftSqrMagnitude > 0.000001f && !_simulationScheduled)",
            "ApplyShiftToBuffer(shiftedFrontStates, _pendingShiftOffset);",
        ],
        "DebrisManager.Tick finite pending origin-shift lifecycle",
    )
    assert_contains_all(
        debris_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "_pendingShiftOffset += shiftOffset;",
            "ApplyShiftToBuffer(frontStates, shiftOffset);",
        ],
        "DebrisManager.OnOriginShift finite scheduled-buffer bridge",
    )
    assert_before(
        debris_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "_pendingShiftOffset += shiftOffset;",
        "DebrisManager.OnOriginShift rejects nonfinite shift before deferred debris rebase",
    )
    assert_contains_all(
        debris_apply_shift,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.000001f)",
            "float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
        ],
        "DebrisManager.ApplyShiftToBuffer finite debris buffer mutation",
    )
    assert_before(
        debris_apply_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
        "DebrisManager.ApplyShiftToBuffer rejects nonfinite shift before native buffer mutation",
    )
    crest_depth_cache = read_project_source(
        "Assets",
        "_Project",
        "Scripts",
        "Plugins",
        "Crest",
        "HectonCrestOceanDepthCacheBootstrap.cs",
    )
    crest_origin_shift = method_block(crest_depth_cache, "public void OnOriginShift")
    crest_reset_shift = method_block(crest_depth_cache, "private void ResetCrestSimulationForOriginShift")
    assert_contains_all(
        crest_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!IsFiniteVector3(shiftOffset) ||",
            "!IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.0001f)",
            "_hasConfiguredBounds = false;",
            "_debugCacheReady = false;",
            "ResetCrestSimulationForOriginShift(shiftOffset);",
            "QueueDepthCacheVisualSync(forcePopulate: true);",
        ],
        "HectonCrestOceanDepthCacheBootstrap.OnOriginShift finite cache reset",
    )
    assert_before(
        crest_origin_shift,
        "!IsFiniteVector3(shiftOffset) ||",
        "_hasConfiguredBounds = false;",
        "HectonCrestOceanDepthCacheBootstrap.OnOriginShift rejects nonfinite shift before depth-cache invalidation",
    )
    assert_contains_all(
        crest_reset_shift,
        [
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!IsFiniteVector3(shiftOffset) ||",
            "!IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.0001f)",
            "oceanRenderer._lodTransform?.SetOrigin(shiftOffset);",
            "shiftingOrigin.SetOrigin(shiftOffset);",
            "oceanRenderer.ClearLodData();",
        ],
        "HectonCrestOceanDepthCacheBootstrap.ResetCrestSimulationForOriginShift finite Crest rebase",
    )
    assert_before(
        crest_reset_shift,
        "!IsFiniteVector3(shiftOffset) ||",
        "oceanRenderer._lodTransform?.SetOrigin(shiftOffset);",
        "HectonCrestOceanDepthCacheBootstrap.ResetCrestSimulationForOriginShift rejects nonfinite shift before Crest origin mutation",
    )
    observer_body_origin_shift = method_block(
        read_project_source("Assets", "_Project", "Scripts", "ObserverRelativeCelestialBody.cs"),
        "public void OnOriginShift",
    )
    assert_contains_all(
        observer_body_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!MathGuard.IsFinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= DirectionEpsilon)",
            "QueuePlacementVisualSync();",
        ],
        "ObserverRelativeCelestialBody.OnOriginShift finite sky placement sync",
    )
    assert_before(
        observer_body_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "QueuePlacementVisualSync();",
        "ObserverRelativeCelestialBody.OnOriginShift rejects nonfinite shift before visual sync",
    )
    for fauna_path_parts, fauna_label, buffer_mutation_fragment in [
        (
            ("Assets", "_Project", "Scripts", "Fauna", "ProceduralCrabLegIKRuntime.cs"),
            "ProceduralCrabLegIKRuntime",
            "entity.RootPosition -= offset;",
        ),
        (
            ("Assets", "_Project", "Scripts", "Fauna", "LeviathanTentacleVerletSolver.cs"),
            "LeviathanTentacleVerletSolver",
            "buffers.Positions[i] = SanitizeFiniteInputFloat3(buffers.Positions[i] - offset, float3.zero);",
        ),
    ]:
        fauna_source = read_project_source(*fauna_path_parts)
        fauna_origin_shift = method_block(fauna_source, "public void OnOriginShift")
        fauna_queue_rebase = method_block(fauna_source, "private void QueueOriginShiftRebase")
        fauna_apply_pending = method_block(fauna_source, "private bool ApplyPendingOriginShiftRebase")
        fauna_apply_rebase = method_block(fauna_source, "private void ApplyOriginShiftRebase")
        fauna_finite_helper = method_block(fauna_source, "private static bool IsFiniteOriginShiftOffset")
        fauna_usable_helper = method_block(fauna_source, "private static bool IsUsableOriginShiftOffset")
        assert_contains_all(
            fauna_origin_shift,
            [
                "Vector3 shiftOffset = shiftData.ShiftOffset;",
                "float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
                "if (!IsFiniteOriginShiftOffset(offset))",
                "DumpTelemetryBlackBoxOnce();",
                "if (!IsUsableOriginShiftOffset(offset) || !HasPersistentBuffers())",
            ],
            f"{fauna_label}.OnOriginShift finite animation rebase ingress",
        )
        assert_before(
            fauna_origin_shift,
            "if (!IsFiniteOriginShiftOffset(offset))",
            "QueueOriginShiftRebase(offset);",
            f"{fauna_label}.OnOriginShift rejects nonfinite shift before deferred rebase queue",
        )
        assert_before(
            fauna_origin_shift,
            "if (!IsFiniteOriginShiftOffset(offset))",
            "ApplyOriginShiftRebase(offset);",
            f"{fauna_label}.OnOriginShift rejects nonfinite shift before immediate rebase",
        )
        assert_contains_all(
            fauna_queue_rebase,
            [
                "if (!IsFiniteOriginShiftOffset(offset))",
                "if (!IsUsableOriginShiftOffset(offset))",
                "_pendingOriginShiftOffset += offset;",
                "if (!IsFiniteOriginShiftOffset(_pendingOriginShiftOffset))",
                "_pendingOriginShiftOffset = float3.zero;",
                "_pendingOriginShiftRebase = false;",
            ],
            f"{fauna_label}.QueueOriginShiftRebase finite pending queue",
        )
        assert_before(
            fauna_queue_rebase,
            "if (!IsFiniteOriginShiftOffset(offset))",
            "_pendingOriginShiftOffset += offset;",
            f"{fauna_label}.QueueOriginShiftRebase rejects nonfinite shift before pending accumulation",
        )
        assert_contains_all(
            fauna_apply_pending,
            [
                "float3 offset = _pendingOriginShiftOffset;",
                "_pendingOriginShiftOffset = float3.zero;",
                "_pendingOriginShiftRebase = false;",
                "if (!IsFiniteOriginShiftOffset(offset))",
                "if (!IsUsableOriginShiftOffset(offset))",
                "ApplyOriginShiftRebase(offset);",
            ],
            f"{fauna_label}.ApplyPendingOriginShiftRebase finite pending drain",
        )
        assert_contains_all(
            fauna_apply_rebase,
            [
                "if (!IsFiniteOriginShiftOffset(offset))",
                "if (!IsUsableOriginShiftOffset(offset))",
                buffer_mutation_fragment,
            ],
            f"{fauna_label}.ApplyOriginShiftRebase finite buffer mutation",
        )
        assert_before(
            fauna_apply_rebase,
            "if (!IsFiniteOriginShiftOffset(offset))",
            buffer_mutation_fragment,
            f"{fauna_label}.ApplyOriginShiftRebase rejects nonfinite shift before persistent buffer mutation",
        )
        assert_contains_all(
            fauna_finite_helper,
            [
                "float offsetLengthSq = math.lengthsq(offset);",
                "math.all(math.isfinite(offset)) && math.isfinite(offsetLengthSq);",
            ],
            f"{fauna_label}.IsFiniteOriginShiftOffset helper",
        )
        assert_contains_all(
            fauna_usable_helper,
            [
                "return IsFiniteOriginShiftOffset(offset) && math.lengthsq(offset) > 0.000001f;",
            ],
            f"{fauna_label}.IsUsableOriginShiftOffset helper",
        )
    fauna_kinematics_source = read_project_source("Assets", "_Project", "Scripts", "Fauna", "FaunaKinematicsRuntime.cs")
    fauna_kinematics_origin_shift = method_block(fauna_kinematics_source, "public void OnOriginShift")
    fauna_kinematics_queue_rebase = method_block(fauna_kinematics_source, "private void QueueOriginShiftRebase")
    fauna_kinematics_apply_pending = method_block(fauna_kinematics_source, "private bool ApplyPendingOriginShiftRebase")
    fauna_kinematics_apply_rebase = method_block(fauna_kinematics_source, "private void ApplyOriginShiftRebase")
    fauna_kinematics_finite_helper = method_block(fauna_kinematics_source, "private static bool IsFiniteOriginShiftOffset")
    fauna_kinematics_usable_helper = method_block(fauna_kinematics_source, "private static bool IsUsableOriginShiftOffset")
    assert_contains_all(
        fauna_kinematics_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
            "if (!IsFiniteOriginShiftOffset(offset))",
            "DumpTelemetryBlackBoxOnce();",
            "if (!IsUsableOriginShiftOffset(offset))",
        ],
        "FaunaKinematicsRuntime.OnOriginShift finite animation rebase ingress",
    )
    assert_before(
        fauna_kinematics_origin_shift,
        "if (!IsFiniteOriginShiftOffset(offset))",
        "QueueOriginShiftRebase(offset);",
        "FaunaKinematicsRuntime.OnOriginShift rejects nonfinite shift before deferred rebase queue",
    )
    assert_before(
        fauna_kinematics_origin_shift,
        "if (!IsFiniteOriginShiftOffset(offset))",
        "ApplyOriginShiftRebase(offset);",
        "FaunaKinematicsRuntime.OnOriginShift rejects nonfinite shift before immediate rebase",
    )
    assert_contains_all(
        fauna_kinematics_queue_rebase,
        [
            "if (!IsFiniteOriginShiftOffset(offset))",
            "if (!IsUsableOriginShiftOffset(offset))",
            "_pendingOriginShiftOffset += offset;",
            "if (!IsFiniteOriginShiftOffset(_pendingOriginShiftOffset))",
            "_pendingOriginShiftOffset = float3.zero;",
            "_pendingOriginShiftRebase = false;",
        ],
        "FaunaKinematicsRuntime.QueueOriginShiftRebase finite pending queue",
    )
    assert_before(
        fauna_kinematics_queue_rebase,
        "if (!IsFiniteOriginShiftOffset(offset))",
        "_pendingOriginShiftOffset += offset;",
        "FaunaKinematicsRuntime.QueueOriginShiftRebase rejects nonfinite shift before pending accumulation",
    )
    assert_contains_all(
        fauna_kinematics_apply_pending,
        [
            "float3 offset = _pendingOriginShiftOffset;",
            "_pendingOriginShiftOffset = float3.zero;",
            "_pendingOriginShiftRebase = false;",
            "if (!IsFiniteOriginShiftOffset(offset))",
            "if (!IsUsableOriginShiftOffset(offset))",
            "ApplyOriginShiftRebase(offset);",
        ],
        "FaunaKinematicsRuntime.ApplyPendingOriginShiftRebase finite pending drain",
    )
    assert_contains_all(
        fauna_kinematics_apply_rebase,
        [
            "if (!IsFiniteOriginShiftOffset(offset))",
            "if (!IsUsableOriginShiftOffset(offset))",
            "segmentPositions[i] = SanitizeFiniteInputFloat3(segmentPositions[i] - offset, float3.zero);",
        ],
        "FaunaKinematicsRuntime.ApplyOriginShiftRebase finite buffer mutation",
    )
    assert_before(
        fauna_kinematics_apply_rebase,
        "if (!IsFiniteOriginShiftOffset(offset))",
        "segmentPositions[i] = SanitizeFiniteInputFloat3(segmentPositions[i] - offset, float3.zero);",
        "FaunaKinematicsRuntime.ApplyOriginShiftRebase rejects nonfinite shift before persistent buffer mutation",
    )
    assert_contains_all(
        fauna_kinematics_finite_helper,
        [
            "float offsetLengthSq = math.lengthsq(offset);",
            "math.all(math.isfinite(offset)) && math.isfinite(offsetLengthSq);",
        ],
        "FaunaKinematicsRuntime.IsFiniteOriginShiftOffset helper",
    )
    assert_contains_all(
        fauna_kinematics_usable_helper,
        [
            "return IsFiniteOriginShiftOffset(offset) && math.lengthsq(offset) > OriginShiftUsableMagnitudeSq;",
        ],
        "FaunaKinematicsRuntime.IsUsableOriginShiftOffset helper",
    )
    assert_before(
        drag_register,
        "if (Application.isPlaying && !_serviceRegistered)",
        "TryRegisterSaveOwner();",
        "SargassumGlobalDragManager.TryRegister",
    )
    assert_contains_all(
        drag_register_save,
        [
            "!_serviceRegistered",
            "!ReferenceEquals(s_activeRuntimeInstance, this)",
            "saveService.Register(this);",
            "_saveRegistered = true;",
        ],
        "SargassumGlobalDragManager.TryRegisterSaveOwner",
    )
    assert_before(
        drag_register_save,
        "!_serviceRegistered",
        "saveService.Register(this);",
        "SargassumGlobalDragManager.TryRegisterSaveOwner",
    )
    assert_before(
        drag_register_save,
        "!ReferenceEquals(s_activeRuntimeInstance, this)",
        "saveService.Register(this);",
        "SargassumGlobalDragManager.TryRegisterSaveOwner",
    )
    assert_contains_all(
        drag_unregister_service,
        [
            "if (ReferenceEquals(GlobalRegistry.SargassumDrag, this))",
            "GlobalRegistry.UnregisterSargassumDragRuntime(this);",
            "_serviceRegistered = false;",
        ],
        "SargassumGlobalDragManager.TryUnregisterService",
    )
    assert_before(
        drag_unregister_service,
        "if (ReferenceEquals(s_activeRuntimeInstance, this))",
        "GlobalRegistry.UnregisterSargassumDragRuntime(this);",
        "SargassumGlobalDragManager.TryUnregisterService",
    )
    assert_before(
        drag_unregister_service,
        "_serviceRegistered = false;",
        "GlobalRegistry.UnregisterSargassumDragRuntime(this);",
        "SargassumGlobalDragManager.TryUnregisterService",
    )
    assert_contains_all(
        drag_replaced,
        [
            "case GlobalRegistryServiceSlot.SargassumDragRuntime:",
            "ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);",
            "Application.isPlaying && isActiveAndEnabled && _serviceRegistered",
        ],
        "SargassumGlobalDragManager.OnGlobalRegistryServiceReplaced",
    )
    assert_contains_all(
        drag_reconcile,
        [
            "currentService is SargassumGlobalDragManager currentRuntime",
            "s_activeRuntimeInstance = currentRuntime;",
            "bool ownsRuntime = ReferenceEquals(currentRuntime, this);",
            "_serviceRegistered = ownsRuntime;",
            "if (_runtimeRoutesRetiredAfterOwnershipLoss)",
            "RestoreRuntimeRoutesAfterOwnershipGain();",
            "RetireRuntimeRoutesAfterOwnershipLoss();",
            "if (ReferenceEquals(previousService, this))",
            "if (ReferenceEquals(s_activeRuntimeInstance, this))",
            "s_activeRuntimeInstance = null;",
        ],
        "SargassumGlobalDragManager.ReconcileRuntimeOwnerFromRegistryReplacement",
    )
    assert_contains_all(
        drag_retire,
        [
            "HectonFloatingOrigin.UnregisterListener(this);",
            "_cutManager = null;",
            "TryUnregisterSaveOwner();",
            "_saveService = null;",
            "GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);",
            "GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);",
            "GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);",
            "_runtimeRoutesRetiredAfterOwnershipLoss = true;",
        ],
        "SargassumGlobalDragManager.RetireRuntimeRoutesAfterOwnershipLoss",
    )
    assert "TryUnregisterHotSwapListener" not in drag_retire
    assert_contains_all(
        drag_restore,
        [
            "if (!Application.isPlaying || !isActiveAndEnabled)",
            "RefreshColdRegistryDependencies();",
            "HectonFloatingOrigin.RegisterListener(this);",
            "TryRegister();",
            "_runtimeRoutesRetiredAfterOwnershipLoss = false;",
        ],
        "SargassumGlobalDragManager.RestoreRuntimeRoutesAfterOwnershipGain",
    )
    assert_contains_all(
        drag,
        [
            "private static bool IsFiniteVector3(Vector3 value)",
            "return math.all(math.isfinite(new float3(value.x, value.y, value.z)));",
            "private static bool IsFiniteMassiveDisplacementSignal(in MassiveDisplacementSignal signal)",
            "IsFiniteVector3(signal.PositionWS)",
            "math.isfinite(signal.RadiusWS)",
            "math.isfinite(signal.Duration)",
            "math.isfinite(signal.ExtremePanicRadiusWS)",
            "signal.RadiusWS > 0.001f",
            "signal.Duration > 0.001f",
            "private static bool IsFiniteEntanglementStrainSignal(in EntanglementStrainSignal signal)",
            "IsFiniteVector3(signal.AnchorWS)",
            "math.isfinite(signal.Tension01)",
            "math.isfinite(signal.EscapeIntent01)",
            "math.isfinite(signal.Shake01)",
            "signal.Tension01 >= 0f",
            "signal.EscapeIntent01 >= 0f",
            "signal.Shake01 >= 0f",
            "private static bool IsFiniteDisruptionZone(in DisruptionZoneState zone)",
            "zone.Mode == (byte)DisruptionZoneMode.CutCollapse",
            "zone.Mode == (byte)DisruptionZoneMode.MassiveDisplacement",
            "IsFiniteVector3(zone.SampleSpaceCenterWS)",
            "math.isfinite(zone.RadiusWS)",
            "math.isfinite(zone.Age)",
            "zone.RampDuration > 0f",
        ],
        "SargassumGlobalDragManager finite vector guard",
    )
    assert_contains_all(
        drag_raise_strain,
        ["if (!IsFiniteEntanglementStrainSignal(in signal))", "return false;"],
        "SargassumGlobalDragManager.TryRaiseEntanglementStrain",
    )
    assert_before(
        drag_raise_strain,
        "if (!IsFiniteEntanglementStrainSignal(in signal))",
        "if (_listenerCount <= 0)",
        "SargassumGlobalDragManager.TryRaiseEntanglementStrain",
    )
    assert_contains_all(
        drag_raise_massive,
        ["if (!IsFiniteMassiveDisplacementSignal(in signal))", "return false;"],
        "SargassumGlobalDragManager.TryRaiseMassiveDisplacement",
    )
    assert_before(
        drag_raise_massive,
        "if (!IsFiniteMassiveDisplacementSignal(in signal))",
        "if (_listenerCount <= 0)",
        "SargassumGlobalDragManager.TryRaiseMassiveDisplacement",
    )
    assert_contains_all(
        drag_dispatch_strain,
        [
            "try",
            "listener.OnSargassumEntanglementStrain(in signal);",
            "catch (Exception exception)",
            "ReportListenerDispatchException();",
            "LogListenerDispatchException(exception);",
        ],
        "SargassumGlobalDragManager.DispatchEntanglementStrainToListener",
    )
    assert_contains_all(
        drag_dispatch_massive,
        [
            "try",
            "listener.OnSargassumMassiveDisplacement(in signal);",
            "catch (Exception exception)",
            "ReportListenerDispatchException();",
            "LogListenerDispatchException(exception);",
        ],
        "SargassumGlobalDragManager.DispatchMassiveDisplacementToListener",
    )
    assert_contains_all(
        drag_register_massive,
        [
            "!IsFiniteVector3(position)",
            "!math.isfinite(radius)",
            "!math.isfinite(duration)",
            "return;",
            "if (!math.isfinite(clampedRadius)",
            "!math.isfinite(clampedDuration)",
            "!math.isfinite(extremePanicRadius)",
            "RegisterOrReinforceDisruptionZone(",
            "cutManager.RegisterExternalCut(position, clampedRadius, massiveDisplacementCutStrength, Vector3.up, 1.15f);",
            "TryRaiseMassiveDisplacement(new MassiveDisplacementSignal",
        ],
        "SargassumGlobalDragManager.RegisterMassiveDisplacement",
    )
    assert_before(
        drag_register_massive,
        "!IsFiniteVector3(position)",
        "RegisterOrReinforceDisruptionZone(",
        "SargassumGlobalDragManager.RegisterMassiveDisplacement",
    )
    assert_contains_all(
        drag_sample_detailed,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radius)",
            "!IsFiniteVector3(movementVelocityWS)",
            "!math.isfinite(currentSpeed)",
            "return false;",
            "sample.AnchorWS = positionWS;",
        ],
        "SargassumGlobalDragManager.SampleDetailedInfluence",
    )
    assert_before(
        drag_sample_detailed,
        "return false;",
        "sample.AnchorWS = positionWS;",
        "SargassumGlobalDragManager.SampleDetailedInfluence",
    )
    assert_contains_all(
        drag_update_disruption,
        [
            "float deltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;",
            "!IsFiniteDisruptionZone(in zone)",
            "_disruptionZones[index] = _disruptionZones[lastIndex];",
            "_disruptionZones[lastIndex] = default;",
            "_activeDisruptionZoneCount = lastIndex;",
            "changed = true;",
        ],
        "SargassumGlobalDragManager.UpdateDisruptionZones",
    )
    assert_contains_all(
        drag_update_scavengers,
        ["float safeDeltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;"],
        "SargassumGlobalDragManager.UpdateScavengerHosts",
    )
    assert_contains_all(
        drag_update_nested,
        ["float safeDeltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;"],
        "SargassumGlobalDragManager.UpdateNestedAttachmentBatches",
    )
    assert_before(
        drag_update_disruption,
        "!IsFiniteDisruptionZone(in zone)",
        "float previousStrength01 = EvaluateDisruptionZone01(zone);",
        "SargassumGlobalDragManager.UpdateDisruptionZones",
    )
    assert_contains_all(
        drag_register_disruption,
        [
            "!IsFiniteVector3(sampleSpaceCenterWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength01)",
            "!math.isfinite(sinkDepthWS)",
            "!math.isfinite(rampDuration)",
            "!math.isfinite(holdDuration)",
            "!math.isfinite(fadeDuration)",
            "return -1;",
            "if (!math.isfinite(clampedRadius)",
            "!math.isfinite(clampedStrength)",
            "!math.isfinite(clampedRamp)",
            "!math.isfinite(clampedHold)",
            "!math.isfinite(clampedFade)",
        ],
        "SargassumGlobalDragManager.RegisterOrReinforceDisruptionZone",
    )
    assert_contains_all(
        drag_sample_disruption,
        [
            "!IsFiniteVector3(sampledPositionWS)",
            "!IsFiniteDisruptionZone(in zone)",
            "continue;",
            "float zone01 = EvaluateDisruptionZone01(zone);",
        ],
        "SargassumGlobalDragManager.SampleDisruptionNoDrift",
    )
    assert_before(
        drag_sample_disruption,
        "!IsFiniteDisruptionZone(in zone)",
        "float zone01 = EvaluateDisruptionZone01(zone);",
        "SargassumGlobalDragManager.SampleDisruptionNoDrift",
    )
    assert_contains_all(
        drag_resolve_max_sink,
        [
            "DisruptionZoneState zone = _disruptionZones[i];",
            "!IsFiniteDisruptionZone(in zone)",
            "continue;",
            "if (zone.SinkDepthWS > maxSinkDepthWS)",
            "maxSinkDepthWS = zone.SinkDepthWS;",
        ],
        "SargassumGlobalDragManager.ResolveMaximumSinkDepthWS",
    )
    assert_contains_all(
        drag_register_scavenger,
        [
            "Vector3 anchorWS = chunk.GetScavengerAnchorWS();",
            "!IsFiniteVector3(anchorWS)",
            "if (_scavengerHosts[i].Chunk == chunk)",
            "return true;",
            "if (_activeScavengerHostCount >= _scavengerHosts.Length)",
            "return false;",
            "AnchorWS = anchorWS,",
            "_activeScavengerHostCount++;",
        ],
        "SargassumGlobalDragManager.RegisterSettledCollapseChunk",
    )
    assert_contains_all(
        drag_unregister_scavenger,
        [
            "int lastIndex = _activeScavengerHostCount - 1;",
            "if (i < lastIndex)",
            "_scavengerHosts[i] = _scavengerHosts[lastIndex];",
            "_scavengerHosts[lastIndex] = default;",
            "_activeScavengerHostCount = lastIndex;",
        ],
        "SargassumGlobalDragManager.UnregisterSettledCollapseChunk",
    )
    drag_register_external_scavenger = method_block(drag, "internal void RegisterExternalScavengerSite")
    assert_contains_all(
        drag_register_external_scavenger,
        [
            "!IsFiniteVector3(anchorWS)",
            "!math.isfinite(radiusWS)",
            "if (!math.isfinite(clampedRadius) || !math.isfinite(clampedDuration))",
            "_externalScavengerSites[targetIndex] = new ExternalScavengerSiteState",
        ],
        "SargassumGlobalDragManager.RegisterExternalScavengerSite",
    )

    cut = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumCutManager.cs")
    assert "public static SargassumCutManager Instance => s_activeRuntimeInstance;" in cut
    assert "public static SargassumCutManager Instance => GlobalRegistry.SargassumCut;" not in cut
    cut_on_enable = method_block(cut, "private void OnEnable")
    cut_on_disable = method_block(cut, "private void OnDisable")
    cut_release_resources = method_block(cut, "private void ReleaseResources")
    cut_tick = method_block(cut, "public void Tick(float deltaTime)")
    cut_late_tick = method_block(cut, "public void LateFrameTick")
    cut_register = method_block(cut, "private void TryRegister()")
    cut_unregister_service = method_block(cut, "private void TryUnregisterService")
    cut_replaced = method_block(cut, "public void OnGlobalRegistryServiceReplaced")
    cut_reconcile = method_block(cut, "private void ReconcileRuntimeOwnerFromRegistryReplacement")
    cut_retire = method_block(cut, "private void RetireRuntimeRoutesAfterOwnershipLoss")
    cut_restore = method_block(cut, "private void RestoreRuntimeRoutesAfterOwnershipGain")
    cut_sample_recent = method_block(cut, "public bool SampleRecentCut01")
    cut_sample_area = method_block(cut, "public bool SampleRecentCutArea")
    cut_register_external = method_block(cut, "public bool RegisterExternalCut")
    cut_decay_recent = method_block(cut, "private void DecayRecentCutStamps")
    cut_register_recent = method_block(cut, "private void RegisterRecentCutStamp")
    cut_register_heat = method_block(cut, "private void RegisterRecentCutHeatStamp")
    cut_execute_stamp = method_block(cut, "private void ExecuteStampPass")
    cut_coalesce_stamp = method_block(cut, "private bool TryCoalesceOverflowStamp")
    cut_queue_debris = method_block(cut, "private void QueueDebrisBurst")
    cut_coalesce_debris = method_block(cut, "private bool TryCoalesceOverflowDebrisBurst")
    cut_flush_debris = method_block(cut, "private void FlushDebrisBursts")
    cut_report_debris = method_block(cut, "private void ReportDebrisBurstOverflow")
    cut_refresh_mask_rect = method_block(cut, "private void RefreshMaskWorldRect")
    cut_inside_mask_rect = method_block(cut, "private bool IsInsideMaskWorldRect")
    cut_queue_damage_sync = method_block(cut, "private void QueueDamageVolumeVisualSync")
    cut_refresh_damage_bounds = method_block(cut, "private void RefreshDamageVolumeBounds")
    cut_reset_transient = method_block(cut, "private void ResetTransientRuntimeQueues")
    cut_queue_damage_volume = method_block(cut, "private void QueueDamageVolumeStamp")
    cut_coalesce_damage_volume = method_block(cut, "private bool TryCoalesceOverflowDamageVolumeStamp")
    cut_process_damage_volume = method_block(cut, "private void ProcessQueuedDamageVolumeUpdate")
    assert_contains_all(
        cut_on_enable,
        ["TryRegisterService();", "if (!_serviceRegistered)", "TryRegister();"],
        "SargassumCutManager.OnEnable",
    )
    assert_contains_all(
        cut_on_disable,
        [
            "TryUnregisterService();",
            "TryUnregister();",
            "TryUnregisterHotSwapListener();",
            "ResetTransientRuntimeQueues();",
            "Shader.SetGlobalFloat(_CutMaskActiveId, 0f);",
            "Shader.SetGlobalFloat(_DamageVolumeActiveId, 0f);",
            "PublishRecentCutHeatCount(0);",
        ],
        "SargassumCutManager.OnDisable",
    )
    assert_contains_all(
        cut_release_resources,
        [
            "ResetTransientRuntimeQueues();",
            "ReleaseMaskTexture(ref _maskRead);",
            "ReleaseGraphicsBuffer(ref _stampCommandBufferA);",
            "ReleaseVaultBuffer(ref _queuedStampCommandsHandle);",
            "ReleaseDamageVolumeTexture(ref _damageVolumeRead);",
        ],
        "SargassumCutManager.ReleaseResources",
    )
    assert_contains_all(
        cut_tick,
        [
            "deltaTime = math.isfinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;",
            "_knifeStampCooldownRemaining = math.isfinite(knifeStampCooldown) ? Mathf.Max(0f, knifeStampCooldown) : 0f;",
            "float recoveryRate = math.isfinite(recoveryPerSecond) ? Mathf.Max(0f, recoveryPerSecond) : 0f;",
            "float recoveredEnergy = Mathf.Max(0f, _maskEnergy - recoveryRate * deltaTime);",
        ],
        "SargassumCutManager.Tick",
    )
    assert_contains_all(
        cut_late_tick,
        [
            "float damageVolumeDeltaTime = math.isfinite(_pendingDamageVolumeDeltaTime)",
            "? Mathf.Max(0f, _pendingDamageVolumeDeltaTime)",
            ": 0f;",
            "ProcessQueuedDamageVolumeUpdate(damageVolumeDeltaTime);",
            "_pendingDamageVolumeDeltaTime = 0f;",
        ],
        "SargassumCutManager.LateFrameTick",
    )
    for later in [
        "TryRegisterHotSwapListener();",
        "CacheGraphicsCapabilitiesCold();",
        "CacheRegistryServicesCold();",
        "CreateResources();",
        "PublishGlobals();",
        "TryRegister();",
    ]:
        assert_before(cut_on_enable, "TryRegisterService();", later, "SargassumCutManager.OnEnable")
    assert_contains_all(
        cut_register,
        ["if (!Application.isPlaying || !_serviceRegistered || GlobalRegistry.Dispatcher == null)"],
        "SargassumCutManager.TryRegister",
    )
    assert_contains_all(
        cut_unregister_service,
        [
            "if (ReferenceEquals(GlobalRegistry.SargassumCut, this))",
            "GlobalRegistry.UnregisterSargassumCutRuntime(this);",
            "_serviceRegistered = false;",
        ],
        "SargassumCutManager.TryUnregisterService",
    )
    assert_before(
        cut_unregister_service,
        "if (ReferenceEquals(s_activeRuntimeInstance, this))",
        "GlobalRegistry.UnregisterSargassumCutRuntime(this);",
        "SargassumCutManager.TryUnregisterService",
    )
    assert_before(
        cut_unregister_service,
        "_serviceRegistered = false;",
        "GlobalRegistry.UnregisterSargassumCutRuntime(this);",
        "SargassumCutManager.TryUnregisterService",
    )
    assert_contains_all(
        cut_replaced,
        [
            "serviceSlot == GlobalRegistryServiceSlot.SargassumCutRuntime",
            "ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);",
        ],
        "SargassumCutManager.OnGlobalRegistryServiceReplaced",
    )
    assert_contains_all(
        cut_reconcile,
        [
            "currentService is SargassumCutManager currentRuntime",
            "s_activeRuntimeInstance = currentRuntime;",
            "bool ownsRuntime = ReferenceEquals(currentRuntime, this);",
            "_serviceRegistered = ownsRuntime;",
            "if (_runtimeRoutesRetiredAfterOwnershipLoss)",
            "RestoreRuntimeRoutesAfterOwnershipGain();",
            "RetireRuntimeRoutesAfterOwnershipLoss();",
            "if (ReferenceEquals(previousService, this))",
            "if (ReferenceEquals(s_activeRuntimeInstance, this))",
            "s_activeRuntimeInstance = null;",
        ],
        "SargassumCutManager.ReconcileRuntimeOwnerFromRegistryReplacement",
    )
    assert_contains_all(
        cut_retire,
        [
            "if (_runtimeRoutesRetiredAfterOwnershipLoss)",
            "ResetTransientRuntimeQueues();",
            "TryUnregister();",
            "_runtimeRoutesRetiredAfterOwnershipLoss = true;",
        ],
        "SargassumCutManager.RetireRuntimeRoutesAfterOwnershipLoss",
    )
    assert "TryUnregisterHotSwapListener" not in cut_retire
    assert_contains_all(
        cut_restore,
        [
            "if (!Application.isPlaying || !isActiveAndEnabled)",
            "CacheRegistryServicesCold();",
            "TryRegister();",
            "_runtimeRoutesRetiredAfterOwnershipLoss = false;",
        ],
        "SargassumCutManager.RestoreRuntimeRoutesAfterOwnershipGain",
    )
    assert_contains_all(
        cut,
        [
            "private static bool IsFiniteVector3(Vector3 value)",
            "return math.all(math.isfinite(new float3(value.x, value.y, value.z)));",
            "private const uint DebrisBurstOverflowWarningHash = 0x5343444Fu;",
            "private const uint DebrisBurstContextHash = 0x53434442u;",
            "private int _debrisBurstOverflowCount;",
            "private int _lastDebrisBurstOverflowTelemetryFrame = -1;",
            "private static bool IsFinitePendingDebrisBurst(in PendingDebrisBurst burst)",
            "IsFiniteVector3(burst.PositionWS)",
            "IsFiniteVector3(burst.DirectionWS)",
            "math.isfinite(burst.CutStrength)",
            "math.isfinite(burst.BubbleWeight)",
            "private static bool IsFiniteRecentCutStamp(in RecentCutStamp stamp)",
            "private static bool IsFiniteRecentCutHeatStamp(in RecentCutHeatStamp stamp)",
            "math.isfinite(stamp.RemainingLifetime)",
            "math.isfinite(stamp.StartTime)",
            "math.isfinite(stamp.Lifetime)",
            "private static bool IsFiniteVector4(Vector4 value)",
            "return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));",
            "private static bool IsFiniteDamageVolumeStampCommand(in DamageVolumeStampCommand command)",
            "IsFiniteVector4(command.PositionRadius)",
            "IsFiniteVector4(command.StrengthPadding)",
            "command.PositionRadius.w > 0f",
        ],
        "SargassumCutManager finite guards and debris overflow telemetry",
    )
    for block, label in [
        (cut_sample_recent, "SargassumCutManager.SampleRecentCut01"),
        (cut_sample_area, "SargassumCutManager.SampleRecentCutArea"),
    ]:
        assert_contains_all(
            block,
            [
                "if (!IsFiniteVector3(positionWS) || !math.isfinite(radiusWS))",
                "float lifetime = math.isfinite(recentCutLifetime) ? Mathf.Max(0.01f, recentCutLifetime) : 0.01f;",
                "!IsFiniteRecentCutStamp(in stamp)",
                "_recentCutStamps[i] = default;",
                "float temporalFalloff = Mathf.Clamp01(stamp.RemainingLifetime / lifetime);",
                "return false;",
            ],
            label,
        )
    assert_contains_all(
        cut_decay_recent,
        [
            "if (!math.isfinite(deltaTime) || deltaTime <= 0f)",
            "!IsFiniteRecentCutStamp(in _recentCutStamps[i])",
            "_recentCutStamps[i] = default;",
        ],
        "SargassumCutManager.DecayRecentCutStamps",
    )
    assert_contains_all(
        cut_register_recent,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "float clampedRadius = Mathf.Max(0.05f, radiusWS);",
            "float clampedStrength = Mathf.Clamp01(strength);",
            "float lifetime = math.isfinite(recentCutLifetime) ? Mathf.Max(0.01f, recentCutLifetime) : 0.01f;",
            "!IsFiniteRecentCutStamp(in stamp)",
            "RadiusWS = clampedRadius,",
            "Strength = clampedStrength,",
            "RemainingLifetime = lifetime",
        ],
        "SargassumCutManager.RegisterRecentCutStamp",
    )
    assert_contains_all(
        cut_register_heat,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "if (!math.isfinite(currentTime))",
            "float clampedRadius = Mathf.Max(0.05f, radiusWS);",
            "float clampedStrength = Mathf.Clamp01(strength);",
            "float lifetime = math.isfinite(shaderScarLifetime) ? Mathf.Max(0.01f, shaderScarLifetime) : 0.01f;",
            "!IsFiniteRecentCutHeatStamp(in stamp)",
            "RadiusWS = clampedRadius,",
            "Strength = clampedStrength,",
            "clampedRadius,",
            "clampedStrength,",
            "PlasmaCutThermalDeltaCelsius * clampedStrength",
        ],
        "SargassumCutManager.RegisterRecentCutHeatStamp",
    )
    assert_contains_all(
        cut_execute_stamp,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "!math.isfinite(deltaTime)",
            "float recoveryRate = math.isfinite(recoveryPerSecond) ? Mathf.Max(0f, recoveryPerSecond) : 0f;",
            "float recovery = Mathf.Max(0f, recoveryRate * Mathf.Max(0f, deltaTime));",
            "return;",
        ],
        "SargassumCutManager.ExecuteStampPass",
    )
    assert_contains_all(
        cut_coalesce_stamp,
        [
            "!math.all(math.isfinite(new float2(uvCenter.x, uvCenter.y)))",
            "!math.isfinite(uvRadius)",
            "!math.isfinite(strength)",
            "!IsFiniteVector3(positionWS)",
            "if (!IsFiniteVector4(payload))",
            "payload = new Vector4(uvCenter.x, uvCenter.y, Mathf.Max(0.0001f, uvRadius), Mathf.Clamp01(strength));",
        ],
        "SargassumCutManager.TryCoalesceOverflowStamp",
    )
    assert_contains_all(
        cut_register_external,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "float safeBubbleWeight = math.isfinite(bubbleWeight) ? Mathf.Max(0f, bubbleWeight) : 1f;",
            "QueueDebrisBurst(positionWS, burstDirection, clampedStrength, safeBubbleWeight);",
        ],
        "SargassumCutManager.RegisterExternalCut",
    )
    assert_contains_all(
        cut_queue_debris,
        [
            "!IsFiniteVector3(positionWS)",
            "!IsFiniteVector3(directionWS)",
            "!math.isfinite(cutStrength)",
            "!math.isfinite(bubbleWeight)",
            "_pendingDebrisBurstCount >= _pendingDebrisBursts.Length",
            "TryCoalesceOverflowDebrisBurst(positionWS, directionWS, cutStrength, bubbleWeight);",
            "ReportDebrisBurstOverflow();",
        ],
        "SargassumCutManager.QueueDebrisBurst",
    )
    assert_contains_all(
        cut_coalesce_debris,
        [
            "int activeCount = math.min(_pendingDebrisBurstCount, _pendingDebrisBursts.Length);",
            "bool replacingInvalidSlot = false;",
            "!IsFinitePendingDebrisBurst(in burst)",
            "replacingInvalidSlot = true;",
            "float score = burst.CutStrength * Mathf.Max(0.1f, burst.BubbleWeight);",
            "float incomingScore = cutStrength * Mathf.Max(0.1f, bubbleWeight);",
            "if (!replacingInvalidSlot && incomingScore < weakestScore)",
            "_pendingDebrisBursts[targetIndex] = new PendingDebrisBurst",
        ],
        "SargassumCutManager.TryCoalesceOverflowDebrisBurst",
    )
    assert_contains_all(
        cut_flush_debris,
        [
            "!IsFinitePendingDebrisBurst(in burst)",
            "continue;",
            "debrisParticleSystem.EmitBurst(burst.PositionWS, burst.DirectionWS, burst.CutStrength, burst.BubbleWeight);",
        ],
        "SargassumCutManager.FlushDebrisBursts",
    )
    assert_contains_all(
        cut_report_debris,
        [
            "_debrisBurstOverflowCount++;",
            "int frame = SystemDispatcher.CurrentFrameIndex;",
            "_lastDebrisBurstOverflowTelemetryFrame == frame",
            "GlobalTelemetryBus.PublishPerformanceWarning(",
            "DebrisBurstOverflowWarningHash",
            "DebrisBurstContextHash",
            "Mathf.Max(1, _debrisBurstOverflowCount)",
        ],
        "SargassumCutManager.ReportDebrisBurstOverflow",
    )
    assert_contains_all(
        cut_refresh_damage_bounds,
        [
            "Vector3 desiredWorldMin = new Vector3(minX, minY, minZ);",
            "Vector3 desiredWorldSize = new Vector3(worldSize, damageVolumeHeight, worldSize);",
            "if (!IsFiniteVector3(desiredWorldMin) || !IsFiniteVector3(desiredWorldSize))",
            "return;",
        ],
        "SargassumCutManager.RefreshDamageVolumeBounds",
    )
    assert_contains_all(
        cut_queue_damage_sync,
        [
            "if (!math.isfinite(deltaTime))",
            "deltaTime = 0f;",
            "_pendingDamageVolumeDeltaTime = Mathf.Max(0f, deltaTime);",
        ],
        "SargassumCutManager.QueueDamageVolumeVisualSync",
    )
    assert_contains_all(
        cut_reset_transient,
        [
            "ResetQueuedMaskUpdateState();",
            "_queuedDamageVolumeStampCount = 0;",
            "_damageVolumeStampOverflowCoalesceCount = 0;",
            "_pendingDamageVolumeDeltaTime = 0f;",
            "_damageVolumeEnergy = 0f;",
            "_pendingDebrisBurstCount = 0;",
            "_debrisBurstOverflowCount = 0;",
            "_maskClearRequested = false;",
            "_damageVolumeClearRequested = false;",
            "_globalsDirty = false;",
            "_pendingHeatRefresh = false;",
        ],
        "SargassumCutManager.ResetTransientRuntimeQueues",
    )
    assert_contains_all(
        cut_refresh_mask_rect,
        [
            "if (!math.isfinite(desiredWorldSize))",
            "desiredWorldSize = 128f;",
            "!math.all(math.isfinite(new float2(desiredCenterXZ.x, desiredCenterXZ.y)))",
            "? _maskCenterXZ",
            ": Vector2.zero;",
        ],
        "SargassumCutManager.RefreshMaskWorldRect",
    )
    assert_contains_all(
        cut_inside_mask_rect,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(_maskWorldSize)",
            "!IsFiniteVector4(_maskWorldRect)",
            "_maskWorldSize <= 0f",
            "_maskWorldRect.z <= 0f",
            "_maskWorldRect.w <= 0f",
            "return false;",
        ],
        "SargassumCutManager.IsInsideMaskWorldRect",
    )
    assert_contains_all(
        cut_queue_damage_volume,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "!IsFiniteVector3(_damageVolumeWorldMin)",
            "!IsFiniteVector3(_damageVolumeWorldSize)",
            "float clampedRadius = Mathf.Max(0.05f, radiusWS);",
            "float clampedStrength = Mathf.Clamp01(strength);",
            "PositionRadius = new Vector4(positionWS.x, positionWS.y, positionWS.z, clampedRadius)",
            "StrengthPadding = new Vector4(clampedStrength, 0f, 0f, 0f)",
        ],
        "SargassumCutManager.QueueDamageVolumeStamp",
    )
    assert_contains_all(
        cut_coalesce_damage_volume,
        [
            "!IsFiniteVector3(positionWS)",
            "!math.isfinite(radiusWS)",
            "!math.isfinite(strength)",
            "float clampedRadius = math.max(0.05f, radiusWS);",
            "float clampedStrength = Mathf.Clamp01(strength);",
            "if (!IsFiniteDamageVolumeStampCommand(in existing))",
            "PositionRadius = new Vector4(positionWS.x, positionWS.y, positionWS.z, clampedRadius)",
            "StrengthPadding = new Vector4(clampedStrength, 0f, 0f, 0f)",
            "strengthPadding.x = math.max(strengthPadding.x, clampedStrength);",
        ],
        "SargassumCutManager.TryCoalesceOverflowDamageVolumeStamp",
    )
    assert_contains_all(
        cut_process_damage_volume,
        [
            "deltaTime = math.isfinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;",
            "float damageVolumeRecoveryRate = math.isfinite(damageVolumeRecoveryPerSecond) ? Mathf.Max(0f, damageVolumeRecoveryPerSecond) : 0f;",
            "_DamageVolumeRecoveryId, Mathf.Max(0f, damageVolumeRecoveryRate * Mathf.Max(0f, deltaTime))",
            "_damageVolumeEnergy - Mathf.Max(0f, damageVolumeRecoveryRate * Mathf.Max(0f, deltaTime))",
        ],
        "SargassumCutManager.ProcessQueuedDamageVolumeUpdate",
    )

    runtime_reference_utility = read_project_source("Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs")
    assert_contains_all(
        runtime_reference_utility,
        [
            "public static bool TryResolveSargassumGlobalDragManager(ref SargassumGlobalDragManager target)",
            "public static bool TryResolveSargassumCutManager(ref SargassumCutManager target)",
            "public static bool TryResolveSargassumMicroFaunaBoids(ref SargassumMicroFaunaBoids target)",
            "public static bool TryResolveSargassumDragReadModel(ref ISargassumDragReadModel target)",
            "public static bool TryResolveSargassumCutWriteService(ref ISargassumCutWriteService target)",
            "public static bool TryResolveMicroFaunaPresentationPulseSink(ref IMicroFaunaPresentationPulseSink target)",
            "if (target is Behaviour targetBehaviour && IsLiveBehaviour(targetBehaviour))",
            "ReferenceEquals(targetBehaviour, active)",
            "SargassumGlobalDragManager active = SargassumGlobalDragManager.Instance;",
            "SargassumCutManager active = SargassumCutManager.Instance;",
            "SargassumMicroFaunaBoids active = SargassumMicroFaunaBoids.Instance;",
        ],
        "WorldRuntimeReferenceUtility Sargassum resolvers",
    )

    owner_local_consumer_contracts = {
        ("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref dragManager);",
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);",
        ],
        ("Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref dragManager);",
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);",
        ],
        ("Assets", "_Project", "Scripts", "World", "SargassumDebrisParticleSystem.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);",
        ],
        ("Assets", "_Project", "Scripts", "World", "SargassumCollapseChunk.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);",
        ],
        ("Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);",
        ],
        ("Assets", "_Project", "Scripts", "World", "AbyssalFluidDecalManager.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);",
        ],
        ("Assets", "_Project", "Scripts", "HectonPlayerMovement.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);",
        ],
        ("Assets", "_Project", "Scripts", "HectonFluidEngine.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumDragReadModel(ref _sargassumDragRuntime);",
        ],
        ("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);",
        ],
        ("Assets", "_Project", "Scripts", "Gameplay", "RandomEventSystem.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _cachedSargassumDrag);",
        ],
        ("Assets", "_Project", "Scripts", "Gameplay", "SargassumCutResponder.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cachedCutManager);",
        ],
        ("Assets", "_Project", "Scripts", "LaserCutter.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutWriteService(ref _cachedSargassumCutWriter);",
        ],
    }

    for path_parts, expected_fragments in owner_local_consumer_contracts.items():
        source = read_project_source(*path_parts)
        for fragment in expected_fragments:
            assert fragment in source, f"missing owner-local route {fragment!r} in {Path(*path_parts)}"
        assert "GlobalRegistry.SargassumDrag" not in source, f"stale drag registry route in {Path(*path_parts)}"
        assert "GlobalRegistry.SargassumCut" not in source, f"stale cut registry route in {Path(*path_parts)}"

    crest_damping = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        block = method_block(crest_damping, signature)
        assert "dragManager = null;" in block, f"SargassumCrestDampingController {signature} must clear drag owner"
        assert "cutManager = null;" in block, f"SargassumCrestDampingController {signature} must clear cut owner"
    debris_particles = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumDebrisParticleSystem.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        assert "_sargassumDrag = null;" in method_block(
            debris_particles,
            signature,
        ), f"SargassumDebrisParticleSystem {signature} must clear drag owner"
    fluid_decals = read_project_source("Assets", "_Project", "Scripts", "World", "AbyssalFluidDecalManager.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        assert "_sargassumDrag = null;" in method_block(
            fluid_decals,
            signature,
        ), f"AbyssalFluidDecalManager {signature} must clear drag owner"
    thermal_manager = read_project_source("Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        block = method_block(thermal_manager, signature)
        assert "cutManager = null;" in block, f"AbyssalThermalManager {signature} must clear cut owner"
        assert "TryUnregister();" in block, f"AbyssalThermalManager {signature} must unregister runtime routes"
    thermal_origin_shift = method_block(thermal_manager, "public void OnOriginShift")
    assert_contains_all(
        thermal_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!isActiveAndEnabled ||",
            "!MathGuard.IsFinite(shiftOffset) ||",
            "!math.isfinite(shiftSqrMagnitude) ||",
            "shiftSqrMagnitude <= 0.0001f)",
            "_lastProcessedAupShiftFrameId = shiftData.Sequence;",
            "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
        ],
        "AbyssalThermalManager.OnOriginShift finite thermal/cable producer",
    )
    assert_before(
        thermal_origin_shift,
        "!MathGuard.IsFinite(shiftOffset) ||",
        "ApplyRuntimeOffsetToCachedState(-shiftOffset);",
        "AbyssalThermalManager.OnOriginShift rejects nonfinite shifts before cached thermal state mutation",
    )
    thermal_register_vent = method_block(thermal_manager, "public void RegisterRuntimeVent")
    assert_contains_all(
        thermal_register_vent,
        [
            "if (runtimeKey == 0L || !MathGuard.IsFinite(positionWS))",
            "RadiusWS = ResolvePositiveFinite(radiusWS, 2f, 2f)",
            "UpdraftVelocity = ResolvePositiveFinite(updraftVelocity, 0.5f, 0.5f)",
            "CableRadiusWS = ResolvePositiveFinite(cableRadiusWS, 2f, 2f)",
        ],
        "AbyssalThermalManager.RegisterRuntimeVent finite authoring/runtime bridge",
    )
    thermal_sync_persistent = method_block(thermal_manager, "private void SyncPersistentThermalVents")
    assert_contains_all(
        thermal_sync_persistent,
        [
            "if (record.RuntimeKey == 0L)",
            "if (!MathGuard.IsFinite(positionWS))",
            "RadiusWS = ResolvePositiveFinite(record.RadiusWS, 2f, 2f)",
            "CableRadiusWS = ResolvePositiveFinite(record.CableRadiusWS, 2f, 2f)",
        ],
        "AbyssalThermalManager.SyncPersistentThermalVents finite persistent bridge",
    )
    assert_before(
        thermal_sync_persistent,
        "if (record.RuntimeKey == 0L)",
        "ResolvePersistentThermalVentRuntimePosition(in record.PositionAup)",
        "AbyssalThermalManager.SyncPersistentThermalVents rejects duplicate-empty persistent owner before position resolve",
    )
    thermal_sample = method_block(thermal_manager, "public bool SampleThermalFlow")
    assert_contains_all(
        thermal_sample,
        [
            "if (_activeVentCount <= 0 || !MathGuard.IsFinite(positionWS))",
            "float effectiveRadius = ResolvePositiveFinite(radiusWS, 0.1f, 0.1f);",
            "if (!IsFiniteVent(in vent))",
            "float eruptiveHeatScale = ResolveNonNegativeFinite(ResolveVentHeatScale(i), 1f);",
            "if (!math.isfinite(planarDistance))",
            "float cableWeight = math.saturate(1f - cableDistance / math.max(cableRadius, 0.001f));",
            "strongestCableCut = Resolve01Finite(ResolveCableCutProgress(positionWS, strongestCableAnchor, cableRadius));",
            "SanitizeThermalFlowSample(ref sample, positionWS);",
        ],
        "AbyssalThermalManager.SampleThermalFlow finite thermal/cable sample",
    )
    thermal_try_cable = method_block(thermal_manager, "private bool TryResolveCableZone")
    assert_contains_all(
        thermal_try_cable,
        [
            "if (_activeVentCount <= 0 || !MathGuard.IsFinite(positionWS))",
            "if (!IsFiniteVent(in vent))",
            "if (!math.isfinite(planarDistance) || planarDistance > cableRadius)",
            "cableCutProgress01 = Resolve01Finite(ResolveCableCutProgress(positionWS, cableAnchorWS, cableRadius));",
        ],
        "AbyssalThermalManager.TryResolveCableZone finite cable zone",
    )
    thermal_cable_anchor = method_block(thermal_manager, "private Vector3 ResolveCableAnchor")
    assert_contains_all(
        thermal_cable_anchor,
        [
            "if (!MathGuard.IsFinite(cableAnchorWS))",
            "if (!MathGuard.IsFinite(positionWS))",
            "if (!math.isfinite(planarDeltaSq) || planarDeltaSq <= 0.0001f || ResolveNonNegativeFinite(cableAnchorPull, 0f) <= 0f)",
            "ResolveNonNegativeFinite(cableAnchorPull, 0f)",
        ],
        "AbyssalThermalManager.ResolveCableAnchor finite anchor",
    )
    thermal_cable_cut = method_block(thermal_manager, "private float ResolveCableCutProgress")
    assert_contains_all(
        thermal_cable_cut,
        [
            "if (cutManager == null || !MathGuard.IsFinite(positionWS) || !MathGuard.IsFinite(cableAnchorWS))",
            "ResolvePositiveFinite(cableRadiusWS, 0.1f, 0.1f)",
            "float releaseThreshold = math.max(0.0001f, Resolve01Finite(cableCutReleaseThreshold));",
            "float safeAccumulatedArea = ResolveNonNegativeFinite(accumulatedAreaWS, 0f);",
            "ResolveNonNegativeFinite(strongestCut01, 0f)",
        ],
        "AbyssalThermalManager.ResolveCableCutProgress finite cut bridge",
    )
    for helper in [
        "private static bool IsFiniteVent(in ThermalVentState vent)",
        "private static void SanitizeThermalFlowSample(ref ThermalFlowSample sample, Vector3 fallbackAnchor)",
        "private static float Resolve01Finite(float value)",
        "private static float ResolveNonNegativeFinite(float value, float fallback)",
        "private static float ResolvePositiveFinite(float value, float fallback, float minimum)",
    ]:
        assert helper in thermal_manager, f"AbyssalThermalManager missing finite helper {helper}"
    collapse_chunk = read_project_source("Assets", "_Project", "Scripts", "World", "SargassumCollapseChunk.cs")
    collapse_chunk_replaced = method_block(collapse_chunk, "public void OnGlobalRegistryServiceReplaced")
    assert_contains_all(
        collapse_chunk_replaced,
        [
            "_registeredScavengerHost && previousService is SargassumGlobalDragManager previousDrag",
            "previousDrag.UnregisterSettledCollapseChunk(this);",
            "_registeredScavengerHost = false;",
            "TryRegisterScavengerHost();",
        ],
        "SargassumCollapseChunk.OnGlobalRegistryServiceReplaced",
    )
    for signature in ["public void OnDespawn", "private void OnDisable", "private void OnDestroy"]:
        assert "_sargassumDrag = null;" in method_block(
            collapse_chunk,
            signature,
        ), f"SargassumCollapseChunk {signature} must clear drag owner after lifecycle unregister"
    cut_responder = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "SargassumCutResponder.cs")
    assert "public sealed class SargassumCutResponder : MonoBehaviour, IGlobalRegistryHotSwapListener" in cut_responder
    assert "TryRegisterHotSwapListener();" in method_block(
        cut_responder,
        "private void OnEnable",
    ), "SargassumCutResponder must subscribe for cut-runtime replacement on enable"
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        block = method_block(cut_responder, signature)
        assert "TryUnregisterHotSwapListener();" in block, f"SargassumCutResponder {signature} must unsubscribe hot-swap listener"
        assert "ClearColdDependencies();" in block, f"SargassumCutResponder {signature} must clear cached cut manager"
    cut_responder_disable = method_block(cut_responder, "private void OnDisable")
    assert "_cutRadius = ResolveMinCutRadius(minCutRadius);" in cut_responder_disable, (
        "SargassumCutResponder disable state must sanitize serialized min radius"
    )
    cut_responder_apply_state = method_block(cut_responder, "private void ApplyCutState")
    assert_contains_all(
        cut_responder_apply_state,
        [
            "_debugCutStrength = math.isfinite(_cutStrength) ? math.saturate(_cutStrength) : 0f;",
            "_debugCutRadius = math.isfinite(_cutRadius) ? math.max(0f, _cutRadius) : 0f;",
            "_debugCutPosition = IsFiniteVector3(_cutPositionWS) ? _cutPositionWS : Vector3.zero;",
        ],
        "SargassumCutResponder.ApplyCutState finite diagnostics",
    )
    cut_responder_replaced = method_block(cut_responder, "public void OnGlobalRegistryServiceReplaced")
    assert_contains_all(
        cut_responder_replaced,
        [
            "serviceSlot != GlobalRegistryServiceSlot.SargassumCutRuntime",
            "_cachedCutManager = currentService as SargassumCutManager;",
            "WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cachedCutManager);",
        ],
        "SargassumCutResponder.OnGlobalRegistryServiceReplaced",
    )
    cut_responder_register = method_block(cut_responder, "public void RegisterCut")
    assert_contains_all(
        cut_responder_register,
        [
            "!IsFiniteVector3(positionWS) || !IsFiniteVector3(velocityWS) || !math.isfinite(speed)",
            "float safeMinCutRadius = ResolveMinCutRadius(minCutRadius);",
            "float safeMaxCutRadius = ResolveMaxCutRadius(maxCutRadius, safeMinCutRadius);",
            "_cutRadius = math.lerp(safeMinCutRadius, safeMaxCutRadius, normalizedSpeed);",
            "float rawQuality = HomeostasisBrain.GlobalQualityWeight;",
            "float quality = math.isfinite(rawQuality) ? math.saturate(rawQuality) : 1f;",
            "int safeBaseDebrisCount = math.max(0, baseDebrisCount);",
            "math.lerp(safeBaseDebrisCount, safeBaseDebrisCount * 2.2f, normalizedSpeed)",
        ],
        "SargassumCutResponder.RegisterCut finite bridge",
    )
    assert_before(
        cut_responder_register,
        "!IsFiniteVector3(positionWS) || !IsFiniteVector3(velocityWS) || !math.isfinite(speed)",
        "ResolveNormalizedCutSpeed(speed, cutSpeedThreshold)",
        "SargassumCutResponder.RegisterCut rejects invalid payload before state mutation",
    )
    cut_responder_publish = method_block(cut_responder, "private void PublishCutMask")
    assert_contains_all(
        cut_responder_publish,
        [
            "!IsFiniteVector3(positionWS)",
            "!IsFiniteVector3(velocityWS)",
            "!math.isfinite(_cutRadius)",
            "!math.isfinite(_cutStrength)",
            "float safeMinCutRadius = ResolveMinCutRadius(minCutRadius);",
            "float cutRadiusWS = math.max(safeMinCutRadius, _cutRadius);",
            "float cutStrength01 = math.saturate(_cutStrength);",
            "float safeRecoverySpeed = math.isfinite(cutRecoverySpeed) ? math.max(0.5f, cutRecoverySpeed) : 0.5f;",
            "cutManager.RegisterExternalCut(positionWS, cutRadiusWS, cutStrength01, velocityWS, recoverySeconds);",
        ],
        "SargassumCutResponder.PublishCutMask finite writer boundary",
    )
    assert_before(
        cut_responder_publish,
        "!IsFiniteVector3(positionWS)",
        "cutManager.RegisterExternalCut(positionWS, cutRadiusWS, cutStrength01, velocityWS, recoverySeconds);",
        "SargassumCutResponder.PublishCutMask rejects invalid payload before writer call",
    )
    cut_responder_cooldown = method_block(cut_responder, "private static uint ResolveCooldownFrames")
    assert "math.isfinite(cooldownSeconds) ? math.max(0.01f, cooldownSeconds) : 0.01f" in cut_responder_cooldown, (
        "SargassumCutResponder cooldown resolver must tolerate nonfinite serialized cooldowns"
    )
    cut_responder_speed = method_block(cut_responder, "private static float ResolveNormalizedCutSpeed")
    assert_contains_all(
        cut_responder_speed,
        [
            "if (!math.isfinite(speed))",
            "float safeThreshold = math.isfinite(threshold) ? math.max(0.001f, threshold) : 0.001f;",
        ],
        "SargassumCutResponder.ResolveNormalizedCutSpeed finite threshold",
    )
    assert "private static bool IsFiniteVector3(Vector3 value)" in cut_responder, (
        "SargassumCutResponder must keep a local finite Vector3 boundary helper"
    )
    physics_zone = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "SargassumPhysicsZone.cs")
    physics_configure = method_block(physics_zone, "public void Configure(")
    assert_contains_all(
        physics_configure,
        [
            "speedMultiplier = ResolveSpeedMultiplier(targetSpeedMultiplier);",
            "dragMultiplier = ResolveDragMultiplier(targetDragMultiplier);",
            "cutSpeedThreshold = ResolveCutSpeedThreshold(targetCutSpeedThreshold);",
            "cutRadius = ResolveCutRadius(targetCutRadius);",
        ],
        "SargassumPhysicsZone.Configure finite authoring bridge",
    )
    physics_awake = method_block(physics_zone, "private void Awake")
    assert_contains_all(
        physics_awake,
        [
            "speedMultiplier = ResolveSpeedMultiplier(speedMultiplier);",
            "dragMultiplier = ResolveDragMultiplier(dragMultiplier);",
            "cutSpeedThreshold = ResolveCutSpeedThreshold(cutSpeedThreshold);",
            "cutRadius = ResolveCutRadius(cutRadius);",
        ],
        "SargassumPhysicsZone.Awake finite serialized bridge",
    )
    physics_tick = method_block(physics_zone, "public void Tick")
    assert_contains_all(
        physics_tick,
        [
            "float safeSpeedMultiplier = ResolveSpeedMultiplier(speedMultiplier);",
            "float safeDragMultiplier = ResolveDragMultiplier(dragMultiplier);",
            "speedMultiplier = safeSpeedMultiplier;",
            "dragMultiplier = safeDragMultiplier;",
            "if (playerTransform != null && !IsFiniteVector3(playerTransform.position))",
            "_playerTransform = null;",
            "influence.StayZone(safeSpeedMultiplier, safeDragMultiplier);",
            "EnterPlayerInfluence(influence, safeSpeedMultiplier, safeDragMultiplier);",
        ],
        "SargassumPhysicsZone.Tick finite movement feedback bridge",
    )
    assert_before(
        physics_tick,
        "if (playerTransform != null && !IsFiniteVector3(playerTransform.position))",
        "_cachedVolume.Contains(_cachedTransform, playerTransform.position)",
        "SargassumPhysicsZone.Tick rejects nonfinite player position before volume sample",
    )
    physics_refresh = method_block(physics_zone, "private void RefreshPlayerReferencesCold")
    assert_contains_all(
        physics_refresh,
        [
            "if (_playerTransform != null && !IsFiniteVector3(_playerTransform.position))",
            "_playerTransform = null;",
        ],
        "SargassumPhysicsZone.RefreshPlayerReferencesCold finite player binding",
    )
    physics_cut = method_block(physics_zone, "private void TryRegisterPlayerCut")
    assert_contains_all(
        physics_cut,
        [
            "if (!IsFiniteVector3(velocity))",
            "if (!math.isfinite(speedSq) || speedSq <= 0f)",
            "float safeCutSpeedThreshold = ResolveCutSpeedThreshold(cutSpeedThreshold);",
            "float cutSpeedThresholdSq = safeCutSpeedThreshold * safeCutSpeedThreshold;",
            "if (!math.isfinite(speed) || !IsFiniteVector3(contactPoint))",
            "cutResponder.RegisterCut(contactPoint, velocity, speed);",
        ],
        "SargassumPhysicsZone.TryRegisterPlayerCut finite cut producer",
    )
    assert_before(
        physics_cut,
        "if (!IsFiniteVector3(velocity))",
        "float speedSq = velocity.sqrMagnitude;",
        "SargassumPhysicsZone.TryRegisterPlayerCut rejects invalid velocity before speed math",
    )
    assert_before(
        physics_cut,
        "if (!math.isfinite(speed) || !IsFiniteVector3(contactPoint))",
        "cutResponder.RegisterCut(contactPoint, velocity, speed);",
        "SargassumPhysicsZone.TryRegisterPlayerCut rejects invalid contact before responder call",
    )
    for helper in [
        "private static float ResolveSpeedMultiplier(float value)",
        "private static float ResolveDragMultiplier(float value)",
        "private static float ResolveCutSpeedThreshold(float value)",
        "private static float ResolveCutRadius(float value)",
        "private static bool IsFiniteVector3(Vector3 value)",
    ]:
        assert helper in physics_zone, f"SargassumPhysicsZone missing finite helper {helper}"
    cached_volume = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "CachedTriggerVolume.cs")
    cached_volume_from_collider = method_block(cached_volume, "public static CachedTriggerVolume FromCollider")
    assert_contains_all(
        cached_volume_from_collider,
        [
            "float safeFallback = ResolvePositiveFinite(fallbackRadius, 0.01f);",
            "volume.LocalCenter = ResolveFiniteFloat3(sphere.center, float3.zero);",
            "volume.Radius = ResolvePositiveFinite(sphere.radius, safeFallback);",
            "volume.LocalCenter = ResolveFiniteFloat3(box.center, float3.zero);",
            "float3 safeSize = ResolveFiniteFloat3(box.size, new float3(safeFallback * 2f));",
            "volume.LocalCenter = ResolveFiniteFloat3(capsule.center, float3.zero);",
            "volume.Radius = ResolvePositiveFinite(capsule.radius, safeFallback);",
            "float safeHeight = ResolvePositiveFinite(capsule.height, volume.Radius * 2f);",
        ],
        "CachedTriggerVolume.FromCollider finite collider bridge",
    )
    cached_volume_contains = method_block(cached_volume, "public bool Contains")
    assert_contains_all(
        cached_volume_contains,
        [
            "if (!math.all(math.isfinite(localPoint)) || !IsFiniteVolume())",
            "return false;",
        ],
        "CachedTriggerVolume.Contains finite runtime volume read",
    )
    cached_volume_surface = method_block(cached_volume, "public Vector3 ResolveSurfacePoint")
    assert_contains_all(
        cached_volume_surface,
        [
            "if (!math.all(math.isfinite(localPoint)) || !IsFiniteVolume())",
            "return worldPoint;",
            "Vector3 closestWorld = owner.TransformPoint((Vector3)closestLocal);",
            "return IsFiniteVector3(closestWorld) ? closestWorld : worldPoint;",
        ],
        "CachedTriggerVolume.ResolveSurfacePoint finite contact read",
    )
    for helper in [
        "private bool IsFiniteVolume()",
        "private static float ResolvePositiveFinite(float value, float fallback)",
        "private static float3 ResolveFiniteFloat3(Vector3 value, float3 fallback)",
        "private static bool IsFiniteVector3(Vector3 value)",
    ]:
        assert helper in cached_volume, f"CachedTriggerVolume missing finite helper {helper}"
    movement_influence = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "SargassumMovementInfluence.cs")
    assert "public float SpeedMultiplier => ResolveSpeedMultiplier(_currentSpeedMultiplier);" in movement_influence, (
        "SargassumMovementInfluence SpeedMultiplier read API must fail finite"
    )
    assert "public float DragMultiplier => ResolveDragMultiplier(_currentDragMultiplier);" in movement_influence, (
        "SargassumMovementInfluence DragMultiplier read API must fail finite"
    )
    assert "internal float Entanglement01 => Resolve01(_currentEntanglement01);" in movement_influence, (
        "SargassumMovementInfluence entanglement read API must fail finite"
    )
    movement_apply_field = method_block(movement_influence, "internal void ApplyDetailedFieldInfluence")
    assert_contains_all(
        movement_apply_field,
        [
            "bool hasFiniteAnchor = IsFiniteVector3(entanglementAnchorWS);",
            "_fieldSpeedMultiplier = active ? ResolveSpeedMultiplier(speedMultiplier) : 1f;",
            "_fieldDragMultiplier = active ? ResolveDragMultiplier(dragMultiplier) : 1f;",
            "_fieldDensity01 = active ? Resolve01(density01) : 0f;",
            "_fieldEntanglementAnchorWS = active && hasFiniteAnchor ? entanglementAnchorWS : EntanglementAnchorWS;",
            "_fieldEntanglement01 = active && hasFiniteAnchor ? Resolve01(entanglement01) : 0f;",
        ],
        "SargassumMovementInfluence.ApplyDetailedFieldInfluence finite global field bridge",
    )
    movement_advance = method_block(movement_influence, "public void Advance")
    assert_contains_all(
        movement_advance,
        [
            "NormalizeRuntimeState();",
            "float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;",
            "_exitGraceTimer = ResolveNonNegative(exitGraceTime, 0.1f);",
            "float blendT = FastExpDecayBlend01(blendSpeed, safeDeltaTime);",
            "float entanglementBlendT = FastExpDecayBlend01(entanglementBlendSpeed, safeDeltaTime);",
            "AdvanceCameraTension(safeDeltaTime);",
        ],
        "SargassumMovementInfluence.Advance finite state lifecycle",
    )
    movement_register_influence = method_block(movement_influence, "private void RegisterInfluence")
    assert_contains_all(
        movement_register_influence,
        [
            "NormalizeRuntimeState();",
            "_targetSpeedMultiplier = math.min(_targetSpeedMultiplier, ResolveSpeedMultiplier(speedMultiplier));",
            "_targetDragMultiplier = math.max(_targetDragMultiplier, ResolveDragMultiplier(dragMultiplier));",
            "_exitGraceTimer = ResolveNonNegative(exitGraceTime, 0.1f);",
        ],
        "SargassumMovementInfluence.RegisterInfluence finite trigger bridge",
    )
    movement_origin_shift = method_block(movement_influence, "internal void ApplyOriginShiftOffset")
    assert_contains_all(
        movement_origin_shift,
        [
            "if (!IsFiniteVector3(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)",
            "NormalizeRuntimeState();",
        ],
        "SargassumMovementInfluence.ApplyOriginShiftOffset finite lifecycle shift",
    )
    movement_sync_debug = method_block(movement_influence, "private void SyncDebugState")
    assert_contains_all(
        movement_sync_debug,
        [
            "_debugTargetSpeedMultiplier = ResolveSpeedMultiplier(_targetSpeedMultiplier);",
            "_debugTargetDragMultiplier = ResolveDragMultiplier(_targetDragMultiplier);",
            "_debugCurrentSpeedMultiplier = SpeedMultiplier;",
            "_debugCurrentDragMultiplier = DragMultiplier;",
            "_debugFieldDensity01 = Resolve01(_fieldDensity01);",
            "_debugEntanglementAnchorWS = EntanglementAnchorWS;",
        ],
        "SargassumMovementInfluence.SyncDebugState finite diagnostics",
    )
    movement_camera = method_block(movement_influence, "private void AdvanceCameraTension")
    assert_contains_all(
        movement_camera,
        [
            "float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;",
            "float tension = Resolve01(_currentEntanglement01);",
            "float safeFrequency = ResolveNonNegative(cameraShakeFrequency, 7.5f);",
            "if (!math.isfinite(_entanglementShakeTime))",
            "_entanglementShakeTime = 0f;",
            "float amplitude = ResolveNonNegative(cameraShakeAmplitude, 0f) * tension;",
            "_cameraPitchOffset = sinB * ResolveNonNegative(cameraPitchAmplitude, 0f) * tension;",
            "_cameraRollOffset = cosA * ResolveNonNegative(cameraRollAmplitude, 0f) * tension;",
        ],
        "SargassumMovementInfluence.AdvanceCameraTension finite camera feedback",
    )
    movement_blend = method_block(movement_influence, "private static float FastExpDecayBlend01")
    assert_contains_all(
        movement_blend,
        [
            "float safeBlendSpeed = math.isfinite(blendSpeed) ? math.max(BlendSpeedFloor, blendSpeed) : BlendSpeedFloor;",
            "float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;",
            "float rawX = safeBlendSpeed * safeDeltaTime;",
            "float x = math.isfinite(rawX) ? math.min(rawX, 64f) : 64f;",
        ],
        "SargassumMovementInfluence.FastExpDecayBlend01 finite blend",
    )
    movement_lerp = method_block(movement_influence, "private static Vector3 LerpVector3")
    assert_contains_all(
        movement_lerp,
        [
            "Vector3 safeCurrent = IsFiniteVector3(current) ? current : Vector3.zero;",
            "Vector3 safeTarget = IsFiniteVector3(target) ? target : safeCurrent;",
            "float safeT = Resolve01(t);",
        ],
        "SargassumMovementInfluence.LerpVector3 finite vector blend",
    )
    movement_triangle = method_block(movement_influence, "private static float TriangleWaveSigned")
    assert "if (!math.isfinite(phase))" in movement_triangle, (
        "SargassumMovementInfluence triangle wave must reject nonfinite phase"
    )
    movement_normalize = method_block(movement_influence, "private void NormalizeRuntimeState")
    assert_contains_all(
        movement_normalize,
        [
            "_targetSpeedMultiplier = ResolveSpeedMultiplier(_targetSpeedMultiplier);",
            "_currentDragMultiplier = ResolveDragMultiplier(_currentDragMultiplier);",
            "_fieldEntanglementAnchorWS = IsFiniteVector3(_fieldEntanglementAnchorWS) ? _fieldEntanglementAnchorWS : Vector3.zero;",
            "_cameraLocalOffset = IsFiniteVector3(_cameraLocalOffset) ? _cameraLocalOffset : Vector3.zero;",
            "_exitGraceTimer = ResolveNonNegative(_exitGraceTimer, 0f);",
        ],
        "SargassumMovementInfluence.NormalizeRuntimeState finite state owner",
    )
    for helper in [
        "private static float ResolveSpeedMultiplier(float value)",
        "private static float ResolveDragMultiplier(float value)",
        "private static float Resolve01(float value)",
        "private static float ResolveNonNegative(float value, float fallback)",
        "private static bool IsFiniteVector3(Vector3 value)",
    ]:
        assert helper in movement_influence, f"SargassumMovementInfluence missing finite helper {helper}"
    player_movement_sargassum = read_project_source("Assets", "_Project", "Scripts", "HectonPlayerMovement.cs")
    player_apply_drag = method_block(player_movement_sargassum, "public void ApplyEnvironmentalDrag")
    assert_contains_all(
        player_apply_drag,
        [
            "if (!math.isfinite(dragMultiplier))",
            "float clampedDragMultiplier = math.max(1f, dragMultiplier);",
            "_externalEnvironmentalDragHoldTimer = ResolveSargassumNonNegative(externalEnvironmentalDragHoldTime, 0f);",
        ],
        "HectonPlayerMovement.ApplyEnvironmentalDrag finite environmental drag ingress",
    )
    player_origin_shift = method_block(player_movement_sargassum, "public void OnOriginShift")
    assert "if (!IsFiniteVector(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)" in player_origin_shift, (
        "HectonPlayerMovement origin shift must reject nonfinite lifecycle payloads before mutating cached anchors"
    )
    player_linear_blend = method_block(player_movement_sargassum, "private static float ResolveLinearBlendT")
    assert_contains_all(
        player_linear_blend,
        [
            "float safeSharpness = math.isfinite(sharpness) ? math.max(0f, sharpness) : 0f;",
            "float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;",
            "return math.saturate(safeSharpness * safeDeltaTime);",
        ],
        "HectonPlayerMovement.ResolveLinearBlendT finite shared blend",
    )
    player_resolve_sargassum_speed = method_block(player_movement_sargassum, "private float ResolveSargassumSpeedMultiplier()")
    assert "ResolveSargassumSpeedMultiplier(_sargassumMovementInfluence.SpeedMultiplier)" in player_resolve_sargassum_speed, (
        "HectonPlayerMovement must sanitize Sargassum speed reads"
    )
    player_resolve_sargassum_drag = method_block(player_movement_sargassum, "private float ResolveSargassumDragMultiplier()")
    assert "ResolveSargassumDragMultiplier(_sargassumMovementInfluence.DragMultiplier)" in player_resolve_sargassum_drag, (
        "HectonPlayerMovement must sanitize Sargassum drag reads"
    )
    player_advance_sargassum = method_block(player_movement_sargassum, "private void AdvanceSargassumInfluence")
    assert_contains_all(
        player_advance_sargassum,
        [
            "float rawSampleRadius = _capsuleCollider != null ? _capsuleCollider.radius : 0.5f;",
            "float sampleRadius = math.isfinite(rawSampleRadius) ? math.max(0.35f, rawSampleRadius) : 0.5f;",
            "_sargassumFieldDensity01 = hasFieldInfluence ? ResolveSargassum01(sample.Density01) : 0f;",
        ],
        "HectonPlayerMovement.AdvanceSargassumInfluence finite sample bridge",
    )
    player_propulsion_ref = method_block(player_movement_sargassum, "private float ResolveActiveTransportPropulsionReference")
    assert "math.max(0.01f, ResolveSargassumPositive(transportPreset.PropulsionForceReference, 0.01f))" in player_propulsion_ref, (
        "HectonPlayerMovement transport propulsion reference must keep finite denominator"
    )
    player_sargassum_drag = method_block(player_movement_sargassum, "private void ApplySargassumEnvironmentalDrag")
    assert_contains_all(
        player_sargassum_drag,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "float tension = ResolveSargassum01(_sargassumMovementInfluence.Entanglement01);",
            "float massReference = math.max(1f, ResolveSargassumPositive(sargassumEntanglementMassReference, 80f));",
            "ResolveSargassumNonNegative(sargassumEntanglementSwimEnvironmentalDrag, 0.45f)",
            "ResolveSargassumNonNegative(sargassumEntanglementTransportEnvironmentalDrag, 1.15f)",
            "requestedDragMultiplier = ResolveSargassumDragMultiplier(requestedDragMultiplier);",
            "ApplySargassumEscapeEnergyDrain(safeFixedDeltaTime, tension, propulsion01);",
        ],
        "HectonPlayerMovement.ApplySargassumEnvironmentalDrag finite consumer",
    )
    player_sargassum_energy = method_block(player_movement_sargassum, "private void ApplySargassumEscapeEnergyDrain")
    assert_contains_all(
        player_sargassum_energy,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "float safeTension = ResolveSargassum01(tension);",
            "float safeEnergyDrainPerSecond = ResolveSargassumNonNegative(sargassumEscapeEnergyDrainPerSecond, 0f);",
            "math.max(1f, ResolveSargassumPositive(sargassumEntanglementEscapeEnergyMultiplier, 3f))",
            "math.max(1f, ResolveSargassumPositive(sargassumHighStrainEnergyMultiplier, 3f))",
            "safeFixedDeltaTime",
        ],
        "HectonPlayerMovement.ApplySargassumEscapeEnergyDrain finite survival feedback",
    )
    player_sargassum_high_strain = method_block(player_movement_sargassum, "private void UpdateSargassumHighStrainState")
    assert_contains_all(
        player_sargassum_high_strain,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "_sargassumHighStrainIntensity = ResolveSargassum01(_sargassumHighStrainIntensity);",
            "_sargassumHighStrainTimer = 0f;",
            "ResolveLinearBlendT(12f, safeFixedDeltaTime);",
        ],
        "HectonPlayerMovement.UpdateSargassumHighStrainState finite strain lifecycle",
    )
    player_sargassum_buoyancy_blend = method_block(player_movement_sargassum, "private void UpdateSargassumMatBuoyancyBlend")
    assert_contains_all(
        player_sargassum_buoyancy_blend,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "float densityThreshold = math.isfinite(sargassumMatBuoyancyDensityThreshold)",
            ": 0.8f;",
            "ResolveSargassum01(_sargassumFieldDensity01)",
            "math.max(0.01f, ResolveSargassumPositive(sargassumMatBuoyancyMaxDepth, 1.65f))",
            "math.max(0.01f, ResolveSargassumPositive(sargassumMatBuoyancyBlendSharpness, 9f))",
            "_sargassumMatBuoyancyBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);",
        ],
        "HectonPlayerMovement.UpdateSargassumMatBuoyancyBlend finite buoyancy blend",
    )
    player_sargassum_buoyancy_support = method_block(player_movement_sargassum, "private void ApplySargassumMatBuoyancySupport")
    assert_contains_all(
        player_sargassum_buoyancy_support,
        [
            "float surfaceReleaseVelocity = math.max(0.01f, ResolveSargassumPositive(surfaceBreachReleaseVelocity, 1f));",
            "ResolveSargassumNonNegative(_cachedGravityMagnitude, 0f)",
            "ResolveSargassumNonNegative(sargassumMatBuoyancyForceScale, 0f)",
            "ResolveSargassum01(_sargassumMatBuoyancyBlend)",
        ],
        "HectonPlayerMovement.ApplySargassumMatBuoyancySupport finite force",
    )
    player_surface_lock = method_block(player_movement_sargassum, "private void ApplySurfaceLock")
    assert_contains_all(
        player_surface_lock,
        [
            "float sargassumMatBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);",
            "math.max(1f, ResolveSargassumPositive(sargassumMatSurfaceLockBoost, 1.4f))",
            "ResolveSargassumNonNegative(sargassumMatSurfaceLiftOffset, 0.16f) * sargassumMatBlend",
        ],
        "HectonPlayerMovement.ApplySurfaceLock finite Sargassum mat support",
    )
    player_sargassum_force = method_block(player_movement_sargassum, "private void ApplySargassumEntanglementForce")
    assert_contains_all(
        player_sargassum_force,
        [
            "float tension = ResolveSargassum01(_sargassumMovementInfluence.Entanglement01);",
            "if (!IsFiniteVector(playerPosition) || !IsFiniteVector(anchor))",
            "displacement.y *= ResolveSargassum01(sargassumEntanglementVerticalInfluence);",
            "if (!math.isfinite(displacementSqr) || displacementSqr <= 0.00000001f)",
            "ResolveSargassum01(sargassumEntanglementEscapeRelief)",
            "ResolveSargassumNonNegative(sargassumEntanglementSpring, 0f)",
            "ResolveSargassumNonNegative(sargassumEntanglementDamping, 0f)",
            "if (!math.isfinite(totalAccelerationMagnitude) || totalAccelerationMagnitude <= 0f)",
            "ResolveSargassumNonNegative(sargassumEntanglementMaxAcceleration, 18f)",
            "float strainThreshold = math.isfinite(sargassumEntanglementStrainThreshold)",
            ": 0.22f;",
            "ResolveSargassumNonNegative(sargassumEntanglementCameraShakeScale, 0.9f)",
        ],
        "HectonPlayerMovement.ApplySargassumEntanglementForce finite motor/event bridge",
    )
    player_sargassum_escape_intent = method_block(player_movement_sargassum, "private float ResolveSargassumEscapeIntent01")
    assert_contains_all(
        player_sargassum_escape_intent,
        [
            "float planarInputMagnitude = ResolveSargassum01(ApproximatePlanarMagnitude(_inputH, _inputV));",
            "float verticalInputMagnitude = math.isfinite(_inputVertical) ? math.abs(_inputVertical) : 0f;",
            "float inputIntent = math.max(planarInputMagnitude, ResolveSargassum01(verticalInputMagnitude * 0.75f));",
            "float escapeIntent = math.max(inputIntent, ResolveSargassum01(propulsion01));",
            "float escapeThreshold = math.isfinite(sargassumEscapeInputThreshold)",
            ": 0.2f;",
            "return math.saturate((escapeIntent - escapeThreshold) / math.max(1f - escapeThreshold, 0.0001f));",
        ],
        "HectonPlayerMovement.ResolveSargassumEscapeIntent01 finite intent threshold",
    )
    player_sargassum_strain = method_block(player_movement_sargassum, "private void HandleSargassumEntanglementStrain")
    assert_contains_all(
        player_sargassum_strain,
        [
            "float shakeIntensity = ResolveSargassum01(signal.Shake01);",
            "float highStrainThreshold = math.isfinite(sargassumHighStrainThreshold)",
            ": 0.5f;",
            "_sargassumHighStrainIntensity = math.max(ResolveSargassum01(_sargassumHighStrainIntensity), highStrainT);",
            "_sargassumHighStrainTimer = ResolveSargassumNonNegative(sargassumHighStrainHoldTime, 0.18f);",
            "math.max(1f, ResolveSargassumPositive(sargassumHighStrainShakeBoost, 1.75f))",
            "QueueCameraEntanglementStrain(ResolveSargassum01(shakeIntensity));",
        ],
        "HectonPlayerMovement.HandleSargassumEntanglementStrain finite camera feedback",
    )
    player_sargassum_audio = method_block(player_movement_sargassum, "private void TryPlaySargassumEntanglementAudio")
    assert_contains_all(
        player_sargassum_audio,
        [
            "float shake01 = ResolveSargassum01(signal.Shake01);",
            "float nextAudioTime = math.isfinite(_nextSargassumEntanglementAudioTime) ? _nextSargassumEntanglementAudioTime : 0f;",
            "if (shake01 <= 0.0001f || now < nextAudioTime)",
            "float volume = math.lerp(0.12f, 0.42f, shake01);",
            "float pitch = math.lerp(0.72f, 0.94f, ResolveSargassum01(signal.EscapeIntent01));",
            "_nextSargassumEntanglementAudioTime = now + math.max(0.05f, ResolveSargassumPositive(sargassumEntanglementAudioCooldown, 0.24f));",
        ],
        "HectonPlayerMovement.TryPlaySargassumEntanglementAudio finite audio cooldown",
    )
    player_external_drag = method_block(player_movement_sargassum, "private void AdvanceExternalEnvironmentalDrag")
    assert_contains_all(
        player_external_drag,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "_externalEnvironmentalDragRequestedMultiplier = ResolveSargassumDragMultiplier(_externalEnvironmentalDragRequestedMultiplier);",
            "_externalEnvironmentalDragHoldTimer = ResolveSargassumNonNegative(externalEnvironmentalDragHoldTime, 0f);",
            "_externalEnvironmentalDragHoldTimer -= safeFixedDeltaTime;",
            "else if (!math.isfinite(_externalEnvironmentalDragHoldTimer))",
            "? ResolveSargassumDragMultiplier(_externalEnvironmentalDragRequestedMultiplier)",
            "ResolveLinearBlendT(math.max(0.01f, ResolveSargassumPositive(externalEnvironmentalDragBlendSpeed, 9f)), safeFixedDeltaTime);",
            "ResolveSargassumDragMultiplier(_externalEnvironmentalDragCurrentMultiplier)",
        ],
        "HectonPlayerMovement.AdvanceExternalEnvironmentalDrag finite external drag lifecycle",
    )
    player_external_drag_read = method_block(player_movement_sargassum, "private float ResolveExternalEnvironmentalDragMultiplier")
    assert_contains_all(
        player_external_drag_read,
        [
            "float multiplier = ResolveSargassumDragMultiplier(_externalEnvironmentalDragCurrentMultiplier);",
            "ResolveSargassumDragMultiplier(brineViscosityDragMultiplier)",
        ],
        "HectonPlayerMovement.ResolveExternalEnvironmentalDragMultiplier finite read model",
    )
    player_sargassum_rest = method_block(player_movement_sargassum, "private void ApplySargassumRestRecovery")
    assert_contains_all(
        player_sargassum_rest,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "float fieldDensity01 = ResolveSargassum01(_sargassumFieldDensity01);",
            "float matBuoyancyBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);",
            "float restDensityThreshold = math.isfinite(sargassumRestDensityThreshold)",
            ": 0.9f;",
            "float maxRestSpeed = math.max(0.01f, ResolveSargassumPositive(sargassumRestMaxSpeed, 0.4f));",
            "if (!math.isfinite(speedSq))",
            "float absInputH = math.isfinite(_inputH) ? math.abs(_inputH) : 0f;",
            "float inputCalmT = 1f - math.saturate(inputIntent / math.max(0.01f, ResolveSargassumPositive(sargassumRestMaxInputIntent, 0.18f)));",
            "float headDepth = ResolveSargassumNonNegative(GetHeadDepthBelowSurface(EffectiveWaterSurfaceY), 0f);",
            "ResolveSargassumPositive(sargassumRestMaxHeadDepth, 0.03f)",
            "ResolveSargassumPositive(sargassumRestBlendSharpness, 6f)",
            "_sargassumRestRecoveryBlend = ResolveSargassum01(_sargassumRestRecoveryBlend);",
            "if (_survivalSystem == null)",
            "float oxygenRestorePerSecond = ResolveSargassumNonNegative(sargassumRestOxygenRestorePerSecond, 8f);",
            "float energyRestorePerSecond = ResolveSargassumNonNegative(sargassumRestEnergyRestorePerSecond, 1.35f);",
            "safeFixedDeltaTime",
        ],
        "HectonPlayerMovement.ApplySargassumRestRecovery finite survival recovery",
    )
    player_update_diagnostics = method_block(player_movement_sargassum, "private void UpdateDiagnostics")
    assert_contains_all(
        player_update_diagnostics,
        [
            "_debugSargassumFieldDensity01 = ResolveSargassum01(_sargassumFieldDensity01);",
            "_debugSargassumMatBuoyancy01 = ResolveSargassum01(_sargassumMatBuoyancyBlend);",
        ],
        "HectonPlayerMovement.UpdateDiagnostics finite Sargassum diagnostics",
    )
    for helper in [
        "private static float ResolveSargassumSpeedMultiplier(float value)",
        "private static float ResolveSargassumDragMultiplier(float value)",
        "private static float ResolveSargassum01(float value)",
        "private static float ResolveSargassumNonNegative(float value, float fallback)",
        "private static float ResolveSargassumPositive(float value, float fallback)",
    ]:
        assert helper in player_movement_sargassum, f"HectonPlayerMovement missing finite helper {helper}"
    assert "private float abyssalCableEntanglementMassReference = 110f;" in player_movement_sargassum, (
        "HectonPlayerMovement abyssal cable drag must not borrow sargassum mass tuning"
    )
    player_external_updraft = method_block(player_movement_sargassum, "public void ApplyExternalThermalUpdraft")
    assert_contains_all(
        player_external_updraft,
        [
            "if (!IsFiniteVector(velocityChange) || velocityChange.y <= 0.0001f)",
            "_externalThermalUpdraftVelocityChange = velocityChange;",
            "_externalThermalUpdraftRequestedThisStep = true;",
        ],
        "HectonPlayerMovement.ApplyExternalThermalUpdraft finite public bridge",
    )
    assert_before(
        player_external_updraft,
        "if (!IsFiniteVector(velocityChange) || velocityChange.y <= 0.0001f)",
        "_externalThermalUpdraftVelocityChange = velocityChange;",
        "HectonPlayerMovement.ApplyExternalThermalUpdraft rejects bad caller data before state mutation",
    )
    player_advance_abyssal = method_block(player_movement_sargassum, "private void AdvanceAbyssalThermalInfluence")
    assert_contains_all(
        player_advance_abyssal,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "if (!IsFiniteVector(samplePosition))",
            "ResolveAbyssalCablePositive(_capsuleCollider.radius, 0.5f)",
            "sample = SanitizeAbyssalThermalSample(sample, samplePosition);",
            "ApplyAbyssalCableEnvironmentalDrag(safeFixedDeltaTime, transportPreset, sample);",
            "ApplyExternalThermalUpdraft(sample.FlowVelocityWS * safeFixedDeltaTime);",
        ],
        "HectonPlayerMovement.AdvanceAbyssalThermalInfluence finite player sample bridge",
    )
    player_tow_snare = method_block(player_movement_sargassum, "private void AdvanceHeavyTowCableSnare")
    assert_contains_all(
        player_tow_snare,
        [
            "if (!IsFiniteVector(payloadPositionWS))",
            "float safePayloadRadius = ResolveAbyssalCablePositive(payloadRadiusWS, 0.5f);",
            "payloadSample = SanitizeAbyssalThermalSample(payloadSample, payloadPositionWS);",
            "if (payloadSample.IsCableZone == 0 || payloadSample.CableTension01 <= 0.0001f)",
            "_heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);",
        ],
        "HectonPlayerMovement.AdvanceHeavyTowCableSnare finite tow sample bridge",
    )
    heavy_tow_winch = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "HeavyTowWinch.cs")
    heavy_tow_external_snare = method_block(heavy_tow_winch, "internal void ApplyExternalCableSnare")
    assert_contains_all(
        heavy_tow_external_snare,
        [
            "float safeTension01 = math.isfinite(tension01) ? math.saturate(tension01) : 0f;",
            "float safeCutProgress01 = math.isfinite(cutProgress01) ? math.saturate(cutProgress01) : 1f;",
            "float effectiveTension01 = safeTension01 * (1f - safeCutProgress01);",
            "if (!IsFinite(anchorWS) || effectiveTension01 <= 0.0001f)",
            "_activeTether.QueueExternalCableSnare(Vector3.zero, 0f, 1f);",
            "_activeTether.QueueExternalCableSnare(anchorWS, safeTension01, safeCutProgress01);",
        ],
        "HeavyTowWinch.ApplyExternalCableSnare finite external snare bridge",
    )
    assert_before(
        heavy_tow_external_snare,
        "if (!IsFinite(anchorWS) || effectiveTension01 <= 0.0001f)",
        "_activeTether.QueueExternalCableSnare(anchorWS, safeTension01, safeCutProgress01);",
        "HeavyTowWinch.ApplyExternalCableSnare neutralizes bad snare before queueing payload force",
    )
    tether_instance = read_project_source("Assets", "_Project", "Scripts", "TetherInstance.cs")
    tether_queue_external_snare = method_block(tether_instance, "public void QueueExternalCableSnare")
    assert_contains_all(
        tether_queue_external_snare,
        [
            "float safeTension01 = math.isfinite(tension01) ? math.saturate(tension01) : 0f;",
            "float safeCutProgress01 = math.isfinite(cutProgress01) ? math.saturate(cutProgress01) : 1f;",
            "float effectiveTension01 = safeTension01 * (1f - safeCutProgress01);",
            "if (!IsFinite(anchorWS) || effectiveTension01 <= 0.0001f)",
            "_bioCableRequestedAnchorWS = Vector3.zero;",
            "_bioCableRequestedTension01 = 0f;",
            "_bioCableRequestedCutProgress01 = 1f;",
            "_bioCableCurrentAnchorWS = Vector3.zero;",
            "_bioCableCurrentTension01 = 0f;",
            "_bioCableCurrentCutProgress01 = 1f;",
            "_bioCableHoldTimer = 0f;",
            "_bioCableRequestedAnchorWS = anchorWS;",
            "_bioCableRequestedTension01 = safeTension01;",
            "_bioCableRequestedCutProgress01 = safeCutProgress01;",
            "_bioCableHoldTimer = math.isfinite(_bioCableHoldTime) ? math.max(0f, _bioCableHoldTime) : 0f;",
        ],
        "TetherInstance.QueueExternalCableSnare finite queued state owner",
    )
    assert_before(
        tether_queue_external_snare,
        "if (!IsFinite(anchorWS) || effectiveTension01 <= 0.0001f)",
        "_bioCableRequestedAnchorWS = anchorWS;",
        "TetherInstance.QueueExternalCableSnare rejects bad snare before storing force anchor",
    )
    assert "_bioCableRequestedAnchorWS = IsFinite(anchorWS) ? anchorWS : _bioCableCurrentAnchorWS;" not in tether_queue_external_snare, (
        "TetherInstance cable-snare ingress must not revive stale anchors on bad caller data"
    )
    tether_advance_external_snare = method_block(tether_instance, "private void AdvanceExternalCableSnare")
    assert_contains_all(
        tether_advance_external_snare,
        [
            "float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;",
            "_bioCableHoldTimer = math.isfinite(_bioCableHoldTime) ? math.max(0f, _bioCableHoldTime) : 0f;",
            "bool keepAlive = _bioCableRequestedThisStep || _bioCableHoldTimer > 0f;",
            "Vector3 targetAnchor = keepAlive && IsFinite(_bioCableRequestedAnchorWS) ? _bioCableRequestedAnchorWS : Vector3.zero;",
            "float currentTension = math.isfinite(_bioCableCurrentTension01) ? math.saturate(_bioCableCurrentTension01) : 0f;",
            "Vector3 currentAnchor = IsFinite(_bioCableCurrentAnchorWS) ? _bioCableCurrentAnchorWS : targetAnchor;",
            "_bioCableRequestedAnchorWS = Vector3.zero;",
            "_bioCableRequestedThisStep = false;",
        ],
        "TetherInstance.AdvanceExternalCableSnare finite snare lifecycle",
    )
    assert "_bioCableHoldTimer = _bioCableHoldTime;" not in tether_advance_external_snare, (
        "TetherInstance cable-snare hold timer must keep finite lifecycle state"
    )
    tether_rebase_managed = method_block(tether_instance, "internal void RebaseManagedRuntimeState")
    assert_contains_all(
        tether_rebase_managed,
        [
            "if (!_isActive || !IsFinite(shiftOffset) || shiftOffset.sqrMagnitude <= MinVectorMagnitudeSq)",
            "float requestedEffectiveTension01 = math.saturate(math.isfinite(_bioCableRequestedTension01) ? _bioCableRequestedTension01 : 0f) *",
            "if (_bioCableRequestedThisStep && requestedEffectiveTension01 > 0.0001f && IsFinite(_bioCableRequestedAnchorWS))",
            "_bioCableRequestedAnchorWS -= shiftOffset;",
            "_bioCableRequestedAnchorWS = Vector3.zero;",
            "float currentEffectiveTension01 = math.saturate(math.isfinite(_bioCableCurrentTension01) ? _bioCableCurrentTension01 : 0f) *",
            "if (currentEffectiveTension01 > 0.0001f && IsFinite(_bioCableCurrentAnchorWS))",
            "_bioCableCurrentAnchorWS -= shiftOffset;",
            "_bioCableCurrentAnchorWS = Vector3.zero;",
        ],
        "TetherInstance.RebaseManagedRuntimeState finite cable-snare origin shift",
    )
    assert_before(
        tether_rebase_managed,
        "float requestedEffectiveTension01 = math.saturate(math.isfinite(_bioCableRequestedTension01) ? _bioCableRequestedTension01 : 0f) *",
        "_bioCableRequestedAnchorWS -= shiftOffset;",
        "TetherInstance.RebaseManagedRuntimeState gates requested snare anchor before origin shift",
    )
    assert_before(
        tether_rebase_managed,
        "float currentEffectiveTension01 = math.saturate(math.isfinite(_bioCableCurrentTension01) ? _bioCableCurrentTension01 : 0f) *",
        "_bioCableCurrentAnchorWS -= shiftOffset;",
        "TetherInstance.RebaseManagedRuntimeState gates current snare anchor before origin shift",
    )
    tether_manager = read_project_source("Assets", "_Project", "Scripts", "TetherManager.cs")
    tether_manager_origin_shift = method_block(tether_manager, "public void OnOriginShift")
    assert_contains_all(
        tether_manager_origin_shift,
        [
            "Vector3 shiftOffset = shiftData.ShiftOffset;",
            "float shiftSqrMagnitude = shiftOffset.sqrMagnitude;",
            "if (!math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
            "float3 shiftOffsetF3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
            "instance.RebaseManagedRuntimeState(shiftOffset);",
            "instance.RebaseVerletRuntime(shiftOffsetF3)",
            "instance.RebaseVisualStagingRuntime(shiftOffsetF3)",
        ],
        "TetherManager.OnOriginShift finite dispatcher",
    )
    assert_before(
        tether_manager_origin_shift,
        "if (!math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)",
        "float3 shiftOffsetF3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);",
        "TetherManager.OnOriginShift rejects nonfinite shifts before rebasing tether runtimes",
    )
    player_abyssal_drag = method_block(player_movement_sargassum, "private void ApplyAbyssalCableEnvironmentalDrag")
    assert_contains_all(
        player_abyssal_drag,
        [
            "sample = SanitizeAbyssalThermalSample(sample, ResolvePlayerAupRuntimePosition());",
            "float tension = ResolveAbyssalCable01(sample.CableTension01);",
            "ResolveAbyssalCablePositive(abyssalCableEntanglementMassReference, 110f)",
            "ResolveAbyssalCableNonNegative(abyssalCableEntanglementSwimEnvironmentalDrag, 1.25f)",
            "ResolveAbyssalCableNonNegative(abyssalCableEntanglementTransportEnvironmentalDrag, 2.85f)",
            "ResolveAbyssalCableSuppression01(sample.CableEscapeSuppression01)",
            "ResolveAbyssalCableDragMultiplier(1f + maxExtraDrag * tension * suppression * bodyMassScale * propulsionRelief)",
            "float drainPerSecond = ResolveAbyssalCableNonNegative(abyssalCableEscapeEnergyDrainPerSecond, 6.2f);",
            "math.max(1f, ResolveAbyssalCablePositive(abyssalCableEscapeEnergyMultiplier, 4.5f))",
            "safeFixedDeltaTime",
        ],
        "HectonPlayerMovement.ApplyAbyssalCableEnvironmentalDrag finite drag/drain consumer",
    )
    assert "sargassumEntanglementMassReference" not in player_abyssal_drag, (
        "Abyssal cable drag must use its own mass reference"
    )
    player_abyssal_force = method_block(player_movement_sargassum, "private void ApplyAbyssalCableEntanglementForce")
    assert_contains_all(
        player_abyssal_force,
        [
            "_abyssalThermalFlowSample = SanitizeAbyssalThermalSample(_abyssalThermalFlowSample, ResolvePlayerAupRuntimePosition());",
            "float tension = ResolveAbyssalCable01(_abyssalThermalFlowSample.CableTension01);",
            "if (!IsFiniteVector(playerPosition) || !IsFiniteVector(anchor))",
            "displacement.y *= ResolveAbyssalCable01(abyssalCableEntanglementVerticalInfluence);",
            "if (!math.isfinite(displacementSqr) || displacementSqr <= 0.00000001f)",
            "if (!math.isfinite(velocityAlongSpring))",
            "float reliefAtFullCut = ResolveAbyssalCable01(abyssalCablePropulsionReliefAtFullCut);",
            "ResolveAbyssalCableSuppression01(_abyssalThermalFlowSample.CableEscapeSuppression01)",
            "ResolveAbyssalCableNonNegative(abyssalCableEntanglementSpring, 28f)",
            "ResolveAbyssalCableNonNegative(abyssalCableEntanglementDamping, 9.5f)",
            "if (!math.isfinite(totalAccelerationMagnitude) || totalAccelerationMagnitude <= 0f)",
            "ResolveAbyssalCableNonNegative(abyssalCableEntanglementMaxAcceleration, 26f)",
        ],
        "HectonPlayerMovement.ApplyAbyssalCableEntanglementForce finite force consumer",
    )
    player_abyssal_cut_release = method_block(player_movement_sargassum, "private float ResolveAbyssalCableCutRelease01")
    assert_contains_all(
        player_abyssal_cut_release,
        [
            "float cutProgress = ResolveAbyssalCable01(cableCutProgress01);",
            "float threshold = ResolveAbyssalCable01(abyssalCableCutReleaseThreshold);",
            "math.max(1f - threshold, 0.0001f)",
        ],
        "HectonPlayerMovement.ResolveAbyssalCableCutRelease01 finite cut relief",
    )
    for helper in [
        "private static AbyssalThermalManager.ThermalFlowSample SanitizeAbyssalThermalSample",
        "private static float ResolveAbyssalCableDragMultiplier(float value)",
        "private static float ResolveAbyssalCable01(float value)",
        "private static float ResolveAbyssalCableSuppression01(float value)",
        "private static float ResolveAbyssalCableNonNegative(float value, float fallback)",
        "private static float ResolveAbyssalCablePositive(float value, float fallback)",
    ]:
        assert helper in player_movement_sargassum, f"HectonPlayerMovement missing Abyssal finite helper {helper}"
    random_events = read_project_source("Assets", "_Project", "Scripts", "Gameplay", "RandomEventSystem.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        assert "_cachedSargassumDrag = null;" in method_block(
            random_events,
            signature,
        ), f"RandomEventSystem {signature} must clear Sargassum drag owner"
    underwater_visuals = read_project_source("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs")
    for signature in ["private void OnDisable", "private void OnDestroy"]:
        assert "_sargassumDragRuntime = null;" in method_block(
            underwater_visuals,
            signature,
        ), f"HectonUnderwaterVisuals {signature} must clear Sargassum drag owner"
    player_movement = read_project_source("Assets", "_Project", "Scripts", "HectonPlayerMovement.cs")
    assert "_sargassumDragRuntime = null;" in method_block(
        player_movement,
        "private void ClearInjectedDependencies",
    ), "HectonPlayerMovement must clear Sargassum drag owner with injected dependencies"
    fluid_engine = read_project_source("Assets", "_Project", "Scripts", "HectonFluidEngine.cs")
    assert "_sargassumDragRuntime = null;" in method_block(
        fluid_engine,
        "private void ClearCachedFluidRuntimeServices",
    ), "HectonFluidEngine must clear Sargassum drag read model with cached runtime services"
    laser_cutter = read_project_source("Assets", "_Project", "Scripts", "LaserCutter.cs")
    assert "_cachedSargassumCutWriter = null;" in method_block(
        laser_cutter,
        "private void ClearColdDependencies",
    ), "LaserCutter must clear Sargassum cut writer with cold dependencies"

    micro_fauna_consumer_contracts = {
        ("Assets", "_Project", "Scripts", "HectonDirectorAI.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref boidSystem);",
            "WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _sargassumMicroFauna);",
        ],
        ("Assets", "_Project", "Scripts", "PlayerPDA.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _microFaunaBoids);",
        ],
        ("Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _cachedSargassumMicroFauna);",
        ],
        ("Assets", "_Project", "Scripts", "FaunaDirector.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveMicroFaunaPresentationPulseSink(ref _sargassumMicroFauna);",
        ],
        ("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs"): [
            "WorldRuntimeReferenceUtility.TryResolveMicroFaunaPresentationPulseSink(ref _sargassumMicroFauna);",
        ],
    }

    for path_parts, expected_fragments in micro_fauna_consumer_contracts.items():
        source = read_project_source(*path_parts)
        for fragment in expected_fragments:
            assert fragment in source, f"missing micro-fauna owner-local route {fragment!r} in {Path(*path_parts)}"
        assert "GlobalRegistry.SargassumMicroFauna" not in source, (
            f"stale micro-fauna registry route in {Path(*path_parts)}"
        )
        assert "GlobalRegistry.MicroFaunaPresentationPulses" not in source, (
            f"stale micro-fauna presentation registry route in {Path(*path_parts)}"
        )

    fauna_director = read_project_source("Assets", "_Project", "Scripts", "FaunaDirector.cs")
    assert "_sargassumMicroFauna = null;" in method_block(
        fauna_director,
        "private void ShutdownServiceState",
    ), "FaunaDirector must clear Sargassum presentation sink on lifecycle shutdown"
    fauna_brain = read_project_source("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs")
    assert "_sargassumMicroFauna = null;" in method_block(
        fauna_brain,
        "private void OnDisable",
    ), "FaunaBrain must clear Sargassum presentation sink on disable"
    assert "_sargassumMicroFauna = null;" in method_block(
        fauna_brain,
        "private void OnDestroy",
    ), "FaunaBrain must clear Sargassum presentation sink on destroy"
    ecosystem_director = read_project_source("Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs")
    assert "_cachedSargassumMicroFauna = null;" in method_block(
        ecosystem_director,
        "private void ShutdownServiceState",
    ), "EcosystemDirector must clear Sargassum micro-fauna reader on lifecycle shutdown"
    player_pda = read_project_source("Assets", "_Project", "Scripts", "PlayerPDA.cs")
    diagnostic_terminal_start = player_pda.find("public sealed class PDADiagnosticTerminal")
    assert diagnostic_terminal_start >= 0, "missing PDADiagnosticTerminal"
    diagnostic_terminal = player_pda[diagnostic_terminal_start:]
    assert "ResolveDiagnosticsSources();" in method_block(
        diagnostic_terminal,
        "private void OnEnable",
    ), "PDADiagnosticTerminal must resolve Sargassum diagnostics sources on enable"
    assert "_microFaunaBoids = null;" in method_block(
        diagnostic_terminal,
        "private void OnDisable",
    ), "PDADiagnosticTerminal must clear Sargassum diagnostics source on disable"
    assert "_microFaunaBoids = null;" in method_block(
        diagnostic_terminal,
        "private void OnDestroy",
    ), "PDADiagnosticTerminal must clear Sargassum diagnostics source on destroy"
    diagnostic_terminal_replaced = method_block(
        diagnostic_terminal,
        "public void OnGlobalRegistryServiceReplaced",
    )
    assert_contains_all(
        diagnostic_terminal_replaced,
        [
            "serviceSlot == GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime",
            "WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _microFaunaBoids);",
            "QueueTerminalRefresh(force: true);",
        ],
        "PDADiagnosticTerminal.OnGlobalRegistryServiceReplaced",
    )


def main() -> int:
    assert_sargassum_dump_layout_contract()
    assert_sargassum_owner_local_runtime_routes()

    root = SMOKE_ROOT
    if root.exists():
        remove_tree_with_retry(root)
    root.mkdir(parents=True, exist_ok=True)

    generic = server.GENERIC_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, server.GENERIC_BLACKBOX_ENTRY.size)
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        10, 3, 0.010, 1.5, 7.25, 512.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        11, 3, 0.020, 1.5, 7.25, 513.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    (root / "Dump_PLAYER_KINEMATICS.bin").write_bytes(generic)
    h8dump_dir = root / "persistent_copy"
    h8dump_dir.mkdir(exist_ok=True)
    h8dump_path = h8dump_dir / "BLACKBOX_CRASH.h8dump"
    h8dump_path.write_bytes(generic)
    (root / "BLACKBOX_CRASH.h8dump").write_bytes(generic)
    parsed_crash = server.parse_dump_file(h8dump_path)
    assert parsed_crash["type"] == "crash_telemetry_buffer"
    assert parsed_crash["latest"]["spike"] is True

    false_positive_generic = server.GENERIC_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, server.GENERIC_BLACKBOX_ENTRY.size)
    false_positive_generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        64, 3, 0.010, 0.0, 7.25, 512.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    false_positive_generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        65, 3, 0.020, 0.0, 7.25, 513.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    false_positive_path = root / "Dump_FALSE_POSITIVE_GENERIC_BLACKBOX.bin"
    false_positive_path.write_bytes(false_positive_generic)
    assert server.parse_dump_file(false_positive_path)["type"] == "generic_blackbox"

    job_hash = server.compute_job_admission_state_hash(77, 0xA1100001, 0.25, 0.0, 3, 0, 0x12)
    job_admission = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 2, 64, 0, 77, 0)
    job_admission += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        77, 0xA1100001, 0.25, 0.0, 3, 0, 1, 0x12, 0, job_hash
    )
    job_admission += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    job_admission += bytes(64)
    job_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin"
    job_path.write_bytes(job_admission)
    parsed_job = server.parse_dump_file(job_path)
    assert parsed_job["type"] == "job_admission_blackbox"
    assert parsed_job["version"] == 2
    assert parsed_job["latest"]["denied"] is True
    assert parsed_job["latest"]["insufficientBudget"] is True
    assert parsed_job["latest"]["stateHashOk"] is True

    legacy_hash = server.compute_job_admission_state_hash(78, 0xA1100002, 0.5, 0.0, 1, 0, 0x03)
    legacy_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 1, 64, 0, 78, 0)
    legacy_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        78, 0xA1100002, 0.5, 0.0, 1, 0, 1, 0x03, 0, legacy_hash
    )
    legacy_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    legacy_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_LegacyV1.bin"
    legacy_path.write_bytes(legacy_job)
    parsed_legacy_job = server.parse_dump_file(legacy_path)
    assert parsed_legacy_job["type"] == "job_admission_blackbox"
    assert parsed_legacy_job["version"] == 1
    assert parsed_legacy_job["latest"]["legacyStarved"] is True
    assert "denied" not in parsed_legacy_job["latest"]
    assert parsed_legacy_job["latest"]["stateHashOk"] is True

    mismatched_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 0, 79, 0)
    mismatched_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        79, 0xA1100004, 0.75, 0.0, 2, 0, 1, 0x02, 0, 0xBAD0BAD0
    )
    mismatched_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    parsed_mismatched_job = server.try_parse_job_admission_blackbox(mismatched_job)
    assert parsed_mismatched_job is not None
    assert parsed_mismatched_job["latest"]["stateHashOk"] is False
    assert "state_hash_mismatch" in parsed_mismatched_job["warnings"]
    invalid_cursor_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 1, 79, 0)
    invalid_cursor_job += bytes(64)
    assert server.try_parse_job_admission_blackbox(invalid_cursor_job) is None

    old_sorted_late_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 0, 1, 0)
    old_sorted_late_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        1,
        0xA1100003,
        0.1,
        0.0,
        0,
        0,
        1,
        0x01,
        0,
        server.compute_job_admission_state_hash(1, 0xA1100003, 0.1, 0.0, 0, 0, 0x01),
    )
    old_sorted_late_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    (root / "Dump_Z_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_OldFrame.bin").write_bytes(old_sorted_late_job)

    simulation_flags = (1 << 1) | (1 << 5)
    simulation_bucket = server.SIMULATION_BUCKET_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 2, 64, 0, 88, 5)
    simulation_bucket += server.SIMULATION_BUCKET_BLACKBOX_ENTRY.pack(
        88, 1, 2, 3, 4, 5, simulation_flags, 5, 1.25, 0.04, 3.0, 1.5, 2.1, 0.75, 2, 1, 0, 0x12345678
    )
    simulation_bucket += bytes(64)
    simulation_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin"
    simulation_path.write_bytes(simulation_bucket)
    parsed_simulation = server.parse_dump_file(simulation_path)
    assert parsed_simulation["type"] == "simulation_bucket_blackbox"
    assert parsed_simulation["version"] == 1
    assert parsed_simulation["latest"]["preSimulationOverBudget"] is True
    assert parsed_simulation["latest"]["homeostasisKillRequested"] is True
    assert parsed_simulation["latest"]["framePacingFlagLabels"] == ["pre-sim-over-budget", "homeostasis-kill"]
    assert parsed_simulation["latest"]["activeBucketLoadMs"] == 1.25
    invalid_simulation_cursor = server.SIMULATION_BUCKET_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 1, 64, 1, 89, 5)
    invalid_simulation_cursor += bytes(64)
    assert server.try_parse_simulation_bucket_blackbox(invalid_simulation_cursor) is None

    terrain_faults = (1 << 0) | (1 << 8)
    terrain_streaming = server.TERRAIN_STREAMING_HEADER.pack(server.HECTON8_MAGIC, 1305, 2, 64, terrain_faults)
    terrain_streaming += server.TERRAIN_STREAMING_PAGER_ENTRY.pack(
        100.0, 200.0, 300.0, 144, 0x1234ABCD, 8, 2, 1, 3, 0.75, 42, 128.0, terrain_faults, 1, 9
    )
    terrain_streaming += bytes(64)
    terrain_path = root / "Dump_1305_TerrainChunkPager.bin"
    terrain_path.write_bytes(terrain_streaming)
    parsed_terrain = server.parse_dump_file(terrain_path)
    assert parsed_terrain["type"] == "terrain_streaming_pager"
    assert parsed_terrain["version"] == 1305
    assert parsed_terrain["latest"]["faultLabels"] == ["missing-file", "checksum"]
    assert parsed_terrain["latest"]["activeChunks"] == 8
    assert parsed_terrain["latest"]["residencyEvalMicros"] == 42

    residency_flags = (1 << 3) | (1 << 10)
    packed_residency_flags = residency_flags | (7 << 16)
    residency_entry = server.WORLD_CHUNK_RESIDENCY_ENTRY.pack(
        123456789, -1, 2, -3, 0.5, 1.5, 2.5, 145, packed_residency_flags, 0xABCDEF01, 4, 11, 2, 1
    )
    residency = server.WORLD_CHUNK_RESIDENCY_HEADER.pack(
        server.HECTON8_MAGIC,
        server.WORLD_CHUNK_RESIDENCY_VERSION,
        2,
        server.WORLD_CHUNK_RESIDENCY_ENTRY.size,
        residency_flags,
        server.WORLD_CHUNK_RESIDENCY_LAYOUT_HASH,
        0,
    )
    residency += residency_entry
    residency += bytes(64)
    residency_path = root / "Dump_1305_WorldChunkResidency.bin"
    residency_path.write_bytes(residency)
    parsed_residency = server.parse_dump_file(residency_path)
    assert parsed_residency["type"] == "world_chunk_residency_blackbox"
    assert parsed_residency["version"] == server.WORLD_CHUNK_RESIDENCY_VERSION
    assert parsed_residency["reasonLabels"] == ["teleport", "addressables-fault"]
    assert parsed_residency["latest"]["flagLabels"] == ["teleport", "addressables-fault"]
    assert parsed_residency["latest"]["activeImpostorCount"] == 7
    assert parsed_residency["latest"]["residentCount"] == 11
    raw_residency_path = root / "Dump_1305_Streaming.bin"
    raw_residency_path.write_bytes(residency_entry + bytes(64))
    parsed_raw_residency = server.parse_dump_file(raw_residency_path)
    assert parsed_raw_residency["type"] == "world_chunk_residency_blackbox"
    assert parsed_raw_residency["latest"]["frame"] == 145

    bus_hash_offset = 64
    bus_source_offset = 512
    bus_mock_physics_offset = bus_source_offset + server.GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY * 64
    bus_mock_origin_offset = bus_mock_physics_offset + 64
    bus_frame_stride = bus_mock_origin_offset + 64
    bus_valid_frames = 2
    bus_header = bytearray(server.GLOBAL_TELEMETRY_BUS_HEADER_BYTES)
    server.GLOBAL_TELEMETRY_PREFIX.pack_into(bus_header, 0, 123456789, 22, 0x57444721)
    bus_metadata = [
        server.GLOBAL_TELEMETRY_BUS_DUMP_MAGIC,
        server.GLOBAL_TELEMETRY_BUS_DUMP_VERSION,
        server.GLOBAL_TELEMETRY_BUS_HEADER_BYTES,
        bus_valid_frames,
        bus_frame_stride,
        bus_valid_frames * bus_frame_stride,
        0xAABBCCDD,
        bus_valid_frames,
        2,
        3,
        bus_hash_offset,
        bus_source_offset,
        bus_mock_physics_offset,
        bus_mock_origin_offset,
        0x11223344,
        0x55667788,
        64,
        48,
        48,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_UINT_STRIDE,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY,
    ]
    for index, value in enumerate(bus_metadata):
        struct.pack_into("<I", bus_header, server.GLOBAL_TELEMETRY_BUS_METADATA_OFFSET + index * 4, value)
    descriptor_offset = (
        server.GLOBAL_TELEMETRY_BUS_METADATA_OFFSET
        + server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX * 4
    )
    struct.pack_into("<IIII", bus_header, descriptor_offset, 0xABC00001, 1, 16, 0)
    struct.pack_into("<IIII", bus_header, descriptor_offset + 16, server.SURVIVAL_BLACKBOX_SOURCE_HASH, 1, 64, 1)
    bus_frames = bytearray(bus_valid_frames * bus_frame_stride)
    for frame_index, frame_number in enumerate((200, 201)):
        frame_offset = frame_index * bus_frame_stride
        server.GLOBAL_TELEMETRY_PREFIX.pack_into(bus_frames, frame_offset, 123456800 + frame_index, frame_number, 0x57444721)
        struct.pack_into("<III", bus_frames, frame_offset + bus_hash_offset, 0x11111111, 0x22222222, 0x33333333)
        struct.pack_into("<IIII", bus_frames, frame_offset + bus_source_offset, 1, 2, 3, frame_number)
    survival_bus_flags = (
        (1 << 1)
        | (1 << 2)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 7)
        | (1 << 8)
        | (1 << 9)
        | (2 << server.SURVIVAL_BLACKBOX_DEATH_CAUSE_SHIFT)
    )
    server.SURVIVAL_BLACKBOX_SOURCE_ENTRY.pack_into(
        bus_frames,
        bus_frame_stride + bus_source_offset + server.GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES,
        server.SURVIVAL_BLACKBOX_SOURCE_HASH,
        201,
        0x504C5952,
        0.12,
        0.34,
        900.0,
        91.0,
        500.0,
        400.0,
        1.0,
        0.77,
        0.64,
        0.88,
        6.5,
        0xAABBCCDD,
        survival_bus_flags,
    )
    bus_path = root / "Dump_GLOBAL_TELEMETRY_BUS.bin"
    bus_path.write_bytes(bytes(bus_header) + bytes(bus_frames))
    parsed_bus = server.parse_dump_file(bus_path)
    assert parsed_bus["type"] == "global_telemetry_bus_blackbox"
    assert parsed_bus["version"] == server.GLOBAL_TELEMETRY_BUS_DUMP_VERSION
    assert parsed_bus["latest"]["frame"] == 201
    assert parsed_bus["latest"]["eventHashCount"] == 3
    assert parsed_bus["latest"]["sourceNonZeroCount"] == 2
    assert parsed_bus["latest"]["decodedSourceCount"] == 1
    assert parsed_bus["latest"]["survival"]["playerEntityHashHex"] == "0x504C5952"
    assert parsed_bus["latest"]["survival"]["oxygen01"] == 0.12
    assert parsed_bus["latest"]["survival"]["pressureAtm"] == 91.0
    assert parsed_bus["latest"]["survival"]["deathCauseLabel"] == "pressure-collapse"
    assert parsed_bus["latest"]["survival"]["flagLabels"] == [
        "underwater",
        "beyond-safe-depth",
        "bends",
        "fresh-physiology",
        "narcosis",
        "toxicity",
        "thermal-stress",
        "has-stats",
    ]
    assert "survival_source_warnings" in parsed_bus["warnings"]
    assert parsed_bus["sourceDescriptors"][0]["sourceHash"] == 0xABC00001
    assert parsed_bus["sourceDescriptors"][1]["sourceName"] == "survival"

    data_monolith = bytearray(
        server.DATA_MONOLITH_TELEMETRY_HEADER.pack(
            server.DATA_MONOLITH_TELEMETRY_MAGIC,
            1,
            2,
            server.DATA_MONOLITH_TELEMETRY_RING_CAPACITY,
            server.DATA_MONOLITH_TELEMETRY_ENTRY_BYTES,
        )
        + bytes(server.DATA_MONOLITH_TELEMETRY_RING_CAPACITY * server.DATA_MONOLITH_TELEMETRY_ENTRY_BYTES)
    )
    server.DATA_MONOLITH_TELEMETRY_ENTRY.pack_into(
        data_monolith,
        server.DATA_MONOLITH_TELEMETRY_HEADER_BYTES,
        0x1111222233334444,
        1200,
        700,
        41,
        0,
        0,
        8,
        (1 << 0) | (1 << 2),
        0x10101010,
        1,
        8,
        4,
        0,
    )
    server.DATA_MONOLITH_TELEMETRY_ENTRY.pack_into(
        data_monolith,
        server.DATA_MONOLITH_TELEMETRY_HEADER_BYTES + server.DATA_MONOLITH_TELEMETRY_ENTRY_BYTES,
        0x5555666677778888,
        2400,
        900,
        42,
        12345678,
        28,
        1,
        (1 << 1) | (1 << 2) | (1 << 4),
        0x20202020,
        0,
        0,
        0,
        0,
    )
    data_monolith_path = root / "Dump_H8StaticDataArena_Telemetry.bin"
    data_monolith_path.write_bytes(data_monolith)
    parsed_data_monolith = server.parse_dump_file(data_monolith_path)
    assert parsed_data_monolith["type"] == "data_monolith_telemetry_blackbox"
    assert parsed_data_monolith["entrySize"] == server.DATA_MONOLITH_TELEMETRY_ENTRY_BYTES
    assert parsed_data_monolith["declaredEntryCount"] == server.DATA_MONOLITH_TELEMETRY_RING_CAPACITY
    assert parsed_data_monolith["nonEmptyEntryCount"] == 2
    assert parsed_data_monolith["latest"]["frame"] == 42
    assert parsed_data_monolith["latest"]["loadStatusLabel"] == "loaded"
    assert parsed_data_monolith["latest"]["pathFlagLabels"] == [
        "memory-mapped-file",
        "vault-backed",
        "native-file",
    ]
    assert parsed_data_monolith["latest"]["checksum64Hex"] == "0x5555666677778888"
    assert parsed_data_monolith["latest"]["stateHashHex"] == "0x20202020"
    assert "load_failures" in parsed_data_monolith["warnings"]
    assert "failure_details" in parsed_data_monolith["warnings"]
    renamed_data_monolith_path = root / "Renamed_DataMonolithTelemetry.bin"
    renamed_data_monolith_path.write_bytes(data_monolith)
    assert server.parse_dump_file(renamed_data_monolith_path)["type"] == "data_monolith_telemetry_blackbox"

    vault_sovereignty = bytearray(
        server.VAULT_SOVEREIGNTY_TELEMETRY_HEADER.pack(
            server.VAULT_SOVEREIGNTY_TELEMETRY_MAGIC,
            server.VAULT_SOVEREIGNTY_TELEMETRY_VERSION,
            server.VAULT_SOVEREIGNTY_TELEMETRY_CAPACITY,
            server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES,
        )
        + bytes(server.VAULT_SOVEREIGNTY_TELEMETRY_CAPACITY * server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES)
    )
    server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY.pack_into(
        vault_sovereignty,
        server.VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES,
        8 * 1024 * 1024,
        4 * 1024 * 1024,
        24,
        1,
        2,
        70.5,
        1100,
        7,
        559,
        0x10101010,
        0.75,
        0,
        0,
    )
    server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY.pack_into(
        vault_sovereignty,
        server.VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES + server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES,
        16 * 1024 * 1024,
        10 * 1024 * 1024,
        42,
        3,
        4,
        91.25,
        1101,
        8,
        559,
        0x20202020,
        0.5,
        (1 << 0) | (1 << 5),
        0,
    )
    vault_sovereignty_path = root / "Dump_SHINOBU_100.bin"
    vault_sovereignty_path.write_bytes(vault_sovereignty)
    parsed_vault_sovereignty = server.parse_dump_file(vault_sovereignty_path)
    assert parsed_vault_sovereignty["type"] == "vault_sovereignty_telemetry_blackbox"
    assert parsed_vault_sovereignty["entrySize"] == server.VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES
    assert parsed_vault_sovereignty["declaredEntryCount"] == server.VAULT_SOVEREIGNTY_TELEMETRY_CAPACITY
    assert parsed_vault_sovereignty["nonEmptyEntryCount"] == 2
    assert parsed_vault_sovereignty["latest"]["frame"] == 1101
    assert parsed_vault_sovereignty["latest"]["totalVaultBytes"] == 16 * 1024 * 1024
    assert parsed_vault_sovereignty["latest"]["arenaBytes"] == 10 * 1024 * 1024
    assert parsed_vault_sovereignty["latest"]["bufferId"] == 559
    assert parsed_vault_sovereignty["latest"]["flagLabels"] == ["fault", "unknown=0x00000020"]
    assert parsed_vault_sovereignty["latest"]["stateHashHex"] == "0x20202020"
    assert parsed_vault_sovereignty["memoryMap"][0]["label"] == "vault-arena"
    assert parsed_vault_sovereignty["memoryMap"][1]["bytes"] == 6 * 1024 * 1024
    assert "fault_flag" in parsed_vault_sovereignty["warnings"]
    assert "unknown_flags" in parsed_vault_sovereignty["warnings"]
    renamed_vault_sovereignty_path = root / "Renamed_VaultSovereigntyTelemetry.bin"
    renamed_vault_sovereignty_path.write_bytes(vault_sovereignty)
    assert server.parse_dump_file(renamed_vault_sovereignty_path)["type"] == "vault_sovereignty_telemetry_blackbox"

    alignment_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    alignment = bytearray(
        server.ARM64_ALIGNMENT_TELEMETRY_HEADER.pack(
            server.ARM64_ALIGNMENT_TELEMETRY_MAGIC,
            server.ARM64_ALIGNMENT_TELEMETRY_VERSION,
            server.ARM64_ALIGNMENT_TELEMETRY_CAPACITY,
            server.ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES,
        )
        + bytes(server.ARM64_ALIGNMENT_TELEMETRY_CAPACITY * server.ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES)
    )
    server.ARM64_ALIGNMENT_TELEMETRY_ENTRY.pack_into(
        alignment,
        server.ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES,
        0x1111222233334444,
        0x1000,
        1.0,
        2.0,
        3.0,
        642,
        16,
        1201,
        1 << 4,
        0.25,
        0x10101010,
    )
    server.ARM64_ALIGNMENT_TELEMETRY_ENTRY.pack_into(
        alignment,
        server.ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES + server.ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES,
        0x5555666677778888,
        0x2000,
        4.0,
        5.0,
        6.0,
        643,
        24,
        1202,
        alignment_flags,
        1.0,
        0x20202020,
    )
    alignment_path = root / "Dump_SHINOBU_204.bin"
    alignment_path.write_bytes(alignment)
    parsed_alignment = server.parse_dump_file(alignment_path)
    assert parsed_alignment["type"] == "arm64_alignment_telemetry_blackbox"
    assert parsed_alignment["entrySize"] == server.ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES
    assert parsed_alignment["declaredEntryCount"] == server.ARM64_ALIGNMENT_TELEMETRY_CAPACITY
    assert parsed_alignment["nonEmptyEntryCount"] == 2
    assert parsed_alignment["latest"]["frame"] == 1202
    assert parsed_alignment["latest"]["bufferID"] == 643
    assert parsed_alignment["latest"]["byteOffset"] == 24
    assert parsed_alignment["latest"]["flags"] == alignment_flags
    assert parsed_alignment["latest"]["flagLabels"] == [
        "pack1-detected",
        "misaligned-8-byte-field",
        "invalid-stride",
        "dynamic-cast-fault",
        "dump-written",
    ]
    assert parsed_alignment["latest"]["severity01"] == 1.0
    assert parsed_alignment["latest"]["structHashHex"] == "0x5555666677778888"
    assert parsed_alignment["latest"]["stateHashHex"] == "0x20202020"
    assert "pack1_detected" in parsed_alignment["warnings"]
    assert "misaligned_8_byte_field" in parsed_alignment["warnings"]
    assert "invalid_stride" in parsed_alignment["warnings"]
    assert "dynamic_cast_fault" in parsed_alignment["warnings"]
    renamed_alignment_path = root / "Renamed_AlignmentTelemetry.bin"
    renamed_alignment_path.write_bytes(alignment)
    assert server.parse_dump_file(renamed_alignment_path)["type"] == "arm64_alignment_telemetry_blackbox"

    haptic = bytearray(
        server.HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY * server.HAPTIC_SYNTHESIS_TELEMETRY_ENTRY_BYTES
    )
    haptic_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    server.HAPTIC_SYNTHESIS_TELEMETRY_ENTRY.pack_into(
        haptic,
        0,
        1.0,
        2.0,
        3.0,
        0.25,
        0.5,
        1301,
        3,
        0,
        100,
        1 << 4,
        0.8,
        0x10101010,
        2,
    )
    server.HAPTIC_SYNTHESIS_TELEMETRY_ENTRY.pack_into(
        haptic,
        server.HAPTIC_SYNTHESIS_TELEMETRY_ENTRY_BYTES,
        4.0,
        5.0,
        6.0,
        0.85,
        0.7,
        1302,
        8,
        2,
        250,
        haptic_flags,
        0.5,
        0x20202020,
        server.HAPTIC_SYNTHESIS_PULSE_CAPACITY + 1,
    )
    haptic_path = root / "Dump_SHINOBU_353.bin"
    haptic_path.write_bytes(haptic)
    parsed_haptic = server.parse_dump_file(haptic_path)
    assert parsed_haptic["type"] == "haptic_synthesis_telemetry_blackbox"
    assert parsed_haptic["entrySize"] == server.HAPTIC_SYNTHESIS_TELEMETRY_ENTRY_BYTES
    assert parsed_haptic["declaredEntryCount"] == server.HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY
    assert parsed_haptic["nonEmptyEntryCount"] == 2
    assert parsed_haptic["latest"]["frame"] == 1302
    assert parsed_haptic["latest"]["rawSignalCount"] == 8
    assert parsed_haptic["latest"]["droppedSignalCount"] == 2
    assert parsed_haptic["latest"]["burstExecutionMicroseconds"] == 250
    assert parsed_haptic["latest"]["flags"] == haptic_flags
    assert parsed_haptic["latest"]["flagLabels"] == [
        "nan-sanitized",
        "budget-exceeded",
        "pulse-overflow",
        "missing-player-aup",
        "mock-storm-active",
    ]
    assert parsed_haptic["latest"]["stateHashHex"] == "0x20202020"
    assert "nan_sanitized" in parsed_haptic["warnings"]
    assert "budget_exceeded" in parsed_haptic["warnings"]
    assert "pulse_overflow" in parsed_haptic["warnings"]
    assert "missing_player_aup" in parsed_haptic["warnings"]
    assert "mock_storm_active" in parsed_haptic["warnings"]
    assert "dropped_signals" in parsed_haptic["warnings"]
    assert "burst_over_200us" in parsed_haptic["warnings"]
    assert "generated_pulse_over_capacity" in parsed_haptic["warnings"]
    renamed_haptic_path = root / "Renamed_HapticTelemetry.bin"
    renamed_haptic_path.write_bytes(haptic)
    assert server.parse_dump_file(renamed_haptic_path)["type"] != "haptic_synthesis_telemetry_blackbox"

    vocal_warning_faults = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6)
    vocal_warning = server.VOCAL_WARNING_TELEMETRY_HEADER.pack(
        server.VOCAL_WARNING_TELEMETRY_MAGIC,
        server.VOCAL_WARNING_TELEMETRY_VERSION,
        server.VOCAL_WARNING_TELEMETRY_ENTRY_BYTES,
        server.VOCAL_WARNING_TELEMETRY_CAPACITY,
        2,
        2,
        0,
        0,
    )
    vocal_warning += server.VOCAL_WARNING_TELEMETRY_ENTRY.pack(
        10,
        20,
        30,
        1 << 0,
        1401,
        1,
        0x43525348,
        1.25,
        50.0,
        0,
        0,
        0,
        1,
        0,
    )
    vocal_warning += server.VOCAL_WARNING_TELEMETRY_ENTRY.pack(
        11,
        21,
        31,
        (1 << 1) | (1 << 2),
        1402,
        2,
        0x4F584C4F,
        2.5,
        125.0,
        vocal_warning_faults,
        2,
        0x4431,
        3,
        2,
    )
    vocal_warning_path = root / "Dump_SHINOBU_352_VWS.bin"
    vocal_warning_path.write_bytes(vocal_warning)
    parsed_vocal_warning = server.parse_dump_file(vocal_warning_path)
    assert parsed_vocal_warning["type"] == "vocal_warning_telemetry_blackbox"
    assert parsed_vocal_warning["entrySize"] == server.VOCAL_WARNING_TELEMETRY_ENTRY_BYTES
    assert parsed_vocal_warning["declaredEntryCount"] == 2
    assert parsed_vocal_warning["capacity"] == server.VOCAL_WARNING_TELEMETRY_CAPACITY
    assert parsed_vocal_warning["nonEmptyEntryCount"] == 2
    assert parsed_vocal_warning["latest"]["frame"] == 1402
    assert parsed_vocal_warning["latest"]["currentWarningLabel"] == "oxygen-low"
    assert parsed_vocal_warning["latest"]["lastDispatchedWarningLabel"] == "hull-breach"
    assert parsed_vocal_warning["latest"]["currentAudioBankHashHex"] == "0x4F584C4F"
    assert parsed_vocal_warning["latest"]["activeAlarmLabels"] == ["hull-breach", "oxygen-low"]
    assert parsed_vocal_warning["latest"]["faultFlagLabels"] == [
        "telemetry-invalid",
        "priority-input-invalid",
        "vocal-cue-rejected",
        "subtitle-rejected",
        "alarm-mask-overflow",
        "vocal-warning-signal-rejected",
    ]
    assert parsed_vocal_warning["latest"]["directionHashHex"] == "0x4431"
    assert "fault_flags" in parsed_vocal_warning["warnings"]
    assert "telemetry_invalid" in parsed_vocal_warning["warnings"]
    assert "priority_input_invalid" in parsed_vocal_warning["warnings"]
    assert "vocal_cue_rejected" in parsed_vocal_warning["warnings"]
    assert "subtitle_rejected" in parsed_vocal_warning["warnings"]
    assert "alarm_mask_overflow" in parsed_vocal_warning["warnings"]
    assert "vocal_warning_signal_rejected" in parsed_vocal_warning["warnings"]
    assert "burst_over_100us" in parsed_vocal_warning["warnings"]
    renamed_vocal_warning_path = root / "Renamed_VocalWarningTelemetry.bin"
    renamed_vocal_warning_path.write_bytes(vocal_warning)
    assert server.parse_dump_file(renamed_vocal_warning_path)["type"] == "vocal_warning_telemetry_blackbox"

    granular = bytearray(
        server.GRANULAR_AUDIO_TELEMETRY_HEADER.pack(
            server.GRANULAR_AUDIO_TELEMETRY_CAPACITY,
            2,
        )
    )
    granular.extend(bytearray(server.GRANULAR_AUDIO_TELEMETRY_CAPACITY * server.GRANULAR_AUDIO_TELEMETRY_ROW_BYTES))
    server.GRANULAR_AUDIO_TELEMETRY_ROW.pack_into(
        granular,
        server.GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES,
        64,
        0.25,
        0.05,
        0.4,
        0.2,
        0.125,
        420.0,
        4,
        16,
        8,
        1 << 2,
    )
    granular_flags = (1 << 0) | (1 << 1) | (1 << 2)
    server.GRANULAR_AUDIO_TELEMETRY_ROW.pack_into(
        granular,
        server.GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES + server.GRANULAR_AUDIO_TELEMETRY_ROW_BYTES,
        128,
        0.95,
        0.7,
        0.8,
        0.65,
        -0.25,
        65000.0,
        64,
        64,
        server.GRANULAR_AUDIO_ECHO_TAP_CAPACITY,
        granular_flags,
    )
    granular_path = root / "Dump_SHINOBU_351.bin"
    granular_path.write_bytes(granular)
    parsed_granular = server.parse_dump_file(granular_path)
    assert parsed_granular["type"] == "granular_audio_telemetry_blackbox"
    assert parsed_granular["entrySize"] == server.GRANULAR_AUDIO_TELEMETRY_ROW_BYTES
    assert parsed_granular["declaredEntryCount"] == server.GRANULAR_AUDIO_TELEMETRY_CAPACITY
    assert parsed_granular["nonEmptyEntryCount"] == 2
    assert parsed_granular["latest"]["sampleIndex"] == 128
    assert parsed_granular["latest"]["activeVoices"] == 64
    assert parsed_granular["latest"]["voiceLimit"] == 64
    assert parsed_granular["latest"]["flagLabels"] == ["invalid", "voice-limit-reached", "impact-drive-active"]
    assert "invalid" in parsed_granular["warnings"]
    assert "voice_limit_reached" in parsed_granular["warnings"]
    renamed_granular_path = root / "Renamed_ProceduralSynth.bin"
    renamed_granular_path.write_bytes(granular)
    assert server.parse_dump_file(renamed_granular_path)["type"] != "granular_audio_telemetry_blackbox"

    prologue_audio = bytearray(
        server.PROLOGUE_AUDIO_TRANSITION_HEADER.pack(
            server.PROLOGUE_AUDIO_TRANSITION_CAPACITY,
            2,
        )
    )
    prologue_audio.extend(bytearray(server.PROLOGUE_AUDIO_TRANSITION_CAPACITY * server.PROLOGUE_AUDIO_TRANSITION_ROW_BYTES))
    server.PROLOGUE_AUDIO_TRANSITION_ROW.pack_into(
        prologue_audio,
        server.PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES,
        1501,
        3,
        4200.0,
        0.35,
        1200.0,
        0.25,
        0.5,
        0.0,
        0.0,
        1600.0,
        0,
        2,
        1 << 2,
        1,
        0,
        1 << 3,
    )
    prologue_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 4)
    prologue_dsp_flags = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4)
    server.PROLOGUE_AUDIO_TRANSITION_ROW.pack_into(
        prologue_audio,
        server.PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES + server.PROLOGUE_AUDIO_TRANSITION_ROW_BYTES,
        1502,
        4,
        6800.0,
        0.9,
        22000.0,
        0.75,
        0.85,
        0.65,
        0.55,
        18000.0,
        4410,
        4,
        prologue_flags,
        2,
        0,
        prologue_dsp_flags,
    )
    prologue_audio_path = root / "Dump_PROLOGUE_ACOUSTIC_ORCHESTRATOR.bin"
    prologue_audio_path.write_bytes(prologue_audio)
    parsed_prologue_audio = server.parse_dump_file(prologue_audio_path)
    assert parsed_prologue_audio["type"] == "prologue_audio_transition_blackbox"
    assert parsed_prologue_audio["entrySize"] == server.PROLOGUE_AUDIO_TRANSITION_ROW_BYTES
    assert parsed_prologue_audio["nonEmptyEntryCount"] == 2
    assert parsed_prologue_audio["latest"]["frame"] == 1502
    assert parsed_prologue_audio["latest"]["stageLabel"] == "ocean-handoff"
    assert parsed_prologue_audio["latest"]["flagLabels"] == [
        "splashdown",
        "portal-active",
        "granular-enabled",
        "nonfinite-guard",
    ]
    assert parsed_prologue_audio["latest"]["dspFlagLabels"] == [
        "invalid",
        "portal-active",
        "granular-enabled",
        "splashdown",
    ]
    assert "invalid" in parsed_prologue_audio["warnings"]
    assert "nonfinite_guard" in parsed_prologue_audio["warnings"]
    renamed_prologue_audio_path = root / "Renamed_PrologueAudio.bin"
    renamed_prologue_audio_path.write_bytes(prologue_audio)
    assert server.parse_dump_file(renamed_prologue_audio_path)["type"] != "prologue_audio_transition_blackbox"

    audio_synthesis = bytearray(
        server.AUDIO_SYNTHESIS_TELEMETRY_HEADER.pack(
            server.AUDIO_SYNTHESIS_TELEMETRY_CAPACITY,
            2,
        )
    )
    audio_synthesis.extend(bytearray(server.AUDIO_SYNTHESIS_TELEMETRY_CAPACITY * server.AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES))
    server.AUDIO_SYNTHESIS_TELEMETRY_ROW.pack_into(
        audio_synthesis,
        server.AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES,
        111111,
        1601,
        0x00011537,
        server.AUDIO_SYNTHESIS_AUDIO_PLAYER_CRITICAL_SYSTEM_ID,
        7,
        7,
        0,
        12,
        32,
        88.25,
        0.9,
        0,
        0,
    )
    audio_synthesis_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3)
    server.AUDIO_SYNTHESIS_TELEMETRY_ROW.pack_into(
        audio_synthesis,
        server.AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES + server.AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES,
        222222,
        1602,
        0x00011538,
        server.AUDIO_SYNTHESIS_AUDIO_PLAYER_CRITICAL_SYSTEM_ID,
        10,
        9,
        audio_synthesis_flags,
        64,
        64,
        250.5,
        0.55,
        4,
        3,
    )
    audio_synthesis_path = root / "Dump_1320_Synthesis.bin"
    audio_synthesis_path.write_bytes(audio_synthesis)
    parsed_audio_synthesis = server.parse_dump_file(audio_synthesis_path)
    assert parsed_audio_synthesis["type"] == "audio_synthesis_telemetry_blackbox"
    assert parsed_audio_synthesis["entrySize"] == server.AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES
    assert parsed_audio_synthesis["nonEmptyEntryCount"] == 2
    assert parsed_audio_synthesis["latest"]["frame"] == 1602
    assert parsed_audio_synthesis["latest"]["bufferIdHex"] == "0x00011538"
    assert parsed_audio_synthesis["latest"]["failureLabel"] == "output-ring-full"
    assert parsed_audio_synthesis["latest"]["flagLabels"] == [
        "lock-contention",
        "stale-or-missing-handle",
        "nonfinite-sample",
        "output-underrun",
    ]
    assert "failure_code" in parsed_audio_synthesis["warnings"]
    assert "generation_mismatch" in parsed_audio_synthesis["warnings"]
    assert "output_underrun" in parsed_audio_synthesis["warnings"]
    assert "underruns" in parsed_audio_synthesis["warnings"]
    renamed_audio_synthesis_path = root / "Renamed_AudioSynthesis.bin"
    renamed_audio_synthesis_path.write_bytes(audio_synthesis)
    assert server.parse_dump_file(renamed_audio_synthesis_path)["type"] != "audio_synthesis_telemetry_blackbox"

    vocal_bank = bytearray(
        server.VOCAL_BANK_SYNTHESIS_HEADER.pack(
            server.VOCAL_BANK_SYNTHESIS_MAGIC,
            server.VOCAL_BANK_SYNTHESIS_VERSION,
            server.VOCAL_BANK_SYNTHESIS_TELEMETRY_CAPACITY,
            server.VOCAL_BANK_SYNTHESIS_ENTRY_BYTES,
            2,
            1 << 3,
            0x05203E88,
            1702,
        )
    )
    vocal_bank.extend(bytearray(server.VOCAL_BANK_SYNTHESIS_TELEMETRY_CAPACITY * server.VOCAL_BANK_SYNTHESIS_ENTRY_BYTES))
    server.VOCAL_BANK_SYNTHESIS_ENTRY.pack_into(
        vocal_bank,
        server.VOCAL_BANK_SYNTHESIS_HEADER_BYTES,
        1701,
        0x05203E88,
        128,
        32000,
        450.0,
        0.5,
        0.12,
        0.95,
        0.1,
        3,
        1 << 0,
        0,
        2048,
        44100,
        1,
    )
    vocal_bank_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    server.VOCAL_BANK_SYNTHESIS_ENTRY.pack_into(
        vocal_bank,
        server.VOCAL_BANK_SYNTHESIS_HEADER_BYTES + server.VOCAL_BANK_SYNTHESIS_ENTRY_BYTES,
        1702,
        0xC001260,
        512,
        4096,
        1200.0,
        0.95,
        0.4,
        0.6,
        0.3,
        7,
        vocal_bank_flags,
        2,
        4096,
        48000,
        2,
    )
    vocal_bank_path = root / "Dump_1308_Synthesis.bin"
    vocal_bank_path.write_bytes(vocal_bank)
    parsed_vocal_bank = server.parse_dump_file(vocal_bank_path)
    assert parsed_vocal_bank["type"] == "vocal_bank_synthesis_blackbox"
    assert parsed_vocal_bank["version"] == server.VOCAL_BANK_SYNTHESIS_VERSION
    assert parsed_vocal_bank["entrySize"] == server.VOCAL_BANK_SYNTHESIS_ENTRY_BYTES
    assert parsed_vocal_bank["nonEmptyEntryCount"] == 2
    assert parsed_vocal_bank["lastFaultFlagLabels"] == ["bank-miss"]
    assert parsed_vocal_bank["lastPhraseHashHex"] == "0x05203E88"
    assert parsed_vocal_bank["latest"]["frame"] == 1702
    assert parsed_vocal_bank["latest"]["phraseHashHex"] == "0x0C001260"
    assert parsed_vocal_bank["latest"]["codecLabel"] == "vorbis"
    assert parsed_vocal_bank["latest"]["flagLabels"] == [
        "playing",
        "vorbis-unsupported",
        "nonfinite",
        "bank-miss",
        "interrupted",
    ]
    assert "last_fault_flags" in parsed_vocal_bank["warnings"]
    assert "vorbis_unsupported" in parsed_vocal_bank["warnings"]
    assert "nonfinite" in parsed_vocal_bank["warnings"]
    assert "bank_miss" in parsed_vocal_bank["warnings"]
    assert "dsp_over_1000us" in parsed_vocal_bank["warnings"]
    assert "underruns" in parsed_vocal_bank["warnings"]
    renamed_vocal_bank_path = root / "Renamed_VocalBankSynthesis.bin"
    renamed_vocal_bank_path.write_bytes(vocal_bank)
    assert server.parse_dump_file(renamed_vocal_bank_path)["type"] == "vocal_bank_synthesis_blackbox"

    adaptive_stem = bytearray(server.ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY * server.ADAPTIVE_STEM_MIXER_ENTRY_BYTES)
    server.ADAPTIVE_STEM_MIXER_ENTRY.pack_into(
        adaptive_stem,
        0,
        1801,
        0xB4510A10,
        0x5348494E,
        1 << 0,
        0.25,
        0.4,
        12000.0,
        350.0,
        0.5,
        0.1,
        0.35,
        0.0,
        0.95,
        0.25,
        0.1,
        2.0,
    )
    adaptive_stem_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    server.ADAPTIVE_STEM_MIXER_ENTRY.pack_into(
        adaptive_stem,
        server.ADAPTIVE_STEM_MIXER_ENTRY_BYTES,
        1802,
        0xB0550A10,
        0x5348494E,
        adaptive_stem_flags,
        0.85,
        0.7,
        800.0,
        1250.0,
        0.2,
        0.6,
        0.4,
        0.9,
        0.55,
        0.75,
        0.65,
        4.0,
    )
    adaptive_stem_path = root / "Dump_STEM_MIXER.bin"
    adaptive_stem_path.write_bytes(adaptive_stem)
    parsed_adaptive_stem = server.parse_dump_file(adaptive_stem_path)
    assert parsed_adaptive_stem["type"] == "adaptive_stem_mixer_blackbox"
    assert parsed_adaptive_stem["entrySize"] == server.ADAPTIVE_STEM_MIXER_ENTRY_BYTES
    assert parsed_adaptive_stem["declaredEntryCount"] == server.ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY
    assert parsed_adaptive_stem["nonEmptyEntryCount"] == 2
    assert parsed_adaptive_stem["latest"]["frame"] == 1802
    assert parsed_adaptive_stem["latest"]["activeStemHashHex"] == "0xB0550A10"
    assert parsed_adaptive_stem["latest"]["flagLabels"] == [
        "beat-gate-open",
        "narrative-override",
        "io-transition-delay",
        "clip-not-streaming",
        "nonfinite",
    ]
    assert "clip_not_streaming" in parsed_adaptive_stem["warnings"]
    assert "io_transition_delay" in parsed_adaptive_stem["warnings"]
    assert "nonfinite" in parsed_adaptive_stem["warnings"]
    assert "mixer_over_1000us" in parsed_adaptive_stem["warnings"]
    renamed_adaptive_stem_path = root / "Renamed_StemMixer.bin"
    renamed_adaptive_stem_path.write_bytes(adaptive_stem)
    assert server.parse_dump_file(renamed_adaptive_stem_path)["type"] != "adaptive_stem_mixer_blackbox"

    camera_juice_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
    )
    camera_juice = server.CAMERA_JUICE_TELEMETRY_HEADER.pack(
        server.CAMERA_JUICE_TELEMETRY_MAGIC,
        server.CAMERA_JUICE_TELEMETRY_VERSION,
        server.CAMERA_JUICE_TELEMETRY_ENTRY_BYTES,
        server.CAMERA_JUICE_TELEMETRY_CAPACITY,
        2,
        2,
        0,
        0,
    )
    camera_juice += server.CAMERA_JUICE_TELEMETRY_ENTRY.pack(
        1901,
        0,
        0.25,
        0.04,
        0.01,
        0.02,
        0.03,
        0.5,
        0.25,
        0.1,
        3,
        50.0,
        0.9,
        0.2,
        0xCAFE0001,
        1901,
    )
    camera_juice += server.CAMERA_JUICE_TELEMETRY_ENTRY.pack(
        1902,
        camera_juice_flags,
        0.85,
        0.12,
        -0.03,
        0.04,
        -0.02,
        2.0,
        -1.5,
        4.0,
        32,
        125.0,
        0.5,
        0.75,
        0xCAFE0002,
        1902,
    )
    camera_juice_path = root / "Dump_CameraJuiceSystem.bin"
    camera_juice_path.write_bytes(camera_juice)
    parsed_camera_juice = server.parse_dump_file(camera_juice_path)
    assert parsed_camera_juice["type"] == "camera_juice_telemetry_blackbox"
    assert parsed_camera_juice["version"] == server.CAMERA_JUICE_TELEMETRY_VERSION
    assert parsed_camera_juice["entrySize"] == server.CAMERA_JUICE_TELEMETRY_ENTRY_BYTES
    assert parsed_camera_juice["declaredEntryCount"] == 2
    assert parsed_camera_juice["nonEmptyEntryCount"] == 2
    assert parsed_camera_juice["latest"]["frame"] == 1902
    assert parsed_camera_juice["latest"]["stateHashHex"] == "0xCAFE0002"
    assert parsed_camera_juice["latest"]["flagLabels"] == [
        "xr-suppressed",
        "nan-sanitized",
        "no-player-aup",
        "vr-somatic-write-rejected",
        "vault-unavailable",
        "burst-budget-exceeded",
    ]
    assert "nan_sanitized" in parsed_camera_juice["warnings"]
    assert "no_player_aup" in parsed_camera_juice["warnings"]
    assert "vr_somatic_write_rejected" in parsed_camera_juice["warnings"]
    assert "vault_unavailable" in parsed_camera_juice["warnings"]
    assert "burst_budget_exceeded" in parsed_camera_juice["warnings"]
    assert "burst_over_100us" in parsed_camera_juice["warnings"]
    renamed_camera_juice_path = root / "Renamed_CameraJuice.bin"
    renamed_camera_juice_path.write_bytes(camera_juice)
    assert server.parse_dump_file(renamed_camera_juice_path)["type"] == "camera_juice_telemetry_blackbox"

    material_decay = bytearray(
        server.MATERIAL_DECAY_HEADER.pack(
            server.MATERIAL_DECAY_MAGIC,
            2,
            2,
            server.MATERIAL_DECAY_TELEMETRY_CAPACITY,
        )
    )
    material_decay.extend(bytearray(server.MATERIAL_DECAY_TELEMETRY_CAPACITY * server.MATERIAL_DECAY_ROW_BYTES))
    server.MATERIAL_DECAY_ROW.pack_into(
        material_decay,
        server.MATERIAL_DECAY_HEADER_BYTES,
        2001,
        0x4D415430,
        0.15,
        0.0,
        0.05,
        2,
        1,
        255,
        1 << 2,
        0xDECAB001,
    )
    material_decay_flags = (1 << 0) | (1 << 1) | (1 << 2)
    server.MATERIAL_DECAY_ROW.pack_into(
        material_decay,
        server.MATERIAL_DECAY_HEADER_BYTES + server.MATERIAL_DECAY_ROW_BYTES,
        2002,
        0x4D415431,
        0.85,
        0.35,
        0.45,
        7,
        2,
        200,
        material_decay_flags,
        0xDECAB002,
    )
    material_decay_path = root / "Dump_MATERIAL_DECAY_ARTIST.bin"
    material_decay_path.write_bytes(material_decay)
    parsed_material_decay = server.parse_dump_file(material_decay_path)
    assert parsed_material_decay["type"] == "material_decay_blackbox"
    assert parsed_material_decay["entrySize"] == server.MATERIAL_DECAY_ROW_BYTES
    assert parsed_material_decay["declaredEntryCount"] == server.MATERIAL_DECAY_TELEMETRY_CAPACITY
    assert parsed_material_decay["dumpReasonLabel"] == "invalid-rust"
    assert parsed_material_decay["nonEmptyEntryCount"] == 2
    assert parsed_material_decay["latest"]["frame"] == 2002
    assert parsed_material_decay["latest"]["itemHashHex"] == "0x4D415431"
    assert parsed_material_decay["latest"]["flagLabels"] == ["rust-active", "wet", "blood"]
    assert "dump_reason" in parsed_material_decay["warnings"]
    assert "rust_active" in parsed_material_decay["warnings"]
    assert "wet" in parsed_material_decay["warnings"]
    assert "blood" in parsed_material_decay["warnings"]
    renamed_material_decay_path = root / "Renamed_MaterialDecay.bin"
    renamed_material_decay_path.write_bytes(material_decay)
    assert server.parse_dump_file(renamed_material_decay_path)["type"] == "material_decay_blackbox"

    wake_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3)
    interactive_wake = bytearray(
        server.INTERACTIVE_WAKE_HEADER.pack(
            server.INTERACTIVE_WAKE_MAGIC,
            server.INTERACTIVE_WAKE_BLACKBOX_CAPACITY,
            2,
        )
    )
    interactive_wake.extend(bytearray(server.INTERACTIVE_WAKE_BLACKBOX_CAPACITY * server.INTERACTIVE_WAKE_ENTRY_BYTES))
    server.INTERACTIVE_WAKE_ENTRY.pack_into(
        interactive_wake,
        server.INTERACTIVE_WAKE_HEADER_BYTES,
        2401,
        2,
        8,
        1.0,
        -2.0,
        3.0,
        0.42,
        0.1,
        0.0,
        -0.1,
        2.5,
        1 << 2,
        0x574B0001,
        7,
        12,
        0.25,
        0.35,
    )
    server.INTERACTIVE_WAKE_ENTRY.pack_into(
        interactive_wake,
        server.INTERACTIVE_WAKE_HEADER_BYTES + server.INTERACTIVE_WAKE_ENTRY_BYTES,
        2402,
        4,
        12,
        4.0,
        -1.0,
        2.0,
        0.95,
        0.3,
        0.2,
        -0.2,
        3.25,
        wake_flags,
        0x574B0002,
        8,
        13,
        0.65,
        0.7,
    )
    interactive_wake_path = root / "Dump_INTERACTIVE_WAKE_VFX.bin"
    interactive_wake_path.write_bytes(interactive_wake)
    parsed_interactive_wake = server.parse_dump_file(interactive_wake_path)
    assert parsed_interactive_wake["type"] == "interactive_wake_blackbox"
    assert parsed_interactive_wake["magicHex"] == "0x57414B45"
    assert parsed_interactive_wake["entrySize"] == server.INTERACTIVE_WAKE_ENTRY_BYTES
    assert parsed_interactive_wake["declaredEntryCount"] == server.INTERACTIVE_WAKE_BLACKBOX_CAPACITY
    assert parsed_interactive_wake["nonEmptyEntryCount"] == 2
    assert parsed_interactive_wake["latest"]["frame"] == 2402
    assert parsed_interactive_wake["latest"]["stateHashHex"] == "0x574B0002"
    assert parsed_interactive_wake["latest"]["flagLabels"] == [
        "invalid-input",
        "nan",
        "budget-pressure",
        "thermal-pressure",
    ]
    assert "invalid_input" in parsed_interactive_wake["warnings"]
    assert "nan_flag" in parsed_interactive_wake["warnings"]
    assert "budget_pressure" in parsed_interactive_wake["warnings"]
    assert "thermal_pressure" in parsed_interactive_wake["warnings"]
    renamed_interactive_wake_path = root / "Renamed_InteractiveWake.h8dump"
    renamed_interactive_wake_path.write_bytes(interactive_wake)
    assert server.parse_dump_file(renamed_interactive_wake_path)["type"] == "interactive_wake_blackbox"

    flora_sway_flags = (1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7)
    flora_sway = bytearray(
        server.FLORA_SWAY_FIELD_HEADER.pack(
            server.FLORA_SWAY_FIELD_MAGIC,
            server.FLORA_SWAY_FIELD_BLACKBOX_CAPACITY,
            2,
        )
    )
    flora_sway.extend(bytearray(server.FLORA_SWAY_FIELD_BLACKBOX_CAPACITY * server.FLORA_SWAY_FIELD_ENTRY_BYTES))
    server.FLORA_SWAY_FIELD_ENTRY.pack_into(
        flora_sway,
        server.FLORA_SWAY_FIELD_HEADER_BYTES,
        2501,
        32,
        2,
        4096,
        1 << 3,
        12.0,
        -4.0,
        8.0,
        3.2,
        0.35,
        0.55,
        0.03333,
        0.2,
        0x46530001,
        11,
        18,
        750,
    )
    server.FLORA_SWAY_FIELD_ENTRY.pack_into(
        flora_sway,
        server.FLORA_SWAY_FIELD_HEADER_BYTES + server.FLORA_SWAY_FIELD_ENTRY_BYTES,
        2502,
        64,
        4,
        65536,
        flora_sway_flags,
        14.0,
        -3.0,
        9.5,
        3.8,
        1.2,
        0.75,
        0.05,
        0.6,
        0x46530002,
        12,
        19,
        1250,
    )
    flora_sway_path = root / "Dump_FLORA_SWAY_DIRECTOR.bin"
    flora_sway_path.write_bytes(flora_sway)
    parsed_flora_sway = server.parse_dump_file(flora_sway_path)
    assert parsed_flora_sway["type"] == "flora_sway_field_blackbox"
    assert parsed_flora_sway["magicHex"] == "0x46535759"
    assert parsed_flora_sway["entrySize"] == server.FLORA_SWAY_FIELD_ENTRY_BYTES
    assert parsed_flora_sway["declaredEntryCount"] == server.FLORA_SWAY_FIELD_BLACKBOX_CAPACITY
    assert parsed_flora_sway["nonEmptyEntryCount"] == 2
    assert parsed_flora_sway["latest"]["frame"] == 2502
    assert parsed_flora_sway["latest"]["stateHashHex"] == "0x46530002"
    assert parsed_flora_sway["latest"]["flagLabels"] == [
        "nan",
        "vault-missing",
        "upload-stall",
        "wrapped-shift",
        "full-reset",
        "discarded-upload",
    ]
    assert "nan_flag" in parsed_flora_sway["warnings"]
    assert "vault_missing" in parsed_flora_sway["warnings"]
    assert "upload_stall" in parsed_flora_sway["warnings"]
    assert "discarded_upload" in parsed_flora_sway["warnings"]
    renamed_flora_sway_path = root / "Renamed_FloraSwayField.bin"
    renamed_flora_sway_path.write_bytes(flora_sway)
    assert server.parse_dump_file(renamed_flora_sway_path)["type"] == "flora_sway_field_blackbox"

    flora_memory_flags_0 = 1 << 0
    flora_memory_flags_1 = (1 << 5) | (1 << 6) | (1 << 7)
    flora_memory_state_0 = (
        server.FLORA_MEMORY_TELEMETRY_EVENT_RESOLVE
        ^ 71652
        ^ 11
        ^ flora_memory_flags_0
    ) & 0xFFFFFFFF
    flora_memory_state_1 = (
        server.FLORA_MEMORY_TELEMETRY_EVENT_WRITE_LOCK
        ^ server.FLORA_MEMORY_TELEMETRY_BUFFER_ID
        ^ 12
        ^ flora_memory_flags_1
    ) & 0xFFFFFFFF
    flora_memory = bytearray(
        server.FLORA_MEMORY_TELEMETRY_HEADER.pack(
            server.FLORA_MEMORY_TELEMETRY_CAPACITY,
            2,
        )
    )
    flora_memory.extend(bytearray(server.FLORA_MEMORY_TELEMETRY_CAPACITY * server.FLORA_MEMORY_TELEMETRY_ENTRY_BYTES))
    server.FLORA_MEMORY_TELEMETRY_ENTRY.pack_into(
        flora_memory,
        server.FLORA_MEMORY_TELEMETRY_HEADER_BYTES,
        2701,
        server.FLORA_MEMORY_TELEMETRY_EVENT_RESOLVE,
        71652,
        4,
        11,
        300,
        0,
        flora_memory_flags_0,
        1,
        21,
        0.45,
        0.2,
        flora_memory_state_0,
        14,
        0,
        0,
    )
    server.FLORA_MEMORY_TELEMETRY_ENTRY.pack_into(
        flora_memory,
        server.FLORA_MEMORY_TELEMETRY_HEADER_BYTES + server.FLORA_MEMORY_TELEMETRY_ENTRY_BYTES,
        2702,
        server.FLORA_MEMORY_TELEMETRY_EVENT_WRITE_LOCK,
        server.FLORA_MEMORY_TELEMETRY_BUFFER_ID,
        4,
        12,
        server.FLORA_MEMORY_TELEMETRY_CAPACITY,
        128,
        flora_memory_flags_1,
        server.FLORA_MEMORY_TELEMETRY_DUMP_FAILURE_THRESHOLD,
        22,
        0.65,
        0.55,
        flora_memory_state_1,
        15,
        35,
        0,
    )
    flora_memory_path = root / "Dump_1327_FloraInteraction.bin"
    flora_memory_path.write_bytes(flora_memory)
    parsed_flora_memory = server.parse_dump_file(flora_memory_path)
    assert parsed_flora_memory["type"] == "flora_memory_telemetry_blackbox"
    assert parsed_flora_memory["entrySize"] == server.FLORA_MEMORY_TELEMETRY_ENTRY_BYTES
    assert parsed_flora_memory["declaredEntryCount"] == server.FLORA_MEMORY_TELEMETRY_CAPACITY
    assert parsed_flora_memory["nonEmptyEntryCount"] == 2
    assert parsed_flora_memory["latest"]["frame"] == 2702
    assert parsed_flora_memory["latest"]["eventLabel"] == "write-lock"
    assert parsed_flora_memory["latest"]["bufferLabel"] == "flora-memory-telemetry"
    assert parsed_flora_memory["latest"]["stateHashOk"] is True
    assert parsed_flora_memory["latest"]["flagLabels"] == [
        "invalid-buffer",
        "write-lock-failed",
        "nan",
    ]
    assert "missing_vault" in parsed_flora_memory["warnings"]
    assert "invalid_buffer" in parsed_flora_memory["warnings"]
    assert "write_lock_failed" in parsed_flora_memory["warnings"]
    assert "nan_flag" in parsed_flora_memory["warnings"]
    assert "consecutive_failure_threshold" in parsed_flora_memory["warnings"]
    flora_memory_h8dump_path = root / "Dump_1327_FloraInteraction.h8dump"
    flora_memory_h8dump_path.write_bytes(flora_memory)
    assert server.parse_dump_file(flora_memory_h8dump_path)["type"] == "flora_memory_telemetry_blackbox"

    ambient_sway_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    ambient_sway = bytearray(
        server.FLORA_AMBIENT_SWAY_HEADER.pack(
            server.FLORA_AMBIENT_SWAY_MAGIC,
            server.FLORA_AMBIENT_SWAY_VERSION,
            server.FLORA_AMBIENT_SWAY_SOURCE_HASH,
            server.FLORA_AMBIENT_SWAY_ENTRY_BYTES,
            server.FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY,
            2,
        )
    )
    ambient_sway.extend(bytearray(server.FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY * server.FLORA_AMBIENT_SWAY_ENTRY_BYTES))
    server.FLORA_AMBIENT_SWAY_ENTRY.pack_into(
        ambient_sway,
        server.FLORA_AMBIENT_SWAY_HEADER_BYTES,
        2801,
        1 << 3,
        0.25,
        0.6,
        0.5,
        0.12,
        0x53465701,
        server.FLORA_AMBIENT_SWAY_SOURCE_HASH,
    )
    server.FLORA_AMBIENT_SWAY_ENTRY.pack_into(
        ambient_sway,
        server.FLORA_AMBIENT_SWAY_HEADER_BYTES + server.FLORA_AMBIENT_SWAY_ENTRY_BYTES,
        2802,
        ambient_sway_flags,
        0.5,
        1.25,
        0.85,
        0.3,
        0x53465702,
        server.FLORA_AMBIENT_SWAY_SOURCE_HASH,
    )
    ambient_sway_path = root / "Dump_SHINOBU_267.bin"
    ambient_sway_path.write_bytes(ambient_sway)
    parsed_ambient_sway = server.parse_dump_file(ambient_sway_path)
    assert parsed_ambient_sway["type"] == "flora_ambient_sway_blackbox"
    assert parsed_ambient_sway["magicHex"] == "0x37363253"
    assert parsed_ambient_sway["sourceHashHex"] == "0x53465759"
    assert parsed_ambient_sway["entrySize"] == server.FLORA_AMBIENT_SWAY_ENTRY_BYTES
    assert parsed_ambient_sway["declaredEntryCount"] == server.FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY
    assert parsed_ambient_sway["nonEmptyEntryCount"] == 2
    assert parsed_ambient_sway["latest"]["frame"] == 2802
    assert parsed_ambient_sway["latest"]["sourceHashOk"] is True
    assert parsed_ambient_sway["latest"]["flagLabels"] == [
        "vault-missing",
        "constant-buffer-unsupported",
        "invalid-number",
        "upload-skipped",
        "burst-kernel-unavailable",
    ]
    assert "vault_missing" in parsed_ambient_sway["warnings"]
    assert "constant_buffer_unsupported" in parsed_ambient_sway["warnings"]
    assert "invalid_number" in parsed_ambient_sway["warnings"]
    assert "upload_skipped" in parsed_ambient_sway["warnings"]
    assert "burst_kernel_unavailable" in parsed_ambient_sway["warnings"]
    renamed_ambient_sway_path = root / "Renamed_AmbientSway.h8dump"
    renamed_ambient_sway_path.write_bytes(ambient_sway)
    assert server.parse_dump_file(renamed_ambient_sway_path)["type"] == "flora_ambient_sway_blackbox"

    vegetation_flags_0 = 1 << 0
    vegetation_flags_1 = (1 << 2) | (1 << 3) | (1 << 5) | (1 << 6)
    vegetation_state_0 = server.compute_vegetation_memory_state_hash(
        server.VEGETATION_MEMORY_TELEMETRY_RING_BUFFER_ID,
        31,
        2901,
        server.VEGETATION_MEMORY_TELEMETRY_CAPACITY,
        server.VEGETATION_MEMORY_TELEMETRY_CAPACITY,
        0,
        0.0,
        0.75,
        1,
        1,
        vegetation_flags_0,
        0.0,
        0.0,
        0.0,
    )
    vegetation_state_1 = server.compute_vegetation_memory_state_hash(
        server.VEGETATION_MEMORY_TELEMETRY_CURSOR_BUFFER_ID,
        32,
        2902,
        256,
        128,
        17,
        420.0,
        0.55,
        8,
        4,
        vegetation_flags_1,
        10.0,
        -2.0,
        4.0,
    )
    vegetation_memory = bytearray(
        server.VEGETATION_MEMORY_HEADER.pack(
            server.VEGETATION_MEMORY_MAGIC,
            server.VEGETATION_MEMORY_VERSION,
            server.VEGETATION_MEMORY_TELEMETRY_CAPACITY,
            server.VEGETATION_MEMORY_ENTRY_BYTES,
            2,
        )
    )
    vegetation_memory.extend(bytearray(server.VEGETATION_MEMORY_TELEMETRY_CAPACITY * server.VEGETATION_MEMORY_ENTRY_BYTES))
    server.VEGETATION_MEMORY_ENTRY.pack_into(
        vegetation_memory,
        server.VEGETATION_MEMORY_HEADER_BYTES,
        vegetation_state_0,
        server.VEGETATION_MEMORY_TELEMETRY_RING_BUFFER_ID,
        31,
        2901,
        server.VEGETATION_MEMORY_TELEMETRY_CAPACITY,
        server.VEGETATION_MEMORY_TELEMETRY_CAPACITY,
        0,
        0.0,
        0.75,
        1,
        1,
        vegetation_flags_0,
        0.0,
        0.0,
        0.0,
        0,
    )
    server.VEGETATION_MEMORY_ENTRY.pack_into(
        vegetation_memory,
        server.VEGETATION_MEMORY_HEADER_BYTES + server.VEGETATION_MEMORY_ENTRY_BYTES,
        vegetation_state_1,
        server.VEGETATION_MEMORY_TELEMETRY_CURSOR_BUFFER_ID,
        32,
        2902,
        256,
        128,
        17,
        420.0,
        0.55,
        8,
        4,
        vegetation_flags_1,
        10.0,
        -2.0,
        4.0,
        0,
    )
    vegetation_memory_path = root / "Dump_1316_Vegetation.bin"
    vegetation_memory_path.write_bytes(vegetation_memory)
    parsed_vegetation_memory = server.parse_dump_file(vegetation_memory_path)
    assert parsed_vegetation_memory["type"] == "vegetation_memory_blackbox"
    assert parsed_vegetation_memory["magicHex"] == "0x313331365F564547"
    assert parsed_vegetation_memory["entrySize"] == server.VEGETATION_MEMORY_ENTRY_BYTES
    assert parsed_vegetation_memory["declaredEntryCount"] == server.VEGETATION_MEMORY_TELEMETRY_CAPACITY
    assert parsed_vegetation_memory["nonEmptyEntryCount"] == 2
    assert parsed_vegetation_memory["latest"]["frame"] == 2902
    assert parsed_vegetation_memory["latest"]["stateHashOk"] is True
    assert parsed_vegetation_memory["latest"]["failureCodeLabel"] == "staging-capacity-exceeded"
    assert parsed_vegetation_memory["latest"]["phaseLabel"] == "defrag"
    assert parsed_vegetation_memory["latest"]["bufferLabel"] == "vegetation-memory-telemetry-cursor"
    assert parsed_vegetation_memory["latest"]["flagLabels"] == [
        "lock-contention",
        "stale-handle",
        "capacity",
        "compaction-fence",
    ]
    assert "cold_boot" in parsed_vegetation_memory["warnings"]
    assert "lock_contention" in parsed_vegetation_memory["warnings"]
    assert "stale_handle" in parsed_vegetation_memory["warnings"]
    assert "capacity_flag" in parsed_vegetation_memory["warnings"]
    assert "compaction_fence" in parsed_vegetation_memory["warnings"]
    assert "actual_length_below_expected" in parsed_vegetation_memory["warnings"]
    renamed_vegetation_memory_path = root / "Renamed_VegetationMemory.h8dump"
    renamed_vegetation_memory_path.write_bytes(vegetation_memory)
    assert server.parse_dump_file(renamed_vegetation_memory_path)["type"] == "vegetation_memory_blackbox"

    organics_flags = (1 << 2) | (1 << 5) | (1 << 6) | (1 << 7)
    organics_hash_0 = server.compute_dear_lie_organics_hash(3001, 12, 3, 0x0DD10001, 180.0)
    organics_hash_1 = server.compute_dear_lie_organics_hash(3002, 24, 6, 0x0DD10002, 280.0)
    organics = bytearray(server.DEAR_LIE_ORGANICS_TELEMETRY_CAPACITY * server.DEAR_LIE_ORGANICS_ENTRY_BYTES)
    server.DEAR_LIE_ORGANICS_ENTRY.pack_into(
        organics,
        0,
        3001,
        420,
        96,
        12,
        3,
        2,
        4,
        1,
        0,
        0,
        0.7,
        organics_hash_0,
        0x0DD10001,
        180.0,
        0,
    )
    server.DEAR_LIE_ORGANICS_ENTRY.pack_into(
        organics,
        server.DEAR_LIE_ORGANICS_ENTRY_BYTES,
        3002,
        512,
        128,
        24,
        6,
        5,
        8,
        2,
        3,
        1,
        0.6,
        organics_hash_1,
        0x0DD10002,
        280.0,
        organics_flags,
    )
    organics_path = root / "Dump_1318_Organics.bin"
    organics_path.write_bytes(organics)
    parsed_organics = server.parse_dump_file(organics_path)
    assert parsed_organics["type"] == "dear_lie_organics_blackbox"
    assert parsed_organics["entrySize"] == server.DEAR_LIE_ORGANICS_ENTRY_BYTES
    assert parsed_organics["declaredEntryCount"] == server.DEAR_LIE_ORGANICS_TELEMETRY_CAPACITY
    assert parsed_organics["nonEmptyEntryCount"] == 2
    assert parsed_organics["latest"]["frame"] == 3002
    assert parsed_organics["latest"]["hashOk"] is True
    assert parsed_organics["latest"]["lastInstanceUidHex"] == "0x0DD10002"
    assert parsed_organics["latest"]["flagLabels"] == [
        "regeneration-recovered",
        "guard-failed",
        "drop-drain-failed",
        "overflow-or-reject",
    ]
    assert "regeneration_recovered" in parsed_organics["warnings"]
    assert "guard_failed" in parsed_organics["warnings"]
    assert "drop_drain_failed" in parsed_organics["warnings"]
    assert "overflow_or_reject" in parsed_organics["warnings"]
    assert "rejected_signals" in parsed_organics["warnings"]
    assert "nan_rejects" in parsed_organics["warnings"]
    organics_h8dump_path = root / "Dump_1318_Organics.h8dump"
    organics_h8dump_path.write_bytes(organics)
    assert server.parse_dump_file(organics_h8dump_path)["type"] == "dear_lie_organics_blackbox"

    chemical_flags = 1 << 0
    chemical_hash_0 = server.compute_chemical_influence_state_hash(3101, 12, 2, 4, 0.45, 0.8, 0)
    chemical_hash_1 = server.compute_chemical_influence_state_hash(3102, 161, 9, 7, 0.9, 0.6, chemical_flags)
    chemical = bytearray(
        server.CHEMICAL_INFLUENCE_HEADER.pack(
            server.CHEMICAL_INFLUENCE_MAGIC,
            server.CHEMICAL_INFLUENCE_VERSION,
            server.CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY,
            server.CHEMICAL_INFLUENCE_ENTRY_BYTES,
        )
    )
    chemical.extend(bytearray(server.CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY * server.CHEMICAL_INFLUENCE_ENTRY_BYTES))
    server.CHEMICAL_INFLUENCE_ENTRY.pack_into(
        chemical,
        server.CHEMICAL_INFLUENCE_HEADER_BYTES,
        120.0,
        -16.0,
        48.0,
        0.45,
        140.0,
        3101,
        12,
        2,
        4,
        chemical_hash_0,
        0,
        0.8,
        3,
    )
    server.CHEMICAL_INFLUENCE_ENTRY.pack_into(
        chemical,
        server.CHEMICAL_INFLUENCE_HEADER_BYTES + server.CHEMICAL_INFLUENCE_ENTRY_BYTES,
        124.0,
        -12.0,
        52.0,
        0.9,
        240.0,
        3102,
        161,
        9,
        7,
        chemical_hash_1 ^ 0x55,
        chemical_flags,
        0.6,
        6,
    )
    chemical_path = root / "Dump_CHEMISTRY_SURGEON.bin"
    chemical_path.write_bytes(chemical)
    parsed_chemical = server.parse_dump_file(chemical_path)
    assert parsed_chemical["type"] == "chemical_influence_blackbox"
    assert parsed_chemical["magicHex"] == "0x3833315F4D454843"
    assert parsed_chemical["entrySize"] == server.CHEMICAL_INFLUENCE_ENTRY_BYTES
    assert parsed_chemical["declaredEntryCount"] == server.CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY
    assert parsed_chemical["nonEmptyEntryCount"] == 2
    assert parsed_chemical["latest"]["frame"] == 3102
    assert parsed_chemical["latest"]["stateHashOk"] is False
    assert parsed_chemical["latest"]["flagLabels"] == ["nan"]
    assert parsed_chemical["latest"]["activeEmitters"] == 161
    assert "nan_flag" in parsed_chemical["warnings"]
    assert "state_hash_mismatch" in parsed_chemical["warnings"]
    assert "emitter_count_out_of_range" in parsed_chemical["warnings"]
    assert "iterations_out_of_range" in parsed_chemical["warnings"]
    renamed_chemical_path = root / "Renamed_ChemistrySurgeon.h8dump"
    renamed_chemical_path.write_bytes(chemical)
    assert server.parse_dump_file(renamed_chemical_path)["type"] == "chemical_influence_blackbox"

    food_chain_flags_0 = (1 << 0) | (1 << 1)
    food_chain_flags_1 = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 31)
    food_chain = bytearray(
        server.SARGASSUM_FOOD_CHAIN_HEADER.pack(
            server.SARGASSUM_FOOD_CHAIN_MAGIC_LOW,
            server.SARGASSUM_FOOD_CHAIN_MAGIC_HIGH,
            server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES,
            server.SARGASSUM_FOOD_CHAIN_CAPACITY,
            2,
            0xEFC00002,
        )
    )
    food_chain.extend(
        bytearray(server.SARGASSUM_FOOD_CHAIN_CAPACITY * server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES)
    )
    server.SARGASSUM_FOOD_CHAIN_ENTRY.pack_into(
        food_chain,
        server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES,
        3301,
        0xF00D1001,
        0xCAFE0001,
        food_chain_flags_0,
        120,
        2,
        3,
        1,
        10.0,
        -2.0,
        5.5,
        11.0,
        -1.5,
        6.0,
        0,
        10.5,
    )
    server.SARGASSUM_FOOD_CHAIN_ENTRY.pack_into(
        food_chain,
        server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES + server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES,
        3302,
        0,
        0xCAFE0002,
        food_chain_flags_1,
        80,
        96,
        9,
        3,
        float("nan"),
        -2.0,
        5.5,
        12.0,
        -1.0,
        7.0,
        0xEFC00002,
        11.0,
    )
    food_chain_path = root / "Dump_SARGASSUM_FOOD_CHAIN.bin"
    food_chain_path.write_bytes(food_chain)
    parsed_food_chain = server.parse_dump_file(food_chain_path)
    assert parsed_food_chain["type"] == "sargassum_food_chain_blackbox"
    assert parsed_food_chain["magicLowHex"] == "0x48454354"
    assert parsed_food_chain["magicHighHex"] == "0x4643484E"
    assert parsed_food_chain["entrySize"] == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
    assert parsed_food_chain["declaredEntryCount"] == server.SARGASSUM_FOOD_CHAIN_CAPACITY
    assert parsed_food_chain["nonEmptyEntryCount"] == 2
    assert parsed_food_chain["entries"][0]["pendingKillJob"] == 3
    assert parsed_food_chain["entries"][0]["flagLabels"] == ["tick", "kill-job-scheduled"]
    assert parsed_food_chain["latest"]["frame"] == 3302
    assert parsed_food_chain["latest"]["flagLabels"] == [
        "tick",
        "kill-job-completed",
        "kill-drained",
        "whale-fall",
        "boids-scattered",
        "nonfinite",
    ]
    assert parsed_food_chain["latest"]["sourceHashHex"] == "0xCAFE0002"
    assert parsed_food_chain["latest"]["entryAnomalyHashHex"] == "0xEFC00002"
    assert "anomaly_hash" in parsed_food_chain["warnings"]
    assert "entry_anomaly_hash" in parsed_food_chain["warnings"]
    assert "nonfinite_values" in parsed_food_chain["warnings"]
    assert "nonfinite_flag" in parsed_food_chain["warnings"]
    assert "state_hash_zero" in parsed_food_chain["warnings"]
    assert "consumed_count_out_of_range" in parsed_food_chain["warnings"]
    assert "pending_kill_job_out_of_range" in parsed_food_chain["warnings"]
    assert "lod_tier_out_of_range" in parsed_food_chain["warnings"]
    assert "kill_job_scheduled" in parsed_food_chain["warnings"]
    assert "kill_job_completed" in parsed_food_chain["warnings"]
    assert "kill_drained" in parsed_food_chain["warnings"]
    assert "whale_fall" in parsed_food_chain["warnings"]
    assert "boids_scattered" in parsed_food_chain["warnings"]
    renamed_food_chain_path = root / "Renamed_SargassumFoodChain.h8dump"
    renamed_food_chain_path.write_bytes(food_chain)
    assert server.parse_dump_file(renamed_food_chain_path)["type"] == "sargassum_food_chain_blackbox"

    truncated_food_chain_path = root / "Interrupted_SargassumFoodChain.h8dump"
    truncated_food_chain_path.write_bytes(food_chain[: server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES + 7])
    parsed_truncated_food_chain = server.parse_dump_file(truncated_food_chain_path)
    assert parsed_truncated_food_chain["type"] == "sargassum_food_chain_blackbox"
    assert parsed_truncated_food_chain["nonEmptyEntryCount"] == 0
    assert "payload_truncated" in parsed_truncated_food_chain["warnings"]
    assert "trailing_partial_entry" in parsed_truncated_food_chain["warnings"]

    invalid_food_chain_path = root / "Dump_SARGASSUM_FOOD_CHAIN.h8dump"
    invalid_food_chain = server.SARGASSUM_FOOD_CHAIN_HEADER.pack(
        server.SARGASSUM_FOOD_CHAIN_MAGIC_LOW,
        server.SARGASSUM_FOOD_CHAIN_MAGIC_HIGH,
        server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES + 4,
        server.SARGASSUM_FOOD_CHAIN_CAPACITY,
        server.SARGASSUM_FOOD_CHAIN_CAPACITY + 1,
        0xFC0000AD,
    )
    invalid_food_chain_path.write_bytes(invalid_food_chain)
    parsed_invalid_food_chain = server.parse_dump_file(invalid_food_chain_path)
    assert parsed_invalid_food_chain["type"] == "sargassum_food_chain_blackbox"
    assert parsed_invalid_food_chain["headerBytes"] == server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES
    assert parsed_invalid_food_chain["entrySize"] == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES + 4
    assert parsed_invalid_food_chain["declaredEntryCount"] == server.SARGASSUM_FOOD_CHAIN_CAPACITY
    assert parsed_invalid_food_chain["capacity"] == server.SARGASSUM_FOOD_CHAIN_CAPACITY
    assert parsed_invalid_food_chain["telemetryCursor"] == server.SARGASSUM_FOOD_CHAIN_CAPACITY + 1
    assert parsed_invalid_food_chain["nonEmptyEntryCount"] == 0
    assert parsed_invalid_food_chain["returnedEntryCount"] == 0
    assert parsed_invalid_food_chain["entries"] == []
    assert parsed_invalid_food_chain["latest"] is None
    assert parsed_invalid_food_chain["warnings"] == ["invalid_header"]
    renamed_invalid_food_chain_path = root / "CopiedInvalidSargassumFoodChain.h8dump"
    renamed_invalid_food_chain_path.write_bytes(invalid_food_chain)
    parsed_renamed_invalid_food_chain = server.parse_dump_file(renamed_invalid_food_chain_path)
    assert parsed_renamed_invalid_food_chain["type"] == "sargassum_food_chain_blackbox"
    assert parsed_renamed_invalid_food_chain["warnings"] == ["invalid_header"]
    no_data_food_chain_dir = root / "NoDataFoodChain"
    no_data_food_chain_dir.mkdir()
    no_data_food_chain_path = no_data_food_chain_dir / "Dump_SARGASSUM_FOOD_CHAIN.h8dump"
    no_data_food_chain_path.write_bytes(b"")
    parsed_no_data_food_chain = server.parse_dump_file(no_data_food_chain_path)
    assert parsed_no_data_food_chain["type"] == "sargassum_food_chain_blackbox"
    assert parsed_no_data_food_chain["headerBytes"] == server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES
    assert parsed_no_data_food_chain["entrySize"] == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
    assert parsed_no_data_food_chain["declaredEntryCount"] == 0
    assert parsed_no_data_food_chain["capacity"] == 0
    assert parsed_no_data_food_chain["telemetryCursor"] == 0
    assert parsed_no_data_food_chain["nonEmptyEntryCount"] == 0
    assert parsed_no_data_food_chain["returnedEntryCount"] == 0
    assert parsed_no_data_food_chain["entries"] == []
    assert parsed_no_data_food_chain["latest"] is None
    assert parsed_no_data_food_chain["warnings"] == ["truncated_header"]

    sargassum_flags_0 = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3)
    sargassum_flags_1 = (1 << 0) | (1 << 2) | (1 << 31)
    sargassum_sensory = bytearray(
        server.SARGASSUM_BOID_SENSORY_HEADER.pack(
            server.SARGASSUM_BOID_SENSORY_MAGIC_LOW,
            server.SARGASSUM_BOID_SENSORY_MAGIC_HIGH,
            server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES,
            server.SARGASSUM_BOID_SENSORY_CAPACITY,
            2,
            0xB01D0002,
        )
    )
    sargassum_sensory.extend(
        bytearray(server.SARGASSUM_BOID_SENSORY_CAPACITY * server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES)
    )
    server.SARGASSUM_BOID_SENSORY_ENTRY.pack_into(
        sargassum_sensory,
        server.SARGASSUM_BOID_SENSORY_HEADER_BYTES,
        3201,
        0xB01D1001,
        sargassum_flags_0,
        3,
        0.0,
        0.0,
        0.0,
        32.0,
        4.0,
        0.0,
        0.0,
        24.0,
        12.0,
        0.0,
        0.0,
        1.0,
    )
    server.SARGASSUM_BOID_SENSORY_ENTRY.pack_into(
        sargassum_sensory,
        server.SARGASSUM_BOID_SENSORY_HEADER_BYTES + server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES,
        3202,
        0xB01D1002,
        sargassum_flags_1,
        17,
        float("nan"),
        0.0,
        0.0,
        32.0,
        0.0,
        0.0,
        0.0,
        0.0,
        12.0,
        0.0,
        0.0,
        2.0,
    )
    sargassum_sensory_path = root / "Dump_SARGASSUM_BOID_SENSORY.bin"
    sargassum_sensory_path.write_bytes(sargassum_sensory)
    parsed_sargassum_sensory = server.parse_dump_file(sargassum_sensory_path)
    assert parsed_sargassum_sensory["type"] == "sargassum_boid_sensory_blackbox"
    assert parsed_sargassum_sensory["magicLowHex"] == "0x424F4944"
    assert parsed_sargassum_sensory["magicHighHex"] == "0x53454E53"
    assert parsed_sargassum_sensory["entrySize"] == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES
    assert parsed_sargassum_sensory["declaredEntryCount"] == server.SARGASSUM_BOID_SENSORY_CAPACITY
    assert parsed_sargassum_sensory["nonEmptyEntryCount"] == 2
    assert parsed_sargassum_sensory["latest"]["frame"] == 3202
    assert parsed_sargassum_sensory["latest"]["flagLabels"] == ["tick", "ping-active", "nonfinite"]
    assert parsed_sargassum_sensory["latest"]["activeThreatCount"] == 17
    assert "anomaly_hash" in parsed_sargassum_sensory["warnings"]
    assert "nonfinite_values" in parsed_sargassum_sensory["warnings"]
    assert "nonfinite_flag" in parsed_sargassum_sensory["warnings"]
    assert "active_threat_count_out_of_range" in parsed_sargassum_sensory["warnings"]
    renamed_sargassum_sensory_path = root / "Renamed_SargassumBoidSensory.h8dump"
    renamed_sargassum_sensory_path.write_bytes(sargassum_sensory)
    assert server.parse_dump_file(renamed_sargassum_sensory_path)["type"] == "sargassum_boid_sensory_blackbox"

    truncated_sargassum_sensory_path = root / "Interrupted_SargassumBoidSensory.h8dump"
    truncated_sargassum_sensory_path.write_bytes(
        sargassum_sensory[: server.SARGASSUM_BOID_SENSORY_HEADER_BYTES + 11]
    )
    parsed_truncated_sargassum_sensory = server.parse_dump_file(truncated_sargassum_sensory_path)
    assert parsed_truncated_sargassum_sensory["type"] == "sargassum_boid_sensory_blackbox"
    assert parsed_truncated_sargassum_sensory["nonEmptyEntryCount"] == 0
    assert "payload_truncated" in parsed_truncated_sargassum_sensory["warnings"]
    assert "trailing_partial_entry" in parsed_truncated_sargassum_sensory["warnings"]

    invalid_sargassum_sensory_path = root / "Dump_SARGASSUM_BOID_SENSORY.h8dump"
    invalid_sargassum_sensory = server.SARGASSUM_BOID_SENSORY_HEADER.pack(
        server.SARGASSUM_BOID_SENSORY_MAGIC_LOW,
        server.SARGASSUM_BOID_SENSORY_MAGIC_HIGH,
        server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES + 8,
        server.SARGASSUM_BOID_SENSORY_CAPACITY + 1,
        1,
        0xB01D00AD,
    )
    invalid_sargassum_sensory_path.write_bytes(invalid_sargassum_sensory)
    parsed_invalid_sargassum_sensory = server.parse_dump_file(invalid_sargassum_sensory_path)
    assert parsed_invalid_sargassum_sensory["type"] == "sargassum_boid_sensory_blackbox"
    assert parsed_invalid_sargassum_sensory["headerBytes"] == server.SARGASSUM_BOID_SENSORY_HEADER_BYTES
    assert parsed_invalid_sargassum_sensory["entrySize"] == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES + 8
    assert parsed_invalid_sargassum_sensory["declaredEntryCount"] == server.SARGASSUM_BOID_SENSORY_CAPACITY + 1
    assert parsed_invalid_sargassum_sensory["capacity"] == server.SARGASSUM_BOID_SENSORY_CAPACITY + 1
    assert parsed_invalid_sargassum_sensory["nonEmptyEntryCount"] == 0
    assert parsed_invalid_sargassum_sensory["returnedEntryCount"] == 0
    assert parsed_invalid_sargassum_sensory["entries"] == []
    assert parsed_invalid_sargassum_sensory["latest"] is None
    assert parsed_invalid_sargassum_sensory["warnings"] == ["invalid_header"]
    renamed_invalid_sargassum_sensory_path = root / "CopiedInvalidSargassumBoidSensory.h8dump"
    renamed_invalid_sargassum_sensory_path.write_bytes(invalid_sargassum_sensory)
    parsed_renamed_invalid_sargassum_sensory = server.parse_dump_file(renamed_invalid_sargassum_sensory_path)
    assert parsed_renamed_invalid_sargassum_sensory["type"] == "sargassum_boid_sensory_blackbox"
    assert parsed_renamed_invalid_sargassum_sensory["warnings"] == ["invalid_header"]
    no_data_sargassum_sensory_dir = root / "NoDataBoidSensory"
    no_data_sargassum_sensory_dir.mkdir()
    no_data_sargassum_sensory_path = no_data_sargassum_sensory_dir / "Dump_SARGASSUM_BOID_SENSORY.h8dump"
    no_data_sargassum_sensory_path.write_bytes(b"")
    parsed_no_data_sargassum_sensory = server.parse_dump_file(no_data_sargassum_sensory_path)
    assert parsed_no_data_sargassum_sensory["type"] == "sargassum_boid_sensory_blackbox"
    assert parsed_no_data_sargassum_sensory["headerBytes"] == server.SARGASSUM_BOID_SENSORY_HEADER_BYTES
    assert parsed_no_data_sargassum_sensory["entrySize"] == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES
    assert parsed_no_data_sargassum_sensory["declaredEntryCount"] == 0
    assert parsed_no_data_sargassum_sensory["capacity"] == 0
    assert parsed_no_data_sargassum_sensory["telemetryCursor"] == 0
    assert parsed_no_data_sargassum_sensory["nonEmptyEntryCount"] == 0
    assert parsed_no_data_sargassum_sensory["returnedEntryCount"] == 0
    assert parsed_no_data_sargassum_sensory["entries"] == []
    assert parsed_no_data_sargassum_sensory["latest"] is None
    assert parsed_no_data_sargassum_sensory["warnings"] == ["truncated_header"]

    marine_snow_flags = (1 << 0) | (1 << 1)
    marine_snow = server.MARINE_SNOW_VFX_HEADER.pack(
        server.MARINE_SNOW_VFX_CONTEXT_HASH,
        server.MARINE_SNOW_VFX_TELEMETRY_CAPACITY,
        server.MARINE_SNOW_VFX_ENTRY_BYTES,
        2,
    )
    marine_snow += server.MARINE_SNOW_VFX_ENTRY.pack(
        2051,
        448,
        1792,
        2,
        0.35,
        0.25,
        1.2,
        0.01,
        10.0,
        -4.0,
        25.0,
        0.4,
        0,
        0x4D534E31,
        700,
        41,
    )
    marine_snow += server.MARINE_SNOW_VFX_ENTRY.pack(
        2052,
        1792,
        1792,
        4,
        0.85,
        0.92,
        3.75,
        0.04,
        11.0,
        -3.5,
        26.0,
        1.2,
        marine_snow_flags,
        0x4D534E32,
        1700,
        42,
    )
    marine_snow_path = root / "Dump_SILT_VFX.h8dump"
    marine_snow_path.write_bytes(marine_snow)
    parsed_marine_snow = server.parse_dump_file(marine_snow_path)
    assert parsed_marine_snow["type"] == "marine_snow_vfx_blackbox"
    assert parsed_marine_snow["contextHashHex"] == "0x4D534E57"
    assert parsed_marine_snow["entrySize"] == server.MARINE_SNOW_VFX_ENTRY_BYTES
    assert parsed_marine_snow["declaredEntryCount"] == 2
    assert parsed_marine_snow["nonEmptyEntryCount"] == 2
    assert parsed_marine_snow["latest"]["frame"] == 2052
    assert parsed_marine_snow["latest"]["stateHashHex"] == "0x4D534E32"
    assert parsed_marine_snow["latest"]["flagLabels"] == ["nonfinite", "gpu-budget-exceeded"]
    assert "nonfinite_flag" in parsed_marine_snow["warnings"]
    assert "gpu_budget_exceeded" in parsed_marine_snow["warnings"]
    assert "gpu_over_1500us" in parsed_marine_snow["warnings"]
    renamed_marine_snow_path = root / "Renamed_SiltVfx.bin"
    renamed_marine_snow_path.write_bytes(marine_snow)
    assert server.parse_dump_file(renamed_marine_snow_path)["type"] == "marine_snow_vfx_blackbox"

    propwash_flags = (1 << 0) | (1 << 1) | (1 << 2)
    propwash = server.PROPWASH_GPU_HEADER.pack(
        server.PROPWASH_GPU_LAYOUT_HASH,
        server.PROPWASH_GPU_TELEMETRY_CAPACITY,
        server.PROPWASH_GPU_ENTRY_BYTES,
        2,
    )
    propwash += server.PROPWASH_GPU_ENTRY.pack(
        2101,
        24,
        server.PROPWASH_GPU_MIN_PARTICLE_BUDGET,
        0,
        0.25,
        0.8,
        350.0,
        1.5,
        0.1,
        -0.2,
        -3.0,
        0x50525731,
        1 << 0,
        24,
        0x933B5BDE,
        0,
    )
    propwash += server.PROPWASH_GPU_ENTRY.pack(
        2102,
        128,
        server.PROPWASH_GPU_MAX_PARTICLE_BUDGET,
        3,
        0.75,
        1.4,
        1250.0,
        0.4,
        1.0,
        2.0,
        -3.0,
        0x50525732,
        propwash_flags,
        128,
        0x933B5BDE,
        0,
    )
    propwash_path = root / "Dump_PROPWASH_GPU.h8dump"
    propwash_path.write_bytes(propwash)
    parsed_propwash = server.parse_dump_file(propwash_path)
    assert parsed_propwash["type"] == "propwash_gpu_blackbox"
    assert parsed_propwash["layoutHashHex"] == "0x53483237"
    assert parsed_propwash["entrySize"] == server.PROPWASH_GPU_ENTRY_BYTES
    assert parsed_propwash["declaredEntryCount"] == 2
    assert parsed_propwash["nonEmptyEntryCount"] == 2
    assert parsed_propwash["latest"]["frame"] == 2102
    assert parsed_propwash["latest"]["stateHashHex"] == "0x50525732"
    assert parsed_propwash["latest"]["flagLabels"] == [
        "mock-source",
        "vehicle-wake-source",
        "wake-source-bridge",
    ]
    assert parsed_propwash["latest"]["profileHashHex"] == "0x933B5BDE"
    assert "overflow_count" in parsed_propwash["warnings"]
    assert "gpu_over_1000us" in parsed_propwash["warnings"]
    assert "mock_source" in parsed_propwash["warnings"]
    renamed_propwash_path = root / "Renamed_PropwashGpu.bin"
    renamed_propwash_path.write_bytes(propwash)
    assert server.parse_dump_file(renamed_propwash_path)["type"] == "propwash_gpu_blackbox"

    debris_flags = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    carve_debris = bytearray(
        server.CARVE_DEBRIS_HEADER.pack(
            server.CARVE_DEBRIS_MAGIC,
            server.CARVE_DEBRIS_BLACKBOX_CAPACITY,
            server.CARVE_DEBRIS_ENTRY_BYTES,
            0,
            debris_flags,
        )
    )
    carve_debris.extend(bytearray(server.CARVE_DEBRIS_BLACKBOX_CAPACITY * server.CARVE_DEBRIS_ENTRY_BYTES))
    server.CARVE_DEBRIS_ENTRY.pack_into(
        carve_debris,
        server.CARVE_DEBRIS_HEADER_BYTES,
        2201,
        10,
        1,
        16,
        1 << 2,
        0xDEB10001,
        0.1,
        0.0,
        -0.05,
        128,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    server.CARVE_DEBRIS_ENTRY.pack_into(
        carve_debris,
        server.CARVE_DEBRIS_HEADER_BYTES + server.CARVE_DEBRIS_ENTRY_BYTES,
        2202,
        120,
        3,
        64,
        debris_flags,
        0xDEB10002,
        0.25,
        -0.125,
        0.5,
        230,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    carve_debris_path = root / "Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump"
    carve_debris_path.write_bytes(carve_debris)
    parsed_carve_debris = server.parse_dump_file(carve_debris_path)
    assert parsed_carve_debris["type"] == "carve_debris_blackbox"
    assert parsed_carve_debris["entrySize"] == server.CARVE_DEBRIS_ENTRY_BYTES
    assert parsed_carve_debris["declaredEntryCount"] == server.CARVE_DEBRIS_BLACKBOX_CAPACITY
    assert parsed_carve_debris["nonEmptyEntryCount"] == 2
    assert parsed_carve_debris["latest"]["frame"] == 2202
    assert parsed_carve_debris["latest"]["stateHashHex"] == "0xDEB10002"
    assert parsed_carve_debris["latest"]["flagLabels"] == [
        "invalid-state",
        "sdf-active",
        "flow-active",
        "stress-recycle",
        "wake-active",
    ]
    assert parsed_carve_debris["reasonFlagLabels"] == [
        "invalid-state",
        "sdf-active",
        "flow-active",
        "stress-recycle",
        "wake-active",
    ]
    assert parsed_carve_debris["latest"]["qualityPressureQ8"] == 230
    assert "reason_flags" in parsed_carve_debris["warnings"]
    assert "invalid_state" in parsed_carve_debris["warnings"]
    assert "stress_recycle" in parsed_carve_debris["warnings"]
    renamed_carve_debris_path = root / "Renamed_CarveDebris.bin"
    renamed_carve_debris_path.write_bytes(carve_debris)
    assert server.parse_dump_file(renamed_carve_debris_path)["type"] == "carve_debris_blackbox"

    biolum_flags = (1 << 0) | (1 << 1) | (1 << 2)
    biolum = server.BIOLUM_PULSE_HEADER.pack(
        server.BIOLUM_PULSE_MAGIC,
        biolum_flags,
        0,
        server.BIOLUM_PULSE_ENTRY_BYTES,
        2,
        2,
    )
    biolum += server.BIOLUM_PULSE_ENTRY.pack(
        2301,
        1200,
        0.04,
        0.35,
        1.25,
        1.1,
        2.5,
        2,
        180,
        1 << 1,
        bytes(32),
    )
    biolum += server.BIOLUM_PULSE_ENTRY.pack(
        2302,
        2400,
        0.15,
        0.8,
        2.5,
        3.2,
        7.5,
        4,
        220,
        biolum_flags,
        bytes(32),
    )
    biolum_path = root / "Dump_SHINOBU_238.bin"
    biolum_path.write_bytes(biolum)
    parsed_biolum = server.parse_dump_file(biolum_path)
    assert parsed_biolum["type"] == "biolum_pulse_blackbox"
    assert parsed_biolum["entrySize"] == server.BIOLUM_PULSE_ENTRY_BYTES
    assert parsed_biolum["declaredEntryCount"] == 2
    assert parsed_biolum["nonEmptyEntryCount"] == 2
    assert parsed_biolum["latest"]["frame"] == 2302
    assert parsed_biolum["latest"]["flagLabels"] == ["nonfinite", "job-overrun", "aup-invalid"]
    assert parsed_biolum["reasonFlagLabels"] == ["nonfinite", "job-overrun", "aup-invalid"]
    assert parsed_biolum["latest"]["qualityTier"] == 220
    assert "reason_flags" in parsed_biolum["warnings"]
    assert "nonfinite_flag" in parsed_biolum["warnings"]
    assert "job_overrun" in parsed_biolum["warnings"]
    assert "aup_invalid" in parsed_biolum["warnings"]
    assert "oscillator_over_0_1ms" in parsed_biolum["warnings"]
    renamed_biolum_path = root / "Renamed_BiolumPulse.h8dump"
    renamed_biolum_path.write_bytes(biolum)
    assert server.parse_dump_file(renamed_biolum_path)["type"] == "biolum_pulse_blackbox"

    biolum_director_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)
    biolum_director_reason = (1 << 1) | (1 << 3)
    biolum_director = bytearray(
        server.BIOLUM_DIRECTOR_HEADER.pack(
            server.BIOLUM_DIRECTOR_MAGIC,
            2,
            biolum_director_reason,
            server.BIOLUM_DIRECTOR_TELEMETRY_CAPACITY,
        )
    )
    biolum_director.extend(bytearray(server.BIOLUM_DIRECTOR_TELEMETRY_CAPACITY * server.BIOLUM_DIRECTOR_ENTRY_BYTES))
    server.BIOLUM_DIRECTOR_ENTRY.pack_into(
        biolum_director,
        server.BIOLUM_DIRECTOR_HEADER_BYTES,
        2601,
        10.0,
        -40.0,
        25.0,
        0.45,
        0.2,
        0.9,
        1,
        2,
        1 << 0,
    )
    server.BIOLUM_DIRECTOR_ENTRY.pack_into(
        biolum_director,
        server.BIOLUM_DIRECTOR_HEADER_BYTES + server.BIOLUM_DIRECTOR_ENTRY_BYTES,
        2602,
        11.0,
        -41.0,
        26.0,
        0.85,
        0.65,
        0.35,
        server.BIOLUM_DIRECTOR_MAX_PREDATOR_CONTACTS,
        server.BIOLUM_DIRECTOR_MAX_TOUCH_RIPPLES,
        biolum_director_flags,
    )
    biolum_director_path = root / "Dump_BIOLUMINESCENCE_DIRECTOR.bin"
    biolum_director_path.write_bytes(biolum_director)
    parsed_biolum_director = server.parse_dump_file(biolum_director_path)
    assert parsed_biolum_director["type"] == "biolum_director_blackbox"
    assert parsed_biolum_director["magicHex"] == "0x42494F4C"
    assert parsed_biolum_director["headerBytes"] == server.BIOLUM_DIRECTOR_HEADER_BYTES
    assert parsed_biolum_director["entrySize"] == server.BIOLUM_DIRECTOR_ENTRY_BYTES
    assert parsed_biolum_director["declaredEntryCount"] == server.BIOLUM_DIRECTOR_TELEMETRY_CAPACITY
    assert parsed_biolum_director["nonEmptyEntryCount"] == 2
    assert parsed_biolum_director["latest"]["frame"] == 2602
    assert parsed_biolum_director["latest"]["flagLabels"] == [
        "daylight-masked",
        "predator-dim",
        "eclipse-masked",
        "camera-nonfinite",
        "zone-registry-overflow",
    ]
    assert parsed_biolum_director["reasonFlagLabels"] == [
        "nonfinite-intensity-phase",
        "camera-nonfinite",
    ]
    assert "reason_flags" in parsed_biolum_director["warnings"]
    assert "nonfinite_intensity_phase" in parsed_biolum_director["warnings"]
    assert "camera_nonfinite" in parsed_biolum_director["warnings"]
    assert "zone_registry_overflow" in parsed_biolum_director["warnings"]
    renamed_biolum_director_path = root / "Renamed_BiolumDirector.h8dump"
    renamed_biolum_director_path.write_bytes(biolum_director)
    assert server.parse_dump_file(renamed_biolum_director_path)["type"] == "biolum_director_blackbox"

    foveated = server.FOVEATED_SIMULATION_HEADER.pack(server.FOVEATED_SIMULATION_MAGIC, 3, 2)
    foveated_hash_0 = server.compute_foveated_simulation_state_hash(5, 1, 2, 2, 1)
    foveated_hash_1 = server.compute_foveated_simulation_state_hash(8, 2, 3, 2, 3)
    foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        300, 5, 1, 2, 2, 1, 10.0, 20.0, 30.0, 0.0, 0.0, 1.0, 0, foveated_hash_0
    )
    foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        301, 8, 2, 3, 2, 3, 11.0, 21.0, 31.0, 0.0, 0.0, 1.0, 1, foveated_hash_1
    )
    foveated += bytes(server.FOVEATED_SIMULATION_ENTRY.size)
    foveated_path = root / "Dump_FOVEATED_SIMULATION_DIRECTOR.bin"
    foveated_path.write_bytes(foveated)
    parsed_foveated = server.parse_dump_file(foveated_path)
    assert parsed_foveated["type"] == "foveated_simulation_blackbox"
    assert parsed_foveated["latest"]["frame"] == 301
    assert parsed_foveated["latest"]["stateHashOk"] is True
    assert parsed_foveated["latest"]["flagLabels"] == ["force-refresh"]
    renamed_foveated_path = root / "Renamed_Header_F8LD.bin"
    renamed_foveated_path.write_bytes(foveated)
    assert server.parse_dump_file(renamed_foveated_path)["type"] == "foveated_simulation_blackbox"
    mismatched_foveated = server.FOVEATED_SIMULATION_HEADER.pack(server.FOVEATED_SIMULATION_MAGIC, 1, 0)
    mismatched_foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        302, 8, 2, 3, 2, 3, 11.0, 21.0, 31.0, 0.0, 0.0, 1.0, 0, 0xBADF00D
    )
    parsed_mismatched_foveated = server.parse_foveated_simulation_blackbox(mismatched_foveated)
    assert parsed_mismatched_foveated["latest"]["stateHashOk"] is False
    assert "state_hash_mismatch" in parsed_mismatched_foveated["warnings"]

    input_entry = server.INPUT_DETERMINISM_ENTRY.pack(12.5, 400, 7, 0x00000003, 0x4B424D21, 750, 2, 1, 0x06)
    input_entry += bytes(64 - server.INPUT_DETERMINISM_ENTRY.size)
    input_path = root / "Dump_INPUT_DETERMINISM.bin"
    input_path.write_bytes(input_entry + bytes(64))
    parsed_input = server.parse_dump_file(input_path)
    assert parsed_input["type"] == "input_determinism_blackbox"
    assert parsed_input["latest"]["currentInputScheme"] == "keyboard-mouse"
    assert parsed_input["latest"]["flagLabels"] == ["delay", "nonfinite-sanitized"]
    assert "polling_time_over_500us" in parsed_input["warnings"]
    assert "nonfinite_sanitized" in parsed_input["warnings"]

    origin_flags = (1 << 2) | (1 << 4)
    origin_combined_stride = server.ORIGIN_SHIFT_BASE_ENTRY.size + server.ORIGIN_SHIFT_DETAIL_ENTRY.size
    origin_entry_count = 2
    origin_payload_bytes = origin_entry_count * origin_combined_stride
    origin = server.ORIGIN_SHIFT_HEADER.pack(
        server.ORIGIN_SHIFT_MAGIC,
        server.ORIGIN_SHIFT_VERSION,
        server.ORIGIN_SHIFT_HEADER.size,
        origin_entry_count,
        server.ORIGIN_SHIFT_BASE_ENTRY.size,
        origin_payload_bytes,
        0,
        501,
        server.ORIGIN_SHIFT_LITTLE_ENDIAN_TAG,
        server.ORIGIN_SHIFT_FLAG_HAS_DETAIL_ROWS,
        server.ORIGIN_SHIFT_DETAIL_ENTRY.size,
        origin_combined_stride,
    )
    origin += server.ORIGIN_SHIFT_BASE_ENTRY.pack(
        1.0, 0.0, -1.0, 500, 3, 44, 0xFACE0001, 80, 12, 0, 32, 0, 1 << 3
    )
    origin += server.ORIGIN_SHIFT_DETAIL_ENTRY.pack(
        1000.0, 0.0, -1000.0, 1.5, 2.5, 3.5, 0.25, 0.98, 0xFACE0001, 0x01020304, 4
    )
    origin += server.ORIGIN_SHIFT_BASE_ENTRY.pack(
        2.0, 0.0, -2.0, 501, 4, 45, 0xFACE0002, 96, 16, 32, 32, 1, origin_flags
    )
    origin += server.ORIGIN_SHIFT_DETAIL_ENTRY.pack(
        1002.0, 0.0, -1002.0, 4.5, 5.5, 6.5, 0.5, 0.91, 0xFACE0002, 0x05060708, 6
    )
    origin_path = root / "Dump_ORIGIN_SHIFT.bin"
    origin_path.write_bytes(origin)
    parsed_origin = server.parse_dump_file(origin_path)
    assert parsed_origin["type"] == "origin_shift_blackbox"
    assert parsed_origin["version"] == server.ORIGIN_SHIFT_VERSION
    assert parsed_origin["latest"]["frame"] == 501
    assert parsed_origin["latest"]["flagLabels"] == ["time-sliced", "shift-commit"]
    assert parsed_origin["latest"]["hotEntitiesShifted"] == 6
    assert parsed_origin["latest"]["rebaseComputeTimeMs"] == 0.5
    renamed_origin_path = root / "Renamed_Header_AUPD.bin"
    renamed_origin_path.write_bytes(origin)
    assert server.parse_dump_file(renamed_origin_path)["type"] == "origin_shift_blackbox"

    sentinel_type_name = "Hecton8.World.AbsoluteUniversePosition"
    sentinel_name_bytes = sentinel_type_name.encode("ascii")
    sentinel = server.BINARY_LAYOUT_SENTINEL_HEADER.pack(
        server.BINARY_LAYOUT_SENTINEL_MAGIC,
        server.BINARY_LAYOUT_SENTINEL_VERSION,
        0x4F464653,
        48,
        56,
        len(sentinel_name_bytes),
        0x12345678,
    )
    sentinel_path = root / "Dump_BINARY_LAYOUT_SENTINEL.bin"
    sentinel_path.write_bytes(sentinel + sentinel_name_bytes)
    parsed_sentinel = server.parse_dump_file(sentinel_path)
    assert parsed_sentinel["type"] == "binary_layout_sentinel"
    assert parsed_sentinel["version"] == server.BINARY_LAYOUT_SENTINEL_VERSION
    assert parsed_sentinel["latest"]["typeName"] == sentinel_type_name
    assert parsed_sentinel["latest"]["contextHash"] == 0x4F464653
    assert parsed_sentinel["latest"]["expected"] == 48
    assert parsed_sentinel["latest"]["observed"] == 56
    assert parsed_sentinel["latest"]["layoutMatches"] is False
    assert "layout_mismatch" in parsed_sentinel["warnings"]
    renamed_sentinel_path = root / "Renamed_Header_H8BL.bin"
    renamed_sentinel_path.write_bytes(sentinel + sentinel_name_bytes)
    assert server.parse_dump_file(renamed_sentinel_path)["type"] == "binary_layout_sentinel"

    terminal_faults = (1 << 1) | (1 << 2)
    terminal_os = server.TERMINAL_OS_HEADER.pack(
        server.TERMINAL_OS_MAGIC,
        server.TERMINAL_OS_VERSION,
        terminal_faults,
        2,
        4,
        2,
        server.TERMINAL_OS_ENTRY.size,
        server.TERMINAL_OS_SOURCE_HASH,
    )
    terminal_os += server.TERMINAL_OS_ENTRY.pack(
        610, 12, 3, 2, 0.25, 40.0, 80.0, 0, 0x12345678, 0x00ABCDEF, 0.75, 0.10, 12, 2, 9.0, 0.8
    )
    terminal_os += server.TERMINAL_OS_ENTRY.pack(
        611,
        13,
        4,
        3,
        0.75,
        55.0,
        120.0,
        terminal_faults,
        0x87654321,
        0x00FEDCBA,
        0.65,
        0.20,
        13,
        1,
        11.0,
        0.7,
    )
    terminal_os_path = root / "Dump_1309_TerminalOS.bin"
    terminal_os_path.write_bytes(terminal_os)
    parsed_terminal_os = server.parse_dump_file(terminal_os_path)
    assert parsed_terminal_os["type"] == "terminal_os_blackbox"
    assert parsed_terminal_os["version"] == server.TERMINAL_OS_VERSION
    assert parsed_terminal_os["latest"]["frame"] == 611
    assert parsed_terminal_os["latest"]["faultLabels"] == ["format-budget", "nonfinite"]
    assert parsed_terminal_os["latest"]["dirtyCount"] == 4
    assert parsed_terminal_os["dumpFaultLabels"] == ["format-budget", "nonfinite"]

    decryption_flags = (1 << 0) | (1 << 2) | (31 << server.TERMINAL_DECRYPTION_HOLD_FRAME_SHIFT)
    decryption_faults = 1 << 4
    terminal_decryption = server.TERMINAL_DECRYPTION_HEADER.pack(
        server.TERMINAL_DECRYPTION_MAGIC,
        server.TERMINAL_DECRYPTION_VERSION,
        decryption_faults,
        1,
        2,
        server.TERMINAL_DECRYPTION_ENTRY.size,
    )
    terminal_decryption += server.TERMINAL_DECRYPTION_ENTRY.pack(
        620, 0xDECA0001, 12.0, 0.1, 13.0, 0.2, 0.92, 80.0, 1 << 0, 0xABC10001, 0xABC20001, 0
    )
    terminal_decryption += server.TERMINAL_DECRYPTION_ENTRY.pack(
        621,
        0xDECA0002,
        14.0,
        0.3,
        14.1,
        0.35,
        0.99,
        140.0,
        decryption_flags,
        0xABC10002,
        0xABC20002,
        decryption_faults,
    )
    terminal_decryption_path = root / "Dump_1309_TerminalDecryption.bin"
    terminal_decryption_path.write_bytes(terminal_decryption)
    parsed_terminal_decryption = server.parse_dump_file(terminal_decryption_path)
    assert parsed_terminal_decryption["type"] == "terminal_decryption_blackbox"
    assert parsed_terminal_decryption["version"] == server.TERMINAL_DECRYPTION_VERSION
    assert parsed_terminal_decryption["latest"]["frame"] == 621
    assert parsed_terminal_decryption["latest"]["flagLabels"] == ["active", "initialized", "hold=31"]
    assert parsed_terminal_decryption["latest"]["holdFrames"] == 31
    assert parsed_terminal_decryption["latest"]["faultLabels"] == ["decryption-budget"]
    assert "solve_threshold_reached" in parsed_terminal_decryption["warnings"]

    terminal_projection_faults = (1 << 16) | (1 << 17)
    terminal_projection = server.TERMINAL_PROJECTION_HEADER.pack(
        server.TERMINAL_PROJECTION_MAGIC,
        server.TERMINAL_PROJECTION_VERSION,
        terminal_projection_faults,
        1,
        2,
        server.TERMINAL_PROJECTION_ENTRY_BYTES,
        server.TERMINAL_PROJECTION_INPUT_STATE_STRIDE_BYTES,
        server.TERMINAL_PROJECTION_ROLLBACK_EXCLUDED,
    )
    terminal_projection += server.TERMINAL_PROJECTION_ENTRY.pack(
        630, 12, 10, 2, 80.0, 8.5, 0.9, 0, 0xFFFFFFFF, 1, 0xABCD0001, 0.0065, 0.01, 0
    )
    terminal_projection += server.TERMINAL_PROJECTION_ENTRY.pack(
        631,
        12,
        9,
        3,
        230.0,
        9.0,
        0.75,
        terminal_projection_faults,
        0xFFFFFFFF,
        1,
        0xABCD0002,
        0.007,
        0.012,
        2,
    )
    terminal_projection_path = root / "Dump_1309_TerminalProjection.bin"
    terminal_projection_path.write_bytes(terminal_projection)
    parsed_terminal_projection = server.parse_dump_file(terminal_projection_path)
    assert parsed_terminal_projection["type"] == "terminal_projection_blackbox"
    assert parsed_terminal_projection["version"] == server.TERMINAL_PROJECTION_VERSION
    assert parsed_terminal_projection["latest"]["frame"] == 631
    assert parsed_terminal_projection["latest"]["faultLabels"] == ["projection-nonfinite", "projection-budget"]
    assert parsed_terminal_projection["latest"]["nonFiniteCount"] == 2
    assert "projection_nonfinite" in parsed_terminal_projection["warnings"]
    assert "projection_budget" in parsed_terminal_projection["warnings"]

    openxr_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 4)
    openxr = server.OPENXR_MANUAL_OVERRIDE_HEADER.pack(3, 1)
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.1, 0.2, 0.3, 0.0, 0.0, 0.0, 44.0, 85.0, 120.0, 700, 1 << 0
    )
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.2, 0.3, 0.4, 0.0, 0.0, 0.0, 55.0, 85.0, 130.0, 701, 1 << 3
    )
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.3, 0.4, 0.5, 0.0, 0.0, 0.0, 86.0, 85.0, 160.0, 702, openxr_flags
    )
    openxr_path = root / "Dump_1335_OpenXRManualOverrideLever.bin"
    openxr_path.write_bytes(openxr)
    parsed_openxr = server.parse_dump_file(openxr_path)
    assert parsed_openxr["type"] == "openxr_manual_override_blackbox"
    assert parsed_openxr["latest"]["frame"] == 702
    assert parsed_openxr["latest"]["flagLabels"] == ["grabbed", "latched", "xr-active", "projection-singular"]
    assert parsed_openxr["latest"]["latched"] is True
    assert "projection_singular" in parsed_openxr["warnings"]

    damage_flags = (1 << 0) | (1 << 2) | (1 << 4) | (1 << 5)
    vehicle_damage = server.VEHICLE_DAMAGE_HOLOGRAPHER_HEADER.pack(
        server.VEHICLE_DAMAGE_HOLOGRAPHER_MAGIC,
        2,
        server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY_BYTES,
        1,
    )
    vehicle_damage += server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY.pack(710, 4, 64, 0.25, 0.0, 1 << 0)
    vehicle_damage += server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY.pack(711, 9, 128, 0.5, 0.35, damage_flags)
    vehicle_damage_path = root / "Dump_VEHICLE_SUB_OS_DAMAGE_HOLOGRAPHER.bin"
    vehicle_damage_path.write_bytes(vehicle_damage)
    parsed_vehicle_damage = server.parse_dump_file(vehicle_damage_path)
    assert parsed_vehicle_damage["type"] == "vehicle_damage_holographer_blackbox"
    assert parsed_vehicle_damage["latest"]["frame"] == 711
    assert parsed_vehicle_damage["latest"]["flagLabels"] == ["resources-ready", "active-dent", "flood", "fallback-warning"]
    assert parsed_vehicle_damage["latest"]["holoDamagePoints"] == 9
    assert "fallback_warning" in parsed_vehicle_damage["warnings"]
    assert "flood_active" in parsed_vehicle_damage["warnings"]

    pda_flags = (1 << 0) | (1 << 3) | (1 << 6)
    pda_projection = server.PDA_PROJECTION_HEADER.pack(
        server.PDA_PROJECTION_MAGIC,
        server.PDA_PROJECTION_VERSION,
        722,
        pda_flags,
        3,
        2,
        server.PDA_PROJECTION_ENTRY_BYTES,
        2 * server.PDA_PROJECTION_ENTRY_BYTES,
        2,
        0,
    )
    pda_projection += server.PDA_PROJECTION_ENTRY.pack(
        721, 1 << 0, 0x50444131, 50 * 65536, 0.42, 0.5, 0.8, 1, 0x01010101, 0x50444131, 0.18, 0.112, 1.33, 0.2, 0.75, 1 << 0
    )
    pda_projection += server.PDA_PROJECTION_ENTRY.pack(
        722,
        pda_flags,
        0x50444132,
        125 * 65536,
        0.55,
        0.9,
        0.6,
        2,
        0x02020202,
        0x50444132,
        0.2,
        0.12,
        1.45,
        0.35,
        0.6,
        pda_flags,
    )
    pda_projection_path = root / "Dump_1335_UIPresentation_PdaProjection.bin"
    pda_projection_path.write_bytes(pda_projection)
    parsed_pda_projection = server.parse_dump_file(pda_projection_path)
    assert parsed_pda_projection["type"] == "pda_projection_blackbox"
    assert parsed_pda_projection["version"] == server.PDA_PROJECTION_VERSION
    assert parsed_pda_projection["latest"]["frame"] == 722
    assert parsed_pda_projection["latest"]["flagLabels"] == ["active", "over-budget", "gpu-upload-fault"]
    assert parsed_pda_projection["latest"]["jobMicroseconds"] == 125.0
    assert "over_budget" in parsed_pda_projection["warnings"]
    assert "gpu_upload_fault" in parsed_pda_projection["warnings"]

    wrist_flags = (1 << 1) | (1 << 3) | (1 << 7)
    wrist_hud = server.WRIST_HUD_HEADER.pack(
        server.WRIST_HUD_MAGIC,
        server.WRIST_HUD_VERSION,
        732,
        wrist_flags,
        2,
        0,
        server.WRIST_HUD_ENTRY_BYTES,
        2 * server.WRIST_HUD_ENTRY_BYTES,
    )
    wrist_hud += server.WRIST_HUD_ENTRY.pack(
        731, 0x10000001, 1 << 1, 18, 12, 2, 75 * 65536, 1, 0.8, 220.0, 300.0, 0.1, 0.05, 0.75, 181.0, 1.0
    )
    wrist_hud += server.WRIST_HUD_ENTRY.pack(
        732,
        0x10000002,
        wrist_flags,
        24,
        15,
        4,
        140 * 65536,
        0,
        0.7,
        240.0,
        300.0,
        0.2,
        0.10,
        0.65,
        183.0,
        1.0,
    )
    wrist_hud_path = root / "Dump_1335_WristHologramHud.bin"
    wrist_hud_path.write_bytes(wrist_hud)
    parsed_wrist_hud = server.parse_dump_file(wrist_hud_path)
    assert parsed_wrist_hud["type"] == "wrist_hud_blackbox"
    assert parsed_wrist_hud["version"] == server.WRIST_HUD_VERSION
    assert parsed_wrist_hud["latest"]["frame"] == 732
    assert parsed_wrist_hud["latest"]["flagLabels"] == ["pda-open", "job-over-budget", "gpu-upload-fault"]
    assert parsed_wrist_hud["latest"]["jobMicroseconds"] == 140.0
    assert "job_over_budget" in parsed_wrist_hud["warnings"]
    assert "gpu_upload_fault" in parsed_wrist_hud["warnings"]

    ladder_flags = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 7)
    ladder_header = server.LADDER_CLIMB_IK_HEADER.pack(
        server.LADDER_CLIMB_IK_MAGIC,
        server.LADDER_CLIMB_IK_VERSION,
        2,
        server.LADDER_CLIMB_IK_ENTRY_BYTES,
        2,
        0,
    )
    ladder_entry_0 = server.LADDER_CLIMB_IK_ENTRY_PREFIX.pack(
        0.0,
        1.0,
        2.0,
        -0.2,
        1.1,
        2.1,
        0.2,
        1.1,
        2.1,
        -0.4,
        1.0,
        2.0,
        0.4,
        1.0,
        2.0,
        1.25,
        0.9,
        4,
        5,
        740,
        0xCAFE0001,
        (1 << 0) | (1 << 5) | (1 << 6),
    )
    ladder_entry_1 = server.LADDER_CLIMB_IK_ENTRY_PREFIX.pack(
        0.0,
        1.2,
        2.2,
        -0.25,
        1.3,
        2.3,
        0.25,
        1.3,
        2.3,
        -0.45,
        1.2,
        2.2,
        0.45,
        1.2,
        2.2,
        1.75,
        0.65,
        5,
        6,
        741,
        0xCAFE0002,
        ladder_flags,
    )
    ladder_path = root / "Dump_LADDER_CLIMB_IK.bin"
    ladder_path.write_bytes(
        ladder_header
        + ladder_entry_0
        + bytes(server.LADDER_CLIMB_IK_ENTRY_BYTES - server.LADDER_CLIMB_IK_ENTRY_PREFIX.size)
        + ladder_entry_1
        + bytes(server.LADDER_CLIMB_IK_ENTRY_BYTES - server.LADDER_CLIMB_IK_ENTRY_PREFIX.size)
    )
    parsed_ladder = server.parse_dump_file(ladder_path)
    assert parsed_ladder["type"] == "ladder_climb_ik_blackbox"
    assert parsed_ladder["version"] == server.LADDER_CLIMB_IK_VERSION
    assert parsed_ladder["latest"]["frame"] == 741
    assert parsed_ladder["latest"]["flagLabels"] == ["active", "vr-grip", "slip", "unreachable"]
    assert parsed_ladder["latest"]["progressMeters"] == 1.75
    assert parsed_ladder["latest"]["stamina01"] == 0.65
    assert "slip" in parsed_ladder["warnings"]
    assert "unreachable" in parsed_ladder["warnings"]

    sonar_flags = (1 << 1) | (1 << 2) | (1 << 31)
    sonar = server.TOPOGRAPHICAL_SONAR_HEADER.pack(
        server.TOPOGRAPHICAL_SONAR_MAGIC,
        server.TOPOGRAPHICAL_SONAR_VERSION,
        2,
        server.TOPOGRAPHICAL_SONAR_ENTRY_BYTES,
        2,
        sonar_flags,
        512,
        12,
    )
    sonar += server.TOPOGRAPHICAL_SONAR_ENTRY.pack(
        10.5,
        1000.0,
        0.0,
        -1000.0,
        999.0,
        0.0,
        -1001.0,
        750,
        11,
        2000,
        256,
        200,
        (1 << 0) | (1 << 3),
        0.9,
        120.0,
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        64.0,
        0.85,
        7,
        900,
    )
    sonar += server.TOPOGRAPHICAL_SONAR_ENTRY.pack(
        10.7,
        1002.0,
        0.0,
        -1002.0,
        1000.0,
        0.0,
        -1004.0,
        751,
        12,
        4000,
        512,
        384,
        sonar_flags,
        0.75,
        140.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        7.0,
        96.0,
        0.65,
        8,
        24000,
    )
    sonar_path = root / "Dump_SONAR_SYNTHESIZER.bin"
    sonar_path.write_bytes(sonar)
    parsed_sonar = server.parse_dump_file(sonar_path)
    assert parsed_sonar["type"] == "topographical_sonar_blackbox"
    assert parsed_sonar["version"] == server.TOPOGRAPHICAL_SONAR_VERSION
    assert parsed_sonar["latest"]["frame"] == 751
    assert parsed_sonar["latest"]["flagLabels"] == ["sdf-unavailable", "gpu-upload", "fault"]
    assert parsed_sonar["latest"]["computeTimeMicroseconds"] == 24000
    assert "fault" in parsed_sonar["warnings"]
    assert "sdf_unavailable" in parsed_sonar["warnings"]
    assert "gpu_upload" in parsed_sonar["warnings"]

    kinetic_flags = (1 << 0) | (1 << 2) | (1 << 6) | (1 << 31)
    kinetic_payload = server.KINETIC_CHARACTER_ENTRY.pack(
        10,
        -2,
        4,
        1.0,
        2.0,
        3.0,
        760,
        64,
        2.25,
        125.5,
        0x14030001,
        1 << 0,
        0.9,
    )
    kinetic_payload += server.KINETIC_CHARACTER_ENTRY.pack(
        10,
        -2,
        5,
        1.5,
        2.5,
        3.5,
        761,
        96,
        3.5,
        181.25,
        0x14030002,
        kinetic_flags,
        0.55,
    )
    kinetic_hash = server.fnv1a_mix_bytes((2166136261 ^ 2) & 0xFFFFFFFF, kinetic_payload)
    kinetic_hash = 2166136261 if kinetic_hash == 0 else kinetic_hash
    kinetic = server.KINETIC_CHARACTER_HEADER.pack(
        server.KINETIC_CHARACTER_MAGIC,
        server.KINETIC_CHARACTER_VERSION,
        2,
        2,
        server.KINETIC_CHARACTER_ENTRY_BYTES,
        kinetic_hash,
    )
    kinetic_path = root / "Dump_1403_KINETIC_CHARACTER.bin"
    kinetic_path.write_bytes(kinetic + kinetic_payload)
    parsed_kinetic = server.parse_dump_file(kinetic_path)
    assert parsed_kinetic["type"] == "kinetic_character_blackbox"
    assert parsed_kinetic["version"] == server.KINETIC_CHARACTER_VERSION
    assert parsed_kinetic["latest"]["frame"] == 761
    assert parsed_kinetic["latest"]["flagLabels"] == ["visible", "sdf-brace", "quality-collapsed", "invalid"]
    assert parsed_kinetic["latest"]["bonesEvaluated"] == 96
    assert parsed_kinetic["dumpHashOk"] is True
    assert "quality_collapsed" in parsed_kinetic["warnings"]
    assert "invalid" in parsed_kinetic["warnings"]

    procedural_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 31)
    procedural_payload = server.PROCEDURAL_BONE_ENTRY.pack(
        770,
        2,
        120,
        112,
        0.65,
        0x1403B001,
        1 << 0,
        0.95,
        1.75,
        12.5,
        0,
        1,
        4.0,
        5.0,
        6.0,
        0,
    )
    procedural_payload += server.PROCEDURAL_BONE_ENTRY.pack(
        771,
        3,
        144,
        132,
        0.95,
        0x1403B002,
        procedural_flags,
        0.5,
        2.25,
        15.5,
        3,
        2,
        4.5,
        5.5,
        6.5,
        0,
    )
    procedural_seed = (2166136261 ^ 2 ^ 2 ^ 0x414E494D) & 0xFFFFFFFF
    procedural_hash = server.fnv1a_mix_bytes(procedural_seed, procedural_payload)
    procedural_hash = 2166136261 if procedural_hash == 0 else procedural_hash
    procedural = server.PROCEDURAL_BONE_HEADER.pack(
        server.PROCEDURAL_BONE_MAGIC,
        server.PROCEDURAL_BONE_VERSION,
        2,
        2,
        server.PROCEDURAL_BONE_ENTRY_BYTES,
        procedural_hash,
    )
    procedural_path = root / "Dump_1403_PROCEDURAL_BONE.bin"
    procedural_path.write_bytes(procedural + procedural_payload)
    parsed_procedural = server.parse_dump_file(procedural_path)
    assert parsed_procedural["type"] == "procedural_bone_blackbox"
    assert parsed_procedural["version"] == server.PROCEDURAL_BONE_VERSION
    assert parsed_procedural["latest"]["frame"] == 771
    assert parsed_procedural["latest"]["flagLabels"] == ["visible", "quality-collapse", "jaw-solved", "invalid"]
    assert parsed_procedural["latest"]["invalidMathCount"] == 3
    assert parsed_procedural["dumpHashOk"] is True
    assert "quality_collapse" in parsed_procedural["warnings"]
    assert "invalid_math_count" in parsed_procedural["warnings"]
    assert "invalid" in parsed_procedural["warnings"]

    def pack_vr_somatic_entry(
        frame: int,
        flags: int,
        hand_ghost_mask: int,
        near_collision: float,
        comfort_vignette: float,
        head_angular_speed: float,
        kcc_horizon_lock: float,
    ) -> bytes:
        row = server.VR_SOMATIC_ENTRY.pack(
            frame,
            0,
            flags,
            hand_ghost_mask,
            1.0,
            2.0,
            3.0,
            0.0,
            0.0,
            0.0,
            1.0,
            near_collision,
            comfort_vignette,
            0.25,
            0.36,
            head_angular_speed,
            42,
            2.5,
            48.0,
            0.35,
            kcc_horizon_lock,
            7,
            frame - 1,
            0x534F4D41,
            0,
            0,
        )
        state_hash = server.compute_vr_somatic_state_hash(row)
        return server.VR_SOMATIC_ENTRY.pack(
            frame,
            state_hash,
            flags,
            hand_ghost_mask,
            1.0,
            2.0,
            3.0,
            0.0,
            0.0,
            0.0,
            1.0,
            near_collision,
            comfort_vignette,
            0.25,
            0.36,
            head_angular_speed,
            42,
            2.5,
            48.0,
            0.35,
            kcc_horizon_lock,
            7,
            frame - 1,
            0x534F4D41,
            0,
            0,
        )

    somatic_flags = (
        (1 << 0)
        | (1 << 2)
        | (1 << 3)
        | (1 << 6)
        | (1 << 9)
        | (1 << 10)
        | (1 << 11)
        | (1 << 12)
        | (1 << 13)
        | (1 << 14)
    )
    somatic = server.VR_SOMATIC_HEADER.pack(
        server.VR_SOMATIC_MAGIC,
        server.VR_SOMATIC_VERSION,
        server.VR_SOMATIC_FRAME_CAPACITY,
        2,
    )
    somatic += pack_vr_somatic_entry(780, 1 << 0, 0, 0.0, 0.05, 0.75, 0.0)
    somatic += pack_vr_somatic_entry(781, somatic_flags, 3, 0.7, 0.42, 4.5, 0.55)
    somatic_path = root / "Dump_1335_SomaticComfort.bin"
    somatic_path.write_bytes(somatic)
    parsed_somatic = server.parse_dump_file(somatic_path)
    assert parsed_somatic["type"] == "vr_somatic_blackbox"
    assert parsed_somatic["version"] == server.VR_SOMATIC_VERSION
    assert parsed_somatic["latest"]["frame"] == 781
    assert parsed_somatic["latest"]["stateHashOk"] is True
    assert parsed_somatic["latest"]["flagLabels"] == [
        "active",
        "left-ghost",
        "right-ghost",
        "near-collision",
        "frame-pressure",
        "protective-fallback",
        "acceleration-tunnel",
        "kcc-signal",
        "kcc-acceleration-tunnel",
        "dynamic-horizon-lock",
    ]
    assert parsed_somatic["latest"]["handGhostMask"] == 3
    assert "near_collision" in parsed_somatic["warnings"]
    assert "frame_pressure" in parsed_somatic["warnings"]
    assert "protective_fallback" in parsed_somatic["warnings"]
    assert "acceleration_tunnel" in parsed_somatic["warnings"]

    lockstep_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 6) | (1 << 8)
    lockstep_master_hash = 0x12345678ABCDEF01
    lockstep = server.LOCKSTEP_STATE_VALIDATOR_HEADER.pack(
        server.LOCKSTEP_STATE_VALIDATOR_MAGIC,
        server.LOCKSTEP_STATE_VALIDATOR_VERSION,
        2,
        server.LOCKSTEP_STATE_VALIDATOR_ENTRY_BYTES,
        2,
        lockstep_master_hash,
    )
    lockstep += server.LOCKSTEP_STATE_VALIDATOR_ENTRY.pack(
        790,
        0xABCDEF00,
        0x12345678,
        0x100,
        0x200,
        0x300,
        0x400,
        1 << 0,
        10,
        1,
        3,
        30,
        0,
        0,
        0,
        0,
    )
    lockstep += server.LOCKSTEP_STATE_VALIDATOR_ENTRY.pack(
        791,
        0xABCDEF01,
        0x12345678,
        0x101,
        0x201,
        0x301,
        0x401,
        lockstep_flags,
        11,
        1,
        4,
        31,
        (1 << 1) | (1 << 3),
        1 << 2,
        5,
        0,
    )
    lockstep_path = root / "Dump_1403_LOCKSTEP_STATE_VALIDATOR.bin"
    lockstep_path.write_bytes(lockstep)
    parsed_lockstep = server.parse_dump_file(lockstep_path)
    assert parsed_lockstep["type"] == "lockstep_state_validator_blackbox"
    assert parsed_lockstep["version"] == server.LOCKSTEP_STATE_VALIDATOR_VERSION
    assert parsed_lockstep["latest"]["frame"] == 791
    assert parsed_lockstep["latest"]["masterHashHex"] == "0x12345678ABCDEF01"
    assert parsed_lockstep["latestMasterHashMatchesHeader"] is True
    assert parsed_lockstep["latest"]["flagLabels"] == ["hash-executed", "missing-data", "nonfinite", "desync", "layout-invalid"]
    assert parsed_lockstep["latest"]["missingLabels"] == ["player-kinematic-state", "entity-aups"]
    assert parsed_lockstep["latest"]["nonFiniteLabels"] == ["room-water-levels"]
    assert "desync" in parsed_lockstep["warnings"]
    assert "missing_data" in parsed_lockstep["warnings"]
    assert "nonfinite" in parsed_lockstep["warnings"]
    assert "layout_invalid" in parsed_lockstep["warnings"]
    renamed_lockstep_path = root / "Renamed_Header_LSDUMP.bin"
    renamed_lockstep_path.write_bytes(lockstep)
    assert server.parse_dump_file(renamed_lockstep_path)["type"] == "lockstep_state_validator_blackbox"

    voxel_flags = (1 << 0) | (1 << 2) | (1 << 4) | (1 << 7) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12)
    voxel_astar = server.VOXEL_ASTAR_ENTRY.pack(
        800,
        2,
        4,
        0,
        1,
        0,
        128,
        64,
        900,
        1 << 12,
        11,
        0xAAA10001,
        0.9,
        1.15,
        12,
        6,
        0,
    )
    voxel_astar += server.VOXEL_ASTAR_ENTRY.pack(
        801,
        3,
        5,
        2,
        0,
        1,
        256,
        96,
        2200,
        voxel_flags,
        12,
        0xAAA10002,
        0.55,
        1.35,
        24,
        0,
        0,
    )
    voxel_astar_path = root / "Dump_1403_VOXEL_ASTAR.bin"
    voxel_astar_path.write_bytes(voxel_astar)
    parsed_voxel_astar = server.parse_dump_file(voxel_astar_path)
    assert parsed_voxel_astar["type"] == "voxel_astar_blackbox"
    assert parsed_voxel_astar["latest"]["frame"] == 801
    assert parsed_voxel_astar["latest"]["flagLabels"] == [
        "nonfinite-input",
        "goal-out-of-bounds",
        "goal-blocked",
        "raw-path-overflow",
        "sdf-missing",
        "nan-detected",
        "time-slice-over-budget",
        "weighted-heuristic",
    ]
    assert parsed_voxel_astar["latest"]["droppedRequests"] == 2
    assert parsed_voxel_astar["latest"]["failedPaths"] == 1
    assert "nonfinite" in parsed_voxel_astar["warnings"]
    assert "time_slice_over_budget" in parsed_voxel_astar["warnings"]
    assert "dropped_requests" in parsed_voxel_astar["warnings"]
    assert "failed_paths" in parsed_voxel_astar["warnings"]
    assert "sdf_missing" in parsed_voxel_astar["warnings"]
    assert "overflow" in parsed_voxel_astar["warnings"]
    assert "out_of_bounds" in parsed_voxel_astar["warnings"]
    assert "blocked" in parsed_voxel_astar["warnings"]

    path_funnel_flags = (1 << 0) | (1 << 1)
    path_funnel = server.PATH_FUNNEL_ENTRY.pack(
        0x1111222233334444,
        0,
        810,
        1,
        22,
        0xFEED1000,
        0.25,
        0,
        12,
        4,
        0,
        0,
    )
    path_funnel += server.PATH_FUNNEL_ENTRY.pack(
        0x1111222233335555,
        0,
        811,
        3,
        23,
        0xFEED1001,
        0.85,
        0,
        13,
        5,
        2,
        path_funnel_flags,
    )
    path_funnel_path = root / "Dump_1403_PATH_FUNNEL.bin"
    path_funnel_path.write_bytes(path_funnel)
    parsed_path_funnel = server.parse_dump_file(path_funnel_path)
    assert parsed_path_funnel["type"] == "path_funnel_blackbox"
    assert parsed_path_funnel["latest"]["frame"] == 811
    assert parsed_path_funnel["latest"]["flagLabels"] == ["blackbox-dump-failed", "wfc-vault-signal-mismatch"]
    assert parsed_path_funnel["latest"]["lastSectorHashHex"] == "0x1111222233335555"
    assert parsed_path_funnel["latest"]["invalidatedPathCount"] == 2
    assert "blackbox_dump_failed" in parsed_path_funnel["warnings"]
    assert "wfc_vault_signal_mismatch" in parsed_path_funnel["warnings"]
    assert "path_invalidations" in parsed_path_funnel["warnings"]

    laser_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
    )
    laser_cutter = server.LASER_CUTTER_DOD_HEADER.pack(
        server.LASER_CUTTER_DOD_MAGIC,
        server.LASER_CUTTER_DOD_VERSION,
        981,
        2,
        server.LASER_CUTTER_DOD_ENTRY_BYTES,
        0,
        45,
        2 * server.LASER_CUTTER_DOD_ENTRY_BYTES,
    )
    laser_cutter += server.LASER_CUTTER_DOD_ENTRY.pack(
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0.0,
        0.0,
        1.0,
        5.5,
        0.75,
        0.80,
        980,
        44,
        0x4C435452,
        0x22500001,
        0,
        (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5),
        64,
        0,
        server.LASER_CUTTER_DOD_LAYOUT_MAGIC,
        0.55,
        0x2250ABCDEF123456,
        135.0,
        18,
    )
    laser_cutter += server.LASER_CUTTER_DOD_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        14.0,
        25.0,
        36.0,
        0.0,
        1.0,
        0.0,
        7.25,
        0.92,
        0.95,
        981,
        45,
        0x4C435452,
        0x22500002,
        0,
        laser_flags,
        128,
        990,
        server.LASER_CUTTER_DOD_LAYOUT_MAGIC,
        0.88,
        0x2250FEDCBA654321,
        165.0,
        24,
    )
    laser_cutter_path = root / "Dump_SHINOBU_225.bin"
    laser_cutter_path.write_bytes(laser_cutter)
    parsed_laser_cutter = server.parse_dump_file(laser_cutter_path)
    assert parsed_laser_cutter["type"] == "laser_cutter_dod_blackbox"
    assert parsed_laser_cutter["version"] == server.LASER_CUTTER_DOD_VERSION
    assert parsed_laser_cutter["latest"]["frame"] == 981
    assert parsed_laser_cutter["latest"]["requestSequence"] == 45
    assert parsed_laser_cutter["latest"]["toolHashHex"] == "0x4C435452"
    assert parsed_laser_cutter["latest"]["layoutMagicHex"] == "0x53484C43"
    assert parsed_laser_cutter["latest"]["stateHashHex"] == "0x2250FEDCBA654321"
    assert parsed_laser_cutter["latest"]["flagLabels"] == [
        "hit",
        "nonfinite",
        "shader-dent-only",
        "gpu-spark-only",
        "battery-drain-queued",
        "decal-queued",
    ]
    assert parsed_laser_cutter["latest"]["sparkCount"] == 128
    assert parsed_laser_cutter["latest"]["hit"] is True
    assert parsed_laser_cutter["latest"]["nonFinite"] is True
    assert "nonfinite" in parsed_laser_cutter["warnings"]

    wfc_flags = (1 << 0) | (1 << 2)
    wfc_laser = server.WFC_LASER_CUT_HEADER.pack(
        server.WFC_LASER_CUT_MAGIC,
        server.WFC_LASER_CUT_VERSION,
        2,
        server.WFC_LASER_CUT_ENTRY_BYTES,
        0,
        server.WFC_LASER_CUT_SOURCE_HASH,
        0,
        0,
    )
    wfc_laser += server.WFC_LASER_CUT_ENTRY.pack(
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0x2250ABCDEF000001,
        990,
        0x4C435452,
        0.40,
        0.10,
        0.70,
        0.60,
        0.20,
        1,
        12,
        0,
        0,
    )
    wfc_laser += server.WFC_LASER_CUT_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        40.0,
        50.0,
        60.0,
        0x2250ABCDEF000002,
        991,
        0x4C435452,
        1.0,
        0.25,
        0.92,
        0.85,
        0.90,
        2,
        13,
        wfc_flags,
        0,
    )
    wfc_laser_path = root / "Dump_SHINOBU_225_WfcLaserCut.bin"
    wfc_laser_path.write_bytes(wfc_laser)
    parsed_wfc_laser = server.parse_dump_file(wfc_laser_path)
    assert parsed_wfc_laser["type"] == "wfc_laser_cut_blackbox"
    assert parsed_wfc_laser["version"] == server.WFC_LASER_CUT_VERSION
    assert parsed_wfc_laser["sourceHashHex"] == "0x544C5352"
    assert parsed_wfc_laser["latest"]["frame"] == 991
    assert parsed_wfc_laser["latest"]["sectorHashHex"] == "0x2250ABCDEF000002"
    assert parsed_wfc_laser["latest"]["cellIndex"] == 13
    assert parsed_wfc_laser["latest"]["progress01"] == 1.0
    assert parsed_wfc_laser["latest"]["flagLabels"] == ["completed", "stress-reduced"]
    assert parsed_wfc_laser["latest"]["completed"] is True
    assert parsed_wfc_laser["latest"]["stressReduced"] is True
    assert "stress_reduced" in parsed_wfc_laser["warnings"]
    renamed_laser_path = root / "Renamed_Header_SH25.bin"
    renamed_laser_path.write_bytes(laser_cutter)
    assert server.parse_dump_file(renamed_laser_path)["type"] == "laser_cutter_dod_blackbox"
    renamed_wfc_path = root / "Renamed_Header_WFCL.bin"
    renamed_wfc_path.write_bytes(wfc_laser)
    assert server.parse_dump_file(renamed_wfc_path)["type"] == "wfc_laser_cut_blackbox"

    tool_kinematics_flags = (1 << 1) | (1 << 7) | (1 << 8) | (1 << 13) | (1 << 16)
    tool_kinematics_entry_count = 2 * server.TOOL_KINEMATICS_BLACKBOX_CAPACITY
    tool_kinematics_payload_bytes = tool_kinematics_entry_count * server.TOOL_KINEMATICS_ENTRY_BYTES
    tool_kinematics = bytearray(
        server.TOOL_KINEMATICS_HEADER.pack(
            server.TOOL_KINEMATICS_MAGIC,
            server.TOOL_KINEMATICS_VERSION,
            tool_kinematics_entry_count,
            server.TOOL_KINEMATICS_ENTRY_BYTES,
            2,
            17,
            1201,
            tool_kinematics_payload_bytes,
        )
        + bytes(tool_kinematics_payload_bytes)
    )
    server.TOOL_KINEMATICS_ENTRY.pack_into(
        tool_kinematics,
        server.TOOL_KINEMATICS_HEADER_BYTES + (17 * server.TOOL_KINEMATICS_ENTRY_BYTES),
        1199,
        0x5343414E,
        0.10,
        0.95,
        12.50,
        24,
        6.25,
        1 << 0,
        0.10,
        0.20,
        0.30,
        1.10,
        1.20,
        1.30,
        0x524F434B,
        0,
    )
    server.TOOL_KINEMATICS_ENTRY.pack_into(
        tool_kinematics,
        server.TOOL_KINEMATICS_HEADER_BYTES
        + ((server.TOOL_KINEMATICS_BLACKBOX_CAPACITY + 17) * server.TOOL_KINEMATICS_ENTRY_BYTES),
        1201,
        0x4C435554,
        0.93,
        0.05,
        7.50,
        72,
        8.25,
        tool_kinematics_flags,
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0x4D45544C,
        0,
    )
    tool_kinematics_path = root / "Dump_TOOL_KINEMATICS.bin"
    tool_kinematics_path.write_bytes(tool_kinematics)
    parsed_tool_kinematics = server.parse_dump_file(tool_kinematics_path)
    assert parsed_tool_kinematics["type"] == "tool_kinematics_blackbox"
    assert parsed_tool_kinematics["version"] == server.TOOL_KINEMATICS_VERSION
    assert parsed_tool_kinematics["declaredEntryCount"] == 600
    assert parsed_tool_kinematics["toolCapacity"] == 2
    assert parsed_tool_kinematics["telemetryCursor"] == 17
    assert parsed_tool_kinematics["nonEmptyEntryCount"] == 2
    assert parsed_tool_kinematics["latest"]["frame"] == 1201
    assert parsed_tool_kinematics["latest"]["toolSlot"] == 1
    assert parsed_tool_kinematics["latest"]["ringSlot"] == 17
    assert parsed_tool_kinematics["latest"]["toolName"] == "laser-cutter"
    assert parsed_tool_kinematics["latest"]["materialHashHex"] == "0x4D45544C"
    assert parsed_tool_kinematics["latest"]["flagLabels"] == [
        "active",
        "fault",
        "ray-hit",
        "raymarch-budget-exceeded",
        "power-depleted-signal-queued",
    ]
    assert parsed_tool_kinematics["latest"]["fault"] is True
    assert parsed_tool_kinematics["latest"]["raymarchBudgetExceeded"] is True
    assert "fault_flag" in parsed_tool_kinematics["warnings"]
    assert "raymarch_budget_exceeded" in parsed_tool_kinematics["warnings"]
    renamed_tool_kinematics_path = root / "Renamed_Header_TKBB.bin"
    renamed_tool_kinematics_path.write_bytes(tool_kinematics)
    assert server.parse_dump_file(renamed_tool_kinematics_path)["type"] == "tool_kinematics_blackbox"

    auxiliary_flags = (1 << 2) | (1 << 29) | (1 << 31)
    auxiliary_equipment = bytearray(server.AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES)
    server.AUXILIARY_EQUIPMENT_ENTRY.pack_into(
        auxiliary_equipment,
        8 * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES,
        1301,
        12,
        2,
        3,
        1,
        60.0,
        210.5,
        0.75,
        1 << 1,
        0x2290ABCD,
        0,
        0,
        0,
        8,
        0,
    )
    server.AUXILIARY_EQUIPMENT_ENTRY.pack_into(
        auxiliary_equipment,
        9 * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES,
        1302,
        13,
        2,
        4,
        1,
        30.0,
        640.25,
        0.50,
        auxiliary_flags,
        0x2290DCBA,
        1,
        2,
        3,
        9,
        0,
    )
    auxiliary_path = root / "Dump_SHINOBU_229.bin"
    auxiliary_path.write_bytes(auxiliary_equipment)
    parsed_auxiliary = server.parse_dump_file(auxiliary_path)
    assert parsed_auxiliary["type"] == "auxiliary_equipment_blackbox"
    assert parsed_auxiliary["entrySize"] == server.AUXILIARY_EQUIPMENT_ENTRY_BYTES
    assert parsed_auxiliary["declaredEntryCount"] == server.AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY
    assert parsed_auxiliary["nonEmptyEntryCount"] == 2
    assert parsed_auxiliary["latest"]["frame"] == 1302
    assert parsed_auxiliary["latest"]["activeCount"] == 13
    assert parsed_auxiliary["latest"]["flagLabels"] == ["sensor-ping", "nonfinite-recovered", "faulted"]
    assert parsed_auxiliary["latest"]["snapshotHashHex"] == "0x2290DCBA"
    assert parsed_auxiliary["latest"]["droppedSlots"] == 1
    assert parsed_auxiliary["latest"]["droppedSignals"] == 2
    assert parsed_auxiliary["latest"]["corruptedSignals"] == 3
    assert "faulted" in parsed_auxiliary["warnings"]
    assert "nonfinite_recovered" in parsed_auxiliary["warnings"]
    assert "dropped_slots" in parsed_auxiliary["warnings"]
    assert "dropped_signals" in parsed_auxiliary["warnings"]
    assert "corrupted_signals" in parsed_auxiliary["warnings"]
    assert "cpu_over_500us" in parsed_auxiliary["warnings"]

    upgrade_matrix = bytearray(server.UPGRADE_MATRIX_TELEMETRY_CAPACITY * server.UPGRADE_MATRIX_ENTRY_BYTES)
    server.UPGRADE_MATRIX_ENTRY.pack_into(
        upgrade_matrix,
        3 * server.UPGRADE_MATRIX_ENTRY_BYTES,
        1401,
        64,
        128,
        64,
        48.25,
        0,
        server.UPGRADE_MATRIX_LAYOUT_MAGIC,
        0x2310ABCD,
        0x00000000FFFF0001,
        0x2310000000000001,
        0,
        0,
    )
    server.UPGRADE_MATRIX_ENTRY.pack_into(
        upgrade_matrix,
        4 * server.UPGRADE_MATRIX_ENTRY_BYTES,
        1402,
        96,
        384,
        96,
        142.75,
        1 << 0,
        server.UPGRADE_MATRIX_LAYOUT_MAGIC,
        0x2310DCBA,
        0x00000000FFFF0002,
        0x2310000000000002,
        0,
        0,
    )
    upgrade_matrix_path = root / "Dump_SHINOBU_231.bin"
    upgrade_matrix_path.write_bytes(upgrade_matrix)
    parsed_upgrade_matrix = server.parse_dump_file(upgrade_matrix_path)
    assert parsed_upgrade_matrix["type"] == "upgrade_matrix_blackbox"
    assert parsed_upgrade_matrix["entrySize"] == server.UPGRADE_MATRIX_ENTRY_BYTES
    assert parsed_upgrade_matrix["declaredEntryCount"] == server.UPGRADE_MATRIX_TELEMETRY_CAPACITY
    assert parsed_upgrade_matrix["nonEmptyEntryCount"] == 2
    assert parsed_upgrade_matrix["latest"]["frame"] == 1402
    assert parsed_upgrade_matrix["latest"]["evaluatedMaskCount"] == 96
    assert parsed_upgrade_matrix["latest"]["activeBitCount"] == 384
    assert parsed_upgrade_matrix["latest"]["faultLabels"] == ["burst-over-budget"]
    assert parsed_upgrade_matrix["latest"]["layoutMagicHex"] == "0x55323331"
    assert parsed_upgrade_matrix["latest"]["lastEntityHashHex"] == "0x2310DCBA"
    assert parsed_upgrade_matrix["latest"]["lastMaskHex"] == "0x00000000FFFF0002"
    assert parsed_upgrade_matrix["latest"]["stateHashHex"] == "0x2310000000000002"
    assert "fault_flags" in parsed_upgrade_matrix["warnings"]
    assert "burst_over_100us" in parsed_upgrade_matrix["warnings"]

    metabolism_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 6)
        | (1 << 8)
        | (1 << 9)
        | (1 << 10)
        | (1 << 30)
        | (1 << 31)
    )
    metabolism = bytearray(
        server.METABOLISM_BLACKBOX_HEADER.pack(
            server.METABOLISM_BLACKBOX_MAGIC,
            server.METABOLISM_BLACKBOX_VERSION,
            server.METABOLISM_TELEMETRY_CAPACITY,
            server.METABOLISM_TELEMETRY_ENTRY_BYTES,
            1452,
            4,
            server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES,
        )
        + bytes(server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_TELEMETRY_ENTRY_BYTES)
        + bytes(server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES)
    )
    metabolism_detail_offset = server.METABOLISM_BLACKBOX_HEADER_BYTES + (
        server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_TELEMETRY_ENTRY_BYTES
    )
    server.METABOLISM_TELEMETRY_ENTRY.pack_into(
        metabolism,
        server.METABOLISM_BLACKBOX_HEADER_BYTES + 3 * server.METABOLISM_TELEMETRY_ENTRY_BYTES,
        0x3200000000000001,
        1451,
        5,
        36.8,
        36.1,
        0.1,
        0,
        0,
        1,
        0.1,
        65.0,
        0.9,
        1 << 8,
        0,
        1,
    )
    server.METABOLISM_TELEMETRY_ENTRY.pack_into(
        metabolism,
        server.METABOLISM_BLACKBOX_HEADER_BYTES + 4 * server.METABOLISM_TELEMETRY_ENTRY_BYTES,
        0x3200000000000002,
        1452,
        5,
        32.5,
        27.5,
        0.84,
        2,
        1,
        3,
        0.1,
        240.5,
        0.55,
        metabolism_flags,
        4,
        7,
    )
    server.METABOLISM_DETAIL_TELEMETRY_ENTRY.pack_into(
        metabolism,
        metabolism_detail_offset + 4 * server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES,
        10.0,
        -42.0,
        90.0,
        120.0,
        4.25,
        -8.0,
        0.72,
        -45.0,
        -0.15,
        1452,
        0x504C5952,
        (1 << 2) | (1 << 3) | (1 << 10),
        0x53554954,
    )
    metabolism_path = root / "Dump_SHINOBU_320.bin"
    metabolism_path.write_bytes(metabolism)
    parsed_metabolism = server.parse_dump_file(metabolism_path)
    assert parsed_metabolism["type"] == "metabolism_blackbox"
    assert parsed_metabolism["version"] == server.METABOLISM_BLACKBOX_VERSION
    assert parsed_metabolism["entrySize"] == server.METABOLISM_TELEMETRY_ENTRY_BYTES
    assert parsed_metabolism["detailEntrySize"] == server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES
    assert parsed_metabolism["nonEmptyEntryCount"] == 2
    assert parsed_metabolism["nonEmptyDetailEntryCount"] == 1
    assert parsed_metabolism["latest"]["frame"] == 1452
    assert parsed_metabolism["latest"]["flagLabels"] == [
        "starving",
        "dehydrated",
        "hypothermia",
        "toxic",
        "invalid-math",
        "thermal-sampled",
        "chemical-sampled",
        "fatigue",
        "hypoxia",
        "execution-budget-exceeded",
        "nan-detected",
    ]
    assert parsed_metabolism["latest"]["maximumToxicity"] == 0.84
    assert parsed_metabolism["latestDetail"]["playerDepthMeters"] == 120.0
    assert parsed_metabolism["latestDetail"]["entityHashHex"] == "0x504C5952"
    assert parsed_metabolism["latestDetail"]["suitProfileHashHex"] == "0x53554954"
    assert "invalid_math" in parsed_metabolism["warnings"]
    assert "nan_detected" in parsed_metabolism["warnings"]
    assert "execution_budget_exceeded" in parsed_metabolism["warnings"]
    assert "execution_over_200us" in parsed_metabolism["warnings"]
    assert "starvation" in parsed_metabolism["warnings"]
    assert "dehydration" in parsed_metabolism["warnings"]
    assert "toxicity" in parsed_metabolism["warnings"]
    assert "hypothermia" in parsed_metabolism["warnings"]
    assert "hypoxia" in parsed_metabolism["warnings"]
    renamed_metabolism_path = root / "Renamed_Header_METASRGE.bin"
    renamed_metabolism_path.write_bytes(metabolism)
    assert server.parse_dump_file(renamed_metabolism_path)["type"] == "metabolism_blackbox"

    physiology = bytearray(
        server.PHYSIOLOGY_AUTOPSY_HEADER.pack(
            server.PHYSIOLOGY_AUTOPSY_MAGIC,
            server.PHYSIOLOGY_AUTOPSY_VERSION,
            server.PHYSIOLOGY_TELEMETRY_CAPACITY,
            server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
            6,
            0x50485953,
            1502,
            server.PHYSIOLOGY_TELEMETRY_CAPACITY,
            server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES,
            7,
            server.PHYSIOLOGY_DECOMPRESSION_RING_BUFFER,
        )
        + bytes(server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES)
        + bytes(server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES)
    )
    physiology_payload_offset = server.PHYSIOLOGY_AUTOPSY_HEADER_BYTES
    decompression_payload_offset = physiology_payload_offset + (
        server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES
    )
    server.PHYSIOLOGY_TELEMETRY_ENTRY.pack_into(
        physiology,
        physiology_payload_offset + 5 * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
        0x3210000000000001,
        1 << 1,
        1501,
        0,
        0.82,
        0.33,
        35.8,
        2.4,
        0.15,
        0.20,
        92.0,
        0.35,
        0,
        64.0,
    )
    physiology_status = (1 << 3) | (1 << 8) | (1 << 12)
    physiology_fatal = (1 << 4) | (1 << 12) | (1 << 16)
    server.PHYSIOLOGY_TELEMETRY_ENTRY.pack_into(
        physiology,
        physiology_payload_offset + 6 * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
        0x3210000000000002,
        physiology_status,
        1502,
        physiology_fatal,
        0.03,
        0.86,
        30.5,
        8.2,
        0.72,
        0.99,
        135.0,
        0.91,
        0x00000005,
        240.0,
    )
    server.DECOMPRESSION_TELEMETRY_ENTRY.pack_into(
        physiology,
        decompression_payload_offset + 7 * server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES,
        0x3210D00000000001,
        1502,
        1 << 0,
        72.5,
        8.2,
        9.8,
        7.5,
        2.3,
        0.99,
        210.0,
        0.75,
        0x00000005,
        3,
        1 << 11,
        0,
    )
    physiology_path = root / "Dump_SHINOBU_321.bin"
    physiology_path.write_bytes(physiology)
    parsed_physiology = server.parse_dump_file(physiology_path)
    assert parsed_physiology["type"] == "physiology_autopsy_blackbox"
    assert parsed_physiology["version"] == server.PHYSIOLOGY_AUTOPSY_VERSION
    assert parsed_physiology["entrySize"] == server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES
    assert parsed_physiology["decompressionEntrySize"] == server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES
    assert parsed_physiology["nonEmptyEntryCount"] == 2
    assert parsed_physiology["nonEmptyDecompressionEntryCount"] == 1
    assert parsed_physiology["latest"]["frame"] == 1502
    assert parsed_physiology["latest"]["statusEffectLabels"] == [
        "oxygen-critical",
        "hypoxia",
        "fatal-gas-toxicity",
    ]
    assert parsed_physiology["latest"]["fatalFlagLabels"] == [
        "fatal-oxygen",
        "hypoxia",
        "fatal-gas-toxicity",
    ]
    assert parsed_physiology["latest"]["bloodOxygen"] == 0.03
    assert parsed_physiology["latest"]["fatalGasToxicity"] is True
    assert parsed_physiology["latestDecompression"]["frame"] == 1502
    assert parsed_physiology["latestDecompression"]["bubbleFlagLabels"] == ["fast-tissue-over-m-value"]
    assert parsed_physiology["latestDecompression"]["fatalFlagLabels"] == ["fatal-bends"]
    assert "fatal_flags" in parsed_physiology["warnings"]
    assert "fatal_oxygen" in parsed_physiology["warnings"]
    assert "hypoxia" in parsed_physiology["warnings"]
    assert "fatal_gas_toxicity" in parsed_physiology["warnings"]
    assert "fatal_bends" in parsed_physiology["warnings"]
    assert "supersaturation_fatal_threshold" in parsed_physiology["warnings"]
    assert "execution_over_200us" in parsed_physiology["warnings"]
    renamed_physiology_path = root / "Renamed_Header_SHINOBU2.bin"
    renamed_physiology_path.write_bytes(physiology)
    assert server.parse_dump_file(renamed_physiology_path)["type"] == "physiology_autopsy_blackbox"

    sensory_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 7)
        | (1 << 8)
    )
    sensory = bytearray(
        server.SENSORY_IMPAIRMENT_HEADER.pack(
            server.SENSORY_IMPAIRMENT_MAGIC,
            server.SENSORY_IMPAIRMENT_VERSION,
            server.SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY,
            server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
            9,
            server.SENSORY_IMPAIRMENT_SOURCE_HASH,
            1552,
        )
        + bytes(server.SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY * server.SENSORY_IMPAIRMENT_ENTRY_BYTES)
    )
    server.SENSORY_IMPAIRMENT_ENTRY.pack_into(
        sensory,
        server.SENSORY_IMPAIRMENT_HEADER_BYTES + 8 * server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
        0x3220000000000001,
        1551,
        (1 << 0),
        0.15,
        0.0,
        0.0,
        0.16,
        0.79,
        0.004,
        24.0,
        0.0,
        0.0,
        0.9,
        22.0,
        8,
    )
    server.SENSORY_IMPAIRMENT_ENTRY.pack_into(
        sensory,
        server.SENSORY_IMPAIRMENT_HEADER_BYTES + 9 * server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
        0x3220000000000002,
        1552,
        sensory_flags,
        0.82,
        0.64,
        180.0,
        0.05,
        7.2,
        0.065,
        88.0,
        0.24,
        12.5,
        0.45,
        310.0,
        9,
    )
    sensory_path = root / "Dump_SHINOBU_322.bin"
    sensory_path.write_bytes(sensory)
    parsed_sensory = server.parse_dump_file(sensory_path)
    assert parsed_sensory["type"] == "sensory_impairment_blackbox"
    assert parsed_sensory["version"] == server.SENSORY_IMPAIRMENT_VERSION
    assert parsed_sensory["entrySize"] == server.SENSORY_IMPAIRMENT_ENTRY_BYTES
    assert parsed_sensory["nonEmptyEntryCount"] == 2
    assert parsed_sensory["latest"]["frame"] == 1552
    assert parsed_sensory["latest"]["flagLabels"] == [
        "hypoxia-active",
        "narcosis-active",
        "latency-active",
        "complex-noise-admitted",
        "mock-toxicity",
        "nonfinite-sanitized",
        "over-budget",
        "csv-profile",
        "input-corrupted",
    ]
    assert parsed_sensory["latest"]["oxygenPartialPressureAtm"] == 0.05
    assert parsed_sensory["latest"]["inputLatencyMilliseconds"] == 180.0
    assert parsed_sensory["latest"]["inputCorrupted"] is True
    assert "nonfinite_sanitized" in parsed_sensory["warnings"]
    assert "over_budget" in parsed_sensory["warnings"]
    assert "hypoxia" in parsed_sensory["warnings"]
    assert "narcosis" in parsed_sensory["warnings"]
    assert "input_latency" in parsed_sensory["warnings"]
    assert "input_corrupted" in parsed_sensory["warnings"]
    assert "mock_toxicity" in parsed_sensory["warnings"]
    renamed_sensory_path = root / "Renamed_Header_S322HYPO.bin"
    renamed_sensory_path.write_bytes(sensory)
    assert server.parse_dump_file(renamed_sensory_path)["type"] == "sensory_impairment_blackbox"

    suit_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 8)
    suit_signal_flags = (1 << 3) | (1 << 8)
    suit = bytearray(
        server.SUIT_INTEGRITY_HEADER.pack(
            server.SUIT_INTEGRITY_MAGIC,
            server.SUIT_INTEGRITY_VERSION,
            server.SUIT_INTEGRITY_TELEMETRY_CAPACITY,
            server.SUIT_INTEGRITY_ENTRY_BYTES,
            11,
            server.SUIT_INTEGRITY_SOURCE_HASH,
            1602,
        )
        + bytes(server.SUIT_INTEGRITY_TELEMETRY_CAPACITY * server.SUIT_INTEGRITY_ENTRY_BYTES)
    )
    server.SUIT_INTEGRITY_ENTRY.pack_into(
        suit,
        server.SUIT_INTEGRITY_HEADER_BYTES + 10 * server.SUIT_INTEGRITY_ENTRY_BYTES,
        0x3230000000000001,
        1601,
        0x504C5952,
        120.0,
        13.0,
        0.15,
        0.05,
        0.82,
        0.1,
        42.0,
        1 << 0,
        0x53554954,
        0.1,
        0,
        0,
    )
    server.SUIT_INTEGRITY_ENTRY.pack_into(
        suit,
        server.SUIT_INTEGRITY_HEADER_BYTES + 11 * server.SUIT_INTEGRITY_ENTRY_BYTES,
        0x3230000000000002,
        1602,
        0x504C5952,
        900.0,
        91.0,
        1.25,
        0.88,
        0.04,
        0.75,
        135.5,
        suit_flags,
        0x53554954,
        0.1,
        suit_signal_flags,
        0,
    )
    suit_path = root / "Dump_SHINOBU_323.bin"
    suit_path.write_bytes(suit)
    parsed_suit = server.parse_dump_file(suit_path)
    assert parsed_suit["type"] == "suit_integrity_blackbox"
    assert parsed_suit["version"] == server.SUIT_INTEGRITY_VERSION
    assert parsed_suit["entrySize"] == server.SUIT_INTEGRITY_ENTRY_BYTES
    assert parsed_suit["nonEmptyEntryCount"] == 2
    assert parsed_suit["latest"]["frame"] == 1602
    assert parsed_suit["latest"]["flagLabels"] == [
        "initialized",
        "warning",
        "buckling",
        "imploded",
        "nonfinite-pressure",
        "over-budget",
        "acoustic-groan",
    ]
    assert parsed_suit["latest"]["signalFlagLabels"] == ["imploded", "acoustic-groan"]
    assert parsed_suit["latest"]["entityHashHex"] == "0x504C5952"
    assert parsed_suit["latest"]["equippedSuitHashHex"] == "0x53554954"
    assert parsed_suit["latest"]["imploded"] is True
    assert parsed_suit["latest"]["currentIntegrity01"] == 0.04
    assert "imploded" in parsed_suit["warnings"]
    assert "pressure_warning" in parsed_suit["warnings"]
    assert "buckling" in parsed_suit["warnings"]
    assert "nonfinite_pressure" in parsed_suit["warnings"]
    assert "over_budget" in parsed_suit["warnings"]
    assert "execution_over_100us" in parsed_suit["warnings"]
    assert "integrity_critical" in parsed_suit["warnings"]
    renamed_suit_path = root / "Renamed_Header_S323PRES.bin"
    renamed_suit_path.write_bytes(suit)
    assert server.parse_dump_file(renamed_suit_path)["type"] == "suit_integrity_blackbox"

    radiation_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 30)
        | (1 << 31)
    )
    radiation = bytearray(
        server.RADIATION_MUTATION_HEADER.pack(
            server.RADIATION_MUTATION_MAGIC,
            server.RADIATION_MUTATION_VERSION,
            server.RADIATION_MUTATION_TELEMETRY_CAPACITY,
            server.RADIATION_MUTATION_ENTRY_BYTES,
            13,
            server.RADIATION_MUTATION_SOURCE_HASH,
            1702,
        )
        + bytes(server.RADIATION_MUTATION_TELEMETRY_CAPACITY * server.RADIATION_MUTATION_ENTRY_BYTES)
    )
    server.RADIATION_MUTATION_ENTRY.pack_into(
        radiation,
        server.RADIATION_MUTATION_HEADER_BYTES + 12 * server.RADIATION_MUTATION_ENTRY_BYTES,
        0x3240000000000001,
        1701,
        (1 << 0) | (1 << 3),
        80.0,
        1.5,
        20.0,
        0.15,
        0.03,
        0.0,
        0.85,
        38.0,
        0.0,
        0.05,
        12,
        server.RADIATION_MUTATION_SOURCE_HASH,
    )
    server.RADIATION_MUTATION_ENTRY.pack_into(
        radiation,
        server.RADIATION_MUTATION_HEADER_BYTES + 13 * server.RADIATION_MUTATION_ENTRY_BYTES,
        0x3240000000000002,
        1702,
        radiation_flags,
        910.0,
        16.5,
        320.0,
        1.0,
        0.42,
        0.75,
        0.55,
        185.0,
        0.64,
        0.90,
        13,
        server.RADIATION_MUTATION_SOURCE_HASH,
    )
    radiation_path = root / "Dump_SHINOBU_324.bin"
    radiation_path.write_bytes(radiation)
    parsed_radiation = server.parse_dump_file(radiation_path)
    assert parsed_radiation["type"] == "radiation_mutation_blackbox"
    assert parsed_radiation["version"] == server.RADIATION_MUTATION_VERSION
    assert parsed_radiation["entrySize"] == server.RADIATION_MUTATION_ENTRY_BYTES
    assert parsed_radiation["nonEmptyEntryCount"] == 2
    assert parsed_radiation["latest"]["frame"] == 1702
    assert parsed_radiation["latest"]["flagLabels"] == [
        "active",
        "critical",
        "toxic-blood-vfx",
        "complex-noise-admitted",
        "metabolic-bridge-applied",
        "nonfinite-sanitized",
        "over-budget",
    ]
    assert parsed_radiation["latest"]["sourceHashHex"] == "0x53333234"
    assert parsed_radiation["latest"]["critical"] is True
    assert parsed_radiation["latest"]["mutationSeverity01"] == 1.0
    assert "critical" in parsed_radiation["warnings"]
    assert "nonfinite_sanitized" in parsed_radiation["warnings"]
    assert "over_budget" in parsed_radiation["warnings"]
    assert "mutation_severity_max" in parsed_radiation["warnings"]
    assert "fatal_dose_reached" in parsed_radiation["warnings"]
    assert "metabolic_toxicity" in parsed_radiation["warnings"]
    renamed_radiation_path = root / "Renamed_Header_S324MUTA.bin"
    renamed_radiation_path.write_bytes(radiation)
    assert server.parse_dump_file(renamed_radiation_path)["type"] == "radiation_mutation_blackbox"

    toxic_flags = (1 << 0) | (1 << 5) | (1 << 6) | (1 << 7)
    toxic_payload_bytes = 2 * server.TOXIC_OUTGASSING_ENTRY_BYTES
    toxic = bytearray(
        server.TOXIC_OUTGASSING_HEADER.pack(
            server.TOXIC_OUTGASSING_MAGIC,
            server.TOXIC_OUTGASSING_VERSION,
            server.TOXIC_OUTGASSING_HEADER_BYTES,
            server.TOXIC_OUTGASSING_ENTRY_BYTES,
            server.TOXIC_OUTGASSING_TELEMETRY_CAPACITY,
            2,
            2,
            toxic_payload_bytes,
        )
        + bytes(toxic_payload_bytes)
    )
    server.TOXIC_OUTGASSING_ENTRY.pack_into(
        toxic,
        server.TOXIC_OUTGASSING_HEADER_BYTES,
        1.0,
        2.0,
        3.0,
        0.12,
        5.5,
        0.8,
        0.45,
        0xABCDEF01,
        1901,
        16,
        2,
        3,
        1 << 0,
        0,
        0,
    )
    server.TOXIC_OUTGASSING_ENTRY.pack_into(
        toxic,
        server.TOXIC_OUTGASSING_HEADER_BYTES + server.TOXIC_OUTGASSING_ENTRY_BYTES,
        4.0,
        5.0,
        6.0,
        0.75,
        12.5,
        0.65,
        18.25,
        0xDEAD1234,
        1902,
        32,
        5,
        7,
        toxic_flags,
        1,
        0,
    )
    toxic_path = root / "Dump_TOXIC_SURGEON.bin"
    toxic_path.write_bytes(toxic)
    parsed_toxic = server.parse_dump_file(toxic_path)
    assert parsed_toxic["type"] == "toxic_outgassing_blackbox"
    assert parsed_toxic["version"] == server.TOXIC_OUTGASSING_VERSION
    assert parsed_toxic["entrySize"] == server.TOXIC_OUTGASSING_ENTRY_BYTES
    assert parsed_toxic["declaredCapacity"] == server.TOXIC_OUTGASSING_TELEMETRY_CAPACITY
    assert parsed_toxic["declaredEntryCount"] == 2
    assert parsed_toxic["latest"]["frame"] == 1902
    assert parsed_toxic["latest"]["activeResolution"] == 32
    assert parsed_toxic["latest"]["maxDensity"] == 0.75
    assert parsed_toxic["latest"]["stateHashHex"] == "0xDEAD1234"
    assert parsed_toxic["latest"]["flagLabels"] == [
        "mock-chemistry",
        "binary-probe-failure",
        "dump-failure",
        "nan",
    ]
    assert parsed_toxic["latest"]["nanDetected"] is True
    assert "nan_detected" in parsed_toxic["warnings"]
    assert "dump_failure" in parsed_toxic["warnings"]
    assert "binary_probe_failure" in parsed_toxic["warnings"]
    assert "mock_chemistry" in parsed_toxic["warnings"]
    renamed_toxic_path = root / "Renamed_ToxicMagic.bin"
    renamed_toxic_path.write_bytes(toxic)
    assert server.parse_dump_file(renamed_toxic_path)["type"] == "toxic_outgassing_blackbox"

    gas_flags = (1 << 1) | (1 << 2)
    gas_failure_flags = server.GAS_DYNAMICS_FAILURE_FLAG | 3
    gas = bytearray(
        server.GAS_DYNAMICS_HEADER.pack(
            server.GAS_DYNAMICS_MAGIC,
            server.GAS_DYNAMICS_VERSION,
            server.GAS_DYNAMICS_ENTRY_BYTES,
            server.GAS_DYNAMICS_TELEMETRY_CAPACITY,
            2,
            99,
        )
        + bytes(server.GAS_DYNAMICS_TELEMETRY_CAPACITY * server.GAS_DYNAMICS_ENTRY_BYTES)
    )
    server.GAS_DYNAMICS_ENTRY.pack_into(
        gas,
        server.GAS_DYNAMICS_HEADER_BYTES,
        (0x00012345 << 32) | 0x00000007,
        2001,
        8,
        150.0,
        5.25,
        612.5,
        105.75,
        0x13572468,
        74420,
        7,
        12,
        0,
        33.5,
        0,
        gas_flags,
        2,
    )
    server.GAS_DYNAMICS_ENTRY.pack_into(
        gas,
        server.GAS_DYNAMICS_HEADER_BYTES + server.GAS_DYNAMICS_ENTRY_BYTES,
        (0x00012345 << 32) | 0x00000007,
        2002,
        8,
        0.0,
        0.0,
        0.0,
        0.0,
        0x24681357,
        74420,
        7,
        13,
        1,
        12.5,
        0,
        gas_failure_flags,
        0,
    )
    gas_path = root / "Dump_1324_SubmarineAtmosphere.bin"
    gas_path.write_bytes(gas)
    parsed_gas = server.parse_dump_file(gas_path)
    assert parsed_gas["type"] == "gas_dynamics_blackbox"
    assert parsed_gas["version"] == server.GAS_DYNAMICS_VERSION
    assert parsed_gas["entrySize"] == server.GAS_DYNAMICS_ENTRY_BYTES
    assert parsed_gas["declaredEntryCount"] == server.GAS_DYNAMICS_TELEMETRY_CAPACITY
    assert parsed_gas["nonEmptyEntryCount"] == 2
    assert parsed_gas["latest"]["frame"] == 2002
    assert parsed_gas["latest"]["flagLabels"] == ["failure", "state-write-lock"]
    assert parsed_gas["latest"]["failureCode"] == 3
    assert parsed_gas["latest"]["nanDetected"] is False
    assert parsed_gas["entries"][0]["flagLabels"] == ["breach", "hibernating"]
    assert parsed_gas["entries"][0]["sleepingRoomCount"] == 2
    assert "failure" in parsed_gas["warnings"]
    assert "state_write_lock" in parsed_gas["warnings"]
    assert "breach" in parsed_gas["warnings"]
    assert "hibernating" in parsed_gas["warnings"]
    assert "dropped_updates" in parsed_gas["warnings"]
    renamed_gas_path = root / "Renamed_GasDynamicsMagic.bin"
    renamed_gas_path.write_bytes(gas)
    assert server.parse_dump_file(renamed_gas_path)["type"] == "gas_dynamics_blackbox"

    atmosphere_faults = (1 << 2) | (1 << 4) | (1 << 5) | (1 << 7)
    atmosphere = bytearray(
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER.pack(
            server.BASE_ATMOSPHERE_LOGISTICS_MAGIC,
            server.BASE_ATMOSPHERE_LOGISTICS_VERSION,
            server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY,
        )
        + bytes(
            server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY
            * server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES
        )
    )
    server.BASE_ATMOSPHERE_LOGISTICS_ENTRY.pack_into(
        atmosphere,
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES,
        0x1111222233334444,
        0.2095,
        0.00042,
        0.79008,
        0.0,
        20.0,
        3001,
        12,
        34,
        3,
        2,
        640,
        9,
        0,
        12000000,
    )
    server.BASE_ATMOSPHERE_LOGISTICS_ENTRY.pack_into(
        atmosphere,
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES + server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES,
        0x5555666677778888,
        0.18,
        0.045,
        0.76,
        0.115,
        31.25,
        3002,
        12,
        34,
        3,
        4,
        980,
        16,
        atmosphere_faults,
        13100000,
    )
    atmosphere_path = root / "Dump_SHINOBU_221.bin"
    atmosphere_path.write_bytes(atmosphere)
    parsed_atmosphere = server.parse_dump_file(atmosphere_path)
    assert parsed_atmosphere["type"] == "base_atmosphere_logistics_blackbox"
    assert parsed_atmosphere["version"] == server.BASE_ATMOSPHERE_LOGISTICS_VERSION
    assert parsed_atmosphere["entrySize"] == server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES
    assert parsed_atmosphere["declaredEntryCount"] == server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY
    assert parsed_atmosphere["nonEmptyEntryCount"] == 2
    assert parsed_atmosphere["latest"]["frame"] == 3002
    assert parsed_atmosphere["latest"]["nodeCount"] == 12
    assert parsed_atmosphere["latest"]["sourceCount"] == 4
    assert parsed_atmosphere["latest"]["averageOxygen01"] == 0.18
    assert parsed_atmosphere["latest"]["maxToxin01"] == 0.115
    assert parsed_atmosphere["latest"]["faultLabels"] == [
        "nonfinite-gas",
        "csr-overflow",
        "source-overflow",
        "nan",
    ]
    assert "nonfinite_gas" in parsed_atmosphere["warnings"]
    assert "csr_overflow" in parsed_atmosphere["warnings"]
    assert "source_overflow" in parsed_atmosphere["warnings"]
    assert "nan_detected" in parsed_atmosphere["warnings"]
    renamed_atmosphere_path = root / "Renamed_BaseAtmosphereMagic.bin"
    renamed_atmosphere_path.write_bytes(atmosphere)
    assert server.parse_dump_file(renamed_atmosphere_path)["type"] == "base_atmosphere_logistics_blackbox"

    storm_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    storm = bytearray(
        server.STORM_PROPAGATION_HEADER.pack(
            server.STORM_PROPAGATION_MAGIC,
            storm_flags,
            1,
            server.STORM_PROPAGATION_TELEMETRY_CAPACITY,
            server.STORM_PROPAGATION_ENTRY_BYTES,
            server.STORM_PROPAGATION_SOURCE_HASH,
            0xABC12345,
            0,
        )
        + bytes(server.STORM_PROPAGATION_TELEMETRY_CAPACITY * server.STORM_PROPAGATION_ENTRY_BYTES)
    )
    server.STORM_PROPAGATION_ENTRY.pack_into(
        storm,
        server.STORM_PROPAGATION_HEADER_BYTES,
        4001,
        (1 << 1) | (1 << 2) | (1 << 5),
        0.42,
        250.0,
        0.29,
        1.5,
        0.67,
        0.22,
        0.1,
        0.0,
        -0.2,
        0.85,
        35.5,
        0.25,
        0x13572468,
        5,
    )
    server.STORM_PROPAGATION_ENTRY.pack_into(
        storm,
        server.STORM_PROPAGATION_HEADER_BYTES + server.STORM_PROPAGATION_ENTRY_BYTES,
        4002,
        storm_flags,
        0.91,
        1400.0,
        0.73,
        2.8,
        0.44,
        0.62,
        0.4,
        0.0,
        -0.6,
        0.65,
        52.25,
        0.42,
        0x24681357,
        8,
    )
    storm_path = root / "Dump_SHINOBU_234.bin"
    storm_path.write_bytes(storm)
    parsed_storm = server.parse_dump_file(storm_path)
    assert parsed_storm["type"] == "storm_propagation_blackbox"
    assert parsed_storm["entrySize"] == server.STORM_PROPAGATION_ENTRY_BYTES
    assert parsed_storm["declaredEntryCount"] == server.STORM_PROPAGATION_TELEMETRY_CAPACITY
    assert parsed_storm["sourceHashHex"] == "0x53483234"
    assert parsed_storm["stateHashHex"] == "0xABC12345"
    assert parsed_storm["reasonFlagLabels"] == [
        "nonfinite",
        "mock-weather",
        "fog",
        "biolum",
        "audio",
        "flow",
    ]
    assert parsed_storm["nonEmptyEntryCount"] == 2
    assert parsed_storm["latest"]["frame"] == 4002
    assert parsed_storm["latest"]["flagLabels"] == [
        "nonfinite",
        "mock-weather",
        "fog",
        "biolum",
        "audio",
        "flow",
    ]
    assert parsed_storm["latest"]["noiseOctaveCount"] == 8
    assert parsed_storm["latest"]["stateHashHex"] == "0x24681357"
    assert "nonfinite" in parsed_storm["warnings"]
    assert "mock_weather" in parsed_storm["warnings"]
    assert "fog_published" in parsed_storm["warnings"]
    assert "biolum_published" in parsed_storm["warnings"]
    assert "audio_published" in parsed_storm["warnings"]
    assert "flow_published" in parsed_storm["warnings"]
    renamed_storm_path = root / "Renamed_StormPropagationMagic.bin"
    renamed_storm_path.write_bytes(storm)
    assert server.parse_dump_file(renamed_storm_path)["type"] == "storm_propagation_blackbox"

    ocean = bytearray(
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER.pack(
            server.OCEAN_SURFACE_ATMOSPHERE_MAGIC,
            server.OCEAN_SURFACE_ATMOSPHERE_MARKER,
            server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY,
            server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES,
            0xCAFEBABE,
            2,
            0,
            0,
        )
        + bytes(
            server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY
            * server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES
        )
    )
    server.OCEAN_SURFACE_ATMOSPHERE_ENTRY.pack_into(
        ocean,
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES,
        5001,
        0,
        1.25,
        0.35,
        120000,
        0.9,
        4,
        0.22,
        0.11,
        0.0,
        1.0,
        0.0,
        0x11112222,
        2,
        32,
    )
    server.OCEAN_SURFACE_ATMOSPHERE_ENTRY.pack_into(
        ocean,
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES + server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES,
        5002,
        1 << 0,
        3.75,
        0.88,
        server.OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS + 10,
        0.65,
        6,
        0.71,
        0.49,
        0.1,
        0.98,
        -0.12,
        0x33334444,
        6,
        48,
    )
    ocean_path = root / "Dump_SHINOBU_147.bin"
    ocean_path.write_bytes(ocean)
    parsed_ocean = server.parse_dump_file(ocean_path)
    assert parsed_ocean["type"] == "ocean_surface_atmosphere_blackbox"
    assert parsed_ocean["entrySize"] == server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES
    assert parsed_ocean["declaredEntryCount"] == server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY
    assert parsed_ocean["stateHashHex"] == "0xCAFEBABE"
    assert parsed_ocean["telemetryCursor"] == 2
    assert parsed_ocean["nonEmptyEntryCount"] == 2
    assert parsed_ocean["latest"]["frame"] == 5002
    assert parsed_ocean["latest"]["flagLabels"] == ["latency-or-budget"]
    assert parsed_ocean["latest"]["waveComputeTimeNs"] == server.OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS + 10
    assert parsed_ocean["latest"]["readbackLatencyFrames"] == 6
    assert parsed_ocean["latest"]["stateHashHex"] == "0x33334444"
    assert "latency_or_budget" in parsed_ocean["warnings"]
    assert "readback_latency" in parsed_ocean["warnings"]
    assert "wave_compute_over_budget" in parsed_ocean["warnings"]
    renamed_ocean_path = root / "Renamed_OceanSurfaceMagic.bin"
    renamed_ocean_path.write_bytes(ocean)
    assert server.parse_dump_file(renamed_ocean_path)["type"] == "ocean_surface_atmosphere_blackbox"

    thermo_hazard_flags = (1 << 0) | (1 << 2) | (1 << 4)
    thermo_hazard = bytearray(
        server.THERMODYNAMICS_HAZARD_HEADER.pack(
            server.THERMODYNAMICS_HAZARD_MAGIC,
            server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY,
            server.THERMODYNAMICS_HAZARD_ENTRY_BYTES,
            2,
        )
        + bytes(server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY * server.THERMODYNAMICS_HAZARD_ENTRY_BYTES)
    )
    server.THERMODYNAMICS_HAZARD_ENTRY.pack_into(
        thermo_hazard,
        server.THERMODYNAMICS_HAZARD_HEADER_BYTES,
        42.0,
        0.25,
        1.5,
        10.0,
        20.0,
        30.0,
        6001,
        8,
        3,
        0,
        0,
        0xFFFFFFFF,
        32,
        0x10101010,
        220,
        200,
        0,
        0,
    )
    server.THERMODYNAMICS_HAZARD_ENTRY.pack_into(
        thermo_hazard,
        server.THERMODYNAMICS_HAZARD_HEADER_BYTES + server.THERMODYNAMICS_HAZARD_ENTRY_BYTES,
        185.5,
        4.25,
        6.75,
        40.0,
        50.0,
        60.0,
        6002,
        9,
        7,
        thermo_hazard_flags,
        5,
        123,
        16,
        0x20202020,
        128,
        64,
        0,
        0,
    )
    thermo_hazard_path = root / "Dump_THERMODYNAMICS.bin"
    thermo_hazard_path.write_bytes(thermo_hazard)
    parsed_thermo_hazard = server.parse_dump_file(thermo_hazard_path)
    assert parsed_thermo_hazard["type"] == "thermodynamics_hazard_blackbox"
    assert parsed_thermo_hazard["entrySize"] == server.THERMODYNAMICS_HAZARD_ENTRY_BYTES
    assert parsed_thermo_hazard["declaredEntryCount"] == server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY
    assert parsed_thermo_hazard["telemetryCursor"] == 2
    assert parsed_thermo_hazard["nonEmptyEntryCount"] == 2
    assert parsed_thermo_hazard["latest"]["frame"] == 6002
    assert parsed_thermo_hazard["latest"]["flagLabels"] == ["nan", "rebase", "signal-drop"]
    assert parsed_thermo_hazard["latest"]["gridOriginHashHex"] == "0x20202020"
    assert parsed_thermo_hazard["latest"]["qualityPressureQ8"] == 128
    assert parsed_thermo_hazard["latest"]["healthPressureQ8"] == 64
    assert "nan_detected" in parsed_thermo_hazard["warnings"]
    assert "rebase" in parsed_thermo_hazard["warnings"]
    assert "signal_drop" in parsed_thermo_hazard["warnings"]
    renamed_thermo_hazard_path = root / "Renamed_ThermoHazardMagic.bin"
    renamed_thermo_hazard_path.write_bytes(thermo_hazard)
    assert server.parse_dump_file(renamed_thermo_hazard_path)["type"] == "thermodynamics_hazard_blackbox"
    thermo_surgeon_headered_path = root / "Dump_THERMO_SURGEON.bin"
    thermo_surgeon_headered_path.write_bytes(thermo_hazard)
    assert server.parse_dump_file(thermo_surgeon_headered_path)["type"] == "thermodynamics_hazard_blackbox"

    abyssal_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    abyssal = bytearray(
        bytes(server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY * server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES)
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        0,
        80.0,
        10000.0,
        9990.0,
        220.0,
        10.0,
        20.0,
        30.0,
        7001,
        1 << 2,
        3,
        6,
        0xFFFFFFFF,
        32,
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES,
        1250.0,
        20000.0,
        20575.0,
        980.0,
        40.0,
        50.0,
        60.0,
        7002,
        abyssal_flags,
        7,
        12,
        12345,
        16,
    )
    abyssal_path = root / "Dump_SHINOBU_203.bin"
    abyssal_path.write_bytes(abyssal)
    parsed_abyssal = server.parse_dump_file(abyssal_path)
    assert parsed_abyssal["type"] == "abyssal_thermodynamics_blackbox"
    assert parsed_abyssal["entrySize"] == server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES
    assert parsed_abyssal["declaredEntryCount"] == server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY
    assert parsed_abyssal["nonEmptyEntryCount"] == 2
    assert parsed_abyssal["latest"]["frame"] == 7002
    assert parsed_abyssal["latest"]["flagLabels"] == [
        "nan",
        "shift",
        "mock-sources",
        "energy-drift",
        "divergent",
        "max-iterations",
    ]
    assert parsed_abyssal["latest"]["energyDelta"] == 575.0
    assert parsed_abyssal["latest"]["nanCellIndex"] == 12345
    assert "nan_detected" in parsed_abyssal["warnings"]
    assert "shift" in parsed_abyssal["warnings"]
    assert "mock_sources" in parsed_abyssal["warnings"]
    assert "energy_drift" in parsed_abyssal["warnings"]
    assert "divergent" in parsed_abyssal["warnings"]
    assert "max_iterations" in parsed_abyssal["warnings"]
    thermo_surgeon_headered_path.write_bytes(abyssal)
    assert server.parse_dump_file(thermo_surgeon_headered_path)["type"] == "abyssal_thermodynamics_blackbox"

    abyssal_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    abyssal = bytearray(
        bytes(server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY * server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES)
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        0,
        80.0,
        10000.0,
        9990.0,
        220.0,
        10.0,
        20.0,
        30.0,
        7001,
        1 << 2,
        3,
        6,
        0xFFFFFFFF,
        32,
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES,
        1250.0,
        20000.0,
        20575.0,
        980.0,
        40.0,
        50.0,
        60.0,
        7002,
        abyssal_flags,
        7,
        12,
        12345,
        16,
    )
    abyssal_path = root / "Dump_SHINOBU_203.bin"
    abyssal_path.write_bytes(abyssal)
    parsed_abyssal = server.parse_dump_file(abyssal_path)
    assert parsed_abyssal["type"] == "abyssal_thermodynamics_blackbox"
    assert parsed_abyssal["entrySize"] == server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES
    assert parsed_abyssal["declaredEntryCount"] == server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY
    assert parsed_abyssal["nonEmptyEntryCount"] == 2
    assert parsed_abyssal["latest"]["frame"] == 7002
    assert parsed_abyssal["latest"]["flagLabels"] == [
        "nan",
        "shift",
        "mock-sources",
        "energy-drift",
        "divergent",
        "max-iterations",
    ]
    assert parsed_abyssal["latest"]["energyDelta"] == 575.0
    assert parsed_abyssal["latest"]["nanCellIndex"] == 12345
    assert "nan_detected" in parsed_abyssal["warnings"]
    assert "shift" in parsed_abyssal["warnings"]
    assert "mock_sources" in parsed_abyssal["warnings"]
    assert "energy_drift" in parsed_abyssal["warnings"]
    assert "divergent" in parsed_abyssal["warnings"]
    assert "max_iterations" in parsed_abyssal["warnings"]
    thermo_surgeon_raw_path = root / "Dump_THERMO_SURGEON.bin"
    thermo_surgeon_raw_path.write_bytes(abyssal)
    assert server.parse_dump_file(thermo_surgeon_raw_path)["type"] == "abyssal_thermodynamics_blackbox"

    reactor_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
    )
    reactor_thermal = bytearray(
        bytes(server.REACTOR_THERMAL_TELEMETRY_CAPACITY * server.REACTOR_THERMAL_ENTRY_BYTES)
    )
    server.REACTOR_THERMAL_ENTRY.pack_into(
        reactor_thermal,
        0,
        1.0,
        2.0,
        3.0,
        100000.0,
        750.0,
        1600.0,
        18.5,
        240.0,
        3,
        1,
        1 << 6,
        8001,
        0x11111111,
        0x22222222,
        9,
        0,
        5,
        4,
        0,
        0x33333333,
        0x44444444,
    )
    server.REACTOR_THERMAL_ENTRY.pack_into(
        reactor_thermal,
        server.REACTOR_THERMAL_ENTRY_BYTES,
        4.0,
        5.0,
        6.0,
        250000.0,
        900.0,
        2200.0,
        25.0,
        310.0,
        4,
        2,
        reactor_flags,
        8002,
        0x55555555,
        0x66666666,
        17,
        1,
        7,
        6,
        1,
        0x77777777,
        0x88888888,
    )
    reactor_path = root / "Dump_SHINOBU_337.bin"
    reactor_path.write_bytes(reactor_thermal)
    parsed_reactor = server.parse_dump_file(reactor_path)
    assert parsed_reactor["type"] == "reactor_thermal_blackbox"
    assert parsed_reactor["entrySize"] == server.REACTOR_THERMAL_ENTRY_BYTES
    assert parsed_reactor["declaredEntryCount"] == server.REACTOR_THERMAL_TELEMETRY_CAPACITY
    assert parsed_reactor["nonEmptyEntryCount"] == 2
    assert parsed_reactor["latest"]["frame"] == 8002
    assert parsed_reactor["latest"]["flagLabels"] == [
        "nonfinite",
        "out-of-grid",
        "meltdown",
        "mock-load",
        "cost-over-budget",
        "signal-overflow-risk",
        "timing-proxy",
    ]
    assert parsed_reactor["latest"]["hotReactorHashHex"] == "0x77777777"
    assert parsed_reactor["latest"]["hotEntityHashHex"] == "0x88888888"
    assert parsed_reactor["latest"]["injectionCellWrites"] == 17
    assert "nonfinite" in parsed_reactor["warnings"]
    assert "out_of_grid" in parsed_reactor["warnings"]
    assert "meltdown" in parsed_reactor["warnings"]
    assert "mock_load" in parsed_reactor["warnings"]
    assert "cost_over_budget" in parsed_reactor["warnings"]
    assert "signal_overflow_risk" in parsed_reactor["warnings"]

    nuclear_flags = (
        (1 << 0)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 7)
        | (1 << 8)
    )
    nuclear_reactor = bytearray(
        bytes(server.REACTOR_THERMAL_TELEMETRY_CAPACITY * server.NUCLEAR_REACTOR_THERMAL_ENTRY_BYTES)
    )
    server.NUCLEAR_REACTOR_THERMAL_ENTRY.pack_into(
        nuclear_reactor,
        0,
        7.0,
        8.0,
        9.0,
        42000000.0,
        12.5,
        940.0,
        2100.0,
        180.0,
        0.42,
        2,
        1,
        1 << 6,
        9001,
        0x10101010,
        0x20202020,
        0x30303030,
        2,
        1,
        0,
        0,
        0,
    )
    server.NUCLEAR_REACTOR_THERMAL_ENTRY.pack_into(
        nuclear_reactor,
        server.NUCLEAR_REACTOR_THERMAL_ENTRY_BYTES,
        10.0,
        11.0,
        12.0,
        84000000.0,
        25.0,
        1250.0,
        2600.0,
        275.0,
        0.55,
        3,
        2,
        nuclear_flags,
        9002,
        0x40404040,
        0x50505050,
        0x60606060,
        4,
        3,
        1,
        1,
        1,
    )
    nuclear_path = root / "Dump_SHINOBU_342.bin"
    nuclear_path.write_bytes(nuclear_reactor)
    parsed_nuclear = server.parse_dump_file(nuclear_path)
    assert parsed_nuclear["type"] == "nuclear_reactor_thermal_blackbox"
    assert parsed_nuclear["entrySize"] == server.NUCLEAR_REACTOR_THERMAL_ENTRY_BYTES
    assert parsed_nuclear["declaredEntryCount"] == server.REACTOR_THERMAL_TELEMETRY_CAPACITY
    assert parsed_nuclear["nonEmptyEntryCount"] == 2
    assert parsed_nuclear["latest"]["frame"] == 9002
    assert parsed_nuclear["latest"]["flagLabels"] == [
        "nonfinite",
        "meltdown",
        "mock-load",
        "cost-over-budget",
        "signal-overflow-risk",
        "timing-proxy",
        "no-coolant",
        "atomic-abort",
    ]
    assert parsed_nuclear["latest"]["powerNodeHashHex"] == "0x50505050"
    assert parsed_nuclear["latest"]["fluidRoomHashHex"] == "0x60606060"
    assert parsed_nuclear["latest"]["radiationSignalCount"] == 4
    assert parsed_nuclear["latest"]["baseCompromiseSignalCount"] == 3
    assert "nonfinite" in parsed_nuclear["warnings"]
    assert "meltdown" in parsed_nuclear["warnings"]
    assert "mock_load" in parsed_nuclear["warnings"]
    assert "cost_over_budget" in parsed_nuclear["warnings"]
    assert "signal_overflow_risk" in parsed_nuclear["warnings"]
    assert "no_coolant" in parsed_nuclear["warnings"]
    assert "atomic_abort" in parsed_nuclear["warnings"]

    respawn_flags = (
        (1 << 0)
        | (1 << 2)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 10)
        | (3 << server.RESPAWN_RECONCILIATION_DROPPED_ITEM_SHIFT)
        | (1 << 31)
    )
    respawn = bytearray(
        server.RESPAWN_RECONCILIATION_HEADER.pack(
            server.RESPAWN_RECONCILIATION_MAGIC,
            server.RESPAWN_RECONCILIATION_VERSION,
            server.RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY,
            14,
            (1 << 5) | (1 << 31),
        )
        + bytes(server.RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY * server.RESPAWN_RECONCILIATION_ENTRY_BYTES)
    )
    server.RESPAWN_RECONCILIATION_ENTRY.pack_into(
        respawn,
        server.RESPAWN_RECONCILIATION_HEADER_BYTES + 13 * server.RESPAWN_RECONCILIATION_ENTRY_BYTES,
        100.0,
        200.0,
        300.0,
        110.0,
        210.0,
        310.0,
        0xDEAD0001,
        1801,
        64.5,
        (1 << 0) | (1 << 1),
    )
    server.RESPAWN_RECONCILIATION_ENTRY.pack_into(
        respawn,
        server.RESPAWN_RECONCILIATION_HEADER_BYTES + 14 * server.RESPAWN_RECONCILIATION_ENTRY_BYTES,
        101.0,
        201.0,
        301.0,
        0.0,
        0.0,
        0.0,
        0xDEAD0002,
        1802,
        155.25,
        respawn_flags,
    )
    respawn_path = root / "Dump_SHINOBU_329.bin"
    respawn_path.write_bytes(respawn)
    parsed_respawn = server.parse_dump_file(respawn_path)
    assert parsed_respawn["type"] == "respawn_reconciliation_blackbox"
    assert parsed_respawn["version"] == server.RESPAWN_RECONCILIATION_VERSION
    assert parsed_respawn["entrySize"] == server.RESPAWN_RECONCILIATION_ENTRY_BYTES
    assert parsed_respawn["nonEmptyEntryCount"] == 2
    assert parsed_respawn["reasonFlagLabels"] == ["invalid-target-aup", "nan-detected"]
    assert parsed_respawn["latest"]["frame"] == 1802
    assert parsed_respawn["latest"]["causeHashHex"] == "0xDEAD0002"
    assert parsed_respawn["latest"]["droppedItemCount"] == 3
    assert parsed_respawn["latest"]["flagLabels"] == [
        "respawn-active",
        "penalty-applied",
        "fallback-lifepod",
        "invalid-target-aup",
        "committed",
        "death-sequence-blackout-primed",
        "nan-detected",
    ]
    assert parsed_respawn["latest"]["nanDetected"] is True
    assert parsed_respawn["latest"]["fallbackLifepod"] is True
    assert "reason_flags" in parsed_respawn["warnings"]
    assert "nan_detected" in parsed_respawn["warnings"]
    assert "invalid_target_aup" in parsed_respawn["warnings"]
    assert "fallback_lifepod" in parsed_respawn["warnings"]
    assert "penalty_applied" in parsed_respawn["warnings"]
    assert "committed" in parsed_respawn["warnings"]
    assert "dropped_items" in parsed_respawn["warnings"]
    renamed_respawn_path = root / "Renamed_Header_RSPNSRGE.bin"
    renamed_respawn_path.write_bytes(respawn)
    assert server.parse_dump_file(renamed_respawn_path)["type"] == "respawn_reconciliation_blackbox"

    pda_frequency = bytearray(
        server.PDA_FREQUENCY_TUNING_HEADER.pack(server.PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY, 2)
        + bytes(server.PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY * server.PDA_FREQUENCY_TUNING_ENTRY_BYTES)
    )
    server.PDA_FREQUENCY_TUNING_ENTRY.pack_into(
        pda_frequency,
        server.PDA_FREQUENCY_TUNING_HEADER_BYTES,
        820,
        0x534F5648,
        1.25,
        0.42,
        1.22,
        0.40,
        0.08,
        250,
        0,
        1 << 0,
    )
    server.PDA_FREQUENCY_TUNING_ENTRY.pack_into(
        pda_frequency,
        server.PDA_FREQUENCY_TUNING_HEADER_BYTES + server.PDA_FREQUENCY_TUNING_ENTRY_BYTES,
        821,
        0x534F5648,
        2.10,
        0.65,
        2.10,
        0.65,
        0.01,
        1000,
        2,
        (1 << 0) | (1 << 1) | (1 << 2),
    )
    pda_frequency_path = root / "Dump_MINIGAME_FREQUENCY_TUNING.bin"
    pda_frequency_path.write_bytes(pda_frequency)
    parsed_pda_frequency = server.parse_dump_file(pda_frequency_path)
    assert parsed_pda_frequency["type"] == "pda_frequency_tuning_blackbox"
    assert parsed_pda_frequency["latest"]["frame"] == 821
    assert parsed_pda_frequency["latest"]["artifactHashHex"] == "0x534F5648"
    assert parsed_pda_frequency["latest"]["stage"] == 2
    assert parsed_pda_frequency["latest"]["holdPermille"] == 1000
    assert parsed_pda_frequency["latest"]["flagLabels"] == ["stage-0-locked", "stage-1-locked", "stage-2-locked"]
    assert parsed_pda_frequency["latest"]["allStagesLocked"] is True
    assert "all_stages_locked" in parsed_pda_frequency["warnings"]

    compass_flags = (1 << 1) | (1 << 2) | (1 << 5) | (1 << 6) | (1 << 9)
    compass = bytearray(
        server.COMPASS_GYRO_HEADER.pack(server.COMPASS_GYRO_MAGIC, server.COMPASS_GYRO_BLACKBOX_CAPACITY, 1)
        + bytes(server.COMPASS_GYRO_BLACKBOX_CAPACITY * server.COMPASS_GYRO_ENTRY_BYTES)
    )
    server.COMPASS_GYRO_ENTRY.pack_into(
        compass,
        server.COMPASS_GYRO_HEADER_BYTES,
        900,
        181.5,
        180.0,
        1.5,
        3.0,
        0.25,
        0.95,
        1 << 1,
        17,
        2,
    )
    server.COMPASS_GYRO_ENTRY.pack_into(
        compass,
        server.COMPASS_GYRO_HEADER_BYTES + server.COMPASS_GYRO_ENTRY_BYTES,
        901,
        90.0,
        102.5,
        12.5,
        8.0,
        0.92,
        0.44,
        compass_flags,
        19,
        3,
    )
    compass_path = root / "Dump_COMPASS_GYRO_STABILIZER.bin"
    compass_path.write_bytes(compass)
    parsed_compass = server.parse_dump_file(compass_path)
    assert parsed_compass["type"] == "compass_gyro_blackbox"
    assert parsed_compass["latest"]["frame"] == 901
    assert parsed_compass["latest"]["flagLabels"] == [
        "powered",
        "anomaly-unstable",
        "nonfinite-fallback",
        "reduced-quality-noise",
        "calibration-requested",
    ]
    assert parsed_compass["latest"]["calibrationCount"] == 3
    assert "nonfinite" in parsed_compass["warnings"]
    assert "anomaly_unstable" in parsed_compass["warnings"]
    assert "reduced_quality_noise" in parsed_compass["warnings"]
    assert "drift_over_max" in parsed_compass["warnings"]

    pda_encyclopedia = bytearray(
        server.PDA_ENCYCLOPEDIA_HEADER.pack(
            server.PDA_ENCYCLOPEDIA_MAGIC,
            911,
            0x54455854,
            server.PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY,
            server.PDA_ENCYCLOPEDIA_ENTRY_BYTES,
            0xAEC57EAC,
        )
        + bytes(server.PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY * server.PDA_ENCYCLOPEDIA_ENTRY_BYTES)
    )
    pda_encyclopedia_flags = 5 | (4 << 8) | server.PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG
    pda_encyclopedia_fields = (
        910,
        0,
        0xAEC57EAC,
        12,
        32,
        256,
        512,
        2048,
        1200,
        1500,
        pda_encyclopedia_flags,
        0x54455854,
        256,
        4096,
    )
    pda_encyclopedia_hash = server.compute_pda_encyclopedia_state_hash(pda_encyclopedia_fields)
    server.PDA_ENCYCLOPEDIA_ENTRY.pack_into(
        pda_encyclopedia,
        server.PDA_ENCYCLOPEDIA_HEADER_BYTES,
        *(
            pda_encyclopedia_fields[0],
            pda_encyclopedia_hash,
            *pda_encyclopedia_fields[2:],
        ),
    )
    pda_encyclopedia_path = root / "Dump_PDAEncyclopediaStreamer_BlackBox.bin"
    pda_encyclopedia_path.write_bytes(pda_encyclopedia)
    parsed_pda_encyclopedia = server.parse_dump_file(pda_encyclopedia_path)
    assert parsed_pda_encyclopedia["type"] == "pda_encyclopedia_blackbox"
    assert parsed_pda_encyclopedia["latest"]["frame"] == 910
    assert parsed_pda_encyclopedia["latest"]["stateHashOk"] is True
    assert parsed_pda_encyclopedia["latest"]["entryHashHex"] == "0xAEC57EAC"
    assert parsed_pda_encyclopedia["latest"]["streamStateLabel"] == "fault"
    assert parsed_pda_encyclopedia["latest"]["sourceLabel"] == "data-monolith"
    assert parsed_pda_encyclopedia["latest"]["flagLabels"] == [
        "stream-fault",
        "source-data-monolith",
        "canvas-split",
    ]
    assert "fault_hash" in parsed_pda_encyclopedia["warnings"]
    assert "stream_fault" in parsed_pda_encyclopedia["warnings"]

    habitat_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 4)
    habitat = bytearray(
        server.HABITAT_FLOOD_HEADER.pack(
            server.HABITAT_FLOOD_MAGIC,
            server.HABITAT_FLOOD_VERSION,
            server.HABITAT_FLOOD_BLACKBOX_CAPACITY,
            2,
            habitat_flags,
        )
        + bytes(server.HABITAT_FLOOD_BLACKBOX_CAPACITY * server.HABITAT_FLOOD_ENTRY_BYTES)
    )
    server.HABITAT_FLOOD_ENTRY.pack_into(
        habitat,
        server.HABITAT_FLOOD_HEADER_BYTES,
        920,
        8,
        10,
        1,
        0,
        0.35,
        0.40,
        12.5,
        0.20,
        1 << 1,
        0x13060001,
        4,
    )
    server.HABITAT_FLOOD_ENTRY.pack_into(
        habitat,
        server.HABITAT_FLOOD_HEADER_BYTES + server.HABITAT_FLOOD_ENTRY_BYTES,
        921,
        8,
        10,
        3,
        0,
        0.90,
        1.15,
        48.0,
        0.88,
        habitat_flags,
        0x13060002,
        5,
    )
    habitat_path = root / "Dump_1306_Construction_HabitatIntegrity.bin"
    habitat_path.write_bytes(habitat)
    parsed_habitat = server.parse_dump_file(habitat_path)
    assert parsed_habitat["type"] == "habitat_flood_blackbox"
    assert parsed_habitat["mode"] == "habitat_integrity"
    assert parsed_habitat["version"] == server.HABITAT_FLOOD_VERSION
    assert parsed_habitat["latest"]["frame"] == 921
    assert parsed_habitat["latest"]["flagLabels"] == [
        "nonfinite",
        "overflow-clamped",
        "topology-invalid",
        "module-stress-invalid",
    ]
    assert parsed_habitat["latest"]["stateHashHex"] == "0x13060002"
    assert parsed_habitat["latest"]["floodedRoomCount"] == 3
    assert "nonfinite" in parsed_habitat["warnings"]
    assert "overflow_clamped" in parsed_habitat["warnings"]
    assert "topology_invalid" in parsed_habitat["warnings"]
    assert "module_stress_invalid" in parsed_habitat["warnings"]
    assert "water_level_over_one" in parsed_habitat["warnings"]

    construction_validation_flags = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 6) | (1 << 7)
    construction_validation = server.CONSTRUCTION_VALIDATION_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        1,
        2,
        3,
        930,
        1 << 3,
        0.35,
        0.18,
        1,
        0,
        0x1306CAFE,
    )
    construction_validation += server.CONSTRUCTION_VALIDATION_ENTRY.pack(
        11.0,
        21.0,
        31.0,
        2,
        3,
        4,
        931,
        construction_validation_flags,
        -0.25,
        0.52,
        1,
        2,
        0x1306BEEF,
    )
    construction_validation_path = root / "Dump_1306_ConstructionValidation.bin"
    construction_validation_path.write_bytes(construction_validation)
    parsed_construction_validation = server.parse_dump_file(construction_validation_path)
    assert parsed_construction_validation["type"] == "construction_validation_blackbox"
    assert parsed_construction_validation["latest"]["frame"] == 931
    assert parsed_construction_validation["latest"]["flagLabels"] == [
        "occupied-grid-cell",
        "terrain-intersection",
        "nonfinite-input",
        "graph-capacity",
        "disconnected-wing",
    ]
    assert parsed_construction_validation["latest"]["resultHashHex"] == "0x1306BEEF"
    assert parsed_construction_validation["latest"]["graphSplices"] == 2
    assert "nonfinite" in parsed_construction_validation["warnings"]
    assert "terrain_intersection" in parsed_construction_validation["warnings"]
    assert "occupied_grid_cell" in parsed_construction_validation["warnings"]
    assert "graph_route_fault" in parsed_construction_validation["warnings"]

    construction_socket_flags = (1 << 3) | (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8) | (1 << 10)
    construction_socket = server.CONSTRUCTION_SOCKET_ENTRY.pack(
        100.0,
        200.0,
        300.0,
        940,
        16,
        24,
        2,
        120.0,
        0.045,
        1 << 5,
        0x1306A001,
        0.85,
        7,
    )
    construction_socket += server.CONSTRUCTION_SOCKET_ENTRY.pack(
        101.0,
        201.0,
        301.0,
        941,
        18,
        36,
        0,
        900.0,
        9.5,
        construction_socket_flags,
        0x1306A002,
        0.35,
        8,
    )
    construction_socket_path = root / "Dump_1306_Construction_SocketTelemetry.bin"
    construction_socket_path.write_bytes(construction_socket)
    parsed_construction_socket = server.parse_dump_file(construction_socket_path)
    assert parsed_construction_socket["type"] == "construction_socket_blackbox"
    assert parsed_construction_socket["latest"]["frame"] == 941
    assert parsed_construction_socket["latest"]["flagLabels"] == [
        "collision-blocked",
        "nonfinite",
        "valid-snap",
        "topology-dirty",
        "rollback-fence",
        "capacity-exceeded",
    ]
    assert parsed_construction_socket["latest"]["resultHashHex"] == "0x1306A002"
    assert "nonfinite" in parsed_construction_socket["warnings"]
    assert "collision_blocked" in parsed_construction_socket["warnings"]
    assert "capacity_exceeded" in parsed_construction_socket["warnings"]
    assert "rollback_fence" in parsed_construction_socket["warnings"]
    assert "topology_dirty" in parsed_construction_socket["warnings"]
    assert "solver_over_500us" in parsed_construction_socket["warnings"]

    construction_holography_flags = (1 << 0) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 9)
    construction_holography = server.CONSTRUCTION_HOLOGRAPHY_ENTRY.pack(
        120.0,
        220.0,
        320.0,
        950,
        0xB1710001,
        8,
        (1 << 0) | (1 << 1) | (1 << 2),
        180.0,
        0.25,
        0x1306C001,
        0.9,
    )
    construction_holography += server.CONSTRUCTION_HOLOGRAPHY_ENTRY.pack(
        121.0,
        221.0,
        321.0,
        951,
        0xB1710002,
        8,
        construction_holography_flags,
        650.0,
        -0.15,
        0x1306C002,
        0.45,
    )
    construction_holography_path = root / "Dump_1306_Construction_Holography.bin"
    construction_holography_path.write_bytes(construction_holography)
    parsed_construction_holography = server.parse_dump_file(construction_holography_path)
    assert parsed_construction_holography["type"] == "construction_holography_blackbox"
    assert parsed_construction_holography["latest"]["frame"] == 951
    assert parsed_construction_holography["latest"]["flagLabels"] == [
        "active",
        "sdf-blocked",
        "bounds-blocked",
        "nonfinite",
        "socket-snap",
        "rollback-excluded",
    ]
    assert parsed_construction_holography["latest"]["prefabHashHex"] == "0xB1710002"
    assert parsed_construction_holography["latest"]["validationStateHashHex"] == "0x1306C002"
    assert "nonfinite" in parsed_construction_holography["warnings"]
    assert "sdf_blocked" in parsed_construction_holography["warnings"]
    assert "bounds_blocked" in parsed_construction_holography["warnings"]
    assert "rollback_excluded" in parsed_construction_holography["warnings"]
    assert "solver_over_500us" in parsed_construction_holography["warnings"]

    defrag = server.DEFRAG_ENTRY_PACK1.pack(1, 2, 3, 100, 64, 16, 32, 0, 0.25, 1, 5, 1, 0, 0)
    (root / "Dump_A_MEMORY_DEFRAGMENTATION_OVERSEER.bin").write_bytes(defrag)

    thermal = server.THERMAL_HEADER.pack(7, 0)
    thermal += server.THERMAL_ENTRY_MANUAL.pack(100, 7, 3, 430, 2, 77, 1, 3, 9)
    (root / "Dump_THERMAL_THROTTLING_DIRECTOR.bin").write_bytes(thermal)

    biomass = server.BIOMASS_HEADER.pack(server.BIOMASS_MAGIC, 1, server.BIOMASS_ENTRY.size, 0, 300)
    biomass += server.BIOMASS_ENTRY.pack(12, 99, 4, 0, 8.0, 5.0, 3.0, 0.4)
    (root / "Dump_ECOLOGICAL_BIOMASS_ENGINE.bin").write_bytes(biomass)

    macro = server.BIOMASS_HEADER.pack(server.MACRO_SWARM_MAGIC, 1, server.MACRO_SWARM_ENTRY.size, 0, 300)
    macro += server.MACRO_SWARM_ENTRY.pack(22, 101, 3, 2, 9.5, 1, 0, 0)
    (root / "Dump_SWARM_MACRO_MIGRATION_DIRECTOR.bin").write_bytes(macro)

    mutation = server.BIOMASS_HEADER.pack(server.FAUNA_MUTATION_MAGIC, 1, server.FAUNA_MUTATION_ENTRY.size, 0, 300)
    mutation += server.FAUNA_MUTATION_ENTRY.pack(23, 102, 7, 4, 3, 5, 0.25, 0.5, 0.75, 0, 0)
    (root / "Dump_ECOLOGY_MUTATION_DIRECTOR.bin").write_bytes(mutation)

    genetics = server.BIOMASS_HEADER.pack(server.FAUNA_GENETICS_MAGIC, 2, server.FAUNA_GENETICS_ENTRY.size, 0, 300)
    genetics += server.FAUNA_GENETICS_ENTRY.pack(
        24,
        0x3060001,
        16,
        12,
        48,
        0,
        0.2,
        0.4,
        0.6,
        0.25,
        320.0,
        0xABCD0001,
        0x87654321,
        0x00000010,
        0,
        0,
    )
    genetics += server.FAUNA_GENETICS_ENTRY.pack(
        25,
        0x3060002,
        18,
        20,
        70,
        2,
        0.3,
        0.5,
        0.7,
        0.9,
        650.0,
        0xABCD0002,
        0x87654321,
        0x00000010,
        1,
        0,
    )
    genetics_path = root / "Dump_SHINOBU_306.bin"
    genetics_path.write_bytes(genetics)
    parsed_genetics = server.parse_dump_file(genetics_path)
    assert parsed_genetics["type"] == "fauna_genetics"
    assert parsed_genetics["magicHex"] == "0x00474E474F434548"
    assert parsed_genetics["entrySize"] == server.FAUNA_GENETICS_ENTRY.size
    assert parsed_genetics["entryCount"] == 2
    assert parsed_genetics["latest"]["frame"] == 25
    assert parsed_genetics["latest"]["flagLabels"] == ["invalid-mask"]
    assert parsed_genetics["latest"]["patternHistogram"][:10] == [1, 2, 3, 4, 5, 6, 7, 8, 0, 1]
    assert "invalid_mask" in parsed_genetics["warnings"]
    assert "genome_count_out_of_range" in parsed_genetics["warnings"]
    assert "extraction_count_mismatch" in parsed_genetics["warnings"]
    assert "burst_over_500us" in parsed_genetics["warnings"]
    renamed_genetics_path = root / "Renamed_FaunaGenetics.h8dump"
    renamed_genetics_path.write_bytes(genetics)
    assert server.parse_dump_file(renamed_genetics_path)["type"] == "fauna_genetics"

    live = server.LIVE_TELEMETRY_ENTRY.pack(
        server.LIVE_TELEMETRY_MAGIC,
        2,
        server.LIVE_TELEMETRY_ENTRY.size,
        333,
        12,
        64,
        17.25,
        0.016,
        2048.0,
        3.5,
        6.25,
        0x123,
        0x40,
        0x77,
        9,
        331,
    )
    (root / "runtime_telemetry.bin").write_bytes(live)
    live_parsed = server.parse_live_telemetry(root / "runtime_telemetry.bin", live)
    assert live_parsed["entrySize"] == 64
    assert live_parsed["latest"]["latencyMs"] == 3.5
    assert live_parsed["latest"]["gpuFrameTimeMs"] == 6.25
    assert live_parsed["latest"]["systemMask"] == 0x123
    legacy_live = server.LIVE_TELEMETRY_ENTRY_V1.pack(server.LIVE_TELEMETRY_MAGIC, 1, 333, 12, 64, 17.25, 0.016, 2048.0)
    legacy_parsed = server.parse_live_telemetry(root / "legacy_runtime_telemetry.bin", legacy_live)
    assert legacy_parsed["entrySize"] == server.LIVE_TELEMETRY_ENTRY_V1.size
    assert "legacy_v1_32_byte_record" in legacy_parsed["warnings"]

    headless = server.HEADLESS_HEADER.pack(server.HEADLESS_MAGIC, 1, server.HEADLESS_ENTRY.size, 1)
    headless += server.HEADLESS_ENTRY.pack(200, 4, 55, 1, 2, 3, 0.1, 0.2, 0.3, 6.0, 2.0, 128.0, 0)
    assert server.parse_headless_blackbox(root / "Dump_HEADLESS.bin", headless)["latest"]["predator"] == 2.0
    assert len(server.cap_entries([{"i": i} for i in range(server.MAX_DUMP_ENTRIES + 1)])) == server.MAX_DUMP_ENTRIES

    memory_text = root / "Dump_CORE_DATA_VAULT_WARDEN.txt"
    memory_text.write_text(
        "H8MEMORY_ALLOCATION_TABLE\n"
        "TotalBytes=256\n"
        "ActiveAllocationCount=1\n"
        "Index=0 Ptr=4096 Bytes=64 Owner=1 Allocator=4 Flags=1\n",
        encoding="utf-8",
    )
    assert server.parse_h8memory_text(memory_text)["memoryMap"][-1]["state"] == "free"

    csv_path = root / "QA_Endurance_Log.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["frame", "avgFps", "PreyBiomass", "PredatorBiomass", "HardwareThermalSeverity", "BatteryPercent"],
        )
        writer.writeheader()
        writer.writerow(
            {
                "frame": "1",
                "avgFps": "60",
                "PreyBiomass": "4.5",
                "PredatorBiomass": "1.2",
                "HardwareThermalSeverity": "2",
                "BatteryPercent": "66",
            }
        )
    assert round(server.parse_csv_file(csv_path, "QA_Endurance_Log.csv")["frameSeries"][0]["frameTimeMs"], 3) == 16.667

    hphi_path = root / "HECTON_PHI_REPORT.md"
    hphi_path.write_text("Date: 2026-05-13\nH-Phi_static = 0.973 * 0.996 * 0.001 * 0.535 = 0.00062\n", encoding="utf-8")
    assert server.parse_hphi_report(hphi_path)["value"] == 0.00062

    missing_logs = root / "MissingAgentLogs"
    old_logs = server.AGENT_LOGS
    server.AGENT_LOGS = missing_logs
    try:
        missing_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs
    assert missing_data["files"] == []
    assert not missing_logs.exists()

    no_data_logs = root / "NoDataAgentLogs"
    no_data_logs.mkdir()
    (no_data_logs / "Dump_SARGASSUM_FOOD_CHAIN.h8dump").write_bytes(b"")
    (no_data_logs / "Dump_SARGASSUM_BOID_SENSORY.h8dump").write_bytes(b"")
    server.AGENT_LOGS = no_data_logs
    try:
        no_data_dump_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs
    assert any(
        file["name"] == "Dump_SARGASSUM_FOOD_CHAIN.h8dump"
        and file["type"] == "sargassum_food_chain_blackbox"
        and file["warnings"] == ["truncated_header"]
        and file["headerBytes"] == server.SARGASSUM_FOOD_CHAIN_HEADER_BYTES
        and file["entrySize"] == server.SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
        and file["declaredEntryCount"] == 0
        and file["returnedEntryCount"] == 0
        for file in no_data_dump_data["files"]
    )
    assert any(
        file["name"] == "Dump_SARGASSUM_BOID_SENSORY.h8dump"
        and file["type"] == "sargassum_boid_sensory_blackbox"
        and file["warnings"] == ["truncated_header"]
        and file["headerBytes"] == server.SARGASSUM_BOID_SENSORY_HEADER_BYTES
        and file["entrySize"] == server.SARGASSUM_BOID_SENSORY_ENTRY_BYTES
        and file["declaredEntryCount"] == 0
        and file["returnedEntryCount"] == 0
        for file in no_data_dump_data["files"]
    )

    server.AGENT_LOGS = root
    original_parse_dump_file = server.parse_dump_file

    def fail_player_dump(path: Path) -> dict[str, object]:
        if path.name == "Dump_PLAYER_KINEMATICS.bin":
            raise ValueError("forced parser failure")
        return original_parse_dump_file(path)

    server.parse_dump_file = fail_player_dump
    try:
        fault_data = server.collect_dumps()
    finally:
        server.parse_dump_file = original_parse_dump_file
        server.AGENT_LOGS = old_logs
    assert any(
        file["name"] == "Dump_PLAYER_KINEMATICS.bin" and file["type"] == "parse_failed"
        for file in fault_data["files"]
    )
    assert any(file["type"] == "live_telemetry" for file in fault_data["files"])

    server.AGENT_LOGS = root
    try:
        dump_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs

    parsed_types = {file["type"] for file in dump_data["files"]}
    assert "macro_swarm" in parsed_types
    assert "fauna_mutation" in parsed_types
    assert "fauna_genetics" in parsed_types
    assert "live_telemetry" in parsed_types
    assert "crash_telemetry_buffer" in parsed_types
    assert "simulation_bucket_blackbox" in parsed_types
    assert "terrain_streaming_pager" in parsed_types
    assert "world_chunk_residency_blackbox" in parsed_types
    assert "global_telemetry_bus_blackbox" in parsed_types
    assert "data_monolith_telemetry_blackbox" in parsed_types
    assert "vault_sovereignty_telemetry_blackbox" in parsed_types
    assert "arm64_alignment_telemetry_blackbox" in parsed_types
    assert "haptic_synthesis_telemetry_blackbox" in parsed_types
    assert "vocal_warning_telemetry_blackbox" in parsed_types
    assert "granular_audio_telemetry_blackbox" in parsed_types
    assert "prologue_audio_transition_blackbox" in parsed_types
    assert "audio_synthesis_telemetry_blackbox" in parsed_types
    assert "vocal_bank_synthesis_blackbox" in parsed_types
    assert "adaptive_stem_mixer_blackbox" in parsed_types
    assert "camera_juice_telemetry_blackbox" in parsed_types
    assert "material_decay_blackbox" in parsed_types
    assert "sargassum_food_chain_blackbox" in parsed_types
    assert "sargassum_boid_sensory_blackbox" in parsed_types
    assert "marine_snow_vfx_blackbox" in parsed_types
    assert "propwash_gpu_blackbox" in parsed_types
    assert "carve_debris_blackbox" in parsed_types
    assert "biolum_pulse_blackbox" in parsed_types
    assert "foveated_simulation_blackbox" in parsed_types
    assert "input_determinism_blackbox" in parsed_types
    assert "origin_shift_blackbox" in parsed_types
    assert "binary_layout_sentinel" in parsed_types
    assert "terminal_os_blackbox" in parsed_types
    assert "terminal_decryption_blackbox" in parsed_types
    assert "terminal_projection_blackbox" in parsed_types
    assert "openxr_manual_override_blackbox" in parsed_types
    assert "vehicle_damage_holographer_blackbox" in parsed_types
    assert "pda_projection_blackbox" in parsed_types
    assert "wrist_hud_blackbox" in parsed_types
    assert "ladder_climb_ik_blackbox" in parsed_types
    assert "topographical_sonar_blackbox" in parsed_types
    assert "kinetic_character_blackbox" in parsed_types
    assert "procedural_bone_blackbox" in parsed_types
    assert "vr_somatic_blackbox" in parsed_types
    assert "lockstep_state_validator_blackbox" in parsed_types
    assert "voxel_astar_blackbox" in parsed_types
    assert "path_funnel_blackbox" in parsed_types
    assert "laser_cutter_dod_blackbox" in parsed_types
    assert "wfc_laser_cut_blackbox" in parsed_types
    assert "tool_kinematics_blackbox" in parsed_types
    assert "auxiliary_equipment_blackbox" in parsed_types
    assert "upgrade_matrix_blackbox" in parsed_types
    assert "metabolism_blackbox" in parsed_types
    assert "physiology_autopsy_blackbox" in parsed_types
    assert "sensory_impairment_blackbox" in parsed_types
    assert "suit_integrity_blackbox" in parsed_types
    assert "radiation_mutation_blackbox" in parsed_types
    assert "reactor_thermal_blackbox" in parsed_types
    assert "nuclear_reactor_thermal_blackbox" in parsed_types
    assert "respawn_reconciliation_blackbox" in parsed_types
    assert "pda_frequency_tuning_blackbox" in parsed_types
    assert "compass_gyro_blackbox" in parsed_types
    assert "pda_encyclopedia_blackbox" in parsed_types
    assert "habitat_flood_blackbox" in parsed_types
    assert "construction_validation_blackbox" in parsed_types
    assert "construction_socket_blackbox" in parsed_types
    assert "construction_holography_blackbox" in parsed_types

    def assert_collected_dump(
        name: str,
        expected_type: str,
        expected_warning: str,
        path_fragment: str | None = None,
    ) -> None:
        assert any(
            file.get("name") == name
            and file.get("type") == expected_type
            and expected_warning in file.get("warnings", [])
            and (path_fragment is None or path_fragment in file.get("path", ""))
            for file in dump_data["files"]
        ), f"missing collected {expected_type} {name} warning={expected_warning}"

    assert_collected_dump(
        "Dump_SARGASSUM_FOOD_CHAIN.h8dump",
        "sargassum_food_chain_blackbox",
        "invalid_header",
    )
    assert_collected_dump(
        "CopiedInvalidSargassumFoodChain.h8dump",
        "sargassum_food_chain_blackbox",
        "invalid_header",
    )
    assert_collected_dump(
        "Dump_SARGASSUM_BOID_SENSORY.h8dump",
        "sargassum_boid_sensory_blackbox",
        "invalid_header",
    )
    assert_collected_dump(
        "CopiedInvalidSargassumBoidSensory.h8dump",
        "sargassum_boid_sensory_blackbox",
        "invalid_header",
    )

    assert len(dump_data["frameSeries"]) >= 3
    assert any(point["jitterMs"] == 10.0 for point in dump_data["frameSeries"])
    assert any(point["source"] == "runtime_telemetry.bin" for point in dump_data["frameSeries"])
    assert dump_data["latestThermal"]["batteryPercent"] == 77
    assert dump_data["jobAdmission"]["deniedCount"] == 1
    assert dump_data["jobAdmission"]["insufficientBudgetCount"] == 1
    assert dump_data["jobAdmission"]["stateHashMismatchCount"] == 0
    assert dump_data["jobAdmission"]["legacyStarvedCount"] == 1
    assert dump_data["jobAdmission"]["latest"]["source"] == "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_LegacyV1.bin"
    assert dump_data["jobAdmission"]["latest"]["frame"] == 78
    assert dump_data["ecologySeries"]
    assert dump_data["memoryMaps"]
    assert dump_data["memoryMaps"][0]["name"] == "Dump_CORE_DATA_VAULT_WARDEN.txt"
    assert dump_data["memoryMaps"][0]["estimated"] is False

    index_text = (Path(__file__).with_name("index.html")).read_text(encoding="utf-8")
    for required in (
        "function normalizeSummary",
        "function normalizeMemoryMap",
        "function objectArray",
        "function updateJobAdmission",
        "crash_telemetry_buffer",
        "simulation_bucket_blackbox",
        "terrain_streaming_pager",
        "world_chunk_residency_blackbox",
        "global_telemetry_bus_blackbox",
        "data_monolith_telemetry_blackbox",
        "vault_sovereignty_telemetry_blackbox",
        "arm64_alignment_telemetry_blackbox",
        "haptic_synthesis_telemetry_blackbox",
        "vocal_warning_telemetry_blackbox",
        "granular_audio_telemetry_blackbox",
        "prologue_audio_transition_blackbox",
        "audio_synthesis_telemetry_blackbox",
        "vocal_bank_synthesis_blackbox",
        "adaptive_stem_mixer_blackbox",
        "camera_juice_telemetry_blackbox",
        "material_decay_blackbox",
        "sargassum_food_chain_blackbox",
        "sargassum_boid_sensory_blackbox",
        "marine_snow_vfx_blackbox",
        "propwash_gpu_blackbox",
        "carve_debris_blackbox",
        "biolum_pulse_blackbox",
        "foveated_simulation_blackbox",
        "input_determinism_blackbox",
        "origin_shift_blackbox",
        "binary_layout_sentinel",
        "terminal_os_blackbox",
        "terminal_decryption_blackbox",
        "terminal_projection_blackbox",
        "openxr_manual_override_blackbox",
        "vehicle_damage_holographer_blackbox",
        "pda_projection_blackbox",
        "wrist_hud_blackbox",
        "ladder_climb_ik_blackbox",
        "topographical_sonar_blackbox",
        "kinetic_character_blackbox",
        "procedural_bone_blackbox",
        "vr_somatic_blackbox",
        "lockstep_state_validator_blackbox",
        "voxel_astar_blackbox",
        "path_funnel_blackbox",
        "laser_cutter_dod_blackbox",
        "wfc_laser_cut_blackbox",
        "tool_kinematics_blackbox",
        "auxiliary_equipment_blackbox",
        "upgrade_matrix_blackbox",
        "metabolism_blackbox",
        "physiology_autopsy_blackbox",
        "sensory_impairment_blackbox",
        "suit_integrity_blackbox",
        "radiation_mutation_blackbox",
        "reactor_thermal_blackbox",
        "nuclear_reactor_thermal_blackbox",
        "respawn_reconciliation_blackbox",
        "pda_frequency_tuning_blackbox",
        "compass_gyro_blackbox",
        "pda_encyclopedia_blackbox",
        "habitat_flood_blackbox",
        "construction_validation_blackbox",
        "construction_socket_blackbox",
        "construction_holography_blackbox",
        "framePacingFlagLabels",
        "faultLabels",
        "reasonLabels",
        "jobAdmissionText",
        "if (!response.ok)",
    ):
        assert required in index_text
    for forbidden in ("innerHTML", "eval(", "new Function", "document.write", "console.log", "debugger"):
        assert forbidden not in index_text

    degraded = server.build_degraded_summary(RuntimeError("forced failure"))
    assert degraded["status"] == "DASHBOARD DEGRADED"
    assert degraded["csv"]["sources"] == []
    assert degraded["dumps"]["files"] == []
    assert degraded["jobAdmission"]["deniedCount"] == 0
    assert degraded["errors"][0]["type"] == "RuntimeError"

    original_build_summary = server.build_summary

    def raise_summary() -> dict[str, object]:
        raise RuntimeError("forced route failure")

    server.build_summary = raise_summary
    try:
        response = server.api_summary()
    finally:
        server.build_summary = original_build_summary
    routed_payload = json.loads(response.body)
    assert response.status_code == 200
    assert routed_payload["status"] == "DASHBOARD DEGRADED"
    assert routed_payload["errors"][0]["type"] == "RuntimeError"
    assert response.headers["cache-control"] == "no-store, max-age=0"
    assert response.headers["pragma"] == "no-cache"
    assert response.headers["x-content-type-options"] == "nosniff"

    assert server.index().headers["cache-control"] == "no-store, max-age=0"
    health_response = server.api_health()
    health_payload = json.loads(health_response.body)
    assert health_payload["status"] == "ok"
    assert health_response.headers["x-content-type-options"] == "nosniff"

    remove_tree_with_retry(root)
    print("telemetry dashboard smoke ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
