Shader "Dongba/RootGrowthInk" {
Properties { [PerRendererData] _MainTex("Glyph",2D)="white" {} _Growth("Root to crown",Range(0,1))=0 _Green("Living ink",Color)=(.30,.40,.13,1) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
// 材质参数放进 UnityPerMaterial：让这个 shader 兼容 SRP Batcher。
// 不写 CBUFFER 时 URP 会走非批处理器的旧路径，多个“同材质 + 同网格”的 SpriteRenderer
// 会被并到一批、整批只取一张 _MainTex，六层树冠笔触就都画成同一根。
CBUFFER_START(UnityPerMaterial)
float _Growth; fixed4 _Green;
CBUFFER_END
sampler2D _MainTex;
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
