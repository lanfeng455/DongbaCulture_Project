using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Ivan.Puzzle;
namespace DongbaPrototype {public partial class ObliqueForest {
    public IEnumerator CheckLanguageSwitching(){
        const string folder="Verification/language";Directory.CreateDirectory(folder);
        string report=folder+"/checks.txt";File.WriteAllText(report,"");bool passed=true;
        System.Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(report,name+"="+ok+"\n");};
        bool hadPreference=PlayerPrefs.HasKey(DongbaLanguage.PreferenceKey);int previousPreference=PlayerPrefs.GetInt(DongbaLanguage.PreferenceKey,0);var previousLocale=DongbaLanguage.Current;
        var storyLines=FatherIntroLines.Concat(HuntInvitationLines).Concat(HomewardLines).Concat(MissingKnifeWithoutPauseLines)
            .Concat(ShuQuestionLines).Concat(ShuAccusationLines).Concat(RealizationLines).Concat(RestorationArrivalLines)
            .Concat(new[]{WaitingNarration,BoarAidLine,"Gaolequ: You're back.","Gaolequ: I'll come back tomorrow."});
        check("Every forest dialogue has both languages",storyLines.All(line=>{
            SplitStoryLine(line,out var speaker,out var content);
            return DongbaLanguage.Entries.Any(e=>e.en==content&&!string.IsNullOrWhiteSpace(e.zh));
        }));
        yield return new WaitForSeconds(1);DongbaLanguage.Set(DongbaLocale.English,false);SetStoryModeEnabled(true);RestartRun();yield return new WaitForEndOfFrame();
        check("English opening rendered",forestSubtitles.SubtitleText.text.Contains("Bring your axe, Gaolequ"));
        DongbaLanguageCapture.Write(cam,folder+"/opening-en.png");
        var position=Feet;int balance=Balance;yield return PressStoryKey(Key.F8);
        check("F8 changes current opening to Chinese",DongbaLanguage.Chinese&&forestSubtitles.SubtitleText.text.Contains("把斧子带上，高勒趣"));
        check("Switch does not advance or attack",fatherIntroLine==0&&fatherIntroActive&&attackAge<0&&Balance==balance&&Vector3.Distance(position,Feet)<.001f);
        Canvas.ForceUpdateCanvases();File.AppendAllText(report,"Chinese HUD values: "+hudHealth.text+" / "+forestSubtitleAdvance.text+" / "+uiMode.text+"\n");
        check("Chinese HUD and continue hint",hudHealth.text=="生命"&&forestSubtitleAdvance.text=="回车   继续"&&uiMode.text=="故事模式");
        DongbaLanguageCapture.Write(cam,folder+"/opening-zh.png");
        yield return PressStoryKey(Key.Enter);check("Enter advances exactly once in Chinese",fatherIntroLine==1&&forestSubtitles.SubtitleText.text.Contains("今天要走多远"));
        yield return PressStoryKey(Key.F8);check("Back to English keeps the same line",fatherIntroLine==1&&forestSubtitles.SubtitleText.text.Contains("How far are we going today?"));
        // Exercise the real clickable rectangle, not a direct call to Set.
        var mouse=Mouse.current;if(mouse==null)mouse=InputSystem.AddDevice<Mouse>();
        Vector2 click=RectTransformUtility.WorldToScreenPoint(null,DongbaLanguageSwitch.Instance.HitRect.position+new Vector3(-70,-15));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=click}.WithButton(MouseButton.Left));yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=click});yield return null;yield return new WaitForEndOfFrame();
        check("Hidden opening control ignores mouse clicks",!DongbaLanguageSwitch.Instance.SwitchCanvas.enabled&&!DongbaLanguage.Chinese&&fatherIntroLine==1);
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(.45f);noticeTime=0;uiHelpOpen=true;yield return null;yield return new WaitForEndOfFrame();
        check("Language control returns after opening",DongbaLanguageSwitch.Instance.SwitchCanvas.enabled);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=click}.WithButton(MouseButton.Left));yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=click});yield return null;yield return new WaitForEndOfFrame();
        check("Visible mouse control changes language",DongbaLanguage.Chinese);
        check("Preference saved",PlayerPrefs.GetInt(DongbaLanguage.PreferenceKey,-1)==(int)DongbaLocale.Chinese);
        check("Chinese controls readable",uiHelpPanel.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("照料 / 喂养")));
        DongbaLanguageCapture.Write(cam,folder+"/controls-zh.png");uiHelpOpen=false;
        var tree=plants.Find(t=>t.tree);player.position=tree.home+Vector3.right;FollowPlayerCamera(100);yield return new WaitForEndOfFrame();
        check("Interaction label translated",hudPromptTitle.text=="树木"&&hudPrompt.text.Contains("照料"));
        DongbaLanguageCapture.Write(cam,folder+"/interaction-zh.png");
        yield return PressStoryKey(Key.R);yield return new WaitForEndOfFrame();check("R reset retains language",DongbaLanguage.Chinese&&fatherIntroLine==0&&forestSubtitles.SubtitleText.text.Contains("把斧子带上"));
        SetStoryModeEnabled(false);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();File.AppendAllText(report,"Sandbox label: "+uiMode.text+"\n");check("Sandbox keeps Chinese",DongbaLanguage.Chinese&&uiMode.text=="沙盒模式");
        // Translate the active shared typewriter without replaying its sequence or losing letters.
        const string line="高勒趣，把“白石”砌在高处，组成<color=#FFDC88>神位</color>。";
        forestSubtitles.SetCharactersPerSecond(1);forestSubtitles.PlayLine("署",line);yield return new WaitForSeconds(.15f);yield return PressStoryKey(Key.F8);yield return new WaitForSeconds(.15f);
        check("Shared puzzle line switches during typing",forestSubtitles.SubtitleText.text.Contains("white stone")&&forestSubtitles.SubtitleText.text.Contains("Shu"));
        check("Translated longer line is not truncated",forestSubtitles.SubtitleText.maxVisibleCharacters>=forestSubtitles.SubtitleText.textInfo.characterCount);
        check("Rich text highlight retained",forestSubtitles.SubtitleText.text.Contains("<color=#FFDC88>spirit seat</color>"));
        DongbaLanguageCapture.Write(cam,folder+"/puzzle-subtitle-en.png");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return new WaitForEndOfFrame();DongbaLanguageCapture.Write(cam,folder+"/puzzle-subtitle-zh.png");
        check("Stage commands never translated",DongbaLanguage.Text("@等待1秒")=="@等待1秒"&&DongbaLanguage.Text("@移动到-5.31")=="@移动到-5.31");
        var allChinese=string.Concat(DongbaLanguage.Entries.Select(e=>e.zh));var missing=new System.Collections.Generic.List<char>();
        foreach(char c in allChinese.Distinct())if(c>=0x3400&&c<=0x9fff&&!DongbaLanguage.ChineseSubtitleFont.HasCharacter(c,true,true))missing.Add(c);
        check("Chinese font covers catalogue",missing.Count==0);if(missing.Count>0)File.AppendAllText(report,"Missing: "+new string(missing.ToArray())+"\n");
        // Confirm the global preference survives the real additive puzzle handoff and return.
        forestSubtitles.HideDialogue();forestSubtitles.SetCharactersPerSecond(ForestSubtitleCharactersPerSecond);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;ResetForestSubtitles();
        PrepareJourneyDestination();journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(30,0,0);FollowPlayerCamera(100);
        var keyboard=Keyboard.current;float age=0;
        bool portalHidden=true;int portalFrames=0;
        while(!reelPortalHost.InLevel&&age<40){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));age+=Time.unscaledDeltaTime;yield return null;if(reelPortalHost.IsBusy&&!reelPortalHost.InLevel){portalHidden&=!DongbaLanguageSwitch.Instance.SwitchCanvas.enabled;portalFrames++;}}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        check("Reel entry hides language control throughout transition",portalHidden&&portalFrames>0);
        check("Chinese selection survives real puzzle entry",reelPortalHost.InLevel&&DongbaLanguage.Chinese&&DongbaLanguageSwitch.Instance!=null);
        check("Puzzle play restores language control",DongbaLanguageSwitch.Instance.SwitchCanvas.enabled);
        if(reelPortalHost.InLevel){
            var puzzle=DialogueDisplay.Instance;puzzle.PlayLine("高勒趣","高处为“白”，低处为“黑”。");puzzle.CompleteCurrentLine();yield return new WaitForEndOfFrame();
            check("Actual puzzle uses Chinese",puzzle.SubtitleText.text.Contains("高处为“白”"));
            DongbaLanguageCapture.Write(Camera.main,folder+"/actual-puzzle-zh.png");yield return PressStoryKey(Key.F8);
            check("F8 works inside puzzle",!DongbaLanguage.Chinese&&puzzle.SubtitleText.text.Contains("High ground"));
            DongbaLanguageCapture.Write(Camera.main,folder+"/actual-puzzle-en.png");
            var director=FindFirstObjectByType<PuzzleDirectorBase>();if(director!=null)typeof(PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);
            age=0;portalHidden=true;portalFrames=0;
            while((reelPortalHost.InLevel||reelPortalHost.IsBusy)&&age<18){age+=Time.unscaledDeltaTime;yield return null;if(reelPortalHost.PresentationTransitionActive){portalHidden&=!DongbaLanguageSwitch.Instance.SwitchCanvas.enabled;portalFrames++;}}
            check("Reel return hides language control throughout transition",portalHidden&&portalFrames>0);
            check("Language retained on return to reel",!reelPortalHost.InLevel&&!reelPortalHost.IsBusy&&!DongbaLanguage.Chinese&&gameObject.activeInHierarchy);
            check("Reel walking restores language control",DongbaLanguageSwitch.Instance.SwitchCanvas.enabled);
        }
        DongbaLanguage.Set(previousLocale,false);if(hadPreference)PlayerPrefs.SetInt(DongbaLanguage.PreferenceKey,previousPreference);else PlayerPrefs.DeleteKey(DongbaLanguage.PreferenceKey);PlayerPrefs.Save();
        File.AppendAllText(report,"Puzzle completion above is a handoff simulation, not a puzzle-solving test.\n");Application.Quit(passed?0:1);
    }
}}
