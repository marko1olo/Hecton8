#if UNITY_EDITOR
namespace Hecton8.EditorTools
{
    public static class SurfaceCrestOceanMaterialAssignmentFixer
    {
        public static void AssignAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_crest_material_assign");
        }

        public static void ForceTextReserializeWorldSceneAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_scene_force_text_reserialize");
        }

        public static void ApplySurfaceRoutePersistentPolishAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");
        }

        public static void InvokeSurfaceRoutePrivatePolishAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_private_polish_invoke");
        }
    }
}
#endif
