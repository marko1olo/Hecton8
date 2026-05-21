#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.AITextureControlMaps
{
    [InitializeOnLoad]
    internal static class AITextureLiveMapPreview
    {
        private static readonly int PreviewModeId = Shader.PropertyToID("_PreviewMode");
        private static readonly int BakeBoundsMinId = Shader.PropertyToID("_BakeBoundsMin");
        private static readonly int BakeBoundsInvSizeId = Shader.PropertyToID("_BakeBoundsInvSize");
        private static readonly int BakeColorIdId = Shader.PropertyToID("_BakeColorId");
        private static readonly int CurvatureScaleId = Shader.PropertyToID("_CurvatureScale");
        private static readonly int CurvatureEdgeGainId = Shader.PropertyToID("_CurvatureEdgeGain");

        private static Object _target;
        private static AITextureControlPass _pass = AITextureControlPass.Curvature;
        private static float _globalQualityWeight = 1.0f;
        private static bool _enabled;
        private static Material _material;

        static AITextureLiveMapPreview()
        {
            SceneView.duringSceneGui -= DrawPreview;
            SceneView.duringSceneGui += DrawPreview;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeMaterial;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeMaterial;
            EditorApplication.quitting -= DisposeMaterial;
            EditorApplication.quitting += DisposeMaterial;
        }

        internal static void SetPreview(Object target, AITextureControlPass pass, bool enabled)
        {
            SetPreview(target, pass, enabled, _globalQualityWeight);
        }

        internal static void SetPreview(Object target, AITextureControlPass pass, bool enabled, float globalQualityWeight)
        {
            _target = target;
            _pass = pass;
            _globalQualityWeight = Mathf.Clamp01(globalQualityWeight);
            _enabled = enabled;
            SceneView.RepaintAll();
        }

        private static void DrawPreview(SceneView sceneView)
        {
            if (!_enabled || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Object source = _target != null ? _target : Selection.activeObject;
            if (!LocateMeshForPreview(source, out Mesh mesh, out Matrix4x4 matrix, out Vector3 labelPosition))
                return;

            Material material = EnsureMaterial();
            if (material == null)
                return;

            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            material.SetInt(PreviewModeId, (int)_pass);
            material.SetVector(BakeBoundsMinId, new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 0.0f));
            material.SetVector(BakeBoundsInvSizeId, new Vector4(
                size.x > 1e-5f ? 1.0f / size.x : 0.0f,
                size.y > 1e-5f ? 1.0f / size.y : 0.0f,
                size.z > 1e-5f ? 1.0f / size.z : 0.0f,
                0.0f));
            material.SetVector(BakeColorIdId, new Vector4(0.16f, 0.82f, 1.0f, 1.0f));
            material.SetFloat(CurvatureScaleId, SelectCurvatureScale(_globalQualityWeight));
            material.SetFloat(CurvatureEdgeGainId, SelectCurvatureEdgeGain(_globalQualityWeight));
            if (material.SetPass(0))
                Graphics.DrawMeshNow(mesh, matrix);

            Handles.color = new Color(0.18f, 0.92f, 1.0f, 0.88f);
            Handles.Label(labelPosition, "AI Control Preview: " + _pass);
        }

        private static bool LocateMeshForPreview(Object source, out Mesh mesh, out Matrix4x4 matrix, out Vector3 labelPosition)
        {
            mesh = null;
            matrix = Matrix4x4.identity;
            labelPosition = Vector3.zero;
            if (source == null)
                return false;

            mesh = source as Mesh;
            if (mesh != null)
            {
                labelPosition = mesh.bounds.center + Vector3.up * (mesh.bounds.extents.y + 0.25f);
                return true;
            }

            GameObject gameObject = source as GameObject;
            if (gameObject == null)
            {
                string assetPath = AssetDatabase.GetAssetPath(source);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { assetPath });
                    if (guids.Length > 0)
                    {
                        mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guids[0]));
                        if (mesh != null)
                        {
                            labelPosition = mesh.bounds.center + Vector3.up * (mesh.bounds.extents.y + 0.25f);
                            return true;
                        }
                    }
                }

                return false;
            }

            MeshFilter filter = gameObject.GetComponentInChildren<MeshFilter>(true);
            if (filter != null && filter.sharedMesh != null)
            {
                mesh = filter.sharedMesh;
                matrix = filter.transform.localToWorldMatrix;
                Bounds bounds = mesh.bounds;
                labelPosition = matrix.MultiplyPoint(bounds.center + Vector3.up * (bounds.extents.y + 0.25f));
                return true;
            }

            SkinnedMeshRenderer skinned = gameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null && skinned.sharedMesh != null)
            {
                mesh = skinned.sharedMesh;
                matrix = skinned.transform.localToWorldMatrix;
                Bounds bounds = mesh.bounds;
                labelPosition = matrix.MultiplyPoint(bounds.center + Vector3.up * (bounds.extents.y + 0.25f));
                return true;
            }

            return false;
        }

        private static float SelectCurvatureScale(float globalQualityWeight)
        {
            return Mathf.Lerp(0.35f, 1.25f, BuildQualityCurve(globalQualityWeight));
        }

        private static float SelectCurvatureEdgeGain(float globalQualityWeight)
        {
            return Mathf.Lerp(4.0f, 18.0f, BuildQualityCurve(globalQualityWeight));
        }

        private static float BuildQualityCurve(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            return q * q * (3.0f - 2.0f * q);
        }

        private static Material EnsureMaterial()
        {
            if (_material != null)
                return _material;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AITextureControlMapConstants.ScenePreviewShaderPath);
            if (shader == null)
                return null;

            _material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _material;
        }

        private static void DisposeMaterial()
        {
            if (_material == null)
                return;

            Object.DestroyImmediate(_material);
            _material = null;
        }
    }
}
#endif
