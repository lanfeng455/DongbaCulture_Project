Shader "Dongba/ReelPlayerInk" {
Properties { [PerRendererData] _MainTex("Original glyph",2D)="white" {} _PaperTex("Existing paper texture",2D)="white" {} _Blend("Reel visibility",Range(0,1))=0 _PaperDecline("Source scene paper state",Range(0,1))=0 }
SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_PaperTex;float _Blend,_PaperDecline;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
void cutEdge(float2 p,float2 a,float2 b,inout float distance,inout float inside){float2 d=b-a;float t=saturate(dot(p-a,d)/max(dot(d,d),.00001));distance=min(distance,length(p-a-t*d));if((a.y>p.y)!=(b.y>p.y)){float x=(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x;if(p.x<x)inside=1-inside;}}
float paperMask(float2 p){float d=10,inside=0;
// Hand-cut outer edge with a narrow open notch between the legs.
#define E(ax,ay,bx,by) cutEdge(p,float2(ax,ay),float2(bx,by),d,inside);
E(0.045,0.925,0.140,0.925)
E(0.140,0.925,0.250,0.880)
E(0.250,0.880,0.340,0.840)
E(0.340,0.840,0.430,0.770)
E(0.430,0.770,0.480,0.700)
E(0.480,0.700,0.490,0.640)
E(0.490,0.640,0.550,0.610)
E(0.550,0.610,0.600,0.550)
E(0.600,0.550,0.600,0.490)
E(0.600,0.490,0.550,0.455)
E(0.550,0.455,0.650,0.415)
E(0.650,0.415,0.660,0.360)
E(0.660,0.360,0.550,0.335)
E(0.550,0.335,0.625,0.010)
E(0.625,0.010,0.550,0.010)
E(0.550,0.010,0.485,0.240)
E(0.485,0.240,0.435,0.015)
E(0.435,0.015,0.370,0.015)
E(0.370,0.015,0.420,0.330)
E(0.420,0.330,0.270,0.355)
E(0.270,0.355,0.270,0.405)
E(0.270,0.405,0.390,0.470)
E(0.390,0.470,0.370,0.530)
E(0.370,0.530,0.400,0.600)
E(0.400,0.600,0.420,0.630)
E(0.420,0.630,0.400,0.680)
E(0.400,0.680,0.310,0.730)
E(0.310,0.730,0.220,0.780)
E(0.220,0.780,0.120,0.840)
E(0.120,0.840,0.045,0.880)
E(0.045,0.880,0.045,0.925)
#undef E
float aa=max(fwidth(d),.0006);return smoothstep(-aa,aa,(inside*2-1)*d+.0011*sin(p.x*313+p.y*187)*sin(p.y*279-p.x*91));}
fixed4 frag(v2f i):SV_Target {
 fixed4 ink=tex2D(_MainTex,i.uv);float cut=paperMask(i.uv);float shadow=paperMask(i.uv+float2(-.015,.020))*.30;
 float paperAlpha=max(cut,ink.a);float a=lerp(ink.a,max(paperAlpha,shadow),_Blend);
 // Sample exactly the original forest paper, including its ecological paper tint.
 fixed3 sheet=saturate(tex2D(_PaperTex,i.uv*1.3).rgb+_PaperDecline*(fixed3(.865,.867,.843)-fixed3(.945,.904,.811)));
 fixed3 surface=lerp(sheet,ink.rgb,ink.a);surface=lerp(fixed3(.20,.14,.08),surface,saturate(paperAlpha/max(a,.001)));
 return fixed4(lerp(ink.rgb,surface,_Blend)*i.color.rgb,a*i.color.a);
}
ENDCG }
}}
