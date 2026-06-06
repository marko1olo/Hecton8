#if UNITY_EDITOR
namespace Hecton8.EditorTools
{
    public static class SurfaceRoute1930AuthoringBridge
    {
        public static void ApplyAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");
        }

        public static void CaptureAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_owner_lighting_capture");
        }
    }
}
#endif
