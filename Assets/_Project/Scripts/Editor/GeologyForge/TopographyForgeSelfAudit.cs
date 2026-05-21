#if UNITY_EDITOR
using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.GeologyForge
{
    internal static unsafe class TopographyForgeSelfAudit
    {
        [MenuItem("HECTON-8/Geology Forge/Topography Forge/Self Audit", false, 188)]
        public static void RunAndWriteReport()
        {
            bool layoutValid = ValidateLayouts(out string layoutMessage);
            int filesChecked = 0;
            int filesValid = 0;
            string firstError = string.Empty;

            if (Directory.Exists(TopographyForgeConstants.SectorOutputFolder))
            {
                string[] files = Directory.GetFiles(TopographyForgeConstants.SectorOutputFolder, "*.h8bin", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    filesChecked++;
                    if (TryValidateTopographyFile(files[i], out string error))
                    {
                        filesValid++;
                    }
                    else if (string.IsNullOrEmpty(firstError))
                    {
                        firstError = error;
                    }
                }
            }

            Directory.CreateDirectory("Docs/Reports");
            StringBuilder builder = new StringBuilder(1024); // COLD ALLOC: self-audit JSON report builder - owner: SHINOBU_240
            builder.AppendLine("{");
            AppendJson(builder, "agent", "SHINOBU_240", true);
            AppendJson(builder, "layout_valid", layoutValid, true);
            AppendJson(builder, "layout_message", layoutMessage, true);
            AppendJson(builder, "files_checked", filesChecked, true);
            AppendJson(builder, "files_valid", filesValid, true);
            AppendJson(builder, "all_h8bin_valid", filesChecked > 0 && filesChecked == filesValid, true);
            AppendJson(builder, "h8bin_artifacts_present", filesChecked > 0, true);
            AppendJson(builder, "first_error", firstError, false);
            builder.AppendLine("}");
            File.WriteAllText(TopographyForgeConstants.LayoutAuditReportPath, builder.ToString());
            Debug.Log("[TopographyForgeSelfAudit] layout=" + layoutValid + ", h8bin=" + filesValid + "/" + filesChecked + ".");
        }

        internal static bool ValidateLayouts(out string message)
        {
            StringBuilder errors = new StringBuilder(512); // COLD ALLOC: layout audit error builder - owner: SHINOBU_240
            bool valid = true;
            valid &= RequireSize<FractalParamsDTO>(32, errors);
            valid &= RequireOffset<FractalParamsDTO>("Frequency", 0, errors);
            valid &= RequireOffset<FractalParamsDTO>("Amplitude", 4, errors);
            valid &= RequireOffset<FractalParamsDTO>("Lacunarity", 8, errors);
            valid &= RequireOffset<FractalParamsDTO>("Persistence", 12, errors);
            valid &= RequireOffset<FractalParamsDTO>("Octaves", 16, errors);
            valid &= RequireOffset<FractalParamsDTO>("SeedHash", 20, errors);
            valid &= RequireOffset<FractalParamsDTO>("_pad0", 24, errors);
            valid &= RequireOffset<FractalParamsDTO>("_pad1", 28, errors);
            valid &= RequireSize<DomainWarpParamsDTO>(32, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("Frequency", 0, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("StrengthMeters", 4, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("Lacunarity", 8, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("Persistence", 12, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("Octaves", 16, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("SeedHash", 20, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("_pad0", 24, errors);
            valid &= RequireOffset<DomainWarpParamsDTO>("_pad1", 28, errors);
            valid &= RequireSize<TectonicRiftSegmentDTO>(64, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("StartAupXZ", 0, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("EndAupXZ", 16, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("WidthMeters", 32, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("DepthMeters", 36, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("EdgeSharpness", 40, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("FalloffPower", 44, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("SeedHash", 48, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("Flags", 52, errors);
            valid &= RequireOffset<TectonicRiftSegmentDTO>("_pad0", 56, errors);
            valid &= RequireSize<TopographyBakeConfigDTO>(128, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("SectorAup", 0, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("PixelSizeMeters", 24, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("Width", 32, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("Height", 36, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("WorldSeed", 80, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("GlobalQualityWeight", 96, errors);
            valid &= RequireOffset<TopographyBakeConfigDTO>("_pad2", 120, errors);
            valid &= RequireSize<TopographyBiomeRecipeDTO>(192, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("Name", 0, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("CenterAupXZ", 64, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("RadiusMeters", 80, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("SeedHash", 100, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("Ridge", 112, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("Warp", 144, errors);
            valid &= RequireOffset<TopographyBiomeRecipeDTO>("_pad3", 184, errors);
            valid &= RequireSize<TopographyBiomeKernelDTO>(128, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("CenterAupXZ", 0, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("RadiusMeters", 16, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("InvRadiusMeters", 20, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("InvRadiusSqMeters", 24, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("TerraceSteps", 28, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("SeedHash", 44, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("Ridge", 48, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("Warp", 80, errors);
            valid &= RequireOffset<TopographyBiomeKernelDTO>("_pad2", 120, errors);
            valid &= RequireSize<HeightmapFileHeaderDTO>(128, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("Magic", 0, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("Width", 16, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("SectorAup", 32, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("PixelSizeMeters", 56, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("DataChecksum", 84, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("ElementStrideBytes", 92, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("EndianMarker", 96, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("SchemaHash", 100, errors);
            valid &= RequireOffset<HeightmapFileHeaderDTO>("Reserved3", 120, errors);
            valid &= RequireSize<BiomeMaskFileHeaderDTO>(128, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("Magic", 0, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("Width", 16, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("SectorAup", 32, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("PixelSizeMeters", 56, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("DataChecksum", 68, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("ElementStrideBytes", 76, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("ChannelCount", 80, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("RecipeCount", 84, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("EndianMarker", 88, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("SchemaHash", 92, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("SemanticsHash", 96, errors);
            valid &= RequireOffset<BiomeMaskFileHeaderDTO>("Reserved3", 120, errors);
            valid &= RequireSize<TopographyBakeTelemetryEntry>(64, errors);
            valid &= RequireOffset<TopographyBakeTelemetryEntry>("SectorAup", 0, errors);
            valid &= RequireOffset<TopographyBakeTelemetryEntry>("Frame", 24, errors);
            valid &= RequireOffset<TopographyBakeTelemetryEntry>("DumpReason", 60, errors);
            valid &= RequireSize<TopographyBakeDumpHeader>(32, errors);
            valid &= RequireSize<TopographyBakeMetrics>(128, errors);
            valid &= RequireOffset<TopographyBakeMetrics>("PipelineMilliseconds", 88, errors);
            valid &= RequireOffset<TopographyBakeMetrics>("_pad5", 120, errors);
            valid &= RequireSize<TopographyBakeRunStateDTO>(192, errors);
            valid &= RequireOffset<TopographyBakeRunStateDTO>("Metrics", 0, errors);
            valid &= RequireOffset<TopographyBakeRunStateDTO>("BlackBoxCursor", 128, errors);
            valid &= RequireOffset<TopographyBakeRunStateDTO>("_pad7", 184, errors);
            message = valid ? "OK" : errors.ToString();
            return valid;
        }

        internal static bool TryValidateTopographyFile(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "Missing topography h8bin: " + path;
                return false;
            }

            uint magic;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int b0 = stream.ReadByte();
                int b1 = stream.ReadByte();
                int b2 = stream.ReadByte();
                int b3 = stream.ReadByte();
                if ((b0 | b1 | b2 | b3) < 0)
                {
                    error = "Topography h8bin missing magic.";
                    return false;
                }

                magic = (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
            }

            if (magic == TopographyForgeConstants.HeightmapMagic)
                return TryValidateHeightmapFile(path, out error);

            if (magic == TopographyForgeConstants.BiomeMaskMagic)
                return TryValidateBiomeMaskFile(path, out error);

            error = "Topography h8bin unknown magic: 0x" + magic.ToString("X8", CultureInfo.InvariantCulture);
            return false;
        }

        internal static bool TryValidateHeightmapFile(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "Missing heightmap h8bin: " + path;
                return false;
            }

            if (!BitConverter.IsLittleEndian)
            {
                error = "Heightmap validator requires little-endian host byte order.";
                return false;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length < TopographyForgeConstants.HeightmapHeaderBytes)
            {
                error = "Heightmap h8bin too small: " + info.Length + " bytes.";
                return false;
            }

            HeightmapFileHeaderDTO header;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (!TryLoadHeightmapHeader(stream, out header, out error))
                    return false;

                if (header.Magic != TopographyForgeConstants.HeightmapMagic)
                {
                    error = "Heightmap magic mismatch: 0x" + header.Magic.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.Version != TopographyForgeConstants.HeightmapVersion)
                {
                    error = "Heightmap version mismatch: " + header.Version;
                    return false;
                }

                if (header.HeaderBytes != TopographyForgeConstants.HeightmapHeaderBytes)
                {
                    error = "Heightmap header byte count mismatch: " + header.HeaderBytes;
                    return false;
                }

                if (header.EndianMarker != TopographyForgeConstants.HeightmapEndianMarker)
                {
                    error = "Heightmap endian marker mismatch: 0x" + header.EndianMarker.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.SchemaHash != TopographyForgeConstants.HeightmapSchemaHash)
                {
                    error = "Heightmap schema hash mismatch: 0x" + header.SchemaHash.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.ElementStrideBytes != UnsafeUtility.SizeOf<float>())
                {
                    error = "Heightmap element stride mismatch: " + header.ElementStrideBytes;
                    return false;
                }

                if (header.Width <= 0 || header.Height <= 0)
                {
                    error = "Heightmap dimensions invalid: " + header.Width + "x" + header.Height + ".";
                    return false;
                }

                if (header.Width > TopographyForgeConstants.MaximumHeightmapResolution || header.Height > TopographyForgeConstants.MaximumHeightmapResolution)
                {
                    error = "Heightmap dimensions exceed SHINOBU_240 contract: " + header.Width + "x" + header.Height + ".";
                    return false;
                }

                if (double.IsNaN(header.PixelSizeMeters) || double.IsInfinity(header.PixelSizeMeters) || header.PixelSizeMeters <= 0.0)
                {
                    error = "Heightmap pixel size invalid: " + header.PixelSizeMeters.ToString("F6", CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                if (!IsFinite(header.SectorAup.x) || !IsFinite(header.SectorAup.y) || !IsFinite(header.SectorAup.z))
                {
                    error = "Heightmap sector AUP contains non-finite coordinates.";
                    return false;
                }

                if (!IsFinite(header.HeightMinContractMeters) || !IsFinite(header.HeightMaxContractMeters) || header.HeightMaxContractMeters <= header.HeightMinContractMeters)
                {
                    error = "Heightmap height contract invalid.";
                    return false;
                }

                if (!IsFinite(header.MinHeightMeters) || !IsFinite(header.MaxHeightMeters) || header.MaxHeightMeters < header.MinHeightMeters || header.MinHeightMeters < header.HeightMinContractMeters || header.MaxHeightMeters > header.HeightMaxContractMeters)
                {
                    error = "Heightmap observed min/max outside header contract.";
                    return false;
                }

                long expectedPayload = (long)header.Width * header.Height * UnsafeUtility.SizeOf<float>();
                long expectedLength = TopographyForgeConstants.HeightmapHeaderBytes + expectedPayload;
                if (header.PayloadBytes != expectedPayload || info.Length != expectedLength)
                {
                    error = "Heightmap payload range mismatch: header=" + header.PayloadBytes + " expected=" + expectedPayload + " file=" + info.Length + ".";
                    return false;
                }

                if ((header.Flags & TopographyForgeConstants.RollbackExcludedFlag) == 0u)
                {
                    error = "Heightmap rollback exclusion flag missing.";
                    return false;
                }

                if (!TryValidatePayloadAndChecksum(stream, expectedPayload, header, out uint checksum, out error))
                    return false;

                if (checksum != header.DataChecksum)
                {
                    error = "Heightmap checksum mismatch: stored=0x" + header.DataChecksum.ToString("X8", CultureInfo.InvariantCulture) + " computed=0x" + checksum.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }
            }

            return true;
        }

        internal static bool TryValidateBiomeMaskFile(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "Missing biome mask h8bin: " + path;
                return false;
            }

            if (!BitConverter.IsLittleEndian)
            {
                error = "Biome mask validator requires little-endian host byte order.";
                return false;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length < TopographyForgeConstants.BiomeMaskHeaderBytes)
            {
                error = "Biome mask h8bin too small: " + info.Length + " bytes.";
                return false;
            }

            BiomeMaskFileHeaderDTO header;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (!TryLoadBiomeMaskHeader(stream, out header, out error))
                    return false;

                if (header.Magic != TopographyForgeConstants.BiomeMaskMagic)
                {
                    error = "Biome mask magic mismatch: 0x" + header.Magic.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.Version != TopographyForgeConstants.HeightmapVersion)
                {
                    error = "Biome mask version mismatch: " + header.Version;
                    return false;
                }

                if (header.HeaderBytes != TopographyForgeConstants.BiomeMaskHeaderBytes)
                {
                    error = "Biome mask header byte count mismatch: " + header.HeaderBytes;
                    return false;
                }

                if (header.EndianMarker != TopographyForgeConstants.HeightmapEndianMarker)
                {
                    error = "Biome mask endian marker mismatch: 0x" + header.EndianMarker.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.SchemaHash != TopographyForgeConstants.BiomeMaskSchemaHash)
                {
                    error = "Biome mask schema hash mismatch: 0x" + header.SchemaHash.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.SemanticsHash != TopographyForgeConstants.BiomeMaskSemanticsHash)
                {
                    error = "Biome mask semantics hash mismatch: 0x" + header.SemanticsHash.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (header.ElementStrideBytes != UnsafeUtility.SizeOf<Unity.Mathematics.float4>())
                {
                    error = "Biome mask element stride mismatch: " + header.ElementStrideBytes;
                    return false;
                }

                if (header.ChannelCount != TopographyForgeConstants.BiomeMaskChannels)
                {
                    error = "Biome mask channel count mismatch: " + header.ChannelCount;
                    return false;
                }

                if (header.RecipeCount > header.ChannelCount)
                {
                    error = "Biome mask recipe count exceeds encoded channel count: recipes=" + header.RecipeCount + " channels=" + header.ChannelCount + ".";
                    return false;
                }

                if (header.Width <= 0 || header.Height <= 0)
                {
                    error = "Biome mask dimensions invalid: " + header.Width + "x" + header.Height + ".";
                    return false;
                }

                if (header.Width > TopographyForgeConstants.MaximumHeightmapResolution || header.Height > TopographyForgeConstants.MaximumHeightmapResolution)
                {
                    error = "Biome mask dimensions exceed SHINOBU_240 contract: " + header.Width + "x" + header.Height + ".";
                    return false;
                }

                if (double.IsNaN(header.PixelSizeMeters) || double.IsInfinity(header.PixelSizeMeters) || header.PixelSizeMeters <= 0.0)
                {
                    error = "Biome mask pixel size invalid: " + header.PixelSizeMeters.ToString("F6", CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                if (!IsFinite(header.SectorAup.x) || !IsFinite(header.SectorAup.y) || !IsFinite(header.SectorAup.z))
                {
                    error = "Biome mask sector AUP contains non-finite coordinates.";
                    return false;
                }

                long expectedPayload = (long)header.Width * header.Height * UnsafeUtility.SizeOf<Unity.Mathematics.float4>();
                long expectedLength = TopographyForgeConstants.BiomeMaskHeaderBytes + expectedPayload;
                if (header.PayloadBytes != expectedPayload || info.Length != expectedLength)
                {
                    error = "Biome mask payload range mismatch: header=" + header.PayloadBytes + " expected=" + expectedPayload + " file=" + info.Length + ".";
                    return false;
                }

                if ((header.Flags & TopographyForgeConstants.RollbackExcludedFlag) == 0u)
                {
                    error = "Biome mask rollback exclusion flag missing.";
                    return false;
                }

                if (!TryValidateBiomeMaskPayloadAndChecksum(stream, expectedPayload, out uint checksum, out error))
                    return false;

                if (checksum != header.DataChecksum)
                {
                    error = "Biome mask checksum mismatch: stored=0x" + header.DataChecksum.ToString("X8", CultureInfo.InvariantCulture) + " computed=0x" + checksum.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }
            }

            return true;
        }

        private static bool TryLoadHeightmapHeader(FileStream stream, out HeightmapFileHeaderDTO header, out string error)
        {
            header = default;
            error = string.Empty;
            byte[] headerBytes = ArrayPool<byte>.Shared.Rent(TopographyForgeConstants.HeightmapHeaderBytes);
            try
            {
                int read = FillBufferFromStream(stream, headerBytes, TopographyForgeConstants.HeightmapHeaderBytes);

                if (read != TopographyForgeConstants.HeightmapHeaderBytes)
                {
                    error = "Incomplete heightmap header read: " + read + "/" + TopographyForgeConstants.HeightmapHeaderBytes + ".";
                    return false;
                }

                fixed (byte* src = headerBytes)
                {
                    HeightmapFileHeaderDTO* dst = &header;
                    UnsafeUtility.MemCpy(dst, src, TopographyForgeConstants.HeightmapHeaderBytes);
                }

                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBytes);
            }
        }

        private static bool TryLoadBiomeMaskHeader(FileStream stream, out BiomeMaskFileHeaderDTO header, out string error)
        {
            header = default;
            error = string.Empty;
            byte[] headerBytes = ArrayPool<byte>.Shared.Rent(TopographyForgeConstants.BiomeMaskHeaderBytes);
            try
            {
                int read = FillBufferFromStream(stream, headerBytes, TopographyForgeConstants.BiomeMaskHeaderBytes);

                if (read != TopographyForgeConstants.BiomeMaskHeaderBytes)
                {
                    error = "Incomplete biome mask header read: " + read + "/" + TopographyForgeConstants.BiomeMaskHeaderBytes + ".";
                    return false;
                }

                fixed (byte* src = headerBytes)
                {
                    BiomeMaskFileHeaderDTO* dst = &header;
                    UnsafeUtility.MemCpy(dst, src, TopographyForgeConstants.BiomeMaskHeaderBytes);
                }

                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBytes);
            }
        }

        private static bool TryValidatePayloadAndChecksum(
            FileStream stream,
            long payloadBytes,
            HeightmapFileHeaderDTO header,
            out uint checksum,
            out string error)
        {
            checksum = 2166136261u;
            error = string.Empty;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            long remaining = payloadBytes;
            try
            {
                while (remaining > 0L)
                {
                    int requested = (int)Math.Min(buffer.Length, remaining);
                    int read = FillBufferFromStream(stream, buffer, requested);
                    if (read != requested)
                    {
                        error = "Incomplete heightmap payload read: " + read + "/" + requested + ".";
                        return false;
                    }

                    for (int i = 0; i < read; i++)
                    {
                        checksum ^= buffer[i];
                        checksum *= 16777619u;
                    }

                    if (!ValidateFloatPayloadChunk(buffer, read, header, out error))
                        return false;

                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        private static bool TryValidateBiomeMaskPayloadAndChecksum(
            FileStream stream,
            long payloadBytes,
            out uint checksum,
            out string error)
        {
            checksum = 2166136261u;
            error = string.Empty;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            long remaining = payloadBytes;
            try
            {
                while (remaining > 0L)
                {
                    int requested = (int)Math.Min(buffer.Length, remaining);
                    int read = FillBufferFromStream(stream, buffer, requested);
                    if (read != requested)
                    {
                        error = "Incomplete biome mask payload read: " + read + "/" + requested + ".";
                        return false;
                    }

                    for (int i = 0; i < read; i++)
                    {
                        checksum ^= buffer[i];
                        checksum *= 16777619u;
                    }

                    if (!ValidateBiomeMaskPayloadChunk(buffer, read, out error))
                        return false;

                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        private static unsafe bool ValidateFloatPayloadChunk(byte[] buffer, int bytes, HeightmapFileHeaderDTO header, out string error)
        {
            error = string.Empty;
            if ((bytes & 3) != 0)
            {
                error = "Heightmap payload chunk is not float-aligned: " + bytes + " bytes.";
                return false;
            }

            fixed (byte* src = buffer)
            {
                float* values = (float*)src;
                int count = bytes / UnsafeUtility.SizeOf<float>();
                for (int i = 0; i < count; i++)
                {
                    float h = values[i];
                    if (float.IsNaN(h) || float.IsInfinity(h))
                    {
                        error = "Heightmap payload contains non-finite float.";
                        return false;
                    }

                    if (h < header.HeightMinContractMeters || h > header.HeightMaxContractMeters)
                    {
                        error = "Heightmap payload value outside contract range: " + h.ToString("F3", CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                }
            }

            return true;
        }

        private static unsafe bool ValidateBiomeMaskPayloadChunk(byte[] buffer, int bytes, out string error)
        {
            error = string.Empty;
            int stride = UnsafeUtility.SizeOf<Unity.Mathematics.float4>();
            if ((bytes % stride) != 0)
            {
                error = "Biome mask payload chunk is not float4-aligned: " + bytes + " bytes.";
                return false;
            }

            fixed (byte* src = buffer)
            {
                float* values = (float*)src;
                int count = bytes / UnsafeUtility.SizeOf<float>();
                for (int i = 0; i < count; i += 4)
                {
                    float r = values[i];
                    float g = values[i + 1];
                    float b = values[i + 2];
                    float a = values[i + 3];
                    if (!IsFinite(r) || !IsFinite(g) || !IsFinite(b) || !IsFinite(a))
                    {
                        error = "Biome mask payload contains non-finite float.";
                        return false;
                    }

                    if (r < -0.0001f || g < -0.0001f || b < -0.0001f || a < -0.0001f || r > 1.0001f || g > 1.0001f || b > 1.0001f || a > 1.0001f)
                    {
                        error = "Biome mask payload value outside 0..1 range.";
                        return false;
                    }

                    float sum = r + g + b + a;
                    if (Math.Abs(sum - 1f) > 0.01f)
                    {
                        error = "Biome mask payload weights are not normalized.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static int FillBufferFromStream(FileStream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int chunk = stream.Read(buffer, read, count - read);
                if (chunk <= 0)
                    break;
                read += chunk;
            }

            return read;
        }

        private static bool RequireSize<T>(int expected, StringBuilder errors) where T : struct
        {
            int actual = UnsafeUtility.SizeOf<T>();
            if (actual == expected)
                return true;

            errors.Append(typeof(T).Name).Append(" size ").Append(actual).Append(" != ").Append(expected).Append("; ");
            return false;
        }

        private static bool RequireOffset<T>(string fieldName, int expected, StringBuilder errors) where T : struct
        {
            int actual = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (actual == expected)
                return true;

            errors.Append(typeof(T).Name).Append('.').Append(fieldName).Append(" offset ").Append(actual).Append(" != ").Append(expected).Append("; ");
            return false;
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
