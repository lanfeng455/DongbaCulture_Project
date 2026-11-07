Shader "Dongba/ReelPlayerInk" {
Properties { [PerRendererData] _MainTex("Original glyph",2D)="white" {} _PaperTex("Existing paper texture",2D)="white" {} _Blend("Reel visibility",Range(0,1))=0 }
SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_PaperTex;float _Blend;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
void cutEdge(float2 p,float2 a,float2 b,inout float distance,inout float inside){float2 d=b-a;float t=saturate(dot(p-a,d)/max(dot(d,d),.00001));distance=min(distance,length(p-a-t*d));if((a.y>p.y)!=(b.y>p.y)){float x=(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x;if(p.x<x)inside=1-inside;}}
float paperMask(float2 p){float d=10,inside=0;
// One continuous cut around the whole actor; spaces between strokes stay paper.
#define E(ax,ay,bx,by) cutEdge(p,float2(ax,ay),float2(bx,by),d,inside);
E(.02,.98,.25,.96) E(.25,.96,.48,.82) E(.48,.82,.57,.66)
E(.57,.66,.59,.51) E(.59,.51,.70,.43) E(.70,.43,.70,.29)
E(.70,.29,.61,.29) E(.61,.29,.65,.01) E(.65,.01,.31,.01)
E(.31,.01,.34,.29) E(.34,.29,.23,.29) E(.23,.29,.23,.44)
E(.23,.44,.39,.50) E(.39,.50,.37,.62) E(.37,.62,.28,.73)
E(.28,.73,.03,.87) E(.03,.87,.02,.98)
#undef E
float aa=max(fwidth(d),.0006);return smoothstep(-aa,aa,(inside*2-1)*d);}
fixed4 frag(v2f i):SV_Target {
 fixed4 ink=tex2D(_MainTex,i.uv);float cut=paperMask(i.uv);float shadow=paperMask(i.uv+float2(-.012,.016))*.22;
 float paperAlpha=max(cut,ink.a);float a=lerp(ink.a,max(paperAlpha,shadow),_Blend);
 fixed3 sheet=lerp(fixed3(.94,.875,.75),tex2D(_PaperTex,i.uv*1.3).rgb,.22);
 fixed3 surface=lerp(sheet,ink.rgb,ink.a);surface=lerp(fixed3(.20,.14,.08),surface,saturate(paperAlpha/max(a,.001)));
 return fixed4(lerp(ink.rgb,surface,_Blend)*i.color.rgb,a*i.color.a);
}
ENDCG }
}}
