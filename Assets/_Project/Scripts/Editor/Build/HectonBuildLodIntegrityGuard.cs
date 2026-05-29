#if UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Build-time guard that deliberately preserves authored LOD chains.
    /// Runtime continuous quality systems own LOD selection; build preprocessors must not destroy high-detail content.
    /// </summary>
    public sealed class HectonBuildLodIntegrityGuard : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => -14500;

        public void OnPreprocessBuild(BuildReport report)
        {
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
        }
    }
}
#endif
