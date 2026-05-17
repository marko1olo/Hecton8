using System;
using System.Collections.Generic;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    [CreateAssetMenu(fileName = "H8DesignData", menuName = "Hecton-8/Bridge/Design Data Facade")]
    public sealed class H8DesignDataFacade : ScriptableObject
    {
        [Serializable]
        public sealed class FloatBinding
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private string displayName = "SubSpeed";
            [SerializeField] private uint fieldHash;
            [SerializeField] private int offsetBytes;
            [SerializeField] private float value = 1f;
            [SerializeField] private float safeDefault = 1f;
            [SerializeField] private float minValue = 0f;
            [SerializeField] private float maxValue = 100f;
            [SerializeField] private bool critical;
            [SerializeField] private bool liveTuning = true;
            [SerializeField] private bool affectsVram;
            [SerializeField] private int textureWidth = 1024;
            [SerializeField] private int textureHeight = 1024;
            [SerializeField] private int textureMipCount = 8;
            [SerializeField] private int textureBytesPerPixel = 4;
            [SerializeField] private uint oneDimensionalLutHash;
            [SerializeField] private uint highTierVisualHash;
            [SerializeField] private float lastAppliedValue;
            [SerializeField] private uint lastAppliedFieldHash;
            [SerializeField] private int lastAppliedOffsetBytes;

            public bool Enabled => enabled;
            public string DisplayName => displayName;
            public uint FieldHash => fieldHash;
            public int OffsetBytes => offsetBytes;
            public float Value => value;
            public float SafeDefault => safeDefault;
            public float MinValue => minValue;
            public float MaxValue => maxValue;
            public bool Critical => critical;
            public bool LiveTuning => liveTuning;
            public bool AffectsVram => affectsVram;
            public int TextureWidth => textureWidth;
            public int TextureHeight => textureHeight;
            public int TextureMipCount => textureMipCount;
            public int TextureBytesPerPixel => textureBytesPerPixel;
            public uint OneDimensionalLutHash => oneDimensionalLutHash;
            public uint HighTierVisualHash => highTierVisualHash;
            public float LastAppliedValue => lastAppliedValue;

            public void ConfigureDefaults(string name, int offset, float defaultValue, float min, float max, bool isCritical)
            {
                displayName = name;
                offsetBytes = math.max(0, offset);
                value = defaultValue;
                safeDefault = defaultValue == 0f ? 1f : defaultValue;
                minValue = min;
                maxValue = max;
                critical = isCritical;
                liveTuning = true;
                RebuildHash();
                lastAppliedValue = value;
                lastAppliedFieldHash = fieldHash;
                lastAppliedOffsetBytes = offsetBytes;
            }

            public void ConfigureVisualDefaults(
                string name,
                int offset,
                float defaultValue,
                float min,
                float max,
                int vramWidth,
                int vramHeight,
                int vramMipCount,
                int vramBytesPerPixel)
            {
                ConfigureDefaults(name, offset, defaultValue, min, max, false);
                affectsVram = vramWidth > 0 && vramHeight > 0;
                textureWidth = math.max(1, vramWidth);
                textureHeight = math.max(1, vramHeight);
                textureMipCount = math.max(1, vramMipCount);
                textureBytesPerPixel = math.max(1, vramBytesPerPixel);
                oneDimensionalLutHash = H8BridgeHashes.ComputeFnv1A(name, H8BridgeHashes.LutSeed);
                highTierVisualHash = H8BridgeHashes.ComputeFnv1A(name, H8BridgeHashes.VisualOverkillSeed);
            }

            public bool SanitizeAndDetectChange(out float oldSerializedValue)
            {
                oldSerializedValue = lastAppliedValue;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = "DesignValue";

                uint previousHash = lastAppliedFieldHash;
                int previousOffset = lastAppliedOffsetBytes;
                RebuildHash();
                offsetBytes = H8BridgeFacadeRuntime.AlignFloatOffsetBytes(offsetBytes);
                textureWidth = math.max(1, textureWidth);
                textureHeight = math.max(1, textureHeight);
                textureMipCount = math.max(1, textureMipCount);
                textureBytesPerPixel = math.max(1, textureBytesPerPixel);

                if (!math.isfinite(safeDefault) || (critical && math.abs(safeDefault) <= float.Epsilon))
                    safeDefault = 1f;

                if (!math.isfinite(minValue))
                    minValue = value;
                if (!math.isfinite(maxValue))
                    maxValue = math.max(minValue + 1f, value);
                if (maxValue <= minValue)
                    maxValue = minValue + 1f;

                bool invalid = !math.isfinite(value) || (critical && math.abs(value) <= float.Epsilon);
                if (invalid)
                    value = safeDefault;

                value = math.clamp(value, minValue, maxValue);
                bool changed = !Mathf.Approximately(value, lastAppliedValue) ||
                    fieldHash != previousHash ||
                    offsetBytes != previousOffset;
                lastAppliedValue = value;
                lastAppliedFieldHash = fieldHash;
                lastAppliedOffsetBytes = offsetBytes;
                return changed;
            }

            public H8DesignValueEntry ToValueEntry(bool designerOverride)
            {
                ushort flags = H8DesignValueFlags.None.ToMask();
                if (critical)
                    flags |= (ushort)H8DesignValueFlags.Critical;
                if (liveTuning)
                    flags |= (ushort)H8DesignValueFlags.LiveTuning;
                if (designerOverride)
                    flags |= (ushort)H8DesignValueFlags.DesignerOverride;
                if (affectsVram)
                    flags |= (ushort)H8DesignValueFlags.VramAffecting;
                if (oneDimensionalLutHash != 0u)
                    flags |= (ushort)H8DesignValueFlags.UsesOneDimensionalLut;
                if (highTierVisualHash != 0u)
                    flags |= (ushort)H8DesignValueFlags.HighTierVisualOverkill;

                return new H8DesignValueEntry
                {
                    FieldHash = fieldHash,
                    OffsetBytes = offsetBytes,
                    Value = value,
                    SafeDefault = safeDefault,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    LutSwapHash = oneDimensionalLutHash,
                    Flags = flags
                };
            }

            public long EstimateVramBytes()
            {
                return affectsVram
                    ? H8BridgeFacadeRuntime.EstimateTextureBytes(textureWidth, textureHeight, textureMipCount, textureBytesPerPixel)
                    : 0L;
            }

            private void RebuildHash()
            {
                fieldHash = H8BridgeHashes.ComputeFnv1A(displayName);
            }
        }

        [SerializeField] private uint facadeHash = H8BridgeHashes.DesignFacade;
        [SerializeField] private bool liveTuningEnabled = true;
        [SerializeField] private bool designerOverride;
        [SerializeField] private uint oneDimensionalLutHash;
        [SerializeField] private uint highTierVisualHash;
        [SerializeField] private List<FloatBinding> floatBindings = new List<FloatBinding>(32);
        [SerializeField] private uint lastChangedFieldHash;
        [SerializeField] private int lastAppliedBindingCount;

        public uint FacadeHash => facadeHash == 0u ? H8BridgeHashes.DesignFacade : facadeHash;
        public bool LiveTuningEnabled => liveTuningEnabled;
        public bool DesignerOverride => designerOverride;
        public uint OneDimensionalLutHash => oneDimensionalLutHash;
        public uint HighTierVisualHash => highTierVisualHash;
        public uint LastChangedFieldHash => lastChangedFieldHash;
        public int BindingCount => floatBindings != null ? floatBindings.Count : 0;

        public FloatBinding GetBinding(int index)
        {
            return floatBindings != null && index >= 0 && index < floatBindings.Count ? floatBindings[index] : null;
        }

        public long EstimateVramBytes()
        {
            long total = 0L;
            if (floatBindings == null)
                return total;

            for (int i = 0; i < floatBindings.Count; i++)
            {
                FloatBinding binding = floatBindings[i];
                if (binding != null)
                    total += binding.EstimateVramBytes();
            }

            return total;
        }

        public bool SyncToVault(IDataVault vault)
        {
            ushort flags = designerOverride ? (ushort)H8DesignValueFlags.DesignerOverride : (ushort)0;
            return H8BridgeFacadeRuntime.SyncDesignData(this, vault, flags);
        }

        private void Reset()
        {
            EnsureDefaultBindings();
            ValidateBindings(pushLive: false);
        }

        private void OnValidate()
        {
            ValidateBindings(pushLive: true);
        }

        [ContextMenu("Seed Default Design Bindings")]
        private void SeedDefaultBindings()
        {
            EnsureDefaultBindings();
            ValidateBindings(pushLive: false);
        }

        private void EnsureBindingList()
        {
            if (floatBindings == null)
                floatBindings = new List<FloatBinding>(32);
        }

        private void EnsureDefaultBindings()
        {
            EnsureBindingList();
            if (floatBindings.Count > 0)
                return;

            FloatBinding subSpeed = new FloatBinding();
            subSpeed.ConfigureDefaults("SubSpeed", 0, 12f, 0.1f, 80f, true);
            floatBindings.Add(subSpeed);

            FloatBinding addedMass = new FloatBinding();
            addedMass.ConfigureDefaults("AddedMass", 4, 1f, 0.01f, 25f, true);
            floatBindings.Add(addedMass);

            FloatBinding visorLut = new FloatBinding();
            visorLut.ConfigureVisualDefaults("VisorSaltCrystalLut01", 8, 1f, 0f, 1f, 1024, 1, 1, 4);
            floatBindings.Add(visorLut);

            FloatBinding triangleNoise = new FloatBinding();
            triangleNoise.ConfigureVisualDefaults("ToasterTriangleNoise01", 12, 0.35f, 0f, 1f, 256, 1, 1, 4);
            floatBindings.Add(triangleNoise);

            FloatBinding dotProductVision = new FloatBinding();
            dotProductVision.ConfigureDefaults("DotProductVisionMask01", 16, 0.75f, 0f, 1f, false);
            floatBindings.Add(dotProductVision);

            FloatBinding siltWake = new FloatBinding();
            siltWake.ConfigureVisualDefaults("VolumetricSiltWake01", 20, 1f, 0f, 1f, 512, 512, 6, 4);
            floatBindings.Add(siltWake);

            FloatBinding hullDents = new FloatBinding();
            hullDents.ConfigureVisualDefaults("ProceduralHullDents01", 24, 1f, 0f, 1f, 2048, 2048, 8, 4);
            floatBindings.Add(hullDents);

            FloatBinding raymarchSteps = new FloatBinding();
            raymarchSteps.ConfigureDefaults("RaymarchStepBudget", 28, 16f, 1f, 64f, false);
            floatBindings.Add(raymarchSteps);

            FloatBinding pomTaps = new FloatBinding();
            pomTaps.ConfigureDefaults("PomTapCount", 32, 16f, 1f, 16f, false);
            floatBindings.Add(pomTaps);

            FloatBinding subsurface = new FloatBinding();
            subsurface.ConfigureVisualDefaults("SubsurfaceScatterWeight01", 36, 0.85f, 0f, 1f, 1024, 1024, 6, 4);
            floatBindings.Add(subsurface);

            FloatBinding particleBudget = new FloatBinding();
            particleBudget.ConfigureDefaults("ParticleOverkillBudget01", 40, 1f, 0f, 1f, false);
            floatBindings.Add(particleBudget);

            FloatBinding saltCrystals = new FloatBinding();
            saltCrystals.ConfigureVisualDefaults("VisorSaltCrystalGrowth01", 44, 0.55f, 0f, 1f, 1024, 1024, 6, 4);
            floatBindings.Add(saltCrystals);
        }

        private void ValidateBindings(bool pushLive)
        {
            EnsureBindingList();
            if (facadeHash == 0u)
                facadeHash = H8BridgeHashes.DesignFacade;
            if (oneDimensionalLutHash == 0u)
                oneDimensionalLutHash = H8BridgeHashes.ComputeFnv1A(name, H8BridgeHashes.LutSeed);
            if (highTierVisualHash == 0u)
                highTierVisualHash = H8BridgeHashes.ComputeFnv1A(name, H8BridgeHashes.VisualOverkillSeed);

            bool changed = false;
            int previousBindingCount = lastAppliedBindingCount;
            for (int i = 0; i < floatBindings.Count; i++)
            {
                FloatBinding binding = floatBindings[i];
                if (binding == null)
                    continue;

                if (binding.SanitizeAndDetectChange(out _))
                {
                    changed = true;
                    lastChangedFieldHash = binding.FieldHash;
                }
            }

            if (floatBindings.Count != previousBindingCount)
            {
                changed = true;
                lastChangedFieldHash = H8BridgeHashes.BridgeHeartbeat;
                lastAppliedBindingCount = floatBindings.Count;
            }

            if (!pushLive || !changed || !liveTuningEnabled || !Application.isPlaying)
                return;

            if (!designerOverride && H8BridgeFacadeRuntime.LiveTuningBlockedByStress())
                return;

            SyncToVault(GlobalRegistry.DataVault);
        }
    }

    internal static class H8DesignValueFlagExtensions
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ushort ToMask(this H8DesignValueFlags flags)
        {
            return (ushort)flags;
        }
    }
}
