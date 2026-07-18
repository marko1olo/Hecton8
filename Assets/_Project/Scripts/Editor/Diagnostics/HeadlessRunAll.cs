using UnityEditor;
using UnityEngine;

namespace Hecton8.Diagnostics
{
    public static class HeadlessRunAll
    {
        private static int currentTaskIndex = 0;

        public static void Run()
        {
            Debug.Log("=============================================");
            Debug.Log("STARTING HEADLESS ARCHITECTURAL AUDIT RUNNER");
            Debug.Log("=============================================");
            
            Debug.Log("Starting Headless MapMagic Proof Mode...");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            // Create a dummy camera so MapMagic's ThreadManager priority queue doesn't crash from float.MaxValue distance
            var mm = UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            if (mm != null)
            {
                var dummyCamGo = new GameObject("HeadlessDummyCamera");
                dummyCamGo.transform.position = Vector3.zero;
                var cam = dummyCamGo.AddComponent<Camera>();
                mm.tiles.SetMainCamera(cam, true);
            }
            
            currentTaskIndex = 0;
            NextTask();
        }

        public static void NextTask()
        {
            currentTaskIndex++;
            switch (currentTaskIndex)
            {
                case 1:
                    HeadlessLiveSystemsValidator.Run();
                    break;
                case 2:
                    HeadlessTerrainDumper.Run();
                    break;
                case 3:
                    HeadlessMatrixBenchmark.Run();
                    break;
                case 4:
                    OfflineErosionBakePipeline.BakeCenterTile();
                    break;
                case 5:
                    CompareErosionTask.Run();
                    break;
                case 6:
                    MeasureAmplitudeTask.Run();
                    break;
                default:
                    Debug.Log("=============================================");
                    Debug.Log("ALL HEADLESS TASKS COMPLETED SUCCESSFULLY");
                    Debug.Log("=============================================");
                    EditorApplication.Exit(0);
                    break;
            }
        }

        public static void ClearMapMagic(MapMagic.Core.MapMagicObject mm)
        {
            if (mm == null || mm.tiles == null) return;
            foreach (var kvp in mm.tiles.grid)
            {
                if (kvp.Value != null) kvp.Value.Remove();
            }
            mm.tiles.grid.Clear();
            mm.tiles.pinned.Clear();
            
            // Wait for coroutines to stop
            Den.Tools.Tasks.CoroutineManager.Update();
        }
    }
}
