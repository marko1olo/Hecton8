#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Shading modes on the contact sheet, in the order the rows appear top to bottom.
    ///
    /// The order is a reading order, not an arbitrary enum. Silhouette first because for hard-surface
    /// work the outline is the fastest honest answer to "manufactured machine or chamfered box", and
    /// `3dmodel.md:321` rejects "low-poly toy silhouettes ... unchipped cubes" on exactly that read.
    /// Studio last of the three lit modes because it is the slowest to judge and only worth the time
    /// once the silhouette has earned it.
    /// </summary>
    public enum H8MeshPreviewMode
    {
        /// <summary>Unlit near-black subject on a light field. Outline only, zero lighting help.</summary>
        Silhouette = 0,

        /// <summary>Neutral matte grey. Form and smoothing groups without specular help.</summary>
        Flat = 1,

        /// <summary>Mid-low roughness dielectric. The bevel/chamfer gate. See <see cref="StudioSmoothness"/>.</summary>
        Studio = 2,

        /// <summary>World-space normal as colour. Exposes faceting and broken smoothing groups.</summary>
        Normals = 3,

        /// <summary>Subject beside a 1 m grid and a 1.8 m human-height marker. Absolute scale read.</summary>
        Scale = 4,
    }

    /// <summary>
    /// One rendered tile, measured rather than assumed.
    ///
    /// `AGENTS.md` `[RULE] Never Trust Automated Assertions Alone`: "the presence of a screenshot file
    /// does NOT prove the interface is functional". A tile that missed the subject, or that was
    /// captured before the GPU produced anything, is a uniform field of clear colour - and it is
    /// indistinguishable from a real capture of a broken asset unless somebody measures it. These
    /// numbers are written into the text report so the lead reads the verdict off the report instead
    /// of inferring it from a picture that may be lying.
    /// </summary>
    public struct H8MeshPreviewTileStats
    {
        public H8MeshPreviewMode Mode;
        public string View;
        public int OccupiedLumaBuckets;
        public float MeanLuma;
        public float LumaStdDev;

        /// <summary>
        /// True when all four tile corners still hold the expected backdrop colour. A corner that
        /// does not is the signature of foreign geometry in frame - the failure mode that produced
        /// three false bug reports on the Blender side of this pipeline
        /// (`Tools/Blender/h8forge/preview.py:565-591`).
        /// </summary>
        public bool CornersAreBackdrop;

        /// <summary>Verdict from the shared policy in <see cref="H8_RouteCaptureStation.EvaluateCapture"/>.</summary>
        public H8CaptureVerdict Verdict;

        public bool IsBlank
        {
            get { return Verdict == H8CaptureVerdict.RejectedBlankFrame; }
        }
    }

    /// <summary>
    /// Renders Unity-native generated `.mesh` assets to multi-view contact-sheet PNGs plus a text
    /// measurement report, so generated geometry can be JUDGED.
    ///
    /// WHY THIS EXISTS. `AGENTS.md` `[REQ] Direct Media Reading` requires the lead to open captures
    /// "with its own visual modality" and states that "A visual verdict without direct image
    /// inspection is a compliance failure". For Unity-side generated meshes there was no way to
    /// produce those images. The Blender harness at `Tools/Blender/h8forge/preview.py` is the proven
    /// design for this job but it cannot read a Unity `.asset` mesh, and the two in-project capture
    /// routes are whole-scene routes: `Assets/_Project/Editor/H8_ScreenshotTaker.cs:29` opens a scene
    /// and photographs it, and `Assets/_Project/Scripts/Editor/H8_RouteCaptureStation.cs:485` captures
    /// whatever is loaded through a scene camera it resolves at `:639`. Neither can frame one mesh
    /// asset, and neither composites. This closes that gap without adding a third scene-capture route.
    ///
    /// WHAT IS REUSED, NOT REBUILT (`AGENTS.md` `[RULE] Global Lookup Before Creating Files`):
    ///  - `Hecton8.Tools.H8CaptureRunDirectory.ResolveProjectRoot()`
    ///    (`Assets/_Project/Scripts/Tools/H8_PlayModeScreenshotter.cs:50-53`) is the single sanctioned
    ///    `Application.dataPath` anchor, per `AGENTS.md` `[RULE] Relative Path Requirement`. No second
    ///    path helper is introduced here.
    ///  - `H8_RouteCaptureStation.TryComputeFrameStatistics`
    ///    (`Assets/_Project/Scripts/Editor/H8_RouteCaptureStation.cs:284`) and
    ///    `H8_RouteCaptureStation.EvaluateCapture` (`:339`) are the blank-frame instrument and the
    ///    blank-frame POLICY. Calling `EvaluateCapture` rather than copying its thresholds means the
    ///    two routes cannot drift apart on what "blank" means.
    ///  - The URP batchmode render call shape comes from `H8_ScreenshotTaker.cs:64-83`. See
    ///    <see cref="RenderThroughPipeline"/> for the one thing that shape gets wrong.
    ///
    /// NON-MUTATING. Nothing here calls `AssetDatabase.SaveAssets`, `EditorUtility.SetDirty`,
    /// `PrefabUtility.SaveAsPrefabAsset`, or `EditorSceneManager.SaveScene`, per `AGENTS.md`
    /// `[RULE] Sandbox Firewall Rule`. No scene is opened - all geometry is staged in a preview scene
    /// from `EditorSceneManager.NewPreviewScene()`, which is never serialised and cannot dirty the
    /// user's open work. Mesh assets are read only; the temporary materials, lights, camera, witness
    /// geometry and render targets are destroyed in `finally` blocks.
    ///
    /// USAGE - batchmode, WITH graphics. `-nographics` is BANNED by `AGENTS.md`
    /// `[RULE] MapMagic &amp; Batchmode Graphics Protocol` and is detected and refused at
    /// <see cref="RenderGeneratedMeshPreviews"/>:
    ///   "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode ^
    ///     -projectPath C:\hades\Hecton8 -logFile Logs\meshpreview.log ^
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.GeneratedMeshPreviewRenderer.RenderGeneratedMeshPreviews
    ///
    /// No `-quit`: this method exits the editor itself with a verdict code, the same contract
    /// `H8_RouteCaptureStation.cs:103` documents.
    /// </summary>
    public static class GeneratedMeshPreviewRenderer
    {
        private const string Marker = "[H8_MESHPREVIEW]";

        // ------------------------------------------------------------------------------------
        // Output location. Project-root relative only: AGENTS.md [RULE] Relative Path Requirement
        // bans "C:\Users\Admin\..." style anchors outright.
        // ------------------------------------------------------------------------------------

        private const string OutputFolderA = "Docs";
        private const string OutputFolderB = "AgentLogs";
        private const string OutputFolderC = "UnityMeshPreviews";

        /// <summary>
        /// Directories that are GLOBBED for meshes, never a filename list. New generator output in
        /// these folders is picked up on the next run with no edit here.
        /// </summary>
        private static readonly string[] DefaultTargetFolders =
        {
            "Assets/_Project/Art/Generated/ProductFace/Tools",
            "Assets/_Project/Art/Generated/ProductFace/PlayerSuit",
            "Assets/_Project/Art/Baked/Structures/Agent1712",
        };

        // ------------------------------------------------------------------------------------
        // Framing. These constants are the reason two sheets of different assets are comparable.
        // ------------------------------------------------------------------------------------

        private const int DefaultTileResolution = 512;

        /// <summary>
        /// Renders at 2x and box-filters down in LINEAR light. This is geometric antialiasing, not a
        /// post effect: `3dmodel.md` and `AGENTS.md` `[REQ]` both forbid presentation that flatters
        /// weak geometry, and MSAA/FXAA/TAA are all off. Averaging in linear rather than in sRGB
        /// matters because gamma-space averaging darkens every silhouette edge by a few percent, and
        /// a dark fringe on a hard edge is exactly what a missing chamfer looks like.
        /// </summary>
        private const int Supersample = 2;

        /// <summary>
        /// Fixed vertical FOV. Mirrors `preview.py:388` (38 degrees). Constant across every asset so
        /// perspective distortion is identical; an asset-dependent FOV makes two sheets incomparable.
        /// </summary>
        private const float CameraFovDegrees = 38f;

        /// <summary>
        /// Bounding-sphere padding. Mirrors `preview.py:82` (1.22). CONSTANT, which is the whole
        /// point: `preview.py:384` - "Comparing two assets is impossible if each one auto-zooms
        /// differently." A 3 m module and a 0.3 m tool occupy the same fraction of frame.
        /// </summary>
        private const float FrameMargin = 1.22f;

        // ------------------------------------------------------------------------------------
        // Materials.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// THE load-bearing constant in this file. URP `Lit` takes SMOOTHNESS, so this is
        /// `1 - roughness`; 0.66 here is `preview.py:210`'s roughness 0.34.
        ///
        /// Why not rougher: `3dmodel.md:94` bans 90-degree corners on visible metal because "PBR
        /// specular response needs that radius", and `3dmodel.md:96-101` sets the bevel width for a
        /// base module structural edge at 0.035-0.12 m. Under a fully rough material the specular
        /// lobe is so broad that the chamfer face's highlight merges with the highlights of both
        /// adjacent faces, and a beveled edge becomes pixel-for-pixel indistinguishable from a raw
        /// one. A matte-only preview therefore CANNOT run the gate `3dmodel.md` section 4 exists to
        /// enforce. At roughness 0.34 the lobe is narrow enough that a chamfer reads as a THIRD
        /// distinct tonal step between its two neighbours - so a beveled edge shows three bands and a
        /// raw edge shows two, which is a judgement the eye makes instantly.
        ///
        /// Why not smoother: below roughness ~0.10 the highlight collapses toward a mirror. It
        /// becomes a one-pixel razor that either misses a 0.035 m chamfer entirely at this framing or
        /// clips to white, and what it mostly reports is the environment rather than the geometry.
        /// 0.34 keeps the chamfer band several pixels wide at a 512 px tile.
        ///
        /// Environment reflections are disabled on this material for the same reason - see
        /// <see cref="CreateStudioMaterial"/>. A highlight that came from an ambient probe belonging
        /// to whatever scene happened to be open is not evidence about this mesh.
        /// </summary>
        private const float StudioSmoothness = 0.66f;

        private const float FlatSmoothness = 0.05f;

        private static readonly Color FlatBaseColor = new Color(0.55f, 0.55f, 0.57f, 1f);
        private static readonly Color StudioBaseColor = new Color(0.42f, 0.44f, 0.47f, 1f);
        private static readonly Color SilhouetteBaseColor = new Color(0.015f, 0.015f, 0.018f, 1f);

        /// <summary>Dark backdrop for the lit and normals modes. `preview.py:84` uses 0.045.</summary>
        private static readonly Color DarkBackdrop = new Color(0.045f, 0.045f, 0.052f, 1f);

        /// <summary>Light backdrop, silhouette mode only. Outline read needs maximum edge contrast.</summary>
        private static readonly Color LightBackdrop = new Color(0.82f, 0.83f, 0.85f, 1f);

        private static readonly Color GridColor = new Color(0.10f, 0.30f, 0.36f, 1f);
        private static readonly Color MarkerColor = new Color(0.85f, 0.42f, 0.10f, 1f);

        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

        /// <summary>
        /// World-space normal as colour, URP-compatible, already in the project at
        /// `Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorNormalDepth.shader:1`. Its fragment
        /// returns `normalWS * 0.5 + 0.5` in RGB (`:45-48`). Reused rather than authoring a second
        /// normal-visualisation shader.
        /// </summary>
        private const string NormalsShaderName = "Hidden/Hecton8/Editor/OctahedralImpostorNormalDepth";

        // ------------------------------------------------------------------------------------
        // Witness geometry (scale row only).
        // ------------------------------------------------------------------------------------

        private const float GridLineSpacing = 1.0f;
        private const float HumanMarkerHeight = 1.8f;
        private const float ScaleRowMinRadius = 2.0f;

        /// <summary>
        /// Lowest camera elevation allowed on the scale row. The witness grid is a thin plane at
        /// y = 0; the `low` view direction is deliberately BELOW the horizon (it is the shot that
        /// exposes an unfinished underside), and from below the grid would occlude the subject and
        /// photograph its own back faces. Clamping only the scale row keeps the honest low-angle shot
        /// on the three form rows, where there is no floor to get in the way.
        /// </summary>
        private const float ScaleRowMinElevation = 0.12f;

        // ------------------------------------------------------------------------------------
        // Sheet layout.
        // ------------------------------------------------------------------------------------

        private const int SheetGutter = 8;

        /// <summary>
        /// Camera directions in world space, unit vectors pointing FROM the subject TO the camera,
        /// converted from `preview.py:49-56` (Blender Z-up) to Unity Y-up by mapping
        /// `(x, y, z)_blender -> (x, z, y)_unity`. Names and geometry are kept identical so a Unity
        /// sheet and a Blender sheet of the same family are directly comparable.
        /// </summary>
        private static readonly string[] ViewNames = { "FRONT", "THREE_QUARTER", "SIDE", "LOW" };

        private static readonly Vector3[] ViewDirections =
        {
            new Vector3(0f, 0f, -1f),            // preview.py front:         (0, -1, 0)
            new Vector3(-0.82f, 0.42f, -0.82f),  // preview.py three_quarter: (-0.82, -0.82, 0.42)
            new Vector3(1f, 0f, 0f),             // preview.py side:          (1, 0, 0)
            new Vector3(-0.6f, -0.22f, -0.9f),   // preview.py low:           (-0.6, -0.9, -0.22)
        };

        private static readonly H8MeshPreviewMode[] AllModes =
        {
            H8MeshPreviewMode.Silhouette,
            H8MeshPreviewMode.Flat,
            H8MeshPreviewMode.Studio,
            H8MeshPreviewMode.Normals,
            H8MeshPreviewMode.Scale,
        };

        // ------------------------------------------------------------------------------------
        // Triangle budgets, transcribed from the table at `3dmodel.md:203-211`.
        // ------------------------------------------------------------------------------------

        private static readonly int[] SmallPropBudget = { 6000, 2000, 350 };
        private static readonly int[] BaseModuleBudget = { 15000, 5000, 700 };

        private const string ClassSmallProp = "Small prop/equipment";
        private const string ClassBaseModule = "Base module piece";
        private const string ClassUnknown = "UNCLASSIFIED";

        // ------------------------------------------------------------------------------------
        // Reused scratch. This is a once-per-run offline tool, not a hot path, but a 40-mesh run
        // renders 800 tiles and re-allocating multi-megabyte buffers 800 times is pointless churn.
        // ------------------------------------------------------------------------------------

        private static readonly List<Vector3> ScratchVertices = new List<Vector3>(65536);
        private static readonly List<Vector3> ScratchNormals = new List<Vector3>(65536);
        private static readonly List<Vector4> ScratchTangents = new List<Vector4>(65536);
        private static readonly List<Vector2> ScratchUv = new List<Vector2>(65536);
        private static readonly List<Color> ScratchColors = new List<Color>(65536);
        private static readonly List<int> ScratchIndices = new List<int>(196608);
        private static readonly int[] LumaHistogram = new int[64];

        private static float[] _srgbToLinear;

        // ====================================================================================
        // Pure helpers. No Unity object graph crosses these signatures, so they are the part of
        // this file that can be reasoned about without an editor.
        // ====================================================================================

        /// <summary>
        /// Camera distance for a subject of bounding-sphere radius <paramref name="radius"/> such that
        /// it fills `1 / margin` of the vertical frame. Mirrors `preview.py:394`.
        /// </summary>
        public static float FrameDistance(float radius, float fovDegrees, float margin)
        {
            float safeRadius = radius > 1e-4f ? radius : 1e-4f;
            float halfFov = fovDegrees * 0.5f * Mathf.Deg2Rad;
            float tan = Mathf.Tan(halfFov);
            if (tan < 1e-5f)
                tan = 1e-5f;

            return (safeRadius * margin) / tan;
        }

        /// <summary>
        /// LOD level from an asset path or mesh name. `_LOD0`/`_LOD1`/`_LOD2` win; anything else is
        /// LOD0, which is correct for this project's generated naming - `H8_A1712_Airlock_01_Mesh` is
        /// the LOD0 asset and its siblings carry the explicit `_LOD1_Mesh` / `_LOD2_Mesh` suffix.
        /// </summary>
        public static int ResolveLodLevel(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return 0;

            if (identifier.IndexOf("_LOD2", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (identifier.IndexOf("_LOD1", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            return 0;
        }

        /// <summary>
        /// Maps an asset path to a `3dmodel.md` section 7 asset class and its LOD triangle maximum.
        /// Returns false for a path the table does not cover, in which case the report says
        /// UNCLASSIFIED rather than inventing a budget the bible does not grant.
        /// </summary>
        public static bool TryResolveTriangleBudget(
            string assetPath,
            string meshName,
            out string assetClass,
            out int lodLevel,
            out int maxTriangles)
        {
            assetClass = ClassUnknown;
            maxTriangles = 0;

            string normalized = assetPath == null
                ? string.Empty
                : assetPath.Replace('\\', '/');

            lodLevel = ResolveLodLevel(meshName);
            if (lodLevel == 0)
                lodLevel = ResolveLodLevel(normalized);

            int clampedLod = lodLevel < 0 ? 0 : (lodLevel > 2 ? 2 : lodLevel);

            if (normalized.IndexOf("/ProductFace/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                assetClass = ClassSmallProp;
                maxTriangles = SmallPropBudget[clampedLod];
                return true;
            }

            if (normalized.IndexOf("/Baked/Structures/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                assetClass = ClassBaseModule;
                maxTriangles = BaseModuleBudget[clampedLod];
                return true;
            }

            return false;
        }

        /// <summary>Sanitises a mesh name into a filename stem. Pure; no culture dependency.</summary>
        public static string SanitizeFileStem(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "unnamed";

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                            (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                builder.Append(keep ? c : '_');
            }

            return builder.Length == 0 ? "unnamed" : builder.ToString();
        }

        // ====================================================================================
        // Output directory, and the pre-run wipe.
        // ====================================================================================

        /// <summary>
        /// `Docs/AgentLogs/UnityMeshPreviews`, anchored on the one sanctioned project-root helper,
        /// `Hecton8.Tools.H8CaptureRunDirectory.ResolveProjectRoot()`
        /// (`Assets/_Project/Scripts/Tools/H8_PlayModeScreenshotter.cs:50-53`), which resolves from
        /// `Application.dataPath` as `AGENTS.md` `[RULE] Relative Path Requirement` demands. Outside
        /// `Assets/`, so no `.meta` files and no import churn.
        /// </summary>
        public static string ResolvePreviewOutputDirectory()
        {
            return Path.Combine(
                Hecton8.Tools.H8CaptureRunDirectory.ResolveProjectRoot(),
                OutputFolderA, OutputFolderB, OutputFolderC);
        }

        /// <summary>
        /// PHYSICALLY deletes every `.png`, `.log` and `.txt` in the output directory before the run.
        ///
        /// `AGENTS.md` `[RULE] Atomic File Delete Rule`: "all `.png` diagnostic artifacts and `.log`
        /// files in the output directory must be physically deleted ... This prevents hallucinatory
        /// visual checks against old screenshots." That failure mode is not hypothetical in this
        /// project: eleven byte-identical 27,260-byte all-black PNGs accumulated across
        /// `Logs/Screenshots/` and four `Logs/RouteCaptures/playmode_*` directories, under names
        /// including `_ACTUAL` and `_RESCUED` that read as progress. Stale artefacts are how a black
        /// frame gets graded twice.
        ///
        /// The `.txt` reports are deleted too. A report is a claim about a PNG; leaving last run's
        /// report beside this run's image is the same hazard with extra credibility.
        ///
        /// Deletion is unconditional here, unlike `H8CaptureRunDirectory`'s fresh-directory strategy
        /// (`H8_PlayModeScreenshotter.cs:20-27`), because that strategy exists for a shared capture
        /// root several lanes write into concurrently. This directory has exactly one writer.
        ///
        /// A file that cannot be deleted - typically held open by an image viewer - is reported as an
        /// ERROR and named, not swallowed. If this run does not overwrite that exact filename, it is
        /// precisely the stale artefact the rule exists to remove.
        /// </summary>
        public static void ClearStaleArtifacts(string directory, out int deleted, out List<string> undeletable)
        {
            deleted = 0;
            undeletable = new List<string>();

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                return;
            }

            string[] entries = Directory.GetFiles(directory);
            for (int i = 0; i < entries.Length; i++)
            {
                string extension = Path.GetExtension(entries[i]);
                bool target =
                    string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);

                if (!target)
                    continue;

                try
                {
                    File.SetAttributes(entries[i], FileAttributes.Normal);
                    File.Delete(entries[i]);
                    deleted++;
                }
                catch (IOException)
                {
                    undeletable.Add(Path.GetFileName(entries[i]));
                }
                catch (UnauthorizedAccessException)
                {
                    undeletable.Add(Path.GetFileName(entries[i]));
                }
            }
        }

        // ====================================================================================
        // Target discovery. Globbed, never a filename list.
        // ====================================================================================

        /// <summary>
        /// Every `Mesh` object under the given folders, ordered deterministically by asset path then
        /// mesh name so two runs produce the same sheet set in the same order.
        ///
        /// Filtered by TYPE, not by filename, which is why the `Agent1712` folder yields its
        /// `_Mesh`, `_LOD1_Mesh` and `_LOD2_Mesh` assets and silently skips the `_Buildable` and
        /// `_Template` ScriptableObjects sitting beside them. `LoadAllAssetsAtPath` rather than
        /// `LoadAssetAtPath&lt;Mesh&gt;` so an asset holding several meshes is fully enumerated.
        /// </summary>
        public static List<KeyValuePair<string, Mesh>> DiscoverMeshTargets(string[] folders)
        {
            var found = new List<KeyValuePair<string, Mesh>>(64);
            if (folders == null || folders.Length == 0)
                return found;

            var validFolders = new List<string>(folders.Length);
            for (int i = 0; i < folders.Length; i++)
            {
                string folder = folders[i];
                if (string.IsNullOrEmpty(folder))
                    continue;

                folder = folder.Replace('\\', '/').TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"{Marker} target folder does not exist, skipped: {folder}");
                    continue;
                }

                validFolders.Add(folder);
            }

            if (validFolders.Count == 0)
                return found;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", validFolders.ToArray());
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
                    continue;

                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int j = 0; j < all.Length; j++)
                {
                    Mesh mesh = all[j] as Mesh;
                    if (mesh != null)
                        found.Add(new KeyValuePair<string, Mesh>(path, mesh));
                }
            }

            found.Sort(CompareTargets);
            return found;
        }

        private static int CompareTargets(KeyValuePair<string, Mesh> a, KeyValuePair<string, Mesh> b)
        {
            int byPath = string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            if (byPath != 0)
                return byPath;

            string an = a.Value != null ? a.Value.name : string.Empty;
            string bn = b.Value != null ? b.Value.name : string.Empty;
            return string.Compare(an, bn, StringComparison.Ordinal);
        }

        // ====================================================================================
        // The rig. One preview scene, one camera, three lights, ONE MeshFilter for the whole run.
        // ====================================================================================

        /// <summary>
        /// Everything temporary this tool creates, with a single owner and a single teardown.
        ///
        /// SUBJECT ISOLATION, which is the defect this class is shaped around. On the Blender side,
        /// leaving sibling LOD objects in the scene "caused three separate false bug reports" because
        /// "the camera photographed several meshes stacked at the same position"
        /// (`Tools/Blender/h8forge/preview.py:565-591`) - every channel then measured plausible
        /// numbers that described the wrong geometry. The module targets here have exactly that shape:
        /// `H8_A1712_Airlock_01_Mesh`, `_LOD1_Mesh` and `_LOD2_Mesh` are three separate assets in one
        /// directory, all authored around the same origin.
        ///
        /// Three independent guarantees, not one:
        ///  1. There is exactly ONE <see cref="MeshFilter"/> in the entire run. Meshes are processed
        ///     sequentially by reassigning <see cref="MeshFilter.sharedMesh"/>, so two LODs can never
        ///     coexist in the scene. Sibling stacking is structurally impossible, not merely avoided.
        ///  2. The camera's culling scene is the preview scene (`Camera.scene`, public API - see
        ///     `Library/PackageCache/com.unity.xr.arfoundation@13d2457b468b/Runtime/Simulation/Subsystems/CameraTextureProvider.cs:299,314`).
        ///     Nothing in the user's open scene can be photographed at all, so the currently loaded
        ///     world cannot leak into a tool render.
        ///  3. <see cref="EnforceIsolation"/> walks the preview scene before every render and disables
        ///     the `enabled` flag on any renderer that is not the intended set - visibility, not
        ///     position, exactly as `preview.py:580` uses `hide_render` rather than moving objects.
        ///     Guard 1 should make this unreachable; it exists so a future edit that breaks guard 1
        ///     fails loudly instead of quietly producing plausible numbers.
        ///
        /// The preview scene comes from `EditorSceneManager.NewPreviewScene()`. It is never serialised
        /// and cannot dirty the user's open work, which matters because the lead holds the Unity lock
        /// and may have unsaved edits: `AGENTS.md` `[RULE] Sandbox Firewall Rule` forbids automation
        /// touching production scene state, and `H8_RouteCaptureStation.cs:459-471` had to REFUSE to
        /// open a scene for exactly this reason. This tool opens none.
        /// </summary>
        private sealed class PreviewRig : IDisposable
        {
            public UnityEngine.SceneManagement.Scene Scene;
            public Camera Camera;
            public UniversalAdditionalCameraData Urp;

            public MeshFilter Filter;
            public MeshRenderer SubjectRenderer;
            private GameObject _subjectRoot;

            private GameObject _gridObject;
            private GameObject _markerObject;
            public MeshRenderer GridRenderer;
            public MeshRenderer MarkerRenderer;
            private Mesh _gridMesh;
            private Mesh _markerMesh;

            public Material FlatMaterial;
            public Material StudioMaterial;
            public Material SilhouetteMaterial;
            public Material NormalsMaterial;
            public string NormalsUnavailableReason = string.Empty;
            private Material _gridMaterial;
            private Material _markerMaterial;

            /// <summary>Which render call actually ran. Written into every report - see <see cref="RenderThroughPipeline"/>.</summary>
            public string RenderPath = "UNSET";

            public static PreviewRig Create()
            {
                var rig = new PreviewRig();
                rig.Scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                rig.Scene.name = "H8MeshPreview";

                rig.BuildCamera();
                rig.BuildLights();
                rig.BuildSubjectHolder();
                rig.BuildMaterials();

                // Populate the ambient SH probe. `H8_TerrainGPUVisualTester.cs:393-395` records the
                // exact failure this prevents: "without this a hand-built batchmode scene has a zero
                // ambient probe and URP GI contributes nothing (pure black terrain)". That harness is
                // the one route in this project with demonstrably non-black output, and a zero ambient
                // probe is a prime suspect for the eleven all-black captures in `Logs/`.
                //
                // This tool does not RELY on ambient - the three directional lights below carry the
                // whole exposure, and `_ENVIRONMENTREFLECTIONS_OFF` removes ambient specular from the
                // studio material entirely - so a zero probe degrades contrast rather than producing
                // black. Calling this anyway costs nothing and closes the known hole. Unlike
                // `H8_TerrainGPUVisualTester.cs:381-391`, nothing here WRITES `RenderSettings`:
                // that would dirty the user's open scene, which the Sandbox Firewall forbids.
                DynamicGI.UpdateEnvironment();
                return rig;
            }

            private void BuildCamera()
            {
                var cameraObject = new GameObject("H8MP_Camera");
                MoveIntoPreviewScene(cameraObject);

                Camera = cameraObject.AddComponent<Camera>();
                Camera.scene = Scene;                       // guard 2: culling restricted to the rig
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = DarkBackdrop;
                Camera.fieldOfView = CameraFovDegrees;
                Camera.orthographic = false;
                Camera.useOcclusionCulling = false;         // no baked occlusion data in a preview scene
                Camera.allowMSAA = false;                   // supersample + linear box filter instead
                Camera.allowHDR = false;                    // no implicit tonemap between render and read
                Camera.depthTextureMode = DepthTextureMode.None;

                Urp = cameraObject.AddComponent<UniversalAdditionalCameraData>();

                // `3dmodel.md` and `AGENTS.md` forbid using presentation to flatter weak geometry, and
                // `preview.py:463-465` makes the same call: "a diagnostic render with a filmic curve is
                // no longer a measurement". No bloom, no vignette, no grading, no tonemap, no AA.
                // Property owners: `UniversalAdditionalCameraData.cs:803` (renderPostProcessing),
                // `:813` (antialiasing), `:710` (volumeLayerMask), `:532` (renderShadows).
                Urp.renderPostProcessing = false;
                Urp.antialiasing = AntialiasingMode.None;

                // volumeLayerMask 0 means no Volume in any scene can reach this camera. The Volume
                // framework is global, not scene-scoped, so the preview scene alone would not stop the
                // open world's global volume from applying fog/exposure overrides to these tiles.
                Urp.volumeLayerMask = 0;

                // Shadows stay ON. A contact shadow is form information - it is how the scale row
                // shows the subject standing ON the grid rather than floating above it.
                Urp.renderShadows = true;
            }

            /// <summary>
            /// Directional three-point rig, transcribed from `preview.py:256-276` and converted from
            /// Blender Z-up to Unity Y-up. Directional rather than point/area lights for the reason
            /// stated there: falloff does not change with subject size, so a 0.3 m glove and a 3 m
            /// module receive identical illumination and their sheets stay comparable.
            ///
            /// Intensities preserve the reference ratio 1 : 0.27 : 0.62 (`preview.py:265-268` uses
            /// 4.2 / 1.15 / 2.6). Absolute level is set so a 0.42 albedo lands mid-frame without
            /// clipping, which keeps the studio highlight inside range instead of blowing to white.
            /// </summary>
            private void BuildLights()
            {
                CreateLight("Key", new Vector3(-0.55f, 0.65f, -0.75f), 1.60f,
                    new Color(0.98f, 0.99f, 1.00f, 1f), LightShadows.Soft);

                // Cool fill reads as bounce and keeps the shadow side legible without flattening form.
                CreateLight("Fill", new Vector3(0.80f, 0.18f, -0.35f), 0.44f,
                    new Color(0.72f, 0.80f, 0.92f, 1f), LightShadows.None);

                // Rim separates silhouette from backdrop on the dark-backdrop modes.
                CreateLight("Rim", new Vector3(0.15f, 0.50f, 0.90f), 0.99f,
                    new Color(1.00f, 0.96f, 0.90f, 1f), LightShadows.None);
            }

            private void CreateLight(string name, Vector3 fromSubjectToLight, float intensity,
                                     Color color, LightShadows shadows)
            {
                var lightObject = new GameObject("H8MP_Light_" + name);
                MoveIntoPreviewScene(lightObject);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.color = color;
                light.shadows = shadows;
                light.shadowStrength = 0.72f;
                light.cullingMask = ~0;

                // A directional light shines along its forward axis, so it must look back down the
                // vector that points from the subject towards it.
                lightObject.transform.rotation =
                    Quaternion.LookRotation(-fromSubjectToLight.normalized, Vector3.up);
            }

            private void BuildSubjectHolder()
            {
                _subjectRoot = new GameObject("H8MP_Subject");
                MoveIntoPreviewScene(_subjectRoot);

                Filter = _subjectRoot.AddComponent<MeshFilter>();
                SubjectRenderer = _subjectRoot.AddComponent<MeshRenderer>();
                SubjectRenderer.shadowCastingMode = ShadowCastingMode.On;
                SubjectRenderer.receiveShadows = true;
                SubjectRenderer.lightProbeUsage = LightProbeUsage.Off;
                SubjectRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            /// <summary>
            /// Objects are created with default flags, moved, and only then marked
            /// <see cref="HideFlags.HideAndDontSave"/>. Setting the flag first risks
            /// `MoveGameObjectToScene` rejecting a non-savable object; the ordering is free insurance.
            /// The preview scene is torn down wholesale by `ClosePreviewScene` regardless.
            /// </summary>
            private void MoveIntoPreviewScene(GameObject target)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, Scene);
                target.hideFlags = HideFlags.HideAndDontSave;
            }

            private void BuildMaterials()
            {
                Shader lit = Shader.Find(UrpLitShaderName);
                Shader unlit = Shader.Find(UrpUnlitShaderName);

                if (lit == null)
                    throw new InvalidOperationException(
                        "Shader not found: " + UrpLitShaderName +
                        ". The flat and studio modes cannot be built, so no honest preview is possible.");
                if (unlit == null)
                    throw new InvalidOperationException(
                        "Shader not found: " + UrpUnlitShaderName +
                        ". The silhouette mode and the scale witness cannot be built.");

                FlatMaterial = CreateFlatMaterial(lit);
                StudioMaterial = CreateStudioMaterial(lit);
                SilhouetteMaterial = CreateSilhouetteMaterial(unlit);
                NormalsMaterial = CreateNormalsMaterial(out NormalsUnavailableReason);

                _gridMaterial = new Material(unlit)
                {
                    name = "H8MP_GridMat",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _gridMaterial.SetColor("_BaseColor", GridColor);

                _markerMaterial = new Material(unlit)
                {
                    name = "H8MP_MarkerMat",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _markerMaterial.SetColor("_BaseColor", MarkerColor);
            }

            /// <summary>
            /// Neutral matte grey, near-zero smoothness. Silhouette and smoothing-group judgement with
            /// no texture and no specular help - `preview.py:184-195` calls the same shot, quoting
            /// `3DMODEL_FLORA_CORAL.md` section 10: "flat-material screenshot proving the silhouette is
            /// biological before texture detail". `TASTE.md:594` rejects "Generated models that read as
            /// primitive shapes after textures are disabled", and this is the row that runs that test.
            /// </summary>
            private static Material CreateFlatMaterial(Shader lit)
            {
                var material = new Material(lit)
                {
                    name = "H8MP_Flat",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                material.SetColor("_BaseColor", FlatBaseColor);
                material.SetFloat("_Smoothness", FlatSmoothness);
                material.SetFloat("_Metallic", 0f);
                DisableSpecularAndReflections(material);
                return material;
            }

            /// <summary>
            /// The bevel gate. Rationale for the smoothness value is on <see cref="StudioSmoothness"/>
            /// and it is the single most load-bearing decision in this file.
            ///
            /// Environment reflections are OFF here as well as on the flat material. That is not a
            /// copy-paste: a mid-low-roughness dielectric picks up the ambient probe strongly, so with
            /// reflections on, a highlight could come from whatever scene happened to be open when the
            /// run started. A chamfer verdict has to be attributable to the geometry and the three
            /// known lights, otherwise it is not evidence about the mesh. Specular highlights stay ON -
            /// they are the entire measurement.
            /// </summary>
            private static Material CreateStudioMaterial(Shader lit)
            {
                var material = new Material(lit)
                {
                    name = "H8MP_Studio",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                material.SetColor("_BaseColor", StudioBaseColor);
                material.SetFloat("_Smoothness", StudioSmoothness);
                material.SetFloat("_Metallic", 0f);

                // Keyword names verified in
                // `Library/PackageCache/com.unity.render-pipelines.universal@0c18adc4ff89/Shaders/Lit.shader:132-133`;
                // both are `shader_feature_local_fragment`, so they are material-local keywords and
                // setting the float alone would not take effect. Property declarations are at `:22-23`.
                material.SetFloat("_SpecularHighlights", 1f);
                material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                return material;
            }

            /// <summary>
            /// Unlit near-black on a light field. No lighting at all, so nothing about the shading can
            /// rescue a bad outline. For hard-surface work this is the fastest honest answer to
            /// "manufactured pressure equipment or a chamfered box", and it is the row that most
            /// directly runs `3dmodel.md:321` ("low-poly toy silhouettes ... unchipped cubes ... are
            /// rejected"). The Blender harness has no equivalent mode.
            ///
            /// Unlit is the correct shader rather than a black Lit material: a Lit material at zero
            /// albedo still receives a specular highlight, which would break the silhouette's edge and
            /// smuggle shading information back into a shading-free test.
            /// </summary>
            private static Material CreateSilhouetteMaterial(Shader unlit)
            {
                var material = new Material(unlit)
                {
                    name = "H8MP_Silhouette",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                material.SetColor("_BaseColor", SilhouetteBaseColor);
                return material;
            }

            /// <summary>
            /// World-space normal as colour, reusing the project's existing URP-compatible normal
            /// shader rather than authoring a second one
            /// (`Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorNormalDepth.shader:1`, fragment
            /// at `:42-49` returns `normalWS * 0.5 + 0.5`).
            ///
            /// Honest caveat, recorded here and in every report: that shader currently has no caller in
            /// the project, and its pass carries no `LightMode` tag, so URP draws it under the
            /// `SRPDefaultUnlit` tag. That is INFERRED from URP's shader-tag list, not proven by a run.
            /// If the inference is wrong the row renders empty - which is why it is gated by
            /// `shader.isSupported` here and by the per-tile blank check downstream, and why the row is
            /// OMITTED with a named reason rather than emitting black tiles that would read as a broken
            /// mesh.
            /// </summary>
            private static Material CreateNormalsMaterial(out string unavailableReason)
            {
                unavailableReason = string.Empty;

                Shader normals = Shader.Find(NormalsShaderName);
                if (normals == null)
                {
                    unavailableReason = "Shader.Find returned null for '" + NormalsShaderName + "'";
                    return null;
                }

                if (!normals.isSupported)
                {
                    unavailableReason = "'" + NormalsShaderName + "' reports isSupported=false on " +
                                        SystemInfo.graphicsDeviceType;
                    return null;
                }

                return new Material(normals)
                {
                    name = "H8MP_Normals",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            private static void DisableSpecularAndReflections(Material material)
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }

            public Material MaterialFor(H8MeshPreviewMode mode)
            {
                switch (mode)
                {
                    case H8MeshPreviewMode.Silhouette:
                        return SilhouetteMaterial;
                    case H8MeshPreviewMode.Normals:
                        return NormalsMaterial;
                    case H8MeshPreviewMode.Flat:
                        return FlatMaterial;
                    default:
                        // Scale row shares the studio material: the scale read is about size against a
                        // witness, and changing the shading between rows would add a second variable.
                        return StudioMaterial;
                }
            }

            public static Color BackdropFor(H8MeshPreviewMode mode)
            {
                return mode == H8MeshPreviewMode.Silhouette ? LightBackdrop : DarkBackdrop;
            }

            /// <summary>
            /// Binds one mesh and parks it XZ-centred on the origin, resting on y = 0.
            ///
            /// Resting on the floor plane is what makes the scale row mean anything: the 1.8 m marker
            /// and the subject then share a ground line, so "knee-high" and "taller than a person" are
            /// direct reads rather than inferences. Returns the local-space bounds so the caller can
            /// frame from them.
            /// </summary>
            public UnityEngine.Bounds SetSubject(Mesh mesh)
            {
                UnityEngine.Bounds bounds = mesh.bounds;
                Filter.sharedMesh = mesh;

                // Submesh count drives the material array: a 3-submesh mesh with one material renders
                // only submesh 0, which would silently hide two thirds of the geometry and look like a
                // generator bug. `3dmodel.md:176-181` assigns real meaning to slots 0-3, so every
                // submesh must be drawn.
                int submeshes = mesh.subMeshCount < 1 ? 1 : mesh.subMeshCount;
                var materials = new Material[submeshes];
                for (int i = 0; i < submeshes; i++)
                    materials[i] = FlatMaterial;
                SubjectRenderer.sharedMaterials = materials;

                Filter.transform.position = new Vector3(
                    -bounds.center.x, -bounds.min.y, -bounds.center.z);

                return bounds;
            }

            /// <summary>Applies one material to every submesh slot without reallocating the array.</summary>
            public void ApplyModeMaterial(Material material)
            {
                Material[] materials = SubjectRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;

                SubjectRenderer.sharedMaterials = materials;
            }

            /// <summary>
            /// Rebuilds the scale witness for one subject: a 1 m grid on the floor plane and a 1.8 m
            /// human-height marker standing clear of the subject.
            ///
            /// `3dmodel.md:18` lists "scale witnesses" as part of the product bar and `:312-321` makes
            /// scale a Visual Target property. Without one a preview cannot answer "does this read at
            /// the right scale", because constant-margin framing deliberately destroys apparent size -
            /// a 0.3 m glove and a 3 m module fill the frame identically on the form rows. The witness
            /// row is where absolute size comes back.
            ///
            /// The marker is placed OUTSIDE the subject's footprint rather than at a fixed corner, which
            /// `preview.py:343` does not do: a fixed 1.15 m offset is swallowed whole by a 3 m module,
            /// and a marker buried inside the subject reads as geometry rather than as a ruler. Its
            /// SIZE is fixed at 1.8 m, which is the part that has to stay constant for comparability.
            /// </summary>
            public void BuildWitness(UnityEngine.Bounds subjectBounds)
            {
                DestroyWitness();

                float subjectRadius = subjectBounds.extents.magnitude;
                float gridExtent = Mathf.Max(2f, Mathf.Ceil(subjectRadius + 1.2f));

                _gridMesh = BuildGridMesh(gridExtent, GridLineSpacing,
                    Mathf.Max(0.005f, gridExtent * 0.0035f));
                _gridObject = new GameObject("H8MP_Witness_Grid");
                MoveIntoPreviewScene(_gridObject);
                _gridObject.transform.position = Vector3.zero;
                _gridObject.AddComponent<MeshFilter>().sharedMesh = _gridMesh;
                GridRenderer = _gridObject.AddComponent<MeshRenderer>();
                GridRenderer.sharedMaterial = _gridMaterial;
                GridRenderer.shadowCastingMode = ShadowCastingMode.Off;
                GridRenderer.receiveShadows = true;

                _markerMesh = BuildBoxMesh(new Vector3(0.16f, HumanMarkerHeight, 0.10f));
                _markerObject = new GameObject("H8MP_Witness_Human1p8m");
                MoveIntoPreviewScene(_markerObject);

                float markerX = Mathf.Min(subjectBounds.extents.x + 0.45f, gridExtent - 0.25f);
                float markerZ = Mathf.Min(subjectBounds.extents.z + 0.45f, gridExtent - 0.25f);
                _markerObject.transform.position =
                    new Vector3(markerX, HumanMarkerHeight * 0.5f, markerZ);

                _markerObject.AddComponent<MeshFilter>().sharedMesh = _markerMesh;
                MarkerRenderer = _markerObject.AddComponent<MeshRenderer>();
                MarkerRenderer.sharedMaterial = _markerMaterial;
                MarkerRenderer.shadowCastingMode = ShadowCastingMode.On;
                MarkerRenderer.receiveShadows = true;
            }

            /// <summary>
            /// Camera framing radius for the scale row. Must contain the marker, so it can never be
            /// tighter than <see cref="ScaleRowMinRadius"/>; a 0.3 m tool then reads as genuinely small
            /// beside a person, which is the correct answer rather than a framing failure.
            /// </summary>
            public static float ScaleRowRadius(UnityEngine.Bounds subjectBounds)
            {
                return Mathf.Max(ScaleRowMinRadius, subjectBounds.extents.magnitude + 1.0f);
            }

            private void DestroyWitness()
            {
                if (_gridObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(_gridObject);
                    _gridObject = null;
                    GridRenderer = null;
                }

                if (_markerObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(_markerObject);
                    _markerObject = null;
                    MarkerRenderer = null;
                }

                DestroyMesh(ref _gridMesh);
                DestroyMesh(ref _markerMesh);
            }

            /// <summary>
            /// Thin coplanar quads on the y = 0 plane, one per grid line per axis, spaced
            /// <paramref name="spacing"/> apart. Real geometry rather than `GL.LINES` so it is lit,
            /// receives the subject's shadow, and survives any render path.
            /// </summary>
            private static Mesh BuildGridMesh(float extent, float spacing, float thickness)
            {
                var vertices = new List<Vector3>(256);
                var normals = new List<Vector3>(256);
                var indices = new List<int>(384);

                int steps = Mathf.FloorToInt((extent * 2f) / spacing) + 1;
                for (int i = 0; i < steps; i++)
                {
                    float offset = -extent + i * spacing;
                    if (offset > extent + 1e-4f)
                        break;

                    AddQuad(vertices, normals, indices,
                        new Vector3(offset - thickness, 0f, -extent),
                        new Vector3(offset + thickness, 0f, -extent),
                        new Vector3(offset + thickness, 0f, extent),
                        new Vector3(offset - thickness, 0f, extent));

                    AddQuad(vertices, normals, indices,
                        new Vector3(-extent, 0f, offset - thickness),
                        new Vector3(-extent, 0f, offset + thickness),
                        new Vector3(extent, 0f, offset + thickness),
                        new Vector3(extent, 0f, offset - thickness));
                }

                var mesh = new Mesh
                {
                    name = "H8MP_GridMesh",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(indices, 0, true);
                return mesh;
            }

            private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<int> indices,
                                       Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int baseIndex = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++)
                    normals.Add(Vector3.up);

                indices.Add(baseIndex);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 3);
                indices.Add(baseIndex + 2);
            }

            /// <summary>Axis-aligned box centred on its own origin, flat-shaded, 24 vertices.</summary>
            private static Mesh BuildBoxMesh(Vector3 size)
            {
                Vector3 h = size * 0.5f;
                var vertices = new List<Vector3>(24);
                var normals = new List<Vector3>(24);
                var indices = new List<int>(36);

                AddBoxFace(vertices, normals, indices, new Vector3(0f, 0f, -h.z), Vector3.back, new Vector3(h.x, h.y, 0f));
                AddBoxFace(vertices, normals, indices, new Vector3(0f, 0f, h.z), Vector3.forward, new Vector3(h.x, h.y, 0f));
                AddBoxFace(vertices, normals, indices, new Vector3(-h.x, 0f, 0f), Vector3.left, new Vector3(0f, h.y, h.z));
                AddBoxFace(vertices, normals, indices, new Vector3(h.x, 0f, 0f), Vector3.right, new Vector3(0f, h.y, h.z));
                AddBoxFace(vertices, normals, indices, new Vector3(0f, -h.y, 0f), Vector3.down, new Vector3(h.x, 0f, h.z));
                AddBoxFace(vertices, normals, indices, new Vector3(0f, h.y, 0f), Vector3.up, new Vector3(h.x, 0f, h.z));

                var mesh = new Mesh
                {
                    name = "H8MP_BoxMesh",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(indices, 0, true);
                return mesh;
            }

            private static void AddBoxFace(List<Vector3> vertices, List<Vector3> normals, List<int> indices,
                                           Vector3 center, Vector3 normal, Vector3 halfSpan)
            {
                // Two in-plane axes derived from the face normal, so the winding is consistent.
                Vector3 up = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
                Vector3 right = Vector3.Cross(up, normal).normalized;
                up = Vector3.Cross(normal, right).normalized;

                Vector3 rightSpan = new Vector3(right.x * halfSpan.x, right.y * halfSpan.y, right.z * halfSpan.z);
                Vector3 upSpan = new Vector3(up.x * halfSpan.x, up.y * halfSpan.y, up.z * halfSpan.z);
                if (rightSpan.sqrMagnitude < 1e-8f)
                    rightSpan = right * Mathf.Max(halfSpan.x, Mathf.Max(halfSpan.y, halfSpan.z));
                if (upSpan.sqrMagnitude < 1e-8f)
                    upSpan = up * Mathf.Max(halfSpan.x, Mathf.Max(halfSpan.y, halfSpan.z));

                AddQuadWithNormal(vertices, normals, indices,
                    center - rightSpan - upSpan,
                    center + rightSpan - upSpan,
                    center + rightSpan + upSpan,
                    center - rightSpan + upSpan,
                    normal);
            }

            private static void AddQuadWithNormal(List<Vector3> vertices, List<Vector3> normals,
                                                  List<int> indices, Vector3 a, Vector3 b, Vector3 c,
                                                  Vector3 d, Vector3 normal)
            {
                int baseIndex = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++)
                    normals.Add(normal);

                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 3);
            }

            /// <summary>
            /// Guard 3. Walks every renderer in the preview scene and enables ONLY the intended set,
            /// returning how many ended up enabled. See the class remarks for why three independent
            /// guarantees exist rather than one.
            ///
            /// Scoped to the preview scene's own roots, never `FindObjectsByType`, because disabling a
            /// renderer in the user's open scene would be exactly the production mutation
            /// `AGENTS.md` `[RULE] Sandbox Firewall Rule` forbids. The count is written into every
            /// report: if a future edit reintroduces stacked siblings, the number moves off 1 and the
            /// lead sees it on the report instead of misreading the picture.
            /// </summary>
            public int EnforceIsolation(bool witnessVisible)
            {
                int enabled = 0;
                GameObject[] roots = Scene.GetRootGameObjects();

                for (int i = 0; i < roots.Length; i++)
                {
                    Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>(true);
                    for (int j = 0; j < renderers.Length; j++)
                    {
                        Renderer renderer = renderers[j];
                        bool intended =
                            ReferenceEquals(renderer, SubjectRenderer) ||
                            (witnessVisible && ReferenceEquals(renderer, GridRenderer)) ||
                            (witnessVisible && ReferenceEquals(renderer, MarkerRenderer));

                        renderer.enabled = intended;
                        if (intended)
                            enabled++;
                    }
                }

                return enabled;
            }

            public void Dispose()
            {
                DestroyWitness();
                DestroyMaterial(ref FlatMaterial);
                DestroyMaterial(ref StudioMaterial);
                DestroyMaterial(ref SilhouetteMaterial);
                DestroyMaterial(ref NormalsMaterial);
                DestroyMaterial(ref _gridMaterial);
                DestroyMaterial(ref _markerMaterial);

                DestroyMesh(ref _gridMesh);
                DestroyMesh(ref _markerMesh);

                // The subject mesh is a PROJECT ASSET. It is only ever read, never destroyed, never
                // marked dirty, never saved. Dropping the reference is the whole cleanup.
                if (Filter != null)
                    Filter.sharedMesh = null;
                if (SubjectRenderer != null)
                    SubjectRenderer.sharedMaterial = null;

                if (Scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(Scene);

                Camera = null;
                Urp = null;
                Filter = null;
                SubjectRenderer = null;
                GridRenderer = null;
                MarkerRenderer = null;
                _subjectRoot = null;
                _gridObject = null;
                _markerObject = null;
            }

            private static void DestroyMaterial(ref Material material)
            {
                if (material == null)
                    return;

                UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }

            private static void DestroyMesh(ref Mesh mesh)
            {
                if (mesh == null)
                    return;

                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
            }
        }
    }
}
#endif
