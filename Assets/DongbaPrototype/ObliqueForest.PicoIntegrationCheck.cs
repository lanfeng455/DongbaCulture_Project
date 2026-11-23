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
        xrSimTrigger=true;yield return null;yield return null;
        Check(attackAge>=0&&axe.gameObject.activeSelf,"Right trigger invokes the existing axe attack");
        xrSimTrigger=false;yield return new WaitForSeconds(.8f);
        int balance=Balance;
        pico.SimulateRepositionInputForVerification(false,0);
        pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);
        yield return null;
        Check(!XRExperienceReady&&!forestCanvas.gameObject.activeSelf,"Reposition hides forest HUD and suspends input");
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
