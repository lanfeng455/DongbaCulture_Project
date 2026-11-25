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
        pico.SimulateModeChoiceForVerification(false);pico.SimulateModeChoiceForVerification(true);
        Check(pico.startInSandbox,"X selects sandbox before placement");pico.SimulateModeChoiceForVerification(true);
        Check(pico.startInSandbox,"Holding X does not cycle the mode repeatedly");
        pico.SimulateModeChoiceForVerification(false);pico.SimulateModeChoiceForVerification(true);pico.SimulateModeChoiceForVerification(false);
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
        // View-space notes retain their angles when the user turns or repositions.
        var hud=(RectTransform)forestCanvas.transform;
        Check(hud.parent==cam.transform&&Mathf.Abs(hud.localPosition.x)<.001f,"MR notes are centred on the viewing camera, not a panel to the right of the map");
        var layoutBefore=ProjectedInkRect(new Vector3(-12,0,4),3,4);
        var beforeHead=cam.transform.rotation;cam.transform.rotation*=Quaternion.Euler(0,65,0);
        Check(ProjectedInkRect(new Vector3(-12,0,4),3,4)==layoutBefore,"MR forest spacing and animal clearance are independent of head direction");
        cam.transform.rotation=beforeHead;
        bool notesInside=true;var noteCorners=new Vector3[4];
        foreach(var note in new[]{xrObjectiveNote,xrContextNote,xrHelpNote}){
            note.GetWorldCorners(noteCorners);
            foreach(var corner in noteCorners){var q=cam.WorldToViewportPoint(corner);notesInside&=q.x>.06f&&q.x<.94f&&q.y>.06f&&q.y<.94f;}
        }
        Check(notesInside,"Objective, nearby action and controls fit within normal view safe margins");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");
        Check(xrHelpOpen&&xrHelpNote.gameObject.activeSelf&&!xrContextNote.gameObject.activeSelf,"B unfolds controls without stacking interaction UI");
        UpdateXRHelpInput(true,true);Check(xrHelpOpen,"Holding B does not flicker the controls");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);UpdateXRHUD(true,"");yield return null;
        Check(xrControllerHints.text.Contains("切换中英"),"MR controls include the bilingual switch in Chinese");
        CaptureSceneToTexture("Verification/pico-integration/controls-zh.png");
        DongbaLanguage.Set(DongbaLocale.English,false);UpdateXRHUD(true,"");yield return null;
        CaptureSceneToTexture("Verification/pico-integration/controls-en.png");
        UpdateXRHelpInput(false,true);UpdateXRHelpInput(true,true);UpdateXRHUD(true,"");
        Check(!xrHelpOpen&&!xrHelpNote.gameObject.activeSelf,"B folds the guide back away");
        var savedFatherIntro=fatherIntroActive;fatherIntroActive=true;UpdateXRHUD(true,"");
        Check(!xrObjectiveNote.gameObject.activeSelf&&!xrContextNote.gameObject.activeSelf&&!xrPanelStatus.gameObject.activeSelf,"Dialogue keeps the view clear of objective, context and help tab");
        fatherIntroActive=savedFatherIntro;UpdateXRHUD(true,"");
        Check(groundMaterial.shader.name=="Dongba/XR Forest Paper"&&xrPaperCoverage!=null,"MR uses a single baked porous-paper coverage mask");
        bool finitePlants=true;foreach(var plant in plants)finitePlants&=!float.IsNaN(plant.home.z)&&!float.IsInfinity(plant.home.z);
        Check(finitePlants&&plants.FindAll(p=>p.tree).Count>40,"MR woodland contains finite, connected forest groups");
        report.AppendLine("MR trees="+plants.FindAll(p=>p.tree).Count+" undergrowth="+plants.FindAll(p=>!p.tree).Count+" animals="+animals.Count);
        // Neutral room surface is a verification backdrop, not part of the game.
        var backdrop=GameObject.CreatePrimitive(PrimitiveType.Plane);backdrop.name="Verification only / room surface";
        backdrop.transform.position=new Vector3(0,-.3f,0);backdrop.transform.localScale=new Vector3(30,1,30);
        var roomMat=new Material(Shader.Find("Unlit/Color"));roomMat.color=new Color(.47f,.52f,.53f);backdrop.GetComponent<Renderer>().sharedMaterial=roomMat;
        cam.backgroundColor=new Color(.47f,.52f,.53f,0);
        cam.transform.position=new Vector3(-3,36,-46);cam.transform.LookAt(new Vector3(-7,0,-2.5f));
        yield return null;yield return null;
        CaptureSceneToTexture("Verification/pico-integration/mr-overview.png");
        cam.transform.position=new Vector3(-12,17,-20);cam.transform.LookAt(new Vector3(-9,0,-2.5f));
        var nearCat=animals.Find(b=>b.speciesIndex==4);player.position=nearCat.root.position+Vector3.left*.9f;UpdateXRHUD(true,"");
        yield return null;yield return null;CaptureSceneToTexture("Verification/pico-integration/mr-close.png");
        Check(xrContextNote.gameObject.activeSelf&&interactionTarget!=null,"Nearby-object name and controller actions appear during real proximity selection");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;yield return null;
        CaptureSceneToTexture("Verification/pico-integration/mr-close-zh.png");
        DongbaLanguage.Set(DongbaLocale.English,false);
        bool desktopHidden=true;foreach(Transform child in forestCanvas.transform)if(!child.name.StartsWith("PICO ")&&child.name!="DialogueCanvas")desktopHidden&=!child.gameObject.activeSelf;
        Check(desktopHidden,"Desktop HUD stays hidden after the complete LateUpdate sequence");
        Destroy(backdrop);Destroy(roomMat);cam.backgroundColor=Color.clear;
        Check(cam.backgroundColor.a==0&&!cam.allowHDR&&!cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing,"MR camera preserves transparent room background without HDR/post-processing");
        // Test the actual rendered alpha, not just the inspector's background colour.
        int savedMask=cam.cullingMask;cam.cullingMask=0;
        var savedTarget=cam.targetTexture;var savedActive=RenderTexture.active;
        var alphaTarget=new RenderTexture(32,32,24,RenderTextureFormat.ARGB32);
        var alphaPixels=new Texture2D(32,32,TextureFormat.RGBA32,false);
        try{
            cam.targetTexture=alphaTarget;cam.Render();RenderTexture.active=alphaTarget;
            alphaPixels.ReadPixels(new Rect(0,0,32,32),0,0);alphaPixels.Apply();
            Check(alphaPixels.GetPixel(16,16).a<.01f,"Empty MR view renders transparent pixels for the passthrough compositor");
        }finally{cam.targetTexture=savedTarget;RenderTexture.active=savedActive;cam.cullingMask=savedMask;alphaTarget.Release();Destroy(alphaTarget);Destroy(alphaPixels);}

        // Reproduce the reported freeze: a head-tracked perspective pose cannot
        // ever satisfy the desktop shot's camera target or orthographic size.
        var mrSpots=new[]{ForestSpawn,new Vector3(-44,0,0)};
        for(int shot=0;shot<mrSpots.Length;shot++){
            RestartRun();fatherIntroActive=false;ResetForestSubtitles();
            player.position=mrSpots[shot];lastDryPosition=Feet;
            storyFatherRoot.position=Feet+Vector3.right*1.5f;
            cam.transform.position=new Vector3(-3,36,-46);cam.transform.LookAt(new Vector3(-7,0,0));
            cam.orthographicSize=6.6f;var headPosition=cam.transform.position;var headRotation=cam.transform.rotation;
            while(Balance>StoryCaptureBalanceThreshold)RegisterDamage();
            yield return null;yield return null;
            Check(journeyStage==JourneyStage.BoarPass,"Ecology threshold starts MR boar pass at location "+shot);
            var initialBoar=storyBoar.root.position;
            yield return new WaitForSeconds(.9f);
            Check(boarPassMoving&&storyBoar.root.gameObject.activeSelf&&storyBoar.root.position.x>initialBoar.x,"MR boar appears and runs without waiting for PC camera at location "+shot);
            if(shot==0)CaptureSceneToTexture("Verification/pico-integration/boar-pass.png");
            float wait=0;bool onPaper=true,jumped=false;
            while(journeyStage==JourneyStage.BoarPass&&wait<8f){
                wait+=Time.deltaTime;
                onPaper&=Mathf.Abs(storyBoar.root.position.x)<ForestHalfWidth&&Mathf.Abs(storyBoar.root.position.z)<ForestHalfDepth;
                jumped|=storyBoar.art.localPosition.y>.5f;
                yield return null;
            }
            Check(boarPassComplete&&onPaper&&jumped,"MR boar crosses river on paper and reaches the east clearing at location "+shot);
            Check(Vector3.Distance(cam.transform.position,headPosition)<.001f&&Quaternion.Angle(cam.transform.rotation,headRotation)<.01f,"Cutscene never moves the tracked camera at location "+shot);
            wait=0;while(!ManualStoryDialogueActive&&wait<5){wait+=Time.deltaTime;yield return null;}
            Check(ManualStoryDialogueActive,"Hunting invitation follows the MR pass at location "+shot);
            for(int i=0;i<16&&ManualStoryDialogueActive;i++){
                forestSubtitles.CompleteCurrentLine();xrSimA=true;yield return null;yield return null;
                xrSimA=false;yield return null;yield return null;
            }
            Check(journeyStage==JourneyStage.Boar&&!ManualStoryDialogueActive,"Controller A completes invitation and unlocks play at location "+shot);
            player.position=new Vector3(-6,0,0);start=Feet;
            yield return new WaitForSeconds(.3f);xrSimStick=Vector2.right;yield return new WaitForSeconds(.3f);xrSimStick=Vector2.zero;
            Check(Vector3.Distance(Feet,start)>.2f,"Player movement resumes after MR threshold event at location "+shot);
        }
        xrSimStick=Vector2.zero;xrSimA=xrSimX=xrSimTrigger=false;
        yield return VerifySandbox(Check);
        yield return VerifyStoryInteractionPermissions(Check);
        xrHelpOpen=true;xrModeHold=xrRestartHold=0;
        xrSimX=true;yield return new WaitForSecondsRealtime(1.2f);xrSimX=false;
        ProcessXRInput(Vector2.zero,false,false,false,false,false,false);
        Check(StoryModeEnabled&&fatherIntroActive&&storyHome.gameObject.activeSelf,"MR hold X switches from sandbox to a fresh story");
        // The desktop shot still waits for its own camera and then completes.
        XRTrackingControlsCamera=false;RestartRun();fatherIntroActive=false;ResetForestSubtitles();
        player.position=ForestSpawn;storyFatherRoot.position=Feet+Vector3.left;
        cam.orthographic=true;cam.orthographicSize=6.6f;cam.transform.position=ForestSpawn+CameraOffset;
        cam.transform.rotation=Quaternion.LookRotation(-CameraOffset,Vector3.up);
        while(Balance>StoryCaptureBalanceThreshold)RegisterDamage();yield return null;yield return null;
        float desktopWait=0;while(journeyStage==JourneyStage.BoarPass&&desktopWait<12){desktopWait+=Time.deltaTime;yield return null;}
        Check(boarPassComplete,"Desktop camera-framed boar pass still completes");
        report.AppendLine("Editor simulated placement/controller checks only; actual headset behaviour is verified separately.");
        File.WriteAllText("Verification/pico-integration/checks.txt",report.ToString());
        UnityEditor.EditorApplication.Exit(passed?0:1);
    }
}}
#endif
