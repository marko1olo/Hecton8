#if UNITY_EDITOR
namespace Hecton8.EditorTools
{
    public static class SurfaceSceneAuthoring1931Bridge
    {
        public static void ApplyAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_authoring_apply");
        }

        public static void CaptureAndExit()
        {
            SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_owner_lighting_capture");
        }
    }
}
#endif
