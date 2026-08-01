#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL CI pin for AcousticZoneController surface/interior/underwater snapshot
    /// cold-path auto-resolve.
    ///
    /// BUILD_PLAYTEST [~] Surface/interior/underwater audio requires MasterMixer snapshots
    /// plus a runtime owner that binds them. AcousticZoneController.EnsureSnapshotBindings
    /// resolves Underwater/BaseInterior/Surface(+Rain/Storm) via masterMixer.FindSnapshot
    /// when serialized refs are null. AudioTransitionSnapshotValidator already gates mixer
    /// asset presence; this validator pins the controller cold-path so authoring cannot
    /// regress the auto-bind lane without a CI RESULT: FAIL line.
    ///
    /// Does not open scenes. Soft FAIL under -quit.
    /// </summary>
    public static class AcousticZoneSnapshotBindingValidator
    {
        private const string LogPrefix = "[AcousticZoneSnapshotBindingValidator]";

        private const string AcousticZoneRelativePath =
            "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string MasterMixerRelativePath =
            "Assets/_Project/MasterMixer.mixer";
        private const string GlobalRegistryRelativePath =
            "Assets/_Project/Scripts/Core/GlobalRegistry.cs";

        private const string PinEnsureSnapshotBindings = "EnsureSnapshotBindings";
        private const string PinResolveSnapshotBinding = "ResolveSnapshotBinding";
        private const string PinFindSnapshot = "FindSnapshot";
        private const string PinDefaultMasterMixerPath = "DefaultMasterMixerPath";
        private const string PinMasterMixerPathLiteral = "Assets/_Project/MasterMixer.mixer";
        private const string PinUnderwater = "\"Underwater\"";
        private const string PinBaseInterior = "\"BaseInterior\"";
        private const string PinSurface = "\"Surface\"";
        private const string PinSurfaceRain = "\"SurfaceRain\"";
        private const string PinSurfaceStorm = "\"SurfaceStorm\"";
        private const string PinRegisterAcousticZone = "RegisterAcousticZoneRuntime";
        private const string PinAcousticZoneProperty = "AcousticZone";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: AcousticZoneSnapshotBindingValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate AcousticZone Snapshot Bindings", priority = 193)]
        public static void ValidateAcousticZoneSnapshotBindings()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("AcousticZone Snapshot Bindings", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — AcousticZone Snapshot Binding Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();
            Report.AppendLine("Note: pins cold-path EnsureSnapshotBindings + FindSnapshot names.");
            Report.AppendLine("MasterMixer asset presence is co-gated (file on disk).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string zonePath = Path.Combine(
                projectRoot,
                AcousticZoneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string mixerPath = Path.Combine(
                projectRoot,
                MasterMixerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string registryPath = Path.Combine(
                projectRoot,
                GlobalRegistryRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool zoneExists = File.Exists(zonePath);
            bool mixerExists = File.Exists(mixerPath);
            bool registryExists = File.Exists(registryPath);

            string zoneText = zoneExists ? File.ReadAllText(zonePath) : string.Empty;
            string registryText = registryExists ? File.ReadAllText(registryPath) : string.Empty;

            bool hasEnsure = zoneExists && zoneText.Contains(PinEnsureSnapshotBindings);
            bool hasResolve = zoneExists && zoneText.Contains(PinResolveSnapshotBinding);
            bool hasFindSnapshot = zoneExists && zoneText.Contains(PinFindSnapshot);
            bool hasDefaultPath = zoneExists && zoneText.Contains(PinDefaultMasterMixerPath);
            bool hasMixerLiteral = zoneExists && zoneText.Contains(PinMasterMixerPathLiteral);
            bool hasUnderwater = zoneExists && zoneText.Contains(PinUnderwater);
            bool hasBaseInterior = zoneExists && zoneText.Contains(PinBaseInterior);
            bool hasSurface = zoneExists && zoneText.Contains(PinSurface);
            bool hasSurfaceRain = zoneExists && zoneText.Contains(PinSurfaceRain);
            bool hasSurfaceStorm = zoneExists && zoneText.Contains(PinSurfaceStorm);
            bool hasRegister = registryExists && registryText.Contains(PinRegisterAcousticZone);
            bool hasRegistryProp = registryExists && registryText.Contains(PinAcousticZoneProperty);

            // Gate: cold-path must resolve the three product zones + rain/storm variants.
            bool snapshotLanePinned =
                hasEnsure &&
                hasResolve &&
                hasFindSnapshot &&
                hasDefaultPath &&
                hasMixerLiteral &&
                hasUnderwater &&
                hasBaseInterior &&
                hasSurface &&
                hasSurfaceRain &&
                hasSurfaceStorm;

            bool registryPinned = hasRegister && hasRegistryProp;

            Report.AppendLine("--- Source / asset presence ---");
            AppendPresence(Report, AcousticZoneRelativePath, zoneExists);
            AppendPresence(Report, MasterMixerRelativePath, mixerExists);
            AppendPresence(Report, GlobalRegistryRelativePath, registryExists);
            Report.AppendLine();

            Report.AppendLine("--- Cold-path snapshot binding pins ---");
            AppendGate(Report, "zone.EnsureSnapshotBindings", hasEnsure);
            AppendGate(Report, "zone.ResolveSnapshotBinding", hasResolve);
            AppendGate(Report, "zone.FindSnapshot", hasFindSnapshot);
            AppendGate(Report, "zone.DefaultMasterMixerPath", hasDefaultPath);
            AppendGate(Report, "zone.MasterMixer path literal", hasMixerLiteral);
            AppendGate(Report, "zone.Underwater name", hasUnderwater);
            AppendGate(Report, "zone.BaseInterior name", hasBaseInterior);
            AppendGate(Report, "zone.Surface name", hasSurface);
            AppendGate(Report, "zone.SurfaceRain name", hasSurfaceRain);
            AppendGate(Report, "zone.SurfaceStorm name", hasSurfaceStorm);
            Report.AppendLine();

            Report.AppendLine("--- Registry owner pins ---");
            AppendGate(Report, "registry.RegisterAcousticZoneRuntime", hasRegister);
            AppendGate(Report, "registry.AcousticZone property", hasRegistryProp);
            Report.AppendLine();

            Report.Append("zoneExists=").Append(zoneExists ? 1 : 0);
            Report.Append(" mixerExists=").Append(mixerExists ? 1 : 0);
            Report.Append(" registryExists=").Append(registryExists ? 1 : 0);
            Report.Append(" hasEnsure=").Append(hasEnsure ? 1 : 0);
            Report.Append(" hasResolve=").Append(hasResolve ? 1 : 0);
            Report.Append(" hasFindSnapshot=").Append(hasFindSnapshot ? 1 : 0);
            Report.Append(" hasDefaultPath=").Append(hasDefaultPath ? 1 : 0);
            Report.Append(" hasMixerLiteral=").Append(hasMixerLiteral ? 1 : 0);
            Report.Append(" hasUnderwater=").Append(hasUnderwater ? 1 : 0);
            Report.Append(" hasBaseInterior=").Append(hasBaseInterior ? 1 : 0);
            Report.Append(" hasSurface=").Append(hasSurface ? 1 : 0);
            Report.Append(" hasSurfaceRain=").Append(hasSurfaceRain ? 1 : 0);
            Report.Append(" hasSurfaceStorm=").Append(hasSurfaceStorm ? 1 : 0);
            Report.Append(" hasRegister=").Append(hasRegister ? 1 : 0);
            Report.Append(" hasRegistryProp=").Append(hasRegistryProp ? 1 : 0);
            Report.AppendLine();

            bool passed =
                zoneExists &&
                mixerExists &&
                registryExists &&
                snapshotLanePinned &&
                registryPinned;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: AcousticZone snapshot cold-path pins missing.");
                if (!zoneExists)
                    Report.AppendLine("  • AcousticZoneController.cs must remain present.");
                if (!mixerExists)
                    Report.AppendLine("  • MasterMixer.mixer must remain on disk at Assets/_Project/.");
                if (!snapshotLanePinned)
                    Report.AppendLine("  • EnsureSnapshotBindings must ResolveSnapshotBinding via FindSnapshot for Underwater/BaseInterior/Surface(+Rain/Storm).");
                if (!registryPinned)
                    Report.AppendLine("  • GlobalRegistry must expose RegisterAcousticZoneRuntime + AcousticZone.");
            }
            else
            {
                Report.AppendLine("PASS: AcousticZone cold-path snapshot bindings + registry owner pins present.");
            }

            Report.Append("RESULT: ").AppendLine(passed ? "PASS" : "FAIL");
            string reportText = LogPrefix + " " + Report.ToString();

            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "AcousticZone Snapshot Bindings",
                    passed
                        ? "PASS\nCold-path snapshot binding pins present."
                        : "FAIL\nOne or more source pins missing.\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static void AppendPresence(StringBuilder sb, string relativePath, bool exists)
        {
            sb.Append(exists ? "  OK  " : "  MISS ");
            sb.AppendLine(relativePath);
        }

        private static void AppendGate(StringBuilder sb, string label, bool ok)
        {
            sb.Append(ok ? "  OK  " : "  MISS ");
            sb.AppendLine(label);
        }
    }
}
#endif
