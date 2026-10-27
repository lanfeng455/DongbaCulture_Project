Shader "Dongba/InkStroke" {
Properties { [PerRendererData] _MainTex ("Glyph", 2D)="white" {} _Radius("Ink spread in source pixels",Float)=2.2 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_TexelSize;float _Radius;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
fixed4 frag(v2f i):SV_Target {fixed4 ink=tex2D(_MainTex,i.uv);for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){fixed4 n=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*_Radius);if(n.a>ink.a)ink=n;}ink.rgb=lerp(ink.rgb,fixed3(.25,.17,.10),.28);return ink*i.color;}
ENDCG }
} }
