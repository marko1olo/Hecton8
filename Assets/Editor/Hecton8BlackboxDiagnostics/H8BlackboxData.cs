// H8BlackboxData.cs — Core DTOs for Hecton8 Blackbox Diagnostics
// READ-ONLY diagnostic tool. Does not modify scenes, settings, or project assets.
using System;
using System.Collections.Generic;

namespace Hecton8.BlackboxDiagnostics
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    public enum H8Severity { Info, Warning, Error, Critical }

    public enum H8FindingCategory
    {
        Bootstrap, Registry, MapMagic, Crest, Atmosphere, Urp,
        Camera, Console, Git, Scene, Project
    }

    // ── Key-Value Pair (JsonUtility-safe) ──────────────────────────────────────

    [Serializable]
    public class H8KV
    {
        public string key = "";
        public string value = "";

        public H8KV() { }
        public H8KV(string k, string v) { key = k ?? ""; value = v ?? ""; }
    }

    // ── Run Summary ───────────────────────────────────────────────────────────

    [Serializable]
    public class H8RunSummary
    {
        public bool success;
        public bool partialSuccess;
        public string abortReason = "";
        public string unityVersion = "";
        public string activeScene = "";
        public string mode = "";
        public string timestamp = "";
        public string outputPath = "";
        public int errorCount;
        public int warningCount;
        public int criticalCount;
        public List<string> outputFiles = new List<string>();
        public List<string> topFindings = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    // ── Project Metadata ──────────────────────────────────────────────────────

    [Serializable]
    public class H8ProjectMetadata
    {
        public string unityVersion = "";
        public string projectPath = "";
        public string platform = "";
        public string buildTarget = "";
        public string qualityLevelName = "";
        public int qualityLevelIndex;
        public string currentRenderPipeline = "";
        public string defaultRenderPipeline = "";
        public string qualityRenderPipeline = "";
        public List<H8KV> packageVersions = new List<H8KV>();
        public List<string> buildScenes = new List<string>();
        public string activeScene = "";
        public List<string> loadedScenes = new List<string>();
    }

    // ── Scene Info ────────────────────────────────────────────────────────────

    [Serializable]
    public class H8SceneInfo
    {
        public string name = "";
        public string path = "";
        public int buildIndex = -1;
        public bool isLoaded;
        public bool isDirty;
        public int rootCount;
        public int totalGameObjects;
        public int activeGameObjects;
        public int inactiveGameObjects;
        public int cameraCount;
        public int rendererCount;
        public int terrainCount;
        public int lightCount;
    }

    // ── Parent Chain Entry ────────────────────────────────────────────────────

    [Serializable]
    public class H8ParentInfo
    {
        public string name = "";
        public bool activeSelf;
        public int layer;

        public H8ParentInfo() { }
        public H8ParentInfo(string n, bool a, int l) { name = n ?? ""; activeSelf = a; layer = l; }
    }

    // ── Component Info ────────────────────────────────────────────────────────

    [Serializable]
    public class H8ComponentInfo
    {
        public string typeName = "";
        public bool isBehaviour;
        public bool enabled;
        public List<H8KV> reflectedFields = new List<H8KV>();
    }

    // ── Key Object Info ───────────────────────────────────────────────────────

    [Serializable]
    public class H8KeyObjectInfo
    {
        public bool exists;
        public string searchKey = "";
        public string objectName = "";
        public string hierarchyPath = "";
        public string sceneName = "";
        public bool activeSelf;
        public bool activeInHierarchy;
        public List<H8ParentInfo> parentChain = new List<H8ParentInfo>();
        public int layerIndex;
        public string layerName = "";
        public string tag = "";
        public List<H8ComponentInfo> components = new List<H8ComponentInfo>();
    }

    // ── Bootstrap Info ────────────────────────────────────────────────────────

    [Serializable]
    public class H8BootstrapInfo
    {
        public bool bootstrapperFound;
        public int instanceCount;
        public bool isBootstrapScene;
        public string activeSceneName = "";
        public int activeSceneBuildIndex = -1;
        public List<H8KV> staticFields = new List<H8KV>();
        public List<H8KV> instanceFields = new List<H8KV>();
        public string inferredState = "UNKNOWN";
    }

    // ── Registry Slot ─────────────────────────────────────────────────────────

    [Serializable]
    public class H8RegistrySlotInfo
    {
        public string slotName = "";
        public bool isNull;
        public string typeName = "";
        public string objectName = "";
        public bool isActiveIfUnityObject;
        public bool memberFound;
        public string memberName = "";
        public string memberKind = "";

        public H8RegistrySlotInfo() { }
        public H8RegistrySlotInfo(string name, bool n, string t, string o, bool a, bool mf, string mn, string mk)
        {
            slotName = name ?? ""; isNull = n; typeName = t ?? ""; objectName = o ?? ""; isActiveIfUnityObject = a;
            memberFound = mf; memberName = mn ?? ""; memberKind = mk ?? "";
        }
    }

    // ── Registry Info ─────────────────────────────────────────────────────────

    [Serializable]
    public class H8RegistryInfo
    {
        public bool typeFound;
        public string typeName = "";
        public int registryPhase = -1;
        public string registryPhaseName = "";
        public List<H8KV> staticFields = new List<H8KV>();
        public List<H8KV> candidateStaticMembers = new List<H8KV>();
        public List<H8RegistrySlotInfo> slots = new List<H8RegistrySlotInfo>();
        public string inferredState = "UNKNOWN";
    }

    // ── MapMagic Info ─────────────────────────────────────────────────────────

    [Serializable]
    public class H8MapMagicInfo
    {
        public bool mapMagicObjectFound;
        public bool mapMagicObjectActive;
        public bool mapMagicObjectActiveInHierarchy;
        public bool runtimeBridgeFound;
        public bool runtimeBridgeActive;
        public bool runtimeBridgeEnabled;
        public bool graphAssigned;
        public string graphAssetName = "";
        public string graphAssetPath = "";
        public int activeTerrainCount;
        public int allTerrainCount;
        public List<H8KV> terrainDetails = new List<H8KV>();
        public List<H8KV> reflectedFields = new List<H8KV>();
        public bool registeredInGlobalRegistry;
    }

    // ── Crest Info ────────────────────────────────────────────────────────────

    [Serializable]
    public class H8CrestInfo
    {
        public bool oceanCrestObjectFound;
        public bool oceanCrestActive;
        public bool oceanCrestActiveInHierarchy;
        public string oceanCrestHierarchyPath = "";
        public bool oceanRendererFound;
        public bool oceanRendererActive;
        public bool oceanRendererEnabled;
        public bool viewCameraAssigned;
        public bool viewpointAssigned;
        public bool primaryLightAssigned;
        public bool adapterFound;
        public bool adapterActive;
        public bool adapterEnabled;
        public bool kinematicsRegistered;
        public bool underwaterRendererFound;
        public bool underwaterRendererEnabled;
        public string underwaterRendererCameraName = "";
        public List<H8KV> reflectedFields = new List<H8KV>();
    }

    // ── Atmosphere Info ───────────────────────────────────────────────────────

    [Serializable]
    public class H8AtmosphereInfo
    {
        public bool atmosphereManagerFound;
        public bool atmosphereManagerActive;
        public bool atmosphereManagerEnabled;
        public bool celestialEngineFound;
        public bool celestialEngineActive;
        public bool celestialEngineEnabled;
        public bool atmosphereRegistered;
        public bool celestialRegistered;
        public string skyboxMaterialName = "";
        public bool skyboxAssigned;
        public string sunName = "";
        public bool sunAssigned;
        public bool fogEnabled;
        public string fogColor = "";
        public string fogMode = "";
        public int directionalLightCount;
        public List<H8KV> renderSettings = new List<H8KV>();
        public List<H8KV> shaderGlobals = new List<H8KV>();
        public List<H8KV> reflectedFields = new List<H8KV>();
    }

    // ── URP Feature Info ──────────────────────────────────────────────────────

    [Serializable]
    public class H8UrpFeatureInfo
    {
        public string name = "";
        public string typeName = "";
        public bool isActive;

        public H8UrpFeatureInfo() { }
        public H8UrpFeatureInfo(string n, string t, bool a) { name = n ?? ""; typeName = t ?? ""; isActive = a; }
    }

    // ── URP Info ──────────────────────────────────────────────────────────────

    [Serializable]
    public class H8UrpInfo
    {
        public string currentRenderPipelineAsset = "";
        public string defaultRenderPipelineAsset = "";
        public string qualityRenderPipelineAsset = "";
        public string activeUrpAssetName = "";
        public string activeRendererDataName = "";
        public List<H8UrpFeatureInfo> rendererFeatures = new List<H8UrpFeatureInfo>();
        public List<H8KV> urpSettings = new List<H8KV>();
    }

    // ── Camera Info ───────────────────────────────────────────────────────────

    [Serializable]
    public class H8CameraInfo
    {
        public string name = "";
        public string hierarchyPath = "";
        public bool activeSelf;
        public bool activeInHierarchy;
        public bool enabled;
        public string tag = "";
        public bool isMainCamera;
        public string clearFlags = "";
        public int cullingMask;
        public List<string> culledLayerNames = new List<string>();
        public List<string> visibleLayerNames = new List<string>();
        public float nearClip;
        public float farClip;
        public float fieldOfView;
        public bool orthographic;
        public float depth;
        public bool hasTargetTexture;
        public string targetTextureName = "";
        public string position = "";
        public string rotation = "";
        public bool hasUrpAdditionalData;
        public bool hasCinemachineBrain;
        public bool hasUnderwaterRenderer;
        public List<H8KV> urpData = new List<H8KV>();
    }

    // ── Console Entry ─────────────────────────────────────────────────────────

    [Serializable]
    public class H8ConsoleEntry
    {
        public string type = "";
        public string message = "";
        public string stackTrace = "";
        public int count = 1;
        public string category = "";
    }

    // ── Console Info ──────────────────────────────────────────────────────────

    [Serializable]
    public class H8ConsoleInfo
    {
        public int totalErrors;
        public int totalWarnings;
        public int totalLogs;
        public List<H8ConsoleEntry> entries = new List<H8ConsoleEntry>();
        public string editorLogTail = "";
    }

    // ── Git Info ──────────────────────────────────────────────────────────────

    [Serializable]
    public class H8GitInfo
    {
        public bool gitAvailable;
        public string branch = "";
        public List<string> modifiedFiles = new List<string>();
        public List<H8KV> targetedDiffs = new List<H8KV>();
    }

    // ── Finding ───────────────────────────────────────────────────────────────

    [Serializable]
    public class H8Finding
    {
        public string id = "";
        public string severity = "Info";
        public string category = "";
        public string title = "";
        public string evidence = "";
        public string measuredValue = "";
        public string whyItMatters = "";
        public int confidence;
        public string nextCheck = "";
        public string likelyFix = "";

        public H8Finding() { }

        public static H8Finding Create(string id, H8Severity sev, H8FindingCategory cat,
            string title, string evidence, string measuredValue, string why,
            int confidence, string nextCheck, string likelyFix)
        {
            return new H8Finding
            {
                id = id ?? "",
                severity = sev.ToString(),
                category = cat.ToString(),
                title = title ?? "",
                evidence = evidence ?? "",
                measuredValue = measuredValue ?? "",
                whyItMatters = why ?? "",
                confidence = confidence,
                nextCheck = nextCheck ?? "",
                likelyFix = likelyFix ?? ""
            };
        }
    }

    // ── Options ───────────────────────────────────────────────────────────────

    [Serializable]
    public class H8DiagnosticOptions
    {
        public bool includeInactiveObjects = true;
        public bool includeReflectionDump = true;
        public bool includeConsoleLog = true;
        public bool includeEditorLogTail = true;
        public bool includeGitDiff = true;
        public bool includePlayModeDiff = true;
        public float playModeWaitSeconds = 5f;
        public float bootstrapPlayModeWaitSeconds = 15f;
        public int maxObjectsPerSection = 200;
        public int maxTextLengthPerValue = 2000;
    }

    // ── Full Diagnostic Snapshot ──────────────────────────────────────────────

    [Serializable]
    public class H8DiagnosticSnapshot
    {
        public string timestamp = "";
        public string mode = "";
        public string sceneName = "";
        public float playModeElapsedSeconds;
        public H8ProjectMetadata project = new H8ProjectMetadata();
        public List<H8SceneInfo> scenes = new List<H8SceneInfo>();
        public List<H8KeyObjectInfo> keyObjects = new List<H8KeyObjectInfo>();
        public H8BootstrapInfo bootstrap = new H8BootstrapInfo();
        public H8RegistryInfo registry = new H8RegistryInfo();
        public H8MapMagicInfo mapMagic = new H8MapMagicInfo();
        public H8CrestInfo crest = new H8CrestInfo();
        public H8AtmosphereInfo atmosphere = new H8AtmosphereInfo();
        public H8UrpInfo urp = new H8UrpInfo();
        public List<H8CameraInfo> cameras = new List<H8CameraInfo>();
        public H8ConsoleInfo console = new H8ConsoleInfo();
        public H8GitInfo git = new H8GitInfo();
    }
}
