using System.Runtime.InteropServices;
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

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
