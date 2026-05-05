#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - SpaceEngineResearchContracts.cs
// Editor-only DTO contracts for SpaceEngine research validation.
// ============================================================================

using System.Collections.Generic;

namespace Hecton8.EditorTools
{
    internal sealed class SpaceEngineResearchAuditResult
    {
        // COLD ALLOC: List<string>[32] - editor smoke failure sample - owner: SpaceEngineResearchSmokeTester
        public readonly List<string> Failures = new List<string>(32);

        public string ProjectRoot;
        public string SpaceEngineRoot;
        public string ReportPath;
        public string ReferenceKernelFolder;
        public string NoPasswordProbeStatus;
        public bool Passed;
        public bool TelemetryWarningRequested;
        public bool TelemetryRuntimeEligible;
        public int ReportLineCount;
        public int MaxReportLineCount;
        public int ReferenceKernelFileCount;
        public int EditorValidationFileCount;
        public int MaxEditorValidationLineCount;
        public int NativeCollectionTokenCount;
        public int JobBarrierTokenCount;
        public int StaticInstanceTokenCount;
        public int HotPathStringTokenCount;
        public int RecentScopeRuntimeCsFileCount;
        public int RecentScopeRuntimeNativeCollectionCount;
        public int FailureCount;
        public SpaceEngineZipProbeResult ShaderPak;
        public SpaceEngineZipProbeResult AtmospherePak;
        public SpaceEngineZipProbeResult CatalogPak;
    }

    internal sealed class SpaceEngineResearchStressResult
    {
        // COLD ALLOC: List<string>[32] - editor stress failure sample - owner: SpaceEngineResearchSmokeTester
        public readonly List<string> Failures = new List<string>(32);

        public string ProjectRoot;
        public string SpaceEngineRoot;
        public bool Passed;
        public int PassCount;
        public int FailureCount;
        public SpaceEngineResearchAuditResult FinalAudit;
    }

    internal struct SpaceEngineZipProbeResult
    {
        public string Path;
        public string ParseError;
        public bool Exists;
        public int EntryCount;
        public int EncryptedEntryCount;
        public int CompressedEntryCount;
        public int StoredEntryCount;
        public int ExpectedEntryCount;
        public int ExpectedFoundCount;
        public int ExpectedMissingCount;
    }
}
#endif
