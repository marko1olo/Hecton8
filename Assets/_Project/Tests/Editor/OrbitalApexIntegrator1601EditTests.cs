using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using UnityEngine;

public sealed class OrbitalApexIntegrator1601EditTests
{
    private static readonly string[] PrologueSpaceFiles =
    {
        "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs",
        "Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs",
        "Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs"
    };

    private static readonly string[] HotMethodNames =
    {
        "Tick",
        "FixedUpdate",
        "LateFrameTick",
        "Update",
        "Execute",
        "ApplyPresentation",
        "BuildPresentationShaderGlobals",
        "BuildCelestialParameters",
        "UploadPresentationShaderGlobalsIfDirty",
        "UploadCelestialGlobalsIfDirty",
        "PresentationShaderGlobalsChanged",
        "CelestialParametersChanged",
        "Vector4DeltaSq",
        "ApplyEclipseLighting",
        "ResolveStableMathLod",
        "ResolveMathLod",
        "ResolveQuality01",
        "QueueCapsuleAuthorityLock",
        "QueueOrbitalPresentation",
        "QueueShaderGlobalClear",
        "QueueRuntimeAuthorityRelease"
    };

    [Test]
    public void HotPhaseMethods_DoNotResolveColdDependencies()
    {
        foreach (string relativePath in PrologueSpaceFiles)
        {
            CompilationUnitSyntax root = Parse(relativePath);
            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!HotMethodNames.Contains(method.Identifier.ValueText))
                    continue;

                string methodSource = method.ToFullString();
                Assert.That(ContainsForbiddenHotText(methodSource), Is.False, relativePath + ":" + method.Identifier.ValueText);

                InvocationExpressionSyntax[] invocations = method
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .ToArray();

                foreach (InvocationExpressionSyntax invocation in invocations)
                {
                    string expression = invocation.Expression.ToString();
                    Assert.That(IsHotDependencyLookup(expression), Is.False, relativePath + ":" + method.Identifier.ValueText + " -> " + expression);
                }
            }
        }
    }

    [Test]
    public void OrbitalPresentation_IsTransferredToLateFrameOnly()
    {
        CompilationUnitSyntax root = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        MethodDeclarationSyntax tick = FindMethod(root, "Tick");
        MethodDeclarationSyntax late = FindMethod(root, "LateFrameTick");
        MethodDeclarationSyntax queue = FindMethod(root, "QueueOrbitalPresentation");

        Assert.That(Calls(tick, "QueueOrbitalPresentation"), Is.True);
        Assert.That(Calls(tick, "ApplyPresentation"), Is.False);
        Assert.That(Calls(late, "ApplyPresentation"), Is.True);
        Assert.That(Calls(queue, "TryRegisterUpdateLane"), Is.False);

        InvocationExpressionSyntax[] applyCalls = root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) == "ApplyPresentation")
            .ToArray();

        Assert.That(applyCalls.Length, Is.EqualTo(1));
        Assert.That(applyCalls[0].FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText, Is.EqualTo("LateFrameTick"));
    }

    [Test]
    public void OrbitalPresentation_UsesTickDeltaInsteadOfUnityGlobalTime()
    {
        string source = Read("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        CompilationUnitSyntax root = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        MethodDeclarationSyntax tick = FindMethod(root, "Tick");
        MethodDeclarationSyntax eclipse = FindMethod(root, "ApplyEclipseLighting");

        Assert.That(ContainsOrdinal(source, "Time.deltaTime"), Is.False);
        Assert.That(ContainsOrdinal(source, "Time.fixedDeltaTime"), Is.False);
        Assert.That(ContainsOrdinal(tick.ToFullString(), "_presentationDeltaTime = dt;"), Is.True);
        Assert.That(ContainsOrdinal(eclipse.ToFullString(), "_presentationDeltaTime * response"), Is.True);
    }

    [Test]
    public void OrbitQualityAndEclipseInputs_AreFiniteSanitized()
    {
        string directorSource = Read("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        string bootstrapSource = Read("Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs");
        CompilationUnitSyntax directorRoot = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        CompilationUnitSyntax bootstrapRoot = Parse("Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs");
        MethodDeclarationSyntax celestial = FindMethod(directorRoot, "BuildCelestialParameters");
        MethodDeclarationSyntax post = FindMethod(bootstrapRoot, "ConfigureOrbitPostProcessing");

        Assert.That(ContainsOrdinal(directorSource, "math.saturate(HomeostasisBrain.GlobalQualityWeight)"), Is.False);
        Assert.That(ContainsOrdinal(bootstrapSource, "math.saturate(HomeostasisBrain.GlobalQualityWeight)"), Is.False);
        Assert.That(ContainsOrdinal(celestial.ToFullString(), "float quality = ResolveQuality01();"), Is.True);
        Assert.That(ContainsOrdinal(post.ToFullString(), "float quality = ResolveQuality01();"), Is.True);
        Assert.That(ContainsOrdinal(celestial.ToFullString(), "SunDirection.w = math.saturate(snapshot.EclipseOcclusion01);"), Is.True);
        Assert.That(ContainsOrdinal(directorSource, "math.select(1f, quality, math.isfinite(quality))"), Is.True);
        Assert.That(ContainsOrdinal(bootstrapSource, "math.select(1f, quality, math.isfinite(quality))"), Is.True);
    }

    [Test]
    public void LegacyTransformOrbitPresentation_IsPurged()
    {
        string source = Read("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");

        Assert.That(ContainsOrdinal(source, "ResolveOrbitalWindowOffset"), Is.False);
        Assert.That(ContainsOrdinal(source, "ResolveGasGiantBackdropPosition"), Is.False);
        Assert.That(ContainsOrdinal(source, "orbitalArcRadius01"), Is.False);
        Assert.That(ContainsOrdinal(source, "hectonOrbitTurns"), Is.False);
        Assert.That(ContainsOrdinal(source, "orbitPresentationFadeDistanceMeters"), Is.False);
        Assert.That(ContainsOrdinal(source, "planetSphere.localPosition"), Is.False);
        Assert.That(ContainsOrdinal(source, "gasGiantBackdrop.localPosition"), Is.False);
    }

    [Test]
    public void PresentationShaderGlobals_AreDirtyGated()
    {
        CompilationUnitSyntax root = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        MethodDeclarationSyntax presentation = FindMethod(root, "ApplyPresentation");
        MethodDeclarationSyntax upload = FindMethod(root, "UploadPresentationShaderGlobalsIfDirty");
        MethodDeclarationSyntax clear = FindMethod(root, "ClearShaderGlobals");
        string source = Read("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");

        Assert.That(ContainsOrdinal(source, "StructLayout(LayoutKind.Explicit, Size = 32)"), Is.True);
        Assert.That(Calls(presentation, "BuildPresentationShaderGlobals"), Is.True);
        Assert.That(Calls(presentation, "UploadPresentationShaderGlobalsIfDirty"), Is.True);
        Assert.That(Calls(presentation, "SetGlobalFloat"), Is.False);
        Assert.That(Calls(upload, "PresentationShaderGlobalsChanged"), Is.True);
        Assert.That(ContainsOrdinal(upload.ToFullString(), "_presentationShaderGlobalsUploaded"), Is.True);
        Assert.That(ContainsOrdinal(clear.ToFullString(), "_presentationShaderGlobalsUploaded = false;"), Is.True);
    }

    [Test]
    public void CelestialShaderGlobals_AreDirtyGated()
    {
        CompilationUnitSyntax root = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        MethodDeclarationSyntax presentation = FindMethod(root, "ApplyPresentation");
        MethodDeclarationSyntax upload = FindMethod(root, "UploadCelestialGlobalsIfDirty");
        MethodDeclarationSyntax clear = FindMethod(root, "ClearShaderGlobals");

        Assert.That(Calls(presentation, "UploadCelestialGlobalsIfDirty"), Is.True);
        Assert.That(Calls(presentation, "SetGlobalVector"), Is.False);
        Assert.That(Calls(presentation, "SetGlobalFloat"), Is.False);
        Assert.That(Calls(upload, "CelestialParametersChanged"), Is.True);
        Assert.That(ContainsOrdinal(upload.ToFullString(), "_celestialParametersUploaded"), Is.True);
        Assert.That(ContainsOrdinal(clear.ToFullString(), "_celestialParametersUploaded = false;"), Is.True);
    }

    [Test]
    public void OrbitBloom_UsesContinuousQualityWeight()
    {
        string source = Read("Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs");

        Assert.That(ContainsOrdinal(source, "quality >"), Is.False);
        Assert.That(ContainsOrdinal(source, "float bloomWeight = quality * quality;"), Is.True);
        Assert.That(ContainsOrdinal(source, "volume.weight = bloomWeight;"), Is.True);
        Assert.That(ContainsOrdinal(source, "bloom.intensity.Override(OrbitBloomFullIntensity * bloomWeight);"), Is.True);
        Assert.That(ContainsOrdinal(source, "bloom.highQualityFiltering.Override(false);"), Is.True);
    }

    [Test]
    public void OrbitBootstrap_UsesDeterministicCameraAndShadowBudget()
    {
        string source = Read("Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs");

        Assert.That(ContainsOrdinal(source, "camera.farClipPlane = OrbitCameraFarClipMeters;"), Is.True);
        Assert.That(ContainsOrdinal(source, "Mathf.Max(camera.farClipPlane"), Is.False);
        Assert.That(ContainsOrdinal(source, "light.intensity = OrbitKeyLightIntensity;"), Is.True);
        Assert.That(ContainsOrdinal(source, "Mathf.Max(light.intensity"), Is.False);
        Assert.That(ContainsOrdinal(source, "LightShadowResolution.VeryHigh"), Is.False);
        Assert.That(ContainsOrdinal(source, "light.shadowResolution = LightShadowResolution.FromQualitySettings;"), Is.True);
    }

    [Test]
    public void DataVaultTelemetryWriteLock_IsSingleAndReleasedInFinally()
    {
        CompilationUnitSyntax root = Parse("Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs");
        InvocationExpressionSyntax[] allLockAcquires = root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) == "TryAcquireWriteLock")
            .ToArray();

        Assert.That(allLockAcquires.Length, Is.EqualTo(1), "1601 domain must not nest multiple DataVault write locks.");

        MethodDeclarationSyntax recordTelemetry = FindMethod(root, "RecordTelemetry");
        InvocationExpressionSyntax[] acquires = recordTelemetry
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) == "TryAcquireWriteLock")
            .ToArray();
        InvocationExpressionSyntax[] releases = recordTelemetry
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) == "ReleaseWriteLock")
            .ToArray();

        Assert.That(acquires.Length, Is.EqualTo(1));
        Assert.That(releases.Length, Is.EqualTo(1));
        Assert.That(acquires[0].FirstAncestorOrSelf<TryStatementSyntax>(), Is.Null);
        Assert.That(releases[0].FirstAncestorOrSelf<FinallyClauseSyntax>(), Is.Not.Null);
    }

    [Test]
    public void OrbitalDomain_DoesNotSpawnBuildProcesses()
    {
        foreach (string relativePath in PrologueSpaceFiles)
        {
            string source = Read(relativePath);
            Assert.That(ContainsOrdinalIgnoreCase(source, "dotnet build"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "ProcessStartInfo"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "BuildPipeline.BuildPlayer"), Is.False, relativePath);
            Assert.That(ContainsOrdinalIgnoreCase(source, "csc.exe"), Is.False, relativePath);
            Assert.That(ContainsOrdinalIgnoreCase(source, "MSBuild"), Is.False, relativePath);
        }
    }

    [Test]
    public void AegirShader_HasNoExpensiveForbiddenCallsOrVariants()
    {
        string shader = Read("Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader");
        Assert.That(ContainsOrdinal(shader, "pow("), Is.False);
        Assert.That(ContainsOrdinal(shader, "sin("), Is.False);
        Assert.That(ContainsOrdinal(shader, "normalize("), Is.False);
        Assert.That(ContainsOrdinal(shader, "_AegirFlowTex"), Is.False);
        Assert.That(ContainsOrdinal(shader, "flowWeight >"), Is.False);
        Assert.That(CountStandaloneSqrt(shader), Is.EqualTo(1));
        Assert.That(ContainsOrdinal(shader, "HardRingMaskSq"), Is.True);
        Assert.That(ContainsOrdinal(shader, "innerRadius * innerRadius"), Is.False);
        Assert.That(ContainsOrdinal(shader, "outerRadius * outerRadius"), Is.False);
        Assert.That(ContainsOrdinal(shader, "multi_compile"), Is.False);
        Assert.That(ContainsOrdinal(shader, "shader_feature"), Is.False);
        Assert.That(ContainsOrdinal(shader, "for ("), Is.False);
        Assert.That(ContainsOrdinal(shader, "while ("), Is.False);
    }

    [Test]
    public void AegirMaterial_DoesNotBindRemovedFlowTexture()
    {
        string material = Read("Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat");

        Assert.That(ContainsOrdinal(material, "_AegirBandTex"), Is.True);
        Assert.That(ContainsOrdinal(material, "_AegirFlowTex"), Is.False);
    }

    private static bool IsHotDependencyLookup(string expression)
    {
        return ContainsOrdinal(expression, "GlobalRegistry.Get<") ||
               ContainsOrdinal(expression, "GlobalRegistry.") ||
               expression.EndsWith(".GetComponent", StringComparison.Ordinal) ||
               expression.EndsWith(".TryGetComponent", StringComparison.Ordinal) ||
               expression == "GetComponent" ||
               expression == "TryGetComponent";
    }

    private static bool ContainsForbiddenHotText(string source)
    {
        return ContainsOrdinal(source, "GlobalRegistry.") ||
               ContainsOrdinal(source, "GetComponent(") ||
               ContainsOrdinal(source, "TryGetComponent(") ||
               ContainsOrdinal(source, "TryRegisterUpdateLane(") ||
               ContainsOrdinal(source, "Time.deltaTime") ||
               ContainsOrdinal(source, "Time.fixedDeltaTime") ||
               ContainsOrdinal(source, "Camera.main");
    }

    private static bool Calls(MethodDeclarationSyntax method, string name)
    {
        return method
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => InvocationName(invocation) == name);
    }

    private static string InvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => invocation.Expression.ToString()
        };
    }

    private static MethodDeclarationSyntax FindMethod(CompilationUnitSyntax root, string name)
    {
        MethodDeclarationSyntax method = root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == name);
        Assert.That(method, Is.Not.Null, name);
        return method;
    }

    private static CompilationUnitSyntax Parse(string relativePath)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Read(relativePath));
        Diagnostic[] parseErrors = tree
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(parseErrors, Is.Empty, relativePath);
        return tree.GetCompilationUnitRoot();
    }

    private static string Read(string relativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return File.ReadAllText(Path.Combine(projectRoot, relativePath));
    }

    private static bool ContainsOrdinal(string source, string value)
    {
        return source.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountStandaloneSqrt(string source)
    {
        int count = 0;
        int index = 0;
        while (index < source.Length)
        {
            int next = source.IndexOf("sqrt(", index, StringComparison.Ordinal);
            if (next < 0)
                break;

            if (next == 0 || source[next - 1] != 'r')
                count++;

            index = next + 5;
        }

        return count;
    }
}
