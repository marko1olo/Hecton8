import contextlib
import io
import json
import sys
import unittest
from dataclasses import dataclass
from pathlib import Path
from unittest.mock import patch

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from AppliedLoreLocalizationDeltaAudit import (  # noqa: E402
    compute_localization_delta,
    localization_delta_to_json_payload,
    main,
    render_delta,
)


@dataclass(frozen=True)
class FakeRow:
    packet_id: str
    locale: str
    release_set_id: str
    article_id: str
    surface_mask: int
    fields: dict[str, str]
    flags: int
    line_number: int


def row(
    packet_id: str,
    locale: str,
    *,
    release_set_id: str = "RS_TEST",
    title: str = "Aegir pressure receipt",
    terminal: str = "Logged by a tired claims clerk.",
    flags: int = 0,
    surface_mask: int = (1 << 0) | (1 << 2),
    line_number: int = 2,
) -> FakeRow:
    return FakeRow(
        packet_id=packet_id,
        locale=locale,
        release_set_id=release_set_id,
        article_id=f"article.{packet_id.lower()}",
        surface_mask=surface_mask,
        fields={
            "title": title,
            "terminal": terminal,
        },
        flags=flags,
        line_number=line_number,
    )


class AppliedLoreLocalizationDeltaAuditTests(unittest.TestCase):
    def test_compute_localization_delta_accepts_native_ready_rows(self):
        stats = compute_localization_delta(
            [
                row("P001", "en_US"),
                row("P001", "ru_RU", title="Kvitantsiya davleniya Aegir", terminal="Zapisano ustavshim klerkom."),
            ]
        )

        self.assertTrue(stats.is_clean)
        self.assertEqual(stats.ready_non_english_rows, 1)
        self.assertEqual(stats.ready_by_locale[0].key, "ru_RU")
        self.assertEqual(stats.ready_by_release_set[0].key, "RS_TEST")
        self.assertIn("AppliedLore native localization delta OK", render_delta(stats))

    def test_compute_localization_delta_groups_draft_rows(self):
        stats = compute_localization_delta(
            [
                row("P001", "en_US"),
                row("P001", "ru_RU", release_set_id="RS_ONE", flags=1, line_number=3),
                row("P002", "ru_RU", release_set_id="RS_ONE", flags=1, line_number=4, surface_mask=1 << 4),
                row("P003", "ja_JP", release_set_id="RS_TWO", flags=1, line_number=5),
            ]
        )

        self.assertFalse(stats.is_clean)
        self.assertEqual(len(stats.draft_rows), 3)
        self.assertEqual(stats.draft_by_locale[0].key, "ru_RU")
        self.assertEqual(stats.draft_by_locale[0].count, 2)
        self.assertEqual(stats.draft_by_release_set[0].key, "RS_ONE")
        self.assertEqual(stats.draft_by_release_set[0].count, 2)
        self.assertEqual(stats.draft_english_clone_rows, 1)
        reason_counts = {group.key: group.count for group in stats.draft_by_reason}
        self.assertEqual(reason_counts["draft_english_clone"], 1)
        self.assertEqual(reason_counts["draft_missing_english_source"], 2)
        self.assertIn("draft surface title", render_delta(stats))
        self.assertIn("draft sample P001/ru_RU", render_delta(stats))

    def test_compute_localization_delta_classifies_draft_text_state(self):
        stats = compute_localization_delta(
            [
                row("P001", "en_US"),
                row("P001", "ru_RU", flags=1),
                row("P002", "en_US"),
                row(
                    "P002",
                    "de_DE",
                    title="Aegir Druckbeleg",
                    terminal="Von einem mueden Sachbearbeiter protokolliert.",
                    flags=1,
                ),
                row("P003", "en_US"),
                row("P003", "fr_FR", title="Recu de pression Aegir", flags=1),
            ]
        )

        self.assertFalse(stats.is_clean)
        self.assertEqual(stats.draft_english_clone_rows, 1)
        self.assertEqual(stats.draft_text_differs_rows, 1)
        self.assertEqual(stats.draft_partial_english_clone_rows, 1)
        reason_counts = {group.key: group.count for group in stats.draft_by_reason}
        self.assertEqual(reason_counts["draft_english_clone"], 1)
        self.assertEqual(reason_counts["draft_text_differs_from_english"], 1)
        self.assertEqual(reason_counts["draft_partial_english_clone"], 1)

    def test_compute_localization_delta_reports_non_draft_english_clone(self):
        stats = compute_localization_delta(
            [
                row("P001", "en_US"),
                row("P001", "de_DE"),
            ]
        )

        self.assertFalse(stats.is_clean)
        self.assertEqual(len(stats.english_clone_rows), 1)
        clone = stats.english_clone_rows[0]
        self.assertEqual(clone.reason, "non_draft_english_clone")
        self.assertEqual(clone.matching_fields, ("title", "terminal"))
        self.assertIn("english-clone sample P001/de_DE", render_delta(stats))

    def test_json_payload_truncates_groups_and_samples(self):
        stats = compute_localization_delta(
            [
                row("P001", "en_US"),
                row("P001", "ru_RU", release_set_id="RS_ONE", flags=1),
                row("P002", "ja_JP", release_set_id="RS_TWO", flags=1),
                row("P003", "en_US"),
                row("P003", "de_DE"),
            ]
        )

        payload = localization_delta_to_json_payload(stats, max_rows=1, max_groups=1, max_locales=1)

        self.assertFalse(payload["clean"])
        self.assertEqual(payload["rows"]["draft"], 2)
        self.assertEqual(payload["rows"]["draft_english_clone"], 1)
        self.assertEqual(payload["rows"]["non_draft_english_clone"], 1)
        self.assertEqual(payload["ready_by_locale"][0]["key"], "de_DE")
        self.assertEqual(payload["ready_by_release_set"][0]["key"], "RS_TEST")
        self.assertEqual(len(payload["draft_by_reason"]), 1)
        self.assertGreater(payload["truncated_draft_by_reason"], 0)
        self.assertEqual(len(payload["draft_samples"]), 1)
        self.assertIn("draft_english_clone", payload["draft_samples_by_reason"])
        self.assertIn("draft_missing_english_source", payload["draft_samples_by_reason"])
        self.assertEqual(payload["draft_samples_by_reason"]["draft_english_clone"]["count"], 1)
        self.assertEqual(payload["draft_samples_by_reason"]["draft_missing_english_source"]["count"], 1)
        self.assertEqual(payload["truncated_draft_samples"], 1)
        self.assertEqual(len(payload["draft_by_locale"]), 1)
        self.assertEqual(payload["truncated_draft_by_locale"], 1)

    def test_cli_returns_failure_for_draft_rows_with_json(self):
        rows = [row("P001", "en_US"), row("P001", "ru_RU", flags=1)]

        stdout = io.StringIO()
        stderr = io.StringIO()
        with patch("AppliedLoreLocalizationDeltaAudit.load_csv_rows", return_value=rows), \
            contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exit_code = main(["--root", ".", "--json", "--max-rows", "1"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertFalse(payload["clean"])
        self.assertEqual(payload["rows"]["draft"], 1)

    def test_cli_returns_zero_for_clean_rows(self):
        rows = [
            row("P001", "en_US"),
            row("P001", "ru_RU", title="Kvitantsiya davleniya Aegir", terminal="Zapisano ustavshim klerkom."),
        ]

        stdout = io.StringIO()
        with patch("AppliedLoreLocalizationDeltaAudit.load_csv_rows", return_value=rows), \
            contextlib.redirect_stdout(stdout):
            exit_code = main(["--root", "."])

        self.assertEqual(exit_code, 0)
        self.assertIn("delta OK", stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
