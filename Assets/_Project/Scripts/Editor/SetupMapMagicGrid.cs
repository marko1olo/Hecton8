using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using System.IO;
using System.Text;

namespace Hecton8.Editor
{
    public static class SetupMapMagicGrid
    {
        private static readonly string LogPath = "C:/Users/danat/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4/setup_grid_log.txt";
        private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

        [MenuItem("Hecton8/Tests/Setup MapMagic 3x3")]
        public static void Execute()
        {
            var log = new StringBuilder();
            try
            {
                log.AppendLine("[SetupMapMagicGrid] Opening scene: " + ScenePath);
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    log.AppendLine("[ERROR] Scene is invalid or not found.");
                    File.WriteAllText(LogPath, log.ToString());
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                MapMagicObject mm = Object.FindAnyObjectByType<MapMagicObject>();
                if (mm == null)
                {
                    log.AppendLine("[ERROR] MapMagicObject NOT FOUND in scene.");
                    File.WriteAllText(LogPath, log.ToString());
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                log.AppendLine($"[INFO] Found MapMagicObject on GameObject: '{mm.gameObject.name}'");
                log.AppendLine($"[INFO] tileSize = {mm.tileSize}");

                int prevMain = mm.mainRange;
                int prevGenerate = mm.tiles.generateRange;

                // Range=1 => tiles from -1,-1 to +1,+1 => 3x3 = 9 chunks
                mm.mainRange = 1;
                mm.tiles.generateRange = 1;

                log.AppendLine($"[CHANGE] mainRange:           {prevMain} -> {mm.mainRange}");
                log.AppendLine($"[CHANGE] tiles.generateRange: {prevGenerate} -> {mm.tiles.generateRange}");
                log.AppendLine($"[INFO] Expected chunks: 3x3 = 9 (range -1..+1 on both axes)");

                EditorUtility.SetDirty(mm);
                bool saved = EditorSceneManager.SaveScene(scene);
                log.AppendLine($"[SAVE] Scene saved: {saved}");
                log.AppendLine("[SetupMapMagicGrid] SUCCESS.");

                File.WriteAllText(LogPath, log.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                log.AppendLine($"[EXCEPTION] {ex}");
                File.WriteAllText(LogPath, log.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
