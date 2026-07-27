        private void CaptureEcosystemSnapshot(
            ref NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot,
            ref NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner)
        {
            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector != null)
            {
                ecosystemDirector.CaptureSaveSnapshot();
                NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
                if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
                {
                    ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
                        ecosystemRecordCount,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory,
                        "ecosystemSectorSnapshotOwner");

                    for (int i = 0; i < ecosystemRecordCount; i++)
                        ecosystemSectorSnapshotOwner[i] = ecosystemView[i];

                    ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
                }
            }
        }
