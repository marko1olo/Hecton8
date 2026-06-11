Shader "Hecton8/VFX/FlashlightConeSilt"
{
    Properties
    {
        _BeamColor ("Beam Color", Color) = (0.28, 0.58, 0.72, 1)
        _BeamParams ("Intensity CellScale Reserved DepthFade", Vector) = (0.18, 2.6, 0.42, 2.8)
        _BeamShape ("NearFade TipFade Reserved Reserved", Vector) = (0.08, 0.86, 0, 0)
        [NoScaleOffset] _SiltMaskAtlas ("Baked Silt Mask Atlas (R Density G Bio B Flow A AO)", 2D) = "white" {}
        [NoScaleOffset] _SiltNormalAtlas ("Baked Silt Normal Atlas", 2D) = "bump" {}
        _SiltAtlasParams ("Atlas Columns Rows NormalWeight MaskWeight", Vector) = (8, 8, 0, 0)
        _SiltFlipbookParams ("Flipbook TimeScale AxialDrift NormalGain Reserved", Vector) = (0.16, 0.11, 0.45, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Back
        AlphaToMask Off

        Pass
        {
            Name "FlashlightConeSilt"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                float4 _BeamParams;
                float4 _BeamShape;
                float4 _SiltAtlasParams;
                float4 _SiltFlipbookParams;
            CBUFFER_END
            TEXTURE2D(_SiltMaskAtlas);
            SAMPLER(sampler_SiltMaskAtlas);
            TEXTURE2D(_SiltNormalAtlas);
            SAMPLER(sampler_SiltNormalAtlas);
            float4 _SiltMaskAtlas_TexelSize;
            float4 _HectonFlashlightFailureState;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float HectonLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) * rcp(max(0.0001, edge1 - edge0)));
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            float ResolveFlashlightFailureFlicker(float3 positionOS, float2 positionCS)
            {
                float battery01 = saturate(_HectonFlashlightFailureState.x);
                float thermal01 = saturate(_HectonFlashlightFailureState.y);
                float failure01 = saturate(_HectonFlashlightFailureState.z);
                float heatCarrier = Hash21(floor(positionOS.xz * lerp(2.0, 9.0, thermal01)) + floor(_Time.y * lerp(8.0, 27.0, failure01)));
                float lowBatteryDrop = saturate((0.22 - battery01) * 4.5454545);
                float triangle = 1.0 - abs(frac((_Time.y * lerp(4.0, 19.0, failure01)) + heatCarrier) * 2.0 - 1.0);
                float dropout = lerp(0.58, 0.18, max(lowBatteryDrop, thermal01 * thermal01));
                float shimmer = lerp(1.0, lerp(dropout, 1.0, triangle * triangle), max(failure01, lowBatteryDrop));
                float screenSpark = lerp(1.0, step(0.06 + failure01 * 0.22, HectonDitherCoverage(positionCS)), failure01 * 0.2);
                return saturate(shimmer * screenSpark);
            }

            float2 ResolveSiltFrameLocalUv(float2 localUv)
            {
                float2 grid = max(_SiltAtlasParams.xy, 1.0.xx);
                float2 halfTexelInFrame = min(_SiltMaskAtlas_TexelSize.xy * grid * 0.5, float2(0.125, 0.125));
                halfTexelInFrame = max(halfTexelInFrame, float2(0.00012207, 0.00012207));
                return saturate(localUv) * saturate(1.0.xx - halfTexelInFrame * 2.0) + halfTexelInFrame;
            }

            float2 ResolveSiltFlipbookUV(float3 positionOS)
            {
                float2 grid = max(_SiltAtlasParams.xy, 1.0.xx);
                float frameCount = max(grid.x * grid.y, 1.0);
                float2 localUv = ResolveSiltFrameLocalUv(frac(positionOS.xz * max(_BeamParams.y, 0.001) + float2(_Time.y * _SiltFlipbookParams.y, 0.0)));
                float phase = _Time.y * max(_SiltFlipbookParams.x, 0.0) + Hash21(floor(positionOS.xz * 3.0)) * 0.37;
                float frame = floor(frac(phase) * frameCount);
                float row = floor(frame * rcp(max(grid.x, 1.0)));
                float col = frame - row * grid.x;
                return (float2(col, row) + localUv) * rcp(grid);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = UnityStereoTransformScreenSpaceTex(input.screenPos.xy * rcp(max(input.screenPos.w, 0.0001)));
                float rawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float coneEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthFade = saturate((sceneEyeDepth - coneEyeDepth) * max(_BeamParams.w, 0.01));

                float axial01 = saturate(input.positionOS.z);
                float radialSq = saturate(dot(input.positionOS.xy, input.positionOS.xy));
                float nearFade = HectonLinearRamp01(_BeamShape.x, _BeamShape.x + 0.12, axial01);
                float tipFade = 1.0 - HectonLinearRamp01(_BeamShape.y, 1.0, axial01);
                float edge01 = saturate(1.0 - radialSq);
                float edgeFade = edge01 * edge01;
                float axialFade = axial01 * (2.0 - axial01);

                float noiseScale = max(_BeamParams.y, 0.001);
                float2 siltCell = floor(input.positionOS.xz * noiseScale);
                float siltNoise = Hash21(siltCell);
                float hashSilt = step(0.38, siltNoise);
                float2 atlasUV = ResolveSiltFlipbookUV(input.positionOS);
                float4 maskPacked = SAMPLE_TEXTURE2D(_SiltMaskAtlas, sampler_SiltMaskAtlas, atlasUV);
                float maskWeight = saturate(_SiltAtlasParams.w);
                float silt = lerp(hashSilt, saturate(maskPacked.r), maskWeight);

                float failureFlicker = ResolveFlashlightFailureFlicker(input.positionOS, input.positionCS.xy);
                half alpha = (half)(nearFade * tipFade * edgeFade * axialFade * depthFade * silt * failureFlicker * max(_BeamParams.x, 0.0));
                clip(alpha - max((half)HectonDitherCoverage(input.positionCS.xy), 0.0005h));
                float2 flowOffset = (maskPacked.b * 2.0 - 1.0) * maskWeight * _SiltMaskAtlas_TexelSize.xy * float2(2.0, -1.35);
                float4 normalPacked = SAMPLE_TEXTURE2D(_SiltNormalAtlas, sampler_SiltNormalAtlas, atlasUV + flowOffset);
                float3 normalTS = normalize((float3)UnpackNormal(normalPacked));
                float2 beamDirection = normalize(float2(input.positionOS.x, input.positionOS.z) + flowOffset * _SiltAtlasParams.xy * 8.0 + float2(0.0001, 0.0001));
                float normalLight = lerp(1.0, lerp(0.72, 1.36, saturate(normalTS.z * 0.74 + dot(normalTS.xy, beamDirection) * 0.26)), saturate(_SiltAtlasParams.z) * saturate(_SiltFlipbookParams.z));
                half3 beamRgb = _BeamColor.rgb * (half)(normalLight + maskPacked.g * maskWeight * 0.18);
                return half4(beamRgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
