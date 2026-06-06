#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class BlackboxXRayViewer : EditorWindow
    {
        private const int FramePreviewCount = 300;
        private const int EventPreviewCount = 128;
        private const int SourcePreviewCount = GlobalTelemetryBus.ShinobuBlackboxSourceCapacity;
        private const int VisibleFrameRows = 48;
        private const int VisibleEventRows = 32;
        private const int VisibleSourceRows = SourcePreviewCount;
        private const double RefreshIntervalSeconds = 0.25d;
        private const string WindowTitle = "Blackbox X-Ray";
        private const string FlagsCsvRelativePath = "Docs/Tasks/telemetry_flags.csv";
        private const string DictionaryCsvRelativePath = "Docs/Tasks/telemetry_hash_dictionary.csv";
        private const string FlagsMissingStatus = "telemetry_flags.csv not found";
        private const string DictionaryMissingStatus = "telemetry_hash_dictionary.csv not found";
        private const uint SurvivalSourceHash = 0x53555256u; // SURV
        private const int SurvivalDeathCauseShift = 24;

        private readonly GlobalTelemetryBus.BlackboxEditorFrame[] _frames = new GlobalTelemetryBus.BlackboxEditorFrame[FramePreviewCount];
        private readonly TelemetryEventDTO[] _events = new TelemetryEventDTO[EventPreviewCount];
        private readonly GlobalTelemetryBus.BlackboxEditorSourcePayload[] _sources =
            new GlobalTelemetryBus.BlackboxEditorSourcePayload[SourcePreviewCount];
        private readonly GlobalTelemetryBus.BlackboxEditorSourceDescriptor[] _sourceDescriptors =
            new GlobalTelemetryBus.BlackboxEditorSourceDescriptor[SourcePreviewCount];
        private readonly Dictionary<uint, string> _eventNames = new Dictionary<uint, string>(256);
        private readonly Label[] _frameLabels = new Label[VisibleFrameRows];
        private readonly Label[] _eventLabels = new Label[VisibleEventRows];
        private readonly Label[] _sourceLabels = new Label[VisibleSourceRows];

        private Label _statusLabel;
        private Label _flagsStatusLabel;
        private Label _dictionaryStatusLabel;
        private string _projectRoot;
        private string _flagsCsvPath;
        private string _dictionaryCsvPath;
        private DateTime _flagsTimestampUtc;
        private DateTime _dictionaryTimestampUtc;
        private double _nextRefreshTime;
        private int _frameCount;
        private int _eventCount;
        private int _sourceCount;
        private int _sourceDescriptorCount;

        [MenuItem("Hecton8/Forensics/Blackbox X-Ray Viewer")]
        private static void Open()
        {
            GetWindow<BlackboxXRayViewer>(WindowTitle);
        }

        private void OnEnable()
        {
            ResolvePaths();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            _nextRefreshTime = 0d;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 6f;
            rootVisualElement.style.paddingRight = 6f;
            rootVisualElement.style.paddingTop = 6f;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 4f;
            rootVisualElement.Add(toolbar);

            Button refreshButton = new Button(RefreshNow);
            refreshButton.text = "Refresh";
            refreshButton.style.width = 78f;
            toolbar.Add(refreshButton);

            Button dumpButton = new Button(() => GlobalTelemetryBus.TryDumpBlackboxNow(0x58524159u));
            dumpButton.text = "Dump";
            dumpButton.style.width = 64f;
            toolbar.Add(dumpButton);

            _statusLabel = new Label();
            _statusLabel.style.marginLeft = 8f;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(_statusLabel);

            _flagsStatusLabel = new Label();
            rootVisualElement.Add(_flagsStatusLabel);

            _dictionaryStatusLabel = new Label();
            rootVisualElement.Add(_dictionaryStatusLabel);

            Label sourceHeader = new Label("Newest source payloads");
            sourceHeader.style.marginTop = 8f;
            sourceHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(sourceHeader);

            ScrollView sourceScroll = new ScrollView();
            sourceScroll.style.height = 160f;
            rootVisualElement.Add(sourceScroll);
            for (int i = 0; i < _sourceLabels.Length; i++)
            {
                Label label = new Label();
                sourceScroll.Add(label);
                _sourceLabels[i] = label;
            }

            Label eventHeader = new Label("TelemetryEventDTO stream");
            eventHeader.style.marginTop = 8f;
            eventHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(eventHeader);

            ScrollView eventScroll = new ScrollView();
            eventScroll.style.height = 230f;
            rootVisualElement.Add(eventScroll);
            for (int i = 0; i < _eventLabels.Length; i++)
            {
                Label label = new Label();
                eventScroll.Add(label);
                _eventLabels[i] = label;
            }

            Label frameHeader = new Label("Frame ring");
            frameHeader.style.marginTop = 8f;
            frameHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(frameHeader);

            ScrollView frameScroll = new ScrollView();
            frameScroll.style.flexGrow = 1f;
            rootVisualElement.Add(frameScroll);
            for (int i = 0; i < _frameLabels.Length; i++)
            {
                Label label = new Label();
                frameScroll.Add(label);
                _frameLabels[i] = label;
            }

            RefreshNow();
        }

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            RefreshNow();
        }

        private void RefreshNow()
        {
            ResolvePaths();
            RefreshFlagCsv();
            RefreshDictionaryCsv();

            _frameCount = GlobalTelemetryBus.CopyBlackboxEditorFrames(_frames);
            _eventCount = GlobalTelemetryBus.CopyBlackboxEditorEvents(_events);
            _sourceCount = GlobalTelemetryBus.CopyNewestBlackboxEditorSourcePayloads(_sources);
            _sourceDescriptorCount = GlobalTelemetryBus.CopyBlackboxEditorSourceDescriptors(_sourceDescriptors);

            if (_statusLabel != null)
            {
                _statusLabel.text = string.Concat(
                    "frames ",
                    _frameCount.ToString(CultureInfo.InvariantCulture),
                    " / ",
                    GlobalTelemetryBus.BlackboxActiveFrameCount.ToString(CultureInfo.InvariantCulture),
                    " events ",
                    _eventCount.ToString(CultureInfo.InvariantCulture),
                    " sources ",
                    _sourceCount.ToString(CultureInfo.InvariantCulture),
                    "/",
                    _sourceDescriptorCount.ToString(CultureInfo.InvariantCulture),
                    " fatal ",
                    GlobalTelemetryBus.IsCatastrophicFailure ? "1" : "0");
            }

            SyncSourceRows();
            SyncEventRows();
            SyncFrameRows();
            SceneView.RepaintAll();
        }

        private void ResolvePaths()
        {
            if (!string.IsNullOrEmpty(_projectRoot))
                return;

            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _flagsCsvPath = Path.Combine(_projectRoot, FlagsCsvRelativePath);
            _dictionaryCsvPath = Path.Combine(_projectRoot, DictionaryCsvRelativePath);
        }

        private void RefreshFlagCsv()
        {
            if (string.IsNullOrEmpty(_flagsCsvPath) || !File.Exists(_flagsCsvPath))
            {
                if (_flagsStatusLabel != null)
                    _flagsStatusLabel.text = FlagsMissingStatus;
                return;
            }

            DateTime timestampUtc = File.GetLastWriteTimeUtc(_flagsCsvPath);
            if (timestampUtc == _flagsTimestampUtc)
                return;

            _flagsTimestampUtc = timestampUtc;
            int applied = 0;
            using (StreamReader reader = new StreamReader(_flagsCsvPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (GlobalTelemetryBus.TryApplyTelemetryFlagCsvLine(line.AsSpan()))
                        applied++;
                }
            }

            if (_flagsStatusLabel != null)
                _flagsStatusLabel.text = string.Concat("telemetry_flags.csv applied ", applied.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshDictionaryCsv()
        {
            if (string.IsNullOrEmpty(_dictionaryCsvPath) || !File.Exists(_dictionaryCsvPath))
            {
                if (_dictionaryStatusLabel != null)
                    _dictionaryStatusLabel.text = DictionaryMissingStatus;
                return;
            }

            DateTime timestampUtc = File.GetLastWriteTimeUtc(_dictionaryCsvPath);
            if (timestampUtc == _dictionaryTimestampUtc)
                return;

            _dictionaryTimestampUtc = timestampUtc;
            _eventNames.Clear();

            using (StreamReader reader = new StreamReader(_dictionaryCsvPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    TryReadDictionaryLine(line);
            }

            if (_dictionaryStatusLabel != null)
                _dictionaryStatusLabel.text = string.Concat("telemetry_hash_dictionary.csv rows ", _eventNames.Count.ToString(CultureInfo.InvariantCulture));
        }

        private void SyncSourceRows()
        {
            int rowCount = Math.Max(_sourceCount, _sourceDescriptorCount);
            for (int i = 0; i < _sourceLabels.Length; i++)
            {
                Label label = _sourceLabels[i];
                if (label == null)
                    continue;

                if (i >= rowCount)
                {
                    label.text = string.Empty;
                    continue;
                }

                GlobalTelemetryBus.BlackboxEditorSourcePayload payload =
                    i < _sourceCount ? _sources[i] : default;
                GlobalTelemetryBus.BlackboxEditorSourceDescriptor descriptor =
                    i < _sourceDescriptorCount ? _sourceDescriptors[i] : default;
                label.text = FormatSourcePayload(i, descriptor, payload);
            }
        }

        private void SyncEventRows()
        {
            int start = Math.Max(0, _eventCount - _eventLabels.Length);
            for (int i = 0; i < _eventLabels.Length; i++)
            {
                Label label = _eventLabels[i];
                if (label == null)
                    continue;

                int source = start + i;
                if (source >= _eventCount)
                {
                    label.text = string.Empty;
                    continue;
                }

                TelemetryEventDTO entry = _events[source];
                label.text = string.Concat(
                    "#",
                    source.ToString(CultureInfo.InvariantCulture),
                    " 0x",
                    entry.EventHash.ToString("X8", CultureInfo.InvariantCulture),
                    " ",
                    ResolveEventName(entry.EventHash),
                    " scalar ",
                    entry.ScalarValue.ToString("0.###", CultureInfo.InvariantCulture),
                    " entity 0x",
                    entry.EntityId.ToString("X8", CultureInfo.InvariantCulture));
            }
        }

        private static string FormatSourcePayload(
            int slot,
            GlobalTelemetryBus.BlackboxEditorSourceDescriptor descriptor,
            GlobalTelemetryBus.BlackboxEditorSourcePayload payload)
        {
            uint sourceHash = descriptor.SourceHash != 0u ? descriptor.SourceHash : payload.Word0;
            if (sourceHash == SurvivalSourceHash)
            {
                uint flags = payload.Word15;
                return string.Concat(
                    "slot ",
                    slot.ToString(CultureInfo.InvariantCulture),
                    " SURV frame ",
                    payload.Word1.ToString(CultureInfo.InvariantCulture),
                    " player 0x",
                    payload.Word2.ToString("X8", CultureInfo.InvariantCulture),
                    " o2 ",
                    UIntBitsToFloat(payload.Word3).ToString("0.000", CultureInfo.InvariantCulture),
                    " integrity ",
                    UIntBitsToFloat(payload.Word4).ToString("0.000", CultureInfo.InvariantCulture),
                    " depth ",
                    UIntBitsToFloat(payload.Word5).ToString("0.0", CultureInfo.InvariantCulture),
                    " atm ",
                    UIntBitsToFloat(payload.Word6).ToString("0.00", CultureInfo.InvariantCulture),
                    " deco ",
                    UIntBitsToFloat(payload.Word12).ToString("0.000", CultureInfo.InvariantCulture),
                    " status 0x",
                    payload.Word14.ToString("X8", CultureInfo.InvariantCulture),
                    " death ",
                    ResolveSurvivalDeathCauseLabel(flags),
                    " flags 0x",
                    flags.ToString("X8", CultureInfo.InvariantCulture),
                    " srcFlags 0x",
                    descriptor.Flags.ToString("X8", CultureInfo.InvariantCulture));
            }

            return string.Concat(
                "slot ",
                slot.ToString(CultureInfo.InvariantCulture),
                " src 0x",
                sourceHash.ToString("X8", CultureInfo.InvariantCulture),
                " bytes ",
                descriptor.PayloadBytes.ToString(CultureInfo.InvariantCulture),
                " srcFlags 0x",
                descriptor.Flags.ToString("X8", CultureInfo.InvariantCulture),
                " payload0 0x",
                payload.Word0.ToString("X8", CultureInfo.InvariantCulture),
                " w1 0x",
                payload.Word1.ToString("X8", CultureInfo.InvariantCulture),
                " w2 0x",
                payload.Word2.ToString("X8", CultureInfo.InvariantCulture),
                " w3 0x",
                payload.Word3.ToString("X8", CultureInfo.InvariantCulture),
                " flags 0x",
                payload.Word15.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static string ResolveSurvivalDeathCauseLabel(uint flags)
        {
            uint cause = (flags >> SurvivalDeathCauseShift) & 0xFFu;
            switch (cause)
            {
                case 0u:
                    return "none";
                case 1u:
                    return "oxygen";
                case 2u:
                    return "pressure";
                case 3u:
                    return "thermal";
                case 4u:
                    return "radiation";
                case 5u:
                    return "starvation";
                case 6u:
                    return "dehydration";
                case 7u:
                    return "integrity";
                default:
                    return cause.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void SyncFrameRows()
        {
            int start = Math.Max(0, _frameCount - _frameLabels.Length);
            for (int i = 0; i < _frameLabels.Length; i++)
            {
                Label label = _frameLabels[i];
                if (label == null)
                    continue;

                int source = start + i;
                if (source >= _frameCount)
                {
                    label.text = string.Empty;
                    continue;
                }

                GlobalTelemetryBus.BlackboxEditorFrame frame = _frames[source];
                label.text = string.Concat(
                    "slot ",
                    frame.Slot.ToString(CultureInfo.InvariantCulture),
                    " frame ",
                    frame.FrameNumber.ToString(CultureInfo.InvariantCulture),
                    " fatal 0x",
                    frame.FatalHash.ToString("X8", CultureInfo.InvariantCulture),
                    " last 0x",
                    frame.LastEventHash.ToString("X8", CultureInfo.InvariantCulture),
                    " ",
                    ResolveEventName(frame.LastEventHash),
                    " impact ",
                    FormatVector(frame.ImpactPosition));
            }
        }

        private void TryReadDictionaryLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            ReadOnlySpan<char> row = line.AsSpan();
            int separator = row.IndexOfAny(',', ';');
            if (separator <= 0 || separator >= row.Length - 1)
                return;

            if (!TryParseHash(row.Slice(0, separator), out uint hash))
                return;

            ReadOnlySpan<char> nameSpan = Trim(row.Slice(separator + 1));
            if (nameSpan.Length <= 0)
                return;

            _eventNames[hash] = nameSpan.ToString();
        }

        private string ResolveEventName(uint hash)
        {
            if (hash == 0u)
                return "NONE";
            return _eventNames.TryGetValue(hash, out string name) ? name : "UNKNOWN";
        }

        private static bool TryParseHash(ReadOnlySpan<char> text, out uint hash)
        {
            ReadOnlySpan<char> trimmed = Trim(text);
            if (trimmed.Length <= 0)
            {
                hash = 0u;
                return false;
            }

            NumberStyles styles = NumberStyles.Integer;
            if (trimmed.Length > 2 && trimmed[0] == '0' && (trimmed[1] == 'x' || trimmed[1] == 'X'))
            {
                trimmed = trimmed.Slice(2);
                styles = NumberStyles.HexNumber;
            }

            return uint.TryParse(trimmed, styles, CultureInfo.InvariantCulture, out hash);
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, end - start + 1);
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Concat(
                "(",
                value.x.ToString("0.##", CultureInfo.InvariantCulture),
                ", ",
                value.y.ToString("0.##", CultureInfo.InvariantCulture),
                ", ",
                value.z.ToString("0.##", CultureInfo.InvariantCulture),
                ")");
        }

        private static float UIntBitsToFloat(uint bits)
        {
            UIntFloatUnion union = default;
            union.UIntValue = bits;
            return union.FloatValue;
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (_frameCount <= 0)
                return;

            Camera camera = sceneView.camera;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            Handles.color = new Color(1f, 0.05f, 0.02f, 0.9f);

            int start = Math.Max(0, _frameCount - 64);
            for (int i = start; i < _frameCount; i++)
            {
                Vector3 point = _frames[i].ImpactPosition;
                if (!IsFinite(point) || point == Vector3.zero)
                    continue;

                float size = HandleUtility.GetHandleSize(point) * 0.08f;
                Handles.DrawLine(point - right * size, point + right * size);
                Handles.DrawLine(point - up * size, point + up * size);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UIntFloatUnion
        {
            [FieldOffset(0)] public uint UIntValue;
            [FieldOffset(0)] public float FloatValue;
        }
    }
}
#endif
