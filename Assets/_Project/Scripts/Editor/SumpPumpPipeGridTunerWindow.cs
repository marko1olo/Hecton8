using System;
using System.Buffers.Binary;
using System.IO;
using Hecton8.Construction;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for SHINOBU_340 Vault-backed drainage tuning.
    /// </summary>
    public sealed class SumpPumpPipeGridTunerWindow : EditorWindow
    {
        private const int HistogramCapacity = SumpPumpPipeGridConstants.TelemetryFrameCount;
        private const uint ProfileBinarySchemaVersion = 1u;
        private const int ProfileBinaryHeaderBytes = 64;
        private const int ProfileBinaryRowBytes = 32;
        private const int CsvValidationOk = 0;
        private const int CsvValidationEmpty = 10;
        private const int CsvValidationNoRows = 11;
        private const int CsvValidationMissingName = 20;
        private const int CsvValidationInvalidConductance = 30;
        private const int CsvValidationInvalidPumpRate = 31;
        private const int CsvValidationInvalidPowerDraw = 32;
        private const int BinaryValidationWriteFailed = 100;
        private const int BinaryValidationReadbackFailed = 101;

        private Label _statusLabel;
        private IntegerField _frameField;
        private FloatField _evacuatedField;
        private IntegerField _pumpsField;
        private FloatField _pressureField;
        private IntegerField _solverMicrosField;
        private Slider _conductanceSlider;
        private Slider _pumpPowerSlider;
        private Slider _maxThroughputSlider;
        private Slider _gravityAssistSlider;
        private Slider _gravityResistanceSlider;
        private Slider _deltaSmoothingSlider;
        private Button _mockButton;
        private TextField _csvSourceField;
        private TextField _binaryOutputField;
        private Label _bridgeStatusLabel;
        private Label _schemaVersionLabel;
        private Label _schemaHashLabel;
        private Label _rowCountLabel;
        private Label _validationStatusLabel;
        private Label _layoutSummaryLabel;
        private DrainageHistogramElement _histogram;
        private DrainageTuningDTO _cachedTuning;
        private readonly DrainageTelemetryEntry[] _histogramScratch = new DrainageTelemetryEntry[HistogramCapacity];
        private readonly PipeProfileDTO[] _profileScratch = new PipeProfileDTO[SumpPumpPipeGridConstants.MaxPipeProfiles];
        private string _csvSourcePath;
        private string _binaryOutputPath;
        private double _nextReadoutRefresh;

        [MenuItem("HECTON-8/Hydraulic Sump Tuner")]
        public static void Open()
        {
            SumpPumpPipeGridTunerWindow window = GetWindow<SumpPumpPipeGridTunerWindow>();
            window.titleContent = new GUIContent("Hydraulic Sump");
            window.minSize = new Vector2(420f, 340f);
        }

        private void OnEnable()
        {
            BuildUi();
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextReadoutRefresh)
                return;

            _nextReadoutRefresh = now + 0.25d;
            RefreshReadout();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            _statusLabel = new Label("Runtime: PENDING");
            rootVisualElement.Add(_statusLabel);
            _frameField = CreateReadoutInteger("Frame");
            _evacuatedField = CreateReadoutFloat("Evacuated m3");
            _pumpsField = CreateReadoutInteger("Active Pumps");
            _pressureField = CreateReadoutFloat("Average Pressure");
            _solverMicrosField = CreateReadoutInteger("Solver us");
            rootVisualElement.Add(_frameField);
            rootVisualElement.Add(_evacuatedField);
            rootVisualElement.Add(_pumpsField);
            rootVisualElement.Add(_pressureField);
            rootVisualElement.Add(_solverMicrosField);
            _histogram = new DrainageHistogramElement();
            rootVisualElement.Add(_histogram);

            SumpPumpPipeGridRuntime.TryGetTuning(out _cachedTuning);
            _conductanceSlider = CreateSlider("Base Pipe Conductance", 0.001f, 0.4f, _cachedTuning.BasePipeConductance, OnConductanceChanged);
            _pumpPowerSlider = CreateSlider("Pump Power Draw", 0f, 750f, _cachedTuning.PumpPowerDraw, OnPumpPowerChanged);
            _maxThroughputSlider = CreateSlider("Max Pump Throughput", 0.001f, 1.25f, _cachedTuning.MaxPumpThroughputM3PerSecond, OnMaxThroughputChanged);
            _gravityAssistSlider = CreateSlider("Gravity Assist Scalar", 1f, 3f, _cachedTuning.GravityAssistScalar, OnGravityAssistChanged);
            _gravityResistanceSlider = CreateSlider("Gravity Resistance Scalar", 0.05f, 1f, _cachedTuning.GravityResistanceScalar, OnGravityResistanceChanged);
            _deltaSmoothingSlider = CreateSlider("Delta Smoothing", 0.05f, 1f, _cachedTuning.DeltaSmoothingFactor, OnDeltaSmoothingChanged);
            rootVisualElement.Add(_conductanceSlider);
            rootVisualElement.Add(_pumpPowerSlider);
            rootVisualElement.Add(_maxThroughputSlider);
            rootVisualElement.Add(_gravityAssistSlider);
            rootVisualElement.Add(_gravityResistanceSlider);
            rootVisualElement.Add(_deltaSmoothingSlider);

            _mockButton = new Button(GenerateMock) { text = "Generate Mock Drainage Network" };
            rootVisualElement.Add(_mockButton);
            BuildProfileBridgeUi();
        }

        private void BuildProfileBridgeUi()
        {
            Label bridgeHeader = new Label("Pipe Profile CSV Bridge");
            bridgeHeader.style.marginTop = 8f;
            rootVisualElement.Add(bridgeHeader);

            _csvSourceField = new TextField("CSV Source");
            _csvSourceField.RegisterValueChangedCallback(OnCsvSourceChanged);
            rootVisualElement.Add(_csvSourceField);
            rootVisualElement.Add(new Button(SelectCsvSource) { text = "Select CSV" });

            _binaryOutputField = new TextField("Binary Output");
            _binaryOutputField.RegisterValueChangedCallback(OnBinaryOutputChanged);
            rootVisualElement.Add(_binaryOutputField);
            rootVisualElement.Add(new Button(SelectBinaryOutput) { text = "Select Binary" });

            VisualElement buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttonRow.Add(new Button(ImportCsvProfiles) { text = "Import CSV To Runtime" });
            buttonRow.Add(new Button(BakeCsvProfilesToBinary) { text = "Bake Profile Binary" });
            rootVisualElement.Add(buttonRow);

            _bridgeStatusLabel = new Label("Bridge: WAITING");
            _schemaVersionLabel = new Label("Schema Version: 1");
            _schemaHashLabel = new Label("Schema Hash: 0x00000000");
            _rowCountLabel = new Label("Rows: 0");
            _validationStatusLabel = new Label("Validation: code=0 row=0 column=0 field=0");
            _layoutSummaryLabel = new Label(ResolveProfileLayoutSummary());
            rootVisualElement.Add(_bridgeStatusLabel);
            rootVisualElement.Add(_schemaVersionLabel);
            rootVisualElement.Add(_schemaHashLabel);
            rootVisualElement.Add(_rowCountLabel);
            rootVisualElement.Add(_validationStatusLabel);
            rootVisualElement.Add(_layoutSummaryLabel);
        }

        private static Slider CreateSlider(string label, float min, float max, float value, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.RegisterValueChangedCallback(callback);
            return slider;
        }

        private static IntegerField CreateReadoutInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            field.SetValueWithoutNotify(0);
            return field;
        }

        private static FloatField CreateReadoutFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            field.SetValueWithoutNotify(0f);
            return field;
        }

        private void OnConductanceChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.BasePipeConductance = Mathf.Max(0.001f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnPumpPowerChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.PumpPowerDraw = Mathf.Max(0f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnMaxThroughputChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.MaxPumpThroughputM3PerSecond = Mathf.Max(0.001f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnGravityAssistChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.GravityAssistScalar = Mathf.Max(0f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnGravityResistanceChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.GravityResistanceScalar = Mathf.Max(0f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnDeltaSmoothingChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.DeltaSmoothingFactor = Mathf.Clamp01(evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void GenerateMock()
        {
            SumpPumpPipeGridRuntime.TryGenerateMockDrainageNetwork();
        }

        private void OnCsvSourceChanged(ChangeEvent<string> evt)
        {
            _csvSourcePath = evt.newValue;
        }

        private void OnBinaryOutputChanged(ChangeEvent<string> evt)
        {
            _binaryOutputPath = evt.newValue;
        }

        private void SelectCsvSource()
        {
            string path = EditorUtility.OpenFilePanel("Pipe profile CSV", "Assets/StreamingAssets/Hecton8", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            _csvSourcePath = path;
            _csvSourceField?.SetValueWithoutNotify(path);
            if (string.IsNullOrEmpty(_binaryOutputPath))
            {
                _binaryOutputPath = Path.ChangeExtension(path, ".h8bin");
                _binaryOutputField?.SetValueWithoutNotify(_binaryOutputPath);
            }
        }

        private void SelectBinaryOutput()
        {
            string path = EditorUtility.SaveFilePanel("Pipe profile binary", "Assets/StreamingAssets/Hecton8/DataMonolith", "pipe_profiles.h8bin", "h8bin");
            if (string.IsNullOrEmpty(path))
                return;

            _binaryOutputPath = path;
            _binaryOutputField?.SetValueWithoutNotify(path);
        }

        private void ImportCsvProfiles()
        {
            if (!TryReadCsvBytes(out byte[] csvBytes, out uint hash))
                return;

            if (!TryParseCsvProfilesWithDiagnostics(csvBytes, _profileScratch.AsSpan(), out int parsedCount, out int row, out int column, out int field, out int code))
            {
                UpdateBridgeReadout("Bridge: CSV INVALID", hash, 0, code, row, column, field);
                return;
            }

            bool imported = SumpPumpPipeGridRuntime.TryLoadPipeProfilesFromCsvBytes(csvBytes, out int profileCount);
            UpdateBridgeReadout(imported ? "Bridge: IMPORTED" : "Bridge: RUNTIME REJECTED", hash, imported ? profileCount : parsedCount, imported ? CsvValidationOk : BinaryValidationReadbackFailed, 0, 0, 0);
        }

        private void BakeCsvProfilesToBinary()
        {
            if (!TryReadCsvBytes(out byte[] csvBytes, out uint hash))
                return;

            Span<PipeProfileDTO> scratch = _profileScratch.AsSpan();
            if (!TryParseCsvProfilesWithDiagnostics(csvBytes, scratch, out int profileCount, out int row, out int column, out int field, out int code))
            {
                UpdateBridgeReadout("Bridge: CSV INVALID", hash, 0, code, row, column, field);
                return;
            }

            if (string.IsNullOrEmpty(_binaryOutputPath))
            {
                _binaryOutputPath = Path.ChangeExtension(_csvSourcePath, ".h8bin");
                _binaryOutputField?.SetValueWithoutNotify(_binaryOutputPath);
            }

            try
            {
                string directory = Path.GetDirectoryName(_binaryOutputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                bool baked = TryWriteProfileBinaryAtomic(_binaryOutputPath, profileCount, hash);
                UpdateBridgeReadout(baked ? "Bridge: BAKED" : "Bridge: BINARY VALIDATION FAILED", hash, profileCount, baked ? CsvValidationOk : BinaryValidationReadbackFailed, 0, 0, 0);
            }
            catch
            {
                UpdateBridgeReadout("Bridge: BINARY WRITE FAILED", hash, profileCount, BinaryValidationWriteFailed, 0, 0, 0);
            }
        }

        private bool TryReadCsvBytes(out byte[] csvBytes, out uint hash)
        {
            csvBytes = null;
            hash = 0u;
            if (string.IsNullOrEmpty(_csvSourcePath) || !File.Exists(_csvSourcePath))
            {
                UpdateBridgeReadout("Bridge: CSV MISSING", 0u, 0, CsvValidationNoRows, 0, 0, 0);
                return false;
            }

            try
            {
                csvBytes = File.ReadAllBytes(_csvSourcePath);
                hash = HashBytes(csvBytes);
                if (csvBytes.Length > 0)
                    return true;

                UpdateBridgeReadout("Bridge: CSV EMPTY", hash, 0, CsvValidationEmpty, 0, 0, 0);
                return false;
            }
            catch
            {
                UpdateBridgeReadout("Bridge: CSV READ FAILED", 0u, 0, CsvValidationEmpty, 0, 0, 0);
                return false;
            }
        }

        private static bool TryParseCsvProfilesWithDiagnostics(ReadOnlySpan<byte> csv, Span<PipeProfileDTO> profiles, out int profileCount, out int row, out int column, out int field, out int validationCode)
        {
            row = 0;
            column = 0;
            field = 0;
            validationCode = CsvValidationOk;
            bool parsed = SumpPumpPipeGridValidation.TryParsePipeProfilesCsv(csv, profiles, out profileCount);
            if (parsed)
                return true;

            ResolveCsvFailure(csv, out row, out column, out field, out validationCode);
            return false;
        }

        private static void ResolveCsvFailure(ReadOnlySpan<byte> csv, out int row, out int column, out int field, out int validationCode)
        {
            row = 0;
            column = 0;
            field = 0;
            validationCode = CsvValidationNoRows;
            if (csv.Length <= 0)
            {
                validationCode = CsvValidationEmpty;
                return;
            }

            int cursor = 0;
            SkipEditorLine(csv, ref cursor);
            int rowNumber = 2;
            while (cursor < csv.Length)
            {
                SkipEditorLineBreak(csv, ref cursor, ref rowNumber);
                if (cursor >= csv.Length)
                    break;

                int lineStart = cursor;
                if (IsEditorLineBreak(csv[cursor]))
                    continue;

                for (int fieldIndex = 1; fieldIndex <= 4; fieldIndex++)
                {
                    int cellStart = cursor;
                    while (cursor < csv.Length && csv[cursor] != (byte)',' && !IsEditorLineBreak(csv[cursor]))
                        cursor++;
                    int cellEnd = cursor;
                    TrimCell(csv, ref cellStart, ref cellEnd);
                    if (fieldIndex == 1)
                    {
                        if (cellStart >= cellEnd)
                        {
                            row = rowNumber;
                            column = math.max(1, cellStart - lineStart + 1);
                            field = fieldIndex;
                            validationCode = CsvValidationMissingName;
                            return;
                        }
                    }
                    else if (!TryValidateNumberCell(csv, cellStart, cellEnd))
                    {
                        row = rowNumber;
                        column = math.max(1, cellStart - lineStart + 1);
                        field = fieldIndex;
                        validationCode = fieldIndex == 2
                            ? CsvValidationInvalidConductance
                            : fieldIndex == 3
                                ? CsvValidationInvalidPumpRate
                                : CsvValidationInvalidPowerDraw;
                        return;
                    }

                    if (fieldIndex < 4)
                    {
                        if (cursor >= csv.Length || IsEditorLineBreak(csv[cursor]))
                        {
                            row = rowNumber;
                            column = math.max(1, cursor - lineStart + 1);
                            field = fieldIndex + 1;
                            validationCode = fieldIndex == 1
                                ? CsvValidationInvalidConductance
                                : fieldIndex == 2
                                    ? CsvValidationInvalidPumpRate
                                    : CsvValidationInvalidPowerDraw;
                            return;
                        }
                        cursor++;
                    }
                }

                SkipEditorLine(csv, ref cursor);
                rowNumber++;
            }

            row = rowNumber;
            column = 1;
            field = 1;
        }

        private static void SkipEditorLine(ReadOnlySpan<byte> csv, ref int cursor)
        {
            while (cursor < csv.Length && !IsEditorLineBreak(csv[cursor]))
                cursor++;
            int unusedRow = 0;
            SkipEditorLineBreak(csv, ref cursor, ref unusedRow);
        }

        private static void SkipEditorLineBreak(ReadOnlySpan<byte> csv, ref int cursor, ref int rowNumber)
        {
            bool skipped = false;
            if (cursor < csv.Length && csv[cursor] == (byte)'\r')
            {
                cursor++;
                skipped = true;
            }
            if (cursor < csv.Length && csv[cursor] == (byte)'\n')
            {
                cursor++;
                skipped = true;
            }
            if (skipped && rowNumber > 0)
                rowNumber++;
        }

        private static bool IsEditorLineBreak(byte value)
        {
            return value == (byte)'\r' || value == (byte)'\n';
        }

        private static void TrimCell(ReadOnlySpan<byte> csv, ref int start, ref int end)
        {
            while (start < end && IsEditorWhitespace(csv[start]))
                start++;
            while (end > start && IsEditorWhitespace(csv[end - 1]))
                end--;
        }

        private static bool IsEditorWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool TryValidateNumberCell(ReadOnlySpan<byte> csv, int start, int end)
        {
            if (start >= end)
                return false;

            int cursor = start;
            if (csv[cursor] == (byte)'+' || csv[cursor] == (byte)'-')
                cursor++;

            bool hasDigit = false;
            while (cursor < end && csv[cursor] >= (byte)'0' && csv[cursor] <= (byte)'9')
            {
                hasDigit = true;
                cursor++;
            }

            if (cursor < end && csv[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < end && csv[cursor] >= (byte)'0' && csv[cursor] <= (byte)'9')
                {
                    hasDigit = true;
                    cursor++;
                }
            }

            if (cursor < end && (csv[cursor] == (byte)'e' || csv[cursor] == (byte)'E'))
            {
                cursor++;
                if (cursor < end && (csv[cursor] == (byte)'+' || csv[cursor] == (byte)'-'))
                    cursor++;

                bool hasExponentDigit = false;
                while (cursor < end && csv[cursor] >= (byte)'0' && csv[cursor] <= (byte)'9')
                {
                    hasExponentDigit = true;
                    cursor++;
                }

                if (!hasExponentDigit)
                    return false;
            }

            return hasDigit && cursor == end;
        }

        private bool TryWriteProfileBinaryAtomic(string outputPath, int profileCount, uint hash)
        {
            if (string.IsNullOrEmpty(outputPath))
                return false;

            string tempPath = outputPath + ".tmp";
            string backupPath = outputPath + ".bak";
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Span<byte> header = stackalloc byte[ProfileBinaryHeaderBytes];
                    WriteProfileHeader(header, profileCount, hash);
                    stream.Write(header);
                    Span<byte> row = stackalloc byte[ProfileBinaryRowBytes];
                    for (int i = 0; i < profileCount; i++)
                    {
                        WriteProfileRow(row, _profileScratch[i]);
                        stream.Write(row);
                    }
                    stream.Flush(true);
                }

                if (!TryValidateProfileBinary(tempPath, profileCount, hash))
                {
                    File.Delete(tempPath);
                    return false;
                }

                if (File.Exists(outputPath))
                    File.Replace(tempPath, outputPath, backupPath, true);
                else
                    File.Move(tempPath, outputPath);
                return true;
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                return false;
            }
        }

        private static bool TryValidateProfileBinary(string path, int profileCount, uint hash)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            long expectedBytes = ProfileBinaryHeaderBytes + ((long)math.max(0, profileCount) * ProfileBinaryRowBytes);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != expectedBytes)
                    return false;

                Span<byte> header = stackalloc byte[ProfileBinaryHeaderBytes];
                int readBytes = stream.Read(header);
                if (readBytes != ProfileBinaryHeaderBytes)
                    return false;

                return header[0] == (byte)'H' &&
                       header[1] == (byte)'8' &&
                       header[2] == (byte)'P' &&
                       header[3] == (byte)'I' &&
                       header[4] == (byte)'P' &&
                       header[5] == (byte)'E' &&
                       BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4)) == ProfileBinarySchemaVersion &&
                       BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(12, 4)) == (uint)math.max(0, profileCount) &&
                       BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4)) == ProfileBinaryRowBytes &&
                       BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4)) == hash &&
                       BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4)) == ResolveProfileLayoutHash();
            }
        }

        private static void WriteProfileHeader(Span<byte> header, int profileCount, uint hash)
        {
            header.Clear();
            header[0] = (byte)'H';
            header[1] = (byte)'8';
            header[2] = (byte)'P';
            header[3] = (byte)'I';
            header[4] = (byte)'P';
            header[5] = (byte)'E';
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), ProfileBinarySchemaVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), (uint)math.max(0, profileCount));
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), ProfileBinaryRowBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20, 4), hash);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), ResolveProfileLayoutHash());
        }

        private static void WriteProfileRow(Span<byte> row, PipeProfileDTO profile)
        {
            row.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(0, 4), profile.NameHash);
            BinaryPrimitives.WriteInt32LittleEndian(row.Slice(4, 4), BitConverter.SingleToInt32Bits(profile.PipeConductance));
            BinaryPrimitives.WriteInt32LittleEndian(row.Slice(8, 4), BitConverter.SingleToInt32Bits(profile.PumpRateM3PerSecond));
            BinaryPrimitives.WriteInt32LittleEndian(row.Slice(12, 4), BitConverter.SingleToInt32Bits(profile.PumpPowerDrawWatts));
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(16, 4), profile.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(20, 4), profile.Reserved0);
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(24, 4), profile.Reserved1);
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(28, 4), profile.Reserved2);
        }

        private void UpdateBridgeReadout(string status, uint hash, int rows, int validationCode, int row, int column, int field)
        {
            if (_bridgeStatusLabel != null)
                _bridgeStatusLabel.text = status;
            if (_schemaVersionLabel != null)
                _schemaVersionLabel.text = "Schema Version: " + ProfileBinarySchemaVersion;
            if (_schemaHashLabel != null)
                _schemaHashLabel.text = "Schema Hash: 0x" + hash.ToString("X8");
            if (_rowCountLabel != null)
                _rowCountLabel.text = "Rows: " + Mathf.Max(0, rows);
            if (_validationStatusLabel != null)
                _validationStatusLabel.text = "Validation: code=" + validationCode + " row=" + Mathf.Max(0, row) + " column=" + Mathf.Max(0, column) + " field=" + Mathf.Max(0, field);
            if (_layoutSummaryLabel != null)
                _layoutSummaryLabel.text = ResolveProfileLayoutSummary();
        }

        private static uint HashBytes(byte[] bytes)
        {
            uint hash = SumpPumpPipeGridConstants.FnvOffset;
            if (bytes == null)
                return hash;

            for (int i = 0; i < bytes.Length; i++)
                hash = SumpPumpPipeGridValidation.MixHash(hash, bytes[i]);
            return hash;
        }

        private static uint ResolveProfileLayoutHash()
        {
            uint hash = SumpPumpPipeGridConstants.FnvOffset;
            hash = SumpPumpPipeGridValidation.MixHash(hash, 32u);
            hash = SumpPumpPipeGridValidation.MixHash(hash, 0u);
            hash = SumpPumpPipeGridValidation.MixHash(hash, 4u);
            hash = SumpPumpPipeGridValidation.MixHash(hash, 8u);
            hash = SumpPumpPipeGridValidation.MixHash(hash, 12u);
            hash = SumpPumpPipeGridValidation.MixHash(hash, 16u);
            return hash;
        }

        private static string ResolveProfileLayoutSummary()
        {
            return SumpPumpPipeGridValidation.ValidatePipeProfileLayout()
                ? "Layout: PipeProfileDTO 32B offsets 0/4/8/12/16/20/24/28"
                : "Layout: INVALID PipeProfileDTO";
        }

        private void RefreshReadout()
        {
            if (_statusLabel == null || _frameField == null || _evacuatedField == null || _pumpsField == null || _pressureField == null || _solverMicrosField == null)
                return;

            bool hasRuntime = SumpPumpPipeGridRuntime.HasActiveRuntime;
            _statusLabel.text = hasRuntime ? "Runtime: ACTIVE" : "Runtime: MISSING";

            if (SumpPumpPipeGridRuntime.TryGetLatestTelemetry(out DrainageTelemetryEntry entry))
            {
                _frameField.SetValueWithoutNotify(ClampUIntToInt(entry.FrameIndex));
                _evacuatedField.SetValueWithoutNotify(entry.FrameEvacuatedM3);
                _pumpsField.SetValueWithoutNotify(ClampUIntToInt(entry.ActivePumpCount));
                _pressureField.SetValueWithoutNotify(entry.AveragePressure);
                _solverMicrosField.SetValueWithoutNotify(ClampUIntToInt(entry.SolverWallMicroseconds));
            }
            else
            {
                _frameField.SetValueWithoutNotify(0);
                _evacuatedField.SetValueWithoutNotify(0f);
                _pumpsField.SetValueWithoutNotify(0);
                _pressureField.SetValueWithoutNotify(0f);
                _solverMicrosField.SetValueWithoutNotify(0);
            }

            if (_histogram != null && SumpPumpPipeGridRuntime.TryCopyTelemetry(_histogramScratch, out int sampleCount))
                _histogram.SetSamples(_histogramScratch, sampleCount);
        }

        private static int ClampUIntToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private sealed class DrainageHistogramElement : VisualElement
        {
            private readonly DrainageTelemetryEntry[] _samples = new DrainageTelemetryEntry[HistogramCapacity];
            private int _count;

            public DrainageHistogramElement()
            {
                style.height = 96f;
                style.marginTop = 8f;
                style.marginBottom = 8f;
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetSamples(DrainageTelemetryEntry[] source, int count)
            {
                _count = Mathf.Clamp(count, 0, Mathf.Min(source != null ? source.Length : 0, _samples.Length));
                for (int i = 0; i < _count; i++)
                    _samples[i] = source[i];
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                var painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.08f, 0.16f, 0.20f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.Stroke();

                if (_count <= 1)
                    return;

                float maxEvacuated = 0.0001f;
                float maxMicros = 1f;
                for (int i = 0; i < _count; i++)
                {
                    maxEvacuated = Mathf.Max(maxEvacuated, _samples[i].FrameEvacuatedM3);
                    maxMicros = Mathf.Max(maxMicros, _samples[i].SolverWallMicroseconds);
                }

                DrawSeries(painter, rect, maxEvacuated, true, new Color(0.1f, 0.65f, 0.9f, 1f));
                DrawSeries(painter, rect, maxMicros, false, new Color(0.95f, 0.38f, 0.18f, 1f));
            }

            private void DrawSeries(Painter2D painter, Rect rect, float maxValue, bool evacuated, Color color)
            {
                painter.strokeColor = color;
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    float value = evacuated ? _samples[i].FrameEvacuatedM3 : _samples[i].SolverWallMicroseconds;
                    float x = rect.xMin + (rect.width * i / Mathf.Max(1, _count - 1));
                    float y = rect.yMax - (Mathf.Clamp01(value / Mathf.Max(0.0001f, maxValue)) * rect.height);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }
        }
    }
}
