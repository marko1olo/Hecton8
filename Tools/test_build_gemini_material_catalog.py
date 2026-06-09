import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))
SCRATCH_ROOT = TOOLS_ROOT.parent / "Temp" / "ToolTests" / "test_build_gemini_material_catalog"

import BuildGeminiMaterialCatalog as catalog  # noqa: E402


class BuildGeminiMaterialCatalogTests(unittest.TestCase):
    def setUp(self) -> None:
        SCRATCH_ROOT.mkdir(parents=True, exist_ok=True)
        self._original_paths = {
            "WORLD_SUPPORT_DECAL_BUILDER_PATH": catalog.WORLD_SUPPORT_DECAL_BUILDER_PATH,
            "WORLD_SUPPORT_AUTHORING_PATH": catalog.WORLD_SUPPORT_AUTHORING_PATH,
            "VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH": catalog.VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH,
            "UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH": catalog.UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH,
        }

    def tearDown(self) -> None:
        for name, value in self._original_paths.items():
            setattr(catalog, name, value)

    def test_world_support_source_consumers_include_material_and_prefab_child(self) -> None:
        builder_path = SCRATCH_ROOT / "WorldSupportGeneratedDecalMaterialBuilder.cs"
        authoring_path = SCRATCH_ROOT / "WorldProceduralSupportFinalAuthoring.cs"
        catalog.WORLD_SUPPORT_DECAL_BUILDER_PATH = builder_path
        catalog.WORLD_SUPPORT_AUTHORING_PATH = authoring_path

        builder_path.write_text(
            """
public static class WorldSupportGeneratedDecalMaterialBuilder
{
    public const string OutputFolder = "Assets/_Project/Art/TEXTURES/Generated/WorldSupportDecals";
    public const string ViewportGlassEdgeMaterialPath = OutputFolder + "/MAT_B34_3418_ViewportGlassEdge.mat";

    private static readonly DecalMaterialSpec[] Specs =
    {
        new DecalMaterialSpec(
            ViewportGlassEdgeMaterialPath,
            "B34-3418",
            "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3418_thick_viewport_glass_edge_decal_atlas_AlphaCandidate.png")
    };
}
""",
            encoding="utf-8",
        )
        authoring_path.write_text(
            """
private const string ViewportGlassEdgeChildName = "RuinApexGlassEdge_ViewportRim";

private static void Author()
{
    Material viewportGlassEdgeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
        WorldSupportGeneratedDecalMaterialBuilder.ViewportGlassEdgeMaterialPath);
    AttachSupportDecal(
        lod0,
        viewportGlassEdgeMaterial,
        ViewportGlassEdgeChildName,
        new Vector3(0f, 0f, 0f));
}
""",
            encoding="utf-8",
        )

        consumers: dict[str, list[dict]] = {}
        catalog.parse_world_support_decal_source_consumers(consumers)

        b34_consumers = consumers["B34-3418"]
        self.assertIn("world_support_generated_decal_material", {entry["lane"] for entry in b34_consumers})
        self.assertIn("world_support_generated_decal_prefab_child", {entry["lane"] for entry in b34_consumers})
        self.assertIn(
            "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_RuinApex.prefab::RuinApexGlassEdge_ViewportRim",
            {entry["target"] for entry in b34_consumers},
        )

    def test_visor_trauma_source_consumers_preserve_slice_and_output(self) -> None:
        integrator_path = SCRATCH_ROOT / "Batch34VisorTraumaDecalArrayIntegrator.cs"
        catalog.VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH = integrator_path
        integrator_path.write_text(
            """
private const string OutputArrayPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/TextureArrays/TX_B34_VisorTrauma_DecalArray.asset";
private static readonly SliceBinding[] Bindings =
{
    new SliceBinding(7, "B34-3423", "leak rust biofilm"),
};
""",
            encoding="utf-8",
        )

        consumers: dict[str, list[dict]] = {}
        catalog.parse_visor_trauma_source_consumers(consumers)

        self.assertEqual("visor_trauma_decal_array_slice", consumers["B34-3423"][0]["lane"])
        self.assertIn("TX_B34_VisorTrauma_DecalArray.asset", consumers["B34-3423"][0]["target"])
        self.assertEqual("slice=7; leak rust biofilm", consumers["B34-3423"][0]["note"])

    def test_uv_atlas_handoff_source_consumers_preserve_target_texture_and_template(self) -> None:
        handoff_path = SCRATCH_ROOT / "Batch34UvAtlasMaterialHandoffBuilder.cs"
        catalog.UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH = handoff_path
        handoff_path.write_text(
            """
private static readonly AtlasMaterialSpec[] Specs =
{
    new AtlasMaterialSpec(
        "B34-3448",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34PaddedAtlasSources_20260608/Pickups/TX_B34-3448_resource_nodule_pickup_uv_atlas_Padded.png",
        "Assets/_Project/Art/Materials/Generated/MAT_B34_UvAtlas_Template.mat",
        "Assets/_Project/Art/Materials/Generated/MAT_B34_3448_ResourcePickupAtlas.mat")
};
""",
            encoding="utf-8",
        )

        consumers: dict[str, list[dict]] = {}
        catalog.parse_uv_atlas_material_handoff_source_consumers(consumers)

        consumer = consumers["B34-3448"][0]
        self.assertEqual("batch34_uv_atlas_material_handoff", consumer["lane"])
        self.assertEqual("Assets/_Project/Art/Materials/Generated/MAT_B34_3448_ResourcePickupAtlas.mat", consumer["target"])
        self.assertIn("TX_B34-3448_resource_nodule_pickup_uv_atlas_Padded.png", consumer["note"])
        self.assertIn("MAT_B34_UvAtlas_Template.mat", consumer["note"])


if __name__ == "__main__":
    unittest.main()
