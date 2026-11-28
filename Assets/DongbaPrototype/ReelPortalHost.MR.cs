using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ivan.Diorama;
using Ivan.Puzzle;
namespace DongbaPrototype {public partial class ReelPortalHost {
    MRPuzzleStage mrStage;
    public MRPuzzleStage MRStage=>mrStage;
    void ReleaseMRStage(){if(mrStage!=null){if(level.IsValid()&&level.isLoaded)foreach(var root in level.GetRootGameObjects())root.SetActive(false);mrStage.Release();Destroy(mrStage.gameObject);mrStage=null;}if(forest!=null)forest.SetXRPuzzleActive(false);}
    IEnumerator EnterMRRoutine(string destinationName){
        busy=true;completed=false;restorationRequested=false;UnsubscribeCompletion();overlay.enabled=false;
        DongbaLanguageSwitch.Instance?.SetPresentationBlocked(this,true);
        forestScene=forest.gameObject.scene;forestDialogue=DialogueDisplay.Instance;
        var eyes=forest.ReelViewCamera;forest.SetXRPuzzleActive(true);
        if(forestDialogue!=null){forestDialogue.HideDialogue();forestDialogue.ReleaseForSceneHandoff();}
        // Preserve the PICO origin/manager: deactivating it also disables passthrough.
        hiddenRoots.Clear();foreach(var root in forestScene.GetRootGameObjects())if(root.activeSelf&&!eyes.transform.IsChildOf(root.transform)){
            hiddenRoots.Add(root);if(root!=forest.XRReelSheet)root.SetActive(false);
        }
        var load=preload!=null&&preloadedScene==destinationName?preload:SceneManager.LoadSceneAsync(destinationName,LoadSceneMode.Additive);
        preload=load;preloadedScene=destinationName;load.allowSceneActivation=true;while(!load.isDone)yield return null;preload=null;
        level=SceneManager.GetSceneByName(destinationName);SceneManager.SetActiveScene(level);
        // Bind output and input before enabling the parked scene. No desktop camera
        // is allowed to render a single frame directly to the headset.
        Camera native=null;DioramaPlayerController controller=null;
        foreach(var root in level.GetRootGameObjects()){
            if(native==null)native=root.GetComponentInChildren<Camera>(true);
            if(controller==null)controller=root.GetComponentInChildren<DioramaPlayerController>(true);
            if(director==null)director=root.GetComponentInChildren<PuzzleDirectorBase>(true);
            if(beat4Director==null)beat4Director=root.GetComponentInChildren<Beat4Puzzle>(true);
        }
        if(native==null||controller==null){Debug.LogError("[MRJourney] Missing puzzle camera/player: "+destinationName);yield return RestartForestSession();yield break;}
        mrStage=new GameObject("MR fixed puzzle stage").AddComponent<MRPuzzleStage>();SceneManager.MoveGameObjectToScene(mrStage.gameObject,gameObject.scene);
        mrStage.Source=forest;mrStage.Initialize(level,native,eyes,forest.XRStageCentre,forest.XRStageWidth,forest.XRWorldUnits);
        if(forestDialogue!=null)forestDialogue.ReleaseForSceneHandoff();
        foreach(var root in preparedRoots)if(root!=null)root.SetActive(true);preparedRoots.Clear();
        if(preparedDialogue!=null)preparedDialogue.RestoreAfterSceneHandoff();
        controller.SetStageMovementLock(true);yield return null;yield return null;
        if(director is Puzzle1Director){puzzle1Exit=director.GetComponent<Puzzle1ExitJourney>();if(puzzle1Exit!=null){puzzle1Exit.ExitReached+=OnCompleted;completed=puzzle1Exit.HasExited;}}
        else if(director!=null){director.PuzzleCompleted+=OnCompleted;completed=director.IsCompleted;}
        if(beat4Director!=null){beat4Director.JourneyCompleted+=OnCompleted;completed|=beat4Director.IsJourneyCompleted;}
        for(float t=0;t<1.1f;t+=Time.unscaledDeltaTime){mrStage.SetAlpha(Mathf.SmoothStep(0,1,t/1.1f));yield return null;}
        mrStage.SetAlpha(1);if(forest.XRReelSheet!=null)forest.XRReelSheet.SetActive(false);mrStage.Ready=true;controller.SetStageMovementLock(false);InLevel=true;
        Debug.Log("[MRJourney] Playable fixed stage: "+destinationName);
        while(!completed)yield return null;
        mrStage.Ready=false;controller.SetStageMovementLock(true);UnsubscribeCompletion();
        for(float t=0;t<.7f;t+=Time.unscaledDeltaTime){mrStage.SetAlpha(1-Mathf.SmoothStep(0,1,t/.7f));yield return null;}
        ReleaseMRStage();var unload=SceneManager.UnloadSceneAsync(level);while(!unload.isDone)yield return null;
        level=default;preloadedScene=null;preparedDialogue=null;preparedRoots.Clear();StopPreparation();
        SceneManager.SetActiveScene(forestScene);foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);hiddenRoots.Clear();
        if(forestDialogue!=null)forestDialogue.RestoreAfterSceneHandoff();
        forest.SetReelActorScale(1);forest.SetReelTransitionFocus(0);resume?.Invoke();
        InLevel=false;busy=false;entryRoutine=null;resume=null;
        if(restorationRequested)forest.BeginRestorationEnding();forest.RefreshLanguagePresentation();
        DongbaLanguageSwitch.Instance?.SetPresentationBlocked(this,false);
        Debug.Log("[MRJourney] Returned to continuing reel");
    }
}}
