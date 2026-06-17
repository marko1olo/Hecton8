using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hecton8.Modding;

namespace Hecton8.Editor
{
    public static class ModLoaderEnvelopeTest
    {
        [MenuItem("Hecton8/Verification/Test Envelope Policy")]
        public static void RunTest()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string modsDir = Path.Combine(projectRoot, "Mods");
            string testModDir = Path.Combine(modsDir, "TestEnvelopeMod");

            if (Directory.Exists(testModDir))
                Directory.Delete(testModDir, true);
            Directory.CreateDirectory(testModDir);

            // Create manifest that declares a managed assembly to trigger rejection
            string manifestPath = Path.Combine(testModDir, "mod.json");
            File.WriteAllText(manifestPath, @"{
                ""Id"": ""test.envelope.mod"",
                ""DisplayName"": ""Test Envelope Mod"",
                ""Version"": ""1.0.0"",
                ""Author"": ""AutomatedTest"",
                ""RequiredAPIVersion"": 1,
                ""EntryAssembly"": ""test.envelope.mod.dll"",
                ""ModPriority"": 0
            }");

            Debug.Log("[EnvelopeTest] Setup complete with manifest declaring DLL. Running ModLoader...");

            ResetAndBootstrap();

            var infos = new List<ModRuntimeInfo>();
            ModLoader.CollectRuntimeInfo(infos);

            bool failedCorrectly = false;

            Debug.Log($"[EnvelopeTest] CollectRuntimeInfo returned {infos.Count} items.");
            foreach (var info in infos)
            {
                Debug.Log($"[EnvelopeTest] Found mod: ID='{info.Metadata.Id}', Status={info.Status}, Message='{info.StatusMessage}'");
                if (info.Metadata.Id == "test.envelope.mod")
                {
                    if (info.Status == ModLoadStatus.Disabled && info.StatusMessage.Contains("strictly banned"))
                    {
                        failedCorrectly = true;
                    }
                }
            }

            if (!failedCorrectly)
            {
                Debug.LogError("[EnvelopeTest] FAILED! Mod with DLL was not rejected properly.");
                Cleanup(testModDir);
                return;
            }

            Debug.Log("[EnvelopeTest] Phase 1 PASS: DLL mod was rejected.");
            Debug.Log("[EnvelopeTest] SUCCESS! Envelope Policy works perfectly!");

            Cleanup(testModDir);
        }

        private static void Cleanup(string testModDir)
        {
            if (Directory.Exists(testModDir))
                Directory.Delete(testModDir, true);
        }

        private static void ResetAndBootstrap()
        {
            var type = typeof(ModLoader);

            var runtimeInfosField = type.GetField("_runtimeInfos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (runtimeInfosField != null)
            {
                var list = runtimeInfosField.GetValue(null) as System.Collections.IList;
                if (list != null) list.Clear();
            }

            var indexField = type.GetField("_runtimeInfoIndexByHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (indexField != null)
            {
                var dict = indexField.GetValue(null) as System.Collections.IDictionary;
                if (dict != null) dict.Clear();
            }

            var initMethod = type.GetMethod("DiscoverAndLoadMods", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (initMethod != null)
            {
                initMethod.Invoke(null, null);
            }
        }
    }
}
