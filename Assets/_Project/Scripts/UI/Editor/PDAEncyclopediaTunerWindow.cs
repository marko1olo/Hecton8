#if UNITY_EDITOR
using System;
using System.Globalization;
using Hecton8.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class PDAEncyclopediaTunerWindow : EditorWindow
    {
        private PDAEncyclopediaStreamer _target;
        private Label _stateLabel;
        private Label _rawLabel;
        private TextField _hashField;
        private readonly char[] _rawBuffer = new char[2048];

        [MenuItem("Hecton8/PDA/Encyclopedia Streamer Tuner")]
        public static void Open()
        {
            GetWindow<PDAEncyclopediaTunerWindow>("PDA Encyclopedia");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _stateLabel = new Label("No PDAEncyclopediaStreamer selected.");
            _stateLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_stateLabel);

            _hashField = new TextField("Entry hash");
            _hashField.value = "0xAEC57EAC";
            root.Add(_hashField);

            _rawLabel = new Label("Raw UTF-8 x-ray not loaded.");
            _rawLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_rawLabel);

            Button refresh = new Button(Refresh) { text = "Refresh" };
            Button select = new Button(SelectHash) { text = "Select Hash" };
            Button unlockAll = new Button(UnlockAll) { text = "Unlock All" };
            Button lockAll = new Button(LockAll) { text = "Lock All" };
            Button ingestCsv = new Button(IngestCsv) { text = "Ingest CSV" };
            Button rawHex = new Button(RefreshRawHex) { text = "Raw UTF-8 Hex" };

            root.Add(refresh);
            root.Add(select);
            root.Add(unlockAll);
            root.Add(lockAll);
            root.Add(ingestCsv);
            root.Add(rawHex);
            Refresh();
        }

        private PDAEncyclopediaStreamer ResolveTarget()
        {
            if (_target != null)
                return _target;

            _target = FindAnyObjectByType<PDAEncyclopediaStreamer>();
            return _target;
        }

        private void Refresh()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null)
            {
                _stateLabel.text = "No PDAEncyclopediaStreamer in the loaded scene.";
                return;
            }

            if (!streamer.EditorTrySnapshot(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO mask))
            {
                _stateLabel.text = "Streamer found. Vault buffers are not available.";
                return;
            }

            PDAEncyclopediaStreamer.ValidateEncyclopediaStateLayout(out int sizeBytes, out int mask0Offset, out int mask3Offset);
            bool layoutsValid = PDAEncyclopediaStreamer.ValidatePdaStreamerLayouts(
                out int encyclopediaBytes,
                out int runtimeBytes,
                out int entryMetaBytes,
                out int telemetryBytes,
                out int typewriterBytes,
                out int aupBytes,
                out int h8lrHeaderBytes,
                out int h8lrRecordBytes,
                out int runtimeSourceBytesOffset,
                out int telemetryFlagsOffset,
                out int typewriterReserved3Offset,
                out int aupReserved1Offset,
                out int h8lrRecordReserved0Offset);
            _stateLabel.text =
                $"Entry: 0x{state.LastEntryHash:X8}\n" +
                $"Unlocked: {state.UnlockedCount}/256  Revision: {state.Revision}\n" +
                $"Decoded/Visible: {state.DecodedChars}/{state.VisibleChars}  Bytes: {state.SourceBytes}\n" +
                $"State: {(PdaEncyclopediaStreamState)state.StreamState}  Fault: 0x{state.FaultHash:X8}\n" +
                $"Mask[0..3]: {mask.Mask0:X16} {mask.Mask1:X16} {mask.Mask2:X16} {mask.Mask3:X16}\n" +
                $"Layout: {sizeBytes} bytes  Mask0@{mask0Offset} Mask3@{mask3Offset}\n" +
                $"DTOs: {(layoutsValid ? "OK" : "FAIL")}  E/R/M/T/TW/AUP/H8H/H8R={encyclopediaBytes}/{runtimeBytes}/{entryMetaBytes}/{telemetryBytes}/{typewriterBytes}/{aupBytes}/{h8lrHeaderBytes}/{h8lrRecordBytes}\n" +
                $"Offsets: SourceBytes@{runtimeSourceBytesOffset} TelemetryFlags@{telemetryFlagsOffset} TypewriterPad@{typewriterReserved3Offset} AUPPad@{aupReserved1Offset} H8LRPad@{h8lrRecordReserved0Offset}";
        }

        private void SelectHash()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null || !TryParseHash(_hashField.value, out uint hash))
                return;

            streamer.EditorSelectEntry(hash);
            Refresh();
        }

        private void UnlockAll()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null)
                return;

            streamer.EditorUnlockAll();
            Refresh();
        }

        private void LockAll()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null)
                return;

            streamer.EditorLockAll();
            Refresh();
        }

        private void IngestCsv()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null)
                return;

            streamer.EditorIngestCsv();
            Refresh();
        }

        private void RefreshRawHex()
        {
            PDAEncyclopediaStreamer streamer = ResolveTarget();
            if (streamer == null || !TryParseHash(_hashField.value, out uint hash))
                return;

            if (streamer.EditorTryWriteRawUtf8Hex(hash, _rawBuffer.AsSpan(), out int written))
                _rawLabel.text = new string(_rawBuffer, 0, written);
            else
                _rawLabel.text = "Raw UTF-8 x-ray unavailable for this hash.";
        }

        private static bool TryParseHash(string value, out uint hash)
        {
            hash = 0u;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out hash);
        }
    }
}
#endif
