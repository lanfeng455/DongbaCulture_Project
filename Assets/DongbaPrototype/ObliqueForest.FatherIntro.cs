using UnityEngine;using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool fatherIntroActive;int fatherIntroLine;GameObject fatherIntroPanel;Text fatherIntroText;
    static readonly string[] FatherIntroLines={"Father: Let us leave home and go hunting in the woods.","Gaolequ: I am ready, Father. Let us go together.","Father: Stay close. I will hunt nearby; you search the woods."};
    void AdvanceFatherIntro(){if(!fatherIntroActive)return;fatherIntroLine++;if(fatherIntroLine>=FatherIntroLines.Length){fatherIntroActive=false;fatherIntroLine=0;Tell("Father: Let us begin.");}}
    void DrawFatherIntro(){
        if(fatherIntroPanel==null&&forestCanvas!=null){var panel=UIBlock("Father opening conversation",forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,105),new Vector2(1050,145));fatherIntroPanel=panel.gameObject;panel.gameObject.AddComponent<Image>().color=new Color(.96f,.92f,.83f,.97f);fatherIntroText=UILabel("Conversation",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(25,-20),new Vector2(1000,110),25,"");}
        if(fatherIntroPanel==null)return;fatherIntroPanel.SetActive(StoryModeEnabled&&fatherIntroActive);if(fatherIntroActive)fatherIntroText.text=FatherIntroLines[Mathf.Clamp(fatherIntroLine,0,FatherIntroLines.Length-1)]+"\n\nE / Enter  Continue";
    }
}}
