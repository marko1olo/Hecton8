Shader "Hecton8/VFX/FlashlightConeSilt"
{
    Properties
    {
        _BeamColor ("Beam Color", Color) = (0.28, 0.58, 0.72, 1)
        _BeamParams ("Intensity CellScale Reserved DepthFade", Vector) = (0.18, 2.6, 0.42, 2.8)
        _BeamShape ("NearFade TipFade Reserved Reserved", Vector) = (0.08, 0.86, 0, 0)
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                float4 _BeamParams;
                float4 _BeamShape;
            CBUFFER_END

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

                float2 screenUV = input.screenPos.xy * rcp(max(input.screenPos.w, 0.0001));
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float coneEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthFade = saturate((sceneEyeDepth - coneEyeDepth) * max(_BeamParams.w, 0.01));

                float axial01 = saturate(input.positionOS.z);
                float radialSq = saturate(dot(input.positionOS.xy, input.positionOS.xy));
                float nearFade = smoothstep(_BeamShape.x, _BeamShape.x + 0.12, axial01);
                float tipFade = 1.0 - smoothstep(_BeamShape.y, 1.0, axial01);
                float edge01 = saturate(1.0 - radialSq);
                float edgeFade = edge01 * edge01;
                float axialFade = axial01 * (2.0 - axial01);

                float noiseScale = max(_BeamParams.y, 0.001);
                float2 siltCell = floor(input.positionOS.xz * noiseScale);
                float siltNoise = Hash21(siltCell);
                float silt = step(0.38, siltNoise);

                half alpha = (half)(nearFade * tipFade * edgeFade * axialFade * depthFade * silt * max(_BeamParams.x, 0.0));
                clip(alpha - max((half)HectonDitherCoverage(input.positionCS.xy), 0.0005h));
                return half4(_BeamColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
