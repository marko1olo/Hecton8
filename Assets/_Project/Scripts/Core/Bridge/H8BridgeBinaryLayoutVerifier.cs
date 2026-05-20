using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    /// <summary>
    /// Cold boot layout sentinel for Bridge DTOs consumed by DataVault, SignalBus, and MacroDB.
    /// </summary>
    public static class H8BridgeBinaryLayoutVerifier
    {
        private const uint SizeHash = 0x53495A45u;
        private const uint OffsetHash = 0x4F464653u;
        private const uint BlitHash = 0x424C4954u;
        private const uint AttributeHash = 0x41545452u;
        private static bool _verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            _verified = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void VerifyBeforeSceneLoad()
        {
            VerifyColdBoot();
        }

        /// <summary>
        /// Verifies Bridge payload sizes and offsets once before runtime consumers read packed buffers.
        /// </summary>
        public static void VerifyColdBoot()
        {
            if (_verified)
                return;

            VerifyPrefabMapping();
            VerifyPrefabLoreLink();
            VerifyDesignValue();
            VerifyFacadeTelemetry();
            VerifyFacadeTelemetryDumpHeader();
            VerifyInputBinding();
            VerifyMacroHeader();
            VerifyDataVaultSignal();
            VerifyAcousticSignal();
            VerifyLoreSignal();

            _verified = true;
        }

        private static void VerifyPrefabMapping()
        {
            AssertSize<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, 64);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.HashID), 0);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.AddressHash), 4);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.LoreHash), 8);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.AcousticSignatureHash), 12);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.EstimatedVramBytes), 16);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.RuntimePrefabId), 24);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.Flags), 28);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.OneDimensionalLutHash), 32);
            AssertOffset<H8PrefabMappingEntry>(H8BridgeTypeHashes.PrefabMapping, nameof(H8PrefabMappingEntry.HighTierVisualHash), 36);
        }

        private static void VerifyPrefabLoreLink()
        {
            AssertSize<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, 32);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.PrefabHash), 0);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.LoreHash), 4);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.AcousticSignatureHash), 8);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.OneDimensionalLutHash), 12);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.HighTierVisualHash), 16);
            AssertOffset<H8PrefabLoreLinkEntry>(H8BridgeTypeHashes.PrefabLoreLink, nameof(H8PrefabLoreLinkEntry.Flags), 20);
        }

        private static void VerifyDesignValue()
        {
            AssertSize<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, 32);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.FieldHash), 0);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.OffsetBytes), 4);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.Value), 8);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.SafeDefault), 12);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.MinValue), 16);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.MaxValue), 20);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.LutSwapHash), 24);
            AssertOffset<H8DesignValueEntry>(H8BridgeTypeHashes.DesignValue, nameof(H8DesignValueEntry.Flags), 28);
        }

        private static void VerifyFacadeTelemetry()
        {
            AssertSize<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, 64);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.Frame), 0);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.FacadeHash), 4);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.FieldHash), 8);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.OffsetBytes), 12);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.OldValue), 16);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.NewValue), 20);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.SafeDefault), 24);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.LutSwapHash), 28);
            AssertOffset<H8FacadeTelemetryEntry>(H8BridgeTypeHashes.FacadeTelemetry, nameof(H8FacadeTelemetryEntry.Flags), 32);
        }

        private static void VerifyFacadeTelemetryDumpHeader()
        {
            AssertSize<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, 32);
            AssertOffset<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, nameof(H8FacadeTelemetryDumpHeader.Magic), 0);
            AssertOffset<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, nameof(H8FacadeTelemetryDumpHeader.EntryCount), 8);
            AssertOffset<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, nameof(H8FacadeTelemetryDumpHeader.EntrySizeBytes), 12);
            AssertOffset<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, nameof(H8FacadeTelemetryDumpHeader.Cursor), 16);
            AssertOffset<H8FacadeTelemetryDumpHeader>(H8BridgeTypeHashes.FacadeTelemetryDumpHeader, nameof(H8FacadeTelemetryDumpHeader.PayloadHash), 24);
        }

        private static void VerifyInputBinding()
        {
            AssertSize<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, 32);
            AssertOffset<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, nameof(H8InputFacadeBindingEntry.ActionNameHash), 0);
            AssertOffset<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, nameof(H8InputFacadeBindingEntry.ButtonMask), 4);
            AssertOffset<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, nameof(H8InputFacadeBindingEntry.PlayerCommand), 8);
            AssertOffset<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, nameof(H8InputFacadeBindingEntry.Flags), 9);
            AssertOffset<H8InputFacadeBindingEntry>(H8BridgeTypeHashes.InputBinding, nameof(H8InputFacadeBindingEntry.DisplayGroupHash), 12);
        }

        private static void VerifyMacroHeader()
        {
            AssertSize<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, 64);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.Magic), 0);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.LastChangedFieldHash), 12);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.Checksum), 28);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.EstimatedVramBytes), 40);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.OneDimensionalLutHash), 48);
            AssertOffset<H8FacadeMacroHeader>(H8BridgeTypeHashes.MacroHeader, nameof(H8FacadeMacroHeader.HighTierVisualHash), 52);
        }

        private static void VerifyDataVaultSignal()
        {
            AssertSize<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, 32);
            AssertOffset<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, nameof(DataVaultUpdateSignal.SourceHash), 0);
            AssertOffset<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, nameof(DataVaultUpdateSignal.OffsetBytes), 8);
            AssertOffset<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, nameof(DataVaultUpdateSignal.OldValue), 12);
            AssertOffset<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, nameof(DataVaultUpdateSignal.NewValue), 16);
            AssertOffset<DataVaultUpdateSignal>(H8BridgeTypeHashes.DataVaultSignal, nameof(DataVaultUpdateSignal.BufferId), 24);
        }

        private static void VerifyAcousticSignal()
        {
            AssertSize<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, 32);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.PrefabHash), 0);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.AcousticSignatureHash), 4);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.LoreHash), 8);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.Resonance01), 16);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.OneDimensionalLutHash), 20);
            AssertOffset<PrefabAcousticSignatureSignal>(H8BridgeTypeHashes.AcousticSignal, nameof(PrefabAcousticSignatureSignal.Flags), 24);
        }

        private static void VerifyLoreSignal()
        {
            AssertSize<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, 32);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.PrefabHash), 0);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.LoreHash), 4);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.Frame), 8);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.OneDimensionalLutHash), 12);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.HighTierVisualHash), 16);
            AssertOffset<PrefabLoreLinkSignal>(H8BridgeTypeHashes.LoreSignal, nameof(PrefabLoreLinkSignal.Flags), 20);
        }

        private static void AssertSize<T>(uint typeHash, int expected) where T : unmanaged
        {
            AssertBinarySafe<T>(typeHash);
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                Fail(typeHash, expected, observed, SizeHash);
        }

        private static void AssertOffset<T>(uint typeHash, string fieldName, int expected) where T : unmanaged
        {
            int observed = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (observed != expected)
                Fail(H8BridgeHashes.Mix(typeHash, H8BridgeHashes.ComputeFnv1A(fieldName)), expected, observed, OffsetHash);
        }

        private static void AssertBinarySafe<T>(uint typeHash) where T : unmanaged
        {
            if (!UnsafeUtility.IsBlittable<T>())
                Fail(typeHash, 1, 0, BlitHash);

            if (!MemoryInquisitor.PrewarmBinaryBlittableSafety<T>())
                Fail(typeHash, 1, 0, AttributeHash);
        }

        private static void Fail(uint typeHash, int expected, int observed, uint reasonHash)
        {
            uint contextHash = H8BridgeHashes.Mix(H8BridgeHashes.BridgeLayoutFault, typeHash);
            contextHash = H8BridgeHashes.Mix(contextHash, reasonHash);
            GlobalTelemetryBus.PublishPerformanceWarning(H8BridgeHashes.BridgeLayoutFault, contextHash, observed);
            H8BridgeFacadeRuntime.RequestBlackBoxDump();
            throw new global::Hecton8.Core.CriticalBootException("[H8BridgeBinaryLayoutVerifier] Bridge binary layout mismatch.");
        }
    }

    internal static class H8BridgeTypeHashes
    {
        public const uint PrefabMapping = 0xB8F00001u;
        public const uint PrefabLoreLink = 0xB8F00002u;
        public const uint DesignValue = 0xB8F00003u;
        public const uint FacadeTelemetry = 0xB8F00004u;
        public const uint InputBinding = 0xB8F00005u;
        public const uint MacroHeader = 0xB8F00006u;
        public const uint DataVaultSignal = 0xB8F00007u;
        public const uint AcousticSignal = 0xB8F00008u;
        public const uint LoreSignal = 0xB8F00009u;
        public const uint FacadeTelemetryDumpHeader = 0xB8F0000Au;
    }
}
