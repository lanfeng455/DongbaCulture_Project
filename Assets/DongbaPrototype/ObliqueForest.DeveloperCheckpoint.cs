using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Local, one-shot QA request, available only in editor/development players.
    // No console, network listener, arbitrary paths or executable commands.
    string DeveloperCheckpointPath => Path.Combine(Application.persistentDataPath,"dongba-checkpoint.request");
    IEnumerator WatchDeveloperCheckpoint() {
        Debug.Log("[DongbaCheckpoint] Listening: "+DeveloperCheckpointPath);
        var interval=new WaitForSecondsRealtime(.35f);
        while(true) {
            yield return interval;
            // Keep the request pending through title/placement or an active handoff.
            if(!enabled||!StoryModeEnabled||player==null||DongbaInterface.GameplayBlocked||
                (XRTrackingControlsCamera&&!XRExperienceReady)||
                (reelPortalHost!=null&&(reelPortalHost.IsBusy||reelPortalHost.InLevel)))continue;
            if(!File.Exists(DeveloperCheckpointPath))continue;
            try {
                string command=new FileInfo(DeveloperCheckpointPath).Length<=64
                    ? File.ReadAllText(DeveloperCheckpointPath).Trim() : "invalid";
                File.Delete(DeveloperCheckpointPath); // Never replay a request after restart.
                if(command!="reel") {
                    File.WriteAllText(DeveloperCheckpointPath+".result","ERROR unsupported checkpoint");
                    Debug.LogWarning("[DongbaCheckpoint] Rejected unsupported checkpoint");
                    continue;
                }
                JumpToDeveloperReel();
                File.WriteAllText(DeveloperCheckpointPath+".result","OK reel");
                Debug.Log("[DongbaCheckpoint] OK reel; waiting for player movement");
            } catch(System.Exception error) {
                Debug.LogError("[DongbaCheckpoint] "+error.Message);
            }
        }
    }
    void JumpToDeveloperReel() {
        // Reset only story presentation/locks. Preserve the current forest and ecology.
        ResetStoryJourney();
        fatherIntroActive=false;openingSurveyAge=OpeningSurveySeconds;
        storyOpeningState=StoryOpeningState.Captured;
        if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);
        journeyStage=JourneyStage.EastJourney;
        jumpAge=attackAge=-1;sampledAttackAge=-1;swingContacts.Clear();
        axe.gameObject.SetActive(false);body.localPosition=Vector3.zero;
        heading=Vector3.right;facing=1;ResetCharacterTurns();playerImage.flipX=false;
        player.position=new Vector3(35.5f,0,0);lastDryPosition=Feet;
        EnsureAfterCapturePresentation();EnsureEastPaperContinuation();
        notice="";noticeTime=0;FollowPlayerCamera(100);
    }
}}
