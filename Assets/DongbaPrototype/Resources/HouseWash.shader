Shader "Dongba/HouseWash" {
Properties { [PerRendererData] _MainTex("Original glyph",2D)="white" {} _WallTint("Wall wash",Color)=(0.64,0.56,0.41,0.40) _RoofTint("Roof wash",Color)=(0.52,0.40,0.24,0.46) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"} Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};sampler2D _MainTex;fixed4 _WallTint,_RoofTint;
v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
float edge(float2 a,float2 b,float2 p){float2 d=b-a;return (d.x*(p.y-a.y)-d.y*(p.x-a.x))/length(d);}
float tri(float2 a,float2 b,float2 c,float2 p){return smoothstep(-.5,1.2,min(edge(a,b,p),min(edge(b,c,p),edge(c,a,p))));}
fixed4 frag(v2f i):SV_Target{float2 p=float2(i.uv.x*292,(1-i.uv.y)*202);fixed4 ink=tex2D(_MainTex,i.uv);float roof=tri(float2(60,87),float2(151,32),float2(226,87),p);float wall=smoothstep(-.5,1.2,min(min(p.x-61,224-p.x),min(p.y-88,183-p.y)));wall=saturate(wall);float grain=.88+.075*sin(p.x*.53+p.y*.23)+.045*sin(p.y*1.17-p.x*.31);fixed4 fill=roof>wall?_RoofTint:_WallTint;float fa=saturate(max(roof,wall)*fill.a*grain);float alpha=ink.a+fa*(1-ink.a);float3 rgb=(ink.rgb*ink.a+fill.rgb*fa*(1-ink.a))/max(alpha,.001);return fixed4(rgb*i.color.rgb,alpha*i.color.a);}
ENDCG}
}}
