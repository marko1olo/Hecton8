import unittest
import sys
import tempfile
from pathlib import Path
import json

sys.path.insert(0, str(Path(__file__).parent))
import OutpostFailSafeValidate

class TestOutpostFailSafeValidate(unittest.TestCase):
    def test_fnv1a_utf16le(self):
        hash_val = OutpostFailSafeValidate.fnv1a_utf16le("outpost.generated")
        self.assertEqual(hash_val, 0x6bd854c2)

    def test_add_error(self):
        errors = []
        OutpostFailSafeValidate.add_error(errors, "Test error")
        self.assertEqual(len(errors), 1)
        self.assertEqual(errors[0], "Test error")

    def test_validate_outpost_refs(self):
        errors = []
        declared = {"outpost.test_1", "outpost.test_2"}
        text = "This uses outpost.test_1 and outpost.test_2"
        OutpostFailSafeValidate.validate_outpost_refs(text, declared, "Context", errors)
        self.assertEqual(len(errors), 0)

        OutpostFailSafeValidate.validate_outpost_refs("outpost.test_3", declared, "Context", errors)
        self.assertEqual(len(errors), 1)
        self.assertEqual(errors[0], "Context unknown outpost ref outpost.test_3")

    def test_validate_identity(self):
        valid_data = {
            "schema": OutpostFailSafeValidate.EXPECTED_SCHEMA,
            "agent": OutpostFailSafeValidate.EXPECTED_AGENT,
            "evidenceClass": "STATIC_DOC",
            "sourceBatch": OutpostFailSafeValidate.EXPECTED_SOURCE_BATCH,
            "requestedBatch": OutpostFailSafeValidate.EXPECTED_REQUESTED_BATCH,
            "requestedBatchPresent": False,
        }
        errors = []
        OutpostFailSafeValidate.validate_identity(valid_data, errors)
        self.assertEqual(len(errors), 0)

        invalid_data = {
            "schema": "wrong",
            "agent": "wrong",
            "evidenceClass": "wrong",
            "sourceBatch": "wrong",
            "requestedBatch": "wrong",
            "requestedBatchPresent": True,
        }
        errors = []
        OutpostFailSafeValidate.validate_identity(invalid_data, errors)
        self.assertEqual(len(errors), 6)

    def test_validate_runtime_decision(self):
        valid_data = {
            "runtimeAssetDecision": {
                "mutatedRuntimeLocalizationAssets": False,
                "activeEnglishTableObserved": OutpostFailSafeValidate.EXPECTED_LOC_TABLE,
                "reason": "Because",
            },
            "hashContract": {
                "algorithm": "FNV-1a 32-bit over UTF-16LE code units",
                "runtimeMatch": "Hecton.Localization.LocHash.Compute",
                "offsetBasis": "0x811C9DC5",
                "prime": "0x01000193",
            }
        }
        errors = []
        OutpostFailSafeValidate.validate_runtime_decision(valid_data, errors)
        self.assertEqual(len(errors), 0)

        invalid_data = {
            "runtimeAssetDecision": {},
            "hashContract": {}
        }
        errors = []
        OutpostFailSafeValidate.validate_runtime_decision(invalid_data, errors)
        self.assertEqual(len(errors), 7)

    def test_validate_flags_empty(self):
        errors = []
        declared = OutpostFailSafeValidate.validate_flags({}, errors)
        self.assertIn("missionFlags count mismatch: 0", errors)

    def test_validate_flags_invalid_prefix(self):
        data = {
            "missionFlags": [{"flag": "invalid.flag"} for _ in range(OutpostFailSafeValidate.EXPECTED_FLAG_COUNT)],
            "topologicalOrder": ["invalid.flag"] * OutpostFailSafeValidate.EXPECTED_FLAG_COUNT
        }
        errors = []
        OutpostFailSafeValidate.validate_flags(data, errors)
        self.assertTrue(any("invalid prefix" in e for e in errors))

    def test_validate_source_authority(self):
        data = {
            "sourceAuthority": {
                "status": OutpostFailSafeValidate.EXPECTED_SOURCE_AUTHORITY_STATUS,
                "expectedPromptId": OutpostFailSafeValidate.EXPECTED_AGENT,
                "expectedPromptRole": OutpostFailSafeValidate.EXPECTED_ROLE,
                "activeBatchContainsPrompt": False,
                "policy": "Some policy"
            }
        }
        errors = []
        status = OutpostFailSafeValidate.validate_source_authority(data, errors)
        self.assertEqual(status, OutpostFailSafeValidate.EXPECTED_SOURCE_AUTHORITY_STATUS)

        errors = []
        invalid_data = {"sourceAuthority": {}}
        OutpostFailSafeValidate.validate_source_authority(invalid_data, errors)
        self.assertTrue(len(errors) >= 5)

    def test_validate_localization(self):
        errors = []
        declared = {"outpost.test"}
        data = {}
        OutpostFailSafeValidate.validate_localization(data, declared, errors)
        self.assertTrue(any("tooltip count mismatch" in e for e in errors))
        self.assertTrue(any("log count mismatch" in e for e in errors))

    def test_validate_fallbacks(self):
        errors = []
        declared = {"outpost.test"}
        data = {}
        OutpostFailSafeValidate.validate_fallbacks(data, declared, errors)
        self.assertTrue(any("fallback count mismatch" in e for e in errors))

    def test_read_text(self):
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8-sig', delete=False) as f:
            f.write("test content")
            filepath = Path(f.name)

        content = OutpostFailSafeValidate.read_text(filepath)
        self.assertEqual(content, "test content")
        filepath.unlink()

if __name__ == '__main__':
    unittest.main()
