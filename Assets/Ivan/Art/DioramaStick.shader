// Diorama stick: the 3D wooden posts every paper entity is mounted on.
// Lighter and brighter than the wall boards so the posts pop out against the
// backdrop, with fine lengthwise grain and the same soft toy-like shading.
//
// Grain is mapped by the dominant normal axis, so vertical posts and held
// tools both get streaks running along their length, in world space.
Shader "Ivan/DioramaStick"
{
    Properties
    {
        _BaseColor ("Wood Tint", Color) = (0.84, 0.70, 0.53)
        _GrainStrength ("Grain Strength", Range(0, 0.25)) = 0.06
        _ToneStrength ("Length Tone Strength", Range(0, 0.2)) = 0.05
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
                float _GrainStrength;
                float _ToneStrength;
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
                float3 an = abs(n);

                // Map to the plane facing away from the stick axis so that
                // x runs around the post and y runs along its length
                float2 uv;
                if (an.y > an.x && an.y > an.z)
                {
                    uv = input.positionWS.xz;
                }
                else if (an.x >= an.z)
                {
                    uv = input.positionWS.zy;
                }
                else
                {
                    uv = input.positionWS.xy;
                }

                // Fine streaks along the length, plus a slow tone drift so
                // long posts are not perfectly uniform
                float grain = ValueNoise(float2(uv.x * 24.0, uv.y * 2.2)) - 0.5;
                float tone = ValueNoise(uv * 0.7) - 0.5;

                half3 albedo = _BaseColor.rgb;
                albedo *= 1.0 + grain * 2.0 * _GrainStrength + tone * 2.0 * _ToneStrength;

                Light mainLight = GetMainLight(input.shadowCoord);

                // Soft wrap lighting keeps the posts toy-like and low-contrast
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
