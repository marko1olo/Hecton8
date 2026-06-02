#if UNITY_EDITOR
using UnityEditor;

namespace Hecton8.Audio.Editor
{
    public static class VocalWarningStormTorture_X_011
    {
        private const int AlarmBitCount = 64;
        private const int StormWarningCount = 50;

        [MenuItem("Hecton8/Audio/Run VWS Storm Torture X_011")]
        public static void Run()
        {
            StormResult result = ExecuteStorm();
            Hecton8.Core.H8Debug.Log("X_011 VWS storm torture " + (result.Pass ? "PASS" : "FAIL") + ".");
        }

        internal static StormResult ExecuteStorm()
        {
            StormSlot[] slots = new StormSlot[AlarmBitCount];
            ulong activeAlarmsMask = 0UL;
            int accepted = 0;
            int replaced = 0;
            int rejected = 0;

            for (int i = 0; i < StormWarningCount; i++)
            {
                byte warningId = (byte)((i % 5) + 1);
                int bitIndex = ResolvePriorityBitIndex(warningId);
                if ((uint)bitIndex >= AlarmBitCount)
                {
                    rejected++;
                    continue;
                }

                float score = ResolveScore(warningId, i);
                ulong mask = 1UL << bitIndex;
                ref StormSlot slot = ref slots[bitIndex];
                if ((activeAlarmsMask & mask) == 0UL)
                {
                    slot = new StormSlot(warningId, score, i);
                    activeAlarmsMask |= mask;
                    accepted++;
                    continue;
                }

                if (score > slot.Score || (score == slot.Score && i < slot.Sequence))
                {
                    slot = new StormSlot(warningId, score, i);
                    replaced++;
                }
                else
                {
                    rejected++;
                }
            }

            int firstBit = ResolveHighestPriorityBitIndex(activeAlarmsMask);
            bool sorted = true;
            int previousBit = -1;
            ulong scanWord = activeAlarmsMask;
            while (scanWord != 0UL)
            {
                int bitIndex = ResolveHighestPriorityBitIndex(scanWord);
                if (bitIndex <= previousBit)
                    sorted = false;

                previousBit = bitIndex;
                scanWord &= ~(1UL << bitIndex);
            }

            return new StormResult
            {
                Pass = CountBits64(activeAlarmsMask) == 5 &&
                       firstBit == 0 &&
                       sorted &&
                       slots[0].WarningId == 1 &&
                       slots[4].WarningId == 5,
                ActiveAlarmsMask = activeAlarmsMask,
                ActiveCount = CountBits64(activeAlarmsMask),
                HighestBit = firstBit,
                Accepted = accepted,
                Replaced = replaced,
                Rejected = rejected,
                TriggerCount = StormWarningCount
            };
        }

        private static float ResolveScore(byte warningId, int sequence)
        {
            float baseScore = 1024f - (warningId * 128f);
            return baseScore + ((sequence % 7) * 0.03125f);
        }

        private static int ResolvePriorityBitIndex(byte warningId)
        {
            return warningId >= 1 && warningId <= 5 ? warningId - 1 : -1;
        }

        private static int ResolveHighestPriorityBitIndex(ulong activeAlarmsMask)
        {
            if (activeAlarmsMask == 0UL)
                return -1;

            uint low = (uint)activeAlarmsMask;
            if (low != 0u)
                return CountTrailingZeros(low);

            return 32 + CountTrailingZeros((uint)(activeAlarmsMask >> 32));
        }

        private static int CountTrailingZeros(uint value)
        {
            if (value == 0u)
                return 32;

            int count = 0;
            uint mask = 1u;
            while ((value & mask) == 0u)
            {
                count++;
                mask <<= 1;
            }

            return count;
        }

        private static int CountBits64(ulong value)
        {
            uint low = (uint)value;
            uint high = (uint)(value >> 32);
            return CountBits32(low) + CountBits32(high);
        }

        private static int CountBits32(uint value)
        {
            int count = 0;
            while (value != 0u)
            {
                value &= value - 1u;
                count++;
            }

            return count;
        }

        private readonly struct StormSlot
        {
            public StormSlot(byte warningId, float score, int sequence)
            {
                WarningId = warningId;
                Score = score;
                Sequence = sequence;
            }

            public readonly byte WarningId;
            public readonly float Score;
            public readonly int Sequence;
        }

        internal struct StormResult
        {
            public bool Pass;
            public ulong ActiveAlarmsMask;
            public int ActiveCount;
            public int HighestBit;
            public int Accepted;
            public int Replaced;
            public int Rejected;
            public int TriggerCount;
        }
    }
}
#endif
