#!/usr/bin/env python3
"""Tests for Tools/AppliedLorePacketRewriteApply.py."""

from __future__ import annotations

import copy
import unittest
from pathlib import Path
from unittest.mock import patch

import AppliedLorePacketRewriteApply as rewrite_apply


FIELDS = ("title", "scanner", "field_note", "terminal", "audio", "in_game_wiki", "external_site")


def field_values(prefix: str) -> dict[str, str]:
    return {field: f"{prefix} {field}" for field in FIELDS}


class AppliedLorePacketRewriteApplyTests(unittest.TestCase):
    def packet_bundle(self) -> dict:
        return {
            "schema": "test",
            "packets": [
                {
                    "packet_id": "P_TEST",
                    "localized": {
                        "en_US": field_values("old en"),
                        "ru_RU": field_values("old ru"),
                    },
                }
            ],
        }

    def test_applies_rewrite_and_is_noop_when_repeated(self) -> None:
        packet_data = self.packet_bundle()
        rewrite_data = {"P_TEST": {"en_US": field_values("new en"), "ru_RU": field_values("new ru")}}

        def fake_load_json(path: Path) -> object:
            return packet_data if path.name == "packets.json" else rewrite_data

        with (
            patch.object(rewrite_apply, "load_json", side_effect=fake_load_json),
            patch.object(Path, "write_text", return_value=None) as write_text,
        ):
            changed, locale_rows, field_writes = rewrite_apply.apply_rewrites(Path("packets.json"), Path("rewrite.json"), FIELDS)
            self.assertEqual(changed, 14)
            self.assertEqual(locale_rows, 2)
            self.assertEqual(field_writes, 14)
            self.assertEqual(packet_data["packets"][0]["localized"]["ru_RU"]["scanner"], "new ru scanner")
            self.assertEqual(write_text.call_count, 1)

            write_text.reset_mock()
            changed_again, _, _ = rewrite_apply.apply_rewrites(Path("packets.json"), Path("rewrite.json"), FIELDS)
            self.assertEqual(changed_again, 0)
            write_text.assert_not_called()

    def test_rejects_missing_field_before_writing(self) -> None:
        packet_data = self.packet_bundle()
        before = copy.deepcopy(packet_data)
        values = field_values("new en")
        del values["audio"]
        rewrite_data = {"P_TEST": {"en_US": values}}

        def fake_load_json(path: Path) -> object:
            return packet_data if path.name == "packets.json" else rewrite_data

        with (
            patch.object(rewrite_apply, "load_json", side_effect=fake_load_json),
            patch.object(Path, "write_text", return_value=None) as write_text,
        ):
            with self.assertRaises(SystemExit):
                rewrite_apply.apply_rewrites(Path("packets.json"), Path("bad_rewrite.json"), FIELDS)
            write_text.assert_not_called()
            self.assertEqual(packet_data, before)


if __name__ == "__main__":
    unittest.main()
