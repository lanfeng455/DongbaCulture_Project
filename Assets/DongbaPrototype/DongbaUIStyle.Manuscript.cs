using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {
public static partial class DongbaUIStyle {
    static Sprite manuscript,brush;static Font heading;
    static readonly Dictionary<string,Sprite> glyphs=new Dictionary<string,Sprite>();
    public static Font HeadingFont {get {if(heading==null)heading=DongbaLanguage.ChineseUIFont;return heading;}}
    public static Sprite ManuscriptPaper {get {
        if(manuscript!=null)return manuscript;
        const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.name="Cut manuscript paper / UI only";var p=new Color[n*n];var rng=new System.Random(917);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){
            float left=3+1.4f*Mathf.Sin(y*.069f)+.55f*Mathf.Sin(y*.61f),right=4+1.1f*Mathf.Sin(y*.055f+2);
            float bottom=3+1.2f*Mathf.Sin(x*.085f+1),top=3+1.4f*Mathf.Sin(x*.053f);
            float edge=Mathf.Min(Mathf.Min(x-left,n-1-x-right),Mathf.Min(y-bottom,n-1-y-top));
            if(x<16&&y<22)edge=Mathf.Min(edge,x+y-16);if(x>n-21&&y>n-28)edge=Mathf.Min(edge,2*n-x-y-19);
            float grain=((float)rng.NextDouble()-.5f)*.022f;if(rng.NextDouble()<.018)grain-=.06f;
            float cloud=(Mathf.PerlinNoise(x*.025f,y*.025f)-.5f)*.025f;
            p[y*n+x]=new Color(.958f+grain+cloud,.919f+grain+cloud,.820f+grain+cloud,Mathf.Clamp01(edge+.45f));
        }
        t.SetPixels(p);t.Apply(false,true);t.wrapMode=TextureWrapMode.Clamp;
        manuscript=Sprite.Create(t,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(25,25,25,25));return manuscript;
    }}
    public static Sprite BrushSprite {get {
        if(brush!=null)return brush;
        const int w=384,h=32;var t=new Texture2D(w,h,TextureFormat.RGBA32,false);var p=new Color[w*h];var rng=new System.Random(121);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float u=x/(float)(w-1),edge=13*(.78f+.12f*Mathf.Sin(u*11)+.05f*Mathf.Sin(u*39));
            float d=edge-Mathf.Abs(y-15.5f+Mathf.Sin(u*14)*1.2f);float alpha=Mathf.Clamp01(d)*Mathf.Clamp01(x/8f)*Mathf.Clamp01((w-1-x)/15f);
            alpha*=rng.NextDouble()<.055?.35f:1;p[y*w+x]=new Color(1,1,1,alpha);
        }
        t.SetPixels(p);t.Apply(false,true);brush=Sprite.Create(t,new Rect(0,0,w,h),Vector2.one*.5f,100);return brush;
    }}
    public static Sprite Glyph(string name){
        if(glyphs.TryGetValue(name,out var sprite))return sprite;
        var t=Resources.Load<Texture2D>("ApprovedArt/"+name);if(t==null)return null;
        sprite=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f,100);glyphs[name]=sprite;return sprite;
    }
    // The same two leaves, spine and edge shadow serve desktop and spatial menus.
    public static RectTransform OpenManuscript(Transform parent,string name,Vector2 anchor,Vector2 position,out RectTransform left,out RectTransform right){
        var panel=Rect(name,parent,anchor,Vector2.one*.5f,position,new Vector2(970,660));
        var under=Rect("Underlying leaf",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(6,-7),new Vector2(962,650));var underside=Plate(under,new Color(.84f,.80f,.69f));EdgeShadow(underside,.25f);
        left=Rect("Story leaf",panel,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(348,652));Plate(left,new Color(.94f,.91f,.83f));
        right=Rect("Menu leaf",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(340,0),new Vector2(630,656));Plate(right,Color.white,true);
        Stroke(left,new Vector2(329,-25),new Vector2(2,598),new Color(.27f,.18f,.10f,.2f));
        return panel;
    }
    public static void EdgeShadow(Image im,float strength=.16f){var s=im.gameObject.AddComponent<Shadow>();s.effectColor=new Color(.16f,.1f,.05f,strength);s.effectDistance=new Vector2(3,-4);s.useGraphicAlpha=true;}
    public static Image Stroke(Transform parent,Vector2 position,Vector2 size,Color color){var r=Rect("Ink rule / interface",parent,new Vector2(0,1),new Vector2(0,1),position,size);var im=r.gameObject.AddComponent<Image>();im.sprite=BrushSprite;im.color=color;im.raycastTarget=false;return im;}
    public static Image Picture(Transform parent,string name,Sprite sprite,Vector2 position,Vector2 size){var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),position,size);var im=r.gameObject.AddComponent<Image>();im.sprite=sprite;im.preserveAspect=true;im.raycastTarget=false;return im;}
    public static void Serif(Text label){DongbaLocalizedLabel.SetSourceFont(label,HeadingFont);label.fontStyle=FontStyle.Normal;}
}
}
