#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    bool XRPanelOnForestSide(RectTransform panel){
        var corners=new Vector3[4];panel.GetWorldCorners(corners);
        foreach(var point in corners)
            if(point.y<-.002f*xrUnits||point.y>xrReadingAnchor.position.y+.002f*xrUnits||Mathf.Abs(point.z-XRReadingWallDepth)>.002f*xrUnits||Mathf.Abs(point.x)>ForestHalfWidth)return false;
        return true;
    }
    bool XRReadingFits(RectTransform panel){
        bool ok=true;
        foreach(var label in panel.GetComponentsInChildren<UnityEngine.UI.Text>()){
            if(!label.isActiveAndEnabled)continue;
            bool fits=label.font==xrReadingFont&&label.fontSize>=24&&label.preferredHeight<=label.rectTransform.rect.height+1;
            if(!fits)Debug.LogError("MR reading overflow/font: "+label.name+" text="+label.text+" preferred="+label.preferredHeight+" box="+label.rectTransform.rect.height);
            ok &= fits;
        }
        return ok;
    }
    bool XRButtonsSeparated(){
        for(int i=0;i<natureButtons.Count;i++)for(int j=i+1;j<natureButtons.Count;j++){
            var a=(RectTransform)natureButtons[i].transform;var b=(RectTransform)natureButtons[j].transform;
            var ar=new Rect(a.anchoredPosition.x,a.anchoredPosition.y-a.sizeDelta.y,a.sizeDelta.x,a.sizeDelta.y);
            var br=new Rect(b.anchoredPosition.x,b.anchoredPosition.y-b.sizeDelta.y,b.sizeDelta.x,b.sizeDelta.y);
            if(ar.Overlaps(br))return false;
        }
        return true;
    }
    IEnumerator VerifyFixedMRUI(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=new Vector3(-7,0,-2.5f);lastDryPosition=Feet;
        cam.fieldOfView=60;cam.transform.position=new Vector3(0,56,-76);cam.transform.LookAt(new Vector3(0,0,-10));
        AnchorXRReadingFrame();careCount=FlourishThreshold;yield return null;yield return null;
        UpdateNatureUnlock();DeferNatureWelcome();natureFirstComplete=true;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);UpdateXRHUD(true,"");
        string folder="Verification/mr-side-ui";Directory.CreateDirectory(folder+"/frames");
        var basePosition=cam.transform.position;var baseRotation=cam.transform.rotation;
        var anchorPosition=xrReadingAnchor.position;var anchorRotation=xrReadingAnchor.rotation;var anchorScale=xrReadingAnchor.lossyScale;
        int revision=xrReadingRevision;
        yield return new WaitForSeconds(.8f);UpdateNatureUI();
        CaptureSceneToTexture(folder+"/forest-and-side.png");
        check(XRPanelOnForestSide(xrObjectiveNote)&&XRPanelOnForestSide(xrContextNote)&&XRPanelOnForestSide(xrControlsTab)&&XRPanelOnForestSide(natureTab),"MR box: passive notes share the vertical plane at the far forest edge");
        // Look from the bottom towards its upright side, without moving the paper.
        cam.transform.LookAt(xrReadingAnchor.TransformPoint(new Vector3(0,-.38f,0)));
        var glanceRotation=cam.transform.rotation;
        float elevation=Mathf.Asin(cam.transform.forward.y)*Mathf.Rad2Deg;
        float floorElevation=Mathf.Asin((baseRotation*Vector3.forward).y)*Mathf.Rad2Deg;
        check(elevation<0&&elevation>floorElevation+10&&elevation<floorElevation+28,"Side UI: reading only raises the gaze from the forest, without looking above eye level");
        var floorJoin=xrReadingAnchor.TransformPoint(new Vector3(0,-XRReadingWidthMetres*XRReadingCanvasHeight/1000f,0));
        check(Mathf.Abs(floorJoin.y)<.001f&&Mathf.Abs(floorJoin.z-ForestHalfDepth)<.005f*xrUnits&&Vector3.Dot(xrReadingAnchor.up,Vector3.up)>.999f,"Side UI: bottom meets the forest edge and side is perpendicular to the ground");
        check(xrSideFold.position==floorJoin,"Side UI: paper crease marks the actual floor-side join");
        check(forestCanvas.sortingOrder<DepthOrder(ForestHalfDepth),"Side UI: foreground forest artwork renders in front of the far paper side");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");yield return null;
        check(xrHelpOpen&&!natureTab.gameObject.activeSelf&&!xrControlsTab.gameObject.activeSelf&&XRPanelOnForestSide(xrHelpNote)&&xrReadingRevision==revision,"MR box: controls unfold on the forest side without repositioning");
        CaptureSceneToTexture(folder+"/controls-side.png");
        check(XRReadingFits(xrHelpNote),"Side UI: controller guide uses regular readable text within its bounds");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");
        ToggleNatureEditor();NatureGo(NaturePage.Plants);yield return new WaitForSeconds(.4f);
        check(XRPanelOnForestSide(naturePanel),"MR box: expanded invitation catalogue stays outside the forest as well");
        CaptureSceneToTexture(folder+"/menu-side.png");
        cam.transform.position=new Vector3(72,62,-66);cam.transform.LookAt(new Vector3(0,20,3));yield return null;
        CaptureSceneToTexture(folder+"/floor-and-side-profile.png");
        cam.transform.SetPositionAndRotation(basePosition,glanceRotation);yield return null;
        var menuPosition=naturePanel.position;var menuRotation=naturePanel.rotation;
        var subtitleLocal=forestSubtitles.SubtitlePanel.localPosition;
        cam.transform.position+=new Vector3(3,1,2);cam.transform.rotation=glanceRotation*Quaternion.Euler(5,22,0);
        yield return null;yield return null;
        check(Vector3.Distance(xrReadingAnchor.position,anchorPosition)<.001f&&Quaternion.Angle(xrReadingAnchor.rotation,anchorRotation)<.001f&&xrReadingAnchor.lossyScale==anchorScale,"MR box: leaning and turning do not move or resize the paper side");
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
        foreach(var language in new[]{DongbaLocale.Chinese,DongbaLocale.English}){
            DongbaLanguage.Set(language,false);
            foreach(var page in new[]{NaturePage.Home,NaturePage.Plants,NaturePage.Animals,NaturePage.Sleeping,NaturePage.Companions}){
                NatureGo(page);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                check(XRReadingFits(naturePanel)&&XRButtonsSeparated(),"Side UI: "+language+" "+page+" text fits and choices have separate bounds");
                if(page==NaturePage.Plants||page==NaturePage.Animals)for(int pageIndex=1;pageIndex<3;pageIndex++){
                    naturePageIndex=pageIndex;natureUIStamp="";yield return null;yield return null;Canvas.ForceUpdateCanvases();
                    check(XRReadingFits(naturePanel)&&XRButtonsSeparated(),"Side UI: "+language+" "+page+" page "+pageIndex+" retains readable glyph names");
                }
            }
            NatureChoose(NatureAction.Plant,null,"plant_01");yield return null;yield return null;
            check(XRReadingFits(naturePanel),"Side UI: "+language+" ground placement instructions fit");
        }
        natureFirstLesson=true;NatureGo(NaturePage.Plants);natureFirstComplete=false;yield return null;yield return null;
        check(XRReadingFits(naturePanel),"Side UI: first-invitation instructions fit the enlarged sheet");
        natureFirstLesson=false;natureFirstComplete=true;
        CloseNatureEditor();DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;
        natureWelcomePending=true;UpdateNatureUI();yield return null;
        check(XRPanelOnForestSide(natureWelcome),"MR box: new-ability introduction also uses the paper side");
        CaptureSceneToTexture(folder+"/unlock-side.png");
        check(XRReadingFits(natureWelcome),"Side UI: Chinese unlock title, copy and two actions are separately readable");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;yield return null;
        check(XRReadingFits(natureWelcome),"Side UI: English unlock uses the same regular reading font and fits");
        CaptureSceneToTexture(folder+"/unlock-en.png");DeferNatureWelcome();DongbaLanguage.Set(DongbaLocale.Chinese,false);
        natureFirstComplete=true;natureAnsweredAt=clock;ToggleNatureEditor();yield return null;yield return null;
        check(!natureWelcome.gameObject.activeSelf&&naturePanel.gameObject.activeSelf,"Side UI: success notice never overlays an open invitation menu");
        CloseNatureEditor();yield return null;yield return null;
        check(natureWelcome.gameObject.activeSelf&&!natureTab.gameObject.activeSelf&&!xrControlsTab.gameObject.activeSelf,"Side UI: success note replaces shortcuts instead of stacking over them");
        yield return new WaitForSeconds(.65f);CaptureSceneToTexture(folder+"/success-side.png");natureAnsweredAt=-100;
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
        check(maxMovement<.001f,"MR box: eight-second scene/menu/dialogue preview has zero side UI drift");
        check(!xrObjectiveNote.gameObject.activeSelf&&!xrContextNote.gameObject.activeSelf&&fatherIntroActive,"MR dialogue: passive notes are suppressed during conversation");
        CaptureSceneToTexture(folder+"/dialogue-follow.png");
        var pico=FindFirstObjectByType<DongbaXR.DongbaPicoMR>();
        pico.SimulateRepositionInputForVerification(false,0);pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);yield return null;
        pico.SimulateSurfacePlacementForVerification(new Vector3(2,0,1));yield return null;
        check(xrReadingRevision==revision+1&&forestCanvas.transform.parent==xrReadingAnchor,"MR box: explicit reposition rebuilds the paper-side pose once");
        check(!forestCanvas.transform.IsChildOf(cam.transform)&&forestSubtitles.SubtitlePanel.IsChildOf(cam.transform)&&!pico.ScanReadingFrameForVerification.IsChildOf(cam.transform),"MR presentation: fixed entry/side UI, head-following dialogue are independent");
        var placedSide=xrReadingAnchor.position;var restoreEye=cam.transform.position;
        cam.transform.position+=Vector3.up*(.5f*xrUnits);AnchorXRReadingFrame();
        check(Vector3.Distance(placedSide,xrReadingAnchor.position)<.001f,"Side UI: reanchoring from a taller eye height never lifts the side away from the ground");
        cam.transform.position=restoreEye;AnchorXRReadingFrame();
        StartForestMode(false);yield return null;
    }
}}
#endif
