// Diorama pond water: a flat translucent surface with slow watercolor ripples.
//
// Same conventions as the other Diorama materials: procedural (no textures),
// world-space XZ mapping so the pattern never stretches with the mesh, and soft
// wrap lighting to keep the toy-like look. Two additions for water:
//  - Vertex color alpha feathers the mesh rim (outermost ring is alpha 0), so the
//    pond edge always fades out even without depth support.
//  - With _SHORELINE_ON (requires the pipeline's depth texture), the color and
//    alpha shift to the shallow tint where terrain approaches the surface,
//    giving a soft waterline against the basin slopes and rocks.
Shader "Ivan/DioramaWater"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.55, 0.79, 0.83, 0.45)
        _DeepColor ("Deep Color", Color) = (0.22, 0.50, 0.66, 0.88)

        [Header(Ripples)]
        _RippleScale ("Ripple Scale", Float) = 0.9
        _RippleStrength ("Ripple Strength", Range(0, 0.5)) = 0.16
        _RippleSpeed ("Ripple Speed", Float) = 0.22

        [Header(Glints)]
        _GlintScale ("Glint Scale", Float) = 2.4
        _GlintStrength ("Glint Strength", Range(0, 1)) = 0.35

        [Header(Shoreline)]
        _ShoreFadeDepth ("Shore Fade Depth", Float) = 0.35

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
        // Seen from above only; cull both sides so winding mistakes can never blank the pond
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _SHORELINE_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "DioramaGlyphScatter.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _RippleScale;
                float _RippleStrength;
                float _RippleSpeed;
                float _GlintScale;
                float _GlintStrength;
                float _ShoreFadeDepth;
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
                // Rim alpha painted by WaterPondBuilder: 1 inside, 0 at the edge
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float4 color       : COLOR;
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
                output.color = input.color;
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
                float t = _Time.y * _RippleSpeed;

                // Two counter-drifting noise layers read as slow watercolor ripples
                float n1 = ValueNoise(uv * _RippleScale + float2(t, t * 0.6));
                float n2 = ValueNoise(uv * _RippleScale * 1.37 - float2(t * 0.8, t));
                float ripple = (n1 + n2) * 0.5;

                // Shoreline: 0 where terrain meets the surface, 1 in open water
                float shoreFactor = 1.0;
                #if defined(_SHORELINE_ON)
                    float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float surfaceEye = -TransformWorldToView(input.positionWS).z;
                    shoreFactor = saturate((sceneEye - surfaceEye) / max(_ShoreFadeDepth, 1e-4));
                #endif

                half3 albedo = lerp(_ShallowColor.rgb, _DeepColor.rgb, shoreFactor);
                albedo *= lerp(1.0 - _RippleStrength, 1.0 + _RippleStrength, ripple);

                // Sparse bright glints drifting with the same field
                float glint = ValueNoise(uv * _GlintScale + float2(-t, t * 0.5));
                glint = saturate((glint - 0.72) / 0.28);
                albedo += glint * glint * _GlintStrength;

                // Dongba water glyphs, drifting slowly; inked into the albedo so they
                // share the water's lighting and read as part of the painting
                float2 glyphUV = uv * _GlyphDensity + float2(t * 0.06, t * 0.045);
                float glyph = SampleGlyph(glyphUV, _GlyphCoverage, _GlyphScale, _GlyphSeed);
                albedo = lerp(albedo, _GlyphColor.rgb, glyph * _GlyphColor.a);

                // Soft wrap lighting, same as the grass/soil/wall family
                Light mainLight = GetMainLight(input.shadowCoord);
                float ndl = dot(n, mainLight.direction);
                float wrap = saturate(ndl * 0.65 + 0.35);

                half3 direct = mainLight.color * (wrap * mainLight.shadowAttenuation);
                half3 ambient = SampleSHVertex(n);

                half3 color = albedo * (direct + ambient);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, shoreFactor) * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
