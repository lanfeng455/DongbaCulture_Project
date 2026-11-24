#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator CheckPicoIntegration(){
        var report=new StringBuilder();bool passed=true;
        void Check(bool ok,string message){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+message);Debug.Log((ok?"PASS ":"FAIL ")+message);}
        yield return new WaitForSecondsRealtime(2);
        var pico=FindFirstObjectByType<DongbaXR.DongbaPicoMR>();
        Check(pico!=null&&XRTrackingControlsCamera&&!XRExperienceReady,"MR environment waits for surface placement");
        Check(XRArtworkScale==1f&&MaxFriendlyAnimals==6,"MR keeps current forest proportions and animal settings");
        int originalAnimals=animals.Count,originalPlants=plants.Count;
        pico.SimulateSurfacePlacementForVerification(new Vector3(2,0,3));
        yield return null;
        yield return null;yield return null;
        xrSimulating=true;
        Check(XRExperienceReady&&StoryModeEnabled,"Placement starts our existing father-and-son story");
        Check(animals.Count==originalAnimals&&plants.Count==originalPlants,"Original ecology and layout remain intact; no alternate tutorial");
        Check(!cam.orthographic&&forestCanvas.renderMode==RenderMode.WorldSpace,"Tracked perspective camera and spatial HUD enabled");
        Check(fatherIntroActive&&forestSubtitles!=null,"Original bilingual opening dialogue is retained");
        yield return null;
        var canvas=forestSubtitles==null?null:forestSubtitles.GetComponentInChildren<Canvas>(true);
        foreach(var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(candidate.name=="DialogueCanvas")canvas=candidate;
        Check(canvas!=null&&canvas.gameObject.activeInHierarchy&&canvas.renderMode==RenderMode.WorldSpace,"Shared subtitle canvas visible in MR");
        Check(canvas.transform.parent==cam.transform&&forestSubtitles.UsesHeadsetPresentation,"Our subtitle canvas belongs to the view, not the side HUD");
        Check(forestSubtitles.PaperSpeakerGlyph!=null&&forestSubtitles.SubtitlePanel.GetComponent<UnityEngine.UI.Image>().sprite==DongbaUIStyle.PaperSprite,"Original cut-paper portrait and manuscript strip retained");
        if(cam.TryGetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>(out var headPose))headPose.enabled=false;
        cam.stereoTargetEye=StereoTargetEyeMask.None;cam.aspect=16f/9;
        cam.transform.position=new Vector3(-3,36,-46);cam.transform.LookAt(new Vector3(-7,0,-2.5f));
        var corners=new Vector3[4];forestSubtitles.SubtitlePanel.GetWorldCorners(corners);bool inView=true;
        foreach(var corner in corners){var q=cam.WorldToViewportPoint(corner);inView&=q.x>0&&q.x<1&&q.y>0&&q.y<.5f;}
        Check(inView,"Paper strip stays fully inside the lower view");
        Check(Mathf.Abs(Vector3.Dot(canvas.transform.position-cam.transform.position,cam.transform.forward)/pico.worldUnitsPerMetre-1.1f)<.001f,"Subtitle depth is 1.1 metres independent of XR origin scale");
        Directory.CreateDirectory("Verification/pico-integration");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;yield return null;forestSubtitles.CompleteCurrentLine();yield return null;
        Check(forestSubtitles.HeadsetContinuePrompt=="A   继续","Chinese uses the headset A prompt");
        CaptureSceneToTexture("Verification/pico-integration/subtitle-zh.png");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;yield return null;forestSubtitles.CompleteCurrentLine();yield return null;
        Check(forestSubtitles.HeadsetContinuePrompt=="A   Continue"&&forestSubtitles.SubtitleText.text.Length>0,"English dialogue and A prompt retain our presentation");
        CaptureSceneToTexture("Verification/pico-integration/subtitle-en.png");
        // Nearby tabletop geometry would occlude an ordinary world-space subtitle.
        cam.transform.position=new Vector3(-5,16,-14);cam.transform.LookAt(new Vector3(-7,0,-2.5f));yield return null;
        CaptureSceneToTexture("Verification/pico-integration/subtitle-near-table.png");
        var localPosition=((RectTransform)canvas.transform).localPosition;
        cam.transform.rotation*=Quaternion.Euler(0,28,0);yield return null;
        Check(Vector3.Distance(((RectTransform)canvas.transform).localPosition,localPosition)<.001f,"Turning the head preserves subtitle placement without resizing");
        cam.transform.position=new Vector3(-3,36,-46);cam.transform.LookAt(new Vector3(-7,0,-2.5f));
        int before=fatherIntroLine;xrSimA=true;yield return null;yield return null;
        Check(fatherIntroLine==before+1&&jumpAge<0,"Controller A advances one line without jumping");
        yield return new WaitForSecondsRealtime(.3f);
        Check(fatherIntroLine==before+1,"Holding A does not skip further dialogue");
        xrSimA=false;yield return null;
        for(int i=0;i<8&&fatherIntroActive;i++){xrSimA=true;yield return null;xrSimA=false;yield return null;}
        Check(!fatherIntroActive,"Original opening can be completed using controller");
        player.position=new Vector3(-6,0,0);var start=Feet;
        xrSimStick=Vector2.right;yield return new WaitForSeconds(.25f);xrSimStick=Vector2.zero;yield return null;
        Check(Vector3.Distance(start,Feet)>.2f,"Controller moves the original protagonist");
        Check(playerMotion.MovingWeight>.2f,"MR joystick movement drives the shared paper walking pose");
        yield return new WaitForSeconds(.4f);
        xrSimStick=Vector2.left;yield return null;yield return null;
        Check(playerTurn.Width<.99f,"MR joystick uses the same smooth paper turn as PC");
        var namedAnimal=animals.Find(b=>b.speciesIndex==4);interactionTarget=namedAnimal;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);UpdateXRHUD(true,"");
        Check(xrContext.text.StartsWith("猫"),"MR nearby card uses the same dictionary species name");
        xrSimTrigger=true;yield return null;yield return null;
        Check(swingFacing<0&&playerImage.flipX&&playerTurn.Width==1,"Reverse joystick plus trigger locks glyph and axe to one direction");
        Check(Vector3.Distance(axe.position,body.TransformPoint(PlayerHandLocalPosition()))<.001f,"MR hand grip remains aligned during reverse attack");
        xrSimStick=Vector2.zero;
        Check(attackAge>=0&&axe.gameObject.activeSelf,"Right trigger invokes the existing axe attack");
        xrSimTrigger=false;yield return new WaitForSeconds(.8f);
        xrSimA=true;yield return null;xrSimA=false;yield return new WaitForSeconds(.24f);
        Check(jumpAge>=0&&body.localPosition.y>.7f,"MR A jump retains the same visible paper arc");
        yield return new WaitForSeconds(.65f);
        Check(playerMotion.Current.scale.y<.99f,"MR landing uses the shared compression / recovery");
        yield return new WaitForSeconds(.4f);
        Check(playerMotion.Current.scale.y>.99f&&Mathf.Abs(body.position.x-Feet.x)<.001f,"MR character settles at its fixed foot anchor");
        int balance=Balance;
        pico.SimulateRepositionInputForVerification(false,0);
        pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);
        yield return null;
        Check(!XRExperienceReady&&!forestCanvas.gameObject.activeSelf&&!canvas.enabled,"Reposition hides forest HUD and our subtitle without advancing");
        start=Feet;xrSimStick=Vector2.right;yield return new WaitForSeconds(.2f);
        Check(Vector3.Distance(start,Feet)<.001f,"No movement during placement");
        xrSimStick=Vector2.zero;pico.SimulateSurfacePlacementForVerification(new Vector3(0,0,1));yield return null;
        Check(XRExperienceReady&&!fatherIntroActive&&Balance==balance&&animals.Count==originalAnimals,"Reposition preserves story and ecology progress");
        if(cam.TryGetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>(out var pose))pose.enabled=false;
        cam.transform.position=new Vector3(-3,18,-22);cam.transform.LookAt(new Vector3(-3,0,0));
        cam.stereoTargetEye=StereoTargetEyeMask.None;yield return null;
        Directory.CreateDirectory("Verification/pico-integration");
        CaptureSceneToTexture("Verification/pico-integration/forest.png");
        report.AppendLine("Editor simulated placement/controller checks only; no PICO headset or Android build toolchain present.");
        File.WriteAllText("Verification/pico-integration/checks.txt",report.ToString());
        UnityEditor.EditorApplication.Exit(passed?0:1);
    }
}}
#endif
