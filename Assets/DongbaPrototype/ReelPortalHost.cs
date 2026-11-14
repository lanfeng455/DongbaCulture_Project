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
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OpenRequestedPreview(){
        if(SceneManager.GetActiveScene().name!="Title")return;
        foreach(var arg in Environment.GetCommandLineArgs())if(arg.StartsWith("--")&&(arg.EndsWith("-check")||arg.EndsWith("-preview")||arg.EndsWith("-capture"))){SceneManager.LoadScene("ObliqueForest");break;}
    }
    readonly bool measureHandoff=Array.IndexOf(Environment.GetCommandLineArgs(),"--journey-reel-check")>=0;
    string handoffPhase="idle";double lastFrame;float worstFrame;
    void LateUpdate(){if(!measureHandoff)return;double now=Time.realtimeSinceStartupAsDouble;if(busy&&!InLevel&&lastFrame>0){float ms=(float)((now-lastFrame)*1000);if(ms>worstFrame){worstFrame=ms;Debug.Log("Handoff peak frame: "+ms.ToString("F1")+" ms / "+handoffPhase);}}lastFrame=now;}
    AsyncOperation preload; string preloadedScene; float walkingPhase, reelScroll;
    public float EntryTravel {get;private set;}
    readonly List<GameObject> preparedRoots=new List<GameObject>();DialogueDisplay preparedDialogue;
    public void Prepare(string scene){
        if(preload!=null||busy||!Application.CanStreamedLevelBeLoaded(scene))return;
        preloadedScene=scene;
        SceneManager.sceneLoaded+=ParkPreparedScene;
        preload=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);preload.allowSceneActivation=false;
        StartCoroutine(ActivatePreparedScene());
    }
    IEnumerator ActivatePreparedScene(){
        while(preload!=null&&preload.progress<.9f)yield return null;
        if(!busy&&DialogueDisplay.Instance!=null)forestDialogue=DialogueDisplay.Instance;if(forestDialogue!=null)forestDialogue.ReleaseForSceneHandoff();
        if(preload!=null)preload.allowSceneActivation=true;
    }
    void ParkPreparedScene(Scene scene,LoadSceneMode mode){
        if(scene.name!=preloadedScene)return;SceneManager.sceneLoaded-=ParkPreparedScene;
        // Awake and asset activation happen before the checkpoint; Start/gameplay stays paused.
        foreach(var root in scene.GetRootGameObjects()){
            var dialogue=root.GetComponentInChildren<DialogueDisplay>(true);if(dialogue!=null)preparedDialogue=dialogue;
            var camera=root.GetComponentInChildren<Camera>();
            if(camera!=null){var target=camera.targetTexture;var warm=RenderTexture.GetTemporary(64,64,24);camera.targetTexture=warm;camera.Render();camera.targetTexture=target;RenderTexture.ReleaseTemporary(warm);}
        }
        foreach(var root in scene.GetRootGameObjects())if(root.activeSelf){preparedRoots.Add(root);root.SetActive(false);}
        if(preparedDialogue!=null)preparedDialogue.ReleaseForSceneHandoff();
        if(forestDialogue!=null&&!busy)forestDialogue.RestoreAfterSceneHandoff(false);
    }
    static float WalkInput(){var k=Keyboard.current;return k==null?0:(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);}
    void WalkPaper(float dt,float weight){walkingPhase+=dt*Mathf.Abs(WalkInput())*11.2f;float angle=Mathf.Sin(walkingPhase)*1.25f*weight;fromActor.rectTransform.localRotation=Quaternion.Euler(0,0,angle);}
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
    Coroutine entryRoutine;bool restarting;
    public void Enter(ObliqueForest source,string scene,Action onReturn){if(busy)return;forest=source;resume=onReturn;entryRoutine=StartCoroutine(EnterRoutine(scene));}
    void Update(){
        if(!busy||restarting||Keyboard.current==null)return;
        if(Keyboard.current.escapeKey.wasPressedThisFrame){
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }else if(Keyboard.current.rKey.wasPressedThisFrame)StartCoroutine(RestartForestSession());
    }
    IEnumerator RestartForestSession(){
        restarting=true;if(entryRoutine!=null)StopCoroutine(entryRoutine);
        if(director!=null)director.PuzzleCompleted-=OnCompleted;
        // An asynchronous load cannot be cancelled. Finish activation before unloading;
        // never leave Unity's scene-operation queue stalled at allowSceneActivation=false.
        if(preload!=null){preload.allowSceneActivation=true;while(!preload.isDone)yield return null;}
        var destination=level.IsValid()?level:SceneManager.GetSceneByName(preloadedScene??"Beat_2_Cleansing_Impurity");
        if(destination.IsValid()&&destination.isLoaded){var unload=SceneManager.UnloadSceneAsync(destination);if(unload!=null)while(!unload.isDone)yield return null;}
        SceneManager.sceneLoaded-=ParkPreparedScene;preload=null;preloadedScene=null;preparedRoots.Clear();preparedDialogue=null;level=default;
        if(forestScene.IsValid())SceneManager.SetActiveScene(forestScene);
        foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);hiddenRoots.Clear();
        if(forestDialogue!=null)forestDialogue.RestoreAfterSceneHandoff();
        HideOverlay();ReleaseTextures();InLevel=false;busy=false;completed=false;restorationRequested=false;
        if(forest!=null)forest.RestartFromPortal();restarting=false;
    }
    // Project the real, transformed sprite quad, including the capture's transparent margin.
    static Rect ActorRect(Camera camera,SpriteRenderer actor){
        var bounds=actor.sprite.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
        for(int i=0;i<4;i++){var corner=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0);var p=camera.WorldToViewportPoint(actor.transform.TransformPoint(corner));min=Vector2.Min(min,new Vector2(p.x,p.y));max=Vector2.Max(max,new Vector2(p.x,p.y));}
        var center=(min+max)*.5f;var size=(max-min)*1.14f;return new Rect(center-size*.5f,size);
    }
    // Both portraits share a foot anchor, but keep their own aspect ratio.
    static void PlacePortrait(RawImage image,Rect view){
        float width=view.height*image.texture.width/image.texture.height*Screen.height/Screen.width;
        Place(image,new Rect(view.center.x-width*.5f,view.yMin,width,view.height));
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
        // On entry the real movement/collision motor stays live; only camera ownership changes.
        var rigidbody=destination.GetComponentInParent<Rigidbody>();bool wasKinematic=rigidbody!=null&&rigidbody.isKinematic;
        var oldInterpolation=rigidbody!=null?rigidbody.interpolation:RigidbodyInterpolation.None;var oldDetection=rigidbody!=null?rigidbody.collisionDetectionMode:CollisionDetectionMode.Discrete;
        if(rigidbody!=null&&!outgoingReel){if(!wasKinematic){rigidbody.linearVelocity=Vector3.zero;rigidbody.angularVelocity=Vector3.zero;}var held=rigidbody.transform.position;rigidbody.interpolation=RigidbodyInterpolation.None;rigidbody.collisionDetectionMode=CollisionDetectionMode.Discrete;rigidbody.isKinematic=true;rigidbody.position=held;}
        var controller=destination.GetComponentInParent<DioramaPlayerController>();
        Vector3 entryFeet=controller!=null?controller.transform.position:destination.transform.position;
        if(outgoingReel&&controller!=null){controller.CameraHandoff=true;controller.enabled=true;controller.SetStageMovementLock(false);}
        Vector3 nativePosition=destinationCamera.transform.position;float nativeZoom=destinationCamera.orthographicSize;
        Rect end=ActorRect(destinationCamera,destination);
        if(!outgoingReel){destinationBackgroundTexture=Background(destinationCamera,destination);destinationBackdrop.texture=destinationBackgroundTexture;destinationBackdrop.color=Color.white;destinationBackdrop.gameObject.SetActive(true);}
        toTexture=Portrait(destinationCamera,destination);toActor.texture=toTexture;toActor.gameObject.SetActive(true);destination.forceRenderingOff=true;
        float initialZoom=nativeZoom*end.height/Mathf.Max(.01f,start.height);destinationCamera.orthographicSize=initialZoom;
        Rect initial=ActorRect(destinationCamera,destination);
        destinationCamera.transform.position+=destinationCamera.transform.right*((initial.center.x-start.center.x)*2*initialZoom*destinationCamera.aspect)+destinationCamera.transform.up*((initial.center.y-start.center.y)*2*initialZoom);
        Vector3 initialPosition=destinationCamera.transform.position;
        // A single continuous camera move changes position and lens together.
        handoffPhase="camera blend";float time=0;Rect current=start;
        while(time<1){
            float dt=Time.captureFramerate>0?1f/Time.captureFramerate:Time.unscaledDeltaTime;
            time+=dt/(outgoingReel?2.8f:1.8f);
            Vector3 travel=outgoingReel&&controller!=null?controller.transform.position-entryFeet:Vector3.zero;
            if(outgoingReel)EntryTravel=travel.magnitude;
            float u=Mathf.Clamp01(time);float ease=Mathf.SmoothStep(0,1,u);
            destinationCamera.transform.position=Vector3.Lerp(initialPosition,nativePosition,ease)+travel;
            // Interpolate apparent height, not lens size: linear zoom produces a rapid
            // reciprocal shrink halfway through. Anchor the feet to the same smooth path.
            float height=Mathf.Lerp(start.height,end.height,ease);
            destinationCamera.orthographicSize=nativeZoom*end.height/Mathf.Max(.001f,height);
            var projected=ActorRect(destinationCamera,destination);
            Vector2 desired=new Vector2(Mathf.Lerp(start.center.x,end.center.x,ease),Mathf.Lerp(start.yMin,end.yMin,ease)+height*.5f);
            float span=2*destinationCamera.orthographicSize;
            destinationCamera.transform.position+=destinationCamera.transform.right*((projected.center.x-desired.x)*span*destinationCamera.aspect)+destinationCamera.transform.up*((projected.center.y-desired.y)*span);
            current=ActorRect(destinationCamera,destination);PlacePortrait(fromActor,current);PlacePortrait(toActor,current);
            // The reel is a fixed full-screen page: crossfade it without camera zoom.
            // Only the actor and the diorama world follow the camera projection.
            float oldScale=initialZoom/destinationCamera.orthographicSize,newScale=nativeZoom/destinationCamera.orthographicSize;
            Vector2 oldCenter=current.center+(Vector2.one*.5f-start.center)*oldScale,newCenter=current.center+(Vector2.one*.5f-end.center)*newScale;
            Place(backdrop,new Rect(0,0,1,1));backdrop.uvRect=outgoingReel?new Rect(reelScroll,0,1,1):new Rect(Vector2.one*.5f-oldCenter/oldScale,Vector2.one/oldScale);
            Place(destinationBackdrop,new Rect(0,0,1,1));destinationBackdrop.uvRect=outgoingReel?new Rect(Vector2.one*.5f-newCenter/newScale,Vector2.one/newScale):new Rect(0,0,1,1);
            Rect reelUv=outgoingReel?backdrop.uvRect:destinationBackdrop.uvRect;MaxReelUvError=Mathf.Max(MaxReelUvError,(reelUv.size-Vector2.one).magnitude);
            if(outgoingReel){reelScroll+=WalkInput()*dt*.055f;WalkPaper(dt,1-ease);}
            float t=Mathf.SmoothStep(0,1,time);float appearance=Mathf.SmoothStep(0,1,Mathf.InverseLerp(outgoingReel?.45f:.65f,1,time));fromActor.color=new Color(1,1,1,1-appearance);toActor.color=new Color(1,1,1,appearance);backdrop.color=new Color(1,1,1,1-t);
            yield return null;
        }
        destinationCamera.transform.position=nativePosition+(outgoingReel&&controller!=null?controller.transform.position-entryFeet:Vector3.zero);destinationCamera.orthographicSize=nativeZoom;
        Rect actual=ActorRect(destinationCamera,destination);PlacePortrait(fromActor,actual);PlacePortrait(toActor,actual);
        LastPositionErrorPixels=Vector2.Scale(current.center-actual.center,new Vector2(Screen.width,Screen.height)).magnitude;LastHeightErrorPixels=Mathf.Abs(current.height-actual.height)*Screen.height;
        if(outgoingReel&&controller!=null)controller.CameraHandoff=false;
        fromActor.rectTransform.localRotation=Quaternion.identity;
        if(rigidbody!=null&&!outgoingReel){rigidbody.isKinematic=wasKinematic;rigidbody.collisionDetectionMode=oldDetection;rigidbody.interpolation=oldInterpolation;}
        destination.forceRenderingOff=false;HideOverlay();ReleaseTextures();
    }
    IEnumerator EnterRoutine(string scene){
        busy=true;completed=false;restorationRequested=false;MaxReelUvError=0;EntryTravel=0;reelScroll=0;
        if(!Application.CanStreamedLevelBeLoaded(scene)){Debug.LogError("Reel destination not included in build: "+scene);resume?.Invoke();busy=false;yield break;}
        handoffPhase="source capture";worstFrame=0;
        yield return new WaitForEndOfFrame(); // Capture after forest UI/ink fading has settled for this frame.
        var source=forest.ReelActorRenderer;var sourceCamera=forest.ReelViewCamera;var start=ActorRect(sourceCamera,source);BeginOverlay(sourceCamera,source);source.forceRenderingOff=true;
        forestScene=forest.gameObject.scene;forestDialogue=DialogueDisplay.Instance;if(forestDialogue!=null){forestDialogue.HideDialogue();forestDialogue.ReleaseForSceneHandoff();}
        hiddenRoots.Clear();foreach(var root in forestScene.GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
        handoffPhase="scene activation";
        var load=preload!=null&&preloadedScene==scene?preload:SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);preload=load;preloadedScene=scene;load.allowSceneActivation=true;
        while(!load.isDone){float dt=Time.unscaledDeltaTime;reelScroll+=WalkInput()*dt*.055f;backdrop.uvRect=new Rect(reelScroll,0,1,1);WalkPaper(dt,1);yield return null;}preload=null;level=SceneManager.GetSceneByName(scene);SceneManager.SetActiveScene(level);
        if(forestDialogue!=null)forestDialogue.ReleaseForSceneHandoff();
        foreach(var root in preparedRoots)if(root!=null)root.SetActive(true);preparedRoots.Clear();
        if(preparedDialogue!=null)preparedDialogue.RestoreAfterSceneHandoff();
        handoffPhase="level startup";
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
    void OnDestroy(){SceneManager.sceneLoaded-=ParkPreparedScene;ReleaseTextures();if(snapshotMaterial!=null)Destroy(snapshotMaterial);}
}}
