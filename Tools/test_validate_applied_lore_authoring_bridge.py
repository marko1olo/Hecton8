import contextlib
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from ValidateAppliedLoreAuthoringBridge import (
    NAVIGATION_CLUSTER_GRAPH_HEADERS,
    NAVIGATION_CLUSTER_GRAPH_PATH,
    main,
    publication_text_issues,
    validate_authoring_bridge,
)
from AppliedLoreImporter import TARGET_LOCALES


def write_navigation_graph(
    root: Path,
    packet_id: str = "P_TEST_AUTHORING_BRIDGE",
    *,
    prereq_packet_ids: str = "",
    next_packet_ids: str = "",
) -> None:
    graph_path = root / "Docs" / "Lore" / "AppliedContent" / NAVIGATION_CLUSTER_GRAPH_PATH
    graph_path.parent.mkdir(parents=True, exist_ok=True)
    graph_path.write_text(
        ",".join(NAVIGATION_CLUSTER_GRAPH_HEADERS)
        + "\n"
        + ",".join(
            [
                packet_id,
                "site_wiki_navigation_clusters",
                "0-10m",
                "test_route",
                prereq_packet_ids,
                next_packet_ids,
                "test_evidence",
                "Test truth claim.",
                "Test player decision?",
                "0",
                "external_site",
            ]
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )


def write_contract_sources(root: Path, *, include_field_note: bool = True, exporter_reads_articles: bool = True) -> None:
    enum_members = [
        "Title = 0",
        "Scanner = 1",
        "Terminal = 2",
        "Audio = 3",
        "InGameWiki = 4",
        "ExternalSite = 5",
    ]
    if include_field_note:
        enum_members.append("FieldNote = 6")
    enum_body = ",\n        ".join(enum_members)
    enum_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8DataMonolithTypes.cs"
    enum_path.parent.mkdir(parents=True, exist_ok=True)
    enum_path.write_text(
        "namespace Hecton8.Data\n"
        "{\n"
        "    public enum H8AppliedLoreSurface : byte\n"
        "    {\n"
        f"        {enum_body}\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    exporter_path = root / "Tools" / "AppliedLorePageExporter.py"
    exporter_path.parent.mkdir(parents=True, exist_ok=True)
    if exporter_reads_articles:
        exporter_path.write_text(
            "def localized_surface_body(localized):\n"
            "    return localized.get('external_site_article_path') or localized.get('external_site_article')\n",
            encoding="utf-8",
        )
    else:
        exporter_path.write_text("def localized_surface_body(localized): return localized.get('external_site')\n", encoding="utf-8")

    write_navigation_graph(root)


def localized_row(article: str = "Long article body.") -> dict[str, str]:
    return {
        "title": "Test Packet",
        "scanner": "Scanner text.",
        "terminal": "Terminal text.",
        "audio": "Audio text.",
        "in_game_wiki": "Wiki text.",
        "external_site": "Site summary.",
        "field_note": "Field note.",
        "external_site_article": article,
    }


def legacy_surface_row() -> dict[str, str]:
    return {
        "title": "Legacy Surface Packet",
        "website_article": "Public article body routed through legacy website_article.",
        "wiki_article": "Wiki article body routed through legacy wiki_article.",
        "pda_codex": "PDA codex text routed through legacy pda_codex.",
        "scanner_entry": "Scanner entry routed through legacy scanner_entry.",
        "terminal_note": "Terminal note routed through legacy terminal_note.",
        "evidence_caption": "Authoring caption text.",
        "spoiler_policy": "Authoring spoiler policy.",
        "string_pool_key": "LORE_TEST_LEGACY_SURFACE",
    }


def write_publication_pages(root: Path, packet_id: str, surfaces: list[str]) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    for folder, surface_key in (("in_game_wiki", "in_game_wiki"), ("external_site", "external_site")):
        if surface_key not in surfaces:
            continue
        for locale in TARGET_LOCALES:
            page_path = base / folder / locale / f"{packet_id}.md"
            page_path.parent.mkdir(parents=True, exist_ok=True)
            page_path.write_text(
                "---\n"
                f"packet_id: {packet_id}\n"
                f"locale: {locale}\n"
                f"surface: {surface_key}\n"
                "source: AppliedContent packet JSON\n"
                "runtime_reads_markdown: false\n"
                "---\n"
                "Generated test page.\n",
                encoding="utf-8",
                newline="\n",
            )


def write_packet_bundle(
    root: Path,
    *,
    surfaces: list[str] | None = None,
    article: str = "Long article body.",
) -> None:
    resolved_surfaces = surfaces if surfaces is not None else ["scanner", "terminal", "audio_subtitle", "in_game_wiki", "external_site", "field_note", "image_brief"]
    packet = {
        "schema": "H8.APPLIED_CONTENT_PACKET.V0",
        "packet_id": "P_TEST_AUTHORING_BRIDGE",
        "article_id": "test.authoring_bridge",
        "release_set_id": "RS_TEST_AUTHORING_BRIDGE",
        "status": "production_facing_draft_pending_native_localization",
        "surfaces": resolved_surfaces,
        "unlock": {
            "primary": "unlock.test",
            "poi_tags": ["poi.test"],
            "biome_tags": ["biome.test"],
        },
        "localized": {locale: localized_row(article) for locale in TARGET_LOCALES},
    }
    packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_TEST_AUTHORING_BRIDGE.packets.json"
    packet_path.parent.mkdir(parents=True, exist_ok=True)
    packet_path.write_text(json.dumps({"packets": [packet]}, indent=2), encoding="utf-8")

    manifest_path = root / "Docs" / "Lore" / "AppliedContent" / "release_sets" / "RS_TEST_AUTHORING_BRIDGE_manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(
        json.dumps(
            {
                "release_set_id": "RS_TEST_AUTHORING_BRIDGE",
                "canonical_importer_ready": True,
                "packet_sources": [packet_path.relative_to(root).as_posix()],
                "packets": ["P_TEST_AUTHORING_BRIDGE"],
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    write_publication_pages(root, "P_TEST_AUTHORING_BRIDGE", resolved_surfaces)


class AppliedLoreAuthoringBridgeTests(unittest.TestCase):
    def test_publication_text_issues_allow_valid_latin_diacritics(self):
        self.assertEqual(publication_text_issues("Nao, N\u00e3o, Espa\u00f1a, S\u00e3o Paulo."), [])

    def test_publication_text_issues_reject_utf8_mojibake_sequences(self):
        issues = publication_text_issues("RU LOC HOLD: \u00d0\u00a2\u00d0\u00b5\u00d1\u0081\u00d1\u0082")

        self.assertTrue(any("mojibake" in issue for issue in issues))

    def test_accepts_matching_runtime_and_publication_contract(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            stats = validate_authoring_bridge(root)

            self.assertEqual(stats.issues, ())
            self.assertEqual(stats.packets, 1)
            self.assertEqual(stats.localized_rows, len(TARGET_LOCALES))
            self.assertEqual(stats.publication_article_fields, len(TARGET_LOCALES))

    def test_strict_localized_text_rejects_mojibake_runtime_field(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "packets"
                / "RS_TEST_AUTHORING_BRIDGE.packets.json"
            )
            data = json.loads(packet_path.read_text(encoding="utf-8"))
            data["packets"][0]["localized"]["ru_RU"]["title"] = "RU LOC HOLD: Ð¢ÐµÑÑ‚"
            packet_path.write_text(json.dumps(data, indent=2), encoding="utf-8")

            default_stats = validate_authoring_bridge(root)
            strict_stats = validate_authoring_bridge(root, strict_localized_text=True)

        self.assertEqual(default_stats.issues, ())
        self.assertTrue(
            any(
                "P_TEST_AUTHORING_BRIDGE/title: localized text" in issue
                and "ru_RU" in issue
                for issue in strict_stats.issues
            )
        )

    def test_rejects_runtime_enum_importer_surface_mismatch(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root, include_field_note=False)
            write_packet_bundle(root)

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("runtime/importer surface mismatch" in issue for issue in stats.issues))

    def test_rejects_publication_article_without_external_site_surface(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root, surfaces=["scanner", "terminal", "audio_subtitle", "in_game_wiki", "field_note"])

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("external_site_article present but external_site surface disabled" in issue for issue in stats.issues))

    def test_rejects_enabled_runtime_surface_without_localized_field(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_TEST_AUTHORING_BRIDGE.packets.json"
            data = json.loads(packet_path.read_text(encoding="utf-8"))
            for row in data["packets"][0]["localized"].values():
                del row["field_note"]
            packet_path.write_text(json.dumps(data, indent=2), encoding="utf-8")

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("field_note: surface enabled but localized field missing" in issue for issue in stats.issues))

    def test_rejects_navigation_cluster_missing_from_canonical_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            write_navigation_graph(root, "P_MISSING_CLUSTER")

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("P_MISSING_CLUSTER" in issue for issue in stats.issues))
            self.assertTrue(any("navigation cluster packet missing" in issue for issue in stats.issues))

    def test_accepts_navigation_cluster_staged_in_source_only_bundle(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            write_navigation_graph(root, "P_STAGED_CLUSTER")

            staged_packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_STAGED_CLUSTER.packets.json"
            staged_packet_path.write_text(
                json.dumps({"packets": [{"packet_id": "P_STAGED_CLUSTER"}]}, indent=2),
                encoding="utf-8",
            )
            staged_manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_STAGED_CLUSTER_manifest.json"
            )
            staged_manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_STAGED_CLUSTER",
                        "canonical_importer_ready": False,
                        "runtime_ready": False,
                        "native_localization_ready": False,
                        "data_monolith_ready": False,
                        "h8bin_ready": False,
                        "unity_placement_ready": False,
                        "generated_page_ready": False,
                        "publication_ready": False,
                        "packet_sources": [staged_packet_path.relative_to(root).as_posix()],
                        "packets": ["P_STAGED_CLUSTER"],
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )

            stats = validate_authoring_bridge(root)

            self.assertEqual(stats.issues, ())

    def test_accepts_navigation_cluster_staged_from_manifest_packet_list(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            write_navigation_graph(root, "P_STAGED_CLUSTER")

            staged_manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_STAGED_CLUSTER_manifest.json"
            )
            staged_manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_STAGED_CLUSTER",
                        "canonical_importer_ready": False,
                        "runtime_ready": False,
                        "native_localization_ready": False,
                        "data_monolith_ready": False,
                        "h8bin_ready": False,
                        "unity_placement_ready": False,
                        "generated_page_ready": False,
                        "publication_ready": False,
                        "packets": ["P_STAGED_CLUSTER"],
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )

            stats = validate_authoring_bridge(root)

            self.assertEqual(stats.issues, ())

    def test_rejects_navigation_cluster_active_ref_to_staged_packet(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            write_navigation_graph(root, next_packet_ids="P_STAGED_CLUSTER")

            staged_manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_STAGED_CLUSTER_manifest.json"
            )
            staged_manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_STAGED_CLUSTER",
                        "canonical_importer_ready": False,
                        "runtime_ready": False,
                        "native_localization_ready": False,
                        "data_monolith_ready": False,
                        "h8bin_ready": False,
                        "unity_placement_ready": False,
                        "generated_page_ready": False,
                        "publication_ready": False,
                        "packets": ["P_STAGED_CLUSTER"],
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("points at staged packet P_STAGED_CLUSTER" in issue for issue in stats.issues))

    def test_rejects_missing_publication_page_artifact(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            page_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "external_site"
                / "ru_RU"
                / "P_TEST_AUTHORING_BRIDGE.md"
            )

            real_exists = Path.exists

            def exists_with_missing_generated_page(path: Path) -> bool:
                if path == page_path:
                    return False
                return real_exists(path)

            with patch("pathlib.Path.exists", exists_with_missing_generated_page):
                stats = validate_authoring_bridge(root)

            self.assertTrue(any("P_TEST_AUTHORING_BRIDGE/external_site" in issue for issue in stats.issues))
            self.assertTrue(any("missing generated page" in issue for issue in stats.issues))

    def test_rejects_publication_page_with_stale_surface_marker(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            page_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "external_site"
                / "ru_RU"
                / "P_TEST_AUTHORING_BRIDGE.md"
            )
            text = page_path.read_text(encoding="utf-8")
            page_path.write_text(text.replace("surface: external_site", "surface: in_game_wiki"), encoding="utf-8")

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("P_TEST_AUTHORING_BRIDGE/external_site" in issue for issue in stats.issues))
            self.assertTrue(any("wrong or missing surface marker" in issue for issue in stats.issues))

    def test_rejects_publication_only_field_declared_as_runtime_surface(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root, surfaces=["scanner", "external_site", "external_site_article"])

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("publication-only field listed as runtime surface" in issue for issue in stats.issues))

    def test_rejects_mojibake_in_publication_article(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root, article="Broken \u00c3\u0090 text")

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("mojibake" in issue for issue in stats.issues))

    def test_rejects_partial_publication_article_locale_coverage(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_TEST_AUTHORING_BRIDGE.packets.json"
            data = json.loads(packet_path.read_text(encoding="utf-8"))
            del data["packets"][0]["localized"]["ru_RU"]["external_site_article"]
            packet_path.write_text(json.dumps(data, indent=2), encoding="utf-8")

            stats = validate_authoring_bridge(root)

            self.assertTrue(
                any("publication article body/path present for some locales" in issue for issue in stats.issues)
            )

    def test_rejects_exporter_that_no_longer_reads_article_fields(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root, exporter_reads_articles=False)
            write_packet_bundle(root)

            stats = validate_authoring_bridge(root)

            self.assertTrue(any("publication-only field is not read" in issue for issue in stats.issues))

    def test_all_packet_json_scope_catches_noncanonical_authoring_backlog(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            backlog_packet = {
                "packet_id": "P_BACKLOG_BAD_SURFACE",
                "release_set_id": "RS_BACKLOG",
                "article_id": "test.backlog",
                "surfaces": ["external_site_article"],
                "localized": {locale: localized_row() for locale in TARGET_LOCALES},
            }
            backlog_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_BACKLOG.packets.json"
            backlog_path.write_text(json.dumps({"packets": [backlog_packet]}, indent=2), encoding="utf-8")

            canonical_stats = validate_authoring_bridge(root)
            all_source_stats = validate_authoring_bridge(root, include_all_packet_json=True)

            self.assertEqual(canonical_stats.issues, ())
            self.assertTrue(any("P_BACKLOG_BAD_SURFACE" in issue for issue in all_source_stats.issues))

    def test_all_packet_json_scope_accepts_legacy_authoring_surface_aliases(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            backlog_packet = {
                "packet_id": "P_BACKLOG_LEGACY_SURFACES",
                "release_set_id": "RS_BACKLOG",
                "article_id": "test.backlog_legacy_surfaces",
                "surfaces": [
                    "website_article",
                    "wiki_article",
                    "pda_codex",
                    "scanner_entry",
                    "terminal_note",
                    "evidence_caption",
                    "spoiler_policy",
                    "string_pool_key",
                ],
                "localized": {locale: legacy_surface_row() for locale in TARGET_LOCALES},
            }
            backlog_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_BACKLOG.packets.json"
            backlog_path.write_text(json.dumps({"packets": [backlog_packet]}, indent=2), encoding="utf-8")

            all_source_stats = validate_authoring_bridge(root, include_all_packet_json=True)

            self.assertEqual(all_source_stats.issues, ())

    def test_cli_returns_zero_for_clean_canonical_bridge(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", str(root)])

            self.assertEqual(exit_code, 0)
            self.assertIn("AppliedLore authoring bridge OK", stdout.getvalue())
            self.assertEqual(stderr.getvalue(), "")

    def test_cli_returns_one_for_all_source_backlog_bridge_failure(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_contract_sources(root)
            write_packet_bundle(root)

            backlog_packet = {
                "packet_id": "P_BACKLOG_BAD_SURFACE",
                "release_set_id": "RS_BACKLOG",
                "article_id": "test.backlog",
                "surfaces": ["external_site_article"],
                "localized": {locale: localized_row() for locale in TARGET_LOCALES},
            }
            backlog_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_BACKLOG.packets.json"
            backlog_path.write_text(json.dumps({"packets": [backlog_packet]}, indent=2), encoding="utf-8")
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", str(root), "--include-all-packet-json"])

            self.assertEqual(exit_code, 1)
            self.assertEqual(stdout.getvalue(), "")
            self.assertIn("AppliedLore authoring bridge failed", stderr.getvalue())
            self.assertIn("P_BACKLOG_BAD_SURFACE", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
