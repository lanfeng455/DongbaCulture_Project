// Diorama soil floor: warm speckled earth with a few tiny pebbles scattered
// through hashed cells, matching the grass/wall shading model (soft wrap
// lighting, receives tree shadows, world-space pattern that never stretches).
Shader "Ivan/DioramaSoil"
{
    Properties
    {
        [Header(Earth)]
        _BaseColor ("Earth Tint", Color) = (0.58, 0.44, 0.32)
        _MottleColor ("Mottle Color", Color) = (0.42, 0.31, 0.22)
        _PatchScale ("Patch Scale", Float) = 0.35
        _PatchStrength ("Patch Strength", Range(0, 1)) = 0.5

        [Header(Pebbles)]
        _PebbleColor ("Pebble Color", Color) = (0.78, 0.72, 0.66)
        _PebbleScale ("Pebble Scale", Float) = 3.0
        _PebbleAmount ("Pebble Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MottleColor;
                float _PatchScale;
                float _PatchStrength;
                half4 _PebbleColor;
                float _PebbleScale;
                float _PebbleAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);

                output.positionWS = pos.positionWS;
                output.positionCS = pos.positionCS;
                output.normalWS = nrm.normalWS;
                output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 n = normalize(input.normalWS);
                float2 uv = input.positionWS.xz;

                // Soft earth patches
                float patch = ValueNoise(uv * _PatchScale);
                half3 albedo = lerp(_MottleColor.rgb, _BaseColor.rgb, patch * _PatchStrength + (1.0 - _PatchStrength));

                // Tiny pebbles: sparse hashed cells brighten into stones with a
                // soft round falloff inside each cell
                float2 cell = uv * _PebbleScale;
                float2 cellId = floor(cell);
                float2 cellUv = frac(cell) - 0.5;
                float hash = Hash21(cellId);
                float radius = 0.12 + hash * 0.10;
                float pebble = smoothstep(radius, radius * 0.5, length(cellUv));
                float sparse = step(1.0 - _PebbleAmount * 0.04, hash);
                albedo = lerp(albedo, _PebbleColor.rgb, pebble * sparse);

                Light mainLight = GetMainLight(input.shadowCoord);

                // Soft wrap lighting keeps the soil toy-like and low-contrast
                float ndl = dot(n, mainLight.direction);
                float wrap = saturate(ndl * 0.65 + 0.35);

                half3 direct = mainLight.color * (wrap * mainLight.shadowAttenuation);
                half3 ambient = SampleSHVertex(n);

                half3 color = albedo * (direct + ambient);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Shadow pancaking, the same near-plane clamp URP's own casters use
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
