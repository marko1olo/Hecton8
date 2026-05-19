#if UNITY_EDITOR
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class Shinobu143CablePhysicsTunerWindow : EditorWindow
    {
        private const int MaterialCapacity = 16;

        private Slider _quality;
        private Slider _gravity;
        private Slider _friction;
        private Slider _stretch;
        private Slider _breakForce;
        private Slider _rockFriction;
        private Slider _reelSpeed;
        private SliderInt _iterations;
        private Label _status;

        [MenuItem("Hecton8/Physics/SHINOBU 143 Cable Tuner")]
        public static void Open()
        {
            GetWindow<Shinobu143CablePhysicsTunerWindow>("SHINOBU 143 Cable");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _quality = BuildSlider("Global Quality", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _gravity = BuildSlider("Gravity Y", -40f, 5f, -9.80665f);
            _friction = BuildSlider("Fluid Friction", 0.90f, 0.995f, 0.975f);
            _iterations = new SliderInt("Constraint Iterations", 0, 15) { value = 0 };
            _stretch = BuildSlider("Stretch Threshold", 0.001f, 0.5f, 0.18f);
            _breakForce = BuildSlider("Break Force", 0f, 20000f, 0f);
            _rockFriction = BuildSlider("Rock Friction", 0f, 1f, 0.58f);
            _reelSpeed = BuildSlider("Reel Speed", 0.5f, 40f, 18f);
            _status = new Label("Vault not sampled.");

            root.Add(_quality);
            root.Add(_gravity);
            root.Add(_friction);
            root.Add(_iterations);
            root.Add(_stretch);
            root.Add(_breakForce);
            root.Add(_rockFriction);
            root.Add(_reelSpeed);
            root.Add(BuildButton("Apply Tuning", ApplyTuning));
            root.Add(BuildButton("Reload cable_materials.csv", ReloadCsv));
            root.Add(_status);

            _quality.RegisterValueChangedCallback(evt =>
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(math.saturate(evt.newValue), true));
            PullFromVault();
        }

        private static Slider BuildSlider(string label, float low, float high, float value)
        {
            return new Slider(label, low, high) { value = value };
        }

        private static Button BuildButton(string text, System.Action action)
        {
            return new Button(action) { text = text };
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

            tuning[0] = dto;
            _gravity.SetValueWithoutNotify(dto.Gravity.y);
            _friction.SetValueWithoutNotify(dto.FluidFriction);
            _iterations.SetValueWithoutNotify(math.clamp(dto.ConstraintIterations, 0, 15));
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
                Reserved0 = 0f,
                Reserved1 = 0f
            };
            _status.text = "Tuning written to Vault.";
        }

        private void ReloadCsv()
        {
            string path = ResolveCsvPath();
            if (!File.Exists(path))
            {
                _status.text = "cable_materials.csv not found.";
                return;
            }

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            NativeArray<CableMaterialDTO> legacyMaterials = vault.GetBufferHandle<CableMaterialDTO>(
                BufferID.VerletCableMaterials,
                MaterialCapacity,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory).Resolve(vault);
            NativeArray<CableMaterialDTO> shinobuMaterials = vault.GetBufferHandle<CableMaterialDTO>(
                BufferID.Shinobu143CableMaterials,
                MaterialCapacity,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory).Resolve(vault);
            int parsed = CableMaterialCsvParser.Parse(bytes.AsSpan(), legacyMaterials);
            if (shinobuMaterials.IsCreated)
                CableMaterialCsvParser.Parse(bytes.AsSpan(), shinobuMaterials);
            _status.text = parsed > 0 ? "CSV materials applied." : "CSV parsed no rows.";
        }

        private static bool TryResolveTuning(out NativeArray<VerletCableTuningDTO> tuning)
        {
            tuning = default;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return false;

            tuning = vault.GetBufferHandle<VerletCableTuningDTO>(
                BufferID.VerletCableTuning,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory).Resolve(vault);
            return tuning.IsCreated && tuning.Length > 0;
        }

        private static string ResolveCsvPath()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(root, "cable_materials.csv");
        }
    }
}
#endif
