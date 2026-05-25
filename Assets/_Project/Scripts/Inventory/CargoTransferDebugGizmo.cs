#if UNITY_EDITOR
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Inventory
{
    [ExecuteAlways]
    public sealed class CargoTransferDebugGizmo : MonoBehaviour
    {
        [SerializeField] private bool drawTransfer = true;
        [SerializeField] private AbsoluteUniversePositionBlit sourceAup;
        [SerializeField] private AbsoluteUniversePositionBlit destinationAup;
        [SerializeField] private float fallbackLineMeters = 4f;

        private void OnDrawGizmos()
        {
            if (!drawTransfer)
                return;

            CargoTransactionDTO transaction = default;
            CargoMergeResultDTO progress = default;
            TryReadCargoSnapshot(ref transaction, ref progress);

            Vector3 source = transform.position;
            Vector3 destination = ResolveDestinationVector(source);
            float hashTint = ((transaction.SourceContainerHashID ^ transaction.DestContainerHashID) & 0xFFu) * 0.0008f;
            float pulse = 0.45f + (0.35f * Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((float)EditorApplication.timeSinceStartup * 5.0f));
            Color lineColor = new Color(1f, math.saturate(0.78f + hashTint), 0.08f, 0.70f + pulse * 0.25f);

            Handles.color = lineColor;
            Handles.DrawAAPolyLine(math.max(2f, 5f + pulse * 3f), source, destination);
            Gizmos.color = lineColor;
            Gizmos.DrawWireSphere(source, 0.18f + pulse * 0.04f);
            Gizmos.DrawWireSphere(destination, 0.24f + pulse * 0.06f);

            int remaining = math.max(0, progress.SourceActiveAfter - progress.NextSourceIndex);
            DrawSevenSegmentNumber(Vector3.Lerp(source, destination, 0.5f), remaining);
        }

        private Vector3 ResolveDestinationVector(Vector3 source)
        {
            if (destinationAup.GridX == 0L &&
                destinationAup.GridY == 0L &&
                destinationAup.GridZ == 0L &&
                math.lengthsq(destinationAup.Local) <= 0.0001f)
            {
                return source + transform.right * math.max(0.25f, fallbackLineMeters);
            }

            float3 delta = ToRelativeMeters(in destinationAup, in sourceAup);
            if (!math.all(math.isfinite(delta)) || math.lengthsq(delta) <= 0.0001f)
                delta = new float3(math.max(0.25f, fallbackLineMeters), 0f, 0f);

            return source + new Vector3(delta.x, delta.y, delta.z);
        }

        private static float3 ToRelativeMeters(in AbsoluteUniversePositionBlit value, in AbsoluteUniversePositionBlit origin)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            double3 relative = new double3(
                ((double)(value.GridX - origin.GridX) * cell) + (value.Local.x - origin.Local.x),
                ((double)(value.GridY - origin.GridY) * cell) + (value.Local.y - origin.Local.y),
                ((double)(value.GridZ - origin.GridZ) * cell) + (value.Local.z - origin.Local.z));
            relative = math.clamp(relative, new double3(-10000d), new double3(10000d));
            return new float3((float)relative.x, (float)relative.y, (float)relative.z);
        }

        private static void TryReadCargoSnapshot(ref CargoTransactionDTO transaction, ref CargoMergeResultDTO progress)
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) || vault == null || vault.IsCompactionFenceActive)
                return;

            if (vault.TryGetGenerationHandle<CargoTransactionDTO>(BufferID.ShinobuCargoTransactions, out VaultGenerationHandle<CargoTransactionDTO> transactionHandle) &&
                vault.TryReadHandle(in transactionHandle, out NativeArray<CargoTransactionDTO> transactions) &&
                transactions.IsCreated &&
                transactions.Length > 0)
            {
                transaction = transactions[0];
            }

            if (vault.TryGetGenerationHandle<CargoMergeResultDTO>(BufferID.ShinobuCargoSyncProgress, out VaultGenerationHandle<CargoMergeResultDTO> progressHandle) &&
                vault.TryReadHandle(in progressHandle, out NativeArray<CargoMergeResultDTO> progressBuffer) &&
                progressBuffer.IsCreated &&
                progressBuffer.Length > 0)
            {
                progress = progressBuffer[0];
            }
        }

        private void DrawSevenSegmentNumber(Vector3 center, int value)
        {
            value = math.clamp(value, 0, 999999);
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            float height = 0.22f;
            float width = height * 0.55f;
            float gap = width * 0.35f;
            int digits = CountDigits(value);
            Vector3 cursor = center - (right * ((digits - 1) * (width + gap) * 0.5f)) + (up * 0.34f);

            int divisor = 1;
            for (int i = 1; i < digits; i++)
                divisor *= 10;

            for (int i = 0; i < digits; i++)
            {
                int digit = (value / divisor) % 10;
                DrawDigit(cursor + right * (i * (width + gap)), right, up, width, height, digit);
                divisor = math.max(1, divisor / 10);
            }
        }

        private static int CountDigits(int value)
        {
            if (value >= 100000)
                return 6;
            if (value >= 10000)
                return 5;
            if (value >= 1000)
                return 4;
            if (value >= 100)
                return 3;
            if (value >= 10)
                return 2;
            return 1;
        }

        private static void DrawDigit(Vector3 origin, Vector3 right, Vector3 up, float width, float height, int digit)
        {
            float halfHeight = height * 0.5f;
            bool a = digit != 1 && digit != 4;
            bool b = digit != 5 && digit != 6;
            bool c = digit != 2;
            bool d = digit != 1 && digit != 4 && digit != 7;
            bool e = digit == 0 || digit == 2 || digit == 6 || digit == 8;
            bool f = digit == 0 || digit == 4 || digit == 5 || digit == 6 || digit == 8 || digit == 9;
            bool g = digit != 0 && digit != 1 && digit != 7;
            if (a)
                DrawSegment(origin + up * height, origin + up * height + right * width);
            if (b)
                DrawSegment(origin + up * halfHeight + right * width, origin + up * height + right * width);
            if (c)
                DrawSegment(origin + right * width, origin + up * halfHeight + right * width);
            if (d)
                DrawSegment(origin, origin + right * width);
            if (e)
                DrawSegment(origin, origin + up * halfHeight);
            if (f)
                DrawSegment(origin + up * halfHeight, origin + up * height);
            if (g)
                DrawSegment(origin + up * halfHeight, origin + up * halfHeight + right * width);
        }

        private static void DrawSegment(Vector3 a, Vector3 b)
        {
            Handles.DrawAAPolyLine(2.5f, a, b);
        }
    }
}
#endif
