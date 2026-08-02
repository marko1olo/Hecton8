#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL pin: SuitHUDV4CanvasOverlayRuntimeConstructionValidator owner must keep Player-build construction
    /// so the service is not absent when bootstrap reorders.
    /// </summary>
    internal sealed class SuitHUDV4CanvasOverlayRuntimeConstructionValidator :
        IPreprocessBuildWithReport
    {
        private const string RuntimeRelativePath =
            "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";

        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void RegisterSoftFailOnLoad()
        {
            EditorApplication.delayCall += RunSoftFailValidation;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RunSoftFailValidation();
        }

        private static void RunSoftFailValidation()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string runtimePath = Path.Combine(projectRoot, RuntimeRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(runtimePath))
                {
                    Debug.LogError(
                        "[SuitHUDV4CanvasOverlayRuntimeConstructionValidator] SOFT-FAIL: missing runtime source at " +
                        RuntimeRelativePath);
                    return;
                }

                string runtimeSource = File.ReadAllText(runtimePath);
                Pin(runtimeSource, "EnsureRuntimeHudCanvasBindings", RuntimeRelativePath);
                Pin(runtimeSource, "Player-build construction path", RuntimeRelativePath);
                Pin(runtimeSource, "AddComponent<HectonUIScaler>", RuntimeRelativePath);
                Pin(runtimeSource, "AddComponent<SuitHUDV4CanvasOverlay>", RuntimeRelativePath);

                // No GameBootstrapper EnsureRuntimeInstance wire required for this owner.
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SuitHUDV4CanvasOverlayRuntimeConstructionValidator] SOFT-FAIL exception: " +
                    exception.Message);
            }
        }

        private static void Pin(string source, string token, string pathLabel)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                Debug.LogError(
                    "[SuitHUDV4CanvasOverlayRuntimeConstructionValidator] SOFT-FAIL: missing pin '" +
                    token +
                    "' in " +
                    pathLabel);
            }
        }
    }
}
#endif