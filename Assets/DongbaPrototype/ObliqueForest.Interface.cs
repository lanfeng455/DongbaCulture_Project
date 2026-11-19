using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DongbaPrototype {public partial class ObliqueForest {
    Text interactionResult,firstStepHint,objectiveTag;GameObject firstStepPanel;CanvasGroup objectiveFade;
    Being resultTarget;string resultText,lastObjective;float resultUntil,objectiveChangedAt;
    bool taughtMove,taughtJump,taughtAxe,taughtCare,endingAcknowledged;Vector3 tutorialOrigin;
    Image boarHealthFill;int lastBoarHealth=6;float boarHurtAt=-10;
    bool ForestCinematic=>fatherIntroActive||ManualStoryDialogueActive||NarrativeInputLocked||RestorationArrivalLocked||AfterCaptureLocked||journeyStage==JourneyStage.ReturnForKnife||journeyStage==JourneyStage.BoarPass||storyOpeningState==StoryOpeningState.CaptureBeat||storyCurtain>.01f||(noticeTime>0&&!string.IsNullOrEmpty(notice));
    bool InterfaceExploring=>!ForestCinematic&&!DongbaInterface.GameplayBlocked;
    public bool InterfaceEndingReady=>journeyStage==JourneyStage.RestorationComplete&&!ManualStoryDialogueActive&&narrativeBeat==NarrativeBeat.None&&!narrativeClosingDialogue&&!endingAcknowledged;
    public void AcknowledgeEndingInterface(){endingAcknowledged=true;}
    public void RestartFromInterface(){SetStoryModeEnabled(true);RestartRun();}
    void ResetInterfaceRun(){taughtMove=taughtJump=taughtAxe=taughtCare=endingAcknowledged=false;tutorialOrigin=ForestSpawn;resultTarget=null;resultText="";lastObjective=null;lastBoarHealth=6;boarHurtAt=-10;}
    void Care(){
        var target=NearestForestInteraction();if(target==null)return;
        int oldHealth=target.health,oldAge=target.age,oldCount=animals.Count;bool oldCultivated=target.cultivated;
        PerformCare();
        string result=oldHealth<=0&&target.health>0?"Replanted":!oldCultivated&&target.cultivated?"Tended":oldAge<target.age?"Fed — growing":animals.Count>oldCount?"Fed — young animal nearby":null;
        if(result==null)return;
        resultTarget=target;resultText=result;resultUntil=clock+1.8f;taughtCare=true;
    }
    void BuildInteractionPolish(){
        interactionResult=UILabel("Local action result",promptPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-116),new Vector2(312,30),18,"");interactionResult.color=new Color(.29f,.40f,.24f);
        firstStepPanel=uiCoreHints;firstStepHint=uiCoreHints.GetComponentInChildren<Text>();
        var help=uiHelpShortcut.AddComponent<Button>();var graphic=uiHelpShortcut.GetComponent<Image>();graphic.raycastTarget=true;help.targetGraphic=graphic;
        var colors=help.colors;colors.highlightedColor=colors.selectedColor=new Color(.85f,.77f,.63f);colors.pressedColor=new Color(.7f,.6f,.43f);help.colors=colors;
        help.onClick.AddListener(()=>DongbaInterface.Instance?.Open(DongbaInterface.Page.Help));
        if(forestCanvas.GetComponent<GraphicRaycaster>()==null)forestCanvas.gameObject.AddComponent<GraphicRaycaster>();
        DongbaInterface.Instance?.AttachForest(this);ResetInterfaceRun();
    }
    void UpdateInterfaceTutorial(Vector3 move,bool jump,bool attack){
        if(!InterfaceExploring)return;
        if(Vector3.Distance(Feet,tutorialOrigin)>.75f)taughtMove=true;
        if(jump)taughtJump=true;if(attack)taughtAxe=true;
    }
    void RefreshInteractionCard(){
        bool visible=InterfaceExploring&&!AfterCapturePeaceful&&eastPaperProgress<=0&&interactionTarget!=null;
        promptPanel.SetActive(visible);selectionRing.gameObject.SetActive(visible);
        if(!visible)return;
        var b=interactionTarget;selectionRing.position=b.root.position;
        var line=selectionRing.GetComponent<LineRenderer>();line.startColor=line.endColor=DongbaUIStyle.Accent;line.widthMultiplier=.035f;
        // The marker and card use exactly the same target as E; the title does not imply an unverified species.
        hudPromptTitle.text=b.animal?(b.age<2?"Young animal":"Forest animal"):b.health<=0?"Tree stump":"Forest tree";
        string care;
        if(b.animal){
            care=clock<b.careReady?"Eating — please wait":b.age<2?"E   Feed to grow":"E   Feed / new young";
        }else care=b.health<=0?"E   Replant here":b.cultivated?"Already tended":clock<b.careReady?"Growing — please wait":"E   Tend";
        hudPrompt.text=care+(b.health>0?(b.animal?"\nJ   Attack":"\nJ   Chop"):"");
        if(b==careLimitTarget&&clock<careLimitUntil)hudPrompt.text=careLimitText+(b.health>0?(b.animal?"\nJ   Attack":"\nJ   Chop"):"");
        interactionResult.text=b==resultTarget&&clock<resultUntil?resultText:"";
    }
    void RefreshContextHUD(){
        if(forestCanvas==null)return;
        bool explore=InterfaceExploring;
        uiHelpShortcut.SetActive(explore);uiHelpPanel.SetActive(false);uiNoticePanel.SetActive(false);
        bool threatened=damageCooldown>0||health<5;
        if(!threatened&&explore){foreach(var animal in animals)if(animal.health>0&&animal.angry&&Vector3.Distance(Feet,animal.root.position)<4){threatened=true;break;}if(storyBoar!=null&&storyBoar.health>0&&Vector3.Distance(Feet,storyBoar.root.position)<6)threatened=true;}
        hudHealth.transform.parent.gameObject.SetActive(explore&&threatened);
        hudArea.transform.parent.gameObject.SetActive(false);uiMode.gameObject.SetActive(false);
        for(int i=0;i<uiHealthPips.Length;i++)uiHealthPips[i].color=i<health?DongbaUIStyle.Accent:new Color(.37f,.30f,.21f,.2f);
        if(uiObjectivePanel!=null){
            string value=QuietStoryObjective();
            bool show=StoryModeEnabled&&explore&&!string.IsNullOrEmpty(value)&&!(journeyStage==JourneyStage.RestorationComplete&&endingAcknowledged);
            uiObjectivePanel.SetActive(show);journeyObjective.text=value;
            if(value!=lastObjective){lastObjective=value;objectiveChangedAt=clock;}
            if(objectiveTag!=null)objectiveTag.text=clock-objectiveChangedAt<3?"Current objective":"";
            if(objectiveFade!=null)objectiveFade.alpha=Mathf.Lerp(.65f,1,Mathf.Clamp01((clock-objectiveChangedAt)/.3f));
        }
        RefreshInteractionCard();
        string hint="";
        if(explore&&!AfterCapturePeaceful&&!promptPanel.activeSelf){
            if(!taughtMove)hint="WASD / Arrows   Move";
            else if(!taughtJump&&Mathf.Abs(Feet.x-RiverX(Feet.z))<4)hint="SPACE   Jump across the river";
            else if(!taughtAxe&&journeyStage==JourneyStage.Hunting)hint="J   Swing the axe";
            else if(!taughtCare&&IsRestorationEnding)hint="Approach a stump, then press E to replant.";
        }
        if(firstStepHint!=null)firstStepHint.text=hint;uiCoreHints.SetActive(hint.Length>0);
        if(uiBoarPlate!=null&&(!explore||DongbaInterface.GameplayBlocked))uiBoarPlate.SetActive(false);
    }
    void RefreshBoarHealth(){
        if(storyBoar==null||boarHealthFill==null)return;
        if(storyBoar.health<lastBoarHealth)boarHurtAt=clock;lastBoarHealth=storyBoar.health;
        float target=Mathf.Clamp01(storyBoar.health/6f);boarHealthFill.rectTransform.anchorMax=new Vector2(Mathf.Lerp(boarHealthFill.rectTransform.anchorMax.x,target,1-Mathf.Exp(-Time.deltaTime*14)),1);
        boarHealthFill.color=clock-boarHurtAt<.25f?new Color(.77f,.41f,.24f):storyBoar.health<=2?new Color(.64f,.18f,.12f):DongbaUIStyle.Accent;
    }
}}
