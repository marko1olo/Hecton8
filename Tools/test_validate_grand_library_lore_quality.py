import sys
import tempfile
import unittest
import json
from pathlib import Path
from unittest.mock import patch
from io import StringIO

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import TARGET_LOCALES
from ValidateGrandLibraryLoreQuality import run_validation


def status_comment(locale: str) -> str:
    if locale == "en_US":
        return "<!-- localization_status: source_authority_en_US -->"
    return "<!-- localization_status: source_draft_pending_native_review -->"


def article_text(locale: str, body: str = "Concrete pressure hull notes.") -> str:
    return "\n".join(
        [
            status_comment(locale),
            "# Pressure Hull Note",
            "",
            body,
            "",
        ]
    )


def write_article_set(root: Path, base_name: str, omitted: set[str] | None = None) -> None:
    omitted = omitted or set()
    library = root / "Docs" / "Lore" / "Grand_Library"
    library.mkdir(parents=True, exist_ok=True)
    for locale in TARGET_LOCALES:
        if locale in omitted:
            continue
        body = f"Concrete pressure hull notes for {locale}."
        (library / f"{base_name}_{locale}.md").write_text(article_text(locale, body), encoding="utf-8")


def packet_row(locale: str, text: str = "Concrete packet text.", extra: dict[str, str] | None = None) -> dict[str, str]:
    status = "source_authority" if locale == "en_US" else "source_draft_pending_native_review"
    row = {
        "localization_status": status,
        "title": f"Packet {locale}",
        "scanner": text,
        "terminal": text,
        "audio": text,
        "in_game_wiki": text,
        "external_site": text,
        "field_note": text,
    }
    if extra:
        row.update(extra)
    return row


def write_packet_bundle(
    root: Path,
    name: str,
    broken_locale: str | None = None,
    *,
    optional_article: bool = False,
    empty_article_locale: str | None = None,
    stale_article_path: bool = False,
) -> None:
    packets = root / "Docs" / "Lore" / "AppliedContent" / "packets"
    packets.mkdir(parents=True, exist_ok=True)
    localized = {}
    for locale in TARGET_LOCALES:
        row_text = "Broken marker: \ufffd" if locale == broken_locale else "Concrete packet text."
        extra = {}
        if optional_article:
            extra["external_site_article"] = "" if locale == empty_article_locale else f"Concrete longform article for {locale}."
        if stale_article_path:
            extra["external_site_article"] = f"Concrete longform article for {locale}."
            extra["external_site_article_path"] = f"articles/{locale}/missing-longform.md"
        localized[locale] = packet_row(locale, row_text, extra)

    bundle = {
        "schema": "H8.APPLIED_LORE_PACKET_BUNDLE.V0",
        "release_set_id": name,
        "runtime_contract": {
            "authoring_only": True,
            "runtime_ready": False,
            "native_localization_ready": False,
            "data_monolith_ready": False,
            "h8bin_ready": False,
            "unity_placement_ready": False,
            "generated_page_ready": False,
            "publication_ready": False,
        },
        "packets": [
            {
                "packet_id": "P900_TEST_PACKET",
                "localized": localized,
            }
        ],
    }
    (packets / f"{name}.packets.json").write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding="utf-8")


def write_release_manifest(
    root: Path,
    name: str,
    ready: bool = False,
    status: str = "source_candidate",
    generated_page_ready: bool = False,
    publication_ready: bool = False,
) -> None:
    release_sets = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    release_sets.mkdir(parents=True, exist_ok=True)
    packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / f"{name}.packets.json"
    packet_path.parent.mkdir(parents=True, exist_ok=True)
    if not packet_path.exists():
        write_packet_bundle(root, name)
    article_sources = [
        f"Docs/Lore/Grand_Library/21_THE_STYX_DROP_PODS_{locale}.md"
        for locale in TARGET_LOCALES
    ]
    manifest = {
        "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
        "release_set_id": name,
        "status": status,
        "packet_sources": [f"Docs/Lore/AppliedContent/packets/{name}.packets.json"],
        "article_sources": article_sources,
        "canonical_importer_ready": ready,
        "runtime_ready": False,
        "native_localization_ready": False,
        "data_monolith_ready": False,
        "h8bin_ready": False,
        "unity_placement_ready": False,
        "generated_page_ready": generated_page_ready,
        "publication_ready": publication_ready,
    }
    (release_sets / f"{name}_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


class TestValidateGrandLibraryLoreQuality(unittest.TestCase):
    def test_complete_article_set_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_utf8_bom_before_title_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_en_US.md"
            target.write_text("\ufeff" + article_text("en_US"), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_missing_locale_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS", omitted={"ru_RU"})

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("missing active locales: ru_RU", out.getvalue())

    def test_non_english_exact_clone_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            english = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_en_US.md"
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_de_DE.md"
            target.write_text(english.read_text(encoding="utf-8"), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("exact clone of en_US", out.getvalue())

    def test_inactive_locale_files_are_visible_warnings(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            library = root / "Docs" / "Lore" / "Grand_Library"
            (library / "21_THE_STYX_DROP_PODS_it_IT.md").write_text(
                article_text("de_DE", "Italian draft outside active roster."),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 0)
            self.assertIn("inactive Grand Library locale files", out.getvalue())
            self.assertIn("inactive_locale_files: 1", out.getvalue())

    def test_ai_cliche_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_en_US.md"
            target.write_text(article_text("en_US", "This entry explores a unique blend of survival and history."), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("anti-AI prose pattern detected", out.getvalue())

    def test_placeholder_token_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_de_DE.md"
            target.write_text(article_text("de_DE", "LOC HOLD until later."), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("placeholder token detected", out.getvalue())

    def test_required_status_comment_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_fr_FR.md"
            target.write_text("# Pressure Hull Note\n\nConcrete pressure hull notes.\n", encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("must carry draft localization status", out.getvalue())

    def test_mojibake_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            target = root / "Docs" / "Lore" / "Grand_Library" / "21_THE_STYX_DROP_PODS_es_ES.md"
            target.write_text(article_text("es_ES", "Broken marker: \ufffd"), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True)

            self.assertEqual(ret, 1)
            self.assertIn("mojibake marker detected", out.getvalue())

    def test_packet_bundle_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_packet_bundle(root, "RS900_TEST_PACKET")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True, packet_glob="RS900_TEST_PACKET*")

            self.assertEqual(ret, 0)
            self.assertIn("packet_bundles: 1", out.getvalue())

    def test_packet_mojibake_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_packet_bundle(root, "RS900_TEST_PACKET", broken_locale="ru_RU")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True, packet_glob="RS900_TEST_PACKET*")

            self.assertEqual(ret, 1)
            self.assertIn("RS900_TEST_PACKET.packets.json:P900_TEST_PACKET:ru_RU.scanner", out.getvalue())

    def test_packet_optional_external_site_article_empty_locale_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_packet_bundle(
                root,
                "RS900_TEST_PACKET",
                optional_article=True,
                empty_article_locale="ru_RU",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True, packet_glob="RS900_TEST_PACKET*")

            self.assertEqual(ret, 1)
            self.assertIn("ru_RU.external_site_article: missing optional packet text", out.getvalue())

    def test_packet_stale_external_site_article_path_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_packet_bundle(root, "RS900_TEST_PACKET", stale_article_path=True)

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(root, "21_THE_STYX_DROP_PODS", True, packet_glob="RS900_TEST_PACKET*")

            self.assertEqual(ret, 1)
            self.assertIn("external_site_article_path: missing packet reference path", out.getvalue())

    def test_release_manifest_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_release_manifest(root, "RS900_TEST_PACKET")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(
                    root,
                    "21_THE_STYX_DROP_PODS",
                    True,
                    packet_glob="RS900_TEST_PACKET*",
                    release_manifest_glob="RS900_TEST_PACKET*",
                )

            self.assertEqual(ret, 0)
            self.assertIn("release_manifests: 1", out.getvalue())

    def test_release_manifest_partial_import_and_page_ready_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_release_manifest(
                root,
                "RS900_TEST_PACKET",
                ready=True,
                generated_page_ready=True,
                status="canonical_importer_ready_route_card_exported_pages_generated_binding_targets_pending_h8bin",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(
                    root,
                    "21_THE_STYX_DROP_PODS",
                    True,
                    packet_glob="RS900_TEST_PACKET*",
                    release_manifest_glob="RS900_TEST_PACKET*",
                )

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_release_manifest_final_publication_ready_claim_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_release_manifest(root, "RS900_TEST_PACKET", ready=True, publication_ready=True)

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(
                    root,
                    "21_THE_STYX_DROP_PODS",
                    True,
                    packet_glob="RS900_TEST_PACKET*",
                    release_manifest_glob="RS900_TEST_PACKET*",
                )

            self.assertEqual(ret, 1)
            self.assertIn("publication_ready must not be true", out.getvalue())

    def test_release_manifest_generated_page_ready_requires_importer_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_release_manifest(root, "RS900_TEST_PACKET", generated_page_ready=True)

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(
                    root,
                    "21_THE_STYX_DROP_PODS",
                    True,
                    packet_glob="RS900_TEST_PACKET*",
                    release_manifest_glob="RS900_TEST_PACKET*",
                )

            self.assertEqual(ret, 1)
            self.assertIn("generated_page_ready requires canonical_importer_ready", out.getvalue())

    def test_release_manifest_status_ready_claim_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_article_set(root, "21_THE_STYX_DROP_PODS")
            write_release_manifest(
                root,
                "RS900_TEST_PACKET",
                status="runtime_ready_publication_ready_native_ready",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_validation(
                    root,
                    "21_THE_STYX_DROP_PODS",
                    True,
                    packet_glob="RS900_TEST_PACKET*",
                    release_manifest_glob="RS900_TEST_PACKET*",
                )

            self.assertEqual(ret, 1)
            self.assertIn("source content claims ready runtime/publication/native status", out.getvalue())


if __name__ == "__main__":
    unittest.main()
