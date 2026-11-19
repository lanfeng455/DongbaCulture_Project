using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype {
// UI decoration only: these edges and rules are not Dongba pictographs.
public static class DongbaUIStyle {
    public static readonly Color Ink=new Color(.22f,.16f,.11f);
    public static readonly Color Muted=new Color(.43f,.36f,.27f);
    public static readonly Color Accent=new Color(.53f,.25f,.17f);
    public static readonly Color Paper=new Color(.965f,.935f,.865f,.98f);
    static Sprite paper;
    public static Sprite PaperSprite {get {
        if(paper!=null)return paper;
        const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.name="Reusable paper UI";
        var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){
            float edge=Mathf.Min(Mathf.Min(x,n-1-x),Mathf.Min(y,n-1-y));
            float alpha=Mathf.SmoothStep(0,1,(edge+.25f*Mathf.Sin(y*1.7f)+.2f*Mathf.Sin(x*2.3f))/3);
            float fiber=1-.015f*(.5f+.5f*Mathf.Sin(x*8.1f+y*13.9f));pixels[y*n+x]=new Color(fiber,fiber,fiber,alpha);
        }
        texture.SetPixels(pixels);texture.Apply();
        paper=Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));return paper;
    }}
    public static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size){
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    public static Image Plate(RectTransform rect,Color color,bool hit=false){var image=rect.gameObject.AddComponent<Image>();image.sprite=PaperSprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=hit;return image;}
    public static Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize=23){
        var rect=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),position,size);var label=rect.gameObject.AddComponent<Text>();
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.color=Ink;label.text=value;
        label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
        DongbaLocalizedLabel.Bind(label);return label;
    }
    public static Button Button(Transform parent,string text,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action){
        var rect=Rect(text,parent,new Vector2(0,1),new Vector2(0,1),position,size);
        var image=Plate(rect,Color.white,true);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=new Color(.88f,.83f,.72f,.48f);colors.highlightedColor=new Color(.83f,.75f,.59f,1);
        colors.selectedColor=new Color(.83f,.75f,.59f,1);colors.pressedColor=new Color(.68f,.57f,.42f,1);colors.disabledColor=new Color(.88f,.86f,.80f,.45f);colors.fadeDuration=.1f;button.colors=colors;
        var label=Label("Label",rect,text,new Vector2(18,-8),size-new Vector2(36,16));label.alignment=TextAnchor.MiddleLeft;
        button.onClick.AddListener(action);return button;
    }
}
}
