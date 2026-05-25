#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public static class VocalWarningStormTorture_X_011
    {
        private const int PriorityWordBitCount = 64;
        private const int StormWarningCount = 50;
        private const string ReportPath = "Docs/Reports/UX_VWS_STORM_TORTURE_X_011.json";

        [MenuItem("Hecton8/Audio/Run VWS Storm Torture X_011")]
        public static void Run()
        {
            StormResult result = ExecuteStorm();
            WriteReport(result);
            AssetDatabase.Refresh();
            Debug.Log("X_011 VWS storm torture " + (result.Pass ? "PASS" : "FAIL") + " wrote " + ReportPath + ".");
        }

        internal static StormResult ExecuteStorm()
        {
            StormSlot[] slots = new StormSlot[PriorityWordBitCount];
            ulong priorityWord = 0UL;
            int accepted = 0;
            int replaced = 0;
            int rejected = 0;

            for (int i = 0; i < StormWarningCount; i++)
            {
                byte warningId = (byte)((i % 5) + 1);
                int bitIndex = ResolvePriorityBitIndex(warningId);
                if ((uint)bitIndex >= PriorityWordBitCount)
                {
                    rejected++;
                    continue;
                }

                float score = ResolveScore(warningId, i);
                ulong mask = 1UL << bitIndex;
                ref StormSlot slot = ref slots[bitIndex];
                if ((priorityWord & mask) == 0UL)
                {
                    slot = new StormSlot(warningId, score, i);
                    priorityWord |= mask;
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

            int firstBit = ResolveHighestPriorityBitIndex(priorityWord);
            bool sorted = true;
            int previousBit = PriorityWordBitCount;
            ulong scanWord = priorityWord;
            while (scanWord != 0UL)
            {
                int bitIndex = ResolveHighestPriorityBitIndex(scanWord);
                if (bitIndex >= previousBit)
                    sorted = false;

                previousBit = bitIndex;
                scanWord &= ~(1UL << bitIndex);
            }

            return new StormResult
            {
                Pass = CountBits64(priorityWord) == 5 &&
                       firstBit == 63 &&
                       sorted &&
                       slots[63].WarningId == 1 &&
                       slots[59].WarningId == 5,
                PriorityWord = priorityWord,
                ActiveCount = CountBits64(priorityWord),
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
            return warningId >= 1 && warningId <= 5 ? PriorityWordBitCount - warningId : -1;
        }

        private static int ResolveHighestPriorityBitIndex(ulong priorityWord)
        {
            if (priorityWord == 0UL)
                return -1;

            uint high = (uint)(priorityWord >> 32);
            if (high != 0u)
                return 32 + (31 - CountLeadingZeros(high));

            return 31 - CountLeadingZeros((uint)priorityWord);
        }

        private static int CountLeadingZeros(uint value)
        {
            if (value == 0u)
                return 32;

            int count = 0;
            uint mask = 0x80000000u;
            while ((value & mask) == 0u)
            {
                count++;
                mask >>= 1;
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

        private static void WriteReport(in StormResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(512);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"X_011\",\n");
            builder.Append("  \"status\": \"").Append(result.Pass ? "PASS_PENDING_COMPILE" : "FAIL_STATIC_STORM").Append("\",\n");
            builder.Append("  \"triggerCount\": ").Append(result.TriggerCount).Append(",\n");
            builder.Append("  \"activeCount\": ").Append(result.ActiveCount).Append(",\n");
            builder.Append("  \"highestBit\": ").Append(result.HighestBit).Append(",\n");
            builder.Append("  \"priorityWordHex\": \"0x").Append(result.PriorityWord.ToString("X16")).Append("\",\n");
            builder.Append("  \"accepted\": ").Append(result.Accepted).Append(",\n");
            builder.Append("  \"replaced\": ").Append(result.Replaced).Append(",\n");
            builder.Append("  \"rejected\": ").Append(result.Rejected).Append("\n");
            builder.Append("}\n");
            File.WriteAllText(absolutePath, builder.ToString(), Encoding.UTF8);
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
            public ulong PriorityWord;
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
