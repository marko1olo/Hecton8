import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import DataVaultSovereigntyAudit as audit  # noqa: E402


class DataVaultSovereigntyAuditTests(unittest.TestCase):
    def test_scan_separates_h8memory_allowed_constructors_from_system_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_audit_") as temp_dir:
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

    def test_constructor_scan_ignores_comments_strings_and_splits_surface(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_constructor_surface_") as temp_dir:
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
        with tempfile.TemporaryDirectory(prefix="h8_vault_allocator_surface_") as temp_dir:
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
        with tempfile.TemporaryDirectory(prefix="h8_vault_editor_transient_surface_") as temp_dir:
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
        with tempfile.TemporaryDirectory(prefix="h8_vault_multiline_allocator_") as temp_dir:
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

    def test_editor_sentinel_tracked_constructor_wrapper_is_not_raw_ownership_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_sentinel_wrapper_") as temp_dir:
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
            self.assertEqual(payload["editorOfflineTransientScratchDirectConstructors"], 1)

    def test_scan_tracks_nativearray_field_declaration_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_declaration_audit_") as temp_dir:
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

    def test_classifies_job_input_native_collections_separately_from_owner_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_job_declaration_audit_") as temp_dir:
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

    def test_classifies_editor_bake_session_and_preview_cache_fields(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_editor_declaration_audit_") as temp_dir:
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
        with tempfile.TemporaryDirectory(prefix="h8_vault_tracked_editor_preview_") as temp_dir:
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

    def test_combined_scan_matches_individual_nativearray_scans(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_combined_audit_") as temp_dir:
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

        with tempfile.TemporaryDirectory(prefix="h8_vault_baseline_") as temp_dir:
            path = Path(temp_dir) / "baseline.json"
            baseline = audit.build_baseline(payload)
            audit.write_json(path, baseline)

            loaded = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual(loaded["schema"], audit.BASELINE_SCHEMA)
        self.assertEqual(loaded["forbiddenByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 2)
        self.assertEqual(loaded["forbiddenDeclarationsByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 1)


if __name__ == "__main__":
    unittest.main()
