using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DongbaPrototype {
public sealed partial class DongbaInterface {
    Vignette titleVignette;bool titleVignetteWasActive;Transform titleActor;Vector3 titleActorOriginal;Camera titleCamera;
    void PrepareTitlePresentation(Scene scene){
        // Only replace the legacy title's graphics, not the team's scene or transition component.
        var oldCanvas=titleTransition!=null?titleTransition.GetComponentInParent<Canvas>():null;
        if(oldCanvas!=null){foreach(var g in oldCanvas.GetComponentsInChildren<Graphic>(true))g.enabled=false;foreach(var b in oldCanvas.GetComponentsInChildren<Button>(true))b.interactable=false;}
        var actor=GameObject.Find("Player foot anchor");if(actor!=null&&actor.scene==scene){titleActor=actor.transform;titleActorOriginal=titleActor.position;}
        foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))if(camera.enabled&&camera.gameObject.scene==scene&&camera.orthographic){titleCamera=camera;break;}
        foreach(var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None)){
            if(volume.gameObject.scene!=scene||volume.sharedProfile==null||!volume.sharedProfile.TryGet<Vignette>(out _))continue;
            if(volume.profile.TryGet(out titleVignette)){titleVignetteWasActive=titleVignette.active;titleVignette.active=false;break;}
        }
    }
    void RestoreTitlePresentation(){if(titleVignette!=null)titleVignette.active=titleVignetteWasActive;titleVignette=null;if(titleActor!=null)titleActor.position=titleActorOriginal;titleActor=null;titleCamera=null;}
    void BuildTitleLeaf(){
        var root=DongbaUIStyle.Rect("Story title actions",safe,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(54,0),new Vector2(460,704));titleMenu=root.gameObject;
        var backing=DongbaUIStyle.Rect("Back cover leaf",root,new Vector2(0,1),new Vector2(0,1),new Vector2(7,-7),new Vector2(452,697));DongbaUIStyle.EdgeShadow(DongbaUIStyle.Plate(backing,new Color(.86f,.81f,.68f)),.2f);
        var face=DongbaUIStyle.Rect("Front cover leaf",root,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(460,704));DongbaUIStyle.Plate(face,Color.white);root=face;
        var tab=DongbaUIStyle.Rect("Opening chapter tab",root,new Vector2(0,1),new Vector2(0,1),new Vector2(32,7),new Vector2(42,82));DongbaUIStyle.Plate(tab,DongbaUIStyle.Accent);
        var numeral=DongbaUIStyle.Label("Opening number",tab,"I",new Vector2(5,-24),new Vector2(32,40),26);numeral.alignment=TextAnchor.MiddleCenter;numeral.color=new Color(.98f,.94f,.82f);DongbaUIStyle.Serif(numeral);
        var tag=DongbaUIStyle.Label("Story title caption",root,"A NAXI TALE",new Vector2(94,-34),new Vector2(324,30),17);tag.color=DongbaUIStyle.Accent;
        var title=DongbaUIStyle.Label("Project title",root,"DONGBA",new Vector2(34,-100),new Vector2(396,74),50);DongbaUIStyle.Serif(title);
        var subtitle=DongbaUIStyle.Label("Project subtitle",root,"THE LIVING SCRIPT",new Vector2(38,-180),new Vector2(390,34),21);DongbaUIStyle.Serif(subtitle);
        var premise=DongbaUIStyle.Label("Playable world",root,"When writing becomes a playable world.",new Vector2(39,-229),new Vector2(374,64),20);premise.color=DongbaUIStyle.Muted;
        DongbaUIStyle.Stroke(root,new Vector2(38,-310),new Vector2(382,2),new Color(.3f,.2f,.1f,.4f));
        var tale=DongbaUIStyle.Label("Featured tale",root,"The Father and Son",new Vector2(39,-328),new Vector2(374,40),25);DongbaUIStyle.Serif(tale);
        string[] names={"Begin story","Settings","Quit game"};
        for(int i=0;i<names.Length;i++){int action=i;DongbaUIStyle.Button(root,names[i],new Vector2(32,-401-i*70),new Vector2(392,56),()=>{if(action==0)BeginStory();else Open(action==1?Page.Settings:Page.Quit);},i+1);}
        var note=DongbaUIStyle.Label("Demo note",root,"Work in progress",new Vector2(40,-650),new Vector2(370,27),15);note.color=DongbaUIStyle.Muted;
        SelectFirst(root.gameObject);FitTitleLeaf();
    }
    void FitTitleLeaf(){
        if(titleMenu==null||!onTitle)return;var r=(RectTransform)titleMenu.transform;
        float scale=Mathf.Min(1,(safe.rect.height-48)/704,(safe.rect.width-48)/460);r.localScale=Vector3.one*Mathf.Max(.35f,scale);r.anchoredPosition=new Vector2(Mathf.Clamp(safe.rect.width*.034f,24,54),0);
        // The title illustration, unlike the playable actor, can be composed for a narrow window.
        // Preserve its original ground plane and move only enough to clear the book cover.
        if(titleActor!=null&&titleCamera!=null){
            float edge=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector3(r.rect.xMax,0))).x;
            float shift=Mathf.Max(0,edge+88-titleCamera.WorldToScreenPoint(titleActorOriginal).x);
            titleActor.position=titleActorOriginal+titleCamera.transform.right*(shift*2*titleCamera.orthographicSize/Screen.height);
        }
    }
}
}
