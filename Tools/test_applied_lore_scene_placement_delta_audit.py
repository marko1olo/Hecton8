#!/usr/bin/env python3
from __future__ import annotations

import contextlib
import csv
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

import AppliedLoreRuntimeAudit as runtime_audit
from AppliedLoreScenePlacementDeltaAudit import (
    compute_scene_placement_delta,
    main,
    render_delta,
)


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def placement_row(
    packet_id: str,
    *,
    packet_hash: int = 1234,
    scene_path: str = "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
    object_name: str = "AL_TERM_P001",
    source_prefab: str = "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab",
    component: str = "MessageTerminal",
    discovery_id: str = "",
) -> dict[str, str]:
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_decimal": str(packet_hash),
        "scene_path": scene_path,
        "placement_root": "__APPLIED_LORE_SCENE_PLACEMENT",
        "object_name": object_name,
        "source_prefab": source_prefab,
        "authoring_component": component,
        "serialized_field": "appliedLorePacketHash",
        "discovery_id": discovery_id,
        "display_name": packet_id,
        "local_position": "0|0|0",
        "local_euler": "0|0|0",
        "local_scale": "1|1|1",
        "depth_band": "shallows",
        "zone_tag": "test",
        "placement_note": "test fixture",
    }


def write_plan(root: Path, rows: list[dict[str, str]]) -> None:
    path = root / "Docs/Lore/AppliedContent/binding_maps/RS001_RS010_scene_placement_plan.csv"
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=runtime_audit.SCENE_PLACEMENT_PLAN_HEADERS)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


class AppliedLoreScenePlacementDeltaAuditTests(unittest.TestCase):
    def test_clean_scene_and_prefab_coverage_has_no_issues(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            rows = [
                placement_row("P001"),
                placement_row(
                    "P002",
                    packet_hash=5678,
                    object_name="AL_DISC_P002",
                    source_prefab="Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab",
                    component="NarrativeDiscovery",
                    discovery_id="applied_lore_p002",
                ),
            ]
            write_plan(root, rows)
            write_text(
                root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
                "AL_TERM_P001\nAL_DISC_P002\ndiscoveryId: applied_lore_p002\n",
            )
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 1234\n")
            write_text(root / "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab", "appliedLorePacketHash: 5678\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.total_rows, 2)
        self.assertEqual(delta.covered_rows, 2)
        self.assertEqual(delta.issues, ())
        self.assertIn("missing=0", render_delta(delta))

    def test_reports_missing_scene_object(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "some other object\n")
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.covered_rows, 0)
        self.assertEqual(delta.issues[0].reason, "object_missing_in_scene")

    def test_reports_duplicate_scene_object_name(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(
                root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
                "m_Name: AL_TERM_P001\nm_Name: AL_TERM_P001\n",
            )
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.issues[0].reason, "duplicate_object_name_in_scene")

    def test_reports_duplicate_object_name_in_plan_before_scene_scan(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(
                root,
                [
                    placement_row("P001", object_name="AL_DUPLICATE"),
                    placement_row("P002", object_name="AL_DUPLICATE", packet_hash=5678),
                ],
            )
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "m_Name: AL_DUPLICATE\n")
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual([issue.reason for issue in delta.issues], ["duplicate_object_name_in_plan", "duplicate_object_name_in_plan"])

    def test_reports_hash_missing_from_scene_and_prefab(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "AL_TERM_P001\n")
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 9999\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.issues[0].reason, "binding_hash_missing_in_scene_and_prefab")

    def test_reports_missing_prefab_when_scene_hash_is_absent(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "AL_TERM_P001\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.issues[0].reason, "binding_hash_missing_and_prefab_missing_or_unreadable")

    def test_reports_missing_discovery_id_for_narrative_discovery(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            row = placement_row(
                "P002",
                object_name="AL_DISC_P002",
                source_prefab="Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab",
                component="NarrativeDiscovery",
                discovery_id="applied_lore_p002",
            )
            write_plan(root, [row])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "AL_DISC_P002\n")
            write_text(root / "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.issues[0].reason, "discovery_id_missing_in_scene")

    def test_reports_duplicate_discovery_id_for_narrative_discovery(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            row = placement_row(
                "P002",
                object_name="AL_DISC_P002",
                source_prefab="Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab",
                component="NarrativeDiscovery",
                discovery_id="applied_lore_p002",
            )
            write_plan(root, [row])
            write_text(
                root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
                "m_Name: AL_DISC_P002\ndiscoveryId: applied_lore_p002\ndiscoveryId: applied_lore_p002\n",
            )
            write_text(root / "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P002.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(delta.issues[0].reason, "duplicate_discovery_id_in_scene")

    def test_reports_duplicate_discovery_id_in_plan_before_scene_scan(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            rows = [
                placement_row(
                    "P001",
                    object_name="AL_DISC_P001",
                    component="NarrativeDiscovery",
                    discovery_id="applied_lore_duplicate",
                ),
                placement_row(
                    "P002",
                    packet_hash=5678,
                    object_name="AL_DISC_P002",
                    component="NarrativeDiscovery",
                    discovery_id="applied_lore_duplicate",
                ),
            ]
            write_plan(root, rows)
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "m_Name: AL_DISC_P001\nm_Name: AL_DISC_P002\n")
            write_text(root / "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab", "appliedLorePacketHash: 1234\n")

            delta = compute_scene_placement_delta(root)

        self.assertEqual(
            [issue.reason for issue in delta.issues],
            ["duplicate_discovery_id_in_plan", "duplicate_discovery_id_in_plan"],
        )

    def test_cli_returns_one_with_grouped_reason_for_missing_rows(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "AL_TERM_P001\n")
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp, "--max-rows", "1"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("missing=1", stderr.getvalue())
        self.assertIn("reason=binding_hash_missing_and_prefab_missing_or_unreadable count=1", stderr.getvalue())
        self.assertIn("scene_missing_work scene=Assets/_Project/Scenes/02_HECTON_WORLD.unity count=1", stderr.getvalue())
        self.assertIn(
            "prefab_source prefab=Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_P001.prefab count=1",
            stderr.getvalue(),
        )
        self.assertIn("depth_band depth=shallows count=1", stderr.getvalue())
        self.assertIn("zone_tag zone=test count=1", stderr.getvalue())
        self.assertIn("local_position=0|0|0", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())

    def test_json_payload_includes_spatial_workload_fields(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            row = placement_row(
                "P001",
                object_name="AL_DISC_P001",
                source_prefab="Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P001.prefab",
                component="NarrativeDiscovery",
                discovery_id="applied_lore_p001",
            )
            row["depth_band"] = "mid_depth"
            row["zone_tag"] = "wreck_field"
            row["local_position"] = "12|34|56"
            write_plan(root, [row])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "some other object\n")
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp, "--max-rows", "1", "--json"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertEqual(payload["depth_bands"], [{"depth": "mid_depth", "count": 1}])
        self.assertEqual(payload["zone_tags"], [{"zone": "wreck_field", "count": 1}])
        issue = payload["issues"][0]
        self.assertEqual(issue["depth_band"], "mid_depth")
        self.assertEqual(issue["zone_tag"], "wreck_field")
        self.assertEqual(issue["local_position"], "12|34|56")

    def test_cli_json_writes_stdout_and_keeps_failure_exit_code(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_plan(root, [placement_row("P001")])
            write_text(root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity", "some other object\n")
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp, "--max-rows", "1", "--json"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertEqual(payload["planned"], 1)
        self.assertEqual(payload["missing"], 1)
        self.assertEqual(payload["truncated_issues"], 0)
        self.assertEqual(payload["issues"][0]["reason"], "object_missing_in_scene")
        self.assertEqual(payload["depth_bands"], [{"depth": "shallows", "count": 1}])

    def test_cli_reports_missing_plan_without_traceback(self) -> None:
        with temporary_directory() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("Missing AppliedLore scene placement plan", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
