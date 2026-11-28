#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    bool XRFrontPanelOutside(RectTransform panel){
        var corners=new Vector3[4];panel.GetWorldCorners(corners);
        foreach(var point in corners)
            if(Vector3.Dot(point,xrFrontOutward)<=xrFrontBoundary||point.y>0)return false;
        return true;
    }
    IEnumerator VerifyFixedMRUI(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=new Vector3(-7,0,-2.5f);lastDryPosition=Feet;
        cam.fieldOfView=60;cam.transform.position=new Vector3(0,56,-76);cam.transform.LookAt(new Vector3(0,0,-10));
        AnchorXRReadingFrame();careCount=FlourishThreshold;yield return null;yield return null;
        UpdateNatureUnlock();DeferNatureWelcome();natureFirstComplete=true;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);UpdateXRHUD(true,"");
        string folder="Verification/mr-box-ui";Directory.CreateDirectory(folder+"/frames");
        var basePosition=cam.transform.position;var baseRotation=cam.transform.rotation;
        var anchorPosition=xrReadingAnchor.position;var anchorRotation=xrReadingAnchor.rotation;var anchorScale=xrReadingAnchor.lossyScale;
        int revision=xrReadingRevision;
        yield return new WaitForSeconds(.8f);UpdateNatureUI();
        CaptureSceneToTexture(folder+"/forest-and-front.png");
        check(XRFrontPanelOutside(xrObjectiveNote)&&XRFrontPanelOutside(xrContextNote)&&XRFrontPanelOutside(xrControlsTab)&&XRFrontPanelOutside(natureTab),"MR box: every passive note is beyond the forest footprint and below its floor");
        // Inspect the front surface by looking down; do not move it to the head.
        cam.transform.LookAt(xrReadingAnchor.TransformPoint(new Vector3(0,-.24f,0)));
        var glanceRotation=cam.transform.rotation;
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");yield return null;
        check(xrHelpOpen&&!natureTab.gameObject.activeSelf&&!xrControlsTab.gameObject.activeSelf&&XRFrontPanelOutside(xrHelpNote)&&xrReadingRevision==revision,"MR box: controls unfold on the front without repositioning");
        CaptureSceneToTexture(folder+"/controls-front.png");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");
        ToggleNatureEditor();NatureGo(NaturePage.Plants);yield return new WaitForSeconds(.4f);
        check(XRFrontPanelOutside(naturePanel),"MR box: expanded invitation catalogue stays outside the forest as well");
        CaptureSceneToTexture(folder+"/menu-front.png");
        var menuPosition=naturePanel.position;var menuRotation=naturePanel.rotation;
        var subtitleLocal=forestSubtitles.SubtitlePanel.localPosition;
        cam.transform.position+=new Vector3(3,1,2);cam.transform.rotation=glanceRotation*Quaternion.Euler(5,22,0);
        yield return null;yield return null;
        check(Vector3.Distance(xrReadingAnchor.position,anchorPosition)<.001f&&Quaternion.Angle(xrReadingAnchor.rotation,anchorRotation)<.001f&&xrReadingAnchor.lossyScale==anchorScale,"MR box: leaning and turning do not move or resize the front surface");
        check(Vector3.Distance(naturePanel.position,menuPosition)<.001f&&Quaternion.Angle(naturePanel.rotation,menuRotation)<.001f,"MR box: menu remains world-stationary");
        check(forestSubtitles.SubtitlePanel.IsChildOf(cam.transform)&&forestSubtitles.SubtitlePanel.localPosition==subtitleLocal,"MR dialogue: separate headset parent preserves reading position");
        CaptureSceneToTexture(folder+"/menu-look-right.png");
        cam.transform.rotation=baseRotation*Quaternion.Euler(0,110,0);yield return null;
        var offscreen=cam.WorldToViewportPoint(menuPosition);
        check(offscreen.z<0||offscreen.x<0||offscreen.x>1,"MR box: looking away leaves menus behind");
        cam.transform.SetPositionAndRotation(basePosition,glanceRotation);yield return null;
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;
        check(xrReadingRevision==revision&&natureTitle.text=="Plant pictographs","MR box: language switch does not reposition the front");
        CaptureSceneToTexture(folder+"/menu-en.png");
        CloseNatureEditor();DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;
        natureWelcomePending=true;UpdateNatureUI();yield return null;
        check(XRFrontPanelOutside(natureWelcome),"MR box: new-ability introduction also uses the front surface");
        CaptureSceneToTexture(folder+"/unlock-front.png");DeferNatureWelcome();
        UpdateXRHUD(false,"");check(!forestCanvas.gameObject.activeSelf,"MR box: tracking loss hides the front");
        UpdateXRHUD(true,"");yield return null;
        check(xrReadingRevision==revision&&Vector3.Distance(xrReadingAnchor.position,anchorPosition)<.001f,"MR box: tracking recovery does not recenter UI on the face");
        // Normal-speed actual scene rendering: scene, glance at front menu, then
        // a conversation while turning. No composited mock UI or room capture.
        int savedRate=Time.captureFramerate;Time.captureFramerate=30;
        var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var previousTarget=cam.targetTexture;var previousActive=RenderTexture.active;
        float maxMovement=0;
        try{for(int frame=0;frame<240;frame++){
            if(frame==75){ToggleNatureEditor();NatureGo(NaturePage.Plants);}
            if(frame==150){CloseNatureEditor();StartForestMode(true);}
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((frame-35)/40f));
            cam.transform.position=basePosition;
            cam.transform.rotation=frame<150?Quaternion.Slerp(baseRotation,glanceRotation,blend):baseRotation*Quaternion.Euler(0,Mathf.Sin((frame-150)/89f*Mathf.PI)*22,0);
            yield return null;if(frame>=150)forestSubtitles.CompleteCurrentLine();
            maxMovement=Mathf.Max(maxMovement,Vector3.Distance(xrReadingAnchor.position,anchorPosition));
            cam.targetTexture=target;cam.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply(false);
            File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",pixels.EncodeToJPG(88));
            cam.targetTexture=previousTarget;RenderTexture.active=previousActive;
        }}finally{cam.targetTexture=previousTarget;RenderTexture.active=previousActive;Time.captureFramerate=savedRate;target.Release();Destroy(target);Destroy(pixels);cam.transform.SetPositionAndRotation(basePosition,baseRotation);}
        check(maxMovement<.001f,"MR box: eight-second scene/menu/dialogue preview has zero front drift");
        check(!xrObjectiveNote.gameObject.activeSelf&&!xrContextNote.gameObject.activeSelf&&fatherIntroActive,"MR dialogue: passive notes are suppressed during conversation");
        CaptureSceneToTexture(folder+"/dialogue-follow.png");
        var pico=FindFirstObjectByType<DongbaXR.DongbaPicoMR>();
        pico.SimulateRepositionInputForVerification(false,0);pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);yield return null;
        pico.SimulateSurfacePlacementForVerification(new Vector3(2,0,1));yield return null;
        check(xrReadingRevision==revision+1&&forestCanvas.transform.parent==xrReadingAnchor,"MR box: explicit reposition rebuilds the near-edge pose once");
        check(!forestCanvas.transform.IsChildOf(cam.transform)&&forestSubtitles.SubtitlePanel.IsChildOf(cam.transform)&&!pico.ScanReadingFrameForVerification.IsChildOf(cam.transform),"MR presentation: fixed entry/front, head-following dialogue are independent");
        StartForestMode(false);yield return null;
    }
}}
#endif
