Shader "Dongba/RootGrowthInk" {
Properties { [PerRendererData] _MainTex("Glyph",2D)="white" {} _Growth("Root to crown",Range(0,1))=0 _Green("Living ink",Color)=(.30,.40,.13,1) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float _Growth; fixed4 _Green;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
fixed4 frag(v2f i):SV_Target {
 fixed4 ink=tex2D(_MainTex,i.uv);
 float edge=lerp(-.035,1.035,_Growth);
 float cover=1-smoothstep(edge-.035,edge+.035,i.uv.y);
 // Only recolor existing ink; alpha, strokes and proportions are unchanged.
 fixed3 living=_Green.rgb*(.85+.35*max(ink.r,max(ink.g,ink.b)));
 ink.rgb=lerp(ink.rgb,living,cover);return ink*i.color;
}
ENDCG }
} }
