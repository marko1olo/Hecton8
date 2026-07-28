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

        // ====================================================================================
        // Render. Per-run GPU resources, allocated once.
        // ====================================================================================

        private static RenderTexture _renderTexture;
        private static Texture2D _readback;
        private static Color32[] _tilePixels;
        private static byte[] _statsRgb;
        private static Color32[] _sheetPixels;
        private static int _sheetWidth;
        private static int _sheetHeight;

        private static void AllocateRunResources(int tileResolution, int rows, int columns)
        {
            int hiRes = tileResolution * Supersample;

            _renderTexture = new RenderTexture(hiRes, hiRes, 32,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "H8MP_TileRT",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
                useMipMap = false,
            };
            _renderTexture.Create();

            _readback = new Texture2D(hiRes, hiRes, TextureFormat.RGB24, false, false)
            {
                name = "H8MP_Readback",
                hideFlags = HideFlags.HideAndDontSave,
            };

            _tilePixels = new Color32[tileResolution * tileResolution];

            // Statistics run on every 2nd pixel per axis: a quarter of the data, same verdict.
            int statsSamples = ((tileResolution + 1) / 2) * ((tileResolution + 1) / 2);
            _statsRgb = new byte[statsSamples * 3];

            _sheetWidth = columns * tileResolution + (columns + 1) * SheetGutter;
            _sheetHeight = rows * tileResolution + (rows + 1) * SheetGutter;

            // COLD ALLOC: Color32[~5.4M] - ~21 MB for a 4x5 sheet of 512 px tiles - owner: GeneratedMeshPreviewRenderer
            // Not streamed: a contact sheet has to be encoded as one image, which is the entire point
            // (`preview.py:514-521` - the lead pays one image-read instead of one per view). Reused
            // across every mesh in the run rather than reallocated per asset.
            _sheetPixels = new Color32[_sheetWidth * _sheetHeight];
        }

        private static void ReleaseRunResources()
        {
            RenderTexture.active = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }

            if (_readback != null)
            {
                UnityEngine.Object.DestroyImmediate(_readback);
                _readback = null;
            }

            _tilePixels = null;
            _statsRgb = null;
            _sheetPixels = null;
        }

        /// <summary>
        /// The render call, and the one place where this project's existing capture routes disagree.
        ///
        /// `H8_ScreenshotTaker.cs:2-3` asserts "cam.Render() alone does NOT invoke URP in batchmode -
        /// SubmitRenderRequest is required", and `H8_RouteCaptureStation.cs:598-603` repeats it. But
        /// `H8_TerrainGPUVisualTester.cs:287` - the one diagnostic route in this project with
        /// demonstrably non-black output - uses a naked `s_camera.Render()` and nothing else. Both
        /// claims cannot be fully right, and neither can be settled without running the editor.
        ///
        /// So this tries `SubmitRenderRequest` first, falls back to `Camera.Render()`, and RECORDS WHICH
        /// ONE RAN into every report. If a sheet comes back black, the report already says which call
        /// produced it instead of leaving the next agent to guess.
        ///
        /// What this deliberately does NOT copy: both existing URP routes do
        /// `request.destination = RTHandles.Alloc(rt)` and then `request.destination.Release()` BEFORE
        /// reading pixels (`H8_ScreenshotTaker.cs:68,73` then `:93-94`;
        /// `H8_RouteCaptureStation.cs:610,614` then `:545-546`). In URP 0c18adc4ff89
        /// `SingleCameraRequest.destination` is a plain `RenderTexture`
        /// (`.../Runtime/UniversalRenderPipeline.cs:2701-2706`), reached through `RTHandle`'s implicit
        /// conversion - so that call is `RenderTexture.Release()`, which frees the hardware surface of
        /// the very texture the next line reads. Unity recreates a released RenderTexture on next use,
        /// with undefined contents. That is a plausible mechanical cause of a black capture, and it is
        /// STATIC ANALYSIS ONLY - the editor was never run to confirm it. This method assigns the target
        /// directly and lets the caller's `finally` own the lifetime.
        /// </summary>
        private static void RenderThroughPipeline(Camera camera, RenderTexture target, ref string renderPath)
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    renderPath = "URP_SubmitRenderRequest";
                    return;
                }
            }

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            renderPath = urpAsset != null
                ? "CameraRender_after_URP_declined_request"
                : "CameraRender_no_URP_asset_active";
        }

        /// <summary>
        /// Discards three renders before any tile is kept.
        ///
        /// Copied from `H8_TerrainGPUVisualTester.cs:350-361`, whose comment at `:115-117` names the
        /// exact hazard: "the first Render() after scene build often returns an empty frame
        /// (shaders/render graph not yet resident). Discard it."
        ///
        /// This is NOT the `EditorApplication.update` frame settling that `AGENTS.md`
        /// `[RULE] MapMagic &amp; Batchmode Graphics Protocol` requires - see the remarks on
        /// <see cref="RenderGeneratedMeshPreviews"/> for why that machinery is wrong for this tool and
        /// would in fact break it. This is one-time shader-variant and RenderGraph residency warm-up: a
        /// different problem with a different fix.
        /// </summary>
        private static void WarmUp(PreviewRig rig, UnityEngine.Bounds bounds)
        {
            AimCamera(rig, H8MeshPreviewMode.Flat, 0, bounds);
            rig.ApplyModeMaterial(rig.FlatMaterial);
            rig.EnforceIsolation(false);

            string discarded = "WARMUP";
            for (int i = 0; i < 3; i++)
                RenderThroughPipeline(rig.Camera, _renderTexture, ref discarded);
        }

        /// <summary>
        /// Points the camera for one mode and view. The scale row clamps elevation above the floor
        /// plane; see <see cref="ScaleRowMinElevation"/> for why only that row.
        /// </summary>
        private static void AimCamera(PreviewRig rig, H8MeshPreviewMode mode, int viewIndex,
                                      UnityEngine.Bounds bounds)
        {
            bool scaleRow = mode == H8MeshPreviewMode.Scale;

            Vector3 direction = ViewDirections[viewIndex];
            if (scaleRow && direction.y < ScaleRowMinElevation)
                direction.y = ScaleRowMinElevation;
            direction = direction.normalized;

            float radius = scaleRow
                ? PreviewRig.ScaleRowRadius(bounds)
                : Mathf.Max(1e-4f, bounds.extents.magnitude);

            Vector3 target = scaleRow
                ? new Vector3(0f, Mathf.Max(HumanMarkerHeight * 0.5f, bounds.size.y * 0.5f), 0f)
                : new Vector3(0f, bounds.extents.y, 0f);

            float distance = FrameDistance(radius, CameraFovDegrees, FrameMargin);

            rig.Camera.transform.position = target + direction * distance;
            rig.Camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            rig.Camera.nearClipPlane = Mathf.Max(0.001f, radius * 0.01f);
            rig.Camera.farClipPlane = distance + radius * 8f + 20f;
            rig.Camera.backgroundColor = PreviewRig.BackdropFor(mode);
        }

        /// <summary>
        /// Renders one tile, downsamples it, and MEASURES it. Leaves the downsampled pixels in
        /// <see cref="_tilePixels"/> (top-down row order) and returns the measurement.
        ///
        /// The measurement is the point, not decoration. `AGENTS.md` `[RULE] Never Trust Automated
        /// Assertions Alone` is the rule and this project has the receipts: eleven byte-identical
        /// all-black PNGs (md5 prefix `7bf59bc3a4d28b66`) accumulated under `Logs/` because nothing on
        /// the write path asked whether the frame contained anything. A harness that cannot tell a
        /// working render from an empty one manufactures proof, which is worse than no harness.
        /// </summary>
        private static void RenderAndMeasureTile(PreviewRig rig, H8MeshPreviewMode mode, int viewIndex,
                                                 UnityEngine.Bounds bounds, int tileResolution,
                                                 out H8MeshPreviewTileStats stats)
        {
            bool witnessVisible = mode == H8MeshPreviewMode.Scale;

            AimCamera(rig, mode, viewIndex, bounds);
            rig.ApplyModeMaterial(rig.MaterialFor(mode));
            rig.EnforceIsolation(witnessVisible);

            string renderPath = rig.RenderPath;
            RenderThroughPipeline(rig.Camera, _renderTexture, ref renderPath);
            rig.RenderPath = renderPath;

            int hiRes = tileResolution * Supersample;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // ReadPixels from the active target is a hard GPU sync point: it cannot return before
                // the GPU has finished writing that surface. That is what makes an explicit
                // wait-for-settled-frames loop unnecessary here.
                RenderTexture.active = _renderTexture;
                _readback.ReadPixels(new Rect(0, 0, hiRes, hiRes), 0, 0);
                _readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            // The parameterless `GetPixels32()` allocates a fresh Color32[] per tile (4 MB at 1024 px).
            // Deliberate: `GetPixelData<Color32>` would avoid the copy but only by reinterpreting the raw
            // mip buffer, which silently depends on the texture format matching Color32 byte-for-byte.
            // This is an offline editor sweep, not one of the cadences `AGENTS.md`
            // `Runtime Hot-Path Law` governs, so the certain API is worth more than the saved allocation.
            DownsampleLinear(_readback.GetPixels32(), hiRes, _tilePixels, tileResolution);

            // ReadPixels yields bottom-up rows. Everything downstream works top-down so that "row 0 is
            // the top row of the sheet" is literally true and the compositor needs no flip arithmetic.
            FlipVerticalInPlace(_tilePixels, tileResolution, tileResolution);

            stats = MeasureTile(mode, ViewNames[viewIndex], tileResolution,
                PreviewRig.BackdropFor(mode));
        }

        /// <summary>
        /// Box-filters a supersampled tile down in LINEAR light, then re-encodes to sRGB.
        ///
        /// Averaging 8-bit sRGB values directly is the obvious version and it is wrong in a way that
        /// matters here: gamma-space averaging darkens every antialiased edge by a few percent, and a
        /// dark fringe along a hard edge is indistinguishable from the missing-chamfer artefact this
        /// tool exists to detect. A 256-entry decode table makes the correct version cost nothing.
        /// </summary>
        private static void DownsampleLinear(Color32[] source, int sourceSize, Color32[] destination, int destSize)
        {
            if (_srgbToLinear == null)
            {
                _srgbToLinear = new float[256];
                for (int i = 0; i < 256; i++)
                {
                    float channel = i / 255f;
                    _srgbToLinear[i] = channel <= 0.04045f
                        ? channel / 12.92f
                        : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
                }
            }

            int block = sourceSize / destSize;
            float inverseSamples = 1f / (block * block);

            for (int y = 0; y < destSize; y++)
            {
                int sourceY = y * block;
                for (int x = 0; x < destSize; x++)
                {
                    int sourceX = x * block;
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;

                    for (int by = 0; by < block; by++)
                    {
                        int row = (sourceY + by) * sourceSize + sourceX;
                        for (int bx = 0; bx < block; bx++)
                        {
                            Color32 sample = source[row + bx];
                            r += _srgbToLinear[sample.r];
                            g += _srgbToLinear[sample.g];
                            b += _srgbToLinear[sample.b];
                        }
                    }

                    destination[y * destSize + x] = new Color32(
                        LinearToSrgbByte(r * inverseSamples),
                        LinearToSrgbByte(g * inverseSamples),
                        LinearToSrgbByte(b * inverseSamples),
                        255);
                }
            }
        }

        private static byte LinearToSrgbByte(float linear)
        {
            float encoded = linear <= 0.0031308f
                ? linear * 12.92f
                : 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;

            return (byte)Mathf.RoundToInt(Mathf.Clamp01(encoded) * 255f);
        }

        private static void FlipVerticalInPlace(Color32[] pixels, int width, int height)
        {
            int halfHeight = height / 2;
            for (int y = 0; y < halfHeight; y++)
            {
                int top = y * width;
                int bottom = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 swap = pixels[top + x];
                    pixels[top + x] = pixels[bottom + x];
                    pixels[bottom + x] = swap;
                }
            }
        }

        /// <summary>
        /// Runs the SHARED blank-frame policy over one tile.
        ///
        /// `H8_RouteCaptureStation.TryComputeFrameStatistics` (`:284`) and
        /// `H8_RouteCaptureStation.EvaluateCapture` (`:339`) are called rather than reimplemented, so the
        /// two capture routes in this project cannot drift apart on what counts as a blank frame. Its
        /// thresholds are private consts there, and copying them here would guarantee that drift.
        ///
        /// The arguments are literally true, not padding to satisfy a signature: the graphics device is
        /// present (checked before the run starts), the camera is resolved, exactly one subject renderer
        /// is in frustum, and `H8ShotCue.Machinery`/`H8ShotCue.Scale` are the two cues from
        /// `TASTE.md:403-412` that a hard-surface equipment sheet actually carries.
        /// </summary>
        private static H8MeshPreviewTileStats MeasureTile(H8MeshPreviewMode mode, string view,
                                                          int tileResolution, Color backdrop)
        {
            int written = 0;
            for (int y = 0; y < tileResolution; y += 2)
            {
                int row = y * tileResolution;
                for (int x = 0; x < tileResolution; x += 2)
                {
                    Color32 pixel = _tilePixels[row + x];
                    _statsRgb[written * 3] = pixel.r;
                    _statsRgb[written * 3 + 1] = pixel.g;
                    _statsRgb[written * 3 + 2] = pixel.b;
                    written++;
                }
            }

            int occupiedBuckets;
            float meanLuma;
            float lumaStdDev;
            H8_RouteCaptureStation.TryComputeFrameStatistics(
                _statsRgb, written, LumaHistogram, out occupiedBuckets, out meanLuma, out lumaStdDev);

            H8ShotCue cues = mode == H8MeshPreviewMode.Scale
                ? H8ShotCue.Machinery | H8ShotCue.Scale
                : H8ShotCue.Machinery;

            return new H8MeshPreviewTileStats
            {
                Mode = mode,
                View = view,
                OccupiedLumaBuckets = occupiedBuckets,
                MeanLuma = meanLuma,
                LumaStdDev = lumaStdDev,
                CornersAreBackdrop = CornersMatchBackdrop(tileResolution, backdrop),
                Verdict = H8_RouteCaptureStation.EvaluateCapture(
                    true, true, cues, occupiedBuckets, lumaStdDev, 1),
            };
        }

        /// <summary>
        /// Checks the four tile corners against the expected clear colour.
        ///
        /// The Unity translation of `preview.py:565-591`: on the Blender side, foreign geometry standing
        /// at the subject's origin produced statistics that looked entirely plausible while describing
        /// the wrong mesh. A corner that is not backdrop means something is in frame that should not be.
        /// Reported as measurement, never as an automatic reject - the scale row's grid legitimately
        /// reaches a corner at some angles.
        /// </summary>
        private static bool CornersMatchBackdrop(int tileResolution, Color backdrop)
        {
            var expected = (Color32)backdrop;
            const int Tolerance = 26;   // ~10 percent of 8-bit range; covers dithering and rim spill
            int last = tileResolution - 1;

            return CornerMatches(0, 0, tileResolution, expected, Tolerance) &&
                   CornerMatches(last, 0, tileResolution, expected, Tolerance) &&
                   CornerMatches(0, last, tileResolution, expected, Tolerance) &&
                   CornerMatches(last, last, tileResolution, expected, Tolerance);
        }

        private static bool CornerMatches(int x, int y, int tileResolution, Color32 expected, int tolerance)
        {
            Color32 actual = _tilePixels[y * tileResolution + x];
            return Mathf.Abs(actual.r - expected.r) <= tolerance &&
                   Mathf.Abs(actual.g - expected.g) <= tolerance &&
                   Mathf.Abs(actual.b - expected.b) <= tolerance;
        }

        // ====================================================================================
        // Composite. One PNG per asset.
        // ====================================================================================

        /// <summary>
        /// Clears the sheet buffer to a near-black surround so gutters read as frame, not as content.
        /// </summary>
        private static void ClearSheet()
        {
            var surround = new Color32(6, 6, 8, 255);
            for (int i = 0; i < _sheetPixels.Length; i++)
                _sheetPixels[i] = surround;
        }

        /// <summary>
        /// Blits the current <see cref="_tilePixels"/> into grid cell (row, column), both top-down.
        /// </summary>
        private static void BlitTileToSheet(int row, int column, int tileResolution)
        {
            int originX = SheetGutter + column * (tileResolution + SheetGutter);
            int originY = SheetGutter + row * (tileResolution + SheetGutter);

            for (int y = 0; y < tileResolution; y++)
            {
                int sourceRow = y * tileResolution;
                int destinationRow = (originY + y) * _sheetWidth + originX;
                Array.Copy(_tilePixels, sourceRow, _sheetPixels, destinationRow, tileResolution);
            }
        }

        /// <summary>
        /// Burns a dot code into the tile's top-left corner: N cyan dots for the row (mode) and M amber
        /// dots for the column (view), both one-based, over a dark backing bar.
        ///
        /// No glyph font. A hand-encoded bitmap font cannot be proofread without running it, and a
        /// garbled label is worse than none. Dots are unambiguous, impossible to typo into nonsense, and
        /// legible at a glance. Grid position is already the primary label - the report states the row
        /// and column order - so this is redundancy that survives a cropped or reordered sheet.
        /// `preview.py:515` declares a `label_band` parameter and never fills it; this is the gap.
        /// </summary>
        private static void BurnDotCode(int row, int column, int tileResolution)
        {
            int originX = SheetGutter + column * (tileResolution + SheetGutter);
            int originY = SheetGutter + row * (tileResolution + SheetGutter);

            int dot = Mathf.Max(6, tileResolution / 64);
            int pad = Mathf.Max(3, dot / 2);
            int barHeight = pad * 3 + dot * 2;
            int barWidth = pad * 2 + (dot + pad) * Mathf.Max(row + 1, column + 1);

            FillSheetRect(originX, originY, barWidth, barHeight, new Color32(10, 10, 12, 255));

            var rowColor = new Color32(90, 220, 235, 255);
            for (int i = 0; i <= row; i++)
                FillSheetRect(originX + pad + i * (dot + pad), originY + pad, dot, dot, rowColor);

            var columnColor = new Color32(235, 160, 45, 255);
            for (int i = 0; i <= column; i++)
                FillSheetRect(originX + pad + i * (dot + pad), originY + pad * 2 + dot, dot, dot, columnColor);
        }

        private static void FillSheetRect(int x, int y, int width, int height, Color32 color)
        {
            int endX = Mathf.Min(x + width, _sheetWidth);
            int endY = Mathf.Min(y + height, _sheetHeight);

            for (int py = Mathf.Max(0, y); py < endY; py++)
            {
                int row = py * _sheetWidth;
                for (int px = Mathf.Max(0, x); px < endX; px++)
                    _sheetPixels[row + px] = color;
            }
        }

        /// <summary>
        /// Encodes the top-down sheet buffer as a PNG. `Texture2D` pixel arrays are bottom-up, so the
        /// buffer is flipped once here rather than at every blit.
        /// </summary>
        private static void WriteSheetPng(string path)
        {
            FlipVerticalInPlace(_sheetPixels, _sheetWidth, _sheetHeight);

            var texture = new Texture2D(_sheetWidth, _sheetHeight, TextureFormat.RGB24, false, false)
            {
                name = "H8MP_Sheet",
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                texture.SetPixels32(_sheetPixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ====================================================================================
        // Measurement. This is what turns the render into evidence instead of an impression.
        // ====================================================================================

        /// <summary>
        /// Everything measurable about one mesh without opening Unity, checked against the contracts in
        /// `3dmodel.md` section 3 (vertex layout), section 7 (LOD triangle budgets), section 10
        /// (validation gates) and section 4 (hard-surface vertex-colour semantics).
        /// </summary>
        private struct MeshMeasurement
        {
            public bool Readable;
            public int VertexCount;
            public int TriangleCount;
            public int SubMeshCount;
            public string Topologies;
            public UnityEngine.Bounds Bounds;

            public bool HasNormals;
            public bool HasTangents;
            public bool HasUv0;
            public bool HasUv1;
            public bool HasColors;

            public int NonFinitePositions;
            public int DegenerateTriangles;
            public int NormalsOutOfTolerance;
            public int TangentHandednessInvalid;
            public float MinNormalLength;
            public float MaxNormalLength;

            public Vector2 Uv0Min;
            public Vector2 Uv0Max;
            public int Uv0OutsideUnitRange;

            public float[] ChannelMin;
            public float[] ChannelMax;
            public float[] ChannelMean;

            public bool BudgetClassified;
            public string AssetClass;
            public int LodLevel;
            public int MaxTriangles;
        }

        private static MeshMeasurement MeasureMesh(Mesh mesh, string assetPath)
        {
            var measurement = new MeshMeasurement
            {
                Readable = mesh.isReadable,
                VertexCount = mesh.vertexCount,
                SubMeshCount = mesh.subMeshCount,
                Bounds = mesh.bounds,

                // `HasVertexAttribute` reads the vertex-layout description, so it answers "does this
                // stream exist" without touching vertex data or requiring a readable mesh. That is the
                // question `3dmodel.md:76-87` asks, and it distinguishes an absent stream from a present
                // stream full of zeros - which a data read alone conflates.
                HasNormals = mesh.HasVertexAttribute(VertexAttribute.Normal),
                HasTangents = mesh.HasVertexAttribute(VertexAttribute.Tangent),
                HasUv0 = mesh.HasVertexAttribute(VertexAttribute.TexCoord0),
                HasUv1 = mesh.HasVertexAttribute(VertexAttribute.TexCoord1),
                HasColors = mesh.HasVertexAttribute(VertexAttribute.Color),

                MinNormalLength = float.MaxValue,
                MaxNormalLength = 0f,
                Uv0Min = new Vector2(float.MaxValue, float.MaxValue),
                Uv0Max = new Vector2(float.MinValue, float.MinValue),
                ChannelMin = new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue },
                ChannelMax = new[] { float.MinValue, float.MinValue, float.MinValue, float.MinValue },
                ChannelMean = new float[4],
            };

            var topologies = new StringBuilder(32);
            int triangles = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                MeshTopology topology = mesh.GetTopology(submesh);
                if (topologies.Length > 0)
                    topologies.Append('/');
                topologies.Append(topology.ToString());

                uint indexCount = mesh.GetIndexCount(submesh);
                if (topology == MeshTopology.Triangles)
                    triangles += (int)(indexCount / 3u);
            }

            measurement.Topologies = topologies.Length == 0 ? "none" : topologies.ToString();
            measurement.TriangleCount = triangles;

            string assetClass;
            int lodLevel;
            int maxTriangles;
            measurement.BudgetClassified = TryResolveTriangleBudget(
                assetPath, mesh.name, out assetClass, out lodLevel, out maxTriangles);
            measurement.AssetClass = assetClass;
            measurement.LodLevel = lodLevel;
            measurement.MaxTriangles = maxTriangles;

            if (!measurement.Readable)
            {
                measurement.MinNormalLength = 0f;
                return measurement;
            }

            // `Mesh.GetVertices(List<T>)` and friends, not `mesh.vertices`. `AGENTS.md`
            // `Runtime API Defaults` names that overload as the required shape for mesh CPU reads, and
            // the lists are static and reused so a 40-mesh run does not churn the heap.
            mesh.GetVertices(ScratchVertices);
            for (int i = 0; i < ScratchVertices.Count; i++)
            {
                Vector3 position = ScratchVertices[i];
                if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                    float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
                {
                    measurement.NonFinitePositions++;
                }
            }

            if (measurement.HasNormals)
            {
                mesh.GetNormals(ScratchNormals);
                for (int i = 0; i < ScratchNormals.Count; i++)
                {
                    float length = ScratchNormals[i].magnitude;
                    if (length < measurement.MinNormalLength)
                        measurement.MinNormalLength = length;
                    if (length > measurement.MaxNormalLength)
                        measurement.MaxNormalLength = length;

                    // `3dmodel.md:270`: "Normals normalized within 0.995 to 1.005 length."
                    if (Mathf.Abs(length - 1f) > 0.005f)
                        measurement.NormalsOutOfTolerance++;
                }
            }

            if (measurement.MinNormalLength > float.MaxValue * 0.5f)
                measurement.MinNormalLength = 0f;

            if (measurement.HasTangents)
            {
                mesh.GetTangents(ScratchTangents);
                for (int i = 0; i < ScratchTangents.Count; i++)
                {
                    // `3dmodel.md:271`: "Tangents normalized and finite; handedness is -1 or 1."
                    float handedness = ScratchTangents[i].w;
                    if (Mathf.Abs(Mathf.Abs(handedness) - 1f) > 0.001f)
                        measurement.TangentHandednessInvalid++;
                }
            }

            if (measurement.HasUv0)
            {
                // `3dmodel.md:90` forbids "unbounded UV island" and `:157` forbids "UV shells touching
                // atlas border without padding". The UV0 bounding box answers both cheaply: a shell that
                // runs past 0..1 is either intentionally tiling or an unwrapper that never packed, and
                // those two cases look identical in every rendered view.
                mesh.GetUVs(0, ScratchUv);
                for (int i = 0; i < ScratchUv.Count; i++)
                {
                    Vector2 uv = ScratchUv[i];
                    if (uv.x < measurement.Uv0Min.x)
                        measurement.Uv0Min.x = uv.x;
                    if (uv.y < measurement.Uv0Min.y)
                        measurement.Uv0Min.y = uv.y;
                    if (uv.x > measurement.Uv0Max.x)
                        measurement.Uv0Max.x = uv.x;
                    if (uv.y > measurement.Uv0Max.y)
                        measurement.Uv0Max.y = uv.y;

                    if (uv.x < -0.001f || uv.x > 1.001f || uv.y < -0.001f || uv.y > 1.001f)
                        measurement.Uv0OutsideUnitRange++;
                }
            }

            if (measurement.Uv0Min.x > float.MaxValue * 0.5f)
            {
                measurement.Uv0Min = Vector2.zero;
                measurement.Uv0Max = Vector2.zero;
            }

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    continue;

                mesh.GetTriangles(ScratchIndices, submesh);
                for (int i = 0; i + 2 < ScratchIndices.Count; i += 3)
                {
                    int a = ScratchIndices[i];
                    int b = ScratchIndices[i + 1];
                    int c = ScratchIndices[i + 2];
                    if (a < 0 || b < 0 || c < 0 ||
                        a >= ScratchVertices.Count || b >= ScratchVertices.Count || c >= ScratchVertices.Count)
                    {
                        measurement.DegenerateTriangles++;
                        continue;
                    }

                    // `3dmodel.md:268`: "No degenerate triangle: length(cross(b - a, c - a)) > epsilon."
                    Vector3 edge0 = ScratchVertices[b] - ScratchVertices[a];
                    Vector3 edge1 = ScratchVertices[c] - ScratchVertices[a];
                    if (Vector3.Cross(edge0, edge1).magnitude <= 1e-7f)
                        measurement.DegenerateTriangles++;
                }
            }

            if (measurement.HasColors)
                MeasureVertexColorChannels(mesh, ref measurement);

            return measurement;
        }

        /// <summary>
        /// Per-channel range of the vertex colours.
        ///
        /// This is the hard-surface translation of the highest-value diagnostic in the Blender harness.
        /// `preview.py:23-26` records that a sway gradient which collapsed to a constant "is invisible in
        /// every ordinary render, passes a presence check", and only the raw channel range exposes it.
        /// The same trap applies to `3dmodel.md:122-126`: R is edge wear, G is rust/oxidation, B is baked
        /// AO/cavity, A is emission/warning-paint. A B channel that is constant means NO ambient occlusion
        /// was baked at all - the stream exists, the mesh is valid, and the asset is missing the single
        /// piece of data that makes a chamfer read as manufactured rather than as flat shading.
        /// </summary>
        private static void MeasureVertexColorChannels(Mesh mesh, ref MeshMeasurement measurement)
        {
            mesh.GetColors(ScratchColors);
            if (ScratchColors.Count == 0)
            {
                for (int channel = 0; channel < 4; channel++)
                {
                    measurement.ChannelMin[channel] = 0f;
                    measurement.ChannelMax[channel] = 0f;
                }

                return;
            }

            var sums = new double[4];
            for (int i = 0; i < ScratchColors.Count; i++)
            {
                Color color = ScratchColors[i];
                AccumulateChannel(ref measurement, sums, 0, color.r);
                AccumulateChannel(ref measurement, sums, 1, color.g);
                AccumulateChannel(ref measurement, sums, 2, color.b);
                AccumulateChannel(ref measurement, sums, 3, color.a);
            }

            for (int channel = 0; channel < 4; channel++)
                measurement.ChannelMean[channel] = (float)(sums[channel] / ScratchColors.Count);
        }

        private static void AccumulateChannel(ref MeshMeasurement measurement, double[] sums,
                                              int channel, float value)
        {
            if (value < measurement.ChannelMin[channel])
                measurement.ChannelMin[channel] = value;
            if (value > measurement.ChannelMax[channel])
                measurement.ChannelMax[channel] = value;

            sums[channel] += value;
        }

        /// <summary>
        /// Judges a channel constant RELATIVE to the range it actually occupies.
        ///
        /// An absolute threshold repeats a bug already fixed on the Blender side
        /// (`preview.py:730-745`): a legitimately narrow band read as flat, because the compliant range
        /// for some assets is narrower than the absolute threshold. Asking "does this channel VARY across
        /// the surface" is the right question; "is its span wide" is not.
        /// </summary>
        private static bool ChannelIsConstant(float min, float max)
        {
            float span = max - min;
            float reference = Mathf.Max(Mathf.Abs(max), 1e-6f);
            return (span / reference) <= 0.02f;
        }

        // ====================================================================================
        // Report.
        // ====================================================================================

        private static string Fixed(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static void WriteReport(string path, string assetPath, Mesh mesh, string sheetFileName,
                                        MeshMeasurement measurement, List<H8MeshPreviewTileStats> tiles,
                                        PreviewRig rig, int tileResolution, int isolatedRenderers,
                                        int blankTiles, bool sheetWritten)
        {
            var builder = new StringBuilder(6144);

            builder.Append("H8 GENERATED MESH PREVIEW REPORT\n");
            builder.Append("route=Hecton8.EditorTools.Diagnostics.GeneratedMeshPreviewRenderer\n");
            builder.Append("assetPath=").Append(assetPath).Append('\n');
            builder.Append("meshName=").Append(mesh.name).Append('\n');
            builder.Append("sheetPng=").Append(sheetWritten ? sheetFileName : "<NOT WRITTEN>").Append('\n');
            builder.Append("generatedUtc=")
                .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .Append("Z\n");
            builder.Append("unityVersion=").Append(Application.unityVersion).Append('\n');
            builder.Append("graphicsDevice=").Append(SystemInfo.graphicsDeviceType).Append('\n');
            builder.Append("batchmode=").Append(Application.isBatchMode ? "true" : "false").Append('\n');
            builder.Append("renderPath=").Append(rig.RenderPath).Append('\n');
            builder.Append("renderPipeline=")
                .Append(GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.GetType().Name
                    : "<none - built-in>")
                .Append('\n');

            builder.Append("\n-- SHEET LAYOUT (position is the label) --\n");
            builder.Append("rows(top->bottom)=");
            for (int i = 0; i < tiles.Count; i += ViewNames.Length)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(tiles[i].Mode.ToString().ToUpperInvariant());
            }

            builder.Append('\n');
            builder.Append("columns(left->right)=").Append(string.Join(",", ViewNames)).Append('\n');
            builder.Append("tilePx=").Append(tileResolution.ToString(CultureInfo.InvariantCulture))
                .Append(" (rendered at ")
                .Append((tileResolution * Supersample).ToString(CultureInfo.InvariantCulture))
                .Append(" and box-downsampled in linear light)\n");
            builder.Append("dotCode=top-left of each tile: cyan dots = row index+1, amber dots = column index+1\n");
            builder.Append("cameraFovDeg=").Append(Fixed(CameraFovDegrees, "F1"))
                .Append("  frameMargin=").Append(Fixed(FrameMargin, "F2"))
                .Append("  (both CONSTANT, so two assets are directly comparable)\n");
            builder.Append("studioSmoothness=").Append(Fixed(StudioSmoothness, "F2"))
                .Append(" (roughness ").Append(Fixed(1f - StudioSmoothness, "F2"))
                .Append(") - chosen so a chamfer reads as a third tonal band; a fully rough surface cannot\n");
            builder.Append("  distinguish a beveled edge from a raw 90-degree one, which is the gate 3dmodel.md:94 exists for\n");
            if (rig.NormalsMaterial == null)
                builder.Append("normalsRow=OMITTED: ").Append(rig.NormalsUnavailableReason).Append('\n');

            builder.Append("\n-- MESH MEASUREMENT (3dmodel.md section 3) --\n");
            builder.Append("readable=").Append(measurement.Readable ? "true" : "FALSE - per-vertex checks unavailable").Append('\n');
            builder.Append("vertices=").Append(measurement.VertexCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("triangles=").Append(measurement.TriangleCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("submeshes=").Append(measurement.SubMeshCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("topology=").Append(measurement.Topologies).Append('\n');
            builder.Append("boundsCentreM=").Append(FormatVector(measurement.Bounds.center)).Append('\n');
            builder.Append("boundsExtentsM=").Append(FormatVector(measurement.Bounds.extents)).Append(" (half-extents)\n");
            builder.Append("boundsSizeM=").Append(FormatVector(measurement.Bounds.size)).Append(" (full size)\n");
            builder.Append("hasNormals=").Append(measurement.HasNormals ? "true" : "FALSE").Append('\n');
            builder.Append("hasTangents=").Append(measurement.HasTangents ? "true" : "FALSE - normal maps will be wrong (3dmodel.md:82)").Append('\n');
            builder.Append("hasUV0=").Append(measurement.HasUv0 ? "true" : "FALSE").Append('\n');
            builder.Append("hasUV1=").Append(measurement.HasUv1 ? "true" : "false").Append('\n');
            builder.Append("hasVertexColours=").Append(measurement.HasColors ? "true" : "FALSE").Append('\n');

            builder.Append("\n-- 3dmodel.md SECTION 7 TRIANGLE BUDGET --\n");
            if (measurement.BudgetClassified)
            {
                builder.Append("assetClass=").Append(measurement.AssetClass).Append('\n');
                builder.Append("lodLevel=LOD").Append(measurement.LodLevel.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("budgetMax=").Append(measurement.MaxTriangles.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("verdict=")
                    .Append(measurement.TriangleCount <= measurement.MaxTriangles
                        ? "PASS (" + measurement.TriangleCount.ToString(CultureInfo.InvariantCulture) + " <= " +
                          measurement.MaxTriangles.ToString(CultureInfo.InvariantCulture) + ")"
                        : "OVER BUDGET (" + measurement.TriangleCount.ToString(CultureInfo.InvariantCulture) + " > " +
                          measurement.MaxTriangles.ToString(CultureInfo.InvariantCulture) + ")")
                    .Append('\n');
                builder.Append("note=the budget is a hard MAXIMUM, not a target (3dmodel.md:213). Being far under\n");
                builder.Append("  it is not a pass on its own: 3dmodel.md:213 spends the saved budget on material\n");
                builder.Append("  detail, and TASTE.md:514 rejects \"boxes, tubes, blobs ... sold as final assets\".\n");
                builder.Append("  Whether this mesh has silhouette intelligence is the SHEET's question, not this number's.\n");
            }
            else
            {
                builder.Append("verdict=UNCLASSIFIED - path matches no row of the 3dmodel.md:203-211 table.\n");
                builder.Append("  No budget is asserted rather than inventing one the bible does not grant.\n");
            }

            builder.Append("\n-- 3dmodel.md SECTION 10 GEOMETRY VALIDATION --\n");
            if (measurement.Readable)
            {
                builder.Append("nonFinitePositions=").Append(measurement.NonFinitePositions.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("degenerateTriangles=").Append(measurement.DegenerateTriangles.ToString(CultureInfo.InvariantCulture)).Append(" (cross-product area <= 1e-7)\n");
                builder.Append("normalsOutOfUnitTolerance=").Append(measurement.NormalsOutOfTolerance.ToString(CultureInfo.InvariantCulture)).Append(" (|len-1| > 0.005)\n");
                builder.Append("normalLengthRange=").Append(Fixed(measurement.MinNormalLength, "F6"))
                    .Append("..").Append(Fixed(measurement.MaxNormalLength, "F6")).Append('\n');
                builder.Append("tangentHandednessInvalid=").Append(measurement.TangentHandednessInvalid.ToString(CultureInfo.InvariantCulture)).Append(" (w not +-1)\n");
                builder.Append("uv0BoundingBox=(").Append(Fixed(measurement.Uv0Min.x, "F4")).Append(", ")
                    .Append(Fixed(measurement.Uv0Min.y, "F4")).Append(") .. (")
                    .Append(Fixed(measurement.Uv0Max.x, "F4")).Append(", ")
                    .Append(Fixed(measurement.Uv0Max.y, "F4")).Append(")\n");
                builder.Append("uv0VerticesOutside0to1=").Append(measurement.Uv0OutsideUnitRange.ToString(CultureInfo.InvariantCulture))
                    .Append(" (tiling by design, or an unwrap that never packed - 3dmodel.md:90,157)\n");
            }
            else
            {
                builder.Append("SKIPPED - mesh.isReadable is false.\n");
            }

            builder.Append("\n-- VERTEX COLOUR CHANNELS (3dmodel.md:122-126 hard-surface contract) --\n");
            if (measurement.HasColors && measurement.Readable)
            {
                AppendChannel(builder, "R edge/rim wear     ", measurement, 0);
                AppendChannel(builder, "G rust/oxid/biofilm ", measurement, 1);
                AppendChannel(builder, "B baked AO/cavity   ", measurement, 2);
                AppendChannel(builder, "A emission/paint    ", measurement, 3);
                builder.Append("A CONSTANT channel means that data was never baked. The stream exists and the mesh\n");
                builder.Append("  is valid, so no presence check catches it - only this range does.\n");
            }
            else
            {
                builder.Append(measurement.HasColors
                    ? "SKIPPED - mesh.isReadable is false.\n"
                    : "ABSENT - no Color stream. 3dmodel.md:122-126 requires wear/oxidation/AO/paint masks\n  for hard-surface generated modules.\n");
            }

            builder.Append("\n-- CAPTURE ENVIRONMENT (read only; nothing was mutated) --\n");
            builder.Append("previewScene=H8MeshPreview (EditorSceneManager.NewPreviewScene; no project scene opened)\n");
            builder.Append("renderersEnabledInPreviewScene=").Append(isolatedRenderers.ToString(CultureInfo.InvariantCulture))
                .Append(" (expected 1 for form rows, 3 for the scale row)\n");
            builder.Append("meshFiltersInRun=1 (subject bound by reassignment, so two LODs cannot coexist)\n");
            builder.Append("postProcessing=false  antialiasing=None  volumeLayerMask=0  hdr=false\n");
            builder.Append("sceneFogEnabled=").Append(RenderSettings.fog ? "TRUE" : "false").Append('\n');
            if (RenderSettings.fog)
            {
                builder.Append("  WARNING: the active scene has fog on and RenderSettings is a per-scene global.\n");
                builder.Append("  It is NOT overridden here, because writing it would dirty the open scene and\n");
                builder.Append("  AGENTS.md [RULE] Sandbox Firewall Rule forbids that. Fog can wash the silhouette\n");
                builder.Append("  and flatten the studio highlight. Re-run with an empty scene loaded for a clean read.\n");
            }

            builder.Append("ambientMode=").Append(RenderSettings.ambientMode)
                .Append("  ambientIntensity=").Append(Fixed(RenderSettings.ambientIntensity, "F2")).Append('\n');
            builder.Append("note=exposure comes from three directional lights, not from ambient, and environment\n");
            builder.Append("  reflections are keyword-disabled on the flat and studio materials. A zero ambient\n");
            builder.Append("  probe therefore lowers contrast here rather than producing the black frame recorded\n");
            builder.Append("  at H8_TerrainGPUVisualTester.cs:393-395.\n");

            builder.Append("\n-- PER-TILE FRAME STATISTICS (AGENTS.md Never Trust Automated Assertions Alone) --\n");
            builder.Append("ROW           VIEW           BUCKETS/64  MEANLUMA  STDDEV   CORNERS   VERDICT\n");
            for (int i = 0; i < tiles.Count; i++)
            {
                H8MeshPreviewTileStats tile = tiles[i];
                builder.Append(tile.Mode.ToString().ToUpperInvariant().PadRight(14));
                builder.Append(tile.View.PadRight(15));
                builder.Append(tile.OccupiedLumaBuckets.ToString(CultureInfo.InvariantCulture).PadRight(12));
                builder.Append(Fixed(tile.MeanLuma, "F2").PadRight(10));
                builder.Append(Fixed(tile.LumaStdDev, "F2").PadRight(9));
                builder.Append((tile.CornersAreBackdrop ? "clean" : "FOREIGN").PadRight(10));
                builder.Append(tile.Verdict.ToString());
                builder.Append('\n');
            }

            builder.Append("blankTiles=").Append(blankTiles.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(tiles.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("CORNERS=FOREIGN means a tile corner is not the clear colour: geometry in frame that\n");
            builder.Append("  should not be there. Expected on the scale row, where the grid reaches a corner.\n");

            builder.Append("\n-- ACCEPTANCE --\n");
            builder.Append("NONE. Docs/QUALITY_GATES.md:176 - \"Raw diagnostic MCP screenshots, static reports, and\n");
            builder.Append("near-identical capture galleries can reject bad visuals only. They cannot accept visual\n");
            builder.Append("quality.\" A non-blank sheet means the frame is fit to enter the Visual Reference Parity\n");
            builder.Append("Gate. The lead's own eyes on the PNG decide; these numbers never do.\n");

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendChannel(StringBuilder builder, string label,
                                          MeshMeasurement measurement, int channel)
        {
            builder.Append(label);
            builder.Append("min=").Append(Fixed(measurement.ChannelMin[channel], "F5"));
            builder.Append("  max=").Append(Fixed(measurement.ChannelMax[channel], "F5"));
            builder.Append("  mean=").Append(Fixed(measurement.ChannelMean[channel], "F5"));
            builder.Append("  ").Append(ChannelIsConstant(
                measurement.ChannelMin[channel], measurement.ChannelMax[channel])
                ? "CONSTANT - not baked"
                : "varies");
            builder.Append('\n');
        }

        private static string FormatVector(Vector3 value)
        {
            return Fixed(value.x, "F4") + ", " + Fixed(value.y, "F4") + ", " + Fixed(value.z, "F4");
        }

        // ====================================================================================
        // Public entry points.
        // ====================================================================================

        /// <summary>
        /// Renders every `Mesh` at one asset path. Returns false if any mesh there failed to produce a
        /// sheet. <paramref name="sheetPath"/> and <paramref name="reportPath"/> describe the first mesh.
        ///
        /// Callable on its own - it builds and tears down its own rig - so a single suspect asset can be
        /// re-rendered without a full sweep. Idempotent: it overwrites its own outputs by name.
        /// </summary>
        public static bool RenderMeshPreview(string assetPath, string outputDir, int tileResolution,
                                             out string sheetPath, out string reportPath)
        {
            sheetPath = null;
            reportPath = null;

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"{Marker} RenderMeshPreview called with an empty asset path.");
                return false;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    $"{Marker} NO GRAPHICS DEVICE (graphicsDeviceType=Null). This editor was launched " +
                    "with -nographics, which AGENTS.md [RULE] MapMagic & Batchmode Graphics Protocol " +
                    "bans. No render can produce pixels and no PNG will be written.");
                return false;
            }

            var meshes = new List<Mesh>(4);
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < all.Length; i++)
            {
                Mesh mesh = all[i] as Mesh;
                if (mesh != null)
                    meshes.Add(mesh);
            }

            if (meshes.Count == 0)
            {
                Debug.LogError($"{Marker} no Mesh object at '{assetPath}'.");
                return false;
            }

            Directory.CreateDirectory(outputDir);
            AllocateRunResources(tileResolution, AllModes.Length, ViewNames.Length);

            PreviewRig rig = null;
            bool allSucceeded = true;
            try
            {
                rig = PreviewRig.Create();
                for (int i = 0; i < meshes.Count; i++)
                {
                    string producedSheet;
                    string producedReport;
                    bool ok = RenderOneMesh(rig, assetPath, meshes[i], outputDir, tileResolution,
                        out producedSheet, out producedReport);

                    if (i == 0)
                    {
                        sheetPath = producedSheet;
                        reportPath = producedReport;
                    }

                    allSucceeded &= ok;
                }
            }
            finally
            {
                if (rig != null)
                    rig.Dispose();

                ReleaseRunResources();
            }

            return allSucceeded;
        }

        /// <summary>
        /// Renders one mesh into one sheet plus one report.
        ///
        /// GATING. If every tile of the SILHOUETTE and FLAT rows is blank, the failure is the render, not
        /// the mesh, and NO PNG is written - only a `RENDER_FAILED` report naming the cause. Writing an
        /// image there is how the eleven black PNGs in `Logs/` came to exist and be graded twice. If only
        /// some tiles are blank the sheet IS written, with `BLANKTILES` in its filename so the defect is
        /// visible in a directory listing rather than only inside the report.
        /// </summary>
        private static bool RenderOneMesh(PreviewRig rig, string assetPath, Mesh mesh, string outputDir,
                                          int tileResolution, out string sheetPath, out string reportPath)
        {
            string stem = SanitizeFileStem(mesh.name);
            sheetPath = null;
            reportPath = Path.Combine(outputDir, stem + "_REPORT.txt");

            UnityEngine.Bounds bounds = rig.SetSubject(mesh);
            MeshMeasurement measurement = MeasureMesh(mesh, assetPath);

            if (bounds.size.sqrMagnitude <= 1e-12f)
            {
                File.WriteAllText(reportPath,
                    "H8 GENERATED MESH PREVIEW REPORT\nassetPath=" + assetPath + "\nmeshName=" + mesh.name +
                    "\nstatus=RENDER_REFUSED\nreason=mesh bounds are degenerate (size " +
                    FormatVector(bounds.size) + "); there is nothing to frame. 3dmodel.md:295 requires " +
                    "bounds finite with extents above 0.001 m.\n", Encoding.UTF8);

                Debug.LogError($"{Marker} REFUSED '{mesh.name}': degenerate bounds, nothing to frame.");
                return false;
            }

            rig.BuildWitness(bounds);
            WarmUp(rig, bounds);

            ClearSheet();

            var tiles = new List<H8MeshPreviewTileStats>(AllModes.Length * ViewNames.Length);
            int blankTiles = 0;
            int isolatedRenderers = 0;
            int row = 0;

            for (int modeIndex = 0; modeIndex < AllModes.Length; modeIndex++)
            {
                H8MeshPreviewMode mode = AllModes[modeIndex];
                if (mode == H8MeshPreviewMode.Normals && rig.NormalsMaterial == null)
                    continue;   // row omitted with a named reason in the report, never rendered black

                for (int viewIndex = 0; viewIndex < ViewNames.Length; viewIndex++)
                {
                    H8MeshPreviewTileStats stats;
                    RenderAndMeasureTile(rig, mode, viewIndex, bounds, tileResolution, out stats);

                    BlitTileToSheet(row, viewIndex, tileResolution);
                    BurnDotCode(row, viewIndex, tileResolution);

                    tiles.Add(stats);
                    if (stats.IsBlank)
                        blankTiles++;
                }

                isolatedRenderers = rig.EnforceIsolation(mode == H8MeshPreviewMode.Scale);
                row++;
            }

            int formTiles = 0;
            int blankFormTiles = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Mode != H8MeshPreviewMode.Silhouette && tiles[i].Mode != H8MeshPreviewMode.Flat)
                    continue;

                formTiles++;
                if (tiles[i].IsBlank)
                    blankFormTiles++;
            }

            bool renderIsBroken = formTiles > 0 && blankFormTiles == formTiles;
            bool sheetWritten = false;

            if (!renderIsBroken)
            {
                string suffix = blankTiles > 0 ? "_SHEET.BLANKTILES.png" : "_SHEET.png";
                sheetPath = Path.Combine(outputDir, stem + suffix);
                WriteSheetPng(sheetPath);
                sheetWritten = true;
            }
            else
            {
                reportPath = Path.Combine(outputDir, stem + "_RENDER_FAILED.txt");
            }

            WriteReport(reportPath, assetPath, mesh, sheetPath == null ? string.Empty : Path.GetFileName(sheetPath),
                measurement, tiles, rig, tileResolution, isolatedRenderers, blankTiles, sheetWritten);

            if (renderIsBroken)
            {
                Debug.LogError(
                    $"{Marker} RENDER FAILED for '{mesh.name}': every silhouette and flat tile is blank, " +
                    $"so the render produced nothing - this is not a verdict on the mesh. renderPath=" +
                    $"{rig.RenderPath}. NO PNG written. Report: {reportPath}");
                return false;
            }

            if (blankTiles > 0)
            {
                Debug.LogWarning(
                    $"{Marker} {blankTiles} blank tile(s) for '{mesh.name}'. Sheet written with BLANKTILES " +
                    $"in the filename. renderPath={rig.RenderPath}. Report: {reportPath}");
                return false;
            }

            Debug.Log(
                $"{Marker} OK {mesh.name} tris={measurement.TriangleCount} " +
                $"verts={measurement.VertexCount} sheet={Path.GetFileName(sheetPath)}");
            return true;
        }

        /// <summary>
        /// Batch entry point. Sweeps <see cref="DefaultTargetFolders"/>, or the semicolon-separated list
        /// in `-h8MeshPreviewFolders`, and writes one sheet plus one report per mesh into
        /// `Docs/AgentLogs/UnityMeshPreviews/`.
        ///
        /// RE-RUNNABLE AND IDEMPOTENT. Output filenames derive from the mesh name, the stale-artefact
        /// wipe runs first, and nothing is appended to. Running it twice leaves the directory in the same
        /// state as running it once.
        ///
        /// ON FRAME SETTLING - the decision, and the reasoning, because getting it wrong produces a
        /// black PNG that reads as a broken asset. `AGENTS.md`
        /// `[RULE] MapMagic &amp; Batchmode Graphics Protocol` requires "state-machine polling via
        /// EditorApplication.update to wait for stable frames ... and at least 200+ frames of complete
        /// silence before capturing". That machinery is NOT used here, and using it would break this tool.
        ///
        /// What that rule waits for is an ASYNC PRODUCER finishing: MapMagic generates terrain across
        /// frames, so `Terrain length == 9`, alphamaps loaded and active `TerrainCollider`s are
        /// conditions that become true LATER. `COMMON_SENSE.md:29-32` ("Batchmode Wait Blindness") is the
        /// same point - a headless editor does not advance itself, so you must pump it.
        ///
        /// This tool has no async producer anywhere in its chain:
        ///  - `AssetDatabase.LoadAllAssetsAtPath` is synchronous; the mesh is fully resident on return.
        ///  - the preview scene, camera, lights and witness geometry are built synchronously on the main
        ///    thread, in this call, with no import and no scene load.
        ///  - `RenderPipeline.SubmitRenderRequest` / `Camera.Render()` are blocking submits, not requests
        ///    queued for a later frame.
        ///  - `Texture2D.ReadPixels` from the active target is a hard GPU sync point: it cannot return
        ///    before the GPU has finished writing that surface.
        /// So the "settled frame" condition holds by construction, and there is nothing for a polling
        /// loop to wait for.
        ///
        /// Deferring onto `EditorApplication.update` would actively break it. `-executeMethod` returns as
        /// soon as this method returns; work parked on a later editor frame would never run before the
        /// editor exits. That is exactly why `H8_RouteCaptureStation.cs:103` documents its own usage as
        /// "no -quit (the station exits on its own)". A synchronous method plus a self-issued exit code is
        /// the correct shape for a capture with no async dependency.
        ///
        /// Two REAL readiness hazards remain, and neither is fixed by waiting frames:
        ///  1. cold shader variants and RenderGraph residency - handled by <see cref="WarmUp"/>, three
        ///     discarded renders, copied from `H8_TerrainGPUVisualTester.cs:350-361`;
        ///  2. a zero ambient SH probe - handled by `DynamicGI.UpdateEnvironment()` in
        ///     <see cref="PreviewRig.Create"/>, plus not depending on ambient for exposure at all.
        /// And because being wrong about any of this is possible, every tile is MEASURED and a run whose
        /// form rows are blank writes no PNG. The reasoning above is an argument; the gate is the check.
        /// </summary>
        [MenuItem("Tools/Hecton/Diagnostics/Render Generated Mesh Previews", priority = 250)]
        public static void RenderGeneratedMeshPreviews()
        {
            // A capture taken now would render the last successfully compiled state and be attributed to
            // current source. Same guard as `H8_RouteCaptureStation.cs:398-405`.
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError(
                    $"{Marker} ABORT scripts failed to compile. A render now would show the last " +
                    "successfully compiled state and be credited to the current source.");
                Finish(5);
                return;
            }

            string outputDir = ResolvePreviewOutputDirectory();

            int deleted;
            List<string> undeletable;
            ClearStaleArtifacts(outputDir, out deleted, out undeletable);
            Debug.Log($"{Marker} pre-run wipe: deleted {deleted} stale .png/.log/.txt from {outputDir}");

            if (undeletable.Count > 0)
            {
                Debug.LogError(
                    $"{Marker} {undeletable.Count} stale artefact(s) COULD NOT BE DELETED and may be " +
                    $"graded as this run's output: {string.Join(", ", undeletable.ToArray())}. " +
                    "Close any image viewer holding them and re-run.");
            }

            // -nographics check before anything else, because it explains every downstream failure and no
            // later reject may mask it. Mirrors `H8_RouteCaptureStation.cs:494,505-518`: manifest, no PNG.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(Path.Combine(outputDir, "_RUN_REFUSED_NO_GRAPHICS.txt"),
                    "H8 GENERATED MESH PREVIEW RUN REFUSED\n" +
                    "reason=SystemInfo.graphicsDeviceType is Null, so this editor was launched with " +
                    "-nographics.\nAGENTS.md [RULE] MapMagic & Batchmode Graphics Protocol bans -nographics " +
                    "for exactly this reason: no camera render and no compute dispatch produces pixels.\n" +
                    "action=re-launch batchmode WITHOUT -nographics.\n" +
                    "pngWritten=false - a blank image here would be indistinguishable from a real capture " +
                    "of a broken asset, and somebody would grade it.\n", Encoding.UTF8);

                Debug.LogError(
                    $"{Marker} REFUSED: no graphics device (-nographics). No PNG written. See " +
                    "_RUN_REFUSED_NO_GRAPHICS.txt");
                Finish(3);
                return;
            }

            string[] folders = ReadFoldersArg();
            int tileResolution = Mathf.Clamp(
                ReadIntArg("-h8MeshPreviewTile", DefaultTileResolution), 128, 2048);

            List<KeyValuePair<string, Mesh>> targets = DiscoverMeshTargets(folders);
            if (targets.Count == 0)
            {
                Debug.LogError(
                    $"{Marker} ABORT no Mesh assets found under: {string.Join("; ", folders)}. " +
                    "Nothing to render, so nothing is claimed.");
                Finish(5);
                return;
            }

            Debug.Log($"{Marker} {targets.Count} mesh target(s), tile {tileResolution} px, output {outputDir}");

            AllocateRunResources(tileResolution, AllModes.Length, ViewNames.Length);

            PreviewRig rig = null;
            int succeeded = 0;
            int failed = 0;

            try
            {
                rig = PreviewRig.Create();
                if (rig.NormalsMaterial == null)
                    Debug.LogWarning($"{Marker} normals row omitted: {rig.NormalsUnavailableReason}");

                for (int i = 0; i < targets.Count; i++)
                {
                    string sheet;
                    string report;
                    if (RenderOneMesh(rig, targets[i].Key, targets[i].Value, outputDir, tileResolution,
                            out sheet, out report))
                    {
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"{Marker} ABORT {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
                failed++;
            }
            finally
            {
                if (rig != null)
                    rig.Dispose();

                ReleaseRunResources();
            }

            WriteRunSummary(outputDir, folders, tileResolution, targets.Count, succeeded, failed,
                deleted, undeletable);

            Debug.Log($"{Marker} DONE clean={succeeded} flagged={failed} of {targets.Count}; dir={outputDir}");
            Finish(failed == 0 ? 0 : 4);
        }

        private static void WriteRunSummary(string outputDir, string[] folders, int tileResolution,
                                            int targetCount, int succeeded, int failed, int deleted,
                                            List<string> undeletable)
        {
            var builder = new StringBuilder(2048);
            builder.Append("H8 GENERATED MESH PREVIEW RUN SUMMARY\n");
            builder.Append("route=Hecton8.EditorTools.Diagnostics.GeneratedMeshPreviewRenderer.RenderGeneratedMeshPreviews\n");
            builder.Append("utc=").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("Z\n");
            builder.Append("unityVersion=").Append(Application.unityVersion).Append('\n');
            builder.Append("graphicsDevice=").Append(SystemInfo.graphicsDeviceType).Append('\n');
            builder.Append("batchmode=").Append(Application.isBatchMode ? "true" : "false").Append('\n');
            builder.Append("tilePx=").Append(tileResolution.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("targetFolders=").Append(string.Join("; ", folders)).Append('\n');
            builder.Append("meshTargets=").Append(targetCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("cleanSheets=").Append(succeeded.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("flagged=").Append(failed.ToString(CultureInfo.InvariantCulture))
                .Append(" (blank tiles, degenerate bounds, or render failure - see the per-mesh report)\n");
            builder.Append("staleArtefactsDeletedBeforeRun=").Append(deleted.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("staleArtefactsUndeletable=").Append(undeletable.Count.ToString(CultureInfo.InvariantCulture));
            if (undeletable.Count > 0)
                builder.Append(" -> ").Append(string.Join(", ", undeletable.ToArray()));
            builder.Append('\n');
            builder.Append("acceptance=NONE. QUALITY_GATES.md:176 - a raw diagnostic capture can reject visual\n");
            builder.Append("  quality, never accept it. A clean sheet is fit to ENTER the Visual Reference Parity\n");
            builder.Append("  Gate; the lead's own eyes on the PNG decide.\n");

            File.WriteAllText(Path.Combine(outputDir, "_RUN_SUMMARY.txt"), builder.ToString(), Encoding.UTF8);
        }

        private static void Finish(int exitCode)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static string[] ReadFoldersArg()
        {
            string raw = ReadStringArg("-h8MeshPreviewFolders", null);
            if (string.IsNullOrEmpty(raw))
                return DefaultTargetFolders;

            string[] split = raw.Split(';');
            var cleaned = new List<string>(split.Length);
            for (int i = 0; i < split.Length; i++)
            {
                string folder = split[i].Trim();
                if (folder.Length > 0)
                    cleaned.Add(folder);
            }

            return cleaned.Count == 0 ? DefaultTargetFolders : cleaned.ToArray();
        }

        // Fully qualified: `Hecton8.Environment` shadows `System.Environment` inside the `Hecton8.*`
        // namespace root and a bare `Environment` fails CS0234. Same note as
        // `H8_RouteCaptureStation.cs:863-864`.
        private static string ReadStringArg(string flag, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int ReadIntArg(string flag, int fallback)
        {
            string raw = ReadStringArg(flag, null);
            int value;
            return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }
    }
}
#endif
