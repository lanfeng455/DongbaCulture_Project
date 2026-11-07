Shader "Dongba/ReelPlayerInk" {
Properties { [PerRendererData] _MainTex("Original glyph",2D)="white" {} _Blend("Reel visibility",Range(0,1))=0 }
SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_TexelSize;float _Blend;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
fixed4 frag(v2f i):SV_Target {fixed4 ink=tex2D(_MainTex,i.uv);float edge=0;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)edge=max(edge,tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*3).a);float a=max(ink.a,edge*_Blend*.90);fixed3 c=lerp(ink.rgb,fixed3(.31,.075,.035),_Blend);c=lerp(fixed3(.96,.87,.67),c,ink.a/max(a,.001));return fixed4(c*i.color.rgb,a*i.color.a);}
ENDCG }
}}
