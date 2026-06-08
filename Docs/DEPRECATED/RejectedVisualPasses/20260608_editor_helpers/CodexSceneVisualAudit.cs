#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class CodexSceneVisualAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string ReportRelativePath = "Docs/AgentLogs/CODEX_SCENE_VISUAL_AUDIT_20260608.txt";

        public static void AuditAndExit()
        {
            int exitCode = 0;
            try
            {
                AuditInternal();
            }
            catch (Exception exception)
            {
                exitCode = 1;
                WriteFailure(exception);
                Debug.LogException(exception);
            }

            EditorApplication.Exit(exitCode);
        }

        private static void AuditInternal()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Bounds legacyOriginBounds = new Bounds(new Vector3(0f, 7f, 88f), new Vector3(260f, 120f, 260f));
            Bounds codexBounds = new Bounds(new Vector3(2600f, -8f, 2600f), new Vector3(560f, 160f, 560f));

            List<RendererRecord> legacy = Collect(scene, legacyOriginBounds);
            List<RendererRecord> codex = Collect(scene, codexBounds);

            StringBuilder builder = new StringBuilder(32768);
            builder.AppendLine("status=OK");
            builder.AppendLine("date=2026-06-08");
            builder.AppendLine("scene=" + ScenePath);
            builder.AppendLine("legacyOriginBounds=center(0,7,88) size(260,120,260)");
            builder.AppendLine("codexBounds=center(2600,-8,2600) size(560,160,560)");
            AppendRecords(builder, "legacy_origin_active_renderers", legacy, 160);
            AppendRecords(builder, "codex_focus_active_renderers", codex, 160);

            string reportPath = AbsoluteProjectPath(ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
            Debug.Log("[CodexSceneVisualAudit] Wrote " + ReportRelativePath + ", legacy=" + legacy.Count.ToString(CultureInfo.InvariantCulture) + ", codex=" + codex.Count.ToString(CultureInfo.InvariantCulture));
        }

        private static List<RendererRecord> Collect(Scene scene, Bounds bounds)
        {
            List<RendererRecord> records = new List<RendererRecord>(256);
            MeshRenderer[] renderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject == null)
                    continue;

                GameObject gameObject = renderer.gameObject;
                if (gameObject.scene != scene || EditorUtility.IsPersistent(gameObject))
                    continue;

                if (!gameObject.activeInHierarchy || !renderer.enabled)
                    continue;

                if (!bounds.Intersects(renderer.bounds))
                    continue;

                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                Material material = renderer.sharedMaterial;
                records.Add(new RendererRecord
                {
                    Name = gameObject.name,
                    Root = ResolveRootName(gameObject.transform),
                    Mesh = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "(none)",
                    Material = material != null ? material.name : "(none)",
                    Shader = material != null && material.shader != null ? material.shader.name : "(none)",
                    Center = renderer.bounds.center,
                    Size = renderer.bounds.size,
                    Magnitude = renderer.bounds.size.sqrMagnitude,
                });
            }

            records.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));
            return records;
        }

        private static void AppendRecords(StringBuilder builder, string section, List<RendererRecord> records, int limit)
        {
            builder.AppendLine();
            builder.AppendLine("[" + section + "]");
            builder.AppendLine("count=" + records.Count.ToString(CultureInfo.InvariantCulture));

            int safeLimit = Mathf.Min(limit, records.Count);
            for (int i = 0; i < safeLimit; i++)
            {
                RendererRecord record = records[i];
                builder.Append(i.ToString("000", CultureInfo.InvariantCulture));
                builder.Append(" root=");
                builder.Append(record.Root);
                builder.Append(" name=");
                builder.Append(record.Name);
                builder.Append(" mesh=");
                builder.Append(record.Mesh);
                builder.Append(" mat=");
                builder.Append(record.Material);
                builder.Append(" shader=");
                builder.Append(record.Shader);
                builder.Append(" center=");
                AppendVector(builder, record.Center);
                builder.Append(" size=");
                AppendVector(builder, record.Size);
                builder.AppendLine();
            }
        }

        private static string ResolveRootName(Transform transform)
        {
            Transform current = transform;
            while (current != null && current.parent != null)
                current = current.parent;

            return current != null ? current.name : "(none)";
        }

        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            builder.Append('(');
            builder.Append(value.x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(value.y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(value.z.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(')');
        }

        private static void WriteFailure(Exception exception)
        {
            string reportPath = AbsoluteProjectPath(ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, "status=FAILED\nexception=" + exception + "\n", Encoding.UTF8);
        }

        private static string AbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root is unavailable.");

            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private struct RendererRecord
        {
            public string Name;
            public string Root;
            public string Mesh;
            public string Material;
            public string Shader;
            public Vector3 Center;
            public Vector3 Size;
            public float Magnitude;
        }
    }
}
#endif
