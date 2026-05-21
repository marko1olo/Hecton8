#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class Shinobu132CablePhysicsTunerWindow : EditorWindow
    {
        private Slider _quality;
        private Slider _gravity;
        private Slider _friction;
        private Slider _stretch;
        private Slider _breakForce;
        private Slider _rockFriction;
        private Slider _reelSpeed;
        private SliderInt _iterations;
        private SliderInt _splineSteps;
        private Label _status;
        private Label _telemetry;
        private double _nextTelemetryRefreshTime;

        [MenuItem("Hecton8/Physics/Abyssal Tether Tuner")]
        public static void Open()
        {
            GetWindow<Shinobu132CablePhysicsTunerWindow>("Abyssal Tether Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshTelemetry;
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _quality = new Slider("Global Quality", 0f, 1f) { value = HomeostasisBrain.GlobalQualityWeight };
            _gravity = new Slider("Gravity Y", -40f, 5f) { value = -9.80665f };
            _friction = new Slider("Fluid Friction", 0.90f, 0.995f) { value = 0.975f };
            _iterations = new SliderInt("Base Stiffness (Iterations)", 0, 15) { value = 0 };
            _splineSteps = new SliderInt("Spline Interpolation Steps", 10, 64) { value = 50 };
            _stretch = new Slider("Stretch Threshold", 0.001f, 0.5f) { value = 0.18f };
            _breakForce = new Slider("Break Force", 0f, 30000f) { value = 18000f };
            _rockFriction = new Slider("Rock Friction", 0f, 1f) { value = 0.58f };
            _reelSpeed = new Slider("Reel Speed", 0.5f, 40f) { value = 18f };
            _status = new Label("Vault not sampled.");
            _telemetry = new Label("Telemetry: --");

            root.Add(_quality);
            root.Add(_gravity);
            root.Add(_friction);
            root.Add(_iterations);
            root.Add(_splineSteps);
            root.Add(_stretch);
            root.Add(_breakForce);
            root.Add(_rockFriction);
            root.Add(_reelSpeed);
            root.Add(new Button(ApplyTuning) { text = "Apply Tuning" });
            root.Add(new Button(ReloadCsv) { text = "Reload cable_materials.csv" });
            root.Add(new Button(DumpCableSurgeon) { text = "Dump Cable Surgeon Ring" });
            root.Add(_telemetry);
            root.Add(_status);

            _quality.RegisterValueChangedCallback(evt =>
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(math.saturate(evt.newValue), true));
            PullFromVault();
            RefreshTelemetry();
        }

        private void PullFromVault()
        {
            if (!TryResolveTuning(out NativeArray<VerletCableTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            VerletCableTuningDTO dto = tuning[0];
            if (math.lengthsq(dto.Gravity) <= 0.000001f)
                dto.Gravity = new float3(0f, -9.80665f, 0f);
            if (dto.FluidFriction <= 0f)
                dto.FluidFriction = 0.975f;
            if (dto.StretchThreshold01 <= 0f)
                dto.StretchThreshold01 = 0.18f;
            if (dto.RockFriction01 <= 0f)
                dto.RockFriction01 = 0.58f;
            if (dto.ReelSpeedMetersPerSecond <= 0f)
                dto.ReelSpeedMetersPerSecond = 18f;
            if (dto.Reserved0 <= 0f)
                dto.Reserved0 = 50f;

            tuning[0] = dto;
            _gravity.SetValueWithoutNotify(dto.Gravity.y);
            _friction.SetValueWithoutNotify(dto.FluidFriction);
            _iterations.SetValueWithoutNotify(math.clamp(dto.ConstraintIterations, 0, 15));
            _splineSteps.SetValueWithoutNotify(math.clamp((int)math.round(dto.Reserved0), 10, 64));
            _stretch.SetValueWithoutNotify(dto.StretchThreshold01);
            _breakForce.SetValueWithoutNotify(math.max(0f, dto.BreakForce));
            _rockFriction.SetValueWithoutNotify(dto.RockFriction01);
            _reelSpeed.SetValueWithoutNotify(dto.ReelSpeedMetersPerSecond);
            _status.text = "Vault sampled.";
        }

        private void ApplyTuning()
        {
            if (!TryResolveTuning(out NativeArray<VerletCableTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            tuning[0] = new VerletCableTuningDTO
            {
                Gravity = new float3(0f, _gravity.value, 0f),
                FluidFriction = math.saturate(_friction.value),
                ConstraintIterations = math.clamp(_iterations.value, 0, 15),
                StretchThreshold01 = math.max(0.001f, _stretch.value),
                BreakForce = math.max(0f, _breakForce.value),
                RockFriction01 = math.saturate(_rockFriction.value),
                ReelSpeedMetersPerSecond = math.max(0.001f, _reelSpeed.value),
                Reserved0 = math.clamp(_splineSteps.value, 10, 64)
            };
            _status.text = "SHINOBU_132 tuning written.";
        }

        private void ReloadCsv()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "Data", "cable_materials.csv");
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "cable_materials.csv");
            if (!File.Exists(path))
            {
                _status.text = "cable_materials.csv not found.";
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            if (!CablePhysicsSolver132.TryOpenOrAcquireMaterialView(vault, out NativeArray<CableMaterialDTO> materials))
            {
                _status.text = "Cable material Vault lane unavailable.";
                return;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0L || info.Length > 1048576L)
            {
                _status.text = "CSV rejected by size gate.";
                return;
            }

            using (NativeArray<byte> csvBytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                int bytesRead = ReadFileIntoNativeScratch(path, csvBytes);
                int parsed = ParseCsvBytes(csvBytes, bytesRead, materials);
                _status.text = parsed > 0 ? "CSV materials applied to SHINOBU_132." : "CSV parsed no rows.";
            }
        }

        private void DumpCableSurgeon()
        {
            bool dumped = CablePhysicsSolver132.TryDumpCableSurgeon(GlobalRegistry.DataVault, 0x5348494Eu);
            _status.text = dumped ? "Dump_SHINOBU_132.bin written." : "Cable surgeon dump unavailable.";
        }

        private void RefreshTelemetry()
        {
            if (_telemetry == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + 0.25d;
            if (!CablePhysicsSolver132.TrySampleLatestTelemetry(GlobalRegistry.DataVault, out TetherTelemetryEntry entry))
            {
                _telemetry.text = "Telemetry: --";
                return;
            }

            _telemetry.text =
                "Telemetry: nodes " + entry.NodeCount +
                " | iters " + entry.IterationCount +
                " | tension " + entry.MaxTension.ToString("F2") +
                " | us " + entry.CpuMicroseconds.ToString("F2") +
                " | hash 0x" + entry.StateHash.ToString("X8");
        }

        private static bool TryResolveTuning(out NativeArray<VerletCableTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            CablePhysicsSolver132.EnsureMockBuffers(vault, HomeostasisBrain.GlobalQualityWeight, 0u);
            if (!CablePhysicsSolver132.TryOpenOrAcquireTuningView(vault, out tuning))
                return false;

            return tuning.IsCreated && tuning.Length > 0;
        }

        private static unsafe int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                return stream.Read(new Span<byte>(pointer, scratch.Length));
            }
        }

        private static unsafe int ParseCsvBytes(NativeArray<byte> csvBytes, int byteCount, NativeArray<CableMaterialDTO> materials)
        {
            if (!csvBytes.IsCreated || byteCount <= 0)
                return 0;

            byte* pointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csvBytes);
            return CableMaterialCsvParser.ParseHashTable(new ReadOnlySpan<byte>(pointer, math.min(byteCount, csvBytes.Length)), materials);
        }
    }
}
#endif
