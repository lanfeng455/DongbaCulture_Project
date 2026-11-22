using UnityEngine;using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool fatherIntroActive;int fatherIntroLine;GameObject fatherIntroPanel;Text fatherIntroText;
    void AdvanceFatherIntro(){if(!fatherIntroActive)return;fatherIntroLine++;if(fatherIntroLine>=FatherIntroLines.Length){fatherIntroActive=false;fatherIntroLine=0;}}
    void DrawFatherIntro(){
        if(fatherIntroPanel==null&&forestCanvas!=null){var panel=CreateQuietDialogue("Father opening conversation",out uiIntroSpeaker,out fatherIntroText,out uiIntroContinue,out uiIntroProgress);fatherIntroPanel=panel.gameObject;}
        if(fatherIntroPanel==null)return;fatherIntroPanel.SetActive(!UseSharedSubtitles&&StoryModeEnabled&&fatherIntroActive);
        if(fatherIntroActive){var line=FatherIntroLines[Mathf.Clamp(fatherIntroLine,0,FatherIntroLines.Length-1)];int colon=line.IndexOf(':');uiIntroSpeaker.text=colon>=0?line.Substring(0,colon).ToUpperInvariant():"FATHER";fatherIntroText.text=colon>=0?line.Substring(colon+1).Trim():line;uiIntroProgress.text=(fatherIntroLine+1)+" / "+FatherIntroLines.Length;}
    }
}}
