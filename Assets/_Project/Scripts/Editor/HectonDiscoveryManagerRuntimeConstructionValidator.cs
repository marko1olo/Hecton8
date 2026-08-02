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
    /// Soft-FAIL pin: HectonDiscoveryManager must keep Player-build EnsureRuntimeInstance
    /// construction so the service is not absent when bootstrap reorders.
    /// </summary>
    internal sealed class HectonDiscoveryManagerRuntimeConstructionValidator :
        IPreprocessBuildWithReport
    {
        private const string RuntimeRelativePath =
            "Assets/_Project/Scripts/HectonDiscoveryManager.cs";

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
                string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(runtimePath))
                {
                    Debug.LogError(
                        "[HectonDiscoveryManagerRuntimeConstructionValidator] SOFT-FAIL: missing runtime source at " +
                        RuntimeRelativePath);
                    return;
                }

                string runtimeSource = File.ReadAllText(runtimePath);
                Pin(runtimeSource, "static HectonDiscoveryManager EnsureRuntimeInstance", RuntimeRelativePath);
                Pin(runtimeSource, "Player-build construction path", RuntimeRelativePath);
                Pin(runtimeSource, "AddComponent<HectonDiscoveryManager>", RuntimeRelativePath);
                Pin(runtimeSource, "new GameObject(\"[HectonDiscoveryManager]\")", RuntimeRelativePath);

                if (File.Exists(bootstrapPath))
                {
                    string bootstrapSource = File.ReadAllText(bootstrapPath);
                    Pin(bootstrapSource, "HectonDiscoveryManager.EnsureRuntimeInstance", BootstrapRelativePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[HectonDiscoveryManagerRuntimeConstructionValidator] SOFT-FAIL exception: " +
                    exception.Message);
            }
        }

        private static void Pin(string source, string token, string pathLabel)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                Debug.LogError(
                    "[HectonDiscoveryManagerRuntimeConstructionValidator] SOFT-FAIL: missing pin '" +
                    token +
                    "' in " +
                    pathLabel);
            }
        }
    }
}
#endif