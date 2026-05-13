Shader "Hecton/Prologue/Orbital Planet Relativity Fake"
{
    Properties
    {
        _GroundColor ("Ground Color", Color) = (0.10, 0.18, 0.20, 1.0)
        _OceanColor ("Ocean Color", Color) = (0.01, 0.08, 0.12, 1.0)
        [HDR] _AtmosphereColor ("Atmosphere Color", Color) = (0.22, 0.70, 0.88, 1.0)
        _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.42
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OrbitalPlanetFake"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GroundColor;
                half4 _OceanColor;
                half4 _AtmosphereColor;
                half _CloudStrength;
                half _RimPower;
            CBUFFER_END

            float _H8OrbitalPlanetDistanceMeters;
            float _H8OrbitalFakeRadiusMeters;
            float _H8OrbitalUniverseSpeed;
            float _H8OrbitalMathLod;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float lod = _H8OrbitalMathLod;
                float fakeRadius = max(_H8OrbitalFakeRadiusMeters, 5000.0);
                float distanceFade = saturate(1.0 - _H8OrbitalPlanetDistanceMeters * 0.00008333333);
                float cheapBulge = fakeRadius * 0.00002 * distanceFade;
                float logBulge = log2(1.0 + fakeRadius * 0.0001) * 32.0 * distanceFade;
                logBulge = lod < 0.5 ? cheapBulge : logBulge;
                float3 positionOS = input.positionOS.xyz + input.normalOS * logBulge;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = input.normalWS;
                half3 viewDirWS = input.viewDirWS;
                half rimBase = saturate(1.0h - dot(normalWS, viewDirWS));

                if (_H8OrbitalMathLod < 0.5)
                {
                    half3 lowColor = lerp(_OceanColor.rgb, _AtmosphereColor.rgb, rimBase * 0.45h);
                    return half4(lowColor, 1.0h);
                }

                half rim = pow(rimBase, _RimPower);
                half bands = saturate(sin((input.positionWS.x + input.positionWS.z) * 0.0017h) * 0.5h + 0.5h);
                half continent = saturate(sin(input.positionWS.x * 0.0007h + input.positionWS.y * 0.0011h) * 0.5h + 0.5h);
                half clouds = smoothstep(0.48h, 0.72h, bands) * _CloudStrength;
                half3 baseColor = lerp(_OceanColor.rgb, _GroundColor.rgb, continent);
                baseColor = lerp(baseColor, half3(0.92h, 0.95h, 0.93h), clouds);
                half overkill = _H8OrbitalMathLod > 2.5 ? 1.35h : 1.0h;
                baseColor += _AtmosphereColor.rgb * rim * (1.0h + saturate(_H8OrbitalUniverseSpeed * 0.00025h) * overkill);
                return half4(baseColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
