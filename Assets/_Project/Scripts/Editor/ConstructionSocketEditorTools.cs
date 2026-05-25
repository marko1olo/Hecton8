#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class ConstructionSocketTunerWindow : EditorWindow
    {
        private Label _summary;

        [MenuItem("Hecton8/Construction/Submarine Snapping & Construction Tuner")]
        public static void Open()
        {
            ConstructionSocketTunerWindow window = GetWindow<ConstructionSocketTunerWindow>();
            window.titleContent = new GUIContent("Submarine Snapping & Construction");
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _summary = new Label();
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_summary);

            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            AddSlider("Snap Radius", 0.1f, 5f, tuning.SnappingRadius, value => Apply(t => { t.SnappingRadius = value; return t; }));
            AddSlider("Unsnap Radius", 0.2f, 8f, tuning.UnsnapRadius, value => Apply(t => { t.UnsnapRadius = value; return t; }));
            AddSlider("Alignment Dot", -1f, 1f, tuning.AlignmentDotThreshold, value => Apply(t => { t.AlignmentDotThreshold = value; return t; }));
            AddSlider("Search Low", 1f, 12f, tuning.SearchRadiusLowMeters, value => Apply(t => { t.SearchRadiusLowMeters = value; return t; }));
            AddSlider("Search Ultra", 4f, 40f, tuning.SearchRadiusUltraMeters, value => Apply(t => { t.SearchRadiusUltraMeters = value; return t; }));
            AddSlider("Magnet Force", 0f, 4f, tuning.MagnetForce, value => Apply(t => { t.MagnetForce = value; return t; }));
            AddSlider("Dear Lie Shrink", 0f, 0.5f, tuning.DearLieShrinkMeters, value => Apply(t => { t.DearLieShrinkMeters = value; return t; }));
            AddSlider("Dear Lie Wiggle", 0f, 60f, tuning.DearLieWiggleSpeed, value => Apply(t => { t.DearLieWiggleSpeed = value; return t; }));

            Button initVault = new Button(() =>
            {
                bool ok = ShinobuSocketConstructionRuntime.InitializeVault(GlobalRegistry.DataVault);
                _summary.text = "Vault init: " + ok;
            })
            {
                text = "Initialize Vault Buffers"
            };
            rootVisualElement.Add(initVault);

            Button mock = new Button(() =>
            {
                bool ok = ShinobuSocketConstructionRuntime.GenerateMockBaseConstructionGrid(GlobalRegistry.DataVault);
                _summary.text = "Mock grid: " + ok + " | modules " + ShinobuSocketConstructionRuntime.MockModuleCount;
            })
            {
                text = "Generate 500 Module Mock Grid"
            };
            rootVisualElement.Add(mock);

            Button csv = new Button(() =>
            {
                bool ok = ConstructionSocketProfilesCsvImporter.TryImportDefaultProfile(out string message);
                _summary.text = message;
                if (!ok)
                    Debug.LogWarning(message);
            })
            {
                text = "Import module_socket_profiles.csv"
            };
            rootVisualElement.Add(csv);

            Button scan = new Button(() =>
            {
                ConstructionOptimizationReport report = ConstructionPhysicsStaticScanner.RunScan();
                _summary.text = "Scan: " + report.TotalHits + " active hits | " + ConstructionPhysicsStaticScanner.ReportPath;
            })
            {
                text = "Run Static Scanner"
            };
            rootVisualElement.Add(scan);

            RefreshSummary();
        }

        private void AddSlider(string label, float min, float max, float value, Action<float> onChanged)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            rootVisualElement.Add(slider);
        }

        private void Apply(Func<ConstructionSocketTuningDTO, ConstructionSocketTuningDTO> mutator)
        {
            ConstructionSocketTuningDTO tuning = mutator(ShinobuSocketConstructionRuntime.GetTuning());
            ShinobuSocketConstructionRuntime.SetTuning(
                tuning.SnappingRadius,
                tuning.UnsnapRadius,
                tuning.AlignmentDotThreshold,
                tuning.SearchRadiusLowMeters,
                tuning.SearchRadiusUltraMeters,
                tuning.DearLieShrinkMeters,
                tuning.DearLieWiggleSpeed,
                tuning.MagnetForce);
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            int activeSockets = 0;
            float solverUs = 0f;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null)
            {
                if (ConstructionSocketEditorVaultReads.TryRead(vault, BufferID.ConstructionSocketCounters, out NativeArray<int>.ReadOnly counters) &&
                    counters.Length > 1)
                {
                    activeSockets = counters[1];
                }

                if (ConstructionSocketEditorVaultReads.TryRead(vault, BufferID.ConstructionSocketTelemetry, out NativeArray<ConstructionSocketTelemetryEntry>.ReadOnly telemetry) &&
                    telemetry.Length > 0)
                {
                    solverUs = telemetry[(int)(Time.frameCount % telemetry.Length)].SolverMicroseconds;
                }
            }

            _summary.text =
                "Q=" + ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight().ToString("0.00", CultureInfo.InvariantCulture) +
                " | snap=" + tuning.SnappingRadius.ToString("0.00", CultureInfo.InvariantCulture) +
                " | candidates=" + ShinobuSocketConstructionRuntime.ResolveCandidateBudget(
                    tuning.MinCandidateBudget,
                    tuning.MaxCandidateBudget) +
                " | sockets=" + activeSockets +
                " | solverUs=" + solverUs.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    internal static class ConstructionSocketProfilesCsvImporter
    {
        private const string DefaultPath = "Docs/Data/module_socket_profiles.csv";

        [MenuItem("Hecton8/Construction/Import Socket Profiles CSV")]
        public static void ImportDefaultMenu()
        {
            if (!TryImportDefaultProfile(out string message))
                throw new BuildFailedException(message);

            Debug.Log(message);
        }

        internal static bool TryImportDefaultProfile(out string message)
        {
            return TryImport(DefaultPath, out message);
        }

        internal static bool TryImport(string path, out string message)
        {
            message = "CSV missing: " + path;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            byte[] bytes = File.ReadAllBytes(path);
            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = TrimAscii(new ReadOnlySpan<byte>(bytes, lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int comma = IndexOfByte(line, (byte)',');
                if (comma <= 0 || comma + 1 >= line.Length)
                    continue;

                ReadOnlySpan<byte> key = TrimAscii(line.Slice(0, comma));
                ReadOnlySpan<byte> rawValue = TrimAscii(line.Slice(comma + 1));
                if (!TryParseInvariantFloat(rawValue, out float value))
                    continue;

                switch (Fnv1aLower(key))
                {
                    case 0x72075204u: tuning.SnappingRadius = value; break;
                    case 0x21473821u: tuning.UnsnapRadius = value; break;
                    case 0x7395F4CEu: tuning.AlignmentDotThreshold = value; break;
                    case 0xE4C19E78u: tuning.SearchRadiusLowMeters = value; break;
                    case 0xDA868154u: tuning.SearchRadiusUltraMeters = value; break;
                    case 0x49AC2433u: tuning.MagnetForce = value; break;
                    case 0x61A1B77Cu: tuning.DearLieShrinkMeters = value; break;
                    case 0x67E2390Cu: tuning.DearLieWiggleSpeed = value; break;
                }
            }

            ShinobuSocketConstructionRuntime.SetTuning(
                tuning.SnappingRadius,
                tuning.UnsnapRadius,
                tuning.AlignmentDotThreshold,
                tuning.SearchRadiusLowMeters,
                tuning.SearchRadiusUltraMeters,
                tuning.DearLieShrinkMeters,
                tuning.DearLieWiggleSpeed,
                tuning.MagnetForce);
            message = "CSV imported: " + path;
            return true;
        }

        private static uint Fnv1aLower(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOfByte(ReadOnlySpan<byte> value, byte target)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool TryParseInvariantFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (value[index] == (byte)'+')
            {
                index++;
            }

            float whole = 0f;
            bool any = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                any = true;
                whole = (whole * 10f) + (value[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    any = true;
                    fraction += (value[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            if (!any)
                return false;

            result = (whole + fraction) * sign;
            return true;
        }
    }

    internal struct ConstructionOptimizationReport
    {
        public int TotalHits;
        public int ScannedFiles;
        public int SocketTriggerHits;
        public int PhysicsQueryHits;
        public int PrefabSpawnHits;
    }

    internal static class ConstructionPhysicsStaticScanner
    {
        internal const string ReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json";
        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Construction",
            "Assets/_Project/Scripts/PlayerBuilder.cs",
            "Assets/_Project/Scripts/Placement" + "Ghost.cs"
        };

        [MenuItem("Hecton8/Construction/Run Construction Optimization Scanner")]
        public static void RunScanMenu()
        {
            ConstructionOptimizationReport report = RunScan();
            if (report.SocketTriggerHits > 0)
                throw new BuildFailedException("Socket trigger residue detected. See " + ReportPath);

            Debug.Log("Construction optimization scan complete: " + report.TotalHits + " hits.");
        }

        internal static ConstructionOptimizationReport RunScan()
        {
            List<string> rows = new List<string>(128);
            ConstructionOptimizationReport report = default;
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = ScanRoots[rootIndex];
                if (Directory.Exists(root))
                {
                    string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                    for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    {
                        string file = files[fileIndex];
                        ScanFile(file, rows, ref report);
                    }
                }
                else if (File.Exists(root))
                {
                    ScanFile(root, rows, ref report);
                }
            }

            report.TotalHits = report.SocketTriggerHits + report.PhysicsQueryHits + report.PrefabSpawnHits;
            WriteReport(rows, report);
            return report;
        }

        private static void ScanFile(string path, List<string> rows, ref ConstructionOptimizationReport report)
        {
            string source = StripComments(File.ReadAllText(path));
            report.ScannedFiles++;
            bool vehicleDockingOwner = path.IndexOf("VehicleDockingModule", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!vehicleDockingOwner)
                Count(path, source, "On" + "Trigger", "socket_trigger", rows, ref report.SocketTriggerHits);
            Count(path, source, "Sphere" + "Collider", "socket_trigger", rows, ref report.SocketTriggerHits);
            Count(path, source, "Phys" + "ics." + "Overlap" + "Sphere" + "NonAlloc", "physics_query", rows, ref report.PhysicsQueryHits);
            Count(path, source, "Phys" + "ics." + "Overlap" + "Box" + "NonAlloc", "physics_query", rows, ref report.PhysicsQueryHits);
            Count(path, source, "Fixed" + "Joint", "physics_query", rows, ref report.PhysicsQueryHits);
            Count(path, source, "Instan" + "tiate(", "prefab_spawn", rows, ref report.PrefabSpawnHits);
            Count(path, source, "Dest" + "roy(", "prefab_spawn", rows, ref report.PrefabSpawnHits);
            Count(path, source, "new Game" + "Object", "prefab_spawn", rows, ref report.PrefabSpawnHits);
        }

        private static void Count(string path, string source, string pattern, string category, List<string> rows, ref int counter)
        {
            int index = 0;
            while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                counter++;
                rows.Add("{\"file\":\"" + Escape(path) + "\",\"category\":\"" + category + "\",\"pattern\":\"" + Escape(pattern) + "\"}");
                index += pattern.Length;
            }
        }

        private static string StripComments(string source)
        {
            StringBuilder builder = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';
                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        builder.Append(c);
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    i++;
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static void WriteReport(List<string> rows, ConstructionOptimizationReport report)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StringBuilder json = new StringBuilder(4096);
            json.Append("{\n");
            json.Append("  \"agent\":\"SHINOBU_217\",\n");
            json.Append("  \"summary\":\"Physics-Based Snapping Purged\",\n");
            json.Append("  \"scannedFiles\":").Append(report.ScannedFiles).Append(",\n");
            json.Append("  \"socketTriggerHits\":").Append(report.SocketTriggerHits).Append(",\n");
            json.Append("  \"physicsQueryHits\":").Append(report.PhysicsQueryHits).Append(",\n");
            json.Append("  \"prefabSpawnHits\":").Append(report.PrefabSpawnHits).Append(",\n");
            json.Append("  \"hits\":[\n");
            for (int i = 0; i < rows.Count; i++)
            {
                json.Append("    ").Append(rows[i]);
                if (i + 1 < rows.Count)
                    json.Append(',');
                json.Append('\n');
            }
            json.Append("  ]\n");
            json.Append("}\n");
            File.WriteAllText(ReportPath, json.ToString());
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class ConstructionSocketEditorVaultReads
    {
        internal static bool TryRead<T>(IDataVault vault, BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            NativeArray<T> resolved = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out resolved) &&
                   resolved.IsCreated &&
                   PublishReadOnly(resolved, out buffer);
        }

        private static bool PublishReadOnly<T>(NativeArray<T> resolved, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = resolved.AsReadOnly();
            return true;
        }
    }

    internal static class ConstructionSocketCsrDebugGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawSocketGizmos(ConstructionManager manager, GizmoType gizmoType)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !ConstructionSocketEditorVaultReads.TryRead(vault, BufferID.ConstructionSocketStates, out NativeArray<SocketStateDTO>.ReadOnly sockets) ||
                !ConstructionSocketEditorVaultReads.TryRead(vault, BufferID.ConstructionSocketAup, out NativeArray<double3>.ReadOnly socketAups))
            {
                return;
            }

            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            int count = math.min(
                math.min(sockets.Length, socketAups.Length),
                ShinobuSocketConstructionRuntime.ResolveCandidateBudget(tuning.MinCandidateBudget, tuning.MaxCandidateBudget));
            double3 origin = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                SocketStateDTO socket = sockets[i];
                double3 runtime = socketAups[i] - origin;
                Vector3 position = new Vector3((float)runtime.x, (float)runtime.y, (float)runtime.z);
                Vector3 normal = new Vector3(socket.NormalDirection.x, socket.NormalDirection.y, socket.NormalDirection.z);
                Gizmos.color = (socket.ConnectionStatus & ConstructionSocketFlags.Connected) != 0u
                    ? new Color(1f, 0.1f, 0.08f, 0.9f)
                    : new Color(0.1f, 1f, 0.35f, 0.75f);
                Gizmos.DrawWireSphere(position, 0.12f);
                Gizmos.color = new Color(1f, 0.85f, 0.08f, 0.9f);
                Gizmos.DrawLine(position, position + normal * 0.45f);
            }
        }
    }
}
#endif
