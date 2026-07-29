using System;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Graphics.Authoring
{
    /// <summary>
    /// Single editor-side owner of the <c>visual_tuning.h8bin</c> path and of the byte-layout contract
    /// for <see cref="VisualTuningState"/>.
    ///
    /// FORMAT NOTE, measured from the bytes on 2026-07-29 rather than inferred from the extension:
    /// <c>visual_tuning.h8bin</c> is NOT an H8 container. It carries no magic, no version field, no
    /// section directory and no checksum. It is a bare <see cref="UnsafeUtility.CopyStructureToPtr"/>
    /// image of <see cref="VisualTuningState"/>, exactly <c>UnsafeUtility.SizeOf&lt;VisualTuningState&gt;()</c>
    /// bytes long. It only shares an extension with the H8DM/H8VB/H8AB containers; it shares no layout
    /// with any of them.
    ///
    /// WHY NO HEADER IS ADDED HERE: the runtime reader at
    /// Assets/_Project/Scripts/Graphics/HectonVisualsOrchestrator.cs:59-65 enforces an EXACT length match
    /// against its own <c>sizeof</c> and throws <c>DataCorruptionException</c> out of <c>Awake()</c> on any
    /// mismatch. Prefixing a magic/version/checksum from the writer side alone would therefore make every
    /// existing artifact throw at load. Adding a real header is a coordinated two-file change and that
    /// reader is not owned by this lane.
    ///
    /// WHY THIS GUARD EXISTS: the reader's exact-length check catches a change to the NUMBER of fields in
    /// <see cref="VisualTuningState"/>, but it is completely blind to a change in their ORDER. Swapping any
    /// two same-width fields keeps the struct at 64 bytes, so the reader accepts the file and applies
    /// transposed values - <c>PlanetCenterRadius</c> receiving a sun intensity, an ocean scatter colour
    /// receiving a sun colour. <c>HectonVisualsOrchestrator.ValidateFinite</c> cannot see it either, because
    /// transposed finite floats are still finite. That is a silent player-visible visual misread with no
    /// error anywhere, so it is caught here at bake time instead, where it can still fail loudly.
    ///
    /// This type also holds the path literal that was previously declared twice - once in
    /// <see cref="VisualTuningBaker"/> and once as a local in
    /// Assets/_Project/Editor/HectonArchitectureBinder.cs. Following the precedent documented at
    /// Assets/_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs:92-108, the StreamingAssets-relative
    /// form is the single literal and the editor write path is derived from it, never restated. When the
    /// runtime reader is next touched, <see cref="StreamingAssetsRelativePath"/> should MOVE to runtime code
    /// beside <c>HectonVisualsOrchestrator.BinaryPath</c> and this const should become a reference to it, so
    /// the path keeps existing in exactly one place. Do not satisfy a caller by adding a second literal.
    /// </summary>
    public static class VisualTuningBinaryContract
    {
        /// <summary>
        /// The one path literal, in the StreamingAssets-relative form the runtime reader uses. Must stay
        /// byte-identical to <c>HectonVisualsOrchestrator.BinaryPath</c>
        /// (Assets/_Project/Scripts/Graphics/HectonVisualsOrchestrator.cs:19). StreamingAssets is the only
        /// route by which a shipped player can read these bytes by path; there is no ScriptedImporter for
        /// .h8bin in this project, so a .h8bin anywhere else under Assets/ imports as a DefaultAsset and
        /// cannot be loaded as bytes at runtime at all.
        /// </summary>
        internal const string StreamingAssetsRelativePath = "Hecton8/DataMonolith/visual_tuning.h8bin";

        /// <summary>Editor write target, derived from <see cref="StreamingAssetsRelativePath"/>, not restated.</summary>
        internal const string OutputAssetPath = "Assets/StreamingAssets/" + StreamingAssetsRelativePath;

        /// <summary>
        /// Authoring input. Also declared as a local in HectonArchitectureBinder.cs:20; that copy should
        /// become a reference to this one. As of 2026-07-29 no asset exists at this path and the directory
        /// Assets/_Project/Settings/ does not exist, so the bake has no authored input to read.
        /// </summary>
        internal const string FacadeAssetPath = "Assets/_Project/Settings/VisualTuningFacade.asset";

        /// <summary>
        /// The size the on-disk format is defined against. Stated as a literal deliberately: comparing
        /// <c>sizeof</c> to itself would be a tautology and would not detect that the struct grew or shrank.
        /// </summary>
        internal const int ExpectedSizeBytes = 64;

        /// <summary>
        /// Field identity of every 4-byte slot in the image, in declaration order. This is the contract a
        /// reorder must break loudly.
        /// </summary>
        private static readonly string[] SlotNames =
        {
            "OceanScatterBase.x", "OceanScatterBase.y", "OceanScatterBase.z", "OceanScatterBase.w",
            "OceanScatterShallow.x", "OceanScatterShallow.y", "OceanScatterShallow.z", "OceanScatterShallow.w",
            "SunColor.x", "SunColor.y", "SunColor.z", "SunColor.w",
            "OceanScatterShallowDepthMax", "PlanetCenterRadius", "SunIntensity", "Exposure"
        };

        /// <summary>
        /// Serialises <see cref="VisualTuningState"/> through the same path the baker writes with.
        /// </summary>
        internal static unsafe byte[] ToBytes(ref VisualTuningState state)
        {
            byte[] buffer = new byte[UnsafeUtility.SizeOf<VisualTuningState>()];
            fixed (byte* ptr = buffer)
            {
                UnsafeUtility.CopyStructureToPtr(ref state, ptr);
            }

            return buffer;
        }

        /// <summary>
        /// Proves that the current <see cref="VisualTuningState"/> still serialises to the byte layout the
        /// on-disk format was defined against - correct total size, correct ARM64 alignment, and every field
        /// still at its contracted 4-byte slot. Writes a sentinel probe through the real
        /// <c>CopyStructureToPtr</c> path rather than reasoning about offsets, so it measures what the baker
        /// will actually emit.
        /// </summary>
        internal static bool TryValidateLayout(out string error)
        {
            if (!BitConverter.IsLittleEndian)
            {
                error = "Host is big-endian. visual_tuning.h8bin is a raw struct image with no endian " +
                        "marker, and the runtime reader does not check endianness, so a bake here would " +
                        "produce a file that is byte-swapped on every consumer.";
                return false;
            }

            int size = UnsafeUtility.SizeOf<VisualTuningState>();
            if (size != ExpectedSizeBytes)
            {
                error = $"VisualTuningState is {size} bytes but the on-disk format is defined for " +
                        $"{ExpectedSizeBytes}. HectonVisualsOrchestrator.cs:64-65 rejects any length that is " +
                        $"not exactly its own sizeof, so this bake would produce a file that throws " +
                        $"DataCorruptionException out of Awake(). Change ExpectedSizeBytes and re-bake only " +
                        "as a deliberate format revision.";
                return false;
            }

            if (size % 8 != 0)
            {
                error = $"VisualTuningState size ({size}) is not a multiple of 8. ARM64 alignment violated.";
                return false;
            }

            // Distinct, exactly-representable sentinels: slot i must carry the value i + 1.
            VisualTuningState probe = new VisualTuningState
            {
                OceanScatterBase = new float4(1f, 2f, 3f, 4f),
                OceanScatterShallow = new float4(5f, 6f, 7f, 8f),
                SunColor = new float4(9f, 10f, 11f, 12f),
                OceanScatterShallowDepthMax = 13f,
                PlanetCenterRadius = 14f,
                SunIntensity = 15f,
                Exposure = 16f
            };

            byte[] image = ToBytes(ref probe);
            int slots = ExpectedSizeBytes / 4;
            for (int i = 0; i < slots; i++)
            {
                float actual = BitConverter.ToSingle(image, i * 4);
                float expected = i + 1;
                if (actual == expected)
                {
                    continue;
                }

                int observedSlot = (int)actual - 1;
                string observedName = observedSlot >= 0 && observedSlot < SlotNames.Length
                    ? SlotNames[observedSlot]
                    : "<unmapped>";

                error = $"VisualTuningState byte layout drifted. Slot {i} (offset {i * 4}) must hold " +
                        $"{SlotNames[i]} but holds {observedName} (sentinel {actual}). The struct is still " +
                        $"{ExpectedSizeBytes} bytes, so HectonVisualsOrchestrator would ACCEPT this file and " +
                        "apply transposed values with no error - ValidateFinite cannot see a transposition. " +
                        "Restore the declaration order in VisualTuningState.cs, or revise this contract and " +
                        "the runtime reader together.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Decodes an on-disk image into a human-readable field dump. Read-only.
        /// </summary>
        private static string DescribeImage(byte[] image)
        {
            var text = new System.Text.StringBuilder(512);
            int slots = Math.Min(SlotNames.Length, image.Length / 4);
            for (int i = 0; i < slots; i++)
            {
                text.Append("  [").Append(i * 4).Append("] ").Append(SlotNames[i]).Append(" = ")
                    .Append(BitConverter.ToSingle(image, i * 4).ToString("R"))
                    .Append('\n');
            }

            return text.ToString();
        }

        /// <summary>
        /// Read-only headless gate. Validates the struct layout, then parses whatever is on disk at
        /// <see cref="OutputAssetPath"/> and prints every field plus whether the payload carries any
        /// information beyond <c>VisualTuningState.Default()</c>. Writes nothing and imports nothing, so it
        /// is safe to run against a dirty tree.
        ///
        /// Headless invocation:
        ///   Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 \
        ///     -executeMethod Hecton8.Graphics.Authoring.VisualTuningBinaryContract.Verify
        ///
        /// Success line to grep for:
        ///   [VisualTuningContract] VERIFY_OK
        /// Failure lines begin with:
        ///   [VisualTuningContract] VERIFY_FAIL
        /// </summary>
        public static void Verify()
        {
            int exitCode = 0;

            if (!TryValidateLayout(out string layoutError))
            {
                Debug.LogError($"[VisualTuningContract] VERIFY_FAIL layout: {layoutError}");
                exitCode = 2;
            }
            else
            {
                Debug.Log($"[VisualTuningContract] layout=match size={ExpectedSizeBytes} slots={SlotNames.Length}");
            }

            if (!File.Exists(OutputAssetPath))
            {
                Debug.LogError($"[VisualTuningContract] VERIFY_FAIL missing artifact at {OutputAssetPath}.");
                exitCode = 3;
            }
            else
            {
                byte[] image = File.ReadAllBytes(OutputAssetPath);
                Debug.Log($"[VisualTuningContract] artifact={OutputAssetPath} bytes={image.Length}");

                if (image.Length != ExpectedSizeBytes)
                {
                    Debug.LogError(
                        $"[VisualTuningContract] VERIFY_FAIL artifact is {image.Length} bytes, reader " +
                        $"requires exactly {ExpectedSizeBytes}. HectonVisualsOrchestrator would throw at Awake().");
                    exitCode = 4;
                }
                else
                {
                    Debug.Log("[VisualTuningContract] field dump:\n" + DescribeImage(image));

                    VisualTuningState defaults = VisualTuningState.Default();
                    byte[] defaultImage = ToBytes(ref defaults);
                    bool identical = true;
                    for (int i = 0; i < ExpectedSizeBytes; i++)
                    {
                        if (image[i] == defaultImage[i])
                        {
                            continue;
                        }

                        identical = false;
                        break;
                    }

                    if (identical)
                    {
                        Debug.LogWarning(
                            "[VisualTuningContract] artifact is byte-identical to VisualTuningState.Default(). " +
                            "The binary carries zero information: the data-driven path and the hardcoded " +
                            "fallback produce the same visuals, so this pipeline is currently inert. " +
                            $"No authored input exists either - {FacadeAssetPath} is absent.");
                    }

                    Debug.Log($"[VisualTuningContract] identicalToDefault={identical}");
                }
            }

            if (exitCode == 0)
            {
                Debug.Log("[VisualTuningContract] VERIFY_OK");
            }

            // Fully qualified: Hecton8.* namespaces shadow several BCL/engine type names in this project
            // (CONTRIBUTING.md - Hecton8.Environment shadows System.Environment), so short names are unsafe here.
            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Headless bake from the authored facade asset. Deliberately REFUSES to invent a defaults-only
        /// facade: silently creating one is how the current inert artifact came to exist, and an artifact
        /// that equals the hardcoded fallback is indistinguishable from no artifact at all.
        ///
        /// Headless invocation:
        ///   Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 \
        ///     -executeMethod Hecton8.Graphics.Authoring.VisualTuningBinaryContract.BakeFromFacadeAsset
        ///
        /// Success line to grep for:
        ///   [VisualTuningContract] BAKE_OK
        /// </summary>
        public static void BakeFromFacadeAsset()
        {
            int exitCode = 0;
            var facade = AssetDatabase.LoadAssetAtPath<VisualTuningFacadeSO>(FacadeAssetPath);
            if (facade == null)
            {
                Debug.LogError(
                    $"[VisualTuningContract] BAKE_FAIL no authored facade at {FacadeAssetPath}. Create and " +
                    "tune one before baking; this entry point will not fabricate a defaults-only facade, " +
                    "because the resulting binary would be byte-identical to VisualTuningState.Default() " +
                    "and would carry no information.");
                exitCode = 3;
            }
            else if (!VisualTuningBaker.TryBake(facade, out string bakeError))
            {
                Debug.LogError($"[VisualTuningContract] BAKE_FAIL {bakeError}");
                exitCode = 2;
            }
            else
            {
                Debug.Log($"[VisualTuningContract] BAKE_OK {OutputAssetPath}");
            }

            // Fully qualified: Hecton8.* namespaces shadow several BCL/engine type names in this project
            // (CONTRIBUTING.md - Hecton8.Environment shadows System.Environment), so short names are unsafe here.
            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
