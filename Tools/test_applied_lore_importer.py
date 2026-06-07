import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import ROW_FLAG_DRAFT_LOCALIZATION, localized_row_flags, sanitize_localized_text


class TestAppliedLoreImporter(unittest.TestCase):
    def test_sanitize_strips_underscore_locale_draft_prefix(self):
        text = "Draft ru_RU localization pending native pass. Shallow Annex P-63 Pump Room"

        self.assertEqual(sanitize_localized_text(text), "Shallow Annex P-63 Pump Room")
        self.assertEqual(localized_row_flags({"title": text}), ROW_FLAG_DRAFT_LOCALIZATION)

    def test_sanitize_strips_hyphen_locale_draft_prefix(self):
        text = "Draft PT-BR localization pending native pass. Livro de Frenagem"

        self.assertEqual(sanitize_localized_text(text), "Livro de Frenagem")
        self.assertEqual(localized_row_flags({"title": text}), ROW_FLAG_DRAFT_LOCALIZATION)


if __name__ == "__main__":
    unittest.main()
