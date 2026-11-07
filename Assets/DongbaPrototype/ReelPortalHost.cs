using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Ivan.Puzzle;
namespace DongbaPrototype {
public class ReelPortalHost:MonoBehaviour {
    CanvasGroup curtain;Image paper;bool busy,completed;PuzzleDirectorBase director;ObliqueForest forest;Action resume;
    readonly List<GameObject> hiddenRoots=new List<GameObject>();Scene level,forestScene;DialogueDisplay forestDialogue;
    public bool InLevel {get;private set;} public bool IsBusy=>busy;
    void Awake(){DontDestroyOnLoad(gameObject);var go=new GameObject("Reel transition curtain",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));go.transform.SetParent(transform,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;curtain=go.GetComponent<CanvasGroup>();curtain.alpha=0;curtain.blocksRaycasts=false;var p=new GameObject("Paper fade",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));p.transform.SetParent(go.transform,false);paper=p.GetComponent<Image>();paper.color=new Color(.91f,.87f,.77f);var rect=paper.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    public void Enter(ObliqueForest source,string scene,Action onReturn){if(busy)return;forest=source;resume=onReturn;StartCoroutine(EnterRoutine(scene));}
    IEnumerator Fade(float target,bool growActor=false){float from=curtain.alpha;float t=0;while(t<1){t+=Time.unscaledDeltaTime/1.1f;curtain.alpha=Mathf.Lerp(from,target,Mathf.SmoothStep(0,1,t));if(forest!=null&&!InLevel)forest.SetReelTransitionFocus(curtain.alpha);if(growActor&&forest!=null)forest.SetReelActorScale(Mathf.Lerp(.18f,1,1-curtain.alpha));yield return null;}curtain.alpha=target;if(growActor&&forest!=null)forest.SetReelActorScale(1);}
    IEnumerator ShrinkActor(){float t=0;while(t<1){t+=Time.unscaledDeltaTime/.9f;forest.SetReelActorScale(Mathf.Lerp(1,.18f,Mathf.SmoothStep(0,1,t)));yield return null;}forest.SetReelActorScale(.18f);}
    IEnumerator EnterRoutine(string scene){
        busy=true;completed=false;yield return ShrinkActor();yield return Fade(1);
        if(!Application.CanStreamedLevelBeLoaded(scene)){Debug.LogError("Reel destination not included in build: "+scene);resume?.Invoke();yield return Fade(0,true);busy=false;yield break;}
        forestScene=forest.gameObject.scene;forestDialogue=DialogueDisplay.Instance;if(forestDialogue!=null){forestDialogue.HideDialogue();forestDialogue.ReleaseForSceneHandoff();}
        hiddenRoots.Clear();foreach(var root in forestScene.GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
        var load=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);while(!load.isDone)yield return null;
        level=SceneManager.GetSceneByName(scene);SceneManager.SetActiveScene(level);
        foreach(var root in level.GetRootGameObjects()){director=root.GetComponentInChildren<PuzzleDirectorBase>(true);if(director!=null)break;}
        if(director!=null){director.PuzzleCompleted+=OnCompleted;completed=director.IsCompleted;}else Debug.LogError("No PuzzleDirectorBase in reel level; handoff cannot detect completion.");
        InLevel=true;yield return Fade(0);
        while(!completed){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Application.Quit();yield return null;}
        // Let the final puzzle action finish before returning to the source artwork.
        yield return new WaitForSecondsRealtime(1);yield return Fade(1);if(director!=null)director.PuzzleCompleted-=OnCompleted;
        var unload=SceneManager.UnloadSceneAsync(level);while(!unload.isDone)yield return null;
        SceneManager.SetActiveScene(forestScene);foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);
        if(forestDialogue!=null)forestDialogue.RestoreAfterSceneHandoff();if(forest!=null)forest.SetReelTransitionFocus(1);resume?.Invoke();InLevel=false;yield return null;yield return Fade(0,true);busy=false;
    }
    void OnCompleted(){completed=true;}
}}
