#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.GeographySanity
{
    [InitializeOnLoad]
    internal static class GeographySanityAnomalySceneView
    {
        private const string ReportPath = "Docs/Reports/GEOGRAPHY_SANITY_REPORT.json";
        private const int MaxRecords = 4096;
        private const byte TypeUnknown = 0;
        private const byte TypeFatal = 1;
        private const byte TypeBuried = 2;
        private const byte TypeFloating = 3;
        private const byte TypeNavigation = 4;
        private const byte TypeCrush = 5;
        private static readonly SceneRecord[] Records = new SceneRecord[MaxRecords];
        private static DateTime _lastReadUtc;
        private static long _lastLength;
        private static int _recordCount;
        private static bool _enabled = true;

        static GeographySanityAnomalySceneView()
        {
            SceneView.duringSceneGui -= DrawSceneOverlay;
            SceneView.duringSceneGui += DrawSceneOverlay;
        }

        [MenuItem("Tools/Hecton8/World Sanity Checker/SceneView Overlay/Toggle")]
        public static void ToggleOverlay()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Hecton8/World Sanity Checker/SceneView Overlay/Reload Report")]
        public static void ReloadReport()
        {
            _lastLength = -1L;
            ReloadIfChanged();
            SceneView.RepaintAll();
        }

        private static void DrawSceneOverlay(SceneView sceneView)
        {
            if (!_enabled)
                return;

            ReloadIfChanged();
            if (_recordCount == 0)
                return;

            CompareFunction previousZTest = Handles.zTest;
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.zTest = CompareFunction.LessEqual;
            Vector3 pivot = sceneView.pivot;
            Handles.matrix = Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one);
            Color previousColor = Handles.color;
            float pulse = 1f + Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((float)EditorApplication.timeSinceStartup * 3.0f) * 0.15f;
            float radius = 3.0f * pulse;
            for (int i = 0; i < _recordCount; i++)
            {
                SceneRecord record = Records[i];
                Vector3 position = ToPivotLocal(record, pivot);
                Handles.color = ResolveColor(record.TypeCode);
                Handles.DrawWireDisc(position, Vector3.up, radius);
                Handles.DrawWireDisc(position, Vector3.right, radius);
                Handles.DrawWireDisc(position, Vector3.forward, radius);
                Handles.Label(position + Vector3.up * (4.0f * pulse), ResolveLabel(record.TypeCode));
            }

            Handles.color = previousColor;
            Handles.matrix = previousMatrix;
            Handles.zTest = previousZTest;
        }

        private static void ReloadIfChanged()
        {
            string path = Path.Combine(ResolveProjectRoot(), ReportPath);
            if (!File.Exists(path))
            {
                _recordCount = 0;
                return;
            }

            FileInfo info = new FileInfo(path);
            if (info.LastWriteTimeUtc == _lastReadUtc && info.Length == _lastLength)
                return;

            _lastReadUtc = info.LastWriteTimeUtc;
            _lastLength = info.Length;
            _recordCount = 0;

            using (StreamReader reader = new StreamReader(path))
            {
                byte typeCode = TypeUnknown;
                bool hasType = false;
                bool hasAup = false;
                double x = 0.0;
                double y = 0.0;
                double z = 0.0;
                string line;
                while (_recordCount < MaxRecords && (line = reader.ReadLine()) != null)
                {
                    int typeIndex = line.IndexOf("\"type\"", StringComparison.Ordinal);
                    if (typeIndex >= 0)
                    {
                        typeCode = ExtractTypeCode(line, typeIndex);
                        hasType = true;
                    }

                    int aupIndex = line.IndexOf("\"aup\"", StringComparison.Ordinal);
                    if (aupIndex >= 0 &&
                        TryExtractNumber(line, "\"x\"", aupIndex, line.Length, out x) &&
                        TryExtractNumber(line, "\"y\"", aupIndex, line.Length, out y) &&
                        TryExtractNumber(line, "\"z\"", aupIndex, line.Length, out z))
                    {
                        hasAup = true;
                    }

                    if (line.IndexOf("    }", StringComparison.Ordinal) < 0)
                        continue;

                    if (hasType && hasAup)
                    {
                        Records[_recordCount++] = new SceneRecord
                        {
                            TypeCode = typeCode,
                            X = x,
                            Y = y,
                            Z = z
                        };
                    }

                    typeCode = TypeUnknown;
                    hasType = false;
                    hasAup = false;
                }
            }
        }

        private static Vector3 ToPivotLocal(SceneRecord record, Vector3 pivot)
        {
            double dx = record.X - pivot.x;
            double dy = record.Y - pivot.y;
            double dz = record.Z - pivot.z;
            return new Vector3((float)dx, (float)dy, (float)dz);
        }

        private static byte ResolveTypeCode(ReadOnlySpan<char> type)
        {
            if (ContainsAsciiIgnoreCase(type, "FATAL".AsSpan()))
                return TypeFatal;

            if (ContainsAsciiIgnoreCase(type, "BURIED".AsSpan()))
                return TypeBuried;

            if (ContainsAsciiIgnoreCase(type, "FLOATING".AsSpan()))
                return TypeFloating;

            if (ContainsAsciiIgnoreCase(type, "NAV".AsSpan()))
                return TypeNavigation;

            if (ContainsAsciiIgnoreCase(type, "CRUSH".AsSpan()))
                return TypeCrush;

            return TypeUnknown;
        }

        private static bool ContainsAsciiIgnoreCase(ReadOnlySpan<char> source, ReadOnlySpan<char> token)
        {
            if (token.Length == 0 || source.Length < token.Length)
                return false;

            int limit = source.Length - token.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < token.Length; j++)
                {
                    if (ToUpperAscii(source[i + j]) != ToUpperAscii(token[j]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        private static char ToUpperAscii(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private static string ResolveLabel(byte typeCode)
        {
            switch (typeCode)
            {
                case TypeFatal: return "WORLD_SANITY: FATAL_MATH_ERROR";
                case TypeBuried: return "WORLD_SANITY: BURIED";
                case TypeFloating: return "WORLD_SANITY: FLOATING";
                case TypeNavigation: return "WORLD_SANITY: NAVIGATIONAL_TRAP";
                case TypeCrush: return "WORLD_SANITY: CRUSH_DEPTH";
                default: return "WORLD_SANITY: UNKNOWN";
            }
        }

        private static Color ResolveColor(byte typeCode)
        {
            switch (typeCode)
            {
                case TypeFatal: return new Color(1.0f, 0.0f, 0.75f, 1.0f);
                case TypeBuried: return new Color(1.0f, 0.22f, 0.05f, 1.0f);
                case TypeFloating: return new Color(0.2f, 0.75f, 1.0f, 1.0f);
                case TypeNavigation: return new Color(1.0f, 0.86f, 0.1f, 1.0f);
                case TypeCrush: return new Color(1.0f, 0.4f, 0.1f, 1.0f);
                default: return Color.red;
            }
        }

        private static byte ExtractTypeCode(string text, int keyIndex)
        {
            int colon = text.IndexOf(':', keyIndex);
            int quote = colon >= 0 ? text.IndexOf('"', colon + 1) : -1;
            int end = quote >= 0 ? text.IndexOf('"', quote + 1) : -1;
            return quote >= 0 && end > quote ? ResolveTypeCode(text.AsSpan(quote + 1, end - quote - 1)) : TypeUnknown;
        }

        private static bool TryExtractNumber(string text, string key, int start, int end, out double value)
        {
            value = 0.0;
            int keyIndex = text.IndexOf(key, start, end - start, StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int colon = text.IndexOf(':', keyIndex);
            if (colon < 0 || colon >= end)
                return false;

            int numberStart = colon + 1;
            while (numberStart < end && char.IsWhiteSpace(text[numberStart]))
                numberStart++;

            int numberEnd = numberStart;
            while (numberEnd < end)
            {
                char c = text[numberEnd];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                {
                    numberEnd++;
                    continue;
                }

                break;
            }

            return numberEnd > numberStart &&
                   double.TryParse(text.AsSpan(numberStart, numberEnd - numberStart), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }

        private struct SceneRecord
        {
            public byte TypeCode;
            public double X;
            public double Y;
            public double Z;
        }
    }
}
#endif
