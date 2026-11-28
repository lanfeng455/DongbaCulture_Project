Shader "Dongba/MR Puzzle Spatial Ink" {
Properties { [PerRendererData] _MainTex("Ink / paper",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) _Sprite("Sprite tint",Float)=1 _StageAlpha("Transition",Range(0,1))=1 _FlashAmount("Hit",Float)=0 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off Blend One OneMinusSrcAlpha
Pass { HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;half4 _Color;float _Sprite,_StageAlpha,_FlashAmount;
CBUFFER_END
#ifdef UNITY_INSTANCING_ENABLED
UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
#define unity_SpriteColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
#endif
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
Varyings Vert(Attributes input){Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=input.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.color=input.color*_Color*lerp(half4(1,1,1,1),unity_SpriteColor,_Sprite);return o;}
half4 Frag(Varyings i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
float edge=min(46-abs(i.world.x),21-abs(i.world.z));clip(edge);
half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;c.rgb=lerp(c.rgb,1,_FlashAmount);c.a*=_StageAlpha*smoothstep(0,.85,edge);c.rgb*=c.a;return c;}
ENDHLSL }
}}
