using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckQuietUI(){
        yield return new WaitForSeconds(1);var folder="Verification/ui-polish";Directory.CreateDirectory(folder);var report=new StringBuilder();
        var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)}){
            Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);
            SetStoryModeEnabled(true);RestartRun();tonePreviewFreezeAnimals=true;yield return new WaitForSeconds(.2f);
            CaptureSceneToTexture(folder+"/dialogue-"+size.x+".png",Screen.width,Screen.height);CheckQuietTextLayout("Dialogue "+Screen.width+"x"+Screen.height,report);
            report.AppendLine("Health label on start="+(hudHealth.text=="Health"));
            report.AppendLine("Dialogue hides locked interaction="+(!promptPanel.activeSelf&&!uiHelpPanel.activeSelf));
            SetStoryModeEnabled(false);RestartRun();tonePreviewFreezeAnimals=true;noticeTime=0;notice="";health=3;
            var tree=plants.Find(p=>p.tree&&p.health>0);player.position=tree.home+new Vector3(1.65f,0,-.45f);FollowPlayerCamera(100);yield return new WaitForSeconds(.2f);
            CaptureSceneToTexture(folder+"/exploration-"+size.x+".png",Screen.width,Screen.height);CheckQuietTextLayout("Explore "+Screen.width+"x"+Screen.height,report);
            report.AppendLine("Nearby interaction card is visible="+promptPanel.activeSelf);
            report.AppendLine("Health label after damage="+(hudHealth.text=="Health"));
            report.AppendLine("Health uses three filled of five ink dots="+(uiHealthPips.Length==5&&uiHealthPips[2].color.a>.9f&&uiHealthPips[3].color.a<.3f&&!hudHealth.text.Contains("|")));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.H));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            CaptureSceneToTexture(folder+"/controls-"+size.x+".png",Screen.width,Screen.height);CheckQuietTextLayout("Help "+Screen.width+"x"+Screen.height,report);report.AppendLine("H opens controls="+uiHelpPanel.activeSelf);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.H));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;report.AppendLine("H closes controls="+!uiHelpPanel.activeSelf);
            SetStoryModeEnabled(true);RestartRun();tonePreviewFreezeAnimals=true;for(int i=0;i<3;i++)AdvanceFatherIntro();journeyStage=JourneyStage.Boar;SpawnStoryBoar();player.position=BoarClearing+Vector3.left*3;FollowPlayerCamera(100);yield return new WaitForSeconds(.2f);
            CaptureSceneToTexture(folder+"/boar-"+size.x+".png",Screen.width,Screen.height);CheckQuietTextLayout("Boar "+Screen.width+"x"+Screen.height,report);
            BeginHearthWaiting();waitingAge=HearthHalfDaySeconds*1.5f;storyFatherRoot.gameObject.SetActive(false);yield return new WaitForSeconds(.2f);
            CaptureSceneToTexture(folder+"/waiting-"+size.x+".png",Screen.width,Screen.height);CheckQuietTextLayout("Waiting "+Screen.width+"x"+Screen.height,report);
        }
        File.WriteAllText(folder+"/checks.txt",report.ToString());Application.Quit();
    }
    void CheckQuietTextLayout(string label,StringBuilder report){
        Canvas.ForceUpdateCanvases();bool fits=true,safe=true,readable=true;var corners=new Vector3[4];int count=0;
        foreach(var textLabel in forestCanvas.GetComponentsInChildren<Text>()){
            if(!textLabel.gameObject.activeInHierarchy||textLabel.color.a<.01f||string.IsNullOrEmpty(textLabel.text))continue;count++;
            if(textLabel.preferredHeight>textLabel.rectTransform.rect.height+1){fits=false;report.AppendLine("OVERFLOW: "+label+" / "+textLabel.name+" preferred="+textLabel.preferredHeight+" available="+textLabel.rectTransform.rect.height);}
            readable&=textLabel.fontSize>=16&&!textLabel.resizeTextForBestFit;
            textLabel.rectTransform.GetWorldCorners(corners);foreach(var point in corners){var screen=RectTransformUtility.WorldToScreenPoint(cam,point);if(screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)safe=false;}
        }
        report.AppendLine(label+": text fits="+fits+"; screen-safe="+safe+"; font sizes >=16="+readable+"; visible labels="+count);
    }
}}
