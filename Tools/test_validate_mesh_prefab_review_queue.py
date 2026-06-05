import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateMeshPrefabReviewQueue as validator  # noqa: E402


class ValidateMeshPrefabReviewQueueTests(unittest.TestCase):
    def test_prefab_counts_reject_missing_baked_row(self) -> None:
        prefabs = self._project_prefabs_without_one_baked_row()

        with self.assertRaises(SystemExit):
            validator.validate_prefab_counts(prefabs)

    def test_proxy_material_guid_resolution_detects_prefab_reference(self) -> None:
        material_guid = "258b2520dce86ef4f906901838cf9f88"
        prefabs = [
            validator.PrefabRow(
                path="Assets/_Project/Prefabs/Nature/Flora/Baked/one.prefab",
                has_lodgroup_token=True,
                builtin_primitive_mesh_ref_count=0,
                mesh_collider_token_count=0,
                policy_flags="NONE",
            )
        ]
        material_guid_map = {
            material_guid: "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat"
        }

        with self._temp_project_with_prefab(material_guid) as root:
            refs = validator.collect_proxy_material_baked_prefabs(prefabs, material_guid_map, root=root)

        self.assertEqual({"Assets/_Project/Prefabs/Nature/Flora/Baked/one.prefab"}, refs)

    def test_queue_rows_reject_proxy_promotion(self) -> None:
        rows = validator.load_queue()
        patched = [
            validator.QueueRow(
                queue_order=row.queue_order,
                priority=row.priority,
                pool=row.pool,
                static_evidence=row.static_evidence,
                required_proof=row.required_proof,
                reject_condition=row.reject_condition,
                disposition="CANDIDATE_OK",
                status=row.status,
            )
            if row.pool == "WorldProceduralProxy visible placement"
            else row
            for row in rows
        ]

        with self.assertRaises(SystemExit):
            validator.validate_queue_rows(patched)

    def test_current_project_queue_matches_static_evidence(self) -> None:
        baked, baked_proxy_refs, proxy, placeholders, construction = validator.validate_mesh_prefab_review_queue()

        self.assertEqual(89, baked)
        self.assertEqual(89, baked_proxy_refs)
        self.assertEqual(88, proxy)
        self.assertEqual(30, placeholders)
        self.assertEqual(10, construction)

    @staticmethod
    def _project_prefabs_without_one_baked_row() -> list[validator.PrefabRow]:
        rows = validator.load_prefab_rows()
        skipped = False
        result: list[validator.PrefabRow] = []
        for row in rows:
            if not skipped and row.path.startswith(validator.BAKED_FLORA_PREFIX):
                skipped = True
                continue
            result.append(row)
        return result

    @staticmethod
    def _temp_project_with_prefab(material_guid: str):
        import contextlib
        import tempfile

        @contextlib.contextmanager
        def make_project():
            with tempfile.TemporaryDirectory() as temp_dir:
                root = Path(temp_dir)
                prefab = root / "Assets/_Project/Prefabs/Nature/Flora/Baked/one.prefab"
                prefab.parent.mkdir(parents=True, exist_ok=True)
                prefab.write_text(
                    f"m_Materials:\n- {{fileID: 2100000, guid: {material_guid}, type: 2}}\n",
                    encoding="utf-8",
                )
                yield root

        return make_project()


if __name__ == "__main__":
    unittest.main()
