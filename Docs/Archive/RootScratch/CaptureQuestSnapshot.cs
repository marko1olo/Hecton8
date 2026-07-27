        private void CaptureQuestSnapshot(
            long saveTimestampTicks,
            ref NativeArray<uint> packedQuestStateSnapshot,
            ref QuestSaveHeader packedQuestSaveHeader)
        {
            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null)
            {
                int packedQuestWordCount = questManager.PackedStateWordCount;
                if (packedQuestWordCount > 0)
                {
                    packedQuestStateSnapshot = CreateTransientNativeArray<uint>(
                        packedQuestWordCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory,
                        "packedQuestStateSnapshot");

                    bool copiedQuestState;
                    unsafe
                    {
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateSnapshot);
                        copiedQuestState = questManager.TryCopyPackedStateSnapshot(
                            destinationPtr,
                            packedQuestStateSnapshot.Length,
                            out packedQuestSaveHeader,
                            saveTimestampTicks);
                    }

                    if (!copiedQuestState)
                        DisposeTransientNativeArrayBestEffortAndReport(ref packedQuestStateSnapshot, "save", "packedQuestStateSnapshot");
                }
            }
        }
