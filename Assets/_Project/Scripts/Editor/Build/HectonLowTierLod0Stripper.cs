#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Build-time low-tier LOD policy: remove LOD0 renderers from build scenes so LOD1 is highest detail.
    /// </summary>
    public sealed class HectonLowTierLod0Stripper : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        private const string LowTierDefine = "HECTON_LOW_TIER";
        private const string LowTierDefineAlt = "HECTON_TIER_LOW";
        private const string BuildTierEnvironmentVariable = "HECTON_BUILD_TIER";
        private const string LowTierArgument = "-hectonLowTier";
        private const string LowTierArgumentAlt = "-hectonTierLow";
        private const string StripCountSessionKey = "HectonLowTierLod0Stripper.StrippedCount";

        // COLD ALLOC: List<GameObject>[256] - scene root traversal scratch - owner: HectonLowTierLod0Stripper
        private static readonly List<GameObject> s_RootScratch = new List<GameObject>(256);
        // COLD ALLOC: List<LODGroup>[512] - LODGroup traversal scratch - owner: HectonLowTierLod0Stripper
        private static readonly List<LODGroup> s_LodGroupScratch = new List<LODGroup>(512);

        public int callbackOrder => -14500;

        public void OnPreprocessBuild(BuildReport report)
        {
            SessionState.SetInt(StripCountSessionKey, 0);
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!ShouldStripLod0(report) || !scene.IsValid() || !scene.isLoaded)
                return;

            int stripped = StripScene(scene);
            if (stripped <= 0)
                return;

            int total = SessionState.GetInt(StripCountSessionKey, 0) + stripped;
            SessionState.SetInt(StripCountSessionKey, total);
            Debug.Log("[HectonLowTierLod0Stripper] Stripped LOD0 groups in scene '" + scene.path + "': " + stripped + ".");
        }

        internal static bool ShouldStripLod0(BuildReport report)
        {
            if (HasLowTierCommandLineArgument())
                return true;

            string tier = Environment.GetEnvironmentVariable(BuildTierEnvironmentVariable);
            if (!string.IsNullOrEmpty(tier) && IsLowTierToken(tier))
                return true;

            if (HasLowTierScriptingDefine(report))
                return true;

            BuildTarget platform = report != null ? report.summary.platform : EditorUserBuildSettings.activeBuildTarget;
            return platform == BuildTarget.Android || platform == BuildTarget.WebGL;
        }

        private static int StripScene(Scene scene)
        {
            int stripped = 0;
            s_RootScratch.Clear();
            scene.GetRootGameObjects(s_RootScratch);

            for (int rootIndex = 0; rootIndex < s_RootScratch.Count; rootIndex++)
            {
                GameObject root = s_RootScratch[rootIndex];
                if (root == null)
                    continue;

                s_LodGroupScratch.Clear();
                root.GetComponentsInChildren(true, s_LodGroupScratch);
                for (int groupIndex = 0; groupIndex < s_LodGroupScratch.Count; groupIndex++)
                {
                    if (StripLod0(s_LodGroupScratch[groupIndex]))
                        stripped++;
                }
            }

            s_LodGroupScratch.Clear();
            s_RootScratch.Clear();
            return stripped;
        }

        private static bool StripLod0(LODGroup lodGroup)
        {
            if (lodGroup == null)
                return false;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length <= 1)
                return false;

            Renderer[] lod0Renderers = lods[0].renderers;
            for (int i = 0; i < lod0Renderers.Length; i++)
            {
                Renderer renderer = lod0Renderers[i];
                if (renderer == null || RendererExistsInRemainingLods(renderer, lods))
                    continue;

                StripRendererMeshReference(renderer);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.enabled = false;
            }

            LOD[] strippedLods = new LOD[lods.Length - 1];
            for (int i = 1; i < lods.Length; i++)
            {
                LOD next = lods[i];
                if (i == 1)
                {
                    next.screenRelativeTransitionHeight = Mathf.Max(
                        lods[0].screenRelativeTransitionHeight,
                        lods[1].screenRelativeTransitionHeight);
                }

                strippedLods[i - 1] = next;
            }

            lodGroup.SetLODs(strippedLods);
            lodGroup.RecalculateBounds();
            return true;
        }

        private static bool RendererExistsInRemainingLods(Renderer renderer, LOD[] lods)
        {
            for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] == renderer)
                        return true;
                }
            }

            return false;
        }

        private static void StripRendererMeshReference(Renderer renderer)
        {
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                skinned.sharedMesh = null;
                return;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = null;
        }

        private static bool HasLowTierCommandLineArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], LowTierArgument, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], LowTierArgumentAlt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLowTierScriptingDefine(BuildReport report)
        {
            BuildTargetGroup targetGroup = report != null
                ? report.summary.platformGroup
                : EditorUserBuildSettings.selectedBuildTargetGroup;
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            if (string.IsNullOrEmpty(defines))
                return false;

            return DefinesContain(defines, LowTierDefine) ||
                   DefinesContain(defines, LowTierDefineAlt);
        }

        private static bool DefinesContain(string defines, string token)
        {
            int tokenLength = token.Length;
            int start = 0;
            while (start < defines.Length)
            {
                int end = defines.IndexOf(';', start);
                if (end < 0)
                    end = defines.Length;

                int length = end - start;
                if (length == tokenLength &&
                    string.Compare(defines, start, token, 0, tokenLength, StringComparison.Ordinal) == 0)
                {
                    return true;
                }

                start = end + 1;
            }

            return false;
        }

        private static bool IsLowTierToken(string tier)
        {
            return string.Equals(tier, "LOW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tier, "MINIMAL", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tier, "MX350", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tier, "TIER_LOW", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
