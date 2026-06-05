#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only source authoring route for first-pass transport body meshes.
    /// Output is intentionally limited to future Mesh assets under
    /// Assets/_Project/Art/Generated/ProductFace/Transport.
    ///
    /// Boundary:
    /// - Does not edit prefabs, anchors, transport presets, colliders, scenes, or runtime truth.
    /// - Future prefab replacement must prove anchor preservation and collider/proxy split separately.
    /// - Rider/dismount clearance intent is recorded in each spec so visual hulls do not consume gameplay space.
    ///
    /// Scaling:
    /// - GlobalQualityWeight is continuous. Compact keeps strong silhouette and material-channel intent.
    /// - Middle adds more clamp/rail/panel detail.
    /// - High adds denser bevel-like support geometry and cleaner viewport/tank reads.
    /// - Ultra adds secondary handles, pods, plates, and fin detail without changing anchors, collision, presets, or gameplay truth.
    /// </summary>
    public sealed class ProductFaceTransportMeshSourceAuthoring : EditorWindow
    {
        private const string OutputDirectory = "Assets/_Project/Art/Generated/ProductFace/Transport";
        private const string MenuPath = "HECTON-8/Product Face/Author Transport Mesh Sources";
        private const float Epsilon = 0.000001f;

        [SerializeField] private float _globalQualityWeight = 0.65f;
        [SerializeField] private bool _generateCargoSled = true;
        [SerializeField] private bool _generateExosuitFrame = true;
        [SerializeField] private bool _generateMicroSub = true;
        [SerializeField] private bool _generateScoutGlider = true;

        [MenuItem(MenuPath, false, 1876)]
        public static void Open()
        {
            GetWindow<ProductFaceTransportMeshSourceAuthoring>("Transport Mesh Sources");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Editor-only source authoring. Writes Mesh assets only. Does not replace prefabs or modify transport anchors/presets.",
                MessageType.Info);

            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _generateScoutGlider = EditorGUILayout.ToggleLeft("ScoutGlider", _generateScoutGlider);
            _generateCargoSled = EditorGUILayout.ToggleLeft("CargoSled", _generateCargoSled);
            _generateExosuitFrame = EditorGUILayout.ToggleLeft("ExosuitFrame", _generateExosuitFrame);
            _generateMicroSub = EditorGUILayout.ToggleLeft("MicroSub", _generateMicroSub);

            using (new EditorGUI.DisabledScope(!HasSelection()))
            {
                if (GUILayout.Button("Generate Selected Transport Mesh Sources"))
                    GenerateSelected();
            }
        }

        private bool HasSelection()
        {
            return _generateCargoSled || _generateExosuitFrame || _generateMicroSub || _generateScoutGlider;
        }

        private void GenerateSelected()
        {
            EnsureAssetFolder(OutputDirectory);

            float q = ClampQuality(_globalQualityWeight);
            int generated = 0;
            if (_generateScoutGlider)
                generated += GenerateAndSave(TransportSpec.CreateScoutGlider(q));
            if (_generateCargoSled)
                generated += GenerateAndSave(TransportSpec.CreateCargoSled(q));
            if (_generateExosuitFrame)
                generated += GenerateAndSave(TransportSpec.CreateExosuitFrame(q));
            if (_generateMicroSub)
                generated += GenerateAndSave(TransportSpec.CreateMicroSub(q));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProductFaceTransportMeshSourceAuthoring] Generated transport mesh source assets: " + generated);
        }

        private static int GenerateAndSave(TransportSpec spec)
        {
            MeshDraft draft = new MeshDraft(spec.TransportId);
            BuildTransport(spec, draft);

            MeshValidation.ValidateDraft(spec, draft);
            Mesh mesh = draft.ToMesh("GEN_" + spec.TransportId + "_Source_LOD0");
            MeshValidation.ValidateMesh(spec, mesh);

            string path = OutputDirectory + "/" + mesh.name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                DestroyImmediate(mesh);
                MeshValidation.ValidateMesh(spec, existing);
                Debug.Log("[ProductFaceTransportMeshSourceAuthoring] Updated mesh source: " + path);
                return 1;
            }

            AssetDatabase.CreateAsset(mesh, path);
            Debug.Log("[ProductFaceTransportMeshSourceAuthoring] Created mesh source: " + path);
            return 1;
        }

        private static void BuildTransport(TransportSpec spec, MeshDraft draft)
        {
            switch (spec.Kind)
            {
                case TransportKind.CargoSled:
                    BuildCargoSled(spec, draft);
                    break;
                case TransportKind.ExosuitFrame:
                    BuildExosuitFrame(spec, draft);
                    break;
                case TransportKind.MicroSub:
                    BuildMicroSub(spec, draft);
                    break;
                case TransportKind.ScoutGlider:
                    BuildScoutGlider(spec, draft);
                    break;
                default:
                    throw new InvalidOperationException("Unknown transport kind: " + spec.Kind);
            }
        }

        private static void BuildCargoSled(TransportSpec spec, MeshDraft draft)
        {
            int sideDetails = Mathf.Max(2, spec.DetailCount);
            AddRoundedDeck(draft, new Vector3(0f, -0.03f, 0f), new Vector3(2.65f, 0.16f, 1.28f), SurfaceRole.Hull);
            AddBox(draft, new Vector3(0f, 0.09f, 0.52f), new Vector3(2.86f, 0.14f, 0.13f), SurfaceRole.Rubber);
            AddBox(draft, new Vector3(0f, 0.09f, -0.52f), new Vector3(2.86f, 0.14f, 0.13f), SurfaceRole.Rubber);
            AddRail(draft, new Vector3(-0.94f, 0.24f, 0.64f), new Vector3(0.94f, 0.24f, 0.64f), 0.045f, SurfaceRole.Trim);
            AddRail(draft, new Vector3(-0.94f, 0.24f, -0.64f), new Vector3(0.94f, 0.24f, -0.64f), 0.045f, SurfaceRole.Trim);
            AddTank(draft, new Vector3(-0.82f, -0.05f, 0.77f), 0.12f, 1.08f, Axis.Z, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(0.82f, -0.05f, 0.77f), 0.12f, 1.08f, Axis.Z, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(-0.82f, -0.05f, -0.77f), 0.12f, 1.08f, Axis.Z, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(0.82f, -0.05f, -0.77f), 0.12f, 1.08f, Axis.Z, spec.Segments, SurfaceRole.Hull);

            for (int i = 0; i < sideDetails; i++)
            {
                float x = Mathf.Lerp(-1.04f, 1.04f, sideDetails == 1 ? 0.5f : i / (sideDetails - 1f));
                AddClamp(draft, new Vector3(x, 0.22f, 0.47f), 0.16f, 0.09f, SurfaceRole.Trim);
                AddClamp(draft, new Vector3(x, 0.22f, -0.47f), 0.16f, 0.09f, SurfaceRole.Trim);
            }

            AddHandle(draft, new Vector3(-1.42f, 0.1f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.26f, SurfaceRole.Trim);
            AddHandle(draft, new Vector3(1.42f, 0.1f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.26f, SurfaceRole.Trim);
            AddSkid(draft, new Vector3(0f, -0.22f, 0.47f), 2.45f, SurfaceRole.Rubber);
            AddSkid(draft, new Vector3(0f, -0.22f, -0.47f), 2.45f, SurfaceRole.Rubber);
            MeshValidation.ValidateClearanceIntent(spec);
        }

        private static void BuildExosuitFrame(TransportSpec spec, MeshDraft draft)
        {
            AddBox(draft, new Vector3(0f, 0.96f, 0f), new Vector3(0.72f, 0.95f, 0.42f), SurfaceRole.Hull);
            AddViewportPlane(draft, new Vector3(0f, 1.22f, -0.235f), new Vector2(0.38f, 0.25f), SurfaceRole.Glass);
            AddRail(draft, new Vector3(-0.48f, 1.38f, 0f), new Vector3(-0.48f, 0.45f, 0f), 0.055f, SurfaceRole.Trim);
            AddRail(draft, new Vector3(0.48f, 1.38f, 0f), new Vector3(0.48f, 0.45f, 0f), 0.055f, SurfaceRole.Trim);
            AddRail(draft, new Vector3(-0.34f, 1.48f, 0.22f), new Vector3(0.34f, 1.48f, 0.22f), 0.05f, SurfaceRole.Trim);
            AddRail(draft, new Vector3(-0.34f, 0.45f, 0.22f), new Vector3(0.34f, 0.45f, 0.22f), 0.05f, SurfaceRole.Trim);

            AddThrusterPod(draft, new Vector3(-0.62f, 0.98f, 0.28f), Quaternion.Euler(90f, 0f, 0f), spec.Segments, SurfaceRole.Hull);
            AddThrusterPod(draft, new Vector3(0.62f, 0.98f, 0.28f), Quaternion.Euler(90f, 0f, 0f), spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(-0.38f, 1.02f, 0.34f), 0.08f, 0.88f, Axis.Y, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(0.38f, 1.02f, 0.34f), 0.08f, 0.88f, Axis.Y, spec.Segments, SurfaceRole.Hull);

            float limbSpread = 0.58f;
            AddSocket(draft, new Vector3(-limbSpread, 1.33f, 0f), SurfaceRole.Trim);
            AddSocket(draft, new Vector3(limbSpread, 1.33f, 0f), SurfaceRole.Trim);
            AddSocket(draft, new Vector3(-0.34f, 0.28f, 0f), SurfaceRole.Trim);
            AddSocket(draft, new Vector3(0.34f, 0.28f, 0f), SurfaceRole.Trim);
            AddHandle(draft, new Vector3(-0.42f, 0.8f, -0.33f), Quaternion.identity, 0.22f, SurfaceRole.Rubber);
            AddHandle(draft, new Vector3(0.42f, 0.8f, -0.33f), Quaternion.identity, 0.22f, SurfaceRole.Rubber);

            for (int i = 0; i < spec.DetailCount; i++)
            {
                float y = Mathf.Lerp(0.55f, 1.33f, spec.DetailCount == 1 ? 0.5f : i / (spec.DetailCount - 1f));
                AddClamp(draft, new Vector3(-0.05f, y, -0.25f), 0.18f, 0.045f, SurfaceRole.Trim);
            }

            MeshValidation.ValidateClearanceIntent(spec);
        }

        private static void BuildMicroSub(TransportSpec spec, MeshDraft draft)
        {
            AddPressureHull(draft, new Vector3(0f, 0.05f, 0f), new Vector3(1.85f, 0.64f, 0.84f), spec.Segments + 6, SurfaceRole.Hull);
            AddViewportPlane(draft, new Vector3(0f, 0.22f, -0.87f), new Vector2(0.72f, 0.38f), SurfaceRole.Glass);
            AddBox(draft, new Vector3(0f, 0.69f, -0.12f), new Vector3(0.78f, 0.14f, 0.52f), SurfaceRole.Trim);
            AddTank(draft, new Vector3(-1.05f, -0.36f, 0.66f), 0.13f, 1.22f, Axis.X, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(1.05f, -0.36f, 0.66f), 0.13f, 1.22f, Axis.X, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(-1.05f, -0.36f, -0.66f), 0.13f, 1.22f, Axis.X, spec.Segments, SurfaceRole.Hull);
            AddTank(draft, new Vector3(1.05f, -0.36f, -0.66f), 0.13f, 1.22f, Axis.X, spec.Segments, SurfaceRole.Hull);
            AddThrusterPod(draft, new Vector3(-1.82f, -0.08f, 0.46f), Quaternion.Euler(0f, 90f, 0f), spec.Segments, SurfaceRole.Trim);
            AddThrusterPod(draft, new Vector3(-1.82f, -0.08f, -0.46f), Quaternion.Euler(0f, 90f, 0f), spec.Segments, SurfaceRole.Trim);
            AddThrusterPod(draft, new Vector3(1.82f, -0.08f, 0.46f), Quaternion.Euler(0f, 90f, 0f), spec.Segments, SurfaceRole.Trim);
            AddThrusterPod(draft, new Vector3(1.82f, -0.08f, -0.46f), Quaternion.Euler(0f, 90f, 0f), spec.Segments, SurfaceRole.Trim);
            AddFin(draft, new Vector3(0f, -0.61f, 0.88f), new Vector3(1.35f, 0.08f, 0.36f), SurfaceRole.Trim);
            AddFin(draft, new Vector3(0f, -0.61f, -0.88f), new Vector3(1.35f, 0.08f, -0.36f), SurfaceRole.Trim);

            for (int i = 0; i < spec.DetailCount + 1; i++)
            {
                float x = Mathf.Lerp(-0.78f, 0.78f, (spec.DetailCount + 1) == 1 ? 0.5f : i / (float)spec.DetailCount);
                AddClamp(draft, new Vector3(x, 0.67f, 0.38f), 0.18f, 0.055f, SurfaceRole.Trim);
            }

            MeshValidation.ValidateClearanceIntent(spec);
        }

        private static void BuildScoutGlider(TransportSpec spec, MeshDraft draft)
        {
            AddPressureHull(draft, new Vector3(0f, 0f, 0f), new Vector3(0.82f, 0.22f, 0.28f), spec.Segments, SurfaceRole.Hull);
            AddViewportPlane(draft, new Vector3(0f, 0.05f, -0.315f), new Vector2(0.3f, 0.16f), SurfaceRole.Glass);
            AddFin(draft, new Vector3(-0.42f, -0.02f, 0.24f), new Vector3(-0.68f, 0.015f, 0.88f), SurfaceRole.Trim);
            AddFin(draft, new Vector3(0.42f, -0.02f, 0.24f), new Vector3(0.68f, 0.015f, 0.88f), SurfaceRole.Trim);
            AddFin(draft, new Vector3(-0.42f, -0.02f, -0.24f), new Vector3(-0.68f, 0.015f, -0.88f), SurfaceRole.Trim);
            AddFin(draft, new Vector3(0.42f, -0.02f, -0.24f), new Vector3(0.68f, 0.015f, -0.88f), SurfaceRole.Trim);
            AddTank(draft, new Vector3(-0.26f, -0.2f, 0f), 0.075f, 0.62f, Axis.X, spec.Segments, SurfaceRole.Rubber);
            AddTank(draft, new Vector3(0.26f, -0.2f, 0f), 0.075f, 0.62f, Axis.X, spec.Segments, SurfaceRole.Rubber);
            AddThrusterPod(draft, new Vector3(0.92f, -0.02f, 0f), Quaternion.Euler(0f, 90f, 0f), spec.Segments, SurfaceRole.Trim);
            AddRail(draft, new Vector3(-0.38f, 0.19f, 0.18f), new Vector3(0.42f, 0.19f, 0.18f), 0.035f, SurfaceRole.Rubber);
            AddRail(draft, new Vector3(-0.38f, 0.19f, -0.18f), new Vector3(0.42f, 0.19f, -0.18f), 0.035f, SurfaceRole.Rubber);
            AddBox(draft, new Vector3(-0.82f, 0.02f, 0f), new Vector3(0.18f, 0.13f, 0.18f), SurfaceRole.Glass);

            for (int i = 0; i < spec.DetailCount; i++)
            {
                float x = Mathf.Lerp(-0.48f, 0.48f, spec.DetailCount == 1 ? 0.5f : i / (spec.DetailCount - 1f));
                AddClamp(draft, new Vector3(x, 0.255f, 0f), 0.12f, 0.04f, SurfaceRole.Trim);
            }

            MeshValidation.ValidateClearanceIntent(spec);
        }

        private static void AddRoundedDeck(MeshDraft draft, Vector3 center, Vector3 size, SurfaceRole role)
        {
            AddBox(draft, center, size, role);
            AddBox(draft, center + new Vector3(0f, size.y * 0.52f, 0f), new Vector3(size.x * 0.82f, size.y * 0.45f, size.z * 0.76f), role);
        }

        private static void AddPressureHull(MeshDraft draft, Vector3 center, Vector3 radii, int segments, SurfaceRole role)
        {
            int seg = Mathf.Clamp(segments, 12, 40);
            int rings = Mathf.Clamp(Mathf.RoundToInt(seg * 0.55f), 7, 22);
            int start = draft.VertexCount;

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float x = Mathf.Lerp(-radii.x, radii.x, t);
                float cap = Mathf.Sin(t * Mathf.PI);
                float yzScale = Mathf.Lerp(0.18f, 1f, Mathf.Sqrt(Mathf.Max(0f, cap)));
                for (int s = 0; s < seg; s++)
                {
                    float a = s * Mathf.PI * 2f / seg;
                    Vector3 local = new Vector3(
                        x,
                        Mathf.Sin(a) * radii.y * yzScale,
                        Mathf.Cos(a) * radii.z * yzScale);
                    Vector3 normal = new Vector3(
                        Mathf.Lerp(-0.55f, 0.55f, t),
                        local.y / Mathf.Max(Epsilon, radii.y),
                        local.z / Mathf.Max(Epsilon, radii.z)).normalized;
                    draft.AddVertex(center + local, normal, new Vector2(s / (float)seg, t), RoleColor(role));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < seg; s++)
                {
                    int a = start + r * seg + s;
                    int b = start + r * seg + ((s + 1) % seg);
                    int c = start + (r + 1) * seg + s;
                    int d = start + (r + 1) * seg + ((s + 1) % seg);
                    draft.AddTriangle(a, c, b);
                    draft.AddTriangle(b, c, d);
                }
            }
        }

        private static void AddTank(MeshDraft draft, Vector3 center, float radius, float length, Axis axis, int segments, SurfaceRole role)
        {
            int seg = Mathf.Clamp(segments, 10, 32);
            float half = length * 0.5f;
            int start = draft.VertexCount;

            for (int end = 0; end < 2; end++)
            {
                float axial = end == 0 ? -half : half;
                for (int s = 0; s < seg; s++)
                {
                    float a = s * Mathf.PI * 2f / seg;
                    Vector3 radial = new Vector3(0f, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius);
                    Vector3 local = AxisVector(axis, axial) + OrientRadial(axis, radial);
                    Vector3 normal = OrientRadial(axis, new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a))).normalized;
                    draft.AddVertex(center + local, normal, new Vector2(s / (float)seg, end), RoleColor(role));
                }
            }

            for (int s = 0; s < seg; s++)
            {
                int a = start + s;
                int b = start + ((s + 1) % seg);
                int c = start + seg + s;
                int d = start + seg + ((s + 1) % seg);
                draft.AddTriangle(a, c, b);
                draft.AddTriangle(b, c, d);
            }

            AddCap(draft, center + AxisVector(axis, -half), axis, -1f, radius, seg, role);
            AddCap(draft, center + AxisVector(axis, half), axis, 1f, radius, seg, role);
        }

        private static void AddThrusterPod(MeshDraft draft, Vector3 center, Quaternion rotation, int segments, SurfaceRole role)
        {
            int seg = Mathf.Clamp(segments, 10, 30);
            AddTank(draft, center, 0.14f, 0.34f, Axis.X, seg, role);
            AddBox(draft, center + rotation * new Vector3(0.2f, 0f, 0f), new Vector3(0.06f, 0.2f, 0.2f), SurfaceRole.Trim);
        }

        private static void AddRail(MeshDraft draft, Vector3 a, Vector3 b, float radius, SurfaceRole role)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 delta = b - a;
            Axis axis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? Axis.X
                : Mathf.Abs(delta.y) >= Mathf.Abs(delta.z) ? Axis.Y : Axis.Z;
            AddTank(draft, mid, radius, delta.magnitude, axis, 10, role);
        }

        private static void AddHandle(MeshDraft draft, Vector3 center, Quaternion rotation, float width, SurfaceRole role)
        {
            Vector3 left = center + rotation * new Vector3(-width * 0.5f, 0f, 0f);
            Vector3 right = center + rotation * new Vector3(width * 0.5f, 0f, 0f);
            Vector3 raisedLeft = left + rotation * new Vector3(0f, 0.16f, 0f);
            Vector3 raisedRight = right + rotation * new Vector3(0f, 0.16f, 0f);
            AddRail(draft, left, raisedLeft, 0.035f, role);
            AddRail(draft, raisedLeft, raisedRight, 0.035f, role);
            AddRail(draft, raisedRight, right, 0.035f, role);
        }

        private static void AddClamp(MeshDraft draft, Vector3 center, float width, float height, SurfaceRole role)
        {
            AddBox(draft, center, new Vector3(width, height, 0.055f), role);
            AddBox(draft, center + new Vector3(-width * 0.42f, height * 0.42f, 0f), new Vector3(width * 0.18f, height * 0.7f, 0.07f), role);
            AddBox(draft, center + new Vector3(width * 0.42f, height * 0.42f, 0f), new Vector3(width * 0.18f, height * 0.7f, 0.07f), role);
        }

        private static void AddSocket(MeshDraft draft, Vector3 center, SurfaceRole role)
        {
            AddTank(draft, center, 0.115f, 0.18f, Axis.Z, 12, role);
            AddBox(draft, center + new Vector3(0f, -0.11f, 0f), new Vector3(0.22f, 0.08f, 0.16f), SurfaceRole.Rubber);
        }

        private static void AddSkid(MeshDraft draft, Vector3 center, float length, SurfaceRole role)
        {
            AddBox(draft, center, new Vector3(length, 0.055f, 0.075f), role);
        }

        private static void AddViewportPlane(MeshDraft draft, Vector3 center, Vector2 size, SurfaceRole role)
        {
            int start = draft.VertexCount;
            Vector3 normal = Vector3.back;
            Color32 color = RoleColor(role);
            draft.AddVertex(center + new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f), normal, new Vector2(0f, 0f), color);
            draft.AddVertex(center + new Vector3(size.x * 0.5f, -size.y * 0.5f, 0f), normal, new Vector2(1f, 0f), color);
            draft.AddVertex(center + new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), normal, new Vector2(0f, 1f), color);
            draft.AddVertex(center + new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), normal, new Vector2(1f, 1f), color);
            draft.AddTriangle(start, start + 2, start + 1);
            draft.AddTriangle(start + 1, start + 2, start + 3);
        }

        private static void AddFin(MeshDraft draft, Vector3 root, Vector3 tip, SurfaceRole role)
        {
            Vector3 dir = (tip - root).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < Epsilon)
                side = Vector3.right;
            side.Normalize();

            float thickness = 0.035f;
            Vector3 p0 = root - side * 0.16f;
            Vector3 p1 = root + side * 0.16f;
            Vector3 p2 = tip;
            Vector3 p3 = p0 + Vector3.up * thickness;
            Vector3 p4 = p1 + Vector3.up * thickness;
            Vector3 p5 = p2 + Vector3.up * thickness;
            AddTriPrism(draft, p0, p1, p2, p3, p4, p5, role);
        }

        private static void AddTriPrism(MeshDraft draft, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, SurfaceRole role)
        {
            int start = draft.VertexCount;
            Color32 color = RoleColor(role);
            Vector3 frontNormal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            Vector3 backNormal = -frontNormal;
            draft.AddVertex(p0, frontNormal, new Vector2(0f, 0f), color);
            draft.AddVertex(p1, frontNormal, new Vector2(1f, 0f), color);
            draft.AddVertex(p2, frontNormal, new Vector2(0.5f, 1f), color);
            draft.AddVertex(p3, backNormal, new Vector2(0f, 0f), color);
            draft.AddVertex(p4, backNormal, new Vector2(1f, 0f), color);
            draft.AddVertex(p5, backNormal, new Vector2(0.5f, 1f), color);
            draft.AddTriangle(start, start + 1, start + 2);
            draft.AddTriangle(start + 3, start + 5, start + 4);
            draft.AddTriangle(start, start + 3, start + 1);
            draft.AddTriangle(start + 1, start + 3, start + 4);
            draft.AddTriangle(start + 1, start + 4, start + 2);
            draft.AddTriangle(start + 2, start + 4, start + 5);
            draft.AddTriangle(start + 2, start + 5, start);
            draft.AddTriangle(start, start + 5, start + 3);
        }

        private static void AddBox(MeshDraft draft, Vector3 center, Vector3 size, SurfaceRole role)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                center + new Vector3(-h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(h.x, h.y, h.z),
                center + new Vector3(-h.x, h.y, h.z)
            };
            AddQuad(draft, p[0], p[4], p[5], p[1], Vector3.back, role);
            AddQuad(draft, p[2], p[6], p[7], p[3], Vector3.forward, role);
            AddQuad(draft, p[0], p[3], p[7], p[4], Vector3.left, role);
            AddQuad(draft, p[1], p[5], p[6], p[2], Vector3.right, role);
            AddQuad(draft, p[4], p[7], p[6], p[5], Vector3.up, role);
            AddQuad(draft, p[0], p[1], p[2], p[3], Vector3.down, role);
        }

        private static void AddCap(MeshDraft draft, Vector3 center, Axis axis, float sign, float radius, int segments, SurfaceRole role)
        {
            int centerIndex = draft.VertexCount;
            Vector3 normal = AxisVector(axis, sign).normalized;
            draft.AddVertex(center, normal, new Vector2(0.5f, 0.5f), RoleColor(role));
            int ringStart = draft.VertexCount;
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                Vector3 local = OrientRadial(axis, new Vector3(0f, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius));
                draft.AddVertex(center + local, normal, new Vector2(Mathf.Sin(a) * 0.5f + 0.5f, Mathf.Cos(a) * 0.5f + 0.5f), RoleColor(role));
            }

            for (int s = 0; s < segments; s++)
            {
                int a = ringStart + s;
                int b = ringStart + ((s + 1) % segments);
                if (sign < 0f)
                    draft.AddTriangle(centerIndex, b, a);
                else
                    draft.AddTriangle(centerIndex, a, b);
            }
        }

        private static void AddQuad(MeshDraft draft, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, SurfaceRole role)
        {
            int start = draft.VertexCount;
            Color32 color = RoleColor(role);
            draft.AddVertex(a, normal, new Vector2(0f, 0f), color);
            draft.AddVertex(b, normal, new Vector2(0f, 1f), color);
            draft.AddVertex(c, normal, new Vector2(1f, 1f), color);
            draft.AddVertex(d, normal, new Vector2(1f, 0f), color);
            draft.AddTriangle(start, start + 1, start + 2);
            draft.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector3 AxisVector(Axis axis, float value)
        {
            switch (axis)
            {
                case Axis.X:
                    return new Vector3(value, 0f, 0f);
                case Axis.Y:
                    return new Vector3(0f, value, 0f);
                default:
                    return new Vector3(0f, 0f, value);
            }
        }

        private static Vector3 OrientRadial(Axis axis, Vector3 radial)
        {
            switch (axis)
            {
                case Axis.X:
                    return new Vector3(0f, radial.y, radial.z);
                case Axis.Y:
                    return new Vector3(radial.y, 0f, radial.z);
                default:
                    return new Vector3(radial.y, radial.z, 0f);
            }
        }

        private static Color32 RoleColor(SurfaceRole role)
        {
            switch (role)
            {
                case SurfaceRole.Hull:
                    return new Color32(58, 128, 185, 255);
                case SurfaceRole.Rubber:
                    return new Color32(18, 36, 44, 255);
                case SurfaceRole.Glass:
                    return new Color32(42, 160, 190, 210);
                case SurfaceRole.Trim:
                    return new Color32(214, 145, 52, 255);
                case SurfaceRole.Clearance:
                    return new Color32(8, 0, 0, 0);
                default:
                    return new Color32(255, 255, 255, 255);
            }
        }

        private static float ClampQuality(float q)
        {
            return IsFinite(q) ? Mathf.Clamp01(q) : 0f;
        }

        private static int ResolveSegments(float q)
        {
            float smooth = q * q * (3f - 2f * q);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(12f, 28f, smooth)), 12, 28);
        }

        private static int ResolveDetailCount(float q)
        {
            float smooth = q * q * (3f - 2f * q);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 7f, smooth)), 2, 7);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Output folder must be under Assets: " + folder);

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private enum TransportKind
        {
            CargoSled,
            ExosuitFrame,
            MicroSub,
            ScoutGlider
        }

        private enum SurfaceRole
        {
            Hull,
            Rubber,
            Glass,
            Trim,
            Clearance
        }

        private enum Axis
        {
            X,
            Y,
            Z
        }

        private struct TransportSpec
        {
            public TransportKind Kind;
            public string TransportId;
            public int Segments;
            public int DetailCount;
            public Vector3 RequiredAspect;
            public Vector3 RiderClearanceCenter;
            public Vector3 RiderClearanceSize;
            public Vector3 DismountClearanceCenter;
            public Vector3 DismountClearanceSize;

            public static TransportSpec CreateCargoSled(float quality)
            {
                return new TransportSpec
                {
                    Kind = TransportKind.CargoSled,
                    TransportId = "CargoSled",
                    Segments = ResolveSegments(quality),
                    DetailCount = ResolveDetailCount(quality),
                    RequiredAspect = new Vector3(2.35f, 0.42f, 1.05f),
                    RiderClearanceCenter = new Vector3(0f, 0.34f, 0f),
                    RiderClearanceSize = new Vector3(0.74f, 0.34f, 0.58f),
                    DismountClearanceCenter = new Vector3(1.6f, 0.18f, 0f),
                    DismountClearanceSize = new Vector3(0.52f, 0.32f, 0.52f)
                };
            }

            public static TransportSpec CreateExosuitFrame(float quality)
            {
                return new TransportSpec
                {
                    Kind = TransportKind.ExosuitFrame,
                    TransportId = "ExosuitFrame",
                    Segments = ResolveSegments(quality),
                    DetailCount = ResolveDetailCount(quality),
                    RequiredAspect = new Vector3(0.95f, 2.05f, 0.8f),
                    RiderClearanceCenter = new Vector3(0f, 0.88f, -0.08f),
                    RiderClearanceSize = new Vector3(0.46f, 0.9f, 0.38f),
                    DismountClearanceCenter = new Vector3(1.3f, 0.24f, 0f),
                    DismountClearanceSize = new Vector3(0.5f, 0.42f, 0.5f)
                };
            }

            public static TransportSpec CreateMicroSub(float quality)
            {
                return new TransportSpec
                {
                    Kind = TransportKind.MicroSub,
                    TransportId = "MicroSub",
                    Segments = ResolveSegments(quality),
                    DetailCount = ResolveDetailCount(quality),
                    RequiredAspect = new Vector3(3.55f, 1.18f, 1.75f),
                    RiderClearanceCenter = new Vector3(0f, 0.46f, 0f),
                    RiderClearanceSize = new Vector3(0.72f, 0.42f, 0.62f),
                    DismountClearanceCenter = new Vector3(2.4f, 0.22f, 0f),
                    DismountClearanceSize = new Vector3(0.58f, 0.42f, 0.58f)
                };
            }

            public static TransportSpec CreateScoutGlider(float quality)
            {
                return new TransportSpec
                {
                    Kind = TransportKind.ScoutGlider,
                    TransportId = "ScoutGlider",
                    Segments = ResolveSegments(quality),
                    DetailCount = ResolveDetailCount(quality),
                    RequiredAspect = new Vector3(1.8f, 0.46f, 1.65f),
                    RiderClearanceCenter = new Vector3(0f, 0.25f, 0f),
                    RiderClearanceSize = new Vector3(0.62f, 0.28f, 0.48f),
                    DismountClearanceCenter = new Vector3(1.6f, 0.16f, 0f),
                    DismountClearanceSize = new Vector3(0.46f, 0.3f, 0.46f)
                };
            }
        }

        private sealed class MeshDraft
        {
            private readonly List<Vector3> _vertices = new List<Vector3>(1024);
            private readonly List<Vector3> _normals = new List<Vector3>(1024);
            private readonly List<Vector2> _uv0 = new List<Vector2>(1024);
            private readonly List<Color32> _colors = new List<Color32>(1024);
            private readonly List<int> _indices = new List<int>(4096);

            public MeshDraft(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public int VertexCount { get { return _vertices.Count; } }
            public int IndexCount { get { return _indices.Count; } }
            public IList<Vector3> Vertices { get { return _vertices; } }
            public IList<int> Indices { get { return _indices; } }

            public void AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Color32 color)
            {
                _vertices.Add(position);
                _normals.Add(normal.sqrMagnitude > Epsilon ? normal.normalized : Vector3.up);
                _uv0.Add(uv);
                _colors.Add(color);
            }

            public void AddTriangle(int a, int b, int c)
            {
                _indices.Add(a);
                _indices.Add(b);
                _indices.Add(c);
            }

            public Mesh ToMesh(string meshName)
            {
                Mesh mesh = new Mesh();
                mesh.name = meshName;
                mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uv0);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_indices, 0, true);
                mesh.RecalculateTangents();
                mesh.bounds = ResolveBounds();
                return mesh;
            }

            private Bounds ResolveBounds()
            {
                if (_vertices.Count == 0)
                    return new Bounds(Vector3.zero, Vector3.zero);

                Bounds bounds = new Bounds(_vertices[0], Vector3.zero);
                for (int i = 1; i < _vertices.Count; i++)
                    bounds.Encapsulate(_vertices[i]);
                return bounds;
            }
        }

        private static class MeshValidation
        {
            public static void ValidateDraft(TransportSpec spec, MeshDraft draft)
            {
                ValidateClearanceIntent(spec);

                if (draft.VertexCount <= 0)
                    throw new InvalidOperationException(spec.TransportId + " produced no vertices.");
                if (draft.IndexCount <= 0 || draft.IndexCount % 3 != 0)
                    throw new InvalidOperationException(spec.TransportId + " produced invalid triangle index count.");

                for (int i = 0; i < draft.VertexCount; i++)
                {
                    if (!IsFinite(draft.Vertices[i]))
                        throw new InvalidOperationException(spec.TransportId + " has non-finite vertex at " + i);
                }

                for (int i = 0; i < draft.IndexCount; i += 3)
                {
                    int a = draft.Indices[i];
                    int b = draft.Indices[i + 1];
                    int c = draft.Indices[i + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= draft.VertexCount || b >= draft.VertexCount || c >= draft.VertexCount)
                        throw new InvalidOperationException(spec.TransportId + " has index outside vertex range.");

                    Vector3 ab = draft.Vertices[b] - draft.Vertices[a];
                    Vector3 ac = draft.Vertices[c] - draft.Vertices[a];
                    if (Vector3.Cross(ab, ac).sqrMagnitude <= Epsilon)
                        throw new InvalidOperationException(spec.TransportId + " has degenerate triangle at index " + i);
                }

                Bounds bounds = ResolveBounds(draft.Vertices);
                ValidateBounds(spec, bounds);
                ValidateDistinctSilhouette(spec, bounds);
            }

            public static void ValidateMesh(TransportSpec spec, Mesh mesh)
            {
                ValidateClearanceIntent(spec);

                if (mesh == null)
                    throw new InvalidOperationException(spec.TransportId + " mesh is null.");
                if (mesh.vertexCount <= 0)
                    throw new InvalidOperationException(spec.TransportId + " saved mesh has no vertices.");
                if (mesh.GetIndexCount(0) <= 0 || mesh.GetIndexCount(0) % 3u != 0u)
                    throw new InvalidOperationException(spec.TransportId + " saved mesh has invalid indices.");
                ValidateBounds(spec, mesh.bounds);
                ValidateDistinctSilhouette(spec, mesh.bounds);
            }

            public static void ValidateClearanceIntent(TransportSpec spec)
            {
                if (!IsFinite(spec.RiderClearanceCenter) || !IsFinite(spec.RiderClearanceSize))
                    throw new InvalidOperationException(spec.TransportId + " rider clearance intent is non-finite.");
                if (!IsFinite(spec.DismountClearanceCenter) || !IsFinite(spec.DismountClearanceSize))
                    throw new InvalidOperationException(spec.TransportId + " dismount clearance intent is non-finite.");
                if (spec.RiderClearanceSize.x <= 0f || spec.RiderClearanceSize.y <= 0f || spec.RiderClearanceSize.z <= 0f)
                    throw new InvalidOperationException(spec.TransportId + " rider clearance intent has invalid size.");
                if (spec.DismountClearanceSize.x <= 0f || spec.DismountClearanceSize.y <= 0f || spec.DismountClearanceSize.z <= 0f)
                    throw new InvalidOperationException(spec.TransportId + " dismount clearance intent has invalid size.");
            }

            private static Bounds ResolveBounds(IList<Vector3> vertices)
            {
                Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                for (int i = 1; i < vertices.Count; i++)
                    bounds.Encapsulate(vertices[i]);
                return bounds;
            }

            private static void ValidateBounds(TransportSpec spec, Bounds bounds)
            {
                if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                    throw new InvalidOperationException(spec.TransportId + " bounds are non-finite.");
                if (bounds.size.x <= 0.05f || bounds.size.y <= 0.05f || bounds.size.z <= 0.05f)
                    throw new InvalidOperationException(spec.TransportId + " bounds are too small.");
            }

            private static void ValidateDistinctSilhouette(TransportSpec spec, Bounds bounds)
            {
                Vector3 size = bounds.size;
                Vector3 actual = new Vector3(
                    size.x / Mathf.Max(0.01f, size.y),
                    size.y / Mathf.Max(0.01f, size.z),
                    size.z / Mathf.Max(0.01f, size.x));
                Vector3 required = new Vector3(
                    spec.RequiredAspect.x / Mathf.Max(0.01f, spec.RequiredAspect.y),
                    spec.RequiredAspect.y / Mathf.Max(0.01f, spec.RequiredAspect.z),
                    spec.RequiredAspect.z / Mathf.Max(0.01f, spec.RequiredAspect.x));
                float delta = Mathf.Abs(actual.x - required.x) + Mathf.Abs(actual.y - required.y) + Mathf.Abs(actual.z - required.z);
                if (delta > 3.25f)
                    throw new InvalidOperationException(spec.TransportId + " silhouette ratio drifted too far from source spec.");
            }

            private static bool IsFinite(Vector3 value)
            {
                return ProductFaceTransportMeshSourceAuthoring.IsFinite(value.x) &&
                       ProductFaceTransportMeshSourceAuthoring.IsFinite(value.y) &&
                       ProductFaceTransportMeshSourceAuthoring.IsFinite(value.z);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

#endif
