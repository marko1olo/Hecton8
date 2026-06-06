#!/usr/bin/env python3
"""Tests for JobCompletionAudit."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import JobCompletionAudit as audit


class JobCompletionAuditTests(unittest.TestCase):
    def test_classifies_frame_teardown_editor_and_polled_dispatcher(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_job_completion_") as tmp:
            root = Path(tmp) / "Assets" / "_Project" / "Scripts"
            runtime = root / "World" / "RuntimeThing.cs"
            runtime.parent.mkdir(parents=True)
            runtime.write_text(
                """
using Unity.Jobs;

public sealed class RuntimeThing
{
    private JobHandle _handle;

    private void Update()
    {
        _handle.Complete();
    }

    private void Tick(float dt)
    {
        DispatcherJobSwap.TryComplete(ref _handle, forceComplete: false);
    }

    private void OnDisable()
    {
        DispatcherJobFence.TryComplete(ref _handle, forceComplete: true);
    }

    private void BakeNow()
    {
        new SampleJob().Schedule().Complete();
    }
}
""",
                encoding="utf-8",
            )
            editor = root / "Editor" / "BakeWindow.cs"
            editor.parent.mkdir(parents=True)
            editor.write_text(
                """
using Unity.Jobs;

public sealed class BakeWindow
{
    private void Bake()
    {
        JobHandle handle = default;
        handle.Complete();
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["byClassification"]["FramePathRawComplete"], 1)
        self.assertEqual(payload["byClassification"]["FramePathPolledDispatcherComplete"], 1)
        self.assertEqual(payload["byClassification"]["TeardownForcedDispatcherComplete"], 1)
        self.assertEqual(payload["byClassification"]["RuntimeScheduleCompleteChain"], 1)
        self.assertEqual(payload["byClassification"]["EditorOrTestComplete"], 1)
        self.assertEqual(payload["framePathBlockerCount"], 1)
        self.assertEqual(payload["rawRuntimeBlockerCount"], 2)

    def test_hard_failures_track_frame_and_raw_runtime_separately(self) -> None:
        payload = {
            "framePathBlockerCount": 1,
            "rawRuntimeBlockerCount": 2,
        }
        args = type(
            "Args",
            (),
            {
                "fail_on_frame_path": True,
                "fail_on_raw_runtime_complete": False,
                "fail_on_plugin_sync_complete": False,
            },
        )()

        self.assertEqual(audit.hard_failures(payload, args), ["frame-path raw/forced JobHandle completion detected"])

        args.fail_on_raw_runtime_complete = True
        self.assertEqual(
            audit.hard_failures(payload, args),
            [
                "frame-path raw/forced JobHandle completion detected",
                "raw runtime JobHandle.Complete detected outside editor/test/teardown",
            ],
        )

    def test_dispatcher_fence_internal_complete_is_visible_but_not_raw_runtime_blocker(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_job_fence_completion_") as tmp:
            root = Path(tmp) / "Assets" / "_Project" / "Scripts"
            fence = root / "Core" / "DispatcherJobFence.cs"
            fence.parent.mkdir(parents=True)
            fence.write_text(
                """
using Unity.Jobs;

public static class DispatcherJobFence
{
    public static bool TryComplete(ref JobHandle handle, bool forceComplete)
    {
        handle.Complete();
        return true;
    }

    public static bool TryFinalizeCompleted(ref JobHandle handle)
    {
        handle.Complete();
        return true;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["byClassification"]["DispatcherFenceInternalRawComplete"], 2)
        self.assertEqual(payload["rawRuntimeBlockerCount"], 0)

    def test_dispatcher_fence_embedded_in_core_contracts_is_not_raw_runtime_blocker(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_job_fence_contracts_completion_") as tmp:
            root = Path(tmp) / "Assets" / "_Project" / "Scripts"
            fence = root / "Core" / "Contracts" / "CoreLowLevelUtilities.cs"
            fence.parent.mkdir(parents=True)
            fence.write_text(
                """
using Unity.Jobs;

public static class DispatcherJobFence
{
    public static bool TryComplete(ref JobHandle handle, bool forceComplete)
    {
        handle.Complete();
        return true;
    }

    public static bool TryFinalizeCompleted(ref JobHandle handle)
    {
        handle.Complete();
        return true;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["byClassification"]["DispatcherFenceInternalRawComplete"], 2)
        self.assertEqual(payload["rawRuntimeBlockerCount"], 0)

    def test_missing_file_during_scan_is_skipped(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_job_missing_file_") as tmp:
            missing = Path(tmp) / "Gone.cs"

            findings = audit.scan_file(missing)

        self.assertEqual([], findings)

    def test_mapmagic_plugin_sync_complete_is_visible_but_not_raw_runtime_blocker(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mapmagic_completion_") as tmp:
            root = Path(tmp) / "Assets" / "_Project" / "Scripts"
            plugin = root / "Plugins" / "MapMagic" / "HectonTerrainSplatmapMapMagicNode.cs"
            plugin.parent.mkdir(parents=True)
            plugin.write_text(
                """
using Unity.Jobs;

public sealed class HectonTerrainSplatmapMapMagicNode
{
    public void Generate()
    {
        JobHandle handle = default;
        handle.Complete();
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["byClassification"]["PluginSynchronousGeneratorRawComplete"], 1)
        self.assertEqual(payload["pluginSyncCompleteCount"], 1)
        self.assertEqual(payload["rawRuntimeBlockerCount"], 0)


if __name__ == "__main__":
    unittest.main()
