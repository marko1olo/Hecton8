#if UNITY_EDITOR
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Hecton8.Editor.Physics
{
    public sealed class BurstVectorizationXRayWindow : EditorWindow
    {
        private readonly char[] _readoutBuffer = new char[1024];
        private Label _statusLabel;
        private Slider _scalarFallbackSlider;
        private VisualElement _vectorBar;
        private VisualElement _scalarBar;
        private VisualElement _throughputBar;
        private int _lastCursor = int.MinValue;
        private int _lastEntityCount = int.MinValue;
        private int _lastVectorCentis = int.MinValue;
        private int _lastThroughputCentis = int.MinValue;
        private int _lastScalarFallbackCentis = int.MinValue;
        private bool _runtimeUnavailableStatusShown;
        private bool _telemetryStatusShown;

        [MenuItem("HECTON-8/Physics/Burst Vectorization X-Ray")]
        public static void Open()
        {
            GetWindow<BurstVectorizationXRayWindow>("Burst SIMD X-Ray");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            Button runBenchmarkButton = new Button(RunBenchmark) { text = "Run 250k SIMD Benchmark" };
            Button auditButton = new Button(AuditLayout) { text = "Audit ARM64 Layout" };
            Button toleranceButton = new Button(LoadToleranceCsv) { text = "Load simd_math_tolerances.csv" };
            rootVisualElement.Add(runBenchmarkButton);
            rootVisualElement.Add(auditButton);
            rootVisualElement.Add(toleranceButton);

            _scalarFallbackSlider = new Slider("Scalar Probe Weight", 0f, 1f);
            _scalarFallbackSlider.RegisterValueChangedCallback(OnScalarFallbackChanged);
            rootVisualElement.Add(_scalarFallbackSlider);

            rootVisualElement.Add(new Label("Vector us"));
            _vectorBar = CreateBar(0.05f, 0.85f, 0.9f);
            rootVisualElement.Add(_vectorBar);
            rootVisualElement.Add(new Label("Scalar us"));
            _scalarBar = CreateBar(0.95f, 0.35f, 0.18f);
            rootVisualElement.Add(_scalarBar);
            rootVisualElement.Add(new Label("Entities/ms"));
            _throughputBar = CreateBar(0.2f, 0.8f, 0.35f);
            rootVisualElement.Add(_throughputBar);

            _statusLabel = new Label("Play Mode runtime and GlobalDataVault required.");
            _statusLabel.style.marginTop = 8;
            rootVisualElement.Add(_statusLabel);

            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            PullSimdTuning();
            AuditLayout();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        private void EditorTick()
        {
            RefreshTelemetry();
        }

        private void RunBenchmark()
        {
            PushScalarFallback();
            if (BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime))
                runtime.GenerateMockSimdBenchmark();

            RefreshTelemetry();
        }

        private void AuditLayout()
        {
            int write = 0;
            AppendLiteral(_readoutBuffer, ref write, "ARM64 SIMD layout audit\n");
            AppendLiteral(_readoutBuffer, ref write, SimdVectorizationLayout.Validate() ? "Validate: OK\n" : "Validate: FAIL\n");
            AppendLayout<SimdFloat3Padded>(_readoutBuffer, ref write, "SimdFloat3Padded");
            AppendLayout<SimdHydrodynamicTuningDTO>(_readoutBuffer, ref write, "SimdHydrodynamicTuningDTO");
            AppendLayout<SimdTelemetryEntry>(_readoutBuffer, ref write, "SimdTelemetryEntry");
            AppendLayout<SimdMathToleranceDTO>(_readoutBuffer, ref write, "SimdMathToleranceDTO");
            AppendLiteral(_readoutBuffer, ref write, "Benchmark lanes: ");
            AppendInt(_readoutBuffer, ref write, SimdVectorizationConstants.BenchmarkEntityCount);
            AppendLiteral(_readoutBuffer, ref write, "\nTelemetry ring: ");
            AppendInt(_readoutBuffer, ref write, SimdVectorizationConstants.TelemetryCapacity);
            _statusLabel.text = new string(_readoutBuffer, 0, write);
        }

        private void LoadToleranceCsv()
        {
            bool ok = BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) &&
                      runtime.TryLoadSimdMathTolerancesCsv();
            _statusLabel.text = ok ? "SIMD tolerance CSV loaded." : "SIMD tolerance CSV not loaded.";
        }

        private void RefreshTelemetry()
        {
            if (_statusLabel == null)
                return;

            if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                !runtime.TryOpenSimdEditorViews(
                    out NativeArray<SimdTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
                    out _))
            {
                if (!_runtimeUnavailableStatusShown)
                    _statusLabel.text = "Play Mode runtime and GlobalDataVault required.";
                _runtimeUnavailableStatusShown = true;
                _telemetryStatusShown = false;
                return;
            }

            if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                return;

            _runtimeUnavailableStatusShown = false;
            if (!_telemetryStatusShown)
            {
                _statusLabel.text = "SIMD telemetry bars read the Vault ring without rebuilding text every editor tick.";
                _telemetryStatusShown = true;
            }

            PullSimdTuning();

            int head = math.max(0, cursor[0]);
            int slot = math.max(0, head - 1) % telemetry.Length;
            SimdTelemetryEntry entry = telemetry[slot];
            int vectorCentis = ToCentis(entry.VectorMicros);
            int throughputCentis = ToCentis(entry.EntitiesPerMillisecond);
            if (head == _lastCursor &&
                entry.EntityCount == _lastEntityCount &&
                vectorCentis == _lastVectorCentis &&
                throughputCentis == _lastThroughputCentis)
            {
                return;
            }

            _lastCursor = head;
            _lastEntityCount = entry.EntityCount;
            _lastVectorCentis = vectorCentis;
            _lastThroughputCentis = throughputCentis;
            SetBarWidth(_vectorBar, entry.VectorMicros);
            SetBarWidth(_scalarBar, entry.ScalarMicros);
            SetScaledBarWidth(_throughputBar, entry.EntitiesPerMillisecond, 0.0002f);
        }

        private void PullSimdTuning()
        {
            if (_scalarFallbackSlider == null)
                return;

            if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                !runtime.TryOpenSimdTuningEditorView(out NativeArray<SimdHydrodynamicTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            int scalarCentis = ToCentis(tuning[0].ScalarFallbackWeight01);
            if (scalarCentis == _lastScalarFallbackCentis)
                return;

            _lastScalarFallbackCentis = scalarCentis;
            _scalarFallbackSlider.SetValueWithoutNotify(math.saturate(tuning[0].ScalarFallbackWeight01));
        }

        private void PushScalarFallback()
        {
            if (_scalarFallbackSlider == null)
                return;

            if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                !runtime.TryOpenSimdTuningEditorView(out NativeArray<SimdHydrodynamicTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            SimdHydrodynamicTuningDTO value = tuning[0];
            value.ScalarFallbackWeight01 = math.saturate(_scalarFallbackSlider.value);
            value.Flags = SimdVectorizationConstants.FlagActive;
            tuning[0] = value;
            _lastScalarFallbackCentis = ToCentis(value.ScalarFallbackWeight01);
        }

        private void OnScalarFallbackChanged(ChangeEvent<float> changeEvent)
        {
            PushScalarFallback();
        }

        private static VisualElement CreateBar(float r, float g, float b)
        {
            VisualElement bar = new VisualElement();
            bar.style.height = 6;
            bar.style.width = 1;
            bar.style.marginBottom = 4;
            bar.style.backgroundColor = new UnityEngine.Color(r, g, b, 1f);
            return bar;
        }

        private static void SetBarWidth(VisualElement bar, float micros)
        {
            if (bar == null)
                return;

            float width = math.clamp(math.select(1f, micros * 0.05f, math.isfinite(micros)), 1f, 320f);
            bar.style.width = width;
        }

        private static void SetScaledBarWidth(VisualElement bar, float value, float scale)
        {
            if (bar == null)
                return;

            float width = math.clamp(math.select(1f, value * scale, math.isfinite(value)), 1f, 320f);
            bar.style.width = width;
        }

        private static void AppendLayout<T>(char[] buffer, ref int offset, string label) where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            int align = UnsafeUtility.AlignOf<T>();
            AppendLiteral(buffer, ref offset, label);
            AppendLiteral(buffer, ref offset, ": size ");
            AppendInt(buffer, ref offset, size);
            AppendLiteral(buffer, ref offset, " align ");
            AppendInt(buffer, ref offset, align);
            AppendLiteral(buffer, ref offset, " lane16 ");
            AppendLiteral(buffer, ref offset, (size & 15) == 0 ? "OK" : "FAIL");
            AppendLiteral(buffer, ref offset, "\n");
        }

        private static int ToCentis(float value)
        {
            if (!math.isfinite(value))
                return 0;

            return (int)math.round(value * 100f);
        }

        private static void AppendLiteral(char[] buffer, ref int offset, string value)
        {
            for (int i = 0; i < value.Length && offset < buffer.Length; i++)
                buffer[offset++] = value[i];
        }

        private static void AppendInt(char[] buffer, ref int offset, int value)
        {
            if (offset >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[offset++] = '0';
                return;
            }

            long remaining = value;
            if (remaining < 0L)
            {
                buffer[offset++] = '-';
                remaining = -remaining;
            }

            int start = offset;
            while (remaining > 0L && offset < buffer.Length)
            {
                buffer[offset++] = (char)('0' + remaining % 10L);
                remaining /= 10L;
            }

            int end = offset - 1;
            while (start < end)
            {
                char swap = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = swap;
                start++;
                end--;
            }
        }

        private static void AppendFixed2(char[] buffer, ref int offset, int centis)
        {
            if (offset >= buffer.Length)
                return;

            if (centis == 0)
            {
                AppendLiteral(buffer, ref offset, "0.00");
                return;
            }

            int scaled = math.abs(centis);
            if (centis < 0)
                AppendLiteral(buffer, ref offset, "-");

            AppendInt(buffer, ref offset, scaled / 100);
            AppendLiteral(buffer, ref offset, ".");
            int fractional = scaled % 100;
            if (offset < buffer.Length)
                buffer[offset++] = (char)('0' + fractional / 10);
            if (offset < buffer.Length)
                buffer[offset++] = (char)('0' + fractional % 10);
        }

        private static void AppendHex8(char[] buffer, ref int offset, uint value)
        {
            const string hex = "0123456789ABCDEF";
            for (int shift = 28; shift >= 0 && offset < buffer.Length; shift -= 4)
                buffer[offset++] = hex[(int)((value >> shift) & 0xFu)];
        }
    }
}
#endif
