#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Cartography;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Cartography.Editor
{
    [InitializeOnLoad]
    internal static class CartographyMemorySovereigntyValidator1321
    {
        private const uint FailureLayout = 1u << 0;
        private const uint FailureHandle = 1u << 1;

        static CartographyMemorySovereigntyValidator1321()
        {
            ValidateOrThrow();
        }

        [MenuItem("HECTON-8/PDA/Run Cartography Memory Sovereignty Validator 1321")]
        private static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        internal static void ValidateOrThrow()
        {
            uint failureFlags = 0u;
            AssertExplicit<CartographyAup>(40, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.GridX), 0, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.GridY), 8, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.GridZ), 16, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.LocalX), 24, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.LocalY), 28, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.LocalZ), 32, ref failureFlags);
            AssertOffset<CartographyAup>(nameof(CartographyAup.Reserved), 36, ref failureFlags);

            AssertExplicit<MapRevealSignal>(56, ref failureFlags);
            AssertOffset<MapRevealSignal>(nameof(MapRevealSignal.Center), 0, ref failureFlags);
            AssertOffset<MapRevealSignal>(nameof(MapRevealSignal.RadiusMeters), 40, ref failureFlags);
            AssertOffset<MapRevealSignal>(nameof(MapRevealSignal.SourceId), 44, ref failureFlags);
            AssertOffset<MapRevealSignal>(nameof(MapRevealSignal.Flags), 48, ref failureFlags);
            AssertOffset<MapRevealSignal>("_pad0", 49, ref failureFlags);
            AssertOffset<MapRevealSignal>("_pad6", 55, ref failureFlags);

            AssertExplicit<CartographyPoiRecord>(48, ref failureFlags);
            AssertOffset<CartographyPoiRecord>(nameof(CartographyPoiRecord.Position), 0, ref failureFlags);
            AssertOffset<CartographyPoiRecord>(nameof(CartographyPoiRecord.Kind), 40, ref failureFlags);
            AssertOffset<CartographyPoiRecord>(nameof(CartographyPoiRecord.Hash), 44, ref failureFlags);

            AssertExplicit<CartographySectorDTO>(32, ref failureFlags);
            AssertOffset<CartographySectorDTO>(nameof(CartographySectorDTO.SectorHash), 0, ref failureFlags);
            AssertOffset<CartographySectorDTO>(nameof(CartographySectorDTO.BaseDataOffset), 8, ref failureFlags);
            AssertOffset<CartographySectorDTO>(nameof(CartographySectorDTO.DiscoveredVoxelCount), 12, ref failureFlags);
            AssertOffset<CartographySectorDTO>(nameof(CartographySectorDTO.Flags), 16, ref failureFlags);
            AssertOffset<CartographySectorDTO>("_pad0", 20, ref failureFlags);
            AssertOffset<CartographySectorDTO>("_pad11", 31, ref failureFlags);

            AssertExplicit<CartographyCounterDTO>(64, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastSectorHash), 0, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.Changed), 8, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.DiscoveredDelta), 12, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.Revision), 16, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastBitIndex), 20, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.TotalDiscoveredVoxels), 24, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.PendingSignalCount), 28, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastRleRunCount), 32, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastRleCompressionPermille), 36, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastMutationMicroseconds), 40, ref failureFlags);
            AssertOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastFailureFlags), 44, ref failureFlags);
            AssertOffset<CartographyCounterDTO>("_pad0", 48, ref failureFlags);
            AssertOffset<CartographyCounterDTO>("_pad15", 63, ref failureFlags);

            AssertExplicit<CartographyTelemetryEntry>(64, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerGridX), 0, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerGridY), 8, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerGridZ), 16, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerLocalX), 24, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerLocalY), 28, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerLocalZ), 32, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.GlobalQualityWeight), 36, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.FrameIndex), 40, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.Revision), 44, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.StateHash), 48, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.MutationMicroseconds), 52, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.RevealedSignalCount), 56, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.RevealedPoiCount), 58, ref failureFlags);
            AssertOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.MapFlags), 60, ref failureFlags);

            AssertExplicit<CartographyStateDTO>(32, ref failureFlags);
            AssertOffset<CartographyStateDTO>(nameof(CartographyStateDTO.LastUpdatedAUP), 0, ref failureFlags);
            AssertOffset<CartographyStateDTO>(nameof(CartographyStateDTO.UpdatedVoxelCount), 24, ref failureFlags);
            AssertOffset<CartographyStateDTO>(nameof(CartographyStateDTO.MapFlags), 28, ref failureFlags);

            AssertExplicit<CartographyTuningDTO>(64, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.SonarPingRadiusMeters), 0, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.SurfaceThicknessMeters), 4, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.VisualGlowIntensity), 8, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.GlobalQualityWeight), 12, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.CellSizeMeters), 16, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.UploadCadenceFrames), 20, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.Flags), 24, ref failureFlags);
            AssertOffset<CartographyTuningDTO>(nameof(CartographyTuningDTO.Revision), 28, ref failureFlags);
            AssertOffset<CartographyTuningDTO>("_pad0", 32, ref failureFlags);
            AssertOffset<CartographyTuningDTO>("_pad31", 63, ref failureFlags);

            AssertExplicit<CartographyScannerProfileDTO>(32, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.UpgradeHash), 0, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.PingRadiusMeters), 4, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.DiscoveryResolutionMeters), 8, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.SurfaceThicknessMeters), 12, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.VisualGlowIntensity), 16, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>(nameof(CartographyScannerProfileDTO.Flags), 20, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>("_pad0", 24, ref failureFlags);
            AssertOffset<CartographyScannerProfileDTO>("_pad7", 31, ref failureFlags);

            AssertExplicit<CartographyRleRunDTO>(16, ref failureFlags);
            AssertOffset<CartographyRleRunDTO>(nameof(CartographyRleRunDTO.WordValue), 0, ref failureFlags);
            AssertOffset<CartographyRleRunDTO>(nameof(CartographyRleRunDTO.StartWordIndex), 8, ref failureFlags);
            AssertOffset<CartographyRleRunDTO>(nameof(CartographyRleRunDTO.WordCount), 12, ref failureFlags);
            AssertOffset<CartographyRleRunDTO>(nameof(CartographyRleRunDTO.Flags), 14, ref failureFlags);

            AssertExplicit<CartographyDebugVoxelDTO>(16, ref failureFlags);
            AssertOffset<CartographyDebugVoxelDTO>(nameof(CartographyDebugVoxelDTO.X), 0, ref failureFlags);
            AssertOffset<CartographyDebugVoxelDTO>(nameof(CartographyDebugVoxelDTO.Y), 4, ref failureFlags);
            AssertOffset<CartographyDebugVoxelDTO>(nameof(CartographyDebugVoxelDTO.Z), 8, ref failureFlags);
            AssertOffset<CartographyDebugVoxelDTO>(nameof(CartographyDebugVoxelDTO.Flags), 12, ref failureFlags);

            if (UnsafeUtility.SizeOf<VaultGenerationHandle<ulong>>() != 16 ||
                UnsafeUtility.SizeOf<VaultGenerationHandle<int>>() != 16 ||
                UnsafeUtility.SizeOf<VaultGenerationHandle<uint>>() != 16 ||
                UnsafeUtility.SizeOf<VaultGenerationHandle<CartographyTelemetryEntry>>() != 16)
            {
                failureFlags |= FailureHandle;
            }

            if (failureFlags != 0u || !CartographyLayoutVerifier.ValidateRuntimeLayouts())
                throw new FatalArchitectureException("1321 cartography memory sovereignty validator failed flags=" + failureFlags);
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureFlags)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }
    }
}
#endif
