using Hecton.Localization;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Editor
{
    public static class FaunaDataTemplateAuthoring
    {
        private const string ArchetypeRoot = "Assets/_Project/Data/AI/CreatureArchetypes";
        private const string TemplateRoot = "Assets/_Project/Data/AI/FaunaTemplates";

        [MenuItem("Hecton/Authoring/Build Fauna Data Templates", priority = 183)]
        public static void BuildFaunaDataTemplates()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/AI");
            EnsureFolder(TemplateRoot);
            CreatureProxyPrefabAuthoring.EnsureProxyAssets();

            string[] archetypeGuids = AssetDatabase.FindAssets("t:CreatureArchetypeData", new[] { ArchetypeRoot });
            int authoredCount = 0;

            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                string archetypePath = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                CreatureArchetypeData archetype = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(archetypePath);
                if (archetype == null)
                    continue;

                authoredCount += CreateOrUpdateTemplate(archetype) ? 1 : 0;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FaunaDataTemplateAuthoring] Authored {authoredCount} fauna data templates.");
        }

        private static bool CreateOrUpdateTemplate(CreatureArchetypeData archetype)
        {
            string assetName = $"FaunaDataTemplate_{ToAssetToken(archetype.displayName)}";
            string assetPath = $"{TemplateRoot}/{assetName}.asset";
            FaunaDataTemplate template = AssetDatabase.LoadAssetAtPath<FaunaDataTemplate>(assetPath);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<FaunaDataTemplate>();
                AssetDatabase.CreateAsset(template, assetPath);
            }

            SerializedObject serializedTemplate = new SerializedObject(template);
            float massKg = Mathf.Max(8f, archetype.maxHealth * 0.8f);
            float bodyRadius = ResolveBodyRadius(archetype);
            float steeringResponse = ResolveSteeringResponse(archetype);
            int speciesId = ComputeStableSpeciesId(archetype.creatureId);

            SetInt(serializedTemplate, "speciesId", speciesId);
            SetObjectReference(serializedTemplate, "speciesProfile", null);
            SetObjectReference(serializedTemplate, "archetype", archetype);
            SetFloat(serializedTemplate, "massKg", massKg);
            SetFloat(serializedTemplate, "bodyRadiusMeters", bodyRadius);
            SetFloat(serializedTemplate, "cruiseSpeedMetersPerSecond", Mathf.Max(0.1f, archetype.cruiseSpeed));
            SetFloat(serializedTemplate, "maxSpeedMetersPerSecond", Mathf.Max(archetype.cruiseSpeed, archetype.burstSpeed));
            SetFloat(serializedTemplate, "steeringResponse", steeringResponse);
            SetInt(serializedTemplate, "maxSchoolCount", ResolveSchoolCount(archetype));
            SetString(serializedTemplate, "scanEntryId", archetype.creatureId);
            SetString(serializedTemplate, "scanEntryTitle", string.IsNullOrWhiteSpace(archetype.displayName) ? "UNIDENTIFIED BIOFORM" : archetype.displayName);
            SetString(serializedTemplate, "scanEntryCategory", archetype.roleType.ToString());
            SetString(serializedTemplate, "scanEntrySummary", BuildScanSummary(archetype));

            Vector3 driveWeights = ResolveDriveWeights(archetype);
            SerializedProperty driveWeightsProperty = serializedTemplate.FindProperty("driveWeights");
            driveWeightsProperty.arraySize = 3;
            driveWeightsProperty.GetArrayElementAtIndex((int)FaunaDriveChannel.Hunger).floatValue = driveWeights.x;
            driveWeightsProperty.GetArrayElementAtIndex((int)FaunaDriveChannel.Fear).floatValue = driveWeights.y;
            driveWeightsProperty.GetArrayElementAtIndex((int)FaunaDriveChannel.Curiosity).floatValue = driveWeights.z;

            SerializedProperty interactionMatrixProperty = serializedTemplate.FindProperty("interactionMatrix");
            interactionMatrixProperty.arraySize = 2;
            WriteInteractionEntry(interactionMatrixProperty.GetArrayElementAtIndex(0), FaunaInteractionKind.Stun, 1f, ResolveStunRetreat(archetype), ResolveStunFear(archetype), true);
            WriteInteractionEntry(interactionMatrixProperty.GetArrayElementAtIndex(1), FaunaInteractionKind.Cut, ResolveCutDamageMultiplier(archetype), ResolveCutRetreat(archetype), ResolveCutFear(archetype), true);

            SerializedProperty loreHashesProperty = serializedTemplate.FindProperty("loreUnlockHashes");
            loreHashesProperty.arraySize = 2;
            loreHashesProperty.GetArrayElementAtIndex(0).uintValue = ComputeStableUnlockHash($"fauna.codex.{archetype.creatureId}");
            loreHashesProperty.GetArrayElementAtIndex(1).uintValue = ComputeStableUnlockHash($"fauna.research.{archetype.creatureId}");

            serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);

            if (archetype.faunaDataTemplate != template)
            {
                archetype.faunaDataTemplate = template;
                EditorUtility.SetDirty(archetype);
            }

            return true;
        }

        private static float ResolveBodyRadius(CreatureArchetypeData archetype)
        {
            switch (archetype.roleType)
            {
                case CreatureRoleType.Leviathan:
                    return 2.2f;
                case CreatureRoleType.Hunter:
                    return archetype.maxHealth >= 100f ? 0.95f : 0.7f;
                case CreatureRoleType.Territorial:
                    return 0.65f;
                case CreatureRoleType.DroneTrader:
                    return 0.8f;
                default:
                    return 0.55f;
            }
        }

        private static float ResolveSteeringResponse(CreatureArchetypeData archetype)
        {
            float normalizedTurn = Mathf.Clamp(archetype.turnSpeed / 5f, 0.35f, 1.8f);
            return Mathf.Max(0.5f, normalizedTurn);
        }

        private static int ResolveSchoolCount(CreatureArchetypeData archetype)
        {
            switch (archetype.roleType)
            {
                case CreatureRoleType.Ambient:
                    return 12;
                case CreatureRoleType.Hunter:
                    return archetype.usePackHunt ? 6 : 2;
                case CreatureRoleType.Territorial:
                    return 3;
                case CreatureRoleType.Leviathan:
                    return 1;
                default:
                    return 2;
            }
        }

        private static Vector3 ResolveDriveWeights(CreatureArchetypeData archetype)
        {
            switch (archetype.roleType)
            {
                case CreatureRoleType.Leviathan:
                    return new Vector3(1.35f, 0.7f, 0.45f);
                case CreatureRoleType.Hunter:
                    return archetype.usePackHunt
                        ? new Vector3(1.25f, 0.9f, 0.65f)
                        : new Vector3(1.15f, 1.05f, 0.55f);
                case CreatureRoleType.Territorial:
                    return new Vector3(0.95f, 0.85f, 0.4f);
                case CreatureRoleType.DroneTrader:
                    return new Vector3(0.55f, 1.1f, 1.25f);
                default:
                    return new Vector3(0.7f, 1.2f, 1.15f);
            }
        }

        private static string BuildScanSummary(CreatureArchetypeData archetype)
        {
            if (!string.IsNullOrWhiteSpace(archetype.gameplayPurpose) && !string.IsNullOrWhiteSpace(archetype.biomeNotes))
                return $"{archetype.gameplayPurpose.Trim()} {archetype.biomeNotes.Trim()}";

            if (!string.IsNullOrWhiteSpace(archetype.gameplayPurpose))
                return archetype.gameplayPurpose.Trim();

            if (!string.IsNullOrWhiteSpace(archetype.biomeNotes))
                return archetype.biomeNotes.Trim();

            return "Passive fauna contact. Manual classification pending.";
        }

        private static float ResolveStunRetreat(CreatureArchetypeData archetype)
        {
            return archetype.roleType == CreatureRoleType.Leviathan ? 2.5f : 4.5f;
        }

        private static float ResolveCutRetreat(CreatureArchetypeData archetype)
        {
            return archetype.roleType == CreatureRoleType.Leviathan ? 5f : 7f;
        }

        private static float ResolveStunFear(CreatureArchetypeData archetype)
        {
            return archetype.roleType == CreatureRoleType.Leviathan ? 0.35f : 0.65f;
        }

        private static float ResolveCutFear(CreatureArchetypeData archetype)
        {
            return archetype.roleType == CreatureRoleType.Leviathan ? 0.55f : 0.9f;
        }

        private static float ResolveCutDamageMultiplier(CreatureArchetypeData archetype)
        {
            return archetype.roleType == CreatureRoleType.Leviathan ? 1.05f : 1.2f;
        }

        private static int ComputeStableSpeciesId(string creatureId)
        {
            uint hash = unchecked((uint)LocHash.Compute(string.IsNullOrWhiteSpace(creatureId) ? "fauna.unknown" : creatureId));
            return (int)(hash & int.MaxValue);
        }

        private static uint ComputeStableUnlockHash(string value)
        {
            uint hash = unchecked((uint)LocHash.Compute(string.IsNullOrWhiteSpace(value) ? "fauna.unlock.unknown" : value));
            return hash;
        }

        private static void WriteInteractionEntry(SerializedProperty property, FaunaInteractionKind interactionKind, float damageMultiplier, float retreatDuration, float fearImpulse01, bool forceRetreat)
        {
            property.FindPropertyRelative("interactionKind").enumValueIndex = (int)interactionKind;
            property.FindPropertyRelative("damageMultiplier").floatValue = damageMultiplier;
            property.FindPropertyRelative("retreatDurationSeconds").floatValue = retreatDuration;
            property.FindPropertyRelative("fearImpulse01").floatValue = fearImpulse01;
            property.FindPropertyRelative("forceRetreat").boolValue = forceRetreat;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            serializedObject.FindProperty(propertyName).intValue = value;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            serializedObject.FindProperty(propertyName).floatValue = value;
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            serializedObject.FindProperty(propertyName).stringValue = value ?? string.Empty;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static string ToAssetToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            return value.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
