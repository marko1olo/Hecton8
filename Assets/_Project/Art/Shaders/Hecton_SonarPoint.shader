Shader "Hecton8/VFX/TopographicalSonarPoint"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 3.2
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _DepthFadeMeters ("Depth Fade Meters", Float) = 0.12
        _MaxDistanceMeters ("Max Distance Meters", Float) = 120
        _GlobalQualityWeight ("Global Quality Weight", Range(0, 1)) = 0.5
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
            Name "TopographicalSonarPoint"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct SonarPointDTO
            {
                float3 LocalPosition;
                uint ColorPacked;
            };

            StructuredBuffer<SonarPointDTO> _SonarPoints;

            CBUFFER_START(UnityPerMaterial)
                float4x4 _PointCloudLocalToWorld;
                float4 _PingSignal;
                float _PointSize;
                float _Opacity;
                float _DepthFadeMeters;
                float _MaxDistanceMeters;
                float _GlobalQualityWeight;
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

            float4 UnpackColor(uint packed)
            {
                float r = (float)(packed & 255u) * rcp(255.0f);
                float g = (float)((packed >> 8) & 255u) * rcp(255.0f);
                float b = (float)((packed >> 16) & 255u) * rcp(255.0f);
                float a = (float)((packed >> 24) & 255u) * rcp(255.0f);
                return float4(r, g, b, a);
            }

            Varyings vert(Attributes input)
            {
                SonarPointDTO point = _SonarPoints[input.instanceId];
                float4 color = UnpackColor(point.ColorPacked);
                float3 cameraLocal = point.LocalPosition;
                float3 noise = frac((float(input.instanceId) + float3(17.0f, 59.0f, 113.0f)) * 0.1031f + _Time.y * 0.071f);
                cameraLocal += (noise - 0.5f) * lerp(0.002f, 0.012f, saturate(_GlobalQualityWeight));

                float3 worldCenter = mul(_PointCloudLocalToWorld, float4(cameraLocal, 1.0f)).xyz;
                float3 cameraRight = SafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0f, 0.0f, 0.0f));
                float3 cameraUp = SafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0f, 1.0f, 0.0f));
                float distanceMeters = max(max(abs(cameraLocal.x), abs(cameraLocal.y)), abs(cameraLocal.z));
                float distance01 = saturate(distanceMeters * rcp(max(_MaxDistanceMeters, 0.001f)));
                float pointScale = max(_PointSize, 0.2f) * 0.0015f * lerp(0.75f, 1.45f, saturate(_GlobalQualityWeight));
                float3 worldPosition = worldCenter + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * pointScale;

                float pingAge = max(0.0f, _PingSignal.x);
                float fadeSeconds = max(0.001f, _PingSignal.y);
                float waveRadius = pingAge * max(18.0f, _MaxDistanceMeters * 0.55f);
                float waveBand = 1.0f - saturate(abs(distanceMeters - waveRadius) * rcp(lerp(1.2f, 6.5f, saturate(_GlobalQualityWeight))));
                float echoFade = saturate(1.0f - pingAge * rcp(fadeSeconds));
                float boost = 0.55f + waveBand * lerp(0.45f, 1.35f, saturate(_GlobalQualityWeight));

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = float4(saturate(color.rgb * boost), color.a * _Opacity * echoFade * saturate(1.0f - distance01 * 0.3f));
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
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.001f));
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
