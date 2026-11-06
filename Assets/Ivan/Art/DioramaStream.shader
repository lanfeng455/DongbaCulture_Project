// Diorama stream: a scrolling watercolor ribbon for water running from the spring
// hole down into the pond. Pairs with the StreamRibbon component, which builds a
// bendable triangle-strip mesh through control points and feeds the shader:
//   UV.x  = 0..1 across the ribbon (edge feather)
//   UV.y  = arc length along the path in meters (flow scroll; density never stretches)
//   UV2.x = 0..1 normalized position along the whole path (end-foam bands)
// Flip the sign of _FlowSpeed to reverse the flow direction.
//
// Unlike the pond shader there is deliberately no depth-based shoreline here:
// the ribbon lies directly on the terrain, where a depth fade would make it
// fully transparent. Edge softness comes from the across-ribbon feather on UV.x.
Shader "Ivan/DioramaStream"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("Water Color", Color) = (0.50, 0.76, 0.82, 0.75)
        _FoamColor ("Foam Color", Color) = (0.93, 0.97, 0.98, 0.90)

        [Header(Flow)]
        _FlowSpeed ("Flow Speed", Float) = 1.4
        _FlowScale ("Flow Pattern Scale", Float) = 2.5
        _StreakScale ("Streak Scale (across)", Float) = 3.0

        [Header(Foam)]
        _FoamThreshold ("Foam Threshold", Range(0.5, 0.95)) = 0.72
        _FoamEnd ("End Foam Band", Range(0, 0.4)) = 0.18

        [Header(Edges)]
        _EdgeFeather ("Edge Feather", Range(0.01, 0.5)) = 0.18

        [Header(Flow Reveal)]
        // Per-renderer; driven by StreamRibbon at runtime via MaterialPropertyBlock
        _FlowFill ("Flow Fill", Float) = 1.0
        _FlowBand ("Leading Band", Range(0.02, 0.25)) = 0.08

        [Header(Dongba Glyphs)]
        [NoScaleOffset] _GlyphTex ("Glyph Texture", 2D) = "white" {}
        _GlyphColor ("Glyph Color", Color) = (0.13, 0.35, 0.50, 0.45)
        _GlyphDensity ("Glyph Density", Float) = 1.2
        _GlyphCoverage ("Glyph Coverage", Range(0, 1)) = 0.45
        _GlyphScale ("Glyph Scale", Range(0.2, 0.9)) = 0.55
        _GlyphSeed ("Glyph Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Viewed from above at varying tilts; render both sides so orientation
        // mistakes can never blank the ribbon
        Cull Off

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
            #include "DioramaGlyphScatter.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _FlowScale;
                float _StreakScale;
                float _FoamThreshold;
                float _FoamEnd;
                float _EdgeFeather;
                float _FlowFill;
                float _FlowBand;
                half4 _GlyphColor;
                float _GlyphDensity;
                float _GlyphCoverage;
                float _GlyphScale;
                float _GlyphSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float2 uv2         : TEXCOORD4;
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
                output.uv = input.uv;
                output.uv2 = input.uv2;
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

                // u across the ribbon (0..1), vFrac along the whole path (0..1)
                float u = input.uv.x;
                float vFrac = input.uv2.x;

                // Flow coordinate: arc length in meters, written by StreamRibbon,
                // so pattern density never depends on transform scale
                float v = input.uv.y * _FlowScale - _Time.y * _FlowSpeed;

                // Streaks stretched along the flow read as running water
                float n1 = ValueNoise(float2(u * _StreakScale, v * 0.45));
                float n2 = ValueNoise(float2(u * _StreakScale * 1.7 + 5.2, v * 0.7));
                float streak = smoothstep(_FoamThreshold, 1.0, max(n1, n2 * 0.9));

                // Foam gathering where the ribbon starts (spring hole) and lands (pond)
                float endBand = smoothstep(_FoamEnd, 0.0, vFrac)
                    + smoothstep(1.0 - _FoamEnd, 1.0, vFrac);
                float endFoam = endBand * smoothstep(0.3, 0.7,
                    ValueNoise(float2(u * 6.0, v * 1.5)) + endBand * 0.35);

                float foam = saturate(streak + endFoam);

                half3 albedo = lerp(_BaseColor.rgb, _FoamColor.rgb, foam);
                albedo *= 0.92 + 0.16 * n1;

                // Dongba water glyphs riding the flow: the scatter grid is built in
                // streak space (v includes the time scroll), so characters travel
                // downstream at exactly the water's speed
                float2 glyphUV = float2(u * 2.0, v / max(_FlowScale, 1e-3)) * _GlyphDensity;
                float glyph = SampleGlyph(glyphUV, _GlyphCoverage, _GlyphScale, _GlyphSeed);
                albedo = lerp(albedo, _GlyphColor.rgb, glyph * _GlyphColor.a);

                // Reveal mask: source (vFrac near 0) becomes visible first, then
                // progressively fills toward the pond end (vFrac near 1). A foam-
                // bright leading edge rides exactly at the fill front so arriving
                // water reads as a wave rather than a hard clipping plane.
                float flowFill = saturate(_FlowFill);
                float flowBand = _FlowBand;
                float forward = (_FlowSpeed >= 0.0) ? 1.0 : -1.0;
                float revealCoord = (forward > 0.0) ? vFrac : 1.0 - vFrac;

                // revealCoord <= flowFill → already filled (visible)
                // revealCoord >  flowFill → not yet filled (transparent)
                // soft band centered on flowFill for the water head
                float reveal = 1.0 - smoothstep(flowFill, flowFill + flowBand, revealCoord);
                float leadingFoam = smoothstep(flowFill - flowBand, flowFill, revealCoord)
                    * (1.0 - smoothstep(flowFill, flowFill + flowBand, revealCoord));
                half3 headColor = lerp(_BaseColor.rgb, _FoamColor.rgb,
                    saturate(0.55 + leadingFoam * 0.45));
                albedo = lerp(albedo, headColor, leadingFoam * 0.6);

                // Soft wrap lighting, same family feel as grass/soil/water
                float3 n = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                float wrap = saturate(dot(n, mainLight.direction) * 0.65 + 0.35);
                half3 direct = mainLight.color * (wrap * mainLight.shadowAttenuation);
                half3 ambient = SampleSHVertex(n);
                half3 color = albedo * (direct + ambient);

                float edge = smoothstep(0.0, _EdgeFeather, u)
                    * smoothstep(0.0, _EdgeFeather, 1.0 - u);
                half alpha = lerp(_BaseColor.a, _FoamColor.a, foam) * edge * reveal;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
