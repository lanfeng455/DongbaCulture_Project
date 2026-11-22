using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DongbaPrototype {
    public sealed class ForestDialogueGraphProbe : MonoBehaviour {
        float age;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void StartRequested(){
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--dialogue-graph-check")<0)return;
            var go=new GameObject("Dialogue graph verification");DontDestroyOnLoad(go);go.AddComponent<ForestDialogueGraphProbe>();
        }
        void Update(){age+=Time.unscaledDeltaTime;var forest=FindFirstObjectByType<ObliqueForest>();
            if(forest!=null&&forest.DialogueGraphPreviewReady){enabled=false;forest.StartCoroutine(forest.CheckDialogueGraph());}
            else if(age>45){Debug.LogError("Dialogue graph check: forest did not initialize.");Application.Quit(1);}
        }
    }
    public partial class ObliqueForest {
        internal IEnumerator CheckDialogueGraph(){
            const string folder="Verification/dialogue-graph";Directory.CreateDirectory(folder);
            var log=new List<string>();int failures=0;
            Action<string,bool> check=(name,ok)=>{log.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;};
            var oldLocale=DongbaLanguage.Current;
            forestDialogueGraph=Instantiate(Resources.Load<ForestDialogueGraph>("ForestCaptureGraph"));
            var originalGraph=forestDialogueGraph;
            var question=forestDialogueGraph.Find("shu_question");string en=question.en,zh=question.zh;
            question.en="Is this your knife? [node test]";question.zh="这是你的刀？（节点验证）";
            PreviewDialogueGraphCapture(true);
            float until=Time.realtimeSinceStartup+6;
            while(ActiveDialogueNode!="shu_question"&&Time.realtimeSinceStartup<until)yield return null;
            check("Confrontation uses the graph's dialogue node",ActiveDialogueNode=="shu_question"&&ManualStoryDialogueActive);
            DongbaLanguage.Set(DongbaLocale.Chinese,false);RefreshForestSubtitles();yield return null;
            check("Edited Chinese text reaches the actual shared subtitles",forestSubtitles.SubtitleText.text.Contains(question.zh));
            DongbaLanguage.Set(DongbaLocale.English,false);yield return null;
            check("Language switch uses edited English, without stale catalogue lookup",forestSubtitles.SubtitleText.text.Contains(question.en));
            question.en=en;question.zh=zh;forestSubtitleKey=null;RefreshForestSubtitles();yield return null;CaptureSceneToTexture(folder+"/question.png");
            AdvanceManualStoryDialogue();yield return new WaitForSeconds(.5f);
            check("One continue enters the full silence beat",ActiveDialogueNode=="shu_silence"&&!ManualStoryDialogueActive);
            until=Time.realtimeSinceStartup+3;while(ActiveDialogueNode!="shu_accusation"&&Time.realtimeSinceStartup<until)yield return null;
            check("Wait automatically reaches accusation, not the capture",ActiveDialogueNode=="shu_accusation"&&!shuConfrontationFinished);
            CaptureSceneToTexture(folder+"/accusation.png");AdvanceManualStoryDialogue();
            check("Continuing accusation releases exactly the existing capture event",shuConfrontationFinished&&!GraphRunning);
            until=Time.realtimeSinceStartup+12;while(storyFatherRoot.gameObject.activeSelf&&Time.realtimeSinceStartup<until)yield return null;
            check("Father is physically carried out of view before hiding",!storyFatherRoot.gameObject.activeSelf);
            PreviewDialogueGraphCapture();yield return new WaitForSeconds(.45f);
            check("Loss entry starts with the original noticed-knife pause",ActiveDialogueNode=="knife_pause"&&!ManualStoryDialogueActive);
            var feet=storyFatherRoot.position;yield return new WaitForSeconds(.35f);
            check("Father stays still during the initial pause",Vector3.Distance(feet,storyFatherRoot.position)<.02f);
            until=Time.realtimeSinceStartup+8;int spoken=0;
            while(Time.realtimeSinceStartup<until&&(GraphRunning||spoken==0)){
                if(ManualStoryDialogueActive){spoken++;AdvanceManualStoryDialogue();yield return null;}else yield return null;
            }
            check("Three loss lines complete before search resumes",spoken==3&&!GraphRunning&&returnWait==0);
            bool jumped=false;until=Time.realtimeSinceStartup+32;
            while(ActiveDialogueNode!="shu_question"&&Time.realtimeSinceStartup<until){jumped|=fatherJumpAge>=0;yield return null;}
            check("Existing father crossing still jumps across the river",jumped&&storyFatherRoot.position.x>RiverX(storyFatherRoot.position.z));
            check("Search arrival triggers the second graph entry",ActiveDialogueNode=="shu_question");
            DongbaLanguage.Set(DongbaLocale.Chinese,false);forestSubtitleKey=null;RefreshForestSubtitles();yield return null;CaptureSceneToTexture(folder+"/question-zh.png");
            RestartRun();check("Restart clears graph, dialogue and capture state",!GraphRunning&&!ManualStoryDialogueActive&&!shuConfrontationFinished&&fatherIntroActive);
            forestDialogueGraph=null;Destroy(originalGraph);DongbaLanguage.Set(oldLocale,false);
            File.WriteAllLines(folder+"/runtime-checks.txt",log);Debug.Log(string.Join("\n",log));Application.Quit(failures==0?0:1);
        }
    }
}
