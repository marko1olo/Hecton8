using System;
using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.World.OfflineWreckageBaker;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    internal static class OfflineWreckageBlackBox
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_209.bin";
        private const string NativeMemoryOwner = nameof(OfflineWreckageBlackBox);
        private const string RingLabel = "s_ring";
        private const uint DumpMagic = 0x5742524Bu; // WBRK
        private const uint DumpVersion = 1u;
        private static NativeArray<OfflineWreckageTelemetryEntry> s_ring;
        private static int s_cursor;
        private static int s_retained;

        public static void Record(
            double3 moduleAup,
            uint meshHash,
            uint damageState,
            int vertexCount,
            int indexCount,
            int tornVertexCount,
            int hullVertexCount,
            double burstMicroseconds,
            uint warningFlags)
        {
            EnsureRing();
            int capacity = s_ring.Length;
            int index = PositiveModulo(s_cursor, capacity);
            s_ring[index] = new OfflineWreckageTelemetryEntry
            {
                ModuleAup = moduleAup,
                MeshHash = meshHash,
                Frame = (uint)s_cursor,
                VertexCount = vertexCount,
                IndexCount = indexCount,
                TornVertexCount = tornVertexCount,
                HullVertexCount = hullVertexCount,
                BurstMicroseconds = (float)burstMicroseconds,
                WarningFlags = warningFlags,
                StateHash = OfflineWreckageBakeMath.Hash(meshHash ^ damageState ^ warningFlags),
                DamageState = damageState
            };
            s_cursor++;
            s_retained = math.min(s_retained + 1, capacity);
        }

        public static bool Dump(string projectRoot)
        {
            if (!s_ring.IsCreated || s_ring.Length <= 0)
                return false;

            string tempPath = null;
            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? "." : projectRoot;
                string path = Path.Combine(root, DumpRelativePath);
                tempPath = OfflineWreckageAtomicFile.CreateTempPath(path);

                int capacity = s_ring.Length;
                int retained = math.clamp(s_retained, 0, capacity);
                int start = retained >= capacity ? PositiveModulo(s_cursor, capacity) : 0;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Span<byte> header = stackalloc byte[32];
                    header.Clear();
                    WriteUInt32(header, 0, DumpMagic);
                    WriteUInt32(header, 4, DumpVersion);
                    WriteInt32(header, 8, capacity);
                    WriteInt32(header, 12, retained);
                    WriteInt32(header, 16, s_cursor);
                    WriteInt32(header, 20, start);
                    WriteInt32(header, 24, UnsafeUtility.SizeOf<OfflineWreckageTelemetryEntry>());
                    stream.Write(header);
                    for (int i = 0; i < retained; i++)
                    {
                        OfflineWreckageTelemetryEntry entry = s_ring[PositiveModulo(start + i, capacity)];
                        WriteEntry(stream, ref entry);
                    }
                }

                OfflineWreckageAtomicFile.Publish(tempPath, path);
                tempPath = null;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                OfflineWreckageAtomicFile.DeleteOwnedTemp(tempPath);
            }
        }

        public static void Dispose()
        {
            if (s_ring.IsCreated)
            {
                NativeMemoryTrackingBridge.UnregisterNativeArray(s_ring, NativeMemoryOwner, RingLabel);
                s_ring.Dispose();
            }

            s_cursor = 0;
            s_retained = 0;
        }

        private static void EnsureRing()
        {
            if (s_ring.IsCreated && s_ring.Length == OfflineWreckageBakeConstants.TelemetryFrames)
                return;

            Dispose();
            s_ring = new NativeArray<OfflineWreckageTelemetryEntry>(
                OfflineWreckageBakeConstants.TelemetryFrames,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            NativeMemoryTrackingBridge.RegisterNativeArray(
                s_ring,
                NativeMemoryOwner,
                RingLabel,
                NativeMemoryBridgeLifetime.Session);
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void WriteUInt32(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFFu);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFFu);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFFu);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }

        private static void WriteInt32(Span<byte> bytes, int offset, int value)
        {
            WriteUInt32(bytes, offset, (uint)value);
        }

        private static unsafe void WriteEntry(FileStream stream, ref OfflineWreckageTelemetryEntry entry)
        {
            const int EntryBytes = 64;
            Span<byte> bytes = stackalloc byte[EntryBytes];
            fixed (byte* destination = bytes)
            {
                UnsafeUtility.CopyStructureToPtr(ref entry, destination);
            }

            stream.Write(bytes);
        }
    }
}
