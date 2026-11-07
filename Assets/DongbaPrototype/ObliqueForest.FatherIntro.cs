using UnityEngine;using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool fatherIntroActive;int fatherIntroLine;GameObject fatherIntroPanel;Text fatherIntroText;
    static readonly string[] FatherIntroLines={"Father: Let us leave the hearth and go hunting in the woods.","Gaolequ: I am ready, Father. Let us go together.","Father: Stay close. I will hunt nearby; you search the woods."};
    void AdvanceFatherIntro(){if(!fatherIntroActive)return;fatherIntroLine++;if(fatherIntroLine>=FatherIntroLines.Length){fatherIntroActive=false;fatherIntroLine=0;Tell("Father: Let us begin.");}}
    void DrawFatherIntro(){
        if(fatherIntroPanel==null&&forestCanvas!=null){var panel=CreateQuietDialogue("Father opening conversation",out uiIntroSpeaker,out fatherIntroText,out uiIntroContinue,out uiIntroProgress);fatherIntroPanel=panel.gameObject;}
        if(fatherIntroPanel==null)return;fatherIntroPanel.SetActive(!UseSharedSubtitles&&StoryModeEnabled&&fatherIntroActive);
        if(fatherIntroActive){var line=FatherIntroLines[Mathf.Clamp(fatherIntroLine,0,FatherIntroLines.Length-1)];int colon=line.IndexOf(':');uiIntroSpeaker.text=colon>=0?line.Substring(0,colon).ToUpperInvariant():"FATHER";fatherIntroText.text=colon>=0?line.Substring(colon+1).Trim():line;uiIntroProgress.text=(fatherIntroLine+1)+" / "+FatherIntroLines.Length;}
    }
}}
