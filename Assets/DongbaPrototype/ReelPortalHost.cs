using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Ivan.Puzzle;
using Ivan.Diorama;
namespace DongbaPrototype {
public class ReelPortalHost:MonoBehaviour {
    bool busy,completed;PuzzleDirectorBase director;ObliqueForest forest;Action resume;
    readonly List<GameObject> hiddenRoots=new List<GameObject>();Scene level,forestScene;DialogueDisplay forestDialogue;
    RawImage backdrop,fromActor,toActor;Canvas overlay;RenderTexture backgroundTexture,fromTexture,toTexture;
    public bool InLevel {get;private set;} public bool IsBusy=>busy;
    public float LastPositionErrorPixels {get;private set;} public float LastHeightErrorPixels {get;private set;}
    void Awake(){
        DontDestroyOnLoad(gameObject);var go=new GameObject("Continuous actor handoff",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
        overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=31000;
        backdrop=Image("Previous world");var rect=backdrop.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        fromActor=Image("Same actor / outgoing appearance");toActor=Image("Same actor / incoming appearance");HideOverlay();
    }
    RawImage Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));go.transform.SetParent(overlay.transform,false);var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;}
    void HideOverlay(){backdrop.gameObject.SetActive(false);fromActor.gameObject.SetActive(false);toActor.gameObject.SetActive(false);}
    public void Enter(ObliqueForest source,string scene,Action onReturn){if(busy)return;forest=source;resume=onReturn;StartCoroutine(EnterRoutine(scene));}
    // Project the real, transformed sprite quad, including the capture's transparent margin.
    static Rect ActorRect(Camera camera,SpriteRenderer actor){
        var bounds=actor.sprite.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
        for(int i=0;i<4;i++){var corner=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0);var p=camera.WorldToViewportPoint(actor.transform.TransformPoint(corner));min=Vector2.Min(min,new Vector2(p.x,p.y));max=Vector2.Max(max,new Vector2(p.x,p.y));}
        var center=(min+max)*.5f;var size=(max-min)*1.14f;return new Rect(center-size*.5f,size);
    }
    static void Place(RawImage image,Rect view){var rect=image.rectTransform;rect.anchorMin=view.min;rect.anchorMax=view.max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    static RenderTexture Background(Camera camera,SpriteRenderer actor){
        var rt=new RenderTexture(Screen.width,Screen.height,24);bool hidden=actor.forceRenderingOff;actor.forceRenderingOff=true;var previous=camera.targetTexture;camera.targetTexture=rt;camera.Render();camera.targetTexture=previous;actor.forceRenderingOff=hidden;return rt;
    }
    static RenderTexture Portrait(Camera view,SpriteRenderer actor){
        var go=new GameObject("Temporary actor capture");var copy=go.AddComponent<SpriteRenderer>();copy.sprite=actor.sprite;copy.sharedMaterial=actor.sharedMaterial;copy.color=actor.color;copy.flipX=actor.flipX;copy.flipY=actor.flipY;
        go.layer=31;go.transform.position=new Vector3(10000,10000,0);var size=actor.transform.lossyScale;go.transform.localScale=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),1);go.transform.rotation=Quaternion.Inverse(view.transform.rotation)*actor.transform.rotation;
        var bounds=copy.bounds;float aspect=Mathf.Max(.1f,bounds.size.x/bounds.size.y);var rt=new RenderTexture(Mathf.Clamp(Mathf.RoundToInt(512*aspect),64,1024),512,24);
        var cameraGo=new GameObject("Temporary actor-only camera");var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=bounds.size.y*1.14f*.5f;camera.aspect=aspect;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.transform.position=bounds.center+Vector3.back*5;camera.targetTexture=rt;camera.Render();camera.targetTexture=null;
        Destroy(cameraGo);Destroy(go);return rt;
    }
    void BeginOverlay(Camera camera,SpriteRenderer actor){
        HideOverlay();ReleaseTextures();backgroundTexture=Background(camera,actor);fromTexture=Portrait(camera,actor);
        backdrop.texture=backgroundTexture;backdrop.color=Color.white;fromActor.texture=fromTexture;fromActor.color=Color.white;toActor.color=Color.clear;
        Place(fromActor,ActorRect(camera,actor));Place(toActor,ActorRect(camera,actor));backdrop.gameObject.SetActive(true);fromActor.gameObject.SetActive(true);
    }
    IEnumerator MatchActor(Rect start,Camera destinationCamera,SpriteRenderer destination){
        toTexture=Portrait(destinationCamera,destination);toActor.texture=toTexture;toActor.gameObject.SetActive(true);destination.forceRenderingOff=true;
        float time=0;Rect current=start;
        while(time<1){
            time+=(Time.captureFramerate>0?1f/Time.captureFramerate:Time.unscaledDeltaTime)/1.35f;float t=Mathf.SmoothStep(0,1,time);Rect end=ActorRect(destinationCamera,destination);
            current=new Rect(Vector2.Lerp(start.position,end.position,t),Vector2.Lerp(start.size,end.size,t));Place(fromActor,current);Place(toActor,current);
            // One moving rectangle throughout; only its paper appearance dissolves near arrival.
            float appearance=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t));fromActor.color=new Color(1,1,1,1-appearance);toActor.color=new Color(1,1,1,appearance);backdrop.color=new Color(1,1,1,1-t);
            yield return null;
        }
        Rect actual=ActorRect(destinationCamera,destination);Place(fromActor,actual);Place(toActor,actual);
        LastPositionErrorPixels=Vector2.Scale(current.center-actual.center,new Vector2(Screen.width,Screen.height)).magnitude;LastHeightErrorPixels=Mathf.Abs(current.height-actual.height)*Screen.height;
        destination.forceRenderingOff=false;HideOverlay();ReleaseTextures();
    }
    IEnumerator EnterRoutine(string scene){
        busy=true;completed=false;
        if(!Application.CanStreamedLevelBeLoaded(scene)){Debug.LogError("Reel destination not included in build: "+scene);resume?.Invoke();busy=false;yield break;}
        yield return new WaitForEndOfFrame(); // Capture after forest UI/ink fading has settled for this frame.
        var source=forest.ReelActorRenderer;var sourceCamera=forest.ReelViewCamera;var start=ActorRect(sourceCamera,source);BeginOverlay(sourceCamera,source);source.forceRenderingOff=true;
        forestScene=forest.gameObject.scene;forestDialogue=DialogueDisplay.Instance;if(forestDialogue!=null){forestDialogue.HideDialogue();forestDialogue.ReleaseForSceneHandoff();}
        hiddenRoots.Clear();foreach(var root in forestScene.GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
        var load=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);while(!load.isDone)yield return null;level=SceneManager.GetSceneByName(scene);SceneManager.SetActiveScene(level);
        var controller=FindFirstObjectByType<DioramaPlayerController>();if(controller!=null)controller.SetStageMovementLock(true);
        yield return null;yield return new WaitForEndOfFrame();
        var destination=controller!=null?controller.BodyRenderer:null;var destinationCamera=Camera.main;
        foreach(var root in level.GetRootGameObjects()){director=root.GetComponentInChildren<PuzzleDirectorBase>(true);if(director!=null)break;}
        if(director!=null){director.PuzzleCompleted+=OnCompleted;completed=director.IsCompleted;}
        if(destination!=null&&destinationCamera!=null)yield return MatchActor(start,destinationCamera,destination);else {Debug.LogError("Missing destination actor/camera for reel match");HideOverlay();ReleaseTextures();}
        if(controller!=null)controller.SetStageMovementLock(false);InLevel=true;
        while(!completed){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Application.Quit();yield return null;}
        yield return new WaitForSecondsRealtime(1);if(controller!=null)controller.SetStageMovementLock(true);
        start=ActorRect(destinationCamera,destination);BeginOverlay(destinationCamera,destination);destination.forceRenderingOff=true;if(director!=null)director.PuzzleCompleted-=OnCompleted;
        var unload=SceneManager.UnloadSceneAsync(level);while(!unload.isDone)yield return null;
        SceneManager.SetActiveScene(forestScene);foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);if(forestDialogue!=null)forestDialogue.RestoreAfterSceneHandoff();
        forest.SetReelActorScale(1);forest.SetReelTransitionFocus(0);resume?.Invoke();yield return null;yield return new WaitForEndOfFrame();
        yield return MatchActor(start,sourceCamera,source);InLevel=false;busy=false;
    }
    void ReleaseTextures(){backdrop.texture=fromActor.texture=toActor.texture=null;foreach(var texture in new[]{backgroundTexture,fromTexture,toTexture})if(texture!=null){texture.Release();Destroy(texture);}backgroundTexture=fromTexture=toTexture=null;}
    void OnCompleted(){completed=true;}
    void OnDestroy(){ReleaseTextures();}
}}
