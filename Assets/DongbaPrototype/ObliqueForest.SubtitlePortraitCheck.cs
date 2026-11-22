using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Ivan.Scroll;

namespace DongbaPrototype {public partial class ObliqueForest {
    void BeginSubtitlePortraitCheck(){var runner=new GameObject("Subtitle portrait verification").AddComponent<SubtitlePortraitCheckRunner>();DontDestroyOnLoad(runner.gameObject);runner.StartCoroutine(CheckSubtitlePortraits());}
    const string PortraitCheckFolder="Verification/subtitle-portraits-20261123";
    bool portraitChecksPassed=true;
    void PortraitCheck(string name,bool ok){portraitChecksPassed&=ok;File.AppendAllText(PortraitCheckFolder+"/checks.txt",(ok?"PASS ":"FAIL ")+name+"\n");}
    IEnumerator PortraitShot(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(PortraitCheckFolder+"/"+name+".png",t.EncodeToPNG());Destroy(t);}
    IEnumerator CheckSubtitlePortraits(){
        Directory.CreateDirectory(PortraitCheckFolder);File.WriteAllText(PortraitCheckFolder+"/checks.txt","");
        var previous=DongbaLanguage.Current;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        yield return new WaitForSeconds(1);
        Screen.SetResolution(1598,898,false);yield return new WaitForSeconds(.2f);Screen.SetResolution(1600,900,false);yield return new WaitForSeconds(.3f);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();
        BeginManualStoryDialogue(new[]{FatherIntroLines[0],FatherIntroLines[1]});yield return new WaitForSeconds(.4f);
        PortraitCheck("Forest father portrait preserved",forestSubtitles.PaperSpeakerGlyph!=null);
        AdvanceManualStoryDialogue();yield return new WaitForSeconds(.3f);var hero=forestSubtitles.PaperSpeakerGlyph;
        PortraitCheck("Forest Gaolequ portrait preserved",hero!=null);yield return PortraitShot("01-forest-hero");
        ResetForestSubtitles();PrepareJourneyDestination();journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(30,0,0);FollowPlayerCamera(100);
        float elapsed=0;
        while(!reelPortalHost.InLevel&&elapsed<40){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));elapsed+=Time.unscaledDeltaTime;yield return null;}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        PortraitCheck("Actual reel-to-puzzle transition completed",reelPortalHost.InLevel);
        if(!reelPortalHost.InLevel){DongbaLanguage.Set(previous,false);Application.Quit(1);yield break;}
        var puzzle=DialogueDisplay.Instance;
        foreach(var t in FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None))t.enabled=false;
        foreach(var s in FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None))s.enabled=false;
        foreach(var d in FindObjectsByType<ScrollPlayDirector>(FindObjectsSortMode.None))d.enabled=false;
        var controller=FindFirstObjectByType<Ivan.Diorama.DioramaPlayerController>();if(controller!=null){controller.ClearAutoMoveInput();controller.SetStageMovementLock(true);}
        puzzle.PlayFromTo(0,0,false,0);yield return null;puzzle.CompleteCurrentLine();yield return new WaitForSeconds(.3f);
        PortraitCheck("Gao from actual level library resolves to Gaolequ portrait",puzzle.PaperSpeakerGlyph==hero);yield return PortraitShot("02-puzzle-gao");
        puzzle.PlayFromTo(7,7,false,0);yield return null;puzzle.CompleteCurrentLine();yield return new WaitForSeconds(.3f);
        var fox=puzzle.PaperSpeakerGlyph;PortraitCheck("Fox uses original scene sprite",fox!=null&&fox.name=="狐狸");
        Image portrait=null;foreach(var im in puzzle.SubtitlePanel.GetComponentsInChildren<Image>(true))if(im.name=="Existing speaker glyph")portrait=im;
        PortraitCheck("White source glyph tinted readable brown",portrait!=null&&portrait.color.r<.4f&&portrait.color.a>.99f&&portrait.preserveAspect);
        var corners=new Vector3[4];if(portrait!=null)portrait.rectTransform.GetWorldCorners(corners);
        PortraitCheck("Portrait stays on screen and leaves text clearance",portrait!=null&&corners[0].x>=0&&corners[2].x<Screen.width&&puzzle.SubtitleText.rectTransform.offsetMin.x>=145);
        yield return PortraitShot("03-puzzle-fox");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return new WaitForSeconds(.3f);PortraitCheck("English switch retains Fox portrait",puzzle.PaperSpeakerGlyph==fox);yield return PortraitShot("04-puzzle-fox-en");
        var actor=StageActorRegistry.Find("Fox");if(actor!=null)actor.Hide();
        puzzle.PlayFromTo(0,0,false,0);yield return null;puzzle.PlayFromTo(7,7,false,0);yield return null;
        PortraitCheck("Fox portrait independent of actor being hidden",puzzle.PaperSpeakerGlyph==fox);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);puzzle.PlayFromTo(29,29,false,0);yield return null;puzzle.CompleteCurrentLine();yield return new WaitForSeconds(.3f);
        var snakes=puzzle.PaperSpeakerGlyph;PortraitCheck("Snakes group resolves without StageActor",snakes!=null&&snakes.name=="蛇");yield return PortraitShot("05-puzzle-snakes");
        foreach(string alias in new[]{"Gao","gaolequ"," 高勒趣 "}){puzzle.PlayLine(alias,"…");yield return null;PortraitCheck("Hero alias "+alias,puzzle.PaperSpeakerGlyph==hero);}
        puzzle.PlayLine("狐狸","…");yield return null;PortraitCheck("Chinese Fox alias",puzzle.PaperSpeakerGlyph==fox);
        puzzle.PlayLine("双蛇","…");yield return null;PortraitCheck("Chinese Snakes alias",puzzle.PaperSpeakerGlyph==snakes);
        puzzle.PlayLine("","…");yield return null;PortraitCheck("Narrator clears portrait and indentation",puzzle.PaperSpeakerGlyph==null&&puzzle.SubtitleText.rectTransform.offsetMin.x==30);
        puzzle.PlayLine("UnconfiguredSpeaker","…");yield return null;PortraitCheck("Unknown speaker never inherits previous portrait",puzzle.PaperSpeakerGlyph==null);
        puzzle.HideDialogue();
        var director=FindFirstObjectByType<Ivan.Puzzle.PuzzleDirectorBase>();
        if(director!=null)typeof(Ivan.Puzzle.PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);
        elapsed=0;while((reelPortalHost.InLevel||reelPortalHost.IsBusy)&&elapsed<22){elapsed+=Time.unscaledDeltaTime;yield return null;}
        PortraitCheck("Actual puzzle-to-reel return completed",!reelPortalHost.InLevel&&!reelPortalHost.IsBusy&&gameObject.activeInHierarchy);
        BeginManualStoryDialogue(new[]{FatherIntroLines[1]});yield return new WaitForSeconds(.3f);
        PortraitCheck("Forest display and hero portrait restored after handoff",DialogueDisplay.Instance==forestSubtitles&&forestSubtitles.PaperSpeakerGlyph==hero);
        yield return PortraitShot("06-return-to-reel");
        ResetForestSubtitles();DongbaLanguage.Set(previous,false);
        File.AppendAllText(PortraitCheckFolder+"/checks.txt","Puzzle completion was simulated to test the actual return transition; no puzzle rules changed.\n");
        Application.Quit(portraitChecksPassed?0:1);
    }
}}

namespace DongbaPrototype { public sealed class SubtitlePortraitCheckRunner:MonoBehaviour {} }
