Shader "Hecton8/UI/PDA Sonar Point Cloud"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 2.5
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _DepthFadeMeters ("Depth Fade Meters", Float) = 0.08
        _DeepColor ("Deep Color", Color) = (0.02, 0.12, 0.34, 1)
        _HighColor ("High Color", Color) = (1.0, 0.32, 0.06, 1)
        _PredatorColor ("Predator Color", Color) = (1.0, 0.05, 0.02, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "PDASonarPointCloud"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            StructuredBuffer<float4> _SonarPoints;

            CBUFFER_START(UnityPerMaterial)
                float4x4 _PointCloudLocalToWorld;
                float4 _AcousticPingSignal;
                float4 _DeepColor;
                float4 _HighColor;
                float4 _PredatorColor;
                float _PointSize;
                float _Opacity;
                float _DepthFadeMeters;
                float _HeightColorization;
                float _ActiveSonarRadius;
                float _ActiveSonarMaxRange;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 color : COLOR0;
                float clipAlpha : TEXCOORD1;
            };

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 0.000001f ? value * rsqrt(lengthSq) : fallback;
            }

            Varyings vert(Attributes input)
            {
                float4 pointData = _SonarPoints[input.instanceId];
                float3 localCenter = pointData.xyz;
                float3 signalNoise = frac((float(input.instanceId) + float3(17.0f, 59.0f, 113.0f)) * 0.1031f + _AcousticPingSignal.z * 0.071f);
                localCenter += (signalNoise - 0.5f) * 0.006f;
                bool predator = pointData.w < 0.0f;
                float intensity = saturate(abs(pointData.w));

                float3 worldCenter = mul(_PointCloudLocalToWorld, float4(localCenter, 1.0f)).xyz;
                float3 cameraRight = SafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0f, 0.0f, 0.0f));
                float3 cameraUp = SafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0f, 1.0f, 0.0f));
                float quadScale = max(_PointSize, 0.25f) * 0.0015f * lerp(1.0f, 1.65f, predator ? 1.0f : 0.0f);
                float3 worldPosition = worldCenter + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * quadScale;

                float localDistance = max(max(abs(localCenter.x), abs(localCenter.y)), abs(localCenter.z));
                float activeRadius01 = saturate(_ActiveSonarRadius * rcp(max(_ActiveSonarMaxRange, 0.001f)));
                float pingRadius = _AcousticPingSignal.w > 0.5f ? activeRadius01 : saturate(_AcousticPingSignal.x);
                float pingWidth = max(_AcousticPingSignal.y, 0.001f);
                float insidePing = _AcousticPingSignal.w > 0.5f ? step(localDistance, pingRadius) : 1.0f;
                float pingBand = 1.0f - saturate(abs(localDistance - pingRadius) * rcp(pingWidth));
                float pingBoost = 0.55f + pingBand * 0.45f;
                float sweepX = lerp(-0.52f, 0.52f, frac(_Time.y * 0.18f));
                float sweepLine = 1.0f - saturate(abs(localCenter.x - sweepX) * 38.0f);

                float height01 = saturate(localCenter.z * 2.0f + 0.5f);
                float3 heightColor = lerp(_DeepColor.rgb, _HighColor.rgb, height01);
                float3 defaultColor = float3(0.18f, 0.95f, 1.0f);
                float3 sonarColor = lerp(defaultColor, heightColor, saturate(_HeightColorization));
                sonarColor = predator ? _PredatorColor.rgb : sonarColor;
                sonarColor = saturate(sonarColor * (1.0f + sweepLine * 0.65f));

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = float4(sonarColor, saturate(intensity * _Opacity * insidePing * max(pingBoost, 0.72f + sweepLine * 0.28f)));
                output.clipAlpha = output.color.a;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPos.xy * rcp(max(input.screenPos.w, 0.0001f));
                float sceneRawDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float particleEyeDepth = max(input.screenPos.w, 0.0001f);
                float depthFade = saturate((sceneEyeDepth - particleEyeDepth) * rcp(max(_DepthFadeMeters, 0.0001f)));
                float alpha = input.clipAlpha * depthFade;
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.001));
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
