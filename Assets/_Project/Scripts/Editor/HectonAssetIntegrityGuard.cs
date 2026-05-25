#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Blocks serialized Everything LayerMask values from re-entering HECTON-8 data assets.
    /// </summary>
    internal sealed class HectonAssetIntegrityGuard : AssetPostprocessor
    {
        private const string GuardName = "HectonAssetIntegrityGuard";
        private static readonly List<string> s_poisonedPaths = new List<string>(8); // COLD ALLOC: List<string>[8] — imported data asset path scratch — owner: HectonAssetIntegrityGuard

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (LayerMaskSanitizer.IsSanitizing || importedAssets == null || importedAssets.Length == 0)
                return;

            s_poisonedPaths.Clear();
            try
            {
                int poisonCount = LayerMaskSanitizer.CountPoisonedDataAssets(importedAssets, s_poisonedPaths);
                if (poisonCount <= 0)
                    return;

                string message = BuildImportBlockMessage(s_poisonedPaths);
                H8Debug.LogError(message);
                throw new InvalidOperationException(message);
            }
            finally
            {
                s_poisonedPaths.Clear();
            }
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
