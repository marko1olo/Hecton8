Shader "Hecton8/UI/PDA Sonar Map"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MapTint ("Map Tint", Color) = (0.18, 0.94, 0.96, 0.28)
        _EdgeTint ("Edge Tint", Color) = (0.62, 0.98, 1.0, 0.86)
        _ThreatTint ("Threat Tint", Color) = (1.0, 0.18, 0.14, 0.82)
        _SdfVolume ("SDF Volume", 3D) = "" {}
        _SdfRange ("SDF Range", Float) = 1
        _GridDimensions ("Grid Dimensions", Vector) = (32,32,32,0)
        _VolumeHalfExtent ("Volume Half Extent", Vector) = (0.55,0.55,0.55,0)
        _TimePhase ("Time Phase", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "PDASonarMap"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MapTint;
                float4 _EdgeTint;
                float4 _ThreatTint;
                float4 _GridDimensions;
                float4 _VolumeHalfExtent;
                float _SdfRange;
                float _TimePhase;
                int _ThreatPingCount;
                float4 _ThreatPings[8];
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE3D(_SdfVolume);
            SAMPLER(sampler_SdfVolume);
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;
            float _H8GlobalQualityWeight;
            static const int PDA_SONAR_MARCH_STEPS = 24;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float DecodeSdf(float3 uvw)
            {
                float encoded = SAMPLE_TEXTURE3D(_SdfVolume, sampler_SdfVolume, saturate(uvw)).r;
                return ((encoded * 2.0) - 1.0) * max(_SdfRange, 0.001);
            }

            float3 ApproximateUnitDirectionDiamond3(float3 value, float3 fallback)
            {
                float3 absValue = abs(value);
                float radius = absValue.x + absValue.y + absValue.z;
                float3 direction = value * rcp(max(radius, 0.0001));
                return radius > 0.0001 ? direction : fallback;
            }

            float FastTriangleSine01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) * rcp(max(edge1 - edge0, 0.00001)));
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveHectonComfortEyeStableScreenUV(float2 positionCS)
            {
                float2 screenUV = saturate(positionCS * rcp(max(_ScreenParams.xy, float2(1.0, 1.0))));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                float4 stereoScaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                screenUV = (screenUV - stereoScaleOffset.zw) * rcp(max(stereoScaleOffset.xy, float2(0.0001, 0.0001)));
#endif
                return saturate(screenUV);
            }

            float ResolveHectonComfortBlackAmount(float2 screenUV, float2 positionCS)
            {
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float somaticTunnel = saturate(_HectonVRSomaticComfortState.x);
                float vrComfortTunnel = saturate(max(max(_HectonVrComfortSignals.x, _HectonVrComfortMotion.z) * vrComfortEnabled, max(_HectonTunnelingIntensity, somaticTunnel)));
                float vrComfortBlackout = saturate(max(_HectonVrComfortSignals.y * vrComfortEnabled, _HectonVRBrownoutIntensity));
                float2 radial = screenUV * 2.0 - 1.0;
                radial.x *= _ScreenParams.x * rcp(max(_ScreenParams.y, 1.0));
                float radialMagnitudeSq = saturate(dot(radial, radial));
                float tunnelInner = lerp(0.74, 0.34, vrComfortTunnel);
                float tunnelInnerSq = tunnelInner * tunnelInner;
                float tunnelMask = saturate((radialMagnitudeSq - tunnelInnerSq) * rcp(max(1.0 - tunnelInnerSq, 0.0009765625))) * vrComfortTunnel;
                float ign = HectonDitherCoverage(positionCS);
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            float FastPow24(float value)
            {
                float v2 = value * value;
                return v2 * lerp(1.0, value, 0.4);
            }

            float FastPow6(float value)
            {
                float v2 = value * value;
                float v4 = v2 * v2;
                return v4 * v2;
            }

            bool TryIntersectVolume(float3 rayOrigin, float3 rayDirection, float3 volumeMin, float3 volumeMax, out float enterDistance, out float exitDistance)
            {
                float3 safeDirection = rayDirection + (abs(rayDirection) < 1e-5.xxx ? 1e-5.xxx : 0.0.xxx);
                float3 inverseDirection = rcp(safeDirection);
                float3 t0 = (volumeMin - rayOrigin) * inverseDirection;
                float3 t1 = (volumeMax - rayOrigin) * inverseDirection;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                enterDistance = max(max(tMin.x, tMin.y), tMin.z);
                exitDistance = min(min(tMax.x, tMax.y), tMax.z);
                return exitDistance > max(enterDistance, 0.0);
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 centeredUv = (i.uv * 2.0) - 1.0;
                float3 volumeHalfExtent = max(_VolumeHalfExtent.xyz, 0.05.xxx);
                float3 volumeMin = -volumeHalfExtent;
                float3 volumeMax = volumeHalfExtent;
                float3 rayOrigin = float3(centeredUv * (volumeHalfExtent.xy * 1.75), -(volumeHalfExtent.z + 0.78));
                float3 rayDirection = ApproximateUnitDirectionDiamond3(float3(centeredUv * float2(0.18, 0.21), 1.45), float3(0.0, 0.0, 1.0));
                float enterDistance;
                float exitDistance;
                if (!TryIntersectVolume(rayOrigin, rayDirection, volumeMin, volumeMax, enterDistance, exitDistance))
                {
                    clip(-1.0);
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                float marchDistance = max(enterDistance, 0.0);
                float marchLength = max(exitDistance - marchDistance, 0.0);
                float marchStep = max(marchLength * rcp((float)PDA_SONAR_MARCH_STEPS), 0.0105);
                float3 position = rayOrigin + rayDirection * marchDistance;

                float3 mapColor = 0.0;
                float mapAlpha = 0.0;
                float threatGlow = 0.0;
                float3 invVolumeSize = rcp(max((volumeHalfExtent * 2.0), 0.001.xxx));
                float3 gridScale = max(_GridDimensions.xyz - 1.0, 1.0.xxx);
                float invSurfaceBandWidth = rcp(max(_SdfRange * 0.074, 0.001));

                [loop]
                for (int stepIndex = 0; stepIndex < PDA_SONAR_MARCH_STEPS; stepIndex++)
                {
                    float3 uvw = saturate((position - volumeMin) * invVolumeSize);
                    float sdf = DecodeSdf(uvw);
                    float surfaceBand = 1.0 - saturate(abs(sdf) * invSurfaceBandWidth);
                    float3 voxelCell = abs(frac(uvw * gridScale) - 0.5);
                    float voxelWire = 1.0 - ResolveLinearRamp01(0.16, 0.34, min(voxelCell.x, min(voxelCell.y, voxelCell.z)));

                    [unroll]
                    for (int pingIndex = 0; pingIndex < 8; pingIndex++)
                    {
                        if (pingIndex >= _ThreatPingCount)
                            break;

                        float4 ping = _ThreatPings[pingIndex];
                        float pingRadius = lerp(0.06, 0.14, saturate(ping.w));
                        float3 pingDelta = position - ping.xyz;
                        float pingDistanceSq = dot(pingDelta, pingDelta);
                        float pingRadiusSq = max(pingRadius * pingRadius, 1e-6);
                        float pulse = FastTriangleSine01((_TimePhase * 7.5) + pingIndex);
                        float pingGlow = saturate(1.0 - (pingDistanceSq * rcp(pingRadiusSq)));
                        threatGlow = max(threatGlow, pingGlow * pulse * ping.w);
                    }

                    if (sdf < 0.0)
                    {
                        float3 fakeNormal = ApproximateUnitDirectionDiamond3((uvw - 0.5) * 2.0 + float3(0.0, 0.0, 0.35), float3(0.0, 0.0, 1.0));
                        float fresnel = FastPow24(1.0 - saturate(abs(dot(fakeNormal, rayDirection))));
                        float scanline = frac((position.y + 0.5) * 28.0 + (_TimePhase * 0.75));
                        float scanlineGlow = FastPow6(saturate(1.0 - abs(scanline * 2.0 - 1.0)));
                        float wireStrength = saturate((voxelWire * 0.82) + (surfaceBand * 0.35) + (fresnel * 0.25));
                        mapColor = lerp(_MapTint.rgb * 0.14, _EdgeTint.rgb, wireStrength);
                        mapColor += _MapTint.rgb * scanlineGlow * 0.18;
                        mapAlpha = saturate((_MapTint.a * 0.12) + (wireStrength * 0.62) + (surfaceBand * 0.18));
                        break;
                    }

                    position += rayDirection * marchStep;
                }

                float threatAlpha = threatGlow * _ThreatTint.a;
                float3 finalColor = mapColor + (_ThreatTint.rgb * threatGlow);
                float finalAlpha = saturate(max(mapAlpha, threatAlpha));
                clip(finalAlpha * i.color.a - max(HectonDitherCoverage(i.vertex.xy), 0.0005));
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(i.vertex.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, i.vertex.xy);
                half3 color = lerp(finalColor * i.color.rgb, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
