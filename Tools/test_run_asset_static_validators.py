import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import RunAssetStaticValidators as runner  # noqa: E402


class RunAssetStaticValidatorsTests(unittest.TestCase):
    def test_manifest_contains_core_asset_validators(self) -> None:
        names = {command.name for command in runner.VALIDATOR_COMMANDS}

        self.assertIn("mass_deletion_dirty_set", names)
        self.assertIn("asset_static_summary", names)
        self.assertIn("asset_front_file_map", names)
        self.assertIn("asset_proof_artifact_index", names)
        self.assertIn("audio_scene_static_route", names)
        self.assertIn("audio_waveform_proof_artifacts", names)
        self.assertIn("audio_direct_ref_detail", names)
        self.assertIn("audio_addressables_p0_synthesis", names)
        self.assertIn("audio_import_meta_policy", names)
        self.assertIn("audio_critical_cue_source_coverage", names)
        self.assertIn("player_route_static_evidence", names)
        self.assertIn("batch31_local_pbr_import_intent_artifacts", names)
        self.assertIn("texture_role_technical_ledger", names)
        self.assertIn("batch31_promotion_prep_artifacts", names)
        self.assertIn("mapmagic_erosion_source_route", names)
        self.assertIn("visual_proof_harness_candidate_quarantine", names)
        self.assertIn("visual_reference_owner_matrix", names)
        self.assertIn("visual_reference_current_rejection_matrix", names)
        self.assertIn("visual_source_promotion_queue", names)

    def test_argument_bound_unity_probe_validators_are_excluded(self) -> None:
        args = "\n".join(" ".join(command.args) for command in runner.VALIDATOR_COMMANDS)

        self.assertNotIn("ValidateTerrainProbeEvidence.py", args)
        self.assertNotIn("--log", args)
        self.assertNotIn("--metadata", args)
        self.assertIn("ValidateMassDeletionDirtySet.py --no-fail", args)
        self.assertIn("ValidateAudioSceneStaticRoute.py --no-fail", args)
        self.assertIn("ValidateAudioImportMetaPolicy.py --no-fail", args)
        self.assertIn("ValidateAudioCriticalCueSourceCoverage.py --no-fail", args)
        self.assertIn("ValidatePlayerRouteStaticEvidence.py --require-production-static --no-fail", args)
        self.assertIn("ValidateTextureRoleTechnicalLedger.py --no-fail", args)
        self.assertIn("ValidateVisualProofCaptureGuardrails.py --mode harness-candidate --allow-diagnostic-rejection", args)

    def test_dry_run_returns_success(self) -> None:
        self.assertEqual(0, runner.run_all(dry_run=True))


if __name__ == "__main__":
    unittest.main()
