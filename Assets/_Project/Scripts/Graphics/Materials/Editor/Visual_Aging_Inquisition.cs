using UnityEditor;

namespace Hecton8.Graphics.Materials.Editor
{
    internal static class Visual_Aging_Inquisition
    {
        [MenuItem("Hecton8/Rendering/Visual Aging Inquisition")]
        public static void RunAndReveal()
        {
            VisualPressureAgingInquisition.RunAndReveal();
        }

        public static string Run()
        {
            return VisualPressureAgingInquisition.Run();
        }
    }
}
