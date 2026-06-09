using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton.Localization;
using Hecton8.Narrative;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Privodit starye ili chastichno pustye seyvy k tekuschemu formatu.
    /// Delaet tolko bezopasnye pravki: dozapolnyaet nedostayuschie polya,
    /// normalizuet schetchiki i vystavlyaet defolty tam, gde staryy seyv
    /// fizicheski ne mog hranit nuzhnye dannye.
    /// </summary>
    public static class SaveDataMigration
    {
        private const float LegacyModuleIntegrityDefault = 100f;
        private const string DefaultBeaconLabelPrefix = "BEACON";
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;
        private const int MaxAtlasRevealStage = 4;
        private const int FirstHourKnownMilestoneMask = (1 << 6) - 1;
        private const int FirstHourKnownGuidanceMask = (1 << 11) - 1;
        private const int PreV73RepairVersion = 73;
        private const ushort MaxDataArchaeologyPartialProgressPermille = 999;
        private const byte MaxDataArchaeologyScanStateValue = 2;
        private const int MaxVoxelDeltaChunks = 65536;
        private const int MaxVoxelDeltaCarvingOperations = 65536;

        private static bool TrimListToMax<T>(List<T> values, int maxCount, string step, List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            values.RemoveRange(safeMax, values.Count - safeMax);
            steps.Add(step);
            return true;
        }

        private static bool CompactNonBlankStringListEntries(List<string> values, string step, List<string> steps)
        {
            if (values == null)
                return false;

            bool changed = false;
            int writeIndex = 0;
            for (int i = 0; i < values.Count; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                {
                    changed = true;
                    continue;
                }

                if (writeIndex != i || !string.Equals(values[writeIndex], value, StringComparison.Ordinal))
                {
                    values[writeIndex] = value;
                    changed = true;
                }

                writeIndex++;
            }

            if (values.Count > writeIndex)
            {
                values.RemoveRange(writeIndex, values.Count - writeIndex);
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool CompactNonBlankStringFloatPairs(
            List<string> ids,
            List<float> values,
            string step,
            List<string> steps)
        {
            if (ids == null || values == null)
                return false;

            int bound = Math.Min(ids.Count, values.Count);
            bool changed = ids.Count != values.Count;
            int writeIndex = 0;
            for (int i = 0; i < bound; i++)
            {
                string id = SaveData.SanitizePersistenceString(ids[i]);
                if (string.IsNullOrWhiteSpace(id))
                {
                    changed = true;
                    continue;
                }

                if (writeIndex != i ||
                    !string.Equals(ids[writeIndex], id, StringComparison.Ordinal))
                {
                    ids[writeIndex] = id;
                    values[writeIndex] = values[i];
                    changed = true;
                }

                writeIndex++;
            }

            if (ids.Count > writeIndex)
            {
                ids.RemoveRange(writeIndex, ids.Count - writeIndex);
                changed = true;
            }

            if (values.Count > writeIndex)
            {
                values.RemoveRange(writeIndex, values.Count - writeIndex);
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool CompactNonBlankStringArrayEntries(
            string[] values,
            ref int count,
            int maxCount,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            int safeCount = math.clamp(count, 0, math.min(values.Length, Math.Max(maxCount, 0)));
            bool changed = safeCount != count;
            int writeIndex = 0;
            for (int i = 0; i < safeCount; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (values[i] != string.Empty)
                    {
                        values[i] = string.Empty;
                        changed = true;
                    }

                    continue;
                }

                if (writeIndex != i || !string.Equals(values[writeIndex], value, StringComparison.Ordinal))
                {
                    values[writeIndex] = value;
                    changed = true;
                }

                writeIndex++;
            }

            for (int i = writeIndex; i < safeCount; i++)
            {
                if (values[i] == string.Empty)
                    continue;

                values[i] = string.Empty;
                changed = true;
            }

            if (count != writeIndex)
            {
                count = writeIndex;
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool EnsureNonNullStringDictionaryValues(
            Dictionary<string, string> values,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            List<string> keysToRepair = null;
            Dictionary<string, string>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, string> pair = enumerator.Current;
                if (pair.Value != null)
                    continue;

                keysToRepair ??= new List<string>();
                keysToRepair.Add(pair.Key);
            }

            enumerator.Dispose();
            if (keysToRepair == null)
                return false;

            for (int i = 0; i < keysToRepair.Count; i++)
                values[keysToRepair[i]] = string.Empty;

            steps.Add(step);
            return true;
        }

        private static bool EnsureNonBlankStringDictionaryKeys<TValue>(
            Dictionary<string, TValue> values,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            List<string> keysToRemove = null;
            List<KeyValuePair<string, TValue>> keysToAdd = null;
            HashSet<string> pendingCanonicalKeys = null;
            Dictionary<string, TValue>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, TValue> pair = enumerator.Current;
                string key = pair.Key;
                string canonicalKey = SaveData.SanitizePersistenceString(key);
                if (canonicalKey.Length == 0)
                {
                    keysToRemove ??= new List<string>(1);
                    keysToRemove.Add(key);
                    continue;
                }

                if (string.Equals(key, canonicalKey, StringComparison.Ordinal))
                    continue;

                keysToRemove ??= new List<string>(1);
                keysToRemove.Add(key);
                if (values.ContainsKey(canonicalKey))
                    continue;

                pendingCanonicalKeys ??= new HashSet<string>(StringComparer.Ordinal);
                if (!pendingCanonicalKeys.Add(canonicalKey))
                    continue;

                keysToAdd ??= new List<KeyValuePair<string, TValue>>(1);
                keysToAdd.Add(new KeyValuePair<string, TValue>(canonicalKey, pair.Value));
            }

            enumerator.Dispose();
            if (keysToRemove == null && keysToAdd == null)
                return false;

            for (int i = 0; keysToRemove != null && i < keysToRemove.Count; i++)
            {
                string key = keysToRemove[i];
                if (key != null)
                    values.Remove(key);
            }

            for (int i = 0; keysToAdd != null && i < keysToAdd.Count; i++)
            {
                KeyValuePair<string, TValue> pair = keysToAdd[i];
                if (!values.ContainsKey(pair.Key))
                    values[pair.Key] = pair.Value;
            }

            steps.Add(step);
            return true;
        }

        private static bool TrimPairedListsToMax<T0, T1>(
            List<T0> values0,
            List<T1> values1,
            int maxCount,
            string step,
            List<string> steps)
        {
            if (values0 == null || values1 == null)
                return false;

            int safeCount = Math.Min(Math.Max(maxCount, 0), Math.Min(values0.Count, values1.Count));
            bool changed = false;

            if (values0.Count > safeCount)
            {
                values0.RemoveRange(safeCount, values0.Count - safeCount);
                changed = true;
            }

            if (values1.Count > safeCount)
            {
                values1.RemoveRange(safeCount, values1.Count - safeCount);
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool TrimDictionaryToMax<TKey, TValue>(
            Dictionary<TKey, TValue> values,
            int maxCount,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            List<TKey> keys = new List<TKey>(values.Keys);
            keys.Sort(CompareStableTrimKeys);
            for (int i = safeMax; i < keys.Count; i++)
                values.Remove(keys[i]);

            steps.Add(step);
            return true;
        }

        private static bool TrimHashSetToMax<T>(HashSet<T> values, int maxCount, string step, List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            List<T> sortedValues = new List<T>(values);
            sortedValues.Sort(CompareStableTrimKeys);
            for (int i = safeMax; i < sortedValues.Count; i++)
                values.Remove(sortedValues[i]);

            steps.Add(step);
            return true;
        }

        private static int CompareStableTrimKeys<T>(T left, T right)
        {
            object leftObject = left;
            object rightObject = right;
            if (ReferenceEquals(leftObject, rightObject))
                return 0;
            if (leftObject == null)
                return -1;
            if (rightObject == null)
                return 1;
            if (leftObject is string leftString && rightObject is string rightString)
                return string.CompareOrdinal(leftString, rightString);
            if (leftObject is IComparable<T> typedComparable)
                return typedComparable.CompareTo(right);
            if (leftObject is IComparable comparable)
                return comparable.CompareTo(rightObject);

            return string.CompareOrdinal(leftObject.ToString(), rightObject.ToString());
        }

        public static bool MigrateInPlace(SaveData data, out int originalVersion, out string summary)
        {
            originalVersion = data != null ? data.version : 0;
            summary = "No migration needed.";

            if (data == null)
                return false;

            int sourceVersion = data.version > 0 ? data.version : 1;
            if (sourceVersion > SaveData.CurrentVersion)
            {
                summary = $"unsupported future save data version {sourceVersion}; current reader supports {SaveData.CurrentVersion}";
                return false;
            }

            bool changed = false;
            List<string> steps = new List<string>(8);

            if (string.IsNullOrWhiteSpace(data.timestamp))
            {
                data.timestamp = DateTime.Now.ToString("O");
                changed = true;
                steps.Add("timestamp repaired");
            }

            double safeTotalPlayTime = SanitizeNonNegativeFinite(data.totalPlayTime);
            if (!Approximately(data.totalPlayTime, safeTotalPlayTime))
            {
                data.totalPlayTime = safeTotalPlayTime;
                changed = true;
                steps.Add("total play time repaired");
            }

            float safeFirstHourSessionTime = SanitizeNonNegativeFinite(data.firstHourSessionTime);
            if (!Approximately(data.firstHourSessionTime, safeFirstHourSessionTime))
            {
                data.firstHourSessionTime = safeFirstHourSessionTime;
                changed = true;
                steps.Add("first hour session time repaired");
            }

            int safeFirstHourMilestones = SanitizeFirstHourMilestones(data.firstHourMilestones);
            if (safeFirstHourMilestones != data.firstHourMilestones)
            {
                data.firstHourMilestones = safeFirstHourMilestones;
                changed = true;
                steps.Add("first hour milestones repaired");
            }

            int safeFirstHourGuidanceFlags = SanitizeFirstHourGuidanceFlags(data.firstHourGuidanceFlags);
            if (safeFirstHourGuidanceFlags != data.firstHourGuidanceFlags)
            {
                data.firstHourGuidanceFlags = safeFirstHourGuidanceFlags;
                changed = true;
                steps.Add("first hour guidance flags repaired");
            }

            int safeEndingChoice = SanitizeEndingChoice(data.endingChoice);
            bool safeEndingComplete = data.endingComplete && safeEndingChoice != 0;
            if (!safeEndingComplete)
                safeEndingChoice = 0;
            bool safeEndingConditionMet = data.endingConditionMet || safeEndingComplete;
            if (safeEndingChoice != data.endingChoice)
            {
                data.endingChoice = safeEndingChoice;
                changed = true;
                steps.Add("ending choice repaired");
            }
            if (safeEndingComplete != data.endingComplete)
            {
                data.endingComplete = safeEndingComplete;
                changed = true;
                steps.Add("ending completion repaired");
            }
            if (safeEndingConditionMet != data.endingConditionMet)
            {
                data.endingConditionMet = safeEndingConditionMet;
                changed = true;
                steps.Add("ending condition repaired");
            }

            if (data.toolDurabilityMap == null)
            {
                data.toolDurabilityMap = new Dictionary<string, float>(SaveData.MaxToolDurabilityRecords);
                changed = true;
                steps.Add("tool durability map created");
            }
            changed |= EnsureNonBlankStringDictionaryKeys(
                data.toolDurabilityMap,
                "tool durability keys repaired",
                steps);
            changed |= EnsureNonNegativeFiniteFloatDictionary(
                data.toolDurabilityMap,
                "tool durability values repaired",
                steps);
            changed |= TrimDictionaryToMax(
                data.toolDurabilityMap,
                SaveData.MaxToolDurabilityRecords,
                "tool durability map capped",
                steps);

            if (data.toolBrokenMap == null)
            {
                data.toolBrokenMap = new Dictionary<string, bool>(SaveData.MaxToolDurabilityRecords);
                changed = true;
                steps.Add("tool broken map created");
            }
            changed |= EnsureNonBlankStringDictionaryKeys(
                data.toolBrokenMap,
                "tool broken keys repaired",
                steps);
            changed |= TrimDictionaryToMax(
                data.toolBrokenMap,
                SaveData.MaxToolDurabilityRecords,
                "tool broken map capped",
                steps);

            if (data.CustomModData == null)
            {
                data.CustomModData = new Dictionary<string, string>(SaveData.MaxCustomModDataEntries);
                changed = true;
                steps.Add("custom mod data created");
            }
            changed |= EnsureNonBlankStringDictionaryKeys(
                data.CustomModData,
                "custom mod data keys repaired",
                steps);
            changed |= EnsureNonNullStringDictionaryValues(
                data.CustomModData,
                "custom mod data values repaired",
                steps);
            changed |= TrimDictionaryToMax(
                data.CustomModData,
                SaveData.MaxCustomModDataEntries,
                "custom mod data capped",
                steps);

            if (data.suitBrokenUpgradeIds == null)
            {
                data.suitBrokenUpgradeIds = new List<string>(SaveData.MaxSuitUpgradeIds);
                changed = true;
                steps.Add("suit broken upgrades created");
            }
            changed |= TrimListToMax(
                data.suitBrokenUpgradeIds,
                SaveData.MaxSuitUpgradeIds,
                "suit broken upgrades capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.suitBrokenUpgradeIds,
                "suit broken upgrade ids repaired",
                steps);

            ulong safeSuitUpgradeMask = SanitizeSuitUpgradeMask(data.suitUpgradeMask);
            if (safeSuitUpgradeMask != data.suitUpgradeMask)
            {
                data.suitUpgradeMask = safeSuitUpgradeMask;
                changed = true;
                steps.Add("suit upgrade mask repaired");
            }

            bool hadPackedBiomeCapacity = BiomeDiscoveryBitMask.HasExpectedCapacity(data.discoveredBiomeBitWords);
            if (!hadPackedBiomeCapacity)
            {
                BiomeDiscoveryBitMask.EnsureCapacity(ref data.discoveredBiomeBitWords);
                changed = true;
                steps.Add("discovered biome bit words created");
            }

            changed |= EnsureValidDiscoveredBiomeIds(data.discoveredBiomeIds, steps);
            changed |= TrimHashSetToMax(
                data.discoveredBiomeIds,
                SaveData.MaxLegacyDiscoveredBiomeIds,
                "discovered biome set capped",
                steps);
            if (BiomeDiscoveryBitMask.SanitizeWords(data.discoveredBiomeBitWords))
            {
                changed = true;
                steps.Add("discovered biome bit words repaired");
            }

            if (!BiomeDiscoveryBitMask.HasAnySet(data.discoveredBiomeBitWords) &&
                data.discoveredBiomeIds != null &&
                data.discoveredBiomeIds.Count > 0)
            {
                BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
                changed = true;
                steps.Add("discovered biome set packed");
            }

            int normalizedLastDiscoveredBiomeId = NormalizeLastDiscoveredBiomeId(
                data.lastDiscoveredBiomeId,
                data.discoveredBiomeIds,
                data.discoveredBiomeBitWords);
            if (normalizedLastDiscoveredBiomeId != data.lastDiscoveredBiomeId)
            {
                data.lastDiscoveredBiomeId = normalizedLastDiscoveredBiomeId;
                changed = true;
                steps.Add("last discovered biome repaired");
            }

            changed |= EnsureNarrative(ref data, steps);

            changed |= EnsureHazardZoneRuntime(ref data.hazardZones, sourceVersion, steps);
            changed |= EnsureRadiationGrid(data, sourceVersion, steps);
            changed |= EnsureRtgDecay(data, steps);
            changed |= EnsurePlayerStatsAndKinematics(data, sourceVersion, steps);
            bool inventoryChanged = EnsureInventory(ref data.inventory, steps);
            changed |= inventoryChanged;
            changed |= EnsureInventoryShadow(data, inventoryChanged, steps);
            changed |= EnsureWorldState(ref data.worldState, steps);
            changed |= EnsureProceduralWorldState(ref data.proceduralWorldState, steps);
            changed |= EnsureConstruction(ref data.construction, sourceVersion, steps);
            changed |= EnsureScanLog(ref data.scanLog, steps);
            changed |= EnsureBarter(ref data.barter, steps);
            changed |= EnsureFieldOperations(ref data.fieldOperations, steps);
            changed |= EnsureBeaconNetwork(ref data.beaconNetwork, steps);
            changed |= EnsureExplorationMap(ref data.explorationMap, steps);
            changed |= EnsurePdaLogbook(ref data.pdaLogbook, steps);
            changed |= EnsurePdaMarkers(ref data.pdaMarkers, steps);
            changed |= EnsurePdaAdvisories(ref data.pdaAdvisories, steps);
            changed |= EnsureProceduralLore(ref data.proceduralLore, steps);
            changed |= EnsureAchievements(ref data.achievements, steps);
            changed |= EnsureRunModifiers(ref data.runModifiers, steps);
            changed |= EnsureMetaCampaign(ref data.metaCampaign, steps);
            changed |= EnsureResourceScarcity(ref data.resourceScarcity, steps);
            changed |= EnsureEnvironmentalStrain(ref data.environmentalStrain, steps);
            changed |= EnsureEcosystemState(ref data.ecosystemState, steps);
            changed |= EnsureProceduralTerrainIdentity(ref data.proceduralTerrainIdentity, steps);
            changed |= EnsureExternalScavengerSites(ref data.externalScavengerSites, steps);
            changed |= EnsureVoxelDeltaPersistence(ref data.voxelDeltaPersistence, steps);
            changed |= EnsureLoreSystems(ref data, sourceVersion, steps);
            changed |= EnsureAtlas6DirectiveState(data, steps);
            changed |= EnsureAtlas6Liability(data, sourceVersion, steps);
            changed |= EnsurePlayerExpression(ref data, steps);
            changed |= EnsurePerformanceSettings(ref data, sourceVersion, steps);
            changed |= EnsureCelestialLightPhase(data, sourceVersion, steps);

            if (data.version != SaveData.CurrentVersion)
            {
                data.version = SaveData.CurrentVersion;
                changed = true;
                steps.Add($"version upgraded from {sourceVersion} to {SaveData.CurrentVersion}");
            }

            if (changed)
                summary = string.Join(", ", steps);

            return changed;
        }

        private static bool EnsureHazardZoneRuntime(ref HazardZoneRuntimeDTO dto, int sourceVersion, List<string> steps)
        {
            float safeHazardToxicityDose = sourceVersion >= SaveData.HazardZoneRuntimePersistenceVersion
                ? ClampFinite(dto.toxicityDose, 0f, SaveData.HazardZoneMaxPersistedToxicityDose)
                : 0f;
            float safePulseAccumulator = sourceVersion >= SaveData.HazardZoneRuntimePersistenceVersion
                ? ClampFinite(dto.toxicityPulseAccumulatorSeconds, 0f, SaveData.HazardZoneMaxPersistedToxicityPulseSeconds)
                : 0f;

            if (safeHazardToxicityDose <= SaveData.HazardZoneToxicityDamageDoseThreshold)
                safePulseAccumulator = 0f;

            bool changed = !Approximately(dto.toxicityDose, safeHazardToxicityDose) ||
                           !Approximately(dto.toxicityPulseAccumulatorSeconds, safePulseAccumulator);

            dto.toxicityDose = safeHazardToxicityDose;
            dto.toxicityPulseAccumulatorSeconds = safePulseAccumulator;

            if (changed)
                steps.Add("hazard zone toxicity state repaired");

            return changed;
        }

        private static bool EnsureRadiationGrid(SaveData data, int sourceVersion, List<string> steps)
        {
            float safeRadiationDose = sourceVersion >= SaveData.RadiationGridPersistenceVersion
                ? (math.isfinite(data.radiationDose) ? math.max(0f, data.radiationDose) : 0f)
                : 0f;
            double safeOriginX = sourceVersion >= SaveData.RadiationGridPersistenceVersion && math.isfinite(data.radiationGridOriginX)
                ? data.radiationGridOriginX
                : 0d;
            double safeOriginY = sourceVersion >= SaveData.RadiationGridPersistenceVersion && math.isfinite(data.radiationGridOriginY)
                ? data.radiationGridOriginY
                : 0d;
            double safeOriginZ = sourceVersion >= SaveData.RadiationGridPersistenceVersion && math.isfinite(data.radiationGridOriginZ)
                ? data.radiationGridOriginZ
                : 0d;
            float safeCellSize = sourceVersion >= SaveData.RadiationGridPersistenceVersion
                ? ClampFinite(
                    data.radiationGridCellSizeMeters,
                    SaveData.RadiationGridDefaultCellSizeMeters,
                    SaveData.RadiationGridMinCellSizeMeters,
                    SaveData.RadiationGridMaxCellSizeMeters)
                : SaveData.RadiationGridDefaultCellSizeMeters;
            int safeRleLength = sourceVersion >= SaveData.RadiationGridPersistenceVersion
                ? ClampRadiationGridRleLength(data.radiationGridRle, data.radiationGridRleLength)
                : 0;
            bool resizedPayload = data.radiationGridRle == null ||
                                  data.radiationGridRle.Length != SaveData.RadiationGridRleMaxBytes;

            bool changed = !Approximately(data.radiationDose, safeRadiationDose) ||
                           !Approximately(data.radiationGridOriginX, safeOriginX) ||
                           !Approximately(data.radiationGridOriginY, safeOriginY) ||
                           !Approximately(data.radiationGridOriginZ, safeOriginZ) ||
                           !Approximately(data.radiationGridCellSizeMeters, safeCellSize) ||
                           data.radiationGridRleLength != safeRleLength ||
                           resizedPayload;

            data.radiationDose = safeRadiationDose;
            data.radiationGridOriginX = safeOriginX;
            data.radiationGridOriginY = safeOriginY;
            data.radiationGridOriginZ = safeOriginZ;
            data.radiationGridCellSizeMeters = safeCellSize;
            data.radiationGridRleLength = safeRleLength;
            if (resizedPayload)
                SaveData.EnsureExactArrayCapacity(ref data.radiationGridRle, SaveData.RadiationGridRleMaxBytes);

            if (changed)
                steps.Add("radiation grid state repaired");

            return changed;
        }

        private static bool EnsureRtgDecay(SaveData data, List<string> steps)
        {
            if (data == null)
                return false;

            bool changed = false;
            int sourceLength = data.rtgDecaySourceIds != null ? data.rtgDecaySourceIds.Length : 0;
            int startLength = data.rtgStartTimesSeconds != null ? data.rtgStartTimesSeconds.Length : 0;
            int flagLength = data.rtgDecayFlags != null ? data.rtgDecayFlags.Length : 0;
            int safeCount = math.clamp(
                data.rtgDecayCount,
                0,
                math.min(SaveData.MaxRtgDecayRecords, sourceLength));

            if (data.rtgDecayCount != safeCount)
            {
                data.rtgDecayCount = safeCount;
                changed = true;
                steps.Add("rtg decay count clamped");
            }

            bool hadExactCapacity =
                data.rtgDecaySourceIds != null &&
                data.rtgDecaySourceIds.Length == SaveData.MaxRtgDecayRecords &&
                data.rtgStartTimesSeconds != null &&
                data.rtgStartTimesSeconds.Length == SaveData.MaxRtgDecayRecords &&
                data.rtgDecayFlags != null &&
                data.rtgDecayFlags.Length == SaveData.MaxRtgDecayRecords;

            if (!hadExactCapacity)
            {
                data.EnsureRtgDecayCapacity();
                changed = true;
                steps.Add("rtg decay arrays repaired");
            }

            if (startLength < safeCount || flagLength < safeCount)
            {
                changed = true;
                steps.Add("rtg decay partial records defaulted");
            }

            bool repairedRecords = false;
            for (int i = 0; i < safeCount; i++)
            {
                int safeSourceId = Math.Max(0, data.rtgDecaySourceIds[i]);
                double safeStartTime = SanitizeNonNegativeFinite(data.rtgStartTimesSeconds[i]);
                byte safeFlags = (byte)(data.rtgDecayFlags[i] & SaveData.RtgDecayPersistedFlagMask);

                if (data.rtgDecaySourceIds[i] == safeSourceId &&
                    Approximately(data.rtgStartTimesSeconds[i], safeStartTime) &&
                    data.rtgDecayFlags[i] == safeFlags)
                {
                    continue;
                }

                data.rtgDecaySourceIds[i] = safeSourceId;
                data.rtgStartTimesSeconds[i] = safeStartTime;
                data.rtgDecayFlags[i] = safeFlags;
                repairedRecords = true;
            }

            if (repairedRecords)
            {
                changed = true;
                steps.Add("rtg decay records repaired");
            }

            return changed;
        }

        private static bool EnsurePlayerStatsAndKinematics(SaveData data, int sourceVersion, List<string> steps)
        {
            PlayerStatsDTO safeStats = data.playerStats;
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref safeStats);
            bool defaultedPlayerHealth = false;
            if (sourceVersion < SaveData.PlayerHealthPersistenceVersion && safeStats.health <= 0f)
            {
                safeStats.health = SaveData.PlayerHealthDefault;
                defaultedPlayerHealth = true;
            }

            PlayerKinematicStateDTO safeKinematic = sourceVersion >= SaveData.FirstHourDtoLockPersistenceVersion
                ? data.playerKinematicState
                : PlayerKinematicStateDTO.FromPlayerStats(in safeStats);
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerKinematicState(ref safeKinematic);
            safeKinematic.ApplyTo(ref safeStats);
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref safeStats);

            bool changed = defaultedPlayerHealth ||
                           !SaveDataPlayerSurvivalSanitizer.PlayerStatsEqual(in data.playerStats, in safeStats) ||
                           !SaveDataPlayerSurvivalSanitizer.PlayerKinematicStateEqual(
                               in data.playerKinematicState,
                               in safeKinematic);

            data.playerStats = safeStats;
            data.playerKinematicState = safeKinematic;

            if (defaultedPlayerHealth)
                steps.Add("player health defaulted");

            if (changed)
                steps.Add("player survival state repaired");

            return changed;
        }

        private static float ClampFinite(float value, float min, float max)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : min;
        }

        private static float ClampFinite(float value, float fallback, float min, float max)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : math.clamp(fallback, min, max);
        }

        private static int ClampRadiationGridRleLength(byte[] payload, int byteLength)
        {
            if (payload == null)
                return 0;

            int payloadCapacity = math.min(payload.Length, SaveData.RadiationGridRleMaxBytes);
            return math.clamp(byteLength, 0, payloadCapacity);
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) <= 0.000001f;
        }

        private static bool Approximately(double a, double b)
        {
            return math.abs(a - b) <= 0.000001d;
        }

        private static int SanitizeFirstHourMilestones(int milestones)
        {
            return milestones >= 0 ? milestones & FirstHourKnownMilestoneMask : 0;
        }

        private static int SanitizeFirstHourGuidanceFlags(int guidanceFlags)
        {
            return guidanceFlags >= 0 ? guidanceFlags & FirstHourKnownGuidanceMask : 0;
        }

        private static int SanitizeEndingChoice(int endingChoice)
        {
            return endingChoice >= 0 && endingChoice <= 3 ? endingChoice : 0;
        }

        private static ulong SanitizeSuitUpgradeMask(ulong upgradeMask)
        {
            return upgradeMask & SaveData.SuitUpgradeSupportedMask;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static double SanitizeNonNegativeFinite(double value)
        {
            return math.isfinite(value) ? math.max(0d, value) : 0d;
        }

        private static bool EnsureNonNegativeFiniteFloatList(List<float> values, string step, List<string> steps)
        {
            if (values == null)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                float safeValue = SanitizeNonNegativeFinite(values[i]);
                if (Approximately(values[i], safeValue))
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool EnsureNonNegativeFiniteFloatDictionary(
            Dictionary<string, float> values,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            List<string> repairedKeys = null;
            Dictionary<string, float>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = enumerator.Current;
                float safeValue = SanitizeNonNegativeFinite(pair.Value);
                if (Approximately(pair.Value, safeValue))
                    continue;

                repairedKeys ??= new List<string>(4);
                repairedKeys.Add(pair.Key);
            }

            enumerator.Dispose();

            bool changed = repairedKeys != null;
            if (changed)
            {
                for (int i = 0; i < repairedKeys.Count; i++)
                {
                    string key = repairedKeys[i];
                    values[key] = SanitizeNonNegativeFinite(values[key]);
                }
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool EnsureInventory(ref InventoryDTO dto, List<string> steps)
        {
            bool changed = SaveDataInventorySanitizer.SanitizeInventory(ref dto);
            if (changed)
                steps.Add("inventory state repaired");

            return changed;
        }

        private static bool EnsureInventoryShadow(SaveData data, bool discardTransientPayload, List<string> steps)
        {
            if (data == null)
                return false;

            bool payloadChanged = false;
            if (discardTransientPayload)
            {
                payloadChanged = data.inventoryShadowPayloadLength != 0 ||
                                 data.inventoryShadowPayloadHash != 0u ||
                                 data.hasInventoryShadowPayload;
                data.inventoryShadowPayloadLength = 0;
                data.inventoryShadowPayloadHash = 0u;
                data.hasInventoryShadowPayload = false;
            }

            int inventoryShadowPayloadLength = discardTransientPayload
                ? 0
                : SaveDataInventorySanitizer.ResolveInventoryShadowPayloadLength(data);
            uint inventoryShadowPayloadHash = inventoryShadowPayloadLength > 0
                ? data.inventoryShadowPayloadHash
                : 0u;
            if (!discardTransientPayload &&
                inventoryShadowPayloadLength == 0 &&
                (data.inventoryShadowPayloadLength != 0 ||
                 data.inventoryShadowPayloadHash != 0u ||
                 data.hasInventoryShadowPayload))
            {
                payloadChanged = true;
                data.inventoryShadowPayloadLength = 0;
                data.inventoryShadowPayloadHash = 0u;
                data.hasInventoryShadowPayload = false;
            }

            if (!discardTransientPayload && inventoryShadowPayloadLength == 0)
            {
                inventoryShadowPayloadLength = SaveDataInventorySanitizer.ResolveInventoryShadowPayloadLength(
                    in data.inventoryShadow,
                    in data.inventory);
                inventoryShadowPayloadHash = inventoryShadowPayloadLength > 0 ? data.inventoryShadow.payloadHash : 0u;
            }

            bool changed = SaveDataInventorySanitizer.SanitizeInventoryShadow(
                ref data.inventoryShadow,
                in data.inventory,
                inventoryShadowPayloadLength,
                inventoryShadowPayloadHash,
                inventoryShadowPayloadLength > 0);
            changed |= payloadChanged;
            if (changed)
                steps.Add("inventory shadow repaired");

            return changed;
        }

        private static int NormalizeLastDiscoveredBiomeId(
            int lastDiscoveredBiomeId,
            HashSet<int> discoveredBiomeIds,
            long[] discoveredBiomeBitWords)
        {
            if (IsValidBiomeId(lastDiscoveredBiomeId) &&
                (BiomeDiscoveryBitMask.Contains(discoveredBiomeBitWords, lastDiscoveredBiomeId) ||
                 (discoveredBiomeIds != null && discoveredBiomeIds.Contains(lastDiscoveredBiomeId))))
            {
                return lastDiscoveredBiomeId;
            }

            if (BiomeDiscoveryBitMask.HasAnySet(discoveredBiomeBitWords))
                return BiomeDiscoveryBitMask.ResolveFallbackLastDiscoveredId(discoveredBiomeBitWords);

            if (discoveredBiomeIds != null)
            {
                for (int biomeId = BiomeDiscoveryBitMask.MinBiomeId; biomeId <= BiomeDiscoveryBitMask.MaxBiomeId; biomeId++)
                {
                    if (discoveredBiomeIds.Contains(biomeId))
                        return biomeId;
                }
            }

            return InvalidBiomeId;
        }

        private static bool EnsureValidDiscoveredBiomeIds(HashSet<int> discoveredBiomeIds, List<string> steps)
        {
            if (discoveredBiomeIds == null || discoveredBiomeIds.Count == 0)
                return false;

            List<int> idsToRemove = null;
            HashSet<int>.Enumerator enumerator = discoveredBiomeIds.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int biomeId = enumerator.Current;
                if (IsValidBiomeId(biomeId))
                    continue;

                idsToRemove ??= new List<int>();
                idsToRemove.Add(biomeId);
            }

            enumerator.Dispose();
            if (idsToRemove == null)
                return false;

            for (int i = 0; i < idsToRemove.Count; i++)
                discoveredBiomeIds.Remove(idsToRemove[i]);

            steps.Add("discovered biome set repaired");
            return true;
        }

        private static bool IsValidBiomeId(int biomeId)
        {
            return BiomeDiscoveryBitMask.IsValidBiomeId(biomeId);
        }

        private static bool EnsureWorldState(ref WorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            int depletedBound = ClampArrayLength(dto.depletedNodeIds, WorldStateDTO.MaxNodes);
            int pickupChunkBound = ClampArrayLength(dto.depletedPickupChunkKeys, WorldStateDTO.MaxPickupChunks);
            pickupChunkBound = math.min(
                pickupChunkBound,
                ClampArrayLength(dto.depletedPickupChunkWordStarts, WorldStateDTO.MaxPickupChunks));
            pickupChunkBound = math.min(
                pickupChunkBound,
                ClampArrayLength(dto.depletedPickupChunkWordCounts, WorldStateDTO.MaxPickupChunks));
            int pickupWordBound = ClampArrayLength(dto.depletedPickupWords, WorldStateDTO.MaxPickupWords);

            if (dto.depletedNodeIds == null || dto.depletedNodeIds.Length != WorldStateDTO.MaxNodes ||
                dto.depletedPickupChunkKeys == null || dto.depletedPickupChunkKeys.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordStarts == null || dto.depletedPickupChunkWordStarts.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordCounts == null || dto.depletedPickupChunkWordCounts.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupWords == null || dto.depletedPickupWords.Length != WorldStateDTO.MaxPickupWords)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("world state capacity repaired");
            }

            int clamped = math.clamp(dto.depletedCount, 0, depletedBound);
            if (clamped != dto.depletedCount)
            {
                dto.depletedCount = clamped;
                changed = true;
                steps.Add("world state count clamped");
            }

            changed |= CompactNonBlankStringArrayEntries(
                dto.depletedNodeIds,
                ref dto.depletedCount,
                WorldStateDTO.MaxNodes,
                "world state depleted ids repaired",
                steps);

            int clampedPickupChunks = math.clamp(
                dto.depletedPickupChunkCount,
                0,
                pickupChunkBound);
            if (clampedPickupChunks != dto.depletedPickupChunkCount)
            {
                dto.depletedPickupChunkCount = clampedPickupChunks;
                changed = true;
                steps.Add("world pickup chunk count clamped");
            }

            int clampedPickupWords = math.clamp(
                dto.depletedPickupWordCount,
                0,
                pickupWordBound);
            if (clampedPickupWords != dto.depletedPickupWordCount)
            {
                dto.depletedPickupWordCount = clampedPickupWords;
                changed = true;
                steps.Add("world pickup word count clamped");
            }

            return changed;
        }

        private static bool EnsureEcosystemState(ref EcosystemStateDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.worldGenerationVersionId < 0)
            {
                dto.worldGenerationVersionId = 0;
                changed = true;
                steps.Add("world generation version clamped");
            }

            int infectedBound = ClampArrayLength(dto.infectedChunkKeys, EcosystemStateDTO.MaxInfectedZones);
            infectedBound = math.min(
                infectedBound,
                ClampArrayLength(dto.infectedSeverities, EcosystemStateDTO.MaxInfectedZones));

            int existingCount = dto.infectedChunkKeys != null ? dto.infectedChunkKeys.Length : 0;
            long[] previousKeys = null;
            float[] previousSeverities = null;

            if (existingCount > 0)
            {
                previousKeys = dto.infectedChunkKeys;
                previousSeverities = dto.infectedSeverities;
            }

            if (dto.infectedChunkKeys == null ||
                dto.infectedChunkKeys.Length != EcosystemStateDTO.MaxInfectedZones ||
                dto.infectedSeverities == null ||
                dto.infectedSeverities.Length != EcosystemStateDTO.MaxInfectedZones)
            {
                dto.EnsureCapacity();
                if (previousKeys != null)
                {
                    int copyCount = math.min(previousKeys.Length, dto.infectedChunkKeys.Length);
                    Array.Copy(previousKeys, dto.infectedChunkKeys, copyCount);
                    if (previousSeverities != null)
                    {
                        int severityCopyCount = math.min(previousSeverities.Length, dto.infectedSeverities.Length);
                        Array.Copy(previousSeverities, dto.infectedSeverities, severityCopyCount);
                    }
                }

                changed = true;
                steps.Add("ecosystem state capacity repaired");
            }

            int clampedCount = math.clamp(dto.infectedZoneCount, 0, infectedBound);
            if (clampedCount != dto.infectedZoneCount)
            {
                dto.infectedZoneCount = clampedCount;
                changed = true;
                steps.Add("ecosystem infected-zone count clamped");
            }

            bool repairedInfectedSeverity = false;
            for (int i = 0; i < clampedCount; i++)
            {
                float currentSeverity = dto.infectedSeverities[i];
                float clampedSeverity = math.isfinite(currentSeverity) ? math.saturate(currentSeverity) : 0f;
                if (!math.isfinite(currentSeverity) || math.abs(clampedSeverity - currentSeverity) > 0.0001f)
                {
                    dto.infectedSeverities[i] = clampedSeverity;
                    repairedInfectedSeverity = true;
                    changed = true;
                }
            }

            if (repairedInfectedSeverity)
                steps.Add("ecosystem infected severity repaired");

            return changed;
        }

        private static bool EnsureProceduralTerrainIdentity(
            ref ProceduralTerrainIdentityDTO dto,
            List<string> steps)
        {
            ProceduralTerrainIdentityDTO safe = dto;
            safe.worldGenerationVersionId = math.max(0, safe.worldGenerationVersionId);
            safe.macroChunkSizeMeters = math.isfinite(safe.macroChunkSizeMeters)
                ? math.max(0f, safe.macroChunkSizeMeters)
                : 0f;
            safe.selectedWaterLevelY =
                math.isfinite(safe.selectedWaterLevelY) &&
                math.abs(safe.selectedWaterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                    ? safe.selectedWaterLevelY
                    : 0f;
            safe.waterCalibrationTravelMeters =
                math.isfinite(safe.waterCalibrationTravelMeters) &&
                safe.waterCalibrationTravelMeters > 0f
                    ? math.min(
                        safe.waterCalibrationTravelMeters,
                        WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
                    : 0f;

            if (safe.chunkMinX > safe.chunkMaxX)
            {
                int swap = safe.chunkMinX;
                safe.chunkMinX = safe.chunkMaxX;
                safe.chunkMaxX = swap;
            }

            if (safe.chunkMinZ > safe.chunkMaxZ)
            {
                int swap = safe.chunkMinZ;
                safe.chunkMinZ = safe.chunkMaxZ;
                safe.chunkMaxZ = swap;
            }

            if (safe.macroArtifactVersion == 0u)
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsMacroGeologyPresent;
            if (safe.waterCalibrationSourceHash == 0u)
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent;
            safe.heightCacheRevision = math.max(0, safe.heightCacheRevision);
            if (safe.terrainProviderFlags == 0u && safe.terrainEntityHash == 0u)
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainProviderIdentityPresent;
            if (safe.heightCacheRevision == 0 &&
                safe.terrainEntityHash == 0u &&
                (safe.terrainProviderFlags & TerrainArtifactIdentityDTO.FlagsHeightPayloadPresent) == 0u)
            {
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainHeightPayloadPresent;
            }

            if (safe.surfaceMaterialContractVersion == 0u)
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainMaterialContractsPresent;
            if (safe.mesoDetailContractVersion == 0u ||
                safe.detailEligibilityContractVersion == 0u ||
                safe.mesoParamsHash == 0u)
            {
                safe.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainMesoContractsPresent;
            }

            bool changed =
                safe.authoringSeed != dto.authoringSeed ||
                safe.runtimeSeed != dto.runtimeSeed ||
                safe.worldGenerationVersionId != dto.worldGenerationVersionId ||
                safe.macroArtifactVersion != dto.macroArtifactVersion ||
                !Approximately(safe.macroChunkSizeMeters, dto.macroChunkSizeMeters) ||
                safe.chunkMinX != dto.chunkMinX ||
                safe.chunkMinZ != dto.chunkMinZ ||
                safe.chunkMaxX != dto.chunkMaxX ||
                safe.chunkMaxZ != dto.chunkMaxZ ||
                safe.chunkArtifactRangeHash != dto.chunkArtifactRangeHash ||
                !Approximately(safe.selectedWaterLevelY, dto.selectedWaterLevelY) ||
                !Approximately(safe.waterCalibrationTravelMeters, dto.waterCalibrationTravelMeters) ||
                safe.waterCalibrationSourceHash != dto.waterCalibrationSourceHash ||
                safe.terrainProviderFlags != dto.terrainProviderFlags ||
                safe.heightCacheRevision != dto.heightCacheRevision ||
                safe.terrainEntityHash != dto.terrainEntityHash ||
                safe.surfaceMaterialContractVersion != dto.surfaceMaterialContractVersion ||
                safe.mesoDetailContractVersion != dto.mesoDetailContractVersion ||
                safe.detailEligibilityContractVersion != dto.detailEligibilityContractVersion ||
                safe.mesoParamsHash != dto.mesoParamsHash ||
                safe.flags != dto.flags;

            if (!changed)
                return false;

            dto = safe;
            steps.Add("procedural terrain identity repaired");
            return true;
        }

        private static bool EnsureExternalScavengerSites(
            ref ExternalScavengerSiteDTO[] sites,
            List<string> steps)
        {
            if (sites == null)
                return false;

            bool changed = sites.Length > SaveData.MaxExternalScavengerSites;
            int sourceCount = math.min(sites.Length, SaveData.MaxExternalScavengerSites);
            int writeIndex = 0;
            for (int i = 0; i < sourceCount; i++)
            {
                ExternalScavengerSiteDTO site = sites[i];
                if (!ExternalScavengerSiteDTO.TrySanitizeForPersistence(
                        in site,
                        out ExternalScavengerSiteDTO safeSite))
                {
                    changed = true;
                    continue;
                }

                if (!ExternalScavengerSiteDTO.PersistenceEquals(in site, in safeSite) || writeIndex != i)
                    changed = true;

                sites[writeIndex] = safeSite;
                writeIndex++;
            }

            if (!changed && writeIndex == sites.Length)
                return false;

            if (writeIndex == 0)
            {
                sites = Array.Empty<ExternalScavengerSiteDTO>();
            }
            else
            {
                Array.Resize(ref sites, writeIndex);
            }

            steps.Add("external scavenger sites repaired");
            return true;
        }

        private static bool EnsureVoxelDeltaPersistence(ref VoxelDeltaPersistenceDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.chunks == null)
            {
                dto.chunks = Array.Empty<VoxelDeltaChunkDTO>();
                dto.chunkCount = 0;
                dto.totalCellCount = 0;
                changed = true;
                steps.Add("voxel delta chunks created");
            }

            int chunkCapacity = math.min(dto.chunks != null ? dto.chunks.Length : 0, MaxVoxelDeltaChunks);
            int clampedChunkCount = math.clamp(dto.chunkCount, 0, chunkCapacity);
            if (clampedChunkCount != dto.chunkCount)
            {
                dto.chunkCount = clampedChunkCount;
                changed = true;
                steps.Add("voxel delta chunk count clamped");
            }

            int totalCellCount = 0;
            bool repairedVoxelDeltaStorageFlags = false;
            bool repairedVoxelDeltaCellFlags = false;
            for (int i = 0; i < dto.chunkCount; i++)
            {
                VoxelDeltaChunkDTO chunk = dto.chunks[i];
                bool hasUniformSdfRleStorage = (chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0;
                byte canonicalStorageFlags = hasUniformSdfRleStorage
                    ? VoxelDeltaChunkDTO.StorageUniformSdfRle
                    : VoxelDeltaChunkDTO.StorageDense;
                if (chunk.storageFlags != canonicalStorageFlags)
                {
                    chunk.storageFlags = canonicalStorageFlags;
                    changed = true;
                    repairedVoxelDeltaStorageFlags = true;
                }

                if (chunk.reservedStorage != 0)
                {
                    chunk.reservedStorage = 0;
                    changed = true;
                    repairedVoxelDeltaStorageFlags = true;
                }

                bool hasDenseStorage =
                    !hasUniformSdfRleStorage &&
                    chunk.dirtyMaskWords != null &&
                    chunk.dirtyMaskWords.Length == VoxelDeltaChunkDTO.DirtyMaskWordCount &&
                    chunk.sdfValueBits != null &&
                    chunk.sdfValueBits.Length == VoxelDeltaChunkDTO.CellCount &&
                    chunk.materialIds != null &&
                    chunk.materialIds.Length == VoxelDeltaChunkDTO.CellCount;

                if (chunk.dirtyMaskWords == null)
                {
                    chunk.dirtyMaskWords = Array.Empty<uint>();
                    changed = true;
                }

                if (chunk.sdfValueBits == null)
                {
                    chunk.sdfValueBits = Array.Empty<ushort>();
                    changed = true;
                }

                if (chunk.materialIds == null)
                {
                    chunk.materialIds = Array.Empty<byte>();
                    changed = true;
                }

                if (hasDenseStorage &&
                    (chunk.cellFlags == null || chunk.cellFlags.Length != VoxelDeltaChunkDTO.CellCount))
                {
                    chunk.cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
                    changed = true;
                }
                else if (chunk.cellFlags == null)
                {
                    chunk.cellFlags = Array.Empty<byte>();
                    changed = true;
                }

                if (hasDenseStorage && SanitizeVoxelDeltaCellFlags(chunk.cellFlags))
                {
                    changed = true;
                    repairedVoxelDeltaCellFlags = true;
                }

                if (hasUniformSdfRleStorage)
                {
                    if (chunk.dirtyMaskWords.Length != 0)
                    {
                        chunk.dirtyMaskWords = Array.Empty<uint>();
                        changed = true;
                    }

                    if (chunk.sdfValueBits.Length != 0)
                    {
                        chunk.sdfValueBits = Array.Empty<ushort>();
                        changed = true;
                    }

                    if (chunk.materialIds.Length != 0)
                    {
                        chunk.materialIds = Array.Empty<byte>();
                        changed = true;
                    }

                    if (chunk.cellFlags.Length != 0)
                    {
                        chunk.cellFlags = Array.Empty<byte>();
                        changed = true;
                    }
                }

                int cellCapacity = chunk.cells != null ? math.min(chunk.cells.Length, VoxelDeltaChunkDTO.CellCount) : 0;
                int legacyCellCount = hasUniformSdfRleStorage ? 0 : math.clamp(chunk.cellCount, 0, cellCapacity);
                int denseCellCount = hasDenseStorage ? CountDirtyMaskBits(chunk.dirtyMaskWords) : 0;
                int clampedCellCount = hasUniformSdfRleStorage
                    ? VoxelDeltaChunkDTO.CellCount
                    : hasDenseStorage
                        ? denseCellCount
                        : legacyCellCount;
                if (clampedCellCount != chunk.cellCount)
                {
                    chunk.cellCount = clampedCellCount;
                    changed = true;
                }

                if (chunk.cells == null)
                {
                    chunk.cells = Array.Empty<VoxelDeltaCellDTO>();
                    changed = true;
                }
                else if ((hasUniformSdfRleStorage || hasDenseStorage) && chunk.cells.Length != 0)
                {
                    chunk.cells = Array.Empty<VoxelDeltaCellDTO>();
                    changed = true;
                }
                else if (!hasUniformSdfRleStorage && !hasDenseStorage && SanitizeVoxelDeltaCells(chunk.cells, legacyCellCount))
                {
                    changed = true;
                    repairedVoxelDeltaCellFlags = true;
                }

                dto.chunks[i] = chunk;
                totalCellCount = AddVoxelDeltaCellCountClamped(totalCellCount, chunk.cellCount);
            }

            if (repairedVoxelDeltaStorageFlags)
                steps.Add("voxel delta storage flags repaired");

            if (repairedVoxelDeltaCellFlags)
                steps.Add("voxel delta cell flags repaired");

            if (dto.totalCellCount != totalCellCount)
            {
                dto.totalCellCount = totalCellCount;
                changed = true;
                steps.Add("voxel delta total count repaired");
            }

            changed |= EnsureVoxelDeltaCarvingOperations(ref dto, steps);
            return changed;
        }

        private static int AddVoxelDeltaCellCountClamped(int current, int add)
        {
            if (add <= 0)
                return math.max(0, current);

            return current > int.MaxValue - add ? int.MaxValue : current + add;
        }

        private static bool EnsureVoxelDeltaCarvingOperations(ref VoxelDeltaPersistenceDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.carvingOperations == null)
            {
                dto.carvingOperations = Array.Empty<VoxelCarvingOperationDTO>();
                changed = true;
                steps.Add("voxel carving operations created");
            }

            int operationCapacity = math.min(dto.carvingOperations.Length, MaxVoxelDeltaCarvingOperations);
            int clampedOperationCount = math.clamp(dto.carvingOperationCount, 0, operationCapacity);
            if (clampedOperationCount != dto.carvingOperationCount)
            {
                dto.carvingOperationCount = clampedOperationCount;
                changed = true;
                steps.Add("voxel carving operation count clamped");
            }

            bool repairedOperations = false;
            for (int i = 0; i < dto.carvingOperationCount; i++)
            {
                VoxelCarvingOperationDTO operation = dto.carvingOperations[i];
                bool repairedOperation = false;

                if (!math.all(math.isfinite(operation.localPosition)))
                {
                    operation.localPosition = new float3(
                        math.isfinite(operation.localPosition.x) ? operation.localPosition.x : 0f,
                        math.isfinite(operation.localPosition.y) ? operation.localPosition.y : 0f,
                        math.isfinite(operation.localPosition.z) ? operation.localPosition.z : 0f);
                    repairedOperation = true;
                }

                if (!math.isfinite(operation.radius) || operation.radius < 0f)
                {
                    operation.radius = 0f;
                    repairedOperation = true;
                }

                if (operation.operation != VoxelCarvingOperationKind.Subtract &&
                    operation.operation != VoxelCarvingOperationKind.Add)
                {
                    operation.operation = VoxelCarvingOperationKind.Subtract;
                    repairedOperation = true;
                }

                if (!repairedOperation)
                    continue;

                dto.carvingOperations[i] = operation;
                changed = true;
                repairedOperations = true;
            }

            if (repairedOperations)
                steps.Add("voxel carving operations repaired");

            return changed;
        }

        private static bool SanitizeVoxelDeltaCellFlags(byte[] cellFlags)
        {
            if (cellFlags == null)
                return false;

            bool changed = false;
            for (int i = 0; i < cellFlags.Length; i++)
            {
                byte safeFlags = SanitizeVoxelDeltaCellFlags(cellFlags[i]);
                if (safeFlags == cellFlags[i])
                    continue;

                cellFlags[i] = safeFlags;
                changed = true;
            }

            return changed;
        }

        private static bool SanitizeVoxelDeltaCells(VoxelDeltaCellDTO[] cells, int count)
        {
            if (cells == null || count <= 0)
                return false;

            bool changed = false;
            int safeCount = math.clamp(count, 0, cells.Length);
            for (int i = 0; i < safeCount; i++)
            {
                byte safeFlags = SanitizeVoxelDeltaCellFlags(cells[i].flags);
                if (safeFlags == cells[i].flags)
                    continue;

                cells[i].flags = safeFlags;
                changed = true;
            }

            return changed;
        }

        private static byte SanitizeVoxelDeltaCellFlags(byte cellFlags)
        {
            return (byte)(cellFlags & VoxelDeltaChunkDTO.SupportedCellFlags);
        }

        private static int CountDirtyMaskBits(uint[] dirtyMaskWords)
        {
            if (dirtyMaskWords == null)
                return 0;

            int total = 0;
            int wordCount = math.min(dirtyMaskWords.Length, VoxelDeltaChunkDTO.DirtyMaskWordCount);
            for (int i = 0; i < wordCount; i++)
            {
                uint value = dirtyMaskWords[i];
                while (value != 0u)
                {
                    value &= value - 1u;
                    total++;
                }
            }

            return total;
        }

        private static bool EnsureNarrative(ref SaveData data, List<string> steps)
        {
            bool changed = false;
            int narrativeBound = ClampArrayLength(data.narrativeDiscoveryIds, SaveData.MaxNarrativeDiscoveries);

            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length != SaveData.MaxNarrativeDiscoveries)
            {
                SaveData.EnsureExactArrayCapacity(ref data.narrativeDiscoveryIds, SaveData.MaxNarrativeDiscoveries);
                changed = true;
                steps.Add("narrative discovery capacity repaired");
            }

            int clampedCount = math.clamp(data.narrativeDiscoveryCount, 0, narrativeBound);
            if (clampedCount != data.narrativeDiscoveryCount)
            {
                data.narrativeDiscoveryCount = clampedCount;
                changed = true;
                steps.Add("narrative discovery count clamped");
            }
            changed |= CompactNonBlankStringArrayEntries(
                data.narrativeDiscoveryIds,
                ref data.narrativeDiscoveryCount,
                SaveData.MaxNarrativeDiscoveries,
                "narrative discovery ids repaired",
                steps);

            if (data.narrativeDepthTier < 0)
            {
                data.narrativeDepthTier = 0;
                changed = true;
                steps.Add("narrative depth tier repaired");
            }

            return changed;
        }

        private static bool EnsureExplorationMap(ref ExplorationMapDTO dto, List<string> steps)
        {
            bool changed = false;
            int exploredChunkBound = ClampArrayLength(dto.exploredChunkKeys, ExplorationMapDTO.MaxExploredChunks);
            int mortonWordBound = ClampArrayLength(dto.exploredMortonMaskWords, ExplorationMapDTO.MortonMaskWordCount);
            int mortonByteBound = ClampArrayLength(dto.exploredMortonMaskBytes, ExplorationMapDTO.MortonMaskByteCount);
            int sectorWordBound = ClampArrayLength(dto.discoveredSectorMaskWords, ExplorationMapDTO.CartographyMaskWordCount);
            int sectorByteBound = ClampArrayLength(dto.discoveredSectorMaskBytes, ExplorationMapDTO.CartographyMaskByteCount);

            if (dto.exploredChunkKeys == null || dto.exploredChunkKeys.Length != ExplorationMapDTO.MaxExploredChunks)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration map capacity repaired");
            }

            if (dto.exploredMortonMaskWords == null || dto.exploredMortonMaskWords.Length != ExplorationMapDTO.MortonMaskWordCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration morton bitmask capacity repaired");
            }

            if (dto.exploredMortonMaskBytes == null || dto.exploredMortonMaskBytes.Length != ExplorationMapDTO.MortonMaskByteCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration morton byte mask capacity repaired");
            }

            if (dto.discoveredSectorMaskWords == null || dto.discoveredSectorMaskWords.Length != ExplorationMapDTO.CartographyMaskWordCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("cartography sector bitmask capacity repaired");
            }

            if (dto.discoveredSectorMaskBytes == null || dto.discoveredSectorMaskBytes.Length != ExplorationMapDTO.CartographyMaskByteCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("cartography sector byte mask capacity repaired");
            }

            int clampedCount = math.clamp(dto.exploredChunkCount, 0, exploredChunkBound);
            if (clampedCount != dto.exploredChunkCount)
            {
                dto.exploredChunkCount = clampedCount;
                changed = true;
                steps.Add("exploration map count clamped");
            }

            int clampedWordCount = math.clamp(dto.exploredMortonWordCount, 0, mortonWordBound);
            if (clampedWordCount != dto.exploredMortonWordCount)
            {
                dto.exploredMortonWordCount = clampedWordCount;
                changed = true;
                steps.Add("exploration morton word count clamped");
            }

            int clampedByteCount = math.clamp(dto.exploredMortonByteCount, 0, mortonByteBound);
            clampedByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(clampedByteCount);
            if (clampedByteCount != dto.exploredMortonByteCount)
            {
                dto.exploredMortonByteCount = clampedByteCount;
                changed = true;
                steps.Add("exploration morton byte count aligned");
            }

            int clampedSectorWordCount = math.clamp(dto.discoveredSectorWordCount, 0, sectorWordBound);
            if (clampedSectorWordCount != dto.discoveredSectorWordCount)
            {
                dto.discoveredSectorWordCount = clampedSectorWordCount;
                changed = true;
                steps.Add("cartography sector word count clamped");
            }

            int clampedSectorByteCount = math.clamp(dto.discoveredSectorByteCount, 0, sectorByteBound);
            clampedSectorByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(clampedSectorByteCount);
            if (clampedSectorByteCount != dto.discoveredSectorByteCount)
            {
                dto.discoveredSectorByteCount = clampedSectorByteCount;
                changed = true;
                steps.Add("cartography sector byte count aligned");
            }

            if (dto.chunkSizeMeters != ExplorationMapDTO.DenseChunkSizeMeters ||
                dto.mortonMaskAxisBits != ExplorationMapDTO.MortonMaskAxisBits ||
                dto.mortonMaskOriginOffset != ExplorationMapDTO.MortonMaskOriginOffset ||
                dto.mortonBuildSalt != SaveBinaryStorage.ExplorationMortonBuildSalt32)
            {
                dto.chunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
                dto.mortonMaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
                dto.mortonMaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
                dto.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
                changed = true;
                steps.Add("exploration morton metadata repaired");
            }

            if (dto.cartographyCellSizeMeters != ExplorationMapDTO.CartographyCellSizeMeters ||
                dto.cartographyMaskAxisBits != ExplorationMapDTO.CartographyMaskAxisBits ||
                dto.cartographyMaskOriginOffset != ExplorationMapDTO.CartographyMaskOriginOffset)
            {
                dto.cartographyCellSizeMeters = ExplorationMapDTO.CartographyCellSizeMeters;
                dto.cartographyMaskAxisBits = ExplorationMapDTO.CartographyMaskAxisBits;
                dto.cartographyMaskOriginOffset = ExplorationMapDTO.CartographyMaskOriginOffset;
                changed = true;
                steps.Add("cartography sector metadata repaired");
            }

            return changed;
        }

        private static bool EnsurePdaLogbook(ref PDALogbookDTO dto, List<string> steps)
        {
            bool changed = false;
            int entryBound = ClampArrayLength(dto.entries, PDALogbookDTO.MaxEntries);
            int seenOriginBound = ClampArrayLength(dto.seenOriginHashes, PDALogbookDTO.MaxSeenOrigins);
            if (seenOriginBound == 0)
                seenOriginBound = ClampArrayLength(dto.seenOriginKeys, PDALogbookDTO.MaxSeenOrigins);

            if (dto.entries == null || dto.entries.Length != PDALogbookDTO.MaxEntries ||
                dto.seenOriginKeys == null || dto.seenOriginKeys.Length != PDALogbookDTO.MaxSeenOrigins ||
                dto.seenOriginHashes == null || dto.seenOriginHashes.Length != PDALogbookDTO.MaxSeenOrigins)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda logbook capacity repaired");
            }

            int clampedCount = math.clamp(dto.entryCount, 0, entryBound);
            if (clampedCount != dto.entryCount)
            {
                dto.entryCount = clampedCount;
                changed = true;
                steps.Add("pda logbook count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = math.max(1, clampedCount + 1);
                changed = true;
                steps.Add("pda logbook sequence repaired");
            }

            bool repairedEntries = false;
            for (int i = 0; i < dto.entryCount; i++)
            {
                PDALogbookEntryDTO entry = dto.entries[i];
                PDALogbookEntryDTO safeEntry = PDALogbookEntryDTO.SanitizeForPersistence(in entry);
                if (PDALogbookEntryDTO.PersistenceEquals(in entry, in safeEntry))
                    continue;

                dto.entries[i] = safeEntry;
                repairedEntries = true;
                changed = true;
            }

            if (repairedEntries)
                steps.Add("pda logbook entries repaired");

            int clampedSeenOriginCount = math.clamp(dto.seenOriginCount, 0, seenOriginBound);
            if (clampedSeenOriginCount != dto.seenOriginCount)
            {
                dto.seenOriginCount = clampedSeenOriginCount;
                changed = true;
                steps.Add("pda logbook seen-origin count clamped");
            }

            if (dto.SanitizeSeenOriginsForPersistence())
            {
                changed = true;
                steps.Add("pda logbook seen origins repaired");
            }

            return changed;
        }

        private static bool EnsurePdaMarkers(ref PDAMarkerRegistryDTO dto, List<string> steps)
        {
            bool changed = false;
            int markerBound = ClampArrayLength(dto.entries, PDAMarkerRegistryDTO.MaxEntries);

            if (dto.entries == null || dto.entries.Length != PDAMarkerRegistryDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda marker capacity repaired");
            }

            int clampedCount = math.clamp(dto.markerCount, 0, markerBound);
            if (clampedCount != dto.markerCount)
            {
                dto.markerCount = clampedCount;
                changed = true;
                steps.Add("pda marker count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = math.max(1, clampedCount + 1);
                changed = true;
                steps.Add("pda marker sequence repaired");
            }

            bool repairedMarkers = false;
            for (int i = 0; i < dto.markerCount; i++)
            {
                PDAMarkerEntryDTO marker = dto.entries[i];
                PDAMarkerEntryDTO safeMarker = PDAMarkerEntryDTO.SanitizeForPersistence(in marker);
                if (PDAMarkerEntryDTO.PersistenceEquals(in marker, in safeMarker))
                    continue;

                dto.entries[i] = safeMarker;
                repairedMarkers = true;
                changed = true;
            }

            if (repairedMarkers)
                steps.Add("pda marker entries repaired");

            return changed;
        }

        private static bool EnsurePdaAdvisories(ref PDAContextualAdvisoryDTO dto, List<string> steps)
        {
            int stepCountBefore = steps.Count;
            PDAContextualAdvisoryDTO safeDto = PDAContextualAdvisoryDTO.SanitizeForPersistence(in dto);
            bool changed = !PDAContextualAdvisoryDTO.PersistenceEquals(in dto, in safeDto);

            if (dto.oxygenDeathCount != safeDto.oxygenDeathCount)
                steps.Add("pda advisory oxygen-death count repaired");

            if (dto.inventoryFullAttemptCount != safeDto.inventoryFullAttemptCount)
                steps.Add("pda advisory inventory-full count repaired");

            if (dto.pressureDeathCount != safeDto.pressureDeathCount)
                steps.Add("pda advisory pressure-death count repaired");

            if (dto.baseEmergencyCount != safeDto.baseEmergencyCount)
                steps.Add("pda advisory base-emergency count repaired");

            if (dto.staleAirIncidentCount != safeDto.staleAirIncidentCount)
                steps.Add("pda advisory stale-air count repaired");

            if (dto.coldStressIncidentCount != safeDto.coldStressIncidentCount)
                steps.Add("pda advisory cold-stress count repaired");

            if (dto.heatStressIncidentCount != safeDto.heatStressIncidentCount)
                steps.Add("pda advisory heat-stress count repaired");

            if (!Approximately(dto.deepExposureSeconds, safeDto.deepExposureSeconds))
                steps.Add("pda advisory deep-exposure time repaired");

            if (!Approximately(dto.coldStressExposureSeconds, safeDto.coldStressExposureSeconds))
                steps.Add("pda advisory cold-stress exposure repaired");

            if (!Approximately(dto.heatStressExposureSeconds, safeDto.heatStressExposureSeconds))
                steps.Add("pda advisory heat-stress exposure repaired");

            dto = safeDto;
            if (changed && steps.Count == stepCountBefore)
                steps.Add("pda advisory values repaired");

            return changed;
        }

        private static bool EnsureProceduralLore(ref ProceduralLoreStateDTO dto, List<string> steps)
        {
            bool changed = false;
            int activePlacementBound = ClampArrayLength(dto.activePlacements, ProceduralLoreStateDTO.MaxActivePlacements);

            if (dto.activePlacements == null || dto.activePlacements.Length != ProceduralLoreStateDTO.MaxActivePlacements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural lore capacity repaired");
            }

            int clampedCount = math.clamp(dto.activeCount, 0, activePlacementBound);
            if (clampedCount != dto.activeCount)
            {
                dto.activeCount = clampedCount;
                changed = true;
                steps.Add("procedural lore count clamped");
            }

            if (dto.nextSourceIndex < 0)
            {
                dto.nextSourceIndex = 0;
                changed = true;
                steps.Add("procedural lore source index repaired");
            }

            bool repairedPlacements = false;
            for (int i = 0; i < dto.activeCount; i++)
            {
                ProceduralLorePlacementDTO placement = dto.activePlacements[i];
                ProceduralLorePlacementDTO safePlacement = ProceduralLorePlacementDTO.SanitizeForPersistence(in placement);
                if (ProceduralLorePlacementDTO.PersistenceEquals(in placement, in safePlacement))
                    continue;

                dto.activePlacements[i] = safePlacement;
                repairedPlacements = true;
                changed = true;
            }

            if (repairedPlacements)
                steps.Add("procedural lore placements repaired");

            return changed;
        }

        private static bool EnsureAchievements(ref AchievementRegistryDTO dto, List<string> steps)
        {
            bool changed = false;
            int unlockedBound = ClampArrayLength(dto.unlockedIds, AchievementRegistryDTO.MaxUnlockedAchievements);

            if (dto.unlockedIds == null || dto.unlockedIds.Length != AchievementRegistryDTO.MaxUnlockedAchievements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("achievement registry capacity repaired");
            }

            int clampedUnlockedCount = math.clamp(dto.unlockedCount, 0, unlockedBound);
            if (clampedUnlockedCount != dto.unlockedCount)
            {
                dto.unlockedCount = clampedUnlockedCount;
                changed = true;
                steps.Add("achievement registry count clamped");
            }

            float safeSwamDistanceMeters = SanitizeNonNegativeFinite(dto.swamDistanceMeters);
            if (!Approximately(dto.swamDistanceMeters, safeSwamDistanceMeters))
            {
                dto.swamDistanceMeters = safeSwamDistanceMeters;
                changed = true;
                steps.Add("achievement swim distance repaired");
            }

            if (dto.craftedItemCount < 0)
            {
                dto.craftedItemCount = 0;
                changed = true;
                steps.Add("achievement crafted count repaired");
            }

            if (dto.discoveredBiomeCount < 0)
            {
                dto.discoveredBiomeCount = 0;
                changed = true;
                steps.Add("achievement biome count repaired");
            }

            changed |= CompactNonBlankStringArrayEntries(
                dto.unlockedIds,
                ref dto.unlockedCount,
                AchievementRegistryDTO.MaxUnlockedAchievements,
                "achievement unlocked ids repaired",
                steps);

            return changed;
        }

        private static bool EnsureRunModifiers(ref RunModifiersDTO dto, List<string> steps)
        {
            bool changed = false;

            if (!dto.isDailySeed && dto.dailySeedId != string.Empty)
            {
                dto.dailySeedId = string.Empty;
                changed = true;
                steps.Add("run modifiers daily-seed id cleared");
            }

            if (dto.isDailySeed)
            {
                string dailySeedId = SaveData.SanitizePersistenceString(dto.dailySeedId);
                if (!string.Equals(dto.dailySeedId, dailySeedId, StringComparison.Ordinal))
                {
                    dto.dailySeedId = dailySeedId;
                    changed = true;
                    steps.Add("run modifiers daily-seed id repaired");
                }
            }

            if (!dto.isPermadeath && dto.runMarkedDead)
            {
                dto.runMarkedDead = false;
                changed = true;
                steps.Add("run modifiers dead-run flag repaired");
            }

            return changed;
        }

        private static bool EnsureMetaCampaign(ref MetaCampaignDTO dto, List<string> steps)
        {
            bool changed = false;
            int previousHashCapacity = dto.variableHashes != null ? dto.variableHashes.Length : 0;
            int previousValueCapacity = dto.variableValues != null ? dto.variableValues.Length : 0;
            int variableBound = math.min(
                MetaCampaignDTO.MaxGlobalVariables,
                math.min(previousHashCapacity, previousValueCapacity));

            dto.EnsureCapacity();
            if (dto.variableHashes.Length != previousHashCapacity ||
                dto.variableValues.Length != previousValueCapacity)
            {
                changed = true;
                steps.Add("meta campaign capacity repaired");
            }

            int clampedCount = math.clamp(
                dto.variableCount,
                0,
                variableBound);
            if (clampedCount != dto.variableCount)
            {
                dto.variableCount = clampedCount;
                changed = true;
                steps.Add("meta campaign variable count clamped");
            }

            int clampedToxicity = math.clamp(dto.toxicityPermille, 0, 1000);
            if (clampedToxicity != dto.toxicityPermille)
            {
                dto.toxicityPermille = clampedToxicity;
                changed = true;
                steps.Add("meta campaign toxicity clamped");
            }

            return changed;
        }

        private static bool EnsureResourceScarcity(ref ResourceScarcityDTO dto, List<string> steps)
        {
            bool changed = false;
            int previousHashCapacity = dto.itemHashIds != null ? dto.itemHashIds.Length : 0;
            int previousItemCapacity = dto.itemIds != null ? dto.itemIds.Length : 0;
            int previousCountCapacity = dto.collectedCounts != null ? dto.collectedCounts.Length : 0;
            int previousIdentityCapacity = math.max(previousHashCapacity, previousItemCapacity);
            int entryBound = math.min(
                ResourceScarcityDTO.MaxTrackedResources,
                math.min(previousIdentityCapacity, previousCountCapacity));

            dto.EnsureCapacity();
            if (dto.itemHashIds.Length != previousHashCapacity ||
                dto.itemIds.Length != previousItemCapacity ||
                dto.collectedCounts.Length != previousCountCapacity)
            {
                changed = true;
                steps.Add("resource scarcity capacity repaired");
            }

            int clampedEntryCount = math.clamp(
                dto.entryCount,
                0,
                entryBound);

            if (clampedEntryCount != dto.entryCount)
            {
                dto.entryCount = clampedEntryCount;
                changed = true;
                steps.Add("resource scarcity entry count clamped");
            }

            bool repairedHashes = false;
            bool repairedItemIds = false;
            bool repairedCounts = false;
            bool repairedTail = false;
            for (int i = 0; i < dto.entryCount; i++)
            {
                string canonicalItemId = SanitizeResourceScarcityItemId(dto.itemHashIds[i], dto.itemIds[i]);
                if (!string.Equals(dto.itemIds[i], canonicalItemId, StringComparison.Ordinal))
                {
                    dto.itemIds[i] = canonicalItemId;
                    changed = true;
                    repairedItemIds = true;
                }

                if (dto.itemHashIds[i] == 0 && canonicalItemId.Length != 0)
                {
                    dto.itemHashIds[i] = LocHash.Compute(canonicalItemId);
                    changed = true;
                    repairedHashes = true;
                }

                if (dto.collectedCounts[i] < 0)
                {
                    dto.collectedCounts[i] = 0;
                    changed = true;
                    repairedCounts = true;
                }
            }

            bool repairedEntries = false;
            int compactCount = 0;
            for (int readIndex = 0; readIndex < dto.entryCount; readIndex++)
            {
                int itemHashId = dto.itemHashIds[readIndex];
                int collectedCount = dto.collectedCounts[readIndex];
                string itemId = dto.itemIds[readIndex] ?? string.Empty;
                if (itemHashId == 0)
                {
                    changed = true;
                    repairedEntries = true;
                    continue;
                }

                int duplicateIndex = -1;
                for (int i = 0; i < compactCount; i++)
                {
                    if (dto.itemHashIds[i] == itemHashId)
                    {
                        duplicateIndex = i;
                        break;
                    }
                }

                if (duplicateIndex >= 0)
                {
                    dto.collectedCounts[duplicateIndex] = SaturatingResourceScarcityCount(
                        dto.collectedCounts[duplicateIndex],
                        collectedCount);
                    if (dto.itemIds[duplicateIndex].Length == 0 && itemId.Length != 0)
                        dto.itemIds[duplicateIndex] = itemId;

                    changed = true;
                    repairedEntries = true;
                    continue;
                }

                if (compactCount != readIndex)
                {
                    dto.itemHashIds[compactCount] = itemHashId;
                    dto.itemIds[compactCount] = itemId;
                    dto.collectedCounts[compactCount] = collectedCount;
                    changed = true;
                    repairedEntries = true;
                }

                compactCount++;
            }

            if (compactCount != dto.entryCount)
            {
                dto.entryCount = compactCount;
                changed = true;
                repairedEntries = true;
            }

            for (int i = dto.entryCount; i < ResourceScarcityDTO.MaxTrackedResources; i++)
            {
                if (dto.itemHashIds[i] != 0)
                {
                    dto.itemHashIds[i] = 0;
                    changed = true;
                    repairedTail = true;
                }

                string tailItemId = dto.itemIds[i];
                if (tailItemId != null && tailItemId.Length != 0)
                {
                    dto.itemIds[i] = string.Empty;
                    changed = true;
                    repairedTail = true;
                }

                if (dto.collectedCounts[i] != 0)
                {
                    dto.collectedCounts[i] = 0;
                    changed = true;
                    repairedTail = true;
                }
            }

            if (repairedHashes)
                steps.Add("resource scarcity hash repaired");
            if (repairedItemIds)
                steps.Add("resource scarcity item ids repaired");
            if (repairedCounts)
                steps.Add("resource scarcity collected counts repaired");
            if (repairedEntries)
                steps.Add("resource scarcity entries compacted");
            if (repairedTail)
                steps.Add("resource scarcity tail repaired");

            return changed;
        }

        private static int SaturatingResourceScarcityCount(int left, int right)
        {
            long total = (long)math.max(0, left) + math.max(0, right);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private static string SanitizeResourceScarcityItemId(int itemHashId, string itemId)
        {
            itemId = SaveData.SanitizePersistenceString(itemId);
            if (itemId.Length == 0 || itemHashId == 0)
                return itemId;

            return LocHash.Compute(itemId) == itemHashId ? itemId : string.Empty;
        }

        private static bool EnsureAtlas6DirectiveState(SaveData data, List<string> steps)
        {
            if (data == null)
                return false;

            bool changed = false;
            int safePlayerStatus = IsKnownAtlas6PlayerStatus(data.atlas6PlayerStatus)
                ? data.atlas6PlayerStatus
                : 0;
            int safeBarterCount = math.max(0, data.atlas6BarterCount);

            if (data.atlas6PlayerStatus != safePlayerStatus ||
                data.atlas6BarterCount != safeBarterCount)
            {
                data.atlas6PlayerStatus = safePlayerStatus;
                data.atlas6BarterCount = safeBarterCount;
                changed = true;
                steps.Add("atlas6 directive state repaired");
            }

            return changed;
        }

        private static bool EnsureAtlas6Liability(SaveData data, int sourceVersion, List<string> steps)
        {
            if (data == null)
                return false;

            bool changed = false;
            int previousWorkerTagCapacity = data.atlas6LiabilityRecoveredWorkerTagHashes != null
                ? data.atlas6LiabilityRecoveredWorkerTagHashes.Length
                : 0;
            SaveData.EnsureExactArrayCapacity(
                ref data.atlas6LiabilityRecoveredWorkerTagHashes,
                SaveData.MaxAtlas6LiabilityWorkerTags);
            if (data.atlas6LiabilityRecoveredWorkerTagHashes.Length != previousWorkerTagCapacity)
            {
                changed = true;
                steps.Add("atlas6 liability worker-tag capacity repaired");
            }

            if (sourceVersion < SaveData.Atlas6LiabilityPersistenceVersion)
            {
                bool hadUnpersistedState =
                    !Approximately(data.atlas6LiabilitySectorXenonOmegaYield, 0f) ||
                    data.atlas6LiabilityHasDisasterEvidence ||
                    data.atlas6LiabilityRecoveredWorkerTagCount != 0 ||
                    !Approximately(data.atlas6LiabilityCorporateHostilityIndex, 0f) ||
                    !Approximately(data.atlas6LiabilityCorporateCreditBalance, 5000f) ||
                    data.atlas6LiabilityExtractionCarrierState != 0 ||
                    !Approximately(data.atlas6LiabilityBiomatterExposureLevel, 0f) ||
                    data.atlas6LiabilityHaldaneLockoutActive ||
                    !Approximately(data.atlas6LiabilityPressureSealIntegrity, 1f) ||
                    data.atlas6LiabilityBulkheadLocked;

                for (int i = 0; i < data.atlas6LiabilityRecoveredWorkerTagHashes.Length; i++)
                {
                    if (data.atlas6LiabilityRecoveredWorkerTagHashes[i] != 0u)
                    {
                        hadUnpersistedState = true;
                        break;
                    }
                }

                data.atlas6LiabilitySectorXenonOmegaYield = 0f;
                data.atlas6LiabilityHasDisasterEvidence = false;
                data.atlas6LiabilityRecoveredWorkerTagCount = 0;
                Array.Clear(
                    data.atlas6LiabilityRecoveredWorkerTagHashes,
                    0,
                    data.atlas6LiabilityRecoveredWorkerTagHashes.Length);
                data.atlas6LiabilityCorporateHostilityIndex = 0f;
                data.atlas6LiabilityCorporateCreditBalance = 5000f;
                data.atlas6LiabilityExtractionCarrierState = 0;
                data.atlas6LiabilityBiomatterExposureLevel = 0f;
                data.atlas6LiabilityHaldaneLockoutActive = false;
                data.atlas6LiabilityPressureSealIntegrity = 1f;
                data.atlas6LiabilityBulkheadLocked = false;

                if (hadUnpersistedState)
                {
                    changed = true;
                    steps.Add("atlas6 liability state defaulted");
                }

                return changed;
            }

            float safeYield = ClampFinite(
                data.atlas6LiabilitySectorXenonOmegaYield,
                0f,
                SaveData.Atlas6LiabilityMaxTrackedSectorYield);
            float safeHostility = SanitizeNonNegativeFinite(data.atlas6LiabilityCorporateHostilityIndex);
            float safeCredit = SanitizeNonNegativeFinite(data.atlas6LiabilityCorporateCreditBalance);
            int safeCarrierState = IsKnownAtlas6CarrierState(data.atlas6LiabilityExtractionCarrierState)
                ? data.atlas6LiabilityExtractionCarrierState
                : 0;
            float safeBiomatterExposure = ClampFinite(
                data.atlas6LiabilityBiomatterExposureLevel,
                0f,
                SaveData.Atlas6LiabilityMaxBiomatterExposure);
            float safeSealIntegrity = ClampFinite(data.atlas6LiabilityPressureSealIntegrity, 1f, 0f, 1f);
            int safeWorkerTagCount = math.clamp(
                data.atlas6LiabilityRecoveredWorkerTagCount,
                0,
                data.atlas6LiabilityRecoveredWorkerTagHashes.Length);

            if (!Approximately(data.atlas6LiabilitySectorXenonOmegaYield, safeYield) ||
                !Approximately(data.atlas6LiabilityCorporateHostilityIndex, safeHostility) ||
                !Approximately(data.atlas6LiabilityCorporateCreditBalance, safeCredit) ||
                data.atlas6LiabilityExtractionCarrierState != safeCarrierState ||
                !Approximately(data.atlas6LiabilityBiomatterExposureLevel, safeBiomatterExposure) ||
                !Approximately(data.atlas6LiabilityPressureSealIntegrity, safeSealIntegrity) ||
                data.atlas6LiabilityRecoveredWorkerTagCount != safeWorkerTagCount)
            {
                data.atlas6LiabilitySectorXenonOmegaYield = safeYield;
                data.atlas6LiabilityCorporateHostilityIndex = safeHostility;
                data.atlas6LiabilityCorporateCreditBalance = safeCredit;
                data.atlas6LiabilityExtractionCarrierState = safeCarrierState;
                data.atlas6LiabilityBiomatterExposureLevel = safeBiomatterExposure;
                data.atlas6LiabilityPressureSealIntegrity = safeSealIntegrity;
                data.atlas6LiabilityRecoveredWorkerTagCount = safeWorkerTagCount;
                changed = true;
                steps.Add("atlas6 liability values repaired");
            }

            bool clearedTail = false;
            for (int i = data.atlas6LiabilityRecoveredWorkerTagCount;
                 i < data.atlas6LiabilityRecoveredWorkerTagHashes.Length;
                 i++)
            {
                if (data.atlas6LiabilityRecoveredWorkerTagHashes[i] == 0u)
                    continue;

                data.atlas6LiabilityRecoveredWorkerTagHashes[i] = 0u;
                clearedTail = true;
            }

            if (clearedTail)
            {
                changed = true;
                steps.Add("atlas6 liability worker-tag tail cleared");
            }

            return changed;
        }

        private static bool IsKnownAtlas6CarrierState(int carrierState)
        {
            return carrierState >= 0 && carrierState <= 4;
        }

        private static bool IsKnownAtlas6PlayerStatus(int playerStatus)
        {
            return playerStatus >= 0 && playerStatus <= 5;
        }

        private static bool EnsureEnvironmentalStrain(ref EnvironmentalStrainDTO dto, List<string> steps)
        {
            EnvironmentalStrainDTO safeDto = EnvironmentalStrainDTO.SanitizeForPersistence(in dto);
            bool changed = !EnvironmentalStrainDTO.PersistenceEquals(in dto, in safeDto);
            dto = safeDto;

            if (changed)
                steps.Add("environmental strain values clamped");

            return changed;
        }

        private static bool EnsurePerformanceSettings(ref SaveData data, int sourceVersion, List<string> steps)
        {
            bool changed = false;

            if (data.LODQualityPreset < 0 || data.LODQualityPreset > 2)
            {
                data.LODQualityPreset = 1;
                changed = true;
                steps.Add("lod quality preset repaired");
            }

            if (sourceVersion < PreV73RepairVersion && !data.DynamicResolutionEnabled)
            {
                data.DynamicResolutionEnabled = true;
                changed = true;
                steps.Add("dynamic resolution default repaired");
            }

            return changed;
        }

        private static bool EnsureCelestialLightPhase(SaveData data, int sourceVersion, List<string> steps)
        {
            bool hasPhase = sourceVersion >= SaveData.CelestialLightPhasePersistenceVersion &&
                            data.celestialLightPhaseSerialized &&
                            math.isfinite(data.celestialLightTimeOfDay01);
            float safeTimeOfDay01 = hasPhase
                ? math.saturate(data.celestialLightTimeOfDay01)
                : SaveData.CelestialLightTimeOfDayDefault;

            bool changed = data.celestialLightPhaseSerialized != hasPhase ||
                           !Approximately(data.celestialLightTimeOfDay01, safeTimeOfDay01);

            data.celestialLightPhaseSerialized = hasPhase;
            data.celestialLightTimeOfDay01 = safeTimeOfDay01;

            if (changed)
                steps.Add("celestial light phase state repaired");

            return changed;
        }

        private static bool EnsurePlayerExpression(ref SaveData data, List<string> steps)
        {
            string profileId = SaveData.SanitizePersistenceString(data.playerExpressionProfileId);
            if (data.playerExpressionProfileId == null)
            {
                data.playerExpressionProfileId = profileId;
                steps.Add("player expression profile initialized");
                return true;
            }

            if (string.Equals(data.playerExpressionProfileId, profileId, StringComparison.Ordinal))
                return false;

            data.playerExpressionProfileId = profileId;
            steps.Add("player expression profile repaired");
            return true;
        }

        private static bool EnsureConstruction(ref ConstructionDTO dto, int sourceVersion, List<string> steps)
        {
            bool changed = false;
            int moduleBound = ClampArrayLength(dto.modules, ConstructionDTO.MaxModules);
            int graphNodeBound = ClampArrayLength(dto.graphNodes, ConstructionDTO.MaxModules);
            int graphEdgeBound = ClampArrayLength(dto.graphEdges, ConstructionDTO.MaxGraphEdges);
            int moduleBlitBound = ClampArrayLength(dto.moduleBlitRecords, ConstructionDTO.MaxModules);
            int habitatFloodBound = ClampArrayLength(dto.habitatFloodStates, ConstructionDTO.MaxModules);

            if (dto.modules == null || dto.modules.Length != ConstructionDTO.MaxModules ||
                dto.graphNodes == null || dto.graphNodes.Length != ConstructionDTO.MaxModules ||
                dto.graphEdges == null || dto.graphEdges.Length != ConstructionDTO.MaxGraphEdges ||
                dto.moduleBlitRecords == null || dto.moduleBlitRecords.Length != ConstructionDTO.MaxModules ||
                dto.habitatFloodStates == null || dto.habitatFloodStates.Length != ConstructionDTO.MaxModules)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("construction capacity repaired");
            }

            int clamped = math.clamp(dto.moduleCount, 0, moduleBound);
            if (clamped != dto.moduleCount)
            {
                dto.moduleCount = clamped;
                changed = true;
                steps.Add("construction count clamped");
            }

            int clampedGraphNodeCount = math.clamp(dto.graphNodeCount, 0, Math.Min(graphNodeBound, dto.moduleCount));
            if (clampedGraphNodeCount != dto.graphNodeCount)
            {
                dto.graphNodeCount = clampedGraphNodeCount;
                changed = true;
                steps.Add("construction graph node count clamped");
            }

            int clampedGraphEdgeCount = math.clamp(dto.graphEdgeCount, 0, graphEdgeBound);
            if (clampedGraphEdgeCount != dto.graphEdgeCount)
            {
                dto.graphEdgeCount = clampedGraphEdgeCount;
                changed = true;
                steps.Add("construction graph edge count clamped");
            }

            int clampedBlitCount = math.clamp(dto.moduleBlitCount, 0, Math.Min(moduleBlitBound, dto.moduleCount));
            if (clampedBlitCount != dto.moduleBlitCount)
            {
                dto.moduleBlitCount = clampedBlitCount;
                changed = true;
                steps.Add("construction blit count clamped");
            }

            int clampedFloodCount = math.clamp(
                dto.habitatFloodStateCount,
                0,
                Math.Min(habitatFloodBound, dto.moduleCount));
            if (clampedFloodCount != dto.habitatFloodStateCount)
            {
                dto.habitatFloodStateCount = clampedFloodCount;
                changed = true;
                steps.Add("construction flood count clamped");
            }

            bool repairedFloodStates = false;
            for (int i = 0; i < dto.habitatFloodStateCount; i++)
            {
                HabitatFloodStateDTO floodState = dto.habitatFloodStates[i];
                HabitatFloodStateDTO safeFloodState = HabitatFloodStateDTO.Sanitize(in floodState);
                if (HabitatFloodStateDTO.PersistenceEquals(in floodState, in safeFloodState))
                    continue;

                dto.habitatFloodStates[i] = safeFloodState;
                repairedFloodStates = true;
                changed = true;
            }

            if (repairedFloodStates)
                steps.Add("construction flood states repaired");

            bool repairedModules = false;
            bool repairedCultivationSeedHashes = false;
            for (int i = 0; i < dto.moduleCount; i++)
            {
                ModuleDTO module = dto.modules[i];
                bool moduleChanged = ModuleDTO.SanitizeForPersistenceInPlace(ref module);
                bool capacityChanged = !HasExactModuleNestedArrayCapacity(in module);
                if (capacityChanged)
                {
                    module.EnsureNestedArrayCapacity();
                    moduleChanged |= ModuleDTO.SanitizeForPersistenceInPlace(ref module);
                }

                bool hashBackfilled = BackfillCultivationSeedHashIds(ref module);
                if (hashBackfilled)
                    repairedCultivationSeedHashes = true;

                moduleChanged |= hashBackfilled;
                if (!moduleChanged && !capacityChanged)
                    continue;

                dto.modules[i] = module;
                repairedModules = true;
                changed = true;
            }

            if (repairedModules)
                steps.Add("construction module state repaired");
            if (repairedCultivationSeedHashes)
                steps.Add("construction cultivation seed hashes repaired");

            bool repairedGraphNodes = false;
            for (int i = 0; i < dto.graphNodeCount; i++)
            {
                ModuleGraphNodeDTO graphNode = dto.graphNodes[i];
                ModuleGraphNodeDTO safeGraphNode = ModuleGraphNodeDTO.SanitizeForPersistence(in graphNode);
                if (ModuleGraphNodeDTO.PersistenceEquals(in graphNode, in safeGraphNode))
                    continue;

                dto.graphNodes[i] = safeGraphNode;
                repairedGraphNodes = true;
                changed = true;
            }

            if (repairedGraphNodes)
                steps.Add("construction graph nodes repaired");

            bool repairedGraphEdges = false;
            int graphEdgeWriteIndex = 0;
            for (int i = 0; i < dto.graphEdgeCount; i++)
            {
                ModuleGraphEdgeDTO graphEdge = dto.graphEdges[i];
                if (!ModuleGraphEdgeDTO.TrySanitizeForPersistence(
                        in graphEdge,
                        dto.graphNodeCount,
                        out ModuleGraphEdgeDTO safeGraphEdge))
                {
                    repairedGraphEdges = true;
                    changed = true;
                    continue;
                }

                if (ModuleGraphEdgeDTO.ContainsPersistenceEdge(dto.graphEdges, graphEdgeWriteIndex, in safeGraphEdge))
                {
                    repairedGraphEdges = true;
                    changed = true;
                    continue;
                }

                if (graphEdgeWriteIndex != i ||
                    !ModuleGraphEdgeDTO.PersistenceEquals(in graphEdge, in safeGraphEdge))
                {
                    repairedGraphEdges = true;
                    changed = true;
                }

                dto.graphEdges[graphEdgeWriteIndex] = safeGraphEdge;
                graphEdgeWriteIndex++;
            }

            if (graphEdgeWriteIndex != dto.graphEdgeCount)
            {
                dto.graphEdgeCount = graphEdgeWriteIndex;
                repairedGraphEdges = true;
                changed = true;
            }

            if (repairedGraphEdges)
                steps.Add("construction graph edges repaired");

            bool repairedBlitRecords = false;
            for (int i = 0; i < dto.moduleBlitCount; i++)
            {
                ModuleBlitDTO blitRecord = dto.moduleBlitRecords[i];
                ModuleBlitDTO safeBlitRecord = ModuleBlitDTO.SanitizeForPersistence(in blitRecord);
                if (ModuleBlitDTO.PersistenceEquals(in blitRecord, in safeBlitRecord))
                    continue;

                dto.moduleBlitRecords[i] = safeBlitRecord;
                repairedBlitRecords = true;
                changed = true;
            }

            if (repairedBlitRecords)
                steps.Add("construction blit records repaired");

            if (sourceVersion < 2 && dto.modules != null)
            {
                bool repairedLegacyIntegrity = false;
                for (int i = 0; i < dto.moduleCount; i++)
                {
                    ModuleDTO module = dto.modules[i];
                    if (!string.IsNullOrWhiteSpace(module.prefabId) && module.integrity <= 0f)
                    {
                        module.integrity = LegacyModuleIntegrityDefault;
                        dto.modules[i] = module;
                        repairedLegacyIntegrity = true;
                        changed = true;
                    }
                }

                if (repairedLegacyIntegrity)
                    steps.Add("legacy construction integrity restored");
            }

            if (sourceVersion < 63 && dto.modules != null)
            {
                bool repairedHealth = false;
                for (int i = 0; i < dto.moduleCount; i++)
                {
                    ModuleDTO module = dto.modules[i];
                    byte health = PackLegacyConstructionHealthByte(module.integrity);
                    if (module.health != health)
                    {
                        module.health = health;
                        dto.modules[i] = module;
                        repairedHealth = true;
                        changed = true;
                    }
                }

                if (repairedHealth)
                    steps.Add("construction health mirror repaired");
            }

            changed |= EnsureHabitatFloodStateMirrors(ref dto, steps);
            return changed;
        }

        private static bool HasExactModuleNestedArrayCapacity(in ModuleDTO module)
        {
            return module.sorterBufferedItemIds != null &&
                   module.sorterBufferedItemIds.Length == ModuleDTO.MaxSorterBufferedSlots &&
                   module.sorterBufferedQuantities != null &&
                   module.sorterBufferedQuantities.Length == ModuleDTO.MaxSorterBufferedSlots &&
                   module.recyclerBufferedItemIds != null &&
                   module.recyclerBufferedItemIds.Length == ModuleDTO.MaxRecyclerBufferedSlots &&
                   module.recyclerBufferedQuantities != null &&
                   module.recyclerBufferedQuantities.Length == ModuleDTO.MaxRecyclerBufferedSlots &&
                   module.recyclerPendingYieldItemIds != null &&
                   module.recyclerPendingYieldItemIds.Length == ModuleDTO.MaxRecyclerPendingYieldSlots &&
                   module.recyclerPendingYieldQuantities != null &&
                   module.recyclerPendingYieldQuantities.Length == ModuleDTO.MaxRecyclerPendingYieldSlots &&
                   module.storageCrateItemIds != null &&
                   module.storageCrateItemIds.Length == ModuleDTO.MaxStorageCrateSlots &&
                   module.storageCrateQuantities != null &&
                   module.storageCrateQuantities.Length == ModuleDTO.MaxStorageCrateSlots &&
                   module.cultivationSeedItemIds != null &&
                   module.cultivationSeedItemIds.Length == ModuleDTO.MaxCultivationSlots &&
                   module.cultivationSeedItemHashIds != null &&
                   module.cultivationSeedItemHashIds.Length == ModuleDTO.MaxCultivationSlots &&
                   module.cultivationGeneticsMasks != null &&
                   module.cultivationGeneticsMasks.Length == ModuleDTO.MaxCultivationSlots &&
                   module.cultivationGrowth01 != null &&
                   module.cultivationGrowth01.Length == ModuleDTO.MaxCultivationSlots &&
                   module.cultivationQuality01 != null &&
                   module.cultivationQuality01.Length == ModuleDTO.MaxCultivationSlots;
        }

        private static bool BackfillCultivationSeedHashIds(ref ModuleDTO module)
        {
            if (module.cultivationSlotCount <= 0 ||
                module.cultivationSeedItemIds == null ||
                module.cultivationSeedItemHashIds == null)
            {
                return false;
            }

            int count = Math.Min(
                Math.Clamp(module.cultivationSlotCount, 0, ModuleDTO.MaxCultivationSlots),
                Math.Min(module.cultivationSeedItemIds.Length, module.cultivationSeedItemHashIds.Length));
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                if (module.cultivationSeedItemHashIds[i] != 0)
                    continue;

                string seedItemId = SaveData.SanitizePersistenceString(module.cultivationSeedItemIds[i]);
                if (seedItemId.Length == 0)
                    continue;

                int seedHashId = LocHash.Compute(seedItemId);
                if (seedHashId == 0)
                    continue;

                module.cultivationSeedItemHashIds[i] = seedHashId;
                changed = true;
            }

            return changed;
        }

        private static bool EnsureHabitatFloodStateMirrors(ref ConstructionDTO dto, List<string> steps)
        {
            if (dto.moduleCount <= 0 || dto.modules == null || dto.habitatFloodStates == null)
                return false;

            int moduleCount = math.clamp(
                dto.moduleCount,
                0,
                Math.Min(ConstructionDTO.MaxModules, dto.modules.Length));
            if (moduleCount <= 0)
                return false;

            bool changed = false;
            if (dto.habitatFloodStateCount != moduleCount)
            {
                dto.habitatFloodStateCount = moduleCount;
                changed = true;
            }

            for (int i = 0; i < moduleCount; i++)
            {
                int moduleHashId = dto.ResolveHabitatFloodStateModuleHashId(i);
                HabitatFloodStateDTO expected = HabitatFloodStateDTO.FromModule(in dto.modules[i], moduleHashId);
                HabitatFloodStateDTO current = dto.habitatFloodStates[i];
                if (HabitatFloodStateDTO.PersistenceEquals(in current, in expected))
                    continue;

                dto.habitatFloodStates[i] = expected;
                changed = true;
            }

            if (changed)
                steps.Add("construction flood mirrors refreshed");

            return changed;
        }

        private static byte PackLegacyConstructionHealthByte(float integrity)
        {
            if (!math.isfinite(integrity) || integrity <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(integrity * 0.01f) * 255f), 0, 255);
        }

        private static bool EnsureProceduralWorldState(ref ProceduralWorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            int suppressedPlacementBound = ClampArrayLength(dto.suppressedPlacementKeys, ProceduralWorldStateDTO.MaxSuppressedPlacements);
            int faunaStateBound = ClampArrayLength(dto.faunaStates, ProceduralWorldStateDTO.MaxFaunaStates);
            int hibernatedFaunaBound = ClampArrayLength(dto.hibernatedFaunaStates, ProceduralWorldStateDTO.MaxHibernatedFaunaStates);
            int geologySeamBound = ClampArrayLength(dto.geologySeamStates, ProceduralWorldStateDTO.MaxGeologySeamStates);
            int geologyCaveBound = ClampArrayLength(dto.geologyCaveEntrances, ProceduralWorldStateDTO.MaxGeologyCaveEntrances);

            if (dto.suppressedPlacementKeys == null || dto.suppressedPlacementKeys.Length != ProceduralWorldStateDTO.MaxSuppressedPlacements ||
                dto.faunaStates == null || dto.faunaStates.Length != ProceduralWorldStateDTO.MaxFaunaStates ||
                dto.hibernatedFaunaStates == null || dto.hibernatedFaunaStates.Length != ProceduralWorldStateDTO.MaxHibernatedFaunaStates ||
                dto.geologySeamStates == null || dto.geologySeamStates.Length != ProceduralWorldStateDTO.MaxGeologySeamStates ||
                dto.geologyCaveEntrances == null || dto.geologyCaveEntrances.Length != ProceduralWorldStateDTO.MaxGeologyCaveEntrances)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural world state capacity repaired");
            }

            int clampedSuppressed = math.clamp(dto.suppressedPlacementCount, 0, suppressedPlacementBound);
            if (clampedSuppressed != dto.suppressedPlacementCount)
            {
                dto.suppressedPlacementCount = clampedSuppressed;
                changed = true;
                steps.Add("procedural suppressed placement count clamped");
            }

            int clampedFauna = math.clamp(dto.faunaStateCount, 0, faunaStateBound);
            if (clampedFauna != dto.faunaStateCount)
            {
                dto.faunaStateCount = clampedFauna;
                changed = true;
                steps.Add("procedural fauna state count clamped");
            }

            int clampedHibernatedFauna = math.clamp(dto.hibernatedFaunaCount, 0, hibernatedFaunaBound);
            if (clampedHibernatedFauna != dto.hibernatedFaunaCount)
            {
                dto.hibernatedFaunaCount = clampedHibernatedFauna;
                changed = true;
                steps.Add("hibernated fauna state count clamped");
            }

            bool repairedFaunaStates = false;
            for (int i = 0; i < dto.faunaStateCount; i++)
            {
                ProceduralFaunaStateDTO state = dto.faunaStates[i];
                ProceduralFaunaStateDTO safeState = ProceduralFaunaStateDTO.SanitizeForPersistence(in state);
                if (ProceduralFaunaStateDTO.PersistenceEquals(in state, in safeState))
                    continue;

                dto.faunaStates[i] = safeState;
                repairedFaunaStates = true;
                changed = true;
            }

            if (repairedFaunaStates)
                steps.Add("procedural fauna states repaired");

            bool repairedHibernatedFaunaStates = false;
            for (int i = 0; i < dto.hibernatedFaunaCount; i++)
            {
                HibernatedFaunaStateDTO state = dto.hibernatedFaunaStates[i];
                HibernatedFaunaStateDTO safeState = HibernatedFaunaStateDTO.SanitizeForPersistence(in state);
                if (HibernatedFaunaStateDTO.PersistenceEquals(in state, in safeState))
                    continue;

                dto.hibernatedFaunaStates[i] = safeState;
                repairedHibernatedFaunaStates = true;
                changed = true;
            }

            if (repairedHibernatedFaunaStates)
                steps.Add("hibernated fauna states repaired");

            int clampedSeamStates = math.clamp(dto.geologySeamStateCount, 0, geologySeamBound);
            if (clampedSeamStates != dto.geologySeamStateCount)
            {
                dto.geologySeamStateCount = clampedSeamStates;
                changed = true;
                steps.Add("procedural geology seam count clamped");
            }

            int clampedCaveEntrances = math.clamp(dto.geologyCaveEntranceCount, 0, geologyCaveBound);
            if (clampedCaveEntrances != dto.geologyCaveEntranceCount)
            {
                dto.geologyCaveEntranceCount = clampedCaveEntrances;
                changed = true;
                steps.Add("procedural geology cave entrance count clamped");
            }

            bool repairedGeologySeamStates = false;
            for (int i = 0; i < dto.geologySeamStateCount; i++)
            {
                ProceduralGeologySeamStateDTO state = dto.geologySeamStates[i];
                ProceduralGeologySeamStateDTO safeState = ProceduralGeologySeamStateDTO.SanitizeForPersistence(in state);
                if (ProceduralGeologySeamStateDTO.PersistenceEquals(in state, in safeState))
                    continue;

                dto.geologySeamStates[i] = safeState;
                repairedGeologySeamStates = true;
                changed = true;
            }

            if (repairedGeologySeamStates)
                steps.Add("procedural geology seam states repaired");

            bool repairedGeologyCaveEntrances = false;
            for (int i = 0; i < dto.geologyCaveEntranceCount; i++)
            {
                ProceduralGeologyCaveEntranceDTO entrance = dto.geologyCaveEntrances[i];
                ProceduralGeologyCaveEntranceDTO safeEntrance = ProceduralGeologyCaveEntranceDTO.SanitizeForPersistence(in entrance);
                if (ProceduralGeologyCaveEntranceDTO.PersistenceEquals(in entrance, in safeEntrance))
                    continue;

                dto.geologyCaveEntrances[i] = safeEntrance;
                repairedGeologyCaveEntrances = true;
                changed = true;
            }

            if (repairedGeologyCaveEntrances)
                steps.Add("procedural geology cave entrances repaired");

            return changed;
        }

        private static int ClampArrayLength<T>(T[] values, int maxCount)
        {
            if (values == null || maxCount <= 0)
                return 0;

            return math.min(values.Length, maxCount);
        }

        private static bool EnsureScanLog(ref ScanLogDTO dto, List<string> steps)
        {
            bool changed = false;
            int entryBound = ClampArrayLength(dto.entries, ScanLogDTO.MaxEntries);
            int recentEntryBound = ClampArrayLength(dto.recentEntryIds, ScanLogDTO.MaxRecentEntries);

            if (dto.entries == null || dto.entries.Length != ScanLogDTO.MaxEntries ||
                dto.recentEntryIds == null || dto.recentEntryIds.Length != ScanLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("scan log capacity repaired");
            }

            int clampedEntries = math.clamp(dto.entryCount, 0, entryBound);
            if (clampedEntries != dto.entryCount)
            {
                dto.entryCount = clampedEntries;
                changed = true;
                steps.Add("scan log count clamped");
            }

            int clampedRecent = math.clamp(dto.recentCount, 0, recentEntryBound);
            if (clampedRecent != dto.recentCount)
            {
                dto.recentCount = clampedRecent;
                changed = true;
                steps.Add("scan log recent count clamped");
            }

            bool repairedEntries = false;
            for (int i = 0; i < dto.entryCount; i++)
            {
                ScanEntryDTO entry = dto.entries[i];
                ScanEntryDTO safeEntry = ScanEntryDTO.SanitizeForPersistence(in entry);
                if (ScanEntryDTO.PersistenceEquals(in entry, in safeEntry))
                    continue;

                dto.entries[i] = safeEntry;
                repairedEntries = true;
                changed = true;
            }

            if (repairedEntries)
                steps.Add("scan log entries repaired");

            changed |= CompactNonBlankStringArrayEntries(
                dto.recentEntryIds,
                ref dto.recentCount,
                ScanLogDTO.MaxRecentEntries,
                "scan log recent ids repaired",
                steps);

            return changed;
        }

        private static bool EnsureBarter(ref BarterDTO dto, List<string> steps)
        {
            bool changed = false;
            int offerBound = ClampArrayLength(dto.offerStates, BarterDTO.MaxOffers);
            int transactionBound = ClampArrayLength(dto.recentTransactions, BarterDTO.MaxRecentTransactions);

            if (dto.offerStates == null || dto.offerStates.Length != BarterDTO.MaxOffers ||
                dto.recentTransactions == null || dto.recentTransactions.Length != BarterDTO.MaxRecentTransactions)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("barter capacity repaired");
            }

            int clampedStates = math.clamp(dto.stateCount, 0, offerBound);
            if (clampedStates != dto.stateCount)
            {
                dto.stateCount = clampedStates;
                changed = true;
                steps.Add("barter state count clamped");
            }

            int clampedTransactions = math.clamp(dto.recentTransactionCount, 0, transactionBound);
            if (clampedTransactions != dto.recentTransactionCount)
            {
                dto.recentTransactionCount = clampedTransactions;
                changed = true;
                steps.Add("barter transaction count clamped");
            }

            bool repairedOfferStates = false;
            for (int i = 0; i < dto.stateCount; i++)
            {
                BarterOfferStateDTO offerState = dto.offerStates[i];
                BarterOfferStateDTO safeOfferState = BarterOfferStateDTO.SanitizeForPersistence(in offerState);
                if (BarterOfferStateDTO.PersistenceEquals(in offerState, in safeOfferState))
                    continue;

                dto.offerStates[i] = safeOfferState;
                repairedOfferStates = true;
                changed = true;
            }

            if (repairedOfferStates)
                steps.Add("barter offer states repaired");

            bool repairedTransactions = false;
            for (int i = 0; i < dto.recentTransactionCount; i++)
            {
                BarterTransactionDTO transaction = dto.recentTransactions[i];
                BarterTransactionDTO safeTransaction = BarterTransactionDTO.SanitizeForPersistence(in transaction);
                if (BarterTransactionDTO.PersistenceEquals(in transaction, in safeTransaction))
                    continue;

                dto.recentTransactions[i] = safeTransaction;
                repairedTransactions = true;
                changed = true;
            }

            if (repairedTransactions)
                steps.Add("barter transactions repaired");

            return changed;
        }

        private static bool EnsureFieldOperations(ref FieldOperationLogDTO dto, List<string> steps)
        {
            bool changed = false;
            int recentEntryBound = ClampArrayLength(dto.recentEntries, FieldOperationLogDTO.MaxRecentEntries);

            if (dto.recentEntries == null || dto.recentEntries.Length != FieldOperationLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("field log capacity repaired");
            }

            int clamped = math.clamp(dto.recentCount, 0, recentEntryBound);
            if (clamped != dto.recentCount)
            {
                dto.recentCount = clamped;
                changed = true;
                steps.Add("field log count clamped");
            }

            bool repairedEntries = false;
            for (int i = 0; i < dto.recentCount; i++)
            {
                FieldOperationEntryDTO entry = dto.recentEntries[i];
                FieldOperationEntryDTO safeEntry = FieldOperationEntryDTO.SanitizeForPersistence(in entry);
                if (FieldOperationEntryDTO.PersistenceEquals(in entry, in safeEntry))
                    continue;

                dto.recentEntries[i] = safeEntry;
                repairedEntries = true;
                changed = true;
            }

            if (repairedEntries)
                steps.Add("field log entries repaired");

            return changed;
        }

        private static bool EnsureBeaconNetwork(ref BeaconNetworkDTO dto, List<string> steps)
        {
            bool changed = false;
            int beaconBound = ClampArrayLength(dto.entries, BeaconNetworkDTO.MaxEntries);

            if (dto.entries == null || dto.entries.Length != BeaconNetworkDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("beacon capacity repaired");
            }

            int clamped = math.clamp(dto.activeCount, 0, beaconBound);
            if (clamped != dto.activeCount)
            {
                dto.activeCount = clamped;
                changed = true;
                steps.Add("beacon count clamped");
            }

            bool repairedBeaconEntries = false;
            for (int i = 0; i < dto.activeCount; i++)
            {
                BeaconEntryDTO entry = dto.entries[i];

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    entry.id = BuildRepairedBeaconId(i, dto.nextSequence);
                    repairedBeaconEntries = true;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(entry.label))
                {
                    entry.label = $"{DefaultBeaconLabelPrefix} {i + 1:00}";
                    repairedBeaconEntries = true;
                    changed = true;
                }

                BeaconEntryDTO safeEntry = BeaconEntryDTO.SanitizeForPersistence(in entry);
                if (!BeaconEntryDTO.PersistenceEquals(in entry, in safeEntry))
                {
                    entry = safeEntry;
                    repairedBeaconEntries = true;
                    changed = true;
                }

                dto.entries[i] = entry;
            }

            if (repairedBeaconEntries)
                steps.Add("beacon entries repaired");

            if (dto.nextSequence <= 0)
            {
                dto.nextSequence = math.max(1, dto.activeCount + 1);
                changed = true;
                steps.Add("beacon sequence repaired");
            }

            return changed;
        }

        // ── v11-16: Lore Systems ──────────────────────────────────

        private static string BuildRepairedBeaconId(int entryIndex, int nextSequence)
        {
            uint salt = unchecked((uint)LocHash.Compute("SaveDataMigration.BeaconNetwork.RepairedId"));
            uint safeIndex = unchecked((uint)math.max(0, entryIndex));
            uint safeSequence = unchecked((uint)math.max(1, nextSequence));
            uint capacity = unchecked((uint)BeaconNetworkDTO.MaxEntries);
            return $"{salt:x8}{safeIndex:x8}{safeSequence:x8}{capacity:x8}";
        }

        private static bool EnsureLoreSystems(ref SaveData data, int sourceVersion, List<string> steps)
        {
            bool changed = false;

            if (data.audioLogDiscoveredIds == null)
            {
                data.audioLogDiscoveredIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("audioLog list created");
            }
            changed |= TrimListToMax(
                data.audioLogDiscoveredIds,
                SaveData.MaxLegacyAudioLogDiscoveredIds,
                "audioLog list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.audioLogDiscoveredIds,
                "audioLog ids repaired",
                steps);

            if (!AudioLogDiscoveryBitMask.HasExpectedCapacity(data.audioLogDiscoveryBitWords))
            {
                AudioLogDiscoveryBitMask.EnsureCapacity(ref data.audioLogDiscoveryBitWords);
                changed = true;
                steps.Add("audioLog discovery bit words created");
            }

            int encryptedFragmentBound = ClampArrayLength(
                data.audioLogEncryptedFragmentHashes,
                SaveData.MaxEncryptedAudioLogFragments);
            encryptedFragmentBound = math.min(
                encryptedFragmentBound,
                ClampArrayLength(data.audioLogEncryptedFragmentBits, SaveData.MaxEncryptedAudioLogFragments));

            if (data.audioLogEncryptedFragmentHashes == null ||
                data.audioLogEncryptedFragmentHashes.Length != SaveData.MaxEncryptedAudioLogFragments)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentHashes,
                    SaveData.MaxEncryptedAudioLogFragments);
                changed = true;
                steps.Add("encrypted audio-log hash state created");
            }

            if (data.audioLogEncryptedFragmentBits == null ||
                data.audioLogEncryptedFragmentBits.Length != SaveData.MaxEncryptedAudioLogFragments)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentBits,
                    SaveData.MaxEncryptedAudioLogFragments);
                changed = true;
                steps.Add("encrypted audio-log bit state created");
            }

            int clampedEncryptedFragmentCount = math.clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                encryptedFragmentBound);
            if (clampedEncryptedFragmentCount != data.audioLogEncryptedFragmentCount)
            {
                data.audioLogEncryptedFragmentCount = clampedEncryptedFragmentCount;
                changed = true;
                steps.Add("encrypted audio-log fragment count clamped");
            }
            changed |= ClearEncryptedAudioLogFragmentTail(data, steps);

            if (data.industrialLoreUnlockWords == null ||
                data.industrialLoreUnlockWords.Length != IndustrialLoreBitMask.WordCount)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.industrialLoreUnlockWords,
                    IndustrialLoreBitMask.WordCount);
                changed = true;
                steps.Add("industrial lore bit words created");
            }
            if (IndustrialLoreBitMask.SanitizeWords(data.industrialLoreUnlockWords))
            {
                changed = true;
                steps.Add("industrial lore bit words repaired");
            }

            if (data.dataArchaeologyDiscoveryBitWords == null ||
                data.dataArchaeologyDiscoveryBitWords.Length != SaveData.MaxDataArchaeologyDiscoveryWords)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.dataArchaeologyDiscoveryBitWords,
                    SaveData.MaxDataArchaeologyDiscoveryWords);
                changed = true;
                steps.Add("data archaeology bit words created");
            }

            int archaeologyPartialBound = ClampArrayLength(
                data.dataArchaeologyPartialScanHashes,
                SaveData.MaxDataArchaeologyPartialScans);
            archaeologyPartialBound = math.min(
                archaeologyPartialBound,
                ClampArrayLength(
                    data.dataArchaeologyPartialScanProgressPermille,
                    SaveData.MaxDataArchaeologyPartialScans));

            if (data.dataArchaeologyPartialScanHashes == null ||
                data.dataArchaeologyPartialScanHashes.Length != SaveData.MaxDataArchaeologyPartialScans)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.dataArchaeologyPartialScanHashes,
                    SaveData.MaxDataArchaeologyPartialScans);
                changed = true;
                steps.Add("data archaeology partial hashes created");
            }

            if (data.dataArchaeologyPartialScanProgressPermille == null ||
                data.dataArchaeologyPartialScanProgressPermille.Length != SaveData.MaxDataArchaeologyPartialScans)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.dataArchaeologyPartialScanProgressPermille,
                    SaveData.MaxDataArchaeologyPartialScans);
                changed = true;
                steps.Add("data archaeology partial progress created");
            }

            int clampedArchaeologyPartialCount = math.clamp(
                data.dataArchaeologyPartialScanCount,
                0,
                archaeologyPartialBound);
            if (clampedArchaeologyPartialCount != data.dataArchaeologyPartialScanCount)
            {
                data.dataArchaeologyPartialScanCount = clampedArchaeologyPartialCount;
                changed = true;
                steps.Add("data archaeology partial count clamped");
            }

            bool repairedArchaeologyPartialProgress = false;
            for (int i = 0; i < data.dataArchaeologyPartialScanCount; i++)
            {
                ushort safeProgress = (ushort)Math.Min(
                    data.dataArchaeologyPartialScanProgressPermille[i],
                    MaxDataArchaeologyPartialProgressPermille);
                if (safeProgress == data.dataArchaeologyPartialScanProgressPermille[i])
                    continue;

                data.dataArchaeologyPartialScanProgressPermille[i] = safeProgress;
                repairedArchaeologyPartialProgress = true;
                changed = true;
            }

            if (repairedArchaeologyPartialProgress)
                steps.Add("data archaeology partial progress repaired");

            int archaeologyScanStateBound = ClampArrayLength(
                data.dataArchaeologyScanStateKeys,
                SaveData.MaxDataArchaeologyScanStates);
            archaeologyScanStateBound = math.min(
                archaeologyScanStateBound,
                ClampArrayLength(data.dataArchaeologyScanStateValues, SaveData.MaxDataArchaeologyScanStates));

            if (data.dataArchaeologyScanStateKeys == null ||
                data.dataArchaeologyScanStateKeys.Length != SaveData.MaxDataArchaeologyScanStates)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.dataArchaeologyScanStateKeys,
                    SaveData.MaxDataArchaeologyScanStates);
                changed = true;
                steps.Add("data archaeology scan-state keys created");
            }

            if (data.dataArchaeologyScanStateValues == null ||
                data.dataArchaeologyScanStateValues.Length != SaveData.MaxDataArchaeologyScanStates)
            {
                SaveData.EnsureExactArrayCapacity(
                    ref data.dataArchaeologyScanStateValues,
                    SaveData.MaxDataArchaeologyScanStates);
                changed = true;
                steps.Add("data archaeology scan-state values created");
            }

            int clampedArchaeologyScanStateCount = math.clamp(
                data.dataArchaeologyScanStateCount,
                0,
                archaeologyScanStateBound);
            if (clampedArchaeologyScanStateCount != data.dataArchaeologyScanStateCount)
            {
                data.dataArchaeologyScanStateCount = clampedArchaeologyScanStateCount;
                changed = true;
                steps.Add("data archaeology scan-state count clamped");
            }

            bool repairedArchaeologyScanStateValues = false;
            for (int i = 0; i < data.dataArchaeologyScanStateCount; i++)
            {
                byte safeValue = data.dataArchaeologyScanStateValues[i] <= MaxDataArchaeologyScanStateValue
                    ? data.dataArchaeologyScanStateValues[i]
                    : (byte)0;
                if (safeValue == data.dataArchaeologyScanStateValues[i])
                    continue;

                data.dataArchaeologyScanStateValues[i] = safeValue;
                repairedArchaeologyScanStateValues = true;
                changed = true;
            }

            if (repairedArchaeologyScanStateValues)
                steps.Add("data archaeology scan-state values repaired");

            if (data.questActiveIds == null)
            {
                data.questActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest active list created");
            }
            changed |= TrimListToMax(
                data.questActiveIds,
                SaveData.MaxLegacyQuestIds,
                "quest active list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.questActiveIds,
                "quest active ids repaired",
                steps);

            if (data.questCompletedIds == null)
            {
                data.questCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest completed list created");
            }
            changed |= TrimListToMax(
                data.questCompletedIds,
                SaveData.MaxLegacyQuestIds,
                "quest completed list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.questCompletedIds,
                "quest completed ids repaired",
                steps);

            if (data.suitInstalledUpgradeIds == null)
            {
                data.suitInstalledUpgradeIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit upgrades list created");
            }
            changed |= TrimListToMax(
                data.suitInstalledUpgradeIds,
                SaveData.MaxSuitUpgradeIds,
                "suit upgrades list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.suitInstalledUpgradeIds,
                "suit upgrade ids repaired",
                steps);

            if (data.suitUnlockedBlueprintIds == null)
            {
                data.suitUnlockedBlueprintIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit blueprints list created");
            }
            changed |= TrimListToMax(
                data.suitUnlockedBlueprintIds,
                SaveData.MaxSuitUpgradeIds,
                "suit blueprints list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.suitUnlockedBlueprintIds,
                "suit blueprint ids repaired",
                steps);

            if (data.corporateReceivedOrderIds == null)
            {
                data.corporateReceivedOrderIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("corporate orders list created");
            }
            changed |= TrimListToMax(
                data.corporateReceivedOrderIds,
                SaveData.MaxCorporateOrderIds,
                "corporate orders list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.corporateReceivedOrderIds,
                "corporate order ids repaired",
                steps);

            if (data.corporatePendingOrderIds == null)
            {
                data.corporatePendingOrderIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("corporate pending orders list created");
            }

            if (data.corporatePendingOrderTimers == null)
            {
                data.corporatePendingOrderTimers = new System.Collections.Generic.List<float>();
                changed = true;
                steps.Add("corporate order timers list created");
            }
            changed |= TrimPairedListsToMax(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                SaveData.MaxCorporateOrderIds,
                "corporate pending orders capped",
                steps);
            changed |= CompactNonBlankStringFloatPairs(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                "corporate pending order ids repaired",
                steps);
            changed |= EnsureNonNegativeFiniteFloatList(
                data.corporatePendingOrderTimers,
                "corporate order timers repaired",
                steps);

            if (data.missionActiveIds == null)
            {
                data.missionActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission active list created");
            }
            changed |= TrimListToMax(
                data.missionActiveIds,
                SaveData.MaxMissionIds,
                "mission active list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.missionActiveIds,
                "mission active ids repaired",
                steps);

            if (data.missionCompletedIds == null)
            {
                data.missionCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission completed list created");
            }
            changed |= TrimListToMax(
                data.missionCompletedIds,
                SaveData.MaxMissionIds,
                "mission completed list capped",
                steps);
            changed |= CompactNonBlankStringListEntries(
                data.missionCompletedIds,
                "mission completed ids repaired",
                steps);

            float safeAtlasSignalPulseTimer = SanitizeNonNegativeFinite(data.atlasSignalPulseTimer);
            if (!Approximately(data.atlasSignalPulseTimer, safeAtlasSignalPulseTimer))
            {
                data.atlasSignalPulseTimer = safeAtlasSignalPulseTimer;
                changed = true;
                steps.Add("atlas signal pulse timer repaired");
            }

            int inferredRevealStage = data.endingConditionMet
                ? MaxAtlasRevealStage
                : data.narrativeDepthTier >= 4
                    ? 3
                    : data.narrativeDepthTier >= 3
                        ? 2
                        : data.atlasSignalDetected
                            ? 2
                            : 0;

            int clampedRevealStage = math.clamp(data.atlasSignalRevealStage, 0, MaxAtlasRevealStage);
            if (sourceVersion < PreV73RepairVersion && clampedRevealStage < inferredRevealStage)
                clampedRevealStage = inferredRevealStage;

            if (clampedRevealStage != data.atlasSignalRevealStage)
            {
                data.atlasSignalRevealStage = clampedRevealStage;
                changed = true;
                steps.Add("atlas reveal stage repaired");
            }

            return changed;
        }

        private static bool ClearEncryptedAudioLogFragmentTail(SaveData data, List<string> steps)
        {
            if (data == null)
                return false;

            int safeCount = math.clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                SaveData.MaxEncryptedAudioLogFragments);
            bool changed = false;

            if (ClearUIntTail(data.audioLogEncryptedFragmentHashes, safeCount))
                changed = true;
            if (ClearUIntTail(data.audioLogEncryptedFragmentBits, safeCount))
                changed = true;

            if (changed)
                steps.Add("encrypted audio-log fragment tail cleared");

            return changed;
        }

        private static bool ClearUIntTail(uint[] values, int startIndex)
        {
            if (values == null || startIndex >= values.Length)
                return false;

            int safeStartIndex = math.clamp(startIndex, 0, values.Length);
            bool changed = false;
            for (int i = safeStartIndex; i < values.Length; i++)
            {
                if (values[i] == 0u)
                    continue;

                values[i] = 0u;
                changed = true;
            }

            return changed;
        }
    }
}
