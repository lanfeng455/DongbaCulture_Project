using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype {
// UI decoration only: these edges and rules are not Dongba pictographs.
public static partial class DongbaUIStyle {
    public static readonly Color Ink=new Color(.19f,.13f,.085f);
    public static readonly Color Muted=new Color(.43f,.36f,.27f);
    public static readonly Color Accent=new Color(.57f,.235f,.14f);
    public static readonly Color Paper=Color.white;
    public static Sprite PaperSprite=>ManuscriptPaper;
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
    public static Button Button(Transform parent,string text,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,int number=0){
        var rect=Rect(text,parent,new Vector2(0,1),new Vector2(0,1),position,size);
        var image=rect.gameObject.AddComponent<Image>();image.sprite=BrushSprite;image.color=Color.clear;image.raycastTarget=true;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
        var rule=Stroke(rect,new Vector2(14,-size.y+2),new Vector2(size.x-28,1.6f),new Color(.35f,.25f,.15f,.26f));
        var mark=Stroke(rect,new Vector2(0,-14),new Vector2(4,22),Accent);
        var label=Label("Label",rect,text,new Vector2(number>0?56:22,-7),new Vector2(size.x-(number>0?78:44),size.y-14),24);label.alignment=TextAnchor.MiddleLeft;Serif(label);
        if(number>0){var no=Label("Row number",rect,number.ToString("00"),new Vector2(18,-11),new Vector2(34,30),15);no.color=Muted;}
        rect.gameObject.AddComponent<DongbaInkButton>().Initialize(image,label,mark,rule);
        button.onClick.AddListener(action);return button;
    }
}
}
