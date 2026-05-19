using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed class DrsContractsEditTests
    {
        [Test]
        public void DrsStateDto_Arm64Layout_IsExact()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<DrsStateDTO>());
            Assert.AreEqual(0, OffsetOf<DrsStateDTO>(nameof(DrsStateDTO.CurrentRenderScale)));
            Assert.AreEqual(4, OffsetOf<DrsStateDTO>(nameof(DrsStateDTO.TargetRenderScale)));
            Assert.AreEqual(8, OffsetOf<DrsStateDTO>(nameof(DrsStateDTO.UpscalerTypeHash)));
            Assert.AreEqual(12, OffsetOf<DrsStateDTO>(nameof(DrsStateDTO._pad0)));
        }

        [Test]
        public void ResolutionScaleState_GlobalQualityWeight_DoesNotGrowContract()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ResolutionScaleState>());
            Assert.AreEqual(52, OffsetOf<ResolutionScaleState>(nameof(ResolutionScaleState.GlobalQualityWeight01)));
            Assert.AreEqual(56, OffsetOf<ResolutionScaleState>(nameof(ResolutionScaleState.Reserved5)));
            Assert.AreEqual(60, OffsetOf<ResolutionScaleState>(nameof(ResolutionScaleState.Reserved6)));
        }

        [Test]
        public void UberNoirReconstructionConstants_Arm64AndGpuLayout_IsExact()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<UberNoirReconstructionConstantsDTO>());
            Assert.AreEqual(0, OffsetOf<UberNoirReconstructionConstantsDTO>(nameof(UberNoirReconstructionConstantsDTO.RenderScaleParams)));
            Assert.AreEqual(16, OffsetOf<UberNoirReconstructionConstantsDTO>(nameof(UberNoirReconstructionConstantsDTO.TemporalParams)));
            Assert.AreEqual(32, OffsetOf<UberNoirReconstructionConstantsDTO>(nameof(UberNoirReconstructionConstantsDTO.OverkillParams)));
        }

        [Test]
        public void MockReconstructionInputSignal_Arm64Layout_IsExact()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<MockReconstructionInputSignal>());
            Assert.AreEqual(0, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.RenderScale01)));
            Assert.AreEqual(4, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.GlobalQualityWeight01)));
            Assert.AreEqual(8, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.JitterPixels)));
            Assert.AreEqual(12, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.FrameTimeMs)));
            Assert.AreEqual(16, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.TemporalStress01)));
            Assert.AreEqual(20, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal.Flags)));
            Assert.AreEqual(24, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal._pad0)));
            Assert.AreEqual(28, OffsetOf<MockReconstructionInputSignal>(nameof(MockReconstructionInputSignal._pad1)));
        }

        [Test]
        public void MockQualityWeightSignal_Arm64Layout_IsExact()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<MockQualityWeightSignal>());
            Assert.AreEqual(0, OffsetOf<MockQualityWeightSignal>(nameof(MockQualityWeightSignal.GlobalQualityWeight)));
            Assert.AreEqual(4, OffsetOf<MockQualityWeightSignal>(nameof(MockQualityWeightSignal.FrameTimeMs)));
            Assert.AreEqual(8, OffsetOf<MockQualityWeightSignal>(nameof(MockQualityWeightSignal.Flags)));
            Assert.AreEqual(12, OffsetOf<MockQualityWeightSignal>(nameof(MockQualityWeightSignal._pad0)));
        }

        [Test]
        public void UberNoirReconstructionVaultIds_AreStable()
        {
            Assert.AreEqual(71030, UberNoirReconstructionVaultIds.Constants);
            Assert.AreEqual(71031, UberNoirReconstructionVaultIds.Telemetry);
            Assert.AreEqual(71032, UberNoirReconstructionVaultIds.AestheticProfiles);
            Assert.AreEqual(71033, UberNoirReconstructionVaultIds.CsvScratch);
            Assert.AreEqual(71034, UberNoirReconstructionVaultIds.MockSignal);
        }

        [Test]
        public void ReconstructionTelemetryEntry_OneCacheLineLayout_IsExact()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ReconstructionTelemetryEntry>());
            Assert.AreEqual(0, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry.Frame)));
            Assert.AreEqual(4, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry.Flags)));
            Assert.AreEqual(8, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry.CurrentRenderScale01)));
            Assert.AreEqual(20, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry.BilateralRadiusPixels)));
            Assert.AreEqual(44, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry.UpscalerModeHash)));
            Assert.AreEqual(56, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry._pad0)));
            Assert.AreEqual(60, OffsetOf<ReconstructionTelemetryEntry>(nameof(ReconstructionTelemetryEntry._pad1)));
        }

        [Test]
        public void UberNoirShaderTelemetryEntry_Arm64Layout_IsExact()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>());
            Assert.AreEqual(0, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.Frame)));
            Assert.AreEqual(4, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.FeatureMask)));
            Assert.AreEqual(8, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.SystemStress01)));
            Assert.AreEqual(20, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.QualityTier)));
            Assert.AreEqual(32, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.PomEnabled01)));
            Assert.AreEqual(44, OffsetOf<HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry>(nameof(HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry.Reserved0)));
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
