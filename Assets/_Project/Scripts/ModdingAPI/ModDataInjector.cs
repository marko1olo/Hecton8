using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Hecton8.Inventory;
using UnityEngine;

namespace Hecton8.Modding
{
    internal static class ModDataInjector
    {
        public static void InjectDiscoveredData()
        {
            var runtimeInfos = new List<ModRuntimeInfo>();
            ModLoader.CollectRuntimeInfo(runtimeInfos);

            foreach (var info in runtimeInfos)
            {
                if (string.IsNullOrWhiteSpace(info.DirectoryPath))
                    continue;

                string dataFolderPath = Path.Combine(info.DirectoryPath, "Data");
                if (!Directory.Exists(dataFolderPath))
                    continue;

                string[] jsonFiles = Directory.GetFiles(dataFolderPath, "*.json", SearchOption.AllDirectories);
                foreach (string jsonPath in jsonFiles)
                {
                    try
                    {
                        ProcessDataFile(jsonPath, info.Metadata.Id);
                    }
                    catch (Exception ex)
                    {
                        H8Debug.LogError($"[ModDataInjector] Failed to process JSON '{jsonPath}' from mod '{info.Metadata.Id}': {ex.Message}");
                    }
                }
            }
        }

        private static void ProcessDataFile(string jsonPath, string modId)
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var overrideFile = JsonUtility.FromJson<ModDataOverrideFile>(jsonContent);

            if (overrideFile == null)
                return;

            if (overrideFile.ItemOverrides != null && overrideFile.ItemOverrides.Length > 0)
            {
                if (ItemTemplateRegistry.IsInitialized)
                {
                    ItemTemplateRegistry.ApplyModOverrides(overrideFile.ItemOverrides);
                    H8Debug.Log($"[ModDataInjector] Mod '{modId}' injected {overrideFile.ItemOverrides.Length} item overrides.");
                }
                else
                {
                    H8Debug.LogWarning($"[ModDataInjector] Mod '{modId}' tried to inject item overrides, but ItemTemplateRegistry is not initialized yet.");
                }
            }
        }
    }
}
