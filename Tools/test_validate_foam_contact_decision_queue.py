import csv
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateFoamContactDecisionQueue as validator  # noqa: E402


class ValidateFoamContactDecisionQueueTests(unittest.TestCase):
    def test_validate_queue_accepts_matching_decisions(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            queue_path = root / "queue.csv"
            self._write_source_paths(root)
            self._write_queue(queue_path)

            decisions = validator.validate_queue(path=queue_path, root=root)

        self.assertEqual(8, len(decisions))

    def test_source_only_candidate_rejects_direct_import_language_gap(self) -> None:
        row = validator.FoamDecision(
            decision_id="FOAMDEC-02",
            priority="P1",
            source_artifact="Docs/source.png",
            role="Foam/contact albedo source",
            decision="Use as source direction only",
            owner_route="ASSET_OWNER_11; ASSET_OWNER_20",
            required_before_import="2x2 tile proof; import readback; material proof",
            reject_if="Seam visible",
            low_consequence="Use only after proof",
            middle_consequence="Admit after proof",
            high_consequence="Add detail after proof",
            ultra_consequence="Layer only after proof",
            status="SOURCE_ONLY_CANDIDATE",
        )

        with self.assertRaises(SystemExit):
            validator.validate_source_only(row)

    def test_missing_source_path_rejects_queue(self) -> None:
        decisions = [
            validator.FoamDecision(
                decision_id="FOAMDEC-01",
                priority="P0",
                source_artifact="Docs/missing.png",
                role="Legacy visible foam tile",
                decision="Reject as final visible waterline or shoreline art",
                owner_route="ASSET_OWNER_11; ASSET_OWNER_20",
                required_before_import="replacement source and material proof required before visible use",
                reject_if="Turquoise pool-foam tile remains visible",
                low_consequence="Never use as compact fallback",
                middle_consequence="Replace after proof",
                high_consequence="Spend budget after proof",
                ultra_consequence="No overkill layer may depend on this rejected source",
                status="REJECTED_VISIBLE_SUPPORT",
            )
        ]

        with tempfile.TemporaryDirectory() as temp_dir:
            with self.assertRaises(SystemExit):
                validator.validate_source_paths(decisions, root=Path(temp_dir))

    def test_current_project_queue_matches_static_contract(self) -> None:
        decisions = validator.validate_queue()

        self.assertEqual(8, len(decisions))
        self.assertEqual(3, sum(1 for decision in decisions if decision.priority == "P0"))

    @staticmethod
    def _write_source_paths(root: Path) -> None:
        for source in _rows():
            path = root / source["SourceArtifact"]
            if path.suffix:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("source", encoding="utf-8")
            else:
                path.mkdir(parents=True, exist_ok=True)

    @staticmethod
    def _write_queue(path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=validator.REQUIRED_COLUMNS)
            writer.writeheader()
            writer.writerows(_rows())


def _rows() -> list[dict[str, str]]:
    return [
        {
            "DecisionId": "FOAMDEC-01",
            "Priority": "P0",
            "SourceArtifact": "Assets/_Project/Art/TEXTURES/foam.png",
            "Role": "Legacy visible foam tile",
            "Decision": "Reject as final visible waterline or shoreline art",
            "OwnerRoute": "ASSET_OWNER_11; ASSET_OWNER_20",
            "RequiredBeforeImport": "replacement source and material proof required before visible use",
            "RejectIf": "Turquoise pool-foam tile remains visible",
            "LowConsequence": "Never use as compact fallback",
            "MiddleConsequence": "Replace after proof",
            "HighConsequence": "Spend budget after proof",
            "UltraConsequence": "No overkill layer may depend on this rejected source",
            "Status": "REJECTED_VISIBLE_SUPPORT",
        },
        {
            "DecisionId": "FOAMDEC-02",
            "Priority": "P1",
            "SourceArtifact": "Docs/albedo.png",
            "Role": "Foam/contact albedo source",
            "Decision": "Use as source direction only",
            "OwnerRoute": "ASSET_OWNER_11; ASSET_OWNER_20",
            "RequiredBeforeImport": "2x2 tile proof; import readback; material proof",
            "RejectIf": "Imported directly as final art",
            "LowConsequence": "Use only after proof",
            "MiddleConsequence": "Admit after proof",
            "HighConsequence": "Add detail after proof",
            "UltraConsequence": "Layer only after proof",
            "Status": "SOURCE_ONLY_CANDIDATE",
        },
        {
            "DecisionId": "FOAMDEC-03",
            "Priority": "P1",
            "SourceArtifact": "Docs/normal.png",
            "Role": "Foam/contact detail normal source",
            "Decision": "Use as source direction only",
            "OwnerRoute": "ASSET_OWNER_11; ASSET_OWNER_20",
            "RequiredBeforeImport": "normal proof; BC5 normal import readback; material proof",
            "RejectIf": "Imported directly as final art",
            "LowConsequence": "Keep subtle until proof",
            "MiddleConsequence": "Admit after proof",
            "HighConsequence": "Use stronger detail after proof",
            "UltraConsequence": "Longer residency after proof",
            "Status": "SOURCE_ONLY_CANDIDATE",
        },
        {
            "DecisionId": "FOAMDEC-04",
            "Priority": "P0",
            "SourceArtifact": "Docs/mrao.png",
            "Role": "Packed mask source",
            "Decision": "Blocked for direct binding",
            "OwnerRoute": "ASSET_OWNER_11; ASSET_OWNER_20; ASSET_OWNER_24",
            "RequiredBeforeImport": "Channel roles rebuilt; linear import readback; material proof",
            "RejectIf": "False-color preview treated as final mask; channel roles guessed",
            "LowConsequence": "No packed mask import",
            "MiddleConsequence": "No packed mask import before channel proof",
            "HighConsequence": "Use stronger response after proof",
            "UltraConsequence": "Layered response after stable proof",
            "Status": "BLOCKED_CHANNEL_SEMANTICS",
        },
        {
            "DecisionId": "FOAMDEC-05",
            "Priority": "P0",
            "SourceArtifact": "Docs/mask.png",
            "Role": "RGBA contact mask source",
            "Decision": "Blocked for direct binding",
            "OwnerRoute": "ASSET_OWNER_11; ASSET_OWNER_20; ASSET_OWNER_24",
            "RequiredBeforeImport": "per-channel role proof; linear import readback; material proof",
            "RejectIf": "False-color preview treated as material truth; channel roles undocumented",
            "LowConsequence": "No RGBA contact import",
            "MiddleConsequence": "No RGBA contact import before channel proof",
            "HighConsequence": "Use richer breakup after proof",
            "UltraConsequence": "Layered residue after proof",
            "Status": "BLOCKED_CHANNEL_REWORK",
        },
        {
            "DecisionId": "FOAMDEC-06",
            "Priority": "P1",
            "SourceArtifact": "Docs/prototype",
            "Role": "First foam/contact prototype pack",
            "Decision": "Reference only source",
            "OwnerRoute": "ASSET_OWNER_11",
            "RequiredBeforeImport": "source proof and readback before any final use",
            "RejectIf": "never bind prototype pack directly",
            "LowConsequence": "Reference only",
            "MiddleConsequence": "Reference only",
            "HighConsequence": "Reference only",
            "UltraConsequence": "Reference only",
            "Status": "SOURCE_REFERENCE_ONLY",
        },
        {
            "DecisionId": "FOAMDEC-07",
            "Priority": "P2",
            "SourceArtifact": "Assets/visor-mask.png",
            "Role": "Visor droplet mask",
            "Decision": "Not a water-contact material candidate in this queue",
            "OwnerRoute": "ASSET_OWNER_17; ASSET_OWNER_20",
            "RequiredBeforeImport": "Separate UI/visor proof before use",
            "RejectIf": "Used as shoreline foam mask",
            "LowConsequence": "No use in water contact",
            "MiddleConsequence": "No use in water contact",
            "HighConsequence": "Possible separate proof only",
            "UltraConsequence": "Possible separate proof only",
            "Status": "OUT_OF_SCOPE_FOR_FOAM_CONTACT",
        },
        {
            "DecisionId": "FOAMDEC-08",
            "Priority": "P2",
            "SourceArtifact": "Assets/visor-normal.png",
            "Role": "Visor/detail normal source",
            "Decision": "Not a water-contact material candidate in this queue",
            "OwnerRoute": "ASSET_OWNER_17; ASSET_OWNER_20",
            "RequiredBeforeImport": "Separate UI/visor proof before use",
            "RejectIf": "Used as ocean or shoreline normal",
            "LowConsequence": "No use in water contact",
            "MiddleConsequence": "No use in water contact",
            "HighConsequence": "Possible separate proof only",
            "UltraConsequence": "Possible separate proof only",
            "Status": "OUT_OF_SCOPE_FOR_FOAM_CONTACT",
        },
    ]


if __name__ == "__main__":
    unittest.main()
