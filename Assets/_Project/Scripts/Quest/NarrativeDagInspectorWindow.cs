#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Quest
{
    /// <summary>
    /// Editor-only facade for translating unmanaged quest masks back to designer-readable nodes.
    /// </summary>
    public sealed class NarrativeDagInspectorWindow : EditorWindow
    {
        private const string WindowTitle = "Narrative DAG Inspector";
        private const string NodeNamesPath = "Data/Narrative/node_names.csv";
        private const string CsvOverridePath = QuestDagRuntimeConstants.DefaultCsvOverridePath;
        private QuestDagBufferHandles _handles;
        private Vector2 _scroll;
        private uint[] _nodeNameHashes = Array.Empty<uint>();
        private string[] _nodeNames = Array.Empty<string>();
        private bool _autoCsv;
        private double _nextCsvPollTime;

        [MenuItem("Hecton/Narrative/Narrative DAG Inspector")]
        private static void Open()
        {
            NarrativeDagInspectorWindow window = GetWindow<NarrativeDagInspectorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            LoadNodeNames();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not initialized. Run bootstrap or create a vault-backed test runtime.", MessageType.Warning);
                if (GUILayout.Button("Reload node_names.csv"))
                    LoadNodeNames();
                return;
            }

            if (!_handles.GlobalStateMasks.IsCreated)
                _handles = QuestDagVault.EnsureBuffers(vault);

            if (!QuestDagVault.TryResolveBuffers(vault, ref _handles, out QuestDagBuffers buffers))
            {
                EditorGUILayout.HelpBox("Quest DAG buffers are unavailable.", MessageType.Error);
                return;
            }

            DrawToolbar(vault, buffers);
            DrawTelemetry(buffers);
            DrawNodes(vault, buffers);
            PollCsvIfNeeded(vault);
        }

        private void DrawToolbar(IDataVault vault, in QuestDagBuffers buffers)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load OSHINO / Mock", GUILayout.Width(150f)))
                {
                    MockQuestDatabase.TryLoadOshinoOrGenerateMock(
                        vault,
                        ref _handles,
                        QuestDagRuntimeConstants.DefaultBinaryPath,
                        out _);
                }

                if (GUILayout.Button("Apply CSV", GUILayout.Width(100f)))
                {
                    QuestDagCsvOverrideIngestor.TryApplyOverridesFromFile(
                        vault,
                        ref _handles,
                        CsvOverridePath,
                        out _);
                }

                _autoCsv = GUILayout.Toggle(_autoCsv, "Auto CSV", EditorStyles.toolbarButton, GUILayout.Width(90f));

                if (GUILayout.Button("Reload Names", GUILayout.Width(110f)))
                    LoadNodeNames();
            }

            int nodeCount = ReadCounter(buffers, QuestDagRuntimeConstants.CounterSlot.NodeCount);
            int triggerCount = ReadCounter(buffers, QuestDagRuntimeConstants.CounterSlot.TriggerCount);
            EditorGUILayout.LabelField("Nodes", nodeCount.ToString());
            EditorGUILayout.LabelField("Triggers", triggerCount.ToString());
        }

        private void DrawTelemetry(in QuestDagBuffers buffers)
        {
            if (!buffers.TelemetryRing.IsCreated || !buffers.TelemetryCursor.IsCreated || buffers.TelemetryRing.Length <= 0)
                return;

            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor += buffers.TelemetryRing.Length;

            QuestDagTelemetryEntry entry = buffers.TelemetryRing[cursor % buffers.TelemetryRing.Length];
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Last Evaluated", entry.ActiveNodesEvaluated.ToString());
            EditorGUILayout.LabelField("Last Bits", "0x" + entry.BitsFlipped.ToString("X16"));
            EditorGUILayout.LabelField("Last Iterations", entry.Iterations.ToString());
            EditorGUILayout.LabelField("Last Compute ms", entry.ResolverComputeTimeMs.ToString("F6"));
        }

        private void DrawNodes(IDataVault vault, in QuestDagBuffers buffers)
        {
            int nodeCount = math.min(
                ReadCounter(buffers, QuestDagRuntimeConstants.CounterSlot.NodeCount),
                math.min(buffers.Nodes.Length, buffers.NodeRuntime.Length));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < nodeCount; i++)
            {
                QuestNodeDTO node = buffers.Nodes[i];
                QuestNodeRuntimeDTO runtime = buffers.NodeRuntime[i];
                if (node.NodeHash == 0u || (uint)runtime.StateChunk >= (uint)buffers.GlobalStateMasks.Length)
                    continue;

                ulong state = buffers.GlobalStateMasks[runtime.StateChunk];
                bool completed = (state & node.CompletionMask) == node.CompletionMask;
                bool active = !completed && (state & node.PrerequisiteMask) == node.PrerequisiteMask;
                string status = completed ? "COMPLETED" : active ? "ACTIVE" : "LOCKED";

                Rect row = EditorGUILayout.GetControlRect(false, 24f);
                Rect labelRect = new Rect(row.x, row.y, row.width - 120f, row.height);
                Rect buttonRect = new Rect(row.xMax - 110f, row.y, 110f, row.height);

                EditorGUI.LabelField(labelRect, ResolveNodeName(node.NodeHash), status);
                if (!completed && GUI.Button(buttonRect, "Force Complete"))
                {
                    QuestDagDebugApi.ForceCompleteNode(
                        vault,
                        ref _handles,
                        node.NodeHash,
                        unchecked((uint)Time.frameCount));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void PollCsvIfNeeded(IDataVault vault)
        {
            if (!_autoCsv || EditorApplication.timeSinceStartup < _nextCsvPollTime)
                return;

            _nextCsvPollTime = EditorApplication.timeSinceStartup + 0.5d;
            if (QuestDagCsvOverrideIngestor.TryApplyOverridesFromFile(vault, ref _handles, CsvOverridePath, out _))
                Repaint();
        }

        private void LoadNodeNames()
        {
            if (!File.Exists(NodeNamesPath))
            {
                _nodeNameHashes = Array.Empty<uint>();
                _nodeNames = Array.Empty<string>();
                return;
            }

            string[] lines = File.ReadAllLines(NodeNamesPath);
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]) && lines[i][0] != '#')
                    count++;
            }

            uint[] hashes = new uint[count];
            string[] names = new string[count];
            int write = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                    continue;

                int comma = line.IndexOf(',');
                if (comma <= 0)
                    continue;

                ReadOnlySpan<char> hashSpan = line.AsSpan(0, comma).Trim();
                if (!QuestDagCsvOverrideIngestorForEditor.TryParseUInt32Editor(hashSpan, out uint hash))
                    continue;

                hashes[write] = hash;
                names[write] = line.Substring(comma + 1).Trim();
                write++;
            }

            if (write != count)
            {
                Array.Resize(ref hashes, write);
                Array.Resize(ref names, write);
            }

            _nodeNameHashes = hashes;
            _nodeNames = names;
        }

        private string ResolveNodeName(uint nodeHash)
        {
            for (int i = 0; i < _nodeNameHashes.Length; i++)
            {
                if (_nodeNameHashes[i] == nodeHash)
                    return _nodeNames[i];
            }

            return "0x" + nodeHash.ToString("X8");
        }

        private static int ReadCounter(in QuestDagBuffers buffers, QuestDagRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return buffers.Counters.IsCreated && (uint)index < (uint)buffers.Counters.Length ? buffers.Counters[index] : 0;
        }
    }

    internal static class QuestDagCsvOverrideIngestorForEditor
    {
        public static bool TryParseUInt32Editor(ReadOnlySpan<char> text, out uint value)
        {
            value = 0u;
            if (text.Length <= 0)
                return false;

            int index = 0;
            bool hex = text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X');
            if (hex)
                index = 2;

            for (; index < text.Length; index++)
            {
                int digit = ParseDigit(text[index], hex);
                if (digit < 0)
                    return false;

                value = hex ? (value << 4) + (uint)digit : (value * 10u) + (uint)digit;
            }

            return true;
        }

        private static int ParseDigit(char c, bool hex)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (!hex)
                return -1;
            if (c >= 'a' && c <= 'f')
                return 10 + c - 'a';
            if (c >= 'A' && c <= 'F')
                return 10 + c - 'A';
            return -1;
        }
    }
}
#endif
