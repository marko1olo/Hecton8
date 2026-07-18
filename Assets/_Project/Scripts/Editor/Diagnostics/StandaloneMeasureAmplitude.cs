using System.Threading.Tasks;
using MapMagic.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using System.IO;

namespace Hecton8.Editor.Diagnostics
{
    public static class StandaloneMeasureAmplitude
    {
        [MenuItem("Hecton8/Diagnostics/Measure Final Amplitude")]
        public static void RunFix()
        {
            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var mm = Object.FindAnyObjectByType<MapMagicObject>();
            if (mm != null)
            {
                Debug.Log($"Forcing generation of MapMagic with height {mm.globals.height}");
                Hecton8.Diagnostics.HeadlessRunAll.ClearMapMagic(mm);
                mm.tiles.Pin(new Den.Tools.Coord(0, 0), false, mm);
                mm.StartGenerate();
                
                // Wait synchronously in headless mode
                while (mm.IsGenerating())
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            Hecton8.Diagnostics.MeasureAmplitudeTask.Run();
            
            EditorApplication.Exit(0);
        }
    }
}
