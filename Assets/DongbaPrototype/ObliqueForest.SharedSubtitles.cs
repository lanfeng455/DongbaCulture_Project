using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    [Header("Shared team subtitles")]
    public bool UseSharedSubtitles=true;
    [Range(10,90)] public float ForestSubtitleCharactersPerSecond=48;
    DialogueDisplay forestSubtitles;
    string forestSubtitleKey;
    Text forestSubtitleAdvance;
    void EnsureForestSubtitles(){
        if(forestSubtitles!=null)return;
        forestSubtitles=DialogueDisplay.Instance;
        if(forestSubtitles==null)forestSubtitles=new GameObject("Shared team story subtitles").AddComponent<DialogueDisplay>();
        forestSubtitles.SetRenderCamera(cam);forestSubtitles.SetCharactersPerSecond(ForestSubtitleCharactersPerSecond);
        forestSubtitleAdvance=UILabel("Shared subtitle advance hint",forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(600,30),18,"ENTER   Continue");forestSubtitleAdvance.alignment=TextAnchor.MiddleCenter;forestSubtitleAdvance.color=uiMuted;
    }
    bool TryCompleteForestSubtitle(){return UseSharedSubtitles&&forestSubtitles!=null&&forestSubtitles.CompleteCurrentLine();}
    void ResetForestSubtitles(){manualStoryLines=null;manualStoryFinished=null;manualStoryIndex=0;forestSubtitleKey=null;if(forestSubtitles!=null)forestSubtitles.HideDialogue();if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);}
    void RefreshForestSubtitles(){
        if(!UseSharedSubtitles&&!fatherIntroActive&&!ManualStoryDialogueActive){if(forestSubtitleKey!=null)ResetForestSubtitles();if(afterCaptureCaption!=null)afterCaptureCaption.enabled=true;return;}
        EnsureForestSubtitles();string speaker="",content="";
        if(StoryModeEnabled){
            if(ManualStoryDialogueActive){SplitStoryLine(manualStoryLines[manualStoryIndex],out speaker,out content);}
            else if(fatherIntroActive){
                var line=FatherIntroLines[Mathf.Clamp(fatherIntroLine,0,FatherIntroLines.Length-1)];int colon=line.IndexOf(':');speaker=colon<0?"Father":line.Substring(0,colon);content=colon<0?line:line.Substring(colon+1).Trim();
            }else if(journeyStage==JourneyStage.WaitingAtHearth||journeyStage==JourneyStage.Realization){content=afterCaptureCaption!=null?afterCaptureCaption.text:"";}

        }
        // Preserve the old presenter as a fallback but never render both at the same time.
        if(fatherIntroPanel!=null)fatherIntroPanel.SetActive(false);
        if(afterCaptureCaption!=null)afterCaptureCaption.enabled=false;
        if(storyCaptionPaper!=null)storyCaptionPaper.gameObject.SetActive(false);
        forestSubtitleAdvance.gameObject.SetActive(StoryModeEnabled&&(fatherIntroActive||ManualStoryDialogueActive));
        if(content.Length>0){
            if(uiNoticePanel!=null)uiNoticePanel.SetActive(false);
            string key=speaker+"\n"+content;
            if(forestSubtitleKey!=key){forestSubtitleKey=key;forestSubtitles.PlayLine(speaker,content);if(fatherIntroActive||ManualStoryDialogueActive)forestSubtitles.CompleteCurrentLine();}
        }else if(forestSubtitleKey!=null){forestSubtitleKey=null;forestSubtitles.HideDialogue();}
    }
}}
