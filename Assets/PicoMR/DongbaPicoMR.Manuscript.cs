using DongbaPrototype;
using UnityEngine;
using UnityEngine.UI;

namespace DongbaXR {
public sealed partial class DongbaPicoMR {
    Text entryHeading,entryCopy,entryHint,entryStoryText,entrySandboxText,entryCaption;
    Image entryStoryPaper,entrySandboxPaper,entryFather;
    Font entryReadingFont;
    Material entryInk;

    Text EntryText(Transform parent,string name,string text,Vector2 pos,Vector2 size,int fontSize,bool heading=false){
        var label=DongbaUIStyle.Label(name,parent,text,pos,size,fontSize);
        DongbaLocalizedLabel.SetReadingFont(label,heading?DongbaUIStyle.HeadingFont:entryReadingFont);
        label.fontStyle=FontStyle.Normal;label.resizeTextForBestFit=false;
        label.lineSpacing=1.16f;label.verticalOverflow=VerticalWrapMode.Truncate;
        return label;
    }
    void BuildPlacementManuscript(){
        entryReadingFont=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
        entryInk=new Material(Shader.Find("Dongba/Headset Paper UI"));
        var root=new GameObject("PICO placement and mode",typeof(RectTransform),typeof(Canvas));root.layer=ScanLayer;root.transform.SetParent(origin.transform,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=xrCamera;
        canvas.sortingOrder=30000;var scaler=root.AddComponent<CanvasScaler>();scaler.dynamicPixelsPerUnit=4;
        scanReadingFrame=(RectTransform)root.transform;scanReadingFrame.sizeDelta=new Vector2(970,660);scanReadingFrame.localScale=Vector3.one*(1.05f/970);
        DongbaUIStyle.OpenManuscript(scanReadingFrame,"Entry manuscript",Vector2.one*.5f,Vector2.zero,out var left,out var right);
        EntryText(left,"Project name","DONGBA",new Vector2(32,-55),new Vector2(290,82),48,true);
        EntryText(left,"Subtitle","THE LIVING\nSCRIPT",new Vector2(34,-143),new Vector2(280,86),27);
        DongbaUIStyle.Picture(left,"Gaolequ / actual game glyph",DongbaUIStyle.Glyph("player_glyph"),new Vector2(52,-267),new Vector2(168,218));
        entryFather=DongbaUIStyle.Picture(left,"Father / actual game glyph",DongbaUIStyle.Glyph("father_glyph"),new Vector2(220,-345),new Vector2(72,132));
        DongbaUIStyle.Stroke(left,new Vector2(40,-510),new Vector2(264,2),new Color(.3f,.2f,.1f,.35f));
        entryCaption=EntryText(left,"Mode caption","",new Vector2(34,-542),new Vector2(280,90),26,true);entryCaption.alignment=TextAnchor.MiddleCenter;
        var caption=EntryText(right,"Caption","THE LIVING SCRIPT",new Vector2(36,-26),new Vector2(552,40),24);caption.color=DongbaUIStyle.Muted;
        entryHeading=EntryText(right,"Choose mode","",new Vector2(34,-70),new Vector2(560,76),38,true);
        DongbaUIStyle.Stroke(right,new Vector2(36,-148),new Vector2(550,2),new Color(.3f,.2f,.1f,.45f));
        var story=DongbaUIStyle.Rect("Story title leaf",right,new Vector2(0,1),new Vector2(0,1),new Vector2(36,-173),new Vector2(548,94));entryStoryPaper=DongbaUIStyle.Plate(story,Color.white);DongbaUIStyle.EdgeShadow(entryStoryPaper,.1f);
        var sandbox=DongbaUIStyle.Rect("Sandbox title leaf",right,new Vector2(0,1),new Vector2(0,1),new Vector2(48,-283),new Vector2(536,94));entrySandboxPaper=DongbaUIStyle.Plate(sandbox,Color.white);DongbaUIStyle.EdgeShadow(entrySandboxPaper,.1f);
        DongbaUIStyle.Stroke(story,new Vector2(16,-25),new Vector2(4,44),DongbaUIStyle.Accent);DongbaUIStyle.Stroke(sandbox,new Vector2(16,-25),new Vector2(4,44),DongbaUIStyle.Accent);
        entryStoryText=EntryText(story,"Story mode","",new Vector2(42,-19),new Vector2(478,62),32,true);
        entrySandboxText=EntryText(sandbox,"Sandbox mode","",new Vector2(42,-19),new Vector2(466,62),32,true);
        entryCopy=EntryText(right,"Mode description","",new Vector2(42,-390),new Vector2(544,102),28);
        message=EntryText(right,"Placement instruction","",new Vector2(42,-495),new Vector2(544,100),27);
        entryHint=EntryText(right,"Mode control","",new Vector2(42,-596),new Vector2(544,45),24);entryHint.color=DongbaUIStyle.Accent;
        foreach(var transform in root.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=ScanLayer;
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true)){graphic.material=entryInk;graphic.raycastTarget=false;}
        RefreshPlacementManuscript();
    }
    void RefreshPlacementManuscript(){
        bool sandbox=forestStarted?!forest.StoryModeEnabled:startInSandbox;
        entryHeading.text=DongbaInterface.Choose(forestStarted?"Place the forest":"Enter the forest",forestStarted?"安放这片森林":"走入象形之森");
        entryStoryText.text=DongbaInterface.Choose("01  Story Mode","01  剧情模式");
        entrySandboxText.text=DongbaInterface.Choose("02  Sandbox Mode","02  沙盒模式");
        var selected=new Color(.88f,.82f,.68f);entryStoryPaper.color=sandbox?Color.white:selected;entrySandboxPaper.color=sandbox?selected:Color.white;
        entryStoryText.color=sandbox?DongbaUIStyle.Muted:DongbaUIStyle.Accent;entrySandboxText.color=sandbox?DongbaUIStyle.Accent:DongbaUIStyle.Muted;
        entryCaption.text=sandbox?DongbaInterface.Choose("A living forest","林间生息"):DongbaInterface.Choose("The Father and Son","父与子");
        entryCopy.text=sandbox?DongbaInterface.Choose("Explore, care and shape\nyour own pictographic forest.","自由探索、照料万物，\n布置自己的字形森林。"):DongbaInterface.Choose("Walk with Gaolequ\ninto a Naxi tale.","跟随高勒趣，\n走进纳西神话的故事。");
        entryFather.enabled=!sandbox&&entryFather.sprite!=null;
        entryHint.text=forestStarted?DongbaInterface.Choose("Your progress stays with the forest.","本局进度与森林一起保留。"):DongbaInterface.Choose("X   Choose Story / Sandbox","X   切换剧情／沙盒");
    }
}
}
