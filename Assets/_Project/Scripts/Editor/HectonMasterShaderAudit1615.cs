#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class HectonMasterShaderAudit1615
    {
        private const string MenuPath = "Hecton/Validation/Rendering/Audit Hecton Master Shader 1615";
        private const string MasterShaderPath = "Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader";
        private const string MasterShaderMetaPath = "Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader.meta";
        private const string MasterVariantCollectionPath = "Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants";
        private const string MasterShaderGuid = "49aa0d16489a41c88aef21e218cbc32e";
        private const string GlobalShaderDispatcherPath = "Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs";
        private const string ShaderGlobalBridgePath = "Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs";
        private const string SystemDispatcherPath = "Assets/_Project/Scripts/Core/SystemDispatcher.cs";
        private const string MaterialMigratorPath = "Assets/_Project/Scripts/Editor/HectonMasterMaterialMigrator1615.cs";
        private const char OpenBrace = (char)123;
        private const char CloseBrace = (char)125;
        private static readonly string[] DomainScriptRoots =
        {
            "Assets/_Project/Scripts/Rendering",
            "Assets/_Project/Scripts/Graphics/Materials"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "Update",
            "FixedTick",
            "FixedUpdate",
            "LateUpdate",
            "LateFrameTick",
            "Execute",
            "VisualSyncTick"
        };

        private static readonly string[] ForbiddenHotLookupTokens =
        {
            "GlobalRegistry.Get",
            "GlobalRegistry.TryGet",
            "GlobalRegistry.Resolve",
            "GetComponent(",
            "GetComponent<",
            "TryGetComponent(",
            "TryGetComponent<",
            "FindObjectOfType",
            "FindFirstObjectByType",
            "FindAnyObjectByType",
            "FindObjectsOfType",
            "Resources.FindObjectsOfTypeAll",
            "GameObject.Find(",
            "GameObject.FindWithTag(",
            "GameObject.FindGameObjectWithTag(",
            "GameObject.FindGameObjectsWithTag(",
            ".material",
            ".materials",
            "SetPropertyBlock(",
            "EnableKeyword" + "(",
            "DisableKeyword" + "("
        };

        private static readonly string[] StringLiteralGlobalSetterNames =
        {
            "SetGlobalFloat",
            "SetGlobalInt",
            "SetGlobalVector",
            "SetGlobalColor",
            "SetGlobalTexture",
            "SetGlobalBuffer",
            "SetGlobalMatrix",
            "SetGlobalFloatArray",
            "SetGlobalVectorArray",
            "SetGlobalMatrixArray",
            "SetGlobalConstantBuffer"
        };

        private static readonly string[] AllowedShaderGlobalWriteMethods =
        {
            "VisualSyncTick",
            "LateFrameTick",
            "ExecuteGlobalDispatch",
            "FlushFallbackVisualSync",
            "EnsureLoadedAndBound",
            "ReleaseGraphicsBuffers",
            "PublishTint"
        };

        private static readonly string[] DataVaultWriteAcquireTokens =
        {
            "TryAcquireMutationGuard(",
            "TryAcquireWriteLock("
        };

        private static readonly string[] DataVaultWriteReleaseTokens =
        {
            "ReleaseMutationGuard(",
            "ReleaseWriteLock("
        };

        private struct DomainHotMethodAuditStats
        {
            public int RuntimeFileCount;
            public int HotMethodBodyCount;
        }

        [MenuItem(MenuPath, priority = 192)]
        private static void AuditMasterShader()
        {
            string shaderText = File.ReadAllText(MasterShaderPath);
            string shaderMetaText = File.Exists(MasterShaderMetaPath)
                ? File.ReadAllText(MasterShaderMetaPath)
                : string.Empty;
            string collectionText = File.Exists(MasterVariantCollectionPath)
                ? File.ReadAllText(MasterVariantCollectionPath)
                : string.Empty;

            string cbufferFailure;
            int cbufferBytes = CalculateUnityPerMaterialBytes(shaderText, out cbufferFailure);
            int cbufferStartCount = CountOccurrences(shaderText, "CBUFFER_START(UnityPerMaterial)");
            int cbufferEndCount = CountOccurrences(shaderText, "CBUFFER_END");
            int sampleCount = CountOccurrences(shaderText, "SAMPLE_TEXTURE2D");
            int shaderFeatureCount = CountOccurrences(shaderText, "#pragma shader_feature");
            int multiCompileCount = CountOccurrences(shaderText, "#pragma multi_compile");
            int expensiveMathCount = CountExpensiveMathCalls(shaderText);
            int globalQualityTokenCount = CountOccurrences(shaderText, "_H8GlobalQualityWeight");
            int passCount = CountShaderPassBlocks(shaderText);
            int hlslIncludeIndex = shaderText.IndexOf("HLSLINCLUDE", StringComparison.Ordinal);
            int cbufferIndex = shaderText.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int sharedHlslEndIndex = hlslIncludeIndex >= 0
                ? shaderText.IndexOf("ENDHLSL", hlslIncludeIndex, StringComparison.Ordinal)
                : -1;
            int firstPassIndex = shaderText.IndexOf("Pass", StringComparison.Ordinal);
            string cbufferBlock = ExtractUnityPerMaterialBlock(shaderText);
            string materialPropertyRegion = ExtractMaterialPropertyRegion(shaderText);
            bool collectionContainsMaster = collectionText.IndexOf(MasterShaderGuid, StringComparison.OrdinalIgnoreCase) >= 0;
            bool collectionContainsPunctualShadow = collectionText.IndexOf("_CASTING_PUNCTUAL_LIGHT_SHADOW", StringComparison.Ordinal) >= 0;
            DomainHotMethodAuditStats hotMethodStats;

            if (cbufferStartCount != 1 || cbufferEndCount != 1)
                throw new InvalidOperationException("FatalArchitectureException: master shader must expose exactly one UnityPerMaterial CBUFFER.");
            if (cbufferBytes <= 0 || (cbufferBytes & 15) != 0)
                throw new InvalidOperationException("FatalArchitectureException: UnityPerMaterial byte size is not 16-byte aligned. " + cbufferFailure);
            if (sampleCount != 3)
                throw new InvalidOperationException("FatalArchitectureException: master shader must keep exactly three SAMPLE_TEXTURE2D calls.");
            if (shaderFeatureCount != 0)
                throw new InvalidOperationException("FatalArchitectureException: master shader contains shader_feature variants.");
            if (multiCompileCount != 4)
                throw new InvalidOperationException("FatalArchitectureException: master shader must keep only three instancing multi_compile pragmas plus the URP punctual shadow caster vertex pragma.");
            if (expensiveMathCount != 0)
                throw new InvalidOperationException("FatalArchitectureException: master shader contains expensive math calls.");
            if (passCount != 3)
                throw new InvalidOperationException("FatalArchitectureException: master shader must keep exactly three passes.");
            if (hlslIncludeIndex < 0 || cbufferIndex < 0 || sharedHlslEndIndex < 0 || firstPassIndex < 0 ||
                hlslIncludeIndex > cbufferIndex || cbufferIndex > sharedHlslEndIndex || sharedHlslEndIndex > firstPassIndex)
                throw new InvalidOperationException("FatalArchitectureException: UnityPerMaterial must live in shared HLSLINCLUDE before all passes.");
            if (!collectionContainsMaster)
                throw new InvalidOperationException("FatalArchitectureException: master SVC does not serialize Hecton_Master_Lit.");
            if (!collectionContainsPunctualShadow)
                throw new InvalidOperationException("FatalArchitectureException: master SVC does not serialize the URP punctual shadow caster variant.");
            if (globalQualityTokenCount != 3)
                throw new InvalidOperationException("FatalArchitectureException: global quality scalar must appear only in declaration and quality resolver.");
            AssertNoToken(cbufferBlock, MasterShaderPath + " UnityPerMaterial", "_H8GlobalQualityWeight");
            AssertNoToken(materialPropertyRegion, MasterShaderPath + " Properties", "_H8GlobalQualityWeight");
            AssertContainsToken(shaderText, MasterShaderPath, "float _H8GlobalQualityWeight;");
            AssertContainsToken(shaderText, MasterShaderPath, "float globalQuality = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;");
            AssertContainsToken(shaderText, MasterShaderPath, "float materialCap = isfinite(_MasterPomParams.w) ? saturate(_MasterPomParams.w) : 1.0;");
            AssertContainsToken(shaderText, MasterShaderPath, "return saturate(globalQuality * materialCap);");
            AssertContainsToken(shaderMetaText, MasterShaderMetaPath, "guid: " + MasterShaderGuid);
            AssertContainsToken(shaderText, MasterShaderPath, "_MasterSurfaceParams(\"Surface: MetallicMap RoughnessMap AO Normal\", Vector) = (0, 0, 1, 1)");
            AssertContainsToken(shaderText, MasterShaderPath, "_MasterAlphaParams(\"Alpha: Cutoff Scale Dither ClipWeight\", Vector) = (0.5, 1, 0.35, 0)");
            AssertContainsToken(shaderText, MasterShaderPath, "_MasterPomParams(\"POM: Scale Steps Bias QualityCap\", Vector) = (0, 0, 0, 1)");
            AssertContainsToken(shaderText, MasterShaderPath, "_MasterShadowParams(\"Shadow: Contact FogDarken Micro MaskLayout\", Vector) = (1, 0.15, 0.18, 0)");
            AssertContainsToken(shaderText, MasterShaderPath, "Name \"ForwardLit\"");
            AssertContainsToken(shaderText, MasterShaderPath, "Name \"ShadowCaster\"");
            AssertContainsToken(shaderText, MasterShaderPath, "Name \"DepthOnly\"");
            AssertContainsToken(shaderText, MasterShaderPath, "Tags { \"LightMode\" = \"UniversalForward\" }");
            AssertContainsToken(shaderText, MasterShaderPath, "Tags { \"LightMode\" = \"ShadowCaster\" }");
            AssertContainsToken(shaderText, MasterShaderPath, "Tags { \"LightMode\" = \"DepthOnly\" }");
            AssertContainsToken(shaderText, MasterShaderPath, "void H8MasterDecodeMaskLayout(half4 packedMask, out half metallicMask, out half roughnessMask, out half occlusionMask, out half emissionHeightMask)");
            AssertContainsToken(shaderText, MasterShaderPath, "half emissionMask = emissionHeightMask * emissionLayoutWeight * (half)saturate(_EmissionColor.a);");
            AssertContainsToken(shaderText, MasterShaderPath, "half clipWeight = (half)saturate(_MasterAlphaParams.w);");
            AssertContainsToken(shaderText, MasterShaderPath, "clip(lerp(1.0h, clipValue, clipWeight));");
            AssertContainsToken(shaderText, MasterShaderPath, "half outputAlpha = lerp(1.0h, alpha, (half)saturate(_MasterAlphaParams.w));");
            AssertContainsToken(shaderText, MasterShaderPath, "half armLayout = (half)saturate(1.0 - abs((float)layout - 3.0));");
            AssertContainsToken(shaderText, MasterShaderPath, "packedMask.b * armLayout");
            AssertContainsToken(shaderText, MasterShaderPath, "packedMask.r * armLayout");
            AssertContainsToken(shaderText, MasterShaderPath, "half4 H8MasterSampleBase(float2 uv)");
            AssertContainsToken(shaderText, MasterShaderPath, "void H8MasterClipAlphaFromRawUv(float2 rawUv, float4 positionCS)");
            AssertContainsToken(shaderText, MasterShaderPath, "ShadowVaryings output = (ShadowVaryings)0;");
            AssertContainsToken(shaderText, MasterShaderPath, "DepthVaryings output = (DepthVaryings)0;");
            AssertContainsToken(shaderText, MasterShaderPath, "half4 ShadowFrag(ShadowVaryings input) : SV_Target");
            AssertContainsToken(shaderText, MasterShaderPath, "half4 DepthFrag(DepthVaryings input) : SV_Target");
            AssertContainsToken(shaderText, MasterShaderPath, "float3 _LightDirection;");
            AssertContainsToken(shaderText, MasterShaderPath, "float3 _LightPosition;");
            AssertContainsToken(shaderText, MasterShaderPath, "#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW");
            AssertContainsToken(shaderText, MasterShaderPath, "positionCS = ApplyShadowClamping(positionCS);");
            AssertContainsToken(shaderText, MasterShaderPath, "float2 H8MasterNormalizedScreenSpaceUv(float4 positionCS)");
            AssertContainsToken(shaderText, MasterShaderPath, "UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_90");
            AssertContainsToken(shaderText, MasterShaderPath, "inputData.normalizedScreenSpaceUV = H8MasterNormalizedScreenSpaceUv(input.positionCS);");
            AssertContainsToken(shaderText, MasterShaderPath, "float steps = floor(saturate(quality) * clamp(_MasterPomParams.y, 0.0, 16.0) + 0.5);");
            AssertContainsToken(shaderText, MasterShaderPath, "if (steps <= 0.0)");
            AssertContainsToken(shaderText, MasterShaderPath, "float heightScale = max(_MasterPomParams.x, 0.0) * quality;");
            AssertContainsToken(shaderText, MasterShaderPath, "for (int i = 0; i < 16; i++)");
            AssertContainsToken(shaderText, MasterShaderPath, "float active = step((float)i, steps - 0.5);");
            AssertContainsToken(shaderText, MasterShaderPath, "float2 H8MasterSafeRcp2(float2 value)");
            AssertContainsToken(shaderText, MasterShaderPath, "float2 parallaxRawDelta = parallaxDelta * H8MasterSafeRcp2(_BaseMap_ST.xy);");
            AssertContainsToken(shaderText, MasterShaderPath, "H8MasterNormalWS(input, TRANSFORM_TEX(input.uv + parallaxRawDelta, _BumpMap), quality)");
            AssertNoToken(shaderText, MasterShaderPath, "half emissionMask = packedMask.a;");
            AssertNoToken(shaderText, MasterShaderPath, "TRANSFORM_TEX(input.uv, _BumpMap) + parallaxDelta");
            AssertNoToken(shaderText, MasterShaderPath, "half4 ShadowFrag() : SV_Target");
            AssertNoToken(shaderText, MasterShaderPath, "half4 DepthFrag() : SV_Target");
            AssertNoToken(shaderText, MasterShaderPath, "ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz)");
            AssertNoToken(shaderText, MasterShaderPath, "inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);");

            hotMethodStats = AuditIntegratorProtocol();

            Debug.Log(
                "[HectonMasterShaderAudit1615] PASS cbufferBytes=" + cbufferBytes +
                " samples=" + sampleCount +
                " passes=" + passCount +
                " multiCompile=" + multiCompileCount +
                " shaderFeature=" + shaderFeatureCount +
                " expensiveMath=" + expensiveMathCount +
                " globalQualityTokens=" + globalQualityTokenCount +
                " hotDomainFiles=" + hotMethodStats.RuntimeFileCount +
                " hotMethodBodies=" + hotMethodStats.HotMethodBodyCount +
                " integrator=pass");
        }

        private static int CalculateUnityPerMaterialBytes(string shaderText, out string failure)
        {
            failure = string.Empty;
            int start = shaderText.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int end = shaderText.IndexOf("CBUFFER_END", StringComparison.Ordinal);
            if (start < 0 || end <= start)
            {
                failure = "CBUFFER block not found.";
                return -1;
            }

            string block = shaderText.Substring(start, end - start);
            string[] lines = block.Split('\n');
            int bytes = 0;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = StripComment(lines[lineIndex]).Trim();
                if (line.Length == 0 || line.StartsWith("CBUFFER_START", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("float4 ", StringComparison.Ordinal))
                {
                    if ((bytes & 15) != 0)
                    {
                        failure = "float4 at unaligned offset " + bytes + ".";
                        return -1;
                    }

                    bytes += 16;
                    continue;
                }

                if (line.StartsWith("float ", StringComparison.Ordinal))
                {
                    bytes += 4;
                    continue;
                }

                failure = "Unsupported CBUFFER declaration: " + line;
                return -1;
            }

            return bytes;
        }

        private static string ExtractUnityPerMaterialBlock(string shaderText)
        {
            int start = shaderText.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int end = shaderText.IndexOf("CBUFFER_END", StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException("FatalArchitectureException: UnityPerMaterial CBUFFER block not found.");

            return shaderText.Substring(start, end - start);
        }

        private static string ExtractMaterialPropertyRegion(string shaderText)
        {
            int subShader = shaderText.IndexOf("SubShader", StringComparison.Ordinal);
            if (subShader <= 0)
                throw new InvalidOperationException("FatalArchitectureException: SubShader block not found.");

            return shaderText.Substring(0, subShader);
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int next = text.IndexOf(needle, index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                count++;
                index = next + needle.Length;
            }

            return count;
        }

        private static int CountShaderPassBlocks(string shaderText)
        {
            int count = 0;
            string[] lines = shaderText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (StripComment(lines[i]).Trim() == "Pass")
                    count++;
            }

            return count;
        }

        private static int CountExpensiveMathCalls(string text)
        {
            return CountFunctionCalls(text, "pow") +
                   CountFunctionCalls(text, "sin") +
                   CountFunctionCalls(text, "cos") +
                   CountFunctionCalls(text, "sqrt") +
                   CountFunctionCalls(text, "tan") +
                   CountFunctionCalls(text, "asin") +
                   CountFunctionCalls(text, "acos") +
                   CountFunctionCalls(text, "atan");
        }

        private static int CountFunctionCalls(string text, string functionName)
        {
            int count = 0;
            int index = 0;
            string needle = functionName + "(";
            while (index < text.Length)
            {
                int next = text.IndexOf(needle, index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                char previous = next > 0 ? text[next - 1] : '\0';
                if (!IsIdentifierChar(previous))
                    count++;

                index = next + needle.Length;
            }

            return count;
        }

        private static bool IsIdentifierChar(char value)
        {
            return (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= '0' && value <= '9') ||
                   value == '_';
        }

        private static DomainHotMethodAuditStats AuditIntegratorProtocol()
        {
            string dispatcherText = File.ReadAllText(GlobalShaderDispatcherPath);
            string bridgeText = File.ReadAllText(ShaderGlobalBridgePath);
            string systemDispatcherText = File.ReadAllText(SystemDispatcherPath);
            DomainHotMethodAuditStats hotMethodStats;

            AssertNoForbiddenHotLookups(dispatcherText, GlobalShaderDispatcherPath);
            AssertNoForbiddenHotLookups(bridgeText, ShaderGlobalBridgePath);
            AssertSetGlobalRoute(dispatcherText, GlobalShaderDispatcherPath, "ExecuteGlobalDispatch");
            AssertSetGlobalRoute(bridgeText, ShaderGlobalBridgePath, "FlushFallbackVisualSync");
            AssertMethodContains(
                systemDispatcherText,
                SystemDispatcherPath,
                "RunDispatcherLateFrame",
                "UpdatePauseFreezeFrameDitherState();");
            AssertMethodContains(
                systemDispatcherText,
                SystemDispatcherPath,
                "RunDispatcherLateFrame",
                "UpdateVisualStaticGlitchState();");
            AssertMethodContains(
                systemDispatcherText,
                SystemDispatcherPath,
                "RunDispatcherLateFrame",
                "FlushSimulationBucketVisualSync();");
            AssertMethodContains(
                systemDispatcherText,
                SystemDispatcherPath,
                "RunDispatcherLateFrame",
                "HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync();");
            AssertMethodDoesNotContain(
                systemDispatcherText,
                SystemDispatcherPath,
                "PublishSimulationBucketSync",
                "Shader.SetGlobal");
            AssertMethodDoesNotContain(
                systemDispatcherText,
                SystemDispatcherPath,
                "RequestVisualStaticGlitch",
                "Shader.SetGlobal");
            AssertMethodContains(
                dispatcherText,
                GlobalShaderDispatcherPath,
                "LateFrameTick",
                "ExecuteGlobalDispatch(");
            AssertMutationGuardPattern(dispatcherText, GlobalShaderDispatcherPath, 5);
            AssertMutationGuardPattern(bridgeText, ShaderGlobalBridgePath, 1);
            AssertNoNestedDataVaultWriteLocks(dispatcherText, GlobalShaderDispatcherPath);
            AssertNoNestedDataVaultWriteLocks(bridgeText, ShaderGlobalBridgePath);
            AssertNoStringLiteralGlobalSetters(dispatcherText, GlobalShaderDispatcherPath);
            AssertNoStringLiteralGlobalSetters(bridgeText, ShaderGlobalBridgePath);
            AssertMaterialMigratorSafety();
            hotMethodStats = AssertDomainHotMethodsNoForbiddenLookups();
            return hotMethodStats;
        }

        private static void AssertMaterialMigratorSafety()
        {
            if (!File.Exists(MaterialMigratorPath))
                throw new InvalidOperationException("FatalArchitectureException: master material migrator is missing.");

            string source = File.ReadAllText(MaterialMigratorPath);
            AssertNoToken(source, MaterialMigratorPath, "EnableKeyword" + "(");
            AssertContainsToken(source, MaterialMigratorPath, "material.enableInstancing = true;");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetVector(\"_MasterPomParams\", new Vector4(0f, 0f, 0f, 1f));");
            AssertContainsToken(source, MaterialMigratorPath, "if (IsUnsupportedMasterSource(material, sourceShaderName))");
            AssertContainsToken(source, MaterialMigratorPath, "float alphaClipWeight = ResolveAlphaClipWeight(material);");
            AssertContainsToken(source, MaterialMigratorPath, "int targetRenderQueue = ResolveTargetRenderQueue(material, alphaClipWeight);");
            AssertContainsToken(source, MaterialMigratorPath, "MaskSemantics maskSemantics = ResolveMaskSemantics(maskSource, sourceShaderName, hasMask);");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetVector(\"_MasterAlphaParams\", new Vector4(Mathf.Clamp01(cutoff), 1f, 0.35f, alphaClipWeight));");
            AssertContainsToken(source, MaterialMigratorPath, "ApplySurfaceRouting(material, alphaClipWeight, targetRenderQueue);");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetOverrideTag(\"RenderType\", \"TransparentCutout\");");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetOverrideTag(\"RenderType\", \"Opaque\");");
            AssertContainsToken(source, MaterialMigratorPath, "material.renderQueue = targetRenderQueue;");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetVector(\"_MasterShadowParams\", new Vector4(1f, 0.15f, 0.18f, maskSemantics.Layout));");
            AssertContainsToken(source, MaterialMigratorPath, "return new MaskSemantics(1f, 1f, 0f, 0f, 2f);");
            AssertContainsToken(source, MaterialMigratorPath, "string sourceShaderName = material.shader != null ? material.shader.name : string.Empty;");
            AssertContainsToken(source, MaterialMigratorPath, "return new MaskSemantics(1f, 1f, 1f, 1f, 3f);");
            AssertContainsToken(source, MaterialMigratorPath, "\"_Base_Map\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"_Normal_Map\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"_Mask_Map\"");
            AssertContainsToken(source, MaterialMigratorPath, "private static readonly string[] UnsupportedExtraTextureNames");
            AssertContainsToken(source, MaterialMigratorPath, "if (HasAssignedTexture(material, UnsupportedExtraTextureNames))");
            AssertContainsToken(source, MaterialMigratorPath, "\"_HectonMicroNormalTex\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"_RustDetailMap\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"_H8UberNoirAlbedoArray\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"_ParasiteOverlayMap\"");
            AssertContainsToken(source, MaterialMigratorPath, "private static readonly string[] UnsupportedSourceShaderNameFragments");
            AssertContainsToken(source, MaterialMigratorPath, "\"GPUInstancer/\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"Indirect\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"/UI/\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"/Flora/\"");
            AssertContainsToken(source, MaterialMigratorPath, "\"Decal\"");
            AssertContainsToken(source, MaterialMigratorPath, "ContainsAnyFragment(sourceShaderName, UnsupportedSourceShaderNameFragments)");
            AssertContainsToken(source, MaterialMigratorPath, "private static bool ContainsAnyFragment(string source, string[] fragments)");
            AssertContainsToken(source, MaterialMigratorPath, "float metallicScale = Mathf.Clamp01(GetFloat(material, \"_Metallic\", GetFloat(material, \"_MetallicScale\", 0f)));");
            AssertContainsToken(source, MaterialMigratorPath, "float smoothnessScale = Mathf.Clamp01(GetFloat(material, \"_Smoothness\", GetFloat(material, \"_GlossMapScale\", GetFloat(material, \"_Glossiness\", 0.55f))));");
            AssertContainsToken(source, MaterialMigratorPath, "float roughnessScale = Mathf.Clamp01(GetFloat(material, \"_RoughnessScale\", 1f));");
            AssertContainsToken(source, MaterialMigratorPath, "if (!material.HasProperty(\"_BumpScale\") && material.HasProperty(\"_NormalStrength\"))");
            AssertContainsToken(source, MaterialMigratorPath, "normalScale *= Mathf.Max(0f, GetFloat(material, \"_NormalStrength\", 1f));");
            AssertContainsToken(source, MaterialMigratorPath, "ApplyMaskScalarCompatibility(");
            AssertContainsToken(source, MaterialMigratorPath, "metallicMapWeight *= metallicScale;");
            AssertContainsToken(source, MaterialMigratorPath, "roughnessMapWeight *= roughnessScale;");
            AssertContainsToken(source, MaterialMigratorPath, "roughnessMapWeight *= smoothnessScale;");
            AssertContainsToken(source, MaterialMigratorPath, "float normalScale = GetFloat(material, \"_BumpScale\", GetFloat(material, \"_NormalScale\", 1f));");
            AssertContainsToken(source, MaterialMigratorPath, "float emissionStrength = Mathf.Max(0f, GetFloat(material, \"_EmissionStrength\", 1f));");
            AssertContainsToken(source, MaterialMigratorPath, "emissionColor.r *= emissionStrength;");
            AssertContainsToken(source, MaterialMigratorPath, "emissionColor.a = HasVisibleEmission(emissionColor) && emissionStrength > 0.0001f ? maskSemantics.EmissionWeight : 0f;");
            AssertContainsToken(source, MaterialMigratorPath, "string.Equals(sourceName, \"_Mask_Map\", StringComparison.Ordinal)");
            AssertContainsToken(source, MaterialMigratorPath, "CopyTexture(material, \"_BaseMap\", baseMap, baseScale, baseOffset);");
            AssertContainsToken(source, MaterialMigratorPath, "CopyTexture(material, \"_BumpMap\", normalMap, normalScaleVector, normalOffset);");
            AssertContainsToken(source, MaterialMigratorPath, "CopyTexture(material, \"_MaskMap\", maskMap, maskScale, maskOffset);");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetTextureScale(targetName, scale);");
            AssertContainsToken(source, MaterialMigratorPath, "material.SetTextureOffset(targetName, offset);");
            AssertNoToken(source, MaterialMigratorPath, "float occlusionWeight = Mathf.Clamp01(occlusionStrength);");
            AssertNoToken(source, MaterialMigratorPath, "ResolveMaskSemantics(maskSource, sourceShaderName, hasMask, occlusion)");
            AssertNoToken(source, MaterialMigratorPath, "hasMask ? 1f : 0f, hasMask ? 1f : 0f");

            int stRead = source.IndexOf("Vector2 baseScale = GetTextureScale(material, baseSource);", StringComparison.Ordinal);
            int normalStRead = source.IndexOf("Vector2 normalScaleVector = GetTextureScale(material, normalSource);", StringComparison.Ordinal);
            int maskStRead = source.IndexOf("Vector2 maskScale = GetTextureScale(material, maskSource);", StringComparison.Ordinal);
            int shaderSwap = source.IndexOf("material.shader = masterShader;", StringComparison.Ordinal);
            int baseCopy = source.IndexOf("CopyTexture(material, \"_BaseMap\", baseMap, baseScale, baseOffset);", StringComparison.Ordinal);
            int normalCopy = source.IndexOf("CopyTexture(material, \"_BumpMap\", normalMap, normalScaleVector, normalOffset);", StringComparison.Ordinal);
            int maskCopy = source.IndexOf("CopyTexture(material, \"_MaskMap\", maskMap, maskScale, maskOffset);", StringComparison.Ordinal);
            if (stRead < 0 || normalStRead < 0 || maskStRead < 0 || shaderSwap < 0)
                throw new InvalidOperationException("FatalArchitectureException: material migrator must read every texture ST before shader swap.");
            if (stRead > shaderSwap || normalStRead > shaderSwap || maskStRead > shaderSwap)
                throw new InvalidOperationException("FatalArchitectureException: material migrator reads texture ST after shader swap.");
            if (baseCopy < shaderSwap || normalCopy < shaderSwap || maskCopy < shaderSwap)
                throw new InvalidOperationException("FatalArchitectureException: material migrator must write master textures and ST after shader swap.");
        }

        private static DomainHotMethodAuditStats AssertDomainHotMethodsNoForbiddenLookups()
        {
            DomainHotMethodAuditStats stats = default;
            for (int rootIndex = 0; rootIndex < DomainScriptRoots.Length; rootIndex++)
            {
                string root = DomainScriptRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = files[fileIndex].Replace('\\', '/');
                    if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    stats.RuntimeFileCount++;
                    string source = File.ReadAllText(files[fileIndex]);
                    AssertNoStringLiteralGlobalSetters(source, path);
                    AssertShaderGlobalWritesInAllowedRoutes(source, path);
                    AssertNoNestedDataVaultWriteLocks(source, path);
                    string sanitized = SanitizeCodePreservingLength(source);
                    for (int methodIndex = 0; methodIndex < HotMethodNames.Length; methodIndex++)
                    {
                        stats.HotMethodBodyCount += AssertAllNamedMethodBodiesDoNotContain(
                            sanitized,
                            path,
                            HotMethodNames[methodIndex],
                            ForbiddenHotLookupTokens);
                    }
                }
            }

            if (stats.RuntimeFileCount <= 0 || stats.HotMethodBodyCount <= 0)
                throw new InvalidOperationException("FatalArchitectureException: domain hot-method audit scanned no runtime files or no hot method bodies.");

            return stats;
        }

        private static void AssertNoForbiddenHotLookups(string source, string path)
        {
            AssertMethodDoesNotContain(source, path, "LateFrameTick", "GlobalRegistry.Get");
            AssertMethodDoesNotContain(source, path, "LateFrameTick", "GlobalRegistry.Resolve");
            AssertMethodDoesNotContain(source, path, "LateFrameTick", "GetComponent");
            AssertMethodDoesNotContain(source, path, "ExecuteGlobalDispatch", "GlobalRegistry.");
            AssertMethodDoesNotContain(source, path, "ExecuteGlobalDispatch", "GetComponent");
            AssertMethodDoesNotContain(source, path, "FlushFallbackVisualSync", "GlobalRegistry.");
            AssertMethodDoesNotContain(source, path, "FlushFallbackVisualSync", "GetComponent");
            AssertMethodDoesNotContain(source, path, "VisualSyncTick", "GlobalRegistry.");
            AssertMethodDoesNotContain(source, path, "VisualSyncTick", "GetComponent");
            AssertMethodDoesNotContain(source, path, "FixedUpdate", "GlobalRegistry.");
            AssertMethodDoesNotContain(source, path, "FixedUpdate", "GetComponent");
        }

        private static void AssertSetGlobalRoute(string source, string path, string ownerMethod)
        {
            string ownerBody = ExtractMethodBody(source, ownerMethod);
            int index = 0;
            while (index < source.Length)
            {
                int next = source.IndexOf("Shader.SetGlobal", index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                if (ownerBody.IndexOf("Shader.SetGlobal", StringComparison.Ordinal) < 0 ||
                    !IndexInsideMethod(source, ownerMethod, next))
                {
                    throw new InvalidOperationException("FatalArchitectureException: Shader.SetGlobal outside " + ownerMethod + " in " + path + ".");
                }

                index = next + "Shader.SetGlobal".Length;
            }
        }

        private static void AssertNoStringLiteralGlobalSetters(string source, string path)
        {
            string commentFreeSource = SanitizeCommentsPreservingLength(source);
            for (int i = 0; i < StringLiteralGlobalSetterNames.Length; i++)
            {
                AssertNoStringLiteralFirstArgument(commentFreeSource, path, StringLiteralGlobalSetterNames[i]);
            }
        }

        private static void AssertNoStringLiteralFirstArgument(string source, string path, string methodName)
        {
            int index = 0;
            while (index < source.Length)
            {
                int next = source.IndexOf(methodName, index, StringComparison.Ordinal);
                if (next < 0)
                    return;

                char previous = next > 0 ? source[next - 1] : '\0';
                int afterName = next + methodName.Length;
                char following = afterName < source.Length ? source[afterName] : '\0';
                if (IsIdentifierChar(previous) || IsIdentifierChar(following))
                {
                    index = afterName;
                    continue;
                }

                int cursor = afterName;
                while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
                    cursor++;

                if (cursor >= source.Length || source[cursor] != '(')
                {
                    index = afterName;
                    continue;
                }

                cursor++;
                while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
                    cursor++;

                if (cursor < source.Length && source[cursor] == '"')
                    throw new InvalidOperationException("FatalArchitectureException: " + methodName + " uses a string literal global property in " + path + ".");

                index = afterName;
            }
        }

        private static void AssertShaderGlobalWritesInAllowedRoutes(string source, string path)
        {
            string sanitized = SanitizeCodePreservingLength(source);
            int index = 0;
            while (index < sanitized.Length)
            {
                int next = sanitized.IndexOf("Shader.SetGlobal", index, StringComparison.Ordinal);
                if (next < 0)
                    return;

                bool allowed = false;
                for (int i = 0; i < AllowedShaderGlobalWriteMethods.Length; i++)
                {
                    if (IndexInsideMethod(sanitized, AllowedShaderGlobalWriteMethods[i], next))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    throw new InvalidOperationException("FatalArchitectureException: Shader.SetGlobal outside approved VISUAL_SYNC/LateFrame/cold route in " + path + ".");

                index = next + "Shader.SetGlobal".Length;
            }
        }

        private static void AssertMutationGuardPattern(string source, string path, int expectedAcquireCount)
        {
            int acquireCount = CountOccurrences(source, "TryAcquireMutationGuard(");
            if (acquireCount != expectedAcquireCount)
                throw new InvalidOperationException("FatalArchitectureException: unexpected mutation guard count in " + path + ".");

            int index = 0;
            while (index < source.Length)
            {
                int next = source.IndexOf("TryAcquireMutationGuard(", index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                int release = source.IndexOf("ReleaseMutationGuard(", next, StringComparison.Ordinal);
                int nestedAcquire = source.IndexOf("TryAcquireMutationGuard(", next + "TryAcquireMutationGuard(".Length, StringComparison.Ordinal);
                int nestedWriteAcquire = source.IndexOf("TryAcquireWriteLock(", next + "TryAcquireMutationGuard(".Length, StringComparison.Ordinal);
                int tryIndex = source.IndexOf("try", next, StringComparison.Ordinal);
                int finallyIndex = source.IndexOf("finally", next, StringComparison.Ordinal);
                if (release < 0 ||
                    tryIndex < 0 ||
                    finallyIndex < 0 ||
                    tryIndex > release ||
                    finallyIndex > release)
                {
                    throw new InvalidOperationException("FatalArchitectureException: mutation guard lacks strict try/finally release in " + path + ".");
                }

                if (nestedAcquire >= 0 && nestedAcquire < release)
                    throw new InvalidOperationException("FatalArchitectureException: nested mutation guard acquire before release in " + path + ".");

                if (nestedWriteAcquire >= 0 && nestedWriteAcquire < release)
                    throw new InvalidOperationException("FatalArchitectureException: DataVault write lock acquire while mutation guard is held in " + path + ".");

                index = next + "TryAcquireMutationGuard(".Length;
            }
        }

        private static void AssertNoNestedDataVaultWriteLocks(string source, string path)
        {
            string sanitized = SanitizeCodePreservingLength(source);
            int index = 0;
            int heldAcquireIndex = -1;
            string heldAcquireToken = string.Empty;
            while (index < sanitized.Length)
            {
                int acquireIndex;
                int releaseIndex;
                string acquireToken;
                string releaseToken;
                bool hasAcquire = TryFindNextToken(sanitized, DataVaultWriteAcquireTokens, index, out acquireIndex, out acquireToken);
                bool hasRelease = TryFindNextToken(sanitized, DataVaultWriteReleaseTokens, index, out releaseIndex, out releaseToken);
                if (!hasAcquire && !hasRelease)
                    return;

                if (hasAcquire && (!hasRelease || acquireIndex < releaseIndex))
                {
                    if (heldAcquireIndex >= 0)
                    {
                        throw new InvalidOperationException(
                            "FatalArchitectureException: nested DataVault write lock acquire " +
                            acquireToken +
                            " while " +
                            heldAcquireToken +
                            " is held in " +
                            path +
                            ".");
                    }

                    heldAcquireIndex = acquireIndex;
                    heldAcquireToken = acquireToken;
                    index = acquireIndex + acquireToken.Length;
                    continue;
                }

                if (hasRelease)
                {
                    heldAcquireIndex = -1;
                    heldAcquireToken = string.Empty;
                    index = releaseIndex + releaseToken.Length;
                    continue;
                }

                return;
            }
        }

        private static bool TryFindNextToken(string source, string[] tokens, int startIndex, out int tokenIndex, out string token)
        {
            tokenIndex = -1;
            token = string.Empty;
            for (int i = 0; i < tokens.Length; i++)
            {
                int next = source.IndexOf(tokens[i], startIndex, StringComparison.Ordinal);
                if (next < 0)
                    continue;

                if (tokenIndex < 0 || next < tokenIndex)
                {
                    tokenIndex = next;
                    token = tokens[i];
                }
            }

            return tokenIndex >= 0;
        }

        private static void AssertMethodContains(string source, string path, string methodName, string requiredToken)
        {
            string body = ExtractMethodBody(source, methodName);
            if (body.Length == 0)
                return;

            if (body.IndexOf(requiredToken, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("FatalArchitectureException: " + methodName + " does not contain " + requiredToken + " in " + path + ".");
        }

        private static void AssertMethodDoesNotContain(string source, string path, string methodName, string forbiddenToken)
        {
            string body = ExtractMethodBody(source, methodName);
            if (body.Length == 0)
                return;

            if (body.IndexOf(forbiddenToken, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("FatalArchitectureException: " + methodName + " contains forbidden token " + forbiddenToken + " in " + path + ".");
        }

        private static int AssertAllNamedMethodBodiesDoNotContain(string source, string path, string methodName, string[] forbiddenTokens)
        {
            int searchIndex = 0;
            int bodyCount = 0;
            while (searchIndex < source.Length)
            {
                int bodyStart;
                int bodyEnd;
                int declaration = FindMethodDeclarationIndex(source, methodName, searchIndex);
                if (declaration < 0)
                    return bodyCount;

                if (!TryFindMethodBodyRange(source, methodName, declaration, out bodyStart, out bodyEnd))
                {
                    searchIndex = declaration + methodName.Length;
                    continue;
                }

                bodyCount++;
                string body = source.Substring(bodyStart, bodyEnd - bodyStart);
                for (int tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    string token = forbiddenTokens[tokenIndex];
                    if (body.IndexOf(token, StringComparison.Ordinal) >= 0)
                        throw new InvalidOperationException("FatalArchitectureException: hot method " + methodName + " contains forbidden token " + token + " in " + path + ".");
                }

                searchIndex = bodyEnd;
            }

            return bodyCount;
        }

        private static void AssertNoToken(string source, string path, string forbiddenToken)
        {
            if (source.IndexOf(forbiddenToken, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("FatalArchitectureException: forbidden token " + forbiddenToken + " in " + path + ".");
        }

        private static void AssertContainsToken(string source, string path, string requiredToken)
        {
            if (source.IndexOf(requiredToken, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("FatalArchitectureException: missing token " + requiredToken + " in " + path + ".");
        }

        private static bool IndexInsideMethod(string source, string methodName, int index)
        {
            int bodyStart;
            int bodyEnd;
            return TryFindMethodBodyRange(source, methodName, out bodyStart, out bodyEnd) &&
                   index >= bodyStart &&
                   index < bodyEnd;
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int bodyStart;
            int bodyEnd;
            return TryFindMethodBodyRange(source, methodName, out bodyStart, out bodyEnd)
                ? source.Substring(bodyStart, bodyEnd - bodyStart)
                : string.Empty;
        }

        private static bool TryFindMethodBodyRange(string source, string methodName, out int bodyStart, out int bodyEnd)
        {
            bodyStart = -1;
            bodyEnd = -1;
            int declaration = FindMethodDeclarationIndex(source, methodName);
            if (declaration < 0)
                return false;

            return TryFindMethodBodyRange(source, methodName, declaration, out bodyStart, out bodyEnd);
        }

        private static bool TryFindMethodBodyRange(string source, string methodName, int declaration, out int bodyStart, out int bodyEnd)
        {
            bodyStart = -1;
            bodyEnd = -1;
            bodyStart = source.IndexOf(OpenBrace, declaration);
            if (bodyStart < 0)
                return false;

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char value = source[i];
                if (value == OpenBrace)
                {
                    depth++;
                    continue;
                }

                if (value != CloseBrace)
                    continue;

                depth--;
                if (depth == 0)
                {
                    bodyEnd = i + 1;
                    return true;
                }
            }

            return false;
        }

        private static int FindMethodDeclarationIndex(string source, string methodName)
        {
            return FindMethodDeclarationIndex(source, methodName, 0);
        }

        private static int FindMethodDeclarationIndex(string source, string methodName, int startIndex)
        {
            int index = 0;
            if (startIndex > 0)
                index = startIndex;

            string needle = methodName + "(";
            while (index < source.Length)
            {
                int next = source.IndexOf(needle, index, StringComparison.Ordinal);
                if (next < 0)
                    return -1;

                if (next > 0 && IsIdentifierChar(source[next - 1]))
                {
                    index = next + needle.Length;
                    continue;
                }

                int lineStart = source.LastIndexOf('\n', next);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                string prefix = source.Substring(lineStart, next - lineStart).TrimStart();
                if (prefix.StartsWith("public ", StringComparison.Ordinal) ||
                    prefix.StartsWith("private ", StringComparison.Ordinal) ||
                    prefix.StartsWith("internal ", StringComparison.Ordinal) ||
                    prefix.StartsWith("protected ", StringComparison.Ordinal) ||
                    prefix.StartsWith("void ", StringComparison.Ordinal) ||
                    prefix.IndexOf(" void ", StringComparison.Ordinal) >= 0)
                {
                    return next;
                }

                index = next + needle.Length;
            }

            return -1;
        }

        private static string SanitizeCodePreservingLength(string source)
        {
            char[] chars = source.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool normalString = false;
            bool verbatimString = false;
            bool character = false;
            bool escape = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

                if (lineComment)
                {
                    if (current == '\r' || current == '\n')
                        lineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        blockComment = false;
                    }
                    else if (current != '\r' && current != '\n')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (normalString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (current == '\\')
                    {
                        escape = true;
                    }
                    else if (current == '"')
                    {
                        normalString = false;
                    }

                    if (current != '\r' && current != '\n')
                        chars[i] = ' ';
                    continue;
                }

                if (verbatimString)
                {
                    if (current == '"' && next == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (current == '"')
                        verbatimString = false;

                    if (current != '\r' && current != '\n')
                        chars[i] = ' ';
                    continue;
                }

                if (character)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (current == '\\')
                    {
                        escape = true;
                    }
                    else if (current == '\'')
                    {
                        character = false;
                    }

                    if (current != '\r' && current != '\n')
                        chars[i] = ' ';
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    verbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    chars[i] = ' ';
                    normalString = true;
                    escape = false;
                    continue;
                }

                if (current == '\'')
                {
                    chars[i] = ' ';
                    character = true;
                    escape = false;
                }
            }

            return new string(chars);
        }

        private static string SanitizeCommentsPreservingLength(string source)
        {
            char[] chars = source.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool normalString = false;
            bool verbatimString = false;
            bool character = false;
            bool escape = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

                if (lineComment)
                {
                    if (current == '\r' || current == '\n')
                        lineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        blockComment = false;
                    }
                    else if (current != '\r' && current != '\n')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (normalString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (current == '\\')
                    {
                        escape = true;
                    }
                    else if (current == '"')
                    {
                        normalString = false;
                    }
                    continue;
                }

                if (verbatimString)
                {
                    if (current == '"' && next == '"')
                    {
                        i++;
                        continue;
                    }

                    if (current == '"')
                        verbatimString = false;
                    continue;
                }

                if (character)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (current == '\\')
                    {
                        escape = true;
                    }
                    else if (current == '\'')
                    {
                        character = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    i++;
                    verbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    normalString = true;
                    escape = false;
                    continue;
                }

                if (current == '\'')
                {
                    character = true;
                    escape = false;
                }
            }

            return new string(chars);
        }
    }
}
#endif
