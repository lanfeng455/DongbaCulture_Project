using System;
namespace DongbaPrototype {public partial class ObliqueForest {
    string[] manualStoryLines;int manualStoryIndex;Action manualStoryFinished;
    bool ManualStoryDialogueActive=>manualStoryLines!=null;
    static void SplitStoryLine(string line,out string speaker,out string content){int colon=line.IndexOf(':');speaker=colon<0?"":line.Substring(0,colon);content=colon<0?line:line.Substring(colon+1).Trim();}
    void BeginManualStoryDialogue(string[] lines,Action finished=null){
        if(lines==null||lines.Length==0)return;
        manualStoryLines=lines;manualStoryIndex=0;manualStoryFinished=finished;forestSubtitleKey=null;
        attackAge=-1;if(axe!=null)axe.gameObject.SetActive(false);
        notice="";noticeTime=0;
    }
    void AdvanceManualStoryDialogue(){
        if(!ManualStoryDialogueActive)return;
        if(++manualStoryIndex<manualStoryLines.Length){forestSubtitleKey=null;return;}
        var finished=manualStoryFinished;manualStoryLines=null;manualStoryFinished=null;manualStoryIndex=0;forestSubtitleKey=null;
        if(forestSubtitles!=null)forestSubtitles.HideDialogue();finished?.Invoke();
    }
    void BeginRealizationDialogue(){
        BeginManualStoryDialogue(new[]{"Gaolequ: Father should have returned by now.","Gaolequ: Something is wrong. I must go east and find him."},()=>{
            journeyStage=JourneyStage.EastJourney;afterCaptureCaption.gameObject.SetActive(false);heading=UnityEngine.Vector3.right;facing=1;EnsureEastPaperContinuation();Tell("Go east through the woods. Cross the river and keep walking.");
        });
    }
}}
