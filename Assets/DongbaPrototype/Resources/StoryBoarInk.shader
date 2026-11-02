Shader "Dongba/StoryBoarInk" {
Properties { [PerRendererData] _MainTex ("Original glyph",2D)="white" {} _Ink ("Special boar ink",Color)=(0.46,0.20,0.105,1) }
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" } Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
sampler2D _MainTex;fixed4 _Ink;
v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
fixed4 frag(v2f i):SV_Target {fixed4 source=tex2D(_MainTex,i.uv);return fixed4(_Ink.rgb*i.color.rgb,source.a*_Ink.a*i.color.a);}
ENDCG }
}}
