using System.IO;
using UnityEngine;
using UnityEditor;

namespace Hecton8.Editor
{
    /// <summary>
    /// Renders the Aegir skybox material through an offscreen camera and writes one PNG so the sky can be
    /// judged by eye. The entry point is <see cref="Execute"/> and the menu path is unchanged: batch
    /// scripts bind editor tools by reflection name, so neither may be renamed.
    ///
    /// WHAT WAS WRONG - AND WHY NO ARTIFACT OF THIS TOOL HAS EVER EXISTED:
    ///
    /// * the output directory was hardcoded to
    ///   <c>C:/Users/danat/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/</c>. That is a
    ///   DIFFERENT USER'S profile. This machine's profile is <c>Admin</c>; <c>C:\Users\danat</c> does not
    ///   exist here and never has (verified 2026-07-29: <c>C:\Users</c> holds only Admin, All Users,
    ///   Default, Default User, Public). There was no <c>Directory.CreateDirectory</c> anywhere, and
    ///   <c>File.WriteAllBytes</c> does not create missing parent directories - it throws
    ///   <c>DirectoryNotFoundException</c>.
    ///
    ///   CONSEQUENCE, STATED SO NOBODY GOES LOOKING AGAIN: every run of this tool threw at the write on
    ///   line 59, was swallowed by the catch below, and exited 1. There has never been a run that produced
    ///   a readable <c>AegirSkyView.png</c> anywhere on this machine. Do not go hunting for a historical
    ///   Aegir sky capture from this tool - no such file was ever written. Any past claim that this tool
    ///   proved the sky renders was made without an artifact.
    ///
    /// * the catch logged the bare exception and exited 1. 1 is not in this instrument layer's exit
    ///   vocabulary (0 did the work / 2 exception / 3 refused-no-GPU / 4 timeout), and the message named
    ///   no artifact, so the log line did not say what had failed to be produced;
    /// * no GPU-context refusal. Under <c>-nographics</c> <c>Camera.Render</c> and <c>ReadPixels</c> return
    ///   zeros, so the tool would have written a black 1920x1080 PNG and exited 0 - a black sky reads as a
    ///   shader bug rather than as an editor launched with the wrong flags;
    /// * no artifact verification. "EncodeToPNG did not throw" was the entire proof;
    /// * the RenderTexture, the Texture2D and the SkyCam GameObject were destroyed only on the success
    ///   path, so every failure leaked all three and left <c>RenderTexture.active</c> dangling;
    /// * <c>RenderSettings.skybox</c>, <c>ambientMode</c> and <c>ambientLight</c> were overwritten and never
    ///   restored. Those are the open scene's lighting settings: running this from the menu silently
    ///   repointed the skybox of whatever scene a human had open. The five <c>_H8Aegir*</c> shader globals
    ///   leaked into the editor session the same way. Both are now saved and restored in a finally;
    /// * the missing-material branch called <c>AssetDatabase.CreateAsset</c>, writing a new material into
    ///   the shipped art tree from a test tool - see <see cref="ResolveSkyMaterial"/>.
    ///
    /// Nothing here saves an asset or a scene. The camera object is created and destroyed in the open
    /// scene, which flags it dirty; that flag is not committed by this tool.
    /// </summary>
    public static class AegirSkyTest
    {
        private const string ToolName = "AegirSkyTest";

        /// <summary>
        /// Per-tool subfolder, inside the repo, so a human can actually find the output. `static readonly`
        /// and not `const` because <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
        /// The subfolder is per-tool on purpose: <c>Logs/</c> root is already littered with loose PNGs from
        /// several tools that wrote colliding filenames into one directory and destroyed each other's
        /// evidence.
        /// </summary>
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "aegir_sky_test");

        private static readonly string OutputPath = Path.Combine(OutputDir, "AegirSkyView.png");

        private static readonly string ProvenancePath =
            Path.Combine(OutputDir, "AegirSkyView_provenance.txt");

        private const string SkyMaterialPath = "Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat";
        private const string SkyShaderName = "HECTON/Sky/Hecton_AegirSky";
        private const string BandTexturePath =
            "Assets/_Project/Art/Textures/Generated/GeminiBiomeMaterialIntake_20260607/AegirBands.png";

        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        /// <summary>Floor for "this is a real PNG and not a truncated stub". Matches ExportArraySlices.cs:36.</summary>
        private const int MinimumPngBytes = 512;

        [MenuItem("Hecton8/Tests/Aegir Sky Render")]
        public static void Execute()
        {
            // PART 4. This tool drives Camera.Render into a RenderTexture, reads it back with ReadPixels and
            // encodes a PNG - the exact call set that C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37
            // names: "compute shaders and Graphics.Blit return zeros with no GPU context". With -nographics
            // the readback is all zeros, the PNG is solid black, and a black Aegir sky is indistinguishable
            // from a real shader failure. Refuse instead of producing that.
            //
            // Unconditional Exit(3) is safe here: graphicsDeviceType is only Null under -nographics, which
            // only happens in batchmode, so this branch cannot fire in a human's interactive editor.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[AegirSkyTest] REFUSED: no GPU context, would return zeros. Remove -nographics.");
                EditorApplication.Exit(3);
                return;
            }

            // Seeded with the failure code, not 0: if any path below ever skips the assignment it must not
            // fall through to reporting success.
            int exitCode = 2;
            try
            {
                exitCode = Render();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: no Aegir sky capture was produced at {OutputPath}. {ex}");
                Exit(2);
                return;
            }

            Exit(exitCode);
        }

        /// <summary>
        /// Returns 0 only after the PNG has been written AND verified on disk AND shown to carry more than
        /// one distinct pixel value. Every "could not do the work" branch returns 2 and logs what was not
        /// produced first.
        /// </summary>
        private static int Render()
        {
            Directory.CreateDirectory(OutputDir);

            // Stale-artifact hygiene (hecton8-shaders-compute.md:43). The existence check at the end can
            // only prove this run wrote something if the previous run's PNG is gone first.
            if (!DeleteStale(OutputPath)) return 2;
            if (!DeleteStale(ProvenancePath)) return 2;

            Material skyMat = ResolveSkyMaterial(out bool materialIsTransient);
            if (skyMat == null)
            {
                // Was: fell through with skyMat still null, assigned null to RenderSettings.skybox, rendered
                // an empty frame and exited 0.
                Debug.LogError(
                    $"[{ToolName}] FAILED: no sky material at '{SkyMaterialPath}' and shader " +
                    $"'{SkyShaderName}' is not compiled into this editor, so there is nothing to render. " +
                    $"No PNG was written to {OutputPath}.");
                return 2;
            }

            if (skyMat.shader == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{SkyMaterialPath}' has a null shader, so the capture would be " +
                    $"magenta or empty. No PNG was written to {OutputPath}.");
                if (materialIsTransient) UnityEngine.Object.DestroyImmediate(skyMat);
                return 2;
            }

            // Saved so the open scene's lighting settings and the editor's global shader state survive this
            // tool. All restored in the finally below.
            Material prevSkybox = RenderSettings.skybox;
            UnityEngine.Rendering.AmbientMode prevAmbientMode = RenderSettings.ambientMode;
            Color prevAmbientLight = RenderSettings.ambientLight;
            Vector4 prevPlanet = Shader.GetGlobalVector("_H8AegirPlanetCenterRadius");
            Vector4 prevSun = Shader.GetGlobalVector("_H8AegirSunDirection");
            Vector4 prevRing = Shader.GetGlobalVector("_H8AegirRingPlaneInner");
            Vector4 prevOrbit = Shader.GetGlobalVector("_H8AegirOrbitScalars");
            float prevQuality = Shader.GetGlobalFloat("_H8GlobalQualityWeight");

            GameObject go = null;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                RenderSettings.skybox = skyMat;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;

                // Aegir global properties - values unchanged from the original on purpose. They position the
                // planet, ring and sun for the shader and are not mine to retune.
                Shader.SetGlobalVector("_H8AegirPlanetCenterRadius", new Vector4(0, 5000, 50000, 30000));
                Shader.SetGlobalVector("_H8AegirSunDirection", new Vector4(1, 0.5f, -0.5f, 0).normalized);
                Shader.SetGlobalVector("_H8AegirRingPlaneInner", new Vector4(0, 1, 0, 40000));
                Shader.SetGlobalVector("_H8AegirOrbitScalars", new Vector4(80000, 0.5f, 1.0f, 1.0f)); // quality = 1.0 (HIGH)
                Shader.SetGlobalFloat("_H8GlobalQualityWeight", 1.0f);

                go = new GameObject("SkyCam");
                Camera cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.fieldOfView = 60f;
                // Look at the planet.
                cam.transform.position = Vector3.zero;
                cam.transform.LookAt(new Vector3(0, 5000, 50000));

                rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
                tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;

                // COLD ALLOC: Color32[1920*1080] - editor-only readback probe, 4 B/px = 8 MiB -
                // owner: AegirSkyTest. Not streamed because the whole frame is needed to prove the capture
                // is not one flat colour, which is what a zeroed or skybox-less render produces.
                Color32[] pixels = tex.GetPixels32();
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: the readback returned no pixels for a " +
                        $"{CaptureWidth}x{CaptureHeight} capture. No PNG was written to {OutputPath}.");
                    return 2;
                }

                if (IsUniform(pixels, out Color32 flatColour))
                {
                    // Loud probe for the dominant silent failure: a frame that encoded and wrote fine while
                    // containing no sky at all. Checked BEFORE the write so a worthless image never lands on
                    // disk to be mistaken for evidence.
                    Debug.LogError(
                        $"[{ToolName}] FAILED: all {pixels.Length} captured pixels are the single colour " +
                        $"RGBA({flatColour.r},{flatColour.g},{flatColour.b},{flatColour.a}), so the frame " +
                        $"contains no sky, no planet and no ring. That is what a zeroed readback or an " +
                        $"unbound skybox produces, not a render worth judging. Nothing was written to " +
                        $"{OutputPath}. graphicsDeviceType={SystemInfo.graphicsDeviceType}, " +
                        $"shader='{skyMat.shader.name}'.");
                    return 2;
                }

                byte[] png = tex.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: EncodeToPNG returned {(png == null ? "null" : "0 bytes")} " +
                        $"for the {CaptureWidth}x{CaptureHeight} capture, so nothing was written to " +
                        $"{OutputPath}.");
                    return 2;
                }

                File.WriteAllBytes(OutputPath, png);

                // "EncodeToPNG did not throw" is not proof the artifact exists.
                if (!ArtifactIsPlausible(OutputPath, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: {png.Length} bytes were handed to File.WriteAllBytes without " +
                        $"throwing but the artifact is not usable - {OutputPath} {detail}.");
                    return 2;
                }

                WriteProvenance(skyMat, materialIsTransient, pixels.Length, detail);

                Debug.Log(
                    $"[{ToolName}] wrote a verified {CaptureWidth}x{CaptureHeight} Aegir sky capture to " +
                    $"{OutputPath} ({detail}) using shader '{skyMat.shader.name}'. Provenance: " +
                    $"{ProvenancePath}");
                return 0;
            }
            finally
            {
                // Every one of these leaked on any failure before. RenderSettings is restored before the
                // transient material is destroyed so the skybox slot is never left pointing at a dead object.
                RenderTexture.active = null;
                RenderSettings.skybox = prevSkybox;
                RenderSettings.ambientMode = prevAmbientMode;
                RenderSettings.ambientLight = prevAmbientLight;
                Shader.SetGlobalVector("_H8AegirPlanetCenterRadius", prevPlanet);
                Shader.SetGlobalVector("_H8AegirSunDirection", prevSun);
                Shader.SetGlobalVector("_H8AegirRingPlaneInner", prevRing);
                Shader.SetGlobalVector("_H8AegirOrbitScalars", prevOrbit);
                Shader.SetGlobalFloat("_H8GlobalQualityWeight", prevQuality);

                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (materialIsTransient && skyMat != null) UnityEngine.Object.DestroyImmediate(skyMat);
            }
        }

        /// <summary>
        /// Loads the authored sky material. If it is absent, builds an IN-MEMORY material for this capture
        /// only.
        ///
        /// The original called <c>AssetDatabase.CreateAsset</c> here, writing a brand-new material into the
        /// shipped art tree as a side effect of running a render test - on a shared working tree, without
        /// <c>SaveAssetIfDirty</c>, and with a band texture path that does not resolve
        /// (<c>AegirBands.png</c> exists nowhere in Assets, verified 2026-07-29), so the material it created
        /// would have had an empty <c>_AegirBandTex</c> slot permanently. A test tool does not author art.
        /// The material is transient now and destroyed in the caller's finally.
        ///
        /// This branch is unreachable in the current tree: <c>Hecton_AegirSky_Mat.mat</c> is present, so
        /// removing the asset write changes nothing observable today.
        /// </summary>
        private static Material ResolveSkyMaterial(out bool isTransient)
        {
            isTransient = false;

            Material authored = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (authored != null)
                return authored;

            Shader shader = Shader.Find(SkyShaderName);
            if (shader == null)
                return null;

            Debug.LogWarning(
                $"[{ToolName}] '{SkyMaterialPath}' is missing. Rendering with a TRANSIENT in-memory " +
                $"material built from '{SkyShaderName}'; no asset is written to the art tree. The capture " +
                $"will not match the authored material's property values.");

            Material transient = new Material(shader) { name = "AegirSkyTest_Transient" };
            isTransient = true;

            Texture2D bandTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BandTexturePath);
            if (bandTex != null)
            {
                transient.SetTexture("_AegirBandTex", bandTex);
            }
            else
            {
                Debug.LogWarning(
                    $"[{ToolName}] band texture '{BandTexturePath}' does not exist, so _AegirBandTex is " +
                    $"unbound and Aegir will render without its cloud bands. The capture is not a valid " +
                    $"reference for band quality.");
            }

            return transient;
        }

        /// <summary>
        /// True when every pixel is byte-identical, which is what a zeroed readback or an unbound skybox
        /// produces. Cheap early-out on the first difference.
        /// </summary>
        private static bool IsUniform(Color32[] pixels, out Color32 flatColour)
        {
            flatColour = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.r != flatColour.r || p.g != flatColour.g || p.b != flatColour.b || p.a != flatColour.a)
                    return false;
            }
            return true;
        }

        private static bool DeleteStale(string path)
        {
            if (!File.Exists(path))
                return true;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: could not delete the stale artifact '{path}', so this run could " +
                    $"not prove a fresh capture was written rather than auditing the previous run's. " +
                    $"{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Proves the file exists and is not truncated. It cannot prove the sky looks right; the uniformity
        /// probe covers "contains nothing", and visual acceptance stays with Docs/QUALITY_GATES.md.
        /// </summary>
        private static bool ArtifactIsPlausible(string path, out string detail)
        {
            if (!File.Exists(path))
            {
                detail = "does not exist on disk after the write";
                return false;
            }

            long length = new FileInfo(path).Length;
            if (length < MinimumPngBytes)
            {
                detail = $"is {length} bytes, below the {MinimumPngBytes}-byte floor for a real PNG";
                return false;
            }

            detail = $"{length} bytes";
            return true;
        }

        /// <summary>
        /// Records what the artifact actually is next to the artifact, so a reader does not have to trust a
        /// chat message about it.
        /// </summary>
        private static void WriteProvenance(
            Material skyMat, bool materialIsTransient, int pixelCount, string outputDetail)
        {
            string[] lines =
            {
                $"{ToolName} - Aegir sky capture provenance",
                $"generated (UTC): {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                $"graphicsDeviceType: {SystemInfo.graphicsDeviceType}",
                $"batchMode: {Application.isBatchMode}",
                $"material: {(materialIsTransient ? "TRANSIENT in-memory (authored asset missing)" : SkyMaterialPath)}",
                $"shader: {skyMat.shader.name}",
                $"resolution: {CaptureWidth}x{CaptureHeight} ({pixelCount} pixels read back)",
                $"output: {OutputPath} ({outputDetail})",
                "",
                "READ THIS BEFORE QUOTING THE IMAGE AS PROOF:",
                "* this is a single offscreen camera at the origin looking at the planet centre. It is not a",
                "  gameplay viewport and it carries none of the scene's post-processing or fog.",
                "* the tool proves the PNG exists, is non-trivially sized, and is not one flat colour. It does",
                "  NOT prove the sky is correct or shippable - that judgement is Docs/QUALITY_GATES.md.",
                "* RenderSettings and the _H8Aegir* shader globals are set for the capture and restored",
                "  afterwards, so this file does not describe the state of any saved scene.",
            };

            File.WriteAllLines(ProvenancePath, lines);
        }

        /// <summary>
        /// Exit codes are only read by a batch caller, and this tool also has a menu item - killing a human's
        /// editor because a material was missing is not acceptable. In batchmode isBatchMode is always true,
        /// so the caller still gets the real code. Matches H8_TerrainGPUVisualTester.cs:76,201,205.
        /// The Exit(3) refusal above is deliberately NOT guarded: it cannot be reached interactively.
        /// </summary>
        private static void Exit(int code)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }
    }
}
