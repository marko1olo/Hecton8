#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton.Localization;
using Hecton8.Core.Contracts;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Editor-only gate for the Abandoned Outpost fail-safe handoff contract.
    /// </summary>
    internal static class OutpostFailSafeHandoffValidator
    {
        private const string MenuPath = "Hecton-8/Validate Outpost Fail-Safe Handoff";
        private const string ExpectedSchema = "H8.OUTPOST.FAILSAFE.HANDOFF.V1";
        private const string ExpectedAgent = "OUTPOST_FAILSAFE_STATIC_CONTRACT";
        private const string ExpectedRole = "DESIGN_MISSIONS";
        private const string ExpectedEvidenceClass = "STATIC_DOC";
        private const string ExpectedSourceBatch = "Docs/Design/Missions/Outpost_Failure_Modes.md";
        private const string ExpectedRequestedBatch = "NONE_STATIC_CONTRACT";
        private const string ExpectedSourceAuthorityStatus = "STATIC_MISSION_CONTRACT";
        private const string ExpectedRuntimeLocalizationTable = "Assets/_Project/Scripts/English.json";
        private const string ExpectedHashAlgorithm = "FNV-1a 32-bit over UTF-16LE code units";
        private const string ExpectedHashRuntimeMatch = "Hecton.Localization.LocHash.Compute";
        private const string HandoffRelativePath = "Docs/Design/Missions/Outpost_FailSafe_Handoff.json";
        private const string MissionDocRelativePath = "Docs/Design/Missions/Outpost_Failure_Modes.md";
        private const string StaleBatchAuthorityToken = "ACTIVE_BATCH";
        private const string OutpostPrefix = "outpost.";
        private const string GasDynamicsRoomFlagPrefix = "GasDynamicsRoomFlags.";
        private const string LegacyRoomFlagPrefix = "roomflag.";
        private const string SubmergedScalarToken = "roomSubmerged01";
        private const int ExpectedFlagCount = 32;
        private const int ExpectedTooltipCount = 10;
        private const int ExpectedLogCount = 5;
        private const int ExpectedFallbackCount = 3;
        private const float ExpectedOxygenStandardKpa = HectonSurvivalContract.StandardOxygenKPa;
        private const float ExpectedPlayerOxygenDrainKpaPerSecond = HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond;
        private const float ExpectedPlayerCo2ProductionKpaPerSecond = HectonSurvivalContract.DefaultPlayerCarbonDioxideKPaPerSecond;
        private const float ExpectedFireOxygenDrainKpaPerSecond = HectonSurvivalContract.DefaultFireOxygenKPaPerSecond;
        private const float ExpectedScrubberCo2RemovalKpaPerSecond = HectonSurvivalContract.DefaultScrubberKPaPerSecond;
        private const float MaxUnpoweredCriticalReadSeconds = 90f;
        private const float GasConstraintEpsilon = 0.0001f;

        // COLD ALLOC: string[4] — editor-only gas flag allowlist — owner: OutpostFailSafeHandoffValidator
        private static readonly string[] AllowedGasDynamicsRoomFlags =
        {
            "GasDynamicsRoomFlags.InternalFire",
            "GasDynamicsRoomFlags.Breached",
            "GasDynamicsRoomFlags.ScrubberInstalled",
            "GasDynamicsRoomFlags.Occupied"
        };

        // COLD ALLOC: string[10] — editor-only stale flag alias needles — owner: OutpostFailSafeHandoffValidator
        private static readonly string[] StaleAliasNeedles =
        {
            "outpost.claim_complete",
            "outpost.deadlock_revert_triggered",
            "outpost.state_restored_after_revert",
            "outpost.roomflag_",
            "roomflag.",
            "outpost.marauder_log_power_found",
            "outpost.marauder_log_air_found",
            "outpost.marauder_log_fire_found",
            "outpost.marauder_log_breach_found",
            "outpost.marauder_log_exit_found"
        };

        // COLD ALLOC: string[4] — editor-only stale count needles — owner: OutpostFailSafeHandoffValidator
        private static readonly string[] StaleCountNeedles =
        {
            "flags=30",
            "Authored 30",
            "30 mission flags",
            "30 outpost DAG"
        };

        [MenuItem(MenuPath, priority = 142)]
        private static void ValidateFromMenu()
        {
            ValidateAndLog();
        }

        internal static int ValidateAndLog()
        {
            var errors = new List<string>(64); // COLD ALLOC: List<string>[64] — editor-only validation errors — owner: OutpostFailSafeHandoffValidator
            var warnings = new List<string>(16); // COLD ALLOC: List<string>[16] — editor-only validation warnings — owner: OutpostFailSafeHandoffValidator

            Validate(errors, warnings);

            for (int i = 0; i < warnings.Count; i++)
                Debug.LogWarning("[OutpostFailSafeHandoffValidator] " + warnings[i]);

            for (int i = 0; i < errors.Count; i++)
                Debug.LogError("[OutpostFailSafeHandoffValidator] " + errors[i]);

            if (errors.Count == 0)
            {
                Debug.Log(
                    "[OutpostFailSafeHandoffValidator] PASS: " +
                    HandoffRelativePath +
                    " matches the 32-flag Outpost fail-safe contract.");
            }

            return errors.Count;
        }

        private static void Validate(List<string> errors, List<string> warnings)
        {
            string projectRoot = ResolveProjectRoot();
            string handoffPath = Path.Combine(projectRoot, HandoffRelativePath);
            if (!File.Exists(handoffPath))
            {
                errors.Add("Missing handoff JSON at " + HandoffRelativePath + ".");
                return;
            }

            string jsonText = File.ReadAllText(handoffPath);
            HandoffRoot root;
            try
            {
                root = JsonUtility.FromJson<HandoffRoot>(jsonText);
            }
            catch (Exception exception)
            {
                errors.Add("Failed to parse " + HandoffRelativePath + ": " + exception.Message);
                return;
            }

            if (root == null)
            {
                errors.Add("Failed to parse " + HandoffRelativePath + ": root is null.");
                return;
            }

            ValidateRootIdentity(root, errors);
            ValidateMetadata(root, errors);
            ValidateSourceAuthority(projectRoot, root.sourceAuthority, root.sourceBatch, errors);
            ValidateRuntimeAssetDecision(root.runtimeAssetDecision, errors);
            ValidateHashContract(root.hashContract, errors);
            ValidateStaleNeedles(jsonText, HandoffRelativePath, errors);

            var declaredFlags = new HashSet<string>(ExpectedFlagCount, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[32] — editor-only declared outpost flags — owner: OutpostFailSafeHandoffValidator
            var declaredFlagHashes = new HashSet<int>(ExpectedFlagCount); // COLD ALLOC: HashSet<int>[32] — editor-only flag hash collision check — owner: OutpostFailSafeHandoffValidator
            ValidateMissionFlags(root.missionFlags, declaredFlags, declaredFlagHashes, errors);
            ValidateTopologicalOrder(root.topologicalOrder, declaredFlags, errors);
            ValidateFallbacks(root.fallbacks, declaredFlags, errors);
            ValidateLocalizationEntries(root.localizationEntries, declaredFlags, errors);
            ValidateGasConstraints(root.gasConstraints, errors);
            ValidateJsonOutpostReferences(jsonText, declaredFlags, errors);
            ValidateMissionDoc(projectRoot, root.sourceAuthority, declaredFlags, errors, warnings);
        }

        private static void ValidateRootIdentity(HandoffRoot root, List<string> errors)
        {
            if (!StringEquals(root.schema, ExpectedSchema))
                errors.Add("Handoff schema must be '" + ExpectedSchema + "'.");

            if (!StringEquals(root.agent, ExpectedAgent))
                errors.Add("Handoff agent must be '" + ExpectedAgent + "'.");
        }

        private static void ValidateMetadata(HandoffRoot root, List<string> errors)
        {
            if (!StringEquals(root.evidenceClass, ExpectedEvidenceClass))
                errors.Add("Handoff evidenceClass must be '" + ExpectedEvidenceClass + "'.");

            if (!StringEquals(root.sourceBatch, ExpectedSourceBatch))
                errors.Add("Handoff sourceBatch must be '" + ExpectedSourceBatch + "'.");

            if (!StringEquals(root.requestedBatch, ExpectedRequestedBatch))
                errors.Add("Handoff requestedBatch must be '" + ExpectedRequestedBatch + "'.");

            if (root.requestedBatchPresent)
                errors.Add("Handoff requestedBatchPresent must remain false for the static mission contract.");
        }

        private static void ValidateSourceAuthority(
            string projectRoot,
            SourceAuthority sourceAuthority,
            string sourceBatch,
            List<string> errors)
        {
            if (sourceAuthority == null)
            {
                errors.Add("sourceAuthority is missing.");
                return;
            }

            if (!StringEquals(sourceAuthority.expectedPromptId, ExpectedAgent))
                errors.Add("sourceAuthority.expectedPromptId must be '" + ExpectedAgent + "'.");

            if (!StringEquals(sourceAuthority.expectedPromptRole, ExpectedRole))
                errors.Add("sourceAuthority.expectedPromptRole must be '" + ExpectedRole + "'.");

            if (IsBlank(sourceAuthority.policy))
                errors.Add("sourceAuthority.policy is missing.");

            if (IsBlank(sourceBatch))
            {
                errors.Add("sourceAuthority cannot validate a blank sourceBatch.");
                return;
            }

            string sourceBatchPath = Path.Combine(projectRoot, sourceBatch);
            if (!File.Exists(sourceBatchPath))
            {
                errors.Add("sourceAuthority sourceBatch file is missing at " + sourceBatch + ".");
                return;
            }

            if (sourceAuthority.activeBatchContainsPrompt)
                errors.Add("sourceAuthority.activeBatchContainsPrompt must remain false for the static mission contract.");

            if (!StringEquals(sourceAuthority.status, ExpectedSourceAuthorityStatus))
                errors.Add("sourceAuthority.status must be '" + ExpectedSourceAuthorityStatus + "'.");
        }

        private static void ValidateRuntimeAssetDecision(RuntimeAssetDecision runtimeAssetDecision, List<string> errors)
        {
            if (runtimeAssetDecision == null)
            {
                errors.Add("runtimeAssetDecision is missing.");
                return;
            }

            if (runtimeAssetDecision.mutatedRuntimeLocalizationAssets)
                errors.Add("runtimeAssetDecision.mutatedRuntimeLocalizationAssets must be false for this handoff.");

            if (!StringEquals(runtimeAssetDecision.activeEnglishTableObserved, ExpectedRuntimeLocalizationTable))
                errors.Add("runtimeAssetDecision.activeEnglishTableObserved must be '" + ExpectedRuntimeLocalizationTable + "'.");

            if (IsBlank(runtimeAssetDecision.reason))
                errors.Add("runtimeAssetDecision.reason is missing.");
        }

        private static void ValidateHashContract(HashContract hashContract, List<string> errors)
        {
            if (hashContract == null)
            {
                errors.Add("hashContract is missing.");
                return;
            }

            if (!StringEquals(hashContract.algorithm, ExpectedHashAlgorithm))
                errors.Add("hashContract.algorithm must be '" + ExpectedHashAlgorithm + "'.");

            if (!StringEquals(hashContract.runtimeMatch, ExpectedHashRuntimeMatch))
                errors.Add("hashContract.runtimeMatch must be '" + ExpectedHashRuntimeMatch + "'.");

            string runtimeOffsetBasis = "0x" + LocHash.FnvOffsetBasis.ToString("X8");
            if (!StringEquals(hashContract.offsetBasis, runtimeOffsetBasis))
                errors.Add("hashContract.offsetBasis must match LocHash.FnvOffsetBasis '" + runtimeOffsetBasis + "'.");

            string runtimePrime = "0x" + LocHash.FnvPrime.ToString("X8");
            if (!StringEquals(hashContract.prime, runtimePrime))
                errors.Add("hashContract.prime must match LocHash.FnvPrime '" + runtimePrime + "'.");
        }

        private static void ValidateMissionFlags(
            MissionFlagEntry[] missionFlags,
            HashSet<string> declaredFlags,
            HashSet<int> declaredFlagHashes,
            List<string> errors)
        {
            if (missionFlags == null)
            {
                errors.Add("missionFlags is missing.");
                return;
            }

            if (missionFlags.Length != ExpectedFlagCount)
                errors.Add("missionFlags must contain " + ExpectedFlagCount + " entries, found " + missionFlags.Length + ".");

            for (int i = 0; i < missionFlags.Length; i++)
            {
                MissionFlagEntry entry = missionFlags[i];
                if (entry == null)
                {
                    errors.Add("missionFlags[" + i + "] is null.");
                    continue;
                }

                if (IsBlank(entry.flag))
                {
                    errors.Add("missionFlags[" + i + "].flag is empty.");
                    continue;
                }

                if (!entry.flag.StartsWith(OutpostPrefix, StringComparison.Ordinal))
                    errors.Add("missionFlags[" + i + "] '" + entry.flag + "' must use the '" + OutpostPrefix + "' prefix.");

                if (!declaredFlags.Add(entry.flag))
                    errors.Add("missionFlags duplicate flag '" + entry.flag + "'.");

                int flagHash = LocHash.Compute(entry.flag);
                if (flagHash == 0)
                    errors.Add("missionFlags[" + i + "] '" + entry.flag + "' resolves to LocHash 0.");

                if (!declaredFlagHashes.Add(flagHash))
                    errors.Add("missionFlags hash collision at '" + entry.flag + "' hash 0x" + unchecked((uint)flagHash).ToString("X8") + ".");

                if (IsBlank(entry.band))
                    errors.Add("missionFlags[" + i + "] '" + entry.flag + "' has no band.");

                if (IsBlank(entry.description))
                    errors.Add("missionFlags[" + i + "] '" + entry.flag + "' has no description.");
            }
        }

        private static void ValidateTopologicalOrder(
            string[] topologicalOrder,
            HashSet<string> declaredFlags,
            List<string> errors)
        {
            if (topologicalOrder == null || topologicalOrder.Length == 0)
            {
                errors.Add("topologicalOrder is missing or empty.");
                return;
            }

            if (topologicalOrder.Length != ExpectedFlagCount)
                errors.Add("topologicalOrder must contain " + ExpectedFlagCount + " entries, found " + topologicalOrder.Length + ".");

            var seen = new HashSet<string>(topologicalOrder.Length, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[topologicalOrder.Length] — editor-only topological duplicate check — owner: OutpostFailSafeHandoffValidator
            for (int i = 0; i < topologicalOrder.Length; i++)
            {
                string flag = topologicalOrder[i];
                ValidateDeclaredFlag(flag, declaredFlags, "topologicalOrder[" + i + "]", errors);

                if (!IsBlank(flag) && !seen.Add(flag))
                    errors.Add("topologicalOrder duplicate flag '" + flag + "'.");
            }

            if (!StringEquals(topologicalOrder[0], "outpost.generated"))
                errors.Add("topologicalOrder must start with outpost.generated.");

            if (!StringEquals(topologicalOrder[topologicalOrder.Length - 1], "outpost.mission_complete"))
                errors.Add("topologicalOrder must end with outpost.mission_complete.");

            var missingFromOrder = new List<string>(8); // COLD ALLOC: List<string>[8] — editor-only missing topological refs — owner: OutpostFailSafeHandoffValidator
            AddMissing(declaredFlags, seen, missingFromOrder);

            for (int i = 0; i < missingFromOrder.Count; i++)
                errors.Add("topologicalOrder does not include declared flag '" + missingFromOrder[i] + "'.");
        }

        private static void ValidateFallbacks(
            FallbackEntry[] fallbacks,
            HashSet<string> declaredFlags,
            List<string> errors)
        {
            if (fallbacks == null)
            {
                errors.Add("fallbacks is missing.");
                return;
            }

            if (fallbacks.Length != ExpectedFallbackCount)
                errors.Add("fallbacks must contain " + ExpectedFallbackCount + " entries, found " + fallbacks.Length + ".");

            for (int i = 0; i < fallbacks.Length; i++)
            {
                FallbackEntry entry = fallbacks[i];
                if (entry == null)
                {
                    errors.Add("fallbacks[" + i + "] is null.");
                    continue;
                }

                if (IsBlank(entry.risk))
                    errors.Add("fallbacks[" + i + "].risk is empty.");

                if (IsBlank(entry.constraint))
                    errors.Add("fallbacks[" + i + "].constraint is empty.");

                ValidateDeclaredFlag(entry.setFlag, declaredFlags, "fallbacks[" + i + "].setFlag", errors);
                ValidateOutpostReferences(entry.trigger, declaredFlags, "fallbacks[" + i + "].trigger", errors);
                ValidateGasReferences(entry.trigger, "fallbacks[" + i + "].trigger", errors);
            }
        }

        private static void ValidateLocalizationEntries(
            LocalizationEntry[] localizationEntries,
            HashSet<string> declaredFlags,
            List<string> errors)
        {
            if (localizationEntries == null)
            {
                errors.Add("localizationEntries is missing.");
                return;
            }

            int tooltipCount = 0;
            int logCount = 0;
            var locIds = new HashSet<string>(localizationEntries.Length, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[localizationEntries.Length] — editor-only LocID duplicate check — owner: OutpostFailSafeHandoffValidator
            var locHashes = new HashSet<int>(localizationEntries.Length); // COLD ALLOC: HashSet<int>[localizationEntries.Length] — editor-only LocHash collision check — owner: OutpostFailSafeHandoffValidator

            for (int i = 0; i < localizationEntries.Length; i++)
            {
                LocalizationEntry entry = localizationEntries[i];
                if (entry == null)
                {
                    errors.Add("localizationEntries[" + i + "] is null.");
                    continue;
                }

                if (IsBlank(entry.locId))
                {
                    errors.Add("localizationEntries[" + i + "].locId is empty.");
                    continue;
                }

                if (!locIds.Add(entry.locId))
                    errors.Add("localizationEntries duplicate locId '" + entry.locId + "'.");

                int locHash = LocHash.Compute(entry.locId);
                string expectedHash = "0x" + unchecked((uint)locHash).ToString("X8");
                if (!StringEquals(entry.hash, expectedHash))
                    errors.Add("localizationEntries[" + i + "] '" + entry.locId + "' hash mismatch. Expected " + expectedHash + ", found '" + entry.hash + "'.");

                if (!locHashes.Add(locHash))
                    errors.Add("localizationEntries hash collision at '" + entry.locId + "' hash " + expectedHash + ".");

                if (!StringEquals(entry.layer, "Narrative"))
                    errors.Add("localizationEntries[" + i + "] '" + entry.locId + "' must use Narrative layer.");

                if (IsBlank(entry.text))
                    errors.Add("localizationEntries[" + i + "] '" + entry.locId + "' has empty text.");

                if (StringEquals(entry.category, "outpost_tooltip"))
                {
                    tooltipCount++;
                    ValidateDeclaredFlag(entry.triggerFlag, declaredFlags, "localizationEntries[" + i + "].triggerFlag", errors);
                    ValidateDeclaredFlag(entry.suppressFlag, declaredFlags, "localizationEntries[" + i + "].suppressFlag", errors);
                    continue;
                }

                if (StringEquals(entry.category, "outpost_log"))
                {
                    logCount++;
                    if (IsBlank(entry.requiredState))
                        errors.Add("localizationEntries[" + i + "] '" + entry.locId + "' has no requiredState.");

                    ValidateOutpostReferences(entry.requiredState, declaredFlags, "localizationEntries[" + i + "].requiredState", errors);
                    ValidateGasReferences(entry.requiredState, "localizationEntries[" + i + "].requiredState", errors);
                    ValidateDeclaredFlag(entry.commitFlag, declaredFlags, "localizationEntries[" + i + "].commitFlag", errors);
                    continue;
                }

                errors.Add("localizationEntries[" + i + "] '" + entry.locId + "' has unsupported category '" + entry.category + "'.");
            }

            if (tooltipCount != ExpectedTooltipCount)
                errors.Add("Expected " + ExpectedTooltipCount + " outpost_tooltip entries, found " + tooltipCount + ".");

            if (logCount != ExpectedLogCount)
                errors.Add("Expected " + ExpectedLogCount + " outpost_log entries, found " + logCount + ".");
        }

        private static void ValidateGasConstraints(GasConstraints gasConstraints, List<string> errors)
        {
            if (gasConstraints == null)
            {
                errors.Add("gasConstraints is missing.");
                return;
            }

            if (gasConstraints.oxygenStandardKpa <= 0f)
                errors.Add("gasConstraints.oxygenStandardKpa must be positive.");
            else
                ValidateFloatEquals(
                    gasConstraints.oxygenStandardKpa,
                    ExpectedOxygenStandardKpa,
                    "gasConstraints.oxygenStandardKpa",
                    errors);

            if (gasConstraints.playerOxygenDrainKpaPerSecond <= 0f)
                errors.Add("gasConstraints.playerOxygenDrainKpaPerSecond must be positive.");
            else
                ValidateFloatEquals(
                    gasConstraints.playerOxygenDrainKpaPerSecond,
                    ExpectedPlayerOxygenDrainKpaPerSecond,
                    "gasConstraints.playerOxygenDrainKpaPerSecond",
                    errors);

            if (gasConstraints.playerCo2ProductionKpaPerSecond <= 0f)
                errors.Add("gasConstraints.playerCo2ProductionKpaPerSecond must be positive.");
            else
                ValidateFloatEquals(
                    gasConstraints.playerCo2ProductionKpaPerSecond,
                    ExpectedPlayerCo2ProductionKpaPerSecond,
                    "gasConstraints.playerCo2ProductionKpaPerSecond",
                    errors);

            if (gasConstraints.fireOxygenDrainKpaPerSecond <= 0f)
                errors.Add("gasConstraints.fireOxygenDrainKpaPerSecond must be positive.");
            else
                ValidateFloatEquals(
                    gasConstraints.fireOxygenDrainKpaPerSecond,
                    ExpectedFireOxygenDrainKpaPerSecond,
                    "gasConstraints.fireOxygenDrainKpaPerSecond",
                    errors);

            if (gasConstraints.scrubberCo2RemovalKpaPerSecond <= 0f)
                errors.Add("gasConstraints.scrubberCo2RemovalKpaPerSecond must be positive.");
            else
                ValidateFloatEquals(
                    gasConstraints.scrubberCo2RemovalKpaPerSecond,
                    ExpectedScrubberCo2RemovalKpaPerSecond,
                    "gasConstraints.scrubberCo2RemovalKpaPerSecond",
                    errors);

            if (gasConstraints.unpoweredSealedCriticalReadCapSeconds > MaxUnpoweredCriticalReadSeconds)
            {
                errors.Add(
                    "gasConstraints.unpoweredSealedCriticalReadCapSeconds must be <= " +
                    MaxUnpoweredCriticalReadSeconds.ToString("0") +
                    ".");
            }

            if (IsBlank(gasConstraints.rule) ||
                gasConstraints.rule.IndexOf("does not create oxygen", StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add("gasConstraints.rule must state that Ghost Power/scrubbers do not create oxygen.");
            }
        }

        private static void ValidateMissionDoc(
            string projectRoot,
            SourceAuthority sourceAuthority,
            HashSet<string> declaredFlags,
            List<string> errors,
            List<string> warnings)
        {
            string missionDocPath = Path.Combine(projectRoot, MissionDocRelativePath);
            if (!File.Exists(missionDocPath))
            {
                warnings.Add("Mission prose doc not found at " + MissionDocRelativePath + "; JSON-only validation ran.");
                return;
            }

            string missionDocText = File.ReadAllText(missionDocPath);
            ValidateMissionDocSourceAuthority(missionDocText, sourceAuthority, errors);
            ValidateStaleNeedles(missionDocText, MissionDocRelativePath, errors);

            var docReferences = new HashSet<string>(declaredFlags.Count, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[declaredFlags.Count] — editor-only prose/json flag parity check — owner: OutpostFailSafeHandoffValidator
            ExtractOutpostReferences(missionDocText, docReferences);

            var missingInJson = new List<string>(8); // COLD ALLOC: List<string>[8] — editor-only missing prose refs — owner: OutpostFailSafeHandoffValidator
            var missingInDoc = new List<string>(8); // COLD ALLOC: List<string>[8] — editor-only missing json refs — owner: OutpostFailSafeHandoffValidator

            AddMissing(docReferences, declaredFlags, missingInJson);
            AddMissing(declaredFlags, docReferences, missingInDoc);

            for (int i = 0; i < missingInJson.Count; i++)
                errors.Add(MissionDocRelativePath + " references undeclared flag '" + missingInJson[i] + "'.");

            for (int i = 0; i < missingInDoc.Count; i++)
                errors.Add(MissionDocRelativePath + " does not reference declared flag '" + missingInDoc[i] + "'.");
        }

        private static void ValidateMissionDocSourceAuthority(
            string missionDocText,
            SourceAuthority sourceAuthority,
            List<string> errors)
        {
            if (missionDocText.IndexOf(StaleBatchAuthorityToken, StringComparison.Ordinal) >= 0)
                errors.Add(MissionDocRelativePath + " contains stale batch authority token: '" + StaleBatchAuthorityToken + "'.");

            if (sourceAuthority == null)
                return;

            if (StringEquals(sourceAuthority.status, ExpectedSourceAuthorityStatus) &&
                missionDocText.IndexOf(ExpectedSourceAuthorityStatus, StringComparison.Ordinal) < 0)
            {
                errors.Add(MissionDocRelativePath + " must state '" + ExpectedSourceAuthorityStatus + "'.");
            }
        }

        private static void ValidateJsonOutpostReferences(
            string jsonText,
            HashSet<string> declaredFlags,
            List<string> errors)
        {
            var jsonReferences = new HashSet<string>(declaredFlags.Count, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[declaredFlags.Count] — editor-only json ref check — owner: OutpostFailSafeHandoffValidator
            ExtractOutpostReferences(jsonText, jsonReferences);

            var missing = new List<string>(8); // COLD ALLOC: List<string>[8] — editor-only undeclared json refs — owner: OutpostFailSafeHandoffValidator
            AddMissing(jsonReferences, declaredFlags, missing);

            for (int i = 0; i < missing.Count; i++)
                errors.Add(HandoffRelativePath + " references undeclared flag '" + missing[i] + "'.");
        }

        private static void ValidateDeclaredFlag(
            string flag,
            HashSet<string> declaredFlags,
            string context,
            List<string> errors)
        {
            if (IsBlank(flag))
            {
                errors.Add(context + " is empty.");
                return;
            }

            if (!declaredFlags.Contains(flag))
                errors.Add(context + " references undeclared flag '" + flag + "'.");
        }

        private static void ValidateOutpostReferences(
            string text,
            HashSet<string> declaredFlags,
            string context,
            List<string> errors)
        {
            if (IsBlank(text))
                return;

            var references = new HashSet<string>(8, StringComparer.Ordinal); // COLD ALLOC: HashSet<string>[8] — editor-only text ref extraction — owner: OutpostFailSafeHandoffValidator
            ExtractOutpostReferences(text, references);

            var missing = new List<string>(4); // COLD ALLOC: List<string>[4] — editor-only undeclared text refs — owner: OutpostFailSafeHandoffValidator
            AddMissing(references, declaredFlags, missing);

            for (int i = 0; i < missing.Count; i++)
                errors.Add(context + " references undeclared flag '" + missing[i] + "'.");
        }

        private static void ValidateGasReferences(string text, string context, List<string> errors)
        {
            if (IsBlank(text))
                return;

            if (text.IndexOf(LegacyRoomFlagPrefix, StringComparison.Ordinal) >= 0)
                errors.Add(context + " uses legacy '" + LegacyRoomFlagPrefix + "' token; use GasDynamicsRoomFlags.*.");

            ValidateBareGasFlagToken(text, "InternalFire", context, errors);
            ValidateBareGasFlagToken(text, "Breached", context, errors);
            ValidateBareGasFlagToken(text, "ScrubberInstalled", context, errors);
            ValidateBareGasFlagToken(text, "Occupied", context, errors);

            if (text.IndexOf("Submerged", StringComparison.Ordinal) >= 0 &&
                text.IndexOf(SubmergedScalarToken, StringComparison.Ordinal) < 0)
            {
                errors.Add(context + " uses bare Submerged; use " + SubmergedScalarToken + " scalar because GasDynamicsRoomFlags has no Submerged flag.");
            }

            int searchIndex = 0;
            while (searchIndex < text.Length)
            {
                int foundIndex = text.IndexOf(GasDynamicsRoomFlagPrefix, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    break;

                int endIndex = foundIndex + GasDynamicsRoomFlagPrefix.Length;
                while (endIndex < text.Length && IsGasFlagChar(text[endIndex]))
                    endIndex++;

                string token = text.Substring(foundIndex, endIndex - foundIndex);
                if (!IsAllowedGasDynamicsRoomFlag(token))
                    errors.Add(context + " references unsupported gas room flag '" + token + "'.");

                searchIndex = endIndex;
            }
        }

        private static void ValidateBareGasFlagToken(
            string text,
            string token,
            string context,
            List<string> errors)
        {
            int searchIndex = 0;
            while (searchIndex < text.Length)
            {
                int foundIndex = text.IndexOf(token, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    break;

                int endIndex = foundIndex + token.Length;
                bool tokenStartsClean = foundIndex == 0 || !IsGasFlagChar(text[foundIndex - 1]);
                bool tokenEndsClean = endIndex >= text.Length || !IsGasFlagChar(text[endIndex]);
                bool hasRequiredPrefix =
                    foundIndex >= GasDynamicsRoomFlagPrefix.Length &&
                    string.CompareOrdinal(
                        text,
                        foundIndex - GasDynamicsRoomFlagPrefix.Length,
                        GasDynamicsRoomFlagPrefix,
                        0,
                        GasDynamicsRoomFlagPrefix.Length) == 0;

                if (tokenStartsClean && tokenEndsClean && !hasRequiredPrefix)
                    errors.Add(context + " uses bare gas room flag '" + token + "'; use GasDynamicsRoomFlags." + token + ".");

                searchIndex = endIndex;
            }
        }

        private static void ValidateStaleNeedles(string text, string context, List<string> errors)
        {
            for (int i = 0; i < StaleAliasNeedles.Length; i++)
            {
                string needle = StaleAliasNeedles[i];
                if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    errors.Add(context + " contains stale flag alias '" + needle + "'.");
            }

            for (int i = 0; i < StaleCountNeedles.Length; i++)
            {
                string needle = StaleCountNeedles[i];
                if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    errors.Add(context + " contains stale count text '" + needle + "'.");
            }
        }

        private static void ExtractOutpostReferences(string text, HashSet<string> references)
        {
            if (string.IsNullOrEmpty(text))
                return;

            int searchIndex = 0;
            while (searchIndex < text.Length)
            {
                int foundIndex = text.IndexOf(OutpostPrefix, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    break;

                int endIndex = foundIndex + OutpostPrefix.Length;
                while (endIndex < text.Length && IsFlagChar(text[endIndex]))
                    endIndex++;

                references.Add(text.Substring(foundIndex, endIndex - foundIndex));
                searchIndex = endIndex;
            }
        }

        private static void AddMissing(
            HashSet<string> source,
            HashSet<string> target,
            List<string> missing)
        {
            var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string value = enumerator.Current;
                if (!target.Contains(value))
                    missing.Add(value);
            }
        }

        private static bool IsFlagChar(char value)
        {
            return value == '_' ||
                   (value >= 'a' && value <= 'z') ||
                   (value >= '0' && value <= '9');
        }

        private static bool IsGasFlagChar(char value)
        {
            return value == '_' ||
                   (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= '0' && value <= '9');
        }

        private static bool IsAllowedGasDynamicsRoomFlag(string token)
        {
            for (int i = 0; i < AllowedGasDynamicsRoomFlags.Length; i++)
            {
                if (StringEquals(token, AllowedGasDynamicsRoomFlags[i]))
                    return true;
            }

            return false;
        }

        private static void ValidateFloatEquals(float actual, float expected, string label, List<string> errors)
        {
            if (Mathf.Abs(actual - expected) > GasConstraintEpsilon)
                errors.Add(label + " must match GasDynamicsSolver default " + expected + ".");
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static string ResolveProjectRoot()
        {
            string assetsPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(assetsPath))
                return Directory.GetCurrentDirectory();

            return Path.GetFullPath(Path.Combine(assetsPath, ".."));
        }

#pragma warning disable CS0649
        [Serializable]
        private sealed class HandoffRoot
        {
            public string schema;
            public string agent;
            public string evidenceClass;
            public string sourceBatch;
            public string requestedBatch;
            public bool requestedBatchPresent;
            public SourceAuthority sourceAuthority;
            public RuntimeAssetDecision runtimeAssetDecision;
            public HashContract hashContract;
            public MissionFlagEntry[] missionFlags;
            public string[] topologicalOrder;
            public FallbackEntry[] fallbacks;
            public LocalizationEntry[] localizationEntries;
            public GasConstraints gasConstraints;
        }

        [Serializable]
        private sealed class SourceAuthority
        {
            public string status;
            public string expectedPromptId;
            public string expectedPromptRole;
            public bool activeBatchContainsPrompt;
            public string policy;
        }

        [Serializable]
        private sealed class RuntimeAssetDecision
        {
            public bool mutatedRuntimeLocalizationAssets;
            public string activeEnglishTableObserved;
            public string reason;
        }

        [Serializable]
        private sealed class HashContract
        {
            public string algorithm;
            public string runtimeMatch;
            public string offsetBasis;
            public string prime;
        }

        [Serializable]
        private sealed class MissionFlagEntry
        {
            public string flag;
            public string band;
            public string description;
        }

        [Serializable]
        private sealed class FallbackEntry
        {
            public string risk;
            public string trigger;
            public string setFlag;
            public string constraint;
        }

        [Serializable]
        private sealed class LocalizationEntry
        {
            public string locId;
            public string hash;
            public string layer;
            public string category;
            public string triggerFlag;
            public string suppressFlag;
            public string requiredState;
            public string commitFlag;
            public string text;
        }

        [Serializable]
        private sealed class GasConstraints
        {
            public float oxygenStandardKpa;
            public float playerOxygenDrainKpaPerSecond;
            public float playerCo2ProductionKpaPerSecond;
            public float fireOxygenDrainKpaPerSecond;
            public float scrubberCo2RemovalKpaPerSecond;
            public float unpoweredSealedCriticalReadCapSeconds;
            public string rule;
        }
#pragma warning restore CS0649
    }
}
#endif
