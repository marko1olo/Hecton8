#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Blocks serialized Everything LayerMask values from re-entering HECTON-8 data assets.
    /// </summary>
    internal sealed class HectonAssetIntegrityGuard : AssetPostprocessor
    {
        private const string GuardName = "HectonAssetIntegrityGuard";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (LayerMaskSanitizer.IsSanitizing || importedAssets == null || importedAssets.Length == 0)
                return;

            List<string> poisonedPaths = new List<string>(8);
            int poisonCount = LayerMaskSanitizer.CountPoisonedDataAssets(importedAssets, poisonedPaths);
            if (poisonCount <= 0)
                return;

            string message = BuildImportBlockMessage(poisonedPaths);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        private static string BuildImportBlockMessage(List<string> poisonedPaths)
        {
            string message = "[" + GuardName + "] Rejected data asset import: serialized Everything LayerMask detected.";
            int count = poisonedPaths.Count;
            for (int i = 0; i < count; i++)
                message += "\n" + poisonedPaths[i];
            return message;
        }
    }
}
#endif
