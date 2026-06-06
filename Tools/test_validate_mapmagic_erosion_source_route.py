#!/usr/bin/env python3
"""Unit tests for the MapMagic erosion source-route static guard."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateMapMagicErosionSourceRoute as guard  # noqa: E402


class _Args:
    def __init__(self, root: Path) -> None:
        self.mapmagic_node = root / "MapMagicNode.cs"
        self.anomaly_node = root / "AnomalyNode.cs"
        self.splatmap_node = root / "SplatmapNode.cs"
        self.biome_postprocess_node = root / "BiomePostprocessNode.cs"
        self.erosion_harness = root / "ErosionHarness.cs"
        self.anomaly_engine = root / "AnomalyEngine.cs"
        self.erosion_job = root / "HydraulicErosionJob.cs"
        self.graph_integrator = root / "GraphIntegrator.cs"
        self.source_root = root
        self.mapmagic_plugin_root = root


def write_clean_sources(root: Path) -> _Args:
    args = _Args(root)
    args.mapmagic_node.write_text(
        """
class Node {
    void Generate() {
        RegisterTempJobBuffers(
            a,
            b,
            c,
            d,
            e,
            out int heightARegistrationId,
            out int heightBRegistrationId,
            out int sedimentRegistrationId,
            out int siltRegistrationId,
            out int wearRegistrationId);
        handle = HydraulicErosionScheduler.ScheduleFourPhaseSliced(ref job, 1, 1, handle);
    }
    static void RegisterTempJobBuffers(
        object a,
        object b,
        object c,
        object d,
        object e,
        out int heightARegistrationId,
        out int heightBRegistrationId,
        out int sedimentRegistrationId,
        out int siltRegistrationId,
        out int wearRegistrationId) {
        heightARegistrationId = 1;
        heightBRegistrationId = 2;
        sedimentRegistrationId = 3;
        siltRegistrationId = 4;
        wearRegistrationId = 5;
    }
    static void DisposeTracked<T>(ref T array, ref int registrationId) {
        NativeMemorySentinel.Unregister(registrationId);
    }
}
""",
        encoding="utf-8",
    )
    args.erosion_harness.write_text(
        """
class Harness {
    void Run() {
        // QUEUED_DELTA_APPLY_QUARANTINED: editor-only proof route.
        handle = HydraulicErosionScheduler.ScheduleFourPhaseSlicedWithDeltaApply(ref job, 1, 1, q, b, 1, handle);
        DisposeTracked(ref pixels, ref pixelsRegistrationId);
    }
    static void DisposeTracked<T>(ref T array, ref int registrationId) {
        NativeMemorySentinel.Unregister(registrationId);
    }
}
""",
        encoding="utf-8",
    )
    args.anomaly_node.write_text(
        """
class AnomalyNode {
    void Generate() {
        RegisterTempJobBuffers(
            heightmap,
            basinMask,
            basinLipMask,
            candidateMask,
            basinRecords,
            featureRecords,
            fissureMask,
            floodHeap,
            visitedStamp,
            acceptedCells,
            out int heightmapRegistrationId,
            out int basinMaskRegistrationId,
            out int basinLipMaskRegistrationId,
            out int candidateMaskRegistrationId,
            out int basinRecordsRegistrationId,
            out int featureRecordsRegistrationId,
            out int fissureMaskRegistrationId,
            out int floodHeapRegistrationId,
            out int visitedStampRegistrationId,
            out int acceptedCellsRegistrationId);
        DisposeTracked(ref heightmap, ref heightmapRegistrationId);
    }
    static void RegisterTempJobBuffers(
        object heightmap,
        object basinMask,
        object basinLipMask,
        object candidateMask,
        object basinRecords,
        object featureRecords,
        object fissureMask,
        object floodHeap,
        object visitedStamp,
        object acceptedCells,
        out int heightmapRegistrationId,
        out int basinMaskRegistrationId,
        out int basinLipMaskRegistrationId,
        out int candidateMaskRegistrationId,
        out int basinRecordsRegistrationId,
        out int featureRecordsRegistrationId,
        out int fissureMaskRegistrationId,
        out int floodHeapRegistrationId,
        out int visitedStampRegistrationId,
        out int acceptedCellsRegistrationId) {
        heightmapRegistrationId = 1;
        basinMaskRegistrationId = 2;
        basinLipMaskRegistrationId = 3;
        candidateMaskRegistrationId = 4;
        basinRecordsRegistrationId = 5;
        featureRecordsRegistrationId = 6;
        fissureMaskRegistrationId = 7;
        floodHeapRegistrationId = 8;
        visitedStampRegistrationId = 9;
        acceptedCellsRegistrationId = 10;
    }
    static void DisposeTracked<T>(ref T array, ref int registrationId) {
        NativeMemorySentinel.Unregister(registrationId);
    }
}
""",
        encoding="utf-8",
    )
    args.splatmap_node.write_text(
        """
class SplatmapNode {
    void Generate() {
        RegisterTempJobBuffers(
            heights,
            sediment,
            weights,
            slopeWeights,
            out int heightsRegistrationId,
            out int sedimentRegistrationId,
            out int weightsRegistrationId,
            out int slopeWeightsRegistrationId);
        DisposeTracked(ref heights, ref heightsRegistrationId);
    }
    static void RegisterTempJobBuffers(
        object heights,
        object sediment,
        object weights,
        object slopeWeights,
        out int heightsRegistrationId,
        out int sedimentRegistrationId,
        out int weightsRegistrationId,
        out int slopeWeightsRegistrationId) {
        heightsRegistrationId = 1;
        sedimentRegistrationId = 2;
        weightsRegistrationId = 3;
        slopeWeightsRegistrationId = 4;
    }
    static void DisposeTracked<T>(ref T array, ref int registrationId) {
        NativeMemorySentinel.Unregister(registrationId);
    }
}
""",
        encoding="utf-8",
    )
    args.biome_postprocess_node.write_text(
        """
class BiomePostprocessNode {
    void Generate() {
        RegisterTempJobBuffers(bufferA, bufferB, out int bufferARegistrationId, out int bufferBRegistrationId);
        DisposeTracked(ref bufferA, ref bufferARegistrationId);
    }
    static void RegisterTempJobBuffers(
        object bufferA,
        object bufferB,
        out int bufferARegistrationId,
        out int bufferBRegistrationId) {
        bufferARegistrationId = 1;
        bufferBRegistrationId = 2;
    }
    static void DisposeTracked<T>(ref T array, ref int registrationId) {
        NativeMemorySentinel.Unregister(registrationId);
    }
}
""",
        encoding="utf-8",
    )
    args.anomaly_engine.write_text(
        """
class Engine {
    void ScheduleClosedBasinDetection() {
        if (ShouldUseEditorDirectExecution(dependency)) {}
    }
    void ScheduleRidgeFeatureDetection() {
        if (ShouldUseEditorDirectExecution(dependency)) {}
    }
    private static bool ShouldUseEditorDirectExecution(JobHandle dependency) {
        if (!dependency.IsCompleted)
            return false;
        if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref _editorMainThreadId))
            return false;
        return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;
    }
}
""",
        encoding="utf-8",
    )
    args.erosion_job.write_text(
        """
struct HydraulicErosionJob {
    // SAFETY_JUSTIFICATION_PARAGRAPH_1:
    // TryEnqueueHeightDeltaBounded validates this optional budget before use.
    // SAFETY_JUSTIFICATION_PARAGRAPH_2:
    // SAFETY_JUSTIFICATION_PARAGRAPH_3:
    [NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
    public NativeArray<int> HeightDeltaBudget;
    static bool TryEnqueueHeightDeltaBounded(NativeArray<int> writerBudget) {
        if (!writerBudget.IsCreated || writerBudget.Length < 2)
            return false;
        return true;
    }
}
""",
        encoding="utf-8",
    )
    args.graph_integrator.write_text(
        """
class Integrator {
    void Run() {
        ResolveHeightSource(graph, heightOutput, tectonicNode, erosionNode, splatNode, anomalyNode);
        LinkGraph(graph, erosionNode.erodedHeightOut, heightOutput);
        LinkGraph(graph, erosionNode.erodedHeightOut, splatNode.heightIn);
        LinkGraph(graph, erosionNode.sedimentMaskOut, splatNode.sedimentIn);
        LinkGraph(graph, erosionNode.erodedHeightOut, anomalyNode.heightIn);
        LinkGraph(graph, anomalyNode.brineMaskOut, layer);
        erosionNode.enabled = true;
        anomalyNode.enabled = true;
    }
    private static void LinkGraph(Graph graph, IOutlet<object> outlet, IInlet<object> inlet) {
        graph.Link(outlet, inlet);
    }
    bool IsUsableSource(object generator) {
        return generator != tectonicNode && generator != erosionNode && generator != splatNode && generator != anomalyNode;
    }
}
""",
        encoding="utf-8",
    )
    return args


class MapMagicErosionSourceRouteGuardTests(unittest.TestCase):
    def test_current_source_route_passes(self) -> None:
        issues = guard.collect_issues(guard.build_parser().parse_args([]))
        self.assertEqual([], issues)

    def test_clean_synthetic_sources_pass(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertEqual([], issues)

    def test_queued_apply_in_mapmagic_node_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            args.mapmagic_node.write_text(
                "class Node { void Run() { HydraulicErosionScheduler.ScheduleFourPhaseSlicedWithDeltaApply(); } }",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        joined = "\n".join(issues)
        self.assertIn("queued delta apply state", joined)

    def test_queued_delta_flags_in_mapmagic_node_fail(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.mapmagic_node.read_text(encoding="utf-8")
            args.mapmagic_node.write_text(text + "\nclass Flags { void Run() { job.QueueHeightDeltas = 1; } }", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("queued delta flags enabled", "\n".join(issues))

    def test_harness_pointer_unregister_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.erosion_harness.read_text(encoding="utf-8")
            args.erosion_harness.write_text(text + "\nNativeMemorySentinel.UnregisterNativeArray(pixels);", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("pointer-based NativeArray unregister", "\n".join(issues))

    def test_mapmagic_node_old_dispose_call_shape_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.mapmagic_node.read_text(encoding="utf-8")
            args.mapmagic_node.write_text(text + "\nclass LeakBack { void Run() { DisposeTracked(ref heightA); } }", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("old one-argument DisposeTracked call", "\n".join(issues))

    def test_anomaly_node_pointer_unregister_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.anomaly_node.read_text(encoding="utf-8")
            args.anomaly_node.write_text(text + "\nNativeMemorySentinel.UnregisterNativeArray(heightmap);", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("pointer-based NativeArray unregister", "\n".join(issues))

    def test_anomaly_node_old_dispose_call_shape_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.anomaly_node.read_text(encoding="utf-8")
            args.anomaly_node.write_text(text + "\nclass LeakBack { void Run() { DisposeTracked(ref heightmap); } }", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("old one-argument DisposeTracked call", "\n".join(issues))

    def test_splatmap_node_pointer_unregister_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.splatmap_node.read_text(encoding="utf-8")
            args.splatmap_node.write_text(text + "\nNativeMemorySentinel.UnregisterNativeArray(heights);", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("pointer-based NativeArray unregister", "\n".join(issues))

    def test_biome_postprocess_node_old_dispose_call_shape_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.biome_postprocess_node.read_text(encoding="utf-8")
            args.biome_postprocess_node.write_text(text + "\nclass LeakBack { void Run() { DisposeTracked(ref bufferA); } }", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("old one-argument DisposeTracked call", "\n".join(issues))

    def test_anomaly_editor_api_before_thread_guard_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            args.anomaly_engine.write_text(
                """
class Engine {
    void ScheduleClosedBasinDetection() { if (ShouldUseEditorDirectExecution(dependency)) {} }
    void ScheduleRidgeFeatureDetection() { if (ShouldUseEditorDirectExecution(dependency)) {} }
    private static bool ShouldUseEditorDirectExecution(JobHandle dependency) {
        if (!dependency.IsCompleted)
            return false;
        bool busy = UnityEditor.EditorApplication.isUpdating;
        if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref _editorMainThreadId))
            return false;
        return busy || UnityEditor.EditorApplication.isCompiling;
    }
}
""",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("thread id guard must execute before EditorApplication.isUpdating", "\n".join(issues))

    def test_anomaly_direct_execution_without_completed_dependency_gate_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            args.anomaly_engine.write_text(
                """
class Engine {
    void ScheduleClosedBasinDetection() { if (ShouldUseEditorDirectExecution(dependency)) {} }
    void ScheduleRidgeFeatureDetection() { if (ShouldUseEditorDirectExecution(dependency)) {} }
    private static bool ShouldUseEditorDirectExecution(JobHandle dependency) {
        if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref _editorMainThreadId))
            return false;
        return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;
    }
}
""",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("missing dependency.IsCompleted direct-execution gate", "\n".join(issues))

    def test_graph_diagnostic_bypass_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            args.graph_integrator.write_text(
                "class Integrator { void Run() { graph.Link(null, anomalyNode.heightIn); anomalyNode.enabled = false; } }",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        joined = "\n".join(issues)
        self.assertIn("diagnostic disabled erosion/anomaly graph bypass", joined)

    def test_graph_without_enabled_recovery_defaults_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.graph_integrator.read_text(encoding="utf-8")
            args.graph_integrator.write_text(
                text.replace("erosionNode.enabled = true;", "").replace("anomalyNode.enabled = true;", ""),
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        joined = "\n".join(issues)
        self.assertIn("erosion node enabled", joined)
        self.assertIn("anomaly node enabled", joined)

    def test_direct_graph_link_outside_wrapper_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.graph_integrator.read_text(encoding="utf-8")
            args.graph_integrator.write_text(text + "\nclass BadLink { void Run() { graph.Link(a, b); } }", encoding="utf-8")
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("direct graph.Link call outside LinkGraph wrapper", "\n".join(issues))

    def test_graph_height_source_without_anomaly_exclusion_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            args = write_clean_sources(Path(temp_dir))
            text = args.graph_integrator.read_text(encoding="utf-8")
            args.graph_integrator.write_text(
                text.replace(", anomalyNode", "").replace(" && generator != anomalyNode", ""),
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        joined = "\n".join(issues)
        self.assertIn("anomaly node excluded from upstream height source resolution", joined)
        self.assertIn("anomaly node rejected as upstream height source", joined)

    def test_hidden_queued_delta_caller_outside_harness_fails(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            root = Path(temp_dir)
            args = write_clean_sources(root)
            (root / "HiddenCaller.cs").write_text(
                "class HiddenCaller { void Run() { HydraulicErosionScheduler.ScheduleFourPhaseSlicedWithDeltaApply(); } }",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("forbidden ScheduleFourPhaseSlicedWithDeltaApply caller outside scheduler/harness quarantine", "\n".join(issues))

    def test_mapmagic_plugin_cleanup_scan_fails_on_hidden_pointer_unregister(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_route_") as temp_dir:
            root = Path(temp_dir)
            args = write_clean_sources(root)
            (root / "HiddenPointerCleanup.cs").write_text(
                "class HiddenPointerCleanup { void Run() { NativeMemorySentinel.UnregisterNativeArray(buffer); } }",
                encoding="utf-8",
            )
            issues = guard.collect_issues(args)  # type: ignore[arg-type]
        self.assertIn("forbidden NativeMemorySentinel.UnregisterNativeArray in MapMagic plugin cleanup", "\n".join(issues))


if __name__ == "__main__":
    unittest.main()
