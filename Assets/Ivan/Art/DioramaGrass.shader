// Diorama grass floor: a soft watercolor lawn whose season color is driven
// globally by EnvironmentManager through _SeasonBlend (0=Spring .. 3=Winter),
// so the whole lawn repaints together with the tree stickers on season change.
//
// All four season colors live on the material, the pattern is mapped in world
// space (XZ) so the lawn size never stretches it, and the mottling stays
// brightness-based so it reads correctly for every season, including snow.
Shader "Ivan/DioramaGrass"
{
    Properties
    {
        [Header(Season Colors)]
        _SpringColor ("Spring", Color) = (0.55, 0.72, 0.38)
        _SummerColor ("Summer", Color) = (0.34, 0.58, 0.27)
        _AutumnColor ("Autumn", Color) = (0.68, 0.52, 0.28)
        _WinterColor ("Winter (snow)", Color) = (0.80, 0.84, 0.88)

        [Header(Mottling)]
        _PatchScale ("Patch Scale", Float) = 0.18
        _PatchStrength ("Patch Strength", Range(0, 0.5)) = 0.16
        _BladeScale ("Blade Detail Scale", Float) = 2.7
        _BladeStrength ("Blade Detail Strength", Range(0, 0.2)) = 0.06
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
                half4 _SpringColor;
                half4 _SummerColor;
                half4 _AutumnColor;
                half4 _WinterColor;
                float _PatchScale;
                float _PatchStrength;
                float _BladeScale;
                float _BladeStrength;
            CBUFFER_END

            // Set globally by EnvironmentManager: 0=Spring, 1=Summer,
            // 2=Autumn, 3=Winter, fractional values blend between seasons
            float _SeasonBlend;

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

            // Branchless season chain: 0-1 spring->summer, 1-2 summer->autumn,
            // 2-3 autumn->winter; integer values land exactly on a season
            half3 SeasonBase(float blend)
            {
                float s = clamp(blend, 0.0, 3.0);
                half3 c = lerp(_SpringColor.rgb, _SummerColor.rgb, saturate(s));
                c = lerp(c, _AutumnColor.rgb, saturate(s - 1.0));
                c = lerp(c, _WinterColor.rgb, saturate(s - 2.0));
                return c;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 n = normalize(input.normalWS);
                float2 uv = input.positionWS.xz;

                // Large soft watercolor washes + fine blade-level detail,
                // brightness only so every season (including snow) stays coherent
                float patch = ValueNoise(uv * _PatchScale);
                float blades = ValueNoise(uv * _BladeScale);

                half3 albedo = SeasonBase(_SeasonBlend);
                albedo *= lerp(1.0 - _PatchStrength, 1.0, patch);
                albedo *= 1.0 + (blades - 0.5) * 2.0 * _BladeStrength;

                Light mainLight = GetMainLight(input.shadowCoord);

                // Soft wrap lighting keeps the lawn toy-like and low-contrast
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
