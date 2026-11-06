using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckSharedSubtitles(){
        yield return new WaitForSeconds(1);var folder="Verification/shared-subtitles";Directory.CreateDirectory(folder);var log=new StringBuilder();
        SetStoryModeEnabled(true);RestartRun();yield return new WaitForSeconds(.7f);
        var display=forestSubtitles;var font=display.SubtitleText.font;log.AppendLine("Shared component and font loaded="+(display!=null&&font!=null&&font.name.Contains("NotoSerifCJKsc")));
        CaptureSceneToTexture(folder+"/typing.png");
        bool typing=display.IsTyping;var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
        int line=fatherIntroLine;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.1f);
        log.AppendLine("First E reveals current line="+(typing&&fatherIntroLine==line&&!display.IsTyping));CaptureSceneToTexture(folder+"/opening.png");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.1f);
        log.AppendLine("Next E advances dialogue="+(fatherIntroLine==line+1));log.AppendLine("Legacy dialogue hidden="+!fatherIntroPanel.activeSelf);
        bool chinese=true;foreach(char c in "父亲高勒趣火塘署")chinese&=font.HasCharacter(c,true,true);log.AppendLine("Chinese glyphs available="+chinese);
        display.PlayLine("父亲","我们从<color=#FFDC88>火塘</color>边出发。");yield return new WaitForSeconds(1.2f);display.CompleteCurrentLine();yield return new WaitForSeconds(.1f);CaptureSceneToTexture(folder+"/chinese-rich-text.png");
        log.AppendLine("Chinese rich text preserved="+(display.SubtitleText.text.Contains("<color=#FFDC88>")&&display.SubtitleText.textInfo.characterCount>0));
        RestartRun();yield return new WaitForSeconds(.1f);log.AppendLine("Restart returns to opening subtitle="+(fatherIntroActive&&fatherIntroLine==0&&display.SubtitleText.text.Contains("hunting")));
        for(int i=0;i<3;i++)AdvanceFatherIntro();BeginHearthWaiting();yield return new WaitForSeconds(.4f);int shown=display.SubtitleText.maxVisibleCharacters;yield return new WaitForSeconds(.5f);
        log.AppendLine("Repeated waiting update does not restart typewriter="+(display.SubtitleText.maxVisibleCharacters>shown));display.CompleteCurrentLine();yield return new WaitForSeconds(.1f);CaptureSceneToTexture(folder+"/waiting.png");
        log.AppendLine("Waiting hides duplicate caption="+(!afterCaptureCaption.enabled&&!storyCaptionPaper.gameObject.activeSelf));
        journeyStage=JourneyStage.Realization;realizationAge=0;yield return new WaitForSeconds(2.8f);log.AppendLine("Timed realization finishes typing before departure="+(!display.IsTyping&&display.SubtitleText.text.Contains("find him")));CaptureSceneToTexture(folder+"/realization.png");
        fatherIntroActive=false;journeyStage=JourneyStage.EastJourney;EnsureEastPaperContinuation();player.position=new Vector3(36,0,0);FollowPlayerCamera(100);yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/blank-paper.png");
        log.AppendLine("Blank paper clears subtitle and hint="+(!display.SubtitleText.gameObject.activeInHierarchy&&!forestSubtitleAdvance.gameObject.activeSelf));
        SetStoryModeEnabled(false);yield return new WaitForSeconds(.5f);log.AppendLine("Sandbox hides shared subtitle="+!display.SubtitleText.gameObject.activeInHierarchy);
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
