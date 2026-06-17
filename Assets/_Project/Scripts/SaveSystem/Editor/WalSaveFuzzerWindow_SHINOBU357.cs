#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.SaveSystem.Editor
{
    [InitializeOnLoad]
    internal static class WalFuzzStateLayoutGuard
    {
        static WalFuzzStateLayoutGuard()
        {
            if (UnsafeUtility.SizeOf<WalFuzzStateDTO>() != 32 ||
                UnsafeUtility.AlignOf<WalFuzzStateDTO>() != 4 ||
                (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.InterruptedByteOffset)) != 0 ||
                (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.FinalValidatedBytes)) != 4 ||
                (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.MismatchFlags)) != 8)
            {
                throw new FatalArchitectureException("SHINOBU_357 WalFuzzStateDTO layout violation: expected Size=32 Align=4 offsets 0/4/8.");
            }

            if (UnsafeUtility.SizeOf<WalFuzzTelemetryEntry>() != 64 ||
                UnsafeUtility.AlignOf<WalFuzzTelemetryEntry>() != 8 ||
                (int)Marshal.OffsetOf<WalFuzzTelemetryEntry>(nameof(WalFuzzTelemetryEntry.Frame)) != 0 ||
                (int)Marshal.OffsetOf<WalFuzzTelemetryEntry>(nameof(WalFuzzTelemetryEntry.PathHash)) != 16)
            {
                throw new FatalArchitectureException("SHINOBU_357 WalFuzzTelemetryEntry layout violation: expected Size=64 Align=8 offsets 0/16.");
            }

            if (UnsafeUtility.SizeOf<WalFuzzFileHandleStatusDTO>() != 64 ||
                UnsafeUtility.AlignOf<WalFuzzFileHandleStatusDTO>() != 8 ||
                (int)Marshal.OffsetOf<WalFuzzFileHandleStatusDTO>(nameof(WalFuzzFileHandleStatusDTO.PrimaryWritable)) != 0 ||
                (int)Marshal.OffsetOf<WalFuzzFileHandleStatusDTO>(nameof(WalFuzzFileHandleStatusDTO.FailureCode)) != 12)
            {
                throw new FatalArchitectureException("SHINOBU_357 WalFuzzFileHandleStatusDTO layout violation: expected Size=64 Align=8 offsets 0/12.");
            }
        }
    }

    public sealed class WalSaveFuzzerWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25d;
        private const int SummaryBufferCapacity = 192;
        private const string HexDigits = "0123456789ABCDEF";
        private Label _summary;
        private ProgressBar _progress;
        private WalFuzzGraphElement _graph;
        private readonly char[] _summaryBuffer = new char[SummaryBufferCapacity]; // COLD ALLOC: char[192] - editor summary formatting scratch - owner: WalSaveFuzzerWindow
        private WalFuzzStateDTO _lastState;
        private WalFuzzerResultDTO _lastResult;
        private IDataVault _dataVault;
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Save/WAL Save Fuzzer")]
        public static void Open()
        {
            GetWindow<WalSaveFuzzerWindow>("WAL Save Fuzzer");
        }

        public void CreateGUI()
        {
            _dataVault = GlobalRegistry.DataVault;
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _summary = new Label("WAL fuzzer telemetry unavailable.");
            _summary.style.marginBottom = 8f;
            root.Add(_summary);

            Button run = new Button(RunFuzzer) { text = "RUN 100 ITERATION WAL FUZZ TEST" };
            root.Add(run);

            Button scan = new Button(RunScanner) { text = "RUN OOP WAL FUZZ SCANNER" };
            scan.style.marginTop = 4f;
            root.Add(scan);

            _progress = new ProgressBar { title = "SHINOBU_357 WAL Fuzz", lowValue = 0f, highValue = 100f, value = 0f };
            _progress.style.marginTop = 6f;
            root.Add(_progress);

            _graph = new WalFuzzGraphElement(this);
            _graph.style.height = 140f;
            _graph.style.marginTop = 8f;
            root.Add(_graph);
        }

        private void OnEnable()
        {
            _dataVault = GlobalRegistry.DataVault;
            SceneView.duringSceneGui -= DrawFailureGizmo;
            SceneView.duringSceneGui += DrawFailureGizmo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFailureGizmo;
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshSeconds;
            RefreshSummary();
            if (_graph != null)
                _graph.MarkDirtyRepaint();
        }

        private void RunFuzzer()
        {
            WalFuzzerProfileDTO profile = WalIntegrityFuzzerCore.BuildShinobu357DefaultProfile();
            string root = System.IO.Path.Combine(Application.temporaryCachePath, "H8_SHINOBU_357_WAL");
            bool passed = WalIntegrityFuzzerCore.RunShinobu357PersistenceIntegrityFuzzer(root, in profile, out _lastState, out _lastResult);
            if (_progress != null)
                _progress.value = passed ? 100f : 0f;
            RefreshSummary();
            Repaint();
            SceneView.RepaintAll();
        }

        private void RunScanner()
        {
            bool passed = WalIntegrityFuzzerCore.RunOopWalFuzzScannerForProject(out OopWalFuzzScanResultDTO result);
            int write = 0;
            if (passed)
            {
                write = AppendLiteral(_summaryBuffer, write, "OOP Fuzzers Eradicated | files ");
                write = AppendUInt32(_summaryBuffer, write, result.FilesScanned);
                write = AppendLiteral(_summaryBuffer, write, " | cold FileStream refs ");
                write = AppendUInt32(_summaryBuffer, write, result.FileStreamFindings);
            }
            else
            {
                write = AppendLiteral(_summaryBuffer, write, "OOP WAL fuzz scanner failed | fatal findings ");
                write = AppendUInt32(_summaryBuffer, write, result.FatalFindings);
            }

            SetSummaryText(new string(_summaryBuffer, 0, write));
        }

        private void RefreshSummary()
        {
            if (_summary == null)
                return;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            if (!WalIntegrityFuzzerCore.TryReadShinobu357Telemetry(_dataVault, out NativeArray<WalFuzzTelemetryEntry>.ReadOnly telemetry, out int cursor))
            {
                SetSummaryText("WAL fuzzer telemetry ring is empty.");
                return;
            }

            int index = telemetry.Length == 0 ? 0 : math.clamp(cursor - 1, 0, telemetry.Length - 1);
            WalFuzzTelemetryEntry entry = telemetry[index];
            int write = 0;
            write = AppendLiteral(_summaryBuffer, write, "Frame ");
            write = AppendUInt32(_summaryBuffer, write, entry.Frame);
            write = AppendLiteral(_summaryBuffer, write, " | interrupted ");
            write = AppendUInt32(_summaryBuffer, write, entry.InterruptedByteOffset);
            write = AppendLiteral(_summaryBuffer, write, " | validated ");
            write = AppendUInt32(_summaryBuffer, write, entry.FinalValidatedBytes);
            write = AppendLiteral(_summaryBuffer, write, " | flags 0x");
            write = AppendHex32(_summaryBuffer, write, entry.MismatchFlags);
            SetSummaryText(new string(_summaryBuffer, 0, write));
        }

        private void DrawFailureGizmo(SceneView sceneView)
        {
            if (_lastState.MismatchFlags == 0u)
                return;

            Vector3 origin = new Vector3(-4f, 1f, 0f);
            Vector3 end = new Vector3(4f, 1f, 0f);
            float t = _lastState.InterruptedByteOffset > 0u && _lastState.FinalValidatedBytes > 0u
                ? math.saturate((float)_lastState.InterruptedByteOffset / math.max(1f, (float)_lastState.FinalValidatedBytes))
                : 0.5f;
            Vector3 fail = Vector3.Lerp(origin, end, t);
            Handles.color = Color.green;
            Handles.DrawLine(origin, end);
            Handles.color = Color.red;
            Handles.SphereHandleCap(0, fail, Quaternion.identity, 0.25f, EventType.Repaint);
            Handles.color = Color.yellow;
            Handles.ArrowHandleCap(0, fail, Quaternion.LookRotation(Vector3.right), 0.75f, EventType.Repaint);
        }

        private sealed class WalFuzzGraphElement : VisualElement
        {
            private readonly WalSaveFuzzerWindow _owner;

            public WalFuzzGraphElement(WalSaveFuzzerWindow owner)
            {
                _owner = owner;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.25f;
                if (_owner._dataVault == null)
                    _owner._dataVault = GlobalRegistry.DataVault;

                if (!WalIntegrityFuzzerCore.TryReadShinobu357Telemetry(_owner._dataVault, out NativeArray<WalFuzzTelemetryEntry>.ReadOnly telemetry, out _) ||
                    telemetry.Length == 0)
                {
                    painter.strokeColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.Stroke();
                    return;
                }

                painter.strokeColor = new Color(0.2f, 0.85f, 0.65f, 1f);
                painter.BeginPath();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    WalFuzzTelemetryEntry entry = telemetry[i];
                    float ratio = entry.FinalValidatedBytes > 0u
                        ? math.saturate((float)entry.InterruptedByteOffset / entry.FinalValidatedBytes)
                        : 0f;
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetry.Length - 1f));
                    float y = rect.yMax - rect.height * ratio;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();

                painter.strokeColor = new Color(1f, 0.2f, 0.1f, 0.95f);
                painter.BeginPath();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    WalFuzzTelemetryEntry entry = telemetry[i];
                    float y01 = entry.MismatchFlags == 0u ? 0f : 1f;
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetry.Length - 1f));
                    float y = rect.yMax - rect.height * y01;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }

        private void SetSummaryText(string text)
        {
            if (_summary != null && !string.Equals(_summary.text, text, StringComparison.Ordinal))
                _summary.text = text;
        }

        private static int AppendLiteral(char[] buffer, int cursor, string text)
        {
            int max = buffer.Length;
            for (int i = 0; i < text.Length && cursor < max; i++)
                buffer[cursor++] = text[i];
            return cursor;
        }

        private static int AppendUInt32(char[] buffer, int cursor, uint value)
        {
            Span<char> scratch = stackalloc char[10];
            int count = 0;
            do
            {
                uint digit = value % 10u;
                scratch[count++] = (char)('0' + digit);
                value /= 10u;
            }
            while (value != 0u && count < scratch.Length);

            for (int i = count - 1; i >= 0 && cursor < buffer.Length; i--)
                buffer[cursor++] = scratch[i];
            return cursor;
        }

        private static int AppendHex32(char[] buffer, int cursor, uint value)
        {
            for (int shift = 28; shift >= 0 && cursor < buffer.Length; shift -= 4)
                buffer[cursor++] = HexDigits[(int)((value >> shift) & 0xFu)];
            return cursor;
        }
    }
}
#endif
