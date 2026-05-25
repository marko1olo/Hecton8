using System.Globalization;
using System.IO;
using Hecton8.Building;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class AbandonedHabitatModuleAuthoring
    {
        private const string TemplateFolder = "Assets/_Project/Data/Construction/AbandonedModuleTemplates";
        private const string LedgerPath = "Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/PROJECT_CONTENT_LEDGER.md";

        [MenuItem("Hecton/Authoring/Rebuild Abandoned Habitat Module Templates", priority = 217)]
        public static void RebuildAbandonedHabitatModuleTemplates()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Construction");
            EnsureFolder(TemplateFolder);

            ModuleTemplateSeed[] seeds =
            {
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_Corridor",
                    "base.module.corridor",
                    0.38f,
                    0.45f,
                    0.32f,
                    12f,
                    180f,
                    new[] { new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 3f) },
                    new[] { new VfxSocketSeed(new Vector3(0.7f, 0.2f, 2.5f), BaseModuleVfxSocketType.Leak), new VfxSocketSeed(new Vector3(-0.6f, 0.8f, -1.8f), BaseModuleVfxSocketType.Spark) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_Airlock",
                    "base.module.airlock",
                    0.42f,
                    0.48f,
                    0.36f,
                    18f,
                    140f,
                    new[] { new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f) },
                    new[] { new VfxSocketSeed(new Vector3(0.9f, 0.5f, 1.2f), BaseModuleVfxSocketType.Vent), new VfxSocketSeed(new Vector3(-0.8f, 0.5f, -1.2f), BaseModuleVfxSocketType.Spark) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_BioReactor",
                    "base.module.bioreactor",
                    0.24f,
                    0.52f,
                    0.4f,
                    44f,
                    260f,
                    new[] { new Vector3(0f, 0f, -2.5f), new Vector3(2.5f, 0f, 0f), new Vector3(-2.5f, 0f, 0f) },
                    new[] { new VfxSocketSeed(new Vector3(1.2f, 1.1f, 0.6f), BaseModuleVfxSocketType.Vent), new VfxSocketSeed(new Vector3(-1.3f, 0.4f, -0.5f), BaseModuleVfxSocketType.Leak) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_WindowObservation",
                    "base.module.window",
                    0.31f,
                    0.43f,
                    0.3f,
                    8f,
                    160f,
                    new[] { new Vector3(0f, 0f, -2.4f), new Vector3(0f, 0f, 2.4f) },
                    new[] { new VfxSocketSeed(new Vector3(0f, 0.7f, 1.9f), BaseModuleVfxSocketType.Leak) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_ControlRoom",
                    "base.module.control_room",
                    0.29f,
                    0.46f,
                    0.34f,
                    22f,
                    240f,
                    new[] { new Vector3(0f, 0f, -2.8f), new Vector3(2.8f, 0f, 0f), new Vector3(-2.8f, 0f, 0f) },
                    new[] { new VfxSocketSeed(new Vector3(1.1f, 1.3f, -1.1f), BaseModuleVfxSocketType.Spark), new VfxSocketSeed(new Vector3(-1.4f, 0.4f, 1.5f), BaseModuleVfxSocketType.Leak) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_JunctionT",
                    "base.module.junction_t",
                    0.34f,
                    0.44f,
                    0.33f,
                    14f,
                    210f,
                    new[] { new Vector3(0f, 0f, -2.8f), new Vector3(0f, 0f, 2.8f), new Vector3(2.8f, 0f, 0f) },
                    new[] { new VfxSocketSeed(new Vector3(0.8f, 0.6f, 2.1f), BaseModuleVfxSocketType.Leak), new VfxSocketSeed(new Vector3(1.9f, 1.0f, 0f), BaseModuleVfxSocketType.Vent) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_CrewQuarters",
                    "base.module.crew_quarters",
                    0.27f,
                    0.42f,
                    0.31f,
                    10f,
                    220f,
                    new[] { new Vector3(0f, 0f, -2.2f), new Vector3(0f, 0f, 2.2f) },
                    new[] { new VfxSocketSeed(new Vector3(-1.2f, 0.3f, 1.3f), BaseModuleVfxSocketType.Leak), new VfxSocketSeed(new Vector3(1.1f, 1.2f, -0.9f), BaseModuleVfxSocketType.Spark) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_ServiceSpine",
                    "base.module.service_spine",
                    0.22f,
                    0.5f,
                    0.38f,
                    20f,
                    150f,
                    new[] { new Vector3(0f, 0f, -3.4f), new Vector3(0f, 0f, 3.4f) },
                    new[] { new VfxSocketSeed(new Vector3(0.5f, 0.9f, -2.4f), BaseModuleVfxSocketType.Vent), new VfxSocketSeed(new Vector3(-0.7f, 0.2f, 2.2f), BaseModuleVfxSocketType.Leak) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_DockingClamp",
                    "base.module.docking_clamp",
                    0.33f,
                    0.47f,
                    0.35f,
                    16f,
                    170f,
                    new[] { new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 3f), new Vector3(-3f, 0f, 0f) },
                    new[] { new VfxSocketSeed(new Vector3(1.4f, 0.6f, 2.1f), BaseModuleVfxSocketType.Spark), new VfxSocketSeed(new Vector3(-1.7f, 0.3f, -1.8f), BaseModuleVfxSocketType.Leak) }),
                new ModuleTemplateSeed(
                    "BaseModuleTemplate_ResearchLab",
                    "base.module.research_lab",
                    0.26f,
                    0.45f,
                    0.34f,
                    28f,
                    280f,
                    new[] { new Vector3(0f, 0f, -2.6f), new Vector3(0f, 0f, 2.6f), new Vector3(2.6f, 0f, 0f) },
                    new[] { new VfxSocketSeed(new Vector3(0.9f, 1.4f, 1.1f), BaseModuleVfxSocketType.Spark), new VfxSocketSeed(new Vector3(-1.0f, 0.4f, -1.7f), BaseModuleVfxSocketType.Leak), new VfxSocketSeed(new Vector3(1.9f, 0.8f, 0f), BaseModuleVfxSocketType.Vent) }),
            };

            BaseModuleTemplate[] assets = new BaseModuleTemplate[seeds.Length];
            for (int i = 0; i < seeds.Length; i++)
                assets[i] = CreateOrUpdateTemplate(seeds[i]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteLedger(assets);
            BaseModulePrefabIntegrityEnforcer.EnforceBaseModulePrefabIntegrity();
        }

        private static BaseModuleTemplate CreateOrUpdateTemplate(ModuleTemplateSeed seed)
        {
            string assetPath = $"{TemplateFolder}/{seed.AssetName}.asset";
            BaseModuleTemplate asset = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BaseModuleTemplate>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            SerializedObject so = new SerializedObject(asset);
            so.FindProperty("stableId").stringValue = seed.StableId;
            so.FindProperty("powerDrawKW").floatValue = seed.PowerDrawKw;
            so.FindProperty("airVolumeM3").floatValue = seed.AirVolumeM3;
            so.FindProperty("defaultIntegrityState").floatValue = seed.DefaultIntegrityState;
            so.FindProperty("floodedBelowIntegrityState").floatValue = seed.FloodedBelowIntegrityState;
            so.FindProperty("oxygenOfflineBelowIntegrityState").floatValue = seed.OxygenOfflineBelowIntegrityState;
            WriteFloat3Array(so.FindProperty("snapPoints"), seed.SnapPoints);
            WriteVfxSockets(so.FindProperty("vfxSockets"), seed.VfxSockets);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return asset;
        }

        private static void WriteFloat3Array(SerializedProperty property, Vector3[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                WriteFloat3(element, values[i]);
            }
        }

        private static void WriteVfxSockets(SerializedProperty property, VfxSocketSeed[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                WriteFloat3(element.FindPropertyRelative("localPosition"), values[i].LocalPosition);
                element.FindPropertyRelative("socketType").enumValueIndex = (int)values[i].SocketType;
            }
        }

        private static void WriteFloat3(SerializedProperty property, Vector3 value)
        {
            property.FindPropertyRelative("x").floatValue = value.x;
            property.FindPropertyRelative("y").floatValue = value.y;
            property.FindPropertyRelative("z").floatValue = value.z;
        }

        private static void WriteLedger(BaseModuleTemplate[] assets)
        {
            string directory = Path.GetDirectoryName(LedgerPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using StreamWriter writer = new StreamWriter(LedgerPath, false);
            writer.WriteLine("# PROJECT_CONTENT_LEDGER");
            writer.WriteLine();
            writer.WriteLine("| Module | PersistentId | HashId | DefaultIntegrityState | AssetPath |");
            writer.WriteLine("|---|---|---:|---:|---|");

            for (int i = 0; i < assets.Length; i++)
            {
                BaseModuleTemplate asset = assets[i];
                if (asset == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(asset);
                writer.WriteLine(
                    "| " + asset.name +
                    " | " + asset.PersistentId +
                    " | " + asset.TemplateHashId +
                    " | " + asset.DefaultIntegrityState.ToString("0.00", CultureInfo.InvariantCulture) +
                    " | " + assetPath +
                    " |");
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int slashIndex = assetPath.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = assetPath.Substring(0, slashIndex);
            string child = assetPath.Substring(slashIndex + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, child);
        }

        private readonly struct ModuleTemplateSeed
        {
            public ModuleTemplateSeed(
                string assetName,
                string stableId,
                float defaultIntegrityState,
                float floodedBelowIntegrityState,
                float oxygenOfflineBelowIntegrityState,
                float powerDrawKw,
                float airVolumeM3,
                Vector3[] snapPoints,
                VfxSocketSeed[] vfxSockets)
            {
                AssetName = assetName;
                StableId = stableId;
                DefaultIntegrityState = defaultIntegrityState;
                FloodedBelowIntegrityState = floodedBelowIntegrityState;
                OxygenOfflineBelowIntegrityState = oxygenOfflineBelowIntegrityState;
                PowerDrawKw = powerDrawKw;
                AirVolumeM3 = airVolumeM3;
                SnapPoints = snapPoints;
                VfxSockets = vfxSockets;
            }

            public string AssetName { get; }
            public string StableId { get; }
            public float DefaultIntegrityState { get; }
            public float FloodedBelowIntegrityState { get; }
            public float OxygenOfflineBelowIntegrityState { get; }
            public float PowerDrawKw { get; }
            public float AirVolumeM3 { get; }
            public Vector3[] SnapPoints { get; }
            public VfxSocketSeed[] VfxSockets { get; }
        }

        private readonly struct VfxSocketSeed
        {
            public VfxSocketSeed(Vector3 localPosition, BaseModuleVfxSocketType socketType)
            {
                LocalPosition = localPosition;
                SocketType = socketType;
            }

            public Vector3 LocalPosition { get; }
            public BaseModuleVfxSocketType SocketType { get; }
        }
    }
}
