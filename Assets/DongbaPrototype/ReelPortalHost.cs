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
    Material snapshotMaterial;bool busy,completed,restorationRequested;PuzzleDirectorBase director;ObliqueForest forest;Action resume;
    readonly List<GameObject> hiddenRoots=new List<GameObject>();Scene level,forestScene;DialogueDisplay forestDialogue;
    RawImage destinationBackdrop,backdrop,fromActor,toActor;Canvas overlay;RenderTexture backgroundTexture,destinationBackgroundTexture,fromTexture,toTexture;
    public bool InLevel {get;private set;} public bool IsBusy=>busy;
    public float MaxReelUvError {get;private set;}
    public float LastPositionErrorPixels {get;private set;} public float LastHeightErrorPixels {get;private set;}
    void Awake(){
        DontDestroyOnLoad(gameObject);var go=new GameObject("Continuous actor handoff",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
        overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=31000;
        destinationBackdrop=Image("Incoming world / camera projection");destinationBackdrop.transform.SetAsFirstSibling();
        backdrop=Image("Previous world");var rect=backdrop.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        snapshotMaterial=new Material(Resources.Load<Shader>("ReelSnapshotFade"));backdrop.material=destinationBackdrop.material=snapshotMaterial;
        fromActor=Image("Same actor / outgoing appearance");toActor=Image("Same actor / incoming appearance");HideOverlay();
    }
    RawImage Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));go.transform.SetParent(overlay.transform,false);var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;}
    void HideOverlay(){destinationBackdrop.gameObject.SetActive(false);backdrop.gameObject.SetActive(false);fromActor.gameObject.SetActive(false);toActor.gameObject.SetActive(false);}
    public void Enter(ObliqueForest source,string scene,Action onReturn){if(busy)return;forest=source;resume=onReturn;StartCoroutine(EnterRoutine(scene));}
    // Project the real, transformed sprite quad, including the capture's transparent margin.
    static Rect ActorRect(Camera camera,SpriteRenderer actor){
        var bounds=actor.sprite.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
        for(int i=0;i<4;i++){var corner=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0);var p=camera.WorldToViewportPoint(actor.transform.TransformPoint(corner));min=Vector2.Min(min,new Vector2(p.x,p.y));max=Vector2.Max(max,new Vector2(p.x,p.y));}
        var center=(min+max)*.5f;var size=(max-min)*1.14f;return new Rect(center-size*.5f,size);
    }
    static void Place(RawImage image,Rect view){var rect=image.rectTransform;rect.anchorMin=view.min;rect.anchorMax=view.max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    static RenderTexture Background(Camera camera,SpriteRenderer actor){
        var rt=new RenderTexture(Screen.width,Screen.height,24);rt.wrapMode=TextureWrapMode.Clamp;bool hidden=actor.forceRenderingOff;actor.forceRenderingOff=true;var previous=camera.targetTexture;camera.targetTexture=rt;camera.Render();camera.targetTexture=previous;actor.forceRenderingOff=hidden;return rt;
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
        backdrop.texture=backgroundTexture;backdrop.uvRect=new Rect(0,0,1,1);backdrop.color=Color.white;fromActor.texture=fromTexture;fromActor.color=Color.white;toActor.color=Color.clear;
        Place(fromActor,ActorRect(camera,actor));Place(toActor,ActorRect(camera,actor));backdrop.gameObject.SetActive(true);fromActor.gameObject.SetActive(true);
    }
    IEnumerator MatchActor(Rect start,Camera destinationCamera,SpriteRenderer destination,bool outgoingReel){
        // Freeze actor world transforms. Only a real orthographic camera moves and zooms.
        var rigidbody=destination.GetComponentInParent<Rigidbody>();bool wasKinematic=rigidbody!=null&&rigidbody.isKinematic;
        var oldInterpolation=rigidbody!=null?rigidbody.interpolation:RigidbodyInterpolation.None;var oldDetection=rigidbody!=null?rigidbody.collisionDetectionMode:CollisionDetectionMode.Discrete;
        if(rigidbody!=null){if(!wasKinematic){rigidbody.linearVelocity=Vector3.zero;rigidbody.angularVelocity=Vector3.zero;}var held=rigidbody.transform.position;rigidbody.interpolation=RigidbodyInterpolation.None;rigidbody.collisionDetectionMode=CollisionDetectionMode.Discrete;rigidbody.isKinematic=true;rigidbody.position=held;}
        Vector3 nativePosition=destinationCamera.transform.position;float nativeZoom=destinationCamera.orthographicSize;
        Rect end=ActorRect(destinationCamera,destination);
        destinationBackgroundTexture=Background(destinationCamera,destination);destinationBackdrop.texture=destinationBackgroundTexture;destinationBackdrop.color=Color.white;destinationBackdrop.gameObject.SetActive(true);
        toTexture=Portrait(destinationCamera,destination);toActor.texture=toTexture;toActor.gameObject.SetActive(true);destination.forceRenderingOff=true;
        float initialZoom=nativeZoom*end.height/Mathf.Max(.01f,start.height);destinationCamera.orthographicSize=initialZoom;
        Rect initial=ActorRect(destinationCamera,destination);
        destinationCamera.transform.position+=destinationCamera.transform.right*((initial.center.x-start.center.x)*2*initialZoom*destinationCamera.aspect)+destinationCamera.transform.up*((initial.center.y-start.center.y)*2*initialZoom);
        Vector3 initialPosition=destinationCamera.transform.position;
        // A single continuous camera move changes position and lens together.
        float time=0;Rect current=start;
        while(time<1){
            time+=(Time.captureFramerate>0?1f/Time.captureFramerate:Time.unscaledDeltaTime)/1.8f;
            float u=Mathf.Clamp01(time);float ease=u*u*u*(u*(u*6-15)+10);
            destinationCamera.transform.position=Vector3.Lerp(initialPosition,nativePosition,ease);
            destinationCamera.orthographicSize=Mathf.Lerp(initialZoom,nativeZoom,ease);
            current=ActorRect(destinationCamera,destination);Place(fromActor,current);Place(toActor,current);
            // The reel is a fixed full-screen page: crossfade it without camera zoom.
            // Only the actor and the diorama world follow the camera projection.
            float oldScale=initialZoom/destinationCamera.orthographicSize,newScale=nativeZoom/destinationCamera.orthographicSize;
            Vector2 oldCenter=current.center+(Vector2.one*.5f-start.center)*oldScale,newCenter=current.center+(Vector2.one*.5f-end.center)*newScale;
            Place(backdrop,new Rect(0,0,1,1));backdrop.uvRect=outgoingReel?new Rect(0,0,1,1):new Rect(Vector2.one*.5f-oldCenter/oldScale,Vector2.one/oldScale);
            Place(destinationBackdrop,new Rect(0,0,1,1));destinationBackdrop.uvRect=outgoingReel?new Rect(Vector2.one*.5f-newCenter/newScale,Vector2.one/newScale):new Rect(0,0,1,1);
            Rect reelUv=outgoingReel?backdrop.uvRect:destinationBackdrop.uvRect;MaxReelUvError=Mathf.Max(MaxReelUvError,reelUv.position.magnitude+(reelUv.size-Vector2.one).magnitude);
            float t=Mathf.SmoothStep(0,1,time);float appearance=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,time));fromActor.color=new Color(1,1,1,1-appearance);toActor.color=new Color(1,1,1,appearance);backdrop.color=new Color(1,1,1,1-t);
            yield return null;
        }
        destinationCamera.transform.position=nativePosition;destinationCamera.orthographicSize=nativeZoom;
        Rect actual=ActorRect(destinationCamera,destination);Place(fromActor,actual);Place(toActor,actual);
        LastPositionErrorPixels=Vector2.Scale(current.center-actual.center,new Vector2(Screen.width,Screen.height)).magnitude;LastHeightErrorPixels=Mathf.Abs(current.height-actual.height)*Screen.height;
        if(rigidbody!=null){rigidbody.isKinematic=wasKinematic;rigidbody.collisionDetectionMode=oldDetection;rigidbody.interpolation=oldInterpolation;}
        destination.forceRenderingOff=false;HideOverlay();ReleaseTextures();
    }
    IEnumerator EnterRoutine(string scene){
        busy=true;completed=false;restorationRequested=false;MaxReelUvError=0;
        if(!Application.CanStreamedLevelBeLoaded(scene)){Debug.LogError("Reel destination not included in build: "+scene);resume?.Invoke();busy=false;yield break;}
        yield return new WaitForEndOfFrame(); // Capture after forest UI/ink fading has settled for this frame.
        var source=forest.ReelActorRenderer;var sourceCamera=forest.ReelViewCamera;var start=ActorRect(sourceCamera,source);BeginOverlay(sourceCamera,source);source.forceRenderingOff=true;
        forestScene=forest.gameObject.scene;forestDialogue=DialogueDisplay.Instance;if(forestDialogue!=null){forestDialogue.HideDialogue();forestDialogue.ReleaseForSceneHandoff();}
        hiddenRoots.Clear();foreach(var root in forestScene.GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
        var load=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);while(!load.isDone)yield return null;level=SceneManager.GetSceneByName(scene);SceneManager.SetActiveScene(level);
        var controller=FindFirstObjectByType<DioramaPlayerController>();if(controller!=null)controller.SetStageMovementLock(true);
        yield return null;yield return new WaitForEndOfFrame();
        var destination=controller!=null?controller.BodyRenderer:null;var destinationCamera=Camera.main;if(controller!=null){controller.enabled=false;destinationCamera.transform.position=controller.transform.position+new Vector3(0,17,-20);}
        foreach(var root in level.GetRootGameObjects()){director=root.GetComponentInChildren<PuzzleDirectorBase>(true);if(director!=null)break;}
        if(director!=null){director.PuzzleCompleted+=OnCompleted;completed=director.IsCompleted;}
        if(destination!=null&&destinationCamera!=null)yield return MatchActor(start,destinationCamera,destination,true);else {Debug.LogError("Missing destination actor/camera for reel match");HideOverlay();ReleaseTextures();}
        if(controller!=null){controller.enabled=true;controller.SetStageMovementLock(false);}InLevel=true;
        while(!completed){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Application.Quit();yield return null;}
        yield return new WaitForSecondsRealtime(1);if(controller!=null){controller.SetStageMovementLock(true);controller.enabled=false;}
        start=ActorRect(destinationCamera,destination);BeginOverlay(destinationCamera,destination);destination.forceRenderingOff=true;if(director!=null)director.PuzzleCompleted-=OnCompleted;
        var unload=SceneManager.UnloadSceneAsync(level);while(!unload.isDone)yield return null;
        SceneManager.SetActiveScene(forestScene);foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);if(forestDialogue!=null)forestDialogue.RestoreAfterSceneHandoff();
        forest.SetReelActorScale(1);forest.SetReelTransitionFocus(0);resume?.Invoke();yield return null;yield return new WaitForEndOfFrame();
        forest.enabled=false;yield return MatchActor(start,sourceCamera,source,false);forest.enabled=true;InLevel=false;busy=false;if(restorationRequested)forest.BeginRestorationEnding();
    }
    void ReleaseTextures(){destinationBackdrop.texture=backdrop.texture=fromActor.texture=toActor.texture=null;foreach(var texture in new[]{backgroundTexture,destinationBackgroundTexture,fromTexture,toTexture})if(texture!=null){texture.Release();Destroy(texture);}backgroundTexture=destinationBackgroundTexture=fromTexture=toTexture=null;}
    // Bind only to the final rescue/death sequence, not ordinary puzzle completion.
    public void ReturnToForestForRestoration(){if(!InLevel)return;restorationRequested=true;completed=true;}
    void OnCompleted(){completed=true;}
    void OnDestroy(){ReleaseTextures();if(snapshotMaterial!=null)Destroy(snapshotMaterial);}
}}
