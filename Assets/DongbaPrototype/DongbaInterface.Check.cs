using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DongbaPrototype {
public sealed partial class DongbaInterface {
    internal static readonly string CheckFolder=Array.IndexOf(Environment.GetCommandLineArgs(),"--sandbox-test")>=0?"Verification/sandbox":"Verification/title-ink-20261121";
    internal readonly List<string> checks=new List<string>();internal int checkFailures;
    int originalMute,originalLocale;float originalVolume;Keyboard checkKeyboard;Mouse checkMouse;
    IEnumerator Start(){
        bool helpPreview=Array.IndexOf(Environment.GetCommandLineArgs(),"--help-layout-preview")>=0;
        bool sandbox=Array.IndexOf(Environment.GetCommandLineArgs(),"--sandbox-test")>=0;
        if(!sandbox&&!helpPreview&&Array.IndexOf(Environment.GetCommandLineArgs(),"--interface-check")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"--interface-title-test")<0)yield break;
        Application.runInBackground=true;
        // Hidden test launches can lose native input focus during screenshots/resize.
        // Isolate injected events from the user's physical keyboard and OS focus resets.
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        checkKeyboard=InputSystem.AddDevice<Keyboard>("InterfaceCheckKeyboard");checkMouse=InputSystem.AddDevice<Mouse>("InterfaceCheckMouse");
        originalMute=PlayerPrefs.GetInt(MuteKey,0);originalVolume=PlayerPrefs.GetFloat(VolumeKey,1);originalLocale=PlayerPrefs.GetInt(DongbaLanguage.PreferenceKey,0);
        Directory.CreateDirectory(CheckFolder);yield return new WaitForSecondsRealtime(2);
        // A hidden launch can leave the swap chain occluded until its first resize.
        // Resize the actual player, then capture the resulting backbuffer (no mock render).
        Screen.SetResolution(1598,898,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.2f);
        Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.3f);
        bool title=Array.IndexOf(Environment.GetCommandLineArgs(),"--interface-title-test")>=0;
        yield return RunChecksSafely(sandbox?CheckSandboxEntry():helpPreview?CaptureHelpLayout():title?CheckTitle():CheckForestWhenReady());
    }
    IEnumerator CaptureHelpLayout(){
        DongbaLanguage.Set(DongbaLocale.English,false);
        yield return KeyPress(Key.H);
        Check("H opens current Help menu",page==Page.Help);
        yield return Snap("help-language-en");CheckLayout("English Help",menuPage);
        yield return KeyPress(Key.F8);
        Check("F8 switches language while Help remains open",DongbaLanguage.Chinese&&page==Page.Help);
        yield return Snap("help-language-zh");CheckLayout("Chinese Help",menuPage);
        Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.4f);
        yield return Snap("help-language-zh-720");CheckLayout("Chinese Help 720p",menuPage);
        yield return KeyPress(Key.F8);yield return Snap("help-language-en-720");CheckLayout("English Help 720p",menuPage);
    }
    IEnumerator RunChecksSafely(IEnumerator routine){
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0){
            object current=null;bool next=false;Exception error=null;
            try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}
            if(error!=null){Check("Runtime test exception: "+error,false);break;}
            if(!next){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
        }
        File.WriteAllLines(CheckFolder+(Array.IndexOf(Environment.GetCommandLineArgs(),"--interface-title-test")>=0?"/title-checks.txt":"/checks.txt"),checks);
        PlayerPrefs.SetInt(MuteKey,originalMute);PlayerPrefs.SetFloat(VolumeKey,originalVolume);PlayerPrefs.SetInt(DongbaLanguage.PreferenceKey,originalLocale);PlayerPrefs.Save();
        Close(false);Time.timeScale=1;AudioListener.pause=false;Application.Quit(checkFailures==0?0:1);
    }
    internal void Check(string label,bool ok){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)checkFailures++;Debug.Log(checks[checks.Count-1]);File.WriteAllLines(CheckFolder+"/progress.txt",checks);}
    internal IEnumerator Snap(string label){
        yield return new WaitForSecondsRealtime(.25f);
        yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(CheckFolder+"/"+label+".png",image.EncodeToPNG());Destroy(image);
    }
    internal IEnumerator KeyPress(Key key){
        var keyboard=checkKeyboard;keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
    }
    internal IEnumerator ClickButton(string name){
        Button button=null;foreach(var candidate in FindObjectsByType<Button>(FindObjectsSortMode.None))if(candidate.name==name&&candidate.isActiveAndEnabled){button=candidate;break;}
        Check("Clickable control exists: "+name,button!=null);if(button==null)yield break;
        var rect=(RectTransform)button.transform;var location=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var mouse=checkMouse;mouse.MakeCurrent();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location});yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location}.WithButton(MouseButton.Left,true));yield return null;yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location});yield return null;yield return null;
    }
    internal IEnumerator ClickAt(Vector2 location){
        var mouse=checkMouse;mouse.MakeCurrent();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location});yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location}.WithButton(MouseButton.Left,true));yield return null;yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=location});yield return null;yield return null;
    }
    internal void CheckLayout(string label,Transform root){
        Canvas.ForceUpdateCanvases();bool fits=true;var details=new List<string>();
        foreach(var text in root.GetComponentsInChildren<Text>()){
            if(!text.gameObject.activeInHierarchy||string.IsNullOrEmpty(text.text))continue;
            if(text.preferredHeight>text.rectTransform.rect.height+2){fits=false;details.Add(text.name+" "+text.preferredHeight+">"+text.rectTransform.rect.height);}
        }
        Check(label+" text fits"+(details.Count>0?": "+string.Join("; ",details):""),fits);
    }
    IEnumerator CheckTitle(){
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;
        Check("Original title retained with four real actions",onTitle&&titleMenu!=null&&titleTransition!=null&&titleMenu.GetComponentsInChildren<Button>().Length==4);
        bool originalInk=false;
        foreach(var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))if(volume.gameObject.scene==SceneManager.GetActiveScene()&&volume.sharedProfile!=null&&volume.sharedProfile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vignette))originalInk|=vignette.active&&vignette.intensity.value>.4f;
        Check("Original title ink composition retained",originalInk&&titleMenu.transform.Find("Front cover leaf")==null);
        CheckLayout("English title",titleMenu.transform);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return Snap("01b-title-zh");CheckLayout("Chinese title",titleMenu.transform);
        Screen.SetResolution(1024,768,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.4f);CheckLayout("Title 1024x768",titleMenu.transform);yield return Snap("title-1024");
        Screen.SetResolution(1600,900,FullScreenMode.Windowed);DongbaLanguage.Set(DongbaLocale.English,false);yield return new WaitForSecondsRealtime(.4f);
        yield return Snap("01-title-en");yield return ClickButton("Settings");Check("Title settings opens",page==Page.Settings);
        yield return ClickButton("Back");Check("Settings returns to title",page==Page.None&&titleMenu.activeSelf);
        yield return ClickButton("Begin story");Check("Original ink bloom transition starts",FindFirstObjectByType<InkBloomOverlay>()!=null&&loading&&!titleMenu.activeSelf);yield return Snap("01c-ink-transition");float limit=Time.realtimeSinceStartup+35;
        while(SceneManager.GetActiveScene().name!="ObliqueForest"&&Time.realtimeSinceStartup<limit)yield return null;
        yield return new WaitForSecondsRealtime(4);Check("Title begins the story, not sandbox",forest!=null&&forest.StoryModeEnabled);
        yield return KeyPress(Key.Escape);Check("Pause reachable from opening",page==Page.Pause);
        yield return ClickButton("Return to title");Check("Return requires confirmation",page==Page.ReturnTitle);
        yield return ClickButton("Confirm");limit=Time.realtimeSinceStartup+25;
        while(SceneManager.GetActiveScene().name!="Title"&&Time.realtimeSinceStartup<limit)yield return null;
        yield return new WaitForSecondsRealtime(2);Check("Return loads original title and releases pause",onTitle&&page==Page.None&&Time.timeScale==1&&!AudioListener.pause);
        Check("Exactly one active UI input system after scene return",FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1);
        yield return ClickButton("Begin story");limit=Time.realtimeSinceStartup+35;while(SceneManager.GetActiveScene().name!="ObliqueForest"&&Time.realtimeSinceStartup<limit)yield return null;
        yield return new WaitForSecondsRealtime(3);Check("A second story starts after return",forest!=null&&forest.StoryModeEnabled);
        yield return KeyPress(Key.Escape);yield return ClickButton("Quit game");Check("Quit explains consequence and waits for confirmation",page==Page.Quit);
        yield return ClickButton("Cancel");Check("Cancel quit returns to pause without losing the run",page==Page.Pause&&forest!=null&&forest.StoryModeEnabled);
    }
    IEnumerator CheckForestWhenReady(){
        float limit=Time.realtimeSinceStartup+25;while((forest==null||!forest.DialogueGraphPreviewReady)&&Time.realtimeSinceStartup<limit){forest=FindFirstObjectByType<ObliqueForest>();yield return null;}
        Check("Forest initialized",forest!=null&&forest.DialogueGraphPreviewReady);if(forest!=null)yield return forest.CheckInterface(this);
    }
}
public partial class ObliqueForest {
    internal IEnumerator CheckInterface(DongbaInterface ui){
        DongbaLanguage.Set(DongbaLocale.English,false);SetStoryModeEnabled(true);RestartRun();yield return new WaitForSecondsRealtime(.5f);
        ui.Check("Opening hides objective, interactions and tutorial",fatherIntroActive&&!uiObjectivePanel.activeSelf&&!promptPanel.activeSelf&&!uiCoreHints.activeSelf);
        ui.Check("Manual dialogue shows Continue",forestSubtitles.ManualContinueVisible);
        ui.Check("Father subtitle uses the actual father glyph",forestSubtitles.PaperSpeakerGlyph==StoryFatherGlyph);
        yield return ui.Snap("02-forest-dialogue-en");yield return new WaitForSecondsRealtime(1);
        ui.Check("Tutorial not consumed during dialogue",!taughtMove&&!taughtJump&&!taughtAxe&&!taughtCare);
        int line=fatherIntroLine;yield return ui.KeyPress(Key.Enter);ui.Check("Enter advances one line without attack",fatherIntroLine==line+1&&attackAge<0);
        while(fatherIntroActive)yield return ui.KeyPress(Key.Enter);yield return new WaitForSecondsRealtime(.1f);
        ui.Check("Exploration restores objective and first action",uiObjectivePanel.activeSelf&&uiCoreHints.activeSelf);
        yield return ui.KeyPress(Key.Enter);ui.Check("Enter no longer attacks in exploration",attackAge<0);
        yield return ui.KeyPress(Key.J);ui.Check("J still attacks",attackAge>=0);yield return new WaitForSecondsRealtime(.7f);
        tonePreviewFreezeAnimals=true;fatherHuntTarget=null;fatherNextSwing=float.PositiveInfinity;
        var tree=plants.Find(p=>p.tree&&p.health>0);player.position=tree.home+new Vector3(1.2f,0,-.45f);interactionTarget=null;FollowPlayerCamera(100);yield return null;yield return null;
        ui.Check("Card and ring identify the same nearby tree",interactionTarget==tree&&promptPanel.activeSelf&&selectionRing.gameObject.activeSelf&&Vector3.Distance(selectionRing.position,tree.home)<.01f);
        Care();yield return null;ui.Check("Care success stays in object card",tree.cultivated&&interactionResult.text=="Tended"&&noticeTime<=0);
        yield return ui.Snap("03-tended-tree");Care();yield return null;ui.Check("Repeated care gives real reason",hudPrompt.text.Contains("Already tended"));
        ui.CheckLayout("English interaction and chapter",forestCanvas.transform);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;yield return ui.Snap("03b-tended-tree-zh");ui.CheckLayout("Chinese interaction and chapter",forestCanvas.transform);DongbaLanguage.Set(DongbaLocale.English,false);yield return null;
        chapterTab.Reveal();float expandedHeight=((RectTransform)uiObjectivePanel.transform).rect.height;
        yield return new WaitForSecondsRealtime(6.3f);ui.Check("Chapter note folds without losing objective",!chapterTab.Expanded&&journeyObjective.text=="Hunt with Father"&&((RectTransform)uiObjectivePanel.transform).rect.height<expandedHeight-20);
        yield return ui.Snap("03c-chapter-folded");yield return ui.KeyPress(Key.Tab);yield return new WaitForSecondsRealtime(.3f);ui.Check("Tab expands the chapter note",chapterTab.Expanded);yield return ui.Snap("03d-chapter-open");
        yield return ui.KeyPress(Key.Tab);yield return new WaitForSecondsRealtime(.3f);ui.Check("Tab folds the chapter note",!chapterTab.Expanded);
        ui.Open(DongbaInterface.Page.Pause);yield return null;ui.Close();yield return new WaitForSecondsRealtime(.4f);ui.Check("Returning from a hidden HUD reveals the chapter note",chapterTab.Expanded);
        while(tree.health>0)ApplyEcologyHit(tree);yield return new WaitForSecondsRealtime(.7f);
        Care();yield return null;ui.Check("Replant gives local result",tree.health>0&&interactionResult.text=="Replanted");
        player.position=ForestSpawn;interactionTarget=null;FollowPlayerCamera(100);yield return null;ui.Check("Walking away clears old target result",interactionTarget!=tree&&(!promptPanel.activeSelf||interactionResult.text!="Replanted"));
        var animal=animals[0];animal.root.position=Feet+Vector3.right;animal.age=0;animal.careReady=0;animal.protectedUntil=clock+99;interactionTarget=null;yield return null;
        Care();yield return null;ui.Check("Feeding state and success reflect actual growth",animal.age==1&&hudPrompt.text.Contains("Eating")&&interactionResult.text=="Fed — growing");yield return ui.Snap("04-feeding");
        float inkWait=interactionWait.fillAmount;yield return new WaitForSecondsRealtime(.3f);ui.Check("Feeding ink strip follows real cooldown",interactionWait.transform.parent.gameObject.activeSelf&&interactionWait.fillAmount<inkWait&&Mathf.Abs(interactionWait.fillAmount-(animal.careReady-clock)/3f)<.02f);
        int beforeAge=animal.age;Care();ui.Check("Waiting still rejects repeated feeding",animal.age==beforeAge);yield return new WaitForSecondsRealtime(1.3f);ui.Check("Wait indicator clears when feeding is ready",!interactionWait.transform.parent.gameObject.activeSelf);
        animal.root.gameObject.SetActive(false);yield return null;yield return null;ui.Check("Hidden animals cannot retain the interaction marker",interactionTarget!=animal);animal.root.gameObject.SetActive(true);yield return null;
        yield return ui.KeyPress(Key.Escape);ui.Check("Esc pauses, does not exit",ui.CurrentPage==DongbaInterface.Page.Pause&&Time.timeScale==0&&AudioListener.pause);
        float before=clock;var feet=Feet;int care=careCount;yield return ui.KeyPress(Key.J);yield return ui.KeyPress(Key.E);yield return ui.KeyPress(Key.D);
        ui.Check("Pause blocks simulation, movement, care and axe",clock==before&&Feet==feet&&care==careCount&&attackAge<0);
        yield return ui.Snap("05-paused-en");yield return ui.ClickButton("Settings");ui.Check("Pause opens settings by pointer",ui.CurrentPage==DongbaInterface.Page.Settings);
        var slider=UnityEngine.Object.FindFirstObjectByType<Slider>();if(slider!=null){var area=(RectTransform)slider.handleRect.parent;yield return ui.ClickAt(RectTransformUtility.WorldToScreenPoint(null,area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin,area.rect.xMax,.35f),area.rect.center.y,0))));}
        ui.Check("Volume control affects listener",Mathf.Abs(AudioListener.volume-.35f)<.01f);
        yield return ui.ClickButton("Mute");ui.Check("Mute works",AudioListener.volume==0);yield return ui.ClickButton("Mute");ui.Check("Unmute restores chosen value",Mathf.Abs(AudioListener.volume-.35f)<.01f);
        yield return ui.ClickButton("Language / 语言");ui.Check("Settings switches to Chinese",DongbaLanguage.Chinese);ui.Check("Language switch preserves button focus",UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name=="Language / 语言");yield return null;yield return ui.Snap("06-settings-zh");ui.CheckLayout("Chinese settings",ui.transform);
        yield return ui.ClickButton("Back");yield return ui.ClickButton("Controls");ui.Check("Help has return path",ui.CurrentPage==DongbaInterface.Page.Help);yield return ui.Snap("07-help-zh");ui.CheckLayout("Chinese help",ui.transform);
        yield return ui.KeyPress(Key.H);ui.Check("H returns help to pause",ui.CurrentPage==DongbaInterface.Page.Pause);
        yield return ui.ClickButton("Resume");yield return null;ui.Check("Resume restores gameplay time",Time.timeScale==1&&!AudioListener.pause);
        yield return ui.KeyPress(Key.R);ui.Check("R asks before resetting",ui.CurrentPage==DongbaInterface.Page.Restart&&careCount==care);
        yield return ui.Snap("08-restart-confirm-zh");yield return ui.KeyPress(Key.Enter);ui.Check("Confirmation defaults to safe Cancel",ui.CurrentPage==DongbaInterface.Page.None&&careCount==care);
        yield return ui.KeyPress(Key.R);yield return ui.ClickButton("Confirm");yield return new WaitForSecondsRealtime(.5f);ui.Check("Confirmed restart resets whole run",Balance==0&&fatherIntroActive&&Feet==ForestSpawn&&animals.Count==initialAnimalCount);
        yield return ui.Snap("09-forest-dialogue-zh");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080)}){
            Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.35f);
            ui.Open(DongbaInterface.Page.Help);yield return null;ui.CheckLayout("Help "+Screen.width+"x"+Screen.height,ui.transform);yield return ui.Snap("help-"+Screen.width);ui.Close();yield return null;
        }
        Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.35f);
        DongbaLanguage.Set(DongbaLocale.English,false);fatherIntroActive=false;ResetForestSubtitles();
        BeginManualStoryDialogue(new[]{"Father: Bring your axe, Gaolequ. We'll go into the forest together. Stay close when we reach the river, and keep your eyes on the trees ahead. There is a long way to walk before we return to the hearth."});
        yield return null;yield return null;yield return ui.Snap("10-long-dialogue");
        var subtitle=forestSubtitles.SubtitleText;ui.Check("Long dialogue fits with speaker and prompt",subtitle.preferredHeight<=subtitle.rectTransform.rect.height+2);
        foreach(var c in forestSubtitles.GetComponentsInChildren<Canvas>())ui.Check("Subtitle is above sprite depth",c.sortingOrder>=30010);
        ResetForestSubtitles();notice="Father: Stay close.";noticeTime=2;yield return null;yield return null;
        ui.Check("Automatic aside has no Continue and hides unrelated HUD",!forestSubtitles.ManualContinueVisible&&!promptPanel.activeSelf&&!uiCoreHints.activeSelf);
        noticeTime=0;notice="";yield return new WaitForSecondsRealtime(.4f);
        ui.Check("No dialogue leaves no empty subtitle panel",!forestSubtitles.SubtitlePanel.gameObject.activeInHierarchy);
        ResetForestSubtitles();journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(34,0,0);FollowPlayerCamera(100);storyFatherRoot.gameObject.SetActive(false);
        yield return null;yield return null;BeginManualStoryDialogue(new[]{"Gaolequ: Father?"});yield return new WaitForSecondsRealtime(.4f);yield return ui.Snap("11-reel-dialogue");ui.Check("Reel subtitle moves above the paper actor",forestSubtitles.SubtitlePanel.anchorMin.y==1);
        ResetForestSubtitles();ui.Close();PrepareRestorationPreview();fatherIntroActive=false;ResetForestSubtitles();journeyStage=JourneyStage.RestorationPlay;storyCurtain=0;storyCurtainImage.color=Color.clear;narrativeBeat=NarrativeBeat.None;restorationHomeHold=false;yield return new WaitForSecondsRealtime(.6f);
        ui.Check("Restoration objective replaces hunt objective",QuietStoryObjective().Contains("clearing")||QuietStoryObjective().Contains("recover"));yield return ui.Snap("12-restoration");
        journeyStage=JourneyStage.RestorationComplete;narrativeBeat=NarrativeBeat.RenewedForest;narrativeBeatAge=0;yield return null;ui.Check("Ending waits for final acting",ui.CurrentPage==DongbaInterface.Page.None);
        narrativeBeat=NarrativeBeat.None;yield return null;yield return null;ui.Check("Ending offers exploration after dialogue",ui.CurrentPage==DongbaInterface.Page.Ending);yield return ui.Snap("13-ending");
        yield return ui.ClickButton("Keep exploring");yield return null;yield return null;ui.Check("Continued exploration clears ending and objective",ui.CurrentPage==DongbaInterface.Page.None&&!uiObjectivePanel.activeSelf&&uiHelpShortcut.activeSelf);
        RestartRun();fatherIntroActive=false;SpawnStoryBoar();journeyStage=JourneyStage.Boar;boarStun=100;player.position=BoarClearing+Vector3.left*3;FollowPlayerCamera(100);yield return null;yield return null;
        ui.Check("Visible boar shows health marker",uiBoarPlate.activeSelf);storyBoar.health=2;yield return new WaitForSecondsRealtime(.5f);ui.Check("Boar low health reflected",boarHealthFill.rectTransform.anchorMax.x<.35f);yield return ui.Snap("14-boar-low-health");
        player.position=ForestSpawn;FollowPlayerCamera(100);yield return null;ui.Check("Offscreen boar label hidden",!uiBoarPlate.activeSelf);
        storyBoar.health=0;DrawBoarTargetMarker();ui.Check("Dead boar label hidden",!uiBoarPlate.activeSelf);
        RestartRun();fatherIntroActive=false;ResetForestSubtitles();journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(36,0,0);PrepareJourneyDestination();FollowPlayerCamera(100);
        var keyboard=Keyboard.current;float until=Time.realtimeSinceStartup+45;
        while(!reelPortalHost.InLevel&&Time.realtimeSinceStartup<until){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return null;}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(1);
        ui.Check("Real reel connects to puzzle",reelPortalHost.InLevel);
        if(reelPortalHost.InLevel){
            yield return ui.KeyPress(Key.Escape);var actor=FindFirstObjectByType<Ivan.Diorama.DioramaPlayerController>();var p=actor.transform.position;
            yield return ui.KeyPress(Key.D);yield return ui.KeyPress(Key.E);ui.Check("Puzzle pauses movement and interaction",ui.CurrentPage==DongbaInterface.Page.Pause&&Vector3.Distance(p,actor.transform.position)<.001f);
            yield return ui.Snap("15-puzzle-paused");yield return ui.ClickButton("Controls");ui.CheckLayout("Puzzle help",ui.transform);yield return ui.Snap("16-puzzle-help");
            yield return ui.KeyPress(Key.Escape);yield return ui.ClickButton("Restart story");yield return ui.ClickButton("Confirm");until=Time.realtimeSinceStartup+20;
            while(ui.CurrentPage!=DongbaInterface.Page.None&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(.5f);ui.Check("Puzzle restart returns to hearth and unloads level",fatherIntroActive&&Balance==0&&!SceneManager.GetSceneByName("Beat_2_Cleansing_Impurity").isLoaded);
        }
    }
}
}
