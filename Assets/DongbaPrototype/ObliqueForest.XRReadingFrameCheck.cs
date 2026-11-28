#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator VerifyFixedMRUI(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=new Vector3(-7,0,-2.5f);lastDryPosition=Feet;
        cam.transform.position=new Vector3(-3,36,-46);cam.transform.LookAt(new Vector3(-7,0,-2.5f));
        // This is a new placement fixture. Subsequent camera poses must never re-anchor.
        AnchorXRReadingFrame();careCount=FlourishThreshold;yield return null;yield return null;
        UpdateNatureUnlock();DeferNatureWelcome();natureFirstComplete=true;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);UpdateXRHUD(true,"");
        ToggleNatureEditor();NatureGo(NaturePage.Plants);yield return new WaitForSeconds(.4f);
        string folder="Verification/mr-fixed-ui";Directory.CreateDirectory(folder+"/frames");
        var anchorPosition=xrReadingAnchor.position;var anchorRotation=xrReadingAnchor.rotation;var anchorScale=xrReadingAnchor.lossyScale;
        var basePosition=cam.transform.position;var baseRotation=cam.transform.rotation;
        var menuPosition=naturePanel.position;var menuRotation=naturePanel.rotation;
        var subtitlePosition=forestSubtitles.SubtitlePanel.position;
        int revision=xrReadingRevision;
        CaptureSceneToTexture(folder+"/menu-front.png");
        cam.transform.position+=new Vector3(2,1,1);cam.transform.rotation=baseRotation*Quaternion.Euler(9,24,0);
        yield return null;yield return null;UpdateNatureUI();
        check(Vector3.Distance(xrReadingAnchor.position,anchorPosition)<.001f&&Quaternion.Angle(xrReadingAnchor.rotation,anchorRotation)<.001f&&xrReadingAnchor.lossyScale==anchorScale,"MR UI: head rotation and translation never move or resize the reading frame");
        check(Vector3.Distance(naturePanel.position,menuPosition)<.001f&&Quaternion.Angle(naturePanel.rotation,menuRotation)<.001f,"MR UI: open invitation menu remains world-stationary");
        check(Vector3.Distance(forestSubtitles.SubtitlePanel.position,subtitlePosition)<.001f,"MR UI: hidden subtitle does not drift with the viewer either");
        CaptureSceneToTexture(folder+"/menu-look-right.png");
        cam.transform.rotation=baseRotation*Quaternion.Euler(0,110,0);yield return null;
        var offscreen=cam.WorldToViewportPoint(menuPosition);
        check(offscreen.z<0||offscreen.x<0||offscreen.x>1,"MR UI: looking away leaves the menu behind instead of dragging it into view");
        cam.transform.SetPositionAndRotation(basePosition,baseRotation);yield return null;
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;
        check(xrReadingRevision==revision&&natureTitle.text=="Plant pictographs","MR UI: language changes update content without repositioning the menu");
        CaptureSceneToTexture(folder+"/menu-en.png");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;
        // A normal-speed camera-motion preview, no game-object or UI animation fakery.
        int savedRate=Time.captureFramerate;Time.captureFramerate=30;
        var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var previousTarget=cam.targetTexture;var previousActive=RenderTexture.active;
        float maxMovement=0,maxAngle=0;
        try{for(int frame=0;frame<240;frame++){
            float phase=frame/239f*Mathf.PI*2;
            cam.transform.position=basePosition+new Vector3(Mathf.Sin(phase)*2,Mathf.Sin(phase)*.5f,0);
            cam.transform.rotation=baseRotation*Quaternion.Euler(Mathf.Sin(phase*2)*5,Mathf.Sin(phase)*20,0);
            yield return null;
            maxMovement=Mathf.Max(maxMovement,Vector3.Distance(xrReadingAnchor.position,anchorPosition));
            maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(xrReadingAnchor.rotation,anchorRotation));
            cam.targetTexture=target;cam.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply(false);
            File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",pixels.EncodeToJPG(88));
            cam.targetTexture=previousTarget;RenderTexture.active=previousActive;
        }}finally{cam.targetTexture=previousTarget;RenderTexture.active=previousActive;Time.captureFramerate=savedRate;target.Release();Destroy(target);Destroy(pixels);cam.transform.SetPositionAndRotation(basePosition,baseRotation);}
        check(maxMovement<.001f&&maxAngle<.001f,"MR UI: eight-second head motion preview has zero frame drift");
        CloseNatureEditor();UpdateXRHUD(true,"");UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");yield return null;
        check(xrHelpOpen&&xrHelpNote.gameObject.activeSelf&&xrReadingRevision==revision,"MR UI: controls open on the same fixed frame");
        CaptureSceneToTexture(folder+"/controls.png");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");
        UpdateXRHUD(false,"");
        check(!forestCanvas.gameObject.activeSelf,"MR UI: tracking or placement loss hides the frame");
        UpdateXRHUD(true,"");yield return null;
        check(xrReadingRevision==revision&&Vector3.Distance(xrReadingAnchor.position,anchorPosition)<.001f,"MR UI: tracking recovery does not snap the frame back onto the face");
        var pico=FindFirstObjectByType<DongbaXR.DongbaPicoMR>();
        pico.SimulateRepositionInputForVerification(false,0);pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);yield return null;
        pico.SimulateSurfacePlacementForVerification(new Vector3(2,0,1));yield return null;
        check(xrReadingRevision==revision+1&&forestCanvas.transform.parent==xrReadingAnchor,"MR UI: explicit forest reposition is the only re-anchor action");
        check(!forestCanvas.transform.IsChildOf(cam.transform)&&!forestSubtitles.SubtitlePanel.IsChildOf(cam.transform)&&!pico.ScanReadingFrameForVerification.IsChildOf(cam.transform),"MR UI: scan, subtitle, invitation and HUD have no headset parent");
        StartForestMode(false);yield return null;
    }
}}
#endif
