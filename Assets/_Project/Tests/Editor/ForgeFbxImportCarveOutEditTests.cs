using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Proves the offline-forge carve-out in <c>HectonFBXPostprocessor</c> fires only for a real forge package
    /// and never for a hand-authored or third-party FBX. The carve-out relaxes the managed model-import policy,
    /// so a false positive would silently change how existing content imports; every reject case below is one
    /// the identification must keep rejecting.
    ///
    /// Reflection is used because <c>[assembly: InternalsVisibleTo]</c> is not granted from
    /// <c>Hecton8.Editor</c> to this test assembly, matching the existing pattern in
    /// <c>H8BlackboxUtilsExceptionTests.cs</c>. The production methods themselves are invoked; nothing here
    /// reimplements their logic.
    /// </summary>
    public sealed class ForgeFbxImportCarveOutEditTests
    {
        private const string PostprocessorTypeName = "Hecton8.Editor.HectonFBXPostprocessor, Hecton8.Editor";
        private const string RealKelpManifest = "Docs/AgentLogs/ForgeKelp/MANIFEST_Flora_Kelp_s4022_q100.json";
        private const string RealKelpFbxFileName = "MESH_Flora_Kelp_s4022_q100.fbx";

        // A schema-1 manifest that declares the full importer contract. Field names and value tokens come from
        // Tools/Blender/h8forge/export_unity.py unity_import_notes() and MANIFEST_SCHEMA.
        private const string DeclaredContractManifest = @"{
 ""schema"": ""h8forge.manifest/1"",
 ""export"": { ""hasCustomNormals"": true },
 ""lod"": { ""levels"": [0, 1, 2] },
 ""unityImport"": {
  ""modelImporter"": {
   ""importNormals"": ""Import"",
   ""normalSmoothingAngle"": 32.0,
   ""generateSecondaryUV"": false,
   ""materialImportMode"": ""None""
  }
 }
}";

        // The same contract from a package that never wrote custom split normals.
        private const string DeclaredContractWithoutCustomNormals = @"{
 ""schema"": ""h8forge.manifest/1"",
 ""export"": { ""hasCustomNormals"": false },
 ""lod"": { ""levels"": [0, 1, 2] },
 ""unityImport"": {
  ""modelImporter"": {
   ""importNormals"": ""Import"",
   ""normalSmoothingAngle"": 32.0,
   ""generateSecondaryUV"": false,
   ""materialImportMode"": ""None""
  }
 }
}";

        // The generator-local shape kelp.py and rock.py actually emit today: no schema, no unityImport.
        private const string GeneratorLocalManifest = @"{
 ""identity"": { ""generator"": ""kelp.py"", ""forgeVersion"": ""1.0.0"" },
 ""files"": { ""fbx"": ""MESH_Flora_Kelp_s4022_q100.fbx"" },
 ""validation"": { ""allPassed"": true },
 ""lods"": [ { ""lod"": 0 }, { ""lod"": 1 }, { ""lod"": 2 } ],
 ""materialSlots"": [
  { ""slot"": 0, ""material"": ""MAT_Flora_tissue"" },
  { ""slot"": 1, ""material"": ""MAT_Flora_basal_collar_scar"" }
 ]
}";

        [Test]
        public void ManifestPath_IsDerivedFromTheForgeMeshName()
        {
            Assert.IsTrue(
                TryResolveManifestPath("Assets/_Project/Art/Generated/MESH_Flora_Kelp_s4022_q100.fbx", out string manifestPath));
            Assert.AreEqual(
                "Assets/_Project/Art/Generated/MANIFEST_Flora_Kelp_s4022_q100.json",
                manifestPath,
                "The manifest must be derived as a sibling of the FBX, MESH_ swapped for MANIFEST_ and .fbx for .json.");
        }

        [Test]
        public void ManifestPath_RejectsEveryFbxThatIsNotAForgePackage()
        {
            // The real near-miss already in the project: "Meshy" shares four characters with "MESH_".
            AssertNotAForgePackage("Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx");

            // Real hand-authored and third-party content under the managed roots.
            AssertNotAForgePackage("Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx");
            AssertNotAForgePackage("Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx");
            AssertNotAForgePackage("Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx");

            // Third-party quarantine can never earn the carve-out even with a correct forge name, because
            // ImporterMatchesScifiFacilityPolicy asserts the strict vendor policy for that root.
            AssertNotAForgePackage("Assets/ScifiFacility/Models/MESH_Flora_Kelp_s4022_q100.fbx");

            // Case matters: law.NAME_MESH writes upper-case MESH_.
            AssertNotAForgePackage("Assets/_Project/Art/Generated/mesh_Flora_Kelp_s4022_q100.fbx");

            // Not a model, and a bare prefix with no stem.
            AssertNotAForgePackage("Assets/_Project/Art/Generated/MESH_Flora_Kelp_s4022_q100.obj");
            AssertNotAForgePackage("Assets/_Project/Art/Generated/MESH_.fbx");
            AssertNotAForgePackage(string.Empty);
        }

        [Test]
        public void DeclaredContract_HonoursEveryFieldTheManifestStates()
        {
            Assert.IsTrue(TryParseContract(DeclaredContractManifest, "m.json", RealKelpFbxFileName, out object contract));

            Assert.IsTrue(ReadBool(contract, "DeclaredImportContract"), "schema h8forge.manifest/1 is tier 1.");
            Assert.IsTrue(ReadBool(contract, "ImportAuthoredNormals"), "importNormals=Import plus hasCustomNormals=true.");
            Assert.AreEqual(32.0f, ReadFloat(contract, "NormalSmoothingAngle"), 0.0001f);
            Assert.IsTrue(ReadBool(contract, "SuppressSecondaryUv"), "generateSecondaryUV=false must suppress auto-unwrap.");
            Assert.IsTrue(ReadBool(contract, "SuppressMaterialImport"), "materialImportMode=None must suppress material import.");
            Assert.AreEqual(3, ReadInt(contract, "AuthoredLodLevelCount"));
        }

        [Test]
        public void DeclaredContract_WillNotImportNormalsThePackageNeverWrote()
        {
            Assert.IsTrue(TryParseContract(DeclaredContractWithoutCustomNormals, "m.json", RealKelpFbxFileName, out object contract));

            Assert.IsFalse(
                ReadBool(contract, "ImportAuthoredNormals"),
                "Without export.hasCustomNormals there is no authored basis to import, so Calculate must stay.");
            Assert.IsTrue(ReadBool(contract, "SuppressSecondaryUv"), "The unrelated UV1 relaxation still applies.");
        }

        [Test]
        public void GeneratorLocalManifest_IsAcceptedOnProvenanceAndDerivesOnlyWhatItProves()
        {
            Assert.IsTrue(TryParseContract(GeneratorLocalManifest, "m.json", RealKelpFbxFileName, out object contract));

            Assert.IsFalse(ReadBool(contract, "DeclaredImportContract"), "No unityImport block: this is tier 2.");
            Assert.IsTrue(ReadBool(contract, "ImportAuthoredNormals"), "3dmodel.md section 3 gives normals to the generator.");
            Assert.AreEqual(0f, ReadFloat(contract, "NormalSmoothingAngle"), 0.0001f, "No angle declared, so none is written.");
            Assert.IsFalse(
                ReadBool(contract, "SuppressSecondaryUv"),
                "A generator-local manifest records no UV1, so Unity's generator must not be forced off.");
            Assert.IsTrue(ReadBool(contract, "SuppressMaterialImport"), "Every declared slot names a MAT_* asset.");
            Assert.AreEqual(3, ReadInt(contract, "AuthoredLodLevelCount"));
        }

        [Test]
        public void GeneratorLocalManifest_RejectedWhenProvenanceIsNotProven()
        {
            // Names a different FBX than the one it sits beside.
            Assert.IsFalse(
                TryParseContract(GeneratorLocalManifest, "m.json", "MESH_Flora_Kelp_s9999_q100.fbx", out _),
                "files.fbx must name the exact file beside the manifest.");

            // A package whose own validation gates failed gets no relaxation.
            Assert.IsFalse(
                TryParseContract(
                    GeneratorLocalManifest.Replace(@"""allPassed"": true", @"""allPassed"": false"),
                    "m.json",
                    RealKelpFbxFileName,
                    out _),
                "3dmodel.md section 10: failure aborts save, so a failed package is not trusted.");

            // No forge provenance at all: an ordinary sidecar JSON must not be mistaken for a manifest.
            Assert.IsFalse(TryParseContract(@"{ ""files"": { ""fbx"": ""MESH_Flora_Kelp_s4022_q100.fbx"" } }", "m.json", RealKelpFbxFileName, out _));
            Assert.IsFalse(TryParseContract(@"{ ""identity"": { ""generator"": ""kelp.py"" } }", "m.json", RealKelpFbxFileName, out _),
                "identity.forgeVersion is required as well as identity.generator.");
            Assert.IsFalse(TryParseContract("{}", "m.json", RealKelpFbxFileName, out _));
            Assert.IsFalse(TryParseContract(string.Empty, "m.json", RealKelpFbxFileName, out _));
        }

        [Test]
        public void DeclaredSchemaWithoutAnImporterBlock_FallsBackToStrictPolicy()
        {
            Assert.IsFalse(
                TryParseContract(@"{ ""schema"": ""h8forge.manifest/1"" }", "m.json", RealKelpFbxFileName, out _),
                "A schema claim with no unityImport.modelImporter has no contract to honour.");
        }

        [Test]
        public void RealForgeManifestOnDisk_IsAcceptedByTheIdentification()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string manifestPath = Path.Combine(projectRoot, RealKelpManifest.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(manifestPath))
            {
                Assert.Ignore("No forge output on disk at " + RealKelpManifest + "; run the kelp generator first.");
                return;
            }

            Assert.IsTrue(
                TryParseContract(File.ReadAllText(manifestPath), RealKelpManifest, RealKelpFbxFileName, out object contract),
                "The manifest the forge actually writes must be recognised, or the carve-out is a gate that cannot fire.");
            Assert.IsTrue(ReadBool(contract, "ImportAuthoredNormals"));
            Assert.GreaterOrEqual(ReadInt(contract, "AuthoredLodLevelCount"), 2, "kelp.py authors LOD0/LOD1/LOD2.");
        }

        private static void AssertNotAForgePackage(string assetPath)
        {
            Assert.IsFalse(
                TryResolveManifestPath(assetPath, out _),
                "'" + assetPath + "' must not be identified as forge output.");
        }

        private static Type PostprocessorType()
        {
            Type type = Type.GetType(PostprocessorTypeName, throwOnError: false);
            Assert.IsNotNull(type, "Could not load " + PostprocessorTypeName);
            return type;
        }

        private static MethodInfo StaticMethod(string name)
        {
            MethodInfo method = PostprocessorType().GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Could not find internal static method " + name);
            return method;
        }

        private static bool TryResolveManifestPath(string assetPath, out string manifestPath)
        {
            object[] args = { assetPath, null };
            bool resolved = (bool)StaticMethod("TryResolveForgeManifestPath").Invoke(null, args);
            manifestPath = args[1] as string;
            return resolved;
        }

        private static bool TryParseContract(string json, string manifestPath, string fbxFileName, out object contract)
        {
            object[] args = { json, manifestPath, fbxFileName, null };
            bool parsed = (bool)StaticMethod("TryParseForgeImportContract").Invoke(null, args);
            contract = args[3];
            return parsed;
        }

        private static object ReadField(object contract, string fieldName)
        {
            Assert.IsNotNull(contract, "Contract was null.");
            FieldInfo field = contract.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Could not find internal field " + fieldName + " on ForgeImportContract");
            return field.GetValue(contract);
        }

        private static bool ReadBool(object contract, string fieldName)
        {
            return (bool)ReadField(contract, fieldName);
        }

        private static float ReadFloat(object contract, string fieldName)
        {
            return (float)ReadField(contract, fieldName);
        }

        private static int ReadInt(object contract, string fieldName)
        {
            return (int)ReadField(contract, fieldName);
        }
    }
}
