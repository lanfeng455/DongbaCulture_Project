// Diorama wall for the toy-theater look: thin warm wooden boards with soft,
// low-contrast shading inspired by wooden children's diorama sets.
//
// The wall is a pure backdrop: it never writes depth, so paper glyphs near it
// can lean "through" it without being clipped, while it still depth-tests
// against the opaque sticks and floor, receives tree shadows, and follows
// the directional light (day/night, weather tinting from EnvironmentLight).
//
// The board pattern is computed in world space, so wall scale never stretches
// it, and every visual aspect is a material property to keep the look tunable.
Shader "Ivan/DioramaWall"
{
    Properties
    {
        [Header(Wood Boards)]
        _BaseColor ("Board Tint", Color) = (0.71, 0.56, 0.42)
        _PlankWidth ("Board Width (m)", Float) = 0.8
        [Toggle] _HorizontalBoards ("Horizontal Boards", Float) = 0
        _SeamWidth ("Seam Width (m)", Float) = 0.014
        _SeamColor ("Seam Color", Color) = (0.45, 0.33, 0.24)
        _ToneVariation ("Board Tone Variation", Range(0, 0.3)) = 0.06
        _GrainStrength ("Grain Strength", Range(0, 0.25)) = 0.05

        [Header(Contact Shade)]
        _BottomFadeHeight ("Bottom Fade Height (m)", Float) = 0.5
        _BottomFadeStrength ("Bottom Fade Strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Backdrop rule: depth-test against the opaque sticks/floor as usual,
        // but never write depth — the transparent paper glyphs render later
        // and must never be clipped by the wall frame
        ZWrite Off
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
                half4 _SeamColor;
                float _PlankWidth;
                float _HorizontalBoards;
                float _SeamWidth;
                float _ToneVariation;
                float _GrainStrength;
                float _BottomFadeHeight;
                float _BottomFadeStrength;
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

            // Wooden board albedo, mapped in world space by dominant normal axis
            // so the pattern never stretches with wall size
            half3 WoodAlbedo(float3 positionWS, float3 normalWS)
            {
                float3 n = abs(normalWS);
                float2 uv;
                if (n.y > n.x && n.y > n.z)
                {
                    uv = positionWS.xz;
                }
                else if (n.x >= n.z)
                {
                    uv = positionWS.zy;
                }
                else
                {
                    uv = positionWS.xy;
                }

                if (_HorizontalBoards > 0.5)
                {
                    uv = uv.yx;
                }

                // Board coordinate: x across the wall face, y along each board
                float boards = uv.x / max(_PlankWidth, 0.05);
                float boardIndex = floor(boards);
                float within = frac(boards);

                // Per-board tone variation, plus a per-board grain offset so
                // streaks never line up across neighboring boards
                float boardHash = Hash21(float2(boardIndex, 17.0));
                float tone = 1.0 + (boardHash - 0.5) * 2.0 * _ToneVariation;

                // Long grain streaks: high frequency across, low frequency along
                float grain = ValueNoise(float2(uv.x * 22.0 + boardHash * 7.0, uv.y * 2.6)) - 0.5;

                // Seam: thin darker gap between boards, in meters
                float metersFromSeam = min(within, 1.0 - within) * _PlankWidth;
                float seamMask = saturate(metersFromSeam / max(_SeamWidth, 0.001));

                half3 albedo = _BaseColor.rgb * tone * (1.0 + grain * 2.0 * _GrainStrength);
                albedo = lerp(_SeamColor.rgb, albedo, seamMask);

                // Gentle contact shade where the wall meets the floor
                float fade = saturate(positionWS.y / max(_BottomFadeHeight, 0.01));
                albedo *= lerp(1.0 - _BottomFadeStrength, 1.0, fade);

                return albedo;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 n = normalize(input.normalWS);
                half3 albedo = WoodAlbedo(input.positionWS, n);

                Light mainLight = GetMainLight(input.shadowCoord);

                // Soft wrap lighting keeps the flat boards toy-like and
                // low-contrast instead of hard CGI plastic
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
