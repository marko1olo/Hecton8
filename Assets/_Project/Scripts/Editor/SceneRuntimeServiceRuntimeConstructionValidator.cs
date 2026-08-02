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
    /// Soft-FAIL pin: SceneRuntimeService must keep Player-build EnsureRuntimeInstance
    /// construction (persistent-root parent + AddComponent) so ISceneService is not absent
    /// after 00_BOOTSTRAP unloads.
    /// </summary>
    internal sealed class SceneRuntimeServiceRuntimeConstructionValidator :
        IPreprocessBuildWithReport
    {
        private const string RuntimeRelativePath =
            "Assets/_Project/Scripts/Core/SceneRuntimeService.cs";

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
                        "[SceneRuntimeServiceRuntimeConstructionValidator] SOFT-FAIL: missing runtime source at " +
                        RuntimeRelativePath);
                    return;
                }

                string runtimeSource = File.ReadAllText(runtimePath);
                Pin(runtimeSource, "public static SceneRuntimeService EnsureRuntimeInstance", RuntimeRelativePath);
                Pin(runtimeSource, "Player-build construction path", RuntimeRelativePath);
                Pin(runtimeSource, "AddComponent<SceneRuntimeService>", RuntimeRelativePath);
                Pin(runtimeSource, "new GameObject(\"[SceneRuntimeService]\")", RuntimeRelativePath);

                if (File.Exists(bootstrapPath))
                {
                    string bootstrapSource = File.ReadAllText(bootstrapPath);
                    Pin(bootstrapSource, "SceneRuntimeService.EnsureRuntimeInstance", BootstrapRelativePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SceneRuntimeServiceRuntimeConstructionValidator] SOFT-FAIL exception: " +
                    exception.Message);
            }
        }

        private static void Pin(string source, string token, string pathLabel)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                Debug.LogError(
                    "[SceneRuntimeServiceRuntimeConstructionValidator] SOFT-FAIL: missing pin '" +
                    token +
                    "' in " +
                    pathLabel);
            }
        }
    }
}
#endif
