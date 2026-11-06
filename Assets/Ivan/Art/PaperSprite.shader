// Paper sprite for the diorama entities: renders exactly like Sprites/Default
// (unlit, tinted, transparent) but adds a ShadowCaster pass so the 2D glyphs
// cast real directional-light shadows alongside the 3D sticks and walls.
// Alpha clipping in the shadow pass keeps the shadow shaped like the glyph
// silhouette, not like the sprite quad.
Shader "Ivan/PaperSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Shadow Alpha Cutoff", Range(0.01, 1)) = 0.5
        _FlashAmount ("Hit Flash", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "SpriteUnlit"
            // No LightMode tag: URP draws this pass as SRPDefaultUnlit,
            // the same way it draws Sprites/Default.
            //
            // 深度拉平（见 Vertex Shader）：SpriteRenderer 始终面朝相机（FaceCamera），
            // 四边形四个顶点深度各异会导致与 3D 物体交错。把所有顶点深度统一到
            // transform 原点（pivot）的深度后，sprite 在深度缓冲里就是一个平面，
            // 架子台面这种薄物体不会挡住它，而真厚墙（pivot 深度在墙后面）仍能正确遮挡。

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Cutoff;
                float _FlashAmount;
            CBUFFER_END

            #ifdef UNITY_INSTANCING_ENABLED
                // Unity 6 passes SpriteRenderer.color to shaders as unity_SpriteColor:
                // a per-instance array on instanced sprite draws (see URP Core2D.hlsl)
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define unity_SpriteColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv        : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // 把所有顶点的深度"拉平"到 transform 原点（pivot）的深度。
                // FaceCamera 后 sprite 四边形斜着朝向相机，顶点深度各异会与 3D 物体交错。
                // 拉平后整个 sprite 在深度缓冲里是一个正对相机的平面，避免局部被挡。
                // 如果需要让某个 sprite 额外前推（比如道具），在 C# 里改 position 即可。
                float4 pivotCS = TransformObjectToHClip(float3(0, 0, 0));
                output.positionCS.z = (pivotCS.z / pivotCS.w) * output.positionCS.w;

                output.uv = input.uv;

                // Unity 6 sprite tint path, matching URP's Sprite-Unlit-Default:
                // unity_SpriteColor is a global for non-instanced draws and a
                // per-instance array for instanced ones (vertex color stays white)
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // Hit flash: push the glyph toward white BEFORE premultiplying, so the paper
                // silhouette (alpha) and its soft edges survive and only the colour reads white
                sprite.rgb = lerp(sprite.rgb, half3(1, 1, 1), _FlashAmount);

                // Premultiplied output for Blend One OneMinusSrcAlpha, matching Sprites/Default
                sprite.rgb *= sprite.a;
                return sprite;
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Layout must stay identical to the sprite pass: SRP Batcher and
            // material-instance property updates both key off UnityPerMaterial
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Cutoff;
                float _FlashAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   tintAlpha  : TEXCOORD1;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv;
                output.tintAlpha = input.color.a * _Color.a;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Shadow pancaking, the same near-plane clamp URP's own casters use:
                // keeps sprites sitting between the light and the near plane engaged
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                // Clip to the glyph silhouette so the shadow is paper-shaped, not a quad
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.tintAlpha;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
