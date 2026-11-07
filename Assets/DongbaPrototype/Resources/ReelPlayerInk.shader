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
// Hand-cut outer edge with a narrow open notch between the legs.
#define E(ax,ay,bx,by) cutEdge(p,float2(ax,ay),float2(bx,by),d,inside);
E(.02,.98,.23,.948) E(.23,.948,.47,.80) E(.47,.80,.57,.66)
E(.57,.66,.59,.51) E(.59,.51,.68,.42) E(.68,.42,.65,.32)
E(.65,.32,.54,.32) E(.54,.32,.59,.015) E(.59,.015,.535,.015)
E(.535,.015,.465,.255) E(.465,.255,.405,.015) E(.405,.015,.345,.02)
E(.345,.02,.405,.31) E(.405,.31,.27,.33) E(.27,.33,.27,.41)
E(.27,.41,.39,.50) E(.39,.50,.37,.62) E(.37,.62,.28,.73)
E(.28,.73,.03,.87) E(.03,.87,.02,.98)
#undef E
float aa=max(fwidth(d),.0006);return smoothstep(-aa,aa,(inside*2-1)*d+.0011*sin(p.x*313+p.y*187)*sin(p.y*279-p.x*91));}
fixed4 frag(v2f i):SV_Target {
 fixed4 ink=tex2D(_MainTex,i.uv);float cut=paperMask(i.uv);float shadow=paperMask(i.uv+float2(-.015,.020))*.30;
 float paperAlpha=max(cut,ink.a);float a=lerp(ink.a,max(paperAlpha,shadow),_Blend);
 fixed3 sheet=lerp(fixed3(.34,.265,.17),tex2D(_PaperTex,i.uv*1.3).rgb*fixed3(.42,.35,.25),.40);
 sheet+=.012*sin(i.uv.x*847+i.uv.y*53)*sin(i.uv.y*671);
 fixed3 surface=lerp(sheet,ink.rgb,ink.a);surface=lerp(fixed3(.20,.14,.08),surface,saturate(paperAlpha/max(a,.001)));
 return fixed4(lerp(ink.rgb,surface,_Blend)*i.color.rgb,a*i.color.a);
}
ENDCG }
}}
