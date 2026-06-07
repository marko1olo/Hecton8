import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

from test_local_temp import project_local_tempdir_factory  # noqa: E402

temporary_directory = project_local_tempdir_factory("data_vault_sovereignty_audit_tests")

import DataVaultSovereigntyAudit as audit  # noqa: E402


class DataVaultSovereigntyAuditTests(unittest.TestCase):
    def test_scan_separates_h8memory_allowed_constructors_from_system_debt(self) -> None:
        with temporary_directory(prefix="h8_vault_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            h8memory = source / "Core" / "Memory" / "H8Memory.cs"
            gameplay = source / "Gameplay" / "BadSystem.cs"
            h8memory.parent.mkdir(parents=True)
            gameplay.parent.mkdir(parents=True)
            h8memory.write_text(
                "new NativeArray<int>(4, Allocator.Persistent);\n"
                "new NativeArray<float>(4, Allocator.Persistent);\n",
                encoding="utf-8",
            )
            gameplay.write_text(
                "new NativeArray<int>(4, Allocator.Persistent);\n"
                "new    NativeArray<float>(4, Allocator.Persistent);\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["totalDirectConstructors"], 4)
            self.assertEqual(payload["allowedDirectConstructors"], 2)
            self.assertEqual(payload["forbiddenDirectConstructors"], 2)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 2)
            self.assertEqual(payload["forbiddenFileCount"], 1)

    def test_constructor_scan_ignores_managed_arrays_of_native_handles(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_managed_native_handle_array_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "World" / "ChunkStore.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "using Unity.Collections;\n"
                "public sealed class ChunkStore\n"
                "{\n"
                "    private readonly NativeArray<int>[] _slots = new NativeArray<int>[8];\n"
                "    public NativeArray<int> Allocate() => new NativeArray<int>(4, Allocator.Persistent);\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["totalDirectConstructors"], 1)
            self.assertEqual(payload["forbiddenDirectConstructors"], 1)
            self.assertEqual(payload["findings"][0]["lines"], [5])

    def test_constructor_scan_ignores_comments_strings_and_splits_surface(self) -> None:
        with temporary_directory(prefix="h8_vault_constructor_surface_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "Gameplay" / "RuntimeNativeOwner.cs"
            editor = source / "Editor" / "NativeArrayBakeWindow.cs"
            offline = source / "World" / "BiomeWeightMapBaker.cs"
            runtime.parent.mkdir(parents=True)
            editor.parent.mkdir(parents=True)
            offline.parent.mkdir(parents=True)
            runtime.write_text(
                "// new NativeArray<int>(4, Allocator.Persistent);\n"
                "private const string Text = \"new NativeArray<float>(4, Allocator.Persistent)\";\n"
                "public void Allocate() { _ = new NativeArray<int>(4, Allocator.Persistent); }\n",
                encoding="utf-8",
            )
            editor.write_text(
                "public void Allocate() { _ = new NativeArray<int>(4, Allocator.Persistent); }\n",
                encoding="utf-8",
            )
            offline.write_text(
                "public void Allocate() { _ = new NativeArray<int>(4, Allocator.Persistent); }\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 3)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)
            self.assertEqual(payload["editorOfflineForbiddenDirectConstructors"], 2)
            self.assertEqual(
                payload["forbiddenDirectConstructorsByExecutionSurface"],
                {"Editor": 1, "OfflineBake": 1, "Runtime": 1},
            )
            self.assertEqual(payload["forbiddenDirectConstructorsByAllocator"], {"Persistent": 3})

    def test_constructor_scan_tracks_allocator_classes(self) -> None:
        with temporary_directory(prefix="h8_vault_allocator_surface_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            baker = source / "World" / "BiomeWeightMapBaker" / "Editor" / "BiomeWeightMapBakePipeline.cs"
            baker.parent.mkdir(parents=True)
            baker.write_text(
                "public void Bake()\n"
                "{\n"
                "    _ = new NativeArray<int>(4, Allocator.TempJob);\n"
                "    _ = new NativeArray<float>(4, Allocator.Temp);\n"
                "    _ = new NativeArray<byte>(4, Allocator.Persistent);\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 1)
            self.assertEqual(payload["allowedDirectConstructors"], 2)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 2)
            self.assertEqual(
                payload["editorOfflineForbiddenDirectConstructorsByAllocator"],
                {"Persistent": 1},
            )
            self.assertEqual(payload["findings"][0]["forbiddenAllocatorCounts"], {"Persistent": 1})
            self.assertEqual(
                payload["findings"][0]["transientEditorScratchAllocatorCounts"],
                {"Temp": 1, "TempJob": 1},
            )
            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["TempJob", "Temp", "Persistent"])

    def test_editor_transient_nativearray_constructors_are_reported_not_gate_relevant(self) -> None:
        with temporary_directory(prefix="h8_vault_editor_transient_surface_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            baker = source / "World" / "StaticCaveSdfBaker" / "Editor" / "StaticCaveSdfBakePipeline.cs"
            baker.parent.mkdir(parents=True)
            baker.write_text(
                "public void Bake()\n"
                "{\n"
                "    _ = new NativeArray<int>(4, Allocator.TempJob);\n"
                "    _ = new NativeArray<float>(4, Allocator.Temp);\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["totalDirectConstructors"], 2)
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineForbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 2)
            self.assertTrue(payload["findings"][0]["allowed"])
            self.assertEqual(payload["findings"][0]["forbiddenCount"], 0)

    def test_multiline_editor_tempjob_constructor_is_not_unknown_debt(self) -> None:
        with temporary_directory(prefix="h8_vault_multiline_allocator_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            baker = source / "World" / "StaticCaveSdfBaker" / "Editor" / "StaticCaveSdfBakePipeline.cs"
            baker.parent.mkdir(parents=True)
            baker.write_text(
                "public void Bake()\n"
                "{\n"
                "    _ = new NativeArray<int>(\n"
                "        4,\n"
                "        Allocator.TempJob,\n"
                "        NativeArrayOptions.UninitializedMemory);\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["TempJob"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 1)

    def test_file_scoped_unity_editor_guard_sets_editor_surface_outside_editor_folder(self) -> None:
        with temporary_directory(prefix="h8_vault_editor_guard_surface_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            window = source / "VFX" / "Debris" / "ShinobuVoxelSculptorWindow.cs"
            window.parent.mkdir(parents=True)
            window.write_text(
                "#if UNITY_EDITOR\n"
                "using Unity.Collections;\n"
                "using UnityEditor;\n"
                "public sealed class ShinobuVoxelSculptorWindow : EditorWindow\n"
                "{\n"
                "    public void Bake() { _ = new NativeArray<int>(4, Allocator.TempJob); }\n"
                "}\n"
                "#endif\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["executionSurface"], "Editor")
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 1)

    def test_partial_unity_editor_guard_classifies_constructor_lines_separately(self) -> None:
        with temporary_directory(prefix="h8_vault_partial_editor_guard_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "VFX" / "BiolumPulseSyncRuntime.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "public sealed class BiolumPulseSyncRuntime\n"
                "{\n"
                "    public void AllocateRuntime() { _ = new NativeArray<int>(4, Allocator.Persistent); }\n"
                "#if UNITY_EDITOR\n"
                "    public void AllocateEditorScratch() { _ = new NativeArray<float>(4, Allocator.TempJob); }\n"
                "    public void AllocateEditorPersistent() { _ = new NativeArray<byte>(4, Allocator.Persistent); }\n"
                "#endif\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["totalDirectConstructors"], 3)
            self.assertEqual(payload["forbiddenDirectConstructors"], 2)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)
            self.assertEqual(payload["editorOfflineForbiddenDirectConstructors"], 1)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 1)
            self.assertEqual(
                payload["directConstructorsByExecutionSurface"],
                {"Editor": 2, "Runtime": 1},
            )
            self.assertEqual(
                payload["forbiddenDirectConstructorsByExecutionSurface"],
                {"Editor": 1, "Runtime": 1},
            )
            self.assertEqual(
                payload["editorOfflineForbiddenDirectConstructorsByAllocator"],
                {"Persistent": 1},
            )
            self.assertEqual(payload["findings"][0]["executionSurface"], "Mixed")
            self.assertEqual(
                payload["findings"][0]["lineExecutionSurfaces"],
                ["Runtime", "Editor", "Editor"],
            )

    def test_nested_preprocessor_guard_restores_active_parent_branch(self) -> None:
        with temporary_directory(prefix="h8_vault_nested_editor_guard_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "VFX" / "MixedPreprocessorRuntime.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "public sealed class MixedPreprocessorRuntime\n"
                "{\n"
                "#if UNITY_EDITOR\n"
                "    public void EditorA() { _ = new NativeArray<int>(4, Allocator.TempJob); }\n"
                "#if HECTON_DEBUG\n"
                "    public void EditorNested() { _ = new NativeArray<float>(4, Allocator.TempJob); }\n"
                "#endif\n"
                "    public void EditorB() { _ = new NativeArray<byte>(4, Allocator.TempJob); }\n"
                "#endif\n"
                "    public void RuntimeA() { _ = new NativeArray<int>(4, Allocator.Persistent); }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(
                payload["findings"][0]["lineExecutionSurfaces"],
                ["Editor", "Editor", "Editor", "Runtime"],
            )
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 3)

    def test_try_get_latest_created_runtime_fallback_is_gate_relevant(self) -> None:
        with temporary_directory(prefix="h8_vault_latest_created_runtime_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "Gameplay" / "HazardRuntime.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "public sealed class HazardRuntime\n"
                "{\n"
                "    public bool TryResolve()\n"
                "    {\n"
                "        // GlobalDataVault.TryGetLatestCreated(out ignored) must stay ignored.\n"
                "        string text = \"GlobalDataVault.TryGetLatestCreated(out ignored)\";\n"
                "        return GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            latest_findings = audit.scan_latest_created_fallback_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                latest_created_fallback_findings=latest_findings,
            )

            self.assertEqual(payload["totalLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["forbiddenLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["runtimeForbiddenLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["forbiddenLatestCreatedFallbackFileCount"], 1)
            self.assertEqual(payload["latestCreatedFallbackFindings"][0]["forbiddenLines"], [7])

    def test_try_get_latest_created_allows_bootstrap_editor_and_diagnostics_routes(self) -> None:
        with temporary_directory(prefix="h8_vault_latest_created_allowed_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            bootstrap = source / "Bootstrap" / "VaultBootstrapProbe.cs"
            editor = source / "World" / "Editor" / "VaultXRayWindow.cs"
            diagnostics = source / "Core" / "Diagnostics" / "VaultMemoryGizmoVisualizer.cs"
            bootstrap.parent.mkdir(parents=True)
            editor.parent.mkdir(parents=True)
            diagnostics.parent.mkdir(parents=True)
            bootstrap.write_text(
                "public sealed class VaultBootstrapProbe\n"
                "{\n"
                "    public bool TryResolve() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);\n"
                "}\n",
                encoding="utf-8",
            )
            editor.write_text(
                "public sealed class VaultXRayWindow\n"
                "{\n"
                "    public bool TryResolve() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);\n"
                "}\n",
                encoding="utf-8",
            )
            diagnostics.write_text(
                "public sealed class VaultMemoryGizmoVisualizer\n"
                "{\n"
                "    public bool TryResolve() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);\n"
                "}\n",
                encoding="utf-8",
            )

            latest_findings = audit.scan_latest_created_fallback_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                latest_created_fallback_findings=latest_findings,
            )

            self.assertEqual(payload["totalLatestCreatedFallbacks"], 3)
            self.assertEqual(payload["allowedLatestCreatedFallbacks"], 3)
            self.assertEqual(payload["forbiddenLatestCreatedFallbacks"], 0)
            self.assertEqual(payload["runtimeForbiddenLatestCreatedFallbacks"], 0)

    def test_try_get_latest_created_partial_editor_guard_counts_runtime_only(self) -> None:
        with temporary_directory(prefix="h8_vault_latest_created_partial_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "Inventory" / "CargoTransferRuntime.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "public sealed class CargoTransferRuntime\n"
                "{\n"
                "#if UNITY_EDITOR\n"
                "    public bool TryEditor() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault editorVault);\n"
                "#endif\n"
                "    public bool TryRuntime() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault runtimeVault);\n"
                "}\n",
                encoding="utf-8",
            )

            latest_findings = audit.scan_latest_created_fallback_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                latest_created_fallback_findings=latest_findings,
            )

            self.assertEqual(payload["totalLatestCreatedFallbacks"], 2)
            self.assertEqual(payload["allowedLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["forbiddenLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["runtimeForbiddenLatestCreatedFallbacks"], 1)
            self.assertEqual(
                payload["latestCreatedFallbackFindings"][0]["lineExecutionSurfaces"],
                ["Editor", "Runtime"],
            )

    def test_try_get_latest_created_diagnostic_name_alone_does_not_allow_runtime(self) -> None:
        with temporary_directory(prefix="h8_vault_latest_created_diag_name_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            runtime = source / "Gameplay" / "DiagnosticHazardRuntime.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                "public sealed class DiagnosticHazardRuntime\n"
                "{\n"
                "    public bool TryResolve() => GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);\n"
                "}\n",
                encoding="utf-8",
            )

            latest_findings = audit.scan_latest_created_fallback_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                latest_created_fallback_findings=latest_findings,
            )

            self.assertEqual(payload["totalLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["forbiddenLatestCreatedFallbacks"], 1)
            self.assertEqual(payload["runtimeForbiddenLatestCreatedFallbacks"], 1)

    def test_editor_sentinel_tracked_constructor_wrapper_is_not_raw_ownership_debt(self) -> None:
        with temporary_directory(prefix="h8_vault_sentinel_wrapper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            baker = source / "Editor" / "HydraulicErosionForge" / "Baker.cs"
            baker.parent.mkdir(parents=True)
            baker.write_text(
                "using Unity.Collections;\n"
                "public static class Baker\n"
                "{\n"
                "    private static NativeArray<T> NewTrackedArray<T>(int length, Allocator allocator) where T : struct\n"
                "    {\n"
                "        NativeArray<T> array = new NativeArray<T>(length, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "        NativeMemorySentinel.RegisterNativeArray(array, \"Owner\", \"Label\", NativeAllocationLifetime.Session);\n"
                "        return array;\n"
                "    }\n"
                "    private static void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct\n"
                "    {\n"
                "        NativeMemorySentinel.UnregisterNativeArray(array);\n"
                "        array.Dispose();\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 0)
            self.assertEqual(payload["sentinelTrackedDirectConstructors"], 1)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 0)

    def test_runtime_sentinel_tracked_constructor_wrapper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_sentinel_wrapper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            save_manager = source / "SaveManager.cs"
            save_manager.parent.mkdir(parents=True)
            save_manager.write_text(
                "using System;\n"
                "using Unity.Collections;\n"
                "public static class SaveManager\n"
                "{\n"
                "    private static void RegisterTransientNativeArray<T>(NativeArray<T> array, string label) where T : struct\n"
                "    {\n"
                "        if (!array.IsCreated)\n"
                "            return;\n"
                "        int registrationId = NativeMemorySentinel.RegisterNativeArray(array, \"SaveManager\", label, NativeAllocationLifetime.Frame);\n"
                "        if (registrationId <= 0)\n"
                "            throw new InvalidOperationException(\"registration failed\");\n"
                "    }\n"
                "    private static NativeArray<T> CreateTransientNativeArray<T>(int length, Allocator allocator, string label) where T : struct\n"
                "    {\n"
                "        NativeArray<T> array = default;\n"
                "        try\n"
                "        {\n"
                "            array = new NativeArray<T>(length, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "            RegisterTransientNativeArray(array, label);\n"
                "            return array;\n"
                "        }\n"
                "        catch\n"
                "        {\n"
                "            if (array.IsCreated)\n"
                "                array.Dispose();\n"
                "            throw;\n"
                "        }\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 0)
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 0)
            self.assertEqual(payload["sentinelTrackedDirectConstructors"], 1)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_unregistered_native_list_constructor_is_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_native_list_debt_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    public static NativeList<int> Allocate(int capacity)\n"
                "    {\n"
                "        NativeList<int> list = new NativeList<int>(capacity, Allocator.Temp);\n"
                "        return list;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["Temp"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 1)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)

    def test_runtime_sentinel_tracked_native_list_constructor_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_native_list_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    public static NativeList<int> Allocate(int capacity)\n"
                "    {\n"
                "        NativeList<int> list = new NativeList<int>(capacity, Allocator.Temp);\n"
                "        NativeMemorySentinel.RegisterNativeListInstance(list, \"SaveBinaryStorage\", \"list\", NativeAllocationLifetime.TransientArena);\n"
                "        return list;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_bulk_sentinel_tracked_native_list_constructor_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_native_list_bulk_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "EncounterDirector.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public sealed class EncounterDirector\n"
                "{\n"
                "    private NativeList<int> _headlessEntities;\n"
                "    public EncounterDirector()\n"
                "    {\n"
                "        try\n"
                "        {\n"
                "            _headlessEntities = new NativeList<int>(1024, Allocator.Persistent);\n"
                "        }\n"
                "        catch\n"
                "        {\n"
                "            throw;\n"
                "        }\n"
                "        RegisterNativeMemorySentinel();\n"
                "    }\n"
                "    private void RegisterNativeMemorySentinel()\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeList(_headlessEntities, nameof(EncounterDirector), nameof(_headlessEntities), NativeAllocationLifetime.Scene);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_editor_qualified_native_list_assignment_direct_register_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_editor_qualified_native_list_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Editor" / "GeographySanity" / "GeographySanityProfileCsv.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public struct GeographySanityProfileStore\n"
                "{\n"
                "    private NativeList<int> _profiles;\n"
                "    public static GeographySanityProfileStore Create(int capacity, Allocator allocator)\n"
                "    {\n"
                "        GeographySanityProfileStore store = default;\n"
                "        store._profiles = new NativeList<int>(capacity, allocator);\n"
                "        NativeMemorySentinel.RegisterNativeList(store._profiles, \"Geography\", \"profiles\", NativeAllocationLifetime.Session);\n"
                "        return store;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["sentinelTrackedDirectConstructors"], 1)

    def test_runtime_local_native_list_helper_named_like_sentinel_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_native_list_same_name_helper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "QuestStateManager.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public sealed class QuestStateManager\n"
                "{\n"
                "    private NativeList<int> _activatedQuestIndices;\n"
                "    public bool Initialize()\n"
                "    {\n"
                "        _activatedQuestIndices = new NativeList<int>(64, Allocator.Persistent);\n"
                "        RegisterNativeList(_activatedQuestIndices, nameof(_activatedQuestIndices));\n"
                "        return true;\n"
                "    }\n"
                "    private static void RegisterNativeList<T>(NativeList<T> list, string label) where T : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeList(list, nameof(QuestStateManager), label, NativeAllocationLifetime.Scene);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_zero_arg_preview_workspace_registration_tracks_each_field(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_preview_workspace_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Editor" / "LSystemGenomeLabWindow.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public sealed class LSystemGenomeLabWindow\n"
                "{\n"
                "    private NativeArray<byte> _expandedSymbols;\n"
                "    private NativeArray<byte> _scratchSymbols;\n"
                "    public void Create(int capacity, Allocator allocator)\n"
                "    {\n"
                "        _expandedSymbols = new NativeArray<byte>(capacity, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "        _scratchSymbols = new NativeArray<byte>(capacity, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "        RegisterPreviewWorkspace();\n"
                "    }\n"
                "    private void RegisterPreviewWorkspace()\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeArray(_expandedSymbols, \"Preview\", nameof(_expandedSymbols), NativeAllocationLifetime.Session);\n"
                "        NativeMemorySentinel.RegisterNativeArray(_scratchSymbols, \"Preview\", nameof(_scratchSymbols), NativeAllocationLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["sentinelTrackedDirectConstructors"], 2)

    def test_runtime_sentinel_tracked_native_parallel_hash_map_constructor_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_parallel_hash_map_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    public static NativeParallelHashMap<int, int> Allocate(int capacity)\n"
                "    {\n"
                "        NativeParallelHashMap<int, int> map = new NativeParallelHashMap<int, int>(capacity, Allocator.Temp);\n"
                "        NativeMemorySentinel.RegisterNativeParallelHashMapInstance(map, \"SaveBinaryStorage\", \"map\", NativeAllocationLifetime.TransientArena);\n"
                "        return map;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_hash_map_helper_registration_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_hash_map_helper_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "ModCommandDispatcher.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class ModCommandDispatcher\n"
                "{\n"
                "    private static NativeHashMap<uint, int> _modIndexByHash;\n"
                "    public static void Initialize()\n"
                "    {\n"
                "        _modIndexByHash = new NativeHashMap<uint, int>(32, Allocator.Persistent);\n"
                "        RegisterHashMap(ref _modIndexByHash, nameof(_modIndexByHash));\n"
                "    }\n"
                "    private static void RegisterHashMap<TValue>(ref NativeHashMap<uint, TValue> map, string label) where TValue : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeHashMap(map, nameof(ModCommandDispatcher), label, NativeAllocationLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_reflective_native_array_bridge_helper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_reflective_bridge_helper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Gameplay" / "ReflectiveNativeBridge.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using System;\n"
                "using System.Reflection;\n"
                "using Unity.Collections;\n"
                "public static class ReflectiveNativeBridge\n"
                "{\n"
                "    private const string NativeMemorySentinelTypeName = \"Hecton8.Core.NativeMemorySentinel\";\n"
                "    public static NativeArray<int> Allocate(int length, Allocator allocator)\n"
                "    {\n"
                "        NativeArray<int> array = new NativeArray<int>(length, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "        RegisterTrackedNativeArray(array, \"array\", \"Session\");\n"
                "        return array;\n"
                "    }\n"
                "    private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label, string lifetimeName) where T : struct\n"
                "    {\n"
                "        Type sentinelType = FindType(NativeMemorySentinelTypeName);\n"
                "        MethodInfo method = sentinelType.GetMethod(\"RegisterNativeArray\", BindingFlags.Public | BindingFlags.Static);\n"
                "        object lifetime = lifetimeName;\n"
                "        method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { array, \"ReflectiveNativeBridge\", label, lifetime });\n"
                "    }\n"
                "    private static Type FindType(string name) { return typeof(object); }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_reflective_native_memory_sentinel_helper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_reflective_sentinel_helper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Gameplay" / "ReflectiveNativeBlackBox.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using System;\n"
                "using System.Reflection;\n"
                "using Unity.Collections;\n"
                "public static class ReflectiveNativeBlackBox\n"
                "{\n"
                "    public static NativeArray<int> Allocate(int length, Allocator allocator)\n"
                "    {\n"
                "        NativeArray<int> array = new NativeArray<int>(length, allocator, NativeArrayOptions.UninitializedMemory);\n"
                "        RegisterNativeMemorySentinel(array, \"array\", \"Session\");\n"
                "        return array;\n"
                "    }\n"
                "    private static void RegisterNativeMemorySentinel<T>(NativeArray<T> array, string label, string lifetimeName) where T : struct\n"
                "    {\n"
                "        Type sentinelType = FindType(\"Hecton8.Core.NativeMemorySentinel\");\n"
                "        MethodInfo method = sentinelType.GetMethod(\"RegisterNativeArray\", BindingFlags.Public | BindingFlags.Static);\n"
                "        object lifetime = lifetimeName;\n"
                "        method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { array, \"ReflectiveNativeBlackBox\", label, lifetime });\n"
                "    }\n"
                "    private static Type FindType(string name) { return typeof(object); }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_bridge_tracked_native_array_constructor_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_bridge_native_array_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Core" / "Contracts" / "CoreLowLevelUtilities.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class NativeFaultDumpWriter\n"
                "{\n"
                "    public static NativeArray<byte> CreateTransientPayload(int byteCount, Allocator allocator, NativeArrayOptions options)\n"
                "    {\n"
                "        NativeArray<byte> payload = new NativeArray<byte>(byteCount, allocator, options);\n"
                "        int id = Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArray(\n"
                "            payload,\n"
                "            \"NativeFaultDumpWriter\",\n"
                "            \"payload\",\n"
                "            Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Temp);\n"
                "        if (id <= 0) throw new System.InvalidOperationException();\n"
                "        return payload;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_bridge_tracked_native_array_helper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_bridge_native_array_helper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Core" / "Contracts" / "CoreLowLevelUtilities.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class NativeFaultDumpWriter\n"
                "{\n"
                "    public static NativeArray<byte> CreateTransientPayload(int byteCount, Allocator allocator, NativeArrayOptions options)\n"
                "    {\n"
                "        NativeArray<byte> payload = new NativeArray<byte>(byteCount, allocator, options);\n"
                "        bool registered = TryRegisterTransientNativeArrayPayload(payload, \"NativeFaultDumpWriter\", \"payload\");\n"
                "        if (!registered) throw new System.InvalidOperationException();\n"
                "        return payload;\n"
                "    }\n"
                "    private static bool TryRegisterTransientNativeArrayPayload(NativeArray<byte> payload, string owner, string label)\n"
                "    {\n"
                "        return Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArray(\n"
                "            payload,\n"
                "            owner,\n"
                "            label,\n"
                "            Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Temp) > 0;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_multiline_sentinel_tracked_constructor_wrapper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_multiline_sentinel_wrapper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    public static NativeArray<int> Allocate(int length)\n"
                "    {\n"
                "        NativeArray<int> records =\n"
                "            new NativeArray<int>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);\n"
                "        NativeMemorySentinel.RegisterNativeArray(records, \"SaveBinaryStorage\", \"Records\", NativeAllocationLifetime.TransientArena);\n"
                "        return records;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_bulk_sentinel_registered_field_assignment_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_bulk_sentinel_wrapper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    private ref struct WriteHandle\n"
                "    {\n"
                "        public NativeArray<int> SourceStates;\n"
                "        internal void RegisterNativeMemorySentinel()\n"
                "        {\n"
                "            RegisterArray(SourceStates, \"SourceStates\");\n"
                "        }\n"
                "        private static void RegisterArray<T>(NativeArray<T> array, string label) where T : struct\n"
                "        {\n"
                "            NativeMemorySentinel.RegisterNativeArray(array, \"SaveBinaryStorage\", label, NativeAllocationLifetime.TempJob);\n"
                "        }\n"
                "    }\n"
                "    public static void Allocate(int length)\n"
                "    {\n"
                "        WriteHandle handle = default;\n"
                "        handle.SourceStates = new NativeArray<int>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);\n"
                "        handle.RegisterNativeMemorySentinel();\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["SentinelTracked"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 1)

    def test_runtime_bulk_sentinel_registration_does_not_cover_unregistered_field_assignment(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_bulk_sentinel_negative_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    private ref struct WriteHandle\n"
                "    {\n"
                "        public NativeArray<int> SourceStates;\n"
                "        public NativeArray<int> OtherStates;\n"
                "        internal void RegisterNativeMemorySentinel()\n"
                "        {\n"
                "            RegisterArray(OtherStates, \"OtherStates\");\n"
                "        }\n"
                "        private static void RegisterArray<T>(NativeArray<T> array, string label) where T : struct\n"
                "        {\n"
                "            NativeMemorySentinel.RegisterNativeArray(array, \"SaveBinaryStorage\", label, NativeAllocationLifetime.TempJob);\n"
                "        }\n"
                "    }\n"
                "    public static void Allocate(int length)\n"
                "    {\n"
                "        WriteHandle handle = default;\n"
                "        handle.SourceStates = new NativeArray<int>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);\n"
                "        handle.RegisterNativeMemorySentinel();\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["TempJob"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 1)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)

    def test_runtime_nested_sidecar_sentinel_registration_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_nested_sidecar_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Core" / "Memory" / "GlobalDataVault.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public sealed class GlobalDataVault\n"
                "{\n"
                "    private NativeList<int> _keys;\n"
                "    private NativeList<long> _blocks;\n"
                "    private NativeParallelHashMap<ulong, int> _macroDatabasePayloadCache;\n"
                "    private NativeParallelHashMap<ulong, uint> _macroDatabasePayloadAccessTicks;\n"
                "    private NativeList<ulong> _macroDatabasePayloadKeys;\n"
                "    public void Initialize(int capacity)\n"
                "    {\n"
                "        _keys = new NativeList<int>(capacity, Allocator.Persistent);\n"
                "        _blocks = new NativeList<long>(capacity, Allocator.Persistent);\n"
                "        _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, int>(capacity, Allocator.Persistent);\n"
                "        _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(capacity, Allocator.Persistent);\n"
                "        _macroDatabasePayloadKeys = new NativeList<ulong>(capacity, Allocator.Persistent);\n"
                "        RegisterNativeSidecarStorage();\n"
                "    }\n"
                "    private void RegisterNativeSidecarStorage()\n"
                "    {\n"
                "        RegisterCoreSidecarSentinels();\n"
                "        RegisterMacroDatabasePayloadCacheSentinels();\n"
                "    }\n"
                "    private void RegisterCoreSidecarSentinels()\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeListInstance(_keys, \"GlobalDataVault\", nameof(_keys), NativeAllocationLifetime.Session);\n"
                "        NativeMemorySentinel.RegisterNativeListInstance(_blocks, \"GlobalDataVault\", nameof(_blocks), NativeAllocationLifetime.Session);\n"
                "    }\n"
                "    private void RegisterMacroDatabasePayloadCacheSentinels()\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeParallelHashMapInstance(_macroDatabasePayloadCache, \"GlobalDataVault\", nameof(_macroDatabasePayloadCache), NativeAllocationLifetime.Session);\n"
                "        NativeMemorySentinel.RegisterNativeParallelHashMapInstance(_macroDatabasePayloadAccessTicks, \"GlobalDataVault\", nameof(_macroDatabasePayloadAccessTicks), NativeAllocationLifetime.Session);\n"
                "        NativeMemorySentinel.RegisterNativeListInstance(_macroDatabasePayloadKeys, \"GlobalDataVault\", nameof(_macroDatabasePayloadKeys), NativeAllocationLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 5)

    def test_runtime_no_arg_register_native_arrays_bridge_wrapper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_register_native_arrays_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Graphics" / "Culling" / "TBDRPipelineSurgeonTypes.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public struct TBDRVertexBudgetVault\n"
                "{\n"
                "    public NativeArray<int> VertexBudgetCounters;\n"
                "    public NativeArray<int> TileWarnings;\n"
                "    public NativeArray<int> TransparentQuadCount;\n"
                "    public NativeArray<int> TelemetryRing;\n"
                "    public TBDRVertexBudgetVault(int capacity)\n"
                "    {\n"
                "        VertexBudgetCounters = new NativeArray<int>(capacity, Allocator.Persistent);\n"
                "        TileWarnings = new NativeArray<int>(capacity, Allocator.Persistent);\n"
                "        TransparentQuadCount = new NativeArray<int>(capacity, Allocator.Persistent);\n"
                "        TelemetryRing = new NativeArray<int>(capacity, Allocator.Persistent);\n"
                "        RegisterNativeArrays();\n"
                "    }\n"
                "    private void RegisterNativeArrays()\n"
                "    {\n"
                "        NativeMemoryTrackingBridge.RegisterNativeArray(VertexBudgetCounters, \"TBDR\", nameof(VertexBudgetCounters), NativeMemoryBridgeLifetime.Session);\n"
                "        NativeMemoryTrackingBridge.RegisterNativeArray(TileWarnings, \"TBDR\", nameof(TileWarnings), NativeMemoryBridgeLifetime.Session);\n"
                "        NativeMemoryTrackingBridge.RegisterNativeArray(TransparentQuadCount, \"TBDR\", nameof(TransparentQuadCount), NativeMemoryBridgeLifetime.Session);\n"
                "        NativeMemoryTrackingBridge.RegisterNativeArray(TelemetryRing, \"TBDR\", nameof(TelemetryRing), NativeMemoryBridgeLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["runtimeSentinelTrackedDirectConstructors"], 4)

    def test_runtime_register_temp_job_buffers_helper_tracks_each_argument(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_tempjob_buffers_helper_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Plugins" / "MapMagic" / "HectonBiomeMatrixMapMagicPostProcessNode.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public sealed class HectonBiomeMatrixMapMagicPostProcessNode\n"
                "{\n"
                "    public void Generate(int cellCount)\n"
                "    {\n"
                "        NativeArray<float> bufferA = default;\n"
                "        NativeArray<float> bufferB = default;\n"
                "        int bufferARegistrationId = 0;\n"
                "        int bufferBRegistrationId = 0;\n"
                "        bufferA = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);\n"
                "        bufferB = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);\n"
                "        RegisterTempJobBuffers(bufferA, bufferB, out bufferARegistrationId, out bufferBRegistrationId);\n"
                "    }\n"
                "    private static void RegisterTempJobBuffers(NativeArray<float> bufferA, NativeArray<float> bufferB, out int bufferARegistrationId, out int bufferBRegistrationId)\n"
                "    {\n"
                "        bufferARegistrationId = RegisterTempJobArray(bufferA, \"A\");\n"
                "        bufferBRegistrationId = RegisterTempJobArray(bufferB, \"B\");\n"
                "    }\n"
                "    private static int RegisterTempJobArray<T>(NativeArray<T> array, string label) where T : struct\n"
                "    {\n"
                "        return NativeMemorySentinel.RegisterNativeArray(array, \"MapMagic\", label, NativeAllocationLifetime.TempJob);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["forbiddenDirectConstructors"], 0)
            self.assertEqual(payload["sentinelTrackedDirectConstructors"], 2)

    def test_runtime_unregistered_constructor_remains_raw_ownership_debt_with_sentinel_elsewhere(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_runtime_unregistered_sentinel_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            bad_owner = source / "Gameplay" / "BadNativeOwner.cs"
            bad_owner.parent.mkdir(parents=True)
            bad_owner.write_text(
                "using Unity.Collections;\n"
                "public static class BadNativeOwner\n"
                "{\n"
                "    private static void RegisterOther(NativeArray<int> other)\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeArray(other, \"Owner\", \"Other\", NativeAllocationLifetime.Session);\n"
                "    }\n"
                "    public static NativeArray<int> Allocate(int length)\n"
                "    {\n"
                "        NativeArray<int> scratch = new NativeArray<int>(length, Allocator.Persistent);\n"
                "        return scratch;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["findings"][0]["allocatorKinds"], ["Persistent"])
            self.assertEqual(payload["forbiddenDirectConstructors"], 1)
            self.assertEqual(payload["runtimeForbiddenDirectConstructors"], 1)

    def test_scan_tracks_nativearray_field_declaration_debt(self) -> None:
        with temporary_directory(prefix="h8_vault_declaration_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            h8memory = source / "Core" / "Memory" / "H8Memory.cs"
            gameplay = source / "Gameplay" / "StatefulSystem.cs"
            h8memory.parent.mkdir(parents=True)
            gameplay.parent.mkdir(parents=True)
            h8memory.write_text(
                "private NativeArray<int> _allocatorScratch;\n",
                encoding="utf-8",
            )
            gameplay.write_text(
                "private NativeArray<int> _localState;\n"
                "[ReadOnly] public NativeArray<float> JobView;\n"
                "public NativeArray<int> View => _localState;\n"
                "NativeArray<int> localOnly = default;\n"
                "// private NativeArray<byte> _commented;\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["totalNativeArrayDeclarations"], 3)
            self.assertEqual(payload["allowedNativeArrayDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeArrayDeclarations"], 2)
            self.assertEqual(payload["declarationFileCount"], 1)

    def test_registered_transient_nativearray_owner_struct_is_not_declaration_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_registered_owner_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "SaveBinaryStorage.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    private struct RegisteredTransientNativeArray<T> where T : struct\n"
                "    {\n"
                "        public NativeArray<T> Array;\n"
                "        public void Dispose() {}\n"
                "    }\n"
                "    private struct UnregisteredScratch<T> where T : struct\n"
                "    {\n"
                "        public NativeArray<T> Array;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeArrayDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeArrayDeclarations"], 1)
            classifications = {
                finding["ownerType"]: finding["classification"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(
                classifications["RegisteredTransientNativeArray"],
                "registeredNativeOwnerStruct",
            )
            self.assertEqual(classifications["UnregisteredScratch"], "unknownStructField")

    def test_ref_struct_native_collection_carrier_is_native_view_declaration(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_ref_struct_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            storage = source / "Inventory" / "InventoryDefragJob.cs"
            storage.parent.mkdir(parents=True)
            storage.write_text(
                "using Unity.Collections;\n"
                "public ref struct InventoryDefragCommand\n"
                "{\n"
                "    public NativeArray<int> ItemHashes;\n"
                "}\n"
                "public ref partial struct MockPlayerInventory\n"
                "{\n"
                "    public NativeArray<uint> ItemHashes;\n"
                "    public NativeArray<int> Quantities;\n"
                "}\n"
                "public struct UnregisteredScratch\n"
                "{\n"
                "    public NativeArray<int> Scratch;\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"]: finding["classification"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(payload["totalNativeCollectionDeclarations"], 4)
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 3)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["InventoryDefragCommand"], "nativeViewStruct")
            self.assertEqual(classifications["MockPlayerInventory"], "nativeViewStruct")
            self.assertEqual(classifications["UnregisteredScratch"], "unknownStructField")

    def test_classifies_job_input_native_collections_separately_from_owner_debt(self) -> None:
        with temporary_directory(prefix="h8_vault_job_declaration_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "Gameplay" / "SignalJobs.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Unity.Burst;\n"
                "using Unity.Collections;\n"
                "using Unity.Jobs;\n"
                "public sealed class HazardZoneManager\n"
                "{\n"
                "    private NativeArray<float> _volumes;\n"
                "    [BurstCompile]\n"
                "    private struct FunnelJob : IJobParallelFor\n"
                "    {\n"
                "        [ReadOnly] public NativeArray<float> Taps;\n"
                "        public NativeList<int> Output;\n"
                "        public void Execute(int index) {}\n"
                "    }\n"
                "    private struct EchoTrackingJob : IJob\n"
                "    {\n"
                "        [ReadOnly] public NativeParallelHashMap<int, float> Echoes;\n"
                "        public void Execute() {}\n"
                "    }\n"
                "    private struct RetinalAdaptationVaultState\n"
                "    {\n"
                "        public NativeHashMap<int, float> Exposure;\n"
                "    }\n"
                "    private struct RetinalAdaptationVaultBuffers\n"
                "    {\n"
                "        public NativeHashMap<int, float> ScratchExposure;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["classification"]: finding["count"]
                for finding in payload["declarationFindings"]
            }

            self.assertEqual(payload["totalNativeCollectionDeclarations"], 6)
            self.assertEqual(payload["jobInputNativeCollectionDeclarations"], 3)
            self.assertEqual(payload["burstJobInputNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["persistentNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["unknownStructNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["nativeViewNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 2)
            self.assertEqual(classifications["persistentOwnerField"], 1)
            self.assertEqual(classifications["unknownStructField"], 1)
            self.assertEqual(classifications["nativeViewStruct"], 1)

    def test_classifies_generic_and_animation_jobs_as_job_input_declarations(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_generic_job_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "Gameplay" / "ContextualPhysicalIkRig.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Unity.Collections;\n"
                "using Unity.Jobs;\n"
                "using UnityEngine.Animations;\n"
                "public struct NativeFilterJob<T> : IJob where T : unmanaged\n"
                "{\n"
                "    public NativeArray<T> Values;\n"
                "    public void Execute() {}\n"
                "}\n"
                "internal struct ContextualPhysicalIkApplyJob : IAnimationJob\n"
                "{\n"
                "    public NativeArray<int> StreamHandles;\n"
                "    public void ProcessRootMotion(AnimationStream stream) {}\n"
                "    public void ProcessAnimation(AnimationStream stream) {}\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["totalNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["jobInputNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)

    def test_named_pending_and_scratch_structs_are_native_view_declarations(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_named_view_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "World" / "GroundPenetratingRadarRuntime.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Unity.Collections;\n"
                "using Unity.Jobs;\n"
                "public sealed class GroundPenetratingRadarRuntime\n"
                "{\n"
                "    private struct RadarPendingJob\n"
                "    {\n"
                "        public NativeArray<int> Counters;\n"
                "        public JobHandle Handle;\n"
                "    }\n"
                "    private struct SimulationNativeScratch\n"
                "    {\n"
                "        public NativeArray<int> Counters;\n"
                "    }\n"
                "    private struct ReadbackDataOwner\n"
                "    {\n"
                "        public NativeArray<int> Data;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"]: finding["classification"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(payload["totalNativeCollectionDeclarations"], 3)
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["RadarPendingJob"], "nativeViewStruct")
            self.assertEqual(classifications["SimulationNativeScratch"], "nativeViewStruct")
            self.assertEqual(classifications["ReadbackDataOwner"], "unknownStructField")

    def test_data_struct_native_collection_aliases_are_native_view_declarations(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_data_view_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "World" / "GlobalWorldSampler.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Unity.Collections;\n"
                "public struct GlobalWorldSamplerData\n"
                "{\n"
                "    [ReadOnly] public NativeArray<ushort> HeightSamples;\n"
                "}\n"
                "public struct ReadbackDataOwner\n"
                "{\n"
                "    public NativeArray<int> Data;\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"]: finding["classification"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["GlobalWorldSamplerData"], "nativeViewStruct")
            self.assertEqual(classifications["ReadbackDataOwner"], "unknownStructField")

    def test_h8memory_tracked_owner_field_declaration_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_tracked_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "InventoryGrid.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public sealed class InventoryGrid\n"
                "{\n"
                "    private NativeArray<int> _cellAnchorIndices;\n"
                "    private NativeArray<int> _missingRelease;\n"
                "    public void Initialize(int count)\n"
                "    {\n"
                "        _cellAnchorIndices = H8Memory.Allocate<int>(count, SystemID.InventorySystem, Allocator.Persistent);\n"
                "        _missingRelease = H8Memory.Allocate<int>(count, SystemID.InventorySystem, Allocator.Persistent);\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        H8Memory.Release(ref _cellAnchorIndices, SystemID.InventorySystem);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"] + "." + finding["names"][0]: finding["classification"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["InventoryGrid._cellAnchorIndices"], "h8MemoryTrackedOwnerField")
            self.assertEqual(classifications["InventoryGrid._missingRelease"], "persistentOwnerField")

    def test_h8memory_helper_released_owner_field_declaration_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_helper_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "EncounterDirector.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "using Unity.Jobs;\n"
                "public sealed class EncounterDirector\n"
                "{\n"
                "    private NativeArray<int> _frontState;\n"
                "    public void Initialize(int count)\n"
                "    {\n"
                "        _frontState = H8Memory.Allocate<int>(count, SystemID.EncounterDirector, Allocator.Persistent);\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        JobHandle handle = default;\n"
                "        bool hasDependency = false;\n"
                "        DisposeNativeArray(ref _frontState, ref handle, ref hasDependency);\n"
                "    }\n"
                "    private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle handle, ref bool hasDependency) where T : struct\n"
                "    {\n"
                "        if (hasDependency)\n"
                "            handle = H8Memory.Release(ref array, handle, SystemID.EncounterDirector);\n"
                "        else\n"
                "            H8Memory.Release(ref array, SystemID.EncounterDirector);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "h8MemoryTrackedOwnerField")

    def test_h8memory_static_owner_fields_with_ref_dispose_helper_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_static_helper_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            core = source / "Core" / "UIStateStore.cs"
            core.parent.mkdir(parents=True)
            core.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "using Unity.Jobs;\n"
                "public static class UIStateStore\n"
                "{\n"
                "    private const SystemID OwnerSystemId = SystemID.UI;\n"
                "    private static NativeArray<int> _states;\n"
                "    private static NativeArray<int> _historyStates;\n"
                "    public static void EnsureInitialized()\n"
                "    {\n"
                "        _states = H8Memory.Allocate<int>(16, OwnerSystemId, Allocator.Persistent);\n"
                "        _historyStates = H8Memory.Allocate<int>(4, OwnerSystemId, Allocator.Persistent);\n"
                "    }\n"
                "    public static void Shutdown()\n"
                "    {\n"
                "        JobHandle disposeHandle = default;\n"
                "        DisposeNativeArray(ref _states, ref disposeHandle);\n"
                "        DisposeNativeArray(ref _historyStates, ref disposeHandle);\n"
                "    }\n"
                "    private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle dependency) where T : struct\n"
                "    {\n"
                "        dependency = H8Memory.Release(ref array, dependency, OwnerSystemId);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "h8MemoryTrackedOwnerField")

    def test_h8memory_tracked_struct_owner_field_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_struct_owner_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            core = source / "Core" / "BurstCallback.cs"
            core.parent.mkdir(parents=True)
            core.write_text(
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public struct BurstCallbackQueue\n"
                "{\n"
                "    private NativeArray<int> _counters;\n"
                "    public BurstCallbackQueue(int capacity)\n"
                "    {\n"
                "        _counters = Hecton8.Core.Memory.H8Memory.Allocate<int>(capacity, SystemID.CoreDiagnostics, Allocator.Persistent);\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        Hecton8.Core.Memory.H8Memory.Release(ref _counters, SystemID.CoreDiagnostics);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "h8MemoryTrackedOwnerField")

    def test_h8memory_constructor_alias_owner_fields_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_constructor_alias_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            editor = source / "Core" / "Memory" / "Editor" / "OOP_MemorySentryConcurrentRelocationFuzzer.cs"
            editor.parent.mkdir(parents=True)
            editor.write_text(
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public static class OOP_MemorySentryConcurrentRelocationFuzzer\n"
                "{\n"
                "    private const SystemID Owner = SystemID.CoreDiagnostics;\n"
                "    private sealed class FuzzerState\n"
                "    {\n"
                "        public readonly NativeArray<int> JobFailures;\n"
                "        public FuzzerState(NativeArray<int> jobFailures)\n"
                "        {\n"
                "            JobFailures = jobFailures;\n"
                "        }\n"
                "    }\n"
                "    public static void Run()\n"
                "    {\n"
                "        NativeArray<int> jobFailures = H8Memory.Allocate<int>(8, Owner, Allocator.Persistent);\n"
                "        FuzzerState state = new FuzzerState(jobFailures);\n"
                "        H8Memory.Release(ref jobFailures, Owner);\n"
                "    }\n"
                "    private static void DeferredCleanupAfterTimeout(FuzzerState state)\n"
                "    {\n"
                "        NativeArray<int> jobFailures = state.JobFailures;\n"
                "        H8Memory.Release(ref jobFailures, Owner);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "h8MemoryTrackedOwnerField")

    def test_h8memory_helper_allocated_and_released_owner_field_declaration_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_helper_alloc_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "AI" / "Ecosystem" / "ShinobuEcosystemBalancer.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public sealed class ShinobuEcosystemBalancer\n"
                "{\n"
                "    private NativeArray<int> _ecosystemTelemetryMirror;\n"
                "    public bool EnsureTelemetryMirrorsCold()\n"
                "    {\n"
                "        EnsureNativeMirrorArray(ref _ecosystemTelemetryMirror, 16, nameof(_ecosystemTelemetryMirror));\n"
                "        return true;\n"
                "    }\n"
                "    private static void EnsureNativeMirrorArray<T>(ref NativeArray<T> array, int length, string label) where T : struct\n"
                "    {\n"
                "        DisposeNativeMirrorArray(ref array);\n"
                "        array = H8Memory.Allocate<T>(length, SystemID.AIEcology, Allocator.Persistent);\n"
                "    }\n"
                "    private void DisposeTelemetryMirrorsCold()\n"
                "    {\n"
                "        DisposeNativeMirrorArray(ref _ecosystemTelemetryMirror);\n"
                "    }\n"
                "    private static void DisposeNativeMirrorArray<T>(ref NativeArray<T> array) where T : struct\n"
                "    {\n"
                "        H8Memory.Release(ref array, SystemID.AIEcology);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "h8MemoryTrackedOwnerField")

    def test_h8memory_factory_allocated_qualified_owner_fields_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_h8memory_factory_qualified_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            core = source / "Core" / "ReplayRecorder.cs"
            core.parent.mkdir(parents=True)
            core.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public sealed class ReplayRecorder\n"
                "{\n"
                "    private const SystemID NativeMemoryOwner = SystemID.CoreDiagnostics;\n"
                "    private NativeBufferSet _buffers = new NativeBufferSet();\n"
                "    public void Initialize(int count)\n"
                "    {\n"
                "        _buffers.Sources = AllocateNativeArray<int>(count, nameof(_buffers.Sources));\n"
                "        _buffers.Hashes = AllocateNativeArray<int>(count, nameof(_buffers.Hashes));\n"
                "        _buffers.Leaked = AllocateNativeArray<int>(count, nameof(_buffers.Leaked));\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        DisposeNativeArray(ref _buffers.Sources);\n"
                "        DisposeNativeArray(ref _buffers.Hashes);\n"
                "    }\n"
                "    private static NativeArray<T> AllocateNativeArray<T>(int length, string label) where T : struct\n"
                "    {\n"
                "        return H8Memory.Allocate<T>(length, NativeMemoryOwner, Allocator.Persistent);\n"
                "    }\n"
                "    private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct\n"
                "    {\n"
                "        H8Memory.Release(ref array, NativeMemoryOwner);\n"
                "    }\n"
                "    private sealed class NativeBufferSet\n"
                "    {\n"
                "        public NativeArray<int> Sources;\n"
                "        public NativeArray<int> Hashes;\n"
                "        public NativeArray<int> Leaked;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"] + "." + name: finding["classification"]
                for finding in payload["declarationFindings"]
                for name in finding["names"]
            }
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 2)
            self.assertEqual(classifications["NativeBufferSet.Sources"], "h8MemoryTrackedOwnerField")
            self.assertEqual(classifications["NativeBufferSet.Hashes"], "h8MemoryTrackedOwnerField")
            self.assertEqual(classifications["NativeBufferSet.Leaked"], "persistentOwnerField")

    def test_sentinel_tracked_hash_map_owner_fields_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_hash_map_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            modding = source / "ModdingAPI" / "ModCommandDispatcher.cs"
            modding.parent.mkdir(parents=True)
            modding.write_text(
                "using Unity.Collections;\n"
                "public static class ModCommandDispatcher\n"
                "{\n"
                "    private static NativeHashMap<uint, int> _modIndexByHash;\n"
                "    private static NativeHashMap<uint, int> _kernelIndexByCommandKey;\n"
                "    public static void Initialize()\n"
                "    {\n"
                "        _modIndexByHash = new NativeHashMap<uint, int>(32, Allocator.Persistent);\n"
                "        RegisterHashMap(ref _modIndexByHash, nameof(_modIndexByHash));\n"
                "        _kernelIndexByCommandKey = new NativeHashMap<uint, int>(32, Allocator.Persistent);\n"
                "        RegisterHashMap(ref _kernelIndexByCommandKey, nameof(_kernelIndexByCommandKey));\n"
                "    }\n"
                "    public static void Shutdown()\n"
                "    {\n"
                "        NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher), nameof(_modIndexByHash));\n"
                "        NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher), nameof(_kernelIndexByCommandKey));\n"
                "    }\n"
                "    private static void RegisterHashMap<TValue>(ref NativeHashMap<uint, TValue> map, string label) where TValue : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeHashMap(map, nameof(ModCommandDispatcher), label, NativeAllocationLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["sentinelTrackedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "sentinelTrackedOwnerField")

    def test_sentinel_factory_assigned_owner_fields_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_factory_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            save = source / "SaveManager.cs"
            save.parent.mkdir(parents=True)
            save.write_text(
                "using System;\n"
                "using Unity.Collections;\n"
                "public sealed class SaveManager\n"
                "{\n"
                "    private const string NativeMemoryOwner = nameof(SaveManager);\n"
                "    private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;\n"
                "    private NativeArray<byte> SavePayloadBuffer;\n"
                "    public void EnsureSavePayloadBuffer()\n"
                "    {\n"
                "        SavePayloadBuffer = CreatePersistentNativeArray<byte>(64, NativeArrayOptions.ClearMemory, nameof(SavePayloadBuffer));\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        Exception firstException = null;\n"
                "        DisposeNativeArrayBestEffort(ref SavePayloadBuffer, ref firstException, sentinelLabel: nameof(SavePayloadBuffer));\n"
                "    }\n"
                "    private static NativeArray<T> CreatePersistentNativeArray<T>(int length, NativeArrayOptions options, string sentinelLabel) where T : struct\n"
                "    {\n"
                "        NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, options);\n"
                "        NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, sentinelLabel, NativeMemoryLifetime);\n"
                "        return array;\n"
                "    }\n"
                "    private static void DisposeNativeArrayBestEffort<T>(ref NativeArray<T> array, ref Exception firstException, string sentinelLabel = null) where T : struct\n"
                "    {\n"
                "        DisposeNativeArray(ref array, sentinelLabel: sentinelLabel);\n"
                "    }\n"
                "    private static void DisposeNativeArray<T>(ref NativeArray<T> array, string sentinelLabel = null) where T : struct\n"
                "    {\n"
                "        NativeMemorySentinel.UnregisterNativeArray(array);\n"
                "        array.Dispose();\n"
                "        array = default;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["sentinelTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "sentinelTrackedOwnerField")

    def test_sentinel_id_backed_struct_owner_field_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_id_struct_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            save = source / "SaveBinaryStorage.cs"
            save.parent.mkdir(parents=True)
            save.write_text(
                "using Unity.Collections;\n"
                "public static class SaveBinaryStorage\n"
                "{\n"
                "    private struct CachedReadWindow\n"
                "    {\n"
                "        public NativeArray<byte> Bytes;\n"
                "        public int BytesSentinelId;\n"
                "    }\n"
                "    private static bool TryCreateCachedReadWindow(out CachedReadWindow window)\n"
                "    {\n"
                "        window = default;\n"
                "        NativeArray<byte> windowBytes = new NativeArray<byte>(64, Allocator.Persistent);\n"
                "        int registrationId = NativeMemorySentinel.RegisterNativeArray(windowBytes, nameof(SaveBinaryStorage), nameof(CachedReadWindow), NativeAllocationLifetime.Session);\n"
                "        window = new CachedReadWindow { Bytes = windowBytes, BytesSentinelId = registrationId };\n"
                "        return true;\n"
                "    }\n"
                "    private static void DisposeCachedReadWindow(ref CachedReadWindow window)\n"
                "    {\n"
                "        DisposeCachedReadWindowBytes(ref window.Bytes, ref window.BytesSentinelId);\n"
                "    }\n"
                "    private static void DisposeCachedReadWindowBytes(ref NativeArray<byte> bytes, ref int sentinelId)\n"
                "    {\n"
                "        NativeMemorySentinel.Unregister(sentinelId);\n"
                "        sentinelId = 0;\n"
                "        bytes.Dispose();\n"
                "        bytes = default;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["sentinelTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "sentinelTrackedOwnerField")

    def test_sentinel_label_released_native_list_owner_field_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_label_list_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            world = source / "World" / "ScatterWorkingMemory.cs"
            world.parent.mkdir(parents=True)
            world.write_text(
                "using Unity.Collections;\n"
                "public sealed class ScatterWorkingMemory\n"
                "{\n"
                "    private const string NativeMemoryOwner = nameof(ScatterWorkingMemory);\n"
                "    private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;\n"
                "    public NativeList<int> GridPlacementSpatialMetadata;\n"
                "    public NativeList<int> LeakedMetadata;\n"
                "    public ScatterWorkingMemory()\n"
                "    {\n"
                "        GridPlacementSpatialMetadata = new NativeList<int>(16, Allocator.Persistent);\n"
                "        LeakedMetadata = new NativeList<int>(16, Allocator.Persistent);\n"
                "        RegisterNativeMemorySentinel();\n"
                "    }\n"
                "    private void RegisterNativeMemorySentinel()\n"
                "    {\n"
                "        RegisterNativeList(GridPlacementSpatialMetadata, nameof(GridPlacementSpatialMetadata));\n"
                "        RegisterNativeList(LeakedMetadata, nameof(LeakedMetadata));\n"
                "    }\n"
                "    private static void RegisterNativeList<T>(NativeList<T> list, string label) where T : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeList(list, NativeMemoryOwner, label, NativeMemoryLifetime);\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        DisposeNativeList(ref GridPlacementSpatialMetadata, nameof(GridPlacementSpatialMetadata));\n"
                "    }\n"
                "    private static void DisposeNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);\n"
                "        list.Dispose();\n"
                "        list = default;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["ownerType"] + "." + name: finding["classification"]
                for finding in payload["declarationFindings"]
                for name in finding["names"]
            }
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["sentinelTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["ScatterWorkingMemory.GridPlacementSpatialMetadata"], "sentinelTrackedOwnerField")
            self.assertEqual(classifications["ScatterWorkingMemory.LeakedMetadata"], "persistentOwnerField")

    def test_sentinel_registered_hash_map_without_unregister_remains_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_hash_map_missing_unregister_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            modding = source / "ModdingAPI" / "ModCommandDispatcher.cs"
            modding.parent.mkdir(parents=True)
            modding.write_text(
                "using Unity.Collections;\n"
                "public static class ModCommandDispatcher\n"
                "{\n"
                "    private static NativeHashMap<uint, int> _modIndexByHash;\n"
                "    public static void Initialize()\n"
                "    {\n"
                "        _modIndexByHash = new NativeHashMap<uint, int>(32, Allocator.Persistent);\n"
                "        RegisterHashMap(ref _modIndexByHash, nameof(_modIndexByHash));\n"
                "    }\n"
                "    private static void RegisterHashMap<TValue>(ref NativeHashMap<uint, TValue> map, string label) where TValue : unmanaged\n"
                "    {\n"
                "        NativeMemorySentinel.RegisterNativeHashMap(map, nameof(ModCommandDispatcher), label, NativeAllocationLifetime.Session);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "persistentOwnerField")

    def test_datavault_alias_owner_field_declaration_is_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_alias_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "QA" / "Headless" / "JacobiStressFuzzer" / "PowerGridJacobiStressFuzzer.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Hecton8.Core;\n"
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public sealed class ScheduledRun\n"
                "{\n"
                "    private NativeArray<int> _nodes;\n"
                "    private NativeArray<int> _nodeAup;\n"
                "    private GlobalDataVault _ownedVault;\n"
                "    private bool TryAllocateAndSchedule()\n"
                "    {\n"
                "        IDataVault vault = _ownedVault;\n"
                "        return TryResolveFuzzerVaultBuffer(vault, BufferID.PowerJacobiNodes, 16, NativeArrayOptions.UninitializedMemory, out _nodes) &&\n"
                "            TryResolveFuzzerVaultBuffer(vault, BufferID.PowerJacobiNodeAup, 16, NativeArrayOptions.UninitializedMemory, out _nodeAup);\n"
                "    }\n"
                "    private static bool TryResolveFuzzerVaultBuffer<T>(IDataVault vault, BufferID bufferId, int requiredLength, NativeArrayOptions options, out NativeArray<T> buffer) where T : struct\n"
                "    {\n"
                "        buffer = default;\n"
                "        VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Power, options);\n"
                "        return vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated;\n"
                "    }\n"
                "    private void DisposeVaultOnly()\n"
                "    {\n"
                "        _ownedVault.Dispose();\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["dataVaultAliasNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "dataVaultAliasOwnerField")

    def test_datavault_resolved_context_fields_are_not_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_context_alias_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            signals = source / "Core" / "Signals" / "SignalWardenRuntime.cs"
            signals.parent.mkdir(parents=True)
            signals.write_text(
                "using Unity.Collections;\n"
                "public struct SignalThreadLocalWriteContext\n"
                "{\n"
                "    public NativeArray<byte> Bytes;\n"
                "    public NativeArray<int> Headers;\n"
                "}\n"
                "public static class SignalThreadLocalScratchpad\n"
                "{\n"
                "    private static IDataVault _vault;\n"
                "    private static VaultGenerationHandle<byte> _bytesHandle;\n"
                "    private static VaultGenerationHandle<int> _headersHandle;\n"
                "    public static bool TryAcquireWriteContext(out SignalThreadLocalWriteContext context)\n"
                "    {\n"
                "        context = default;\n"
                "        NativeArray<byte> bytes = default;\n"
                "        NativeArray<int> headers = default;\n"
                "        if (!TryResolve(_vault, in _bytesHandle, out bytes) || !TryResolve(_vault, in _headersHandle, out headers))\n"
                "            return false;\n"
                "        context.Bytes = bytes;\n"
                "        context.Headers = headers;\n"
                "        return true;\n"
                "    }\n"
                "    private static bool TryResolve<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct\n"
                "    {\n"
                "        return vault.TryResolveHandle(in handle, out buffer);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["dataVaultAliasNativeCollectionDeclarations"], 2)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "dataVaultAliasOwnerField")

    def test_plain_out_helper_owner_field_declaration_remains_raw_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_plain_out_declaration_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "Gameplay" / "BadOwner.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "using Unity.Collections;\n"
                "public sealed class BadOwner\n"
                "{\n"
                "    private NativeArray<int> _scratch;\n"
                "    private bool TryAllocate()\n"
                "    {\n"
                "        return TryResolveScratch(out _scratch);\n"
                "    }\n"
                "    private static bool TryResolveScratch(out NativeArray<int> buffer)\n"
                "    {\n"
                "        buffer = default;\n"
                "        return true;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 0)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["declarationFindings"][0]["classification"], "persistentOwnerField")

    def test_classifies_editor_bake_session_and_preview_cache_fields(self) -> None:
        with temporary_directory(prefix="h8_vault_editor_declaration_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            editor = source / "World" / "OfflineHadalTrenchBaker" / "Editor" / "HadalTrenchBakePipeline.cs"
            editor.parent.mkdir(parents=True)
            editor.write_text(
                "using Unity.Collections;\n"
                "public sealed class AsyncTrenchBakeSession\n"
                "{\n"
                "    private NativeArray<float> _densities;\n"
                "}\n"
                "public static class HadalTrenchPreviewStore\n"
                "{\n"
                "    private static NativeArray<int> s_faults;\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["classification"]: finding["count"]
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(classifications["editorOfflineSessionScratchField"], 1)
            self.assertEqual(classifications["editorOfflinePersistentPreviewField"], 1)
            self.assertEqual(payload["editorOfflineSessionScratchNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["editorOfflinePersistentPreviewNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)

    def test_allows_tracked_editor_preview_cache_fields(self) -> None:
        with temporary_directory(prefix="h8_vault_tracked_editor_preview_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            editor = source / "World" / "OfflineHadalTrenchBaker" / "Editor" / "HadalTrenchForgeWindow.cs"
            editor.parent.mkdir(parents=True)
            editor.write_text(
                "using Hecton8.Core.Memory;\n"
                "using Unity.Collections;\n"
                "public static class HadalTrenchPreviewStore\n"
                "{\n"
                "    // H8MEMORY_TRACKED_EDITOR_PREVIEW\n"
                "    private static NativeArray<int> s_faults;\n"
                "    public static void Rebuild() { s_faults = H8Memory.Allocate<int>(4, SystemID.ContentAuthority, Allocator.Persistent); }\n"
                "    public static void Dispose() { H8Memory.Release(ref s_faults, SystemID.ContentAuthority); }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["editorOfflinePersistentPreviewNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 0)

    def test_allows_h8memory_tracked_partial_runtime_fields_only_with_release(self) -> None:
        with temporary_directory(prefix="h8_vault_partial_h8memory_fields_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            world = source / "World"
            owner = world / "SharedRuntime.cs"
            partial = world / "SharedRuntime.Memory.cs"
            world.mkdir(parents=True)
            owner.write_text(
                "using Unity.Collections;\n"
                "public sealed partial class SharedRuntime\n"
                "{\n"
                "    private NativeArray<int> _tracked;\n"
                "    private NativeArray<int> _missingRelease;\n"
                "}\n",
                encoding="utf-8",
            )
            partial.write_text(
                "using Hecton8.Core.Memory;\n"
                "public sealed partial class SharedRuntime\n"
                "{\n"
                "    public void Allocate()\n"
                "    {\n"
                "        _tracked = H8Memory.Allocate<int>(4, SystemID.WorldStreaming, Allocator.Persistent);\n"
                "        _missingRelease = H8Memory.Allocate<int>(4, SystemID.WorldStreaming, Allocator.Persistent);\n"
                "    }\n"
                "    public void Dispose()\n"
                "    {\n"
                "        H8Memory.Release(ref _tracked, SystemID.WorldStreaming);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            classifications = {
                finding["classification"]: finding
                for finding in payload["declarationFindings"]
            }
            self.assertEqual(payload["h8MemoryTrackedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["persistentNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["allowedNativeCollectionDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeCollectionDeclarations"], 1)
            self.assertEqual(classifications["h8MemoryTrackedOwnerField"]["names"], ["_tracked"])
            self.assertEqual(classifications["persistentOwnerField"]["names"], ["_missingRelease"])

    def test_declaration_classifier_ignores_comments_and_string_literals(self) -> None:
        source = (
            "public sealed class Commented\n"
            "{\n"
            "    // private NativeArray<int> _commented;\n"
            "    private const string Text = \"public NativeArray<int> Fake;\";\n"
            "    /* public NativeList<int> Blocked; */\n"
            "    private NativeArray<int> _real;\n"
            "}\n"
        )

        findings = audit.scan_native_collection_declarations_in_source(
            source,
            "Assets/_Project/Scripts/Gameplay/Commented.cs",
        )

        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].names, ("_real",))

    def test_declaration_scan_reuses_presanitized_lines_without_semantic_change(self) -> None:
        source = (
            "using Unity.Burst;\n"
            "using Unity.Collections;\n"
            "public sealed class RuntimeOwner\n"
            "{\n"
            "    private NativeArray<int> _localState;\n"
            "    [BurstCompile]\n"
            "    private struct BuildJob : IJob\n"
            "    {\n"
            "        [ReadOnly] public NativeList<float> Input;\n"
            "        public void Execute() {}\n"
            "    }\n"
            "}\n"
        )
        relative_path = "Assets/_Project/Scripts/Gameplay/RuntimeOwner.cs"
        expected = audit.scan_native_collection_declarations_in_source(source, relative_path)
        reused = audit.scan_native_collection_declarations_in_source(
            source,
            relative_path,
            sanitized_lines=audit.sanitize_csharp_source(source).splitlines(),
            original_lines=source.splitlines(),
        )

        self.assertEqual(reused, expected)

    def test_combined_scan_matches_individual_nativearray_scans(self) -> None:
        with temporary_directory(prefix="h8_vault_combined_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "Gameplay" / "StatefulSystem.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "private NativeArray<int> _localState;\n"
                "public void Allocate() { _localState = new NativeArray<int>(4, Allocator.Persistent); }\n",
                encoding="utf-8",
            )

            constructor_findings = audit.scan_source_tree(source, root)
            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            combined_constructors, combined_declarations = audit.scan_source_tree_with_declarations(source, root)

            self.assertEqual(combined_constructors, constructor_findings)
            self.assertEqual(combined_declarations, declaration_findings)

    def test_no_regression_gate_fails_when_file_count_increases(self) -> None:
        payload = {
            "findings": [
                {
                    "path": "Assets/_Project/Scripts/Gameplay/BadSystem.cs",
                    "count": 2,
                    "lines": [1, 2],
                    "allowed": False,
                }
            ],
            "forbiddenDirectConstructors": 2,
        }
        baseline = {
            "schema": audit.BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 2,
            "forbiddenByFile": {
                "Assets/_Project/Scripts/Gameplay/BadSystem.cs": 1,
            },
        }

        errors = audit.detect_regressions(payload, baseline)

        self.assertEqual(len(errors), 1)
        self.assertIn("BadSystem.cs", errors[0])

    def test_no_regression_gate_fails_when_declaration_count_increases(self) -> None:
        payload = {
            "findings": [],
            "forbiddenDirectConstructors": 0,
            "declarationFindings": [
                {
                    "path": "Assets/_Project/Scripts/Gameplay/StatefulSystem.cs",
                    "count": 2,
                    "lines": [4, 5],
                    "allowed": False,
                }
            ],
            "forbiddenNativeArrayDeclarations": 2,
        }
        baseline = {
            "schema": audit.BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 0,
            "forbiddenByFile": {},
            "forbiddenNativeArrayDeclarations": 1,
            "forbiddenDeclarationsByFile": {
                "Assets/_Project/Scripts/Gameplay/StatefulSystem.cs": 1,
            },
        }

        errors = audit.detect_regressions(payload, baseline)

        self.assertGreaterEqual(len(errors), 1)
        self.assertTrue(any("StatefulSystem.cs" in error for error in errors))

    def test_baseline_round_trip_preserves_forbidden_counts(self) -> None:
        payload = {
            "sourceRoot": "Assets/_Project/Scripts",
            "pattern": audit.NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
            "declarationPattern": audit.NATIVE_ARRAY_DECLARATION_RE.pattern,
            "totalDirectConstructors": 3,
            "allowedDirectConstructors": 1,
            "forbiddenDirectConstructors": 2,
            "forbiddenFileCount": 1,
            "totalNativeArrayDeclarations": 2,
            "allowedNativeArrayDeclarations": 1,
            "forbiddenNativeArrayDeclarations": 1,
            "declarationFileCount": 1,
            "allowedPathSuffixes": list(audit.DEFAULT_ALLOWED_PATH_SUFFIXES),
            "declarationAllowedPathSuffixes": list(audit.DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES),
            "findings": [
                {
                    "path": "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
                    "count": 1,
                    "lines": [10],
                    "allowed": True,
                },
                {
                    "path": "Assets/_Project/Scripts/World/BadWorld.cs",
                    "count": 2,
                    "lines": [20, 21],
                    "allowed": False,
                },
            ],
            "declarationFindings": [
                {
                    "path": "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
                    "count": 1,
                    "lines": [30],
                    "allowed": True,
                },
                {
                    "path": "Assets/_Project/Scripts/World/BadWorld.cs",
                    "count": 1,
                    "lines": [40],
                    "allowed": False,
                },
            ],
        }

        with temporary_directory(prefix="h8_vault_baseline_") as temp_dir:
            path = Path(temp_dir) / "baseline.json"
            baseline = audit.build_baseline(payload)
            audit.write_json(path, baseline)

            loaded = json.loads(path.read_text(encoding="utf-8"))

            self.assertEqual(loaded["schema"], audit.BASELINE_SCHEMA)
            self.assertEqual(loaded["forbiddenByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 2)
            self.assertEqual(loaded["forbiddenDeclarationsByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 1)

    def test_baseline_round_trip_preserves_latest_created_fallback_counts(self) -> None:
        payload = {
            "sourceRoot": "Assets/_Project/Scripts",
            "pattern": audit.NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
            "declarationPattern": audit.NATIVE_ARRAY_DECLARATION_RE.pattern,
            "latestCreatedFallbackPattern": audit.LATEST_CREATED_FALLBACK_RE.pattern,
            "totalDirectConstructors": 0,
            "allowedDirectConstructors": 0,
            "forbiddenDirectConstructors": 0,
            "forbiddenFileCount": 0,
            "totalLatestCreatedFallbacks": 2,
            "allowedLatestCreatedFallbacks": 1,
            "forbiddenLatestCreatedFallbacks": 1,
            "runtimeForbiddenLatestCreatedFallbacks": 1,
            "forbiddenLatestCreatedFallbackFileCount": 1,
            "allowedPathSuffixes": list(audit.DEFAULT_ALLOWED_PATH_SUFFIXES),
            "declarationAllowedPathSuffixes": list(audit.DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES),
            "latestCreatedAllowedPathSuffixes": list(audit.DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES),
            "findings": [],
            "declarationFindings": [],
            "latestCreatedFallbackFindings": [
                {
                    "path": "Assets/_Project/Scripts/Gameplay/BadRuntime.cs",
                    "count": 1,
                    "lines": [8],
                    "allowed": False,
                    "forbiddenCount": 1,
                    "forbiddenLineExecutionSurfaceCounts": {"Runtime": 1},
                    "allowedCount": 0,
                },
                {
                    "path": "Assets/_Project/Scripts/Bootstrap/BootstrapProbe.cs",
                    "count": 1,
                    "lines": [4],
                    "allowed": True,
                    "forbiddenCount": 0,
                    "forbiddenLineExecutionSurfaceCounts": {},
                    "allowedCount": 1,
                },
            ],
        }

        with temporary_directory(prefix="h8_vault_latest_baseline_") as temp_dir:
            path = Path(temp_dir) / "baseline.json"
            baseline = audit.build_baseline(payload)
            audit.write_json(path, baseline)

            loaded = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual(loaded["schema"], audit.BASELINE_SCHEMA)
        self.assertEqual(
            loaded["forbiddenLatestCreatedFallbacksByFile"]["Assets/_Project/Scripts/Gameplay/BadRuntime.cs"],
            1,
        )
        self.assertEqual(
            loaded["runtimeForbiddenLatestCreatedFallbacksByFile"]["Assets/_Project/Scripts/Gameplay/BadRuntime.cs"],
            1,
        )


if __name__ == "__main__":
    unittest.main()
